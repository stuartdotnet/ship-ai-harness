namespace ShipAI.Simulation;

/// <summary>
/// Source of the simulation's current turn number.
/// </summary>
/// <remarks>
/// Design decision 1: time advances one turn per completed agent turn, which is what
/// makes a run reproducible. This seam exists so a real-time variant can be swapped in
/// later without touching <see cref="ShipSimulation"/>'s rules.
/// </remarks>
public interface IShipClock
{
    int CurrentTurn { get; }

    void Advance();
}

/// <summary>The default clock: one turn per agent turn.</summary>
public sealed class TurnClock(int startTurn = 0) : IShipClock
{
    public int CurrentTurn { get; private set; } = startTurn;

    public void Advance() => CurrentTurn++;
}
