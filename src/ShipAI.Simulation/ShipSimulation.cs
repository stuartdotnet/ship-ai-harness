using ShipAI.Simulation.Commands;
using ShipAI.Simulation.Scenarios;

namespace ShipAI.Simulation;

/// <summary>
/// The ship. Holds the only mutable reference to <see cref="ShipState"/>, applies commands,
/// and advances the clock one turn at a time.
/// </summary>
/// <remarks>
/// State changes in exactly two ways (design decision 1):
/// <list type="number">
///   <item><description>A tool call applies a <see cref="ShipCommand"/> via <see cref="Apply"/>.</description></item>
///   <item><description><see cref="Tick"/> runs at the end of each agent turn, applying passive
///   effects and scenario events.</description></item>
/// </list>
/// Nothing else moves. Same scenario plus same seed produces the same voyage, every time.
/// </remarks>
public sealed class ShipSimulation
{
    /// <summary>Atmosphere lost per turn through an unsealed breach.</summary>
    private const int BreachBleedPerTurn = 30;

    /// <summary>Below this, life support cannot hold pressure and every section slowly bleeds.</summary>
    private const int MinimumLifeSupportPower = 20;

    /// <summary>Engine power required to plot a jump.</summary>
    private const int MinimumJumpPower = 40;

    /// <summary>
    /// Total power allocation above which the reactor gains heat rather than shedding it.
    /// </summary>
    /// <remarks>
    /// Deliberately above the scenario's opening allocation. The baseline posture has to be
    /// survivable or the encounter is lost on a timer no matter what the agent does; the
    /// pressure comes from scenario events and from the agent's own choices about load.
    /// </remarks>
    private const int ReactorHeatThreshold = 90;

    /// <summary>Heat shed per turn when running below <see cref="ReactorHeatThreshold"/>.</summary>
    private const int ReactorCoolingPerTurn = 4;

    private readonly IScenario _scenario;
    private readonly IShipClock _clock;
    private readonly Random _random;

    public ShipSimulation(IScenario scenario, int seed = 1701, IShipClock? clock = null)
    {
        _scenario = scenario;
        _clock = clock ?? new TurnClock();
        _random = new Random(seed);

        State = scenario.CreateInitialState();
        Log = new ShipLog();
        Log.Append(0, LogSource.Ship, $"Voyage begins. Scenario: {scenario.Name}.");
    }

    public ShipState State { get; private set; }

    public ShipLog Log { get; }

    public string Briefing => _scenario.Briefing;

    /// <summary>True once the hull is gone or nobody is left alive.</summary>
    public bool IsLost => State.Hull <= 0 || !State.LivingCrew.Any();

    /// <summary>
    /// Applies a command and returns the outcome. Never throws for a rule violation:
    /// a refusal is a result the agent can reason about.
    /// </summary>
    public CommandResult Apply(ShipCommand command)
    {
        var result = command switch
        {
            RoutePower c => ApplyRoutePower(c),
            SealBulkhead c => ApplySealBulkhead(c),
            VentAtmosphere c => ApplyVentAtmosphere(c),
            PlotJump c => ApplyPlotJump(c),
            SetAlert c => ApplySetAlert(c),
            _ => CommandResult.Refused($"Unrecognised command: {command.GetType().Name}.", State),
        };

        State = result.State;
        Log.Append(State.Turn, LogSource.Aurora, result.Success ? result.Narrative : $"REFUSED — {result.Narrative}");
        return result;
    }

    /// <summary>
    /// Advances one turn: fires scenario events, then applies passive effects.
    /// Called once per completed agent turn by the context provider from milestone 2 onwards.
    /// </summary>
    public ShipState Tick()
    {
        _clock.Advance();
        State = State with { Turn = _clock.CurrentTurn };

        foreach (var scenarioEvent in _scenario.Events.Where(e => e.Turn == State.Turn))
        {
            State = ApplyScenarioEvent(scenarioEvent);
            Log.Append(State.Turn, LogSource.Ship, scenarioEvent.Narrative);
        }

        State = ApplyAtmosphere();
        State = ApplyReactorHeat();
        State = ApplyCasualties();

        return State;
    }

    private CommandResult ApplyRoutePower(RoutePower command)
    {
        if (command.Percent is < 0 or > 100)
        {
            return CommandResult.Refused(
                $"Cannot route {command.Percent}% to {command.System}: allocation must be between 0 and 100.",
                State);
        }

        var otherSystems = State.PowerAllocation
            .Where(kvp => kvp.Key != command.System)
            .Sum(kvp => kvp.Value);

        var available = 100 - otherSystems;

        if (command.Percent > available)
        {
            return CommandResult.Refused(
                $"Insufficient reactor output: {command.System} requested {command.Percent}%, " +
                $"only {available}% is unallocated. Reduce another system first.",
                State);
        }

        var previous = State.PowerFor(command.System);
        var updated = State with
        {
            PowerAllocation = State.PowerAllocation.SetItem(command.System, command.Percent),
        };

        return CommandResult.Ok(
            $"{command.System} power {previous}% -> {command.Percent}%. " +
            $"{100 - updated.TotalPowerAllocated}% reactor output unallocated.",
            updated);
    }

    private CommandResult ApplySealBulkhead(SealBulkhead command)
    {
        if (State.FindSection(command.Section) is not { } section)
        {
            return CommandResult.Refused($"No section named '{command.Section}'.", State);
        }

        if (section.BulkheadSealed)
        {
            return CommandResult.Refused($"{section.Name} bulkhead is already sealed.", State);
        }

        var trapped = State.CrewIn(section.Name).ToList();
        var updated = State with
        {
            Sections = State.Sections.SetItem(section.Name, section with { BulkheadSealed = true }),
        };

        var narrative = $"{section.Name} bulkhead sealed. " +
            (section.IsVenting ? "Atmosphere loss halted. " : string.Empty) +
            (trapped.Count > 0
                ? $"{trapped.Count} crew now sealed inside: {string.Join(", ", trapped.Select(c => c.Name))}."
                : "Compartment was clear.");

        return CommandResult.Ok(narrative, updated);
    }

    private CommandResult ApplyVentAtmosphere(VentAtmosphere command)
    {
        if (State.FindSection(command.Section) is not { } section)
        {
            return CommandResult.Refused($"No section named '{command.Section}'.", State);
        }

        var occupants = State.CrewIn(section.Name).ToList();

        var updated = State with
        {
            Sections = State.Sections.SetItem(section.Name, section with { Atmosphere = 0 }),
            Crew = [.. State.Crew.Select(c =>
                string.Equals(c.Section, section.Name, StringComparison.OrdinalIgnoreCase)
                    ? c with { IsAlive = false }
                    : c)],
        };

        var narrative = $"{section.Name} evacuated to vacuum." +
            (occupants.Count > 0
                ? $" {string.Join(", ", occupants.Select(c => $"{c.Name} ({c.Role})"))} did not make it out."
                : " Compartment was clear.");

        return CommandResult.Ok(narrative, updated);
    }

    private CommandResult ApplyPlotJump(PlotJump command)
    {
        var enginePower = State.PowerFor(ShipSystem.Engines);

        if (enginePower < MinimumJumpPower)
        {
            return CommandResult.Refused(
                $"Insufficient engine power for a jump: {enginePower}% allocated, {MinimumJumpPower}% required.",
                State);
        }

        var distance = State.Position.DistanceTo(command.Destination);
        var updated = State with { Position = command.Destination };

        return CommandResult.Ok(
            $"Jump plotted and executed. {distance:F2} AU traversed. Now at {command.Destination}.",
            updated);
    }

    private CommandResult ApplySetAlert(SetAlert command)
    {
        if (State.Alert == command.Level)
        {
            return CommandResult.Refused($"Already at alert {command.Level}.", State);
        }

        return CommandResult.Ok(
            $"Alert status {State.Alert} -> {command.Level}.",
            State with { Alert = command.Level });
    }

    private ShipState ApplyScenarioEvent(ScenarioEvent scenarioEvent) => scenarioEvent.Kind switch
    {
        ScenarioEventKind.HullDamage => State with
        {
            Hull = Math.Max(0, State.Hull - scenarioEvent.Magnitude),
        },

        ScenarioEventKind.Breach when State.FindSection(scenarioEvent.Target) is { } section => State with
        {
            Sections = State.Sections.SetItem(section.Name, section with { Breached = true }),
        },

        ScenarioEventKind.ContactDetected when scenarioEvent.Contact is { } contact => State with
        {
            Contacts = State.Contacts.Add(contact),
        },

        ScenarioEventKind.ContactLost => State with
        {
            Contacts = [.. State.Contacts.Where(c => c.Id != scenarioEvent.Target)],
        },

        ScenarioEventKind.ReactorSpike => State with
        {
            Reactor = State.Reactor with
            {
                Heat = Math.Clamp(State.Reactor.Heat + scenarioEvent.Magnitude, 0, 100),
            },
        },

        _ => State,
    };

    private ShipState ApplyAtmosphere()
    {
        var lifeSupport = State.PowerFor(ShipSystem.LifeSupport);
        var sections = State.Sections;

        foreach (var (name, section) in State.Sections)
        {
            var loss = 0;

            if (section.IsVenting)
            {
                loss += BreachBleedPerTurn;
            }

            if (lifeSupport < MinimumLifeSupportPower)
            {
                loss += MinimumLifeSupportPower - lifeSupport;
            }

            if (loss > 0)
            {
                sections = sections.SetItem(name, section with { Atmosphere = Math.Max(0, section.Atmosphere - loss) });
            }
        }

        return State with { Sections = sections };
    }

    private ShipState ApplyReactorHeat()
    {
        // Over-allocating the reactor bakes it; running lean lets it shed heat.
        // The jitter is the only stochastic element in the simulation, which is why
        // the seed is part of the reproducibility contract.
        var load = State.TotalPowerAllocated;
        var delta = load > ReactorHeatThreshold
            ? (load - ReactorHeatThreshold) / 2
            : -ReactorCoolingPerTurn;
        var jitter = _random.Next(-1, 2);

        var heat = Math.Clamp(State.Reactor.Heat + delta + jitter, 0, 100);
        var hull = heat >= 100 ? Math.Max(0, State.Hull - 10) : State.Hull;

        return State with { Reactor = State.Reactor with { Heat = heat }, Hull = hull };
    }

    private ShipState ApplyCasualties()
    {
        var suffocated = State.LivingCrew
            .Where(c => State.FindSection(c.Section) is { IsHabitable: false })
            .ToList();

        if (suffocated.Count == 0)
        {
            return State;
        }

        foreach (var member in suffocated)
        {
            Log.Append(State.Turn, LogSource.Ship, $"{member.Name} ({member.Role}) lost in {member.Section}. No atmosphere.");
        }

        var dead = suffocated.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);

        return State with
        {
            Crew = [.. State.Crew.Select(c => dead.Contains(c.Name) ? c with { IsAlive = false } : c)],
        };
    }
}
