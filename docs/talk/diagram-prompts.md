# Diagram prompts

Paste any of these into Copilot, ChatGPT, or whatever image tool you prefer. They are written to
produce a **flat, dark, technical diagram** that sits beside the deck rather than fighting it, so
the style block is repeated in each one — image tools forget it between prompts.

Drop the result into the slide it names and delete the ASCII/text version that is there now.

## The house style, if you want it separately

> Flat vector technical diagram. Dark background `#0B0E13`. Panel fills `#131922` with 1px borders
> `#232C39`. Text near-white `#E8EDF4`, secondary grey `#8494A6`. One accent amber `#F2B33D`, one
> secondary blue `#5AA9FF`, alert red `#FF5D52`, success green `#46C46A`. Sans-serif labels,
> monospace for any code identifier. No gradients, no drop shadows, no 3D, no glow, no stock-art
> icons, no people. Thin 1–2px connector lines with small solid arrowheads. Generous whitespace.
> 16:9. Nothing decorative — every mark carries information.

---

## 1 — Layering (slide 9)

> [house style]
>
> Draw three stacked rectangles, equal width, vertically arranged with a gap between each, connected
> top-to-bottom by a single thin arrow labelled "references" in grey monospace.
>
> Top box, amber border: title `ShipAI.Console`, subtitle "terminal host — REPL, slash commands, the
> tick", and a smaller line `ChatClientFactory: the model-provider seam`.
> Middle box, blue border: title `ShipAI.Agent`, subtitle "harness wiring", smaller line
> `ShipAgentFactory · Tools/ · Instructions/`.
> Bottom box, green border and a slightly heavier stroke than the other two: title
> `ShipAI.Simulation`, subtitle "pure domain", smaller line `ShipState · Commands/ · Scenarios/`.
>
> To the right of the bottom box only, outside it, in amber: **"no PackageReference at all"** with a
> short thin leader line pointing at that box. Nothing points to the right of the other two boxes.
>
> Arrows only ever point downward. Do not draw any arrow from the bottom box upward.

## 2 — The agent turn loop (slides 8 and 11)

> [house style]
>
> A horizontal cycle diagram of one agent turn, five nodes left to right with the last curving back
> to the first underneath.
>
> 1. Rounded rectangle, grey: "Captain types an order"
> 2. Rectangle, blue border: "Harness builds the turn" with small sub-labels inside:
>    `instructions · history · context providers`
> 3. Rectangle, amber border: "Model responds" with two outgoing branches:
>    branch A labelled "tool call" loops back up into node 2 with a small circular arrow annotated
>    "repeat until the model stops asking";
>    branch B labelled "text" continues right
> 4. Rectangle, grey: "Streamed to the bridge"
> 5. Rectangle, green border: `simulation.Tick()` with sub-label "the world moves — once"
>
> A thin return line from node 5 back to node 1 along the bottom, unlabelled.
>
> Set the whole thing off with a caption line beneath, grey: "one tick per completed agent turn —
> not a wall clock".

## 3 — Persona wider than the tool list (slide 29) — the most useful one

> [house style]
>
> A Venn-style diagram of two overlapping shapes, but deliberately unequal.
>
> A large amber outlined ellipse labelled **"What the persona implies it can do"**, filled with
> faint grey monospace phrases scattered inside: `cool the reactor`, `seal the bulkhead`,
> `plot a course`, `deploy a boarding party`, `run repair drones`, `initiate light speed`.
>
> Inside it, small and low and clearly contained, a green outlined ellipse labelled **"What the
> tools can actually do"**, containing only: `ScanSector`, `AnalyseContact`, `QueryCrewManifest`,
> `ReadShipLog`.
>
> Shade the crescent between them very faintly in red and label that region, in red, pointing in
> with a thin leader: **"the model fills this with prose"**.
>
> No third circle. The size difference between the two ellipses is the point — make the amber one at
> least four times the area of the green one.

## 4 — Tools versus context (slide 35)

> [house style]
>
> A two-column comparison, split by a single thin vertical rule down the middle. No boxes around the
> columns.
>
> Left column, amber heading `TOOLS`: body line "Things the agent **chooses** to do or look up." A
> small icon-free diagram beneath: a rectangle labelled "agent" with a single arrow reaching out and
> back to a rectangle labelled "world", annotated "on request".
>
> Right column, blue heading `CONTEXT`: body line "Things that are **always true and always
> changing**." A small diagram beneath: a rectangle labelled "world" with three parallel arrows
> flowing continuously into a rectangle labelled "every turn", annotated "unasked".
>
> Beneath both, spanning the full width, a single line in amber monospace:
> `there is deliberately no GetShipStatus tool`.

## 5 — The stale-sweep timeline (slides 33 and 34)

> [house style]
>
> A horizontal timeline with four tick marks labelled `T000`, `T001`, `T002`, `T003`.
>
> Above the line — a lane labelled "The world" in green: at T000 a small marker "sector empty"; at
> T002 a red marker "freighter resolves"; at T003 a red marker "freighter still there".
>
> Below the line — a lane labelled "What the agent believes" in amber: a single long horizontal bar
> starting at T000 and running unbroken all the way to T003, labelled inside
> `[T000] no contacts — background only`.
>
> At T003 in the lower lane, a red callout box with the agent's words: "No vessel on sensors. I
> cannot approach what I cannot find."
>
> Between the two lanes at T002, a red vertical arrow pointing downward that stops short and does
> not reach the lower lane, with a small red label beside it: **"nothing tells it"**. The arrow must
> visibly fail to connect.

## 6 — Approval tiers (slide 36)

> [house style]
>
> Three horizontal bands stacked vertically, increasing in border weight from top to bottom.
>
> Band 1, green, thinnest border: label "Read-only — auto-approved", contents in monospace
> `ScanSector · AnalyseContact · QueryCrewManifest · ReadShipLog`. To its right, a small open
> gateway glyph drawn as two short parallel lines with a clear gap between them.
>
> Band 2, amber: label "Consequential, reversible — gated", contents `RoutePower · PlotJump ·
> SealBulkhead`. To its right, the same gateway glyph but with a thin bar across the gap.
>
> Band 3, red, heaviest border: label "Lethal — gated", contents `VentAtmosphere · OpenAirlock`. To
> its right, the gateway glyph with a heavy bar across the gap.
>
> Down the left edge, spanning all three bands, a thin vertical arrow pointing downward labelled
> "consequence", not "category".

## 7 — Where state lives (optional, for Q&A)

> [house style]
>
> A single amber-bordered rectangle in the centre labelled `ShipSimulation`, containing a smaller
> solid rectangle labelled `ShipState` with the sub-label "the only mutable reference".
>
> Three thin grey arrows point *out* of it to three small outlined rectangles arranged around it:
> "console panel", "tools", "tests". Each arrow is labelled `snapshot`.
>
> Two arrows point *in*, both amber and thicker than the others, entering from the left: one
> labelled `Apply(command)` and one labelled `Tick()`.
>
> Caption beneath in grey: "state changes in exactly two places".

---

## If the output is wrong in the usual ways

| It did this | Add this to the prompt |
| --- | --- |
| Glossy, gradients, 3D | "Strictly flat. No gradients, no shadows, no bevels, no 3D perspective." |
| Added robots, people, brains | "No illustrations of people, robots, brains, or clouds. Boxes, lines, and text only." |
| Illegible small text | "Minimum label size equivalent to 14pt at 1920×1080. Fewer labels if needed." |
| Made everything the same size | "Relative size carries meaning — keep the specified proportions." |
| Light background | "Background must be near-black `#0B0E13`. The diagram is for a dark projector deck." |
