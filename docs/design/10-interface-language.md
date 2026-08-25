# 10 — THE INTERFACE LANGUAGE (the design system all nine desks speak)

Binding for every surface the lane specs 01–09 add. The spine (00 §10–11) owns
*where* things go and what fits; this file owns *what everything is made of*:
the palette's meanings, the type scale, the paper, the motion, the components,
and the clarity/interaction contracts. A desk that follows 00's placement and
10's language ships at the bar: **genuinely good, actually clear, explained,
great interaction, visually beautiful**. A desk that follows only one of them
does not ship.

Sources of truth this file codifies (read them before building anything):
Godot `game/src/ui/binder.gd`, `game/src/screens/garage_view_screen.gd`,
`game/src/journal/journal_page.gd`, `game/src/ui/paper_input.gd`; Unity
`unity/Assets/Scripts/App/DrawnUI.cs` (+ `DrawnUI.Boil.cs`, `InkBoil.cs`),
`unity/Assets/Scripts/Game/GameUi.cs`, `BinderScreen.cs`, `DrawnChart.cs`,
`JournalPage.cs`, `unity/Assets/Scripts/App/Curtain.cs`.

The one-sentence law, from the code's own comments: **drawn, breathing, never
chrome** — ink on cream, one pen, wobbled edges, motion quantized to the hand's
own clock, and every number explained in the breath it appears.

---

## 1. THE VISUAL DNA

### 1.1 The palette — six colors, each with a JOB

Hex values are constants in `binder.gd` / `DrawnUI.cs`. Never introduce a
seventh color; never use one of these for another's job.

| name | hex | job (verified against every current call site) |
|---|---|---|
| CREAM | `F2EAD3` | paper. The only surface. Cards, sheets, buttons are cream. |
| INK | `1E1E1E` | the writing and the edges. All prose, all borders, all rules. Meaning is carried at full alpha; supporting text steps down the alpha ladder (§1.2). |
| PEN / CORAL | `E86A5C` | **the founder's pen + the world's alarm.** Three uses, nothing else: (a) *what you chose / what is yours* — the active-tab ring, your lever and price values, "your slice", signature strokes, pen crosses; (b) *attention* — bangs `!`, warnings, deadlines, clocks, debuffs ▼, cold leads, arm-state labels; (c) *money bleeding* — negative net, THE RED, burning channels, losing prices. |
| SAGE | `8FA582` | **healthy / money-in.** Positive net, buffs ▲, morale + customer sparks, hot leads, bet-progress fill, investors' slice, the boom banner. |
| BLUE | `6E8CA0` | **the world explaining itself.** Unit-economics summaries, teaching footers, commitments ↻, "FREE ON PURPOSE", cash spark, cofounders' slice. Blue is the tone of a patient accountant: never alarmed, never selling. |
| YELL | `F4B942` | **heat, hype, and the furniture.** The clipboard clip, hype spark, warm leads, the LEADS funnel bar, the option-pool slice. |
| — | black @ α | shadows only: paper (4,5)@0.35 button · (7,9)@0.18 draft card · (8,12)@0.25–0.30 sheet/clipboard · scrim (0.05,0.05,0.06)@0.55 · journal dim 0.45. |

**The heat ramp** (leads, utilization, any danger→health scale) is always
CORAL → YELL → SAGE, coloring **one word**, never a whole line and never a
fill: `40 seats · warm · wk 3` colors only "warm".

**The coral budget.** Coral in role (a) may repeat per row (each lever's `$X/wk`
is the founder's pen). Coral in roles (b)+(c) is rationed: at most **2 warning
lines per pane** (further items live on `threats`), one bang per tab, and at
most **one pulsing element per screen** (§2.8). When everything is coral,
nothing is.

**Tints, not fills.** SAGE/BLUE/YELL paint fills only inside drawn instruments
(chart bars/slices/fills at 0.5–0.75 α under an ink outline). Text in these
colors is full-alpha. No color ever backgrounds a text row.

### 1.2 The ink-alpha ladder

Ink carries hierarchy through alpha, exactly as today's call sites do:

```
1.00  the fact                       0.45  flavor, margin notes (journal FAINT)
0.80  secondary fact                 0.40  hints
0.70  quoted speech, posture         0.35  disabled words
0.60  captions ("cash, drawn weekly:")0.25  divider rules
0.55  receipts under a name          0.22  underlines (trait rule)
0.50  desk-law footers               0.06  off-state pip fill
                                     0.03  chart wash
```

Pick from the ladder; do not invent 0.63.

### 1.3 Typography — one hand in the binder, six sizes with roles

Fonts: **Patrick Hand** (the writing hand) and **Baloo2 Bold** (the display
hand, `_font_d` / `DrawnUI.Display`). The display hand lives on garage-room
buttons, draft ceremonies and pip labels. **Inside the binder there is ONE
hand — the writing one.** Emphasis is size, UPPERCASE, or the pen — never a
font switch (`NAME.to_upper()` is the binder's bold).

The scale (sizes in use across `binder.gd`/`BinderScreen.cs`, codified):

| role | px | use |
|---|---|---|
| HERO | 46 | the one number the desk is about ("$12,400 in the bank", "v0.6", "31 customers · 4 logos") |
| TITLE | 38 | the desk's name-line ("the ledger — money, debt, and the taxman") |
| ROW | 30 | primary row text; the default `_label`/`L()` size |
| STATUS | 27 | verdicts, states, inline warnings ("pricey — 60% of fair demand") |
| DETAIL | 23 | receipts, captions, effect lines, tab labels |
| LAW | 21 | the desk-rules footer, assumption notes (ink 0.5) |

A size may flex one step (±2–3px) to fit a measured line — the lane specs'
named sizes (19/22/24/26/28/32/36/40) are all flexes of these six bands —
but **bands never skip**: a receipt is never set at ROW, a verdict never at
LAW. Fixed outliers, untouchable: journal TITLE 64 / BODY 34 (JournalPage
owns them — "two sizes, there is no third"), tab strip 23, close `×` 46.

Money is always `_fmt` / `GameUi.Money` (thousands commas), sign outside the
`$` (`-$300`, never `$-300`), `GameUi.Cash` in Unity. Compact form
(`1.2M/8k`) only inside chart corners.

Facts on one line join with the middle dot ` · ` (`TokenLine` idiom); a
caption ending in `:` takes a space, not a dot. **Maximum 4 segments per
dot-chain**; a fifth fact is a second line or a "+N more".

### 1.4 Space & rhythm

- Stage 1536×1024 · clipboard 1240×920 at (148,52) · **content pane 1160×760
  at (40,118)**. Ten tabs, pitch 120 (spine ruling). All coordinates are
  Godot top-left; Unity transcribes 1:1 via `DrawnUI.Rect`.
- **The cursor idiom**: a running `y`, advanced by MEASURED heights —
  `_wrap_h(text, size, w)` / `BinderScreen.Height()` — never fixed steps for
  wrapping text (the street stacked on itself the week a thesis wrapped).
- Row pitches in use: text line 34 (at DETAIL–STATUS) · stepper/control row
  46–64 · card row 62–118 by density. Between groups ≥ 12px; a new block
  after a divider gets ≥ 16px.
- **Column grammar of a desk row**: identity at x10 → state/value at
  x430–520 → live effect at x640–688 → controls at x1000/x1064 (52×46).
  Eyes travel left→right from *what it is* to *what you can do*.
- Lists cap at 6 + `"+N more …"` line. **The binder never scrolls. No
  modals.** A desk that outgrows its pane becomes a state machine (§4.3),
  not a scroll view.
- Dividers: 2px ink@0.25 full-width rules (`DrawnUI.Fill`), or the framed
  strip (§2.5 of 09) when a band must read as one object.

### 1.5 Paper physics

Every card is cut by the same hand. The knobs (already in `PaperStyle` /
`DraftPaper`) and when to use each:

| style | shadow | edge | use |
|---|---|---|---|
| Button (`PaperStyle.Button`) | (4,5) @0.35 | inset 2 · 10 steps/edge · jitter 1.6 · 3.5px · seed 12 | pressables |
| Sheet (`PaperStyle.Sheet`) | (8,12) @0.30 | inset 3 · 16 steps · jitter 2 · 4px | big sheets |
| Draft card (`GameUi.DraftPaper`) | (7,9) @0.18 | inset t/2+3 · 13 steps · jitter 2.1 · 4px · seed 17+lean | cards in a grid |
| Clipboard | (8,12) @0.25 | binder's own | the binder |

- **Seeded wobble**: same card = same wobble every frame and every session.
  Cards in a grid vary by `lean = int(x) % 5` folded into the seed, so
  neighbors are visibly cut by the same scissors but not cloned.
- Lean: big sheets may carry a hair of rotation (journal −0.012 rad applied
  over −0.069 drawn); desk cards inside the binder stay square — the binder
  is the one tidy object the founder owns.
- Inline mini-boxes (pips, chips) use the light edge: inset 1 · 4–6 steps ·
  jitter 0.35–1.0 · 1.4–2.5px — `GameUi.StatPips`' recipe. **Every box is
  inked, on and off alike**: "a bare coral square is a UI element; a bordered
  one is a box somebody filled in."
- No gradients. No glows in the binder (Runway.Effects motes/glows belong to
  the room and ceremonies, never to desks). No corner radii except the
  garage-room `_style_button` (radius 14 + 4px ink border + hover color-fill)
  — that idiom stays in the ROOM; binder buttons are ink words and paper
  cards.

### 1.6 The motion law

Motion exists to make paper feel alive and acts feel weighty. Everything else
holds still.

1. **The breath — 12fps, quantized.** Any pulse (scale 1±0.03 on the open
   button, alarm alphas, vignette) reads
   `t = floor(now * 12)/12` (`BREATH_FPS`) so Godot/Unity swallow identical
   frames. Nothing pulses smoothly; smooth is chrome.
2. **The boil — 8fps, 3 frames, ≤1.5px.** All baked ink (edges, rules,
   rings, chart lines) boils via `DrawnBoil.Apply/Sweep` with per-instance
   phase. **Never text. Never a Button's edge** — a pressable holding still
   IS the affordance. (Godot edges redraw per-frame with a fixed seed; the
   boil is the Unity twin of "alive but calm".)
3. **Ink-settle is the JOURNAL's law**, not the binder's: pages write in
   behind a travelling nib (80 cps body, 34 cps title, 3.6s budget, one
   click skips — `journal_page.gd`). The binder is a reference object,
   already written: it fades UP once (0.2s) and every tab/state change
   inside it is **instant**.
4. **The signature beat.** Any press that changes the books (§2.9's list)
   answers with the commit stroke: a coral `Rule` draws under the pressed
   words in 0.14s, holds 0.10s, then the act fires (`_commit_week` idiom).
   The most consequential click in the game must never feel like a menu.
5. **Eases**: rises are cubic-out (`EaseOutCubic`); fades linear; the
   curtain closes 0.45s / opens 0.55s. Nothing bounces, springs or
   overshoots.
6. **Value flashes**: a number that changed while you watch (money label)
   tints SAGE (up) or CORAL (down) and tweens back to ink — once, ~0.6s.
   Desks rebuilt on `_refresh` do not flash; the room's HUD does.

### 1.7 What never happens

No spinners. No progress percentages (a pen stroke advances with real
progress). No tooltips or hover popovers — the teaching is printed on the
row (§3.2). No drop-down menus. No checkbox/radio chrome — choices are pen
rings and written words. No scrollbars in the binder. No red/green other
than CORAL/SAGE. No pure white (#fff is only inside the rasterizer, tinted
by `Graphic.color`).

---

## 2. THE COMPONENT LIBRARY

Each component: purpose · anatomy · states · interaction · motion · engines.
Coordinates are pane-local examples; the lane specs' own numbers rule
placement. "Ink button" = `_ink_btn`/`GameUi.InkWord` (flat word, coral
hover). "Paper card" = §1.5.

### 2.1 The world-clamped stepper

**Purpose**: set a number the world allows, one deliberate notch at a time.
The game's only "slider" — where a spec says slider, build THIS (drag has no
pen; ladders are the house physics).

```
MARKETING                                $2,000/wk    reach ×1.92        −   +
the funnel mix — saturates past ~$2k                  — saturates ~$2k
└ identity (ROW/DETAIL)                  └ pen value  └ live effect   └ 52×46 @ x1000/1064
```

- **Anatomy**: name (ROW, ink) + one-line why (DETAIL, ink 0.6) · current
  value (STATUS–ROW, **coral — the founder's pen**) · live-effect string
  from the engine's own formula (DETAIL, ink 0.75) · `−` `+` ink buttons
  40px glyphs in 52×46 targets.
- **The ladder**: every stepper walks a named value ladder
  (`LEVER_STEPS`, price fair-multiples, borrow 1k…100k, salary, produce,
  term list) — never raw ±1 unless the unit is literally one (build target
  steps 1, hold repeats 5). The engine re-clamps on write; the UI is never
  trusted.
- **States**: `default` as above · `hover` glyph turns coral · `at-bound`
  the press does nothing AND the reason is printed — the bound value line
  gains ` (era cap)` or the effect column carries the engine's refusal
  ("no revenue, no line"); the dead glyph dims to ink 0.35 · `zero` the
  effect line states the honest zero ("founder sells alone", "instant
  coffee, cold room") · `disabled` whole row at ink 0.35 with the reason
  where the effect was.
- **Teaching line**: mandatory. A stepper with no live-effect string does
  not ship — mechanics visible at the point of decision is house law.
- **Interaction**: click only; every press writes state and `_refresh()`es
  the tab (instant rebuild). No hold-to-repeat except unit-stepping
  (build target).
- **Motion**: none. Steppers are adjustments, not commitments — no
  signature beat.
- **Engines**: Godot `_ink_btn(Button)` + ladder func (`_lever_step`,
  `_price_step` patterns); Unity `GameUi.InkWord` + static ladder
  (`BinderScreen.Step`). Effect strings live beside the formulas
  (`_lever_effect`/`LeverEffect`) so desk and engine can never disagree.

### 2.2 The expandable ledger row (the ▸ affordance)

**Purpose**: a list that scans whole, with one item's fine print on demand —
the accordion that keeps a desk on one sheet.

```
COLLAPSED (62px)
POCKET SYNTH  ·  per unit                      $18 · margin $9/unit · about fair   ▸    −   +
serve ≈ $9 (×0.89 learned) · fixed $40/wk · margin $9/unit
                                              └ status (STATUS)              └(936) └(1000/1064)
EXPANDED = the row REPLACES the list with a full-pane DETAIL card:
◂ all offers                                                    drop this offer ×
POCKET SYNTH · per unit
the street charges ≈ $20 · a sale costs ≈ $9 to serve · fixed $40/wk
PRICE   $18 per unit                                                  −   +
...fine print: variable lines · fixed lines · break-even...
```

- **Anatomy (collapsed)**: name-line (ROW) · receipts line under it
  (DETAIL, ink 0.55) · status column at x430 · `▸` ink button (52×46, x936)
  · any inline steppers keep x1000/1064.
- **The rule of one**: only one row expands, and expansion REPLACES the
  list (a full-pane DETAIL state) rather than pushing rows down — single-
  expand accordion is a spine ruling; DETAIL always opens with `◂ all …`
  at (10,6) so the way back is the first thing readable.
- **States**: collapsed · detail · detail-with-armed-drop (§2.9). The `▸`
  glyph is the only expand affordance — never "click the row" (rows carry
  other targets).
- **Motion**: instant swap (binder law). The `▸` hovers coral.
- **Engines**: a desk-local `_mode`/`_detail_idx` (NOT saved — desk state
  dies with the node); Godot rebuilds in `_refresh()`, Unity in the tab
  method. `▸` is U+25B8, in `DrawnUI.GlyphSet`'s family (verify glyph
  coverage; add to `GlyphSet` if missing — boxes are a shipped bug).

### 2.3 The review card — a proposal awaiting the founder's pen
*(the most important new component — 01 §7.5 is the canonical instance)*

**Purpose**: the world (LLM or engine) hands the founder a filled-in paper
form; the founder adjusts the adjustable lines and signs it into the books —
or tears it up. **Nothing an LLM wrote ever enters state unreviewed.**

```
the street's terms — adjust the lines, then shelve it            ← banner, CORAL, STATUS
──────────────────────────────────────────────────────────
MEAL-PREP BOX · per box                                          ← the read (printed facts)
the street would charge ≈ $24 · elasticity steep · weight 1.0
arrives unpriced — it bills at the going rate ≈ $24 until you price it

what one sale costs — variable                                   ← the adjustable lines
  ingredients                              $8        −   +
  packing + delivery                       $5        −   +
  = variable cost $13/unit · served at ×1.00 today               ← sum line, BLUE
standing costs — every week, sold or not
  kitchen rental                           $90/wk    −   +
  = $90/wk · break-even: 8 sales/wk pay for it                   ← BLUE (CORAL when it never pays)

the street shrugged — house numbers                              ← provenance note (keyless only)

put it on the shelf            tear it up                        ← confirm / cancel
```

- **Anatomy**: (1) coral banner naming the provenance and the ask; (2) the
  READ — facts the founder does not get to edit (the market's shape:
  name, unit, fair price, elasticity), printed in ink; (3) the LINES —
  every adjustable number on a world-clamped stepper (§2.1) with BLUE sum
  lines doing the arithmetic out loud; (4) the verdict/lesson line when one
  applies ("this price never pays for itself — every sale loses $4",
  CORAL); (5) confirm + cancel as two ink buttons, confirm first, cancel
  never coral (cancel is safe, not scary).
- **The paper feel**: the review renders on the same pane, same cursor,
  same hand as DETAIL — it IS a desk sheet, not a dialog. No scrim, no
  floating card. What marks it as *pending* is the coral banner and the
  absence of a price row.
- **States**: `review` (adjust+confirm) · `review-keyless` (adds the one
  ink-0.5 provenance note) · `refused` (the engine declined on confirm —
  a coral line explains, both buttons stay) · after confirm → the list,
  with the new row present and its bang lit if it arrived unpriced.
- **Interaction**: steppers adjust only the lines; confirm calls the
  clamped engine op (`add_offer(...)`), logs a receipt
  (`log_action("NEW OFFER shelved: …")`) and returns to LIST; cancel
  returns with the proposal discarded. Esc = cancel. Closing the binder
  discards (desk-local state dies with the node) — **no offer ever appears
  unreviewed**.
- **Motion**: the card lands instantly; **confirm carries the signature
  beat** (§1.6.4) — the stroke draws under "put it on the shelf", then the
  books change. Cancel is instant.
- **Engines**: reuse the DETAIL renderer with the three review deltas (01
  §7.5). Deterministic siblings (no WAIT, engine-authored terms) use the
  same anatomy: the bank's SIGN THE NOTE block is a review card whose READ
  is the quote line and whose LINES are amount+term.

### 2.4 The card grid (applicants, bets, term sheets)

**Purpose**: a small set of peers to weigh against each other — people,
bets, offers. One anatomy, three densities.

```
ONE CARD (applicant, 66px row-card)
Mara Voss · ●●●●○ · asks $1,700/wk (market $1,200)              hire    pass
"negotiates via long silences" · waiting 2wk
└ name ROW · pips · ask+anchor        └ flavor DETAIL ink0.45   └ ink buttons x1000/1064

ONE CARD (bet, 118px board-card)
COLD-START FIX · retention, ambition 2
kills the empty first day for new signups                        point the team →
6 R&D-wks · LAUNCH RISK: clean ship ~65% (DC 9 vs build)
```

- **Anatomy**: line 1 = NAME (ROW) + the deciding numbers with their
  anchors (`asks $1,700` **always beside** `market $1,200` — a number
  without its anchor is not a decision); line 2 = flavor/why in the world's
  voice (DETAIL, ink 0.45–0.65, quoted when it is speech); line 3 (dense
  cards) = cost + odds; actions right, at the stepper columns.
- **Pips** (`●●●●○` or the inked-box pips of `GameUi.StatPips`): skill and
  ratings render as five marks, filled coral with ink edges — never a bare
  number for a 1–5 scale.
- **Variants**: row-card 62–72px (applicants, notes, machines) · board-card
  90–118px (bets: adds a description line and a right-side state block) ·
  journal chip (term sheets/M&A: `icon_row` cards inside the journal, pen-
  ring select — the journal's own idiom, not rebuilt in the binder).
- **States**: default · hover (action words coral) · `cap-full` (action at
  ink 0.35 + the reason printed in its place or beside) · `armed` (§2.9,
  destructive actions only) · `committed` (bets: button replaced by the
  progress vessel + ETA) · `expiring` (append the clock in coral:
  `— dies in 1 wk`, `waiting 2wk`) · `spent` (journal only: PenCross —
  spent cards never render in the binder; the binder shows what IS).
- **Grid math**: max 6 cards then `"+N more wait behind these"` — the cap
  is a spine law; order = the engine's order (never re-sort on the desk;
  the desk is a window, not an opinion).
- **Motion**: none beyond hover; a card arriving with this week's tick is
  simply present next open (the journal announced it).
- **Engines**: cursor rows of `_label`+`_ink_btn` / `L()`+`InkWord`; pips
  via the `StatPips`/`TraitPips` recipe scaled to 13–17px boxes; Godot
  draws dots `●●●●○` as text only if the font carries them — prefer the
  inked-box pips both engines already own.

### 2.5 The stage board (pipeline columns with chips)

**Purpose**: named things moving through named gates — the enterprise
pipeline (05), and the pattern for any future staged flow.

```
MEETING              PILOT                PROCUREMENT          CONTRACT
──────────           ──────────           ──────────           ──────────
Vanta Systems        Meridian Logistics                        Quill Health
6 seats · warm · wk1 40 seats · hot · wk3                      12 seats · hot · wk6
Ashby & Sons
9 seats · cold · wk5
— dies in 1 wk
```

- **Anatomy**: column headers (STATUS, 26px) over pen-ruled columns —
  **rules, not boxes**: a 2px ink@0.25 vertical `Rule` between columns, or
  headers alone when the chips make the columns obvious; chips are 2-line
  entries at 64px pitch (name ROW ink · facts DETAIL with the heat word
  colored on the ramp §1.1); optional 3rd flavor line (18px ink 0.45) only
  while a column holds ≤3.
- **States**: chip normal · expiring (coral clock line) · column empty
  (the column header stays — an empty stage is information) · board empty
  (§2.11).
- **Interaction**: **none — and that is the design.** The pipeline is
  pushed by written moves in the journal ("fly out to Meridian"), not by
  dragging chips. The board is the founder's wall calendar; hands move
  deals in the story. (`no controls` is 05's own ruling; boards for future
  lanes inherit it unless their spec says otherwise.)
- **Motion**: none. Chips do not slide between columns; the week moved
  them and the journal said why.
- **Engines**: fixed column x-slots (3×386 / 4×290), chips laid by cursor
  per column; measured wrap for names; heat word via a 3-color pick.

### 2.6 The action log (receipt lines with age)

**Purpose**: a rap sheet — what an actor did, when, so patterns become
readable (rivals; also the outstanding-notes list and any history strip).

```
VANTAGE — looks unstoppable
flush · undercutting · fights on price · loud
plays: undercut, poach, ship
wk14: cut prices ~8% · wk15: quiet · wk16: poach attempt
└ trail: last 3, oldest first, joined " · ", DETAIL ink 0.7
```

- **Anatomy**: identity line (ROW) · posture line in **word-maps, never raw
  floats** ("flush / steady / tight / bleeding") · the trail: last 3 log
  entries, each stamped with its week (`wk14:`), joined ` · `, oldest →
  newest so the line reads as time does.
- **Age**: the week stamp IS the age; entries older than the window simply
  fall off the line. No relative times ("2 wks ago") in logs — the game's
  clock is week numbers.
- **States**: quiet week → the entry is the word `quiet` (silence is data)
  · a BIG beat renders in the week's journal with ⚡, never louder here —
  the log is memory, not alarm (the bang system carries urgency).
- **Engines**: one wrapped label per rival block, cursor-advanced by
  measured height; the log array is engine state (`rival.log`), the desk
  prints its tail verbatim.

### 2.7 The teaching footer

**Purpose**: the desk states its own laws — the pedagogy line naming real
concepts, in the game's dry voice.

```
y≈700–740, full width, LAW 20–21px:
BLUE  when it computes from the run's own numbers:
      "win rate 4/11 (36%) · avg cycle 7 wks · cost per signed seat ≈ $310"
INK@0.5 when it states the standing rules:
      "the rules of this desk: the MARKET RATE is what the street pays for the
       role · a head costs more than a salary (fully-loaded) · SEVERANCE is
       tenure-banded, and grows up with the company"
```

- **Placement**: the last thing on the sheet — reading order ends on the
  lesson. One footer per desk (computed stats line may sit above the rules
  line where a spec grants both).
- **Tone**: lowercase declaratives; REAL TERMS in caps at first use (COGS,
  MARKET RATE, CAC); middle dots; no exclamation marks; the wry beat is
  allowed one clause ("equity: forever").
- **Yield rule**: warnings outrank wisdom — when the pane's warning slot
  fires, the rules line yields (04/06 idiom: rules show only when no
  warning does). The computed-stats footer never yields (it is content).
- **Engines**: one `_label`/`L()` at w≈1100, measured wrap.

### 2.8 The bang system v2 (attention without screaming)

**Purpose**: route the founder's eye through severities — one registry
(spine §4 `attention_items`), four surfaces, strict hierarchy so ten live
items never read as ten alarms.

```
severity 1  note   ink "!"      tab only
severity 2  warn   coral "!"    tab + ticker eligible + threats row
severity 3  alarm  coral "!" pulsing on the 12fps breath (alpha 0.7↔1.0)
                              tab + ticker + threats, top of list
```

- **The tab bang**: one `!` per tab, 30px, hung on the tab label's
  top-right shoulder (today's offset tab pos + (103, −12) at pitch 133;
  re-derive as `tab_w − 27, −12` when the 10-tab pitch-120 row lands — the
  bang and the pen ring move WITH the tab row, the twice-shipped desync
  bug). It wears the tab's **highest** severity — never a count, never
  stacked marks.
- **The chip badge**: the garage binder chip carries one coral `!`
  (cream-outlined) when ANY item exists — the room-level "open the binder"
  light. It never says which; the ticker does.
- **The ticker**: one hand-font line under the binder button — the single
  top item by (severity desc, registry order), engine label verbatim,
  ≤40 chars, severity ≥2 only. The ticker is pedagogy: it names the problem
  in business terms ("a note payment is due you cannot cover").
- **In-tab attention rows**: inside the owning desk, the item's subject
  carries an inline coral mark at STATUS size — `! wants $1,600 (market)`
  on the roster row, `! billing at the going rate $24` on the offer row.
  Cap 2 inline marks per pane (§1.1 coral budget); the overflow lives as
  rows on `threats` (sev ≥2, cap 12 + "+N more", sev 3 first).
- **Acknowledgeable notes** (sev 1–2 milestones: `first_tax`,
  `broke_even`): clear on view — visiting the desk IS the ack.
- **The pulse discipline**: at most one element on screen pulses. If two
  sev-3 items are live, both tabs wear steady coral bangs and only the
  ticker line pulses. (Pulse = alpha breath at 12fps, never scale, never
  color cycling.)
- **Engines**: consumers read `attention_items(state)` only — no desk
  hardcodes a predicate (spine kills the old OR-chains). Bang labels are
  `HandLabel`/`Label` "!" — text never boils, so the pulse is the alpha
  tween quantized to `BREATH_FPS`.

### 2.9 The two-tap arm (the confirmation pattern)

**Purpose**: irreversible or expensive acts get a visible cost and a second
chance — without a dialog box (no modals, house law).

```
tap 1 (arm)                          tap 2 (fire)
let go            →   owe $3,000 severance — sure?   →  the deed + receipt
drop this offer × →   sure? it disappears ×          →  remove_offer
[ SELL $2.4M ]    →   SELL — tap again               →  the run ends
```

- **The arm**: first press re-renders the SAME control, re-captioned in
  **coral**, carrying **the price or the consequence** in the label —
  the invoice before the deed ("owe $3,000 severance — sure?"). Nothing
  else on the sheet changes; no dim, no overlay.
- **The fire**: second press within the same visit executes, logs the
  receipt, and (for run-enders) plays its ceremony. Signature-beat rule:
  acts that change the books get the commit stroke (§1.6.4) on the second
  tap.
- **Disarm**: anything that rebuilds or leaves the state disarms — switch
  tab, expand another row, Esc, close the binder, or press any OTHER
  control. Armed state is a desk-local bool (`_drop_armed`, `_mna_armed`),
  reset each build, never saved.
- **What arms**: fire someone · drop an offer · sell the company · ring
  the bell · stand down a committed bet? — NO: stand-down is reversible
  (recommit next week), one tap. The test: **arm iff the act destroys
  something a later week cannot rebuild** (a person, an offer's history,
  the run) **or books an immediate real cost** (severance).
- **What never arms**: steppers, hires (reversible at a price — the price
  is quoted on the fire side as severance), repay, toggles, navigation.
  One arm per pane at a time — arming a second control disarms the first.
- **Engines**: label swap + bool; Godot rebuilds the button, Unity
  re-captions the TMP label. The armed caption ends `— sure?` or
  `— tap again` (two house phrasings; pick by voice: money uses the
  invoice + `sure?`, run-enders use the imperative + `tap again`).

### 2.10 Charts — the drawn instruments

Six instruments exist; new needs map to one of them. **Everything is a pen:
seeded wobble, ink outline, tinted fill at 0.5–0.75α, and its number written
beside it.** A chart without its number is decoration; decoration does not
ship.

| instrument | is | new uses |
|---|---|---|
| **spark** (`_Spark`/`DrawnChart.MountSpark`) | wobbled polyline, coral last-dot, hi/lo corner numbers @20px ink 0.45, 0.03 wash, "not enough weeks on record yet" under 2 points | net weekly (SAGE) · revenue (BLUE) at 540×64; full-width 1120×130–190 for the tab's one big series |
| **vessel** (`_DebtJar`, `_BetBar`) | inked jar/bar with a level fill | bet progress (SAGE fill + `62% · ships in ~3 wks`) · debt jar shrunk (64×84) |
| **bars** (`_FunnelBars`, new — build from Fill + ink edge) | horizontal pen-stroke bars, w = 40 + 460×v/max, fills BLUE/YELL/SAGE 0.5α under seeded ink outline, label + value ON the bar row | the funnel (REACH/LEADS/SIGNED) · any small comparison |
| **pie/donut** (`CapPie`/`Donut`) | slices at 0.75α under a 4px ink rim, labels hung round the wheel at the arc's middle — never a side legend | cap table gains the YELL pool slice |
| **pen ring / ellipse** (`PenEllipse`, `PenRing`) | the coral loop that marks a choice | active tab · journal selections — never a highlight box |
| **clock** (`DrawnChart.Clock`) | drawn face + two ink hands | any deadline line's leading mark |

Rulings: **no dials, no gauges** — "utilization 79%" is words on the status
line (09's own choice); radial means share-of-whole and nothing else. Bar
maxima are the visible set's max, not all-time. Sparks keep their hi/lo
corner numbers ("a spark without them says 'it moved' and never how far").
Charts boil (they are ink); their labels do not (text never boils).

### 2.11 Empty states — a desk with nothing still teaches

**Anatomy**: fact (ROW–STATUS, ink 0.6–0.7) + the tell or the mechanism that
fills it (DETAIL, ink 0.5, or coral when it is a pointer to an action).
Never blank space, never "No data". The voice: lowercase, declarative, two
beats — the fact, then the wry tell — and the invitation names the
MECHANISM, not a button.

House exemplars (shipped): `nothing ticking. that never lasts.` ·
`none yet. every point of the company is still on this table.` ·
`not enough weeks on record yet` · `the world hasn't defined your offers
yet — they arrive with the bible.`

Three new ones, at the bar (copy is normative, reuse verbatim):

```
crew · hiring, no roles open:
   nobody is hiring. open a role and the street starts sending people —
   the advert against the MARKET RATE decides how many.

customers · enterprise, empty board:
   the calendar is empty. enterprise deals start as meetings —
   write a move that gets you one.

the street · no rival action yet:
   the street is quiet this week. it is watching what you charge.
```

### 2.12 Waiting & keyless states (how waiting looks in a paper world)

- **The WAIT state** (a desk waiting on an LLM, e.g. `the street is
  pricing it…`): one breathing line — STATUS size, ink 0.6, alpha
  0.45↔0.75 sine on the 12fps clock — plus a `cancel` ink button. That is
  the whole state. No spinner, no dots animation, no progress bar (the
  loading screen's pen-stroke progress is for the between-weeks beat with
  real reported progress; a desk wait has none).
- **The phrasing**: the world is doing something, present tense, ellipsis:
  `the street is pricing it…` · `the world considers your move...` ·
  `the dice are out...`. The subject is always the fiction (the street,
  the world, the dice) — never "loading", "thinking", or the vendor.
- **Cancel is real**: leaving WAIT (cancel, Esc, closing the binder) drops
  the reply on arrival — callbacks guard `is_instance_valid(self)` /
  destroyed + `_mode == "wait"` before touching anything.
- **Keyless/seeded**: skips WAIT entirely — instant REVIEW with house
  numbers and the one provenance note `the street shrugged — house
  numbers` (ink 0.5). Keyless is never a degraded screen; it is the same
  desk with a dry footnote.
- **Never blocked**: the binder always opens, every tab always renders
  from state. Only a desk's own WAIT sub-state and the journal's
  adjudication (behind the curtain, which breathes) ever wait on a model.

---

## 3. THE CLARITY CONTRACT (desks explain themselves)

1. **First appearance = name + why.** The first time a pane shows a number,
   the number's NAME rides with it and its one-line why sits in the same
   group: `unit cost $16.02 (base $18.00, learning curve −11%)`. Real
   vocabulary, capitalized at first use — COGS, CAC, MARKET RATE,
   CONTRIBUTION MARGIN — the desk uses the words a founder will meet in the
   world (01 §6's law, generalized to every desk).
2. **Units always attached.** `$`, `/wk`, `wks`, `%`, `×`, `seats`,
   `units`, `heads` — never a bare number. Money through `_fmt`/`Money`;
   rates as `4.0%/wk`; multipliers as `×1.92`.
3. **Color never carries alone.** Strip every color from the pane and the
   words still say it: `pricey`, `burning`, `cold`, `MISSED`, `▲/▼`, `✗`.
   Color seconds the word; the word is the signal.
4. **Anchors beside asks.** Any number that is a judgment call renders
   beside its reference: ask vs market, price vs street, covenant vs
   current, quote vs shark vs equity. A number without its anchor is
   trivia.
5. **The WHY receipt idiom.** Every number that moved this week can point
   to a receipt (journal `rep.lines`) whose clause names the mechanic:
   `carrying 34 units: −$12 (2%/wk of unit cost — money parked on
   shelves)`. Desk shows the state; receipt explains the consequence;
   **both use the same words** (09's law, generalized).
6. **Cognitive load caps**: a pane holds ≤5 groups · a group ≤7 numbers ·
   a line ≤4 dot-joined facts. Past a cap, group under a DETAIL-size
   caption (ink 0.55–0.6) or spill to "+N more". The interface map's
   worst-case pixel math per desk must exist in the spec before code.
7. **Reading order = layout order.** Top-left answers *what is this desk
   about* (identity + hero number); the middle is the working surface;
   controls live right (x1000/1064); the bottom teaches (§2.7). No pane
   requires reading upward.
8. **Word-maps over floats.** Internal scalars surface as words with the
   number in reach (`flush`, `warm`, `about fair`, fuzz(strength)) — raw
   engine floats never print (the fog of war and the fiction both demand
   it).
9. **Progressive depth is the desk growing, not options appearing**: era
   unlocks add whole labeled sections (the fine print at coworking, weight
   at office, mini P&L at floor). A locked capability is absent — not
   grayed — except where a bound must teach (dimmed buy-row with the
   engine's refusal: "the bank won't answer — clear the collectors
   first").
10. **Fog of war shows the fog.** Below the analytics gate the desk says
    what it cannot know and how to know it (`invest in analytics to see
    the funnel`) — an earned dashboard is a mechanic, never a missing
    feature. Your own actions (your meetings, your roster) are never
    fogged.

---

## 4. THE INTERACTION CONTRACT

1. **No dead ends.** Every sub-state renders its exit on-screen at build
   time: `◂ all offers`, `never mind`, `cancel`, `tear it up`, the close
   `×`. If a state has no visible way out, it does not ship.
2. **Esc pops before it closes.** Inside a desk state machine Esc walks
   DETAIL/WRITE/WAIT/REVIEW → LIST; only from a tab's base state does
   Esc/TAB/B close the binder. (One frame of key-deafness on open stays —
   the `_armed` idiom.) Enter submits write fields; Shift+Enter newlines
   (journal `_wire_free` / `PageBlocks.WriteField`).
3. **A state machine beats scrolling, always.** 1160×760 is the whole
   world; a desk that cannot fit becomes LIST→DETAIL, a pen-toggle of page
   modes (crew `roster / hiring`), or a capped list + threats spillover.
   Scrolling is vetoed (spine); if you are tempted, the pane is over its
   load cap (§3.6).
4. **Latency law**: state-render is always instant and local. Only these
   may wait on a model: a desk WAIT sub-state (cancellable, §2.12) and the
   journal's adjudication behind the curtain. LLM enrichment otherwise
   arrives with the tick (dressed entities) or through review cards.
   Every async callback guards liveness + mode before writing.
5. **Touch targets ≥ 44px** on the short side: steppers 52×46, tabs
   130(118)×44, ink buttons ≥ 160×44, `▸`/`×` 52×46. Word-buttons pad
   their hitbox to the minimum even when the word is short.
6. **Error forgiveness, in order of preference**: (a) reversible by
   another press (steppers, toggles, stand down, repay) → no confirm;
   (b) reversible at a quoted price (hire → severance later) → price
   printed before the act; (c) irreversible → two-tap arm (§2.9);
   (d) run-ending → two-tap arm + ceremony. Undo-as-history does not
   exist — the week is the undo boundary, and the journal records what
   you did.
7. **The engine is the bouncer.** Every control writes through a clamped
   op; a desk never pre-computes a forbidden state into being. Refusals
   come back as printed reasons in the desk's own voice, never as
   silently-dead buttons.
8. **One writer per desk visit**: desk-local pending state (`_pending`,
   `_drop_armed`, `_mode`) is never saved, never shared between tabs, and
   dies with the node — reopening the binder is always a clean read of
   state.
9. **Hover is a whisper**: words tint coral (`HoverTint`, paper buttons
   scale 1.045 max). No cursors changes beyond pointing-hand on live
   targets, no underlines-on-hover, no tooltip delays.

---

## 5. PER-DESK COMPOSITION SKETCHES (the gestalt, at the bar)

Placement/pixel budgets are 00 §11's; these sketches fix hierarchy,
grouping, whitespace, and where the teaching lives — what makes each desk a
drawn page rather than a spreadsheet. (Pane = 1160×760; `▁` marks the
teaching footer.)

### 5.1 pricing — the shopkeeper's shelf (01)

```
pricing — what Mossflow sells
POCKET SYNTH · per unit          $18 · margin $9/unit · about fair      ▸  −  +
  serve ≈ $9 (×0.89 learned) · fixed $40/wk · margin $9/unit
CALIBRATION KIT · per kit        ! billing at the going rate $60        ▸  −  +
  serve ≈ $22 · fixed $15/wk
unit economics: ≈ $14.0 ARPU − $6.1 COGS = $7.9 contribution … → ≈ $980/wk at 124
▁ COGS bills only when you sell · fixed bills either way · price − variable = margin
+ sell something new
```

Beautiful because: each offer is a price card written in one breath — name
big, verdict in the middle where the eye lands, fine print truly fine (ink
0.55). The whole shelf scans in four seconds; one `▸` and the pane becomes
a single offer's ledger with room to think. The blue summary is the
shopkeeper counting the till out loud.

### 5.2 the bank — a banker's letter (06)

```
the bank
quotes 4.0%/wk against your books (runway 22 wks · growth +6% · office era)
money costs/wk: bank 4.0% · shark 18% · equity: forever
borrow  $10,000   −  +        over  8 weeks   −  +
→ $1,486/wk · ≈ $11,888 all-in ($1,888 interest)
[ SIGN THE NOTE ]
bank note — $8,215 left                                      repay
  $1,486/wk · 4.0%/wk · 6 wks · missed 1
THE SHARK — $12,400 (18%/wk, it feeds first)
the next 4 weeks, as planned: $8.2k → $6.9k → $5.1k → $2.8k (before surprises)
▁ break-even: 34 customers (16 now) · tax accrues on profit from office up
```

Beautiful because: the quote reads as a sentence about YOU, reasons in the
parenthesis — a banker sizing you up, not a form. The preview line does the
amortization math before the pen touches paper, so SIGN THE NOTE (with its
signature stroke) feels like signing. Notes stack like filed letters; the
shark's line is one cold clause.

### 5.3 crew — people, not rows (02)

```
crew                                    ⟨ roster / hiring ⟩  ← pen toggle
Priya — engineer · $1,500/wk ($1,940 loaded) · sk ●●●●○ · burnout 32   +10%  let go
Nico — sales · $1,100/wk ($1,540 loaded) · sk ●●●○○ ·
  ! wants $1,600 (market)                                              +10%  let go
—— hiring ——
ENGINEER  advert $1,400/wk  −  + · market $1,200 · 3 waiting · ×
Mara Voss · ●●●●○ · asks $1,700/wk (market $1,200)                    hire   pass
  "negotiates via long silences" · waiting 2wk
payroll $6,200/wk · fully-loaded $8,050/wk            ╱╲╱─ morale
▁ the MARKET RATE is what the street pays · a head costs more than a salary …
```

Beautiful because: every person is a card with a voice — the quirk line in
quotes is the interview you remember. The two page-modes keep either half
calm; the decay clock (`waiting 2wk`) and the coral raise-ask make the desk
feel inhabited by people with options. Let-go quotes the invoice in its own
armed label — the price IS the confirmation.

### 5.4 the street — a beat reporter's notebook (03)

```
the street                          tailwinds — the street buys
FUNDING WINTER — checks shrink, terms bite · 6 wks left
VANTAGE — looks unstoppable
  flush · undercutting · fights on price · loud
  plays: undercut, poach, ship
  wk14: cut prices ~8% · wk15: quiet · wk16: poach attempt
NIMBUS — wobbling
  tight · premium · fights on product · quiet
  wk15: stumbled — the demo crashed · wk16: quiet
the money: Ashgrove Capital (momentum) · Bell & Weir (contrarian)
```

Beautiful because: rivals are read, not fought with buttons — four lines of
words build a character (posture as adjectives, the log as a rap sheet).
Silence prints as `quiet`, which is the most menacing word on the page. The
macro banner is weather across the top; winter is coral, and the whole desk
teaches pattern-reading as the skill.

### 5.5 customers — the funnel lit by analytics (04)

```
124 customers
╱─╲╱╱─ customers, weekly
the funnel, last week:              CAC by channel
REACH   1,240 ▓▓▓▓▓▓▓▓▓▓▓▓▓▓        ads        $82 · $2,000/wk
LEADS     96  ▓▓▓▓▓ (conv 7.7%)     content    $41 · $500/wk
SIGNED    11  ▓▓ (ceiling 14 hit)   referrals  — 
                                    outbound   burning
market, as you believe it: ~100,000 buyers (0.1% reached) …
of 100 who joined 12 wks ago, ~61 are still here · churn 4.1%/wk · care trims 12%
▁ the truth: ~82,000 buyers (you believed 100,000) · your cheapest customer comes from CONTENT
```

Beautiful because: the funnel is three pen strokes of shrinking length — the
SHAPE is the lesson before any number lands. CAC sits beside it like a
margin note, one coral word (`burning`) doing the alarm work. Fog of war
keeps the lower bands earned: the desk literally sharpens as the company
learns, which makes analytics feel like buying eyesight.

### 5.6 customers, Enterprise — the wall calendar (05)

```
31 customers · 4 logos signed
the pipeline — 5 live · 78 seats in motion · pool 12 waiting
MEETING              PILOT                 CONTRACT
Vanta Systems        Meridian Logistics    Quill Health
6 seats · warm · wk1 40 seats · hot · wk3  12 seats · hot · wk6
Ashby & Sons
9 seats · cold · wk5
— dies in 1 wk
logos: Quill Health (12) · Fernbay Group (9, renews in 3 wks)
▁ win rate 4/11 (36%) · avg cycle 7 wks · cost per signed seat ≈ $310 · a seat pays ≈ $400/wk
```

Beautiful because: named deals under written column heads read as the
founder's own wall — no chrome could add urgency the coral `— dies in 1 wk`
doesn't already carry. No buttons at all: the board makes you want to open
the journal and WRITE the move, which is exactly the game's loop. The blue
footer turns four jargon terms into this run's own numbers.

### 5.7 product — index cards on the board (07)

```
v0.6   ▯jar  debt 46 · outage ≈ 2%/wk · TECH-DEBT INTEREST: −9% velocity
the roadmap — one team, 3.2 R&D-wks/wk of capacity
COLD-START FIX · retention, ambition 2
  kills the empty first day for new signups              ▓▓▓▓▓▓░░ 62% · ships in ~2 wks
  6 R&D-wks · LAUNCH RISK: clean ship ~65% (DC 9)                    stand down
PLATFORM PLAY · platform, ambition 3
  open the API and let others build                       point the team →
  11 R&D-wks · LAUNCH RISK: clean ship ~40% (DC 13)
── standing ──
HARDENING SPRINT · pay down the codebase                  point the team →
▁ OPPORTUNITY COST: rnd money builds the committed bet — uncommitted, it polishes …
```

Beautiful because: bets are index cards pinned in a column — name lettered,
pitch in the world's voice, odds printed like the DM saying them across the
table. The one committed card grows a sage vessel: visible progress with no
animation at all. READY week turns its right side coral, a held breath
before the dice.

### 5.8 product, Hardware — THE BENCH (09)

```
┌ THE BENCH — building: Pocket Synth ($16.02/unit) ────────────────────┐
│ stock: 34 units · capacity: 24/wk · utilization: 79% · demand ≈ 19/wk │
│ build:  − 19 +  AUTO      unit cost $16.02 (base $18.00, learning −11%)│
│ machines: Assembly Jig ×2 (+12, $30/wk) · Pick-and-Place (+18, $60/wk) │
│ buy: [ Reflow Oven Line  $12,000  +45/wk  $180/wk upkeep ]             │
│ make vs buy — overflow to a contract mfr at 1.6×: ON · fill 100%       │
│ carrying cost: $12/wk on 34 units (2%/wk of unit cost)                 │
└────────────────────────────────────────────────────────────────────────┘
```

Beautiful because: the frame — a wobbled ink rectangle — makes the factory
one object clipped to the bottom of the product sheet, a shop traveler card.
Six dense rows work because every number wears its name and its cause in
the same clause; the strip is a working vocabulary lesson, not a dashboard.
On non-Hardware runs it simply isn't there.

### 5.9 cap table — the scorecard in your own hand (08)

```
     ⬤ pie: you 54% (PEN) · cofounders 18% (BLUE)      rounds:
        · option pool 10% (YELL) · investors 18% (SAGE) · pre-seed — closed
                                                        valuation $2,400,000
                                                        your slice today: $1,296,000
raise ~$240k now: pre-money $2.4M → post $2.64M — they'd ask ≈ 12% · your 54% → ≈ 47%
⚡ ON THE TABLE: Corvid Systems — $3.1M (1.3× standalone) · no-shop ends in 2 wk. The journal signs.
the board:
growth covenant: $1,800/wk by wk 34 — now $1,420/wk · 5 wks left
strikes ✗ · · · goodwill 2/3 · the room is warm
▁ a real board: covenants + the pool shuffle
```

Beautiful because: the pool slice makes dilution a drawn wound — you SEE the
shuffle carved from your side of the wheel. Strikes render as pen marks
(`✗ · ·`), governance as a scorecard the founder keeps on themself. The ⚡
banner burns quietly above the board block, and the page with no board at
all stays clean — the empty state IS the bootstrap flex.

---

## 6. THE QUALITY CHECKLIST (the review gate — all 15 must pass)

Binary, per implemented desk, both engines, before it ships:

1. **Paper only**: every surface is cream+ink with seeded wobble; no
   gradients, no web-box fills, no shadows beyond §1.5's four; no corner
   radii in the binder.
2. **One hand**: binder text is Patrick Hand only; emphasis via size /
   UPPER / pen; every glyph used is covered (no tofu boxes — check
   `GlyphSet`).
3. **Scale discipline**: every size maps to a §1.3 band ±one step; journal
   stays 64/34.
4. **Named numbers**: each number's first appearance carries its name and
   unit; anchors sit beside asks (§3.1–3.4).
5. **The why is present**: every value that moves has its effect line or
   receipt in the same words as the engine's formula.
6. **Color-blind pass**: read the pane in gray — every state is still in
   the words; heat/status words colored, never lines or fills.
7. **Coral budget**: ≤2 warning lines per pane, one bang per tab, ≤1
   pulsing element per screen.
8. **Measured wrap**: every wrapping label advances the cursor by measured
   height; worst-case pixel math for the pane exists and lands ≤760; lists
   cap 6 + "+N more"; nothing scrolls.
9. **Ladders + clamps**: every stepper walks a named ladder, writes
   through an engine clamp, and prints its reason at bound; disabled
   controls carry their reason.
10. **Exits everywhere**: every sub-state shows its way back; Esc pops
    state before closing the binder; write fields take Enter/Shift+Enter.
11. **Destruction is armed**: irreversible acts use the two-tap arm with
    the price in the armed label; book-changing confirms play the
    signature stroke; arms reset on rebuild.
12. **Latency honest**: no desk render waits on a model; WAIT states
    breathe, cancel cleanly, and guard their callbacks; keyless path is
    instant with the provenance note.
13. **Motion lawful**: pulses quantized to 12fps; boil at 8fps on ink only
    (never text/buttons); state changes instant; exactly one ceremony per
    consequential act.
14. **Empty states written**: every list/board/log renders its authored
    empty line (fact + tell + mechanism) — never blank, never "no data".
15. **Twin + shot proof**: coordinates and copy byte-match across
    binder.gd/BinderScreen.cs; one binder shot per changed tab passes in
    both engines' harnesses (ring alignment shot included when the tab row
    changes).
