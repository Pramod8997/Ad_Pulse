using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;

namespace AdPulse.Api.Tests.WorkflowIntegration;

public sealed class Phase2WorkflowContractTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Authenticated_tenant_A_cannot_read_or_mutate_tenant_B_campaigns()
    {
        using var handler = new WorkflowApiContractDouble();
        using var tenantA = handler.Client("tenant-a");
        using var tenantB = handler.Client("tenant-b");

        var campaignA = await CreateCampaignAsync(tenantA, "Tenant A campaign");
        var campaignB = await CreateCampaignAsync(tenantB, "Tenant B campaign");

        var listA = await tenantA.GetAsync("/app/campaigns?tenantId=tenant-b");
        var listB = await tenantB.GetAsync("/app/campaigns");
        Assert.Equal(HttpStatusCode.OK, listA.StatusCode);
        Assert.Equal(HttpStatusCode.OK, listB.StatusCode);
        Assert.Contains(campaignA.Name, await listA.Content.ReadAsStringAsync());
        Assert.DoesNotContain(campaignB.Name, await listA.Content.ReadAsStringAsync());
        Assert.Contains(campaignB.Name, await listB.Content.ReadAsStringAsync());
        Assert.DoesNotContain(campaignA.Name, await listB.Content.ReadAsStringAsync());

        var crossTenantRead = await tenantB.GetAsync($"/app/campaigns/{campaignA.Id}");
        Assert.Equal(HttpStatusCode.NotFound, crossTenantRead.StatusCode);

        var crossTenantMutation = await tenantB.PatchJsonAsync(
            $"/app/campaigns/{campaignA.Id}", CampaignWrite("must-not-cross-tenant"));
        Assert.Equal(HttpStatusCode.NotFound, crossTenantMutation.StatusCode);

        var tenantOverride = await tenantB.GetAsync("/app/campaigns?tenantId=tenant-a");
        Assert.Equal(HttpStatusCode.OK, tenantOverride.StatusCode);
        Assert.DoesNotContain(campaignA.Name, await tenantOverride.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Campaign_contract_supports_create_read_update_and_list_paging()
    {
        using var handler = new WorkflowApiContractDouble();
        using var client = handler.Client("tenant-a");

        var created = await CreateCampaignAsync(client, "CRUD campaign");
        var read = await client.GetAsync($"/app/campaigns/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(created.Id, await ReadGuidAsync(read, "id"));

        var updated = await client.PatchJsonAsync(
            $"/app/campaigns/{created.Id}", CampaignWrite("CRUD campaign updated", "paused", 125.50m));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("paused", await ReadStringAsync(updated, "status"));
        Assert.Equal("CRUD campaign updated", await ReadStringAsync(updated, "name"));

        _ = await CreateCampaignAsync(client, "Second campaign");
        var page = await client.GetAsync("/app/campaigns?limit=1");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal(1, await ReadItemsCountAsync(page));
        Assert.True(await ReadBooleanAsync(page, "hasMore"));
        Assert.NotNull(await ReadNullableStringAsync(page, "nextCursor"));

        // The frozen v2 contract uses status=completed instead of a DELETE route.
        var delete = await client.DeleteAsync($"/app/campaigns/{created.Id}");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, delete.StatusCode);
    }

    [Fact]
    public async Task Repeated_metric_put_for_one_campaign_day_is_idempotent()
    {
        using var handler = new WorkflowApiContractDouble();
        using var client = handler.Client("tenant-a");
        var campaign = await CreateCampaignAsync(client, "Metric campaign");
        var metric = new
        {
            spend = 10.2500m,
            revenue = 42.0000m,
            impressions = 1000,
            clicks = 100,
            conversions = 4
        };
        var route = $"/app/campaigns/{campaign.Id}/metrics/2026-02-01";

        var first = await client.PutJsonAsync(route, metric);
        var retry = await client.PutJsonAsync(route, metric);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(await ReadGuidAsync(first, "campaignId"), await ReadGuidAsync(retry, "campaignId"));

        var list = await client.GetAsync($"/app/campaigns/{campaign.Id}/metrics?limit=100");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(1, await ReadItemsCountAsync(list));
    }

    [Fact]
    public async Task Import_contract_returns_bounded_row_errors_without_raw_uploaded_rows()
    {
        using var handler = new WorkflowApiContractDouble();
        using var client = handler.Client("tenant-a");
        const string rawRow = "do-not-return-this-uploaded-row";
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(
            "campaign_id,metric_date,currency,spend,revenue,impressions,clicks,conversions\n" +
            $"not-a-guid,not-a-date,USD,{rawRow},1,10,1,0\n"), "file", "metrics.csv");

        var upload = await client.PostAsync("/app/imports", form);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var uploadBody = await upload.Content.ReadAsStringAsync();
        Assert.DoesNotContain(rawRow, uploadBody);
        Assert.Equal(1, await ReadIntAsync(upload, "rejectedRows"));
        Assert.Equal("failed", await ReadStringAsync(upload, "status"));
        Assert.Equal(1, await ReadItemsCountAsync(upload, "errors"));

        var importId = await ReadGuidAsync(upload, "id");
        var details = await client.GetAsync($"/app/imports/{importId}");
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        Assert.DoesNotContain(rawRow, await details.Content.ReadAsStringAsync());
        Assert.Equal("invalid_row", await ReadStringAsync(details, "errors", 0, "errorCode"));
    }

    [Fact]
    public async Task Alert_lifecycle_is_tenant_scoped_and_requires_a_reason_to_resolve()
    {
        using var handler = new WorkflowApiContractDouble();
        using var tenantA = handler.Client("tenant-a");
        using var tenantB = handler.Client("tenant-b");
        var alertId = await ReadFirstIdAsync(await tenantA.GetAsync("/app/alerts?status=open"));

        var missingReason = await tenantA.PatchJsonAsync(
            $"/app/alerts/{alertId}", new { status = "resolved" });
        Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);

        var acknowledge = await tenantA.PatchJsonAsync(
            $"/app/alerts/{alertId}", new { status = "acknowledged" });
        Assert.Equal(HttpStatusCode.OK, acknowledge.StatusCode);
        Assert.Equal("acknowledged", await ReadStringAsync(acknowledge, "status"));

        var crossTenant = await tenantB.PatchJsonAsync(
            $"/app/alerts/{alertId}", new { status = "resolved", reason = "must-not-cross-tenant" });
        Assert.Equal(HttpStatusCode.NotFound, crossTenant.StatusCode);

        var resolve = await tenantA.PatchJsonAsync(
            $"/app/alerts/{alertId}", new { status = "resolved", reason = "reviewed" });
        Assert.Equal(HttpStatusCode.OK, resolve.StatusCode);
        Assert.Equal("resolved", await ReadStringAsync(resolve, "status"));
    }

    [Fact]
    public async Task Audit_contract_is_read_only_and_tenant_scoped()
    {
        using var handler = new WorkflowApiContractDouble();
        using var tenantA = handler.Client("tenant-a");
        using var tenantB = handler.Client("tenant-b");

        var auditA = await tenantA.GetAsync("/app/audit");
        var auditB = await tenantB.GetAsync("/app/audit");
        Assert.Equal(HttpStatusCode.OK, auditA.StatusCode);
        Assert.Equal(HttpStatusCode.OK, auditB.StatusCode);
        Assert.Contains("user-tenant-a", await auditA.Content.ReadAsStringAsync());
        Assert.DoesNotContain("user-tenant-b", await auditA.Content.ReadAsStringAsync());
        Assert.Contains("user-tenant-b", await auditB.Content.ReadAsStringAsync());
        Assert.DoesNotContain("user-tenant-a", await auditB.Content.ReadAsStringAsync());

        using var body = JsonContent(new { action = "audit.injected" });
        var create = await tenantA.PostAsync("/app/audit", body);
        var update = await tenantA.PatchJsonAsync("/app/audit", new { action = "audit.tampered" });
        var delete = await tenantA.DeleteAsync("/app/audit");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, create.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, update.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, delete.StatusCode);
    }

    [Fact]
    public async Task Workflow_requests_require_a_bearer_token()
    {
        using var handler = new WorkflowApiContractDouble();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };

        var response = await client.GetAsync("/app/campaigns");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static CampaignWire CampaignWrite(
        string name,
        string status = "active",
        decimal budget = 100m) => new(name, status, "USD", budget, "2026-01-01", "2026-01-31");

    private static async Task<CampaignWireResponse> CreateCampaignAsync(HttpClient client, string name)
    {
        var response = await client.PostAsync(
            "/app/campaigns", JsonContent(CampaignWrite(name)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return new CampaignWireResponse(
            await ReadGuidAsync(response, "id"),
            await ReadStringAsync(response, "name"));
    }

    private static StringContent JsonContent<T>(T value) => new(
        JsonSerializer.Serialize(value, Json), Encoding.UTF8, "application/json");

    private static async Task<JsonElement> RootAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static async Task<Guid> ReadGuidAsync(HttpResponseMessage response, string property, int index = -1)
    {
        var value = await ReadPropertyAsync(response, property, index);
        Assert.True(Guid.TryParse(value.GetString(), out var id), $"{property} must be a GUID.");
        return id;
    }

    private static async Task<string> ReadStringAsync(
        HttpResponseMessage response,
        string property,
        int index = -1,
        string? nestedProperty = null)
    {
        var value = await ReadPropertyAsync(response, property, index, nestedProperty);
        Assert.Equal(JsonValueKind.String, value.ValueKind);
        return value.GetString()!;
    }

    private static async Task<string?> ReadNullableStringAsync(HttpResponseMessage response, string property)
    {
        var value = await ReadPropertyAsync(response, property);
        return value.ValueKind == JsonValueKind.Null ? null : value.GetString();
    }

    private static async Task<int> ReadIntAsync(HttpResponseMessage response, string property)
    {
        var value = await ReadPropertyAsync(response, property);
        Assert.True(value.TryGetInt32(out var number));
        return number;
    }

    private static async Task<bool> ReadBooleanAsync(HttpResponseMessage response, string property)
    {
        var value = await ReadPropertyAsync(response, property);
        Assert.True(value.ValueKind is JsonValueKind.True or JsonValueKind.False);
        return value.GetBoolean();
    }

    private static async Task<int> ReadItemsCountAsync(HttpResponseMessage response, string property = "items")
    {
        var value = await ReadPropertyAsync(response, property);
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        return value.GetArrayLength();
    }

    private static async Task<Guid> ReadFirstIdAsync(HttpResponseMessage response)
    {
        var value = await ReadPropertyAsync(response, "items", 0, "id");
        Assert.True(Guid.TryParse(value.GetString(), out var id));
        return id;
    }

    private static async Task<JsonElement> ReadPropertyAsync(
        HttpResponseMessage response,
        string property,
        int index = -1,
        string? nestedProperty = null)
    {
        var root = await RootAsync(response);
        var value = root.GetProperty(property);
        if (index >= 0)
        {
            value = value[index];
        }

        return nestedProperty is null ? value : value.GetProperty(nestedProperty);
    }

    private sealed record CampaignWire(
        string name,
        string status,
        string currency,
        decimal budget,
        string startsOn,
        string endsOn);

    private sealed record CampaignWireResponse(Guid Id, string Name);
}

internal sealed class WorkflowApiContractDouble : HttpMessageHandler
{
    private readonly Dictionary<string, WorkflowTenant> _tenants = new(StringComparer.Ordinal)
    {
        ["tenant-a"] = WorkflowTenant.Create("tenant-a"),
        ["tenant-b"] = WorkflowTenant.Create("tenant-b")
    };

    public HttpClient Client(string tenant)
    {
        var client = new HttpClient(this, disposeHandler: false)
        {
            BaseAddress = new Uri("https://api.test")
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenant);
        return client;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = request.Headers.Authorization?.Scheme == "Bearer"
            ? request.Headers.Authorization.Parameter
            : null;
        if (token is null || !_tenants.TryGetValue(token, out var tenant))
        {
            return Response(HttpStatusCode.Unauthorized, new { });
        }

        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        if (path.Equals("/app/campaigns", StringComparison.Ordinal))
        {
            return await CampaignsAsync(request, tenant, cancellationToken);
        }

        if (path.StartsWith("/app/campaigns/", StringComparison.Ordinal))
        {
            return await CampaignResourceAsync(request, tenant, path, cancellationToken);
        }

        if (path.Equals("/app/imports", StringComparison.Ordinal)
            || path.StartsWith("/app/imports/", StringComparison.Ordinal))
        {
            return await ImportsAsync(request, tenant, path, cancellationToken);
        }

        if (path.Equals("/app/alerts", StringComparison.Ordinal)
            || path.StartsWith("/app/alerts/", StringComparison.Ordinal))
        {
            return await AlertsAsync(request, tenant, path, cancellationToken);
        }

        if (path.Equals("/app/audit", StringComparison.Ordinal))
        {
            return request.Method == HttpMethod.Get
                ? Page(tenant.Audit, 25)
                : Response(HttpStatusCode.MethodNotAllowed, new { });
        }

        return Response(HttpStatusCode.NotFound, new { });
    }

    private static async Task<HttpResponseMessage> CampaignsAsync(
        HttpRequestMessage request,
        WorkflowTenant tenant,
        CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Post)
        {
            var body = await JsonDocument.ParseAsync(await request.Content!.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            if (body.RootElement.TryGetProperty("tenantId", out _))
            {
                return Response(HttpStatusCode.BadRequest, new { code = "tenant_scope_forbidden" });
            }

            var campaign = new WorkflowCampaign(
                Guid.NewGuid(),
                body.RootElement.GetProperty("name").GetString()!,
                body.RootElement.GetProperty("status").GetString()!,
                body.RootElement.GetProperty("currency").GetString()!,
                body.RootElement.GetProperty("budget").GetDecimal(),
                body.RootElement.GetProperty("startsOn").GetString()!,
                body.RootElement.GetProperty("endsOn").GetString()!,
                DateTimeOffset.UtcNow);
            tenant.Campaigns[campaign.Id] = campaign;
            tenant.Audit.Add(new { id = Guid.NewGuid(), actorSubjectId = $"user-{tenant.Id}", action = "campaign.created" });
            return Response(HttpStatusCode.Created, campaign);
        }

        if (request.Method == HttpMethod.Get)
        {
            var limit = QueryLimit(request.RequestUri?.Query);
            return Page(tenant.Campaigns.Values.Select(c => (object)c).ToArray(), limit);
        }

        return Response(HttpStatusCode.MethodNotAllowed, new { });
    }

    private static async Task<HttpResponseMessage> CampaignResourceAsync(
        HttpRequestMessage request,
        WorkflowTenant tenant,
        string path,
        CancellationToken cancellationToken)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 4 && segments[3].Equals("metrics", StringComparison.Ordinal))
        {
            return await MetricsAsync(request, tenant, segments, cancellationToken);
        }

        if (!Guid.TryParse(segments.ElementAtOrDefault(2), out var id)
            || !tenant.Campaigns.TryGetValue(id, out var campaign))
        {
            return Response(HttpStatusCode.NotFound, new { });
        }

        if (request.Method == HttpMethod.Get)
        {
            return Response(HttpStatusCode.OK, campaign);
        }

        if (request.Method == HttpMethod.Patch)
        {
            var body = await JsonDocument.ParseAsync(await request.Content!.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var updated = campaign with
            {
                Name = body.RootElement.GetProperty("name").GetString()!,
                Status = body.RootElement.GetProperty("status").GetString()!,
                Currency = body.RootElement.GetProperty("currency").GetString()!,
                Budget = body.RootElement.GetProperty("budget").GetDecimal(),
                StartsOn = body.RootElement.GetProperty("startsOn").GetString()!,
                EndsOn = body.RootElement.GetProperty("endsOn").GetString()!
            };
            tenant.Campaigns[id] = updated;
            tenant.Audit.Add(new { id = Guid.NewGuid(), actorSubjectId = $"user-{tenant.Id}", action = "campaign.updated" });
            return Response(HttpStatusCode.OK, updated);
        }

        return Response(HttpStatusCode.MethodNotAllowed, new { });
    }

    private static async Task<HttpResponseMessage> MetricsAsync(
        HttpRequestMessage request,
        WorkflowTenant tenant,
        IReadOnlyList<string> segments,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(segments.ElementAtOrDefault(2), out var campaignId)
            || !tenant.Campaigns.ContainsKey(campaignId))
        {
            return Response(HttpStatusCode.NotFound, new { });
        }

        if (request.Method == HttpMethod.Get)
        {
            return Page(tenant.Metrics.Values.Where(metric => metric.CampaignId == campaignId).Cast<object>().ToArray(), 100);
        }

        if (request.Method == HttpMethod.Put && segments.Count == 5)
        {
            var date = segments[4];
            var body = await JsonDocument.ParseAsync(await request.Content!.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var metric = new WorkflowMetric(
                campaignId,
                date,
                body.RootElement.GetProperty("spend").GetDecimal(),
                body.RootElement.GetProperty("revenue").GetDecimal(),
                body.RootElement.GetProperty("impressions").GetInt64(),
                body.RootElement.GetProperty("clicks").GetInt64(),
                body.RootElement.GetProperty("conversions").GetInt64());
            tenant.Metrics[(campaignId, date)] = metric;
            return Response(HttpStatusCode.OK, metric);
        }

        return Response(HttpStatusCode.MethodNotAllowed, new { });
    }

    private static async Task<HttpResponseMessage> ImportsAsync(
        HttpRequestMessage request,
        WorkflowTenant tenant,
        string path,
        CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Post && path.Equals("/app/imports", StringComparison.Ordinal))
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (!body.Contains("name=\"file\"", StringComparison.Ordinal)
                && !body.Contains("name=file", StringComparison.Ordinal))
            {
                return Response(HttpStatusCode.BadRequest, new { code = "file_required" });
            }

            var import = new WorkflowImport(
                Guid.NewGuid(),
                "metrics.csv",
                DateTimeOffset.UtcNow,
                0,
                1,
                "failed",
                [new { rowNumber = 2, errorCode = "invalid_row", message = "The metric row is invalid." }]);
            tenant.Imports[import.Id] = import;
            return Response(HttpStatusCode.OK, import);
        }

        if (request.Method == HttpMethod.Get && Guid.TryParse(path.Split('/').Last(), out var id)
            && tenant.Imports.TryGetValue(id, out var existing))
        {
            return Response(HttpStatusCode.OK, existing);
        }

        return Response(HttpStatusCode.MethodNotAllowed, new { });
    }

    private static async Task<HttpResponseMessage> AlertsAsync(
        HttpRequestMessage request,
        WorkflowTenant tenant,
        string path,
        CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Get && path.Equals("/app/alerts", StringComparison.Ordinal))
        {
            return Page(tenant.Alerts.Values.Cast<object>().ToArray(), 25);
        }

        if (request.Method == HttpMethod.Patch && Guid.TryParse(path.Split('/').Last(), out var id)
            && tenant.Alerts.TryGetValue(id, out var alert))
        {
            var body = await JsonDocument.ParseAsync(await request.Content!.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var status = body.RootElement.GetProperty("status").GetString()!;
            if (status == "resolved"
                && (!body.RootElement.TryGetProperty("reason", out var reason) || string.IsNullOrWhiteSpace(reason.GetString())))
            {
                return Response(HttpStatusCode.BadRequest, new { code = "reason_required" });
            }

            if (status is not ("open" or "acknowledged" or "resolved"))
            {
                return Response(HttpStatusCode.BadRequest, new { code = "invalid_status" });
            }

            var updated = alert with { Status = status };
            tenant.Alerts[id] = updated;
            tenant.Audit.Add(new { id = Guid.NewGuid(), actorSubjectId = $"user-{tenant.Id}", action = $"alert.{status}" });
            return Response(HttpStatusCode.OK, updated);
        }

        if (path.StartsWith("/app/alerts/", StringComparison.Ordinal))
        {
            return Response(HttpStatusCode.NotFound, new { });
        }

        return Response(HttpStatusCode.MethodNotAllowed, new { });
    }

    private static int QueryLimit(string? query)
    {
        var value = query?.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(part => part.Length == 2 && part[0].TrimStart('?') == "limit")
            .Select(part => int.TryParse(part[1], out var limit) ? limit : 25)
            .FirstOrDefault() ?? 25;
        return value is < 1 or > 100 ? 25 : value;
    }

    private static HttpResponseMessage Page(IEnumerable<object> items, int limit)
    {
        var all = items.ToArray();
        var page = all.Take(limit).ToArray();
        return Response(HttpStatusCode.OK, new
        {
            items = page,
            nextCursor = all.Length > limit ? "cursor-1" : null,
            hasMore = all.Length > limit,
            limit
        });
    }

    private static HttpResponseMessage Response(HttpStatusCode status, object body) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }), Encoding.UTF8, "application/json")
    };

    private sealed class WorkflowTenant
    {
        public required string Id { get; init; }
        public Dictionary<Guid, WorkflowCampaign> Campaigns { get; } = [];
        public Dictionary<(Guid CampaignId, string Date), WorkflowMetric> Metrics { get; } = [];
        public Dictionary<Guid, WorkflowImport> Imports { get; } = [];
        public Dictionary<Guid, WorkflowAlert> Alerts { get; } = [];
        public List<object> Audit { get; } = [];

        public static WorkflowTenant Create(string id)
        {
            var tenant = new WorkflowTenant { Id = id };
            var alertId = Guid.NewGuid();
            tenant.Alerts[alertId] = new WorkflowAlert(
                alertId, null, $"{id}-fingerprint", "cpc", 80m, "critical", "open", 2m, 1m, 1m,
                "sufficient", ["degraded_vs_baseline"], "scoring-v1", "2026-01-01", "2026-01-07", DateTimeOffset.UtcNow);
            tenant.Audit.Add(new { id = Guid.NewGuid(), actorSubjectId = $"user-{id}", action = "seeded" });
            return tenant;
        }
    }

    private sealed record WorkflowCampaign(
        Guid Id,
        string Name,
        string Status,
        string Currency,
        decimal Budget,
        string StartsOn,
        string EndsOn,
        DateTimeOffset CreatedAt);

    private sealed record WorkflowMetric(
        Guid CampaignId,
        string MetricDate,
        decimal Spend,
        decimal Revenue,
        long Impressions,
        long Clicks,
        long Conversions);

    private sealed record WorkflowImport(
        Guid Id,
        string FileName,
        DateTimeOffset UploadedAt,
        int AcceptedRows,
        int RejectedRows,
        string Status,
        IReadOnlyList<object> Errors);

    private sealed record WorkflowAlert(
        Guid Id,
        Guid? CampaignId,
        string Fingerprint,
        string MetricName,
        decimal Score,
        string Severity,
        string Status,
        decimal Observed,
        decimal Baseline,
        decimal Confidence,
        string DataSufficiency,
        IReadOnlyList<string> ReasonCodes,
        string ScoringVersion,
        string EvidenceWindowStart,
        string EvidenceWindowEnd,
        DateTimeOffset DetectedAt);
}

internal static class WorkflowHttpClientExtensions
{
    public static Task<HttpResponseMessage> PatchJsonAsync<T>(this HttpClient client, string uri, T value) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, uri) { Content = JsonContent(value) });

    public static Task<HttpResponseMessage> PutJsonAsync<T>(this HttpClient client, string uri, T value) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Put, uri) { Content = JsonContent(value) });

    private static StringContent JsonContent<T>(T value) => new(
        JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
        Encoding.UTF8,
        "application/json");
}
