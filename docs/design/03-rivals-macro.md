# 03 — LIVING RIVALS + MACRO/SEASONS (Wave C, plan §3 + §8)

Both share `the street`. Engine owns every number; the DM narrates beats the
engine already resolved. Twin implementation targets:

| | Godot | Unity |
|---|---|---|
| engine | `game/src/core/sim_engine.gd` | `unity/Assets/Scripts/Core/SimEngine.cs` |
| worldgen | `game/src/core/world_gen.gd` | `unity/Assets/Scripts/Core/WorldGen.cs` |
| rival shape | `game/src/core/game_state.gd` (`rivals: Array` of dicts) | `unity/Assets/Scripts/Core/GameState.cs` `Rival` class |
| DM context | `game/src/llm/event_generator.gd` `_directives()` + `compose_event_user()` | `unity/Assets/Scripts/LLM/EventGenerator.cs` `Directives()` / `ComposeEventUser()` + `RunSnapshot.cs` + `CoreSnapshot.cs` |
| desk | `game/src/ui/binder.gd` `_tab_street()` / `_tab_threats()` / `_refresh` bangs | `unity/Assets/Scripts/Game/BinderScreen.cs` twins |
| tests | `game/tests/sim_engine_test.gd` | `unity/Runway.Core.Tests/Program.cs` |

**Persistence law discovered in audit**: Godot saves persist an explicit field
list (`save_system.gd`) — Object metas are NOT saved. Therefore anything with
a duration lives in **statuses** (persisted, catalog-typed, auto-shown on the
threats tab, auto-fed to the DM via `signals()`), or **inside the rival dicts**
(persisted wholesale). Metas are used only for transient week-scoped data.

---

## 1. RIVAL STATE MODEL

Extend each rival dict (GD) / `Rival` class (C#). Old saves deserialize with
defaults — GD reads via `.get(k, default)`, C# fields carry initializers.

| field | type | default | range | meaning (real-world analogue) |
|---|---|---|---|---|
| `vigor` | float | 55.0 | 0..100 | war chest / free cash — capacity to act; actions burn it, rest restores (capital constraint on competitive conduct) |
| `focus` | String | `"growth"` | `price` \| `product` \| `growth` | strategic bent (cost leadership / differentiation / share-grab — Porter generic strategies, 3-bucket proxy) |
| `price_posture` | float | 1.0 | 0.80..1.20 | their price vs the street's reference (Bertrand position) |
| `hype` | float | 20.0 | 0..100 | share of voice; decays like adstock |
| `last_action` | String | `""` | action ids | last week's action |
| `log` | Array[String] | `[]` | cap 6 | `"wk%d: %s"` entries for the street tab |
| `cooldowns` | Dictionary | `{}` | action→int wks | response-lag realism + anti-spam |
| `sniffing` | int | 0 | week set | acquisition-interest marker (M&A handoff, §5.7) |

Kept as-is: `name, what, strength, tactics, secret, weeks_since_move`
(`weeks_since_move` stays for save-compat; `log` supersedes it — retire at the
next save VERSION bump).

C# additions to `Rival` (GameState.cs, after `Secret`):

```csharp
[JsonProperty("vigor")] public double Vigor = 55.0;
[JsonProperty("focus")] public string Focus = "growth";
[JsonProperty("price_posture")] public double PricePosture = 1.0;
[JsonProperty("hype")] public double Hype = 20.0;
[JsonProperty("last_action")] public string LastAction = "";
[JsonProperty("log")] public List<string> Log = new List<string>();
[JsonProperty("cooldowns")] public Dictionary<string,int> Cooldowns = new Dictionary<string,int>();
[JsonProperty("sniffing")] public int Sniffing;
```

**Worldgen seeding** — `WorldGen.build()` / `Build()`: draw the new fields at
the **END** of build (after offers), iterating rivals in order — inserting
draws mid-sequence would shift every later investor/offer draw and break
worldgen determinism. Per rival, in order:
`vigor = rng.randf_range(40.0, 70.0)`, `hype = rng.randf_range(10.0, 40.0)`,
`focus = ["price","product","growth"][rng.randi() % 3]`, `price_posture = 1.0`.
LLM-world path (`apply_llm_world`, no rng in scope): defaults above, and
`focus = ["price","product","growth"][name.length() % 3]` (twin-safe, no hash).

## 2. SALT REGISTRY + THE WEEKLY ACTION LOOP

Used salts today: 4, 5, 6, 7, 9, 77, 88, 91, 93. **Convention for all waves:
salt = plan-section × 10 + n.** This wave claims:

| salt | stream | draw discipline |
|---|---|---|
| **30** | rival weekly actions | exactly **2 draws per rival, always** (d1 `randf` action pick, d2 `randi` line pick), rivals in array order — fixed draw count so one rival's branch never shifts another's dice |
| **31** | poach resolution | 1 `randf`, only when poach fires (sole consumer — variable is safe) |
| **32** | disruptor spawn (hq) | 1 `randf`/wk at hq; on fire, feeds `WorldGen.make_name` + field draws |
| **80** | macro shocks | 1 `randf`/wk always; +1 `randi_range(6,10)` on shock activation |

Salt **6** (the old rival ratchet) is **retired** — the block it fed is
replaced by this design. Never reuse it. Add a `SALT` registry comment at the
top of both SimEngines.

### 2.1 Per-rival upkeep (deterministic, no draws — runs every week, every era)

```
for each cooldown: cooldowns[k] = max(cooldowns[k] - 1, 0)
strength += clampf((vigor - 45.0) / 50.0, -0.5, 0.7) + 0.005 * hype   → clamp 5..95
hype     = maxf(hype - 4.0, 0.0)
vigor    = clampf(vigor + (55.0 - vigor) / 12.0, 0.0, 100.0)
price_posture += clampf(1.0 - price_posture, -0.01, 0.01)
```

Real-world: firms grow on cash + attention, not dice — replaces the old
random `+0..1.2` ratchet with state-driven drift (same ~−0.5..+1.2/wk band).
Buzz decays (adstock); reserves mean-revert; discounts erode back to list
price after wars end (airline fare wars pattern).

### 2.2 The gap

```
player_power = clampf(0.5 * product + 0.25 * hype
             + 25.0 * clampf(traction / (0.02 * tam), 0.0, 1.0), 5.0, 95.0)
gap          = rival.strength - player_power        # + = they outmatch you
```

Product half, buzz a quarter, market share a quarter (2% of TAM = full
marks — early-stage scale, audience-normalized via TAM so Enterprise's 30
customers weigh like Consumer's 18k).

### 2.3 Action availability by era — "scaling by stage"

Competitive response is threshold-triggered in the real world: incumbents do
not answer challengers below visibility/share thresholds; macro (credit
cycles) hits everyone regardless of size. Attention ladder:

| era | attention | what the street does to you |
|---|---|---|
| garage | **unseen** | Rivals act only among themselves: `{quiet, launch, blitz, stumble}` with SELF-effects only — no player statuses, no price war, no poach, no sniff, no journal noise (street-tab log only). Macro is FULLY live (cycle, winter/boom, valuation, term sheets). Lesson: markets don't care that you exist; the economy still prices you. |
| coworking | **noticed** | + `launch` and `blitz` now land player effects (`outshipped`, `rival_fud`). First taste of relative-quality and share-of-voice pressure. |
| office | **contested** | + `price_cut` (price war, §5.1) and `poach` (§5.4) unlock — cuts are aimed at your catalog, your underpaid people get calls. |
| floor | **category war** | `launch`/`blitz` base weights ×1.5 (press battles); + `sniff` eligible (§5.7). |
| hq | **incumbent** | + disruptor spawn (§6): you ARE the reference price now; low-end attackers appear beneath you (Christensen low-end disruption). |

Implement as `street_level(state) -> int` = index in
`["garage","coworking","office","floor","hq"]` (0..4); gates below reference
L0..L4.

### 2.4 The weekly action table (one seeded pick per rival, salt 30, d1)

Eligibility gates (weight = 0 when failed), then weights, then one cumulative
scan over d1 × Σw in FIXED order
`[price_cut, launch, blitz, poach, stumble, sniff, quiet]`:

| action | gates | weight |
|---|---|---|
| `price_cut` | L≥2 · vigor ≥ 25 · posture > 0.82 · cooldown 0 | `8 × (2 if focus=="price") × (0.5 if posture ≤ 0.90)` `+ 6 if offers_overpriced(state)` |
| `launch` | vigor ≥ 30 · cooldown 0 | `10 × (2 if focus=="product") + (4 if gap ≤ 0)`; `×1.5 at L≥3` |
| `blitz` | vigor ≥ 30 · cooldown 0 | `8 × (2 if focus=="growth") + (4 if player.hype ≥ rival.hype + 15)`; `×1.5 at L≥3` |
| `poach` | L≥2 · vigor ≥ 40 · labor target exists (§5.4) · cooldown 0 | `4 + (4 if gap ≤ 0) + (2 if focus=="product")` |
| `stumble` | cooldown 0 | `4 + (6 if vigor < 30) + (4 if hype ≥ 70) + (6 if secret=="quietly running out of money")` |
| `sniff` | L≥3 · strength ≥ 60 · gap ≥ 10 · player_power ≥ 35 · `sniffing == 0` · cooldown 0 | `2` |
| `quiet` | always | `30 + (15 if vigor < 25)` |

`offers_overpriced(state)`: any offer with `price > 0` and
`price ≥ 1.15 × fair_price` — greed invites undercutting (price umbrella:
pricing above the reference invites entry under it).

Real-world mapping: weights = conjectural-variation conduct — firms act on
capacity (vigor), strategy (focus), relative position (gap). A trailing rival
(gap ≤ 0) ships and poaches harder (catch-up behavior). Weekly cadence
compresses quarterly conduct; acceptable because relative frequency, not
absolute calendar, drives the feel.

**Cooldowns installed on fire** (competitive response lags — a real price
move takes a quarter to answer; poaching runs recruiting cycles):

`price_cut 4 (3 if focus=="price") · launch 5 · blitz 3 · poach 6 · stumble 8 · sniff 12 · quiet 0`

After any action: `last_action = id`, `log.append("wk%d: %s" % [week, label])`
(cap 6, drop front), `weeks_since_move = 0` on non-quiet.

## 3. NEW STATUS CATALOG ENTRIES (both engines, magnitudes live HERE)

```gdscript
"price_war":      {"fair_mult": 0.92, "kind": "condition"},
"outshipped":     {"adopt_mult": 0.85, "kind": "condition"},
"rival_stumbled": {"adopt_mult": 1.25, "kind": "buff"},
"winter_watch":   {"kind": "condition"},                    # banner/DM only
"boom_watch":     {"kind": "buff"},                          # banner/DM only
"funding_winter": {"val_mult": 0.6, "amt_mult": 0.7, "spread_mult": 1.25,
                   "dis": "raise", "kind": "condition"},
"boom":           {"val_mult": 1.3, "amt_mult": 1.3, "spread_mult": 0.9,
                   "adv": "raise", "kind": "buff"},
```

New effect keys are read ONLY by new helpers (§5.1, §7.3) — the existing
section-8 status loop (adopt/churn/arpu) is untouched by them. Statuses
persist in saves, auto-list on the threats tab, and auto-feed the DM through
`signals()` — the house pattern doing the plumbing for free. The DM may
narratively install `price_war` (it is catalog-typed like `market_headwind`);
magnitudes stay engine-owned.

## 4. WHY STATUSES, NOT METAS

`price_war`, watches and shocks must survive save/reload; metas do not
persist in the Godot save. Duration state therefore rides `state.statuses`.
Metas used (transient by design): `street_beats` (rebuilt every tick),
`shock_cool` (reload resets it to eligible — accepted default-safe loss,
same class as the existing `served_total`/`prev_revenue` reset).

## 5. EXACT EFFECTS PER ACTION

All player-facing installs are gated by street level (§2.3); receipts land in
`rep.events` at L≥1 (garage keeps the street-tab log only).

### 5.1 `price_cut` — price war (Bertrand undercutting → reference-price erosion + margin compression)
- `price_posture = maxf(price_posture - 0.06, 0.80)`; `vigor -= 8`.
- `add_status(state, "price_war", 5 if focus=="price" else 4)` (re-cuts extend
  duration via the existing `add_status` max rule; magnitude fixed at 0.92).
- **Effective fair price**: new helper, both engines:

```gdscript
static func street_fair_mult(state: GameState) -> float:
    var m := 1.0
    for s in state.statuses:
        m *= float(STATUS.get(String((s as Dictionary).get("name","")), {}).get("fair_mult", 1.0))
    return maxf(m, 0.85)
```

  Applied at exactly three sites (all already hold `state`):
  1. `offers_demand_mult`: demand ratio uses `price / (fair × m)` — give
     `offer_demand(offer, price, fair_mult := 1.0)` a third defaulted param;
     the binder's price-curve preview passes the live mult (the desk shows
     street truth).
  2. `offers_price_pain`: ratio uses `fair × m` — holding your list price
     through a war reads as expensive, so churn pain rises.
  3. `offers_arpu`: a never-priced offer bills at `fair × m` (the going rate
     itself dropped).
- Weekly ledger receipt while active:
  `"price war on the street: the going rate is down %d%% (%d wks left)"`.
- Simplification named: no cost asymmetry — the war has no cost-side winner
  because rival P&L is off-screen; `vigor` proxies their burn. Reference
  price mean-reverts (war ends → mult expires) instead of permanently
  repricing the category; acceptable at this altitude.

### 5.2 `launch` — product leapfrog (vertical differentiation: their step up is your relative step down)
- `strength = minf(strength + 4.0, 95.0)`; `hype = minf(hype + 15.0, 100.0)`;
  `vigor -= 12`.
- L≥1: `add_status(state, "outshipped", 3)` (adopt ×0.85 — buyers comparing
  you to the new thing hesitate; your product meter is untouched because you
  lost no code, only relative appeal).
- Simplification: no feature-space positioning — one axis of quality.

### 5.3 `blitz` — share-of-voice battle (SOV→SOM: attention crowd-out; the status is decaying adstock)
- `hype = minf(hype + 20.0, 100.0)`; `vigor -= 15`.
- L≥1: `add_status(state, "rival_fud", 2)` — reuses the existing entry
  (adopt ×0.8, dis sell); this finally gives `rival_fud` an engine-side
  installer.

### 5.4 `poach` — pay-gap arbitrage (labor mobility: underpaid people answer recruiter calls; counter-offer season)
- Interface owned by the labor wave (§2 of the plan). This wave calls:
  `LaborMarket.poach_target(state) -> {index, name, salary, market_salary, pay_gap}`
  or `{}`; `pay_gap = (market_salary - salary) / market_salary`; eligible when
  `pay_gap ≥ 0.15`. Until the labor desk exists (or with no employees) there
  is no target → the gate zeroes the weight — no shim, no fake wages.
- `vigor -= 10` (attempt cost, success or not).
- Success probability, exact:

```
p = clampf(0.15 + 1.2 * (pay_gap - 0.15) + 0.003 * (vigor - 50.0), 0.05, 0.70)
```

  (pay_gap 0.15/vigor 50 → 0.15; pay_gap 0.40/vigor 80 → 0.54; cap 0.70 —
  even flush acquirers lose recruiting battles.)
- Roll: salt-31 `randf() < p`.
- **Success**: remove the employee from `state.employees` (receipt names
  them), `morale = clampi(morale - 6, 0, 100)`, rival `strength = minf(+2, 95)`.
  They leave BEFORE this week's GTM capacity count and payroll (§8).
- **Fail**: receipt only — the warning shot ("the salary conversation is
  coming"). Whether a failed poach raises that employee's ask is the labor
  wave's hook (open question 1).

### 5.5 `stumble` — execution risk correlates with overextension (scandal/outage/recall; high hype + low cash companies break loudest)
- `strength = maxf(strength - (12.0 if secret=="quietly running out of money" else 6.0), 5.0)`;
  `vigor = maxf(vigor - (20.0 if money-secret else 10.0), 0.0)`; `hype *= 0.5`.
- L≥1: `add_status(state, "rival_stumbled", 2)` (adopt ×1.25 — their churn is
  your word of mouth).
- The worldgen `secret` finally pays off mechanically.

### 5.6 `quiet` — consolidation
- `vigor = minf(vigor + 6.0, 100.0)`; posture drifts per upkeep. No receipt;
  silence in the log (`"wk%d: quiet"`).

### 5.7 `sniff` — M&A HANDOFF ONLY (consolidation interest tracks target traction + acquirer strength)
- `rd["sniffing"] = state.week` (persisted in the rival dict);
  `state.set_flag("acquisition_sniff")` (persisted). One street beat (§9).
- **This wave prices nothing and spawns no offer.** The M&A wave (plan §10)
  owns the courtship: it scans `rivals[i].sniffing > 0`, builds its event,
  clears `sniffing` and the flag when consumed or lapsed. When it prices the
  offer it should apply `shock_val_mult` (§7.3) — winters buy cheap.

## 6. DISRUPTOR SPAWN (hq only)

Low-end disruption: incumbents create the price umbrella attackers live
under. At L4, if `rivals.size() < 3`, roll salt-32 `randf() < 0.04` (~1 per
25 wks at hq). On fire, from the same salt-32 stream:

```
name = WorldGen.make_name(r32)         # variable draws — sole consumer, safe
strength = 12.0 + r32.randf_range(0.0, 8.0)
vigor = 70.0 + r32.randf_range(0.0, 20.0)   hype = 30.0
price_posture = 0.90                    focus = "price"      # disruptors attack on price
tactics = RIVAL_TACTICS[0]              what = ""  secret = ""  log/cooldowns fresh
```

Receipt + street beat (§9). Rivals array is iterated everywhere (street tab,
bible digest, pressure) — size 3 is safe; `apply_llm_world`'s exactly-2 rule
is worldgen-time only. Cap stays 3 (open question 3: does an acquired rival
free the slot?).

## 7. MACRO: THE CYCLE AND THE SHOCKS

### 7.1 The season cycle (business-cycle trend+cycle decomposition, one stylized frequency)
Pure function, no storage, no draws:

```
season_phase = int(abs(sim_seed)) % 52
cycle_target(state) = 1.0 + 0.12 * sin(TAU * float(week + season_phase) / 52.0)
                      - 0.10 if has_status("funding_winter") or has_status("winter_watch")
                      + 0.10 if has_status("boom") or has_status("boom_watch")
```

Section 7 of the tick becomes mean-reverting around it — **same single
salt-7 draw, stream preserved**:

```
market_trend = clampf(market_trend
    + (cycle_target(state) - market_trend) * 0.15
    + r7.randf_range(-1.0, 1.0) * theta.trend_vol, 0.5, 1.5)
```

Period 52 wks, amplitude ±0.12, reversion 0.15/wk, noise ±trend_vol. Trend
hovers within ~0.1 of target — seasons become READABLE (the pedagogical
point: demand has weather). Simplification: real cycles are multi-frequency
with stochastic period; one sine + noise keeps the banner honest and the
math auditable.

Season band for banner/report: `trend ≥ 1.10 → "tailwinds"`,
`≤ 0.90 → "headwinds"`, else `"calm"`. Report line only on band CHANGE:
`"the street turned: %s"`. Macro does NOT install
`market_tailwind`/`market_headwind` statuses (those stay DM-installable
story beats) — macro already flows through the `market_trend` multiplier in
adoption; double-dipping would count the weather twice.

### 7.2 Shocks (credit cycles: risk-on/risk-off repricing — 2022 multiple compression ×~0.6, 2021 mirror)
Weekly, salt 80, one `randf` d ALWAYS drawn. Eligible when: `week ≥ 8`, no
watch/shock status active, meta `shock_cool == 0`.

- `d < 0.010` → `add_status("winter_watch", 1)` — the pre-announcement.
- `elif d < 0.020` → `add_status("boom_watch", 1)`.
- ~2%/wk combined ≈ 60% chance of at least one shock per 52-wk year;
  `shock_cool = 20` after a shock ends spaces them.

**Transition** (macro block, after section 2 has expired statuses): if
`rep.expired` contains `"winter_watch"` → `add_status("funding_winter", D)`,
`D = r80.randi_range(6, 10)`; mirror for boom. 6-10 wks = a 12-18-month real
winter compressed to game-time (a week is a beat — consistent with the rest
of the sim). One week of watch = leading indicators turning before the money
does (sentiment precedes term sheets).

### 7.3 Shock effects (exact)
New helpers, both engines — product over active statuses, default 1.0:
`shock_val_mult(state)`, `shock_amt_mult(state)`, `shock_spread_mult(state)`
reading `val_mult` / `amt_mult` / `spread_mult` from the catalog.

- `SimEngine.valuation()`: `arr * mult * funding_mult * shock_val_mult(state)`
  before the int cast (cash floor unchanged). Winter ×0.6, boom ×1.3.
- `generate_offers()`: `amount = maxi(int(pre * r.randf_range(0.05, 0.15) * shock_amt_mult(state)), 5_000)`;
  `spread *= shock_spread_mult(state)` (equity clamp 1..45 unchanged). Winter:
  smaller checks, more equity asked; boom mirror.
- `dis raise` / `adv raise` ride the existing status machinery free of charge.
- `GameState.valuation()` (era/score lens) untouched — the run's score floor
  should not evaporate the week a winter ends; exit pricing under shocks is
  the M&A wave's call via `shock_val_mult` (§5.7).

## 8. WEEKLY-TICK INTEGRATION ORDER

Replace section 6 wholesale; macro slots in as 6b, before section 7:

```
1 clocks · 2 statuses decrement/expire · 3 pipeline · 4 fatigue/morale · 5 debt/outage
6a THE STREET: per-rival upkeep → weekly action (salt 30; poach salt 31;
   era gates) → disruptor roll (salt 32, hq) → existing avg-strength
   `pressure` formula UNCHANGED
6b MACRO: shock roll (salt 80) → watch→shock transitions → shock_cool--
7  market trend: mean-reverting walk (salt 7, §7.1)
8  adoption/churn — statuses installed in 6a/6b are live NOW; street_fair_mult
   applies; a poached employee is already out of the GTM head-count
9  money — the leaver is off payroll the week they left
```

**Rivals act BEFORE adoption, justified**: rival triggers read LAST week's
player state (the price you posted, the hype you had when the week opened),
while their effects land on THIS week's adoption — exactly the real shape:
conduct responds with a lag, consequences are immediate on announcement. It
also matches the existing convention that section-5/6 statuses gate the same
week's section 8. Status durations keep the house rule: a 2-wk status
installed mid-tick is live this week + next (decrement at tick START).

**P&L lanes: none.** Rival and macro effects are demand-side and
funding-side; they surface as receipts, statuses, valuation and term-sheet
math — inventing a money lane for someone else's price cut would be fake
accounting.

## 9. BIG-BEAT INJECTION (DM context — zero new API calls)

Engine writes meta `street_beats: Array[String]` each tick (cleared at tick
start; cap 4; priority macro > sniff > poach > launch > stumble; garage: macro
only). LLM layer appends them verbatim:

- GD `_directives()`: each beat as a `"- "` line (adjudicator sees them as
  non-negotiable facts).
- GD `compose_event_user()`: when non-empty, append
  `"\nTHE STREET THIS WEEK (facts — weave in when it fits):\n- " + "\n- ".join(beats)`
  (new cards can BE the beat).
- C#: `RunSnapshot` gains `public string[] StreetBeats;` mapped in
  `CoreSnapshot.From` from `state.GetMeta("street_beats")`;
  `Directives()` / `ComposeEventUser()` twin the two injection points.

Exact templates (engine formats; DM never sees numbers it can change):

```
MACRO: the street smells a funding winter — from next week valuations compress and term sheets tighten. Investors already talk colder.
MACRO: funding winter, %d wks left — valuations 0.6x, rounds smaller and meaner. Money scenes are hostile.
MACRO: the thaw — the street funds again.
MACRO: the street smells a boom — from next week money runs warm and careless.
MACRO: boom, %d wks left — valuations 1.3x, term sheets sweeten. Everyone is a genius this quarter.
THE STREET: %s launched for real this week (strength %d). Customers are comparing.
THE STREET: %s tried to poach %s this week — %s. The team noticed.        # third %s: "and they left" | "they stayed, this time"
THE STREET: %s stumbled publicly — their customers are looking around. A door is open this week.
THE STREET: quiet word is %s is asking around about acquiring the company. Do not resolve it — let it charge the room.
THE STREET: a new name, %s, is undercutting from below. Incumbents ignore these at their own funeral.
```

`price_cut` and `blitz` do NOT get DM beats (minor conduct; their statuses
already appear in `signals()`) — keeps the block ≤4 lines on the wildest week.

Also: `signals()` rival strings gain street literacy —
`"%s (%s, %s, %s, fights on %s)"` → name, fuzz(strength), vigor word, posture
word, focus (words from §11). String-only change, schema untouched.

## 10. AUTHORED ONE-LINER POOLS (engine receipts, salt-30 d2 pick, dry voice + the named dynamic)

`price_cut` (`%s` rival):
- "%s cut their price. The street noticed. — a price war buys share with margin"
- "%s went cheaper. The going rate just followed them down."
- "%s discounted hard. Margin compression is now everyone's problem."
- "%s put a sale sign in the window. Your list price reads expensive today."

`launch`:
- "%s shipped. It's good. Your product got older overnight."
- "%s launched the thing they teased. Buyers are comparing notes."
- "%s cut a ribbon on a real feature. The category ladder just moved."
- "%s shipped loud. Relative quality is the only quality the street sees."

`blitz`:
- "%s is everywhere this week. Attention is a zero-sum street."
- "%s bought the billboard, the podcast, and probably your ad slot."
- "%s is outspending you on being seen. Share of voice buys share of market."
- "%s made noise. Your quiet got quieter."

`poach` success:
- "%s called %s with a number. The number won."
- "%s hired %s away. Underpaying is a bet somebody else collects."
- "%s made %s an offer the payroll sheet couldn't answer."

`poach` fail:
- "%s called %s with a number. %s stayed — this time."
- "%s went fishing in your team. Nobody bit. The bait will get bigger."
- "%s tested a loyalty you haven't been paying for."

`stumble`:
- "%s had a very public bad week. Their churn is your word of mouth."
- "%s broke something customers loved. Doors are open."
- "%s made the news for the wrong reason. Execution risk collects."
- "%s stumbled. Overextension always invoices eventually."

`sniff`:
- "somebody at %s keeps asking what you'd cost."
- "%s's corp-dev person knows your numbers a little too well."
- "a banker mentioned %s and your name in one sentence."

disruptor (hq):
- "a new name, %s, is doing what you do for less. You remember this trick."
- "%s just launched under your price umbrella. You built that umbrella."
- "%s is scrappy, cheap, and pointed at your cheapest customers first."

Macro banner lines (desk + report, no `%s`): winter watch — "the street
smells winter. money gets cold next week"; winter — "FUNDING WINTER — checks
shrink, terms bite"; thaw — "the thaw. the street funds again"; boom watch —
"the street smells a boom. money warms next week"; boom — "BOOM — everyone's
a genius, every round oversubscribed"; boom end — "the boom cooled. everyone
pretends they called it".

## 11. THE DESK: STREET TAB v2 + THREATS + BANGS

`_tab_street()` / BinderScreen twin, top to bottom:
1. **Macro banner**: season word from trend (`≥1.10 "tailwinds — the street
   buys"`, `≤0.90 "headwinds — wallets closed"`, else `"calm"`) + the active
   watch/shock banner line (PEN color for winter, SAGE for boom).
2. **Per rival block** (word maps, teach by name — never raw floats):
   - line 1: `"%s — %s"` name + fuzz(strength) (existing).
   - line 2 posture read: vigor `≥70 "flush" / ≥45 "steady" / ≥25 "tight" / "bleeding"`;
     posture `≤0.94 "undercutting" / ≥1.06 "premium" / "at market"`;
     `"fights on %s"` focus; hype `≥60 "loud" / ≥30 "buzzing" / "quiet"`.
   - line 3: `"plays: ..."` tactics (existing).
   - line 4: last 3 log entries joined `" · "`.
3. **The money** (investors — unchanged, below).

Threats tab tie-ins (beyond the automatic status rows): while `price_war`
active add `"▼ price war: the going rate is down %d%% for %d wks"`; while
`acquisition_sniff` flagged add `"▼ someone is circling — %s asked about
buying you"`.

**Bang condition** (`_refresh`): `_bangs["the street"].visible = ` meta
`street_beats` non-empty — one predicate covering poach attempts, rival
launches, stumbles, sniffs, winter/boom announcements and disruptor spawns
(strict superset of the brief's three). Garage never bangs for rival theater
(beats are macro-only at L0).

## 12. INTERFACE DELTA

Every UI change this lane needs, standalone-assessable. Each row lands in
BOTH engines (Godot surface named; Unity twin = `BinderScreen.cs`,
`JournalPage.cs`/`JournalSpreads.cs`, `GarageScreen.cs` respectively).

| # | surface | exists today? | CHANGE / ADD | exactly how | why the player needs it |
|---|---|---|---|---|---|
| 1 | binder · "the street" — macro banner | no | ADD | Top strip under the tab title (y≈56..130, above the first rival block): line 1 = season word from trend bands — "calm" / "tailwinds — the street buys" (≥1.10) / "headwinds — wallets closed" (≤0.90); line 2 only while a watch/shock is active, the authored banner + weeks left, e.g. "FUNDING WINTER — checks shrink, terms bite · 6 wks left". Colors: PEN for winter/watch, SAGE for boom. | Seasons and shocks must be readable BEFORE the money screens punish you; the one-week pre-announcement is the playable warning. |
| 2 | binder · "the street" — per-rival posture line | no | ADD | New line 2 of each rival block (26px, ink 0.7), four word-reads joined " · ": vigor ("flush/steady/tight/bleeding"), posture ("undercutting/at market/premium"), "fights on price\|product\|growth", hype ("loud/buzzing/quiet"). Never raw floats. | Teaches competitive posture by name — reading who is flush and price-focused IS the counterplay. |
| 3 | binder · "the street" — per-rival action log | no | ADD | New line 4 of each rival block: last 3 `log` entries joined " · ", e.g. "wk14: cut prices · wk15: quiet · wk16: poach attempt". | Rivals become predictable through their record, not through hidden stats — pattern-reading is the skill. |
| 4 | binder · "the street" — block layout + "the money" | yes (2-line rival blocks, investor list) | CHANGE | Rival blocks grow 2→4 lines (measured-wrap stacking already handles height). With a 3rd rival (hq disruptor), the investor section compresses to one line each (name + archetype only) so the page never overflows. | The street stays one page at every era, including the hq third rival. |
| 5 | binder · tab bang on "the street" | bang machinery yes; street predicate no | ADD | In `_refresh`: `_bangs["the street"].visible =` meta `street_beats` non-empty — covers poach attempt, rival launch, stumble, sniff, winter/boom announcement, disruptor spawn. Never fires at garage for rival theater (beats are macro-only at L0). | The coral ! is the game's single attention channel; these are exactly the weeks the street must be opened. |
| 6 | binder · "threats" — price-war row | no | ADD | While `price_war` active, one PEN row: "▼ price war: the going rate is down 8% for 3 wks" (live percent from `street_fair_mult`, weeks from the status). | Explains WHY demand and revenue sag this week — the receipt next to the damage. |
| 7 | binder · "threats" — circling-acquirer row | no | ADD | While flag `acquisition_sniff` set: "▼ someone is circling — %s asked about buying you" (rival with `sniffing > 0`). | The sniff must be visible dread from the week it happens, before the M&A wave's event arrives. |
| 8 | binder · "threats" — winter/boom/watch rows | yes (automatic status renderer) | no code — new content | New statuses (`winter_watch`, `funding_winter`, `boom_watch`, `boom`, `price_war`, `outshipped`, `rival_stumbled`) appear through the existing ▲/▼ + weeks-left renderer. | Owner should expect rows like "▼ funding winter — 8 wks left" with zero new rendering code. |
| 9 | journal · weekly report — rival receipts | partially ("%s made a move — …") | CHANGE | The generic move line is replaced by the authored per-action one-liners (§10 pools, salt-30 pick), incl. poach receipts naming the employee ("Vantage hired Mara Voss away…"). Garage era: no journal lines from rival theater. | Receipts teach the dynamic by name (price war, share of voice, execution risk) — the pedagogy lives here. |
| 10 | journal · weekly report — price-war ledger line | no | ADD | Every active war week, in `rep.lines` beside the money lines: "price war on the street: the going rate is down 8% (3 wks left)". | The P&L reader sees the cause sitting next to the numbers it dented. |
| 11 | journal · weekly report — macro lines | no | ADD | Authored lines on: shock announce, shock start (with duration), shock end, and season band CHANGE only ("the street turned: headwinds"). No weekly repetition. | The macro narrates itself in the record; the announce line is actionable info (raise before the winter lands). |
| 12 | journal · DM narration | yes (weekly narration) | no UI code — richer input | DM receives the §9 street-beat lines inside its existing context; big beats (launch, poach, stumble, sniff, macro) get woven into the story. | One narrator: the street's drama arrives through the DM's voice, not a parallel text channel. |
| 13 | binder · "pricing" — price-curve preview | yes | CHANGE | `offer_demand()` gains a defaulted `fair_mult` param; the preview passes live `street_fair_mult` so the demand curve shifts during a war; add one caption line while active: "price war: the street's reference is 8% down". | Pricing during a war is THE decision this tab exists for — the desk must not show peacetime demand in wartime. |
| 14 | binder · "cap table" — raise preview | yes | CHANGE | Valuation side inherits ×`shock_val_mult` automatically (patched `SimEngine.valuation`); the equity-ask preview multiplies its spread by `shock_spread_mult`; append suffix while shocked: "(winter: valuations 0.6×)" / "(boom: 1.3×)". | Raise-timing against the cycle is the core macro lesson, decided on this preview line. |
| 15 | garage · coach card | yes (3-step first-run coach) | ADD one-shot step | On the FIRST street bang (mark `user://seen_street_v1`, same pattern as `COACH_MARK`): "the street lit up: a rival did something that touches you. the street tab has the log; threats has the damage." Positioned over the binder button like coach step 2. | The street tab is scenery until coworking; players need one pointer the week it turns hostile. |
| 16 | everything else (garage room, vitals, ledger, customers, product, crew HUDs) | yes | no change | — | This lane is demand-side + funding-side; its story surfaces are the street, threats, journal, pricing/cap-table previews only. |

## 13. TWIN TEST PINS (add to both suites, same constants)

1. **Determinism**: seed 42, wk 5, office-era state with extended rivals;
   run 10 ticks twice from identical states → identical
   `JSON.stringify(rivals)`, `market_trend`, and status lists.
2. **Era gate**: garage state, 30 ticks → no `price_war` / `outshipped` /
   `rival_fud` / `rival_stumbled` ever installed, zero employees lost to
   poach; yet some rival's `strength` or `hype` changed (the street lives
   without you).
3. **Cooldown law**: office-era, 200 seeds × 26 wks → for every rival, two
   firings of the same non-quiet action are ≥ cooldown[action] weeks apart;
   aggregate quiet share in [0.20, 0.70].
4. **Poach pin (success case)**: employee {salary 900, market 2250 →
   pay_gap 0.6}, rival vigor 80 → `p == 0.70` exactly (cap); with a pinned
   seed whose salt-31 draw < 0.70: employee removed, `morale` −6, receipt
   contains the employee's name, rival strength +2. Also pin
   `p(pay_gap 0.15, vigor 50) == 0.15` and a pinned-seed failure leaves
   `employees.size()` unchanged.
5. **Price-war math**: install `price_war` → `street_fair_mult == 0.92`; an
   offer priced exactly at fair with elasticity 2.2 →
   `offers_demand_mult == pow(1.0/0.92, -2.2)` ± 1e−9 (≈0.8323); after the
   status expires the mult reads 1.0 again.
6. **Macro pins**: `cycle_target(sim_seed 4242, week 10)` `== 1.0 +
   0.12*sin(TAU*40.0/52.0)` ± 1e−9 (phase 4242%52 = 30) — same float both
   engines; with `funding_winter` active: `SimEngine.valuation` equals the
   unshocked float ×0.6 before the int cast, `generate_offers` amounts scale
   ×0.7 on identical salt-9 draws, and equity_pct is ≥ the unshocked twin;
   boom mirrors upward.

## 14. LLM LEVERAGE

Where a call genuinely earns its place:
1. **Worldgen rival dressing** (exists — `apply_llm_world`): pitch-born
   names/`what`/tactics. Trigger: run birth, one batch. Never decides:
   strength (word→number via `str_map`), any weekly number.
2. **Big beats as story** (§9): launch/poach/stumble/sniff/macro lines ride
   the EXISTING event + adjudication calls as context. Trigger: none new —
   zero extra calls. Never decides: outcomes (already resolved), amounts,
   durations.
3. **Disruptor introduction** (optional, still zero calls): the spawn beat
   line lets the next event card introduce them in-world; keyless Markov
   name is always the fallback identity.
4. **Era-transition re-dressing** (optional, later): one batch per era to
   refresh rival `what`/tactic wording as the category matures. Names only;
   strictly cosmetic; skippable forever.

Tempting but WRONG:
- LLM picking weekly actions — breaks seeded replay and produces
  un-receipted causality; the action table IS the simulation.
- LLM setting price-war depth, poach odds, shock timing/duration — engine
  owns numbers; fairness demands the macro be replayable from the seed.
- LLM-written weekly receipts — cost + voice drift; authored pools carry the
  named dynamic (§10) at zero tokens.
- LLM "deciding" whether the sniff becomes an offer — that is the M&A wave's
  engine logic, not prose.

## 15. ENGINE-IMPROVEMENT SUGGESTIONS (max 5)

1. Add the SALT registry comment block atop both SimEngines (existing +
   §2's convention) — the only collision guard future waves have.
2. Append `market_trend` to `metric_history` snapshots — a street sparkline
   for the banner later, one field.
3. Extract the effective-fair computation into one helper before Wave D
   (funnel) touches demand math — three call sites already (§5.1).
4. Extend `tests/balance_sim.gd` to chart rival pressure + trend over 60 wks
   per era — tuning the weights table needs the curve, not anecdotes.
5. At the next save VERSION bump: drop `weeks_since_move` (superseded by
   `log`), and consider persisting a small `world_meta` dict so durable
   state stops needing status-shaped workarounds.

## 16. OPEN QUESTIONS (max 3)

1. **Poach aftermath**: should a FAILED poach raise the target employee's
   salary expectation (counter-offer dynamics)? Interface hook exists; the
   labor-market owner decides.
2. **Stacked misery floor**: winter drag + price war + blitz can compound to
   ~0.55× adoption for several weeks. Roguelike-honest, or cap combined
   street pressure at 0.5×? (Recommend: no cap — receipts explain every
   piece; capping would falsify the lesson that downturns compound.)
3. **Disruptor slot**: rivals cap at 3 — when the M&A wave lets a rival die
   or be acquired, does the freed slot re-open disruptor spawns below hq?
