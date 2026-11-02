using System.Security.Claims;
using AdPulse.Domain.Tenancy;

namespace AdPulse.Api.Tenancy;

public static class TenantClaimScope
{
    public static TenantScope Create(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var claims = principal.FindAll("tenant_id").ToArray();
        if (claims.Length != 1)
        {
            throw new TenantAccessDeniedException("Exactly one valid tenant_id claim is required.");
        }

        var tenantId = claims[0].Value;
        if (string.IsNullOrWhiteSpace(tenantId)
            || tenantId.Length > 64
            || tenantId.Contains('|', StringComparison.Ordinal))
        {
            throw new TenantAccessDeniedException("Exactly one valid tenant_id claim is required.");
        }

        return TenantScope.For(tenantId);
    }
}
