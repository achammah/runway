extends SceneTree
## Photograph all three pages of HOW THIS WORLD WORKS — each one a baked loop
## in its film frame, its rule underneath, its page dot lit. Pages are turned
## through the real button, so the wiring is under test with the picture.
## The last press is taken too: it must write user://seen_howto and emit done.
## Run (windowed — headless renders nothing):
##     RUNWAY_STRESS_DIR=<dir> godot --path . --script tests/howto_shot.gd

var _font_alive: Font
var _dir := "/tmp"

func _init() -> void:
	call_deferred("_go")

func _shot(name: String) -> void:
	await RenderingServer.frame_post_draw
	root.get_viewport().get_texture().get_image().save_png("%s/%s.png" % [_dir, name])
	print("shot %s" % name)

func _go() -> void:
	await process_frame
	_font_alive = load("res://assets/fonts/PatrickHand-Regular.ttf")
	if OS.get_environment("RUNWAY_STRESS_DIR") != "":
		_dir = OS.get_environment("RUNWAY_STRESS_DIR")
	# a first start is the only time this screen exists: leave the mark as found
	var had := FileAccess.file_exists("user://seen_howto")
	if had:
		DirAccess.remove_absolute(ProjectSettings.globalize_path("user://seen_howto"))

	var ht := HowToScreen.new()
	root.add_child(ht)   # FULL_RECT anchors size it to the 1536x1024 window
	var fired := [false]
	ht.done.connect(func() -> void: fired[0] = true)
	await create_timer(1.0).timeout
	await _shot("howto_p1")
	ht._btn.pressed.emit()
	await create_timer(0.8).timeout
	await _shot("howto_p2")
	ht._btn.pressed.emit()
	await create_timer(0.8).timeout
	await _shot("howto_p3")
	ht._btn.pressed.emit()
	await create_timer(0.3).timeout
	print("HOWTO SHOT pages=%d done_emitted=%s seen=%s -> %s" % [
		ht._count, fired[0], HowToScreen.seen(), _dir])
	# leave the mark exactly as found, either way
	if had:
		var f := FileAccess.open("user://seen_howto", FileAccess.WRITE)
		f.store_string("1")
		f.close()
	else:
		DirAccess.remove_absolute(ProjectSettings.globalize_path("user://seen_howto"))
	quit(0)
