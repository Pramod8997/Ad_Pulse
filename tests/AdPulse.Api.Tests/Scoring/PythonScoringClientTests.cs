using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AdPulse.Api.Scoring;
using AdPulse.Domain.Analytics;
using Microsoft.Extensions.Options;
using Xunit;

namespace AdPulse.Api.Tests.Scoring;

public sealed class PythonScoringClientTests
{
    [Fact]
    public async Task Sends_scoring_v1_json_and_maps_response()
    {
        HttpRequestMessage? sentRequest = null;
        string? sentBody = null;
        var handler = new RecordingHandler((request, _) =>
        {
            sentRequest = request;
            sentBody = request.Content!.ReadAsStringAsync(CancellationToken.None).GetAwaiter().GetResult();
            return Task.FromResult(JsonResponse("""
                {"score":"12.3451","severity":"low","confidence":"1.0000","data_sufficiency":"sufficient","reason_codes":["degraded_vs_baseline"],"fingerprint":"cf567acbd1230f8caccdbb8e8c3c0a8d954ea2b8d39ffa3a593f1c101ad5c63f"}
                """));
        });
        var client = CreateClient(handler);

        var result = await client.ScoreAsync(Request());

        Assert.Equal(12.3451m, result.Score);
        Assert.Equal(ScoringSeverity.Low, result.Severity);
        Assert.NotNull(sentRequest);
        Assert.Equal(HttpMethod.Post, sentRequest!.Method);
        Assert.Equal("/v1/score", sentRequest.RequestUri!.AbsolutePath);
        Assert.Equal("application/json", sentRequest.Content!.Headers.ContentType!.MediaType);
        Assert.Contains("\"tenant_id\":\"tenant-a\"", sentBody, StringComparison.Ordinal);
        Assert.Contains("\"observed\":\"2.5\"", sentBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Retries_transient_status_but_not_client_error()
    {
        var transientCalls = 0;
        var transientHandler = new RecordingHandler((_, _) =>
        {
            transientCalls++;
            return Task.FromResult(transientCalls == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : JsonResponse("""
                    {"score":"0.0000","severity":"none","confidence":"1.0000","data_sufficiency":"sufficient","reason_codes":["within_baseline"],"fingerprint":"e4ad1cdb9f542791ffc036de21f9e8317e2fb71f23e056ea5d21ee4a87ebbc43"}
                    """));
        });

        var result = await CreateClient(transientHandler).ScoreAsync(Request());

        Assert.Equal(ScoringSeverity.None, result.Severity);
        Assert.Equal(2, transientCalls);

        var clientErrorCalls = 0;
        var clientErrorHandler = new RecordingHandler((_, _) =>
        {
            clientErrorCalls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
        });

        await Assert.ThrowsAsync<ScoringClientException>(() => CreateClient(clientErrorHandler).ScoreAsync(Request()));
        Assert.Equal(1, clientErrorCalls);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_retried_or_converted_to_timeout()
    {
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RecordingHandler(async (_, token) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var task = CreateClient(handler).ScoreAsync(Request(), cancellation.Token);
        await entered.Task;
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Applies_operation_timeout_without_retrying_timeout()
    {
        var handler = new RecordingHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var client = new PythonScoringClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://scoring.test") },
            Options.Create(new ScoringClientOptions
            {
                Timeout = TimeSpan.FromMilliseconds(20),
                RetryDelay = TimeSpan.Zero
            }));

        await Assert.ThrowsAsync<TimeoutException>(() => client.ScoreAsync(Request()));
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Rejects_oversized_response_without_logging_body()
    {
        var options = new ScoringClientOptions { MaxResponseBytes = 1024, RetryDelay = TimeSpan.Zero };
        var body = new string('x', 1025);
        var handler = new RecordingHandler((_, _) => Task.FromResult(JsonResponse(body)));
        var client = new PythonScoringClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://scoring.test") },
            Options.Create(options));

        var exception = await Assert.ThrowsAsync<ScoringClientException>(() => client.ScoreAsync(Request()));

        Assert.DoesNotContain(body, exception.Message, StringComparison.Ordinal);
    }

    private static PythonScoringClient CreateClient(HttpMessageHandler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("http://scoring.test") },
        Options.Create(new ScoringClientOptions { RetryDelay = TimeSpan.Zero }));

    private static ScoringRequest Request() => new(
        "tenant-a",
        "campaign-1",
        "cpc",
        2.5m,
        1m,
        30,
        7,
        MetricDirection.LowerIsBetter,
        new DateOnly(2026, 1, 1),
        new DateOnly(2026, 1, 7));

    private static HttpResponseMessage JsonResponse(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };

    private sealed class RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return handler(request, cancellationToken);
        }
    }
}
