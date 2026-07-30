using Microsoft.Extensions.AI;
using ShipAI.Simulation;
using ShipAI.Simulation.Scenarios;

namespace ShipAI.Agent.Tests;

/// <summary>
/// Guards the composition root, in particular the harness defaults this repo switches off.
/// </summary>
/// <remarks>
/// These run without parallelisation because the file-memory test has to control the process
/// working directory: the harness roots its default file store at <c>{cwd}/agent-file-memory</c>.
/// </remarks>
[Collection(nameof(ShipAgentFactoryTests))]
[CollectionDefinition(nameof(ShipAgentFactoryTests), DisableParallelization = true)]
public class ShipAgentFactoryTests
{
    private static ShipSimulation CreateSimulation()
        => new(JsonScenario.Load("derelict-freighter"));

    [Fact]
    public async Task Create_ProducesAnAgentNamedAurora()
    {
        using var chatClient = new FakeChatClient();

        var agent = ShipAgentFactory.Create(chatClient, CreateSimulation());

        Assert.Equal("AURORA", agent.Name);

        var session = await agent.CreateSessionAsync();
        Assert.NotNull(session);
    }

    [Fact]
    public void Create_RejectsNullDependencies()
    {
        using var chatClient = new FakeChatClient();

        Assert.Throws<ArgumentNullException>(() => ShipAgentFactory.Create(null!, CreateSimulation()));
        Assert.Throws<ArgumentNullException>(() => ShipAgentFactory.Create(chatClient, null!));
    }

    [Fact]
    public async Task Run_ReachesTheChatClientWithTheShipToolsAndNoWebSearch()
    {
        using var chatClient = new FakeChatClient("Sector clear, Captain.");

        var agent = ShipAgentFactory.Create(chatClient, CreateSimulation());
        var session = await agent.CreateSessionAsync();

        var response = await agent.RunAsync("Report.", session);

        Assert.Contains("Sector clear", response.Text);

        var options = Assert.Single(chatClient.ObservedOptions, o => o is not null)!;
        var toolNames = (options.Tools ?? []).Select(t => t.Name).ToList();

        Assert.Contains("ScanSector", toolNames);
        Assert.Contains("AnalyseContact", toolNames);
        Assert.Contains("QueryCrewManifest", toolNames);
        Assert.Contains("ReadShipLog", toolNames);

        // DisableWebSearch = true. A starship AI reaching for a web search breaks the fiction
        // and cannot help; everything it needs to know is aboard the ship.
        Assert.DoesNotContain(options.Tools ?? [], t => t is HostedWebSearchTool);
    }

    [Fact]
    public async Task Run_CombinesHarnessDoctrineWithTheMissionBriefing()
    {
        using var chatClient = new FakeChatClient();
        var simulation = CreateSimulation();

        var agent = ShipAgentFactory.Create(chatClient, simulation);
        var session = await agent.CreateSessionAsync();

        await agent.RunAsync("Report.", session);

        var options = Assert.Single(chatClient.ObservedOptions, o => o is not null)!;
        var instructions = options.Instructions ?? string.Empty;

        // Harness instructions first, then the scenario briefing. The doctrine is constant
        // across voyages; the mission is not.
        Assert.Contains("AURORA — Operating Doctrine", instructions);
        Assert.Contains("Halveston", instructions);
        Assert.True(
            instructions.IndexOf("Operating Doctrine", StringComparison.Ordinal)
            < instructions.IndexOf("Halveston", StringComparison.Ordinal),
            "harness instructions should precede the mission briefing");
    }

    [Fact]
    public async Task Create_DoesNotLitterTheWorkingDirectoryWithAgentFileMemory()
    {
        // FileMemoryProvider is ON by default and roots itself at {cwd}/agent-file-memory.
        // Milestone 4 re-enables it against a per-voyage store; until then it should leave
        // no trace at all.
        var original = Directory.GetCurrentDirectory();
        var sandbox = Directory.CreateTempSubdirectory("shipai-cwd-");

        try
        {
            Directory.SetCurrentDirectory(sandbox.FullName);

            using var chatClient = new FakeChatClient();
            var agent = ShipAgentFactory.Create(chatClient, CreateSimulation());
            var session = await agent.CreateSessionAsync();

            await agent.RunAsync("Report.", session);

            Assert.False(
                Directory.Exists(Path.Combine(sandbox.FullName, "agent-file-memory")),
                "the harness created its default file-memory store despite DisableFileMemory");
            Assert.Empty(Directory.GetFileSystemEntries(sandbox.FullName));
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            sandbox.Delete(recursive: true);
        }
    }
}
