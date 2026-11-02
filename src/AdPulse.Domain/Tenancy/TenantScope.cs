namespace AdPulse.Domain.Tenancy;

public sealed class TenantScope
{
    private TenantScope(string? tenantId, bool isSystemScope)
    {
        TenantId = tenantId;
        IsSystemScope = isSystemScope;
    }

    public string? TenantId { get; }

    public bool IsSystemScope { get; }

    public static TenantScope For(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || tenantId.Length > 64)
        {
            throw new ArgumentException("A tenant identifier is required and must be at most 64 characters.", nameof(tenantId));
        }

        return new TenantScope(tenantId, false);
    }

    // Only migrations and controlled maintenance may use this scope; web requests never do.
    public static TenantScope System => new(null, true);
}
