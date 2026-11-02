using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AdPulse.Api.Campaigns;

public readonly record struct CampaignCursor(DateTimeOffset CreatedAt, Guid Id, string FilterHash = "");

public static class CampaignCursorCodec
{
    public static string Encode(CampaignCursor cursor)
    {
        var value = string.Concat(
            cursor.CreatedAt.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture),
            "|",
            cursor.Id.ToString("D"),
            "|",
            cursor.FilterHash);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
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
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(padded)).Split('|');
            if (parts.Length != 3
                || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
                || !Guid.TryParseExact(parts[1], "D", out var id)
                || ticks < DateTimeOffset.MinValue.UtcTicks
                || ticks > DateTimeOffset.MaxValue.UtcTicks)
            {
                return false;
            }

            cursor = new CampaignCursor(new DateTimeOffset(ticks, TimeSpan.Zero), id, parts.Length == 3 ? parts[2] : "");
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
    }

    public static string FilterHash(CampaignListQuery query)
    {
        var canonical = string.Join("\n", [
            query.Search?.Trim() ?? "",
            query.Status?.Trim().ToLowerInvariant() ?? "",
            query.StartsOnFrom?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
            query.StartsOnTo?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
            query.EndsOnFrom?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
            query.EndsOnTo?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? ""
        ]);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}
