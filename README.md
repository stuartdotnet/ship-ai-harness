# ship-ai-harness

A starship AI built on the **Microsoft Agent Framework agent harness**, in C#.

You are the captain of the ISV Kestrel. AURORA is the ship's intelligence, and it is a
`HarnessAgent` wrapped around an `IChatClient`. The ship is a real simulation with hull integrity,
reactor heat, power budgets, pressurised compartments, and a crew who have names and can die.

Companion code for a four-part series. Each post has a git tag, so you land on the code the post
describes rather than the head of `main`.

| Post | Covers | Tag |
| --- | --- | --- |
| 1. Build a starship AI in C# with the Agent Framework harness | Tools, harness options, which defaults to switch off and why | `post-1` |
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
dotnet test                   # 33 tests, no model calls, ~1s
dotnet run --project src/ShipAI.Console
```

Authentication is Entra ID via `DefaultAzureCredential` unless you set `AZURE_OPENAI_API_KEY`.
`az login` is usually all it takes.

```
  ISV KESTREL  ·  AURORA shipboard intelligence online  ·  Derelict Freighter
  ──────────────────────────────────────────────────────────────────────────

  ISV Kestrel is nine hours from the Halveston rendezvous with a hold full of
  medical cargo. Twenty minutes ago the long-range array picked up a repeating
  distress beacon on a heading that costs us four hours we do not have...

  /status  /log  /help  /quit

  T000  │  HULL ██████████ 100  │  RCTR  18°  │  PWR  95%  │  ALERT GREEN  │  CREW 8/8  │  CONTACTS 0

  CAPTAIN › sweep the sector and tell me what's out there
```

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
went stale two turns ago. In milestone 1 that means AURORA genuinely cannot see the status bar
the captain is looking at; milestone 2 closes the gap with a custom `AIContextProvider`. Building
the tool version first and watching it fail is the point of post 2.

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
