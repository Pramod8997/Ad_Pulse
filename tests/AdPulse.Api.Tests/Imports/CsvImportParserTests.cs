using AdPulse.Api.Imports;

namespace AdPulse.Api.Tests.ImportTests;

public sealed class CsvImportParserTests
{
    private static readonly Guid CampaignId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Parses_the_download_template_and_keeps_only_field_values()
    {
        var parser = new CsvImportParser();
        var result = parser.Parse($"{string.Join(',', ImportContract.Headers)}\n{CampaignId},2026-01-02,USD,10.2500,20.0000,1000,100,4\n");

        Assert.Equal(ImportContract.Headers, result.Headers);
        var row = Assert.Single(result.Rows);
        Assert.Equal(2, row.RowNumber);
        Assert.Equal(CampaignId.ToString(), row.Fields[0]);
        Assert.Equal("2026-01-02", row.Fields[1]);
    }

    [Fact]
    public void Rejects_headers_that_are_missing_reordered_or_extra()
    {
        var parser = new CsvImportParser();

        var exception = Assert.Throws<ImportValidationException>(() => parser.Parse(
            "metric_date,campaign_id,currency,spend,revenue,impressions,clicks,conversions\n"));

        Assert.Equal("malformed_headers", exception.Code);
        Assert.DoesNotContain("campaign_id", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Supports_quoted_fields_and_does_not_create_a_raw_row_property()
    {
        var parser = new CsvImportParser();
        var csv = $"{string.Join(',', ImportContract.Headers)}\n{CampaignId},2026-01-02,USD,\"10.2500\",20,1000,100,4\n";

        var result = parser.Parse(csv);

        Assert.Equal("10.2500", result.Rows[0].Fields[3]);
        Assert.DoesNotContain("RawRow", typeof(CsvImportRow).GetProperties().Select(property => property.Name));
    }

    [Fact]
    public void Keeps_row_fields_bounded_to_the_declared_columns()
    {
        var parser = new CsvImportParser();
        var result = parser.Parse(
            $"{string.Join(',', ImportContract.Headers)}\n{CampaignId},2026-01-02,USD,10,20,1000,100,4\n");

        Assert.All(result.Rows, row => Assert.Equal(ImportContract.Headers.Count, row.Fields.Count));
    }

    [Theory]
    [InlineData("2026-02-30", "invalid_date")]
    [InlineData("usd", "invalid_currency")]
    [InlineData("-1", "invalid_number")]
    [InlineData("1e2", "invalid_number")]
    public void Row_validator_reports_safe_metadata_for_invalid_values(string value, string expectedCode)
    {
        var fields = new[]
        {
            CampaignId.ToString(),
            "2026-01-02",
            "USD",
            "10",
            "20",
            "1000",
            "100",
            "4"
        };
        if (expectedCode == "invalid_date")
        {
            fields[1] = value;
        }
        else if (expectedCode == "invalid_currency")
        {
            fields[2] = value;
        }
        else
        {
            fields[3] = value;
        }

        var valid = ImportRowValidator.TryParse(
            new CsvImportRow(2, fields),
            out _,
            out var rejection);

        Assert.False(valid);
        Assert.Equal(expectedCode, rejection!.ErrorCode);
        Assert.DoesNotContain(value, rejection.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_an_unterminated_quoted_field()
    {
        var parser = new CsvImportParser();
        var exception = Assert.Throws<ImportValidationException>(() => parser.Parse(
            $"{string.Join(',', ImportContract.Headers)}\n{CampaignId},2026-01-02,USD,\"10,20,1000,100,4\n"));

        Assert.Equal("malformed_csv", exception.Code);
    }
}
