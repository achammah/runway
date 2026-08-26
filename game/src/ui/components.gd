class_name DeskKit
extends RefCounted
## THE DESK KIT — the drawn components every binder desk is built from.
## Binding source: docs/design/10-interface-language.md §2 (the component
## library) and §1 (palette, type scale, paper, motion). Lanes USE these; a lane
## that forks one has shipped a second design system.
##
## Every function takes the Binder as `b` and draws through its public hand
## (`b.label`, `b.ink_btn`, `b.pane`, `b.fmt`, `b.wrap_h`, `b.desk`), so a desk
## file never reaches into the sheet and the whole binder keeps one voice.
##
## THE CURSOR IDIOM: a component returns the y it ENDED at, measured, never
## assumed — pass that y to the next one. Nothing here reads a fixed step for
## wrapping text (the street stacked on itself the week a thesis wrapped).

const CREAM := Color("F2EAD3")
const INK := Color("1E1E1E")
const PEN := Color("E86A5C")
const SAGE := Color("8FA582")
const YELL := Color("F4B942")
const BLUE := Color("6E8CA0")

## THE TYPE SCALE (§1.3) — six bands with roles. A size may flex a step to fit a
## measured line; a band never skips (a receipt is never set at ROW).
const HERO := 46
const TITLE := 38
const ROW := 30
const STATUS := 27
const DETAIL := 23
const LAW := 21

## THE COLUMN GRAMMAR OF A DESK ROW (§1.4): identity → state → live effect →
## controls. Eyes travel left to right, from what it is to what you can do.
const X_ID := 10.0
const X_VALUE := 430.0
const X_LEVER := 520.0
const X_EFFECT := 688.0
const X_EXPAND := 936.0
const X_MINUS := 1000.0
const X_PLUS := 1064.0
const BTN := Vector2(52, 46)
const PANE_W := 1160.0
const PANE_H := 760.0
const LIST_CAP := 6          ## six cards, then "+N more" — the binder never scrolls
const FOOTER_Y := 700.0      ## the computed-stats line
const RULES_Y := 734.0       ## the desk-law line, or the warning that outranks it

## THE REWORK GRAMMAR (docs/design/11-binder-rework.md): hero band, then 2-4 paper
## cards, then the teaching foot. Every number below is kit-owned so a desk spends
## nothing but a y cursor — per-desk arithmetic is how nine pages drift apart.
const HERO_BIG := 56         ## the hero band's number (the doctrine's 44-64 band)
const HERO_SLOT := 120.0     ## the instrument slot at the band's left, caller-drawn
const BAND_MIN := 108.0      ## the band is never shorter than its instrument
const AIR_ROW := 18.0        ## ≥18px between rows (Law 6 — air is a feature)
const AIR_CARD := 24.0       ## ≥24px between cards
const CARD_PAD := 18.0       ## paper edge → content, both sides
const CARD_TITLE := 24       ## the card's pen title
const CARD_HEAD := 52.0      ## title band: the first row starts this far down
const CARD_CTRL := 128.0     ## the ± gutter a card reserves when its rows carry one
const MONEY := 26            ## a card row: label left, money right (Law 5's row band)
const MONEY_PITCH := 44.0    ## 26px of type + the 18px of air
const TWOBAR_H := 30.0       ## one pen-stroke bar
const TWOBAR_GAP := 4.0      ## between two segment strokes of the same bar
const TWOBAR_PITCH := 56.0   ## between the two bars
const TWOBAR_LAB := 96.0     ## the end-label column, left of the bar
const TWOBAR_NUM := 220.0    ## the value text's room, right of the longest bar
const FUNNEL_LAB := 120.0    ## the stage name, left of the mouth
const FUNNEL_H := 66.0       ## one trapezoid
const FUNNEL_GAP := 8.0      ## between two mouths
const FUNNEL_NARROW := 0.18  ## each stage keeps 18% less width than the one above
const METER_H := 22.0        ## the drawn fill (fuse, progress, share)
const GRID2_W := 540.0       ## a lever cell: 2 × 540 + 40 = the pane's 1120
const GRID2_GAP := 40.0
const GRID2_H := 120.0
const SEV_BOX := 26.0        ## the severity dot's footprint, so a row can reserve it

# ─────────────────────────── the desk's own head ────────────────────────────

## The desk's name-line. Returns the y the body may start at.
static func title(b, text: String, y: float = 6.0) -> float:
	b.label(text, Vector2(X_ID, y), TITLE)
	return y + 72.0

## The one number the desk is about, with its name riding along (§3.1).
static func hero(b, number: String, caption: String, y: float = 6.0) -> float:
	b.label(number, Vector2(100.0, y), HERO)
	if caption != "":
		b.label(caption, Vector2(100.0, y + 56.0), ROW, Color(INK, 0.7))
		return y + 104.0
	return y + 68.0

## A 2px ink@0.25 rule across the pane — the divider between groups.
static func rule(b, y: float, x: float = X_ID, w: float = 1120.0) -> float:
	var r := _Rule.new()
	r.w = w
	r.mouse_filter = Control.MOUSE_FILTER_IGNORE
	r.position = Vector2(x, y)
	r.set_deferred("size", Vector2(w, 4.0))
	b.pane().add_child(r)
	return y + 16.0

# ────────────────────────── 2.1 the world-clamped stepper ────────────────────

## THE GAME'S ONLY SLIDER. Set a number the world allows, one deliberate notch
## at a time — where a spec says slider, build this (drag has no pen).
##
## cfg: name · why · value · effect · on_minus · on_plus · at_min · at_max ·
##      bound (the reason printed at the bound) · pitch (row height)
## A stepper with no live-effect string does not ship: mechanics visible at the
## point of decision is house law.
static func stepper(b, y: float, cfg: Dictionary) -> float:
	var pitch := float(cfg.get("pitch", 78.0))
	var disabled := bool(cfg.get("disabled", false))
	var body := Color(INK, 0.35) if disabled else INK
	b.label(String(cfg.get("name", "")).to_upper(), Vector2(X_ID, y), 28, body)
	var why := String(cfg.get("why", ""))
	if why != "":
		b.label(why, Vector2(X_ID, y + 34.0), LAW, Color(INK, 0.35 if disabled else 0.6), 480.0)
	# THE BOUND PRINTS ITS REASON, and the two ways of saying it never overlap:
	# the note rides the value line while it fits in that column, and drops into
	# the effect column when it does not. (Unfitted, "$100,000  (era cap)" wrote
	# itself straight through "no bank answers a garage".)
	var bound := String(cfg.get("bound", ""))
	var effect := String(cfg.get("effect", ""))
	var x_value := float(cfg.get("x_value", X_LEVER))
	var val_w := X_EFFECT - x_value - 8.0
	var val_text := String(cfg.get("value", ""))
	if bound != "":
		var joined := val_text + "  " + bound
		if b.font().get_string_size(joined, HORIZONTAL_ALIGNMENT_LEFT, -1, ROW).x <= val_w:
			val_text = joined
		else:
			effect = bound if effect == "" else bound + " · " + effect
	b.label(val_text, Vector2(x_value, y + 4.0), ROW,
		Color(INK, 0.35) if disabled else PEN, val_w)
	# WHAT THIS NUMBER IS DOING RIGHT NOW, in the engine's own formula, or —
	# at a bound, disabled, or honestly zero — why it is doing nothing.
	b.label(effect, Vector2(X_EFFECT, y + 12.0), DETAIL,
		Color(INK, 0.35 if disabled else 0.75), 300.0)
	var at_min := disabled or bool(cfg.get("at_min", false))
	var at_max := disabled or bool(cfg.get("at_max", false))
	_glyph(b, "−", Vector2(X_MINUS, y), at_min, cfg.get("on_minus", Callable()))
	_glyph(b, "+", Vector2(X_PLUS, y), at_max, cfg.get("on_plus", Callable()))
	return y + pitch

## A dead glyph dims and does nothing — the reason is already printed beside it.
static func _glyph(b, text: String, pos: Vector2, dead: bool, on_press) -> void:
	var btn := Button.new()
	btn.text = text
	btn.position = pos
	btn.size = BTN
	b.ink_btn(btn)
	if dead:
		btn.add_theme_color_override("font_color", Color(INK, 0.35))
		btn.add_theme_color_override("font_hover_color", Color(INK, 0.35))
		btn.disabled = true
		btn.add_theme_color_override("font_disabled_color", Color(INK, 0.35))
	elif on_press is Callable and (on_press as Callable).is_valid():
		var cb := on_press as Callable
		btn.pressed.connect(func() -> void:
			b.desk.erase("armed")   # any other control disarms the armed one
			cb.call()
			b.refresh())
		# HOLD TO REPEAT (09's bench spec, kit-owned so every desk gets it):
		# after 0.45s held, the glyph re-fires 5×/s. The refresh rebuilds the
		# pane, so the timer dies with the button — no leak, no ghost press.
		var rt := Timer.new()
		rt.wait_time = 0.2
		rt.one_shot = false
		btn.add_child(rt)
		var held := {"t": 0.0}
		btn.button_down.connect(func() -> void:
			held["t"] = Time.get_ticks_msec()
			rt.start(0.45))
		btn.button_up.connect(func() -> void: rt.stop())
		rt.timeout.connect(func() -> void:
			rt.wait_time = 0.2
			b.desk.erase("armed")
			cb.call()
			b.refresh())
	b.pane().add_child(btn)

## THE NAMED LADDER every stepper walks. `steps` is the world's own list (lever
## amounts, fair-price multiples, borrow sizes, salaries, terms); the engine
## re-clamps on write, the UI is never trusted.
static func ladder(steps: Array, cur: float, dir: int) -> float:
	if steps.is_empty():
		return cur
	var idx := 0
	for i in steps.size():
		if float(steps[i]) <= cur:
			idx = i
	idx = clampi(idx + dir, 0, steps.size() - 1)
	return float(steps[idx])

static func at_min(steps: Array, cur: float) -> bool:
	return not steps.is_empty() and cur <= float(steps[0])

static func at_max(steps: Array, cur: float) -> bool:
	return not steps.is_empty() and cur >= float(steps[steps.size() - 1])

# ───────────────────────────── 2.2 the ▸ affordance ──────────────────────────

## The expand mark: a drawn triangle, never a typed glyph (the hand font has
## never carried ▸, and a tofu box is a shipped bug). One row expands, and the
## expansion REPLACES the list with a full-pane DETAIL state.
static func expand(b, pos: Vector2, on_press: Callable) -> void:
	var btn := Button.new()
	btn.flat = true
	btn.position = pos
	btn.size = BTN
	for stn in ["normal", "hover", "pressed", "focus"]:
		btn.add_theme_stylebox_override(stn, StyleBoxEmpty.new())
	var tri := _Tri.new()
	tri.mouse_filter = Control.MOUSE_FILTER_IGNORE
	tri.position = Vector2(14, 11)
	tri.set_deferred("size", Vector2(24, 24))
	btn.add_child(tri)
	btn.mouse_entered.connect(func() -> void:
		tri.col = PEN
		tri.queue_redraw())
	btn.mouse_exited.connect(func() -> void:
		tri.col = INK
		tri.queue_redraw())
	btn.pressed.connect(func() -> void:
		on_press.call()
		b.refresh())
	b.pane().add_child(btn)

## The way back out of any sub-state, always the first thing readable (§4.1).
static func back(b, text: String, on_press: Callable, pos := Vector2(10.0, 6.0)) -> void:
	word(b, text, pos, on_press, STATUS, INK, 300.0)

# ──────────────────────────── 2.3 THE REVIEW CARD ────────────────────────────

## A PROPOSAL AWAITING THE FOUNDER'S PEN — the load-bearing component. The world
## (an LLM, or the engine itself) hands over a filled-in paper form; the founder
## adjusts the adjustable lines and signs it into the books, or tears it up.
## NOTHING AN LLM WROTE EVER ENTERS STATE UNREVIEWED.
##
## cfg:
##   banner   the coral line naming the provenance and the ask
##   read     Array[String] — the facts the founder does NOT get to edit
##   groups   Array[{caption, lines: Array[stepper cfg], sum, sum_col}]
##   verdict  the lesson line when one applies (coral)
##   note     the keyless provenance footnote (ink 0.5)
##   refused  the engine's refusal after a failed confirm (coral)
##   confirm / cancel   the two words; confirm first, cancel never coral
##   on_confirm / on_cancel
##
## It renders on the same pane, same cursor, same hand as a DETAIL page — it IS
## a desk sheet, not a dialog. No scrim, no floating card.
static func review(b, cfg: Dictionary, y: float = 6.0) -> float:
	b.label(String(cfg.get("banner", "")), Vector2(X_ID, y), STATUS, PEN, 1100.0)
	y += 44.0
	y = rule(b, y)
	for line in cfg.get("read", []):
		var t := String(line)
		b.label(t, Vector2(X_ID, y), DETAIL, Color(INK, 0.8), 1100.0)
		y += maxf(b.wrap_h(t, DETAIL, 1100.0), 26.0) + 6.0
	y += 8.0
	for grp in cfg.get("groups", []):
		var g: Dictionary = grp
		var cap := String(g.get("caption", ""))
		if cap != "":
			b.label(cap, Vector2(X_ID, y), DETAIL, Color(INK, 0.6), 900.0)
			y += 32.0
		for ln in g.get("lines", []):
			var l: Dictionary = ln
			l["pitch"] = float(l.get("pitch", 52.0))
			l["x_value"] = float(l.get("x_value", X_VALUE))
			y = stepper(b, y, l)
		var sum_text := String(g.get("sum", ""))
		if sum_text != "":
			# THE BLUE LINE DOES THE ARITHMETIC OUT LOUD — the patient accountant
			b.label(sum_text, Vector2(X_ID + 18.0, y), DETAIL,
				g.get("sum_col", BLUE), 1080.0)
			y += maxf(b.wrap_h(sum_text, DETAIL, 1080.0), 26.0) + 14.0
	var verdict := String(cfg.get("verdict", ""))
	if verdict != "":
		b.label(verdict, Vector2(X_ID, y), STATUS, PEN, 1100.0)
		y += maxf(b.wrap_h(verdict, STATUS, 1100.0), 30.0) + 10.0
	var refused := String(cfg.get("refused", ""))
	if refused != "":
		b.label(refused, Vector2(X_ID, y), STATUS, PEN, 1100.0)
		y += maxf(b.wrap_h(refused, STATUS, 1100.0), 30.0) + 10.0
	var note := String(cfg.get("note", ""))
	if note != "":
		b.label(note, Vector2(X_ID, y), LAW, Color(INK, 0.5), 1100.0)
		y += 34.0
	y += 10.0
	# confirm first, cancel second and never coral — cancel is safe, not scary
	var confirm := String(cfg.get("confirm", "sign it"))
	var on_confirm: Callable = cfg.get("on_confirm", Callable())
	# NO HANDLER FROM `word` HERE (an empty Callable leaves the button bare): the
	# confirm press owns its whole beat — stroke first, books second, rebuild last.
	# Letting word() rebuild the pane first freed the very button the stroke draws
	# under.
	var btn := word(b, confirm, Vector2(X_ID, y), Callable(), ROW, INK, 420.0)
	btn.pressed.connect(func() -> void:
		# THE SIGNATURE BEAT: the stroke draws under the words, THEN the books change
		sign_stroke(b, btn, func() -> void:
			if on_confirm.is_valid():
				on_confirm.call()
			b.refresh()))
	var on_cancel: Callable = cfg.get("on_cancel", Callable())
	word(b, String(cfg.get("cancel", "tear it up")), Vector2(X_ID + 440.0, y), func() -> void:
		if on_cancel.is_valid():
			on_cancel.call()
		b.refresh(), ROW, Color(INK, 0.7), 320.0)
	return y + 56.0

# ─────────────────────────── 2.4 the card grid ───────────────────────────────

## ONE CARD ANATOMY, three densities — applicants, bets, term sheets, notes,
## machines. Line 1 is the name and the deciding numbers WITH THEIR ANCHORS (a
## number without its anchor is not a decision); line 2 is the world's voice;
## line 3, on dense cards, is cost and odds. Actions sit at the stepper columns.
##
## cfg: name · pips (0-5) · flavor · dense · actions Array[{text, on, reason}]
##      · pitch (62-118 by density) · state ("", "committed", "expiring")
static func card(b, y: float, cfg: Dictionary) -> float:
	var pitch := float(cfg.get("pitch", 66.0))
	b.label(String(cfg.get("name", "")), Vector2(X_ID, y), ROW, INK, 900.0)
	var px := float(cfg.get("pips_x", 0.0))
	if cfg.has("pips") and px > 0.0:
		pips(b, Vector2(px, y + 8.0), int(cfg.get("pips", 0)))
	var flavor := String(cfg.get("flavor", ""))
	if flavor != "":
		b.label(flavor, Vector2(X_ID, y + 34.0), DETAIL, Color(INK, 0.45), 900.0)
	var dense := String(cfg.get("dense", ""))
	if dense != "":
		b.label(dense, Vector2(X_ID, y + 66.0), DETAIL, Color(INK, 0.65), 900.0)
	var ax := X_MINUS
	var acts: Array = cfg.get("actions", [])
	if acts.size() > 1:
		ax = 940.0
	for a in acts:
		var ad: Dictionary = a
		var reason := String(ad.get("reason", ""))
		if reason != "":
			# A CAP THAT BITES SAYS SO where the action was (§2.4 cap-full)
			b.label(reason, Vector2(ax, y + 8.0), DETAIL, Color(INK, 0.35), 200.0)
		else:
			word(b, String(ad.get("text", "")), Vector2(ax, y), ad.get("on", Callable()),
				STATUS, INK, 160.0)
		ax += 172.0
	return y + pitch

## Five marks, filled coral under an ink edge — never a bare number for a 1-5
## scale. EVERY BOX IS INKED, on and off alike.
static func pips(b, pos: Vector2, filled: int, total: int = 5) -> void:
	var p := _Pips.new()
	p.filled = filled
	p.total = total
	p.mouse_filter = Control.MOUSE_FILTER_IGNORE
	p.position = pos
	p.set_deferred("size", Vector2(float(total) * 21.0, 16.0))
	b.pane().add_child(p)

# ──────────────────────────── 2.5 the stage board ────────────────────────────

## NAMED THINGS MOVING THROUGH NAMED GATES — pen-ruled columns, two-line chips,
## and no controls at all: the board is the founder's wall calendar, and hands
## move deals in the story, not by dragging.
##
## columns: Array[{head, chips: Array[{name, facts_lead, heat, facts, note, flavor}]}]
## `facts_lead · HEAT · facts` renders as ONE row with only the heat word coloured.
static func board(b, y: float, columns: Array, empty_line: String = "") -> float:
	if columns.is_empty():
		return empty(b, Vector2(X_ID, y), empty_line, "")
	var n := columns.size()
	var col_w := (1120.0 / float(n))
	# THE RULES ARE COUNTED FIRST. §2.5 allows "headers alone when the chips make
	# the columns obvious" — and on an empty board there are no chips, so the
	# pen-ruled columns had nothing to separate and drew themselves straight
	# through the authored empty line instead.
	var live := 0
	for c0 in columns:
		live += (c0 as Dictionary).get("chips", []).size()
	var ruled := live > 0
	live = 0
	for ci in n:
		var c: Dictionary = columns[ci]
		var cx := X_ID + float(ci) * col_w
		b.label(String(c.get("head", "")).to_upper(), Vector2(cx, y), 26, INK, col_w - 16.0)
		if ci > 0 and ruled:
			var vr := _VRule.new()
			vr.h = 300.0
			vr.mouse_filter = Control.MOUSE_FILTER_IGNORE
			vr.position = Vector2(cx - 12.0, y)
			vr.set_deferred("size", Vector2(4.0, 300.0))
			b.pane().add_child(vr)
		var cy := y + 44.0
		var chips: Array = c.get("chips", [])
		live += chips.size()
		for ch in chips:
			var chd: Dictionary = ch
			b.label(String(chd.get("name", "")), Vector2(cx, cy), 26, INK, col_w - 20.0)
			cy += maxf(b.wrap_h(String(chd.get("name", "")), 26, col_w - 20.0), 30.0)
			# THE FACTS LINE IS ONE ROW WITH ONE COLOURED WORD (§1.1's heat ramp,
			# 05 §12): `6 seats · warm · wk 1` colours only "warm". A chip that
			# folded the heat into `facts` printed it in the same grey as the seat
			# count and the whole point of the board — is this deal warming or
			# dying — went with it; a chip that gave heat its own ROW cost the
			# column a third of its height. Three measured segments, one line.
			var heat := String(chd.get("heat", ""))
			var facts := String(chd.get("facts", ""))
			if heat != "":
				var lead := String(chd.get("facts_lead", ""))
				if lead == "" and facts != "":
					# no explicit lead: the whole facts string sits ahead of the word
					lead = facts
					facts = ""
				var fx := cx
				if lead != "":
					b.label(lead + "  ·  ", Vector2(fx, cy), DETAIL, Color(INK, 0.7), col_w - 20.0)
					fx += b.font().get_string_size(lead + "  ·  ",
						HORIZONTAL_ALIGNMENT_LEFT, -1, DETAIL).x
				b.label(heat, Vector2(fx, cy), DETAIL, heat_col(heat), col_w - 20.0)
				if facts != "":
					fx += b.font().get_string_size(heat, HORIZONTAL_ALIGNMENT_LEFT, -1, DETAIL).x
					b.label("  ·  " + facts, Vector2(fx, cy), DETAIL, Color(INK, 0.7),
						col_w - 20.0)
				cy += 28.0
			elif facts != "":
				b.label(facts, Vector2(cx, cy), DETAIL, Color(INK, 0.7), col_w - 20.0)
				cy += 28.0
			var note := String(chd.get("note", ""))
			if note != "":
				b.label(note, Vector2(cx, cy), DETAIL, PEN, col_w - 20.0)
				cy += 28.0
			if chips.size() <= 3 and String(chd.get("flavor", "")) != "":
				b.label(String(chd.get("flavor", "")), Vector2(cx, cy), 18,
					Color(INK, 0.45), col_w - 20.0)
				cy += 24.0
			cy += 10.0
	if live == 0 and empty_line != "":
		b.label(empty_line, Vector2(X_ID, y + 60.0), STATUS, Color(INK, 0.6), 1100.0)
	return y + 320.0

# ──────────────────────────── 2.6 the action log ─────────────────────────────

## A RAP SHEET: who this actor is, how they are standing, and the last three
## things they did, each stamped with its week, oldest first — so the line reads
## the way time does. Postures are WORD-MAPS; raw engine floats never print.
##
## cfg: identity · posture · plays · trail Array[String]
static func log_block(b, y: float, cfg: Dictionary) -> float:
	b.label(String(cfg.get("identity", "")), Vector2(X_ID, y), 32, INK, 1100.0)
	y += 44.0
	var posture := String(cfg.get("posture", ""))
	if posture != "":
		b.label(posture, Vector2(30.0, y), DETAIL, Color(INK, 0.8), 1070.0)
		y += maxf(b.wrap_h(posture, DETAIL, 1070.0), 28.0) + 4.0
	var plays := String(cfg.get("plays", ""))
	if plays != "":
		b.label(plays, Vector2(30.0, y), 26, Color(INK, 0.7), 1070.0)
		y += maxf(b.wrap_h(plays, 26, 1070.0), 30.0) + 4.0
	var trail: Array = cfg.get("trail", [])
	if not trail.is_empty():
		var line := "  ·  ".join(PackedStringArray(trail))
		b.label(line, Vector2(30.0, y), DETAIL, Color(INK, 0.7), 1070.0)
		y += maxf(b.wrap_h(line, DETAIL, 1070.0), 28.0)
	return y + 18.0

# ─────────────────────────── 2.7 the teaching footer ─────────────────────────

## THE DESK STATES ITS OWN LAWS. Blue when it computes from the run's own
## numbers; ink 0.5 when it states the standing rules. WARNINGS OUTRANK WISDOM:
## when the pane's warning slot fires, the rules line yields. The computed line
## never yields — it is content.
static func footer(b, cfg: Dictionary) -> void:
	var computed := String(cfg.get("computed", ""))
	var y := float(cfg.get("y", FOOTER_Y))
	if computed != "":
		b.label(computed, Vector2(X_ID, y), LAW, BLUE, 1100.0)
	var warning := String(cfg.get("warning", ""))
	if warning != "":
		b.label(warning, Vector2(X_ID, float(cfg.get("rules_y", RULES_Y))), LAW, PEN, 1100.0)
		return
	var rules := String(cfg.get("rules", ""))
	if rules != "":
		b.label(rules, Vector2(X_ID, float(cfg.get("rules_y", RULES_Y))), LAW,
			Color(INK, 0.5), 1100.0)

# ──────────────────────────── 2.9 the two-tap arm ────────────────────────────

## IRREVERSIBLE OR EXPENSIVE ACTS GET A VISIBLE COST AND A SECOND CHANCE —
## without a dialog box. The first press re-captions the SAME control in coral,
## carrying the price or the consequence; the second fires. Anything else that
## rebuilds or leaves disarms it, and only one control on a pane is ever armed.
##
## Arm iff the act destroys something a later week cannot rebuild, or books an
## immediate real cost. Steppers, hires, repayments and navigation never arm.
static func arm(b, id: String, plain: String, armed_caption: String, pos: Vector2,
		on_fire: Callable, w: float = 300.0, sz: int = STATUS) -> Button:
	var is_armed := String(b.desk.get("armed", "")) == id
	var btn := Button.new()
	btn.flat = true
	btn.text = armed_caption if is_armed else plain
	btn.position = pos
	btn.size = Vector2(w, 46.0)
	btn.add_theme_font_override("font", b.font())
	btn.add_theme_font_size_override("font_size", sz)
	btn.add_theme_color_override("font_color", PEN if is_armed else INK)
	btn.add_theme_color_override("font_hover_color", PEN)
	btn.alignment = HORIZONTAL_ALIGNMENT_LEFT
	for stn in ["normal", "hover", "pressed", "focus"]:
		btn.add_theme_stylebox_override(stn, StyleBoxEmpty.new())
	btn.pressed.connect(func() -> void:
		if String(b.desk.get("armed", "")) == id:
			b.desk.erase("armed")
			sign_stroke(b, btn, func() -> void:
				on_fire.call()
				b.refresh())
			return
		b.desk["armed"] = id   # arming a second control disarms the first
		b.refresh())
	b.pane().add_child(btn)
	return btn

## THE SIGNATURE BEAT (§1.6.4): a coral rule draws under the pressed words in
## 0.14s, holds 0.10s, and only then does the act fire. The most consequential
## click in the game must never feel like a menu.
static func sign_stroke(b, btn: Button, on_done: Callable) -> void:
	var tw_w: float = b.font().get_string_size(btn.text, HORIZONTAL_ALIGNMENT_LEFT, -1,
		btn.get_theme_font_size("font_size")).x
	var stroke := _Stroke.new()
	stroke.mouse_filter = Control.MOUSE_FILTER_IGNORE
	stroke.position = btn.position + Vector2(-4.0, btn.size.y - 10.0)
	stroke.set_deferred("size", Vector2(tw_w + 12.0, 10.0))
	b.pane().add_child(stroke)
	var tw: Tween = b.create_tween()
	tw.tween_method(func(p: float) -> void:
		if is_instance_valid(stroke):
			stroke.progress = p
			stroke.queue_redraw(), 0.0, 1.0, 0.14)
	tw.tween_interval(0.10)
	tw.tween_callback(on_done)

## A flat word button — the binder's only kind of button. The hitbox pads to the
## 44px minimum even when the word is short.
static func word(b, text: String, pos: Vector2, on_press: Callable, sz: int = STATUS,
		col: Color = INK, w: float = 200.0) -> Button:
	var btn := Button.new()
	btn.flat = true
	btn.text = text
	btn.position = pos
	btn.size = Vector2(maxf(w, 160.0), 46.0)
	btn.add_theme_font_override("font", b.font())
	btn.add_theme_font_size_override("font_size", sz)
	btn.add_theme_color_override("font_color", col)
	btn.add_theme_color_override("font_hover_color", PEN)
	btn.alignment = HORIZONTAL_ALIGNMENT_LEFT
	for stn in ["normal", "hover", "pressed", "focus"]:
		btn.add_theme_stylebox_override(stn, StyleBoxEmpty.new())
	if on_press.is_valid():
		btn.pressed.connect(func() -> void:
			b.desk.erase("armed")
			on_press.call()
			b.refresh())
	b.pane().add_child(btn)
	return btn

# ──────────────────────── 2.10 the drawn instruments ─────────────────────────

## HORIZONTAL PEN-STROKE BARS: w = 40 + 460×v/max, a tinted fill under a seeded
## ink outline, the label and the value ON the bar row. A chart without its
## number is decoration, and decoration does not ship. Bar maxima are the
## VISIBLE set's max, never all-time.
##
## rows: Array[{label, value (float), text, col}]
static func bars(b, pos: Vector2, rows: Array, pitch: float = 52.0) -> float:
	var hi := 0.0
	for r in rows:
		hi = maxf(hi, float((r as Dictionary).get("value", 0.0)))
	var y := pos.y
	for r2 in rows:
		var rd: Dictionary = r2
		var v := float(rd.get("value", 0.0))
		var w := 40.0 + 460.0 * (v / maxf(hi, 1.0))
		b.label(String(rd.get("label", "")).to_upper(), Vector2(pos.x, y), DETAIL, INK, 200.0)
		var bar := _Bar.new()
		bar.col = rd.get("col", BLUE)
		bar.seed_i = int(y)
		bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
		bar.position = Vector2(pos.x + 170.0, y + 2.0)
		bar.set_deferred("size", Vector2(w, 26.0))
		b.pane().add_child(bar)
		b.label(String(rd.get("text", "")), Vector2(pos.x + 180.0 + w, y), DETAIL,
			Color(INK, 0.8), 420.0)
		y += pitch
	return y

## The spark, with its caption — the existing chart idiom, one call.
static func spark(b, b_series: Array, pos: Vector2, size_v: Vector2, col: Color,
		caption: String = "") -> float:
	if caption != "":
		b.label(caption, Vector2(pos.x, pos.y), 24, Color(INK, 0.6), 600.0)
		pos.y += 32.0
	b.spark(b_series, pos, size_v, col)
	return pos.y + size_v.y + 12.0

## The heat ramp (§1.1): coral → yell → sage, colouring ONE WORD, never a line
## and never a fill.
static func heat_col(word_v: String) -> Color:
	match word_v:
		"hot", "healthy", "flush", "warm+":
			return SAGE
		"warm", "steady":
			return YELL
	return PEN

# ═════════════════ THE REWORK PRIMITIVES (11-binder-rework) ══════════════════
## The owner's verdict on the first binder was "confusing, not clear, not
## beautiful": every row the same size, money hidden inside prose, no paper
## structure, and nothing DRAWN on the page where the densest numbers live.
## These eight are the answer, built once for both engines. A desk composes them
## against a single y cursor and does no arithmetic of its own.

## A HAND-DRAWN RULE — the wobbled twin of `rule()`, for the rework's own
## structure. `rule()` keeps its dead-straight 2px line because eight shipped
## desks measure themselves against it; a fresh page rules itself with the pen.
static func pen_rule(b, y: float, x: float = X_ID, w: float = 1120.0,
		col: Color = Color(INK, 0.25), seed_i: int = 5) -> float:
	var r := _PenLine.new()
	r.w = w
	r.col = col
	r.seed_i = seed_i
	r.mouse_filter = Control.MOUSE_FILTER_IGNORE
	r.position = Vector2(x, y)
	r.set_deferred("size", Vector2(w, 8.0))
	b.pane().add_child(r)
	return y + 16.0

## LAW 1 — THE HERO ANSWERS THE TAB'S QUESTION. One big number, one plain
## sentence under it, and an optional drawn instrument beside it: the answer to
## "how is this doing?" in one second, before the eye reaches a single card.
##
## `instrument` reserves the left HERO_SLOT and nothing else — THE CALLER DRAWS
## THE INSTRUMENT (a jar, a meter, a clock, a pie), because only the desk knows
## what shape its own idea has. Returns the band's bottom y: hand it to the first
## card and the page composes itself.
static func hero_band(b, big_text: String, sentence: String, col: Color = INK,
		y: float = 6.0, instrument: bool = false) -> float:
	var x := X_ID + (HERO_SLOT if instrument else 0.0)
	var w := PANE_W - x - 40.0
	b.label(big_text, Vector2(x, y), HERO_BIG, col, w)
	var bottom := y + 74.0
	if sentence != "":
		# ONE PLAIN SENTENCE, in words a tired founder reads without decoding —
		# the number said again in English, never a second number.
		b.label(sentence, Vector2(x, y + 66.0), ROW, Color(INK, 0.7), w)
		bottom = y + 66.0 + maxf(b.wrap_h(sentence, ROW, w), 34.0)
	bottom = maxf(bottom, y + BAND_MIN)
	pen_rule(b, bottom + 10.0)
	return bottom + 26.0

## LAW 3 — CARDS, NOT LISTS. A wobbled paper card cut by the same scissors as
## every other card in the game (the draft-card recipe: shadow (7,9)@0.18, an ink
## edge walked in 13 steps with 2.1 of jitter, seeded by the card's own x so
## neighbours are visibly hand-cut and never cloned). The body is the same cream
## held one shade up to the light, which is what makes a card read as lying ON
## the clipboard rather than being a hole cut in it.
##
## Returns the card's own geometry, so a row inside it does no arithmetic:
##   content_x  where a label starts        money_x  where a value ENDS
##   content_y  where the first row sits    bottom   where the card ends
##   cursor     the running y money_row advances (starts at content_y)
## `controls` reserves the ± gutter — pass it and the money column stays put
## whether or not a given row carries a stepper (Law 2: one column, always).
static func card_frame(b, x: float, y: float, w: float, h: float, title: String,
		controls: bool = false) -> Dictionary:
	var c := _Card.new()
	c.lean = int(x) % 5
	c.mouse_filter = Control.MOUSE_FILTER_IGNORE
	c.position = Vector2(x, y)
	c.set_deferred("size", Vector2(w, h))
	b.pane().add_child(c)
	if title != "":
		# UPPERCASE IS THE BINDER'S BOLD (§1.3) — one hand, and emphasis is size,
		# caps or the pen, never a second font.
		b.label(title.to_upper(), Vector2(x + CARD_PAD, y + 12.0), CARD_TITLE, INK,
			w - CARD_PAD * 2.0)
	var money_x := x + w - CARD_PAD - (CARD_CTRL if controls else 0.0)
	var content_y := y + (CARD_HEAD if title != "" else CARD_PAD)
	return {
		"content_x": x + CARD_PAD,
		"content_y": content_y,
		"cursor": content_y,
		"money_x": money_x,
		"bottom": y + h,
		"x": x, "y": y, "w": w, "h": h,
	}

## LAW 2 — MONEY LIVES IN COLUMNS, NEVER IN SENTENCES. The label at the card's
## left, the value RIGHT-ALIGNED so every dollar on the card ends on one line,
## and — when the row is a lever — the ± glyphs in the gutter the frame reserved.
##
## The frame carries the cursor, so a desk writes four rows in four lines and
## never adds a pitch by hand. Returns the next y as well, for a caller that
## wants to interleave something else.
static func money_row(b, frame: Dictionary, label_text: String, value: String,
		col: Color = INK, on_minus = null, on_plus = null,
		at_min: bool = false, at_max: bool = false) -> float:
	var y := float(frame.get("cursor", frame.get("content_y", 0.0)))
	var cx := float(frame.get("content_x", X_ID))
	var mx := float(frame.get("money_x", PANE_W - CARD_PAD))
	b.label(label_text, Vector2(cx, y), MONEY, Color(INK, 0.85), mx - cx - 8.0)
	var v: Label = b.label(value, Vector2(cx, y), MONEY, col, mx - cx)
	v.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	if on_minus != null or on_plus != null:
		_glyph(b, "−", Vector2(mx + 16.0, y - 6.0), at_min,
			on_minus if on_minus != null else Callable())
		_glyph(b, "+", Vector2(mx + 76.0, y - 6.0), at_max,
			on_plus if on_plus != null else Callable())
	var next := y + MONEY_PITCH
	frame["cursor"] = next
	return next

## LAW 4 — DRAW THE SHAPE OF THE IDEA. In and out is not two sentences, it is two
## bars of different length: the SHAPE lands before a single digit does. Both
## bars share one scale (the larger total fills the track), and either may be
## segmented by lane — the segments are separate pen strokes with a hair of paper
## between them, so a reader counts the lanes without a legend.
##
## `a_frac_segments` / `b_frac_segments` are the lane MAGNITUDES in one unit;
## they sum to that bar's total. A plain unsegmented bar is a one-item array.
static func twobar(b, x: float, y: float, w: float,
		a_label: String, a_val_text: String, a_frac_segments: Array,
		b_label: String, b_val_text: String, b_frac_segments: Array,
		a_col: Color = SAGE, b_col: Color = PEN) -> float:
	var ta := _sum(a_frac_segments)
	var tb := _sum(b_frac_segments)
	var hi := maxf(maxf(ta, tb), 1.0)
	var track := maxf(w - TWOBAR_LAB - TWOBAR_NUM, 120.0)
	y = _one_bar(b, x, y, track, a_label, a_val_text, a_frac_segments, ta, hi, a_col, 3)
	y = _one_bar(b, x, y, track, b_label, b_val_text, b_frac_segments, tb, hi, b_col, 9)
	return y

static func _sum(vals: Array) -> float:
	var t := 0.0
	for v in vals:
		t += maxf(float(v), 0.0)
	return t

static func _one_bar(b, x: float, y: float, track: float, lab: String, val_text: String,
		segs: Array, total: float, hi: float, col: Color, seed_i: int) -> float:
	b.label(lab.to_upper(), Vector2(x, y + 2.0), MONEY, INK, TWOBAR_LAB - 8.0)
	var full := track * (total / hi)
	var bx := x + TWOBAR_LAB
	var drawn := 0.0
	var live: Array = []
	for s in segs:
		if float(s) > 0.0:
			live.append(float(s))
	if live.is_empty():
		live = [0.0]
	for i in live.size():
		# EVERY LANE KEEPS A VISIBLE STROKE: a $12 line beside a $1,400 one still
		# has to be countable, so no segment is ever thinner than the pen.
		var sw := maxf(full * (float(live[i]) / maxf(total, 1.0)) - TWOBAR_GAP, 6.0)
		var seg := _Bar.new()
		seg.col = col
		seg.seed_i = seed_i + i * 7
		seg.mouse_filter = Control.MOUSE_FILTER_IGNORE
		seg.position = Vector2(bx + drawn, y)
		seg.set_deferred("size", Vector2(sw, TWOBAR_H))
		b.pane().add_child(seg)
		drawn += sw + TWOBAR_GAP
	b.label(val_text, Vector2(bx + maxf(drawn, 8.0) + 4.0, y + 2.0), MONEY,
		Color(INK, 0.85), TWOBAR_NUM - 12.0)
	return y + TWOBAR_PITCH

## THE FUNNEL IS A FUNNEL — four narrowing pen trapezoids, the number written
## inside each mouth. The SHAPE is the lesson before any figure lands, and a
## stage the company has not earned the eyesight to see keeps its mouth and loses
## its number: `known = false` draws the outline faint and writes "?", so the fog
## is visible rather than absent (§3.10).
##
## stages: Array[{label, value_text, known, col}] — up to four, top to bottom.
static func funnel_shape(b, x: float, y: float, w: float, stages: Array) -> float:
	var n := mini(stages.size(), 4)
	if n <= 0:
		return y
	var fx := x + FUNNEL_LAB
	var fw := w - FUNNEL_LAB
	var fh := float(n) * (FUNNEL_H + FUNNEL_GAP) - FUNNEL_GAP
	var shape := _Funnel.new()
	shape.stages = stages.slice(0, n)
	shape.mouse_filter = Control.MOUSE_FILTER_IGNORE
	shape.position = Vector2(fx, y)
	shape.set_deferred("size", Vector2(fw, fh))
	b.pane().add_child(shape)
	for i in n:
		var st: Dictionary = stages[i]
		var known := bool(st.get("known", true))
		var sy := y + float(i) * (FUNNEL_H + FUNNEL_GAP)
		b.label(String(st.get("label", "")).to_upper(), Vector2(x, sy + 20.0), DETAIL,
			Color(INK, 0.8 if known else 0.4), FUNNEL_LAB - 10.0)
		var mouth := fw * (1.0 - FUNNEL_NARROW * (float(i) + 0.5))
		var lbl: Label = b.label(String(st.get("value_text", "")) if known else "?",
			Vector2(fx + (fw - mouth) * 0.5, sy + 14.0), ROW,
			INK if known else Color(INK, 0.4), mouth)
		lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	return y + fh + 12.0

## A DRAWN FILL — the fuse, the progress vessel laid flat, any "how far along is
## it". A pen outline round the whole track, a tinted wash to `frac`, and the
## words after it: a chart without its number is decoration, and decoration does
## not ship.
static func meter(b, x: float, y: float, w: float, frac: float, col: Color,
		label_text: String = "") -> float:
	var m := _Meter.new()
	m.frac = clampf(frac, 0.0, 1.0)
	m.col = col
	m.mouse_filter = Control.MOUSE_FILTER_IGNORE
	m.position = Vector2(x, y)
	m.set_deferred("size", Vector2(w, METER_H))
	b.pane().add_child(m)
	if label_text != "":
		b.label(label_text, Vector2(x + w + 14.0, y - 6.0), DETAIL, Color(INK, 0.8), 420.0)
	return y + METER_H + 12.0

## THE 2×2 LEVER GRID — eight stacked stepper rows were the ledger's worst wall
## of same-weight text; four compact cells are the same levers read in a glance.
## Each cell: the NAME small, the money big and in the founder's own pen, the
## effect in one word, and the ± tight against the cell's right edge.
##
## cells: Array[{name, value, effect, on_minus, on_plus, at_min, at_max}]
static func grid2(b, x: float, y: float, cells: Array) -> float:
	var n := mini(cells.size(), 4)
	for i in n:
		var c: Dictionary = cells[i]
		var cx := x + float(i % 2) * (GRID2_W + GRID2_GAP)
		var cy := y + float(i / 2) * GRID2_H
		b.label(String(c.get("name", "")).to_upper(), Vector2(cx, cy), 22, INK, 320.0)
		b.label(String(c.get("value", "")), Vector2(cx, cy + 28.0), MONEY, PEN, 380.0)
		b.label(String(c.get("effect", "")), Vector2(cx, cy + 62.0), 18,
			Color(INK, 0.6), GRID2_W - 130.0)
		_glyph(b, "−", Vector2(cx + GRID2_W - 116.0, cy + 18.0),
			bool(c.get("at_min", false)), c.get("on_minus", Callable()))
		_glyph(b, "+", Vector2(cx + GRID2_W - 58.0, cy + 18.0),
			bool(c.get("at_max", false)), c.get("on_plus", Callable()))
		# the ruled ledger under each cell: structure without a box round it
		pen_rule(b, cy + GRID2_H - 18.0, cx, GRID2_W, Color(INK, 0.14), 11 + i)
	return y + float((n + 1) / 2) * GRID2_H + 8.0

## THE ATTENTION DOT — heat as a shape, so the threats list ranks itself before
## a word is read. A note is ink and quiet; a warning is the pen; an alarm is the
## same pen, simply bigger, with a loop drawn round it. NOTHING HERE PULSES: the
## screen is allowed one pulsing element and the ticker owns it (§2.8).
static func sev_dot(b, x: float, y: float, severity: int) -> void:
	var d := _Dot.new()
	d.severity = clampi(severity, 1, 3)
	d.mouse_filter = Control.MOUSE_FILTER_IGNORE
	d.position = Vector2(x, y)
	d.set_deferred("size", Vector2(SEV_BOX, SEV_BOX))
	b.pane().add_child(d)

# ─────────────────────────── 2.11 the empty states ───────────────────────────

## A DESK WITH NOTHING STILL TEACHES: the fact, then the tell or the mechanism
## that fills it. Never blank space, never "No data" — and the invitation names
## the MECHANISM, not a button.
static func empty(b, pos: Vector2, fact: String, tell: String,
		pointer: bool = false) -> float:
	var y := pos.y
	if fact != "":
		b.label(fact, Vector2(pos.x, y), STATUS, Color(INK, 0.65), 1100.0)
		y += maxf(b.wrap_h(fact, STATUS, 1100.0), 32.0) + 6.0
	if tell != "":
		b.label(tell, Vector2(pos.x, y), DETAIL, PEN if pointer else Color(INK, 0.5), 1100.0)
		y += maxf(b.wrap_h(tell, DETAIL, 1100.0), 28.0)
	return y + 10.0

## Six, then the truth about the rest (§2.4 grid math).
static func more(b, pos: Vector2, n: int, tail: String = "wait behind these") -> float:
	if n <= 0:
		return pos.y
	b.label("+%d more %s" % [n, tail], Vector2(pos.x, pos.y), DETAIL, Color(INK, 0.5), 900.0)
	return pos.y + 32.0

# ──────────────────────── 2.12 waiting & keyless states ──────────────────────

## HOW WAITING LOOKS IN A PAPER WORLD: one breathing line and a cancel word.
## No spinner, no dots, no progress bar. The subject is always the fiction —
## the street, the world, the dice — never "loading" and never the vendor.
## Cancel is real: leaving drops the reply on arrival.
static func wait(b, pos: Vector2, phrase: String, on_cancel: Callable) -> float:
	var l: Label = b.label(phrase, pos, STATUS, Color(INK, 0.6), 700.0)
	var br := _Breath.new()
	br.target = l
	l.add_child(br)
	word(b, "cancel", Vector2(pos.x, pos.y + 44.0), on_cancel, DETAIL, Color(INK, 0.7), 160.0)
	return pos.y + 100.0

## The keyless path is never a degraded screen — it is the same desk with a dry
## footnote (§2.12). Print this under a review the house numbers wrote.
const HOUSE_NOTE := "the street shrugged — house numbers"

# ═════════════════ THE DESKKIT v2 PRIMITIVES (binder rework, DAG2) ═══════════
## The ledger family, the didactic zone, the kanban wall, the receipt, the
## ownership instruments and the rung-3 faces — the shapes DECISIONS.md's owner
## picks are made of, built once for both engines. Pixel source: docs/design/
## mockups/06 (ledger sheet), 07 (ADJUST column), 03 (quartet), 14 (arrange),
## 16 (the wall), 18 (ownership). Same cursor idiom as everything above.

## The alarm system's red (DECISIONS: alarm-red). Coral stays money-out;
## ALERT means act. Never spent on anything but attention.
const ALERT := Color("D93425")
const KRAFT := Color("DDBE8C")
const KRAFT2 := Color("CBA96F")
const PAPER2 := Color("F6F0DE")
const CARD_TINT := Color("EFE6CE")
const SAGE_BAND := Color(0.561, 0.647, 0.51, 0.14)   ## the green ledger band
const KRAFT_BAND := Color(0.796, 0.663, 0.435, 0.22) ## a section row's wash

## LEDGER GEOMETRY (mockup 06/07): the row-number gutter, the ADJUST column
## that hosts the two SEPARATE stepper squares, and the row pitches.
const LG_ROWNUM := 34.0      ## faint row numbers live here
const LG_ADJUST := 92.0      ## the ADJUST column (owner's stepper law)
const LG_PAD := 14.0         ## sheet edge → first column
const LG_ROW_H := 40.0       ## a book row
const LG_HEAD_H := 34.0      ## the small-caps header band
const LG_SEC_H := 30.0       ## a section row
const LG_TOT_H := 48.0       ## the double-ruled total
const ADJ_BTN := 27.0        ## one drawn stepper square
const ADJ_GAP := 7.0         ## the visible gap between − and + (the law)

# ─────────────────────────── THE LEDGER SHEET ────────────────────────────────
## ONE ACCOUNTING PRIMITIVE for every money desk (DECISIONS: bills/spend/team/
## the bank's BOOKS). Paper book-keeping, not a webpage table: small-caps
## header with the unit stated once, faint row numbers, vertical column rules,
## the amount column on the classic green band, single rule above a subtotal,
## DOUBLE rule above the total, and the total must equal the hero's number.
##
## cfg:
##   columns  Array[{label, w, align}] — absolute px widths; the kit prepends
##            the 34px row-number gutter itself
##   amount   int — which column is the money band (right-aligned, sage wash)
##   adjust   bool — reserve the ADJUST column right of the amount band; two
##            SEPARATE drawn − + squares with a visible gap (the stepper law;
##            obligation rows simply pass no callables and stay bare)
##   unit     the note said once in the header ("all figures $/week")
##
## Returns the sheet frame: {x, y, w, cursor, cols:[{x,w,align}], amount_i,
## adjust_x, row_n}. Rows advance `cursor`; `ledger_end` draws the outer
## border round everything the book wrote and returns the y after it.
static func ledger_sheet(b, x: float, y: float, w: float, cfg: Dictionary) -> Dictionary:
	var cols_in: Array = cfg.get("columns", [])
	var amount_i := int(cfg.get("amount", maxi(cols_in.size() - 1, 0)))
	var with_adjust := bool(cfg.get("adjust", false))
	var cols: Array = []
	var cx := x + LG_PAD + LG_ROWNUM
	for i in cols_in.size():
		var c: Dictionary = cols_in[i]
		var cw := float(c.get("w", 120.0))
		cols.append({"x": cx, "w": cw, "align": String(c.get("align",
			"right" if i == amount_i else "left"))})
		cx += cw
		if with_adjust and i == amount_i:
			cx += LG_ADJUST
	var sheet := {"x": x, "y": y, "w": w, "cursor": y + LG_HEAD_H, "cols": cols,
		"amount_i": amount_i, "adjust": with_adjust, "row_n": 0}
	sheet["adjust_x"] = float((cols[amount_i] as Dictionary).get("x", x)) \
		+ float((cols[amount_i] as Dictionary).get("w", 0.0)) if with_adjust else 0.0
	# the ground: paper2 held one shade up, the sheet the book is written on
	_tint(b, x, y, w, LG_HEAD_H, Color(PAPER2, 1.0))
	# the amount column's green band starts under the header and is grown by
	# ledger_end once the book knows its own height
	# header band: small-caps labels, the unit said once, a 2.4px rule under
	for i2 in cols_in.size():
		var col: Dictionary = cols[i2]
		var lbl: Label = b.label(String((cols_in[i2] as Dictionary).get("label", "")).to_upper(),
			Vector2(float(col.get("x", 0.0)), y + 8.0), 18, Color(INK, 0.42),
			float(col.get("w", 100.0)) - 8.0)
		if String(col.get("align", "left")) == "right":
			lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	if with_adjust:
		var al: Label = b.label("ADJUST", Vector2(sheet["adjust_x"], y + 8.0), 18,
			Color(INK, 0.42), LG_ADJUST - 6.0)
		al.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	var unit := String(cfg.get("unit", ""))
	if unit != "":
		var ul: Label = b.label(unit, Vector2(x + w - 320.0 - LG_PAD, y + 8.0), 18,
			Color(INK, 0.42), 320.0)
		ul.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	_hrule(b, x, y + LG_HEAD_H - 2.0, w, Color(INK, 1.0), 2.4)
	return sheet

## One book row. `cells` writes into the sheet's columns in order; the amount
## column right-aligns on the band. Editable rows pass on_minus/on_plus and get
## the two SEPARATE squares in the ADJUST column; obligations pass nothing.
## cfg: col (amount color) · dim · on_minus/on_plus · at_min/at_max · on_press
static func ledger_row(b, sheet: Dictionary, cells: Array, cfg: Dictionary = {}) -> float:
	var y := float(sheet.get("cursor", 0.0))
	var x := float(sheet.get("x", 0.0))
	var w := float(sheet.get("w", 0.0))
	var dim := bool(cfg.get("dim", false))
	sheet["row_n"] = int(sheet.get("row_n", 0)) + 1
	b.label(str(sheet["row_n"]), Vector2(x + LG_PAD, y + 10.0), 16, Color(INK, 0.25), LG_ROWNUM - 6.0)
	var cols: Array = sheet.get("cols", [])
	for i in mini(cells.size(), cols.size()):
		var col: Dictionary = cols[i]
		var is_amount := i == int(sheet.get("amount_i", -1))
		var cell_col: Color = cfg.get("col", INK) if is_amount else \
			(Color(INK, 0.6) if (dim or i > 0) else INK)
		if is_amount and dim:
			cell_col = Color(INK, 0.42)
		var lbl: Label = b.label(String(cells[i]), Vector2(float(col.get("x", 0.0)), y + 6.0),
			22 if is_amount else 21, cell_col, float(col.get("w", 100.0)) - 10.0)
		if String(col.get("align", "left")) == "right":
			lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
		elif String(col.get("align", "left")) == "center":
			lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	if bool(sheet.get("adjust", false)) and (cfg.get("on_minus") != null or cfg.get("on_plus") != null):
		adjust_pair(b, float(sheet.get("adjust_x", 0.0)) + (LG_ADJUST - ADJ_BTN * 2.0 - ADJ_GAP) * 0.5,
			y + (LG_ROW_H - ADJ_BTN) * 0.5 - 2.0,
			cfg.get("on_minus", Callable()), cfg.get("on_plus", Callable()),
			bool(cfg.get("at_min", false)), bool(cfg.get("at_max", false)))
	var on_press = cfg.get("on_press")
	if on_press is Callable and (on_press as Callable).is_valid():
		var hit := word(b, "", Vector2(x, y), on_press, DETAIL, INK, w * 0.5)
		hit.size = Vector2(w * 0.5, LG_ROW_H)
	var next := y + LG_ROW_H
	_hrule(b, x, next - 1.0, w, Color(INK, 0.12), 1.6)
	sheet["cursor"] = next
	return next

## A SECTION row: small caps on a kraft wash — "THE FLAT", "CLOSING — sales".
static func ledger_section(b, sheet: Dictionary, label_text: String) -> float:
	var y := float(sheet.get("cursor", 0.0))
	var x := float(sheet.get("x", 0.0))
	var w := float(sheet.get("w", 0.0))
	_tint(b, x, y, w, LG_SEC_H, KRAFT_BAND)
	b.label(label_text.to_upper(), Vector2(x + LG_PAD + LG_ROWNUM, y + 3.0), 18,
		Color(INK, 0.6), w - LG_PAD * 2.0)
	var next := y + LG_SEC_H
	_hrule(b, x, next - 1.0, w, Color(INK, 0.12), 1.6)
	sheet["cursor"] = next
	return next

## THE ACCOUNTING RULES LAW, half one: a SUBTOTAL carries a single rule above
## and its label in the accountant's own smaller hand ("subtotal — the flat").
## note: the effect line the spend book prints on its subtotal rows.
static func ledger_subtotal(b, sheet: Dictionary, label_text: String, amount: String,
		note: String = "") -> float:
	var y := float(sheet.get("cursor", 0.0))
	var x := float(sheet.get("x", 0.0))
	var w := float(sheet.get("w", 0.0))
	_hrule(b, x, y, w, Color(INK, 0.6), 2.0)   # the single rule
	var cols: Array = sheet.get("cols", [])
	var amount_i := int(sheet.get("amount_i", 0))
	var acol: Dictionary = cols[amount_i] if amount_i < cols.size() else {}
	b.label(label_text, Vector2(x + LG_PAD + LG_ROWNUM, y + 7.0), 19, Color(INK, 0.6),
		maxf(float(acol.get("x", x + w * 0.5)) - x - LG_PAD - LG_ROWNUM - 12.0, 80.0))
	var av: Label = b.label(amount, Vector2(float(acol.get("x", 0.0)), y + 5.0), 22, INK,
		float(acol.get("w", 100.0)) - 10.0)
	av.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	if note != "":
		var nx := float(acol.get("x", 0.0)) + float(acol.get("w", 0.0)) \
			+ (LG_ADJUST if bool(sheet.get("adjust", false)) else 0.0) + 10.0
		b.label(note, Vector2(nx, y + 8.0), 18, Color(INK, 0.6), x + w - nx - LG_PAD)
	var next := y + LG_ROW_H
	_hrule(b, x, next - 1.0, w, Color(INK, 0.12), 1.6)
	sheet["cursor"] = next
	return next

## THE ACCOUNTING RULES LAW, half two: the TOTAL sits at the bottom under a
## DOUBLE RULE, on the card tint — the biggest number on the sheet after the
## hero, and it must equal the hero's number.
static func ledger_total(b, sheet: Dictionary, label_text: String, amount: String,
		col: Color = INK) -> float:
	var y := float(sheet.get("cursor", 0.0))
	var x := float(sheet.get("x", 0.0))
	var w := float(sheet.get("w", 0.0))
	_hrule(b, x, y, w, INK, 2.2)
	_hrule(b, x, y + 4.0, w, INK, 2.2)   # the double rule: two pen lines apart
	_tint(b, x, y + 6.0, w, LG_TOT_H - 6.0, CARD_TINT)
	b.label(label_text.to_upper(), Vector2(x + LG_PAD + LG_ROWNUM, y + 12.0), 24, INK,
		w * 0.5)
	var cols: Array = sheet.get("cols", [])
	var amount_i := int(sheet.get("amount_i", 0))
	var acol: Dictionary = cols[amount_i] if amount_i < cols.size() else {}
	var av: Label = b.label(amount, Vector2(float(acol.get("x", 0.0)), y + 8.0), 30, col,
		float(acol.get("w", 120.0)) - 10.0)
	av.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	sheet["cursor"] = y + LG_TOT_H
	return sheet["cursor"]

## An accounting MEMO row — the quiet comparison line under the total
## ("the Monday floor eats 1.9× revenue" · "fully loaded ≈$3,700").
static func ledger_memo(b, sheet: Dictionary, label_text: String, amount: String = "",
		note: String = "") -> float:
	var y := float(sheet.get("cursor", 0.0))
	var x := float(sheet.get("x", 0.0))
	var w := float(sheet.get("w", 0.0))
	_hrule(b, x, y, w, Color(INK, 0.12), 1.6)
	b.label(label_text, Vector2(x + LG_PAD + LG_ROWNUM, y + 8.0), 19, Color(INK, 0.6), w * 0.4)
	var cols: Array = sheet.get("cols", [])
	var amount_i := int(sheet.get("amount_i", 0))
	var acol: Dictionary = cols[amount_i] if amount_i < cols.size() else {}
	if amount != "":
		var av: Label = b.label(amount, Vector2(float(acol.get("x", 0.0)), y + 6.0), 20,
			Color(INK, 0.6), float(acol.get("w", 100.0)) - 10.0)
		av.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	if note != "":
		var nx := float(acol.get("x", 0.0)) + float(acol.get("w", 0.0)) \
			+ (LG_ADJUST if bool(sheet.get("adjust", false)) else 0.0) + 10.0
		b.label(note, Vector2(nx, y + 8.0), 18, Color(INK, 0.6), x + w - nx - LG_PAD)
	sheet["cursor"] = y + LG_ROW_H
	return sheet["cursor"]

## Close the book: the green band down the amount column, the vertical column
## rules, and the outer wobbled border round everything written. Returns the y
## the page continues at.
static func ledger_end(b, sheet: Dictionary) -> float:
	var x := float(sheet.get("x", 0.0))
	var y := float(sheet.get("y", 0.0))
	var w := float(sheet.get("w", 0.0))
	var h := float(sheet.get("cursor", y)) - y
	var cols: Array = sheet.get("cols", [])
	var amount_i := int(sheet.get("amount_i", 0))
	if amount_i < cols.size():
		var acol: Dictionary = cols[amount_i]
		var band := _tint(b, float(acol.get("x", 0.0)) - 6.0, y + LG_HEAD_H,
			float(acol.get("w", 100.0)) + 2.0, h - LG_HEAD_H, SAGE_BAND)
		band.show_behind_parent = true
		band.z_index = -1
	# fine vertical rules between columns (the paper book-keeping grammar)
	for i in cols.size():
		if i == 0:
			continue
		var col: Dictionary = cols[i]
		_vrule(b, float(col.get("x", 0.0)) - 8.0, y + LG_HEAD_H, h - LG_HEAD_H,
			Color(INK, 0.12), 1.6)
	var box := _SheetEdge.new()
	box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	box.position = Vector2(x, y)
	box.set_deferred("size", Vector2(w, h))
	b.pane().add_child(box)
	return y + h + 14.0

## THE STEPPER LAW (owner, DECISIONS): two SEPARATE drawn squares — − and +,
## each its own wobbly box, a visible gap between them — never a joined chip.
## The amount column stays pure right-aligned numerals; these live beside it.
static func adjust_pair(b, x: float, y: float, on_minus, on_plus,
		at_min: bool = false, at_max: bool = false) -> void:
	_adj_btn(b, "−", Vector2(x, y), at_min, on_minus if on_minus != null else Callable())
	_adj_btn(b, "+", Vector2(x + ADJ_BTN + ADJ_GAP, y), at_max,
		on_plus if on_plus != null else Callable())

static func _adj_btn(b, glyph: String, pos: Vector2, dead: bool, on_press) -> void:
	var btn := Button.new()
	btn.text = glyph
	btn.flat = true
	btn.position = pos
	btn.size = Vector2(ADJ_BTN, ADJ_BTN)
	btn.add_theme_font_override("font", b.font())
	btn.add_theme_font_size_override("font_size", 20)
	btn.add_theme_color_override("font_color", Color(INK, 0.35) if dead else INK)
	btn.add_theme_color_override("font_hover_color", Color(INK, 0.35) if dead else PEN)
	for stn in ["normal", "hover", "pressed", "focus"]:
		btn.add_theme_stylebox_override(stn, StyleBoxEmpty.new())
	var box := _AdjBox.new()
	box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	box.show_behind_parent = true
	box.set_deferred("size", Vector2(ADJ_BTN, ADJ_BTN))
	btn.add_child(box)
	if dead:
		btn.disabled = true
		btn.add_theme_color_override("font_disabled_color", Color(INK, 0.35))
	elif on_press is Callable and (on_press as Callable).is_valid():
		var cb := on_press as Callable
		btn.pressed.connect(func() -> void:
			b.desk.erase("armed")
			cb.call()
			b.refresh())
	b.pane().add_child(btn)

# ─────────────────────────── THE NUMBERED ZONE ───────────────────────────────
## THE DIDACTIC SPINE (DECISIONS: the bank's Meeting, promoted binder-wide for
## concept-heavy desks — the works, cap table, pivot, the offer). A numbered
## badge, a small-caps title, and the zone's one-line LESSON written into the
## header — read top to bottom like an appointment at the branch.
## Returns a frame like card_frame: {content_x, content_y, cursor, money_x,
## bottom} — rows inside compose against it.
static func zone(b, x: float, y: float, w: float, h: float, num: int, title_text: String,
		lesson: String) -> Dictionary:
	var box := _ZoneBox.new()
	box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	box.position = Vector2(x, y)
	box.set_deferred("size", Vector2(w, h))
	b.pane().add_child(box)
	var badge := _Badge.new()
	badge.num = num
	badge.font = b.font()
	badge.mouse_filter = Control.MOUSE_FILTER_IGNORE
	badge.position = Vector2(x + 12.0, y + 10.0)
	badge.set_deferred("size", Vector2(34.0, 34.0))
	b.pane().add_child(badge)
	b.label(title_text.to_upper(), Vector2(x + 58.0, y + 12.0), 24, INK, w - 70.0)
	if lesson != "":
		b.label(lesson, Vector2(x + 58.0, y + 44.0), LAW, Color(INK, 0.6), w - 70.0)
	var cy := y + (78.0 if lesson != "" else 52.0)
	return {"content_x": x + CARD_PAD, "content_y": cy, "cursor": cy,
		"money_x": x + w - CARD_PAD, "bottom": y + h, "x": x, "y": y, "w": w, "h": h}

# ───────────────────────────── THE KANBAN WALL ───────────────────────────────
## WHAT WE MAKE's pipeline wall (DECISIONS: style 5): columns with a kraft
## header and a one-line meaning, cards with one anatomy everywhere. The READY
## variant wears the alarm red; a card may carry a progress fill.
static func wall_column(b, x: float, y: float, w: float, h: float, head: String,
		sub: String) -> Dictionary:
	_tint(b, x, y, w, 54.0, Color(KRAFT, 0.45))
	var box := _ZoneBox.new()
	box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	box.position = Vector2(x, y)
	box.set_deferred("size", Vector2(w, h))
	b.pane().add_child(box)
	b.label(head.to_upper(), Vector2(x + 10.0, y + 4.0), 22, INK, w - 20.0)
	if sub != "":
		b.label(sub, Vector2(x + 10.0, y + 30.0), 17, Color(INK, 0.6), w - 20.0)
	return {"x": x, "y": y, "w": w, "h": h, "cursor": y + 62.0, "content_x": x + 8.0}

## One wall card. cfg: title · facts (Array[String], the $/weeks/odds line and
## friends) · progress (0-1, -1 for none) · ready (red READY variant) ·
## sev (0-3 solidity/attention dot) · on_press
static func wall_card(b, col_frame: Dictionary, cfg: Dictionary) -> float:
	var x := float(col_frame.get("content_x", 0.0))
	var y := float(col_frame.get("cursor", 0.0))
	var w := float(col_frame.get("w", 200.0)) - 16.0
	var facts: Array = cfg.get("facts", [])
	var progress := float(cfg.get("progress", -1.0))
	var h := 40.0 + float(facts.size()) * 24.0 + (16.0 if progress >= 0.0 else 0.0)
	var ship_ready := bool(cfg.get("ready", false))
	var card := _WallCard.new()
	card.ship_ready = ship_ready
	card.lean = int(x + y) % 5
	card.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.position = Vector2(x, y)
	card.set_deferred("size", Vector2(w, h))
	b.pane().add_child(card)
	var sev := int(cfg.get("sev", 0))
	if sev > 0:
		sev_dot(b, x + w - SEV_BOX - 4.0, y + 6.0, sev)
	b.label(String(cfg.get("title", "")), Vector2(x + 10.0, y + 6.0), 22,
		Color.WHITE if ship_ready else INK, w - (SEV_BOX + 22.0 if sev > 0 else 20.0))
	var fy := y + 36.0
	for f in facts:
		b.label(String(f), Vector2(x + 10.0, fy), 17,
			Color(1, 1, 1, 0.85) if ship_ready else Color(INK, 0.65), w - 20.0)
		fy += 24.0
	if progress >= 0.0:
		meter(b, x + 10.0, fy + 2.0, w - 20.0, progress, SAGE, "")
	var on_press = cfg.get("on_press")
	if on_press is Callable and (on_press as Callable).is_valid():
		var hit := word(b, "", Vector2(x, y), on_press, DETAIL, INK, w)
		hit.size = Vector2(w, h)
	col_frame["cursor"] = y + h + 10.0
	return col_frame["cursor"]

# ──────────────────────────── THE TICKET / RECEIPT ───────────────────────────
## The priced slip of paper (the unit ticket, the pre-move receipt, the bank's
## quote): dashed rules head and foot, priced lines in the money column, the
## price line under a DOUBLE rule. cfg: title · lines Array[{label, value,
## col}] · total_label · total_value · total_col · foot
static func ticket(b, x: float, y: float, w: float, cfg: Dictionary) -> float:
	var lines: Array = cfg.get("lines", [])
	var has_total := String(cfg.get("total_value", "")) != ""
	var foot := String(cfg.get("foot", ""))
	var h := 46.0 + float(lines.size()) * 32.0 + (44.0 if has_total else 8.0) \
		+ (30.0 if foot != "" else 0.0) + 14.0
	_tint(b, x, y, w, h, Color("FBF6E8"))
	var box := _ZoneBox.new()
	box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	box.position = Vector2(x, y)
	box.set_deferred("size", Vector2(w, h))
	b.pane().add_child(box)
	_dashrule(b, x + 10.0, y + 34.0, w - 20.0)
	b.label(String(cfg.get("title", "")).to_upper(), Vector2(x + 14.0, y + 6.0), 20,
		Color(INK, 0.6), w - 28.0)
	var ly := y + 44.0
	for ln in lines:
		var d: Dictionary = ln
		b.label(String(d.get("label", "")), Vector2(x + 14.0, ly), 21,
			Color(INK, 0.85), w * 0.6)
		var v: Label = b.label(String(d.get("value", "")), Vector2(x + 14.0, ly), 21,
			d.get("col", INK), w - 28.0)
		v.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
		ly += 32.0
	if has_total:
		_hrule(b, x + 10.0, ly + 2.0, w - 20.0, INK, 2.0)
		_hrule(b, x + 10.0, ly + 6.0, w - 20.0, INK, 2.0)
		b.label(String(cfg.get("total_label", "the price")), Vector2(x + 14.0, ly + 12.0),
			22, INK, w * 0.6)
		var tv: Label = b.label(String(cfg.get("total_value", "")),
			Vector2(x + 14.0, ly + 10.0), 26, cfg.get("total_col", PEN), w - 28.0)
		tv.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
		ly += 44.0
	if foot != "":
		b.label(foot, Vector2(x + 14.0, ly + 2.0), 17, Color(INK, 0.5), w - 28.0)
		ly += 30.0
	_dashrule(b, x + 10.0, ly + 6.0, w - 20.0)
	return y + h + 14.0

# ─────────────────────────── OWNERSHIP INSTRUMENTS ───────────────────────────
## CAP BARS: the holders as horizontal share bars — label, the drawn bar, the
## percent, a note (invested/preferences). rows: Array[{label, pct, col, note}]
static func capbars(b, x: float, y: float, w: float, rows: Array) -> float:
	var track := w - 260.0 - 210.0
	for r in rows:
		var d: Dictionary = r
		var pct := clampf(float(d.get("pct", 0.0)), 0.0, 100.0)
		b.label(String(d.get("label", "")), Vector2(x, y), DETAIL, INK, 250.0)
		var bar := _Bar.new()
		bar.col = d.get("col", SAGE)
		bar.seed_i = int(y)
		bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
		bar.position = Vector2(x + 260.0, y + 2.0)
		bar.set_deferred("size", Vector2(maxf(track * pct / 100.0, 8.0), 24.0))
		b.pane().add_child(bar)
		b.label("%.1f%%" % pct, Vector2(x + 260.0 + maxf(track * pct / 100.0, 8.0) + 10.0,
			y), DETAIL, Color(INK, 0.85), 90.0)
		var note := String(d.get("note", ""))
		if note != "":
			b.label(note, Vector2(x + w - 200.0, y + 2.0), 17, Color(INK, 0.5), 200.0)
		y += 40.0
	return y + 6.0

## THE DILUTION STORY: the shrinking-bar timeline — each ownership event a
## vertical bar of your slice, % down but paper value up (the core lesson).
## steps: Array[{label, pct, note}]
static func dilution_bar(b, x: float, y: float, w: float, steps: Array) -> float:
	var n := steps.size()
	if n == 0:
		return y
	var cell := minf(w / float(n), 190.0)
	var bar_h := 120.0
	for i in n:
		var d: Dictionary = steps[i]
		var pct := clampf(float(d.get("pct", 0.0)), 0.0, 100.0)
		var cx := x + float(i) * cell
		var fill_h := bar_h * pct / 100.0
		var m := _Meter.new()
		m.frac = 1.0
		m.col = SAGE if i == 0 else BLUE
		m.mouse_filter = Control.MOUSE_FILTER_IGNORE
		m.position = Vector2(cx + 20.0, y + bar_h - fill_h)
		m.set_deferred("size", Vector2(46.0, fill_h))
		b.pane().add_child(m)
		b.label("%.0f%%" % pct, Vector2(cx + 74.0, y + bar_h - fill_h - 4.0), 19,
			INK, cell - 78.0)
		b.label(String(d.get("label", "")), Vector2(cx + 4.0, y + bar_h + 8.0), 17,
			Color(INK, 0.75), cell - 10.0)
		var note := String(d.get("note", ""))
		if note != "":
			b.label(note, Vector2(cx + 4.0, y + bar_h + 32.0), 15, Color(INK, 0.5),
				cell - 10.0)
	return y + bar_h + 64.0

# ───────────────────────────── THE RUNG-3 FACES ──────────────────────────────
## HERO PLATE: the version/name plate — what the thing IS, in one drawn plate.
static func hero_plate(b, x: float, y: float, name_text: String, version: String,
		note: String = "") -> float:
	var w := 420.0
	_tint(b, x, y, w, 78.0, Color(PAPER2, 1.0))
	var box := _ZoneBox.new()
	box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	box.position = Vector2(x, y)
	box.set_deferred("size", Vector2(w, 78.0))
	b.pane().add_child(box)
	b.label(name_text, Vector2(x + 16.0, y + 6.0), 30, INK, w - 130.0)
	if version != "":
		b.label(version, Vector2(x + w - 110.0, y + 10.0), 26, PEN, 100.0)
	if note != "":
		b.label(note, Vector2(x + 16.0, y + 46.0), 17, Color(INK, 0.6), w - 32.0)
	return y + 92.0

## HERO ROW (rung-3 read face, DECISIONS default B): one calm row per product/
## site/unit — name, the fact line, the money at the row's end, the red state,
## press opens the thing's own page. cfg: name · facts · value · sev · on_press
static func hero_row(b, y: float, cfg: Dictionary) -> float:
	var x := X_ID
	var w := 1120.0
	var sev := int(cfg.get("sev", 0))
	if sev > 0:
		sev_dot(b, x, y + 10.0, sev)
	var nx := x + (SEV_BOX + 10.0 if sev > 0 else 0.0)
	b.label(String(cfg.get("name", "")), Vector2(nx, y), ROW, INK, 380.0)
	b.label(String(cfg.get("facts", "")), Vector2(x + 420.0, y + 4.0), DETAIL,
		Color(INK, 0.65), 480.0)
	var v: Label = b.label(String(cfg.get("value", "")), Vector2(x + 900.0, y), ROW,
		cfg.get("col", INK), 220.0)
	v.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	var on_press = cfg.get("on_press")
	if on_press is Callable and (on_press as Callable).is_valid():
		var hit := word(b, "", Vector2(x, y - 4.0), on_press, DETAIL, INK, 880.0)
		hit.size = Vector2(880.0, 44.0)
	pen_rule(b, y + 44.0, x, w, Color(INK, 0.14), int(y) % 23)
	return y + 58.0

## FOLDER: the kraft folder face — a labeled folder with its count, pressable.
static func folder(b, x: float, y: float, w: float, title_text: String,
		count_note: String, on_press: Callable = Callable()) -> float:
	var h := 96.0
	var f := _Folder.new()
	f.mouse_filter = Control.MOUSE_FILTER_IGNORE
	f.position = Vector2(x, y)
	f.set_deferred("size", Vector2(w, h))
	b.pane().add_child(f)
	b.label(title_text, Vector2(x + 16.0, y + 26.0), ROW, INK, w - 32.0)
	if count_note != "":
		b.label(count_note, Vector2(x + 16.0, y + 60.0), 17, Color(INK, 0.6), w - 32.0)
	if on_press.is_valid():
		var hit := word(b, "", Vector2(x, y), on_press, DETAIL, INK, w)
		hit.size = Vector2(w, h)
	return y + h + 14.0

# ─────────────────────────────── CHIPS & FOLDS ───────────────────────────────
## A CHIP — the arrange mode's movable element (person / machine / spend-line),
## and any small named token. Selected chips wear the pen ring. Returns the x
## the next chip may start at (chips flow horizontally).
static func chip(b, x: float, y: float, cfg: Dictionary) -> float:
	var text := String(cfg.get("text", ""))
	var kind := String(cfg.get("kind", "person"))
	# the kind's mark stays inside the hand font's own glyphs — "$" leads a
	# spend line; people and machines are their names (the box's seed differs)
	var full := ("$ " + text) if kind == "spend" else text
	var tw: float = b.font().get_string_size(full, HORIZONTAL_ALIGNMENT_LEFT, -1, 19).x
	var w := tw + 26.0
	var selected := bool(cfg.get("selected", false))
	var box := _ChipBox.new()
	box.selected = selected
	box.kind = kind
	box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	box.position = Vector2(x, y)
	box.set_deferred("size", Vector2(w, 34.0))
	b.pane().add_child(box)
	var lbl: Label = b.label(full, Vector2(x + 13.0, y + 3.0), 19,
		PEN if selected else INK, tw + 8.0)
	lbl.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var on_press = cfg.get("on_press")
	if on_press is Callable and (on_press as Callable).is_valid():
		var hit := word(b, "", Vector2(x, y), on_press, DETAIL, INK, w)
		hit.size = Vector2(w, 34.0)
	return x + w + 10.0

## THE ARRANGE BIN — a labeled container chips move into (a site, SHARED/HQ,
## or the dashed "+ new" ghost). cfg: title · note · ghost · closing · on_press
## Returns the bin frame {content_x, cursor, x, y, w, h}.
static func bin(b, x: float, y: float, w: float, h: float, cfg: Dictionary) -> Dictionary:
	var box := _BinBox.new()
	box.ghost = bool(cfg.get("ghost", false))
	box.closing = bool(cfg.get("closing", false))
	box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	box.position = Vector2(x, y)
	box.set_deferred("size", Vector2(w, h))
	b.pane().add_child(box)
	if not bool(cfg.get("ghost", false)):
		b.label(String(cfg.get("title", "")).to_upper(), Vector2(x + 12.0, y + 6.0), 21,
			INK, w - 24.0)
		var note := String(cfg.get("note", ""))
		if note != "":
			b.label(note, Vector2(x + 12.0, y + 34.0), 15, Color(INK, 0.5), w - 24.0)
	else:
		var gl: Label = b.label("+ new\n(a priced door)", Vector2(x + 8.0, y + h * 0.5 - 30.0),
			19, Color(INK, 0.6), w - 16.0)
		gl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	var on_press = cfg.get("on_press")
	if on_press is Callable and (on_press as Callable).is_valid():
		var hit := word(b, "", Vector2(x, y), on_press, DETAIL, INK, w)
		hit.size = Vector2(w, h)
	return {"x": x, "y": y, "w": w, "h": h, "content_x": x + 12.0, "cursor": y + 58.0}

## THE FOLD ROW — the collapse ladder's honest tail: a dashed row that says
## "the other N ▸" and opens the crowd. Items closest to money are never the
## hidden ones; this row is where the healthy crowd sleeps.
static func fold_row(b, x: float, y: float, n: int, label_text: String,
		on_press: Callable = Callable()) -> float:
	if n <= 0:
		return y
	_dashrule(b, x, y + 20.0, 1120.0 * 0.35)
	var text := "the other %d %s" % [n, label_text]
	if on_press.is_valid():
		word(b, text + "  →", Vector2(x + 1120.0 * 0.36, y - 2.0), on_press, DETAIL,
			Color(INK, 0.6), 420.0)
	else:
		b.label(text, Vector2(x + 1120.0 * 0.36, y + 4.0), DETAIL, Color(INK, 0.5), 420.0)
	_dashrule(b, x + 1120.0 * 0.36 + 440.0, y + 20.0, 1120.0 - (1120.0 * 0.36 + 440.0))
	return y + 44.0

## THE DEADLINE CLOCK CHIP — alert-red, white words, the drawn clock face at
## its head (the momentary tab's clock, team's "3 waiting"). Returns end x.
static func clock_chip(b, x: float, y: float, text: String) -> float:
	var tw: float = b.font().get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, 17).x
	var w := tw + 20.0
	var box := _ClockChip.new()
	box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	box.position = Vector2(x, y)
	box.set_deferred("size", Vector2(w, 28.0))
	b.pane().add_child(box)
	var lbl: Label = b.label(text, Vector2(x + 10.0, y + 1.0), 17, Color.WHITE, tw + 8.0)
	lbl.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return x + w + 8.0

# ───────────────────────── the desk-stub furniture ───────────────────────────
## THE QUESTION LINE (DAG2 W1): every desk stub renders its hero question so
## navigation is testable before the lanes land. On a page that embeds a
## shipped desk the question rides the sheet's quiet bottom-right corner —
## below the old pane's 760, inside the new 880 — so nothing collides.
static func hero_question(b, q: String) -> void:
	var l: Label = b.label(q, Vector2(560.0, 846.0), LAW, Color(INK, 0.4), 560.0)
	l.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT

## A page that exists but is not built yet: the hero question AS the hero,
## and an honest pen note — never a blank sheet, never "coming soon" chrome.
static func under_construction(b, big: String, question: String, note: String) -> float:
	var y := hero_band(b, big, question, INK, 6.0, false)
	y += 8.0
	b.label("· " + note, Vector2(X_ID + 20.0, y), STATUS, Color(INK, 0.6), 1060.0)
	y += 60.0
	b.label("this desk is on the drafting table — its numbers land with the next wave",
		Vector2(X_ID + 20.0, y), LAW, Color(INK, 0.4), 1060.0)
	return y + 40.0

# ── the small drawn helpers the v2 primitives compose from ───────────────────

static func _tint(b, x: float, y: float, w: float, h: float, col: Color) -> Control:
	var t := ColorRect.new()
	t.color = col
	t.mouse_filter = Control.MOUSE_FILTER_IGNORE
	t.position = Vector2(x, y)
	t.set_deferred("size", Vector2(w, h))
	b.pane().add_child(t)
	return t

static func _hrule(b, x: float, y: float, w: float, col: Color, thick: float) -> void:
	var r := _FlatRule.new()
	r.col = col
	r.thick = thick
	r.mouse_filter = Control.MOUSE_FILTER_IGNORE
	r.position = Vector2(x, y)
	r.set_deferred("size", Vector2(w, thick + 2.0))
	b.pane().add_child(r)

static func _vrule(b, x: float, y: float, h: float, col: Color, thick: float) -> void:
	var r := _FlatVRule.new()
	r.col = col
	r.thick = thick
	r.mouse_filter = Control.MOUSE_FILTER_IGNORE
	r.position = Vector2(x, y)
	r.set_deferred("size", Vector2(thick + 2.0, h))
	b.pane().add_child(r)

static func _dashrule(b, x: float, y: float, w: float) -> void:
	var r := _DashRule.new()
	r.mouse_filter = Control.MOUSE_FILTER_IGNORE
	r.position = Vector2(x, y)
	r.set_deferred("size", Vector2(w, 4.0))
	b.pane().add_child(r)

# ─────────────────────────────── drawn pieces ────────────────────────────────

class _Rule:
	extends Control
	var w := 1120.0
	func _draw() -> void:
		draw_line(Vector2(0, 1), Vector2(w, 1), Color(DeskKit.INK, 0.25), 2.0)

class _VRule:
	extends Control
	var h := 300.0
	func _draw() -> void:
		draw_line(Vector2(1, 0), Vector2(1, h), Color(DeskKit.INK, 0.25), 2.0)

## The expand mark, drawn: a filled ink triangle pointing the way in.
class _Tri:
	extends Control
	var col := DeskKit.INK
	func _draw() -> void:
		draw_colored_polygon(PackedVector2Array([Vector2(4, 2), Vector2(20, 12),
			Vector2(4, 22)]), col)

## The commit stroke: one underline in the founder's pen, left to right.
class _Stroke:
	extends Control
	var progress := 0.0
	func _draw() -> void:
		if progress <= 0.02:
			return
		var pts := PackedVector2Array()
		var rng := RandomNumberGenerator.new()
		rng.seed = 23
		var n: int = maxi(int(progress * 24.0), 2)
		for i in 24:
			var jitter := rng.randf_range(-1.4, 1.4)
			if i < n:
				pts.append(Vector2(size.x * float(i) / 23.0, 5.0 + jitter))
		draw_polyline(pts, DeskKit.PEN, 4.0, true)

## Five inked boxes, the filled ones coral. A bare coral square is a UI element;
## a bordered one is a box somebody filled in.
class _Pips:
	extends Control
	var filled := 0
	var total := 5
	func _draw() -> void:
		for i in total:
			var x := float(i) * 21.0
			var on := i < filled
			var r := Rect2(x, 0, 17, 13)
			draw_rect(r, Color(DeskKit.PEN, 0.85) if on else Color(DeskKit.INK, 0.06))
			draw_rect(r, DeskKit.INK if on else Color(DeskKit.INK, 0.32), false,
				2.5 if on else 2.0)

## One pen-stroke bar: a tinted fill under a seeded, wobbled ink outline.
class _Bar:
	extends Control
	var col := DeskKit.BLUE
	var seed_i := 3
	func _draw() -> void:
		draw_rect(Rect2(0, 0, size.x, size.y), Color(col, 0.6))
		var rng := RandomNumberGenerator.new()
		rng.seed = seed_i
		var pts := PackedVector2Array()
		var corners := [Vector2(1, 1), Vector2(size.x - 1, 1),
			Vector2(size.x - 1, size.y - 1), Vector2(1, size.y - 1)]
		for i in 4:
			var a: Vector2 = corners[i]
			var bb: Vector2 = corners[(i + 1) % 4]
			for k in 6:
				pts.append(a.lerp(bb, float(k) / 6.0)
					+ Vector2(rng.randf_range(-1.0, 1.0), rng.randf_range(-1.0, 1.0)))
		pts.append(pts[0])
		draw_polyline(pts, DeskKit.INK, 2.5, true)

## A RULE WITH A HAND IN IT: the same 2px weight the straight rule carries, drawn
## as a stroke that wanders a pixel — the difference between a page ruled with a
## pen and a page ruled with a ruler.
class _PenLine:
	extends Control
	var w := 1120.0
	var col := Color(DeskKit.INK, 0.25)
	var seed_i := 5
	func _draw() -> void:
		var rng := RandomNumberGenerator.new()
		rng.seed = seed_i
		var pts := PackedVector2Array()
		for i in 19:
			pts.append(Vector2(w * float(i) / 18.0, 3.0 + rng.randf_range(-1.1, 1.1)))
		draw_polyline(pts, col, 2.0, true)

## A PAPER CARD ON THE CLIPBOARD — the draft-card hand (shadow (7,9)@0.18, an ink
## edge walked in 13 steps with 2.1 of jitter), on cream held one shade up to the
## light. The lift is what makes a card read as a piece of paper LYING on the
## board; the shadow is what makes it read as lying on TOP of it. Take either
## away and the card becomes a box drawn on the sheet.
##
## SEEDED BY THE CARD'S OWN COLUMN: neighbours in a grid wobble differently
## (`lean`), so the eye sees two pieces of paper cut by one pair of scissors
## rather than one card stamped twice.
class _Card:
	extends Control
	var lean := 0
	func _draw() -> void:
		var w := size.x
		var h := size.y
		var inset := 4.5
		var rng := RandomNumberGenerator.new()
		rng.seed = 17 + lean
		var ring := PackedVector2Array()
		var corners := [Vector2(inset, inset), Vector2(w - inset, inset),
			Vector2(w - inset, h - inset), Vector2(inset, h - inset)]
		for i in 4:
			var a: Vector2 = corners[i]
			var bb: Vector2 = corners[(i + 1) % 4]
			for k in 13:
				ring.append(a.lerp(bb, float(k) / 13.0)
					+ Vector2(rng.randf_range(-2.1, 2.1), rng.randf_range(-2.1, 2.1)))
		# THE PAPER IS THE WOBBLE, not a square hiding behind one. A rectangular
		# fill under a hand-drawn edge pokes its four right angles out through the
		# stroke, and a card with four right angles is a box drawn on the sheet
		# rather than a piece of paper lying on it.
		draw_rect(Rect2(7, 9, w, h), Color(0, 0, 0, 0.18))
		draw_colored_polygon(ring, DeskKit.CREAM)
		draw_colored_polygon(ring, Color(1, 1, 1, 0.07))
		var pts := PackedVector2Array(ring)
		pts.append(ring[0])
		draw_polyline(pts, DeskKit.INK, 3.0, true)

## THE DRAWN FILL: a faint ground for what is not there yet, a tinted wash for
## what is, and one seeded ink outline round the WHOLE track — without the
## outline the level floats and the meter is not a meter (the jar's own lesson).
class _Meter:
	extends Control
	var frac := 0.0
	var col := DeskKit.SAGE
	func _draw() -> void:
		var w := size.x
		var h := size.y
		draw_rect(Rect2(0, 0, w, h), Color(DeskKit.INK, 0.06))
		if frac > 0.0:
			draw_rect(Rect2(0, 0, w * frac, h), Color(col, 0.6))
		var rng := RandomNumberGenerator.new()
		rng.seed = 19
		var pts := PackedVector2Array()
		var corners := [Vector2(1, 1), Vector2(w - 1, 1), Vector2(w - 1, h - 1),
			Vector2(1, h - 1)]
		for i in 4:
			var a: Vector2 = corners[i]
			var bb: Vector2 = corners[(i + 1) % 4]
			for k in 8:
				pts.append(a.lerp(bb, float(k) / 8.0)
					+ Vector2(rng.randf_range(-0.9, 0.9), rng.randf_range(-0.9, 0.9)))
		pts.append(pts[0])
		draw_polyline(pts, DeskKit.INK, 2.5, true)

## FOUR NARROWING MOUTHS. Each stage is a trapezoid whose top is the stage above
## it and whose bottom is the stage below: the drawing is the arithmetic, and a
## funnel that stops narrowing is a funnel that stopped working. A fogged stage
## keeps its mouth and loses its wash — the shape of the thing you cannot see yet
## is itself the invitation to buy the eyesight.
class _Funnel:
	extends Control
	var stages: Array = []
	func _draw() -> void:
		var w := size.x
		# the funnel's own ramp, top to bottom: the world's blue, the heat of a
		# lead, then the sage of a customer who actually arrived
		var ramp := [DeskKit.BLUE, DeskKit.YELL, DeskKit.SAGE, DeskKit.SAGE]
		for i in stages.size():
			var st: Dictionary = stages[i]
			var known := bool(st.get("known", true))
			var col: Color = st.get("col", ramp[mini(i, 3)])
			var top := w * (1.0 - DeskKit.FUNNEL_NARROW * float(i))
			var bot := w * (1.0 - DeskKit.FUNNEL_NARROW * float(i + 1))
			var y0 := float(i) * (DeskKit.FUNNEL_H + DeskKit.FUNNEL_GAP)
			var y1 := y0 + DeskKit.FUNNEL_H
			var quad := PackedVector2Array([
				Vector2((w - top) * 0.5, y0), Vector2((w + top) * 0.5, y0),
				Vector2((w + bot) * 0.5, y1), Vector2((w - bot) * 0.5, y1)])
			if known:
				draw_colored_polygon(quad, Color(col, 0.5))
			var rng := RandomNumberGenerator.new()
			rng.seed = 41 + i * 3
			var pts := PackedVector2Array()
			for k in 4:
				var a: Vector2 = quad[k]
				var bb: Vector2 = quad[(k + 1) % 4]
				for s in 7:
					pts.append(a.lerp(bb, float(s) / 7.0)
						+ Vector2(rng.randf_range(-1.4, 1.4), rng.randf_range(-1.4, 1.4)))
			pts.append(pts[0])
			draw_polyline(pts, DeskKit.INK if known else Color(DeskKit.INK, 0.35),
				3.0 if known else 2.0, true)

## THE ATTENTION DOT: a blot of the founder's pen, wobbled like everything else
## the hand puts down. An alarm is the same ink, simply bigger, with a loop drawn
## round it — bigger reads across a page where a colour change does not, and the
## page's one pulse is spent elsewhere.
class _Dot:
	extends Control
	var severity := 2
	func _draw() -> void:
		var c := Vector2(DeskKit.SEV_BOX, DeskKit.SEV_BOX) * 0.5
		var r := 7.0 if severity == 1 else (8.0 if severity == 2 else 11.0)
		var col: Color = Color(DeskKit.INK, 0.5) if severity == 1 else DeskKit.PEN
		var rng := RandomNumberGenerator.new()
		rng.seed = 27
		var pts := PackedVector2Array()
		for i in 17:
			var t := TAU * float(i) / 16.0
			pts.append(c + Vector2(cos(t), sin(t)) * (r + rng.randf_range(-0.7, 0.7)))
		draw_colored_polygon(pts, col)
		if severity >= 3:
			var ring := PackedVector2Array()
			for i in 19:
				var t2 := TAU * float(i) / 18.0
				ring.append(c + Vector2(cos(t2), sin(t2))
					* (r + 4.0 + rng.randf_range(-0.8, 0.8)))
			draw_polyline(ring, Color(DeskKit.PEN, 0.55), 2.0, true)

## THE BREATH, quantized to 12fps (§1.6.1): a desk's WAIT line pulses its alpha
## between 0.45 and 0.75 on the hand's own clock. Nothing pulses smoothly;
## smooth is chrome.
class _Breath:
	extends Node
	const BREATH_FPS := 12.0
	var target: Control
	func _process(_dt: float) -> void:
		if target == null or not is_instance_valid(target):
			return
		var t := floorf(Time.get_ticks_msec() / 1000.0 * BREATH_FPS) / BREATH_FPS
		target.modulate.a = 0.6 + 0.15 * sin(t * 3.0)

# ─────────────────────── the v2 primitives' drawn pieces ─────────────────────

## A dead-straight rule at any weight — the accounting book rules itself with
## a ruler, which is exactly what separates it from the pen-ruled cards.
class _FlatRule:
	extends Control
	var col := Color(DeskKit.INK, 0.25)
	var thick := 2.0
	func _draw() -> void:
		draw_line(Vector2(0, 1), Vector2(size.x, 1), col, thick)

class _FlatVRule:
	extends Control
	var col := Color(DeskKit.INK, 0.25)
	var thick := 2.0
	func _draw() -> void:
		draw_line(Vector2(1, 0), Vector2(1, size.y), col, thick)

## The receipt's dashed rule — torn-off paper, head and foot.
class _DashRule:
	extends Control
	func _draw() -> void:
		var x := 0.0
		while x < size.x:
			draw_line(Vector2(x, 1), Vector2(minf(x + 9.0, size.x), 1),
				Color(DeskKit.INK, 0.45), 2.0)
			x += 16.0

## The ledger sheet's outer edge: a near-straight ink border (2.6px, a hair of
## wobble) — a book, not a card, so it wobbles less than the scissors-cut paper.
class _SheetEdge:
	extends Control
	func _draw() -> void:
		var rng := RandomNumberGenerator.new()
		rng.seed = 31
		var pts := PackedVector2Array()
		var corners := [Vector2(1, 1), Vector2(size.x - 1, 1),
			Vector2(size.x - 1, size.y - 1), Vector2(1, size.y - 1)]
		for i in 4:
			var a: Vector2 = corners[i]
			var bb: Vector2 = corners[(i + 1) % 4]
			for k in 16:
				pts.append(a.lerp(bb, float(k) / 16.0)
					+ Vector2(rng.randf_range(-0.8, 0.8), rng.randf_range(-0.8, 0.8)))
		pts.append(pts[0])
		draw_polyline(pts, DeskKit.INK, 2.6, true)

## One ADJUST square: paper2 under a wobbled ink edge with the small thrown
## shadow — visibly a separate drawn button, never half of a joined chip.
class _AdjBox:
	extends Control
	func _draw() -> void:
		draw_rect(Rect2(2, 2, size.x, size.y), Color(0, 0, 0, 0.2))
		var rng := RandomNumberGenerator.new()
		rng.seed = 37
		var pts := PackedVector2Array()
		var corners := [Vector2(1, 1), Vector2(size.x - 1, 1),
			Vector2(size.x - 1, size.y - 1), Vector2(1, size.y - 1)]
		for i in 4:
			var a: Vector2 = corners[i]
			var bb: Vector2 = corners[(i + 1) % 4]
			for k in 5:
				pts.append(a.lerp(bb, float(k) / 5.0)
					+ Vector2(rng.randf_range(-0.8, 0.8), rng.randf_range(-0.8, 0.8)))
		draw_colored_polygon(pts, DeskKit.PAPER2)
		pts.append(pts[0])
		draw_polyline(pts, DeskKit.INK, 2.4, true)

## A zone/column/ticket border: straight-ish 2.6px ink box, rounded by wobble.
class _ZoneBox:
	extends Control
	func _draw() -> void:
		var rng := RandomNumberGenerator.new()
		rng.seed = 43 + int(position.x) % 7
		var pts := PackedVector2Array()
		var corners := [Vector2(1, 1), Vector2(size.x - 1, 1),
			Vector2(size.x - 1, size.y - 1), Vector2(1, size.y - 1)]
		for i in 4:
			var a: Vector2 = corners[i]
			var bb: Vector2 = corners[(i + 1) % 4]
			for k in 14:
				pts.append(a.lerp(bb, float(k) / 14.0)
					+ Vector2(rng.randf_range(-1.2, 1.2), rng.randf_range(-1.2, 1.2)))
		pts.append(pts[0])
		draw_polyline(pts, DeskKit.INK, 2.6, true)

## The numbered didactic badge: a drawn circle with the number inside — the
## Meeting's "read me top to bottom" spine, promoted binder-wide.
class _Badge:
	extends Control
	var num := 1
	var font: Font
	func _draw() -> void:
		var c := size * 0.5
		var r := minf(size.x, size.y) * 0.5 - 2.0
		var rng := RandomNumberGenerator.new()
		rng.seed = 47 + num
		var pts := PackedVector2Array()
		for i in 21:
			var t := TAU * float(i) / 20.0
			pts.append(c + Vector2(cos(t), sin(t)) * (r + rng.randf_range(-0.8, 0.8)))
		draw_colored_polygon(pts, DeskKit.YELL)
		pts.append(pts[0])
		draw_polyline(pts, DeskKit.INK, 2.6, true)
		if font != null:
			var txt := str(num)
			var tw := font.get_string_size(txt, HORIZONTAL_ALIGNMENT_LEFT, -1, 22).x
			draw_string(font, Vector2(c.x - tw * 0.5, c.y + 8.0), txt,
				HORIZONTAL_ALIGNMENT_LEFT, -1, 22, DeskKit.INK)

## A wall card: cream paper normally; the READY variant is the alarm filled in
## — red means act, and SHIP is an act.
class _WallCard:
	extends Control
	var ship_ready := false
	var lean := 0
	func _draw() -> void:
		var rng := RandomNumberGenerator.new()
		rng.seed = 53 + lean
		var pts := PackedVector2Array()
		var corners := [Vector2(2, 2), Vector2(size.x - 2, 2),
			Vector2(size.x - 2, size.y - 2), Vector2(2, size.y - 2)]
		for i in 4:
			var a: Vector2 = corners[i]
			var bb: Vector2 = corners[(i + 1) % 4]
			for k in 9:
				pts.append(a.lerp(bb, float(k) / 9.0)
					+ Vector2(rng.randf_range(-1.4, 1.4), rng.randf_range(-1.4, 1.4)))
		draw_rect(Rect2(4, 5, size.x, size.y), Color(0, 0, 0, 0.16))
		draw_colored_polygon(pts, DeskKit.ALERT if ship_ready else DeskKit.CARD_TINT)
		pts.append(pts[0])
		draw_polyline(pts, DeskKit.INK, 2.4, true)

## The kraft folder face: a folder tab poking up off the body.
class _Folder:
	extends Control
	func _draw() -> void:
		var rng := RandomNumberGenerator.new()
		rng.seed = 59
		draw_rect(Rect2(5, 8, size.x, size.y - 14.0), Color(0, 0, 0, 0.16))
		# the tab
		var tab := PackedVector2Array([Vector2(10, 14), Vector2(14, 2),
			Vector2(size.x * 0.34, 2), Vector2(size.x * 0.36, 14)])
		draw_colored_polygon(tab, DeskKit.KRAFT2)
		# the body
		var pts := PackedVector2Array()
		var corners := [Vector2(2, 14), Vector2(size.x - 2, 14),
			Vector2(size.x - 2, size.y - 4), Vector2(2, size.y - 4)]
		for i in 4:
			var a: Vector2 = corners[i]
			var bb: Vector2 = corners[(i + 1) % 4]
			for k in 10:
				pts.append(a.lerp(bb, float(k) / 10.0)
					+ Vector2(rng.randf_range(-1.4, 1.4), rng.randf_range(-1.4, 1.4)))
		draw_colored_polygon(pts, DeskKit.KRAFT)
		pts.append(pts[0])
		draw_polyline(pts, DeskKit.INK, 2.6, true)
		var tpts := PackedVector2Array(tab)
		tpts.append(tab[0])
		draw_polyline(tpts, DeskKit.INK, 2.4, true)

## A chip's body: card paper with the small shadow; the selected one wears the
## pen's ring instead of the ink edge.
class _ChipBox:
	extends Control
	var selected := false
	var kind := "person"
	func _draw() -> void:
		var rng := RandomNumberGenerator.new()
		rng.seed = 61 + int(position.x) % 11
		draw_rect(Rect2(2, 2, size.x, size.y), Color(0, 0, 0, 0.18))
		var pts := PackedVector2Array()
		var corners := [Vector2(1, 1), Vector2(size.x - 1, 1),
			Vector2(size.x - 1, size.y - 1), Vector2(1, size.y - 1)]
		for i in 4:
			var a: Vector2 = corners[i]
			var bb: Vector2 = corners[(i + 1) % 4]
			for k in 7:
				pts.append(a.lerp(bb, float(k) / 7.0)
					+ Vector2(rng.randf_range(-0.9, 0.9), rng.randf_range(-0.9, 0.9)))
		draw_colored_polygon(pts, DeskKit.CARD_TINT)
		pts.append(pts[0])
		draw_polyline(pts, DeskKit.PEN if selected else DeskKit.INK,
			3.0 if selected else 2.3, true)

## An arrange bin: paper2 in an ink box; the ghost is dashed; a closing bin
## wears the alarm ring — the teardown wizard's own color.
class _BinBox:
	extends Control
	var ghost := false
	var closing := false
	func _draw() -> void:
		if not ghost:
			draw_rect(Rect2(1, 1, size.x - 2, size.y - 2), DeskKit.PAPER2)
		if ghost:
			# dashed edge, drawn by hand
			var per := 14.0
			var x := 0.0
			while x < size.x:
				draw_line(Vector2(x, 1), Vector2(minf(x + 8.0, size.x), 1), Color(DeskKit.INK, 0.5), 2.4)
				draw_line(Vector2(x, size.y - 1), Vector2(minf(x + 8.0, size.x), size.y - 1),
					Color(DeskKit.INK, 0.5), 2.4)
				x += per
			var yy := 0.0
			while yy < size.y:
				draw_line(Vector2(1, yy), Vector2(1, minf(yy + 8.0, size.y)), Color(DeskKit.INK, 0.5), 2.4)
				draw_line(Vector2(size.x - 1, yy), Vector2(size.x - 1, minf(yy + 8.0, size.y)),
					Color(DeskKit.INK, 0.5), 2.4)
				yy += per
			return
		var rng := RandomNumberGenerator.new()
		rng.seed = 67 + int(position.x) % 13
		var pts := PackedVector2Array()
		var corners := [Vector2(1, 1), Vector2(size.x - 1, 1),
			Vector2(size.x - 1, size.y - 1), Vector2(1, size.y - 1)]
		for i in 4:
			var a: Vector2 = corners[i]
			var bb: Vector2 = corners[(i + 1) % 4]
			for k in 12:
				pts.append(a.lerp(bb, float(k) / 12.0)
					+ Vector2(rng.randf_range(-1.1, 1.1), rng.randf_range(-1.1, 1.1)))
		pts.append(pts[0])
		if closing:
			draw_polyline(pts, Color(DeskKit.ALERT, 0.4), 7.0, true)
		draw_polyline(pts, DeskKit.ALERT if closing else DeskKit.INK, 2.7, true)

## The deadline chip: alert red under an ink edge, with a tiny drawn clock face
## at its head — the momentary tab's own mark.
class _ClockChip:
	extends Control
	func _draw() -> void:
		var rng := RandomNumberGenerator.new()
		rng.seed = 71
		var pts := PackedVector2Array()
		var corners := [Vector2(1, 1), Vector2(size.x - 1, 1),
			Vector2(size.x - 1, size.y - 1), Vector2(1, size.y - 1)]
		for i in 4:
			var a: Vector2 = corners[i]
			var bb: Vector2 = corners[(i + 1) % 4]
			for k in 6:
				pts.append(a.lerp(bb, float(k) / 6.0)
					+ Vector2(rng.randf_range(-0.7, 0.7), rng.randf_range(-0.7, 0.7)))
		draw_colored_polygon(pts, DeskKit.ALERT)
		pts.append(pts[0])
		draw_polyline(pts, DeskKit.INK, 2.2, true)
