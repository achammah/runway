extends RefCounted
## LANE SUITE — street. Spec: docs/design/03-rivals-macro.md §13 (the twin pins).
##
## `tests/sim_engine_test.gd` calls run() after the engine's own checks and hands
## over `ok`, the same assert the whole suite uses: ok.call(cond, "what it pins").
##
## The porting law: a check lands HERE first, then in the same order in
## unity/Runway.Core.Tests/Lanes/StreetTests.cs. Same checks, same order, same
## logic — the two engines do not share PRNG internals, so nothing here pins a
## draw across them, only behaviour. Where a pin needs a particular die, it
## SEARCHES the seeds for one instead of hardcoding it.

const RIVAL_A := {"name": "Vantage", "strength": 48.0, "tactics": ["undercut pricing"],
	"weeks_since_move": 0, "secret": "", "vigor": 60.0, "hype": 25.0,
	"focus": "price", "price_posture": 1.0, "last_action": "", "log": [],
	"cooldowns": {}, "sniffing": 0}
const RIVAL_B := {"name": "Northgate", "strength": 33.0, "tactics": ["shipped a clone feature"],
	"weeks_since_move": 0, "secret": "quietly running out of money", "vigor": 45.0,
	"hype": 18.0, "focus": "product", "price_posture": 1.0, "last_action": "",
	"log": [], "cooldowns": {}, "sniffing": 0}

static func _state(seed_v: int, era: String) -> GameState:
	var s := GameState.new()
	s.sim_seed = seed_v
	s.week = 5
	s.era = era
	s.cash = 80_000
	s.traction = 120
	s.product = 45
	s.morale = 70
	s.hype = 35
	s.biz_what = "Software"
	s.biz_who = "SMB"
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	s.offers = [{"name": "the plan", "unit": "per month", "price": 40.0,
		"fair_price": 40.0, "elasticity": 2.2, "unit_cost": 9.0, "weight": 1.0}]
	s.set_flag("launched")
	s.rivals = [RIVAL_A.duplicate(true), RIVAL_B.duplicate(true)]
	return s

static func run(ok: Callable) -> void:
	_pin_determinism(ok)
	_pin_era_gate(ok)
	_pin_cooldown_law(ok)
	_pin_poach(ok)
	_pin_price_war(ok)
	_pin_macro(ok)

# ── 1 · DETERMINISM ──────────────────────────────────────────────────────────
## The whole lane is dice, and dice the player cannot replay are not fair. Two
## identical states, ten weeks each, must land on the same street.
static func _pin_determinism(ok: Callable) -> void:
	var a := _state(42, "office")
	var b := _state(42, "office")
	for i in 10:
		SimEngine.weekly_tick(a)
		SimEngine.weekly_tick(b)
		a.week += 1
		b.week += 1
	ok.call(JSON.stringify(a.rivals) == JSON.stringify(b.rivals),
		"ten weeks of rivals replay identically from one seed")
	ok.call(absf(a.market_trend - b.market_trend) < 1e-12,
		"the mean-reverting trend replays identically")
	ok.call(JSON.stringify(a.statuses) == JSON.stringify(b.statuses),
		"the statuses the street installed replay identically")

# ── 2 · THE ERA GATE ─────────────────────────────────────────────────────────
## A garage is beneath notice. Rivals still live their lives — that is the
## lesson — but nothing they do lands on a company nobody has heard of.
static func _pin_era_gate(ok: Callable) -> void:
	var g := _state(7, "garage")
	g.employees = [{"name": "Mara Voss", "role": "engineer", "salary": 900, "burnout": 10}]
	var before := JSON.stringify(g.rivals)
	var touched := false
	for i in 30:
		var rep := SimEngine.weekly_tick(g)
		for s in g.statuses:
			var n := String((s as Dictionary).get("name", ""))
			if n in ["price_war", "outshipped", "rival_fud", "rival_stumbled"]:
				touched = true
		g.week += 1
	ok.call(not touched, "a garage never eats a rival status: nobody is answering you yet")
	# the roster itself is the labor lane's to move (resignations, reviews); what
	# this pins is that NO POACH ever rang — the phone-call meta is only ever
	# written here, so its absence is the honest headcount claim
	ok.call(int(g.get_meta("poach_wk", -1)) == -1,
		"nobody poaches from a company the street cannot see")
	ok.call(JSON.stringify(g.rivals) != before,
		"the street lives without you: rivals still move at the garage")

# ── 3 · THE COOLDOWN LAW ─────────────────────────────────────────────────────
## Competitive response has a lag. Two hundred worlds, half a year each: no
## rival ever repeats a move inside its own response time, and the street stays
## mostly quiet — conduct is punctuation, not the sentence.
static func _pin_cooldown_law(ok: Callable) -> void:
	var violations := 0
	var fires := 0
	var quiets := 0
	for s in 200:
		var st := _state(1000 + s, "office")
		var last := {}          # "rival|action" -> week it last fired
		for w in 26:
			SimEngine.weekly_tick(st)
			for i in st.rivals.size():
				var rd: Dictionary = st.rivals[i]
				var act := String(rd.get("last_action", ""))
				if act == "":
					continue
				if act == "quiet":
					quiets += 1
					continue
				fires += 1
				var key := "%d|%s" % [i, act]
				var cd := int(SimStreet.COOLDOWNS.get(act, 0))
				if act == "price_cut" and String(rd.get("focus", "")) == "price":
					cd = 3
				if last.has(key) and st.week - int(last[key]) < cd:
					violations += 1
				last[key] = st.week
			st.week += 1
	ok.call(violations == 0, "no rival repeats a move inside its cooldown (%d breaches)" % violations)
	var quiet_share := float(quiets) / maxf(float(quiets + fires), 1.0)
	ok.call(quiet_share >= 0.20 and quiet_share <= 0.70,
		"the street is mostly quiet but never asleep (quiet share %.2f)" % quiet_share)

# ── 4 · THE POACH ────────────────────────────────────────────────────────────
## Pay-gap arbitrage, priced exactly. The target comes from the labor lane's
## interface; the suite hands over a stubbed one so the resolution can be pinned
## before that desk exists.
static func _pin_poach(ok: Callable) -> void:
	ok.call(absf(SimStreet.poach_odds(0.6, 80.0) - 0.70) < 1e-9,
		"a 60%% pay gap and a full war chest still caps at 0.70 — money does not always win")
	ok.call(absf(SimStreet.poach_odds(0.15, 50.0) - 0.15) < 1e-9,
		"the curve is anchored at a 15%% gap on an average war chest")
	ok.call(absf(SimStreet.poach_odds(0.40, 80.0) - 0.54) < 1e-9,
		"a 40%% gap with money behind it is better than a coin flip")
	# {salary 900, market 2250} -> pay_gap 0.6, exactly as the labor query reports it
	var target := {"index": 0, "name": "Mara Voss", "salary": 900,
		"market_salary": 2250, "pay_gap": 0.6}
	var win_seed := _seed_where(true, 0.70)
	var lose_seed := _seed_where(false, 0.70)
	ok.call(win_seed >= 0 and lose_seed >= 0, "the salt-31 stream has both outcomes to pin")

	var s1 := _state(win_seed, "office")
	s1.employees = [{"name": "Mara Voss", "role": "engineer", "salary": 900, "burnout": 10}]
	s1.morale = 70
	var rep1 := {"lines": [], "events": [], "expired": [], "fired_clocks": []}
	var rd1: Dictionary = s1.rivals[0]
	rd1["vigor"] = 80.0
	var str_before := float(rd1["strength"])
	var landed: bool = SimStreet.resolve_poach(s1, rep1, [], rd1, 1, target)
	ok.call(landed and s1.employees.is_empty(), "the number won: they are off the roster this week")
	ok.call(s1.morale == 64, "the team feels it: morale −6")
	ok.call(absf(float(rd1["strength"]) - (str_before + 2.0)) < 1e-9,
		"the rival banks the hire: strength +2")
	var named := false
	for l in rep1["events"]:
		if String(l).contains("Mara Voss"):
			named = true
	ok.call(named, "the receipt names the person who left")
	ok.call(int(s1.get_meta("poach_wk", -1)) == s1.week,
		"the crew desk is handed the week the phone rang")

	var s2 := _state(lose_seed, "office")
	s2.employees = [{"name": "Mara Voss", "role": "engineer", "salary": 900, "burnout": 10}]
	var rep2 := {"lines": [], "events": [], "expired": [], "fired_clocks": []}
	var rd2: Dictionary = s2.rivals[0]
	rd2["vigor"] = 80.0
	var landed2: bool = SimStreet.resolve_poach(s2, rep2, [], rd2, 1, target)
	ok.call(not landed2 and s2.employees.size() == 1, "a lost recruiting battle costs no headcount")
	ok.call(int(s2.get_meta("poach_failed_wk", -1)) == s2.week,
		"a failed poach opens the counter-offer season for the labor desk")

## The first seed whose salt-31 draw lands on the wanted side of `p` — a pinned
## die found by search, because the two engines' PRNGs are different by design.
static func _seed_where(want_win: bool, p: float) -> int:
	for s in range(1, 400):
		var probe := GameState.new()
		probe.sim_seed = s
		probe.week = 5
		var d := SimEngine.rng_for(probe, SimEngine.SALT_RIVAL_POACH).randf()
		if (d < p) == want_win:
			return s
	return -1

# ── 5 · THE PRICE WAR ────────────────────────────────────────────────────────
## Bertrand undercutting, in one number: the street's reference price drops, so
## holding your list price through a war is what reads as expensive.
static func _pin_price_war(ok: Callable) -> void:
	var s := _state(11, "office")
	SimEngine.add_status(s, "price_war", 3)
	ok.call(absf(SimEngine.street_fair_mult(s) - 0.92) < 1e-12,
		"a price war knocks 8% off the going rate")
	var want := pow(1.0 / 0.92, -2.2)
	ok.call(absf(SimEngine.offers_demand_mult(s) - want) < 1e-9,
		"an offer held at its old list price loses demand at its own elasticity (%.4f)" % want)
	s.statuses = []
	ok.call(absf(SimEngine.street_fair_mult(s) - 1.0) < 1e-12
		and absf(SimEngine.offers_demand_mult(s) - 1.0) < 1e-9,
		"the war ends and the reference price mean-reverts to fair")

# ── 6 · THE MACRO ────────────────────────────────────────────────────────────
## One stylised business cycle plus rare credit shocks. The cycle is a pure
## function of seed and week; the shocks reprice every valuation and term sheet
## at once, which is the whole lesson about raise timing.
static func _pin_macro(ok: Callable) -> void:
	var s := _state(4242, "office")
	s.week = 10
	var want := 1.0 + 0.12 * sin(TAU * 40.0 / 52.0)      # phase = 4242 % 52 = 30
	ok.call(absf(SimStreet.cycle_target(s) - want) < 1e-9,
		"the season is a pure function of seed and week (phase 30)")
	ok.call(SimStreet.trend_band(1.12) == "tailwinds" and SimStreet.trend_band(0.88) == "headwinds"
		and SimStreet.trend_band(1.0) == "calm", "the banner reads the trend in words")

	var base := _state(4242, "office")
	base.week = 10
	var v_base := SimEngine.valuation(base)
	var o_base := SimEngine.generate_offers(base, base.investors)
	var win := _state(4242, "office")
	win.week = 10
	SimEngine.add_status(win, "funding_winter", 8)
	ok.call(absf(SimEngine.shock_val_mult(win) - 0.6) < 1e-12
		and absf(SimEngine.shock_amt_mult(win) - 0.7) < 1e-12
		and absf(SimEngine.shock_spread_mult(win) - 1.25) < 1e-12,
		"a funding winter reprices valuations 0.6x, checks 0.7x, equity asks 1.25x")
	ok.call(absf(float(SimEngine.valuation(win)) - float(v_base) * 0.6) <= 1.0,
		"the winter's 0.6x lands on the valuation before the int cast")
	var o_win := SimEngine.generate_offers(win, win.investors)
	ok.call(absf(float(o_win[0]["amount"]) - float(o_base[0]["amount"]) * 0.6 * 0.7) <= 2.0,
		"a winter's checks come in smaller on the same salt-9 draws")
	# THE PRICE OF MONEY, not the size of the bite: a winter shrinks the check
	# faster (0.42x) than it widens the ask (1.25x), so the absolute equity
	# percentage FALLS while every dollar costs far more of the company. That
	# ratio is the raise-timing lesson, and it is what this pins.
	var per_win := float(o_win[0]["equity_pct"]) / maxf(float(o_win[0]["amount"]), 1.0)
	var per_base := float(o_base[0]["equity_pct"]) / maxf(float(o_base[0]["amount"]), 1.0)
	ok.call(per_win > per_base,
		"a winter charges more of the company per dollar raised (%.2fx)" % (per_win / per_base))
	var boom := _state(4242, "office")
	boom.week = 10
	SimEngine.add_status(boom, "boom", 8)
	ok.call(SimEngine.valuation(boom) > v_base and SimEngine.shock_spread_mult(boom) < 1.0,
		"a boom mirrors it upward: richer valuations, gentler terms")
	ok.call(SimStreet.season(win) == "winter" and SimStreet.season(boom) == "boom"
		and SimStreet.season(base) == "steady",
		"the persisted season word is what the M&A desk reads")
