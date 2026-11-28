using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using AdPulse.Api.Audit;
using AdPulse.Api.Settings;
using AdPulse.Domain.Tenancy;
using AdPulse.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdPulse.Api.Tests.AuditSettings;

public sealed class AuditSettingsEndpointTests
{
    [Theory]
    [InlineData("audit", true)]
    [InlineData("auditor", true)]
    [InlineData("tenant-admin", true)]
    [InlineData("tenant_admin", true)]
    [InlineData("analyst", false)]
    [InlineData("importer", false)]
    public async Task Audit_endpoint_enforces_audit_roles(string role, bool allowed)
    {
        await using var database = await TestDatabase.CreateAsync();
        await using (var system = database.Context(TenantScope.System))
        {
            system.AuditEvents.Add(new AuditEvent
            {
                TenantId = "tenant-a",
                Id = Guid.NewGuid(),
                OccurredAt = new DateTimeOffset(2026, 2, 1, 10, 0, 0, TimeSpan.Zero),
                ActorSubjectId = "actor-1",
                Action = "campaign.created",
                EntityType = "Campaign",
                EntityId = "c-1",
                PayloadJson = "{}"
            });
            await system.SaveChangesAsync();
        }

        var response = await InvokeEndpointAsync(
            database,
            Member("tenant-a", role),
            HttpMethods.Get,
            "/app/audit");

        Assert.Equal(allowed ? StatusCodes.Status200OK : StatusCodes.Status403Forbidden, response.StatusCode);
        if (allowed)
        {
            using var doc = JsonDocument.Parse(response.Body);
            Assert.Single(doc.RootElement.GetProperty("items").EnumerateArray());
        }
    }

    [Theory]
    [InlineData("?limit=0")]
    [InlineData("?limit=101")]
    [InlineData("?cursor=not-valid")]
    [InlineData("?from=2026-02-10&to=2026-02-01")]
    [InlineData("?to=9999-12-31")]
    public async Task Audit_endpoint_maps_invalid_query_parameters_to_400_validation_problem(string queryString)
    {
        await using var database = await TestDatabase.CreateAsync();

        var response = await InvokeEndpointAsync(
            database,
            Member("tenant-a", "audit"),
            HttpMethods.Get,
            "/app/audit",
            queryString);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("tenant-admin")]
    [InlineData("tenant_admin")]
    public async Task Settings_service_and_endpoints_allow_both_admin_role_formats(string role)
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new InMemorySettingsStore();
        var tenant = Member("tenant-a", role);

        var missing = await InvokeEndpointAsync(database, tenant, HttpMethods.Get, "/app/settings", settingsStore: store);
        Assert.Equal(StatusCodes.Status404NotFound, missing.StatusCode);

        var updated = await InvokeEndpointAsync(
            database,
            tenant,
            HttpMethods.Patch,
            "/app/settings",
            body: "{\"reportingTimeZone\":\"UTC\",\"defaultCurrency\":\"USD\"}",
            settingsStore: store);
        Assert.True(updated.StatusCode == StatusCodes.Status200OK, $"Expected 200 but got {updated.StatusCode}: {updated.Body}");

        var loaded = await InvokeEndpointAsync(database, tenant, HttpMethods.Get, "/app/settings", settingsStore: store);
        Assert.Equal(StatusCodes.Status200OK, loaded.StatusCode);
        using var doc = JsonDocument.Parse(loaded.Body);
        Assert.Equal("UTC", doc.RootElement.GetProperty("reportingTimeZone").GetString());
        Assert.Equal("USD", doc.RootElement.GetProperty("defaultCurrency").GetString());
    }

    [Theory]
    [InlineData("analyst")]
    [InlineData("audit")]
    [InlineData("importer")]
    public async Task Settings_endpoints_return_403_for_non_admins(string role)
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new InMemorySettingsStore();
        await store.SaveAsync(new TenantReportingSettings
        {
            TenantId = "tenant-a",
            ReportingTimeZone = "UTC",
            DefaultCurrency = "USD",
            UpdatedAtUtc = DateTimeOffset.UtcNow
        }, CancellationToken.None);

        var tenant = Member("tenant-a", role);

        var getResponse = await InvokeEndpointAsync(database, tenant, HttpMethods.Get, "/app/settings", settingsStore: store);
        Assert.Equal(StatusCodes.Status403Forbidden, getResponse.StatusCode);

        var patchResponse = await InvokeEndpointAsync(
            database,
            tenant,
            HttpMethods.Patch,
            "/app/settings",
            body: "{\"reportingTimeZone\":\"UTC\",\"defaultCurrency\":\"USD\"}",
            settingsStore: store);
        Assert.Equal(StatusCodes.Status403Forbidden, patchResponse.StatusCode);
    }

    [Fact]
    public async Task Settings_patch_maps_invalid_values_to_400_validation_problem()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new InMemorySettingsStore();
        var tenant = Member("tenant-a", "tenant_admin");

        var invalidCurrency = await InvokeEndpointAsync(
            database,
            tenant,
            HttpMethods.Patch,
            "/app/settings",
            body: "{\"reportingTimeZone\":\"UTC\",\"defaultCurrency\":\"usd\"}",
            settingsStore: store);
        Assert.Equal(StatusCodes.Status400BadRequest, invalidCurrency.StatusCode);

        var invalidTimeZone = await InvokeEndpointAsync(
            database,
            tenant,
            HttpMethods.Patch,
            "/app/settings",
            body: "{\"reportingTimeZone\":\"Invalid/Zone\",\"defaultCurrency\":\"USD\"}",
            settingsStore: store);
        Assert.Equal(StatusCodes.Status400BadRequest, invalidTimeZone.StatusCode);
    }

    private static async Task<(int StatusCode, string Body)> InvokeEndpointAsync(
        TestDatabase database,
        TenantContext tenant,
        string method,
        string path,
        string queryString = "",
        string? body = null,
        ITenantSettingsStore? settingsStore = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddScoped(_ => tenant);
        builder.Services.AddScoped(_ => database.Context(TenantScope.For(tenant.TenantId)));
        builder.Services.AddAuditServices();
        builder.Services.AddTenantSettingsServices();
        if (settingsStore is not null)
        {
            builder.Services.AddSingleton(settingsStore);
        }

        await using var app = builder.Build();
        app.MapAuditEndpoints();
        app.MapTenantSettingsEndpoints();

        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(e => string.Equals(e.RoutePattern.RawText?.TrimEnd('/'), path.TrimEnd('/'), StringComparison.Ordinal)
                && e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains(method) == true);

        await using var scope = app.Services.CreateAsyncScope();
        var http = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider
        };
        http.Request.Method = method;
        http.Request.Path = path;
        http.Request.QueryString = new QueryString(queryString);
        if (body is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            http.Features.Set<IHttpRequestBodyDetectionFeature>(new TestRequestBodyDetectionFeature(true));
            http.Request.Body = new MemoryStream(bytes);
            http.Request.ContentType = "application/json";
            http.Request.ContentLength = bytes.Length;
        }

        http.Response.Body = new MemoryStream();

        await endpoint.RequestDelegate!(http);
        http.Response.Body.Position = 0;
        using var reader = new StreamReader(http.Response.Body, Encoding.UTF8);
        return (http.Response.StatusCode, await reader.ReadToEndAsync());
    }

    private static TenantContext Member(string tenantId, string role) =>
        new(tenantId, "user-1", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { role });

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
            context.Tenants.Add(new Tenant { Id = "tenant-a", Name = "Tenant A", CreatedAt = DateTimeOffset.UtcNow });
            await context.SaveChangesAsync();
            return database;
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }

    private sealed class TestRequestBodyDetectionFeature(bool canHaveBody) : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => canHaveBody;
    }

    private sealed class InMemorySettingsStore : ITenantSettingsStore
    {
        private readonly Dictionary<string, TenantReportingSettings> _items = new(StringComparer.Ordinal);

        public Task<TenantReportingSettings?> FindAsync(string tenantId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_items.GetValueOrDefault(tenantId));
        }

        public Task SaveAsync(TenantReportingSettings settings, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _items[settings.TenantId] = settings;
            return Task.CompletedTask;
        }
    }
}
