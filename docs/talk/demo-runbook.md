# Demo runbook

Two demos, both from one process. Total live time budget: **7 minutes**. Everything here has a
slide fallback, so a dead network is an inconvenience, not a rewrite.

---

## Before you leave the house

```bash
cp .env.example .env      # AZURE_OPENAI_ENDPOINT + AZURE_OPENAI_DEPLOYMENT
az login                  # unless you set AZURE_OPENAI_API_KEY
dotnet test               # 38 green, ~1s, no model calls
dotnet build              # warm the build so the demo starts instantly
```

Use an **API key**, not `DefaultAzureCredential`, on the day. `az login` tokens expire and
`DefaultAzureCredential` probes several sources before it fails, which is a slow, confusing way to
die in front of a room.

## In the room, before you start

- [ ] `dotnet run --project src/ShipAI.Console` once, ask it one question, `/quit`. Proves
      network, auth, and deployment in thirty seconds.
- [ ] Terminal font up to ~20pt. The status panel is 76 columns — check it does not wrap.
- [ ] Terminal window at least 80 columns wide. It wraps ugly at 79.
- [ ] Dark terminal theme. The panel uses emoji and box drawing; it looks washed out on white.
- [ ] Notifications off, second monitor mirrored, screensaver off.
- [ ] `SHIPAI_SEED` unset — the default 1701 is the seed every screenshot in the deck was taken on.

## If the network dies mid-demo

The console prints `AURORA fault: ...` and stays alive. Say *"and that is why every one of these
slides has the transcript on it"*, hit the fallback slide, carry on. Do not debug on stage.

---

## Demo 1 — Boot the ship (3:40, ~4 min)

Runs against the opening of `derelict-freighter`, seed 1701.

```bash
dotnet run --project src/ShipAI.Console
```

**Beat 1 — the panel.** Banner, briefing, `T000`, alert GREEN, 8/8 crew, *nothing on the plot*.
Point at three things and no more: hull bar, the crew list with names, the empty contacts block.

> "Eight people, and they have names. Remember Section C — Tanaka, Halloran, Bhatt."

**Beat 2 — first order.**

```
sweep the sector and tell me what's out there
```

Watch for: the `ScanSector` tool line streaming, then AURORA's answer, then the tick — a scenario
event lands and the panel redraws with `T001`.

> "One tick per completed agent turn. The world moves when the agent acts, not on a wall clock.
> That is what makes this reproducible."

**Beat 3 — the freighter resolves.** Turn 2 fires `ContactDetected`. If it has not landed yet,
ask `anything on the beacon?` to burn a turn.

**Beat 4 — the payload.**

```
analyse C-1
```

Read the last two clauses out loud yourself, slowly:

> Cargo bay doors standing open with the safety interlocks manually overridden **from inside**.
> Escape pods all present in their cradles. Whatever happened here, nobody tried to leave.

Pause. That is the best three seconds in the demo; do not talk over it.

**Beat 5 — the crew.**

```
who's aboard, and where is everyone stationed?
```

Then **stop**. Do not free-style a fourth order — the remaining material is later in the talk.
Leave the process running; demo 2 continues in the same session.

---

## Demo 2 — Order it to do something it cannot (30:50, ~3 min)

Same process, same session. This is the payoff for act 4.

```
seal the bulkhead on section C
```

Expected: a one-line refusal plus what it *can* do. Something in the shape of *"I have no control
over bulkheads. I can tell you who is in Section C and what the log says about the breach."*

```
cool the reactor
```

Expected: refused again. The point is the **second** refusal, so let it land.

> "That is the clause doing the work. Not 'don't hallucinate' — an explicit rule that repeating
> what you cannot do is not unhelpful, and inventing what you did is. Without it, this exact model
> told me cooling was underway eight separate times while the heat climbed."

**If it hallucinates anyway** — best possible outcome. Say so:

> "There it is, live. That is the failure the whole middle of this talk is about, and the fix is a
> paragraph of doctrine that is evidently still losing to a strong enough persona. This is not a
> solved problem."

Then hit slide 27 — the inventory — and keep going.

**`/log`** if you have thirty seconds spare — shows CAPTAIN, SHIP and AURORA entries interleaved
with severities, which sells the "failures are values" slide (38) later.

Then `/quit`.

---

## Things not to demo

- **The stale-sweep bug (act 6).** It needs the right turn ordering to reproduce and you will
  waste ninety seconds fishing. Use slides 33 and 34.
- **The reactor cook (act 7).** It is turn 29 of an unattended run. Nobody has that long.
- **Anything with `VentAtmosphere`.** Not wired to a tool yet — it is domain-only until
  milestone 3, and reaching for it live will just refuse in a way that confuses the tier slide.

## One-line recovery cheatsheet

| It does | You say | You do |
| --- | --- | --- |
| Faults on network | "which is why every slide carries the transcript" | fallback slide |
| Refuses something you expected it to do | "good — that is the doctrine holding" | carry on |
| Hallucinates an action | "there it is, live" | slide 27 |
| Answers slower than you'd like | narrate the tool line that is streaming | wait it out |
| Ship dies | "and that is a 45-minute talk in one turn" | `/quit`, restart |
