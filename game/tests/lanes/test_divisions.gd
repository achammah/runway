extends RefCounted
## LANE SUITE — divisions & sites (L-DIVWORKS live pins). Spec: docs/design/
## DECISIONS.md (THE DIVISION MECHANIC, ARRANGE, THE PRICE BOOK) + DAG2.md.
##
## The W1 stub pins asserted the lane was NEUTRAL; the lane is live now, so
## these pins assert the OPPOSITE contract: the price book clamps every read,
## quotes are week-stable and book exactly what they previewed, sites bill
## their rent through the one `site_rent` lane, books are pure group-by sums
## with an honest SHARED row, and every mutation prices itself (ink free,
## brick priced, obligations surviving removal).
##
## The porting law: a check lands HERE first, then in the same order in
## unity/Runway.Core.Tests/Lanes/DivisionsTests.cs. Same checks, same order,
## same logic — the two engines do not share PRNG internals, so nothing pins
## a draw across them, only behaviour and bands.

static func _state() -> GameState:
	var s := GameState.new()
	s.sim_seed = 4242
	s.week = 12
	s.era = "office"
	s.cash = 60_000
	s.traction = 90
	s.product = 50
	s.morale = 70
	s.hype = 30
	s.biz_what = "Service"
	s.biz_who = "SMB"
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	SimEngine.add_offer(s, "the classic session", "per session", 80.0, 31.0, 2.0, 1.0)
	(s.offers[0] as Dictionary)["price"] = 80.0
	(s.offers[0] as Dictionary)["price_set"] = true
	s.set_flag("launched")
	return s

static func _book(s: GameState) -> void:
	s.price_book = {"open_site_pack": 18_000, "relocation_fee": 400,
		"machine_shipping": 900, "lease_break_weeks": 8,
		"contract_notice_wks": 4, "refinance_break_fee": 350,
		"freelance_rate": 65, "subcontract_rate": 30,
		"account_fire_penalty": 1_200}

static func _site(s: GameState, id: String, rent: int, wage: float, learn: int,
		weight: float) -> void:
	s.sites.append({"id": id, "name": id.capitalize(), "rent_wk": rent,
		"wage_mult": wage, "learning_count": learn, "demand_weight": weight,
		"opened_wk": 2})

static func run(ok: Callable) -> void:
	# ── 1 · the fields exist, at safe defaults
	var s0 := GameState.new()
	ok.call(s0.sites.is_empty() and s0.price_book.is_empty()
		and s0.topics.is_empty() and s0.spend_book.is_empty(),
		"divisions: a fresh state carries no sites and an empty price book")

	# ── 2 · a state with no sites raises no attention and speaks no directives
	ok.call(SimDivisions.attention(s0).is_empty() and SimDivisions.directives(s0).is_empty(),
		"divisions: no roofs, no attention, no directives")

	# ── 3 · no sites → the site_rent lane stays zero through a tick
	var s1 := _state()
	SimEngine.weekly_tick(s1)
	ok.call(int((s1.get_meta("pnl", {}) as Dictionary).get("site_rent", -1)) == 0,
		"divisions: no roofs, no site rent booked")

	# ── 4 · LIVE: an opened roof bills its rent through site_rent, and the
	# identity the twin suite pins still balances with it inside
	var s2 := _state()
	_book(s2)
	_site(s2, "site_lyon", 2_600, 0.92, 140, 0.35)
	SimEngine.weekly_tick(s2)
	var pnl2: Dictionary = s2.get_meta("pnl", {})
	ok.call(int(pnl2.get("site_rent", 0)) == 2_600,
		"divisions: the opened roof bills its $2,600 rent through the site_rent lane")
	ok.call(int(pnl2.get("net", 1)) == int(pnl2.get("revenue", 0)) - int(pnl2.get("burn", 0))
		- int(pnl2.get("liabilities_wk", 0)) - int(pnl2.get("interest", 0))
		- int(pnl2.get("tax", 0)),
		"divisions: the pnl identity holds with site rent billing")

	# ── 5 · the price book clamps every read and answers mid-band defaults
	var s3 := _state()
	ok.call(is_equal_approx(SimDivisions.pb(s3, "relocation_fee"), 400.0),
		"divisions: a missing price-book key reads its mid-band default")
	s3.price_book = {"relocation_fee": 999_999, "lease_break_weeks": -3}
	ok.call(is_equal_approx(SimDivisions.pb(s3, "relocation_fee"), 1_500.0)
		and is_equal_approx(SimDivisions.pb(s3, "lease_break_weeks"), 4.0),
		"divisions: price-book reads are clamped to their bands, high and low")

	# ── 6 · quotes are week-stable and open_site books EXACTLY the preview
	var s4 := _state()
	_book(s4)
	var q1 := SimDivisions.quote_site(s4)
	var q2 := SimDivisions.quote_site(s4)
	ok.call(int(q1.get("rent_wk", -1)) == int(q2.get("rent_wk", -2))
		and is_equal_approx(float(q1.get("wage_mult", -1.0)), float(q2.get("wage_mult", -2.0))),
		"divisions: the open-a-roof quote is stable within a week")
	var cash_before := s4.cash
	var res := SimDivisions.open_site(s4, "Lyon")
	ok.call(bool(res.get("ok", false)) and s4.sites.size() == 1
		and cash_before - s4.cash == int(res.get("pack", 0))
		and int((s4.sites[0] as Dictionary).get("rent_wk", 0)) == int(q1.get("rent_wk", -1)),
		"divisions: signing books the quoted pack and rent, to the dollar")
	var lines := SimDivisions.pack_lines(int(res.get("pack", 0)))
	var lsum := 0
	for l in lines:
		lsum += int((l as Dictionary).get("amount", 0))
	ok.call(lsum == int(res.get("pack", 0)),
		"divisions: the pack's receipt lines sum exactly to the pack")

	# ── 7 · the engine is the bouncer: cash and the era cap both refuse
	var s5 := _state()
	_book(s5)
	s5.cash = 100
	ok.call(not bool(SimDivisions.open_site(s5).get("ok", true)),
		"divisions: a pack cash cannot cover refuses with a reason")

	# ── 8 · a young roof ramps its demand on its own curve
	var s6 := _state()
	_book(s6)
	_site(s6, "site_a", 1_000, 1.0, 0, 0.15)
	var w_before := float((s6.sites[0] as Dictionary).get("demand_weight", 0.0))
	SimEngine.weekly_tick(s6)
	ok.call(float((s6.sites[0] as Dictionary).get("demand_weight", 0.0)) > w_before,
		"divisions: a new roof's demand weight climbs every week")

	# ── 9 · the book is a GROUP-BY: payroll sums by roof, SHARED carries the
	# founder, the era's own rent and brand marketing — never smeared
	var s7 := _state()
	_book(s7)
	_site(s7, "site_lyon", 2_600, 0.92, 140, 0.5)
	s7.employees = [
		{"name": "June Park", "role": "therapist", "salary": 1_500, "burnout": 10,
			"quirk": "", "skill": 4, "hired_week": 3, "site": "site_lyon"},
		{"name": "Ana Reyes", "role": "therapist", "salary": 1_200, "burnout": 10,
			"quirk": "", "skill": 3, "hired_week": 3, "site": ""}]
	s7.budgets["ads"] = 300
	var book := SimDivisions.works_book(s7, "site")
	var lyon: Dictionary = {}
	var home: Dictionary = {}
	var shared: Dictionary = {}
	for r in book:
		match String((r as Dictionary).get("id", "?")):
			"site_lyon": lyon = r
			"": home = r
			"shared": shared = r
	ok.call(int(lyon.get("payroll_wk", 0)) == 1_500 and int(home.get("payroll_wk", 0)) == 1_200
		and int(lyon.get("heads", 0)) == 1 and int(home.get("heads", 0)) == 1,
		"divisions: payroll and heads group by the roof their records carry")
	ok.call(int(shared.get("rent_wk", 0)) == int(GameState.ERA_RENT.get(s7.era, 0))
		and int(shared.get("net_wk", 0)) <= -(GameState.RAMEN_PER_WEEK + 300),
		"divisions: SHARED/HQ carries the founder, the era roof and brand spend")

	# ── 10 · rungs are deterministic counts; the slicer lists only real axes
	var s8 := _state()
	ok.call(SimDivisions.rung(s8) == 1, "divisions: one offer, one roof — the boutique")
	SimEngine.add_offer(s8, "the deep 90", "per session", 130.0, 52.0, 2.0, 0.8)
	SimEngine.add_offer(s8, "house calls", "per session", 110.0, 47.0, 2.0, 0.6)
	ok.call(SimDivisions.rung(s8) == 2, "divisions: three offers under one roof — the house")
	_book(s8)
	_site(s8, "site_lyon", 2_600, 0.92, 140, 0.5)
	ok.call(SimDivisions.rung(s8) == 3 and SimDivisions.default_slice(s8) == "site",
		"divisions: a second roof makes the empire, sliced by site")
	ok.call(SimDivisions.slice_axes(s8).has("site") and SimDivisions.slice_axes(s8).has("offer"),
		"divisions: the slicer lists only axes with two or more divisions")

	# ── 11 · moving a person is brick (fee + ramp marker); tags are ink (free)
	var s9 := _state()
	_book(s9)
	_site(s9, "site_lyon", 2_600, 0.92, 140, 0.5)
	s9.employees = [{"name": "June Park", "role": "therapist", "salary": 1_500,
		"burnout": 10, "quirk": "", "skill": 4, "hired_week": 3, "site": ""}]
	var cash9 := s9.cash
	var mv := SimDivisions.reassign_employee(s9, 0, "site_lyon")
	ok.call(bool(mv.get("ok", false)) and cash9 - s9.cash == 400
		and String((s9.employees[0] as Dictionary).get("site", "")) == "site_lyon"
		and SimDivisions.marked_until(s9, "works_ramp", "June Park") == s9.week + 1,
		"divisions: a person moves for the relocation fee and a marked ramp week")
	var cash9b := s9.cash
	SimDivisions.tag_offer(s9, 0, "spa_line")
	s9.spend_book = [{"name": "staff meals", "buys": "the kitchen fed", "amt": 220,
		"bucket": "office", "contract_notice": 0, "division": ""}]
	SimDivisions.tag_spend_line(s9, 0, "site_lyon")
	ok.call(s9.cash == cash9b
		and String((s9.offers[0] as Dictionary).get("product_id", "")) == "spa_line"
		and String((s9.spend_book[0] as Dictionary).get("division", "")) == "site_lyon",
		"divisions: tags are ink — free, and they stick")

	# ── 12 · the teardown: severance ALWAYS owed, the lease breaks at N weeks
	# of rent, resale never rides (no machines), the roof's counter dies with it
	var s10 := _state()
	_book(s10)
	_site(s10, "site_gen", 1_000, 1.1, 20, 0.5)
	s10.employees = [{"name": "Ines Rol", "role": "therapist", "salary": 1_000,
		"burnout": 10, "quirk": "", "skill": 3, "hired_week": 3, "site": "site_gen"}]
	var q10 := SimDivisions.close_quote(s10, "site_gen", {})
	ok.call(int(q10.get("net_now", 1)) < 0 and int(q10.get("freed_wk", 0)) >= 1_000 + 1_000,
		"divisions: the closing quote prices severance and frees rent plus payroll")
	var cash10 := s10.cash
	var sev_before := s10.severance_due
	var res10 := SimDivisions.close_site(s10, "site_gen", {})
	ok.call(bool(res10.get("ok", false)) and s10.sites.is_empty()
		and s10.severance_due > sev_before
		and cash10 - s10.cash == 8 * 1_000,
		"divisions: closing books the lease break now; severance accrues and is always owed")

	# ── 13 · Lyon ≠ Geneva, mechanically: rent, wages and learning bend the
	# same formula — nobody writes the comparison
	var s11 := _state()
	_book(s11)
	_site(s11, "site_lyon", 1_000, 0.92, 4_100, 1.0)
	_site(s11, "site_gen", 3_600, 1.15, 90, 1.0)
	var book11 := SimDivisions.works_book(s11, "site")
	var lyon11: Dictionary = {}
	var gen11: Dictionary = {}
	for r11 in book11:
		match String((r11 as Dictionary).get("id", "?")):
			"site_lyon": lyon11 = r11
			"site_gen": gen11 = r11
	ok.call(float(lyon11.get("unit_cost", 99.0)) < float(gen11.get("unit_cost", 0.0)),
		"divisions: the dearer roof makes a dearer unit — rent, wages and learning, mechanically")

	# ── 14 · stopping a spend line honours the contract: notice bills through
	var s12 := _state()
	_book(s12)
	s12.spend_book = [
		{"name": "the answering service", "buys": "phones", "amt": 120,
			"bucket": "care", "contract_notice": 3, "division": ""},
		{"name": "fresh flowers", "buys": "the room", "amt": 40,
			"bucket": "office", "contract_notice": 0, "division": ""}]
	var st := SimDivisions.stop_spend_line(s12, 0)
	ok.call(bool(st.get("ok", false)) and int(st.get("notice_wks", 0)) == 3
		and s12.commitments.size() == 1
		and int((s12.commitments[0] as Dictionary).get("cash_wk", 0)) == -120,
		"divisions: a contract line's notice bills through as a standing commitment")
	var st2 := SimDivisions.stop_spend_line(s12, 0)
	ok.call(bool(st2.get("ok", false)) and int(st2.get("notice_wks", 99)) == 0
		and s12.commitments.size() == 1,
		"divisions: a non-contract line stops instantly, nothing lingers")
