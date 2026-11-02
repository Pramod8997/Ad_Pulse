using System.Text.Json;
using AdPulse.Domain.Tenancy;
using AdPulse.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AdPulse.Api.Alerts;

/// <summary>Authorized, tenant-scoped alert reads and lifecycle writes.</summary>
public sealed class AlertService(AdPulseDbContext db)
{
    public async Task<AlertPage> ListAsync(
        TenantContext access,
        AlertListQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant(access);
        ArgumentNullException.ThrowIfNull(query);
        var validated = query.Validate();
        var alerts = Filter(db.Alerts.AsNoTracking(), validated);
        if (validated.Cursor is not null)
        {
            if (!AlertCursor.TryDecode(validated.Cursor, out var cursor)
                || cursor is null
                || !string.Equals(cursor.Filter, AlertCursor.FilterFor(validated), StringComparison.Ordinal))
            {
                throw new AlertValidationException("Cursor is invalid for the current alert filters.");
            }

            alerts = alerts.Where(alert => alert.DetectedAt < cursor.DetectedAt
                || (alert.DetectedAt == cursor.DetectedAt && alert.Id.CompareTo(cursor.Id) < 0));
        }

        var rows = await alerts
            .Include(alert => alert.Campaign)
            .OrderByDescending(alert => alert.DetectedAt)
            .ThenByDescending(alert => alert.Id)
            .Take(validated.Limit + 1)
            .ToListAsync(cancellationToken);
        var hasMore = rows.Count > validated.Limit;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return new AlertPage(
            rows.Select(ToSummary).ToArray(),
            hasMore && rows.Count > 0 ? AlertCursor.Encode(rows[^1], validated) : null,
            hasMore,
            validated.Limit);
    }

    public async Task<AlertDetails?> GetAsync(
        TenantContext access,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant(access);
        var alert = await db.Alerts
            .AsNoTracking()
            .Include(candidate => candidate.Campaign)
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return alert is null ? null : ToDetails(alert);
    }

    public Task<AlertMutationOutcome?> AcknowledgeAsync(
        TenantContext access,
        Guid id,
        AlertMutationRequest request,
        CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(access, id, AlertStatuses.Acknowledged, "acknowledge", request, cancellationToken);

    public Task<AlertMutationOutcome?> ResolveAsync(
        TenantContext access,
        Guid id,
        AlertMutationRequest request,
        CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(access, id, AlertStatuses.Resolved, "resolve", request, cancellationToken);

    public Task<AlertMutationOutcome?> ReopenAsync(
        TenantContext access,
        Guid id,
        AlertMutationRequest request,
        CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(access, id, AlertStatuses.Open, "reopen", request, cancellationToken);

    public async Task<AlertMutationOutcome?> ChangeStatusAsync(
        TenantContext access,
        Guid id,
        AlertStatusChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var (status, reason) = request?.Validate() ?? throw new AlertValidationException("A mutation body is required.");
        return await ChangeStatusAsync(
            access,
            id,
            status,
            status switch
            {
                AlertStatuses.Acknowledged => "acknowledge",
                AlertStatuses.Resolved => "resolve",
                AlertStatuses.Open => "reopen",
                _ => throw new AlertValidationException("Status is invalid.")
            },
            new AlertMutationRequest(true, reason),
            cancellationToken);
    }

    /// <summary>
    /// Persists a scored alert once per tenant/fingerprint. A repeated scoring delivery returns the existing row.
    /// </summary>
    public async Task<AlertDetails?> PersistScoredAsync(
        TenantContext access,
        ScoredAlert scored,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant(access);
        ArgumentNullException.ThrowIfNull(scored);
        scored.Validate();

        if (scored.Severity == "none")
        {
            return null;
        }

        var existing = await db.Alerts
            .Include(alert => alert.Campaign)
            .SingleOrDefaultAsync(alert => alert.Fingerprint == scored.Fingerprint, cancellationToken);
        if (existing is not null)
        {
            return ToDetails(existing);
        }

        if (scored.CampaignId is { } campaignId
            && !await db.Campaigns.AnyAsync(campaign => campaign.Id == campaignId, cancellationToken))
        {
            throw new AlertValidationException("Campaign was not found in the current tenant.");
        }

        var alert = new Alert
        {
            TenantId = access.TenantId,
            Id = Guid.NewGuid(),
            CampaignId = scored.CampaignId,
            Fingerprint = scored.Fingerprint,
            MetricName = scored.MetricName.Trim(),
            Score = scored.Score,
            Severity = scored.Severity,
            Status = AlertStatuses.Open,
            Observed = scored.Observed,
            Baseline = scored.Baseline,
            Confidence = scored.Confidence,
            DataSufficiency = scored.DataSufficiency,
            ReasonCodesJson = AlertContractSerialization.SerializeReasonCodes(scored.ReasonCodes),
            ScoringVersion = scored.ScoringVersion.Trim(),
            EvidenceWindowStart = scored.EvidenceWindowStart,
            EvidenceWindowEnd = scored.EvidenceWindowEnd,
            DetectedAt = scored.DetectedAt ?? DateTimeOffset.UtcNow
        };
        db.Alerts.Add(alert);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The unique tenant/fingerprint index is the concurrency boundary. Re-read the winner for retry idempotency.
            db.Entry(alert).State = EntityState.Detached;
            existing = await db.Alerts
                .Include(candidate => candidate.Campaign)
                .SingleOrDefaultAsync(candidate => candidate.Fingerprint == scored.Fingerprint, cancellationToken);
            if (existing is null)
            {
                throw;
            }

            return ToDetails(existing);
        }

        return ToDetails(alert);
    }

    private async Task<AlertMutationOutcome?> ChangeStatusAsync(
        TenantContext access,
        Guid id,
        string targetStatus,
        string action,
        AlertMutationRequest request,
        CancellationToken cancellationToken)
    {
        EnsureTenant(access);
        var reason = request?.ValidateAndGetReason(targetStatus == AlertStatuses.Resolved)
            ?? throw new AlertValidationException("A mutation body is required.");
        var alert = await db.Alerts
            .Include(candidate => candidate.Campaign)
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (alert is null)
        {
            return null;
        }

        var currentStatus = alert.Status.ToLowerInvariant();
        if (currentStatus == targetStatus)
        {
            return new AlertMutationOutcome(ToDetails(alert), false, action);
        }

        if (!CanTransition(currentStatus, targetStatus))
        {
            throw new AlertConflictException($"An alert in status '{alert.Status}' cannot be {Verb(action)}.");
        }

        alert.Status = targetStatus;
        db.AuditEvents.Add(new AuditEvent
        {
            TenantId = access.TenantId,
            Id = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            ActorSubjectId = access.SubjectId,
            Action = $"alert.{PastTense(action)}",
            EntityType = "alert",
            EntityId = alert.Id.ToString("D"),
            PayloadJson = JsonSerializer.Serialize(new
            {
                from_status = currentStatus,
                to_status = targetStatus,
                reason
            })
        });
        await db.SaveChangesAsync(cancellationToken);

        return new AlertMutationOutcome(ToDetails(alert), true, action);
    }

    private static bool CanTransition(string currentStatus, string targetStatus) =>
        (currentStatus, targetStatus) switch
        {
            (AlertStatuses.Open, AlertStatuses.Acknowledged) => true,
            (AlertStatuses.Open, AlertStatuses.Resolved) => true,
            (AlertStatuses.Acknowledged, AlertStatuses.Resolved) => true,
            (AlertStatuses.Resolved, AlertStatuses.Open) => true,
            _ => false
        };

    private static string PastTense(string action) => action switch
    {
        "acknowledge" => "acknowledged",
        "resolve" => "resolved",
        "reopen" => "reopened",
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown alert action.")
    };

    private static string Verb(string action) => action switch
    {
        "acknowledge" => "acknowledged",
        "resolve" => "resolved",
        "reopen" => "reopened",
        _ => action
    };

    private static IQueryable<Alert> Filter(IQueryable<Alert> alerts, AlertListQuery query)
    {
        if (query.Status is not null)
        {
            alerts = alerts.Where(alert => alert.Status.ToLower() == query.Status);
        }

        if (query.Severity is not null)
        {
            alerts = alerts.Where(alert => alert.Severity.ToLower() == query.Severity);
        }

        if (query.Metric is not null)
        {
            alerts = alerts.Where(alert => alert.MetricName.ToLower() == query.Metric);
        }

        if (query.CampaignId is not null)
        {
            alerts = alerts.Where(alert => alert.CampaignId == query.CampaignId);
        }

        if (query.From is not null)
        {
            alerts = alerts.Where(alert => alert.DetectedAt >= new DateTimeOffset(query.From.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)));
        }

        if (query.To is not null)
        {
            alerts = alerts.Where(alert => alert.DetectedAt < new DateTimeOffset(query.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)));
        }

        return alerts;
    }

    private void EnsureTenant(TenantContext access)
    {
        if (access is null
            || string.IsNullOrWhiteSpace(access.TenantId)
            || string.IsNullOrWhiteSpace(access.SubjectId)
            || db.Scope.IsSystemScope
            || !string.Equals(db.Scope.TenantId, access.TenantId, StringComparison.Ordinal))
        {
            throw new TenantAccessDeniedException("A validated tenant-scoped identity is required.");
        }
    }

    private static AlertSummary ToSummary(Alert alert) => new(
        alert.Id,
        alert.CampaignId,
        alert.Campaign?.Name,
        alert.Fingerprint,
        alert.MetricName,
        alert.Score,
        alert.Severity,
        alert.Status,
        alert.Observed,
        alert.Baseline,
        alert.Confidence,
        alert.DataSufficiency,
        AlertContractSerialization.DeserializeReasonCodes(alert.ReasonCodesJson),
        alert.ScoringVersion,
        alert.EvidenceWindowStart,
        alert.EvidenceWindowEnd,
        alert.DetectedAt);

    private static AlertDetails ToDetails(Alert alert) => new(
        alert.Id,
        alert.CampaignId,
        alert.Campaign?.Name,
        alert.Fingerprint,
        alert.MetricName,
        alert.Score,
        alert.Severity,
        alert.Status,
        alert.Observed,
        alert.Baseline,
        alert.Confidence,
        alert.DataSufficiency,
        AlertContractSerialization.DeserializeReasonCodes(alert.ReasonCodesJson),
        alert.ScoringVersion,
        alert.EvidenceWindowStart,
        alert.EvidenceWindowEnd,
        alert.DetectedAt);
}

internal sealed record AlertCursor(DateTimeOffset DetectedAt, Guid Id, string Filter)
{
    public static string Encode(Alert alert, AlertListQuery query)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new AlertCursor(
            alert.DetectedAt.ToUniversalTime(),
            alert.Id,
            FilterFor(query)));
        return Convert.ToBase64String(payload)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static bool TryDecode(string encoded, out AlertCursor? cursor)
    {
        cursor = null;
        try
        {
            var padded = encoded.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
            cursor = JsonSerializer.Deserialize<AlertCursor>(Convert.FromBase64String(padded));
            return cursor is not null && cursor.Id != Guid.Empty && cursor.DetectedAt.Offset == TimeSpan.Zero;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string FilterFor(AlertListQuery query) => string.Join(
        '|',
        query.Status ?? string.Empty,
        query.Severity ?? string.Empty,
        query.Metric ?? string.Empty,
        query.CampaignId?.ToString("D") ?? string.Empty,
        query.From?.ToString("yyyy-MM-dd") ?? string.Empty,
        query.To?.ToString("yyyy-MM-dd") ?? string.Empty);
}
