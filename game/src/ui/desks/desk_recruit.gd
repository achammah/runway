class_name DeskRecruit
extends RefCounted
## DESK — COSTS · "recruitment" (the hiring pipeline with real offers). W2
## lane: L-OWN, per DECISIONS (THE OWNERSHIP CLUSTER §3) and mockup 18 page 3.
##
## THE PAGE: hero (seats open · candidates in motion · offers out, the expiry
## clock face-up) -> THE OPEN SEATS (per-seat card, band from the labor
## market, the advert stepper with the live ≈applicants/wk read) -> THE
## CANDIDATES (four columns: applied -> interviewed -> offer out -> joined;
## interviewing costs the founder's week) -> THE OFFER COMPOSER (cash stepper
## + options stepper with SEPARATE − +, live acceptance odds, the pool-after
## line). Comp design is the lesson: a mercenary chases cash, a missionary
## takes equity, and the pool is finite.
##
## Bands come from the labor MARKET (SimLabor, era-priced) — the price book
## prices structures, never people. All odds come from
## SimOwnership.acceptance_odds — the desk recomputes nothing.
##
## DAG3 Wave B: S1 zero state ("no seats open — a seat is a weekly wage + a
## promise"), S3 DO lane ([send the offer] rides the lane's own two-tap — it
## replaced the in-zone SEND arm), S4 the hero pipeline receipt, the odds
## ticket that ANIMATES on stepper press (the number ticks over ~0.25s, the
## marginal foot re-inks). The page compacted for the lane's 762 anchor:
## money-desk hero, zones 156/230/222, the zone lessons relocated to the
## column subs and the new teaching foot, the ticket one fact line tall.

const QUESTION := "who are we hiring, and will they say yes?"

const CASH_STEP := 10
const OPT_STEP := 0.1

const SHEET_X := 10.0
const Y_FOOT := 806.0
const Y_RULES := 840.0

static func hero_summary(state) -> Dictionary:
	var s: GameState = state
	var seats := _roles(s).size()
	var motion := _candidates_in(s, ["applied", "interviewed", "offer"]).size()
	if seats == 0 and motion == 0:
		return {"big": "hiring", "line": "no seats open — the pipeline starts with an advert"}
	return {"big": "%d seat%s open" % [seats, "" if seats == 1 else "s"],
		"line": "%d candidate%s in motion · %d offer%s out" % [motion,
			"" if motion == 1 else "s", _offers(s).size(), "" if _offers(s).size() == 1 else "s"]}

## S8 — the rail's micro-status: how many seats stand open.
static func micro_status(state) -> String:
	var s: GameState = state
	var seats := _roles(s).size()
	return "%d open" % seats if seats > 0 else ""

## S8 — structurally dormant while the market is shut and nothing is in
## motion: the tab dims to 60%, the page still teaches (never hidden).
static func is_dormant(state) -> bool:
	var s: GameState = state
	return not SimLabor.market_open(s.era) and _roles(s).is_empty() \
		and _candidates_in(s, ["applied", "interviewed", "offer", "joined"]).is_empty() \
		and _offers(s).is_empty()

static func _roles(s: GameState) -> Array:
	return s.recruitment.get("roles", []) if not s.recruitment.is_empty() else []

static func _offers(s: GameState) -> Array:
	return s.recruitment.get("offers_out", []) if not s.recruitment.is_empty() else []

static func _candidates_in(s: GameState, stages: Array) -> Array:
	var out: Array = []
	if s.recruitment.is_empty():
		return out
	for c in s.recruitment.get("candidates", []):
		if stages.has(String((c as Dictionary).get("stage", ""))):
			out.append(c)
	return out

static func draw(b) -> void:
	var state: GameState = b.state
	if String(b.desk.get("mode", "")) == "seats":
		_draw_seats_page(b, state)
		return
	var roles := _roles(state)
	var offers := _offers(state)
	var motion := _candidates_in(state, ["applied", "interviewed", "offer"])

	# ── S1 · the zero state: a seat is a weekly wage + a promise
	if roles.is_empty() and motion.is_empty() and offers.is_empty() \
			and _candidates_in(state, ["joined"]).is_empty():
		var zero_open := func() -> void:
			b.desk["mode"] = "seats"
		DeskKit.zero_state(b, {
			"will_show": "no seats open",
			"would_line": "a seat is a weekly wage + a promise — the band is the market's, not yours",
			"action_label": "open a seat",
			"action_cb": zero_open,
			"wakes_hint": "first candidates arrive when a seat opens" if SimLabor.market_open(state.era)
				else "the labor market opens at coworking — until then, hire the people you know",
		})
		return

	# ── the hero (the money desks' idiom — the band gave its height to the
	# zones so the DO lane keeps the kit's one anchor)
	var big := "%d seat%s open · %d in motion" % [roles.size(),
		"" if roles.size() == 1 else "s", motion.size()]
	b.label(big, Vector2(SHEET_X, 6.0), DeskKit.HERO, DeskKit.INK, 700.0)
	var bw: float = b.font().get_string_size(big, HORIZONTAL_ALIGNMENT_LEFT, -1, DeskKit.HERO).x
	b.label("· %d offer%s out" % [offers.size(), "" if offers.size() == 1 else "s"],
		Vector2(SHEET_X + bw + 14.0, 22.0), DeskKit.ROW, Color(DeskKit.INK, 0.7), 300.0)
	b.label("hiring is a pipeline too — the offer is a design: cash, equity, title.",
		Vector2(SHEET_X, 62.0), DeskKit.DETAIL, Color(DeskKit.INK, 0.6), 720.0)
	if not offers.is_empty():
		var off: Dictionary = offers[0]
		var cand := SimOwnership.cand_by_id(state, String(off.get("candidate_id", "")))
		DeskKit.clock_chip(b, 800.0, 10.0, "%s's offer expires in %d wk" % [
			String(cand.get("name", "someone")).split(" ")[0],
			maxi(int(off.get("expires_wk", 0)) - state.week, 0)])
	# S4 — the hero presses into the pipeline's receipt
	var adv_total := 0
	var rate_total := 0.0
	for r0 in roles:
		adv_total += int((r0 as Dictionary).get("advert_wk", 0))
		rate_total += SimOwnership.arrival_rate_r(state, r0)
	b.mark_control("pipeline_hero", Rect2(SHEET_X - 4.0, 4.0, 640.0, 78.0))
	DeskKit.press_receipt(b, "pipeline_hero", "the pipeline, priced", [
		{"label": "seats open", "value": str(roles.size())},
		{"label": "adverts", "value": "$%s/wk" % SimOwnership.money(adv_total)},
		{"label": "applicants they pull", "value": "≈%.1f/wk" % rate_total},
		{"label": "seats left this era", "value": str(SimLabor.seats_left(state))},
	])
	# S2a — red speaks on the page. R5 — the strip renders in its own slot
	# (96-118); the zones hold the content slot whether or not the desk is red.
	var y := DeskKit.CONTENT_Y0
	DeskKit.ask_strip(b, "recruitment", SHEET_X, 86.0, 1000.0,
		"a lapsed offer is heard on the street")

	# ── zone 1 · THE OPEN SEATS — every seat priced by the labor market
	var z1 := DeskKit.zone(b, DeskKit.X_ID, y, 1120.0, 156.0, 1, "the open seats", "")
	if roles.is_empty():
		DeskKit.empty(b, Vector2(z1.content_x, z1.cursor + 2.0),
			"no seats open.", "open one — the advert is the magnet, the band is the market", true)
	for i in mini(roles.size(), 2):
		var rd: Dictionary = roles[i]
		var fx := float(z1.content_x) + float(i) * 552.0
		var fr := DeskKit.card_frame(b, fx, z1.cursor - 4.0, 532.0, 88.0,
			"%s · band $%s–%s/wk" % [String(rd.get("seat", "?")).to_upper(),
				SimOwnership.money(int(rd.get("band_lo", 0))), SimOwnership.money(int(rd.get("band_hi", 0)))],
			true)
		var rid := String(rd.get("id", ""))
		var adv := int(rd.get("advert_wk", 0))
		DeskKit.money_row(b, fr, "advert  -> ≈%.1f applicants/wk" % SimOwnership.arrival_rate_r(state, rd),
			"$%s/wk" % SimOwnership.money(adv), DeskKit.INK,
			func() -> void: SimOwnership.set_advert(b.state, rid, adv - CASH_STEP),
			func() -> void: SimOwnership.set_advert(b.state, rid, adv + CASH_STEP),
			adv <= 0, adv >= 400)
	if roles.size() > 2:
		# the fold note rides the header lane — the cards keep their room
		var extra := roles.size() - 2
		b.label(("the other %d seat waits below" % extra) if extra == 1
			else ("the other %d seats wait below" % extra),
			Vector2(z1.content_x + 520.0, float(z1.y) + 14.0),
			17, Color(DeskKit.INK, 0.5), 380.0)
	DeskKit.word(b, "open a seat", Vector2(z1.content_x + 940.0, z1.y + 8.0), func() -> void:
		b.desk["mode"] = "seats", DeskKit.DETAIL, Color(DeskKit.INK, 0.7), 170.0)
	y += 156.0 + 10.0

	# ── zone 2 · THE CANDIDATES — four columns, the founder's week is a cost.
	# R9 — 230 tall, not 244: the old slack rode below the fold note, and with
	# the zones starting at CONTENT_Y0 the composer would cross the DO lane.
	var z2 := DeskKit.zone(b, DeskKit.X_ID, y, 1120.0, 230.0, 2, "the candidates", "")
	var col_w := 264.0   ## R9 — 4×264 + 3×16 sits inside the zone; 268 poked out
	var col_h := 154.0
	var cx := float(z2.content_x) - 6.0
	# the old zone lesson lives on the columns now, split where it lands
	var heads := [["applied", "applied", "interviewing costs your week"],
		["interviewed", "interviewed", "ghosting costs your name"],
		["offer out", "offer", ""], ["joined", "joined", ""]]
	for hi in heads.size():
		var col := DeskKit.wall_column(b, cx + float(hi) * (col_w + 16.0), z2.cursor,
			col_w, col_h, String(heads[hi][0]), String(heads[hi][2]))
		var stage := String(heads[hi][1])
		var shown := 0
		var cands := _candidates_in(state, [stage])
		for c in cands:
			if shown >= 1:
				break
			var cd: Dictionary = c
			var cid := String(cd.get("id", ""))
			var facts: Array = ["%s · asks $%s" % [_seat_word(state, cd),
				SimOwnership.money(int(cd.get("ask", 0)))]]
			var cfg := {"title": String(cd.get("name", "?")), "facts": facts}
			match stage:
				"applied":
					cfg["on_press"] = func() -> void:
						SimOwnership.interview(b.state, cid)
				"interviewed":
					facts.append(String(cd.get("profile", "")))
					cfg["on_press"] = func() -> void:
						b.desk["cand"] = cid
						b.desk.erase("cash")
						b.desk.erase("opt")
					if String(b.desk.get("cand", "")) == cid:
						cfg["sev"] = 1
				"offer":
					cfg["ready"] = true
					var off2 := _offer_for(state, cid)
					if not off2.is_empty():
						facts.append("$%s/wk + %.1f%%" % [SimOwnership.money(int(off2.get("cash_wk", 0))),
							float(off2.get("options_pct", 0.0))])
					# S2b — the expiring offer's red row lands on its card
					if shown == 0:
						b.mark_control("offer_out", Rect2(float(col.content_x),
							float(col.cursor), col_w - 16.0, 92.0))
				"joined":
					facts.append("wk %d — in" % int(cd.get("arrived_wk", 0)))
			DeskKit.wall_card(b, col, cfg)
			shown += 1
		if cands.size() > shown:
			# the fold note lives BELOW the column box — a tall card (two facts)
			# never prints through it
			b.label("+%d more wait behind" % (cands.size() - shown), Vector2(float(col.content_x) + 2.0,
				float(col.y) + col_h + 4.0), 16, Color(DeskKit.INK, 0.5), col_w - 20.0)
	y += 230.0 + 10.0

	# ── zone 3 · THE OFFER COMPOSER — comp is a mix you design
	var z3 := DeskKit.zone(b, DeskKit.X_ID, y, 1120.0, 222.0, 3, "the offer composer", "")
	var actions: Array = []
	var cand2 := _composer_target(b, state)
	if cand2.is_empty():
		DeskKit.empty(b, Vector2(z3.content_x, z3.cursor + 6.0),
			"nobody is at the offer stage.",
			"interview a candidate — the composer opens on whoever you pick", true)
	else:
		_composer(b, state, z3, cand2, actions)

	# ── S3 · the DO lane: send the offer when one is composing, grow always
	var lane_seat := func() -> void:
		b.desk["mode"] = "seats"
	actions.append({"label": "open a seat", "tier": "", "cb": lane_seat})
	DeskKit.do_lane(b, actions)

	# ── the teaching foot (the composer zone's law, moved to the money
	# desks' own slots when the zones compacted for the lane)
	b.label("comp is a mix, the pool is finite · signed → TEAM grows a vesting bar · declined → the market hears",
		Vector2(SHEET_X, Y_FOOT), DeskKit.LAW, Binder.BLUE, 1100.0)
	b.label("interviews cost the founder's week · every seat carries the market's band before you advertise",
		Vector2(SHEET_X, Y_RULES), DeskKit.LAW, Color(DeskKit.INK, 0.5), 1100.0)

## THE COMPOSER: steppers left, the WILL-SHE-SAY-YES ticket right. The SEND
## beat rides the DO lane's own two-tap (the pane's one action slot).
static func _composer(b, state: GameState, z3: Dictionary, cand: Dictionary, actions: Array) -> void:
	var cid := String(cand.get("id", ""))
	var ask := int(cand.get("ask", 0))
	var cash := int(b.desk.get("cash", ask - (ask % 10)))
	var opt := float(b.desk.get("opt", 0.0))
	var free := SimOwnership.pool_free(state)
	var fr := DeskKit.card_frame(b, float(z3.content_x), float(z3.cursor) - 2.0, 480.0, 140.0,
		"the mix — %s" % String(cand.get("name", "?")), true)
	DeskKit.money_row(b, fr, "cash", "$%d/wk" % cash, DeskKit.INK,
		func() -> void:
			b.desk["cand"] = cid
			b.desk["cash"] = maxi(cash - CASH_STEP, 10),
		func() -> void:
			b.desk["cand"] = cid
			b.desk["cash"] = cash + CASH_STEP,
		cash <= 10, false)
	DeskKit.money_row(b, fr, "options · 4yr/1yr", "%.1f%% · %.1f%% left" % [opt, free - opt], DeskKit.INK,
		func() -> void:
			b.desk["cand"] = cid
			b.desk["opt"] = maxf(opt - OPT_STEP, 0.0),
		func() -> void:
			b.desk["cand"] = cid
			b.desk["opt"] = minf(opt + OPT_STEP, free),
		opt <= 0.0, opt + 0.0001 >= free)
	var odds := SimOwnership.acceptance_odds(state, cand, cash, opt)
	var d_cash := SimOwnership.acceptance_odds(state, cand, cash + 30, opt) - odds
	var d_opt := SimOwnership.acceptance_odds(state, cand, cash, opt + 0.2) - odds
	var mercenary := String(cand.get("profile", "")) == "mercenary"
	var ratio := float(cash) / maxf(float(ask), 1.0)
	var reads := "rich" if ratio >= 1.05 else ("fair" if ratio >= 0.97 else
		("fair, cash-light" if ratio >= 0.85 else "thin"))
	var total_txt := "≈%d%%" % int(round(odds))
	var foot_txt := "+$30 cash %+dpts · +0.2%% opt %+dpts" % [int(round(d_cash)), int(round(d_opt))]
	DeskKit.ticket(b, float(z3.content_x) + 510.0, float(z3.cursor) - 2.0, 380.0, {
		"title": "will %s say yes?" % String(cand.get("name", "?")).split(" ")[0],
		"lines": [
			{"label": "her ask — %s" % ("cash-leaning" if mercenary else "mission-leaning"),
				"value": "$%s · reads %s" % [SimOwnership.money(ask), reads]},
		],
		"total_label": "acceptance odds", "total_value": total_txt,
		"total_col": DeskKit.SAGE if odds >= 60.0 else DeskKit.PEN,
		"foot": foot_txt})
	# THE DIAL FEELS LIVE: on a stepper press the odds number ticks over
	# ~0.25s and the marginal foot re-inks — it computed already, now it moves
	_animate_odds(b, int(round(odds)), total_txt, foot_txt)
	if SimLabor.seats_left(state) <= 0:
		b.label("the house is full — no desk to offer", Vector2(float(z3.content_x),
			float(z3.cursor) + 146.0), DeskKit.DETAIL, Color(DeskKit.INK, 0.45), 360.0)
		return
	var lane_send := func() -> void:
		SimOwnership.op_send_offer(b.state, cid, cash, opt)
		b.desk.erase("cash")
		b.desk.erase("opt")
	actions.append({"label": "send the offer — %s" % String(cand.get("name", "?")).split(" ")[0],
		"tier": "two-tap", "cb": lane_send})

## The tick of the dial: tween the total label's int between the last shown
## odds and the new ones; wash the marginal foot back in from faint ink.
static func _animate_odds(b, target: int, total_txt: String, foot_txt: String) -> void:
	var prev := int(b.desk.get("odds_shown", target))
	b.desk["odds_shown"] = target
	if prev == target:
		return
	var lbl := _label_with(b, total_txt)
	if lbl != null:
		var tw: Tween = b.create_tween()
		tw.tween_method(func(v: float) -> void:
			if is_instance_valid(lbl):
				lbl.text = "≈%d%%" % int(round(v)), float(prev), float(target), 0.25)
	var foot := _label_with(b, foot_txt)
	if foot != null:
		foot.self_modulate = Color(1.0, 1.0, 1.0, 0.15)
		var tw2: Tween = b.create_tween()
		tw2.tween_property(foot, "self_modulate", Color(1.0, 1.0, 1.0, 1.0), 0.3)

## The newest label wearing exactly this text (the ticket drew it this frame).
static func _label_with(b, text: String) -> Label:
	var kids: Array = b.pane().get_children()
	for i in range(kids.size() - 1, -1, -1):
		if kids[i] is Label and (kids[i] as Label).text == text:
			return kids[i]
	return null

## The seat door — a DETAIL page listing what this era can hire.
static func _draw_seats_page(b, state: GameState) -> void:
	DeskKit.back(b, "back to recruitment", func() -> void:
		b.desk.erase("mode"))
	var y := 64.0
	y = DeskKit.hero_band(b, "open a seat",
		"the advert is the magnet, the ask is the contract — the band is the market's, not yours.",
		DeskKit.INK, y)
	if not SimLabor.market_open(state.era):
		DeskKit.empty(b, Vector2(DeskKit.X_ID + 20.0, y + 10.0),
			"nobody answers an advert taped to a garage door.",
			"the labor market opens at coworking — until then, hire the people you know", true)
		return
	for seat in ["engineer", "sales", "designer", "ops", "support", "manager"]:
		if not SimLabor.role_unlocked(seat, state.era):
			continue
		var band := SimOwnership.band_for(state, seat)
		var taken := not _role_open_for(state, seat).is_empty()
		var seat_v := String(seat)
		var press := Callable()
		if not taken:
			press = func() -> void:
				SimOwnership.open_seat(b.state, seat_v)
				b.desk.erase("mode")
		y = DeskKit.hero_row(b, y, {"name": seat, "facts": "band $%s–%s/wk · ≈$40/wk advert" % [
			SimOwnership.money(int(band.get("lo", 0))), SimOwnership.money(int(band.get("hi", 0)))],
			"value": "open" if not taken else "already open",
			"col": DeskKit.INK if not taken else Color(DeskKit.INK, 0.4),
			"on_press": press})
	DeskKit.footer(b, {"computed": "seats left this era: %d" % SimLabor.seats_left(state),
		"rules": "Esc goes back — an advert starts billing the week it opens",
		"y": 812.0, "rules_y": 846.0})

# ─────────────────────────── the page's own reads ────────────────────────────

static func _seat_word(state: GameState, cand: Dictionary) -> String:
	if state.recruitment.is_empty():
		return "?"
	for r in state.recruitment.get("roles", []):
		if String((r as Dictionary).get("id", "")) == String(cand.get("role_id", "")):
			return String((r as Dictionary).get("seat", "?"))
	return SimLabor.role_row(String(cand.get("role_id", "")).trim_prefix("role_").split("_")[0]) \
		if String(cand.get("role_id", "")) != "" else "the seat closed"

static func _offer_for(state: GameState, cand_id: String) -> Dictionary:
	for off in _offers(state):
		if String((off as Dictionary).get("candidate_id", "")) == cand_id:
			return off
	return {}

static func _role_open_for(state: GameState, seat: String) -> Dictionary:
	for r in _roles(state):
		if SimLabor.role_row(String((r as Dictionary).get("seat", ""))) == SimLabor.role_row(seat):
			return r
	return {}

static func _composer_target(b, state: GameState) -> Dictionary:
	var want := String(b.desk.get("cand", ""))
	if want != "":
		var cand := SimOwnership.cand_by_id(state, want)
		if not cand.is_empty() and String(cand.get("stage", "")) == "interviewed":
			return cand
	var pool := _candidates_in(state, ["interviewed"])
	return pool[0] if not pool.is_empty() else {}

static func handle(_b, _id: String) -> void:
	pass
