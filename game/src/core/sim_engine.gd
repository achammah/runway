class_name SimEngine
extends RefCounted
## THE DETERMINISTIC BUSINESS ENGINE — docs/DND_STARTUP_PLAN.md Pillar A.
##
## The one law, from every serious repo studied: the ENGINE owns every number,
## the DM owns every sentence, a narrow typed schema is the only bridge. This
## file is the world that grinds the company down by default — burn, churn,
## fatigue, debt, rivals — so that doing nothing is slow death and the weekly
## written move is how the founder pushes back.
##
## Formula provenance (see the plan for the full research):
##   Bass adoption + churn-by-quality      — BSL system-dynamics library
##   elasticity / CAC / funding math       — Ventiqra
##   buff slots / staffing balance          — TeamDay business-tycoon
##   burnout cliff / resignation roll       — Ventiqra morale module
##   market demography                      — opendnd Dominia
##
## Everything externally settable passes a clamp. Every stochastic subsystem
## rolls on its own salted stream keyed (seed, week), so a run replays exactly.

# ───────────────────────────── THETA: the world constants ────────────────────
## Generated once per run from the pitch (LLM emits INTUITIVE quantities, we
## convert and clamp). These defaults are the keyless world.
const THETA_CLAMPS := {
	"tam": [2_000.0, 5_000_000.0],          # buyers in the whole market
	"adopt_p": [0.00005, 0.004],            # weekly independent adoption fraction
	"adopt_ic": [0.05, 0.9],                # word-of-mouth contact*conversion
	"lifetime_wk": [6.0, 200.0],            # customer residence at product=50
	"arpu_wk": [0.5, 5_000.0],              # $ per customer per week at price 1.0
	"cac_sat": [500.0, 100_000.0],          # marketing saturation $ per week
	"rival_strength": [5.0, 60.0],          # starting rival power
	"trend_vol": [0.005, 0.05],             # market mood volatility per week
	"burn_mult": [0.6, 1.8],                # difficulty
	"churn_mult": [0.5, 1.8],
	"funding_mult": [0.5, 1.5],
}

static func default_theta(what: String, who: String) -> Dictionary:
	var t := {
		"tam": 120_000.0, "adopt_p": 0.00025, "adopt_ic": 0.06,
		"lifetime_wk": 40.0, "arpu_wk": 5.0, "cac_sat": 8_000.0,
		"rival_strength": 20.0, "trend_vol": 0.02,
		"burn_mult": 1.0, "churn_mult": 1.0, "funding_mult": 1.0,
	}
	match who:
		"Enterprise":
			t.tam = 4_000.0; t.arpu_wk = 400.0; t.adopt_p = 0.00018
			t.adopt_ic = 0.02; t.lifetime_wk = 90.0
		"SMB":
			t.tam = 60_000.0; t.arpu_wk = 14.0; t.lifetime_wk = 50.0
		"Consumer":
			t.tam = 900_000.0; t.arpu_wk = 0.9; t.adopt_ic = 0.15
			t.lifetime_wk = 22.0
	match what:
		"Hardware":
			t.arpu_wk *= 2.2; t.adopt_p *= 0.6; t.lifetime_wk *= 1.4
		"Marketplace":
			t.adopt_ic *= 1.3; t.arpu_wk *= 0.5
		"Service":
			t.adopt_p *= 1.5; t.arpu_wk *= 1.8; t.tam *= 0.3
	return clamp_theta(t)

static func clamp_theta(t: Dictionary) -> Dictionary:
	var out := t.duplicate()
	for k in THETA_CLAMPS:
		var c: Array = THETA_CLAMPS[k]
		out[k] = clampf(float(out.get(k, (float(c[0]) + float(c[1])) * 0.5)),
				float(c[0]), float(c[1]))
	return out

# ───────────────────────────── the status catalog ────────────────────────────
## Conditions and buffs are ONE typed catalog: the DM (or the engine itself)
## installs a status BY NAME with a duration; the magnitudes live HERE, so the
## LLM can never invent an untyped modifier. `adv`/`dis` grant advantage or
## disadvantage on the named stat while active — state drives the dice.
const STATUS := {
	"press_surge":      {"adopt_mult": 1.6, "hype_wk": 4.0, "kind": "buff"},
	"press_darling":    {"adopt_mult": 1.25, "adv": "sell", "kind": "buff"},
	"word_of_mouth":    {"adopt_mult": 1.35, "kind": "buff"},
	"viral_moment":     {"adopt_mult": 2.2, "kind": "buff"},
	"enterprise_pilot": {"arpu_mult": 1.3, "kind": "buff"},
	"crunch":           {"velocity_mult": 1.35, "fatigue_wk": 9.0, "kind": "buff"},
	"investor_pressure":{"morale_wk": -2.0, "dis": "raise", "kind": "condition"},
	"burnt_out":        {"velocity_mult": 0.6, "dis": "grit", "kind": "condition"},
	"press_backlash":   {"adopt_mult": 0.6, "hype_wk": -6.0, "kind": "condition"},
	"outage_fallout":   {"churn_mult": 1.6, "dis": "sell", "kind": "condition"},
	"churn_spiral":     {"churn_mult": 1.4, "kind": "condition"},
	"lawsuit_cloud":    {"dis": "raise", "morale_wk": -1.0, "kind": "condition"},
	"talent_magnet":    {"adv": "recruit", "kind": "buff"},
	"data_room_ready":  {"adv": "raise", "kind": "buff"},
	"founder_flow":     {"adv": "build", "velocity_mult": 1.15, "kind": "buff"},
	"market_tailwind":  {"adopt_mult": 1.3, "kind": "buff"},
	"market_headwind":  {"adopt_mult": 0.7, "kind": "condition"},
	"rival_fud":        {"adopt_mult": 0.8, "dis": "sell", "kind": "condition"},
	# ── THE WAVE'S ADDITIONS (docs/design/00-spine.md §7). Names are unique
	# across the catalog and across lanes, checked. The DM may install any of
	# them BY NAME through the existing `status` op — the magnitudes live HERE,
	# once, so a narrator can never invent an untyped modifier.
	#
	# NOTE the four NEW effect keys (fair_mult, val_mult, amt_mult, spread_mult)
	# are read ONLY by the helpers their owning specs define. The existing
	# section-8 status loop (adopt/churn/arpu) never sees them, so a price war
	# cannot accidentally double-dip on adoption.
	# 03 — the street
	"price_war":        {"fair_mult": 0.92, "kind": "condition"},
	"outshipped":       {"adopt_mult": 0.85, "kind": "condition"},
	"rival_stumbled":   {"adopt_mult": 1.25, "kind": "buff"},
	"winter_watch":     {"kind": "condition"},   # banner + DM only: the pre-announcement
	"boom_watch":       {"kind": "buff"},        # ditto, the other way
	"funding_winter":   {"val_mult": 0.6, "amt_mult": 0.7, "spread_mult": 1.25,
						 "dis": "raise", "kind": "condition"},
	"boom":             {"val_mult": 1.3, "amt_mult": 1.3, "spread_mult": 0.9,
						 "adv": "raise", "kind": "buff"},
	# 06 — the bank
	"collections_calls":{"morale_wk": -1.0, "dis": "raise", "kind": "condition"},
	# 07 — the roadmap
	"sticky_release":   {"churn_mult": 0.75, "kind": "buff"},
	"feature_buzz":     {"adopt_mult": 1.3, "kind": "buff"},
	# 08 — the board
	"board_delight":    {"adv": "raise", "morale_wk": 2.0, "hype_wk": 3.0, "kind": "buff"},
}

# ─────────────────────── seeded per-subsystem randomness ─────────────────────
## THE SALT REGISTRY (docs/design/00-spine.md §3). Every stochastic subsystem
## draws on its own stream keyed (seed, week, salt), so a run replays exactly
## and one subsystem's dice never shift another's.
##
## THE CONVENTION: salt = business-plan section × 10 + n (1 catalog · 2 labor ·
## 3 rivals · 4 funnel · 5 enterprise · 6 finance · 7 roadmap · 8 macro ·
## 9 board · 10 M&A · 11 hardware), skipping the frozen legacy numbers that
## already sit inside a decade. A frozen salt NEVER changes meaning — replay
## and save compatibility both depend on it. Nothing references a bare number:
## a lane cites the NAME, which is what makes a collision impossible to write.
const SALT_MORALE_QUIT := 4          # frozen — morale resignation roll
const SALT_OUTAGE := 5               # frozen — outage roll
const SALT_RIVAL_RATCHET := 6        # RETIRED — the old strength ratchet; a
                                     # tombstone, never reassigned (03 replaces
                                     # it with the weekly action table)
const SALT_TREND := 7                # frozen — the market-mood walk; 03-macro
                                     # mean-reverts it STREAM-PRESERVED
const SALT_TERM_SHEETS := 9          # frozen — term-sheet generation
const SALT_CATALOG_JITTER := 11      # 01 — keyless draft jitter
const SALT_LABOR_ARRIVALS := 20      # 02 — candidate arrivals (per open role)
const SALT_LABOR_STATS := 21         # 02 — candidate skill/ask, creation order
const SALT_LABOR_PATIENCE := 22      # 02 — applicant patience decay
const SALT_LABOR_LADDER := 23        # 02 — raise-ask / resignation ladder
const SALT_LABOR_POOLS := 24         # 02 — keyless name/quirk pools
const SALT_RIVAL_ACTION := 30        # 03 — weekly action pick (per rival)
const SALT_RIVAL_POACH := 31         # 03 — poach roll
const SALT_RIVAL_DISRUPTOR := 32     # 03 — hq disruptor spawn
const SALT_PIPELINE := 50            # 05 — THE pipeline stream, fixed draw order
const SALT_PIPELINE_NAMES := 51      # 05 — keyless lead-name pool
const SALT_ROADMAP_SHIP := 70        # 07 — ship roll payoff spread
const SALT_ROADMAP_SLOTS := 71       # 07 — slot refresh
const SALT_RND_REMAINDER := 77       # frozen — R&D quality seeded remainder
const SALT_MACRO_SHOCK := 80         # 03-macro — shock roll
const SALT_BELIEFS := 88             # frozen — belief seeding
const SALT_ADOPT_REMAINDER := 91     # frozen — adoption net seeded remainder
const SALT_INCIDENTS := 93           # frozen — incidents + standing liabilities
const SALT_BURNED := 95              # BURNED — four lanes claimed it at once;
                                     # permanently reserved so any stale 95
                                     # fails review on sight. Never draw on it.
const SALT_MNA := 100                # 08 — M&A offer arrival + premium rolls
const SALT_HW_BREAKDOWN := 110       # 09 — machine breakdown roll
const SALT_HW_REPURCHASE := 111      # 09 — repurchase seeded remainder

static func _rng(state: GameState, salt: int) -> RandomNumberGenerator:
	var r := RandomNumberGenerator.new()
	r.seed = hash(str(state.sim_seed) + ":" + str(state.week) + ":" + str(salt))
	return r

## Public stream accessor — the lanes cannot reach `_rng`, and every lane needs
## its own salted stream. Same keying, same guarantee.
static func rng_for(state: GameState, salt: int) -> RandomNumberGenerator:
	return _rng(state, salt)

# ───────────────────────────── lookup curves (BSL) ───────────────────────────
## Janoschek falling curve: 1.0 at x=0 down to `floor_v` as x→∞, knee at x_ref.
static func jano_down(x: float, x_ref: float, floor_v: float = 0.25) -> float:
	if x <= 0.0:
		return 1.0
	var k := 0.6931 / maxf(x_ref, 0.001)     # ln2: halfway to floor at x_ref
	return floor_v + (1.0 - floor_v) * exp(-k * x)

# ═══════════════════════════ THE WEEKLY TICK ═══════════════════════════
## The hostile world, in order (THE TICK ORDER v2, docs/design/00-spine.md §1).
## Returns the week's REPORT: every delta with its why, so the journal can print
## receipts the DM never invented.
##
## THE LANE HOOK MAP — the only insertion points. Each subsystem is ONE section
## with its number in a comment; a lane fills its own file and never touches
## this one (docs/design/HOOKS.md):
##
##   §3b  SimLabor.tick_pre     roster + applicants settle before morale reads them
##   §6a  SimStreet.tick_pre    rivals act, then macro — both before the market
##   §7   SimRoadmap.tick_pre   a shipped bet must exist before adoption reads product
##   §7h  SimFactory.tick_pre   produce FIRST: stock exists before adoption spends it
##   §8   SimCatalog / SimFunnel / SimPipeline.tick_pre   then the market moves once
##   §9   SimBank / SimBoard.tick_pre, then ALL NINE .tick_money(state, rep, m)
##   §9c+ ALL NINE .tick_post — board review and M&A read the finished week
##
## Every hook is a no-op until its lane lands, and the tick's arithmetic is
## byte-identical while they are: that invariant is what lets nine lanes ship
## in parallel against one engine.
static func weekly_tick(state: GameState) -> Dictionary:
	var rep := {"lines": [], "fired_clocks": [], "expired": [], "events": []}
	var th := state.theta
	if th.is_empty():
		th = default_theta(state.biz_what, state.biz_who)
		state.theta = th
	# a legacy save's single `marketing` budget becomes paid ads before any
	# reader sees it (idempotent — safe every tick, docs/design/04 §5)
	migrate_budgets(state)

	# 1 ── clocks: deadlines fire deterministically
	var kept_clocks: Array = []
	for c in state.clocks:
		var cd: Dictionary = c
		cd["weeks_left"] = int(cd.get("weeks_left", 1)) - 1
		if int(cd["weeks_left"]) <= 0:
			rep["fired_clocks"].append(String(cd.get("consequence", "a deadline passes")))
		else:
			kept_clocks.append(cd)
	state.clocks = kept_clocks

	# 2 ── statuses decrement, expire
	var kept_status: Array = []
	for s in state.statuses:
		var sd: Dictionary = s
		sd["weeks_left"] = int(sd.get("weeks_left", 1)) - 1
		if int(sd["weeks_left"]) <= 0:
			rep["expired"].append(String(sd.get("name", "")))
		else:
			kept_status.append(sd)
	state.statuses = kept_status

	# 3a ── the hiring pipeline advances: cohort 0 onboards → cohort 1 → productive
	# (graduates join the roster BEFORE the labor market counts open seats)
	if state.pipeline.size() > 0:
		var grads: Array = []
		var still: Array = []
		for h in state.pipeline:
			var hd: Dictionary = h
			hd["weeks_in"] = int(hd.get("weeks_in", 0)) + 1
			if int(hd["weeks_in"]) >= 2:
				grads.append(hd)
			else:
				still.append(hd)
		state.pipeline = still
		for g in grads:
			state.employees.append({"name": String(g.get("name", "hire")),
				"role": String(g.get("role", "engineer")),
				"salary": int(g.get("salary", 1200)), "burnout": 10,
				"quirk": String(g.get("quirk", ""))})
			rep["lines"].append("%s finished onboarding — productive now" % g.get("name", "a hire"))

	# 3b ── THE LABOR MARKET: arrivals → applicant decay → review cycle. The
	# roster and the applicant pool must be final before morale feels them in
	# §4 and payroll pays them in §9.
	SimLabor.tick_pre(state, rep)

	# 4 ── fatigue and morale drift (the slow tax)
	var crunching := has_status(state, "crunch")
	var target_fatigue := 65.0 if crunching else 20.0
	state.fatigue += (target_fatigue - state.fatigue) / 4.0
	# morale drifts toward a lived-in 50 (up when battered, down when coasting);
	# statuses, red ink and events push around that baseline
	var morale_wk := (50.0 - float(state.morale)) / 6.0
	morale_wk += 3.0 * (1.0 - exp(-float(state.budgets.get("office", 0)) / 800.0))
	for s2 in state.statuses:
		morale_wk += float(STATUS.get(String((s2 as Dictionary).get("name", "")), {}).get("morale_wk", 0.0))
	if state.cash < 0:
		morale_wk -= 3.0
	state.morale = clampi(int(state.morale + morale_wk), 0, 100)
	# burnout cliff: below 30 someone may walk — best people first
	if state.morale < 30 and state.employees.size() > 0:
		var r4 := _rng(state, SALT_MORALE_QUIT)
		if r4.randf() < 0.6 * float(31 - state.morale) / 31.0:
			var best_i := 0
			for i in state.employees.size():
				if int(state.employees[i].get("salary", 0)) > int(state.employees[best_i].get("salary", 0)):
					best_i = i
			var quit: Dictionary = state.employees[best_i]
			state.employees.remove_at(best_i)
			rep["events"].append("%s quit (morale %d): the good ones leave first" % [quit.get("name", "someone"), state.morale])
	# exhaustion track 0-6 rises with fatigue, falls with rest
	if state.fatigue > 55.0:
		state.exhaustion = mini(state.exhaustion + 1, 6)
	elif state.fatigue < 30.0 and state.exhaustion > 0:
		state.exhaustion -= 1
	if state.exhaustion >= 4 and not has_status(state, "burnt_out"):
		add_status(state, "burnt_out", 3)
		rep["events"].append("the founder is burnt out (exhaustion %d)" % state.exhaustion)

	# 5 ── tech debt: decays product if nobody builds; outage roll
	var eng := 0
	for e in state.employees:
		if String(e.get("role", "")).contains("engineer"):
			eng += 1
	if eng == 0 and state.competences.get("build", 3) < 4:
		state.tech_debt = minf(state.tech_debt + 1.5, 100.0)
	var r5 := _rng(state, SALT_OUTAGE)
	if state.tech_debt > 40.0 and r5.randf() < (state.tech_debt - 40.0) / 250.0:
		add_status(state, "outage_fallout", 2)
		rep["events"].append("OUTAGE — the debt collected (debt %d)" % int(state.tech_debt))

	# 6a ── THE STREET: rivals act (per-rival upkeep → weekly action pick →
	# poach → disruptor), then 6b MACRO. Rivals move BEFORE the market, so a
	# price cut or a launch shapes THIS week's demand; the poach lands after
	# arrivals (3b) and before payroll (9).
	SimStreet.tick_pre(state, rep)
	if not SimStreet.OWNS_RIVALS:
		# THE LEGACY RATCHET (salt 6, retired by 03): strength drifts up and a
		# move lands now and then. The lane replaces this wholesale by flipping
		# OWNS_RIVALS; until then the old world runs exactly as it always has.
		var r6 := _rng(state, SALT_RIVAL_RATCHET)
		for rv in state.rivals:
			var rd: Dictionary = rv
			rd["strength"] = minf(float(rd.get("strength", 20.0)) + r6.randf_range(0.0, 1.2), 95.0)
			rd["weeks_since_move"] = int(rd.get("weeks_since_move", 0)) + 1
			if int(rd["weeks_since_move"]) >= 5 and r6.randf() < 0.4:
				rd["weeks_since_move"] = 0
				rd["strength"] = minf(float(rd["strength"]) + 4.0, 95.0)
				rep["events"].append("%s made a move — %s" % [rd.get("name", "a rival"),
					String(rd.get("tactics", ["shipped something loud"])[r6.randi() % (rd.get("tactics", ["x"]) as Array).size()])])
	# avg-strength pressure closes 6a whoever moved the rivals — the market
	# reads the settled board, never the mover
	var pressure := 0.0
	for rv2 in state.rivals:
		pressure += float((rv2 as Dictionary).get("strength", 0.0))
	pressure = minf(pressure / maxf(float(state.rivals.size()), 1.0) / 100.0 * 0.5, 0.45)

	# 6b ── MACRO: the market-mood walk. 03-macro mean-reverts this around a
	# season cycle using the SAME single salt-7 draw (stream preserved), so
	# owning it never shifts another subsystem's dice.
	if not SimStreet.OWNS_MACRO:
		var r7 := _rng(state, SALT_TREND)
		state.market_trend = clampf(state.market_trend + r7.randf_range(-1.0, 1.0) * float(th.trend_vol), 0.5, 1.5)

	# 7 ── ROADMAP BETS: rnd-routed progress, READY bets roll the house dice.
	# A shipped bet's payoff must exist before adoption reads product.
	SimRoadmap.tick_pre(state, rep)

	# 7h ── HARDWARE PRODUCTION: build target → produce → breakdown roll.
	# PRODUCE FIRST — stock must exist before adoption can be clamped to it.
	SimFactory.tick_pre(state, rep)

	# 8 ── adoption and churn (Bass + quality residence). The market moves
	# exactly ONCE, after weather, rivals, quality and stock are all settled.
	SimCatalog.tick_pre(state, rep)
	SimFunnel.tick_pre(state, rep)
	SimPipeline.tick_pre(state, rep)
	var A := float(state.traction)
	var N := float(th.tam)
	var P := maxf(N - A, 0.0)
	var hype_mult := 0.6 + float(state.hype) / 100.0 * 0.9
	# THE LEVERS (owner: "actually spending money on different topics"): four
	# weekly budgets the player sets in the Binder's ledger. Every dollar is
	# real: it leaves cash in section 9, and it does exactly this —
	#   marketing -> reach (diminishing via cac_sat), sales -> closing capacity,
	#   care -> retention, rnd -> product quality and debt paydown.
	var bud: Dictionary = state.budgets
	# the acquisition spend is the FOUR channels summed (04): a legacy save's
	# `marketing` already migrated into `ads`, and the legacy `set_marketing`
	# op's budget folds in here exactly as it always did
	var b_mk := float(int(bud.get("ads", 0)) + int(bud.get("content", 0))
			+ int(bud.get("referrals", 0)) + int(bud.get("outbound", 0))
			+ state.marketing_budget)
	var b_sales := float(bud.get("sales", 0))
	var b_care := float(bud.get("care", 0))
	var b_rnd := float(bud.get("rnd", 0))
	# THE OFFICE LANE (owner: running a business is also salaries, benefits,
	# rent, food — Bonopoly's training/retention molecule): a weekly budget
	# for food, perks and benefits that buys morale and keeps people whole.
	var b_office := float(bud.get("office", 0))
	var mk_budget := b_mk
	# REACH: one blended saturating curve today; 04 replaces it with the
	# four-channel reach term through this seam (the stub hands back the
	# default, so the blended lever is what runs until the lane lands).
	var mk_mult := SimFunnel.reach_mult(state, mk_budget,
			1.0 + 1.4 * (1.0 - exp(-mk_budget / float(th.cac_sat))))
	var status_adopt := 1.0
	var status_churn := 1.0
	var status_arpu := 1.0
	for s3 in state.statuses:
		var eff: Dictionary = STATUS.get(String((s3 as Dictionary).get("name", "")), {})
		status_adopt *= float(eff.get("adopt_mult", 1.0))
		status_churn *= float(eff.get("churn_mult", 1.0))
		status_arpu *= float(eff.get("arpu_mult", 1.0))
	# NOTHING SELLS ITSELF BEFORE LAUNCH: organic adoption requires the launch;
	# an unlaunched product only grows by word of mouth of the few it has (half
	# rate) and whatever the founder's written moves win directly.
	var launched := state.has_flag("launched")
	var quality_gate := 0.2 + float(state.product) / 100.0 * 0.8
	var p_eff := float(th.adopt_p) * hype_mult * mk_mult * status_adopt \
			* state.market_trend * (1.0 - pressure) * quality_gate \
			* (1.0 if launched else 0.0)
	var wom := float(th.adopt_ic) * A * P / maxf(N, 1.0) * status_adopt \
			* (1.0 - pressure) * quality_gate * (1.0 if launched else 0.5)
	var price_demand := pow(maxf(state.price_mult, 0.1), -1.5)
	var offer_mult := offers_demand_mult(state)
	if offer_mult >= 0.0:
		# offers exist: THEY are the price signal. Unpriced offers bill at
		# fair (demand 1.0); a mult of ~0 now only means priced-to-the-moon,
		# and the 0.1 clamp below is lifeline enough.
		price_demand = offer_mult
	var adds := (p_eff * P + wom) * clampf(price_demand, 0.1, 3.0)
	# THE GTM CAPACITY CLAMP (tycoon's staffingBalance): demand is not closing.
	# A tiny team can only land what its go-to-market can actually handle —
	# founder sell-stat, sales hires, and marketing reach set the weekly ceiling.
	var sales_heads := 0
	for e3 in state.employees:
		if String(e3.get("role", "")).contains("sales"):
			sales_heads += 1
	var cap_scale := 1.0
	match state.biz_who:
		"SMB": cap_scale = 3.0
		"Consumer": cap_scale = 40.0
	# a sales budget hires fractional closing power (an SDR-hour equivalent)
	var gtm_cap := (1.5 + 0.8 * float(state.competences.get("sell", 3)) 			+ 3.0 * float(sales_heads) + mk_budget / 400.0 + b_sales / 600.0) * cap_scale
	adds = minf(adds, gtm_cap)
	# HARDWARE: you cannot sell what you did not build. 09 clamps adds to the
	# shelf and decrements it here; off Hardware the stub hands adds straight
	# back, so demand is stock-free exactly as it is today.
	adds = SimFactory.clamp_adds(state, rep, adds)
	var residence := float(th.lifetime_wk) * (0.4 + float(state.product) / 100.0 * 1.2)
	# customer care keeps people: churn eases toward −30% as care approaches ~$3k/wk
	var care_mult := 1.0 - 0.30 * (1.0 - exp(-b_care / 1500.0))
	var churn := A / maxf(residence, 2.0) * float(th.churn_mult) * status_churn * care_mult
	# pricing pain lands on RETENTION, never on invisible spend-shrink
	churn *= offers_price_pain(state)
	# a market of 0.3 adds/wk is a REAL market: int(round()) erased Enterprise
	# forever — the seeded remainder (the R&D block's own idiom) keeps it
	var net_f := adds - churn
	var net := int(floor(absf(net_f))) * (1 if net_f >= 0.0 else -1)
	if _rng(state, SALT_ADOPT_REMAINDER).randf() < absf(net_f) - floor(absf(net_f)):
		net += 1 if net_f >= 0.0 else -1
	# ENTERPRISE: named accounts arrive through the pipeline, not the coin —
	# 05 routes adds/churn through its own stream here and returns the week's
	# real net. Every other run gets the seeded remainder back unchanged.
	net = SimPipeline.adoption_net(state, rep, adds, churn, net)
	state.traction = maxi(state.traction + net, 0)
	rep["adds"] = int(round(adds))
	rep["churn"] = int(round(churn))
	if adds >= 1.0:
		rep["lines"].append("+%d customers (organic %d · word of mouth %d)" % [int(round(adds)), int(round(p_eff * P * price_demand)), int(round(wom * price_demand))])
	if churn >= 1.0:
		rep["lines"].append("−%d churned (lifetime %d wks at v0.%d)" % [int(round(churn)), int(round(residence)), state.product])

	# 9 ── MONEY & P&L. One place computes the week's truth: revenue, cost of
	# serving, the standing costs, every lane's spend, THEN interest, THEN tax,
	# and only then the record. Interest and tax land BEFORE the record is
	# written so the ledger never lies about what the week actually cost.
	SimBank.tick_pre(state, rep)
	SimBoard.tick_pre(state, rep)
	var arpu_off := offers_arpu(state)
	var revenue := 0.0
	if arpu_off >= 0.0:
		revenue = float(state.traction) * arpu_off * status_arpu
		if state.traction > 0 and offers_any_unpriced(state):
			rep["lines"].append("no price on the wall — the market paid the going rate (~$%d/customer/wk). Name yours in THE BINDER." % int(round(arpu_off)))
		elif state.traction > 0 and offers_any_free(state):
			rep["lines"].append("free on purpose — the giveaway pays in users, not dollars.")
		elif arpu_off == 0.0 and state.traction > 0:
			rep["lines"].append("NOTHING IS ON SALE — %d customers, $0 revenue. Set prices in THE BINDER." % state.traction)
	else:
		revenue = float(state.traction) * float(th.arpu_wk) * state.price_mult * status_arpu
	var payroll := 0
	for e2 in state.employees:
		payroll += int(e2.get("salary", 0))
	for h2 in state.pipeline:
		payroll += int(h2.get("salary", 0))          # paid before productive
	var rent := int(GameState.ERA_RENT.get(state.era, 150))
	var infra := 50 + int(float(state.traction) * 0.05)
	# R&D: a real budget ships real product — +1 quality per ~$1200/wk (seeded
	# remainder), and it pays down tech debt as it goes
	if b_rnd > 0.0:
		var quality_gain := b_rnd / 1200.0
		var whole := int(floor(quality_gain))
		if _rng(state, SALT_RND_REMAINDER).randf() < quality_gain - float(whole):
			whole += 1
		if whole > 0:
			state.product = mini(state.product + whole, 100)
			rep["lines"].append("R&D shipped: product v0.%d" % state.product)
		state.tech_debt = maxf(state.tech_debt - b_rnd / 1500.0, 0.0)
	var cogs := 0.0
	if arpu_off >= 0.0:
		cogs = float(state.traction) * offers_cogs_per_customer(state)
		if cogs >= 1.0:
			rep["lines"].append("cost of serving customers: $%d" % int(round(cogs)))
	# the more you have served, the cheaper serving gets (Bonopoly's learning
	# curve): the discount lives in offers_cogs_per_customer via served_total.
	# A FIELD, not a meta — metas do not survive a save, and the curve silently
	# reset to zero on every load until this moved (docs/design/DECISIONS.md §A3).
	state.served_total += state.traction
	var offer_fixed := offers_fixed_wk(state)
	if offer_fixed >= 1.0:
		rep["lines"].append("catalog overheads: $%d/wk (tools, licenses, storage)" % int(round(offer_fixed)))

	# THE WORKING MONEY RECORD (docs/design/00-spine.md §2): one key per P&L
	# lane. The engine fills its own lanes above; each subsystem writes ONLY the
	# lanes it owns; the engine then sums burn and writes the record whole.
	var m := {
		"revenue": revenue, "cogs": cogs,
		"rent": rent, "payroll": payroll, "infra": infra,
		"marketing": mk_budget, "sales": b_sales, "care": b_care,
		"rnd": b_rnd, "office": b_office,
		"offer_fixed": offer_fixed,
		"severance": 0.0, "recruiting": 0.0,
		"production": 0.0, "subcontract": 0.0,
		"equip_upkeep": 0.0, "carrying": 0.0,
		"incident": 0.0, "liabilities_wk": 0,
		"interest": 0.0, "tax": 0.0, "burn": 0,
	}
	SimCatalog.tick_money(state, rep, m)
	SimLabor.tick_money(state, rep, m)
	SimStreet.tick_money(state, rep, m)
	SimFunnel.tick_money(state, rep, m)
	SimPipeline.tick_money(state, rep, m)
	SimBank.tick_money(state, rep, m)
	SimRoadmap.tick_money(state, rep, m)
	SimBoard.tick_money(state, rep, m)
	SimFactory.tick_money(state, rep, m)

	# THE UNFORESEEN (owner: running a business includes what nobody planned):
	# some weeks a small real cost lands — seeded, receipted, never a mystery.
	var inc_r := _rng(state, SALT_INCIDENTS)
	var incident_cost := 0
	if inc_r.randf() < 0.30:
		incident_cost = int(float(rent + payroll + infra) * inc_r.randf_range(0.01, 0.04)) + inc_r.randi_range(20, 90)
		var inc_what: Array = ["the printer died mid-invoice", "the fridge gave up",
			"a parking fine found the van", "the wifi needed a new router",
			"someone broke the good chair", "the same invoice arrived twice",
			"a deposit nobody remembered came due", "the door lock jammed after hours"]
		rep["lines"].append("the unforeseen: −$%d (%s)" % [incident_cost,
			String(inc_what[inc_r.randi_range(0, inc_what.size() - 1)])])
	elif inc_r.randf() < 0.06 and state.week >= 4:
		# THE STANDING LIABILITY (rare): some surprises do not leave — a rent
		# bump, an insurance premium, a machine on a payment plan. It becomes a
		# commitment the ledger pays and prints every week until it runs out.
		var liab_pick: Array = [
			{"name": "the landlord adjusts the rent", "wk": 8, "frac": 0.06},
			{"name": "liability insurance, overdue", "wk": 6, "frac": 0.05},
			{"name": "the compliance letter means a lawyer", "wk": 4, "frac": 0.08},
			{"name": "the broken machine went on a payment plan", "wk": 6, "frac": 0.06},
		]
		var lb: Dictionary = liab_pick[inc_r.randi_range(0, liab_pick.size() - 1)]
		var wkcost := -maxi(int(float(rent + payroll) * float(lb.get("frac", 0.06))), 40)
		state.commitments.append({"name": String(lb.get("name", "a standing cost")),
			"cash_wk": wkcost, "weeks_left": int(lb.get("wk", 6))})
		rep["lines"].append("NEW STANDING COST: %s — $%d/wk for %d weeks" % [
			String(lb.get("name", "a standing cost")), -wkcost, int(lb.get("wk", 6))])
	m["incident"] = float(incident_cost)

	# BURN IS OPERATING SPEND ONLY (docs/design/00-spine.md §2). Interest and
	# tax sit OUTSIDE it — the real income-statement shape, which is the whole
	# pedagogy: operating profit → cost of debt → tax → net.
	var lane_burn := float(m["severance"]) + float(m["recruiting"]) \
			+ float(m["production"]) + float(m["subcontract"]) \
			+ float(m["equip_upkeep"]) + float(m["carrying"])
	var burn := int((float(rent + payroll + infra) + mk_budget + b_sales + b_care + b_rnd + b_office) * float(th.burn_mult) + cogs + offer_fixed + lane_burn)
	burn += incident_cost
	m["burn"] = burn
	state.cash += int(round(revenue)) - burn
	# THE COST OF DEBT, before the record. The legacy shark note compounds here
	# and its interest becomes a real P&L lane; 06 takes the whole step over
	# (structured notes, honest rates, amortization) by flipping OWNS_DEBT.
	if not SimBank.OWNS_DEBT:
		if state.loan_principal > 0:
			var interest := int(ceil(float(state.loan_principal) * 0.18))
			state.loan_principal += interest
			m["interest"] = float(m["interest"]) + float(interest)
			rep["lines"].append("the loan compounds: +$%d interest (owe $%d)" % [interest, state.loan_principal])
			if state.cash > 2000:
				var pay := mini(state.cash - 1500, state.loan_principal)
				state.cash -= pay
				state.loan_principal -= pay
				rep["lines"].append("auto-repaid $%d of the loan" % pay)
	var liab_wk := 0
	for cm0 in state.commitments:
		liab_wk += mini(int((cm0 as Dictionary).get("cash_wk", 0)), 0)
	m["liabilities_wk"] = -liab_wk
	# THE STATE, last: tax is charged on what is left after interest, so it can
	# only be computed once every other lane has closed (06 owns the math).
	m["tax"] = float(SimBank.tax_wk(state, m))
	# THE RECORD, written whole and exactly once. The identity a twin test pins
	# every week:  net = revenue − burn − liabilities_wk − interest − tax.
	state.set_meta("pnl", {
		"revenue": int(round(revenue)), "cogs": int(round(cogs)),
		"rent": rent, "payroll": payroll, "infra": infra,
		"marketing": int(mk_budget), "sales": int(b_sales), "care": int(b_care),
		"rnd": int(b_rnd), "office": int(b_office),
		"offer_fixed": int(round(offer_fixed)),
		"severance": int(round(float(m["severance"]))),
		"recruiting": int(round(float(m["recruiting"]))),
		"production": int(round(float(m["production"]))),
		"subcontract": int(round(float(m["subcontract"]))),
		"equip_upkeep": int(round(float(m["equip_upkeep"]))),
		"carrying": int(round(float(m["carrying"]))),
		"incident": incident_cost,
		"liabilities_wk": -liab_wk,
		"interest": int(round(float(m["interest"]))),
		"tax": int(round(float(m["tax"]))),
		"burn": burn,
		"net": int(round(revenue)) - burn + liab_wk
			- int(round(float(m["interest"]))) - int(round(float(m["tax"]))),
		"learning": learning_curve(state),
	})
	if state.get_meta("prev_revenue", 0.0) > 1.0:
		state.last_growth = clampf((revenue - float(state.get_meta("prev_revenue"))) / float(state.get_meta("prev_revenue")), -0.5, 0.5)
	state.set_meta("prev_revenue", revenue)
	rep["revenue"] = int(round(revenue))
	rep["burn"] = burn
	var lever_txt := ""
	if b_sales + b_care + b_rnd + b_office > 0.0:
		lever_txt = " · sales %d · care %d · rnd %d · office %d" % [int(b_sales), int(b_care), int(b_rnd), int(b_office)]
	rep["lines"].append("$%d in · $%d out (rent %d · payroll %d · infra %d · marketing %d%s)" % [
		int(round(revenue)), burn, rent, payroll, infra, int(mk_budget), lever_txt])
	# ── UNIT ECONOMICS, computed honestly every week (the simulator SHOWS its
	# math): CAC from what acquisition actually cost / who actually arrived;
	# LTV from residence × margin-per-week; payback in weeks.
	var arpu_real := offers_arpu(state)
	var arpu := (arpu_real if arpu_real >= 0.0 else float(th.arpu_wk) * state.price_mult) * status_arpu
	var new_adds := maxf(adds, 0.0)
	rep["cac"] = int(round((b_mk + b_sales) / new_adds)) if new_adds >= 0.5 and (b_mk + b_sales) > 0.0 else 0
	rep["ltv"] = int(round(residence * arpu))
	rep["payback_wk"] = int(ceil(float(rep["cac"]) / maxf(arpu, 0.01))) if int(rep["cac"]) > 0 else 0
	state.set_meta("unit_econ", {"arpu": arpu, "cac": rep["cac"], "ltv": rep["ltv"],
		"payback_wk": rep["payback_wk"], "residence": int(residence)})

	# 9b ── the founder's working assumptions converge toward the truth.
	# Rate: analytics tooling, real customers, and R&D all teach.
	if state.beliefs.is_empty():
		seed_beliefs(state)
	else:
		var k := clampf(0.02 + 0.05 * float(state.analytics_level)
				+ 0.003 * float(state.traction) + b_rnd / 40000.0, 0.0, 0.30)
		state.beliefs["tam"] = float(state.beliefs["tam"]) + (float(th.tam) - float(state.beliefs["tam"])) * k
		state.beliefs["lifetime_wk"] = float(state.beliefs["lifetime_wk"]) \
				+ (float(th.lifetime_wk) - float(state.beliefs["lifetime_wk"])) * k

	# 9c/9d ── the week is closed, so the readers of a closed week run now:
	# the board review against the covenant, M&A offers priced off this week's
	# growth, bets that finished, catalog and factory bookkeeping.
	SimCatalog.tick_post(state, rep)
	SimLabor.tick_post(state, rep)
	SimStreet.tick_post(state, rep)
	SimFunnel.tick_post(state, rep)
	SimPipeline.tick_post(state, rep)
	SimBank.tick_post(state, rep)
	SimRoadmap.tick_post(state, rep)
	SimBoard.tick_post(state, rep)
	SimFactory.tick_post(state, rep)

	# 10 ── commitments (recurring deltas with duration)
	var kept_comm: Array = []
	for cm in state.commitments:
		var cmd: Dictionary = cm
		state.cash += int(cmd.get("cash_wk", 0))
		cmd["weeks_left"] = int(cmd.get("weeks_left", 1)) - 1
		rep["lines"].append("%s: %s$%d" % [cmd.get("name", "commitment"),
			"+" if int(cmd.get("cash_wk", 0)) >= 0 else "−", absi(int(cmd.get("cash_wk", 0)))])
		if int(cmd["weeks_left"]) > 0:
			kept_comm.append(cmd)
		else:
			rep["expired"].append(String(cmd.get("name", "")))
	state.commitments = kept_comm

	# the binder's memory: one snapshot per week, capped
	state.metric_history.append({"wk": state.week, "cash": state.cash,
		"customers": state.traction, "revenue": int(rep.get("revenue", 0)),
		"burn": int(rep.get("burn", 0)), "morale": state.morale,
		"debt": int(state.tech_debt), "hype": state.hype})
	if state.metric_history.size() > 90:
		state.metric_history = state.metric_history.slice(state.metric_history.size() - 90)
	state.clampi_meters()
	return rep

# ─────────────────────────── status / clock helpers ──────────────────────────
static func add_status(state: GameState, name: String, weeks: int) -> bool:
	if not STATUS.has(name):
		return false
	for s in state.statuses:
		if String((s as Dictionary).get("name", "")) == name:
			(s as Dictionary)["weeks_left"] = maxi(int((s as Dictionary).get("weeks_left", 0)), weeks)
			return true
	state.statuses.append({"name": name, "weeks_left": maxi(weeks, 1)})
	return true

static func has_status(state: GameState, name: String) -> bool:
	for s in state.statuses:
		if String((s as Dictionary).get("name", "")) == name:
			return true
	return false

static func add_clock(state: GameState, weeks: int, consequence: String) -> void:
	state.clocks.append({"weeks_left": maxi(weeks, 1), "consequence": consequence.left(120)})

# ───────────────────────── the D&D resolution layer ──────────────────────────
const DC_FLOORS := {"routine": 6, "solid": 9, "bold": 12, "wild": 15}

# ─────────────────────────────── THE SIX TRAITS ──────────────────────────────
## Competences are rolled; TRAITS are never rolled. They are who the founder is,
## and they bend the dice and the terms from behind — an ex-FAANG PM walks into
## the raise with credibility and a phone full of numbers, and the room is
## simply easier for her than it is for the hacker, every single time.
##
## The numbers live HERE, once, in the same table as the words that explain
## them: the select card, the bag page and the DM all print these strings, so a
## rule can never drift from its own description. Thresholds:
##   charisma   4+  advantage on sell and recruit
##   focus      4+  advantage on build
##   cred+net   8+  advantage on raise, and warmer term sheets
##   luck       4+  a natural 1 is rerolled once  ·  1  a natural 20 is only 19
##   stamina    2-  disadvantage on grit once exhaustion bites
const TRAIT_RULES := {
	"charisma": "People say yes to you. At 4+ you roll SELL and RECRUIT with advantage: two dice, keep the best.",
	"luck": "The dice bend. At 4+ a natural 1 is rerolled once. At 1 a natural 20 only ever counts as 19.",
	"network": "Counted together with CREDIBILITY. At 8+ combined the investor doors open: advantage on RAISE.",
	"focus": "Deep work. At 4+ you roll BUILD with advantage: two dice, keep the best.",
	"credibility": "Counted with NETWORK. At 8+ combined you raise with advantage, and offers ask up to 8% less equity.",
	"stamina": "Reserves. At 2 or less, GRIT rolls go to disadvantage as soon as exhaustion reaches 3.",
}

## Which trait rules are ON for this founder right now, in the words the screens
## print. The bag page reads this to answer "what did packing that actually buy".
static func trait_effects(state: GameState) -> Array[String]:
	var out: Array[String] = []
	var doors := state.trait_level("credibility") + state.trait_level("network")
	if doors >= 8:
		out.append("doors open (cred+net %d): advantage on RAISE" % doors)
	elif doors == 7:
		out.append("one point from open doors (cred+net 7)")
	if state.trait_level("charisma") >= 4:
		out.append("people say yes: advantage on SELL + RECRUIT")
	if state.trait_level("focus") >= 4:
		out.append("deep work: advantage on BUILD")
	if state.trait_level("luck") >= 4:
		out.append("luck rerolls a natural 1")
	if state.trait_level("luck") <= 1:
		out.append("a natural 20 only counts as 19")
	if state.trait_level("stamina") <= 2:
		out.append("no reserves: disadvantage on GRIT when tired")
	var warm := warmth_pct(state)
	if warm > 0.0:
		out.append("offers ask %.0f%% less equity" % warm)
	return out

## Advantage/disadvantage from STATE — items, hires, statuses, exhaustion, and
## the six traits the founder never rolls.
static func roll_context(state: GameState, stat: String) -> Dictionary:
	var adv: Array[String] = []
	var dis: Array[String] = []
	for s in state.statuses:
		var eff: Dictionary = STATUS.get(String((s as Dictionary).get("name", "")), {})
		if String(eff.get("adv", "")) == stat:
			adv.append(String((s as Dictionary).get("name", "")))
		if String(eff.get("dis", "")) == stat:
			dis.append(String((s as Dictionary).get("name", "")))
	if state.exhaustion >= 3 and stat == "grit":
		dis.append("exhaustion %d" % state.exhaustion)
	if state.tech_debt > 70.0 and stat == "build":
		dis.append("tech debt %d" % int(state.tech_debt))
	for e in state.employees:
		var role := String(e.get("role", ""))
		if role.contains("sales") and stat == "sell" and not adv.has("sales team"):
			adv.append("sales team")
	# WHO YOU ARE, at the table. Same reasons the card promised, word for word.
	if stat == "raise":
		var doors := state.trait_level("credibility") + state.trait_level("network")
		if doors >= 8:
			adv.append("doors open (credibility+network %d)" % doors)
	if (stat == "sell" or stat == "recruit") and state.trait_level("charisma") >= 4:
		adv.append("people say yes to you")
	if stat == "build" and state.trait_level("focus") >= 4:
		adv.append("deep work")
	if stat == "grit" and state.trait_level("stamina") <= 2 and state.exhaustion >= 3:
		dis.append("no reserves")
	var has_a := adv.size() > 0
	var has_d := dis.size() > 0
	return {"advantage": has_a and not has_d, "disadvantage": has_d and not has_a,
			"adv_reasons": adv, "dis_reasons": dis}

## The full roll: 1d20, or 2d20 keep best/worst under advantage/disadvantage,
## and then LUCK, which only ever touches the two extremes and says so out loud.
## Every die comes out of the caller's roller, so a run replays exactly.
static func roll_d20_ctx(state: GameState, stat: String, rng_roll: Callable) -> Dictionary:
	var ctx := roll_context(state, stat)
	var a: int = rng_roll.call()
	var b: int = rng_roll.call()
	var used := a
	if bool(ctx.advantage):
		used = maxi(a, b)
	elif bool(ctx.disadvantage):
		used = mini(a, b)
	ctx["rolls"] = [a, b] if (bool(ctx.advantage) or bool(ctx.disadvantage)) else [a]
	# THE LUCKY ARE SPARED THE 1; THE UNLUCKY NEVER GET THE 20.
	var luck := state.trait_level("luck")
	var note := ""
	if used == 1 and luck >= 4:
		used = int(rng_roll.call())
		note = "luck rerolls the 1"
	elif used == 20 and luck <= 1:
		used = 19
		note = "never quite perfect"
	ctx["a"] = a
	ctx["b"] = b
	ctx["luck_note"] = note
	ctx["d20"] = used
	ctx["mod"] = int(state.competences.get(stat, 3)) - 3
	ctx["total"] = used + int(ctx["mod"])
	return ctx

## total − dc → the band that FORCES the narration frame.
static func margin_band(total: int, dc: int) -> String:
	var m := total - dc
	if m >= 5:
		return "brilliant"
	if m >= 0:
		return "fine"
	if m >= -2:
		return "risky"
	return "backfired"

# ───────────────────────────── the funding module ────────────────────────────
static func valuation(state: GameState) -> int:
	var arpu_v := offers_arpu(state)
	if arpu_v < 0.0:
		arpu_v = float(state.theta.get("arpu_wk", 4.0)) * state.price_mult
	var arr := float(state.traction) * arpu_v * 52.0
	var growth := clampf(float(state.last_growth), 0.0, 0.4)
	var mult := 8.0 + minf(12.0, growth * 60.0)
	return maxi(state.cash, int(arr * mult * float(state.theta.get("funding_mult", 1.0))))

## HOW WARM THE ROOM IS, in percent off the equity asked. Credibility and the
## phone book are read together: every point over 6 combined is worth about 2%
## less dilution, capped at 8%. This is the owner's ex-FAANG case, priced — the
## same company raises on better terms because of who is asking.
static func warmth_pct(state: GameState) -> float:
	var doors := state.trait_level("credibility") + state.trait_level("network")
	return minf(2.0 * float(maxi(doors - 6, 0)), 8.0)

## Three offers against fair price; desperation prices against you, standing
## in the room warms them back.
static func generate_offers(state: GameState, investors: Array) -> Array:
	var pre := valuation(state)
	var r := _rng(state, SALT_TERM_SHEETS)
	var desperate := state.cash < 0 or runway_weeks(state) <= 4
	var warm := warmth_pct(state)
	var out: Array = []
	for i in 3:
		var inv: Dictionary = investors[i % maxi(investors.size(), 1)] if investors.size() > 0 else {"name": "an angel"}
		var amount := int(float(pre) * r.randf_range(0.05, 0.15))
		var fair := float(amount) / float(pre + amount) * 100.0
		var spread := r.randf_range(1.15, 1.6) * (1.35 if desperate else 1.0) * (1.0 - warm / 100.0)
		out.append({"investor": String(inv.get("name", "?")),
			"amount": maxi(amount, 5_000),
			"equity_pct": snappedf(clampf(fair * spread, 1.0, 45.0), 0.1),
			"fair_pct": snappedf(fair, 0.1),
			"warmth": snappedf(warm, 0.1),
			"thesis": String(inv.get("thesis", ""))})
	return out

static func apply_round(state: GameState, amount: int, equity_pct: float) -> void:
	state.cash += amount
	var keep := 1.0 - equity_pct / 100.0
	state.founder_pct = maxf(state.founder_pct * keep, 1.0)
	for cf in state.cofounders:
		cf["equity_diluted"] = float(cf.get("equity_diluted", cf.get("equity", 0.0))) * keep
	var ladder := ["pre-seed", "seed", "series_a", "series_b", "series_c", "growth"]
	state.rounds_raised.append(ladder[mini(state.rounds_raised.size(), ladder.size() - 1)])
	state.morale = clampi(state.morale + 5, 0, 100)

# ───────────────────────────── derived signals ───────────────────────────────
## THE DEMAND CURVE (owner: nobody EVER buys a $500 massage): how much of
## fair demand survives at this price. (p/fair)^-elasticity, clamped so a
## giveaway can at most triple demand and an absurd price sells ~nothing.
## `fair_mult` is THE STREET'S price, not yours: while a rival's price war runs,
## the going rate itself drops, so holding your list price reads as expensive
## (03 §5.1). 1.0 = no war, and everything below behaves exactly as before.
static func offer_demand(offer: Dictionary, price: float, fair_mult: float = 1.0) -> float:
	var fair := maxf(float(offer.get("fair_price", 1.0)) * fair_mult, 0.01)
	if price <= 0.0:
		return 0.0   # not on sale
	var e := float(offer.get("elasticity", 2.0))
	return clampf(pow(price / fair, -e), 0.0, 2.0)

## THE GOING RATE, after the street has had its say: the product of every live
## status' `fair_mult`, floored so a war can never erase the market. Read at
## exactly three sites — demand, retention pain, and what an unpriced offer
## bills — because those are the three places a customer feels a price.
static func street_fair_mult(state: GameState) -> float:
	var mlt := 1.0
	for s in state.statuses:
		mlt *= float(STATUS.get(String((s as Dictionary).get("name", "")), {}).get("fair_mult", 1.0))
	return maxf(mlt, 0.85)

## How often one customer pays for an offer, in purchases per week. The
## founder's mental math is "customers × price"; the cadence is the honest
## bridge between that and a weekly ledger line.
static func offer_cadence(unit: String) -> float:
	var u := unit.to_lower()
	if u.contains("session") or u.contains("order") or u.contains("hour"):
		return 1.0
	if u.contains("month") or u.contains("plan"):
		return 0.25
	if u.contains("year"):
		return 0.02
	if u.contains("package") or u.contains("kit") or u.contains("unit") or u.contains("device"):
		return 0.2
	return 0.5

## REAL weekly revenue per customer across offers. THE OWNER'S LAW (#196:
## "16 customers, $70 product, $200 revenue — the math is not mathing"):
## the old form taxed EXISTING customers' spend by price-demand and a
## hidden 0.25 cadence and quietly subtracted unit cost — three silent
## multipliers between a founder's mental math and the number.
## Now: demand gates ACQUISITION (adds) and pushes CHURN above fair price;
## existing customers simply pay their offer's price at its cadence, and
## the cost of serving them is a VISIBLE cogs line in burn.
## THE BACKSTOP (owner: customers paying $0 "is IMPOSSIBLE"): an offer the
## founder never priced bills at its FAIR price — the market pays the going
## rate until a price is named. Zero revenue with customers on the books
## cannot happen by algorithm, whatever the narrator missed.
static func offers_arpu(state: GameState) -> float:
	if state.offers.is_empty():
		return -1.0   # legacy runs: fall back to theta arpu
	var total := 0.0
	var fm := street_fair_mult(state)
	for o in state.offers:
		var od: Dictionary = o
		var price := offer_billed_price(od, fm)
		if price <= 0.0:
			continue
		total += float(od.get("weight", 1.0)) * price * offer_cadence(String(od.get("unit", "")))
	return total

## What an offer actually bills at: the founder's price, or the fair (going)
## rate while unpriced. 0 only when the offer has neither. An unpriced offer
## follows the street down during a price war — the going rate IS the street's.
static func offer_billed_price(od: Dictionary, fair_mult: float = 1.0) -> float:
	var price := float(od.get("price", 0.0))
	if price <= 0.0:
		# a CONSCIOUS $0 (price_set) stays free — the founder overruled the
		# backstop on purpose; only a never-priced offer bills at fair.
		if bool(od.get("price_set", false)):
			return 0.0
		price = maxf(float(od.get("fair_price", 0.0)) * fair_mult, 0.0)
	return price

## True when any offer is billing at the going rate instead of a named price.
static func offers_any_unpriced(state: GameState) -> bool:
	for o in state.offers:
		var od: Dictionary = o
		if float(od.get("price", 0.0)) <= 0.0 and float(od.get("fair_price", 0.0)) > 0.0 \
				and not bool(od.get("price_set", false)):
			return true
	return false

## True when any offer is consciously free (price_set at $0).
static func offers_any_free(state: GameState) -> bool:
	for o in state.offers:
		var od: Dictionary = o
		if bool(od.get("price_set", false)) and float(od.get("price", 0.0)) <= 0.0:
			return true
	return false

## The weekly cost of serving one customer's purchases (unit costs at the
## same cadence) — lands in burn where the ledger can show it.
static func offers_cogs_per_customer(state: GameState) -> float:
	if state.offers.is_empty():
		return 0.0
	var total := 0.0
	var lc := learning_curve(state)
	for o in state.offers:
		var od: Dictionary = o
		if offer_billed_price(od) <= 0.0 and not bool(od.get("price_set", false)):
			continue
		total += float(od.get("weight", 1.0)) * float(od.get("unit_cost", 0.0)) * lc \
				* offer_cadence(String(od.get("unit", "")))
	return total

## THE CATALOG IS THE FOUNDER'S (owner: decide what we sell) — but the
## WORLD prices reality: every field passes a clamp, the marginal cost stays
## a sane fraction of fair, and nothing enters the books unclamped. The new
## offer arrives UNPRICED (price 0, not price_set): it bills at the going
## rate until the founder names a price.
static func add_offer(state: GameState, o_name: String, unit: String,
		fair: float, cost: float, elasticity: float, weight: float,
		cost_lines: Array = [], fixed_lines: Array = []) -> Dictionary:
	var f := clampf(fair, 1.0, 50_000.0)
	var offer := {
		"name": o_name.substr(0, 40),
		"unit": (unit if unit != "" else "per order").substr(0, 20),
		"fair_price": f,
		"unit_cost": clampf(cost, 0.0, f * 0.9),
		"elasticity": clampf(elasticity, 0.5, 3.0),
		"weight": clampf(weight, 0.2, 3.0),
		"price": 0.0,
	}
	if not cost_lines.is_empty():
		offer["cost_lines"] = cost_lines
	if not fixed_lines.is_empty():
		offer["fixed_lines"] = fixed_lines
	sync_offer_costs(offer)
	state.offers.append(offer)
	return offer

## The itemised truth stays the truth: unit_cost = Σ variable lines (clamped
## to 90% of fair), fixed_wk = Σ weekly lines (clamped). Called after any
## per-line adjustment so the totals can never drift from their receipts.
static func sync_offer_costs(offer: Dictionary) -> void:
	var fair := maxf(float(offer.get("fair_price", 1.0)), 1.0)
	if offer.has("cost_lines"):
		var v := 0.0
		for cl in offer["cost_lines"]:
			var cld: Dictionary = cl
			cld["amount"] = clampf(float(cld.get("amount", 0.0)), 0.0, fair * 0.5)
			v += float(cld["amount"])
		offer["unit_cost"] = clampf(v, 0.0, fair * 0.9)
	if offer.has("fixed_lines"):
		var fx := 0.0
		for fl in offer["fixed_lines"]:
			var fld: Dictionary = fl
			fld["amount"] = clampf(float(fld.get("amount", 0.0)), 0.0, 5_000.0)
			fx += float(fld["amount"])
		offer["fixed_wk"] = clampf(fx, 0.0, 10_000.0)

## The weekly overhead the catalog itself carries (tool subscriptions,
## licenses, storage — each offer's fixed lines), independent of volume.
static func offers_fixed_wk(state: GameState) -> float:
	var total := 0.0
	for o in state.offers:
		# a hand-edited fixed_wk with no lines behind it is caught here
		total += clampf(float((o as Dictionary).get("fixed_wk", 0.0)), 0.0, 10_000.0)
	return total

static func remove_offer(state: GameState, idx: int) -> bool:
	if idx < 0 or idx >= state.offers.size():
		return false
	state.offers.remove_at(idx)
	return true

## Keyless (or model-down) pricing for a founder-written offer: the world's
## defaults, audience-scaled, unit sniffed from the words themselves.
static func draft_offer_terms(state: GameState, idea: String) -> Dictionary:
	var aud := 1.0
	match state.biz_who:
		"Consumer": aud = 0.25
		"Enterprise": aud = 4.0
	var low := idea.to_lower()
	var unit := "per order"
	if low.contains("month") or low.contains("subscription") or low.contains("plan"):
		unit = "per month"
	elif low.contains("session") or low.contains("workshop") or low.contains("class") or low.contains("consult"):
		unit = "per session"
	elif low.contains("kit") or low.contains("device") or low.contains("box") or low.contains("unit"):
		unit = "per unit"
	elif low.contains("year") or low.contains("annual"):
		unit = "per year"
	elif low.contains("hour"):
		unit = "per hour"
	var fair := 40.0 * aud
	return {"name": idea.substr(0, 40), "unit": unit, "fair_price": fair,
		"unit_cost": fair * 0.35, "elasticity": 2.0, "weight": 1.0,
		"variable_costs": [
			{"label": "materials & delivery", "amount": fair * 0.20},
			{"label": "labor share", "amount": fair * 0.15}],
		"fixed_costs_wk": [
			{"label": "tools & subscriptions", "amount": 15.0 * aud}]}

## THE LEARNING CURVE (Bonopoly): each 10× of customers ever served takes
## ~11% off the unit serving cost, floored at 65% — scale earns its margin.
static func learning_curve(state: GameState) -> float:
	var served := state.served_total
	if served <= 1:
		return 1.0
	return maxf(1.0 - 0.115 * (log(float(served)) / log(10.0)), 0.65)

## Above fair price the invoice reminds people to leave: retention pain,
## 1.0 at or below fair, rising 0.4 per 100% over fair, capped at 1.6.
static func offers_price_pain(state: GameState) -> float:
	if state.offers.is_empty():
		return 1.0
	var num := 0.0
	var den := 0.0
	var fm := street_fair_mult(state)
	for o in state.offers:
		var od: Dictionary = o
		var price := offer_billed_price(od, fm)   # fair-billed = no pain (ratio 1)
		if price <= 0.0:
			continue
		var fair := maxf(float(od.get("fair_price", price)) * fm, 1.0)
		var wgt := float(od.get("weight", 1.0))
		num += wgt * (price / fair)
		den += wgt
	if den <= 0.0:
		return 1.0
	var ratio := num / den
	if ratio <= 1.0:
		return 1.0
	return 1.0 + minf((ratio - 1.0) * 0.4, 0.6)

## The blended price-demand multiplier adoption feels (1.0 at fair prices).
static func offers_demand_mult(state: GameState) -> float:
	if state.offers.is_empty():
		return -1.0
	var num := 0.0
	var den := 0.0
	var fm := street_fair_mult(state)
	for o in state.offers:
		var od: Dictionary = o
		var wgt := float(od.get("weight", 1.0))
		den += wgt
		var price := offer_billed_price(od, fm)   # fair-billed = fair demand (1.0)
		if price <= 0.0 and bool(od.get("price_set", false)):
			num += wgt * 2.0   # free on purpose: the giveaway cap, not zero
		else:
			num += wgt * (offer_demand(od, price, fm) if price > 0.0 else 0.0)
	return clampf(num / maxf(den, 0.01), 0.0, 3.0) if den > 0.0 else 0.0

## What one week may plausibly spend at this stage — the DM's inputs are
## clamped here so no narration can invent hq money in a garage.
## First guesses about the market — wrong on purpose, corrected by playing.
static func seed_beliefs(state: GameState) -> void:
	var th := state.theta
	var br := _rng(state, SALT_BELIEFS)
	state.beliefs = {
		"tam": float(th.get("tam", 100000.0)) * br.randf_range(0.35, 2.6),
		"lifetime_wk": float(th.get("lifetime_wk", 40.0)) * br.randf_range(0.4, 2.2),
	}

# ════════════════════ THE SPINE'S AGGREGATORS ════════════════════
## Four pure functions the whole game reads through: the budget migration, the
## attention registry, the pre-roll review, and the DM directive block. Every
## one of them merges the nine lanes so no screen ever calls a lane directly.

## THE BUDGET MIGRATION (docs/design/04 §5, docs/design/00-spine.md §8).
## Idempotent by construction — run it at every tick start and after every load.
## The old single `marketing` lever becomes PAID ADS, which inherits both of its
## behaviours (instant reach, the closing-capacity feed), so a mid-run save
## spends identically until the player touches the mix.
static func migrate_budgets(state: GameState) -> void:
	if state.budgets.has("marketing"):
		state.budgets["ads"] = int(state.budgets.get("ads", 0)) + int(state.budgets["marketing"])
		state.budgets.erase("marketing")
	for k in ["ads", "content", "referrals", "outbound", "sales", "care", "rnd", "office"]:
		if not state.budgets.has(k):
			state.budgets[k] = 0

## THE ATTENTION REGISTRY (docs/design/00-spine.md §4). ONE engine-side function
## behind every bang in the game: the binder's tab marks, the garage badge, the
## garage ticker, the threats desk and the pre-roll review card all read this
## list and nothing else. Rows are {desk, key, severity, label}; severity 1 =
## note, 2 = warn, 3 = alarm.
##
## The label is PEDAGOGY, not decoration: it names the problem in the business
## term the player must learn, in ≤40 characters, because the garage ticker
## prints it verbatim with no room to explain itself.
const ATTENTION_DESKS := ["pricing", "the ledger", "the bank", "crew",
	"cap table", "customers", "product", "the street", "vitals", "threats"]

static func attention_items(state: GameState) -> Array:
	var rows: Array = []
	# ── the spine's own rows: the three conditions that predate the registry
	if offers_any_unpriced(state):
		rows.append({"desk": "pricing", "key": "unpriced", "severity": 2,
			"label": "unpriced offer — billing at going rate"})
	var pnl: Dictionary = state.get_meta("pnl", {})
	if not pnl.is_empty() and int(pnl.get("net", 0)) < 0:
		rows.append({"desk": "the ledger", "key": "losing_week", "severity": 2,
			"label": "losing week — burn beat revenue"})
	if state.has_flag("fundraising_open"):
		rows.append({"desk": "cap table", "key": "term_sheets", "severity": 3,
			"label": "term sheets waiting — they expire"})
	# ── the nine lanes, each owning its own predicates
	for lane_rows in [SimCatalog.attention(state), SimLabor.attention(state),
			SimStreet.attention(state), SimFunnel.attention(state),
			SimPipeline.attention(state), SimBank.attention(state),
			SimRoadmap.attention(state), SimBoard.attention(state),
			SimFactory.attention(state)]:
		for r in lane_rows:
			if r is Dictionary:
				rows.append(r)
	# ── ONE order for every consumer: loudest first, then registry order, then
	# the order the lanes spoke. The last key makes the sort total, so two runs
	# of the same state can never disagree about which row the ticker shows.
	var idx := 0
	for r2 in rows:
		(r2 as Dictionary)["_i"] = idx
		idx += 1
	rows.sort_custom(func(a: Dictionary, b: Dictionary) -> bool:
		var sa := int(a.get("severity", 1))
		var sb := int(b.get("severity", 1))
		if sa != sb:
			return sa > sb
		var da := ATTENTION_DESKS.find(String(a.get("desk", "")))
		var db := ATTENTION_DESKS.find(String(b.get("desk", "")))
		if da < 0: da = ATTENTION_DESKS.size()
		if db < 0: db = ATTENTION_DESKS.size()
		if da != db:
			return da < db
		return int(a.get("_i", 0)) < int(b.get("_i", 0)))
	for r3 in rows:
		(r3 as Dictionary).erase("_i")
	return rows

## Every attention row on one desk, highest severity first — the binder asks
## this for a tab's bang, the threats page for its lines.
static func attention_for_desk(state: GameState, desk: String) -> Array:
	var out: Array = []
	for r in attention_items(state):
		if String((r as Dictionary).get("desk", "")) == desk:
			out.append(r)
	return out

## The severity a desk's bang wears: its loudest item, 0 for a quiet desk.
static func attention_severity(state: GameState, desk: String) -> int:
	var worst := 0
	for r in attention_items(state):
		if String((r as Dictionary).get("desk", "")) == desk:
			worst = maxi(worst, int((r as Dictionary).get("severity", 1)))
	return worst

## THE PRE-ROLL REVIEW (docs/design/DECISIONS.md #2). Before ANY dice roll —
## the weekly LOCK IN included — the game shows what is still outstanding, so a
## founder never rolls past an unpriced offer or a repayment cliff without
## having been told. This is the ENGINE half: the list, filtered to what is
## genuinely worth stopping for (warn and above). Zero rows = no card at all.
static func preroll_items(state: GameState) -> Array:
	var out: Array = []
	for r in attention_items(state):
		if int((r as Dictionary).get("severity", 1)) >= 2:
			out.append(r)
	return out

## THE DIRECTIVE BLOCK's subsystem half, in the spine's fixed section order
## (docs/design/00-spine.md §5). Lanes never touch the event generator: they
## return lines, the spine orders them, the composer caps them.
static func lane_directives(state: GameState) -> Array[String]:
	var out: Array[String] = []
	for lane_lines in [SimCatalog.directives(state), SimLabor.directives(state),
			SimStreet.directives(state), SimFunnel.directives(state),
			SimPipeline.directives(state), SimBank.directives(state),
			SimRoadmap.directives(state), SimBoard.directives(state),
			SimFactory.directives(state)]:
		for l in lane_lines:
			out.append(String(l))
	return out

## THE TOKEN BUDGET GUARD (docs/design/00-spine.md §5): the whole DIRECTIVES
## block is hard-capped at 24 lines / 1200 chars. Priority IS the order — the
## runway line is never dropped — and the composer truncates, never the
## subsystems, so no lane can starve another by writing more.
const DIRECTIVE_MAX_LINES := 24
const DIRECTIVE_MAX_CHARS := 1200

static func cap_directives(lines: Array) -> Array[String]:
	var out: Array[String] = []
	var chars := 0
	for l in lines:
		if out.size() >= DIRECTIVE_MAX_LINES:
			break
		var s := String(l)
		if chars + s.length() + 1 > DIRECTIVE_MAX_CHARS and not out.is_empty():
			break
		out.append(s)
		chars += s.length() + 1
	return out

static func era_spend_cap(era: String) -> int:
	return int({"garage": 6_000, "coworking": 25_000, "office": 80_000,
		"floor": 300_000, "hq": 1_200_000}.get(era, 6_000))

static func runway_weeks(state: GameState) -> int:
	var th := state.theta
	var arpu_r := offers_arpu(state)
	if arpu_r < 0.0:
		arpu_r = float(th.get("arpu_wk", 4.0)) * state.price_mult
	var revenue := float(state.traction) * arpu_r
	var payroll := 0
	for e in state.employees:
		payroll += int(e.get("salary", 0))
	var lever_sum := 0
	for k in state.budgets:
		lever_sum += int(state.budgets[k])
	var burn := float(int(GameState.ERA_RENT.get(state.era, 150)) + payroll + 50) \
			+ float(state.marketing_budget + lever_sum) - revenue
	if burn <= 0.0:
		return 999
	return maxi(int(floor(float(state.cash) / burn)), 0)

static func health_band(state: GameState) -> String:
	var rw := runway_weeks(state)
	if state.cash < 0:
		return "CRITICAL — in the red"
	if rw <= 4:
		return "CRITICAL — %d weeks" % rw
	if rw <= 10:
		return "WARNING — %d weeks" % rw
	return "STABLE — %d weeks" % mini(rw, 260)

## Everything the DM should know that a founder would feel. Fed every call.
static func signals(state: GameState) -> Dictionary:
	var th := state.theta
	var A := float(state.traction)
	var N := float(th.get("tam", 100_000.0))
	var phase := "pre-launch"
	if A > 0.5 * N:
		phase = "saturating"
	elif A > 0.1 * N:
		phase = "scaling"
	elif A > 0.0:
		phase = "early adopters"
	var conds: Array = []
	for s in state.statuses:
		conds.append("%s (%dwk)" % [(s as Dictionary).get("name", ""), (s as Dictionary).get("weeks_left", 0)])
	var clocks_out: Array = []
	for c in state.clocks:
		clocks_out.append("in %d wks: %s" % [(c as Dictionary).get("weeks_left", 0), (c as Dictionary).get("consequence", "")])
	return {
		"health": health_band(state), "runway_weeks": runway_weeks(state),
		"market_phase": phase, "market_penetration_pct": snappedf(A / N * 100.0, 0.1),
		"market_mood": snappedf(state.market_trend, 0.01),
		"price_mult": state.price_mult, "marketing_weekly": state.marketing_budget,
		"tech_debt": int(state.tech_debt), "fatigue": int(state.fatigue),
		"exhaustion": state.exhaustion, "statuses": conds, "clocks": clocks_out,
		"loan_owed": state.loan_principal, "valuation": valuation(state),
		"rivals": state.rivals.map(func(r): return "%s (%s)" % [
			(r as Dictionary).get("name", "?"), _fuzz(float((r as Dictionary).get("strength", 20.0)))]),
	}

static func _fuzz(strength: float) -> String:
	if strength >= 70.0:
		return "dominant"
	if strength >= 45.0:
		return "strong"
	if strength >= 25.0:
		return "scrappy"
	return "struggling"
