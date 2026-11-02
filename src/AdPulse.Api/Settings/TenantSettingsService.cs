using System.Security;
using AdPulse.Domain.Tenancy;

namespace AdPulse.Api.Settings;

public sealed record TenantReportingSettings
{
    public string TenantId { get; init; } = string.Empty;
    public string ReportingTimeZone { get; init; } = string.Empty;
    public string? DefaultCurrency { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed record TenantSettingsUpdate(string ReportingTimeZone, string? DefaultCurrency);

public sealed record TenantSettingsResponse(
    string ReportingTimeZone,
    string? DefaultCurrency,
    DateTimeOffset UpdatedAtUtc);

public interface ITenantSettingsStore
{
    Task<TenantReportingSettings?> FindAsync(string tenantId, CancellationToken cancellationToken);

    Task SaveAsync(TenantReportingSettings settings, CancellationToken cancellationToken);
}

public sealed class TenantSettingsService
{
    private const string TenantAdminRole = "tenant-admin";
    private readonly ITenantSettingsStore _store;

    public TenantSettingsService(ITenantSettingsStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<TenantSettingsResponse> GetAsync(
        TenantContext tenant,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant(tenant);
        var settings = await _store.FindAsync(tenant.TenantId, cancellationToken);
        return ToResponse(tenant, settings);
    }

    public async Task<TenantSettingsResponse> UpdateAsync(
        TenantContext tenant,
        TenantSettingsUpdate update,
        CancellationToken cancellationToken = default)
    {
        RequireTenantAdmin(tenant);
        ArgumentNullException.ThrowIfNull(update);

        var validated = ValidateValues(update.ReportingTimeZone, update.DefaultCurrency);
        var settings = new TenantReportingSettings
        {
            TenantId = tenant.TenantId,
            ReportingTimeZone = validated.ReportingTimeZone,
            DefaultCurrency = validated.DefaultCurrency,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await _store.SaveAsync(settings, cancellationToken);
        return ToResponse(tenant, settings);
    }

    private static TenantSettingsResponse ToResponse(
        TenantContext tenant,
        TenantReportingSettings? settings)
    {
        if (settings is null)
        {
            throw new InvalidOperationException("Reporting settings have not been configured for this tenant.");
        }

        EnsureTenantValue(tenant, settings);
        var validated = ValidateValues(settings.ReportingTimeZone, settings.DefaultCurrency);
        return new TenantSettingsResponse(
            validated.ReportingTimeZone,
            validated.DefaultCurrency,
            settings.UpdatedAtUtc.ToUniversalTime());
    }

    private static void EnsureTenant(TenantContext? tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        if (string.IsNullOrWhiteSpace(tenant.TenantId) || string.IsNullOrWhiteSpace(tenant.SubjectId))
        {
            throw new TenantAccessDeniedException("A validated tenant identity is required for tenant settings.");
        }
    }

    private static void RequireTenantAdmin(TenantContext? tenant)
    {
        EnsureTenant(tenant);
        var validatedTenant = tenant!;
        if (!validatedTenant.Roles.Any(role => string.Equals(role, TenantAdminRole, StringComparison.OrdinalIgnoreCase)))
        {
            throw new TenantAccessDeniedException("The tenant-admin role is required for tenant settings.");
        }
    }

    private static void EnsureTenantValue(TenantContext tenant, TenantReportingSettings settings)
    {
        if (!string.Equals(settings.TenantId, tenant.TenantId, StringComparison.Ordinal))
        {
            throw new SecurityException("The settings record is outside the authenticated tenant scope.");
        }
    }

    private static ValidatedValues ValidateValues(string? timeZoneId, string? currency)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId)
            || timeZoneId != timeZoneId.Trim()
            || timeZoneId.Length > 128
            || (timeZoneId != "UTC" && !timeZoneId.Contains('/', StringComparison.Ordinal)))
        {
            throw new ArgumentException("A valid reporting timezone identifier is required.", nameof(timeZoneId));
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException exception)
        {
            throw new ArgumentException("The reporting timezone identifier is not installed.", nameof(timeZoneId), exception);
        }
        catch (InvalidTimeZoneException exception)
        {
            throw new ArgumentException("The reporting timezone identifier is invalid.", nameof(timeZoneId), exception);
        }

        if (currency is not null
            && (currency.Length != 3
                || currency != currency.Trim()
                || currency.Any(character => character is < 'A' or > 'Z')))
        {
            throw new ArgumentException("Default currency must be exactly three uppercase ASCII letters when supplied.", nameof(currency));
        }

        return new ValidatedValues(timeZoneId, currency);
    }

    private sealed record ValidatedValues(string ReportingTimeZone, string? DefaultCurrency);
}
