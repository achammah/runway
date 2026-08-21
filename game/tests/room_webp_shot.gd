extends SceneTree
## Photograph rooms that are served entirely from the webp mirrors, so a shrunk
## export is proven by the picture rather than by the byte count.
##
## Two rooms, because a room reaches the screen by two different routes:
##   garage_starving   a 48-frame anim loop plus an ambient delta set;
##   garage_thriving_v2  the flat scene.png still, no loop under it.
## A missing mirror shows up here as a black plate or a room with no light moving.
## Run: godot --path . --script tests/room_webp_shot.gd

func _init() -> void:
	call_deferred("_go")

func _go() -> void:
	await process_frame
	var out := OS.get_environment("RUNWAY_SHOT_DIR")
	if out == "":
		out = "/tmp"
	if not await _shoot("garage_starving", out + "/webp_room_check.png", true):
		quit(1)
		return
	if not await _shoot("garage_thriving_v2", out + "/webp_still_check.png", false):
		quit(1)
		return
	quit(0)

func _shoot(id: String, path: String, with_ambient: bool) -> bool:
	var dir := "res://assets/scenes/%s" % id
	print("--- %s ---" % id)
	print("  scene.png  resolves to: %s" % SceneRoom.art_path(dir + "/scene.png"))
	print("  frame_01   resolves to: %s" % SceneRoom.art_path(dir + "/anim/frame_01.png"))
	print("  ambient d_00 resolves to: %s" % SceneRoom.art_path(dir + "/ambient/d_00.png"))
	var room := SceneRoom.new()
	room.size = Vector2(1536, 1024)
	root.add_child(room)
	if not room.load_scene(id):
		print("ROOM WEBP SHOT FAIL — %s is not renderable" % id)
		return false
	if with_ambient:
		room.ambient()
	print("  anim frames: %d   ambient frames: %d" % [room._anim_frames.size(), room._ambient_frames.size()])
	await create_timer(1.5).timeout
	await RenderingServer.frame_post_draw
	root.get_viewport().get_texture().get_image().save_png(path)
	print("ROOM WEBP SHOT -> %s" % path)
	room.queue_free()
	await process_frame
	return true
