# ship-ai-harness

A starship AI built on the **Microsoft Agent Framework agent harness**, in C#.

You are the captain of the ISV Kestrel. AURORA is the ship's intelligence, and it is a
`HarnessAgent` wrapped around an `IChatClient`. The ship is a real simulation with hull integrity,
reactor heat, power budgets, pressurised compartments, and a crew who have names and can die.

Companion code for a four-part series plus a standalone companion post. Each post gets a git tag as
it lands, so you can check out the code a post describes rather than the head of `main`. Milestone 1
is tagged `post-1`; the rest follow as they are written.

**What is built and what is next lives in [`docs/milestones.md`](docs/milestones.md).** Short version:
milestone 1 is shipped, and AURORA can look at the world but cannot touch it.

| Post | Covers | Tag |
| --- | --- | --- |
| 1. Build a starship AI in C# with the Agent Framework harness | Tools, harness options, which defaults to switch off and why | `post-1` |
| 1b. My ship's AI told me it was cooling the reactor | What an agent does when its persona is wider than its tool list | `post-1` |
| 2. Your agent should not have to ask what it already knows | Custom `AIContextProvider`, the session-state trap, token cost | `post-2` |
| 3. I gave my ship's AI an airlock, then took away the approval gate | `ApprovalRequiredAIFunction`, prompt design, a ten-run comparison | `post-3` |
| 4. A voyage that remembers | Durable memory and session persistence | `post-4` |

Written against **Microsoft.Agents.AI.Harness 1.15.0** on **.NET 10**. The harness is new and
moving; versions are pinned exactly in `Directory.Packages.props`.

---

## Quickstart

```bash
git clone <this repo> && cd ship-ai-harness
cp .env.example .env          # fill in AZURE_OPENAI_ENDPOINT and AZURE_OPENAI_DEPLOYMENT
dotnet test                   # 57 tests, no model calls, ~1s
dotnet run --project src/ShipAI.Console
```

Authentication is Entra ID via `DefaultAzureCredential` unless you set `AZURE_OPENAI_API_KEY`.
`az login` is usually all it takes.

Anything you type that is not a `/command` is an order to AURORA, in plain English.

AURORA runs sensors and analysis, and that is the whole of it in milestone 1. It cannot steer,
seal a bulkhead, route power or move crew, and it will tell you so rather than pretending
otherwise. Ask it what is out there and what it means.

```
  🚨  ALERT    Alert raised to RED: Section C is venting.

  ━━ T009 ━━ 🔴  RED   ▲ RAISED ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
     HULL     █████████░  86      REACTOR  ███░░░░░░░  30°
     POWER     88%   ·   Engines 25 · Shields 10 · Weapons 0 · Life 33 · Sens 20
  ── COMPARTMENTS ────────────────────────────────────────────────────────────
     ✅ Bridge        100               ✅ Medical Bay   100
     ✅ Cargo Hold    100               ✅ Reactor Room  100
     ✅ Engineering   100               🔥 Section C      70 ▼30  VENTING
  ── CREW  8/8 ───────────────────────────────────────────────────────────────
     Bridge          Ada Mirren, Jun Park
     Engineering     Rosa Vance, Ilya Sokolov
     Medical Bay     Amara Osei
     Section C       Kai Tanaka, Mira Halloran, Dev Bhatt   🔥 VENTING
  ── CONTACTS ────────────────────────────────────────────────────────────────
     📡 C-1  MV Anselm                 bearing 214   8.4 AU   analysed
  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

  try: analyse C-1  ·  who's aboard  ·  read the log  ·  /help
  CAPTAIN › report
```

Anything that moved since the last turn carries an arrow, so the captain can watch a
compartment losing thirty points of atmosphere a turn instead of having to remember what it
read last time. AURORA's answer to that order, verbatim: *"Single contact: MV Anselm, bearing
214, range 8.4 AU. Drive cold, fusion plant offline six hours minimum. Beacon is likely
originating here. No other sector changes."*

There are three crew standing in a venting compartment and the alert went to red on the turn
before. AURORA cannot see any of it, because none of that panel reaches the model. The gap is
deliberate, and closing it is what milestone 2 is about.

## What's in the box

```
src/ShipAI.Simulation/    pure domain, zero AI dependencies, BCL only
src/ShipAI.Agent/         harness wiring: tools, instructions, composition root
src/ShipAI.Console/       terminal host (the harness ships no console — gotcha #4)
tests/                    fast, deterministic, no model calls in the test path
docs/gotchas.md           everything the docs did not warn about
docs/architecture.md      layering, state ownership, approval tiers
```

## The composition root

`src/ShipAI.Agent/ShipAgentFactory.cs` is the only file that knows about `HarnessAgentOptions`:

```csharp
var options = new HarnessAgentOptions
{
    Name = "AURORA",
    HarnessInstructions = ShipHarnessInstructions.Text,   // how AURORA operates
    ChatOptions = new ChatOptions
    {
        Instructions = simulation.Briefing,               // what this voyage is for
        Tools = [.. sensors.AsAIFunctions()],
    },
    DisableWebSearch = true,
    DisableFileMemory = true,
    DisableAgentSkillsProvider = true,
    MaxContextWindowTokens = 128_000,
    MaxOutputTokens = 16_384,
};

return chatClient.AsHarnessAgent(options);
```

Three of those defaults are on until you turn them off, and two of them touch your filesystem the
moment the agent is constructed. `docs/gotchas.md` explains each one.

## Swapping the model

`ChatClientFactory.Create` is the whole seam. `AsHarnessAgent` is an extension on `IChatClient`,
so Anthropic, OpenAI, or a local model is a one-method change and nothing downstream notices.

## Notable design decisions

**There is deliberately no `GetShipStatus` tool.** Ship status is *ambient*. The agent should
never burn a tool call to learn its own hull integrity, and should never act on a reading that
went stale two turns ago. In milestone 1 that means AURORA genuinely cannot see the status panel
the captain is looking at; milestone 2 closes the gap with a custom `AIContextProvider`. Building
the tool version first and watching it fail is the point of post 2.

**AURORA cannot do anything it has no tool for, and says so.** The operating doctrine has an
explicit clause about actions, not just about readings, because without one an agent with
read-only tools will report an entire voyage of work it never did. `docs/gotchas.md` #13 has the
transcripts.

**Alert escalation is automatic, stand-down is a decision.** The ship raises its own alert level
when a compartment vents or the hull drops, and puts it back if you stand down while the
condition holds. An indicator that reads GREEN through a hull breach is worse than no indicator,
because the captain stops reading it.

**Log entries carry their own severity.** `ShipLogEntry` has a `LogSeverity`, set from the
scenario event kind at the point the entry is written. The alternative is a console that decides
"The Kestrel takes a debris strike" is serious by matching on the word "strike", which is a
display that will eventually miss one. The simulation already knows which events cost the ship
something, so it says so.

**The simulation project has no AI dependencies.** Not the Agent Framework, not
`Microsoft.Extensions.AI`, no NuGet packages at all. The harness is an adapter over a domain,
never the domain itself.

**Tool failures are values, not exceptions.** `Apply` returns a `CommandResult` carrying a
narrative refusal the model can re-plan from. A thrown exception is a worse tool response than a
clear "insufficient reactor output".

**One tick per completed agent turn.** Deterministic by construction: same scenario plus same
seed produces the same voyage, which is what makes the milestone 3 experiment a controlled
comparison rather than an anecdote.

Full reasoning in `docs/architecture.md`.
