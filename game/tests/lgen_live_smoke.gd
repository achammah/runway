extends SceneTree
## MANUAL LIVE SMOKE — L-GEN birth generation + binder portrait. NETWORK.
##
## NOT part of the automated suite (the suite never calls the network): run
## by hand with the keys in the environment —
##   OPENAI_API_KEY=... godot --headless --path game --script tests/lgen_live_smoke.gd
##
## What it proves, in order:
##   0. OFFLINE: the deterministic fallback (keyless build fills the book) and
##      the applier's clamps (an out-of-band price book is pulled into band).
##   1. LIVE: one birth-generation call — strict JSON parses, all six birth
##      blocks present, apply_birth writes clamped state fields.
##   2. LIVE: one binder-portrait generation — a PNG lands at
##      user://binder_portrait.png (the wrapper checks its alpha channel).
## Exit 0 = everything attempted held; 1 = an offline invariant broke;
## live failures print [LIVE FAIL ...] and the fallback proof stands instead.

var _fails := 0
## LGEN_SMOKE_SECTIONS picks the sections to run ("0,1,2,3" default):
## 0 offline invariants · 1 birth call · 2 portrait · 3 logo. Lets the QA
## wave re-prove one asset without re-paying the whole ladder.
var _sections := PackedStringArray()

func _want(n: int) -> bool:
	return _sections.has(str(n))

func _check(cond: bool, label: String) -> void:
	if cond:
		print("  ok — " + label)
	else:
		_fails += 1
		print("  FAIL — " + label)

func _init() -> void:
	_run()

func _run() -> void:
	var wanted := OS.get_environment("LGEN_SMOKE_SECTIONS")
	_sections = ("0,1,2,3" if wanted.strip_edges() == "" else wanted).split(",")
	print("── 0 · offline: deterministic fallback + clamps ──")
	var s := GameState.new()
	s.sim_seed = 777
	s.company_name = "Kneadful"
	s.company_idea = "a walk-in massage studio for desk workers in Brussels"
	s.biz_what = "Service"
	s.biz_who = "Consumer"
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	WorldGen.build(s)
	_check(not s.topics.is_empty() and s.topics.has("growth") and s.topics.has("works"),
		"keyless build fills topics (identity/growth/works)")
	_check(s.spend_book.size() == 4, "keyless build installs the bare four-line book")
	_check(int(s.price_book.get("open_site_pack", 0)) == 18000,
		"keyless build installs the mid-band price book")
	_check(s.features.size() >= 3, "keyless build installs generic features")
	var g_ads: Dictionary = (s.topics["growth"] as Dictionary).get("ads", {})
	_check(String(g_ads.get("one_line", "")).contains("dollar buys a little less"),
		"the ads plot keeps its saturating character")
	# clamps: feed a deliberately rotten gen and watch the bands bite
	var s2 := GameState.new()
	s2.biz_what = "Service"
	s2.biz_who = "Consumer"
	s2.theta = SimEngine.default_theta(s2.biz_what, s2.biz_who)
	WorldGen.build(s2)
	WorldGen.apply_birth(s2, {
		"price_book": {"open_site_pack": 999999, "relocation_fee": 1,
			"lease_break_weeks": 99, "account_fire_penalty": -5},
		"spend_book": [
			{"name": "gold taps", "buys": "gold", "amt": 99999, "bucket": "vanity", "contract_notice": 99},
			{"name": "a", "buys": "b", "amt": 10, "bucket": "care", "contract_notice": 0},
			{"name": "c", "buys": "d", "amt": 10, "bucket": "rnd", "contract_notice": 0},
			{"name": "e", "buys": "f", "amt": 10, "bucket": "office", "contract_notice": 0}],
		"birth_features": [
			{"name": "the everything", "job": "magic", "keep_wk": 9999, "unit_cost_add": 500},
			{"name": "two", "job": "pull", "keep_wk": 10, "unit_cost_add": 0},
			{"name": "three", "job": "keep", "keep_wk": 10, "unit_cost_add": 0}],
	})
	_check(int(s2.price_book["open_site_pack"]) == 40000, "open_site_pack clamps to the band top")
	_check(int(s2.price_book["relocation_fee"]) == 100, "relocation_fee clamps to the band floor")
	_check(int(s2.price_book["lease_break_weeks"]) == 16, "lease_break_weeks clamps to 16")
	_check(int(s2.price_book["account_fire_penalty"]) == 200, "a negative penalty clamps to the floor")
	_check(int(s2.price_book["machine_shipping"]) == 900, "a missing key lands at its default")
	var line0: Dictionary = s2.spend_book[0]
	_check(int(line0["amt"]) <= 400, "a 99999 spend line clamps to garage scale")
	_check(String(line0["bucket"]) == "office", "an unknown bucket degrades to office")
	var f0: Dictionary = s2.features[0]
	_check(int(f0["keep_wk"]) == 150 and String(f0["job"]) == "keep",
		"feature keep_wk clamps to 150 and an unknown job degrades to keep")
	var plumbed := false
	for f in s2.features:
		if String((f as Dictionary).get("job", "")) == "plumbing":
			plumbed = true
	_check(plumbed, "a bookful of no plumbing gains the generic plumbing row")

	var key := OS.get_environment("OPENAI_API_KEY").strip_edges()
	if key == "":
		print("── no OPENAI_API_KEY in the environment: offline half only ──")
		quit(1 if _fails > 0 else 0)
		return
	# in a --script SceneTree, children added during _init are not yet inside
	# the tree — HTTPRequest refuses to fire until the first frame has run
	await process_frame
	if _want(1):
		await _live_worldgen(s, key)
	if _want(2):
		await _live_portrait()
	if _want(3):
		await _live_logo(s)
	print("── live smoke done: %d offline fails ──" % _fails)
	quit(1 if _fails > 0 else 0)

func _live_worldgen(s: GameState, key: String) -> void:
	print("── 1 · live: one birth-generation call ──")
	var llm := LlmClient.new()
	root.add_child(llm)
	llm.setup({"OPENAI_API_KEY": key})
	var gen_box: Array = []
	var evg := EventGenerator.new(llm)
	root.add_child(evg)
	evg.generate_world(s, func(gen: Dictionary) -> void: gen_box.append(gen))
	var waited := 0.0
	while gen_box.is_empty() and waited < 150.0:
		await create_timer(0.5).timeout
		waited += 0.5
	if gen_box.is_empty() or (gen_box[0] as Dictionary).is_empty():
		print("[LIVE FAIL worldgen] no reply in %.0fs — the deterministic fallback above stands" % waited)
	else:
		var gen: Dictionary = gen_box[0]
		var have := PackedStringArray()
		for k in ["identity", "growth_topics", "works_terms", "spend_book", "price_book", "birth_features"]:
			if gen.has(k):
				have.append(k)
		print("  reply in %.0fs; birth blocks present: %s" % [waited, ", ".join(have)])
		_check(have.size() == 6, "all six birth blocks in the strict JSON")
		var before_psp := int(s.price_book.get("open_site_pack", 0))
		WorldGen.apply_llm_world(s, gen)
		print("  identity: " + JSON.stringify((s.topics as Dictionary).get("identity", {})))
		print("  growth.ads: " + JSON.stringify(((s.topics as Dictionary).get("growth", {}) as Dictionary).get("ads", {})))
		print("  works: " + JSON.stringify((s.topics as Dictionary).get("works", {})))
		print("  spend_book (%d lines): " % s.spend_book.size() + JSON.stringify(s.spend_book))
		print("  price_book: " + JSON.stringify(s.price_book))
		print("  features (%d): " % s.features.size() + JSON.stringify(s.features))
		_check(s.spend_book.size() >= 4 and s.spend_book.size() <= 10, "spend book landed at 4-10 lines")
		var in_band := true
		for pk in WorldGen.PRICE_BANDS:
			var band: Array = WorldGen.PRICE_BANDS[pk]
			var v := int(s.price_book.get(pk, -1))
			if v < int(band[0]) or v > int(band[1]):
				in_band = false
		_check(in_band, "every price-book value inside its band after apply")
		_check(s.features.size() >= 3 and s.features.size() <= 7, "3-7 features after apply")
		print("  (open_site_pack default %d -> generated %d)" % [before_psp, int(s.price_book["open_site_pack"])])

func _live_portrait() -> void:
	print("── 2 · live: one binder-portrait generation (60-150s) ──")
	var pc := PortraitClient.new(self)
	var p_box: Array = []
	pc.generate(func(path: String) -> void: p_box.append(path), true)
	var p_waited := 0.0
	while p_box.is_empty() and p_waited < 300.0:
		await create_timer(0.5).timeout
		p_waited += 0.5
	if p_box.is_empty() or String(p_box[0]) == "":
		print("[LIVE FAIL portrait] no PNG in %.0fs — the drawn kraft cover stands" % p_waited)
	else:
		var gpath := ProjectSettings.globalize_path(String(p_box[0]))
		print("  portrait in %.0fs -> %s" % [p_waited, gpath])
		var img := Image.new()
		var lerr := img.load(gpath)
		_check(lerr == OK, "the PNG loads")
		if lerr == OK:
			print("  size %dx%d, format %d (alpha checked by the wrapper via sips)" % [
				img.get_width(), img.get_height(), img.get_format()])

func _live_logo(s: GameState) -> void:
	print("── 3 · live: one company-logo generation ──")
	var pc := PortraitClient.new(self)
	var l_box: Array = []
	pc.generate_logo({"idea": s.company_idea, "what": s.biz_what, "who": s.biz_who},
		func(path: String) -> void: l_box.append(path), true)
	var l_waited := 0.0
	while l_box.is_empty() and l_waited < 300.0:
		await create_timer(0.5).timeout
		l_waited += 0.5
	if l_box.is_empty() or String(l_box[0]) == "":
		print("[LIVE FAIL logo] no PNG in %.0fs — the drawn monogram stands" % l_waited)
		return
	var lpath := ProjectSettings.globalize_path(String(l_box[0]))
	print("  logo in %.0fs -> %s" % [l_waited, lpath])
	var img := Image.new()
	var lerr := img.load(lpath)
	_check(lerr == OK, "the logo PNG loads")
	if lerr != OK:
		return
	var w := img.get_width()
	var h := img.get_height()
	var maxa := 0.0
	for p in [Vector2i(2, 2), Vector2i(w - 3, 2), Vector2i(2, h - 3), Vector2i(w - 3, h - 3)]:
		maxa = maxf(maxa, img.get_pixelv(p).a)
	_check(maxa < 0.06, "logo corners transparent (max corner alpha %.2f)" % maxa)
	print("  size %dx%d" % [w, h])
