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
	var h := _hero_text(s)
	b.label(String(h.get("big", "")), Vector2(DeskKit.X_ID, 6.0), DeskKit.HERO_BIG, DeskKit.INK, 760.0)
	b.label(String(h.get("line", "")), Vector2(DeskKit.X_ID, 74.0), DeskKit.ROW,
		Color(DeskKit.INK, 0.7), 740.0)
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
	var y := PLOT_Y
	var keys := SimFunnel.MIX
	for i in keys.size():
		var px := 10.0 + float(i % 2) * (PLOT_W + 14.0)
		var py := y + float(i / 2) * (PLOT_H + 14.0)
		_plot(b, s, String(keys[i]), px, py)
	# WORD OF MOUTH — the honest unbuyable row
	var f := SimFunnel.funnel(s)
	var womtxt := "word of mouth: ≈%d joined free this week — not for sale, earned" \
		% int(round(SimFunnel.num(f, "wom"))) if not f.is_empty() \
		else "word of mouth: not for sale — it arrives when joiners bring friends"
	b.label(womtxt, Vector2(DeskKit.X_ID, y + 2.0 * PLOT_H + 34.0), DeskKit.DETAIL,
		Color(DeskKit.INK, 0.6), 1100.0)
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

static func _plot(b, s: GameState, key: String, x: float, y: float) -> void:
	var topic := _topic(s, key)
	var frame := DeskKit.card_frame(b, x, y, PLOT_W, PLOT_H,
		"%s — %s" % [key, String(topic.get("name", ""))])
	var cx := float(frame.get("content_x", x))
	var cy := float(frame.get("content_y", y))
	# the verdict — one computed word, colored, on the title row
	var vd := _verdict(s, key)
	var vl: Label = b.label(String(vd.get("word", "")), Vector2(x + PLOT_W - 250.0, y + 14.0),
		20, vd.get("col", DeskKit.INK), 232.0)
	vl.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
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
	DeskKit.adjust_pair(b, tx + 196.0, cy + 46.0,
		func() -> void: s.budgets[key] = down,
		func() -> void: s.budgets[key] = up,
		down == cur, up == cur)
	# the yield line — the engine's live formula at the point of decision
	b.label(_yield_line(s, key, up == cur and cur > 0), Vector2(tx, cy + 78.0), 16,
		Color(DeskKit.INK, 0.7), tw)

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
