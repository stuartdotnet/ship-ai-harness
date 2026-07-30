using System.Collections.Immutable;

namespace ShipAI.Simulation;

/// <summary>Reactor output and accumulated heat, both as percentages.</summary>
public sealed record ReactorState(int Output, int Heat)
{
    public bool IsCritical => Heat >= 90;
}

/// <summary>A pressurised compartment of the ship.</summary>
public sealed record ShipSection(string Name, int Atmosphere, bool Breached = false, bool BulkheadSealed = false)
{
    public bool IsHabitable => Atmosphere > 0;

    /// <summary>A breach only bleeds atmosphere while the bulkhead is open.</summary>
    public bool IsVenting => Breached && !BulkheadSealed;
}

/// <summary>Position in the local navigation frame, in astronomical units.</summary>
public sealed record Coordinates(double X, double Y, double Z)
{
    public double DistanceTo(Coordinates other)
        => Math.Sqrt(Math.Pow(X - other.X, 2) + Math.Pow(Y - other.Y, 2) + Math.Pow(Z - other.Z, 2));

    public override string ToString() => $"{X:F2} / {Y:F2} / {Z:F2}";
}

/// <summary>A sensor contact. <see cref="Analysis"/> is null until the contact has been analysed.</summary>
public sealed record Contact(
    string Id,
    string Designation,
    int Bearing,
    double Range,
    string DriveSignature,
    string? Analysis = null);

/// <summary>
/// The complete operational state of the ship at a single turn.
/// </summary>
/// <remarks>
/// Immutable by construction. <see cref="ShipSimulation"/> holds the only mutable
/// reference to a <see cref="ShipState"/> and replaces it wholesale; everything else
/// in the system works with snapshots.
/// </remarks>
public sealed record ShipState
{
    public required int Hull { get; init; }

    public required ReactorState Reactor { get; init; }

    /// <summary>Percentage of reactor output routed to each system. Totals at most 100.</summary>
    public required ImmutableDictionary<ShipSystem, int> PowerAllocation { get; init; }

    /// <summary>Sections keyed by name, compared case-insensitively.</summary>
    public required ImmutableDictionary<string, ShipSection> Sections { get; init; }

    public required ImmutableArray<CrewMember> Crew { get; init; }

    public required ImmutableArray<Contact> Contacts { get; init; }

    public required Coordinates Position { get; init; }

    public required AlertLevel Alert { get; init; }

    public required int Turn { get; init; }

    public int TotalPowerAllocated => PowerAllocation.Values.Sum();

    public int PowerFor(ShipSystem system) => PowerAllocation.TryGetValue(system, out var p) ? p : 0;

    public IEnumerable<CrewMember> LivingCrew => Crew.Where(c => c.IsAlive);

    public IEnumerable<CrewMember> CrewIn(string section)
        => LivingCrew.Where(c => string.Equals(c.Section, section, StringComparison.OrdinalIgnoreCase));

    public ShipSection? FindSection(string name)
        => Sections.TryGetValue(name, out var section) ? section : null;
}
