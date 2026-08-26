extends SceneTree
## DESK SHOTS — the works (L-DIVWORKS): every state that renders differently.
## Boutique / house / empire (with a bleeding roof) per type, the capacity
## DETAIL, and the arrange write-view (bins, a staged move, the teardown
## wizard, the open-roof door).
##
## Run: RUNWAY_STRESS_DIR=<dir> godot --headless --path . --script tests/shots_divworks.gd
## Files land as works_<state>_godot.png beside the other desk shots.

var _dir := "/tmp"
var _b: Binder = null

func _init() -> void:
	var env := OS.get_environment("RUNWAY_STRESS_DIR")
	if env != "":
		_dir = env
	call_deferred("_go")

func _shot(nm: String) -> void:
	await create_timer(0.25).timeout
	await RenderingServer.frame_post_draw
	root.get_viewport().get_texture().get_image().save_png("%s/%s_godot.png" % [_dir, nm])
	print("SHOT %s" % nm)

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
	b.focus_desk("the works")
	await create_timer(0.20).timeout
	_b = b
	return b

func _state(what: String, who: String) -> GameState:
	var s := GameState.new()
	s.sim_seed = 77
	s.week = 30
	s.era = "office"
	s.cash = 48_000
	s.traction = 90
	s.product = 55
	s.morale = 68
	s.hype = 35
	s.biz_what = what
	s.biz_who = who
	s.company_name = "Dao & Daughters"
	s.theta = SimEngine.default_theta(what, who)
	s.set_flag("launched")
	s.price_book = {"open_site_pack": 18_000, "relocation_fee": 400,
		"machine_shipping": 900, "lease_break_weeks": 8, "contract_notice_wks": 4,
		"refinance_break_fee": 350, "freelance_rate": 48, "subcontract_rate": 30,
		"account_fire_penalty": 1_200}
	return s

func _offer(s: GameState, nm: String, unit: String, fair: float, cost: float,
		weight: float, lines: Array = []) -> void:
	SimEngine.add_offer(s, nm, unit, fair, cost, 2.0, weight, lines)
	var od: Dictionary = s.offers[s.offers.size() - 1]
	od["price"] = fair
	od["price_set"] = true

func _emp(s: GameState, nm: String, role: String, sal: int, skill: int, site: String) -> void:
	s.employees.append({"name": nm, "role": role, "salary": sal, "burnout": 12,
		"quirk": "", "skill": skill, "hired_week": 4, "site": site})

func _service_boutique() -> GameState:
	var s := _state("Service", "SMB")
	s.traction = 82
	_offer(s, "the classic 50", "per session", 80.0, 31.0, 1.0,
		[{"label": "hands, 50 min", "amount": 22.0}, {"label": "oils & linens", "amount": 4.0},
			{"label": "room & laundry", "amount": 5.0}])
	_emp(s, "Nadia Beck", "therapist", 1_500, 5, "")
	_emp(s, "Tomas Iri", "therapist", 1_400, 4, "")
	SimWorks.relief_set(s, "freelance", 10)
	SimEngine.weekly_tick(s)
	return s

func _service_house() -> GameState:
	var s := _service_boutique()
	_offer(s, "the deep 90", "per session", 130.0, 52.0, 0.8)
	_offer(s, "house calls", "per session", 110.0, 47.0, 0.6)
	SimEngine.weekly_tick(s)
	return s

func _service_empire() -> GameState:
	var s := _service_house()
	s.cash = 120_000
	s.sites = [
		{"id": "site_1", "name": "Lyon", "rent_wk": 1_100, "wage_mult": 0.92,
			"learning_count": 4_100, "demand_weight": 1.0, "opened_wk": 6},
		{"id": "site_2", "name": "Geneva", "rent_wk": 3_400, "wage_mult": 1.15,
			"learning_count": 120, "demand_weight": 0.8, "opened_wk": 8}]
	_emp(s, "June Park", "therapist", 1_500, 4, "site_1")
	_emp(s, "Ines Rol", "therapist", 1_450, 3, "site_2")
	# three red weeks on Geneva so the alarm face shows
	SimDivisions._mark(s, "works_red", "site_2", 3)
	SimEngine.weekly_tick(s)
	return s

func _software() -> GameState:
	var s := _state("Software", "SMB")
	s.traction = 1_240
	s.budgets["care"] = 2_600
	_offer(s, "the plan", "per month", 18.0, 4.0, 1.0,
		[{"label": "hosting", "amount": 0.9}, {"label": "support minutes", "amount": 2.6},
			{"label": "billing fees", "amount": 0.5}])
	_emp(s, "Rae Ling", "support", 1_100, 4, "")
	_emp(s, "Om Datta", "support", 1_050, 3, "")
	SimEngine.weekly_tick(s)
	return s

func _marketplace() -> GameState:
	var s := _state("Marketplace", "Consumer")
	s.traction = 540
	s.last_growth = 0.22
	_offer(s, "a matched order", "per order", 9.0, 3.5, 1.0,
		[{"label": "payment fees", "amount": 1.1}, {"label": "support & disputes", "amount": 0.8},
			{"label": "winning the seller", "amount": 1.6}])
	SimWorks.relief_set(s, "recruit_supply", 300)
	SimEngine.weekly_tick(s)
	return s

func _hardware() -> GameState:
	var s := _state("Hardware", "Consumer")
	s.traction = 60
	_offer(s, "Pocket Synth", "per unit", 100.0, 20.0, 1.0,
		[{"label": "parts", "amount": 12.0}, {"label": "hands", "amount": 6.0},
			{"label": "wear", "amount": 2.0}])
	var hw := SimFactory.hw_state(s)
	SimFactory.buy_equipment(s, "jig")
	hw["stock"] = 8
	SimEngine.weekly_tick(s)
	return s

func _go() -> void:
	root.get_viewport().size = Vector2(1536, 1024)

	var b := await _open(_service_boutique())
	await _shot("works_service_boutique")
	b.desk["page"] = "capacity"
	b.refresh()
	await _shot("works_service_capacity")

	b = await _open(_service_house())
	await _shot("works_service_house")

	b = await _open(_service_empire())
	await _shot("works_empire_lineup")
	b.desk["row"] = "site_1"
	b.refresh()
	await _shot("works_empire_site_open")
	b.desk.erase("row")
	b.desk["mode"] = "arrange"
	b.refresh()
	await _shot("works_arrange_bins")
	# a staged person move: chip June Park (employee index 2 — the boutique
	# seeds two before the empire adds her) → Geneva
	b.desk["staged2"] = {"kind": "e", "idx": 2, "to": "site_2",
		"quote": SimDivisions.reassign_quote(b.state, 2, "site_2")}
	b.refresh()
	await _shot("works_arrange_staged_move")
	b.desk.erase("staged2")
	b.desk["teardown"] = "site_2"
	b.refresh()
	await _shot("works_arrange_teardown")
	b.desk.erase("teardown")
	b.desk["open_roof"] = true
	b.refresh()
	await _shot("works_arrange_open_roof")

	b = await _open(_software())
	await _shot("works_software")
	b = await _open(_marketplace())
	await _shot("works_marketplace")
	b = await _open(_hardware())
	await _shot("works_hardware")

	print("DIVWORKS SHOTS DONE")
	quit(0)
