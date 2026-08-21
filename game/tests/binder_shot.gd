extends SceneTree
## Photograph every Binder tab with a lively mid-game state (customers, crew,
## debt, rivals, statuses, a loan — all seven tabs populated). Catches layout
## drift the moment a tab changes. Run: godot --path . --script tests/binder_shot.gd

func _init() -> void:
	call_deferred("_go")

func _go() -> void:
	await process_frame
	var s := GameState.new()
	s.sim_seed = 99
	s.week = 14
	s.cash = 31_500
	s.traction = 210
	s.product = 58
	s.morale = 44
	s.hype = 61
	s.founder_name = "Lena Voss"
	s.company_name = "Pivotflow"
	s.biz_what = "Software"
	s.biz_who = "SMB"
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	s.analytics_level = 1
	s.tech_debt = 55.0
	s.exhaustion = 3
	s.loan_principal = 12_000
	s.marketing_budget = 400
	s.price_mult = 1.1
	s.founder_pct = 61.0
	s.cofounders = [{"role": 0, "commitment": 0, "equity": 25.0, "vesting": true, "name": "Nico Ferreira"}]
	s.employees = [{"name": "Priya Voss", "role": "engineer", "salary": 1500, "burnout": 30.0}]
	s.investors = [{"name": "Harborline Syndicate", "archetype": "the operator VC",
		"thesis": "founders who ship beat founders who pitch", "coords": [-0.3, 0.2]}]
	s.rivals = [{"name": "Solacely", "strength": 45.0, "what": "legacy suite via trade associations",
		"tactics": ["undercut pricing"], "weeks_since_move": 1},
		{"name": "Eterna", "strength": 25.0, "what": "memorial pages with aggressive SEO",
		"tactics": ["bought ads on your name"], "weeks_since_move": 3}]
	SimEngine.add_status(s, "investor_pressure", 2)
	SimEngine.add_status(s, "word_of_mouth", 3)
	SimEngine.add_clock(s, 3, "the bridge loan comes due")
	for w in range(1, 14):
		s.metric_history.append({"week": w, "cash": 60_000 - w * 2_200,
			"customers": int(pow(w, 1.7)), "morale": 70 - w * 2, "product": 20 + w * 3})
	var dir := OS.get_environment("RUNWAY_STRESS_DIR")
	if dir == "":
		dir = "/tmp"
	var b := Binder.new()
	b.setup(s)
	root.add_child(b)
	b.size = Vector2(1536, 1024)
	await create_timer(0.6).timeout
	for i in 7:
		b.set("_tab", i)
		b.call("_refresh")
		await create_timer(0.35).timeout
		await RenderingServer.frame_post_draw
		root.get_viewport().get_texture().get_image().save_png("%s/binder_%d.png" % [dir, i])
	print("BINDER SHOTS -> %s/binder_0..6.png" % dir)
	quit(0)
