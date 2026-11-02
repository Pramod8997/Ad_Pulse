using AdPulse.Domain.Analytics;

namespace AdPulse.Api.Tests.ReleaseGates;

public sealed class FinancialSemanticsReleaseGateTests
{
    [Fact]
    public void Zero_denominators_are_explicit_nulls_and_omitted_budget_is_null()
    {
        var metrics = KpiCalculator.Calculate(
            spend: 0m,
            revenue: 99.9999m,
            impressions: 0,
            clicks: 0,
            conversions: 0);

        Assert.Null(metrics.Ctr);
        Assert.Null(metrics.Cpc);
        Assert.Null(metrics.Cpa);
        Assert.Null(metrics.Roas);
        Assert.Null(metrics.BudgetPacing);
    }

    [Fact]
    public void Ratios_keep_decimal_arithmetic_without_binary_float_coercion()
    {
        var metrics = KpiCalculator.Calculate(
            spend: 1m,
            revenue: 3m,
            impressions: 3,
            clicks: 2,
            conversions: 2,
            plannedToDateBudget: 4m);

        Assert.Equal(2m / 3m, metrics.Ctr);
        Assert.Equal(0.5m, metrics.Cpc);
        Assert.Equal(0.5m, metrics.Cpa);
        Assert.Equal(3m, metrics.Roas);
        Assert.Equal(0.25m, metrics.BudgetPacing);
        Assert.DoesNotContain("double", ReleaseGateRepository.ReadText("src", "AdPulse.Domain", "Analytics", "KpiCalculator.cs"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Negative_money_and_counts_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            KpiCalculator.Calculate(-1m, 0m, 1, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            KpiCalculator.Calculate(0m, -1m, 1, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            KpiCalculator.Calculate(0m, 0m, -1, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            KpiCalculator.Calculate(0m, 0m, 0, 0, 0, -1m));
    }

    [Fact]
    public void Persisted_money_fields_use_decimal_nineteen_four()
    {
        var model = ReleaseGateRepository.ReadText("src", "AdPulse.Infrastructure", "Data", "AdPulseDbContext.cs");
        var migration = ReleaseGateRepository.ReadText("db", "migrations", "001_initial.sql");

        Assert.Contains("HasPrecision(19, 4)", model, StringComparison.Ordinal);
        Assert.Contains("Budget decimal(19, 4)", migration, StringComparison.Ordinal);
        Assert.Contains("Spend decimal(19, 4)", migration, StringComparison.Ordinal);
        Assert.Contains("Revenue decimal(19, 4)", migration, StringComparison.Ordinal);
    }
}
