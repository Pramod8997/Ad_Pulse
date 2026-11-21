using System.Globalization;
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

        return alert is null ? null : await ToDetailsAsync(alert, cancellationToken);
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

    public async Task<AlertMutationOutcome?> ChangeIncidentStatusAsync(
        TenantContext access,
        Guid id,
        IncidentStatusChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant(access);
        var (targetStatus, summary) = request?.Validate() ?? throw new AlertValidationException("A mutation body is required.");
        var alert = await FindAsync(id, cancellationToken);
        if (alert is null)
        {
            return null;
        }

        var currentStatus = IncidentStatusFor(alert);
        if (currentStatus == targetStatus)
        {
            return new AlertMutationOutcome(await ToDetailsAsync(alert, cancellationToken), false, $"incident.{targetStatus}");
        }

        if (!CanTransitionIncident(currentStatus, targetStatus))
        {
            throw new AlertConflictException($"An incident in status '{currentStatus}' cannot transition to '{targetStatus}'.");
        }

        ApplyIncidentState(alert, targetStatus, summary, access.SubjectId);
        AddAudit(access, alert.Id, $"incident.{targetStatus}", new
        {
            from_incident_status = currentStatus,
            to_incident_status = targetStatus,
            summary
        });
        await db.SaveChangesAsync(cancellationToken);

        return new AlertMutationOutcome(await ToDetailsAsync(alert, cancellationToken), true, $"incident.{targetStatus}");
    }

    public async Task<AlertMutationOutcome?> AssignAsync(
        TenantContext access,
        Guid id,
        AlertAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant(access);
        var ownerSubjectId = request?.Validate() ?? throw new AlertValidationException("A mutation body is required.");
        if (ownerSubjectId is not null
            && !await db.Memberships.AnyAsync(member => member.SubjectId == ownerSubjectId, cancellationToken))
        {
            throw new AlertValidationException("Owner must be a member of the current tenant.");
        }

        var alert = await FindAsync(id, cancellationToken);
        if (alert is null)
        {
            return null;
        }

        if (string.Equals(alert.OwnerSubjectId, ownerSubjectId, StringComparison.Ordinal))
        {
            return new AlertMutationOutcome(await ToDetailsAsync(alert, cancellationToken), false, ownerSubjectId is null ? "incident.unassigned" : "incident.assigned");
        }

        alert.OwnerSubjectId = ownerSubjectId;
        alert.AssignedAt = ownerSubjectId is null ? null : DateTimeOffset.UtcNow;
        AddAudit(access, alert.Id, ownerSubjectId is null ? "incident.unassigned" : "incident.assigned", new
        {
            owner_subject_id = ownerSubjectId
        });
        await db.SaveChangesAsync(cancellationToken);

        return new AlertMutationOutcome(await ToDetailsAsync(alert, cancellationToken), true, ownerSubjectId is null ? "incident.unassigned" : "incident.assigned");
    }

    public async Task<AlertDetails?> AddNoteAsync(
        TenantContext access,
        Guid id,
        IncidentNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant(access);
        var note = request?.Validate() ?? throw new AlertValidationException("A note body is required.");
        var alert = await FindAsync(id, cancellationToken);
        if (alert is null)
        {
            return null;
        }

        AddAudit(access, alert.Id, "incident.note_added", new { note });
        await db.SaveChangesAsync(cancellationToken);
        return await ToDetailsAsync(alert, cancellationToken);
    }

    public async Task<IReadOnlyList<AlertTimelineEntry>?> GetTimelineAsync(
        TenantContext access,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant(access);
        var exists = await db.Alerts.AnyAsync(alert => alert.Id == id, cancellationToken);
        return exists ? await ReadTimelineAsync(id, cancellationToken) : null;
    }

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
            return await ToDetailsAsync(existing, cancellationToken);
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
            IncidentStatus = IncidentStatuses.New,
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
        AddAudit(access, alert.Id, "incident.created", new
        {
            incident_status = IncidentStatuses.New,
            severity = alert.Severity,
            metric = alert.MetricName
        });

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

            return await ToDetailsAsync(existing, cancellationToken);
        }

        return await ToDetailsAsync(alert, cancellationToken);
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
            return new AlertMutationOutcome(await ToDetailsAsync(alert, cancellationToken), false, action);
        }

        if (!CanTransition(currentStatus, targetStatus))
        {
            throw new AlertConflictException($"An alert in status '{alert.Status}' cannot be {Verb(action)}.");
        }

        var currentIncidentStatus = IncidentStatusFor(alert);
        alert.Status = targetStatus;
        alert.IncidentStatus = targetStatus switch
        {
            AlertStatuses.Open => IncidentStatuses.New,
            AlertStatuses.Acknowledged => IncidentStatuses.Acknowledged,
            AlertStatuses.Resolved => IncidentStatuses.Resolved,
            _ => alert.IncidentStatus
        };
        if (targetStatus == AlertStatuses.Resolved)
        {
            alert.ResolutionSummary = reason;
            alert.ResolvedAt = DateTimeOffset.UtcNow;
            alert.ResolvedBySubjectId = access.SubjectId;
        }
        else if (targetStatus == AlertStatuses.Open)
        {
            alert.ResolutionSummary = null;
            alert.FalsePositiveReason = null;
            alert.ResolvedAt = null;
            alert.ResolvedBySubjectId = null;
        }

        AddAudit(access, alert.Id, $"alert.{PastTense(action)}", new
        {
            from_status = currentStatus,
            to_status = targetStatus,
            from_incident_status = currentIncidentStatus,
            to_incident_status = alert.IncidentStatus,
            reason
        });
        await db.SaveChangesAsync(cancellationToken);

        return new AlertMutationOutcome(await ToDetailsAsync(alert, cancellationToken), true, action);
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

    private static bool CanTransitionIncident(string currentStatus, string targetStatus) =>
        (currentStatus, targetStatus) switch
        {
            (IncidentStatuses.New, IncidentStatuses.Acknowledged or IncidentStatuses.Investigating or IncidentStatuses.Resolved or IncidentStatuses.FalsePositive) => true,
            (IncidentStatuses.Acknowledged, IncidentStatuses.Investigating or IncidentStatuses.Mitigated or IncidentStatuses.Resolved or IncidentStatuses.FalsePositive) => true,
            (IncidentStatuses.Investigating, IncidentStatuses.Acknowledged or IncidentStatuses.Mitigated or IncidentStatuses.Resolved or IncidentStatuses.FalsePositive) => true,
            (IncidentStatuses.Mitigated, IncidentStatuses.Investigating or IncidentStatuses.Resolved) => true,
            (IncidentStatuses.Resolved or IncidentStatuses.FalsePositive, IncidentStatuses.New) => true,
            _ => false
        };

    private static void ApplyIncidentState(Alert alert, string targetStatus, string? summary, string actorSubjectId)
    {
        alert.IncidentStatus = targetStatus;
        alert.Status = targetStatus switch
        {
            IncidentStatuses.New => AlertStatuses.Open,
            IncidentStatuses.Resolved or IncidentStatuses.FalsePositive => AlertStatuses.Resolved,
            _ => AlertStatuses.Acknowledged
        };

        if (targetStatus == IncidentStatuses.Resolved)
        {
            alert.ResolutionSummary = summary;
            alert.FalsePositiveReason = null;
            alert.ResolvedAt = DateTimeOffset.UtcNow;
            alert.ResolvedBySubjectId = actorSubjectId;
        }
        else if (targetStatus == IncidentStatuses.FalsePositive)
        {
            alert.FalsePositiveReason = summary;
            alert.ResolutionSummary = null;
            alert.ResolvedAt = DateTimeOffset.UtcNow;
            alert.ResolvedBySubjectId = actorSubjectId;
        }
        else if (targetStatus == IncidentStatuses.New)
        {
            alert.ResolutionSummary = null;
            alert.FalsePositiveReason = null;
            alert.ResolvedAt = null;
            alert.ResolvedBySubjectId = null;
        }
    }

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
            alerts = alerts.Where(alert => alert.Status == query.Status);
        }

        if (query.IncidentStatus is not null)
        {
            alerts = alerts.Where(alert => alert.IncidentStatus == query.IncidentStatus
                || (alert.IncidentStatus == null && query.IncidentStatus == LegacyIncidentStatus(alert.Status)));
        }

        if (query.OwnerSubjectId is not null)
        {
            alerts = alerts.Where(alert => alert.OwnerSubjectId == query.OwnerSubjectId);
        }

        if (query.Severity is not null)
        {
            alerts = alerts.Where(alert => alert.Severity == query.Severity);
        }

        if (query.Metric is not null)
        {
            alerts = alerts.Where(alert => alert.MetricName == query.Metric);
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

    private async Task<Alert?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Alerts
            .Include(candidate => candidate.Campaign)
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

    private async Task<AlertDetails> ToDetailsAsync(Alert alert, CancellationToken cancellationToken) => new(
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
        alert.DetectedAt,
        IncidentStatusFor(alert),
        alert.OwnerSubjectId,
        alert.AssignedAt,
        alert.ResolutionSummary,
        alert.FalsePositiveReason,
        alert.ResolvedAt,
        alert.ResolvedBySubjectId,
        await ReadTimelineAsync(alert.Id, cancellationToken));

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
        alert.DetectedAt,
        IncidentStatusFor(alert),
        alert.OwnerSubjectId,
        alert.AssignedAt);

    private async Task<IReadOnlyList<AlertTimelineEntry>> ReadTimelineAsync(Guid alertId, CancellationToken cancellationToken)
    {
        var query = db.AuditEvents
            .AsNoTracking()
            .Where(audit => audit.EntityType == "alert" && audit.EntityId == alertId.ToString("D"));
        List<AuditEvent> events;
        if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            events = (await query.ToListAsync(cancellationToken))
                .OrderByDescending(audit => audit.OccurredAt)
                .ThenByDescending(audit => audit.Id)
                .Take(100)
                .ToList();
        }
        else
        {
            events = await query
                .OrderByDescending(audit => audit.OccurredAt)
                .ThenByDescending(audit => audit.Id)
                .Take(100)
                .ToListAsync(cancellationToken);
        }

        return events.Select(ToTimelineEntry).ToArray();
    }

    private static AlertTimelineEntry ToTimelineEntry(AuditEvent audit)
    {
        string? note = null;
        string? fromStatus = null;
        string? toStatus = null;
        string? ownerSubjectId = null;
        try
        {
            using var document = JsonDocument.Parse(audit.PayloadJson);
            var payload = document.RootElement;
            note = ReadString(payload, "note") ?? ReadString(payload, "reason") ?? ReadString(payload, "summary");
            fromStatus = ReadString(payload, "from_incident_status");
            toStatus = ReadString(payload, "to_incident_status");
            ownerSubjectId = ReadString(payload, "owner_subject_id");
        }
        catch (JsonException)
        {
            // The database validates JSON in production; malformed test data remains a safe, payload-free timeline entry.
        }

        return new AlertTimelineEntry(audit.Id, audit.OccurredAt.ToUniversalTime(), audit.ActorSubjectId, audit.Action, note, fromStatus, toStatus, ownerSubjectId);
    }

    private static string? ReadString(JsonElement payload, string propertyName) =>
        payload.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string IncidentStatusFor(Alert alert) => alert.IncidentStatus ?? LegacyIncidentStatus(alert.Status);

    private static string LegacyIncidentStatus(string status) => status.ToLowerInvariant() switch
    {
        AlertStatuses.Acknowledged => IncidentStatuses.Acknowledged,
        AlertStatuses.Resolved => IncidentStatuses.Resolved,
        _ => IncidentStatuses.New
    };

    private void AddAudit(TenantContext access, Guid alertId, string action, object payload) =>
        db.AuditEvents.Add(new AuditEvent
        {
            TenantId = access.TenantId,
            Id = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            ActorSubjectId = access.SubjectId,
            Action = action,
            EntityType = "alert",
            EntityId = alertId.ToString("D"),
            PayloadJson = JsonSerializer.Serialize(payload)
        });
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
        query.From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
        query.To?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
        query.IncidentStatus ?? string.Empty,
        query.OwnerSubjectId ?? string.Empty);
}
