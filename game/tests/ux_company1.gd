extends SceneTree
## B-COMPANY1 UX PROBE — the three COMPANY desks of DAG3 Wave B, per tab ×
## {week-1 zero state, live state with the DO lane, one press_receipt open,
## red state with the ask strip}: what we make (queue-the-rebuild, history
## cites, SHIP/commit lane), cap table (THE VALUATION SLIDER, step receipts),
## the raise (doubts → weak desks, pitch/sign/walk, the velocity banner).
##
## Run WINDOWED (the shot law): RUNWAY_STRESS_DIR=<dir> godot --path game \
##   --script tests/ux_company1.gd
## Files land as <surface>_godot.png. Modeled on tests/shots_rev.gd.

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
	for k in st:
		b.desk[k] = st[k]
	if not st.is_empty():
		b.refresh()

## Press the INVISIBLE receipt hit whose top-left falls inside `region` —
## the press_receipt wiring itself opens the popover (never drawn directly).
func _press_hit(b: Binder, region: Rect2) -> bool:
	for child in b.pane().get_children():
		if child is Button and (child as Button).text == "" \
				and region.has_point((child as Control).position):
			(child as Button).pressed.emit()
			return true
	return false

## Press the word button carrying exactly `text` (the slider's + / −).
func _press_word(b: Binder, text: String) -> bool:
	for child in b.pane().get_children():
		if child is Button and (child as Button).text == text:
			(child as Button).pressed.emit()
			return true
	return false

# ═══════════════════════════════ fixtures ════════════════════════════════════

## The shared trunk: a named company, sane meters. Offers stay EMPTY so week-1
## states keep an empty wall; live states append their own.
func _base(era := "office", week := 30) -> GameState:
	var s := GameState.new()
	s.sim_seed = 77
	s.week = week
	s.era = era
	s.cash = 41_000
	s.traction = 96
	s.product = 58
	s.morale = 61
	s.hype = 38
	s.founder_name = "Mara Iversen"
	s.company_name = "Loomhaus"
	s.company_idea = "hand-tools for small workshops"
	s.biz_what = "Software"
	s.biz_who = "SMB"
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	s.set_flag("launched")
	s.budgets = {"ads": 1200, "content": 400, "referrals": 200, "outbound": 600,
		"sales": 800, "care": 400, "rnd": 2000, "office": 400}
	s.set_meta("pnl", {"revenue": 2_900, "cogs": 600, "burn": 9_100, "net": -6_200})
	s.investors = [{"name": "Halden Ventures"}, {"name": "R. Osei"},
		{"name": "Cormorant Capital"}]
	return s

func _with_offer(s: GameState) -> void:
	s.offers = [{"name": "bench planner", "unit": "per seat", "fair_price": 24.0,
		"elasticity": 2.0, "unit_cost": 8.0, "price": 22.0, "price_set": true,
		"weight": 1.0, "fixed_wk": 30.0}]

## A finished bet waiting on the dice (the DO lane's [SHIP]).
func _ready_bet(s: GameState) -> void:
	s.bets.append({"id": "bet_probe_ready", "name": "the exports pack",
		"desc": "", "kind": "quality", "ambition": 2, "cost_rnd_weeks": 5.0,
		"progress": 5.0, "committed": true, "committed_week": s.week - 5,
		"ready": true, "ready_week": s.week - 1, "shipped": false,
		"shipped_week": 0, "band": "", "era": s.era})

## Measured landings on every job, so any shelf card can cite its history.
func _with_history(s: GameState) -> void:
	for spec in [["ft_h1", "the referral loop", "pull", 3.4],
			["ft_h2", "calendar sync", "keep", 2.1],
			["ft_h3", "white-label", "charge", 5.0]]:
		s.features.append({"id": String(spec[0]), "name": String(spec[1]),
			"job": String(spec[2]), "family": "", "solidity": "solid",
			"keep_wk": 5, "unit_cost_add": 0.25, "product_id": "",
			"born_wk": s.week - 6, "measured": float(spec[3])})
	# the source bets, so promised+measured both print
	for spec2 in [["the referral loop", "reach"], ["calendar sync", "retention"],
			["white-label", "quality"]]:
		s.bets.append({"id": "bet_src_%s" % String(spec2[0]).replace(" ", "_"),
			"name": String(spec2[0]), "desc": "", "kind": String(spec2[1]),
			"ambition": 2, "cost_rnd_weeks": 5.0, "progress": 5.0,
			"committed": false, "committed_week": 0, "ready": false,
			"shipped": true, "shipped_week": s.week - 6, "band": "fine",
			"era": s.era})

## The ownership book of a company two rounds in. with_safe adds the
## unconverted SAFE (its memo row makes the book DEEP — the compact mode).
func _with_book(s: GameState, with_safe := true) -> void:
	s.founder_pct = 54.0
	s.cofounders = [{"role": "ops", "commitment": "full", "equity": 9.0,
		"equity_diluted": 8.2, "vesting": true, "name": "Jonas Brandt"}]
	s.instruments = [
		{"kind": "priced", "holder": "Halden Ventures", "amount": 250_000,
			"cap": 0, "discount": 0.0, "rate": 0.0, "maturity_wk": 0,
			"pct": 18.0, "prefs": 1.0, "protective": true,
			"drag_threshold": 60.0, "signed_wk": 24},
	]
	if with_safe:
		s.instruments.append({"kind": "safe", "holder": "R. Osei",
			"amount": 60_000, "cap": 900_000, "discount": 0.2, "rate": 0.0,
			"maturity_wk": 0, "pct": 0.0, "prefs": 0.0, "protective": false,
			"drag_threshold": 0.0, "signed_wk": 14})
	s.esop = {"pool_pct": 10.0, "granted": [
		{"emp_id": "sana_qureshi", "pct": 1.2, "vest_start_wk": 20}]}
	s.board = {"strikes": 0, "goodwill": 1}
	s.board_seats_investor = 1

# ═════════════════════════════════ the run ═══════════════════════════════════

func _go() -> void:
	await process_frame
	var d := OS.get_environment("RUNWAY_STRESS_DIR")
	if d != "":
		_dir = d
	await _make()
	await _cap()
	await _raise()
	print("B-COMPANY1 UX SHOTS: %d captures -> %s" % [_shots.size(), _dir])
	quit(0)

# ── what we make ─────────────────────────────────────────────────────────────
func _make() -> void:
	# S1 · the designed first week: a truly blank wall (no offers → no seeds)
	var s0 := _base("garage", 1)
	s0.traction = 0
	s0.product = 0
	var b0 := await _open(s0)
	_page(b0, "what we make")
	await _shot("make_zero")

	# live · seeds + history + a READY bet → DO lane [SHIP], history cites
	var s1 := _base()
	_with_offer(s1)
	_with_history(s1)
	_ready_bet(s1)
	var b1 := await _open(s1)
	_page(b1, "what we make")
	await _shot("make_live")

	# S4 · the first shelf card's history cite pressed open (col 1 region)
	_press_hit(b1, Rect2(8.0, 200.0, 40.0, 200.0))
	await _shot("make_receipt")

	# red · two creaks + real debt → ask strip, rebuild leads the shelf,
	# the suggestion stands, DO lane says [commit — rebuild: …]
	var s2 := _base()
	_with_offer(s2)
	_with_history(s2)
	s2.tech_debt = 62.0
	var b2 := await _open(s2)
	SimFeatures.seed_defaults(s2)
	(s2.features[0] as Dictionary)["solidity"] = "creaky"
	(s2.features[1] as Dictionary)["solidity"] = "creaky"
	_page(b2, "what we make")
	b2.refresh()
	await _shot("make_red")
	print("make suggestions: %s" % str(DeskMake.suggestions(s2)))

# ── cap table ────────────────────────────────────────────────────────────────
func _cap() -> void:
	# S1 · the blank book
	var s0 := _base("garage", 1)
	var b0 := await _open(s0)
	_page(b0, "cap table")
	await _shot("cap_zero")

	# live · a round in (shallow book) → slices, the dilution story, THE
	# SLIDER at ×1, DO lane [expand — the pool], breakeven line
	var s1 := _base()
	_with_offer(s1)
	_with_book(s1, false)
	var b1 := await _open(s1)
	_page(b1, "cap table")
	await _shot("cap_live")

	# the slider STEPPED — the + word pressed twice, the waterfall redrawn
	# live through SimOwnership.waterfall at the new price
	_press_word(b1, "+")
	await create_timer(0.15).timeout
	_press_word(b1, "+")
	await _shot("cap_slider_x2")

	# S4 · the hero pressed open — the waterfall receipt behind the number
	_press_hit(b1, Rect2(4.0, 2.0, 24.0, 24.0))
	await _shot("cap_receipt")

	# deep · the FULL book (SAFE memo row makes 6 lines) → the compact mode:
	# zones end above the teaching foot, notes live in the receipts, the DO
	# lane yields to the z1 header word
	var sd := _base()
	_with_offer(sd)
	_with_book(sd, true)
	var bd := await _open(sd)
	_page(bd, "cap table")
	await _shot("cap_deep")

	# red · the pool granted dry while seats are open → pool_empty ask
	var s2 := _base()
	_with_offer(s2)
	_with_book(s2)
	s2.esop = {"pool_pct": 4.0, "granted": [
		{"emp_id": "sana_qureshi", "pct": 4.0, "vest_start_wk": 20}]}
	s2.recruitment = {"roles": [{"id": "role_1", "seat": "engineer",
		"advert_wk": 0}], "candidates": [], "offers_out": []}
	var b2 := await _open(s2)
	_page(b2, "cap table")
	await _shot("cap_red")

# ── the raise ────────────────────────────────────────────────────────────────
func _raise() -> void:
	# S1 · the untouched pipeline (dormant at the garage, the map teaches)
	var s0 := _base("garage", 1)
	s0.raise_state = {"stages": [], "interest_score": 22.0, "active": false,
		"founder_time_tax": 0.0}
	var b0 := await _open(s0)
	_page(b0, "the raise")
	await _shot("raise_zero")

	# live · a full pipeline mid-raise: radar + doubted conversation + two
	# sheets → the comparison, the velocity banner, DO [pitch][sign][walk]
	var s1 := _base()
	_with_offer(s1)
	_with_book(s1)
	s1.raise_state = {"active": true, "founder_time_tax": 0.3,
		"interest_score": 61.0, "stages": [
			{"name": "Cormorant Capital", "stage": "radar", "inbound": true,
				"arrived_wk": s1.week - 1},
			{"name": "Bright & Motte", "stage": "conversations", "inbound": false,
				"arrived_wk": s1.week - 3, "asked_wk": s1.week - 2,
				"doubt": "the margin page bleeds"},
			{"name": "Halden Ventures", "stage": "terms", "inbound": true,
				"arrived_wk": s1.week - 5, "terms": {"kind": "priced",
					"amount": 400_000, "valuation": 1_600_000, "pct": 20.0,
					"prefs": 1.0, "participating": false, "protective": true,
					"drag_threshold": 60.0, "board_seat": true,
					"no_shop_wks": 4, "pool_topup_pct": 8.0,
					"expires_wk": s1.week + 2}},
			{"name": "R. Osei", "stage": "terms", "inbound": false,
				"arrived_wk": s1.week - 4, "terms": {"kind": "safe",
					"amount": 150_000, "cap": 1_400_000, "discount": 0.2,
					"expires_wk": s1.week + 1}},
		]}
	var b1 := await _open(s1)
	_page(b1, "the raise")
	await _shot("raise_live")

	# red · a matured note → sev-3 ask strip; not active → the interest
	# block + its S4 receipt
	var s2 := _base()
	_with_offer(s2)
	s2.founder_pct = 88.0
	s2.instruments = [{"kind": "note", "holder": "Bright & Motte",
		"amount": 80_000, "cap": 700_000, "discount": 0.2, "rate": 0.003,
		"maturity_wk": s2.week - 2, "pct": 0.0, "prefs": 0.0,
		"protective": false, "drag_threshold": 0.0, "signed_wk": s2.week - 54}]
	s2.raise_state = {"stages": [], "interest_score": 44.0, "active": false,
		"founder_time_tax": 0.0}
	var b2 := await _open(s2)
	_page(b2, "the raise")
	await _shot("raise_red")

	# S4 · the interest number pressed open
	_press_hit(b2, Rect2(736.0, 4.0, 20.0, 20.0))
	await _shot("raise_receipt")
