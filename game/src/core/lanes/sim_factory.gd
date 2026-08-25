class_name SimFactory
extends RefCounted
## LANE 09 — HARDWARE PRODUCTION (build, stock, machines). Spec: docs/design/09-hardware.md
##
## THE STUB the engine spine planted. Every entry point below is a no-op, and
## the weekly tick is arithmetically identical while they stay that way — that
## is what lets this lane be written against a live engine without touching a
## shared file. Fill the bodies here; the call sites already exist.
##
## The spine calls, in tick order (docs/design/00-spine.md §1, HOOKS.md):
##   tick_pre   tick §7h — PRODUCE FIRST, before adoption can spend the shelf
##   tick_money the money section — write ONLY the P&L lanes this subsystem owns
##   tick_post  after the week's record is written and can be read back
## and outside the tick: directives() feeds the DM block, attention() feeds
## every bang in the game through SimEngine.attention_items.

## Tick §7h: build target → produce (learning curve) → breakdown roll (salt 110).
## PRODUCE FIRST — stock must exist before §8 is allowed to sell it.
static func tick_pre(_state: GameState, _rep: Dictionary) -> void:
	pass

## The money section. `_m` is the working P&L record — one key per lane of
## docs/design/00-spine.md §2. Write only what this subsystem owns; the spine
## sums burn and writes the record whole.
static func tick_money(_state: GameState, _rep: Dictionary, _m: Dictionary) -> void:
	pass

## After the record is written: stockout and overstock bookkeeping for the closed week.
static func tick_post(_state: GameState, _rep: Dictionary) -> void:
	pass

## DM context lines, section 13 of the DIRECTIVES block. Return plain
## strings; the spine orders them and caps the block.
static func directives(_state: GameState) -> Array[String]:
	return []

## Attention rows — the product desk (stockout, overstock, a machine down).
## Each row is {desk, key, severity, label} with label ≤40 chars: the garage
## ticker prints it verbatim (docs/design/00-spine.md §4).
static func attention(_state: GameState) -> Array:
	return []

# ── THE STOCK SEAM ───────────────────────────────────────────────────────────
## You cannot sell what you did not build. The engine hands over the week's
## demand AFTER the go-to-market clamp; clamp it to the shelf, decrement the
## shelf, and receipt the stockout. Off Hardware, hand `adds` straight back and
## demand stays stock-free exactly as it is today.
static func clamp_adds(_state: GameState, _rep: Dictionary, adds: float) -> float:
	return adds
