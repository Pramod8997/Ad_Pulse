using AdPulse.Api.Alerts;
using AdPulse.Domain.Tenancy;
using AdPulse.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AdPulse.Api.Tests.Alerts;

public sealed class IncidentWorkflowTests
{
    [Fact]
    public async Task Assignment_notes_and_resolution_are_persisted_in_a_tenant_scoped_timeline()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context(TenantScope.For("tenant-a"));
        var service = new AlertService(db);
        var access = Access("tenant-a", "operator-1");

        var assigned = await service.AssignAsync(access, database.AlertId, new AlertAssignmentRequest("operator-2", true));
        Assert.NotNull(assigned);
        Assert.Equal("operator-2", assigned!.Alert.OwnerSubjectId);

        var noted = await service.AddNoteAsync(access, database.AlertId, new IncidentNoteRequest("Validated the tracking change."));
        Assert.NotNull(noted);
        Assert.Contains(noted!.Timeline, entry => entry.Action == "incident.note_added" && entry.Note == "Validated the tracking change.");

        await service.ChangeIncidentStatusAsync(access, database.AlertId, new IncidentStatusChangeRequest("investigating", "Reviewing evidence", true));
        var resolved = await service.ChangeIncidentStatusAsync(access, database.AlertId, new IncidentStatusChangeRequest("resolved", "Tracking was corrected and verified.", true));

        Assert.NotNull(resolved);
        Assert.Equal("resolved", resolved!.Alert.IncidentStatus);
        Assert.Equal("resolved", resolved.Alert.Status);
        Assert.Equal("Tracking was corrected and verified.", resolved.Alert.ResolutionSummary);
        Assert.Equal("operator-1", resolved.Alert.ResolvedBySubjectId);
        Assert.Contains(resolved.Alert.Timeline, entry => entry.Action == "incident.resolved" && entry.ToIncidentStatus == "resolved");
    }

    [Fact]
    public async Task Assignment_rejects_a_subject_outside_the_current_tenant()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context(TenantScope.For("tenant-a"));
        var service = new AlertService(db);

        await Assert.ThrowsAsync<AlertValidationException>(() => service.AssignAsync(
            Access("tenant-a", "operator-1"),
            database.AlertId,
            new AlertAssignmentRequest("tenant-b-operator", true)));

        await Assert.ThrowsAsync<TenantAccessDeniedException>(() => service.GetAsync(Access("tenant-b", "operator-1"), database.AlertId));
    }

    [Fact]
    public async Task False_positive_requires_a_reason_and_is_queryable()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context(TenantScope.For("tenant-a"));
        var service = new AlertService(db);

        await Assert.ThrowsAsync<AlertValidationException>(() => service.ChangeIncidentStatusAsync(
            Access("tenant-a", "operator-1"),
            database.AlertId,
            new IncidentStatusChangeRequest("false_positive", null, true)));

        var outcome = await service.ChangeIncidentStatusAsync(
            Access("tenant-a", "operator-1"),
            database.AlertId,
            new IncidentStatusChangeRequest("false_positive", "Expected seasonal variance.", true));

        Assert.Equal("false_positive", outcome!.Alert.IncidentStatus);
        Assert.Equal("Expected seasonal variance.", outcome.Alert.FalsePositiveReason);
    }

    private static TenantContext Access(string tenantId, string subjectId) =>
        new(tenantId, subjectId, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "analyst" });

    private sealed class TestDatabase(SqliteConnection connection) : IAsyncDisposable
    {
        public Guid AlertId { get; } = Guid.NewGuid();

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
            var database = new TestDatabase(connection);
            await using var context = database.Context(TenantScope.System);
            await context.Database.EnsureCreatedAsync();
            context.Tenants.AddRange(
                new Tenant { Id = "tenant-a", Name = "Tenant A", CreatedAt = DateTimeOffset.UtcNow },
                new Tenant { Id = "tenant-b", Name = "Tenant B", CreatedAt = DateTimeOffset.UtcNow });
            context.Memberships.AddRange(
                new Membership { TenantId = "tenant-a", SubjectId = "operator-1", Role = "analyst" },
                new Membership { TenantId = "tenant-a", SubjectId = "operator-2", Role = "analyst" },
                new Membership { TenantId = "tenant-b", SubjectId = "tenant-b-operator", Role = "analyst" },
                new Membership { TenantId = "tenant-b", SubjectId = "operator-1", Role = "analyst" });
            var campaignId = Guid.NewGuid();
            context.Campaigns.Add(new Campaign
            {
                TenantId = "tenant-a",
                Id = campaignId,
                Name = "Campaign A",
                Status = "active",
                Currency = "USD",
                Budget = 100m,
                StartsOn = new DateOnly(2026, 1, 1),
                EndsOn = new DateOnly(2026, 1, 31),
                CreatedAt = DateTimeOffset.UtcNow
            });
            context.Alerts.Add(new Alert
            {
                TenantId = "tenant-a",
                Id = database.AlertId,
                CampaignId = campaignId,
                Fingerprint = new string('a', 64),
                MetricName = "cpc",
                Score = 80m,
                Severity = "critical",
                Status = "open",
                IncidentStatus = "new",
                Observed = 2m,
                Baseline = 1m,
                Confidence = 1m,
                DataSufficiency = "sufficient",
                ReasonCodesJson = "[\"degraded_vs_baseline\"]",
                ScoringVersion = "scoring-v1",
                EvidenceWindowStart = new DateOnly(2026, 1, 1),
                EvidenceWindowEnd = new DateOnly(2026, 1, 7),
                DetectedAt = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync();
            return database;
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
