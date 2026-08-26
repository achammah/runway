class_name SimDivisions
extends RefCounted
## LANE — DIVISIONS & SITES. Spec: docs/design/DECISIONS.md (THE DIVISION
## MECHANIC, the works scale ladder, ARRANGE MODE) + docs/design/DAG2.md.
##
## STUB (DAG2 W1). The engine spine plants the entry points; W2 L-DIVWORKS
## fills the logic. Until then every hook is a no-op and the tick's arithmetic
## is byte-identical to a tree without this file.
##
## What lands here (W2): site records (open_site / close_site / edit_site with
## price-book costs), relocation + machine shipping, per-site demand weights,
## wage multipliers and learning counts, the SHARED/HQ row, group-by
## aggregators over records the engine already keeps (employee.site,
## machine.site, offer.product_id), and the deterministic rung rule —
## sites ≥ 2 → empire · offers ≥ 3 → house · else boutique. Divisions are
## NEVER generated: born only from real ops; the LLM names, never numbers.
##
## The spine calls, in tick order (docs/design/HOOKS.md):
##   tick_pre   tick §6c — sites settle (ramps, weights) before the market
##              splits demand by site
##   tick_money the money section — this lane owns the `site_rent` P&L lane
##              (per-site rents beside the era's own roof), zero until filled
##   tick_post  after the record — site flags read the finished week
## and outside the tick: directives() feeds the DM block, attention() feeds
## every bang in the game through SimEngine.attention_items.
##
## SALTS (docs/design/00-spine.md §3): the 130-139 decade is this lane's.
## Burned so far: SALT_DIV_SITES (130), SALT_DIV_NAMES (131).
##
## TWIN LAW: this file and unity/Assets/Scripts/Core/Lanes/SimDivisions.cs
## carry the same logic in the same order.

# ═══════════════════════════ THE SPINE'S ENTRY POINTS ═══════════════════════

## Tick §6c. Neutral: no sites move until the lane lands.
static func tick_pre(_state: GameState, _rep: Dictionary) -> void:
	pass

## The money section. Will write ONLY `m["site_rent"]`; neutral until filled.
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
