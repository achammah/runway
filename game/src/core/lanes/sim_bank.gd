class_name SimBank
extends RefCounted
## LANE 06 — THE BANK & THE STATE (credit, interest, tax). Spec: docs/design/06-finance.md
##
## THE STUB the engine spine planted. Every entry point below is a no-op, and
## the weekly tick is arithmetically identical while they stay that way — that
## is what lets this lane be written against a live engine without touching a
## shared file. Fill the bodies here; the call sites already exist.
##
## The spine calls, in tick order (docs/design/00-spine.md §1, HOOKS.md):
##   tick_pre   tick §9 — notes settle before the money is assembled
##   tick_money the money section — write ONLY the P&L lanes this subsystem owns
##   tick_post  after the week's record is written and can be read back
## and outside the tick: directives() feeds the DM block, attention() feeds
## every bang in the game through SimEngine.attention_items.

## Tick §9, before the money is assembled: migrate legacy notes, accrue the
## week's schedule, run the miss ladder.
static func tick_pre(_state: GameState, _rep: Dictionary) -> void:
	pass

## The money section. `_m` is the working P&L record — one key per lane of
## docs/design/00-spine.md §2. Write only what this subsystem owns; the spine
## sums burn and writes the record whole.
static func tick_money(_state: GameState, _rep: Dictionary, _m: Dictionary) -> void:
	pass

## After the record is written: anything reading the finished record (forecast, break-even).
static func tick_post(_state: GameState, _rep: Dictionary) -> void:
	pass

## DM context lines, section 10 of the DIRECTIVES block. Return plain
## strings; the spine orders them and caps the block.
static func directives(_state: GameState) -> Array[String]:
	return []

## Attention rows — the bank (debt distress, the first tax week, break-even).
## Each row is {desk, key, severity, label} with label ≤40 chars: the garage
## ticker prints it verbatim (docs/design/00-spine.md §4).
static func attention(_state: GameState) -> Array:
	return []

# ── THE DEBT SEAM ────────────────────────────────────────────────────────────
## While this is false the engine runs the LEGACY shark note: 18%/wk compounded
## into principal, auto-repaid above $2,000, its interest booked to the P&L's
## interest lane. Flip it and this lane owns every note — the structured `loans`
## list, honest risk-priced rates, amortization, the miss ladder.
const OWNS_DEBT := false

## THE STATE, charged last. Tax can only be computed once every other lane has
## closed, because it is levied on what is LEFT: revenue − burn − standing
## liabilities − interest. `m` is the working record, already complete except
## for this. Return $0 and the week is untaxed, which is the garage's truth.
static func tax_wk(_state: GameState, _m: Dictionary) -> int:
	return 0
