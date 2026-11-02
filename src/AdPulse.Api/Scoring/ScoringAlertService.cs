using AdPulse.Api.Alerts;
using AdPulse.Domain.Analytics;
using AdPulse.Domain.Tenancy;

namespace AdPulse.Api.Scoring;

public sealed class ScoringAlertService(IPythonScoringClient scorer, AlertService alerts)
{
    public async Task<AlertDetails?> ScoreAndPersistAsync(
        TenantContext tenant,
        ScoringRequest request,
        Guid? campaignId,
        DateTimeOffset detectedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(tenant.TenantId)
            || !string.Equals(tenant.TenantId, request.TenantId, StringComparison.Ordinal))
        {
            throw new TenantAccessDeniedException("The scoring request is outside the current tenant.");
        }

        var requestCampaignId = Guid.Empty;
        if (campaignId.HasValue && !Guid.TryParse(request.CampaignId, out requestCampaignId))
        {
            throw new ArgumentException("The campaign identifier is invalid.", nameof(request));
        }
        else if (campaignId.HasValue && requestCampaignId != campaignId.Value)
        {
            throw new TenantAccessDeniedException("The scoring request campaign does not match the current resource.");
        }

        var result = await scorer.ScoreAsync(request, cancellationToken).ConfigureAwait(false);
        var persisted = await alerts.PersistScoredAsync(
            tenant,
            new ScoredAlert(
                campaignId,
                request.MetricName,
                result.Score,
                result.Severity.ToString().ToLowerInvariant(),
                request.Observed,
                request.Baseline,
                result.Confidence,
                result.DataSufficiency,
                result.ReasonCodes,
                request.ScoringVersion,
                request.EvidenceWindowStart,
                request.EvidenceWindowEnd,
                result.Fingerprint,
                detectedAt),
            cancellationToken).ConfigureAwait(false);

        return persisted;
    }
}
