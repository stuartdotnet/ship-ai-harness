using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ShipAI.Agent;
using ShipAI.Agent.Tools;
using ShipAI.ConsoleHost;
using ShipAI.ConsoleHost.Rendering;
using ShipAI.Simulation;
using ShipAI.Simulation.Scenarios;
using SysConsole = System.Console;

SysConsole.OutputEncoding = System.Text.Encoding.UTF8;

if (DotEnv.FindNearest(AppContext.BaseDirectory) is { } envFile)
{
    DotEnv.Load(envFile);
}

var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddUserSecrets(typeof(Program).Assembly, optional: true)
    .Build();

var scenario = JsonScenario.Load(configuration["SHIPAI_SCENARIO"] ?? "derelict-freighter");
var seed = int.TryParse(configuration["SHIPAI_SEED"], out var configuredSeed) ? configuredSeed : 1701;
var simulation = new ShipSimulation(scenario, seed);

IChatClient chatClient;

try
{
    chatClient = ChatClientFactory.Create(configuration);
}
catch (InvalidOperationException ex)
{
    ShipConsole.Error(ex.Message);
    return 1;
}

using (chatClient)
{
    var aurora = ShipAgentFactory.Create(chatClient, simulation);
    var session = await aurora.CreateSessionAsync();

    ShipConsole.Banner(scenario.Name, scenario.Situation);
    ShipConsole.StatusPanel(simulation.State);

    // AURORA speaks first. Prime the agent with the captain's arrival so it opens the
    // exchange rather than waiting mutely at an empty prompt. This is a stage direction,
    // not a captain order: it is neither logged nor ticked, so the first real order still
    // lands at T000.
    await RunAuroraTurnAsync("Captain on the bridge. Awaiting orders.");

    while (true)
    {
        var input = ShipConsole.Prompt(simulation.State);

        if (input is null || input.Trim() is "/quit" or "/exit")
        {
            break;
        }

        var order = input.Trim();

        if (order.Length == 0)
        {
            continue;
        }

        if (order.StartsWith('/'))
        {
            switch (order)
            {
                case "/status":
                    ShipConsole.StatusPanel(simulation.State);
                    break;
                case "/log":
                    ShipConsole.Log(simulation.Log, entries: 20);
                    break;
                case "/help":
                    ShipConsole.Help();
                    break;
                default:
                    ShipConsole.System($"Unknown command '{order}'. Try /help.");
                    break;
            }

            continue;
        }

        simulation.Log.Append(simulation.State.Turn, LogSource.Captain, order);

        if (!await RunAuroraTurnAsync(order))
        {
            continue;
        }

        // Design decision 1: one tick per completed agent turn.
        //
        // It lives here in milestone 1 because there is no context provider yet, which makes
        // the clock the console host's problem. Milestone 2 moves this line into
        // ShipStateProvider.StoreAIContextAsync, where it belongs: advancing the world is an
        // ambient concern of the agent turn, not of whichever UI happens to be attached.
        // Mark the log, tick, then narrate whatever the tick wrote. Scenario events announce
        // themselves to the captain this way and to AURORA not at all: it can reach the same
        // entries through ReadShipLog, but only by choosing to spend a tool call on them.
        var logMark = simulation.Log.Count;

        simulation.Tick();

        ShipConsole.Events(simulation.Log.Since(logMark));
        ShipConsole.StatusPanel(simulation.State);

        if (simulation.IsLost)
        {
            ShipConsole.Error("ISV Kestrel is lost. Voyage over.");
            break;
        }
    }

    // One agent turn, streamed to the bridge. Returns false if AURORA faulted, so the caller
    // can skip the tick and status refresh that only make sense after a completed turn.
    async Task<bool> RunAuroraTurnAsync(string order)
    {
        try
        {
            ShipConsole.BeginAurora();

            await foreach (var update in aurora.RunStreamingAsync(order, session))
            {
                foreach (var call in update.Contents.OfType<FunctionCallContent>())
                {
                    ShipConsole.ToolCall(call.Name, SensorTools.ToolNames.Contains(call.Name));
                }

                if (update.Text is { Length: > 0 } text)
                {
                    ShipConsole.Aurora(text);
                }
            }
        }
        catch (Exception ex)
        {
            ShipConsole.EndAurora();
            ShipConsole.Error($"AURORA fault: {ex.Message}");
            return false;
        }

        ShipConsole.EndAurora();
        return true;
    }
}

ShipConsole.System("AURORA offline.");
return 0;
