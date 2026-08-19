class_name SceneDirector
extends RefCounted
## THE GENERATIVE SCENE PIPELINE — owned by MAIN.
##
## Turns "what just happened" into "the room you are looking at". Every week:
##
##   1. the DM has already returned the consequence text AND scene facets
##   2. resolve() picks a background from the 516-entry library by facets
##   3. compose() sends that background + EVERY character in the scene to
##      seedream-v5.0-pro/edit, which paints them into the room with matching
##      light and real contact shadows
##   4. the player reads the consequences on LoadingScreen while that renders
##   5. the finished scene opens
##
## WHY AN EDIT AND NOT A COMPOSITE. Pasting sprites gives "assembled, not organic",
## the owner's standing defect. The edit blends the cast into the room's own light.
## Measured cost: 67s with one character, 113.6s with four. That is why the reading
## beat exists, and why the request starts the instant the week locks.
##
## WHY THE MODEL NEVER NAMES A FILE. It emits facets; `resolve()` drops them in a
## fixed priority order until something exists. A hallucinated filename would be a
## black screen; a dropped facet is just a slightly less specific room.
##
## NOVEL SITUATIONS. When the DM wants somewhere the library does not hold, it sets
## `novel` with a description and the director generates that background first, then
## composes on it, and caches it into the library so it is free next time.

signal progress(fraction: float)      ## 0..1, for the reading beat's pen stroke
signal ready(image_path: String)      ## the composed scene is on disk
signal failed(reason: String)

const MANIFEST := "res://assets/backgrounds/manifest.json"
const CACHE_DIR := "user://generated_scenes"
## Facets are dropped in this order until a background matches. Framing first
## because it matters least; family last because it is the story.
const DROP_ORDER := ["framing", "time", "condition", "place", "family"]

var _entries: Array = []
var _http: HTTPRequest
var _tree: SceneTree

func _init(tree: SceneTree) -> void:
	_tree = tree
	if FileAccess.file_exists(MANIFEST):
		var d = JSON.parse_string(FileAccess.get_file_as_string(MANIFEST))
		if d is Array:
			_entries = d
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(CACHE_DIR))

## Pick a background by facets. Returns {id, dropped} — `dropped` names the first
## facet that had to be given up, or "exact". Never returns empty while the library
## has a single entry, because a scene must always resolve.
func resolve(want: Dictionary) -> Dictionary:
	if _entries.is_empty():
		return {"id": "", "dropped": "empty_library"}
	var have := want.duplicate()
	var order: Array = [""] + DROP_ORDER
	for drop in order:
		if drop != "":
			have.erase(drop)
		for e in _entries:
			var ok := true
			for k in have:
				if String(e.get(k, "")) != String(have[k]):
					ok = false
					break
			if ok:
				return {"id": String(e.get("id", "")), "dropped": (drop if drop != "" else "exact")}
	return {"id": String((_entries[0] as Dictionary).get("id", "")), "dropped": "fallback"}

## The prompt that paints the cast into the room. Everything the model must not do
## is stated as a prohibition, because "keep the room the same" is the instruction
## it is most likely to drift on.
func compose_prompt(cast: Array, beat: String) -> String:
	var roster := ""
	for i in cast.size():
		var c: Dictionary = cast[i]
		roster += "Character %d is the %s, %s. " % [i + 1, String(c.get("role", "founder")),
				String(c.get("doing", "at work"))]
	return ("The FIRST image is the room. EVERY image after it is one character who must appear in "
		+ "the finished scene. Place all %d characters into that room. " % cast.size()
		+ roster
		+ (("This week: " + beat + " ") if beat != "" else "")
		+ "Put them at plausible working positions spread across the room, casually overlapping "
		+ "rather than lined up, each one partly behind a piece of furniture so they sit IN the "
		+ "room and not on top of it. "
		+ "EVERY character must appear EXACTLY ONCE: none omitted, none duplicated, no extra "
		+ "creatures invented. "
		+ "Keep the room, its furniture, its walls, its lighting and its camera EXACTLY as the "
		+ "first image — do not redraw, move, add or remove anything belonging to the room. "
		+ "Match each character's lighting to the room's own light and give each a soft contact "
		+ "shadow where it meets the floor. Keep every character exactly on-model as drawn in its "
		+ "own reference image, distinguished ONLY by the props it carries. "
		# The edit drifts here specifically: a pilot compose gave one character a
		# MOUTH. The style block already forbids it, but the compose prompt is the
		# operative instruction, so it has to say so itself.
		+ "Every character is a solid ink-black bean with ONLY two blank white oval eyes. "
		+ "NO mouths, NO noses, NO ears, NO eyebrows, NO pupils, NO clothing of any kind. "
		+ "Each has one ink cowlick spike and tiny cream sneakers. Do not add a face. "
		+ "Leave the top tenth and the bottom seventh of the frame calm and uncluttered, and keep "
		+ "the middle of the lowest quarter especially empty: interface sits there.")

## Fire the compose. Non-blocking: the caller shows the reading beat and awaits
## `ready` or `failed`. A failure is never fatal — the caller keeps the old room.
func compose(background_url: String, cast_urls: Array, cast: Array, beat: String, out_name: String) -> void:
	if background_url == "":
		failed.emit("no background resolved")
		return
	var images: Array = [background_url]
	images.append_array(cast_urls)
	var body := {
		"model": "bytedance/seedream-v5.0-pro/edit",
		"prompt": compose_prompt(cast, beat),
		"images": images,
		"size": "2048*1360",
		"output_format": "png",
	}
	_run(body, out_name)

func _run(body: Dictionary, out_name: String) -> void:
	# progress is REPORTED, never faked: the pen stroke on the reading beat moves
	# when something real happens, and otherwise waits.
	progress.emit(0.05)
	var key := _atlas_key()
	if key == "":
		failed.emit("no atlas key")
		return
	var http := HTTPRequest.new()
	_tree.root.add_child(http)
	var err := http.request("https://api.atlascloud.ai/api/v1/model/generateImage",
			PackedStringArray(["Content-Type: application/json", "Authorization: Bearer " + key]),
			HTTPClient.METHOD_POST, JSON.stringify(body))
	if err != OK:
		http.queue_free()
		failed.emit("request failed: %d" % err)
		return
	var res: Array = await http.request_completed
	http.queue_free()
	var parsed = JSON.parse_string((res[3] as PackedByteArray).get_string_from_utf8())
	if not (parsed is Dictionary) or not (parsed as Dictionary).has("data"):
		failed.emit("bad create response")
		return
	var jid := String(((parsed as Dictionary)["data"] as Dictionary).get("id", ""))
	if jid == "":
		failed.emit("no job id")
		return
	progress.emit(0.15)
	await _poll(jid, key, out_name)

func _poll(jid: String, key: String, out_name: String) -> void:
	# ~113s worst case with a full crew, so poll steadily and report honest motion
	for i in 90:
		await _tree.create_timer(2.0).timeout
		var http := HTTPRequest.new()
		_tree.root.add_child(http)
		var err := http.request("https://api.atlascloud.ai/api/v1/model/prediction/" + jid,
				PackedStringArray(["Authorization: Bearer " + key]), HTTPClient.METHOD_GET)
		if err != OK:
			http.queue_free()
			continue
		var res: Array = await http.request_completed
		http.queue_free()
		var parsed = JSON.parse_string((res[3] as PackedByteArray).get_string_from_utf8())
		if not (parsed is Dictionary):
			continue
		var data: Dictionary = (parsed as Dictionary).get("data", {})
		var status := String(data.get("status", ""))
		# the render takes about 110s at worst; creep toward 0.9 and let the
		# download own the last tenth
		progress.emit(minf(0.9, 0.15 + float(i) * 0.014))
		if status in ["completed", "succeeded"]:
			var outs: Array = data.get("outputs", [])
			if outs.is_empty():
				failed.emit("completed with no output")
				return
			await _download(String(outs[0]), out_name)
			return
		if status == "failed":
			failed.emit(String(data.get("error", "generation failed")))
			return
	failed.emit("timed out")

func _download(url: String, out_name: String) -> void:
	var http := HTTPRequest.new()
	_tree.root.add_child(http)
	var err := http.request(url)
	if err != OK:
		http.queue_free()
		failed.emit("download request failed")
		return
	var res: Array = await http.request_completed
	http.queue_free()
	var bytes: PackedByteArray = res[3]
	# a partial body written as a file is how two truncated scenes shipped before;
	# a PNG that does not end in IEND is not a PNG
	if bytes.size() < 4096 or bytes.slice(bytes.size() - 8, bytes.size() - 4).get_string_from_ascii() != "IEND":
		failed.emit("incomplete image (%d bytes)" % bytes.size())
		return
	var path := "%s/%s.png" % [CACHE_DIR, out_name]
	var f := FileAccess.open(path, FileAccess.WRITE)
	if f == null:
		failed.emit("cannot write cache")
		return
	f.store_buffer(bytes)
	f.close()
	progress.emit(1.0)
	ready.emit(path)

func _atlas_key() -> String:
	if OS.has_environment("ATLASCLOUD_API_KEY"):
		return OS.get_environment("ATLASCLOUD_API_KEY")
	# the game reads its keys the same way the rest of the LLM layer does
	if FileAccess.file_exists("res://.env"):
		for line in FileAccess.get_file_as_string("res://.env").split("\n"):
			var t := line.strip_edges()
			if t.begins_with("ATLASCLOUD_API_KEY="):
				return t.split("=", true, 1)[1].strip_edges()
	return ""
