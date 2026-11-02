using System.Security;
using AdPulse.Api.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AdPulse.Api.Metrics;

public static class MetricEndpoints
{
    public static IServiceCollection AddMetricServices(this IServiceCollection services) =>
        services.AddScoped<DailyMetricService>();

    public static IServiceCollection AddMetrics(this IServiceCollection services) =>
        services.AddMetricServices();

    public static RouteGroupBuilder MapMetricEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/app/campaigns/{campaignId:guid}/metrics")
            .RequireAuthorization();
        group.MapGet(string.Empty, ReadAsync);
        group.MapPost(string.Empty, PostAsync);
        group.MapPut("/{metricDate}", PutAsync);
        return group;
    }

    public static RouteGroupBuilder MapMetrics(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapMetricEndpoints();

    private static async Task<IResult> ReadAsync(
        Guid campaignId,
        [AsParameters] MetricListQuery query,
        DailyMetricService metrics,
        CancellationToken cancellationToken)
    {
        try
        {
            var page = await metrics.ReadAsync(campaignId, query, cancellationToken);
            return Results.Ok(new CursorPage<DailyMetricDto>
            {
                Items = page.Items.Select(metric => metric.ToContract()).ToArray(),
                NextCursor = page.NextCursor,
                HasMore = page.HasMore,
                Limit = page.Limit
            });
        }
        catch (SecurityException)
        {
            return Results.Forbid();
        }
        catch (MetricRequestValidationException exception)
        {
            return Results.ValidationProblem(exception.Errors.ToDictionary(pair => pair.Key, pair => pair.Value));
        }
        catch (MetricNotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static Task<IResult> PostAsync(
        Guid campaignId,
        DailyMetricIngestRequest request,
        DailyMetricService metrics,
        CancellationToken cancellationToken) =>
        IngestAsync(campaignId, request.MetricDate, request, metrics, cancellationToken);

    private static Task<IResult> PutAsync(
        Guid campaignId,
        DateOnly metricDate,
        UpsertDailyMetricRequest request,
        DailyMetricService metrics,
        CancellationToken cancellationToken) =>
        IngestAsync(
            campaignId,
            metricDate,
            new DailyMetricIngestRequest(
                metricDate,
                request.Spend,
                request.Revenue,
                request.Impressions,
                request.Clicks,
                request.Conversions),
            metrics,
            cancellationToken);

    private static async Task<IResult> IngestAsync(
        Guid campaignId,
        DateOnly metricDate,
        DailyMetricIngestRequest request,
        DailyMetricService metrics,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.MetricDate != metricDate)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.MetricDate)] = ["The route date and request date must match."]
                });
            }

            var result = await metrics.UpsertAsync(campaignId, request, cancellationToken);
            if (!result.Created)
            {
                return Results.Ok(result.Metric.ToContract());
            }

            var location = $"/app/campaigns/{campaignId}/metrics/{request.MetricDate:yyyy-MM-dd}";
            return Results.Created(location, result.Metric.ToContract());
        }
        catch (SecurityException)
        {
            return Results.Forbid();
        }
        catch (MetricRequestValidationException exception)
        {
            return Results.ValidationProblem(exception.Errors.ToDictionary(pair => pair.Key, pair => pair.Value));
        }
        catch (MetricNotFoundException)
        {
            return Results.NotFound();
        }
    }
}
