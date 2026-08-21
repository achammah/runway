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
var _layout: Dictionary = {}
var _marks: Dictionary = {}       # name -> {foot_x, foot_y, scale, ...}
var _occluders: Dictionary = {}   # name -> rect that draws OVER the cast
var _cast: Array = []             # the TextureRects currently standing in the room
var _ambient: TextureRect          # additive ambient-motion layer over the still
var _ambient_frames: Array = []

## PREFER THE WEBP MIRROR OF A PLATE.
##
## An anim frame, an ambient delta and a background are FULL-FRAME plates, not cutouts,
## so a lossy mirror costs nothing the eye can find and the exported .app drops several
## gigabytes: 4798 plates went from 6.3 GB of png to 325 MB of webp. Every png stays on
## disk for the tools — only the export filter leaves it behind — so whichever of the
## pair is present wins, webp first. Cutouts (layer_*, sprite, poses) keep their png:
## lossy alpha haloes a character standing over a room.
static func art_path(png_path: String) -> String:
	var webp := png_path.get_basename() + ".webp"
	return webp if ResourceLoader.exists(webp) else png_path

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
	# scene.png carries no cutouts, so it mirrors to webp with the anim plates.
	# room_bg.png does not: it is the plate the cast is composited ONTO, and a lossy
	# seam under a cutout is exactly the artefact this room cannot afford.
	var still := art_path(dir + "/scene.png")
	if ResourceLoader.exists(dir + "/room_bg.png"):
		base_path = dir + "/room_bg.png"
		layers_allowed = true
	elif ResourceLoader.exists(still):
		base_path = still
		layers_allowed = false
	elif not ResourceLoader.exists(art_path(dir + "/anim/frame_01.png")):
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
		var fp := art_path("%s/anim/frame_%02d.png" % [dir, i])
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
	if not FileAccess.file_exists(layout_path):
		return true
	var layout = JSON.parse_string(FileAccess.get_file_as_string(layout_path))
	if not (layout is Dictionary):
		return true
	_layout = layout
	for name in layout:
		var row: Dictionary = layout[name]
		if not bool(row.get("placed", true)):
			continue
		var kind := String(row.get("kind", ""))
		# A CREW MARK IS AN ANCHOR, NOT A PICTURE. It says where a character stands;
		# the cast is composited onto it by populate().
		if kind == "crew_mark":
			_marks[name] = row
			continue
		# An OCCLUDER is a crop of the room's own furniture that must draw OVER the
		# cast, so a character stands behind the desk instead of on it. It is held
		# back here and added after the cast in populate().
		if kind == "occluder":
			_occluders[name] = row
			continue
		# Legacy character cutouts only belong on an inpainted base; drawing them
		# over a full painting renders every character twice.
		if not layers_allowed:
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

## PUT THE CREW IN THE ROOM.
##
## The annotated stages are generated EMPTY on purpose — a room with crew painted
## into it shows those figures AND the composited sprites, which is the doubled-cast
## bug. So the cast is placed here, at the marks the scene declares, and the room
## looked empty until this existed.
##
## `crew` is [{sprite: "cast_hacker_fine", mood: "fine"}, ...]. The first entry takes
## founder_mark; the rest fill crew_1..crew_4 in order. Every sprite is FOOT-ANCHORED
## to the mark's foot point and scaled by the mark's own scale, because marks further
## back in the room are smaller — a uniform scale is what makes a composite read as
## pasted. Occluders are then re-added on top so the cast stands behind the furniture.
func populate(crew: Array) -> void:
	for c in _cast:
		if is_instance_valid(c):
			c.queue_free()
	_cast.clear()
	var order: Array = ["founder_mark", "crew_1", "crew_2", "crew_3", "crew_4"]
	var i := 0
	for spec in crew:
		if i >= order.size():
			break
		var mark_name: String = order[i]
		i += 1
		if not _marks.has(mark_name):
			continue
		var mark: Dictionary = _marks[mark_name]
		var sprite := String((spec as Dictionary).get("sprite", ""))
		var png := "res://assets/scenes/%s/sprite.png" % sprite
		if not ResourceLoader.exists(png):
			continue
		var tex: Texture2D = load(png)
		var sc := float(mark.get("scale", 1.0))
		var h := float(mark.get("h", 300.0)) * sc
		var w := h * (float(tex.get_width()) / maxf(float(tex.get_height()), 1.0))
		var tr := TextureRect.new()
		tr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		tr.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		tr.mouse_filter = Control.MOUSE_FILTER_IGNORE
		tr.texture = tex
		# foot-anchored: the mark names where the FEET land, not the top-left
		var fx := float(mark.get("foot_x", float(mark.get("x", 0.0)) + float(mark.get("w", 0.0)) * 0.5))
		var fy := float(mark.get("foot_y", float(mark.get("y", 0.0)) + float(mark.get("h", 0.0))))
		tr.position = Vector2(fx - w * 0.5, fy - h)
		tr.set_deferred("size", Vector2(w, h))
		tr.pivot_offset = Vector2(w * 0.5, h)      # squash and breathe from the feet
		add_child(tr)
		_layers[mark_name + "_cast"] = tr
		_cast.append(tr)
	_raise_occluders()
	# idle life, free and state-reactive: a burnt-out cofounder breathes slower
	var names: Array = []
	for k in _layers:
		if String(k).ends_with("_cast"):
			names.append(k)
	breathe(names)

## Occluders must sit above the cast, so they are (re)added last.
func _raise_occluders() -> void:
	var dir := "res://assets/scenes/%s" % scene_id
	for name in _occluders:
		var row: Dictionary = _occluders[name]
		var png := "%s/%s.png" % [dir, name]
		if not ResourceLoader.exists(png):
			continue
		var old_layer: TextureRect = _layers.get(name)
		if old_layer != null and is_instance_valid(old_layer):
			old_layer.queue_free()
		var tr := TextureRect.new()
		tr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		tr.stretch_mode = TextureRect.STRETCH_SCALE
		tr.mouse_filter = Control.MOUSE_FILTER_IGNORE
		tr.texture = load(png)
		tr.position = Vector2(float(row.get("x", 0)), float(row.get("y", 0)))
		var sz := Vector2(float(row.get("w", 0)), float(row.get("h", 0)))
		tr.set_deferred("size", sz)
		add_child(tr)
		_layers[name] = tr

## Which marks this scene offers — a screen can ask before it builds a crew.
func mark_count() -> int:
	return _marks.size()

## AMBIENT LIFE OVER A STILL — the bulb sways even when the room was generated.
##
## A composed scene is one image and therefore dead, while the pre-built stages breathe
## because they carry a 48-frame loop. Measured on stage_garage: only 0.9% of a loop's
## pixels ever change, in about a dozen places. The motion is LOCALISED, so it separates
## from the room: `tools/make_ambient.py` stores frame_i minus frame_0 as an additive
## delta, and adding that to ANY still of the same room reproduces exactly the light that
## moved and nothing else. Black adds nothing, so the still is untouched where nothing
## moved — and where the bulb brightens it also brightens a character standing under it,
## which is correct rather than a bug.
##
## The delta MUST come from the same room. Verified by laying the garage's delta over a
## hangar: the light lands in the wrong place and pokes a spike near a character's head.
func ambient(scene_for_motion: String = "") -> void:
	var src := scene_for_motion if scene_for_motion != "" else scene_id
	var dir := "res://assets/scenes/%s/ambient" % src
	_ambient_frames.clear()
	var i := 0
	while true:
		var fp := art_path("%s/d_%02d.png" % [dir, i])
		if not ResourceLoader.exists(fp):
			break
		_ambient_frames.append(load(fp))
		i += 1
	if _ambient_frames.is_empty():
		return
	if _ambient != null and is_instance_valid(_ambient):
		_ambient.queue_free()
	_ambient = TextureRect.new()
	_ambient.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_ambient.stretch_mode = TextureRect.STRETCH_SCALE
	_ambient.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var mat := CanvasItemMaterial.new()
	mat.blend_mode = CanvasItemMaterial.BLEND_MODE_ADD
	_ambient.material = mat
	_ambient.texture = _ambient_frames[0]
	_ambient.set_deferred("size", Vector2(1536, 1024))
	add_child(_ambient)
	set_process(true)

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
	_anim_t += delta
	if _ambient != null and is_instance_valid(_ambient) and not _ambient_frames.is_empty():
		var ai := int(_anim_t * ANIM_FPS) % _ambient_frames.size()
		_ambient.texture = _ambient_frames[ai]
	if _anim_frames.is_empty() or not _layers.has("anim"):
		return
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
		# A looping tween whose first step has ZERO duration makes Godot spin:
		# "ERROR: Infinite loop detected" out of tween.cpp, which froze full runs.
		# The stagger interval was 0.35*i, and i is 0 for the first layer.
		var tw := create_tween().set_loops()
		tw.tween_interval(0.35 * float(i) + 0.05)
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
