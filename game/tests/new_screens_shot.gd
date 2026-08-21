extends SceneTree
## Photographs every screen added in the overhaul, directly instantiated:
## title menu + slot panel, HOW IT WORKS, the keys desk, the birth screen
## (full-frame loop), and the book intro fed with a sample entry.
## Run: RUNWAY_STRESS_DIR=<dir> godot --path . --script tests/new_screens_shot.gd

func _shot(name: String) -> void:
	await RenderingServer.frame_post_draw
	var dir := OS.get_environment("RUNWAY_STRESS_DIR")
	root.get_viewport().get_texture().get_image().save_png("%s/%s.png" % [dir, name])

func _init() -> void:
	call_deferred("_go")

func _go() -> void:
	await process_frame
	var vp := Vector2(1536, 1024)

	# a fake save so the menu shows CONTINUE and the slot panel has a row
	SaveSystem.active_slot = 2
	var s := GameState.new()
	s.company_name = "Driftdeck"
	s.founder_name = "Zara Duval"
	s.week = 7
	var rec := RunRecord.new()
	rec.seed_value = 777
	SaveSystem.save_run(s, rec)

	var t := TitleScreen.new()
	root.add_child(t)
	t.size = vp
	await create_timer(1.2).timeout
	t._show_menu()
	await create_timer(0.9).timeout
	await _shot("n1_title_menu")
	t._pick_slot(false)
	await create_timer(0.8).timeout
	await _shot("n2_slot_panel")
	t.queue_free()
	SaveSystem.clear_run()

	var ht := HowToScreen.new()
	root.add_child(ht)
	ht.size = vp
	await create_timer(0.7).timeout
	await _shot("n3_howto")
	ht.queue_free()

	var kd := KeysScreen.new()
	root.add_child(kd)
	kd.size = vp
	await create_timer(0.6).timeout
	await _shot("n4_keys")
	kd.queue_free()

	var bs := BirthScreen.new()
	root.add_child(bs)
	bs.size = vp
	await create_timer(1.0).timeout
	await _shot("n5_birth_fullframe")
	bs.queue_free()

	var st2 := GameState.new()
	st2.company_name = "Fernora"
	st2.theta = SimEngine.default_theta("Service", "Consumer")
	SimEngine.seed_beliefs(st2)
	st2.set_meta("market_line", "a market that books calm by the hour")
	st2.investors = [{"name": "Steamline Partners", "archetype": "the operator VC",
		"thesis": "wellness works when it sells a repeatable escape to people who cannot leave their jobs"}]
	st2.rivals = [{"name": "Brume House", "strength": 45.0,
		"what": "polished urban thermal spas for office workers"}]
	var bk := BookIntroScreen.new()
	bk.setup(st2)
	root.add_child(bk)
	bk.size = vp
	await create_timer(0.6).timeout
	bk.feed_entry("The key sticks, then gives. I sign the lease on the hood of a borrowed car and carry the first box in alone. Two treatment rooms, a reception desk the last tenant abandoned, and a smell of paint that will outlast my savings. We are promising tired people one calm hour that starts on time. It will cost eleven thousand a month before a single towel is warm. Tonight that number looks enormous, and I write it down anyway so tomorrow it looks like a plan.")
	await create_timer(0.8).timeout
	await _shot("n6_book_intro")
	print("NEW SCREENS SHOT DONE")
	quit(0)
