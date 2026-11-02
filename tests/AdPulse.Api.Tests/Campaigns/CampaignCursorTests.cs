using AdPulse.Api.Campaigns;

namespace AdPulse.Api.Tests.Campaigns;

public sealed class CampaignCursorTests
{
    [Fact]
    public void Cursor_round_trips_the_stable_ordering_keys()
    {
        var expected = new CampaignCursor(
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            Guid.NewGuid(),
            "filter-hash");

        var encoded = CampaignCursorCodec.Encode(expected);

        Assert.True(CampaignCursorCodec.TryDecode(encoded, out var actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Filter_hash_is_stable_for_the_same_filters()
    {
        var query = new CampaignListQuery { Search = "spring", Status = "ACTIVE" };

        Assert.Equal(CampaignCursorCodec.FilterHash(query), CampaignCursorCodec.FilterHash(query));
        Assert.NotEqual(CampaignCursorCodec.FilterHash(query), CampaignCursorCodec.FilterHash(query with { Status = "paused" }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-cursor")]
    [InlineData("dGVuYW50fG5vdC1hLWd1aWQ")]
    public void Rejects_malformed_cursor(string encoded)
    {
        Assert.False(CampaignCursorCodec.TryDecode(encoded, out _));
    }

    [Fact]
    public void Filter_hash_changes_when_a_filter_changes()
    {
        var query = new CampaignListQuery { Search = "spring", Status = "active" };

        Assert.NotEqual(
            CampaignCursorCodec.FilterHash(query),
            CampaignCursorCodec.FilterHash(query with { Search = "winter" }));
    }
}
