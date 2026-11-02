using System.Security;
using AdPulse.Api.Settings;
using AdPulse.Domain.Tenancy;

namespace AdPulse.Api.Tests.AuditSettings;

public sealed class TenantSettingsServiceTests
{
    [Fact]
    public async Task Tenant_admin_can_persist_and_read_reporting_settings()
    {
        var store = new FakeStore();
        var service = new TenantSettingsService(store);
        var tenant = Tenant("tenant-a", "tenant-admin");

        var saved = await service.UpdateAsync(tenant, new TenantSettingsUpdate("UTC", "USD"));
        var loaded = await service.GetAsync(Tenant("tenant-a", "analyst"));

        Assert.Equal("UTC", saved.ReportingTimeZone);
        Assert.Equal("USD", saved.DefaultCurrency);
        Assert.Equal(saved, loaded);
        Assert.Equal("tenant-a", store.LastSaved?.TenantId);
    }

    [Fact]
    public async Task Non_admin_cannot_read_or_update_settings()
    {
        var service = new TenantSettingsService(new FakeStore());
        var tenant = Tenant("tenant-a", "analyst");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAsync(tenant));
        await Assert.ThrowsAsync<TenantAccessDeniedException>(() =>
            service.UpdateAsync(tenant, new TenantSettingsUpdate("UTC", "USD")));
    }

    [Theory]
    [InlineData("", "USD")]
    [InlineData("not-a-real-timezone", "USD")]
    [InlineData("Pacific Standard Time", "USD")]
    [InlineData("UTC", "usd")]
    [InlineData("UTC", "US$")]
    [InlineData("UTC", "US")]
    public async Task Rejects_invalid_reporting_values(string timeZone, string currency)
    {
        var service = new TenantSettingsService(new FakeStore());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateAsync(Tenant("tenant-a", "tenant-admin"), new TenantSettingsUpdate(timeZone, currency)));
    }

    [Fact]
    public async Task Refuses_a_persisted_record_from_another_tenant()
    {
        var store = new FakeStore
        {
            Existing = new TenantReportingSettings
            {
                TenantId = "tenant-b",
                ReportingTimeZone = "UTC",
                DefaultCurrency = "USD",
                UpdatedAtUtc = DateTimeOffset.UtcNow
            }
        };
        var service = new TenantSettingsService(store);

        await Assert.ThrowsAsync<SecurityException>(() => service.GetAsync(Tenant("tenant-a", "tenant-admin")));
    }

    [Fact]
    public async Task Missing_settings_fail_closed_instead_of_using_machine_defaults()
    {
        var service = new TenantSettingsService(new FakeStore());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetAsync(Tenant("tenant-a", "tenant-admin")));
    }

    private static TenantContext Tenant(string tenantId, string role) =>
        new(tenantId, "user-1", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { role });

    private sealed class FakeStore : ITenantSettingsStore
    {
        public TenantReportingSettings? Existing { get; init; }
        public TenantReportingSettings? LastSaved { get; private set; }

        public Task<TenantReportingSettings?> FindAsync(string tenantId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(LastSaved ?? Existing);
        }

        public Task SaveAsync(TenantReportingSettings settings, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastSaved = settings;
            return Task.CompletedTask;
        }
    }
}
