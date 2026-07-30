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
/// There is deliberately no <c>GetShipStatus</c> here. Hull, reactor, power, and section
/// integrity are ambient facts the ship already knows about itself, and milestone 2 injects
/// them through an <c>AIContextProvider</c> instead. Making the agent spend a tool call to
/// learn its own condition is the mistake that post 2 exists to demonstrate.
/// </para>
/// </remarks>
public sealed class SensorTools(ShipSimulation simulation)
{
    [Description("""
        Sweeps the current sector and returns every sensor contact: contact ID, designation,
        bearing in degrees, range in astronomical units, and drive signature. Use the returned
        contact IDs with AnalyseContact. Returns a clear-sector report if nothing is out there.
        """)]
    public string ScanSector()
    {
        var contacts = simulation.State.Contacts;

        if (contacts.Length == 0)
        {
            return "Sector sweep complete. No contacts. Background only.";
        }

        var report = new StringBuilder($"Sector sweep complete. {contacts.Length} contact(s):");

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
        condition, and anything anomalous. Requires a contact ID from ScanSector, for example 'C-1'.
        """)]
    public string AnalyseContact(
        [Description("The contact ID returned by ScanSector, for example 'C-1'.")] string contactId)
    {
        var contact = simulation.State.Contacts
            .FirstOrDefault(c => string.Equals(c.Id, contactId, StringComparison.OrdinalIgnoreCase));

        if (contact is null)
        {
            var known = simulation.State.Contacts.Select(c => c.Id).ToList();

            return known.Count == 0
                ? $"No contact '{contactId}' on the plot. There are no contacts at all — run ScanSector first."
                : $"No contact '{contactId}' on the plot. Current contacts: {string.Join(", ", known)}.";
        }

        return contact.Analysis is { Length: > 0 } analysis
            ? $"{contact.Id} — {contact.Designation}. {analysis}"
            : $"{contact.Id} — {contact.Designation}. Range {contact.Range:F1} AU is too great for a detailed profile. Close to under 5 AU for a full analysis.";
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
            if (state.FindSection(section) is null)
            {
                return $"No section named '{section}'. Sections: {string.Join(", ", state.Sections.Keys)}.";
            }

            var inSection = state.CrewIn(section).ToList();

            return inSection.Count == 0
                ? $"{section} is unoccupied."
                : $"{section}, {inSection.Count} crew: {string.Join("; ", inSection.Select(c => $"{c.Name} ({c.Role})"))}.";
        }

        var manifest = new StringBuilder($"Crew manifest — {state.LivingCrew.Count()} of {state.Crew.Length} alive:");

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

    /// <summary>Exposes these methods to the model as tools.</summary>
    public IEnumerable<AIFunction> AsAIFunctions() =>
    [
        AIFunctionFactory.Create(ScanSector),
        AIFunctionFactory.Create(AnalyseContact),
        AIFunctionFactory.Create(QueryCrewManifest),
        AIFunctionFactory.Create(ReadShipLog),
    ];
}
