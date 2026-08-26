extends RefCounted
## LANE SUITE — the ownership cluster (DAG2 W1 stub pins). Spec: docs/design/DAG2.md.
##
## The stub contract, pinned: pool, paper, raise, recruitment and buyout fields
## exist at safe defaults, the hooks are wired, and NOTHING vests, converts,
## knocks or bills until W2 L-OWN lands.
##
## The porting law: a check lands HERE first, then in the same order in
## unity/Runway.Core.Tests/Lanes/OwnershipTests.cs. Same checks, same order.

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
	s.biz_who = "SMB"
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	return s

static func _populate(s: GameState) -> void:
	s.esop = {"pool_pct": 10.0, "granted": [
		{"emp_id": "june_park", "pct": 0.4, "vest_start_wk": 12}]}
	s.instruments = [{"kind": "safe", "holder": "Fern Capital",
		"amount": 150_000, "cap": 4_000_000, "discount": 0.2, "rate": 0.0,
		"maturity_wk": 0, "pct": 0.0, "prefs": 0.0, "protective": false,
		"drag_threshold": 0.0, "signed_wk": 9}]
	s.raise_state = {"stages": [], "interest_score": 22.5, "active": true,
		"founder_time_tax": 0.15}
	s.recruitment = {"roles": [{"role": "designer"}], "candidates": [],
		"offers_out": []}
	s.buyout_offer = {"buyer": "Larkspur Depot", "cash": 1_200_000}

static func run(ok: Callable) -> void:
	# ── 1 · the fields exist, at safe defaults
	var s0 := _state()
	ok.call(s0.esop.is_empty() and s0.instruments.is_empty()
		and s0.raise_state.is_empty() and s0.recruitment.is_empty()
		and s0.buyout_offer.is_empty(),
		"ownership: a fresh state has no pool, no paper, no raise, no buyout")

	# ── 2 · the stub speaks when spoken to, and says nothing
	ok.call(SimOwnership.attention(s0).is_empty() and SimOwnership.directives(s0).is_empty(),
		"ownership: the stub raises no attention and speaks no directives")

	# ── 3 · the pre-registered recruit_ads lane stays zero through a tick
	var s1 := _state()
	SimEngine.weekly_tick(s1)
	ok.call(int((s1.get_meta("pnl", {}) as Dictionary).get("recruit_ads", -1)) == 0,
		"ownership: the neutral tick books no recruitment adverts")

	# ── 4 · NEUTRALITY: paper on the books changes no number in the tick
	var ctrl := _state()
	var full := _state()
	_populate(full)
	SimEngine.weekly_tick(ctrl)
	SimEngine.weekly_tick(full)
	ok.call(ctrl.cash == full.cash and ctrl.traction == full.traction
		and ctrl.morale == full.morale,
		"ownership: instruments on the books do not move the money (stub is neutral)")

	# ── 5 · the records ride the tick untouched
	ok.call(int((full.instruments[0] as Dictionary).get("cap", 0)) == 4_000_000
		and absf(float(full.esop.get("pool_pct", 0.0)) - 10.0) < 0.001
		and bool(full.raise_state.get("active", false)),
		"ownership: the cap table's paper survives the tick untouched")
