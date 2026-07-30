using System.Text.Json;
using System.Text.Json.Serialization;
using ShipAI.Simulation;

namespace ShipAI.Simulation.Tests;

/// <summary>
/// Produces a canonical string for a <see cref="ShipState"/>.
/// </summary>
/// <remarks>
/// <see cref="ShipState"/> is a record, but its <c>ImmutableDictionary</c> and
/// <c>ImmutableArray</c> members compare by reference, so the compiler-generated
/// <c>Equals</c> reports two structurally identical states as different. Serialising is
/// the cheap way to get the comparison the determinism test actually needs.
/// </remarks>
internal static class StateDigest
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true,
    };

    public static string Of(ShipState state) => JsonSerializer.Serialize(state, Options);
}
