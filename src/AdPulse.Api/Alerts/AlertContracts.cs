using System.Text.Json;

namespace AdPulse.Api.Alerts;

public static class AlertStatuses
{
    public const string Open = "open";
    public const string Acknowledged = "acknowledged";
    public const string Resolved = "resolved";

    public static bool IsKnown(string value) => value is Open or Acknowledged or Resolved;
}

public static class AlertSeverities
{
    public static bool IsKnown(string value) => value is "none" or "low" or "medium" or "high" or "critical";
}

public sealed record AlertListQuery(
    string? Cursor = null,
    int Limit = 25,
    string? Status = null,
    string? Severity = null,
    string? Metric = null,
    Guid? CampaignId = null,
    DateOnly? From = null,
    DateOnly? To = null)
{
    public const int DefaultLimit = 25;
    public const int MaximumLimit = 100;

    public AlertListQuery Validate()
    {
        if (Limit is < 1 or > MaximumLimit)
        {
            throw new AlertValidationException($"Limit must be between 1 and {MaximumLimit}.");
        }

        var status = Normalize(Status);
        if (status is not null && !AlertStatuses.IsKnown(status))
        {
            throw new AlertValidationException("Status must be open, acknowledged or resolved.");
        }

        var severity = Normalize(Severity);
        if (severity is not null && !AlertSeverities.IsKnown(severity))
        {
            throw new AlertValidationException("Severity is invalid.");
        }

        var metricName = Normalize(Metric);
        if (metricName is not null && metricName.Length > 64)
        {
            throw new AlertValidationException("Metric name must be at most 64 characters.");
        }

        if (From is not null && To is not null && From > To)
        {
            throw new AlertValidationException("From must not be after To.");
        }

        if (Cursor is not null && Cursor.Length > 4096)
        {
            throw new AlertValidationException("Cursor is invalid.");
        }

        return this with { Status = status, Severity = severity, Metric = metricName };
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}

public sealed record AlertMutationRequest(bool Confirm, string? Reason)
{
    public string ValidateAndGetReason(bool required = false)
    {
        if (!Confirm)
        {
            throw new AlertValidationException("Confirmation is required for an alert status change.");
        }

        var reason = Reason?.Trim();
        if (required && string.IsNullOrWhiteSpace(reason))
        {
            throw new AlertValidationException("A reason is required for an alert status change.");
        }

        if (reason is not null && reason.Length > 500)
        {
            throw new AlertValidationException("Reason must be at most 500 characters.");
        }

        return reason ?? string.Empty;
    }
}

public sealed record AlertStatusChangeRequest(string Status, string? Reason = null, bool Confirm = false)
{
    public (string Status, string? Reason) Validate()
    {
        var status = Status?.Trim().ToLowerInvariant();
        if (status is not AlertStatuses.Open and not AlertStatuses.Acknowledged and not AlertStatuses.Resolved)
        {
            throw new AlertValidationException("Status must be open, acknowledged or resolved.");
        }

        var reason = Reason?.Trim();
        if (reason is not null && reason.Length > 500)
        {
            throw new AlertValidationException("Reason must be at most 500 characters.");
        }

        if (!Confirm)
        {
            throw new AlertValidationException("Confirmation is required for an alert status change.");
        }

        if (status == AlertStatuses.Resolved && string.IsNullOrWhiteSpace(reason))
        {
            throw new AlertValidationException("A reason is required when resolving an alert.");
        }

        return (status, reason);
    }
}

/// <summary>Scored values to persist. Tenant scope comes from <see cref="AdPulse.Domain.Tenancy.TenantContext"/>, never this input.</summary>
public sealed record ScoredAlert(
    Guid? CampaignId,
    string MetricName,
    decimal Score,
    string Severity,
    decimal Observed,
    decimal Baseline,
    decimal Confidence,
    string DataSufficiency,
    IReadOnlyList<string> ReasonCodes,
    string ScoringVersion,
    DateOnly EvidenceWindowStart,
    DateOnly EvidenceWindowEnd,
    string Fingerprint,
    DateTimeOffset? DetectedAt = null)
{
    public void Validate()
    {
        if (CampaignId is { } campaignId && campaignId == Guid.Empty)
        {
            throw new AlertValidationException("Campaign ID must be a non-empty identifier.");
        }

        if (string.IsNullOrWhiteSpace(MetricName) || MetricName.Trim().Length > 64)
        {
            throw new AlertValidationException("Metric name is required and must be at most 64 characters.");
        }

        if (Score is < 0m or > 100m || Observed < 0m || Baseline < 0m || Confidence is < 0m or > 1m)
        {
            throw new AlertValidationException("Scored alert values are outside their allowed ranges.");
        }

        if (!AlertSeverities.IsKnown(Severity))
        {
            throw new AlertValidationException("Severity is invalid.");
        }

        if (DataSufficiency is not ("insufficient" or "sufficient"))
        {
            throw new AlertValidationException("Data sufficiency is invalid.");
        }

        if (ReasonCodes is null || ReasonCodes.Count == 0 || ReasonCodes.Count > 32 || ReasonCodes.Any(string.IsNullOrWhiteSpace))
        {
            throw new AlertValidationException("At least one reason code is required.");
        }

        if (string.IsNullOrWhiteSpace(ScoringVersion) || ScoringVersion.Trim().Length > 32)
        {
            throw new AlertValidationException("Scoring version is required and must be at most 32 characters.");
        }

        if (EvidenceWindowEnd < EvidenceWindowStart)
        {
            throw new AlertValidationException("The evidence window end must not precede its start.");
        }

        if (string.IsNullOrWhiteSpace(Fingerprint)
            || Fingerprint.Length != 64
            || Fingerprint.Any(character => !Uri.IsHexDigit(character))
            || Fingerprint != Fingerprint.ToLowerInvariant())
        {
            throw new AlertValidationException("Fingerprint must be a lowercase 64-character SHA-256 value.");
        }
    }
}

public sealed record AlertSummary(
    Guid Id,
    Guid? CampaignId,
    string? CampaignName,
    string Fingerprint,
    string MetricName,
    decimal Score,
    string Severity,
    string Status,
    decimal Observed,
    decimal Baseline,
    decimal Confidence,
    string DataSufficiency,
    IReadOnlyList<string> ReasonCodes,
    string ScoringVersion,
    DateOnly EvidenceWindowStart,
    DateOnly EvidenceWindowEnd,
    DateTimeOffset DetectedAt);

public sealed record AlertDetails(
    Guid Id,
    Guid? CampaignId,
    string? CampaignName,
    string Fingerprint,
    string MetricName,
    decimal Score,
    string Severity,
    string Status,
    decimal Observed,
    decimal Baseline,
    decimal Confidence,
    string DataSufficiency,
    IReadOnlyList<string> ReasonCodes,
    string ScoringVersion,
    DateOnly EvidenceWindowStart,
    DateOnly EvidenceWindowEnd,
    DateTimeOffset DetectedAt);

public sealed record AlertPage(
    IReadOnlyList<AlertSummary> Items,
    string? NextCursor,
    bool HasMore,
    int Limit);

public sealed record AlertMutationOutcome(AlertDetails Alert, bool Changed, string Action)
{
    public Guid Id => Alert.Id;

    public string Status => Alert.Status;
}

public sealed class AlertValidationException(string message) : ArgumentException(message);

public sealed class AlertConflictException(string message) : InvalidOperationException(message);

internal static class AlertContractSerialization
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string SerializeReasonCodes(IReadOnlyList<string> reasonCodes) => JsonSerializer.Serialize(reasonCodes, Options);

    public static IReadOnlyList<string> DeserializeReasonCodes(string value)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(value, Options)
                ?.Where(code => !string.IsNullOrWhiteSpace(code))
                .ToArray()
                ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
