using System.Text.Json.Serialization;

namespace AdPulse.Api.Contracts;

public sealed record TenantSettingsDto
{
    [JsonPropertyName("reportingTimeZone")]
    public string ReportingTimeZone { get; init; } = string.Empty;

    [JsonPropertyName("reportingCurrency")]
    public string ReportingCurrency { get; init; } = string.Empty;

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record UpdateTenantSettingsRequest
{
    [JsonPropertyName("reportingTimeZone")]
    public string ReportingTimeZone { get; init; } = string.Empty;

    [JsonPropertyName("reportingCurrency")]
    public string ReportingCurrency { get; init; } = string.Empty;
}
