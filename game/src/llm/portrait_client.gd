class_name PortraitClient
extends RefCounted
## THE BINDER PORTRAIT (DECISIONS § THE BINDER PORTRAIT) — one transparent
## PNG of the company's own well-used ring binder, generated ONCE at run start
## and regenerated only on a rename or a nature-changing pivot.
##
## PURE TRANSPORT: prompt in, PNG on disk out. The label in the image is BLANK
## on purpose (image models garble text); the binder screen overlays the
## company name in the founder's hand (binder.gd LABEL_RECT). The drawn kraft
## cover is the instant placeholder and the permanent fallback — the binder
## never waits on this call, and a failure costs a picture, never a turn.
##
## THE MODEL LADDER (the "gpt image 2" ruling): ask for "gpt-image-2" first;
## if the API reports no such model, fall back to "gpt-image-1". Any OTHER
## failure does not burn the second model — it is the same account and the
## same request, so the retry would fail the same way.

const OUT_PATH := "user://binder_portrait.png"
## THE COMPANY LOGO (DECISIONS § THE COMPANY LOGO): the second asset on the
## same ladder — a small flat emblem fitted to the business, blank of text
## (names are always engine-overlaid), bold enough to read at 48px. The UI's
## drawn monogram is the instant placeholder and the permanent fallback.
const LOGO_PATH := "user://company_logo.png"
const IMAGES_URL := "https://api.openai.com/v1/images/generations"
const MODELS := ["gpt-image-2", "gpt-image-1"]

static func portrait_path() -> String:
	return OUT_PATH

static func logo_path() -> String:
	return LOGO_PATH

## The look (owner-amended): a NICE 3D-ILLUSTRATED object — soft-shaded,
## gently dimensional, a chunky real binder prop — still inside the game
## palette and its hand-drawn world. The label stays BLANK; the four index
## tabs are the divider-group colors; the background is transparent.
const PROMPT := ("A single chunky, well-used ring binder as a game prop, "
	+ "seen straight on, slightly three-quarter: soft-shaded 3D illustration, "
	+ "gentle volume and a soft drop shadow baked only under the object "
	+ "itself, clean silhouette, flat-color palette. Kraft-brown cardboard "
	+ "cover, visibly used at the corners. FOUR thick index tabs sticking out "
	+ "of the page edge, top to bottom: green sage #8FA582, coral red "
	+ "#E86A5C, muted blue #6E8CA0, warm yellow #F4B942. Untidy papers "
	+ "poking out unevenly between the covers. A BLANK taped paper label on "
	+ "the front cover — completely blank, nothing written on it. Palette "
	+ "only: ink #1E1E1E, coral #E86A5C, yellow #F4B942, sage #8FA582, blue "
	+ "#6E8CA0, cream #F2EAD3. No gradients except the soft shading, no "
	+ "text anywhere, no letters, no numbers, no logos. Transparent "
	+ "background: nothing behind or around the binder at all.")

var _tree: SceneTree
var _inflight := {}   # out_path -> true while that asset is painting

func _init(tree: SceneTree) -> void:
	_tree = tree

## Fire the portrait. cb receives the saved path ("" on failure). Cached: an
## existing PNG answers immediately; pass force=true on rename/pivot to
## regenerate. Coalesced: a second call while one is painting is dropped
## (the binder polls the file, so nobody is left waiting).
func generate(cb: Callable = Callable(), force := false) -> void:
	_generate_to(PROMPT, OUT_PATH, cb, force)

## Fire the logo mark. `company` carries {idea, what, who} (the name is never
## put in the prompt — letters are exactly what the image must not contain).
## Same cache, force and coalescing semantics as the portrait.
func generate_logo(company: Dictionary, cb: Callable = Callable(), force := false) -> void:
	_generate_to(_logo_prompt(company), LOGO_PATH, cb, force)

func _logo_prompt(company: Dictionary) -> String:
	var idea := String(company.get("idea", "")).strip_edges()
	var fit := "a small honest business"
	if idea != "":
		fit = "%s (%s for %s)" % [idea, String(company.get("what", "a business")),
			String(company.get("who", "its customers"))]
	return ("A small flat logo mark for a game: one simple bold emblem "
		+ "representing " + fit + ". "
		+ "ONE central pictorial symbol, thick simple shapes, a bold clean "
		+ "silhouette that stays readable at 48 pixels, flat fills, no "
		+ "gradients, no outlines thinner than a pencil. Palette only: ink "
		+ "#1E1E1E, coral #E86A5C, yellow #F4B942, sage #8FA582, blue "
		+ "#6E8CA0, cream #F2EAD3. NO TEXT anywhere: no letters, no numbers, "
		+ "no letterforms, no wordmark — a pure pictorial mark. Transparent "
		+ "background: nothing behind or around the emblem at all.")

## The shared ladder: one prompt, one target file, the two-model fall-through.
func _generate_to(prompt: String, out_path: String, cb: Callable, force: bool) -> void:
	if not force and FileAccess.file_exists(out_path):
		if cb.is_valid():
			cb.call(out_path)
		return
	if _inflight.has(out_path):
		return
	_inflight[out_path] = true
	var key := _openai_key()
	if key == "":
		_inflight.erase(out_path)
		if cb.is_valid():
			cb.call("")
		return
	for model in MODELS:
		var verdict := await _attempt(String(model), key, prompt, out_path)
		if verdict == "ok":
			_inflight.erase(out_path)
			if cb.is_valid():
				cb.call(out_path)
			return
		if verdict != "model_missing":
			break
	_inflight.erase(out_path)
	if cb.is_valid():
		cb.call("")

## One request against one model. Returns "ok" | "model_missing" | "failed".
func _attempt(model: String, key: String, prompt: String, out_path: String) -> String:
	var http := HTTPRequest.new()
	http.timeout = 240.0
	_tree.root.add_child(http)
	var body := {
		"model": model,
		"prompt": prompt,
		"background": "transparent",
		"output_format": "png",
		"size": "1024x1024",
		"quality": "medium",
		"n": 1,
	}
	var err := http.request(IMAGES_URL,
			PackedStringArray(["Content-Type: application/json",
				"Authorization: Bearer " + key]),
			HTTPClient.METHOD_POST, JSON.stringify(body))
	if err != OK:
		http.queue_free()
		return "failed"
	# the render ladder's lesson: HTTPRequest's own clock has slept through
	# wedged sockets — race the await against a hard wall so a silent hang
	# becomes a failed attempt instead of a portrait that never resolves
	var res_box: Array = []
	http.request_completed.connect(func(a: int, b: int, c: PackedStringArray, d: PackedByteArray) -> void:
		res_box.append([a, b, c, d]))
	var waited := 0.0
	while res_box.is_empty() and waited < 260.0:
		await _tree.create_timer(0.5).timeout
		waited += 0.5
	if res_box.is_empty():
		print("PortraitClient: request HUNG %ds — cancelled" % int(waited))
		http.cancel_request()
		http.queue_free()
		return "failed"
	var res: Array = res_box[0]
	http.queue_free()
	var body_txt := (res[3] as PackedByteArray).get_string_from_utf8()
	var code := int(res[1])
	if code < 200 or code >= 300:
		print("PortraitClient: %s -> HTTP %d — %s" % [model, code, body_txt.left(200)])
		if _looks_like_missing_model(code, body_txt):
			return "model_missing"
		return "failed"
	var parsed = JSON.parse_string(body_txt)
	if not (parsed is Dictionary):
		return "failed"
	var data: Array = (parsed as Dictionary).get("data", [])
	if data.is_empty():
		return "failed"
	var b64 := String((data[0] as Dictionary).get("b64_json", ""))
	if b64 != "":
		return "ok" if _save_png(Marshalls.base64_to_raw(b64), out_path) else "failed"
	var url := String((data[0] as Dictionary).get("url", ""))
	if url != "":
		return "ok" if await _download(url, out_path) else "failed"
	return "failed"

## "no such model" wears several coats; every one mentions the model. Any
## other 4xx (quota, org verification) must NOT read as missing.
func _looks_like_missing_model(code: int, body_txt: String) -> bool:
	if code != 400 and code != 404:
		return false
	var low := body_txt.to_lower()
	if low.contains("model_not_found"):
		return true
	return low.contains("model") and (low.contains("not found")
		or low.contains("does not exist") or low.contains("unknown")
		or low.contains("invalid model"))

func _download(url: String, out_path: String) -> bool:
	var http := HTTPRequest.new()
	http.timeout = 120.0
	_tree.root.add_child(http)
	if http.request(url) != OK:
		http.queue_free()
		return false
	var res: Array = await http.request_completed
	http.queue_free()
	if int(res[1]) < 200 or int(res[1]) >= 300:
		return false
	return _save_png(res[3] as PackedByteArray, out_path)

## A partial body written as a file is how truncated art shipped before: a
## PNG that does not open with the PNG magic and end in IEND is not a PNG.
func _save_png(bytes: PackedByteArray, out_path: String) -> bool:
	if bytes.size() < 4096:
		return false
	if bytes[0] != 0x89 or bytes[1] != 0x50 or bytes[2] != 0x4E or bytes[3] != 0x47:
		return false
	if bytes.slice(bytes.size() - 8, bytes.size() - 4).get_string_from_ascii() != "IEND":
		return false
	var f := FileAccess.open(out_path, FileAccess.WRITE)
	if f == null:
		return false
	f.store_buffer(bytes)
	f.close()
	return true

## THE ONE KEY, WHEREVER IT LIVES — the same stack the narrator and the
## renderer read (user://keys.env over res://.env, process env first), never
## a private path of its own.
func _openai_key() -> String:
	if OS.has_environment("OPENAI_API_KEY"):
		return OS.get_environment("OPENAI_API_KEY").strip_edges()
	return String(DotEnv.load_env().get("OPENAI_API_KEY", "")).strip_edges()
