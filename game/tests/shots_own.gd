extends SceneTree
## L-OWN DESK SHOTS — the ownership cluster's four desks in every state that
## renders differently: cap table (populated · bootstrap-clean · the pool
## receipt), the raise (live pipeline + comparison · empty), recruitment
## (seats + wall + composer · seats door · empty), THE OFFER (a live buyout,
## flags and powers face-up). Modeled on tests/desk_shots.gd; PNGs are
## gitignored, paths print as SHOT lines.
##
## Run: RUNWAY_SHOT_DIR=<dir> godot --headless --path . --script tests/shots_own.gd

var _dir := "/tmp"
var _b: Binder = null

func _init() -> void:
	var env := OS.get_environment("RUNWAY_SHOT_DIR")
	if env != "":
		_dir = env
	call_deferred("_go")

func _shot(nm: String) -> void:
	await create_timer(0.25).timeout
	await RenderingServer.frame_post_draw
	root.get_viewport().get_texture().get_image().save_png("%s/%s_godot.png" % [_dir, nm])
	print("SHOT %s/%s_godot.png" % [_dir, nm])

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

## A mid-game office company with the whole ownership cluster populated.
func _own_state() -> GameState:
	var s := GameState.new()
	s.sim_seed = 77
	s.week = 40
	s.era = "office"
	s.cash = 88_000
	s.traction = 160
	s.product = 62
	s.morale = 66
	s.hype = 44
	s.founder_name = "Lena Voss"
	s.company_name = "Mossflow"
	s.biz_what = "Software"
	s.biz_who = "SMB"
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	s.founder_pct = 58.0
	s.last_growth = 0.06
	s.cofounders = [{"role": 2, "commitment": 1, "equity": 17.0,
		"equity_diluted": 17.0, "vesting": true, "name": "Mara Voss"}]
	s.employees = [
		{"name": "June Park", "role": "engineer", "salary": 1500, "burnout": 20,
			"skill": 4, "hired_week": 12, "quirk": ""},
		{"name": "Tomas Beck", "role": "sales", "salary": 1100, "burnout": 30,
			"skill": 3, "hired_week": 19, "quirk": ""},
	]
	s.investors = [
		{"name": "Harborline Syndicate", "thesis": "small teams that charge properly"},
		{"name": "Bell & Weir", "thesis": "the boring middle of the market"},
		{"name": "Cormorant Capital", "thesis": "own the workflow"},
	]
	s.rivals = [{"name": "Vantage", "strength": 72.0, "tactics": ["undercut"]}]
	s.loan_principal = 7_350
	s.rounds_raised = ["pre-seed"] as Array[String]
	s.board = {"target_growth_pct": 35.0, "base_revenue": 1_300, "target_revenue": 1_800,
		"review_week": 46, "strikes": 0, "goodwill": 2}
	s.board_seats_investor = 1
	s.esop = {"pool_pct": 10.0, "granted": [
		{"emp_id": "june_park", "pct": 0.4, "vest_start_wk": 12},
		{"emp_id": "tomas_beck", "pct": 0.2, "vest_start_wk": 36}]}
	s.option_pool_pct = 10.0
	s.instruments = [
		{"kind": "safe", "holder": "R. Osei", "amount": 60_000, "cap": 1_500_000,
			"discount": 0.2, "rate": 0.0, "maturity_wk": 0, "pct": 0.0, "prefs": 0.0,
			"protective": false, "drag_threshold": 0.0, "signed_wk": 9},
		{"kind": "priced", "holder": "Fern Capital", "amount": 400_000, "cap": 0,
			"discount": 0.0, "rate": 0.0, "maturity_wk": 0, "pct": 15.0, "prefs": 1.0,
			"protective": true, "drag_threshold": 60.0, "signed_wk": 31}]
	s.raise_state = {"stages": [
		{"name": "Halden Ventures", "stage": "radar", "inbound": true, "arrived_wk": 38},
		{"name": "Cormorant Capital", "stage": "conversations", "inbound": true,
			"arrived_wk": 36, "asked_wk": 38, "doubt": "the margin page bleeds"},
		{"name": "Bell & Weir", "stage": "terms", "inbound": true, "arrived_wk": 33,
			"terms": {"kind": "safe", "amount": 250_000, "cap": 3_000_000,
				"discount": 0.2, "expires_wk": 43}},
		{"name": "Harborline Syndicate", "stage": "terms", "inbound": false,
			"arrived_wk": 32, "terms": {"kind": "priced", "valuation": 2_500_000,
				"amount": 500_000, "pct": 16.7, "prefs": 1.0, "participating": false,
				"protective": true, "drag_threshold": 60.0, "board_seat": true,
				"no_shop_wks": 4, "pool_topup_pct": 10.0, "expires_wk": 42}},
	], "interest_score": 61.0, "active": true, "founder_time_tax": 0.3}
	s.recruitment = {"roles": [
		{"id": "role_sales_36", "seat": "salesperson #2", "band_lo": 1400,
			"band_hi": 2060, "advert_wk": 60, "opened_wk": 36},
		{"id": "role_care_lead_37", "seat": "care lead", "band_lo": 980,
			"band_hi": 1440, "advert_wk": 40, "opened_wk": 37}],
		"candidates": [
		{"id": "c1", "role_id": "role_sales_36", "name": "Priya Nair", "ask": 1540,
			"profile": "missionary", "skill": 3, "stage": "applied", "arrived_wk": 39},
		{"id": "c2", "role_id": "role_sales_36", "name": "Tom Burrell", "ask": 1500,
			"profile": "mercenary", "skill": 2, "stage": "applied", "arrived_wk": 40},
		{"id": "c3", "role_id": "role_sales_36", "name": "Dana Kovic", "ask": 1540,
			"profile": "mercenary", "skill": 4, "stage": "interviewed", "arrived_wk": 38},
		{"id": "c4", "role_id": "role_care_lead_37", "name": "Ada Whitlock", "ask": 1200,
			"profile": "missionary", "skill": 4, "stage": "offer", "arrived_wk": 37},
		{"id": "c5", "role_id": "role_sales_36", "name": "June Novak", "ask": 1450,
			"profile": "missionary", "skill": 3, "stage": "joined", "arrived_wk": 33}],
		"offers_out": [{"candidate_id": "c4", "cash_wk": 1240, "options_pct": 0.3,
			"expires_wk": 41, "sent_wk": 40}]}
	s.set_meta("pnl", {"revenue": 3_400, "cogs": 700, "rent": 3_000, "payroll": 2_600,
		"infra": 200, "marketing": 1_500, "sales": 500, "care": 300, "rnd": 900,
		"office": 250, "offer_fixed": 100, "severance": 0, "recruiting": 0,
		"recruit_ads": 100, "relief": 0, "site_rent": 0, "incident": 0,
		"liabilities_wk": 0, "interest": 120, "tax": 0, "burn": 10_050,
		"net": -6_770, "learning": 0.9})
	return s

func _go() -> void:
	# ── CAP TABLE: populated · the pool receipt · the bootstrap-clean page
	var s := _own_state()
	var b := await _open(s)
	b.focus_desk("cap table")
	await _shot("own_cap_populated")
	b.desk["mode"] = "pool"
	b.refresh()
	await _shot("own_cap_pool_receipt")
	var clean := GameState.new()
	clean.sim_seed = 5
	clean.week = 6
	clean.company_name = "Mossflow"
	clean.theta = SimEngine.default_theta("Software", "SMB")
	b = await _open(clean)
	b.focus_desk("cap table")
	await _shot("own_cap_bootstrap_clean")

	# ── THE RAISE: the live pipeline + comparison · the quiet page
	b = await _open(_own_state())
	b.focus_desk("the raise")
	await _shot("own_raise_live")
	b = await _open(clean)
	b.focus_desk("the raise")
	await _shot("own_raise_quiet")

	# ── RECRUITMENT: the wall + composer · the seats door · the quiet page
	var s2 := _own_state()
	b = await _open(s2)
	b.focus_desk("recruitment")
	await _shot("own_recruit_live")
	b.desk["mode"] = "seats"
	b.refresh()
	await _shot("own_recruit_seats_door")
	b = await _open(clean)
	b.focus_desk("recruitment")
	await _shot("own_recruit_quiet")

	# ── THE OFFER: the momentary desk, flags and powers face-up
	var s3 := _own_state()
	s3.mna = {"buyer": "Vantiv Group", "why": "rival", "premium": 1.2,
		"price": 4_200_000, "expires_week": 42}
	s3.buyout_offer = {"buyer": "Vantiv Group", "headline": 4_200_000,
		"cash": 1_600_000, "stock": 1_900_000, "lockup_wks": 78,
		"earnout": 700_000, "earnout_controller": "buyer",
		"retention_wks": 104, "retention_carve": false, "expires_wk": 42,
		"fishy_flags": [
			"the earnout's targets are set — and measured — by the buyer",
			"$1.9M of the price is their stock, locked 19 months"],
		"why": "rival", "arrived_wk": 40, "countered": false,
		"headline_line": "Vantiv Group offers $4,200,000"}
	b = await _open(s3)
	b.summon_momentary("the offer", "THE COMPANY", "THE OFFER", 2)
	b.focus_desk("the offer")
	await _shot("own_offer_live")

	print("OWN SHOTS DONE")
	quit(0)
