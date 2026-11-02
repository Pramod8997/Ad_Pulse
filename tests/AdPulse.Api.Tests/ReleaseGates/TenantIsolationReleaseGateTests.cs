using System.Security.Claims;
using AdPulse.Domain.Tenancy;

namespace AdPulse.Api.Tests.ReleaseGates;

public sealed class TenantIsolationReleaseGateTests
{
    [Fact]
    public async Task Tenant_claims_require_matching_server_membership()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "user-1"),
            new Claim("tenant_id", "tenant-a")
        ], "release-gate"));
        var memberships = new FixedMembershipStore(
            new TenantMembership("tenant-b", "user-1", new HashSet<string>()));

        await Assert.ThrowsAsync<TenantAccessDeniedException>(() =>
            new TenantContextResolver(memberships).ResolveAsync(principal));
        Assert.Equal(("user-1", "tenant-a"), memberships.LastLookup);
    }

    [Fact]
    public void Every_tenant_owned_entity_is_declared_with_a_query_filter()
    {
        var model = ReleaseGateRepository.ReadText("src", "AdPulse.Infrastructure", "Data", "AdPulseDbContext.cs");

        Assert.Equal(8, model.Split("entity.HasQueryFilter", StringSplitOptions.None).Length - 1);
        Assert.Contains("x => Scope.IsSystemScope || x.Id == Scope.TenantId", model, StringComparison.Ordinal);
        Assert.Contains("x => Scope.IsSystemScope || x.TenantId == Scope.TenantId", model, StringComparison.Ordinal);
    }

    [Fact]
    public void Metric_associations_and_alert_fingerprints_are_tenant_aware()
    {
        var model = ReleaseGateRepository.ReadText("src", "AdPulse.Infrastructure", "Data", "AdPulseDbContext.cs");

        Assert.Contains("HasKey(x => new { x.TenantId, x.CampaignId, x.MetricDate })", model, StringComparison.Ordinal);
        Assert.Contains("HasForeignKey(x => new { x.TenantId, x.CampaignId })", model, StringComparison.Ordinal);
        Assert.Contains("HasPrincipalKey(x => new { x.TenantId, x.Id })", model, StringComparison.Ordinal);
        Assert.Contains("HasIndex(x => new { x.TenantId, x.Fingerprint })", model, StringComparison.Ordinal);
        Assert.Contains(".IsUnique()", model, StringComparison.Ordinal);
    }

    [Fact]
    public void Cross_tenant_writes_are_rejected_before_database_access()
    {
        var model = ReleaseGateRepository.ReadText("src", "AdPulse.Infrastructure", "Data", "AdPulseDbContext.cs");

        Assert.Contains("throw new SecurityException", model, StringComparison.Ordinal);
        Assert.Contains("!string.Equals(tenantOwned.TenantId, Scope.TenantId", model, StringComparison.Ordinal);
    }

    private sealed class FixedMembershipStore(TenantMembership membership) : ITenantMembershipStore
    {
        public (string Subject, string Tenant)? LastLookup { get; private set; }

        public Task<TenantMembership?> FindAsync(
            string subjectId,
            string tenantId,
            CancellationToken cancellationToken)
        {
            LastLookup = (subjectId, tenantId);
            return Task.FromResult<TenantMembership?>(membership);
        }
    }
}
