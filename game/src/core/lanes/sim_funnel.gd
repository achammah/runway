class_name SimFunnel
extends RefCounted
## LANE 04 — THE FUNNEL (four acquisition channels). Spec: docs/design/04-funnel-channels.md
##
## The old path multiplied organic adoption by one blended reach lever and hid
## the funnel. This lane makes the funnel the actual computation:
##
##   REACH (bought: ads + content + outbound) ──┐
##                                              ├─→ LEADS (× conv) ─→ SIGNED
##   WALK-INS (organic + word of mouth, ────────┘      = min(demand, capacity)
##     word of mouth amplified by referrals)
##
## Four real growth dynamics, each named by its real name:
##   ads       auction-bought reach — instant, concave in spend, CAC inflates
##   content   a STOCK (content_equity) that compounds while funded and rots when starved
##   referrals promoters amplify word of mouth; below an NPS bar there are none
##   outbound  quota math — buys reach AND closing capacity, priced by audience
##
## Every number is engine arithmetic on state. NO new RNG salts (attribution is
## exact division, never a die) and NO LLM calls (spec §7): the DM narrates the
## mix for free through directives().
##
## HOW IT REACHES THE ENGINE. The spine owns the weekly tick; this lane owns
## three hooks and one seam:
##   tick_pre    settles the content stock, then computes the WHOLE week's
##               funnel and parks it on the state as the `funnel` read-out
##   reach_mult  hands the spine the one multiplier that makes its own adoption
##               line produce exactly the funnel's number (see _plan)
##   tick_post   reconciles the attribution against what actually landed and
##               writes the receipts that teach
## The invariant a pin asserts every week:
##   organic + word of mouth + Σ channels == adds

# ── THE CHANNEL TABLE (spec §1.5) ────────────────────────────────────────────
## Per audience, exact. `ads_a`/`con_a` are the reach/week a channel can buy at
## its ceiling; `ads_k` scales the world's own knee (theta.cac_sat) into an ads
## saturation point; `ref_a` is the loop's amplitude; `ob_aud` is who answers a
## cold touch; `conv` is the base lead conversion for all bought reach.
const CHANNELS := {
	"Consumer": {"ads_a": 2400.0, "ads_k": 0.30, "con_a": 1600.0, "con_sat": 1600.0,
		"ref_a": 2.6, "ref_sat": 900.0, "ob_aud": 0.15, "conv": 0.030},
	"SMB": {"ads_a": 320.0, "ads_k": 0.40, "con_a": 520.0, "con_sat": 1600.0,
		"ref_a": 1.8, "ref_sat": 1200.0, "ob_aud": 1.0, "conv": 0.080},
	"Enterprise": {"ads_a": 20.0, "ads_k": 0.65, "con_a": 30.0, "con_sat": 2200.0,
		"ref_a": 1.2, "ref_sat": 1500.0, "ob_aud": 2.5, "conv": 0.060},
}

## The library ramps 12.5%/wk toward the level its funding supports (~80% of
## target in 12 weeks) and decays 7%/wk unfunded (half-life ≈ 9.6 weeks).
const CON_RAMP := 0.125
const CON_DECAY := 0.93
## Lists and sequences scale linearly with budget — there is always another list.
const OB_REACH_PER_K := 5.0
## THE ERA LADDER (spec §6.3). A garage has no brand and no pixel history, so
## paid reach lands at a third; a name on the door opens doors.
const ERA_REACH_EFF := {"garage": 0.35, "coworking": 0.7, "office": 1.0,
	"floor": 1.1, "hq": 1.25}
## Full attribution is an office-era capability: a garage cannot buy a data stack.
const ERA_AN_CAP := {"garage": 1, "coworking": 2, "office": 3, "floor": 3, "hq": 3}
## Channel teams amplify what money buys, capped so a department is not a cheat.
const TEAM_PER_HEAD := 0.12
const TEAM_HEADS_MAX := 5

## The four acquisition lanes, in the ONE order every reader walks them.
const MIX: Array[String] = ["ads", "content", "referrals", "outbound"]
## A channel this well funded that signs nobody is burning money, not learning.
const BURN_SPEND := 500.0
const BURN_SIGNED := 0.05
## Below this the product has detractors, not promoters — a referral program
## buys silence (spec §1.3).
const HAPPY_FLOOR := 0.1

# ═════════════════════════ THE READ HELPERS ══════════════════════════════════
## Everything the ledger, the customers desk and the pins ask this lane. Pure
## functions of state: no side effects, safe to call from a redraw.

## The channel constants for this run's audience.
static func channel(state: GameState) -> Dictionary:
	return CHANNELS.get(state.biz_who, CHANNELS["SMB"])

## One channel's weekly dollars. The legacy `set_marketing` op's budget folds
## into ADS exactly as it folded into the old blended lever (spec §5).
static func spend_of(state: GameState, key: String) -> float:
	var v := float(int(state.budgets.get(key, 0)))
	if key == "ads":
		v += float(state.marketing_budget)
	return v

## What acquisition costs this week, all four lanes — the P&L's `marketing` sum.
static func spend_total(state: GameState) -> float:
	var t := 0.0
	for k in MIX:
		t += spend_of(state, k)
	return t

static func era_eff(state: GameState) -> float:
	return float(ERA_REACH_EFF.get(state.era, 1.0))

## EFFECTIVE ANALYTICS: what the founder can actually see, level clamped by era.
static func analytics(state: GameState) -> int:
	return mini(state.analytics_level, int(ERA_AN_CAP.get(state.era, 3)))

static func mk_heads(state: GameState) -> int:
	var n := 0
	for em in state.employees:
		if String((em as Dictionary).get("role", "")).contains("marketing"):
			n += 1
	return n

## +12% per marketing head, five heads deep. Live at every era (0 heads = ×1);
## salaries and era spend caps make it a floor/hq play in practice.
static func team_mult(state: GameState) -> float:
	return 1.0 + TEAM_PER_HEAD * float(mini(mk_heads(state), TEAM_HEADS_MAX))

## The very term care already uses on churn, reused here: money answering the
## phone is half of whether anyone would vouch for you.
static func care_soft(state: GameState) -> float:
	return 1.0 - exp(-float(state.budgets.get("care", 0)) / 1500.0)

## THE NPS GATE. Below v0.25 there are no promoters at all, and a paid referral
## program amplifies exactly that silence.
static func happy(state: GameState) -> float:
	return pow(maxf((float(state.product) - 25.0) / 75.0, 0.0), 1.2) \
			* (0.5 + 0.5 * care_soft(state))

## What the referral program multiplies word of mouth BY (0 = it changes nothing).
static func ref_gain(state: GameState) -> float:
	var ch := channel(state)
	var b := spend_of(state, "referrals")
	return float(ch.ref_a) * (1.0 - exp(-b / float(ch.ref_sat))) * happy(state) \
			* team_mult(state)

## The auction: the first dollars buy the cheap, well-targeted audience, and
## pushing spend climbs the bid landscape. Concave, so CAC rises on its own.
static func ads_sat(state: GameState) -> float:
	return maxf(float(state.theta.get("cac_sat", 8000.0)) * float(channel(state).ads_k), 1.0)

static func reach_ads(state: GameState) -> float:
	var ch := channel(state)
	return float(ch.ads_a) * (1.0 - exp(-spend_of(state, "ads") / ads_sat(state))) \
			* era_eff(state) * team_mult(state)

## The library pays at ~zero marginal cost from the stock it has TODAY. Pass an
## explicit equity to read a level the state has not reached yet.
static func reach_content(state: GameState, equity: float = -1.0) -> float:
	var c := state.content_equity if equity < 0.0 else equity
	return float(channel(state).con_a) * c * era_eff(state) * team_mult(state)

## The level a given weekly spend funds — the ceiling the ramp climbs toward.
static func content_target(state: GameState, budget: float = -1.0) -> float:
	var b := spend_of(state, "content") if budget < 0.0 else budget
	return 1.0 - exp(-b / float(channel(state).con_sat))

## Cold touch is era-neutral: a founder with a list works the same in a garage.
static func reach_outbound(state: GameState) -> float:
	return OB_REACH_PER_K * spend_of(state, "outbound") / 1000.0 * float(channel(state).ob_aud)

## Outbound money is also buying an SDR-hour equivalent — closing, not just reach.
static func ob_closers(state: GameState) -> float:
	return spend_of(state, "outbound") / 600.0 * float(channel(state).ob_aud)

# ── THE CAPACITY SEAM ────────────────────────────────────────────────────────
## HOW MUCH CLOSING DID ACQUISITION MONEY BUY? Not every dollar buys any. Ads
## pull inbound onto the founder's calendar — that is the old blended lever's
## own `/400` slot, inherited here exactly, so a migrated save keeps its ceiling.
## Outbound money IS an SDR hour and buys closing directly, priced by who
## answers a cold touch. Content and referral dollars buy NONE: a library and a
## promoter make demand, not a person to sign it. `mk_budget` is the spine's
## blended total, which this lane no longer needs.
static func cap_reach(state: GameState, _mk_budget: float) -> float:
	return spend_of(state, "ads") / 400.0 + ob_closers(state)

## THE WEEKLY CEILING, mirrored from the spine's own clamp so the funnel and the
## tick can never disagree about what closing capacity is: founder sell-stat,
## the sales roster (the labor lane prices its own people), the sales budget and
## `cap_reach` above — all scaled by audience.
static func gtm_cap(state: GameState) -> float:
	var sales_heads := 0
	for e in state.employees:
		if String((e as Dictionary).get("role", "")).contains("sales"):
			sales_heads += 1
	var cap_scale := 1.0
	match state.biz_who:
		"SMB": cap_scale = 3.0
		"Consumer": cap_scale = 40.0
	return (1.5 + 0.8 * float(state.competences.get("sell", 3))
			+ _sales_capacity(state, 3.0 * float(sales_heads))
			+ cap_reach(state, 0.0)
			+ _roadmap_cap_bonus(state)
			+ float(state.budgets.get("sales", 0)) / 600.0) * cap_scale

## THE SPINE, LATE-BOUND. `sim_engine.gd` calls into this file, so naming
## SimEngine here at compile time closes a cycle and the class arrives as a bare
## script with no members on it (the tick then cannot find `tick_pre` at all).
## Loading it on first use breaks the cycle and keeps ONE source of truth for
## the status catalog and the priced shelf — this lane never forks either.
static var _spine: GDScript = null

static func _sp() -> GDScript:
	if _spine == null:
		_spine = load("res://src/core/sim_engine.gd") as GDScript
	return _spine

## THE SIBLING LANES, late-bound for the same reason (each names SimEngine too,
## and a direct reference would close the cycle through the spine). This lane
## reads their numbers; it never re-derives them, so a roster, a shipped bet or
## a named-account run is priced exactly once, by whoever owns it.
static var _lanes := {}

static func _lane(file: String) -> GDScript:
	if not _lanes.has(file):
		_lanes[file] = load("res://src/core/lanes/%s.gd" % file) as GDScript
	return _lanes[file]

static func _sales_capacity(state: GameState, dflt: float) -> float:
	return float(_lane("sim_labor").sales_capacity(state, dflt))

static func _roadmap_cap_bonus(state: GameState) -> float:
	return float(_lane("sim_roadmap").gtm_cap_bonus(state))

## An Enterprise run signs through the pipeline's own stages, so the weekly
## ceiling does not apply to it at all — the funnel must read that the same way
## the tick does, or the desk would print a bottleneck nobody is feeling.
static func _skips_cap(state: GameState) -> bool:
	return bool(_lane("sim_pipeline").skips_gtm_cap(state))

## The status catalog's adoption multipliers, applied exactly as tick §8 applies
## them (a press surge lifts the funnel too — it is the same market).
static func _status_adopt(state: GameState) -> float:
	var cat: Dictionary = _sp().get_script_constant_map().get("STATUS", {})
	var m := 1.0
	for s in state.statuses:
		var eff: Dictionary = cat.get(String((s as Dictionary).get("name", "")), {})
		m *= float(eff.get("adopt_mult", 1.0))
	return m

## LAST WEEK'S FUNNEL, whole — the flat read-out the customers desk draws from.
## Empty before the first tick and after a load (a meta is not saved), which is
## a real state the desk must print rather than a case it may assume away.
static func funnel(state: GameState) -> Dictionary:
	var f: Variant = state.get_meta("funnel", {})
	return f if f is Dictionary else {}

## The week before it, for the receipts that need a direction (CAC rising).
static func funnel_prev(state: GameState) -> Dictionary:
	var f: Variant = state.get_meta("funnel_prev", {})
	return f if f is Dictionary else {}

static func num(f: Dictionary, key: String, dflt: float = 0.0) -> float:
	return float(f.get(key, dflt))

## WHAT THIS MONEY IS DOING RIGHT NOW, in the engine's own formula — the string
## the ledger prints beside each channel row (house law: mechanics visible at
## the point of decision).
static func lever_effect(state: GameState, cat: String) -> String:
	match cat:
		"ads":
			if spend_of(state, "ads") <= 0.0:
				return "no reach bought"
			var era_note := ""
			if state.era == "garage" or state.era == "coworking":
				era_note = " (era ×%.2f)" % era_eff(state)
			return "reach ≈%d/wk%s" % [int(round(reach_ads(state))), era_note]
		"content":
			var c := state.content_equity
			if spend_of(state, "content") <= 0.0:
				if c >= 0.005:
					return "fading −7%/wk"
				return "nothing written yet"
			return "equity %d%% → ≈%d/wk" % [int(round(c * 100.0)), int(round(reach_content(state)))]
		"referrals":
			if spend_of(state, "referrals") <= 0.0:
				return "no program"
			if happy(state) < HAPPY_FLOOR:
				return "nobody would vouch yet (v0.%d)" % state.product
			return "word of mouth ×%.2f" % (1.0 + ref_gain(state))
		"outbound":
			if spend_of(state, "outbound") <= 0.0:
				return "no lists worked"
			return "+%d reach · +%.1f closing" % [int(round(reach_outbound(state))), ob_closers(state)]
	return ""

# ═════════════════════════ THE WEEK'S FUNNEL ═════════════════════════════════
## THE WHOLE COMPUTATION, once, from settled state. Called from tick_pre — after
## rivals, macro, quality and the content stock have all moved, and before the
## spine reads adoption.
##
## Returns a FLAT map of numbers (no nesting): the same shape both engines write
## and both desks read, so a twin can never drift on a key.
static func _plan(state: GameState) -> Dictionary:
	var th: Dictionary = state.theta
	var ch := channel(state)
	var A := float(state.traction)
	var N := maxf(float(th.get("tam", 100_000.0)), 1.0)
	var P := maxf(N - A, 0.0)
	var launched := state.has_flag("launched")
	var launch_f := 1.0 if launched else 0.0
	var quality_gate := 0.2 + float(state.product) / 100.0 * 0.8
	var hype_mult := 0.6 + float(state.hype) / 100.0 * 0.9
	# the same three world terms the spine's own adoption line reads, read the
	# same way — statuses, the settled rival board, the priced shelf
	var status_adopt := _status_adopt(state)
	var pressure := 0.0
	for rv in state.rivals:
		pressure += float((rv as Dictionary).get("strength", 0.0))
	pressure = minf(pressure / maxf(float(state.rivals.size()), 1.0) / 100.0 * 0.5, 0.45)
	var pd := pow(maxf(state.price_mult, 0.1), -1.5)
	var om: float = _sp().offers_demand_mult(state)
	if om >= 0.0:
		pd = om
	pd = clampf(pd, 0.1, 3.0)

	# ── REACH: three bought sources (§1.1–1.4) ──────────────────────────────
	var b_ads := spend_of(state, "ads")
	var b_con := spend_of(state, "content")
	var b_ref := spend_of(state, "referrals")
	var b_ob := spend_of(state, "outbound")
	var r_ads := reach_ads(state)
	var r_con := reach_content(state)
	var r_ob := reach_outbound(state)

	# ── LEADS: ONE conversion gate for all bought reach, out of the same terms
	# the organic path uses, plus prospect-pool exhaustion and the launch gate
	var avail := P / N
	var conv := float(ch.conv) * quality_gate * status_adopt * state.market_trend \
			* (1.0 - pressure) * avail * launch_f
	var l_ads := r_ads * conv
	var l_con := r_con * conv
	var l_ob := r_ob * conv
	var leads_paid := l_ads + l_con + l_ob

	# ── WALK-INS: the untouched organic pipeline. `organic_base` is the spine's
	# own p_eff with the reach lever taken out; referrals amplify word of mouth.
	var organic_base := float(th.get("adopt_p", 0.00025)) * hype_mult * status_adopt \
			* state.market_trend * (1.0 - pressure) * quality_gate * launch_f * P
	var wom_base := float(th.get("adopt_ic", 0.06)) * A * P / N * status_adopt \
			* (1.0 - pressure) * quality_gate * (1.0 if launched else 0.5)
	var gain := ref_gain(state)
	# THE ONE THING THE SEAM CANNOT CARRY: the lift rides the spine's organic
	# term, and that term is zero before launch (nothing sells itself yet). A
	# pre-launch referral program buys nothing, and the read-out says nothing.
	var deliverable := organic_base > 0.0
	var lift := gain if deliverable else 0.0

	# ── SIGNED: price, then the capacity ceiling ────────────────────────────
	var organic := organic_base * pd
	var wom_all := wom_base * (1.0 + lift) * pd
	var demand := organic + wom_all + leads_paid * pd
	# ONE CEILING. `gtm_cap` mirrors the spine's own clamp term for term (its
	# reach half IS this lane's `cap_reach`), so the funnel reads the same
	# ceiling the tick will apply and the clamp lands exactly once — and where
	# the pipeline signs its own way, neither of them applies it.
	var cap := gtm_cap(state)
	var adds := demand if _skips_cap(state) else minf(demand, cap)
	var close_rate := adds / maxf(demand, 0.001)

	# ── THE SEAM'S ANSWER. The spine computes (organic_base × mult + wom_base)
	# × price and then applies that ceiling itself; solving the first half for
	# `mult` is what makes its line produce this funnel's DEMAND exactly,
	# referral lift and all — and leaves the clamping to the clamp. An unfunded
	# week therefore hands back exactly ×1.00 and the tick is arithmetically the
	# week it always was.
	var mk_mult := 1.0
	if deliverable:
		mk_mult = maxf((demand / maxf(pd, 0.0001) - wom_base) / organic_base, 0.0)

	# ── ATTRIBUTION, proportional and exact: every arrival is assigned, and the
	# parts sum to `adds` with no residue (pin #1 asserts it every week).
	var att_ads := l_ads * pd * close_rate
	var att_con := l_con * pd * close_rate
	var att_ob := l_ob * pd * close_rate
	var att_ref := wom_all * close_rate * (lift / (1.0 + lift))
	var att_org := organic * close_rate
	var att_wom := wom_all * close_rate / (1.0 + lift)

	var f := {
		"wk": float(state.week),
		"spend_ads": b_ads, "spend_content": b_con,
		"spend_referrals": b_ref, "spend_outbound": b_ob,
		"spend_total": b_ads + b_con + b_ref + b_ob,
		"reach_ads": r_ads, "reach_content": r_con,
		"reach_referrals": 0.0, "reach_outbound": r_ob,
		"leads_ads": l_ads, "leads_content": l_con,
		"leads_referrals": 0.0, "leads_outbound": l_ob,
		"signed_ads": att_ads, "signed_content": att_con,
		"signed_referrals": att_ref, "signed_outbound": att_ob,
		"reach_total": r_ads + r_con + r_ob,
		"leads_total": leads_paid,
		"conv": conv, "close_rate": close_rate,
		"equity": state.content_equity, "equity_before": state.content_equity,
		"ref_gain": gain, "happy": happy(state),
		"organic": att_org, "wom": att_wom,
		"demand": demand, "adds": adds,
		"gtm_cap": cap,
		"ob_closers": ob_closers(state),
		"era_eff": era_eff(state), "team_mult": team_mult(state),
		"price_demand": pd, "launched": launch_f,
		"blended_cac": 0.0,
		"_b_sales": float(state.budgets.get("sales", 0)),
		"_mk": mk_mult,
	}
	_recac(f)
	return f

## CAC is what a customer COST: this channel's dollars over this channel's
## arrivals. Recomputed wherever attribution moves, so the two can never
## disagree. 0 means "no honest number" — the desk prints the reason instead.
static func _recac(f: Dictionary) -> void:
	for k in MIX:
		var sp := num(f, "spend_" + k)
		var got := num(f, "signed_" + k)
		f["cac_" + k] = (sp / got) if (got >= BURN_SIGNED and sp > 0.0) else 0.0
	# the blended read the ledger already prints: acquisition + closing spend
	# over the week's arrivals (the spine's own rep.cac, same meaning)
	var total := num(f, "spend_total") + num(f, "_b_sales")
	var got_all := maxf(num(f, "adds"), 0.0)
	f["blended_cac"] = round(total / got_all) if (got_all >= 0.5 and total > 0.0) else 0.0

# ═════════════════════════ THE SPINE'S HOOKS ═════════════════════════════════

## Tick §8, before adoption: the content stock compounds or rots, then the whole
## week's funnel is computed and parked for the seam and the desk to read.
static func tick_pre(state: GameState, _rep: Dictionary) -> void:
	state.set_meta("funnel_prev", funnel(state))
	# THE ONE STOCK IN THIS SUBSYSTEM. Posts and rankings are capital, not
	# spend: funded, the library climbs toward the level its budget supports;
	# starved, it fades. Equity builds even pre-launch (writing before shipping
	# is a real strategy) — only the CONVERSION is launch-gated.
	var c0 := state.content_equity
	var b_con := spend_of(state, "content")
	if b_con > 0.0:
		state.content_equity = clampf(c0 + (content_target(state, b_con) - c0) * CON_RAMP, 0.0, 1.0)
	else:
		state.content_equity *= CON_DECAY
	var f := _plan(state)
	f["equity_before"] = c0
	# the seam's answer travels with the PLAN, never inside the public read-out:
	# it is an implementation detail of one engine's adoption line
	var mult := num(f, "_mk", 1.0)
	f.erase("_mk")
	state.set_meta("funnel", f)
	state.set_meta("_funnel_plan", {"mk_mult": mult, "adds": num(f, "adds")})

## THE REACH SEAM. `dflt` is the blended lever the engine would use on its own;
## the multiplier below makes the spine's adoption line land on this week's
## funnel number instead — reach, leads, referral lift and the capacity ceiling
## all folded into the one factor its formula exposes.
static func reach_mult(state: GameState, _spend: float, dflt: float) -> float:
	var plan: Variant = state.get_meta("_funnel_plan", {})
	if not (plan is Dictionary) or (plan as Dictionary).is_empty():
		return dflt
	return float((plan as Dictionary).get("mk_mult", dflt))

## The money section. Acquisition spend is ONE P&L lane, `marketing`, and the
## spine already books it as the four channels summed — the split lives where
## the funnel lives (the customers desk and the mix receipt), so the compact
## ledger line stays readable and every existing reader of `pnl.marketing`
## keeps working (spec §4, P&L decision). Nothing to write here.
static func tick_money(_state: GameState, _rep: Dictionary, _m: Dictionary) -> void:
	pass

## After the record is written: the attribution follows what ACTUALLY landed,
## then the receipts — each one naming the real concept and its cause.
static func tick_post(state: GameState, rep: Dictionary) -> void:
	var plan: Variant = state.get_meta("_funnel_plan", {})
	if not (plan is Dictionary) or (plan as Dictionary).is_empty():
		return
	state.remove_meta("_funnel_plan")
	var f := funnel(state)
	if f.is_empty():
		return
	var planned := num(f, "adds")
	var actual := float(int(rep.get("adds", 0)))
	# a stock-out or any later clamp can land a smaller week than the funnel
	# planned; attribution is a statement about arrivals, so it follows them
	if int(round(planned)) != int(actual) and planned > 0.0:
		var k := actual / planned
		for key in ["signed_ads", "signed_content", "signed_referrals",
				"signed_outbound", "organic", "wom"]:
			f[key] = num(f, key) * k
		f["adds"] = actual
		f["close_rate"] = actual / maxf(num(f, "demand"), 0.001)
		_recac(f)
		state.set_meta("funnel", f)
	_receipts(state, rep, f)

## THREE NUMBERS THAT ADD UP. Rounding each source on its own printed
## "+31 customers (organic 9 · word of mouth 11 · channels 12)" — a receipt a
## player checks with a finger must balance, so the parts are apportioned by
## largest remainder, ties to the earlier source.
static func _split_int(total: int, parts: Array) -> Array:
	var out: Array = []
	var sum := 0.0
	for p in parts:
		sum += maxf(float(p), 0.0)
	for _p in parts:
		out.append(0)
	if sum <= 0.0 or total <= 0:
		return out
	var rema: Array = []
	var put := 0
	for i in parts.size():
		var exact := maxf(float(parts[i]), 0.0) / sum * float(total)
		var whole := int(floor(exact))
		out[i] = whole
		put += whole
		rema.append({"i": i, "r": exact - float(whole)})
	rema.sort_custom(func(x: Dictionary, y: Dictionary) -> bool:
		if not is_equal_approx(float(x["r"]), float(y["r"])):
			return float(x["r"]) > float(y["r"])
		return int(x["i"]) < int(y["i"]))
	var k := 0
	while put < total and k < rema.size():
		out[int((rema[k] as Dictionary)["i"])] += 1
		put += 1
		k += 1
	return out

## THE RECEIPTS (spec §4). Every line names the mechanism and why it fired.
static func _receipts(state: GameState, rep: Dictionary, f: Dictionary) -> void:
	var lines: Array = rep.get("lines", [])
	var prev := funnel_prev(state)
	var b_ads := num(f, "spend_ads")
	var b_con := num(f, "spend_content")
	var b_ref := num(f, "spend_referrals")
	var b_ob := num(f, "spend_outbound")
	var adds := num(f, "adds")

	# 1 ── the week's arrivals gain their third source. The spine printed
	# organic and word of mouth from its own blended line, which now carries
	# the channels inside it; this restates it with the real split.
	if adds >= 1.0:
		var chan_sum := num(f, "signed_ads") + num(f, "signed_content") \
				+ num(f, "signed_referrals") + num(f, "signed_outbound")
		var total := int(rep.get("adds", int(round(adds))))
		var split := _split_int(total, [num(f, "organic"), num(f, "wom"), chan_sum])
		for i in lines.size():
			var s := String(lines[i])
			if s.begins_with("+") and s.contains(" customers (organic "):
				lines[i] = "+%d customers (organic %d · word of mouth %d · channels %d)" % [
					total, split[0], split[1], split[2]]
				break

	# 2 ── the mix: spend → customers, per channel, whenever there is a choice
	var funded := 0
	for k in MIX:
		if num(f, "spend_" + k) > 0.0:
			funded += 1
	if funded >= 2:
		lines.append("the mix: ads $%d→%.1f · content $%d→%.1f (equity %d%%) · referrals $%d→%.1f · outbound $%d→%.1f" % [
			int(b_ads), num(f, "signed_ads"), int(b_con), num(f, "signed_content"),
			int(round(num(f, "equity") * 100.0)), int(b_ref), num(f, "signed_referrals"),
			int(b_ob), num(f, "signed_outbound")])

	# 3 ── saturation, taught at the moment it bites: more money, worse price
	var cac_ads := num(f, "cac_ads")
	var cac_was := num(prev, "cac_ads")
	if cac_ads > 0.0 and cac_was > 0.0 and b_ads >= 1.2 * num(prev, "spend_ads") \
			and cac_ads >= 1.25 * cac_was:
		lines.append("ads CAC rose to $%d — the cheap audience is spent (saturation)" % int(round(cac_ads)))

	# 4 ── the stock crossing a threshold upward is the only visible sign that
	# a library is compounding
	var c_now := num(f, "equity")
	var c_was := num(f, "equity_before")
	for gate in [0.25, 0.5, 0.75]:
		if c_was < gate and c_now >= gate:
			lines.append("the library compounds: content reaches %d/wk now, at $0 marginal"
					% int(round(num(f, "reach_content"))))
			break

	# 5 ── and rot is the other half of the same lesson
	if b_con <= 0.0 and c_now >= 0.05:
		lines.append("the library goes quiet — content equity fades to %d%%" % int(round(c_now * 100.0)))

	# 6 ── the NPS gate, named instead of silently eating the spend
	if b_ref >= 500.0 and num(f, "happy") < HAPPY_FLOOR:
		lines.append("a referral program for a product nobody would vouch for (v0.%d) — promoters first, program second" % state.product)

	# 7 ── demand versus capacity, the funnel's last lesson
	if num(f, "close_rate") < 0.9 and num(f, "demand") >= 1.0:
		lines.append("demand outran closing: %d wanted in, you signed %d — capacity, not demand, is the bottleneck (sales or outbound)" % [
			int(round(num(f, "demand"))), int(round(adds))])

	# 8 ── money quietly buying nothing, by name
	for k in MIX:
		if num(f, "spend_" + k) >= BURN_SPEND and num(f, "signed_" + k) < BURN_SIGNED:
			lines.append("$%d into %s found nobody — saturated or mispriced" % [
				int(num(f, "spend_" + k)), k])

	# 9 ── the classic pre-launch mistake, with its reason
	if num(f, "launched") <= 0.0 and b_ads + b_ob > 0.0:
		lines.append("reach with nothing to sign — ads and cold calls convert only after launch")

	# 10 ── stage-appropriate acquisition, taught once, the first time paid
	# money goes in early
	if (state.era == "garage" or state.era == "coworking") and b_ads + b_con >= 500.0 \
			and not state.has_flag("seen_paid_era_note"):
		state.set_flag("seen_paid_era_note")
		lines.append("the garage discount: paid reach ×%.2f — no brand, no pixel history. Outbound and word of mouth are the garage channels." % num(f, "era_eff"))

## DM context. The narrator gets the mix in one line and can never contradict
## the ledger, because it IS the ledger's numbers (spec §4, `funnel_mix`).
static func directives(state: GameState) -> Array[String]:
	var out: Array[String] = []
	var total := spend_total(state)
	var f := funnel(state)
	if total <= 0.0 and f.is_empty():
		return out
	var cac := int(num(f, "blended_cac"))
	out.append("- The funnel mix: ads $%d · content $%d (equity %d%%) · referrals $%d · outbound $%d · blended CAC %s." % [
		int(spend_of(state, "ads")), int(spend_of(state, "content")),
		int(round(state.content_equity * 100.0)), int(spend_of(state, "referrals")),
		int(spend_of(state, "outbound")), ("$%d" % cac) if cac > 0 else "not yet knowable"])
	return out

## Attention rows. Two conditions are worth stopping a founder for: money that
## bought nobody (the fix is on the ledger, where the lever is) and a library
## left to rot (the read is on the customers desk, where the stock shows).
static func attention(state: GameState) -> Array:
	var rows: Array = []
	var f := funnel(state)
	if not f.is_empty():
		for k in MIX:
			if num(f, "spend_" + k) >= BURN_SPEND and num(f, "signed_" + k) < BURN_SIGNED:
				rows.append({"desk": "the ledger", "key": "burning_" + k, "severity": 2,
					"label": "$%d/wk into %s finds nobody" % [int(num(f, "spend_" + k)), k]})
	if spend_of(state, "content") <= 0.0 and state.content_equity >= 0.3:
		rows.append({"desk": "customers", "key": "content_rot", "severity": 1,
			"label": "the library fades · content unfunded"})
	return rows

# ── THE DM's ONE MARKETING CATEGORY ──────────────────────────────────────────
## The narrator says "put $2k into marketing" and the ENGINE decides which
## channels that means, splitting by the mix the player already curated: a
## narrator must never silently overwrite a curated mix, and the op schema stays
## byte-identical in both prompt files (spec §5).
static func set_marketing(state: GameState, amount: int) -> void:
	var mix_sum := 0
	for k in MIX:
		mix_sum += int(state.budgets.get(k, 0))
	if mix_sum <= 0:
		# cold start: the instant channel, because nothing else pays in week one
		state.budgets["ads"] = amount
		for k2 in MIX:
			if k2 != "ads":
				state.budgets[k2] = int(state.budgets.get(k2, 0))
	else:
		var put := 0
		for k3 in MIX:   # deterministic order; the remainder lands on ads
			var share := int(floor(float(amount) * float(int(state.budgets.get(k3, 0))) / float(mix_sum)))
			state.budgets[k3] = share
			put += share
		state.budgets["ads"] = int(state.budgets.get("ads", 0)) + amount - put
	state.marketing_budget = 0
