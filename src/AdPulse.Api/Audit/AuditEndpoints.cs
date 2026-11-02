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
    public int Limit { get; init; } = PageQuery.DefaultLimit;
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
            PageSize = Limit,
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
}
