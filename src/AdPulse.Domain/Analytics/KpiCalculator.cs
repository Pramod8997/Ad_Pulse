namespace AdPulse.Domain.Analytics;

public sealed record KpiMetrics(
    decimal? Ctr,
    decimal? Cpc,
    decimal? Cpa,
    decimal? Roas,
    decimal? BudgetPacing);

public static class KpiCalculator
{
    public static KpiMetrics Calculate(
        decimal spend,
        decimal revenue,
        long impressions,
        long clicks,
        long conversions,
        decimal? plannedToDateBudget = null)
    {
        EnsureNonNegative(nameof(spend), spend);
        EnsureNonNegative(nameof(revenue), revenue);
        EnsureNonNegative(nameof(impressions), impressions);
        EnsureNonNegative(nameof(clicks), clicks);
        EnsureNonNegative(nameof(conversions), conversions);

        if (plannedToDateBudget is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(plannedToDateBudget));
        }

        return new KpiMetrics(
            Ratio(clicks, impressions),
            Ratio(spend, clicks),
            Ratio(spend, conversions),
            Ratio(revenue, spend),
            plannedToDateBudget is null ? null : Ratio(spend, plannedToDateBudget.Value));
    }

    private static decimal? Ratio(decimal numerator, long denominator) =>
        denominator == 0 ? null : numerator / denominator;

    private static decimal? Ratio(decimal numerator, decimal denominator) =>
        denominator == 0 ? null : numerator / denominator;

    private static void EnsureNonNegative<T>(string name, T value) where T : IComparable<T>
    {
        if (value.CompareTo(default) < 0)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}
