using AdPulse.Infrastructure.Data;

namespace AdPulse.Api.Campaigns;

public sealed record CampaignWriteRequest
{
    public string? Name { get; init; }
    public string? Status { get; init; }
    public string? Currency { get; init; }
    public decimal? Budget { get; init; }
    public DateOnly? StartsOn { get; init; }
    public DateOnly? EndsOn { get; init; }
}

public sealed record CampaignListQuery
{
    public string? Search { get; init; }
    public string? Status { get; init; }
    public DateOnly? StartsOnFrom { get; init; }
    public DateOnly? StartsOnTo { get; init; }
    public DateOnly? EndsOnFrom { get; init; }
    public DateOnly? EndsOnTo { get; init; }
    public int? Limit { get; init; }
    public string? Cursor { get; init; }
}

public sealed record CampaignDto(
    Guid Id,
    string Name,
    string Status,
    string Currency,
    decimal Budget,
    DateOnly StartsOn,
    DateOnly EndsOn,
    DateTimeOffset CreatedAt)
{
    public static CampaignDto FromEntity(Campaign campaign) => new(
        campaign.Id,
        campaign.Name,
        campaign.Status,
        campaign.Currency,
        campaign.Budget,
        campaign.StartsOn,
        campaign.EndsOn,
        campaign.CreatedAt);
}

public sealed record CampaignPage(IReadOnlyList<CampaignDto> Items, string? NextCursor, bool HasMore, int Limit);
