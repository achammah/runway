# 05 — THE ENTERPRISE PIPELINE (named leads)

Desk: `customers`, Enterprise runs only. Plan §5, Wave D. House pattern:
deterministic core → one rare batch naming call → binder desk → feeds.
LAW: the ENGINE owns every number; the DM owns sentences and heat, never seats,
stages, or closes.

This subsystem is a teaching instrument first: it makes the player live the
real mechanics of enterprise sales — stage-gated pipelines, win rate, sales
cycle, ACV, no-decision deaths, procurement, renewals, land-and-expand — with
each concept NAMED in the receipts the week it happens.

Activation gate (both engines):

```
enterprise_pipeline_active(state) := state.biz_who == "Enterprise"
```

Non-Enterprise runs are byte-identical to today (twin-pinned, §12).

---

## 1. State model

### 1.1 New GameState fields

| field | type | default | meaning |
|---|---|---|---|
| `leads` | Array of Lead | `[]` | live named prospects |
| `logos` | Array of Logo | `[]` | signed accounts (their seats live inside `traction`) |
| `pipe_units` | float | `0.0` | demand pool: seats of interest not yet attached to a named lead |
| `pipe_churn_acc` | float | `0.0` | fractional account-churn accumulator |
| `pipe_stats` | Dictionary | `{}` | `{signed:int, lost:int, cycle_sum:int, seats_signed:int, spend:float, first_wk:int}` |

Lead (JSON dict / C# sealed class, keys byte-identical):

```
{ "name": String(≤30), "flavor": String(≤90, "" keyless),
  "seats": int(3..120), "stage": "meeting"|"pilot"|"procurement"|"contract",
  "age_weeks": int, "heat": int(0..100) }
```

Logo:

```
{ "name": String(≤30), "seats": int, "since_wk": int, "renewal_wk": int }
```
`renewal_wk` is 0 until the run reaches `floor` era (§6).

`stage` extends the plan's three-stage enum with a conditional fourth,
`procurement`, per the era ladder (§8) — it can only ever appear at
office+ on deals ≥ 20 seats, so early-game saves never contain it.

### 1.2 Save compatibility

- **Godot**: five new vars on `GameState` with the defaults above; five new keys
  in `SaveSystem.save_run`'s state dict. The loader is `for k in sd: if k in
  state: state.set(k, sd[k])` — an old save simply leaves the defaults (empty
  pipeline, pool 0), and an old build ignores the new keys. No version bump.
- **Unity**: `Lead` / `Logo` sealed classes with `[JsonProperty]` names matching
  the Godot keys exactly; `GameState` gains `[JsonProperty("leads")] List<Lead>
  Leads = new()` etc. `RunSave` serializes the whole state object, so the
  fields are saved the day they exist. Field initializers are the old-save
  defaults.
- A **legacy mid-run Enterprise save** resumes with its existing `traction` as
  *loose units* (§6) and an empty board; the pipeline simply starts flowing
  from that week. Nothing converts, nothing breaks (open question 1).

### 1.3 Caps and constants (all engine-side, all clamped)

```
LEAD_CAP        = 8          # live leads max — a real AE runs ~10-15 open
                             # opportunities; a founder juggling everything, fewer
SPAWNS_PER_WK   = 2          # keeps the naming batch small and rare
MIN_SEATS       = 3
POOL_CAP        = 0.25*tam   # 1000 units at default Enterprise tam 4000
HEAT_SPAWN      = 50..65     # seeded uniform int at spawn
HEAT_DECAY      = 8          # per week (−1 per sales head at floor/hq, max −3, floor 4)
HEAT_ADVANCE    = +25        # momentum on a stage advance
PUSH_CLAMP      = ±40        # push_lead v clamp
BASE_ADV        = {meeting:0.45, pilot:0.35, procurement:0.35, contract:0.40}
P_ADV_CLAMP     = 0.05..0.85
PROCUREMENT_SEATS = 20
RENEW_EVERY     = 26         # weeks (floor/hq): the annual-contract cliff
SALT_PIPE       = 95         # all pipeline mechanics rolls (unused today; existing: 4,5,6,7,9,77,88,91,93)
SALT_PIPE_NAME  = 96         # keyless name draws only — name-length draws must never
                             # shift a mechanics roll
```

---

## 2. Reconciliation with the Bass adoption block

**Decision: fractional adds become POOL units, not lead-spawns, and every seat
in every named account is a seat that came out of that pool.** Conservation is
exact: the pipeline re-times and re-chunks the same demand the Bass block
already generates; it never invents or destroys market.

In §8 of `weekly_tick`, the Enterprise branch:

1. Computes `adds` exactly as today — hype, marketing reach, statuses, market
   trend, rival pressure, quality gate, launch gate, price demand all apply —
   **but skips the GTM-capacity min()**. Demand generation (meetings booked)
   is marketing's job; closing capacity moves to stage advance (§4), where the
   `gtm_cap` formula becomes the capacity input `C`. Keeping the cap in both
   places would tax sales twice.
   *Real analogue*: pipeline generation vs. quota capacity are separate
   functions (marketing/SDR vs. AE) in every B2B org; "demand is not closing"
   is already this engine's own comment.
2. `pipe_units = min(pipe_units + adds, POOL_CAP)` — the pool holds fractional
   seats of unattached interest.
3. **Skips** `net = adds − churn` and the salt-91 remainder entirely. Traction
   now only moves through: closes (+seats), account churn/renewal (−seats),
   loose-unit churn (§6), and DM `traction_delta` side-sales.
4. A **spawn** (§3) draws a lead and debits its full `seats` from the pool.
   A lead that **dies cold refunds its seats to the pool** (clamped at
   POOL_CAP): the demand goes back to the street; what the founder lost is
   *time* — weeks of unearned revenue at ~$400/seat/wk — not the market
   itself. In steady state, throughput out of the pipeline equals `adds`
   exactly, so the tuned Enterprise growth curve (tam 4000, adopt_p 0.00018,
   lifetime 90) survives untouched; the pipeline adds delay, lumpiness and
   player agency on top.
   *Real analogue*: ~40-60% of forecasted B2B deals are lost to **no
   decision** — the prospect stays in-market and re-enters someone's pipeline
   later. Full refund models exactly that; a "half refund" alternative was
   rejected because it silently rebases long-run growth to ~0.7× the pinned
   Bass curve.
5. The **seeded-remainder idiom survives at the edges it belongs to**: the pool
   itself stays float (no rounding at all — strictly better than a remainder
   coin), and loose-unit churn keeps the floor + seeded-coin conversion, on
   the salt-95 stream (salt 91 remains untouched and non-Enterprise-only, so
   existing replays and pins never shift).

Rejected alternative, for the record: "every whole add spawns one lead of 3-60
seats" multiplies demand by mean-seats × close-rate ≈ 5-8× and breaks every
Enterprise balance pin. Named in the doc so nobody re-invents it.

---

## 3. Spawning named leads

Runs inside the tick (§9 order below), on the salt-95 stream, after advances:

```
spawned = 0
while spawned < SPAWNS_PER_WK and leads.size() < LEAD_CAP and pipe_units >= MIN_SEATS:
    tier   = seeded tier draw (era table, §8)
    seats  = seeded uniform int inside tier, then clampi(seats, MIN_SEATS,
             maxi(MIN_SEATS, int(floor(pipe_units))))      # a big logo only spawns
    pipe_units = maxf(pipe_units - seats, 0.0)             # once the demand exists
    heat   = seeded 50..65
    name   = keyless placeholder (§10)
    leads.append({name, flavor:"", seats, stage:"meeting", age_weeks:0, heat})
    rep.spawned_leads.append(name);  spawned += 1
    rep line: "pipeline: +%s enters the calendar (%d seats, first meeting)"
```

The pool-gate is the honest version of "whales appear when the machine can
feed them": a 60-seat draw at week 6 with 4.1 pool units becomes a 4-seat
design partner instead. `pipe_stats.first_wk` is set on the first spawn ever.

*Real analogue*: deal-size distributions are log-normal (many small, few
whales — the tier table approximates it), and pipeline coverage precedes
bookings. *Simplification dropped*: no separate SQL/SAL qualification step —
the pool IS qualification; acceptable because a sub-meeting stage would add a
column with no decision attached to it.

---

## 4. Stage advance math

One seeded roll per live lead per week (salt 95, array order — order is part
of the spec). A lead advances when `roll < p_adv`:

```
C               = 1.5 + 0.8*sell + 3.0*sales_heads + mk_budget/400 + b_sales/600
                  # the exact gtm_cap formula, cap_scale 1.0 — reused, not re-invented
capacity_factor = clampf(C / (1.5 * leads.size()), 0.5, 1.5)
quality_factor  = 0.6 + product/100.0 * 0.8                     # 0.6..1.4
price_factor    = clampf(dm < 0.0 ? 1.0 : dm, 0.5, 1.3)         # dm = offers_demand_mult(state)
heat_factor     = 0.5 + heat/100.0                              # 0.5..1.5
size_factor     = jano_down(seats, SIZE_REF[era], 0.55)         # existing BSL curve
p_adv           = clampf(BASE_ADV[stage] * capacity_factor * quality_factor
                         * price_factor * heat_factor * size_factor, 0.05, 0.85)
```

Real analogues, one per factor:
- **BASE_ADV per stage** ≈ stage-to-stage conversion gates of a CRM pipeline
  (qualified-meeting → evaluation → negotiation → closed-won). The emergent
  overall win rate on a spawned lead lands at ~25-35% untended and ~55-65%
  well-run — the qualified-opportunity benchmark band (~20-30%) with the
  player's attention as the documented outperformance.
- **capacity_factor**: AE capacity is finite and shared; more open deals than
  the motion covers slows every one of them.
- **quality_factor**: pilots convert on product, and this reuses the tick's
  own quality-gate shape with a gentler floor (a pilot can limp; adoption
  cannot).
- **price_factor**: above-fair pricing stalls in evaluation and procurement;
  discounting at the margin accelerates closes (end-of-quarter behavior).
- **heat_factor**: deal momentum / recency-of-engagement scoring — stale
  deals slip, sponsored deals move.
- **size_factor**: cycle length grows with deal size; `jano_down` against an
  era-scaled knee makes a 40-seat deal crawl for a garage founder and move
  for an hq motion (§8).

Weekly journey at all-factors≈1: `1/0.45 + 1/0.35 + 1/0.40 ≈ 8` weeks
meeting→signed (11-12 through procurement). Under this game's compressed
clock (one tick ≈ a fortnight-to-month of calendar time) that reads as the
real 3-9-month enterprise cycle. *Simplification dropped*: multi-stakeholder
committees and legal redlines are folded into `procurement` and heat;
modeling individual champions would need a cast the save doesn't carry.

On advance:
- `stage` steps `meeting → pilot → (procurement if seats ≥ PROCUREMENT_SEATS
  and era_index ≥ 2) → contract`; `heat = min(heat+HEAT_ADVANCE, 100)`.
- Receipt names the stage AND the dominant factor (the factor farthest above
  1.0; ties break in the order listed):
  `"Meridian Logistics moved to pilot — the demo held (product v0.6)"`,
  `"…cleared procurement — the price sat at fair"`.

On advance FROM `contract` — **the close**:

```
traction += seats
logos.append({name, seats, since_wk: week, renewal_wk: era>=floor ? week+RENEW_EVERY : 0})
pipe_stats: signed+1, seats_signed+=seats, cycle_sum+=age_weeks
state.set_meta("pipe_signed_wk", week)
rep line: "SIGNED: Meridian Logistics — 12 seats · ~$4,800/wk (ACV ≈ $250k) · cycle 7 wks"
    where ACV = seats * unit_rev_wk * 52,
    unit_rev_wk = offers_arpu(state) >= 0 ? offers_arpu(state) : theta.arpu_wk * price_mult
```

Heat, decay, death (before the advance roll, same per-lead pass):

```
age_weeks += 1
decay = HEAT_DECAY - (era >= floor ? mini(sales_heads, 3) : 0);  decay = maxi(decay, 4)
heat = maxi(heat - decay, 0)
if heat == 0:  the lead DIES COLD —
    pipe_units = minf(pipe_units + seats, POOL_CAP)      # §2 refund
    pipe_stats.lost += 1
    rep line: "gone cold: Vanta Systems (6 seats) — %d wks of silence; enterprise
               deals die of no-decision, not a no" % age_weeks
```

Spawn heat 50-65 at decay 8 ⇒ an untouched lead dies in ~6-8 weeks — the
"cold after N weeks" rule with N emergent and player-extendable (push, §7;
advances refresh it). Weekly pipeline receipts are capped at 6 lines; the
overflow compresses to `"…and 2 more moved"`.

---

## 5. Account churn (below `floor` era)

The tick's churn stays THE churn — same formula, same knobs
(`A/residence × churn_mult × status_churn × care_mult × price_pain`) — but for
Enterprise it lands on accounts, not a smear of units:

```
loose_units = maxi(traction - Σ logo.seats, 0)      # DM side-sales, presets, legacy saves
loose_share = A > 0 ? loose_units / A : 0.0
loose churn = churn * loose_share  → floor + seeded coin (salt-95 stream) → traction -= n
logo churn:  pipe_churn_acc += churn * (1 - loose_share)
             if logos not empty and pipe_churn_acc >= seats of a seeded uniform pick:
                 that logo leaves WHOLE (max one per week):
                 traction -= seats; pipe_churn_acc -= seats; logos.erase
                 rep line: "−Quill Health churned — 9 seats leave together
                            (lifetime %d wks at v0.%d)"
```

Seats leave together because enterprise revenue is contract-shaped: you lose
logos, not fractions of logos. Expected units lost per week equals the old
formula exactly (the accumulator just batches it), so residence/lifetime pins
keep holding. *Simplification dropped*: the pick is seeded-uniform, not
health-weighted — post-sale account health scoring is real but would need a
per-logo meter the desk can't teach yet; the uniform pick is named here so a
later wave can upgrade it deliberately.

---

## 6. Renewals and expansion (`floor` / `hq` — see §8)

At `floor` the run graduates to annual contracts:

- **Renewal cliffs replace the accumulator for logos** (loose units keep §5's
  loose path). Each logo carries `renewal_wk`; logos signed earlier get
  `renewal_wk = week + RENEW_EVERY` assigned the tick the era flips. On its
  renewal week (salt-95 roll):

```
p_renew = clampf(1.0 - (RENEW_EVERY / residence) * th.churn_mult
                 * status_churn * care_mult * offers_price_pain(state), 0.50, 0.98)
pass: renewal_wk += RENEW_EVERY
      rep: "RENEWED: Quill Health — the annual contract holds (logo retention %d%%)" % p_renew*100
fail: whole logo churns (as §5's departure)
      rep: "LOST AT RENEWAL: Quill Health — 9 seats walk (care and quality decide renewals)"
```

  Calibration: expected weekly loss `seats × (1−p_renew)/RENEW_EVERY` equals
  the continuous formula by construction, so switching regimes does not bend
  the churn curve — it only makes it cliff-shaped, which is the truth of
  enterprise revenue. *Real analogue*: annual logo-retention benchmarks
  (~85-95%) — p_renew lands there when care and product are funded.
- **Expansion — land-and-expand.** Per logo per week (salt-95, array order),
  skipped when `traction ≥ 0.9 × tam`:

```
p_expand = 0.05 * quality_factor * (2.0 - care_mult)          # ≈ 0.03..0.09
hit: grow = maxi(int(ceil(seats * seeded 0.15..0.30)), 2)
     seats += grow; traction += grow
     rep: "EXPANSION at Quill Health: +4 seats — land-and-expand pays"
```

  *Real analogue*: net revenue retention >100% is the enterprise SaaS growth
  engine; expansion draws down the same TAM through Bass's own `P = N − A`,
  so it is bounded, not free. *Simplification dropped*: no per-logo upsell
  negotiation — expansion is earned by product + care, not played as a move
  (a written move can still heat nothing here; the DM narrates from the
  receipt).

---

## 7. Player push — the `push_lead` op

The DM's context lists the board by name (§11), so narration references real
leads; when the written move works one, the DM emits:

```
{ "op": "push_lead", "cat": "<lead name>", "v": <heat delta>, "why": "...", "weeks": 1 }
```

- **Schema** (`ADJUDICATE_SCHEMA` in `llm_client.gd` + C# twin, byte-synced):
  add `"push_lead"` to the effects `op` enum, and widen `cat` from the budget
  enum to `{"type":"string","maxLength":40}`. The budget-cat strictness moves
  entirely to the executor — which already defaults unknown budget cats to
  `"marketing"` in both engines — so nothing is lost but schema prettiness
  (open question 2). `EVENT_SCHEMA` (tier-2 cards) does NOT get the op: cards
  don't see the board. `event_generator.gd::ALLOWED_OPS` + the C# validator
  add `"push_lead"`.
- **Executor** (`garage_view_screen.gd::_apply_dm_effects` +
  `WeekCommit.cs::ApplyDmEffects`), the `price_offer` matching idiom:

```
"push_lead":
    var delta := clampi(int(d.get("v", 0)), -40, 40)
    match lead by two-way case-insensitive substring on `cat`
    no match  → out.append("no such lead in the pipeline — the phone rings nowhere")
    match     → lead.heat = clampi(lead.heat + delta, 0, 100)
                out.append("pushed %s: heat %+d — %s" % [name, delta, why])
```

- Negative pushes are legal (a botched demo cools a deal). Push never moves a
  stage and never adds traction: heat raises next tick's `heat_factor`, and
  the dice do the rest — founder attention is an executive-sponsor effect,
  not a signature. *Real analogue*: executive engagement measurably lifts
  win rates; it does not sign contracts by itself.
- **Sentinel** (`_sentinel`): a `push_lead` whose `cat` matches no live lead
  is a fault line (`"push_lead names 'X' but the pipeline holds: …"`) — same
  retry ladder as unknown statuses. Also extend the sentinel's `known` names
  list with `leads` + `logos` so the finished unknown-NPC check covers the
  board (engine-improvement 2).

---

## 8. Scaling by stage (era ladder — what unlocks when)

The same math everywhere; the CONSTANTS ladder up, so depth arrives as the
company earns it:

| era | seat-tier weights (3-8 / 9-20 / 21-60 / 61-120) | SIZE_REF | stages | churn regime | extras |
|---|---|---|---|---|---|
| garage | 70 / 25 / 5 / 0 | 10 | 3 (no procurement) | accumulator (§5) | founder-sold: `C ≈ 1.5+0.8×sell`; a 25-seat deal advances at ×~0.62 — big logos are long odds by math, never forbidden. Coach line: "design-partner pilots" |
| coworking | 60 / 30 / 10 / 0 | 16 | 3 | accumulator | first real contracts; ACV receipts start meaning something |
| office | 45 / 35 / 17 / 3 | 28 | 4: **procurement/security review** inserts between pilot and contract on deals ≥ 20 seats | accumulator | repeatable motion: sales hires visibly move stages (+3.0 C each); desk prints "your motion covers ~C/1.5 live deals" |
| floor | 30 / 35 / 27 / 8 | 45 | 4 | **renewal cliffs** (§6) | account teams: −1 heat decay per sales head (max −3); **expansion** unlocks |
| hq | 20 / 30 / 35 / 15 | 70 | 4 | renewal cliffs | whale-capable machine; everything on |

`SEAT_TIERS` and `SIZE_REF` are const tables in SimEngine (both engines).
*Real analogue for the ladder itself*: startups sell design-partner pilots
before they can sell procurement-grade contracts; security review appears
exactly when deal size makes a buyer's IT department wake up; renewals and
expansion become the revenue story at scale (NRR). The ladder is that
career, in constants.

---

## 9. Weekly-tick integration order

Inside `weekly_tick`, §8 (adoption/churn) grows one branch; everything else
is untouched:

```
§8  computes adds (float) and churn (float) exactly as today
    if enterprise_pipeline_active(state):
        8a  pool inflow: pipe_units += adds (NO gtm cap — §2), clamp POOL_CAP
        8b  ONE salt-95 rng, draws in this order:
            1. churn: loose units (floor+coin) → account accumulator pick (§5)
               OR renewal rolls in logos order (§6, floor/hq)
            2. per-lead pass in array order: age → decay → cold-death+refund
            3. survivors' advance rolls in array order; closes mutate traction/logos
            4. expansion rolls in logos order (floor/hq)
            5. spawns (≤2), placeholder names from salt-96 (§10)
        skip the legacy `net = adds − churn` + salt-91 remainder
    else: existing path, byte-identical
§9  money — unchanged; revenue sees post-close traction, so a contract signed
    this week bills this week. pipe_stats.spend += b_mk + b_sales (Enterprise only).
```

`rep` gains `spawned_leads: Array[String]` (Godot dict key / `[JsonProperty
("spawned_leads")] List<string>` on `WeeklyReport`). `metric_history` is
unchanged (customers = traction, as today).

**P&L impact: none new — decided.** Meetings, travel and demos ride the
`sales` lever narratively; a separate pipeline lane would double-bill the
same dollars. The pipeline changes WHEN revenue arrives, not where money
goes.

---

## 10. LLM leverage (the complete map)

Where a model genuinely earns its call — and where it must never be let near.

**L1 — batch lead naming + flavor (the ONE new call).**
- *Trigger*: after `weekly_tick`, when `rep.spawned_leads` is non-empty and
  `llm.enabled()` — from `_apply_lock` (garage_view_screen.gd) / the
  `WeekCommit.ApplyLock` twin. Fire-and-forget, `{"tier": "clarify"}` (the
  cheap fast model; this is prose off the critical path). ≤2 leads/wk by
  SPAWNS_PER_WK, so the call is rare and tiny.
- *Schema* (`LEAD_SCHEMA`, llm_client.gd + C# twin):

```
{ "type":"object", "additionalProperties":false, "required":["leads"],
  "properties":{ "leads":{ "type":"array", "minItems":1, "maxItems":3,
    "items":{ "type":"object", "additionalProperties":false,
      "required":["name","one_liner"],
      "properties":{ "name":{"type":"string","maxLength":30},
                     "one_liner":{"type":"string","maxLength":90} }}}}}
```

- *System prompt* (`LEAD_PROMPT`, ships byte-synced in both engines): "You
  name enterprise prospects for RUNWAY!, a satirical startup survival game.
  You receive the player's company (name, idea, what × who) and N new
  prospects that just took a first meeting, each with a size band. Invent N
  fictional companies that would plausibly BUY from this exact business —
  sector-appropriate, pronounceable, never real companies or people. one_liner:
  who they are and why they're suddenly shopping, dry, wince-funny, a complete
  sentence. Return exactly N leads in the order given. Never output numbers,
  seat counts, or stages."
- *User payload*: `{company:{name, idea, what, who}, era, existing_names:[all
  lead/logo/rival/investor names], new_leads:[{placeholder, band:
  "small|mid|large|whale", stage:"meeting"}]}` — the band is INPUT for flavor
  scale; the model outputs prose only.
- *Executor on callback*: match reply[i] to spawned lead i (count-checked);
  overwrite `name` (≤30, reject empty, dedupe against `existing_names` — on
  collision keep the placeholder) and `flavor`. Numbers, stage, heat, seats:
  never touched. A late reply lands on the next binder refresh; a save
  between spawn and reply persists placeholders, which is fine.
- *Keyless fallback* (also the instant placeholder while the call flies):
  `WorldGen.make_name(rng96) + " " + ENT_SUFFIX[seeded]` with `ENT_SUFFIX :=
  ["Logistics","Systems","Group","Health","Labs","Industrial","Financial",
  "Retail","Foods","Media"]` (both engines; the Markov seeds already contain
  Meridian, Vanta, Quill — the world names its own customers). Redraw up to 3
  on collision. `flavor` stays `""`.

**L2 — the DM references the board (zero new calls).** The pipeline rides the
existing adjudicator via `_directives` + digest lines (§11); the DM may emit
`push_lead` (heat only), `status`, `clock`. Signed/cold receipts enter the
run history like every engine line, so tier-2 event cards write follow-ups
("the Meridian pilot got a new stakeholder") for free.

**L3 — sentinel hardening (zero calls).** Leads + logos join the known-cast
list; `push_lead` against a ghost lead is a retriable fault (§7).

**Tempting but WRONG:**
- Letting the model set `seats` or deal size ("a Fortune-500 logo should be
  huge") — breaks pool conservation and every balance pin. Size is a seeded
  tier draw; the model only ever *describes* it.
- Letting narration advance a stage or close a deal ("the demo went great —
  they're in pilot now") — outcomes belong to the salt-95 dice against
  state-derived probabilities; the DM's only lever is heat, clamped ±40.
- A per-week re-flavor call for the whole board — token burn with zero
  mechanical content, and it violates "one rare batch call".
- LLM-written churn/renewal/expansion explanations — those receipts are the
  engine showing its math; the DM may re-narrate them, never re-decide them.
- Asking the model how many leads to spawn this week — spawn rate IS the
  reconciliation contract (§2); prose has no vote.

---

## 11. DM context, digest, signals

`event_generator.gd::_directives` (+ `EventGenerator.Directives` twin) gains,
on Enterprise runs (top 5 leads by heat desc, then `(+N more)`):

```
- Pipeline: Meridian Logistics (pilot, 40 seats, warm) · Vanta Systems (meeting, 6 seats, cold — dies in 1 wk) · Quill Health (contract, 12 seats, hot) (+2 more)
- SIGNED THIS WEEK: Quill Health (12 seats). Let the week feel it.        [when pipe_signed_wk == week]
- A lead is about to go cold: Vanta Systems. If the move works a named lead, use push_lead {cat: the exact lead name, v: heat −40..40}.   [when any heat ≤ 16]
- Enterprise law: customers arrive ONLY through signed contracts. Never grant traction for pipeline work — heat the lead instead.
```

Heat words: `hot ≥75 · warm ≥50 · cool ≥25 · cold <25`; "dies in N wk" =
`ceil(heat / decay)` when ≤ 2.

`GameState.to_digest()` adds (Enterprise only) `"pipeline": ["Meridian
Logistics — pilot, 40 seats, warm", …]` and `"signed_logos": "4 logos, 63
seats"` — so tier-2 cards see the same board the adjudicator does.

`SimEngine.signals()` adds one entry:
`"pipeline": "3 live (18 seats) · hottest Meridian Logistics (pilot, warm) · pool 4.2 seats"`.

---

## 12. Desk — the customers tab (1160×760), report lines, bangs

**Godot** `binder.gd::_tab_customers()` branches first:
`if state.biz_who == "Enterprise": _tab_customers_enterprise(); return` —
twin branch in `BinderScreen.TabCustomers()`. The board is the founder's own
calendar, so it shows at analytics 0 (fog of war hides MARKET numbers, not
your own meetings); the believed-market lines keep their analytics gates.

Layout (pen-and-clipboard idiom — `_label` + heat-colored words, no SaaS
widgets):

```
y6    "%d customers · %d logos signed" % [traction, logos.size()]        (46px)
y64   "the pipeline — %d live · %d seats in motion · pool %.0f waiting"  (24px, INK 0.6)
y96   column headers: MEETING | PILOT | [PROCUREMENT] | CONTRACT         (26px)
      3 columns ×386px pre-office, 4 ×290px from office (matches §8)
y128+ lead chips, 64px pitch, 2 lines each, in stage's column:
        "Meridian Logistics"                                (26px, INK)
        "40 seats · warm · wk 3"                            (21px; heat word colored
                                                             PEN cold / YELL warm / SAGE hot)
      flavor (when present) as the chip's hover-free 3rd line at 18px, INK 0.45,
      only when the column holds ≤3 chips (space math: 8-chip cap fits one column
      at 64px; flavor lines yield first)
y~620 signed logos strip: "logos: Quill Health (12) · Fernbay Group (9) · …" (22px, wraps, ≤2 lines);
      at floor/hq a logo ≤4 wks from renewal appends it: "Quill Health (12, renews in 3 wks)"
y~700 THE TEACHING FOOTER (24px, BLUE):
        "win rate %d/%d (%d%%) · avg cycle %d wks · cost per signed seat ≈ $%s · a seat pays ≈ $%.0f/wk"
        from pipe_stats: signed/(signed+lost), cycle_sum/signed, spend/seats_signed, unit_rev_wk
y~734 era coach line (20px, INK 0.5), e.g. garage: "design-partner pilots: small
      deals teach fastest"; office: "procurement appears on 20+ seat deals — price
      fairness moves it"; floor: "renewals every 26 wks — care and quality decide them"
```

**Bang**: `"customers"` joins the bang tabs in both engines (`_bangs` /
`_bangs` dict). Visible when Enterprise AND (any lead `heat ≤ 16` — about to
go cold — OR `meta pipe_signed_wk == week` — a contract signed). Same
mechanism as pricing/ledger/cap-table.

**Report lines** (already specified inline): spawn, advance (+dominant
factor), SIGNED (+ACV + cycle), gone-cold (+no-decision lesson), churn
(seats leave together), RENEWED / LOST AT RENEWAL, EXPANSION. Capped at 6
pipeline lines/week. These receipts are the pedagogy: sales cycle, win rate,
ACV, no-decision, logo retention and land-and-expand each get named by the
engine in the week the player lives them.

---

## INTERFACE DELTA (assessable, stands alone)

Every player-visible change this lane needs, both engines. Surfaces: binder
tab · journal (weekly outcome log + spreads print the engine's receipt lines)
· bang · coach. Garage HUD, vitals, ledger, pricing, crew, cap table, street,
threats: untouched.

| surface | exists today? | CHANGE/ADD | exactly how (content, controls, position, states) | why the player needs it |
|---|---|---|---|---|
| binder `customers` tab — Enterprise branch | tab exists; shows count + fog text + market beliefs | CHANGE (Enterprise runs only; SMB/Consumer keep today's page) | New `_tab_customers_enterprise()` page in the 1160×760 pane: headline y6 "N customers · M logos signed" (46px); summary y64 "the pipeline — L live · S seats in motion · pool P waiting" (24px). Non-Enterprise page byte-identical to today | Enterprise customers are named accounts in motion, not a count; the desk must show the motion |
| binder `customers` — stage board | no | ADD | Column headers y96 (26px): MEETING · PILOT · CONTRACT at 3×386px (garage/coworking); PROCUREMENT column appears from office era, 4×290px. Pen-ruled columns in the clipboard idiom, no boxes-with-chrome | The stage-gated pipeline is the mental model being taught; columns ARE the lesson |
| binder `customers` — lead chips | no | ADD | Per lead, in its stage column from y128, 64px pitch, 2 lines: "Meridian Logistics" (26px INK) / "40 seats · warm · wk 3" (21px; heat word colored PEN cold / YELL warm / SAGE hot). Cold leads ≤2 wks from dead append "— dies in 1 wk" in PEN. Optional 3rd flavor line (18px, INK 0.45) only while the column holds ≤3 chips. No controls — the pipeline is pushed by written moves, not buttons | See every deal's size, warmth and age at a glance; know who to write the week's move about |
| binder `customers` — signed logos strip | no | ADD | y~620 (22px, ≤2 wrapped lines): "logos: Quill Health (12) · Fernbay Group (9) · …"; at floor/hq each logo appends its renewal countdown when ≤4 wks: "Quill Health (12, renews in 3 wks)" | Closed business stays visible as named accounts — and renewal risk announces itself |
| binder `customers` — teaching footer | no | ADD | y~700 (24px BLUE): "win rate 4/11 (36%) · avg cycle 7 wks · cost per signed seat ≈ $310 · a seat pays ≈ $400/wk" from `pipe_stats`; "?" placeholders until first signing | Names the concepts (win rate, sales cycle, CAC-per-seat vs revenue) with the run's own numbers |
| binder `customers` — fog of war | yes (analytics 0 hides everything) | CHANGE (Enterprise only) | Board + chips + footer render at analytics 0; the believed-market lines keep their analytics gates | The pipeline is the founder's own calendar — fog hides market truth, never your own meetings |
| coach — era line on the tab | coach-line idiom exists elsewhere | ADD | y~734 (20px, INK 0.5), one line per era: garage "design-partner pilots: small deals teach fastest" · coworking "first real contracts — the ACV on a receipt is a year of one logo" · office "procurement appears on 20+ seat deals — price fairness moves it" · floor/hq "renewals every 26 wks — care and quality decide them" | Tells the player what this era's pipeline can and cannot do, in one sentence |
| bang — `customers` tab | bang mechanism exists (pricing / ledger / cap table) | ADD | "!" on the customers tab, same PEN mark + position idiom, visible when Enterprise AND (any lead heat ≤ 16 OR a contract signed this tick, `meta pipe_signed_wk == week`) | Pulls the player to the desk exactly when a deal is dying or just landed |
| journal — pipeline receipts | outcome-log line idiom exists | ADD (8 line formats, ≤6 pipeline lines/wk then "…and 2 more moved") | "pipeline: +Vanta Systems enters the calendar (6 seats, first meeting)" · "Meridian Logistics moved to pilot — the demo held (product v0.6)" (dominant factor named) · "SIGNED: Meridian Logistics — 12 seats · ~$4,800/wk (ACV ≈ $250k) · cycle 7 wks" · "gone cold: Vanta Systems (6 seats) — 5 wks of silence; enterprise deals die of no-decision, not a no" · "−Quill Health churned — 9 seats leave together (lifetime 87 wks at v0.6)" · "RENEWED: Quill Health — the annual contract holds (logo retention 84%)" · "LOST AT RENEWAL: Quill Health — 9 seats walk (care and quality decide renewals)" · "EXPANSION at Quill Health: +4 seats — land-and-expand pays" | Receipts are the pedagogy: every move or death explains WHY in sales language, in the week it happens |
| journal — push receipts | DM-effect receipt idiom exists | ADD (2 formats) | "pushed Meridian Logistics: heat +20 — flew out for the exec dinner" · miss: "no such lead in the pipeline — the phone rings nowhere" | The player sees their written move land on (or miss) a named deal |

---

## 13. Twin test pins (both suites)

1. **Pure advance math, exact**: expose `lead_advance_p(state, lead, live)`;
   garage state (sell 3, no hires, budgets 0, product 50, no offers priced),
   lead {6 seats, heat 55, meeting}, live 1 → assert the closed-form value to
   1e-9 in BOTH engines (no RNG involved — the one pin that can be
   value-identical across engines). Then monotonicity on the same helper:
   b_sales 4000 raises it; seats 60 lowers it; heat 100 raises it.
2. **Determinism**: two identical Enterprise states, 5 ticks each → identical
   `leads` (name/stage/heat/age), `pipe_units`, `traction`, `logos`.
3. **Cold death refunds the pool, exactly**: unlaunched, traction 0, pool 0,
   one planted lead {12 seats, heat 8} → one tick → lead gone,
   `pipe_units == 12.0`, a "gone cold" line in rep.
4. **Close conserves seats**: drive the close path (helper `close_lead(state,
   i, rep)`) on a planted contract-stage lead {12 seats} → traction +12
   exactly, logo registered, `pipe_stats.signed == 1`, rep line contains
   "SIGNED".
5. **Accounts churn whole**: traction 40, one logo {40 seats}, loose 0,
   `pipe_churn_acc` preloaded 40 → churn resolution removes the logo and
   exactly 40 traction in one week — never a partial account.
6. **Non-Enterprise byte-identity**: an SMB state ticks with `leads` empty,
   `pipe_units == 0.0`, and the §8 adds/churn lines present — the pipeline
   never touches the other two audiences.

---

## 14. Engine-improvement suggestions (≤5)

1. Finish the sentinel's unknown-NPC check (the `known` list is built in
   `_sentinel` but narration is never scanned against it) and include
   leads + logos — the board makes hallucinated customers likelier.
2. Retarget the pre-pipeline `enterprise_pilot` status (arpu ×1.3) to also
   grant +10 heat/wk to all live leads while active, so the buff's words
   match the new mechanics.
3. Add a `pipeline` (live seats) series to `metric_history` for a customers-
   tab spark once the snapshot struct is touched for another reason (Unity's
   `MetricSnapshot` is typed — piggyback, don't special-case).
4. Switch Enterprise `rep.cac` from demand-based to the blended
   cost-per-signed-seat the desk already computes from `pipe_stats` (SMB /
   Consumer keep the current form).
5. Subsystems 2 (applicants) and 3 (rival actions) should adopt this doc's
   summary-string idiom in `to_digest`/`signals` so the DM reads every desk
   the same way.

## 15. Open questions (≤3)

1. Legacy mid-run Enterprise saves resume with their traction as loose units.
   Worth a one-shot cosmetic backfill (batch-name 1-3 logos out of existing
   traction) or leave the past unnamed?
2. Widening `ADJUDICATE_SCHEMA.cat` to a free string (for lead names) drops
   schema-side budget-cat strictness; executors already guard. Acceptable, or
   should `push_lead` carry a dedicated `lead` field instead (bigger schema
   diff, stricter)?
3. Renewal cliffs at floor/hq are exactly the revenue events the Board
   subsystem (§9 of the plan) will want expectations around — should the
   renewal calendar surface on `cap table` too, or stay a customers-tab fact?
