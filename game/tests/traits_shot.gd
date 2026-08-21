extends SceneTree
## Photographs the D&D character layer, directly instantiated: the founder card
## with the six hidden traits and one of their rules opened, then the bag page
## with two things packed and the running loadout written underneath.
## Run: RUNWAY_STRESS_DIR=<dir> godot --path . --script tests/traits_shot.gd

func _shot(name: String) -> void:
	await RenderingServer.frame_post_draw
	var dir := OS.get_environment("RUNWAY_STRESS_DIR")
	root.get_viewport().get_texture().get_image().save_png("%s/%s.png" % [dir, name])

func _init() -> void:
	call_deferred("_go")

func _go() -> void:
	await process_frame
	var vp := Vector2(1536, 1024)
	var content := ContentDb.new()
	content.load_all()
	var d := FounderDraftScreen.new()
	d.content_items = content.items.values()
	root.add_child(d)
	d.size = vp
	await create_timer(1.0).timeout

	# THE EX-FAANG PM: credibility 5 and a phone book full of numbers, which is
	# the owner's whole case for hidden traits printed on one card.
	var ex_i := 0
	for i in (d._archs as Array).size():
		if String((d._archs[i] as Dictionary).get("id", "")) == "exfaang":
			ex_i = i
	d._show_page(1)
	d._select(ex_i, false)
	await create_timer(0.8).timeout
	d._show_trait_tip("credibility")
	await create_timer(0.5).timeout
	await _shot("traits_card")

	# THE BAG, two things packed: one that buys luck at the price of standing,
	# one that buys stamina at the price of concentration.
	d._show_page(6)
	await create_timer(0.7).timeout
	for id in ["itm_crystal_ball", "itm_energy_drinks"]:
		var btn = d._bag_btns.get(id)
		if btn == null:
			print("MISSING SHELF TILE: ", id)
			continue
		d._toggle_bag(id, int((content.items.get(id, {}) as Dictionary).get("carry_cost", 1)), btn)
	d._bag_detail(content.items.get("itm_crystal_ball", {}))
	await create_timer(0.9).timeout
	await _shot("traits_bag")
	print("TRAITS SHOT DONE")
	quit(0)
