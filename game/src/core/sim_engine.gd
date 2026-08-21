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
}

# ─────────────────────── seeded per-subsystem randomness ─────────────────────
static func _rng(state: GameState, salt: int) -> RandomNumberGenerator:
	var r := RandomNumberGenerator.new()
	r.seed = hash(str(state.sim_seed) + ":" + str(state.week) + ":" + str(salt))
	return r

# ───────────────────────────── lookup curves (BSL) ───────────────────────────
## Janoschek falling curve: 1.0 at x=0 down to `floor_v` as x→∞, knee at x_ref.
static func jano_down(x: float, x_ref: float, floor_v: float = 0.25) -> float:
	if x <= 0.0:
		return 1.0
	var k := 0.6931 / maxf(x_ref, 0.001)     # ln2: halfway to floor at x_ref
	return floor_v + (1.0 - floor_v) * exp(-k * x)

# ═══════════════════════════ THE WEEKLY TICK ═══════════════════════════
## The hostile world, in order. Returns the week's REPORT: every delta with its
## why, so the journal can print receipts the DM never invented.
static func weekly_tick(state: GameState) -> Dictionary:
	var rep := {"lines": [], "fired_clocks": [], "expired": [], "events": []}
	var th := state.theta
	if th.is_empty():
		th = default_theta(state.biz_what, state.biz_who)
		state.theta = th

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

	# 3 ── the hiring pipeline advances: cohort 0 onboards → cohort 1 → productive
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

	# 4 ── fatigue and morale drift (the slow tax)
	var crunching := has_status(state, "crunch")
	var target_fatigue := 65.0 if crunching else 20.0
	state.fatigue += (target_fatigue - state.fatigue) / 4.0
	# morale drifts toward a lived-in 50 (up when battered, down when coasting);
	# statuses, red ink and events push around that baseline
	var morale_wk := (50.0 - float(state.morale)) / 6.0
	for s2 in state.statuses:
		morale_wk += float(STATUS.get(String((s2 as Dictionary).get("name", "")), {}).get("morale_wk", 0.0))
	if state.cash < 0:
		morale_wk -= 3.0
	state.morale = clampi(int(state.morale + morale_wk), 0, 100)
	# burnout cliff: below 30 someone may walk — best people first
	if state.morale < 30 and state.employees.size() > 0:
		var r4 := _rng(state, 4)
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
	var r5 := _rng(state, 5)
	if state.tech_debt > 40.0 and r5.randf() < (state.tech_debt - 40.0) / 250.0:
		add_status(state, "outage_fallout", 2)
		rep["events"].append("OUTAGE — the debt collected (debt %d)" % int(state.tech_debt))

	# 6 ── rivals ratchet up; occasional launch
	var r6 := _rng(state, 6)
	for rv in state.rivals:
		var rd: Dictionary = rv
		rd["strength"] = minf(float(rd.get("strength", 20.0)) + r6.randf_range(0.0, 1.2), 95.0)
		rd["weeks_since_move"] = int(rd.get("weeks_since_move", 0)) + 1
		if int(rd["weeks_since_move"]) >= 5 and r6.randf() < 0.4:
			rd["weeks_since_move"] = 0
			rd["strength"] = minf(float(rd["strength"]) + 4.0, 95.0)
			rep["events"].append("%s made a move — %s" % [rd.get("name", "a rival"),
				String(rd.get("tactics", ["shipped something loud"])[r6.randi() % (rd.get("tactics", ["x"]) as Array).size()])])
	var pressure := 0.0
	for rv2 in state.rivals:
		pressure += float((rv2 as Dictionary).get("strength", 0.0))
	pressure = minf(pressure / maxf(float(state.rivals.size()), 1.0) / 100.0 * 0.5, 0.45)

	# 7 ── market mood random walk
	var r7 := _rng(state, 7)
	state.market_trend = clampf(state.market_trend + r7.randf_range(-1.0, 1.0) * float(th.trend_vol), 0.5, 1.5)

	# 8 ── adoption and churn (Bass + quality residence)
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
	var b_mk := float(int(bud.get("marketing", 0)) + state.marketing_budget)
	var b_sales := float(bud.get("sales", 0))
	var b_care := float(bud.get("care", 0))
	var b_rnd := float(bud.get("rnd", 0))
	var mk_budget := b_mk
	var mk_mult := 1.0 + 1.4 * (1.0 - exp(-mk_budget / float(th.cac_sat)))
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
	var residence := float(th.lifetime_wk) * (0.4 + float(state.product) / 100.0 * 1.2)
	# customer care keeps people: churn eases toward −30% as care approaches ~$3k/wk
	var care_mult := 1.0 - 0.30 * (1.0 - exp(-b_care / 1500.0))
	var churn := A / maxf(residence, 2.0) * float(th.churn_mult) * status_churn * care_mult
	var net := int(round(adds - churn))
	state.traction = maxi(state.traction + net, 0)
	rep["adds"] = int(round(adds))
	rep["churn"] = int(round(churn))
	if adds >= 1.0:
		rep["lines"].append("+%d customers (organic %d · word of mouth %d)" % [int(round(adds)), int(round(p_eff * P * price_demand)), int(round(wom * price_demand))])
	if churn >= 1.0:
		rep["lines"].append("−%d churned (lifetime %d wks at v0.%d)" % [int(round(churn)), int(round(residence)), state.product])

	# 9 ── money: revenue, burn, loan
	var revenue := float(state.traction) * float(th.arpu_wk) * state.price_mult * status_arpu
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
		if _rng(state, 77).randf() < quality_gain - float(whole):
			whole += 1
		if whole > 0:
			state.product = mini(state.product + whole, 100)
			rep["lines"].append("R&D shipped: product v0.%d" % state.product)
		state.tech_debt = maxf(state.tech_debt - b_rnd / 1500.0, 0.0)
	var burn := int((float(rent + payroll + infra) + mk_budget + b_sales + b_care + b_rnd) * float(th.burn_mult))
	state.cash += int(round(revenue)) - burn
	if state.get_meta("prev_revenue", 0.0) > 1.0:
		state.last_growth = clampf((revenue - float(state.get_meta("prev_revenue"))) / float(state.get_meta("prev_revenue")), -0.5, 0.5)
	state.set_meta("prev_revenue", revenue)
	rep["revenue"] = int(round(revenue))
	rep["burn"] = burn
	var lever_txt := ""
	if b_sales + b_care + b_rnd > 0.0:
		lever_txt = " · sales %d · care %d · rnd %d" % [int(b_sales), int(b_care), int(b_rnd)]
	rep["lines"].append("$%d in · $%d out (rent %d · payroll %d · infra %d · marketing %d%s)" % [
		int(round(revenue)), burn, rent, payroll, infra, int(mk_budget), lever_txt])
	# ── UNIT ECONOMICS, computed honestly every week (the simulator SHOWS its
	# math): CAC from what acquisition actually cost / who actually arrived;
	# LTV from residence × margin-per-week; payback in weeks.
	var arpu := float(th.arpu_wk) * state.price_mult * status_arpu
	var new_adds := maxf(adds, 0.0)
	rep["cac"] = int(round((b_mk + b_sales) / new_adds)) if new_adds >= 0.5 and (b_mk + b_sales) > 0.0 else 0
	rep["ltv"] = int(round(residence * arpu))
	rep["payback_wk"] = int(ceil(float(rep["cac"]) / maxf(arpu, 0.01))) if int(rep["cac"]) > 0 else 0
	state.set_meta("unit_econ", {"arpu": arpu, "cac": rep["cac"], "ltv": rep["ltv"],
		"payback_wk": rep["payback_wk"], "residence": int(residence)})
	if state.loan_principal > 0:
		var interest := int(ceil(float(state.loan_principal) * 0.18))
		state.loan_principal += interest
		rep["lines"].append("the loan compounds: +$%d interest (owe $%d)" % [interest, state.loan_principal])
		if state.cash > 2000:
			var pay := mini(state.cash - 1500, state.loan_principal)
			state.cash -= pay
			state.loan_principal -= pay
			rep["lines"].append("auto-repaid $%d of the loan" % pay)

	# 9b ── the founder's working assumptions converge toward the truth.
	# Rate: analytics tooling, real customers, and R&D all teach.
	if state.beliefs.is_empty():
		var br := _rng(state, 88)
		state.beliefs = {
			"tam": float(th.tam) * br.randf_range(0.35, 2.6),
			"lifetime_wk": float(th.lifetime_wk) * br.randf_range(0.4, 2.2),
		}
	else:
		var k := clampf(0.02 + 0.05 * float(state.analytics_level)
				+ 0.003 * float(state.traction) + b_rnd / 40000.0, 0.0, 0.30)
		state.beliefs["tam"] = float(state.beliefs["tam"]) + (float(th.tam) - float(state.beliefs["tam"])) * k
		state.beliefs["lifetime_wk"] = float(state.beliefs["lifetime_wk"]) \
				+ (float(th.lifetime_wk) - float(state.beliefs["lifetime_wk"])) * k

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

## Advantage/disadvantage from STATE — items, hires, statuses, exhaustion.
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
	var has_a := adv.size() > 0
	var has_d := dis.size() > 0
	return {"advantage": has_a and not has_d, "disadvantage": has_d and not has_a,
			"adv_reasons": adv, "dis_reasons": dis}

## The full roll: 1d20, or 2d20 keep best/worst under advantage/disadvantage.
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
	var arr := float(state.traction) * float(state.theta.get("arpu_wk", 4.0)) * state.price_mult * 52.0
	var growth := clampf(float(state.last_growth), 0.0, 0.4)
	var mult := 8.0 + minf(12.0, growth * 60.0)
	return maxi(state.cash, int(arr * mult * float(state.theta.get("funding_mult", 1.0))))

## Three offers against fair price; desperation prices against you.
static func generate_offers(state: GameState, investors: Array) -> Array:
	var pre := valuation(state)
	var r := _rng(state, 9)
	var desperate := state.cash < 0 or runway_weeks(state) <= 4
	var out: Array = []
	for i in 3:
		var inv: Dictionary = investors[i % maxi(investors.size(), 1)] if investors.size() > 0 else {"name": "an angel"}
		var amount := int(float(pre) * r.randf_range(0.05, 0.15))
		var fair := float(amount) / float(pre + amount) * 100.0
		var spread := r.randf_range(1.15, 1.6) * (1.35 if desperate else 1.0)
		out.append({"investor": String(inv.get("name", "?")),
			"amount": maxi(amount, 5_000),
			"equity_pct": snappedf(clampf(fair * spread, 1.0, 45.0), 0.1),
			"fair_pct": snappedf(fair, 0.1),
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
## What one week may plausibly spend at this stage — the DM's inputs are
## clamped here so no narration can invent hq money in a garage.
static func era_spend_cap(era: String) -> int:
	return int({"garage": 6_000, "coworking": 25_000, "office": 80_000,
		"floor": 300_000, "hq": 1_200_000}.get(era, 6_000))

static func runway_weeks(state: GameState) -> int:
	var th := state.theta
	var revenue := float(state.traction) * float(th.get("arpu_wk", 4.0)) * state.price_mult
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
