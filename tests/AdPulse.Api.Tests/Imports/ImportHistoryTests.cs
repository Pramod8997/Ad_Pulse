using System.Security;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using AdPulse.Api.Contracts;
using AdPulse.Api.Imports;
using AdPulse.Domain.Tenancy;
using AdPulse.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdPulse.Api.Tests.ImportTests;

public sealed class ImportHistoryTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ListAsync_orders_newest_first_with_id_tiebreaker_and_isolates_tenants()
    {
        await using var database = await TestDatabase.CreateAsync();
        var t1 = new DateTimeOffset(2026, 2, 1, 10, 0, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2026, 2, 2, 10, 0, 0, TimeSpan.Zero);
        var idLow = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var idHigh = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var idOlder = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var idOtherTenant = Guid.Parse("00000000-0000-0000-0000-000000000099");

        await using (var system = database.Context(TenantScope.System))
        {
            system.ImportBatches.AddRange(
                Batch("tenant-a", idOlder, t1, "older.csv", 1, 1, ImportContract.CompletedStatus),
                Batch("tenant-a", idLow, t2, "tie-low.csv", 2, 0, ImportContract.ValidatedStatus),
                Batch("tenant-a", idHigh, t2, "tie-high.csv", 3, 0, ImportContract.CompletedStatus),
                Batch("tenant-b", idOtherTenant, t2.AddDays(1), "other-tenant.csv", 5, 0, ImportContract.CompletedStatus));
            system.ImportRowErrors.AddRange(
                new ImportRowError
                {
                    TenantId = "tenant-a",
                    ImportBatchId = idOlder,
                    RowNumber = 3,
                    ErrorCode = "invalid_date",
                    Message = "Invalid date"
                },
                new ImportRowError
                {
                    TenantId = "tenant-a",
                    ImportBatchId = idOlder,
                    RowNumber = 2,
                    ErrorCode = "invalid_spend",
                    Message = "Invalid spend"
                });
            await system.SaveChangesAsync();
        }

        await using var db = database.Context(TenantScope.For("tenant-a"));
        var workflow = new ImportWorkflow();
        var tenant = Member("tenant-a", "importer");

        var first = await workflow.ListAsync(db, tenant, cursor: null, limit: 2);
        Assert.Equal(2, first.Limit);
        Assert.True(first.HasMore);
        Assert.NotNull(first.NextCursor);
        Assert.Equal([idHigh, idLow], first.Items.Select(x => x.ImportBatchId).ToArray());

        var second = await workflow.ListAsync(db, tenant, cursor: first.NextCursor, limit: 2);
        Assert.Equal(2, second.Limit);
        Assert.False(second.HasMore);
        Assert.Null(second.NextCursor);
        var only = Assert.Single(second.Items);
        Assert.Equal(idOlder, only.ImportBatchId);
        Assert.Equal([2, 3], only.Rejections.Select(x => x.RowNumber).ToArray());
        Assert.DoesNotContain(first.Items.Concat(second.Items), x => x.ImportBatchId == idOtherTenant);
    }

    [Fact]
    public async Task ListAsync_rejects_system_or_mismatched_tenant_scope()
    {
        await using var database = await TestDatabase.CreateAsync();
        var workflow = new ImportWorkflow();
        var tenant = Member("tenant-a", "importer");

        await using var systemDb = database.Context(TenantScope.System);
        await Assert.ThrowsAsync<SecurityException>(() => workflow.ListAsync(systemDb, tenant));

        await using var otherDb = database.Context(TenantScope.For("tenant-b"));
        await Assert.ThrowsAsync<SecurityException>(() => workflow.ListAsync(otherDb, tenant));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task ListAsync_rejects_invalid_limit(int limit)
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context(TenantScope.For("tenant-a"));
        var workflow = new ImportWorkflow();

        var exception = await Assert.ThrowsAsync<ImportValidationException>(() =>
            workflow.ListAsync(db, Member("tenant-a", "importer"), cursor: null, limit: limit));

        Assert.Equal("invalid_limit", exception.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-valid-cursor")]
    [InlineData("e30")]
    public async Task ListAsync_rejects_malformed_cursor(string cursor)
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context(TenantScope.For("tenant-a"));
        var workflow = new ImportWorkflow();

        var exception = await Assert.ThrowsAsync<ImportValidationException>(() =>
            workflow.ListAsync(db, Member("tenant-a", "importer"), cursor: cursor, limit: 10));

        Assert.Equal("invalid_cursor", exception.Code);
    }

    [Fact]
    public async Task ListAsync_rejects_cross_tenant_cursor()
    {
        await using var database = await TestDatabase.CreateAsync();
        var now = new DateTimeOffset(2026, 2, 1, 12, 0, 0, TimeSpan.Zero);
        await using (var system = database.Context(TenantScope.System))
        {
            system.ImportBatches.AddRange(
                Batch("tenant-b", Guid.NewGuid(), now, "b1.csv", 1, 0, ImportContract.CompletedStatus),
                Batch("tenant-b", Guid.NewGuid(), now.AddMinutes(-1), "b2.csv", 1, 0, ImportContract.CompletedStatus));
            await system.SaveChangesAsync();
        }

        var workflow = new ImportWorkflow();
        await using var dbB = database.Context(TenantScope.For("tenant-b"));
        var pageB = await workflow.ListAsync(dbB, Member("tenant-b", "importer"), cursor: null, limit: 1);
        Assert.NotNull(pageB.NextCursor);

        await using var dbA = database.Context(TenantScope.For("tenant-a"));
        var exception = await Assert.ThrowsAsync<ImportValidationException>(() =>
            workflow.ListAsync(dbA, Member("tenant-a", "importer"), cursor: pageB.NextCursor, limit: 1));

        Assert.Equal("invalid_cursor", exception.Code);
    }

    [Fact]
    public async Task Endpoint_enforces_import_permission_and_returns_page_or_400()
    {
        await using var database = await TestDatabase.CreateAsync();
        var batchId = Guid.NewGuid();
        await using (var system = database.Context(TenantScope.System))
        {
            system.ImportBatches.Add(Batch(
                "tenant-a",
                batchId,
                new DateTimeOffset(2026, 2, 5, 9, 0, 0, TimeSpan.Zero),
                "metrics.csv",
                4,
                0,
                ImportContract.CompletedStatus));
            await system.SaveChangesAsync();
        }

        var forbidden = await InvokeGetImportsAsync(database, Member("tenant-a", "analyst"), string.Empty);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);

        var ok = await InvokeGetImportsAsync(database, Member("tenant-a", "tenant_admin"), "?limit=10");
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        using (var doc = JsonDocument.Parse(ok.Body))
        {
            var root = doc.RootElement;
            Assert.Equal(10, root.GetProperty("limit").GetInt32());
            Assert.False(root.GetProperty("hasMore").GetBoolean());
            Assert.Equal(JsonValueKind.Null, root.GetProperty("nextCursor").ValueKind);
            var item = Assert.Single(root.GetProperty("items").EnumerateArray());
            Assert.Equal(batchId, item.GetProperty("id").GetGuid());
            Assert.Equal("metrics.csv", item.GetProperty("fileName").GetString());
            Assert.Equal(4, item.GetProperty("acceptedRows").GetInt32());
            Assert.Equal(0, item.GetProperty("rejectedRows").GetInt32());
            Assert.Equal("completed", item.GetProperty("status").GetString());
            Assert.Empty(item.GetProperty("errors").EnumerateArray());
        }

        var invalidLimit = await InvokeGetImportsAsync(database, Member("tenant-a", "importer"), "?limit=101");
        Assert.Equal(StatusCodes.Status400BadRequest, invalidLimit.StatusCode);
        using (var doc = JsonDocument.Parse(invalidLimit.Body))
        {
            Assert.Equal("invalid_limit", doc.RootElement.GetProperty("code").GetString());
        }

        var invalidCursor = await InvokeGetImportsAsync(database, Member("tenant-a", "importer"), "?cursor=invalid");
        Assert.Equal(StatusCodes.Status400BadRequest, invalidCursor.StatusCode);
        using (var doc = JsonDocument.Parse(invalidCursor.Body))
        {
            Assert.Equal("invalid_cursor", doc.RootElement.GetProperty("code").GetString());
        }
    }

    private static async Task<(int StatusCode, string Body)> InvokeGetImportsAsync(
        TestDatabase database,
        TenantContext tenant,
        string queryString)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddScoped(_ => tenant);
        builder.Services.AddScoped(_ => database.Context(TenantScope.For(tenant.TenantId)));

        await using var app = builder.Build();
        app.MapImportEndpoints();

        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(e => string.Equals(e.RoutePattern.RawText?.TrimEnd('/'), ImportContract.Route.TrimEnd('/'), StringComparison.Ordinal)
                && e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains(HttpMethods.Get) == true);

        await using var scope = app.Services.CreateAsyncScope();
        var http = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider
        };
        http.Request.Method = HttpMethods.Get;
        http.Request.Path = ImportContract.Route;
        http.Request.QueryString = new QueryString(queryString);
        http.Response.Body = new MemoryStream();

        await endpoint.RequestDelegate!(http);
        http.Response.Body.Position = 0;
        using var reader = new StreamReader(http.Response.Body, Encoding.UTF8);
        return (http.Response.StatusCode, await reader.ReadToEndAsync());
    }

    private static TenantContext Member(string tenantId, string role) =>
        new(tenantId, "user-1", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { role });

    private static ImportBatch Batch(
        string tenantId,
        Guid id,
        DateTimeOffset uploadedAt,
        string fileName,
        int acceptedRows,
        int rejectedRows,
        string status) => new()
        {
            TenantId = tenantId,
            Id = id,
            UploadedBySubjectId = "user-1",
            UploadedAt = uploadedAt,
            FileName = fileName,
            AcceptedRows = acceptedRows,
            RejectedRows = rejectedRows,
            Status = status
        };

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(AuthenticateResult.NoResult());
    }

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
            var database = new TestDatabase(connection);
            await using var context = database.Context(TenantScope.System);
            await context.Database.EnsureCreatedAsync();
            context.Tenants.AddRange(
                new Tenant { Id = "tenant-a", Name = "Tenant A", CreatedAt = DateTimeOffset.UtcNow },
                new Tenant { Id = "tenant-b", Name = "Tenant B", CreatedAt = DateTimeOffset.UtcNow });
            await context.SaveChangesAsync();
            return database;
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
