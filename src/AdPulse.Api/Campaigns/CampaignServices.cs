using Microsoft.Extensions.DependencyInjection;

namespace AdPulse.Api.Campaigns;

public static class CampaignServices
{
    public static IServiceCollection AddCampaignServices(this IServiceCollection services)
    {
        services.AddScoped<IAuditEventWriter, AuditEventWriter>();
        services.AddScoped<CampaignService>();
        return services;
    }
}
