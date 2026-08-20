class_name DiceCup
extends Control
## THE TABLE ROLL (owner: "a cup shaken then put on the ground and dice rolling,
## like 3D effect with D&D dice"). Plays ON THE ROOM, before the curtain:
## a drawn dice cup slides in and rattles, tips, and a REAL 3D d20 tumbles out
## under physics, bounces, settles — and the rolled number stamps onto it in the
## founder's hand. The physical face is irrelevant on purpose: the inked number
## IS the result, which is exactly how this art style tells the truth.
##
## Usage (MAIN owns it):
##   var dc := DiceCup.new()
##   add_child(dc)
##   await dc.roll(17)     # plays the whole ceremony, ~2.6s
##   dc.queue_free()

signal settled

const CREAM := Color("F2EAD3")
const INK := Color("1E1E1E")
const COR := Color("E86A5C")

var _vp: SubViewport
var _die: RigidBody3D
var _cam: Camera3D
var _n := 0
var _phase := ""          ## shake -> pour -> tumble -> stamped
var _t := 0.0
var _cup_angle := 0.0
var _cup_x := 0.0
var _stamp_pos := Vector2.ZERO
var _stamp_scale := 0.0
var _rattle: AudioStreamPlayer

func _init() -> void:
	set_anchors_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP   # the ceremony owns the click for its beat

func _ready() -> void:
	# ── the 3D table inside a transparent viewport ──
	_vp = SubViewport.new()
	_vp.size = Vector2i(900, 700)
	_vp.transparent_bg = true
	_vp.own_world_3d = true
	add_child(_vp)
	var world := Node3D.new()
	_vp.add_child(world)
	_cam = Camera3D.new()
	_cam.position = Vector3(0, 5.2, 3.4)
	_cam.rotation_degrees = Vector3(-56, 0, 0)
	_cam.fov = 38
	world.add_child(_cam)
	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-55, -30, 0)
	sun.light_energy = 1.25
	world.add_child(sun)
	var fill := DirectionalLight3D.new()
	fill.rotation_degrees = Vector3(-30, 140, 0)
	fill.light_energy = 0.5
	world.add_child(fill)
	var floor_body := StaticBody3D.new()
	var floor_shape := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size = Vector3(20, 0.4, 20)
	floor_shape.shape = box
	floor_body.add_child(floor_shape)
	floor_body.position = Vector3(0, -0.2, 0)
	world.add_child(floor_body)
	# walls keep an unlucky bounce inside the frame
	for wx in [-3.2, 3.2]:
		var wb := StaticBody3D.new()
		var ws := CollisionShape3D.new()
		var wsh := BoxShape3D.new()
		wsh.size = Vector3(0.4, 6, 20)
		ws.shape = wsh
		wb.add_child(ws)
		wb.position = Vector3(wx, 2, 0)
		world.add_child(wb)
	for wz in [-2.6, 2.2]:
		var wb2 := StaticBody3D.new()
		var ws2 := CollisionShape3D.new()
		var wsh2 := BoxShape3D.new()
		wsh2.size = Vector3(20, 6, 0.4)
		ws2.shape = wsh2
		wb2.add_child(ws2)
		wb2.position = Vector3(0, 2, wz)
		world.add_child(wb2)

	_die = RigidBody3D.new()
	var verts := _icosahedron(0.62)
	var shape := ConvexPolygonShape3D.new()
	shape.points = verts
	var cs := CollisionShape3D.new()
	cs.shape = shape
	_die.add_child(cs)
	var mi := MeshInstance3D.new()
	mi.mesh = _ico_mesh(verts)
	_die.add_child(mi)
	_die.position = Vector3(0, 6, 0)
	_die.freeze = true
	_die.physics_material_override = PhysicsMaterial.new()
	_die.physics_material_override.bounce = 0.42
	_die.physics_material_override.friction = 0.85
	world.add_child(_die)

	# the viewport's picture, over the room, bottom-centre table area
	var tr := TextureRect.new()
	tr.texture = _vp.get_texture()
	tr.mouse_filter = Control.MOUSE_FILTER_IGNORE
	tr.position = Vector2(318, 260)
	tr.set_deferred("size", Vector2(900, 700))
	add_child(tr)

	if FileAccess.file_exists("res://assets/sfx/dice_rattle.wav"):
		_rattle = AudioStreamPlayer.new()
		_rattle.stream = load("res://assets/sfx/dice_rattle.wav")
		_rattle.volume_db = -6.0
		add_child(_rattle)
	set_process(true)

## The whole ceremony. n is the number the die will be inked with.
func roll(n: int) -> void:
	_n = clampi(n, 1, 20)
	_phase = "shake"
	_t = 0.0
	if _rattle != null:
		_rattle.play()
	await settled

func _process(delta: float) -> void:
	_t += delta
	match _phase:
		"shake":
			_cup_angle = sin(_t * 34.0) * 0.24
			_cup_x = sin(_t * 27.0) * 10.0
			if _t > 0.85:
				_phase = "pour"
				_t = 0.0
		"pour":
			_cup_angle = lerpf(_cup_angle, -1.9, minf(_t * 6.0, 1.0))
			if _t > 0.22 and _die.freeze:
				_die.freeze = false
				_die.linear_velocity = Vector3(randf_range(-1.2, 1.2), -2.5, randf_range(2.2, 3.4))
				_die.angular_velocity = Vector3(randf_range(-14, 14), randf_range(-14, 14), randf_range(-14, 14))
			if _t > 0.5:
				_phase = "tumble"
				_t = 0.0
		"tumble":
			if _t > 0.55 and _die.linear_velocity.length() < 0.25 and _die.angular_velocity.length() < 0.6:
				_phase = "stamped"
				_t = 0.0
			elif _t > 2.4:
				_phase = "stamped"   # never wait on a spinning outlier
				_t = 0.0
		"stamped":
			_stamp_scale = minf(_stamp_scale + delta * 6.0, 1.0)
			if _t > 0.9:
				_phase = "done"
				settled.emit()
	if _phase in ["stamped", "done"] and _die != null:
		var p3 := _die.position + Vector3(0, 0.7, 0)
		_stamp_pos = Vector2(318, 260) + _cam.unproject_position(p3) * Vector2(900, 700) / Vector2(_vp.size)
	queue_redraw()

func _draw() -> void:
	# the CUP, drawn: a coral beaker with ink outline, held at the table's edge
	if _phase in ["shake", "pour"]:
		var base := Vector2(size.x * 0.5 + _cup_x, size.y * 0.40)
		draw_set_transform(base, _cup_angle, Vector2.ONE)
		var cup := PackedVector2Array([Vector2(-52, -80), Vector2(52, -80), Vector2(38, 40), Vector2(-38, 40)])
		draw_colored_polygon(cup, COR)
		var rng := RandomNumberGenerator.new()
		rng.seed = 3
		var outline := PackedVector2Array()
		for i in cup.size() + 1:
			var p := cup[i % cup.size()]
			outline.append(p + Vector2(rng.randf_range(-2, 2), rng.randf_range(-2, 2)))
		draw_polyline(outline, INK, 5.0, true)
		draw_line(Vector2(-52, -80), Vector2(52, -80), INK, 7.0, true)
		draw_set_transform(Vector2.ZERO, 0.0, Vector2.ONE)
	# the NUMBER, inked onto the settled die in the founder's hand
	if _phase in ["stamped", "done"] and _stamp_scale > 0.01:
		var f: Font = load("res://assets/fonts/PatrickHand-Regular.ttf")
		var num := str(_n)
		var sz := int(74.0 * (1.0 + (1.0 - _stamp_scale) * 0.8))
		var s := f.get_string_size(num, HORIZONTAL_ALIGNMENT_LEFT, -1, sz)
		draw_string(f, _stamp_pos - Vector2(s.x * 0.5, -s.y * 0.3), num,
				HORIZONTAL_ALIGNMENT_LEFT, -1, sz, Color(INK, _stamp_scale))

## A unit icosahedron's 12 vertices, scaled.
func _icosahedron(r: float) -> PackedVector3Array:
	var phi := (1.0 + sqrt(5.0)) / 2.0
	var pts := PackedVector3Array()
	for s1 in [-1.0, 1.0]:
		for s2 in [-1.0, 1.0]:
			pts.append(Vector3(0, s1, s2 * phi))
			pts.append(Vector3(s1, s2 * phi, 0))
			pts.append(Vector3(s1 * phi, 0, s2))
	var out := PackedVector3Array()
	for p in pts:
		out.append(p.normalized() * r)
	return out

## Flat-shaded convex hull mesh of those vertices — reads as a faceted d20.
func _ico_mesh(verts: PackedVector3Array) -> ArrayMesh:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var mat := StandardMaterial3D.new()
	mat.albedo_color = CREAM
	mat.roughness = 0.95
	st.set_material(mat)
	# convex hull triangulation: every outward-facing triple (small N, brute force)
	var n := verts.size()
	for i in n:
		for j in range(i + 1, n):
			for k in range(j + 1, n):
				var a := verts[i]; var b := verts[j]; var c := verts[k]
				var nrm := (b - a).cross(c - a)
				if nrm.length() < 0.05:
					continue
				var outward := true
				for m in n:
					if m == i or m == j or m == k:
						continue
					if nrm.dot(verts[m] - a) > 0.001:
						outward = false
						break
				if outward:
					if nrm.dot(a) < 0.0:
						var tmp := b; b = c; c = tmp
						nrm = -nrm
					st.set_normal(nrm.normalized())
					st.add_vertex(a)
					st.set_normal(nrm.normalized())
					st.add_vertex(b)
					st.set_normal(nrm.normalized())
					st.add_vertex(c)
	return st.commit()
