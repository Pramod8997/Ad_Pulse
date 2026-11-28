using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AdPulse.Api.Alerts;
using AdPulse.Api.Scoring;
using AdPulse.Domain.Analytics;
using AdPulse.Domain.Tenancy;
using AdPulse.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AdPulse.Api.Tests.Scoring;

public sealed class ScoringEndpointsTests
{
    [Fact]
    public async Task Endpoint_requires_authorization_metadata()
    {
        await using var db = await TestDatabase.CreateAsync();
        var scorer = new StubScorer(_ => Task.FromResult(AlertResult("fp-1")));
        await using var host = await EndpointTestHost.CreateAsync(db, "tenant-a", scorer);

        Assert.Contains(host.Endpoint.Metadata, item => item is IAuthorizeData);
    }

    [Fact]
    public async Task Scores_campaign_computes_kpis_and_persists_alert_idempotently()
    {
        await using var db = await TestDatabase.CreateAsync();
        var campaignId = Guid.NewGuid();
        await db.SeedCampaignAsync("tenant-a", campaignId);
        await db.SeedMetricsAsync("tenant-a", campaignId,
            Metric(new DateOnly(2026, 1, 1), spend: 10m, revenue: 50m, impressions: 1000, clicks: 10, conversions: 2), // cpc=1.0
            Metric(new DateOnly(2026, 1, 2), spend: 30m, revenue: 60m, impressions: 1000, clicks: 10, conversions: 2), // cpc=3.0
            Metric(new DateOnly(2026, 1, 3), spend: 60m, revenue: 40m, impressions: 1000, clicks: 10, conversions: 2)); // latest cpc=6.0

        var fingerprint = new string('b', 64);
        var scorer = new StubScorer(_ => Task.FromResult(AlertResult(fingerprint)));
        await using var host = await EndpointTestHost.CreateAsync(db, "tenant-a", scorer);

        var first = await host.InvokeAsync(campaignId, Principal("tenant-a", "user-a"));
        Assert.Equal(StatusCodes.Status200OK, first.StatusCode);
        Assert.True(first.Body.RootElement.GetProperty("created").GetBoolean());
        Assert.Equal(fingerprint, first.Body.RootElement.GetProperty("alert").GetProperty("fingerprint").GetString());

        Assert.NotNull(scorer.LastRequest);
        Assert.Equal("tenant-a", scorer.LastRequest!.TenantId);
        Assert.Equal(campaignId.ToString("D"), scorer.LastRequest.CampaignId);
        Assert.Equal("cpc", scorer.LastRequest.MetricName);
        Assert.Equal(6m, scorer.LastRequest.Observed);
        Assert.Equal(2m, scorer.LastRequest.Baseline);
        Assert.Equal(3, scorer.LastRequest.DataPoints);
        Assert.Equal(MetricDirection.LowerIsBetter, scorer.LastRequest.Direction);
        Assert.Equal(new DateOnly(2026, 1, 1), scorer.LastRequest.EvidenceWindowStart);
        Assert.Equal(new DateOnly(2026, 1, 3), scorer.LastRequest.EvidenceWindowEnd);
        Assert.Equal("scoring-v1", scorer.LastRequest.ScoringVersion);

        var second = await host.InvokeAsync(campaignId, Principal("tenant-a", "user-a"));
        Assert.Equal(StatusCodes.Status200OK, second.StatusCode);
        Assert.False(second.Body.RootElement.GetProperty("created").GetBoolean());

        await using var verifyDb = db.Context(TenantScope.For("tenant-a"));
        Assert.Equal(1, await verifyDb.Alerts.CountAsync(a => a.CampaignId == campaignId));
    }

    [Fact]
    public async Task Supports_configurable_metric_name_and_higher_is_better_direction()
    {
        await using var db = await TestDatabase.CreateAsync();
        var campaignId = Guid.NewGuid();
        await db.SeedCampaignAsync("tenant-a", campaignId);
        await db.SeedMetricsAsync("tenant-a", campaignId,
            Metric(new DateOnly(2026, 1, 1), spend: 10m, revenue: 40m, impressions: 1000, clicks: 10, conversions: 2), // roas=4
            Metric(new DateOnly(2026, 1, 2), spend: 10m, revenue: 10m, impressions: 1000, clicks: 10, conversions: 2)); // roas=1

        var scorer = new StubScorer(_ => Task.FromResult(HealthyResult()));
        await using var host = await EndpointTestHost.CreateAsync(db, "tenant-a", scorer);

        var response = await host.InvokeAsync(campaignId, Principal("tenant-a", "user-a"), "{\"metricName\":\"roas\"}");
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.False(response.Body.RootElement.GetProperty("created").GetBoolean());
        Assert.Equal(JsonValueKind.Null, response.Body.RootElement.GetProperty("alert").ValueKind);

        Assert.Equal("roas", scorer.LastRequest!.MetricName);
        Assert.Equal(1m, scorer.LastRequest.Observed);
        Assert.Equal(4m, scorer.LastRequest.Baseline);
        Assert.Equal(MetricDirection.HigherIsBetter, scorer.LastRequest.Direction);
    }

    [Fact]
    public async Task Rejects_scoring_when_fewer_than_two_metrics_exist()
    {
        await using var db = await TestDatabase.CreateAsync();
        var emptyCampaignId = Guid.NewGuid();
        var singleMetricCampaignId = Guid.NewGuid();
        await db.SeedCampaignAsync("tenant-a", emptyCampaignId);
        await db.SeedCampaignAsync("tenant-a", singleMetricCampaignId);
        await db.SeedMetricsAsync("tenant-a", singleMetricCampaignId,
            Metric(new DateOnly(2026, 1, 1), spend: 10m, revenue: 20m, impressions: 100, clicks: 5, conversions: 1));

        var scorer = new StubScorer(_ => Task.FromResult(HealthyResult()));
        await using var host = await EndpointTestHost.CreateAsync(db, "tenant-a", scorer);

        var emptyResult = await host.InvokeAsync(emptyCampaignId, Principal("tenant-a", "user-a"));
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, emptyResult.StatusCode);
        Assert.Contains("At least two daily metric records are required to score a campaign.", emptyResult.RawBody);

        var singleResult = await host.InvokeAsync(singleMetricCampaignId, Principal("tenant-a", "user-a"));
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, singleResult.StatusCode);
        Assert.Contains("At least two daily metric records are required to score a campaign.", singleResult.RawBody);
        Assert.Equal(0, scorer.Calls);
    }

    [Fact]
    public async Task Rejects_zero_denominator_observed_kpi_instead_of_coercing_to_zero()
    {
        await using var db = await TestDatabase.CreateAsync();
        var campaignId = Guid.NewGuid();
        await db.SeedCampaignAsync("tenant-a", campaignId);
        await db.SeedMetricsAsync("tenant-a", campaignId,
            Metric(new DateOnly(2026, 1, 1), spend: 10m, revenue: 20m, impressions: 100, clicks: 5, conversions: 1),
            Metric(new DateOnly(2026, 1, 2), spend: 10m, revenue: 0m, impressions: 100, clicks: 0, conversions: 0));

        var scorer = new StubScorer(_ => Task.FromResult(HealthyResult()));
        await using var host = await EndpointTestHost.CreateAsync(db, "tenant-a", scorer);

        var result = await host.InvokeAsync(campaignId, Principal("tenant-a", "user-a"), "{\"metricName\":\"cpc\"}");
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.StatusCode);
        Assert.Equal(0, scorer.Calls);
    }

    [Fact]
    public async Task Rejects_invalid_metric_name()
    {
        await using var db = await TestDatabase.CreateAsync();
        var campaignId = Guid.NewGuid();
        await db.SeedCampaignAsync("tenant-a", campaignId);

        var scorer = new StubScorer(_ => Task.FromResult(HealthyResult()));
        await using var host = await EndpointTestHost.CreateAsync(db, "tenant-a", scorer);

        var result = await host.InvokeAsync(campaignId, Principal("tenant-a", "user-a"), "{\"metricName\":\"spend\"}");
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal(0, scorer.Calls);
    }

    [Fact]
    public async Task Enforces_tenant_isolation_and_membership()
    {
        await using var db = await TestDatabase.CreateAsync();
        var tenantBCampaignId = Guid.NewGuid();
        await db.SeedCampaignAsync("tenant-b", tenantBCampaignId);
        await db.SeedMetricsAsync("tenant-b", tenantBCampaignId,
            Metric(new DateOnly(2026, 1, 1), spend: 10m, revenue: 20m, impressions: 100, clicks: 5, conversions: 1),
            Metric(new DateOnly(2026, 1, 2), spend: 20m, revenue: 20m, impressions: 100, clicks: 5, conversions: 1));

        var scorer = new StubScorer(_ => Task.FromResult(AlertResult(new string('c', 64))));
        await using var host = await EndpointTestHost.CreateAsync(db, "tenant-a", scorer);

        // Tenant A cannot see Tenant B's campaign -> 404.
        var crossTenant = await host.InvokeAsync(tenantBCampaignId, Principal("tenant-a", "user-a"));
        Assert.Equal(StatusCodes.Status404NotFound, crossTenant.StatusCode);
        Assert.Equal(0, scorer.Calls);

        // Unauthenticated / non-member identity -> 403.
        var nonMember = await host.InvokeAsync(tenantBCampaignId, Principal("tenant-a", "intruder"));
        Assert.Equal(StatusCodes.Status403Forbidden, nonMember.StatusCode);
        Assert.Equal(0, scorer.Calls);
    }

    [Fact]
    public async Task Maps_scoring_client_and_http_failures_to_502_without_leaking_details()
    {
        await using var db = await TestDatabase.CreateAsync();
        var campaignId = Guid.NewGuid();
        await db.SeedCampaignAsync("tenant-a", campaignId);
        await db.SeedMetricsAsync("tenant-a", campaignId,
            Metric(new DateOnly(2026, 1, 1), spend: 10m, revenue: 20m, impressions: 100, clicks: 5, conversions: 1),
            Metric(new DateOnly(2026, 1, 2), spend: 30m, revenue: 20m, impressions: 100, clicks: 5, conversions: 1));

        var scorer = new StubScorer(_ => throw new ScoringClientException("http://internal-scorer:8000/v1/score secret-token", HttpStatusCode.ServiceUnavailable));
        await using var host = await EndpointTestHost.CreateAsync(db, "tenant-a", scorer);

        var result = await host.InvokeAsync(campaignId, Principal("tenant-a", "user-a"));
        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
        Assert.DoesNotContain("internal-scorer", result.RawBody, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token", result.RawBody, StringComparison.Ordinal);

        scorer.Behavior = _ => throw new HttpRequestException("connection refused internal-host");
        var httpFailure = await host.InvokeAsync(campaignId, Principal("tenant-a", "user-a"));
        Assert.Equal(StatusCodes.Status502BadGateway, httpFailure.StatusCode);
        Assert.DoesNotContain("internal-host", httpFailure.RawBody, StringComparison.Ordinal);
    }

    private static ClaimsPrincipal Principal(string tenantId, string subjectId) => new(new ClaimsIdentity(
        [new Claim("sub", subjectId), new Claim("tenant_id", tenantId)],
        "test"));

    private static DailyMetric Metric(
        DateOnly date,
        decimal spend,
        decimal revenue,
        long impressions,
        long clicks,
        long conversions) => new()
        {
            MetricDate = date,
            Spend = spend,
            Revenue = revenue,
            Impressions = impressions,
            Clicks = clicks,
            Conversions = conversions
        };

    private static ScoringResult AlertResult(string fingerprint) => new(
        80m,
        ScoringSeverity.Critical,
        0.5m,
        "sufficient",
        ["degraded_vs_baseline"],
        fingerprint);

    private static ScoringResult HealthyResult() => new(
        0m,
        ScoringSeverity.None,
        0.5m,
        "sufficient",
        ["within_baseline"],
        new string('a', 64));

    private sealed class StubScorer(Func<ScoringRequest, Task<ScoringResult>> behavior) : IPythonScoringClient
    {
        public Func<ScoringRequest, Task<ScoringResult>> Behavior { get; set; } = behavior;
        public int Calls { get; private set; }
        public ScoringRequest? LastRequest { get; private set; }

        public Task<ScoringResult> ScoreAsync(ScoringRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequest = request;
            return Behavior(request);
        }
    }

    private sealed class EndpointTestHost : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly string _tenantId;
        private readonly TestDatabase _database;

        public RouteEndpoint Endpoint { get; }

        private EndpointTestHost(WebApplication app, RouteEndpoint endpoint, string tenantId, TestDatabase database)
        {
            _app = app;
            Endpoint = endpoint;
            _tenantId = tenantId;
            _database = database;
        }

        public static async Task<EndpointTestHost> CreateAsync(
            TestDatabase database,
            string scopeTenantId,
            IPythonScoringClient scorer)
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.Services.AddAuthorization();
            builder.Services.AddLogging();
            builder.Services.AddScoped(_ => database.Context(TenantScope.For(scopeTenantId)));
            builder.Services.AddScoped<ITenantMembershipStore, EfTenantMembershipStore>();
            builder.Services.AddScoped<TenantContextResolver>();
            builder.Services.AddScoped<AlertService>();
            builder.Services.AddSingleton(scorer);
            builder.Services.AddScoped<ScoringAlertService>();

            var app = builder.Build();
            app.MapScoringEndpoints();

            var endpoint = ((IEndpointRouteBuilder)app).DataSources
                .SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>()
                .Single(e => e.RoutePattern.RawText == "/app/campaigns/{campaignId:guid}/score");

            return await Task.FromResult(new EndpointTestHost(app, endpoint, scopeTenantId, database));
        }

        public async Task<(int StatusCode, string RawBody, JsonDocument Body)> InvokeAsync(
            Guid campaignId,
            ClaimsPrincipal user,
            string? jsonBody = null)
        {
            await using var scope = _app.Services.CreateAsyncScope();
            var context = new DefaultHttpContext
            {
                RequestServices = scope.ServiceProvider,
                User = user
            };
            context.Request.Method = HttpMethods.Post;
            context.Request.Path = $"/app/campaigns/{campaignId:D}/score";
            context.Request.RouteValues["campaignId"] = campaignId.ToString("D");

            if (jsonBody is not null)
            {
                var bytes = Encoding.UTF8.GetBytes(jsonBody);
                context.Request.ContentType = "application/json";
                context.Request.ContentLength = bytes.Length;
                context.Request.Body = new MemoryStream(bytes);
            }

            context.Response.Body = new MemoryStream();
            await Endpoint.RequestDelegate!(context);
            context.Response.Body.Position = 0;

            using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync();
            var document = string.IsNullOrWhiteSpace(rawBody)
                ? JsonDocument.Parse("{}")
                : JsonDocument.Parse(rawBody);
            return (context.Response.StatusCode, rawBody, document);
        }

        public ValueTask DisposeAsync() => _app.DisposeAsync();
    }

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
            system.Memberships.AddRange(
                new Membership { TenantId = "tenant-a", SubjectId = "user-a", Role = "analyst" },
                new Membership { TenantId = "tenant-b", SubjectId = "user-b", Role = "analyst" });
            await system.SaveChangesAsync();
            return db;
        }

        public async Task SeedCampaignAsync(string tenantId, Guid campaignId)
        {
            await using var system = Context(TenantScope.System);
            system.Campaigns.Add(new Campaign
            {
                TenantId = tenantId,
                Id = campaignId,
                Name = $"Campaign {tenantId}",
                Status = "active",
                Currency = "USD",
                Budget = 1000m,
                StartsOn = new DateOnly(2026, 1, 1),
                EndsOn = new DateOnly(2026, 1, 31),
                CreatedAt = DateTimeOffset.UtcNow
            });
            await system.SaveChangesAsync();
        }

        public async Task SeedMetricsAsync(string tenantId, Guid campaignId, params DailyMetric[] metrics)
        {
            await using var system = Context(TenantScope.System);
            foreach (var metric in metrics)
            {
                metric.TenantId = tenantId;
                metric.CampaignId = campaignId;
                system.DailyMetrics.Add(metric);
            }
            await system.SaveChangesAsync();
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
