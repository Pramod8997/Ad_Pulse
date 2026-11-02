using System.Security;
using AdPulse.Api.Contracts;
using AdPulse.Domain.Tenancy;
using AdPulse.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AdPulse.Api.Metrics;

public sealed class MetricRequestValidationException(
    IReadOnlyDictionary<string, string[]> errors) : Exception("The metric request is invalid.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

public sealed class MetricNotFoundException : Exception
{
    public MetricNotFoundException() : base("The campaign was not found in the current tenant.")
    {
    }
}

public sealed class DailyMetricService(AdPulseDbContext db, TenantContext tenant)
{
    private readonly AdPulseDbContext _db = db ?? throw new ArgumentNullException(nameof(db));
    private readonly TenantContext _tenant = tenant ?? throw new ArgumentNullException(nameof(tenant));

    public async Task<DailyMetricPageResponse> ReadAsync(
        Guid campaignId,
        MetricListQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant();
        var queryErrors = MetricValidation.ValidateQuery(query);
        if (queryErrors.Count > 0)
        {
            throw new MetricRequestValidationException(queryErrors);
        }

        if (!await _db.Campaigns.AsNoTracking().AnyAsync(x => x.Id == campaignId, cancellationToken))
        {
            throw new MetricNotFoundException();
        }

        MetricCursor? metricCursor = null;
        if (query.Cursor is not null)
        {
            if (!MetricCursor.TryDecode(query.Cursor, out metricCursor)
                || metricCursor!.CampaignId != campaignId
                || metricCursor.From != query.From
                || metricCursor.To != query.To)
            {
                throw new MetricRequestValidationException(new Dictionary<string, string[]>
                {
                    ["cursor"] = ["Cursor is invalid for this campaign."]
                });
            }
        }

        var metrics = _db.DailyMetrics
            .AsNoTracking()
            .Include(x => x.Campaign)
            .Where(x => x.CampaignId == campaignId);
        if (query.From is not null)
        {
            metrics = metrics.Where(x => x.MetricDate >= query.From.Value);
        }

        if (query.To is not null)
        {
            metrics = metrics.Where(x => x.MetricDate <= query.To.Value);
        }

        if (metricCursor is not null)
        {
            metrics = metrics.Where(x => x.MetricDate < metricCursor.MetricDate);
        }

        var limit = query.Limit;
        var rows = await metrics
            .OrderByDescending(x => x.MetricDate)
            .ThenBy(x => x.CampaignId)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        var hasNext = rows.Count > limit;
        if (hasNext)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var nextCursor = hasNext && rows.Count > 0
            ? new MetricCursor(rows[^1].MetricDate, rows[^1].CampaignId, query.From, query.To).Encode()
            : null;
        return new DailyMetricPageResponse(
            rows.Select(x => DailyMetricResponse.From(x, x.Campaign)).ToArray(),
            nextCursor,
            hasNext,
            limit);
    }

    public Task<DailyMetricPageResponse> ReadAsync(
        Guid campaignId,
        DateOnly? from,
        DateOnly? to,
        int limit,
        string? cursor,
        CancellationToken cancellationToken = default) =>
        ReadAsync(campaignId, new MetricListQuery
        {
            From = from,
            To = to,
            Limit = limit,
            Cursor = cursor
        }, cancellationToken);

    public async Task<CursorPage<DailyMetricDto>> ListAsync(
        Guid campaignId,
        MetricListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await ReadAsync(campaignId, query, cancellationToken);
        return new CursorPage<DailyMetricDto>
        {
            Items = page.Items.Select(metric => metric.ToContract()).ToArray(),
            NextCursor = page.NextCursor,
            HasMore = page.HasMore,
            Limit = page.Limit
        };
    }

    public Task<DailyMetricIngestResponse> UpsertAsync(
        Guid campaignId,
        DailyMetricIngestRequest request,
        CancellationToken cancellationToken = default)
    {
        return UpsertCoreAsync(campaignId, request, cancellationToken);
    }

    public Task<DailyMetricIngestResponse> IngestAsync(
        Guid campaignId,
        DailyMetricIngestRequest request,
        CancellationToken cancellationToken = default) =>
        UpsertAsync(campaignId, request, cancellationToken);

    public Task<DailyMetricIngestResponse> UpsertAsync(
        Guid campaignId,
        DateOnly metricDate,
        UpsertDailyMetricRequest request,
        CancellationToken cancellationToken = default) =>
        UpsertAsync(campaignId, new DailyMetricIngestRequest(
            metricDate,
            request.Spend,
            request.Revenue,
            request.Impressions,
            request.Clicks,
            request.Conversions), cancellationToken);

    private async Task<DailyMetricIngestResponse> UpsertCoreAsync(
        Guid campaignId,
        DailyMetricIngestRequest request,
        CancellationToken cancellationToken)
    {
        EnsureTenant();
        var errors = MetricValidation.Validate(request);
        if (errors.Count > 0)
        {
            throw new MetricRequestValidationException(errors);
        }

        var campaign = await _db.Campaigns
            .SingleOrDefaultAsync(x => x.Id == campaignId, cancellationToken);
        if (campaign is null)
        {
            throw new MetricNotFoundException();
        }

        var dateErrors = MetricValidation.ValidateDate(request.MetricDate, campaign.StartsOn, campaign.EndsOn);
        if (dateErrors.Count > 0)
        {
            throw new MetricRequestValidationException(dateErrors);
        }

        var existing = await _db.DailyMetrics
            .SingleOrDefaultAsync(
                x => x.CampaignId == campaignId && x.MetricDate == request.MetricDate,
                cancellationToken);
        if (existing is not null)
        {
            SetPayload(existing, request);
            await _db.SaveChangesAsync(cancellationToken);
            return new DailyMetricIngestResponse(DailyMetricResponse.From(existing, campaign), false);
        }

        var metric = new DailyMetric
        {
            TenantId = _tenant.TenantId,
            CampaignId = campaignId,
            MetricDate = request.MetricDate,
            Spend = request.Spend,
            Revenue = request.Revenue,
            Impressions = request.Impressions,
            Clicks = request.Clicks,
            Conversions = request.Conversions
        };
        _db.DailyMetrics.Add(metric);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return new DailyMetricIngestResponse(DailyMetricResponse.From(metric, campaign), true);
        }
        catch (DbUpdateException)
        {
            // A concurrent retry may have won the composite-key insert. Re-read it and
            // apply this complete PUT payload instead of turning a safe retry into a 500.
            _db.Entry(metric).State = EntityState.Detached;
            var persisted = await _db.DailyMetrics
                .SingleOrDefaultAsync(
                    x => x.CampaignId == campaignId && x.MetricDate == request.MetricDate,
                    cancellationToken);
            if (persisted is null)
            {
                throw;
            }

            SetPayload(persisted, request);
            await _db.SaveChangesAsync(cancellationToken);
            return new DailyMetricIngestResponse(DailyMetricResponse.From(persisted, campaign), false);
        }
    }

    private void EnsureTenant()
    {
        if (_db.Scope.IsSystemScope || !string.Equals(_db.Scope.TenantId, _tenant.TenantId, StringComparison.Ordinal))
        {
            throw new SecurityException("The database scope does not match the authenticated tenant.");
        }
    }

    private static void SetPayload(DailyMetric metric, DailyMetricIngestRequest request)
    {
        metric.Spend = request.Spend;
        metric.Revenue = request.Revenue;
        metric.Impressions = request.Impressions;
        metric.Clicks = request.Clicks;
        metric.Conversions = request.Conversions;
    }
}
