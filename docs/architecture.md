# Architecture

## Layering

```
┌──────────────────────────────────────────────────────────┐
│  ShipAI.Console          terminal host                   │
│    Program.cs            REPL, slash commands, the tick   │
│    ChatClientFactory     the model-provider seam          │
│    Rendering/            status bar, transcript           │
└───────────────────────────┬──────────────────────────────┘
                            │ references
┌───────────────────────────▼──────────────────────────────┐
│  ShipAI.Agent            harness wiring                  │
│    ShipAgentFactory      AsHarnessAgent(...)             │
│    Tools/SensorTools     read-only, auto-approved        │
│    Instructions/         AURORA's operating doctrine      │
└───────────────────────────┬──────────────────────────────┘
                            │ references
┌───────────────────────────▼──────────────────────────────┐
│  ShipAI.Simulation       pure domain — BCL only          │
│    ShipState             immutable snapshot              │
│    ShipSimulation        applies commands, advances ticks │
│    Commands/             one record per mutating action   │
│    Scenarios/            scripted, seeded encounters      │
└──────────────────────────────────────────────────────────┘
```

The arrows only point one way. `ShipAI.Simulation` has no `PackageReference` at all — not to the
Agent Framework, not to `Microsoft.Extensions.AI`, not to anything. The harness is an adapter over
a domain, never the domain itself, and the whole simulation runs at unit-test speed with no model
in the loop.

## Where state lives

`ShipSimulation` holds the only mutable reference to a `ShipState` and replaces it wholesale.
Everything else works with immutable snapshots. State changes in exactly two ways:

1. A tool call applies a `ShipCommand` through `ShipSimulation.Apply`.
2. `ShipSimulation.Tick()` runs once per completed agent turn, firing scenario events and
   applying passive effects (atmosphere bleeding from a breach, reactor heat, casualties).

Nothing else moves. Same scenario plus same seed produces the same voyage, every time — which is
what makes screenshots reproducible, blog samples honest, and the milestone 3 experiment a
controlled comparison rather than an anecdote.

`Tick()` is currently called by the console host. Milestone 2 moves it into
`ShipStateProvider.StoreAIContextAsync`, where advancing the world becomes an ambient concern of
the agent turn rather than of whichever UI happens to be attached.

## Failures are values

`ShipSimulation.Apply` never throws for a rule violation. It returns a `CommandResult` with
`Success = false` and a narrative explaining what the ship actually needs:

> Insufficient reactor output: Engines requested 60%, only 25% is unallocated. Reduce another
> system first.

A tool that throws hands the model a stack trace. A tool that answers like that hands it
something to re-plan from, and the difference shows up directly in transcript quality.

## What the agent can and cannot see (milestone 1)

| | Captain | AURORA |
| --- | --- | --- |
| Hull, reactor, power, alert, breaches | status bar | **nothing** |
| Sensor contacts | status bar count | `ScanSector`, `AnalyseContact` |
| Crew manifest | `/status`, `/log` | `QueryCrewManifest` |
| Ship's log | `/log` | `ReadShipLog` |

That gap is deliberate and temporary. There is no `GetShipStatus` tool, because ship status is
*ambient*: the agent should never burn a tool call to learn its own condition, and should never
be able to act on a reading that went stale two turns ago. Milestone 2 closes the gap with a
custom `AIContextProvider` that injects a status block into every turn.

Building the tool version first, watching it fail, and then deleting it is the narrative spine of
post 2 — so the tool is absent here rather than present-and-adequate.

## Approval tiers

Split by consequence, not by category. A tool is auto-approved because it cannot do harm, not
because it happens to be a sensor.

| Tier | Tools | Status |
| --- | --- | --- |
| Read-only, auto-approved | `ScanSector`, `AnalyseContact`, `QueryCrewManifest`, `ReadShipLog` | **built** |
| Consequential, reversible, gated | `RoutePower`, `PlotJump`, `SealBulkhead` | domain built, tools milestone 3 |
| Lethal, gated | `VentAtmosphere`, `OpenAirlock`, `FireWeapons` | `VentAtmosphere` in domain; rest milestone 3 |

`OpenAirlock` and `FireWeapons` are deliberately absent from the domain so far. Weapons need a
tactical model that only earns its place in milestone 3, and building it now would be a combat
balance system nobody asked for.

## Harness defaults

| Default | Setting | Why |
| --- | --- | --- |
| `HostedWebSearchTool` | `DisableWebSearch = true` | A starship AI googling breaks the fiction and cannot help. Everything it needs is aboard. |
| `FileMemoryProvider` | `DisableFileMemory = true` | On by default; writes to `{cwd}/agent-file-memory/…`. Milestone 4 re-enables it as the ship's log. |
| `AgentSkillsProvider` | `DisableAgentSkillsProvider = true` | On by default; scans cwd for skills that do not exist. |
| `FileAccessProvider` | *(untouched)* | Opt-in via `FileAccessStore`, which stays null. No setting needed. |
| `TodoProvider` | *(kept)* | Becomes the damage-control checklist for free. |
| `AgentModeProvider` | *(kept, default modes)* | Milestone 2 replaces plan/execute with Advisory/Command. |
| Compaction | both token limits set | Setting only one silently disables it. |

Batteries-included means knowing which batteries to remove.

## The model-provider seam

`ChatClientFactory.Create` is the only place that knows which model is behind AURORA.
`AsHarnessAgent` is an extension on `IChatClient`, so swapping Azure OpenAI for Anthropic,
OpenAI, or a local model is a change to that one method's return value. Nothing downstream —
tools, factory, console — knows the difference.
