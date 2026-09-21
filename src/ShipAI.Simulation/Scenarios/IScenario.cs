namespace ShipAI.Simulation.Scenarios;

/// <summary>
/// A scripted encounter: the starting world plus the events that fire as turns elapse.
/// </summary>
/// <remarks>
/// Scenarios exist so that a demo, a screenshot, and a blog code sample all produce the
/// same transcript. Combined with a fixed seed, a scenario replay is deterministic.
/// </remarks>
public interface IScenario
{
    string Name { get; }

    /// <summary>
    /// The mission as the captain hands it to AURORA. Becomes <c>ChatOptions.Instructions</c>.
    /// </summary>
    /// <remarks>
    /// Written in the second person, and the "you" is AURORA. It is a system prompt, so it is
    /// never shown to the captain — printing it on the terminal tells a human reading the
    /// screen that they are the ship's AI. Use <see cref="Situation"/> for that.
    /// </remarks>
    string Briefing { get; }

    /// <summary>
    /// The same mission as the captain understands it, for the terminal. Never sent to the model.
    /// </summary>
    /// <remarks>
    /// Here the "you" is the human at the keyboard. Two texts rather than one reused text,
    /// because the two readers are on opposite sides of the conversation and a single voice
    /// cannot address both.
    /// </remarks>
    string Situation { get; }

    /// <summary>Events keyed by the turn on which they fire.</summary>
    IReadOnlyList<ScenarioEvent> Events { get; }

    ShipState CreateInitialState();
}
