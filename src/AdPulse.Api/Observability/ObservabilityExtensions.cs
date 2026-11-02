using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace AdPulse.Api.Observability;

public static class ObservabilityExtensions
{
    public const string CorrelationHeaderName = "X-Correlation-ID";

    public static IServiceCollection AddAdPulseObservability(this IServiceCollection services)
    {
        services.AddSingleton<IRedactedLogSanitizer, RedactedLogSanitizer>();
        return services;
    }

    public static IApplicationBuilder UseAdPulseObservability(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationMiddleware>();
    }
}
