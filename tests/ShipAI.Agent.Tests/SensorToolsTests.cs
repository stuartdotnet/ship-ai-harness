using ShipAI.Agent.Tools;
using ShipAI.Simulation;
using ShipAI.Simulation.Scenarios;

namespace ShipAI.Agent.Tests;

public class SensorToolsTests
{
    private static (ShipSimulation Simulation, SensorTools Tools) Create()
    {
        var simulation = new ShipSimulation(JsonScenario.Load("derelict-freighter"));
        return (simulation, new SensorTools(simulation));
    }

    private static void AdvanceTo(ShipSimulation simulation, int turn)
    {
        while (simulation.State.Turn < turn)
        {
            simulation.Tick();
        }
    }

    [Fact]
    public void ToolNames_MatchesWhatIsActuallyExposedToTheModel()
    {
        // The console uses ToolNames to tell ship systems apart from the harness's own tools in
        // the transcript. Drift between the two lists silently relabels a real ship action as
        // framework plumbing, which is exactly the confusion the split exists to remove.
        var (_, tools) = Create();

        var exposed = tools.AsAIFunctions().Select(f => f.Name).ToHashSet();

        Assert.Equal(exposed, SensorTools.ToolNames.ToHashSet());
    }

    [Fact]
    public void ScanSector_ReportsAClearSectorBeforeAnyContactAppears()
    {
        var (_, tools) = Create();

        Assert.Contains("No contacts", tools.ScanSector());
    }

    [Fact]
    public void Readings_AreStampedWithTheTurnTheyWereTakenOn()
    {
        // Without the stamp, a sweep result is a bare sentence that stays in the conversation
        // for the rest of the voyage and reads as current on every later turn. Observed: the
        // agent swept an empty sector on turn 0 and was still reporting "no vessel detected"
        // on turn 3 with the freighter on the captain's panel.
        var (simulation, tools) = Create();

        Assert.StartsWith("[T000]", tools.ScanSector());

        AdvanceTo(simulation, 4);

        Assert.StartsWith("[T004]", tools.ScanSector());
        Assert.StartsWith("[T004]", tools.AnalyseContact("C-1"));
        Assert.StartsWith("[T004]", tools.QueryCrewManifest());
        Assert.StartsWith("[T004]", tools.AnalyseContact("C-99"));
    }

    [Fact]
    public void ScanSector_ReportsBearingRangeAndDriveSignature()
    {
        var (simulation, tools) = Create();
        AdvanceTo(simulation, 2);

        var report = tools.ScanSector();

        Assert.Contains("C-1", report);
        Assert.Contains("MV Anselm", report);
        Assert.Contains("214", report);
        Assert.Contains("8.4 AU", report);
        Assert.Contains("Fusion plant offline", report);
    }

    [Fact]
    public void AnalyseContact_ReturnsTheAuthoredAnalysis()
    {
        var (simulation, tools) = Create();
        AdvanceTo(simulation, 2);

        var analysis = tools.AnalyseContact("C-1");

        Assert.Contains("Anselm Freight", analysis);
        Assert.Contains("escape pods", analysis, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnalyseContact_IsCaseInsensitiveOnContactId()
    {
        var (simulation, tools) = Create();
        AdvanceTo(simulation, 2);

        Assert.Equal(tools.AnalyseContact("C-1"), tools.AnalyseContact("c-1"));
    }

    [Fact]
    public void AnalyseContact_UnknownId_ExplainsWhatIsAvailableInsteadOfThrowing()
    {
        var (simulation, tools) = Create();
        AdvanceTo(simulation, 2);

        var response = tools.AnalyseContact("C-99");

        // A tool that throws hands the model a stack trace. A tool that answers with the
        // valid options hands it a way to recover on the next call.
        Assert.Contains("No contact 'C-99'", response);
        Assert.Contains("C-1", response);
    }

    [Fact]
    public void QueryCrewManifest_ScopedToASection_NamesTheOccupants()
    {
        var (_, tools) = Create();

        var manifest = tools.QueryCrewManifest("Section C");

        Assert.Contains("Kai Tanaka", manifest);
        Assert.Contains("Mira Halloran", manifest);
        Assert.Contains("Dev Bhatt", manifest);
        Assert.DoesNotContain("Ada Mirren", manifest);
    }

    [Fact]
    public void QueryCrewManifest_UnknownSection_ListsTheRealOnes()
    {
        var (_, tools) = Create();

        var manifest = tools.QueryCrewManifest("Observation Lounge");

        Assert.Contains("No section named", manifest);
        Assert.Contains("Bridge", manifest);
    }

    [Fact]
    public void QueryCrewManifest_MarksCasualties()
    {
        var (simulation, tools) = Create();
        simulation.Apply(new Simulation.Commands.VentAtmosphere("Section C"));

        var manifest = tools.QueryCrewManifest();

        Assert.Contains("5 of 8 alive", manifest);
        Assert.Contains("LOST", manifest);
    }

    [Fact]
    public void ReadShipLog_ReturnsRecentEntriesOldestFirst()
    {
        var (simulation, tools) = Create();
        AdvanceTo(simulation, 4);

        var log = tools.ReadShipLog(entries: 5);
        var lines = log.Split(Environment.NewLine);

        Assert.Contains("distress beacon", log);
        Assert.True(lines.Length <= 5);
    }

    [Fact]
    public void ReadShipLog_ClampsAbsurdRequests()
    {
        var (_, tools) = Create();

        Assert.False(string.IsNullOrWhiteSpace(tools.ReadShipLog(entries: -10)));
        Assert.False(string.IsNullOrWhiteSpace(tools.ReadShipLog(entries: 100_000)));
    }

    [Fact]
    public void EveryTool_CarriesADescriptionForTheModel()
    {
        var (_, tools) = Create();
        var functions = tools.AsAIFunctions().ToList();

        Assert.Equal(4, functions.Count);

        foreach (var function in functions)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(function.Description),
                $"{function.Name} has no description; the model has to guess what it does.");

            // "Scans the sector" is a name restated. A description earns its place by saying
            // what comes back and when to reach for it.
            Assert.True(
                function.Description.Length > 60,
                $"{function.Name} description is too thin: '{function.Description}'");
        }
    }

    [Fact]
    public void ToolSurface_ContainsNoShipStatusTool()
    {
        var (_, tools) = Create();
        var names = tools.AsAIFunctions().Select(f => f.Name).ToList();

        // Design decision 3. Ship status is ambient and arrives via an AIContextProvider in
        // milestone 2. If a status tool ever reappears here, the lesson has been undone.
        Assert.DoesNotContain(names, n => n.Contains("Status", StringComparison.OrdinalIgnoreCase));
    }
}
