using System.ComponentModel.DataAnnotations;
using System.Security;
using AdPulse.Domain.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdPulse.Api.Errors;

public static class ApiProblemDetails
{
    public const string ContentType = "application/problem+json";
    public const string CorrelationHeader = "X-Correlation-ID";

    private const int MaximumCorrelationIdLength = 128;

    public static string GetOrCreateCorrelationId(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Items[CorrelationHeader] is string existing)
        {
            return existing;
        }

        var requested = context.Request.Headers[CorrelationHeader];
        var correlationId = IsSafeCorrelationId(requested) ? requested.ToString() : Guid.NewGuid().ToString("N");
        context.Items[CorrelationHeader] = correlationId;
        return correlationId;
    }

    public static ProblemDetails FromException(Exception exception, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(context);

        var (status, type, title, detail) = exception switch
        {
            ApiValidationException or ValidationException or ArgumentException or BadHttpRequestException
                when IsNamed(exception, "CampaignValidationException", "ImportValidationException", "ImportFileTooLargeException")
                || exception is ApiValidationException or ValidationException or ArgumentException or BadHttpRequestException
                => (StatusCodes.Status400BadRequest, "/problems/validation", "Request validation failed", "One or more request values are invalid."),
            ApiAuthorizationException or TenantAccessDeniedException or UnauthorizedAccessException or SecurityException
                => (StatusCodes.Status403Forbidden, "/problems/forbidden", "Access denied", "You are not authorized to perform this operation."),
            ApiNotFoundException or KeyNotFoundException
                => (StatusCodes.Status404NotFound, "/problems/not-found", "Resource not found", "The requested resource was not found."),
            ApiConcurrencyException or DbUpdateConcurrencyException
                when IsNamed(exception, "AlertConflictException", "ImportConflictException")
                || exception is ApiConcurrencyException or DbUpdateConcurrencyException
                => (StatusCodes.Status409Conflict, "/problems/concurrency", "Resource conflict", "The resource was changed by another request. Retry with the latest version."),
            _ when IsNamed(exception, "CampaignValidationException", "ImportValidationException", "ImportFileTooLargeException")
                => (StatusCodes.Status400BadRequest, "/problems/validation", "Request validation failed", "One or more request values are invalid."),
            _ when IsNamed(exception, "AlertConflictException", "ImportConflictException")
                => (StatusCodes.Status409Conflict, "/problems/concurrency", "Resource conflict", "The resource was changed by another request. Retry with the latest version."),
            _ => (StatusCodes.Status500InternalServerError, "/problems/internal", "Unexpected error", "An unexpected error occurred.")
        };

        return Create(status, type, title, detail, context);
    }

    public static ProblemDetails FromStatusCode(int statusCode, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var (type, title, detail) = statusCode switch
        {
            StatusCodes.Status400BadRequest => ("/problems/validation", "Request validation failed", "One or more request values are invalid."),
            StatusCodes.Status401Unauthorized => ("/problems/unauthorized", "Authentication required", "Authentication is required to perform this operation."),
            StatusCodes.Status403Forbidden => ("/problems/forbidden", "Access denied", "You are not authorized to perform this operation."),
            StatusCodes.Status404NotFound => ("/problems/not-found", "Resource not found", "The requested resource was not found."),
            StatusCodes.Status409Conflict => ("/problems/concurrency", "Resource conflict", "The resource was changed by another request. Retry with the latest version."),
            _ when statusCode >= 500 => ("/problems/internal", "Unexpected error", "An unexpected error occurred."),
            _ => ("/problems/request", "Request failed", "The request could not be completed.")
        };

        return Create(statusCode, type, title, detail, context);
    }

    private static ProblemDetails Create(int status, string type, string title, string detail, HttpContext context)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Type = type,
            Title = title,
            Detail = detail
        };
        problem.Extensions["correlationId"] = GetOrCreateCorrelationId(context);
        return problem;
    }

    private static bool IsSafeCorrelationId(Microsoft.Extensions.Primitives.StringValues value)
    {
        if (value.Count != 1 || value[0] is not { Length: > 0 and <= MaximumCorrelationIdLength } candidate)
        {
            return false;
        }

        foreach (var character in candidate)
        {
            if (!(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsNamed(Exception exception, params string[] names) =>
        names.Contains(exception.GetType().Name, StringComparer.Ordinal);
}
