using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AdPulse.Api.Campaigns;

public readonly record struct CampaignCursor(
    DateTimeOffset CreatedAt,
    Guid Id,
    string FilterHash = "",
    string Sort = "createdAt",
    string Direction = "desc",
    string? TextKey = null,
    decimal? BudgetKey = null);

public static class CampaignCursorCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Encode(CampaignCursor cursor)
    {
        var sort = CampaignValidation.NormalizedSort(cursor.Sort);
        var direction = CampaignValidation.NormalizedDirection(cursor.Direction);

        var payload = new CursorPayload(
            cursor.CreatedAt.ToUniversalTime().Ticks,
            cursor.Id,
            cursor.FilterHash,
            sort,
            direction,
            cursor.TextKey,
            cursor.BudgetKey);

        return Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static bool TryDecode(string? encoded, out CampaignCursor cursor)
    {
        cursor = default;
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return false;
        }

        try
        {
            var padded = encoded.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
            var bytes = Convert.FromBase64String(padded);

            if (bytes.Length > 0 && bytes[0] == (byte)'{')
            {
                var payload = JsonSerializer.Deserialize<CursorPayload>(bytes, JsonOptions);
                if (payload is null
                    || payload.Ticks < DateTimeOffset.MinValue.UtcTicks
                    || payload.Ticks > DateTimeOffset.MaxValue.UtcTicks
                    || !IsValidSort(payload.Sort, out var sort)
                    || !IsValidDirection(payload.Direction, out var direction))
                {
                    return false;
                }

                if (sort is "name" or "status" && payload.TextKey is null)
                {
                    return false;
                }

                if (sort == "budget" && (payload.BudgetKey is null || payload.BudgetKey < 0m))
                {
                    return false;
                }

                cursor = new CampaignCursor(
                    new DateTimeOffset(payload.Ticks, TimeSpan.Zero),
                    payload.Id,
                    payload.FilterHash ?? string.Empty,
                    sort,
                    direction,
                    payload.TextKey,
                    payload.BudgetKey);
                return true;
            }

            // Backward compatibility for legacy pipe-delimited createdAt DESC cursors.
            var parts = Encoding.UTF8.GetString(bytes).Split('|');
            if (parts.Length != 3
                || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
                || !Guid.TryParseExact(parts[1], "D", out var id)
                || ticks < DateTimeOffset.MinValue.UtcTicks
                || ticks > DateTimeOffset.MaxValue.UtcTicks)
            {
                return false;
            }

            cursor = new CampaignCursor(new DateTimeOffset(ticks, TimeSpan.Zero), id, parts[2]);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string FilterHash(CampaignListQuery query)
    {
        var canonical = string.Join("\n", [
            query.Search?.Trim() ?? "",
            query.Status?.Trim().ToLowerInvariant() ?? "",
            query.StartsOnFrom?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
            query.StartsOnTo?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
            query.EndsOnFrom?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
            query.EndsOnTo?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
            CampaignValidation.NormalizedSort(query.Sort),
            CampaignValidation.NormalizedDirection(query.Direction)
        ]);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static bool IsValidSort(string? sort, out string normalized)
    {
        normalized = sort?.Trim().ToLowerInvariant() switch
        {
            "createdat" => "createdAt",
            "name" => "name",
            "status" => "status",
            "budget" => "budget",
            _ => string.Empty
        };
        return normalized.Length > 0;
    }

    private static bool IsValidDirection(string? direction, out string normalized)
    {
        normalized = direction?.Trim().ToLowerInvariant() switch
        {
            "asc" => "asc",
            "desc" => "desc",
            _ => string.Empty
        };
        return normalized.Length > 0;
    }

    private sealed record CursorPayload(
        long Ticks,
        Guid Id,
        string? FilterHash,
        string? Sort,
        string? Direction,
        string? TextKey,
        decimal? BudgetKey);
}
