namespace AdPulse.Api.Tests.ReleaseGates;

public sealed class ApiFrontendContractReleaseGateTests
{
    [Fact]
    public void Accepted_api_contract_contains_the_auth_and_tenant_boundary()
    {
        var contract = ReleaseGateRepository.ReadText("contracts", "api-v1.md");
        var workflowContract = ReleaseGateRepository.ReadText("contracts", "api-v2.md");

        Assert.Contains("Authorization: Bearer", contract, StringComparison.Ordinal);
        Assert.Contains("membership in SQL Server", contract, StringComparison.Ordinal);
        Assert.Contains("No request body, query string, route value or custom tenant header", contract, StringComparison.Ordinal);
        Assert.Contains("KPI division by zero returns `null`", contract, StringComparison.Ordinal);
        Assert.Contains("application/problem+json", contract, StringComparison.Ordinal);
        Assert.Contains("Request bodies never contain an authorization `tenantId`", workflowContract, StringComparison.Ordinal);
        Assert.Contains("from another tenant is indistinguishable from a missing resource (`404`)", workflowContract, StringComparison.Ordinal);
        Assert.Contains("correlationId` is returned on every workflow error", workflowContract, StringComparison.Ordinal);
        Assert.Contains("`PUT` replaces the complete", workflowContract, StringComparison.Ordinal);
    }

    [Fact]
    public void Accepted_ui_contract_contains_routes_token_and_null_semantics()
    {
        var contract = ReleaseGateRepository.ReadText("docs", "ui-contract.md");
        var router = ReleaseGateRepository.ReadText("web", "src", "router.ts");

        foreach (var route in new[]
        {
            "`/app`",
            "`/app/campaigns`",
            "`/app/campaigns/:id`",
            "`/app/alerts`",
            "`/app/imports`",
            "`/app/audit`",
            "`/app/settings`"
        })
        {
            Assert.Contains(route, contract, StringComparison.Ordinal);
        }

        Assert.Contains("kept out of local storage", contract, StringComparison.Ordinal);
        Assert.Contains("must not accept a tenant ID override", contract, StringComparison.Ordinal);
        Assert.Contains("Missing numeric data stays `null`", contract, StringComparison.Ordinal);
        Assert.Contains("loading/empty/error/partial/stale states", contract, StringComparison.Ordinal);
        foreach (var route in new[]
        {
            "path: '/app'",
            "path: '/app/campaigns'",
            "path: '/app/campaigns/:id'",
            "path: '/app/alerts'",
            "path: '/app/imports'",
            "path: '/app/audit'",
            "path: '/app/settings'"
        })
        {
            Assert.Contains(route, router, StringComparison.Ordinal);
        }

        Assert.Contains("requiredPermission: 'import'", router, StringComparison.Ordinal);
        Assert.Contains("requiredPermission: 'audit'", router, StringComparison.Ordinal);
        Assert.Contains("requiredRole: 'tenant-admin'", router, StringComparison.Ordinal);
    }

    [Fact]
    public void Api_and_ui_contract_files_are_present_in_the_repository()
    {
        Assert.True(File.Exists(ReleaseGateRepository.PathTo("contracts", "api-v1.md")));
        Assert.True(File.Exists(ReleaseGateRepository.PathTo("contracts", "api-v2.md")));
        Assert.True(File.Exists(ReleaseGateRepository.PathTo("contracts", "api-v2.schema.json")));
        Assert.True(File.Exists(ReleaseGateRepository.PathTo("contracts", "scoring-v1.schema.json")));
        Assert.True(File.Exists(ReleaseGateRepository.PathTo("docs", "ui-contract.md")));
        Assert.True(File.Exists(ReleaseGateRepository.PathTo("web", "src", "router.ts")));
    }
}
