namespace ShipAI.Simulation;

/// <summary>
/// A named member of the crew, assigned to a section.
/// </summary>
/// <remarks>
/// Crew are named deliberately. From milestone 3 onwards the approval prompt for a
/// lethal action lists the people standing in the affected section by name, and that
/// only works if they have names.
/// </remarks>
public sealed record CrewMember(string Name, string Role, string Section, bool IsAlive = true);
