using System.Globalization;
using System.Text;
using AdPulse.Api.Campaigns;
using AdPulse.Domain.Tenancy;
using AdPulse.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

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
    public void Cursor_round_trips_sort_keys_for_name_status_and_budget()
    {
        var nameCursor = new CampaignCursor(
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            Guid.NewGuid(),
            "hash-1",
            "name",
            "asc",
            TextKey: "Spring | Sale");
        var statusCursor = new CampaignCursor(
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            Guid.NewGuid(),
            "hash-2",
            "status",
            "desc",
            TextKey: "active");
        var budgetCursor = new CampaignCursor(
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            Guid.NewGuid(),
            "hash-3",
            "budget",
            "asc",
            BudgetKey: 1234.5678m);

        Assert.True(CampaignCursorCodec.TryDecode(CampaignCursorCodec.Encode(nameCursor), out var decodedName));
        Assert.True(CampaignCursorCodec.TryDecode(CampaignCursorCodec.Encode(statusCursor), out var decodedStatus));
        Assert.True(CampaignCursorCodec.TryDecode(CampaignCursorCodec.Encode(budgetCursor), out var decodedBudget));

        Assert.Equal(nameCursor, decodedName);
        Assert.Equal(statusCursor, decodedStatus);
        Assert.Equal(budgetCursor, decodedBudget);
    }

    [Fact]
    public void Decodes_legacy_pipe_delimited_cursor()
    {
        var createdAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var id = Guid.NewGuid();
        var raw = $"{createdAt.UtcTicks.ToString(CultureInfo.InvariantCulture)}|{id:D}|legacy-hash";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        Assert.True(CampaignCursorCodec.TryDecode(encoded, out var decoded));
        Assert.Equal(createdAt, decoded.CreatedAt);
        Assert.Equal(id, decoded.Id);
        Assert.Equal("legacy-hash", decoded.FilterHash);
        Assert.Equal("createdAt", decoded.Sort);
        Assert.Equal("desc", decoded.Direction);
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
    public void Filter_hash_changes_when_a_filter_or_sort_changes()
    {
        var query = new CampaignListQuery { Search = "spring", Status = "active" };

        Assert.NotEqual(
            CampaignCursorCodec.FilterHash(query),
            CampaignCursorCodec.FilterHash(query with { Search = "winter" }));
        Assert.NotEqual(
            CampaignCursorCodec.FilterHash(query),
            CampaignCursorCodec.FilterHash(query with { Sort = "budget" }));
        Assert.NotEqual(
            CampaignCursorCodec.FilterHash(query),
            CampaignCursorCodec.FilterHash(query with { Direction = "asc" }));
        Assert.Equal(
            CampaignCursorCodec.FilterHash(query),
            CampaignCursorCodec.FilterHash(query with { Sort = "CREATEDAT", Direction = "DESC" }));
    }

    [Theory]
    [InlineData("name", "asc", "Alpha,Beta,Gamma")]
    [InlineData("name", "desc", "Gamma,Beta,Alpha")]
    [InlineData("budget", "asc", "Alpha,Gamma,Beta")]
    [InlineData("budget", "desc", "Beta,Gamma,Alpha")]
    [InlineData("status", "asc", "Alpha,Beta,Gamma")]
    public async Task Keyset_pagination_paginates_stably_across_sort_fields(
        string sort,
        string direction,
        string expectedOrderCsv)
    {
        var expectedOrder = expectedOrderCsv.Split(',');
        await using var db = await TestDatabase.CreateAsync();
        var now = new DateTimeOffset(2026, 2, 1, 12, 0, 0, TimeSpan.Zero);

        await using (var system = db.Context(TenantScope.System))
        {
            system.Campaigns.AddRange(
                Campaign("tenant-a", Guid.Parse("00000000-0000-0000-0000-000000000001"), "Alpha", "active", 100m, now),
                Campaign("tenant-a", Guid.Parse("00000000-0000-0000-0000-000000000002"), "Beta", "draft", 300m, now.AddMinutes(1)),
                Campaign("tenant-a", Guid.Parse("00000000-0000-0000-0000-000000000003"), "Gamma", "paused", 200m, now.AddMinutes(2)),
                Campaign("tenant-b", Guid.Parse("00000000-0000-0000-0000-000000000099"), "OtherTenant", "active", 999m, now.AddMinutes(3)));
            await system.SaveChangesAsync();
        }

        await using var tenantDb = db.Context(TenantScope.For("tenant-a"));
        var service = new CampaignService(tenantDb, new TenantContext("tenant-a", "user-1", new HashSet<string>()), new AuditEventWriter());

        var page1 = await service.ListAsync(new CampaignListQuery { Sort = sort, Direction = direction, Limit = 2 });
        Assert.Equal(2, page1.Items.Count);
        Assert.True(page1.HasMore);
        Assert.NotNull(page1.NextCursor);

        var page2 = await service.ListAsync(new CampaignListQuery { Sort = sort, Direction = direction, Limit = 2, Cursor = page1.NextCursor });
        Assert.Single(page2.Items);
        Assert.False(page2.HasMore);
        Assert.Null(page2.NextCursor);

        var actualOrder = page1.Items.Concat(page2.Items).Select(c => c.Name).ToArray();
        Assert.Equal(expectedOrder, actualOrder);

        // Cursor bound to sort cannot be reused with a different sort.
        await Assert.ThrowsAsync<CampaignValidationException>(() => service.ListAsync(new CampaignListQuery
        {
            Sort = sort == "name" ? "budget" : "name",
            Direction = direction,
            Limit = 2,
            Cursor = page1.NextCursor
        }));
    }

    [Fact]
    public async Task List_and_get_enrich_campaigns_with_kpis_and_keep_zero_denominators_null()
    {
        await using var db = await TestDatabase.CreateAsync();
        var withMetricsId = Guid.NewGuid();
        var zeroDenominatorId = Guid.NewGuid();
        var noMetricsId = Guid.NewGuid();
        var created = new DateTimeOffset(2026, 2, 1, 12, 0, 0, TimeSpan.Zero);

        await using (var system = db.Context(TenantScope.System))
        {
            system.Campaigns.AddRange(
                Campaign("tenant-a", withMetricsId, "With Metrics", "active", 500m, created),
                Campaign("tenant-a", zeroDenominatorId, "Zero Denominator", "active", 500m, created.AddMinutes(1)),
                Campaign("tenant-a", noMetricsId, "No Metrics", "draft", 500m, created.AddMinutes(2)));

            system.DailyMetrics.AddRange(
                new DailyMetric
                {
                    TenantId = "tenant-a",
                    CampaignId = withMetricsId,
                    MetricDate = new DateOnly(2026, 1, 1),
                    Spend = 40m,
                    Revenue = 100m,
                    Impressions = 1000,
                    Clicks = 20,
                    Conversions = 2
                },
                new DailyMetric
                {
                    TenantId = "tenant-a",
                    CampaignId = withMetricsId,
                    MetricDate = new DateOnly(2026, 1, 2),
                    Spend = 60m,
                    Revenue = 200m,
                    Impressions = 1000,
                    Clicks = 30,
                    Conversions = 3
                },
                new DailyMetric
                {
                    TenantId = "tenant-a",
                    CampaignId = zeroDenominatorId,
                    MetricDate = new DateOnly(2026, 1, 5),
                    Spend = 0m,
                    Revenue = 0m,
                    Impressions = 100,
                    Clicks = 0,
                    Conversions = 0
                });

            await system.SaveChangesAsync();
        }

        await using var tenantDb = db.Context(TenantScope.For("tenant-a"));
        var service = new CampaignService(tenantDb, new TenantContext("tenant-a", "user-1", new HashSet<string>()), new AuditEventWriter());

        var page = await service.ListAsync(new CampaignListQuery { Limit = 10 });
        var withMetrics = Assert.Single(page.Items, c => c.Id == withMetricsId);
        Assert.Equal(100m, withMetrics.Spend);
        Assert.Equal(2m, withMetrics.Cpc);
        Assert.Equal(20m, withMetrics.Cpa);
        Assert.Equal(3m, withMetrics.Roas);
        Assert.Equal(new DateOnly(2026, 1, 2), withMetrics.LastMetricDate);

        var zeroDenominator = Assert.Single(page.Items, c => c.Id == zeroDenominatorId);
        Assert.Equal(0m, zeroDenominator.Spend);
        Assert.Null(zeroDenominator.Cpc);
        Assert.Null(zeroDenominator.Cpa);
        Assert.Null(zeroDenominator.Roas);
        Assert.Equal(new DateOnly(2026, 1, 5), zeroDenominator.LastMetricDate);

        var noMetrics = Assert.Single(page.Items, c => c.Id == noMetricsId);
        Assert.Null(noMetrics.Spend);
        Assert.Null(noMetrics.Cpc);
        Assert.Null(noMetrics.Cpa);
        Assert.Null(noMetrics.Roas);
        Assert.Null(noMetrics.LastMetricDate);

        var fetched = await service.GetAsync(withMetricsId);
        Assert.NotNull(fetched);
        Assert.Equal(100m, fetched!.Spend);
        Assert.Equal(2m, fetched.Cpc);
        Assert.Equal(20m, fetched.Cpa);
        Assert.Equal(3m, fetched.Roas);
        Assert.Equal(new DateOnly(2026, 1, 2), fetched.LastMetricDate);

        var fetchedNoMetrics = await service.GetAsync(noMetricsId);
        Assert.NotNull(fetchedNoMetrics);
        Assert.Null(fetchedNoMetrics!.Spend);
        Assert.Null(fetchedNoMetrics.LastMetricDate);
    }

    private static Campaign Campaign(string tenantId, Guid id, string name, string status, decimal budget, DateTimeOffset createdAt) => new()
    {
        TenantId = tenantId,
        Id = id,
        Name = name,
        Status = status,
        Currency = "USD",
        Budget = budget,
        StartsOn = new DateOnly(2026, 1, 1),
        EndsOn = new DateOnly(2026, 1, 31),
        CreatedAt = createdAt
    };

    private sealed class TestDatabase(SqliteConnection connection) : IAsyncDisposable
    {
        public AdPulseDbContext Context(TenantScope scope) => new(
            new DbContextOptionsBuilder<AdPulseDbContext>().UseSqlite(connection).Options,
            scope);

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new TestDatabase(connection);
            await using var system = db.Context(TenantScope.System);
            await system.Database.EnsureCreatedAsync();
            system.Tenants.AddRange(
                new Tenant { Id = "tenant-a", Name = "Tenant A", CreatedAt = DateTimeOffset.UtcNow },
                new Tenant { Id = "tenant-b", Name = "Tenant B", CreatedAt = DateTimeOffset.UtcNow });
            await system.SaveChangesAsync();
            return db;
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
