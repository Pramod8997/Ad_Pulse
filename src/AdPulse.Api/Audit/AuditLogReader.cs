using System.Text.Json;
using System.Security.Cryptography;
using AdPulse.Domain.Tenancy;
using AdPulse.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AdPulse.Api.Audit;

public sealed record AuditLogQuery
{
    public int PageSize { get; init; } = 25;
    public string? Cursor { get; init; }
    public DateTimeOffset? FromUtcInclusive { get; init; }
    public DateTimeOffset? ToUtcExclusive { get; init; }
    public string? ActorSubjectId { get; init; }
    public string? Action { get; init; }
    public string? EntityType { get; init; }
    public string? EntityId { get; init; }

    internal NormalizedAuditLogQuery Normalize()
    {
        if (PageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(PageSize), "Page size must be between 1 and 100.");
        }

        var from = FromUtcInclusive?.ToUniversalTime();
        var to = ToUtcExclusive?.ToUniversalTime();
        if (from.HasValue && to.HasValue && from >= to)
        {
            throw new ArgumentException("The audit time range must have an exclusive end after its inclusive start.");
        }

        return new NormalizedAuditLogQuery(
            PageSize,
            Cursor,
            from,
            to,
            Filter(ActorSubjectId, 128, nameof(ActorSubjectId)),
            Filter(Action, 64, nameof(Action)),
            Filter(EntityType, 64, nameof(EntityType)),
            Filter(EntityId, 128, nameof(EntityId)));
    }

    private static string? Filter(string? value, int maxLength, string name)
    {
        if (value is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength || value != value.Trim())
        {
            throw new ArgumentException($"{name} must be non-blank, trimmed and at most {maxLength} characters.", name);
        }

        return value;
    }
}

internal sealed record NormalizedAuditLogQuery(
    int PageSize,
    string? Cursor,
    DateTimeOffset? FromUtcInclusive,
    DateTimeOffset? ToUtcExclusive,
    string? ActorSubjectId,
    string? Action,
    string? EntityType,
    string? EntityId);

public sealed record AuditEventResponse(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    string ActorSubjectId,
    string Action,
    string EntityType,
    string EntityId);

public sealed record AuditLogPage(
    IReadOnlyList<AuditEventResponse> Items,
    string? NextCursor,
    bool HasMore,
    int Limit);

public sealed class AuditLogReader
{
    private readonly AdPulseDbContext _db;

    public AuditLogReader(AdPulseDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<AuditLogPage> ReadAsync(
        TenantContext tenant,
        AuditLogQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        EnsureTenantScope(tenant);

        var request = (query ?? new AuditLogQuery()).Normalize();
        var cursor = DecodeCursor(request.Cursor);
        var filterKey = CreateFilterKey(tenant.TenantId, request);
        if (cursor is not null
            && (!string.Equals(cursor.TenantId, tenant.TenantId, StringComparison.Ordinal)
                || !string.Equals(cursor.FilterKey, filterKey, StringComparison.Ordinal)))
        {
            throw new ArgumentException("The audit cursor is incompatible with this tenant or filter.", nameof(query));
        }

        var events = _db.AuditEvents
            .AsNoTracking()
            .Where(x => x.TenantId == tenant.TenantId);

        if (request.ActorSubjectId is { } actor)
        {
            events = events.Where(x => x.ActorSubjectId == actor);
        }

        if (request.Action is { } action)
        {
            events = events.Where(x => x.Action == action);
        }

        if (request.EntityType is { } entityType)
        {
            events = events.Where(x => x.EntityType == entityType);
        }

        if (request.EntityId is { } entityId)
        {
            events = events.Where(x => x.EntityId == entityId);
        }

        List<AuditEvent> rows;
        if (_db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            // SQLite cannot translate DateTimeOffset ordering/comparisons; production SQL Server keeps this work server-side.
            var candidates = await events.ToListAsync(cancellationToken);
            rows = candidates
                .Where(x => IsAfterStart(x, request.FromUtcInclusive))
                .Where(x => IsBeforeEnd(x, request.ToUtcExclusive))
                .Where(x => IsAfterCursor(x, cursor))
                .OrderByDescending(x => x.OccurredAt)
                .ThenByDescending(x => x.Id)
                .Take(request.PageSize + 1)
                .ToList();
        }
        else
        {
            if (request.FromUtcInclusive is { } from)
            {
                events = events.Where(x => x.OccurredAt >= from);
            }

            if (request.ToUtcExclusive is { } to)
            {
                events = events.Where(x => x.OccurredAt < to);
            }

            if (cursor is not null)
            {
                events = events.Where(x =>
                    x.OccurredAt < cursor.OccurredAtUtc
                    || (x.OccurredAt == cursor.OccurredAtUtc && x.Id.CompareTo(cursor.Id) < 0));
            }

            rows = await events
                .OrderByDescending(x => x.OccurredAt)
                .ThenByDescending(x => x.Id)
                .Take(request.PageSize + 1)
                .ToListAsync(cancellationToken);
        }

        var hasMore = rows.Count > request.PageSize;
        var pageRows = hasMore ? rows.Take(request.PageSize).ToArray() : rows.ToArray();
        var items = pageRows
            .Select(x => new AuditEventResponse(
                x.Id,
                x.OccurredAt.ToUniversalTime(),
                x.ActorSubjectId,
                x.Action,
                x.EntityType,
                x.EntityId))
            .ToArray();

        return new AuditLogPage(
            items,
            hasMore ? EncodeCursor(pageRows[^1], tenant.TenantId, filterKey) : null,
            hasMore,
            request.PageSize);
    }

    private void EnsureTenantScope(TenantContext tenant)
    {
        if (_db.Scope.IsSystemScope
            || !string.Equals(_db.Scope.TenantId, tenant.TenantId, StringComparison.Ordinal))
        {
            throw new TenantAccessDeniedException("The database scope does not match the authenticated tenant.");
        }
    }

    private static string EncodeCursor(AuditEvent auditEvent, string tenantId, string filterKey)
    {
        var cursor = new AuditCursor
        {
            OccurredAtUtc = auditEvent.OccurredAt.ToUniversalTime(),
            Id = auditEvent.Id,
            TenantId = tenantId,
            FilterKey = filterKey
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(cursor);
        return Convert.ToBase64String(json)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static bool IsAfterStart(AuditEvent auditEvent, DateTimeOffset? from) =>
        !from.HasValue || auditEvent.OccurredAt.ToUniversalTime() >= from.Value;

    private static bool IsBeforeEnd(AuditEvent auditEvent, DateTimeOffset? to) =>
        !to.HasValue || auditEvent.OccurredAt.ToUniversalTime() < to.Value;

    private static bool IsAfterCursor(AuditEvent auditEvent, AuditCursor? cursor) =>
        cursor is null
        || auditEvent.OccurredAt.ToUniversalTime() < cursor.OccurredAtUtc
        || (auditEvent.OccurredAt.ToUniversalTime() == cursor.OccurredAtUtc
            && auditEvent.Id.CompareTo(cursor.Id) < 0);

    private static string CreateFilterKey(string tenantId, NormalizedAuditLogQuery query)
    {
        var value = JsonSerializer.SerializeToUtf8Bytes(new
        {
            tenantId,
            query.FromUtcInclusive,
            query.ToUtcExclusive,
            query.ActorSubjectId,
            query.Action,
            query.EntityType,
            query.EntityId
        });
        return Convert.ToHexString(SHA256.HashData(value));
    }

    private static AuditCursor? DecodeCursor(string? encoded)
    {
        if (encoded is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(encoded) || encoded.Length > 512)
        {
            throw new ArgumentException("The audit cursor is invalid.", nameof(encoded));
        }

        try
        {
            var base64 = encoded.Replace('-', '+').Replace('_', '/');
            base64 += (base64.Length % 4) switch
            {
                0 => string.Empty,
                2 => "==",
                3 => "=",
                _ => throw new FormatException()
            };

            var cursor = JsonSerializer.Deserialize<AuditCursor>(Convert.FromBase64String(base64));
            if (cursor is null
                || cursor.Id == Guid.Empty
                || string.IsNullOrWhiteSpace(cursor.TenantId)
                || string.IsNullOrWhiteSpace(cursor.FilterKey)
                || cursor.OccurredAtUtc == default)
            {
                throw new FormatException();
            }

            cursor.OccurredAtUtc = cursor.OccurredAtUtc.ToUniversalTime();
            return cursor;
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("The audit cursor is invalid.", nameof(encoded), exception);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("The audit cursor is invalid.", nameof(encoded), exception);
        }
    }

    private sealed class AuditCursor
    {
        public DateTimeOffset OccurredAtUtc { get; set; }
        public Guid Id { get; set; }
        public string TenantId { get; set; } = string.Empty;
        public string FilterKey { get; set; } = string.Empty;
    }
}
