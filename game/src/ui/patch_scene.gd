class_name PatchScene
extends Control
## THE SPOT-PATCH ROOM — docs/BLANK_SCENES_ARCHITECTURE.md §8.
##
## A scene's spots are not coordinates to paste at. They are REGIONS WITH RENDITIONS:
## every character patch was cut out of a NATIVE RENDER of this same scene, at this
## same place, under this same light. Assembly is therefore CHOOSING, not pasting —
## which is the whole point, and the reason the owner rejected the two assemblies
## that came before it ("backgrounds and you just pasted on top random characters").
##
## What ships per era, under game/assets/patch_scenes/<era>/:
##   blank.png                    the room with every spot empty (the erase edit)
##   populated.png                the same room with every spot filled (reference)
##   spots.json                   per spot: its region, and who it can hold
##   patches.json                 per patch: its offset in the blank, and its size
##   patches/<spot>__<who>.png    the character, cut from a native render
##   patches/<spot>__<who>__f2.png   an optional second frame of that same character
##   eyes.json                    per patch: eye boxes, in PATCH-LOCAL pixels
##   ambient/d_NN.png             light deltas over the blank, additive
##
## THE THREE RULES THIS CLASS WILL NOT BREAK:
##
##  1. A PATCH IS NEVER TRANSFORMED. No scaling, no flipping, no colour grading. Its
##     pixels already carry the room's own light and it already interlocks with the
##     furniture it was cut against; anything applied to it breaks that registration
##     and puts us straight back to the pasted look. The only transform in this file
##     is the SINGLE frame fit shared by the blank and every patch alike (see _frame),
##     which cannot desynchronise them because it is one scale on one parent.
##
##  2. NOBODY IS EVER SUBSTITUTED. A `who` with no patch in this scene is SKIPPED,
##     with one warning, and its spot simply stays empty. That is not a degrade, it
##     is the feature: the room where half the crew has left LOOKS like the room
##     where half the crew has left.
##
##  3. NOTHING CRASHES OR BLANKS. Every missing file costs exactly one push_warning
##     and the room renders without it. Only a missing blank is fatal, and it returns
##     false so the caller keeps the room it already had.
##
## Usage:
##   var ps := PatchScene.new()
##   room.add_child(ps)                        # in the tree first: life needs tweens
##   if ps.build("garage", crew_cast()): …     # cast: [{who, mood, doing}]

const ROOT := "res://assets/patch_scenes"
const FRAME := Vector2(1536.0, 1024.0)
const AMBIENT_FPS := 12.0

## Blink: how long the lids are down, and the window between blinks.
const BLINK_HOLD := 0.12
const BLINK_MIN := 3.0
const BLINK_MAX := 6.0

## The bob. ±1–2px of TRANSLATION only — never scale. A scaled patch stops lining up
## with the desk edge it was cut against, and a 1px seam along a desk is visible from
## across the room.
const BOB_MIN := 1.0
const BOB_MAX := 2.0
const BOB_SECS := 1.7

## The second frame's rhythm: about a second on each, so it reads as a person moving
## rather than a sprite flickering.
const F2_ON := 0.9
const F2_OFF := 1.15

## THE RUN SPEAKS ROLES; THE SCENE SPEAKS ART. crew_cast() says "tech" and "founder",
## and the patches are named for who was actually drawn: cofd_tech at the bench, and
## the founder's own chair holding one patch per archetype. Translating here is what
## lets the caller hand this its cast unedited.
const COFOUNDER_ART := "cofd_"
const EMPLOYEE_ART := "employee"
const ARCHETYPE_ART := {
	"hacker": "hacker", "hustler": "hustler",
	"consultant": "consultant", "exfaang": "pm",
}

var era := ""

## The run's founder archetype (hacker / hustler / consultant / exfaang). It picks
## WHICH rendition of the founder's chair is theirs. Left empty, the chair falls back
## to whichever rendition the scene ships as its resident.
var archetype := ""

## Test hook: point the whole loader at a fixture tree under user://. The suite must
## never depend on which minute the scene factory wrote a file.
var _root := ROOT

var _frame: Control                  # the scene at its NATIVE pixel size
var _blank: TextureRect
var _ambient: TextureRect
var _ambient_frames: Array = []
var _t := 0.0
var _life_pending := false

var _meta: Dictionary = {}           # patch name -> {offset: Vector2, size: Vector2}
var _spots: Dictionary = {}          # spot id -> its row out of spots.json
var _spot_order: Array = []          # the order spots.json declares them in
var _founder_spot := ""              # the chair the room is composed around
var _eyes: Dictionary = {}           # patch name -> [[x0, y0, x1, y1], …] patch-local
var _tweens: Array = []              # every loop this build started, so a rebuild ends them
var _placed: Array = []              # [{who, spot, name, mood, doing, f2}]
var _skipped: Array = []             # every `who` this scene has no patch for
var _rng := RandomNumberGenerator.new()


## Scenes are STATIC IMAGES for now — the owner's call after reviewing the animated
## composites. The whole life layer (ambient deltas, bob, blinks, f2 alternation)
## stays built and under test, but it mounts only when RUNWAY_LIFE=1 is set.
static func life_enabled() -> bool:
	return OS.get_environment("RUNWAY_LIFE") == "1"


## Does a FINISHED scene ship for this era? Cheap enough for a mount key.
static func exists_for(p_era: String) -> bool:
	if p_era == "":
		return false
	return _shipped("%s/%s" % [ROOT, p_era])


## A SCENE IS ITS BLANK **AND** ITS PEOPLE. A directory holding only a blank is a
## scene mid-production, not a room where everyone has left — and mounting it would
## quietly empty the game's room for as long as the factory was still cutting. The
## two cases look identical from the blank alone, so the patches decide.
static func _shipped(dir: String) -> bool:
	var blank := dir + "/blank.png"
	if not (ResourceLoader.exists(blank) or FileAccess.file_exists(blank)):
		return false
	if FileAccess.file_exists(dir + "/patches.json"):
		var t: Variant = JSON.parse_string(FileAccess.get_file_as_string(dir + "/patches.json"))
		if t is Dictionary and not (t as Dictionary).is_empty():
			return true
	var d := DirAccess.open(dir + "/patches")
	if d == null:
		return false
	for f in d.get_files():
		if String(f).ends_with(".png"):
			return true
	return false


## BUILD THE ROOM. `cast` is [{who, mood, doing}, …] straight out of the run — the
## founder first. Returns false only when this era ships no scene, so the caller can
## fall through to whatever room it was already standing in.
func build(p_era: String, cast: Array) -> bool:
	_reset()
	era = p_era
	var dir := "%s/%s" % [_root, era]
	if not _shipped(dir):
		# Either the era ships nothing, or the factory is still cutting it. Both are
		# "keep the room you have", never "show an empty one".
		push_warning("PatchScene: no finished scene for era '%s' in %s" % [era, dir])
		return false
	var blank_tex := _texture(dir + "/blank.png")
	if blank_tex == null:
		push_warning("PatchScene: blank unreadable for era '%s' — %s/blank.png" % [era, dir])
		return false

	mouse_filter = Control.MOUSE_FILTER_IGNORE
	size = FRAME
	set_deferred("size", FRAME)

	# THE FRAME FIT LIVES ON ONE PARENT. Patch offsets and eye boxes are recorded in
	# the blank's own pixels, so everything is laid out at native resolution inside
	# _frame and the whole composite is fitted to the room once. One scale on one
	# node cannot drift a patch off the furniture it interlocks with — whereas
	# converting each offset separately is exactly how a 1px seam gets in.
	var native := Vector2(float(blank_tex.get_width()), float(blank_tex.get_height()))
	_frame = Control.new()
	_frame.name = "frame"
	_frame.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_frame.position = Vector2.ZERO
	_frame.size = native
	_frame.set_deferred("size", native)
	_frame.scale = Vector2(FRAME.x / maxf(native.x, 1.0), FRAME.y / maxf(native.y, 1.0))
	add_child(_frame)

	# THE DOCUMENTED TRAP: the flags go on BEFORE the texture, and the size is set
	# deferred. A TextureRect handed its texture first sizes itself to the image and
	# ignores what it is told afterwards, which is how a 2048px blank ends up hanging
	# off the bottom of a 1024px room.
	var bg := TextureRect.new()
	bg.name = "blank"
	bg.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	bg.stretch_mode = TextureRect.STRETCH_SCALE
	bg.mouse_filter = Control.MOUSE_FILTER_IGNORE
	bg.texture = blank_tex
	bg.position = Vector2.ZERO
	bg.size = native
	bg.set_deferred("size", native)
	_frame.add_child(bg)
	_blank = bg

	_meta = _patch_table(dir)
	_read_spots(dir)
	_eyes = _json_dict(dir + "/eyes.json")

	_rng.seed = hash(era)          # the same room staggers the same way twice
	# BACK TO FRONT. Spots can overlap — someone at a desk in front of the bench
	# shares pixels with it — and the cast arrives in the run's order (founder first),
	# which has nothing to do with depth. Whoever's feet are LOWER in the frame is
	# nearer the camera and draws last, which is the only ordering that never puts a
	# distant figure in front of a near one.
	var rows := _choose(dir, cast)
	rows.sort_custom(func(a, b): return _bottom_of(a) < _bottom_of(b))
	for row in rows:
		_draw_patch(dir, row as Dictionary)
	if life_enabled():
		_mount_ambient(dir)
		_begin_life()
	return true


## Who actually ended up in the room: [{who, spot, name, mood, doing, f2}].
func placements() -> Array:
	return _placed.duplicate(true)


## Who the run asked for and this scene has no patch for. Their spots stay empty.
func skipped() -> Array:
	return _skipped.duplicate()


## How many ambient delta frames the room is breathing on. 0 = a still room.
func ambient_frames() -> int:
	return _ambient_frames.size()


## The patches playing a two-frame loop rather than bobbing.
func alternating() -> Array:
	var out: Array = []
	for p in _placed:
		var row: Dictionary = p
		if bool(row.get("f2", false)):
			out.append(String(row.get("name", "")))
	return out


## One line for the run log, so a capture can be checked without opening the frame.
func cast_line() -> String:
	var bits := PackedStringArray()
	for p in _placed:
		var row: Dictionary = p
		bits.append("%s @%s%s" % [row.get("who", ""), row.get("spot", ""),
			" (f2)" if bool(row.get("f2", false)) else ""])
	for w in _skipped:
		bits.append("%s NO PATCH" % String(w))
	return String(", ").join(bits)


# ═════════════════════════════════════════════════════════════════════════════
# CHOOSING — never pasting
# ═════════════════════════════════════════════════════════════════════════════

## One patch per cast member, at most one character per spot. Nobody is invented and
## nobody is stood in for. The founder is resolved FIRST, whatever order the cast
## arrives in, because their chair is the one everybody else is arranged around.
func _choose(dir: String, cast: Array) -> Array:
	var names := _patch_names(dir)
	var taken: Dictionary = {}
	var out: Array = []
	var ordered: Array = []
	for entry in cast:
		if _is_founder(entry as Dictionary):
			ordered.append(entry)
	for entry in cast:
		if not _is_founder(entry as Dictionary):
			ordered.append(entry)
	for entry in ordered:
		var row: Dictionary = entry
		var who := String(row.get("who", ""))
		if who == "":
			continue
		var pick := _pick_for(names, row, taken)
		if pick == "":
			# RULE 2. No foreign sprite, ever. The spot stays empty and the room says
			# something true about the run.
			push_warning("PatchScene: '%s' has no patch in '%s' — the spot stays empty" % [who, era])
			_skipped.append(who)
			continue
		taken[_spot_of(pick)] = true
		out.append({
			"who": who,
			"spot": _spot_of(pick),
			"name": pick,
			"mood": String(row.get("mood", "")),
			"doing": String(row.get("doing", "")),
		})
	return out


static func _is_founder(row: Dictionary) -> bool:
	return String(row.get("kind", "")) == "founder" or String(row.get("who", "")) == "founder"


## The patch this cast member is drawn by, or "" if this scene never drew them.
func _pick_for(names: Array, row: Dictionary, taken: Dictionary) -> String:
	if _is_founder(row):
		return _pick_founder(names, taken)
	var who := String(row.get("who", ""))
	# The art vocabulary, best first: a cofounder is drawn as cofd_<role>, and a hire
	# has its OWN drawing — never a second copy of the cofounder whose job it shares.
	var keys: Array = []
	if String(row.get("kind", "")) == "employee":
		keys = [EMPLOYEE_ART, who]
	else:
		keys = [COFOUNDER_ART + who, who]
	for k in keys:
		var hit := _first_free(names, String(k), taken)
		if hit != "":
			return hit
	# Last resort, still exact about the person: a patch whose own name ENDS in this
	# role ("<anything>_tech" for "tech"), never one that merely resembles it.
	for n in names:
		var nm := String(n)
		if taken.has(_spot_of(nm)):
			continue
		if _who_of(nm).ends_with("_" + who):
			return nm
	return ""


## THE FOUNDER TAKES THE FOUNDER'S SPOT — spots.json names it outright, and it is
## never guessable from the id ("desk", "presenter", "window"). Which RENDITION of
## that chair is theirs is the run's archetype; if the scene never drew that
## archetype the chair stays empty, because the alternative is putting a different
## person in the founder's seat.
func _pick_founder(names: Array, taken: Dictionary) -> String:
	var spot := _founder_spot
	if spot == "" or taken.has(spot):
		for s in _spot_order:
			if String(s).contains("founder") and not taken.has(String(s)):
				spot = String(s)
				break
	if spot == "" or taken.has(spot):
		return ""
	var here: Array = []
	for n in names:
		if _spot_of(String(n)) == spot:
			here.append(String(n))
	if here.is_empty():
		return ""
	here.sort()
	if archetype != "":
		for k in [String(ARCHETYPE_ART.get(archetype, archetype)), archetype]:
			var want := "%s__%s" % [spot, String(k)]
			if here.has(want):
				return want
		return ""          # their archetype was never drawn — rule 2, no stand-in
	var resident := "%s__founder" % spot
	return resident if here.has(resident) else String(here[0])


## The first free spot holding a patch drawn for exactly this `who`, preferring the
## order spots.json declares — which is the room's own depth order.
func _first_free(names: Array, key: String, taken: Dictionary) -> String:
	if key == "":
		return ""
	var cands: Array = []
	for n in names:
		var nm := String(n)
		if _who_of(nm) != key or taken.has(_spot_of(nm)):
			continue
		cands.append(nm)
	if cands.is_empty():
		return ""
	cands.sort()
	for s in _spot_order:
		for n in cands:
			if _spot_of(String(n)) == String(s):
				return String(n)
	return String(cands[0])


## Every character patch this scene actually has on disk. patches.json is the index;
## a directory scan backs it up so a scene that shipped without the table still
## renders. `__empty` is the blank's own pixels and `__f2` is a second frame, so
## neither is ever a candidate.
func _patch_names(dir: String) -> Array:
	var seen: Dictionary = {}
	for k in _meta:
		seen[String(k)] = true
	var d := DirAccess.open(dir + "/patches")
	if d != null:
		for f in d.get_files():
			var fn := String(f)
			if fn.ends_with(".import"):
				fn = fn.get_basename()
			if not fn.ends_with(".png"):
				continue
			seen[fn.get_basename()] = true
	var out: Array = []
	for k in seen:
		var nm := String(k)
		if nm.ends_with("__f2") or nm.ends_with("__empty"):
			continue
		var w := _who_of(nm)
		if w == "" or w == "empty":
			continue
		if not _exists("%s/patches/%s.png" % [dir, nm]):
			continue
		out.append(nm)
	out.sort()
	return out


## A patch is named "<spot>__<who>". Renders of the same pair can carry a "#n"
## variant tag, which is not part of the character's name.
static func _who_of(patch_name: String) -> String:
	var parts := patch_name.split("__", false)
	if parts.size() < 2:
		return ""
	var w := String(parts[parts.size() - 1])
	var tag := w.find("#")
	if tag >= 0:
		w = w.substr(0, tag)
	return w


static func _spot_of(patch_name: String) -> String:
	var parts := patch_name.split("__", false)
	if parts.size() < 2:
		return patch_name
	parts.remove_at(parts.size() - 1)
	return String("__").join(parts)


# ═════════════════════════════════════════════════════════════════════════════
# DRAWING — the scene's own pixels, at the offset they were cut from
# ═════════════════════════════════════════════════════════════════════════════

func _draw_patch(dir: String, row: Dictionary) -> void:
	var nm := String(row.get("name", ""))
	var tex := _texture("%s/patches/%s.png" % [dir, nm])
	if tex == null:
		push_warning("PatchScene: patch '%s' listed but unreadable — skipped" % nm)
		return
	# The PATCH's own size, not the spot's region: a cut patch is trimmed to its
	# content and is routinely shorter than the box it came out of. Stretching it to
	# the region is a scale, and rule 1 says no.
	var sz := Vector2(float(tex.get_width()), float(tex.get_height()))
	var off := _offset_of(nm)
	var tr := TextureRect.new()
	tr.name = nm
	tr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	tr.stretch_mode = TextureRect.STRETCH_SCALE
	tr.mouse_filter = Control.MOUSE_FILTER_IGNORE
	tr.texture = tex
	tr.position = off
	tr.size = sz
	tr.set_deferred("size", sz)
	_frame.add_child(tr)

	var f2 := _texture("%s/patches/%s__f2.png" % [dir, nm])
	var lids := _lids_for(nm, off, sz)

	var kept: Dictionary = row.duplicate()
	kept["offset"] = off
	kept["size"] = sz
	kept["f2"] = f2 != null
	kept["node"] = tr
	kept["f1_tex"] = tex
	kept["f2_tex"] = f2
	kept["lids"] = lids
	_placed.append(kept)


## The closed lids: ink ellipses over the coordinates the eye detector recorded, in
## the patch's own pixel space. Blinking is a draw call over stored coordinates, not
## a second sprite in the library.
func _lids_for(nm: String, off: Vector2, sz: Vector2) -> Control:
	var boxes := _eye_boxes(nm)
	if boxes.is_empty():
		return null
	var lids := PatchLids.new()
	lids.name = nm + "__lids"
	lids.mouse_filter = Control.MOUSE_FILTER_IGNORE
	lids.position = off
	lids.size = sz
	lids.set_deferred("size", sz)
	lids.eyes = boxes
	lids.visible = false
	_frame.add_child(lids)
	return lids


## eyes.json is written by the detector as boxes [x0, y0, x1, y1, area]; a pose-style
## table writes bare centres [x, y] instead. Both are read, both become an ellipse.
func _eye_boxes(nm: String) -> Array:
	var raw: Variant = _eyes.get(nm, null)
	if raw == null:
		raw = _eyes.get(_spot_of(nm), [])       # a table keyed by spot rather than patch
	if not (raw is Array):
		return []
	var out: Array = []
	for e in (raw as Array):
		if not (e is Array):
			continue
		var v: Array = e
		if v.size() >= 4:
			var x0 := float(v[0])
			var y0 := float(v[1])
			var x1 := float(v[2])
			var y1 := float(v[3])
			out.append([Vector2((x0 + x1) * 0.5, (y0 + y1) * 0.5),
				Vector2(maxf((x1 - x0) * 0.5, 2.0), maxf((y1 - y0) * 0.55, 2.0))])
		elif v.size() == 2:
			out.append([Vector2(float(v[0]), float(v[1])), Vector2(5.0, 4.0)])
	return out


## Where this patch's lowest pixel lands in the room — its depth, in one number.
func _bottom_of(row: Variant) -> float:
	var nm := String((row as Dictionary).get("name", ""))
	return _offset_of(nm).y + _size_of(nm).y


func _size_of(nm: String) -> Vector2:
	var row: Variant = _meta.get(nm, null)
	if not (row is Dictionary):
		return Vector2.ZERO
	var s: Variant = (row as Dictionary).get("size", Vector2.ZERO)
	return s if s is Vector2 else Vector2.ZERO


func _offset_of(nm: String) -> Vector2:
	var row: Variant = _meta.get(nm, null)
	if not (row is Dictionary):
		return Vector2.ZERO
	var o: Variant = (row as Dictionary).get("offset", Vector2.ZERO)
	return o if o is Vector2 else Vector2.ZERO


# ═════════════════════════════════════════════════════════════════════════════
# LIFE — the room is never a photograph
# ═════════════════════════════════════════════════════════════════════════════

## Ambient light deltas, added over the finished composite. Black adds nothing, so
## the room is untouched everywhere nothing moved — and where the bulb brightens it
## brightens the person standing under it too, which is right rather than a bug.
func _mount_ambient(dir: String) -> void:
	var base := dir + "/ambient"
	for start in [0, 1]:
		var i := int(start)
		while true:
			var t := _texture("%s/d_%02d.png" % [base, i])
			if t == null:
				break
			_ambient_frames.append(t)
			i += 1
		if not _ambient_frames.is_empty():
			break
	if _ambient_frames.is_empty():
		push_warning("PatchScene: no ambient deltas for '%s' — the room holds still" % era)
		return
	var a := TextureRect.new()
	a.name = "ambient"
	a.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	a.stretch_mode = TextureRect.STRETCH_SCALE
	a.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var mat := CanvasItemMaterial.new()
	mat.blend_mode = CanvasItemMaterial.BLEND_MODE_ADD
	a.material = mat
	a.texture = _ambient_frames[0]
	a.position = Vector2.ZERO
	a.size = _frame.size
	a.set_deferred("size", _frame.size)
	_frame.add_child(a)          # last, so the light lands over everyone
	_ambient = a
	set_process(true)


## Tweens need the tree. When the caller builds before adding, _ready picks this up.
func _begin_life() -> void:
	if not is_inside_tree():
		_life_pending = true
		return
	_life_pending = false
	for i in _placed.size():
		var row: Dictionary = _placed[i]
		var tr: TextureRect = row.get("node")
		if tr == null or not is_instance_valid(tr):
			continue
		if bool(row.get("f2", false)):
			var f1t: Texture2D = row.get("f1_tex")
			var f2t: Texture2D = row.get("f2_tex")
			_alternate(tr, f1t, f2t, i)
		else:
			_bob(tr, i)
		var lids: Control = row.get("lids")
		if lids != null and is_instance_valid(lids):
			_blink(lids, i)


## A patch breathes by MOVING, one or two pixels, slowly, out of phase with its
## neighbours. It must never scale: it is interlocked with the furniture around it.
func _bob(tr: TextureRect, i: int) -> void:
	var base_y := tr.position.y
	var amp: float = BOB_MIN + fmod(float(i) * 0.7, BOB_MAX - BOB_MIN + 0.001)
	# A looping tween whose FIRST step has zero duration makes Godot spin ("Infinite
	# loop detected", out of tween.cpp) — so the phase interval is never zero.
	var tw := _loop()
	tw.tween_interval(0.05 + 0.41 * float(i))
	tw.tween_property(tr, "position:y", base_y - amp, BOB_SECS).set_trans(Tween.TRANS_SINE)
	tw.tween_property(tr, "position:y", base_y, BOB_SECS).set_trans(Tween.TRANS_SINE)


## A LOOP THAT A REBUILD CAN END. Tweens bind to the node that made them — this
## scene — not to the patch they animate, so a re-cast that frees the old patches
## leaves their loops running against freed instances. Every loop is held here and
## killed in _reset(), and the callbacks re-check their target besides.
func _loop() -> Tween:
	var tw := create_tween().set_loops()
	_tweens.append(tw)
	return tw


## A patch that shipped a second frame ACTS instead of bobbing: the factory already
## moved the parts of them that should move, bonded to their own body, so a bob on
## top of it would only smear that work by a pixel.
func _alternate(tr: TextureRect, a: Texture2D, b: Texture2D, i: int) -> void:
	if a == null or b == null:
		return
	var tw := _loop()
	tw.tween_interval(0.05 + 0.33 * float(i))
	tw.tween_callback(func(): _set_tex(tr, b))
	tw.tween_interval(F2_ON)
	tw.tween_callback(func(): _set_tex(tr, a))
	tw.tween_interval(F2_OFF)


func _blink(lids: Control, i: int) -> void:
	var gap := _rng.randf_range(BLINK_MIN, BLINK_MAX)
	var tw := _loop()
	tw.tween_interval(0.2 + 0.83 * float(i))
	tw.tween_callback(func(): _set_shown(lids, true))
	tw.tween_interval(BLINK_HOLD)
	tw.tween_callback(func(): _set_shown(lids, false))
	tw.tween_interval(gap)


static func _set_tex(tr: TextureRect, t: Texture2D) -> void:
	if is_instance_valid(tr):
		tr.texture = t


static func _set_shown(c: Control, v: bool) -> void:
	if is_instance_valid(c):
		c.visible = v


func _ready() -> void:
	if _life_pending:
		_begin_life()


func _process(delta: float) -> void:
	if _ambient == null or not is_instance_valid(_ambient) or _ambient_frames.is_empty():
		return
	_t += delta
	var i := int(_t * AMBIENT_FPS) % _ambient_frames.size()
	_ambient.texture = _ambient_frames[i]


# ═════════════════════════════════════════════════════════════════════════════
# THE TABLES
# ═════════════════════════════════════════════════════════════════════════════

## patches.json: name -> offset and size. Written flat, or under a "patches" key, and
## the pair itself as [x, y] or as {"x": …, "y": …} — every shape the producer has
## used is read, because a table we cannot parse costs the whole room.
func _patch_table(dir: String) -> Dictionary:
	var raw := _json_dict(dir + "/patches.json")
	if raw.has("patches") and raw["patches"] is Dictionary:
		raw = raw["patches"]
	var out: Dictionary = {}
	for k in raw:
		var v: Variant = raw[k]
		if not (v is Dictionary):
			continue
		var row: Dictionary = v
		out[String(k)] = {
			"offset": _pair(row, "offset", "x", "y"),
			"size": _pair(row, "size", "w", "h"),
		}
	return out


## spots.json: the regions, in the order the scene declares them. A dict of rows or a
## list of rows carrying their own id — both are read.
func _read_spots(dir: String) -> void:
	var raw := _json_dict(dir + "/spots.json")
	_founder_spot = String(raw.get("founder_spot", ""))
	var body: Variant = raw
	if raw.has("spots"):
		body = raw["spots"]
	if body is Dictionary:
		for k in (body as Dictionary):
			_spots[String(k)] = (body as Dictionary)[k]
			_spot_order.append(String(k))
	elif body is Array:
		for e in (body as Array):
			if not (e is Dictionary):
				continue
			var row: Dictionary = e
			var id := String(row.get("id", row.get("spot", "")))
			if id == "":
				continue
			_spots[id] = row
			_spot_order.append(id)


func _pair(row: Dictionary, key: String, ka: String, kb: String) -> Vector2:
	var v: Variant = row.get(key, null)
	if v is Array and (v as Array).size() >= 2:
		return Vector2(float((v as Array)[0]), float((v as Array)[1]))
	if v is Dictionary:
		var d: Dictionary = v
		return Vector2(float(d.get(ka, 0.0)), float(d.get(kb, 0.0)))
	if row.has(ka) and row.has(kb):
		return Vector2(float(row[ka]), float(row[kb]))
	return Vector2.ZERO


func _json_dict(path: String) -> Dictionary:
	if not FileAccess.file_exists(path):
		return {}
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(path))
	return parsed if parsed is Dictionary else {}


# ═════════════════════════════════════════════════════════════════════════════
# FILES — res:// is imported, user:// and absolute paths are raw bytes
# ═════════════════════════════════════════════════════════════════════════════

func _exists(path: String) -> bool:
	if path.begins_with("res://") and ResourceLoader.exists(path):
		return true
	return FileAccess.file_exists(path)


func _texture(path: String) -> Texture2D:
	if path == "":
		return null
	if path.begins_with("res://") and ResourceLoader.exists(path):
		var res: Variant = load(path)
		if res is Texture2D:
			return res
	# Freshly shipped art that has not been through --import yet, and every fixture
	# under user://, is decoded straight from its bytes. file_exists first, so a
	# missing frame costs a warning rather than an engine error in the run log.
	if not FileAccess.file_exists(path):
		return null
	var img := Image.new()
	if img.load(path) != OK or img.is_empty():
		return null
	return ImageTexture.create_from_image(img)


func _reset() -> void:
	for t in _tweens:
		var tw: Tween = t
		if tw != null and is_instance_valid(tw) and tw.is_valid():
			tw.kill()
	_tweens.clear()
	for c in get_children():
		remove_child(c)
		c.queue_free()
	_frame = null
	_blank = null
	_ambient = null
	_ambient_frames.clear()
	_meta.clear()
	_spots.clear()
	_spot_order.clear()
	_founder_spot = ""
	_eyes.clear()
	_placed.clear()
	_skipped.clear()
	_t = 0.0
	_life_pending = false
	set_process(false)


## The lids themselves. Drawn, not loaded — an ellipse of body ink over each eye.
class PatchLids extends Control:
	var eyes: Array = []                       # [[centre: Vector2, radius: Vector2], …]
	var ink := Color(0.118, 0.118, 0.118)

	func _draw() -> void:
		for e in eyes:
			var pair: Array = e
			var centre: Vector2 = pair[0]
			var radius: Vector2 = pair[1]
			draw_set_transform(centre, 0.0, radius)
			draw_circle(Vector2.ZERO, 1.0, ink)       # a unit circle, scaled = an ellipse
			draw_set_transform(Vector2.ZERO, 0.0, Vector2.ONE)
