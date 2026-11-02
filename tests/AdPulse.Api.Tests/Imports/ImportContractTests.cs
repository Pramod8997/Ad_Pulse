using AdPulse.Api.Imports;
using AdPulse.Domain.Tenancy;

namespace AdPulse.Api.Tests.ImportTests;

public sealed class ImportContractTests
{
    [Fact]
    public void Template_is_bounded_and_has_the_frozen_metric_columns()
    {
        Assert.Equal(
            "campaign_id,metric_date,currency,spend,revenue,impressions,clicks,conversions\n",
            ImportTemplate.Content);
        Assert.True(ImportContract.MaxFileBytes <= 5 * 1024 * 1024);
        Assert.True(ImportContract.MaxDataRows > 0);
    }

    [Theory]
    [InlineData("tenant_admin", true)]
    [InlineData("importer", true)]
    [InlineData("analyst", false)]
    public void Import_permission_is_derived_from_membership_roles(string role, bool expected)
    {
        var tenant = new TenantContext("tenant-a", "subject-1", new HashSet<string>([role]));

        Assert.Equal(expected, ImportContract.HasImportPermission(tenant));
    }

    [Fact]
    public void Row_error_metadata_does_not_have_a_raw_row_property()
    {
        var properties = typeof(ImportRowRejection).GetProperties().Select(property => property.Name);

        Assert.Equal(["RowNumber", "ErrorCode", "Message"], properties);
    }
}
