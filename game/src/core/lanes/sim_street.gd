class_name SimStreet
extends RefCounted
## LANE 03 — THE STREET (rivals + macro weather). Spec: docs/design/03-rivals-macro.md
##
## THE STUB the engine spine planted. Every entry point below is a no-op, and
## the weekly tick is arithmetically identical while they stay that way — that
## is what lets this lane be written against a live engine without touching a
## shared file. Fill the bodies here; the call sites already exist.
##
## The spine calls, in tick order (docs/design/00-spine.md §1, HOOKS.md):
##   tick_pre   tick §6a/§6b — rivals act, then the weather turns
##   tick_money the money section — write ONLY the P&L lanes this subsystem owns
##   tick_post  after the week's record is written and can be read back
## and outside the tick: directives() feeds the DM block, attention() feeds
## every bang in the game through SimEngine.attention_items.

## Tick §6a then §6b, in that order inside this one call: per-rival upkeep →
## weekly action pick (salt 30) → poach (31) → hq disruptor (32), and then the
## macro walk and shock roll (80). Both run BEFORE the market so a price cut or
## a launch shapes THIS week's demand.
static func tick_pre(_state: GameState, _rep: Dictionary) -> void:
	pass

## The money section. `_m` is the working P&L record — one key per lane of
## docs/design/00-spine.md §2. Write only what this subsystem owns; the spine
## sums burn and writes the record whole.
static func tick_money(_state: GameState, _rep: Dictionary, _m: Dictionary) -> void:
	pass

## After the record is written: street bookkeeping that reads the closed week.
static func tick_post(_state: GameState, _rep: Dictionary) -> void:
	pass

## DM context lines, section 7 and 8 of the DIRECTIVES block. Return plain
## strings; the spine orders them and caps the block.
static func directives(_state: GameState) -> Array[String]:
	return []

## Attention rows — the street desk (a beat the founder would retell).
## Each row is {desk, key, severity, label} with label ≤40 chars: the garage
## ticker prints it verbatim (docs/design/00-spine.md §4).
static func attention(_state: GameState) -> Array:
	return []

# ── THE TWO SEAMS THE SPINE LEFT OPEN ────────────────────────────────────────
## Flip these to true when tick_pre takes the job over. While they are false the
## engine runs its LEGACY blocks — the salt-6 strength ratchet and the plain
## salt-7 mood walk — so the world behaves exactly as it always has.
##
## Owning the macro walk does NOT mean a new stream: draw the SAME single salt-7
## number (SimEngine.rng_for(state, SimEngine.SALT_TREND)) and mean-revert it,
## or every downstream subsystem's dice shift with you.
const OWNS_RIVALS := false
const OWNS_MACRO := false
