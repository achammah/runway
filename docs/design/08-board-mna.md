# 08 — THE BOARD & M&A / EXIT OFFERS
Plan §9 + §10. Law: ENGINE owns every number, DM owns every sentence. No new LLM
calls — board and M&A beats ride the existing event/adjudication context.
North star: this desk TEACHES real governance and M&A by name — pre/post-money,
board seats, covenants, the pool shuffle, strategic premium, no-shop, secondaries
— and every receipt says WHY. Twin rule: every mechanic lands in
`game/src/core/sim_engine.gd` + `unity/Assets/Scripts/Core/SimEngine.cs`,
prompts byte-synced, pins in both suites.

---

## 1. STATE (new, durable — save-listed, never Object-meta)

`GameState` (Godot `game/src/core/game_state.gd`, Unity `Core/GameState.cs` with
`[JsonProperty]` snake_case; all added to `save_system.gd` dict / Unity save):

```gdscript
var board: Dictionary = {}      # {} until a round closes. Keys:
    # target_growth_pct: float   the growth covenant, %/quarter
    # target_revenue: int        $/wk to show at review
    # base_revenue: int          $/wk the covenant was set from
    # review_week: int           absolute week the review lands
    # strikes: int 0..3          missed covenants on record
    # goodwill: int 0..3         clean quarters + updates on record
var mna: Dictionary = {}        # {} or {buyer, price, why, premium, expires_week}
var mna_last_week: int = -99    # cooldown anchor (offer generation OR lapse)
var option_pool_pct: float = 0.0  # ESOP slice of the cap table (pool shuffle, §3)
var founder_banked: int = 0     # secondary proceeds — the founder's, run over or not
var macro_season: String = "steady"   # "winter"|"steady"|"boom" — see below
```

**Macro interface (coordinate with subsystem 8):** `macro_season` is WRITTEN
only by the macro engine; this subsystem only reads it. The field ships here
with default `"steady"` if macro's wave hasn't landed — everything degrades to
neutral. We consume the season label only.

Realism: a priced round installs governance — a board with information rights
reviewing actuals against the operating plan approved at the raise. `board` is
that plan-of-record; `review_week` is the quarterly board meeting at weekly
scale (12 wks ≈ a quarter). Simplification named: one aggregate board voice, no
per-seat votes — seat counts stay real (stamped in §3) but vote math is
dropped; the player answers to one voice anyway (the DM's).

## 2. SCALING BY STAGE — what unlocks at which era

`static func board_stage(state) -> int: return state.era_index()`
(garage 0 · coworking 1 · office 2 · floor 3 · hq 4). Stage is read LIVE at
each review/signing — governance grows when the company does, mirroring how a
real board hardens from an angel's phone call to audit-committee cadence.

| Stage / era | Governance that exists | Exact mechanics live |
|---|---|---|
| **0 garage** | No board. Angels are informal — expectations, not covenants. | A closed round still sets a target + 12-wk review, but misses install `investor_pressure` (3 wks) ONLY: **no strikes accrue, no coach, no reprice, no seat**. Beats still earn goodwill (the angel vouches for you — warmth works). Receipts say "the angel checks in", never "the board". |
| **1 coworking** | First board seat; light expectations. | `apply_round` stamps `board_seats_investor = max(current, 1)`. Full review cadence; strikes accrue but **cap at 2**: strike 2 sends the coach, there is NO reprice rung yet. |
| **2 office** | A real board: quarterly cadence, growth covenants, option-pool pressure. | Full strike ladder (§4): coach at 2, **reprice at 3**. Term sheets now carry the **pool shuffle**: `generate_offers` adds `pool_pct = 10.0` (a 10% option pool written pre-money — §3). |
| **3 floor** | Multi-investor politics; secondaries open. | Round ≥3 closed at stage ≥3 stamps a **2nd investor seat** (cap 3 total). A covenant miss ALSO costs `hype −2` (board leaks travel). Signing weeks offer a **secondary** card when `goodwill ≥ 2` (§7): sell 5 points of founder stake at a 15% discount to the round price → `founder_banked`. Pool ask stays 10%. |
| **4 hq** | Exit-grade governance; the IPO window opens as the alternative to M&A. | Strike 2 ALSO costs `hype −5` (public company scrutiny, pre-IPO). Pool ask drops to 5% (later rounds top up, not create). **IPO window** (§6): weekly engine check — open sets `ipo_window`, the journal shows the bell card. |

Everything below is written stage-aware; where a rule differs by stage, the
stage column above is the authority.

## 3. ROUND CLOSE: SEATS, THE POOL SHUFFLE, COVENANT SET (engine)

**Where:** `SimEngine.apply_round` (Godot `sim_engine.gd:645`, Unity
`SimEngine.cs:1088`). New signature (twin-exact):
`apply_round(state, amount: int, equity_pct: float, pool_pct: float = 0.0)`.

```gdscript
static func apply_round(state, amount, equity_pct, pool_pct := 0.0) -> void:
    state.cash += amount
    # THE POOL SHUFFLE: the pool is created PRE-money, so it dilutes only the
    # existing side — then the investor's slice dilutes everyone including it.
    var pool_keep := 1.0 - clampf(pool_pct, 0.0, 15.0) / 100.0
    var inv_keep  := 1.0 - equity_pct / 100.0
    state.founder_pct = maxf(state.founder_pct * pool_keep * inv_keep, 1.0)
    for cf in state.cofounders:
        cf["equity_diluted"] = float(cf.get("equity_diluted", cf.get("equity", 0.0))) * pool_keep * inv_keep
    state.option_pool_pct = (state.option_pool_pct * pool_keep + clampf(pool_pct, 0.0, 15.0)) * inv_keep
    # ladder append (unchanged), morale +5 (unchanged), then:
    var stage := board_stage(state)
    if stage >= 1:
        state.board_seats_investor = clampi(maxi(state.board_seats_investor,
            1 + (1 if state.rounds_raised.size() >= 3 and stage >= 3 else 0)), 0, 3)
    var pnl: Dictionary = state.get_meta("pnl", {})
    var base_rev := maxi(int(pnl.get("revenue", 0)), int(ERA_REV_FLOOR[state.era]))
    var pct := board_target_pct(state)
    state.board = {"target_growth_pct": pct, "base_revenue": base_rev,
        "target_revenue": int(float(base_rev) * (1.0 + pct / 100.0)),
        "review_week": state.week + 12,
        "strikes": int(state.board.get("strikes", 0)),     # a new round inherits the record
        "goodwill": int(state.board.get("goodwill", 0))}
```

`generate_offers` (salt 9, `sim_engine.gd:626`) adds to each offer dict:
`"pool_pct": 10.0 if stage in [2,3] else (5.0 if stage == 4 else 0.0)`. The
signing handlers pass it through to `apply_round`.

**Covenant math:**

```gdscript
const ERA_REV_FLOOR := {"garage": 40, "coworking": 120, "office": 500,
    "floor": 2000, "hq": 8000}   # a pre-revenue raise still gets a concrete bar

static func board_target_pct(state) -> float:
    var r := state.rounds_raised.size()                  # ≥1 when a board exists
    var base := 25.0 + 5.0 * float(mini(r, 4))           # 30/35/40/45 %/quarter
    var era_m: float = {"garage": 1.0, "coworking": 1.0, "office": 0.9,
        "floor": 0.8, "hq": 0.65}.get(state.era, 1.0)
    var mac_m := 0.7 if state.macro_season == "winter" else (1.2 if state.macro_season == "boom" else 1.0)
    return snappedf(clampf(base * era_m * mac_m, 10.0, 60.0), 1.0)
```

Realism, per term:
- **30–45%/quarter by round** ≈ T2D3: a seed/A company tripling ARR yearly is
  ~31%/quarter compounding; each round resets the bar higher.
- **Era discount 1.0→0.65** = growth persistence decay: big bases grow slower
  and real boards plan for exactly that.
- **Winter ×0.7 / boom ×1.2** = boards re-forecast to the climate (the 2022
  "cut the plan" letters); a winter board asks for less, not nothing.
- **Pool shuffle 10%→5%** = the standard term-sheet move: the option pool is
  written into the PRE-money so the dilution lands on the founders' side, not
  the investor's; later rounds top up a smaller amount. Simplification named:
  pool is never granted to employees as actual equity effects — it sits as a
  cap-table slice only (hiring already pays cash salaries). Acceptable: the
  LESSON is where the pool comes from, and the pie shows it.
- Simplification named: real covenants are multi-metric (ARR, burn multiple,
  runway). One revenue bar keeps the binder readable; burn is already policed
  by the world (`weeks_in_red` death).

## 4. REVIEW RESOLUTION (weekly tick, deterministic — no dice, no salt)

**Where:** new step **9c** in `weekly_tick`, after 9b (beliefs,
`sim_engine.gd:427`), before 10 (commitments) — the week's revenue exists
there. Fires when `not state.board.is_empty() and state.week >= int(state.board.review_week)`.

```
measured = int(round(revenue))          # step 9's local
b = state.board; stage = board_stage(state)
if state.has_flag("investor_update_sent"):
    b.goodwill = mini(goodwill + 1, 3); erase flag
    rep.lines += "the update you sent bought patience — the room read it (+goodwill)"
BEAT (measured >= b.target_revenue):
    b.strikes = maxi(strikes - 1, 0); b.goodwill = mini(goodwill + 1, 3)
    add_status("board_delight", 4)
    rep.lines += "BOARD REVIEW — COVENANT MET: $%d/wk against the $%d bar. A clean
                  quarter is cheap capital later (board_delight, 4 wks)"
    (stage 0 wording: "the angel checked in — the numbers spoke for you")
MISS (measured < b.target_revenue):
    stage 0: add_status("investor_pressure", 3)   # informal — no strike ladder
        rep.lines += "the angel checked in — $%d/wk against the $%d you talked about.
                      Awkward calls all week (investor_pressure, 3 wks)"
    stage >= 1:
        b.strikes = mini(strikes + 1, 2 if stage == 1 else 3)
        add_status("investor_pressure", 4)
        rep.lines += "BOARD REVIEW — COVENANT MISSED: $%d/wk against the $%d bar.
                      Strike %d (investor_pressure, 4 wks)"
        if stage >= 3: hype = clampi(hype - 2, 0, 100)   # board leaks travel
        if strikes hits 2:
            commitments += {"name": "the executive coach the board sent",
                "cash_wk": -clampi(int(payroll * 0.05), 250, 2500), "weeks_left": 6}
            rep.events += "STRIKE TWO — the board sent a CEO coach: $%d/wk for six
                           weeks. This is what boards do before they do worse"
            if stage == 4: hype = clampi(hype - 5, 0, 100)   # scrutiny is public now
        if strikes hits 3 (stage >= 2 only):
            theta.funding_mult = maxf(funding_mult * 0.8, 0.5)   # THETA clamp floor
            set_flag("down_round_threat")
            rep.events += "STRIKE THREE — the board reprices you: every future round
                           now values the company 20% lower. That is a down round
                           waiting to happen"
RE-ARM (both branches):
    b.base_revenue = maxi(measured, ERA_REV_FLOOR[era])
    b.target_growth_pct = board_target_pct(state)     # era/round/season re-read live
    b.target_revenue = int(base * (1 + pct/100)); b.review_week = week + 12
```

Payroll is step 9's local; reuse it. Clamps: strikes ∈ [0, stage cap], goodwill
∈ [0,3], coach ∈ [$250,$2500]/wk, `funding_mult` floored at 0.5 so repeated
strike-3s converge, never zero.

**STATUS catalog addition** (Godot `STATUS` at `sim_engine.gd:76` + Unity twin —
exact magnitudes; the DM may also install it by name like any catalog status):

```gdscript
"board_delight": {"adv": "raise", "morale_wk": 2.0, "hype_wk": 3.0, "kind": "buff"},
```

`investor_pressure` (exists: `morale_wk −2.0, dis: raise`) is the miss condition.

**Warmth integration** — `warmth_pct` (`sim_engine.gd:620`, Unity `:1051`)
becomes trait warmth ± the governance record:

```gdscript
static func warmth_pct(state) -> float:
    var doors := state.trait_level("credibility") + state.trait_level("network")
    var trait_w := minf(2.0 * float(maxi(doors - 6, 0)), 8.0)     # unchanged
    var brd := 2.0 * float(state.board.get("goodwill", 0)) \
             - 2.5 * float(state.board.get("strikes", 0))
    return clampf(trait_w + brd, 0.0, 12.0)
```

`generate_offers` and the binder's dilution preview already consume
`warmth_pct` — beat streaks warm the next round, strikes chill it. Realism: a
clean governance record IS lower perceived risk and a smaller equity ask;
missed plans are a risk premium. Simplification named: goodwill never decays on
a calendar — the 3-cap and the strike offset bound it.

Realism, resolution ladder: first miss = a hard meeting (pressure); repeated
misses = the real intervention ladder — boards hire the CEO an executive coach
before anything harsher; persistent misses = repricing/structure (the
down-round threat made real through `funding_mult`). Simplification named: the
real terminal rung is replacing the founder-CEO; we stop at coach + reprice
because firing the player ends the game's premise — the autopsy can still say
the board won.

## 5. THE INVESTOR-UPDATE MOVE (existing adjudicator, one paragraph)

No new op. Path: the DM grades the written move → emits catalog `status` +
`set_flag`; the ENGINE converts the flag to goodwill at the next review (§4),
so the LLM never touches a board number.

**Exact text to append to `game/data/prompts/adjudicator.txt` (and byte-synced
`unity/Assets/StreamingAssets/prompts/adjudicator.txt`), after the RAISE
paragraph (§ "A successful RAISE move…", line ~38):**

> ## THE INVESTOR UPDATE
> A move that genuinely reports to investors — a written monthly update, board
> memo, metrics email, a KPI deck with real numbers — is a real lever. On fine
> or brilliant: emit `status` `data_room_ready` (weeks 3) and `set_flag`
> `investor_update_sent`; the engine converts that flag into board goodwill at
> the next review — never touch warmth, strikes, or the board's numbers
> yourself. On risky: the flag only. On backfired (numbers dressed up, a lie in
> the deck the board can check): `status` `investor_pressure` (weeks 2)
> instead. An update with nothing new to say earns nothing twice: if
> `investor_update_sent` is already in run_state.flags, narrate the silence and
> emit neither op.

Realism: regular no-surprises updates measurably keep the data room warm and
improve follow-on odds; a caught-embellished one does the opposite.
`data_room_ready` (exists, `adv: raise`) IS the maintained data room, by name.

## 6. M&A OFFERS + THE IPO WINDOW (engine, salt 95)

**Where:** new step **9d** in `weekly_tick`, immediately after 9c (valuation
now reflects the week). One live offer max. All rolls on `_rng(state, 95)` —
salt 95 is unused (in use: 4,5,6,7,9,77,88,91,93).

**Lapse first** (step 9d, before generation): if `mna` live and
`state.week > int(mna.expires_week)`: `morale −3` (−5 when `why=="lifeline"`),
`hype +2`, `mna = {}`, `mna_last_week = state.week`, line:
`"the no-shop lapsed — the offer is off the table. The team heard the number
(−3 morale); so did the street (+2 hype)"`.
Realism: LOIs die by lapse; leaked deal talk destabilizes a team while a public
suitor validates the market. Writing any other move IS walking away.

**Generation gate:** `mna.is_empty() and week >= mna_last_week + 10 and week >= 6`.
The 10-week cooldown ≈ one approach per quarter — corp dev does not re-approach
the week after a lapse.

**Triggers, priority order (first hit wins), `r := _rng(state, 95)`,
`v := valuation(state)`:**

| # | why | condition | fires | premium band | buyer |
|---|-----|-----------|-------|--------------|-------|
| 1 | `lifeline` | (`weeks_in_red >= 2` or `runway_weeks <= 2`) and (`traction >= 5` or `product >= 30`) | always (it's the floor) | `0.3 + r.randf()*0.2` → 0.3–0.5× | strongest rival if strength ≥ 55, else "a quiet strategic" |
| 2 | `rival` | any rival `strength >= 70` | `r.randf() < 0.20` | `0.9 + r.randf()*0.4` → 0.9–1.3× | that rival's `name` |
| 3 | `boom` | `macro_season == "boom"` and `v >= 500_000` | `r.randf() < 0.15` | `1.2 + r.randf()*0.6` → 1.2–1.8× | "a strategic riding the market" |
| 4 | `milestone` | `v` first crosses $2M / $10M / $50M (one-shot flags `mna_band_2m/10m/50m`) | `r.randf() < 0.35`/wk while in a new band | `1.0 + r.randf()*0.5` → 1.0–1.5× | strongest rival ≥ 55 else "a strategic who has been watching" |

```gdscript
state.mna = {"buyer": buyer, "why": why, "premium": snappedf(prem, 0.01),
    "price": maxi(int(float(v) * prem), 10_000), "expires_week": state.week + 2}
state.mna_last_week = state.week
rep.events += "AN OFFER FOR THE COMPANY: %s puts $%s on the table — a %d%%
    %s on your $%s standalone value. The no-shop clock runs 2 weeks"
    # %s-detail: premium >= 1.0 → "strategic premium"; lifeline → "acqui-hire
    # discount — they are pricing the team and the shutdown avoided, not the business"
```

**THE IPO WINDOW (stage 4 only — the alternative exit).** Same step, after M&A:

```
open  := era == "hq" and traction >= 100 and rounds_raised.size() >= 2
         and board.strikes == 0 and macro_season != "winter"
opening: set_flag("ipo_window"), rep.events += "THE IPO WINDOW IS OPEN — clean
         covenants, a hundred believers, and a market that's buying. The bell
         is a journal card while it lasts"
closing (any condition breaks while flag set): clear flag, rep.lines +=
         "the IPO window closed — %s" (reason: "winter came" / "the board's
         strikes" / "the numbers slipped")
bell price (computed at signing, not stored): int(valuation(state) *
         (1.35 if macro_season == "boom" else 1.1))   # IPO pop premium
```

Realism, per band: public-M&A control premia run ~20–50% over standalone — the
`milestone`/`rival` bands sit there, a rival sometimes lowballing (0.9×: a
consolidator buying a wounded competitor). `boom` 1.2–1.8× is frothy-market
multiple expansion. `lifeline` 0.3–0.5× is distressed acqui-hire economics.
The 2-week hard clock is the exploding LOI / no-shop window at game scale. The
IPO window is literally that — a market condition, not a decision: it opens on
clean governance in a receptive market and shuts in winters. Simplifications
named: (a) **no liquidation-preference stack** — in a real 0.3× exit, 1× prefs
would likely wipe common and `founder_pct × price` overstates the check;
accepted because the WHOLE game scores `founder_pct × price` (finale law) and
the cap table has no preference ledger (see suggestions). (b) no
earnouts/retention/lockups — one number, one signature, roguelike-clean.

## 7. SIGNING UX — the journal term-sheet idiom, reused

**Godot** `game/src/screens/garage_view_screen.gd :: _spread_ahead()`. Three
additions to the special block (Unity `JournalSpreads.cs`: mirror as
`bool specialUsed = TermSheets() || MnaCard() || IpoBell() || LevelUp();`,
line 342; `SecondaryCard()` lives inside `TermSheets()`).

**(a) Term-sheet week teaching lines + pool + secondary** (inside the existing
`fundraising_open` block, `~line 2140`): after the warmth line, add ONE faint
line naming the mechanics (numbers all engine-owned):

```gdscript
var pre := SimEngine.valuation(state)
_jp.line("pre-money $%s — their check makes the post; their slice = check ÷ post.%s" % [
    _fmt(pre), ("  their sheet writes a %d%% option pool PRE-money — the pool
    shuffle comes out of your side." % int(offers[0].pool_pct)) if float(offers[0].pool_pct) > 0.0 else ""], true)
```

The sign handler passes the pool through:
`SimEngine.apply_round(state, o2.amount, o2.equity_pct, float(o2.get("pool_pct", 0.0)))`
and appends the formation receipt to the log:
`"a board now sits between you and the company: %d investor seat(s) · growth
covenant %d%%/quarter · first review wk %d"` (stage 0: `"the angel shook on
it: %d%%/quarter is the number you said out loud — talk again wk %d"`).

**Secondary card** (stage ≥ 3 and `board.goodwill >= 2`, once per signing
week, drawn beside the three sheets): card
`{"id": "sec:0", "text": "secondary $%dk" % bank/1000}` where
`bank := int(float(pre + offers[0].amount) * 0.05 * 0.85)` — 5 points of
founder stake at a 15% discount to the round price (secondaries price below
primary). On tap: `founder_pct = maxf(founder_pct - 5.0, 1.0)`,
`founder_banked += bank`, log
`"SECONDARY: sold 5 points of YOUR OWN stake at a 15% discount — $%s banked,
yours whatever happens to the company"`. Realism: founder secondaries are how
real boards let founders de-risk at floor-stage; the discount and the
goodwill-gate (only trusted founders get board consent) are the lesson.
Scoring: `FinaleScreen._score()` becomes
`final_payout = int(base * mult) + state.founder_banked` with chip
`["CHIPS OFF THE TABLE", "+$%s" % _fmt(banked), BLUE]` when banked > 0 — banked
cash is NOT multiplied (it already left the casino: de-risking forgoes upside,
that IS the teaching). The autopsy adds one line when dead with banked > 0:
`"you banked $%s on the way down. The company died; you didn't."`

**(b) M&A card** — insert directly after the term-sheets block (`~line 2163`),
sets the same `special_used = true`:

```gdscript
if not state.mna.is_empty():
    special_used = true
    var mo: Dictionary = state.mna
    _jp.line("SOMEONE WANTS TO BUY THE COMPANY:")
    _jp.line("$%s all-in · your %d%% = $%s · no-shop ends in %d wk — or write
        anything else and let it lapse." % [_fmt(int(mo.price)),
        int(state.founder_pct),
        _fmt(int(float(mo.price) * state.founder_pct / 100.0)),
        maxi(int(mo.expires_week) - state.week, 0)], true)
    _jp.icon_row([{"id": "mna:0", "text": "%s  $%.0fk" % [
        String(mo.buyer).split(" ")[0].left(9), float(mo.price) / 1000.0]}],
        Vector2(300, 40), "body")
    _jp.choice_made.connect(func(id: String):
        if id != "mna:0": return
        if not _mna_armed:                       # first tap arms — selling ends the run
            _mna_armed = true
            # rebuild the row with caption "SELL — tap again" (same idiom)
            return
        state.exit_value = int(state.mna.price)
        state.set_flag("acquired_exit")
        state.log_action("SOLD to %s for $%d (%s)" % [state.mna.buyer,
            int(state.mna.price), String(state.mna.why)])
        state.mna = {}
        _sfx["win"].play()
        _over = true
        done.emit({"victory": true}))
```

`_mna_armed: bool` screen var, reset each `_spread_ahead`. The two-tap arm
exists because, unlike a term sheet, this tap ends the run.

**(c) The bell** (flag `ipo_window`, no live M&A card competing): same two-tap
idiom, card `{"id": "ipo:0", "text": "RING THE BELL  $%.0fM" }`, faint line
`"an IPO prices the company at $%s — your %d%% = $%s. Windows close."` On the
armed tap: `state.exit_value = int(valuation * (1.35 boom else 1.1))`,
log `"FILED. Priced at $%s."`, `_over = true`, `done.emit({"victory": true})` —
NO `acquired_exit` flag, so main routes it as an IPO (below).

**Run-end wiring (exact existing path, nothing new):**
`done.emit({"victory": true})` → `main.gd::_after_grind` (line 1228):
`acquired_exit` flag → `FinaleScreen.setup(state, "acquisition")`; no flag +
`era == "hq"` → `"ipo"`. Both use `state.payout_today()` =
`exit_value × founder_pct / 100` (`game_state.gd:268`) → `fin.done` →
`_to_autopsy(result, exit_kind)`. Signing-room vs bell art, style bonuses, the
last page — all come for free.

## 8. DESK — binder `cap table` tab

**Godot** `game/src/ui/binder.gd :: _tab_cap()` (line 509; Unity
`BinderScreen.cs::TabCap()` line 648 mirrors 1:1). Existing geometry stays:
pie (40,30) 430², right column x=540, term-sheet banner (40,480).

**Pie gains the pool slice** (4 slices — the shuffle made visible):

```gdscript
{"pct": founder, PEN, "you %.0f%%"} · {cof, BLUE, "cofounders"} ·
{state.option_pool_pct, YELL, "option pool %.0f%%"} ·
{max(100 − founder − cof − pool, 0), SAGE, "investors"}
```

**Right column** — the dilution preview (line 543) is retitled to teach
pre/post by name (same math, same slot):
`"raise ~$%s now: pre-money $%s → post $%s — they'd ask ≈ %.0f%%%s · your
%.0f%% → ≈ %.0f%%"` (+ existing warmth parenthetical). At stage ≥2 append:
`" · plus a ~10%% pool written pre-money"`.

**New rows (all left column, w=470, under the pie — stays clear of x=540):**

```
y=520  OFFER BANNER (mna live) PEN sz27 w=1100:
       "⚡ ON THE TABLE: {buyer} — ${price} ({premium}× standalone) · your slice
        ${…} · no-shop ends in {n} wk. The journal signs."
       (ipo_window and no mna: "⚡ THE IPO WINDOW IS OPEN — the bell is in the
        journal. Windows close.")
y=568  "the board:" 32pt INK      (board non-empty; stage 0 says "the angel:")
y=610  covenant line 28pt INK w=470:
       "growth covenant: ${target_revenue}/wk by wk {review_week} — now
        ${pnl.revenue}/wk · {review_week − week} wks left"
y=654  record line 28pt (PEN if strikes>0 else SAGE) w=470:
       "strikes {✗×strikes}{·×(cap−strikes)} · goodwill {goodwill}/3 · the room
        is {strikes>=2: "ice" · goodwill>=2: "warm" · else "professional"}"
y=698  stage line 24pt faint INK w=470 — the governance vocabulary, by era:
       stage0 "no board — an angel and a handshake"
       stage1 "1 investor seat — expectations, lightly held"
       stage2 "a real board: covenants + the pool shuffle"
       stage3 "%d investor seats — politics, leaks, secondaries" 
       stage4 "exit-grade governance — clean quarters open windows"
```

No board: rows y≥568 absent — the clean page IS the bootstrap flex.

**Bang** (`binder.gd::_refresh()` line 125 + Unity line 174) — cap table `!`:

```gdscript
visible = state.has_flag("fundraising_open") or not state.mna.is_empty() \
    or state.has_flag("ipo_window") \
    or (not state.board.is_empty() and int(state.board.review_week) - state.week <= 1)
```

## 9. WEEKLY-TICK ORDER, FEEDS, RECEIPTS

**Tick order (final):** 1 clocks · 2 statuses · 3 pipeline · 4 morale/fatigue ·
5 debt · 6 rivals · 7 trend · 8 adoption/churn · 9 money+unit econ+loan ·
9b beliefs · **9c board review** · **9d M&A lapse→generation→IPO window** ·
10 commitments · snapshot · clamp. (9c needs the week's revenue; 9d needs the
post-revenue valuation; both precede commitments so the coach bills starting
NEXT week — you get the week you were told about it.)

**Receipts:** exact strings in §4/§6 — every one names its mechanism (covenant,
strike, strategic premium, acqui-hire, no-shop, pool shuffle, secondary,
window) AND says why it matters in the same breath. That sentence-with-a-reason
format is the pedagogy contract for any future line here.

**`SimEngine.signals()` additions** (adjudicator's ENGINE SIGNALS):

```gdscript
"board": ("review wk%d (in %d): covenant $%d/wk · now $%d/wk · strikes %d ·
    goodwill %d" % [...]) if board else "no board — nobody to answer to",
"mna": ("offer on the table: %s at $%d — no-shop ends wk%d" % [...]) if mna else "",
"ipo_window": state.has_flag("ipo_window"),
```

**`GameState.to_digest()` additions** (event generation): the same board/mna
strings as `"board_review"` / `"acquisition_offer"`, present only when live.

**`event_generator.gd::_directives()` additions (deterministic register):**

```
review this week : "- BOARD REVIEW THIS WEEK: the covenant is $X/wk revenue; the
                    company sits at $Y/wk. The boardroom is part of this week's story."
review next week : "- The board reviews NEXT week: covenant $X/wk, now $Y/wk. The
                    founder can feel it."
strikes == 2     : "- The board is one missed review from repricing the company.
                    The coach's sessions are on the calendar."
mna live         : "- AN ACQUISITION OFFER IS ON THE TABLE: {buyer} at ${price},
                    no-shop ends week {w}. Weave the courtship; only the journal
                    card signs — never close or kill the deal yourself."
ipo_window       : "- THE IPO WINDOW IS OPEN. Bankers circle; the bell is the
                    founder's to ring in the journal, never yours."
```

**Journal week-ahead faint line** (both engines, beside the status line in
`_spread_ahead`): when `review_week − week == 1` →
`"the board reviews next week — the covenant is $X/wk."` (helper
`SimEngine.board_review_in(state) -> int`, −1 when boardless).

## 10. INTERFACE DELTA — every UI change this lane needs (assessable, standalone)

One row per element. Godot surface named; every row lands TWIN in Unity
(`BinderScreen.cs::TabCap`, `JournalSpreads.cs`, the Unity finale/autopsy).

| # | Surface | Exists today? | CHANGE / ADD | Exactly how (content, controls, position, states) | Why the player needs it |
|---|---------|---------------|--------------|---------------------------------------------------|-------------------------|
| 1 | Binder · cap table · equity pie | YES — 3 slices (you / cofounders / investors), 430px at (40,30) | CHANGE | 4th slice "option pool N%" (YELL) between cofounders and investors, fed by `option_pool_pct`; 0% slice hidden | The pool shuffle is invisible until the pool is a drawn slice carved from the founder's side |
| 2 | Binder · cap table · dilution preview line | YES — "raise ~$X now → investors ask ≈…" at (540, y+186) | CHANGE | Reworded to name the mechanics: "raise ~$X now: pre-money $V → post $V+X — they'd ask ≈P%(warmth note) · your F% → ≈F'%"; at office+ appends " · plus a ~10% pool written pre-money" | Teaches pre/post-money and where the ask comes from BEFORE the first signature |
| 3 | Binder · cap table · offer/window banner | NO | ADD | One line, (40,520), PEN, 27pt, w=1100. State A (M&A live): "⚡ ON THE TABLE: {buyer} — ${price} ({premium}× standalone) · your slice ${…} · no-shop ends in {n} wk. The journal signs." State B (`ipo_window`, no offer): "⚡ THE IPO WINDOW IS OPEN — the bell is in the journal. Windows close." | A live clock must be visible without opening the journal |
| 4 | Binder · cap table · board header | NO | ADD | (40,568), 32pt INK: "the board:" — stage 0 says "the angel:"; absent when no round closed | Names who the founder answers to; the empty page IS the bootstrap flex |
| 5 | Binder · cap table · covenant line | NO | ADD | (40,610), 28pt INK, w=470: "growth covenant: ${T}/wk by wk {N} — now ${R}/wk · {k} wks left" | Target + countdown at a glance, with the real term on it |
| 6 | Binder · cap table · record line | NO | ADD | (40,654), 28pt, PEN if strikes>0 else SAGE, w=470: "strikes ✗✗· · goodwill 2/3 · the room is {ice/warm/professional}" (✗ per strike, · per empty slot up to the stage cap) | The miss ladder is a visible track, never a surprise |
| 7 | Binder · cap table · stage line | NO | ADD | (40,698), 24pt faint INK, w=470; five authored strings by era (garage "no board — an angel and a handshake" → hq "exit-grade governance — clean quarters open windows") | Shows governance hardening as the company scales — the scale-progressive lesson in one line |
| 8 | Binder · cap table · tab bang (!) | YES — fires on `fundraising_open` only | CHANGE | Also visible when: M&A offer live, `ipo_window` set, or `review_week − week <= 1` | Every time-boxed cap-table decision pulls the eye to the desk |
| 9 | Journal · week-ahead · term-sheet block | YES — 3 cards 230×40 + warmth line | CHANGE | Add one faint teaching line under warmth: "pre-money $V — their check makes the post; their slice = check ÷ post." + pool sentence at office+ ("their sheet writes a 10% option pool PRE-money — the pool shuffle comes out of your side."); sign handler now passes `pool_pct` and logs the formation receipt (#14) | The moment of signing is the ONE teachable moment for round math |
| 10 | Journal · week-ahead · secondary card | NO | ADD | One extra card "secondary $Xk" beside the sheets; only stage ≥3 AND goodwill ≥2, once per signing week; single tap: founder_pct −5, `founder_banked += X`, receipt printed | Teaches founder secondaries: de-risking at a discount, unlocked by board trust |
| 11 | Journal · week-ahead · M&A card | NO | ADD | "SOMEONE WANTS TO BUY THE COMPANY:" + faint math line ("$P all-in · your F% = $… · no-shop ends in n wk — or write anything else and let it lapse.") + one 300×40 card "{buyer} $Nk". TWO-TAP: tap 1 re-captions "SELL — tap again"; tap 2 sets exit and ends the run into the acquisition finale | Selling ends the run — an irreversible tap gets an arm; the faint line teaches exit = your % × price |
| 12 | Journal · week-ahead · IPO bell card | NO | ADD | Same two-tap idiom when `ipo_window` set and no M&A card: card "RING THE BELL $XM" + faint line "an IPO prices the company at $V′ — your F% = $…. Windows close." | The alternative exit is a priced choice on paper, not a cutscene |
| 13 | Journal · week-ahead · review reminder | NO | ADD | Faint line by the status line when `review_week − week == 1`: "the board reviews next week — the covenant is $T/wk." | One week of warning makes the review a playable deadline |
| 14 | Journal · outcome log · formation receipt | NO | ADD | Printed the week a round signs: "a board now sits between you and the company: {s} investor seat(s) · growth covenant {p}%/quarter · first review wk {N}" (stage 0: "the angel shook on it: {p}%/quarter is the number you said out loud — talk again wk {N}") | The obligations taken with the check enter the written record |
| 15 | Journal · outcome log · review receipts | NO | ADD | "BOARD REVIEW — COVENANT MET/MISSED: $R/wk against the $T bar. …"; "STRIKE TWO — the board sent a CEO coach: $N/wk for six weeks. This is what boards do before they do worse"; "STRIKE THREE — the board reprices you: every future round now values the company 20% lower…" (coach cost also appears as a normal commitment lane in the P&L) | Receipts carry the WHY; consequences are named, never mysterious |
| 16 | Journal · outcome log · M&A receipts | NO | ADD | Arrival: "AN OFFER FOR THE COMPANY: {buyer} puts ${P} on the table — a {x}% strategic premium on your ${V} standalone value. The no-shop clock runs 2 weeks" (lifeline wording: "acqui-hire discount — they are pricing the team and the shutdown avoided, not the business"). Lapse: "the no-shop lapsed — the offer is off the table. The team heard the number (−3 morale); so did the street (+2 hype)" | Premium-vs-standalone and offer death are the two M&A lessons |
| 17 | Journal · outcome log · IPO window receipts | NO | ADD | Open: "THE IPO WINDOW IS OPEN — clean covenants, a hundred believers, and a market that's buying…"; close: "the IPO window closed — {winter came / the board's strikes / the numbers slipped}" | Windows are weather; the close reason teaches what keeps them open |
| 18 | Journal · outcome log · update conversion receipt | NO | ADD | At the review following a graded investor-update move: "the update you sent bought patience — the room read it (+goodwill)" | Closes the loop on a written move weeks after it was made |
| 19 | Binder · threats tab · statuses list | YES — auto-lists the STATUS catalog | NO CHANGE (rides existing) | `board_delight` (new catalog entry: adv raise, +2 morale/wk, +3 hype/wk) and `investor_pressure` show with durations automatically | Review outcomes surface at zero UI cost |
| 20 | Garage room · equity sticky ("YOURS N%") | YES | NO CHANGE | Already prints `founder_pct`, which now also moves with pool + secondaries | The room quietly reflects every dilution event |
| 21 | Finale screen · hero number + chips | YES | CHANGE | `_score()`: `final_payout = int(base × mult) + founder_banked`; new chip "CHIPS OFF THE TABLE +$X" (BLUE) when banked > 0; (pending open Q1: lifeline kicker "THE SOFT LANDING") | Secondary cash is real and un-multiplied — de-risking forgoes upside, visibly |
| 22 | Autopsy screen · last page | YES | CHANGE | One added line when dead and banked > 0: "you banked $X on the way down. The company died; you didn't." | The secondary lesson lands hardest on a death |
| 23 | Coach (first-install tutorial chips) | YES — 3 chips, week 1 | NO CHANGE | Board/M&A teaching is contextual: rows #14 and #4–7 appear exactly when the mechanic first exists; week 1 has no board | Teaching at the moment of relevance beats a longer tutorial |

## 11. LLM LEVERAGE (all riding EXISTING calls — zero new calls)

Where a model genuinely earns its seat:
1. **Courtship dressing (event context).** Trigger: `mna` non-empty →
   `acquisition_offer` digest line + directive. The weekly event/DM call turns
   "buyer, price, deadline" into a scene — the dinner, the sheet across the
   table, the rival's smirk — in the world bible's voices. NEVER decides:
   price, premium, expiry, whether an offer exists, or closing it (the
   directive forbids it explicitly).
2. **Boardroom narration (adjudication context).** Trigger: review directives +
   the engine's receipt lines in ENGINE SIGNALS. The DM gives the beat/miss a
   face (which director speaks, what the coach is like). NEVER decides:
   met/missed, strikes, statuses, coach cost — tick 9c installed them before
   any call.
3. **Investor-update adjudication (§5).** Grading a free-written move as a real
   update vs theater IS the adjudicator's core competence; output capped at two
   whitelisted ops + one flag; the engine converts flag→goodwill.
4. **Buyer naming.** When the buyer is "a quiet strategic", the DM may name the
   firm in narration; the engine string stays the stored truth. (Persistent
   named acquirers would be a WorldGen bible entry at run birth — still no
   weekly call.)

Tempting but WRONG uses (rejected):
- **LLM pricing the acquisition / picking premium or pool** — anchors on
  narrative drama, breaks replayability, violates the money law.
- **LLM judging the covenant** — it would grade story effort, not revenue
  against a bar.
- **LLM inventing board statuses or strike counts** — catalog-only rule exists
  for exactly this; unknown names drop.
- **LLM-triggered offers or windows** ("this feels like an exit moment") —
  frequency must be seeded and replay-stable; salt 95 owns it.
- **A dedicated board-meeting call each review** — cost and latency for
  sentences the existing calls already produce from receipts.

Keyless mode: fully playable — every board/M&A/IPO beat is a complete engine
receipt sentence; the DM only ever adds flavor on top.

## 12. TWIN TEST PINS (add to `game/tests/sim_engine_test.gd` + Unity suite)

1. **Round close does the full shuffle:** fresh coworking-era state,
   `apply_round(s, 100_000, 20.0, 10.0)` → `founder_pct == 100×0.9×0.8 = 72`,
   `option_pool_pct == 10×0.8 = 8`, `board_seats_investor == 1`,
   `board.review_week == week + 12`, `10 ≤ target_growth_pct ≤ 60`,
   `target_revenue ≥ ERA_REV_FLOOR[era]`.
2. **Review is deterministic and re-arms:** two identical states ticked to the
   review week produce identical strikes/goodwill/lines; `review_week` advanced
   exactly 12 after resolution.
3. **The stage ladder gates the strikes:** miss at garage → pressure only,
   strikes stay 0; miss ×2 at office → coach commitment named "the executive
   coach the board sent" with `cash_wk ∈ [−2500, −250]`; ×3 →
   `theta.funding_mult` shrank ×0.8, floored at 0.5; beat →
   `board_delight` installed and goodwill +1.
4. **Warmth reads the record:** same traits, goodwill 3 vs strikes 3 →
   `warmth_pct` differs per spec (+6 / −7.5, clamped [0,12]);
   `generate_offers` equity asks order accordingly.
5. **Lifeline + no-shop + cooldown:** `weeks_in_red = 2` → offer with
   `0.3 ≤ premium ≤ 0.5` and `expires_week == week + 2`; same seed twice =
   same buyer and price; let it lapse → morale fell, `mna` empty, and no new
   offer for the next 9 ticks with triggers held true.
6. **The window is weather:** hq state, traction 120, 2 rounds, strikes 0,
   `macro_season = "boom"` → `ipo_window` flag set; flip to `"winter"` → next
   tick clears it with a receipt line.

## 13. ENGINE-IMPROVEMENT SUGGESTIONS (≤5)

1. Persist the engine's Object-metas (`served_total`, `prev_revenue`,
   `fundraising_week`) — a reload silently resets the learning curve and
   growth readings.
2. A preference ledger (`liq_pref_total` = Σ raised) would let low exits pay
   common honestly (`payout = max(price − prefs, 0) × pct`) — the one realism
   gap this design consciously keeps (the finale law owns scoring today).
3. Investor-consent rights: at 2+ investor seats, a lifeline sale below money
   raised should need a board-consent beat (today the founder signs alone) —
   completes the governance lesson; deferred to keep the card flow one-tap-arm.
4. The garage signing flow flags `seed_raised` for `rounds_raised.size() <= 2`
   (`garage_view_screen.gd:2159`) — round 3 mislabels; derive the flag from the
   ladder name `apply_round` just appended.
5. Move the `weeks_in_red` increment from the garage screen into `weekly_tick`
   so headless tests (and the M&A lifeline trigger) see red streaks without a
   screen.

## 14. OPEN QUESTIONS (≤3)

1. **Lifeline exits and style bonuses:** a 0.4× fire-sale still earns finale
   multipliers like "SOLD AT THE TOP" when hype is high. Suppress style bonuses
   when `why == "lifeline"` (kicker says "THE SOFT LANDING"), or keep roguelike
   generosity? Recommend: keep the bonuses, swap the kicker line only.
2. **Secondary money in the hero number:** specced as un-multiplied add-on
   (`+ founder_banked` after style multipliers) because banked cash already
   left the casino. Confirm the owner wants it in the hero count-up rather than
   as a separate line under it.
3. **Rival spite:** should a lapsed `rival` offer feed that rival (+4 strength,
   "they'll build it themselves")? Couples into subsystem 3's action table —
   held for the rivals designer.
