extends SceneTree
## Photograph the birth screen TWICE — once mid-arrival (the founder walking in on
## the taped-shut boxes) and once inside the unpacking loop it hands over to. One
## shot can only ever prove one of the two phases exists. Nothing else is in the
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
	var dir := OS.get_environment("RUNWAY_STRESS_DIR")
	if dir == "":
		dir = "/tmp"
	await create_timer(0.5).timeout   # mid-arrival, the fade-in already landed
	await _shot(dir + "/birth_intro_check.png")
	await create_timer(4.0).timeout   # 4.5s in: the arrival is spent, the loop runs
	await _shot(dir + "/birth_loop_check.png")
	print("BIRTH SHOT %s -> %s/birth_intro_check.png + birth_loop_check.png" % [b.size, dir])
	quit(0)

func _shot(path: String) -> void:
	await RenderingServer.frame_post_draw
	root.get_viewport().get_texture().get_image().save_png(path)
