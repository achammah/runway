extends SceneTree
## L-COMPANY DESK SHOTS — the six reworked desks (the street, threats, pivot,
## this week, history, events) in their live states, plus THE COMPANY and
## THE LOG group overviews with the lane's hero providers filled.
##
## Run: RUNWAY_STRESS_DIR=<dir> godot --path . --script tests/shots_company.gd
## Files land as company_<surface>_godot.png. PNGs are local-only (gitignored).

var _dir := "/tmp"
var _b: Binder = null
var _shots: Array[String] = []

func _init() -> void:
	call_deferred("_go")

func _shot(nm: String) -> void:
	await create_timer(0.25).timeout
	await RenderingServer.frame_post_draw
	root.get_viewport().get_texture().get_image().save_png(
		"%s/company_%s_godot.png" % [_dir, nm])
	_shots.append(nm)
	print("SHOT %s" % nm)

## A fresh binder on a state; tour off so the probe photographs raw pages.
func _open(s: GameState) -> Binder:
	if _b != null and is_instance_valid(_b):
		_b.queue_free()
		await process_frame
	var b := Binder.new()
	b.tour_enabled = false
	b.setup(s, null)
	root.add_child(b)
	b.size = Vector2(1536, 1024)
	await create_timer(0.30).timeout
	_b = b
	return b

func _page(b: Binder, id: String, st: Dictionary = {}) -> void:
	b.open_page(id)
	b.desk.clear()
	for k in st:
		b.desk[k] = st[k]
	b.refresh()

## A live office-era company with everything my six desks read: rivals with
## records, weather, deadlines, mail-worthy paper, and a real book of weeks.
func _mid() -> GameState:
	var s := GameState.new()
	s.sim_seed = 99
	s.week = 34
	s.era = "office"
	s.cash = 48_600
	s.traction = 124
	s.product = 62
	s.morale = 58
	s.hype = 44
	s.founder_name = "Lena Voss"
	s.company_name = "Mossflow"
	s.biz_what = "Software"
	s.biz_who = "SMB"
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	s.set_flag("launched")
	s.analytics_level = 2
	s.tech_debt = 46.0
	s.content_equity = 1_650.0
	s.market_trend = 0.88
	s.offers = [{"name": "pocket synth", "unit": "per unit", "fair_price": 20.0,
		"elasticity": 2.4, "unit_cost": 9.0, "price": 18.0, "price_set": true,
		"weight": 1.0}]
	s.employees = [
		{"name": "Priya Raman", "role": "engineer", "salary": 1_500, "burnout": 32,
			"skill": 4, "hired_week": 12, "site": ""},
		{"name": "Tomas Beck", "role": "sales", "salary": 1_100, "burnout": 51,
			"skill": 3, "hired_week": 19, "wants_raise": true, "asked_week": 32,
			"site": ""},
	]
	s.applicants = [{"name": "Mara Voss", "role": "engineer", "skill": 4,
		"ask": 1_700, "quirk": "", "one_liner": "", "applied_week": 32,
		"source": "inbound"}]
	s.investors = [
		{"name": "Harborline Syndicate", "archetype": "the operator VC",
			"thesis": "small teams that charge properly", "trait": "", "coords": [0.0, 0.0]},
		{"name": "Bell & Weir", "archetype": "the contrarian",
			"thesis": "the boring middle", "trait": "", "coords": [0.1, 0.6]},
	]
	s.rivals = [
		{"name": "Vantage", "strength": 72.0, "what": "", "tactics": ["undercut", "poach"],
			"weeks_since_move": 1, "secret": "", "vigor": 62.0, "hype": 30.0,
			"focus": "price", "price_posture": 0.88, "last_action": "price_cut",
			"log": ["wk31: cut prices", "wk32: quiet", "wk33: cut prices"],
			"cooldowns": {}, "sniffing": 0},
		{"name": "Nimbus", "strength": 28.0, "what": "", "tactics": ["premium"],
			"weeks_since_move": 2, "secret": "", "vigor": 40.0, "hype": 55.0,
			"focus": "growth", "price_posture": 1.08, "last_action": "blitz",
			"log": ["wk32: stumbled", "wk33: ad blitz"], "cooldowns": {}, "sniffing": 0},
		{"name": "Lattice", "strength": 51.0, "what": "", "tactics": ["partner"],
			"weeks_since_move": 0, "secret": "", "vigor": 75.0, "hype": 20.0,
			"focus": "product", "price_posture": 1.0, "last_action": "sniff",
			"log": ["wk33: asking about you"], "cooldowns": {}, "sniffing": 33},
	]
	s.loans = [{"kind": "bank", "principal": 10_000, "balance": 8_215,
		"rate_wk": 0.04, "term_wk": 8, "taken_week": 30, "pay_wk": 1_486, "missed": 1}]
	s.loan_principal = 12_400
	s.instruments = [{"kind": "safe", "holder": "Fern Capital", "amount": 150_000,
		"cap": 4_000_000, "discount": 0.2, "rate": 0.0, "maturity_wk": 0, "pct": 0.0,
		"prefs": 0.0, "protective": false, "drag_threshold": 0.0, "signed_wk": 26}]
	s.raise_state = {"stages": [], "interest_score": 34.0, "active": false,
		"founder_time_tax": 0.0}
	s.board = {"target_growth_pct": 35.0, "base_revenue": 1_300,
		"target_revenue": 1_800, "review_week": 39, "strikes": 1, "goodwill": 2}
	s.price_book = {"open_site_pack": 18_000, "relocation_fee": 400}
	s.leads = [
		{"name": "Meridian Logistics", "flavor": "", "seats": 40, "stage": "pilot",
			"age_weeks": 3, "heat": 88},
		{"name": "Corvid Freight", "flavor": "", "seats": 22, "stage": "procurement",
			"age_weeks": 6, "heat": 55},
	]
	s.bets = [{"id": "b1", "name": "Alerts that matter", "kind": "retention",
		"ambition": 2, "cost_rnd_weeks": 6.0, "progress": 3.0, "committed": true}]
	SimEngine.add_status(s, "funding_winter", 6)
	SimEngine.add_status(s, "price_war", 3)
	SimEngine.add_clock(s, 3, "the bridge loan comes due")
	s.commitments = [{"name": "the trade-show booth", "cash_wk": -180, "weeks_left": 4}]
	for w in range(1, 34):
		s.metric_history.append({"wk": w, "cash": 60_000 - w * 400,
			"customers": int(pow(w, 1.5)), "revenue": 40 * w, "burn": 900 + 30 * w,
			"morale": 74 - w, "debt": 12 + w, "hype": 20 + (w % 17),
			"net": 40 * w - (900 + 30 * w)})
	for w2 in range(28, 34):
		s.run_history.append({"wk": w2, "said": "week %d's move, as written" % w2,
			"heard": "", "verdict": "fine", "roll": "d20=%d vs DC 9 (sell)" % (8 + w2 % 10),
			"fx": ["status: word_of_mouth for 2 wks — the demo landed",
				"spend $400 on one_off — the booth"]})
		s.history.append({"week": w2, "entry": "event 'The window wk%d' — wrote: push" % w2})
	s.history.append({"week": 34, "entry": "wrote: chase the enterprise lead"})
	s.set_meta("pnl", {"revenue": 3_420, "burn": 5_100, "net": -1_680})
	return s

func _go() -> void:
	await process_frame
	var d := OS.get_environment("RUNWAY_STRESS_DIR")
	if d != "":
		_dir = d

	var s := _mid()
	var b := await _open(s)

	# ── the street: winter + three rivals (one circling) + the wire
	_page(b, "the street")
	await _shot("street")
	_page(b, "the street", {"mode": "rivals"})
	await _shot("street_all_rivals")

	# ── threats: the command center with live rows + spillover
	_page(b, "threats")
	await _shot("threats")

	# ── pivot: the two doors → chosen door + preview → armed
	_page(b, "pivot")
	await _shot("pivot_doors")
	_page(b, "pivot", {"mode": "audience", "chip": "Consumer", "typed": "PIVOT"})
	await _shot("pivot_ready")
	SimPivot.arm_product(s, "")
	b.refresh()
	await _shot("pivot_armed")
	SimPivot.disarm(s)

	# ── this week: the landing tab (card fallback + staged + lock note)
	DeskThisWeek.week_card = {"title": "The trade-show window",
		"line": "a booth came free two blocks from the venue — cheap, this week only",
		"icon": ""}
	_page(b, "this week")
	await _shot("this_week")
	_page(b, "this week", {"mode": "preroll"})
	await _shot("this_week_preroll")
	DeskThisWeek.week_card = {}

	# ── history: the book, then one week's receipts
	_page(b, "history")
	await _shot("history")
	_page(b, "history", {"mode": "receipts", "wk": 31})
	await _shot("history_receipts")

	# ── events: the inbox, everything unread
	_page(b, "events")
	await _shot("events")

	# ── the group overviews with this lane's providers live
	_page(b, "the street")
	b.set("_overview", 2)
	b.refresh()
	await _shot("overview_company")
	_page(b, "this week")
	b.set("_overview", 3)
	b.refresh()
	await _shot("overview_log")

	print("L-COMPANY SHOTS: %d captures -> %s" % [_shots.size(), _dir])
	quit(0)
