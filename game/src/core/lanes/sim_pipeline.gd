class_name SimPipeline
extends RefCounted
## LANE 05 — THE ENTERPRISE PIPELINE (named leads, logos, renewals). Spec: docs/design/05-enterprise-pipeline.md
##
## Enterprise customers do not arrive as a coin flip. They are named accounts
## that took a meeting, sat in a pilot, survived a security review and signed a
## contract — and every seat inside them came out of the SAME demand the Bass
## block already generates. That is the whole law of this file:
##
##   CONSERVATION IS SACRED. §8's `adds` lands in a POOL (`pipe_units`). A spawn
##   DEBITS its seats from the pool; a lead that dies cold REFUNDS them. Nothing
##   here invents market and nothing destroys it — the pipeline only re-times and
##   re-chunks demand, so the tuned Enterprise curve survives untouched and what
##   the founder loses to a dead deal is TIME, not the market.
##
##   THE ENGINE OWNS EVERY NUMBER. The DM's only lever on this board is heat,
##   clamped ±40 (`push_lead`). Narration never advances a stage, never sets a
##   seat count and never signs a contract.
##
##   NON-ENTERPRISE RUNS ARE BYTE-IDENTICAL. Every entry point below leaves on
##   the activation gate, no stream is touched, no field is written. SMB and
##   Consumer tick exactly as they did before this file had a body.
##
## The spine calls, in tick order (docs/design/00-spine.md §1, HOOKS.md):
##   tick_pre   tick §8 — leads advance before the market is counted
##   tick_money the money section — write ONLY the P&L lanes this subsystem owns
##   tick_post  after the week's record is written and can be read back
## and outside the tick: directives() feeds the DM block, attention() feeds
## every bang in the game through SimEngine.attention_items.
##
## WHERE THE WEEK ACTUALLY RESOLVES: `adoption_net`, not `tick_pre`. The pool
## inflow is `adds` and the churn regime is `churn`, and neither exists until §8
## has computed them — so the whole pipeline runs in the adoption seam, in the
## spec's fixed draw order, on ONE salt-50 stream.

# ── the activation gate ──────────────────────────────────────────────────────
## Enterprise runs only. Everything in this file is behind this one predicate.
static func active(state: GameState) -> bool:
	return String(state.biz_who) == "Enterprise"

## THE SECOND GATE, read by the tick itself (§2.1). Demand generation and closing
## capacity are two different jobs in every real B2B org — marketing books the
## meetings, an AE moves them through gates. On Enterprise runs the gtm ceiling
## moves OUT of §8's adds and INTO `capacity_factor` on the stage advance, so the
## tick steps its own min() aside and the motion is taxed once instead of twice.
static func skips_gtm_cap(state: GameState) -> bool:
	return active(state)

# ── caps and constants (spec §1.3; all engine-side, all clamped) ─────────────
const LEAD_CAP := 8            ## live leads max — a real AE runs 10-15 open
                               ## opportunities; a founder juggling everything, fewer
const SPAWNS_PER_WK := 2       ## keeps the naming batch small and rare
const MIN_SEATS := 3
const POOL_CAP_FRAC := 0.25    ## × tam — 1000 units at the default Enterprise tam 4000
const HEAT_SPAWN_LO := 50
const HEAT_SPAWN_HI := 65
const HEAT_DECAY := 8          ## per week; −1 per sales head at floor/hq, max −3
const HEAT_DECAY_FLOOR := 4    ## account teams slow the rot, they never stop it
const HEAT_ADVANCE := 12       ## momentum on a stage advance — worth ~1.5 weeks at
                               ## HEAT_DECAY, not three. A gate cleared is real
                               ## momentum, but it must not be a full refill: at 25
                               ## every advance bought back most of the deal's
                               ## lifespan, so a deal that was moving at all could
                               ## never die, and no-decision — the thing this
                               ## subsystem exists to teach — stopped happening.
const PUSH_CLAMP := 40         ## push_lead v clamp
const BASE_ADV := {"meeting": 0.45, "pilot": 0.35, "procurement": 0.35, "contract": 0.40}
const P_ADV_MIN := 0.05
const P_ADV_MAX := 0.85
const PROCUREMENT_SEATS := 20  ## the seat count that wakes a buyer's IT department
const RENEW_EVERY := 26        ## weeks (floor/hq): the annual-contract cliff
const MAX_LINES := 6           ## pipeline receipts per week, then "…and N more moved"

## THE ERA LADDER (spec §8) — the same math everywhere, the CONSTANTS climb, so
## depth arrives as the company earns it. Startups sell design-partner pilots
## before they can sell procurement-grade contracts.
const SEAT_BANDS := [[3, 8], [9, 20], [21, 60], [61, 120]]
const SEAT_TIERS := {
	"garage":    [70, 25, 5, 0],
	"coworking": [60, 30, 10, 0],
	"office":    [45, 35, 17, 3],
	"floor":     [30, 35, 27, 8],
	"hq":        [20, 30, 35, 15],
}
## The knee of the size penalty: a 40-seat deal crawls for a garage founder and
## moves for an hq motion.
const SIZE_REF := {"garage": 10.0, "coworking": 16.0, "office": 28.0,
	"floor": 45.0, "hq": 70.0}

## THE KEYLESS NAME PATH (spec §10). The Markov seeds already carry Meridian,
## Vanta and Quill — the world names its own customers, with or without a key.
const ENT_SUFFIX := ["Logistics", "Systems", "Group", "Health", "Labs",
	"Industrial", "Financial", "Retail", "Foods", "Media"]

## One line per era: what this stage of the company's pipeline can and cannot do.
const COACH := {
	"garage": "design-partner pilots: small deals teach fastest",
	"coworking": "first real contracts — the ACV on a receipt is a year of one logo",
	"office": "procurement appears on 20+ seat deals — price fairness moves it",
	"floor": "renewals every 26 wks — care and quality decide them",
	"hq": "renewals every 26 wks — care and quality decide them",
}

## The heat ramp in words (spec §11) — the desk and the DM read the same scale.
static func heat_word(heat: int) -> String:
	if heat >= 75:
		return "hot"
	if heat >= 50:
		return "warm"
	if heat >= 25:
		return "cool"
	return "cold"

## The pool ceiling: a quarter of the addressable market can be in play at once.
static func pool_cap(state: GameState) -> float:
	return POOL_CAP_FRAC * float(state.theta.get("tam", 4000.0))

## Weekly heat decay. Account teams (floor+) hold a deal warm a little longer.
static func decay_for(state: GameState) -> int:
	var d := HEAT_DECAY
	if state.era_index() >= 3:
		d -= mini(_sales_heads(state), 3)
	return maxi(d, HEAT_DECAY_FLOOR)

## Weeks of silence a lead has left before it dies of no-decision.
static func weeks_to_cold(heat: int, decay: int) -> int:
	return int(ceil(float(heat) / float(maxi(decay, 1))))

static func _sales_heads(state: GameState) -> int:
	var n := 0
	for e in state.employees:
		if String((e as Dictionary).get("role", "")).contains("sales"):
			n += 1
	return n

## THE CLOSING CAPACITY `C` — the tick's own gtm_cap formula at cap_scale 1.0,
## REUSED, not re-invented. Demand generation is marketing's job (§2); this is
## the AE capacity that moves deals through gates.
static func capacity(state: GameState) -> float:
	var bud: Dictionary = state.budgets
	var mk_budget := float(int(bud.get("ads", 0)) + int(bud.get("content", 0))
			+ int(bud.get("referrals", 0)) + int(bud.get("outbound", 0))
			+ state.marketing_budget)
	return 1.5 + 0.8 * float(state.competences.get("sell", 3)) \
			+ 3.0 * float(_sales_heads(state)) + mk_budget / 400.0 \
			+ float(bud.get("sales", 0)) / 600.0

# ── §4 THE STAGE ADVANCE MATH ────────────────────────────────────────────────
## The probability this lead clears its current gate THIS week. Pure, closed
## form, no RNG — which is what lets the twin suites pin it to 1e-9 in both
## engines. `live` is the number of deals sharing the motion.
##
## Every factor is a real thing a founder can move:
##   capacity  AE capacity is finite and shared — more open deals slows all of them
##   quality   pilots convert on product (the tick's quality gate, gentler floor:
##             a pilot can limp where adoption cannot)
##   price     above-fair pricing stalls in evaluation and procurement
##   heat      deal momentum — stale deals slip, sponsored deals move
##   size      cycle length grows with deal size, against an era-scaled knee
static func lead_advance_p(state: GameState, lead: Dictionary, live: int) -> float:
	var f := advance_factors(state, lead, live)
	var stage := String(lead.get("stage", "meeting"))
	var p := float(BASE_ADV.get(stage, 0.40))
	for k in ["capacity", "quality", "price", "heat", "size"]:
		p *= float(f[k])
	return clampf(p, P_ADV_MIN, P_ADV_MAX)

## The five factors by name, so the receipt can say WHICH one carried the week.
static func advance_factors(state: GameState, lead: Dictionary, live: int) -> Dictionary:
	var dm := SimEngine.offers_demand_mult(state)
	return {
		# CAPACITY ONLY EVER SLOWS A DEAL. The ceiling is 1.0, not 1.5: a motion
		# with room to spare does not push a buyer through their own stage gate
		# faster than the gate opens — it just stops being the bottleneck. Letting
		# it accelerate made a starved board (0.6-1.6 live deals is normal at
		# Enterprise's demand rate) a permanent ×1.5 on every BASE_ADV, which won
		# 93% of deals even untended.
		"capacity": clampf(capacity(state) / (1.5 * float(maxi(live, 1))), 0.5, 1.0),
		"quality": 0.6 + float(state.product) / 100.0 * 0.8,
		"price": clampf(1.0 if dm < 0.0 else dm, 0.5, 1.3),
		"heat": 0.5 + float(int(lead.get("heat", 0))) / 100.0,
		"size": SimEngine.jano_down(float(int(lead.get("seats", MIN_SEATS))),
			float(SIZE_REF.get(state.era, 10.0)), 0.55),
	}

## The factor farthest above 1.0 — ties break in the listed order — turned into
## the sentence the journal prints. A receipt that only says "it moved" teaches
## nothing; this one names the lever the founder pulled.
static func _dominant_why(state: GameState, lead: Dictionary, live: int) -> String:
	var f := advance_factors(state, lead, live)
	var best := ""
	var best_v := 1.0
	for k in ["capacity", "quality", "price", "heat", "size"]:
		if float(f[k]) > best_v + 1e-9:
			best_v = float(f[k])
			best = String(k)
	match best:
		"capacity":
			return "the motion had room (%d live deals)" % maxi(live, 1)
		"quality":
			return "the demo held (product v0.%d)" % state.product
		"price":
			return "the price sat at fair"
		"heat":
			return "the room stayed warm"
		"size":
			return "a %d-seat deal moves fast" % int(lead.get("seats", MIN_SEATS))
	return "nobody found a reason to say no"

## The stage AFTER this one, for this deal, in this era. `procurement` only
## exists at office+ on deals ≥ 20 seats — a security review appears exactly
## when deal size makes a buyer's IT department wake up.
static func next_stage(state: GameState, lead: Dictionary) -> String:
	match String(lead.get("stage", "meeting")):
		"meeting":
			return "pilot"
		"pilot":
			if int(lead.get("seats", 0)) >= PROCUREMENT_SEATS and state.era_index() >= 2:
				return "procurement"
			return "contract"
		"procurement":
			return "contract"
	return "signed"

## What one seat bills per week — the offers catalog when there is one, the
## world's own arpu otherwise.
static func unit_rev_wk(state: GameState) -> float:
	var a := SimEngine.offers_arpu(state)
	if a >= 0.0:
		return a
	return float(state.theta.get("arpu_wk", 400.0)) * state.price_mult

# ── the spine's tick hooks ───────────────────────────────────────────────────

## Tick §8, before adoption. The board cannot move yet: `adds` (the pool inflow)
## and `churn` (the regime) are computed further down §8, so the whole weekly
## resolution lives in `adoption_net` below, on one stream in one fixed order.
## What happens here is the week's clean slate for the receipts.
static func tick_pre(state: GameState, rep: Dictionary) -> void:
	if not active(state):
		return
	rep["spawned_leads"] = []
	state.set_meta("pipe_spawned", [])

## The money section. The pipeline books NO new P&L lane — meetings, travel and
## demos ride the `sales` lever narratively, and a separate pipeline line would
## double-bill the same dollars (spec §9). What it does do is remember what
## acquisition COST, so the desk can divide it by the seats it actually bought.
static func tick_money(state: GameState, _rep: Dictionary, m: Dictionary) -> void:
	if not active(state):
		return
	var st := _stats(state)
	st["spend"] = float(st.get("spend", 0.0)) + float(m.get("marketing", 0.0)) \
			+ float(m.get("sales", 0.0))
	state.pipe_stats = st

## After the record is written. Every signed-contract fact — traction, the logo,
## the cycle, the signed-this-week marker — is booked at the close itself, inside
## the single salt-50 pass, so a replay lands on the same week.
##
## What is left is ONE line for another desk. The board (08) plans around the
## renewal calendar, so the finished week publishes it to `cap_renewal_line` and
## the cap table prints whatever it finds there — blank hides the line, so a run
## that is not on annual contracts simply never shows one. A published string,
## not a cross-desk call: neither lane has to know the other exists.
static func tick_post(state: GameState, _rep: Dictionary) -> void:
	if not active(state):
		return
	state.set_meta("cap_renewal_line", renewal_line(state))

## THE RENEWAL CALENDAR, in one line. The next three contracts up for renewal,
## soonest first — the board's whole question about enterprise revenue is "what
## has to be re-won, and when". "" before `floor`, where there are no annual
## contracts to lose yet, and the reading desk hides the line on "".
static func renewal_line(state: GameState) -> String:
	if not active(state) or state.era_index() < 3:
		return ""
	var due: Array = []
	for lg in state.logos:
		var lgd: Dictionary = lg
		var wk := int(lgd.get("renewal_wk", 0))
		if wk > 0 and wk - state.week <= 52:
			due.append(lgd)
	if due.is_empty():
		return "none inside a year"
	due.sort_custom(func(a: Dictionary, b: Dictionary) -> bool:
		var wa := int(a.get("renewal_wk", 0))
		var wb := int(b.get("renewal_wk", 0))
		if wa != wb:
			return wa < wb
		return String(a.get("name", "")) < String(b.get("name", "")))
	var parts: Array[String] = []
	for i in mini(due.size(), 3):
		var lg2: Dictionary = due[i]
		parts.append("%s (%d seats, wk %d)" % [String(lg2.get("name", "?")),
			int(lg2.get("seats", 0)), int(lg2.get("renewal_wk", 0))])
	var out := " · ".join(PackedStringArray(parts))
	if due.size() > 3:
		out += " · +%d more" % (due.size() - 3)
	return out

# ── THE ADOPTION SEAM — where the whole week resolves ────────────────────────
## `dflt` is the engine's seeded-remainder net; hand it back and every
## non-Enterprise run is untouched.
##
## On Enterprise runs this returns 0, and that is not a shrug: the pipeline has
## ALREADY moved `state.traction` itself, seat by seat, through named accounts —
## a close adds its seats, a churned logo takes all of its seats at once, an
## expansion adds the seats it grew. There is no smear left over for the spine to
## apply, and the salt-91 remainder is simply not consulted.
##
## THE DRAW ORDER IS THE SPEC (§9): churn → age/decay/death → advances/closes →
## expansion → spawns. One rng for mechanics (salt 50), one for names (salt 51),
## so a name-length draw can never shift a mechanics roll.
static func adoption_net(state: GameState, rep: Dictionary, adds: float,
		churn: float, dflt: int) -> int:
	if not active(state):
		return dflt

	if not rep.has("spawned_leads"):
		rep["spawned_leads"] = []
	var cap := pool_cap(state)
	# 8a ── POOL INFLOW. Fractional adds become units of unattached interest;
	# no rounding at all, which is strictly better than a remainder coin.
	state.pipe_units = minf(state.pipe_units + adds, cap)

	var r := SimEngine.rng_for(state, SimEngine.SALT_PIPELINE)
	var ctx := {"said": 0, "more": 0}
	var st := _stats(state)

	_churn_pass(state, rep, ctx, churn, r)
	_decay_pass(state, rep, ctx, st, cap)
	_advance_pass(state, rep, ctx, st, r)
	_expand_pass(state, rep, ctx, r)
	_spawn_pass(state, rep, ctx, st, r)

	state.pipe_stats = st
	if int(ctx["more"]) > 0:
		(rep["lines"] as Array).append("…and %d more moved" % int(ctx["more"]))
	# every seat is already booked to its account — nothing smears
	return 0

# ── §5 / §6 — churn, renewal cliffs, and whole-logo departures ───────────────
## The tick's churn stays THE churn — same formula, same knobs — but for
## Enterprise it lands on ACCOUNTS, not a smear of units. You lose logos, not
## fractions of logos, because enterprise revenue is contract-shaped.
static func _churn_pass(state: GameState, rep: Dictionary, ctx: Dictionary,
		churn: float, r: RandomNumberGenerator) -> void:
	var a := float(state.traction)
	var seated := 0
	for lg in state.logos:
		seated += int((lg as Dictionary).get("seats", 0))
	# DM side-sales, presets and legacy saves leave units nobody named. They keep
	# the old continuous path: floor + seeded coin, on this lane's own stream.
	var loose_units := maxi(state.traction - seated, 0)
	var loose_share := (float(loose_units) / a) if a > 0.0 else 0.0
	var loose := churn * loose_share
	var n := int(floor(loose))
	if r.randf() < loose - floor(loose):
		n += 1
	if n > 0:
		state.traction = maxi(state.traction - mini(n, loose_units), 0)

	if state.era_index() >= 3:
		_renewal_pass(state, rep, ctx, churn, a, r)
		return

	# BELOW `floor`: the accumulator batches the account share of the churn until
	# it is worth a whole logo, then takes one — never a partial account. The
	# expected units lost per week equal the old formula exactly.
	state.pipe_churn_acc += churn * (1.0 - loose_share)
	if state.logos.is_empty():
		return
	# the pick is drawn whenever there is anything to pick, so the stream position
	# never depends on the accumulator's value
	var pick := r.randi_range(0, state.logos.size() - 1)
	var lg2: Dictionary = state.logos[pick]
	var seats := int(lg2.get("seats", 0))
	if state.pipe_churn_acc < float(seats):
		return
	state.traction = maxi(state.traction - seats, 0)
	state.pipe_churn_acc -= float(seats)
	state.logos.remove_at(pick)
	_say(rep, ctx, "−%s churned — %d seats leave together (lifetime %d wks at v0.%d)" % [
		String(lg2.get("name", "an account")), seats,
		maxi(state.week - int(lg2.get("since_wk", state.week)), 0), state.product])

## AT `floor` THE RUN GRADUATES TO ANNUAL CONTRACTS. Renewal cliffs replace the
## accumulator for logos: revenue stops eroding and starts arriving in decisions,
## which is the truth of enterprise revenue.
##
## p_renew is the spec's formula, algebraically folded onto the churn the tick
## already computed:
##     churn = A/residence × churn_mult × status_churn × care_mult × price_pain
##  ⇒  (RENEW_EVERY/residence) × (that product of knobs) = RENEW_EVERY × churn / A
## so the cliff is calibrated to the continuous curve BY CONSTRUCTION — switching
## regimes cannot bend the churn curve, only make it cliff-shaped — and no knob
## has to be re-read (or drift) here.
static func _renewal_pass(state: GameState, rep: Dictionary, ctx: Dictionary,
		churn: float, a: float, r: RandomNumberGenerator) -> void:
	# logos signed before the era flipped get their first cliff scheduled now
	for lg in state.logos:
		if int((lg as Dictionary).get("renewal_wk", 0)) <= 0:
			(lg as Dictionary)["renewal_wk"] = state.week + RENEW_EVERY
	var p_renew := 0.98
	if a > 0.0:
		p_renew = clampf(1.0 - float(RENEW_EVERY) * churn / a, 0.50, 0.98)
	var kept: Array = []
	for lg2 in state.logos:
		var lgd: Dictionary = lg2
		if int(lgd.get("renewal_wk", 0)) != state.week:
			kept.append(lgd)
			continue
		var seats := int(lgd.get("seats", 0))
		if r.randf() < p_renew:
			lgd["renewal_wk"] = int(lgd["renewal_wk"]) + RENEW_EVERY
			kept.append(lgd)
			_say(rep, ctx, "RENEWED: %s — the annual contract holds (logo retention %d%%)" % [
				String(lgd.get("name", "an account")), int(round(p_renew * 100.0))])
		else:
			state.traction = maxi(state.traction - seats, 0)
			_say(rep, ctx, "LOST AT RENEWAL: %s — %d seats walk (care and quality decide renewals)" % [
				String(lgd.get("name", "an account")), seats])
	state.logos = kept

# ── §4 — age, heat decay, and death by no-decision ──────────────────────────
## Spawn heat 50-65 at decay 8 means an untouched lead dies in ~6-8 weeks: the
## "cold after N weeks" rule with N emergent and player-extendable. A dead lead
## REFUNDS its seats to the pool — ~40-60% of forecast B2B deals are lost to no
## decision, and those prospects stay in-market. What the founder lost is time.
static func _decay_pass(state: GameState, rep: Dictionary, ctx: Dictionary,
		st: Dictionary, cap: float) -> void:
	var decay := decay_for(state)
	var kept: Array = []
	for ld in state.leads:
		var lead: Dictionary = ld
		lead["age_weeks"] = int(lead.get("age_weeks", 0)) + 1
		lead["heat"] = maxi(int(lead.get("heat", 0)) - decay, 0)
		if int(lead["heat"]) > 0:
			kept.append(lead)
			continue
		var seats := int(lead.get("seats", MIN_SEATS))
		state.pipe_units = minf(state.pipe_units + float(seats), cap)
		st["lost"] = int(st.get("lost", 0)) + 1
		_say(rep, ctx, "gone cold: %s (%d seats) — %d wks of silence; enterprise deals die of no-decision, not a no" % [
			String(lead.get("name", "a prospect")), seats, int(lead["age_weeks"])])
	state.leads = kept

# ── §4 — the advance rolls, and the close ───────────────────────────────────
## One seeded roll per live lead per week, in array order (the order is part of
## the spec). At all-factors ≈ 1 the journey is ~8 weeks meeting→signed, 11-12
## through procurement — which under this game's compressed clock reads as the
## real 3-9-month enterprise cycle.
static func _advance_pass(state: GameState, rep: Dictionary, ctx: Dictionary,
		st: Dictionary, r: RandomNumberGenerator) -> void:
	var live := state.leads.size()
	if live == 0:
		return
	var kept: Array = []
	for ld in state.leads:
		var lead: Dictionary = ld
		if r.randf() >= lead_advance_p(state, lead, live):
			kept.append(lead)
			continue
		var nxt := next_stage(state, lead)
		if nxt == "signed":
			_close(state, rep, ctx, st, lead)
			continue
		var why := _dominant_why(state, lead, live)
		lead["stage"] = nxt
		lead["heat"] = mini(int(lead.get("heat", 0)) + HEAT_ADVANCE, 100)
		kept.append(lead)
		_say(rep, ctx, "%s moved to %s — %s" % [
			String(lead.get("name", "a prospect")), nxt, why])
	state.leads = kept

## THE CLOSE. The seats become customers, the account becomes a logo, and the
## receipt names ACV and the sales cycle — the two numbers an enterprise founder
## has to learn to say out loud.
static func _close(state: GameState, rep: Dictionary, ctx: Dictionary,
		st: Dictionary, lead: Dictionary) -> void:
	var seats := int(lead.get("seats", MIN_SEATS))
	var age := int(lead.get("age_weeks", 0))
	var name_v := String(lead.get("name", "an account"))
	state.traction = maxi(state.traction + seats, 0)
	state.logos.append({
		"name": name_v, "seats": seats, "since_wk": state.week,
		"renewal_wk": (state.week + RENEW_EVERY) if state.era_index() >= 3 else 0,
	})
	st["signed"] = int(st.get("signed", 0)) + 1
	st["seats_signed"] = int(st.get("seats_signed", 0)) + seats
	st["cycle_sum"] = int(st.get("cycle_sum", 0)) + age
	state.set_meta("pipe_signed_wk", state.week)
	var unit := unit_rev_wk(state)
	_say(rep, ctx, "SIGNED: %s — %d seats · ~$%s/wk (ACV ≈ %s) · cycle %d wks" % [
		name_v, seats, _grp(int(round(float(seats) * unit))),
		_acv(float(seats) * unit * 52.0), age])

## THE CLOSE, exposed. The twin suites drive this path directly (pin 4) and the
## desk's teaching footer is only honest if the close is the one place seats,
## logos and stats move together. Returns the seats booked, 0 for a bad index.
static func close_lead(state: GameState, i: int, rep: Dictionary) -> int:
	if i < 0 or i >= state.leads.size():
		return 0
	var lead: Dictionary = state.leads[i]
	var st := _stats(state)
	var ctx := {"said": 0, "more": 0}
	_close(state, rep, ctx, st, lead)
	state.leads.remove_at(i)
	state.pipe_stats = st
	return int(lead.get("seats", 0))

# ── §6 — land and expand (floor / hq) ───────────────────────────────────────
## Net revenue retention above 100% is the enterprise growth engine. Expansion is
## EARNED by product and care, never played as a move — and it draws down the
## same TAM through Bass's own `P = N − A`, so it is bounded, not free.
static func _expand_pass(state: GameState, rep: Dictionary, ctx: Dictionary,
		r: RandomNumberGenerator) -> void:
	if state.era_index() < 3 or state.logos.is_empty():
		return
	if float(state.traction) >= 0.9 * float(state.theta.get("tam", 4000.0)):
		return
	var quality := 0.6 + float(state.product) / 100.0 * 0.8
	var care_mult := 1.0 - 0.30 * (1.0 - exp(-float(state.budgets.get("care", 0)) / 1500.0))
	var p_expand := 0.05 * quality * (2.0 - care_mult)
	for lg in state.logos:
		var lgd: Dictionary = lg
		if r.randf() >= p_expand:
			continue
		var grow := maxi(int(ceil(float(lgd.get("seats", 0)) * r.randf_range(0.15, 0.30))), 2)
		lgd["seats"] = int(lgd.get("seats", 0)) + grow
		state.traction += grow
		_say(rep, ctx, "EXPANSION at %s: +%d seats — land-and-expand pays" % [
			String(lgd.get("name", "an account")), grow])

# ── §3 — spawning named leads out of the pool ───────────────────────────────
## A BIG DEAL TAKES TIME TO MATERIALIZE. When the era's tier table draws a deal
## the pool cannot fund, the week does NOT shrink it into a design partner — it
## HOLDS, and the demand banks. Interest keeps arriving; a few quiet weeks later
## the pool is deep enough and the whale walks in whole.
##
## That is the honest dynamic and it is what makes the era ladder mean anything:
## at hq, where a third of draws are 21-60 seats, the board goes quiet for a
## stretch and then lands something that changes the company. Shrinking every
## draw to fit would have made SEAT_TIERS and SIZE_REF decorative — every deal
## would spawn at the floor of the smallest band forever.
##
## Deal sizes are log-normal in the real world; the era tier table approximates
## it, and pipeline coverage precedes bookings.
static func _spawn_pass(state: GameState, rep: Dictionary, ctx: Dictionary,
		st: Dictionary, r: RandomNumberGenerator) -> void:
	var rn: RandomNumberGenerator = null
	var spawned := 0
	while spawned < SPAWNS_PER_WK and state.leads.size() < LEAD_CAP \
			and state.pipe_units >= float(MIN_SEATS):
		var band: Array = _tier_draw(state, r)
		var seats := maxi(r.randi_range(int(band[0]), int(band[1])), MIN_SEATS)
		# THE HOLD: the demand to fill this deal does not exist yet. Bank the pool
		# and stop the week here — a shrunken whale is a lie about the market, and
		# retrying the draw in-loop would spin until something small came up.
		if float(seats) > floor(state.pipe_units):
			break
		state.pipe_units = maxf(state.pipe_units - float(seats), 0.0)
		var heat := r.randi_range(HEAT_SPAWN_LO, HEAT_SPAWN_HI)
		if rn == null:
			rn = SimEngine.rng_for(state, SimEngine.SALT_PIPELINE_NAMES)
		var name_v := _placeholder_name(state, rn)
		state.leads.append({"name": name_v, "flavor": "", "seats": seats,
			"stage": "meeting", "age_weeks": 0, "heat": heat})
		if int(st.get("first_wk", 0)) <= 0:
			st["first_wk"] = state.week
		(rep["spawned_leads"] as Array).append(name_v)
		var spawn_meta: Array = state.get_meta("pipe_spawned", [])
		spawn_meta.append(name_v)
		state.set_meta("pipe_spawned", spawn_meta)
		spawned += 1
		_say(rep, ctx, "pipeline: +%s enters the calendar (%d seats, first meeting)" % [
			name_v, seats])

## The era's seat-tier table, drawn seeded. Returns the band [lo, hi].
static func _tier_draw(state: GameState, r: RandomNumberGenerator) -> Array:
	var w: Array = SEAT_TIERS.get(state.era, SEAT_TIERS["garage"])
	var total := 0.0
	for x in w:
		total += float(x)
	if total <= 0.0:
		return SEAT_BANDS[0]
	var roll := r.randf() * total
	for i in w.size():
		roll -= float(w[i])
		if roll <= 0.0:
			return SEAT_BANDS[i]
	return SEAT_BANDS[0]

## THE KEYLESS NAME, which is also the instant placeholder while an L1 naming
## call flies. Never a degraded path — the world's own Markov chain plus a sector
## suffix, redrawn up to three times against a collision.
static func _placeholder_name(state: GameState, rn: RandomNumberGenerator) -> String:
	var taken := _known_names(state)
	var name_v := ""
	for _try in 3:
		name_v = WorldGen.make_name(rn) + " " + String(ENT_SUFFIX[rn.randi_range(0, ENT_SUFFIX.size() - 1)])
		name_v = name_v.left(30)
		if not taken.has(name_v.to_lower()):
			return name_v
	return name_v

static func _known_names(state: GameState) -> Dictionary:
	var out := {}
	for ld in state.leads:
		out[String((ld as Dictionary).get("name", "")).to_lower()] = true
	for lg in state.logos:
		out[String((lg as Dictionary).get("name", "")).to_lower()] = true
	return out

# ── §10 L1 — the ONE batch naming call ──────────────────────────────────────
## THE KEYLESS PATH IS THE COMPLETE PATH. Every lead already has a name the
## moment it spawns (§3, salt 51), so this call only ever replaces WORDS — the
## board is fully playable before, during and after it, and a run with no key is
## not a degraded run.
##
## Same shape as the labor lane's applicant dressing (02 §8.1): a payload the
## week something spawned, and a rows lander that refuses a bad reply whole.
## Returns {} when there is nothing to name — the caller skips the call.
static func dressing_payload(state: GameState) -> Dictionary:
	if not active(state):
		return {}
	var spawned: Array = state.get_meta("pipe_spawned", [])
	if spawned.is_empty():
		return {}
	var fresh: Array = []
	for ld in state.leads:
		var lead: Dictionary = ld
		if not spawned.has(String(lead.get("name", ""))):
			continue
		fresh.append({"placeholder": String(lead.get("name", "")),
			"band": _band_word(int(lead.get("seats", MIN_SEATS))), "stage": "meeting"})
	if fresh.is_empty():
		return {}
	var taken: Array = []
	for k in _known_names(state):
		taken.append(String(k))
	for rv in state.rivals:
		taken.append(String((rv as Dictionary).get("name", "")))
	for iv in state.investors:
		taken.append(String((iv as Dictionary).get("name", "")))
	return {"company": {"name": state.company_name, "idea": state.company_idea,
			"what": state.biz_what, "who": state.biz_who},
		"era": state.era, "existing_names": taken, "new_leads": fresh}

## The size band, as a word the model can write scale into. It is INPUT for
## flavor only — the model never returns a number, and seats are the dice's.
static func _band_word(seats: int) -> String:
	if seats >= 61:
		return "whale"
	if seats >= 21:
		return "large"
	if seats >= 9:
		return "mid"
	return "small"

## Land a reply: rows of {name, one_liner} in spawn order. Returns how many leads
## were dressed; 0 means the reply was discarded and the placeholders stand —
## which is a complete board either way, so nothing is ever waiting on this.
static func dress_leads_rows(state: GameState, rows: Array) -> int:
	var names: Array = []
	var flavors: Array = []
	for r in rows:
		if not (r is Dictionary):
			return 0
		names.append(String((r as Dictionary).get("name", "")))
		flavors.append(String((r as Dictionary).get("one_liner", "")))
	return dress_leads(state, names, flavors)

## The typed core. The caller hands back one name and one one-liner per lead
## spawned this week, in the order they spawned; this overwrites `name` and
## `flavor` and NOTHING else. Seats, stage, heat and age are the dice's, always.
##
## A count mismatch is refused whole (the placeholders are already good names), a
## collision keeps the placeholder, and a save between spawn and reply simply
## persists the placeholders.
static func dress_leads(state: GameState, names: Array, flavors: Array) -> int:
	if not active(state):
		return 0
	var spawned: Array = state.get_meta("pipe_spawned", [])
	if spawned.is_empty() or names.size() != spawned.size():
		return 0
	var dressed := 0
	for i in spawned.size():
		var want := String(spawned[i])
		var fresh := String(names[i]).strip_edges().left(30)
		if fresh == "":
			continue
		for ld in state.leads:
			var lead: Dictionary = ld
			if String(lead.get("name", "")) != want:
				continue
			var taken := _known_names(state)
			taken.erase(want.to_lower())
			if not taken.has(fresh.to_lower()):
				lead["name"] = fresh
			if i < flavors.size():
				lead["flavor"] = String(flavors[i]).strip_edges().left(90)
			dressed += 1
			break
	return dressed

# ── §7 THE `push_lead` OP ───────────────────────────────────────────────────
## The founder writes a move that leans on a deal. Executive engagement measurably
## lifts win rates; it does not sign contracts by itself — so a push moves HEAT
## and nothing else. It never advances a stage, never adds traction, and a
## negative push is legal (a botched demo cools a deal).
##
## Returns the receipt line, or "" when no live lead matched — the executor turns
## an empty return into the sentinel's "no such lead" line.
static func push_lead(state: GameState, lead_name: String, heat_delta: int) -> String:
	if not active(state) or state.leads.is_empty():
		return ""
	var want := lead_name.strip_edges().to_lower()
	if want == "":
		return ""
	var delta := clampi(heat_delta, -PUSH_CLAMP, PUSH_CLAMP)
	for ld in state.leads:
		var lead: Dictionary = ld
		var have := String(lead.get("name", "")).to_lower()
		if have == "" or not (have.contains(want) or want.contains(have)):
			continue
		lead["heat"] = clampi(int(lead.get("heat", 0)) + delta, 0, 100)
		return "pushed %s: heat %s — the deal reads %s now" % [
			String(lead.get("name", "")), _signed(delta), heat_word(int(lead["heat"]))]
	return ""

# ── §11 DM context, and the bangs ───────────────────────────────────────────
## The DM's context lists the board BY NAME, so narration references real leads
## and the adjudicator has something true to push. The engine still owns every
## number in these lines — they are a read, never a lever.
static func directives(state: GameState) -> Array[String]:
	var out: Array[String] = []
	if not active(state):
		return out
	if not state.leads.is_empty():
		var order := leads_by_heat(state)
		var parts: Array[String] = []
		var decay := decay_for(state)
		for i in mini(order.size(), 5):
			var lead: Dictionary = state.leads[order[i]]
			var heat := int(lead.get("heat", 0))
			var word := heat_word(heat)
			var dies := weeks_to_cold(heat, decay)
			if dies <= 2:
				word += " — dies in %d wk" % dies
			parts.append("%s (%s, %d seats, %s)" % [String(lead.get("name", "")),
				String(lead.get("stage", "meeting")), int(lead.get("seats", 0)), word])
		var line := "Pipeline: " + " · ".join(PackedStringArray(parts))
		if order.size() > 5:
			line += " (+%d more)" % (order.size() - 5)
		out.append(line)
	if int(state.get_meta("pipe_signed_wk", 0)) == state.week and state.week > 0:
		var last: Dictionary = state.logos.back() if not state.logos.is_empty() else {}
		out.append("SIGNED THIS WEEK: %s (%d seats). Let the week feel it." % [
			String(last.get("name", "a new logo")), int(last.get("seats", 0))])
	var cold := _coldest(state)
	if cold != "":
		out.append("A lead is about to go cold: %s. If the move works a named lead, use push_lead {cat: the exact lead name, v: heat −40..40}." % cold)
	out.append("Enterprise law: customers arrive ONLY through signed contracts. Never grant traction for pipeline work — heat the lead instead.")
	return out

## Attention rows — the customers desk. The bang pulls the player to the board
## exactly when a deal is dying or one has just landed, and never otherwise.
static func attention(state: GameState) -> Array:
	var rows: Array = []
	if not active(state):
		return rows
	var cold := 0
	for ld in state.leads:
		if int((ld as Dictionary).get("heat", 0)) <= 16:
			cold += 1
	if cold == 1:
		rows.append({"desk": "customers", "key": "lead_cold", "severity": 2,
			"label": "a deal is going cold — push it"})
	elif cold > 1:
		rows.append({"desk": "customers", "key": "lead_cold", "severity": 2,
			"label": "%d deals going cold — push them" % cold})
	if int(state.get_meta("pipe_signed_wk", 0)) == state.week and state.week > 0:
		rows.append({"desk": "customers", "key": "signed", "severity": 1,
			"label": "a contract signed — seats booked"})
	return rows

# ── reads the desk and the DM share ─────────────────────────────────────────
## Lead indices ordered hottest first; the array index breaks every tie, so two
## reads of one state can never disagree about who is at the top of the board.
static func leads_by_heat(state: GameState) -> Array:
	var idx: Array = []
	for i in state.leads.size():
		idx.append(i)
	idx.sort_custom(func(a: int, b: int) -> bool:
		var ha := int((state.leads[a] as Dictionary).get("heat", 0))
		var hb := int((state.leads[b] as Dictionary).get("heat", 0))
		if ha != hb:
			return ha > hb
		return a < b)
	return idx

## The name of the coldest lead that is genuinely about to die, or "".
static func _coldest(state: GameState) -> String:
	var worst := 17
	var name_v := ""
	for ld in state.leads:
		var h := int((ld as Dictionary).get("heat", 0))
		if h <= 16 and h < worst:
			worst = h
			name_v = String((ld as Dictionary).get("name", ""))
	return name_v

## Live seats sitting on the board — the number the desk's summary line prints.
static func seats_in_motion(state: GameState) -> int:
	var n := 0
	for ld in state.leads:
		n += int((ld as Dictionary).get("seats", 0))
	return n

## THE DIGEST'S TWO ENTRIES (§11), so tier-2 event cards see the same board the
## adjudicator does and can write follow-ups about a real deal by name. Empty
## dictionary off Enterprise — the digest simply gains nothing.
static func digest_rows(state: GameState) -> Dictionary:
	if not active(state):
		return {}
	var board: Array[String] = []
	for i in leads_by_heat(state):
		var lead: Dictionary = state.leads[i]
		board.append("%s — %s, %d seats, %s" % [String(lead.get("name", "")),
			String(lead.get("stage", "meeting")), int(lead.get("seats", 0)),
			heat_word(int(lead.get("heat", 0)))])
	var seated := 0
	for lg in state.logos:
		seated += int((lg as Dictionary).get("seats", 0))
	return {"pipeline": board,
		"signed_logos": "%d logos, %d seats" % [state.logos.size(), seated]}

## One line for the signals block: the board at a glance.
static func signal_line(state: GameState) -> String:
	if not active(state):
		return ""
	var hottest := "nobody yet"
	var order := leads_by_heat(state)
	if not order.is_empty():
		var lead: Dictionary = state.leads[order[0]]
		hottest = "%s (%s, %s)" % [String(lead.get("name", "")),
			String(lead.get("stage", "meeting")), heat_word(int(lead.get("heat", 0)))]
	return "%d live (%d seats) · hottest %s · pool %.1f seats" % [
		state.leads.size(), seats_in_motion(state), hottest, state.pipe_units]

# ── the running totals the desk divides ─────────────────────────────────────
## `pipe_stats` with every key present. An old save carries `{}`; the desk must
## never divide by a missing key.
static func _stats(state: GameState) -> Dictionary:
	var st: Dictionary = state.pipe_stats
	for k in ["signed", "lost", "cycle_sum", "seats_signed", "first_wk"]:
		if not st.has(k):
			st[k] = 0
	if not st.has("spend"):
		st["spend"] = 0.0
	return st

# ── receipts ────────────────────────────────────────────────────────────────
## Six pipeline lines a week, then the truth about the rest. The journal is a
## page, not a log file.
static func _say(rep: Dictionary, ctx: Dictionary, line: String) -> void:
	if int(ctx["said"]) < MAX_LINES:
		(rep["lines"] as Array).append(line)
		ctx["said"] = int(ctx["said"]) + 1
	else:
		ctx["more"] = int(ctx["more"]) + 1

## A signed delta the way a founder writes one: +20, −40.
static func _signed(v: int) -> String:
	return ("+%d" % v) if v >= 0 else ("−%d" % absi(v))

## Annual contract value, said the way a salesperson says it.
static func _acv(v: float) -> String:
	if v >= 1_000_000.0:
		return "$%.1fM" % (v / 1_000_000.0)
	if v >= 1000.0:
		return "$%dk" % int(round(v / 1000.0))
	return "$%d" % int(round(v))

## Thousands separators, engine-side, so a receipt reads like an invoice.
static func _grp(n: int) -> String:
	var s := str(absi(n))
	var out := ""
	var c := 0
	for i in range(s.length() - 1, -1, -1):
		out = s.substr(i, 1) + out
		c += 1
		if c % 3 == 0 and i > 0:
			out = "," + out
	return ("−" if n < 0 else "") + out
