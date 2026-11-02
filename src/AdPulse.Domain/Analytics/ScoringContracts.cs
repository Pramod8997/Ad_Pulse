namespace AdPulse.Domain.Analytics;

public enum MetricDirection
{
    HigherIsBetter,
    LowerIsBetter
}

public enum ScoringSeverity
{
    None,
    Low,
    Medium,
    High,
    Critical
}

public sealed record ScoringRequest(
    string TenantId,
    string CampaignId,
    string MetricName,
    decimal Observed,
    decimal Baseline,
    int DataPoints,
    int MinimumDataPoints,
    MetricDirection Direction,
    DateOnly EvidenceWindowStart,
    DateOnly EvidenceWindowEnd,
    string ScoringVersion = "scoring-v1");

public sealed record ScoringResult(
    decimal Score,
    ScoringSeverity Severity,
    decimal Confidence,
    string DataSufficiency,
    IReadOnlyList<string> ReasonCodes,
    string Fingerprint);
