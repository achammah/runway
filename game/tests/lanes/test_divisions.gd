extends RefCounted
## LANE SUITE — divisions & sites (DAG2 W1 stub pins). Spec: docs/design/DAG2.md.
##
## The stub contract, pinned: the fields exist at safe defaults, the hooks are
## wired, and NOTHING moves until W2 L-DIVWORKS lands — a populated site list
## must not change a single dollar of the tick.
##
## The porting law: a check lands HERE first, then in the same order in
## unity/Runway.Core.Tests/Lanes/DivisionsTests.cs. Same checks, same order.

static func _state() -> GameState:
	var s := GameState.new()
	s.sim_seed = 4242
	s.week = 12
	s.cash = 60_000
	s.traction = 30
	s.product = 50
	s.morale = 70
	s.hype = 30
	s.biz_what = "Service"
	s.biz_who = "SMB"
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	return s

static func _populate(s: GameState) -> void:
	s.sites = [{"id": "site_lyon", "name": "Lyon", "rent_wk": 2_600,
		"wage_mult": 0.92, "learning_count": 140, "demand_weight": 0.35,
		"opened_wk": 9}]
	s.price_book = {"open_site_pack": 18_000, "relocation_fee": 400,
		"machine_shipping": 900, "lease_break_weeks": 8,
		"contract_notice_wks": 4, "refinance_break_fee": 350,
		"freelance_rate": 65, "subcontract_rate": 30,
		"account_fire_penalty": 1_200}
	s.topics = {"growth_plots": ["the garden"], "works_term": "the studio"}
	s.spend_book = [{"name": "staff meals", "buys": "the kitchen fed",
		"amt": 220, "bucket": "office", "contract_notice": 0, "division": ""}]

static func run(ok: Callable) -> void:
	# ── 1 · the fields exist, at safe defaults
	var s0 := _state()
	ok.call(s0.sites.is_empty() and s0.price_book.is_empty()
		and s0.topics.is_empty() and s0.spend_book.is_empty(),
		"divisions: a fresh state carries no sites and an empty price book")

	# ── 2 · the stub speaks when spoken to, and says nothing
	ok.call(SimDivisions.attention(s0).is_empty() and SimDivisions.directives(s0).is_empty(),
		"divisions: the stub raises no attention and speaks no directives")

	# ── 3 · the pre-registered site_rent lane stays zero through a tick
	var s1 := _state()
	SimEngine.weekly_tick(s1)
	ok.call(int((s1.get_meta("pnl", {}) as Dictionary).get("site_rent", -1)) == 0,
		"divisions: the neutral tick books no site rent")

	# ── 4 · NEUTRALITY: populated fields change no number in the tick
	var ctrl := _state()
	var full := _state()
	_populate(full)
	SimEngine.weekly_tick(ctrl)
	SimEngine.weekly_tick(full)
	ok.call(ctrl.cash == full.cash and ctrl.traction == full.traction
		and ctrl.morale == full.morale,
		"divisions: a populated site list does not move the money (stub is neutral)")

	# ── 5 · the records ride the tick untouched
	ok.call(full.sites.size() == 1
		and int((full.sites[0] as Dictionary).get("rent_wk", 0)) == 2_600
		and int(full.price_book.get("open_site_pack", 0)) == 18_000,
		"divisions: sites and the price book survive the tick untouched")
