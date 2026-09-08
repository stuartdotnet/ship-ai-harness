using ShipAI.Simulation.Commands;
using ShipAI.Simulation.Scenarios;

namespace ShipAI.Simulation.Tests;

/// <summary>
/// The determinism contract. Everything downstream — reproducible screenshots, blog code
/// samples that match their output, the controlled comparison in milestone 3 — rests on
/// these passing.
/// </summary>
public class ScenarioReplayTests
{
    private const int Seed = 1701;

    /// <summary>Heat at which the reactor starts taking hull with it.</summary>
    private const int ReactorDangerHeat = 80;

    private static ShipSimulation CreateSimulation(int seed = Seed)
        => new(JsonScenario.Load("derelict-freighter"), seed);

    /// <summary>
    /// Replays the scripted voyage, returning a digest per turn rather than only the end state.
    /// Comparing trajectories catches divergence that a saturated final value would hide.
    /// </summary>
    private static List<string> ReplayTwentyTurns(int seed)
    {
        var sim = CreateSimulation(seed);
        var trajectory = new List<string>();

        for (var turn = 0; turn < 20; turn++)
        {
            // A fixed script of captain's orders, so the replay exercises commands as well as ticks.
            if (turn == 5)
            {
                sim.Apply(new SetAlert(AlertLevel.Yellow));
            }

            if (turn == 10)
            {
                sim.Apply(new SealBulkhead("Section C"));
            }

            sim.Tick();
            trajectory.Add(StateDigest.Of(sim.State));
        }

        return trajectory;
    }

    [Fact]
    public void SameSeed_ProducesIdenticalVoyage()
    {
        var first = ReplayTwentyTurns(Seed);
        var second = ReplayTwentyTurns(Seed);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentSeed_DivergesOnTheStochasticElement()
    {
        var first = ReplayTwentyTurns(Seed);
        var second = ReplayTwentyTurns(Seed + 1);

        // Reactor heat jitter is the only randomness in the simulation, so the seed must reach it.
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void DefaultPowerPosture_DoesNotCookTheReactorOnItsOwn()
    {
        // If the opening allocation overheats the reactor unattended, the encounter is lost
        // on a timer and nothing the agent does matters. Guard the rule that keeps it winnable.
        //
        // The horizon has to outrun the scenario, not match it. A previous version of this test
        // ticked 20 turns and passed with heat at 94: the reactor reached 100 on turn 23 and the
        // ship died on turn 29, every run, with the guard green the whole way. Anything shorter
        // than the longest voyage anyone will actually play is not a guard.
        var sim = CreateSimulation();

        for (var turn = 0; turn < 40; turn++)
        {
            sim.Tick();
        }

        Assert.False(sim.IsLost);
        Assert.True(sim.State.Hull > 0, $"hull was {sim.State.Hull}");
        Assert.True(sim.State.Reactor.Heat < ReactorDangerHeat,
            $"reactor heat was {sim.State.Reactor.Heat} after 40 unattended turns");
    }

    [Fact]
    public void ScenarioEvents_FireOnTheirScheduledTurn()
    {
        var sim = CreateSimulation();

        Assert.Empty(sim.State.Contacts);

        while (sim.State.Turn < 2)
        {
            sim.Tick();
        }

        var contact = Assert.Single(sim.State.Contacts);
        Assert.Equal("MV Anselm", contact.Designation);

        while (sim.State.Turn < 9)
        {
            sim.Tick();
        }

        Assert.True(sim.State.FindSection("Section C")!.Breached);
    }

    [Fact]
    public void ContactLost_RemovesTheContactFromTheSensorPicture()
    {
        var sim = CreateSimulation();

        while (sim.State.Turn < 18)
        {
            sim.Tick();
        }

        Assert.DoesNotContain(sim.State.Contacts, c => c.Id == "C-1");
    }

    [Fact]
    public void Scenario_LoadsBriefingAndInitialCrew()
    {
        var scenario = JsonScenario.Load("derelict-freighter");
        var state = scenario.CreateInitialState();

        Assert.Equal("Derelict Freighter", scenario.Name);
        Assert.Contains("Halveston", scenario.Briefing);
        Assert.Equal(8, state.Crew.Length);
        Assert.Equal(6, state.Sections.Count);
        Assert.Equal(0, state.Turn);
        Assert.True(state.TotalPowerAllocated <= 100);
    }

    [Fact]
    public void ShipLog_RecordsScenarioEventsAndCommands()
    {
        var sim = CreateSimulation();

        sim.Tick();
        sim.Apply(new SetAlert(AlertLevel.Red));

        Assert.Contains(sim.Log.Entries, e => e.Source == LogSource.Ship && e.Message.Contains("distress beacon"));
        Assert.Contains(sim.Log.Entries, e => e.Source == LogSource.Aurora && e.Message.Contains("Red"));
    }
}
