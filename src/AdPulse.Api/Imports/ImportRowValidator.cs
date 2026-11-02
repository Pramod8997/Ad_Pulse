using System.Globalization;

#pragma warning disable CA1716

namespace AdPulse.Api.Imports;

public static class ImportRowValidator
{
    public static bool TryParse(
        CsvImportRow row,
        out ImportMetricCandidate? parsed,
        out ImportRowRejection? rejection)
    {
        parsed = null;
        rejection = null;
        var values = row.Fields.Select(value => value.Trim()).ToArray();

        if (values.Length != ImportContract.Headers.Count)
        {
            rejection = Reject(row.RowNumber, "malformed_row", "The row does not contain the expected number of columns.");
            return false;
        }

        if (!Guid.TryParse(values[0], out var campaignId))
        {
            rejection = Reject(row.RowNumber, "invalid_campaign_id", "The campaign identifier is invalid.");
            return false;
        }

        if (!DateOnly.TryParseExact(values[1], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var metricDate))
        {
            rejection = Reject(row.RowNumber, "invalid_date", "The metric date must use YYYY-MM-DD.");
            return false;
        }

        var currency = values[2];
        if (currency.Length != 3 || currency.Any(character => character is < 'A' or > 'Z'))
        {
            rejection = Reject(row.RowNumber, "invalid_currency", "The currency must be three uppercase letters.");
            return false;
        }

        if (!TryParseMoney(values[3], out var spend)
            || !TryParseMoney(values[4], out var revenue)
            || !TryParseCount(values[5], out var impressions)
            || !TryParseCount(values[6], out var clicks)
            || !TryParseCount(values[7], out var conversions))
        {
            rejection = Reject(row.RowNumber, "invalid_number", "A metric value is invalid or outside the supported range.");
            return false;
        }

        if (clicks > impressions || conversions > clicks)
        {
            rejection = Reject(row.RowNumber, "invalid_metric_relationship", "Clicks cannot exceed impressions and conversions cannot exceed clicks.");
            return false;
        }

        parsed = new ImportMetricCandidate(
            row.RowNumber,
            campaignId,
            metricDate,
            currency,
            spend,
            revenue,
            impressions,
            clicks,
            conversions);
        return true;
    }

    private static bool TryParseMoney(string value, out decimal result)
    {
        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out result)
            || result < 0m
            || decimal.Round(result, 4) != result
            || result > 999_999_999_999_999.9999m)
        {
            result = default;
            return false;
        }

        return true;
    }

    private static bool TryParseCount(string value, out long result) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result) && result >= 0;

    private static ImportRowRejection Reject(int rowNumber, string code, string message) =>
        new(rowNumber, code, message);
}
