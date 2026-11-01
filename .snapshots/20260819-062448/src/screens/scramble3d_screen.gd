class_name Scramble3dScreen
extends Node3D
## Act 0 — The Leap, as a 2.5D paper diorama: hand-drawn flats standing in a 3D
## room, a springy chase camera, and a player sprite with real movement feel
## (acceleration, lean, hop-bob, squash on grab, shake on deposit).

signal done(result: Dictionary)   # {banked: [ids], left: [ids]}

const PALETTE := {
	"cream": Color("F2EAD3"), "ink": Color("1E1E1E"), "coral": Color("E86A5C"),
	"yellow": Color("F4B942"), "sage": Color("8FA582"), "blue": Color("6E8CA0"),
}
const DURATION := 60.0
const CAPACITY := 4
const ACCEL := 34.0
const FRICTION := 10.0
const MAX_SPEED := 7.2
const GRAB_RADIUS := 1.6
const ROOM := Vector2(16.0, 9.0)   # floor extent (x, z)

var rng: SeededRng
var item_defs: Array = []
var archetype: Dictionary = {}

var _player: Node3D
var _player_spr: Sprite3D
var _cam: Camera3D
var _cam_target: Vector3
var _shake := 0.0
var _vel := Vector3.ZERO
var _bob_t := 0.0
var _time_left := DURATION
var _carrying: Array = []
var _floor_items: Array = []     # {def, node}
var _banked: Array = []
var _door_pos := Vector3(7.4, 0.0, 0.0)
var _door_mesh: MeshInstance3D
var _hud: Label
var _hint: Label
var _vignette: ColorRect
var _over := false
var _tick_accum := 0.0
var _sfx: Dictionary = {}
var _frames: Dictionary = {}   # pose name -> Array[Texture2D]
var _grab_flash := 0.0
var _started := false

func _place_flat(sprite_name: String, height: float, pos: Vector3, billboard: bool, decal: bool) -> void:
	var path := "res://assets/sprites/%s.png" % sprite_name
	if not ResourceLoader.exists(path):
		return
	var tex: Texture2D = load(path)
	var s := Sprite3D.new()
	s.texture = tex
	s.pixel_size = height / float(tex.get_height())
	s.shaded = false
	if decal:
		s.rotation.x = -PI / 2.0
	elif billboard:
		s.billboard = BaseMaterial3D.BILLBOARD_FIXED_Y
	s.position = pos
	add_child(s)

func _load_frames() -> void:
	var sets := {
		"idle": ["chr_founder_idle_01", "chr_founder_idle_02"],
		"run": ["chr_founder_run_01", "chr_founder_run_02"],
		"carry": ["chr_founder_carry_01", "chr_founder_carry_02"],
		"grab": ["chr_founder_grab_01"],
	}
	for pose in sets:
		var texs: Array = []
		for n in sets[pose]:
			var p := "res://assets/sprites/%s.png" % n
			if ResourceLoader.exists(p):
				texs.append(load(p))
		if not texs.is_empty():
			_frames[pose] = texs

func _update_pose(speed_frac: float) -> void:
	if _frames.is_empty():
		return
	var pose := "idle"
	if _grab_flash > 0.0:
		_grab_flash -= get_process_delta_time()
		pose = "grab"
	elif speed_frac > 0.15:
		pose = "carry" if not _carrying.is_empty() and _frames.has("carry") else "run"
	var texs: Array = _frames.get(pose, _frames.get("idle", []))
	if texs.is_empty():
		return
	var idx := 0
	if texs.size() > 1:
		if pose == "idle":
			idx = int(Time.get_ticks_msec() / 380.0) % texs.size()   # boil pair
		else:
			idx = 0 if fmod(_bob_t, PI * 2.0) < PI else 1            # stride sync
	if _player_spr.texture != texs[idx]:
		_player_spr.texture = texs[idx]
		_player_spr.pixel_size = 1.35 / float((texs[idx] as Texture2D).get_height())

func setup(p_rng: SeededRng, p_items: Array) -> void:
	rng = p_rng
	item_defs = p_items

func _ready() -> void:
	# --- audio ---
	for n in ["pickup", "deposit", "tick", "step"]:
		var p := AudioStreamPlayer.new()
		p.stream = load("res://assets/sfx/%s.wav" % n)
		add_child(p)
		_sfx[n] = p
	# --- floor ---
	var floor_mesh := MeshInstance3D.new()
	var plane := PlaneMesh.new()
	plane.size = ROOM * 1.15
	floor_mesh.mesh = plane
	floor_mesh.material_override = _mat_tex("res://assets/env/floor.png", PALETTE["cream"], false)
	add_child(floor_mesh)
	# --- back wall (the stage backdrop) ---
	var wall := MeshInstance3D.new()
	var wall_quad := QuadMesh.new()
	wall_quad.size = Vector2(ROOM.x * 1.15, 6.2)
	wall.mesh = wall_quad
	wall.position = Vector3(0, 3.1, -ROOM.y / 2.0)
	wall.material_override = _mat_tex("res://assets/env/wall.png", PALETTE["sage"], false)
	add_child(wall)
	# --- side walls: plain cream flats for the diorama-box feel ---
	for sx in [-1.0, 1.0]:
		var side := MeshInstance3D.new()
		var sq := QuadMesh.new()
		sq.size = Vector2(ROOM.y, 5.0)
		side.mesh = sq
		side.rotation.y = PI / 2.0 * sx
		side.position = Vector3(sx * ROOM.x / 2.0 * 1.08, 2.5, 0)
		side.material_override = _mat_flat(Color("E8DCC0"))
		add_child(side)
	# --- door / deposit zone ---
	_door_mesh = MeshInstance3D.new()
	var dq := BoxMesh.new()
	dq.size = Vector3(1.2, 0.08, 3.2)
	_door_mesh.mesh = dq
	_door_mesh.position = _door_pos + Vector3(0, 0.04, 0)
	_door_mesh.material_override = _mat_flat(PALETTE["yellow"])
	add_child(_door_mesh)
	var door_frame := MeshInstance3D.new()
	var dfq := QuadMesh.new()
	dfq.size = Vector2(2.2, 3.4)
	door_frame.mesh = dfq
	door_frame.rotation.y = -PI / 2.0
	door_frame.position = Vector3(ROOM.x / 2.0 * 1.07, 1.7, 0)
	door_frame.material_override = _mat_flat(PALETTE["ink"])
	add_child(door_frame)
	# --- player: the real blob v2 cutout as a billboard flat ---
	_player = Node3D.new()
	_player.position = Vector3(-5.5, 0, 1.5)
	add_child(_player)
	_player_spr = Sprite3D.new()
	_load_frames()
	var char_tex: Texture2D = _frames.get("idle", [null])[0]
	if char_tex == null:
		char_tex = load("res://assets/sprites/founder.png")
	if char_tex:
		_player_spr.texture = char_tex
		_player_spr.pixel_size = 1.35 / float(char_tex.get_height())
	_player_spr.billboard = BaseMaterial3D.BILLBOARD_FIXED_Y
	_player_spr.shaded = false
	_player_spr.position.y = 0.72
	_player.add_child(_player_spr)
	# soft blob shadow
	var shadow := MeshInstance3D.new()
	var shq := PlaneMesh.new()
	shq.size = Vector2(0.9, 0.45)
	shadow.mesh = shq
	shadow.position.y = 0.02
	shadow.material_override = _mat_flat(Color(0.12, 0.12, 0.12, 0.25))
	_player.add_child(shadow)
	# --- the diorama dressing: wall-mounted, standing flats, floor decals ---
	var wall_z := -ROOM.y / 2.0 + 0.06
	var mounted := [
		["env_window", 2.3, Vector3(-4.6, 3.2, wall_z)],
		["env_mirror", 1.0, Vector3(-2.6, 3.2, wall_z)],
		["env_poster", 1.5, Vector3(-0.9, 3.4, wall_z)],
		["env_clock", 0.8, Vector3(0.7, 4.1, wall_z)],
		["env_shelf", 1.0, Vector3(2.2, 3.6, wall_z)],
		["env_calendar", 1.1, Vector3(3.8, 3.2, wall_z)],
		["env_whiteboard", 1.6, Vector3(5.7, 3.3, wall_z)],
		["env_fairy_lights", 1.2, Vector3(1.2, 5.1, wall_z)],
	]
	for m in mounted:
		_place_flat(m[0], m[1], m[2], false, false)
	var standing := [
		["env_bed", 1.5, Vector3(-6.8, 0, -3.3)],
		["env_couch", 1.5, Vector3(-4.2, 0, -3.6)],
		["env_lamp", 2.0, Vector3(-2.5, 0, -3.8)],
		["env_desk", 1.6, Vector3(1.6, 0, -3.5)],
		["env_kitchenette", 1.6, Vector3(4.4, 0, -3.4)],
		["env_stove", 1.4, Vector3(6.2, 0, -3.4)],
		["env_fridge", 2.1, Vector3(7.5, 0, -3.2)],
		["env_cupboard", 1.9, Vector3(-7.6, 0, -2.0)],
		["env_washing_machine", 1.3, Vector3(-7.5, 0, 0.4)],
		["env_tv", 1.1, Vector3(0.2, 0, -1.9)],
		["env_boxes", 1.3, Vector3(6.1, 0, 2.7)],
		["env_pizza", 0.8, Vector3(-2.5, 0, 0.9)],
		["env_dishes", 0.7, Vector3(-5.2, 0, 1.9)],
		["env_clothes", 0.6, Vector3(-0.6, 0, 2.7)],
		["env_trash", 0.8, Vector3(2.4, 0, -0.7)],
		["env_cables", 0.5, Vector3(1.2, 0, 1.8)],
		["env_books", 1.0, Vector3(-4.0, 0, -1.0)],
		["env_microwave", 0.7, Vector3(5.0, 0, -1.7)],
		["env_backpack", 0.7, Vector3(-1.8, 0, 3.5)],
		["env_sneakers", 0.5, Vector3(0.8, 0, 3.9)],
		["env_skateboard", 0.5, Vector3(3.4, 0, 3.3)],
		["env_kettle", 0.6, Vector3(4.8, 0, 0.7)],
		["env_dumbbell", 0.45, Vector3(-6.4, 0, 3.4)],
		["env_yoga_mat", 0.7, Vector3(-7.0, 0, 2.2)],
		["env_fan", 0.9, Vector3(2.9, 0, -2.5)],
		["env_box_scrawl", 0.8, Vector3(7.0, 0, 3.7)],
	]
	for f in standing:
		_place_flat(f[0], f[1], f[2] + Vector3(0, float(f[1]) / 2.0, 0), true, false)
	var decals := [
		["env_rug", 2.6, Vector3(0.0, 0.02, 0.6)],
		["env_welcome_mat", 1.3, Vector3(6.5, 0.02, 0.1)],
		["env_papers", 1.2, Vector3(-3.3, 0.02, 2.9)],
		["env_papers", 1.0, Vector3(2.0, 0.02, 2.4)],
	]
	for d in decals:
		_place_flat(d[0], d[1], d[2], false, true)
	# warm light pools (flat translucent discs — lamp glow + window moonlight)
	for pool in [[Vector3(-2.5, 0.01, -3.2), 1.5, Color(0.96, 0.73, 0.26, 0.20)], [Vector3(-4.6, 0.01, -2.6), 1.9, Color(0.43, 0.55, 0.63, 0.16)]]:
		var disc := MeshInstance3D.new()
		var cyl := CylinderMesh.new()
		cyl.top_radius = pool[1]
		cyl.bottom_radius = pool[1]
		cyl.height = 0.015
		disc.mesh = cyl
		disc.position = pool[0]
		disc.material_override = _mat_flat(pool[2])
		add_child(disc)
	if ResourceLoader.exists("res://assets/sprites/env_door.png"):
		var dtex: Texture2D = load("res://assets/sprites/env_door.png")
		var ds := Sprite3D.new()
		ds.texture = dtex
		ds.pixel_size = 3.2 / float(dtex.get_height())
		ds.rotation.y = -PI / 2.0
		ds.shaded = false
		ds.position = Vector3(ROOM.x / 2.0 * 1.06, 1.6, 0)
		add_child(ds)
		door_frame.visible = false
	if ResourceLoader.exists("res://assets/sprites/env_bathroom_door.png"):
		var btex: Texture2D = load("res://assets/sprites/env_bathroom_door.png")
		var bs := Sprite3D.new()
		bs.texture = btex
		bs.pixel_size = 2.6 / float(btex.get_height())
		bs.rotation.y = PI / 2.0
		bs.shaded = false
		bs.position = Vector3(-ROOM.x / 2.0 * 1.06, 1.3, 2.4)
		add_child(bs)
	# --- items as standing paper flats (spawned clear of the furniture row) ---
	for def in item_defs:
		var node := _make_item_flat(def)
		node.position = Vector3(
			rng.randf() * 13.6 - 7.0,
			0.0,
			rng.randf() * 6.6 - 2.5)
		add_child(node)
		_floor_items.append({"def": def, "node": node})
	# --- camera: angled down, spring-follows the player ---
	_cam = Camera3D.new()
	_cam.fov = 42
	add_child(_cam)
	_cam_target = _cam_pos_for(_player.position)
	_cam.position = _cam_target
	_cam.look_at(_player.position + Vector3(0, 0.6, 0))
	# --- 2D overlay HUD ---
	var overlay := CanvasLayer.new()
	add_child(overlay)
	_vignette = ColorRect.new()
	_vignette.set_anchors_preset(Control.PRESET_FULL_RECT)
	_vignette.color = Color(0.91, 0.42, 0.36, 0.0)
	_vignette.mouse_filter = Control.MOUSE_FILTER_IGNORE
	overlay.add_child(_vignette)
	_hud = _mk_label(overlay, 40, Vector2(28, 14))
	_hint = _mk_label(overlay, 26, Vector2(28, 970))
	_hint.text = "GRAB WHAT MATTERS. GET TO THE DOOR.   [WASD move · SPACE grab]"
	_hint.add_theme_color_override("font_color", PALETTE["blue"])
	_run_countdown(overlay)

func _run_countdown(overlay: CanvasLayer) -> void:
	var big := _mk_label(overlay, 200, Vector2(0, 0))
	big.set_anchors_preset(Control.PRESET_FULL_RECT)
	big.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	big.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	big.add_theme_color_override("font_color", PALETTE["coral"])
	for t in ["YOU QUIT YOUR JOB TONIGHT.", "3", "2", "1", "RUN!"]:
		big.text = t
		big.scale = Vector2(0.6, 0.6)
		big.pivot_offset = get_viewport().get_visible_rect().size / 2.0
		big.add_theme_font_size_override("font_size", 64 if t.length() > 4 else 200)
		var tw := create_tween()
		tw.tween_property(big, "scale", Vector2.ONE, 0.18).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
		if _sfx.has("tick"):
			_sfx["tick"].play()
		await get_tree().create_timer(1.0 if t.length() > 4 else 0.65).timeout
	big.queue_free()
	_started = true

func _mk_label(parent: Node, size: int, pos: Vector2) -> Label:
	var l := Label.new()
	l.position = pos
	l.add_theme_font_override("font", load("res://assets/fonts/PatrickHand-Regular.ttf"))
	l.add_theme_font_size_override("font_size", size)
	l.add_theme_color_override("font_color", PALETTE["ink"])
	parent.add_child(l)
	return l

func _mat_flat(c: Color) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = c
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	if c.a < 1.0:
		m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	return m

func _mat_tex(path: String, fallback: Color, transparent: bool) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	var tex := load(path)
	if tex:
		m.albedo_texture = tex
		if transparent:
			m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	else:
		m.albedo_color = fallback
	return m

func _make_item_flat(def: Dictionary) -> Node3D:
	var root := Node3D.new()
	var spr := Sprite3D.new()
	var tex := load("res://assets/sprites/%s.png" % String(def["id"]))
	if tex:
		spr.texture = tex
		spr.pixel_size = (0.75 + 0.2 * int(def.get("carry_cost", 1))) / float(tex.get_height())
	else:
		# fallback: colored quad placeholder
		var img := Image.create(64, 64, false, Image.FORMAT_RGBA8)
		var col: Color = PALETTE["sage"]
		if def.get("tags", []).has("liquid"): col = PALETTE["yellow"]
		elif def.get("tags", []).has("tech"): col = PALETTE["blue"]
		elif def.get("tags", []).has("vice"): col = PALETTE["coral"]
		img.fill(col)
		spr.texture = ImageTexture.create_from_image(img)
		spr.pixel_size = 0.011
	spr.billboard = BaseMaterial3D.BILLBOARD_FIXED_Y
	spr.shaded = false
	spr.position.y = 0.45
	root.add_child(spr)
	var shadow := MeshInstance3D.new()
	var shq := PlaneMesh.new()
	shq.size = Vector2(0.6, 0.3)
	shadow.mesh = shq
	shadow.position.y = 0.015
	shadow.material_override = _mat_flat(Color(0.12, 0.12, 0.12, 0.18))
	root.add_child(shadow)
	return root

func _cam_pos_for(target: Vector3) -> Vector3:
	return Vector3(target.x * 0.55, 7.2, target.z * 0.35 + 7.8)

func _process(delta: float) -> void:
	if _over or not _started:
		return
	_time_left -= delta
	if _time_left <= 0.0:
		_finish()
		return
	# movement with real acceleration/friction
	var dir := Vector3.ZERO
	if Input.is_key_pressed(KEY_W) or Input.is_key_pressed(KEY_UP): dir.z -= 1
	if Input.is_key_pressed(KEY_S) or Input.is_key_pressed(KEY_DOWN): dir.z += 1
	if Input.is_key_pressed(KEY_A) or Input.is_key_pressed(KEY_LEFT): dir.x -= 1
	if Input.is_key_pressed(KEY_D) or Input.is_key_pressed(KEY_RIGHT): dir.x += 1
	dir = dir.normalized()
	var speed_mult := float((archetype.get("scramble", {}) as Dictionary).get("speed_mult", 1.0))
	var slow := 1.0 - 0.07 * _carry_cost()
	if dir != Vector3.ZERO:
		_vel += dir * ACCEL * delta
	_vel -= _vel * FRICTION * delta
	_vel = _vel.limit_length(MAX_SPEED * speed_mult * slow)
	_player.position += _vel * delta
	_player.position.x = clampf(_player.position.x, -ROOM.x / 2.0 + 0.8, ROOM.x / 2.0 + 1.2)
	_player.position.z = clampf(_player.position.z, -2.9, ROOM.y / 2.0)   # furniture row is scenery
	# feel: hop-bob while moving, lean into velocity, sprite flip, pose frames
	var speed_frac := _vel.length() / MAX_SPEED
	_bob_t += delta * (3.0 + 11.0 * speed_frac)
	_player_spr.position.y = 0.72 + absf(sin(_bob_t)) * 0.14 * speed_frac
	_player_spr.rotation.z = -_vel.x * 0.035
	if absf(_vel.x) > 0.4:
		_player_spr.flip_h = _vel.x < 0
	_update_pose(speed_frac)
	if speed_frac > 0.4 and fmod(_bob_t, PI) < 0.1 and not _sfx["step"].playing:
		_sfx["step"].pitch_scale = 0.9 + rng.randf() * 0.25
		_sfx["step"].play()
	# camera spring + shake
	_cam_target = _cam_pos_for(_player.position)
	_cam.position = _cam.position.lerp(_cam_target, 1.0 - exp(-5.0 * delta))
	if _shake > 0.0:
		_shake = maxf(0.0, _shake - delta * 3.0)
		_cam.position += Vector3(rng.randf() - 0.5, rng.randf() - 0.5, 0) * _shake * 0.35
	_cam.look_at(_player.position * 0.7 + Vector3(0, 0.8, 0))
	# door pulse
	var pulse := 0.75 + 0.25 * sin(Time.get_ticks_msec() / 180.0)
	(_door_mesh.material_override as StandardMaterial3D).albedo_color = PALETTE["yellow"] * pulse
	# deposit
	if _player.position.distance_to(_door_pos) < 1.8 and not _carrying.is_empty():
		for def in _carrying:
			_banked.append(def)
		_carrying.clear()
		_shake = 0.6
		_sfx["deposit"].play()
	# HUD + endgame heartbeat
	var names := []
	for def in _carrying:
		names.append(String(def["name"]))
	var cap := CAPACITY + int((archetype.get("scramble", {}) as Dictionary).get("carry_bonus", 0))
	_hud.text = "%02d   ·   bag %d/%d   ·   %s" % [int(ceil(_time_left)), _carry_cost(), cap, ", ".join(names)]
	if _time_left < 10.0:
		_hud.add_theme_color_override("font_color", PALETTE["coral"])
		_vignette.color.a = 0.10 + 0.10 * sin(Time.get_ticks_msec() / 120.0)
		_tick_accum += delta
		if _tick_accum > (0.5 if _time_left > 5.0 else 0.25):
			_tick_accum = 0.0
			_sfx["tick"].play()

func _unhandled_input(event: InputEvent) -> void:
	if _over or not (event is InputEventKey and event.pressed):
		return
	if event.keycode == KEY_SPACE or event.keycode == KEY_E:
		_try_grab()

func _try_grab() -> void:
	var best := -1
	var best_d := GRAB_RADIUS
	for i in _floor_items.size():
		var d: float = _player.position.distance_to(_floor_items[i]["node"].position)
		if d < best_d:
			best_d = d
			best = i
	if best < 0:
		return
	var def: Dictionary = _floor_items[best]["def"]
	var capacity := CAPACITY + int((archetype.get("scramble", {}) as Dictionary).get("carry_bonus", 0))
	if _carry_cost() + int(def.get("carry_cost", 1)) > capacity:
		_hint.text = "HANDS FULL — drop at the DOOR!"
		return
	_sfx["pickup"].pitch_scale = 0.95 + 0.1 * rng.randf()
	_sfx["pickup"].play()
	_grab_flash = 0.22
	_carrying.append(def)
	# squash the player, pop the item toward them, then free it
	var node: Node3D = _floor_items[best]["node"]
	_floor_items.remove_at(best)
	var tw := create_tween()
	tw.tween_property(node, "position", _player.position + Vector3(0, 1.2, 0), 0.16).set_trans(Tween.TRANS_BACK)
	tw.parallel().tween_property(node, "scale", Vector3.ONE * 0.05, 0.16)
	tw.tween_callback(node.queue_free)
	var squash := create_tween()
	squash.tween_property(_player_spr, "scale", Vector3(1.18, 0.82, 1), 0.07)
	squash.tween_property(_player_spr, "scale", Vector3.ONE, 0.12).set_trans(Tween.TRANS_BOUNCE)

func _carry_cost() -> int:
	var c := 0
	for def in _carrying:
		c += int(def.get("carry_cost", 1))
	return c

func _finish() -> void:
	_over = true
	if _player.position.distance_to(_door_pos) < 2.2:
		for def in _carrying:
			_banked.append(def)
		_carrying.clear()
	var banked_ids: Array = []
	var left_ids: Array = []
	for def in _banked:
		banked_ids.append(String(def["id"]))
	for it in _floor_items:
		left_ids.append(String(it["def"]["id"]))
	for def in _carrying:
		left_ids.append(String(def["id"]))
	_show_tableau(banked_ids, left_ids)

func _show_tableau(banked_ids: Array, left_ids: Array) -> void:
	var overlay := CanvasLayer.new()
	add_child(overlay)
	var dim := ColorRect.new()
	dim.set_anchors_preset(Control.PRESET_FULL_RECT)
	dim.color = Color(0.12, 0.12, 0.12, 0.85)
	overlay.add_child(dim)
	var got_names: Array = []
	var left_names: Array = []
	for it in item_defs:
		if banked_ids.has(it["id"]): got_names.append(String(it["name"]))
		elif left_ids.has(it["id"]): left_names.append(String(it["name"]))
	var txt := _mk_label(overlay, 34, Vector2(420, 160))
	txt.add_theme_color_override("font_color", PALETTE["cream"])
	txt.text = "WHAT YOU GOT\n%s\n\nWHAT YOU LEFT\n%s\n\n[press any key]" % [
		"· " + "\n· ".join(got_names) if not got_names.is_empty() else "· nothing. bold.",
		"· " + "\n· ".join(left_names) if not left_names.is_empty() else "· nothing!"]
	set_process_unhandled_input(false)
	await get_tree().create_timer(0.8).timeout
	while Input.is_anything_pressed():
		await get_tree().process_frame
	while not Input.is_anything_pressed():
		await get_tree().process_frame
	done.emit({"banked": banked_ids, "left": left_ids})
