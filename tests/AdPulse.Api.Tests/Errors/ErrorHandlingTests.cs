using System.Text.Json;
using AdPulse.Api.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace AdPulse.Api.Tests.Errors;

public sealed class ErrorHandlingTests
{
    [Fact]
    public async Task Propagates_a_safe_correlation_id_to_success_responses()
    {
        var response = await InvokeAsync(_ => Task.CompletedTask, "request-123");

        Assert.Equal("request-123", response.Response.Headers[ApiProblemDetails.CorrelationHeader]);
    }

    [Fact]
    public async Task Includes_the_same_correlation_id_in_problem_responses()
    {
        var response = await InvokeAsync(_ => throw new InvalidOperationException("SQL token uploaded row"), "request-123");
        var body = await ReadBodyAsync(response);

        Assert.Equal(StatusCodes.Status500InternalServerError, response.Response.StatusCode);
        Assert.Equal("request-123", response.Response.Headers[ApiProblemDetails.CorrelationHeader]);
        Assert.Equal("request-123", body.RootElement.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task Suppresses_sensitive_exception_detail()
    {
        const string sensitive = "SELECT * FROM Campaigns; bearer-token; raw uploaded row contents";
        var response = await InvokeAsync(_ => throw new InvalidOperationException(sensitive), null);
        var body = await ReadBodyAsync(response);
        var json = body.RootElement.ToString();

        Assert.Equal("An unexpected error occurred.", body.RootElement.GetProperty("detail").GetString());
        Assert.DoesNotContain(sensitive, json, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bearer-token", json, StringComparison.Ordinal);
        Assert.Equal(ApiProblemDetails.ContentType, response.Response.ContentType);
    }

    [Theory]
    [InlineData(typeof(ApiValidationException), StatusCodes.Status400BadRequest)]
    [InlineData(typeof(ApiAuthorizationException), StatusCodes.Status403Forbidden)]
    [InlineData(typeof(ApiNotFoundException), StatusCodes.Status404NotFound)]
    [InlineData(typeof(ApiConcurrencyException), StatusCodes.Status409Conflict)]
    public async Task Maps_safe_application_errors_to_problem_statuses(Type exceptionType, int statusCode)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;
        var response = await InvokeAsync(_ => throw exception, null);

        Assert.Equal(statusCode, response.Response.StatusCode);
        Assert.Equal(ApiProblemDetails.ContentType, response.Response.ContentType);
    }

    private static async Task<DefaultHttpContext> InvokeAsync(RequestDelegate next, string? correlationId)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        if (correlationId is not null)
        {
            context.Request.Headers[ApiProblemDetails.CorrelationHeader] = correlationId;
        }

        var middleware = new ApiErrorHandlingMiddleware(next, NullLogger<ApiErrorHandlingMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        context.Response.Body.Position = 0;
        return context;
    }

    private static async Task<JsonDocument> ReadBodyAsync(DefaultHttpContext context)
    {
        return await JsonDocument.ParseAsync(context.Response.Body);
    }
}
