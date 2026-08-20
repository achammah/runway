extends SceneTree
## The reveal performance, photographed. Composes one representative page LIVE
## (no instant switch) and captures a timed filmstrip of the writing, then
## clicks a choice and films the pen circle being drawn. For eyes, not asserts:
## run without --headless, frames land in $RUNWAY_REVEAL_DIR.
##
##   RUNWAY_REVEAL_DIR=/tmp/reveal godot --path . --script tests/journal_reveal_probe.gd

const TIMES := [0.15, 0.5, 0.9, 1.4, 1.9, 2.4, 2.9, 3.4, 3.9]

func _init() -> void:
	call_deferred("_go")

func _go() -> void:
	await process_frame
	var dir := OS.get_environment("RUNWAY_REVEAL_DIR")
	if dir == "":
		dir = "/tmp/journal_reveal"
	DirAccess.make_dir_recursive_absolute(dir)

	var bg := ColorRect.new()
	bg.color = Color("6E6862")
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.add_child(bg)

	var p := JournalPage.new()
	p.build("WEEK 23")
	root.add_child(p)
	p.line("The demo worked until the investor touched it. It has never been touched before.")
	p.ask("The lead wants a board seat and your desk chair — what do you do?", [
		{"id": "yes", "text": "Give the chair"},
		{"id": "no", "text": "Keep the chair"},
		{"id": "stall", "text": "Form a committee"},
	])
	p.arrows(true, true)

	var t0 := Time.get_ticks_msec()
	for i in TIMES.size():
		var wait: float = float(TIMES[i]) - float(Time.get_ticks_msec() - t0) / 1000.0
		if wait > 0.0:
			await create_timer(wait).timeout
		await RenderingServer.frame_post_draw
		root.get_viewport().get_texture().get_image().save_png("%s/w_%02d.png" % [dir, i])

	# let it finish, then film the circle being drawn on a real click
	await create_timer(1.0).timeout
	var slot := _find_slot(p, "no")
	if slot != null:
		var ev := InputEventMouseButton.new()
		ev.button_index = MOUSE_BUTTON_LEFT
		ev.pressed = true
		slot.gui_input.emit(ev)
	for i in 5:
		await create_timer(0.055).timeout
		await RenderingServer.frame_post_draw
		root.get_viewport().get_texture().get_image().save_png("%s/c_%02d.png" % [dir, i])
	print("REVEAL PROBE DONE: %s" % dir)
	quit(0)

func _find_slot(p: JournalPage, id: String) -> Control:
	for row in p.space.get_children():
		for s in row.get_children():
			if s is Control and String((s as Control).get_meta("id", "")) == id:
				return s
	return null
