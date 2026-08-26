# THE BINDER REWORK — from spreadsheet-in-a-hand-font to a founder's desk

The owner's verdict on v1: confusing, unclear, not beautiful. The QA pass
made the pages CORRECT (no collisions, no tofu, measured wrap) — but
correctness is not design. The v1 pages are lists of same-weight text
lines; the eye has no entry point, the numbers hide inside sentences, and
the game's own drawn language (jars, pies, washes, cards) stops at the
binder's edge.

## The diagnosis, in five sentences

1. NO HIERARCHY: every row is the same size, color and shape — a tired
   player cannot find the one number that matters.
2. TEXT AS DATA: money lives inside prose ("out: rent $150 · payroll $0 ·
   infra $50…") instead of aligned columns and drawn shapes.
3. NO GROUPING: one flat pane per tab; sections bleed together without
   paper structure.
4. NOTHING DRAWN: the game draws clocks, jars and pies everywhere except
   here, where the densest numbers live.
5. TOO MUCH AT ONCE: 12-16 rows of 18-24px text on one pane. Fewer
   things, bigger, with detail behind a press.

## THE NEW GRAMMAR — every desk, no exceptions

```
┌────────────────────────────────────────────────────────────┐
│  THE HERO BAND (y6..~150)                                  │
│  one drawn instrument + ONE big number + one plain         │
│  sentence. The answer to "how is this doing?" in 1 second. │
├──────────────────────┬─────────────────────────────────────┤
│  CARD                │  CARD                               │
│  a wobbled paper     │  4-6 rows max, label left,          │
│  card with a pen     │  MONEY RIGHT-ALIGNED in one         │
│  title; ≤6 facts     │  column; controls on the row        │
├──────────────────────┴─────────────────────────────────────┤
│  THE TEACHING FOOT (last ~56px): computed line in blue,    │
│  law line in faint ink, warning outranks both              │
└────────────────────────────────────────────────────────────┘
```

**Law 1 — THE HERO ANSWERS THE TAB'S QUESTION.** Each desk exists to
answer one question; the hero band answers it at 44-64px with a drawn
instrument beside it, and one sentence under it in plain words. Everything
else is supporting detail.

**Law 2 — MONEY LIVES IN COLUMNS, NEVER IN SENTENCES.** Inside cards,
every dollar right-aligns at the card's money column. Sentences may name
concepts; only the teaching foot may put a number in prose.

**Law 3 — CARDS, NOT LISTS.** Content groups onto 2-4 paper cards per
pane (DeskKit.card_frame: wobbled edge, pen title, its own money column).
A card holds at most 6 rows. If a desk wants more, it wants a DETAIL
state, not a seventh row.

**Law 4 — DRAW THE SHAPE OF THE IDEA.** In/out is a two-bar drawing.
The funnel is a funnel. Debt is the jar. Heat is a colored dot. Progress
is a fill. Strikes are pen crosses. A number with a natural shape gets
the shape FIRST and the digits second.

**Law 5 — THREE INK WEIGHTS ONLY.** Hero (44-64), row (26-28), detail
(20-22). Secondary facts take the alpha ladder, never a fourth size.

**Law 6 — AIR IS A FEATURE.** ≥18px between rows, ≥24px between cards,
one idea per row. When in doubt, delete a line: the binder is a desk,
not a database.

**Law 7 — COLOR TELLS ONE STORY.** Sage = money in / healthy. Coral =
attention / money bleeding. Blue = the world explaining itself. Yellow =
the pool/hype accents already established. Never two coral stories on
one pane (the ≤2 budget stands).

## PER-DESK REWORK (hero → cards → foot)

### vitals — "how are we doing?"
HERO: the company's pulse — cash big, with runway as a drawn fuse-length
under it ("runway ▓▓▓▓░░ 14 weeks"), one sentence: "alive, losing $218 a
week." CARDS: The Company (era, week, crew count, customers) · The Meters
(morale/product/hype as three small drawn dials or fills — NOT sentences)
· The Charts (customer + hype sparks side by side, halved height). KILL:
the prose burn line (the ledger owns it), the beliefs paragraph (one line).

### the ledger — "where does the money go?"
HERO: THE BOTTOM LINE — "+$418 a week" at 56px on a sage/coral wash chip,
under it the two-bar drawing: IN ▓▓▓▓▓▓ $1,240 / OUT ▓▓▓▓▓▓▓▓ $1,458,
each bar segmented by lane (hover-free: segments labeled below by the two
largest + "…"). CARDS: THE MIX (4 channel steppers, compact 2×2 grid —
each cell: name, $/wk, one-word effect) · THE ORG (sales/care/rnd/office
same 2×2) · fixed costs card (rent/payroll/infra/catalog/standing —
money column, no sentence). FOOT: CAC/LTV/payback computed line + the
rules line. KILL: the 8-row lever list (becomes two 2×2 grids), the
"out:" sentence (becomes the card + bar segments).

### the bank — "what do we owe and can we borrow?"
HERO: DEBT $9,400 big with the worst rate beside it; if debt-free, the
credit line available at this era ("the bank would lend ~$12,000 at
4%/wk"). The jar drawing fills with debt share of valuation. CARDS: THE
QUOTE (borrow/term steppers + payment preview + SIGN) · THE NOTES (filed
letters, one row each: kind, balance, $/wk, weeks left, [repay]) ·
FORECAST (4 numbers in 4 cells with week labels, not a sentence). BOOKS
mode keeps the grouped statement but in money columns on two cards
(IN/OUT) with the two-bar drawing repeated small. KILL: paragraph-style
quote text.

### pricing — "what do we sell and what does each sale earn?"
HERO: "each customer pays ≈ $18/wk · costs $6 to serve → margin $12" as
the big line with a small two-bar per-customer drawing. CARDS: one card
PER OFFER (max 3 visible + "+N more"): name big, price big right, under
it "street $12 · serve $5 · margin $7/unit" in the money column, demand
verdict as ONE colored word (fair/deal/pricey/absurd), the ± steppers on
the card edge, ▸ opens DETAIL (unchanged five-state machine below).
KILL: the run-on verdict sentences; the bottom summary sentence (moves to
hero).

### customers — "who is coming and staying?"
HERO: the customer count big + this week's net (+12 / −3) colored, spark
behind it as a wash. THE FUNNEL DRAWN AS A FUNNEL: four narrowing pen
trapezoids (reach → leads → signed → kept) with the numbers inside each
mouth — analytics fog keeps lower stages as "?" shapes. CARDS: per-
channel CAC (4 cells) at analytics≥1 · cohort line at ≥2. Enterprise
runs: the stage board IS the pane (already good) — give each column a
narrowing header so the board itself reads as a funnel. KILL: prose
funnel lines.

### product — "what are we building and how solid is it?"
HERO: v0.62 big + the debt jar beside it + one sentence ("shipping
steady; debt is billing you −4% velocity"). CARDS: THE BOARD (bet cards
— keep, they're already cards; tighten to the grammar: name, kind chip,
cost/odds in the money column, commit arm) · THE BENCH strip (hardware)
stays but its status line becomes cells, not a sentence. FOOT unchanged.

### crew — "who works here and who's asking?"
HERO: N people · payroll $X/wk big; morale as a small drawn face-meter
(the game has smiley/frown coins already — reuse). CARDS: PEOPLE (rows:
name, role, skill pips, $/wk right, coral ask-chip when raise wanted) ·
HIRING (open roles as small cards with the advert stepper + a live
"≈2 applicants/wk" read) · APPLICANTS (cards — keep). KILL: fully-loaded
prose (one teaching-foot line instead).

### cap table — "who owns what and what's the company worth?"
HERO: the pie IS the hero — bigger (500px), your slice % as the big
number beside it with "your slice today ≈ $X" under. CARDS: THE ROUNDS
(rows with money column) · THE BOARD (covenant target vs now as a small
two-bar + strike crosses + countdown). The offer banner stays coral at
top. KILL: the wall of governance sentences (each becomes a card row).

### the street — "what is the world doing to us?"
HERO: the season banner as drawn weather (winter: coral band with a
drawn snow-scribble; boom: sage sun-scribble) + one sentence. CARDS: one
card PER RIVAL (max 3): name big, posture words as chips, last-3 log
lines with week stamps. KILL: log_block walls; investors list compresses
to one row of names.

### threats — "what could kill us?"
HERO: the loudest item BIG with its desk named ("no price on the wall —
pricing"), count of the rest under it. CARDS: THE LIST (rows: severity
dot, label, desk word as a pressable jump via focus_desk) · the spillover
stays. This desk becomes the command center it was meant to be.

## NEW DESKKIT PRIMITIVES (built once, both engines)

- `hero_band(b, big_text, sentence, color)` — the 56px number + sentence,
  optional instrument slot at the left 120px.
- `card_frame(b, x, y, w, h, title)` → returns the card's content origin
  + its money column x; wobbled paper, pen title 24px.
- `money_row(b, card, label, value_str, color?, control?)` — label left,
  $ right-aligned at the card's column; optional ± control slot.
- `twobar(b, x, y, w, a_label, a_val, b_label, b_val, a_col, b_col)` —
  the in/out two-bar with segment caps and end labels.
- `funnel_shape(b, x, y, w, stages[{label, value_text, known}])` — four
  narrowing pen trapezoids, "?" fills when fogged.
- `meter(b, x, y, w, frac, col, label)` — a drawn fill (fuse/progress).
- `grid2(b, x, y, cells[{name, value, effect, on_minus, on_plus}])` —
  the 2×2 compact lever cell.
- `sev_dot(b, x, y, severity)` — the attention dot in the ramp color.

Existing primitives (stepper, review card, arm, board, pips, footer,
spark, jar, pie, clock) stay and slot into cards.

## WHAT DOES NOT CHANGE

Engine reads, desk state machines, Esc contract, bang registry, the
pre-roll card, probe guards, the coral budget, the type scale's three
weights, PatrickHand-only glyphs, twin parity law, the 15-check gate —
the rework must land green on all of it. Receipts and teaching feet keep
their words; the rework moves WHERE things live, not what is true.

## ACCEPTANCE (per desk, on top of the 15 checks)

- The tab's question answered by the hero alone, screenshot test: cover
  everything below y150 — do you know how you're doing? 
- No dollar inside a sentence above the teaching foot.
- ≤3 cards + hero + foot per pane state.
- At least one DRAWN shape per desk (not counting text).
- A stranger's 5-second read: the three biggest things on the page are
  the three most important.
