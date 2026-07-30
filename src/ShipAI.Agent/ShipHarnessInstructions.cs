using System.Reflection;

namespace ShipAI.Agent;

/// <summary>
/// Loads AURORA's operating doctrine from the embedded markdown file.
/// </summary>
/// <remarks>
/// Kept as markdown rather than a C# string constant so it can be edited, diffed, and
/// reviewed like prose. Instructions are a prompt-engineering artefact; treating them as
/// source code makes them harder to iterate on.
/// </remarks>
public static class ShipHarnessInstructions
{
    private const string ResourceName = "ShipAI.Agent.Instructions.ShipHarnessInstructions.md";

    private static readonly Lazy<string> Cached = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public static string Text => Cached.Value;

    private static string Load()
    {
        using var stream = typeof(ShipHarnessInstructions).GetTypeInfo().Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' was not found.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
