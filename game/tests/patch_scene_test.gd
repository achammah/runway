extends SceneTree
## PatchScene contract test — docs/BLANK_SCENES_ARCHITECTURE.md §8.
## Run: godot --headless --path . --script tests/patch_scene_test.gd
##
## HERMETIC BY CONSTRUCTION. The era scenes are being cut RIGHT NOW by the scene
## factory and no PNG in this repo is tracked by git, so a suite that read
## res://assets/patch_scenes would pass or fail on whichever minute a file landed.
## Every byte this suite asserts against is written here, under user://: two tiny
## blanks, five patches, the three tables and a two-frame ambient loop. The
## loader is pointed at that tree through `ps._root`.

const FIX := "user://patch_fixture"

## The full fixture scene, shaped exactly the way the factory writes one. Three of
## its contents are deliberate traps:
##   the founder's spot is called `desk` — nothing in the id says "founder", so only
##     the `founder_spot` field in spots.json can find it;
##   `back__hacker` is a SECOND patch of the founder's archetype, at a spot that is
##     NOT theirs. The founder must still take `desk`;
##   `floor__empty` is the blank's own pixels — never a character, not even for a
##     cast member literally called "empty".
const LOFT := "loft"
const STILL := "still"          # the same scene with no ambient deltas on disk
const GONE := "nope"            # an era that ships no scene at all
const HALF := "halfbuilt"       # a blank on disk, its patches not cut yet

const OFF := {
	"desk__hacker": [10, 60],
	"desk__hustler": [10, 60],
	"back__hacker": [150, 20],
	"bench__cofd_tech": [120, 70],
	"floor__empty": [4, 4],
}

const STEPS := ["_test_full_build", "_test_unknown_who_is_skipped", "_test_missing_era",
	"_test_missing_ambient_still_builds", "_test_f2_alternation", "_test_empty_is_never_cast",
	"_test_archetype_picks_the_chair", "_test_undrawn_archetype_leaves_the_chair_empty",
	"_test_a_hire_is_never_the_cofounder", "_test_a_blank_alone_is_not_a_scene"]

## Steps that must run frames rather than just assert, so the life tweens actually
## tick. They are awaited from _go, which is why they are not in STEPS.
const AWAITED := ["_test_recast_ends_the_old_life"]

var _checks := 0
var _failed := false


func _init() -> void:
	call_deferred("_go")


func _go() -> void:
	await process_frame
	_build_fixtures()
	# quit() is DEFERRED and a failed assert() only unwinds the function it sits in,
	# so a suite leaning on either keeps running and prints PASS over its own failure.
	# The latch is checked between steps, and PASS has exactly one path to it.
	for step in STEPS:
		call(String(step))
		if _failed:
			quit(1)
			return
	for step in AWAITED:
		await call(String(step))
		if _failed:
			quit(1)
			return
	print("%d checks held" % _checks)
	print("PATCH TEST PASS")
	quit(0)


# ═════════════════════════════════════════════════════════════════════════════
# THE CONTRACT
# ═════════════════════════════════════════════════════════════════════════════

## Two cast members, two spots, and each patch drawn at the offset it was cut from.
func _test_full_build() -> void:
	var ps := _scene()
	var ok := ps.build(LOFT, [
		{"who": "founder", "kind": "founder", "mood": "fine", "doing": "types all night"},
		{"who": "tech", "kind": "cofounder", "mood": "burnt", "doing": "fixes the build"},
	])
	_ok(ok, "a scene that ships a blank must build")
	if not _need(ps.placements().size() == 2,
			"2 cast with 2 patches must place 2, got %d" % ps.placements().size()):
		return
	_ok(ps.skipped().is_empty(), "nobody may be skipped when everybody has a patch")
	var by_who := _by_who(ps)
	# THE FOUNDER TAKES THE FOUNDER SPOT — named only in spots.json, and back__hacker
	# exists to catch a chooser that went looking for the archetype instead.
	_ok(String((by_who["founder"] as Dictionary).get("spot", "")) == "desk",
		"the founder must take the named founder spot, got '%s'" % (by_who["founder"] as Dictionary).get("spot", ""))
	# THE RUN'S VOCABULARY IS TRANSLATED: "tech" is drawn as cofd_tech.
	_ok(String((by_who["tech"] as Dictionary).get("name", "")) == "bench__cofd_tech",
		"'tech' must resolve to its cofd_ patch, got '%s'" % (by_who["tech"] as Dictionary).get("name", ""))
	# THE PATCH IS DRAWN WHERE IT WAS CUT FROM — patches.json, not the spot region.
	var off: Vector2 = (by_who["founder"] as Dictionary).get("offset", Vector2.ZERO)
	_ok(off == Vector2(10, 60), "the founder patch must sit at its recorded offset, got %s" % off)
	# AND AT ITS OWN SIZE: no scaling, ever.
	var sz: Vector2 = (by_who["founder"] as Dictionary).get("size", Vector2.ZERO)
	_ok(sz == Vector2(40, 50), "a patch must keep its own pixel size, got %s" % sz)
	_ok(ps.ambient_frames() == 2, "both ambient deltas must load, got %d" % ps.ambient_frames())
	ps.queue_free()


## A `who` this scene has no patch for is SKIPPED — with a warning, not an error, and
## never with somebody else's sprite standing in for them. The spot stays empty.
func _test_unknown_who_is_skipped() -> void:
	var ps := _scene()
	var ok := ps.build(LOFT, [
		{"who": "founder", "kind": "founder", "mood": "fine", "doing": "types all night"},
		{"who": "ghost", "kind": "cofounder", "mood": "fine", "doing": "was never drawn"},
		{"who": "tech", "kind": "cofounder", "mood": "fine", "doing": "fixes the build"},
	])
	_ok(ok, "an unknown cast member must not fail the build")
	_ok(ps.placements().size() == 2,
		"the unknown must be skipped, not substituted — placed %d" % ps.placements().size())
	_ok(ps.skipped().has("ghost"), "the unknown must be reported as skipped")
	# NO SUBSTITUTION: the free spots the ghost could have taken are still free, and
	# nobody is standing in a patch drawn for somebody else.
	var want := {"founder": "desk__hacker", "tech": "bench__cofd_tech"}
	for p in ps.placements():
		var row: Dictionary = p
		var who := String(row.get("who", ""))
		_ok(String(row.get("name", "")) == String(want.get(who, "")),
			"'%s' must be drawn by its own patch, got '%s'" % [who, row.get("name", "")])
	ps.queue_free()


## An era that ships no scene returns false and touches nothing, so the screen keeps
## the room it is already standing in.
func _test_missing_era() -> void:
	var ps := _scene()
	var ok := ps.build(GONE, [{"who": "founder", "mood": "fine", "doing": "waits"}])
	_ok(not ok, "an era with no blank.png must return false")
	_ok(ps.placements().is_empty(), "a failed build must place nobody")
	ps.queue_free()


## No ambient deltas is a still room, not a broken one.
func _test_missing_ambient_still_builds() -> void:
	var ps := _scene()
	var ok := ps.build(STILL, [{"who": "founder", "mood": "fine", "doing": "types all night"}])
	_ok(ok, "a scene with no ambient/ must still build")
	_ok(ps.ambient_frames() == 0, "a scene with no ambient/ must report 0 delta frames")
	_ok(ps.placements().size() == 1, "the patch must still be drawn without ambient")
	ps.queue_free()


## A patch that shipped a second frame plays it; one that did not, bobs instead.
func _test_f2_alternation() -> void:
	var ps := _scene()
	var ok := ps.build(LOFT, [
		{"who": "founder", "kind": "founder", "mood": "fine", "doing": "types all night"},
		{"who": "tech", "kind": "cofounder", "mood": "fine", "doing": "fixes the build"},
	])
	_ok(ok, "the build for the f2 check must succeed")
	var alt := ps.alternating()
	_ok(alt.has("bench__cofd_tech"),
		"bench__cofd_tech ships an f2 and must alternate, got %s" % str(alt))
	_ok(not alt.has("desk__hacker"), "desk__hacker ships no f2 and must bob, not alternate")
	ps.queue_free()


## `__empty` is the blank's own pixels. It is a spot's EMPTY rendition, never a
## character, and asking for it by name must not hand it out.
func _test_empty_is_never_cast() -> void:
	var ps := _scene()
	var ok := ps.build(LOFT, [{"who": "empty", "mood": "fine", "doing": "nothing"}])
	_ok(ok, "a cast of nobody must still build the room")
	_ok(ps.placements().is_empty(), "'empty' is not a character and must never be placed")
	_ok(ps.skipped().has("empty"), "'empty' must be reported as having no patch")
	ps.queue_free()


## The founder's chair ships one rendition per archetype. WHICH one is theirs is the
## run's, not the scene's.
func _test_archetype_picks_the_chair() -> void:
	var ps := _scene()
	ps.archetype = "hustler"
	var ok := ps.build(LOFT, [{"who": "founder", "kind": "founder", "mood": "fine", "doing": "sells"}])
	_ok(ok, "a hustler founder must build")
	if not _need(ps.placements().size() == 1, "the hustler must be seated"):
		return
	var row: Dictionary = ps.placements()[0]
	_ok(String(row.get("name", "")) == "desk__hustler",
		"the hustler must get their own rendition of the chair, got '%s'" % row.get("name", ""))
	ps.queue_free()


## RULE 2, at the seat it costs the most: an archetype this scene never drew leaves
## the founder's chair EMPTY. Seating a different person in it is the substitution
## the whole model exists to prevent.
func _test_undrawn_archetype_leaves_the_chair_empty() -> void:
	var ps := _scene()
	ps.archetype = "exfaang"          # → "pm", and no desk__pm.png was ever cut
	var ok := ps.build(LOFT, [
		{"who": "founder", "kind": "founder", "mood": "fine", "doing": "plans"},
		{"who": "tech", "kind": "cofounder", "mood": "fine", "doing": "fixes the build"},
	])
	_ok(ok, "an undrawn founder must not fail the room")
	_ok(ps.skipped().has("founder"), "an undrawn founder must be reported as skipped")
	for p in ps.placements():
		var row: Dictionary = p
		_ok(String(row.get("spot", "")) != "desk",
			"nobody may be seated in the founder's chair in their place")
	ps.queue_free()


## A hire is drawn by the scene's OWN employee rendition, never as a second copy of
## the cofounder whose job it shares. This scene has no employee, so the hire is
## skipped — and the cofounder keeps their patch.
func _test_a_hire_is_never_the_cofounder() -> void:
	var ps := _scene()
	ps.archetype = "hacker"
	var ok := ps.build(LOFT, [
		{"who": "founder", "kind": "founder", "mood": "fine", "doing": "types all night"},
		{"who": "tech", "kind": "cofounder", "mood": "fine", "doing": "fixes the build"},
		{"who": "tech", "kind": "employee", "mood": "burnt", "doing": "closes tickets"},
	])
	_ok(ok, "a hire this scene never drew must not fail the room")
	_ok(ps.placements().size() == 2,
		"the hire must be skipped, not drawn twice — placed %d" % ps.placements().size())
	_ok(ps.skipped().has("tech"), "the hire must be reported as skipped")
	ps.queue_free()


## A directory holding only a blank is a scene STILL BEING CUT, and it looks exactly
## like a room everyone has walked out of. Mounting it would empty the game's room
## for as long as the factory was still working — so it does not count as a scene.
## This is not hypothetical: it is what the five era directories looked like for the
## first minutes of their production.
func _test_a_blank_alone_is_not_a_scene() -> void:
	var ps := _scene()
	var ok := ps.build(HALF, [
		{"who": "founder", "kind": "founder", "mood": "fine", "doing": "types all night"},
	])
	_ok(not ok, "a blank with no patches cut yet must not mount as an empty room")
	_ok(ps.placements().is_empty(), "a half-built scene must place nobody")
	# The mount key asks the same question, and must give the same answer.
	_ok(not PatchScene._shipped("%s/%s" % [FIX, HALF]),
		"a half-built directory must not count as a shipped scene")
	_ok(PatchScene._shipped("%s/%s" % [FIX, LOFT]),
		"a finished directory must count as a shipped scene")
	ps.queue_free()


## A DEPARTURE REBUILDS THE ROOM, and rebuilding frees every patch the old cast was
## standing in. The bob, the blink and the two-frame loop are bound to the SCENE, not
## to the patch they animate, so unless the rebuild ends them they keep running
## against freed instances — which is a crash in the room, one week in. This step
## runs frames on both sides of the rebuild so the loops actually tick; a leaked one
## shows up as SCRIPT ERROR in the run output, which the gate counts.
func _test_recast_ends_the_old_life() -> void:
	var ps := _scene()
	_ok(ps.build(LOFT, [
		{"who": "founder", "kind": "founder", "mood": "fine", "doing": "types all night"},
		{"who": "tech", "kind": "cofounder", "mood": "fine", "doing": "fixes the build"},
	]), "the first build must succeed")
	for i in 8:
		await process_frame
	# the technical cofounder walks out
	_ok(ps.build(LOFT, [
		{"who": "founder", "kind": "founder", "mood": "burnt", "doing": "types all night"},
	]), "the re-cast must succeed")
	_ok(ps.placements().size() == 1, "only the founder is left in the room")
	for i in 8:
		await process_frame
	_ok(ps.placements().size() == 1, "the room must still hold exactly the new cast")
	ps.queue_free()


# ═════════════════════════════════════════════════════════════════════════════
# THE FIXTURES
# ═════════════════════════════════════════════════════════════════════════════

func _scene() -> PatchScene:
	var ps := PatchScene.new()
	ps._root = FIX
	ps.archetype = "hacker"
	root.add_child(ps)          # in the tree before build: the life tweens need one
	return ps


func _build_fixtures() -> void:
	var loft := "%s/%s" % [FIX, LOFT]
	_png(loft + "/blank.png", 240, 160, Color(0.10, 0.12, 0.20, 1.0))
	_png(loft + "/populated.png", 240, 160, Color(0.12, 0.14, 0.22, 1.0))
	_png(loft + "/patches/desk__hacker.png", 40, 50, Color(0.9, 0.9, 0.88, 1.0))
	_png(loft + "/patches/desk__hustler.png", 40, 50, Color(0.88, 0.9, 0.88, 1.0))
	_png(loft + "/patches/back__hacker.png", 30, 30, Color(0.8, 0.8, 0.78, 1.0))
	_png(loft + "/patches/bench__cofd_tech.png", 36, 44, Color(0.85, 0.86, 0.9, 1.0))
	_png(loft + "/patches/bench__cofd_tech__f2.png", 36, 44, Color(0.84, 0.87, 0.9, 1.0))
	_png(loft + "/patches/floor__empty.png", 20, 20, Color(0.2, 0.2, 0.2, 1.0))
	_png(loft + "/ambient/d_00.png", 240, 160, Color(0, 0, 0, 1.0))
	_png(loft + "/ambient/d_01.png", 240, 160, Color(0.03, 0.02, 0.0, 1.0))
	var patches: Dictionary = {}
	for k in OFF:
		var xy: Array = OFF[k]
		patches[String(k)] = {"offset": [xy[0], xy[1]], "size": [40, 50],
			"spot": _spot(String(k)), "who": _who(String(k))}
	_json(loft + "/patches.json", patches)
	# spots.json exactly as the factory writes it: a corner box per spot, and the
	# founder's chair NAMED — the id itself says nothing about who sits in it.
	_json(loft + "/spots.json", {
		"scene": LOFT, "size": [240, 160], "founder_spot": "desk",
		"spots": {
			"back": [140, 10, 190, 60],
			"desk": [0, 50, 60, 120],
			"bench": [110, 60, 170, 130],
			"floor": [0, 0, 30, 30],
		},
		"cast": {"desk": ["hacker", "hustler"], "bench": ["cofd_tech"], "back": ["hacker"]},
	})
	# eye boxes are PATCH-LOCAL: they are measured on the cut patch, not on the room.
	_json(loft + "/eyes.json", {
		"desk__hacker": [[10, 8, 20, 16, 80], [24, 8, 34, 16, 80]],
		"bench__cofd_tech": [[8, 6, 17, 14, 72], [21, 6, 30, 14, 72]],
	})
	# The same room with the deltas never produced.
	var still := "%s/%s" % [FIX, STILL]
	_png(still + "/blank.png", 240, 160, Color(0.10, 0.12, 0.20, 1.0))
	_png(still + "/patches/desk__hacker.png", 40, 50, Color(0.9, 0.9, 0.88, 1.0))
	_json(still + "/patches.json", {"desk__hacker": {"offset": [10, 60]}})
	_json(still + "/spots.json", {"founder_spot": "desk", "spots": {"desk": [0, 50, 60, 120]}})
	# A scene caught mid-production: the blank is cut, nobody is.
	_png("%s/%s/blank.png" % [FIX, HALF], 240, 160, Color(0.10, 0.12, 0.20, 1.0))


func _spot(patch_name: String) -> String:
	var parts := patch_name.split("__", false)
	parts.remove_at(parts.size() - 1)
	return String("__").join(parts)


func _who(patch_name: String) -> String:
	var parts := patch_name.split("__", false)
	return String(parts[parts.size() - 1])


func _by_who(ps: PatchScene) -> Dictionary:
	var out: Dictionary = {}
	for p in ps.placements():
		var row: Dictionary = p
		out[String(row.get("who", ""))] = row
	return out


func _png(path: String, w: int, h: int, c: Color) -> void:
	DirAccess.make_dir_recursive_absolute(path.get_base_dir())
	var img := Image.create_empty(w, h, false, Image.FORMAT_RGBA8)
	img.fill(c)
	img.save_png(path)


func _json(path: String, data: Variant) -> void:
	DirAccess.make_dir_recursive_absolute(path.get_base_dir())
	var f := FileAccess.open(path, FileAccess.WRITE)
	if f != null:
		f.store_string(JSON.stringify(data))
		f.close()


func _ok(cond: bool, why: String) -> void:
	if _failed:
		return
	_checks += 1
	if cond:
		return
	_failed = true
	print("PATCH TEST FAIL — ", why)


## For checks that guard an index: false means the caller must return immediately,
## because reading placements[0] of an empty array is a crash, not a test failure.
func _need(cond: bool, why: String) -> bool:
	_ok(cond, why)
	return cond and not _failed
