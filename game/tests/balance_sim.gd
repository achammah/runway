extends SceneTree
## C8 — balance harness on the POST-era economy: drives real weekly ticks
## (upkeep, staff, era advancement, event draws with choices) for N seeded runs,
## reports first-death week distribution + era reach + death causes.
func _init() -> void:
	var content := ContentDb.new()
	content.load_all()
	var deaths_at: Array[int] = []
	var causes := {}
	var era_reach := {}
	var victories := 0
	const RUNS := 40
	for trial in RUNS:
		var rng := SeededRng.new(5000 + trial)
		var state := GameState.new()
		state.cash = 12000
		state.items.append_array(["itm_laptop", "itm_savings_jar"])
		state.competences = {"build": 4, "sell": 3, "raise": 2, "recruit": 2, "grit": 3}
		state.cofounders = [{"role": "Business", "commitment": "Full-time", "equity": 30.0, "vesting": true, "loyalty": 70}]
		var cause := ""
		for week in 80:
			state.week += 1
			state.cash -= state.burn_per_week()
			state.product = clampi(state.product + 2 + int(state.competences.get("build", 3)), 0, 100)
			if state.product >= 40:
				state.traction += 1 + int(int(state.competences.get("sell", 3)) / 2.0)
			if state.has_method("weekly_staff_tick"):
				state.weekly_staff_tick()
			if state.has_method("advance_era_if_ready"):
				state.advance_era_if_ready()
			if state.cash < 0:
				state.weeks_in_red += 1
				state.morale = maxi(0, state.morale - 6)
				if state.weeks_in_red >= 3:
					cause = "ramen_zero"
					break
			else:
				state.weeks_in_red = 0
			var pool := content.eligible_events(state)
			if not pool.is_empty():
				var ev: Dictionary = rng.weighted_pick(pool)
				var choice: Dictionary = rng.pick(ev["choices"])
				EffectOps.apply_all(choice.get("effects", []), state)
			if state.morale <= 0:
				cause = "flatline"
				break
			if state.has_flag("acquired_exit"):
				victories += 1
				cause = "sold"
				break
		era_reach[state.era] = int(era_reach.get(state.era, 0)) + 1
		if cause in ["ramen_zero", "flatline"]:
			deaths_at.append(state.week)
			causes[cause] = int(causes.get(cause, 0)) + 1
			# per-death anatomy (RUNWAY_BALANCE_VERBOSE=1) — localizes which death
			# cluster drags the median before anyone tunes weights blind
			if OS.get_environment("RUNWAY_BALANCE_VERBOSE") == "1":
				print("DEATH t=%d wk=%d era=%s cause=%s cash=%d tr=%d rev=%d burn=%d seed=%s staff=%d morale=%d" % [
					trial, state.week, state.era, cause, state.cash, state.traction,
					state.revenue_per_week(), state.burn_per_week(),
					str(state.has_flag("seed_raised")), state.employees.size(), state.morale])
		elif cause == "":
			causes["survived_80wk"] = int(causes.get("survived_80wk", 0)) + 1
	deaths_at.sort()
	var median := deaths_at[deaths_at.size() / 2] if not deaths_at.is_empty() else -1
	print("BALANCE: runs=%d deaths=%d median_death_wk=%d causes=%s era_reach=%s exits=%d" % [
		RUNS, deaths_at.size(), median, JSON.stringify(causes), JSON.stringify(era_reach), victories])
	print("TARGET: median death week 14-20 (dossier)")
	quit(0)
