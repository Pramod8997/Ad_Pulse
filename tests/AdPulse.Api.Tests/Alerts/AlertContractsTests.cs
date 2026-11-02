using AdPulse.Api.Alerts;
using AdPulse.Domain.Tenancy;
using AdPulse.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AdPulse.Api.Tests.Alerts;

public sealed class AlertContractsTests
{
    [Fact]
    public void List_query_normalizes_filters_and_keeps_stable_bounds()
    {
        var query = new AlertListQuery("cursor", 50, " ACKNOWLEDGED ", " CRITICAL ", " CPC ", Guid.NewGuid()).Validate();

        Assert.Equal("cursor", query.Cursor);
        Assert.Equal(50, query.Limit);
        Assert.Equal("acknowledged", query.Status);
        Assert.Equal("critical", query.Severity);
        Assert.Equal("cpc", query.Metric);
    }

    [Theory]
    [InlineData(101)]
    [InlineData(0)]
    public void List_query_rejects_unbounded_paging(int limit)
    {
        Assert.Throws<AlertValidationException>(() => new AlertListQuery(Limit: limit).Validate());
    }

    [Fact]
    public void Mutation_requires_confirmation_and_a_bounded_reason()
    {
        Assert.Throws<AlertValidationException>(() => new AlertMutationRequest(false, "reason").ValidateAndGetReason());
        Assert.Equal(string.Empty, new AlertMutationRequest(true, " ").ValidateAndGetReason());
        Assert.Throws<AlertValidationException>(() => new AlertMutationRequest(true, " ").ValidateAndGetReason(true));
        Assert.Throws<AlertValidationException>(() => new AlertMutationRequest(true, new string('x', 501)).ValidateAndGetReason());
        Assert.Equal("operator reviewed", new AlertMutationRequest(true, " operator reviewed ").ValidateAndGetReason());
    }

    [Fact]
    public void Scored_alert_rejects_noncanonical_fingerprint()
    {
        var scored = ValidScoredAlert() with { Fingerprint = "not-a-fingerprint" };

        Assert.Throws<AlertValidationException>(scored.Validate);
    }

    [Fact]
    public void Scored_alert_keeps_confidence_as_a_decimal_sufficiency_value()
    {
        var scored = ValidScoredAlert() with { Confidence = 0.2143m, DataSufficiency = "insufficient" };

        scored.Validate();
        Assert.Equal(0.2143m, scored.Confidence);
        Assert.Equal("insufficient", scored.DataSufficiency);
    }

    [Fact]
    public async Task Service_rejects_a_context_that_does_not_match_the_validated_tenant()
    {
        var options = new DbContextOptionsBuilder<AdPulseDbContext>().Options;
        await using var db = new AdPulseDbContext(options, TenantScope.For("tenant-a"));
        var service = new AlertService(db);
        var access = new TenantContext("tenant-b", "user-1", new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        await Assert.ThrowsAsync<TenantAccessDeniedException>(() => service.GetAsync(access, Guid.NewGuid()));
    }

    [Fact]
    public async Task Service_rejects_system_scope_even_when_the_request_contains_a_tenant_context()
    {
        var options = new DbContextOptionsBuilder<AdPulseDbContext>().Options;
        await using var db = new AdPulseDbContext(options, TenantScope.System);
        var service = new AlertService(db);
        var access = new TenantContext("tenant-a", "user-1", new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        await Assert.ThrowsAsync<TenantAccessDeniedException>(() => service.GetAsync(access, Guid.NewGuid()));
    }

    private static ScoredAlert ValidScoredAlert() => new(
        null,
        "cpc",
        80m,
        "critical",
        2m,
        1m,
        1m,
        "sufficient",
        ["degraded_vs_baseline"],
        "scoring-v1",
        new DateOnly(2026, 1, 1),
        new DateOnly(2026, 1, 7),
        new string('a', 64));
}
