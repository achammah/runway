class_name SimFunnel
extends RefCounted
## LANE 04 — THE FUNNEL (four acquisition channels). Spec: docs/design/04-funnel-channels.md
##
## THE STUB the engine spine planted. Every entry point below is a no-op, and
## the weekly tick is arithmetically identical while they stay that way — that
## is what lets this lane be written against a live engine without touching a
## shared file. Fill the bodies here; the call sites already exist.
##
## The spine calls, in tick order (docs/design/00-spine.md §1, HOOKS.md):
##   tick_pre   tick §8 — channel stocks settle before reach is read
##   tick_money the money section — write ONLY the P&L lanes this subsystem owns
##   tick_post  after the week's record is written and can be read back
## and outside the tick: directives() feeds the DM block, attention() feeds
## every bang in the game through SimEngine.attention_items.

## Tick §8, before adoption: content equity compounds or rots, referral and
## outbound stocks settle — everything reach_mult is about to read.
static func tick_pre(_state: GameState, _rep: Dictionary) -> void:
	pass

## The money section. `_m` is the working P&L record — one key per lane of
## docs/design/00-spine.md §2. Write only what this subsystem owns; the spine
## sums burn and writes the record whole.
static func tick_money(_state: GameState, _rep: Dictionary, _m: Dictionary) -> void:
	pass

## After the record is written: attribution bookkeeping for the closed week.
static func tick_post(_state: GameState, _rep: Dictionary) -> void:
	pass

## DM context lines, section (no numbered slot; rides after the street) of the DIRECTIVES block. Return plain
## strings; the spine orders them and caps the block.
static func directives(_state: GameState) -> Array[String]:
	return []

## Attention rows — the ledger (a channel burning money for nothing).
## Each row is {desk, key, severity, label} with label ≤40 chars: the garage
## ticker prints it verbatim (docs/design/00-spine.md §4).
static func attention(_state: GameState) -> Array:
	return []

# ── THE REACH SEAM ───────────────────────────────────────────────────────────
## THE ENGINE'S QUESTION: how much reach did this week's acquisition spend buy?
## `dflt` is the blended lever the engine would use on its own — hand it back
## unchanged and adoption is byte-identical. Replace it with the four-channel
## term (ads saturate, content compounds, referrals amplify, outbound is quota
## math) and the whole funnel lights up without the engine changing a line.
static func reach_mult(_state: GameState, _spend: float, dflt: float) -> float:
	return dflt

## THE DM's `set_budget` with the founder-language cat "marketing" lands here:
## the narrator says "put $2k into marketing" and the ENGINE decides which
## channels that means, splitting by the mix the player already curated. The
## stub does the spine's simple ruling — legacy marketing IS paid ads.
static func set_marketing(state: GameState, amount: int) -> void:
	state.budgets["ads"] = amount
