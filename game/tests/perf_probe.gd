extends SceneTree
## THE ENERGY PROBE (owner: "it takes a lot of energy and RAM and is heavy").
## Stands each heavy screen up alone in a real rendering tree, lets it settle,
## then watches it for three seconds and reports what it costs to sit there
## doing nothing but breathing.
##
## WHY FPS IS NOT THE COST HERE. An empty tree on this machine also runs at
## ~118 fps / 8.5ms a frame: macOS paces presentation to the panel whatever
## vsync is set to, so every screen reads the same and a real saving reads as
## no change. The machine is not short of frames. It is doing FULL-SCREEN work
## a hundred-plus times a second for 12fps hand-drawn content, and that is the
## heat. So the column that matters is redraw/s.
##
## The columns:
##   redraw/s — CanvasItem.draw firings per second across the screen's whole
##              subtree. A 12fps baked loop needs 12. Anything above that is
##              a repaint of an identical 1536x1024 frame.
##   fps      — presented frames per second (panel-paced; headroom, not cost)
##   pk prc   — Performance.TIME_PROCESS. Godot refreshes this once a SECOND
##              and keeps the second's WORST step, so read it as a spike gauge.
##   pk phy   — TIME_PHYSICS_PROCESS, same once-a-second peak
##   draws    — RENDER_TOTAL_DRAW_CALLS_IN_FRAME
##   vram     — RENDER_TEXTURE_MEM_USED while THIS screen is up
##   stat     — MEMORY_STATIC
##   nodes    — OBJECT_NODE_COUNT
##
## A floor line is taken with an empty tree between screens, so a screen that
## never gives its textures back shows up as a floor that keeps climbing.
##
## Run WINDOWED — headless renders nothing and every render number reads zero:
##     godot --path . --script tests/perf_probe.gd
##
## SOAK MODE, for the one number a table cannot give: CPU seconds burned.
## One screen, one process, a fixed wall clock, and the shell does the timing —
##     RUNWAY_PERF_SCREEN=birth /usr/bin/time -l godot --path . --script tests/perf_probe.gd
## Screens: title draft birth howto curtain dice garage empty. RUNWAY_PERF_SECS
## sets the hold (default 10). Wall time is fixed, so user+sys IS the energy.
##
## Optional: RUNWAY_PERF_OUT=<file> also writes the table to disk.

const WARM := 1.2      ## settle: tweens land, hydrators finish, fades complete
const WINDOW := 3.0    ## the watch itself

## the fonts a dozen screens share in the real game; alone in this tree they
## would be freed between frames and every text cost would measure as a reload
var _fonts: Array = []
var _rows: Array[String] = []
var _content: ContentDb
var _llm: LlmClient
var _gen: EventGenerator
var _redraws := 0
var _watched: Array = []

func _init() -> void:
	call_deferred("_go")

# ── the watch ────────────────────────────────────────────────────────────────

## every CanvasItem under the screen reports its own repaints into one counter
func _watch(n: Node) -> void:
	if n is CanvasItem:
		(n as CanvasItem).draw.connect(_tick_redraw)
		if OS.get_environment("RUNWAY_PERF_BLAME") != "":
			(n as CanvasItem).draw.connect(_blame.bind(n))
		_watched.append(n)
	for c in n.get_children():
		_watch(c)

func _tick_redraw() -> void:
	_redraws += 1

## RUNWAY_PERF_BLAME=1: which node is repainting, by name and class, so a storm
## has an address instead of a total
var _blamed: Dictionary = {}

func _blame(n: Node) -> void:
	var kind := n.get_class()
	if n.get_script() != null:
		kind = String((n.get_script() as Script).resource_path).get_file()
	var where := String(n.get_path()).replace("/root/@Node2D@1/", "")
	var box := ""
	if n is Control:
		box = " %s%s" % [(n as Control).position, (n as Control).size]
	_blamed["%s [%s]%s" % [where, kind, box]] = int(
		_blamed.get("%s [%s]%s" % [where, kind, box], 0)) + 1

func _report_blame(secs: float) -> void:
	if _blamed.is_empty():
		return
	var keys := _blamed.keys()
	keys.sort_custom(func(a, b) -> bool: return int(_blamed[a]) > int(_blamed[b]))
	print("  who is repainting:")
	for i in mini(10, keys.size()):
		print("    %8.1f/s  %s" % [float(_blamed[keys[i]]) / secs, keys[i]])

func _unwatch() -> void:
	for n in _watched:
		if is_instance_valid(n) and (n as CanvasItem).draw.is_connected(_tick_redraw):
			(n as CanvasItem).draw.disconnect(_tick_redraw)
	_watched.clear()

func _sample(label: String) -> void:
	# an occluded window stops drawing and Godot idles at 6.9ms a frame, freezing
	# every render number at its last value — so ask for the front before looking
	DisplayServer.window_move_to_foreground()
	_redraws = 0
	var t0 := Time.get_ticks_usec()
	var t_end := t0 + int(WINDOW * 1000000.0)
	var frames := 0
	var proc := 0.0
	var phys := 0.0
	var draws := 0.0
	var vram := 0.0
	var stat := 0.0
	var nodes := 0.0
	var blind := false
	while Time.get_ticks_usec() < t_end:
		await process_frame
		if not DisplayServer.window_can_draw():
			blind = true
		frames += 1
		proc = maxf(proc, Performance.get_monitor(Performance.TIME_PROCESS))
		phys = maxf(phys, Performance.get_monitor(Performance.TIME_PHYSICS_PROCESS))
		draws += Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME)
		vram += Performance.get_monitor(Performance.RENDER_TEXTURE_MEM_USED)
		stat += Performance.get_monitor(Performance.MEMORY_STATIC)
		nodes += Performance.get_monitor(Performance.OBJECT_NODE_COUNT)
	var secs := float(Time.get_ticks_usec() - t0) / 1000000.0
	var n := float(maxi(frames, 1))
	var row := "%-26s %8.1f %7.1f %7.1f %7.2f %7.1f %9.1f %8.1f %7.0f%s" % [
		label, float(_redraws) / secs, float(frames) / secs,
		proc * 1000.0, phys * 1000.0,
		draws / n, vram / n / 1048576.0, stat / n / 1048576.0, nodes / n,
		"  << BLIND, window could not draw" if blind else ""]
	_rows.append(row)
	print(row)

func _head() -> void:
	var h := "%-26s %8s %7s %7s %7s %7s %9s %8s %7s" % [
		"screen", "redraw/s", "fps", "pk prc", "pk phy", "draws", "vram MB",
		"stat MB", "nodes"]
	_rows.append(h)
	_rows.append("-".repeat(h.length()))
	print(h)
	print(_rows[1])

## stand a screen up alone, watch it, tear it down, report the floor it left
func _stage(label: String, node: Node, warm: float = WARM) -> void:
	root.add_child(node)
	if node is Control:
		(node as Control).size = Vector2(1536, 1024)
	await create_timer(warm).timeout
	_watch(node)
	await _sample(label)
	_unwatch()
	node.queue_free()
	await create_timer(0.6).timeout   # queue_free lands, textures unreference

func _floor(label: String) -> void:
	await create_timer(0.4).timeout
	await _sample(label)

func _prep() -> void:
	await process_frame
	# nothing may hide the window: occluded, it stops drawing and every render
	# number below would be a frozen copy of the last live one
	DisplayServer.window_set_flag(DisplayServer.WINDOW_FLAG_ALWAYS_ON_TOP, true)
	DisplayServer.window_move_to_foreground()
	for f in ["PatrickHand-Regular", "Baloo2-Bold"]:
		_fonts.append(load("res://assets/fonts/%s.ttf" % f))
	_content = ContentDb.new()
	_content.load_all()
	_llm = LlmClient.new()
	root.add_child(_llm)
	_gen = EventGenerator.new(_llm)
	_gen.disabled = true   # no network in a heat measurement
	root.add_child(_gen)

# ── the screens ──────────────────────────────────────────────────────────────

func _go() -> void:
	await _prep()
	if OS.get_environment("RUNWAY_PERF_SCREEN") != "":
		await _soak(OS.get_environment("RUNWAY_PERF_SCREEN"))
		return

	print("\nRUNWAY! energy probe — %.1fs per screen, godot %s\n" % [
		WINDOW, Engine.get_version_info()["string"]])
	_head()

	await _floor("00 empty tree")

	await _stage("01 title", TitleScreen.new(), 2.5)   # 48 frames stream in
	await _floor("02 floor after title")

	var draft := FounderDraftScreen.new()
	draft.content_items = _content.items.values()
	root.add_child(draft)
	draft.size = Vector2(1536, 1024)
	await create_timer(1.8).timeout   # the loops hydrate 6 frames per 0.05s tick
	draft._show_page(1)
	await create_timer(0.8).timeout
	_watch(draft)
	await _sample("03 draft page 1")
	_unwatch()
	draft.queue_free()
	await create_timer(0.6).timeout
	await _floor("04 floor after draft")

	await _stage("05 birth (intro)", BirthScreen.new(), 0.5)
	var bs2 := BirthScreen.new()
	root.add_child(bs2)
	bs2.size = Vector2(1536, 1024)
	await create_timer(3.0).timeout   # past the 24-frame arrival, inside the loop
	_watch(bs2)
	await _sample("06 birth (loop)")
	_unwatch()
	bs2.queue_free()
	await create_timer(0.6).timeout

	await _stage("07 howto", HowToScreen.new(), 1.0)

	var cur := Curtain.new()
	root.add_child(cur)
	cur.close()   # not awaited: we want the held-shut sway, not the sweep
	await create_timer(2.5).timeout
	_watch(cur)
	await _sample("08 curtain shut")
	_unwatch()
	# MAIN holds ONE curtain for the whole run, so what it costs while OPEN is
	# what it costs for the other 99% of the game. This line is that number.
	cur.open()
	await create_timer(1.5).timeout
	await _sample("08b curtain open (held)")
	cur.queue_free()
	await create_timer(0.6).timeout

	var dr := DiceRoll.new()
	root.add_child(dr)
	dr.size = Vector2(1536, 1024)
	dr.roll(17)   # not awaited: sample it mid-tumble
	await create_timer(0.4).timeout
	_watch(dr)
	await _sample("09 dice mid-roll")
	_unwatch()
	dr.queue_free()
	await create_timer(0.6).timeout
	await _floor("10 floor after dice")

	var g := _build_garage()
	root.add_child(g)
	g.size = Vector2(1536, 1024)
	await create_timer(2.0).timeout
	_watch(g)
	await _sample("11 garage")
	_unwatch()
	g.queue_free()
	await create_timer(0.6).timeout
	await _floor("12 floor after garage")

	var body := "\n".join(_rows)
	var out := OS.get_environment("RUNWAY_PERF_OUT")
	if out != "":
		var f := FileAccess.open(out, FileAccess.WRITE)
		if f != null:
			f.store_string(body + "\n")
			f.close()
	print("\nPERF PROBE DONE")
	quit(0)

# ── soak mode: one screen, one process, the shell holds the stopwatch ────────

func _soak(which: String) -> void:
	var secs := 10.0
	if OS.get_environment("RUNWAY_PERF_SECS") != "":
		secs = float(OS.get_environment("RUNWAY_PERF_SECS"))
	var node: Node = null
	match which:
		"title": node = TitleScreen.new()
		"birth": node = BirthScreen.new()
		"howto": node = HowToScreen.new()
		"curtain": node = Curtain.new()
		"dice": node = DiceRoll.new()
		"garage": node = _build_garage()
		"draft":
			var d := FounderDraftScreen.new()
			d.content_items = _content.items.values()
			node = d
		"empty": pass
		_:
			print("SOAK: no screen named '%s'" % which)
			quit(1)
			return
	if node != null:
		root.add_child(node)
		if node is Control:
			(node as Control).size = Vector2(1536, 1024)
	await create_timer(2.0).timeout   # settle before the clock that counts
	if which == "draft":
		(node as FounderDraftScreen)._show_page(1)
		await create_timer(0.8).timeout
	elif which == "curtain":
		(node as Curtain).close()
		await create_timer(1.5).timeout
	elif which == "dice":
		(node as DiceRoll).roll(17)
	if node != null:
		_watch(node)
	DisplayServer.window_move_to_foreground()
	_redraws = 0
	var t0 := Time.get_ticks_usec()
	var frames := 0
	var blind := 0
	while Time.get_ticks_usec() - t0 < int(secs * 1000000.0):
		await process_frame
		frames += 1
		if not DisplayServer.window_can_draw():
			blind += 1
	var el := float(Time.get_ticks_usec() - t0) / 1000000.0
	print("SOAK %s: %.1fs  redraw/s=%.1f  fps=%.1f  vram=%.1fMB  audio=%d  blind=%d/%d" % [
		which, el, float(_redraws) / el, float(frames) / el,
		Performance.get_monitor(Performance.RENDER_TEXTURE_MEM_USED) / 1048576.0,
		_count_players(root), blind, frames])
	_report_blame(el)
	quit(0)

## suspect 5: how many AudioStreamPlayers is a screen actually standing up
func _count_players(n: Node) -> int:
	var c := 1 if n is AudioStreamPlayer else 0
	for ch in n.get_children():
		c += _count_players(ch)
	return c

func _build_garage() -> GarageViewScreen:
	var state := GameState.new()
	state.week = 9
	state.company_name = "Blobsworth Industrial"
	state.company_idea = "Peer-to-peer subscription box for artisanal compliance software"
	state.archetype_id = "consultant"
	state.archetype_name = "THE EX-CONSULTANT"
	state.cash = 4200
	state.product = 38
	state.traction = 7
	state.morale = 31
	state.hype = 22
	state.founder_pct = 41.0
	state.cofounders = [
		{"role": "Technical", "commitment": "Full-time", "equity": 22.0, "vesting": true, "loyalty": 18},
		{"role": "Business", "commitment": "Full-time", "equity": 15.0, "vesting": true, "loyalty": 55},
	]
	state.employees = [
		{"name": "Priya", "role": "engineer", "salary": 1400, "burnout": 78, "quirk": "rust evangelist"},
	]
	state.items.assign(["itm_laptop", "itm_houseplant"])
	state.theta = SimEngine.default_theta("Service", "Consumer")
	SimEngine.seed_beliefs(state)
	var rec := RunRecord.new()
	rec.seed_value = 7
	var g := GarageViewScreen.new()
	g.setup(state, _content, SeededRng.new(7), rec, _gen)
	return g
