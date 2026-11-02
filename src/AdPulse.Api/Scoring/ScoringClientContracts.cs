using System.Text.Json.Serialization;

namespace AdPulse.Api.Scoring;

internal sealed record ScoringHttpRequest(
    [property: JsonPropertyName("tenant_id")] string TenantId,
    [property: JsonPropertyName("campaign_id")] string CampaignId,
    [property: JsonPropertyName("metric_name")] string MetricName,
    [property: JsonPropertyName("observed")] string Observed,
    [property: JsonPropertyName("baseline")] string Baseline,
    [property: JsonPropertyName("data_points")] int DataPoints,
    [property: JsonPropertyName("minimum_data_points")] int MinimumDataPoints,
    [property: JsonPropertyName("direction")] string Direction,
    [property: JsonPropertyName("evidence_window_start")] string EvidenceWindowStart,
    [property: JsonPropertyName("evidence_window_end")] string EvidenceWindowEnd,
    [property: JsonPropertyName("scoring_version")] string ScoringVersion);

internal sealed record ScoringHttpResponse(
    [property: JsonPropertyName("score")] string? Score,
    [property: JsonPropertyName("severity")] string? Severity,
    [property: JsonPropertyName("confidence")] string? Confidence,
    [property: JsonPropertyName("data_sufficiency")] string? DataSufficiency,
    [property: JsonPropertyName("reason_codes")] string[]? ReasonCodes,
    [property: JsonPropertyName("fingerprint")] string? Fingerprint);
