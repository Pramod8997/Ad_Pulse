using AdPulse.Domain.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AdPulse.Api.Alerts;

public static class AlertEndpoints
{
    public static IServiceCollection AddAlerts(this IServiceCollection services) => services.AddScoped<AlertService>();

    public static RouteGroupBuilder MapAlertEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/app/alerts").RequireAuthorization();
        group.MapGet("", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPatch("/{id:guid}", ChangeStatusAsync);
        group.MapPost("/{id:guid}/acknowledge", AcknowledgeAsync);
        group.MapPost("/{id:guid}/resolve", ResolveAsync);
        group.MapPost("/{id:guid}/reopen", ReopenAsync);
        return group;
    }

    private static Task<IResult> ListAsync(
        [AsParameters] AlertListQuery query,
        HttpContext httpContext,
        TenantContextResolver tenantResolver,
        AlertService service,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            httpContext,
            tenantResolver,
            cancellationToken,
            tenant => service.ListAsync(tenant, query, cancellationToken));

    private static Task<IResult> GetAsync(
        Guid id,
        HttpContext httpContext,
        TenantContextResolver tenantResolver,
        AlertService service,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            httpContext,
            tenantResolver,
            cancellationToken,
            tenant => service.GetAsync(tenant, id, cancellationToken));

    private static Task<IResult> AcknowledgeAsync(
        Guid id,
        AlertMutationRequest? request,
        HttpContext httpContext,
        TenantContextResolver tenantResolver,
        AlertService service,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            httpContext,
            tenantResolver,
            cancellationToken,
            tenant => service.AcknowledgeAsync(tenant, id, request!, cancellationToken));

    private static Task<IResult> ChangeStatusAsync(
        Guid id,
        AlertStatusChangeRequest? request,
        HttpContext httpContext,
        TenantContextResolver tenantResolver,
        AlertService service,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            httpContext,
            tenantResolver,
            cancellationToken,
            tenant => service.ChangeStatusAsync(tenant, id, request!, cancellationToken));

    private static Task<IResult> ResolveAsync(
        Guid id,
        AlertMutationRequest? request,
        HttpContext httpContext,
        TenantContextResolver tenantResolver,
        AlertService service,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            httpContext,
            tenantResolver,
            cancellationToken,
            tenant => service.ResolveAsync(tenant, id, request!, cancellationToken));

    private static Task<IResult> ReopenAsync(
        Guid id,
        AlertMutationRequest? request,
        HttpContext httpContext,
        TenantContextResolver tenantResolver,
        AlertService service,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            httpContext,
            tenantResolver,
            cancellationToken,
            tenant => service.ReopenAsync(tenant, id, request!, cancellationToken));

    private static async Task<IResult> ExecuteAsync<T>(
        HttpContext httpContext,
        TenantContextResolver tenantResolver,
        CancellationToken cancellationToken,
        Func<TenantContext, Task<T>> operation)
    {
        try
        {
            var tenant = await tenantResolver.ResolveAsync(httpContext.User, cancellationToken);
            var result = await operation(tenant);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (TenantAccessDeniedException)
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Access denied.");
        }
        catch (AlertValidationException exception)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: exception.Message);
        }
        catch (AlertConflictException exception)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: exception.Message);
        }
    }
}
