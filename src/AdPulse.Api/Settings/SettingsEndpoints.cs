using System.Security;
using AdPulse.Domain.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AdPulse.Api.Settings;

public static class SettingsEndpoints
{
    public static IServiceCollection AddTenantSettingsServices(this IServiceCollection services) =>
        services
            .AddScoped<ITenantSettingsStore, EfTenantSettingsStore>()
            .AddScoped<TenantSettingsService>();

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
        if (!TenantSettingsService.IsTenantAdmin(tenant))
        {
            return Results.Forbid();
        }

        try
        {
            var settings = await service.GetAsync(tenant, cancellationToken);
            return Results.Ok(settings);
        }
        catch (Exception exception) when (exception is TenantAccessDeniedException or SecurityException)
        {
            return Results.Forbid();
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> UpdateAsync(
        TenantSettingsUpdate? update,
        TenantContext tenant,
        TenantSettingsService service,
        CancellationToken cancellationToken)
    {
        if (!TenantSettingsService.IsTenantAdmin(tenant))
        {
            return Results.Forbid();
        }

        if (update is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(update)] = ["A settings update payload is required."]
            });
        }

        try
        {
            var settings = await service.UpdateAsync(tenant, update, cancellationToken);
            return Results.Ok(settings);
        }
        catch (Exception exception) when (exception is TenantAccessDeniedException or SecurityException)
        {
            return Results.Forbid();
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [string.IsNullOrWhiteSpace(exception.ParamName) ? "settings" : exception.ParamName] = [exception.Message]
            });
        }
    }
}
