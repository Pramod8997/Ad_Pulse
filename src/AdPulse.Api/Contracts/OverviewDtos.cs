using System.Text.Json.Serialization;

namespace AdPulse.Api.Contracts;

public sealed record OverviewQuery
{
    [JsonPropertyName("from")]
    public DateOnly From { get; init; }

    [JsonPropertyName("to")]
    public DateOnly To { get; init; }

    [JsonPropertyName("currency")]
    public string? Currency { get; init; }
}

public sealed record OverviewTrendPointDto
{
    [JsonPropertyName("date")]
    public DateOnly Date { get; init; }

    [JsonPropertyName("spend")]
    public decimal? Spend { get; init; }

    [JsonPropertyName("revenue")]
    public decimal? Revenue { get; init; }
}

public sealed record BudgetPacingDto
{
    [JsonPropertyName("campaignId")]
    public Guid CampaignId { get; init; }

    [JsonPropertyName("campaignName")]
    public string CampaignName { get; init; } = string.Empty;

    [JsonPropertyName("spent")]
    public decimal? Spent { get; init; }

    [JsonPropertyName("plannedToDate")]
    public decimal? PlannedToDate { get; init; }

    [JsonPropertyName("pacing")]
    public decimal? Pacing { get; init; }

    [JsonPropertyName("remainingBudget")]
    public decimal? RemainingBudget { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;
}

public sealed record OverviewDto
{
    [JsonPropertyName("from")]
    public DateOnly From { get; init; }

    [JsonPropertyName("to")]
    public DateOnly To { get; init; }

    [JsonPropertyName("reportingTimeZone")]
    public string ReportingTimeZone { get; init; } = string.Empty;

    [JsonPropertyName("currency")]
    public string Currency { get; init; } = string.Empty;

    [JsonPropertyName("spend")]
    public decimal? Spend { get; init; }

    [JsonPropertyName("revenue")]
    public decimal? Revenue { get; init; }

    [JsonPropertyName("kpis")]
    public KpiMetricsDto Kpis { get; init; } = new();

    [JsonPropertyName("trend")]
    public IReadOnlyList<OverviewTrendPointDto> Trend { get; init; } = Array.Empty<OverviewTrendPointDto>();

    [JsonPropertyName("budgetPacing")]
    public IReadOnlyList<BudgetPacingDto> BudgetPacing { get; init; } = Array.Empty<BudgetPacingDto>();

    [JsonPropertyName("priorityAlerts")]
    public IReadOnlyList<AlertSummaryDto> PriorityAlerts { get; init; } = Array.Empty<AlertSummaryDto>();
}
