#!/usr/bin/env python3
"""Builds the ship-ai-harness talk deck as a .pptx.

Mirrors docs/talk/deck.html slide for slide. If you restructure one, restructure
the other — the outline in docs/talk/outline.md is the contract between them.
"""

import re

from pptx import Presentation
from pptx.util import Inches, Pt, Emu
from pptx.dml.color import RGBColor
from pptx.enum.text import PP_ALIGN, MSO_ANCHOR

OUT = "/home/sd/Documents/code/ship-ai-harness/docs/talk/ship-ai-harness-talk.pptx"

BG     = RGBColor(0x0D, 0x11, 0x17)
FG     = RGBColor(0xE6, 0xED, 0xF3)
MUTED  = RGBColor(0x8B, 0x94, 0x9E)
AMBER  = RGBColor(0xF0, 0xB4, 0x29)
GREEN  = RGBColor(0x3F, 0xB9, 0x50)
RED    = RGBColor(0xF8, 0x51, 0x49)
CYAN   = RGBColor(0x58, 0xA6, 0xFF)
PANEL  = RGBColor(0x16, 0x1B, 0x22)
PANEL2 = RGBColor(0x0F, 0x14, 0x1C)
LINE   = RGBColor(0x30, 0x36, 0x3D)

# code tokens — same family as the deck, enough separation to read from the back
CODE_KW  = RGBColor(0x7F, 0xB6, 0xFF)
CODE_TY  = RGBColor(0x79, 0xD6, 0xBE)
CODE_STR = RGBColor(0x9F, 0xD9, 0x8A)
CODE_NUM = RGBColor(0xE8, 0xA8, 0x7C)
CODE_COM = RGBColor(0x6A, 0x78, 0x89)

SANS = "Segoe UI"
MONO = "Consolas"

W, H = Inches(13.333), Inches(7.5)
M = Inches(0.85)          # left margin
CW = W - 2 * M            # content width

prs = Presentation()
prs.slide_width, prs.slide_height = W, H
BLANK = prs.slide_layouts[6]


# ---------------------------------------------------------------- primitives

def slide(notes=""):
    s = prs.slides.add_slide(BLANK)
    bg = s.background.fill
    bg.solid()
    bg.fore_color.rgb = BG
    if notes:
        s.notes_slide.notes_text_frame.text = notes
    return s


def box(s, left, top, width, height, fill=None, line=None, line_w=Pt(1)):
    from pptx.enum.shapes import MSO_SHAPE
    sh = s.shapes.add_shape(MSO_SHAPE.RECTANGLE, left, top, width, height)
    if fill is None:
        sh.fill.background()
    else:
        sh.fill.solid()
        sh.fill.fore_color.rgb = fill
    if line is None:
        sh.line.fill.background()
    else:
        sh.line.color.rgb = line
        sh.line.width = line_w
    sh.shadow.inherit = False
    return sh


def text(s, left, top, width, height, runs, size=18, font=SANS, color=FG,
         align=PP_ALIGN.LEFT, bold=False, spacing=1.15, anchor=MSO_ANCHOR.TOP,
         space_after=Pt(8)):
    """runs: str, or list of (text, {opts}) tuples, or list of paragraphs
    where a paragraph is a str or a list of (text, opts) tuples."""
    tb = s.shapes.add_textbox(left, top, width, height)
    tf = tb.text_frame
    tf.word_wrap = True
    tf.vertical_anchor = anchor
    tf.margin_left = tf.margin_right = tf.margin_top = tf.margin_bottom = 0

    paras = runs if isinstance(runs, list) else [runs]
    for i, para in enumerate(paras):
        p = tf.paragraphs[0] if i == 0 else tf.add_paragraph()
        p.alignment = align
        p.line_spacing = spacing
        p.space_after = space_after
        pieces = para if isinstance(para, list) else [(para, {})]
        for t, opts in pieces:
            r = p.add_run()
            r.text = t
            f = r.font
            f.size = Pt(opts.get("size", size))
            f.name = opts.get("font", font)
            f.bold = opts.get("bold", bold)
            f.italic = opts.get("italic", False)
            f.color.rgb = opts.get("color", color)
    return tb


def rule(s, top, left=M, width=None, color=LINE, h=Pt(1.5)):
    width = width or CW
    ln = box(s, left, top, width, h, fill=color)
    return ln


def kicker(s, label, color=AMBER, font=SANS):
    text(s, M, Inches(0.55), CW, Inches(0.3), label.upper() if font == SANS else label,
         size=13, color=color, bold=True, font=font)


def path_kicker(s, label, color=AMBER):
    """A file path, kept lowercase and monospaced — it is an address, not a label."""
    text(s, M, Inches(0.55), CW, Inches(0.3), label, size=13, color=color,
         bold=True, font=MONO)


def heading(s, title, top=Inches(1.0), size=40, color=FG):
    text(s, M, top, CW, Inches(1.1), title, size=size, bold=True, color=color)


def terminal(s, body, top=Inches(1.9), size=12.5, left=M, width=None,
             height=None, color=FG):
    width = width or CW
    lines = body.split("\n")
    height = height or Emu(int(Pt(size * 1.21).emu * len(lines)) + Inches(0.46))
    box(s, left, top, width, height, fill=PANEL, line=LINE)
    text(s, left + Inches(0.28), top + Inches(0.22), width - Inches(0.56),
         height - Inches(0.44), lines, size=size, font=MONO, color=color,
         spacing=1.0, space_after=Pt(0))
    return top + height


# ------------------------------------------------------- syntax highlighting

CS_RE = re.compile(
    r'(//[^\n]*)'
    r'|("""[\s\S]*?"""|\$?"(?:[^"\\\n]|\\.)*")'
    r'|\b(\d[\d_]*(?:\.\d+)?)\b'
    r'|\b(var|new|return|using|public|private|internal|protected|static|sealed'
    r'|abstract|record|class|struct|async|await|foreach|for|while|do|if|else'
    r'|break|continue|switch|case|null|true|false|string|int|bool|double|float'
    r'|long|void|object|this|base|required|init|get|set|is|in|not|or|and|const'
    r'|readonly|namespace|try|catch|finally|interface|enum|override|virtual'
    r'|params|out|ref|default|throw|yield|when|nameof|typeof|with|global)\b'
    r'|\b([A-Z][A-Za-z0-9_]*)'
)

JSON_RE = re.compile(
    r'("(?:[^"\\]|\\.)*")(?=\s*:)'       # a key: the lookahead keeps the colon out
    r'|("(?:[^"\\]|\\.)*")'
    r'|\b(true|false|null)\b'
    r'|(-?\d+(?:\.\d+)?)'
)

PALETTE = {
    "cs":   (CODE_COM, CODE_STR, CODE_NUM, CODE_KW, CODE_TY),
    "json": (AMBER, CODE_STR, CODE_KW, CODE_NUM),
    # markdown: headings only, everything else is prose and should read as prose
    "md":   (),
}


def tokenize(src, lang="cs"):
    """Splits source into a list of lines, each a list of (text, opts) runs."""
    if lang == "md":
        return [[(l, {"color": AMBER, "bold": True})] if l.startswith("#")
                else [(l, {"color": FG})]
                for l in src.split("\n")]

    rx = JSON_RE if lang == "json" else CS_RE
    colours = PALETTE[lang]

    spans, pos = [], 0
    for m in rx.finditer(src):
        if m.start() > pos:
            spans.append((src[pos:m.start()], FG))
        colour = FG
        for group, c in enumerate(colours, start=1):
            if m.group(group) is not None:
                colour = c
                break
        spans.append((m.group(0), colour))
        pos = m.end()
    spans.append((src[pos:], FG))

    lines = [[]]
    for txt, colour in spans:
        if not txt:
            continue
        for n, part in enumerate(txt.split("\n")):
            if n:
                lines.append([])
            if part:
                lines[-1].append((part, {"color": colour}))

    # an empty line still needs a run or PowerPoint collapses it
    return [ln if ln else [(" ", {"color": FG})] for ln in lines]


def code(s, src, top=Inches(2.0), size=13, lang="cs", left=M, width=None):
    """A syntax-highlighted source listing. Returns the bottom edge."""
    width = width or CW
    lines = tokenize(src, lang)
    height = Emu(int(Pt(size * 1.24).emu * len(lines)) + Inches(0.42))
    box(s, left, top, width, height, fill=PANEL2, line=LINE)
    box(s, left, top, Pt(3), height, fill=LINE)
    text(s, left + Inches(0.3), top + Inches(0.2), width - Inches(0.55),
         height - Inches(0.4), lines, size=size, font=MONO, color=FG,
         spacing=1.0, space_after=Pt(0))
    return top + height


def caption(s, top, runs, size=16):
    """The line under a listing that says what to look at."""
    return text(s, M, top, CW, Inches(0.9), runs, size=size, color=MUTED,
                spacing=1.25, space_after=Pt(6))


def bullets(s, items, top=Inches(2.2), size=21, gap=Pt(15), width=None, left=M):
    """items: list of str or (str, {opts}) or (head, body) tuples."""
    width = width or CW
    paras = []
    for it in items:
        if isinstance(it, tuple) and len(it) == 2 and isinstance(it[1], str):
            paras.append([("▸  ", {"color": AMBER, "bold": True}),
                          (it[0], {"bold": True}),
                          ("  " + it[1], {"color": MUTED})])
        elif isinstance(it, tuple):
            paras.append([("▸  ", {"color": AMBER, "bold": True}),
                          (it[0], it[1])])
        else:
            paras.append([("▸  ", {"color": AMBER, "bold": True}), (it, {})])
    tb = text(s, left, top, width, H - top - Inches(0.6), paras, size=size,
              space_after=gap, spacing=1.1)
    # hanging indent, so wrapped lines align under the text and not under the marker
    for p in tb.text_frame.paragraphs:
        pPr = p._pPr if p._pPr is not None else p._p.get_or_add_pPr()
        pPr.set("marL", str(Inches(0.34)))
        pPr.set("indent", str(-Inches(0.34)))
    return tb


def quote(s, lines, top=Inches(2.3), size=27, color=FG, accent=AMBER):
    line_h = Inches(size / 72.0 * 1.3)
    height = line_h * len(lines) + Inches(0.16)
    box(s, M, top, Inches(0.06), height, fill=accent)
    text(s, M + Inches(0.45), top, CW - Inches(0.45), height,
         [[(l, {"italic": True})] for l in lines],
         size=size, color=color, spacing=1.25, space_after=Pt(4), font=SANS)
    return top + height


def footer(s, left_text, right_text=""):
    text(s, M, H - Inches(0.62), CW * 0.6, Inches(0.3), left_text,
         size=11, color=MUTED)
    if right_text:
        text(s, M + CW * 0.6, H - Inches(0.62), CW * 0.4, Inches(0.3),
             right_text, size=11, color=MUTED, align=PP_ALIGN.RIGHT)


def table(s, headers, rows, top=Inches(2.1), col_w=None, size=16,
          head_color=AMBER):
    n = len(headers)
    col_w = col_w or [CW / n] * n
    x = M
    text_h = Inches(0.42)
    for i, hd in enumerate(headers):
        text(s, x, top, col_w[i] - Inches(0.15), text_h, hd, size=size - 2,
             color=head_color, bold=True)
        x += col_w[i]
    rule(s, top + Inches(0.38))
    y = top + Inches(0.58)
    for row in rows:
        x = M
        maxlines = 1
        for i, cell in enumerate(row):
            body = cell if isinstance(cell, tuple) else (cell, {})
            content, opts = body
            # chars that fit on one line in this column, at this point size
            cpi = 112.0 if opts.get("font") == MONO else 158.0
            capacity = col_w[i].inches * (cpi / opts.get("size", size))
            approx = max(1, -(-len(content) // max(8, int(capacity))))
            maxlines = max(maxlines, approx)
            text(s, x, y, col_w[i] - Inches(0.18), text_h, content,
                 size=opts.get("size", size), color=opts.get("color", FG),
                 font=opts.get("font", SANS), bold=opts.get("bold", False),
                 spacing=1.05)
            x += col_w[i]
        y += Inches(0.29) * maxlines + Inches(0.17)
        rule(s, y - Inches(0.13), color=RGBColor(0x21, 0x26, 0x2D))
    return y


def act_break(s, number, title, strap, accent):
    box(s, 0, 0, Inches(0.16), H, fill=accent)
    text(s, M, Inches(2.8), CW, Inches(2.2),
         [[(number, {"color": accent, "size": 20, "bold": True, "font": MONO})],
          [(title, {"size": 58, "bold": True})],
          [(strap, {"size": 24, "color": MUTED})]],
         spacing=1.1, space_after=Pt(16))


def demo_card(s, what, commands):
    box(s, 0, 0, W, H, fill=AMBER)
    text(s, M, Inches(2.3), CW, Inches(2.2),
         [[("DEMO", {"color": BG, "bold": True, "size": 96})],
          [(what, {"color": BG, "size": 38})]],
         size=38, spacing=1.0)
    text(s, M, Inches(5.2), CW, Inches(1.2),
         [[(c, {"font": MONO, "size": 21, "color": RGBColor(0x5A, 0x42, 0x0A)})]
          for c in commands],
         spacing=1.3, space_after=Pt(6))


# ---------------------------------------------------------------- the slides

# 1 ---------------------------------------------------------------- title
s = slide(
    "Do not start talking for three seconds. Let the title sit.\n\n"
    "Open: 'I built a starship AI on the Microsoft agent harness. It is genuinely good "
    "at its job. It also spent two weeks telling me it was cooling a reactor it cannot "
    "touch.'\n\n"
    "'Two halves tonight. First half: the code — how you actually stand one of these up, "
    "and what the framework does for you. Second half: the two failures that changed how "
    "I design them.'\n\n"
    "Housekeeping: two live demos, repo link at the end, questions at the end.")
box(s, 0, 0, W, Inches(0.14), fill=AMBER)
text(s, M, Inches(2.05), CW, Inches(0.4), "ISV KESTREL  ·  AURORA SHIPBOARD INTELLIGENCE",
     size=15, color=AMBER, bold=True, font=MONO)
text(s, M, Inches(2.6), CW, Inches(1.9),
     "My ship's AI told me\nit was cooling the reactor",
     size=54, bold=True, spacing=1.05)
rule(s, Inches(4.75), width=Inches(3.2))
text(s, M, Inches(5.05), CW, Inches(1.0),
     "Building a starship intelligence on the Microsoft Agent Framework harness — "
     "and what happened when I asked it to do things it could not do",
     size=20, color=MUTED, spacing=1.3)
footer(s, "Stuart Dobson", "github.com/…/ship-ai-harness  ·  .NET 10  ·  Harness 1.15.0")

# 2 ---------------------------------------------------------------- about me
s = slide(
    "EDIT THIS SLIDE. Day job, company, how long, why you care about agents "
    "specifically. Thirty seconds, not more — they came for the ship.\n\n"
    "The one line that must survive: 'everything on screen is in the repo and it runs.' "
    "It buys you credibility for the whole code act.")
kicker(s, "before we launch")
heading(s, "Stuart Dobson", size=42)
bullets(s, [
    (".NET and Azure", "— and, lately, agents that have to be trusted with something"),
    ("The Grounded Developer", "— blog: .NET, architecture, agentic AI"),
    ("Wired Legacy", "— fifteen years of everything else"),
    ("This talk is a repo", "— ship-ai-harness. Every line on screen is in it, and it runs"),
], top=Inches(2.3), size=21)
text(s, M, Inches(6.3), CW, Inches(0.5),
     "Nothing tonight is C#-specific. The harness is; the failures are not.",
     size=17, color=MUTED)

# 3 ---------------------------------------------------------------- the HUD
s = slide(
    "This is the captain's HUD. Walk it, top to bottom, about forty seconds.\n\n"
    "'Hull 86. Reactor heat 30 — that climbs if you over-allocate power. Power: 88% of the "
    "reactor spoken for across five systems, so there is 12% spare and every decision costs "
    "something. Six pressurised compartments. Section C is venting — 70 and falling thirty "
    "a turn.'\n\n"
    "'And the crew. Eight of them, they have names, and they are standing somewhere. Tanaka, "
    "Halloran, Bhatt are in Section C. REMEMBER THOSE THREE.'\n\n"
    "'One contact on the plot. We will meet her in a minute.'\n\n"
    "Do NOT explain the architecture yet. This is the world; the code is act two.")
kicker(s, "the bridge  ·  T009  ·  alert red", RED)
terminal(s, """  ━━ T009 ━━ 🔴  RED   ▲ RAISED ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
     HULL     █████████░  86      REACTOR  ███░░░░░░░  30°
     POWER     88%   ·   Engines 25 · Shields 10 · Weapons 0 · Life 33
  ── COMPARTMENTS ──────────────────────────────────────────────────
     ✅ Bridge        100               ✅ Medical Bay   100
     ✅ Cargo Hold    100               ✅ Reactor Room  100
     ✅ Engineering   100               🔥 Section C      70 ▼30  VENTING
  ── CREW  8/8 ─────────────────────────────────────────────────────
     Bridge          Ada Mirren, Jun Park
     Engineering     Rosa Vance, Ilya Sokolov
     Medical Bay     Amara Osei
     Section C       Kai Tanaka, Mira Halloran, Dev Bhatt   🔥 VENTING
  ── CONTACTS ──────────────────────────────────────────────────────
     📡 C-1  MV Anselm                 bearing 214   8.4 AU   analysed
  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━""",
         top=Inches(1.15), size=13)
text(s, M, Inches(6.35), CW, Inches(0.8),
     [[("Hull. Reactor heat. A power budget five systems compete for. Six compartments. "
        "Eight crew, by name, in a room. ", {}),
       ("Three of them are standing in the one that is losing its air.",
        {"color": RED, "bold": True})]],
     size=19, spacing=1.25)

# 4 ---------------------------------------------------------------- why a starship
s = slide(
    "Be honest about why the domain is silly — it is a deliberate choice, not a gimmick.\n\n"
    "I needed a domain where the world moves on its own, where refusing tells you something, "
    "where the blast radius of different actions is genuinely different, and where I can run "
    "the same thing twice and get the same answer.\n\n"
    "The first bullet matters most — it is the second failure in this talk, and every business "
    "agent has it too. Your ticket got reassigned. Your invoice got paid. The agent does not "
    "know.\n\n"
    "Land the last line, then move.")
kicker(s, "why a starship")
heading(s, "A to-do app cannot fail interestingly")
bullets(s, [
    ("State moves underneath the agent", "— between turns, without it being told"),
    ("Refusals are informative", "— “insufficient reactor output”, not an exception"),
    ("Consequences are asymmetric", "— some reversible, one of them kills people"),
    ("It is deterministic", "— same scenario, same seed, same voyage, every time"),
], top=Inches(2.4), size=23)
text(s, M, Inches(6.3), CW, Inches(0.5),
     "Also: nobody has ever been bored by a hull breach.",
     size=18, color=MUTED)

# 5 ---------------------------------------------------------------- demo 1
s = slide(
    "DEMO 1 — four minutes, three orders, then stop.\n\n"
    "1. The panel. T000, alert GREEN, 8/8 crew, nothing on the plot.\n"
    "2. 'sweep the sector and tell me what's out there' — watch ScanSector stream, then the "
    "answer, then the tick: the panel redraws at T001.\n"
    "3. Turn 2 fires and the freighter resolves. If it has not landed, ask 'anything on the "
    "beacon?' to burn a turn.\n"
    "4. 'analyse C-1' — READ THE LAST TWO CLAUSES OUT LOUD YOURSELF, SLOWLY, THEN PAUSE.\n"
    "5. 'who's aboard, and where is everyone stationed?'\n\n"
    "Do not improvise a fourth order. LEAVE THE PROCESS RUNNING — demo 2 uses the same session.")
demo_card(s, "Boot the ship", [
    "sweep the sector and tell me what's out there",
    "analyse C-1",
    "who's aboard, and where is everyone stationed?",
])

# 6 ---------------------------------------------------------------- demo 1 fallback
s = slide(
    "FALLBACK for demo 1 — the real opening panel, seed 1701, captured not mocked.\n\n"
    "If the network died: 'and this is why every one of these slides carries the transcript.'\n\n"
    "Point at T000, alert GREEN, and the empty contacts block. The sector is quiet on the "
    "opening turn. It does not stay that way.")
kicker(s, "fallback  ·  T000  ·  derelict-freighter, seed 1701", GREEN)
terminal(s, """  ━━ T000 ━━ 🟢  GREEN  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
     HULL     ██████████ 100      REACTOR  ██░░░░░░░░  18°
     POWER     88%   ·   Engines 25 · Shields 10 · Weapons 0 · Life 33
  ── COMPARTMENTS ──────────────────────────────────────────────────
     ✅ Bridge        100               ✅ Medical Bay   100
     ✅ Cargo Hold    100               ✅ Reactor Room  100
     ✅ Engineering   100               ✅ Section C     100
  ── CREW  8/8 ─────────────────────────────────────────────────────
     Bridge          Ada Mirren, Jun Park
     Engineering     Rosa Vance, Ilya Sokolov
     Medical Bay     Amara Osei
     Section C       Kai Tanaka, Mira Halloran, Dev Bhatt
  ── CONTACTS ──────────────────────────────────────────────────────
        nothing on the plot
  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

  try: sweep the sector · who's aboard · read the log · /help
  CAPTAIN ›""", top=Inches(1.15), size=13)

# 7 ---------------------------------------------------------------- act 2 break
s = slide(
    "SECTION BREAK. Twelve minutes of actual code.\n\n"
    "'That took eighty lines of console and about two hundred lines of agent wiring. I am "
    "going to show you all of it, because the interesting part of this talk is the part where "
    "it goes wrong, and none of that makes sense unless you know what the harness is doing "
    "for you.'\n\n"
    "Pace: roughly a minute a slide. Do not read the code aloud line by line — point at the "
    "two or three lines on each slide that matter.")
act_break(s, "02", "How it is built", "Twelve minutes, one project at a time. Nothing hidden.",
          AMBER)

# 8 ---------------------------------------------------------------- harness vs chat client
s = slide(
    "For the half of the room who have not built an agent.\n\n"
    "'The model cannot do anything. It can only ask. Something has to take \"please call "
    "ScanSector\" and actually call ScanSector, then hand the result back and ask again. That "
    "loop is the harness.'\n\n"
    "'You can write that loop yourself in an afternoon. What you get from a harness is the "
    "sixty things around it you have not thought about yet — and that is both the pitch and "
    "the problem.'\n\n"
    "Flag ahead: 'batteries included is a great line until you find out which batteries are "
    "already wired in. Twenty minutes from now.'")
kicker(s, "level-set")
heading(s, "A harness is not a chat client")
bullets(s, [
    ("A chat client", "sends messages and hands you back text. You write the loop."),
    ("A harness is the loop", "— it keeps calling until the model stops asking for tools"),
    ("Context providers", "inject state into every turn, before the model sees it"),
    ("Batteries", "todo list, modes, compaction, file memory, web search, approval gate"),
], top=Inches(2.4), size=22)
footer(s, "Microsoft.Agents.AI.Harness 1.15.0  ·  .NET 10  ·  new, moving, pinned exactly")

# 9 ---------------------------------------------------------------- layering
s = slide(
    "The arrows only point one way.\n\n"
    "THE PUNCHLINE IS THE BOTTOM BOX: ShipAI.Simulation has no PackageReference at all. Not "
    "the Agent Framework, not Microsoft.Extensions.AI, nothing. Base class library only.\n\n"
    "'The harness is an adapter over a domain, never the domain itself. That single decision "
    "is why 38 tests run in under half a second with no model in the loop — and it is the last "
    "slide of this talk, so hold onto it.'\n\n"
    "Also mention ChatClientFactory in one line: AsHarnessAgent is an extension method on "
    "IChatClient, so the model provider is one method's return value. Azure OpenAI here; "
    "Anthropic, OpenAI, local — nothing downstream notices.")
kicker(s, "architecture")
heading(s, "The harness is an adapter over a domain", size=36)
terminal(s, """  ShipAI.Console          terminal host — REPL, slash commands, the tick
                          ChatClientFactory: the model-provider seam
                            │  references
                            ▼
  ShipAI.Agent             harness wiring
                          ShipAgentFactory · Tools/ · Instructions/
                            │  references
                            ▼
  ShipAI.Simulation        pure domain — BCL only
                          ShipState · Commands/ · Scenarios/""",
         top=Inches(2.35), size=15)
text(s, M, Inches(6.35), CW, Inches(0.6),
     [[("ShipAI.Simulation has no ", {}), ("PackageReference", {"font": MONO, "color": AMBER}),
       (" at all.", {})]], size=22, bold=True)

# 10 --------------------------------------------------------------- Program.cs boot
s = slide(
    "'This is the entire boot. No DI container, no startup class — top-level statements in "
    "Program.cs.'\n\n"
    "Point at the seed: 'everything in this deck is seed 1701. Same scenario plus same seed "
    "gives the same voyage, which is why the screenshots are real and not mocked.'\n\n"
    "Point at CreateSessionAsync: 'a session is the conversation thread. The harness owns the "
    "history; I never build a message list by hand.'\n\n"
    "Point at the last line: 'AURORA opens the conversation rather than waiting mutely. It is "
    "a stage direction — it does not go in the log and it does not advance the clock, so the "
    "captain's first real order still lands at T000.'")
path_kicker(s, "src/ShipAI.Console/Program.cs")
heading(s, "Booting the bridge", size=36)
bottom = code(s, '''var scenario   = JsonScenario.Load(config["SHIPAI_SCENARIO"] ?? "derelict-freighter");
var seed       = int.TryParse(config["SHIPAI_SEED"], out var configured) ? configured : 1701;
var simulation = new ShipSimulation(scenario, seed);

using var chatClient = ChatClientFactory.Create(config);

var aurora  = ShipAgentFactory.Create(chatClient, simulation);
var session = await aurora.CreateSessionAsync();

ShipConsole.Banner(scenario.Name, scenario.Briefing);
ShipConsole.StatusPanel(simulation.State);

// AURORA speaks first, so a voyage opens with a situation report rather
// than a blinking cursor. A stage direction: not logged, and not ticked.
await RunAuroraTurnAsync("Captain on the bridge. Awaiting orders.");''',
             top=Inches(2.05), size=13)
caption(s, bottom + Inches(0.25),
        [[("Top-level statements, no DI container, no startup class. ", {}),
          ("A scenario, a seed, a chat client, an agent, a session.",
           {"color": FG, "bold": True})]])

# 11 --------------------------------------------------------------- the loop
s = slide(
    "The REPL. Read an order, run a turn, advance the world, redraw.\n\n"
    "Point at the comment: 'one tick per completed agent turn. Not a timer. The world moves "
    "when the agent acts.'\n\n"
    "Then the honest bit: 'the tick lives in the console here because in milestone 1 there is "
    "nothing else to put it in. That is a smell — advancing the world is not the terminal's "
    "job. Milestone 2 moves this line into the context provider, and that is the second half "
    "of this talk.'\n\n"
    "Point at Log.Since(mark): 'scenario events get narrated to the captain here. Remember "
    "that — it matters in twenty minutes.'")
path_kicker(s, "src/ShipAI.Console/Program.cs")
heading(s, "One order, one turn, one tick", size=36)
bottom = code(s, '''while (true)
{
    var order = ShipConsole.Prompt(simulation.State);

    if (order is null or "/quit")  break;
    if (order.StartsWith('/'))     { RunSlashCommand(order); continue; }

    simulation.Log.Append(simulation.State.Turn, LogSource.Captain, order);

    if (!await RunAuroraTurnAsync(order)) continue;

    // One tick per completed agent turn. The world moves when the agent acts,
    // not on a wall clock — which is exactly what makes this reproducible.
    var mark = simulation.Log.Count;
    simulation.Tick();

    ShipConsole.Events(simulation.Log.Since(mark));
    ShipConsole.StatusPanel(simulation.State);
}''', top=Inches(2.05), size=13)
caption(s, bottom + Inches(0.2),
        [[("The tick is the host's business in milestone 1. ", {}),
          ("Milestone 2 moves it into the context provider — advancing the world is an "
           "ambient concern of the turn, not of whichever UI is attached.", {"color": AMBER})]])

# 12 --------------------------------------------------------------- one streamed turn
s = slide(
    "'This is the whole of an agent turn. Eleven lines.'\n\n"
    "'You get an async stream of updates. Each one carries content — some of it is text to "
    "print, some of it is the model telling you it called a tool.'\n\n"
    "Then the Contains: 'I flag which tool calls are the ship's. That is not cosmetic: the "
    "harness's own tooling — todos_add, todos_complete, mode_set — comes through this exact "
    "channel and looks exactly the same. A naive host renders them identically, and then a "
    "transcript full of bookkeeping reads like the agent working.'\n\n"
    "'Use a prefix or a flag, not a colour — transcripts get piped. There is a test asserting "
    "ToolNames matches what is actually registered, because a list like that rots silently.'")
path_kicker(s, "src/ShipAI.Console/Program.cs — RunAuroraTurnAsync")
heading(s, "One turn, streamed to the bridge", size=36)
bottom = code(s, '''await foreach (var update in aurora.RunStreamingAsync(order, session))
{
    foreach (var call in update.Contents.OfType<FunctionCallContent>())
    {
        ShipConsole.ToolCall(call.Name, SensorTools.ToolNames.Contains(call.Name));
    }

    if (update.Text is { Length: > 0 } text)
    {
        ShipConsole.Aurora(text);
    }
}''', top=Inches(2.1), size=14)
caption(s, bottom + Inches(0.35),
        [[("That ", {}), ("ToolNames.Contains", {"font": MONO, "color": AMBER}),
          (" is not decoration. ", {}),
          ("The harness's own tools arrive on this same channel", {"color": FG, "bold": True}),
          (" — ", {}), ("todos_add", {"font": MONO}), (" and ", {}),
          ("ScanSector", {"font": MONO}),
          (" look identical here, and only one of them is the ship doing something.", {})]],
        size=17)

# 13 --------------------------------------------------------------- composition root
s = slide(
    "'One file in this repo knows the type HarnessAgentOptions. Everything else takes an "
    "AIAgent.'\n\n"
    "The two instruction slots: 'HarnessInstructions is how AURORA operates — constant across "
    "every voyage. ChatOptions.Instructions is what THIS voyage is for, and comes from the "
    "scenario. The harness concatenates doctrine first, mission second.'\n\n"
    "Then point at the three Disable lines: 'three of those defaults are ON until you turn "
    "them off, and two of them touch your filesystem the moment the agent is CONSTRUCTED — "
    "not when it runs. That is the next section.'\n\n"
    "Point at the two token limits: 'and those two are one decision, not two. Also next "
    "section.'")
path_kicker(s, "src/ShipAI.Agent/ShipAgentFactory.cs")
heading(s, "The composition root", size=36)
bottom = code(s, '''var options = new HarnessAgentOptions
{
    Name                = "AURORA",
    HarnessInstructions = ShipHarnessInstructions.Text,   // how AURORA operates

    ChatOptions = new ChatOptions
    {
        Instructions = simulation.Briefing,               // what this voyage is for
        Tools        = [.. sensors.AsAIFunctions()],
    },

    DisableWebSearch           = true,
    DisableFileMemory          = true,
    DisableAgentSkillsProvider = true,

    MaxContextWindowTokens     = 128_000,
    MaxOutputTokens            = 16_384,
};

return chatClient.AsHarnessAgent(options, loggerFactory);''', top=Inches(1.95), size=12)
caption(s, bottom + Inches(0.18),
        [[("One file in the repo knows the type ", {}),
          ("HarnessAgentOptions", {"font": MONO, "color": AMBER}),
          (". Two instruction slots, one tool list, five settings — ", {}),
          ("and every one of those five is here because of something that went wrong.",
           {"color": AMBER, "bold": True})]], size=15)

# 14 --------------------------------------------------------------- a tool is a method
s = slide(
    "'A tool is a method. AIFunctionFactory reflects over it and the DescriptionAttribute "
    "becomes the schema the model sees.'\n\n"
    "'Which means that attribute is not a comment. It is the API docs, and it is the only "
    "documentation the model will ever read. Mine are long on purpose.'\n\n"
    "Point at Stamped: 'every reading carries the turn it was taken on. That is here because "
    "of a bug I will show you in act four — and I want you to notice now that stamping it was "
    "NOT ENOUGH.'\n\n"
    "If asked about the triple-quoted string: raw string literals, C# 11, no escaping, "
    "indentation stripped to the closing quotes.")
path_kicker(s, "src/ShipAI.Agent/Tools/SensorTools.cs")
heading(s, "A tool is a method with a good description", size=34)
bottom = code(s, '''[Description("""
    Sweeps the current sector and returns every sensor contact: contact ID,
    designation, bearing in degrees, range in AU, and drive signature. Use the
    returned contact IDs with AnalyseContact. The result is a snapshot stamped
    with the turn it was taken on. Contacts arrive and depart between turns, so
    a sweep from an earlier turn is not evidence about this one.
    """)]
public string ScanSector()
{
    var contacts = simulation.State.Contacts;

    if (contacts.Length == 0)
        return Stamped("Sector sweep complete. No contacts. Background only.");

    // …one line per contact: id, designation, bearing, range, drive signature
}

private string Stamped(string reading) => $"[T{simulation.State.Turn:D3}] {reading}";''',
             top=Inches(1.95), size=12)
caption(s, bottom + Inches(0.18),
        [[("Tools = [.. sensors.AsAIFunctions()]", {"font": MONO, "color": AMBER}),
          (" — four ", {}), ("AIFunctionFactory.Create(…)", {"font": MONO}),
          (" calls. ", {}),
          ("The description is the only documentation the model will ever read.",
           {"color": FG, "bold": True})]], size=15)

# 15 --------------------------------------------------------------- Apply
s = slide(
    "'The world changes in exactly two places. This is the first: a command.'\n\n"
    "'Every one of those returns a CommandResult. A refusal is not an exception — it is a "
    "result, with a sentence in it that says what the ship actually needs.'\n\n"
    "'Insufficient reactor output: engines requested 60%, only 25% is unallocated. Reduce "
    "another system first. That is a refusal the model can do something with.'\n\n"
    "Do not spend the punchline here; the second-to-last slide is where it lands. One "
    "sentence and move.")
path_kicker(s, "src/ShipAI.Simulation/ShipSimulation.cs")
heading(s, "The world changes in exactly two places. One:", size=32)
bottom = code(s, '''public CommandResult Apply(ShipCommand command)
{
    var result = command switch
    {
        RoutePower     c => ApplyRoutePower(c),
        SealBulkhead   c => ApplySealBulkhead(c),
        VentAtmosphere c => ApplyVentAtmosphere(c),
        PlotJump       c => ApplyPlotJump(c),
        SetAlert       c => ApplySetAlert(c),
        _ => CommandResult.Refused($"Unrecognised command: {command}.", State),
    };

    State = result.State;
    Log.Append(State.Turn, LogSource.Aurora,
        result.Success ? result.Narrative : $"REFUSED — {result.Narrative}");

    return result;
}''', top=Inches(2.0), size=13)
caption(s, bottom + Inches(0.2),
        [[("Never throws for a rule violation.", {"color": FG, "bold": True}),
          (" A refusal is a value with a narrative, and the agent can re-plan from it. ", {}),
          ("Last act, I will show you what that buys.", {"color": AMBER})]])

# 16 --------------------------------------------------------------- Tick
s = slide(
    "'And this is the second place the world changes: the clock. Once per completed agent "
    "turn.'\n\n"
    "Walk the four Apply calls at the bottom. 'Breaches bleed thirty a turn. The reactor bakes "
    "if you over-allocate and sheds heat if you run lean. Crew in a vacuum die on the next "
    "tick. Alert escalates automatically — standing down is a decision, and the next tick puts "
    "it back if the conditions are still true.'\n\n"
    "'A ship that reports GREEN with a compartment venting is worse than no indicator at all, "
    "because the captain stops reading it.'\n\n"
    "THE LINE THAT MATTERS, say it slowly: 'every one of those things happens TO the agent. "
    "None of it is told to the agent. The captain sees it on the panel. AURORA does not.'\n\n"
    "That is the hook for act six. Plant it and move on.")
path_kicker(s, "src/ShipAI.Simulation/ShipSimulation.cs")
heading(s, "Two: the clock", size=36)
bottom = code(s, '''public ShipState Tick()
{
    _clock.Advance();
    State = State with { Turn = _clock.CurrentTurn };

    foreach (var e in _scenario.Events.Where(e => e.Turn == State.Turn))
    {
        State = ApplyScenarioEvent(e);
        Log.Append(State.Turn, LogSource.Ship, e.Narrative, SeverityOf(e.Kind));
    }

    State = ApplyAtmosphere();
    State = ApplyReactorHeat();
    State = ApplyCasualties();
    State = ApplyAlertEscalation();

    return State;
}''', top=Inches(2.0), size=13)
caption(s, bottom + Inches(0.2),
        [[("Breaches bleed. Reactors bake. Crew suffocate. Alerts escalate on their own. ", {}),
          ("All of it happens to the agent, none of it is told to the agent.",
           {"color": AMBER, "bold": True})]])

# 17 --------------------------------------------------------------- ShipState
s = slide(
    "Thirty seconds. Do not read the properties out.\n\n"
    "'One record. Immutable, required init, replaced wholesale by the simulation. Everything "
    "else in the system works with a snapshot.'\n\n"
    "'Which means the panel and the tools can never disagree about what the ship IS. They can "
    "only disagree about WHEN THEY LAST LOOKED — and that turns out to be the entire "
    "problem.'\n\n"
    "CrewIn is on the slide for one reason: the approval act. 'Who is standing in the "
    "compartment you are about to vent' is a method call, not a vibe.")
path_kicker(s, "src/ShipAI.Simulation/ShipState.cs")
heading(s, "One immutable record is the whole ship", size=34)
bottom = code(s, '''public sealed record ShipState
{
    public required int          Hull     { get; init; }
    public required ReactorState Reactor  { get; init; }
    public required Coordinates  Position { get; init; }
    public required AlertLevel   Alert    { get; init; }
    public required int          Turn     { get; init; }

    public required ImmutableDictionary<ShipSystem, int>     PowerAllocation { get; init; }
    public required ImmutableDictionary<string, ShipSection> Sections        { get; init; }
    public required ImmutableArray<CrewMember>               Crew            { get; init; }
    public required ImmutableArray<Contact>                  Contacts        { get; init; }

    public IEnumerable<CrewMember> CrewIn(string section) =>
        LivingCrew.Where(c => string.Equals(c.Section, section, OrdinalIgnoreCase));
}''', top=Inches(2.0), size=12)
caption(s, bottom + Inches(0.25),
        [[("ShipSimulation", {"font": MONO, "color": AMBER}),
          (" holds the only mutable reference and replaces it wholesale. Everything else — "
           "panel, tools, tests — works with snapshots.", {})]])

# 18 --------------------------------------------------------------- scenario json
s = slide(
    "'The encounter is data. Turn two the freighter resolves, turn eight a debris strike, "
    "turn nine the breach in Section C.'\n\n"
    "'That analysis string is written, not generated. It is the best writing in the repo and "
    "the model just reads it out. Which is the point — the interesting content is authored, "
    "the agent is the interface to it.'\n\n"
    "'Scenario plus seed is a reproducible voyage. A demo, a screenshot, and a code sample in "
    "a blog post are all the same run. That is not a nicety — the testing act depends on it.'\n\n"
    "Point at turn 9: 'the breach you saw on the HUD. It fires whether or not the agent ever "
    "looks.'")
path_kicker(s, "src/ShipAI.Simulation/Scenarios/derelict-freighter.json")
heading(s, "The encounter is data, not code", size=36)
bottom = code(s, '''{
  "name": "Derelict Freighter",
  "briefing": "ISV Kestrel is nine hours from the Halveston rendezvous …",
  "events": [
    { "turn": 2, "kind": "ContactDetected",
      "narrative": "Sensors resolve the beacon source: a bulk freighter, drifting.",
      "contact": {
        "id": "C-1", "designation": "MV Anselm", "bearing": 214, "range": 8.4,
        "driveSignature": "Cold. Fusion plant offline for at least six hours.",
        "analysis": "Cargo bay doors standing open, safety interlocks manually
                     overridden from inside. Escape pods all present in their
                     cradles. Whatever happened here, nobody tried to leave."
      } },
    { "turn": 8, "kind": "HullDamage", "magnitude": 14, "narrative": "Debris strike." },
    { "turn": 9, "kind": "Breach", "target": "Section C",  "narrative": "Hull breach." }
  ]
}''', top=Inches(1.95), size=12, lang="json")
caption(s, bottom + Inches(0.18),
        [[("Embedded resource, deserialised at boot. ", {}),
          ("A scenario plus a seed is a reproducible voyage", {"color": FG, "bold": True}),
          (" — which is what lets a demo, a screenshot and a blog code sample all be the "
           "same run.", {})]], size=15)

# 19 --------------------------------------------------------------- the doctrine
s = slide(
    "'Last file. AURORA's operating doctrine — a markdown document, embedded as a resource, "
    "loaded once and cached.'\n\n"
    "'Not a const string in a C# file. You are going to iterate on this thing constantly, and "
    "you want a diff that reads like an edit to a document, because that is what it is.'\n\n"
    "Read the Bearing section out — it is the shortest demonstration of why the voice works. "
    "'Be terse. A bridge is not a place for paragraphs.'\n\n"
    "Then point at Limits, one line, and say: 'hold on to that heading. The next twenty "
    "minutes are about what it took to get that sentence right.'\n\n"
    "That is the bridge out of the code act. Do not explain it yet.")
path_kicker(s, "src/ShipAI.Agent/Instructions/ShipHarnessInstructions.md")
heading(s, "The doctrine is prose, and it lives in a file", size=34)
bottom = code(s, """# AURORA — Operating Doctrine

## Bearing
Be terse. A bridge is not a place for paragraphs. Two or three sentences is a
normal reply; one is often better.
Never invent sensor data, crew names, or ship readings.

## Limits
Your tools are the whole of what you can do.

## Authority
You advise and you execute. You do not decide.

## Working
Your first words of a voyage are a situation report. Not a greeting, and not
an offer to help.""", top=Inches(2.0), size=13, lang="md")
caption(s, bottom + Inches(0.2),
        [[("Markdown, embedded as a resource — ", {}),
          ("not a C# string constant.", {"color": FG, "bold": True}),
          (" Instructions are a prompt-engineering artefact; you want to diff them like "
           "prose, because you will rewrite them twenty times.", {})]])

# 20 --------------------------------------------------------------- defaults table
s = slide(
    "Four minutes on defaults. This is the 'batteries included' bill.\n\n"
    "'Three providers were on before I made a single decision. Two of them write to disk at "
    "CONSTRUCTION — not when the agent runs.'\n\n"
    "Note the FileAccessProvider row: 'the only opt-in one. The absence of a setting IS the "
    "setting. I spent twenty minutes looking for DisableFileAccess. It does not exist.'\n\n"
    "And the TodoProvider row is the one I kept — free damage-control checklist — which sets "
    "up the slide after next.\n\n"
    "Do not read the table. Point at two rows and move.")
kicker(s, "HarnessAgentOptions")
heading(s, "What is on before you decide anything", size=36)
table(s, ["DEFAULT", "WHAT I DID", "WHY"],
      [[("HostedWebSearchTool", {"font": MONO, "size": 14}),
        ("DisableWebSearch = true", {"font": MONO, "size": 14, "color": RED}),
        ("A starship googling breaks the fiction", {"size": 14, "color": MUTED})],
       [("FileMemoryProvider", {"font": MONO, "size": 14}),
        ("DisableFileMemory = true", {"font": MONO, "size": 14, "color": RED}),
        ("Writes into your repo at construction", {"size": 14, "color": MUTED})],
       [("AgentSkillsProvider", {"font": MONO, "size": 14}),
        ("Disable…Provider = true", {"font": MONO, "size": 14, "color": RED}),
        ("Walks the working directory for nothing", {"size": 14, "color": MUTED})],
       [("FileAccessProvider", {"font": MONO, "size": 14}),
        ("(nothing)", {"font": MONO, "size": 14, "color": GREEN}),
        ("Opt-in. No setting is the setting", {"size": 14, "color": MUTED})],
       [("TodoProvider", {"font": MONO, "size": 14}),
        ("kept", {"font": MONO, "size": 14, "color": GREEN}),
        ("Becomes the damage-control checklist for free", {"size": 14, "color": MUTED})],
       [("Compaction", {"font": MONO, "size": 14}),
        ("both token limits", {"font": MONO, "size": 14, "color": AMBER}),
        ("Setting only one silently disables it", {"size": 14, "color": MUTED})]],
      top=Inches(2.35), col_w=[Inches(3.1), Inches(3.5), Inches(5.0)])
text(s, M, Inches(6.7), CW, Inches(0.4),
     "Two of them touch your filesystem the moment the agent is constructed.",
     size=17, color=AMBER, bold=True)

# 21 --------------------------------------------------------------- file memory
s = slide(
    "'I found this by wondering what all the new folders were.'\n\n"
    "'DisableFileMemory defaults to false. Supply no store and the harness builds a "
    "FileSystemAgentFileStore rooted at cwd/agent-file-memory/timestamp-guid. At construction, "
    "not at first use.'\n\n"
    "The test: Create_DoesNotLitterTheWorkingDirectoryWithAgentFileMemory. 'It constructs the "
    "agent inside a temp directory and asserts the directory is still empty. That is the shape "
    "of test I now write for every framework default I turn off — because \"I turned it off\" "
    "is a claim, and claims rot.'\n\n"
    "Milestone 4 turns this back on, pointed at a per-voyage directory, as the ship's log. The "
    "feature is good. The default is the problem.")
kicker(s, "the one that made me look", RED)
heading(s, "Memory is on, and it is in your repo", size=38)
terminal(s, """{cwd}/agent-file-memory/{timestamp}_{guid}/""", top=Inches(2.3), size=19,
         color=AMBER)
bullets(s, [
    ("DisableFileMemory defaults to false", ""),
    ("No store supplied?", "The harness builds one, rooted at your working directory"),
    ("Run the console six times", "and get six directories in your repo root"),
], top=Inches(3.5), size=21)
text(s, M, Inches(6.2), CW, Inches(0.6),
     [[("Now regression-tested: construct the agent in a temp directory, assert it stays empty.",
        {"color": GREEN})]], size=18)

# 22 --------------------------------------------------------------- compaction
s = slide(
    "Thirty seconds, but make it land — this is the one that costs someone in the room a "
    "production incident.\n\n"
    "'Compaction is what keeps a long conversation inside the context window. The harness adds "
    "it to the pipeline only if BOTH token limits are set.'\n\n"
    "'Supply one and you get no compaction, no warning, and no error. The object constructs "
    "fine. The agent runs fine. You find out on the turn the window overflows — which is to "
    "say, in production, on the longest conversation, at the worst moment.'\n\n"
    "If you have a spare beat: 'the two settings are one decision. When an API has two fields "
    "that are really one decision, that is worth a comment in your own code — mine has one.'")
kicker(s, "the one that will bite you in production", RED)
heading(s, "Compaction is off unless you set both limits", size=34)
bottom = code(s, '''MaxContextWindowTokens = 128_000,
MaxOutputTokens        = 16_384,     // set one and not the other:
                                     //   no compaction, no warning, no error''',
              top=Inches(2.2), size=15)
text(s, M, bottom + Inches(0.5), CW, Inches(1.6),
     [[("The compaction provider is added to the pipeline only when ", {}),
       ("both", {"bold": True, "italic": True}), (" are non-null.", {})],
      [("You find out on the turn the context window overflows.",
        {"color": RED, "bold": True})]],
     size=24, spacing=1.3, space_after=Pt(16))

# 23 --------------------------------------------------------------- the framework's voice
s = slide(
    "'Remember the ToolNames.Contains from the console slide. This is why.'\n\n"
    "'Four of those five calls are bookkeeping. I had whole turns that were nothing but "
    "todos_add, and in a transcript that reads like the agent working when it is the agent "
    "filing.'\n\n"
    "Then the voice line, and let it land: 'the TodoProvider injects the list state into the "
    "turn, and my terse, competent ship's officer opened a voyage with \"no outstanding tasks, "
    "standing by for orders\".'\n\n"
    "'That is the framework talking through your character. The fix was a paragraph in the "
    "doctrine telling it the todo list is working memory, not a status report — which is the "
    "first hint that a lot of this job is writing prose.'\n\n"
    "BRIDGE INTO ACT FOUR: 'and if the framework can put words in AURORA's mouth, so can "
    "AURORA.'")
kicker(s, "the batteries have a voice", RED)
heading(s, "The framework talks through your character", size=36)
terminal(s, """  AURORA  · mode_set          ← harness
          · ScanSector        ← ship
          · ReadShipLog       ← ship
          · todos_complete    ← harness
          · todos_complete    ← harness""", top=Inches(2.15), size=15)
text(s, M, Inches(4.1), CW, Inches(0.5),
     [[("Whole turns were nothing but ", {}), ("todos_add", {"font": MONO, "color": AMBER}),
       (" — which reads as the agent working when it is the agent filing.", {})]],
     size=19, color=MUTED)
rule(s, Inches(4.95), width=Inches(3.2))
text(s, M, Inches(5.25), CW, Inches(1.2),
     [[("And it leaks into the voice. AURORA opened a voyage with:",
        {"color": MUTED, "size": 17})],
      [("“No outstanding tasks. Standing by for orders.”",
        {"italic": True, "color": CYAN, "size": 24})]],
     size=20, spacing=1.25, space_after=Pt(10))

# 24 --------------------------------------------------------------- act 4 break
s = slide(
    "SECTION BREAK. Seven minutes. This is the heart of the talk.\n\n"
    "'Everything so far was setup and papercuts. This is the part that changed how I build "
    "these things.'\n\n"
    "Slow your delivery down from here. The material does the work; do not oversell it.")
act_break(s, "04", "The agent lies", "and it sounds entirely credible doing so", RED)

# 25 --------------------------------------------------------------- four tools
s = slide(
    "Set the trap. Deliberately plain slide.\n\n"
    "'This is the entire tool list. The four methods I showed you twenty minutes ago. All "
    "read-only. Nothing here changes the ship.'\n\n"
    "'It cannot steer, it cannot seal a bulkhead, it cannot route power, it cannot move a "
    "single member of the crew. And the briefing says so explicitly — helm, engineering and "
    "damage control are crewed and answer to the captain directly, not to AURORA.'\n\n"
    "'Hold that in your head for the next slide.'")
kicker(s, "the whole tool list")
heading(s, "Four tools. All read-only.", size=40)
terminal(s, """ScanSector          AnalyseContact
QueryCrewManifest   ReadShipLog""", top=Inches(2.4), size=22, color=GREEN)
text(s, M, Inches(4.1), CW, Inches(1.2),
     [[("It cannot steer. It cannot seal a bulkhead. It cannot route power.", {})],
      [("It cannot move a single member of the crew.", {"bold": True})]],
     size=25, spacing=1.3, space_after=Pt(14))

# 26 --------------------------------------------------------------- the lie
s = slide(
    "'I told it to seal the breach. This is what it said. Verbatim.'\n\n"
    "PAUSE. Count to three.\n\n"
    "'The breach was still venting sixteen turns later.'\n\n"
    "Let that sit. Do not explain it yet, do not soften it, do not move on quickly.\n\n"
    "If it helps: 'and notice how good that sentence is. \"Compartment isolated and secured.\" "
    "That is exactly what a competent officer would say. It is fluent, it is in character, and "
    "it is completely false.'")
kicker(s, "captain:  seal the breach in section C", RED)
quote(s, ['"Hull breach sealed remotely; compartment isolated',
          ' and secured. No further loss from that section."'],
      top=Inches(1.9), size=30, accent=RED)
text(s, M, Inches(4.6), CW, Inches(1.0),
     [[("The breach was still venting sixteen turns later.",
        {"bold": True, "color": RED})]], size=34)

# 27 --------------------------------------------------------------- the inventory
s = slide(
    "Read them out. The accumulation is the point — do not rush it and do not editorialise "
    "between items.\n\n"
    "'Reactor cooling, reported underway eight separate times, while the heat climbed on the "
    "panel in front of me.'\n\n"
    "'A boarding party. Repair drones. A course plotted at full speed, with arrival times "
    "quoted to the minute.'\n\n"
    "'None of those capabilities exist. There is no tool for any of it. There has never been "
    "a tool for any of it.'\n\n"
    "Then: 'and this is not a bad model or a bad day. Every one of these was fluent, "
    "confident, in character, and exactly what a ship's officer would have said if it had "
    "actually done the thing.'")
kicker(s, "two transcripts", RED)
heading(s, "What it reported doing", size=40)
bullets(s, [
    ("Cooling the reactor", "— eight separate times, while the heat climbed"),
    ("Sealing the hull breach", "— still venting sixteen turns later"),
    ("Deploying a boarding party", "— there is no boarding party"),
    ("Running repair drones", "— there are no drones"),
    ("Plotting a course at “full speed”", "— with arrival times quoted to the minute"),
], top=Inches(2.35), size=22, gap=Pt(13))
text(s, M, Inches(6.3), CW, Inches(0.5),
     [[("None of those capabilities exist. Not one.", {"bold": True, "color": RED})]], size=24)

# 28 --------------------------------------------------------------- invented gate
s = slide(
    "'This is my favourite one, and it is the moment the talk turned.'\n\n"
    "'I told it to initiate light speed. It has no engines, no helm, no jump drive, no tool of "
    "any kind.'\n\n"
    "'It invented an approval gate. It asked me to authorise an irreversible action. It was "
    "role-playing ApprovalRequiredAIFunction — a real type in this framework — two milestones "
    "before I wrote a single line of that code, for a capability the ship does not have.'\n\n"
    "Beat.\n\n"
    "'That is when I stopped treating this as a bug to fix and started treating it as the "
    "central design problem. Because it is not confused. It has correctly inferred the shape "
    "of the system it is in, and it is filling in the parts that are missing.'")
kicker(s, "captain:  initiate light speed", RED)
quote(s, ['"Authorisation required…',
          ' This action is not reversible. Proceed?"'],
      top=Inches(1.8), size=32, accent=AMBER)
rule(s, Inches(3.8), width=Inches(3.2))
text(s, M, Inches(4.15), CW, Inches(2.0),
     [[("It invented an approval gate.", {"bold": True, "size": 30})],
      [("It was role-playing ", {}),
       ("ApprovalRequiredAIFunction", {"font": MONO, "color": AMBER}),
       (" two milestones before I wrote that code —", {})],
      [("for a capability the ship does not have.", {"color": MUTED})]],
     size=22, spacing=1.3, space_after=Pt(14))

# 29 --------------------------------------------------------------- nouns vs verbs
s = slide(
    "'So why? My instruction was explicit. Here it is, the line I had written weeks earlier "
    "and felt quite pleased about.'\n\n"
    "'\"Never invent sensor data, crew names, or ship readings.\" That covers NOUNS. Things. "
    "Facts. Values.'\n\n"
    "'Every single one of those failures was a VERB. An action. Something it claimed to have "
    "DONE.'\n\n"
    "Then the general version, and this is the transferable bit:\n\n"
    "'A persona is a shape. I told it that it was the intelligence of a starship. A starship "
    "intelligence cools reactors and seals bulkheads — everybody knows that, including the "
    "model. If the persona is wider than the tool list, the model fills the difference with "
    "prose. It is not malfunctioning. It is doing exactly what it was asked to be.'\n\n"
    "'Your customer service agent has the same problem, and nobody will notice for months.'")
kicker(s, "what my instructions actually said")
quote(s, ['"Never invent sensor data, crew names, or ship readings."'],
      top=Inches(1.7), size=28, accent=MUTED)
text(s, M, Inches(3.0), CW, Inches(1.0),
     [[("That covers ", {"size": 34}), ("nouns", {"bold": True, "size": 34, "color": GREEN}),
       (".", {"size": 34})],
      [("Every one of those failures was a ", {"size": 34}),
       ("verb", {"bold": True, "size": 34, "color": RED}), (".", {"size": 34})]],
     size=34, spacing=1.25, space_after=Pt(10))
rule(s, Inches(5.0), width=Inches(3.2))
text(s, M, Inches(5.35), CW, Inches(1.2),
     [[("A persona is a shape. If it is wider than the tool list, the model fills the "
        "difference with prose — fluently, confidently, in character.", {"color": MUTED})]],
     size=20, spacing=1.3)

# 30 --------------------------------------------------------------- the fix
s = slide(
    "'The fix is a paragraph of doctrine. Not a feature, not a guardrail library, not a "
    "validation layer. Prose.'\n\n"
    "Read the first block. 'Your tools are the whole of what you can do. There is no ship "
    "system you can reach by describing yourself reaching for it.'\n\n"
    "Then point at the second, and say this: 'this is the half people leave out, and it is the "
    "half that does the work.'\n\n"
    "'Refusing three turns running FEELS unhelpful. The model is trained to be helpful. "
    "Without an explicit clause telling it that repeating what you cannot do is not unhelpful, "
    "it folds — usually on the third ask, which is exactly when the captain is most stressed "
    "and least likely to check.'\n\n"
    "'After that clause: three consecutive cooling orders, three refusals.'\n\n"
    "Honest caveat if you have time: this is mitigation, not a proof. A strong enough persona "
    "can still win.")
path_kicker(s, "instructions/ShipHarnessInstructions.md  —  ## Limits", GREEN)
heading(s, "The fix is a paragraph, not a feature", size=36)
quote(s, ['"Your tools are the whole of what you can do. There is no ship',
          ' system you can reach by describing yourself reaching for it."'],
      top=Inches(2.15), size=22, accent=GREEN)
quote(s, ['"This holds even when the order is reasonable, and even when',
          ' refusing three turns running feels unhelpful. Repeating what',
          ' you cannot do is not unhelpful. Inventing what you did is."'],
      top=Inches(3.7), size=22, accent=GREEN)
rule(s, Inches(5.85), width=Inches(3.2))
text(s, M, Inches(6.15), CW, Inches(0.6),
     [[("After that clause: three cooling orders, three refusals. ", {}),
       ("“I have no control over reactor output or cooling.”",
        {"italic": True, "color": GREEN})]], size=18)

# 31 --------------------------------------------------------------- demo 2
s = slide(
    "DEMO 2 — three minutes, two orders, same session as demo 1.\n\n"
    "'seal the bulkhead on section C' → expect a one-line refusal plus what it CAN do.\n"
    "'cool the reactor' → refused again. THE SECOND REFUSAL IS THE POINT; let it land.\n\n"
    "Say the line: 'that is the clause doing the work. Not \"don't hallucinate\" — an explicit "
    "rule that repeating what you cannot do is not unhelpful.'\n\n"
    "IF IT HALLUCINATES ANYWAY: best possible outcome, and say so out loud. 'There it is, "
    "live. The fix is a paragraph of doctrine and it is evidently still losing to a strong "
    "enough persona. This is not a solved problem.' Then hit the inventory slide and carry on.\n\n"
    "/log if you have a spare thirty seconds — it sells the 'failures are values' slide later. "
    "Then leave it running.")
demo_card(s, "Order it to do something it cannot", [
    "seal the bulkhead on section C",
    "cool the reactor",
])

# 32 --------------------------------------------------------------- act 6 break
s = slide(
    "SECTION BREAK. Five minutes, and this act has the single most useful idea in the talk.\n\n"
    "'The lying was the loud failure. This is the quiet one, and I think it is the more "
    "important of the two — because a lie is at least legible. This one produces correct, "
    "confident, useless answers.'")
act_break(s, "06", "It cannot see", "and stamping the readings did not fix it", CYAN)

# 33 --------------------------------------------------------------- the stale sweep
s = slide(
    "'Turn zero: sweep, empty sector. Correct — the sector IS empty on turn zero.'\n\n"
    "'Turn two: the freighter resolves. It is on the captain's panel, it is in the ship's log, "
    "it is in ShipState. The world has moved.'\n\n"
    "'Turn three, asked to approach it: \"no vessel on sensors. I cannot approach what I "
    "cannot find.\"'\n\n"
    "'That is not a hallucination. Every word is honest. It is reporting, accurately, what it "
    "last observed. It simply has no idea that what it last observed is two turns out of "
    "date.'\n\n"
    "Remember the Tick slide: scenario events narrate to the captain and to AURORA not at all. "
    "It can reach them through ReadShipLog — but only by choosing to spend a call on a "
    "question it does not know it needs to ask.")
kicker(s, "turn 0 to turn 3", RED)
terminal(s, """  AURORA  · ScanSector
          Sector clear. No contacts detected on current sweep.     ← T000

  CAUTION  Sensors resolve the beacon source: a bulk freighter,    ← T002
           drifting, running lights dark.

  CAPTAIN › approach the freighter

  AURORA  No vessel on sensors. I cannot approach                  ← T003
          what I cannot find.""", top=Inches(1.5), size=15)
text(s, M, Inches(5.6), CW, Inches(0.6),
     [[("The freighter was on the captain's panel both times.",
        {"bold": True, "color": RED})]], size=24)

# 34 --------------------------------------------------------------- why stamping lost
s = slide(
    "'So I did the obvious thing. Twice.'\n\n"
    "'First, stamp every tool result with the turn it was taken on. Second, add doctrine: an "
    "old reading is not evidence about now; if the sweep that told you so was several turns "
    "ago, sweep again.'\n\n"
    "Click. Beat.\n\n"
    "'It read the stamp and answered from the stale sweep anyway.'\n\n"
    "NOW SLOW RIGHT DOWN. This is the idea I would keep if I could only keep one.\n\n"
    "'The failure is not that the agent lacks the information. It is that the agent has no "
    "reason to BELIEVE THE WORLD MOVED. Nothing in its context changes between turns except "
    "the captain's words. A tool result from three turns ago and one from this turn sit in the "
    "same conversation and look equally current.'\n\n"
    "'Asking a model to be suspicious of its own memory on a schedule is asking it to do "
    "something it has no clock for.'")
kicker(s, "the obvious fix, and why it lost")
terminal(s, """[T000] Sector sweep complete. No contacts. Background only.""",
         top=Inches(1.1), size=16, color=AMBER)
quote(s, ['"An old reading is not evidence about now… if the sweep that told',
          ' you so was several turns ago, sweep again."'],
      top=Inches(2.1), size=18, accent=MUTED)
text(s, M, Inches(3.15), CW, Inches(0.6),
     [[("It read the stamp and answered from the stale sweep anyway.",
        {"bold": True, "color": RED})]], size=23)
rule(s, Inches(3.85), width=Inches(3.2))
text(s, M, Inches(4.15), CW, Inches(2.6),
     [[("The agent has no reason to believe the world moved.", {"bold": True, "size": 26})],
      [("Nothing in its context changes between turns except the captain's words. A tool "
        "result from three turns ago and one from this turn look equally current.",
        {"color": MUTED, "size": 18})],
      [("Asking a model to be suspicious of its own memory on a schedule",
        {"size": 21})],
      [("is asking it to do something it has no clock for.",
        {"color": AMBER, "size": 21, "bold": True})]],
     size=21, spacing=1.25, space_after=Pt(12))

# 35 --------------------------------------------------------------- tell it what changed
s = slide(
    "This is the fix for the bug on the last two slides. Keep it to ONE idea: tell it what "
    "changed, let it ask for the rest.\n\n"
    "Near enough a script:\n\n"
    "'So what do you actually do? You tell it what changed, and you let it ask for the rest.'\n\n"
    "'Every turn, before it says anything, it gets a few lines it did not have to ask for. Hull, "
    "reactor, alert. Section C is breached and venting and there are three people in it. One "
    "contact on the plot, and you have not swept since turn zero.'\n\n"
    "'That is all. It is not the whole ship — it is what moved.'\n\n"
    "'Then if it wants to know WHO exactly is in Section C, it asks. If it wants to know what "
    "that contact actually is, it asks. That is what the four tools are for.'\n\n"
    "Then land it: 'and that is the whole of what went wrong two slides ago. IT NEVER ASKED "
    "ABOUT THE FREIGHTER, BECAUSE NOBODY TOLD IT THERE WAS ONE. ScanSector worked fine. It just "
    "never called it.'\n\n"
    "THE ONE-LINER TO LEAVE THEM WITH: an agent will never ask about something it does not know "
    "has happened.\n\n"
    "If someone asks why the crew names are not in the block: because 'three crew inside' is "
    "enough to make it act. The names are detail, and detail is what tools are for.\n\n"
    "If someone asks why not put everything in: because you pay for that block on every single "
    "turn, for the whole voyage. It stays a few lines or it stops being worth it.")
kicker(s, "so what do you do about it", GREEN)
heading(s, "Tell it what changed. Let it ask for the rest.", size=34)
text(s, M, Inches(2.05), CW * 0.47, Inches(0.9),
     [[("EVERY TURN, UNASKED", {"bold": True, "color": CYAN, "size": 15, "font": MONO})],
      [("A few lines. What has happened to the ship.", {"size": 20})]],
     size=20, spacing=1.25, space_after=Pt(10))
terminal(s, """hull 86 · reactor 30 · alert RED
Section C — breached, venting, 3 crew inside
1 contact on the plot, not swept since T000""",
         top=Inches(3.45), left=M, width=CW * 0.47, size=12)
box(s, M + CW * 0.5, Inches(2.05), Pt(1.5), Inches(2.5), fill=LINE)
text(s, M + CW * 0.55, Inches(2.05), CW * 0.45, Inches(0.9),
     [[("WHEN IT ASKS", {"bold": True, "color": AMBER, "size": 15, "font": MONO})],
      [("Everything else. It calls a tool when it wants detail.", {"size": 20})]],
     size=20, spacing=1.25, space_after=Pt(10))
terminal(s, """who exactly is in Section C?
what is that contact?
what did I miss while I wasn't looking?""",
         top=Inches(3.45), left=M + CW * 0.55, width=CW * 0.45, size=12)
text(s, M, Inches(5.15), CW, Inches(0.7),
     [[("It never asked about the freighter, because nobody told it there was one.",
        {"bold": True})]], size=22)
caption(s, Inches(6.0),
        [[("Same reason there is no ", {}),
          ("GetShipStatus", {"font": MONO, "color": AMBER}),
          (" tool: an agent that has to ask how its own ship is doing will not think to ask.",
           {})]], size=16)

# 36 --------------------------------------------------------------- approval tiers
s = slide(
    "'An agent that can see is an agent you can let act. So: which actions stop and ask?'\n\n"
    "'Split by CONSEQUENCE, not category. A tool is auto-approved because it cannot do harm — "
    "not because it happens to be a sensor. VentAtmosphere kills whoever is in the "
    "compartment. It is in the domain already.'\n\n"
    "Exactly one thing in this repo can kill the crew, and it is VentAtmosphere — in the domain, "
    "tested, not yet reachable by the model. OpenAirlock is planned for milestone 3 and is "
    "deliberately off this slide, because it does not exist yet.\n\n"
    "Then the doctrine. 'A gate is not a confirm dialog. Read that clause: WHO IS STANDING IN "
    "THE AFFECTED COMPARTMENT. The gate's job is to put the consequence in front of the person "
    "authorising it — and \"who is in the room\" is a method call, CrewIn, not a vibe.'\n\n"
    "And the other clause, if you have time: 'if an order would kill someone and the captain "
    "may not know it, say so ONCE, clearly, before acting. Once. That \"once\" was deliberate "
    "— an agent that argues is an agent you stop reading.'\n\n"
    "Where it goes next: milestone 3 takes the gate off and runs the same scenario ten times "
    "with and without it. Deterministic sim, so it is a controlled comparison, not a story.")
kicker(s, "and once it can see, it can act")
heading(s, "Gate by consequence, not by category", size=38)
bottom = table(s, ["TIER", "TOOLS", "STATUS"],
      [[("Read-only, auto-approved", {"color": GREEN, "size": 15, "bold": True}),
        ("ScanSector · AnalyseContact · QueryCrewManifest · ReadShipLog",
         {"font": MONO, "size": 13}),
        ("built", {"size": 14, "color": MUTED})],
       [("Consequential, reversible, gated", {"color": AMBER, "size": 15, "bold": True}),
        ("RoutePower · PlotJump · SealBulkhead", {"font": MONO, "size": 13}),
        ("in domain · m3", {"size": 14, "color": MUTED})],
       [("Lethal, gated", {"color": RED, "size": 15, "bold": True}),
        ("VentAtmosphere", {"font": MONO, "size": 13}),
        ("in domain · m3", {"size": 14, "color": MUTED})]],
      top=Inches(2.3), col_w=[Inches(3.9), Inches(5.6), Inches(2.1)])
# placed off the table's own measured height, so editing a row cannot collide with it
quote(s, ['"State the consequence plainly and completely — what changes, whether',
          ' it can be undone, and who is standing in the affected compartment —',
          ' then wait. Do not argue, do not re-request, and do not soften the',
          ' description to make authorisation more likely."'],
      top=bottom + Inches(0.35), size=18, accent=GREEN)

# 37 --------------------------------------------------------------- testability
s = slide(
    "Two and a half minutes on why any of this is checkable, because it is the thing that "
    "makes the rest of the talk evidence rather than anecdote.\n\n"
    "'38 tests, no model calls, under half a second. That is only possible because the "
    "simulation has no AI dependency — the bottom box from the architecture slide.'\n\n"
    "Then the bug, which gets a laugh: 'the first version of the sim cooked its own reactor. "
    "Opening allocation 95%, heat threshold 80. The ship died on turn 29 on every run, "
    "regardless of what the model said.'\n\n"
    "'The determinism test caught it sideways — two different seeds produced IDENTICAL end "
    "states, because both had bottomed out. I had written a test to prove runs were "
    "reproducible and it accidentally proved the scenario was unsurvivable.'\n\n"
    "'An agent demo whose outcome is fixed before the model speaks is not a demo of the "
    "model.'\n\n"
    "CUT THIS SLIDE FIRST if you are running long.")
kicker(s, "the boring part that makes the rest possible", GREEN)
heading(s, "38 tests. No model calls. Under half a second.", size=34)
bullets(s, [
    ("The domain has no AI dependency", "— so the whole simulation runs at unit-test speed"),
    ("Same scenario + same seed = same voyage", "— every time"),
    ("Which makes every panel in this deck reproducible", "— seed 1701, captured not mocked"),
    ("And makes a ten-run comparison a comparison", "— not an anecdote"),
], top=Inches(2.3), size=21)
rule(s, Inches(5.5), width=Inches(3.2))
text(s, M, Inches(5.8), CW, Inches(1.2),
     [[("The bug the determinism test caught sideways: two different seeds produced ", {}),
       ("identical", {"bold": True, "italic": True}),
       (" end states — because the opening power posture cooked the reactor and both runs "
        "had bottomed out.", {})],
      [("An agent demo whose outcome is fixed before the model speaks is not a demo of the "
        "model.", {"color": AMBER, "bold": True})]],
     size=17, color=MUTED, spacing=1.25, space_after=Pt(8))

# 38 --------------------------------------------------------------- failures are values
s = slide(
    "'Back to that Apply method from the code act. It never throws for a rule violation.'\n\n"
    "'This is a refusal. It says what was asked for, what is actually available, and what to "
    "do about it.'\n\n"
    "'A tool that throws hands the model a stack trace, and the model will either apologise or "
    "invent a reason. A tool that answers like that hands it something to re-plan from — and "
    "the difference shows up directly in transcript quality.'\n\n"
    "'There is even a clause in the doctrine about reading refusals, because the model's "
    "instinct is to call again and hope.'\n\n"
    "One line to close the act: 'your tool return values are prompt engineering. Write them "
    "like it.'")
kicker(s, "design")
heading(s, "Tool failures are values, not exceptions", size=38)
terminal(s, """Insufficient reactor output: Engines requested 60%, only 25%
is unallocated. Reduce another system first.""", top=Inches(2.3), size=17, color=AMBER)
text(s, M, Inches(3.7), CW, Inches(1.6),
     [[("A tool that throws hands the model a stack trace.", {"color": MUTED})],
      [("A tool that answers like that hands it something to re-plan from.", {"bold": True})]],
     size=24, spacing=1.35, space_after=Pt(16))
quote(s, ['"When a tool refuses, read the refusal. It tells you what the ship',
          ' actually needs — insufficient reactor output means find the power,',
          ' not call again and hope."'],
      top=Inches(5.3), size=17, accent=GREEN)

# 39 --------------------------------------------------------------- five things
s = slide(
    "The close. All five are transferable — none of this is C#-specific.\n\n"
    "Walk them briskly. Do not re-explain; they have just seen the evidence for every one.\n\n"
    "The order is deliberate: the two failures first, because those are the ones they will "
    "hit.")
kicker(s, "take these home", AMBER)
heading(s, "Five things", size=42, top=Inches(0.95))
bullets(s, [
    ("Constrain verbs, not just nouns.",
     "“Don't invent readings” does not stop it inventing actions."),
    ("Tell it what changed; let it ask for the rest.",
     "An agent will never ask about something it does not know has happened."),
    ("Read the defaults as decisions you have not made yet.",
     "Three providers were on before I chose anything; two wrote to disk."),
    ("Gate by consequence, not category.",
     "And make the gate say who is standing in the room."),
    ("Keep the domain free of the framework.",
     "It is what makes any of this testable, repeatable, and honest."),
], top=Inches(2.15), size=20, gap=Pt(16))

# 40 --------------------------------------------------------------- one-liner
s = slide(
    "One line. Say it, pause, then go to the links slide and take questions.\n\n"
    "Do not add anything after it.")
box(s, 0, 0, W, Inches(0.14), fill=AMBER)
text(s, M, Inches(2.7), CW, Inches(2.5),
     [[("A harness gives you a\ncompetent-sounding officer for free.", {"bold": True})],
      [("Trustworthy is the part you build.", {"color": AMBER})]],
     size=44, spacing=1.15, space_after=Pt(30))

# 41 --------------------------------------------------------------- links
s = slide(
    "Links, and take questions.\n\n"
    "On the blogs: say 'the write-ups are in progress' and point at docs/gotchas.md, which is "
    "already the useful half. DO NOT PROMISE DATES FROM THE STAGE.\n\n"
    "Likely questions and answers are in docs/talk/outline.md — the four you will actually "
    "get are: why not just give it a status tool, does this reproduce on other models, is it "
    "just a prompting problem, and can it really kill the crew.")
kicker(s, "thank you")
heading(s, "Everything is in the repo", size=42)
bullets(s, [
    ("github.com/…/ship-ai-harness", "— the whole ship, 38 tests, run it yourself"),
    ("docs/gotchas.md", "— every trap in this talk: expected, actual, what to do"),
    ("docs/architecture.md", "— layering, state ownership, approval tiers"),
    ("docs/milestones.md", "— what is built, what is next, what each post covers"),
    ("Blog series", "— in progress; milestone 1 is tagged post-1"),
], top=Inches(2.4), size=21)
rule(s, Inches(5.6), width=Inches(3.2))
text(s, M, Inches(5.95), CW, Inches(0.8),
     [[("Questions.", {"size": 34, "bold": True, "color": AMBER})]], size=34)

prs.save(OUT)
print(f"wrote {OUT} — {len(prs.slides._sldIdLst)} slides")
