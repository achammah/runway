class_name SimCatalog
extends RefCounted
## LANE 01 — THE CATALOG (offers, prices, itemized costs). Spec: docs/design/01-catalog.md
##
## THE LAW, unchanged: the ENGINE owns every number (every line, every total,
## every clamp); the DM owns sentences; the LLM proposes terms that are ALWAYS
## shown for adjustment and only enter the books through this lane's clamped
## door after the founder confirms.
##
## NORTH STAR: this subsystem teaches unit economics by their real names — COGS,
## fixed vs variable cost, contribution margin, break-even — with receipts that
## say WHY a number moved.
##
## WHAT LIVES WHERE. `SimEngine` already owns the arithmetic half: the cost-line
## sync (F1), the catalog overhead sum (F2), COGS per customer (F3), the learning
## curve (F4), demand/pain/arpu (F5), and the scalar clamps inside `add_offer`.
## THIS FILE is the lane's own half, and it is exactly three things:
##
##   1. THE DOOR — `add_offer` here is the only entry the desks and the DM should
##      use, because the ERA SHELF CAP and the Σ-WEIGHT BUDGET live at the door,
##      not in the engine's scalar clamps (D1: the arpu exploit is closed
##      structurally, by an engine clamp, never by UI politeness).
##   2. PROPOSAL TIME — the keyless draft (F7 v2), era-scaled tooling and a
##      seeded jitter on salt 11, so two keyless runs never sell the identical
##      workshop at the identical price and a replay still lands on the same one.
##   3. THE PEDAGOGY — contribution margin, break-even, and the one lesson a
##      founder must not miss (a price under its own variable cost), computed
##      once here so the desk, the receipts and the attention row can never
##      disagree about the number or the words.
##
## The spine calls, in tick order (docs/design/00-spine.md §1, HOOKS.md):
##   tick_pre   tick §8 — the shelf invariant, before the market reads weights
##   tick_money the money section — the catalog's receipts, in their real names
##   tick_post  after the week's record is written and can be read back
## and outside the tick: directives() feeds the DM block, attention() feeds every
## bang in the game through SimEngine.attention_items.

# ─────────────────────────────── the constants ───────────────────────────────
## THE SHELF (docs/design/DECISIONS.md, standing recommendation): how many offers
## a company of this stage can actually keep on the shelf, and how much of one
## customer's finite weekly wallet the whole catalog may claim.
const ERA_OFFER_CAP := {"garage": 2, "coworking": 3, "office": 5, "floor": 8, "hq": 8}
## THE TOOLING A STAGE IS QUOTED (D2): era pressure enters at PROPOSAL time only.
## A floor-era founder is QUOTED heavier tooling; the quoted number then stays the
## number until they step it by hand. A receipt that silently grows with promotion
## would be a hidden multiplier, which is the one sin this whole subsystem exists
## to refuse.
const ERA_TOOL_SCALE := {"garage": 1.0, "coworking": 1.4, "office": 2.2,
	"floor": 4.0, "hq": 7.0}
## Σ of every offer's weight, whole catalog. A customer's weekly budget is finite,
## so a spammed catalog cannot mint arpu (D1, share-of-wallet).
const SHELF_WEIGHT_CAP := 6.0
const MIN_WEIGHT := 0.2
const MAX_WEIGHT := 3.0
## 1-4 variable lines, 0-3 fixed lines: past that the fine print stops being fine
## print and the DETAIL card stops fitting the sheet.
const MAX_COST_LINES := 4
const MAX_FIXED_LINES := 3
const MAX_LABEL := 24

# ──────────────────────────────── the shelf ──────────────────────────────────

## How many offers this stage holds. Era demotion never deletes an offer — the
## cap gates the door, and a company that fell back to the garage keeps the five
## things it was selling.
static func offer_cap(state: GameState) -> int:
	return int(ERA_OFFER_CAP.get(state.era, 2))

## Σ weight across the catalog — the slice of a customer's wallet already spoken for.
static func shelf_weight(state: GameState) -> float:
	var total := 0.0
	for o in state.offers:
		total += float((o as Dictionary).get("weight", 1.0))
	return total

## What is left of the wallet, never below zero.
static func weight_room(state: GameState) -> float:
	return maxf(SHELF_WEIGHT_CAP - shelf_weight(state), 0.0)

## True when nothing more can be shelved — either the stage is out of slots or
## there is no wallet left for even a minimum-weight offer.
static func shelf_full(state: GameState) -> bool:
	return state.offers.size() >= offer_cap(state) or weight_room(state) < MIN_WEIGHT

## Why the door is shut, in the desk's own voice. Empty while it is open.
static func shelf_full_line(state: GameState) -> String:
	if state.offers.size() >= offer_cap(state):
		return "the shelf is full at this stage — drop something first"
	if weight_room(state) < MIN_WEIGHT:
		return "the catalog already claims a whole customer's wallet — drop something first"
	return ""

# ──────────────────────── the door into the books (F6) ───────────────────────

## THE ONLY DOOR. Every path that puts an offer on the shelf comes through here:
## the review card, a DM `price_offer` naming something that does not exist, a
## future op. Order of operations is F6's, and it is deliberate:
##   1. REFUSE first, so a full shelf costs nothing and says so;
##   2. narrow the weight to what the wallet has left BEFORE the engine's own
##      [0.2, 3.0] clamp sees it — the lane door narrows, the engine floor holds;
##   3. sanitize the lines (count, label, numeric) so `sync_offer_costs` is
##      handed receipts it can believe;
##   4. hand the whole thing to SimEngine.add_offer, which owns the scalar clamps
##      and the sync. Nothing here re-implements a clamp the engine already has.
## Returns the new offer, or {} when the shelf refused it.
static func add_offer(state: GameState, o_name: String, unit: String,
		fair: float, cost: float, elasticity: float, weight: float,
		cost_lines: Array = [], fixed_lines: Array = []) -> Dictionary:
	if state.offers.size() >= offer_cap(state):
		return {}
	var room := weight_room(state)
	if room < MIN_WEIGHT:
		return {}
	var w := clampf(weight, MIN_WEIGHT, minf(MAX_WEIGHT, room))
	return SimEngine.add_offer(state, o_name, unit, fair, cost, elasticity, w,
		sanitize_lines(cost_lines, MAX_COST_LINES),
		sanitize_lines(fixed_lines, MAX_FIXED_LINES))

## Dropping is instant behind the desk's two-tap arm (DECISIONS.md): the revenue
## consequence is the natural cost, and a wind-down would only teach the founder
## to fear the shelf.
static func remove_offer(state: GameState, idx: int) -> bool:
	return SimEngine.remove_offer(state, idx)

## Receipts the engine can believe: at most `cap` of them (extras drop from the
## tail), a stripped label of 24 characters that is never blank, and a number
## where a number belongs. The AMOUNTS are not clamped here — `sync_offer_costs`
## owns that, against the fair price, and owning it twice is how two clamps
## start disagreeing.
static func sanitize_lines(lines: Array, cap: int) -> Array:
	var out: Array = []
	for l in lines:
		if out.size() >= cap:
			break
		if not (l is Dictionary):
			continue
		var ld: Dictionary = l
		var label := String(ld.get("label", "")).strip_edges().substr(0, MAX_LABEL)
		if label == "":
			label = "line"
		var amount := 0.0
		var raw = ld.get("amount", 0.0)
		if raw is float or raw is int:
			amount = float(raw)
		out.append({"label": label, "amount": amount})
	return out

# ───────────────────────── proposal time: the draft (F7) ─────────────────────

## What this audience pays, relative to an SMB. Consumer pays a quarter,
## Enterprise four times — and the costs scale with the price, so margin holds.
static func audience_scale(who: String) -> float:
	match who:
		"Consumer":
			return 0.25
		"Enterprise":
			return 4.0
	return 1.0

static func tool_scale(era: String) -> float:
	return float(ERA_TOOL_SCALE.get(era, 1.0))

## THE KEYLESS DRAFT v2 (F7). Cost-plus estimation at roughly a 65% gross margin,
## era-scaled tooling, and one seeded jitter so the house numbers are not a fixed
## price list. Returns the SAME SHAPE the model returns (`variable_costs` /
## `fixed_costs_wk`), so the review card has exactly one road to read.
##
## THE ONE DRAW COMES FIRST and it is the only draw: same (seed, week) ⇒ same
## draft, so a replay shelves the identical offer. Salt 11 is this lane's, and
## this is its only draw-site (docs/design/00-spine.md §3).
static func draft_terms(state: GameState, idea: String) -> Dictionary:
	var jitter := SimEngine.rng_for(state, SimEngine.SALT_CATALOG_JITTER).randf_range(0.8, 1.3)
	# the engine's own draft supplies the name and sniffs the billing unit out of
	# the founder's words; this lane re-prices it and itemizes the cost sheet.
	var terms: Dictionary = SimEngine.draft_offer_terms(state, idea)
	var aud := audience_scale(state.biz_who)
	var fair := maxf(roundf(40.0 * aud * jitter), 1.0)
	var materials := roundf(fair * 0.20)
	var labor := roundf(fair * 0.15)
	terms["fair_price"] = fair
	terms["unit_cost"] = materials + labor
	terms["elasticity"] = 2.0
	terms["weight"] = 1.0
	# GENERIC ON PURPOSE (L2): a keyed run answers with this business's own
	# vocabulary ("cold-chain packaging", "a barista's hour"), so the house
	# labels must read visibly plainer than the street's.
	terms["variable_costs"] = [
		{"label": "materials & delivery", "amount": materials},
		{"label": "labor share", "amount": labor}]
	terms["fixed_costs_wk"] = [
		{"label": "tools & subscriptions", "amount": round(15.0 * aud * tool_scale(state.era))}]
	return terms

# ─────────────────────────── the pedagogy, computed once ─────────────────────

## What one sale costs to serve TODAY — the founder's stepped line amounts, times
## the one learning factor, applied at the total and never per line (F4: the
## stepped numbers are receipts and must stay exactly what the founder set).
static func served_unit_cost(offer: Dictionary, lc: float) -> float:
	return float(offer.get("unit_cost", 0.0)) * lc

## CONTRIBUTION MARGIN — price minus variable cost, per unit. The number that
## decides whether volume is a business or a hobby.
##
## `fair_mult` is THE STREET'S price, not yours (03 §5.1): while a rival's price
## war runs, an UNPRICED offer follows the going rate down, so its margin is
## genuinely thinner that week. A named price ignores it — the founder's number
## is the founder's number.
static func contribution(offer: Dictionary, lc: float, fair_mult: float = 1.0) -> float:
	return SimEngine.offer_billed_price(offer, fair_mult) - served_unit_cost(offer, lc)

## BREAK-EVEN — how many sales a week pay for this offer's standing tools.
## −1 when the price never pays for itself, because there is no such number and
## printing a big one instead of the lesson would be the kinder lie.
static func break_even(offer: Dictionary, lc: float, fair_mult: float = 1.0) -> int:
	var margin := contribution(offer, lc, fair_mult)
	if margin <= 0.0:
		return -1
	return int(ceil(float(offer.get("fixed_wk", 0.0)) / margin))

## THE ONE MISTAKE A FOUNDER MUST NOT MISS: every sale loses money. A conscious
## $0 giveaway is NOT this — it is a strategy the founder chose, priced at zero on
## purpose, and the desk says so in blue. This is a named price that sits under
## its own variable cost.
static func never_pays(offer: Dictionary, lc: float) -> bool:
	var price := float(offer.get("price", 0.0))
	if price <= 0.0:
		return false
	return price <= served_unit_cost(offer, lc)

# ───────────────────────────────── the tick ──────────────────────────────────

## Tick §8, before adoption: THE SHELF INVARIANT.
##
## `add_offer` guards the door, but state can arrive by other roads — a world
## bible, a hand-edited save, a legacy run from before the cap existed. Σ weight
## is what turns customers into revenue (D1: weight stays ABSOLUTE in arpu), so
## if it is ever allowed past 6.0 the catalog mints money. It is trimmed here,
## every week, in both engines — and it RECEIPTS when it bites, because a clamp
## that moves a number in silence is exactly the hidden multiplier this
## subsystem refuses to contain.
static func tick_pre(state: GameState, rep: Dictionary) -> void:
	if state.offers.is_empty():
		return
	var total := shelf_weight(state)
	if total <= SHELF_WEIGHT_CAP + 0.001:
		return
	var k := SHELF_WEIGHT_CAP / total
	for o in state.offers:
		var od: Dictionary = o
		od["weight"] = clampf(float(od.get("weight", 1.0)) * k, MIN_WEIGHT, MAX_WEIGHT)
	rep["lines"].append(
		"the shelf only holds so much: catalog weights trimmed to Σ%.1f — one customer's weekly wallet is finite"
		% shelf_weight(state))

## The money section. The catalog's P&L lanes (`cogs`, `offer_fixed`) are already
## assembled by the engine above this call — this lane does not write a second
## number over them. What it DOES own is the RECEIPTS: the engine's working lines
## name the money, and the lane names the CONCEPT, which is the whole pedagogy
## contract (§6: COGS, fixed vs variable, billed sold or not).
##
## The upgrade happens in place, matched by the engine's own prefix, so nothing
## ever prints twice. If the engine's wording ever changes, the match simply
## misses and its line stands as written — a lane may sharpen the spine's voice,
## never shout over it.
static func tick_money(state: GameState, rep: Dictionary, m: Dictionary) -> void:
	var lines: Array = rep.get("lines", [])
	var cogs := int(round(float(m.get("cogs", 0.0))))
	if cogs >= 1:
		# the same learning factor the week's record carries, so the journal and
		# the ledger can never disagree about what the curve did
		var lc := SimEngine.learning_curve(state)
		var learned := (", learning ×%.2f" % lc) if lc < 0.995 else ""
		_reword(lines, "cost of serving customers: $",
			"COGS $%d — serving %d customers (variable cost × volume%s)"
			% [cogs, state.traction, learned])
	var fixed := int(round(float(m.get("offer_fixed", 0.0))))
	if fixed >= 1:
		_reword(lines, "catalog overheads: $",
			"fixed costs — the catalog's standing tools: $%d/wk (billed sold or not)" % fixed)

## Replace the last line carrying `prefix`. Silent when there is none.
static func _reword(lines: Array, prefix: String, replacement: String) -> void:
	for i in range(lines.size() - 1, -1, -1):
		if String(lines[i]).begins_with(prefix):
			lines[i] = replacement
			return

## The catalog's books close inside the money section — nothing here needs the
## finished week, and a hook that does nothing costs the tick nothing.
static func tick_post(_state: GameState, _rep: Dictionary) -> void:
	pass

## DM context lines, section 5 of the DIRECTIVES block (docs/design/00-spine.md §5).
##
## The composer already prints WHAT is on sale and at what price, one line per
## offer. What it cannot say — and what makes the difference between a narrator
## inventing costs and one reading them — is what a sale COSTS and what the shelf
## carries whether or not anything sells. Two lines, aggregate on purpose: the
## per-offer serve cost belongs on the composer's own on-sale line, and printing
## it again here would be the same fact twice in a 24-line budget.
static func directives(state: GameState) -> Array[String]:
	var out: Array[String] = []
	if state.offers.is_empty():
		return out
	var serve := SimEngine.offers_cogs_per_customer(state)
	if serve >= 1.0:
		out.append("- Serving one customer costs ~$%d/wk (COGS — variable cost, it bills only when they buy)."
			% int(round(serve)))
	var fixed := SimEngine.offers_fixed_wk(state)
	if fixed >= 1.0:
		out.append("- The catalog carries $%d/wk of standing tool costs, sold or not."
			% int(round(fixed)))
	return out

## Attention rows — the pricing desk (docs/design/00-spine.md §4).
##
## The `unpriced` row of the registry is filed by the spine itself and is NOT
## repeated here: one condition, one row, or the ticker starts stuttering.
##
## What this lane adds is the row the spec calls the one lesson a founder must
## not miss (§6): a NAMED price sitting under its own variable cost, which loses
## money on every single sale and does it more the better the marketing works.
## It is a warn, so it reaches the pre-roll review and stops the dice — losing
## money per unit is worth one more look before a week is spent scaling it.
static func attention(state: GameState) -> Array:
	var rows: Array = []
	if state.offers.is_empty():
		return rows
	var lc := SimEngine.learning_curve(state)
	for o in state.offers:
		if never_pays(o as Dictionary, lc):
			rows.append({"desk": "pricing", "key": "losing_price", "severity": 2,
				"label": "a price below its variable cost", "control": "losing_price"})
			break
	return rows
