namespace ShipAI.Simulation;

/// <summary>A powered subsystem competing for reactor output.</summary>
public enum ShipSystem
{
    Engines,
    Shields,
    Weapons,
    LifeSupport,
    Sensors,
}

/// <summary>The ship's readiness posture.</summary>
public enum AlertLevel
{
    Green,
    Yellow,
    Red,
}
