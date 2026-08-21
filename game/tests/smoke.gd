extends SceneTree
## Headless smoke test: simulates full grind runs through the real sim core
## (content db, eligibility, effect ops, timebombs, death/victory) with no UI.
## Run: godot --headless --path . --script tests/smoke.gd

func _init() -> void:
	var content := ContentDb.new()
	content.load_all()
	assert(content.items.size() == 29, "expected 29 items (20 core + 9 trade-specific)")
	assert(content.events.size() >= 8, "expected authored events")

	var deaths := 0
	var victories := 0
	var stalls := 0
	for trial in 30:
		var rng := SeededRng.new(1000 + trial)
		var state := GameState.new()
		# random scramble outcome: bank 3-6 random items
		var all_ids := content.items.keys()
		for i in rng.randi_range(3, 6):
			var id: String = rng.pick(all_ids)
			if not state.items.has(id):
				state.items.append(id)
				state.cash += int(content.items[id].get("cash_value", 0))
		if state.cash == 0:
			state.cash = 1500
		# grind loop
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
			"death": deaths += 1
			"victory": victories += 1
			_: stalls += 1
	print("30 simulated runs → deaths: %d · victories: %d · stalls: %d" % [deaths, victories, stalls])
	assert(deaths + victories > 0, "sim must resolve runs")

	# clamp check: an outrageous LLM-style effect must be clamped
	var s2 := GameState.new()
	s2.cash = 1000
	EffectOps.apply({"op": "cash_delta", "v": 999999}, s2)
	assert(s2.cash == 6000, "cash_delta must clamp to +5000, got %d" % s2.cash)
	EffectOps.apply({"op": "morale_delta", "v": -500}, s2)
	assert(s2.morale == 40, "morale_delta must clamp to -20")
	var bad := EventGenerator.new(LlmClient.new())._validate_card({"title": "x", "body": "y", "choices": [{"label": "a", "effects": [{"op": "delete_save_file", "v": 1}]}, {"label": "b", "effects": [{"op": "cash_delta", "v": 5}]}]})
	assert(bad == false, "validator must reject non-whitelisted ops")
	print("clamps + validator OK")

	# ── dilution math: pro-rata for everyone ──
	var s3 := GameState.new()
	s3.founder_pct = 60.0
	s3.cofounders = [{"role": "cto", "equity": 40.0}]
	s3.dilute_all(20.0)
	assert(absf(s3.founder_pct - 48.0) < 0.01, "founder must dilute 60→48")
	assert(absf(float(s3.cofounders[0]["equity"]) - 32.0) < 0.01, "cofounder must dilute 40→32")
	assert(s3.has_flag("lost_majority"), "sub-50%% must arm lost_majority")
	EffectOps.apply({"op": "dilute_pct", "v": 90}, s3)  # clamps to 35
	assert(s3.founder_pct > 25.0, "dilute_pct must clamp to 35%%")

	# ── staff caps + hire/fire + burnout ladder ──
	var s4 := GameState.new()
	assert(s4.staff_cap() == 2, "garage cap is 2")
	EffectOps.apply({"op": "hire", "name": "Ada", "role": "eng", "salary": 900}, s4)
	EffectOps.apply({"op": "hire", "name": "Lin", "role": "sales", "salary": 800}, s4)
	EffectOps.apply({"op": "hire", "name": "Sam", "role": "ops", "salary": 700}, s4)
	assert(s4.employees.size() == 2, "cap must refuse the 3rd garage hire")
	assert(s4.burn_per_week() == 150 + 500 + 1700, "salaries must land in burn")
	s4.morale = 20
	var quit_seen := false
	for i in 30:
		s4.weekly_staff_tick()
		if s4.employees.is_empty():
			quit_seen = true
			break
	assert(quit_seen, "burnout ladder must end in gone (quit)")
	assert(s4.has_flag("staff_cooked") and s4.has_flag("staff_quit"), "ladder flags must arm")
	EffectOps.apply({"op": "fire_role", "v": "eng"}, s4)  # firing an empty roster is a no-op

	# ── era ladder up + down ──
	var s5 := GameState.new()
	s5.product = 65
	s5.traction = 6
	var up := s5.advance_era_if_ready()
	assert(up["changed"] and s5.era == "coworking", "garage→coworking gate")
	assert(s5.burn_per_week() >= 600 + 500, "coworking rent must apply")
	s5.set_flag("launched")
	s5.traction = 30
	up = s5.advance_era_if_ready()
	assert(up["changed"] and s5.era == "office", "coworking→office gate")
	s5.set_flag("pmf")
	s5.set_flag("seed_raised")
	s5.cash = 1000
	assert(not s5.advance_era_if_ready()["changed"], "office→floor must demand a cash cushion")
	s5.cash = 6 * int(GameState.ERA_RENT["floor"])
	assert(s5.advance_era_if_ready()["changed"] and s5.era == "floor", "office→floor gate")
	s5.set_flag("series_a")
	s5.traction = 120
	s5.cash = 6 * int(GameState.ERA_RENT["hq"])
	assert(s5.advance_era_if_ready()["changed"] and s5.era == "hq", "floor→hq gate")
	assert(not s5.advance_era_if_ready()["changed"], "hq is the top")
	var down := s5.demote("down round")
	assert(down["changed"] and s5.era == "floor", "demotion must step one era down")

	# ── valuation monotonicity + acquisition ──
	var lo := GameState.new()
	var hi := GameState.new()
	hi.era = "office"
	hi.product = 50
	hi.traction = 40
	assert(hi.valuation() > lo.valuation(), "valuation must grow with era+meters")
	var prev := 0
	for e_name in GameState.ERAS:
		var sv := GameState.new()
		sv.era = e_name
		assert(sv.valuation() > prev, "valuation monotonic across eras")
		prev = sv.valuation()
	var s6 := GameState.new()
	s6.era = "office"
	s6.product = 40
	s6.traction = 30
	EffectOps.apply({"op": "accept_acquisition", "v": 0.6}, s6)
	assert(s6.exit_value > 0 and s6.has_flag("acquired_exit"), "acquisition must bank an exit")
	assert(s6.payout_today() == int(s6.exit_value * s6.founder_pct / 100.0), "payout uses the banked exit")

	# ── content integrity: every referenced event/item exists, ops whitelisted ──
	var wl := EffectOps.op_whitelist()
	var evt_count := 0
	for ev in content.events.values():
		evt_count += 1
		for ch in ev.get("choices", []):
			for ef in ch.get("effects", []):
				var opn := String(ef.get("op", ""))
				assert(wl.has(opn), "event %s uses non-whitelisted op %s" % [ev.get("id"), opn])
				if opn == "arm_timebomb":
					assert(content.events.has(String(ef.get("event", ""))),
						"timebomb in %s points at missing event %s" % [ev.get("id"), ef.get("event")])
				if opn == "weight_future":
					assert(content.events.has(String(ef.get("v", ""))),
						"weight_future in %s points at missing event" % ev.get("id"))
				if opn in ["grant_item", "destroy_item"]:
					assert(content.items.has(String(ef.get("v", ""))),
						"item op in %s points at missing item %s" % [ev.get("id"), ef.get("v")])
		for need_key in ["items_any"]:
			for id in ev.get("requires", {}).get(need_key, []):
				assert(content.items.has(id), "requires in %s names missing item %s" % [ev.get("id"), id])
	print("systems OK — %d events, dilution, staff, eras, valuation all hold" % evt_count)

	# ── Tier-3 run director: zero-impact fallback, validation, prompt injection ──
	var gen := EventGenerator.new(LlmClient.new())
	var s7 := GameState.new()
	gen.generate_arcs(s7)  # no key → must be a no-op
	assert(s7.arcs.is_empty(), "no key must mean no arcs")
	assert(not gen.compose_event_user(s7).contains("NARRATIVE DIRECTIVES"), "no arc block without arcs")
	var fixture := {"arcs": [
		{"arc_id": "arc_rival", "kind": "rival", "premise": "A rival undercuts every move.",
		"actors": ["Grindstone Labs", "Petra Voss"], "escalation_rule": "intensify each era",
		"beats": [
			{"era": "garage", "directive": "Grindstone Labs demos a suspiciously similar prototype at the meetup."},
			{"era": "office", "directive": "Petra Voss poaches your first enterprise lead."}]}]}
	var stored := gen._ingest_arcs(s7, fixture)
	assert(stored.size() == 1 and s7.arcs.size() == 1, "valid arcs must store on state")
	var u := gen.compose_event_user(s7)
	assert(u.contains("Grindstone Labs demos"), "garage beat must inject into the Tier-2 prompt")
	assert(not u.contains("poaches your first enterprise lead"), "office beat must NOT inject in garage era")
	assert(gen.compose_adjudicate_user(s7, {"title": "T", "body": "B"}, "I do a thing").contains("Grindstone Labs demos"),
		"directive must inject into the adjudicator message")
	s7.era = "office"
	assert(gen.compose_event_user(s7).contains("poaches your first enterprise lead"),
		"era transition must switch the active beat")
	assert(gen._validate_arcs({"arcs": [{"arc_id": "x", "kind": "kaiju", "premise": "p", "actors": ["A"],
		"beats": [{"era": "garage", "directive": "d"}], "escalation_rule": "r"}]}).is_empty(), "bad kind must reject")
	assert(gen._validate_arcs({"arcs": [{"arc_id": "x", "kind": "rival", "premise": "p", "actors": ["A"],
		"beats": [{"era": "moonbase", "directive": "d"}], "escalation_rule": "r"}]}).is_empty(), "bad era must reject")
	print("run director OK")

	# EVERY SCRIPT MUST ACTUALLY PARSE.
	# This suite exercised the core systems and never loaded a screen, so it printed
	# SMOKE PASS while garage_view_screen.gd had four parse errors and the game
	# crashed on launch the moment the draft finished. A gate that is green while the
	# game is unplayable is worse than no gate. Walk src/ and load everything.
	var broken: Array = []
	var checked := 0
	var stack: Array = ["res://src"]
	while not stack.is_empty():
		var d: String = stack.pop_back()
		var da := DirAccess.open(d)
		if da == null:
			continue
		da.list_dir_begin()
		var f := da.get_next()
		while f != "":
			if da.current_is_dir():
				if not f.begins_with("."):
					stack.append(d + "/" + f)
			elif f.ends_with(".gd"):
				var path := d + "/" + f
				checked += 1
				# load() hands back a GDScript object even when the file failed to
				# parse, so a null check silently passes broken scripts. reload() is
				# accurate but FAILS on any script this suite already instantiated,
				# which flagged four healthy core scripts. get_instance_base_type()
				# is empty only when the parse actually failed, and it disturbs
				# nothing that is already live.
				# THREE CHECKS, because each one alone lets a real break through.
				# load() returns a GDScript object even when the parse failed, so a null
				# check passes broken files. get_instance_base_type() goes empty for an
				# unregistered script, but a script with a class_name that is ALREADY in
				# the global registry keeps its cached base type even when it stops
				# compiling — that is how one untyped max() took the whole game down to a
				# blank screen while this gate printed SMOKE PASS. can_instantiate() is
				# the one that is false the moment the script no longer compiles.
				var sc = load(path)
				if sc == null:
					broken.append(path)
				elif sc is GDScript:
					var g := sc as GDScript
					if String(g.get_instance_base_type()) == "" or not g.can_instantiate():
						broken.append(path)
			f = da.get_next()
		da.list_dir_end()
	if not broken.is_empty():
		print("SMOKE FAIL — %d of %d scripts do not parse:" % [broken.size(), checked])
		for b in broken:
			print("   ", b)
		quit(1)
		return          # quit() is deferred; without this the suite prints SMOKE PASS anyway
	print("scripts OK — %d parsed" % checked)

	print("SMOKE PASS")
	quit(0)
