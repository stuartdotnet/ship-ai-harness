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

    /// <summary>
    /// The contact is on the plot and on the captain's panel, and the model asked for it by a
    /// name that is not character-for-character its ID.
    /// </summary>
    /// <remarks>
    /// This is the shape the bug arrived in: at T003 the plot holds C-1, the captain asks
    /// about the freighter, and the answer comes back that there is no such contact while it
    /// is rendered on screen. Every variant below is something a model plausibly emits from
    /// the sensor line "C-1 — MV Anselm", including the en dash it can pick up from the em
    /// dash in that very line.
    /// </remarks>
    [Theory]
    [InlineData("C-1")]
    [InlineData("c-1")]
    [InlineData("C1")]
    [InlineData(" C-1 ")]
    [InlineData("C-1.")]
    [InlineData("\"C-1\"")]
    [InlineData("C\u20131")]
    [InlineData("MV Anselm")]
    [InlineData("mv anselm")]
    [InlineData("Anselm")]
    [InlineData("contact C-1")]
    public void AnalyseContact_ResolvesWhatAModelActuallyPasses(string asked)
    {
        var (simulation, tools) = Create();
        AdvanceTo(simulation, 3);

        Assert.Contains("C-1", simulation.State.Contacts.Select(c => c.Id));

        var reading = tools.AnalyseContact(asked);

        Assert.DoesNotContain("No contact", reading, StringComparison.Ordinal);
        Assert.Contains("MV Anselm", reading, StringComparison.Ordinal);
    }

    [Fact]
    public void AnalyseContact_NamesTheContactsItDoesHaveWhenItCannotResolve()
    {
        // The recovery path matters more than the match: an agent told only "no contact 'X'"
        // has nothing to retry with. Listing designations alongside IDs is what lets it ask
        // again correctly on the next call instead of reporting the contact as absent.
        var (simulation, tools) = Create();
        AdvanceTo(simulation, 3);

        var reading = tools.AnalyseContact("the Sparrow");

        Assert.Contains("C-1", reading, StringComparison.Ordinal);
        Assert.Contains("MV Anselm", reading, StringComparison.Ordinal);
    }

    [Fact]
    public void AnalyseContact_SaysSoRatherThanGuessingWhenTwoContactsMatch()
    {
        // A tool that silently picks one of two is worse than one that cannot choose. The
        // second is a sentence the agent can act on; the first is a confident wrong answer.
        var (simulation, tools) = Create();
        AdvanceTo(simulation, 12);

        Assert.Equal(2, simulation.State.Contacts.Length);

        var reading = tools.AnalyseContact("C");

        Assert.Contains("more than one", reading, StringComparison.Ordinal);
        Assert.Contains("C-1", reading, StringComparison.Ordinal);
        Assert.Contains("C-2", reading, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Section C")]
    [InlineData("section c")]
    [InlineData("sectionC")]
    [InlineData("Section-C")]
    public void QueryCrewManifest_ResolvesSectionNamesTheSameWay(string asked)
    {
        // Same defect, same fix, and this one sits in the middle of the breach demo: the
        // captain asks who is in Section C on the turn it starts venting.
        var (simulation, tools) = Create();
        AdvanceTo(simulation, 9);

        var manifest = tools.QueryCrewManifest(asked);

        Assert.DoesNotContain("No section", manifest, StringComparison.Ordinal);
        Assert.Contains("Kai Tanaka", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void QueryCrewManifest_StillRejectsASectionTheShipDoesNotHave()
    {
        // Leniency has to stop somewhere, or every argument resolves to something and the
        // agent never learns it asked for a compartment that does not exist.
        var (_, tools) = Create();

        var manifest = tools.QueryCrewManifest("Observation Deck");

        Assert.Contains("No section", manifest, StringComparison.Ordinal);
    }
}
