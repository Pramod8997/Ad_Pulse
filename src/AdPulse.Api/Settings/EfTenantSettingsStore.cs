using AdPulse.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AdPulse.Api.Settings;

public sealed class EfTenantSettingsStore(AdPulseDbContext db) : ITenantSettingsStore
{
    public async Task<TenantReportingSettings?> FindAsync(string tenantId, CancellationToken cancellationToken)
    {
        var setting = await db.TenantSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        return setting is null
            ? null
            : new TenantReportingSettings
            {
                TenantId = setting.TenantId,
                ReportingTimeZone = setting.ReportingTimeZone,
                DefaultCurrency = setting.DefaultCurrency,
                UpdatedAtUtc = setting.UpdatedAtUtc
            };
    }

    public async Task SaveAsync(TenantReportingSettings settings, CancellationToken cancellationToken)
    {
        var existing = await db.TenantSettings
            .SingleOrDefaultAsync(x => x.TenantId == settings.TenantId, cancellationToken);

        if (existing is null)
        {
            db.TenantSettings.Add(new TenantSetting
            {
                TenantId = settings.TenantId,
                ReportingTimeZone = settings.ReportingTimeZone,
                DefaultCurrency = settings.DefaultCurrency,
                UpdatedAtUtc = settings.UpdatedAtUtc
            });
        }
        else
        {
            existing.ReportingTimeZone = settings.ReportingTimeZone;
            existing.DefaultCurrency = settings.DefaultCurrency;
            existing.UpdatedAtUtc = settings.UpdatedAtUtc;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
