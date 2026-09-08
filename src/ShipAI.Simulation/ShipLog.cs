using System.Collections.Immutable;

namespace ShipAI.Simulation;

public enum LogSource
{
    Ship,
    Captain,
    Aurora,
}

/// <summary>
/// How hard an entry should hit the person reading it.
/// </summary>
/// <remarks>
/// Recorded here rather than inferred by the host. A bridge display that decides "The Kestrel
/// takes a debris strike" is serious by matching on the word "strike" is a display that will
/// eventually miss one, and the simulation already knows which events cost the ship something.
/// </remarks>
public enum LogSeverity
{
    /// <summary>Narration. Worth reading, but nothing about the ship has changed.</summary>
    Routine,

    /// <summary>The situation moved against the ship, or something new is on the plot.</summary>
    Warning,

    /// <summary>Damage, a breach, a red alert, or a death.</summary>
    Critical,
}

public sealed record ShipLogEntry(
    int Turn,
    LogSource Source,
    string Message,
    LogSeverity Severity = LogSeverity.Routine)
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

    /// <summary>Number of entries recorded so far. Take a mark, then read <see cref="Since"/>.</summary>
    public int Count => _entries.Count;

    public void Append(int turn, LogSource source, string message,
        LogSeverity severity = LogSeverity.Routine)
        => _entries.Add(new ShipLogEntry(turn, source, message, severity));

    /// <summary>
    /// Everything appended since a mark taken from <see cref="Count"/>, oldest first.
    /// </summary>
    /// <remarks>
    /// Lets a host narrate exactly what a tick produced without having to diff ship state or
    /// guess at how many entries an event wrote.
    /// </remarks>
    public ImmutableArray<ShipLogEntry> Since(int mark)
        => [.. _entries.Skip(Math.Max(0, mark))];

    /// <summary>Returns the most recent <paramref name="count"/> entries, oldest first.</summary>
    public ImmutableArray<ShipLogEntry> Tail(int count)
        => [.. _entries.Skip(Math.Max(0, _entries.Count - count))];
}
