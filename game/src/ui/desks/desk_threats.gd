class_name DeskThreats
extends RefCounted
## DESK — the binder's `threats` tab. Spec: docs/design/11-binder-rework.md
## §threats, and the attention registry in docs/design/00-spine.md §4/§11.
##
## `binder.gd` dispatches the tab body here and passes ITSELF, so this file draws
## through the binder's own helpers and never reaches into the sheet directly.
##
## THE QUESTION THIS DESK ANSWERS: "what could kill us?" — the whole company's
## shouting, in one place, loudest first.
##   1 THE SPILLOVER — every attention item at warn or above, ranked. This is the
##     same list the tab bangs, the garage badge and the pre-roll review read, so
##     a desk that is shouting can never be shouting only somewhere the player is
##     not looking. Every row names the desk that owns the fix.
##   2 THE CLOCKS — what fires, and in how many weeks, each led by a drawn face.
##   3 THE CONDITIONS — what is helping and what is hurting, with the weeks left.
##   4 THE STANDING COSTS — what bills every week until it runs out.
##
## The bar every surface ships at (docs/design/00-spine.md §11): readable first
## pass by a tired player; concepts named in real business terms with a teaching
## line where a number first appears; no dead ends and every state leavable;
## drawn in the game's hand, never a SaaS panel. The shared components live in
## game/src/ui/components.gd (DeskKit) — use them, never fork them.

## The drawn clock's footprint — the space the typed ⏰ used to take at 30px type.
const CLOCK_SIDE := 30.0
## The spillover cap: twelve rows, then the truth about the rest.
const SPILL_CAP := 12

## Draw the overflow page. `b` is the Binder itself (untyped to keep the two
## files free of a cyclic class dependency).
static func draw(b) -> void:
	var state: GameState = b.state
	b.label("threats & promises", Vector2(10, 6), 40)
	var y := 80.0
	# WHAT NEEDS A HAND, in one place (docs/design/00-spine.md §4/§11): every
	# attention item at warn or above, loudest first. This is the same list the
	# tab bangs, the garage badge and the pre-roll review read — so a desk that
	# is shouting can never be shouting only somewhere the player is not looking.
	var wants := SimEngine.preroll_items(state)
	if not wants.is_empty():
		var shown := 0
		for it in wants:
			if shown >= SPILL_CAP:
				b.label("+%d more — the desks have the details" % (wants.size() - shown),
					Vector2(10, y), 26, Color(DeskKit.INK, 0.6))
				y += 44.0
				break
			var itd: Dictionary = it
			b.label("! %s  ·  %s" % [String(itd.get("label", "")), String(itd.get("desk", ""))],
				Vector2(10, y), 28,
				DeskKit.PEN if int(itd.get("severity", 2)) >= 3 else Color(DeskKit.INK, 0.85))
			y += 44.0
			shown += 1
		y += 12.0
	if state.clocks.is_empty() and state.statuses.is_empty() and state.commitments.is_empty():
		b.label("nothing ticking. that never lasts.", Vector2(10, y), 30,
			Color(DeskKit.INK, 0.6))
	# THE WORD IS THE MARK. The hand carries no clock, no triangles and no repeat
	# arrow (U+23F0/25B2/25BC/21BB are all absent from Patrick Hand), so a typed
	# one was borrowed from whatever face the OS handed over — the clock arrived
	# as a full-colour emoji on a cream sheet. Each row now leads with the word
	# that says what kind of row it is, which is also what §3.3 asks for: read the
	# page in grey and every state is still there.
	# THE CLOCK IS DRAWN, NOT TYPED — the instrument §2.10 names, and the twin
	# Unity has always drawn: a wobbled coral face with two ink hands.
	for c in state.clocks:
		var cd: Dictionary = c
		b.clock(Vector2(10, y + 3.0), CLOCK_SIDE)
		b.label("in %d wks: %s" % [int(cd.get("weeks_left", 0)),
			String(cd.get("consequence", ""))],
			Vector2(10.0 + CLOCK_SIDE + 8.0, y), 30, DeskKit.PEN)
		y += 52.0
	for s in state.statuses:
		var sd: Dictionary = s
		var kind := String(SimEngine.STATUS.get(String(sd.get("name", "")), {}).get("kind", "condition"))
		b.label("%s %s — %d wks left" % ["helping:" if kind == "buff" else "hurting:",
			String(sd.get("name", "")).replace("_", " "), int(sd.get("weeks_left", 0))],
			Vector2(10, y), 30, DeskKit.SAGE if kind == "buff" else DeskKit.PEN)
		y += 52.0
	for cm in state.commitments:
		var cmd: Dictionary = cm
		b.label("standing: %s — $%d/wk for %d more wks" % [String(cmd.get("name", "")),
			int(cmd.get("cash_wk", 0)), int(cmd.get("weeks_left", 0))],
			Vector2(10, y), 30, DeskKit.BLUE)
		y += 52.0
	# THE PAGE STATES ITS OWN LAW, like every desk does (§2.7). Reading order ends
	# on the lesson: this sheet is the overflow, and the thing it has to teach is
	# that these rows are ranked and that the desks hold the controls.
	b.label("the rules of this page: everything the company is shouting about, loudest first · "
		+ "a CLOCK fires on its week · a CONDITION expires on its own · a STANDING cost bills "
		+ "until it runs out · nothing is fixed here, and every row names the desk that owns it",
		Vector2(10, 734), 21, Color(DeskKit.INK, 0.5), 1100.0)

## A press inside this desk. `id` is whatever the desk's own draw registered —
## the rework's pressable rows jump to the desk that owns the fix, which is a
## `focus_desk` on the binder and never a mutation here.
static func handle(b, id: String) -> void:
	if id.begins_with("go:"):
		b.focus_desk(id.substr(3))
