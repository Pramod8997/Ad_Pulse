using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AdPulse.Api.Scoring;

public static class ScoringServices
{
    public static IServiceCollection AddScoringServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<ScoringClientOptions>()
            .Bind(configuration.GetSection("Scoring"));
        services.AddHttpClient<IPythonScoringClient, PythonScoringClient>();
        services.AddScoped<ScoringAlertService>();
        return services;
    }
}
