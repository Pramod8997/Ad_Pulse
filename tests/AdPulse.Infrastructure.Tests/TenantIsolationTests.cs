using System.Security;
using AdPulse.Domain.Tenancy;
using AdPulse.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AdPulse.Infrastructure.Tests;

public sealed class TenantIsolationTests
{
    [Fact]
    public async Task Tenant_scope_filters_campaign_metric_import_alert_and_audit_reads()
    {
        await using var database = await TestDatabase.CreateAsync();
        var campaignA = Guid.NewGuid();
        var campaignB = Guid.NewGuid();
        var importA = Guid.NewGuid();
        var importB = Guid.NewGuid();

        await using (var system = database.Context(TenantScope.System))
        {
            system.Campaigns.AddRange(
                Campaign("tenant-a", campaignA),
                Campaign("tenant-b", campaignB));
            system.DailyMetrics.AddRange(
                Metric("tenant-a", campaignA),
                Metric("tenant-b", campaignB));
            system.ImportBatches.AddRange(
                Import("tenant-a", importA),
                Import("tenant-b", importB));
            system.ImportRowErrors.AddRange(
                Error("tenant-a", importA),
                Error("tenant-b", importB));
            system.Alerts.AddRange(
                Alert("tenant-a", campaignA, "fingerprint-a"),
                Alert("tenant-b", campaignB, "fingerprint-b"));
            system.AuditEvents.AddRange(
                Audit("tenant-a"),
                Audit("tenant-b"));
            await system.SaveChangesAsync();
        }

        await using var tenantA = database.Context(TenantScope.For("tenant-a"));
        var campaigns = await tenantA.Campaigns.ToListAsync();
        var metrics = await tenantA.DailyMetrics.ToListAsync();
        var imports = await tenantA.ImportBatches.ToListAsync();
        var errors = await tenantA.ImportRowErrors.ToListAsync();
        var alerts = await tenantA.Alerts.ToListAsync();
        var audits = await tenantA.AuditEvents.ToListAsync();
        Assert.Single(campaigns);
        Assert.Single(metrics);
        Assert.Single(imports);
        Assert.Single(errors);
        Assert.Single(alerts);
        Assert.Single(audits);
        Assert.All(campaigns, entity => Assert.Equal("tenant-a", entity.TenantId));
        Assert.All(metrics, entity => Assert.Equal("tenant-a", entity.TenantId));
        Assert.All(imports, entity => Assert.Equal("tenant-a", entity.TenantId));
        Assert.All(errors, entity => Assert.Equal("tenant-a", entity.TenantId));
        Assert.All(alerts, entity => Assert.Equal("tenant-a", entity.TenantId));
        Assert.All(audits, entity => Assert.Equal("tenant-a", entity.TenantId));
    }

    [Theory]
    [InlineData("membership")]
    [InlineData("campaign")]
    [InlineData("metric")]
    [InlineData("import")]
    [InlineData("import-error")]
    [InlineData("alert")]
    [InlineData("audit")]
    public async Task Save_changes_rejects_cross_tenant_writes_for_each_tenant_owned_entity(string entityType)
    {
        await using var database = await TestDatabase.CreateAsync();
        var campaignB = Guid.NewGuid();
        var importB = Guid.NewGuid();
        await using (var system = database.Context(TenantScope.System))
        {
            system.Campaigns.Add(Campaign("tenant-b", campaignB));
            system.ImportBatches.Add(Import("tenant-b", importB));
            await system.SaveChangesAsync();
        }

        await using var tenantA = database.Context(TenantScope.For("tenant-a"));
        object entity = entityType switch
        {
            "membership" => new Membership { TenantId = "tenant-b", SubjectId = "user-2", Role = "analyst" },
            "campaign" => Campaign("tenant-b", Guid.NewGuid()),
            "metric" => Metric("tenant-b", campaignB),
            "import" => Import("tenant-b", Guid.NewGuid()),
            "import-error" => Error("tenant-b", importB),
            "alert" => Alert("tenant-b", campaignB, "cross-tenant-fingerprint"),
            "audit" => Audit("tenant-b"),
            _ => throw new ArgumentOutOfRangeException(nameof(entityType), entityType, null)
        };
        tenantA.Add(entity);

        await Assert.ThrowsAsync<SecurityException>(() => tenantA.SaveChangesAsync());
    }

    [Fact]
    public async Task Composite_foreign_key_blocks_metric_campaign_cross_tenant_association()
    {
        await using var database = await TestDatabase.CreateAsync();
        var campaignB = Guid.NewGuid();
        await using (var system = database.Context(TenantScope.System))
        {
            system.Campaigns.Add(Campaign("tenant-b", campaignB));
            await system.SaveChangesAsync();
            system.DailyMetrics.Add(Metric("tenant-a", campaignB));

            await Assert.ThrowsAsync<DbUpdateException>(() => system.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task Daily_metric_key_is_idempotent_for_repeated_same_day_ingestion()
    {
        await using var database = await TestDatabase.CreateAsync();
        var campaign = Guid.NewGuid();
        await using (var system = database.Context(TenantScope.System))
        {
            system.Campaigns.AddRange(
                Campaign("tenant-a", campaign),
                Campaign("tenant-b", campaign));
            system.DailyMetrics.AddRange(
                Metric("tenant-a", campaign),
                Metric("tenant-b", campaign));
            await system.SaveChangesAsync();
        }

        await using var duplicate = database.Context(TenantScope.System);
        duplicate.DailyMetrics.Add(Metric("tenant-a", campaign));
        await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
    }

    [Fact]
    public async Task Audit_events_are_append_only()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using (var system = database.Context(TenantScope.System))
        {
            system.AuditEvents.Add(Audit("tenant-a"));
            await system.SaveChangesAsync();
        }

        await using var tenantA = database.Context(TenantScope.For("tenant-a"));
        var audit = await tenantA.AuditEvents.SingleAsync();
        audit.Action = "tampered";

        await Assert.ThrowsAsync<InvalidOperationException>(() => tenantA.SaveChangesAsync());

        tenantA.Entry(audit).State = EntityState.Unchanged;
        tenantA.Remove(audit);

        await Assert.ThrowsAsync<InvalidOperationException>(() => tenantA.SaveChangesAsync());
    }

    [Fact]
    public async Task Membership_store_returns_only_roles_inside_scoped_tenant()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using (var system = database.Context(TenantScope.System))
        {
            system.Memberships.AddRange(
                new Membership { TenantId = "tenant-a", SubjectId = "user-1", Role = "analyst" },
                new Membership { TenantId = "tenant-b", SubjectId = "user-1", Role = "tenant-admin" });
            await system.SaveChangesAsync();
        }

        await using var tenantA = database.Context(TenantScope.For("tenant-a"));
        var store = new EfTenantMembershipStore(tenantA);

        var own = await store.FindAsync("user-1", "tenant-a", CancellationToken.None);
        var other = await store.FindAsync("user-1", "tenant-b", CancellationToken.None);

        Assert.NotNull(own);
        Assert.Contains("analyst", own!.Roles);
        Assert.Null(other);
    }

    private static Campaign Campaign(string tenant, Guid id) => new()
    {
        TenantId = tenant,
        Id = id,
        Name = $"Campaign {tenant}",
        Status = "active",
        Currency = "USD",
        Budget = 100m,
        StartsOn = new DateOnly(2026, 1, 1),
        EndsOn = new DateOnly(2026, 1, 31),
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static DailyMetric Metric(string tenant, Guid campaign) => new()
    {
        TenantId = tenant,
        CampaignId = campaign,
        MetricDate = new DateOnly(2026, 1, 1),
        Spend = 10m,
        Revenue = 25m,
        Impressions = 1000,
        Clicks = 100,
        Conversions = 4
    };

    private static ImportBatch Import(string tenant, Guid id) => new()
    {
        TenantId = tenant,
        Id = id,
        UploadedBySubjectId = "user-1",
        UploadedAt = DateTimeOffset.UtcNow,
        FileName = "metrics.csv",
        AcceptedRows = 1,
        RejectedRows = 0,
        Status = "completed"
    };

    private static ImportRowError Error(string tenant, Guid import) => new()
    {
        TenantId = tenant,
        ImportBatchId = import,
        RowNumber = 2,
        ErrorCode = "invalid_date",
        Message = "Date is invalid."
    };

    private static Alert Alert(string tenant, Guid campaign, string fingerprint) => new()
    {
        TenantId = tenant,
        Id = Guid.NewGuid(),
        CampaignId = campaign,
        Fingerprint = fingerprint,
        MetricName = "cpc",
        Score = 80m,
        Severity = "critical",
        Status = "open",
        Observed = 2m,
        Baseline = 1m,
        Confidence = 1m,
        DataSufficiency = "sufficient",
        ReasonCodesJson = "[\"degraded_vs_baseline\"]",
        ScoringVersion = "scoring-v1",
        EvidenceWindowStart = new DateOnly(2026, 1, 1),
        EvidenceWindowEnd = new DateOnly(2026, 1, 7),
        DetectedAt = DateTimeOffset.UtcNow
    };

    private static AuditEvent Audit(string tenant) => new()
    {
        TenantId = tenant,
        Id = Guid.NewGuid(),
        OccurredAt = DateTimeOffset.UtcNow,
        ActorSubjectId = "user-1",
        Action = "campaign.created",
        EntityType = "campaign",
        EntityId = Guid.NewGuid().ToString(),
        PayloadJson = "{}"
    };

    private sealed class TestDatabase(SqliteConnection connection) : IAsyncDisposable
    {
        public AdPulseDbContext Context(TenantScope scope)
        {
            var options = new DbContextOptionsBuilder<AdPulseDbContext>()
                .UseSqlite(connection)
                .Options;
            return new AdPulseDbContext(options, scope);
        }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA foreign_keys = ON;";
                await command.ExecuteNonQueryAsync();
            }

            await using var context = new TestDatabase(connection).Context(TenantScope.System);
            await context.Database.EnsureCreatedAsync();
            context.Tenants.AddRange(
                new Tenant { Id = "tenant-a", Name = "Tenant A", CreatedAt = DateTimeOffset.UtcNow },
                new Tenant { Id = "tenant-b", Name = "Tenant B", CreatedAt = DateTimeOffset.UtcNow });
            await context.SaveChangesAsync();
            return new TestDatabase(connection);
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
