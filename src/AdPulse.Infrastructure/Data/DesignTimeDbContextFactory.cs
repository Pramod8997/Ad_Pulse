using AdPulse.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AdPulse.Infrastructure.Data;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AdPulseDbContext>
{
    public AdPulseDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AdPulseDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=AdPulse;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new AdPulseDbContext(options, TenantScope.System);
    }
}
