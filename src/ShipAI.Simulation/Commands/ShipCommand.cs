namespace ShipAI.Simulation.Commands;

/// <summary>
/// A request to mutate ship state. One record per action the captain or the ship's AI can take.
/// </summary>
public abstract record ShipCommand;

/// <summary>Routes a percentage of reactor output to a system.</summary>
public sealed record RoutePower(ShipSystem System, int Percent) : ShipCommand;

/// <summary>Seals a section's bulkhead, stopping atmosphere loss through a breach.</summary>
public sealed record SealBulkhead(string Section) : ShipCommand;

/// <summary>
/// Evacuates a section's atmosphere to vacuum. Kills anyone still inside.
/// </summary>
/// <remarks>Approval-gated and lethal from milestone 3 onwards.</remarks>
public sealed record VentAtmosphere(string Section) : ShipCommand;

/// <summary>Moves the ship to new coordinates. Requires engine power.</summary>
public sealed record PlotJump(Coordinates Destination) : ShipCommand;

/// <summary>Changes the ship's readiness posture.</summary>
public sealed record SetAlert(AlertLevel Level) : ShipCommand;
