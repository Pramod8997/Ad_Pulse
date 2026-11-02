using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AdPulse.Api.Errors;

public static class ErrorHandlingExtensions
{
    public static IApplicationBuilder UseAdPulseErrorHandling(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<ApiErrorHandlingMiddleware>();
    }
}

public sealed class ApiErrorHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Action<ILogger, string, string, Exception?> LogUnhandledException =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(1, nameof(LogUnhandledException)),
            "Unhandled API exception. CorrelationId: {CorrelationId}; ExceptionType: {ExceptionType}");

    private readonly RequestDelegate _next;
    private readonly ILogger<ApiErrorHandlingMiddleware> _logger;

    public ApiErrorHandlingMiddleware(RequestDelegate next, ILogger<ApiErrorHandlingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ApiProblemDetails.GetOrCreateCorrelationId(context);
        context.Response.Headers[ApiProblemDetails.CorrelationHeader] = correlationId;

        try
        {
            await _next(context);

            if (!context.Response.HasStarted
                && context.Response.StatusCode >= 400
                && context.Response.ContentType is null)
            {
                await WriteProblemAsync(context, ApiProblemDetails.FromStatusCode(context.Response.StatusCode, context));
            }
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            var problem = ApiProblemDetails.FromException(exception, context);
            LogUnhandledException(_logger, correlationId, exception.GetType().Name, null);
            await WriteProblemAsync(context, problem);
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, Microsoft.AspNetCore.Mvc.ProblemDetails problem)
    {
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = ApiProblemDetails.ContentType;
        context.Response.Headers[ApiProblemDetails.CorrelationHeader] = ApiProblemDetails.GetOrCreateCorrelationId(context);
        await JsonSerializer.SerializeAsync(context.Response.Body, problem, JsonOptions, context.RequestAborted);
    }
}
