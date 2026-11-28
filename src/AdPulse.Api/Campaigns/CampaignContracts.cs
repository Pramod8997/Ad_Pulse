using AdPulse.Domain.Analytics;
using AdPulse.Infrastructure.Data;

namespace AdPulse.Api.Campaigns;

public sealed record CampaignWriteRequest
{
    public string? Name { get; init; }
    public string? Status { get; init; }
    public string? Currency { get; init; }
    public decimal? Budget { get; init; }
    public DateOnly? StartsOn { get; init; }
    public DateOnly? EndsOn { get; init; }
}

public sealed record CampaignListQuery
{
    public string? Search { get; init; }
    public string? Status { get; init; }
    public DateOnly? StartsOnFrom { get; init; }
    public DateOnly? StartsOnTo { get; init; }
    public DateOnly? EndsOnFrom { get; init; }
    public DateOnly? EndsOnTo { get; init; }
    public string? Sort { get; init; } = "createdAt";
    public string? Direction { get; init; } = "desc";
    public int? Limit { get; init; }
    public string? Cursor { get; init; }
}

public sealed record CampaignDto(
    Guid Id,
    string Name,
    string Status,
    string Currency,
    decimal Budget,
    DateOnly StartsOn,
    DateOnly EndsOn,
    DateTimeOffset CreatedAt,
    decimal? Spend = null,
    decimal? Cpc = null,
    decimal? Cpa = null,
    decimal? Roas = null,
    DateOnly? LastMetricDate = null)
{
    public static CampaignDto FromEntity(Campaign campaign) => new(
        campaign.Id,
        campaign.Name,
        campaign.Status,
        campaign.Currency,
        campaign.Budget,
        campaign.StartsOn,
        campaign.EndsOn,
        campaign.CreatedAt);

    public static CampaignDto FromEntity(Campaign campaign, IReadOnlyCollection<DailyMetric> metrics)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(metrics);

        if (metrics.Count == 0)
        {
            return FromEntity(campaign);
        }

        var spend = metrics.Sum(metric => metric.Spend);
        var revenue = metrics.Sum(metric => metric.Revenue);
        var impressions = metrics.Sum(metric => metric.Impressions);
        var clicks = metrics.Sum(metric => metric.Clicks);
        var conversions = metrics.Sum(metric => metric.Conversions);
        var kpis = KpiCalculator.Calculate(spend, revenue, impressions, clicks, conversions);
        var lastMetricDate = metrics.Max(metric => metric.MetricDate);

        return new CampaignDto(
            campaign.Id,
            campaign.Name,
            campaign.Status,
            campaign.Currency,
            campaign.Budget,
            campaign.StartsOn,
            campaign.EndsOn,
            campaign.CreatedAt,
            spend,
            kpis.Cpc,
            kpis.Cpa,
            kpis.Roas,
            lastMetricDate);
    }
}

public sealed record CampaignPage(IReadOnlyList<CampaignDto> Items, string? NextCursor, bool HasMore, int Limit);
