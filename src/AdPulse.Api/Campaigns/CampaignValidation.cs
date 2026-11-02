namespace AdPulse.Api.Campaigns;

public sealed class CampaignValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("The campaign request is invalid.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

public static class CampaignValidation
{
    private const decimal MaximumBudget = 999_999_999_999_999.9999m;
    private static readonly string[] ValidStatuses = ["draft", "active", "paused", "completed"];

    public static IReadOnlyDictionary<string, string[]> Validate(CampaignWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            Add(errors, nameof(request.Name), "Name is required.");
        }
        else if (request.Name.Trim().Length > 200)
        {
            Add(errors, nameof(request.Name), "Name must be at most 200 characters.");
        }

        if (string.IsNullOrWhiteSpace(request.Status))
        {
            Add(errors, nameof(request.Status), "Status is required.");
        }
        else if (request.Status.Trim().Length > 32)
        {
            Add(errors, nameof(request.Status), "Status must be at most 32 characters.");
        }
        else if (!ValidStatuses.Contains(request.Status.Trim().ToLowerInvariant(), StringComparer.Ordinal))
        {
            Add(errors, nameof(request.Status), "Status must be draft, active, paused or completed.");
        }

        if (request.Currency is null || request.Currency.Length != 3 || request.Currency.Any(character => character is < 'A' or > 'Z'))
        {
            Add(errors, nameof(request.Currency), "Currency must be three uppercase ASCII letters.");
        }

        if (request.Budget is null)
        {
            Add(errors, nameof(request.Budget), "Budget is required.");
        }
        else if (request.Budget < 0 || request.Budget > MaximumBudget || decimal.Round(request.Budget.Value, 4) != request.Budget.Value)
        {
            Add(errors, nameof(request.Budget), "Budget must be non-negative and use at most four decimal places.");
        }

        if (request.StartsOn is null)
        {
            Add(errors, nameof(request.StartsOn), "StartsOn is required.");
        }

        if (request.EndsOn is null)
        {
            Add(errors, nameof(request.EndsOn), "EndsOn is required.");
        }

        if (request.StartsOn is not null && request.EndsOn is not null && request.EndsOn < request.StartsOn)
        {
            Add(errors, nameof(request.EndsOn), "EndsOn must be on or after StartsOn.");
        }

        return errors.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToArray(),
            StringComparer.Ordinal);
    }

    public static void ValidateOrThrow(CampaignWriteRequest request)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            throw new CampaignValidationException(errors);
        }
    }

    public static IReadOnlyDictionary<string, string[]> Validate(CampaignListQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        if (query.Search?.Length > 200)
        {
            Add(errors, nameof(query.Search), "Search must be at most 200 characters.");
        }

        if (query.Status?.Length > 32)
        {
            Add(errors, nameof(query.Status), "Status must be at most 32 characters.");
        }
        else if (query.Status is not null
            && !ValidStatuses.Contains(query.Status.Trim().ToLowerInvariant(), StringComparer.Ordinal))
        {
            Add(errors, nameof(query.Status), "Status must be draft, active, paused or completed.");
        }

        if (query.Cursor?.Length > 4096)
        {
            Add(errors, nameof(query.Cursor), "Cursor must be at most 4096 characters.");
        }

        if (query.Limit is <= 0 or > 100)
        {
            Add(errors, nameof(query.Limit), "Limit must be between 1 and 100.");
        }

        if (query.StartsOnFrom is not null && query.StartsOnTo is not null && query.StartsOnFrom > query.StartsOnTo)
        {
            Add(errors, nameof(query.StartsOnTo), "StartsOnTo must be on or after StartsOnFrom.");
        }

        if (query.EndsOnFrom is not null && query.EndsOnTo is not null && query.EndsOnFrom > query.EndsOnTo)
        {
            Add(errors, nameof(query.EndsOnTo), "EndsOnTo must be on or after EndsOnFrom.");
        }

        return errors.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToArray(),
            StringComparer.Ordinal);
    }

    public static string NormalizedName(string name) => name.Trim();

    public static string NormalizedStatus(string status) => status.Trim().ToLowerInvariant();

    private static void Add(IDictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var messages))
        {
            messages = [];
            errors[field] = messages;
        }

        messages.Add(message);
    }
}
