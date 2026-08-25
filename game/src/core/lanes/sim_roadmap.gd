class_name SimRoadmap
extends RefCounted
## LANE 07 — THE ROADMAP (bets, the ship roll, tech debt). Spec: docs/design/07-roadmap.md
##
## THE STUB the engine spine planted. Every entry point below is a no-op, and
## the weekly tick is arithmetically identical while they stay that way — that
## is what lets this lane be written against a live engine without touching a
## shared file. Fill the bodies here; the call sites already exist.
##
## The spine calls, in tick order (docs/design/00-spine.md §1, HOOKS.md):
##   tick_pre   tick §7 — bets progress and ship before adoption reads product
##   tick_money the money section — write ONLY the P&L lanes this subsystem owns
##   tick_post  after the week's record is written and can be read back
## and outside the tick: directives() feeds the DM block, attention() feeds
## every bang in the game through SimEngine.attention_items.

## Tick §7: rnd-routed progress ticks, READY bets roll the house dice (salt 70),
## payoffs land. A shipped bet's quality must exist before §8 reads product.
static func tick_pre(_state: GameState, _rep: Dictionary) -> void:
	pass

## The money section. `_m` is the working P&L record — one key per lane of
## docs/design/00-spine.md §2. Write only what this subsystem owns; the spine
## sums burn and writes the record whole.
static func tick_money(_state: GameState, _rep: Dictionary, _m: Dictionary) -> void:
	pass

## After the record is written: slot refresh and the week-ahead READY notice.
static func tick_post(_state: GameState, _rep: Dictionary) -> void:
	pass

## DM context lines, section 11 of the DIRECTIVES block. Return plain
## strings; the spine orders them and caps the block.
static func directives(_state: GameState) -> Array[String]:
	return []

## Attention rows — the product desk (a bet ready to ship, debt gone critical).
## Each row is {desk, key, severity, label} with label ≤40 chars: the garage
## ticker prints it verbatim (docs/design/00-spine.md §4).
static func attention(_state: GameState) -> Array:
	return []
