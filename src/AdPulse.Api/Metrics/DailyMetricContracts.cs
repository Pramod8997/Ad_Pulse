using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdPulse.Api.Contracts;
using AdPulse.Domain.Analytics;
using AdPulse.Infrastructure.Data;

namespace AdPulse.Api.Metrics;

public sealed record DailyMetricIngestRequest(
    [property: JsonPropertyName("metricDate")] DateOnly MetricDate,
    [property: JsonPropertyName("spend")] decimal Spend,
    [property: JsonPropertyName("revenue")] decimal Revenue,
    [property: JsonPropertyName("impressions")] long Impressions,
    [property: JsonPropertyName("clicks")] long Clicks,
    [property: JsonPropertyName("conversions")] long Conversions);

public sealed record DailyMetricResponse(
    Guid CampaignId,
    DateOnly MetricDate,
    decimal Spend,
    decimal Revenue,
    long Impressions,
    long Clicks,
    long Conversions,
    DailyMetricKpiResponse Kpis)
{
    public DailyMetricDto ToContract() => new()
    {
        CampaignId = CampaignId,
        MetricDate = MetricDate,
        Spend = Spend,
        Revenue = Revenue,
        Impressions = Impressions,
        Clicks = Clicks,
        Conversions = Conversions,
        Kpis = new KpiMetricsDto
        {
            Ctr = Kpis.Ctr,
            Cpc = Kpis.Cpc,
            Cpa = Kpis.Cpa,
            Roas = Kpis.Roas,
            BudgetPacing = Kpis.BudgetPacing
        }
    };

    public static DailyMetricResponse From(DailyMetric metric, Campaign? campaign = null)
    {
        ArgumentNullException.ThrowIfNull(metric);

        var plannedToDateBudget = campaign is null
            ? null
            : PlannedToDateBudget(campaign, metric.MetricDate);
        var kpis = KpiCalculator.Calculate(
            metric.Spend,
            metric.Revenue,
            metric.Impressions,
            metric.Clicks,
            metric.Conversions,
            plannedToDateBudget);

        return new DailyMetricResponse(
            metric.CampaignId,
            metric.MetricDate,
            metric.Spend,
            metric.Revenue,
            metric.Impressions,
            metric.Clicks,
            metric.Conversions,
            new DailyMetricKpiResponse(kpis.Ctr, kpis.Cpc, kpis.Cpa, kpis.Roas, kpis.BudgetPacing));
    }

    private static decimal? PlannedToDateBudget(Campaign campaign, DateOnly metricDate)
    {
        var campaignDays = campaign.EndsOn.DayNumber - campaign.StartsOn.DayNumber + 1;
        if (campaignDays <= 0)
        {
            return null;
        }

        var elapsedDays = Math.Clamp(metricDate.DayNumber - campaign.StartsOn.DayNumber + 1, 0, campaignDays);
        return campaign.Budget * elapsedDays / campaignDays;
    }
}

public sealed record DailyMetricKpiResponse(
    decimal? Ctr,
    decimal? Cpc,
    decimal? Cpa,
    decimal? Roas,
    decimal? BudgetPacing);

public sealed record DailyMetricPageResponse(
    IReadOnlyList<DailyMetricResponse> Items,
    string? NextCursor,
    bool HasMore,
    int Limit);

public sealed record DailyMetricIngestResponse(
    DailyMetricResponse Metric,
    bool Created);

public sealed record MetricCursor(
    DateOnly MetricDate,
    Guid CampaignId,
    DateOnly? From = null,
    DateOnly? To = null)
{
    private const int MaxEncodedLength = 256;

    public string Encode()
    {
        var payload = JsonSerializer.Serialize(new CursorPayload(
            MetricDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            CampaignId,
            From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            To?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static bool TryDecode(string? encoded, out MetricCursor? cursor)
    {
        cursor = null;
        if (string.IsNullOrWhiteSpace(encoded) || encoded.Length > MaxEncodedLength)
        {
            return false;
        }

        try
        {
            var base64 = encoded.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            var payload = JsonSerializer.Deserialize<CursorPayload>(Convert.FromBase64String(base64));
            if (payload is null
                || !DateOnly.TryParseExact(payload.MetricDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                || payload.CampaignId == Guid.Empty)
            {
                return false;
            }

            if (!TryParseDate(payload.From, out var from) || !TryParseDate(payload.To, out var to))
            {
                return false;
            }

            cursor = new MetricCursor(date, payload.CampaignId, from, to);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryParseDate(string? value, out DateOnly? date)
    {
        date = null;
        if (value is null)
        {
            return true;
        }

        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return false;
        }

        date = parsed;
        return true;
    }

    private sealed record CursorPayload(string MetricDate, Guid CampaignId, string? From, string? To);
}

public static class MetricValidation
{
    public const int PageSizeDefault = PageQuery.DefaultLimit;
    public const int PageSizeMax = 100;
    public const long MetricCountMax = 9_000_000_000_000_000_000;
    public const decimal MoneyMax = 999_999_999_999_999.9999m;

    public static IReadOnlyDictionary<string, string[]> Validate(DailyMetricIngestRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (request.Spend < 0 || !FitsMoney(request.Spend))
        {
            errors[nameof(request.Spend)] = ["Spend must be non-negative and fit decimal(19,4)."];
        }

        if (request.Revenue < 0 || !FitsMoney(request.Revenue))
        {
            errors[nameof(request.Revenue)] = ["Revenue must be non-negative and fit decimal(19,4)."];
        }

        if (request.Impressions < 0 || request.Impressions > MetricCountMax)
        {
            errors[nameof(request.Impressions)] = ["Impressions must be non-negative and within the supported bound."];
        }

        if (request.Clicks < 0 || request.Clicks > MetricCountMax)
        {
            errors[nameof(request.Clicks)] = ["Clicks must be non-negative and within the supported bound."];
        }
        else if (request.Clicks > request.Impressions)
        {
            errors[nameof(request.Clicks)] = ["Clicks cannot exceed impressions."];
        }

        if (request.Conversions < 0 || request.Conversions > MetricCountMax)
        {
            errors[nameof(request.Conversions)] = ["Conversions must be non-negative and within the supported bound."];
        }
        else if (request.Conversions > request.Clicks)
        {
            errors[nameof(request.Conversions)] = ["Conversions cannot exceed clicks."];
        }

        return errors;
    }

    public static IReadOnlyDictionary<string, string[]> ValidateDate(
        DateOnly metricDate,
        DateOnly startsOn,
        DateOnly endsOn)
    {
        if (metricDate < startsOn || metricDate > endsOn)
        {
            return new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [nameof(DailyMetricIngestRequest.MetricDate)] = ["MetricDate must be inside the campaign date range."]
            };
        }

        return new Dictionary<string, string[]>(StringComparer.Ordinal);
    }

    public static IReadOnlyDictionary<string, string[]> ValidateQuery(MetricListQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var errors = ValidateQuery(query.From, query.To, query.Limit);
        if (query.Cursor?.Length > 256)
        {
            var mutable = errors.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            mutable[nameof(query.Cursor)] = ["Cursor is too long."];
            return mutable;
        }

        return errors;
    }

    public static IReadOnlyDictionary<string, string[]> ValidateQuery(
        DateOnly? from,
        DateOnly? to,
        int limit)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (from is not null && to is not null && from > to)
        {
            errors["from"] = ["The from date cannot be after the to date."];
        }

        if (limit is < 1 or > PageSizeMax)
        {
            errors["limit"] = [$"Limit must be between 1 and {PageSizeMax}."];
        }

        return errors;
    }

    private static bool FitsMoney(decimal value) =>
        value is >= 0 and <= MoneyMax && decimal.Round(value, 4) == value;
}
