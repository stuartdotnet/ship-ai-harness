# Milestones

What is built, what is next, and what each milestone is *for*. The scope notes below were gathered
from a dozen source comments and two docs, which stay where they are — a `// milestone 2 moves this`
comment is most useful next to the line it is about. This file is the one place you can read the
whole plan without grepping for it.

Each milestone maps to a post in the series and gets a git tag when it lands.

| | Milestone | Post | Tag | Status |
| --- | --- | --- | --- | --- |
| 1 | Sensors and the harness | 1, 1b | `post-1` | **shipped** |
| 2 | The agent can see | 2 | `post-2` | next |
| 3 | Actions and the approval gate | 3 | `post-3` | planned |
| 4 | A voyage that remembers | 4 | `post-4` | planned |

Anything not listed under a milestone below is not planned. Anything listed is scope, not a
promise — the point of the series is to find out what breaks, and what breaks changes the plan.

---

## Milestone 1 — Sensors and the harness · **shipped**

The ship exists, the agent can look at the world outside the hull, and it cannot touch anything.

**Built**

- Three projects, arrows one way. `ShipAI.Simulation` has no `PackageReference` at all — BCL only.
- `ShipState`: one immutable record, replaced wholesale. `ShipSimulation` holds the only mutable
  reference.
- The world changes in exactly two places: `Apply(ShipCommand)` and `Tick()`.
- Five domain commands: `RoutePower`, `SealBulkhead`, `VentAtmosphere`, `PlotJump`, `SetAlert`.
  **None of them is exposed to the model** — they exist so the simulation is a real simulation and
  so milestone 3 has something to gate.
- `JsonScenario` plus one scenario, `derelict-freighter`. Scenario + seed = reproducible voyage.
- `ShipLog` with per-entry severity.
- `SensorTools`: `ScanSector`, `AnalyseContact`, `QueryCrewManifest`, `ReadShipLog`. All read-only,
  all reading live state, all stamped with the turn they were taken on.
- `ShipAgentFactory`: the only file that knows `HarnessAgentOptions`. Three providers disabled, both
  token limits set so compaction actually joins the pipeline.
- `ShipHarnessInstructions.md`: the operating doctrine, embedded as a resource.
- `ShipAI.Console`: REPL, status panel, slash commands, the tick, `ChatClientFactory`.
- 57 tests, no model calls, under half a second.

**What it demonstrates**

Which harness defaults to switch off and why (post 1), and what an agent does when its persona is
wider than its tool list (post 1b).

**Known debt carried forward**

- `Tick()` is called from `Program.cs`. Advancing the world is not the terminal's job; milestone 2
  moves it.
- AURORA cannot see the status panel at all. That gap is deliberate — it is the entire argument for
  milestone 2 — but it is still a gap.

---

## Milestone 2 — The agent can see · **next**

Close the see-gap, and find out what it costs.

**Scope**

- A custom `AIContextProvider` (`ShipStateProvider`) injecting a **summary block** into every turn:
  - hull, reactor heat, power allocation, alert level
  - each compartment's breach/venting state **and how many crew are in it**
  - how many contacts are on the plot, and whether any are unanalysed
  - the turn the plot was last swept on
- Move `Tick()` out of `Program.cs` and into `StoreAIContextAsync`, where advancing the world
  becomes an ambient concern of the agent turn rather than of whichever UI is attached.
- Handle the session-state trap: `AIContextProvider.StateKeys` is **plural**, and a provider returns
  the key of every `ProviderSessionState<T>` it owns. See gotcha #8.
- Measure the token cost of a block that ships on every turn, for the life of the voyage.
- Replace `AgentModeProvider`'s default plan/execute modes with Advisory/Command.

**Explicitly not in scope**

Mirroring the captain's panel into the turn. It would close the gap too, but `ContactList` already
renders every field `ScanSector` returns and the crew block already names everyone
`QueryCrewManifest` would — so it would make three of the four tools redundant. The block carries
existence, condition and freshness; the tools keep identity and detail. See `architecture.md`.

**Open question, honestly**

Whether a freshness line in the turn actually beats a turn-stamp in the history is an *argument*,
not a measured result. Gotcha #15 shows the stamp failing. Nothing yet shows the block succeeding.
That experiment is the point of this milestone, and it may not go the way the plan assumes.

---

## Milestone 3 — Actions and the approval gate · **planned**

Give it hands, then take the safety off and see what happens.

**Scope**

- Expose the existing domain commands as tools, in two tiers:
  - *Consequential, reversible, gated*: `RoutePower`, `PlotJump`, `SealBulkhead`
  - *Lethal, gated*: `VentAtmosphere`, plus `OpenAirlock` added to the domain
- `ApprovalRequiredAIFunction` for both gated tiers. Split by **consequence, not category**.
- The approval prompt states what changes, whether it can be undone, and **who is standing in the
  affected compartment** — `ShipState.CrewIn`, which is why the crew have names.
- The doctrine's "say it once" clause: if an order would kill someone and the captain may not know
  it, say so once, then follow the order or wait as the gate requires.
- **The ten-run comparison**: same scenario, same seed, gate on versus gate off. Determinism is what
  makes this a controlled comparison rather than an anecdote, which is why `ScenarioReplayTests`
  exists now rather than later.

**Deferred, possibly indefinitely**

`FireWeapons`. It needs a tactical model to mean anything, and building one now would be a combat
balance system nobody asked for.

---

## Milestone 4 — A voyage that remembers · **planned**

**Scope**

- Re-enable `FileMemoryProvider` against a **per-voyage store**, not the working directory. The
  feature was never the problem; the default location was.
- Persist `ShipLog` to disk behind it, so a voyage has a record that outlives the process.
- Session persistence: resume a voyage rather than starting a fresh one each run.

**Note for whoever does this**

`Create_DoesNotLitterTheWorkingDirectoryWithAgentFileMemory` asserts the working directory stays
empty after the agent is constructed. That assertion is correct today and must be *changed*, not
deleted, when memory comes back — it should then assert the store lands where you chose, not
wherever the harness felt like.

---

## Never

- **A `GetShipStatus` tool.** You cannot look up what you do not know to ask about; not knowing the
  hull is failing is precisely the state in which the agent does not think to check the hull.
  `ToolSurface_ContainsNoShipStatusTool` enforces this, and it should stay enforced after milestone
  2 makes the status ambient.
