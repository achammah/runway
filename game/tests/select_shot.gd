extends SceneTree
## Photographs CHOOSE YOUR FOUNDER with the fourth founder picked, so the idle
## loop can be checked with an eye rather than a number: the whole character
## (head tuft to feet), standing on the spotlight beam, at the same size and on
## the same baseline as the other three.
## Run: RUNWAY_STRESS_DIR=<dir> godot --path . --script tests/select_shot.gd

func _shot(name: String) -> void:
	await RenderingServer.frame_post_draw
	var dir := OS.get_environment("RUNWAY_STRESS_DIR")
	if dir == "":
		dir = "."
	root.get_viewport().get_texture().get_image().save_png("%s/%s.png" % [dir, name])

func _init() -> void:
	call_deferred("_go")

func _go() -> void:
	await process_frame
	var content := ContentDb.new()
	content.load_all()
	var draft := FounderDraftScreen.new()
	draft.content_items = content.items.values()
	root.add_child(draft)
	draft.size = Vector2(1536, 1024)
	# the loops hydrate 6 frames per 0.05s tick — let them finish before picking
	await create_timer(1.8).timeout
	draft._show_page(1)
	await create_timer(0.6).timeout
	draft._select(3)
	await create_timer(1.2).timeout
	await _shot("select_norm_check")
	print("SELECT SHOT DONE")
	quit(0)
