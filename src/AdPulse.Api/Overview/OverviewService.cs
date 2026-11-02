using AdPulse.Api.Contracts;
using AdPulse.Api.Settings;
using AdPulse.Domain.Analytics;
using AdPulse.Domain.Tenancy;
using AdPulse.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AdPulse.Api.Overview;

public sealed class OverviewValidationException(string message) : ArgumentException(message);

public sealed class OverviewService(
    AdPulseDbContext db,
    TenantContext tenant,
    ITenantSettingsStore settings)
{
    public async Task<OverviewDto> GetAsync(OverviewQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.From == default && query.To == default)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            query = query with { From = today.AddDays(-29), To = today };
        }

        if (query.From == default || query.To == default || query.From > query.To)
        {
            throw new OverviewValidationException("A valid inclusive from/to date range is required.");
        }

        if (query.To.DayNumber - query.From.DayNumber > 366)
        {
            throw new OverviewValidationException("The overview date range cannot exceed 367 days.");
        }

        var tenantSettings = await settings.FindAsync(tenant.TenantId, cancellationToken);
        if (tenantSettings is null)
        {
            throw new OverviewValidationException("Reporting settings must be configured before viewing analytics.");
        }

        var requestedCurrency = NormalizeCurrency(query.Currency);
        var campaignsQuery = db.Campaigns.AsNoTracking();
        if (requestedCurrency is not null)
        {
            campaignsQuery = campaignsQuery.Where(x => x.Currency == requestedCurrency);
        }
        else
        {
            var currencies = await campaignsQuery.Select(x => x.Currency).Distinct().Take(2).ToArrayAsync(cancellationToken);
            if (currencies.Length > 1)
            {
                throw new OverviewValidationException("currency_scope_required: choose one campaign currency.");
            }
        }

        var campaigns = await campaignsQuery
            .OrderByDescending(x => x.CreatedAt)
            .Take(1000)
            .ToArrayAsync(cancellationToken);
        var campaignIds = campaigns.Select(x => x.Id).ToArray();
        var metrics = campaignIds.Length == 0
            ? []
            : await db.DailyMetrics.AsNoTracking()
                .Where(x => campaignIds.Contains(x.CampaignId)
                    && x.MetricDate >= query.From
                    && x.MetricDate <= query.To)
                .OrderBy(x => x.MetricDate)
                .ToArrayAsync(cancellationToken);

        var total = KpiCalculator.Calculate(
            metrics.Sum(x => x.Spend),
            metrics.Sum(x => x.Revenue),
            metrics.Sum(x => x.Impressions),
            metrics.Sum(x => x.Clicks),
            metrics.Sum(x => x.Conversions));

        var budgetPacing = campaigns.Select(campaign => BuildPacing(campaign, metrics.Where(x => x.CampaignId == campaign.Id), query.To)).ToArray();
        var alerts = await db.Alerts.AsNoTracking()
            .Include(x => x.Campaign)
            .Where(x => (x.CampaignId == null || campaignIds.Contains(x.CampaignId.Value))
                && x.Status != AlertStatuses.Resolved
                && x.Severity != "none")
            .OrderByDescending(x => x.DetectedAt)
            .Take(10)
            .Select(x => new AlertSummaryDto
            {
                Id = x.Id,
                CampaignId = x.CampaignId,
                CampaignName = x.Campaign == null ? null : x.Campaign.Name,
                MetricName = x.MetricName,
                Severity = x.Severity,
                Status = x.Status,
                DetectedAt = x.DetectedAt
            })
            .ToArrayAsync(cancellationToken);

        var trend = metrics
            .GroupBy(x => x.MetricDate)
            .OrderBy(x => x.Key)
            .Select(group => new OverviewTrendPointDto
            {
                Date = group.Key,
                Spend = group.Sum(x => x.Spend),
                Revenue = group.Sum(x => x.Revenue)
            })
            .ToArray();

        var spend = metrics.Sum(x => x.Spend);
        var revenue = metrics.Sum(x => x.Revenue);
        return new OverviewDto
        {
            From = query.From,
            To = query.To,
            ReportingTimeZone = tenantSettings.ReportingTimeZone,
            Currency = requestedCurrency ?? campaigns.Select(x => x.Currency).FirstOrDefault() ?? tenantSettings.DefaultCurrency ?? string.Empty,
            Spend = metrics.Length == 0 ? null : spend,
            Revenue = metrics.Length == 0 ? null : revenue,
            Kpis = new KpiMetricsDto
            {
                Ctr = total.Ctr,
                Cpc = total.Cpc,
                Cpa = total.Cpa,
                Roas = total.Roas,
                BudgetPacing = BudgetRatio(budgetPacing)
            },
            Trend = trend,
            BudgetPacing = budgetPacing,
            PriorityAlerts = alerts
        };
    }

    private static BudgetPacingDto BuildPacing(Campaign campaign, IEnumerable<DailyMetric> campaignMetrics, DateOnly through)
    {
        var rows = campaignMetrics.ToArray();
        decimal? spent = rows.Length == 0 ? null : rows.Sum(x => x.Spend);
        var planned = PlannedToDate(campaign, through);
        var pacing = spent is null || planned == 0m ? null : spent / planned;
        var status = pacing is null ? "insufficient-data" : pacing > 1.1m ? "over-budget" : pacing > 1m ? "at-risk" : "on-track";
        return new BudgetPacingDto
        {
            CampaignId = campaign.Id,
            CampaignName = campaign.Name,
            Spent = spent,
            PlannedToDate = planned,
            Pacing = pacing,
            RemainingBudget = spent is null ? null : campaign.Budget - spent,
            Status = status
        };
    }

    private static decimal PlannedToDate(Campaign campaign, DateOnly through)
    {
        if (through < campaign.StartsOn)
        {
            return 0m;
        }

        var elapsed = Math.Min(campaign.EndsOn.DayNumber, through.DayNumber) - campaign.StartsOn.DayNumber + 1;
        var days = campaign.EndsOn.DayNumber - campaign.StartsOn.DayNumber + 1;
        return days <= 0 ? 0m : campaign.Budget * elapsed / days;
    }

    private static decimal? BudgetRatio(IEnumerable<BudgetPacingDto> rows)
    {
        var values = rows.Where(x => x.Spent is not null && x.PlannedToDate is > 0).ToArray();
        return values.Length == 0 ? null : values.Sum(x => x.Spent!.Value) / values.Sum(x => x.PlannedToDate!.Value);
    }

    private static string? NormalizeCurrency(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            return null;
        }

        var normalized = currency.Trim().ToUpperInvariant();
        if (normalized.Length != 3 || normalized.Any(x => x is < 'A' or > 'Z'))
        {
            throw new OverviewValidationException("Currency must be three uppercase ASCII letters.");
        }

        return normalized;
    }
}
