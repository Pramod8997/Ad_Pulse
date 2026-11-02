using System.Security.Claims;

namespace AdPulse.Domain.Tenancy;

public sealed record TenantMembership(string TenantId, string SubjectId, IReadOnlySet<string> Roles);

public interface ITenantMembershipStore
{
    Task<TenantMembership?> FindAsync(string subjectId, string tenantId, CancellationToken cancellationToken);
}

public sealed record TenantContext(string TenantId, string SubjectId, IReadOnlySet<string> Roles)
{
    public bool IsInRole(string role) => Roles.Contains(role);
}

public sealed class TenantAccessDeniedException(string message) : UnauthorizedAccessException(message);

public sealed class TenantContextResolver(ITenantMembershipStore memberships)
{
    public async Task<TenantContext> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            throw new TenantAccessDeniedException("An authenticated identity is required.");
        }

        var subjectId = SingleClaim(principal, "sub", 128);
        var tenantId = SingleClaim(principal, "tenant_id", 64);
        var membership = await memberships.FindAsync(subjectId, tenantId, cancellationToken);

        if (membership is null
            || !string.Equals(membership.SubjectId, subjectId, StringComparison.Ordinal)
            || !string.Equals(membership.TenantId, tenantId, StringComparison.Ordinal))
        {
            throw new TenantAccessDeniedException("The identity is not a member of the requested tenant.");
        }

        return new TenantContext(membership.TenantId, membership.SubjectId, membership.Roles);
    }

    private static string SingleClaim(ClaimsPrincipal principal, string claimType, int maxLength)
    {
        var claims = principal.FindAll(claimType).ToArray();
        if (claims.Length != 1)
        {
            throw new TenantAccessDeniedException($"Exactly one valid {claimType} claim is required.");
        }

        var value = claims[0].Value;
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength || value.Contains('|', StringComparison.Ordinal))
        {
            throw new TenantAccessDeniedException($"Exactly one valid {claimType} claim is required.");
        }

        return value;
    }
}
