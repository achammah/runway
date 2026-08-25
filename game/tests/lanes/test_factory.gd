extends RefCounted
## LANE SUITE — factory. Spec: docs/design/09-hardware.md §14 (the twin pins).
##
## `tests/sim_engine_test.gd` calls run() after the engine's own checks and
## hands over `ok`, the same assert the whole suite uses.
##
## The porting law: a check lands HERE first, then in the same order in
## unity/Runway.Core.Tests/Lanes/FactoryTests.cs. Same checks, same order, same
## logic — the two engines do not share PRNG internals, so nothing below pins a
## draw across them, only behaviour.

## A live Hardware run with a priced flagship: $100 per unit, $20 of parts.
static func _hw(era: String = "garage") -> GameState:
	var s := GameState.new()
	s.sim_seed = 42
	s.week = 5
	s.cash = 50_000
	s.traction = 40
	s.product = 50
	s.morale = 70
	s.hype = 40
	s.biz_what = "Hardware"
	s.biz_who = "Consumer"
	s.era = era
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	SimEngine.add_offer(s, "Pocket Synth", "per unit", 100.0, 20.0, 2.0, 1.0)
	(s.offers[0] as Dictionary)["price"] = 100.0
	(s.offers[0] as Dictionary)["price_set"] = true
	s.set_flag("launched")
	return s

static func _rep() -> Dictionary:
	return {"lines": [], "fired_clocks": [], "expired": [], "events": []}

static func _pnl(s: GameState) -> Dictionary:
	var p: Dictionary = s.get_meta("pnl", {})
	return p

static func run(ok: Callable) -> void:
	# ── PIN 1 — STOCKOUT CAPS ADDS. You cannot sell what you did not build:
	# demand exists, the shelf is empty, and every new customer is lost sales
	# (consumer hardware does not backorder). Empty shelves also push the
	# people who came back out: fill 0 → churn ×1.35 exactly.
	var s1 := _hw()
	var hw1 := SimFactory.hw_state(s1)
	hw1["capacity_base"] = 0.0
	hw1["production_target"] = 0
	hw1["stock"] = 0
	var t1 := s1.traction
	var r1 := SimEngine.weekly_tick(s1)
	var w1 := SimFactory.week_block(s1)
	ok.call(int(r1.get("adds", -1)) == 0, "stockout: an empty shelf lands zero customers")
	ok.call(int(w1.get("lost_adds", 0)) > 0, "stockout: the lost sales are receipted, not silent")
	ok.call(s1.traction <= t1, "stockout: traction never rises off an empty shelf")
	ok.call(absf(float(w1.get("fill", 1.0))) < 0.0001
		and absf(SimFactory.starve_churn_mult(s1) - 1.35) < 0.0001,
		"stockout: fill rate 0 makes churn exactly ×1.35")

	# ── PIN 2 — CARRYING BILLS. 50 units of a $20 unit at 2%/wk = $20, and it
	# joins burn like every other dollar.
	var s2 := _hw()
	var hw2 := SimFactory.hw_state(s2)
	hw2["capacity_base"] = 0.0
	hw2["production_target"] = 0
	hw2["stock"] = 50
	s2.traction = 0
	s2.flags.erase("launched")
	SimEngine.weekly_tick(s2)
	var s2b := _hw()
	var hw2b := SimFactory.hw_state(s2b)
	hw2b["capacity_base"] = 0.0
	hw2b["production_target"] = 0
	hw2b["stock"] = 0
	s2b.traction = 0
	s2b.flags.erase("launched")
	SimEngine.weekly_tick(s2b)
	ok.call(int(_pnl(s2).get("carrying", -1)) == 20,
		"carrying: 50 units × 2% of $20 = $20 a week on the shelf")
	ok.call(int(_pnl(s2).get("burn", 0)) - int(_pnl(s2b).get("burn", 0)) == 20,
		"carrying: the shelf's rent is inside burn, to the dollar")

	# ── PIN 3 — MAKE VS BUY. The jobber's ceiling is a multiple of YOUR OWN
	# footprint and the premium is the era's; no learning rides it, and a
	# subcontracted unit never touches the shelf.
	ok.call(SimFactory.sub_cap_units(_hw("office"), 5.0) == 15
		and SimFactory.sub_cap_units(_hw("coworking"), 5.0) == 5
		and SimFactory.sub_cap_units(_hw("garage"), 5.0) == 0,
		"make vs buy: the ceiling is 3× footprint at office, 1× at coworking, shut in the garage")
	ok.call(absf(SimFactory.sub_mult("coworking") - 1.6) < 0.0001
		and absf(SimFactory.sub_mult("floor") - 1.45) < 0.0001
		and absf(SimFactory.sub_mult("hq") - 1.35) < 0.0001,
		"make vs buy: committed volume prices the premium down 1.6× → 1.45× → 1.35×")
	var s3 := _hw("office")
	var hw3 := SimFactory.hw_state(s3)
	hw3["capacity_base"] = 5.0
	hw3["production_target"] = 0
	hw3["stock"] = 0
	hw3["subcontract_on"] = true
	hw3["produced_total"] = 1_000        # a deep learning curve the CM does NOT get
	SimEngine.weekly_tick(s3)
	ok.call(int(SimFactory.week_block(s3).get("sub_units", 0)) == 15
		and int(_pnl(s3).get("subcontract", 0)) == 480,
		"make vs buy: 15 units at 1.6× $20 = $480, with no learning discount")
	ok.call(int((s3.hardware as Dictionary).get("stock", -1)) == 0,
		"make vs buy: made-to-order units never enter stock")

	# ── PIN 4 — A NON-HARDWARE RUN IS UNTOUCHED. No state, no lane, no line,
	# no roll, no row: the absence is the test.
	var s4 := GameState.new()
	s4.sim_seed = 42
	s4.week = 5
	s4.cash = 50_000
	s4.traction = 40
	s4.product = 50
	s4.biz_what = "Software"
	s4.biz_who = "SMB"
	s4.theta = SimEngine.default_theta(s4.biz_what, s4.biz_who)
	var r4 := SimEngine.weekly_tick(s4)
	var p4 := _pnl(s4)
	var hw_words := false
	for l in r4.get("lines", []):
		var ls := String(l)
		if ls.contains("STOCKOUT") or ls.contains("carrying ") or ls.contains("built ") \
				or ls.contains("make vs buy") or ls.contains("machine down"):
			hw_words = true
	ok.call(s4.hardware.is_empty() and SimFactory.week_block(s4).is_empty(),
		"off Hardware: the factory state is never allocated")
	ok.call(int(p4.get("production", 0)) == 0 and int(p4.get("subcontract", 0)) == 0
		and int(p4.get("equip_upkeep", 0)) == 0 and int(p4.get("carrying", 0)) == 0,
		"off Hardware: none of the four factory lanes carry a dollar")
	ok.call(not hw_words and SimFactory.attention(s4).is_empty()
		and SimFactory.directives(s4).is_empty()
		and absf(SimFactory.clamp_adds(s4, _rep(), 7.5) - 7.5) < 0.0001,
		"off Hardware: no receipt, no bang, no directive, and demand is stock-free")

	# ── PIN 5 — DETERMINISM. The salt-110 breakdown stream and the salt-111
	# repurchase remainder replay exactly, six weeks running.
	var s5a := _hw("office")
	var s5b := _hw("office")
	for st in [s5a, s5b]:
		var stt: GameState = st
		var h := SimFactory.hw_state(stt)
		h["capacity_base"] = 40.0
		for i in 8:
			(h["equipment"] as Array).append({"id": "jig", "name": "Assembly Jig",
				"capacity_add": 6.0, "upkeep_wk": 15.0, "bought_week": 1})
	var same := true
	for i in 6:
		s5a.week += 1
		s5b.week += 1
		SimEngine.weekly_tick(s5a)
		SimEngine.weekly_tick(s5b)
		var ha: Dictionary = s5a.hardware
		var hb: Dictionary = s5b.hardware
		var pa := _pnl(s5a)
		var pb := _pnl(s5b)
		if int(ha.get("stock", 0)) != int(hb.get("stock", 0)) \
				or int(ha.get("produced_total", 0)) != int(hb.get("produced_total", 0)) \
				or int(pa.get("production", 0)) != int(pb.get("production", 0)) \
				or int(pa.get("subcontract", 0)) != int(pb.get("subcontract", 0)) \
				or int(pa.get("equip_upkeep", 0)) != int(pb.get("equip_upkeep", 0)) \
				or int(pa.get("carrying", 0)) != int(pb.get("carrying", 0)):
			same = false
	ok.call(same, "determinism: same seed and week rebuild the same shelf and the same four lanes")

	# ── PIN 6 — THE LEARNING CURVE AND THE ERA GATE. Wright's law on units
	# BUILT: 10 made = 1 − 0.115·log10(10) = 0.885, and it is what production
	# actually pays. A garage cannot sign for a CNC cell, whatever it has.
	var s6 := _hw()
	var hw6 := SimFactory.hw_state(s6)
	hw6["capacity_base"] = 100.0
	hw6["production_target"] = 20
	hw6["produced_total"] = 10
	ok.call(absf(SimFactory.learning(s6) - 0.885) < 0.0001,
		"learning curve: 10 units built takes 11.5% off the next one")
	var cash6 := s6.cash
	var gate := SimFactory.buy_equipment(s6, "cnc")
	ok.call(not bool(gate.get("ok", true)) and s6.cash == cash6
		and String(gate.get("why", "")).contains("office"),
		"era gate: a garage is refused a CNC cell, and the refusal says why")
	SimEngine.weekly_tick(s6)
	ok.call(int(_pnl(s6).get("production", 0)) == 354,
		"learning curve: 20 units at $20 × 0.885 = $354 of production, not $400")

	# ── THE WAY BACK OUT (docs/design/DECISIONS.md #4): equipment sells back at
	# half price — CAPEX is forgiving, and costly.
	var s7 := _hw()
	var base7 := SimFactory.capacity(s7)
	ok.call(bool(SimFactory.buy_equipment(s7, "jig").get("ok", false))
		and s7.cash == 49_100 and absf(SimFactory.capacity(s7) - (base7 + 6.0)) < 0.0001,
		"a jig costs $900 and puts 6 units a week on the bench")
	var sold := SimFactory.sell_equipment(s7, 0)
	ok.call(bool(sold.get("ok", false)) and s7.cash == 49_550
		and absf(SimFactory.capacity(s7) - base7) < 0.0001
		and int(sold.get("back", 0)) == 450,
		"resale: the secondhand market pays half, and the capacity leaves with it")

	# ── THE FLOOR HOLDS TWELVE. Capacity is bought in lumps, not infinitely.
	var s8 := _hw()
	s8.cash = 1_000_000
	for i in 13:
		SimFactory.buy_equipment(s8, "jig")
	ok.call((SimFactory.hw_view(s8).get("equipment", []) as Array).size() == 12,
		"the fleet caps at 12 machines, and the 13th is refused with a reason")

	# ── AUTO IS A BASE-STOCK POLICY: order up to four weeks of the smoothed
	# forecast, minus the shelf, and never spend a quarter of the cash at once.
	var s9 := _hw()
	var hw9 := SimFactory.hw_state(s9)
	hw9["capacity_base"] = 100.0
	hw9["demand_ema"] = 10.0
	hw9["stock"] = 0
	ok.call(SimFactory.target_now(s9, 100.0, 20.0) == 40,
		"AUTO: four weeks of cover on a 10/wk forecast is a 40-unit order")
	hw9["stock"] = 35
	ok.call(SimFactory.target_now(s9, 100.0, 20.0) == 5,
		"AUTO: what is already on the shelf comes off the order")
	s9.cash = 200
	hw9["stock"] = 0
	ok.call(SimFactory.target_now(s9, 100.0, 20.0) == 2,
		"AUTO: a quarter of $200 at $20 a unit is two units, and no more")
