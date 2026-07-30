namespace ShipAI.ConsoleHost;

/// <summary>
/// Minimal <c>.env</c> loader. Seeds process environment variables so the standard
/// environment-variable configuration provider picks them up.
/// </summary>
/// <remarks>
/// Existing environment variables always win, so a real environment (CI, a container) is
/// never overridden by a stray file on disk.
/// </remarks>
internal static class DotEnv
{
    public static void Load(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();

            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var separator = trimmed.IndexOf('=');

            if (separator <= 0)
            {
                continue;
            }

            var key = trimmed[..separator].Trim();
            var value = trimmed[(separator + 1)..].Trim().Trim('"');

            if (Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    /// <summary>Walks up from the binary towards the repo root looking for a <c>.env</c>.</summary>
    public static string? FindNearest(string startDirectory, int maxDepth = 8)
    {
        var directory = new DirectoryInfo(startDirectory);

        for (var depth = 0; depth < maxDepth && directory is not null; depth++)
        {
            var candidate = Path.Combine(directory.FullName, ".env");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
