extends RefCounted
## LANE SUITE — the works (L-DIVWORKS live pins). Spec: docs/design/
## DECISIONS.md (the factory → THE WORKS + SCALE LADDER) + DAG2.md.
##
## The W1 stub pins asserted neutrality; the lane is live, so these pins hold
## the works' own contract: capacity is read from the records each type
## really has (crew hours, care ceiling, machines, seller pool), the gap is
## priced honestly (un-billed revenue for service/marketplace, churn for
## software, the factory's own books for hardware), relief valves are
## standing levers clamped at the engine, and the mutation executors price
## what the mutation law says they price.
##
## The porting law: a check lands HERE first, then in the same order in
## unity/Runway.Core.Tests/Lanes/WorksTests.cs. Same checks, same order, same
## logic — behaviour and bands, never a cross-engine draw.

static func _base(what: String, who: String) -> GameState:
	var s := GameState.new()
	s.sim_seed = 4242
	s.week = 12
	s.era = "office"
	s.cash = 60_000
	s.traction = 90
	s.product = 50
	s.morale = 70
	s.hype = 30
	s.biz_what = what
	s.biz_who = who
	s.theta = SimEngine.default_theta(what, who)
	s.set_flag("launched")
	return s

static func _priced(s: GameState, name: String, unit: String, fair: float,
		cost: float, weight: float = 1.0) -> void:
	SimEngine.add_offer(s, name, unit, fair, cost, 2.0, weight)
	var od: Dictionary = s.offers[s.offers.size() - 1]
	od["price"] = fair
	od["price_set"] = true

static func run(ok: Callable) -> void:
	# ── 1 · no offers, no works: quiet attention, quiet directives
	var s0 := _base("Service", "Consumer")
	ok.call(SimWorks.attention(s0).is_empty() and SimWorks.directives(s0).is_empty(),
		"works: without a catalog the works stays quiet")

	# ── 2 · the neutral (offer-less) tick books no relief
	var s1 := _base("Service", "Consumer")
	SimEngine.weekly_tick(s1)
	var pnl: Dictionary = s1.get_meta("pnl", {})
	ok.call(int(pnl.get("relief", -1)) == 0,
		"works: the neutral tick books no relief spend")

	# ── 3 · all three DAG2 lanes are registered in the record, at zero here
	ok.call(int(pnl.get("recruit_ads", -1)) == 0 and int(pnl.get("relief", -1)) == 0
		and int(pnl.get("site_rent", -1)) == 0,
		"works: the three new pnl lanes are pre-registered at zero")

	# ── 4 · the identity the twin suite pins still balances
	ok.call(int(pnl.get("net", 1)) == int(pnl.get("revenue", 0)) - int(pnl.get("burn", 0))
		- int(pnl.get("liabilities_wk", 0)) - int(pnl.get("interest", 0))
		- int(pnl.get("tax", 0)),
		"works: the pnl identity holds with the new lanes registered")

	# ── 5 · a site tag on a person rides the tick untouched
	var s2 := _base("Service", "Consumer")
	s2.employees = [{"name": "June Park", "role": "therapist", "salary": 1_500,
		"burnout": 10, "quirk": "", "skill": 4, "hired_week": 3, "site": "site_lyon"}]
	SimEngine.weekly_tick(s2)
	ok.call(s2.employees.size() == 1
		and String((s2.employees[0] as Dictionary).get("site", "")) == "site_lyon",
		"works: a site tag on an employee survives the tick")

	# ── 6 · service capacity is the crew's hands: the founder + serving heads
	# at their skill; the sellers don't serve, and a ramping hand gives zero
	var s3 := _base("Service", "SMB")
	_priced(s3, "the classic", "per session", 80.0, 31.0)
	ok.call(is_equal_approx(SimWorks.service_capacity(s3), 26.0),
		"works: a solo founder's hands hold 26 slots")
	s3.employees = [
		{"name": "June Park", "role": "therapist", "salary": 1_500, "burnout": 10,
			"quirk": "", "skill": 4, "hired_week": 3, "site": ""},
		{"name": "Sal Ory", "role": "sales lead", "salary": 1_200, "burnout": 10,
			"quirk": "", "skill": 4, "hired_week": 3, "site": ""}]
	ok.call(is_equal_approx(SimWorks.service_capacity(s3), 26.0 + 26.0),
		"works: a skill-4 hand adds 26 slots and a seller adds none")
	SimDivisions._mark(s3, "works_ramp", "June Park", s3.week + 1)
	ok.call(is_equal_approx(SimWorks.service_capacity(s3), 26.0),
		"works: a ramping hand gives zero this week")

	# ── 7 · the service gap is UN-BILLED revenue: the record's revenue drops
	# by the walked share and the journal receipts it
	var s4 := _base("Service", "SMB")
	_priced(s4, "the classic", "per session", 80.0, 31.0)
	var rep4 := SimEngine.weekly_tick(s4)
	var pnl4: Dictionary = s4.get_meta("pnl", {})
	var w4 := SimWorks.week_view(s4)
	ok.call(float(w4.get("walk_units", 0.0)) >= 1.0,
		"works: ninety wanted sessions overflow a solo founder's hands")
	ok.call(int(pnl4.get("revenue", 0)) < int(round(float(s4.traction) * SimEngine.offers_arpu(s4))),
		"works: walked sessions are un-billed — revenue is smaller than customers × price")
	var said4 := false
	for l in rep4.get("lines", []):
		if String(l).contains("turned away"):
			said4 = true
	ok.call(said4, "works: the walk is receipted, not silent")
	ok.call(int(pnl4.get("net", 1)) == int(pnl4.get("revenue", 0)) - int(pnl4.get("burn", 0))
		- int(pnl4.get("liabilities_wk", 0)) - int(pnl4.get("interest", 0))
		- int(pnl4.get("tax", 0)),
		"works: the identity holds while revenue walks")

	# ── 8 · the freelance valve: clamped at the engine, billed per unit
	# actually served, and it shrinks the walk
	var s5 := _base("Service", "SMB")
	_priced(s5, "the classic", "per session", 80.0, 31.0)
	s5.price_book = {"freelance_rate": 48}
	ok.call(SimWorks.relief_set(s5, "freelance", 999) == 60,
		"works: the freelance cap clamps at the engine, not the desk")
	SimWorks.relief_set(s5, "freelance", 20)
	SimEngine.weekly_tick(s5)
	var pnl5: Dictionary = s5.get_meta("pnl", {})
	var w5 := SimWorks.week_view(s5)
	ok.call(float(w5.get("relief_used", 0.0)) >= 1.0
		and int(pnl5.get("relief", 0)) == int(round(float(w5.get("relief_used", 0.0)) * 48.0)),
		"works: freelancers bill per unit served at the price book's rate")
	ok.call(float(w5.get("walk_units", 99.0)) < float(SimWorks.week_view(s4).get("walk_units", 0.0)),
		"works: the valve open, fewer sessions walk than with it closed")

	# ── 9 · software degrades instead of turning away: over the ceiling churn
	# bites (the documented 0.4pt/wk multiplier), under it nothing does
	var s6 := _base("Software", "Consumer")
	_priced(s6, "the plan", "per month", 18.0, 4.0)
	s6.traction = 3_000   # far over the 400-seat free ceiling at zero care spend
	SimEngine.weekly_tick(s6)
	var w6 := SimWorks.week_view(s6)
	ok.call(float(w6.get("over", 0.0)) > 0.0 and int(w6.get("degrade_walked", 0)) >= 1,
		"works: past the ceiling the queue churns people — degradation, not lost sales")
	ok.call(float(w6.get("unbilled", 0.0)) < 1.0,
		"works: software never un-bills — its gap is churn, not walked revenue")
	var s7 := _base("Software", "Consumer")
	_priced(s7, "the plan", "per month", 18.0, 4.0)
	s7.traction = 120
	SimEngine.weekly_tick(s7)
	ok.call(int(SimWorks.week_view(s7).get("degrade_walked", 0)) == 0,
		"works: under the ceiling nobody churns to the queue")

	# ── 10 · the marketplace starves on growth and the recruit push feeds it
	var s8 := _base("Marketplace", "Consumer")
	_priced(s8, "a matched order", "per order", 9.0, 3.5)
	s8.last_growth = 0.30
	SimEngine.weekly_tick(s8)
	var w8 := SimWorks.week_view(s8)
	ok.call(float(w8.get("walk_units", 0.0)) >= 1.0,
		"works: fast growth outruns the seller pool and shelves go empty")
	var s9 := _base("Marketplace", "Consumer")
	_priced(s9, "a matched order", "per order", 9.0, 3.5)
	s9.last_growth = 0.30
	SimWorks.relief_set(s9, "recruit_supply", 500)
	SimEngine.weekly_tick(s9)
	ok.call(float(SimWorks.week_view(s9).get("walk_units", 99.0)) < float(w8.get("walk_units", 0.0))
		and int((s9.get_meta("pnl", {}) as Dictionary).get("relief", 0)) == 500,
		"works: the recruit push spends whole and closes part of the gap")

	# ── 11 · hardware stays the factory's: the works books no relief and
	# never un-bills on top of the factory's own honest billing
	var s10 := _base("Hardware", "Consumer")
	_priced(s10, "Pocket Synth", "per unit", 100.0, 20.0)
	SimEngine.weekly_tick(s10)
	ok.call(int((s10.get_meta("pnl", {}) as Dictionary).get("relief", -1)) == 0,
		"works: on hardware the factory owns the molecule — the works books nothing")

	# ── 12 · the unit ticket is the offer's cost lines × learning + the
	# feature inventory's per-unit adds
	var s11 := _base("Service", "SMB")
	SimEngine.add_offer(s11, "the classic", "per session", 80.0, 31.0, 2.0, 1.0,
		[{"label": "hands, 50 min", "amount": 22.0}, {"label": "oils & linens", "amount": 4.0},
			{"label": "room & laundry", "amount": 5.0}], [])
	(s11.offers[0] as Dictionary)["price"] = 80.0
	(s11.offers[0] as Dictionary)["price_set"] = true
	s11.features = [{"id": "f1", "name": "the loyalty card", "job": "keep",
		"family": "", "solidity": "solid", "keep_wk": 12, "unit_cost_add": 1.5,
		"product_id": "", "born_wk": 1, "measured": 0.0}]
	var t11 := SimWorks.unit_ticket(s11, 0)
	ok.call(is_equal_approx(float(t11.get("cost_each", 0.0)),
		31.0 * SimEngine.learning_curve(s11) + 1.5),
		"works: the ticket is cost lines × learning at the total, plus the features' share")
	ok.call((t11.get("lines", []) as Array).size() == 4,
		"works: the ticket itemizes its lines and the features' share rides last")

	# ── 13 · the gap raises the works' own attention on the works desk
	var s12 := _base("Service", "SMB")
	_priced(s12, "the classic", "per session", 80.0, 31.0)
	SimEngine.weekly_tick(s12)
	var rows12 := SimWorks.attention(s12)
	var gap_row := false
	for r in rows12:
		if String((r as Dictionary).get("desk", "")) == "the works" \
				and String((r as Dictionary).get("key", "")) == "works_gap":
			gap_row = true
	ok.call(gap_row, "works: money walking raises a warn on the works desk")

	# ── 14 · retire_product: offers retire, exactly half the customers
	# migrate, the rest churn — and the flagship alone can never retire
	var s13 := _base("Software", "SMB")
	_priced(s13, "Core", "per month", 18.0, 4.0, 1.0)
	_priced(s13, "Legacy API", "per month", 12.0, 9.0, 1.0)
	SimDivisions.tag_offer(s13, 1, "legacy")
	var t13 := s13.traction
	var res13 := SimWorks.retire_product(s13, "legacy")
	ok.call(bool(res13.get("ok", false)) and s13.offers.size() == 1
		and t13 - s13.traction == int(res13.get("churned", 0))
		and int(res13.get("churned", 0)) == int(floor(float(t13) * 0.5 * 0.5)),
		"works: retiring a product churns exactly the un-migrated half of its share")
	ok.call(not bool(SimWorks.retire_product(s13, "").get("ok", true)),
		"works: the only product cannot retire — that is a pivot")

	# ── 15 · fire_account: the penalty bills, the revenue dies, the street hears
	var s14 := _base("Service", "SMB")
	_priced(s14, "the classic", "per session", 80.0, 31.0)
	s14.price_book = {"account_fire_penalty": 1_200}
	var cash14 := s14.cash
	var t14 := s14.traction
	var res14 := SimWorks.fire_account(s14)
	ok.call(bool(res14.get("ok", false)) and cash14 - s14.cash == 1_200
		and t14 - s14.traction == 1 and SimEngine.has_status(s14, "rival_fud"),
		"works: firing an account bills the penalty, kills the revenue, and the street hears")

	# ── 16 · refinance_note: today's standing, the break fee, and no laundering
	var s15 := _base("Service", "SMB")
	_priced(s15, "the classic", "per session", 80.0, 31.0)
	s15.price_book = {"refinance_break_fee": 350}
	s15.loans = [{"kind": "bank", "principal": 10_000, "balance": 8_000,
		"rate_wk": 0.11, "term_wk": 12, "taken_week": 2, "pay_wk": 1_200, "missed": 0}]
	var cash15 := s15.cash
	var res15 := SimWorks.refinance_note(s15, 0, 12)
	var note15: Dictionary = s15.loans[0]
	ok.call(bool(res15.get("ok", false)) and cash15 - s15.cash == 350
		and is_equal_approx(float(note15.get("rate_wk", 0.0)), SimBank.bank_rate_wk(s15))
		and int(note15.get("pay_wk", 0)) == SimBank.loan_payment_wk(8_000, SimBank.bank_rate_wk(s15), 12),
		"works: refinance swaps to today's standing for the break fee")
	(s15.loans[0] as Dictionary)["missed"] = 2
	ok.call(not bool(SimWorks.refinance_note(s15, 0, 12).get("ok", true)),
		"works: a distressed note never refinances — the miss ladder cannot be laundered")
