using AdPulse.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace AdPulse.Infrastructure.Data;

public sealed class EfTenantMembershipStore(AdPulseDbContext db) : ITenantMembershipStore
{
    public async Task<TenantMembership?> FindAsync(string subjectId, string tenantId, CancellationToken cancellationToken)
    {
        var roles = await db.Memberships
            .AsNoTracking()
            .Where(x => x.SubjectId == subjectId && x.TenantId == tenantId)
            .Select(x => x.Role)
            .ToArrayAsync(cancellationToken);

        return roles.Length == 0
            ? null
            : new TenantMembership(tenantId, subjectId, roles.ToHashSet(StringComparer.OrdinalIgnoreCase));
    }
}
