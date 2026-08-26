extends RefCounted
## LANE SUITE — the feature inventory (DAG2 W1 stub pins). Spec: docs/design/DAG2.md.
##
## The stub contract, pinned: the inventory field exists at a safe default, the
## hooks are wired, and NO keep-cost bills and no solidity decays until W2
## L-MAKE lands.
##
## The porting law: a check lands HERE first, then in the same order in
## unity/Runway.Core.Tests/Lanes/FeaturesTests.cs. Same checks, same order.

static func _state() -> GameState:
	var s := GameState.new()
	s.sim_seed = 4242
	s.week = 12
	s.cash = 60_000
	s.traction = 30
	s.product = 50
	s.morale = 70
	s.hype = 30
	s.biz_what = "Software"
	s.biz_who = "Consumer"
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	return s

static func _populate(s: GameState) -> void:
	s.features = [
		{"id": "ft_booking", "name": "online booking", "job": "pull",
		 "family": "", "solidity": "solid", "keep_wk": 40,
		 "unit_cost_add": 0.0, "product_id": "", "born_wk": 1, "measured": 0.0},
		{"id": "ft_pipes", "name": "the data plumbing", "job": "plumbing",
		 "family": "", "solidity": "creaky", "keep_wk": 25,
		 "unit_cost_add": 0.5, "product_id": "", "born_wk": 4, "measured": 0.0}]

static func run(ok: Callable) -> void:
	# ── 1 · the field exists, at a safe default
	var s0 := _state()
	ok.call(s0.features.is_empty(),
		"features: a fresh state ships no feature inventory")

	# ── 2 · the stub speaks when spoken to, and says nothing
	ok.call(SimFeatures.attention(s0).is_empty() and SimFeatures.directives(s0).is_empty(),
		"features: the stub raises no attention and speaks no directives")

	# ── 3 · NEUTRALITY: an inventory on the wall changes no number in the tick
	var ctrl := _state()
	var full := _state()
	_populate(full)
	SimEngine.weekly_tick(ctrl)
	SimEngine.weekly_tick(full)
	ok.call(ctrl.cash == full.cash and ctrl.traction == full.traction
		and ctrl.product == full.product,
		"features: a populated inventory does not move the money (stub is neutral)")

	# ── 4 · the records ride the tick untouched (keep-cost, solidity intact)
	ok.call(full.features.size() == 2
		and int((full.features[0] as Dictionary).get("keep_wk", 0)) == 40
		and String((full.features[1] as Dictionary).get("solidity", "")) == "creaky",
		"features: the inventory survives the tick untouched")
