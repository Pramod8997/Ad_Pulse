using AdPulse.Domain.Analytics;
using Xunit;

namespace AdPulse.Domain.Tests;

public sealed class KpiCalculatorTests
{
    [Fact]
    public void Calculates_decimal_metrics_without_binary_rounding()
    {
        var result = KpiCalculator.Calculate(10.00m, 25.00m, 1_000, 100, 4, 20.00m);

        Assert.Equal(0.1m, result.Ctr);
        Assert.Equal(0.10m, result.Cpc);
        Assert.Equal(2.50m, result.Cpa);
        Assert.Equal(2.5m, result.Roas);
        Assert.Equal(0.5m, result.BudgetPacing);
    }

    [Fact]
    public void Returns_null_for_every_zero_denominator()
    {
        var result = KpiCalculator.Calculate(0m, 10m, 0, 0, 0, 0m);

        Assert.Null(result.Ctr);
        Assert.Null(result.Cpc);
        Assert.Null(result.Cpa);
        Assert.Null(result.Roas);
        Assert.Null(result.BudgetPacing);
    }

    [Fact]
    public void Rejects_negative_inputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => KpiCalculator.Calculate(-0.01m, 0m, 0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => KpiCalculator.Calculate(0m, -0.01m, 0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => KpiCalculator.Calculate(0m, 0m, -1, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => KpiCalculator.Calculate(0m, 0m, 0, -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => KpiCalculator.Calculate(0m, 0m, 0, 0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => KpiCalculator.Calculate(0m, 0m, 0, 0, 0, -0.01m));
    }

    [Fact]
    public void Leaves_budget_pacing_null_when_budget_is_not_provided()
    {
        var result = KpiCalculator.Calculate(10m, 25m, 1_000, 100, 4);

        Assert.Null(result.BudgetPacing);
    }

    [Fact]
    public void Accepts_maximum_nonnegative_inputs()
    {
        var result = KpiCalculator.Calculate(
            decimal.MaxValue,
            decimal.MaxValue,
            long.MaxValue,
            long.MaxValue,
            long.MaxValue,
            decimal.MaxValue);

        Assert.Equal(1m, result.Ctr);
        Assert.Equal(decimal.MaxValue / long.MaxValue, result.Cpc);
        Assert.Equal(decimal.MaxValue / long.MaxValue, result.Cpa);
        Assert.Equal(1m, result.Roas);
        Assert.Equal(1m, result.BudgetPacing);
    }
}
