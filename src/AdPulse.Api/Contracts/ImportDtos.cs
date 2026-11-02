using System.Text.Json.Serialization;

namespace AdPulse.Api.Contracts;

public static class ImportStatuses
{
    public const string Validated = "validated";
    public const string Completed = "completed";
    public const string Rejected = "rejected";
    public const string Conflict = "conflict";
}

public sealed record ImportRowErrorDto
{
    [JsonPropertyName("rowNumber")]
    public int RowNumber { get; init; }

    [JsonPropertyName("errorCode")]
    public string ErrorCode { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}

public sealed record ImportBatchDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("fileName")]
    public string FileName { get; init; } = string.Empty;

    [JsonPropertyName("uploadedAt")]
    public DateTimeOffset UploadedAt { get; init; }

    [JsonPropertyName("acceptedRows")]
    public int AcceptedRows { get; init; }

    [JsonPropertyName("rejectedRows")]
    public int RejectedRows { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("errors")]
    public IReadOnlyList<ImportRowErrorDto> Errors { get; init; } = Array.Empty<ImportRowErrorDto>();
}

public sealed record ImportListQuery : PageQuery
{
    [JsonPropertyName("from")]
    public DateOnly? From { get; init; }

    [JsonPropertyName("to")]
    public DateOnly? To { get; init; }
}
