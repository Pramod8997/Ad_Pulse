using AdPulse.Api.Metrics;
using AdPulse.Infrastructure.Data;
using Xunit;

namespace AdPulse.Api.Tests.Metrics;

public sealed class DailyMetricTests
{
    [Fact]
    public void Rejects_negative_values_and_invalid_count_relationships()
    {
        var errors = MetricValidation.Validate(new DailyMetricIngestRequest(
            new DateOnly(2026, 1, 1),
            -1m,
            0m,
            10,
            11,
            12));

        Assert.Contains(nameof(DailyMetricIngestRequest.Spend), errors.Keys);
        Assert.Contains(nameof(DailyMetricIngestRequest.Clicks), errors.Keys);
        Assert.Contains(nameof(DailyMetricIngestRequest.Conversions), errors.Keys);
    }

    [Fact]
    public void Rejects_money_that_cannot_be_stored_as_decimal_19_4()
    {
        var errors = MetricValidation.Validate(new DailyMetricIngestRequest(
            new DateOnly(2026, 1, 1),
            0.00001m,
            MetricValidation.MoneyMax + 0.0001m,
            0,
            0,
            0));

        Assert.Contains(nameof(DailyMetricIngestRequest.Spend), errors.Keys);
        Assert.Contains(nameof(DailyMetricIngestRequest.Revenue), errors.Keys);
    }

    [Fact]
    public void Cursor_round_trips_and_is_bound_to_the_campaign()
    {
        var cursor = new MetricCursor(new DateOnly(2026, 1, 2), Guid.NewGuid());

        Assert.True(MetricCursor.TryDecode(cursor.Encode(), out var decoded));
        Assert.Equal(cursor, decoded);
    }

    [Fact]
    public void Date_must_be_inside_the_inclusive_campaign_range()
    {
        var before = MetricValidation.ValidateDate(
            new DateOnly(2025, 12, 31),
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 31));
        var end = MetricValidation.ValidateDate(
            new DateOnly(2026, 1, 31),
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 31));

        Assert.Contains(nameof(DailyMetricIngestRequest.MetricDate), before.Keys);
        Assert.Empty(end);
    }

    [Fact]
    public void Response_keeps_zero_denominator_kpis_null()
    {
        var response = DailyMetricResponse.From(new DailyMetric
        {
            CampaignId = Guid.NewGuid(),
            MetricDate = new DateOnly(2026, 1, 1),
            Spend = 0m,
            Revenue = 10m,
            Impressions = 0,
            Clicks = 0,
            Conversions = 0
        });

        Assert.Null(response.Kpis.Ctr);
        Assert.Null(response.Kpis.Cpc);
        Assert.Null(response.Kpis.Cpa);
        Assert.Null(response.Kpis.Roas);
        Assert.Null(response.Kpis.BudgetPacing);
    }
}
