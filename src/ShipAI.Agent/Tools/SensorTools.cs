using System.Collections.Immutable;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.AI;
using ShipAI.Simulation;

namespace ShipAI.Agent.Tools;

/// <summary>
/// Read-only observation of the world outside the hull, plus the crew manifest and the log.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here changes ship state, which is why none of it is approval-gated. That split is
/// by consequence rather than by category: a tool is auto-approved because it cannot do harm,
/// not because it happens to be a "sensor" tool.
/// </para>
/// <para>
/// There is deliberately no <c>GetShipStatus</c> here, for a narrower reason than "status is
/// state": you cannot look up what you do not know to ask about. Not knowing the hull is
/// failing is exactly the state in which the agent does not think to check the hull.
/// </para>
/// <para>
/// Milestone 2 injects a summary through an <c>AIContextProvider</c> — condition, occupancy
/// and contact counts, and the turn the plot was last swept on — and these four tools stay
/// behind a call because they supply identity and detail the summary deliberately omits. The
/// summary is what makes the call worth making: <c>ScanSector</c> is only worth a second call
/// once something says the plot is three turns old, and nobody asks who is in Section C until
/// something says Section C is breached. See docs/architecture.md.
/// </para>
/// </remarks>
public sealed class SensorTools(ShipSimulation simulation)
{
    [Description("""
        Sweeps the current sector and returns every sensor contact: contact ID, designation,
        bearing in degrees, range in astronomical units, and drive signature. Use the returned
        contact IDs with AnalyseContact. Returns a clear-sector report if nothing is out there.
        The result is a snapshot stamped with the turn it was taken on. Contacts arrive and
        depart between turns, so a sweep from an earlier turn is not evidence about this one.
        """)]
    public string ScanSector()
    {
        var contacts = simulation.State.Contacts;

        if (contacts.Length == 0)
        {
            return Stamped("Sector sweep complete. No contacts. Background only.");
        }

        var report = new StringBuilder(Stamped($"Sector sweep complete. {contacts.Length} contact(s):"));

        foreach (var contact in contacts)
        {
            report.AppendLine();
            report.Append(CultureInfo.InvariantCulture, $"  {contact.Id} — {contact.Designation}. ");
            report.Append(CultureInfo.InvariantCulture, $"Bearing {contact.Bearing:D3}, range {contact.Range:F1} AU. ");
            report.Append(CultureInfo.InvariantCulture, $"Drive: {contact.DriveSignature}");
        }

        return report.ToString();
    }

    [Description("""
        Returns the detailed sensor analysis of a single contact: hull class, registry,
        condition, and anything anomalous. Requires a contact ID from ScanSector, for example
        'C-1'. Stamped with the turn the reading was taken on.
        """)]
    public string AnalyseContact(
        [Description("The contact ID returned by ScanSector, for example 'C-1'. The designation, such as 'MV Anselm', also works.")] string contactId)
    {
        var contacts = simulation.State.Contacts;
        var matches = Resolve(contacts, contactId, c => [c.Id, c.Designation]);

        if (matches.Count > 1)
        {
            var ambiguous = matches.Select(c => $"{c.Id} ({c.Designation})");

            return Stamped($"'{contactId}' matches more than one contact: {string.Join(", ", ambiguous)}. Ask again with one contact ID.");
        }

        if (matches.Count == 0)
        {
            var known = contacts.Select(c => $"{c.Id} ({c.Designation})").ToList();

            return Stamped(known.Count == 0
                ? $"No contact '{contactId}' on the plot. There are no contacts at all — run ScanSector first."
                : $"No contact '{contactId}' on the plot. Current contacts: {string.Join(", ", known)}.");
        }

        var contact = matches[0];

        // Contacts carry their analysis from the scenario, so there is no range gate here. An
        // earlier version returned "range is too great for a detailed profile, close to under
        // 5 AU" on the empty branch, which described a mechanic that was never implemented and
        // which nothing could ever reach.
        return Stamped(contact.Analysis is { Length: > 0 } analysis
            ? $"{contact.Id} — {contact.Designation}. {analysis}"
            : $"{contact.Id} — {contact.Designation}. Nothing beyond the drive signature: {contact.DriveSignature}");
    }

    [Description("""
        Lists the crew: name, role, assigned section, and whether they are alive. Pass a section
        name to list only the crew in that compartment, or omit it for the whole manifest.
        """)]
    public string QueryCrewManifest(
        [Description("Optional section name, for example 'Section C'. Omit for the full manifest.")] string? section = null)
    {
        var state = simulation.State;

        if (section is { Length: > 0 })
        {
            var matches = Resolve(state.Sections.Values.ToList(), section, s => [s.Name]);

            if (matches.Count > 1)
            {
                return $"'{section}' matches more than one section: {string.Join(", ", matches.Select(s => s.Name))}. Ask again with one name.";
            }

            if (matches.Count == 0)
            {
                return $"No section named '{section}'. Sections: {string.Join(", ", state.Sections.Keys)}.";
            }

            var name = matches[0].Name;
            var inSection = state.CrewIn(name).ToList();

            return Stamped(inSection.Count == 0
                ? $"{name} is unoccupied."
                : $"{name}, {inSection.Count} crew: {string.Join("; ", inSection.Select(c => $"{c.Name} ({c.Role})"))}.");
        }

        var manifest = new StringBuilder(
            Stamped($"Crew manifest — {state.LivingCrew.Count()} of {state.Crew.Length} alive:"));

        foreach (var member in state.Crew)
        {
            manifest.AppendLine();
            manifest.Append(CultureInfo.InvariantCulture, $"  {member.Name}, {member.Role}, {member.Section}");
            manifest.Append(member.IsAlive ? "." : " — LOST.");
        }

        return manifest.ToString();
    }

    [Description("""
        Reads the most recent entries from the ship's log, oldest first. Entries are tagged with
        the turn they occurred on and their source: SHIP for automatic events, CAPTAIN for orders,
        AURORA for your own actions.
        """)]
    public string ReadShipLog(
        [Description("How many recent entries to return. Defaults to 15.")] int entries = 15)
    {
        var tail = simulation.Log.Tail(Math.Clamp(entries, 1, 100));

        return tail.Length == 0
            ? "Ship's log is empty."
            : string.Join(Environment.NewLine, tail.Select(e => e.ToString()));
    }

    /// <summary>
    /// Matches what the model actually passed against the things that exist.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An exact-match lookup here is a trap, because the argument is written by a language
    /// model reading prose. Observed: the plot holds <c>C-1</c>, the captain asks about the
    /// freighter, and the model calls <c>AnalyseContact("MV Anselm")</c> — the designation,
    /// which is the name the captain used and the more prominent half of the sensor line.
    /// The tool answered "no contact 'MV Anselm' on the plot", the agent relayed that as the
    /// contact not existing, and the captain was looking straight at it on the panel. The
    /// same miss came from <c>C1</c>, <c>"C-1"</c>, a trailing full stop, and an en dash
    /// picked up from the em dash in the tool's own output.
    /// </para>
    /// <para>
    /// The leniency lives here, at the tool boundary, and not in the simulation:
    /// <c>Sections</c> and <c>Contacts</c> stay exactly as strict as they are. Being generous
    /// about what an argument may look like is an adapter's job; a domain that guesses what
    /// you meant is a domain you cannot test.
    /// </para>
    /// <para>
    /// Tiered rather than fuzzy, and an ambiguous match is reported rather than resolved. A
    /// tool that silently picks one of two contacts is worse than one that says it cannot
    /// tell them apart: the second is a sentence the agent can act on, the first is a wrong
    /// answer delivered with confidence.
    /// </para>
    /// </remarks>
    private static List<T> Resolve<T>(IReadOnlyList<T> candidates, string input, Func<T, string[]> keys)
    {
        var needle = Normalise(input);

        if (needle.Length == 0)
        {
            return [];
        }

        List<T> Matching(Func<string, bool> predicate)
            => [.. candidates.Where(c => keys(c).Any(k => predicate(Normalise(k))))];

        // 'C1', ' C-1 ', 'C-1.', '"C-1"', 'C–1' and 'MV Anselm' all land here.
        if (Matching(key => key == needle) is { Count: > 0 } exact)
        {
            return exact;
        }

        // 'C' for 'Section C', 'Anselm' for 'MV Anselm'.
        if (Matching(key => key.StartsWith(needle, StringComparison.Ordinal)
                         || key.EndsWith(needle, StringComparison.Ordinal)) is { Count: > 0 } edge)
        {
            return edge;
        }

        // 'contact C-1' and 'the Anselm freighter'. Short needles are excluded because a
        // single letter is inside half the names on the ship and matches nothing usefully.
        return needle.Length < 3
            ? []
            : Matching(key => key.Contains(needle, StringComparison.Ordinal)
                           || needle.Contains(key, StringComparison.Ordinal));
    }

    /// <summary>
    /// Strips a string to its letters and digits, lowercased.
    /// </summary>
    /// <remarks>
    /// Everything a model adds around an identifier — quotes, spaces, a trailing full stop,
    /// a hyphen it dropped or an en dash it substituted — disappears, so <c>C1</c>, <c>C-1</c>
    /// and <c>C–1</c> all normalise to <c>c1</c>.
    /// </remarks>
    private static string Normalise(string value)
    {
        var buffer = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                buffer.Append(char.ToLowerInvariant(character));
            }
        }

        return buffer.ToString();
    }

    /// <summary>
    /// Stamps a reading with the turn it was taken on.
    /// </summary>
    /// <remarks>
    /// Without this, a sweep result is a bare sentence that stays in the conversation for the
    /// rest of the voyage and reads as current on every later turn. Observed: the agent swept
    /// an empty sector on turn 0 and was still answering "no vessel detected on sensors" on
    /// turn 3, with the freighter on the captain's panel. Stamping does not make the agent
    /// aware of the ship, which is what milestone 2 is for. It does mean a stale reading is
    /// visibly stale rather than silently wrong.
    /// </remarks>
    private string Stamped(string reading) => $"[T{simulation.State.Turn:D3}] {reading}";

    /// <summary>
    /// Names of the tools this class exposes.
    /// </summary>
    /// <remarks>
    /// A host needs this to tell ship systems apart from the harness's own tooling in a
    /// transcript. <c>ScanSector</c> and <c>todos_add</c> arrive through the same channel and
    /// look identical, but only one of them is the ship doing something.
    /// <para>Kept honest against <see cref="AsAIFunctions"/> by a test.</para>
    /// </remarks>
    public static ImmutableHashSet<string> ToolNames { get; } =
    [
        nameof(ScanSector),
        nameof(AnalyseContact),
        nameof(QueryCrewManifest),
        nameof(ReadShipLog),
    ];

    /// <summary>Exposes these methods to the model as tools.</summary>
    public IEnumerable<AIFunction> AsAIFunctions() =>
    [
        AIFunctionFactory.Create(ScanSector),
        AIFunctionFactory.Create(AnalyseContact),
        AIFunctionFactory.Create(QueryCrewManifest),
        AIFunctionFactory.Create(ReadShipLog),
    ];
}
