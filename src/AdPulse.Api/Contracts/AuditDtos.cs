using System.Text.Json.Serialization;

namespace AdPulse.Api.Contracts;

public sealed record AuditEventDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("occurredAt")]
    public DateTimeOffset OccurredAt { get; init; }

    [JsonPropertyName("actorSubjectId")]
    public string ActorSubjectId { get; init; } = string.Empty;

    [JsonPropertyName("action")]
    public string Action { get; init; } = string.Empty;

    [JsonPropertyName("entityType")]
    public string EntityType { get; init; } = string.Empty;

    [JsonPropertyName("entityId")]
    public string EntityId { get; init; } = string.Empty;
}

public sealed record AuditListQuery : PageQuery
{
    [JsonPropertyName("from")]
    public DateOnly? From { get; init; }

    [JsonPropertyName("to")]
    public DateOnly? To { get; init; }

    [JsonPropertyName("actorSubjectId")]
    public string? ActorSubjectId { get; init; }

    [JsonPropertyName("action")]
    public string? Action { get; init; }

    [JsonPropertyName("entityType")]
    public string? EntityType { get; init; }
}
