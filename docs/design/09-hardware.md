# 09 — HARDWARE PRODUCTION (Hardware runs only)

Bonopoly's loop scaled to a garage: you must BUILD what you sell. Capacity,
stock, stockouts, carrying cost, equipment, subcontracting. Deterministic,
seeded, clamped; zero LLM (the equipment catalog is authored). ENGINE owns
every number; the desk is a strip at the bottom of the binder's `product` tab.

Every mechanic is a scaled-down textbook manufacturing model, named in its
section (rough-cut capacity planning, periodic-review base-stock, inventory
holding cost, Wright's experience curve, make-vs-buy premium, constant-hazard
reliability, lost-sales fill rate); §12 is the ledger of what each
simplification drops and why the weekly tick can afford it.

**Active guard (exact):** every hardware entry point begins
`if state.biz_what != "Hardware": return <neutral>` (Godot) /
`if (state.BizWhat != "Hardware") return;` (Unity). On non-Hardware runs the
hardware state is NEVER allocated, no lane, no line, no roll — the tick is
byte-identical to today (pin P4).

---

## 1. State model

Godot (`game_state.gd`): one new var, mirroring `theta`/`budgets` style:

```gdscript
## HARDWARE (biz_what == "Hardware" only; {} on every other run).
## Seeded lazily by SimEngine.hw_state() on first Hardware touch.
var hardware: Dictionary = {}
```

Fields inside (all engine-written, all clamped):

| field | type | default | meaning |
|---|---|---|---|
| `stock` | int | 0 | finished units on the shelf, ≥ 0 |
| `capacity_base` | float | 6.0 | founder hand-assembly, units/wk |
| `equipment` | Array | [] | `[{id, name, capacity_add, upkeep_wk, bought_week}]`, max 12 |
| `production_target` | int | −1 | −1 = AUTO; else 0..capacity, player-set |
| `produced_total` | int | 0 | cumulative in-house units ever built (learning curve) |
| `subcontract_on` | bool | false | overflow toggle, era ≥ coworking |
| `demand_ema` | float | 0.0 | EMA(α=0.3) of true weekly unit demand |

`equipment` entries denormalize `capacity_add`/`upkeep_wk` at purchase time so
a later catalog rebalance never rewrites an owned machine (save-stable).

Unity (`GameState.cs`): typed, the `Budgets` idiom:

```csharp
public sealed class EquipmentItem {
    [JsonProperty("id")] public string Id = "";
    [JsonProperty("name")] public string Name = "";
    [JsonProperty("capacity_add")] public double CapacityAdd;
    [JsonProperty("upkeep_wk")] public double UpkeepWk;
    [JsonProperty("bought_week")] public int BoughtWeek;
}
public sealed class HardwareState {
    [JsonProperty("stock")] public int Stock;
    [JsonProperty("capacity_base")] public double CapacityBase = 6.0;
    [JsonProperty("equipment")] public List<EquipmentItem> Equipment = new();
    [JsonProperty("production_target")] public int ProductionTarget = -1;
    [JsonProperty("produced_total")] public int ProducedTotal;
    [JsonProperty("subcontract_on")] public bool SubcontractOn;
    [JsonProperty("demand_ema")] public double DemandEma;
}
[JsonProperty("hardware")] public HardwareState Hardware = null;  // null off-Hardware
```

**Save-compat (both engines, no version bump):** Godot adds
`"hardware": state.hardware` to the `save_run` doc; the generic
`if k in state: state.set(k, sd[k])` load path restores it; an old save lacks
the key → default `{}` → `hw_state()` re-seeds on the next Hardware tick.
Unity: Newtonsoft leaves `Hardware` null when the key is absent; the guard +
lazy seed handles it. `SaveSystem.VERSION` stays 2 — a bump would orphan every
existing slot (`!= VERSION` → `{}`), and this change is purely additive.

**Do not park durable hardware state in Object meta**: Godot meta is not in
the save whitelist (Unity's `Meta` is) — first-class fields only. Transient
per-week display data (`hw` report block) MAY use meta, same contract as `pnl`.

Lazy seed (only place allocation happens):

```gdscript
static func hw_state(state: GameState) -> Dictionary:
    # caller already guarded biz_what == "Hardware"
    if state.hardware.is_empty():
        state.hardware = {"stock": 0, "capacity_base": 6.0, "equipment": [],
            "production_target": -1, "produced_total": 0,
            "subcontract_on": false, "demand_ema": 0.0}
    return state.hardware
```

---

## 2. The flagship binding (what a "unit" is)

Production builds the **flagship unit**: the first offer in `state.offers`
whose cadence is "per unit" (`offer_cadence(unit) == 0.2` — unit/device/kit/
package/box). No stored index, no dangling reference when `remove_offer`
shifts the array — a pure selector, recomputed wherever needed:

```gdscript
static func hw_flagship_index(state: GameState) -> int:
    for i in state.offers.size():
        if is_equal_approx(offer_cadence(String((state.offers[i] as Dictionary).get("unit", ""))), 0.2):
            return i
    return 0 if state.offers.size() > 0 else -1
```

- Flagship `unit_cost` (= Σ variable `cost_lines`, catalog-owned) is the
  production cost basis; flagship `fair_price`/`price` stay catalog-owned.
- `-1` (no offers at all, legacy runs): fallback unit cost
  `1.75 * th.arpu_wk` (≈ 0.35 margin share of the per-unit price implied by
  weekly arpu at 0.2 cadence), fallback billed price `th.arpu_wk / 0.2`.
- Non-flagship offers (accessories, subscriptions, service plans) are NOT
  produced — they keep today's serve-time cogs path untouched.
- Desk labels the flagship row so the binding is visible, never guessed.

---

## 3. Capacity math

Real analogue: rough-cut capacity planning — available output = rated machine
capacity + direct labor, in units per period. One aggregate resource pool, no
routing, because the weekly tick and single flagship SKU need exactly one
honest number: units/wk.

```
capacity_wk = capacity_base                                  # 6.0 founder hands
            + Σ equipment[i].capacity_add                    # minus this week's broken machine
            + HW_OPS_UNITS × Σ over employees with role containing "ops":
                  (1.0 + 0.25 × (clamp(skill,1,5) − 3))      # skill read e.get("skill", 3)
```

Constants: `HW_OPS_UNITS := 10.0` → an ops hire adds 5..15 units/wk by skill,
10 while the labor market has no skill field yet (default 3 = neutral —
forward-compatible with subsystem 2, read-only coordination: labor owns the
roster, we only read `role.contains("ops")`).

Units built this week = `clampi(target_now, 0, int(floor(capacity_wk)))` where
`target_now` is the stepper value, or AUTO when `production_target == -1`:

```
auto = clampi(int(round(HW_AUTO_COVER_WK × maxf(demand_ema, HW_AUTO_DEMAND_FLOOR))) − stock, 0, capacity)
auto = mini(auto, int(floor(0.25 × maxf(cash, 0) / unit_cost_eff)))   # AUTO never spends >25% of cash
```

`HW_AUTO_COVER_WK := 4.0`, `HW_AUTO_DEMAND_FLOOR := 2.0`. AUTO is a textbook
periodic-review base-stock policy — review period R = 1 week, order-up-to
level S = 4 weeks of forecast demand — with the forecast being plain
exponential smoothing (`demand_ema`, α = 0.3), the same pair every MRP system
runs on. It holds ~4 weeks of cover so a founder who never opens the strip
neither starves the shelf nor drowns in carrying; the manual stepper is
uncapped by cash (going red is a choice the reaper already prices). Building
less than capacity is always allowed — idle capacity costs only its upkeep,
and the strip prints the resulting **capacity utilization** (built/capacity)
so the cost of idleness has a name.

---

## 4. Weekly tick integration — PRODUCE BEFORE ADOPTION

New step **7.5** in `weekly_tick`, between the market-mood walk (7) and
adoption (8); serve happens inside 8; the money lands in 9.

**Why produce-first:** (a) no dead week — week-1 stock would otherwise be 0
and the launch's first adds lost before any decision existed; (b) it is
Bonopoly's plan→produce→sell weekly molecule; (c) cause and effect stay inside
one report: the target you set this week serves this week's demand. Stockouts
still bite whenever demand > capacity + stock — that is the intended tension,
not a scheduling artifact.

```gdscript
# 7.5 ── hardware: the bench builds (Hardware runs only)
if state.biz_what == "Hardware":
    var hw := hw_state(state)
    var r95 := _rng(state, 95)                       # NEW SALT 95 — hardware stream
    # breakdown roll first, fixed draw order (replay-exact):
    var down_i := -1
    var eq: Array = hw.equipment
    if eq.size() > 0 and r95.randf() < minf(0.02 * float(eq.size()), 0.15):
        down_i = r95.randi_range(0, eq.size() - 1)   # that machine idles this week
    var capacity := hw_capacity(state, down_i)       # section 3, skipping down_i
    var unit_cost_eff := hw_unit_cost(state) * hw_learning(state)
    var built := hw_target_now(state, capacity, unit_cost_eff)   # stepper or AUTO
    hw.stock = int(hw.stock) + built
    hw.produced_total = int(hw.produced_total) + built
    # costs remembered for step 9: production, upkeep (+ repair 4×upkeep_wk if down)
```

Inside step 8, after `adds = minf(adds, gtm_cap)` (GTM caps first — you cannot
sell from a shelf faster than the team can close):

```gdscript
if state.biz_what == "Hardware":
    var U_exist := _seeded_int(A * 0.2, r95)         # existing repurchases, salt-95 remainder
    var served := mini(U_exist, hw.stock)
    hw.stock -= served
    var fill := 1.0 if U_exist == 0 else float(served) / float(U_exist)
    var adds_raw := adds
    adds = minf(adds, float(hw.stock))               # a new customer NEEDS a unit
    hw_lost = int(round(adds_raw - adds))            # receipted
    churn *= 1.0 + HW_STARVE_CHURN * (1.0 - fill)    # empty shelves push people out
    hw.demand_ema = 0.7 * float(hw.demand_ema) + 0.3 * (float(U_exist) + adds_raw)
# ... existing net landing (salt 91) unchanged; then:
#     hw.stock -= mini(int(round(adds)), hw.stock)   # sold first units leave the shelf
```

Abstraction stated plainly: a new customer's first unit ships at signup (whole
unit off the shelf); billing stays the catalog's smoothed
`price × 0.2 × traction` ARPU-week. The shelf moves whole units, the books
bill cadence — divergence ≤ 1 unit/wk from rounding, stock clamped ≥ 0.

**Honest billing when repeat buyers go unserved:** step 9 subtracts
`lost_billing = flagship_billed_price × 0.2 × A × (1.0 − fill)` from revenue
with the line `"unserved repeat buyers: −$X"` — nobody is invoiced for a unit
that never shipped (owner's law #196).

Salt discipline: **95** is the hardware stream (proposed here; 4, 5, 6, 7, 9,
77, 88, 91, 93 are taken). Fixed draw order per tick: ① breakdown randf,
② breakdown pick (only on hit), ③ existing-demand remainder. Salt 93's
"unforeseen" keeps its own stream — no collision, no shared draws; its
"broken machine on a payment plan" flavor line is unrelated to equipment
entries (never mutates `hardware`).

---

## 5. Production cost, cogs, and the learning curve

- **Production spend (step 9):** `built × unit_cost_eff` joins burn on the new
  `production` lane. `unit_cost_eff = flagship.unit_cost × hw_learning(state)`.
- **No double count:** on Hardware runs `offers_cogs_per_customer` SKIPS the
  flagship index — its variable cost is paid at build time, once. All other
  offers keep the existing served-based cogs path and its `learning_curve`.
- **`hw_learning` — Wright's law on cumulative units BUILT, same shape as the
  serving curve for twin parity:**

```gdscript
static func hw_learning(state: GameState) -> float:
    var made := int(hw_state(state).produced_total)
    if made <= 1: return 1.0
    return maxf(1.0 - 0.115 * (log(float(made)) / log(10.0)), 0.65)
```

Real analogue: Wright's 1936 experience curve — unit cost falls a fixed
fraction per **doubling of cumulative output**. The canonical form is the
power law `C(N) = C₁·N^−b` (aerospace 80–85% curves ≈ 15–20% per doubling);
ours is its linear-in-log approximation, −11.5 pts per 10× ≈ **−3.5% per
doubling**, deliberately gentler than aerospace because a garage builds one
product with bought parts, and floored at **0.65** because learning compresses
labor/assembly/yield, never the purchased BOM — the floor IS the material
share. The receipt teaches it by name: `"learning curve: unit cost −11%
(1,000 built — practice makes cheaper)"`.

**Decision — produced units do NOT feed `served_total`:** the factory learns
by building (Wright's law counts cumulative units produced, stock included),
not by counting customers; `served_total` stays the catalog's serving counter
for every other offer. No double-dip is possible because the flagship is
excluded from serve-time cogs — each discount applies to exactly one cost.
Subcontracted units feed **neither** counter: outsourcing teaches your bench
nothing (the CM keeps its own learning), which makes in-house-at-a-premium a
real long-game choice — the make-vs-buy trade stated honestly.

---

## 6. Carrying cost

End-of-week stock (units that actually sit into next week — freshly built and
sold units never bill):

```
carrying = stock_end × maxf(HW_CARRY_RATE × flagship.unit_cost, HW_CARRY_MIN)
HW_CARRY_RATE := 0.02      # 2% of unit cost per week held
HW_CARRY_MIN  := 0.10      # $0.10/unit/wk floor (cheap gadgets still need shelves)
```

Real analogue: inventory holding cost — capital tied up + storage + insurance
+ shrinkage + **obsolescence**, the sum practitioners price at 20–30%/yr of
unit value for durable goods. Ours is 2%/wk ≈ 104%/yr: deliberately ~4× the
warehouse textbook because (a) a run compresses years into weeks and the
pain must register inside a season, and (b) startup hardware sits at the
obsolescence-heavy end (consumer electronics devalue 1–2%/wk on their own).
This is the holding half of the EOQ trade-off; the setup-cost half is dropped
(§12) so there is no batch-size game, only how-much-cover.

Joins burn on the `carrying` lane; the receipt explains itself:
`"carrying %d units: −$%d (2%%/wk of unit cost — money parked on shelves)"`.
**Overstock warning** (bang + coach line) when
`stock > 8.0 × maxf(demand_ema, 1.0) and stock > 20`.

---

## 7. Equipment — the authored catalog

`const HW_EQUIPMENT` in SimEngine (authored pool, LLM never touches it), era
gates honor `era_spend_cap` (garage 6k / coworking 25k / office 80k /
floor 300k / hq 1.2M):

| id | name | era gate | price | capacity_add | upkeep_wk |
|---|---|---|---:|---:|---:|
| `jig` | Assembly Jig | garage | $900 | +6 | $15 |
| `pick_place` | Benchtop Pick-and-Place | coworking | $3,500 | +18 | $60 |
| `reflow` | Reflow Oven Line | coworking | $12,000 | +45 | $180 |
| `cnc` | CNC Cell | office | $45,000 | +140 | $600 |
| `line` | Assembly Line | floor | $180,000 | +450 | $2,200 |
| `lightsout` | Lights-Out Cell | hq | $700,000 | +1,500 | $7,000 |

Real analogue: capacity is bought in **lumps** (stepwise capacity expansion —
you cannot buy 3% of a reflow oven), upkeep ≈ 1.6%/wk of price is the
maintenance-budget rule of thumb (~15–20%/yr of asset value at game
compression), and $/unit of capacity improves with scale — economies of scale
in capex, the Bonopoly ladder. A jig is hand-assembly tooling, so the garage
gets it; everything with a power cord waits for a real tenancy (§9).
Duplicates allowed (two Jigs = +12), 12 machines max.

**Purchase** (desk buy row → engine, world-clamped):

```gdscript
static func hw_buy_equipment(state: GameState, id: String) -> Dictionary:
    # refuses: non-Hardware run · unknown id · era below gate ·
    #          price > era_spend_cap(state.era) · cash < price · 12 owned
    # accepts: cash -= price; equipment.append({id, name, capacity_add,
    #          upkeep_wk, bought_week: state.week}); log_action(...)
    # returns {ok: bool, why: String}
```

One-off cash out; `Σ upkeep_wk` joins burn every week on the `equip_upkeep`
lane (fixed cost — idle machines still cost).

**Breakdown (salt 95, section 4):** weekly chance `0.02 × machines` (cap
0.15); the picked machine contributes 0 capacity this week and bills a repair
of `4 × its upkeep_wk` into `equip_upkeep`; line
`"machine down: NAME (repair −$X)"`; bang on `product`. One week, then it
runs again — no repair queue state. `capacity_base` never breaks (hands
don't; machines do). Real analogue: the constant-hazard (exponential)
reliability model — memoryless failure at MTBF ≈ 50 machine-weeks, MTTR
floored at one tick, corrective repair priced at ~a month of that machine's
preventive-maintenance budget. Risk scales with fleet size exactly as it
should: more machines, more failures — and one giant machine concentrates
exposure (§9, hq).

---

## 8. Subcontracting (Bonopoly overflow) — make vs buy

Real analogue: the make-vs-buy decision. A contract manufacturer's quote is
your marginal cost plus THEIR margin, overhead and transaction costs — the
premium buys zero capex and zero commitment, exactly like renting capacity
spot instead of owning it. Relationship and committed volume narrow the
premium at scale; a jobber never books unlimited line time for a small
client. All of that becomes three era-laddered numbers:

Unlocked from **coworking** era (`era_index() >= 1`), desk toggle
`subcontract_on`. When ON, after in-house serving leaves demand short:

```
sub_units = min(unserved_units_this_week, int(HW_SUB_CAP_X[era] × capacity_wk))
sub_cost  = sub_units × HW_SUB_MULT[era] × flagship.unit_cost
HW_SUB_CAP_X := {coworking: 1.0, office: 3.0, floor: 3.0, hq: 3.0}
HW_SUB_MULT  := {coworking: 1.6, office: 1.6, floor: 1.45, hq: 1.35}
```

- coworking = a local jobber takes small overflow (spot rate 1.6×, at most
  1× your own footprint); office = a real CM relationship (3× footprint);
  floor/hq = supplier contract terms — committed volume prices the premium
  down to 1.45× then 1.35× (§9). Zero extra state: the era IS the
  relationship maturity.
- Made-to-order: sub units serve existing repurchases first, then adds; they
  **never enter stock** and never bill carrying.
- **No learning curve** on the multiplier (the sub's price is the sub's
  price) and no `produced_total` accrual (section 5).
- Cap ×footprint: a jobber won't scale past a client's own capacity —
  equipment stays the growth spine, the toggle buys slack, not the game.
- Lane `subcontract`; the receipt teaches the trade by name:
  `"make vs buy: subcontracted %d units −$%d (1.6× unit cost — their margin,
  your sale, none of your learning)"`.

Margin truth at defaults: unit_cost = 0.35 × fair → sub cogs 0.56 × fair —
still positive at fair price, visibly worse; the desk prints both margins.

---

## 9. Scaling by stage — what each era unlocks, exactly

The era ladder is the manufacturing growth story told in numbers. Nothing
here is new state — it is the equipment gates (§7), the sub ladder (§8) and
the fleet's own arithmetic, laid end to end:

| era | the story | exact unlocks & ceilings |
|---|---|---|
| **garage** | hand-assembly | base 6/wk + Assembly Jigs ($900, +6) only; no subcontracting; learning ≈ 1.0 → unit cost at its highest; practical ceiling ~18/wk (base + 2 jigs), staff cap 2 leaves ~no ops headroom |
| **coworking** | first equipment | Pick-and-Place ($3.5k, +18) and Reflow Line ($12k, +45) purchasable; spot subcontracting unlocks (1.6×, cap 1× footprint); ceiling ~87/wk owned + as much again subbed |
| **office** | a real line | CNC Cell ($45k, +140); CM relationship: sub cap 3× footprint at 1.6×; ops hires start mattering (staff cap 9); ceiling ~300/wk owned, ~1,200/wk with subbing |
| **floor** | second line, automation | Assembly Line ($180k, +450), duplicates = literally a second line; supplier contract terms: sub rate 1.45×; carrying and upkeep become real lanes (a two-line floor bills ~$4.4k/wk upkeep before payroll) |
| **hq** | scale manufacturing, scale risks | Lights-Out Cell ($700k, +1,500); sub rate 1.35×; the risks of scale arrive by arithmetic: a big fleet fails somewhere most months (breakdown p → 0.15 cap), a downed Lights-Out drops 1,500 units in one week and repairs at $28k, and overstock at hq volumes makes 2%/wk carrying a five-figure lane |

The strip never says "you leveled up" — the era gates simply light new rows,
and the receipts price the new scale honestly.

## 10. Feeds — P&L lanes, report, DM

**P&L lanes** (keys added to the `pnl` meta dict ONLY when hw_active; absent
otherwise — pin P4): `production`, `subcontract`, `equip_upkeep` (upkeep +
repair), `carrying`. All four join `burn`. The ledger tab renders them as
extra lanes when present.

**Report lines** (journal receipts — each carries its WHY in one clause, the
teaching voice): `"built %d units at $%s each (utilization %d%%)"` /
make-vs-buy (§8) / carrying (§6) / learning (§5) /
`"STOCKOUT — %d sales lost (demand %d, shelf %d): add capacity or
subcontract"` / `"−%d customers walked (fill rate %d%% — repeat buyers found
empty shelves)"` / machine-down (§7) / unserved-billing (§4).
`rep["hw"] = {built, sub_units, lost_adds, fill_pct, stock_end, capacity,
utilization_pct, down_name}` and mirrored to `set_meta("hw", ...)` for the
desk (same transient contract as `pnl`).

**DM context:** `signals()` gains, only when hw_active:
`"hardware": "stock %d · capacity %d/wk · lost %d to stockout · fill %d%%"` —
the DM narrates factory pain but never invents a number.

---

## 11. Desk — the production strip (binder `product` tab)

Bottom band of the sheet (y ≈ 600–850), a self-contained framed strip titled
**THE BENCH** — the roadmap desk (subsystem 7) owns the tab's upper region;
nothing of ours renders outside the frame, and on non-Hardware runs the strip
does not exist (tab pixel-identical to today).

```
┌ THE BENCH — building: Pocket Synth ─────────────────────────────────────┐
│ stock: 34 units   capacity: 24/wk   utilization: 79%   demand ≈ 19/wk   │
│ build: [−] 19 [+] [AUTO]  unit cost $16.02 (base $18.00, learning −11%) │
│ machines: Assembly Jig ×2 (+12, $30/wk) · Pick-and-Place (+18, $60/wk)  │
│ buy: [Reflow Oven Line  $12,000  +45/wk  $180/wk upkeep]   (era-gated)  │
│ make vs buy — overflow to a contract mfr at 1.6×: [ON/off]  fill: 100%  │
│ carrying cost: $12/wk on 34 units (2%/wk of unit cost)                  │
└─────────────────────────────────────────────────────────────────────────┘
```

The strip is a working vocabulary lesson — it prints the real terms on the
real numbers, never a tooltip essay: **capacity utilization** (built/capacity
%), **carrying cost** ($/wk, with its rate), **learning curve** (current
discount + units built), **make vs buy** (the toggle's own label), **fill
rate** (served/demanded last week). Every receipt in the journal carries its
WHY in one clause (§10 line texts) — the strip shows the state, the receipt
explains the consequence, and both use the same words.

- Stepper writes `production_target` (0..capacity, AUTO button → −1);
  world-clamped in the engine, never trusted from the UI.
- Buy row shows the next era-legal machines; unaffordable/gated rows render
  dimmed with the reason (the engine's `why` string).
- Flagship row is named in the header, so the binding is visible.

**Bangs on the `product` tab** (binder `_bangs` wiring, hw_active only):
stockout last week (`lost_adds > 0`) · overstock (section 6 condition) ·
machine down (`down_name != ""`).

## 12. Simplifications ledger — what each model drops, and why that holds

| mechanic | dropped from the real thing | why acceptable at weekly ticks |
|---|---|---|
| capacity (§3) | routings, shifts, OEE, changeovers | one SKU, one aggregate resource; a second number would teach nothing new |
| AUTO (§3) | safety-stock service-level math (z·σ) | 4-wk cover approximates it; the stepper exists for players who want the real decision |
| production (§4) | lead times, WIP — build completes same week | tick = 1 week ≥ garage assembly lead time; sub lead time absorbed the same way |
| stockout (§4) | backorders — all unmet demand is lost sales | consumer hardware IS lost-sales retail; Enterprise backorder patience is folded into the churn-pain constant instead |
| billing (§4) | invoice-per-unit — books bill smoothed ARPU-weeks while the shelf moves whole units | catalog owns revenue; divergence ≤ 1 unit/wk, and unserved repeat buyers are explicitly un-billed |
| learning (§5) | Wright power law → linear-in-log, −3.5%/doubling, floor 0.65 | gentler than aerospace (bought BOM doesn't learn); floor = material share; monotone and clamped either way |
| carrying (§6) | EOQ's setup-cost half; 20–30%/yr → 104%/yr | no batching exists to optimize; rate compressed to bite within a run and priced like obsolescence-heavy electronics |
| breakdown (§7) | aging/wear curves, repair queues | constant-hazard exponential model is a legitimate reliability baseline; MTTR = 1 tick keeps state at zero |
| make-vs-buy (§8) | NRE, MOQs, qualification lead time | folded into the era gates (relationship maturity) and the flat premium ladder |

## 13. LLM leverage

**None — pure math over an authored equipment catalog.** The DM narrates
factory pain from `rep` lines and `signals().hardware` riding the EXISTING
adjudication calls — zero new calls, zero LLM-owned numbers. That is the
whole answer, kept deliberately.

Tempting but wrong:
- *LLM-flavored machine names per company* ("the Pocket Synth reflow line") —
  the catalog must stay typed and stable for era gates, save-compat and twin
  parity; the flagship offer's name already carries the fiction into the
  strip header for free.
- *LLM-tuned capacity/costs from the pitch* ("drones need different
  machines") — numbers are ENGINE-owned; theta already scales worlds; the
  STATUS lesson stands: no untyped modifiers, ever.
- *LLM breakdown drama picking severity or duration* — severity is a number;
  the DM gets the receipt and writes the sentence, never the other way.
- *LLM demand forecasting for AUTO* — the EMA is deterministic, replayable
  and explainable on the strip; a model forecast breaks replay and smuggles
  numbers past the clamps.

---

## 14. Twin test pins (both suites, same numbers)

1. **Stockout caps adds:** Hardware run, stock 0, capacity 0, target 0, sub
   off, demand present → `rep.adds == 0`, `rep.hw.lost_adds > 0`, traction
   never rises; churn multiplier ≥ 1 (fill 0 → ×1.35 exact).
2. **Carrying bills:** stock_end 50, flagship unit_cost 20 →
   `pnl.carrying == 20` (50 × 0.02 × 20) and `burn` includes it.
3. **Subcontract margin math:** capacity 5, demand 30, sub on, era office →
   `sub_units == min(25, 15) == 15`; `pnl.subcontract == 15 × 1.6 ×
   unit_cost` with NO learning discount; sub units never enter `stock`.
   Same state at era coworking → `sub_units == 5` (1× footprint cap); at
   era hq the multiplier reads 1.35.
4. **Non-Hardware untouched:** Software run tick → `state.hardware == {}` /
   `Hardware == null`, `pnl` has none of the four hw keys, no hw report
   lines, and net matches the pre-change baseline byte-for-byte.
5. **Determinism:** same seed+week Hardware tick run twice → identical
   `stock`, `produced_total`, all four lanes (salt-95 stream isolated).
6. **Learning curve:** `produced_total == 10` → `hw_learning == 0.885`
   (1 − 0.115·log10 10) applied to production spend; era gate: `hw_buy_
   equipment` refuses `cnc` in garage with ok=false, cash unchanged.

## 15. Engine-improvement suggestions

1. Godot parks `served_total` in Object meta, which the save whitelist skips
   (Unity persists `Meta`) — the serving learning curve silently resets on
   Godot load. Promote to a first-class saved field in both engines.
2. Surface the constraint chain in the adds receipt ("demand 120 → GTM 80 →
   stock 40") — gtm_cap, stock, and price each cap adds invisibly today.
3. `infra = 50 + 0.05 × traction` reads odd for Hardware (warehouse, not
   servers) — consider a `biz_what` flavor split so the lane names stay true.
4. Let a purchased machine grant `adv` on `build` rolls via `roll_context`
   (items already bend traits) — equipment is currently invisible to dice.
5. Unify the two cumulative-counter learning curves behind one helper taking
   `(count) -> mult` so the constants can never drift between them.

## 16. Open questions

1. **Multi-SKU factories:** v1 binds ONE flagship (first unit-cadence offer);
   a founder selling two devices shares the pool implicitly. Per-SKU stock is
   a real Bonopoly-scale feature — later wave, or never?
2. **Service capacity:** consulting hours are also finite capacity; should
   Service runs eventually reuse this molecule (renamed, no stock), or stay
   uncapped by design?
3. **Resale/disposal:** no selling machines in v1 (Bonopoly allows disposal).
   Add a 50%-of-price resale on demotion or cash crunch, or keep purchases
   irreversible as a commitment mechanic?

## INTERFACE DELTA

Every player-facing change this lane needs. All rows are **Hardware runs
only**; on every other run every surface below is pixel- and text-identical
to today (that absence is itself a tested state, pin P4).

| surface | exists today? | CHANGE or ADD | exactly how (content, controls, position, states) | why the player needs it |
|---|---|---|---|---|
| binder `product` tab — THE BENCH strip frame | no | ADD | framed strip in the bottom band (y ≈ 600–850), header `THE BENCH — building: <flagship name> ($<unit cost>/unit)`; absent on non-Hardware runs; upper tab region untouched (roadmap lane owns it) | one glance = what the factory builds and what one unit costs; the frame keeps two lanes from colliding on one tab |
| BENCH row 1 — status line | no | ADD | `stock: N units · capacity: N/wk · utilization: N% · demand ≈ N/wk`; utilization reddens (PEN color) below 50% and at 100%; demand is the EMA forecast | the four numbers every production decision needs, with utilization named so idle capacity has a word |
| BENCH row 2 — build target stepper | no | ADD | `build: [−] N [+] [AUTO]`; − / + step 1 (hold = 5), clamped 0..capacity; AUTO button sets −1 (label shows `AUTO (N)` with this week's computed target); engine re-clamps, UI never trusted | the core weekly lever: how many units to build; AUTO is the safe hands-off default |
| BENCH row 2b — unit cost + learning read | no | ADD | `unit cost $16.02 (base $18.00, learning curve −11%, 1,000 built)`; updates after each tick | shows practice making units cheaper, by name — the reason to build in-house |
| BENCH row 3 — machines owned line | no | ADD | `machines: <Name> ×n (+cap, $upkeep/wk) · …`; a downed machine renders struck-through with `DOWN` for that week | what the fleet contributes and costs weekly; makes a breakdown visible where it happened |
| BENCH row 4 — equipment buy row | no | ADD | next era-legal catalog machines as buttons: `[Reflow Oven Line $12,000 +45/wk $180/wk upkeep]`; unaffordable or era-gated rows dimmed with the engine's refusal reason; tap = one-off purchase, cash out | growth path: capacity is bought in lumps; the dimmed reason teaches the gate instead of hiding the button |
| BENCH row 5 — make-vs-buy toggle | no | ADD | `make vs buy — overflow to a contract mfr at <1.6×/1.45×/1.35×>: [ON/off] · fill: N%`; hidden in garage era, appears from coworking with the era's multiplier | the classic overflow decision, priced; fill rate shows what the toggle would have saved |
| BENCH row 6 — carrying cost line | no | ADD | `carrying cost: $N/wk on N units (2%/wk of unit cost)`; reddens when the overstock condition holds | money parked on shelves must be visible weekly or stockpiling looks free |
| binder tab bang — `product` | bang system yes, product bang no | ADD | coral `!` on the product tab while: stockout last week OR overstock (stock > 8× demand and > 20) OR machine down; same `_bangs` wiring as pricing/ledger | pulls the player to the strip exactly when the factory needs a decision |
| binder `the ledger` tab — P&L lanes | yes | CHANGE | four lanes appended when present in `pnl`: `production`, `subcontract`, `equip_upkeep`, `carrying`; rendered in the existing lane list style; absent keys render nothing | the factory's money must sit in the same honest ledger as every other dollar |
| journal — production receipt | no | ADD | `built 19 units at $16.02 each (utilization 79%)` | the week's build, priced, with utilization named |
| journal — stockout receipts | no | ADD | `STOCKOUT — 12 sales lost (demand 31, shelf 19): add capacity or subcontract` and `−3 customers walked (fill rate 78% — repeat buyers found empty shelves)` and `unserved repeat buyers: −$X` | lost demand needs a receipt with a cause and a next move, and un-shipped units must visibly un-bill |
| journal — make-vs-buy receipt | no | ADD | `make vs buy: subcontracted 15 units −$432 (1.6× unit cost — their margin, your sale, none of your learning)` | prices the premium and states what outsourcing does NOT buy (learning) |
| journal — carrying receipt | no | ADD | `carrying 34 units: −$12 (2%/wk of unit cost — money parked on shelves)` | the WHY clause turns a fee into a lesson |
| journal — machine-down receipt | no | ADD | `machine down: Reflow Oven Line (repair −$720)` | breakdowns must be receipted, never mysterious |
| journal — learning milestone receipt | no | ADD | on each new whole −1% step: `learning curve: unit cost −11% (1,000 built — practice makes cheaper)` | progress on the experience curve, celebrated by name, without weekly spam |
| coach lines | yes | CHANGE | four authored lines added to the coach pool, fired by the bang conditions + idle bench: stockout ("empty shelves are a pricing and capacity problem, not bad luck"), overstock, machine down, utilization < 50% | first-run players need the vocabulary handed to them once, in plain words |
| DM context (`signals()`) | yes | CHANGE | adds `hardware: "stock N · capacity N/wk · lost N to stockout · fill N%"` only when hw_active | the DM narrates factory pain truthfully without inventing a number |
| scene rooms / HUD | yes | NO CHANGE | — | production lives entirely on the binder's product tab; the room stays the DM's stage |
