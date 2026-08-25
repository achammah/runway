class_name DeskPipeline
extends RefCounted
## DESK — the binder's `customers` tab. Spec: docs/design/05-enterprise-pipeline.md
##
## THE STUB the spine planted. `binder.gd` dispatches the tab body here and
## passes ITSELF, so this file draws through the binder's own helpers and never
## reaches into the sheet directly. Empty until the lane fills it.
##
## The bar every surface ships at (docs/design/00-spine.md §11): readable first
## pass by a tired player; concepts named in real business terms with a teaching
## line where a number first appears; no dead ends and every state leavable;
## drawn in the game's hand, never a SaaS panel. The shared components live in
## game/src/ui/components.gd — use them, never fork them.

## Draw the stage board, lead chips, signed-logos strip and teaching footer. `b` is the Binder itself (untyped to keep the two files free of
## a cyclic class dependency).
static func draw(_b) -> void:
	pass

## A press inside this desk. `id` is whatever the desk's own draw registered.
static func handle(_b, _id: String) -> void:
	pass

## Drawn INSIDE the customers desk on Enterprise runs (DeskCustomers calls this).
static func draw_board(_b) -> void:
	pass
