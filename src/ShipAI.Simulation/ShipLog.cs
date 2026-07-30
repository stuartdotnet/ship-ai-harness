using System.Collections.Immutable;

namespace ShipAI.Simulation;

public enum LogSource
{
    Ship,
    Captain,
    Aurora,
}

public sealed record ShipLogEntry(int Turn, LogSource Source, string Message)
{
    public override string ToString() => $"[T{Turn:D3}] {Source.ToString().ToUpperInvariant()}: {Message}";
}

/// <summary>
/// The ship's log. Append-only within a voyage.
/// </summary>
/// <remarks>
/// Milestone 4 persists this to disk behind <c>FileMemoryProvider</c> so a voyage can
/// reference earlier encounters. Until then it lives for the length of the process.
/// </remarks>
public sealed class ShipLog
{
    private readonly List<ShipLogEntry> _entries = [];

    public ImmutableArray<ShipLogEntry> Entries => [.. _entries];

    public void Append(int turn, LogSource source, string message)
        => _entries.Add(new ShipLogEntry(turn, source, message));

    /// <summary>Returns the most recent <paramref name="count"/> entries, oldest first.</summary>
    public ImmutableArray<ShipLogEntry> Tail(int count)
        => [.. _entries.Skip(Math.Max(0, _entries.Count - count))];
}
