class_name DeskPipeline
extends RefCounted
## DESK — the binder's `customers` tab, Enterprise branch.
## Spec: docs/design/05-enterprise-pipeline.md
##
## THE STUB the spine planted. `DeskCustomers` dispatches the Enterprise page
## here and passes the Binder ITSELF, so this file draws through the binder's own
## helpers and never reaches into the sheet directly. Empty until the lane fills
## it.
##
## THE HANDOVER IS THIS LANE'S CALL: `owns_page()` answers false while the board
## is a stub, so an Enterprise run keeps today's customer page until this file
## can draw something better. Flip it to `String(b.state.biz_who) == "Enterprise"`
## in the same commit that fills `draw_board` — that one line is the whole
## takeover, and nobody has to touch desk_customers.gd for it.
##
## The bar every surface ships at (docs/design/00-spine.md §11): readable first
## pass by a tired player; concepts named in real business terms with a teaching
## line where a number first appears; no dead ends and every state leavable;
## drawn in the game's hand, never a SaaS panel. The shared components live in
## game/src/ui/components.gd (DeskKit) — the stage board is DeskKit.board().

## Does the pipeline own the customers page on this run? False until the board
## is real: an empty page is a worse Enterprise desk than today's funnel read.
static func owns_page(_b) -> bool:
	return false

## Draw the stage board, lead chips, signed-logos strip and teaching footer. `b`
## is the Binder itself (untyped to keep the two files free of a cyclic class
## dependency).
static func draw(_b) -> void:
	pass

## A press inside this desk. `id` is whatever the desk's own draw registered.
static func handle(_b, _id: String) -> void:
	pass

## Drawn INSIDE the customers desk on Enterprise runs (DeskCustomers calls this).
static func draw_board(_b) -> void:
	pass
