namespace AdPulse.Api.Scoring;

public sealed class ScoringClientOptions
{
    public Uri? BaseAddress { get; set; }

    public string ScorePath { get; set; } = "/v1/score";

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    public int MaxRetries { get; set; } = 2;

    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(50);

    public int MaxResponseBytes { get; set; } = 64 * 1024;

    internal void Validate()
    {
        if (BaseAddress is not null
            && (!BaseAddress.IsAbsoluteUri || BaseAddress.Scheme is not ("http" or "https")))
        {
            throw new InvalidOperationException("The scoring base address must be an absolute HTTP or HTTPS URI.");
        }

        if (string.IsNullOrWhiteSpace(ScorePath)
            || ScorePath[0] != '/'
            || ScorePath.StartsWith("//", StringComparison.Ordinal)
            || !Uri.TryCreate(ScorePath, UriKind.Relative, out _))
        {
            throw new InvalidOperationException("The scoring path must be a relative URI.");
        }

        if (Timeout <= TimeSpan.Zero || Timeout > TimeSpan.FromSeconds(30))
        {
            throw new InvalidOperationException("The scoring timeout must be greater than zero and at most 30 seconds.");
        }

        if (MaxRetries is < 0 or > 3)
        {
            throw new InvalidOperationException("The scoring retry count must be between zero and three.");
        }

        if (RetryDelay < TimeSpan.Zero || RetryDelay > TimeSpan.FromSeconds(1))
        {
            throw new InvalidOperationException("The scoring retry delay must be between zero and one second.");
        }

        if (MaxResponseBytes is < 1024 or > 64 * 1024)
        {
            throw new InvalidOperationException("The scoring response limit must be between 1 KiB and 64 KiB.");
        }
    }
}
