using System.Diagnostics;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AdPulse.Api.Observability;

public sealed class CorrelationMiddleware(RequestDelegate next, ILogger<CorrelationMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = CorrelationId.Resolve(context.Request.Headers[ObservabilityExtensions.CorrelationHeaderName].FirstOrDefault());
        context.Response.Headers[ObservabilityExtensions.CorrelationHeaderName] = correlationId;

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["correlation_id"] = correlationId
        });

        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation(
            "HTTP request started {HttpMethod} {HttpPath}",
            context.Request.Method,
            context.Request.Path.Value ?? "/");

        try
        {
            await next(context);
            logger.LogInformation(
                "HTTP request completed {HttpMethod} {HttpPath} {HttpStatusCode} {DurationMilliseconds}",
                context.Request.Method,
                context.Request.Path.Value ?? "/",
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception)
        {
            logger.LogError(
                "HTTP request failed {HttpMethod} {HttpPath} {ErrorType} {DurationMilliseconds}",
                context.Request.Method,
                context.Request.Path.Value ?? "/",
                exception.GetType().Name,
                stopwatch.ElapsedMilliseconds);
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
