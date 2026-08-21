extends SceneTree
## Photograph the curtain HELD SHUT — the state that plays the baked sway loop
## (assets/title/curtain_loop.png) with the considering line breathing over it.
## Nothing else is in the frame, so anything visible is the curtain's own doing.
## Run (windowed — headless renders nothing):
##     RUNWAY_STRESS_DIR=<dir> godot --path . --script tests/curtain_shot.gd

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
	var c := Curtain.new()
	root.add_child(c)   # FULL_RECT anchors size it to the 1536x1024 window
	c.close()   # not awaited: the shot wants the held-shut state, not the sweep
	# past the 0.9s mark so the considering line has faded up over the loop
	await create_timer(2.5).timeout
	await RenderingServer.frame_post_draw
	var dir := OS.get_environment("RUNWAY_STRESS_DIR")
	if dir == "":
		dir = "/tmp"
	root.get_viewport().get_texture().get_image().save_png(dir + "/curtain_loop_check.png")
	print("CURTAIN SHOT %s -> %s/curtain_loop_check.png" % [c.size, dir])
	quit(0)
