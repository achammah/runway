# THE BINDER UX PLAN — every tab, one experience

The rework gave every desk deep INFORMATION design (the choose-by-letter
rounds). The first live-play round exposed what got less thought: the
EXPERIENCE — what you do on a page, what a red mark asks, what changed
since last week, how fast you move, what week 1 feels like. Evidence,
all owner-hit within one session: a hero that read hypothetical money as
real ("$23 in" at 0 customers), a red tab whose page named no ask, three
text-overflow classes, and no desk saying "this is the thing to do now."

This plan fixes the UX as a SYSTEM first (nine cross-cutting systems,
built once in the kit), then applies it to EVERY tab (19 desks + the
frame surfaces). Systems are the levers — veto at that level and the
per-tab entries adjust themselves.

## PART 1 — THE NINE SYSTEMS

**S1 · THE FIRST WEEK (zero-state law).** Week 1 is the first thing a
player ever sees, and today most desks show an empty warehouse. Every
desk designs its zero state as a TEACHING state: what this page WILL
show, framed honestly as promise not fact ("one customer's week WOULD
earn…" — the offers fix, generalized), plus the single action available
now, plus when the desk comes alive ("first candidates arrive when a
seat opens"). No desk may open on bare furniture.

**S2 · RED SPEAKS, AND FIXES IN ONE PRESS.** A red "!" must name its ask
ON the page (the spend fix, made law) — a one-line red strip under the
hero: the attention label + the verb ("adopt the book or fund a line").
And the jump gets teeth: `focus_desk` grows into `focus_control` — from
threats or the pre-roll review, one press lands on the desk WITH the
offending control spotlit (the coach's spotlight, reused). Red is a
doorbell today; it becomes a hand that walks you to the switch.

**S3 · THE DO LANE.** Every desk's live actions sit scattered in its
zones. Each desk declares 1–3 PRIMARY actions and they render in one
consistent slot (bottom-right of the pane, above the teaching foot) with
one grammar: `[ verb — object ]`. The eye learns ONE place to look on
every page for "what can I do here." Zone-level arms stay where context
needs them; the DO lane mirrors the primary ones.

**S4 · PRESS ANY NUMBER (the receipt popover).** The teaching-sim north
star as an interaction: hero numbers and double-ruled totals become
pressable — a small receipt popover shows the terms that made the number
("$23 ARPU = Σ price × share…" / "runway 7 = cash ÷ net burn"). The
engine already computes every term; the popover just says them. Scope:
heroes + totals + verdict words first, not every cell.

**S5 · WHAT CHANGED (the delta layer).** The binder is a WEEKLY
artifact; the story between two opens is the deltas. A thin pen layer:
▲/▼ beside hero numbers (vs last week), a small pen circle on any row
that moved since the binder was last opened, and each quartet card
carries its delta. One durable field (last-open week per desk) + the
metric history already kept. This is the single highest-leverage read
improvement: the page answers "what happened?" before "what is?"

**S6 · THE MEASURE SYSTEM (generated text law).** We patched overflow
per-desk; make it structural: every lane that renders GENERATED text
declares its measure (chars × size × lines) in ONE kit helper —
`fit_line` (one line, measured ellipsis) and `fit_par` (N lines, then
ellipsis) — and desks may not call raw labels with generated strings.
TMP/Godot differences die in the helper (that class of Unity-only wraps
can never recur).

**S7 · SPEED AND WAYFINDING.** Keyboard: 1–4 open groups, ←/→ walk
pages, Esc pops (already law), Enter arms the focused control. A BACK
STACK for cross-desk jumps: following a threat or a bill's "→ the bank"
leaves a small "← back to threats" pill by the rail; drilling a rung-3
site into its works leaves a breadcrumb ("Lyon ‹ the works"). Jumps stop
being one-way teleports.

**S8 · ERA DIMMING (never hiding).** Desks that are structurally dormant
early (the raise at garage, the works pre-launch, recruitment with no
seats) stay VISIBLE — the map is the curriculum — but their rail tabs
dim to 60% with a tiny era hint on the page ("wakes at coworking").
Challenged and rejected: hiding tabs until unlock — a teaching tool
shows the whole territory.

**S9 · ONE ARM FAMILY.** Three confirm grammars ship (two-tap arm,
sign-stroke, typed-PIVOT). Keep all three — they encode severity — but
style them as one family and SAY the tier on the control ("two-tap" /
"sign for it" / "type the word"), so the player learns the danger scale
by shape.

## PART 2 — EVERY SINGLE TAB

### The frame

- **The rail.** Weak: closed groups carry one aggregate; tabs are names
  only. Fix: each page tab gains a micro-status right-aligned (offers
  "$40 avg" · team "6" · the bank "$7.4k"), the group header keeps its
  total, and S8 dimming applies. Why: the rail becomes a readable
  dashboard before anything opens.
- **Group overviews (the quartet).** Weak: cards show heroes only. Fix:
  each card adds its S5 delta line and, when red, its S2 ask line; card
  press already navigates. Why: the overview becomes triage, not
  gallery.
- **THE PRE-ROLL REVIEW.** Weak: lists items, exits to fix are coarse.
  Fix: every item deep-links via S2 focus_control; the LOCK IN button
  wears the outstanding count as a badge all week. Why: the review is
  the game's safety net — it should walk you to the switch, not the
  hallway.
- **Momentary tabs (THE OFFER).** Weak: urgency reads once. Fix: the
  gold tab's clock chip ticks weekly and the desk's ACCEPT lane shows
  the S9 tier explicitly; NEGOTIATE shows "counter spent" state plainly.
  Why: a dying offer should feel like a countdown, not a page.
- **Arrange mode.** Weak: reachable only from the works; rename needs
  the keyboard. Fix: an [arrange] word on every division-aware desk
  (team rung 3, what-we-make lineup), and rename gains typed input
  (paper_input exists). Why: the write view should live where the
  read view raised the question.
- **The tour/coach.** Already refreshed to v3; add one chip pointing at
  the DO lane once S3 lands ("every page keeps its actions HERE").

### REVENUE (sage)

- **offers — THE RATE CARD.** Weak: verdict words are inert; the zero
  state was just fixed but the "set a price" moment is still quiet. Fix:
  press a verdict word → S4 popover showing the street math and the
  fair band drawn; DO lane = [set price — offer] [add an offer]; S5
  circles on rows whose demand verdict changed. Why: pricing is the
  game's first real decision — the desk should teach the band, not just
  grade the guess.
- **customers — THE SCOREBOARD.** Weak: fog says "invest in analytics"
  without a door; all read-only. Fix: the fog line becomes a jump to the
  spend line that buys analytics (S2 grammar); kept% pressable → cohort
  receipt (S4); S5 deltas on won/lost. Why: every "?" should sell its
  own unlock.
- **in motion — THE TRIPTYCH.** Weak: the SMB rank-1 "this week's move"
  and Enterprise pushes still require the player to go write the move
  themselves. Fix: THE PREFILL — press [push — Café Verde] and the
  journal draft receives the written move ("push Café Verde: …", player
  edits freely); rank-1 wears it in the DO lane. Consumer: press a
  source bar → growth opens focused on that plot (S7 back pill). Why:
  the binder becomes a cockpit that DRAFTS decisions, not a report you
  leave to act.
- **growth — THE MARKET GARDEN.** Weak: verdict chips assert ("near the
  knee") without showing the curve; the era cap is a line, not a
  feeling. Fix: press a verdict chip → S4 popover with the saturation
  curve drawn and your spend dotted on it; the cap meter pulses when a
  stepper press is refused; DO lane = the four steppers' summary +
  [balance the mix — suggest] which writes a SUGGESTION the player
  adopts per the spend-book pattern (never auto-applied). Why: the four
  characters are the game's best economics lesson — the curves should
  be touchable.

### COSTS (coral)

- **spend — THE ORG LEDGER.** Weak: smallest gap now (SUGGEST/ADOPT +
  ask strip shipped). Fix: bucket subtotal press → S4 effect receipt
  ("$300 closing → +9% close rate, next $100 buys +2.1%"); S5 circles
  on adopted/stopped lines. Why: the marginal number is what teaches
  diminishing returns.
- **team — THE PAYROLL LEDGER.** Weak: asks are answerable but morale
  motion is invisible; vesting bars are inert. Fix: S5 morale ▲/▼ on
  the hero; vesting bar press → cap-table pool (S7 back pill); DO lane
  = [answer ask — Ravi] when one exists, else [open a seat →
  recruitment]. Why: the desk's job is "who's asking" — the ask should
  be the loudest object on it.
- **recruitment — THE OFFER DESK.** Weak: garage zero state is thin;
  odds move invisibly while composing. Fix: S1 zero state ("no seats
  open — a seat is a weekly wage + a promise"; [open a seat] as the one
  action); the odds ticket animates on stepper press (number ticks, the
  marginal line updates — it computes already, make it FELT). Why: comp
  design is a dial-turning pleasure; let the dial feel live.
- **bills — THE BILLS LEDGER.** Weak: pure reading; trend column
  whispers. Fix: every row press jumps to its SOURCE (rent → the works'
  roof, interest → that note in the bank, serving → the works ticket)
  with S7 back pills; the memo line ("the floor eats 1.9× revenue")
  gains S5 week-over-week movement. Why: bills are consequences — the
  desk should teach where each one is edited.
- **the bank — THE MEETING.** Weak: zones 3/4 fold-swap works but the
  receipt doesn't live-update visibly while stepping; credit-lock
  explains itself only in prose. Fix: the RECEIPT re-inks on every
  stepper press (small pen flick animation); locked state shows the
  unlock condition as a checklist ("2 clean Mondays of 4"); DO lane =
  [borrow] [repay] [refinance] as available. Why: the loan calculator
  is the desk's toy — it should feel like one.
- **the works — THE FOUR-TYPE ENGINE.** Weak: relief steppers don't
  show marginal price; the un-billed number doesn't say who walked;
  rung-3 drill has no way back. Fix: relief rows show "next 10 ≈ $X vs
  $Y in-house" beside the stepper (S4 inline); the walked number
  pressable → the demand mix rows that overflowed; site drill leaves
  the S7 breadcrumb; DO lane = [set relief] [arrange] [open a roof]
  by rung. Why: overflow is now honest money — the levers that answer
  it should quote their price on the row.

### THE COMPANY (blue)

- **what we make — THE WALL.** Weak: the creak → rebuild path takes
  three reads; shelf cards don't cite the player's own history. Fix:
  press a creak card → [queue the rebuild] one-press (prefills the
  commit arm on the matching shelf candidate); shelf cards carry "your
  last keeps-bet measured +3.4" when history exists (S4); DO lane =
  [SHIP] when ready, else [commit — best fit]. Why: the wall already
  knows the answer; let it hand it over.
- **cap table — THE OWNERSHIP STATE.** Weak: the waterfall is one
  frozen number; dilution steps are inert. Fix: THE VALUATION SLIDER —
  drag "if sold at $X" and the waterfall redraws live (pure function,
  cheap, deeply teaching: watch preferences eat everything under
  ≈$700k); dilution story steps pressable → that event's receipt (S4).
  Why: the single best "aha" in startup finance, one slider away.
- **the raise — THE PIPELINE.** Weak: data-room doubts are named but
  not walkable; walking away isn't a visible choice. Fix: each doubt
  pressable → the weak desk via S2; DO lane = [pitch — name] [sign —
  best terms] [walk away] (walking is a real, styled choice per the
  desk's own law); the founder-time banner quotes this week's velocity
  cost in numbers. Why: the desk teaches that raising is optional and
  expensive — the UI should make "no" as pressable as "yes."
- **the street — THE WORLD.** Weak: read-only is right, but nothing
  marks what's NEW. Fix: S5 pen circles on new rival acts and mood
  shifts since last open; acts that targeted YOU keep their face-up
  pin + counter-desk jumps get back pills. Why: the street is a weekly
  newspaper — mark what's news.
- **threats — THE COMMAND CENTER.** Weak: rows jump to desks, not to
  switches; no age. Fix: S2 focus_control on every row; age chips ("3
  wks") once the engine carries since_wk (small field, this wave);
  rows ordered severity → age; the count badges the LOCK IN via the
  pre-roll. Why: this desk IS the to-do list — it should behave like
  one.
- **pivot — THE ESCAPE HATCH.** Weak: the preview lists deaths; the
  survivals read as afterthought; the two doors look symmetric. Fix:
  the preview becomes a two-column KEEP (sage) / DIES (red) ledger per
  door, numbers pressable (S4); doors carry their historical framing
  ("audience pivots are rarer and bloodier") in one line. Why: the
  scariest choice in the game deserves the clearest before/after in
  the game.

### THE LOG (yellow)

- **this week — THE COCKPIT.** Weak: it hosts the card, draft, armed
  list — but suggestions born on other desks never arrive here. Fix:
  THE WEEK'S CHIPS — a strip collecting every desk-suggested action
  (rank-1 push, adopt-the-book, answer-Ravi, queue-the-rebuild) as
  pressable chips that PREFILL the draft or deep-link (S2/S3 feeding
  one place); post-roll, the outcome view links each consequence line
  to its desk. Why: the landing tab should be where the week is
  DECIDED, not just where it's typed.
- **history — THE RUN'S LEDGER.** Weak: solid after the era-section
  fix; receipts one press down. Fix: light only — S5 sparkline
  endpoint dots, filed momentary rows (★) press-through to their
  receipts. Why: it works; don't gild it.
- **events — THE MAIL.** Weak: action letters carry jumps but not
  verbs; read/unread is the only state. Fix: action letters carry
  their DO inline ([answer], [read terms]) via S2; answered letters
  auto-file with a small ✓; the LOG divider badge counts unread
  action letters only. Why: mail you can act on from the envelope is
  the difference between an inbox and a chore.

## PART 3 — EXECUTION

- **Wave A (the kit, once):** S1 zero-state component · S2 ask strip +
  focus_control · S3 DO lane bar · S4 receipt popover · S5 delta layer
  (+ last-open field, since_wk field) · S6 fit_line/fit_par · S7
  keyboard + back stack · S8 dim states · S9 arm family styling. Twin
  suites extended per component.
- **Wave B (nineteen desks + frame adopt the systems):** per-tab
  entries above, six lanes in parallel by group (the DAG2 lane split
  worked; reuse it), coordinator merges.
- **Wave C (proof):** zero-state probe (all 19 desks at week 1),
  delta probe (two-open diff states), press-map probe (every DO lane
  and popover captured), a live-play round, recut DMGs.

**Rejected on purpose:** hiding dormant tabs (the map teaches); auto-
applied suggestions anywhere (adopt-only — teaching integrity); adding
more data panels to any desk (the gap is action and flow, not
information); drag-and-drop on the enterprise board (press-to-push
through the journal keeps the one-writer law).
