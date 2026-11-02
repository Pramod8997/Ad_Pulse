using System.Text.Json.Serialization;

namespace AdPulse.Api.Contracts;

public static class CampaignStatuses
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string Paused = "paused";
    public const string Completed = "completed";
}

public record CampaignDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("currency")]
    public string Currency { get; init; } = string.Empty;

    [JsonPropertyName("budget")]
    public decimal Budget { get; init; }

    [JsonPropertyName("startsOn")]
    public DateOnly StartsOn { get; init; }

    [JsonPropertyName("endsOn")]
    public DateOnly EndsOn { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record CampaignListItemDto : CampaignDto
{
    [JsonPropertyName("spend")]
    public decimal? Spend { get; init; }

    [JsonPropertyName("cpc")]
    public decimal? Cpc { get; init; }

    [JsonPropertyName("cpa")]
    public decimal? Cpa { get; init; }

    [JsonPropertyName("roas")]
    public decimal? Roas { get; init; }

    [JsonPropertyName("lastMetricDate")]
    public DateOnly? LastMetricDate { get; init; }
}

public sealed record CampaignDetailDto
{
    [JsonPropertyName("campaign")]
    public CampaignDto Campaign { get; init; } = new();

    [JsonPropertyName("spend")]
    public decimal? Spend { get; init; }

    [JsonPropertyName("revenue")]
    public decimal? Revenue { get; init; }

    [JsonPropertyName("lastMetricDate")]
    public DateOnly? LastMetricDate { get; init; }

    [JsonPropertyName("kpis")]
    public KpiMetricsDto Kpis { get; init; } = new();

    [JsonPropertyName("recentAlerts")]
    public IReadOnlyList<AlertSummaryDto> RecentAlerts { get; init; } = Array.Empty<AlertSummaryDto>();
}

public sealed record CreateCampaignRequest
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = CampaignStatuses.Draft;

    [JsonPropertyName("currency")]
    public string Currency { get; init; } = string.Empty;

    [JsonPropertyName("budget")]
    public decimal Budget { get; init; }

    [JsonPropertyName("startsOn")]
    public DateOnly StartsOn { get; init; }

    [JsonPropertyName("endsOn")]
    public DateOnly EndsOn { get; init; }
}

public sealed record UpdateCampaignRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("currency")]
    public string? Currency { get; init; }

    [JsonPropertyName("budget")]
    public decimal? Budget { get; init; }

    [JsonPropertyName("startsOn")]
    public DateOnly? StartsOn { get; init; }

    [JsonPropertyName("endsOn")]
    public DateOnly? EndsOn { get; init; }
}

public sealed record CampaignListQuery : PageQuery
{
    [JsonPropertyName("search")]
    public string? Search { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("sort")]
    public string Sort { get; init; } = "createdAt";

    [JsonPropertyName("direction")]
    public string Direction { get; init; } = "desc";
}
