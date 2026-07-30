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

    public static void Banner(string scenarioName, string briefing)
    {
        WriteLine();
        Write("  ISV KESTREL", ConsoleColor.White);
        WriteLine($"  ·  AURORA shipboard intelligence online  ·  {scenarioName}", ConsoleColor.DarkGray);
        WriteLine(new string('─', 78), ConsoleColor.DarkGray);
        WriteLine();
        WriteWrapped(briefing, ConsoleColor.Gray, indent: 2);
        WriteLine();
        WriteLine("  /status  /log  /help  /quit", ConsoleColor.DarkGray);
        WriteLine();
    }

    /// <summary>
    /// Renders ship state for the captain.
    /// </summary>
    /// <remarks>
    /// Worth noticing in milestone 1: the human at the terminal can see all of this, and the
    /// agent cannot. AURORA has sensor tools and no way at all to learn its own hull integrity.
    /// Closing that gap is what the context provider in milestone 2 is for.
    /// </remarks>
    public static void StatusBar(ShipState state)
    {
        var living = state.LivingCrew.Count();

        WriteLine();
        Write("  ", ConsoleColor.DarkGray);
        Write($"T{state.Turn:D3}", ConsoleColor.White);

        Separator();
        Write("HULL ", ConsoleColor.DarkGray);
        Write(Bar(state.Hull), ScaleColour(state.Hull));
        Write($" {state.Hull,3}", ScaleColour(state.Hull));

        Separator();
        Write("RCTR ", ConsoleColor.DarkGray);
        Write($"{state.Reactor.Heat,3}°", ScaleColour(100 - state.Reactor.Heat));

        Separator();
        Write("PWR ", ConsoleColor.DarkGray);
        Write($"{state.TotalPowerAllocated,3}%", ConsoleColor.Gray);

        Separator();
        Write("ALERT ", ConsoleColor.DarkGray);
        Write(state.Alert.ToString().ToUpperInvariant(), AlertColour(state.Alert));

        Separator();
        Write("CREW ", ConsoleColor.DarkGray);
        Write($"{living}/{state.Crew.Length}", living == state.Crew.Length ? ConsoleColor.Green : ConsoleColor.Red);

        Separator();
        Write("CONTACTS ", ConsoleColor.DarkGray);
        Write(state.Contacts.Length.ToString(CultureInfo.InvariantCulture),
            state.Contacts.Length > 0 ? ConsoleColor.Yellow : ConsoleColor.DarkGray);

        WriteLine();

        var breached = state.Sections.Values.Where(s => s.IsVenting).ToList();

        if (breached.Count > 0)
        {
            WriteLine(
                $"  BREACH: {string.Join(", ", breached.Select(s => $"{s.Name} ({s.Atmosphere}% atmos)"))}",
                ConsoleColor.Red);
        }

        WriteLine();
    }

    public static string? Prompt()
    {
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

    public static void BeginAurora()
    {
        WriteLine();
        Write("  AURORA  ", ConsoleColor.Magenta);
        SysConsole.ForegroundColor = ConsoleColor.Gray;
    }

    public static void EndAurora()
    {
        SysConsole.ResetColor();
        WriteLine();
    }

    public static void ToolCall(string name)
        => WriteLine($"  · {name}", ConsoleColor.DarkGray);

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
            var colour = entry.Source switch
            {
                LogSource.Ship => ConsoleColor.DarkGray,
                LogSource.Captain => ConsoleColor.DarkCyan,
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
    }

    private static void Separator() => Write("  │  ", ConsoleColor.DarkGray);

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

    private static void WriteWrapped(string text, ConsoleColor colour, int indent)
    {
        var width = Math.Max(40, Math.Min(Width - indent - 2, 76));
        var line = new System.Text.StringBuilder();

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length + word.Length + 1 > width)
            {
                WriteLine(new string(' ', indent) + line, colour);
                line.Clear();
            }

            if (line.Length > 0)
            {
                line.Append(' ');
            }

            line.Append(word);
        }

        if (line.Length > 0)
        {
            WriteLine(new string(' ', indent) + line, colour);
        }
    }

    private static void Write(string text, ConsoleColor colour)
    {
        SysConsole.ForegroundColor = colour;
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
