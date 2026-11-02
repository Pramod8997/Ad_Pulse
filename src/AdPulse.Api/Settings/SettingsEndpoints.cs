using AdPulse.Domain.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AdPulse.Api.Settings;

public static class SettingsEndpoints
{
    public static IServiceCollection AddTenantSettingsServices(this IServiceCollection services) =>
        services.AddScoped<TenantSettingsService>();

    public static RouteGroupBuilder MapTenantSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/app/settings").RequireAuthorization();
        group.MapGet(string.Empty, GetAsync);
        group.MapPatch(string.Empty, UpdateAsync);
        return group;
    }

    private static async Task<IResult> GetAsync(
        TenantContext tenant,
        TenantSettingsService service,
        CancellationToken cancellationToken)
    {
        var settings = await service.GetAsync(tenant, cancellationToken);
        return Results.Ok(settings);
    }

    private static async Task<IResult> UpdateAsync(
        TenantSettingsUpdate? update,
        TenantContext tenant,
        TenantSettingsService service,
        CancellationToken cancellationToken)
    {
        var settings = await service.UpdateAsync(tenant, update!, cancellationToken);
        return Results.Ok(settings);
    }
}
