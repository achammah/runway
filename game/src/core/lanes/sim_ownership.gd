class_name SimOwnership
extends RefCounted
## LANE — THE OWNERSHIP CLUSTER (ESOP, instruments, the raise, recruitment,
## buyout offers). Spec: docs/design/DECISIONS.md (THE OWNERSHIP CLUSTER,
## THE ESOP THREAD, THE OFFER) + docs/design/DAG2.md. W2 L-OWN, filled.
##
## ═══ THE MATH THIS MODULE PINS (tests hold every formula) ═══
##
## VESTING — 208-wk linear with a 52-wk cliff, COMPUTED never stored:
##   vest_frac(weeks_in) = 0 while weeks_in < 52, else min(weeks_in, 208)/208.
## A leaver keeps vested, unvested returns to the pool: the grant row's pct is
## cut to the vested figure and its emp_id gains the "left:" marker — no new
## save keys, both engines' typed rows survive untouched.
##
## CONVERSION (post-money SAFE style, DECISIONS): at a priced event the WHOLE
## unconverted safe/note/bridge stack converts at the round's pre-money:
##   amount_eff = amount                      (safe)
##              = amount × (1 + rate × (wk − signed_wk))   (note/bridge, simple)
##   eff_val    = min(cap when cap>0, pre × (1 − discount) when discount>0)
##                (neither term set → pre itself)
##   pct        = clamp(100 × amount_eff / max(eff_val, 1), 0, 35)
## All stack pcts are slices of the PRE-round company; every existing holder
## (founder, cofounders, pool, prior priced paper) scales by 1 − Σpct/100.
## Then the priced investor takes amount/(pre+amount) through the EXISTING
## seams — SimEngine.apply_round then SimBoard.on_round_closed (pool shuffle,
## covenant, seats) — and prior instrument pcts scale by the same
## pool_keep × inv_keep those seams applied to the founder.
##
## THE POOL MIRROR — esop.pool_pct is the source of truth for everything this
## lane reads (grants, waterfall, desks); the legacy `option_pool_pct` field
## stays alive as a MIRROR because SimBoard.on_round_closed (its only other
## writer) does the pool shuffle there. tick_pre absorbs any divergence INTO
## esop (one-way, idempotent — the legacy write is always the newer truth
## because this lane re-mirrors after every one of its own ops).
##
## THE RAISE — interest_score is a deterministic weekly read:
##   score = clamp(8×era + 10×log10(1+traction) + hype/4 + max(growth,0)×30
##           + (board: 6 + 2×goodwill − 2×strikes) + (boom +8 | winter −10)
##           + (active raise: +4), 0, 100)
## Inbound knocks roll p = score/100 × 0.35 on SALT_OWN_INBOUND. A
## conversation matures into terms when the DATA ROOM reads ≥3 of 5 binder
## pages healthy (growth>0, runway≥10, net≥0, product≥50, traction≥20);
## the failing pages become the investor's named doubts. Terms are generated
## on SALT_OWN_TERMS inside engine bands and the cycle's shock multipliers.
## While raise_state.active the founder-time tax is REAL: the field carries
## 0.30 (the desk prints it) and the lane re-arms the `raise_distraction`
## status every week (catalog entry = coordinator package; add_status
## refuses unknown names, so the arming is a safe no-op until it lands).
##
## RECRUITMENT — roles/candidates/offers_out are the source of truth; the
## legacy labor fields (open_roles/applicants) migrate in at every tick start
## and labor's own arrival flow keeps feeding candidates through that sweep.
## Arrivals (SALT_RECRUIT_ARRIVALS): lam = clamp(advert/30, 0, 4)
##   × (0.5 + SimLabor.attractiveness at the band mid) × 0.6-if-senior,
##   drawn binomial(8, lam/8). Profiles (SALT_RECRUIT_PROFILE): mercenary 55%.
## Acceptance (SALT_RECRUIT_ACCEPT), the composer's own curve:
##   odds = clamp(60 + (cash/ask − 1)×216×w_cash + options×20×w_opt
##          + hype/10 + (morale−50)/10, 5, 95)
##   mercenary w_cash 1.0 / w_opt 1.0 · missionary w_cash 0.5 / w_opt 2.0.
## A declined offer rolls the rival counter (SALT_RECRUIT_COUNTER): lost to a
## counter-offer, or stays interviewed with the ask hardened +5%; either way
## the market hears (hype −1).
##
## BUYOUT OFFERS — this lane EXTENDS the board's M&A courtship, never forks
## it: when SimBoard's ladder writes state.mna, tick_post DRESSES it into the
## structured buyout_offer (SALT_OWN_BUYOUT: cash/stock/earnout split by the
## offer's why, lockups, retention, the fishy flags COMPUTED as fields).
## When the board ladder stayed quiet, this lane's own arrival roll
## (p = 0.02 + 0.0004×traction + 0.0006×hype + 0.01×era, cap 10%) writes BOTH
## records so the board's lapse/cooldown machinery owns the clock either way.
## THE WATERFALL is a pure function: debts first (SimBank.debt_total), then
## prefs-or-convert per instrument (take max of amount×prefs vs
## pct/100 × (price − debts) — the mockup's own arithmetic), unconverted
## safes/notes join that loop at 1× vs conversion-at-cap, then the remainder
## splits pro-rata across founder, cofounders, converted paper and the
## VESTED ESOP (the unallocated pool is cancelled at exit — it simply leaves
## the denominator). Resolution: accept (two-tap desk-side, waterfall runs,
## the run ends through the existing exit_value/acquired_exit seam),
## negotiate (ONE counter, the world reprices once), decline (cooldown, the
## street hears). The momentary tab follows state.buyout_offer (binder sync =
## coordinator package); a resolved offer files into the run log.
##
## The spine calls, in tick order (docs/design/HOOKS.md):
##   tick_pre   tick §9 — migrations, leaver vesting, the founder-time drag,
##              recruitment (offers resolve → arrivals → patience)
##   tick_money the money section — writes ONLY m["recruit_ads"] (Σ adverts).
##              ESOP is NON-CASH and never enters the identity; raise wires
##              are one-shot event cash inside op_sign_instrument.
##   tick_post  after the record — interest score, inbound knocks, terms,
##              and the buyout dressing/arrival reading the finished week
##
## SALTS (docs/design/00-spine.md §3), draw order fixed per week:
## SALT_OWN_INBOUND (120), SALT_OWN_TERMS (121), SALT_OWN_BUYOUT (122),
## SALT_RECRUIT_ARRIVALS (150), SALT_RECRUIT_PROFILE (151),
## SALT_RECRUIT_ACCEPT (152), SALT_RECRUIT_COUNTER (153).
##
## TWIN LAW: this file and unity/Assets/Scripts/Core/Lanes/SimOwnership.cs
## carry the same logic in the same order (behavioural parity; the engines
## never share PRNG bytes).

# ─────────────────────────── the lane's constants ────────────────────────────
const VEST_WEEKS := 208            ## 4-year vest at game scale
const CLIFF_WEEKS := 52            ## 1-year cliff
const RADAR_CAP := 5               ## live radar entries, most
const KNOCK_P_MAX := 0.35          ## knock chance at interest 100
const CONVERT_PCT_CAP := 35.0      ## one instrument can never convert past this
const TERMS_EXPIRE_WKS := 3        ## a term sheet's shelf life
const OFFER_OUT_WKS := 2           ## a comp offer's shelf life
const CANDIDATE_PATIENCE := 5      ## weeks before a waiting candidate walks
const FOUNDER_TIME_TAX := 0.30     ## the raise eats ~30% of the week (display + drag)
const RAISE_STATUS := "raise_distraction"   ## catalog entry = coordinator package
const MERCENARY_P := 0.55          ## profile split
const SENIOR_WORDS: Array[String] = ["lead", "senior", "head", "chief", "manager", "principal"]

# ═══════════════════════════ small hands ═════════════════════════════════════

## The lane's money hand — receipts, cards and the desk read one format.
static func money(n: int) -> String:
	var s := str(absi(n))
	var out := ""
	while s.length() > 3:
		out = "," + s.substr(s.length() - 3) + out
		s = s.substr(0, s.length() - 3)
	return ("-" if n < 0 else "") + s + out

static func money_short(n: int) -> String:
	var v := absf(float(n))
	var sign_s := "-" if n < 0 else ""
	if v >= 1_000_000_000.0:
		return "%s$%.1fB" % [sign_s, v / 1_000_000_000.0]
	if v >= 1_000_000.0:
		return "%s$%.1fM" % [sign_s, v / 1_000_000.0]
	if v >= 1_000.0:
		return "%s$%.0fk" % [sign_s, v / 1_000.0]
	return "%s$%d" % [sign_s, int(v)]

## THE GRANT KEY: an employee's slug — "June Park" → "june_park". Grants and
## the team desk's getter meet on this string, never on the display name.
static func emp_slug(name_v: String) -> String:
	return name_v.strip_edges().to_lower().replace(" ", "_")

static func _round10(v: float) -> int:
	return int(round(v / 10.0)) * 10

# ═══════════════════════════ THE MIGRATIONS ══════════════════════════════════
## One-way, idempotent, run at every tick start (the migrate_budgets law).

static func migrate_ownership(state: GameState) -> void:
	# 1 · ESOP: seed from the legacy field once, then absorb the legacy
	# mirror whenever the board's round-close seam moved it.
	if state.esop.is_empty() and state.option_pool_pct > 0.0:
		state.esop = {"pool_pct": state.option_pool_pct, "granted": []}
	if not state.esop.is_empty():
		if not state.esop.has("granted"):
			state.esop["granted"] = []
		# The mirror rule, both directions: an esop built without the legacy
		# field (fixtures, probes) mirrors OUT; a legacy round-close write
		# (the only other writer) is absorbed IN. A live pool can never reach
		# exactly zero through the shuffle, so the zero test is unambiguous.
		if state.option_pool_pct <= 0.0001 and float(state.esop.get("pool_pct", 0.0)) > 0.0:
			state.option_pool_pct = float(state.esop.get("pool_pct", 0.0))
		elif absf(state.option_pool_pct - float(state.esop.get("pool_pct", 0.0))) > 0.0001:
			state.esop["pool_pct"] = state.option_pool_pct
	# 2 · RECRUITMENT: the legacy labor market feeds in. Draining the legacy
	# lists is the design — roles/candidates become the source of truth and
	# labor's own arrival flow keeps landing here through this sweep.
	if not state.open_roles.is_empty() or not state.applicants.is_empty():
		_ensure_recruitment(state)
		var rec: Dictionary = state.recruitment
		for row in state.open_roles:
			var rd: Dictionary = row
			var seat := String(rd.get("role", "engineer"))
			var mk := SimLabor.market_salary(seat, state.era)
			# The legacy advert was a POSTED WAGE; the new lever is ad spend.
			# Carry the founder's intent through: a loud advert stays loud
			# (40 × ratio², clamped) — posted wages direct search either way.
			var ratio := clampf(float(rd.get("offered_salary", mk)) / maxf(float(mk), 1.0), 0.5, 2.0)
			(rec["roles"] as Array).append({
				"id": "role_%s_%d" % [emp_slug(seat), int(rd.get("opened_week", state.week))],
				"seat": seat,
				"band_lo": _round10(float(mk) * 0.85),
				"band_hi": _round10(float(mk) * 1.25),
				"advert_wk": clampi(int(round(40.0 * ratio * ratio)), 10, 200),
				"opened_wk": int(rd.get("opened_week", state.week)),
			})
		state.open_roles = []
		var r151 := SimEngine.rng_for(state, SimEngine.SALT_RECRUIT_PROFILE)
		var n := 0
		for a in state.applicants:
			var ad: Dictionary = a
			var role := String(ad.get("role", "engineer"))
			(rec["candidates"] as Array).append({
				"id": "cand_%d_m%d" % [state.week, n],
				"role_id": _role_id_for(state, role),
				"name": String(ad.get("name", "someone")),
				"ask": int(ad.get("ask", SimLabor.market_salary(role, state.era))),
				"profile": "mercenary" if r151.randf() < MERCENARY_P else "missionary",
				"skill": clampi(int(ad.get("skill", 3)), 1, 5),
				"stage": "applied",
				"arrived_wk": int(ad.get("applied_week", state.week)),
			})
			n += 1
		state.applicants = []

static func _ensure_recruitment(state: GameState) -> void:
	if state.recruitment.is_empty():
		state.recruitment = {"roles": [], "candidates": [], "offers_out": []}
	for k in ["roles", "candidates", "offers_out"]:
		if not state.recruitment.has(k):
			state.recruitment[k] = []

static func _ensure_raise(state: GameState) -> void:
	if state.raise_state.is_empty():
		state.raise_state = {"stages": [], "interest_score": 0.0, "active": false,
			"founder_time_tax": 0.0}
	if not state.raise_state.has("stages"):
		state.raise_state["stages"] = []

static func _role_id_for(state: GameState, seat: String) -> String:
	if state.recruitment.is_empty():
		return ""
	var want := SimLabor.role_row(seat)
	for r in state.recruitment.get("roles", []):
		if SimLabor.role_row(String((r as Dictionary).get("seat", ""))) == want:
			return String((r as Dictionary).get("id", ""))
	return ""

# ═══════════════════════════ THE ESOP THREAD ═════════════════════════════════

## The vest fraction at `weeks_in` weeks from the grant: the cliff, then linear.
static func vest_frac(weeks_in: int) -> float:
	if weeks_in < CLIFF_WEEKS:
		return 0.0
	return float(mini(weeks_in, VEST_WEEKS)) / float(VEST_WEEKS)

## THE TEAM DESK'S GETTER (L-MONEY reads this for the vesting mini-bar):
## how many points of the company `emp_id` has actually vested by week `wk`.
## A leaver's frozen grant ("left:" marker) is fully theirs — vested was kept.
static func vested_pct(state: GameState, emp_id: String, wk: int) -> float:
	var total := 0.0
	for g in state.esop.get("granted", []):
		var gd: Dictionary = g
		var gid := String(gd.get("emp_id", ""))
		if gid == emp_id:
			total += vest_frac(wk - int(gd.get("vest_start_wk", wk))) * float(gd.get("pct", 0.0))
		elif gid == "left:" + emp_id:
			total += float(gd.get("pct", 0.0))
	return total

## The grant a live employee holds (0 when none) — the desk's row read.
static func granted_pct(state: GameState, emp_id: String) -> float:
	var total := 0.0
	for g in state.esop.get("granted", []):
		if String((g as Dictionary).get("emp_id", "")) == emp_id:
			total += float((g as Dictionary).get("pct", 0.0))
	return total

## Pool space not yet granted (leavers' kept shares stay allocated).
static func pool_free(state: GameState) -> float:
	if state.esop.is_empty():
		return 0.0
	var granted := 0.0
	for g in state.esop.get("granted", []):
		granted += float((g as Dictionary).get("pct", 0.0))
	return maxf(float(state.esop.get("pool_pct", 0.0)) - granted, 0.0)

## Pool birth by founder op. Expansion is the same op with a pool already born.
static func create_pool(state: GameState, pct: float) -> String:
	if state.esop.is_empty():
		state.esop = {"pool_pct": 0.0, "granted": []}
	return expand_pool(state, pct)

## EXPANSION DILUTES EVERY EXISTING HOLDER pro-rata (the cap math the dilution
## story draws): keep = 1 − add/100; pool = pool×keep + add; the sum stays 100.
static func expand_pool(state: GameState, add_pct: float) -> String:
	var add := clampf(add_pct, 0.0, 15.0)
	if add <= 0.0 or state.esop.is_empty():
		return ""
	var keep := 1.0 - add / 100.0
	state.founder_pct = maxf(state.founder_pct * keep, 1.0)
	for cf in state.cofounders:
		var cfd: Dictionary = cf
		cfd["equity_diluted"] = float(cfd.get("equity_diluted", cfd.get("equity", 0.0))) * keep
	for inst in state.instruments:
		var idd: Dictionary = inst
		if float(idd.get("pct", 0.0)) > 0.0:
			idd["pct"] = float(idd["pct"]) * keep
	state.esop["pool_pct"] = clampf(float(state.esop.get("pool_pct", 0.0)) * keep + add, 0.0, 100.0)
	state.option_pool_pct = float(state.esop["pool_pct"])
	var line := "the pool grows +%.1f%% — every holder diluted ×%.3f (the slice came out of everyone)" % [add, keep]
	state.log_action(line)
	return line

## A grant from the pool. Refused (empty string) when the free space is short.
static func grant_options(state: GameState, emp_name: String, pct: float) -> String:
	if pct <= 0.0 or state.esop.is_empty():
		return ""
	if pool_free(state) + 0.0001 < pct:
		return ""
	(state.esop["granted"] as Array).append({"emp_id": emp_slug(emp_name),
		"pct": snappedf(pct, 0.01), "vest_start_wk": state.week})
	return "%s granted %.2f%% (%d-wk vest, %d-wk cliff)" % [emp_name, pct, VEST_WEEKS, CLIFF_WEEKS]

## THE LEAVER RULE, hooked through the tick (never by editing sim_labor): a
## grant whose person is no longer on the books crystallizes — vested kept
## (the row stays, marked "left:"), unvested returns to the pool's free space.
static func _crystallize_leavers(state: GameState, rep: Dictionary) -> void:
	if state.esop.is_empty():
		return
	var present := {}
	for e in state.employees:
		present[emp_slug(String((e as Dictionary).get("name", "")))] = true
	for h in state.pipeline:
		present[emp_slug(String((h as Dictionary).get("name", "")))] = true
	for g in state.esop.get("granted", []):
		var gd: Dictionary = g
		var gid := String(gd.get("emp_id", ""))
		if gid.begins_with("left:") or present.has(gid):
			continue
		var vested := vest_frac(state.week - int(gd.get("vest_start_wk", state.week))) \
			* float(gd.get("pct", 0.0))
		var returned := float(gd.get("pct", 0.0)) - vested
		gd["pct"] = snappedf(vested, 0.001)
		gd["emp_id"] = "left:" + gid
		if returned > 0.0005:
			rep["lines"].append("%s left — %.2f%% unvested returned to the pool, %.2f%% vested kept"
				% [gid.replace("_", " "), returned, vested])

# ═══════════════════════════ INSTRUMENTS ═════════════════════════════════════

## What a note is worth today: simple interest from signing (safes carry none).
static func amount_due(inst: Dictionary, wk: int) -> int:
	var amt := float(inst.get("amount", 0))
	var kind := String(inst.get("kind", "safe"))
	if kind == "note" or kind == "bridge":
		amt *= 1.0 + float(inst.get("rate", 0.0)) * float(maxi(wk - int(inst.get("signed_wk", wk)), 0))
	return int(round(amt))

## The conversion slice ONE instrument takes at a priced round's pre-money.
## The header's formula, verbatim — tests pin the cap side and the discount side.
static func convert_pct_at(inst: Dictionary, round_pre: float, wk: int) -> float:
	var amt := float(amount_due(inst, wk))
	var eff := maxf(round_pre, 1.0)
	var disc := float(inst.get("discount", 0.0))
	if disc > 0.0:
		eff = round_pre * (1.0 - disc)
	var cap := float(inst.get("cap", 0))
	if cap > 0.0:
		eff = minf(eff, cap)
	return clampf(100.0 * amt / maxf(eff, 1.0), 0.0, CONVERT_PCT_CAP)

## True while the paper has not converted (pct is written at conversion).
static func _unconverted(inst: Dictionary) -> bool:
	var kind := String(inst.get("kind", "safe"))
	return (kind == "safe" or kind == "note" or kind == "bridge") \
		and float(inst.get("pct", 0.0)) <= 0.0

## THE SAFE-STACK WARNING's number: total deferred dilution if the whole
## unconverted stack converts at `round_pre` today. Pure, UI-ready.
static func stack_dilution_at(state: GameState, round_pre: float) -> float:
	var total := 0.0
	for inst in state.instruments:
		if _unconverted(inst as Dictionary):
			total += convert_pct_at(inst as Dictionary, round_pre, state.week)
	return total

## A matured, unconverted note is a repayment demand (attention reads this).
static func matured_notes(state: GameState) -> Array:
	var out: Array = []
	for inst in state.instruments:
		var idd: Dictionary = inst
		if _unconverted(idd) and int(idd.get("maturity_wk", 0)) > 0 \
				and state.week >= int(idd.get("maturity_wk", 0)):
			out.append(idd)
	return out

# ═══════════════════════════ THE RAISE ═══════════════════════════════════════

## The deterministic weekly interest read — the header's formula, verbatim.
static func interest_score_calc(state: GameState) -> float:
	var score := 8.0 * float(state.era_index())
	score += 10.0 * (log(1.0 + float(state.traction)) / log(10.0))
	score += float(state.hype) / 4.0
	score += maxf(float(state.last_growth), 0.0) * 30.0
	if not state.board.is_empty():
		score += 6.0 + 2.0 * float(int(state.board.get("goodwill", 0))) \
			- 2.0 * float(int(state.board.get("strikes", 0)))
	if state.macro_season == "boom":
		score += 8.0
	elif state.macro_season == "winter":
		score -= 10.0
	if bool(state.raise_state.get("active", false)):
		score += 4.0
	return snappedf(clampf(score, 0.0, 100.0), 0.1)

## THE DATA ROOM reads the founder's own binder: five pages, pass or doubt.
## ≥3 healthy = terms come; the failing pages are the investor's named doubts.
static func data_room(state: GameState) -> Dictionary:
	var doubts: Array = []
	var score := 0
	if state.last_growth > 0.0:
		score += 1
	else:
		doubts.append("the growth page is flat")
	if SimEngine.runway_weeks(state) >= 10:
		score += 1
	else:
		doubts.append("the runway page is short")
	var pnl: Dictionary = state.get_meta("pnl", {})
	if not pnl.is_empty() and int(pnl.get("net", -1)) >= 0:
		score += 1
	else:
		doubts.append("the margin page bleeds")
	if state.product >= 50:
		score += 1
	else:
		doubts.append("the product page is thin")
	if state.traction >= 20:
		score += 1
	else:
		doubts.append("the customer page is quiet")
	return {"score": score, "doubts": doubts}

static func _stage_by_name(state: GameState, name_v: String) -> Dictionary:
	for st in state.raise_state.get("stages", []):
		if String((st as Dictionary).get("name", "")) == name_v:
			return st
	return {}

static func _stages_in(state: GameState, stage: String) -> Array:
	var out: Array = []
	for st in state.raise_state.get("stages", []):
		if String((st as Dictionary).get("stage", "")) == stage:
			out.append(st)
	return out

## True while a signed no-shop freezes the other term sheets.
static func no_shop_until(state: GameState) -> int:
	var until := 0
	for st in state.raise_state.get("stages", []):
		until = maxi(until, int((st as Dictionary).get("no_shop_until", 0)))
	return until

## Outbound targets: world-bible investors not yet in the pipeline.
static func outbound_targets(state: GameState) -> Array:
	var seen := {}
	for st in state.raise_state.get("stages", []):
		seen[String((st as Dictionary).get("name", ""))] = true
	var out: Array = []
	for inv in state.investors:
		var nm := String((inv as Dictionary).get("name", ""))
		if nm != "" and not seen.has(nm):
			out.append(nm)
	return out

## §tick_post — the raise's week: score, knocks, conversations ripening into
## terms, terms expiring. One SALT_OWN_INBOUND stream, one SALT_OWN_TERMS
## stream, drawn in this order forever.
static func _raise_weekly(state: GameState, rep: Dictionary) -> void:
	_ensure_raise(state)
	var rs: Dictionary = state.raise_state
	rs["interest_score"] = interest_score_calc(state)
	rs["founder_time_tax"] = FOUNDER_TIME_TAX if bool(rs.get("active", false)) else 0.0
	var r120 := SimEngine.rng_for(state, SimEngine.SALT_OWN_INBOUND)
	# 1 · the inbound knock — to traction, not to wishes
	if _stages_in(state, "radar").size() < RADAR_CAP \
			and r120.randf() < float(rs["interest_score"]) / 100.0 * KNOCK_P_MAX:
		var nm := ""
		var pool := outbound_targets(state)
		if not pool.is_empty():
			nm = String(pool[r120.randi() % pool.size()])
		else:
			var made: Array = ["Halden Ventures", "R. Osei", "Cormorant Capital",
				"the fund that emailed twice", "Bright & Motte", "a syndicate off the board's list"]
			nm = String(made[r120.randi() % made.size()])
		if _stage_by_name(state, nm).is_empty():
			(rs["stages"] as Array).append({"name": nm, "stage": "radar",
				"inbound": true, "arrived_wk": state.week})
			rep["lines"].append("%s knocked — the growth got noticed (ON THE RADAR)" % nm)
	# 2 · conversations ripen (≥2 weeks in) — the data room decides
	var r121 := SimEngine.rng_for(state, SimEngine.SALT_OWN_TERMS)
	for st in _stages_in(state, "conversations"):
		var sd: Dictionary = st
		if state.week - int(sd.get("asked_wk", state.week)) < 2:
			continue
		var room := data_room(state)
		if int(room.get("score", 0)) >= 3:
			sd["stage"] = "terms"
			sd["terms"] = _draft_terms(state, sd, r121)
			sd["doubt"] = ""
			rep["events"].append("%s put TERMS ON THE TABLE — %s, expires wk %d" % [
				String(sd.get("name", "an investor")),
				_terms_headline(sd["terms"]), int((sd["terms"] as Dictionary).get("expires_wk", 0))])
		else:
			var doubts: Array = room.get("doubts", [])
			sd["doubt"] = String(doubts[0]) if not doubts.is_empty() else ""
			if state.week - int(sd.get("asked_wk", state.week)) >= 6:
				sd["stage"] = "passed"
				rep["lines"].append("%s passed — %s" % [String(sd.get("name", "an investor")),
					String(sd.get("doubt", "the numbers didn't hold"))])
	# 3 · terms expire — walking away happens to you too
	for st2 in _stages_in(state, "terms"):
		var sd2: Dictionary = st2
		var t: Dictionary = sd2.get("terms", {})
		if int(t.get("expires_wk", 1 << 30)) < state.week:
			sd2["stage"] = "passed"
			rs["interest_score"] = maxf(float(rs["interest_score"]) - 5.0, 0.0)
			rep["lines"].append("%s pulled their terms — sheets have shelf lives"
				% String(sd2.get("name", "an investor")))
	# 4 · the passed pile stays small
	var stages: Array = rs["stages"]
	var kept: Array = []
	for st3 in stages:
		var sd3: Dictionary = st3
		if String(sd3.get("stage", "")) == "passed" \
				and state.week - int(sd3.get("arrived_wk", 0)) > 12:
			continue
		kept.append(sd3)
	rs["stages"] = kept

## The world writes a term sheet inside engine bands (SALT_OWN_TERMS stream,
## passed in so the weekly draw order stays fixed).
static func _draft_terms(state: GameState, entry: Dictionary, r: RandomNumberGenerator) -> Dictionary:
	var val := float(SimEngine.valuation(state))
	var era := state.era_index()
	var warm := SimEngine.warmth_pct(state)
	var desperate := state.cash < 0 or SimEngine.runway_weeks(state) <= 4
	var kind := "priced"
	if SimEngine.runway_weeks(state) < 6 and not state.instruments.is_empty():
		kind = "bridge"
	elif era <= 1:
		kind = "safe" if r.randf() < 0.65 else "note"
	var t := {"kind": kind, "expires_wk": state.week + TERMS_EXPIRE_WKS}
	match kind:
		"safe", "note":
			t["amount"] = maxi(int(val * r.randf_range(0.08, 0.18) * SimEngine.shock_amt_mult(state)), 25_000)
			t["cap"] = int(val * r.randf_range(1.2, 2.0) * SimEngine.shock_val_mult(state))
			t["discount"] = snappedf(r.randf_range(0.15, 0.25), 0.01)
			if kind == "note":
				t["rate"] = snappedf(r.randf_range(0.002, 0.004), 0.0005)
				t["maturity_wk"] = state.week + 52
		"bridge":
			t["amount"] = maxi(int(val * r.randf_range(0.05, 0.10)), 15_000)
			t["rate"] = snappedf(r.randf_range(0.003, 0.005), 0.0005)
			t["maturity_wk"] = state.week + 26
			t["discount"] = 0.2
			t["cap"] = int(val * 1.2)
		"priced":
			var pre := val * r.randf_range(0.9, 1.3) * SimEngine.shock_val_mult(state)
			pre *= 1.0 + warm / 100.0 * 0.5
			if desperate:
				pre *= 0.8
			t["valuation"] = int(pre)
			t["amount"] = maxi(int(pre * r.randf_range(0.15, 0.25) * SimEngine.shock_amt_mult(state)), 50_000)
			t["pct"] = snappedf(100.0 * float(t["amount"]) / maxf(pre + float(t["amount"]), 1.0), 0.1)
			t["prefs"] = 1.0
			t["participating"] = r.randf() < 0.10   # flagged predatory on the desk
			t["protective"] = true
			t["drag_threshold"] = 60.0
			t["board_seat"] = true
			t["no_shop_wks"] = 4
			t["pool_topup_pct"] = SimBoard.pool_ask_pct(state)
	return t

static func _terms_headline(t: Dictionary) -> String:
	match String(t.get("kind", "")):
		"safe":
			return "SAFE $%s · cap %s" % [money_short(int(t.get("amount", 0))).lstrip("$"),
				money_short(int(t.get("cap", 0)))]
		"note":
			return "note $%s · matures wk %d" % [money_short(int(t.get("amount", 0))).lstrip("$"),
				int(t.get("maturity_wk", 0))]
		"bridge":
			return "bridge $%s from the insiders" % money_short(int(t.get("amount", 0))).lstrip("$")
	return "priced: %s at %s pre" % [money_short(int(t.get("amount", 0))),
		money_short(int(t.get("valuation", 0)))]

## OP pitch_investor — a written move or a desk press: a radar target (or a
## fresh outbound name) moves into CONVERSATIONS; the week is spent.
static func op_pitch_investor(state: GameState, name_v: String = "") -> String:
	_ensure_raise(state)
	var rs: Dictionary = state.raise_state
	var target: Dictionary = {}
	if name_v != "":
		for st in rs["stages"]:
			if String((st as Dictionary).get("name", "")).to_lower().contains(name_v.to_lower()):
				target = st
				break
	if target.is_empty():
		for st2 in _stages_in(state, "radar"):
			target = st2
			break
	if target.is_empty():
		var nm := name_v
		if nm == "":
			var pool := outbound_targets(state)
			if pool.is_empty():
				return ""
			nm = String(pool[0])
		target = {"name": nm, "stage": "radar", "inbound": false, "arrived_wk": state.week}
		(rs["stages"] as Array).append(target)
	target["stage"] = "conversations"
	target["asked_wk"] = state.week
	rs["active"] = true
	rs["founder_time_tax"] = FOUNDER_TIME_TAX
	state.fatigue = minf(state.fatigue + 6.0, 100.0)
	var line := "pitched %s — they asked for real numbers (the data room reads YOUR binder)" \
		% String(target.get("name", "an investor"))
	state.log_action(line)
	return line

## OP sign_instrument — the signature. SAFEs/notes/bridges wire one-shot event
## cash; a PRICED round converts the stack, then rides the existing seams
## (apply_round → on_round_closed) so covenants, seats and the pool shuffle
## stay the board lane's law. Returns "" when nothing matched or a no-shop holds.
static func op_sign_instrument(state: GameState, name_v: String = "") -> String:
	_ensure_raise(state)
	var entries := _stages_in(state, "terms")
	if entries.is_empty():
		return ""
	var entry: Dictionary = {}
	if name_v != "":
		for st in entries:
			if String((st as Dictionary).get("name", "")).to_lower().contains(name_v.to_lower()):
				entry = st
				break
	if entry.is_empty():
		entry = entries[0]
	if no_shop_until(state) > state.week:
		return ""
	var t: Dictionary = entry.get("terms", {})
	var holder := String(entry.get("name", "an investor"))
	var kind := String(t.get("kind", "safe"))
	var amount := int(t.get("amount", 0))
	var line := ""
	if kind == "priced":
		line = _sign_priced(state, holder, t)
	else:
		state.cash += amount   # ONE-SHOT EVENT CASH — never a weekly lane
		state.instruments.append({"kind": kind, "holder": holder, "amount": amount,
			"cap": int(t.get("cap", 0)), "discount": float(t.get("discount", 0.0)),
			"rate": float(t.get("rate", 0.0)), "maturity_wk": int(t.get("maturity_wk", 0)),
			"pct": 0.0, "prefs": 0.0, "protective": false, "drag_threshold": 0.0,
			"signed_wk": state.week})
		line = "signed %s's %s: $%s wired now — dilution deferred%s" % [holder, kind,
			money(amount), (", matures wk %d" % int(t.get("maturity_wk", 0))) if kind != "safe" else ""]
	entry["stage"] = "wired"
	entry["wired_wk"] = state.week
	if kind == "priced":
		entry["no_shop_until"] = state.week + int(t.get("no_shop_wks", 4))
		state.raise_state["active"] = false
		state.raise_state["founder_time_tax"] = 0.0
	state.log_action(line)
	return line

## The priced closing, in the fixed order the header documents.
static func _sign_priced(state: GameState, holder: String, t: Dictionary) -> String:
	var pre := maxf(float(t.get("valuation", SimEngine.valuation(state))), 1.0)
	var amount := int(t.get("amount", 0))
	# 1 · the stack converts, whole, at the round's pre-money
	var conv_total := 0.0
	var conv_notes: Array[String] = []
	for inst in state.instruments:
		var idd: Dictionary = inst
		if not _unconverted(idd):
			continue
		var pct_i := convert_pct_at(idd, pre, state.week)
		idd["pct"] = snappedf(pct_i, 0.01)
		conv_total += pct_i
		conv_notes.append("%s -> %.1f%%" % [String(idd.get("holder", "?")), pct_i])
	if conv_total > 0.0:
		var keep := 1.0 - conv_total / 100.0
		state.founder_pct = maxf(state.founder_pct * keep, 1.0)
		for cf in state.cofounders:
			var cfd: Dictionary = cf
			cfd["equity_diluted"] = float(cfd.get("equity_diluted", cfd.get("equity", 0.0))) * keep
		if not state.esop.is_empty():
			state.esop["pool_pct"] = float(state.esop.get("pool_pct", 0.0)) * keep
	# 2 · mirror the pool out so the board's shuffle math starts from truth
	if not state.esop.is_empty():
		state.option_pool_pct = float(state.esop.get("pool_pct", 0.0))
	var pool_keep := 1.0 - clampf(SimBoard.pool_ask_pct(state), 0.0, 15.0) / 100.0
	var inv_pct := 100.0 * float(amount) / (pre + float(amount))
	var inv_keep := 1.0 - inv_pct / 100.0
	# 3+4 · the existing seams: cash, ladder, founder dilution, pool shuffle,
	# seats, covenant — never forked
	SimEngine.apply_round(state, amount, inv_pct)
	SimBoard.on_round_closed(state, amount, inv_pct)
	# 5 · the paper the seams don't know about scales by the same keeps
	for inst3 in state.instruments:
		var i3: Dictionary = inst3
		if float(i3.get("pct", 0.0)) > 0.0:
			i3["pct"] = snappedf(float(i3["pct"]) * pool_keep * inv_keep, 0.01)
	# 6 · absorb the shuffled pool back into the source of truth
	if state.option_pool_pct > 0.0 and state.esop.is_empty():
		state.esop = {"pool_pct": state.option_pool_pct, "granted": []}
	elif not state.esop.is_empty():
		state.esop["pool_pct"] = state.option_pool_pct
	# 7 · the new preferred stock on the books. The investor's slice is of the
	# POST company — the pool was written PRE-money, out of the founding side,
	# so their pct never takes the shuffle (that asymmetry IS the lesson).
	state.instruments.append({"kind": "priced", "holder": holder, "amount": amount,
		"cap": 0, "discount": 0.0, "rate": 0.0, "maturity_wk": 0,
		"pct": snappedf(inv_pct, 0.01),
		"prefs": 1.0,
		"protective": bool(t.get("protective", true)),
		"drag_threshold": float(t.get("drag_threshold", 60.0)),
		"signed_wk": state.week})
	# the round's milestone flag, exactly as the journal signing sites set it
	var last := String(state.rounds_raised[state.rounds_raised.size() - 1]) \
		if not state.rounds_raised.is_empty() else ""
	if last == "seed":
		state.set_flag("seed_raised")
	elif last == "series_a":
		state.set_flag("series_a")
	var conv_txt := ""
	if not conv_notes.is_empty():
		conv_txt = " · the stack converted at once (%s)" % " · ".join(conv_notes)
	return "PRICED ROUND SIGNED — %s wires $%s at $%s pre -> ≈%.1f%% preferred, board seat, covenant armed%s" % [
		holder, money(amount), money(int(pre)), inv_pct, conv_txt]

# ═══════════════════════════ RECRUITMENT ═════════════════════════════════════

## The seat's band — era/world labor market data, never the price book.
static func band_for(state: GameState, seat: String) -> Dictionary:
	var mk := SimLabor.market_salary(seat, state.era)
	return {"lo": _round10(float(mk) * 0.85), "hi": _round10(float(mk) * 1.25)}

static func _senior_mult(seat: String) -> float:
	var low := seat.to_lower()
	for w in SENIOR_WORDS:
		if low.contains(w):
			return 0.6
	return 1.0

## Expected applicants per week for one seat — the number the desk prints
## beside the advert stepper.
static func arrival_rate_r(state: GameState, role: Dictionary) -> float:
	var advert := float(role.get("advert_wk", 0))
	if advert <= 0.0:
		return 0.0
	var seat := String(role.get("seat", "engineer"))
	var band_mid := (float(role.get("band_lo", 0)) + float(role.get("band_hi", 0))) * 0.5
	var attract := SimLabor.attractiveness(state, seat, int(band_mid))
	return clampf(advert / 30.0, 0.0, 4.0) * (0.5 + attract) * _senior_mult(seat)

## THE COMPOSER'S CURVE — the header's formula, verbatim. Pure: the desk
## recomputes the marginal points from this same function.
static func acceptance_odds(state: GameState, cand: Dictionary, cash_wk: int, options_pct: float) -> float:
	var ask := maxf(float(cand.get("ask", 1)), 1.0)
	var mercenary := String(cand.get("profile", "mercenary")) == "mercenary"
	var w_cash := 1.0 if mercenary else 0.5
	var w_opt := 1.0 if mercenary else 2.0
	var odds := 60.0
	odds += (float(cash_wk) / ask - 1.0) * 216.0 * w_cash
	odds += options_pct * 20.0 * w_opt
	odds += float(state.hype) / 10.0
	odds += (float(state.morale) - 50.0) / 10.0
	return clampf(odds, 5.0, 95.0)

static func cand_by_id(state: GameState, id: String) -> Dictionary:
	for c in state.recruitment.get("candidates", []):
		if String((c as Dictionary).get("id", "")) == id:
			return c
	return {}

static func _role_by_id(state: GameState, id: String) -> Dictionary:
	for r in state.recruitment.get("roles", []):
		if String((r as Dictionary).get("id", "")) == id:
			return r
	return {}

## Open a seat (desk door). Band from the labor market; advert defaults $40/wk.
static func open_seat(state: GameState, seat: String) -> String:
	_ensure_recruitment(state)
	if not SimLabor.market_open(state.era) or not SimLabor.role_unlocked(seat, state.era):
		return ""
	if SimLabor.seats_left(state) <= (state.recruitment.get("roles", []) as Array).size():
		return ""
	var band := band_for(state, seat)
	(state.recruitment["roles"] as Array).append({
		"id": "role_%s_%d" % [emp_slug(seat), state.week], "seat": seat,
		"band_lo": int(band.get("lo", 0)), "band_hi": int(band.get("hi", 0)),
		"advert_wk": 40, "opened_wk": state.week})
	return "SEAT OPEN: %s (band $%s–%s/wk) — advert $40/wk" % [seat,
		money(int(band.get("lo", 0))), money(int(band.get("hi", 0)))]

static func close_seat(state: GameState, role_id: String) -> void:
	if state.recruitment.is_empty():
		return
	var roles: Array = state.recruitment.get("roles", [])
	for i in range(roles.size() - 1, -1, -1):
		if String((roles[i] as Dictionary).get("id", "")) == role_id:
			roles.remove_at(i)

static func set_advert(state: GameState, role_id: String, advert: int) -> void:
	var role := _role_by_id(state, role_id)
	if not role.is_empty():
		role["advert_wk"] = clampi(advert, 0, 400)

## INTERVIEWING COSTS THE FOUNDER'S WEEK — a desk press, not a DM op.
static func interview(state: GameState, cand_id: String) -> String:
	var cand := cand_by_id(state, cand_id)
	if cand.is_empty() or String(cand.get("stage", "")) != "applied":
		return ""
	cand["stage"] = "interviewed"
	state.fatigue = minf(state.fatigue + 4.0, 100.0)
	return "interviewed %s — %s, asks $%s" % [String(cand.get("name", "?")),
		String(cand.get("profile", "")), money(int(cand.get("ask", 0)))]

## OP send_offer — comp is a designed mix. Refused ("" ) when the candidate is
## not in motion, the house is full, or the pool cannot cover the equity ask.
static func op_send_offer(state: GameState, cand_id: String, cash_wk: int, options_pct: float) -> String:
	var cand := cand_by_id(state, cand_id)
	if cand.is_empty():
		return ""
	var stage := String(cand.get("stage", ""))
	if stage != "applied" and stage != "interviewed":
		return ""
	if SimLabor.seats_left(state) <= 0:
		return ""
	if options_pct > 0.0 and pool_free(state) + 0.0001 < options_pct:
		return ""   # empty pool blocks equity offers — expand it on the cap table
	var seat_role := ""
	var role := _role_by_id(state, String(cand.get("role_id", "")))
	seat_role = String(role.get("seat", "engineer")) if not role.is_empty() else "engineer"
	var cash := SimLabor.clamp_salary(SimLabor.market_salary(seat_role, state.era), cash_wk)
	(state.recruitment["offers_out"] as Array).append({
		"candidate_id": String(cand.get("id", "")), "cash_wk": cash,
		"options_pct": snappedf(options_pct, 0.01),
		"expires_wk": state.week + OFFER_OUT_WKS, "sent_wk": state.week})
	cand["stage"] = "offer"
	var line := "OFFER OUT to %s: $%s/wk%s — odds ≈%d%%" % [String(cand.get("name", "?")),
		money(cash), (" + %.1f%% options" % options_pct) if options_pct > 0.0 else "",
		int(round(acceptance_odds(state, cand, cash, options_pct)))]
	state.log_action(line)
	return line

## §tick_pre — recruitment's week: offers resolve first (the candidate has had
## a week to think), then arrivals, then patience. Salt order fixed.
static func _recruit_weekly(state: GameState, rep: Dictionary) -> void:
	if state.recruitment.is_empty():
		return
	_ensure_recruitment(state)
	var rec: Dictionary = state.recruitment
	var r152 := SimEngine.rng_for(state, SimEngine.SALT_RECRUIT_ACCEPT)
	var r153 := SimEngine.rng_for(state, SimEngine.SALT_RECRUIT_COUNTER)
	# 1 · offers out resolve one week after sending; expiry is a hard stop
	var offers: Array = rec.get("offers_out", [])
	for i in range(offers.size() - 1, -1, -1):
		var off: Dictionary = offers[i]
		var cand := cand_by_id(state, String(off.get("candidate_id", "")))
		if cand.is_empty():
			offers.remove_at(i)
			continue
		if state.week <= int(off.get("sent_wk", state.week)):
			continue   # she thinks for a week
		var odds := acceptance_odds(state, cand, int(off.get("cash_wk", 0)),
			float(off.get("options_pct", 0.0)))
		if r152.randf() * 100.0 < odds:
			_offer_accepted(state, rep, cand, off)
			offers.remove_at(i)
			continue
		# declined — the rival counter roll, then the market hears
		var mercenary := String(cand.get("profile", "")) == "mercenary"
		if r153.randf() < (0.35 if mercenary else 0.15):
			cand["stage"] = "lost"
			rep["events"].append("%s took a rival's counter-offer — her profile chases %s"
				% [String(cand.get("name", "?")), "cash" if mercenary else "meaning"])
		else:
			cand["stage"] = "interviewed"
			cand["ask"] = _round10(float(cand.get("ask", 0)) * 1.05)
			rep["lines"].append("%s declined — the ask hardened to $%s" % [
				String(cand.get("name", "?")), money(int(cand.get("ask", 0)))])
		state.hype = clampi(state.hype - 1, 0, 100)
		offers.remove_at(i)
	# 2 · arrivals per open seat (SALT_RECRUIT_ARRIVALS then PROFILE)
	var r150 := SimEngine.rng_for(state, SimEngine.SALT_RECRUIT_ARRIVALS)
	var r151 := SimEngine.rng_for(state, SimEngine.SALT_RECRUIT_PROFILE)
	var born := 0
	for role in rec.get("roles", []):
		var rd: Dictionary = role
		var lam := arrival_rate_r(state, rd)
		if lam <= 0.0:
			continue
		var p := minf(lam, 8.0) / 8.0
		var count := 0
		for _k in 8:
			if r150.randf() < p:
				count += 1
		for _c in count:
			var skill := _draw_skill(r151)
			var lo := float(rd.get("band_lo", 0))
			var hi := float(rd.get("band_hi", 0))
			var ask := _round10((lo + (hi - lo) * float(skill - 1) / 4.0) * r151.randf_range(0.95, 1.12))
			(rec["candidates"] as Array).append({
				"id": "cand_%d_%d" % [state.week, born],
				"role_id": String(rd.get("id", "")),
				"name": _fresh_name(state, r151),
				"ask": ask,
				"profile": "mercenary" if r151.randf() < MERCENARY_P else "missionary",
				"skill": skill, "stage": "applied", "arrived_wk": state.week})
			born += 1
		if count > 0:
			rep["lines"].append("%d applied for %s (advert $%d/wk -> ≈%.1f/wk)" % [
				count, String(rd.get("seat", "?")).to_upper(), int(rd.get("advert_wk", 0)), lam])
	# 3 · patience: waiting candidates walk at the shelf life; the good ones
	# leaving is a beat. Offer-out candidates are exempt — they hold YOUR paper.
	var cands: Array = rec.get("candidates", [])
	for j in range(cands.size() - 1, -1, -1):
		var cd: Dictionary = cands[j]
		var stg := String(cd.get("stage", ""))
		if stg == "applied" or stg == "interviewed":
			if state.week - int(cd.get("arrived_wk", state.week)) >= CANDIDATE_PATIENCE:
				if int(cd.get("skill", 3)) >= 4:
					rep["events"].append("%s stopped waiting — the good ones are gone in weeks"
						% String(cd.get("name", "?")))
				cands.remove_at(j)
		elif (stg == "joined" or stg == "lost") \
				and state.week - int(cd.get("arrived_wk", 0)) > 10:
			cands.remove_at(j)

## The signed hire rides the EXISTING labor hire path — the onboarding
## pipeline the engine already graduates — plus the grant when options rode
## the offer (pool decrements through granted; the free check ran at send).
static func _offer_accepted(state: GameState, rep: Dictionary, cand: Dictionary, off: Dictionary) -> void:
	if SimLabor.seats_left(state) <= 0:
		cand["stage"] = "interviewed"
		rep["lines"].append("%s said yes but the house is full — the offer lapsed"
			% String(cand.get("name", "?")))
		return
	var role := _role_by_id(state, String(cand.get("role_id", "")))
	var seat := String(role.get("seat", "engineer")) if not role.is_empty() else "engineer"
	state.pipeline.append({"name": String(cand.get("name", "hire")),
		"role": SimLabor.role_row(seat),
		"salary": int(off.get("cash_wk", 1200)), "weeks_in": 0,
		"quirk": "", "skill": clampi(int(cand.get("skill", 3)), 1, 5)})
	cand["stage"] = "joined"
	var opt := float(off.get("options_pct", 0.0))
	var grant_txt := ""
	if opt > 0.0:
		var line := grant_options(state, String(cand.get("name", "hire")), opt)
		grant_txt = (" · " + line) if line != "" else " · the pool ran dry — cash-only after all"
	if not role.is_empty():
		close_seat(state, String(role.get("id", "")))
	rep["events"].append("%s SIGNED at $%s/wk — onboarding%s" % [String(cand.get("name", "?")),
		money(int(off.get("cash_wk", 0))), grant_txt])

static func _draw_skill(rng: RandomNumberGenerator) -> int:
	var u := rng.randf()
	var acc := 0.0
	var weights := [0.15, 0.25, 0.30, 0.20, 0.10]
	for i in weights.size():
		acc += float(weights[i])
		if u < acc:
			return i + 1
	return 5

static func _fresh_name(state: GameState, rng: RandomNumberGenerator) -> String:
	var taken := SimLabor.taken_names(state)
	var nm := ""
	for _i in 5:
		nm = WorldGen.person_name(rng)
		if not taken.has(nm):
			return nm
	return nm

# ═══════════════════════════ BUYOUT OFFERS ═══════════════════════════════════

## §tick_post — after SimBoard._mna settled the courtship: dress the board's
## offer with structure, or (both quiet) roll this lane's own arrival; when
## the underlying offer died elsewhere, file the folder into the log.
static func _buyout_weekly(state: GameState, rep: Dictionary) -> void:
	var r122 := SimEngine.rng_for(state, SimEngine.SALT_OWN_BUYOUT)
	if not state.buyout_offer.is_empty():
		if state.mna.is_empty() or String(state.mna.get("buyer", "")) != String(state.buyout_offer.get("buyer", "")):
			# lapsed or answered through the old journal seam — file and fold
			state.log_action("the buyout folder closed unanswered — %s's offer left the table"
				% String(state.buyout_offer.get("buyer", "a buyer")))
			state.buyout_offer = {}
		return
	if not state.mna.is_empty():
		state.buyout_offer = _dress_offer(state, state.mna, r122)
		rep["events"].append("THE OFFER, IN WRITING: %s — read the small lines on THE OFFER desk"
			% String(state.buyout_offer.get("headline_line", "the structure is on the desk")))
		return
	# the lane's own arrival — only when the board's ladder stayed quiet
	if state.exit_value > 0 or state.dead:
		return
	if state.week < 6 or state.week < state.mna_last_week + 10:
		return
	var p := clampf(0.02 + 0.0004 * float(state.traction) + 0.0006 * float(state.hype)
		+ 0.01 * float(state.era_index()), 0.0, 0.10)
	if r122.randf() >= p:
		return
	var v := SimEngine.valuation(state)
	var prem := r122.randf_range(0.9, 1.3)
	var strong := _strongest_rival(state)
	var buyer := String(strong.get("name", "")) if float(strong.get("strength", 0.0)) >= 55.0 \
		else "a strategic who has been watching"
	state.mna = {"buyer": buyer, "why": "inbound", "premium": snappedf(prem, 0.01),
		"price": maxi(int(float(v) * prem), 10_000),
		"expires_week": state.week + 2}
	state.mna_last_week = state.week
	state.buyout_offer = _dress_offer(state, state.mna, r122)
	rep["events"].append("AN OFFER FOR THE COMPANY: %s puts $%s on the table — it expires wk %d. THE OFFER desk has the fine print"
		% [buyer, money(int(state.mna.get("price", 0))), int(state.mna.get("expires_week", 0))])

## The structure — cash/stock/earnout split by the offer's why, handcuffs,
## and THE FISHY FLAGS COMPUTED AS FIELDS. Some offers are fishy on purpose.
static func _dress_offer(state: GameState, mo: Dictionary, r: RandomNumberGenerator) -> Dictionary:
	var price := int(mo.get("price", 0))
	var why := String(mo.get("why", ""))
	var f_cash := 0.4
	var f_stock := 0.35
	match why:
		"lifeline":
			f_cash = 0.8
			f_stock = 0.2
		"rival":
			f_cash = 0.5
			f_stock = 0.3
		"boom":
			f_cash = 0.3
			f_stock = 0.5
	f_cash = clampf(f_cash + r.randf_range(-0.1, 0.1), 0.1, 0.9)
	f_stock = clampf(f_stock + r.randf_range(-0.1, 0.1), 0.0, 0.9 - f_cash + 0.8)
	if f_cash + f_stock > 1.0:
		f_stock = 1.0 - f_cash
	var cash := int(float(price) * f_cash)
	var stock := int(float(price) * f_stock)
	var earnout := price - cash - stock
	var lockup := 0
	if stock > 0:
		lockup = r.randi_range(26, 104)
	var controller := "buyer" if (earnout > 0 and r.randf() < 0.6) else "neutral"
	var retention := r.randi_range(52, 104)
	var carve := r.randf() < 0.3
	var flags: Array = []
	if earnout > 0 and controller == "buyer":
		flags.append("the earnout's targets are set — and measured — by the buyer")
	if lockup >= 52:
		flags.append("$%s of the price is their stock, locked %d months" % [money_short(stock), lockup / 4])
	if price < int(float(SimEngine.valuation(state)) * 0.8) \
			and int(mo.get("expires_week", 0)) - state.week <= 2:
		flags.append("a low price on a short fuse — expiry pressure is the point")
	if carve:
		flags.append("the retention pool is carved from YOUR share, not the buyer's")
	return {"buyer": String(mo.get("buyer", "a buyer")), "headline": price,
		"cash": cash, "stock": stock, "lockup_wks": lockup,
		"earnout": earnout, "earnout_controller": controller,
		"retention_wks": retention, "retention_carve": carve,
		"expires_wk": int(mo.get("expires_week", state.week + 2)),
		"fishy_flags": flags, "why": why, "arrived_wk": state.week,
		"countered": false,
		"headline_line": "%s offers $%s" % [String(mo.get("buyer", "a buyer")), money(price)]}

## THE WATERFALL — pure. Debts → prefs-or-convert per holder (max of the two,
## computed) → the split incl. vested ESOP. Returns rows in payout order plus
## the founder's decomposed take.
static func waterfall(state: GameState, price: int) -> Dictionary:
	var rows: Array = []
	var pot := float(price)
	var debts := float(SimBank.debt_total(state))
	var debts_paid := minf(pot, debts)
	pot -= debts_paid
	if debts_paid > 0.0:
		rows.append({"holder": "the bank", "take": int(round(debts_paid)),
			"note": "debts die first"})
	# prefs-or-convert, per holder: the compare uses pct × (price − debts),
	# the mockup's own arithmetic
	var after_debts := pot
	var pref_total := 0.0
	var pref_pcts := 0.0
	var choices: Array = []
	for inst in state.instruments:
		var idd: Dictionary = inst
		var pct := float(idd.get("pct", 0.0))
		var conv_pct := pct
		if _unconverted(idd):
			var basis := minf(float(idd.get("cap", 0)) if float(idd.get("cap", 0)) > 0.0 else float(price),
				float(price))
			conv_pct = clampf(100.0 * float(amount_due(idd, state.week)) / maxf(basis, 1.0),
				0.0, CONVERT_PCT_CAP)
		var pref_amt := 0.0
		if float(idd.get("prefs", 0.0)) > 0.0:
			pref_amt = float(idd.get("amount", 0)) * float(idd.get("prefs", 0.0))
		elif _unconverted(idd):
			pref_amt = float(amount_due(idd, state.week))   # 1× money back
		var conv_val := conv_pct / 100.0 * after_debts
		choices.append({"inst": idd, "conv_pct": conv_pct, "pref": pref_amt,
			"converts": conv_val >= pref_amt})
		if conv_val < pref_amt:
			pref_total += pref_amt
			pref_pcts += conv_pct
	pref_total = minf(pref_total, pot)
	pot -= pref_total
	# the split's denominator: everyone participating, vested ESOP included,
	# the unallocated pool cancelled (it simply leaves the denominator)
	var cof := 0.0
	for cf in state.cofounders:
		cof += float((cf as Dictionary).get("equity_diluted", (cf as Dictionary).get("equity", 0.0)))
	var esop_vested := 0.0
	for g in state.esop.get("granted", []):
		var gd: Dictionary = g
		if String(gd.get("emp_id", "")).begins_with("left:"):
			esop_vested += float(gd.get("pct", 0.0))
		else:
			esop_vested += vest_frac(state.week - int(gd.get("vest_start_wk", state.week))) \
				* float(gd.get("pct", 0.0))
	var conv_pcts := 0.0
	for ch in choices:
		if bool((ch as Dictionary).get("converts", false)):
			conv_pcts += float((ch as Dictionary).get("conv_pct", 0.0))
	var denom := maxf(state.founder_pct + cof + esop_vested + conv_pcts, 0.01)
	# the rows, in order: pref-takers, converters, ESOP, cofounders, YOU
	for ch2 in choices:
		var cd: Dictionary = ch2
		var idd2: Dictionary = cd.get("inst", {})
		var holder := String(idd2.get("holder", "?"))
		if bool(cd.get("converts", false)):
			var take := pot * float(cd.get("conv_pct", 0.0)) / denom
			rows.append({"holder": holder, "take": int(round(take)),
				"note": "converts — %.0f%% beats their %s ($%s); computed" % [
					float(cd.get("conv_pct", 0.0)),
					("%.0f×" % float(idd2.get("prefs", 1.0))) if float(idd2.get("prefs", 0.0)) > 0.0 else "1×",
					money(int(idd2.get("amount", 0)))]})
		else:
			rows.append({"holder": holder, "take": int(round(minf(float(cd.get("pref", 0.0)), pref_total))),
				"note": "takes the preference — safer than converting"})
	var esop_take := pot * esop_vested / denom
	if esop_take >= 1.0:
		rows.append({"holder": "the ESOP holders", "take": int(round(esop_take)),
			"note": "vested only — your people get paid too"})
	for cf2 in state.cofounders:
		var cfd: Dictionary = cf2
		var cpct := float(cfd.get("equity_diluted", cfd.get("equity", 0.0)))
		if cpct > 0.01:
			rows.append({"holder": String(cfd.get("name", "cofounder")),
				"take": int(round(pot * cpct / denom)), "note": "common"})
	var your_take := pot * state.founder_pct / denom
	var breakeven := int(round(debts + pref_total))
	return {"rows": rows, "your_take": int(round(your_take)),
		"esop_take": int(round(esop_take)), "debts": int(round(debts_paid)),
		"prefs_paid": int(round(pref_total)), "breakeven": breakeven}

## Your take decomposed by the offer's own mix: cash today / locked stock /
## maybe-earnout — the same fractions the headline was made of.
static func take_decomposed(bo: Dictionary, your_take: int) -> Dictionary:
	var headline := maxf(float(bo.get("headline", 1)), 1.0)
	var cash := int(round(float(your_take) * float(bo.get("cash", 0)) / headline))
	var stock := int(round(float(your_take) * float(bo.get("stock", 0)) / headline))
	return {"cash": cash, "stock": stock, "earnout": your_take - cash - stock}

## WHO CAN SAY NO — the powers were signed at the raise, resolved here:
## protective holders lean by their computed take; drag counts the preferred.
static func powers(state: GameState, price: int) -> Array:
	var out: Array = []
	out.append({"who": "you", "line": "your yes is needed", "blocks": false})
	var wf := waterfall(state, price)
	var pref_pct_total := 0.0
	var pref_pct_yes := 0.0
	for inst in state.instruments:
		var idd: Dictionary = inst
		var pct := float(idd.get("pct", 0.0))
		if pct <= 0.0:
			continue
		pref_pct_total += pct
		var take := 0
		for row in wf.get("rows", []):
			if String((row as Dictionary).get("holder", "")) == String(idd.get("holder", "")):
				take = int((row as Dictionary).get("take", 0))
		var happy := float(take) >= float(idd.get("amount", 0))
		if happy:
			pref_pct_yes += pct
		if bool(idd.get("protective", false)):
			out.append({"who": String(idd.get("holder", "?")),
				"line": "holds a sale veto (protective provision) — leaning %s" % ("yes" if happy else "NO"),
				"blocks": not happy})
	for inst2 in state.instruments:
		var i2: Dictionary = inst2
		var thr := float(i2.get("drag_threshold", 0.0))
		if thr > 0.0 and pref_pct_total > 0.0:
			var share := 100.0 * pref_pct_yes / pref_pct_total
			out.append({"who": "drag-along",
				"line": "≥%.0f%% of preferred could force a sale — %s" % [thr,
					("TRIGGERED at %.0f%%" % share) if share >= thr else "not triggered here"],
				"blocks": false})
			break
	return out

## ACCEPT — desk-side two-tap already taken; the waterfall runs, the run ends
## through the EXISTING exit seam (exit_value + acquired_exit, soft landing
## kept for lifelines), and the folder files into the log.
static func buyout_accept(state: GameState) -> String:
	if state.buyout_offer.is_empty():
		return ""
	var bo: Dictionary = state.buyout_offer
	var price := int(bo.get("headline", 0))
	var wf := waterfall(state, price)
	var dec := take_decomposed(bo, int(wf.get("your_take", 0)))
	state.exit_value = price
	state.set_flag("acquired_exit")
	if String(bo.get("why", "")) == "lifeline":
		state.set_flag("soft_landing")
	state.set_meta("exit_take", wf.get("your_take", 0))
	var line := "SOLD to %s for $%s — the waterfall pays you ≈$%s ($%s cash + $%s locked stock + $%s maybe-earnout)" % [
		String(bo.get("buyer", "a buyer")), money(price), money(int(wf.get("your_take", 0))),
		money(int(dec.get("cash", 0))), money(int(dec.get("stock", 0))), money(int(dec.get("earnout", 0)))]
	state.log_action(line)
	state.mna = {}
	state.buyout_offer = {}
	return line

## NEGOTIATE — one counter; the world reprices ONCE (SALT_OWN_BUYOUT).
static func buyout_negotiate(state: GameState) -> String:
	if state.buyout_offer.is_empty() or bool(state.buyout_offer.get("countered", false)):
		return ""
	var bo: Dictionary = state.buyout_offer
	var r := SimEngine.rng_for(state, SimEngine.SALT_OWN_BUYOUT)
	var lean := 1.0
	for p in powers(state, int(bo.get("headline", 0))):
		if bool((p as Dictionary).get("blocks", false)):
			lean = 0.95   # a blocked table has no leverage
	var mult := r.randf_range(0.95, 1.15) * lean
	var new_price := maxi(int(float(bo.get("headline", 0)) * mult), 10_000)
	var scale := float(new_price) / maxf(float(bo.get("headline", 1)), 1.0)
	bo["headline"] = new_price
	bo["cash"] = int(float(bo.get("cash", 0)) * scale)
	bo["stock"] = int(float(bo.get("stock", 0)) * scale)
	bo["earnout"] = new_price - int(bo.get("cash", 0)) - int(bo.get("stock", 0))
	bo["countered"] = true
	bo["headline_line"] = "%s offers $%s" % [String(bo.get("buyer", "a buyer")), money(new_price)]
	if not state.mna.is_empty():
		state.mna["price"] = new_price
	var line := "countered — %s repriced to $%s (%s). One counter is all the room there is." % [
		String(bo.get("buyer", "the buyer")), money(new_price),
		"up" if mult >= 1.0 else "down"]
	state.log_action(line)
	return line

## DECLINE — the street hears; the courtship cools on the board's own cooldown.
static func buyout_decline(state: GameState) -> String:
	if state.buyout_offer.is_empty():
		return ""
	var buyer := String(state.buyout_offer.get("buyer", "a buyer"))
	state.hype = clampi(state.hype + 2, 0, 100)
	state.mna = {}
	state.mna_last_week = state.week
	state.buyout_offer = {}
	var line := "DECLINED %s's offer — the street heard you say no (+2 hype). Offers can sour, or come back higher." % buyer
	state.log_action(line)
	return line

static func _strongest_rival(state: GameState) -> Dictionary:
	var best: Dictionary = {}
	for rv in state.rivals:
		var rd: Dictionary = rv
		if best.is_empty() or float(rd.get("strength", 0.0)) > float(best.get("strength", 0.0)):
			best = rd
	return best

# ═══════════════════════ THE SPINE'S ENTRY POINTS ════════════════════════════

## Tick §9, with the financial lanes: migrations, vesting's leaver rule, the
## founder-time drag, then recruitment's week.
static func tick_pre(state: GameState, rep: Dictionary) -> void:
	migrate_ownership(state)
	_crystallize_leavers(state, rep)
	if bool(state.raise_state.get("active", false)):
		state.raise_state["founder_time_tax"] = FOUNDER_TIME_TAX
		SimEngine.add_status(state, RAISE_STATUS, 2)   # no-op until the catalog entry lands
	elif not state.raise_state.is_empty():
		state.raise_state["founder_time_tax"] = 0.0
	_recruit_weekly(state, rep)

## The money section — ONLY the advert lane. ESOP never touches the identity;
## a closed raise wired its cash as an event inside the op.
static func tick_money(state: GameState, rep: Dictionary, m: Dictionary) -> void:
	if state.recruitment.is_empty():
		return
	var ads := 0.0
	for role in state.recruitment.get("roles", []):
		ads += float((role as Dictionary).get("advert_wk", 0))
	if ads > 0.0:
		m["recruit_ads"] = float(m.get("recruit_ads", 0.0)) + ads
		rep["lines"].append("role adverts: −$%d/wk (the seats stay lit)" % int(ads))

## After the record — the raise reads the finished week, then the buyout.
static func tick_post(state: GameState, rep: Dictionary) -> void:
	if state.dead:
		return
	_raise_weekly(state, rep)
	_buyout_weekly(state, rep)

## DM context lines (the spine caps the block; ≤4 from this lane).
static func directives(state: GameState) -> Array[String]:
	var out: Array[String] = []
	if bool(state.raise_state.get("active", false)):
		out.append("- THE RAISE is active: %d in conversations, %d terms on the table. It eats ~30%% of the founder's week — the shop measurably slows."
			% [_stages_in(state, "conversations").size(), _stages_in(state, "terms").size()])
	elif not _stages_in(state, "terms").is_empty():
		out.append("- Terms are on the table at THE RAISE; only sign_instrument signs, never your narration.")
	if not state.buyout_offer.is_empty():
		out.append("- The buyout's fine print carries %d flag(s) the founder can read on THE OFFER desk. The desk answers it, never you."
			% (state.buyout_offer.get("fishy_flags", []) as Array).size())
	if not state.recruitment.is_empty():
		var offers: Array = state.recruitment.get("offers_out", [])
		if not offers.is_empty():
			var off: Dictionary = offers[0]
			var cand := cand_by_id(state, String(off.get("candidate_id", "")))
			out.append("- A comp offer is out to %s ($%d/wk%s), expires wk %d."
				% [String(cand.get("name", "a candidate")), int(off.get("cash_wk", 0)),
					(" + %.1f%% options" % float(off.get("options_pct", 0.0))) if float(off.get("options_pct", 0.0)) > 0.0 else "",
					int(off.get("expires_wk", 0))])
	return out

## Attention rows {desk, key, severity, label} — labels ≤40 chars, pedagogy.
static func attention(state: GameState) -> Array:
	var rows: Array = []
	for idd in matured_notes(state):
		rows.append({"desk": "the raise", "key": "note_matured", "severity": 3,
			"label": ("note matured — $%s due or convert"
				% money_short(amount_due(idd as Dictionary, state.week)).lstrip("$")).left(40)})
	if not state.buyout_offer.is_empty():
		var left := maxi(int(state.buyout_offer.get("expires_wk", 0)) - state.week, 0)
		rows.append({"desk": "the offer", "key": "buyout_live", "severity": 3,
			"label": ("buyout expires in %d wk — answer it" % left).left(40)})
	for st in _stages_in(state, "terms"):
		var t: Dictionary = (st as Dictionary).get("terms", {})
		rows.append({"desk": "the raise", "key": "terms_open", "severity": 2,
			"label": ("terms on the table — expire wk %d" % int(t.get("expires_wk", 0))).left(40)})
		break
	if not state.recruitment.is_empty():
		for off in state.recruitment.get("offers_out", []):
			var od: Dictionary = off
			var cand := cand_by_id(state, String(od.get("candidate_id", "")))
			rows.append({"desk": "recruitment", "key": "offer_out", "severity": 2,
				"label": ("%s's offer expires in %d wk" % [String(cand.get("name", "someone")),
					maxi(int(od.get("expires_wk", 0)) - state.week, 0)]).left(40)})
			break
		if pool_free(state) <= 0.0001 and not state.esop.is_empty() \
				and not (state.recruitment.get("roles", []) as Array).is_empty():
			rows.append({"desk": "cap table", "key": "pool_empty", "severity": 2,
				"label": "pool empty — no equity offers"})
	for g in state.esop.get("granted", []):
		var gd: Dictionary = g
		if String(gd.get("emp_id", "")).begins_with("left:"):
			continue
		var cliff_in := int(gd.get("vest_start_wk", 0)) + CLIFF_WEEKS - state.week
		if cliff_in > 0 and cliff_in <= 4:
			rows.append({"desk": "cap table", "key": "cliff_near", "severity": 1,
				"label": ("%s's cliff lands in %d wk" % [
					String(gd.get("emp_id", "")).replace("_", " "), cliff_in]).left(40)})
			break
	if no_shop_until(state) > state.week:
		rows.append({"desk": "the raise", "key": "no_shop", "severity": 1,
			"label": ("no-shop holds until wk %d" % no_shop_until(state)).left(40)})
	return rows
