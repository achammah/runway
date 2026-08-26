extends SceneTree
## TUTORIAL SHOTS — the five coach chips (seen_coach_v3) over a live week-1
## room, plus the harness-suppression proof: under every garage harness env the
## coach must never appear. The probe NEVER writes the seen mark: steps are
## driven by hand and the last card is freed, not advanced, so a probe run
## cannot burn the owner's one real first-start.
## Run (windowed): RUNWAY_TUTORIAL_DIR=<dir> godot --path . --script tests/shots_tutorial.gd

var _dir := "/tmp/tutorial_shots"

func _init() -> void:
	call_deferred("_go")

func _shot(nm: String) -> void:
	await create_timer(0.35).timeout
	await RenderingServer.frame_post_draw
	root.get_viewport().get_texture().get_image().save_png("%s/%s.png" % [_dir, nm])
	print("SHOT %s" % nm)

func _state() -> GameState:
	var s := GameState.new()
	s.week = 1
	s.company_name = "Blobsworth"
	s.company_idea = "compliance software with feelings"
	s.archetype_id = "hacker"
	s.archetype_name = "The Hacker"
	s.cash = 24000
	s.product = 12
	s.traction = 0
	s.morale = 70
	s.hype = 10
	s.founder_pct = 70.0
	s.cofounders = [{"role": "Technical", "commitment": "Full-time",
		"equity": 30.0, "vesting": true, "loyalty": 70}]
	return s

func _go() -> void:
	await process_frame
	var d := OS.get_environment("RUNWAY_TUTORIAL_DIR")
	if d != "":
		_dir = d
	DirAccess.make_dir_recursive_absolute(_dir)
	var content := ContentDb.new()
	content.load_all()
	var llm := LlmClient.new()          # never .setup(): keyless, no art, no cost
	root.add_child(llm)
	var gen := EventGenerator.new(llm)
	root.add_child(gen)

	# ── the five chips, driven by hand ────────────────────────────────────────
	var mark_pre := FileAccess.file_exists("user://seen_coach_v3")
	var g := GarageViewScreen.new()
	g.setup(_state(), content, SeededRng.new(11), RunRecord.new(), gen)
	root.add_child(g)
	g.size = Vector2(1536, 1024)
	await create_timer(1.2).timeout
	var fired := g._coach != null and is_instance_valid(g._coach)
	print("COACH auto-fired on the week-1 room: %s (mark pre-existing: %s)" % [str(fired), str(mark_pre)])
	for i in 5:
		g._coach_step = i
		g._show_coach_step()
		await _shot("chip_%d" % i)
	# dismiss WITHOUT advancing: the mark must stay unwritten
	if g._coach != null and is_instance_valid(g._coach):
		g._coach.queue_free()
		g._coach = null
	await _shot("after_dismissed")
	var mark_untouched := mark_pre == FileAccess.file_exists("user://seen_coach_v3")
	print("MARK untouched: %s" % str(mark_untouched))
	g.queue_free()
	await process_frame

	# ── suppression: every garage harness env keeps the coach away ────────────
	var results := {}
	for env in ["RUNWAY_SHOT", "RUNWAY_SHOTS", "RUNWAY_FULLRUN", "RUNWAY_FIRSTFLOW"]:
		OS.set_environment(env, "1")
		var g2 := GarageViewScreen.new()
		g2.setup(_state(), content, SeededRng.new(12), RunRecord.new(), gen)
		root.add_child(g2)
		g2.size = Vector2(1536, 1024)
		await create_timer(0.7).timeout
		results[env] = g2._coach == null
		if env == "RUNWAY_SHOT":
			await _shot("suppressed_runway_shot")
		g2.queue_free()
		await process_frame
		OS.set_environment(env, "")
	print("SUPPRESSION %s" % str(results))

	var ok := mark_untouched and (fired or mark_pre)
	for k in results:
		if not results[k]:
			ok = false
	print("TUTORIAL SHOTS %s" % ("OK" if ok else "FAILED"))
	quit(0 if ok else 1)
