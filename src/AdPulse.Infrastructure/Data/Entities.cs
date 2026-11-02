namespace AdPulse.Infrastructure.Data;

public interface ITenantOwned
{
    string TenantId { get; }
}

public sealed class Tenant : ITenantOwned
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public string TenantId => Id;
}

public sealed class Membership : ITenantOwned
{
    public string TenantId { get; set; } = string.Empty;
    public string SubjectId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public Tenant? Tenant { get; set; }
}

public sealed class TenantSetting : ITenantOwned
{
    public string TenantId { get; set; } = string.Empty;
    public string ReportingTimeZone { get; set; } = string.Empty;
    public string? DefaultCurrency { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public Tenant? Tenant { get; set; }
}

public sealed class Campaign : ITenantOwned
{
    public string TenantId { get; set; } = string.Empty;
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal Budget { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public ICollection<DailyMetric> DailyMetrics { get; } = new List<DailyMetric>();
    public ICollection<Alert> Alerts { get; } = new List<Alert>();
}

public sealed class DailyMetric : ITenantOwned
{
    public string TenantId { get; set; } = string.Empty;
    public Guid CampaignId { get; set; }
    public DateOnly MetricDate { get; set; }
    public decimal Spend { get; set; }
    public decimal Revenue { get; set; }
    public long Impressions { get; set; }
    public long Clicks { get; set; }
    public long Conversions { get; set; }
    public Campaign? Campaign { get; set; }
}

public sealed class ImportBatch : ITenantOwned
{
    public string TenantId { get; set; } = string.Empty;
    public Guid Id { get; set; }
    public string UploadedBySubjectId { get; set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int AcceptedRows { get; set; }
    public int RejectedRows { get; set; }
    public string Status { get; set; } = string.Empty;
    public ICollection<ImportRowError> RowErrors { get; } = new List<ImportRowError>();
}

public sealed class ImportRowError : ITenantOwned
{
    public string TenantId { get; set; } = string.Empty;
    public Guid ImportBatchId { get; set; }
    public int RowNumber { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public ImportBatch? ImportBatch { get; set; }
}

public sealed class Alert : ITenantOwned
{
    public string TenantId { get; set; } = string.Empty;
    public Guid Id { get; set; }
    public Guid? CampaignId { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Observed { get; set; }
    public decimal Baseline { get; set; }
    public decimal Confidence { get; set; }
    public string DataSufficiency { get; set; } = string.Empty;
    public string ReasonCodesJson { get; set; } = "[]";
    public string ScoringVersion { get; set; } = string.Empty;
    public DateOnly EvidenceWindowStart { get; set; }
    public DateOnly EvidenceWindowEnd { get; set; }
    public DateTimeOffset DetectedAt { get; set; }
    public Campaign? Campaign { get; set; }
}

public sealed class AuditEvent : ITenantOwned
{
    public string TenantId { get; set; } = string.Empty;
    public Guid Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string ActorSubjectId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
}
