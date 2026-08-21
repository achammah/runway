extends SceneTree
## Photograph WorldRevealScreen with worst-case LLM verbosity: long market
## line, three two-line theses, rivals with long what-lines. Catches fixed-slot
## overflow. Run: godot --path . --script tests/world_reveal_shot.gd

func _init() -> void:
	call_deferred("_go")

func _go() -> void:
	await process_frame
	var s := GameState.new()
	s.company_name = "Pivotflow"
	s.theta = SimEngine.default_theta("Software", "SMB")
	s.set_meta("market_line", "a market that says it wants innovation and buys whatever its auditor already approved")
	s.investors = [
		{"name": "Harborline Syndicate", "archetype": "the operator VC",
		 "thesis": "funeral homes buy software once a decade, so whoever is standing in the doorway that year takes the whole shelf"},
		{"name": "Vantagrove Capital", "archetype": "the momentum fund",
		 "thesis": "grief is recession-proof and nobody wants to demo against a casket, which keeps churn low and competitors squeamish"},
		{"name": "Cobalt Anders", "archetype": "the contrarian angel",
		 "thesis": "the last three winners in this space looked unfundable for four straight years before the market admitted they were right"},
	]
	s.rivals = [
		{"name": "Solacely", "strength": 45.0, "what": "legacy funeral-home management suite sold through regional trade associations"},
		{"name": "Eterna", "strength": 25.0, "what": "a two-person startup doing memorial pages with aggressive SEO"},
	]
	var wr := WorldRevealScreen.new()
	wr.setup(s)
	root.add_child(wr)
	wr.size = Vector2(1536, 1024)
	await create_timer(1.0).timeout
	await RenderingServer.frame_post_draw
	var dir := OS.get_environment("RUNWAY_STRESS_DIR")
	if dir == "":
		dir = "/tmp"
	root.get_viewport().get_texture().get_image().save_png(dir + "/world_reveal.png")
	print("WORLD REVEAL SHOT -> %s/world_reveal.png" % dir)
	quit(0)
