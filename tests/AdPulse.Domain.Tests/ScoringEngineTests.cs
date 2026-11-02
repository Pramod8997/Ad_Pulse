using AdPulse.Domain.Analytics;
using Xunit;

namespace AdPulse.Domain.Tests;

public sealed class ScoringEngineTests
{
    [Fact]
    public void Produces_deterministic_critical_result_for_large_degradation()
    {
        var request = new ScoringRequest(
            "tenant-a",
            "campaign-1",
            "cpc",
            2.5m,
            1m,
            30,
            7,
            MetricDirection.LowerIsBetter,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 7));

        var first = ScoringEngine.Score(request);
        var second = ScoringEngine.Score(request);

        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.Severity, second.Severity);
        Assert.Equal(first.Confidence, second.Confidence);
        Assert.Equal(first.DataSufficiency, second.DataSufficiency);
        Assert.Equal(first.ReasonCodes, second.ReasonCodes);
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(100m, first.Score);
        Assert.Equal(ScoringSeverity.Critical, first.Severity);
        Assert.Equal(1m, first.Confidence);
        Assert.Equal("sufficient", first.DataSufficiency);
        Assert.Equal(["degraded_vs_baseline"], first.ReasonCodes);
        Assert.Equal("d3b5d107128b24577b84db5e19fd45794396c2d94de804997a44bbe06244d97e", first.Fingerprint);
    }

    [Fact]
    public void Does_not_alert_before_minimum_data_threshold()
    {
        var result = ScoringEngine.Score(new ScoringRequest(
            "tenant-a", "campaign-1", "roas", 0.2m, 1m, 3, 7,
            MetricDirection.HigherIsBetter, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 7)));

        Assert.Equal(ScoringSeverity.None, result.Severity);
        Assert.Equal(0m, result.Score);
        Assert.Equal(0.2143m, result.Confidence);
        Assert.Equal("insufficient", result.DataSufficiency);
        Assert.Equal(["insufficient_data"], result.ReasonCodes);
    }

    [Fact]
    public void Treats_zero_baseline_as_unavailable_instead_of_dividing()
    {
        var result = ScoringEngine.Score(new ScoringRequest(
            "tenant-a", "campaign-1", "cpa", 2m, 0m, 30, 7,
            MetricDirection.LowerIsBetter, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 7)));

        Assert.Equal(ScoringSeverity.None, result.Severity);
        Assert.Equal("insufficient", result.DataSufficiency);
        Assert.Equal(0m, result.Confidence);
        Assert.Equal(["baseline_unavailable"], result.ReasonCodes);
    }

    [Fact]
    public void Rejects_malformed_fingerprint_parts()
    {
        var request = new ScoringRequest(
            "tenant|a", "campaign-1", "cpc", 1m, 1m, 7, 7,
            MetricDirection.LowerIsBetter, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 7));

        Assert.Throws<ArgumentException>(() => ScoringEngine.Score(request));
    }

    [Fact]
    public void Rounds_degradation_to_four_decimal_places()
    {
        var result = ScoringEngine.Score(new ScoringRequest(
            "tenant-a", "campaign-1", "roas", 0.8765495m, 1m, 14, 7,
            MetricDirection.HigherIsBetter, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 7)));

        Assert.Equal(12.3451m, result.Score);
        Assert.Equal(ScoringSeverity.Low, result.Severity);
    }

    [Fact]
    public void Caps_extreme_decimal_degradation_without_overflow()
    {
        var result = ScoringEngine.Score(new ScoringRequest(
            "tenant-a", "campaign-1", "cpc", decimal.MaxValue,
            0.0000000000000000000000000001m, 14, 7,
            MetricDirection.LowerIsBetter, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 7)));

        Assert.Equal(100m, result.Score);
        Assert.Equal(ScoringSeverity.Critical, result.Severity);
    }

    [Fact]
    public void Rejects_invalid_direction_and_contract_bounds()
    {
        var request = new ScoringRequest(
            "tenant-a", "campaign-1", "cpc", 1m, 1m, 7, 7,
            MetricDirection.LowerIsBetter, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 7));

        Assert.Throws<ArgumentOutOfRangeException>(() => ScoringEngine.Score(request with { Direction = (MetricDirection)99 }));
        Assert.Throws<ArgumentException>(() => ScoringEngine.Score(request with { MetricName = new string('m', 65) }));
        Assert.Throws<ArgumentException>(() => ScoringEngine.Score(request with { ScoringVersion = new string('v', 33) }));
        Assert.Throws<ArgumentOutOfRangeException>(() => ScoringEngine.Score(request with { DataPoints = 1_000_001 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => ScoringEngine.Score(request with { MinimumDataPoints = 1_000_001 }));
        Assert.Throws<ArgumentNullException>(() => ScoringEngine.Score(null!));
    }
}
