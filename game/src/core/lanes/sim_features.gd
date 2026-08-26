class_name SimFeatures
extends RefCounted
## LANE — THE FEATURE INVENTORY behind WHAT WE MAKE. Spec:
## docs/design/DECISIONS.md (PRODUCT desk — corrected understanding, THE
## KANBAN WALL, its scale ladder) + docs/design/DAG2.md.
##
## STUB (DAG2 W1). The engine spine plants the entry points; W2 L-MAKE fills
## the logic. Until then every hook is a no-op and the tick's arithmetic is
## byte-identical to a tree without this file.
##
## What lands here (W2): birth features from world gen, landed bets becoming
## feature records, family tags (ink — free), keep-costs, solidity states and
## creak taxes (tech debt made visible PER FEATURE, concentrated in the
## plumbing), per-unit impact on the works' ticket, shelf candidates priced
## inside price-book bands, and promised-vs-measured checks on fresh landings.
##
## The spine calls, in tick order (docs/design/HOOKS.md):
##   tick_pre   tick §7f — a bet that just landed becomes inventory before
##              anything reads the wall
##   tick_money the money section — feature keep-costs will bill here (the
##              lane's spend is part of what the product costs); neutral
##              until filled. No new P&L lane is pre-registered: keep-costs
##              are expected to ride existing lanes (rnd / offer cost lines)
##              per the L-MAKE design — if the lane needs its own column, that
##              is a coordinator package on the fixed money record.
##   tick_post  after the record — measured-vs-promised reads the finished week
## and outside the tick: directives() feeds the DM block, attention() feeds
## every bang in the game through SimEngine.attention_items.
##
## SALTS (docs/design/00-spine.md §3): the 140-149 decade is this lane's.
## Burned so far: SALT_FEAT_SHELF (140), SALT_FEAT_CREAK (141),
## SALT_FEAT_MEASURED (142).
##
## TWIN LAW: this file and unity/Assets/Scripts/Core/Lanes/SimFeatures.cs
## carry the same logic in the same order.

# ═══════════════════════════ THE SPINE'S ENTRY POINTS ═══════════════════════

## Tick §7f. Neutral: no bet becomes a feature until the lane lands.
static func tick_pre(_state: GameState, _rep: Dictionary) -> void:
	pass

## The money section. Neutral until the lane lands.
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
