extends SceneTree
## MONEY DESK SHOTS (DAG2 W2 L-MONEY) — the four reworked money desks in
## every state that renders differently: spend with a generated book (adopt
## pending), the fallback bare book carrying legacy levers, the add door and
## the stop-notice state; team at all three rungs (asks, vesting bars, the
## open seat pointing at recruitment); bills with both sections (and the
## sites/venture variant); the bank's MEETING (all four zones) in its lively,
## locked and garage states plus BOOKS on the ledger sheet.
##
## Run: RUNWAY_STRESS_DIR=<dir> godot --headless --path . --script tests/shots_money.gd
## Files land as money_<name>_godot.png. PNGs are local-only (gitignored).

var _dir := "/tmp"
var _b: Binder = null
var _shots: Array[String] = []

func _init() -> void:
	call_deferred("_go")

# ═══════════════════════════ the shot harness ════════════════════════════════

func _shot(nm: String) -> void:
	await create_timer(0.25).timeout
	await RenderingServer.frame_post_draw
	root.get_viewport().get_texture().get_image().save_png("%s/money_%s_godot.png" % [_dir, nm])
	_shots.append(nm)
	print("SHOT money_%s" % nm)

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

## Point the binder at a page with desk-local state, then rebuild.
func _page(b: Binder, id: String, st: Dictionary = {}) -> void:
	b.focus_desk(id)
	for k in st:
		b.desk[k] = st[k]
	b.refresh()

# ═══════════════════════════════ fixtures ════════════════════════════════════

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
	s.company_name = "Mossflow"
	s.founder_name = "Lena Voss"
	s.biz_what = "Software"
	s.biz_who = "SMB"
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	s.analytics_level = 2
	s.last_growth = 0.06
	s.price_mult = 1.0
	s.offers = [
		{"name": "pocket synth", "unit": "per unit", "fair_price": 20.0,
			"elasticity": 2.4, "unit_cost": 9.0, "price": 18.0, "price_set": true,
			"weight": 1.0, "fixed_wk": 40.0,
			"cost_lines": [{"label": "components", "amount": 6.0},
				{"label": "packing + delivery", "amount": 3.0}],
			"fixed_lines": [{"label": "bench rental", "amount": 40.0}]},
		{"name": "support retainer", "unit": "per month", "fair_price": 240.0,
			"elasticity": 1.2, "unit_cost": 90.0, "price": 900.0, "price_set": true,
			"weight": 0.4, "fixed_wk": 60.0,
			"cost_lines": [{"label": "on-call hours", "amount": 90.0}],
			"fixed_lines": [{"label": "helpdesk seat", "amount": 60.0},
				{"label": "status page", "amount": 12.0}]},
	]
	s.employees = [
		{"name": "Priya Raman", "role": "engineer", "salary": 1500, "burnout": 32,
			"skill": 4, "hired_week": 12, "quirk": "ships on fridays, apologises on mondays"},
		{"name": "Tomas Beck", "role": "sales", "salary": 1100, "burnout": 51,
			"skill": 3, "hired_week": 19, "wants_raise": true, "asked_week": 32,
			"underpaid_since": 27, "quirk": "negotiates via long silences"},
		{"name": "Ada Whitlock", "role": "designer", "salary": 1350, "burnout": 18,
			"skill": 5, "hired_week": 24, "quirk": "redraws the logo when nervous"},
		{"name": "June Okafor", "role": "support", "salary": 900, "burnout": 22,
			"skill": 3, "hired_week": 26, "quirk": "answers before the phone rings"},
	]
	s.loans = [
		{"kind": "bank", "principal": 10_000, "balance": 6_000, "rate_wk": 0.04,
			"term_wk": 26, "taken_week": 20, "pay_wk": 258, "missed": 0},
	]
	s.loan_principal = 3_400
	s.commitments = [{"name": "the trade-show booth", "cash_wk": -180, "weeks_left": 4}]
	s.tax_loss_carry = 8_200
	s.set_meta("pnl", {"revenue": 3_840, "cogs": 760, "rent": 3_000, "payroll": 4_850,
		"infra": 220, "marketing": 3_750, "sales": 300, "care": 120, "rnd": 150,
		"office": 220, "offer_fixed": 112, "severance": 0, "recruiting": 1_500,
		"production": 0, "subcontract": 0, "equip_upkeep": 0, "carrying": 0,
		"recruit_ads": 0, "relief": 0, "site_rent": 0,
		"incident": 240, "liabilities_wk": 180, "interest": 852, "tax": 0,
		"burn": 15_182, "net": -12_374, "learning": 0.89})
	s.set_meta("bank_principal_wk", 158)
	s.set_meta("unit_econ", {"arpu": 27.6, "cac": 310, "ltv": 1_104, "payback_wk": 11})
	for w in range(1, 34):
		s.metric_history.append({"week": w, "cash": 60_000 - w * 400,
			"customers": int(pow(w, 1.5)), "revenue": 40 * w, "burn": 900 + 40 * w,
			"net": 40 * w - (900 + 40 * w)})
	return s

## The generated org book — a dev-tools business, suggestions unadopted.
func _with_book(s: GameState) -> void:
	s.spend_book = [
		{"name": "sales engineering", "buys": "demos that land", "amt": 180, "bucket": "sales", "contract_notice": 0, "division": ""},
		{"name": "the demo rig", "buys": "always ready to show", "amt": 120, "bucket": "sales", "contract_notice": 0, "division": ""},
		{"name": "on-call rotation", "buys": "nights answered", "amt": 120, "bucket": "care", "contract_notice": 4, "division": ""},
		{"name": "success check-ins", "buys": "nobody drifts away", "amt": 80, "bucket": "care", "contract_notice": 0, "division": ""},
		{"name": "the test bench", "buys": "bugs die young", "amt": 150, "bucket": "rnd", "contract_notice": 0, "division": ""},
		{"name": "refactor fridays", "buys": "debt pays down", "amt": 100, "bucket": "rnd", "contract_notice": 0, "division": ""},
		{"name": "the kitchen", "buys": "fed people stay", "amt": 220, "bucket": "office", "contract_notice": 0, "division": ""},
		{"name": "gym floats", "buys": "backs survive desks", "amt": 180, "bucket": "office", "contract_notice": 0, "division": ""},
	]

func _crowd(s: GameState, n: int) -> void:
	var roles := ["engineer", "sales", "support", "ops", "designer", "manager"]
	var first := ["Mara", "Yusuf", "Hanne", "Nico", "Ines", "Ravi", "Lena", "Karl",
		"Sofia", "Emil", "Noor", "Anders", "Beatriz", "Otto", "Wanda", "Felix"]
	var last := ["Voss", "Adeyemi", "Skov", "Ferreira", "Cardoso", "Raman", "Beck",
		"Whitlock", "Okafor", "Lindqvist", "Marchetti", "Sato"]
	while s.employees.size() < n:
		var i := s.employees.size()
		s.employees.append({"name": "%s %s" % [first[i % first.size()], last[(i * 7) % last.size()]],
			"role": roles[i % roles.size()], "salary": 800 + (i % 9) * 130,
			"burnout": (i * 13) % 80, "skill": 1 + (i % 5), "hired_week": 4 + (i % 28),
			"quirk": ""})

# ═════════════════════════════════ the run ═══════════════════════════════════

func _go() -> void:
	await process_frame
	var d := OS.get_environment("RUNWAY_STRESS_DIR")
	if d != "":
		_dir = d

	await _spend()
	await _team()
	await _bills()
	await _bank()

	print("MONEY SHOTS: %d captures -> %s" % [_shots.size(), _dir])
	quit(0)

# ── spend ────────────────────────────────────────────────────────────────────
func _spend() -> void:
	# THE GENERATED BOOK, suggestions pending, two lines adopted by hand
	var s := _mid()
	_with_book(s)
	SimSpendBook.adopt_line(s, 0)
	SimSpendBook.adopt_line(s, 6)
	var b := await _open(s)
	_page(b, "spend")
	await _shot("spend_generated")

	# THE FALLBACK BARE BOOK carrying legacy levers (the absorb path)
	var sf := _mid()
	sf.spend_book = []
	sf.budgets = {"ads": 2000, "content": 500, "referrals": 0, "outbound": 0,
		"sales": 1000, "care": 500, "rnd": 2000, "office": 500}
	var bf := await _open(sf)
	_page(bf, "spend")
	await _shot("spend_fallback")

	# THE ADD DOOR, staged into a bucket (the receipt before the ADD arm)
	var sa := _mid()
	_with_book(sa)
	var ba := await _open(sa)
	_page(ba, "spend", {"mode": "add", "staged": "care"})
	await _shot("spend_add_door")

	# A CONTRACT LINE STOPPING — the notice bills through, rendered
	var ss := _mid()
	_with_book(ss)
	SimSpendBook.adopt_book(ss)
	SimSpendBook.stop_line(ss, 2, ss.week - 1)   # on-call rotation, notice 4
	var bs := await _open(ss)
	_page(bs, "spend")
	await _shot("spend_stopping")

# ── team ─────────────────────────────────────────────────────────────────────
func _team() -> void:
	# RUNG 1 — flat rows: an ask, a vesting bar, an onboarding hire, the seat
	var s := _mid()
	s.esop = {"pool_pct": 10.0, "granted": [
		{"emp_id": "Priya Raman", "pct": 0.4, "vest_start_wk": 2}]}
	s.pipeline = [{"name": "Ines Cardoso", "role": "support", "salary": 900, "weeks_in": 1}]
	s.open_roles = [{"role": "sales", "offered_salary": 700, "opened_week": 32, "seats": 1}]
	s.applicants = [
		{"name": "Mara Voss", "role": "sales", "skill": 4, "ask": 1_700, "applied_week": 33,
			"source": "inbound", "one_liner": ""},
		{"name": "Yusuf Adeyemi", "role": "sales", "skill": 3, "ask": 1_250, "applied_week": 33,
			"source": "referral", "one_liner": ""},
		{"name": "Hanne Skov", "role": "sales", "skill": 5, "ask": 2_100, "applied_week": 31,
			"source": "inbound", "one_liner": ""},
	]
	var b := await _open(s)
	_page(b, "team")
	await _shot("team_rung1")

	# RUNG 2 — function groups, the askers face-up, one group open
	var s2 := _mid()
	_crowd(s2, 14)
	s2.esop = {"pool_pct": 10.0, "granted": [
		{"emp_id": "Ada Whitlock", "pct": 0.3, "vest_start_wk": 24}]}
	var b2 := await _open(s2)
	_page(b2, "team", {"fn_engineer": true})
	await _shot("team_rung2")

	# RUNG 3 — business units (default mapping), one unit opened to its
	# functions: the same grouped-row component recursed
	var s3 := _mid("hq")
	_crowd(s3, 60)
	var b3 := await _open(s3)
	_page(b3, "team", {"unit_open": "engineering", "u_engineering_engineer": true})
	await _shot("team_rung3")

# ── bills ────────────────────────────────────────────────────────────────────
func _bills() -> void:
	# THE OFFICE FLOOR: both sections, the seat trend, NOL banked, standing
	var s := _mid()
	s.open_roles = [{"role": "sales", "offered_salary": 560, "opened_week": 32, "seats": 1}]
	var b := await _open(s)
	_page(b, "bills")
	await _shot("bills_office")

	# SITES + INTEREST-ONLY DEBT: second roofs on the flat; the trend column
	# teaching "never falls on its own"; the tools fold opened
	var s2 := _mid("floor")
	s2.sites = [{"id": "site_lyon", "name": "Lyon", "rent_wk": 2_400, "wage_mult": 0.9,
		"learning_count": 0, "demand_weight": 0.8, "opened_wk": 30}]
	s2.loans = [{"kind": "venture", "principal": 24_000, "balance": 24_000, "rate_wk": 0.03,
		"term_wk": 26, "taken_week": 20, "pay_wk": 0, "missed": 0}]
	s2.loan_principal = 0
	var b2 := await _open(s2)
	_page(b2, "bills", {"tools_open": true})
	await _shot("bills_sites")

# ── the bank ─────────────────────────────────────────────────────────────────
func _bank() -> void:
	# THE MEETING, lively: an amortizing note + the legacy shark, a live
	# quote, the refinance preview, the stairs
	var s := _mid()
	var b := await _open(s)
	_page(b, "the bank")
	await _shot("bank_meeting")

	# THE VENTURE BALLOON near + venture debt on offer (floor, round raised)
	var sv := _mid("floor")
	sv.cash = 320_000
	sv.traction = 640
	sv.rounds_raised = ["pre-seed", "seed"] as Array[String]
	sv.last_round_amount = 2_400_000
	sv.loans = [
		{"kind": "bank", "principal": 20_000, "balance": 14_000, "rate_wk": 0.05,
			"term_wk": 26, "taken_week": 20, "pay_wk": 972, "missed": 0},
		{"kind": "venture", "principal": 30_000, "balance": 30_000, "rate_wk": 0.03,
			"term_wk": 12, "taken_week": 24, "pay_wk": 0, "missed": 0},
	]
	sv.loan_principal = 0
	var bv := await _open(sv)
	_page(bv, "the bank")
	await _shot("bank_meeting_venture")

	# LOCKED: two misses — zone 1 and zone 3 teach the lock
	var sl := _mid()
	sl.cash = 900
	(sl.loans[0] as Dictionary)["missed"] = 2
	var bl := await _open(sl)
	_page(bl, "the bank")
	await _shot("bank_meeting_locked")

	# THE GARAGE: no bank answers — the zones teach instead of greying
	var sg := _mid("garage")
	sg.cash = 3_100
	sg.loans = []
	sg.loan_principal = 2_000
	var bg := await _open(sg)
	_page(bg, "the bank")
	await _shot("bank_meeting_garage")

	# BOOKS — the full grouped statement on the ledger sheet
	var sb := _mid()
	sb.receivables = [{"name": "invoiced on net-30", "cash_wk": 3_100, "weeks_left": 2}]
	var bb := await _open(sb)
	_page(bb, "the bank", {"mode": "books"})
	await _shot("bank_books")
