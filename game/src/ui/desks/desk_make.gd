class_name DeskMake
extends RefCounted
## DESK — THE COMPANY · "what we make". W2 lane: L-MAKE.
## THE QUESTION THIS DESK ANSWERS: "what are we making, and how solid is it?"
##
## THE KANBAN WALL (docs/design/DECISIONS.md, PICK style 5; pixels: mockups
## 16-what-we-make-wall.html + 17-wall-scale.html):
##   HERO      the name plate (identity/topics), version, the counts line,
##             the creak line, the self-ship clock.
##   THE WALL  four columns — THE SHELF (priced ideas, commit arm) -> NEXT
##             (the queue, reorder) -> BUILDING (progress + $/wk + stand-down
##             −25%) -> READY (SHIP rolls the dice at the press, behind the
##             pre-roll review; the slip clock is honest).
##   LIVE      the inventory, grouped by job. Rung 1 (≤12 live): every
##             feature a card. Rung 2: FAMILIES (ink tags) + attention-first
##             folds — creaks and fresh landings stay face-up, the healthy
##             crowd folds to "the other N solid — $X/wk ▸".
##   RUNG 3    (a second product exists): THE LINEUP — one hero-row per
##             product, press opens its wall — and the SHARED PLUMBING band
##             (product_id "" plumbing; a creak THERE taxes every build).
##   FOOT      build + keep + per-unit -> the works + the creak tax, matching
##             SimFeatures' own numbers exactly.
##
## Solidity wears the kit's marks, not the mockup's palette: solid is calm
## (no dot), creaky = sev 2, breaking = sev 3 — red means act.
## The engine half lives in game/src/core/lanes/sim_features.gd; this file
## only reads it and routes presses through the roadmap's own doors.

const QUESTION := "what are we making, and how solid is it?"

const COL_W := 272.0
const COL_H := 404.0
const COL_Y := 112.0
const LIVE_Y := 524.0
const FOOT_Y := 818.0
const RULES_Y := 848.0
const JOB_ORDER := ["pull", "keep", "charge", "plumbing"]
const JOB_LABEL := {"pull": "BRINGS THEM IN", "keep": "KEEPS THEM",
	"charge": "LETS US CHARGE", "plumbing": "THE PLUMBING"}
const RUNG2_LIVE := 13    # DECISIONS: rung 1 holds "≤ ~12 live"
const FRESH_WKS := 4      # a landing stays face-up while its verdict settles

## The group overview's card IS this page's hero (DECISIONS: the quartet).
static func hero_summary(state) -> Dictionary:
	var s: GameState = state
	var creaks := SimFeatures.creak_count(s)
	var line := "%d live · %d building · %d ready" % [s.features.size(),
		SimRoadmap.committed_bets(s).size(), SimRoadmap.ready_bets(s).size()]
	if creaks > 0:
		line += " · %d creak%s" % [creaks, "" if creaks == 1 else "s"]
	return {"big": "v0.%d" % maxi(1, s.product / 10), "line": line}

static func draw(b) -> void:
	var state: GameState = b.state
	SimRoadmap.ensure_board(state)
	SimFeatures.seed_defaults(state)
	match String(b.desk.get("mode", "")):
		"preroll":
			_preroll_card(b)
			return
		"shipped":
			_ship_card(b)
			return
		"family":
			_family_page(b)
			return
		"job":
			_job_page(b)
			return
		"product":
			_product_page(b)
			return
	if not SimFeatures.product_ids(state).is_empty():
		_lineup(b, state)
		return
	_hero(b, state)
	_wall(b, state)
	_live_band(b, state)
	_cost_foot(b, state)

# ═══════════════════════════════ THE HERO ════════════════════════════════════

static func _hero(b, state: GameState) -> void:
	var name := String(state.topics.get("make_name", state.company_name))
	var line := String(state.topics.get("make_line", state.company_idea)).substr(0, 60)
	if line == "":
		line = "the thing we make"
	var version := "v0.%d" % maxi(1, state.product / 10)
	DeskKit.hero_plate(b, 10.0, 6.0, name.substr(0, 22), version, "what we make")
	# THE MAKE illustration (DECISIONS § THE THREE BINDER ILLUSTRATIONS) —
	# the thing the company makes, beside the plate; the plate alone is the
	# fallback and the page never waits.
	if FileAccess.file_exists(PortraitClient.MAKE_PATH):
		var mimg := Image.new()
		if mimg.load(PortraitClient.MAKE_PATH) == OK:
			var mtr := TextureRect.new()
			mtr.texture = ImageTexture.create_from_image(mimg)
			mtr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
			mtr.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
			mtr.position = Vector2(196.0, 4.0)
			mtr.set_deferred("size", Vector2(84.0, 84.0))
			mtr.mouse_filter = Control.MOUSE_FILTER_IGNORE
			b.pane().add_child(mtr)
	b.label(line, Vector2(450.0, 8.0), 32, Binder.INK, 660.0)
	var counts := "%d live · %d building · %d ready · %d on the shelf" % [
		state.features.size(), SimRoadmap.committed_bets(state).size(),
		SimRoadmap.ready_bets(state).size(), _shelf_rows(state).size()]
	b.label(counts, Vector2(450.0, 52.0), 21, Color(Binder.INK, 0.6), 660.0)
	var creaks := SimFeatures.creak_count(state)
	if creaks > 0:
		var tax := SimFeatures.creak_tax_pct(state)
		var cl := "creaks at '%s'" % SimFeatures.worst_creak_name(state)
		if tax > 0:
			cl += " — build speed −%d%%" % tax
		b.label(cl, Vector2(450.0, 78.0), 21, Binder.PEN, 500.0)
	var ready := SimRoadmap.ready_bets(state)
	if not ready.is_empty():
		var rb: Dictionary = ready[0]
		var left := SimRoadmap.stall_left(state, rb)
		DeskKit.clock_chip(b, 956.0, 78.0, ("self-ships in %d wk" % left) if left > 0
			else "self-ships this week")
	b.label("SHIP rolls the dice · stand-down burns 25%", Vector2(830.0, 52.0), 17,
		Color(Binder.INK, 0.5), 300.0)

# ═══════════════════════════ THE PIPELINE WALL ═══════════════════════════════

static func _wall(b, state: GameState) -> void:
	var shelf := _shelf_rows(state)
	var queued := SimFeatures.queued_bets(state)
	var building := SimRoadmap.committed_bets(state)
	var ready := SimRoadmap.ready_bets(state)
	# ── THE SHELF: priced ideas, press one to commit it
	var c1 := DeskKit.wall_column(b, 10.0, COL_Y, COL_W, COL_H,
		"THE SHELF", "priced ideas — take one on")
	# the column never overflows its box: three exact, or two + the honest count
	var shelf_cap := 3 if shelf.size() <= 3 else 2
	var shown := 0
	for row in shelf:
		if shown >= shelf_cap:
			break
		var rd: Dictionary = row
		DeskKit.wall_card(b, c1, {"title": String(rd.get("name", "")),
			"facts": ["$%s · %d wk · %d%% · %s" % [b.fmt(int(rd.get("cost_usd", 0))),
				int(rd.get("weeks", 1)), int(rd.get("odds_pct", 50)),
				String(rd.get("job_words", ""))]]})
		_shelf_arm(b, c1, state, rd)
		shown += 1
	if shelf.size() > shown:
		DeskKit.more(b, Vector2(float(c1.get("content_x", 18.0)), float(c1.get("cursor", 0.0))),
			shelf.size() - shown, "ideas wait")
		c1["cursor"] = float(c1.get("cursor", 0.0)) + 30.0
	b.label("write your own in THIS WEEK — the world prices it",
		Vector2(float(c1.get("content_x", 18.0)), float(c1.get("cursor", 0.0)) + 2.0),
		15, Color(Binder.INK, 0.45), COL_W - 20.0)
	# ── NEXT: the committed queue, reorder freely
	var c2 := DeskKit.wall_column(b, 292.0, COL_Y, COL_W, COL_H,
		"NEXT", "the queue — reorder freely")
	if queued.is_empty():
		b.label("nothing waits — the shelf commits straight to the team",
			Vector2(float(c2.get("content_x", 0.0)), float(c2.get("cursor", 0.0)) + 4.0),
			15, Color(Binder.INK, 0.45), COL_W - 20.0)
	var qn := 0
	for qbet in queued:
		if qn >= 3:
			break
		var qd: Dictionary = qbet
		var starts := "when a slot frees" if qn == 0 else "after '%s'" % \
			String((queued[qn - 1] as Dictionary).get("name", "")).substr(0, 14)
		DeskKit.wall_card(b, c2, {"title": String(qd.get("name", "")),
			"facts": ["starts · %s" % starts]})
		_queue_words(b, c2, state, String(qd.get("id", "")), qn, queued.size())
		qn += 1
	if queued.size() > qn:
		DeskKit.more(b, Vector2(float(c2.get("content_x", 0.0)), float(c2.get("cursor", 0.0))),
			queued.size() - qn, "queued behind")
	# ── BUILDING: money burning against odds
	var head3 := "BUILDING" if building.size() <= 1 else "BUILDING — %d" % building.size()
	var c3 := DeskKit.wall_column(b, 574.0, COL_Y, COL_W, COL_H,
		head3, "money burning against odds")
	var rnd_wk := SimFeatures.build_total(state)
	# three compact bars fit; the rebuild note only rides when there is room
	var build_cap := 3 if building.size() <= 3 else 2
	var rebuild_fact := building.size() <= 2
	var bn := 0
	for bet in building:
		if bn >= build_cap:
			break
		var bd: Dictionary = bet
		var facts := ["%d%% built · odds %d%% · $%s/wk" % [SimRoadmap.progress_pct(bd),
			SimRoadmap.ship_odds_pct(state, bd),
			b.fmt(rnd_wk / maxi(building.size(), 1))]]
		if rebuild_fact and String(bd.get("kind", "")) == "debt" \
				and SimFeatures.creak_count(state) > 0:
			facts.append("kills the creak when it lands")
		DeskKit.wall_card(b, c3, {"title": String(bd.get("name", "")), "facts": facts,
			"progress": clampf(float(bd.get("progress", 0.0))
				/ maxf(float(bd.get("cost_rnd_weeks", 1.0)), 0.001), 0.0, 1.0)})
		_stand_down_arm(b, c3, state, bd)
		bn += 1
	if building.size() > bn:
		DeskKit.more(b, Vector2(float(c3.get("content_x", 0.0)),
			float(c3.get("cursor", 0.0))), building.size() - bn, "building behind")
	if building.is_empty():
		b.label("nothing committed — the rnd money polishes base quality",
			Vector2(float(c3.get("content_x", 0.0)), float(c3.get("cursor", 0.0)) + 4.0),
			15, Color(Binder.INK, 0.45), COL_W - 20.0)
	# ── READY: ship it, or it ships itself
	var c4 := DeskKit.wall_column(b, 856.0, COL_Y, COL_W, COL_H,
		"READY", "ship it, or it ships itself")
	var rn := 0
	for rbet in ready:
		if rn >= 2:
			break
		var rdd: Dictionary = rbet
		var left := SimRoadmap.stall_left(state, rdd)
		DeskKit.wall_card(b, c4, {"title": String(rdd.get("name", "")), "ready": true,
			"facts": ["promises · %s" % _bet_job_words(rdd),
				("slips out in %d wk" % left) if left > 0 else "slips out this week"]})
		_ship_button(b, c4, state, rdd)
		rn += 1
	if ready.is_empty():
		b.label("nothing built yet — a finished bet waits here for the dice",
			Vector2(float(c4.get("content_x", 0.0)), float(c4.get("cursor", 0.0)) + 4.0),
			15, Color(Binder.INK, 0.45), COL_W - 20.0)

## THE SHELF ROWS the wall shows: the roadmap's own dressed candidates first
## (real paper on the board), then the lane's generated ideas. One shape.
static func _shelf_rows(state: GameState) -> Array:
	var out: Array = []
	for bet in SimRoadmap.board_bets(state):
		var bd: Dictionary = bet
		if bool(bd.get("committed", false)) or bool(bd.get("ready", false)) \
				or int(bd.get("committed_week", 0)) < 0 \
				or float(bd.get("progress", 0.0)) > 0.0:
			continue
		var dc := SimRoadmap.bet_dc(bd)
		out.append({"id": String(bd.get("id", "")), "board": true,
			"name": String(bd.get("name", "")),
			"cost_usd": int(float(bd.get("cost_rnd_weeks", 3.0)) * SimRoadmap.RND_PER_WEEK),
			"weeks": int(ceil(float(bd.get("cost_rnd_weeks", 3.0)))),
			"odds_pct": SimRoadmap.ship_odds_pct(state, bd),
			"job_words": _bet_job_words(bd), "dc": dc})
	for cand in SimFeatures.shelf_candidates(state):
		var cd: Dictionary = cand
		cd["board"] = false
		out.append(cd)
	return out

static func _bet_job_words(bet: Dictionary) -> String:
	var kind := String(bet.get("kind", ""))
	if kind == "debt":
		return "kills a creak"
	var job := String(SimFeatures.KIND_TO_JOB.get(kind, "plumbing"))
	return String(SimFeatures.JOB_WORDS.get(job, "plumbing"))

## The commit arm under a shelf card — the mutation law's two-tap: the first
## press quotes the price, the second books it (team, or the NEXT queue).
static func _shelf_arm(b, col: Dictionary, state: GameState, row: Dictionary) -> void:
	var id := String(row.get("id", ""))
	var full := SimRoadmap.committed_bets(state).size() >= SimRoadmap.wip_cap(state)
	var plain := "queue it ->" if full else "take it on ->"
	var armed := "sure? $%s · %d wk" % [b.fmt(int(row.get("cost_usd", 0))),
		int(row.get("weeks", 1))]
	var is_board := bool(row.get("board", false))
	var fire := func() -> void:
		if is_board:
			if SimRoadmap.committed_bets(state).size() < SimRoadmap.wip_cap(state):
				SimRoadmap.commit_bet(state, id)
			else:
				SimFeatures.enqueue_bet(state, id)
		else:
			SimFeatures.commit_shelf(state, id)
	DeskKit.arm(b, "take:" + id, plain, armed,
		Vector2(float(col.get("content_x", 0.0)), float(col.get("cursor", 0.0)) - 8.0),
		fire, 250.0, 18)
	col["cursor"] = float(col.get("cursor", 0.0)) + 26.0

## sooner · later · drop — the queue's own words, one row under its card.
static func _queue_words(b, col: Dictionary, state: GameState, id: String,
		pos: int, count: int) -> void:
	var x := float(col.get("content_x", 0.0))
	var y := float(col.get("cursor", 0.0)) - 8.0
	if pos > 0:
		DeskKit.word(b, "sooner", Vector2(x, y), func() -> void:
			SimFeatures.queue_move(state, id, -1), 17, Color(Binder.INK, 0.7), 70.0)
	if pos < count - 1:
		DeskKit.word(b, "later", Vector2(x + 78.0, y), func() -> void:
			SimFeatures.queue_move(state, id, 1), 17, Color(Binder.INK, 0.7), 62.0)
	DeskKit.word(b, "drop", Vector2(x + 148.0, y), func() -> void:
		SimFeatures.dequeue_bet(state, id), 17, Color(Binder.INK, 0.7), 60.0)
	col["cursor"] = y + 34.0

## Standing down is priced (−25% of the build) — the arm quotes it first.
static func _stand_down_arm(b, col: Dictionary, state: GameState, bet: Dictionary) -> void:
	var id := String(bet.get("id", ""))
	DeskKit.arm(b, "down:" + id, "stand down", "sure? 25% of the build is lost",
		Vector2(float(col.get("content_x", 0.0)), float(col.get("cursor", 0.0)) - 14.0),
		func() -> void: SimRoadmap.uncommit_bet(state, id), 250.0, 18)
	col["cursor"] = float(col.get("cursor", 0.0)) + 18.0

## SHIP: the dice roll AT the press, behind the pre-roll review — the same
## ritual the journal's LOCK IN uses, on this desk's own paper.
static func _ship_button(b, col: Dictionary, state: GameState, bet: Dictionary) -> void:
	var id := String(bet.get("id", ""))
	var x := float(col.get("content_x", 0.0))
	var y := float(col.get("cursor", 0.0)) - 8.0
	var btn: Button = DeskKit.word(b, "SHIP ->", Vector2(x, y), Callable(), 22,
		Binder.INK, 160.0)
	btn.pressed.connect(func() -> void:
		b.desk.erase("armed")
		if not _preroll_rows(state).is_empty():
			b.desk["mode"] = "preroll"
			b.desk["bet"] = id
			b.refresh()
			return
		DeskKit.sign_stroke(b, btn, func() -> void: _fire(b, id)))
	col["cursor"] = y + 36.0

# ═══════════════════════ LIVE — THE INVENTORY BAND ═══════════════════════════

static func _live_band(b, state: GameState) -> void:
	var rung2 := state.features.size() >= RUNG2_LIVE
	var head := "LIVE — WHAT IT'S MADE OF TODAY"
	if rung2:
		head = "LIVE — %d FEATURES · attention face-up, the healthy fold" \
			% state.features.size()
	b.label(head, Vector2(10.0, LIVE_Y), 22, Binder.INK, 1100.0)
	DeskKit.pen_rule(b, LIVE_Y + 30.0)
	var y := LIVE_Y + 44.0
	for job in JOB_ORDER:
		var members := _job_members(state, job, "")
		if members.is_empty():
			continue
		y = _job_row(b, state, job, members, y, rung2)
	# fresh landings teach: the measured note rides the newest card's group

## One job group: the label column, then up to three cards — families and
## attention first at rung 2 — then the honest fold in the label's own column.
static func _job_row(b, state: GameState, job: String, members: Array, y: float,
		rung2: bool) -> float:
	members = _attention_first(members, state)
	b.label(String(JOB_LABEL.get(job, job)).to_upper(), Vector2(10.0, y + 8.0), 15,
		Color(Binder.INK, 0.55), 148.0)
	var slots: Array = []
	var folded_n := 0
	var folded_keep := 0
	if not rung2:
		slots = members.slice(0, 3)
		folded_n = members.size() - slots.size()
		for m in members.slice(3):
			folded_keep += int((m as Dictionary).get("keep_wk", 0))
	else:
		var fams := _families_of(members)
		for fam_name in fams:
			if slots.size() >= 3 or fam_name == "":
				continue
			slots.append({"fam_slot": fam_name, "members": fams[fam_name]})
		for m2 in members:
			var md: Dictionary = m2
			if String(md.get("family", "")) != "":
				continue
			var hot := String(md.get("solidity", "solid")) != "solid" \
				or (int(md.get("born_wk", 0)) > 0
					and state.week - int(md.get("born_wk", 0)) <= FRESH_WKS)
			if hot and slots.size() < 3:
				slots.append(md)
			elif not hot:
				folded_n += 1
				folded_keep += int(md.get("keep_wk", 0))
			else:
				folded_n += 1
				folded_keep += int(md.get("keep_wk", 0))
	var x := 160.0
	for slot in slots:
		var sd: Dictionary = slot
		if sd.has("fam_slot"):
			_family_card(b, state, x, y, sd)
		else:
			_feature_card(b, state, x, y, sd)
		x += 320.0
	if folded_n > 0:
		var open_job := func() -> void:
			b.desk["mode"] = "job"
			b.desk["job"] = job
		DeskKit.word(b, "the other %d — $%d/wk" % [folded_n, folded_keep],
			Vector2(10.0, y + 28.0), open_job, 15, Color(Binder.INK, 0.5), 148.0)
	return y + 64.0

## One live feature card: solidity mark, name, keep right — the one anatomy.
static func _feature_card(b, state: GameState, x: float, y: float, fd: Dictionary) -> void:
	var frame := DeskKit.card_frame(b, x, y, 306.0, 56.0, "")
	var cx := float(frame.get("content_x", x + 18.0))
	var sev := _solidity_sev(String(fd.get("solidity", "solid")))
	if sev > 0:
		DeskKit.sev_dot(b, cx - 6.0, y + 12.0, sev)
	b.label(String(fd.get("name", "")), Vector2(cx + (24.0 if sev > 0 else 0.0), y + 8.0),
		20, Binder.INK, 190.0)
	var v: Label = b.label("$%d/wk" % int(fd.get("keep_wk", 0)),
		Vector2(x + 306.0 - 110.0, y + 8.0), 18, Color(Binder.INK, 0.6), 96.0)
	v.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	var note := _feature_note(state, fd)
	if note != "":
		b.label(note, Vector2(cx, y + 32.0), 14, Binder.PEN if sev > 0
			else Color(Binder.INK, 0.5), 270.0)

## The family card: worst-member mark, ×N, summed keep; opens to members.
static func _family_card(b, state: GameState, x: float, y: float, sd: Dictionary) -> void:
	var fam := String(sd.get("fam_slot", ""))
	var members: Array = sd.get("members", [])
	var keep := 0
	var worst := 0
	var creaky_name := ""
	for m in members:
		var md: Dictionary = m
		keep += int(md.get("keep_wk", 0))
		var s := _solidity_sev(String(md.get("solidity", "solid")))
		if s > worst:
			worst = s
			creaky_name = String(md.get("name", ""))
	var frame := DeskKit.card_frame(b, x, y, 306.0, 56.0, "")
	var cx := float(frame.get("content_x", x + 18.0))
	if worst > 0:
		DeskKit.sev_dot(b, cx - 6.0, y + 12.0, worst)
	b.label("%s ×%d" % [fam, members.size()],
		Vector2(cx + (24.0 if worst > 0 else 0.0), y + 8.0), 20, Binder.INK, 190.0)
	var v: Label = b.label("$%d/wk" % keep, Vector2(x + 306.0 - 110.0, y + 8.0), 18,
		Color(Binder.INK, 0.6), 96.0)
	v.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	if worst > 0 and creaky_name != "":
		b.label("1 creaky member — %s" % creaky_name, Vector2(cx, y + 32.0), 14,
			Binder.PEN, 270.0)
	var open_fam := func() -> void:
		b.desk["mode"] = "family"
		b.desk["family"] = fam
	var hit := DeskKit.word(b, "", Vector2(x, y), open_fam, 14, Binder.INK, 306.0)
	hit.size = Vector2(306.0, 56.0)

## What a card whispers under its name: the creak, or the measured verdict.
static func _feature_note(state: GameState, fd: Dictionary) -> String:
	var solidity := String(fd.get("solidity", "solid"))
	if solidity == "breaking":
		return "BREAKING — rebuild it"
	if solidity == "creaky":
		return "creaky — rebuild candidate"
	var measured := float(fd.get("measured", 0.0))
	if measured > 0.0 and state.week - int(fd.get("born_wk", 0)) <= FRESH_WKS * 2:
		var promised := SimFeatures.promised_units(state, fd)
		if promised > 0:
			return "promised +%d, measured +%.1f" % [promised, measured]
		return "measured +%.1f" % measured
	return ""

static func _solidity_sev(solidity: String) -> int:
	match solidity:
		"breaking": return 3
		"creaky": return 2
	return 0

## THE COLLAPSE LAW's half the fold can never break: creaks and fresh
## landings sort to the front, so a cap can only ever fold the healthy.
static func _attention_first(members: Array, state: GameState) -> Array:
	var scored: Array = []
	for i in members.size():
		var md: Dictionary = members[i]
		var score := 0
		match String(md.get("solidity", "solid")):
			"breaking": score = 30
			"creaky": score = 20
		if int(md.get("born_wk", 0)) > 0 \
				and state.week - int(md.get("born_wk", 0)) <= FRESH_WKS:
			score += 10
		scored.append({"s": score, "i": i, "f": md})
	scored.sort_custom(func(a: Dictionary, b2: Dictionary) -> bool:
		if int(a.get("s", 0)) != int(b2.get("s", 0)):
			return int(a.get("s", 0)) > int(b2.get("s", 0))
		return int(a.get("i", 0)) < int(b2.get("i", 0)))
	var out: Array = []
	for row in scored:
		out.append((row as Dictionary).get("f", {}))
	return out

static func _job_members(state: GameState, job: String, product_id: String) -> Array:
	var out: Array = []
	for f in state.features:
		var fd: Dictionary = f
		if String(fd.get("job", "")) != job:
			continue
		if String(fd.get("product_id", "")) != product_id:
			continue
		out.append(fd)
	return out

static func _families_of(members: Array) -> Dictionary:
	var fams := {}
	for m in members:
		var md: Dictionary = m
		var fam := String(md.get("family", ""))
		if fam == "":
			continue
		if not fams.has(fam):
			fams[fam] = []
		(fams[fam] as Array).append(md)
	return fams

# ═══════════════════════════ THE COST FOOTER ═════════════════════════════════

## The four numbers the engine owns, said once: build + keep + per-unit ->
## the works + the creak tax. They MUST match SimFeatures' own reads.
static func _cost_foot(b, state: GameState) -> void:
	var computed := "building $%s/wk · keeping %d features $%s/wk · they add $%.2f/unit -> the works" % [
		b.fmt(SimFeatures.build_total(state)), state.features.size(),
		b.fmt(SimFeatures.keep_total(state)), SimFeatures.unit_cost_total(state, "")]
	var creaks := SimFeatures.creak_count(state)
	var warning := ""
	if creaks > 0:
		var tax := SimFeatures.creak_tax_pct(state)
		if tax > 0:
			warning = "%d creak%s tax build speed −%d%% — a rebuild bet kills a creak" % [
				creaks, "" if creaks == 1 else "s", tax]
		else:
			warning = "%d creak%s on the wall — a rebuild bet firms them up" % [
				creaks, "" if creaks == 1 else "s"]
	DeskKit.footer(b, {"computed": computed, "y": FOOT_Y, "rules_y": RULES_Y,
		"rules": "features are never free — every landing signs a keep line; the creaky card IS the debt, pointable",
		"warning": warning})

# ═══════════════════════ RUNG 3 — THE LINEUP ═════════════════════════════════

static func _lineup(b, state: GameState) -> void:
	var pids := SimFeatures.product_ids(state)
	b.label("THE LINEUP", Vector2(10.0, 8.0), 40, Binder.INK, 500.0)
	b.label("· %d things we make" % (pids.size() + 1), Vector2(250.0, 20.0), 22,
		Color(Binder.INK, 0.6), 300.0)
	b.label("press a product — its whole wall opens. Red climbs from any feature to this page.",
		Vector2(10.0, 58.0), 20, Color(Binder.INK, 0.6), 1100.0)
	var y := 108.0
	b.label("VERSION", Vector2(430.0, y), 15, Color(Binder.INK, 0.42), 100.0)
	b.label("FEATURES · BUILDING", Vector2(560.0, y), 15, Color(Binder.INK, 0.42), 220.0)
	b.label("KEEP+BUILD", Vector2(930.0, y), 15, Color(Binder.INK, 0.42), 180.0)
	y += 24.0
	y = _lineup_row(b, state, "", String(state.topics.get("make_name",
		state.company_name)), y)
	for pid in pids:
		y = _lineup_row(b, state, String(pid), String(pid), y)
	# SHARED PLUMBING — what every product stands on
	y += 10.0
	b.label("SHARED PLUMBING — every product stands on these; a creak HERE taxes every build",
		Vector2(10.0, y), 20, Binder.INK, 1100.0)
	DeskKit.pen_rule(b, y + 28.0)
	y += 42.0
	var shared := _job_members(state, "plumbing", "")
	var x := 10.0
	var pn := 0
	for f in shared:
		if pn >= 3:
			break
		_feature_card(b, state, x, y, f as Dictionary)
		x += 320.0
		pn += 1
	if shared.size() > pn:
		DeskKit.more(b, Vector2(10.0, y + 62.0), shared.size() - pn, "shared pieces")
	_cost_foot(b, state)

static func _lineup_row(b, state: GameState, pid: String, name: String, y: float) -> float:
	var live := 0
	var worst := 0
	for f in state.features:
		var fd: Dictionary = f
		if String(fd.get("product_id", "")) != pid:
			continue
		if pid == "" and String(fd.get("job", "")) == "plumbing":
			continue   # the flagship's plumbing sits in the SHARED band
		live += 1
		worst = maxi(worst, _solidity_sev(String(fd.get("solidity", "solid"))))
	var keep := 0
	for f2 in state.features:
		var fd2: Dictionary = f2
		if String(fd2.get("product_id", "")) == pid \
				and not (pid == "" and String(fd2.get("job", "")) == "plumbing"):
			keep += int(fd2.get("keep_wk", 0))
	var building := SimRoadmap.committed_bets(state).size() if pid == "" else 0
	var value := keep + (SimFeatures.build_total(state) if pid == "" else 0)
	var open_product := func() -> void:
		b.desk["mode"] = "product"
		b.desk["pid"] = pid
	return DeskKit.hero_row(b, y, {"name": name.substr(0, 22),
		"facts": "v0.%d · %d live · %d building" % [maxi(1, state.product / 10),
			live, building],
		"value": "$%s/wk" % b.fmt(value), "sev": worst,
		"on_press": open_product})

# ═══════════════════════════ THE SUB-PAGES ═══════════════════════════════════

static func _family_page(b) -> void:
	var state: GameState = b.state
	var fam := String(b.desk.get("family", ""))
	DeskKit.back(b, "back to the wall", func() -> void: b.desk.clear())
	var fam_title := fam.to_upper() if fam.to_lower().begins_with("the ") \
		else "THE %s" % fam.to_upper()
	b.label("%s FAMILY" % fam_title, Vector2(10.0, 60.0), 34, Binder.INK, 1100.0)
	var members: Array = []
	for f in state.features:
		if String((f as Dictionary).get("family", "")) == fam:
			members.append(f)
	b.label("%d features · $%d/wk to keep · families are ink — regroup them in arrange"
		% [members.size(), _sum_keep(members)], Vector2(10.0, 106.0), 20,
		Color(Binder.INK, 0.6), 1100.0)
	_card_grid(b, state, members, 150.0)

static func _job_page(b) -> void:
	var state: GameState = b.state
	var job := String(b.desk.get("job", ""))
	DeskKit.back(b, "back to the wall", func() -> void: b.desk.clear())
	b.label(String(JOB_LABEL.get(job, job)), Vector2(10.0, 60.0), 34, Binder.INK, 1100.0)
	var members := _job_members(state, job, "")
	b.label("%d features · $%d/wk to keep" % [members.size(), _sum_keep(members)],
		Vector2(10.0, 106.0), 20, Color(Binder.INK, 0.6), 1100.0)
	_card_grid(b, state, members, 150.0)

static func _product_page(b) -> void:
	var state: GameState = b.state
	var pid := String(b.desk.get("pid", ""))
	DeskKit.back(b, "back to the lineup", func() -> void: b.desk.clear())
	var name := String(state.topics.get("make_name", state.company_name)) if pid == "" else pid
	b.label(name.substr(0, 26), Vector2(10.0, 60.0), 34, Binder.INK, 700.0)
	var y := 116.0
	for job in JOB_ORDER:
		var members := _job_members(state, job, pid)
		if pid == "" and job == "plumbing":
			continue   # the flagship's plumbing lives on the SHARED band
		if members.is_empty():
			continue
		b.label(String(JOB_LABEL.get(job, job)), Vector2(10.0, y + 8.0), 15,
			Color(Binder.INK, 0.55), 148.0)
		var x := 160.0
		var pn := 0
		for f in members:
			if pn >= 3:
				break
			_feature_card(b, state, x, y, f as Dictionary)
			x += 320.0
			pn += 1
		if members.size() > pn:
			DeskKit.more(b, Vector2(10.0, y + 30.0), members.size() - pn, "more")
		y += 66.0
	b.label("the pipeline wall is shared — bets land on the flagship first",
		Vector2(10.0, y + 12.0), 17, Color(Binder.INK, 0.5), 1100.0)

static func _card_grid(b, state: GameState, members: Array, y0: float) -> void:
	var y := y0
	var x := 10.0
	var n := 0
	for f in members:
		if n >= 12:
			break
		_feature_card(b, state, x, y, f as Dictionary)
		x += 320.0
		n += 1
		if n % 3 == 0:
			x = 10.0
			y += 66.0
	if members.size() > n:
		DeskKit.more(b, Vector2(10.0, y + 66.0), members.size() - n, "more features")

static func _sum_keep(members: Array) -> int:
	var total := 0
	for m in members:
		total += int((m as Dictionary).get("keep_wk", 0))
	return total

# ═══════════════ THE SHIP RITUAL (pre-roll review -> dice -> receipt) ══════════

## The engine decides what is outstanding; this page reads it — minus the
## row that IS this press ("a bet is built" never blocks its own shipping).
static func _preroll_rows(state: GameState) -> Array:
	var out: Array = []
	for it in SimEngine.preroll_items(state):
		if String((it as Dictionary).get("key", "")) == "bet_ready":
			continue
		out.append(it)
	return out

static func _preroll_card(b) -> void:
	var state: GameState = b.state
	var id := String(b.desk.get("bet", ""))
	var bet := SimRoadmap.bet_by_id(state, id)
	if bet.is_empty():
		b.desk.clear()
		draw(b)
		return
	var rows := _preroll_rows(state)
	var read: Array = []
	var shown := 0
	for it in rows:
		if shown >= 5:
			read.append("…and %d more, on the threats page." % (rows.size() - shown))
			break
		var itd: Dictionary = it
		read.append("%s%s — %s" % ["! " if int(itd.get("severity", 2)) >= 3 else "",
			String(itd.get("desk", "")), String(itd.get("label", ""))])
		shown += 1
	var to_desk := String((rows[0] as Dictionary).get("desk", "")) if not rows.is_empty() else ""
	DeskKit.review(b, {
		"banner": "before the dice: '%s' ships on a d20 vs DC %d" % [
			String(bet.get("name", "")), SimRoadmap.bet_dc(bet)],
		"read": read,
		"verdict": "fix them, or roll and live with it.",
		"note": "clean ship ~%d%% at build %d — debt, focus and flow all move that number"
			% [SimRoadmap.ship_odds_pct(state, bet), int(state.competences.get("build", 3))],
		"confirm": "roll anyway",
		"cancel": "go fix it",
		"on_confirm": func() -> void: _fire(b, id),
		"on_cancel": func() -> void:
			b.desk.clear()
			if to_desk != "":
				b.focus_desk(to_desk),
	})

## The dice, at the press. The engine rolls; the card that returns is the
## receipt, and the landing joins the wall at the week's close.
static func _fire(b, id: String) -> void:
	var state: GameState = b.state
	var res := SimRoadmap.ship_ready(state, id)
	b.desk.clear()
	if not res.is_empty():
		b.desk["mode"] = "shipped"
		b.desk["ship"] = res
	b.refresh()

static func _ship_card(b) -> void:
	var res: Dictionary = b.desk.get("ship", {})
	if res.is_empty():
		b.desk.clear()
		draw(b)
		return
	DeskKit.back(b, "back to the wall", func() -> void:
		b.desk.clear())
	var y := 90.0
	b.label(String(res.get("event", "")), Vector2(DeskKit.X_ID, y), DeskKit.TITLE,
		Binder.SAGE if String(res.get("band", "")) in ["brilliant", "fine"] else Binder.PEN,
		1100.0)
	y += 64.0
	y = DeskKit.rule(b, y)
	var receipts: Array = res.get("lines", [])
	for l in receipts:
		var t := String(l)
		b.label(t, Vector2(DeskKit.X_ID, y), DeskKit.STATUS, Color(Binder.INK, 0.85), 1100.0)
		y += maxf(b.wrap_h(t, DeskKit.STATUS, 1100.0), 32.0) + 6.0
	if String(res.get("band", "")) != "backfired":
		b.label("it joins the wall at the week's close — keep-cost signs on with it.",
			Vector2(DeskKit.X_ID, y), DeskKit.DETAIL, Color(Binder.INK, 0.6), 1100.0)
	DeskKit.footer(b, {
		"computed": "the dice were %d%+d against DC %d — margin %d" % [
			int(res.get("d20", 0)), int(res.get("mod", 0)), int(res.get("dc", 0)),
			int(res.get("total", 0)) - int(res.get("dc", 0))],
		"rules": "LAUNCH RISK: scope widens the spread — preparation is the only thing that moves the odds.",
	})

## Every control on this page carries its own closure (the kit's idiom); the
## router stays because `binder.desk_press` names desks in its match.
static func handle(_b, _id: String) -> void:
	pass
