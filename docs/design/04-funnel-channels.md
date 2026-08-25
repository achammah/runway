# 04 — THE FUNNEL: four marketing channels

Owner-approved replacement for the single `marketing` lever (plan §4, Wave D).
`sales` stays what it is: closing capacity. RUNWAY! is an economic simulator
first — every mechanic below mirrors a real growth dynamic, is named by its
real name (CAC, saturation, payback, organic vs paid), and prints receipts
that explain WHY. Engine owns every number. No LLM in this subsystem (see §7).

Engines: `game/src/core/sim_engine.gd` + `unity/Assets/Scripts/Core/SimEngine.cs`
(twin math), desks in `game/src/ui/binder.gd` + `unity/Assets/Scripts/Game/BinderScreen.cs`,
pins in `game/tests/sim_engine_test.gd` + `unity/Runway.Core.Tests/Program.cs`.

---

## 0. The shape

The old path multiplied organic adoption by one blended `mk_mult` and hid the
funnel. The new path makes the funnel the actual computation:

```
REACH (bought: ads + content + outbound)  ──┐
                                            ├─→ LEADS (× conv: quality, mood,
WALK-INS (organic p_eff·P + word of mouth,  │    market, prospect availability)
  word of mouth amplified by referrals) ────┘         │ × price demand
                                                      ▼
                                            SIGNED = min(demand, gtm_cap)
```

Every stage number the binder shows is the engine's real intermediate value.
Channels are pure state + arithmetic: no new RNG salts (net adds keep salt 91;
attribution is exact arithmetic, no dice).

---

## 1. The channel model

Four weekly budgets in `state.budgets`: `ads`, `content`, `referrals`,
`outbound` (plus the untouched `sales`, `care`, `rnd`, `office`). All flow
into burn exactly like the old levers — every dollar leaves cash.

### 1.1 Paid ads — instant, saturates hard, CAC inflates

Real-world analogue: auction-bought performance media. Second-price auctions
mean the first dollars buy the cheap, well-targeted audience; pushing spend
climbs the bid landscape and marginal CPM rises. The standard marketing-mix
response curve for this is a concave saturating reach function — exactly the
house `1−exp` idiom.

```
ads_sat   = theta.cac_sat × ads_k[audience]        # the world still prices the knee
reach_ads = ads_a[audience] × (1 − exp(−b_ads / ads_sat)) × era_eff × team_mult
```

Full effect the same week; zero memory (stop paying, reach stops). Rising CAC
is emergent, not scripted: reach is concave in spend, so CAC = spend/attributed
rises as you climb the curve — the binder receipts it in those words.

Dropped simplifications: creative fatigue cycles and per-platform split (one
blended auction), multi-day adstock carryover (performance-ad half-life is
days; at weekly grain ≈ 0). Acceptable: the strategic truths — instant, capped,
inflating — survive intact. Competitor bid pressure is deferred to Wave C
(rivals raise `ads_sat`, §9.1).

### 1.2 Content — a stock that compounds, and rots when starved

Real-world analogue: SEO/content libraries. Posts and rankings are capital,
not spend: effect builds over months toward a level your investment supports
(domain authority), keeps paying at ~zero marginal cost, and decays slowly
when unfunded (content rot, slipping rankings). So content is the subsystem's
one new STATE variable, `content_equity` C ∈ [0,1]:

```
funded (b_con > 0):  C += ((1 − exp(−b_con / con_sat)) − C) × 0.125   # CON_RAMP
starved (b_con = 0): C ×= 0.93                                        # CON_DECAY
reach_content = con_a[audience] × C × era_eff × team_mult
```

Ramp: 12.5%/wk toward the funded ceiling → ~80% of target in 12 weeks
(matches the "6–12 months to rank" folklore, compressed honestly to the
game's week grain). Decay half-life ≈ 9.6 weeks. Holding level L costs
`−con_sat × ln(1−L)` per week — maintenance is cheaper than building, which
is the real economics of a library. Equity builds even pre-launch (writing
before shipping is a real strategy); conversion stays launch-gated (§2).

Dropped: per-post virality variance (the `viral_moment` status already covers
spikes), evergreen-vs-news split. Acceptable: one stock captures compounding +
decay, the two truths that change decisions.

### 1.3 Referrals — promoters amplify word of mouth; detractors refer no one

Real-world analogue: the viral loop. k-factor = invites × invite-conversion,
and only promoters send invites (NPS logic: below a quality bar you have
detractors, and a paid referral program amplifies silence). Referral spend
(double-sided incentives, referral tooling) multiplies the EXISTING Bass
word-of-mouth term — it cannot conjure advocates a bad product never made:

```
care_soft = 1 − exp(−b_care / 1500)                   # the very term care already uses on churn
happy     = max((product − 25) / 75, 0)^1.2 × (0.5 + 0.5 × care_soft)   # NPS gate: 0 below v0.25
ref_gain  = ref_a[audience] × (1 − exp(−b_ref / ref_sat)) × happy × team_mult
wom      ×= (1 + ref_gain)
```

Needs an installed base too — `wom` already scales with A, so zero customers
= zero referrals, for free. Dropped: per-referred-signup incentive payouts
(folded into the weekly budget), invite latency. Acceptable: the loop's gate
(happy customers) and its multiplier nature are what teach.

### 1.4 Outbound — quota math; buys reach AND closing

Real-world analogue: SDR outbound. Lists and sequences scale linearly with
budget (there is always another list), replies convert at the same
product/price gates as everything else, and outbound money is also selling
CAPACITY — an SDR-hour equivalent, the same idiom as `b_sales/600`. It only
pencils where contract value justifies a human toucher, hence the audience
multiplier:

```
reach_ob = 5.0 × (b_ob / 1000) × ob_aud[audience]          # OB_REACH_PER_K, era-neutral (founder-led)
gtm_cap += (b_ob / 600) × ob_aud[audience]                  # inside the cap parenthesis, × cap_scale
```

Dropped: multi-week enterprise sales cycles (§5 Enterprise pipeline, Wave D
sibling, will consume `leads_ob` as named accounts — the hook is reserved),
list exhaustion beyond the global `avail` term. Acceptable for now: cycle
length returns with the pipeline board.

### 1.5 Constants — exact, per audience

Engine consts (`const CHANNELS` in sim_engine.gd; static table in SimEngine.cs):

| const                    | Consumer | SMB   | Enterprise | meaning |
|--------------------------|----------|-------|------------|---------|
| `ads_a` (reach/wk max)   | 2400     | 320   | 20         | audience you can buy |
| `ads_k` (× theta.cac_sat)| 0.30     | 0.40  | 0.65       | ads_sat: 2400/3200/5200 at default θ |
| `con_a` (reach/wk max)   | 1600     | 520   | 30         | library ceiling |
| `con_sat` ($ knee)       | 1600     | 1600  | 2200       | funded-target knee |
| `ref_a` (max wom lift)   | 2.6      | 1.8   | 1.2        | loop amplitude |
| `ref_sat` ($ knee)       | 900      | 1200  | 1500       | program saturation |
| `ob_aud`                 | 0.15     | 1.0   | 2.5        | who answers cold touch |
| `conv` (base lead conv)  | 0.030    | 0.080 | 0.060      | CONV_AUD |

Shared: `CON_RAMP 0.125`, `CON_DECAY 0.93`, `OB_REACH_PER_K 5.0`,
`ERA_REACH_EFF {garage 0.35, coworking 0.7, office 1.0, floor 1.1, hq 1.25}`,
`ERA_AN_CAP {garage 1, coworking 2, office 3, floor 3, hq 3}`,
team mult `1 + 0.12 × min(marketing_heads, 5)`.

**Proof no channel dominates** — attributed adds/wk at $2,000/wk each, office
era, product 50 (quality_gate 0.6), care $1,000, trend 1, no rivals:

| channel    | Consumer (A=1000)     | SMB (A=300)          | Enterprise (A=20)   |
|------------|-----------------------|----------------------|---------------------|
| ads        | 24.4  (CAC $82)       | 7.1  (CAC $280)      | 0.23 (CAC $8.7k)    |
| content ss | 20.5  (CAC $97)       | **17.8 (CAC $112)**  | 0.64 (CAC $3.1k)    |
| referrals  | **41.5 (CAC $48)**    | 3.1  (CAC $643)      | 0.04 (CAC $48k)     |
| outbound   | 0.03  (CAC $74k)      | 0.5 + **3.3 closers**| **0.90 (CAC $2.2k) + 8.3 closers** |

- **Ads** best-in-class: any cold start (referrals need A, content needs weeks
  — at A=100 Consumer referrals drop to ~4/wk, ads still 24; week 1 content
  is at C=0.09). The instant channel.
- **Content** best-in-class: SMB long-run (steady-state 17.8/wk beats
  everything) and the only channel that survives a spend freeze.
- **Referrals** best-in-class: Consumer at scale — multiplying the biggest
  `adopt_ic` (0.15) with customers on the books.
- **Outbound** best-in-class: Enterprise always (cheapest CAC + triples a
  cap_scale-1 closing ceiling); SMB when capacity-bound.
- Enterprise sanity: CAC $2.2k against LTV ≈ residence 90wk × arpu $400 —
  payback ~6 weeks, honest enterprise economics.

---

## 2. The funnel, explicit — stage math

Weekly-tick section 8 becomes (normative GDScript; C# mirrors names in
PascalCase — `ContentEquity`, `ChannelFor(state.BizWho)`):

```gdscript
var ch: Dictionary = CHANNELS.get(state.biz_who, CHANNELS["SMB"])
var b_ads := float(bud.get("ads", 0)) + float(state.marketing_budget)  # legacy op folds here, as before
var b_con := float(bud.get("content", 0))
var b_ref := float(bud.get("referrals", 0))
var b_ob  := float(bud.get("outbound", 0))
var ch_total := b_ads + b_con + b_ref + b_ob
var era_eff := float(ERA_REACH_EFF.get(state.era, 1.0))
var mk_heads := 0
for em in state.employees:
    if String(em.get("role", "")).contains("marketing"):
        mk_heads += 1
var team_mult := 1.0 + 0.12 * float(mini(mk_heads, 5))

# REACH — three bought sources (§1.1–1.4)
var ads_sat := float(th.cac_sat) * float(ch.ads_k)
var reach_ads := float(ch.ads_a) * (1.0 - exp(-b_ads / ads_sat)) * era_eff * team_mult
if b_con > 0.0:
    state.content_equity = clampf(state.content_equity \
        + ((1.0 - exp(-b_con / float(ch.con_sat))) - state.content_equity) * CON_RAMP, 0.0, 1.0)
else:
    state.content_equity *= CON_DECAY
var reach_con := float(ch.con_a) * state.content_equity * era_eff * team_mult
var reach_ob := OB_REACH_PER_K * b_ob / 1000.0 * float(ch.ob_aud)

# LEADS — one conversion gate for all bought reach, reusing existing terms
var avail := P / maxf(N, 1.0)                       # prospect-pool exhaustion (wom's own P/N idiom)
var conv := float(ch.conv) * quality_gate * status_adopt * state.market_trend \
        * (1.0 - pressure) * avail * (1.0 if launched else 0.0)
var leads_ads := reach_ads * conv
var leads_con := reach_con * conv
var leads_ob  := reach_ob * conv
var leads_paid := leads_ads + leads_con + leads_ob

# WALK-INS — the untouched organic pipeline, minus mk_mult; referrals lift wom
var care_soft := 1.0 - exp(-b_care / 1500.0)
var happy := pow(maxf((float(state.product) - 25.0) / 75.0, 0.0), 1.2) * (0.5 + 0.5 * care_soft)
var ref_gain := float(ch.ref_a) * (1.0 - exp(-b_ref / float(ch.ref_sat))) * happy * team_mult
# p_eff: DELETE the mk_mult factor, keep everything else byte-identical
# wom:   multiply by (1.0 + ref_gain) after its existing factors

# SIGNED — price, then the capacity ceiling
var pd := clampf(price_demand, 0.1, 3.0)
var organic := p_eff * P * pd
var wom_all := wom * pd
var demand := organic + wom_all + leads_paid * pd
var gtm_cap := (1.5 + 0.8 * float(state.competences.get("sell", 3)) + 3.0 * float(sales_heads) \
        + b_ads / 400.0 + b_sales / 600.0 + b_ob / 600.0 * float(ch.ob_aud)) * cap_scale
var adds := minf(demand, gtm_cap)
var close_rate := adds / maxf(demand, 0.001)
```

Then the existing seeded-remainder net (salt 91), residence, care churn — all
unchanged. `b_ads/400` inherits the old `mk_budget/400` slot exactly, so a
migrated save keeps its closing ceiling.

**Launch gate**: `conv` carries the same `launched` 0/1 as `p_eff`; `wom`
keeps its 0.5 pre-launch half-rate. Pre-launch, content equity still builds
(the one investment channel) and pre-launch ads print a warning receipt.

**Fog of war** — effective analytics `an = min(state.analytics_level,
ERA_AN_CAP[state.era])` (a garage has no data stack to buy; full attribution
is an office-era capability):

| an | customers tab shows |
|----|----------------------|
| 0  | count "give or take", no funnel (unchanged) |
| 1  | funnel bars REACH → LEADS → SIGNED + per-channel CAC this week |
| 2  | + cohort/retention read: 12-wk survival, churn %/wk, care's trim, lifetime at v0.X |
| 3  | + the truth: true TAM vs believed, conv %, close rate, best-CAC channel called out |

---

## 3. CAC and attribution — exact

Attribution is proportional and exact (sums to `adds`, no residue):

```gdscript
var att := {
    "ads":       leads_ads * pd * close_rate,
    "content":   leads_con * pd * close_rate,
    "outbound":  leads_ob  * pd * close_rate,
    "referrals": wom_all * close_rate * (ref_gain / (1.0 + ref_gain)),
}
var att_organic := organic * close_rate
var att_wom     := wom_all * close_rate / (1.0 + ref_gain)
# invariant: att_organic + att_wom + Σ att == adds  (pin #1 asserts it)
```

- **Per-channel CAC**: `spend_i / att_i`, shown when `att_i ≥ 0.05`; "—" at
  zero spend; "burning" (coral) when `spend_i ≥ 500` and `att_i < 0.05`.
- **Blended CAC** (existing `rep.cac`, same meaning, new sum):
  `int(round((ch_total + b_sales) / new_adds))` — acquisition + closing spend
  over arrivals. LTV and `payback_wk` lines unchanged.
- Outbound's capacity contribution is deliberately NOT in its CAC (capacity
  released benefits every source; the desk shows "+N closing" beside it).
- Stored each tick in `state.set_meta("funnel", {...})`: per-channel
  `spend/reach/leads/signed/cac`, plus `conv`, `close_rate`, `equity`,
  `organic`, `wom`, `blended_cac`, and last week's copy is read first for the
  CAC-rise receipt. Unity mirrors via the `unit_econ` meta idiom
  (Dictionary fresh / JObject off disk — reuse the `UnitEcon` reader pattern).

**Where CAC shows**: ledger unit-econ line (blended — exists), customers tab
(per-channel at an≥1), DM signals string (§4), weekly report line (exists).

---

## 4. Weekly-tick integration — the edit map

`sim_engine.gd` (line refs pre-change) and the same spots in `SimEngine.cs`:

| site | change |
|------|--------|
| L239 `b_mk` / L246–247 `mk_budget`,`mk_mult` | replaced by §2 channel block (`state.marketing_budget` folds into `b_ads`) |
| L261 `p_eff` | drop `mk_mult` factor, rest identical |
| L264 `wom` | append `* (1.0 + ref_gain)` |
| L273 `adds =` | becomes organic/wom_all/leads_paid + demand/cap/close_rate (§2) |
| L286 `gtm_cap` | `mk_budget/400` → `b_ads/400 + b_ob/600*ob_aud`; `b_sales/600` stays |
| L350 `burn` | `mk_budget` → `ch_total` |
| L384 pnl meta | `"marketing": int(ch_total)` stays as the SUM lane; add detail lanes `"ads"`, `"content"`, `"referrals"`, `"outbound"` (Unity `Pnl` gains 4 ints; `Marketing` stays = sum, nothing else breaks) |
| L402 money line | prints marketing = ch_total (no format change) |
| L410 `rep.cac` | `(b_mk + b_sales)` → `(ch_total + b_sales)` |
| L451 metric_history | unchanged |
| `signals()` L950 | `marketing_weekly` = ch_total + legacy (sum semantics kept); ADD `"funnel_mix": "ads $a · content $b (equity N%) · referrals $c · outbound $d · blended CAC $e"` — the DM narrates channels with zero new calls |
| `runway_weeks()` | Godot: dict iteration already sums new keys — no edit. Unity: `Budgets.Sum()` must add the four new fields |

**P&L decision**: ONE `marketing` lane + four detail keys, not four top-level
lanes — the compact ledger line stays readable, back-compat readers of
`pnl.marketing` keep working, and the channel split lives where the funnel
lives (customers tab + mix receipt).

**Report lines** (receipts teach — each names the real concept and its cause):

1. adds line gains a source: `+N customers (organic a · word of mouth b · channels c)`.
2. mix (when ≥2 channels funded): `the mix: ads $A→n1 · content $B→n2 (equity E%) · referrals $C→n3 · outbound $D→n4`.
3. CAC inflation (b_ads ≥ 1.2× last week AND cac_ads ≥ 1.25× last week's):
   `ads CAC rose to $X — the cheap audience is spent (saturation)`.
4. compounding (C crosses a 0.25/0.5/0.75 threshold upward):
   `the library compounds: content reaches N/wk now, at $0 marginal`.
5. rot (b_con = 0, C ≥ 0.05): `the library goes quiet — content equity fades to N%`.
6. referral gate (b_ref ≥ 500, happy < 0.1): `a referral program for a product
   nobody would vouch for (v0.X) — promoters first, program second`.
7. capacity (close_rate < 0.9): `demand outran closing: N wanted in, you signed
   C — capacity, not demand, is the bottleneck (sales or outbound)`.
8. burning channel (spend ≥ 500, att < 0.05): `$X into ads found nobody — saturated or mispriced`.
9. pre-launch paid (b_ads + b_ob > 0, not launched): `reach with nothing to
   sign — ads and cold calls convert only after launch`.
10. garage note (era garage/coworking, b_ads + b_con ≥ 500, once via flag
    `seen_paid_era_note`): `the garage discount: paid reach ×0.35 — no brand,
    no pixel history. Outbound and word of mouth are the garage channels.`

---

## 5. Migration and the DM op

**State**: `game/src/core/game_state.gd` L40 default becomes
`{"ads":0,"content":0,"referrals":0,"outbound":0,"sales":0,"care":0,"rnd":0,"office":0}`;
add `var content_equity := 0.0`. Unity `Budgets` gains `Ads/Content/Referrals/Outbound`
(Marketing field kept as the legacy JSON hook), `GameState` gains
`[JsonProperty("content_equity")] double ContentEquity`.

**Save-compat rule (both engines, idempotent, called at tick start AND after load)**:

```gdscript
static func migrate_budgets(state: GameState) -> void:
    if state.budgets.has("marketing"):
        state.budgets["ads"] = int(state.budgets.get("ads", 0)) + int(state.budgets["marketing"])
        state.budgets.erase("marketing")
    for k in ["ads", "content", "referrals", "outbound", "sales", "care", "rnd", "office"]:
        if not state.budgets.has(k):
            state.budgets[k] = 0
```

Old `marketing` maps to **paid ads** — it inherits both old behaviors
(instant reach, the /400 cap feed), so a mid-run save feels no cliff. Save
`VERSION` stays 2 (additive keys; the loader's `if k in state` skip-unknowns
handles both directions; missing `content_equity` defaults 0). Godot save dict
adds `"content_equity"`. Legacy `state.marketing_budget` (the `set_marketing`
op's target) keeps folding into the ads lane exactly as it folded into `b_mk`.

**DM op — decision: keep ONE `marketing` cat; the engine splits it by the
current mix.** `llm_client.gd` schema and both prompt files stay byte-identical
(cats stay `marketing/sales/care/rnd/office/one_off`). Because: (1) the DM
speaks founder-language ("put $2k into marketing"); WHICH channel is the
player's desk decision, and a narrator must never silently overwrite a curated
mix; (2) zero schema churn keeps the twin prompts byte-synced; (3) channel
cats can be added later without breaking either. `garage_view_screen.gd`
~L2797 (`WeekCommit.cs` mirror):

```gdscript
"set_budget" with cat "marketing":
    var mix := ["ads", "content", "referrals", "outbound"]
    var mix_sum := 0
    for k in mix: mix_sum += int(state.budgets.get(k, 0))
    if mix_sum <= 0:
        state.budgets["ads"] = wk_amt              # cold start: ads (the instant channel)
    else:
        var put := 0
        for k in mix:                               # deterministic order; remainder → ads
            var share := int(floor(float(wk_amt) * float(state.budgets[k]) / float(mix_sum)))
            state.budgets[k] = share
            put += share
        state.budgets["ads"] += wk_amt - put
    state.marketing_budget = 0
```

Direct channel cats (`ads` etc.) are accepted if they ever arrive (future-proof
branch) but the schema does not advertise them yet. The `hire` op's salary
table gains `"marketing": 1300` in both engines (channel teams, §6.5).

---

## 6. The desk

### 6.1 The ledger — 8 lever rows in the 1160×760 pane

Channels live in a sub-block at 58px pitch (name 24px at y, desc 18px at
y+27); org levers keep the fuller 62px rows. Column x's unchanged idiom:
label x10, $ x455 (26px, PEN), live effect x640 w340 (20px), − x1000 + x1064
(52×44 buttons at y+2). Exact vertical map:

```
y=6    "the ledger — where this week's money goes" (38)
y=62   "MARKETING — the funnel mix · $X/wk · blended CAC $Y" (24, ink 0.6)
y=96   ads        "paid reach — instant, saturates hard; runs only while fed"
y=154  content    "the library — slow to build, works while you sleep, rots if starved"
y=212  referrals  "promoters talking — multiplies word of mouth; needs product + care"
y=270  outbound   "lists and cold calls — buys reach AND closing; born for enterprise"
y=333  divider line (ink 0.25)
y=340  SALES   (62px pitch, unchanged text)
y=402  CARE
y=464  RND
y=526  OFFICE
y=592  unit-econ line: pays/CAC/LTV/payback (23)        ← drops if BOTH warnings fire
y=626  pnl in (24, BLUE) · y=660 pnl out (24) · y=694 THE BOTTOM LINE (27)
y=734  one slot: warnings (runway ⚠ / THE RED, PEN) else the rules line (20)
```

Rules line gains funnel vocabulary: `reach saturates · content compounds ·
only capacity closes · churn is a leaky bucket · three weeks below zero ends it`.

Live effect strings (from the engine's own formulas, like `_lever_effect`):
ads `reach ≈N/wk` (+ ` (era ×0.35)` in garage/coworking); content
`equity N% → ≈M/wk` or `fading −7%/wk`; referrals `word of mouth ×1.NN` or
`nobody would vouch yet (v0.X)` when happy < 0.1; outbound `+R reach · +C closing`.
`LEVER_STEPS [0,250,500,1000,2000,4000,8000]` and the era-cap step clamp apply
per row unchanged. Godot: split `LEVERS` into `CHANNEL_LEVERS` + `ORG_LEVERS`;
Unity mirrors the two arrays.

### 6.2 Customers tab — the funnel lit by analytics (an = min(level, era cap))

```
y=6/10  icon + "N customers" (46) — unchanged, incl. an=0 fog text
y=100   "customers, weekly:" (24) · y=130 spark 1120×170 (was 200 — reclaim 30px)
an≥1:
y=316   "the funnel, last week:" (24, ink 0.6)
y=348   _FunnelBars at (10,348) size 700×148 — three bars, 46px pitch, h=30:
        REACH N (bought) / LEADS M (conv X%) / SIGNED K — bar w = 40 + 460×v/max,
        fill BLUE/YELL/SAGE at 0.5 alpha, ink wobble outline (rng seed 13 idiom),
        SIGNED bar shows "ceiling C hit" in PEN when close_rate < 0.9
x=740,y=348  "CAC by channel" (22) + 4 rows 26px pitch: "ads $82 · $2,000/wk" …
        ("—" unfunded, "burning" coral per §3)
y=512   "market, as you believe it: …" (27) + y=548 assumptions note (22)  [moved from 356/392]
an≥2:
y=584   "of 100 who joined 12 wks ago, ~N are still here · churn X.X%/wk · care trims Y%"
        (26)   — survival = (1 − 1/residence)^12, from the engine's residence
y=620   "lifetime ≈ N wks at v0.X · blended CAC $Y · payback Z wks" (24)
an≥3:
y=668   "the truth: ~TAM buyers (you believed B) · conv X.X% · close NN% ·
        your cheapest customer comes from CHANNEL" (26, SAGE)
```

The old an≥2 pseudo-CAC (`mk/max(1, mk/900)`) is deleted — real per-channel
CAC replaces it. Vitals tab burn line swaps `state.marketing_budget` for the
pnl marketing sum. **New bang**: `customers` joins `_bangs` — visible when any
channel is "burning" (spend ≥ 500, att < 0.05) or content is rotting (b_con=0,
C ≥ 0.3). Unity `BinderScreen.TabCustomers`/`TabLedger` mirror every
coordinate; funnel bars drawn with the DrawnUI rect + hand-label idiom.

### 6.3–6.5 Scaling by stage (owner north star: depth grows with the company)

Engine-owned era gates, all deterministic, already listed in §1.5 consts:

| era | what unlocks (exactly) |
|-----|------------------------|
| **garage** | Founder-led scrappy acquisition: outbound + word of mouth at full strength; paid reach (ads+content) ×0.35 — no brand, no pixel history, DIY creative. Analytics capped at 1 (bars only). Receipt #10 teaches it the first time paid money goes in. |
| **coworking** | First paid experiments: reach eff ×0.70; analytics cap 2 (cohorts readable). |
| **office** | The full 4-channel mix at ×1.00 and full attribution: analytics cap 3 — conv, close rate, best-CAC channel, true-TAM read. |
| **floor** | Brand effects begin: reach eff ×1.10 (the name opens doors). Channel teams practical: `marketing` hires exist (hire table +$1,300/wk) and team_mult (+12%/head, cap 5) amplifies ads, content and referrals. |
| **hq** | Brand at scale: reach eff ×1.25. Same team math, bigger spend caps via existing `era_spend_cap`. |

`team_mult` is live at every era (0 heads = ×1), but salaries + era spend caps
make it a floor/hq-scale play in practice — no artificial lock needed.

---

## INTERFACE DELTA — every UI change in this lane (twin: each row lands in binder.gd AND BinderScreen.cs)

| surface | exists today? | CHANGE/ADD | exactly how (content, controls, position, states) | why the player needs it |
|---|---|---|---|---|
| binder · ledger — marketing lever row | yes (1 row, "reach — saturates past ~$2k", −/+ at x1000/1064) | CHANGE | replaced by a 4-row channel sub-block at 58px pitch: `ads` y96, `content` y154, `referrals` y212, `outbound` y270; each row = NAME (24px, x10) + one-line desc (18px, y+27) + `$N/wk` (26px PEN, x455) + live effect (20px, x640, w340) + −/+ steppers (52×44, x1000/x1064, y+2), LEVER_STEPS unchanged | the single blended lever hid the core decision; the mix IS the strategy |
| binder · ledger — channel section header | no | ADD | `MARKETING — the funnel mix · $X/wk · blended CAC $Y` (24px, ink 0.6) at y62; divider line (ink 0.25) at y333 under the block | groups the mix, keeps total spend + blended CAC (real name) in view while stepping |
| binder · ledger — channel live-effect strings | yes for old marketing ("reach ×N") | CHANGE | from engine formulas: ads `reach ≈N/wk` + ` (era ×0.35)` in garage/coworking; content `equity N% → ≈M/wk` or `fading −7%/wk`; referrals `word of mouth ×1.NN` or `nobody would vouch yet (v0.X)` when happy < 0.1; outbound `+R reach · +C closing` | mechanics visible at the point of decision (house rule), incl. the era discount and the NPS gate |
| binder · ledger — org lever rows (sales/care/rnd/office) | yes (78px pitch from y78) | CHANGE | texts/controls identical, repositioned to 62px pitch: y340/402/464/526 | makes room for 8 rows + P&L inside the 760px sheet |
| binder · ledger — unit-econ / P&L / bottom-line block | yes | CHANGE | same lines at y592/626/660/694; final slot y734 = warnings else rules line; NEW drop rule: if both warnings fire, the unit-econ line yields its slot; rules line reworded: `reach saturates · content compounds · only capacity closes · churn is a leaky bucket · three weeks below zero ends it` | the sheet must never overflow; the rules line teaches the two new laws |
| binder · customers — funnel bars | no | ADD | `the funnel, last week:` (24px, y316); `_FunnelBars` control at (10,348) 700×148: three bars 46px pitch h30, `REACH N (bought)` / `LEADS M (conv X%)` / `SIGNED K`, width 40+460×v/max, fills BLUE/YELL/SAGE 0.5α, ink wobble outline; `ceiling C hit` in PEN on SIGNED when close_rate < 0.9; shown at effective analytics ≥1 (an = min(level, era cap)) | the funnel is the subsystem — reach→leads→signed with conversion and the capacity ceiling, by their real names |
| binder · customers — CAC by channel block | no | ADD | at (740,348): title `CAC by channel` (22px) + 4 rows 26px pitch `ads $82 · $2,000/wk`; states: `—` when unfunded, coral `burning` when spend ≥ $500 and attributed < 0.05; shown at an ≥1 | per-channel CAC is the teaching heart: which dollar buys a customer cheapest |
| binder · customers — believed-market lines | yes (y356/392) | CHANGE | identical text moved to y512/548 | shifted below the funnel block |
| binder · customers — customers sparkline | yes (y132, 1120×200) | CHANGE | height 200→170 at y130 | reclaims 30px for the funnel block |
| binder · customers — an≥2 cohort read | partial (lifetime line) | CHANGE+ADD | y584: `of 100 who joined 12 wks ago, ~N are still here · churn X.X%/wk · care trims Y%` (26px, survival = (1−1/residence)^12); y620: `lifetime ≈ N wks at v0.X · blended CAC $Y · payback Z wks`; DELETES the old pseudo-CAC formula `mk/max(1, mk/900)` | retention/cohort by its real name; the old CAC line was a fiction the engine never computed |
| binder · customers — an≥3 truth line | yes (flavor only) | CHANGE | y668 (26px, SAGE): `the truth: ~TAM buyers (you believed B) · conv X.X% · close NN% · your cheapest customer comes from CHANNEL` | level 3 now pays with data, not a compliment |
| binder · customers — fog gating | yes (analytics_level 0–3) | CHANGE | displayed level = min(analytics_level, era cap: garage 1 / coworking 2 / office+ 3); an=0 text unchanged | attribution is an office-era capability; a garage has no data stack to buy |
| binder · vitals — burn line | yes | CHANGE | `marketing $` source: `state.marketing_budget` → pnl marketing sum lane | the old field goes stale once channels carry the spend |
| binder · tab bangs | yes (pricing / ledger / cap table) | ADD | `customers` joins `_bangs` (same coral `!`, position idiom b.position+(103,−12)): visible when any channel is burning (spend ≥ 500, att < 0.05) or content rots (b_con=0, C ≥ 0.3) | money quietly buying nothing is the exact moment the desk must call the player in |
| journal · weekly adds receipt | yes | CHANGE | `+N customers (organic a · word of mouth b · channels c)` — third source added | shows organic vs paid mix every week, by name |
| journal · mix receipt | no | ADD | when ≥2 channels funded: `the mix: ads $A→n1 · content $B→n2 (equity E%) · referrals $C→n3 · outbound $D→n4` | the weekly attribution statement — spend→customers per channel |
| journal · CAC-inflation receipt | no | ADD | trigger: b_ads ≥ 1.2× last wk AND ads CAC ≥ 1.25× last wk: `ads CAC rose to $X — the cheap audience is spent (saturation)` | teaches saturation causally, at the moment it bites |
| journal · content receipts | no | ADD | equity crosses 0.25/0.5/0.75 upward: `the library compounds: content reaches N/wk now, at $0 marginal`; b_con=0 & C ≥ 0.05: `the library goes quiet — content equity fades to N%` | compounding and rot are invisible without a stock read-out |
| journal · referral-gate receipt | no | ADD | b_ref ≥ 500 & happy < 0.1: `a referral program for a product nobody would vouch for (v0.X) — promoters first, program second` | names the NPS gate instead of silently eating the spend |
| journal · capacity receipt | no | ADD | close_rate < 0.9: `demand outran closing: N wanted in, you signed C — capacity, not demand, is the bottleneck (sales or outbound)` | the demand-vs-capacity distinction is the funnel's last lesson |
| journal · burning-channel receipt | no | ADD | spend ≥ 500 & att < 0.05: `$X into ads found nobody — saturated or mispriced` | pairs with the bang; says which dollar is dying and hints why |
| journal · pre-launch paid receipt | no | ADD | (b_ads+b_ob) > 0 & not launched: `reach with nothing to sign — ads and cold calls convert only after launch` | stops the classic pre-launch ad-spend mistake with a reason |
| journal · era receipt | no | ADD | once per run (flag `seen_paid_era_note`), era garage/coworking & (b_ads+b_con) ≥ 500: `the garage discount: paid reach ×0.35 — no brand, no pixel history. Outbound and word of mouth are the garage channels.` | teaches stage-appropriate acquisition the first time paid money goes in early |
| journal · DM set_budget confirmation | yes | CHANGE | cat `marketing` now prints `marketing set to $X/wk across the current mix — why` (split rule §5) | the player must see the DM funded the MIX, not one channel |
| journal · money line | yes | CHANGE | `marketing N` in the burn breakdown = channel sum (format unchanged) | continuity: one familiar number, now backed by four lanes |
| DM context (narration feed) | yes (`signals()`) | ADD | key `funnel_mix`: `ads $a · content $b (equity N%) · referrals $c · outbound $d · blended CAC $e`; `marketing_weekly` keeps sum semantics | the DM narrates channel life with zero new calls and can never contradict the ledger |
| coach lines | yes (system) | no change | v1 teaching rides the receipts above | receipts fire in-context; a parallel coach track would duplicate them |

## 7. LLM leverage — none; this subsystem is pure math

The honest answer per the house pattern: the funnel adds **zero LLM calls**.
Every number is engine arithmetic on state; every string above is authored.
The DM already narrates channels for free because `signals()` gains the
`funnel_mix` line — it rides the existing adjudication call.

Tempting-but-wrong cases, named so nobody adds them later:

- **"Have the LLM judge channel fit from the pitch"** ("this idea would go
  viral, boost ref_a") — wrong: numbers are engine-owned; the pitch already
  shapes the world via `biz_who`/`biz_what` → the CHANNELS table. If per-run
  temperament is ever wanted, it enters through clamped θ generation (§9.5),
  not a per-week opinion.
- **"Weekly flavor for campaigns"** (name the blog posts, the ad creative) —
  pure dressing with no mechanical content, at a weekly call cost the plan
  forbids for this desk. The journal's existing receipts carry the story.
- **"LLM explains the attribution"** ("your content is working!") — the
  receipts in §4 already explain cause deterministically, in the engine's own
  words; an LLM paraphrase could only drift from the math it describes.

---

## 8. Twin test pins (both suites; fixtures set `era="office"`, `launched`, no rivals)

1. **Baseline + determinism + conservation**: all channel budgets 0, equity 0
   → funnel meta zeroed, adds equals the mk_mult-free organic+wom path; two
   identical states tick to identical cash/traction/content_equity; and with
   any mix, `att_organic + att_wom + Σ att_channels == adds` (±1e-6).
2. **Ads instant, content week-1 weak; the garage discount**: SMB, product 50,
   $2,000 one week: ads-attributed ≥ 3× content-attributed (expected ≈7.1 vs
   2.2). Same state in garage era: ads-attributed < 0.4× its office value.
3. **Content beats ads over 12 weeks at equal total spend (SMB)**: $2,000/wk
   for 12 ticks each arm (fixture: sell 5, two sales hires, b_sales 2000 both
   arms so the cap never binds): cumulative content-attributed > cumulative
   ads-attributed (expected ≈114 vs 86); and content's week-12 weekly rate ≥
   1.5× ads'.
4. **Referrals need a product worth vouching for**: Consumer, A=1000, $2,000
   referrals: at product 10 referral-attributed == 0 (NPS gate); at product 80
   with care $1,000 it exceeds 5/wk — and exceeds the product-10 arm ≥ 10×.
5. **Outbound is Enterprise's channel, not Consumer's**: Enterprise $2,000:
   outbound-attributed > ads-attributed AND gtm_cap(ob=2000) > gtm_cap(0)
   (expected 12.2 vs 3.9). Consumer $2,000: outbound-attributed < 0.2× ads'.
6. **Migration + DM split**: a save dict with `budgets {"marketing": 2000}`
   loads to `ads 2000`, no `marketing` key, tick runs, `pnl.marketing == 2000`;
   `set_budget cat "marketing" v 2000` on an empty mix → ads 2000; on mix
   {ads 500, content 1500} → 500/1500 (largest-remainder, remainder→ads).

(Suites grow from 82 in lockstep — same `_ok`/`Ok` count both sides.)

---

## 9. Engine-improvement suggestions (≤5)

1. **Wave C hook — CAC inflation from rivals**: a rival "marketing blitz"
   multiplies effective `ads_sat` (auction pressure) for 3–4 weeks; one
   factor, receipted "the street is bidding against you".
2. **Wave D sibling — Enterprise pipeline consumes `leads_ob`**: named
   accounts spawn from outbound leads (meta `funnel.leads.outbound` is the
   reserved feed), closing the loop between §4 and §5 of the plan.
3. **Channel statuses in the catalog**: `ad_account_ban` (ads → 0, 2wk),
   `seo_update` (content equity −20% once), `press_surge` already lifts conv
   via status_adopt — typed names the DM can install, magnitudes housed in
   STATUS like everything else.
4. **Macro seasons (§8) modulate `conv` per audience** (consumer Q4 warm,
   enterprise December frozen) — one seeded multiplier, pre-announced.
5. **Per-run channel temperament via θ**: world-gen may nudge `ref_a`/`con_a`
   ±20% (clamped) from the pitch — run variety through the existing clamped-θ
   door, never a live LLM opinion.

## 10. Open questions (≤3)

1. **Era cap per channel or on the channel SUM?** Step clamp is per-lever
   today; four channels quadruple potential garage marketing. Recommendation:
   clamp Σ channels at `era_spend_cap` inside the channel rows' step function.
2. **Pre-launch content harvest**: equity already builds pre-launch and pays
   after — should launch also fire a one-time waitlist burst (e.g. equity ×
   con_a × conv × 4 as a spike)? Recommendation: no burst v1; the carried
   stock is already the reward, and a spike invites farming.
3. **DM channel cats now or later?** Schema keeps one `marketing` cat (§5).
   Revisit adding `ads/content/referrals/outbound` cats only if players ask
   the DM for channel moves in words the split rule mangles.
