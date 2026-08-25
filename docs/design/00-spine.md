# 00 — THE INTEGRATION SPINE

The contract every subsystem design plugs into. The nine lanes: 01 catalog ·
02 labor · 03 rivals+macro · 04 funnel · 05 enterprise pipeline · 06 finance ·
07 roadmap bets · 08 board+M&A · 09 hardware production. Where a lane doc
disagrees with this file, THIS FILE WINS; change it here first, then everywhere.
Twin law: every rule below lands in BOTH engines (game/ Godot, unity/ C#) in the
same commit, with the same twin-suite checks in the same order.

## 0. THE LAWS

1. **The engine owns every number, the DM owns every sentence.** A narrow typed
   schema is the only bridge. Nothing below breaks this.
2. **PEDAGOGY.** RUNWAY! is an economic business simulator that teaches. Every
   number the player sees is a real business concept under its real name (CAC,
   contribution margin, dilution, runway, severance, carrying cost, COGS), with a
   plain gloss at the point of display. Every receipt line teaches WHY:
   `"severance: 3 wks of $1,200 = −$3,600 — letting go costs now to save later"`,
   never `"-$3,600"`. Each subsystem spec lists its receipt lines with the
   teaching clause; a mechanic that resolves silently is a review-rejection.
3. **FAITHFUL MECHANICS.** Every formula in a subsystem spec carries two lines:
   `Real-world analogue: <one line>` and `Simplification drops: <one line>`
   (e.g. adoption = Bass diffusion, drops adopter heterogeneity; learning curve
   = Wright's law, drops the labor/material split; elasticity = constant-
   elasticity demand, drops reference-price anchoring). Gamey hand-waving with
   no named analogue is a review-rejection.
4. **SCALE-PROGRESSIVE DEPTH.** Subsystems deepen with era, like reality
   (§9 the era ladder is the single source). A garage has no taxes, no board
   cadence, no HR process; HQ has all of it. Specs conform to the ladder table.
5. **CLAMP LAW.** Everything externally settable (DM op, desk stepper, LLM
   field) passes an engine-side clamp at the boundary, era-scaled where money is
   involved (the `era_spend_cap` idiom). No clamp, no merge.
6. **RECEIPTS IDIOM.** Subsystems never print. They append plain strings to the
   weekly report: `rep.lines` (receipts, `$%d` formatted, lowercase, wry),
   `rep.events` (BIG beats only — journal ⚡ + DM event context), and
   `rep.fired_clocks`. Big = a named beat a founder would retell (launch,
   scandal, poach, shock, offer, board verdict, first stockout), never drift.

---

## 1. THE TICK ORDER v2

`SimEngine.weekly_tick` / `SimEngine.WeeklyTick` — one function, numbered
sections, this order. Each new subsystem is ONE section with its number in a
comment; hooks named here are the only insertion points.

| # | section | owner | why here |
|---|---------|-------|----------|
| 1 | **clocks** fire | core (exists) | time hits first; deadlines define the week before anyone acts. |
| 2 | **statuses** decrement/expire | core (exists) | modifiers must settle before anything reads them (install with weeks≥2 to affect a tick — unchanged). |
| 3a | **onboarding pipeline** advances (old §3) | core (exists) | graduates join the roster before the labor market counts open seats. |
| 3b | **labor market** — arrivals (salts 20/21) → applicant decay (22) → review cycle + raise asks/resignations (23) | 02-labor | the roster and applicant pool must be final before morale feels them and payroll pays them. |
| 4 | **fatigue, morale drift, resignation roll, exhaustion** (old §4) | core (exists) | the human cost reads the settled roster and last week's shocks (statuses persist across ticks). |
| 5 | **tech debt decay + outage roll** (old §5) | core (exists) | build health settles before roadmap, production and adoption depend on it. |
| 6a | **THE STREET — rivals act** (replaces the old §6 ratchet): per-rival upkeep → weekly action pick (salt 30; poach 31; era gates) → hq disruptor roll (32) → avg-strength pressure | 03-rivals | rivals move before the market so price cuts and launches shape THIS week's demand; the poach lands after arrivals (3b) and before payroll (9). |
| 6b | **MACRO** — mean-reverting walk (salt 7, stream preserved) → shock roll (80) → watch→shock transitions → banner lines | 03-macro | shocks install statuses read by everything downstream this same tick (adoption §8, money §9, offers); rivals read macro one week lagged BY DESIGN — the pre-announced watch week is the reaction lag. |
| 7 | **roadmap bets** — rnd-routed progress ticks; READY bets roll the house dice (salt 70); payoffs land | 07-roadmap | a shipped bet's quality/retention payoff must exist before adoption reads product. |
| 7h | **hardware production** (Hardware runs) — build target → produce (learning curve) → breakdown roll (110) | 09-production | produce-first: stock must exist before adoption; stockouts cap adds, not revenue after the fact. |
| 8 | **funnel → adoption & churn** (old §8) — channel reach replaces mk_mult; Bass + word-of-mouth; `gtm_cap` clamp; Hardware clamps adds to stock and decrements it; Enterprise runs route adds/churn through the pipeline stream (salt 50) and SKIP the salt-91 remainder | 04-funnel + 05-pipeline | the market moves exactly once, after weather, rivals, quality and stock are all settled. |
| 9 | **money & P&L** (old §9) — revenue, cogs+learning, payroll, rent, infra, budgets, offer_fixed, severance, recruiting, production lanes, incidents (93), **then interest, then tax** — the pnl record is written HERE, complete (§2) | 06-finance owns the assembly | one place computes the week's truth; interest and tax land BEFORE the record so the ledger never lies. |
| 9b | **beliefs converge** (old §9b) | core (exists) | learning reads the week's real outcomes. |
| 9c | **board review** — deterministic, at `review_week`: revenue vs covenant, strikes/goodwill, statuses | 08-board | the review reads the finished week's pnl; no dice, no salt. |
| 9d | **M&A offers / IPO window** (salt 100) — rare, valuation-priced offer + hard clock; acqui-hire floor when dying | 08-M&A | valuation reads this week's growth; the offer is a journal event, never an auto-accept. |
| 10 | **commitments** pay + decrement (old §10) | core (exists) | standing costs settle after the operating week; their lane is already in the pnl (identity). |
| 11 | **metric history snapshot + clamp_meters** | core (exists) | always last; the binder's memory records the finished week. |

The caller contract is unchanged: the world acts first (`_apply_lock` /
`ApplyLock` runs the tick, then the adjudicated move's ops). The garage-side
legacy staff tick (`weekly_staff_tick`) stays where it is; do not move it in
this wave.

---

## 2. THE PNL RECORD v2

One record per week, written whole in tick §9. Godot: `state.set_meta("pnl",
{...})`. Unity: `state.LastPnl = new Pnl {...}` — same JSON keys byte-for-byte.

Final field list (snake_case keys; C# JsonProperty must match; lanes marked
(hw) exist only when Hardware production is active — an absent key reads 0):

```
revenue, cogs, rent, payroll, infra,
marketing, sales, care, rnd, office,   # levers; marketing = Σ of the four channel budgets (04)
offer_fixed,                           # catalog weekly overheads (01)
severance, recruiting,                 # labor: firing invoice · recruiter retainer (02)
production, subcontract,               # (hw) in-house build cost · contract-mfr premium (09)
equip_upkeep, carrying,                # (hw) machine upkeep · stock carrying cost (09)
incident, liabilities_wk,              # the unforeseen + standing commitments lane
interest, tax,                         # the bank & the state — OUTSIDE burn (06)
burn, net,                             # totals
learning                               # meta (multiplier, NOT money)
```

**The identity (a twin test pins BOTH lines, every week of an 8-week mixed run):**

```
burn = cogs + rent + payroll + infra
     + marketing + sales + care + rnd + office
     + offer_fixed + severance + recruiting
     + production + subcontract + equip_upkeep + carrying + incident
net  = revenue − burn − liabilities_wk − interest − tax
```

Burn is OPERATING spend only. Interest and tax sit outside it — the real
income-statement shape (operating profit → cost of debt → tax → net), which is
the pedagogy: 06-finance's EBT lesson. Tax = 20% of positive EBT
(revenue − burn − liabilities_wk − interest), weekly, office era up, with loss
carryforward sheltering profit (06 owns the math). `liabilities_wk` stays
positive-stored (the commitments lane), paid in tick §10 — unchanged. Interest
moves BEFORE the record is written (today it mutates cash after — that
changes; the ledger must show it). The channel split is NOT in the pnl: it
lives in `budgets` state (04), and `pnl.marketing` is the sum — back-compat
readers keep working.

**Display split (binding)**: THE LEDGER keeps the compact three-line P&L
(in/serving · out-lanes · bottom line, zero lanes omitted, 04's fixed slots),
appending conditional lane fragments (severance, recruiting, hw lanes) and
06's one `the bank & the state:` line. THE BANK tab renders the FULL grouped
statement: IN → COST OF SERVING (learning gloss) → KEEPING THE LIGHTS ON
(rent · payroll · infra · offer_fixed · equip_upkeep · carrying) → THE LEVERS
→ THE UNPLANNED (incident · severance · standing) → THE BANK & THE STATE
(interest · principal · tax) → THE BOTTOM LINE with break-even.

---

## 3. THE SALT REGISTRY

`_rng(state, salt)` / `RngFor(state, salt)` — stream keyed (seed, week, salt).
Frozen salts never change meaning (replay + save compatibility).

**THE CONVENTION (03-rivals proposed; spine ENDORSES with two guards):
`salt = business-sim-plan section × 10 + n` (sections: 1 catalog · 2 labor ·
3 rivals · 4 funnel · 5 enterprise · 6 finance · 7 roadmap · 8 macro · 9 board
· 10 M&A · 11 hardware). Guards: (a) a lane SKIPS any frozen legacy number
inside its decade (77 in roadmap's, 88 in macro's, 91/93 in board's); (b) n ≤
9, one draw-site per salt.** This makes future claims collision-free by
construction.

The authoritative table (lane specs citing anything else update to this):

| salt | lane | draw |
|------|------|------|
| 4 | core (frozen) | morale resignation roll |
| 5 | core (frozen) | outage roll |
| 6 | core (RETIRED) | old rival strength ratchet — 03 replaces it with actions; the number is a tombstone, never reassigned |
| 7 | core (frozen) | trend walk — 03-macro mean-reverts it STREAM-PRESERVED (endorsed) |
| 9 | core (frozen) | term-sheet generation |
| 77 | core (frozen) | R&D quality seeded remainder |
| 88 | core (frozen) | belief seeding |
| 91 | core (frozen) | adoption net seeded remainder |
| 93 | core (frozen) | incidents + standing liabilities (06 may extend the pick table, same salt) |
| 11 | 01-catalog | keyless draft jitter |
| 20 | 02-labor | candidate arrivals (per open role, shared stream) |
| 21 | 02-labor | candidate stats (skill/ask, creation order) |
| 22 | 02-labor | applicant patience decay |
| 23 | 02-labor | raise-ask / resignation ladder |
| 24 | 02-labor | keyless name/quirk pools |
| 30 | 03-rivals | weekly action pick (d1, per rival) |
| 31 | 03-rivals | poach roll |
| 32 | 03-rivals | hq disruptor spawn |
| 80 | 03-macro | shock roll (watch→shock transitions) |
| 50 | 05-pipeline | THE pipeline stream — advances, renewals, expansion, spawns, loose-churn coin, in the spec's fixed draw order |
| 51 | 05-pipeline | keyless lead-name pool |
| 70 | 07-roadmap | ship roll payoff spread |
| 71 | 07-roadmap | slot refresh |
| 100 | 08-M&A | offer arrival + premium rolls |
| 110 | 09-hardware | machine breakdown roll |
| 111 | 09-hardware | repurchase seeded remainder |
| 95 | **BURNED** | four lanes claimed it (05 advances, 07 ship, 08 offers, 09 breakdowns) — permanently reserved so any stale `95` in code or spec fails review on sight |

**Explicit reassignments (each lane updates its spec + code):**
01-catalog 21 → **11** · 02-labor 30/31/32/33/34 → **20/21/22/23/24** (its
claimed block sat in rivals' decade) · 03-rivals keeps 30/31/32, 03-macro
keeps 80 (their claims followed the convention) · 05-pipeline 95/96 →
**50/51** · 07-roadmap 95/97 → **70/71** · 08-M&A 95 → **100** · 09-hardware
95 (breakdown) → **110** and its r95 repurchase remainder → **111**.
04-funnel and 06-finance claim no salts (correct — pure state + arithmetic).

Non-stream seeded hashes in use (leave alone): hire-name
`hash(seed:week:pipeline.size)`.
NOTE: the two engines do NOT share PRNG internals (Unity `Rng.FromKey` is not
Godot's `hash()`+PCG32). Determinism is a PER-ENGINE guarantee: same seed+week
⇒ same result in that engine. Never write a test expecting cross-engine
byte-equal draws; twin parity is same checks, same order, same logic.

---

## 4. THE ATTENTION SYSTEM v2

One engine-side pure function, both engines, replacing every ad-hoc bang
condition (binder `_refresh`, garage `_process`, BinderScreen, GarageScreen):

```
SimEngine.attention_items(state) -> Array[{desk:String, key:String, severity:int, label:String}]
C#: List<AttentionItem> SimEngine.AttentionItems(GameState)   (same JSON keys)
```

Severity: 1 = note (ink "!"), 2 = warn (coral "!"), 3 = alarm (coral "!",
pulses on the existing 12fps breath clock). Consumers:

- **Binder tabs**: a tab wears the bang of its highest-severity item; every tab
  may bang now (registry below), not just the three hardcoded ones.
- **Garage badge**: visible when any item exists (unchanged position).
- **Garage HUD ticker** (NEW — decided: YES): one hand-font line under the
  binder button, the single top item by (severity desc, registry order), engine
  `label` verbatim, ≤40 chars, severity ≥2 only. Both engines. The ticker is
  pedagogy: it names the problem in business terms.

The registry — the union of the nine lanes' claimed bang conditions,
consolidated (keys are stable identifiers; tests pin one row each; each lane's
spec details the exact predicate — the registry row is the contract):

| desk | key | condition (owning lane) | sev |
|------|-----|-------------------------|-----|
| pricing | `unpriced` | any offer billing at going rate (01) | 2 |
| the ledger | `losing_week` | last pnl net < 0 (06, kept) | 2 |
| the ledger | `channel_waste` | a channel burning: spend ≥ 500 with attribution < 0.05, or content rotting (04) | 2 |
| the bank | `debt_distress` | cash < 2× weekly debt service, a missed note, or a balloon ≤2 wks unpayable (06) | 3 |
| the bank | `first_tax` | first tax week, until viewed (ack `tax_seen`) (06) | 2 |
| the bank | `broke_even` | first break-even crossed, until viewed (ack `broke_even_seen`) (06) | 1 |
| crew | `applicants_waiting` | any applicant waiting (02) | 1 |
| crew | `wants_raise` | any employee flagged wants_raise (02) | 2 |
| crew | `silent_role` | a role open ≥ 8 wks with 0 waiting (02) | 2 |
| crew | `span_thin` | span_mult < 1.0 — floor+ (02) | 2 |
| crew | `poach_attempt` | a poach fired this week (03→02 handoff, meta `poach_wk == week`) | 3 |
| cap table | `term_sheets` | flag `fundraising_open` (exists) | 3 |
| cap table | `board_review` | review_week − week ≤ 1 (08) | 2 |
| cap table | `offer_on_table` | M&A offer or `ipo_window` live (08) | 3 |
| customers | `lead_cold` | Enterprise: any lead heat ≤ 16 (05) | 2 |
| customers | `pipe_signed` | Enterprise: a contract signed this tick (05) | 1 |
| product | `bet_ready` | any bet finished R&D, dice next week (07) | 2 |
| product | `debt_critical` | tech_debt ≥ 70 (07) | 2 |
| product | `stockout` | hw: stockout last week (09) | 3 |
| product | `overstock` | hw: stock > 8× demand and > 20 (09) | 2 |
| product | `machine_down` | hw: a machine down this week (09) | 2 |
| the street | `street_beat` | meta `street_beats` non-empty — poach, launch, stumble, sniff, winter/boom announce, disruptor (03) | 2 |

The threats tab additionally renders all items of severity ≥2 as lines (free
aggregation; 03's price-war row rides this). Lanes do NOT wire `_bangs` or the
garage OR-chain directly — where a lane spec says "add to `_bangs`" or "extend
the garage OR-chain", implement it as rows of THIS registry; the per-tab bang
is derived. Twin tests: one check per registry row.

---

## 5. THE DM CONTEXT CONTRACT

The adjudicator user message keeps the context sandwich exactly as built today
(`compose_adjudicate_user` / Unity twin): digest → ENGINE SIGNALS → WORLD →
STORY SO FAR → RECENT WEEKS → arcs → dice → traits → DIRECTIVES → event → move.

`_directives()` (Godot) / `Directives()` (C#) is the per-subsystem state-lines
block and grows by this contract. Section order and one-line formats (emit only
when true; `%` = engine-formatted number):

```
 1 runway    - Runway is %d weeks. The world MUST escalate; nothing is routine.   (exists)
 2 founder   - The founder is exhausted (%d/6). It shows in everything.           (exists)
 3 clocks    - A deadline looms (%d wks): %s. Reference it.                       (exists)
 4 debt      - Tech debt is %d. The cracks are visible to customers.              (exists)
 5 catalog   - On sale: '%s' at $%d %s.  (+ unpriced/free variants)               (exists)
 6 labor     - Hiring: %d applicants for %s (best: skill %d, asks $%d/wk).
              - POACH: %s is courting %s ($%d/wk, underpaid).
 7 rivals    - %s cut prices ~%d%% this week.  /  - %s launched: %s.              (this week's actions only)
 8 macro     - The street smells %s: valuations ×%.1f, term sheets %s.            (banner line verbatim)
 9 leads     - Lead: %s at %s, %d wks stalled.                                    (max 3, coldest first)
10 finance   - Loan: $%d at %.1f%%/wk; payment $%d due in %d wks.
              - The taxman takes %d%% of profit now (EBT $%d last week).
11 bets      - Bet ready to ship: '%s' (R&D done; shipping rolls the house dice).
12 board     - The board's covenant: $%d/wk by wk %d; tracking %s (strikes %d).
13 production- Stock: %d units (made %d, sold %d last week).  / - STOCKOUT: demand outran stock.
14 M&A       - OFFER ON THE TABLE: %s, $%d for the company, expires in %d wks.
```

**Token budget guard**: the whole DIRECTIVES block is hard-capped at 24 lines /
~1,200 chars. Priority = the order above (1 is never dropped); within a
section, most-urgent first. The composer truncates, never the subsystems.

**BIG-beat rule**: only entries the engine appended to `rep.events` this week
may also enter the EVENT context (journal ⚡, `played_events`, next card user
message). Weekly drift stays in `rep.lines` receipts and out of the DM's event
memory. The event-card composer (`compose_event_user`) gains ONE line:
`THIS WEEK IN THE WORLD:` + the current week's `rep.events`, nothing else new.

---

## 6. THE LLM REGISTRY

Every LLM call across all nine subsystems. The LLM dresses entities that
already exist engine-side, and proposes only from fixed enums (status catalog,
op list, arc kinds); **every number it emits passes an engine clamp before
touching state; it never sets a number the engine didn't clamp**. Keyless path:
seeded pools, byte-identical mechanics.

| call | trigger | cadence | batch shape | cost | must never decide |
|------|---------|---------|-------------|------|-------------------|
| worldgen (exists) | run start | once/run | 1 call: market+investors+rivals | heavy | theta values (clamped), rival strength (clamped) |
| run director arcs (exists) | run start + era change | ~5/run | 1 call, ≤3 arcs | heavy | any state number |
| event card prefetch (exists) | pool < 3, background | ≤1/wk | 1 card | medium | effects beyond the op enum/ranges |
| clarify pre-pass (exists) | written move | ≤3/turn (capped) | 1 question | cheap | anything but the question |
| adjudicate (exists) | move commit | 1 + ≤1 sentinel retry | 1 verdict | heavy | DC floor breaches, off-enum ops, unclamped magnitudes |
| dept free-plans (exists) | free-text dept plans | ≤3/turn | 1 per dept | heavy | same as adjudicate |
| offer pricing (exists, grows) | "+ sell something new" | on demand | 1 offer incl. itemized cost_lines/fixed_lines | medium | final numbers (add_offer clamps every line) |
| **labor dressing** (new) | arrival week only | ≤1/wk | ONE call, ALL candidates (names, quirks, one-liners) | cheap | salary, skill, patience (engine-drawn) |
| **lead naming** (new) | lead spawn (rare) | ≤1/spawn wk | one call, all new leads | cheap | stage odds, seat counts |
| **bet cards** (new) | era change or slot opens | rare | one call, all open slots | cheap | cost weeks, payoff distribution |
| rivals, macro, funnel, finance, board, M&A, production | — | **zero new calls** | ride `rep.events` into existing context | — | — |

**Per-week ceiling**: a normal week with no written move = 1 scheduled call
(prefetch). A written-move week ≤ 8 (clarify 3 + adjudicate 1 + retry 1 + dept
3). New subsystems add at most +1 scheduled call on an arrival/spawn week.
Standing rule: **≤2 scheduled calls per week outside the move path**; a
subsystem needing more must batch into one.

---

## 7. THE OP REGISTRY

One list, SIX sites, byte-synced — Godot: schema enum (`llm_client.gd`),
`ALLOWED_OPS` (`event_generator.gd`), executor (`garage_view_screen.gd
_apply_dm_effects`); Unity: schema enum (`LlmClient.cs`), `ALLOWED_OPS`
(`EventGenerator.cs`), executor (`WeekCommit.cs ApplyDmEffects`). A twin test
pins the three lists equal per engine. **KNOWN BUG (fix in Wave A):
`price_offer` is in both schema enums and both executors but MISSING from both
`ALLOWED_OPS` — the validator rejects any reply carrying it wholesale. Add it +
the pin test.**

Final op list v2 (v/cat semantics; every op returns a receipt line):

| op | v | cat | clamp | wave |
|----|---|-----|-------|------|
| cash_delta / product_delta / traction_delta / morale_delta / hype_delta | number | — | EffectOps era/meter clamps (exists) | — |
| set_flag | flag name | — | known-flag list (exists) | — |
| status | catalog name | — | STATUS catalog only (exists) | — |
| clock | consequence text | — | weeks ≥1, text ≤120 (exists) | — |
| set_price | mult | — | 0.5..2.0 (exists, legacy) | — |
| price_offer | $ per unit | offer name (fuzzy) | 0..50,000 (exists) | A (validator fix) |
| set_marketing | $/wk | — | 0..50,000 (exists, legacy) | — |
| hire | role | — | fixed role→salary table (exists; story hires bypass the market on purpose) | — |
| take_loan | $ | — | 1k..250k, shark 18%/wk (exists — the STORY loan keeps its rate) | — |
| spend | $ | label | era_spend_cap, never below zero (exists) | — |
| set_budget | $/wk | lane name | lanes enum grows to `ads, content, referrals, outbound, sales, care, rnd, office`; legacy cat `marketing` maps to `ads`; era_spend_cap | D (04) |
| **push_lead** (new) | heat delta | lead name (fuzzy) | v clamped ±40, weeks:1; sentinel flags a cat matching no live lead; Enterprise only | D (05) |

Every op above marked "exists" already has its executor case in BOTH engines
(`_apply_dm_effects` and `ApplyDmEffects` were verified case-for-case in the
spine survey); `push_lead` and the `set_budget` cat widening land in both
executors in Wave D.

Deliberately NOT ops (desk/journal actions only, as the owning lanes chose):
open_role (02: desk API), repay/SIGN a bank note (06: desk two-step),
commit_bet/ship (07: desk + dice at press), produce/buy machine (09:
steppers), accept M&A / ring the IPO bell (08: journal two-tap), fire (02:
desk, severance quoted before the second press). Rule for any future op: it
exists only where a written move plausibly does the thing in fiction AND the
engine can clamp it; lands in all six sites + twin executor test in the same
commit.

### The STATUS catalog registry (additions across lanes — names are unique, checked)

| status | keys | kind | owner |
|--------|------|------|-------|
| price_war | fair_mult 0.92 | condition | 03 |
| outshipped | adopt_mult 0.85 | condition | 03 |
| rival_stumbled | adopt_mult 1.25 | buff | 03 |
| winter_watch / boom_watch | (banner/DM only) | condition / buff | 03 |
| funding_winter | val_mult 0.6 · amt_mult 0.7 · spread_mult 1.25 · dis raise | condition | 03 |
| boom | val_mult 1.3 · amt_mult 1.3 · spread_mult 0.9 · adv raise | buff | 03 |
| collections_calls | morale_wk −1 · dis raise | condition | 06 |
| sticky_release | churn_mult 0.75 | buff | 07 |
| feature_buzz | adopt_mult 1.3 | buff | 07 |
| board_delight | adv raise · morale_wk 2 · hype_wk 3 | buff | 08 |

No collisions with the existing 18-entry catalog or between lanes. NEW effect
keys (`fair_mult`, `val_mult`, `amt_mult`, `spread_mult`) are read ONLY by the
new helpers their owning specs define — the existing section-8 status loop
(adopt/churn/arpu) stays untouched by them. 07 is the first consumer of the
dormant `velocity_mult` key. The DM may install any of these BY NAME through
the existing `status` op; magnitudes live in the catalog, both engines,
byte-synced.

---

## 8. THE SAVE-COMPAT RULES

- **VERSION stays 2** (both engines; Godot `SaveSystem.VERSION`, Unity
  `RunSave.Version`). The Godot loader hard-rejects other versions — so never
  bump for additive fields. Bump only for a semantic break, and then ship a
  migrator, not a reject.
- **Godot**: every new state field is a `var` with its default in
  `game_state.gd`; the loader (`for k in sd: if k in state`) leaves absent keys
  at default. Read every nested dict with `.get(key, default)`. New collections
  (`open_roles`, `applicants`, `leads`, `bets`, `loans`, `board`, `hw`)
  default `[]`/`{}`/`0`.
- **C#**: every new field gets `[JsonProperty("exact_godot_key")]` + a field
  initializer; guard nested lists with `?? new`. JSON keys equal the Godot save
  keys byte-for-byte.
- **Migrations by construction** (no migrator code):
  - loans: `loan_principal` (shark) keeps its meaning; deliberate bank loans
    are a NEW `loans: []` list — old saves simply have none.
  - marketing split (04's rule): `budgets` gains `ads/content/referrals/
    outbound`; on load, a legacy `marketing` key maps into `ads` and is
    dropped — an old save spends identically until the player touches the
    mix; `pnl.marketing` stays the sum for all back-compat readers.
  - leads: absent ⇒ `[]`; a mid-run Enterprise save keeps raw adds until new
    adds spawn as leads (spawning covers new adds only).
  - tax (06's rule): no accrual state; the weekly EBT charge simply starts on
    the first profitable office-era week after load; the loss-carryforward
    counter defaults 0.
- **The pin test (both suites)**: a frozen pre-wave save fixture
  (`game/tests/fixtures/save_v2_prewave.json`, mirrored under
  `unity/Runway.Core.Tests/fixtures/`) must LOAD and TICK 4 weeks with no
  error, finite cash, and every new field at its default. This check is added
  in Wave A and never removed.

---

## 9. THE ERA LADDER (scale-progressive depth — single source)

What unlocks/deepens where. A subsystem spec may not gate anything on era
except as written here. "—" = inactive; "≤" carries forward.

| subsystem | garage | coworking | office | floor | hq |
|-----------|--------|-----------|--------|-------|----|
| 01 catalog | full: offers, prices, itemized costs (this IS the garage game) | mix weights matter | fixed_lines scale with era | ≤ | ≤ |
| 02 labor | story hires only (`hire` op; cap 2) — no market | **market opens**: roles, applicants, market-salary line | HR process: severance owed, 12-wk review cycles, raises, poachable stars | managers + span of control; recruiter retainer ($1,500/wk, pipeline ×1.75) | departments, seat steppers, full corporate roster |
| 03 rivals | 2 rivals, drift + occasional move (current) | **full action table weekly** | ≤ | acquisition sniffs possible | ≤ |
| 04 funnel | one blended marketing lever (current) | **4 channels unlock** | ≤ | ≤ | ≤ (analytics fog is orthogonal, all eras) |
| 05 pipeline (Enterprise) | founder-led: max 2 live leads | 4 | 6 | 10 | 15 |
| 06 finance | shark bridge loan only (banks don't lend to garages) | **the bank returns your calls**: real books (P&L v2), notes with terms preview; forecast from coworking | **taxes begin**: 20% of weekly EBT, loss carryforward; honest rates f(runway, revenue, era); standing contracts | net-30 invoicing; covenant/default repricing on missed notes | sweep account pays on idle cash |
| 07 roadmap | 1 bet slot (you can chase one thing) | 2 slots | 3 + hardening sprint standing bet | ≤ | ≤ |
| 08 board | a closed round = an angel and a handshake (soft covenant) | first real term sheets; growth covenant + review countdown | option pool written pre-money; investor updates buy goodwill | **board seats, strike ladder, CEO-coach commitments**; secondaries at goodwill ≥2 | exit-grade governance; clean quarters open the IPO window |
| 03 macro | on at all eras (the world always has weather) | ≤ | ≤ | ≤ | ≤ |
| 08 M&A | — | acqui-hire floor when dying | rival acquisition offers (rare) | strategic acquirer offers | ≤ |
| 09 production (Hardware) | hand-built batches, tiny capacity, no breakdowns | + equipment assets, breakdown rolls | + carrying cost formalized in pnl | ≤ | ≤ |

Pedagogy of the ladder: each unlock is the layer that genuinely appears at that
stage of a real company, and its first receipt line says so (06's
`"now you're on the radar: the taxman's cut: −$412 (20% of EBT $2,062 — profit
after interest)"` is the model).

---

## 10. THE UI GRID

**Verdict on 'no new tabs': VETOED — one 10th tab, "the bank."** Math: the
sheet is 1240 wide; 10 tabs need pitch 120 (24 + 10×120 = 1224 ≤ 1240; buttons
118×44; the longest label "the street" ≈ 110px at 23px hand font — fits). The
`_Clipboard` ring formula and the button row change from 133 → 120 TOGETHER in
both engines (the ring has desynced from the row twice before; a shot test
guards it). The ledger at 760px content height cannot host borrow/repay,
forecast, tax and break-even without scrolling — and finance deserves a desk.

Tab map (10 tabs, pitch 120): `vitals · the ledger · the bank · pricing ·
customers · product · crew · cap table · the street · threats`

| tab | grows | subsystem |
|-----|-------|-----------|
| vitals | debt-total line (all notes); hype spark arrives from product | 06/07 |
| the ledger | 04's 8-row lever rebuild (4-channel sub-block at 58px + org levers at 62px); compact P&L + `bank & the state` line + break-even suffix | 04/06 |
| **the bank** (NEW) | 06's whole desk: quote + cost-of-capital, borrow/term steppers + SIGN, notes list with repay, net+revenue sparklines, forecast, tax block, full grouped statement | 06 |
| pricing | 01's five-state machine: LIST · DETAIL (full-pane offer economics) · WRITE-IN · REVIEW | 01 |
| customers | non-Enterprise: today + 04's funnel reads at analytics gates; Enterprise: 05's stage board, lead chips, logos strip, teaching footer | 04/05 |
| product | 07's roadmap board (capacity, bet cards, progress, READY) + 09's BENCH strip on Hardware runs | 07/09 |
| crew | 02's grown roster rows + raise/let-go, open roles, applicant cards — behind the roster/hiring toggle | 02 |
| cap table | 08's pool slice, covenant + strikes + stage lines, offer/window banner | 08 |
| the street | 03's macro banner + 4-line rival blocks (posture, action log) | 03 |
| threats | attention items sev ≥2; 03's price-war row | spine/03 |

Conventions (all new desks): the cursor-y idiom with measured wrap (`_wrap_h` —
never assume one line); fixed x-slots (label x=10, value x≈430–520, steppers at
x=1000/1064, 52×46); the stepper idiom = −/+ Button pair over a value-ladder
function (`LEVER_STEPS` / `_price_step` pattern — new ladders: salary, borrow,
produce, channel); list renders cap at 6 cards + `"+N more"` line — the binder
never scrolls. New surface allowed ONLY: the bank tab, the garage HUD ticker
(§4), in-tab page states (01's DETAIL panes, crew's roster/hiring toggle), and
cards/steppers inside existing tabs. No modals. Full rulings + the acceptance
bar: §11.

---

## 11. THE INTERFACE MAP (consolidated — the owner reads THIS to see the UI evolve)

Every lane spec carries an `## INTERFACE DELTA` table (`surface | exists
today? | CHANGE or ADD | exactly how | why the player needs it`) — all nine
are on disk and this section is their merge, per surface, with collisions
ruled. Budget baseline: binder content = **1160×760px**; a text row at 24–30px
≈ 34px; a stepper row ≈ 46–64px. Rulings here are binding; approval authority
for new surfaces sits in this file.

**THE ACCEPTANCE BAR (every surface, every lane)**: a new interface ships only
at the bar of the existing game — (a) CLEAR: readable first pass by a tired
player; (b) SELF-EXPLAINING: concepts named in real business terms, a teaching
line where a number first appears; (c) GREAT INTERACTION: no dead ends, every
state reachable and leavable, destructive acts behind a two-press confirm;
(d) BEAUTIFUL in the game's hand-drawn language — pen labels, measured wobble,
cursor-y layout, never a SaaS panel. **The binding design system is
`docs/design/10-interface-language.md`** (produced in parallel) — where a
lane's coordinates or styles disagree with it, the language doc wins.
Utilitarian bolt-on rows get redesigned, not merged.

**vitals** — 06: loan line becomes `DEBT $X across N notes (worst Y%/wk)`.
07: hype spark arrives from product at (10,580) 1120×120. 03 adds no vitals
row (season reads live on the street banner). Fits (~740 worst). No collision.

**the ledger** — THE WAVE'S HOTTEST SURFACE; three lanes redesigned it and two
are incompatible as written:
- 04-funnel (Wave D): full-width rebuild — marketing lever becomes a 4-row
  channel sub-block at 58px pitch (ads y96 · content y154 · referrals y212 ·
  outbound y270 + header y62, divider y333), org levers repositioned to 62px
  pitch (y340–526), unit-econ/P&L/bottom-line at fixed slots y592/626/660/694,
  y734 = warnings-else-rules, with an explicit drop rule. Self-consistent,
  measured, ACCEPTED as the ledger's final lever layout.
- 06-finance wrote its bank block into the ledger's RIGHT half (levers
  compacted to x10..560) — **written before the bank tab existed; RULING: the
  right-half compaction is REJECTED; 06's bank content RELOCATES to THE BANK
  tab** (quote line, cost-of-capital line, borrow/term steppers + SIGN, notes
  list with [repay], era-state panels, net + revenue sparklines, forecast
  line, full grouped statement per §2). What 06 KEEPS on the ledger: the
  one-line `the bank & the state: interest · principal · tax` (slot shared
  with 04's y626/y660 stack), the bottom line's `· break-even 34 (16 now)`
  suffix, and the merged-warnings rule at y734. The ledger title stays
  "where this week's money goes"; 06's "money, debt, and the taxman" titles
  THE BANK.
- 02 (severance/recruiting) and 09 (production/subcontract/equip_upkeep/
  carrying) append fragments to the "out:" line. RULING: the out-line prints
  at most its width; overflow closes with `· +N lanes — the bank keeps the
  full books`. The full statement always lives on the bank tab.

**the bank (NEW 10th tab — approved, §10 math: 10 tabs at 120px pitch, ring
formula synced in both engines)** — sole owner 06; hosts everything relocated
above at full width plus the tax block and era-unlock states ("no bank
answers a garage — only the shark does"). ~640px worst. Fits.

**pricing** — 01 rebuilds the tab as a FIVE-STATE machine: LIST (rows + status
+ ▸/−/+ at the house x-slots) · DETAIL(i) (full-pane single offer: back
button, two-press drop, price steppers + contribution margin, itemized
variable/fixed cost lines each on steppers, break-even line, weight/shelf at
office+, mini P&L at floor+) · WRITE-IN · REVIEW (LLM-priced proposal card,
confirm/tear-up) · plus the write-in arrival state. Desk-local state, NOT
saved; every state reachable and leavable (◂ all offers everywhere). ACCEPTED
— it is the reference implementation of the acceptance bar. No other lane
touches pricing (verified).

**customers** — 04 puts its SPEND controls on the ledger, so the collision I
expected here dissolves: 05 owns the Enterprise branch wholesale
(`_tab_customers_enterprise()`: stage-board columns MEETING·PILOT·CONTRACT
(+PROCUREMENT at office+), 64px lead chips with heat words and death
countdowns, signed-logos strip ~y620, teaching footer ~y700, coach era line
~y734; chips have NO controls — the pipeline is pushed by written moves), and
the non-Enterprise page stays byte-identical today, gaining only 04's funnel
READS at the existing analytics gates. RULING: 04's customer-tab reads render
on the non-Enterprise branch only; the blended-CAC line on the ledger serves
both.

**product** — 07 rebuilds the top: debt jar shrinks to (300,10) 64×84 with a
one-line triple-cost caption, debt spark REMOVED, hype spark MOVED to vitals,
capacity header y100, bet cards at 118px pitch from y140 (uncommitted /
committed+progress-bar+stand-down / READY states), hardening row ~y520,
footer y700. 09 adds THE BENCH framed strip for Hardware runs — but its
claimed band y600–850 EXCEEDS the 760px pane and collides with 07's hardening
row + footer. **RULING (Hardware runs only): bet cards cap at 2 visible
(ladder gives garage 1 slot), hardening row moves into the card stack, 07's
footer line yields, THE BENCH gets y470–740 (header + 6 rows at ~38px);
09 re-measures to that band. Non-Hardware runs: 07's layout verbatim.**
Both lanes' bang predicates become §4 registry rows (bet_ready,
debt_critical, stockout, overstock, machine_down).

**crew** — 02's rows (roster rows grown to loaded-cost + skill dots + wants-
raise inline, +10% raise and two-press let-go buttons, OPEN ROLES rows with
advert steppers, ≤6 applicant cards at 66px, recruiter row floor+, payroll
totals, desk-footer rules line, morale spark shrunk to 560px) total ~1,040px
with any real roster — over the pane. **RULING stands: crew splits into two
page modes behind a pen toggle in the header — `roster / hiring` (in-tab, not
a new tab). Roster mode: employee rows + raise/let-go + payroll totals +
morale spark. Hiring mode: open roles + applicant cards + recruiter row +
footer rules. Both fit (<640 worst).** Journal lines (arrivals, decay with
cause, raise asks, resignations with ratio, review week, benefits, span,
severance receipt) are ACCEPTED as written — exemplary pedagogy.
FLAG for 10-interface-language pass: the roster row now carries 5 data + 2
buttons + 1 inline warning — at the bar it likely needs the DETAIL-view idiom
from 01 (row → card) rather than more density; redesign, don't cram.

**cap table** — 08's layout is self-measured and ACCEPTED: 4th pie slice
(option pool, YELL), reworded dilution-preview line teaching pre/post-money,
offer/window banner (40,520), board header (40,568), covenant line (40,610),
strikes/goodwill record line (40,654), era stage line (40,698). Journal takes
the signing math line, the secondary card (stage ≥3, goodwill ≥2), the
two-tap M&A SELL and IPO bell cards, review reminders and receipts. My
earlier "raise preview collapses" ruling is WITHDRAWN — 08 placed everything
without it. Finale gains the `founder_banked` chip.

**the street** — 03: macro banner strip (y≈56..130: season word + authored
shock line with weeks left), rival blocks grow 2→4 lines (posture words ·
"fights on" read · last-3 action log — never raw floats), investor section
compresses to one line each when the hq third rival exists. Measured-wrap
stacking handles heights. ACCEPTED; fits at every era.

**threats** — 03 adds the price-war row with live percent; 08's statuses ride
the existing auto-list; spine adds attention items sev ≥2 (cap 12 lines +
"+N more", severity 3 first).

**garage HUD** — spine's ticker only (§4). 07's OR-chain edit at
garage_view_screen.gd:450 is superseded by the registry; 09 confirms no room
changes. The room stays the DM's stage.

**journal** — receives the bulk of the wave's pedagogy: 02's seven labor
lines, 05's eight pipeline receipts + two push receipts (≤6 pipeline lines/wk
then "…and N more moved"), 06's eleven finance receipts, 07's ship/payoff/
READY/progress lines, 08's formation/review/M&A/IPO receipts + week-ahead
cards, 09's six factory receipts. RULING: weekly `rep.lines` cap at 14 in the
spread, overflow closes with `+N more — the binder keeps the books`; BIG
beats (`rep.events`) always render with ⚡; per-lane per-week line caps as
specced (05 ≤6, others ≤4) so no lane floods the page.

**coach** — 05's era line on the customers tab and 09's four bench lines join
the pool; spine adds one unlock chip per era-ladder unlock (the bank at
coworking, hiring at coworking, channels at coworking, tax at office, board
review at floor); max one new chip per week; the 3-chip first-run tour is
untouched.

**bang system** — REPLACED wholesale by the attention registry (§4). Every
lane's "add to `_bangs`" row implements as registry rows; the hardcoded
conditions in binder.gd/garage_view_screen.gd/BinderScreen.cs/GarageScreen.cs
are deleted the week the registry lands (Wave A).

New-surface scorecard: approved — the bank tab, the HUD ticker, the crew
in-tab toggle, 01's five-state pricing machine, 05's Enterprise branch page,
09's BENCH strip (at the re-ruled band). Vetoed — any 11th tab, modals,
scrolling desks, 06's ledger right-half bank block (relocated), 09's y850
overflow (re-measured), second garage widgets. A lane needing more surface
than ruled here amends THIS section first.

---

## 12. TEST STRATEGY

- **Twin suites grow in lockstep**: a check lands in `game/tests/
  sim_engine_test.gd` first, then same-order in `unity/Runway.Core.Tests/
  Program.cs` (the porting law that built the current 82). Target per
  subsystem: 6–10 checks (Wave A ≈ +12: pnl identity both lines, interest
  before the record, first EBT tax + loss carryforward, price_offer validator
  pin, attention registry rows, save fixture). End state ≈ 150 per engine.
- **P&L identity test** (§2): 8 mixed weeks (budgets on, offers priced, a loan,
  office era, Hardware variant), assert both identity lines every week.
- **Determinism extension**: two identical states tick 12 weeks with EVERY new
  subsystem active → equal state serialization, per engine. Plus the salt
  canary: first draw of each registered salt at (seed 42, wk 5) pinned as a
  table — per engine, not cross-engine (§3 note).
- **Old-save load test**: the §8 fixture — load, 4 ticks, no error, defaults.
- **Attention tests**: one per registry row (§4).
- **Op executor tests**: synthetic effects arrays through both executors,
  receipts asserted; the three-list-equality pin per engine.
- **Headless full-run smoke**: extend `RUNWAY_FULLRUN` with a scripted 40-week
  driver that touches each desk path (wk2 budgets, wk3 price, wk4 open role,
  wk6 hire from applicants, wk8 borrow, wk12 ship a bet, wk20 answer a board
  review) and asserts: reaches wk40 alive or dead-cleanly, `attention_items`
  never throws, pnl identity holds each week. Mirror on Unity via the existing
  RunDriver + Editor batch probe idiom (`UnityFlow.Probe`).
- **Shot guards**: one binder shot per changed tab (both engines' existing
  shot harnesses); the 10-tab ring alignment gets its own shot.

---

## 13. CONFLICT CALLS (owner + interface, one line each)

- **learning vs learning**: there are TWO curves ON PURPOSE — catalog's
  `served_total` curve discounts SERVE cogs (01 owns), hardware's
  `produced_total` curve discounts BUILD unit cost (09 owns; subcontracted
  units earn none). Receipts name which curve spoke; neither reads the other.
- **poach vs morale**: rivals roll the poach (salt 31) and hand the named
  target to labor; labor owns the roster mutation + severance/none and emits
  the one-shot morale delta consumed by tick §4 next week; nobody edits
  morale directly.
- **macro vs valuation vs board, in `generate_offers`/`valuation`**: three
  writers, one pinned order — fair price first, then 03's status keys
  (`val_mult`, `amt_mult`, `spread_mult` via the new helpers), then 08's
  strike/goodwill repricing, then the existing trait `warmth_pct`. Each lane
  touches only its own factor.
- **channels vs gtm_cap**: funnel owns the reach term (replaces `mk_mult`);
  `gtm_cap` stays the closing clamp with its sell/sales-heads/sales-budget
  terms unchanged, reading TOTAL channel spend for its marketing term — one
  formula, funnel section owns it.
- **rival price cut vs offers**: rivals own the transient `price_war` status
  (`fair_mult`, demand-side, expires); they NEVER mutate `offer.fair_price` —
  the catalog's numbers are the founder's.
- **bets vs rnd budget**: while a bet is committed, the rnd budget feeds the
  bet's weeks first (roadmap owns routing); the passive +1/$1,200 drip applies
  only to uncommitted spend.
- **production vs adoption**: order pinned in §1 — produce (tick §7h), adopt
  clamped to stock and decrement it (tick §8); funnel computes demand, never
  stock.
- **severance vs finance**: labor computes severance (tenure-banded, era per
  §9 ladder) and writes the pnl lane next tick; finance only displays it.
- **M&A vs the finale**: 08 sets `exit_value`/`founder_banked` + flags and
  extends `_score()` with the banked chip; main's existing finale owns the
  ceremony — no second finale path.
- **tick §3b review cycle vs §9c board review**: both run on 12-week cadences
  — they are DIFFERENT clocks on purpose (comp reviews vs board reviews);
  labor owns `week % 12`, board owns `review_week`; neither reads the other.

---

## 14. KNOWN PARITY GAPS TO CLOSE IN WAVE A (found during spine survey)

1. **Unity catalog engine half missing**: `cost_lines` / `fixed_lines` /
   `offers_fixed_wk` / pnl `offer_fixed` exist in Godot only; Unity `Pnl` and
   burn lack the lane (`SimEngine.cs` ~line 653, `GameState.cs` Pnl class).
   Port before anything else lands on the record.
2. **price_offer validator bug** (§7): present in both schema enums + both
   executors, absent from both `ALLOWED_OPS` — a DM reply that uses it is
   rejected wholesale. Two-line fix + pin test.
3. Unity `WeeklyReport` has no dedicated pnl copy — fine (LastPnl is the
   record), but the identity test must read the same source both sides:
   Godot `get_meta("pnl")`, Unity `LastPnl`.
