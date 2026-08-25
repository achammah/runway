class_name SimLabor
extends RefCounted
## LANE 02 — THE LABOR MARKET (roles, applicants, raises, severance). Spec: docs/design/02-labor-market.md
##
## THE STUB the engine spine planted. Every entry point below is a no-op, and
## the weekly tick is arithmetically identical while they stay that way — that
## is what lets this lane be written against a live engine without touching a
## shared file. Fill the bodies here; the call sites already exist.
##
## The spine calls, in tick order (docs/design/00-spine.md §1, HOOKS.md):
##   tick_pre   tick §3b — arrivals, decay and the review cycle
##   tick_money the money section — write ONLY the P&L lanes this subsystem owns
##   tick_post  after the week's record is written and can be read back
## and outside the tick: directives() feeds the DM block, attention() feeds
## every bang in the game through SimEngine.attention_items.

## Tick §3b: arrivals (salt 20/21) → applicant decay (22) → review cycle,
## raise asks and resignations (23). The roster must be FINAL here: §4 reads it
## for morale and §9 pays it.
static func tick_pre(_state: GameState, _rep: Dictionary) -> void:
	pass

## The money section. `_m` is the working P&L record — one key per lane of
## docs/design/00-spine.md §2. Write only what this subsystem owns; the spine
## sums burn and writes the record whole.
static func tick_money(_state: GameState, _rep: Dictionary, _m: Dictionary) -> void:
	pass

## After the record is written: anything that needs the finished payroll.
static func tick_post(_state: GameState, _rep: Dictionary) -> void:
	pass

## DM context lines, section 6 of the DIRECTIVES block. Return plain
## strings; the spine orders them and caps the block.
static func directives(_state: GameState) -> Array[String]:
	return []

## Attention rows — the crew desk (applicants waiting, raise asks, thin span, poach).
## Each row is {desk, key, severity, label} with label ≤40 chars: the garage
## ticker prints it verbatim (docs/design/00-spine.md §4).
static func attention(_state: GameState) -> Array:
	return []


# ═══ COORDINATOR PARITY STUBS — lane 02 replaces every body below ═══
# Each returns its exact legacy default so the tick is byte-identical
# until the lane lands. Signatures are the arbitrated contract.

static func sales_capacity(_state: GameState, default_v: float) -> float:
	return default_v

static func design_mult(_state: GameState) -> float:
	return 1.0

static func care_eff(_state: GameState, b_care: float) -> float:
	return b_care

static func rnd_gain(_state: GameState, default_v: float) -> float:
	return default_v

static func debt_paydown(_state: GameState, default_v: float) -> float:
	return default_v

static func ops_mult(_state: GameState) -> float:
	return 1.0

## The dressing payload for the batch candidate call ({} = nobody arrived
## this week → no call fires).
static func dressing_payload(_state: GameState) -> Dictionary:
	return {}

## Order-matches model rows onto this week's applicants; returns applied count.
static func dress_applicants_rows(_state: GameState, _rows: Array) -> int:
	return 0

## THE POACH TARGET (03 §5.4 calls this; 02 owns the answer). Contract per
## 02's spec: the skill-max employee with market/salary >= 1.25 (pay_gap =
## (market - salary) / market >= 0.2). Keys: {index, name, salary,
## market_salary, pay_gap}. EMPTY = no target → the street's poach weight
## goes to zero — no shim, no fake wages.
static func poach_target(_state: GameState) -> Dictionary:
	return {}
