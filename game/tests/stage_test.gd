extends SceneTree
## SceneStage contract test — docs/BLANK_SCENES_ARCHITECTURE.md §2 and §3.
## Run: godot --headless --path . --script tests/stage_test.gd
##
## HERMETIC BY CONSTRUCTION. The pose library, slots.json and the ambient deltas are
## being produced right now by other lanes, and no PNG in this repo is tracked by git,
## so a suite that read res://assets would pass or fail on whichever lane happened to
## have written a file that minute. Everything this suite asserts against is built
## here, under user://: a background plus its index, three pose sprites, one canonical
## sprite, and a slots table injected through stage._slots_override. The res:// library
## is touched only by the optional screenshot pass at the end.
##
## Optional: RUNWAY_STAGE_SHOT=<dir> godot --path . --script tests/stage_test.gd
## renders a real build over a real background (canonical cast sprites as stand-ins).

const FIX := "user://stage_fixture"
const SCENE_ID := "test_room/loft/night_steady_wide"
const SCENE_W := 256
const SCENE_H := 160

## Three typed slots, one per pose class, deep-first by prominence. The two occ rects
## are deliberately written in the two conventions a slots producer might emit —
## desk_1 as x/y/w/h, floor_1 as corners — so both readings are exercised.
const SLOTS := {
	"slots": [
		{"id": "desk_1", "pose_class": "sit_desk", "x": 50.0, "y": 110.0, "h": 80.0,
			"face": "left", "occ": [10, 120, 120, 40], "prominence": 1},
		{"id": "board", "pose_class": "stand_present", "x": 130.0, "y": 140.0, "h": 90.0,
			"face": "right", "occ": null, "prominence": 2},
		{"id": "floor_1", "pose_class": "stand", "x": 210.0, "y": 150.0, "h": 95.0,
			"face": "any", "occ": [200, 100, 260, 170], "prominence": 3},
	]
}

const STEPS := ["_test_full_build", "_test_unknown_activity", "_test_missing_scene",
	"_test_missing_pose", "_test_overflow_drops", "_test_no_slots_at_all"]

var _checks := 0
var _failed := false


func _init() -> void:
	call_deferred("_go")


func _go() -> void:
	await process_frame
	_build_fixtures()
	# quit() is DEFERRED and a failed assert() only unwinds the function it sits in, so
	# a suite that leans on either keeps running and prints PASS over its own failure.
	# The latch is checked between steps instead, and PASS has one path to it.
	for step in STEPS:
		call(String(step))
		if _failed:
			quit(1)
			return
	await _optional_shot()
	print("%d checks held" % _checks)
	print("STAGE TEST PASS")
	quit(0)


# ═════════════════════════════════════════════════════════════════════════════
# THE CONTRACT
# ═════════════════════════════════════════════════════════════════════════════

## A full build: three cofounders, three slots, three pose layers, nothing dropped —
## and each one graded down toward the room it is standing in.
func _test_full_build() -> void:
	var stage := _stage()
	var ok := stage.build(SCENE_ID, [
		{"who": "founder_pm", "doing": "types all night", "mood": "fine"},
		{"who": "cofd_tech", "doing": "presents the deck", "mood": "fine"},
		{"who": "cofd_sales", "doing": "calls investors", "mood": "fine"},
	])
	_ok(ok, "a full build must return true")
	_ok(stage.dropped().is_empty(), "nothing may drop when every pose has a slot")
	if not _need(stage.pose_layers().size() >= 3,
			"3 cast must produce >= 3 pose children, got %d" % stage.pose_layers().size()):
		return

	var got := stage.placements()
	# Founder first, then input order; each takes the lowest-prominence fitting slot.
	_ok(String(got[0]["who"]) == "founder_pm" and String(got[0]["slot_id"]) == "desk_1",
		"the founder must take the most prominent fitting slot")
	_ok(String(got[0]["pose"]) == "sit_desk_typing", "'types all night' must be sit_desk_typing")
	_ok(String(got[1]["pose"]) == "stand_present_pointer" and String(got[1]["slot_id"]) == "board",
		"'presents the deck' must be stand_present_pointer at the board")
	_ok(String(got[2]["pose"]) == "stand_phone" and String(got[2]["slot_id"]) == "floor_1",
		"'calls investors' must be stand_phone, and a stand slot takes any stand_* pose")
	for row in got:
		_ok(String(row["source"]) == "pose", "every character must come from the pose library here")

	# The room is a dark blue night loft, so nobody in it may be daylight-bright — and
	# the nudge is clamped, so nobody goes black either.
	var tint: Color = stage.pose_layers()[0].modulate
	_ok(tint.r < 1.0, "a night room must grade its cast down, got %.3f" % tint.r)
	_ok(tint.r >= 0.85 and tint.g >= 0.85 and tint.b >= 0.85, "the grade must clamp at 0.85")
	_ok(tint.b > tint.r, "a blue room must leave its cast cooler, not just darker")

	# The occ crops are the scene's own pixels drawn back over the cast: both slots
	# that declare one must have produced a layer above their character.
	_ok(stage.get_child_count() >= 6, "scene + 3 poses + 2 occ crops, got %d children" % stage.get_child_count())
	stage.queue_free()


## Free text the table has never seen is stand_neutral, always — and a burnt mood
## overrides within the class rather than multiplying the library.
func _test_unknown_activity() -> void:
	_ok(SceneStage.pose_for("yodelling at a houseplant", "fine") == "stand_neutral",
		"an unknown activity must map to stand_neutral")
	_ok(SceneStage.pose_for("yodelling at a houseplant", "burnt") == "stand_slumped",
		"burnt + stand must fold into stand_slumped")
	_ok(SceneStage.pose_for("types all night", "burnt") == "sit_desk_slumped",
		"burnt + sit_desk_typing must fold into sit_desk_slumped")

	var stage := _stage()
	var ok := stage.build(SCENE_ID, [{"who": "mystery", "doing": "yodelling at a houseplant", "mood": "fine"}])
	_ok(ok, "an unknown activity must still build")
	var got := stage.placements()
	if not _need(got.size() == 1, "the unknown verb must still reach the frame"):
		return
	_ok(String(got[0]["pose"]) == "stand_neutral", "the unknown verb must be placed as stand_neutral")
	# A neutral stander fits neither the desk nor the board: sit_desk refuses it, and
	# so does stand_present, because a slot named for presenting expects a presenter.
	# It walks past both to the plain floor slot rather than being mis-posed at either.
	_ok(String(got[0]["slot_id"]) == "floor_1",
		"stand_neutral must take the plain stand slot, not the desk or the board, got '%s'" % String(got[0]["slot_id"]))
	stage.queue_free()


## A scene that is not in the index is the ONE thing that returns false, so the caller
## can fall back. It must not guess a filename.
func _test_missing_scene() -> void:
	var stage := _stage()
	var ok := stage.build("nowhere/nothing/never_wide", [
		{"who": "founder_pm", "doing": "types all night", "mood": "fine"},
	])
	_ok(not ok, "a fabricated scene id must return false")
	_ok(stage.pose_layers().is_empty(), "a missed scene must draw nothing at all")
	stage.queue_free()


## No pose sprite is not a missing character: the canonical sprite stands in.
func _test_missing_pose() -> void:
	var stage := _stage()
	var ok := stage.build(SCENE_ID, [{"who": "ghost", "doing": "types all night", "mood": "fine"}])
	_ok(ok, "a missing pose must not fail the build")
	var got := stage.placements()
	if not _need(got.size() == 1, "the character must still be in the frame"):
		return
	_ok(String(got[0]["source"]) == "canonical", "a missing pose must fall back to the canonical sprite")
	_ok(stage.dropped().is_empty(), "a character with a canonical sprite is never dropped")
	stage.queue_free()


## Six characters into three slots: three stand, three are dropped. A dropped
## character is a warning, never an error, and never a mis-posed body.
func _test_overflow_drops() -> void:
	var stage := _stage()
	var crowd: Array = []
	for i in 6:
		crowd.append({"who": "crowd", "doing": "types all night", "mood": "fine"})
	var ok := stage.build(SCENE_ID, crowd)
	_ok(ok, "an overfull cast must still build")
	_ok(stage.dropped().size() == 3, "the other 3 must be dropped, got %d" % stage.dropped().size())
	for row in stage.dropped():
		_ok(String((row as Dictionary)["reason"]) == "no free slot", "every drop must carry its reason")
	if not _need(stage.placements().size() == 3, "3 slots take 3, got %d" % stage.placements().size()):
		return
	# The two that could not sit were moved to stand slots rather than mis-posed.
	var poses: Array = []
	for row in stage.placements():
		poses.append(String((row as Dictionary)["pose"]))
	_ok(poses[0] == "sit_desk_typing", "the one real desk must still be typed at")
	_ok(poses[1] == "stand_neutral" and poses[2] == "stand_neutral",
		"a seated pose with no seat must stand, not float at a desk that is not there")
	stage.queue_free()


## No typed slots and no crew marks either: everyone degrades out, the room still
## renders, and build() still reports the scene it found.
func _test_no_slots_at_all() -> void:
	var stage := _stage()
	stage._slots_override = {}
	var ok := stage.build(SCENE_ID, [
		{"who": "founder_pm", "doing": "types all night", "mood": "fine"},
		{"who": "cofd_tech", "doing": "calls investors", "mood": "burnt"},
	])
	_ok(ok, "a slotless scene still has a scene: build must return true")
	_ok(stage.pose_layers().is_empty(), "with nowhere to stand, nobody is placed")
	_ok(stage.dropped().size() == 2, "both must be dropped, not mis-posed")
	_ok(stage.get_child_count() == 1, "the room itself must still be drawn")
	stage.queue_free()


# ═════════════════════════════════════════════════════════════════════════════
# FIXTURES
# ═════════════════════════════════════════════════════════════════════════════

func _stage() -> SceneStage:
	var stage := SceneStage.new()
	stage._bg_root = FIX + "/bg"
	stage._poses_root = FIX + "/poses"
	stage._cast_root = FIX + "/cast"
	stage._slots_override = SLOTS.duplicate(true)
	root.add_child(stage)          # in the tree before build: tweens need a tree
	return stage


func _build_fixtures() -> void:
	# The room: a dark blue night loft, so the grade pass has something to pull toward.
	_png("%s/bg/room.png" % FIX, SCENE_W, SCENE_H, Color(0.10, 0.12, 0.20, 1.0))
	_json("%s/bg/index.json" % FIX, {SCENE_ID: "room.png"})
	# Three poses, one per class the slots offer.
	for pair in [["founder_pm", "sit_desk_typing"], ["cofd_tech", "stand_present_pointer"],
			["cofd_sales", "stand_phone"], ["mystery", "stand_neutral"],
			["crowd", "sit_desk_typing"], ["crowd", "stand_neutral"]]:
		var p: Array = pair
		_png("%s/poses/%s/%s.png" % [FIX, String(p[0]), String(p[1])], 48, 64, Color(0.9, 0.9, 0.9, 1.0))
	# One pose ships meta, so the seat anchor and the blink coords are exercised.
	_json("%s/poses/founder_pm/sit_desk_typing.json" % FIX,
		{"eyes": [[19, 17], [29, 18]], "anchor": "seat", "w": 48, "h": 64})
	# `ghost` deliberately has NO pose of any kind — only the canonical sprite.
	_png("%s/cast/cast_ghost_fine/sprite.png" % FIX, 40, 70, Color(0.8, 0.8, 0.8, 1.0))


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
	print("STAGE TEST FAIL — ", why)


## For checks that guard an index: false means the caller must return immediately,
## because reading placements[0] of an empty array is a crash, not a test failure.
func _need(cond: bool, why: String) -> bool:
	_ok(cond, why)
	return cond and not _failed


# ═════════════════════════════════════════════════════════════════════════════
# OPTIONAL: a real build over a real background, for eyes rather than asserts
# ═════════════════════════════════════════════════════════════════════════════

## RUNWAY_STAGE_SHOT=<dir> and no --headless: assemble three of the existing cast
## sprites onto a real 1536x1024 blank scene and save the frame.
##
## Nothing is injected here — this is the path the game takes TODAY, all the way down
## the degrade ladder: no slots.json, so the scene's own crew marks become stand slots
## at their own depth scale; no pose library, so every character falls back to its
## canonical sprite. It is the fallback rendering an honest frame, on purpose.
func _optional_shot() -> void:
	var out_dir := OS.get_environment("RUNWAY_STAGE_SHOT")
	if out_dir == "":
		return
	DirAccess.make_dir_recursive_absolute(out_dir)
	var scene := _first_real_scene()
	if scene == "":
		print("no res:// background on disk — screenshot skipped")
		return
	var stage := SceneStage.new()
	root.add_child(stage)
	var ok := stage.build(scene, [
		{"who": "founder_pm", "doing": "types all night", "mood": "burnt"},
		{"who": "cofd_tech", "doing": "presents the deck", "mood": "fine"},
		{"who": "cofd_sales", "doing": "calls investors", "mood": "fine"},
	])
	print("shot scene: %s  built=%s  placed=%d  dropped=%d" % [scene, str(ok),
		stage.placements().size(), stage.dropped().size()])
	if not ok:
		return
	await create_timer(0.8).timeout
	await RenderingServer.frame_post_draw
	var img := root.get_viewport().get_texture().get_image()
	img.save_png("%s/stage_over_real_background.png" % out_dir)
	print("STAGE SHOT: %s/stage_over_real_background.png" % out_dir)


## The first id in the shipped index whose png is actually on disk. None of the art is
## tracked by git, so a clean checkout finds nothing and simply skips the shot.
func _first_real_scene() -> String:
	var path := "res://assets/backgrounds/index.json"
	if not FileAccess.file_exists(path):
		return ""
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(path))
	if not (parsed is Dictionary):
		return ""
	var want := OS.get_environment("RUNWAY_STAGE_SCENE")
	var index: Dictionary = parsed
	if want != "" and index.has(want):
		if ResourceLoader.exists("res://assets/backgrounds/%s" % String(index[want])):
			return want
	for key in index:
		if ResourceLoader.exists("res://assets/backgrounds/%s" % String(index[key])):
			return String(key)
	return ""
