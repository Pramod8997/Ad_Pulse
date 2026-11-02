using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AdPulse.Domain.Analytics;

public static class ScoringEngine
{
    public static ScoringResult Score(ScoringRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);

        var confidence = Round(Math.Min(1m, (decimal)request.DataPoints / (request.MinimumDataPoints * 2m)));

        if (request.DataPoints < request.MinimumDataPoints)
        {
            return BuildResult(0m, ScoringSeverity.None, confidence, "insufficient", "insufficient_data", request);
        }

        if (request.Baseline == 0m)
        {
            return BuildResult(0m, ScoringSeverity.None, 0m, "insufficient", "baseline_unavailable", request);
        }

        var degraded = request.Direction == MetricDirection.LowerIsBetter
            ? request.Observed > request.Baseline
            : request.Observed < request.Baseline;

        if (!degraded)
        {
            return BuildResult(0m, ScoringSeverity.None, confidence, "sufficient", "within_baseline", request);
        }

        var deviation = Math.Abs(request.Observed - request.Baseline);
        var score = deviation >= request.Baseline
            ? 100m
            : Round(deviation / request.Baseline * 100m);
        return BuildResult(score, SeverityFor(score), confidence, "sufficient", "degraded_vs_baseline", request);
    }

    private static ScoringResult BuildResult(
        decimal score,
        ScoringSeverity severity,
        decimal confidence,
        string dataSufficiency,
        string reasonCode,
        ScoringRequest request)
    {
        var fingerprintInput = string.Join(
            '|',
            request.TenantId,
            request.CampaignId,
            request.MetricName,
            request.EvidenceWindowStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            request.EvidenceWindowEnd.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            request.ScoringVersion,
            reasonCode);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintInput))).ToLowerInvariant();

        return new ScoringResult(score, severity, confidence, dataSufficiency, [reasonCode], fingerprint);
    }

    private static ScoringSeverity SeverityFor(decimal score) => score switch
    {
        >= 75m => ScoringSeverity.Critical,
        >= 50m => ScoringSeverity.High,
        >= 25m => ScoringSeverity.Medium,
        >= 10m => ScoringSeverity.Low,
        _ => ScoringSeverity.None
    };

    private static decimal Round(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    private static void Validate(ScoringRequest request)
    {
        RequireToken(request.TenantId, nameof(request.TenantId), 128);
        RequireToken(request.CampaignId, nameof(request.CampaignId), 128);
        RequireToken(request.MetricName, nameof(request.MetricName), 64);
        RequireToken(request.ScoringVersion, nameof(request.ScoringVersion), 32);
        EnsureNoPipe(request.TenantId, nameof(request.TenantId));
        EnsureNoPipe(request.CampaignId, nameof(request.CampaignId));
        EnsureNoPipe(request.MetricName, nameof(request.MetricName));
        EnsureNoPipe(request.ScoringVersion, nameof(request.ScoringVersion));

        if (!Enum.IsDefined(request.Direction))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Metric direction is invalid.");
        }

        if (request.Observed < 0m || request.Baseline < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Observed and baseline values cannot be negative.");
        }

        if (request.DataPoints is < 0 or > 1_000_000)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Data points must be between 0 and 1,000,000.");
        }

        if (request.MinimumDataPoints is < 1 or > 1_000_000)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Minimum data points must be between 1 and 1,000,000.");
        }

        if (request.EvidenceWindowEnd < request.EvidenceWindowStart)
        {
            throw new ArgumentException("The evidence window end must not precede its start.", nameof(request));
        }
    }

    private static void RequireToken(string value, string name, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength)
        {
            throw new ArgumentException($"The value is required and must be at most {maximumLength} characters.", name);
        }
    }

    private static void EnsureNoPipe(string value, string name)
    {
        if (value.Contains('|', StringComparison.Ordinal))
        {
            throw new ArgumentException("The value cannot contain the fingerprint separator.", name);
        }
    }
}
