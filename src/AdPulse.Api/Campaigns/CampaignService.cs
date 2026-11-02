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

        if (query.Cursor is not null)
        {
            if (!CampaignCursorCodec.TryDecode(query.Cursor, out var cursor)
                || !string.Equals(cursor.FilterHash, filterHash, StringComparison.Ordinal))
            {
                throw new CampaignValidationException(new Dictionary<string, string[]>
                {
                    [nameof(query.Cursor)] = ["Cursor is invalid."]
                });
            }

            campaigns = campaigns.Where(c => c.CreatedAt < cursor.CreatedAt
                || (c.CreatedAt == cursor.CreatedAt && c.Id.CompareTo(cursor.Id) < 0));
        }

        var rows = await campaigns
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > limit;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var nextCursor = hasMore && rows.Count > 0
            ? CampaignCursorCodec.Encode(new CampaignCursor(rows[^1].CreatedAt, rows[^1].Id, filterHash))
            : null;

        return new CampaignPage(rows.Select(CampaignDto.FromEntity).ToArray(), nextCursor, hasMore, limit);
    }

    public async Task<CampaignDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var campaign = await _db.Campaigns.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        return campaign is null ? null : CampaignDto.FromEntity(campaign);
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
}
