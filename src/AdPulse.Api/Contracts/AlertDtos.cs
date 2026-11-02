using System.Text.Json.Serialization;

namespace AdPulse.Api.Contracts;

public static class AlertStatuses
{
    public const string Open = "open";
    public const string Acknowledged = "acknowledged";
    public const string Resolved = "resolved";
}

public record AlertSummaryDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("campaignId")]
    public Guid? CampaignId { get; init; }

    [JsonPropertyName("campaignName")]
    public string? CampaignName { get; init; }

    [JsonPropertyName("metricName")]
    public string MetricName { get; init; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("detectedAt")]
    public DateTimeOffset DetectedAt { get; init; }
}

public sealed record AlertDto : AlertSummaryDto
{
    [JsonPropertyName("fingerprint")]
    public string Fingerprint { get; init; } = string.Empty;

    [JsonPropertyName("score")]
    public decimal Score { get; init; }

    [JsonPropertyName("observed")]
    public decimal Observed { get; init; }

    [JsonPropertyName("baseline")]
    public decimal Baseline { get; init; }

    [JsonPropertyName("confidence")]
    public decimal Confidence { get; init; }

    [JsonPropertyName("dataSufficiency")]
    public string DataSufficiency { get; init; } = string.Empty;

    [JsonPropertyName("reasonCodes")]
    public IReadOnlyList<string> ReasonCodes { get; init; } = Array.Empty<string>();

    [JsonPropertyName("scoringVersion")]
    public string ScoringVersion { get; init; } = string.Empty;

    [JsonPropertyName("evidenceWindowStart")]
    public DateOnly EvidenceWindowStart { get; init; }

    [JsonPropertyName("evidenceWindowEnd")]
    public DateOnly EvidenceWindowEnd { get; init; }
}

public sealed record UpdateAlertRequest
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}

public sealed record AlertListQuery : PageQuery
{
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("severity")]
    public string? Severity { get; init; }

    [JsonPropertyName("metric")]
    public string? Metric { get; init; }

    [JsonPropertyName("campaignId")]
    public Guid? CampaignId { get; init; }

    [JsonPropertyName("from")]
    public DateOnly? From { get; init; }

    [JsonPropertyName("to")]
    public DateOnly? To { get; init; }
}
