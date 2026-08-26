extends RefCounted
## LANE SUITE — the feature inventory (DAG2 W2 L-MAKE). Spec:
## docs/design/DECISIONS.md (PRODUCT desk corrected + THE KANBAN WALL) +
## docs/design/DAG2.md + the L-MAKE brief.
##
## What these pins hold:
##   · births — the seeded default wall, landed bets becoming records, the
##     kind→job map, era×ambition keep pricing, risky-born-creaky, the
##     backfired null, the dedup guard, the rebuild heal
##   · measured — unknown for four weeks, then the landing's actual payoff
##     × the salted market spread, deterministic per (seed, week)
##   · solidity — the jar's face: plumbing creaks first, the creak load
##     converges on ceil((debt−40)/15), healing follows paydown, and the
##     ONE-TAX law (SimRoadmap.debt_drag is the only velocity tax)
##   · the shelf — 3..5 deterministic priced ideas, gap jobs first, the
##     rebuild when the wall creaks, era-capped ambitions
##   · the NEXT queue — commit-or-queue, the freed slot, reorder, dequeue
##   · money — INERT until the coordinator's feature_keep package (the
##     record's lanes are fixed); neutrality is pinned here until then
##
## The porting law: a check lands HERE first, then in the same order in
## unity/Runway.Core.Tests/Lanes/FeaturesTests.cs. Same checks, same order,
## byte-identical messages. Checks that would pin a specific DRAW pin the
## LAW instead (the C# Rng diverges in values by design).

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
		 "unit_cost_add": 0.0, "product_id": "", "born_wk": 0, "measured": 0.0},
		{"id": "ft_pipes", "name": "the data plumbing", "job": "plumbing",
		 "family": "", "solidity": "creaky", "keep_wk": 25,
		 "unit_cost_add": 0.5, "product_id": "", "born_wk": 0, "measured": 0.0}]
	s.tech_debt = 50.0   # target load 1 — the fixture's creak is jar-stable

## A bet already built, waiting for the dice.
static func _mk_bet(s: GameState, kind: String, amb: int, name: String) -> Dictionary:
	var cost := SimRoadmap.bet_cost(kind, amb)
	var bet := {"id": "tb_%s_%d" % [kind, s.bets.size()], "name": name,
		"desc": "", "kind": kind, "ambition": amb, "cost_rnd_weeks": cost,
		"progress": cost, "committed": false, "committed_week": s.week,
		"ready": true, "shipped": false, "shipped_week": 0, "band": "", "era": s.era}
	s.bets.append(bet)
	return bet

static func run(ok: Callable) -> void:
	# ── 1 · the field exists, at a safe default
	var s0 := _state()
	ok.call(s0.features.is_empty(),
		"features: a fresh state ships no feature inventory")

	# ── 2 · a truly blank state (the draft) seeds nothing
	var blank := GameState.new()
	blank.sim_seed = 7
	blank.traction = 0
	blank.product = 0
	SimFeatures.seed_defaults(blank)
	ok.call(blank.features.is_empty(),
		"features: the blank draft state seeds no wall")

	# ── 3/4 · an old save's minimal wall, derived from its offers
	var sw := _state()
	SimEngine.add_offer(sw, "the massage hour", "per session", 40.0, 10.0, 2.0, 1.0)
	SimFeatures.seed_defaults(sw)
	ok.call(sw.features.size() == 3
		and String((sw.features[1] as Dictionary).get("name", "")) == "the massage hour"
		and int((sw.features[0] as Dictionary).get("born_wk", -1)) == 0,
		"features: an old save seeds its minimal wall from the offers")
	var jobs: Array = []
	for f in sw.features:
		jobs.append(String((f as Dictionary).get("job", "")))
	ok.call(jobs.has("pull") and jobs.has("keep") and jobs.has("plumbing"),
		"features: the seeded wall covers pull, keep and the plumbing")

	# ── 5 · NEUTRALITY until the feature_keep package: no money moves
	var ctrl := _state()
	ctrl.tech_debt = 50.0
	var full := _state()
	_populate(full)
	SimEngine.weekly_tick(ctrl)
	SimEngine.weekly_tick(full)
	ok.call(ctrl.cash - full.cash == SimFeatures.keep_total(full) - SimFeatures.keep_total(ctrl)
		and ctrl.traction == full.traction and ctrl.product == full.product,
		"features: keep-costs bill into burn, dollar for dollar")

	# ── 6 · the records ride the tick untouched (jar-stable fixture)
	ok.call(int((full.features[0] as Dictionary).get("keep_wk", 0)) == 40
		and String((full.features[1] as Dictionary).get("solidity", "")) == "creaky",
		"features: the inventory survives the tick untouched")

	# ── 7/8 · a landed bet joins the wall: kind→job, era×ambition keep
	var sl := _state()
	var b7 := _mk_bet(sl, "reach", 2, "group scheduling")
	SimRoadmap.ship_bet(sl, b7, func() -> int: return 20)
	SimFeatures.tick_pre(sl, {"lines": []})
	var f7: Dictionary = sl.features[sl.features.size() - 1] if not sl.features.is_empty() else {}
	ok.call(sl.features.size() >= 1 and String(f7.get("job", "")) == "pull"
		and String(f7.get("name", "")) == "group scheduling"
		and int(f7.get("born_wk", -1)) == sl.week
		and float(f7.get("measured", -1.0)) == 0.0
		and String(f7.get("solidity", "")) == "solid",
		"features: a landed reach bet joins the wall as brings-them-in")
	ok.call(int(f7.get("keep_wk", 0)) == 6
		and absf(float(f7.get("unit_cost_add", 0.0)) - 0.3) < 0.0001,
		"features: a landing prices its keep from era and ambition")

	# ── 9 · a backfired launch ships nothing worth keeping
	var sb := _state()
	var b9 := _mk_bet(sb, "reach", 1, "the referral loop")
	SimRoadmap.ship_bet(sb, b9, func() -> int: return 1)
	SimFeatures.tick_pre(sb, {"lines": []})
	ok.call(sb.features.size() == 3,   # only the seeded defaults, no landing
		"features: a backfired launch ships nothing worth keeping")

	# ── 10 · a risky ship is born creaky (shipped in a hurry)
	var sr := _state()
	var b10 := _mk_bet(sr, "retention", 1, "SMS pack")
	SimRoadmap.ship_bet(sr, b10, func() -> int: return 7)
	SimFeatures.tick_pre(sr, {"lines": []})
	var f10: Dictionary = sr.features[sr.features.size() - 1]
	ok.call(String(f10.get("solidity", "")) == "creaky"
		and String(f10.get("job", "")) == "keep",
		"features: a risky ship is born creaky")

	# ── 11 · the landing is born once, not twice
	var n11 := sr.features.size()
	SimFeatures.tick_pre(sr, {"lines": []})
	ok.call(sr.features.size() == n11,
		"features: the landing is born once, not twice")

	# ── 12 · a rebuild landing makes the worst creak solid again
	var sh := _state()
	_populate(sh)
	var b12 := _mk_bet(sh, "debt", 1, "Hardening sprint")
	SimRoadmap.ship_bet(sh, b12, func() -> int: return 15)
	SimFeatures.tick_pre(sh, {"lines": []})
	ok.call(String((sh.features[1] as Dictionary).get("solidity", "")) == "solid",
		"features: a rebuild landing makes the worst creak solid again")

	# ── 13-16 · promised vs measured: four quiet weeks, then the verdict
	var sm := _state()
	var b13 := _mk_bet(sm, "reach", 1, "the referral loop")
	SimRoadmap.ship_bet(sm, b13, func() -> int: return 10)   # fine → 4 units
	SimFeatures.tick_pre(sm, {"lines": []})
	var fm: Dictionary = sm.features[sm.features.size() - 1]
	for i in 3:
		sm.week += 1
		SimFeatures.tick_post(sm, {})
	ok.call(float(fm.get("measured", -1.0)) == 0.0,
		"features: measured stays unknown until the fourth week")
	sm.week += 1
	SimFeatures.tick_post(sm, {})
	var m13 := float(fm.get("measured", 0.0))
	ok.call(m13 >= 2.95 and m13 <= 5.05,
		"features: the market answers inside the promised spread")
	var sm2 := _state()
	var b14 := _mk_bet(sm2, "reach", 1, "the referral loop")
	SimRoadmap.ship_bet(sm2, b14, func() -> int: return 10)
	SimFeatures.tick_pre(sm2, {"lines": []})
	for i2 in 4:
		sm2.week += 1
		SimFeatures.tick_post(sm2, {})
	ok.call(absf(float((sm2.features[sm2.features.size() - 1] as Dictionary).get("measured", -9.0)) - m13) < 0.0001,
		"features: the measured verdict is deterministic")
	ok.call(SimFeatures.promised_units(sm, fm) == 4,
		"features: the promise is recovered from the launch history")

	# ── 17-20 · the jar's face: plumbing first, converge, stop, heal
	var sj := _state()
	sj.tech_debt = 70.0   # target load: ceil(30/15) = 2
	sj.features = []
	for i3 in 5:
		sj.features.append({"id": "ft_s%d" % i3, "name": "solid thing %d" % i3,
			"job": "keep", "family": "", "solidity": "solid", "keep_wk": 5,
			"unit_cost_add": 0.0, "product_id": "", "born_wk": 0, "measured": 0.0})
	sj.features.append({"id": "ft_plumb", "name": "the billing core",
		"job": "plumbing", "family": "", "solidity": "solid", "keep_wk": 9,
		"unit_cost_add": 0.0, "product_id": "", "born_wk": 0, "measured": 0.0})
	SimFeatures.tick_post(sj, {})
	var plumb: Dictionary = sj.features[5]
	ok.call(String(plumb.get("solidity", "")) == "creaky" and SimFeatures.creak_load(sj) == 1,
		"features: the debt creaks the plumbing first")
	SimFeatures.tick_post(sj, {})
	ok.call(SimFeatures.creak_load(sj) == 2
		and SimFeatures.expected_creak_load(sj.tech_debt) == 2,
		"features: the jar's level becomes the wall's creak count")
	SimFeatures.tick_post(sj, {})
	ok.call(SimFeatures.creak_load(sj) == 2,
		"features: the creaks stop at the jar's level")
	sj.tech_debt = 10.0
	SimFeatures.tick_post(sj, {})
	SimFeatures.tick_post(sj, {})
	ok.call(SimFeatures.creak_load(sj) == 0,
		"features: paying the jar down heals the wall")

	# ── 21 · THE ONE-TAX LAW: the jar's drag is the only velocity tax
	var st1 := _state()
	st1.tech_debt = 70.0
	var st2 := _state()
	st2.tech_debt = 70.0
	_populate(st2)
	st2.tech_debt = 70.0
	ok.call(absf(SimRoadmap.capacity_pool(st1) - SimRoadmap.capacity_pool(st2)) < 0.0001
		and SimFeatures.creak_tax_pct(st1) == int(round((1.0 - SimRoadmap.debt_drag(st1)) * 100.0)),
		"features: creaks never tax twice — the jar's drag is the only tax")

	# ── 22-26 · the shelf: priced, deterministic, gap-first, era-capped
	var ss := _state()
	SimEngine.add_offer(ss, "the planner", "per month", 30.0, 8.0, 2.0, 1.0)
	SimFeatures.seed_defaults(ss)
	var shelf := SimFeatures.shelf_candidates(ss)
	var priced := not shelf.is_empty()
	for c in shelf:
		var cd: Dictionary = c
		if int(cd.get("cost_usd", 0)) <= 0 or int(cd.get("weeks", 0)) < 1 \
				or int(cd.get("odds_pct", 0)) < 5 or int(cd.get("odds_pct", 0)) > 95:
			priced = false
	ok.call(shelf.size() >= 3 and shelf.size() <= 5 and priced,
		"features: the shelf holds three to five priced ideas")
	var shelf2 := SimFeatures.shelf_candidates(ss)
	var same := shelf.size() == shelf2.size()
	for i4 in shelf.size():
		if same and String((shelf[i4] as Dictionary).get("id", "")) != String((shelf2[i4] as Dictionary).get("id", "x")):
			same = false
	ok.call(same,
		"features: the shelf re-draws the same paper within a week")
	var sc := _state()
	_populate(sc)
	var shelf3 := SimFeatures.shelf_candidates(sc)
	var has_rebuild := false
	for c3 in shelf3:
		if String((c3 as Dictionary).get("kind", "")) == "debt":
			has_rebuild = true
	ok.call(has_rebuild,
		"features: a creaky wall puts a rebuild on the shelf")
	var has_charge := false
	for c4 in shelf:
		if String((c4 as Dictionary).get("job", "")) == "charge":
			has_charge = true
	ok.call(has_charge,
		"features: the shelf fills the wall's missing jobs first")
	var capped := true
	for c5 in shelf:
		if int((c5 as Dictionary).get("ambition", 9)) > SimRoadmap.ambition_cap(ss):
			capped = false
	ok.call(capped,
		"features: shelf ambitions respect the era's cap")

	# ── 27-29 · commit or queue, the freed slot, reorder + dequeue
	var sq := _state()
	SimEngine.add_offer(sq, "the planner", "per month", 30.0, 8.0, 2.0, 1.0)
	SimFeatures.seed_defaults(sq)
	var cands := SimFeatures.shelf_candidates(sq)
	var r1 := SimFeatures.commit_shelf(sq, String((cands[0] as Dictionary).get("id", "")))
	var cands2 := SimFeatures.shelf_candidates(sq)
	var r2 := SimFeatures.commit_shelf(sq, String((cands2[0] as Dictionary).get("id", "")))
	ok.call(r1 == "committed" and r2 == "queued"
		and SimRoadmap.committed_bets(sq).size() == 1
		and SimFeatures.queued_bets(sq).size() == 1,
		"features: committing the shelf points the team or queues")
	var committed_id := String((SimRoadmap.committed_bets(sq)[0] as Dictionary).get("id", ""))
	SimRoadmap.uncommit_bet(sq, committed_id)
	SimFeatures.tick_pre(sq, {"lines": []})
	ok.call(SimRoadmap.committed_bets(sq).size() == 1
		and SimFeatures.queued_bets(sq).is_empty(),
		"features: the queue takes the freed slot in order")
	var so := _state()
	so.era = "office"
	SimRoadmap.ensure_board(so)
	var board := SimRoadmap.board_bets(so)
	var qa := String((board[0] as Dictionary).get("id", ""))
	var qb := String((board[1] as Dictionary).get("id", ""))
	SimFeatures.enqueue_bet(so, qa)
	SimFeatures.enqueue_bet(so, qb)
	SimFeatures.queue_move(so, qb, -1)
	var q_after := SimFeatures.queued_bets(so)
	var reordered := String((q_after[0] as Dictionary).get("id", "")) == qb
	SimFeatures.dequeue_bet(so, qa)
	var qa_bet := SimRoadmap.bet_by_id(so, qa)
	ok.call(reordered and SimFeatures.queued_bets(so).size() == 1
		and int(qa_bet.get("committed_week", -9)) == 0,
		"features: the queue reorders and returns to the shelf")

	# ── 30 · attention: the creaks named inside the ticker's 40 characters
	var sa := _state()
	_populate(sa)
	var rows := SimFeatures.attention(sa)
	var creak_row: Dictionary = rows[0] if not rows.is_empty() else {}
	(sa.features[1] as Dictionary)["solidity"] = "breaking"
	var rows2 := SimFeatures.attention(sa)
	var break_row: Dictionary = rows2[0] if not rows2.is_empty() else {}
	ok.call(String(creak_row.get("key", "")) == "creak_tax"
		and int(creak_row.get("severity", 0)) == 2
		and String(creak_row.get("label", "")).length() <= 40
		and String(break_row.get("key", "")) == "feature_breaking"
		and int(break_row.get("severity", 0)) == 3
		and String(break_row.get("label", "")).length() <= 40
		and String(creak_row.get("desk", "")) == "what we make",
		"features: attention names the creaks inside 40 characters")

	# ── 31 · keep_total is pure arithmetic over the wall
	ok.call(SimFeatures.keep_total(sa) == 65,
		"features: keep_total is the sum of the wall's keep lines")

	# ── 32 · the DM hears the creak, and only the creak
	var sd := _state()
	_populate(sd)
	var dd := SimFeatures.directives(sd)
	var quiet := _state()
	SimEngine.add_offer(quiet, "the planner", "per month", 30.0, 8.0, 2.0, 1.0)
	SimFeatures.seed_defaults(quiet)
	ok.call(dd.size() >= 1 and String(dd[0]).contains("creak")
		and SimFeatures.directives(quiet).is_empty(),
		"features: the DM hears the creak, and only the creak")
