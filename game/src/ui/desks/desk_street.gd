class_name DeskStreet
extends RefCounted
## DESK — the binder's `the street` tab. Spec: docs/design/03-rivals-macro.md §11
##
## `binder.gd` dispatches the tab body here and passes ITSELF, so this file draws
## through the binder's own helpers and never reaches into the sheet directly.
##
## THE PAGE, top to bottom: the weather, then the competition, then the money.
##   1 THE MACRO BANNER — the season in words beside the tab's name, and, when a
##     shock or its one-week warning is live, the authored line with weeks left.
##     Seasons must be readable BEFORE the money screens punish you; the
##     pre-announcement is the whole playable warning.
##   2 A BLOCK PER RIVAL — four lines through DeskKit.log_block: who they are,
##     how they stand (four word-reads, never a raw float), what they play, and
##     the last three things they did. Rivals become predictable through their
##     RECORD, not through hidden stats — pattern-reading is the skill this page
##     is teaching, and the word maps live once on SimStreet so the two engines
##     cannot drift apart.
##   3 THE MONEY — the investors, unchanged, compressing to a line each exactly
##     when the page needs the room (an hq third rival, a long thesis, a live
##     shock banner). Budgeted, then measured.
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
	var y := DeskKit.title(b, "the street")
	# the season rides the title line, in the desk's value column — one glance
	# tells you whether the weather is helping before you read a single rival
	b.label(SimStreet.season_read(state.market_trend), Vector2(DeskKit.X_VALUE, 18.0),
		DeskKit.ROW, Color(DeskKit.INK, 0.75), 720.0)
	y = _banner(b, state, y)

	if state.rivals.is_empty():
		y = DeskKit.empty(b, Vector2(DeskKit.X_ID, y),
			"nobody is competing with you this week.",
			"that is rarer, and more temporary, than it feels.")
	for rv in state.rivals:
		var r: Dictionary = rv
		var log_all: Array = r.get("log", [])
		var trail: Array = log_all.slice(maxi(log_all.size() - 3, 0))
		y = DeskKit.log_block(b, y, {
			"identity": "%s — %s" % [String(r.get("name", "?")),
				SimEngine._fuzz(float(r.get("strength", 20.0)))],
			"posture": SimStreet.posture_line(r),
			"plays": "plays: " + ", ".join(r.get("tactics", [])),
			"trail": trail,
		})

	b.label("the money:", Vector2(DeskKit.X_ID, y + 4.0), DeskKit.ROW)
	y += 48.0
	_investors(b, state, y)

## THE WEATHER STRIP. Line one is the season, drawn beside the title above; this
## draws line two — the authored shock line and how long it has left — and only
## while there is weather to report. Coral for a winter and its warning, sage
## for a boom: the page is colour-coded to which way the money is moving.
static func _banner(b, state: GameState, y: float) -> float:
	for key in ["funding_winter", "boom", "winter_watch", "boom_watch"]:
		if not SimEngine.has_status(state, String(key)):
			continue
		var text := String(SimStreet.BANNER.get(key, ""))
		if key in ["funding_winter", "boom"]:
			text += "  ·  %d wks left" % SimStreet.weeks_left(state, String(key))
		var warm: bool = key in ["boom", "boom_watch"]
		b.label(text, Vector2(DeskKit.X_ID, y), DeskKit.STATUS,
			DeskKit.SAGE if warm else DeskKit.PEN, 1100.0)
		return y + 40.0
	return y

## What one investor entry costs the page: FULL_WK with a one-line thesis,
## TIGHT_WK as a name and archetype only, WRAP_WK once the thesis takes two
## lines. The budget picks the MODE up front so the section never mixes the two;
## the layout still MEASURES every wrap, and the room check reserves WRAP_WK —
## the worst case, not the common one — so the last entry can never hang off the
## bottom of the page. Anything that will not fit closes with "+N more".
const FULL_WK := 88.0
const TIGHT_WK := 38.0
const WRAP_WK := 124.0
## THE "+N MORE" LINE IS PART OF THE BUDGET. The room check used to reserve only
## the next ENTRY, so on the crowded page (an hq third rival, a live shock banner)
## the closing line was drawn at the y that had just failed the test — off the
## bottom of the pane and onto the clipboard's shadow.
const MORE_WK := 34.0

## THE MONEY, still the founder's phone book. Full entries while the page has
## room; one line each when it does not — the street has to stay one page at
## every era, including the week an hq disruptor takes a third block.
static func _investors(b, state: GameState, y: float) -> void:
	var tight := y + float(state.investors.size()) * FULL_WK > DeskKit.PANE_H
	var shown := 0
	for inv in state.investors:
		if shown >= DeskKit.LIST_CAP \
				or y + (TIGHT_WK if tight else WRAP_WK) + MORE_WK > DeskKit.PANE_H:
			if y + MORE_WK <= DeskKit.PANE_H:
				DeskKit.more(b, Vector2(DeskKit.X_ID, y), state.investors.size() - shown,
					"are in the book")
			return
		var d: Dictionary = inv
		b.label("%s (%s)" % [String(d.get("name", "?")), String(d.get("archetype", ""))],
			Vector2(DeskKit.X_ID, y), 29)
		if tight:
			y += TIGHT_WK
		else:
			var quote := "\"%s\"  ·  %s" % [String(d.get("thesis", "")), String(d.get("trait", ""))]
			b.label(quote, Vector2(30.0, y + 38.0), 25, Color(DeskKit.INK, 0.65), 1070.0)
			y += 44.0 + b.wrap_h(quote, 25, 1070.0) + 16.0
		shown += 1

## A press inside this desk. `id` is whatever the desk's own draw registered.
## The street is a page you READ — every control on it would be a lie, because
## none of this is yours to change from here.
static func handle(_b, _id: String) -> void:
	pass
