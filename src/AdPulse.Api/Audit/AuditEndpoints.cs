using AdPulse.Api.Contracts;
using AdPulse.Domain.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AdPulse.Api.Audit;

public sealed record AuditEndpointQuery
{
    public string? Cursor { get; init; }
    public int? Limit { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public string? ActorSubjectId { get; init; }
    public string? Action { get; init; }
    public string? EntityType { get; init; }
    public string? EntityId { get; init; }

    public AuditLogQuery ToServiceQuery()
    {
        DateTimeOffset? from = From?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        DateTimeOffset? to = null;
        if (To is { } end)
        {
            if (end == DateOnly.MaxValue)
            {
                throw new ArgumentException("The audit end date is outside the supported range.", nameof(To));
            }

            to = end.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        }

        return new AuditLogQuery
        {
            PageSize = Limit ?? PageQuery.DefaultLimit,
            Cursor = Cursor,
            FromUtcInclusive = from,
            ToUtcExclusive = to,
            ActorSubjectId = ActorSubjectId,
            Action = Action,
            EntityType = EntityType,
            EntityId = EntityId
        };
    }
}

public static class AuditEndpoints
{
    public static IServiceCollection AddAuditServices(this IServiceCollection services) =>
        services.AddScoped<AuditLogReader>();

    public static RouteGroupBuilder MapAuditEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/app/audit").RequireAuthorization();
        group.MapGet(string.Empty, ReadAsync);
        return group;
    }

    private static async Task<IResult> ReadAsync(
        [AsParameters] AuditEndpointQuery query,
        TenantContext tenant,
        AuditLogReader reader,
        CancellationToken cancellationToken)
    {
        if (!HasAuditPermission(tenant))
        {
            return Results.Forbid();
        }

        try
        {
            var page = await reader.ReadAsync(tenant, query.ToServiceQuery(), cancellationToken);
            return Results.Ok(new CursorPage<AuditEventDto>
            {
                Items = page.Items.Select(item => new AuditEventDto
                {
                    Id = item.Id,
                    OccurredAt = item.OccurredAtUtc,
                    ActorSubjectId = item.ActorSubjectId,
                    Action = item.Action,
                    EntityType = item.EntityType,
                    EntityId = item.EntityId
                }).ToArray(),
                NextCursor = page.NextCursor,
                HasMore = page.HasMore,
                Limit = page.Limit
            });
        }
        catch (TenantAccessDeniedException)
        {
            return Results.Forbid();
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [string.IsNullOrWhiteSpace(exception.ParamName) ? "query" : exception.ParamName] = [exception.Message]
            });
        }
    }

    private static bool HasAuditPermission(TenantContext tenant) =>
        tenant.Roles.Any(role =>
            string.Equals(role, "audit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "auditor", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "tenant-admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "tenant_admin", StringComparison.OrdinalIgnoreCase));
}
