extends SceneTree
## MAKE SHOTS (L-MAKE) — the WHAT WE MAKE desk in every state that renders
## differently: the rung-1 wall (four columns + LIVE band + creak), the
## rung-2 wall (families, attention-first folds, the queue, parallel builds),
## rung 3 (THE LINEUP + SHARED PLUMBING), the family page, the pre-roll
## review and the ship receipt. Modeled on tests/desk_shots.gd; probe-only,
## never part of the suite.
##
## Run: RUNWAY_STRESS_DIR=<dir> godot --headless --path . --script tests/shots_make.gd
## Files land as make_<state>_godot.png.

var _dir := "/tmp"
var _b: Binder = null
var _shots: Array[String] = []

func _init() -> void:
	call_deferred("_go")

func _shot(nm: String) -> void:
	await create_timer(0.25).timeout
	await RenderingServer.frame_post_draw
	root.get_viewport().get_texture().get_image().save_png("%s/%s_godot.png" % [_dir, nm])
	_shots.append(nm)
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
	_b = b
	return b

## Navigate first (focus clears desk-local state), then seed the state the
## shot wants, then rebuild — the same order a real press produces.
func _page(b: Binder, st: Dictionary = {}) -> void:
	b.focus_desk("what we make")
	b.desk.clear()
	for k in st:
		b.desk[k] = st[k]
	b.refresh()

# ═══════════════════════════════ fixtures ════════════════════════════════════

## A live software company mid-run: money moving, a board on the wall.
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
	s.company_name = "Paceboard"
	s.company_idea = "schedules for small teams"
	s.biz_what = "Software"
	s.biz_who = "SMB"
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	s.tech_debt = 46.0
	s.competences = {"build": 4, "sell": 3, "raise": 3, "recruit": 2, "grit": 4}
	s.budgets = {"ads": 2000, "content": 500, "referrals": 250, "outbound": 1000,
		"sales": 1000, "care": 500, "rnd": 2000, "office": 500}
	s.offers = [{"name": "team plan", "unit": "per month", "fair_price": 24.0,
		"elasticity": 2.0, "unit_cost": 8.0, "price": 22.0, "price_set": true,
		"weight": 1.0}]
	s.set_meta("pnl", {"revenue": 3_420, "burn": 5_100, "net": -1_680})
	return s

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

## Rung 1: nine live, one creaky plumbing, one build, one ready.
func _rung1() -> GameState:
	var s := _mid()
	s.features = [
		_feat("f1", "online booking", "pull", 6),
		_feat("f2", "reminders", "pull", 2),
		_feat("f3", "team calendar", "keep", 4),
		_feat("f4", "reports", "keep", 8, "creaky"),
		_feat("f5", "SSO", "charge", 5, "solid", "", 30, 4.6),
		_feat("f6", "permissions", "charge", 3),
		_feat("f7", "billing core", "plumbing", 9, "creaky"),
		_feat("f8", "scheduler engine", "plumbing", 11),
		_feat("f9", "data store", "plumbing", 7),
	]
	_bet(s, "b_build", "mobile app", "reach", 2, true, false, 0.5)
	_bet(s, "b_ready", "integrations API", "quality", 2, false, true)
	return s

## Rung 2: 26 live in families, two creaks, parallel builds, a queue.
func _rung2() -> GameState:
	var s := _rung1()
	s.week = 52
	s.tech_debt = 55.0
	var fams := {"the booking suite": "pull", "the invite loop": "pull",
		"reporting": "keep", "the calendar core": "keep",
		"enterprise trust": "charge", "the data platform": "plumbing"}
	var i := 10
	for fam in fams:
		var parts := String(fam).split(" ")
		var stem := parts[parts.size() - 1]
		for k in 3:
			var solidity := "creaky" if fam == "reporting" and k == 0 else "solid"
			s.features.append(_feat("f%d" % i, "%s %d" % [stem, k + 1],
				String(fams[fam]), 3 + k, solidity, String(fam)))
			i += 1
	s.features.append(_feat("f_fresh", "the invite loop v2", "pull", 4, "solid",
		"", 50, 8.2))
	_bet(s, "b_build2", "billing core rebuild", "debt", 1, true, false, 0.33)
	_bet(s, "b_build3", "team spaces", "reach", 3, true, false, 0.4)
	_bet(s, "b_q1", "analytics pack", "retention", 2, false, false)
	_bet(s, "b_q2", "the referral loop", "reach", 1, false, false)
	SimFeatures.enqueue_bet(s, "b_q1")
	SimFeatures.enqueue_bet(s, "b_q2")
	return s

## Rung 3: a second product exists — the lineup + shared plumbing.
func _rung3() -> GameState:
	var s := _rung2()
	for f in s.features:
		var fd: Dictionary = f
		if String(fd.get("family", "")) == "enterprise trust":
			fd["product_id"] = "Atlas API"
	s.features.append(_feat("f_api1", "the key vault", "keep", 5, "solid", "",
		0, 0.0, "Atlas API"))
	s.features.append(_feat("f_api2", "rate limiting", "charge", 4, "creaky", "",
		0, 0.0, "Atlas API"))
	return s

# ═══════════════════════════════ the run ═════════════════════════════════════

func _go() -> void:
	_dir = OS.get_environment("RUNWAY_STRESS_DIR")
	if _dir == "":
		_dir = "/tmp"
	# rung 1 — the wall
	var b1 := await _open(_rung1())
	_page(b1)
	await _shot("make_wall_rung1")
	# rung 2 — families, folds, queue, parallel builds
	var b2 := await _open(_rung2())
	_page(b2)
	await _shot("make_wall_rung2")
	# rung 2 — a family opened
	_page(b2, {"mode": "family", "family": "the booking suite"})
	await _shot("make_family")
	# rung 3 — the lineup + shared plumbing
	var b3 := await _open(_rung3())
	_page(b3)
	await _shot("make_lineup_rung3")
	# the pre-roll review (an unpriced offer stands between founder and dice)
	var sp := _rung1()
	sp.offers.append({"name": "the audit pack", "unit": "per month",
		"fair_price": 60.0, "elasticity": 1.6, "unit_cost": 20.0, "price": 0.0,
		"weight": 0.5})
	var bp := await _open(sp)
	_page(bp, {"mode": "preroll", "bet": "b_ready"})
	await _shot("make_preroll")
	# the receipt — ship with a fixed die so the shot is stable
	var sr := _rung1()
	var bet := SimRoadmap.bet_by_id(sr, "b_ready")
	var res := SimRoadmap.ship_bet(sr, bet, func() -> int: return 17)
	var br := await _open(sr)
	_page(br, {"mode": "shipped", "ship": res})
	await _shot("make_receipt")
	print("MAKE SHOTS DONE: %d → %s" % [_shots.size(), _dir])
	quit(0)
