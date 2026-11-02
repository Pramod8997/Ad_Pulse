using AdPulse.Api.Scoring;
using AdPulse.Api.Alerts;
using AdPulse.Domain.Analytics;
using AdPulse.Domain.Tenancy;
using AdPulse.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AdPulse.Api.Tests.Scoring;

public sealed class ScoringAlertServiceTests
{
    [Fact]
    public async Task Rejects_request_outside_current_tenant_before_calling_scorer()
    {
        await using var db = Context("tenant-a");
        var scorer = new StubScorer(HealthyResult());
        var service = new ScoringAlertService(scorer, new AlertService(db));

        await Assert.ThrowsAsync<TenantAccessDeniedException>(() => service.ScoreAndPersistAsync(
            new TenantContext("tenant-a", "user-1", new HashSet<string>()),
            Request("tenant-b"),
            null,
            DateTimeOffset.UtcNow));

        Assert.Equal(0, scorer.Calls);
    }

    [Fact]
    public async Task Does_not_persist_a_non_alert_result()
    {
        await using var db = Context("tenant-a");
        var service = new ScoringAlertService(new StubScorer(HealthyResult()), new AlertService(db));

        var alert = await service.ScoreAndPersistAsync(
            new TenantContext("tenant-a", "user-1", new HashSet<string>()),
            Request("tenant-a"),
            null,
            DateTimeOffset.UtcNow);

        Assert.Null(alert);
    }

    private static AdPulseDbContext Context(string tenant) => new(
        new DbContextOptionsBuilder<AdPulseDbContext>().Options,
        TenantScope.For(tenant));

    private static ScoringRequest Request(string tenant) => new(
        tenant,
        "campaign-1",
        "cpc",
        1m,
        1m,
        30,
        7,
        MetricDirection.LowerIsBetter,
        new DateOnly(2026, 1, 1),
        new DateOnly(2026, 1, 7));

    private static ScoringResult HealthyResult() => new(
        0m,
        ScoringSeverity.None,
        1m,
        "sufficient",
        ["within_baseline"],
        "e4ad1cdb9f542791ffc036de21f9e8317e2fb71f23e056ea5d21ee4a87ebbc43");

    private sealed class StubScorer(ScoringResult result) : IPythonScoringClient
    {
        public int Calls { get; private set; }

        public Task<ScoringResult> ScoreAsync(ScoringRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }
}
