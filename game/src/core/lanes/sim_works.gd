class_name SimWorks
extends RefCounted
## LANE — THE WORKS (per-type capacity, the unit ticket, relief valves).
## Spec: docs/design/DECISIONS.md (the factory → THE WORKS, its SCALE LADDER)
## + docs/design/DAG2.md.
##
## STUB (DAG2 W1). The engine spine plants the entry points; W2 L-DIVWORKS
## fills the logic. Until then every hook is a no-op and the tick's arithmetic
## is byte-identical to a tree without this file.
##
## What lands here (W2): per-type capacity — service hours from the crew,
## software ceiling from care bandwidth, hardware machines (the factory lane's
## molecule, reused not forked), marketplace supply proxy; the unit ticket
## from the catalog's generated cost lines; relief valves priced against
## in-house (freelancers per session, cloud burst, the subcontract shop,
## recruit-supply / throttle demand) including recruit-supply bursts; learning
## curves per type (practice / automation / Wright / ops maturity); and the
## mutation-law ops routed here: refinance_note lives with the bank,
## fire_account / retire_product / stop-line notice periods compose with the
## price book's contract terms.
##
## The spine calls, in tick order (docs/design/HOOKS.md):
##   tick_pre   tick §7i — per-type capacity settles AFTER production (§7h)
##              and before the market reads it
##   tick_money the money section — this lane owns the `relief` P&L lane
##              (freelancers, burst, subcontract relief), zero until filled
##   tick_post  after the record — utilization and gap costs read the
##              finished week
## and outside the tick: directives() feeds the DM block, attention() feeds
## every bang in the game through SimEngine.attention_items.
##
## SALTS (docs/design/00-spine.md §3): the 160-169 decade is this lane's.
## Burned so far: SALT_WORKS_CAPACITY (160), SALT_WORKS_RELIEF (161),
## SALT_WORKS_REMAINDER (162).
##
## TWIN LAW: this file and unity/Assets/Scripts/Core/Lanes/SimWorks.cs
## carry the same logic in the same order.

# ═══════════════════════════ THE SPINE'S ENTRY POINTS ═══════════════════════

## Tick §7i. Neutral: capacity is whatever the older lanes computed until this lands.
static func tick_pre(_state: GameState, _rep: Dictionary) -> void:
	pass

## The money section. Will write ONLY `m["relief"]`; neutral until filled.
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
