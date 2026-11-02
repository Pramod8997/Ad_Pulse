using AdPulse.Api.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AdPulse.Api.Overview;

public static class OverviewEndpoints
{
    public static IServiceCollection AddOverviewServices(this IServiceCollection services) =>
        services.AddScoped<OverviewService>();

    public static RouteGroupBuilder MapOverviewEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/app").RequireAuthorization();
        group.MapGet(string.Empty, GetAsync);
        return group;
    }

    private static async Task<IResult> GetAsync(
        [AsParameters] OverviewQuery query,
        OverviewService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.GetAsync(query, cancellationToken));
        }
        catch (OverviewValidationException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["overview"] = [exception.Message]
            });
        }
    }
}
