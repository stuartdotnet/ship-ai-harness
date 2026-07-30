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

    /// <summary>The mission, in the captain's words. Becomes <c>ChatOptions.Instructions</c>.</summary>
    string Briefing { get; }

    /// <summary>Events keyed by the turn on which they fire.</summary>
    IReadOnlyList<ScenarioEvent> Events { get; }

    ShipState CreateInitialState();
}
