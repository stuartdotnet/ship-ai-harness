using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShipAI.Simulation.Scenarios;

/// <summary>
/// A scenario loaded from an embedded JSON definition.
/// </summary>
public sealed class JsonScenario : IScenario
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private readonly ScenarioDefinition _definition;

    private JsonScenario(ScenarioDefinition definition) => _definition = definition;

    public string Name => _definition.Name;

    public string Briefing => _definition.Briefing;

    public string Situation => _definition.Situation;

    public IReadOnlyList<ScenarioEvent> Events => _definition.Events;

    /// <summary>Loads a scenario by file name, e.g. <c>derelict-freighter</c>.</summary>
    public static JsonScenario Load(string name)
    {
        var resource = $"ShipAI.Simulation.Scenarios.{name}.json";
        using var stream = typeof(JsonScenario).GetTypeInfo().Assembly.GetManifestResourceStream(resource)
            ?? throw new FileNotFoundException($"No embedded scenario named '{name}'.", resource);

        var definition = JsonSerializer.Deserialize<ScenarioDefinition>(stream, SerializerOptions)
            ?? throw new InvalidDataException($"Scenario '{name}' deserialised to null.");

        return new JsonScenario(definition);
    }

    public ShipState CreateInitialState()
    {
        var initial = _definition.InitialState;

        return new ShipState
        {
            Hull = initial.Hull,
            Reactor = new ReactorState(initial.Reactor.Output, initial.Reactor.Heat),
            PowerAllocation = initial.PowerAllocation.ToImmutableDictionary(),
            Sections = initial.Sections.ToImmutableDictionary(
                s => s.Name,
                s => new ShipSection(s.Name, s.Atmosphere, s.Breached, s.BulkheadSealed),
                StringComparer.OrdinalIgnoreCase),
            Crew = [.. initial.Crew.Select(c => new CrewMember(c.Name, c.Role, c.Section))],
            Contacts = [.. initial.Contacts],
            Position = new Coordinates(initial.Position.X, initial.Position.Y, initial.Position.Z),
            Alert = initial.Alert,
            Turn = 0,
        };
    }

    private sealed record ScenarioDefinition
    {
        public required string Name { get; init; }
        public required string Briefing { get; init; }
        public required string Situation { get; init; }
        public required InitialStateDefinition InitialState { get; init; }
        public ImmutableArray<ScenarioEvent> Events { get; init; } = [];
    }

    private sealed record InitialStateDefinition
    {
        public required int Hull { get; init; }
        public required ReactorDefinition Reactor { get; init; }
        public required Dictionary<ShipSystem, int> PowerAllocation { get; init; }
        public required ImmutableArray<SectionDefinition> Sections { get; init; }
        public required ImmutableArray<CrewDefinition> Crew { get; init; }
        public ImmutableArray<Contact> Contacts { get; init; } = [];
        public required PositionDefinition Position { get; init; }
        public AlertLevel Alert { get; init; } = AlertLevel.Green;
    }

    private sealed record ReactorDefinition(int Output, int Heat);

    private sealed record SectionDefinition(string Name, int Atmosphere, bool Breached = false, bool BulkheadSealed = false);

    private sealed record CrewDefinition(string Name, string Role, string Section);

    private sealed record PositionDefinition(double X, double Y, double Z);
}
