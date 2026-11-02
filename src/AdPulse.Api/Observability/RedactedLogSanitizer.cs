namespace AdPulse.Api.Observability;

public interface IRedactedLogSanitizer
{
    IReadOnlyDictionary<string, object?> Sanitize(IEnumerable<KeyValuePair<string, object?>> properties);
}

public sealed class RedactedLogSanitizer : IRedactedLogSanitizer
{
    private const string RedactedValue = "[REDACTED]";
    private const int MaximumStringLength = 256;

    public IReadOnlyDictionary<string, object?> Sanitize(IEnumerable<KeyValuePair<string, object?>> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        var sanitized = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in properties)
        {
            var name = string.IsNullOrWhiteSpace(property.Key) ? "unnamed" : property.Key;
            sanitized[name] = IsSensitiveName(name) ? RedactedValue : SanitizeValue(property.Value);
        }

        return sanitized;
    }

    private static object? SanitizeValue(object? value) => value switch
    {
        null => null,
        string text => text.Length <= MaximumStringLength ? text : text[..MaximumStringLength],
        bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => value,
        DateTime or DateTimeOffset or TimeSpan or Guid => value,
        Enum => value.ToString(),
        _ => RedactedValue
    };

    private static bool IsSensitiveName(string name) =>
        name.Contains("authorization", StringComparison.OrdinalIgnoreCase)
        || name.Contains("token", StringComparison.OrdinalIgnoreCase)
        || name.Contains("secret", StringComparison.OrdinalIgnoreCase)
        || name.Contains("password", StringComparison.OrdinalIgnoreCase)
        || name.Contains("cookie", StringComparison.OrdinalIgnoreCase)
        || name.Contains("apikey", StringComparison.OrdinalIgnoreCase)
        || name.Contains("connectionstring", StringComparison.OrdinalIgnoreCase)
        || name.Contains("sql", StringComparison.OrdinalIgnoreCase)
        || name.Contains("query", StringComparison.OrdinalIgnoreCase)
        || name.Contains("payload", StringComparison.OrdinalIgnoreCase)
        || name.Contains("raw", StringComparison.OrdinalIgnoreCase)
        || name.Contains("body", StringComparison.OrdinalIgnoreCase);
}
