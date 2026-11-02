namespace AdPulse.Api.Tests.ReleaseGates;

internal static class ReleaseGateRepository
{
    private static readonly Lazy<string> Root = new(FindRoot);

    public static string PathTo(params string[] parts) => Path.Combine([Root.Value, .. parts]);

    public static string ReadText(params string[] parts) => File.ReadAllText(PathTo(parts));

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AdPulse.sln"))
                && Directory.Exists(Path.Combine(directory.FullName, "contracts")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("The AdPulse repository root could not be found from the test output directory.");
    }
}
