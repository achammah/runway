class_name SceneRoom
extends Control
## Shared loader for scene-first rooms (owned by MAIN; lanes instantiate, never edit).
## Mounts game/assets/scenes/<scene_id>/: room_bg.png full-frame + every layout.json
## layer at its matched coordinates. Layers become addressable children so callers
## can swap/hide/animate them (money tiers, crew moods, decay) without breaking
## the composed look.
##
## Usage:
##   var room := SceneRoom.new()
##   room.load_scene("garage_steady")        # or any id in assets/scenes/
##   add_child(room)
##   room.get_layer("money").visible = false
##   room.swap_layer("crew_vest", "res://assets/scenes/garage_starving/crew_vest.png")
##   room.breathe(["founder", "crew_vest"])  # idle life on characters

var scene_id := ""
var _layers: Dictionary = {}   # name -> TextureRect
var _anim_frames: Array = []   # full-scene loop frames (owner mandate: scenes feel alive)
var _anim_t := 0.0
const ANIM_FPS := 12.0

func load_scene(p_scene_id: String) -> bool:
	scene_id = p_scene_id
	for c in get_children():
		c.queue_free()
	_layers.clear()
	var dir := "res://assets/scenes/%s" % scene_id
	# A scene is renderable from ANY of: the inpainted base (layers can move on
	# top of it), the flat still, or its animation loop. Requiring room_bg alone
	# silently rejected a dozen produced scenes.
	# WHICH BASE, AND WHETHER LAYERS MAY BE DRAWN ON IT.
	# room_bg.png is the INPAINTED plate: the cast has been painted out of it, so the
	# cutouts belong on top. scene.png is the FULL composed painting and already
	# contains everyone. Drawing the cutouts over scene.png renders every character
	# TWICE — that is the doubled crew and the ghost cowlicks in the room.
	var base_path := ""
	var layers_allowed := false
	if ResourceLoader.exists(dir + "/room_bg.png"):
		base_path = dir + "/room_bg.png"
		layers_allowed = true
	elif ResourceLoader.exists(dir + "/scene.png"):
		base_path = dir + "/scene.png"
		layers_allowed = false
	elif not ResourceLoader.exists(dir + "/anim/frame_01.png"):
		push_warning("SceneRoom: nothing renderable for " + scene_id)
		return false
	size = Vector2(1536, 1024)
	if base_path != "":
		var bg := TextureRect.new()
		bg.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		bg.stretch_mode = TextureRect.STRETCH_SCALE
		bg.texture = load(base_path)
		bg.size = Vector2(1536, 1024)
		bg.set_deferred("size", Vector2(1536, 1024))
		add_child(bg)
		_layers["room_bg"] = bg
	# full-scene animation loop: if anim frames exist, they play as the WHOLE
	# scene (composed video loop) above the layered set; layers stay for state
	# swaps drawn over it only when the loop is absent.
	_anim_frames.clear()
	var i := 1
	while true:
		var fp := "%s/anim/frame_%02d.png" % [dir, i]
		if not ResourceLoader.exists(fp):
			break
		_anim_frames.append(load(fp))
		i += 1
	if not _anim_frames.is_empty():
		var av := TextureRect.new()
		av.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		av.stretch_mode = TextureRect.STRETCH_SCALE
		av.texture = _anim_frames[0]
		av.size = Vector2(1536, 1024)
		av.set_deferred("size", Vector2(1536, 1024))
		add_child(av)
		_layers["anim"] = av
		set_process(true)
	# A full-scene animation loop is the whole picture too: layers over it double
	# the cast exactly the way layers over scene.png do.
	if not _anim_frames.is_empty():
		layers_allowed = false
	var layout_path := dir + "/layout.json"
	if not layers_allowed or not FileAccess.file_exists(layout_path):
		return true
	var layout = JSON.parse_string(FileAccess.get_file_as_string(layout_path))
	if not (layout is Dictionary):
		return true
	for name in layout:
		var row: Dictionary = layout[name]
		if not bool(row.get("placed", true)):
			continue
		var png := "%s/%s.png" % [dir, name]
		if not ResourceLoader.exists(png):
			continue
		var tr := TextureRect.new()
		tr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		tr.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		tr.position = Vector2(float(row["x"]), float(row["y"]))
		var sz := Vector2(float(row["w"]), float(row["h"]))
		tr.texture = load(png)
		tr.size = sz
		tr.set_deferred("size", sz)
		tr.pivot_offset = Vector2(sz.x / 2.0, sz.y)   # feet-anchored for squash
		add_child(tr)
		_layers[name] = tr
	return true

## Darken the room so something laid over it (the log book) reads as the subject.
## The owner asked for exactly this: "the paper log should come simply on top of a
## scene and we need to have a dark overlay on scene". Call after load_scene().
func dim(amount: float = 0.45) -> void:
	var v := ColorRect.new()
	v.name = "dim_veil"
	v.color = Color(0.06, 0.05, 0.07, clampf(amount, 0.0, 1.0))
	v.mouse_filter = Control.MOUSE_FILTER_IGNORE
	v.size = Vector2(1536, 1024)
	v.set_deferred("size", Vector2(1536, 1024))
	add_child(v)
	move_child(v, get_child_count() - 1)   # above the room, below whatever comes next

func _process(delta: float) -> void:
	if _anim_frames.is_empty() or not _layers.has("anim"):
		return
	_anim_t += delta
	var idx := int(_anim_t * ANIM_FPS) % _anim_frames.size()
	(_layers["anim"] as TextureRect).texture = _anim_frames[idx]

func get_layer(name: String) -> TextureRect:
	return _layers.get(name)

func has_layer(name: String) -> bool:
	return _layers.has(name)

## Swap a layer's art in place (state changes: money tier, crew mood, decay variant).
func swap_layer(name: String, png_path: String) -> void:
	var tr: TextureRect = _layers.get(name)
	if tr and ResourceLoader.exists(png_path):
		var sz := tr.size
		tr.texture = load(png_path)
		tr.set_deferred("size", sz)

## Gentle idle breathing on named layers (characters) — composed scenes stay alive.
func breathe(names: Array) -> void:
	var i := 0
	for name in names:
		var tr: TextureRect = _layers.get(name)
		if tr == null:
			continue
		var tw := create_tween().set_loops()
		tw.tween_interval(0.35 * i)
		tw.tween_property(tr, "scale", Vector2(1.004, 0.988), 1.15).set_trans(Tween.TRANS_SINE)
		tw.tween_property(tr, "scale", Vector2.ONE, 1.15).set_trans(Tween.TRANS_SINE)
		i += 1

## Pop a layer (drop-in on state change).
func pop_layer(name: String) -> void:
	var tr: TextureRect = _layers.get(name)
	if tr == null:
		return
	var tw := create_tween()
	tw.tween_property(tr, "scale", Vector2(1.08, 0.92), 0.08)
	tw.tween_property(tr, "scale", Vector2.ONE, 0.14).set_trans(Tween.TRANS_BOUNCE)
