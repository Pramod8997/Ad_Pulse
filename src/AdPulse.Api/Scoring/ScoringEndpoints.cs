using System.Text.Json;
using AdPulse.Api.Alerts;
using AdPulse.Domain.Analytics;
using AdPulse.Domain.Tenancy;
using AdPulse.Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AdPulse.Api.Scoring;

public sealed record ScoreCampaignRequest(string? MetricName = null);

public sealed record ScoreCampaignResponse(AlertDetails? Alert, bool Created);

public static class ScoringEndpoints
{
    private const int MinimumDataPoints = 7;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static RouteGroupBuilder MapScoringEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/app/campaigns").RequireAuthorization();
        group.MapPost("/{campaignId:guid}/score", ScoreCampaignAsync);
        return group;
    }

    private static async Task<IResult> ScoreCampaignAsync(
        Guid campaignId,
        HttpContext httpContext,
        TenantContextResolver tenantResolver,
        AdPulseDbContext db,
        ScoringAlertService scoringAlerts,
        CancellationToken cancellationToken)
    {
        TenantContext tenant;
        try
        {
            tenant = await tenantResolver.ResolveAsync(httpContext.User, cancellationToken);
        }
        catch (TenantAccessDeniedException)
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Access denied.");
        }

        if (db.Scope.IsSystemScope
            || !string.Equals(db.Scope.TenantId, tenant.TenantId, StringComparison.Ordinal))
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Access denied.");
        }

        var (body, bodyError) = await TryReadRequestAsync(httpContext.Request, cancellationToken);
        if (bodyError is not null)
        {
            return bodyError;
        }

        var metricName = body!.MetricName is null
            ? "cpc"
            : body.MetricName.Trim().ToLowerInvariant();
        if (metricName is not ("cpc" or "cpa" or "ctr" or "roas"))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["metricName"] = ["MetricName must be cpc, cpa, ctr or roas."]
            });
        }

        var campaignExists = await db.Campaigns
            .AsNoTracking()
            .AnyAsync(campaign => campaign.Id == campaignId, cancellationToken);
        if (!campaignExists)
        {
            return Results.NotFound();
        }

        var metrics = await db.DailyMetrics
            .AsNoTracking()
            .Where(metric => metric.CampaignId == campaignId)
            .OrderByDescending(metric => metric.MetricDate)
            .ToListAsync(cancellationToken);

        if (metrics.Count < 2)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["metrics"] = ["At least two daily metric records are required to score a campaign."]
                },
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var observed = SelectKpi(metrics[0], metricName);
        if (observed is null)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["metrics"] = [$"The latest daily metric has a zero denominator for '{metricName}'."]
                },
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var priorValues = metrics
            .Skip(1)
            .Select(metric => SelectKpi(metric, metricName))
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();

        if (priorValues.Length == 0)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["metrics"] = [$"At least one prior daily metric with a non-zero denominator is required for '{metricName}'."]
                },
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var baseline = priorValues.Sum() / priorValues.Length;
        var direction = metricName is "ctr" or "roas"
            ? MetricDirection.HigherIsBetter
            : MetricDirection.LowerIsBetter;

        var scoringRequest = new ScoringRequest(
            tenant.TenantId,
            campaignId.ToString("D"),
            metricName,
            observed.Value,
            baseline,
            metrics.Count,
            MinimumDataPoints,
            direction,
            metrics[^1].MetricDate,
            metrics[0].MetricDate);

        var existingAlertIds = (await db.Alerts
            .AsNoTracking()
            .Where(alert => alert.CampaignId == campaignId)
            .Select(alert => alert.Id)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        try
        {
            var alert = await scoringAlerts.ScoreAndPersistAsync(
                tenant,
                scoringRequest,
                campaignId,
                DateTimeOffset.UtcNow,
                cancellationToken);
            var created = alert is not null && !existingAlertIds.Contains(alert.Id);
            return Results.Ok(new ScoreCampaignResponse(alert, created));
        }
        catch (ScoringClientException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Scoring service error.",
                detail: "The scoring service could not process the request.");
        }
        catch (HttpRequestException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Scoring service error.",
                detail: "The scoring service could not process the request.");
        }
        catch (TimeoutException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Scoring service error.",
                detail: "The scoring service did not respond in time.");
        }
    }

    private static async Task<(ScoreCampaignRequest? Request, IResult? Error)> TryReadRequestAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength is 0)
        {
            return (new ScoreCampaignRequest(), null);
        }

        using var reader = new StreamReader(request.Body, leaveOpen: true);
        var raw = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (new ScoreCampaignRequest(), null);
        }

        if (!request.HasJsonContentType())
        {
            return (null, Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["body"] = ["Request body must use application/json."]
            }));
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<ScoreCampaignRequest>(raw, JsonOptions);
            return (parsed ?? new ScoreCampaignRequest(), null);
        }
        catch (JsonException)
        {
            return (null, Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["body"] = ["Request body must be valid JSON."]
            }));
        }
    }

    private static decimal? SelectKpi(DailyMetric metric, string metricName)
    {
        var kpis = KpiCalculator.Calculate(
            metric.Spend,
            metric.Revenue,
            metric.Impressions,
            metric.Clicks,
            metric.Conversions);

        return metricName switch
        {
            "cpc" => kpis.Cpc,
            "cpa" => kpis.Cpa,
            "ctr" => kpis.Ctr,
            "roas" => kpis.Roas,
            _ => throw new ArgumentOutOfRangeException(nameof(metricName), metricName, "Unsupported metric name.")
        };
    }
}
