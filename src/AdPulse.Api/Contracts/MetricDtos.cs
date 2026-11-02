using System.Text.Json.Serialization;

namespace AdPulse.Api.Contracts;

public sealed record DailyMetricDto
{
    [JsonPropertyName("campaignId")]
    public Guid CampaignId { get; init; }

    [JsonPropertyName("metricDate")]
    public DateOnly MetricDate { get; init; }

    [JsonPropertyName("spend")]
    public decimal Spend { get; init; }

    [JsonPropertyName("revenue")]
    public decimal Revenue { get; init; }

    [JsonPropertyName("impressions")]
    public long Impressions { get; init; }

    [JsonPropertyName("clicks")]
    public long Clicks { get; init; }

    [JsonPropertyName("conversions")]
    public long Conversions { get; init; }

    [JsonPropertyName("kpis")]
    public KpiMetricsDto Kpis { get; init; } = new();
}

public sealed record UpsertDailyMetricRequest
{
    [JsonPropertyName("spend")]
    public decimal Spend { get; init; }

    [JsonPropertyName("revenue")]
    public decimal Revenue { get; init; }

    [JsonPropertyName("impressions")]
    public long Impressions { get; init; }

    [JsonPropertyName("clicks")]
    public long Clicks { get; init; }

    [JsonPropertyName("conversions")]
    public long Conversions { get; init; }
}

public sealed record MetricListQuery : PageQuery
{
    [JsonPropertyName("from")]
    public DateOnly? From { get; init; }

    [JsonPropertyName("to")]
    public DateOnly? To { get; init; }
}
