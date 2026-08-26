extends SceneTree
## DESK SHOTS — every binder surface the nine lanes added, in every state that
## renders differently. The tab shot (tests/binder_shot.gd) photographs ten tabs
## in one mid-game state; this one drives each desk's STATE MACHINE — catalog
## LIST/DETAIL/WRITE/WAIT/REVIEW/war, crew ROSTER/PERSON/HIRING, bank DESK/BOOKS/
## locked, product board/armed/preroll/receipt/bench, customers at every
## analytics gate and the Enterprise board, cap with pool/covenant/offer, the
## ledger's full stack, threats spillover, and every authored empty state.
##
## Run: RUNWAY_STRESS_DIR=<dir> godot --path . --script tests/desk_shots.gd
## Files land as <surface>_<state>_godot.png so the Unity twin can sit beside them.

var _dir := "/tmp"
var _b: Binder = null
var _shots: Array[String] = []

func _init() -> void:
	call_deferred("_go")

# ═══════════════════════════ the shot harness ════════════════════════════════

func _shot(nm: String) -> void:
	await create_timer(0.25).timeout
	await RenderingServer.frame_post_draw
	root.get_viewport().get_texture().get_image().save_png("%s/%s_godot.png" % [_dir, nm])
	_shots.append(nm)
	print("SHOT %s" % nm)

## Open a fresh binder on a state. The old one is freed first: desk-local state
## dies with the node, which is exactly the contract the desks are written to.
func _open(s: GameState, gen = null) -> Binder:
	if _b != null and is_instance_valid(_b):
		_b.queue_free()
		await process_frame
	var b := Binder.new()
	b.setup(s, gen)
	root.add_child(b)
	b.size = Vector2(1536, 1024)
	await create_timer(0.30).timeout
	_b = b
	return b

## Point the binder at a tab with a desk-local state dict, then rebuild.
func _tab(b: Binder, i: int, st: Dictionary = {}) -> void:
	b.set("_tab", i)
	b.desk.clear()
	for k in st:
		b.desk[k] = st[k]
	b.refresh()

# ═══════════════════════════════ fixtures ════════════════════════════════════

## A live company at the office era: money moving, people on the books, debt
## filed, rivals circling, a board watching. The base every scenario bends.
func _mid(era := "office") -> GameState:
	var s := GameState.new()
	s.sim_seed = 99
	s.week = 34
	s.era = era
	s.cash = 48_600
	s.traction = 124
	s.product = 62
	s.morale = 58
	s.hype = 44
	s.level = 3
	s.founder_name = "Lena Voss"
	s.company_name = "Mossflow"
	s.biz_what = "Software"
	s.biz_who = "SMB"
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	s.analytics_level = 3
	s.tech_debt = 46.0
	s.exhaustion = 2
	s.last_growth = 0.06
	s.founder_pct = 54.0
	s.option_pool_pct = 10.0
	s.price_mult = 1.0
	s.competences = {"build": 4, "sell": 3, "raise": 3, "recruit": 2, "grit": 4}
	s.budgets = {"ads": 2000, "content": 500, "referrals": 250, "outbound": 1000,
		"sales": 1000, "care": 500, "rnd": 2000, "office": 500}
	s.offers = [
		{"name": "pocket synth", "unit": "per unit", "fair_price": 20.0,
			"elasticity": 2.4, "unit_cost": 9.0, "price": 18.0, "price_set": true,
			"weight": 1.0, "fixed_wk": 40.0,
			"cost_lines": [{"label": "components", "amount": 6.0},
				{"label": "packing + delivery", "amount": 3.0}],
			"fixed_lines": [{"label": "bench rental", "amount": 40.0}]},
		{"name": "calibration kit", "unit": "per kit", "fair_price": 60.0,
			"elasticity": 1.8, "unit_cost": 22.0, "price": 0.0, "weight": 0.6,
			"fixed_wk": 15.0,
			"cost_lines": [{"label": "reference cell", "amount": 14.0},
				{"label": "courier", "amount": 8.0}],
			"fixed_lines": [{"label": "calibration lab", "amount": 15.0}]},
		{"name": "support retainer", "unit": "per month", "fair_price": 240.0,
			"elasticity": 1.2, "unit_cost": 90.0, "price": 900.0, "price_set": true,
			"weight": 0.4, "fixed_wk": 60.0,
			"cost_lines": [{"label": "on-call hours", "amount": 90.0}],
			"fixed_lines": [{"label": "helpdesk seat", "amount": 60.0}]},
	]
	# THE DRAFT STORES THE ROLE AS ITS CARD INDEX (`{"role": i}`), so the fixture
	# carries an int and the desk has to be the one that turns it into a word.
	s.cofounders = [{"role": 2, "commitment": 1, "equity": 18.0,
		"equity_diluted": 18.0, "vesting": true, "name": "Nico Ferreira"}]
	s.employees = [
		{"name": "Priya Raman", "role": "engineer", "salary": 1500, "burnout": 32.0,
			"skill": 4, "hired_week": 12, "quirk": "ships on fridays, apologises on mondays"},
		{"name": "Tomas Beck", "role": "sales", "salary": 1100, "burnout": 51.0,
			"skill": 3, "hired_week": 19, "wants_raise": true, "asked_week": 32,
			"underpaid_since": 27, "quirk": "negotiates via long silences"},
		{"name": "Ada Whitlock", "role": "designer", "salary": 1350, "burnout": 18.0,
			"skill": 5, "hired_week": 24, "quirk": "redraws the logo when nervous"},
	]
	s.investors = [
		{"name": "Harborline Syndicate", "archetype": "the operator VC",
			"thesis": "small teams that charge properly beat big teams that grow badly",
			"trait": "carries a stopwatch and notices queue lengths before introductions",
			"coords": [-0.3, 0.2]},
		{"name": "Bell & Weir", "archetype": "the contrarian",
			"thesis": "the boring middle of a market is where the durable margin hides",
			"trait": "asks for the churn number before the pitch starts", "coords": [0.1, 0.6]},
	]
	s.rivals = [
		{"name": "Vantage", "strength": 72.0, "what": "legacy suite via trade associations",
			"tactics": ["undercut", "poach", "ship"], "weeks_since_move": 1,
			"log": ["wk31: cut prices ~8%", "wk32: quiet", "wk33: poach attempt"]},
		{"name": "Nimbus", "strength": 28.0, "what": "a thin wrapper with a loud blog",
			"tactics": ["premium", "partner"], "weeks_since_move": 2,
			"log": ["wk32: stumbled — the demo crashed", "wk33: quiet"]},
	]
	s.loans = [
		{"kind": "bank", "principal": 10_000, "balance": 8_215, "rate_wk": 0.04,
			"term_wk": 8, "taken_week": 30, "pay_wk": 1_486, "missed": 1},
	]
	s.loan_principal = 12_400
	s.rounds_raised = ["pre-seed"] as Array[String]
	s.board = {"target_growth_pct": 35.0, "base_revenue": 1_300, "target_revenue": 1_800,
		"review_week": 39, "strikes": 1, "goodwill": 2}
	s.board_seats_investor = 1
	s.beliefs = {"tam": 100_000.0, "lifetime_wk": 40.0}
	s.set_meta("pnl", {"revenue": 3_420, "cogs": 760, "rent": 3_000, "payroll": 3_950,
		"infra": 220, "marketing": 3_750, "sales": 1_000, "care": 500, "rnd": 2_000,
		"office": 500, "offer_fixed": 115, "severance": 0, "recruiting": 1_500,
		"incident": 240, "liabilities_wk": 180, "interest": 329, "tax": 0,
		"burn": 16_715, "net": -13_624, "learning": 0.89})
	s.set_meta("bank_principal_wk", 1_157)
	s.set_meta("unit_econ", {"arpu": 27.6, "cac": 310, "ltv": 1_104, "payback_wk": 11})
	SimEngine.add_status(s, "investor_pressure", 2)
	SimEngine.add_status(s, "word_of_mouth", 3)
	SimEngine.add_clock(s, 3, "the bridge loan comes due")
	s.commitments = [{"name": "the trade-show booth", "cash_wk": 180, "weeks_left": 4}]
	for w in range(1, 34):
		s.metric_history.append({"week": w, "cash": 60_000 - w * 400,
			"customers": int(pow(w, 1.5)), "morale": 74 - w, "product": 12 + w * 1.5,
			"revenue": 40 * w, "burn": 900 + 40 * w, "hype": 20 + (w % 17),
			"net": 40 * w - (900 + 40 * w)})
	return s

## The funnel's own numbers, so the customers desk has bars and CAC to draw.
func _with_funnel(s: GameState) -> void:
	s.set_meta("funnel", {"reach_total": 1_240.0, "leads_total": 96.0, "adds": 11.0,
		"conv": 0.077, "close_rate": 0.78, "gtm_cap": 14.0, "blended_cac": 310.0,
		"spend_ads": 2_000.0, "cac_ads": 82.0, "spend_content": 500.0, "cac_content": 41.0,
		"spend_referrals": 250.0, "cac_referrals": 0.0,
		"spend_outbound": 1_000.0, "cac_outbound": 0.0})

# ═════════════════════════════════ the run ═══════════════════════════════════

func _go() -> void:
	await process_frame
	var d := OS.get_environment("RUNWAY_STRESS_DIR")
	if d != "":
		_dir = d

	await _catalog()
	await _crew()
	await _bank()
	await _customers()
	await _product()
	await _cap()
	await _street()
	await _ledger()
	await _threats_vitals()

	print("DESK SHOTS: %d captures -> %s" % [_shots.size(), _dir])
	quit(0)

# ── pricing ──────────────────────────────────────────────────────────────────
func _catalog() -> void:
	var s := _mid()
	var b := await _open(s)
	_tab(b, 3)
	await _shot("catalog_list")
	_tab(b, 3, {"mode": "detail", "row": 0})
	await _shot("catalog_detail")
	_tab(b, 3, {"mode": "detail", "row": 0, "armed": "drop"})
	await _shot("catalog_detail_armed")
	_tab(b, 3, {"mode": "write", "text": "a monthly meal-prep box for people who cook once and eat five times"})
	await _shot("catalog_write")
	_tab(b, 3, {"mode": "write", "text": "a"})
	b.desk["short"] = true
	b.refresh()
	await _shot("catalog_write_short")
	_tab(b, 3, {"mode": "wait", "text": "a monthly meal-prep box"})
	await _shot("catalog_wait")
	var p := DeskCatalog._proposal(s, SimCatalog.draft_terms(s, "a monthly meal-prep box"))
	_tab(b, 3, {"mode": "review", "pending": p.duplicate(true), "text": "a monthly meal-prep box"})
	await _shot("catalog_review")
	_tab(b, 3, {"mode": "review", "pending": p.duplicate(true), "house": true,
		"text": "a monthly meal-prep box"})
	await _shot("catalog_review_keyless")
	_tab(b, 3, {"mode": "review", "pending": p.duplicate(true), "house": true,
		"refused": "the shelf is full at this stage — drop something first"})
	await _shot("catalog_review_refused")

	# THE PRICE WAR: a rival's cut moves the street's reference, and every
	# verdict on the sheet is measured against the lower number.
	var sw := _mid()
	SimEngine.add_status(sw, "price_war", 3)
	var bw := await _open(sw)
	_tab(bw, 3)
	await _shot("catalog_list_war")

	# THE GARAGE: totals mode, one number per sheet, no fine print yet.
	var sg := _mid("garage")
	sg.cash = 4_200
	sg.traction = 9
	sg.offers = [sg.offers[0], sg.offers[1]]
	var bg := await _open(sg)
	_tab(bg, 3, {"mode": "detail", "row": 0})
	await _shot("catalog_detail_garage")

	# EMPTY: the world has not written the offers yet.
	var se := _mid("garage")
	se.offers = []
	var be := await _open(se)
	_tab(be, 3)
	await _shot("catalog_empty")

# ── crew ─────────────────────────────────────────────────────────────────────
func _crew() -> void:
	var s := _mid()
	s.pipeline = [{"name": "Ines Cardoso", "role": "support", "salary": 900, "weeks_in": 1}]
	var b := await _open(s)
	_tab(b, 6)
	await _shot("crew_roster")
	_tab(b, 6, {"mode": "person", "row": 1})
	await _shot("crew_person")
	_tab(b, 6, {"mode": "person", "row": 1, "armed": "letgo_1"})
	await _shot("crew_person_armed")

	# HIRING: two open roles, applicants waiting, a recruiter on retainer.
	var sh := _mid()
	sh.open_roles = [
		{"role": "engineer", "offered_salary": 1_400, "opened_week": 30, "seats": 1},
		{"role": "sales", "offered_salary": 700, "opened_week": 32, "seats": 1},
	]
	sh.applicants = [
		{"name": "Mara Voss", "role": "engineer", "skill": 4, "ask": 1_700,
			"quirk": "negotiates via long silences", "applied_week": 32, "source": "inbound",
			"one_liner": "negotiates via long silences"},
		{"name": "Yusuf Adeyemi", "role": "engineer", "skill": 3, "ask": 1_250,
			"applied_week": 33, "source": "referral",
			"one_liner": "keeps a notebook of every outage he has ever seen"},
		{"name": "Hanne Skov", "role": "sales", "skill": 5, "ask": 2_100,
			"applied_week": 31, "source": "inbound",
			"one_liner": "has sold to three of your rivals and remembers every price"},
	]
	sh.recruiters = 1
	var bh := await _open(sh)
	_tab(bh, 6, {"mode": "hiring"})
	await _shot("crew_hiring")

	# HIRING, NOBODY ANSWERING: a role open under the market rate.
	var sq := _mid()
	sq.open_roles = [{"role": "engineer", "offered_salary": 700, "opened_week": 33, "seats": 1}]
	var bq := await _open(sq)
	_tab(bq, 6, {"mode": "hiring"})
	await _shot("crew_hiring_silent")

	# HIRING, NO ROLES OPEN — the authored empty line.
	var sn := _mid()
	var bn := await _open(sn)
	_tab(bn, 6, {"mode": "hiring"})
	await _shot("crew_hiring_empty")

	# THE GARAGE GATE: no market to advertise into, taught rather than greyed.
	var sgr := _mid("garage")
	sgr.employees = []
	var bgr := await _open(sgr)
	_tab(bgr, 6, {"mode": "hiring"})
	await _shot("crew_hiring_garage")
	_tab(bgr, 6)
	await _shot("crew_roster_empty")

# ── the bank ─────────────────────────────────────────────────────────────────
func _bank() -> void:
	var s := _mid()
	var b := await _open(s)
	_tab(b, 2)
	await _shot("bank_desk")
	_tab(b, 2, {"armed": "repay_0"})
	await _shot("bank_desk_armed")
	_tab(b, 2, {"mode": "books"})
	await _shot("bank_books")

	# VENTURE DEBT rides the same block once a round has closed (floor+).
	var sv := _mid("floor")
	sv.cash = 320_000
	sv.traction = 640
	sv.rounds_raised = ["pre-seed", "seed"] as Array[String]
	sv.last_round_amount = 2_400_000
	sv.receivables = [{"name": "Quill Health", "cash_wk": 3_100, "weeks_left": 2}]
	sv.tax_loss_carry = 41_000
	var bv := await _open(sv)
	_tab(bv, 2)
	await _shot("bank_desk_venture")
	_tab(bv, 2, {"mode": "books"})
	await _shot("bank_books_floor")

	# LOCKED: a note in default, the whole borrow block dimmed with its reason.
	var sl := _mid()
	sl.cash = 900
	(sl.loans[0] as Dictionary)["missed"] = 3
	var bl := await _open(sl)
	_tab(bl, 2)
	await _shot("bank_desk_locked")

	# THE GARAGE: no bank answers, and the shark is the whole lesson.
	var sg := _mid("garage")
	sg.cash = 3_100
	sg.loans = []
	var bg := await _open(sg)
	_tab(bg, 2)
	await _shot("bank_desk_garage")
	_tab(bg, 2, {"mode": "books"})
	await _shot("bank_books_empty")

	# OWING NOBODY — the authored empty line.
	var sd := _mid()
	sd.loans = []
	sd.loan_principal = 0
	var bd := await _open(sd)
	_tab(bd, 2)
	await _shot("bank_desk_debtfree")

# ── customers ────────────────────────────────────────────────────────────────
func _customers() -> void:
	for lvl in [0, 1, 2, 3]:
		var s := _mid()
		s.analytics_level = lvl
		_with_funnel(s)
		var b := await _open(s)
		_tab(b, 4)
		await _shot("customers_analytics%d" % lvl)

	# THE ERA GATE: bought the level, the garage still cannot see the funnel.
	var sg := _mid("garage")
	sg.analytics_level = 2
	_with_funnel(sg)
	var bg := await _open(sg)
	_tab(bg, 4)
	await _shot("customers_era_gate")

	# NO WEEK ON THE BOOKS: the funnel is measured, never predicted.
	var sn := _mid()
	sn.set_meta("funnel", {})
	var bn := await _open(sn)
	_tab(bn, 4)
	await _shot("customers_no_week")

	# ENTERPRISE: the wall calendar, chips in four columns, logos signed.
	var se := _mid()
	se.biz_who = "Enterprise"
	se.leads = [
		{"name": "Vanta Systems", "flavor": "the security team found you first",
			"seats": 6, "stage": "meeting", "age_weeks": 1, "heat": 62},
		{"name": "Ashby & Sons", "flavor": "a family firm with a new CFO",
			"seats": 9, "stage": "meeting", "age_weeks": 5, "heat": 14},
		{"name": "Meridian Logistics", "flavor": "forty depots, one spreadsheet",
			"seats": 40, "stage": "pilot", "age_weeks": 3, "heat": 88},
		{"name": "Corvid Freight", "flavor": "", "seats": 22, "stage": "procurement",
			"age_weeks": 6, "heat": 55},
		{"name": "Quill Health", "flavor": "", "seats": 12, "stage": "contract",
			"age_weeks": 6, "heat": 91},
	]
	se.logos = [{"name": "Quill Health", "seats": 12, "since_wk": 24, "renewal_wk": 37},
		{"name": "Fernbay Group", "seats": 9, "since_wk": 20, "renewal_wk": 72}]
	se.pipe_units = 12.0
	se.pipe_stats = {"signed": 4, "lost": 7, "cycle_sum": 28, "seats_signed": 21,
		"spend": 6_500.0, "first_wk": 8}
	var be := await _open(se)
	_tab(be, 4)
	await _shot("customers_enterprise")

	# THE EMPTY BOARD — the authored line, at the garage where there is no
	# procurement column yet.
	var sb := _mid("garage")
	sb.biz_who = "Enterprise"
	sb.pipe_units = 3.0
	var bb := await _open(sb)
	_tab(bb, 4)
	await _shot("customers_enterprise_empty")

# ── product ──────────────────────────────────────────────────────────────────
func _product() -> void:
	var s := _mid()
	SimRoadmap.ensure_board(s)
	var b := await _open(s)
	_tab(b, 5)
	await _shot("product_board")
	var bets := SimRoadmap.board_bets(s)
	if not bets.is_empty():
		var id := String((bets[0] as Dictionary).get("id", ""))
		_tab(b, 5, {"armed": "on:" + id})
		await _shot("product_board_armed")

	# COMMITTED: the vessel fills, the ETA is honest, standing down is armed.
	var sc := _mid()
	SimRoadmap.ensure_board(sc)
	var cb := SimRoadmap.board_bets(sc)
	if not cb.is_empty():
		var cd: Dictionary = cb[0]
		cd["committed"] = true
		cd["progress"] = float(cd.get("cost_rnd_weeks", 6.0)) * 0.62
		var bc := await _open(sc)
		_tab(bc, 5)
		await _shot("product_committed")
		_tab(bc, 5, {"armed": "down:" + String(cd.get("id", ""))})
		await _shot("product_committed_armed")

	# READY: the held breath, then the pre-roll review the SHIP press raises.
	var sr := _mid()
	SimRoadmap.ensure_board(sr)
	var rb := SimRoadmap.board_bets(sr)
	if not rb.is_empty():
		var rd: Dictionary = rb[0]
		rd["committed"] = true
		rd["ready"] = true
		rd["progress"] = float(rd.get("cost_rnd_weeks", 6.0))
		rd["ready_week"] = sr.week
		var br := await _open(sr)
		_tab(br, 5)
		await _shot("product_ready")
		_tab(br, 5, {"mode": "preroll", "bet": String(rd.get("id", ""))})
		await _shot("product_preroll")
		_tab(br, 5, {"mode": "shipped", "ship": {
			"event": "COLD-START FIX shipped clean",
			"band": "fine", "d20": 14, "mod": 3, "dc": 9, "total": 17,
			"lines": ["product +6 — the empty first day is gone",
				"tech debt +4 — two shortcuts stayed in",
				"hype +9 — the changelog got quoted",
				"customers +5 — the ones who bounced came back"]}})
		await _shot("product_shipped")

	# HARDWARE: the bench takes the bottom band and the cards cap at two.
	var sh := _mid()
	sh.biz_what = "Hardware"
	sh.hardware = {"stock": 34, "capacity_base": 6.0,
		"equipment": [
			{"id": "jig", "name": "Assembly Jig", "capacity_add": 6.0, "upkeep_wk": 15.0},
			{"id": "jig", "name": "Assembly Jig", "capacity_add": 6.0, "upkeep_wk": 15.0},
			{"id": "pick_place", "name": "Benchtop Pick-and-Place",
				"capacity_add": 18.0, "upkeep_wk": 60.0}],
		"production_target": -1, "produced_total": 1_240,
		"subcontract_on": true, "demand_ema": 19.4}
	sh.set_meta("hw", {"week": 33, "built": 24, "capacity": 30.0, "utilization": 0.79,
		"unit_cost_eff": 16.02, "down_name": "", "down_i": -1, "repair": 0.0,
		"sub_units": 3, "lost_adds": 0, "fill": 1.0, "sold": 21, "served": 21,
		"shelf": 34, "stock_end": 34, "carrying": 12.0, "upkeep": 90.0, "walked": 0})
	SimRoadmap.ensure_board(sh)
	var bh := await _open(sh)
	_tab(bh, 5)
	await _shot("product_bench")

	# THE BENCH WITH A MACHINE DOWN, and the sell arm quoting its haircut.
	var sd := _mid()
	sd.biz_what = "Hardware"
	sd.cash = 2_400
	sd.hardware = (sh.hardware as Dictionary).duplicate(true)
	(sd.hardware as Dictionary)["stock"] = 260
	sd.set_meta("hw", {"week": 33, "built": 6, "capacity": 24.0, "utilization": 0.25,
		"unit_cost_eff": 16.02, "down_name": "Benchtop Pick-and-Place", "down_i": 2,
		"repair": 240.0, "sub_units": 0, "lost_adds": 4, "fill": 0.62, "sold": 9,
		"served": 9, "shelf": 260, "stock_end": 260, "carrying": 92.0,
		"upkeep": 90.0, "walked": 4})
	SimRoadmap.ensure_board(sd)
	var bd := await _open(sd)
	_tab(bd, 5, {"armed": "sell_machine"})
	await _shot("product_bench_down_armed")

	# THE GARAGE BENCH: no machines at all — the founder's hands are the line.
	var sgh := _mid("garage")
	sgh.biz_what = "Hardware"
	sgh.cash = 5_600
	SimRoadmap.ensure_board(sgh)
	var bgh := await _open(sgh)
	_tab(bgh, 5)
	await _shot("product_bench_garage")

# ── cap table ────────────────────────────────────────────────────────────────
func _cap() -> void:
	var s := _mid()
	var b := await _open(s)
	_tab(b, 7)
	await _shot("cap_pool")

	# THE OFFER BANNER, above the board block, with the no-shop clock running.
	var sm := _mid()
	sm.mna = {"buyer": "Corvid Systems", "price": 3_100_000, "why": "they want the bench",
		"premium": 1.3, "expires_week": sm.week + 2}
	var bm := await _open(sm)
	_tab(bm, 7)
	await _shot("cap_offer")

	# TERM SHEETS ON THE TABLE + a winter pricing the raise line.
	var st := _mid()
	st.set_flag("fundraising_open")
	SimEngine.add_status(st, "funding_winter", 6)
	var bt := await _open(st)
	_tab(bt, 7)
	await _shot("cap_termsheets_winter")

	# THE IPO WINDOW, at hq, with three strikes on the track.
	var si := _mid("hq")
	si.cash = 4_200_000
	si.traction = 8_400
	si.rounds_raised = ["pre-seed", "seed", "series_a"] as Array[String]
	si.board = {"target_growth_pct": 35.0, "base_revenue": 90_000,
		"target_revenue": 120_000, "review_week": 36, "strikes": 2, "goodwill": 0}
	si.board_seats_investor = 3
	si.founder_pct = 31.0
	si.founder_banked = 400_000
	si.set_flag("ipo_window")
	si.set_meta("cap_renewal_line", "renewals: Fernbay Group (9 seats) comes up in 3 wks")
	var bi := await _open(si)
	_tab(bi, 7)
	await _shot("cap_ipo_hq")

	# THE BOOTSTRAP FLEX: no board, no rounds, the clean page.
	var sb := _mid("garage")
	sb.board = {}
	sb.rounds_raised = [] as Array[String]
	sb.founder_pct = 82.0
	sb.option_pool_pct = 0.0
	var bb := await _open(sb)
	_tab(bb, 7)
	await _shot("cap_bootstrap")

# ── the street ───────────────────────────────────────────────────────────────
func _street() -> void:
	var s := _mid()
	SimEngine.add_status(s, "funding_winter", 6)
	s.market_trend = 0.88
	var b := await _open(s)
	_tab(b, 8)
	await _shot("street_winter")

	# A BOOM, three rivals, and long theses — the page that has to compress.
	var sb := _mid("hq")
	SimEngine.add_status(sb, "boom", 4)
	sb.market_trend = 1.18
	sb.rivals.append({"name": "Lattice Dynamics", "strength": 55.0,
		"what": "a well-funded disruptor",
		"tactics": ["price", "poach", "partner", "ship"], "weeks_since_move": 0,
		"log": ["wk31: raised a round", "wk32: hired your designer", "wk33: shipped a clone"]})
	sb.investors.append({"name": "Ashgrove Capital", "archetype": "momentum",
		"thesis": "whatever is compounding fastest deserves the next cheque, and the one after that",
		"trait": "reads the weekly numbers before breakfast", "coords": [0.4, -0.2]})
	var bb := await _open(sb)
	_tab(bb, 8)
	await _shot("street_boom_crowded")

	# THE QUIET WEEK: nobody competing, the authored empty line.
	var sq := _mid("garage")
	sq.rivals = []
	sq.investors = []
	var bq := await _open(sq)
	_tab(bq, 8)
	await _shot("street_empty")

# ── the ledger ───────────────────────────────────────────────────────────────
func _ledger() -> void:
	var s := _mid()
	var b := await _open(s)
	_tab(b, 1)
	await _shot("ledger_full")

	# THE RED: both warnings fire, and the unit-econ line yields its slot.
	var sr := _mid()
	sr.cash = -2_400
	sr.weeks_in_red = 2
	sr.budgets["ads"] = 4_000
	var br := await _open(sr)
	_tab(br, 1)
	await _shot("ledger_red")

	# THE FIRST WEEK: no P&L on the books yet.
	var sf := _mid("garage")
	sf.remove_meta("pnl")
	sf.remove_meta("unit_econ")
	sf.budgets = {"ads": 0, "content": 0, "referrals": 0, "outbound": 0,
		"sales": 0, "care": 0, "rnd": 0, "office": 0}
	var bf := await _open(sf)
	_tab(bf, 1)
	await _shot("ledger_fresh")

	# THE ERA CEILING: the mix pinned at the garage cap, every row saying why.
	var sc := _mid("garage")
	sc.budgets = {"ads": 4_000, "content": 1_000, "referrals": 500, "outbound": 500,
		"sales": 250, "care": 250, "rnd": 250, "office": 250}
	var bc := await _open(sc)
	_tab(bc, 1)
	await _shot("ledger_ceiling")

# ── threats + vitals ─────────────────────────────────────────────────────────
func _threats_vitals() -> void:
	var s := _mid()
	s.set_flag("fundraising_open")
	s.offers[1]["price"] = 0.0
	var b := await _open(s)
	_tab(b, 9)
	await _shot("threats_spill")
	_tab(b, 0)
	await _shot("vitals_mid")

	# NOTHING TICKING — the authored empty line.
	var sq := _mid("garage")
	sq.clocks = []
	sq.statuses = []
	sq.commitments = []
	sq.offers[1]["price"] = 30.0
	sq.offers[1]["price_set"] = true
	sq.loans = []
	sq.loan_principal = 0
	sq.remove_meta("pnl")
	var bq := await _open(sq)
	_tab(bq, 9)
	await _shot("threats_empty")
	_tab(bq, 0)
	await _shot("vitals_garage")
