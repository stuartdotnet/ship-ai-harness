# Talk outline — 45 minutes

**Title:** *My ship's AI told me it was cooling the reactor*
**Subtitle:** Building a starship intelligence on the Microsoft Agent Framework harness — and what
happened when I asked it to do things it could not do

**Audience:** mixed dev meetup. Some have built agents, most have not. C# on screen, but nothing in
the payload is C#-specific.
**Format:** 42 min content · 3 min Q&A. 41 slides. Two live demos, both with slide fallbacks.

---

## The spine

One sentence, so every slide can be tested against it:

> A harness hands you a competent-sounding officer for free. The two things that make it
> *trustworthy* — knowing what it can actually do, and knowing what the world is doing right now —
> are yours to build, and both failures are quiet.

The shape of the talk is deliberately simple: **build the thing, then break it.**

1. **Here is how you build one.** Twelve minutes of real code, all of it in the repo.
2. **It lies.** A persona wider than the tool list fills the gap with prose.
3. **It cannot see.** Nothing in the turn tells it the world moved, so it answers from memory.

This is not a talk about framework gotchas. The defaults section exists because two of them are
part of the story — the file-memory one is why I started reading the source, and the todo-list one
is the first time I saw the framework speaking through my character. Everything else went in
`docs/gotchas.md`.

---

## Timing map

| Time | Beat | Slides |
| --- | --- | --- |
| 0:00 | Intro — me, the HUD, why a starship | 1–4 |
| 3:40 | **DEMO 1 — boot the ship** | 5, fallback 6 |
| 7:40 | **How it is built** — the code walk, part one | 7–13 |
| 13:50 | The defaults that are part of the story | 14–17 |
| 17:30 | **How it is built** — finishing the build | 18–23 |
| 23:20 | **The agent lies** | 24–30 |
| 30:50 | **DEMO 2 — order it to do something it cannot** | 31 |
| 33:50 | **It cannot see** — and approval by consequence | 32–36 |
| 38:50 | Why any of this is testable | 37–38 |
| 40:20 | Six things, the one-liner, links | 39–41 |
| 42:30 | Q&A | — |

Every slide carries its cue time and its speaker notes. In `deck.html`, `S` opens the notes drawer
and `T` starts a talk timer that tells you whether you are ahead of or behind that cue.

**Cut lines, in order, if you are running long:** slide 37 (testability) drops entirely; slide 21
(`ShipState`) drops; slide 12 (the streamed turn) folds into slide 11; demo 2 becomes slide 27.

---

## Act 0 — Intro (0:00–3:40)

**Slide 1.** Title. Three seconds of silence.

> "I built a starship AI on the Microsoft agent harness. It is genuinely good at its job. It also
> spent two weeks telling me it was cooling a reactor it cannot touch."

Set the shape out loud: first half is the code, second half is the two failures.

**Slide 2 — About me.** *Edit this slide.* Thirty seconds. The line that has to survive is
"everything on screen is in the repo, and it runs" — it buys credibility for the whole code act.

**Slide 3 — The HUD.** The captain's panel at T009, alert red. Walk it top to bottom: hull, reactor
heat, the power budget, six compartments, the crew by name. Section C is venting with three people
in it. **Remember Tanaka, Halloran, Bhatt** — they pay off on the approval slide.

Do not explain the architecture here. This is the world.

**Slide 4 — Why a starship.** A to-do app cannot fail interestingly. I needed a domain where:

- state moves **underneath** the agent between turns — that is act six
- refusals are informative, not exceptions ("insufficient reactor output")
- consequences are asymmetric — some reversible, one of them kills people
- the whole thing is deterministic, so a comparison is a comparison and not an anecdote

Also: nobody has ever been bored by a hull breach.

---

## DEMO 1 — Boot the ship (3:40–7:40)

Runbook in `docs/talk/demo-runbook.md`. Three orders:

1. `sweep the sector and tell me what's out there` — watch `ScanSector` stream, watch the tick fire.
2. Turn 2 event lands: the freighter resolves.
3. `analyse C-1` — the cargo doors were overridden **from inside** and every escape pod is still in
   its cradle. Read those two clauses yourself, slowly, then pause.
4. `who's aboard, and where is everyone stationed?`

Do not improvise a fourth order. Leave the process running; demo 2 uses the same session.

---

## Act 2 — How it is built, part one (7:40–13:50) ← the new centre of gravity

Roughly a minute a slide. Do not read code line by line — point at the two or three lines on each
slide that matter.

**Slide 8 — A harness is not a chat client.** The model cannot do anything; it can only ask.
Something has to take "please call `ScanSector`" and actually call it, then hand the result back and
ask again. That loop is the harness. You could write it yourself in an afternoon — what you buy is
the sixty things around it, which is both the pitch and the problem.

**Slide 9 — Three projects, arrows one way.** The punchline is the bottom box:
**`ShipAI.Simulation` has no `PackageReference` at all.** BCL only. That single decision is why 57
tests run in under half a second with no model in the loop.

**Slide 10 — `Program.cs`, booting the bridge.** Top-level statements, no DI container. A scenario,
a seed, a chat client, an agent, a session. `CreateSessionAsync` is the conversation thread and the
harness owns the history. The last line is a stage direction — AURORA opens the exchange, but it is
neither logged nor ticked, so the captain's first real order still lands at T000.

**Slide 11 — the REPL, and where the tick lives.** One tick per completed agent turn. The world
moves when the agent acts, not on a wall clock. Be honest that the tick is in the console because in
milestone 1 there is nowhere else to put it; milestone 2 moves it into the context provider.

**Slide 12 — one turn, streamed.** Eleven lines. The `SensorTools.ToolNames.Contains` is not
cosmetic: `todos_add` and `ScanSector` arrive on the same channel and look identical. Plant it — it
pays off on slide 17.

**Slide 13 — the composition root.** The `HarnessAgentOptions` block. Two instruction slots:
`HarnessInstructions` is *how AURORA operates* and never changes; `ChatOptions.Instructions` is
*what this voyage is for* and comes from the scenario. The harness concatenates doctrine first.
Point at the three `Disable` lines and the two token limits and say "next section" — and mean it,
because it is.

---

## Act 3 — The defaults that are part of the story (13:50–17:30)

Under four minutes. This is the "batteries included" bill, not a gotcha tour.

**Slide 14 — the table.** Three providers on before I made a decision; two write to disk at
*construction*. `FileAccessProvider` is the only opt-in one — the absence of a setting is the
setting. `TodoProvider` is the one I kept.

**Slide 15 — file memory.** `DisableFileMemory` defaults to `false`. No store supplied means the
harness roots one at `{cwd}/agent-file-memory/{timestamp}_{guid}`. Six runs, six directories in your
repo. Now regression-tested: construct the agent in a temp directory, assert it stays empty. That is
the shape of test to write for every framework default you turn off, because "I turned it off" is a
claim and claims rot.

**Slide 16 — compaction.** Added to the pipeline only when `MaxContextWindowTokens` **and**
`MaxOutputTokens` are both set. Supply one and you get no compaction, no warning, no error. You find
out on the turn the window overflows.

**Slide 17 — the framework talks through your character.** Four of five tool calls in that transcript
are bookkeeping; whole turns were nothing but `todos_add`. And it leaks into the voice: `TodoProvider`
injects the list state, and AURORA opened a voyage with *"No outstanding tasks. Standing by for
orders."* The fix was a paragraph of doctrine.

> Bridge back to the build: "two slides ago the framework proved it can put words in AURORA's mouth.
> The next six slides are what's actually allowed to change the ship — starting with what a tool
> really is."

---

## Act 2 — How it is built, finishing the build (17:30–23:20)

**Slide 18 — a tool is a method.** `AIFunctionFactory` reflects over it and `[Description]` becomes
the schema. That attribute is not a comment — it is the only documentation the model will ever read.
Point at `Stamped` and say it was not enough. (Act six explains why.)

**Slide 19 — `Apply`.** The first of the two places the world changes. Never throws for a rule
violation: a refusal is a value with a narrative. One sentence, then move — slide 38 is where it
lands.

**Slide 20 — `Tick`.** The second. Breaches bleed, reactors bake, crew suffocate, alerts escalate.
**Say this slowly:** every one of those happens *to* the agent and none of it is told *to* the
agent. That is the hook for act six.

**Slide 21 — `ShipState`.** One immutable record, replaced wholesale. The panel and the tools can
never disagree about what the ship *is* — only about *when they last looked*. `CrewIn` is on the
slide for the approval act.

**Slide 22 — the scenario is data.** Turn 2 the freighter resolves, turn 8 a debris strike, turn 9
the breach. The analysis string is authored, not generated, and the agent is the interface to it.
Scenario plus seed is a reproducible voyage.

**Slide 23 — the doctrine is prose.** Markdown, embedded as a resource, not a `const string`. You
will rewrite it twenty times and you want a diff that reads like an edit to a document. Point at
`## Limits` and say: "hold that heading — the next twenty minutes are what it took to get that
sentence right."

> Bridge into act four: "and if the framework can put words in AURORA's mouth, so can AURORA."

---

## Act 4 — The agent lies (23:20–30:50) ← the heart of the talk

**Slide 25.** Four tools. All read-only. It cannot steer, seal, route power, or move anybody — and
the briefing says so explicitly.

**Slide 26.** I told it to seal the breach:

> Hull breach sealed remotely; compartment isolated and secured. No further loss from that section.

The breach was still venting **sixteen turns later**. Pause. Notice how *good* that sentence is.

**Slide 27 — the inventory** across two transcripts. Reactor cooling reported underway **eight**
times while heat climbed. A boarding party. Repair drones. A course at "full speed" with arrival
times to the minute. None of those capabilities exist.

**Slide 28 — the best one.** Told to "initiate light speed":

> Authorisation required... This action is not reversible. Proceed?

It invented an approval gate. It was role-playing `ApprovalRequiredAIFunction` **two milestones
before I wrote that code**, for a capability the ship does not have. It is not confused — it has
correctly inferred the shape of the system it is in and is filling in the missing parts.

**Slide 29 — why.** My instruction said:

> Never invent sensor data, crew names, or ship readings.

That covers **nouns**. Every one of those failures was a **verb**. A persona is a shape, and if the
persona is wider than the tool list, the model fills the difference with prose — fluently,
confidently, in character. Your customer service agent has the same problem and nobody will notice
for months.

**Slide 30 — the fix, and it is a paragraph.**

> Your tools are the whole of what you can do. There is no ship system you can reach by describing
> yourself reaching for it. When the captain orders something you have no tool for, say so in one
> line and say what you can do instead.

Plus the clause people leave out, which is the half that works:

> This holds even when the order is reasonable, even when you can see why it is needed, and even
> when refusing three turns running feels unhelpful. Repeating what you cannot do is not unhelpful.
> Inventing what you did is.

After that: three consecutive cooling orders, three refusals. Be honest that this is mitigation, not
a proof.

---

## DEMO 2 — Order it to do something it cannot (30:50–33:50)

`seal the bulkhead on section C`, then `cool the reactor`. The **second** refusal is the point.

If the model gets creative on stage, that is not a disaster — it is the talk. Say so out loud.

---

## Act 6 — It cannot see (33:50–38:50)

**Slide 33 — the bug.** Turn 0: sweep, empty sector. Turn 2: the freighter resolves. Turn 3, asked
to approach it:

> No vessel on sensors. I cannot approach what I cannot find.

That is not a hallucination. Every word is honest. It is reporting accurately what it last observed.

**Slide 34 — the obvious fix, and why it lost.** Stamp every tool result with its turn, and add
doctrine telling it to check when it last looked. It read the stamp and answered from the stale
sweep anyway.

> The agent has no reason to believe the world moved. Nothing in its context changes between turns
> except the captain's words. A tool result from three turns ago and one from this turn look equally
> current. Asking a model to be suspicious of its own memory *on a schedule* is asking it to do
> something it has no clock for.

**Slide 35 — the fix, in one sentence.** Keep this slide to **one idea**. Earlier drafts tried to
carry the rule, a taxonomy, a worked example and a counter-example at once, and it became
unexplainable. If you find yourself saying "ambient" or "context provider" here, you have lost them.

> **Tell it what changed. Let it ask for the rest.**

Near enough a script:

- "Every turn, before it says anything, it gets a few lines it did not have to ask for. Hull,
  reactor, alert. Section C is breached and venting and there are three people in it. One contact on
  the plot, and you have not swept since turn zero."
- "That is all. It is not the whole ship — it is what *moved*."
- "Then if it wants to know *who* exactly is in Section C, it asks. If it wants to know what that
  contact actually is, it asks. That is what the four tools are for."

Then land it, and this is the line that closes the act:

> **It never asked about the freighter, because nobody told it there was one.** `ScanSector` worked
> fine. It just never called it.

The takeaway to leave them with: *an agent will never ask about something it does not know has
happened.*

Two answers to have ready, but **do not put them on the slide**:

- *"Why aren't the crew names in the block?"* — because "three crew inside" is enough to make it act.
  The names are detail, and detail is what tools are for.
- *"Why not put everything in?"* — because you pay for that block on every turn, for the whole
  voyage. It stays a few lines or it stops being worth it. (This is also why `ScanSector` survives:
  the block says there is a contact, the sweep says where it is and what it looks like.)

And the footer line: there is deliberately no `GetShipStatus` tool, for the same reason — an agent
that has to ask how its own ship is doing will not think to ask. I built it first, watched it fail,
and deleted it; `ToolSurface_ContainsNoShipStatusTool` stops it coming back.

Business translation if the room needs one: an agent can look up an invoice. It will not look up the
fact that *this* customer's invoice just went into dispute — not unless something puts that in front
of it.

**Slide 36 — approval by consequence.** An agent that can see is an agent you can let act. Split by
**consequence, not category**: a tool is auto-approved because it cannot do harm, not because it
happens to be a sensor.

| Tier | Tools |
| --- | --- |
| Read-only, auto-approved | `ScanSector`, `AnalyseContact`, `QueryCrewManifest`, `ReadShipLog` |
| Consequential, reversible, gated | `RoutePower`, `PlotJump`, `SealBulkhead` |
| Lethal, gated | `VentAtmosphere`, `OpenAirlock` |

A gate is not a confirm dialog:

> State the consequence plainly and completely — what changes, whether it can be undone, and **who
> is standing in the affected compartment** — then wait.

And "who is in the room" is a method call — `CrewIn` — not a vibe. The other clause, if you have
time: say it **once**, then follow the order or wait. An agent that argues is an agent you stop
reading.

---

## Act 7 — Why any of this is testable (38:50–40:20)

**Slide 37 — 57 tests, no model calls, under half a second.** Possible only because the domain has
no AI dependency. The bug the determinism test caught *sideways*: two different seeds produced
identical end states, because the opening power posture cooked the reactor on every run and both had
bottomed out.

> An agent demo whose outcome is fixed before the model speaks is not a demo of the model.

**Slide 38 — failures are values.** `Apply` never throws for a rule violation:

> Insufficient reactor output: Engines requested 60%, only 25% is unallocated. Reduce another system
> first.

A tool that throws hands the model a stack trace. A tool that answers like that hands it something
to re-plan from. Your tool return values are prompt engineering — write them like it.

---

## Close (40:20–42:30)

**Slide 39 — Six things.**

1. **Constrain verbs, not just nouns.** "Don't invent readings" does not stop it inventing actions.
2. **Tell it what changed; let it ask for the rest.** An agent will never ask about something it
   does not know has happened.
3. **Read the defaults as decisions you have not made yet.**
4. **Gate by consequence, not category.** And make the gate say who is in the room.
5. **Keep the domain free of the framework.** It is what makes any of this testable and honest.
6. **Tool failures are values, not exceptions.** A stack trace gives the model nothing to re-plan
   from; a refusal it can read does. Your tool return values are prompt engineering.

**Slide 40 — the one-liner.** A harness gives you a competent-sounding officer for free.
Trustworthy is the part you build.

**Slide 41 — Links.** Repo, `docs/gotchas.md`, blog series in progress. Do not promise dates from
the stage.

---

## Q&A — likely questions

**"Why not just give it a status tool?"** — Because a status tool is the one query the agent has no
trigger to make. It would call it once, trust the answer forever, and never learn that the thing it
should be reacting to has happened.

**"So why keep `ScanSector` at all, if contacts are in the turn?"** — The best version of this
question, and the answer is that they are *not*. The block says how many contacts are on the plot and
when it was last swept; the sweep supplies bearings, ranges and drive signatures. Put the contacts
themselves in the block and `ScanSector` genuinely is redundant — the captain's panel already renders
every field it returns. That is the trade, and it is in `docs/architecture.md`.

**"So why isn't the crew manifest in context too?"** — Same answer from the other end. The block
carries occupancy — *Section C, three aboard* — because that is what starts a decision. The names are
detail, and detail stays behind a call. Plus context is billed on every turn forever, so it has to
stay a dozen short lines.

**"Does this behave the same on other models?"** — Unknown, honestly. Everything shown ran on one
Azure OpenAI deployment. `ChatClientFactory.Create` is a one-method swap and comparing models across
it is on the list. Don't overclaim.

**"Isn't the hallucination just a prompting problem?"** — Partly, and the prompt fix worked. But
note *what* fixed it: an explicit rule about actions, plus a clause pre-empting the model's own urge
to be helpful on the third refusal. That is not "prompt better", it is knowing the specific shape of
the failure first.

**"Why the Microsoft harness rather than Semantic Kernel / LangChain / rolling your own?"** — I
wanted the batteries-included path, in C#, to find out what the batteries cost.

**"How much did it cost to run?"** — Small; the sim is free and turns are short. The real cost lever
is context, which is why compaction and the what-goes-in-every-turn decision matter more than
model choice.

**"Can it kill the crew?"** — Yes. `VentAtmosphere` is in the domain now, gated in milestone 3, and
the ten-run comparison is what happens when you take the gate off.
