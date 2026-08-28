class_name SimPivot
extends RefCounted
## LANE — THE PIVOT (the escape hatch). Spec: docs/design/DECISIONS.md
## § THE PIVOT (owner-specified mechanic) + docs/design/12-binder-rework-2.md
## § pivot. W2 lane: L-COMPANY.
##
## Pivoting is the classic startup escape hatch: the money in the bank
## survives, and what you burn depends on the axis you pivot along. Debts and
## obligations survive BOTH pivots — the bank does not forget.
##
##   AUDIENCE PIVOT (change customer type): customers → 0, and ALL market-side
##   learning dies — named deals and leads, channel learning, content equity
##   (the well drains), word-of-mouth base, market beliefs re-fog. What
##   survives: the product (as built), the team (employees are contracts, not
##   traction), the cash, the debts.
##
##   PRODUCT PIVOT (same audience, new product): customers take a uniform
##   random 50–100% loss; ALL product advances die (quality, version, roadmap
##   bets — tech debt clears with the codebase it lived in); what survives:
##   channel/marketing/sales learning (content equity, CAC learning, the
##   relationships), the cash, the debts. In-flight Enterprise deals survive
##   as named leads knocked back to the earliest stage.
##
## THE FLOW (the desk's two-tap + typed PIVOT arms it; the world fires it):
##   arm_audience / arm_product   → a durable FLAG carries the intent
##   disarm                       → Esc-grade abandon, the flag dies
##   resolve_armed                → called at the next LOCK IN (the week-turn
##                                  seam calls this; the pivot applies, the DM
##                                  narrates, regeneration re-dresses the run)
## The executors are also directly callable as the DM ops the op registry
## names: pivot_audience / pivot_product (DECISIONS: op names are FIXED).
##
## This module is OP-DRIVEN: it owns no tick seam. tick_pre/tick_money/
## tick_post exist only to keep the lane-module shape and are never wired.
##
## SALT: SALT_PIVOT_LOSS = 170 — a fresh decade (170-179, pivot), burned here
## for the product pivot's 50–100% customer roll. Registry listing rides the
## coordinator package (this lane does not edit sim_engine.gd).
##
## TWIN LAW: this file and unity/Assets/Scripts/Core/Lanes/SimPivot.cs carry
## the same logic in the same order. The two engines do not share PRNG
## internals, so the loss ROLL is pinned per-engine (determinism + range),
## never across them.

## The product pivot's one draw: the uniform 50–100% customer loss.
const SALT_PIVOT_LOSS := 170

## The durable intent flags (state.flags — saved, byte-identical keys both
## engines). The audience flag carries its target after the colon; the product
## flag carries an optional new craft the same way.
const FLAG_AUD := "pivot_armed_audience"
const FLAG_PROD := "pivot_armed_product"

const AUDIENCES: Array[String] = ["Enterprise", "SMB", "Consumer"]
const CRAFTS: Array[String] = ["Software", "Hardware", "Marketplace", "Service"]

# ═══════════════════════════ THE ARM / DISARM SURFACE ════════════════════════

## Arm the audience pivot at `new_who`. False when the target is not a real
## audience or is the one the company already serves.
static func arm_audience(state: GameState, new_who: String) -> bool:
	if not AUDIENCES.has(new_who) or new_who == state.biz_who:
		return false
	disarm(state)
	state.flags.append(FLAG_AUD + ":" + new_who)
	state.log_action("ARMED the audience pivot: %s → %s (fires at LOCK IN)"
		% [state.biz_who, new_who])
	return true

## Arm the product pivot. `new_what` may be "" (same craft, new product) or a
## different craft. False only on a nonsense craft.
static func arm_product(state: GameState, new_what: String = "") -> bool:
	if new_what != "" and not CRAFTS.has(new_what):
		return false
	disarm(state)
	state.flags.append(FLAG_PROD + (":" + new_what if new_what != "" else ""))
	state.log_action("ARMED the product pivot%s (fires at LOCK IN)"
		% ((": craft → " + new_what) if new_what != "" else ""))
	return true

## Esc-grade abandon: every pivot intent dies, nothing else moves.
static func disarm(state: GameState) -> void:
	for i in range(state.flags.size() - 1, -1, -1):
		var f := String(state.flags[i])
		if f == FLAG_AUD or f == FLAG_PROD \
				or f.begins_with(FLAG_AUD + ":") or f.begins_with(FLAG_PROD + ":"):
			state.flags.remove_at(i)

## The armed intent, or {}: {kind: "audience"|"product", target: String}.
static func armed(state: GameState) -> Dictionary:
	for f in state.flags:
		var s := String(f)
		if s == FLAG_AUD or s.begins_with(FLAG_AUD + ":"):
			return {"kind": "audience", "target": s.substr(FLAG_AUD.length() + 1)
				if s.length() > FLAG_AUD.length() else ""}
		if s == FLAG_PROD or s.begins_with(FLAG_PROD + ":"):
			return {"kind": "product", "target": s.substr(FLAG_PROD.length() + 1)
				if s.length() > FLAG_PROD.length() else ""}
	return {}

## The LOCK IN seam calls this once per week-turn: an armed pivot fires, the
## flag dies with the arming, and the receipt comes back for the narration.
## {} when nothing was armed (the overwhelmingly common week).
static func resolve_armed(state: GameState) -> Dictionary:
	var a := armed(state)
	if a.is_empty():
		return {}
	disarm(state)
	if String(a.kind) == "audience":
		return pivot_audience(state, String(a.target))
	return pivot_product(state, String(a.target))

# ═══════════════════════════ THE EXECUTORS (DM op names, FIXED) ══════════════

## AUDIENCE PIVOT — customers → 0, the market-side learning dies, the product
## and the team survive. Returns the receipt: {ok, kind, lines[], ...}.
static func pivot_audience(state: GameState, new_who: String) -> Dictionary:
	if not AUDIENCES.has(new_who) or new_who == state.biz_who:
		return {"ok": false, "kind": "audience",
			"reason": "the new audience must be a real one you do not already serve"}
	var old_who := state.biz_who
	var lost := state.traction
	var well := int(round(state.content_equity))
	var deals := state.leads.size()
	# ── what dies: the market side, whole
	state.traction = 0
	state.leads = []
	state.logos = []
	state.pipe_units = 0.0
	state.pipe_churn_acc = 0.0
	state.pipe_stats = {}
	state.content_equity = 0.0
	state.beliefs = {}          # re-fog: the next tick reseeds first guesses
	# ── the new market: the world reprices itself for who you now serve
	state.biz_who = new_who
	state.theta = SimEngine.default_theta(state.biz_what, new_who)
	# ── the record
	state.pivots += 1
	state.set_flag("pivoted")
	state.log_action("THE PIVOT (audience): %s → %s — %d customers released"
		% [old_who, new_who, lost])
	var lines: Array[String] = [
		"audience pivot: %s → %s" % [old_who, new_who],
		"%d customers released — traction starts over" % lost,
		"%d named deals died with the market that held them" % deals,
		"the content well drained ($%d of equity)" % well,
		"market beliefs re-fogged — the first guesses return",
		"the product survives as built · the team stays · the debts stay",
	]
	return {"ok": true, "kind": "audience", "lines": lines,
		"lost_customers": lost, "well_drained": well, "deals_dead": deals,
		"old_who": old_who, "new_who": new_who}

## PRODUCT PIVOT — the 50–100% roll decides who stays; the product advances
## die; the market learning survives. `new_what` "" keeps the craft.
static func pivot_product(state: GameState, new_what: String = "") -> Dictionary:
	if new_what != "" and not CRAFTS.has(new_what):
		return {"ok": false, "kind": "product",
			"reason": "the new craft is not one the world knows"}
	var old_what := state.biz_what
	var old_product := state.product
	var old_debt := int(round(state.tech_debt))
	var bets_dead := state.bets.size()
	var before := state.traction
	# ── the roll: uniform 50–100% of the customers walk (SALT_PIVOT_LOSS)
	var loss := SimEngine.rng_for(state, SALT_PIVOT_LOSS).randf_range(0.5, 1.0)
	var kept := int(floor(float(before) * (1.0 - loss)))
	var lost := before - kept
	state.traction = kept
	# ── what dies: the product side, whole
	state.product = 10                  # v0.62 → v0.1
	state.bets = []
	state.platform_level = 0
	state.tech_debt = 0.0               # the debt clears with its codebase
	state.features = []
	state.served_total = 0              # serving practice was practice ON the product
	if not state.hardware.is_empty():
		state.hardware["stock"] = 0                # shelved units of a dead product
		state.hardware["produced_total"] = 0       # the build curve restarts
		state.hardware["demand_ema"] = 0.0
		state.hardware["production_target"] = -1
	# ── the relationships survive: named deals knock back to the first meeting
	var knocked := state.leads.size()
	for l in state.leads:
		var ld: Dictionary = l
		ld["stage"] = "meeting"
		ld["age_weeks"] = 0
	# signed logos are customers: the same roll decides who stays (newest kept)
	var keep_logos := int(round(float(state.logos.size()) * (1.0 - loss)))
	if keep_logos < state.logos.size():
		state.logos = state.logos.slice(state.logos.size() - keep_logos)
	state.pipe_units = 0.0              # unnamed interest was in the old product
	# pipe_stats stays — CAC and cycle learning are the sales team's, not the product's
	# ── the craft, when it changes; the world reprices what you now make
	if new_what != "":
		state.biz_what = new_what
	state.theta = SimEngine.default_theta(state.biz_what, state.biz_who)
	# ── the record
	state.pivots += 1
	state.set_flag("pivoted")
	state.log_action("THE PIVOT (product): v0.%d → v0.1%s — %d of %d customers walked"
		% [maxi(1, old_product / 10), ((" · craft → " + state.biz_what) if new_what != "" else ""),
		lost, before])
	var lines: Array[String] = [
		"product pivot%s" % ((": %s → %s" % [old_what, state.biz_what]) if new_what != "" else ""),
		"the roll took %d%% — %d of %d customers walked" % [int(round(loss * 100.0)), lost, before],
		"v0.%d → v0.1 — the advances died with the codebase" % maxi(1, old_product / 10),
		"%d bets died on the wall · the plumbing debt cleared (−%d)" % [bets_dead, old_debt],
		"%d named deals knocked back to the first meeting" % knocked,
		"channel learning, the well and the relationships survive · the debts stay",
	]
	return {"ok": true, "kind": "product", "lines": lines,
		"lost_customers": lost, "kept_customers": kept,
		"loss_pct": int(round(loss * 100.0)), "old_version": maxi(1, old_product / 10),
		"bets_dead": bets_dead, "debt_cleared": old_debt, "deals_knocked": knocked}

# ═══════════════════════════ THE PREVIEW (pure, no mutation) ═════════════════

## THE PREVIEW the desk prints before the arm — computed from live state,
## never asserted, and it must not touch a single field. The product roll is
## shown as its honest RANGE: the die is cast at the press, not before.
static func preview(state: GameState, kind: String) -> Dictionary:
	var debt_wk := SimBank.debt_total(state)
	if kind == "audience":
		return {
			"kind": "audience",
			"customers_lost": state.traction,
			"well": int(round(state.content_equity)),
			"deals_dead": state.leads.size(),
			"version": "v0.%d" % maxi(1, state.product / 10),
			"debts": debt_wk,
		}
	return {
		"kind": "product",
		"customers_at_risk": state.traction,
		"version_from": "v0.%d" % maxi(1, state.product / 10),
		"version_to": "v0.1",
		"bets_dead": state.bets.size(),
		"debt_cleared": int(round(state.tech_debt)),
		"deals_knocked": state.leads.size(),
		"debts": debt_wk,
	}

# ═══════════════════════════ THE SPINE'S ENTRY POINTS ════════════════════════
## Kept for lane-module shape. THE PIVOT IS OP-DRIVEN: none of the three tick
## hooks is wired into sim_engine, and none may ever mutate state.

static func tick_pre(_state: GameState, _rep: Dictionary) -> void:
	pass

static func tick_money(_state: GameState, _rep: Dictionary, _m: Dictionary) -> void:
	pass

static func tick_post(_state: GameState, _rep: Dictionary) -> void:
	pass

## DM context: an armed pivot is the week's loudest fact — the DM builds the
## tension and NEVER resolves it (the LOCK IN seam owns the resolution).
static func directives(state: GameState) -> Array[String]:
	var a := armed(state)
	if a.is_empty():
		return []
	var toward := (" toward " + String(a.target)) if String(a.target) != "" else ""
	return [("THE PIVOT: the founder has ARMED a %s pivot%s. It resolves at this "
		+ "week's LOCK IN — narrate the held breath; do not resolve it yourself.")
		% [String(a.kind), toward]]

## Attention: the armed pivot is a sev-3 alarm until it fires or is disarmed.
## (Fan-in to SimEngine.attention_items rides the coordinator package.)
static func attention(state: GameState) -> Array:
	var a := armed(state)
	if a.is_empty():
		return []
	return [{"desk": "pivot", "key": "pivot_armed", "severity": 3,
		"label": "the pivot is armed — it fires at LOCK IN", "control": "disarm"}]
