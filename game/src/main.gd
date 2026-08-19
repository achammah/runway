extends Node
## Scene flow: TITLE → SCRAMBLE (Act 0) → GRIND (garage weeks) → AUTOPSY → TITLE.
## Owns the run: seed, rng, state, content, run record, LLM client.

var content := ContentDb.new()
var llm: LlmClient
var generator: EventGenerator

var rng: SeededRng
var state: GameState
var record: RunRecord
var _screen: Node
var music: MusicManager

func _ready() -> void:
	content.load_all()
	llm = LlmClient.new()
	llm.setup(DotEnv.load_env())
	add_child(llm)
	generator = EventGenerator.new(llm)
	add_child(generator)
	music = MusicManager.new()
	add_child(music)
	print("RUNWAY! content: %d items, %d events · LLM: %s" % [
		content.items.size(), content.events.size(),
		(llm.provider + "/" + llm.model) if llm.enabled() else "off (authored only)"])
	_to_title()
	if OS.get_environment("RUNWAY_SHOT") != "":
		_autopilot()
	elif OS.get_environment("RUNWAY_FULLRUN") != "":
		_fullrun(OS.get_environment("RUNWAY_FULLRUN"))
	elif OS.get_environment("RUNWAY_LANEWIRE") != "":
		_shoot_lane_screens(OS.get_environment("RUNWAY_LANEWIRE"))
	elif OS.get_environment("RUNWAY_READING") != "":
		_shoot_reading_beat(OS.get_environment("RUNWAY_READING"))

## I2 — integration autopilot: plays a REAL run end-to-end through the actual
## screens (draft picks, weekly journal locks, era transitions, death/exit),
## screenshotting every few weeks. Proves the systems hold hands.
func _fullrun(dir: String) -> void:
	SaveSystem.clear_run()
	DirAccess.make_dir_recursive_absolute(dir)
	await get_tree().create_timer(1.2).timeout
	if _screen is TitleScreen:
		(_screen as TitleScreen).done.emit()
	await get_tree().create_timer(1.2).timeout
	if not (_screen is FounderDraftScreen):
		print("FULLRUN ABORT: no draft screen")
		get_tree().quit(1)
		return
	var d := _screen as FounderDraftScreen
	d._select(1)
	d._transition_to(1)
	await get_tree().create_timer(0.8).timeout
	d._transition_to(2)
	await get_tree().create_timer(0.8).timeout
	d._transition_to(3)
	await get_tree().create_timer(0.8).timeout
	d._cofounders.append({"role": 0, "commitment": 0, "equity": 30.0, "vesting": true, "fresh": true})
	d._refresh_capline()
	d._transition_to(4)
	await get_tree().create_timer(0.8).timeout
	var funds: Array = d.data.get("fundings", [])
	if funds.size() > 2 and d._fund_btns.size() > 2:
		d._pick_fund(funds[2], d._fund_btns[2])
	d._transition_to(5)
	await get_tree().create_timer(0.8).timeout
	for iid in ["itm_laptop", "itm_savings_jar", "itm_houseplant", "itm_guitar"]:
		if d._bag_btns.has(iid):
			d._toggle_bag(iid, 1, d._bag_btns[iid])
	await get_tree().create_timer(0.4).timeout
	d._do_launch()
	await get_tree().create_timer(1.6).timeout
	var week_cap := 95
	var shots := 0
	var eras_seen: Array[String] = ["garage"]
	var rooms_shot: Array[String] = []   # the room, tracked apart from the move beats
	while _screen is GarageViewScreen and week_cap > 0:
		week_cap -= 1
		var gv := _screen as GarageViewScreen
		if not gv._journal.visible:
			gv._open_journal()
			await get_tree().create_timer(0.4).timeout
		# walk the spreads like a player
		gv._page_i = 4
		gv._show_spread()
		await get_tree().create_timer(0.3).timeout
		if not gv._current_event.is_empty():
			for choice in gv._current_event.get("choices", []):
				if gv._choice_lock_reason(choice) == "":
					gv._pending_choice = choice
					break
		if state.week % 4 == 0 and state.cofounders.size() > 0:
			gv._pending_people[0] = "pay"
		gv._pending_work["PRODUCT"] = {"kind": "preset", "id": "sprint"}
		if state.week % 2 == 0:
			gv._pending_work["MARKETING"] = {"kind": "preset", "id": "post_log"}
			gv._pending_work["SALES"] = {"kind": "preset", "id": "demos"}
		gv._show_spread()
		await get_tree().create_timer(0.2).timeout
		if state.week % 5 == 0:
			await _shot(dir, "wk%02d_%s" % [state.week, state.era])
			shots += 1
		gv._lock_week()
		await get_tree().create_timer(1.4).timeout
		while _era_overlay != null and is_instance_valid(_era_overlay):
			await get_tree().create_timer(1.2).timeout
			var moved := String((_era_overlay as EraTransitionScreen).to_era)
			await _shot(dir, "move_%s_wk%02d" % [moved, state.week])
			shots += 1
			if not eras_seen.has(moved):
				eras_seen.append(moved)
			_era_overlay.queue_free()
			_era_overlay = null
			_pump_era_queue()   # a second move may be waiting behind this one
			await get_tree().create_timer(0.6).timeout
		# one capture of the room itself the first week each era is standing
		if not rooms_shot.has(state.era):
			rooms_shot.append(state.era)
			await get_tree().create_timer(0.5).timeout
			await _shot(dir, "era_%s_wk%02d" % [state.era, state.week])
			shots += 1
	await get_tree().create_timer(2.5).timeout
	await _shot(dir, "final_%s_wk%02d" % ["dead" if (state and state.dead) else "alive", state.week if state else 0])
	print("FULLRUN DONE: weeks=%d era=%s cash=%d dead=%s shots=%d eras=%s" % [
		state.week if state else -1, state.era if state else "?",
		state.cash if state else 0, str(state.dead if state else "?"), shots + 1,
		", ".join(eras_seen)])
	get_tree().quit()

## Screen harness for the wiring lane: renders THE MOVE and THE EXIT against a
## synthetic company so both beats can be reviewed without waiting for a run to
## reach hq. Never runs in a player build — it is env-gated.
## Renders THE READING BEAT against a real adjudication, so the screen that carries
## the whole wait can be reviewed without playing to it. Three shots: the moment it
## opens, mid-reveal, and fully revealed.
func _shoot_reading_beat(dir: String) -> void:
	DirAccess.make_dir_recursive_absolute(dir)
	await get_tree().create_timer(0.6).timeout
	var l := LoadingScreen.new()
	l.begin("WEEK 7")
	add_child(l)
	l.say("You said", "I stop building features and phone every single person who ever signed up, one by one, and just ask them what they actually need.")
	l.say("They heard", "You call the eight people who signed up and ask what would make voice-based tax help worth using.")
	l.say("", "At 9:12, you sit beside the savings jar with a legal pad and begin dialing. The first number goes to voicemail. The second belongs to Mara, who says she does not want a subscription box for taxes; she wants someone to tell her which envelope matters.")
	l.say("", "By lunch, five people have answered. Two want reminders, one wants a plain-language checklist, and another asks whether the voice can hear panic. Your tired Tech cofounder quietly removes a feature from the roadmap without making eye contact.")
	l.say("", "Nothing has gone viral. Nothing has been automated. But the product now has a problem small enough to solve, which is more than it had on Monday.")
	l.say("", "The eighth caller says they would pay if it stopped sounding like a tax podcast. This is, unfortunately, useful.")
	await get_tree().create_timer(1.2).timeout
	await _shot(dir, "read_01_opens")
	await get_tree().create_timer(7.0).timeout
	await _shot(dir, "read_02_midway")
	await get_tree().create_timer(14.0).timeout
	l.report(1.0)
	await get_tree().create_timer(2.0).timeout
	await _shot(dir, "read_03_full")
	print("READING BEAT DONE: 3 shots")
	get_tree().quit()

func _shoot_lane_screens(dir: String) -> void:
	DirAccess.make_dir_recursive_absolute(dir)
	await get_tree().create_timer(0.8).timeout
	var st := GameState.new()
	st.company_name = "Bytesy"
	st.founder_pct = 57.0
	st.morale = 72
	st.product = 74
	st.traction = 40
	st.hype = 66
	st.pivots = 2
	st.cash = 48000
	for spec in [
			{"from": "garage", "to": "coworking", "up": true, "week": 9,
				"reason": "something works and someone noticed", "tag": "move_up_coworking"},
			{"from": "coworking", "to": "office", "up": true, "week": 24,
				"reason": "launched, and the numbers kept moving", "tag": "move_up_office"},
			{"from": "office", "to": "coworking", "up": false, "week": 31,
				"reason": "payroll missed twice", "tag": "move_down_coworking"}]:
		var to_era := String(spec["to"])
		var from_era := String(spec["from"])
		var mv := EraTransitionScreen.new()
		mv.setup({
			"from": from_era, "to": to_era, "reason": String(spec["reason"]),
			"up": bool(spec["up"]), "week": int(spec["week"]),
			"new_rent": int(GameState.ERA_RENT.get(to_era, 0)),
			"old_rent": int(GameState.ERA_RENT.get(from_era, 0)),
			"new_cap": int(GameState.ERA_STAFF_CAP.get(to_era, 0)),
			"old_cap": int(GameState.ERA_STAFF_CAP.get(from_era, 0)),
			"era_label": String(GameState.ERA_NAMES.get(to_era, to_era)),
		})
		add_child(mv)
		mv.size = get_viewport().get_visible_rect().size
		await get_tree().create_timer(2.2).timeout
		await _shot(dir, String(spec["tag"]))
		mv.queue_free()
		await get_tree().create_timer(0.4).timeout
	st.era = "hq"
	st.week = 61
	st.exit_value = 42000000
	st.traction = 140
	for kind in ["acquisition", "ipo"]:
		if kind == "acquisition":
			st.set_flag("acquired_exit")
		var fin := FinaleScreen.new()
		fin.setup(st, kind)
		add_child(fin)
		fin.size = get_viewport().get_visible_rect().size
		await get_tree().create_timer(3.4).timeout
		await _shot(dir, "finale_" + kind)
		fin.queue_free()
		await get_tree().create_timer(0.4).timeout
	print("LANEWIRE SHOTS DONE")
	get_tree().quit()

## Dev screenshot autopilot: walks the screens and saves viewport captures,
## so the build can be reviewed without OS screen-recording permissions.
func _autopilot() -> void:
	var dir := OS.get_environment("RUNWAY_SHOT")
	DirAccess.make_dir_recursive_absolute(dir)
	await get_tree().create_timer(1.0).timeout
	await _shot(dir, "01_title")
	# gallery state (G on title)
	var gal := GalleryScreen.new()
	add_child(gal)
	(gal as Control).size = get_viewport().get_visible_rect().size
	await get_tree().create_timer(0.5).timeout
	await _shot(dir, "01b_gallery")
	gal.queue_free()
	await get_tree().create_timer(0.2).timeout
	if _screen is TitleScreen:
		(_screen as TitleScreen).done.emit()
	await get_tree().create_timer(1.4).timeout
	await _shot(dir, "02_select")
	if _screen is FounderDraftScreen:
		var d := _screen as FounderDraftScreen
		d._select(4)
		await get_tree().create_timer(0.9).timeout
		await _shot(dir, "03_select_consultant")
		d._transition_to(1)
		await get_tree().create_timer(1.0).timeout
		await _shot(dir, "04_name")
		d._transition_to(2)
		await get_tree().create_timer(1.0).timeout
		await _shot(dir, "04b_shape")
		d._transition_to(3)
		await get_tree().create_timer(1.0).timeout
		d._cofounders.append({"role": 0, "commitment": 0, "equity": 30.0, "vesting": true, "fresh": true})
		d._cofounders.append({"role": 3, "commitment": 1, "equity": 5.0, "vesting": false, "fresh": true})
		d._refresh_capline()
		await get_tree().create_timer(0.8).timeout
		await _shot(dir, "05_crew")
		d._open_recruit()
		await get_tree().create_timer(0.4).timeout
		await _shot(dir, "06_recruit")
		d._recruit_layer.visible = false
		d._transition_to(4)
		await get_tree().create_timer(1.0).timeout
		var funds: Array = d.data.get("fundings", [])
		if funds.size() > 2 and d._fund_btns.size() > 2:
			d._pick_fund(funds[2], d._fund_btns[2])
		await get_tree().create_timer(0.6).timeout
		await _shot(dir, "07_money")
		d._transition_to(5)
		await get_tree().create_timer(1.0).timeout
		for iid in ["itm_laptop", "itm_savings_jar", "itm_houseplant"]:
			if d._bag_btns.has(iid):
				d._toggle_bag(iid, 1, d._bag_btns[iid])
		await get_tree().create_timer(0.8).timeout
		await _shot(dir, "08_bag")
		d._do_launch()
		await get_tree().create_timer(1.6).timeout
		await _shot(dir, "10_garage")
		if _screen is GarageViewScreen:
			var gv := _screen as GarageViewScreen
			gv._open_journal()
			await get_tree().create_timer(0.6).timeout
			await _shot(dir, "09_journal")
			gv._page_i = 1
			gv._show_spread()
			await get_tree().create_timer(0.4).timeout
			await _shot(dir, "11_people")
			gv._page_i = 2
			gv._show_spread()
			await get_tree().create_timer(0.4).timeout
			await _shot(dir, "12_work")
			gv._page_i = 3
			gv._show_spread()
			await get_tree().create_timer(0.4).timeout
			await _shot(dir, "12b_situation")
			# force a real event so the event branches of situation/decision are photographed
			if gv._current_event.is_empty():
				var forced: Dictionary = content.events.get("evt_cofounder_pitch", {})
				if forced.is_empty() and not content.events.is_empty():
					forced = content.events.values()[0]
				gv._current_event = forced
				gv._show_spread()
				await get_tree().create_timer(0.4).timeout
				await _shot(dir, "12c_situation_event")
			gv._page_i = 4
			gv._show_spread()
			await get_tree().create_timer(0.4).timeout
			await _shot(dir, "13_decision")
			# a chosen option + a gesture + a work preset → the decision page in its "ready to lock" state
			if not gv._current_event.is_empty() and not gv._current_event.get("choices", []).is_empty():
				gv._pending_choice = gv._current_event["choices"][0]
			gv._pending_people[0] = "pay"
			gv._pending_work["PRODUCT"] = {"kind": "preset", "id": "sprint"}
			gv._show_spread()
			await get_tree().create_timer(0.4).timeout
			await _shot(dir, "13b_decision_ready")
			# pivot panel
			gv._open_pivot()
			await get_tree().create_timer(0.4).timeout
			await _shot(dir, "13c_pivot")
			for c in gv._journal.get_children():
				if c != gv._j_left and c != gv._j_right and c.get_index() >= 4:
					c.queue_free()
			# room states: item note + in-the-red vignette
			gv._close_journal()
			await get_tree().create_timer(0.3).timeout
			gv._item_note("itm_laptop", gv._spots.get("item_itm_laptop", gv._open_btn))
			await get_tree().create_timer(0.35).timeout
			await _shot(dir, "10b_room_item_note")
			state.cash = -300
			state.weeks_in_red = 2
			gv._sync_room()
			await get_tree().create_timer(0.5).timeout
			await _shot(dir, "10c_room_in_the_red")
			# consequences page with a real outcome recorded
			gv._last_outcome = {"title": "The Groomer Revolt", "verdict": "risky", "narration": "You herd nine suspicious groomers onto a call, own the identical-poodle incident, and buy time with free months.", "reality": "You do not, in fact, own a truck.", "log": ["cash -900", "product +8", "morale +3"]}
			gv._open_journal()
			gv._page_i = 0
			gv._show_spread()
			await get_tree().create_timer(0.5).timeout
			await _shot(dir, "09b_consequences_real")
			# force the end to photograph the last page
			state.morale = 1
			gv._lock_week()
			await get_tree().create_timer(3.6).timeout
			await _shot(dir, "14_autopsy")
	print("AUTOPILOT DONE")
	get_tree().quit()

func _shot(dir: String, name: String) -> void:
	await RenderingServer.frame_post_draw
	var img := get_viewport().get_texture().get_image()
	img.save_png("%s/%s.png" % [dir, name])

var _last_saved_week := -1
var _last_era := ""

func _process(_delta: float) -> void:
	if state != null and state.era != _last_era:
		if _last_era != "":
			if not daily_mode:
				generator.generate_arcs(state)   # era transition: evolve the arcs
			var up := GameState.ERAS.find(state.era) > GameState.ERAS.find(_last_era)
			_show_era_transition(_last_era, state.era, up)
		_last_era = state.era
	if daily_mode and generator != null and not generator.pool.is_empty():
		generator.pool.clear()   # authored-only: a generated card must never enter a daily run
	if _screen is GarageViewScreen and state != null and not state.dead:
		if state.week != _last_saved_week:
			_last_saved_week = state.week
			SaveSystem.save_run(state, record)
			_check_exit()
		if state.cash < 0:
			music.play("in_the_red")
			music.set_stem("")
		else:
			music.play("garage")
			if state.morale >= 75:
				music.set_stem("whistle")
			elif state.morale >= 55:
				music.set_stem("hum")
			else:
				music.set_stem("")

var _settings_open := false

var _gallery_open := false
var daily_mode := false

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and event.keycode == KEY_D and _screen is TitleScreen:
		daily_mode = true
		_start_run()
		return
	if event is InputEventKey and event.pressed and event.keycode == KEY_G and _screen is TitleScreen and not _gallery_open:
		_gallery_open = true
		var gal := GalleryScreen.new()
		gal.closed.connect(func(): _gallery_open = false)
		add_child(gal)
		(gal as Control).size = get_viewport().get_visible_rect().size
		return
	if event is InputEventKey and event.pressed and event.keycode == KEY_ESCAPE and not _settings_open:
		_settings_open = true
		var sset := SettingsScreen.new()
		sset.setup(llm.enabled(), (llm.provider + "/" + llm.model) if llm.enabled() else "")
		sset.closed.connect(func(): _settings_open = false)
		add_child(sset)
		if sset is Control:
			(sset as Control).size = get_viewport().get_visible_rect().size

var _era_overlay: Node = null
var _era_queue: Array = []

## THE MOVE — an overlay, not a screen swap: the run underneath keeps its state,
## the player just watches the new room arrive (or the boxes come out).
##
## Moves QUEUE. A company can outgrow two rooms in consecutive weeks, and dropping
## the second beat because the first is still on screen loses a move the player
## paid for. Each is captured with the era it describes, not whatever era the run
## has reached by the time it is shown.
func _show_era_transition(from_era: String, to_era: String, up: bool) -> void:
	if OS.get_environment("RUNWAY_SHOT") != "":
		return   # the state autopilot walks fixed screens; no surprise overlays
	var reason := ""
	for i in range(state.history.size() - 1, -1, -1):
		var e := String(state.history[i].get("entry", ""))
		if e.begins_with("MOVED"):
			var open_i := e.find("(")
			if open_i > 0:
				reason = e.substr(open_i + 1).rstrip(")")
			break
	_era_queue.append({
		"from": from_era, "to": to_era, "reason": reason, "up": up, "week": state.week,
		"new_rent": int(GameState.ERA_RENT.get(to_era, 0)),
		"old_rent": int(GameState.ERA_RENT.get(from_era, 0)),
		"new_cap": int(GameState.ERA_STAFF_CAP.get(to_era, 0)),
		"old_cap": int(GameState.ERA_STAFF_CAP.get(from_era, 0)),
		"era_label": String(GameState.ERA_NAMES.get(to_era, to_era)),
	})
	_pump_era_queue()

func _pump_era_queue() -> void:
	if _era_queue.is_empty() or (_era_overlay != null and is_instance_valid(_era_overlay)):
		return
	var scr := EraTransitionScreen.new()
	scr.setup(_era_queue.pop_front())
	scr.done.connect(func():
		_era_overlay = null
		_pump_era_queue())
	scr.z_index = 100   # a screen swapped in underneath must not bury the beat
	_era_overlay = scr
	add_child(scr)
	(scr as Control).size = get_viewport().get_visible_rect().size

func _swap(next: Node) -> void:
	if _screen and is_instance_valid(_screen):
		_screen.queue_free()
	_screen = next
	add_child(next)
	# Controls parented to a plain Node never get a layout pass; give them the
	# viewport size explicitly or full-rect-anchored children collapse to 0x0.
	if next is Control:
		(next as Control).size = get_viewport().get_visible_rect().size

func _to_title() -> void:
	music.play("title")
	music.set_stem("")
	var t := TitleScreen.new()
	t.done.connect(_start_run)
	_swap(t)

func _start_run() -> void:
	# one ongoing run at a time (60 Seconds! style): resume it if it exists;
	# death/exit clears it. Autopilot modes always start fresh.
	if OS.get_environment("RUNWAY_SHOT") == "" and OS.get_environment("RUNWAY_FULLRUN") == "" and SaveSystem.has_run():
		var loaded := SaveSystem.load_run()
		if not loaded.is_empty():
			state = loaded["state"]
			record = loaded["record"]
			rng = SeededRng.new(record.seed_value + state.week)
			generator.pool.clear()
			music.play("garage")
			var g0 := GarageViewScreen.new()
			g0.setup(state, content, rng, record, generator)
			g0.done.connect(_after_grind)
			_swap(g0)
			return
	var seed_value := int(Time.get_unix_time_from_system())
	if daily_mode:
		var dt := Time.get_date_dict_from_system()
		seed_value = int(dt["year"]) * 10000 + int(dt["month"]) * 100 + int(dt["day"])
	generator.disabled = daily_mode
	rng = SeededRng.new(seed_value)
	state = GameState.new()
	record = RunRecord.new()
	record.seed_value = seed_value
	generator.pool.clear()
	music.play("selection")
	var draft := FounderDraftScreen.new()
	draft.content_items = content.items.values()
	draft.done.connect(_after_draft)
	_swap(draft)

func _after_draft(result: Dictionary) -> void:
	var arch: Dictionary = result.get("archetype", {})
	var funding: Dictionary = result.get("funding", {})
	var cofounders: Array = result.get("cofounders", [])
	state.archetype_id = String(arch.get("id", ""))
	state.archetype_name = String(arch.get("name", "founder"))
	state.competences = (arch.get("stats", {}) as Dictionary).duplicate()
	state.company_name = String(result.get("company_name", "Untitled Inc"))
	state.company_idea = String(result.get("company_idea", ""))
	state.biz_what = String(result.get("biz_what", "Software"))
	state.biz_who = String(result.get("biz_who", "Consumer"))
	state.funding_id = String(funding.get("id", "bootstrap"))
	state.cofounders = cofounders.duplicate(true)
	state.structure_id = "solo" if cofounders.is_empty() else "team"
	# cap table: founding splits 100%, then investors dilute EVERYONE pro-rata
	var cf_equity := 0.0
	for cf in cofounders:
		cf_equity += float(cf.get("equity", 0))
	var dilution := 1.0 - float(funding.get("equity_cost", 0)) / 100.0
	for cf in state.cofounders:
		cf["equity_diluted"] = float(cf.get("equity", 0)) * dilution
	state.founder_pct = (100.0 - cf_equity) * dilution
	state.cash += int(arch.get("start_cash_bonus", 0)) + int(funding.get("cash", 0))
	# competence coverage: full-time roles patch stats, part-time patches half
	for cf in cofounders:
		var full := String(cf.get("commitment", "")) == "Full-time"
		match String(cf.get("role", "")):
			"Technical":
				state.competences["build"] = maxi(int(state.competences["build"]), 4 if full else 3)
			"Business":
				state.competences["sell"] = maxi(int(state.competences["sell"]), 4 if full else 3)
				state.competences["raise"] = maxi(int(state.competences["raise"]), 3)
			"Design":
				state.competences["build"] = mini(5, int(state.competences["build"]) + 1)
				state.competences["sell"] = maxi(int(state.competences["sell"]), 3)
			"The Idea Friend":
				pass   # that's the joke
	if not cofounders.is_empty():
		state.flags.append("has_cofounder")
	# the traps chosen at the draft become the flags that spawn consequences
	for t in result.get("traps", []):
		if String(t) != "solo" and not state.flags.has(String(t)):
			state.flags.append(String(t))
	# the bag
	for id in result.get("items", []):
		state.items.append(String(id))
		state.cash += int(content.items.get(id, {}).get("cash_value", 0))
	if state.cash <= 0:
		state.cash = 1500   # emergency couch cushions
	record.log_event(0, {"id": "draft", "title": "The Founding of %s" % state.company_name},
		"%s · %d cofounder(s) · %s · kept %.0f%%" % [state.archetype_name, cofounders.size(), funding.get("name", ""), state.founder_pct], [])
	generator.generate_arcs(state)   # Tier-3 run director: the run's narrative arcs
	music.play("garage")
	var g := GarageViewScreen.new()
	g.setup(state, content, rng, record, generator)
	g.done.connect(_after_grind)
	_swap(g)

func _after_grind(result: Dictionary) -> void:
	# money endings earn the ceremony first; ash goes straight to the last page
	var exit_kind := ""
	if state != null and state.has_flag("acquired_exit"):
		exit_kind = "acquisition"
	elif result.has("victory") and state != null and state.era == "hq":
		exit_kind = "ipo"
	# ACT BREAK — shipping an MVP is a chapter, not an ending. Below the top
	# floor the company keeps trading: a fresh week opens instead.
	if exit_kind == "" and result.has("victory") and state != null and not state.dead:
		_next_chapter()
		return
	if exit_kind != "" and OS.get_environment("RUNWAY_SHOT") == "":
		music.play("title")
		music.set_stem("")
		var fin := FinaleScreen.new()
		fin.setup(state, exit_kind)
		fin.done.connect(func(): _to_autopsy(result, exit_kind))
		_swap(fin)
		return
	_to_autopsy(result, exit_kind)

## The garage screen raises its Act-1 gate whenever the meters stay high, so this
## re-enters the grind on a fresh week rather than ending the run. A new screen
## runs _start_week() itself, so the week rolls with its payroll and people pass
## intact. The era beat is driven by _process on the era change, not from here.
func _next_chapter() -> void:
	if not state.has_flag("act1_cleared"):
		state.flags.append("act1_cleared")
		record.log_event(state.week, {"id": "act1", "title": "Act One closed"},
			"MVP shipped, users landed — the company keeps trading", [])
	music.play("garage")
	var g := GarageViewScreen.new()
	g.setup(state, content, rng, record, generator)
	g.done.connect(_after_grind)
	_swap(g)

## A RUN MUST BE ABLE TO END IN SUCCESS.
## The IPO was gated on the Act-1 victory signal, which stops firing once Act One
## is cleared — so a company that reached the top floor could never end at all.
## The owner played to WEEK 69 at HQ with $4M and the game simply kept going.
## Success is now checked every week the run advances, and no run can outlive the
## cap. Called from _process on a week change, so it does not depend on any
## screen remembering to raise a signal.
const RUN_WEEK_CAP := 78

func _check_exit() -> void:
	if state == null or state.dead or _era_overlay != null:
		return
	if state.has_flag("exit_taken"):
		return
	var reason := ""
	if state.era == "hq" and state.valuation() >= 25_000_000 and state.traction >= 70:
		reason = "ipo"                     # the company is genuinely public-ready
	elif state.week >= RUN_WEEK_CAP:
		# no run runs forever. Whatever it has built by now IS the ending.
		reason = "ipo" if (state.era == "hq" and state.cash > 0) else "timeout"
	if reason == "":
		return
	state.flags.append("exit_taken")
	if reason == "timeout":
		_to_autopsy({"death": "THE LONG HAUL — %d weeks in, the story ran out before the money did." % state.week}, "")
		return
	if OS.get_environment("RUNWAY_SHOT") == "":
		music.play("title")
		music.set_stem("")
		var fin := FinaleScreen.new()
		fin.setup(state, "ipo")
		fin.done.connect(func(): _to_autopsy({"victory": true}, "ipo"))
		_swap(fin)
	else:
		_to_autopsy({"victory": true}, "ipo")

func _to_autopsy(result: Dictionary, exit_kind: String = "") -> void:
	var headline: String
	if result.has("death"):
		headline = String(result["death"])
	elif exit_kind == "acquisition":
		headline = "SOLD THE COMPANY — you shook the hand in week %d." % state.week
	elif exit_kind == "ipo":
		headline = "RANG THE BELL — %s went public in week %d." % [state.company_name, state.week]
	else:
		headline = "SURVIVED: MVP shipped, first users on board. (Act 1 gate — more acts coming.)"
	headline += "\nYour slice today: $%s  (%.0f%% of the company)" % [str(state.payout_today()), state.founder_pct]
	SaveSystem.record_run_end(state, ("[DAILY] " if daily_mode else "") + headline.split("\n")[0])
	daily_mode = false
	music.play("last_page")
	music.set_stem("")
	var a := AutopsyScreen.new()
	a.setup(headline, record, state)
	a.done.connect(_to_title)
	_swap(a)
