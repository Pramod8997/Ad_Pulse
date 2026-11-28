using AdPulse.Api.Campaigns;

namespace AdPulse.Api.Tests.Campaigns;

public sealed class CampaignValidationTests
{
    [Fact]
    public void Accepts_valid_campaign_values_and_normalizes_text()
    {
        var request = ValidRequest with
        {
            Name = "  Spring campaign  ",
            Status = " ACTIVE "
        };

        var errors = CampaignValidation.Validate(request);

        Assert.Empty(errors);
        Assert.Equal("Spring campaign", CampaignValidation.NormalizedName(request.Name!));
        Assert.Equal("active", CampaignValidation.NormalizedStatus(request.Status!));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1000000000000000)]
    public void Rejects_budget_outside_decimal_money_contract(decimal budget)
    {
        var errors = CampaignValidation.Validate(ValidRequest with { Budget = budget });

        Assert.Contains(nameof(CampaignWriteRequest.Budget), errors.Keys);
    }

    [Fact]
    public void Rejects_budget_with_more_than_four_decimal_places()
    {
        var errors = CampaignValidation.Validate(ValidRequest with { Budget = 1000.12345m });

        Assert.Contains(nameof(CampaignWriteRequest.Budget), errors.Keys);
    }

    [Fact]
    public void Rejects_invalid_currency_and_reversed_dates()
    {
        var errors = CampaignValidation.Validate(ValidRequest with
        {
            Currency = "usd",
            StartsOn = new DateOnly(2026, 2, 1),
            EndsOn = new DateOnly(2026, 1, 31)
        });

        Assert.Contains(nameof(CampaignWriteRequest.Currency), errors.Keys);
        Assert.Contains(nameof(CampaignWriteRequest.EndsOn), errors.Keys);
    }

    [Fact]
    public void Rejects_unknown_status()
    {
        var errors = CampaignValidation.Validate(ValidRequest with { Status = "running" });

        Assert.Contains(nameof(CampaignWriteRequest.Status), errors.Keys);
    }

    [Fact]
    public void Rejects_missing_required_values()
    {
        var errors = CampaignValidation.Validate(new CampaignWriteRequest());

        Assert.Contains(nameof(CampaignWriteRequest.Name), errors.Keys);
        Assert.Contains(nameof(CampaignWriteRequest.Status), errors.Keys);
        Assert.Contains(nameof(CampaignWriteRequest.Currency), errors.Keys);
        Assert.Contains(nameof(CampaignWriteRequest.Budget), errors.Keys);
        Assert.Contains(nameof(CampaignWriteRequest.StartsOn), errors.Keys);
        Assert.Contains(nameof(CampaignWriteRequest.EndsOn), errors.Keys);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("createdAt", "desc")]
    [InlineData("CREATEDAT", "ASC")]
    [InlineData("name", "asc")]
    [InlineData("status", "desc")]
    [InlineData("budget", "asc")]
    public void Accepts_valid_list_query_sort_and_direction(string? sort, string? direction)
    {
        var errors = CampaignValidation.Validate(new CampaignListQuery
        {
            Sort = sort,
            Direction = direction
        });

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("", "desc")]
    [InlineData("   ", "asc")]
    [InlineData("spend", "desc")]
    [InlineData("createdAt", "")]
    [InlineData("name", "sideways")]
    public void Rejects_invalid_list_query_sort_and_direction(string sort, string direction)
    {
        var errors = CampaignValidation.Validate(new CampaignListQuery
        {
            Sort = sort,
            Direction = direction
        });

        Assert.True(
            errors.ContainsKey(nameof(CampaignListQuery.Sort))
            || errors.ContainsKey(nameof(CampaignListQuery.Direction)));
    }

    [Fact]
    public void Rejects_both_invalid_sort_and_invalid_direction_with_field_errors()
    {
        var errors = CampaignValidation.Validate(new CampaignListQuery
        {
            Sort = "unknown",
            Direction = "up"
        });

        Assert.Contains(nameof(CampaignListQuery.Sort), errors.Keys);
        Assert.Contains(nameof(CampaignListQuery.Direction), errors.Keys);
    }

    private static CampaignWriteRequest ValidRequest => new()
    {
        Name = "Spring campaign",
        Status = "active",
        Currency = "USD",
        Budget = 1000.1234m,
        StartsOn = new DateOnly(2026, 1, 1),
        EndsOn = new DateOnly(2026, 1, 31)
    };
}
