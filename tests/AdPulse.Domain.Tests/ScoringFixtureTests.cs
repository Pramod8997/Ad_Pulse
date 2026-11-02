using System.Globalization;
using System.Text.Json;
using AdPulse.Domain.Analytics;
using Xunit;

namespace AdPulse.Domain.Tests;

public sealed class ScoringFixtureTests
{
    [Fact]
    public void Shared_json_fixtures_match_the_dotnet_engine()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "scoring-v1-fixtures.json")));

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
                request.GetProperty("scoring_version").GetString()!));

            Assert.Equal(decimal.Parse(expected.GetProperty("score").GetString()!, CultureInfo.InvariantCulture), result.Score);
            Assert.Equal(expected.GetProperty("severity").GetString(), result.Severity.ToString().ToLowerInvariant());
            Assert.Equal(decimal.Parse(expected.GetProperty("confidence").GetString()!, CultureInfo.InvariantCulture), result.Confidence);
            Assert.Equal(expected.GetProperty("data_sufficiency").GetString(), result.DataSufficiency);
            Assert.Equal(expected.GetProperty("reason_codes").EnumerateArray().Select(x => x.GetString()!).ToArray(), result.ReasonCodes);
            Assert.Equal(expected.GetProperty("fingerprint").GetString(), result.Fingerprint);
        }
    }
}
