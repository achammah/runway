class_name DeskProduct
extends RefCounted
## DESK — the binder's `product` tab. Spec: docs/design/07-roadmap.md
##
## `binder.gd` dispatches the tab body here and passes ITSELF, so this file draws
## through the binder's own helpers and never reaches into the sheet directly.
##
## WHAT IS HERE NOW is today's shipped product page, moved verbatim off the
## binder so the lane REPLACES a working baseline instead of a blank file. The
## lane's job (07): the debt jar shrinks to (300,10) 64×84 with its triple-cost
## caption, the debt spark goes, the hype spark moves to vitals, and the roadmap
## board takes the sheet — capacity header, bet cards at 118px pitch
## (uncommitted / committed+progress / READY), hardening row, footer.
##
## The bar every surface ships at (docs/design/00-spine.md §11): readable first
## pass by a tired player; concepts named in real business terms with a teaching
## line where a number first appears; no dead ends and every state leavable;
## drawn in the game's hand, never a SaaS panel. The shared components live in
## game/src/ui/components.gd (DeskKit) — use them, never fork them.

## Draw the roadmap board: capacity, bet cards, progress, READY. `b` is the
## Binder itself (untyped to keep the two files free of a cyclic class
## dependency).
static func draw(b) -> void:
	var state: GameState = b.state
	b.icon("product", Vector2(10, 6))
	b.label("v0.%d" % state.product, Vector2(100, 10), 46)
	b.label("tech debt:", Vector2(10, 110), 28)
	b.debt_jar(state.tech_debt / 100.0, Vector2(160, 92), Vector2(90, 110))
	var risk := maxf((state.tech_debt - 40.0) / 250.0, 0.0) * 100.0
	b.label("outage odds ≈ %d%% weekly" % int(risk), Vector2(290, 120), 28,
		Binder.PEN if risk > 10.0 else Color(Binder.INK, 0.7))
	b.label("debt, weekly:", Vector2(10, 236), 24, Color(Binder.INK, 0.6))
	b.spark(b.series("debt"), Vector2(10, 268), Vector2(1120, 170), Binder.PEN)
	b.label("hype:", Vector2(10, 470), 28)
	b.spark(b.series("hype"), Vector2(120, 452), Vector2(1010, 130), Binder.YELL)
	# THE BENCH rides the bottom band on Hardware runs (see draw_bench).
	if String(state.biz_what) == "Hardware":
		draw_bench(b)

## A press inside this desk. `id` is whatever the desk's own draw registered.
static func handle(_b, _id: String) -> void:
	pass

## THE BENCH belongs to the hardware lane and is drawn inside this desk on
## Hardware runs only. The call site is planted; the band is ruled in
## docs/design/00-spine.md §11 (y470-740) — on Hardware runs 07 caps its bet
## cards at 2 and yields the footer line to make room.
static func draw_bench(b) -> void:
	DeskFactory.draw_bench(b)
