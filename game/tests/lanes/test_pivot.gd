extends RefCounted
## LANE SUITE — the pivot (DAG2 W2 L-COMPANY). Spec: docs/design/DECISIONS.md
## § THE PIVOT + docs/design/12-binder-rework-2.md § pivot.
##
## `tests/sim_engine_test.gd` calls run() after the engine's own checks and
## hands over `ok`, the same assert the whole suite uses:
## ok.call(cond, "what it pins"). (Registration rides the coordinator package;
## tests/run_company.gd runs this file standalone until it lands.)
##
## The porting law: a check lands HERE first, then in the same order in
## unity/Runway.Core.Tests/Lanes/PivotTests.cs. The two engines do not share
## PRNG internals, so the 50–100% roll is pinned per-engine (determinism +
## range), never across them.

static func _state(seed_v: int = 4242) -> GameState:
	var s := GameState.new()
	s.sim_seed = seed_v
	s.week = 20
	s.era = "office"
	s.cash = 48_000
	s.traction = 120
	s.product = 62
	s.morale = 66
	s.hype = 40
	s.biz_what = "Software"
	s.biz_who = "SMB"
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	s.set_flag("launched")
	s.content_equity = 1_800.0
	s.tech_debt = 46.0
	s.served_total = 900
	s.platform_level = 2
	s.loan_principal = 9_000
	s.loans = [{"kind": "bank", "principal": 10_000, "balance": 8_200,
		"rate_wk": 0.04, "term_wk": 8, "taken_week": 12, "pay_wk": 1_480, "missed": 0}]
	s.employees = [
		{"name": "Priya Raman", "role": "engineer", "salary": 1500, "burnout": 20,
			"skill": 4, "hired_week": 6, "site": ""},
		{"name": "Tomas Beck", "role": "sales", "salary": 1100, "burnout": 30,
			"skill": 3, "hired_week": 9, "site": ""},
	]
	s.leads = [
		{"name": "Meridian Logistics", "flavor": "forty depots, one spreadsheet",
			"seats": 40, "stage": "pilot", "age_weeks": 3, "heat": 88},
		{"name": "Corvid Freight", "flavor": "", "seats": 22, "stage": "procurement",
			"age_weeks": 6, "heat": 55},
	]
	s.logos = [
		{"name": "Quill Health", "seats": 12, "since_wk": 8, "renewal_wk": 60},
		{"name": "Fernbay Group", "seats": 9, "since_wk": 14, "renewal_wk": 66},
	]
	s.pipe_units = 12.0
	s.pipe_stats = {"signed": 4, "lost": 7, "cycle_sum": 28, "seats_signed": 21,
		"spend": 6_500.0, "first_wk": 8}
	s.beliefs = {"tam": 90_000.0, "lifetime_wk": 44.0}
	s.bets = [{"id": "b1", "name": "Alerts that matter", "kind": "retention",
		"ambition": 2, "cost_rnd_weeks": 6.0, "progress": 3.0, "committed": true}]
	s.features = [{"id": "f1", "name": "online booking", "job": "pull",
		"family": "", "solidity": "solid", "keep_wk": 40, "unit_cost_add": 0.0,
		"product_id": "", "born_wk": 1, "measured": 0.0}]
	return s

static func run(ok: Callable) -> void:
	_pin_audience(ok)
	_pin_product(ok)
	_pin_debts_survive(ok)
	_pin_determinism_and_range(ok)
	_pin_arm_flow(ok)
	_pin_preview_pure(ok)
	_pin_refusals(ok)

# ── 1 · THE AUDIENCE PIVOT — the market dies, the shop survives ──────────────
static func _pin_audience(ok: Callable) -> void:
	var s := _state()
	var cash0 := s.cash
	var crew0 := s.employees.size()
	var res := SimPivot.pivot_audience(s, "Consumer")
	ok.call(bool(res.get("ok", false)), "pivot: audience executor accepts a real new audience")
	ok.call(s.traction == 0, "pivot(audience): customers go to zero")
	ok.call(s.leads.is_empty() and s.logos.is_empty() and s.pipe_units == 0.0,
		"pivot(audience): named deals, logos and loose interest all die")
	ok.call(s.content_equity == 0.0, "pivot(audience): the content well drains")
	ok.call(s.beliefs.is_empty(), "pivot(audience): market beliefs re-fog")
	ok.call(s.biz_who == "Consumer" and absf(float(s.theta.get("tam", 0.0)) - 900_000.0) < 1.0,
		"pivot(audience): the world reprices itself for the new audience")
	ok.call(s.product == 62 and s.tech_debt == 46.0 and s.features.size() == 1
		and s.bets.size() == 1 and s.served_total == 900,
		"pivot(audience): the product survives as built")
	ok.call(s.cash == cash0 and s.employees.size() == crew0,
		"pivot(audience): the cash and the team survive")
	ok.call(s.pivots == 1 and s.has_flag("pivoted"),
		"pivot(audience): the record notes the pivot")
	ok.call((res.get("lines", []) as Array).size() >= 5,
		"pivot(audience): the receipt speaks in full lines")

# ── 2 · THE PRODUCT PIVOT — the product dies, the market learning survives ───
static func _pin_product(ok: Callable) -> void:
	var s := _state()
	var cash0 := s.cash
	var well0 := s.content_equity
	var res := SimPivot.pivot_product(s, "")
	ok.call(bool(res.get("ok", false)), "pivot: product executor fires on the same craft")
	var lost := int(res.get("lost_customers", 0))
	ok.call(lost >= 60 and lost <= 120 and s.traction == 120 - lost,
		"pivot(product): the roll takes between 50% and 100% of the customers")
	ok.call(s.product == 10 and s.bets.is_empty() and s.platform_level == 0
		and s.features.is_empty(),
		"pivot(product): version v0.1, bets, platform and features all die")
	ok.call(s.tech_debt == 0.0, "pivot(product): tech debt clears with its codebase")
	ok.call(s.served_total == 0, "pivot(product): serving practice restarts with the product")
	ok.call(s.leads.size() == 2
		and String((s.leads[0] as Dictionary).get("stage", "")) == "meeting"
		and int((s.leads[0] as Dictionary).get("age_weeks", 9)) == 0,
		"pivot(product): named deals survive, knocked back to the first meeting")
	ok.call(s.content_equity == well0 and not s.pipe_stats.is_empty(),
		"pivot(product): the well and the sales learning survive")
	ok.call(s.cash == cash0, "pivot(product): the cash survives")
	ok.call(s.pivots == 1 and s.has_flag("pivoted"),
		"pivot(product): the record notes the pivot")
	# the craft may change with the product
	var s2 := _state()
	var res2 := SimPivot.pivot_product(s2, "Service")
	ok.call(bool(res2.get("ok", false)) and s2.biz_what == "Service",
		"pivot(product): a new craft lands and the world reprices it")

# ── 3 · DEBTS SURVIVE BOTH — the bank does not forget ────────────────────────
static func _pin_debts_survive(ok: Callable) -> void:
	var a := _state()
	SimPivot.pivot_audience(a, "Enterprise")
	ok.call(a.loan_principal == 9_000 and a.loans.size() == 1
		and int((a.loans[0] as Dictionary).get("balance", 0)) == 8_200,
		"pivot(audience): every note on the books survives untouched")
	var p := _state()
	SimPivot.pivot_product(p, "")
	ok.call(p.loan_principal == 9_000 and p.loans.size() == 1,
		"pivot(product): every note on the books survives untouched")

# ── 4 · DETERMINISM + RANGE — the roll replays, and stays in its band ────────
static func _pin_determinism_and_range(ok: Callable) -> void:
	var a := _state(77)
	var b := _state(77)
	SimPivot.pivot_product(a, "")
	SimPivot.pivot_product(b, "")
	ok.call(a.traction == b.traction,
		"pivot: the same seed and week rolls the same loss (replayable)")
	var seen_low := false
	var seen_high := false
	for sd in range(1, 40):
		var s := _state(sd)
		var res := SimPivot.pivot_product(s, "")
		var pct := int(res.get("loss_pct", 0))
		if pct < 75:
			seen_low = true
		if pct >= 75:
			seen_high = true
		if pct < 50 or pct > 100:
			ok.call(false, "pivot: a loss roll left the 50–100% band")
			return
	ok.call(seen_low and seen_high,
		"pivot: the loss roll actually spreads across its band")

# ── 5 · THE ARM FLOW — flag in, Esc out, LOCK IN resolves ────────────────────
static func _pin_arm_flow(ok: Callable) -> void:
	var s := _state()
	ok.call(SimPivot.armed(s).is_empty() and SimPivot.resolve_armed(s).is_empty(),
		"pivot: an unarmed company resolves to nothing")
	ok.call(not SimPivot.arm_audience(s, "SMB"),
		"pivot: arming toward the audience you already serve is refused")
	ok.call(SimPivot.arm_audience(s, "Consumer")
		and String(SimPivot.armed(s).get("kind", "")) == "audience"
		and String(SimPivot.armed(s).get("target", "")) == "Consumer",
		"pivot: the armed flag carries the door and the destination")
	ok.call(SimPivot.attention(s).size() == 1
		and int((SimPivot.attention(s)[0] as Dictionary).get("severity", 0)) == 3,
		"pivot: an armed pivot is a sev-3 alarm")
	ok.call(SimPivot.directives(s).size() == 1,
		"pivot: an armed pivot briefs the DM")
	SimPivot.disarm(s)
	ok.call(SimPivot.armed(s).is_empty() and s.pivots == 0,
		"pivot: disarm abandons the whole intent and nothing fired")
	ok.call(SimPivot.arm_product(s, "") and not SimPivot.arm_product(s, "Bakery"),
		"pivot: the product arm takes the same craft and refuses a nonsense one")
	var res := SimPivot.resolve_armed(s)
	ok.call(bool(res.get("ok", false)) and String(res.get("kind", "")) == "product"
		and s.pivots == 1 and SimPivot.armed(s).is_empty(),
		"pivot: LOCK IN resolves the armed pivot exactly once")
	ok.call(SimPivot.attention(s).is_empty() and SimPivot.directives(s).is_empty(),
		"pivot: a fired pivot stops shouting")

# ── 6 · THE PREVIEW is pure — it prices, it never touches ────────────────────
static func _pin_preview_pure(ok: Callable) -> void:
	var s := _state()
	var before := JSON.stringify(SaveSystem.state_to_dict(s))
	var pa := SimPivot.preview(s, "audience")
	var pp := SimPivot.preview(s, "product")
	ok.call(int(pa.get("customers_lost", 0)) == 120 and int(pa.get("deals_dead", 0)) == 2
		and int(pa.get("well", 0)) == 1_800,
		"pivot: the audience preview prices the live books")
	ok.call(String(pp.get("version_from", "")) == "v0.6"
		and String(pp.get("version_to", "")) == "v0.1"
		and int(pp.get("debt_cleared", 0)) == 46,
		"pivot: the product preview prices the live books")
	ok.call(int(pa.get("debts", 0)) == 17_200 and int(pp.get("debts", 0)) == 17_200,
		"pivot: both previews name the debts that survive")
	ok.call(JSON.stringify(SaveSystem.state_to_dict(s)) == before,
		"pivot: the preview mutates nothing at all")

# ── 7 · REFUSALS — hostile input bounces with a reason ───────────────────────
static func _pin_refusals(ok: Callable) -> void:
	var s := _state()
	var r1 := SimPivot.pivot_audience(s, "SMB")
	var r2 := SimPivot.pivot_audience(s, "Martians")
	var r3 := SimPivot.pivot_product(s, "Bakery")
	ok.call(not bool(r1.get("ok", true)) and not bool(r2.get("ok", true))
		and not bool(r3.get("ok", true)) and s.pivots == 0 and s.traction == 120,
		"pivot: refused pivots change nothing and say why")
