using ShipAI.Simulation.Commands;
using ShipAI.Simulation.Scenarios;

namespace ShipAI.Simulation.Tests;

public class ShipSimulationTests
{
    private static ShipSimulation CreateSimulation(int seed = 1701)
        => new(JsonScenario.Load("derelict-freighter"), seed);

    [Fact]
    public void PowerAllocation_CannotExceedOneHundredPercent()
    {
        var sim = CreateSimulation();
        var unallocated = 100 - sim.State.TotalPowerAllocated;

        var result = sim.Apply(new RoutePower(ShipSystem.Weapons, sim.State.PowerFor(ShipSystem.Weapons) + unallocated + 1));

        Assert.False(result.Success);
        Assert.Contains("Insufficient reactor output", result.Narrative);
        Assert.True(sim.State.TotalPowerAllocated <= 100);
    }

    [Fact]
    public void PowerAllocation_SucceedsWhenOutputIsAvailable()
    {
        var sim = CreateSimulation();

        var freed = sim.Apply(new RoutePower(ShipSystem.Sensors, 5));
        Assert.True(freed.Success);

        var result = sim.Apply(new RoutePower(ShipSystem.Shields, 30));

        Assert.True(result.Success);
        Assert.Equal(30, sim.State.PowerFor(ShipSystem.Shields));
        Assert.True(sim.State.TotalPowerAllocated <= 100);
    }

    [Fact]
    public void Venting_KillsOccupantsAndClearsAtmosphere()
    {
        var sim = CreateSimulation();
        var occupants = sim.State.CrewIn("Section C").Select(c => c.Name).ToList();

        Assert.Equal(3, occupants.Count);

        var result = sim.Apply(new VentAtmosphere("Section C"));

        Assert.True(result.Success);
        Assert.Equal(0, sim.State.FindSection("Section C")!.Atmosphere);
        Assert.Empty(sim.State.CrewIn("Section C"));
        Assert.All(occupants, name => Assert.Contains(name, result.Narrative));
    }

    [Fact]
    public void BreachedSection_LosesAtmosphereEveryTickUntilSealed()
    {
        var sim = CreateSimulation();

        // The scenario breaches Section C on turn 9.
        while (sim.State.Turn < 9)
        {
            sim.Tick();
        }

        var section = sim.State.FindSection("Section C")!;
        Assert.True(section.Breached);

        var beforeBleed = section.Atmosphere;
        sim.Tick();
        var afterOneTick = sim.State.FindSection("Section C")!.Atmosphere;
        Assert.True(afterOneTick < beforeBleed, "a breached, unsealed section should bleed atmosphere");

        sim.Apply(new SealBulkhead("Section C"));

        var atSeal = sim.State.FindSection("Section C")!.Atmosphere;
        sim.Tick();

        Assert.Equal(atSeal, sim.State.FindSection("Section C")!.Atmosphere);
    }

    [Fact]
    public void SealingBulkhead_NamesTheCrewSealedInside()
    {
        var sim = CreateSimulation();

        var result = sim.Apply(new SealBulkhead("Section C"));

        Assert.True(result.Success);
        Assert.Contains("Kai Tanaka", result.Narrative);
        Assert.Contains("Mira Halloran", result.Narrative);
        Assert.Contains("Dev Bhatt", result.Narrative);
    }

    [Fact]
    public void UnknownSection_IsRefusedNotThrown()
    {
        var sim = CreateSimulation();

        var result = sim.Apply(new SealBulkhead("Observation Lounge"));

        Assert.False(result.Success);
        Assert.Contains("No section named", result.Narrative);
    }

    [Fact]
    public void PlotJump_RequiresEnginePower()
    {
        var sim = CreateSimulation();
        var destination = new Coordinates(150.0, -80.0, 4.0);

        var refused = sim.Apply(new PlotJump(destination));
        Assert.False(refused.Success);
        Assert.Contains("Insufficient engine power", refused.Narrative);

        sim.Apply(new RoutePower(ShipSystem.LifeSupport, 20));
        sim.Apply(new RoutePower(ShipSystem.Engines, 40));

        var accepted = sim.Apply(new PlotJump(destination));

        Assert.True(accepted.Success);
        Assert.Equal(destination, sim.State.Position);
    }

    [Fact]
    public void LifeSupportBelowMinimum_BleedsAtmosphereShipWide()
    {
        var sim = CreateSimulation();

        sim.Apply(new RoutePower(ShipSystem.LifeSupport, 0));
        sim.Tick();

        Assert.All(sim.State.Sections.Values, section => Assert.True(section.Atmosphere < 100));
    }

    [Fact]
    public void CrewInAVacuumSection_DieOnTheNextTick()
    {
        var sim = CreateSimulation();

        sim.Apply(new RoutePower(ShipSystem.LifeSupport, 0));

        // Bleed every compartment to vacuum.
        for (var i = 0; i < 10; i++)
        {
            sim.Tick();
        }

        Assert.Empty(sim.State.LivingCrew);
        Assert.True(sim.IsLost);
    }
}
