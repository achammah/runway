# 01 — THE CATALOG & UNIT ECONOMICS (itemized costs)

Desk: `pricing`. Engine files: `game/src/core/sim_engine.gd` /
`unity/Assets/Scripts/Core/SimEngine.cs`. The Godot half of this design is
partially in the working tree (uncommitted); the C# half has only
`AddOffer/RemoveOffer/DraftOfferTerms` without lines. This document is the
full contract both engines must land together.

The law, unchanged: the ENGINE owns every number (every line, every total,
every clamp); the DM owns sentences; the LLM proposes terms that are ALWAYS
shown for adjustment and only enter the books through `add_offer`'s clamps
after the founder confirms.

North star: this desk teaches unit economics by their real names — COGS,
fixed vs variable cost, contribution margin, break-even — with receipts
that say why a number moved.

---

## 1. State model

### 1.1 Offer — Godot dict keys (`state.offers: Array[Dictionary]`)

| key | type | default | notes |
|---|---|---|---|
| `name` | String | "" | ≤40 chars |
| `unit` | String | "per order" | ≤20 chars, drives `offer_cadence` |
| `fair_price` | float | 1.0 | street reference, clamp [1, 50000] |
| `elasticity` | float | 2.0 | clamp [0.5, 3.0] |
| `unit_cost` | float | 0.0 | **derived** = Σ variable lines when `cost_lines` present; standalone scalar for legacy offers |
| `price` | float | 0.0 | 0 + !price_set = bills at fair (backstop) |
| `price_set` | bool | false | conscious price choice, $0 included |
| `weight` | float | 1.0 | clamp [0.2, min(3.0, 6.0 − Σ other weights)] |
| `cost_lines` | Array | *(absent)* | NEW — `[{label: String ≤24, amount: float}]`, 1–4 items, variable $ per unit |
| `fixed_lines` | Array | *(absent)* | NEW — same shape, 0–3 items, $ per week |
| `fixed_wk` | float | 0.0 | NEW — **derived** = Σ fixed lines, cached for the tick |

### 1.2 Offer — C# (`GameState.cs`)

```csharp
public sealed class CostLine
{
    [JsonProperty("label")] public string Label = "";
    [JsonProperty("amount")] public double Amount;
}
// added to Offer:
[JsonProperty("cost_lines")]  public List<CostLine> CostLines;   // null = legacy offer
[JsonProperty("fixed_lines")] public List<CostLine> FixedLines;  // null = none
[JsonProperty("fixed_wk")]    public double FixedWk;             // derived cache, default 0
```

`Offer.Duplicate()` currently uses `MemberwiseClone` — it MUST deep-copy
`CostLines`/`FixedLines` (new lists, new `CostLine` instances) or twin tests
that duplicate offers will share line objects. Godot mirror: any code
duplicating an offer dict that may carry lines uses `duplicate(true)`.

### 1.3 Pnl lane (both engines)

Godot `state.set_meta("pnl", {...})` gains key `"offer_fixed"` (int) —
already present in the Godot working tree. C# `Pnl` adds:

```csharp
[JsonProperty("offer_fixed")] public int OfferFixed;
```

### 1.4 Engine constants (new, both engines)

```
ERA_OFFER_CAP := {"garage": 2, "coworking": 3, "office": 5, "floor": 8, "hq": 8}
ERA_TOOL_SCALE := {"garage": 1.0, "coworking": 1.4, "office": 2.2, "floor": 4.0, "hq": 7.0}
SHELF_WEIGHT_CAP := 6.0        # Σ offer weights, whole catalog
```

Salt: keyless draft jitter uses **salt 21** (unused; taken: 4,5,6,7,9,77,88,91,93).

### 1.5 Save compatibility

- Old saves: no `cost_lines`/`fixed_lines`/`fixed_wk` keys → Godot `get(k, default)`
  and C# null/0 defaults reproduce today's behavior exactly. No migration.
- Legacy offers keep the scalar `unit_cost` path forever; lines are never
  synthesized on load. The desk shows a totals-only view for them (§7).
- A tampered/hand-edited `fixed_wk` with no `fixed_lines` is caught at read:
  `offers_fixed_wk` clamps each offer's contribution to [0, 10000].
- The in-flight LLM proposal (§8) lives only in the open desk, never in
  `GameState` — closing the binder discards it; saves cannot carry it.
- Era demotion below the offer cap never deletes offers; the cap gates
  `add_offer` only.

---

## 2. Formulas, constants, clamps

Every formula names its real-world analogue (⟶) and its dropped reality
where it simplifies.

**F1 — itemized variable cost (the sync law).** Called by `sync_offer_costs(offer)`
after ANY per-line change and inside `add_offer`:

```
for line in cost_lines:  line.amount = clampf(amount, 0.0, fair_price * 0.5)
unit_cost = clampf(Σ cost_lines.amount, 0.0, fair_price * 0.9)
for line in fixed_lines: line.amount = clampf(amount, 0.0, 5000.0)
fixed_wk  = clampf(Σ fixed_lines.amount, 0.0, 10000.0)
```

Sync touches `unit_cost` **only when `cost_lines` exists AND is non-empty**
(same for `fixed_wk`/`fixed_lines`) — the Godot partial's `has()` check must
gain the non-empty guard, and C# uses `!= null && Count > 0`. Totals can
never drift from their receipts; receipts can never exceed the world.
⟶ bill of materials → unit COGS; the 0.9×fair total cap is the "no negative
gross margin by accident" floor (10% worst-case gross margin — selling below
variable cost must be a *pricing* decision, visible on the price row, not a
cost-entry typo).

**F2 — catalog overhead.**

```
offers_fixed_wk(state) = Σ over offers of clampf(fixed_wk, 0, 10000)
```

Flat dollars. **No era scaling, no per-offer-count scaling** — decided in §5.
⟶ operating expenses below gross margin (tool subscriptions, licenses —
SG&A-shaped, not COGS). Dropped reality: vendors reprice over time; that
belongs to the incident/commitment engine, which already produces receipted
standing costs — a silently inflating line would be a hidden multiplier,
the exact #196 sin.

**F3 — COGS per customer-week** (exists, unchanged shape):

```
offers_cogs_per_customer = Σ over billing offers of
    weight * unit_cost * learning_curve(state) * offer_cadence(unit)
```

"Billing offers" includes conscious-free (`price_set` at $0) offers — a
giveaway still costs to serve — and excludes nothing else.
⟶ variable cost × volume, normalized to weekly (cadence is the MRR-style
frequency conversion: per-month = 0.25/wk etc.). Dropped: order-level
variance; the engine's seeded-remainder idiom already keeps fractional
flows honest at this granularity.

**F4 — learning curve applies to the VARIABLE TOTAL, never per line, never
to fixed lines.** `learning_curve` stays `max(1 − 0.115·log10(served_total), 0.65)`.
Decision + why: mathematically `lc·Σaᵢ = Σ(lc·aᵢ)`, so per-line application
buys nothing but display drift — the founder's stepped line amounts must
stay exactly the numbers they set (they are receipts), with learning shown
once as `×0.87` at the total. Fixed lines never learn: a license does not
get cheaper because you served customers.
⟶ Wright's experience curve (~15% cost per doubling ≈ our 11.5% per decade
of cumulative volume), floored at 65%. Dropped: forgetting/turnover effects
— those are HR-subsystem territory.

**F5 — demand, pain, arpu** (all exist, untouched by this wave):
`offer_demand = clamp((p/fair)^−ε, 0, 2)` ⟶ constant-elasticity demand
around a reference price; `offers_price_pain` (retention pain +0.4 per
100% over fair, cap 1.6) ⟶ invoice-regret churn; `offers_arpu = Σ w·p·cadence`
⟶ revenue per account per week. The fair-price backstop for unpriced offers
⟶ "billing at the going market rate"; dropped reality: an unpriced product
really earns $0 — accepted by owner's law ("customers paying $0 is
IMPOSSIBLE" reads as a bug, not a lesson).

**F6 — add_offer (the only door into the books).** Signature both engines:

```
add_offer(state, name, unit, fair, cost, elasticity, weight,
          cost_lines = [], fixed_lines = []) -> Dictionary / Offer (empty/null on refusal)
```

Order of operations:
1. **Refuse** (return `{}` / `null`) when `offers.size() >= ERA_OFFER_CAP[era]`
   or `Σ existing weight >= SHELF_WEIGHT_CAP − 0.2` (no room for even a
   minimum-weight offer).
2. Clamp scalars: `fair` [1, 50000] · `unit_cost` [0, 0.9·fair] ·
   `elasticity` [0.5, 3.0] · `weight` [0.2, min(3.0, 6.0 − Σ existing w)].
3. Sanitize lines: keep at most **4** cost_lines and **3** fixed_lines
   (drop extras from the tail); `label` = stripped, ≤24 chars, "" → "line";
   non-numeric amount → 0. Empty arrays are NOT stored (key absent).
4. `sync_offer_costs(offer)` — per-line + total clamps land (F1).
5. `price = 0.0`, no `price_set` → arrives billing at fair, bang until priced.

⟶ the Σw shelf cap is share-of-wallet: a customer's weekly budget is finite,
so a spammed catalog cannot mint arpu (see §5). Dropped: substitution/
cannibalization between own offers; the portfolio cap approximates the
finite wallet at game precision.

**F7 — keyless draft, v2** (`draft_offer_terms(state, idea)`), fully seeded:

```
aud   = 0.25 Consumer | 1.0 SMB | 4.0 Enterprise
unit  = sniffed from idea words (existing table, unchanged)
r     = _rng(state, 21)                      # NEW salt 21
fair  = round(40.0 * aud * r.randf_range(0.8, 1.3))
tool  = ERA_TOOL_SCALE[state.era]
variable_costs = [ {"materials & delivery", round(fair * 0.20)},
                   {"labor share",          round(fair * 0.15)} ]
fixed_costs_wk = [ {"tools & subscriptions", round(15.0 * aud * tool)} ]
elasticity = 2.0, weight = 1.0
```

One `randf_range` draw, first thing, so both engines consume the stream
identically. Same (seed, week) → same draft: replays hold.
⟶ cost-plus estimation at a ~65% gross margin, era-scaled tooling; the
jitter keeps two keyless runs from selling identical-price workshops.

**Edge cases**
- **0 offers**: `offers_fixed_wk` → 0; arpu/demand return the −1 sentinel;
  theta fallback path unchanged.
- **All offers free on purpose**: revenue 0, COGS still bills (F3), demand
  mult = giveaway cap 2.0, pain 1.0 — pinned in §9/P6.
- **All offers unpriced**: backstop bills at fair; bang on.
- **Absurd LLM output**: schema forces the unit enum and numeric ranges;
  everything else dies in F6 steps 2–4 (a $900 line on a $70 offer becomes
  $35; three of them total-clamp to $63). Nothing enters unclamped.
- **`fair_price` 0 on a legacy offer**: F1 clamps lines against
  `max(fair, 1.0)`; billed price falls back per `offer_billed_price`.
- **Founder steps every line to $0**: legal — serving is free, margin is
  price; lines are never deletable, only steppable to zero.

---

## 3. Weekly-tick integration (exact order)

All inside section 9 (money) of `weekly_tick`/`WeeklyTick`, in this order
(Godot working tree already does 1–4; C# must add 3–5):

1. revenue (arpu path) → 2. payroll/rent/infra → R&D block → 3. `cogs`
   computed + report line → 4. `served_total` meta update →
5. `offer_fixed := offers_fixed_wk(state)` + report line →
6. `burn = (rent + payroll + infra + levers) × burn_mult + cogs + offer_fixed`
   (+ incident). COGS and catalog fixed sit OUTSIDE `burn_mult`: difficulty
   scales operating spend, never the physical cost of goods.
7. pnl record gains lane `"offer_fixed"` / `OfferFixed`;
   `net = revenue − burn + liabilities` identity unchanged.

**Report lines** (both engines, exact strings):
- COGS (reword of the existing line, pedagogy):
  `"COGS $%d — serving %d customers (variable cost × volume%s)"` with
  `", learning ×%.2f"` appended when lc < 0.995.
- Catalog fixed (fires when ≥ $1):
  `"fixed costs — the catalog's standing tools: $%d/wk (billed sold or not)"`.
- Existing backstop / free-on-purpose / nothing-on-sale lines: unchanged.

**Ledger desk**: the `out:` line appends `" · catalog $%s"` when
`pnl.offer_fixed > 0` (both engines).

**`runway_weeks` becomes honest**: its burn estimate adds
`offers_fixed_wk(state) + traction × offers_cogs_per_customer(state)`
(when the offers path is active). Today the health band ignores serving
cost entirely — a high-COGS company reads healthier than it is. No current
twin pin asserts a runway number (verified); the DIRECTIVES escalation
threshold inherits the honesty.

**DM context** (`event_generator.gd _directives` + C# `EventGenerator.Directives`):
- the on-sale line gains the serve cost:
  `"- On sale: '%s' at $%d %s (costs ~$%d a sale to serve)."`
- one catalog line when `offers_fixed_wk ≥ 1`:
  `"- The catalog carries $%d/wk of standing tool costs, sold or not."`

---

## 4. The demand side — two decisions

**D1 — weight stays ABSOLUTE in arpu (no normalization), capped by the
shelf.** `offers_arpu = Σ w·p·cadence`: adding an offer genuinely adds
revenue per customer — that is the cross-sell fantasy and the Bonopoly
catalog loop. Normalizing would make every new offer cannibalize the last,
which punishes the exact behavior the subsystem exists to reward. The
exploit (LLM-spam 20 offers → infinite arpu) is closed structurally:
`Σ weight ≤ 6.0` + `ERA_OFFER_CAP` (F6.1), both engine clamps, not UI
politeness. Demand mult and price pain remain weight-AVERAGED (unchanged) —
they are per-dollar perceptions, not volumes.

**D2 — fixed costs do NOT scale with era or offer count.** Fixed lines are
printed receipts; a receipt that silently grows with promotion is a hidden
multiplier (the #196 law). Era pressure already lives in rent; audience and
stage scale enters at PROPOSAL time only (LLM sees `era` in the payload,
keyless uses `ERA_TOOL_SCALE`) — a floor-era founder is QUOTED heavier
tooling, then the quoted number stays the number until stepped by hand.

---

## 5. Scaling by stage (era-progressive depth)

The engine always books the full itemized truth; the ERA gates what the
desk exposes and how many offers the shelf holds. Unlocks are desk-side
reads of `state.era` — no new state.

| era | shelf cap | the desk teaches |
|---|---|---|
| garage | 2 | Price is the only dial. Rows + price steppers; costs shown as two totals ("a sale costs ≈ $X to serve · tools $Y/wk"). Review card in totals mode (§7.5). COGS named on the ledger from week 1. |
| coworking | 3 | **Real cost accounting appears.** The detail card opens the fine print: variable lines vs fixed lines by name, per-line steppers, `contribution margin = price − variable cost`, and per-offer **break-even**: `fixed_wk / (price − unit_cost·lc)` sales/wk. |
| office | 5 | **Portfolio management.** Weight becomes visible and steppable (ladder §7.4) with the shelf meter "shelf: Σ4.2 of 6.0 used"; the price verdict names discounting ("−30% vs street"); the list summary ranks offers by contribution. |
| floor | 8 | **Product lines with own P&Ls.** The detail card adds the offer's weekly mini P&L: `≈U sales → $R in − $V variable − $F fixed = $C contribution` at current volume. |
| hq | 8 | Nothing new unlocks; the catalog is a portfolio of per-line P&Ls. Depth now comes from volume, learning, and the funnel subsystem. |

Garage totals-mode editing: the single "serve cost" stepper scales ALL
variable lines proportionally (×/÷ one ladder step) then re-syncs — the
lines stay the stored truth, so nothing is lost when coworking reveals them.

---

## 6. Pedagogy contract (what the receipts must say)

- The desk uses the real names, always: **COGS**, **variable cost**,
  **fixed costs**, **contribution margin**, **break-even**, **ARPU**.
- Every number that moves prints its WHY in the same breath: the COGS line
  carries `variable cost × volume` and the learning factor; the fixed line
  carries `billed sold or not`; the break-even line reads
  `"break-even: %d sales/wk pay for its tools"` — and when
  `price ≤ unit_cost·lc` it flips to coral:
  `"this price never pays for itself — every sale loses $%d"` (the one
  lesson a founder must not miss).
- List summary (coworking+), exact copy:
  `"unit economics: ≈ $%.1f ARPU − $%.1f COGS = $%.1f contribution per customer per week → ≈ $%s/wk at %d customers"`
  with the footer
  `"COGS bills only when you sell · fixed bills either way · price − variable = contribution margin"`.
  Garage keeps today's curve hint instead.

---

## 7. The desk — pricing tab v2 (both engines)

BinderScreen mirrors binder.gd coordinates 1:1 (`L(text,x,y,size,col,w)` ↔
`_label`, `GameUi.InkWord` ↔ `_ink_btn`), so one coordinate spec serves
both. Content pane 1160×760 at `_content` origin; running y-cursor idiom
(measure wraps, never assume one line — the street tab's law).

The tab is a five-state machine. Desk-local fields (NOT saved):
`_pricing_mode: String` in {"list","detail","write","wait","review"},
`_detail_idx: int`, `_pending: Dictionary/Offer proposal`, `_drop_armed: bool`.

### 7.1 LIST (default)

- (10, 6) 36px header: `pricing — what {company} sells`.
- Rows from y = 84, pitch 62 per collapsed row, max 8:
  - (10, y) 28px: `NAME  ·  unit`
  - (10, y+32) 20px ink@0.55 receipts: garage
    `serve ≈ $C` · coworking+ `serve ≈ $C (×LC learned) · fixed $F/wk · margin $M/unit`
  - (430, y+4) 26px status (existing three-state: FREE ON PURPOSE blue /
    `! billing at the going rate $F` coral / `$P · margin $M/unit · verdict`;
    office+ verdict adds the discount read `(−30% vs street)` when p < fair)
  - `▸` expand (936, y) 52×46 → DETAIL(i) · `−` (1000, y) · `+` (1064, y)
    price steppers, existing fair-multiple ladder
    [off, 0.4, 0.55, 0.7, 0.85, 1.0, 1.15, 1.35, 1.6, 2.0, 2.6, 3.5, 5.0]×fair.
- After rows: summary block (§6) — 26px blue line + 20px ink@0.5 footer.
- Bottom: `+ sell something new` ink button (10, cursor+16) 340×48 → WRITE.
  When `add_offer` would refuse: replaced by 24px ink@0.5 label
  `"the shelf is full at this stage — drop something first"`.

### 7.2 DETAIL(i) — one offer, whole pane

- (10, 6) `◂ all offers` 200×44 → LIST. `drop this offer ×` coral 24px at
  (940, 14) 210×40; first press arms (`sure? it disappears ×`), second
  press within this visit calls `remove_offer` → LIST.
- (10, 64) 34px: `NAME · unit`.
- (10, 110) 24px ink@0.6:
  `the street charges ≈ $F · a sale costs ≈ $C to serve (learning ×LC) · fixed $FX/wk`.
- PRICE row: (10, 158) 28px `PRICE`; (150, 152) 32px coral `$P per unit`
  (or the unpriced/free status); steppers − (1000, 152) + (1064, 152),
  same ladder as list. Verdict line (10, 204) 24px: demand verdict +
  `contribution margin $M/unit (price − variable cost)` [coworking+].
- **The fine print** [coworking+; garage shows §7.5 totals mode instead]:
  - (10, 250) 24px ink@0.55: `what one sale costs — variable`.
  - Variable line rows from ly = 286, pitch 40: label (40, ly) 24px;
    `$A` (560, ly) 24px coral; − (1000, ly−4) + (1064, ly−4) 52×46.
    Ladder per press: fair-relative
    [0, 0.02, 0.05, 0.08, 0.12, 0.16, 0.22, 0.30, 0.40, 0.50]×fair,
    rounded, clamp [0, 0.5×fair]. Every press calls `sync_offer_costs`.
  - Sum line 22px blue: `= variable cost $UC/unit · served at ×LC today`.
  - +12px: (10, ·) 24px ink@0.55: `standing costs — every week, sold or not`.
  - Fixed line rows, same idiom; ladder (absolute $)
    [0, 5, 10, 15, 25, 40, 60, 90, 140, 220, 350, 550, 900, 1400, 2200, 3500, 5000].
  - Sum line 22px blue: `= $FX/wk · break-even: N sales/wk pay for it`
    (coral lesson line when price never pays, §6).
- Weight row [office+]: (10, ·) 24px `shelf weight W — the slice of a
  customer's wallet`; steppers on ladder
  [0.2, 0.4, 0.6, 0.8, 1.0, 1.3, 1.6, 2.0, 2.5, 3.0], engine-clamped to
  `min(3.0, 6.0 − Σ others)`; meter text `shelf: Σ%.1f of 6.0 used`.
- Mini P&L [floor+]: one wrapped 22px blue label:
  `this offer, a week at current volume: ≈%d sales → $%d in − $%d variable − $%d fixed = $%d contribution`
  where `sales/wk = traction × weight × cadence`, `in = sales × price`,
  `variable = sales × unit_cost × lc`, `contribution = in − variable − fixed_wk`.
- Worst case fits: 4+3 lines × 40 + headers + weight + mini P&L ≤ 760
  (cursor layout; only one offer is ever expanded — DETAIL replaces LIST).

### 7.3 WRITE (the write-in)

- (10, 6) 36px: `what do you want to sell?`
- (10, 58) 22px ink@0.55: `plain words — "a monthly meal-prep box", "API access for clinics", "a two-hour audit"`.
- Text box (10, 96) 1140×150: Godot `TextEdit` (journal `_wire_free` idiom:
  Enter submits, Shift+Enter newlines); Unity `TMP_InputField` built the way
  `PageBlocks.WriteField` builds one (hand font, multiline submit), parented
  to `_content`.
- `price it ▸` (10, 268) 220×50 → WAIT (or straight to REVIEW when keyless).
  Empty/`< 3 chars` input: button does nothing. `never mind` (250, 268)
  200×50 → LIST.

### 7.4 WAIT (pricing in flight)

- Header + (10, 96) 30px: `the street is pricing it…` ·
  (10, 150) `cancel` 160×44 → LIST (proposal dropped on arrival).
- Fires `price_offer_idea` (tier "clarify", watchdog inherited). Callback
  guards: `is_instance_valid(self)` (Godot) / destroyed check (C#) AND
  `_pricing_mode == "wait"` — otherwise the reply is dropped on the floor.
  Success → REVIEW with the LLM terms; `{}`/failure → REVIEW with the
  keyless draft + one 22px ink@0.5 note `the street shrugged — house numbers`.
- Binder closed mid-flight: proposal discarded (desk-local state dies with
  the node). No offer ever appears unreviewed.

### 7.5 REVIEW (the owner's requirement: ALWAYS shown before the books)

Same renderer as DETAIL with three changes:
- banner replaces the back button: (10, 6) 28px coral
  `the street's terms — adjust the lines, then shelve it`;
- the PRICE row is replaced by 24px ink@0.6
  `arrives unpriced — it bills at the going rate ≈ $F until you price it`
  (one decision at a time; the bang walks the founder back here);
- bottom buttons: `put it on the shelf` (10, 700) 300×50 →
  `add_offer(state, name, unit, fair, Σvar, elasticity, weight, cost_lines,
  fixed_lines)` → on success `log_action("NEW OFFER shelved: %s (%s) — street $%d")`
  → LIST; on refusal (cap raced) a coral line explains. `tear it up`
  (340, 700) 200×50 → LIST, proposal gone.
- Steppable in review: the LINES only (garage: the two proportional totals,
  §5). Name, unit, fair, elasticity, weight are the street's read — the
  founder's lever is price (later) and costs, not the market's shape.

### 7.6 The bang

Unchanged trigger, now with more ways to fire it: pricing tab bang and the
garage binder chip = `offers_any_unpriced(state)` — every shelved review
offer arrives unpriced, so the coral `!` walks the founder from "shelved"
to "priced" with zero new plumbing. Meaning line stays: something bills at
the going rate — name a price.

### 7.7 Wiring

Godot: `Binder.setup(p_state, p_gen: EventGenerator = null)`;
`garage_view_screen._open_binder()` passes its `generator`. Unity:
`BinderScreen` reads `Boot.Instance.Generator`. Null/`!llm.enabled()` →
keyless path (skip WAIT).

---

## INTERFACE DELTA (assessable, stands alone)

Every UI change this lane needs, both engines (coordinates are the shared
1160×760 binder content pane; Unity mirrors Godot 1:1).

| surface | exists today? | change | exactly how | why the player needs it |
|---|---|---|---|---|
| binder · pricing — collapsed offer row | yes (104px row: name, street/serve line, price status, − + steppers) | CHANGE | pitch 104→62px; receipts line becomes `serve ≈ $C (×LC learned) · fixed $F/wk · margin $M/unit` (garage: `serve ≈ $C` only); NEW `▸` expand button (936, y) 52×46; − + stay at (1000)/(1064) | scan the whole shelf's unit economics without opening anything |
| binder · pricing — DETAIL card | no | ADD | full-pane card replacing the list (one offer at a time): `◂ all offers` (10,6); `drop this offer ×` (940,14) two-press confirm; PRICE row + steppers + `contribution margin $M/unit`; itemized variable lines each with − + steppers (ladder = fair-relative %); fixed lines each with − + steppers ($ ladder); `break-even: N sales/wk pay for it` line; weight stepper + `shelf: Σx of 6.0` (office+); one-line mini P&L (floor+) | edit every cost line by hand; learn margin and break-even where the decision happens |
| binder · pricing — `+ sell something new` | no | ADD | ink button 340×48 under the summary; when the era shelf is full it is replaced by the label `the shelf is full at this stage — drop something first` | grow the catalog; know why growth is blocked |
| binder · pricing — WRITE state | no | ADD | header `what do you want to sell?`, hint line, text box (10,96) 1140×150 (Godot TextEdit / Unity TMP_InputField, Enter submits), `price it ▸` + `never mind` buttons | describe the new offer in plain words |
| binder · pricing — WAIT state | no | ADD | `the street is pricing it…` + `cancel`; reply dropped if cancelled/closed | see the pricing call in flight; escape it safely |
| binder · pricing — REVIEW card | no | ADD | the DETAIL renderer with: coral banner `the street's terms — adjust the lines, then shelve it`; `arrives unpriced — bills at the going rate ≈ $F until you price it` note instead of the price row; per-line steppers live; `put it on the shelf` / `tear it up` buttons at (10,700)/(340,700) | owner requirement: LLM terms are ALWAYS reviewed and adjustable before entering the books |
| binder · pricing — summary block | yes (one blue arpu line + curve hint) | CHANGE | coworking+: `unit economics: ≈ $A ARPU − $C COGS = $M contribution per customer per week → ≈ $T/wk at N customers` + footer `COGS bills only when you sell · fixed bills either way · price − variable = contribution margin`; garage keeps today's copy | the catalog's whole P&L story in the game's real vocabulary |
| binder · pricing — price verdict text | yes (fair/deal/pricey/absurd) | CHANGE (office+) | below-fair prices append `(−30% vs street)` | discounting gets named as a strategy |
| binder · pricing tab bang + garage binder chip `!` | yes (`offers_any_unpriced`) | UNCHANGED trigger | newly shelved offers arrive unpriced, so the existing bang walks the founder from "shelved" to "priced" | attention lands exactly where a price is missing |
| binder · the ledger — `out:` line | yes | CHANGE | append ` · catalog $X` when the pnl `offer_fixed` lane > 0 | the catalog's standing cost is visible where the money leaves |
| journal · weekly report — COGS line | yes (`cost of serving customers: $X`) | CHANGE | `COGS $X — serving N customers (variable cost × volume, learning ×L)` (learning shown when < 0.995) | teaches COGS by name, with its why |
| journal · weekly report — catalog fixed line | Godot only (uncommitted) | ADD both | `fixed costs — the catalog's standing tools: $X/wk (billed sold or not)` | the fixed-vs-variable lesson lands weekly |
| journal · DETAIL lesson line | no | ADD | when `price ≤ unit_cost×lc`: coral `this price never pays for itself — every sale loses $X` | the one unit-economics mistake a founder must not miss |
| DM narration context (directives) | yes (on-sale lines) | CHANGE + ADD | on-sale lines gain `(costs ~$C a sale to serve)`; new line `The catalog carries $X/wk of standing tool costs, sold or not` when > 0 | the storyteller narrates from real unit economics, never invents them |
| binder · pricing — era unlocks | no | ADD | detail-card sections appear by era (§5): fine print + break-even at coworking; weight + shelf meter + discount read at office; mini P&L at floor | progressive depth — the desk grows as the company does |

---

## 8. LLM leverage

Where a model genuinely earns its place in this subsystem — and where it
must never be let in. House rule applies throughout: schema-forced JSON,
clamped on entry, keyless fallback for every call.

**L1 — pricing a founder-written offer (the one real call).**
Trigger: WRITE submit. Shape: ONE `request_json` per submission, tier
`"clarify"` (fast lane, cheap model), `EventGenerator.price_offer_idea` /
C# twin `PriceOfferIdea`. Value: mapping plain words to plausible market
terms AND concrete itemized cost labels in the business's own vocabulary
("cold-chain packaging", "a barista's hour") is exactly what a model does
well and a lookup table cannot. It must NEVER decide: the founder's actual
price (arrives unpriced), whether the offer enters the books (review card
does), or any number's final value (F6 clamps + steppers do).

**L2 — the same call is the naming call.** No separate dressing call:
`name` and the line `label`s ride L1's schema. Keyless labels are generic
on purpose ("materials & delivery") so a keyed run visibly reads richer.

**L3 — the DM knows the catalog for free.** Serve costs and catalog
overhead ride the existing adjudication context as `_directives` lines
(§3) — zero extra calls; the storyteller can say "the workshop barely
clears its room rental" because the engine told it so.

**Tempting but wrong:**
- LLM sets or nudges the live price — that is the player's one strategic
  dial; auto-pricing deletes the game.
- LLM drifts `fair_price`/`elasticity` over time ("the market moved") — an
  invisible number-decider; market motion belongs to the seeded trend walk.
- LLM invents weekly cost fluctuations ("supplier hiked prices") — cost
  surprises must be engine incidents/commitments with receipts, or the
  ledger stops being trustworthy.
- An "AI cost optimizer" suggesting cheaper lines — decides numbers,
  teaches nothing; the steppers + break-even line teach it honestly.
- LLM re-validating founder line edits — `sync_offer_costs` is the
  deterministic validator; a second, stochastic one would disagree with it.

### 8.1 Final system prompt (`OFFER_PROMPT`, byte-synced both engines)

```
You itemize and price a new product or service for a startup-survival
business simulator. You receive the company (what kind, for whom, its idea,
its stage) and the founder's plain-words description of something new they
want to sell. Output realistic market terms as strict JSON:
- name: a short clean name for the offer, taken from the founder's words (<=40 chars)
- unit: the billing unit — one of "per session", "per month", "per order", "per unit", "per year", "per hour"
- fair_price: what this audience typically pays per unit at the going market rate, in USD (Consumer offers are cheap, Enterprise expensive)
- elasticity: how hard demand punishes overpricing — 0.8 luxury/inelastic, ~2.0 typical, 2.6 commodity
- weight: how much of an average customer's weekly spend lands on this offer (1.0 typical, 0.5 side item, 2.0 flagship)
- variable_costs: 1-4 itemized costs paid EVERY TIME one unit is sold or served (materials, packaging, compute, payment fees, a worker's hour). Concrete labels (<=24 chars) in this business's own vocabulary, never generic. Amounts in USD per unit; their SUM should land at 15-60% of fair_price — a plausible gross margin for this kind of business.
- fixed_costs_wk: 0-3 weekly standing costs this offer adds whether or not anything sells (a tool subscription, a license, storage, a rented machine). USD per week, scaled to the company's stage.
Never invent revenue, discounts, or advice. Strict JSON only. No prose.
```

### 8.2 Final JSON schema (`OFFER_SCHEMA` v2 — replaces the uncommitted flat one)

```json
{
  "type": "object", "additionalProperties": false,
  "required": ["name", "unit", "fair_price", "elasticity", "weight",
               "variable_costs", "fixed_costs_wk"],
  "properties": {
    "name": {"type": "string", "maxLength": 40},
    "unit": {"type": "string", "enum": ["per session", "per month", "per order",
             "per unit", "per year", "per hour"]},
    "fair_price": {"type": "number", "minimum": 1, "maximum": 50000},
    "elasticity": {"type": "number", "minimum": 0.5, "maximum": 3.0},
    "weight": {"type": "number", "minimum": 0.2, "maximum": 3.0},
    "variable_costs": {
      "type": "array", "minItems": 1, "maxItems": 4,
      "items": {"type": "object", "additionalProperties": false,
        "required": ["label", "amount"],
        "properties": {"label": {"type": "string", "maxLength": 24},
                        "amount": {"type": "number", "minimum": 0, "maximum": 25000}}}},
    "fixed_costs_wk": {
      "type": "array", "minItems": 0, "maxItems": 3,
      "items": {"type": "object", "additionalProperties": false,
        "required": ["label", "amount"],
        "properties": {"label": {"type": "string", "maxLength": 24},
                        "amount": {"type": "number", "minimum": 0, "maximum": 5000}}}}
  }
}
```

`unit_cost` is gone from the schema — it is derived (F1). C#: `OfferSchema`
JObject + `OfferPrompt` land in `LlmClient.cs`, `PriceOfferIdea` in
`EventGenerator.cs` (all currently missing there).

### 8.3 User payload

```json
{"company": {"name": "...", "idea": "...", "what": "Software",
             "who": "SMB", "era": "coworking"},
 "new_offer": "<founder text, ≤200 chars>"}
```

`era` is new in the payload — stage-scaled tooling comes from the proposal,
not from a runtime multiplier (D2).

### 8.4 Failure / keyless

No key, model down, watchdog fired, or `{}`: REVIEW opens with
`draft_offer_terms` (F7) — the game is fully playable keyless, drafts are
seeded and replayable, and the review card is identical either way.

---

## 9. Twin-suite test pins

Same six blocks in `game/tests/sim_engine_test.gd` (`_ok`) and
`unity/Runway.Core.Tests/Program.cs` (`Ok`); both suites grow by the same
check count. Offers carrying lines must be duplicated deep (`duplicate(true)`
/ fixed `Offer.Duplicate`).

**P1 — the itemized truth syncs and clamps.**
```gdscript
var it := {"name": "boxed lunch", "unit": "per order", "fair_price": 70.0,
    "elasticity": 2.0, "weight": 1.0, "price": 0.0,
    "cost_lines": [{"label": "ingredients", "amount": 12.0},
        {"label": "packaging", "amount": 10.0}],
    "fixed_lines": [{"label": "kitchen license", "amount": 30.0}]}
SimEngine.sync_offer_costs(it)
_ok(absf(float(it.unit_cost) - 22.0) < 0.01, "unit_cost = Σ variable lines (22)")
_ok(absf(float(it.fixed_wk) - 30.0) < 0.01, "fixed_wk = Σ fixed lines (30)")
var greedy := {"fair_price": 70.0, "cost_lines": [{"label": "a", "amount": 900.0},
    {"label": "b", "amount": 900.0}, {"label": "c", "amount": 900.0}]}
SimEngine.sync_offer_costs(greedy)
_ok(absf(float((greedy.cost_lines[0] as Dictionary).amount) - 35.0) < 0.01,
    "a hostile line clamps to half of fair (35)")
_ok(absf(float(greedy.unit_cost) - 63.0) < 0.01,
    "the variable total clamps to 0.9×fair (63)")
```

**P2 — the offer_fixed lane exists and the P&L identity holds.**
```gdscript
var fx := _state(); fx.traction = 10; fx.set_flag("launched")
fx.offers = [{"name": "s", "unit": "per session", "price": 70.0,
    "fair_price": 70.0, "unit_cost": 20.0, "weight": 1.0, "fixed_wk": 120.0,
    "fixed_lines": [{"label": "booking tool", "amount": 120.0}]}]
SimEngine.weekly_tick(fx)
var pnl_fx: Dictionary = fx.get_meta("pnl", {})
_ok(int(pnl_fx.offer_fixed) == 120, "the catalog's fixed lane bills $120")
_ok(int(pnl_fx.net) == int(pnl_fx.revenue) - int(pnl_fx.burn) - int(pnl_fx.liabilities_wk),
    "the P&L identity balances with the offer_fixed lane in burn")
```

**P3 — learning cuts the variable total only; fixed never learns.**
```gdscript
var lc_s := _state(); lc_s.set_meta("served_total", 1000)
lc_s.offers = [{"name": "s", "unit": "per session", "price": 70.0,
    "fair_price": 70.0, "unit_cost": 22.0, "weight": 1.0, "fixed_wk": 30.0}]
var cpc := SimEngine.offers_cogs_per_customer(lc_s)
_ok(cpc > 14.2 and cpc < 14.6, "learning serves 22 at ~14.4 (×0.655)")
_ok(absf(SimEngine.offers_fixed_wk(lc_s) - 30.0) < 0.01, "fixed lines never learn (30)")
```

**P4 — hostile numbers clamp; the era shelf refuses the overflow.**
```gdscript
var cap_s := _state(); cap_s.era = "coworking"     # ERA_OFFER_CAP 3
var o1: Dictionary = SimEngine.add_offer(cap_s, "big thing", "per unit",
    900_000.0, 900_000.0, 99.0, 99.0)
_ok(float(o1.fair_price) == 50_000.0 and float(o1.unit_cost) <= 45_000.0
    and float(o1.elasticity) == 3.0 and float(o1.weight) <= 3.0,
    "hostile terms pass every clamp")
SimEngine.add_offer(cap_s, "b", "per order", 40.0, 10.0, 2.0, 1.0)
SimEngine.add_offer(cap_s, "c", "per order", 40.0, 10.0, 2.0, 1.0)
var o4: Dictionary = SimEngine.add_offer(cap_s, "d", "per order", 40.0, 10.0, 2.0, 1.0)
_ok(o4.is_empty() and cap_s.offers.size() == 3,
    "coworking shelves three offers, the fourth is refused")
```
(C#: `Ok(o4 == null && capS.Offers.Count == 3, ...)`.)

**P5 — the keyless draft is seeded, itemized, and in band.**
```gdscript
var dr := _state()     # seed 42, week 5, SMB
var d1: Dictionary = SimEngine.draft_offer_terms(dr, "a weekend workshop")
var d2: Dictionary = SimEngine.draft_offer_terms(dr, "a weekend workshop")
_ok(String(d1.unit) == "per session"
    and absf(float(d1.fair_price) - float(d2.fair_price)) < 0.01,
    "the keyless draft is seeded and repeatable")
_ok(float(d1.fair_price) >= 32.0 and float(d1.fair_price) <= 52.0,
    "an SMB draft prices inside the jittered band (40×[0.8,1.3])")
_ok((d1.variable_costs as Array).size() == 2
    and (d1.fixed_costs_wk as Array).size() == 1,
    "the draft itemizes: 2 variable lines + 1 fixed line")
```

**P6 — a conscious giveaway earns $0 and still costs to serve.**
```gdscript
var fr := _state(); fr.traction = 50; fr.set_flag("launched")
fr.offers = [{"name": "free tier", "unit": "per session", "price": 0.0,
    "price_set": true, "fair_price": 70.0, "unit_cost": 18.0, "weight": 1.0}]
var r_fr := SimEngine.weekly_tick(fr)
_ok(int(r_fr.revenue) == 0, "free on purpose earns $0")
var pnl_fr: Dictionary = fr.get_meta("pnl", {})
_ok(int(pnl_fr.cogs) >= 950 and int(pnl_fr.cogs) <= 1250,
    "the giveaway still pays COGS (~$1080 after giveaway-fueled adds)")
```
(Expected: giveaway demand ×2 pushes adds to the GTM cap ≈ 11–12, traction
50 → ~60–61, COGS = 60×18×1.0 ≈ 1080–1100.)

---

## 10. Engine-improvement suggestions (found while reading)

1. `price_offer`'s unmatched-name fallback (garage_view_screen.gd:2760 /
   WeekCommit.cs:~420) CREATES an offer with raw field writes — it bypasses
   every add_offer clamp and the shelf caps; route it through `add_offer`.
2. `runway_weeks` ignores COGS + catalog fixed (and hardcodes infra at 50):
   the health band overstates high-COGS companies (fixed in §3 here).
3. C# `OffersCogsPerCustomer` calls `LearningCurve` inside the per-offer
   loop (SimEngine.cs:1205); Godot hoists it — hoist in C# too.
4. The offer `unit` LLM enum lacks "per package"/"per kit", yet WorldGen
   births those exact units (premium package, accessories) — the review
   flow can never propose the shapes the world itself uses; add both to the
   enum (cadence already handles them at 0.2).
5. Godot dict-offer tests use shallow `duplicate()`; with nested line
   arrays this shares line objects between test states — standardize
   `duplicate(true)` for offers anywhere they carry lines.

---

## 11. Open questions for the owner

1. **Shelf caps**: era ladder 2/3/5/8/8 offers and Σweight ≤ 6.0 (my rec:
   yes — bounds the arpu exploit and the sheet) — or no count cap with
   weight-normalized arpu (cross-sell stops adding revenue, cannibalizes
   instead)? Changes D1 fundamentally.
2. **Birth offers**: should the flagship WorldGen offer carry one starter
   fixed line (~$15×audience/wk, "the tools that make it") so the catalog
   overhead lane is alive from week 1 (my rec: yes, flagship only) — or
   should the lane stay $0 until the founder adds an itemized offer?
3. **Dropping an offer**: instant removal with the two-press confirm (my
   rec — the revenue consequence is the natural cost), or a 2-week
   wind-down commitment ("customers on it churn out over two weeks") for
   offers that had paying volume?
