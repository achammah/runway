class_name SimBoard
extends RefCounted
## LANE 08 — THE BOARD & M&A (covenants, offers, the exit). Spec: docs/design/08-board-mna.md
##
## THE STUB the engine spine planted. Every entry point below is a no-op, and
## the weekly tick is arithmetically identical while they stay that way — that
## is what lets this lane be written against a live engine without touching a
## shared file. Fill the bodies here; the call sites already exist.
##
## The spine calls, in tick order (docs/design/00-spine.md §1, HOOKS.md):
##   tick_pre   tick §9 — governance state settles before the money
##   tick_money the money section — write ONLY the P&L lanes this subsystem owns
##   tick_post  after the week's record is written and can be read back
## and outside the tick: directives() feeds the DM block, attention() feeds
## every bang in the game through SimEngine.attention_items.

## Tick §9, before the money: governance bookkeeping the week's record needs.
static func tick_pre(_state: GameState, _rep: Dictionary) -> void:
	pass

## The money section. `_m` is the working P&L record — one key per lane of
## docs/design/00-spine.md §2. Write only what this subsystem owns; the spine
## sums burn and writes the record whole.
static func tick_money(_state: GameState, _rep: Dictionary, _m: Dictionary) -> void:
	pass

## After the record is written: §9c the board review against the covenant (deterministic — no dice, no
## salt), then §9d M&A offers and the IPO window (salt 100), priced off the
## growth this week just posted.
## SEAM (coordinator-planted): fires at the signature, both signing sites.
## 08 fills it (pool shuffle, covenant set); no-op keeps today's behavior.
static func on_round_closed(_state: GameState, _amount: int, _pct: float) -> void:
	pass

## SEAM (coordinator-planted): a journal offer block mirroring the term-sheet
## idiom. Empty dict = no card. 08 returns {"title": String, "cards":
## [{"id","text"}], and the journal routes chosen ids to journal_pick().
static func journal_offer(_state: GameState) -> Dictionary:
	return {}

static func journal_pick(_state: GameState, _id: String) -> String:
	return ""   # receipt line, "" = ignored

static func tick_post(_state: GameState, _rep: Dictionary) -> void:
	pass

## DM context lines, section 12 (board) and 14 (M&A) of the DIRECTIVES block. Return plain
## strings; the spine orders them and caps the block.
static func directives(_state: GameState) -> Array[String]:
	return []

## Attention rows — the cap table (review due, an offer on the table).
## Each row is {desk, key, severity, label} with label ≤40 chars: the garage
## ticker prints it verbatim (docs/design/00-spine.md §4).
static func attention(_state: GameState) -> Array:
	return []
