extends Node
## Scene flow: TITLE → SCRAMBLE (Act 0) → GRIND (garage weeks) → AUTOPSY → TITLE.
## Owns the run: seed, rng, state, content, run record, LLM client — and THE TURN:
## the week locks, the DM answers with the consequences AND the scene to build, the
## art starts immediately, and the player reads the consequences while it renders.
## See "THE GENERATIVE WEEK" below and docs/GENERATIVE_ARCHITECTURE.md.

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
	_setup_director()
	print("RUNWAY! content: %d items, %d events · LLM: %s" % [
		content.items.size(), content.events.size(),
		(llm.provider + "/" + llm.model) if llm.enabled() else "off (authored only)"])
	_to_title()
	if OS.get_environment("RUNWAY_SHOT") != "":
		_autopilot()
	elif OS.get_environment("RUNWAY_FULLRUN") != "":
		_fullrun(OS.get_environment("RUNWAY_FULLRUN"))
	elif OS.get_environment("RUNWAY_FINALE_SHOT") != "":
		_finale_probe(OS.get_environment("RUNWAY_FINALE_SHOT"))
	elif OS.get_environment("RUNWAY_LANEWIRE") != "":
		_shoot_lane_screens(OS.get_environment("RUNWAY_LANEWIRE"))
	elif OS.get_environment("RUNWAY_READING") != "":
		_shoot_reading_beat(OS.get_environment("RUNWAY_READING"))
	elif OS.get_environment("RUNWAY_TURN") != "":
		_shoot_turn(OS.get_environment("RUNWAY_TURN"))

## I2 — integration autopilot: plays a REAL run end-to-end through the actual
## screens (draft picks, weekly journal locks, era transitions, death/exit),
## screenshotting every few weeks. Proves the systems hold hands.
## Deterministic endgame check: force a public-ready company at HQ, let the real
## _check_exit fire, and photograph the IPO ceremony and the last page — the one
## path long soaks only hit by luck.
func _finale_probe(dir: String) -> void:
	DirAccess.make_dir_recursive_absolute(dir)
	await get_tree().create_timer(1.0).timeout
	state = GameState.new()
	record = RunRecord.new()
	record.seed_value = 7
	rng = SeededRng.new(7)
	state.company_name = "Blobsworth"
	state.company_idea = "compliance software with feelings"
	state.era = "hq"
	state.week = 60
	state.cash = 2_000_000
	state.traction = 120
	state.product = 95
	state.morale = 70
	state.hype = 60
	state.founder_pct = 41.0
	var g := GarageViewScreen.new()
	g.setup(state, content, rng, record, generator)
	g.done.connect(_after_grind)
	_swap(g)
	await get_tree().create_timer(1.0).timeout
	# _check_exit runs from _process; give it a few frames, then photograph
	var cap := 20
	while not (_screen is FinaleScreen) and cap > 0:
		cap -= 1
		await get_tree().create_timer(0.3).timeout
	await get_tree().create_timer(1.4).timeout
	await _shot(dir, "finale_ipo")
	if _screen is FinaleScreen:
		(_screen as FinaleScreen).done.emit()
	await get_tree().create_timer(1.6).timeout
	await _shot(dir, "last_page_ipo")
	print("FINALE PROBE DONE: screen=%s" % (_screen.get_class() if _screen != null else "none"))
	get_tree().quit()

func _fullrun(dir: String) -> void:
	# BUG-15: no clear_run() here. A test run must not delete the game the owner has
	# in progress — and it does not need to, because _start_run always starts fresh
	# under a harness env var.
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
	if OS.get_environment("RUNWAY_WEEK_CAP") != "":
		week_cap = maxi(int(OS.get_environment("RUNWAY_WEEK_CAP")), 1)
	var shots := 0
	var eras_seen: Array[String] = ["garage"]
	var rooms_shot: Array[String] = []   # the room, tracked apart from the move beats
	while _screen is GarageViewScreen and week_cap > 0:
		week_cap -= 1
		print("FULLRUN week %d · busy=%s beat=%s curtain=%s" % [state.week, str(_turn_busy),
				str(_beat != null and is_instance_valid(_beat)),
				str(_curtain != null and is_instance_valid(_curtain) and _curtain.visible)])
		var gv := _screen as GarageViewScreen
		if not gv._journal.visible:
			gv._open_journal()
			await get_tree().create_timer(0.4).timeout
		# THE RUN CAN END ON ANY WEEK TICK. _check_exit swaps the finale in from
		# _process, which frees this screen between two awaits — and the harness then
		# wrote into a freed node and died there, leaving the run with no last page.
		# Every await inside this loop is followed by this check.
		if not _still_playing(gv):
			await get_tree().create_timer(0.5).timeout
			continue   # act break swapped the screen, or the run ended — the loop
					   # condition decides which, and neither may be written to
		# walk the TWO spreads like a player: read the week, then write the move
		gv._page_i = 1
		gv._show_spread()
		await get_tree().create_timer(0.3).timeout
		if not _still_playing(gv):
			await get_tree().create_timer(0.5).timeout
			continue   # act break swapped the screen, or the run ended — the loop
					   # condition decides which, and neither may be written to
		var moves := ["Head down and sprint on the product all week.",
			"Get out of the building: demo to ten real customers and close one paying.",
			"Spend the week on money: chase invoices and warm up an angel."]
		var mv: String = moves[state.week % moves.size()]
		if state.product >= 55 and not state.has_flag("launched"):
			mv = "Ship it: public launch this week — post everywhere, email every signup."
		elif state.has_flag("launched") and state.traction >= 20 and not state.has_flag("seed_raised"):
			mv = "Run the seed round to a close: line up the angels, set a deadline, sign."
		elif state.era == "office" and not state.has_flag("pmf") and state.traction >= 40:
			mv = "Obsess over the keenest customers all week: interviews, retention fixes, referral asks — prove people would riot if we vanished."
		elif state.era == "floor" and not state.has_flag("series_a") and state.traction >= 90:
			mv = "Take the Series A to term sheets: three partners, one deadline, close it."
		gv._free_text[1] = mv
		var fld: TextEdit = gv._jp.input_field()
		if fld != null:
			fld.text = String(gv._free_text[1])
		if state.week % 5 == 0:
			await _shot(dir, "wk%02d_%s" % [state.week, state.era])
			shots += 1
			if not _still_playing(gv):
				continue
		gv._commit_from_text()
		if OS.get_environment("RUNWAY_CURTAIN_FILM") != "" and state.week <= 2:
			for ci in 6:
				await get_tree().create_timer([0.1, 0.2, 0.25, 0.6, 1.9, 5.5][ci]).timeout
				await _shot(dir, "curtain_%02d_wk%02d_%d" % [state.week, state.week, ci])
		# a live adjudication takes seconds; keyless answers instantly — wait it
		# out, and NEVER touch the screen again without checking it still exists:
		# a lock can trigger an era move that frees gv mid-iteration, and one
		# freed-instance error kills this whole coroutine silently (the exact way
		# two soaks hung at the coworking transition with no line in the log).
		var adj_cap := 40
		while adj_cap > 0 and is_instance_valid(gv) and bool(gv.get("_adjudicating")):
			adj_cap -= 1
			await get_tree().create_timer(0.5).timeout
		# READ THE BEAT LIKE A PLAYER: wait for it to open, let it breathe, then
		# click "look up". Skipping this stacked unread beats and swallowed weeks.
		var beat_cap := 30
		while beat_cap > 0 and (_beat == null or not is_instance_valid(_beat)):
			beat_cap -= 1
			await get_tree().create_timer(0.5).timeout
		var read_cap := 240
		while read_cap > 0 and _beat != null and is_instance_valid(_beat):
			read_cap -= 1
			_beat.set("_proceed", true)
			await get_tree().create_timer(0.5).timeout
		await get_tree().create_timer(1.2).timeout
		if not _still_playing(gv):
			await get_tree().create_timer(0.5).timeout
			continue
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
		if _still_playing(gv) and not rooms_shot.has(state.era):
			rooms_shot.append(state.era)
			# 2.4s, not 0.5: a plain week reaches here mid dread-scrim (its fade-out
			# ends ~2.25s in), which photographed era_garage at luminance 50 against
			# ~190 for post-move weeks. The scrim is a design beat, so the harness
			# waits it out rather than the beat being shortened.
			await get_tree().create_timer(2.4).timeout
			if not _still_playing(gv):
				continue
			await _shot(dir, "era_%s_wk%02d" % [state.era, state.week])
			shots += 1
	await get_tree().create_timer(2.5).timeout
	await _shot(dir, "final_%s_wk%02d" % ["dead" if (state and state.dead) else "alive", state.week if state else 0])
	# a paid render may still be in flight: wait it out and photograph the proof,
	# otherwise the harness quits with the evidence half-downloaded
	if OS.get_environment("RUNWAY_TURN_ART") != "":
		var art_cap := 240
		while _turn_busy and art_cap > 0:
			art_cap -= 1
			await get_tree().create_timer(1.0).timeout
		if _scene_path != "":
			if not (_scene_layer != null and is_instance_valid(_scene_layer)):
				_open_scene(_scene_path, _scene_headline)
			await get_tree().create_timer(1.0).timeout
			await _shot(dir, "v2_generated_scene")
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

## Harness for THE TURN — the whole generative week against a canned DM verdict, so
## the beat, the room opening and the dead-network path can all be reviewed without
## playing to week 7 and without spending a render. Two weeks are played: one where
## the art lands, one where it dies. RUNWAY_TURN_ART=1 makes the first one REAL
## (a live compose: one to three minutes, and it costs money).
func _shoot_turn(dir: String) -> void:
	DirAccess.make_dir_recursive_absolute(dir)
	await get_tree().create_timer(0.6).timeout
	state = GameState.new()
	state.company_name = "Bytesy"
	state.week = 7
	state.archetype_id = "hacker"
	state.archetype_name = "The Hacker"
	state.morale = 58
	# a real cofounder on the cap table, and the DM below also asks for a sales one
	# this company never hired — the invented person must not reach the canvas
	state.cofounders = [{"role": "Technical", "commitment": "Full-time", "equity": 30.0,
		"vesting": true, "loyalty": 24}]
	record = RunRecord.new()
	record.seed_value = 424242
	var dm := {
		"player_text": "I stop building features and phone every single person who ever signed up, one by one, and just ask them what they actually need.",
		"interpreted_as": "You call the eight people who signed up and ask what would make voice-based tax help worth using.",
		"narration": "At 9:12 you sit beside the savings jar with a legal pad and begin dialing. The first number goes to voicemail. The second belongs to Mara, who says she does not want a subscription box for taxes; she wants someone to tell her which envelope matters.\n\nBy lunch five people have answered. Two want reminders, one wants a plain-language checklist, and another asks whether the voice can hear panic. Your tired Tech cofounder quietly removes a feature from the roadmap without making eye contact.\n\nNothing has gone viral. Nothing has been automated. But the product now has a problem small enough to solve, which is more than it had on Monday.",
		"reality_check": "The eighth caller says they would pay if it stopped sounding like a tax podcast. This is, unfortunately, useful.",
		"verdict": "fine",
		"headline": "EIGHT PHONE CALLS AND ONE USEFUL SENTENCE",
		"scene": {
			"family": "scrappy_workspace", "place": "garage desk", "time": "night",
			"condition": "steady", "framing": "wide",
			"novel_place": "a two-car garage converted into an office, a folding table under a work lamp, a savings jar and a legal pad beside a cooling mug",
			"beat": "the founder works the phone while the tech cofounder quietly deletes a feature",
		},
		"cast": [
			{"who": "founder", "mood": "fine", "doing": "on the phone with a legal pad, tallying answers"},
			{"who": "tech", "mood": "burnt", "doing": "removing a feature from the roadmap without eye contact"},
			{"who": "sales", "mood": "fine", "doing": "working a list this company has never had anyone to work"},
		],
	}
	var live := OS.get_environment("RUNWAY_TURN_ART") != ""
	var pack := _cast_pack(dm["cast"])
	print("TURN HARNESS: %d of %d cast members have a fetchable sprite · art=%s" % [
		(pack["urls"] as Array).size(), (dm["cast"] as Array).size(), "LIVE" if live else "stub"])
	# ── week 7: the art lands, the room opens ──
	_begin_turn(dm, "" if live else "res://docs/refs/pilot_composed_hangar.png")
	await get_tree().create_timer(1.2).timeout
	await _shot(dir, "turn_01_beat_opens")
	await get_tree().create_timer(9.0).timeout
	await _shot(dir, "turn_02_beat_reading")
	var waited := 0.0
	var cap := 300.0 if live else 70.0
	while (_turn_busy or (live and not _scene_done)) and waited < cap:
		await get_tree().create_timer(1.0).timeout
		waited += 1.0
	# a live render can outlast the beat's ceiling, and out here there is no room to
	# come back to, so the harness opens whatever landed in order to photograph it
	if _scene_path != "" and not (_scene_layer != null and is_instance_valid(_scene_layer)):
		_open_scene(_scene_path, String(dm["headline"]))
	await get_tree().create_timer(1.0).timeout
	await _shot(dir, "turn_03_room_opens")
	var opened := _scene_layer != null and is_instance_valid(_scene_layer)
	_close_scene()
	await get_tree().create_timer(0.8).timeout
	# ── week 8: the render dies, and the week must carry on regardless ──
	state.week = 8
	_begin_turn(dm, "FAIL")
	await get_tree().create_timer(4.0).timeout
	await _shot(dir, "turn_04_reading_after_a_dead_render")
	waited = 0.0
	while _turn_busy and waited < 70.0:
		await get_tree().create_timer(1.0).timeout
		waited += 1.0
	await get_tree().create_timer(0.8).timeout
	# ── week 9: no art at all (no key, or art switched off). The week is still read ──
	var text_only := false
	if not live:
		state.week = 9
		_begin_turn(dm)
		await get_tree().create_timer(2.0).timeout
		text_only = _beat != null and is_instance_valid(_beat) and _beat.visible
		await _shot(dir, "turn_05_reading_with_no_art_at_all")
		waited = 0.0
		while _turn_busy and waited < 70.0:
			await get_tree().create_timer(1.0).timeout
			waited += 1.0
	var stranded := _scene_layer != null and is_instance_valid(_scene_layer)
	print("TURN HARNESS DONE: room_opened=%s · failure_left_the_room_alone=%s · text_only_beat=%s · turn_busy=%s" % [
		str(opened), str(not stranded), str(text_only), str(_turn_busy)])
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
		# INDEX, NOT COUNT. `_select` wraps with wrapi(i, 0, _archs.size()) and exactly
		# four archetypes ship, so 4 silently lands back on 0 — and this shot spent a
		# whole QA pass calling a photograph of the hacker `03_select_consultant`.
		# The consultant is the LAST of the four: index 3.
		d._select(3)
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
			# THE TWO-SPREAD WALK: quiet decision page, event decision page, a
			# written move (ready), and the considering state — the real states.
			gv._page_i = 1
			gv._show_spread()
			await get_tree().create_timer(0.4).timeout
			await _shot(dir, "11_decision_quiet")
			if gv._current_event.is_empty():
				var forced: Dictionary = content.events.get("evt_cofounder_pitch", {})
				if forced.is_empty() and not content.events.is_empty():
					forced = content.events.values()[0]
				gv._current_event = forced
			gv._show_spread()
			await get_tree().create_timer(0.4).timeout
			await _shot(dir, "12_decision_event")
			var fld2: TextEdit = gv._jp.input_field()
			if fld2 != null:
				fld2.text = "Call all nine groomers and own the poodle incident personally."
				gv._free_text[1] = fld2.text
			gv._lock_button()
			await get_tree().create_timer(0.3).timeout
			await _shot(dir, "13_decision_written")
			gv._adjudicating = true
			gv._lock_button()
			await get_tree().create_timer(0.3).timeout
			await _shot(dir, "13b_decision_considering")
			gv._adjudicating = false
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

## Is the screen the harness is holding still the screen the game is showing? A run
## that ends mid-week frees it, and a freed node must never be written to.
## The argument is deliberately UNTYPED: a freed instance cannot be passed to a
## typed Node parameter at all — the call itself is the error, before the body runs.
func _still_playing(gv) -> bool:
	return is_instance_valid(gv) and _screen == gv and state != null and not state.dead

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
	if _upload_thread != null and _upload_done:
		_collect_upload()   # a room hosted too late for its own week is free for the next
	if _screen is GarageViewScreen and state != null and not state.dead:
		_poll_turn(_screen as GarageViewScreen)
		if state.week != _last_saved_week:
			_last_saved_week = state.week
			if not _harness():
				SaveSystem.save_run(state, record)   # BUG-15: harnesses never touch the player's save
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
	# the week's room is up: any key or click puts it away, and nothing else fires
	if _scene_layer != null and is_instance_valid(_scene_layer):
		if (event is InputEventKey and event.pressed) or (event is InputEventMouseButton and event.pressed):
			_close_scene()
			get_viewport().set_input_as_handled()
		return
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
		_pump_era_queue()
		# FIRST DAY IN THE NEW PLACE (owner: "space still empty"): arriving in an
		# era renders the new home WITH the crew in it, exactly like founding day.
		if _era_queue.is_empty():
			_era_scene())
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
	_cancel_turn()
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
			g0.week_committing.connect(_drop_curtain)
			g0.week_rolled.connect(_show_die)
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
	state.founder_name = String(result.get("founder_name", ""))
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
	# THE WORLD IS BORN (plan A4/B5): seed the engine, then the bible
	state.sim_seed = record.seed_value if state.sim_seed == 0 else state.sim_seed
	if state.theta.is_empty():
		state.theta = SimEngine.default_theta(state.biz_what, state.biz_who)
	if state.investors.is_empty():
		WorldGen.build(state)
	generator.generate_arcs(state)   # Tier-3 run director: the run's narrative arcs
	music.play("garage")
	var g := GarageViewScreen.new()
	g.setup(state, content, rng, record, generator)
	g.done.connect(_after_grind)
	g.week_committing.connect(_drop_curtain)
	g.week_rolled.connect(_show_die)
	# THE WORLD IS WRITTEN FROM THE PITCH (owner: the bible must not feel
	# disconnected): one LLM call rewrites the deterministic skeleton — market
	# numbers, investors circling THIS space, rivals selling to THESE customers.
	# Keyless keeps the skeleton. Then the market map, then day one.
	if OS.get_environment("RUNWAY_SHOT") == "" and OS.get_environment("RUNWAY_FULLRUN") == "":
		generator.generate_world(state, func(gen: Dictionary) -> void:
			WorldGen.apply_llm_world(state, gen)
			var wr := WorldRevealScreen.new()
			wr.setup(state)
			wr.done.connect(func() -> void:
				_swap(g)
				_cold_open(g))
			_swap(wr)
			wr.size = get_viewport().get_visible_rect().size)
	else:
		_swap(g)
		_cold_open(g)

## WEEK ONE IS GENERATED TOO (owner: "it's just the bland standard situation").
## Day one gets its own DM story: a real roll, a real narration from the pitch,
## the type, the capital and the crew — and the DM's own scene staging drives
## the opening image. Keyless runs keep the authored line and the synthetic
## opening scene.
## DAY ONE IS AN ORIGIN STORY, NOT A GAMBLE (owner): no dice, no "you said" —
## the curtain drops the moment you settle in, the DM writes the founding, the
## beat reads it while the first image of YOUR company renders behind it.
func _cold_open(gv: GarageViewScreen) -> void:
	if generator == null or not generator.llm.enabled():
		_opening_scene()
		return
	_drop_curtain()
	# day one is an adjudication like any other: the journal's lock must refuse
	# until it lands, or a fast press stacks two in-flight turns (probe-caught)
	gv.set("_adjudicating", true)
	var who_founds := (" The founder signing the lease is %s." % state.founder_name) \
			if state.founder_name != "" else ""
	var move := ("This is day one of %s — %s." + who_founds
			+ " Write the FOUNDING of this exact company: the "
			+ "place, the crew, the first real stake in the ground. No dice language, no "
			+ "verdict talk — an opening chapter.") % [state.company_name,
			state.company_idea if state.company_idea != "" else "a company that refuses to explain itself"]
	generator.adjudicate(state, {}, move, func(res: Dictionary) -> void:
		if is_instance_valid(gv):
			gv.set("_adjudicating", false)
		if res.is_empty() or state == null:
			_raise_curtain()
			_opening_scene()
			return
		res["player_text"] = ""        # nothing was "said": this is the founding
		res["interpreted_as"] = ""
		res.erase("dice")
		res.erase("roll")
		res["week_played"] = 0         # day one, before any week is played
		state.last_outcome = {
			"title": "day one", "verdict": "",
			"said": "", "heard": "",
			"narration": String(res.get("narration", "")),
			"reality": String(res.get("reality_check", "")),
			"dec_log": [], "log": [],
			# ALREADY CONSUMED: _begin_turn is called directly below. Without the
			# stamp, _poll_turn found this dm unseen and played the founding beat
			# a second time (the owner's "goes back to Week 1").
			"dm": res.duplicate(true), "dm_seen": true}
		if is_instance_valid(gv):
			gv.set("_last_outcome", state.last_outcome.duplicate(true))
		_begin_turn(res))

func _company_ctx() -> Dictionary:
	if state == null:
		return {}
	return {"name": state.company_name, "idea": state.company_idea,
		"what": state.biz_what, "who": state.biz_who}

## Moving day: the new era's home, generated with the whole crew unpacking in it.
const ERA_ROOMS := {
	"garage": "a scrappy suburban garage workspace",
	"coworking": "a bright coworking floor with hot desks, a phone booth and a coffee corner",
	"office": "the company's first proper small office: a handful of desks and a window",
	"floor": "a full open-plan startup floor of desk rows and monitors",
	"hq": "a top-floor headquarters with floor-to-ceiling glass and a skyline",
}

func _era_scene() -> void:
	if not _art_enabled() or OS.get_environment("RUNWAY_GPT_SCENES") == "0":
		return
	if director == null or state == null or state.dead:
		return
	var scene := {
		"novel_place": ("%s — the new home of %s, a %s company for %s that %s. Moving day: "
				+ "a few boxes still packed, the good chair already claimed.") % [
			String(ERA_ROOMS.get(state.era, "a startup workspace")), state.company_name,
			state.biz_what.to_lower(), state.biz_who.to_lower(),
			state.company_idea if state.company_idea != "" else "keeps going anyway"],
		"place": state.era, "condition": "steady", "time": "day", "framing": "wide",
		"beat": "moving day",
	}
	var cast: Array = [{"who": "founder", "mood": "fine", "doing": "claiming the desk by the window"}]
	var role_who := {"Technical": "tech", "Business": "business", "Design": "tech",
		"Sales": "sales", "The Hustler": "hustler", "The Idea Friend": "idea_friend"}
	for cf in state.cofounders:
		cast.append({"who": String(role_who.get(String(cf.get("role", "Technical")), "tech")),
			"mood": "fine", "doing": "carrying a labelled box"})
	var pack := _cast_pack(cast)
	_scene_seq = _turn_seq
	_scene_headline = "MOVING DAY"
	_last_stage_sig = ""
	director.make_scene_v2(scene, pack["cast"], pack["urls"], "moving day",
			"era_%s_run%d" % [state.era, record.seed_value if record != null else 0], _company_ctx())

## THE FIRST IMAGE IS YOURS (owner directive): generated at launch from the type,
## the segment, the capital and the pitch itself, with the founder and the
## cofounders in it. It renders while week one is being written; when it lands,
## the room under the book quietly becomes YOUR company's room.
func _opening_scene() -> void:
	if not _art_enabled() or OS.get_environment("RUNWAY_GPT_SCENES") == "0":
		return
	if director == null or state == null:
		return
	var money: String = {
		"bootstrap": "furnished from savings: secondhand everything, ramen on the shelf",
		"fnf": "furnished on a family loan: mismatched but hopeful",
		"angel": "fresh angel money visible in one or two conspicuously new purchases",
	}.get(state.funding_id, "")
	var scene := {
		"novel_place": ("the very first workspace of %s, a %s company for %s that %s. "
				+ "A scrappy garage-like space on founding day, %s.") % [
			state.company_name, state.biz_what.to_lower(), state.biz_who.to_lower(),
			state.company_idea if state.company_idea != "" else "does something nobody asked for",
			money],
		"place": "garage", "condition": "steady", "time": "day", "framing": "wide",
		"beat": "founding day",
	}
	var cast: Array = [{"who": "founder", "mood": "fine", "doing": "raising a toast with instant coffee"}]
	var role_who := {"Technical": "tech", "Business": "business", "Design": "tech",
		"Sales": "sales", "The Hustler": "hustler", "The Idea Friend": "idea_friend"}
	for cf in state.cofounders:
		cast.append({"who": String(role_who.get(String(cf.get("role", "Technical")), "tech")),
			"mood": "fine", "doing": "unpacking a box of cables"})
	var pack := _cast_pack(cast)
	_scene_seq = _turn_seq
	_scene_headline = "DAY ONE"
	director.make_scene_v2(scene, pack["cast"], pack["urls"], "founding day",
			"opening_run%d" % (record.seed_value if record != null else 0), _company_ctx())

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
		_cancel_turn()   # the company sold or rang the bell: no room is coming after that
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

# ═══════════════════════════════════════════════════════════════════════════
# THE GENERATIVE WEEK
# ═══════════════════════════════════════════════════════════════════════════
#
# THE TURN, end to end:
#
#   the player writes a move  ─> the DM answers ONCE with both halves of the week:
#                                the consequence text AND the scene to build
#   the player locks the week ─> THE ART STARTS THIS INSTANT (67s one character,
#                                113s four, +107s when the room has to be built)
#                             ─> the reading beat opens over the room and the wait
#                                is spent on the most interesting thing in the game
#   the scene lands           ─> the room opens
#
# WHY MAIN OWNS THIS. The decision page belongs to another lane and raises no signal
# for the lock, so main watches the run instead: the DM's verdict sits on the page as
# a pending move, and the frame it is consumed is the frame the week turned.
#
# WHAT HAPPENS WHEN THE NETWORK DOES NOT COOPERATE — the whole point of the rules
# below, because a render failure must cost a picture and never a turn:
#   · the DM is off or errors   → no scene facets, no beat: the authored week runs
#                                  exactly as it always has
#   · the compose fails         → `failed` keeps the PREVIOUS room and the week
#                                  continues; the player is told nothing, because
#                                  the last room persisting one more week is
#                                  diegetically fine
#   · the render hangs          → the beat closes at HOLD_CEILING regardless
#   · it lands after that       → it opens later, when the player is back in the
#                                  room, or is dropped if the week has moved on
#   · the run ends mid-render   → the turn is cancelled and nothing from it can
#                                  reach the screen
# Nothing on this path awaits a network call without a deadline.

## The beat never holds a reader longer than this, whatever the render is doing.
const HOLD_CEILING := 150.0

## The lock answers INSTANTLY (owner: "it works but doesn't do anything on the
## click"): the theater curtain drops the moment the week commits, the world
## does its thinking behind it, and it rises on the reading beat. If nothing
## arrives (keyless, dead network), it rises anyway — a curtain that stays shut
## is a hang, and 12 seconds is the most a beat may keep its audience waiting.
func _drop_curtain() -> void:
	if _curtain == null or not is_instance_valid(_curtain):
		_curtain = Curtain.new()
		add_child(_curtain)
	# THE CEREMONY OUTRANKS THE CURTAIN, ALWAYS. The premature move_child here
	# put an already-shut curtain (the founding's) ABOVE the cup and the whole
	# roll played invisibly — the owner's "sometimes no video dice roll". The
	# curtain claims the top only once the die has settled.
	if _cup != null and is_instance_valid(_cup):
		move_child(_cup, get_child_count() - 1)
		await _cup.settled
		await get_tree().create_timer(0.15).timeout
	move_child(_curtain, get_child_count() - 1)
	_curtain.close()
	# The hang failsafe. 12s was WRONG: a live adjudication often takes 15-25s,
	# and the curtain lifted onto the stale page mid-think (the owner's "goes
	# back to blank background"). 40s only catches a truly dead network — and a
	# week still mid-adjudication keeps the curtain down however long it takes.
	get_tree().create_timer(40.0).timeout.connect(func() -> void:
		var still_thinking := _screen is GarageViewScreen \
				and bool((_screen as GarageViewScreen).get("_adjudicating"))
		if _curtain != null and is_instance_valid(_curtain) and _curtain.visible \
				and not _turn_busy and not still_thinking:
			push_warning("curtain failsafe: nothing arrived in 40s, opening")
			_curtain.open())

## The table roll: the pre-rendered cup-and-die clip for the rolled number plays
## ON THE ROOM, then the curtain falls. The DM is already thinking while the die
## tumbles, so the ceremony costs no extra wait.
var _cup: DiceRoll

func _show_die(n: int) -> void:
	_roll_ceremony(n)

func _roll_ceremony(n: int) -> void:
	if _cup != null and is_instance_valid(_cup):
		return
	_cup = DiceRoll.new()
	add_child(_cup)
	move_child(_cup, get_child_count() - 1)
	_cup.set_deferred("size", get_viewport().get_visible_rect().size)
	await _cup.roll(n)
	if _cup != null and is_instance_valid(_cup):
		_cup.queue_free()
		_cup = null
	# the cup already showed the die; the curtain stays clean until the stamp

func _raise_curtain() -> void:
	if _curtain != null and is_instance_valid(_curtain) and _curtain.visible:
		_curtain.open()
## Cast sprite URLs, uploaded once by the scene pipeline. res:// paths are useless
## here — the image API has to be able to FETCH every reference.
const CAST_REFS := "res://assets/scenes/refs.json"

## The cast directories, matching the room's own mapping so the composed scene and
## the room agree on who is who. `founder` goes through the archetype that was
## actually drafted — the person in the picture is the person the player chose.
const FOUNDER_CAST := {
	"hacker": "cast_hacker", "hustler": "cast_founder_hustler",
	"exfaang": "cast_founder_pm", "consultant": "cast_founder_consultant",
}
const COFD_CAST := {
	"tech": "cast_cofd_tech", "technical": "cast_cofd_tech", "design": "cast_cofd_tech",
	"business": "cast_cofd_business", "sales": "cast_cofd_sales",
	"hustler": "cast_cofd_hustler", "idea": "cast_cofd_idea",
}
## The DM asks for people by these words; a role string on the cap table answers to one.
const ROLE_KEYS := {
	"tech": "tech", "technical": "tech", "design": "tech", "business": "business",
	"sales": "sales", "hustler": "hustler", "idea": "idea_friend",
}
const MOOD_WORDS := {"burnt": " (burnt out, running on fumes)", "gone": " (checked out entirely)"}

var director: SceneDirector
var _refs: Dictionary = {}          # ref key -> permanent https url
var _dm: Dictionary = {}            # the DM verdict currently pending on the page
var _dm_armed := false              # ...and whether it is still sitting there
var _turn_busy := false
var _turn_seq := 0                  # bumped by any cancel; orphans everything in flight
var _scene_seq := -1                # the seq the in-flight render belongs to
var _scene_path := ""
var _scene_done := false
var _scene_progress := 0.0
var _from_library := ""             # the library room this render is built on, if any
var _last_stage_sig := ""           # place|condition|cast of the last rendered beat
var _scene_headline := ""           # the DM's title for it, kept for a late arrival
var _beat: LoadingScreen
var _scene_layer: Control
var _curtain: Curtain

## A worker still running at shutdown must be joined, or the engine reports a thread
## that was never disposed. Blocking here is correct: the game is already closing.
func _exit_tree() -> void:
	_collect_upload(true)

func _setup_director() -> void:
	director = SceneDirector.new(get_tree())
	director.ready.connect(_on_scene_ready)
	director.failed.connect(_on_scene_failed)
	director.progress.connect(_on_scene_progress)
	if FileAccess.file_exists(CAST_REFS):
		var r = JSON.parse_string(FileAccess.get_file_as_string(CAST_REFS))
		if r is Dictionary:
			_refs = r
	print("RUNWAY! scene library: %d rooms · art %s" % [
		director._entries.size(), "on" if _art_enabled() else "off"])

## HOSTING A LIBRARY ROOM — the caller's job, by the director's design.
## The rooms ship as files, but the remote edit FETCHES its references over HTTP, so a
## room has to exist at a url before anything can be composed on it. The director hands
## that job out here on purpose: an upload that fails has to cost a picture and never a
## turn. It runs on a thread, because the week must not freeze on a subprocess; it is
## cached across runs, so a room costs this once, ever; and only rooms a run actually
## walks into are ever uploaded.
##
## `nexus` is a dev machine's CLI. Where it does not exist the upload simply fails, the
## room is never hosted, and the week keeps the room it already had.
var _upload_thread: Thread
var _upload_id := ""
var _upload_url := ""
var _upload_done := false

func _host_room(id: String) -> bool:
	if _upload_thread != null or id == "":
		return false
	var res_path := director._local_path(id)
	if not FileAccess.file_exists(res_path):
		return false
	_upload_id = id
	_upload_url = ""
	_upload_done = false
	_upload_thread = Thread.new()
	if _upload_thread.start(_upload_worker.bind(ProjectSettings.globalize_path(res_path))) != OK:
		_upload_thread = null
		return false
	return true

func _upload_worker(abs_path: String) -> void:
	var out: Array = []
	var url := ""
	if OS.execute("nexus", ["--timeout", "300", "asset", "upload", abs_path, "--json"], out, true) == 0 \
			and not out.is_empty():
		var parsed = JSON.parse_string(String(out[0]))
		if parsed is Dictionary:
			url = String((parsed as Dictionary).get("url", ""))
			if url == "":
				url = String(((parsed as Dictionary).get("data", {}) as Dictionary).get("url", ""))
	_upload_url = url
	_upload_done = true

## Join a FINISHED upload and tell the director where the room now lives. Returns the
## url, or "" when the upload did not work — in which case the week keeps its old room.
## Never joins a thread that is still working: that would freeze the game on a
## subprocess. A late one is collected by _process instead, and the room is hosted in
## time for a later week. `force` is only for shutdown, where blocking is correct.
func _collect_upload(force: bool = false) -> String:
	if _upload_thread == null or not (_upload_done or force):
		return ""
	_upload_thread.wait_to_finish()
	_upload_thread = null
	var url := _upload_url
	if url != "":
		director.remember_url(_upload_id, url)
	else:
		print("RUNWAY! could not host room %s — keeping the previous room" % _upload_id)
	_upload_id = ""
	_upload_url = ""
	return url

## Art costs money and up to three minutes. Harnesses never spend either, and
## RUNWAY_NO_ART=1 turns it off for anyone who wants the game without renders.
##
## RUNWAY_ART=1 IS THE QA OPT-IN, and it exists because without it no capture
## harness could ever photograph the game a keyed player actually plays: art-off
## was the only path a screenshot had ever shown, so every review of this build
## reviewed a code path nobody buys. Set it alongside any harness and the art
## paths run for REAL. It stays off by default because on means money and minutes
## per room, and the ordinary harnesses have to stay fast and free.
##
## RUNWAY_NO_ART still wins over both: an explicit "off" is never overruled.
func _art_enabled() -> bool:
	if OS.get_environment("RUNWAY_NO_ART") != "":
		return false
	if OS.get_environment("RUNWAY_ART") != "":
		return true   # a harness that asked, in writing, to spend on real renders
	if OS.get_environment("RUNWAY_TURN_ART") != "":
		return true   # the turn harness, asked explicitly for a real render
	return not _harness()

func _harness() -> bool:
	for v in ["RUNWAY_SHOT", "RUNWAY_FULLRUN", "RUNWAY_LANEWIRE", "RUNWAY_READING", "RUNWAY_TURN"]:
		if OS.get_environment(v) != "":
			return true
	return false

## WATCH THE LOCK. The adjudicated move waits on the decision page as a pending
## verdict; `_apply_lock` consumes it into the week's outcome and clears it in the
## same call. That transition — pending, then gone, with the outcome quoting the
## same words back — is the lock. Read through `get()` on purpose: the property
## belongs to another lane's file, and if it is ever renamed this must fall back to
## the authored week, not take the game down with it.
func _poll_turn(gv: GarageViewScreen) -> void:
	if _turn_busy or director == null or gv == null or not is_instance_valid(gv):
		return   # the era overlay or an ending can free the screen mid-frame
	# THE DURABLE PATH: a locked week leaves its full DM payload ON the outcome.
	# The one-press commit can set and consume the pending verdict inside a single
	# frame, so racing it with a poll silently loses the beat — reading the outcome
	# cannot lose. Consumed exactly once via the `dm_seen` stamp.
	var outcome_now = gv.get("_last_outcome")
	if outcome_now is Dictionary:
		var od := outcome_now as Dictionary
		var dm_p = od.get("dm")
		if dm_p is Dictionary and not (dm_p as Dictionary).is_empty() and not bool(od.get("dm_seen", false)):
			od["dm_seen"] = true
			if state != null and not state.dead and not state.has_flag("exit_taken") and not bool(gv.get("_over")):
				_begin_turn((dm_p as Dictionary).duplicate(true))
				return
	var pending = gv.get("_pending_free")
	if pending is Dictionary and not (pending as Dictionary).is_empty():
		_dm = (pending as Dictionary).duplicate(true)
		_dm_armed = true
		return
	if not _dm_armed:
		return
	_dm_armed = false
	var outcome = gv.get("_last_outcome")
	if not (outcome is Dictionary):
		return
	var said := String((outcome as Dictionary).get("said", ""))
	if said == "" or said != String(_dm.get("player_text", "")):
		return   # the verdict was replaced or discarded, never locked
	if state == null or state.dead or state.has_flag("exit_taken") or bool(gv.get("_over")):
		return   # the run ended on this very move; there is no next room to show
	_begin_turn(_dm)

## `stub_path` is the harness seam ONLY: it stands in for the render so the beat and
## the scene opening can be reviewed without a network call. Empty in a real game.
func _begin_turn(dm: Dictionary, stub_path: String = "") -> void:
	var scene: Dictionary = dm.get("scene", {})
	var narration := String(dm.get("narration", "")).strip_edges()
	# THE BEAT IS FOR THE READING; THE ART IS WHAT IT WAITS FOR. With a render in
	# flight it fills the wait. With no render — art off, or no key — it still runs,
	# because the consequence chain IS the payoff of the written move, and with
	# nothing to wait for it closes the moment the last line is read. With neither
	# text nor art there is nothing to show, and the authored week runs untouched.
	var want_art := stub_path != "" or (_art_enabled() and not scene.is_empty())
	# one line per turn, permanently: this is the heartbeat a whole playthrough
	# once went without, and nobody could see that from the log
	print("TURN wk%02d: beat opens · narration %d chars · art %s · place %s" % [
		state.week if state != null else -1, narration.length(),
		"ON" if want_art else "off", String(scene.get("place", "-"))])
	print("TURN headline: %s" % String(dm.get("headline", "")))
	print("TURN journal_note: %s" % String(dm.get("journal_note", "")))
	print("TURN narration[0:220]: %s" % narration.left(220).replace("\n", " / "))
	if narration == "" and not want_art:
		return
	_turn_busy = true
	if _screen is GarageViewScreen:
		(_screen as GarageViewScreen)._world_busy = true
	var seq := _turn_seq
	_scene_seq = seq
	_scene_path = ""
	_scene_done = false
	_scene_progress = 0.0

	var cast_pack := _cast_pack(dm.get("cast", []))
	var want := {
		"family": String(scene.get("family", "scrappy_workspace")),
		"place": _slug(String(scene.get("place", ""))),
		"time": String(scene.get("time", "day")),
		"condition": String(scene.get("condition", "steady")),
		"framing": String(scene.get("framing", "wide")),
	}
	_scene_headline = String(dm.get("headline", ""))
	var out_name := "run%d_wk%02d" % [record.seed_value if record != null else 0, state.week]
	# WHICH ROOM THIS IS BUILT ON. A remembered room is only as good as its url, and
	# a generated url can expire — so if the compose dies on a library room, that room
	# is forgotten for this session and the place gets rebuilt next time rather than
	# failing every week from now on.
	# CHANGE-BEATS (owner-approved cadence): a fresh image is generated when the
	# WEEK'S STAGE changes — new place, new condition, new cast. A quiet week in
	# the same room keeps its scene, the beat still reads, and nothing is spent.
	var stage_sig := "wk%d|%s|%s" % [state.week, String(scene.get("novel_place", want["place"])),
			want["condition"]]
	if want_art and stub_path == "" and stage_sig == _last_stage_sig and _scene_layer == null:
		want_art = false
	var pick := director.resolve(want)
	_from_library = "" if bool(pick.get("miss", true)) else String(pick.get("id", ""))
	# THE GENERATIVE PATH IS THE MAIN PATH (owner pivot): one GPT-medium image per
	# staged beat, built from references + an instruction contract. The library +
	# seedream compose remain the fallback ladder behind RUNWAY_GPT_SCENES=0.
	var use_v2 := want_art and stub_path == "" \
			and OS.get_environment("RUNWAY_GPT_SCENES") != "0"
	# A room the run has never walked into is on disk but not yet at a url. Start
	# hosting it now, in the background, and compose the moment it lands.
	var hosting := want_art and not use_v2 and stub_path == "" and _from_library != "" \
			and director.needs_upload(_from_library) and _host_room(_from_library)

	# THE ART STARTS FIRST — before a single line is drawn, because every second of
	# the beat is a second of render already paid for.
	if hosting:
		pass   # the compose is fired below, as soon as the room has a url
	elif not want_art:
		_scene_done = true   # nothing is coming; the beat is pure reading
	elif stub_path != "":
		# harness only: stand in for the render, "FAIL" to rehearse a dead network
		get_tree().create_timer(3.0).timeout.connect(func():
			if stub_path == "FAIL":
				_on_scene_failed("harness: forced failure")
			else:
				_on_scene_ready(stub_path))
	elif use_v2:
		_last_stage_sig = stage_sig
		director.make_scene_v2(scene, cast_pack["cast"], cast_pack["urls"],
				String(scene.get("beat", "")), out_name, _company_ctx())
	else:
		_last_stage_sig = stage_sig
		director.make_scene(want, String(scene.get("novel_place", "")), cast_pack["cast"],
			cast_pack["urls"], String(scene.get("beat", "")), out_name)
		# it may have failed before it ever reached the network (no key, bad request).
		# That is not a reason to skip the week: the beat carries on as reading.

	var l := LoadingScreen.new()
	var wkp := int(dm.get("week_played", state.week))
	l.begin("DAY ONE" if wkp <= 0 else "WEEK %d" % wkp)
	add_child(l)
	l.size = get_viewport().get_visible_rect().size
	_beat = l
	# the DM's own title for the week, and then the week itself. It has nowhere else
	# to be: the room takes the picture, not the words.
	l.say("", String(dm.get("headline", "")))
	if String(dm.get("player_text", "")) != "":
		l.say("You said", String(dm.get("player_text", "")))
		l.say("They heard", String(dm.get("interpreted_as", "")))
	# THE JUDGEMENT: the settled die gets its DC and its band before anything
	# else — the player watches the number they rolled become the week they got.
	var roll_d: Dictionary = dm.get("roll", {})
	var dice_d: Dictionary = dm.get("dice", {})
	var used_d20 := int(dice_d.get("used", 0))
	if used_d20 > 0 and not roll_d.is_empty():
		# the die was FINAL at the press (the cup already showed it); here it
		# meets the DC and is explained in plain words
		var stt := String(dice_d.get("stat", "grit"))
		var mod := int(dice_d.get("mod", 0))
		var mode := String(dice_d.get("mode", ""))
		var dc := int(roll_d.get("dc", 10))
		var total := used_d20 + mod
		var band_key := SimEngine.margin_band(total, dc)
		var band: String = {"brilliant": "BRILLIANT", "fine": "IT LANDS",
			"risky": "MIXED RESULT", "backfired": "IT BACKFIRES"}.get(band_key, "IT LANDS")
		var mode_txt := ("  ·  " + mode) if mode != "" else ""
		l.say("", "The die came up %d. Your %s adds %s%d — total %d, and this needed %d. %s%s" % [
			used_d20, stt, "+" if mod >= 0 else "−", absi(mod), total, dc,
			{"brilliant": "It lands beautifully.", "fine": "It lands.",
			 "risky": "It half-lands: something gives.", "backfired": "It goes wrong."}.get(band_key, "It lands."),
			mode_txt])
	l.say("", String(dm.get("narration", "")))
	l.say("", String(dm.get("reality_check", "")))
	if _curtain != null and is_instance_valid(_curtain):
		move_child(_curtain, get_child_count() - 1)
		# THE Z-ORDER OF THE TURN: beat under curtain under cup. Without the cup
		# re-raise, a verdict landing mid-ceremony added the beat topmost and the
		# reading text covered the playing die (the owner's overlap photo).
		if _cup != null and is_instance_valid(_cup):
			move_child(_cup, get_child_count() - 1)
			await _cup.settled
		await get_tree().create_timer(0.9 if used_d20 > 0 else 0.65).timeout
	_raise_curtain()

	var deadline := Time.get_ticks_msec() + int(HOLD_CEILING * 1000.0)
	# THE ROOM HAS TO EXIST AT A URL BEFORE ANYTHING CAN BE PAINTED INTO IT. The reader
	# is already reading while this finishes; it takes about two seconds, and it shares
	# the same deadline as everything else on this path.
	if hosting:
		while not _upload_done and Time.get_ticks_msec() < deadline and seq == _turn_seq:
			await get_tree().process_frame
		if seq != _turn_seq:
			return
		if _collect_upload() != "":
			director.make_scene(want, String(scene.get("novel_place", "")), cast_pack["cast"],
				cast_pack["urls"], String(scene.get("beat", "")), out_name)
		else:
			_scene_done = true   # unhosted: no art this week, and the reading goes on

	# THE BOUNDED WAIT. The pen tracks the real render; the deadline is what makes a
	# hung request impossible to be stranded by.
	while not _scene_done and Time.get_ticks_msec() < deadline and seq == _turn_seq:
		l.report(_scene_progress)
		await get_tree().process_frame
	if seq != _turn_seq:
		return   # the run ended under us; the cancel already took the beat away
	await l.finish()   # drains whatever is left to read, then fades. Never the art.
	_beat = null
	if seq != _turn_seq:
		return
	_turn_busy = false
	if _screen is GarageViewScreen:
		(_screen as GarageViewScreen)._world_busy = false
	if _scene_path != "":
		_open_scene(_scene_path, String(dm.get("headline", "")))
	_check_exit()   # deferred while the week was being read

## Progress is never allowed to run backwards: the director reports the novel-room
## generation and the compose on one channel, and the second stage restarts its own
## count. A pen that goes back down reads as a bug, so it only ever moves forward.
func _on_scene_progress(f: float) -> void:
	_scene_progress = maxf(_scene_progress, clampf(f, 0.0, 1.0))

func _on_scene_ready(path: String) -> void:
	if _scene_seq != _turn_seq:
		return   # belongs to a run that has already ended
	print("TURN art landed: %s" % path)
	_scene_path = path
	_scene_done = true
	if not _turn_busy:
		_late_scene(path)

## A FAILED RENDER IS A COSMETIC LOSS. The previous room stays, the week continues,
## and the only trace is a line in the log for whoever is watching.
func _on_scene_failed(reason: String) -> void:
	if _scene_seq != _turn_seq:
		return
	print("TURN art FAILED: %s" % reason)
	_scene_path = ""
	_scene_done = true
	if _from_library != "":
		# A room the GAME generated carries its own url, and that url is signed and
		# expires within a day, so a failure on one means it has gone stale: stop
		# trusting it for this session and let the place be rebuilt. A shipped room is
		# hosted permanently, so a failure there is the request, not the room, and the
		# library keeps it.
		for i in range(director._entries.size() - 1, -1, -1):
			var e: Dictionary = director._entries[i]
			if String(e.get("id", "")) == _from_library and String(e.get("url", "")) != "":
				director._entries.remove_at(i)
		_from_library = ""
	print("RUNWAY! scene skipped (%s) — keeping the previous room" % reason)

## The render came in after the beat closed. Handing it to the room is never an
## interruption — it just becomes the room, even behind an open book, and the player
## looks up into it when they are done writing. Only the fallback overlay has to wait
## for the book to be shut, and is dropped if the week has moved on.
func _late_scene(path: String) -> void:
	if not (_screen is GarageViewScreen) or state == null or state.dead:
		return
	if _screen.has_method("adopt_composed") and bool(_screen.call("adopt_composed", path, false)):
		return
	if _scene_layer != null and is_instance_valid(_scene_layer):
		return
	var journal = (_screen as GarageViewScreen).get("_journal")
	if journal != null and is_instance_valid(journal) and bool(journal.visible):
		print("RUNWAY! scene arrived late while the book was open — dropped")
		return
	_open_scene(path, _scene_headline)

## NOTHING IN FLIGHT SURVIVES THE END OF A RUN. Bumping the sequence orphans the
## render, the beat and anything the director still has in the air; the beat is
## hidden rather than freed, so the coroutine awaiting it can finish safely.
func _cancel_turn() -> void:
	_turn_seq += 1
	_turn_busy = false
	if _screen is GarageViewScreen:
		(_screen as GarageViewScreen)._world_busy = false
	_dm = {}
	_dm_armed = false
	_scene_path = ""
	_scene_headline = ""
	_scene_done = true
	if _beat != null and is_instance_valid(_beat):
		_beat.visible = false
		_beat.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_beat = null
	if _scene_layer != null and is_instance_valid(_scene_layer):
		_scene_layer.queue_free()
	_scene_layer = null

## THE ROOM YOU ARE LOOKING AT. The composed scene fills the frame; the headline
## sits in the calm top band and the hint in the calm bottom one — the two strips
## the compose prompt keeps clear for exactly this. Any key or click puts it away.
func _open_scene(path: String, headline: String) -> void:
	# THE BOUNDARY: the room screen owns what the room looks like, so the week's scene
	# is handed to it and BECOMES the room — no overlay to dismiss, the player simply
	# looks up from the page into the room their decision made. `aligned` is false: the
	# turn composes on a library room, not on the stage we were standing in, so the
	# room's handwriting stands down rather than writing the cash total across a
	# stranger's wall. The overlay below is the fallback for anywhere else.
	if _screen is GarageViewScreen and _screen.has_method("adopt_composed"):
		if bool(_screen.call("adopt_composed", path, false)):
			return
	var img := Image.new()
	if img.load(path) != OK:
		print("RUNWAY! scene unreadable on disk: %s" % path)
		return
	var vp := get_viewport().get_visible_rect().size
	var layer := Control.new()
	layer.size = vp
	layer.mouse_filter = Control.MOUSE_FILTER_STOP
	var back := ColorRect.new()
	back.color = Color(0.06, 0.05, 0.07, 1.0)
	back.size = vp
	back.mouse_filter = Control.MOUSE_FILTER_IGNORE
	layer.add_child(back)
	var tr := TextureRect.new()
	tr.texture = ImageTexture.create_from_image(img)
	tr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	tr.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	tr.size = vp
	tr.mouse_filter = Control.MOUSE_FILTER_IGNORE
	layer.add_child(tr)
	var font: Font = load("res://assets/fonts/PatrickHand-Regular.ttf")
	if headline.strip_edges() != "":
		layer.add_child(_scene_line(headline, font, 44, Color("F2EAD3"), vp, 34.0))
	layer.add_child(_scene_line("click anywhere to get on with the week", font, 26,
		Color(Color("F2EAD3"), 0.55), vp, vp.y - 74.0))
	add_child(layer)
	_scene_layer = layer
	layer.modulate.a = 0.0
	create_tween().tween_property(layer, "modulate:a", 1.0, 0.35)

func _scene_line(text: String, font: Font, sz: int, col: Color, vp: Vector2, y: float) -> Label:
	var l := Label.new()
	l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	l.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	l.add_theme_font_override("font", font)
	l.add_theme_font_size_override("font_size", sz)
	l.add_theme_color_override("font_color", col)
	l.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0.55))
	l.add_theme_constant_override("shadow_offset_y", 3)
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	l.text = text
	l.position = Vector2(vp.x * 0.12, y)
	l.size = Vector2(vp.x * 0.76, 0)
	l.custom_minimum_size = Vector2(vp.x * 0.76, 0)
	return l

func _close_scene() -> void:
	if _scene_layer == null or not is_instance_valid(_scene_layer):
		_scene_layer = null
		return
	var l := _scene_layer
	_scene_layer = null
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var tw := l.create_tween()
	tw.tween_property(l, "modulate:a", 0.0, 0.25)
	tw.tween_callback(l.queue_free)

## THE CAST IS THE PLAYER'S ACTUAL CREW — never a generic set.
## The model paints these characters into the room itself, so what it is handed IS
## the company: the founder is the archetype that was drafted, the crew are the
## people still on the cap table and the payroll, and the moods are the ones the
## room is already showing — burnt when loyalty or morale says burnt. The DM only
## chooses who is present this week and what each of them is doing.
##
## A character with no fetchable sprite is dropped from BOTH lists together: the
## compose prompt numbers the roster against the images, so they must stay equal or
## the model is told about someone it was never shown.
func _cast_pack(dm_cast) -> Dictionary:
	var cast: Array = []
	var urls: Array = []
	if not (dm_cast is Array) or state == null:
		return {"cast": cast, "urls": urls}
	var roster := _crew_roster()
	var used: Array = []
	for c in dm_cast:
		if not (c is Dictionary):
			continue
		var who := String((c as Dictionary).get("who", "")).to_lower()
		if not roster.has(who) or used.has(who):
			continue   # the DM asked for someone this company does not have
		var person: Dictionary = roster[who]
		var url := _cast_url(String(person["base"]), String(person["mood"]))
		if url == "":
			continue
		used.append(who)
		cast.append({
			"role": String(person["role"]) + String(MOOD_WORDS.get(String(person["mood"]), "")),
			"doing": String((c as Dictionary).get("doing", "at work")),
		})
		urls.append(url)
	# the DM wanted people and none of them exist: the founder is always in the run
	if cast.is_empty() and not (dm_cast as Array).is_empty() and roster.has("founder"):
		var f: Dictionary = roster["founder"]
		var f_url := _cast_url(String(f["base"]), String(f["mood"]))
		if f_url != "":
			cast.append({"role": String(f["role"]) + String(MOOD_WORDS.get(String(f["mood"]), "")),
				"doing": "in the middle of it"})
			urls.append(f_url)
	return {"cast": cast, "urls": urls}

## Who this company actually contains, keyed by the words the DM uses. Moods follow
## the same rules the room uses, so the picture never disagrees with the crew line.
func _crew_roster() -> Dictionary:
	var out: Dictionary = {}
	var f_burnt := state.morale <= 30 or state.weeks_in_red >= 2
	out["founder"] = {
		"base": String(FOUNDER_CAST.get(state.archetype_id, "cast_hacker")),
		"mood": "burnt" if f_burnt else "fine",
		"role": ("%s, the founder" % state.archetype_name.to_lower()) if state.archetype_name != "" else "founder",
	}
	for cf in state.cofounders:
		var role := String(cf.get("role", "Tech"))
		var key := _role_key(role)
		if out.has(key):
			continue   # one sprite per type: the DM names types, not names
		var sour := int(cf.get("loyalty", 70)) <= 30 or state.morale <= 20 or state.has_flag("trap_underpaid_cofounder")
		out[key] = {"base": _cofd_base(role), "mood": "burnt" if sour else "fine",
			"role": "%s cofounder" % role.to_lower()}
	for e in state.employees:
		var e_role := String(e.get("role", "generalist"))
		var e_key := _role_key(e_role)
		if out.has(e_key):
			continue
		var cooked := GameState.burnout_state(int(e.get("burnout", 0))) in ["cooked", "gone"]
		out[e_key] = {"base": _cofd_base(e_role), "mood": "burnt" if cooked else "fine",
			"role": "%s, the %s" % [String(e.get("name", "the hire")).to_lower(), e_role.to_lower()]}
	return out

## Roles arrive as display strings ("The Idea Friend") and events invent their own
## ("generalist"), so match on containment before defaulting.
func _role_key(role: String) -> String:
	var key := role.to_lower().strip_edges()
	if ROLE_KEYS.has(key):
		return String(ROLE_KEYS[key])
	for k in ROLE_KEYS:
		if key.contains(String(k)):
			return String(ROLE_KEYS[k])
	return "tech"

func _cofd_base(role: String) -> String:
	var key := role.to_lower().strip_edges()
	if COFD_CAST.has(key):
		return String(COFD_CAST[key])
	for k in COFD_CAST:
		if key.contains(String(k)):
			return String(COFD_CAST[k])
	return "cast_cofd_tech"

func _cast_url(base: String, mood: String) -> String:
	if base == "":
		return ""
	# a mood we have no sprite for is better shown at the wrong mood than not at all
	for m in [mood, "fine"]:
		for layer in ["sprite", "scene"]:
			var u := String(_refs.get("%s_%s/%s" % [base, m, layer], ""))
			if u.begins_with("http"):
				return u
	return ""

## The DM writes places in English; the library indexes them as slugs.
func _slug(s: String) -> String:
	var out := ""
	for ch in s.strip_edges().to_lower():
		if (ch >= "a" and ch <= "z") or (ch >= "0" and ch <= "9"):
			out += ch
		elif out != "" and not out.ends_with("_"):
			out += "_"
	return out.rstrip("_")

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
	if _turn_busy:
		# the week that just locked is still being read. Ending the run out from
		# under the beat would tear the page away mid-sentence; the turn calls this
		# again the moment it closes, so nothing is skipped, only sequenced.
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
	_cancel_turn()   # the run is over: no render still in the air may reach the screen
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

## BUG-15 — THE HARNESSES GET THEIR OWN PROFILE.
## Every autopilot run used to land in the player's gallery: 39 runs and ×37 FOUNDER
## FLATLINE that nobody ever played. A harness now keeps its own book at
## user://profile_harness.json — same shape, so it can still be read back — and the
## player's profile and saved run are left exactly as they were.
const HARNESS_PROFILE := "user://profile_harness.json"

func _record_run_end(cause: String) -> void:
	if not _harness():
		SaveSystem.record_run_end(state, cause)
		return
	var prof: Dictionary = {"version": SaveSystem.VERSION, "runs": [], "endings_seen": [], "best_payout": 0}
	if FileAccess.file_exists(HARNESS_PROFILE):
		var parsed = JSON.parse_string(FileAccess.get_file_as_string(HARNESS_PROFILE))
		if parsed is Dictionary:
			prof = parsed
	var payout := state.payout_today()
	var runs: Array = prof.get("runs", [])
	runs.append({
		"company": state.company_name, "archetype": state.archetype_name,
		"weeks": state.week, "era": state.era, "cause": cause, "payout": payout,
		"founder_pct": state.founder_pct, "pivots": state.pivots, "harness": true,
	})
	while runs.size() > 50:
		runs.pop_front()
	prof["runs"] = runs
	var seen: Array = prof.get("endings_seen", [])
	if not seen.has(cause):
		seen.append(cause)
	prof["endings_seen"] = seen
	prof["best_payout"] = maxi(int(prof.get("best_payout", 0)), payout)
	var f := FileAccess.open(HARNESS_PROFILE, FileAccess.WRITE)
	if f != null:
		f.store_string(JSON.stringify(prof))
		f.close()

func _to_autopsy(result: Dictionary, exit_kind: String = "") -> void:
	_cancel_turn()
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
	_record_run_end(("[DAILY] " if daily_mode else "") + headline.split("\n")[0])
	daily_mode = false
	music.play("last_page")
	music.set_stem("")
	var a := AutopsyScreen.new()
	a.setup(headline, record, state)
	a.done.connect(_to_title)
	_swap(a)
