extends SceneTree
## The balance harness (tycoon practice): scripted founder strategies through
## the REAL engine for 50 weeks, tables printed. No LLM — strategies apply the
## same lever/status moves the DM would, so this calibrates the ECONOMY, not
## the prose. Run: godot --headless --path . --script tests/balance_sim.gd

const WEEKS := 50

func _strategies() -> Dictionary:
	return {
		"idle": func(_s: GameState, _w: int) -> void:
			pass,
		"builder": func(s: GameState, w: int) -> void:
			if w == 6:
				s.set_flag("launched")
				s.product = maxi(s.product, 45)
			s.product = mini(s.product + 3, 100)
			s.tech_debt = maxf(s.tech_debt - 1.0, 0.0)
			if w % 8 == 0:
				SimEngine.add_status(s, "founder_flow", 2),
		"seller": func(s: GameState, w: int) -> void:
			if w == 4:
				s.set_flag("launched")
			s.product = mini(s.product + 1, 100)
			s.marketing_budget = 400 if s.cash > 10_000 else 0
			if w % 6 == 0:
				SimEngine.add_status(s, "word_of_mouth", 2)
			s.traction += 2,     # direct founder sales, the written-move analog
		"balanced": func(s: GameState, w: int) -> void:
			if w == 5:
				s.set_flag("launched")
			s.product = mini(s.product + 2, 100)
			if w > 8:
				s.marketing_budget = 300
			if w == 12 and s.traction >= 20:
				var offers := SimEngine.generate_offers(s, s.investors)
				if offers.size() > 0:
					var o: Dictionary = offers[0]
					SimEngine.apply_round(s, int(o.amount), float(o.equity_pct))
					s.set_flag("seed_raised"),
		"reckless": func(s: GameState, w: int) -> void:
			if w == 3:
				s.set_flag("launched")
			SimEngine.add_status(s, "crunch", 2)
			s.product = mini(s.product + 4, 100)
			s.tech_debt = minf(s.tech_debt + 3.0, 100.0)
			s.marketing_budget = 800
			if s.cash < 2000 and s.loan_principal == 0:
				s.loan_principal = 15_000
				s.cash += 15_000,
	}

func _init() -> void:
	call_deferred("_go")

func _go() -> void:
	await process_frame
	print("%-9s %6s %8s %6s %6s %5s %5s %4s %6s" % [
		"strategy", "week", "cash", "cust", "morale", "prod", "debt", "exh", "state"])
	var strats := _strategies()
	for name in strats:
		var s := GameState.new()
		s.sim_seed = 1234
		s.cash = 25_000
		s.traction = 0
		s.product = 20
		s.biz_what = "Software"
		s.biz_who = "SMB"
		s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
		s.investors = [{"name": "Fund A", "thesis": "momentum"}]
		var died_at := 0
		for w in range(1, WEEKS + 1):
			s.week = w
			(strats[name] as Callable).call(s, w)
			SimEngine.weekly_tick(s)
			if s.cash < -5_000 and died_at == 0:
				died_at = w
			if w in [10, 25, 50]:
				print("%-9s %6d %8d %6d %6d %5d %5d %4d %6s" % [
					name, w, s.cash, s.traction, s.morale, s.product,
					int(s.tech_debt), s.exhaustion,
					("DEAD@%d" % died_at) if died_at > 0 else "alive"])
	print("BALANCE SIM DONE")
	quit(0)
