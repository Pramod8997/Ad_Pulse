using System.Diagnostics;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AdPulse.Api.Observability;

public sealed class CorrelationMiddleware(RequestDelegate next, ILogger<CorrelationMiddleware> logger)
{
    private static readonly Action<ILogger, string, string, Exception?> RequestStarted =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(100, nameof(RequestStarted)),
            "HTTP request started {HttpMethod} {HttpPath}");

    private static readonly Action<ILogger, string, string, int, long, Exception?> RequestCompleted =
        LoggerMessage.Define<string, string, int, long>(
            LogLevel.Information,
            new EventId(101, nameof(RequestCompleted)),
            "HTTP request completed {HttpMethod} {HttpPath} {HttpStatusCode} {DurationMilliseconds}");

    private static readonly Action<ILogger, string, string, string, long, Exception?> RequestFailed =
        LoggerMessage.Define<string, string, string, long>(
            LogLevel.Error,
            new EventId(102, nameof(RequestFailed)),
            "HTTP request failed {HttpMethod} {HttpPath} {ErrorType} {DurationMilliseconds}");

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = CorrelationId.Resolve(context.Request.Headers[ObservabilityExtensions.CorrelationHeaderName].FirstOrDefault());
        context.Response.Headers[ObservabilityExtensions.CorrelationHeaderName] = correlationId;

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["correlation_id"] = correlationId
        });

        var stopwatch = Stopwatch.StartNew();
        RequestStarted(
            logger,
            context.Request.Method,
            context.Request.Path.Value ?? "/",
            null);

        try
        {
            await next(context);
            RequestCompleted(
                logger,
                context.Request.Method,
                context.Request.Path.Value ?? "/",
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                null);
        }
        catch (Exception exception)
        {
            RequestFailed(
                logger,
                context.Request.Method,
                context.Request.Path.Value ?? "/",
                exception.GetType().Name,
                stopwatch.ElapsedMilliseconds,
                exception);
            throw;
        }
    }
}

internal static class CorrelationId
{
    public static string Resolve(string? candidate)
    {
        if (!string.IsNullOrEmpty(candidate)
            && candidate.Length <= 128
            && candidate.All(IsSafeCharacter))
        {
            return candidate;
        }

        return Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
    }

    private static bool IsSafeCharacter(char value) =>
        value is >= 'a' and <= 'z'
            or >= 'A' and <= 'Z'
            or >= '0' and <= '9'
            or '-' or '_' or '.' or '~';
}
