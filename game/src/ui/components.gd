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
