extends SceneTree
## Journal stress harness: worst-case content volumes through all 5 spreads.
## 4 cofounders + 3 employees + max-length adjudication + every pending slot
## filled. Screenshots to $RUNWAY_STRESS_DIR. Proves wrapping never breaks.

func _init() -> void:
	call_deferred("_go")

func _go() -> void:
	await process_frame
	var dir := OS.get_environment("RUNWAY_STRESS_DIR")
	if dir == "":
		dir = "/tmp/lane_garage_stress"
	DirAccess.make_dir_recursive_absolute(dir)

	var content := ContentDb.new()
	content.load_all()
	var llm := LlmClient.new()
	root.add_child(llm)
	var gen := EventGenerator.new(llm)
	root.add_child(gen)
	var rng := SeededRng.new(7)
	var record := RunRecord.new()

	var state := GameState.new()
	state.week = 9
	state.company_name = "Blobsworth Industrial"
	state.company_idea = "Peer-to-peer subscription box for artisanal compliance software"
	state.archetype_id = "consultant"
	state.archetype_name = "THE EX-CONSULTANT"
	state.cash = -450
	state.weeks_in_red = 2
	state.product = 38
	state.traction = 7
	state.morale = 31
	state.hype = 22
	state.founder_pct = 41.0
	state.set_flag("lost_majority")
	state.cofounders = [
		{"role": "Technical", "commitment": "Full-time", "equity": 22.0, "vesting": true, "loyalty": 18},
		{"role": "Business", "commitment": "Full-time", "equity": 15.0, "vesting": true, "loyalty": 55},
		{"role": "Design", "commitment": "Part-time", "equity": 12.0, "vesting": false, "loyalty": 71},
		{"role": "The Idea Friend", "commitment": "Part-time", "equity": 10.0, "vesting": false, "loyalty": 34},
	]
	state.employees = [
		{"name": "Priya", "role": "engineer", "salary": 1400, "burnout": 78, "quirk": "rust evangelist"},
		{"name": "Marcus", "role": "sales", "salary": 1200, "burnout": 45, "quirk": "gong owner"},
		{"name": "Jun", "role": "support", "salary": 900, "burnout": 12, "quirk": "plant whisperer"},
	]
	state.items.assign(["itm_laptop", "itm_houseplant", "itm_guitar", "itm_dads_server"])

	var screen := GarageViewScreen.new()
	screen.setup(state, content, rng, record, gen)
	root.add_child(screen)
	screen.size = Vector2(1536, 1024)
	await create_timer(1.2).timeout

	# force the worst-case journal payload AFTER _ready ran its own week
	screen._current_event = {
		"title": "The Longest Possible Crisis Title That Still Fits",
		"body": "Your largest customer, a regional dog-grooming consortium with eleven locations and a group chat, has discovered that the artisanal compliance module recommends the same haircut for every regulatory filing. They want a refund, an apology, a roadmap, and — inexplicably — a birthday card for someone named Gerald.",
		"choices": [
			{"label": "Refund everyone. Apologize profusely. Eat the quarter.", "effects": [{"op": "cash_delta", "v": -900}]},
			{"label": "Blame the intern who does not exist", "effects": [{"op": "hype_delta", "v": -4}]},
			{"label": "Send Gerald the card, fix nothing else yet", "effects": [{"op": "morale_delta", "v": 2}]},
		],
	}
	screen._last_outcome = {
		"title": "The Groomer Revolt, Continued",
		"verdict": "backfired",
		"narration": "You attempted to charm all eleven locations simultaneously with a livestreamed apology, but the stream froze on your face mid-sentence for nine full minutes, and the consortium has now adopted the freeze-frame as their official letterhead.",
		"reality": "You do not, in fact, own a professional streaming rig, a second camera, or the goodwill you budgeted for.",
		"log": ["cash -900", "hype -6", "morale -4", "product +2"],
	}
	screen._pending_work["PRODUCT"] = {"kind": "free", "text": "Rewrite the recommendation engine so each of the eleven locations gets genuinely distinct filings, then ship a per-breed configuration matrix"}
	screen._pending_work["MARKETING"] = {"kind": "preset", "id": "post_log"}
	screen._pending_work["SALES"] = {"kind": "preset", "id": "chase"}
	screen._pending_people[0] = "pay"
	screen._pending_people[2] = "shares"
	screen._pending_choice = screen._current_event["choices"][0]

	screen._open_journal()
	await create_timer(0.6).timeout
	for i in 5:
		screen._page_i = i
		print("SPREAD %d composing" % i)
		screen._show_spread()
		await create_timer(0.45).timeout
		await RenderingServer.frame_post_draw
		var img := root.get_viewport().get_texture().get_image()
		img.save_png("%s/stress_p%d.png" % [dir, i])
	print("STRESS DONE: 5 spreads captured to " + dir)
	# Optional second pass: film the page TURN (arrow press -> old sheet away,
	# new sheet lands, reveal begins). Run WITHOUT RUNWAY_INSTANT_PAGES to see
	# the writing start after the paper settles.
	if OS.get_environment("RUNWAY_TURN_FILM") != "":
		screen._page_i = 1
		screen._show_spread()
		await create_timer(0.8).timeout
		screen._jp.next_page.emit()
		var stamps := [0.06, 0.12, 0.2, 0.3, 0.45, 0.7, 1.1, 1.6]
		var t0 := Time.get_ticks_msec()
		for i in stamps.size():
			var wait: float = float(stamps[i]) - float(Time.get_ticks_msec() - t0) / 1000.0
			if wait > 0.0:
				await create_timer(wait).timeout
			await RenderingServer.frame_post_draw
			root.get_viewport().get_texture().get_image().save_png("%s/turn_%02d.png" % [dir, i])
		print("TURN FILMED")
	quit(0)
