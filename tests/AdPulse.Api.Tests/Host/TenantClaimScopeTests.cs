using System.Security.Claims;
using AdPulse.Api.Tenancy;
using AdPulse.Domain.Tenancy;

namespace AdPulse.Api.Tests.Host;

public sealed class TenantClaimScopeTests
{
    [Fact]
    public void Creates_scope_only_from_one_valid_tenant_claim()
    {
        var principal = Principal("tenant-a");

        var scope = TenantClaimScope.Create(principal);

        Assert.Equal("tenant-a", scope.TenantId);
        Assert.False(scope.IsSystemScope);
    }

    [Theory]
    [InlineData("")]
    [InlineData("tenant|other")]
    public void Rejects_unsafe_tenant_claim(string tenantId)
    {
        Assert.Throws<TenantAccessDeniedException>(() => TenantClaimScope.Create(Principal(tenantId)));
    }

    [Fact]
    public void Rejects_duplicate_tenant_claims()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("tenant_id", "tenant-a"), new Claim("tenant_id", "tenant-b")],
            "test"));

        Assert.Throws<TenantAccessDeniedException>(() => TenantClaimScope.Create(principal));
    }

    [Fact]
    public void Rejects_claims_from_an_unauthenticated_identity()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenant_id", "tenant-a")]));

        Assert.Throws<TenantAccessDeniedException>(() => TenantClaimScope.Create(principal));
    }

    private static ClaimsPrincipal Principal(string tenantId) => new(new ClaimsIdentity(
        [new Claim("tenant_id", tenantId)],
        "test"));
}
