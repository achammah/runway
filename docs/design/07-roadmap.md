# 07 — PRODUCT ROADMAP (feature bets) · desk: `product`

Business-sim plan §7. The one law holds: **the ENGINE owns every number, the DM owns
every sentence, the house dice resolve outcomes.** RUNWAY! is an economic simulator
first — this desk teaches capacity allocation, opportunity cost, tech-debt interest
and launch risk *by name*, with receipts.

**Decisions taken (the three DECIDEs in the brief):**
1. **R&D output splits, it does not double.** While a bet is committed, the rnd
   budget's output becomes bet progress; uncommitted, it stays the existing
   +1 quality per $1,200 (salt-77 path untouched). One team, one throughput —
   opportunity cost is the lesson, so it must be real.
2. **Ship resolution is world-owned, on schedule.** A bet that completes is marked
   READY; the house dice roll it on the *next* weekly tick (salt 95). One "brace
   week" of visible anticipation: the player can pay down debt, stack `build`
   advantage — the existing mechanics move the odds. No new UI ceremony.
3. **LLM proposes kind+ambition from fixed enums; the engine prices everything
   from tables.** Ambition drives DC — a number — so the LLM can only pick a rung
   of an authored ladder, never invent a magnitude (same guarantee as
   STATUS-by-name installs). Full justification in §10.

---

## 1. State model (both engines, save-compat)

New field on GameState (Godot `game/src/core/game_state.gd`, Unity
`unity/Assets/Scripts/Core/GameState.cs`):

```gdscript
var bets: Array = []          # plain dicts, JSON-native (like offers/statuses)
var platform_level: int = 0   # 0..4 — shipped platform bets compound velocity
```
```csharp
[JsonProperty("bets")] public List<Bet> Bets = new List<Bet>();
[JsonProperty("platform_level")] public int PlatformLevel = 0;
```

One bet (JSON field names identical across engines; Unity `Bet` class mirrors
`Offer`'s `[JsonProperty]` idiom):

| field | type | default | meaning |
|---|---|---|---|
| `id` | String | — | `"bet_w<week>_<n>"`; the standing bet is exactly `"hardening"` |
| `name` | String ≤28 | — | card title (authored or LLM-dressed) |
| `desc` | String ≤90 | — | card line |
| `kind` | String | — | `quality` \| `retention` \| `reach` \| `debt` \| `platform` |
| `ambition` | int 1–3 | 1 | rung on the authored cost/DC/payoff ladders |
| `cost_rnd_weeks` | float | — | engine-priced from tables, never free-form |
| `progress` | float | 0.0 | accumulated R&D-weeks (continuous, no RNG needed) |
| `committed` | bool | false | the team is pointed here (invariant below) |
| `committed_week` | int | 0 | week of the latest commit (DM flavor) |
| `ready` | bool | false | complete; the dice roll it next tick |
| `shipped` | bool | false | resolved |
| `shipped_week` | int | 0 | week it shipped |
| `band` | String | "" | `brilliant`/`fine`/`risky`/`backfired` once shipped |
| `era` | String | — | era it was drawn in (drives era-refresh; no meta needed) |

Invariants (engine-enforced): `committed` true on at most `wip_cap(era)` unshipped
bets; `ready ⇒ not committed`; `shipped ⇒ not committed and not ready`;
`"hardening"` always present exactly once among unshipped bets. Shipped bets stay
in the array as history, pruned to the **last 8** (slice, oldest first out).

**Save-compat:** Godot `save_system.gd` adds `"bets": state.bets` and
`"platform_level": state.platform_level` to `save_run` (generic `k in state`
loader restores them; VERSION stays 2 — additive). Unity round-trips via
JsonProperty automatically. Old saves: missing → `[]` / `0` → `refresh_bets`
reseeds the board on the next tick. Nothing else migrates.

---

## 2. Scaling by stage — what unlocks at which era

Product development *changes shape* as a company grows; the desk grows with it.
Exact table (engine consts, both twins):

| era | board slots | WIP cap (committed at once) | ambition cap | founder hands-on | QA net | platform bets | maintenance tax |
|---|---|---|---|---|---|---|---|
| garage | 2 | 1 | 2 | +0.25 wk/wk | — | — | — |
| coworking | 3 | 1 | 3 | +0.25 wk/wk | — | — | — |
| office | 3 | 2 | 3 | — | yes | — | — |
| floor | 4 | 2 | 3 | — | yes | yes | — |
| hq | 4 | 3 | 3 | — | yes | yes | yes |

```gdscript
const BET_SLOTS := {"garage": 2, "coworking": 3, "office": 3, "floor": 4, "hq": 4}
const BET_WIP := {"garage": 1, "coworking": 1, "office": 2, "floor": 2, "hq": 3}
```

- **garage — one bet, founder-built, cheap and fast.** WIP 1; ambition-3 cards are
  never drawn (and LLM ambition is clamped to 2); the founder personally adds
  +0.25 R&D-wk/wk to the pool while anything is committed. *Analogue: pre-team
  startups run WIP-1 single-piece flow; the founder is the capacity.*
- **coworking — first engineers multiply progress.** Ambition 3 unlocks; engineer
  contribution (§4) starts mattering as hires land. Founder still hands-on.
- **office — parallel bets + QA/process.** WIP 2 (capacity pool SPLITS — see §4:
  parallelism spreads the same throughput, teaching WIP cost). Founder hands-on
  ends (managers manage). **QA net:** a ship-roll miss by 3–4 is softened from
  `backfired` to `risky` — staging and review truncate the tail, they don't raise
  the ceiling. *Analogue: process reduces variance, not mean.*
- **floor — platform investments.** `platform` kind enters the draw pool: cost
  fixed **10.0** R&D-weeks, DC fixed **12** (known work, big scope), payoff
  `platform_level += 1` — every level multiplies ALL future bet progress ×1.15
  (compounding infrastructure). *Analogue: internal platforms/deploy tooling pay
  in velocity, not features.*
- **hq — innovation vs maintenance portfolio.** WIP 3, and the **maintenance
  tax**: if no `debt`/`platform` bet shipped in the last 10 weeks and none is
  committed, tech debt drifts +0.8/wk extra, receipted as
  `organizational entropy: debt +0.8 (no maintenance shipped in 10 wks)`.
  *Analogue: large orgs pay a standing maintenance share or rot.*

---

## 3. Slot rules & refresh (salt 97)

`SimEngine.refresh_bets(state, rep)` — called every tick (step 5c, §7), idempotent:

1. **Standing law:** if no unshipped bet with id `"hardening"` exists, append it
   fresh (`progress 0`): `{id:"hardening", name:"Hardening sprint", desc:"No
   features. Pay the debt down before the debt collects you.", kind:"debt",
   ambition:1, cost_rnd_weeks:2.5, era:state.era}`. Never discarded by refresh.
2. **Era refresh:** remove every bet with `not committed and not ready and not
   shipped and id != "hardening" and era != state.era`. (Committed work survives
   a move — you don't lose progress; abandoned candidates don't. *Analogue:
   roadmaps don't survive a stage change.*)
3. **Refill:** `open = BET_SLOTS[era] − count(unshipped, non-hardening)`. Draw
   `open` cards from the authored pool (§11) via `_rng(state, 97)`
   (`r.randi_range` over eligible indices), excluding (a) names already on the
   board, (b) names among the last 8 shipped, (c) `ambition > 2` while era is
   garage, (d) `kind == "platform"` while `era_index < 3`. If exclusions empty
   the pool, drop exclusion (b) first; (c)/(d) never drop. Drawn card fields:
   `id = "bet_w%d_%d" % [week, n]`, `era = state.era`, `cost_rnd_weeks` and DC
   from §5 tables.
4. If `drawn > 0`: `rep["bets_refreshed"] = drawn` and report line
   `"%d new bets on the roadmap board" % drawn`. This flag is the LLM trigger
   (§10) — the engine itself never calls the LLM.

**Exact appearance triggers, spelled out:** (a) first tick of a run (board empty →
seeded); (b) the tick after `state.era` changed (step 2 discards stale candidates,
step 3 refills — era changes happen outside the tick, the `era` field on each bet
is the detector, so no meta/marker is needed and saves stay compatible); (c) the
tick a bet ships (its slot refills in the same tick, ship runs first).

Commit API (desk + tests): `commit_bet(state, id) -> bool` (refuses ready/shipped;
if at WIP cap, refuses — the desk uncommits explicitly first),
`uncommit_bet(state, id)`, `committed_bets(state) -> Array`,
`any_bet_ready(state) -> bool`. Commit/uncommit call
`state.log_action("roadmap: pointed the team at '%s'" / "roadmap: stood down '%s'")`
— the DM's `recent_actions` sees every allocation decision.

---

## 4. Progress math — capacity allocation, priced honestly

One weekly **capacity pool** in R&D-weeks (the industry's person-week currency;
$1,200 ≈ one junior-engineer loaded week in this economy — the constant the
existing lever already uses):

```
vel    = Π status.velocity_mult over active statuses      # crunch 1.35 · burnt_out 0.6 · founder_flow 1.15
drag   = clampf(1.0 − maxf(tech_debt − 40.0, 0.0) / 120.0, 0.5, 1.0)
plat   = 1.0 + 0.15 * platform_level
eng_pw = Σ over employees with role containing "engineer": 0.25 * clampi(int(e.get("skill", 3)), 1, 5)
fdr_pw = 0.25 if era_index() <= 1 and committed_bets not empty else 0.0
pool   = (b_rnd / 1200.0 + eng_pw + fdr_pw) * vel * drag * plat
share  = pool / committed_count                            # even split across committed bets
```

Per committed bet: `bet.progress += share`. Uncommitted weeks: the existing base
path runs verbatim (salt-77 seeded remainder, +1 quality per $1,200). **Debt
paydown `tech_debt −= b_rnd/1500` runs in BOTH branches** (ambient hygiene —
unchanged from today).

- *Capacity allocation:* engineers contribute `0.25×skill` (skill 1–5 → 0.25–1.25
  wk/wk; sub-1.0 is the honest meetings/support/review tax on nominal capacity).
  `skill` is the labor-market field (design 02); `int(e.get("skill", 3))` default
  keeps pre-market saves and current hires working at 0.75.
- *Opportunity cost:* committed spend ships no base quality. The desk says so in
  copy (§8) and the P&L already shows the rnd lane.
- *WIP cost:* the pool splits evenly across committed bets — two parallel bets
  finish later on average than the same two in series. The lesson is emergent
  from the arithmetic, not scripted.
- *Tech-debt interest* (Cunningham): every point above 40 taxes throughput
  linearly to a −50% floor at 100. Exact: drag(40)=1.0, drag(90)=0.5833,
  drag(100)=0.5.
- *Compounding platforms:* ×1.15 per level, multiplicative with everything.
- This is the **first consumer of the STATUS catalog's `velocity_mult`**, dormant
  since it was authored — crunch, burnt_out and founder_flow finally bite.

Completion (inside the tick's R&D block, step 9): when `progress >=
cost_rnd_weeks`, set `ready = true`, `committed = false`, report line
`"READY TO SHIP: '%s' — the dice roll next week"`. Ready bets ship next tick
(§5), so there is exactly one brace week.

---

## 5. The ship roll (salt 95) — launch risk, priced

`SimEngine.ship_ready_bets(state, rep)` — tick step 5b, before adoption math so
payoff statuses land the same week they ship. For each `ready and not shipped`
bet, in array order:

```
r      = _rng(state, 95);  roller = func(): return r.randi_range(1, 20)
ctx    = roll_d20_ctx(state, "build", roller)      # advantage/disadvantage/luck all apply
dc     = 12 if kind == "platform" else DC_BY_AMBITION[ambition − 1]   # [8, 11, 14]
band   = margin_band(int(ctx.total), dc)
if era_index() >= 2 and band == "backfired" and int(ctx.total) − dc >= −4:
    band = "risky"                                  # THE QA NET (office+)
apply payoff (below); bet.shipped = true; bet.ready = false
bet.shipped_week = state.week; bet.band = band
```

The roll is the existing house die: `focus ≥ 4` and `founder_flow` grant
advantage on build, `tech_debt > 70` disadvantage, luck bends the extremes —
all pre-existing, all now levers the player can pull during the brace week.
*Analogue: launch outcomes disperse — most shipped features underperform their
expectation; scope widens the dispersion (big-bang integration risk); de-risking
(senior review = build stat, calm codebase = low debt, flow) moves real odds.*

**Cost / DC ladders** (engine consts):

```gdscript
const COST_BY_AMBITION := [3.0, 5.0, 8.0]     # R&D-weeks; hardening 2.5; platform 10.0
const DC_BY_AMBITION := [8, 11, 14]           # platform fixed DC 12
```

**Payoff — one integer magnitude matrix, zero runtime rounding** (kills the
Godot-round vs C#-banker's-round twin trap):

```gdscript
# BET_PAYOFF[ambition − 1][band]  ·  band index: brilliant 0, fine 1, risky 2, backfired 3
const BET_PAYOFF := [[6, 4, 2, 0], [11, 7, 4, 0], [15, 10, 5, 0]]
```

`units = BET_PAYOFF[amb−1][band]`, then per kind:

| kind | payoff applied | clamp |
|---|---|---|
| `quality` | `product += units` | `mini(product, 100)` |
| `retention` | `add_status("sticky_release", units)` (0 units → no status) | catalog |
| `reach` | `add_status("feature_buzz", units)` | catalog |
| `debt` | `tech_debt −= units * 3` (18/12/6 · 30/20/10 · 45/30/15) | `maxf(0.0)` |
| `platform` | `platform_level += (1 if units > 0 else 0)` | `mini(4)` |

**Band side effects** (all kinds; `debt`/`platform` kind halves/zeroes the debt
penalty — punishing a refactor with debt is absurd):

| band | side effects | debt/platform-kind override |
|---|---|---|
| brilliant | `hype +8` | same |
| fine | `hype +3` | same |
| risky | `tech_debt +6` ("shipped hot") | debt kind: +0 · platform: +10 |
| backfired | `tech_debt +12`, `morale −6` | debt kind: debt +6 only, morale −6 |

All meter writes clamp (`clampi 0..100` for morale/hype, product ≤100, debt
0..100). New STATUS catalog entries, both engines, DM-installable by name like
every other:

```gdscript
"sticky_release": {"churn_mult": 0.75, "kind": "buff"},   # churn −25% while active
"feature_buzz":   {"adopt_mult": 1.3,  "kind": "buff"},   # adoption ×1.3 while active
```

Ambition duration/magnitude via `units` keeps the one-typed-catalog law:
magnitudes live in STATUS once; ambition buys *weeks*, never a new multiplier.

Expected-value honesty (document for balance review, receipts teach it in-game):
at build 3, straight roll — amb 1: 65% fine+, amb 3: 35% fine+ and 50% backfire
pre-QA-net. Ambition 3 only out-earns the base path for a prepared founder
(build 4–5, advantage, debt < 70). That asymmetry is the lesson, not a bug.

---

## 6. Tech debt — current teeth, plus two new ones

Already live (unchanged): debt > 40 → weekly outage roll `p = (debt−40)/250`
(salt 5) installing `outage_fallout`; debt > 70 → disadvantage on `build` rolls;
`eng == 0 and build < 4` → debt +1.5/wk; `b_rnd/1500` ambient paydown; DM
directive at debt ≥ 70; the binder's jar + outage odds.

New, from this design:
1. **Velocity drag on bet progress** (§4 `drag`) — debt now taxes *throughput*,
   not just risk. Exact, receipted on the desk: `TECH-DEBT INTEREST: −%d%%
   velocity` where `%d = int(round((1.0 − drag) * 100))`.
2. **HQ maintenance tax** (§2) — portfolio neglect compounds, receipted.

---

## 7. Weekly-tick integration (exact order + strings)

Insert into `weekly_tick` (Godot `sim_engine.gd` / Unity `SimEngine.cs`
`WeeklyTick`), byte-identical report strings:

- **Step 5b** (after tech-debt block, before rivals): `ship_ready_bets(state, rep)`.
  - `rep.events += "SHIPPED %s: '%s' — d20 %d%+d vs DC %d" % [band.to_upper(), name, d20, mod, dc]`
  - payoff receipt in `rep.lines` (the WHY, by kind):
    - quality: `"  → product v0.%d (+%d quality)" % [product, units]`
    - retention: `"  → customers stick: churn −25%% for %d wks" % units`
    - reach: `"  → word gets out: adoption ×1.3 for %d wks" % units`
    - debt: `"  → the codebase breathes: debt −%d" % (units * 3)`
    - platform: `"  → the platform compounds: all builds ×%.2f from here" % (1.0 + 0.15 * platform_level)`
    - backfired: `"  → nothing shipped worth keeping: debt +%d, the room deflates" % dpen`
    - QA net fired: `"  → the QA net caught the worst of it"`
- **Step 5c**: `refresh_bets(state, rep)` (line + `bets_refreshed` per §3).
- **Step 5d** (hq only): maintenance tax check (§2, exact line there).
- **Step 9, R&D block** becomes a branch: committed → §4 pool/split/progress with
  weekly line `"roadmap: '%s' — %d%% built" % [name, int(progress / cost * 100)]`
  per committed bet, plus the ready line on completion; uncommitted → existing
  base-quality path verbatim (its `"R&D shipped: product v0.%d"` line and salt-77
  draw byte-identical to today). Debt paydown in both branches.

Salts now in use after this wave: 4, 5, 6, 7, 9, 77, 88, 91, 93, **95 (ship
roll)**, **97 (bet draw)**. No other stream order changes — pins on existing
salts stay green.

**Bang conditions** (binder tab row, both engines): add `"product"` to the bang
tab list; visible when `SimEngine.any_bet_ready(state) or state.tech_debt >= 70.0`
— a bet about to roll, or debt critical.

---

## 8. Desk — product tab v2 (`binder.gd _tab_product` / `BinderScreen.TabProduct`)

Idiom: `_label`/`_icon`/`_ink_btn`/drawn pieces; content sheet 1160×760; same
coordinates in both engines. The old debt spark and hype spark are cut (no room;
the jar + odds line keep the debt read; the hype spark moves to `vitals` at
`(10, 580) 1120×120` — one `_spark` call).

```
y 0    icon "product" (10,6) · "v0.%d" (100,10,46)
       debt jar (_DebtJar) at (300,10) 64×84 · at (390,16,25):
       "debt %d · outage ≈ %d%%/wk · TECH-DEBT INTEREST: −%d%% velocity"   (PEN if debt ≥ 40)
y 100  "the roadmap — one team, %.1f R&D-wks/wk of capacity" (10,100,32)
       [capacity = §4 pool at current settings — the number teaches CAPACITY]
y 140  bet cards, one per unshipped non-hardening bet, 118px pitch:
  name.to_upper() (10,y,30) · "· %s, ambition %d" kind tag (follows name, 22, INK 0.55)
  desc (10,y+36,22, INK 0.65, w 690)
  "%s R&D-wks · LAUNCH RISK: clean ship ~%d%% (DC %d vs build)" (10,y+64,23)
  right side, one of:
    uncommitted → button "point the team →" (940,y+8) 200×50 (_ink_btn) → commit_bet + log + _refresh
    committed   → _BetBar (720,y+10) 320×34 (progress/cost fill, SAGE)
                  + "%d%% · ships in ~%d wks" (720,y+48,22)
                  + button "stand down" (1060,y+8) 90×50
    ready       → "READY — the dice roll next week" (720,y+20,27, PEN)
    (shipped bets don't render; they live in the journal)
~y 520 "── standing ──" (10,·,20, INK 0.4)
       hardening row: same card anatomy, always last, never discarded
y 700  "OPPORTUNITY COST: rnd money builds the committed bet — uncommitted, it
        polishes base quality (+1 per $1,200) · parallel bets split the same
        capacity" (10,700,20, INK 0.5, w 1100)
```

Odds preview (exact, shared helper `ship_odds_pct(state, bet) -> int` so desk and
tests agree; luck's extreme-bending is deliberately not previewed — luck is felt,
never advertised):

```
mod  = competences.build − 3
need = clampi(dc − mod, 2, 20)
p    = (21 − need) / 20.0
ctx  = roll_context(state, "build")
p    = 1 − (1−p)*(1−p) if ctx.advantage else (p*p if ctx.disadvantage else p)
return int(round(p * 100.0))
```

`_BetBar` drawn piece: wobbly ink rect outline (4px), horizontal SAGE fill at
`progress/cost`, exactly the `_DebtJar` construction rotated. Unity mirrors with
`DrawnUI` primitives. WIP cap full → uncommitted cards' button renders disabled
tint with `"team is at capacity (%d/%d)"` in place of odds.

Coach line (first time the board is non-empty): `"the roadmap is live — point the
team at a bet, or the R&D money just polishes what exists."`

---

## INTERFACE DELTA — every UI change, assessable standalone

One row per element. Positions are content-sheet coordinates (binder sheet
1160×760) identical in Godot (`binder.gd`) and Unity (`BinderScreen.cs`).

| surface | exists today? | change | exactly how (content, controls, position, states) | why the player needs it |
|---|---|---|---|---|
| binder · product tab — version header | yes | KEEP | `"v0.%d"` (100,10,46) + product icon (10,6) unchanged | anchor: the tab still opens on what the product is |
| binder · product tab — debt jar | yes | CHANGE | `_DebtJar` moves (160,92) 90×110 → (300,10) 64×84, same drawn piece | frees the sheet for the roadmap while keeping the debt read at the top |
| binder · product tab — debt caption | yes | CHANGE | old two labels ("tech debt:", "outage odds ≈ %d%% weekly") become ONE line at (390,16,25): `"debt %d · outage ≈ %d%%/wk · TECH-DEBT INTEREST: −%d%% velocity"`, PEN when debt ≥ 40 | debt's cost is now three-fold (outage, build rolls, velocity) — the new interest number is the lesson, printed where the jar is |
| binder · product tab — debt sparkline | yes | REMOVE | `_spark(_series("debt"))` row deleted | no room; jar + caption keep the read, history stays in metric_history |
| binder · product tab — hype sparkline | yes | MOVE | deleted here; re-added on `vitals` at (10,580) 1120×120, YELL, label "hype:" | hype is a company vital, not a product control; vitals has the space |
| binder · vitals tab — hype sparkline | no | ADD | one `_spark(_series("hype"))` at (10,580) 1120×120 under the market line | counterpart of the move above |
| binder · product tab — capacity header | no | ADD | `"the roadmap — one team, %.1f R&D-wks/wk of capacity"` (10,100,32), value = live §4 pool | names CAPACITY; the player sees what a week of their org is worth before allocating it |
| binder · product tab — bet cards | no | ADD | one card per unshipped non-hardening bet, 118px pitch from y 140: NAME upper (10,y,30) + kind tag `"· %s, ambition %d"` (22, INK 0.55); desc (10,y+36,22, INK 0.65, w 690); `"%s R&D-wks · LAUNCH RISK: clean ship ~%d%% (DC %d vs build)"` (10,y+64,23). Card states: uncommitted / committed / ready (next three rows) | the board itself: what could be built, at what cost, at what odds — the decision surface |
| binder · product tab — commit button | no | ADD | `"point the team →"` (940,y+8) 200×50, `_ink_btn` style; on press: `commit_bet`, `log_action`, `_refresh`. Disabled state when WIP cap reached: dimmed, text replaced by `"team is at capacity (%d/%d)"` | the allocation act — the desk's one real control; the disabled state teaches the WIP cap |
| binder · product tab — progress bar | no | ADD | new drawn piece `_BetBar` (720,y+10) 320×34: wobbly ink outline, SAGE fill at progress/cost; caption `"%d%% · ships in ~%d wks"` (720,y+48,22) | committed money must be visibly *going* somewhere, with an honest ETA |
| binder · product tab — stand-down button | no | ADD | `"stand down"` (1060,y+8) 90×50 on committed cards only; uncommits + logs | reversibility; switching cost is time, so the control must exist to make that a real choice |
| binder · product tab — READY state | no | ADD | committed controls replaced by `"READY — the dice roll next week"` (720,y+20,27, PEN) | the brace week: the player knows the roll is coming and can still move the odds (debt, statuses) |
| binder · product tab — hardening row | no | ADD | separator `"── standing ──"` (10,~520,20, INK 0.4), then one card with the same anatomy, always last, never removed | the standing maintenance choice must always be one press away |
| binder · product tab — footer copy | no | ADD | `"OPPORTUNITY COST: rnd money builds the committed bet — uncommitted, it polishes base quality (+1 per $1,200) · parallel bets split the same capacity"` (10,700,20, INK 0.5, w 1100) | names the tab's core economics in one line, always visible |
| binder · tab row — product bang | no | ADD | `"product"` joins the bang-label tabs (coral `!` at tab pos +(103,−12)); visible when `any_bet_ready(state) or tech_debt >= 70.0` | pulls the player to the desk exactly when a roll is imminent or debt is critical |
| garage HUD · binder corner bang | yes | CHANGE | `garage_view_screen.gd:450` OR-chain gains `or SimEngine.any_bet_ready(state) or state.tech_debt >= 70.0` (Unity twin same) | the room-level attention light must agree with the tab-level one |
| journal · outcome log — SHIPPED event line | no | ADD | tick `rep.events` (⚡-prefixed by existing wiring): `"SHIPPED %s: '%s' — d20 %d%+d vs DC %d"`; backfired appends `" (disadvantage: %s)"` when applicable | the ship receipt: die, DC, band, and WHY — launch risk with its work shown |
| journal · outcome log — payoff receipts | no | ADD | `rep.lines`, one per ship, per kind (§7): `"  → product v0.%d (+%d quality)"` / `"  → customers stick: churn −25%% for %d wks"` / `"  → word gets out: adoption ×1.3 for %d wks"` / `"  → the codebase breathes: debt −%d"` / `"  → the platform compounds: all builds ×%.2f from here"` / backfired `"  → nothing shipped worth keeping: debt +%d, the room deflates"` / QA `"  → the QA net caught the worst of it"` | every delta arrives with its cause — the receipts are the teaching tool |
| journal · outcome log — READY line | no | ADD | `rep.lines`: `"READY TO SHIP: '%s' — the dice roll next week"` | announces the brace week in the week's own record |
| journal · outcome log — progress line | no | ADD | `rep.lines`, per committed bet: `"roadmap: '%s' — %d%% built"` | weekly proof the allocation is working (or too slow) |
| journal · outcome log — board refresh line | no | ADD | `rep.lines`: `"%d new bets on the roadmap board"` | tells the player the board changed without opening the binder |
| journal · outcome log — hq entropy line | no | ADD | `rep.lines` (hq only): `"organizational entropy: debt +0.8 (no maintenance shipped in 10 wks)"` | the maintenance tax must bill with an explanation, never silently |
| journal · narration (DM) | yes | CHANGE | no new UI; adjudication narration now covers ship weeks via §9 directives (ready / shipped-last-week lines) | the launch becomes story, not just ledger — with zero extra calls |
| coach | yes | ADD one line | first time the board is non-empty: `"the roadmap is live — point the team at a bet, or the R&D money just polishes what exists."` | one-time onboarding into the new desk, in the coach's existing voice |

---

## 9. DM integration — zero new calls

- **`SimEngine.signals()`** gains one compact entry (rides every existing call):
  `"roadmap": {"committed": [names] or "none", "progress_pct": int_of_first,
  "ready": name or "", "last_shipped": "name (band, wk N)" or ""}`.
- **`GameState.to_digest()`** gains the same `"roadmap"` dict (Tier-2 event cards
  see the board).
- **`EventGenerator._directives()`** (and the C# twin) appends:
  - any ready bet: `- '%s' is code-complete: the house dice roll it next week. The team can feel it.`
  - any bet with `shipped_week >= state.week − 1`:
    `- SHIPPED: '%s' went out %s. The week's story must feel the launch.` with the
    band phrase map — brilliant `"and the launch sang"`, fine `"and it landed
    fine"`, risky `"hot, with smoke coming out"`, backfired `"and it faceplanted"`.
- The tick's `rep.events` SHIPPED line already flows to the journal's outcome log
  (⚡ prefix, `garage_view_screen.gd:2897`); commits flow through `log_action`
  (§3) into `recent_actions`. The DM narrates the ship week from directives +
  signals — **no new request, no new schema field.**

---

## 10. LLM leverage — where a call earns its place, and where it must never be

**Value point A — bet-card dressing (the one batch call).**
- *Trigger:* after `weekly_tick`, iff `int(rep.get("bets_refreshed", 0)) > 0 and
  llm.enabled() and not generator.disabled` (daily seeded runs stay authored-only)
  and no dressing call already pending. Fires from `garage_view_screen` right
  where `generate_arcs` rides era changes — the engine never holds the client.
- *Batch shape:* ONE `llm.request_json(BETS_PROMPT, user, LlmClient.BETS_SCHEMA,
  cb)` dressing every freshly drawn candidate at once (1–3 cards). Era changes
  ~4×/run and ships a dozen times → a few calls per run, each tiny.
- *What it decides:* `name` (≤28), `desc` (≤90), and a **rung**: `kind` from the
  fixed enum, `ambition` 1–3. Themes, not terms.
- *What it must NEVER decide:* cost, DC, payoff units, durations, which slot
  opens, when refresh happens, what the roll earned. All engine tables.
- *Ingestion (`SimEngine.apply_bet_dressing(state, cards)` — engine-owned):* each
  returned card replaces the next candidate with `progress == 0.0 and not
  committed and not ready and not shipped and id != "hardening"`, in board order.
  Clamp: name `.substr(0,28)`, desc `.substr(0,90)`, kind ∉ enum → keep the
  authored card; `platform` while `era_index < 3` → `quality`; ambition
  `clampi(1..3)` then `mini(2)` in garage; then **re-price cost/DC from the §5
  tables**. Committed or progressed bets are untouchable — a slow reply can
  never repaint work in flight. Keyless runs produce structurally identical
  boards from §11; the desk cannot tell the difference.

**Value point B — the ship-week story (no call at all).** The DM narrates the
launch through the existing adjudication call via §9's directives and signals.
Names stay consistent because the narrated name IS the state's name.

**Schema (add to `llm_client.gd` + C# twin as `BETS_SCHEMA`):**
```gdscript
const BETS_SCHEMA := {
  "type": "object", "additionalProperties": false, "required": ["bets"],
  "properties": {"bets": {"type": "array", "minItems": 1, "maxItems": 3,
    "items": {"type": "object", "additionalProperties": false,
      "required": ["name", "desc", "kind", "ambition"],
      "properties": {
        "name": {"type": "string", "maxLength": 28},
        "desc": {"type": "string", "maxLength": 90},
        "kind": {"type": "string", "enum": ["quality", "retention", "reach", "platform"]},
        "ambition": {"type": "integer", "minimum": 1, "maximum": 3}}}}}}
```
(`debt` is excluded on purpose: hardening is the only debt bet and it is standing.)

**System prompt (`BETS_PROMPT`, byte-synced twins):**
```
You name feature bets for RUNWAY!, a satirical startup survival game. Given the
company, its era, what already shipped and what sits on the board, write N
candidate feature bets SPECIFIC to this exact business. name: <=28 chars, plain
product-speak a PM would write on a card. desc: <=90 chars, dry and wince-funny —
what it is and who it is for. kind: quality (the product gets better for
everyone), retention (existing customers stay longer), reach (new people get a
reason to show up), platform (infrastructure that makes all future building
faster — only natural for a company with real scale). ambition: 1 small and
safe, 2 a real feature, 3 the big swing. Cover at least two different kinds
across the batch. Never numbers, never metric promises, never real companies or
people, never a bet already on the board or recently shipped.
```
User message: `JSON.stringify({"company": {name, idea, what, who}, "era":
era_display_name, "board": [names], "recently_shipped": [last 8 names],
"slots": N})`.

**Tempting-but-wrong uses (rejected):**
1. *LLM prices cost/payoff/DC from the desc* — realistic-sounding, but the same
   card would cost differently across runs and providers; balance-critical
   numbers leave the engine. Tables only.
2. *LLM adjudicates the ship* — the house dice own outcomes; a narrated verdict
   would be the game inventing a judgement it never rolled.
3. *A per-week progress-flavor call* — call volume for zero mechanics; the DM
   already sees progress in signals.
4. *LLM advises which bet to commit* — coaching would launder agency through the
   dressing channel; the odds preview is the honest advisor.
5. *A new call to narrate the launch* — value point B gets it free.

---

## 11. Keyless authored pool (generic, era-filtered at draw)

```gdscript
const BET_POOL := [
  {"name": "Onboarding, but humane", "desc": "New users stop rage-quitting the first screen. Mostly.", "kind": "quality", "ambition": 1},
  {"name": "Annual plans", "desc": "Twelve months upfront, a discount, and a calmer churn chart.", "kind": "retention", "ambition": 1},
  {"name": "The Referral Loop", "desc": "Users invite users. A button, a bribe, a dream of virality.", "kind": "reach", "ambition": 1},
  {"name": "Offline mode", "desc": "Works on a plane, in a tunnel, at your uncle's farm. Sync is the hard part.", "kind": "quality", "ambition": 2},
  {"name": "The Big Integration", "desc": "Plug into the tool your customers already live in. Their IT has questions.", "kind": "reach", "ambition": 2},
  {"name": "Alerts that matter", "desc": "Fewer notifications, better ones. Customers stop muting you.", "kind": "retention", "ambition": 2},
  {"name": "The Redesign", "desc": "Everything moves. Half the users hate it loudly, then miss it later.", "kind": "quality", "ambition": 3},
  {"name": "Mobile, finally", "desc": "The whole thing, on a phone, without weeping. The board keeps asking.", "kind": "reach", "ambition": 3},
  {"name": "The API platform", "desc": "Everything becomes a building block. Slow now, faster forever.", "kind": "platform", "ambition": 3},
  {"name": "One-click deploys", "desc": "Shipping stops being a ceremony. The team ships twice as often.", "kind": "platform", "ambition": 3},
]
```
Plus the fixed hardening card (§3). 8 generic + 2 platform; kinds spread 3/2/3/2.

---

## 12. Faithful mechanics — analogue per formula, simplifications named

| mechanic | real-world analogue | simplification dropped — and why it holds |
|---|---|---|
| WIP cap / single commit early | small-team single-piece flow; WIP limits | no fractional allocation sliders — even split at office+ keeps the desk readable; the WIP lesson survives intact |
| R&D-weeks + $1,200/wk unit | person-week estimation currency; loaded eng cost | cost is *certain* up front (real estimates err) — schedule variance is deliberately moved into the ship dice, one uncertainty instead of two |
| engineer `0.25×skill` | productivity dispersion; meetings/support tax on nominal capacity | no ramp-up curve per engineer — onboarding pipeline already charges 2 unpaid-productivity weeks |
| debt drag (−0.83%/pt over 40, floor 0.5) | tech debt as interest on velocity (Cunningham) | linear, not compounding — the outage roll + build disadvantage already stack on top; three teeth suffice |
| ship roll, DC by ambition | launch outcome dispersion; most features underperform; scope widens variance | all-or-nothing delivery, no MVP slicing — the weekly written move can still narrate demos/betas; weekly granularity can't honestly track partials |
| payoff-as-status (weeks) | feature buzz fades; retention gains wash out as cohorts turn over | no permanent churn improvements — prevents churn-floor creep; chained retention bets model sustained investment honestly |
| hardening 6× ambient paydown | dedicated refactor sprints beat drive-by fixes | none — that ratio is the practice |
| QA net at office+ | process truncates the failure tail without raising the ceiling | QA has no cost lane of its own — it rides the era (rent/staff already price the org) |
| platform ×1.15/level | internal platform & deploy tooling compound all future work | velocity-only payoff (no reliability effect) — debt mechanics already own reliability |
| era refresh of candidates | strategy churn: stage changes reset priorities | committed bets survive the move — losing paid work would teach nothing but resentment |
| hq maintenance tax | large-org standing maintenance share; entropy | binary freshness window (10 wks), not a ratio — one legible rule beats a hidden portfolio equation |

---

## 13. Pedagogy — the four lessons, surfaced by name

| concept | where it is named, with receipts |
|---|---|
| **CAPACITY** | desk header prints the live pool (`"one team, %.1f R&D-wks/wk"`); weekly progress line shows what a week of it bought |
| **OPPORTUNITY COST** | desk footer names it; committed weeks ship no base quality and the P&L rnd lane keeps charging |
| **TECH-DEBT INTEREST** | jar line prints `−%d%% velocity`; outage and QA receipts cite debt by number |
| **LAUNCH RISK** | odds line names it with a % and the DC; the ship receipt prints the die, the DC, the band, and the WHY (`"shipped too big for the codebase"` when debt disadvantage decided it — include `ctx.dis_reasons` in the backfired receipt) |

Add to the backfired event line when `ctx.disadvantage`:
`" (disadvantage: %s)" % ", ".join(dis_reasons)` — the burn always explains itself.

---

## 14. Twin test pins (Godot suite + C# `EconomyProbe`, identical assertions)

1. **Determinism & seed board:** fresh state, seed 42, week 1 → two ticks on
   clones produce identical `bets` (ids, names, order); board = hardening + 2
   candidates in garage, all `ambition <= 2`, none `platform`.
2. **Opportunity cost:** twins at `rnd 2400`, one committed to an ambition-2
   bet → `progress == 2.0` exactly (no statuses, debt 10 → drag 1.0, no
   engineers, garage founder +0.25 ⇒ 2.25 — pin 2.25) and `product == p0`;
   the uncommitted twin keeps the existing `product >= p0 + 2` pin and
   `progress == 0.0`.
3. **Debt drag exact:** same committed setup at `tech_debt 90` vs `10` →
   progress ratio `0.58333 ± 0.0001` (garage founder term included in both).
4. **Band table via scripted roller:** `ship_bet(state, bet, roller)` exposed;
   roller `[20, ...]` on ambition-2 quality → brilliant, `product +11`,
   `hype +8`; roller `[7, ...]` (total 7 vs DC 11, margin −4) → `backfired` in
   garage but `risky` at office (QA net), debt `+12` vs `+6`; clamps hold at
   product 98 → 100.
5. **Ready ships NEXT tick + hardening standing:** progress 4.5/5.0 with spend
   to complete → tick N: `ready true, shipped false`; tick N+1: `shipped true,
   band != ""`, slot refilled same tick; ship hardening → next tick a fresh
   `"hardening"` at `progress 0.0` exists.
6. **Multipliers compose:** one engineer `skill 4` adds exactly `+1.0` pool at
   `rnd 0`; `platform_level 1` multiplies the pin-2 pool to `2.5875`
   (2.25 × 1.15).

---

## 15. Engine-improvement suggestions (≤5)

1. `velocity_mult` was authored into STATUS but consumed nowhere — §4 wires it
   into bet progress; also apply it to the base-quality rnd path for consistency
   (adjust the existing `+2 at $2,400` pin accordingly).
2. `hype_wk` in STATUS (`press_surge` 4.0, `press_backlash` −6.0) is equally
   dormant — wire a weekly hype drift in step 4 beside `morale_wk`.
3. `served_total` lives in unsaved meta — the learning curve silently resets on
   load; promote to a saved field.
4. Tick `rep.events` (outages, quits, rival moves — now ships) are printed but
   never `log_action`'d, unlike fired clocks — the DM's `recent_actions` is
   blind to them; log them at the call site.
5. Twin drift watch: Unity `SimEngine.cs:653` burn omits the Godot
   `+ offer_fixed` catalog-overhead lane (Wave-A half-landed) — pin it in both
   suites when Wave A closes, before roadmap lines shift byte-parity checks.

---

## 16. Open questions (≤3)

1. **Abandonment tax:** switching commitment freezes progress today. Should a
   stood-down bet decay (say −25% once) to price context-switching, or is the
   era-refresh discard punishment enough?
2. **Ship as ceremony:** ready bets auto-roll next tick (world-owned schedule).
   Worth a later dice-at-press SHIP button in the binder for the feel (engine
   function is already UI-agnostic: `ship_bet(state, bet, roller)`), at the cost
   of a stall rule for players who never press?
3. **Enterprise reach:** `feature_buzz` lifts `adopt_p`, but Enterprise adds are
   GTM-capacity-clamped — should reach payoffs also add `+2` temporary
   `gtm_cap` on Enterprise runs (coordinate with design 05, pipeline)?
