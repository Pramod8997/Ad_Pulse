using AdPulse.Domain.Tenancy;
using AdPulse.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AdPulse.Api.Campaigns;

public sealed class CampaignService
{
    private readonly AdPulseDbContext _db;
    private readonly TenantContext _tenant;
    private readonly IAuditEventWriter _audit;

    public CampaignService(AdPulseDbContext db, TenantContext tenant, IAuditEventWriter audit)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _tenant = tenant ?? throw new ArgumentNullException(nameof(tenant));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));

        if (_db.Scope.IsSystemScope
            || string.IsNullOrWhiteSpace(_tenant.TenantId)
            || string.IsNullOrWhiteSpace(_tenant.SubjectId)
            || !string.Equals(_db.Scope.TenantId, _tenant.TenantId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The campaign context must be created for the authenticated tenant.");
        }
    }

    public async Task<CampaignPage> ListAsync(CampaignListQuery query, CancellationToken cancellationToken = default)
    {
        ThrowIfInvalid(CampaignValidation.Validate(query));
        var limit = query.Limit ?? 25;
        var sort = CampaignValidation.NormalizedSort(query.Sort);
        var direction = CampaignValidation.NormalizedDirection(query.Direction);
        var isAscending = direction == "asc";
        var filterHash = CampaignCursorCodec.FilterHash(query);
        var campaigns = _db.Campaigns.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = EscapeLike(query.Search.Trim());
            campaigns = campaigns.Where(c => EF.Functions.Like(c.Name, $"%{search}%", "\\"));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim().ToLowerInvariant();
            campaigns = campaigns.Where(c => c.Status == status);
        }

        if (query.StartsOnFrom is not null)
        {
            campaigns = campaigns.Where(c => c.StartsOn >= query.StartsOnFrom.Value);
        }

        if (query.StartsOnTo is not null)
        {
            campaigns = campaigns.Where(c => c.StartsOn <= query.StartsOnTo.Value);
        }

        if (query.EndsOnFrom is not null)
        {
            campaigns = campaigns.Where(c => c.EndsOn >= query.EndsOnFrom.Value);
        }

        if (query.EndsOnTo is not null)
        {
            campaigns = campaigns.Where(c => c.EndsOn <= query.EndsOnTo.Value);
        }

        CampaignCursor? decodedCursor = null;
        if (query.Cursor is not null)
        {
            if (!CampaignCursorCodec.TryDecode(query.Cursor, out var cursor)
                || !string.Equals(cursor.FilterHash, filterHash, StringComparison.Ordinal)
                || !string.Equals(cursor.Sort, sort, StringComparison.Ordinal)
                || !string.Equals(cursor.Direction, direction, StringComparison.Ordinal))
            {
                throw new CampaignValidationException(new Dictionary<string, string[]>
                {
                    [nameof(query.Cursor)] = ["Cursor is invalid."]
                });
            }

            decodedCursor = cursor;
        }

        List<Campaign> rows;
        if (_db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            // SQLite cannot translate DateTimeOffset or decimal ordering/comparisons; production SQL Server executes these server-side.
            var candidates = await campaigns.ToListAsync(cancellationToken);
            rows = ApplySqliteOrderingAndCursor(candidates, sort, isAscending, decodedCursor, limit);
        }
        else
        {
            campaigns = ApplySqlServerCursorFilter(campaigns, sort, isAscending, decodedCursor);
            campaigns = ApplySqlServerOrdering(campaigns, sort, isAscending);
            rows = await campaigns.Take(limit + 1).ToListAsync(cancellationToken);
        }

        var hasMore = rows.Count > limit;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var nextCursor = hasMore && rows.Count > 0
            ? EncodeNextCursor(rows[^1], filterHash, sort, direction)
            : null;

        var metricsByCampaign = await LoadMetricsByCampaignAsync(rows.Select(r => r.Id).ToArray(), cancellationToken);
        var items = rows
            .Select(campaign => metricsByCampaign.TryGetValue(campaign.Id, out var metrics)
                ? CampaignDto.FromEntity(campaign, metrics)
                : CampaignDto.FromEntity(campaign))
            .ToArray();

        return new CampaignPage(items, nextCursor, hasMore, limit);
    }

    public async Task<CampaignDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var campaign = await _db.Campaigns.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (campaign is null)
        {
            return null;
        }

        var metrics = await _db.DailyMetrics
            .AsNoTracking()
            .Where(m => m.CampaignId == campaign.Id)
            .ToArrayAsync(cancellationToken);

        return CampaignDto.FromEntity(campaign, metrics);
    }

    public async Task<CampaignDto> CreateAsync(CampaignWriteRequest request, CancellationToken cancellationToken = default)
    {
        CampaignValidation.ValidateOrThrow(request);
        var campaign = new Campaign
        {
            TenantId = _tenant.TenantId,
            Id = Guid.NewGuid(),
            Name = CampaignValidation.NormalizedName(request.Name!),
            Status = CampaignValidation.NormalizedStatus(request.Status!),
            Currency = request.Currency!,
            Budget = request.Budget!.Value,
            StartsOn = request.StartsOn!.Value,
            EndsOn = request.EndsOn!.Value,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Campaigns.Add(campaign);
        _audit.Add(_db, _tenant, "campaign.created", campaign.Id);
        await _db.SaveChangesAsync(cancellationToken);
        return CampaignDto.FromEntity(campaign);
    }

    public async Task<CampaignDto?> UpdateAsync(Guid id, CampaignWriteRequest request, CancellationToken cancellationToken = default)
    {
        CampaignValidation.ValidateOrThrow(request);
        var campaign = await _db.Campaigns.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (campaign is null)
        {
            return null;
        }

        campaign.Name = CampaignValidation.NormalizedName(request.Name!);
        campaign.Status = CampaignValidation.NormalizedStatus(request.Status!);
        campaign.Currency = request.Currency!;
        campaign.Budget = request.Budget!.Value;
        campaign.StartsOn = request.StartsOn!.Value;
        campaign.EndsOn = request.EndsOn!.Value;
        _audit.Add(_db, _tenant, "campaign.updated", campaign.Id);
        await _db.SaveChangesAsync(cancellationToken);
        return CampaignDto.FromEntity(campaign);
    }

    private static void ThrowIfInvalid(IReadOnlyDictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
        {
            throw new CampaignValidationException(errors);
        }
    }

    private static string EscapeLike(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    private async Task<Dictionary<Guid, DailyMetric[]>> LoadMetricsByCampaignAsync(
        Guid[] campaignIds,
        CancellationToken cancellationToken)
    {
        if (campaignIds.Length == 0)
        {
            return [];
        }

        var metrics = await _db.DailyMetrics
            .AsNoTracking()
            .Where(m => campaignIds.Contains(m.CampaignId))
            .ToListAsync(cancellationToken);

        return metrics
            .GroupBy(m => m.CampaignId)
            .ToDictionary(group => group.Key, group => group.ToArray());
    }

    private static List<Campaign> ApplySqliteOrderingAndCursor(
        List<Campaign> candidates,
        string sort,
        bool isAscending,
        CampaignCursor? cursor,
        int limit)
    {
        IEnumerable<Campaign> filtered = cursor is { } c
            ? candidates.Where(x => CompareCampaignToCursor(x, sort, c) is var cmp && (isAscending ? cmp > 0 : cmp < 0))
            : candidates;

        IEnumerable<Campaign> ordered = (sort, isAscending) switch
        {
            ("name", true) => filtered.OrderBy(x => x.Name, StringComparer.Ordinal).ThenBy(x => x.Id),
            ("name", false) => filtered.OrderByDescending(x => x.Name, StringComparer.Ordinal).ThenByDescending(x => x.Id),
            ("status", true) => filtered.OrderBy(x => x.Status, StringComparer.Ordinal).ThenBy(x => x.Id),
            ("status", false) => filtered.OrderByDescending(x => x.Status, StringComparer.Ordinal).ThenByDescending(x => x.Id),
            ("budget", true) => filtered.OrderBy(x => x.Budget).ThenBy(x => x.Id),
            ("budget", false) => filtered.OrderByDescending(x => x.Budget).ThenByDescending(x => x.Id),
            (_, true) => filtered.OrderBy(x => x.CreatedAt.ToUniversalTime()).ThenBy(x => x.Id),
            (_, false) => filtered.OrderByDescending(x => x.CreatedAt.ToUniversalTime()).ThenByDescending(x => x.Id)
        };

        return ordered.Take(limit + 1).ToList();
    }

    private static int CompareCampaignToCursor(Campaign campaign, string sort, CampaignCursor cursor)
    {
        var primary = sort switch
        {
            "name" => string.Compare(campaign.Name, cursor.TextKey, StringComparison.Ordinal),
            "status" => string.Compare(campaign.Status, cursor.TextKey, StringComparison.Ordinal),
            "budget" => campaign.Budget.CompareTo(cursor.BudgetKey!.Value),
            _ => campaign.CreatedAt.ToUniversalTime().CompareTo(cursor.CreatedAt.ToUniversalTime())
        };
        return primary != 0 ? primary : campaign.Id.CompareTo(cursor.Id);
    }

    private static IQueryable<Campaign> ApplySqlServerCursorFilter(
        IQueryable<Campaign> campaigns,
        string sort,
        bool isAscending,
        CampaignCursor? cursor)
    {
        if (cursor is not { } c)
        {
            return campaigns;
        }

        return (sort, isAscending) switch
        {
            ("name", true) => campaigns.Where(x => x.Name.CompareTo(c.TextKey!) > 0
                || (x.Name == c.TextKey! && x.Id.CompareTo(c.Id) > 0)),
            ("name", false) => campaigns.Where(x => x.Name.CompareTo(c.TextKey!) < 0
                || (x.Name == c.TextKey! && x.Id.CompareTo(c.Id) < 0)),
            ("status", true) => campaigns.Where(x => x.Status.CompareTo(c.TextKey!) > 0
                || (x.Status == c.TextKey! && x.Id.CompareTo(c.Id) > 0)),
            ("status", false) => campaigns.Where(x => x.Status.CompareTo(c.TextKey!) < 0
                || (x.Status == c.TextKey! && x.Id.CompareTo(c.Id) < 0)),
            ("budget", true) => campaigns.Where(x => x.Budget > c.BudgetKey!.Value
                || (x.Budget == c.BudgetKey!.Value && x.Id.CompareTo(c.Id) > 0)),
            ("budget", false) => campaigns.Where(x => x.Budget < c.BudgetKey!.Value
                || (x.Budget == c.BudgetKey!.Value && x.Id.CompareTo(c.Id) < 0)),
            (_, true) => campaigns.Where(x => x.CreatedAt > c.CreatedAt
                || (x.CreatedAt == c.CreatedAt && x.Id.CompareTo(c.Id) > 0)),
            (_, false) => campaigns.Where(x => x.CreatedAt < c.CreatedAt
                || (x.CreatedAt == c.CreatedAt && x.Id.CompareTo(c.Id) < 0))
        };
    }

    private static IQueryable<Campaign> ApplySqlServerOrdering(
        IQueryable<Campaign> campaigns,
        string sort,
        bool isAscending) => (sort, isAscending) switch
        {
            ("name", true) => campaigns.OrderBy(x => x.Name).ThenBy(x => x.Id),
            ("name", false) => campaigns.OrderByDescending(x => x.Name).ThenByDescending(x => x.Id),
            ("status", true) => campaigns.OrderBy(x => x.Status).ThenBy(x => x.Id),
            ("status", false) => campaigns.OrderByDescending(x => x.Status).ThenByDescending(x => x.Id),
            ("budget", true) => campaigns.OrderBy(x => x.Budget).ThenBy(x => x.Id),
            ("budget", false) => campaigns.OrderByDescending(x => x.Budget).ThenByDescending(x => x.Id),
            (_, true) => campaigns.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
            (_, false) => campaigns.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
        };

    private static string EncodeNextCursor(Campaign last, string filterHash, string sort, string direction) =>
        CampaignCursorCodec.Encode(new CampaignCursor(
            last.CreatedAt.ToUniversalTime(),
            last.Id,
            filterHash,
            sort,
            direction,
            sort switch { "name" => last.Name, "status" => last.Status, _ => null },
            sort == "budget" ? last.Budget : null));
}
