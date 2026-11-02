using System.Text.Json;
using AdPulse.Api.Contracts;
using Xunit;

namespace AdPulse.Api.Tests.Contracts;

public sealed class ApiV2ContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Workflow_write_dtos_do_not_serialize_a_tenant_override()
    {
        var json = JsonSerializer.Serialize(
            new CreateCampaignRequest
            {
                Name = "Spring",
                Currency = "USD",
                Budget = 100m,
                StartsOn = new DateOnly(2026, 1, 1),
                EndsOn = new DateOnly(2026, 1, 31)
            },
            JsonOptions);

        Assert.DoesNotContain("tenantId", json, StringComparison.Ordinal);
        Assert.Contains("\"startsOn\":\"2026-01-01\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_metrics_remain_null_and_zero_is_preserved()
    {
        var json = JsonSerializer.Serialize(
            new CampaignListItemDto
            {
                Id = Guid.Empty,
                Name = "Spring",
                Currency = "USD",
                Budget = 0m,
                Spend = 0m,
                Cpc = null,
                Cpa = null,
                Roas = null,
                LastMetricDate = null
            },
            JsonOptions);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(JsonValueKind.Number, root.GetProperty("spend").ValueKind);
        Assert.Equal(0m, root.GetProperty("spend").GetDecimal());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("cpc").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("lastMetricDate").ValueKind);
    }

    [Fact]
    public void Pagination_defaults_are_bounded_for_stable_cursor_queries()
    {
        var query = new PageQuery();

        Assert.Equal(25, query.Limit);
        Assert.Equal(25, PageQuery.DefaultLimit);
        Assert.Equal(100, PageQuery.MaxLimit);
    }

    [Fact]
    public void Daily_metric_contract_carries_nullable_kpis()
    {
        var metric = new DailyMetricDto
        {
            CampaignId = Guid.NewGuid(),
            MetricDate = new DateOnly(2026, 1, 1),
            Kpis = new KpiMetricsDto { Ctr = null, Cpc = null, Cpa = null, Roas = null, BudgetPacing = null }
        };

        Assert.Null(metric.Kpis.Ctr);
        Assert.Null(metric.Kpis.Roas);
    }

    [Fact]
    public void Problem_details_include_safe_code_and_correlation_id()
    {
        var dto = new ProblemDetailsDto
        {
            Status = 422,
            Title = "Validation failed",
            Detail = "One or more fields are invalid.",
            Code = "validation_failed",
            CorrelationId = "corr-1"
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"code\":\"validation_failed\"", json, StringComparison.Ordinal);
        Assert.Contains("\"correlationId\":\"corr-1\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("stackTrace", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Alert_dto_keeps_scoring_v1_explanation_fields()
    {
        var alert = new AlertDto
        {
            Fingerprint = new string('a', 64),
            Score = 50m,
            Severity = "high",
            Status = AlertStatuses.Open,
            DataSufficiency = "sufficient",
            ReasonCodes = ["degraded_vs_baseline"],
            ScoringVersion = "scoring-v1"
        };

        Assert.Equal(64, alert.Fingerprint.Length);
        Assert.Equal(["degraded_vs_baseline"], alert.ReasonCodes);
        Assert.Equal("scoring-v1", alert.ScoringVersion);
    }

    [Fact]
    public void Settings_dto_uses_reporting_timezone_and_currency_without_tenant_identity()
    {
        var json = JsonSerializer.Serialize(
            new TenantSettingsDto
            {
                ReportingTimeZone = "UTC",
                ReportingCurrency = "USD",
                UpdatedAt = DateTimeOffset.UtcNow
            },
            JsonOptions);

        Assert.Contains("\"reportingTimeZone\":\"UTC\"", json, StringComparison.Ordinal);
        Assert.Contains("\"reportingCurrency\":\"USD\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("tenantId", json, StringComparison.Ordinal);
    }
}
