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
        group.MapGet("/{id:guid}/timeline", TimelineAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPatch("/{id:guid}", ChangeStatusAsync);
        group.MapPatch("/{id:guid}/incident", ChangeIncidentStatusAsync);
        group.MapPatch("/{id:guid}/assignment", AssignAsync);
        group.MapPost("/{id:guid}/notes", AddNoteAsync);
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
            tenant => service.ListAsync(tenant, query, cancellationToken),
            cancellationToken);

    private static Task<IResult> GetAsync(
        Guid id,
        HttpContext httpContext,
        TenantContextResolver tenantResolver,
        AlertService service,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            httpContext,
            tenantResolver,
            tenant => service.GetAsync(tenant, id, cancellationToken),
            cancellationToken);

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
            tenant => service.AcknowledgeAsync(tenant, id, request!, cancellationToken),
            cancellationToken);

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
            tenant => service.ChangeStatusAsync(tenant, id, request!, cancellationToken),
            cancellationToken);

    private static Task<IResult> ChangeIncidentStatusAsync(
        Guid id,
        IncidentStatusChangeRequest? request,
        HttpContext httpContext,
        TenantContextResolver tenantResolver,
        AlertService service,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            httpContext,
            tenantResolver,
            tenant => service.ChangeIncidentStatusAsync(tenant, id, request!, cancellationToken),
            cancellationToken);

    private static Task<IResult> AssignAsync(
        Guid id,
        AlertAssignmentRequest? request,
        HttpContext httpContext,
        TenantContextResolver tenantResolver,
        AlertService service,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            httpContext,
            tenantResolver,
            tenant => service.AssignAsync(tenant, id, request!, cancellationToken),
            cancellationToken);

    private static Task<IResult> AddNoteAsync(
        Guid id,
        IncidentNoteRequest? request,
        HttpContext httpContext,
        TenantContextResolver tenantResolver,
        AlertService service,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            httpContext,
            tenantResolver,
            tenant => service.AddNoteAsync(tenant, id, request!, cancellationToken),
            cancellationToken);

    private static Task<IResult> TimelineAsync(
        Guid id,
        HttpContext httpContext,
        TenantContextResolver tenantResolver,
        AlertService service,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            httpContext,
            tenantResolver,
            tenant => service.GetTimelineAsync(tenant, id, cancellationToken),
            cancellationToken);

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
            tenant => service.ResolveAsync(tenant, id, request!, cancellationToken),
            cancellationToken);

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
            tenant => service.ReopenAsync(tenant, id, request!, cancellationToken),
            cancellationToken);

    private static async Task<IResult> ExecuteAsync<T>(
        HttpContext httpContext,
        TenantContextResolver tenantResolver,
        Func<TenantContext, Task<T>> operation,
        CancellationToken cancellationToken)
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
