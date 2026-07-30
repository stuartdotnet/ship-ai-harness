using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ShipAI.Agent.Tools;
using ShipAI.Simulation;

namespace ShipAI.Agent;

/// <summary>
/// Composition root. Turns an <see cref="IChatClient"/> into AURORA.
/// </summary>
/// <remarks>
/// The only thing in this repo that knows about <c>HarnessAgentOptions</c>. Swapping model
/// provider means changing the <see cref="IChatClient"/> passed in here and nothing else.
/// <para>Written against Microsoft.Agents.AI.Harness 1.15.0.</para>
/// </remarks>
public static class ShipAgentFactory
{
    /// <summary>
    /// Context window to compact against. Must be set together with <see cref="MaxOutputTokens"/>
    /// or the harness silently omits the compaction provider from the pipeline entirely.
    /// </summary>
    private const int MaxContextWindowTokens = 128_000;

    private const int MaxOutputTokens = 16_384;

    public static AIAgent Create(
        IChatClient chatClient,
        ShipSimulation simulation,
        ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(simulation);

        var sensors = new SensorTools(simulation);

        var options = new HarnessAgentOptions
        {
            Name = "AURORA",
            Description = "Shipboard intelligence of the ISV Kestrel.",

            // How AURORA operates. Constant across every voyage.
            HarnessInstructions = ShipHarnessInstructions.Text,

            ChatOptions = new ChatOptions
            {
                // What this voyage is for. Comes from the scenario and changes per encounter.
                // The harness concatenates HarnessInstructions first, then these.
                Instructions = simulation.Briefing,
                Tools = [.. sensors.AsAIFunctions()],
            },

            // Batteries-included means knowing which batteries to remove.
            //
            // A starship AI reaching for a web search breaks the fiction and cannot help:
            // everything it needs to know is aboard the ship.
            DisableWebSearch = true,

            // On by default, and it writes to {cwd}/agent-file-memory/{timestamp}_{guid} the
            // moment the agent is constructed. Milestone 4 turns this back on pointed at a
            // per-voyage directory, as the ship's log.
            DisableFileMemory = true,

            // On by default, and it scans the working directory for skill definitions. There
            // are none, so this is a filesystem walk for nothing.
            DisableAgentSkillsProvider = true,

            // FileAccessProvider needs no disabling: it is opt-in via FileAccessStore, which
            // stays null. The agent touches the ship through tools or not at all.

            // Both are required for compaction. Setting only one silently disables it, which
            // you discover on the turn the context window overflows rather than at startup.
            MaxContextWindowTokens = MaxContextWindowTokens,
            MaxOutputTokens = MaxOutputTokens,
        };

        return chatClient.AsHarnessAgent(options, loggerFactory);
    }
}
