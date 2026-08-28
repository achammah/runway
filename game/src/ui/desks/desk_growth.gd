class_name DeskGrowth
extends RefCounted
## DESK — REVENUE · "growth" = THE MARKET GARDEN (DECISIONS: owner pick E, with
## the two generated layers).
## THE QUESTION THIS DESK ANSWERS: "where does next week's demand come from?"
##
## Four plots, one per channel, each with its own drawn instrument: the topics
## (names + one-liners) come from state.topics — LLM-generated once per run,
## dressing only; the engine's four characters are preserved verbatim in
## whatever world the DM invents. The garden set below is the shipped fallback
## (DECISIONS: "a fallback topic library keyed by audience ships … garden set =
## the default"), so a keyless run plays word for word.
##
## Illustration slots read the generation lane's cache (user://garden_<key>.png,
## the gen_scenes_v2 idiom); the drawn instruments are the instant placeholder
## and the permanent fallback. Engine numbers, verdicts and steppers NEVER wait
## on an image.
##
## Money law: separate −/+ per channel (never a joined chip); the era cap
## clamps the SUM and the meter under the hero shows it; word of mouth is the
## honest unbuyable line. Verdicts are WORDS COMPUTED FROM THE FUNNEL — the
## audience flips (enterprise ads "a drop in the ocean", consumer outbound
## "nobody answers a cold call") fall out of the data, never a hardcode.
##
## DAG3 (13-binder-ux · growth): a verdict chip is a DOOR — press it and the
## channel's own curve opens drawn (the saturating character, the knee ticked,
## your spend dotted), street math in receipt lines under it; a stepper press
## the CAP refuses pulses the meter coral (the ceiling made felt); [balance
## the mix — suggest] lays the SAME total even-marginal across the plots as
## ADOPT rows (spend-book pattern — nothing ever applies itself); the empty
## garden is the S1 teaching state.

const QUESTION := "where does next week's demand come from?"

## The ladder every channel lever walks (the ledger's own steps).
const LEVER_STEPS := [0, 250, 500, 1000, 2000, 4000, 8000]

## THE FALLBACK TOPIC LIBRARY — the garden set, the default dressing when the
## world has not written its own (state.topics.growth.<key>.{name,line}).
const DEFAULT_TOPICS := {
	"ads": {"name": "cut flowers", "line": "bloom now, wilt fast — paid reach runs only while fed"},
	"content": {"name": "the orchard", "line": "slow, then generous — compounds funded, rots starved"},
	"referrals": {"name": "the bees", "line": "multiply what's healthy — only a liked product has promoters"},
	"outbound": {"name": "the stall", "line": "staffed, certain, dear — quota knocking on doors"},
}

## The 2×2 plot grid.
const PLOT_W := 553.0
const PLOT_H := 206.0
const PLOT_Y := 224.0

# ─────────────────────────────── the dispatch ────────────────────────────────

static func hero_summary(state) -> Dictionary:
	var s: GameState = state
	var h := _hero_text(s)
	return {"big": String(h.get("big", "")), "line": String(h.get("line", ""))}

static func draw(b) -> void:
	var s: GameState = b.state
	var f0 := SimFunnel.funnel(s)
	if f0.is_empty() and SimFunnel.spend_total(s) <= 0.0:
		# S1 — the untouched garden is a TEACHING state: the four characters
		# said once, and the first $250 one press away (a stepper is free)
		DeskKit.zero_state(b, {
			"will_show": "where next week's demand comes from",
			"would_line": "four plots, four characters — ads pour while fed, content "
				+ "compounds, referrals multiply a liked product, outbound knocks on doors",
			"action_label": "put the first $250 into ads",
			"action_cb": func() -> void: s.budgets["ads"] = 250,
			"wakes_hint": "verdicts and CAC arrive with the first locked week — "
				+ "the era caps what the whole mix may spend",
		})
		return
	var mode := String(b.desk.get("mode", ""))
	_garden(b, s, f0, mode == "suggest")
	if mode.begins_with("curve:"):
		# S4 — the verdict opened: the garden stays under the paper card
		_curve_card(b, s, mode.substr(6))

## The whole garden sheet — hero, cap meter, four plots, wom, foot. In suggest
## mode the yield lines give way to the even-marginal ADOPT rows.
static func _garden(b, s: GameState, f0: Dictionary, suggesting: bool) -> void:
	var h := _hero_text(s)
	b.label(String(h.get("big", "")), Vector2(DeskKit.X_ID, 6.0), DeskKit.HERO_BIG, DeskKit.INK, 760.0)
	b.label(String(h.get("line", "")), Vector2(DeskKit.X_ID, 74.0), DeskKit.ROW,
		Color(DeskKit.INK, 0.7), 740.0)
	# S5 — the hero against the last open: what the mix bought
	if not f0.is_empty():
		var bought := int(round(SimFunnel.num(f0, "signed_ads") + SimFunnel.num(f0, "signed_content")
			+ SimFunnel.num(f0, "signed_referrals") + SimFunnel.num(f0, "signed_outbound")))
		var gp: String = b.seen_prev("growth", "hero")
		if b.seen("growth", "hero", str(bought)) and gp.is_valid_int():
			var hbw: float = b.font().get_string_size(String(h.get("big", "")),
				HORIZONTAL_ALIGNMENT_LEFT, -1, DeskKit.HERO_BIG).x
			DeskKit.delta_arrow(b, DeskKit.X_ID + hbw + 10.0, 26.0, float(bought), float(gp.to_int()))
	# the era's allowance, said at the top right where the mix is set
	var cap := SimEngine.era_spend_cap(s.era)
	var cl: Label = b.label("the %s era allows $%s/wk" % [s.era, b.fmt(cap)],
		Vector2(790.0, 12.0), 24, DeskKit.INK, 340.0)
	cl.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	var tm := SimFunnel.team_mult(s)
	var heads := SimFunnel.mk_heads(s)
	var tl: Label = b.label(("%d marketing head%s sharpen%s it all ×%.2f" % [heads,
		"" if heads == 1 else "s", "s" if heads == 1 else "", tm]) if heads > 0
		else "a marketing head would sharpen it all ×1.12",
		Vector2(730.0, 48.0), 17, Color(DeskKit.INK, 0.5), 400.0)
	tl.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	DeskKit.pen_rule(b, 128.0)
	# THE SUM, against the era's cap — the clamp made visible
	var total := SimFunnel.spend_total(s)
	DeskKit.meter(b, DeskKit.X_ID, 152.0, 560.0, total / maxf(float(cap), 1.0), DeskKit.SAGE,
		"$%s of the $%s the era allows" % [b.fmt(int(total)), b.fmt(cap)])
	# the refused press made FELT: a coral pulse breathes once over the meter
	if bool(b.desk.get("cap_pulse", false)):
		b.desk.erase("cap_pulse")
		_cap_pulse(b, Rect2(DeskKit.X_ID - 4.0, 146.0, 568.0, 30.0))
	var split := _even_split(s) if suggesting else {}
	if suggesting and _split_differs(s, split):
		# the whole-mix adopt (spend-book pattern): one arm prices all four
		var t := 0
		for k0 in SimFunnel.MIX:
			t += int(split.get(String(k0), 0))
		var fire_all := func() -> void:
			for k2 in SimFunnel.MIX:
				s.budgets[String(k2)] = int(split.get(String(k2), 0))
		DeskKit.arm(b, "adopt_mix_all", "adopt the whole split — $%s/wk" % b.fmt(t),
			"set all four plots — sure?", Vector2(620.0, 180.0), fire_all, 330.0, 17)
	var y := PLOT_Y
	var keys := SimFunnel.MIX
	for i in keys.size():
		var px := 10.0 + float(i % 2) * (PLOT_W + 14.0)
		var py := y + float(i / 2) * (PLOT_H + 14.0)
		_plot(b, s, String(keys[i]), px, py, split)
	# WORD OF MOUTH — the honest unbuyable row
	var womtxt := "word of mouth: ≈%d joined free this week — not for sale, earned" \
		% int(round(SimFunnel.num(f0, "wom"))) if not f0.is_empty() \
		else "word of mouth: not for sale — it arrives when joiners bring friends"
	b.label(womtxt, Vector2(DeskKit.X_ID, y + 2.0 * PLOT_H + 34.0), DeskKit.DETAIL,
		Color(DeskKit.INK, 0.6), 1100.0)
	# S3 — the one primary act: the even-marginal walk (ADOPT-only, S15)
	if suggesting:
		var hint: Label = b.label("nothing applies itself — adopt per plot, Esc keeps your mix",
			Vector2(560.0, DeskKit.DO_LANE_Y + 10.0), 17, Color(DeskKit.INK, 0.55), 570.0)
		hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	elif _split_differs(s, _even_split(s)):
		DeskKit.do_lane(b, [{"label": "balance the mix — suggest", "tier": "",
			"cb": func() -> void: b.desk["mode"] = "suggest"}])
	_foot(b, s)

static func handle(_b, _id: String) -> void:
	pass

# ─────────────────────────────── the hero ────────────────────────────────────

## Money wears its commas everywhere on the desk (the accounting rules law).
static func _fmt(n: int) -> String:
	var s := str(absi(n))
	var out := ""
	while s.length() > 3:
		out = "," + s.substr(s.length() - 3) + out
		s = s.substr(0, s.length() - 3)
	return ("-" if n < 0 else "") + s + out

static func _hero_text(s: GameState) -> Dictionary:
	var total := int(SimFunnel.spend_total(s))
	var f := SimFunnel.funnel(s)
	if f.is_empty():
		return {"big": "$%s/wk into the garden" % _fmt(total),
			"line": "the first locked week measures what a dollar buys in each channel"}
	var bought := SimFunnel.num(f, "signed_ads") + SimFunnel.num(f, "signed_content") \
		+ SimFunnel.num(f, "signed_referrals") + SimFunnel.num(f, "signed_outbound")
	var cac := int(SimFunnel.num(f, "blended_cac"))
	var wom := int(round(SimFunnel.num(f, "wom")))
	return {"big": "$%s/wk buys ≈%d customers" % [_fmt(total), int(round(bought))],
		"line": "%s, and word of mouth adds ≈%d more for free" % [
			("CAC $%s" % _fmt(cac)) if cac > 0 else "CAC not yet knowable", wom]}

# ─────────────────────────────── one plot ────────────────────────────────────

static func _plot(b, s: GameState, key: String, x: float, y: float,
		split: Dictionary = {}) -> void:
	var topic := _topic(s, key)
	var frame := DeskKit.card_frame(b, x, y, PLOT_W, PLOT_H,
		"%s — %s" % [key, String(topic.get("name", ""))])
	var cx := float(frame.get("content_x", x))
	var cy := float(frame.get("content_y", y))
	# S2b — the plot is a named landing: cross-desk jumps spotlight it whole
	b.mark_control("plot_" + key, Rect2(x, y, PLOT_W, PLOT_H))
	# S4 — the verdict is a DOOR: press → the channel's curve, drawn
	var vd := _verdict(s, key)
	var vbtn := DeskKit.word(b, String(vd.get("word", "")), Vector2(x + PLOT_W - 250.0, y + 10.0),
		func() -> void: b.desk["mode"] = "curve:" + key, 20, vd.get("col", DeskKit.INK), 232.0)
	vbtn.size = Vector2(232.0, 40.0)
	vbtn.alignment = HORIZONTAL_ALIGNMENT_RIGHT
	# the illustration slot: the cache when the painter has been, the drawn
	# instrument always underneath (numbers never wait on an image)
	_plot_art(b, key, cx, cy)
	var tx := cx + 140.0
	var tw := PLOT_W - DeskKit.CARD_PAD * 2.0 - 140.0
	b.label(String(topic.get("line", "")), Vector2(tx, cy - 2.0), 16,
		Color(DeskKit.INK, 0.55), tw)
	# the spend row: the amount in the pen, the two SEPARATE squares beside it
	var cur := int(s.budgets.get(key, 0))
	var shown_cur := cur + (int(s.marketing_budget) + int(s.budgets.get("marketing", 0))
		if key == "ads" else 0)
	b.label("$%s/wk" % b.fmt(shown_cur), Vector2(tx, cy + 40.0), 27, DeskKit.PEN, 180.0)
	var down := _step_to(s, key, cur, -1)
	var up := _step_to(s, key, cur, 1)
	# the CAP's refusal stays a live press — it answers by pulsing the meter
	# (the era ceiling made FELT); only the ladder's own top goes dead
	var ladder_top := cur >= int(LEVER_STEPS[LEVER_STEPS.size() - 1])
	var capped := up == cur and not ladder_top
	DeskKit.adjust_pair(b, tx + 196.0, cy + 46.0,
		func() -> void: s.budgets[key] = down,
		(func() -> void: b.desk["cap_pulse"] = true) if capped
			else func() -> void: s.budgets[key] = up,
		down == cur, up == cur and not capped)
	b.mark_control("mix_" + key, Rect2(tx + 192.0, cy + 42.0, 100.0, 44.0))
	# the yield line — or, in suggest mode, the even-marginal ADOPT row
	if split.is_empty():
		b.label(_yield_line(s, key, up == cur and cur > 0), Vector2(tx, cy + 78.0), 16,
			Color(DeskKit.INK, 0.7), tw)
	else:
		var sug := int(split.get(key, cur))
		if sug == cur:
			b.label("this plot already sits even", Vector2(tx, cy + 78.0), 16,
				Color(DeskKit.INK, 0.55), tw)
		else:
			var fire_adopt := func() -> void: s.budgets[key] = sug
			DeskKit.arm(b, "adopt_mix_" + key, "suggested $%s — adopt" % b.fmt(sug),
				"set $%s/wk — sure?" % b.fmt(sug), Vector2(tx, cy + 74.0),
				fire_adopt, 300.0, 17)

## The topic for one plot: the world's own words, or the garden set.
static func _topic(s: GameState, key: String) -> Dictionary:
	var growth: Variant = s.topics.get("growth", {})
	if growth is Dictionary:
		var t: Variant = (growth as Dictionary).get(key, {})
		if t is Dictionary and String((t as Dictionary).get("name", "")) != "":
			return t as Dictionary
	var d: Dictionary = DEFAULT_TOPICS.get(key, {"name": key, "line": ""})
	return d

## THE STEP a press would land on: the ledger's ladder, era-capped, and a step
## UP that would push the whole mix past the era's ceiling is refused (the sum
## is what the engine clamps — clamping per lever would let four channels
## quadruple the allowance).
static func _step_to(s: GameState, key: String, cur: int, dir: int) -> int:
	var cap := SimEngine.era_spend_cap(s.era)
	var idx := 0
	for i in LEVER_STEPS.size():
		if int(LEVER_STEPS[i]) <= cur:
			idx = i
	idx = clampi(idx + dir, 0, LEVER_STEPS.size() - 1)
	var want := mini(int(LEVER_STEPS[idx]), cap)
	if dir <= 0:
		return want
	var others := 0
	for k in SimFunnel.MIX:
		if String(k) != key:
			others += int(s.budgets.get(String(k), 0))
	if others + want > cap:
		return cur
	return want

## WHAT THIS MONEY IS DOING RIGHT NOW — the funnel lane's own words, with the
## ceiling's reason when the mix is pinned.
static func _yield_line(s: GameState, key: String, pinned: bool) -> String:
	var live := SimFunnel.lever_effect(s, key)
	var f := SimFunnel.funnel(s)
	var cac := SimFunnel.num(f, "cac_" + key)
	if cac > 0.0:
		live += " · CAC $%s" % _fmt(int(round(cac)))
	if pinned:
		live += " · the mix is at the era's ceiling"
	return live

## THE VERDICT WORDS — computed from the funnel's own parameters, so the same
## page flips by audience: enterprise ads read "a drop in the ocean" because
## the reach IS a drop; consumer outbound reads "nobody answers a cold call"
## because almost nobody does. Words from data, never hardcoded per audience.
static func _verdict(s: GameState, key: String) -> Dictionary:
	var f := SimFunnel.funnel(s)
	var spend := SimFunnel.spend_of(s, key)
	match key:
		"ads":
			if spend <= 0.0:
				return {"word": "unfunded", "col": Color(DeskKit.INK, 0.45)}
			if SimFunnel.reach_ads(s) < 3.0:
				return {"word": "a drop in the ocean", "col": DeskKit.PEN}
			var sat := SimFunnel.ads_sat(s)
			if spend >= 1.2 * sat:
				return {"word": "past the knee", "col": DeskKit.PEN}
			if spend >= 0.6 * sat:
				return {"word": "near the knee", "col": DeskKit.YELL}
			if _cheapest(f) == "ads":
				return {"word": "the engine", "col": DeskKit.SAGE}
			return {"word": "pouring", "col": Color(DeskKit.INK, 0.8)}
		"content":
			var eq := s.content_equity
			if spend <= 0.0:
				if eq >= 0.05:
					return {"word": "rotting −7%/wk", "col": DeskKit.PEN}
				return {"word": "nothing written", "col": Color(DeskKit.INK, 0.45)}
			if SimFunnel.content_target(s) > eq + 0.02:
				return {"word": "compounding", "col": DeskKit.SAGE}
			if eq >= 0.6:
				return {"word": "the well is deep", "col": DeskKit.SAGE}
			return {"word": "filling", "col": Color(DeskKit.INK, 0.8)}
		"referrals":
			if SimFunnel.happy(s) < SimFunnel.HAPPY_FLOOR:
				return {"word": "gate closed (v0.%d)" % s.product,
					"col": DeskKit.PEN if spend > 0.0 else Color(DeskKit.INK, 0.45)}
			if spend <= 0.0:
				return {"word": "gate open, unfunded", "col": Color(DeskKit.INK, 0.8)}
			return {"word": "gate open ×%.1f" % (1.0 + SimFunnel.ref_gain(s)), "col": DeskKit.SAGE}
		"outbound":
			var aud := float(SimFunnel.channel(s).get("ob_aud", 1.0))
			if aud < 0.5:
				return {"word": "nobody answers a cold call", "col": Color(DeskKit.INK, 0.45)}
			if spend <= 0.0:
				return {"word": "no lists worked", "col": Color(DeskKit.INK, 0.45)}
			if _cheapest(f) == "outbound":
				return {"word": "the engine", "col": DeskKit.SAGE}
			var cac := SimFunnel.num(f, "cac_outbound")
			var blended := SimFunnel.num(f, "blended_cac")
			if cac > 0.0 and blended > 0.0 and cac > 1.5 * blended:
				return {"word": "sure but dear", "col": DeskKit.YELL}
			return {"word": "knocking", "col": Color(DeskKit.INK, 0.8)}
	return {"word": "", "col": DeskKit.INK}

## The channel that bought customers cheapest last week, or "".
static func _cheapest(f: Dictionary) -> String:
	var best := ""
	var best_cac := 0.0
	for k in SimFunnel.MIX:
		var c := SimFunnel.num(f, "cac_" + k)
		if c > 0.0 and (best == "" or c < best_cac):
			best = k
			best_cac = c
	return best

# ─────────────── the even-marginal split (S3/S15, ADOPT-only) ────────────────

## THE EVEN-MARGINAL SPLIT — the classic lesson on the engine's own curves:
## re-lay the SAME total across the plots so the next dollar buys about the
## same everywhere. Greedy over the ladder: each rung goes to the channel
## whose next step buys the most reach-equivalent, so the walk stops exactly
## where marginals even out on the grid. Gates zero a closed channel (the
## happy floor, the cold-call audience); the era cap bounds the total; the
## characters are their curves — ads/content/referrals saturate at their own
## knee, outbound is the straight line. {} = nothing to lay out yet.
static func _even_split(s: GameState) -> Dictionary:
	var f := SimFunnel.funnel(s)
	if f.is_empty():
		return {}
	var cap := SimEngine.era_spend_cap(s.era)
	var budget := mini(int(SimFunnel.spend_total(s)), cap)
	if budget < int(LEVER_STEPS[1]):
		return {}
	var ch := SimFunnel.channel(s)
	var tm := SimFunnel.team_mult(s)
	var ee := SimFunnel.era_eff(s)
	# reach-equivalent ceilings, in the funnel's own terms: referrals' extra
	# joiners convert through the measured reach-per-add so the units compare
	var adds := maxf(SimFunnel.num(f, "adds"), 0.5)
	var reach_per_add := maxf(SimFunnel.num(f, "reach_total") / adds, 1.0)
	var happy := SimFunnel.happy(s)
	var wom_base := SimFunnel.num(f, "wom") / maxf(1.0 + SimFunnel.ref_gain(s), 1.0)
	var ceils := {
		"ads": float(ch.get("ads_a", 0.0)) * ee * tm,
		"content": float(ch.get("con_a", 0.0)) * ee * tm,
		"referrals": (float(ch.get("ref_a", 0.0)) * happy * tm * wom_base * reach_per_add)
			if happy >= SimFunnel.HAPPY_FLOOR else 0.0,
		"outbound": 0.0,
	}
	var sats := {
		"ads": SimFunnel.ads_sat(s),
		"content": maxf(float(ch.get("con_sat", 1600.0)), 1.0),
		"referrals": maxf(float(ch.get("ref_sat", 1200.0)), 1.0),
		"outbound": 1.0,
	}
	var aud := float(ch.get("ob_aud", 1.0))
	var ob_marginal := (SimFunnel.OB_REACH_PER_K / 1000.0 * aud) if aud >= 0.5 else 0.0
	var alloc := {"ads": 0, "content": 0, "referrals": 0, "outbound": 0}
	var spent := 0
	while true:
		var best := ""
		var best_m := 0.0
		for k in SimFunnel.MIX:
			var key := String(k)
			var cur := int(alloc[key])
			var ni := _ladder_idx(cur) + 1
			if ni >= LEVER_STEPS.size():
				continue
			var nxt := mini(int(LEVER_STEPS[ni]), cap)
			if nxt <= cur or spent - cur + nxt > budget:
				continue
			var gain := 0.0
			if key == "outbound":
				gain = ob_marginal * float(nxt - cur)
			else:
				var sat := float(sats[key])
				gain = float(ceils[key]) * (exp(-float(cur) / sat) - exp(-float(nxt) / sat))
			var m := gain / float(nxt - cur)
			if m > best_m + 0.000001:
				best_m = m
				best = key
		if best == "" or best_m <= 0.0:
			break
		var bn := mini(int(LEVER_STEPS[_ladder_idx(int(alloc[best])) + 1]), cap)
		spent += bn - int(alloc[best])
		alloc[best] = bn
	if spent <= 0:
		return {}
	return alloc

## The rung at or under a value (off-ladder values land on the rung below).
static func _ladder_idx(cur: int) -> int:
	var idx := 0
	for i in LEVER_STEPS.size():
		if int(LEVER_STEPS[i]) <= cur:
			idx = i
	return idx

## Whether the suggestion would move anything — {} never does.
static func _split_differs(s: GameState, split: Dictionary) -> bool:
	if split.is_empty():
		return false
	for k in SimFunnel.MIX:
		if int(split.get(String(k), 0)) != int(s.budgets.get(String(k), 0)):
			return true
	return false

# ───────────────── S4 · the curve, drawn (press a verdict) ───────────────────

## THE VERDICT, OPENED: the channel's own curve in the desk's hand — the
## saturating character (or outbound's straight line), the knee ticked, your
## spend dotted onto it — street math in receipt lines below. Any press or
## Esc closes the read before anything else (the desk-mode chain).
static func _curve_card(b, s: GameState, key: String) -> void:
	if not SimFunnel.MIX.has(key):
		b.desk["mode"] = ""
		return
	var catcher := DeskKit.word(b, "", Vector2(0.0, 0.0), func() -> void:
		b.desk["mode"] = "", DeskKit.DETAIL, DeskKit.INK, 1140.0)
	catcher.size = Vector2(1140.0, 880.0)
	var spend := SimFunnel.spend_of(s, key)
	var lines := _curve_lines(b, s, key, spend)
	var card_h := 56.0 + 206.0 + float(lines.size()) * 30.0 + 18.0
	var title := ("%s — the curve" % key) if key != "outbound" \
		else "outbound — the straight line"
	var frame := DeskKit.card_frame(b, 250.0, 150.0, 640.0, card_h, title)
	var cx := float(frame.get("content_x", 268.0))
	var cy := float(frame.get("content_y", 206.0))
	var sat := 1.0
	var linear := key == "outbound"
	match key:
		"ads":
			sat = SimFunnel.ads_sat(s)
		"content":
			sat = maxf(float(SimFunnel.channel(s).get("con_sat", 1600.0)), 1.0)
		"referrals":
			sat = maxf(float(SimFunnel.channel(s).get("ref_sat", 1200.0)), 1.0)
	var art := _CurveArt.new()
	art.font = b.font()
	art.linear = linear
	art.dim = key == "referrals" and SimFunnel.happy(s) < SimFunnel.HAPPY_FLOOR
	art.sat = sat
	art.xmax = maxf(2.4 * sat, spend * 1.15 + 250.0) if not linear \
		else maxf(spend * 2.0, 2000.0)
	art.spend = spend
	art.mouse_filter = Control.MOUSE_FILTER_IGNORE
	art.position = Vector2(cx, cy)
	art.set_deferred("size", Vector2(640.0 - DeskKit.CARD_PAD * 2.0, 190.0))
	b.pane().add_child(art)
	var money_x := float(frame.get("money_x", 872.0))
	var ly := cy + 206.0
	for ln in lines:
		var ld: Dictionary = ln
		DeskKit.fit_line(b, String(ld.get("label", "")), Vector2(cx, ly), 19,
			Color(DeskKit.INK, 0.85), 340.0)
		var v: Label = DeskKit.fit_line(b, String(ld.get("value", "")),
			Vector2(cx + 350.0, ly), 19, ld.get("col", DeskKit.INK), money_x - cx - 350.0)
		v.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
		ly += 30.0

## The receipt lines under the drawing — the funnel lane's own numbers only.
static func _curve_lines(b, s: GameState, key: String, spend: float) -> Array:
	var f := SimFunnel.funnel(s)
	var vd := _verdict(s, key)
	var lines: Array = [
		{"label": "the verdict", "value": String(vd.get("word", "")),
			"col": vd.get("col", DeskKit.INK)},
		{"label": "spend now", "value": "$%s/wk" % b.fmt(int(spend))},
	]
	if not f.is_empty():
		lines.append({"label": "bought last week", "value": "≈%.1f customers"
			% SimFunnel.num(f, "signed_" + key)})
		var cac := SimFunnel.num(f, "cac_" + key)
		lines.append({"label": "CAC last week", "value": ("$%s" % b.fmt(int(round(cac))))
			if cac > 0.0 else "not yet knowable"})
	match key:
		"ads":
			lines.append({"label": "the knee sits at", "value": "$%s/wk"
				% b.fmt(int(round(SimFunnel.ads_sat(s))))})
		"content":
			lines.append({"label": "this spend funds level", "value": "%d%%"
				% int(round(SimFunnel.content_target(s) * 100.0))})
			lines.append({"label": "equity today", "value": "%d%%"
				% int(round(s.content_equity * 100.0))})
		"referrals":
			if SimFunnel.happy(s) < SimFunnel.HAPPY_FLOOR:
				lines.append({"label": "the gate", "value": "closed (v0.%d)" % s.product,
					"col": DeskKit.PEN})
			else:
				lines.append({"label": "word of mouth ×", "value": "%.2f"
					% (1.0 + SimFunnel.ref_gain(s))})
		"outbound":
			lines.append({"label": "reach per $1k", "value": "≈%d" % int(round(
				SimFunnel.OB_REACH_PER_K * float(SimFunnel.channel(s).get("ob_aud", 1.0))))})
			lines.append({"label": "closing bought", "value": "+%.1f"
				% SimFunnel.ob_closers(s)})
	return lines

# ─────────────────────── S8/S15 · the desk's own voice ───────────────────────

## The garden is live from the garage; the S1 state covers week one.
static func is_dormant(_state) -> bool:
	return false

## The rail's four-character read: what the week's mix costs.
static func micro_status(state) -> String:
	var s: GameState = state
	var total := int(SimFunnel.spend_total(s))
	if total <= 0:
		return ""
	if total < 1000:
		return "$%d/wk" % total
	return "$%.1fk/wk" % (float(total) / 1000.0)

## S15 — the desk speaks up: the even-marginal walk, as a jump chip.
static func suggestions(state) -> Array:
	var s: GameState = state
	if not _split_differs(s, _even_split(s)):
		return []
	return [{"label": "balance the mix — growth", "kind": "jump",
		"payload": {"control": "do_0"}}]

## The refusal made FELT: a coral wash breathes once over the cap meter and
## dies (~0.45s, self-freeing — spawned on the draw after the refused press).
static func _cap_pulse(b, rect: Rect2) -> void:
	var p := _Pulse.new()
	p.mouse_filter = Control.MOUSE_FILTER_IGNORE
	p.position = rect.position
	p.set_deferred("size", rect.size)
	b.pane().add_child(p)
	var tw: Tween = b.create_tween()
	tw.tween_method(func(a: float) -> void:
		if is_instance_valid(p):
			p.alpha = a
			p.queue_redraw(), 0.9, 0.0, 0.45)
	tw.tween_callback(func() -> void:
		if is_instance_valid(p):
			p.queue_free())

# ─────────────────────────────── the foot ────────────────────────────────────

static func _foot(b, s: GameState) -> void:
	var f := SimFunnel.funnel(s)
	var computed := ""
	if not f.is_empty():
		var bought := SimFunnel.num(f, "signed_ads") + SimFunnel.num(f, "signed_content") \
			+ SimFunnel.num(f, "signed_referrals") + SimFunnel.num(f, "signed_outbound")
		var cac := int(SimFunnel.num(f, "blended_cac"))
		computed = "last week: bought ≈%d + free ≈%d = %d joined · blended CAC %s" % [
			int(round(bought)), int(round(SimFunnel.num(f, "wom") + SimFunnel.num(f, "organic"))),
			int(round(SimFunnel.num(f, "adds"))),
			("$%s" % b.fmt(cac)) if cac > 0 else "not yet knowable"]
	DeskKit.footer(b, {
		"computed": computed,
		"rules": "four levers, four characters — ads pour, content compounds, referrals multiply, outbound knocks",
		"y": 806.0, "rules_y": 840.0,
	})

# ───────────────────────── the illustration slot ─────────────────────────────

## The painter's cache when it exists; the drawn instrument always. The image
## sits OVER the instrument, so a half-written cache never leaves a hole.
static func _plot_art(b, key: String, x: float, y: float) -> void:
	var art := _PlotArt.new()
	art.kind = key
	art.mouse_filter = Control.MOUSE_FILTER_IGNORE
	art.position = Vector2(x, y)
	art.set_deferred("size", Vector2(116.0, 92.0))
	b.pane().add_child(art)
	# the generation lane caches per run+pivot (regenerates at a pivot)
	var path := "user://gen_illustrations/%d_p%d/plot_%s.png" % [b.state.sim_seed, b.state.pivots, key]
	if FileAccess.file_exists(path):
		var img := Image.new()
		if img.load(path) == OK:
			var tr := TextureRect.new()
			tr.texture = ImageTexture.create_from_image(img)
			tr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
			tr.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
			tr.mouse_filter = Control.MOUSE_FILTER_IGNORE
			tr.position = Vector2(x, y)
			tr.set_deferred("size", Vector2(116.0, 92.0))
			b.pane().add_child(tr)

## THE DRAWN INSTRUMENTS — the garden's four characters in the game's hand:
## cut flowers (ads), the orchard (content), the bees (referrals), the stall
## (outbound). Palette only, wobbled ink, no gradients.
class _PlotArt:
	extends Control
	var kind := "ads"
	func _draw() -> void:
		var w := size.x
		var h := size.y
		var rng := RandomNumberGenerator.new()
		rng.seed = 73
		match kind:
			"ads":
				# cut flowers — stems up, coral blooms, one wilting
				for i in 3:
					var sx := w * (0.22 + 0.28 * float(i))
					var top := h * (0.30 + 0.08 * float(i % 2))
					draw_line(Vector2(sx, h * 0.92), Vector2(sx, top), DeskKit.INK, 2.6)
					draw_circle(Vector2(sx, top - 7.0), 8.0,
						Color(DeskKit.PEN, 0.5 if i == 2 else 0.85))
					draw_arc(Vector2(sx, top - 7.0), 8.0, 0.0, TAU, 12, DeskKit.INK, 2.2)
			"content":
				# the orchard — one trunk, a sage canopy of three
				var bx := w * 0.5
				draw_line(Vector2(bx, h * 0.92), Vector2(bx, h * 0.38), DeskKit.INK, 3.0)
				draw_line(Vector2(bx, h * 0.60), Vector2(bx - 16.0, h * 0.46), DeskKit.INK, 2.4)
				draw_line(Vector2(bx, h * 0.54), Vector2(bx + 18.0, h * 0.40), DeskKit.INK, 2.4)
				for c in [Vector2(bx, h * 0.26), Vector2(bx - 22.0, h * 0.36), Vector2(bx + 24.0, h * 0.34)]:
					draw_circle(c, 13.0, Color(DeskKit.SAGE, 0.85))
					draw_arc(c, 13.0, 0.0, TAU, 14, DeskKit.INK, 2.2)
			"referrals":
				# the bees — three yellow bodies, dashed flight
				for pv in [Vector2(w * 0.32, h * 0.42), Vector2(w * 0.64, h * 0.26), Vector2(w * 0.72, h * 0.62)]:
					draw_set_transform(pv, 0.35, Vector2.ONE)
					draw_rect(Rect2(-9, -6, 18, 12), Color(DeskKit.YELL, 0.9))
					draw_rect(Rect2(-9, -6, 18, 12), DeskKit.INK, false, 2.2)
					draw_line(Vector2(-3, -6), Vector2(-3, 6), DeskKit.INK, 1.6)
					draw_set_transform(Vector2.ZERO, 0.0, Vector2.ONE)
				for d in 5:
					draw_line(Vector2(w * 0.16 + float(d) * 9.0, h * 0.78 - float(d) * 4.0),
						Vector2(w * 0.16 + float(d) * 9.0 + 5.0, h * 0.78 - float(d) * 4.0 - 2.0),
						Color(DeskKit.INK, 0.45), 1.8)
			"outbound":
				# the stall — kraft box, kraft2 roof, a doorway
				draw_rect(Rect2(w * 0.2, h * 0.36, w * 0.6, h * 0.5), Color(DeskKit.KRAFT, 1.0))
				draw_rect(Rect2(w * 0.2, h * 0.36, w * 0.6, h * 0.5), DeskKit.INK, false, 2.6)
				var roof := PackedVector2Array([Vector2(w * 0.16, h * 0.36),
					Vector2(w * 0.5, h * 0.12), Vector2(w * 0.84, h * 0.36)])
				draw_colored_polygon(roof, DeskKit.KRAFT2)
				var rp := roof.duplicate()
				rp.append(roof[0])
				draw_polyline(rp, DeskKit.INK, 2.6, true)
				draw_rect(Rect2(w * 0.44, h * 0.58, w * 0.14, h * 0.28), DeskKit.INK, false, 2.4)

## THE CURVE — the channel's character in the desk's own hand: the saturating
## sweep (or outbound's straight line), the knee ticked, your spend dotted
## down onto its bead on the curve.
class _CurveArt:
	extends Control
	var font: Font
	var linear := false
	var dim := false
	var sat := 1000.0
	var xmax := 2400.0
	var spend := 0.0
	func _f(x: float) -> float:
		if linear:
			return x / maxf(xmax, 1.0)
		return 1.0 - exp(-x / maxf(sat, 1.0))
	func _draw() -> void:
		var w := size.x
		var h := size.y
		var ax := h - 30.0
		var top := 10.0
		var span := ax - top
		var ymax := maxf(_f(xmax), 0.001)
		var ink_c := Color(DeskKit.INK, 0.35 if dim else 1.0)
		draw_line(Vector2(0, ax), Vector2(w, ax), DeskKit.INK, 2.2)
		draw_line(Vector2(0, ax), Vector2(0, top - 4.0), Color(DeskKit.INK, 0.5), 1.6)
		var pts := PackedVector2Array()
		for k in 33:
			var fx := xmax * float(k) / 32.0
			pts.append(Vector2(w * fx / xmax, ax - span * (_f(fx) / ymax)))
		draw_polyline(pts, ink_c, 3.0, true)
		if not linear and sat < xmax:
			var kx := w * sat / xmax
			var yy := top
			while yy < ax:
				draw_line(Vector2(kx, yy), Vector2(kx, minf(yy + 5.0, ax)),
					Color(DeskKit.INK, 0.35), 1.6)
				yy += 10.0
			if font != null:
				draw_string(font, Vector2(kx - 26.0, ax + 22.0), "the knee",
					HORIZONTAL_ALIGNMENT_LEFT, -1, 14, Color(DeskKit.INK, 0.55))
		var px := clampf(w * spend / maxf(xmax, 1.0), 0.0, w)
		var py := ax - span * (_f(spend) / ymax)
		var y2 := top
		while y2 < ax - 4.0:
			draw_line(Vector2(px, y2), Vector2(px, minf(y2 + 6.0, ax - 4.0)), DeskKit.PEN, 2.2)
			y2 += 11.0
		draw_circle(Vector2(px, py), 6.0, DeskKit.PEN)
		draw_arc(Vector2(px, py), 6.0, 0.0, TAU, 12, DeskKit.INK, 2.0)
		if font != null:
			draw_string(font, Vector2(clampf(px - 40.0, 0.0, w - 110.0), top + 10.0),
				"you: $%d" % int(round(spend)), HORIZONTAL_ALIGNMENT_LEFT, -1, 14, DeskKit.PEN)
			if dim:
				draw_string(font, Vector2(w * 0.32, ax - span * 0.5), "the gate is closed",
					HORIZONTAL_ALIGNMENT_LEFT, -1, 16, Color(DeskKit.INK, 0.5))

## The pulse the era's ceiling answers with — a coral wash that breathes once.
class _Pulse:
	extends Control
	var alpha := 0.9
	func _draw() -> void:
		draw_rect(Rect2(Vector2.ZERO, size), Color(DeskKit.PEN, alpha * 0.45))
		draw_rect(Rect2(Vector2.ZERO, size), Color(DeskKit.PEN, alpha), false, 3.0)
