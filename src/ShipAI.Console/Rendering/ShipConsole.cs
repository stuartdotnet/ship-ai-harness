using System.Globalization;
using ShipAI.Simulation;
using SysConsole = System.Console;

namespace ShipAI.ConsoleHost.Rendering;

/// <summary>
/// Terminal presentation for the bridge.
/// </summary>
/// <remarks>
/// The harness ships no console host — <c>HarnessConsole.RunAgentAsync</c> exists in the
/// framework's samples, not in any NuGet package — so this is written by hand.
/// </remarks>
internal static class ShipConsole
{
    private const int BarWidth = 10;

    /// <summary>Indent for the body of a labelled status block, which sits under its own rule.</summary>
    private const int BlockIndent = 5;

    /// <summary>Columns taken by an event badge, so wrapped event text lines up under itself.</summary>
    private const int EventIndent = 15;

    /// <summary>Left gutter for AURORA output. Exactly the width of the "  AURORA  " label.</summary>
    private const string AuroraGutter = "          ";

    /// <summary>A stretch of panel text in a single colour. Several make up one status item.</summary>
    private readonly record struct Run(string Text, ConsoleColor Colour);

    /// <summary>Where the cursor sits within an AURORA reply.</summary>
    private enum AuroraLine
    {
        /// <summary>Just after the label, which the first output shares a line with.</summary>
        AtLabel,

        /// <summary>At the start of a fresh line, gutter not yet written.</summary>
        AtStart,

        /// <summary>Mid-line, with content already on it.</summary>
        Mid,
    }

    private static AuroraLine _auroraLine = AuroraLine.AtStart;

    /// <summary>Columns of model text written on the current line, gutter excluded.</summary>
    private static int _auroraColumn;

    /// <summary>The word being streamed, held back until its width is known.</summary>
    private static readonly System.Text.StringBuilder _auroraWord = new();

    /// <summary>A space seen but not yet written, so a wrap can discard it.</summary>
    private static bool _auroraPendingSpace;

    /// <summary>The state the panel drew last, whatever the turn.</summary>
    private static ShipState? _lastRendered;

    /// <summary>
    /// The state every delta is measured against: the last one drawn for an earlier turn.
    /// </summary>
    /// <remarks>
    /// Kept separate from <see cref="_lastRendered"/> so that <c>/status</c>, which redraws the
    /// turn already on screen, shows the same movement rather than reporting a flat ship.
    /// </remarks>
    private static ShipState? _previousTurn;

    /// <summary>Turn each contact first appeared on the plot, keyed by contact id.</summary>
    private static readonly Dictionary<string, int> _contactSighted = [];

    /// <summary>Turn each contact's analysis first came back, keyed by contact id.</summary>
    private static readonly Dictionary<string, int> _contactAnalysed = [];

    /// <summary>
    /// Orders that work on the opening turn, before anything is on the plot.
    /// </summary>
    private static readonly string[] ExampleOpeners =
    [
        "sweep the sector and tell me what's out there",
        "who's aboard, and where is everyone stationed?",
        "read me the log",
    ];

    public static void Banner(string scenarioName, string briefing)
    {
        WriteLine();
        Write("  ISV KESTREL", ConsoleColor.White);
        WriteLine($"  ·  AURORA shipboard intelligence online  ·  {scenarioName}", ConsoleColor.DarkGray);
        WriteLine($"  {new string('━', PanelWidth)}", ConsoleColor.DarkGray);
        WriteLine();
        WriteWrapped(briefing, ConsoleColor.Gray, indent: 2);
        WriteLine();
        WriteLine("  /status  /log  /help  /quit", ConsoleColor.DarkGray);
        WriteLine();
        Openers();
    }

    /// <summary>
    /// Example orders for a captain who has just started the app, and AURORA's scope.
    /// </summary>
    /// <remarks>
    /// Without this, the first thing a new captain sees is a bare prompt above four slash
    /// commands, which reads as though the slash commands are the whole interface. Anything
    /// not prefixed with '/' goes to AURORA as an order, and nothing on screen said so.
    /// <para>
    /// The scope line is here because leaving it out cost a whole playthrough. A captain who
    /// has just been told the ship is in trouble will order it saved, and every one of those
    /// orders comes back refused: AURORA has sensors and nothing else in this milestone. Saying
    /// so once, up front, is the difference between a deliberate constraint and a broken app.
    /// </para>
    /// </remarks>
    public static void Openers()
    {
        WriteLine("  Anything that is not a /command is an order to AURORA. Plain English.", ConsoleColor.DarkGray);
        WriteLine();

        foreach (var opener in ExampleOpeners)
        {
            WriteLine($"    {opener}", ConsoleColor.DarkCyan);
        }

        WriteLine();
        WriteLine("  AURORA runs sensors and analysis. It cannot steer, seal, route power or move", ConsoleColor.DarkGray);
        WriteLine("  crew: that is the bridge crew's job, and they are not listening to you here.", ConsoleColor.DarkGray);
        WriteLine("  Ask it what is out there and what it means. The sector is quiet on the", ConsoleColor.DarkGray);
        WriteLine("  opening turn. It does not stay that way.", ConsoleColor.DarkGray);
    }

    /// <summary>
    /// Renders the complete ship state for the captain, every turn.
    /// </summary>
    /// <remarks>
    /// Everything here is drawn straight from <see cref="ShipState"/> and never enters a prompt.
    /// The captain sees all of it; AURORA in milestone 1 sees none of it. That gap is the entire
    /// argument for the context provider in milestone 2, and it lands harder the more complete
    /// this panel is, so completeness here is a feature rather than a concession.
    /// </remarks>
    public static void StatusPanel(ShipState state)
    {
        if (_lastRendered is { } last && last.Turn != state.Turn)
        {
            _previousTurn = last;
        }

        var before = _previousTurn;

        WriteLine();
        PanelHeader(state, before);
        VitalsRow(state, before);
        PowerRow(state, before);
        Rule("COMPARTMENTS");
        CompartmentGrid(state, before);
        var living = state.LivingCrew.Count();

        Rule($"CREW  {living}/{state.Crew.Length}",
            DeltaRun(living, before?.LivingCrew.Count(), higherIsBetter: true));

        CrewRoster(state);
        Rule("CONTACTS");
        ContactList(state, before);
        WriteLine($"  {new string('━', PanelWidth)}", ConsoleColor.DarkGray);
        WriteLine();

        _lastRendered = state;
    }

    /// <summary>
    /// Narrates the scenario events that fired on the turn just completed.
    /// </summary>
    /// <remarks>
    /// These previously went to the ship's log and nowhere else, so the hull could drop fourteen
    /// points with no explanation on screen unless the captain thought to type /log. AURORA is
    /// deliberately not given this channel: it can reach the same entries through ReadShipLog,
    /// but only by choosing to spend a tool call on them.
    /// </remarks>
    public static void Events(IEnumerable<ShipLogEntry> entries)
    {
        foreach (var entry in entries.Where(e => e.Source is LogSource.Ship))
        {
            WriteLine();

            if (entry.Severity is LogSeverity.Routine)
            {
                Write("  · ", ConsoleColor.DarkGray);
                WriteWrapped(entry.Message, ConsoleColor.Gray, indent: 4, hanging: true);
                continue;
            }

            var (icon, label, foreground, background) = entry.Severity is LogSeverity.Critical
                ? ("🚨", "ALERT  ", ConsoleColor.White, ConsoleColor.Red)
                : ("🟠", "CAUTION", ConsoleColor.Black, ConsoleColor.DarkYellow);

            Write($"  {icon} ", foreground is ConsoleColor.White ? ConsoleColor.Red : ConsoleColor.DarkYellow);
            Write($" {label} ", foreground, background);
            Write(" ", ConsoleColor.DarkGray);

            WriteWrapped(entry.Message,
                entry.Severity is LogSeverity.Critical ? ConsoleColor.Red : ConsoleColor.Yellow,
                indent: EventIndent,
                hanging: true);
        }
    }

    /// <summary>
    /// Draws the turn number and the alert state, with the rule filling whatever is left.
    /// </summary>
    /// <remarks>
    /// The alert sits in the header rather than in a column of vitals because it is the one
    /// reading that should change how the captain reads everything below it. Letting the rule
    /// absorb the remaining width means the badge can be any length, which matters when its
    /// glyph count and its column count disagree.
    /// </remarks>
    private static void PanelHeader(ShipState state, ShipState? before)
    {
        Write("  ━━ ", ConsoleColor.DarkGray);
        Write($"T{state.Turn:D3}", ConsoleColor.White);
        Write(" ━━ ", ConsoleColor.DarkGray);

        // Everything after the two-space indent: "━━ ", the turn, " ━━ ", then the badge.
        var used = 3 + 4 + 4 + AlertBadge(state.Alert);

        // An alert that climbed this turn is the one thing on the panel the captain must not
        // scroll past, and a colour change alone survives neither a screenshot nor a
        // colourblind reader.
        if (before is { } previous && state.Alert > previous.Alert)
        {
            Write("  ▲ RAISED", AlertColour(state.Alert));
            used += 10;
        }

        Write(" ", ConsoleColor.DarkGray);
        WriteLine(new string('━', Math.Max(4, PanelWidth - used - 1)), ConsoleColor.DarkGray);
    }

    private static void VitalsRow(ShipState state, ShipState? before)
    {
        Write("     HULL     ", ConsoleColor.DarkGray);
        Write(Bar(state.Hull), ScaleColour(state.Hull));
        Write($" {state.Hull,3}", ScaleColour(state.Hull));
        Delta(state.Hull, before?.Hull, higherIsBetter: true);

        Write("      REACTOR  ", ConsoleColor.DarkGray);
        Write(Bar(state.Reactor.Heat), ScaleColour(100 - state.Reactor.Heat));
        Write($" {state.Reactor.Heat,3}°", ScaleColour(100 - state.Reactor.Heat));
        Delta(state.Reactor.Heat, before?.Reactor.Heat, higherIsBetter: false);

        WriteLine();
    }

    private static void PowerRow(ShipState state, ShipState? before)
    {
        Write("     POWER    ", ConsoleColor.DarkGray);
        Write($"{state.TotalPowerAllocated,3}%", ConsoleColor.White);
        Delta(state.TotalPowerAllocated, before?.TotalPowerAllocated, higherIsBetter: true);

        Write("   ·   ", ConsoleColor.DarkGray);

        var systems = state.PowerAllocation.OrderBy(entry => entry.Key).ToList();

        for (var i = 0; i < systems.Count; i++)
        {
            if (i > 0)
            {
                Write(" · ", ConsoleColor.DarkGray);
            }

            Write($"{ShortName(systems[i].Key)} ", ConsoleColor.DarkGray);
            Write(systems[i].Value.ToString(CultureInfo.InvariantCulture),
                systems[i].Value == 0 ? ConsoleColor.DarkGray : ConsoleColor.Cyan);
            Delta(systems[i].Value, before?.PowerFor(systems[i].Key), higherIsBetter: true);
        }

        WriteLine();
    }

    /// <summary>
    /// Lays the compartments out in two columns, filled down then across.
    /// </summary>
    private static void CompartmentGrid(ShipState state, ShipState? before)
    {
        var sections = state.Sections.Values
            .OrderBy(section => section.Name, StringComparer.Ordinal)
            .ToList();

        var rows = (sections.Count + 1) / 2;

        // Wide enough for the longest cell (name, atmosphere, delta and status word) and no
        // wider: on a full-screen terminal an evenly split panel puts the two columns so far
        // apart that they stop reading as a pair.
        var columnWidth = Math.Min(36, (PanelWidth - BlockIndent) / 2);

        for (var row = 0; row < rows; row++)
        {
            SysConsole.Write(new string(' ', BlockIndent));
            var written = CompartmentCell(sections[row], before);

            if (row + rows < sections.Count)
            {
                SysConsole.Write(new string(' ', Math.Max(2, columnWidth - written)));
                CompartmentCell(sections[row + rows], before);
            }

            WriteLine();
        }
    }

    /// <summary>Writes one compartment and returns the columns it occupied.</summary>
    private static int CompartmentCell(ShipSection section, ShipState? before)
    {
        var (icon, colour, status) = section switch
        {
            { IsVenting: true } => ("🔥", ConsoleColor.Red, "VENTING"),
            { IsVented: true } => ("💀", ConsoleColor.DarkRed, "VENTED"),
            { Breached: true } => ("🔒", ConsoleColor.Yellow, "SEALED"),
            { Atmosphere: < 100 } => ("🟠", ConsoleColor.Yellow, ""),
            _ => ("✅", ConsoleColor.Gray, ""),
        };

        Write($"{icon} ", colour);
        Write($"{section.Name,-14}", colour);
        Write($"{section.Atmosphere,3}", colour);

        var width = 3 + 14 + 3;

        if (DeltaRun(section.Atmosphere, before?.FindSection(section.Name)?.Atmosphere,
                higherIsBetter: true) is { } movement)
        {
            Write(movement.Text, movement.Colour);
            width += Columns(movement.Text);
        }

        if (status.Length > 0)
        {
            Write($"  {status}", colour);
            width += 2 + status.Length;
        }

        return width;
    }

    /// <summary>
    /// Lists the crew one compartment per line, then the dead one per line beneath.
    /// </summary>
    /// <remarks>
    /// A roster flowed inline saved four lines and cost the captain the ability to answer
    /// "who is in the compartment that is venting" at a glance, which is the only question
    /// the roster exists to answer.
    /// </remarks>
    private static void CrewRoster(ShipState state)
    {
        var stations = state.LivingCrew
            .GroupBy(member => member.Section)
            .OrderBy(group => group.Key, StringComparer.Ordinal);

        foreach (var station in stations)
        {
            var section = state.FindSection(station.Key);
            var atRisk = section is { Atmosphere: < 100 };

            SysConsole.Write(new string(' ', BlockIndent));
            Write($"{station.Key,-16}", atRisk ? ConsoleColor.Red : ConsoleColor.DarkGray);
            Write(string.Join(", ", station.Select(member => member.Name)),
                atRisk ? ConsoleColor.Red : ConsoleColor.Gray);

            if (section is { IsVenting: true })
            {
                Write("   🔥 VENTING", ConsoleColor.Red);
            }
            else if (atRisk)
            {
                Write("   🟠 FALLING", ConsoleColor.Yellow);
            }

            WriteLine();
        }

        foreach (var member in state.Crew.Where(member => !member.IsAlive))
        {
            SysConsole.Write(new string(' ', BlockIndent));
            Write("💀 ", ConsoleColor.DarkRed);
            Write($"{member.Name,-18}", ConsoleColor.DarkRed);
            WriteLine(member.Role, ConsoleColor.DarkGray);
        }
    }

    /// <summary>
    /// Renders the plot: one line per contact, with prose only on the turn it is first learned.
    /// </summary>
    /// <remarks>
    /// The drive signature and the analysis are news exactly once. Reprinting them under every
    /// contact for the rest of the voyage pushed the panel past a screen and trained the captain
    /// to skip the block, which is the opposite of what a plot is for. Both are keyed to the
    /// turn they arrived rather than to a "seen" flag, so <c>/status</c> redraws the turn it is
    /// looking at rather than silently swallowing the description.
    /// </remarks>
    private static void ContactList(ShipState state, ShipState? before)
    {
        if (state.Contacts.Length == 0)
        {
            WriteLine($"{new string(' ', BlockIndent)}   nothing on the plot", ConsoleColor.DarkGray);
            return;
        }

        foreach (var contact in state.Contacts)
        {
            var previous = before?.Contacts.FirstOrDefault(c => c.Id == contact.Id);

            SysConsole.Write(new string(' ', BlockIndent));
            Write("📡 ", ConsoleColor.Yellow);
            Write($"{contact.Id,-5}", ConsoleColor.Yellow);
            Write($"{contact.Designation,-26}", ConsoleColor.White);
            Write($"bearing {contact.Bearing:D3}   ", ConsoleColor.DarkGray);
            Write($"{contact.Range.ToString("F1", CultureInfo.InvariantCulture)} AU", ConsoleColor.Gray);

            // Closing range is the only movement on the plot that changes what the captain
            // should do about it, so it is worth the arrow even at a tenth of an AU.
            if (previous is not null && Math.Abs(previous.Range - contact.Range) >= 0.05)
            {
                var change = contact.Range - previous.Range;
                Write(
                    $" {(change < 0 ? "▼" : "▲")}{Math.Abs(change).ToString("F1", CultureInfo.InvariantCulture)}",
                    change < 0 ? ConsoleColor.Red : ConsoleColor.Green);
            }

            var analysed = contact.Analysis is { Length: > 0 };

            if (analysed && FirstSeen(_contactAnalysed, contact.Id, state.Turn) != state.Turn)
            {
                Write("   analysed", ConsoleColor.DarkCyan);
            }

            WriteLine();

            if (FirstSeen(_contactSighted, contact.Id, state.Turn) == state.Turn)
            {
                WriteWrapped(contact.DriveSignature, ConsoleColor.DarkGray, indent: BlockIndent + 3);
            }

            if (analysed && FirstSeen(_contactAnalysed, contact.Id, state.Turn) == state.Turn)
            {
                WriteWrapped(contact.Analysis!, ConsoleColor.DarkCyan, indent: BlockIndent + 3);
            }
        }
    }

    /// <summary>The turn a contact reached a given state, recording it the first time it did.</summary>
    private static int FirstSeen(Dictionary<string, int> record, string id, int turn)
    {
        if (!record.TryGetValue(id, out var first))
        {
            first = turn;
            record[id] = first;
        }

        return first;
    }

    /// <summary>Draws a group heading: a label, an optional delta, then a rule to the edge.</summary>
    private static void Rule(string label, Run? trailing = null)
    {
        Write("  ── ", ConsoleColor.DarkGray);
        Write(label, ConsoleColor.DarkCyan);

        // "── " and the trailing space, which the label sits between.
        var used = 4 + label.Length;

        if (trailing is { } run)
        {
            Write(run.Text, run.Colour);
            used += Columns(run.Text);
        }

        Write(" ", ConsoleColor.DarkGray);
        WriteLine(new string('─', Math.Max(4, PanelWidth - used)), ConsoleColor.DarkGray);
    }

    /// <summary>
    /// Writes the movement in a value since the previous turn, or nothing if it held steady.
    /// </summary>
    private static void Delta(int now, int? before, bool higherIsBetter)
    {
        if (DeltaRun(now, before, higherIsBetter) is { } run)
        {
            Write(run.Text, run.Colour);
        }
    }

    private static Run? DeltaRun(int now, int? before, bool higherIsBetter)
    {
        if (before is not { } previous || now == previous)
        {
            return null;
        }

        var change = now - previous;
        var improving = higherIsBetter ? change > 0 : change < 0;

        return new Run(
            $" {(change > 0 ? "▲" : "▼")}{Math.Abs(change).ToString(CultureInfo.InvariantCulture)}",
            improving ? ConsoleColor.Green : ConsoleColor.Red);
    }

    private static string ShortName(ShipSystem system) => system switch
    {
        ShipSystem.LifeSupport => "Life",
        ShipSystem.Sensors => "Sens",
        _ => system.ToString(),
    };


    /// <summary>
    /// Reads the captain's next order, preceded by a short reminder of what can be said.
    /// </summary>
    /// <remarks>
    /// The lead suggestion tracks the plot, so it never proposes analysing a contact that does
    /// not exist yet.
    /// </remarks>
    public static string? Prompt(ShipState state)
    {
        Hint(state);

        Write("  CAPTAIN ", ConsoleColor.Cyan);
        Write("› ", ConsoleColor.DarkGray);
        SysConsole.ForegroundColor = ConsoleColor.White;

        try
        {
            return SysConsole.ReadLine();
        }
        finally
        {
            SysConsole.ResetColor();
        }
    }

    private static void Hint(ShipState state)
    {
        var lead = state.Contacts.Length > 0
            ? $"analyse {state.Contacts[0].Id}"
            : "sweep the sector";

        WriteLine($"  try: {lead}  ·  who's aboard  ·  read the log  ·  /help", ConsoleColor.DarkGray);
    }

    /// <summary>
    /// Opens an AURORA reply. The label doubles as a left gutter: everything that follows,
    /// tool announcements and streamed text alike, aligns underneath it.
    /// </summary>
    public static void BeginAurora()
    {
        WriteLine();
        Write("  AURORA  ", ConsoleColor.Magenta);
        _auroraLine = AuroraLine.AtLabel;
        _auroraColumn = 0;
        _auroraPendingSpace = false;
        _auroraWord.Clear();
    }

    /// <summary>
    /// Announces a tool call on its own line.
    /// </summary>
    /// <remarks>
    /// Breaks the current line first when the model is mid-sentence, which it frequently is:
    /// tool calls arrive interleaved with streamed text, not tidily before it.
    /// </remarks>
    /// <summary>
    /// Announces a tool call, distinguishing ship systems from the harness's own tooling.
    /// </summary>
    /// <remarks>
    /// <c>ScanSector</c> and <c>todos_add</c> arrive on the same channel and rendered the same
    /// way they read as equivalent, which flatters the transcript: several turns of the agent
    /// shuffling its todo list look like several turns of it working the ship. The prefix
    /// survives a piped capture, where colour does not.
    /// </remarks>
    public static void ToolCall(string name, bool isShipSystem)
    {
        ToolCall(isShipSystem ? name : $"harness/{name}");
    }

    private static void ToolCall(string name)
    {
        FlushWord();

        if (_auroraLine is AuroraLine.Mid)
        {
            SysConsole.WriteLine();
            _auroraLine = AuroraLine.AtStart;
        }

        if (_auroraLine is AuroraLine.AtStart)
        {
            SysConsole.Write(AuroraGutter);
        }

        WriteLine($"· {name}", ConsoleColor.DarkGray);
        _auroraLine = AuroraLine.AtStart;
        _auroraColumn = 0;
        _auroraPendingSpace = false;
    }

    /// <summary>
    /// Writes streamed model text, wrapped to the panel width and aligned under the label.
    /// </summary>
    /// <remarks>
    /// Wrapping a stream means never seeing the end of the current word, so words are buffered
    /// and measured at the space that terminates them. Leaving the terminal to soft-wrap is the
    /// obvious alternative and looks wrong: it breaks mid-word, ignores the gutter, and on a
    /// wide window runs a reply out past the edge of the status panel it sits under.
    /// <para>
    /// The gutter is written lazily, when content actually turns up rather than when the
    /// newline does, so a reply never leaves a line of trailing spaces in a piped transcript.
    /// </para>
    /// </remarks>
    public static void Aurora(string text)
    {
        foreach (var character in text)
        {
            switch (character)
            {
                case '\r':
                    break;
                case '\n':
                    FlushWord();
                    SysConsole.WriteLine();
                    _auroraLine = AuroraLine.AtStart;
                    _auroraColumn = 0;
                    _auroraPendingSpace = false;
                    break;
                case ' ' or '\t':
                    FlushWord();

                    // Dropped at the start of a line, so a wrap never leaves a leading space.
                    _auroraPendingSpace = _auroraLine is not AuroraLine.AtStart;
                    break;
                default:
                    _auroraWord.Append(character);
                    break;
            }
        }
    }

    /// <summary>Writes the buffered word, breaking the line first if it will not fit.</summary>
    private static void FlushWord()
    {
        if (_auroraWord.Length == 0)
        {
            return;
        }

        var word = _auroraWord.ToString();
        _auroraWord.Clear();

        var onALine = _auroraLine is not AuroraLine.AtStart;
        var needed = word.Length + (_auroraPendingSpace && onALine ? 1 : 0);

        if (onALine && _auroraColumn + needed > AuroraWidth)
        {
            SysConsole.WriteLine();
            _auroraLine = AuroraLine.AtStart;
            _auroraColumn = 0;
            _auroraPendingSpace = false;
        }

        if (_auroraLine is AuroraLine.AtStart)
        {
            SysConsole.Write(AuroraGutter);
        }
        else if (_auroraPendingSpace)
        {
            SysConsole.Write(' ');
            _auroraColumn++;
        }

        _auroraPendingSpace = false;

        SysConsole.ForegroundColor = ConsoleColor.Gray;
        SysConsole.Write(word);
        SysConsole.ResetColor();

        _auroraColumn += word.Length;
        _auroraLine = AuroraLine.Mid;
    }

    public static void EndAurora()
    {
        FlushWord();
        SysConsole.ResetColor();

        // Nothing to close when the reply ended on a tool call or a newline. Adding one here
        // would give the status panel a second blank line above it.
        if (_auroraLine is not AuroraLine.AtStart)
        {
            WriteLine();
        }

        _auroraLine = AuroraLine.AtStart;
        _auroraColumn = 0;
        _auroraPendingSpace = false;
    }

    public static void System(string message)
        => WriteLine($"  {message}", ConsoleColor.DarkYellow);

    public static void Error(string message)
    {
        WriteLine();
        WriteLine($"  {message}", ConsoleColor.Red);
        WriteLine();
    }

    public static void Log(ShipLog log, int entries)
    {
        WriteLine();

        foreach (var entry in log.Tail(entries))
        {
            var colour = (entry.Source, entry.Severity) switch
            {
                (LogSource.Ship, LogSeverity.Critical) => ConsoleColor.Red,
                (LogSource.Ship, LogSeverity.Warning) => ConsoleColor.Yellow,
                (LogSource.Ship, _) => ConsoleColor.DarkGray,
                (LogSource.Captain, _) => ConsoleColor.DarkCyan,
                _ => ConsoleColor.DarkMagenta,
            };

            WriteLine($"  {entry}", colour);
        }

        WriteLine();
    }

    public static void Help()
    {
        WriteLine();
        WriteLine("  /status   ship state as the captain sees it", ConsoleColor.DarkGray);
        WriteLine("  /log      recent ship's log entries", ConsoleColor.DarkGray);
        WriteLine("  /help     this list", ConsoleColor.DarkGray);
        WriteLine("  /quit     end the voyage", ConsoleColor.DarkGray);
        WriteLine();
        Openers();
        WriteLine();
    }

    private static string Bar(int percent)
    {
        var filled = (int)Math.Round(Math.Clamp(percent, 0, 100) / 100.0 * BarWidth);
        return new string('█', filled) + new string('░', BarWidth - filled);
    }

    private static ConsoleColor ScaleColour(int percent) => percent switch
    {
        >= 70 => ConsoleColor.Green,
        >= 35 => ConsoleColor.Yellow,
        _ => ConsoleColor.Red,
    };

    private static ConsoleColor AlertColour(AlertLevel level) => level switch
    {
        AlertLevel.Green => ConsoleColor.Green,
        AlertLevel.Yellow => ConsoleColor.Yellow,
        _ => ConsoleColor.Red,
    };

    /// <summary>
    /// Draws the alert level as a filled badge rather than coloured text.
    /// </summary>
    /// <remarks>
    /// Foreground colour alone put RED and GREEN at the same visual weight, which is the wrong
    /// way round: the whole point of the indicator is that a raised alert should be impossible
    /// to read past. Returns the columns it occupied so the header rule can fill the rest.
    /// </remarks>
    private static int AlertBadge(AlertLevel level)
    {
        var (icon, foreground, background) = level switch
        {
            AlertLevel.Green => ("🟢", ConsoleColor.Black, ConsoleColor.DarkGreen),
            AlertLevel.Yellow => ("🟡", ConsoleColor.Black, ConsoleColor.DarkYellow),
            _ => ("🔴", ConsoleColor.White, ConsoleColor.Red),
        };

        var text = $" {level.ToString().ToUpperInvariant()} ";

        Write($"{icon} ", AlertColour(level));
        Write(text, foreground, background);

        return 3 + text.Length;
    }

    /// <summary>
    /// Display width of a string in terminal cells.
    /// </summary>
    /// <remarks>
    /// <see cref="string.Length"/> counts UTF-16 units, which is wrong in both directions for
    /// the panel's glyphs: "🔥" is two units and two cells, "✅" is one unit and two cells.
    /// Padding computed from Length drifts either way, so every emoji on the panel is drawn
    /// from a set that is unambiguously double-width and counted as such here.
    /// </remarks>
    private static int Columns(string text)
    {
        var width = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]))
            {
                width += 2;
                i++;
            }
            else
            {
                width += text[i] is '✅' ? 2 : 1;
            }
        }

        return width;
    }

    /// <summary>
    /// Usable text width, safe when output is redirected.
    /// </summary>
    /// <remarks>
    /// <c>Console.WindowWidth</c> throws <see cref="IOException"/> ("The handle is invalid")
    /// whenever stdout is not a terminal — piping the app, recording it, or running it in CI.
    /// </remarks>
    private static int Width
    {
        get
        {
            if (SysConsole.IsOutputRedirected)
            {
                return 80;
            }

            try
            {
                return SysConsole.WindowWidth;
            }
            catch (IOException)
            {
                return 80;
            }
        }
    }

    /// <summary>
    /// Width of the status panel, in columns.
    /// </summary>
    /// <remarks>
    /// Follows the terminal so a wide window stops the compartment grid and the crew roster
    /// wrapping, but stops at 110: a rule drawn across an ultrawide monitor is harder to read
    /// than a short one, not easier.
    /// </remarks>
    private static int PanelWidth => Math.Clamp(Width - 4, 74, 110);

    /// <summary>
    /// Columns available to model text, so a reply ends on the same column as the panel rules.
    /// </summary>
    private static int AuroraWidth => PanelWidth - AuroraGutter.Length + 2;

    /// <summary>
    /// Writes wrapped text at a fixed indent.
    /// </summary>
    /// <param name="hanging">
    /// When true the first line starts at the cursor rather than at the indent, so text can run
    /// on from a badge already written to the left of it.
    /// </param>
    private static void WriteWrapped(string text, ConsoleColor colour, int indent, bool hanging = false)
    {
        var width = Math.Max(40, Math.Min(Width - indent - 2, PanelWidth - indent));
        var line = new System.Text.StringBuilder();
        var first = true;

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length + word.Length + 1 > width)
            {
                WriteLine((first && hanging ? string.Empty : new string(' ', indent)) + line, colour);
                line.Clear();
                first = false;
            }

            if (line.Length > 0)
            {
                line.Append(' ');
            }

            line.Append(word);
        }

        if (line.Length > 0)
        {
            WriteLine((first && hanging ? string.Empty : new string(' ', indent)) + line, colour);
        }
    }

    private static void Write(string text, ConsoleColor colour)
    {
        SysConsole.ForegroundColor = colour;
        SysConsole.Write(text);
        SysConsole.ResetColor();
    }

    private static void Write(string text, ConsoleColor colour, ConsoleColor background)
    {
        SysConsole.ForegroundColor = colour;
        SysConsole.BackgroundColor = background;
        SysConsole.Write(text);
        SysConsole.ResetColor();
    }

    private static void WriteLine(string text = "", ConsoleColor? colour = null)
    {
        if (colour is { } c)
        {
            SysConsole.ForegroundColor = c;
        }

        SysConsole.WriteLine(text);
        SysConsole.ResetColor();
    }
}
