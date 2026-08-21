class_name SceneStage
extends Control
## THE INSTANTANEOUS RUNTIME — docs/BLANK_SCENES_ARCHITECTURE.md §2 and §3.
##
## Everything expensive already happened offline: the blank scene is painted, the pose
## sprites are cut and keyed, the slots are typed. At runtime this class does a
## dictionary lookup and a draw call, so putting three cofounders in a room costs
## nothing and takes no time. The DM never names a slot or a pose id, so there is
## nothing for it to hallucinate — it emits free text and the table below decides.
##
##   var stage := SceneStage.new()
##   add_child(stage)
##   var ok := stage.build("legit_workspace/small_office/day_steady_wide", [
##       {"who": "founder_pm", "doing": "typing all night",        "mood": "burnt"},
##       {"who": "cofd_tech",  "doing": "soldering the prototype", "mood": "fine"},
##       {"who": "cofd_sales", "doing": "calls investors",         "mood": "fine"},
##   ])
##   if not ok:
##       ...   # a genuine scene miss: the caller falls back (SceneRoom, novel room)
##
## THE CONTRACT IS THAT NOTHING BREAKS (§3). Every lookup degrades exactly one rung
## and logs exactly ONE push_warning:
##   unknown `doing`      → stand_neutral
##   no matching slot     → a plain stand slot; still none → the character is DROPPED
##                          from the frame, because a mis-posed character is worse
##                          than an absent one
##   missing pose sprite  → the character's canonical standing sprite
##   missing pose meta    → seat/feet inferred from the pose id, and no blink
##   missing slots.json   → the crew marks in annotations.json, as stand slots
##   missing ambient      → a still room (alive-ness lost, nothing broken)
##   missing scene        → build() returns false. That is the ONLY false.

# ── the frame ────────────────────────────────────────────────────────────────
const CANVAS := Vector2(1536.0, 1024.0)
const INK := Color(0.118, 0.118, 0.118)      # #1E1E1E, the constitution's only ink
const AMBIENT_FPS := 12.0
const BLINK_HOLD := 0.12                     # a lid is down for 0.12s and no longer
const SEAT_RATIO := 0.55                     # a seat anchor sits 55% down the sprite
const DEFAULT_POSE := "stand_neutral"

## Poses whose whole point is the mood. When a character has to be moved to a plain
## stand slot, these keep their slump instead of snapping back to neutral — the
## narration said the character was falling apart, and the frame must not disagree.
const DESPAIR_POSES: Array = ["sit_couch_headinhands", "sit_desk_slumped", "stand_slumped"]

## ACTIVITY → POSE CLASS. Free text in, one of the canonical 24 out.
##
## Rows are tried IN ORDER and the first row with a matching substring wins, so the
## order encodes the disambiguation the architecture doc calls for: "pitches on stage"
## must reach stand_mic before the bare "pitch" in stand_phone can claim it, and
## "watches the demo" must reach the audience before "demo" claims it for the
## presenter. Each row is [pose, any-of, none-of]; `none-of` exists only for the
## substrings that lie ("network" is not "work").
const ACTIVITY_TABLE: Array = [
	["lie_hospital", ["hospital", "ambulance", "collaps", "bedridden", "iv drip",
		"the er", "intensive care", "stretcher"], []],
	["sleep_desk", ["sleep", "asleep", "nap", "passed out", "doz", "snor",
		"crashed at", "out cold"], []],
	["sit_bed", ["in bed", "on the bed", "wakes up", "waking up", "bedroom",
		"under the covers"], []],
	# The watcher must be found before the presenter, or "watches the demo" puts the
	# audience on the stage.
	["sit_audience_neutral", ["watch", "attend", "listen", "observ", "sits through",
		"spectat", "in the audience", "sits in on", "takes notes from"],
		["present", "demo the", "gives the"]],
	["stand_mic", ["on stage", "onstage", "demo day", "keynote", "microphone", " mic",
		"speech", "addresses the", "to the audience", "town hall", "podium", "panel",
		"pitches to the room"], []],
	["stand_present_pointer", ["present", "demo", "whiteboard", "slide", "deck",
		"walks through", "explains", "diagram", "draws the", "chart"], []],
	["stand_phone", ["call", "phone", "dial", "rings up", "pitch", "negotiat",
		"investor", "voicemail", "hangs up"], []],
	["sit_couch_headinhands", ["despair", "cry", "cries", "sobs", "breaks down",
		"head in hands", "grieves", "gives up", "melts down", "spiral", "weeps"], []],
	["stand_wave_celebrate", ["celebrat", "cheer", "high five", "high-five", "toast",
		"champagne", "party", "whoop", "fist pump", "victory", "rejoic", "waves"], []],
	["sit_audience_clapping", ["clap", "applau", "ovation"], []],
	["stand_reading_paper", [" read", "reads", "reading", "term sheet", "lawsuit", "letter", "contract",
		"notice", "memo", "the report", "the filing", "studies the", "reviews the",
		"subpoena"], []],
	["crouch_pack", ["pack", "boxes up", "tapes up", "clears the desk", "shreds",
		"crates"], []],
	["stand_carrybox", ["carr", "hauls", "moves out", "moving out", "lugs",
		"the box", "cardboard"], []],
	["stand_handshake_L", ["handshake", "shakes hands", "shaking hands",
		"signs the deal", "closes the deal", "greets", "welcomes", "seals",
		"works the room", "network"], []],
	["stand_point_accuse", ["accus", "blames", "points at", "argu", "confront",
		"yells", "shouts", "fires ", "storms"], []],
	["stand_armscrossed", ["arms crossed", "skeptic", "sceptic", "doubt", "disapprov",
		"refuses", "stands firm", "waits it out", "judges", "stonewall"], []],
	["stand_writing_clipboard", ["clipboard", "takes notes", "note", "checklist",
		"inventor", "audit", "inspect", "interview", "signs off", "tally"], []],
	["stand_coffee", ["coffee", "espresso", "caffeine", "sips", "break room",
		"kettle", "mug"], []],
	["sit_couch_relaxed", ["couch", "sofa", "relax", "lounge", "chills", "rests",
		"unwind"], []],
	["walk_stride", ["walk", "paces", "pacing", "strides", "rushes", "runs",
		"hurries", "arrives", "heads out", "leaves"], []],
	["sit_desk_slumped", ["stares at", "staring at", "zones out", "burnt out",
		"burned out", "numb", "blank screen"], []],
	["sit_desk_typing", ["type", "typing", "cod", "ship", "program", "debug",
		"commit", "refactor", "keyboard", "laptop", "hack", "builds", "building",
		"writes", "spreadsheet", "email", "solder", "prototype", "tinker", "wires",
		"fixes", "bug", "work"], ["network", "homework", "framework"]],
]

# ── test seams (roots, so a suite can run entirely off user:// fixtures) ──────
## A synthetic slots table. Accepts {"slots":[…]} for the scene being built, or the
## full {scene_id: {"slots":[…]}} table. Non-empty means slots.json is not read.
var _slots_override: Dictionary = {}
var _bg_root := "res://assets/backgrounds"
var _poses_root := "res://assets/poses"
var _cast_root := "res://assets/scenes"

# ── state ────────────────────────────────────────────────────────────────────
var scene_id := ""
var scene_file := ""                        # the flat filename the index resolved to
var _scene_path := ""
var _scene_tex: Texture2D = null
var _scene_img: Image = null
var _scene_img_tried := false
var _placements: Array = []                 # [{who, mood, doing, pose, slot_id, source, layer, blink, blink_at}]
var _dropped: Array = []                    # [{who, pose, reason}]
var _ambient: TextureRect = null
var _ambient_frames: Array = []
var _t := 0.0
var _life_pending := false
var _rng := RandomNumberGenerator.new()

## Parsed json lives here for the whole session, keyed by path: annotations.json is
## 2.3 MB and a room can be rebuilt every week of a run.
static var _json_cache: Dictionary = {}


func _init() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE   # a backdrop never eats a click


func _ready() -> void:
	if _life_pending:
		_begin_life()


# ═════════════════════════════════════════════════════════════════════════════
# THE ONE PUBLIC CALL
# ═════════════════════════════════════════════════════════════════════════════

## Assemble `scene_id` with `cast` = [{who, doing, mood}, …].
## Returns false ONLY when the scene itself is a miss, so the caller can fall back.
func build(p_scene_id: String, cast: Array) -> bool:
	_reset()
	scene_id = p_scene_id
	_rng.seed = p_scene_id.hash()      # blink cadence is stable for a given room

	# 1. SCENE. The id is not the filename (ids are slash-separated, the library is
	# flat), and an absent index key IS the miss. Guessing a path is how a cast ends
	# up standing in a room the story never mentioned, so this never guesses.
	var path := _resolve_scene(p_scene_id)
	if path == "":
		push_warning("SceneStage: '%s' is not in the background index — caller must fall back" % p_scene_id)
		return false
	_scene_path = path
	_scene_tex = _texture(path)
	if _scene_tex == null:
		push_warning("SceneStage: background '%s' did not decode — caller must fall back" % path)
		return false

	var frame := Vector2(float(_scene_tex.get_width()), float(_scene_tex.get_height()))
	if frame.x < 1.0 or frame.y < 1.0:
		frame = CANVAS
	size = frame
	set_deferred("size", frame)
	_draw_scene(frame)

	# 2 + 3. Activity → pose, then deterministic slot assignment.
	var plan := _assign(cast, _slots_for(p_scene_id))
	# 4 + 6. Draw each placement, graded to the room it stands in.
	for row in plan:
		_place(row as Dictionary, frame)
	# 5. Life, all free.
	_start_ambient()
	_begin_life()
	return true


## The pose layers currently in the frame, in placement order.
func pose_layers() -> Array:
	var out: Array = []
	for row in _placements:
		var layer: TextureRect = (row as Dictionary).get("layer")
		if layer != null and is_instance_valid(layer):
			out.append(layer)
	return out


## What was placed, and how: [{who, mood, doing, pose, slot_id, source}].
## `source` is "pose" (the pose library) or "canonical" (the degraded sprite).
func placements() -> Array:
	var out: Array = []
	for row in _placements:
		var r: Dictionary = row
		out.append({
			"who": r.get("who", ""), "mood": r.get("mood", ""),
			"doing": r.get("doing", ""), "pose": r.get("pose", ""),
			"slot_id": r.get("slot_id", ""), "source": r.get("source", ""),
		})
	return out


## Who could not be placed, and why. Never an error — a dropped character is the
## designed floor of the degrade ladder.
func dropped() -> Array:
	return _dropped.duplicate(true)


## Free text → one of the canonical 24, with the mood folded in (never multiplied).
static func pose_for(doing: String, mood: String = "fine") -> String:
	return apply_mood(pose_for_activity(doing), mood)


## Free text → one of the canonical 24. Unknown verbs are stand_neutral, always.
static func pose_for_activity(doing: String) -> String:
	var t := doing.to_lower()
	if t.strip_edges() == "":
		return DEFAULT_POSE
	for row in ACTIVITY_TABLE:
		var r: Array = row
		var blocked := false
		for bad in (r[2] as Array):
			if t.contains(String(bad)):
				blocked = true
				break
		if blocked:
			continue
		for key in (r[1] as Array):
			if t.contains(String(key)):
				return String(r[0])
	return DEFAULT_POSE


## Mood overrides WITHIN the class — the burnt cofounder IS the one in the slumped
## pose, which is what keeps the library at 24 poses instead of 72.
static func apply_mood(pose: String, mood: String) -> String:
	if mood.to_lower() != "burnt":
		return pose
	if pose == "sit_desk_typing":
		return "sit_desk_slumped"
	if pose == "sit_couch_relaxed":
		return "sit_couch_headinhands"
	if pose == "sit_audience_clapping":
		return "sit_audience_neutral"
	if pose.begins_with("stand"):
		return "stand_slumped"
	return pose


# ═════════════════════════════════════════════════════════════════════════════
# 1. SCENE
# ═════════════════════════════════════════════════════════════════════════════

func _resolve_scene(id: String) -> String:
	if id == "":
		return ""
	var index := _load_json("%s/index.json" % _bg_root)
	# index.json maps EVERY facet combination — not just the 516 exact ids — to its
	# filename, which is what makes an absent key an unambiguous miss.
	var f := String(index.get(id, ""))
	if f == "":
		return ""
	var p := "%s/%s" % [_bg_root, f]
	if not _exists(p):
		return ""
	scene_file = f
	return p


func _draw_scene(frame: Vector2) -> void:
	var bg := TextureRect.new()
	bg.name = "scene"
	# Flags BEFORE size, texture before the deferred size — the documented trap.
	bg.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	bg.stretch_mode = TextureRect.STRETCH_SCALE
	bg.mouse_filter = Control.MOUSE_FILTER_IGNORE
	bg.texture = _scene_tex
	bg.position = Vector2.ZERO
	bg.size = frame
	bg.set_deferred("size", frame)
	add_child(bg)


# ═════════════════════════════════════════════════════════════════════════════
# 3. SLOTS
# ═════════════════════════════════════════════════════════════════════════════

func _slots_for(id: String) -> Array:
	var raw: Dictionary = _slots_override
	if raw.is_empty():
		raw = _load_json("%s/slots.json" % _bg_root)
	var out: Array = []
	if raw.has("slots"):
		out = _slot_rows(raw.get("slots"))
	elif raw.has(id):
		var entry: Variant = raw.get(id)
		if entry is Dictionary:
			out = _slot_rows((entry as Dictionary).get("slots"))
		elif entry is Array:
			out = _slot_rows(entry)
	if out.is_empty():
		# The documented degrade: annotations.json already carries a founder mark and
		# four crew marks per scene, which are exactly stand slots with a depth scale.
		out = _marks_as_slots(id)
		push_warning("SceneStage: no typed slots for '%s' — falling back to crew marks (%d)" % [id, out.size()])
	return out


func _slot_rows(v: Variant) -> Array:
	var out: Array = []
	if not (v is Array):
		return out
	for row in (v as Array):
		if row is Dictionary:
			out.append(row)
	return out


func _marks_as_slots(id: String) -> Array:
	var ann := _load_json("%s/annotations.json" % _bg_root)
	var entry: Variant = ann.get(id, null)
	if not (entry is Dictionary):
		return []
	var marks: Variant = (entry as Dictionary).get("marks", null)
	if not (marks is Dictionary):
		return []
	var table: Dictionary = marks
	var out: Array = []
	var prominence := 1
	# INSIDE-OUT, not crew_1 onwards. The derived marks are laid out symmetrically
	# about the founder (272 · 464 · [656] · 848 · 1040 on a typical office), so
	# filling them in name order puts a three-person cast in the left third of the
	# frame, overlapping, with the right half empty — assembled, not composed. The
	# founder takes the centre and the rest flank outward, so every cast size from one
	# to five is balanced. Real slots.json carries authored prominence and never
	# reaches this.
	for key in ["founder_mark", "crew_2", "crew_3", "crew_1", "crew_4"]:
		if not table.has(key):
			continue
		var m: Dictionary = table[key]
		var mw := float(m.get("w", 0.0))
		var mh := float(m.get("h", 300.0))
		out.append({
			"id": key,
			"pose_class": "stand",
			"x": float(m.get("foot_x", float(m.get("x", 0.0)) + mw * 0.5)),
			"y": float(m.get("foot_y", float(m.get("y", 0.0)) + mh)),
			"h": mh * float(m.get("scale", 1.0)),   # marks carry depth as a multiplier
			"face": "any",
			"occ": null,
			"prominence": prominence,
		})
		prominence += 1
	return out


## Deterministic, every time, for the same cast in the same room: founder first, then
## input order; each takes the LOWEST-prominence free slot whose class fits the pose.
func _assign(cast: Array, slots: Array) -> Array:
	var ordered := _by_prominence(slots)
	var taken: Dictionary = {}
	var plan: Array = []
	for member in _founder_first(cast):
		var m: Dictionary = member
		var who := String(m.get("who", ""))
		var doing := String(m.get("doing", ""))
		var mood := String(m.get("mood", "fine"))
		var pose := pose_for(doing, mood)
		var pick := _first_free(ordered, taken, pose)
		if pick < 0:
			# Next rung: any plain stand slot, with the neutral (or, if the mood or the
			# activity demands it, the slumped) stand pose.
			var alt := _stand_fallback(pose, mood)
			pick = _first_free(ordered, taken, alt, true)
			if pick >= 0:
				push_warning("SceneStage: no '%s' slot free in '%s' — %s stands as %s instead" % [pose, scene_id, who, alt])
				pose = alt
		if pick < 0:
			# The floor of the ladder. A character in the wrong body position reads as
			# pasted, which is the failure the whole architecture exists to avoid.
			_dropped.append({"who": who, "pose": pose, "reason": "no free slot"})
			push_warning("SceneStage: no slot left in '%s' for %s (%s) — dropped rather than mis-posed" % [scene_id, who, pose])
			continue
		taken[pick] = true
		plan.append({"who": who, "mood": mood, "doing": doing, "pose": pose,
			"slot": ordered[pick]})
	return plan


## Stable insertion sort: Array.sort_custom is not stable, and two slots sharing a
## prominence must still resolve the same way on every build.
func _by_prominence(slots: Array) -> Array:
	var out: Array = []
	for s in slots:
		var row: Dictionary = s
		var p := int(row.get("prominence", 99))
		var at := out.size()
		for i in out.size():
			if int((out[i] as Dictionary).get("prominence", 99)) > p:
				at = i
				break
		out.insert(at, row)
	return out


func _first_free(ordered: Array, taken: Dictionary, pose: String, stand_only := false) -> int:
	for i in ordered.size():
		if taken.has(i):
			continue
		var klass := String((ordered[i] as Dictionary).get("pose_class", "stand"))
		if stand_only:
			if klass == "stand" or klass.begins_with("stand"):
				return i
			continue
		if _pose_fits(pose, klass):
			return i
	return -1


## A pose fits a slot class when the class is a prefix of it, so a "stand" slot takes
## any stand_* pose while a "sit_desk" slot refuses stand_phone.
static func _pose_fits(pose: String, klass: String) -> bool:
	if klass == "" or klass == "any":
		return true
	return pose == klass or pose.begins_with(klass + "_")


static func _stand_fallback(pose: String, mood: String) -> String:
	if mood.to_lower() == "burnt" or DESPAIR_POSES.has(pose):
		return "stand_slumped"
	return DEFAULT_POSE


static func _founder_first(cast: Array) -> Array:
	var founders: Array = []
	var rest: Array = []
	for c in cast:
		if not (c is Dictionary):
			continue
		var d: Dictionary = c
		if _is_founder(d):
			founders.append(d)
		else:
			rest.append(d)
	founders.append_array(rest)     # input order survives inside each group
	return founders


static func _is_founder(d: Dictionary) -> bool:
	if bool(d.get("founder", false)):
		return true
	var role := String(d.get("role", "")).to_lower()
	if role.begins_with("founder"):
		return true
	var who := String(d.get("who", "")).to_lower()
	return who.begins_with("founder") or who.begins_with("cast_founder")


# ═════════════════════════════════════════════════════════════════════════════
# 4 + 6. DRAW, then GRADE
# ═════════════════════════════════════════════════════════════════════════════

func _place(row: Dictionary, frame: Vector2) -> void:
	var slot: Dictionary = row["slot"]
	var who := String(row["who"])
	var mood := String(row["mood"])
	var pose := String(row["pose"])

	var source := "pose"
	var meta := _pose_meta(who, pose)
	var tex := _texture(_pose_png(who, pose))
	if tex == null:
		tex = _canonical(who, mood)
		source = "canonical"
		meta = {}       # a canonical sprite is not a pose: feet anchor, no eye coords
		if tex != null:
			push_warning("SceneStage: no pose '%s' for %s — canonical sprite stands in" % [pose, who])
	if tex == null:
		_dropped.append({"who": who, "pose": pose, "reason": "no sprite"})
		push_warning("SceneStage: no art at all for %s — dropped from '%s'" % [who, scene_id])
		return

	var anchor := String(meta.get("anchor", _default_anchor(pose)))
	var h := maxf(float(slot.get("h", 300.0)), 1.0)
	var tw := maxf(float(tex.get_width()), 1.0)
	var th := maxf(float(tex.get_height()), 1.0)
	var w := h * (tw / th)                                   # depth scale is per slot
	var ax := float(slot.get("x", 0.0))
	var ay := float(slot.get("y", 0.0))
	var anchor_y := h if anchor == "feet" else h * SEAT_RATIO
	var origin := Vector2(ax - w * 0.5, ay - anchor_y)

	var layer := TextureRect.new()
	layer.name = "pose_%d_%s" % [_placements.size(), who]
	layer.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	layer.stretch_mode = TextureRect.STRETCH_SCALE
	layer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	# Every pose is drawn facing LEFT, one side only; the engine supplies the other.
	layer.flip_h = String(slot.get("face", "any")).to_lower() == "right"
	layer.texture = tex
	layer.position = origin
	layer.size = Vector2(w, h)
	layer.set_deferred("size", Vector2(w, h))
	layer.pivot_offset = Vector2(w * 0.5, anchor_y)          # breathe from the anchor
	layer.modulate = _grade(Rect2(origin, Vector2(w, h)))
	add_child(layer)

	var entry: Dictionary = {
		"who": who, "mood": mood, "doing": String(row.get("doing", "")),
		"pose": pose, "slot_id": String(slot.get("id", "")), "source": source,
		"layer": layer,
	}
	var eyes := _eye_ellipses(meta, Vector2(w, h), Vector2(tw, th), layer.flip_h)
	if not eyes.is_empty():
		var lids := BlinkLids.new()
		lids.eyes = eyes
		lids.mouse_filter = Control.MOUSE_FILTER_IGNORE
		lids.position = Vector2.ZERO
		lids.size = Vector2(w, h)
		lids.set_deferred("size", Vector2(w, h))
		lids.visible = false
		layer.add_child(lids)      # inside the pose, so it breathes and sways with it
		entry["blink"] = lids
		entry["blink_at"] = _rng.randf_range(1.0, 5.0)
	_placements.append(entry)

	# The scene's OWN pixels, drawn back over the character: a crop of the room is
	# aligned by construction, so the desk in front of a seated pose needs no
	# detection and cannot drift.
	var occ := _occ_rect(slot.get("occ"), frame)
	if occ.size.x >= 1.0 and occ.size.y >= 1.0:
		var atlas := AtlasTexture.new()
		atlas.atlas = _scene_tex
		atlas.region = occ
		atlas.filter_clip = true
		var crop := TextureRect.new()
		crop.name = "occ_%d" % (_placements.size() - 1)
		crop.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		crop.stretch_mode = TextureRect.STRETCH_SCALE
		crop.mouse_filter = Control.MOUSE_FILTER_IGNORE
		crop.texture = atlas
		crop.position = occ.position
		crop.size = occ.size
		crop.set_deferred("size", occ.size)
		add_child(crop)


## FOUR NUMBERS ARE AMBIGUOUS. The architecture doc's own example, [0,470,460,720] on
## a 1536x1024 plate, is corners — read as x/y/w/h it would run 166px off the bottom.
## A slots producer may well emit x/y/w/h instead, so both are accepted: corners are
## assumed only when the x/y/w/h reading would leave the plate and the corner reading
## would not. Either way the result is clamped to the scene, so neither can crash.
func _occ_rect(v: Variant, frame: Vector2) -> Rect2:
	var x := 0.0
	var y := 0.0
	var w := 0.0
	var h := 0.0
	if v is Array and (v as Array).size() >= 4:
		var a: Array = v
		x = float(a[0])
		y = float(a[1])
		w = float(a[2])
		h = float(a[3])
		if w > x and h > y and (x + w > frame.x or y + h > frame.y):
			w -= x
			h -= y
	elif v is Dictionary:
		var d: Dictionary = v
		x = float(d.get("x", 0.0))
		y = float(d.get("y", 0.0))
		w = float(d.get("w", 0.0))
		h = float(d.get("h", 0.0))
	else:
		return Rect2()
	if w <= 0.0 or h <= 0.0:
		return Rect2()
	return Rect2(x, y, w, h).intersection(Rect2(Vector2.ZERO, frame))


## A character in a night room must not be daylight-bright. Sample the scene's own
## mean tone in the band the character occupies and pull the pose layer toward it,
## clamped to 0.85..1.0 per channel so the grade is a nudge, never a repaint.
func _grade(band: Rect2) -> Color:
	var img := _scene_image()
	if img == null:
		return Color.WHITE
	var bounds := Rect2(0.0, 0.0, float(img.get_width()), float(img.get_height()))
	var r := band.intersection(bounds)
	if r.size.x < 2.0 or r.size.y < 2.0:
		return Color.WHITE
	var steps := 8
	var sr := 0.0
	var sg := 0.0
	var sb := 0.0
	var n := 0
	for iy in steps:
		for ix in steps:
			var px := clampi(int(r.position.x + r.size.x * (float(ix) + 0.5) / float(steps)),
				0, img.get_width() - 1)
			var py := clampi(int(r.position.y + r.size.y * (float(iy) + 0.5) / float(steps)),
				0, img.get_height() - 1)
			var c := img.get_pixel(px, py)
			sr += c.r
			sg += c.g
			sb += c.b
			n += 1
	if n == 0:
		return Color.WHITE
	var mr := sr / float(n)
	var mg := sg / float(n)
	var mb := sb / float(n)
	var luma := clampf(0.2126 * mr + 0.7152 * mg + 0.0722 * mb, 0.0, 1.0)
	var k := clampf(0.85 + 0.30 * luma, 0.85, 1.0)     # dark room → darker character
	var d := maxf(luma, 0.001)
	return Color(
		clampf(k * lerpf(1.0, mr / d, 0.25), 0.85, 1.0),   # …and a red room, warmer
		clampf(k * lerpf(1.0, mg / d, 0.25), 0.85, 1.0),
		clampf(k * lerpf(1.0, mb / d, 0.25), 0.85, 1.0),
		1.0)


## HEADLESS RUNS ON A DUMMY RENDERER whose texture_2d_get hands back nothing, so a
## texture is never the first place to ask for pixels. Any path we decoded ourselves
## is re-read from the file; res:// goes through the texture and only falls back to
## the source png, which exists in a project run but never in an export.
func _scene_image() -> Image:
	if _scene_img != null or _scene_img_tried:
		return _scene_img
	_scene_img_tried = true
	if not _scene_path.begins_with("res://"):
		_scene_img = _decode(_scene_path)
		if _scene_img != null:
			return _scene_img
	if _scene_tex != null:
		var img := _scene_tex.get_image()
		if img != null and not img.is_empty():
			if img.is_compressed() and img.decompress() != OK:
				push_warning("SceneStage: '%s' will not decompress — grading skipped" % scene_id)
				return null
			_scene_img = img
			return _scene_img
	_scene_img = _decode(_scene_path)
	if _scene_img == null:
		push_warning("SceneStage: '%s' hands back no pixels — grading skipped" % scene_id)
	return _scene_img


func _decode(path: String) -> Image:
	if path == "" or not FileAccess.file_exists(path):
		return null
	var img := Image.new()
	if img.load(path) != OK or img.is_empty():
		return null
	return img


# ═════════════════════════════════════════════════════════════════════════════
# 5. LIFE — all of it free, all of it in-engine
# ═════════════════════════════════════════════════════════════════════════════

## The additive delta loop: only ~1% of an ambient loop's pixels ever move, so the
## motion is stored as frame_i minus frame_0 and ADDED over the still. Black adds
## nothing, so the room is untouched where nothing moved — and where the bulb
## brightens it also brightens the character standing under it, which is correct.
func _start_ambient() -> void:
	_ambient_frames.clear()
	var roots: Array = []
	var stem := scene_file.get_basename()
	if stem != "":
		roots.append(stem)
	var flat := scene_id.replace("/", "__")
	if not roots.has(flat):
		roots.append(flat)
	for r in roots:
		var base := "%s/ambient/%s" % [_bg_root, String(r)]
		var i := 0
		while true:
			var fp := "%s/d_%02d.png" % [base, i]
			if not _exists(fp):
				break
			var t := _texture(fp)
			if t == null:
				break
			_ambient_frames.append(t)
			i += 1
		if not _ambient_frames.is_empty():
			break
	if _ambient_frames.is_empty():
		push_warning("SceneStage: no ambient deltas for '%s' — the room is still" % scene_id)
		return
	_ambient = TextureRect.new()
	_ambient.name = "ambient"
	_ambient.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_ambient.stretch_mode = TextureRect.STRETCH_SCALE
	_ambient.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var mat := CanvasItemMaterial.new()
	mat.blend_mode = CanvasItemMaterial.BLEND_MODE_ADD
	_ambient.material = mat
	_ambient.texture = _ambient_frames[0]
	_ambient.position = Vector2.ZERO
	_ambient.size = size
	_ambient.set_deferred("size", size)
	add_child(_ambient)             # last, so the light lands over everything


func _begin_life() -> void:
	if not is_inside_tree():
		_life_pending = true        # create_tween needs the tree; _ready picks it up
		return
	_life_pending = false
	for i in _placements.size():
		var row: Dictionary = _placements[i]
		var layer: TextureRect = row.get("layer")
		if layer == null or not is_instance_valid(layer):
			continue
		# A burnt character breathes slower. Mood is state, not decoration.
		var slow := 1.7 if String(row.get("mood", "fine")).to_lower() == "burnt" else 1.0
		# A looping tween whose FIRST step has zero duration makes Godot spin
		# ("Infinite loop detected", tween.cpp), so the stagger never starts at 0.
		var br := create_tween().set_loops()
		br.tween_interval(0.13 * float(i) + 0.05)
		br.tween_property(layer, "scale", Vector2(1.004, 0.988), 1.15 * slow).set_trans(Tween.TRANS_SINE)
		br.tween_property(layer, "scale", Vector2.ONE, 1.15 * slow).set_trans(Tween.TRANS_SINE)
		var pose := String(row.get("pose", ""))
		if pose.begins_with("stand") or pose.begins_with("walk"):
			# Idle sway, rotating about the foot anchor — a standing body is never
			# perfectly vertical, and perfect verticality is what reads as pasted.
			var sw := create_tween().set_loops()
			sw.tween_interval(0.21 * float(i) + 0.07)
			sw.tween_property(layer, "rotation", deg_to_rad(0.6), 2.3 * slow).set_trans(Tween.TRANS_SINE)
			sw.tween_property(layer, "rotation", deg_to_rad(-0.6), 4.6 * slow).set_trans(Tween.TRANS_SINE)
			sw.tween_property(layer, "rotation", 0.0, 2.3 * slow).set_trans(Tween.TRANS_SINE)
	set_process(true)


func _process(delta: float) -> void:
	_t += delta
	if _ambient != null and is_instance_valid(_ambient) and not _ambient_frames.is_empty():
		_ambient.texture = _ambient_frames[int(_t * AMBIENT_FPS) % _ambient_frames.size()]
	for row in _placements:
		var r: Dictionary = row
		if not r.has("blink"):
			continue
		var lids: Control = r["blink"]
		if lids == null or not is_instance_valid(lids):
			continue
		if _t < float(r.get("blink_at", 0.0)):
			continue
		if lids.visible:
			lids.visible = false
			r["blink_at"] = _t + _rng.randf_range(3.0, 6.0)
		else:
			lids.visible = true
			r["blink_at"] = _t + BLINK_HOLD


# ═════════════════════════════════════════════════════════════════════════════
# POSE LOOKUP + THE DEGRADE LADDER
# ═════════════════════════════════════════════════════════════════════════════

func _pose_png(who: String, pose: String) -> String:
	return "%s/%s/%s.png" % [_poses_root, who, pose]


## Eye coords and the anchor are extracted ONCE at import, which is what makes
## blinking free at runtime. Three shapes are accepted because the pose lane may ship
## per-pose json, a per-character table, or one library-wide table.
func _pose_meta(who: String, pose: String) -> Dictionary:
	var direct := _load_json("%s/%s/%s.json" % [_poses_root, who, pose])
	if not direct.is_empty():
		return direct
	var per_char := _load_json("%s/%s/meta.json" % [_poses_root, who])
	if per_char.has(pose) and per_char[pose] is Dictionary:
		return per_char[pose]
	var lib := _load_json("%s/meta.json" % _poses_root)
	var key := "%s/%s" % [who, pose]
	if lib.has(key) and lib[key] is Dictionary:
		return lib[key]
	if lib.has(pose) and lib[pose] is Dictionary:
		return lib[pose]
	return {}


## No meta is not a failure: a sitting pose is anchored at the seat, everything else
## at the feet, and the pose id already says which it is.
static func _default_anchor(pose: String) -> String:
	if pose.begins_with("sit_") or pose.begins_with("lie_") or pose.begins_with("sleep_"):
		return "seat"
	return "feet"


## Eye centres are stored in the pose's OWN pixel space, so they scale with the slot
## and mirror with the flip.
func _eye_ellipses(meta: Dictionary, placed: Vector2, native: Vector2, flipped: bool) -> Array:
	var raw: Variant = meta.get("eyes", null)
	if not (raw is Array):
		return []
	var mw := maxf(float(meta.get("w", native.x)), 1.0)
	var mh := maxf(float(meta.get("h", native.y)), 1.0)
	var sx := placed.x / mw
	var sy := placed.y / mh
	var rx := maxf(placed.y * 0.030, 2.0)
	var ry := maxf(placed.y * 0.012, 1.0)
	var out: Array = []
	for e in (raw as Array):
		if not (e is Array) or (e as Array).size() < 2:
			continue
		var pair: Array = e
		var cx := float(pair[0]) * sx
		var cy := float(pair[1]) * sy
		if flipped:
			cx = placed.x - cx
		var r := Vector2(rx, ry)
		if pair.size() >= 3:
			var er := float(pair[2]) * sx
			r = Vector2(maxf(er, 1.0), maxf(er * 0.40, 1.0))
		out.append([Vector2(cx, cy), r])
	return out


## The last rung before dropping a character: the canonical sprite that every pose in
## the library was made consistent against.
func _canonical(who: String, mood: String) -> Texture2D:
	var m := mood.to_lower()
	var names: Array = []
	if m == "fine" or m == "burnt" or m == "gone":
		names.append("cast_%s_%s" % [who, m])
	var fine := "cast_%s_fine" % who
	if not names.has(fine):
		names.append(fine)
	names.append(who)                       # a caller may pass the folder name whole
	for n in names:
		var p := "%s/%s/sprite.png" % [_cast_root, String(n)]
		if _exists(p):
			return _texture(p)
	return null


# ═════════════════════════════════════════════════════════════════════════════
# FILE ACCESS — res:// goes through the importer, everything else through Image
# ═════════════════════════════════════════════════════════════════════════════

## The library plates ship as webp and the png is left out of the export, so every
## res:// lookup asks for the mirror first (SceneRoom.art_path). A path that has no
## mirror comes back unchanged, which is what keeps the pose cutouts on their png.
func _exists(path: String) -> bool:
	if path.begins_with("res://"):
		return ResourceLoader.exists(SceneRoom.art_path(path))
	return FileAccess.file_exists(path)


func _texture(path: String) -> Texture2D:
	if path == "":
		return null
	if path.begins_with("res://"):
		var res_path := SceneRoom.art_path(path)
		if not ResourceLoader.exists(res_path):
			return null
		var res: Variant = load(res_path)
		return res if res is Texture2D else null
	# user:// and absolute paths are not imported resources, so load() cannot see
	# them: the bytes are decoded directly.
	var img := _decode(path)
	if img == null:
		return null
	return ImageTexture.create_from_image(img)


func _load_json(path: String) -> Dictionary:
	if _json_cache.has(path):
		return _json_cache[path]
	var out: Dictionary = {}
	if FileAccess.file_exists(path):
		var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(path))
		if parsed is Dictionary:
			out = parsed
	_json_cache[path] = out
	return out


func _reset() -> void:
	for c in get_children():
		remove_child(c)
		c.queue_free()
	_placements.clear()
	_dropped.clear()
	_ambient = null
	_ambient_frames.clear()
	_scene_tex = null
	_scene_img = null
	_scene_img_tried = false
	_life_pending = false
	_t = 0.0
	scene_file = ""
	_scene_path = ""
	set_process(false)


## The closed lids. Blinking is a draw call over stored coordinates, not an extra
## sprite in the library — which is the whole reason eye detection runs at import.
class BlinkLids extends Control:
	var eyes: Array = []                 # [[centre: Vector2, radius: Vector2], …]
	var ink := Color(0.118, 0.118, 0.118)

	func _draw() -> void:
		for e in eyes:
			var pair: Array = e
			var centre: Vector2 = pair[0]
			var radius: Vector2 = pair[1]
			draw_set_transform(centre, 0.0, radius)
			draw_circle(Vector2.ZERO, 1.0, ink)      # a unit circle, scaled = an ellipse
			draw_set_transform(Vector2.ZERO, 0.0, Vector2.ONE)
