using AdPulse.Api.Campaigns;
using AdPulse.Domain.Tenancy;
using AdPulse.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AdPulse.Api.Tests.Campaigns;

public sealed class CampaignServiceAuthorizationTests
{
    [Fact]
    public void Rejects_a_system_or_mismatched_db_scope()
    {
        var options = new DbContextOptionsBuilder<AdPulseDbContext>()
            .UseSqlServer("Server=campaign-test;Database=unused;TrustServerCertificate=True")
            .Options;
        var tenant = new TenantContext("tenant-a", "subject-a", new HashSet<string>());

        using var systemDb = new AdPulseDbContext(options, TenantScope.System);
        Assert.Throws<InvalidOperationException>(() => new CampaignService(systemDb, tenant, new AuditEventWriter()));

        using var otherTenantDb = new AdPulseDbContext(options, TenantScope.For("tenant-b"));
        Assert.Throws<InvalidOperationException>(() => new CampaignService(otherTenantDb, tenant, new AuditEventWriter()));
    }
}
