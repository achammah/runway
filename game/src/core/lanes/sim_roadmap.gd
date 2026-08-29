class_name SimRoadmap
extends RefCounted
## LANE 07 — THE ROADMAP (bets, the ship roll, tech debt). Spec: docs/design/07-roadmap.md
##
## WHAT THIS DESK TEACHES, by name and with receipts: CAPACITY (one team, so
## many R&D-weeks a week), OPPORTUNITY COST (money spent on a bet ships no base
## quality), TECH-DEBT INTEREST (every point over 40 taxes throughput), LAUNCH
## RISK (the house dice decide the launch, and preparation moves the odds).
##
## THE ONE LAW: the engine owns every number. The LLM may dress a card with a
## name and a rung of an authored ladder; cost, DC, payoff and duration are
## tables in this file and nowhere else.
##
## THE SPINE CALLS, in tick order (docs/design/00-spine.md §1, HOOKS.md):
##   tick_pre   §7  — stalled READY bets roll themselves out; the hq maintenance
##                    tax bills. Ships land BEFORE §8 reads product.
##   tick_money §9  — the R&D block's own section: while a bet is committed the
##                    rnd budget buys WEEKS, not polish (the base drip is
##                    reversed here — see _route_rnd), the capacity pool splits
##                    across committed bets, and a finished bet goes READY.
##   tick_post      — the board refreshes its slots (salt 71).
## Outside the tick: directives() feeds the DM block, attention() feeds every
## bang, and the desk drives commit/uncommit/ship through the API below.
##
## SHIP IS A BUTTON (docs/design/DECISIONS.md #2): a READY bet waits for the
## founder's press at the product desk, where the dice roll AT the press behind
## the pre-roll review. Three weeks unpressed and it slips out on its own —
## the world does not hold a launch forever.

# ─────────────────────────── the era ladders (consts) ────────────────────────
## BOARD SLOTS = candidate cards on the board, hardening excluded (it is
## standing law, §3). THE SPINE'S ERA LADDER WINS over this spec's §2 table
## (docs/design/00-spine.md §9: "1 bet slot · 2 slots · 3 + hardening · ≤ · ≤"),
## and it agrees with the spec's own §8 layout — three cards is what the sheet
## holds above the standing row.
const BET_SLOTS := {"garage": 1, "coworking": 2, "office": 3, "floor": 3, "hq": 3}
## WIP = how many bets the team may build AT ONCE. The pool splits evenly across
## them, so two parallel bets finish later than the same two in series: the WIP
## lesson is arithmetic, never a scripted scolding.
const BET_WIP := {"garage": 1, "coworking": 1, "office": 2, "floor": 2, "hq": 3}
const ERA_AMBITION_CAP := {"garage": 2, "coworking": 3, "office": 3, "floor": 3, "hq": 3}

## THE PRICE AND THE ODDS, per rung of ambition. Authored, never inferred: the
## same card must cost the same in every run and in both engines.
const COST_BY_AMBITION := [3.0, 5.0, 8.0]     # R&D-weeks
const DC_BY_AMBITION := [8, 11, 14]
const HARDENING_COST := 2.5
const PLATFORM_COST := 10.0
const PLATFORM_DC := 12

## THE PAYOFF MATRIX — integers only, so a Godot round() and a C# banker's
## round can never disagree about what a launch earned.
## BET_PAYOFF[ambition − 1][band] · band index: brilliant 0, fine 1, risky 2, backfired 3
const BET_PAYOFF := [[6, 4, 2, 0], [11, 7, 4, 0], [15, 10, 5, 0]]
const BANDS := ["brilliant", "fine", "risky", "backfired"]
const KINDS := ["quality", "retention", "reach", "debt", "platform", "cost_down"]
## THE COST SPRINT (owner: the player never dials a cost — cutting one is a
## BUILD, a sprint with the team). 3% off the offer's variable cost per payoff
## unit, capped at 40% per sprint, floored at 20% of the fair price.
const COST_DOWN_PCT_PER_UNIT := 3.0
const COST_DOWN_CAP_PCT := 40.0
const COST_DOWN_FLOOR_OF_FAIR := 0.2

## $1,200 ≈ one loaded junior-engineer week in this economy — the constant the
## rnd lever already prices itself in, so the desk can speak in WEEKS.
const RND_PER_WEEK := 1200.0
const ENG_PER_SKILL := 0.25        # skill 1-5 → 0.25-1.25 wk/wk (the meetings tax is real)
const FOUNDER_HANDS_ON := 0.25     # garage + coworking only: the founder IS capacity
const PLATFORM_MULT := 0.15        # ×1.15 compounding per shipped platform level
const PLATFORM_MAX := 4
const DEBT_FREE := 40.0            # debt below this is free; above it charges interest
const DEBT_SPAN := 120.0           # linear to the floor
const DEBT_FLOOR := 0.5            # −50% velocity at debt 100
const STALL_WEEKS := 3             # READY and unpressed this long → it slips out
const ABANDON_DECAY := 0.25        # standing down costs a quarter of the build
const MAINTENANCE_WINDOW := 10     # hq: weeks of neglect before entropy bills
const MAINTENANCE_DEBT := 0.8
const QA_NET_ERA := 2              # office and up: staging truncates the tail
const QA_NET_MARGIN := -4          # a miss by 3-4 softens to risky, never better
const SHIPPED_KEPT := 8            # the last 8 launches stay as history
const SHIP_DRAWS := 3              # dice a single ship can pull from the stream
## One tick's worth of scratch: product as it stood before the engine's R&D
## block ran (−1 = nothing to reverse). Meta, never saved — it dies with the week.
const PRODUCT_PRE := "roadmap_product_pre"

## THE STANDING BET. Never drawn, never discarded, always exactly one on the
## board: the maintenance choice must always be one press away.
const HARDENING_ID := "hardening"
const HARDENING := {
	"name": "Hardening sprint",
	"desc": "No features. Pay the debt down before the debt collects you.",
	"kind": "debt", "ambition": 1,
}

## THE KEYLESS POOL (§11) — the COMPLETE path, not a fallback. A run with no
## model draws these; a run with one gets the same cards wearing this business's
## own words (dress_bets). Kinds spread 3 quality / 2 retention / 3 reach /
## 2 platform, ambitions 1-3, so every era's filter still has something to draw.
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

## The band phrase the DM must narrate a launch with — engine-owned words for an
## engine-owned outcome (§9). The narrator picks sentences, never the verdict.
const BAND_PHRASE := {
	"brilliant": "and the launch sang",
	"fine": "and it landed fine",
	"risky": "hot, with smoke coming out",
	"backfired": "and it faceplanted",
}

## THE DRESSING TRIGGER (§10): how many cards the last refresh drew. The engine
## never calls a model — it reports that fresh paper exists and the screen that
## owns the client decides. Reset on every refresh, so a stale count can never
## fire a second call.
static var last_refreshed := 0

# ═════════════════════════════ THE TICK HOOKS ════════════════════════════════

## §7 — before adoption reads product. Two things happen here and nothing else:
## a READY bet nobody pressed for three weeks rolls itself out, and (hq) the
## maintenance tax bills a portfolio that has shipped no upkeep in ten weeks.
static func tick_pre(state: GameState, rep: Dictionary) -> void:
	_ship_stalled(state, rep)
	_maintenance_tax(state, rep)
	# the snapshot the R&D branch is measured against (see _route_rnd): after
	# this line the engine's own R&D block is the ONLY writer of product, which
	# is what makes the reversal exact instead of a re-derivation.
	state.set_meta(PRODUCT_PRE, state.product)

## §9, the R&D block's own section — the spine's base drip has just run.
##
## THE ROUTING (the spec's DECIDE #1): output SPLITS, it never doubles. While a
## bet is committed the rnd money buys R&D-weeks, so the +1-quality-per-$1,200
## drip the engine just applied is reversed here, receipt and all. Uncommitted,
## nothing happens and the legacy path stands verbatim. Debt paydown belongs to
## the engine's block in BOTH branches — ambient hygiene, unchanged.
static func tick_money(state: GameState, rep: Dictionary, _m: Dictionary) -> void:
	var live := committed_bets(state)
	if live.is_empty():
		state.set_meta(PRODUCT_PRE, -1)
		return
	_route_rnd(state, rep)
	var pool := capacity_pool(state)
	var share := pool / float(live.size())
	for bet in live:
		var bd: Dictionary = bet
		bd["progress"] = float(bd.get("progress", 0.0)) + share
		var cost := float(bd.get("cost_rnd_weeks", 1.0))
		rep["lines"].append("roadmap: '%s' — %d%% built" % [String(bd.get("name", "")),
			int(minf(float(bd["progress"]) / maxf(cost, 0.001), 1.0) * 100.0)])
		if float(bd["progress"]) >= cost:
			bd["progress"] = cost
			bd["ready"] = true
			bd["committed"] = false
			# committed_week doubles as THE STALL CLOCK: the week the team last
			# touched this bet. It is the field that answers "how long has this
			# been sitting built and unshipped" without a new saved key.
			bd["committed_week"] = state.week
			rep["lines"].append("READY TO SHIP: '%s' — the dice are yours at the product desk"
				% String(bd.get("name", "")))
	# NO P&L LANE OF ITS OWN: `_m["rnd"]` is already on the record and the money
	# still leaves the bank. THAT is the opportunity cost — the same dollars,
	# a different output.

## After the week's record is written: the board refreshes its slots (salt 71).
## A bet that shipped this week has already freed its slot, so the refill lands
## in the same tick the launch did.
static func tick_post(state: GameState, rep: Dictionary) -> void:
	refresh_bets(state, rep)

## DM context lines, section 11 of the DIRECTIVES block (docs/design/00-spine.md
## §5). The launch becomes story through these — no new call, ever.
static func directives(state: GameState) -> Array[String]:
	var out: Array[String] = []
	for bet in ready_bets(state):
		out.append("- Bet ready to ship: '%s' (R&D done; shipping rolls the house dice)."
			% String((bet as Dictionary).get("name", "")))
		break
	for bet2 in state.bets:
		var bd: Dictionary = bet2
		if not bool(bd.get("shipped", false)):
			continue
		if int(bd.get("shipped_week", 0)) < state.week - 1:
			continue
		out.append("- SHIPPED: '%s' went out %s. The week's story must feel the launch."
			% [String(bd.get("name", "")),
			String(BAND_PHRASE.get(String(bd.get("band", "fine")), "and it landed fine"))])
	return out

## Attention rows — the product desk. Labels are ≤40 chars because the garage
## ticker prints them verbatim, and they name the business term, not the state.
static func attention(state: GameState) -> Array:
	var rows: Array = []
	if any_bet_ready(state):
		rows.append({"desk": "product", "key": "bet_ready", "severity": 2,
			"control": "ship", "label": "a bet is built — ship it"})
	if state.tech_debt >= 70.0:
		rows.append({"desk": "product", "key": "debt_critical", "severity": 2,
			"control": "rebuild",
			"label": "tech debt %d — everything builds slow" % int(state.tech_debt)})
	return rows

# ═══════════════════════════ THE BOARD (reads) ═══════════════════════════════

## Every bet still in play — the board plus anything committed or ready.
static func unshipped(state: GameState) -> Array:
	var out: Array = []
	for b in state.bets:
		if not bool((b as Dictionary).get("shipped", false)):
			out.append(b)
	return out

## The candidate cards, hardening excluded (it renders under its own rule).
static func board_bets(state: GameState) -> Array:
	var out: Array = []
	for b in unshipped(state):
		if String((b as Dictionary).get("id", "")) != HARDENING_ID:
			out.append(b)
	return out

static func hardening_bet(state: GameState) -> Dictionary:
	for b in unshipped(state):
		if String((b as Dictionary).get("id", "")) == HARDENING_ID:
			return b
	return {}

static func bet_by_id(state: GameState, id: String) -> Dictionary:
	for b in state.bets:
		if String((b as Dictionary).get("id", "")) == id:
			return b
	return {}

static func committed_bets(state: GameState) -> Array:
	var out: Array = []
	for b in unshipped(state):
		if bool((b as Dictionary).get("committed", false)):
			out.append(b)
	return out

static func ready_bets(state: GameState) -> Array:
	var out: Array = []
	for b in unshipped(state):
		if bool((b as Dictionary).get("ready", false)):
			out.append(b)
	return out

static func any_bet_ready(state: GameState) -> bool:
	return not ready_bets(state).is_empty()

static func shipped_bets(state: GameState) -> Array:
	var out: Array = []
	for b in state.bets:
		if bool((b as Dictionary).get("shipped", false)):
			out.append(b)
	return out

static func wip_cap(state: GameState) -> int:
	return int(BET_WIP.get(state.era, 1))

static func slots(state: GameState) -> int:
	return int(BET_SLOTS.get(state.era, 1))

static func ambition_cap(state: GameState) -> int:
	return int(ERA_AMBITION_CAP.get(state.era, 3))

## How long a READY bet has waited for the founder's press.
static func ready_age(state: GameState, bet: Dictionary) -> int:
	return maxi(state.week - int(bet.get("committed_week", state.week)), 0)

## Weeks before a READY bet slips out on its own (0 = it goes this tick).
static func stall_left(state: GameState, bet: Dictionary) -> int:
	return maxi(STALL_WEEKS - ready_age(state, bet), 0)

# ═══════════════════════ COMMIT — the allocation act ═════════════════════════

## Point the team at a bet. Refuses a ready or shipped card, and refuses at the
## WIP cap: the desk stands one down explicitly, because switching costs.
static func commit_bet(state: GameState, id: String) -> bool:
	var bet := bet_by_id(state, id)
	if bet.is_empty() or bool(bet.get("shipped", false)) or bool(bet.get("ready", false)):
		return false
	if bool(bet.get("committed", false)):
		return true
	if committed_bets(state).size() >= wip_cap(state):
		return false
	bet["committed"] = true
	bet["committed_week"] = state.week
	state.log_action("roadmap: pointed the team at '%s'" % String(bet.get("name", "")))
	return true

## THE PLAYER STARTS A COST SPRINT from the offers desk: a real bet on the
## roadmap — it eats R&D capacity like any build and ships on the same dice.
static func add_cost_down_bet(state: GameState, offer_name: String) -> Dictionary:
	for bv in state.bets:
		var bd: Dictionary = bv
		if String(bd.get("kind", "")) == "cost_down" \
				and String(bd.get("offer", "")) == offer_name \
				and not bool(bd.get("shipped", false)):
			return {"ok": false, "why": "a cost sprint for this offer is already on the board"}
	var bet := {
		"id": "costdown_%s_%d" % [offer_name.to_snake_case(), state.week],
		"name": "Cost sprint — %s" % offer_name,
		"desc": "The team rebuilds how one gets made and served. Cheaper, or nothing.",
		"kind": "cost_down", "ambition": 1, "offer": offer_name,
		"cost_rnd_weeks": COST_BY_AMBITION[0],
		"committed": false, "ready": false, "shipped": false, "progress": 0.0,
	}
	state.bets.append(bet)
	var went := commit_bet(state, String(bet["id"]))
	state.log_action("cost sprint opened on '%s'%s" % [offer_name,
		"" if went else " (parked — the team is full; commit it on the roadmap)"])
	return {"ok": true, "committed": went}

## Stand a bet down. The team carries a quarter of the build out the door with
## them (docs/design/DECISIONS.md — context-switching is priced, not free).
static func uncommit_bet(state: GameState, id: String) -> bool:
	var bet := bet_by_id(state, id)
	if bet.is_empty() or not bool(bet.get("committed", false)):
		return false
	bet["committed"] = false
	bet["progress"] = maxf(float(bet.get("progress", 0.0)) * (1.0 - ABANDON_DECAY), 0.0)
	state.log_action("roadmap: stood down '%s' — a quarter of the build went with it"
		% String(bet.get("name", "")))
	return true

# ═══════════════════ CAPACITY — one team, priced honestly ════════════════════

## THE WEEKLY CAPACITY POOL, in R&D-weeks. Every term is a real one:
##   money      the rnd lever at $1,200 the loaded week
##   engineers  0.25 × skill each — sub-1.0 is the honest meetings/review tax
##   founder    +0.25 while the founder still builds (garage, coworking)
##   vel        the STATUS catalog's velocity_mult (crunch, burnt_out, flow)
##   drag       TECH-DEBT INTEREST: every point over 40 taxes throughput
##   plat       ×1.15 per shipped platform level — infrastructure compounds
static func capacity_pool(state: GameState) -> float:
	var money := float(state.budgets.get("rnd", 0)) / RND_PER_WEEK
	var eng := 0.0
	for e in state.employees:
		if String((e as Dictionary).get("role", "")).contains("engineer"):
			eng += ENG_PER_SKILL * float(clampi(int((e as Dictionary).get("skill", 3)), 1, 5))
	var founder := 0.0
	if state.era_index() <= 1 and not committed_bets(state).is_empty():
		founder = FOUNDER_HANDS_ON
	return (money + eng + founder) * velocity_mult(state) * debt_drag(state) * platform_mult(state)

## The first consumer of the STATUS catalog's velocity_mult, dormant since it
## was authored: crunch 1.35, burnt_out 0.6, founder_flow 1.15 finally bite.
static func velocity_mult(state: GameState) -> float:
	var v := 1.0
	for s in state.statuses:
		var eff: Dictionary = SimEngine.STATUS.get(String((s as Dictionary).get("name", "")), {})
		v *= float(eff.get("velocity_mult", 1.0))
	return v

## TECH-DEBT INTEREST (Cunningham): linear from 1.0 at debt 40 to 0.5 at 100.
static func debt_drag(state: GameState) -> float:
	return clampf(1.0 - maxf(state.tech_debt - DEBT_FREE, 0.0) / DEBT_SPAN, DEBT_FLOOR, 1.0)

static func platform_mult(state: GameState) -> float:
	return 1.0 + PLATFORM_MULT * float(state.platform_level)

## What one committed bet gets this week (the pool splits evenly — that is the
## whole WIP lesson, in one division).
static func weekly_share(state: GameState) -> float:
	var n := committed_bets(state).size()
	if n <= 0:
		return capacity_pool(state)
	return capacity_pool(state) / float(n)

static func progress_pct(bet: Dictionary) -> int:
	var cost := maxf(float(bet.get("cost_rnd_weeks", 1.0)), 0.001)
	return int(clampf(float(bet.get("progress", 0.0)) / cost, 0.0, 1.0) * 100.0)

## Honest ETA in weeks at THIS week's settings, or −1 when the current spend
## would never finish it (the desk says so in words).
static func eta_weeks(state: GameState, bet: Dictionary) -> int:
	var left := float(bet.get("cost_rnd_weeks", 0.0)) - float(bet.get("progress", 0.0))
	if left <= 0.0:
		return 0
	# an uncommitted card quotes the ETA it would have IF the team took it on —
	# the number the founder is actually deciding about
	var n := committed_bets(state).size()
	var share := capacity_pool(state) / float(maxi(n if bool(bet.get("committed", false)) else n + 1, 1))
	if share <= 0.001:
		return -1
	return int(ceil(left / share))

# ═══════════════════════ THE SHIP ROLL (salt 70) ═════════════════════════════

## The odds the desk prints before the press — the same numbers the dice will
## face, minus luck (luck is felt, never advertised).
static func ship_odds_pct(state: GameState, bet: Dictionary) -> int:
	var dc := bet_dc(bet)
	var mod := int(state.competences.get("build", 3)) - 3
	var need := clampi(dc - mod, 2, 20)
	var p := float(21 - need) / 20.0
	var ctx := SimEngine.roll_context(state, "build")
	if bool(ctx.advantage):
		p = 1.0 - (1.0 - p) * (1.0 - p)
	elif bool(ctx.disadvantage):
		p = p * p
	return int(round(p * 100.0))

static func bet_dc(bet: Dictionary) -> int:
	if String(bet.get("kind", "")) == "platform":
		return PLATFORM_DC
	return int(DC_BY_AMBITION[clampi(int(bet.get("ambition", 1)), 1, 3) - 1])

static func bet_cost(kind: String, ambition: int, id: String = "") -> float:
	if id == HARDENING_ID:
		return HARDENING_COST
	if kind == "platform":
		return PLATFORM_COST
	return float(COST_BY_AMBITION[clampi(ambition, 1, 3) - 1])

## THE LAUNCH, resolved by the house dice. UI-agnostic on purpose: the desk's
## SHIP button, the three-week slip and the twin suites all come through here
## with their own roller, so the ceremony can never disagree with the test.
##
## Returns the receipt: {band, d20, mod, dc, total, units, event, lines}.
static func ship_bet(state: GameState, bet: Dictionary, roller: Callable) -> Dictionary:
	var ctx := SimEngine.roll_d20_ctx(state, "build", roller)
	var dc := bet_dc(bet)
	var band := SimEngine.margin_band(int(ctx.total), dc)
	var qa := false
	if state.era_index() >= QA_NET_ERA and band == "backfired" \
			and int(ctx.total) - dc >= QA_NET_MARGIN:
		# THE QA NET: staging and review truncate the tail. They never raise the
		# ceiling — process reduces variance, not mean.
		band = "risky"
		qa = true
	var lines: Array[String] = []
	var units := _apply_payoff(state, bet, band, lines)
	_apply_band(state, bet, band, lines)
	if qa:
		lines.append("  → the QA net caught the worst of it")
	bet["shipped"] = true
	bet["ready"] = false
	bet["committed"] = false
	bet["shipped_week"] = state.week
	bet["band"] = band
	# W2 L-MAKE seam: the landing joins the wall in the same beat as the dice
	SimFeatures.on_bet_landed(state, bet, {})
	state.clampi_meters()
	var event := "SHIPPED %s: '%s' — d20 %d%+d vs DC %d" % [band.to_upper(),
		String(bet.get("name", "")), int(ctx.d20), int(ctx.mod), dc]
	if band == "backfired" and bool(ctx.disadvantage):
		# THE BURN ALWAYS EXPLAINS ITSELF (§13): the reason the die was loaded
		# rides the receipt, so a bad week is a lesson and not a mood.
		event += " (disadvantage: %s)" % ", ".join(ctx.dis_reasons)
	return {"band": band, "d20": int(ctx.d20), "mod": int(ctx.mod), "dc": dc,
		"total": int(ctx.total), "units": units, "qa_net": qa,
		"event": event, "lines": lines}

## THE PRESS (docs/design/DECISIONS.md #2): the desk's SHIP button lands here
## and the house dice pour immediately. The stream is salt 70, keyed to the
## week, and steps forward once per launch already resolved this week, so two
## launches in one week never roll the same die twice.
static func ship_ready(state: GameState, id: String) -> Dictionary:
	var bet := bet_by_id(state, id)
	if bet.is_empty() or not bool(bet.get("ready", false)) or bool(bet.get("shipped", false)):
		return {}
	var res := ship_bet(state, bet, house_roller(state))
	state.log_action("roadmap: shipped '%s' — %s (d20 %d%+d vs DC %d)" % [
		String(bet.get("name", "")), String(res.get("band", "")),
		int(res.get("d20", 0)), int(res.get("mod", 0)), int(res.get("dc", 0))])
	return res

## The house dice for a launch this week. Deterministic per (seed, week): the
## draws already spent by this week's launches are stepped over first.
static func house_roller(state: GameState) -> Callable:
	var r := SimEngine.rng_for(state, SimEngine.SALT_ROADMAP_SHIP)
	var done := 0
	for b in state.bets:
		var bd: Dictionary = b
		if bool(bd.get("shipped", false)) and int(bd.get("shipped_week", 0)) == state.week:
			done += 1
	for i in done * SHIP_DRAWS:
		r.randi_range(1, 20)
	return func() -> int:
		return r.randi_range(1, 20)

## The integer payoff, by kind. Every magnitude is a table lookup; a status
## carries its own multiplier from the catalog and ambition buys WEEKS of it,
## never a bigger number (the one-typed-catalog law).
static func _apply_payoff(state: GameState, bet: Dictionary, band: String,
		lines: Array[String]) -> int:
	var amb := clampi(int(bet.get("ambition", 1)), 1, 3)
	var bi := maxi(BANDS.find(band), 0)
	var units := int(BET_PAYOFF[amb - 1][bi])
	match String(bet.get("kind", "")):
		"quality":
			if units > 0:
				state.product = mini(state.product + units, 100)
				lines.append("  → product v0.%d (+%d quality)" % [state.product, units])
		"retention":
			if units > 0:
				SimEngine.add_status(state, "sticky_release", units)
				lines.append("  → customers stick: churn −25%% for %d wks" % units)
		"reach":
			if units > 0:
				SimEngine.add_status(state, "feature_buzz", units)
				lines.append("  → word gets out: adoption ×1.3 for %d wks" % units)
				if String(state.biz_who) == "Enterprise":
					# 05-pipeline reads gtm_cap_bonus() — a buzz the salespeople
					# can actually carry (docs/design/DECISIONS.md, roadmap).
					lines.append("  → and the room takes more meetings: +2 GTM capacity while it lasts")
		"debt":
			if units > 0:
				state.tech_debt = maxf(state.tech_debt - float(units * 3), 0.0)
				lines.append("  → the codebase breathes: debt −%d" % (units * 3))
		"platform":
			if units > 0:
				state.platform_level = mini(state.platform_level + 1, PLATFORM_MAX)
				lines.append("  → the platform compounds: all builds ×%.2f from here"
					% platform_mult(state))
		"cost_down":
			if units > 0:
				var oname := String(bet.get("offer", ""))
				for ov in state.offers:
					var od: Dictionary = ov
					if String(od.get("name", "")) != oname:
						continue
					var cut := minf(float(units) * COST_DOWN_PCT_PER_UNIT, COST_DOWN_CAP_PCT)
					var floor_c := maxf(float(od.get("fair_price", 1.0))
						* COST_DOWN_FLOOR_OF_FAIR, 0.0)
					var before := float(od.get("unit_cost", 0.0))
					od["unit_cost"] = maxf(before * (1.0 - cut / 100.0), floor_c)
					lines.append("  → '%s' serves cheaper: $%d -> $%d/unit (−%d%%)" % [
						oname, int(round(before)), int(round(float(od["unit_cost"]))),
						int(round(cut))])
					break
	return units

## What the launch did to the room, and to the codebase. A refactor is never
## punished with debt — that would be absurd.
static func _apply_band(state: GameState, bet: Dictionary, band: String,
		lines: Array[String]) -> void:
	var kind := String(bet.get("kind", ""))
	var gentle := kind == "debt" or kind == "platform"
	match band:
		"brilliant":
			state.hype = clampi(state.hype + 8, 0, 100)
		"fine":
			state.hype = clampi(state.hype + 3, 0, 100)
		"risky":
			var pen := 0.0
			if kind == "platform":
				pen = 10.0
			elif kind != "debt":
				pen = 6.0
			if pen > 0.0:
				state.tech_debt = clampf(state.tech_debt + pen, 0.0, 100.0)
				lines.append("  → shipped hot: debt +%d" % int(pen))
		"backfired":
			var dpen := 6.0 if gentle else 12.0
			state.tech_debt = clampf(state.tech_debt + dpen, 0.0, 100.0)
			state.morale = clampi(state.morale - 6, 0, 100)
			lines.append("  → nothing shipped worth keeping: debt +%d, the room deflates"
				% int(dpen))

## THE THREE-WEEK SLIP (docs/design/DECISIONS.md #2): a launch nobody presses
## goes out anyway. The world does not wait forever, and the receipt says why.
static func _ship_stalled(state: GameState, rep: Dictionary) -> void:
	for bet in ready_bets(state):
		var bd: Dictionary = bet
		if ready_age(state, bd) < STALL_WEEKS:
			continue
		var res := ship_bet(state, bd, house_roller(state))
		rep["events"].append(String(res.get("event", "")))
		rep["lines"].append("nobody pressed ship for %d weeks — '%s' slipped out on its own"
			% [STALL_WEEKS, String(bd.get("name", ""))])
		var receipts: Array = res.get("lines", [])
		for l in receipts:
			rep["lines"].append(String(l))
		state.log_action("roadmap: '%s' slipped out on its own (%s)"
			% [String(bd.get("name", "")), String(res.get("band", ""))])

## THE HQ MAINTENANCE TAX (§2): a big org pays a standing maintenance share or
## it rots. Ten weeks with no upkeep shipped and nothing committed bills 0.8
## debt a week, with the reason attached.
static func _maintenance_tax(state: GameState, rep: Dictionary) -> void:
	if state.era != "hq":
		return
	for b in state.bets:
		var bd: Dictionary = b
		var kind := String(bd.get("kind", ""))
		if kind != "debt" and kind != "platform":
			continue
		if bool(bd.get("committed", false)) or bool(bd.get("ready", false)):
			return
		if bool(bd.get("shipped", false)) \
				and int(bd.get("shipped_week", 0)) > state.week - MAINTENANCE_WINDOW:
			return
	state.tech_debt = clampf(state.tech_debt + MAINTENANCE_DEBT, 0.0, 100.0)
	rep["lines"].append("organizational entropy: debt +%.1f (no maintenance shipped in %d wks)"
		% [MAINTENANCE_DEBT, MAINTENANCE_WINDOW])

## OPPORTUNITY COST, made real. The spine's R&D block has already turned this
## week's rnd money into base quality; while a bet is committed that money was
## spent on WEEKS instead, so the drip is handed back — the product number and
## the receipt line both. One team, one throughput.
static func _route_rnd(state: GameState, rep: Dictionary) -> void:
	var p0 := int(state.get_meta(PRODUCT_PRE, -1))
	state.set_meta(PRODUCT_PRE, -1)
	if p0 < 0 or state.product <= p0:
		return
	state.product = p0
	var lines: Array = rep.get("lines", [])
	for i in range(lines.size() - 1, -1, -1):
		if String(lines[i]).begins_with("R&D shipped: product v0."):
			lines.remove_at(i)
			break

# ═════════════════════ THE BOARD REFRESH (salt 71) ═══════════════════════════

## Idempotent, every tick: the standing bet exists, stale candidates go, open
## slots refill from the pool. Committed work survives an era change — losing
## paid work teaches nothing but resentment.
static func refresh_bets(state: GameState, rep: Dictionary) -> void:
	last_refreshed = 0
	# 1 ── the standing law
	if hardening_bet(state).is_empty():
		var h := HARDENING.duplicate()
		h["id"] = HARDENING_ID
		h["cost_rnd_weeks"] = HARDENING_COST
		h["progress"] = 0.0
		h["committed"] = false
		h["committed_week"] = 0
		h["ready"] = false
		h["shipped"] = false
		h["shipped_week"] = 0
		h["band"] = ""
		h["era"] = state.era
		state.bets.append(h)
	# 2 ── the era refresh: a stage change resets the roadmap's candidates
	var kept: Array = []
	for b in state.bets:
		var bd: Dictionary = b
		if bool(bd.get("shipped", false)) or bool(bd.get("committed", false)) \
				or bool(bd.get("ready", false)) \
				or String(bd.get("id", "")) == HARDENING_ID \
				or String(bd.get("era", "")) == state.era:
			kept.append(bd)
	state.bets = kept
	# 3 ── refill what the era allows
	var open_slots := slots(state) - board_bets(state).size()
	var drawn := 0
	if open_slots > 0:
		var r := SimEngine.rng_for(state, SimEngine.SALT_ROADMAP_SLOTS)
		var recent: Array = []
		for s in shipped_bets(state):
			recent.append(String((s as Dictionary).get("name", "")))
		for n in range(open_slots):
			var eligible := _eligible(state, recent)
			if eligible.is_empty():
				# exclusion (b) drops FIRST — a board with nothing on it teaches
				# nothing; the era gates (c) and (d) never drop.
				eligible = _eligible(state, [])
			if eligible.is_empty():
				break
			var pick: Dictionary = BET_POOL[int(eligible[r.randi_range(0, eligible.size() - 1)])]
			state.bets.append(_make_bet(state, pick, n + 1))
			drawn += 1
	# 4 ── history: the last eight launches stay, the rest fall off the board
	var drop := shipped_bets(state).size() - SHIPPED_KEPT
	if drop > 0:
		var out: Array = []
		for b2 in state.bets:
			var bd2: Dictionary = b2
			if bool(bd2.get("shipped", false)) and drop > 0:
				drop -= 1     # the array is in launch order: the oldest go first
				continue
			out.append(bd2)
		state.bets = out
	if drawn > 0:
		last_refreshed = drawn
		rep["bets_refreshed"] = drawn
		if rep.has("lines"):
			rep["lines"].append("%d new bets on the roadmap board" % drawn)

## The board with paper on it from the first open, even before the first tick.
## Deterministic (salt 71 keyed to the week) and idempotent — it only fires
## when there is nothing to look at.
static func ensure_board(state: GameState) -> void:
	if unshipped(state).is_empty():
		refresh_bets(state, {})

## Pool indices this era may draw: nothing already on the board, nothing in the
## last eight launches, ambition capped in the garage, platform work only once
## there is a floor to put it on.
static func _eligible(state: GameState, recent: Array) -> Array:
	var on_board: Array = []
	for b in unshipped(state):
		on_board.append(String((b as Dictionary).get("name", "")))
	var out: Array = []
	for i in BET_POOL.size():
		var c: Dictionary = BET_POOL[i]
		var nm := String(c.get("name", ""))
		if on_board.has(nm) or recent.has(nm):
			continue
		if int(c.get("ambition", 1)) > ambition_cap(state):
			continue
		if String(c.get("kind", "")) == "platform" and state.era_index() < 3:
			continue
		out.append(i)
	return out

static func _make_bet(state: GameState, card: Dictionary, n: int) -> Dictionary:
	var kind := String(card.get("kind", "quality"))
	var amb := clampi(int(card.get("ambition", 1)), 1, ambition_cap(state))
	return {
		"id": "bet_w%d_%d" % [state.week, n],
		"name": String(card.get("name", "")).substr(0, 28),
		"desc": String(card.get("desc", "")).substr(0, 90),
		"kind": kind, "ambition": amb,
		"cost_rnd_weeks": bet_cost(kind, amb),
		"progress": 0.0, "committed": false, "committed_week": 0,
		"ready": false, "shipped": false, "shipped_week": 0,
		"band": "", "era": state.era,
	}

# ═══════════════════ THE DRESSING SEAM (LLM value point A) ═══════════════════

## What the one batch dressing call is told (§10). Empty = nothing to dress, so
## the caller never fires. The model sees the business and the board; it never
## sees a cost, a DC or a payoff, because those are not its business.
static func dressing_payload(state: GameState) -> Dictionary:
	var targets := dressable(state)
	if targets.is_empty():
		return {}
	var board: Array = []
	for b in unshipped(state):
		board.append(String((b as Dictionary).get("name", "")))
	var shipped: Array = []
	for s in shipped_bets(state):
		shipped.append(String((s as Dictionary).get("name", "")))
	return {"company": {"name": state.company_name, "idea": state.company_idea,
			"what": state.biz_what, "who": state.biz_who},
		"era": state.era_display_name(), "board": board,
		"recently_shipped": shipped, "slots": targets.size()}

## The one place a model may touch the roadmap: a fresh candidate's WORDS and
## its rung of an authored ladder. Everything with a number attached is
## re-priced from the tables below, so a slow or hostile reply can only ever
## change what a card is CALLED.
##
## `cards` = [{name, desc, kind, ambition}] (LlmClient.BETS_SCHEMA shape).
## Returns how many candidates were dressed. Committed or started work is
## untouchable: a reply that arrives late can never repaint work in flight.
static func dress_bets(state: GameState, cards: Array) -> int:
	var targets := dressable(state)
	var done := 0
	for c in cards:
		if not (c is Dictionary):
			continue
		if done >= targets.size():
			break
		var card: Dictionary = c
		var target: Dictionary = targets[done]
		var kind := String(card.get("kind", ""))
		if not KINDS.has(kind):
			kind = String(target.get("kind", "quality"))   # off-enum: keep the authored card
		if kind == "platform" and state.era_index() < 3:
			kind = "quality"
		var amb := clampi(int(card.get("ambition", 1)), 1, ambition_cap(state))
		var nm := String(card.get("name", "")).substr(0, 28)
		var ds := String(card.get("desc", "")).substr(0, 90)
		if nm.strip_edges() == "":
			continue
		target["name"] = nm
		target["desc"] = ds
		target["kind"] = kind
		target["ambition"] = amb
		target["cost_rnd_weeks"] = bet_cost(kind, amb, String(target.get("id", "")))
		done += 1
	return done

## Legacy name from the spec — the same ingestion, one door.
static func apply_bet_dressing(state: GameState, cards: Array) -> int:
	return dress_bets(state, cards)

## The candidates a reply may repaint, in board order: untouched paper only.
static func dressable(state: GameState) -> Array:
	var out: Array = []
	for b in board_bets(state):
		var bd: Dictionary = b
		if float(bd.get("progress", 0.0)) == 0.0 and not bool(bd.get("committed", false)) \
				and not bool(bd.get("ready", false)):
			out.append(bd)
	return out

# ═════════════════════ COORDINATION — what other lanes read ══════════════════

## THE COACH'S ONE LINE about this desk (07 INTERFACE DELTA). Empty until there
## is a board to point at; the garage owns the one-timer, so this only ever
## answers "is there something worth saying, and what is it".
static func coach_chip(state: GameState) -> Dictionary:
	if unshipped(state).is_empty():
		return {}
	return {"id": "roadmap_live",
		"text": "the roadmap is live — point the team at a bet, or the R&D money just polishes what exists."}

## ENTERPRISE REACH (docs/design/DECISIONS.md, roadmap): a reach launch on an
## Enterprise run also buys the salespeople two more meetings a week for as
## long as the buzz lasts. Derived from live state — no new saved field, and it
## expires exactly when `feature_buzz` does. 04/05 add this term to `gtm_cap`.
static func gtm_cap_bonus(state: GameState) -> float:
	if String(state.biz_who) != "Enterprise":
		return 0.0
	return 2.0 if SimEngine.has_status(state, "feature_buzz") else 0.0
