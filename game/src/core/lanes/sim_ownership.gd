class_name SimOwnership
extends RefCounted
## LANE — THE OWNERSHIP CLUSTER (ESOP, instruments, the raise, recruitment,
## buyout offers). Spec: docs/design/DECISIONS.md (THE OWNERSHIP CLUSTER,
## THE ESOP THREAD, THE OFFER) + docs/design/DAG2.md.
##
## STUB (DAG2 W1). The engine spine plants the entry points; W2 L-OWN fills
## the logic. Until then every hook is a no-op and the tick's arithmetic is
## byte-identical to a tree without this file.
##
## What lands here (W2): ESOP pool + grants (208-wk vest, 52-wk cliff,
## leavers keep vested), instruments safe/note/priced/bridge with conversion
## at priced rounds and the SAFE-stack math, investor interest score + raise
## stages + the founder-time tax, the waterfall executor, buyout-offer
## generation (fishy structures included) with powers checks read off the
## instrument fields (protective, drag_threshold), and recruitment:
## roles / candidates / the offer composer / acceptance model / rival
## counters. Extends the board lane's term-sheet mechanics — never forks them.
##
## The spine calls, in tick order (docs/design/HOOKS.md):
##   tick_pre   tick §9 — vesting ticks, instrument maturities and the raise
##              pipeline settle with the financial lanes, before the money
##   tick_money the money section — this lane owns the `recruit_ads` P&L lane
##              (role adverts), zero until filled. ESOP grants are NON-CASH and
##              never enter the P&L identity; a raise that closes wires cash in
##              as an EVENT (like apply_round), not as a weekly lane.
##   tick_post  after the record — inbound knocks and buyout offers read the
##              finished week
## and outside the tick: directives() feeds the DM block, attention() feeds
## every bang in the game through SimEngine.attention_items.
##
## SALTS (docs/design/00-spine.md §3): the 120-129 decade (ownership) and the
## 150-159 decade (recruitment) are this lane's. Burned so far:
## SALT_OWN_INBOUND (120), SALT_OWN_TERMS (121), SALT_OWN_BUYOUT (122),
## SALT_RECRUIT_ARRIVALS (150), SALT_RECRUIT_PROFILE (151),
## SALT_RECRUIT_ACCEPT (152), SALT_RECRUIT_COUNTER (153).
##
## TWIN LAW: this file and unity/Assets/Scripts/Core/Lanes/SimOwnership.cs
## carry the same logic in the same order.

# ═══════════════════════════ THE SPINE'S ENTRY POINTS ═══════════════════════

## Tick §9, with the financial lanes. Neutral: nothing vests until the lane lands.
static func tick_pre(_state: GameState, _rep: Dictionary) -> void:
	pass

## The money section. Will write ONLY `m["recruit_ads"]`; neutral until filled.
static func tick_money(_state: GameState, _rep: Dictionary, _m: Dictionary) -> void:
	pass

## After the record is written. Neutral until the lane lands.
static func tick_post(_state: GameState, _rep: Dictionary) -> void:
	pass

## DM context lines (the spine caps the block). Empty until the lane lands.
static func directives(_state: GameState) -> Array[String]:
	return []

## Attention rows {desk, key, severity, label}. Empty until the lane lands.
static func attention(_state: GameState) -> Array:
	return []
