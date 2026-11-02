using AdPulse.Api.Audit;
using AdPulse.Domain.Tenancy;
using AdPulse.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AdPulse.Api.Tests.AuditSettings;

public sealed class AuditLogReaderTests
{
    [Fact]
    public async Task Uses_stable_keyset_ordering_and_cursor_pagination_inside_one_tenant()
    {
        await using var database = await TestDatabase.CreateAsync();
        var occurredAt = new DateTimeOffset(2026, 1, 3, 12, 0, 0, TimeSpan.Zero);
        var ids = new[]
        {
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            Guid.Parse("00000000-0000-0000-0000-000000000003")
        };

        await using (var system = database.Context(TenantScope.System))
        {
            system.AuditEvents.AddRange(
                Audit("tenant-a", ids[0], occurredAt, "campaign.created"),
                Audit("tenant-a", ids[1], occurredAt, "campaign.updated"),
                Audit("tenant-a", ids[2], occurredAt, "campaign.deleted"),
                Audit("tenant-b", Guid.Parse("00000000-0000-0000-0000-000000000099"), occurredAt, "other.tenant"));
            await system.SaveChangesAsync();
        }

        await using var tenant = database.Context(TenantScope.For("tenant-a"));
        var reader = new AuditLogReader(tenant);
        var first = await reader.ReadAsync(
            Member("tenant-a"),
            new AuditLogQuery { PageSize = 2 });

        Assert.True(first.HasMore);
        Assert.Equal(ids[2], first.Items[0].Id);
        Assert.Equal(ids[1], first.Items[1].Id);
        Assert.NotNull(first.NextCursor);

        var second = await reader.ReadAsync(
            Member("tenant-a"),
            new AuditLogQuery { PageSize = 2, Cursor = first.NextCursor });

        Assert.False(second.HasMore);
        Assert.Single(second.Items);
        Assert.Equal(ids[0], second.Items[0].Id);
        Assert.All(first.Items.Concat(second.Items), item => Assert.Equal("campaign", item.EntityType));
    }

    [Fact]
    public async Task Applies_time_actor_action_and_entity_filters()
    {
        await using var database = await TestDatabase.CreateAsync();
        var matchingId = Guid.NewGuid();
        var matchingTime = new DateTimeOffset(2026, 2, 2, 10, 0, 0, TimeSpan.Zero);

        await using (var system = database.Context(TenantScope.System))
        {
            system.AuditEvents.AddRange(
                Audit("tenant-a", matchingId, matchingTime, "campaign.updated", "user-1", "campaign", "c-1"),
                Audit("tenant-a", Guid.NewGuid(), matchingTime.AddHours(-1), "campaign.updated", "user-1", "campaign", "c-1"),
                Audit("tenant-a", Guid.NewGuid(), matchingTime, "alert.updated", "user-1", "alert", "a-1"),
                Audit("tenant-a", Guid.NewGuid(), matchingTime, "campaign.updated", "user-2", "campaign", "c-1"));
            await system.SaveChangesAsync();
        }

        await using var tenant = database.Context(TenantScope.For("tenant-a"));
        var page = await new AuditLogReader(tenant).ReadAsync(
            Member("tenant-a"),
            new AuditLogQuery
            {
                FromUtcInclusive = matchingTime.AddMinutes(-1),
                ToUtcExclusive = matchingTime.AddMinutes(1),
                ActorSubjectId = "user-1",
                Action = "campaign.updated",
                EntityType = "campaign",
                EntityId = "c-1"
            });

        Assert.Single(page.Items);
        Assert.Equal(matchingId, page.Items[0].Id);
    }

    [Fact]
    public async Task Rejects_a_database_scope_that_does_not_match_authenticated_tenant()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var tenant = database.Context(TenantScope.For("tenant-a"));

        await Assert.ThrowsAsync<TenantAccessDeniedException>(() =>
            new AuditLogReader(tenant).ReadAsync(Member("tenant-b")));
    }

    [Fact]
    public async Task Rejects_a_cursor_reused_with_a_different_filter()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using (var system = database.Context(TenantScope.System))
        {
            system.AuditEvents.AddRange(
                Audit("tenant-a", Guid.NewGuid(), DateTimeOffset.UtcNow, "campaign.created"),
                Audit("tenant-a", Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1), "campaign.updated"));
            await system.SaveChangesAsync();
        }

        await using var tenant = database.Context(TenantScope.For("tenant-a"));
        var reader = new AuditLogReader(tenant);
        var first = await reader.ReadAsync(Member("tenant-a"), new AuditLogQuery { PageSize = 1 });

        await Assert.ThrowsAsync<ArgumentException>(() => reader.ReadAsync(
            Member("tenant-a"),
            new AuditLogQuery { PageSize = 1, Cursor = first.NextCursor, Action = "campaign.created" }));
    }

    private static TenantContext Member(string tenantId) =>
        new(tenantId, "user-1", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "analyst" });

    private static AuditEvent Audit(
        string tenant,
        Guid id,
        DateTimeOffset occurredAt,
        string action,
        string actor = "user-1",
        string entityType = "campaign",
        string entityId = "c-1") => new()
        {
            TenantId = tenant,
            Id = id,
            OccurredAt = occurredAt,
            ActorSubjectId = actor,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            PayloadJson = "{\"tenant\":\"" + tenant + "\"}"
        };

    private sealed class TestDatabase(SqliteConnection connection) : IAsyncDisposable
    {
        public AdPulseDbContext Context(TenantScope scope)
        {
            var options = new DbContextOptionsBuilder<AdPulseDbContext>()
                .UseSqlite(connection)
                .Options;
            return new AdPulseDbContext(options, scope);
        }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            await using var context = new TestDatabase(connection).Context(TenantScope.System);
            await context.Database.EnsureCreatedAsync();
            context.Tenants.AddRange(
                new Tenant { Id = "tenant-a", Name = "Tenant A", CreatedAt = DateTimeOffset.UtcNow },
                new Tenant { Id = "tenant-b", Name = "Tenant B", CreatedAt = DateTimeOffset.UtcNow });
            await context.SaveChangesAsync();
            return new TestDatabase(connection);
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
