extends SceneTree
## Photograph the birth screen — the baked unpacking loop full-frame, the painted
## RUNWAY! logotype over it, the status line at the bottom. Nothing else is in the
## tree, so anything visible is the screen's own doing.
## Run (windowed — headless renders nothing):
##     RUNWAY_STRESS_DIR=<dir> godot --path . --script tests/birth_shot.gd

## _draw loads the font per frame and drops the reference; in the game a dozen
## screens hold one so the cached FontFile stays alive. Alone in this tree it
## would be freed every frame and the line would photograph as white blocks —
## so the harness holds the reference the game always has.
var _font_alive: Font

func _init() -> void:
	call_deferred("_go")

func _go() -> void:
	await process_frame
	_font_alive = load("res://assets/fonts/PatrickHand-Regular.ttf")
	var b := BirthScreen.new()
	root.add_child(b)
	b.size = Vector2(1536, 1024)   # the window, in case the preset has not settled
	await create_timer(1.2).timeout   # a few loop frames in, the line faded up
	await RenderingServer.frame_post_draw
	var dir := OS.get_environment("RUNWAY_STRESS_DIR")
	if dir == "":
		dir = "/tmp"
	root.get_viewport().get_texture().get_image().save_png(dir + "/birth_check.png")
	print("BIRTH SHOT %s -> %s/birth_check.png" % [b.size, dir])
	quit(0)
