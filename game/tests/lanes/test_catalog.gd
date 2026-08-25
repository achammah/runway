extends RefCounted
## LANE SUITE — catalog. Spec: docs/design/01-catalog.md §9 (the twin test pins).
##
## `tests/sim_engine_test.gd` calls run() after the engine's own checks and hands
## over `ok`, the same assert the whole suite uses: ok.call(condition, "what this
## pins").
##
## The porting law: a check lands HERE first, then in the same order in
## unity/Runway.Core.Tests/Lanes/CatalogTests.cs. Same checks, same order, same
## logic — the two engines do not share PRNG internals, so never pin a draw
## across them, only behaviour.

static func _state() -> GameState:
	var s := GameState.new()
	s.sim_seed = 42
	s.week = 5
	s.cash = 50_000
	s.traction = 40
	s.product = 50
	s.morale = 70
	s.hype = 40
	s.biz_what = "Software"
	s.biz_who = "SMB"
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	return s

static func run(ok: Callable) -> void:
	# ── P1 — THE ITEMIZED TRUTH SYNCS AND CLAMPS. Totals can never drift from
	# their receipts, and receipts can never exceed the world.
	var it := {"name": "boxed lunch", "unit": "per order", "fair_price": 70.0,
		"elasticity": 2.0, "weight": 1.0, "price": 0.0,
		"cost_lines": [{"label": "ingredients", "amount": 12.0},
			{"label": "packaging", "amount": 10.0}],
		"fixed_lines": [{"label": "kitchen license", "amount": 30.0}]}
	SimEngine.sync_offer_costs(it)
	ok.call(absf(float(it.unit_cost) - 22.0) < 0.01, "unit_cost = Σ variable lines (22)")
	ok.call(absf(float(it.fixed_wk) - 30.0) < 0.01, "fixed_wk = Σ fixed lines (30)")
	var greedy := {"fair_price": 70.0, "cost_lines": [{"label": "a", "amount": 900.0},
		{"label": "b", "amount": 900.0}, {"label": "c", "amount": 900.0}]}
	SimEngine.sync_offer_costs(greedy)
	ok.call(absf(float((greedy.cost_lines[0] as Dictionary).amount) - 35.0) < 0.01,
		"a hostile line clamps to half of fair (35)")
	ok.call(absf(float(greedy.unit_cost) - 63.0) < 0.01,
		"the variable total clamps to 0.9×fair (63)")

	# ── P2 — THE offer_fixed LANE EXISTS AND THE P&L IDENTITY HOLDS. A standing
	# tool cost is a real lane of burn, not a silent subtraction from cash.
	var fx := _state()
	fx.traction = 10
	fx.set_flag("launched")
	fx.offers = [{"name": "s", "unit": "per session", "price": 70.0,
		"fair_price": 70.0, "unit_cost": 20.0, "weight": 1.0, "fixed_wk": 120.0,
		"fixed_lines": [{"label": "booking tool", "amount": 120.0}]}]
	SimEngine.weekly_tick(fx)
	var pnl_fx: Dictionary = fx.get_meta("pnl", {})
	ok.call(int(pnl_fx.get("offer_fixed", 0)) == 120, "the catalog's fixed lane bills $120")
	ok.call(int(pnl_fx.get("net", 0)) == int(pnl_fx.get("revenue", 0))
			- int(pnl_fx.get("burn", 0)) - int(pnl_fx.get("liabilities_wk", 0))
			- int(pnl_fx.get("interest", 0)) - int(pnl_fx.get("tax", 0)),
		"the P&L identity balances with the offer_fixed lane inside burn")

	# ── P3 — LEARNING CUTS THE VARIABLE TOTAL ONLY; FIXED NEVER LEARNS. A license
	# does not get cheaper because you served customers.
	var lc_s := _state()
	lc_s.served_total = 1000
	lc_s.offers = [{"name": "s", "unit": "per session", "price": 70.0,
		"fair_price": 70.0, "unit_cost": 22.0, "weight": 1.0, "fixed_wk": 30.0}]
	var cpc := SimEngine.offers_cogs_per_customer(lc_s)
	ok.call(cpc > 14.2 and cpc < 14.6, "learning serves 22 at ~14.4 (×0.655)")
	ok.call(absf(SimEngine.offers_fixed_wk(lc_s) - 30.0) < 0.01,
		"fixed lines never learn (30)")

	# ── P4 — HOSTILE NUMBERS CLAMP; THE ERA SHELF REFUSES THE OVERFLOW. The door
	# narrows before the engine's own clamps ever see the terms.
	var cap_s := _state()
	cap_s.era = "coworking"                       # ERA_OFFER_CAP 3
	var o1: Dictionary = SimCatalog.add_offer(cap_s, "big thing", "per unit",
		900_000.0, 900_000.0, 99.0, 99.0)
	ok.call(float(o1.get("fair_price", 0.0)) == 50_000.0
			and float(o1.get("unit_cost", 0.0)) <= 45_000.0
			and float(o1.get("elasticity", 0.0)) == 3.0
			and float(o1.get("weight", 0.0)) <= 3.0,
		"hostile terms pass every clamp")
	SimCatalog.add_offer(cap_s, "b", "per order", 40.0, 10.0, 2.0, 1.0)
	SimCatalog.add_offer(cap_s, "c", "per order", 40.0, 10.0, 2.0, 1.0)
	var o4: Dictionary = SimCatalog.add_offer(cap_s, "d", "per order", 40.0, 10.0, 2.0, 1.0)
	ok.call(o4.is_empty() and cap_s.offers.size() == 3,
		"coworking shelves three offers, the fourth is refused")

	# ── P5 — THE KEYLESS DRAFT IS SEEDED, ITEMIZED, AND IN BAND. Same seed, same
	# week, same draft: a replay shelves the identical offer.
	var dr := _state()                            # seed 42, week 5, SMB
	var d1: Dictionary = SimCatalog.draft_terms(dr, "a weekend workshop")
	var d2: Dictionary = SimCatalog.draft_terms(dr, "a weekend workshop")
	ok.call(String(d1.get("unit", "")) == "per session"
			and absf(float(d1.get("fair_price", 0.0)) - float(d2.get("fair_price", 0.0))) < 0.01,
		"the keyless draft is seeded and repeatable")
	ok.call(float(d1.get("fair_price", 0.0)) >= 32.0
			and float(d1.get("fair_price", 0.0)) <= 52.0,
		"an SMB draft prices inside the jittered band (40×[0.8,1.3])")
	ok.call((d1.get("variable_costs", []) as Array).size() == 2
			and (d1.get("fixed_costs_wk", []) as Array).size() == 1,
		"the draft itemizes: 2 variable lines + 1 fixed line")

	# ── P6 — A CONSCIOUS GIVEAWAY EARNS $0 AND STILL COSTS TO SERVE. The lesson
	# the free tier teaches: revenue is a choice, COGS is not.
	var fr := _state()
	fr.traction = 50
	fr.set_flag("launched")
	fr.offers = [{"name": "free tier", "unit": "per session", "price": 0.0,
		"price_set": true, "fair_price": 70.0, "unit_cost": 18.0, "weight": 1.0}]
	var r_fr := SimEngine.weekly_tick(fr)
	ok.call(int(r_fr.revenue) == 0, "free on purpose earns $0")
	var pnl_fr: Dictionary = fr.get_meta("pnl", {})
	# EXACT, and immune to another lane's adoption tuning: COGS is every customer
	# the giveaway holds, times what one of them costs to serve, learning ×1.0 at
	# a standing start.
	ok.call(fr.traction > 50
			and int(pnl_fr.get("cogs", 0)) == int(round(float(fr.traction) * 18.0)),
		"the giveaway grew the base and paid COGS on every one of them (%d × $18 = $%d)"
			% [fr.traction, int(pnl_fr.get("cogs", 0))])

	# ── P7 — THE SHELF IS A WALLET, AND A LOSING PRICE RAISES ITS HAND.
	# The count cap is not the only door: a customer's weekly budget is finite,
	# so three flagship-weight offers fill it and the next one is refused.
	var wal := _state()
	wal.era = "office"                            # ERA_OFFER_CAP 5 — count is not the binding limit
	SimCatalog.add_offer(wal, "one", "per order", 40.0, 10.0, 2.0, 3.0)
	SimCatalog.add_offer(wal, "two", "per order", 40.0, 10.0, 2.0, 3.0)
	var w3: Dictionary = SimCatalog.add_offer(wal, "three", "per order", 40.0, 10.0, 2.0, 3.0)
	ok.call(w3.is_empty() and absf(SimCatalog.shelf_weight(wal) - 6.0) < 0.01,
		"Σweight 6.0 fills the wallet and the shelf refuses the next offer")
	# a shelf that arrived over the cap by another road is trimmed, with a receipt
	var tam := _state()
	tam.offers = [{"name": "a", "unit": "per order", "fair_price": 40.0, "weight": 5.0},
		{"name": "b", "unit": "per order", "fair_price": 40.0, "weight": 5.0}]
	var tam_rep := {"lines": []}
	SimCatalog.tick_pre(tam, tam_rep)
	ok.call(SimCatalog.shelf_weight(tam) <= 6.001 and (tam_rep["lines"] as Array).size() == 1,
		"a tampered shelf is trimmed to Σ6.0 and says so in the week's receipts")
	# the one lesson a founder must not miss — and the one that is NOT a mistake
	var los := _state()
	los.offers = [{"name": "underwater", "unit": "per order", "price": 10.0,
		"price_set": true, "fair_price": 70.0, "unit_cost": 18.0, "weight": 1.0}]
	var los_rows := SimCatalog.attention(los)
	var gift := _state()
	gift.offers = [{"name": "free tier", "unit": "per order", "price": 0.0,
		"price_set": true, "fair_price": 70.0, "unit_cost": 18.0, "weight": 1.0}]
	ok.call(los_rows.size() == 1
			and String((los_rows[0] as Dictionary).get("key", "")) == "losing_price"
			and int((los_rows[0] as Dictionary).get("severity", 0)) == 2
			and String((los_rows[0] as Dictionary).get("label", "")).length() <= 40
			and SimCatalog.attention(gift).is_empty(),
		"a price under its variable cost raises a warn; a chosen giveaway does not")
