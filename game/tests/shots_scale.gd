extends SceneTree
## SCALE + LONG-TEXT SHOTS (W4 QA — the owner's law, DECISIONS §LONG-TEXT +
## SCROLLING QA LAW): synthetic max-length fixtures on the settled desks —
## a 10-line spend book (28-char names, 60-char buys), team at rung 2
## (18 people, 2 askers) and rung 3 (45), the works with 5 sites (one
## bleeding), a 34-feature wall with a full pipeline, an 80-week history,
## a 25-letter inbox, the raise with all four columns, recruitment with six
## candidates, and the garden wearing 110-char topic one-liners. A 24-char
## company name rides every fixture so the label overlay truncates on camera.
##
## Asserted visually on the shots: sheets fold per their ladders, hero +
## total stay pinned, nothing clips mid-glyph, folds appear at thresholds,
## attention items never fold away.
##
## Run: RUNWAY_STRESS_DIR=<dir> godot --path . --script tests/shots_scale.gd
## Files land as scale_<state>_godot.png. PNGs are local-only (gitignored).

var _dir := "/tmp"
var _b: Binder = null
var _shots: Array[String] = []

const CO_NAME := "Mosswater & Daughters Co"   # 24 chars — the label overlay law

func _init() -> void:
	call_deferred("_go")

# ═══════════════════════════ the shot harness ════════════════════════════════

func _shot(nm: String) -> void:
	await create_timer(0.25).timeout
	await RenderingServer.frame_post_draw
	root.get_viewport().get_texture().get_image().save_png("%s/scale_%s_godot.png" % [_dir, nm])
	_shots.append(nm)
	print("SHOT scale_%s" % nm)

func _open(s: GameState) -> Binder:
	if _b != null and is_instance_valid(_b):
		_b.queue_free()
		await process_frame
	var b := Binder.new()
	b.tour_enabled = false
	b.setup(s)
	root.add_child(b)
	b.size = Vector2(1536, 1024)
	await create_timer(0.30).timeout
	_b = b
	return b

## Navigate first (focus clears desk-local state), then seed what the shot
## wants, then rebuild — the same order a real press produces.
func _page(b: Binder, id: String, st: Dictionary = {}) -> void:
	b.focus_desk(id)
	b.desk.clear()
	for k in st:
		b.desk[k] = st[k]
	b.refresh()

## The fixture strings are EXACT-length by contract; drift is a probe error.
func _x(s: String, n: int) -> String:
	if s.length() != n:
		push_error("SCALE FIXTURE LENGTH %d != %d: %s" % [s.length(), n, s])
	return s

# ═══════════════════════════════ fixtures ════════════════════════════════════

func _base(era := "office", what := "Software", who := "SMB") -> GameState:
	var s := GameState.new()
	s.sim_seed = 777001            # odd seed: no user:// read-marks collide
	s.week = 40
	s.era = era
	s.cash = 52_000
	s.traction = 180
	s.product = 62
	s.morale = 58
	s.hype = 44
	s.company_name = _x(CO_NAME, 24)
	s.founder_name = "Lena Voss"
	s.biz_what = what
	s.biz_who = who
	s.theta = SimEngine.default_theta(what, who)
	s.set_flag("launched")
	s.analytics_level = 2
	s.last_growth = 0.05
	s.price_mult = 1.0
	s.offers = [{"name": "the standing order", "unit": "per week", "fair_price": 24.0,
		"elasticity": 2.0, "unit_cost": 8.0, "price": 22.0, "price_set": true,
		"weight": 1.0}]
	s.set_meta("pnl", {"revenue": 3_960, "cogs": 720, "rent": 3_000, "payroll": 4_850,
		"infra": 220, "marketing": 2_750, "sales": 300, "care": 220, "rnd": 450,
		"office": 220, "offer_fixed": 90, "severance": 0, "recruiting": 0,
		"incident": 0, "liabilities_wk": 180, "interest": 240, "tax": 0,
		"burn": 13_240, "net": -9_280, "learning": 0.9})
	for w in range(1, 40):
		s.metric_history.append({"wk": w, "cash": 60_000 - w * 300,
			"customers": int(pow(w, 1.4)), "revenue": 60 * w, "burn": 900 + 30 * w,
			"morale": 70 - (w % 25), "hype": 20 + (w % 17),
			"net": 60 * w - (900 + 30 * w)})
	return s

func _crowd(s: GameState, n: int) -> void:
	var roles := ["engineer", "sales", "support", "ops", "designer", "manager"]
	var first := ["Mara", "Yusuf", "Hanne", "Nico", "Ines", "Ravi", "Lena", "Karl",
		"Sofia", "Emil", "Noor", "Anders", "Beatriz", "Otto", "Wanda", "Felix"]
	var last := ["Voss", "Adeyemi", "Skov", "Ferreira", "Cardoso", "Raman", "Beck",
		"Whitlock", "Okafor", "Lindqvist", "Marchetti", "Sato"]
	while s.employees.size() < n:
		var i := s.employees.size()
		s.employees.append({"name": "%s %s" % [first[i % first.size()], last[(i * 7) % last.size()]],
			"role": roles[i % roles.size()], "salary": 900 + (i % 9) * 190,
			"burnout": (i * 13) % 80, "skill": 1 + (i % 5), "hired_week": 2 + (i % 36),
			"quirk": ""})

## ═════════════════════════════════ the run ═══════════════════════════════════

func _go() -> void:
	await process_frame
	var d := OS.get_environment("RUNWAY_STRESS_DIR")
	if d != "":
		_dir = d

	await _spend_book10()
	await _team_rungs()
	await _works_5sites()
	await _make_wall34()
	await _history_80()
	await _events_25()
	await _raise_full()
	await _recruit_6()
	await _growth_topics()

	print("SCALE SHOTS: %d captures -> %s" % [_shots.size(), _dir])
	quit(0)

# ── (a) the 10-line spend book, names at 28, buys at 60 ──────────────────────
func _spend_book10() -> void:
	var s := _base("office", "Service", "Consumer")
	var names: Array[String] = [
		_x("front-of-house training week", 28),
		_x("the overnight bake rotation ", 28),
		_x("wholesale account managering", 28),
		_x("test-kitchen provisioning wk", 28),
		_x("customer complaint call-back", 28),
		_x("the delivery van maintenance", 28),
		_x("recipe refactoring afternoon", 28),
		_x("apprentice pastry programmes", 28),
		_x("spring fair booth + haulage ", 28),
		_x("staff meals + late taxi fund", 28),
	]
	var buys: Array[String] = [
		_x("keeps every demo environment warm before the buyer ever asks", 60),
		_x("someone answers the phone at 3am so the bakers never have to", 60),
		_x("the ovens proofed and loaded before the first tram roll past", 60),
		_x("flour, butter and yeast arriving before anyone has to charge", 60),
		_x("the corner tables wiped and the coffee machine bled by seven", 60),
		_x("a stand at the spring fair with the banner nobody can ignore", 60),
		_x("the new hires shadowing the head baker for their first month", 60),
		_x("every invoice chased politely twice and then by the founders", 60),
		_x("the vans fuelled, insured and parked where the police allow.", 60),
		_x("someone reads every complaint card before the morning shift.", 60),
	]
	var buckets := ["office", "care", "sales", "rnd", "care", "office", "rnd",
		"office", "sales", "office"]
	var amts := [140, 260, 1_240, 180, 90, 310, 120, 220, 480, 150]
	s.spend_book = []
	for i in 10:
		s.spend_book.append({"name": names[i], "buys": buys[i], "amt": amts[i],
			"bucket": buckets[i], "contract_notice": 4 if i == 1 else 0, "division": ""})
	SimSpendBook.adopt_book(s)
	SimSpendBook.stop_line(s, 1, s.week - 1)   # the on-call contract, billing through
	var b := await _open(s)
	_page(b, "spend")
	await _shot("spend_book10")

# ── (b) team at rung 2 (18, two askers) and rung 3 (45) ──────────────────────
func _team_rungs() -> void:
	var s := _base()
	_crowd(s, 18)
	for i in [3, 11]:
		var e: Dictionary = s.employees[i]
		e["wants_raise"] = true
		e["asked_week"] = 38 + (i % 2)
		e["underpaid_since"] = 30
	s.esop = {"pool_pct": 10.0, "granted": [
		{"emp_id": String((s.employees[0] as Dictionary)["name"]), "pct": 0.4, "vest_start_wk": 4},
		{"emp_id": String((s.employees[5] as Dictionary)["name"]), "pct": 0.2, "vest_start_wk": 30}]}
	var b := await _open(s)
	_page(b, "team")
	await _shot("team_rung2")
	_page(b, "team", {"fn_engineer": true})
	await _shot("team_rung2_open")

	var s3 := _base("hq")
	_crowd(s3, 45)
	for i in [7, 23]:
		var e3: Dictionary = s3.employees[i]
		e3["wants_raise"] = true
		e3["asked_week"] = 39
	var b3 := await _open(s3)
	_page(b3, "team")
	await _shot("team_rung3")
	_page(b3, "team", {"unit_open": "engineering", "u_engineering_engineer": true})
	await _shot("team_rung3_unit")

# ── (c) the works at rung 3: 5 sites, one bleeding ───────────────────────────
func _works_5sites() -> void:
	var s := _base("office", "Service", "SMB")
	s.cash = 150_000
	s.traction = 300
	s.price_book = {"open_site_pack": 18_000, "relocation_fee": 400,
		"machine_shipping": 900, "lease_break_weeks": 8, "contract_notice_wks": 4,
		"refinance_break_fee": 350, "freelance_rate": 48, "subcontract_rate": 30,
		"account_fire_penalty": 1_200}
	s.offers = []
	SimEngine.add_offer(s, "the classic 50", "per session", 80.0, 31.0, 2.0, 1.0,
		[{"label": "hands, 50 min", "amount": 22.0}, {"label": "oils & linens", "amount": 4.0}])
	SimEngine.add_offer(s, "the deep 90", "per session", 130.0, 52.0, 2.0, 0.8, [])
	SimEngine.add_offer(s, "house calls", "per session", 110.0, 47.0, 2.0, 0.6, [])
	for od in s.offers:
		(od as Dictionary)["price"] = float((od as Dictionary)["fair_price"])
		(od as Dictionary)["price_set"] = true
	s.sites = [
		{"id": "site_1", "name": "Lyon", "rent_wk": 1_100, "wage_mult": 0.92,
			"learning_count": 4_100, "demand_weight": 1.0, "opened_wk": 6},
		{"id": "site_2", "name": "Geneva", "rent_wk": 3_400, "wage_mult": 1.15,
			"learning_count": 900, "demand_weight": 0.8, "opened_wk": 12},
		{"id": "site_3", "name": "Basel", "rent_wk": 2_100, "wage_mult": 1.05,
			"learning_count": 640, "demand_weight": 0.7, "opened_wk": 18},
		{"id": "site_4", "name": "Turin", "rent_wk": 4_800, "wage_mult": 1.35,
			"learning_count": 0, "demand_weight": 0.2, "opened_wk": 30},
		{"id": "site_5", "name": "Marseille", "rent_wk": 1_500, "wage_mult": 0.9,
			"learning_count": 380, "demand_weight": 0.6, "opened_wk": 26},
	]
	var sitenames := ["", "site_1", "site_1", "site_2", "site_2", "site_3",
		"site_4", "site_5", "site_5"]
	var i := 0
	for nm in ["Nadia Beck", "Tomas Iri", "June Park", "Ines Rol", "Omar Datta",
			"Rae Ling", "Vera Kaas", "Noel Brandt", "Sam Oduya"]:
		s.employees.append({"name": nm, "role": "therapist", "salary": 1_350 + (i % 4) * 90,
			"burnout": 12, "quirk": "", "skill": 3 + (i % 3), "hired_week": 4,
			"site": sitenames[i]})
		i += 1
	SimDivisions._mark(s, "works_red", "site_4", 3)   # Turin bleeds, three weeks
	SimEngine.weekly_tick(s)
	var b := await _open(s)
	_page(b, "the works")
	await _shot("works_5sites")
	_page(b, "the works", {"row": "site_4"})
	await _shot("works_bleed_open")

# ── (d) what we make: 34 features, 2 creaks, the full pipeline ───────────────
func _feat(id: String, nm: String, job: String, keep: int, solidity := "solid",
		fam := "", born := 0, measured := 0.0, pid := "") -> Dictionary:
	return {"id": id, "name": nm, "job": job, "family": fam, "solidity": solidity,
		"keep_wk": keep, "unit_cost_add": float(keep) / 20.0, "product_id": pid,
		"born_wk": born, "measured": measured}

func _bet(s: GameState, id: String, nm: String, kind: String, amb: int,
		committed: bool, ready: bool, progress_frac := 0.0) -> Dictionary:
	var cost := SimRoadmap.bet_cost(kind, amb)
	var bet := {"id": id, "name": nm, "desc": "", "kind": kind, "ambition": amb,
		"cost_rnd_weeks": cost, "progress": cost * progress_frac,
		"committed": committed, "committed_week": s.week - 1 if (committed or ready) else 0,
		"ready": ready, "shipped": false, "shipped_week": 0, "band": "", "era": s.era}
	s.bets.append(bet)
	return bet

func _make_wall34() -> void:
	var s := _base("office", "Software", "SMB")
	s.week = 60
	s.company_idea = "schedules for small teams"
	s.tech_debt = 52.0
	var fams := {"the booking suite": "pull", "the invite loop": "pull",
		"reporting & exports": "keep", "the calendar core": "keep",
		"enterprise trust": "charge", "the billing stack": "charge",
		"the data platform": "plumbing", "sync & webhooks": "plumbing"}
	var i := 1
	for fam in fams:
		for k in 4:
			var nm := "%s %d" % [String(fam).split(" ")[String(fam).split(" ").size() - 1], k + 1]
			if i == 3:
				nm = "the multi-tenant permission grid"
			elif i == 17:
				nm = "offline conflict reconciliation"
			var solidity := "solid"
			if i == 6 or i == 29:
				solidity = "creaky"   # exactly two creaks on the wall
			s.features.append(_feat("f%d" % i, nm, String(fams[fam]), 2 + (i % 9),
				solidity, String(fam)))
			i += 1
	s.features.append(_feat("f_fresh", "the invite loop v2", "pull", 4, "solid", "",
		58, 8.2))
	s.features.append(_feat("f_loose", "the changelog digest", "keep", 3))
	_bet(s, "b_build1", "billing core rebuild", "debt", 1, true, false, 0.33)
	_bet(s, "b_build2", "team spaces", "reach", 3, true, false, 0.6)
	_bet(s, "b_ready", "integrations API", "quality", 2, false, true)
	_bet(s, "b_q1", "analytics pack", "retention", 2, false, false)
	_bet(s, "b_q2", "the referral loop", "reach", 1, false, false)
	SimFeatures.enqueue_bet(s, "b_q1")
	SimFeatures.enqueue_bet(s, "b_q2")
	var b := await _open(s)
	_page(b, "what we make")
	await _shot("make_wall34")

# ── (e) history: 80 weeks on the book, eras marked the engine's way ──────────
func _history_80() -> void:
	var s := _base("hq")
	s.week = 80
	s.metric_history = []
	for w in range(1, 80):
		var net := 60 * w - (1_200 + 25 * w)
		s.metric_history.append({"wk": w, "cash": 40_000 + w * 350 - (w % 7) * 900,
			"customers": int(pow(w, 1.35)), "revenue": 60 * w, "burn": 1_200 + 25 * w,
			"morale": 55 + (w % 20), "hype": 20 + (w % 17), "net": net})
	# the engine's own era stamps (game_state.log_action format, verbatim)
	s.history.append({"week": 20, "entry": "MOVED UP: garage → office (the lease signed)"})
	s.history.append({"week": 45, "entry": "MOVED UP: office → floor (the round closed)"})
	s.history.append({"week": 70, "entry": "MOVED UP: floor → hq (the lobby got a plant)"})
	s.history.append({"week": 77, "entry": "event 'The audit letter' — wrote: open the books"})
	for w2 in range(74, 80):
		s.run_history.append({"wk": w2, "said": "week %d's move, as written" % w2,
			"heard": "", "verdict": "fine", "roll": "d20=%d vs DC 9 (sell)" % (6 + w2 % 12),
			"fx": ["status: word_of_mouth for 2 wks — the demo landed",
				"spend $400 on one_off — the booth"]})
	s.run_history.append({"wk": 80,
		"said": "hold the price where it is and let the winter pass while the vans keep every standing order alive",
		"heard": "", "verdict": "fine", "roll": "d20=14 vs DC 9 (grit)",
		"fx": ["morale +2 — the crews liked being trusted"]})
	var b := await _open(s)
	_page(b, "history")
	await _shot("history_80wk")
	_page(b, "history", {"mode": "receipts", "wk": 77})
	await _shot("history_receipts")

# ── (f) events: a 25-letter inbox, one action letter old enough to test the
#     fold (attention never folds away) ───────────────────────────────────────
func _events_25() -> void:
	var s := _base()
	s.rivals = [
		{"name": "Vantage", "strength": 72.0, "what": "", "tactics": ["undercut"],
			"weeks_since_move": 1, "log": ["wk37: cut prices ~8%", "wk38: poach attempt",
				"wk39: quiet, watching"], "cooldowns": {}, "sniffing": 0},
		{"name": "Nimbus", "strength": 28.0, "what": "", "tactics": ["premium"],
			"weeks_since_move": 2, "log": ["wk36: stumbled — the demo crashed",
				"wk38: ad blitz", "wk39: hired a designer"], "cooldowns": {}, "sniffing": 0},
		{"name": "Lattice", "strength": 51.0, "what": "", "tactics": ["partner"],
			"weeks_since_move": 0, "log": ["wk35: raised a round", "wk38: shipped a clone",
				"wk40: asking about you"], "cooldowns": {}, "sniffing": 33},
	]
	s.applicants = [
		{"name": "Mara Voss", "role": "engineer", "skill": 4, "ask": 1_700,
			"applied_week": 38, "source": "inbound", "one_liner": ""},
		{"name": "Yusuf Adeyemi", "role": "engineer", "skill": 3, "ask": 1_250,
			"applied_week": 39, "source": "referral", "one_liner": ""},
		{"name": "Hanne Skov", "role": "sales", "skill": 5, "ask": 2_100,
			"applied_week": 40, "source": "inbound", "one_liner": ""},
	]
	s.employees = [
		{"name": "Priya Raman", "role": "engineer", "salary": 1_500, "burnout": 32,
			"skill": 4, "hired_week": 12, "quirk": "", "wants_raise": true,
			"asked_week": 37, "underpaid_since": 30},
		{"name": "Tomas Beck", "role": "sales", "salary": 1_100, "burnout": 51,
			"skill": 3, "hired_week": 19, "quirk": "", "wants_raise": true,
			"asked_week": 39, "underpaid_since": 33},
	]
	SimEngine.add_clock(s, 2, "the bridge loan comes due")
	SimEngine.add_clock(s, 4, "the lease renewal lands")
	SimEngine.add_clock(s, 6, "the covenant review")
	s.loans = [
		{"kind": "shark", "principal": 6_000, "balance": 4_100, "rate_wk": 0.08,
			"term_wk": 0, "taken_week": 5, "pay_wk": 328, "missed": 1},
		{"kind": "bank", "principal": 12_000, "balance": 9_400, "rate_wk": 0.04,
			"term_wk": 26, "taken_week": 31, "pay_wk": 570, "missed": 0},
	]
	s.instruments = [
		{"kind": "safe", "holder": "R. Osei", "amount": 60_000, "cap": 1_500_000,
			"discount": 0.2, "rate": 0.0, "maturity_wk": 0, "pct": 0.0, "prefs": 0.0,
			"protective": false, "drag_threshold": 0.0, "signed_wk": 9},
		{"kind": "priced", "holder": "Fern Capital", "amount": 400_000, "cap": 0,
			"discount": 0.0, "rate": 0.0, "maturity_wk": 0, "pct": 15.0, "prefs": 1.0,
			"protective": true, "drag_threshold": 60.0, "signed_wk": 31},
	]
	s.board = {"target_growth_pct": 35.0, "base_revenue": 1_300, "target_revenue": 1_800,
		"review_week": 44, "strikes": 1, "goodwill": 2}
	SimEngine.add_status(s, "funding_winter", 6)
	s.price_book = {"open_site_pack": 18_000, "relocation_fee": 400}
	s.mna = {"buyer": "Corvid Systems", "price": 3_100_000, "why": "they want the bench",
		"premium": 1.3, "expires_week": 42}
	s.mna_last_week = 38
	# 9 rival moves + 3 applications + 2 asks + 3 clocks + 2 loans + 2 papers
	# + 1 board + 1 weather + 1 price book + 1 buyout = 25 letters
	var b := await _open(s)
	_page(b, "events")
	await _shot("events_25")

# ── (g) the raise: all four columns populated, folds honest ──────────────────
func _raise_full() -> void:
	var s := _base()
	s.set_flag("fundraising_open")
	s.instruments = [
		{"kind": "safe", "holder": "R. Osei", "amount": 60_000, "cap": 1_500_000,
			"discount": 0.2, "rate": 0.0, "maturity_wk": 0, "pct": 0.0, "prefs": 0.0,
			"protective": false, "drag_threshold": 0.0, "signed_wk": 9},
		{"kind": "priced", "holder": "Fern Capital", "amount": 400_000, "cap": 0,
			"discount": 0.0, "rate": 0.0, "maturity_wk": 0, "pct": 15.0, "prefs": 1.0,
			"protective": true, "drag_threshold": 60.0, "signed_wk": 31},
	]
	s.rounds_raised = ["pre-seed"] as Array[String]
	s.board = {"target_growth_pct": 35.0, "base_revenue": 1_300, "target_revenue": 1_800,
		"review_week": 46, "strikes": 0, "goodwill": 2}
	s.raise_state = {"stages": [
		{"name": "Halden Ventures", "stage": "radar", "inbound": true, "arrived_wk": 38},
		{"name": "Ashgrove Capital", "stage": "radar", "inbound": true, "arrived_wk": 39},
		{"name": "Quay Partners", "stage": "radar", "inbound": false, "arrived_wk": 40},
		{"name": "Cormorant Capital", "stage": "conversations", "inbound": true,
			"arrived_wk": 36, "asked_wk": 38, "doubt": "the margin page bleeds"},
		{"name": "Bell & Weir", "stage": "conversations", "inbound": true,
			"arrived_wk": 35, "asked_wk": 39, "doubt": ""},
		{"name": "Ostra Ventures", "stage": "conversations", "inbound": false,
			"arrived_wk": 37, "asked_wk": 40, "doubt": "wants a second reference"},
		{"name": "Harborline Syndicate", "stage": "terms", "inbound": false,
			"arrived_wk": 32, "terms": {"kind": "priced", "valuation": 2_500_000,
				"amount": 500_000, "pct": 16.7, "prefs": 1.0, "participating": false,
				"protective": true, "drag_threshold": 60.0, "board_seat": true,
				"no_shop_wks": 4, "pool_topup_pct": 10.0, "expires_wk": 44}},
		{"name": "Meridian Growth", "stage": "terms", "inbound": true,
			"arrived_wk": 33, "terms": {"kind": "safe", "amount": 250_000,
				"cap": 3_000_000, "discount": 0.2, "expires_wk": 43}},
		{"name": "Pillar House", "stage": "terms", "inbound": true,
			"arrived_wk": 34, "terms": {"kind": "safe", "amount": 150_000,
				"cap": 2_600_000, "discount": 0.15, "expires_wk": 45}},
	], "interest_score": 61.0, "active": true, "founder_time_tax": 0.3}
	var b := await _open(s)
	_page(b, "the raise")
	await _shot("raise_full")

# ── (h) recruitment: six candidates across the wall ──────────────────────────
func _recruit_6() -> void:
	var s := _base()
	s.recruitment = {"roles": [
		{"id": "role_sales_36", "seat": "salesperson #2", "band_lo": 1_400,
			"band_hi": 2_060, "advert_wk": 60, "opened_wk": 36},
		{"id": "role_care_37", "seat": "care lead", "band_lo": 980,
			"band_hi": 1_440, "advert_wk": 40, "opened_wk": 37},
		{"id": "role_eng_38", "seat": "second engineer", "band_lo": 1_600,
			"band_hi": 2_400, "advert_wk": 80, "opened_wk": 38}],
		"candidates": [
		{"id": "c1", "role_id": "role_sales_36", "name": "Priya Nair", "ask": 1_540,
			"profile": "missionary", "skill": 3, "stage": "applied", "arrived_wk": 39},
		{"id": "c2", "role_id": "role_eng_38", "name": "Tom Burrell", "ask": 2_150,
			"profile": "mercenary", "skill": 4, "stage": "applied", "arrived_wk": 40},
		{"id": "c3", "role_id": "role_sales_36", "name": "Dana Kovic", "ask": 1_540,
			"profile": "mercenary", "skill": 4, "stage": "interviewed", "arrived_wk": 38},
		{"id": "c4", "role_id": "role_eng_38", "name": "Sol Andrade", "ask": 1_980,
			"profile": "missionary", "skill": 3, "stage": "interviewed", "arrived_wk": 39},
		{"id": "c5", "role_id": "role_care_37", "name": "Ada Whitlock", "ask": 1_200,
			"profile": "missionary", "skill": 4, "stage": "offer", "arrived_wk": 37},
		{"id": "c6", "role_id": "role_sales_36", "name": "June Novak", "ask": 1_450,
			"profile": "missionary", "skill": 3, "stage": "joined", "arrived_wk": 33}],
		"offers_out": [{"candidate_id": "c5", "cash_wk": 1_240, "options_pct": 0.3,
			"expires_wk": 41, "sent_wk": 40}]}
	var b := await _open(s)
	_page(b, "recruitment")
	await _shot("recruit_6")

# ── the garden wearing 110-char topic one-liners ─────────────────────────────
func _growth_topics() -> void:
	var s := _base("office", "Service", "Consumer")
	s.budgets = {"ads": 1_200, "content": 400, "referrals": 250, "outbound": 600,
		"sales": 300, "care": 200, "rnd": 200, "office": 200}
	s.topics = {"growth": {
		"ads": {"name": "the market stall",
			"line": _x("the morning market stall keeps selling out before nine because the neighbourhood now plans breakfast around it", 110)},
		"content": {"name": "the loyalty card",
			"line": _x("a hand-stamped loyalty card that regulars fill in twelve visits and then quietly hand on to their own friends.", 110)},
		"referrals": {"name": "the long counter",
			"line": _x("the long counter where the head baker teaches sourdough patience to anyone who buys the early winter workshops", 110)},
		"outbound": {"name": "the rye vans",
			"line": _x("vans that smell of warm rye idling outside offices at eight, taking standing orders from the same twenty desks", 110)},
	}}
	var b := await _open(s)
	_page(b, "growth")
	await _shot("growth_topics110")
