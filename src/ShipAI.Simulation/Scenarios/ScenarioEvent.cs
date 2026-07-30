namespace ShipAI.Simulation.Scenarios;

public enum ScenarioEventKind
{
    /// <summary>Reduces hull integrity by <see cref="ScenarioEvent.Magnitude"/>.</summary>
    HullDamage,

    /// <summary>Breaches the section named by <see cref="ScenarioEvent.Target"/>.</summary>
    Breach,

    /// <summary>Adds <see cref="ScenarioEvent.Contact"/> to the sensor picture.</summary>
    ContactDetected,

    /// <summary>Removes the contact whose id matches <see cref="ScenarioEvent.Target"/>.</summary>
    ContactLost,

    /// <summary>Adds <see cref="ScenarioEvent.Magnitude"/> to reactor heat.</summary>
    ReactorSpike,

    /// <summary>Narrative only. Appends to the ship's log and changes nothing.</summary>
    Message,
}

/// <summary>
/// A scripted event fired at the start of a specific turn.
/// </summary>
public sealed record ScenarioEvent
{
    public required int Turn { get; init; }

    public required ScenarioEventKind Kind { get; init; }

    public string Target { get; init; } = string.Empty;

    public int Magnitude { get; init; }

    public required string Narrative { get; init; }

    /// <summary>Payload for <see cref="ScenarioEventKind.ContactDetected"/>.</summary>
    public Contact? Contact { get; init; }
}
