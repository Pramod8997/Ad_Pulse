using AdPulse.Domain.Tenancy;
using System.Text.Json.Serialization;

#pragma warning disable CA1716

namespace AdPulse.Api.Imports;

public static class ImportContract
{
    public const string Route = "/app/imports";
    public const string TemplateFileName = "adpulse-daily-metrics-template.csv";
    public const string TemplateContentType = "text/csv; charset=utf-8";
    public const long MaxFileBytes = 5 * 1024 * 1024;
    public const int MaxDataRows = 100_000;
    public const int MaxStagedRows = MaxDataRows;
    public const int MaxFileNameLength = 260;
    public const string ValidatedStatus = "validating";
    public const string CompletedStatus = "completed";
    public const string RejectedStatus = "failed";
    public const string ConflictStatus = "conflict";

    public static IReadOnlyList<string> Headers =>
    [
        "campaign_id",
        "metric_date",
        "currency",
        "spend",
        "revenue",
        "impressions",
        "clicks",
        "conversions"
    ];

    public static bool HasImportPermission(TenantContext tenant) =>
        tenant.IsInRole("tenant_admin")
        || tenant.IsInRole("tenant-admin")
        || tenant.IsInRole("importer")
        || tenant.IsInRole("import");
}

public sealed record ImportRowRejection(
    [property: JsonPropertyName("rowNumber")] int RowNumber,
    [property: JsonPropertyName("errorCode")] string ErrorCode,
    [property: JsonPropertyName("message")] string Message);

public sealed record ImportResult(
    [property: JsonPropertyName("id")] Guid ImportBatchId,
    [property: JsonPropertyName("fileName")] string FileName,
    [property: JsonPropertyName("uploadedAt")] DateTimeOffset UploadedAt,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("acceptedRows")] int AcceptedRows,
    [property: JsonPropertyName("rejectedRows")] int RejectedRows,
    [property: JsonPropertyName("errors")] IReadOnlyList<ImportRowRejection> Rejections);

public sealed record ImportMetricRow(
    Guid CampaignId,
    DateOnly MetricDate,
    decimal Spend,
    decimal Revenue,
    long Impressions,
    long Clicks,
    long Conversions);

public sealed record ImportMetricCandidate(
    int RowNumber,
    Guid CampaignId,
    DateOnly MetricDate,
    string Currency,
    decimal Spend,
    decimal Revenue,
    long Impressions,
    long Clicks,
    long Conversions)
{
    public ImportMetricRow ToMetricRow() => new(
        CampaignId,
        MetricDate,
        Spend,
        Revenue,
        Impressions,
        Clicks,
        Conversions);
}

public class ImportValidationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class ImportFileTooLargeException()
    : ImportValidationException("file_too_large", "The CSV file exceeds the 5 MiB limit.");

public sealed class ImportConflictException(string message) : Exception(message);
