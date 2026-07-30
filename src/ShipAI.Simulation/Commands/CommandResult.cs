namespace ShipAI.Simulation.Commands;

/// <summary>
/// The outcome of applying a <see cref="ShipCommand"/>.
/// </summary>
/// <remarks>
/// Failures come back as results, never as exceptions. A tool that throws gives the model
/// a stack trace; a tool that returns "insufficient reactor output: engines need 40%, 25%
/// available" gives it something to reason about and re-plan from. That difference shows up
/// directly in transcript quality.
/// </remarks>
public sealed record CommandResult(bool Success, string Narrative, ShipState State)
{
    public static CommandResult Ok(string narrative, ShipState state) => new(true, narrative, state);

    public static CommandResult Refused(string reason, ShipState state) => new(false, reason, state);
}
