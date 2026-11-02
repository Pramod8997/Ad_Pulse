using System.Text.Json.Serialization;

namespace AdPulse.Api.Contracts;

public sealed record CursorPage<T>
{
    [JsonPropertyName("items")]
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; init; }

    [JsonPropertyName("hasMore")]
    public bool HasMore { get; init; }

    [JsonPropertyName("limit")]
    public int Limit { get; init; } = PageQuery.DefaultLimit;
}

public record PageQuery
{
    public const int DefaultLimit = 25;
    public const int MaxLimit = 100;

    [JsonPropertyName("cursor")]
    public string? Cursor { get; init; }

    [JsonPropertyName("limit")]
    public int Limit { get; init; } = DefaultLimit;
}

public sealed record ProblemDetailsDto
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "about:blank";

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public int Status { get; init; }

    [JsonPropertyName("detail")]
    public string Detail { get; init; } = string.Empty;

    [JsonPropertyName("instance")]
    public string? Instance { get; init; }

    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; init; } = string.Empty;

    [JsonPropertyName("errors")]
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
}

public sealed record KpiMetricsDto
{
    [JsonPropertyName("ctr")]
    public decimal? Ctr { get; init; }

    [JsonPropertyName("cpc")]
    public decimal? Cpc { get; init; }

    [JsonPropertyName("cpa")]
    public decimal? Cpa { get; init; }

    [JsonPropertyName("roas")]
    public decimal? Roas { get; init; }

    [JsonPropertyName("budgetPacing")]
    public decimal? BudgetPacing { get; init; }
}
