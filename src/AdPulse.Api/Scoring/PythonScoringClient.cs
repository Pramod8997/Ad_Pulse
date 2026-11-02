using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AdPulse.Domain.Analytics;
using Microsoft.Extensions.Options;

namespace AdPulse.Api.Scoring;

public interface IPythonScoringClient
{
    Task<ScoringResult> ScoreAsync(ScoringRequest request, CancellationToken cancellationToken = default);
}

public sealed class ScoringClientException : Exception
{
    public ScoringClientException(string message, HttpStatusCode? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode? StatusCode { get; }
}

public sealed class PythonScoringClient : IPythonScoringClient
{
    private static readonly Regex DecimalResponsePattern = new("^(0|[1-9][0-9]*)(\\.[0-9]{1,4})?$", RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly HttpClient _httpClient;
    private readonly ScoringClientOptions _options;

    public PythonScoringClient(HttpClient httpClient, IOptions<ScoringClientOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _options = options.Value;
        _options.Validate();

        if (_httpClient.BaseAddress is null && _options.BaseAddress is not null)
        {
            _httpClient.BaseAddress = _options.BaseAddress;
        }

        if (_httpClient.BaseAddress is not { IsAbsoluteUri: true } baseAddress
            || baseAddress.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("The scoring HTTP client requires an absolute HTTP or HTTPS base address.");
        }

        // The operation timeout below must remain the only timeout boundary, including across retries.
        _httpClient.Timeout = Timeout.InfiniteTimeSpan;
    }

    public async Task<ScoringResult> ScoreAsync(ScoringRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        var payload = ToHttpRequest(request);
        using var operationTimeout = new CancellationTokenSource(_options.Timeout);
        using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            operationTimeout.Token);

        for (var attempt = 0; attempt <= _options.MaxRetries; attempt++)
        {
            try
            {
                using var message = new HttpRequestMessage(HttpMethod.Post, _options.ScorePath)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(payload, JsonOptions),
                        Encoding.UTF8,
                        "application/json")
                };
                using var response = await _httpClient.SendAsync(
                    message,
                    HttpCompletionOption.ResponseHeadersRead,
                    operationCancellation.Token).ConfigureAwait(false);

                if (IsTransient(response.StatusCode) && attempt < _options.MaxRetries)
                {
                    await DelayBeforeRetryAsync(operationCancellation.Token).ConfigureAwait(false);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new ScoringClientException(
                        $"The scoring service returned HTTP {(int)response.StatusCode}.",
                        response.StatusCode);
                }

                return await ReadResponseAsync(response, operationCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException("The scoring service did not respond before the configured timeout.");
            }
            catch (HttpRequestException) when (attempt < _options.MaxRetries)
            {
                await DelayBeforeRetryAsync(operationCancellation.Token).ConfigureAwait(false);
            }
            catch (HttpRequestException exception)
            {
                throw new ScoringClientException("The scoring service could not be reached.", innerException: exception);
            }
        }

        throw new InvalidOperationException("The scoring retry loop ended unexpectedly.");
    }

    private async Task<ScoringResult> ReadResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var body = new MemoryStream();
            var buffer = new byte[4096];

            while (true)
            {
                var bytesRead = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    break;
                }

                if (body.Length + bytesRead > _options.MaxResponseBytes)
                {
                    throw new ScoringClientException("The scoring service response exceeded the configured size limit.");
                }

                await body.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            }

            var parsed = JsonSerializer.Deserialize<ScoringHttpResponse>(body.ToArray(), JsonOptions);
            return ToDomainResult(parsed);
        }
        catch (JsonException exception)
        {
            throw new ScoringClientException("The scoring service returned an invalid response.", innerException: exception);
        }
    }

    private async Task DelayBeforeRetryAsync(CancellationToken cancellationToken)
    {
        if (_options.RetryDelay > TimeSpan.Zero)
        {
            await Task.Delay(_options.RetryDelay, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.RequestTimeout or
        HttpStatusCode.TooManyRequests or
        HttpStatusCode.InternalServerError or
        HttpStatusCode.BadGateway or
        HttpStatusCode.ServiceUnavailable or
        HttpStatusCode.GatewayTimeout;

    private static ScoringHttpRequest ToHttpRequest(ScoringRequest request) => new(
        request.TenantId,
        request.CampaignId,
        request.MetricName,
        request.Observed.ToString(CultureInfo.InvariantCulture),
        request.Baseline.ToString(CultureInfo.InvariantCulture),
        request.DataPoints,
        request.MinimumDataPoints,
        request.Direction == MetricDirection.HigherIsBetter ? "higher_is_better" : "lower_is_better",
        request.EvidenceWindowStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        request.EvidenceWindowEnd.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        request.ScoringVersion);

    private static ScoringResult ToDomainResult(ScoringHttpResponse? response)
    {
        if (response is null
            || !TryParseDecimal(response.Score, out var score)
            || !TryParseDecimal(response.Confidence, out var confidence)
            || response.ReasonCodes is null
            || response.ReasonCodes is not { Length: >= 1 and <= 4 }
            || response.ReasonCodes.Any(string.IsNullOrWhiteSpace)
            || response.Fingerprint is not { Length: 64 }
            || response.Fingerprint.Any(character => !IsLowerHex(character)))
        {
            throw new ScoringClientException("The scoring service returned an invalid response.");
        }

        var severity = response.Severity switch
        {
            "none" => ScoringSeverity.None,
            "low" => ScoringSeverity.Low,
            "medium" => ScoringSeverity.Medium,
            "high" => ScoringSeverity.High,
            "critical" => ScoringSeverity.Critical,
            _ => throw new ScoringClientException("The scoring service returned an invalid response.")
        };

        if (score is < 0m or > 100m
            || confidence is < 0m or > 1m
            || response.DataSufficiency is not ("sufficient" or "insufficient"))
        {
            throw new ScoringClientException("The scoring service returned an invalid response.");
        }

        return new ScoringResult(score, severity, confidence, response.DataSufficiency, response.ReasonCodes, response.Fingerprint);
    }

    private static bool TryParseDecimal(string? value, out decimal result)
    {
        result = default;
        return value is not null
            && DecimalResponsePattern.IsMatch(value)
            && decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out result);
    }

    private static bool IsLowerHex(char value) =>
        value is >= '0' and <= '9' or >= 'a' and <= 'f';

    private static void ValidateRequest(ScoringRequest request)
    {
        RequireToken(request.TenantId, nameof(request.TenantId), 128);
        RequireToken(request.CampaignId, nameof(request.CampaignId), 128);
        RequireToken(request.MetricName, nameof(request.MetricName), 64);
        RequireToken(request.ScoringVersion, nameof(request.ScoringVersion), 32);
        EnsureNoPipe(request.TenantId, nameof(request.TenantId));
        EnsureNoPipe(request.CampaignId, nameof(request.CampaignId));
        EnsureNoPipe(request.MetricName, nameof(request.MetricName));
        EnsureNoPipe(request.ScoringVersion, nameof(request.ScoringVersion));

        if (!Enum.IsDefined(request.Direction))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Metric direction is invalid.");
        }

        if (request.Observed < 0m || request.Baseline < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Observed and baseline values cannot be negative.");
        }

        if (request.DataPoints is < 0 or > 1_000_000)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Data points must be between 0 and 1,000,000.");
        }

        if (request.MinimumDataPoints is < 1 or > 1_000_000)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Minimum data points must be between 1 and 1,000,000.");
        }

        if (request.EvidenceWindowEnd < request.EvidenceWindowStart)
        {
            throw new ArgumentException("The evidence window end must not precede its start.", nameof(request));
        }
    }

    private static void RequireToken(string value, string name, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength)
        {
            throw new ArgumentException($"The value is required and must be at most {maximumLength} characters.", name);
        }
    }

    private static void EnsureNoPipe(string value, string name)
    {
        if (value.Contains('|', StringComparison.Ordinal))
        {
            throw new ArgumentException("The value cannot contain the fingerprint separator.", name);
        }
    }
}
