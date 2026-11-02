using AdPulse.Domain.Tenancy;
using Microsoft.AspNetCore.Http;

namespace AdPulse.Api.Tenancy;

public sealed class TenantRequestContext(IHttpContextAccessor httpContextAccessor)
{
    private TenantContext? _current;

    public TenantContext? Current => _current;

    public TenantScope CandidateScope
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User
                ?? throw new TenantAccessDeniedException("An HTTP identity is required.");

            return TenantClaimScope.Create(principal);
        }
    }

    public void SetResolved(TenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);

        if (!string.Equals(CandidateScope.TenantId, tenantContext.TenantId, StringComparison.Ordinal))
        {
            throw new TenantAccessDeniedException("The resolved tenant does not match the authenticated identity.");
        }

        _current = tenantContext;
    }

    public TenantContext RequireResolved() => _current
        ?? throw new TenantAccessDeniedException("A verified tenant membership is required.");
}
