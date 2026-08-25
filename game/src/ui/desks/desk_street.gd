class_name DeskStreet
extends RefCounted
## DESK — the binder's `the street` tab. Spec: docs/design/03-rivals-macro.md
##
## `binder.gd` dispatches the tab body here and passes ITSELF, so this file draws
## through the binder's own helpers and never reaches into the sheet directly.
##
## WHAT IS HERE NOW is today's shipped street page — rivals and the money —
## moved verbatim off the binder so the lane REPLACES a working baseline instead
## of a blank file. The lane's job (03): the macro banner across the top, rival
## blocks grown from two lines to four (posture words, what they fight on, the
## last-3 action log — never raw floats), investors compressed to a line each
## once a third rival exists. The action log component is DeskKit.log_block().
##
## The bar every surface ships at (docs/design/00-spine.md §11): readable first
## pass by a tired player; concepts named in real business terms with a teaching
## line where a number first appears; no dead ends and every state leavable;
## drawn in the game's hand, never a SaaS panel. The shared components live in
## game/src/ui/components.gd (DeskKit) — use them, never fork them.

## Draw the macro banner and the four-line rival blocks. `b` is the Binder itself
## (untyped to keep the two files free of a cyclic class dependency).
##
## Wrapped text is MEASURED, never assumed one line — fixed steps stacked the
## street on itself the first week a thesis wrapped (owner photo).
static func draw(b) -> void:
	var state: GameState = b.state
	b.label("the street", Vector2(10, 6), 40)
	var y := 80.0
	for rv in state.rivals:
		var r: Dictionary = rv
		b.label("%s — %s" % [String(r.get("name", "?")), SimEngine._fuzz(float(r.get("strength", 20.0)))],
			Vector2(10, y), 32)
		var plays := "plays: " + ", ".join(r.get("tactics", []))
		b.label(plays, Vector2(30, y + 42), 26, Color(Binder.INK, 0.7), 1070.0)
		y += 50.0 + b.wrap_h(plays, 26, 1070.0) + 18.0
	b.label("the money:", Vector2(10, y + 10), 32)
	y += 64.0
	for inv in state.investors:
		var d: Dictionary = inv
		b.label("%s (%s)" % [String(d.get("name", "?")), String(d.get("archetype", ""))],
			Vector2(10, y), 29)
		var quote := "\"%s\"  ·  %s" % [String(d.get("thesis", "")), String(d.get("trait", ""))]
		b.label(quote, Vector2(30, y + 38), 25, Color(Binder.INK, 0.65), 1070.0)
		y += 44.0 + b.wrap_h(quote, 25, 1070.0) + 16.0

## A press inside this desk. `id` is whatever the desk's own draw registered.
static func handle(_b, _id: String) -> void:
	pass
