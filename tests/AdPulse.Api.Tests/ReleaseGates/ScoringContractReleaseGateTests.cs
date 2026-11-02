using System.Globalization;
using System.Text.Json;
using AdPulse.Domain.Analytics;

namespace AdPulse.Api.Tests.ReleaseGates;

public sealed class ScoringContractReleaseGateTests
{
    [Fact]
    public void Schema_keeps_canonical_decimal_strings_and_closed_objects()
    {
        using var schema = JsonDocument.Parse(ReleaseGateRepository.ReadText("contracts", "scoring-v1.schema.json"));
        var root = schema.RootElement;

        Assert.Equal("object", root.GetProperty("type").GetString());
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            "string",
            root.GetProperty("properties").GetProperty("observed").GetProperty("type").GetString());
        Assert.Contains(
            "canonical JSON strings",
            root.GetProperty("$comment").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Every_shared_fixture_matches_the_dotnet_scoring_engine()
    {
        using var document = JsonDocument.Parse(ReleaseGateRepository.ReadText("contracts", "scoring-v1-fixtures.json"));

        foreach (var fixture in document.RootElement.EnumerateArray())
        {
            var request = fixture.GetProperty("request");
            var expected = fixture.GetProperty("expected");
            var result = ScoringEngine.Score(new ScoringRequest(
                request.GetProperty("tenant_id").GetString()!,
                request.GetProperty("campaign_id").GetString()!,
                request.GetProperty("metric_name").GetString()!,
                decimal.Parse(request.GetProperty("observed").GetString()!, CultureInfo.InvariantCulture),
                decimal.Parse(request.GetProperty("baseline").GetString()!, CultureInfo.InvariantCulture),
                request.GetProperty("data_points").GetInt32(),
                request.GetProperty("minimum_data_points").GetInt32(),
                request.GetProperty("direction").GetString() switch
                {
                    "lower_is_better" => MetricDirection.LowerIsBetter,
                    "higher_is_better" => MetricDirection.HigherIsBetter,
                    var value => throw new InvalidDataException($"Unsupported fixture direction: {value}"),
                },
                DateOnly.Parse(request.GetProperty("evidence_window_start").GetString()!, CultureInfo.InvariantCulture),
                DateOnly.Parse(request.GetProperty("evidence_window_end").GetString()!, CultureInfo.InvariantCulture),
                request.TryGetProperty("scoring_version", out var version)
                    ? version.GetString()!
                    : "scoring-v1"));

            Assert.Equal(decimal.Parse(expected.GetProperty("score").GetString()!, CultureInfo.InvariantCulture), result.Score);
            Assert.Equal(expected.GetProperty("severity").GetString(), result.Severity.ToString().ToLowerInvariant());
            Assert.Equal(decimal.Parse(expected.GetProperty("confidence").GetString()!, CultureInfo.InvariantCulture), result.Confidence);
            Assert.Equal(expected.GetProperty("data_sufficiency").GetString(), result.DataSufficiency);
            Assert.Equal(expected.GetProperty("reason_codes").EnumerateArray().Select(x => x.GetString()!).ToArray(), result.ReasonCodes);
            Assert.Equal(expected.GetProperty("fingerprint").GetString(), result.Fingerprint);
        }
    }

    [Fact]
    public void Scoring_contract_rejects_zero_baseline_as_an_alert_source()
    {
        var result = ScoringEngine.Score(new ScoringRequest(
            "tenant-a",
            "campaign-1",
            "cpa",
            2m,
            0m,
            30,
            7,
            MetricDirection.LowerIsBetter,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 7)));

        Assert.Equal(ScoringSeverity.None, result.Severity);
        Assert.Equal("baseline_unavailable", Assert.Single(result.ReasonCodes));
        Assert.Equal(0m, result.Score);
    }
}
