namespace AdPulse.Api.Tests.ReleaseGates;

public sealed class IdempotencyReleaseGateTests
{
    [Fact]
    public void Daily_metric_idempotency_boundary_is_tenant_campaign_and_date()
    {
        var model = ReleaseGateRepository.ReadText("src", "AdPulse.Infrastructure", "Data", "AdPulseDbContext.cs");
        var migration = ReleaseGateRepository.ReadText("db", "migrations", "001_initial.sql");

        Assert.Contains("HasKey(x => new { x.TenantId, x.CampaignId, x.MetricDate })", model, StringComparison.Ordinal);
        Assert.Contains("PK_DailyMetrics PRIMARY KEY (TenantId, CampaignId, MetricDate)", migration, StringComparison.Ordinal);
    }

    [Fact]
    public void Alert_idempotency_boundary_is_tenant_and_fingerprint()
    {
        var model = ReleaseGateRepository.ReadText("src", "AdPulse.Infrastructure", "Data", "AdPulseDbContext.cs");
        var migration = ReleaseGateRepository.ReadText("db", "migrations", "001_initial.sql");

        Assert.Contains(
            "HasIndex(x => new { x.TenantId, x.Fingerprint })",
            model,
            StringComparison.Ordinal);
        Assert.Contains(".IsUnique()", model, StringComparison.Ordinal);
        Assert.Contains(
            "UQ_Alerts_TenantFingerprint UNIQUE (TenantId, Fingerprint)",
            migration,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Idempotency_keys_are_not_global_across_tenants()
    {
        var migration = ReleaseGateRepository.ReadText("db", "migrations", "001_initial.sql");

        Assert.Contains("PRIMARY KEY (TenantId, CampaignId, MetricDate)", migration, StringComparison.Ordinal);
        Assert.Contains("UNIQUE (TenantId, Fingerprint)", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIMARY KEY (CampaignId, MetricDate)", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("UNIQUE (Fingerprint)", migration, StringComparison.Ordinal);
    }
}
