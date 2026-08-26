extends SceneTree
## W4 BALANCE diagnostic probe — a COPY of the smoke.gd 30-run sim loop with
## per-run instrumentation, plus a real-engine business-type spread harness.
## Never a gate; smoke.gd stays the gate. Run:
##   godot --headless --path . --script tests/balance_probe.gd

func _init() -> void:
	var content := ContentDb.new()
	content.load_all()

	print("=== PART A: the smoke 30-run loop, instrumented ===")
	print("%-4s %-7s %-8s %-4s %-9s %-5s %-5s %-5s %-5s %-7s %-7s %-6s %-8s" % [
		"run", "outcome", "cause", "wk", "start$", "prod", "trac", "mor", "hype",
		"evt$net", "evts", "laptop", "launched"])
	var deaths := 0
	var victories := 0
	var stalls := 0
	var death_weeks: Array = []
	var cash_deaths := 0
	var morale_deaths := 0
	var evt_cash_all := 0
	var evt_count_all := 0
	for trial in 30:
		var rng := SeededRng.new(1000 + trial)
		var state := GameState.new()
		var all_ids := content.items.keys()
		for i in rng.randi_range(3, 6):
			var id: String = rng.pick(all_ids)
			if not state.items.has(id):
				state.items.append(id)
				state.cash += int(content.items[id].get("cash_value", 0))
		if state.cash == 0:
			state.cash = 1500
		var start_cash := state.cash
		var outcome := "stall"
		var cause := "-"
		var evt_cash := 0     # net cash moved by event/timebomb effects
		var evt_n := 0
		for week in 40:
			state.week += 1
			state.cash -= state.burn_per_week()
			state.product = clampi(state.product + 4 + (2 if state.has_item("itm_laptop") else 0), 0, 100)
			if state.product >= 40:
				state.traction += 1 + int(state.hype / 20.0)
			if state.cash < 0:
				outcome = "death"
				cause = "cash"
				break
			for tb in state.timebombs.duplicate():
				tb["weeks_left"] -= 1
				if tb["weeks_left"] <= 0:
					state.timebombs.erase(tb)
					var bomb: Dictionary = content.events.get(tb["event"], {})
					if not bomb.is_empty():
						var bc: Dictionary = rng.pick(bomb["choices"])
						var pre_b := state.cash
						EffectOps.apply_all(bc["effects"], state)
						evt_cash += state.cash - pre_b
			var pool := content.eligible_events(state)
			if not pool.is_empty():
				var ev: Dictionary = rng.weighted_pick(pool)
				var choice: Dictionary = rng.pick(ev["choices"])
				var pre := state.cash
				EffectOps.apply_all(choice["effects"], state)
				evt_cash += state.cash - pre
				evt_n += 1
			if state.morale <= 0:
				outcome = "death"
				cause = "morale"
				break
			if state.product >= 60 and state.traction >= 10:
				outcome = "victory"
				cause = "-"
				break
		match outcome:
			"death":
				deaths += 1
				death_weeks.append(state.week)
				if cause == "cash": cash_deaths += 1
				else: morale_deaths += 1
			"victory": victories += 1
			_: stalls += 1
		evt_cash_all += evt_cash
		evt_count_all += evt_n
		print("%-4d %-7s %-8s %-4d %-9d %-5d %-5d %-5d %-5d %-7d %-7d %-6s %-8s" % [
			trial, outcome, cause, state.week, start_cash, state.product, state.traction,
			state.morale, state.hype, evt_cash, evt_n,
			"y" if state.has_item("itm_laptop") else "n",
			"y" if state.has_flag("launched") else "n"])
	var mean_dw := 0.0
	for w in death_weeks:
		mean_dw += float(w)
	if not death_weeks.is_empty():
		mean_dw /= float(death_weeks.size())
	print("A-SUMMARY deaths=%d (cash=%d morale=%d) victories=%d stalls=%d mean_death_wk=%.1f" % [
		deaths, cash_deaths, morale_deaths, victories, stalls, mean_dw])
	print("A-SUMMARY total_event_cash=%d over %d event picks (avg %+.0f per pick)" % [
		evt_cash_all, evt_count_all, float(evt_cash_all) / maxf(1.0, float(evt_count_all))])
	print("A-SUMMARY garage burn (no staff, no revenue) = %d/wk; era never advances in this loop" % [
		GameState.new().burn_per_week()])

	# ── the garage event pool, audited: what does a random pick pay on average?
	print("\n=== PART B: garage event pool census (what the RNG can draw) ===")
	var probe_state := GameState.new()
	probe_state.cash = 2000
	var pool0 := content.eligible_events(probe_state)
	var w_sum := 0.0
	var w_cash := 0.0        # weight-x-mean-choice cash per pick
	var w_morale := 0.0
	for ev in pool0:
		var wt := maxf(0.0, float(ev.get("weight", 1)))
		var c_sum := 0.0
		var m_sum := 0.0
		var n := 0
		for ch in ev.get("choices", []):
			n += 1
			for ef in ch.get("effects", []):
				match String(ef.get("op", "")):
					"cash_delta": c_sum += float(ef.get("v", 0))
					"morale_delta": m_sum += float(ef.get("v", 0))
		if n > 0:
			w_sum += wt
			w_cash += wt * c_sum / float(n)
			w_morale += wt * m_sum / float(n)
	print("eligible at start: %d events · E[cash/pick]=%+.0f · E[morale/pick]=%+.1f (weighted, uniform choice)" % [
		pool0.size(), w_cash / maxf(0.001, w_sum), w_morale / maxf(0.001, w_sum)])

	# ── PART C: the candidate fix, pre-validated — the scripted runs start from
	# the game's REAL day-one bank ($8000, founder_draft_screen grants it since
	# the first commit) with banked scramble value on top, instead of the $1500
	# couch-cushion fallback that never matched the game.
	print("\n=== PART C: candidate fix — day-one bank $8000 + banked items ===")
	for start_bank in [8000]:
		var d2 := 0
		var v2 := 0
		var s2 := 0
		var vic_weeks: Array = []
		for trial in 30:
			var rng := SeededRng.new(1000 + trial)
			var state := GameState.new()
			var all_ids := content.items.keys()
			for i in rng.randi_range(3, 6):
				var id: String = rng.pick(all_ids)
				if not state.items.has(id):
					state.items.append(id)
					state.cash += int(content.items[id].get("cash_value", 0))
			state.cash += start_bank
			var outcome := "stall"
			for week in 40:
				state.week += 1
				state.cash -= state.burn_per_week()
				state.product = clampi(state.product + 4 + (2 if state.has_item("itm_laptop") else 0), 0, 100)
				if state.product >= 40:
					state.traction += 1 + int(state.hype / 20.0)
				if state.cash < 0:
					outcome = "death"
					break
				for tb in state.timebombs.duplicate():
					tb["weeks_left"] -= 1
					if tb["weeks_left"] <= 0:
						state.timebombs.erase(tb)
						var bomb: Dictionary = content.events.get(tb["event"], {})
						if not bomb.is_empty():
							var bc: Dictionary = rng.pick(bomb["choices"])
							EffectOps.apply_all(bc["effects"], state)
				var pool := content.eligible_events(state)
				if not pool.is_empty():
					var ev: Dictionary = rng.weighted_pick(pool)
					var choice: Dictionary = rng.pick(ev["choices"])
					EffectOps.apply_all(choice["effects"], state)
				if state.morale <= 0:
					outcome = "death"
					break
				if state.product >= 60 and state.traction >= 10:
					outcome = "victory"
					break
			match outcome:
				"death": d2 += 1
				"victory":
					v2 += 1
					vic_weeks.append(state.week)
				_: s2 += 1
		print("C-SUMMARY bank=%d → deaths=%d victories=%d stalls=%d victory_weeks=%s" % [
			start_bank, d2, v2, s2, str(vic_weeks)])

	# ── PART D: works-capacity sanity — the REAL engine, same scripted play,
	# only the business type varies. 20 seeds × 4 types × 40 weeks. Death is the
	# game's own rule: 3 consecutive weeks in the red. No relief levers touched,
	# so the raw capacity/overflow economics of each type carry the week.
	print("\n=== PART D: business-type spread through SimEngine.weekly_tick ===")
	print("%-12s %-7s %-8s %-8s %-8s %-9s %-10s %-10s" % [
		"type", "deaths", "d-rate", "mean_dw", "end_cust", "end_cash", "unbilled/w", "relief/w"])
	var spread: Dictionary = {}
	for cell in [["Software", "Consumer"], ["Hardware", "Consumer"], ["Marketplace", "Consumer"],
			["Service", "Consumer"], ["Software", "SMB"], ["Hardware", "SMB"],
			["Marketplace", "SMB"], ["Service", "SMB"]]:
		var bt := String(cell[0])
		var t_deaths := 0
		var t_dw := 0.0
		var t_cust := 0.0
		var t_cash := 0.0
		var t_unbilled := 0.0
		var t_relief := 0.0
		var t_weeks := 0.0
		for i in 20:
			var s := GameState.new()
			s.sim_seed = 5000 + i
			s.biz_what = bt
			s.biz_who = String(cell[1])
			s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
			WorldGen.build(s)
			s.cash = GameState.START_CASH   # bootstrap birth, no riders
			var red := 0
			var dead_wk := 0
			for w in range(1, 41):
				s.week = w
				# the same plain founder in every world: build, launch wk 5,
				# modest ads from wk 3, one serving hand at wk 12 if affordable
				s.product = mini(s.product + 2, 100)
				if w == 5:
					s.set_flag("launched")
				if w >= 3:
					s.budgets["ads"] = 200
					s.budgets["content"] = 50
				if w == 12 and s.cash > 8000 and s.employees.size() == 0:
					s.employees.append({"name": "Sam", "role": "ops", "salary": 700, "burnout": 0, "skill": 3})
				SimEngine.weekly_tick(s)
				var wk: Dictionary = s.get_meta("works", {}) if s.has_meta("works") else {}
				t_unbilled += float(wk.get("unbilled", 0.0))
				t_relief += float(wk.get("relief_spend", 0.0))
				t_weeks += 1.0
				if s.cash < 0:
					red += 1
				else:
					red = 0
				if red >= 3:
					dead_wk = w
					break
			if dead_wk > 0:
				t_deaths += 1
				t_dw += float(dead_wk)
			t_cust += float(s.traction)
			t_cash += float(s.cash)
		var cell_name := "%s/%s" % [bt, String(cell[1])]
		spread[cell_name] = float(t_deaths) / 20.0 * 100.0
		print("%-22s %-7s %-8s %-8s %-8d %-9d %-10.1f %-10.1f" % [
			cell_name, "%d/20" % t_deaths, "%.0f%%" % (float(t_deaths) / 20.0 * 100.0),
			("%.1f" % (t_dw / maxf(1.0, float(t_deaths)))) if t_deaths > 0 else "-",
			int(t_cust / 20.0), int(t_cash / 20.0),
			t_unbilled / maxf(t_weeks, 1.0), t_relief / maxf(t_weeks, 1.0)])
	var rates := spread.values()
	rates.sort()
	print("D-SUMMARY death-rate spread = %.0fpt (min %.0f%% max %.0f%%) — flag if >15pt" % [
		float(rates.back()) - float(rates.front()), float(rates.front()), float(rates.back())])
	quit(0)
