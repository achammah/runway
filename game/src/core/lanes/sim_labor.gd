class_name SimLabor
extends RefCounted
## LANE 02 — THE LABOR MARKET (roles, applicants, raises, severance). Spec: docs/design/02-labor-market.md
##
## THE ONE IDEA: a head is a PRICE, not a slot. What the street pays for a role
## (the MARKET RATE) is a world constant the player learns across runs; the
## advert against that rate decides who applies, the ask against it decides what
## a hire costs, and the salary against it decides who stays. Every departure
## prints the ratio that caused it.
##
## The spine calls, in tick order (docs/design/00-spine.md §1, HOOKS.md):
##   tick_pre   tick §3b — arrivals (20/21) → decay (22) → review + ladder (23)
##   tick_money the money section — severance and the recruiter retainer
##   tick_post  after the record is written (this lane needs nothing there)
## and outside the tick: directives() feeds the DM block, attention() feeds
## every bang in the game through SimEngine.attention_items.
##
## THE PRODUCTIVITY SEAMS (§4): the tick's own math calls sales_capacity,
## design_mult, care_eff, rnd_gain, debt_paydown and ops_mult. Every one of them
## returns EXACTLY the pre-wave number when the roster is all skill 3 and the era
## is below floor — that parity is what let this lane land on a live engine.
##
## TWIN LAW: this file and unity/Assets/Scripts/Core/Lanes/SimLabor.cs carry the
## same logic in the same order. The engines do NOT share PRNG internals, so
## parity means same checks and same behaviour, never a byte-equal draw.

# ─────────────────────────── THE MARKET SALARY TABLE ─────────────────────────
## $/wk by role × era. Real analogue: the occupational wage structure (engineers
## above designers above sales above ops above support) multiplied by the
## firm-size wage premium — a bigger employer pays ~25-35% more for the same
## occupation, which is the ~×1.3 step per column. Anchor: garage engineer 1200
## is the engine's own hire-salary default, so nothing moved under a legacy save.
## Drops: regional and experience spreads collapse into one number per role×era;
## the within-role spread is carried by the skill-ask curve, where the desk can
## show it. Benefits are not salary — they are the office lever (§5).
const ROLE_MARKET := {
	"engineer": {"garage": 1200, "coworking": 1500, "office": 2000, "floor": 2600, "hq": 3400},
	"sales":    {"garage": 1000, "coworking": 1250, "office": 1650, "floor": 2150, "hq": 2800},
	"designer": {"garage": 1050, "coworking": 1300, "office": 1750, "floor": 2250, "hq": 2950},
	"ops":      {"garage":  850, "coworking": 1050, "office": 1400, "floor": 1850, "hq": 2400},
	"support":  {"garage":  700, "coworking":  900, "office": 1150, "floor": 1500, "hq": 1950},
	"manager":  {"garage": 1450, "coworking": 1800, "office": 2400, "floor": 3000, "hq": 3900},
}
## Matched longest-first, so "sales engineer" reads as an engineer and never as
## sales — the same substring idiom the tick already uses for its head counts.
const ROLE_ORDER: Array[String] = ["engineer", "designer", "support", "manager", "sales", "ops"]

## What a skill band asks, as a multiple of the market rate. 0.70..1.60 ≈ 2.3×,
## which is real within-occupation p10-p90 wage dispersion.
const SKILL_ASK := {1: 0.70, 2: 0.85, 3: 1.00, 4: 1.25, 5: 1.60}
## The applicant quality distribution: {1:15%, 2:25%, 3:30%, 4:20%, 5:10%}.
const SKILL_WEIGHTS: Array[float] = [0.15, 0.25, 0.30, 0.20, 0.10]

const RECRUITER_FEE := 1500          ## $/wk on retainer, floor era up
const BENEFITS_PER_HEAD := 250       ## $/wk the office lever should carry, office up
const CROWD_HALF := 4.0              ## applicants waiting that halve the arrival rate
const STALE_WEEKS := 8               ## a role open this long reads as a red flag
const GRACE_WEEKS := 2               ## a candidate waits this long for free
const PATIENCE_CAP := 5              ## and is gone for certain at this many
const REVIEW_CYCLE := 12             ## weeks between synchronised comp reviews

## The keyless quirk pool (salt 24). A dressing reply replaces these in place;
## nothing waits on it — the cards are playable the instant they exist.
const QUIRK_POOL: Array[String] = [
	"brings a mechanical keyboard to interviews", "answers every question with a diagram",
	"left three startups the month before each died", "refers to money only as 'runway'",
	"has strong opinions about fonts", "already uses your product wrong",
	"asks about the pension plan, twice", "codes only between 11pm and 4am",
	"keeps a spreadsheet of past managers' flaws", "quotes their old boss like scripture",
	"negotiates via long silences", "brings homemade cookies to close deals",
	"insists on being called a craftsperson", "hosts a podcast about quitting jobs",
	"writes thank-you notes in fountain pen", "claims to have met your rival's founder",
	"will not work Wednesdays, won't say why", "laughs at their own spreadsheets",
	"alphabetizes the shared fridge", "sends follow-ups at 5am sharp",
	"wears their last employer's company shirt", "describes everything as 'basically shipping'",
	"interviews you back, taking notes", "once returned a signing bonus on principle",
]

# ═══════════════════════════ THE PURE HELPERS ════════════════════════════════
## One source for the engine, the desk and the tests — a rule the desk recomputes
## for itself is a rule that drifts.

static func era_idx(era: String) -> int:
	return maxi(0, GameState.ERAS.find(era))

## The table row a free-text role string belongs to. Unknown → engineer.
static func role_row(role: String) -> String:
	var low := role.to_lower()
	for r in ROLE_ORDER:
		if low.contains(r):
			return r
	return "engineer"

## What the street pays for this role at this stage of company.
static func market_salary(role: String, era: String) -> int:
	var row: Dictionary = ROLE_MARKET.get(role_row(role), ROLE_MARKET["engineer"])
	return int(row.get(era, row["garage"]))

## THE ERA LADDER, roles half (docs/design/00-spine.md §9): engineers and sellers
## from the first day; the specialists once there is a company to specialise in;
## managers only when there is a floor to manage.
static func role_unlocked(role: String, era: String) -> bool:
	match role_row(role):
		"engineer", "sales":
			return true
		"designer", "ops", "support":
			return era_idx(era) >= 1
		"manager":
			return era_idx(era) >= 3
	return false

## THE MARKET ITSELF opens at coworking: a garage hires the people it already
## knows (the `hire` op), and nobody answers an advert taped to a garage door.
static func market_open(era: String) -> bool:
	return era_idx(era) >= 1

## Severance norms, era-banded by tenure: a handshake becomes a package. Real
## analogue — the "1-2 weeks per year of service" rule of thumb and the
## statutory tenure bands. It is ALWAYS owed (docs/design/DECISIONS.md).
static func severance_weeks(era: String, tenure_wk: int) -> int:
	match era:
		"garage":
			return 1
		"coworking":
			return 2
		"hq":
			return 3 if tenure_wk < 78 else 4
	if tenure_wk < 26:
		return 2
	if tenure_wk < 78:
		return 3
	return 4

## How many recruiters this era will keep on retainer.
static func recruiter_cap(era: String) -> int:
	var i := era_idx(era)
	if i >= 4:
		return 2
	if i >= 3:
		return 1
	return 0

static func recruiters_active(state: GameState) -> int:
	return clampi(state.recruiters, 0, recruiter_cap(state.era))

## Seats per requisition: one role, one seat — until hq turns a role into a batch.
static func seat_cap(era: String) -> int:
	return 5 if era_idx(era) >= 4 else 1

# ── the roster reads ─────────────────────────────────────────────────────────

static func skill_of(e: Dictionary) -> int:
	return clampi(int(e.get("skill", 3)), 1, 5)

static func role_of(e: Dictionary) -> String:
	return String(e.get("role", "engineer"))

## Tenure in weeks. An unknown hire week (a legacy save, a DM-conjured hire)
## reads as tenure 0 — the world charges the shortest band, never a guess.
static func tenure_of(state: GameState, e: Dictionary) -> int:
	var hw := int(e.get("hired_week", -1))
	if hw < 0:
		return 0
	return maxi(state.week - hw, 0)

## What this person would cost on the open market today. Era moves it, which is
## real pay compression: every promotion silently underpays the veterans.
static func fair_pay(state: GameState, e: Dictionary) -> int:
	var mk := market_salary(role_of(e), state.era)
	return int(round(float(mk) * float(SKILL_ASK.get(skill_of(e), 1.0))))

## SPAN OF CONTROL (floor up): five direct reports per manager, plus the five the
## founder carries personally. Below the floor era it is exactly 1.0 — the
## founder manages everyone, which is what a small company IS.
static func span_mult(state: GameState) -> float:
	if era_idx(state.era) < 3:
		return 1.0
	var mgr_skill := 0.0
	var non_mgr := 0
	for e in state.employees:
		if role_of(e as Dictionary).to_lower().contains("manager"):
			mgr_skill += float(skill_of(e as Dictionary))
		else:
			non_mgr += 1
	if non_mgr <= 0:
		return 1.0
	var capacity := 5.0 * (1.0 + mgr_skill / 3.0)
	return clampf(capacity / float(non_mgr), 0.5, 1.0)

static func manager_count(state: GameState) -> int:
	var n := 0
	for e in state.employees:
		if role_of(e as Dictionary).to_lower().contains("manager"):
			n += 1
	return n

## Salary plus the standing cost of having a desk at all, split across every
## head. THE FULLY-LOADED COST — the number a founder underestimates once.
static func loaded_cost(state: GameState, salary: int) -> int:
	var heads := 1 + state.employees.size() + state.pipeline.size()
	var overhead := float(GameState.ERA_RENT.get(state.era, 150))
	overhead += 50.0 + float(state.traction) * 0.05          # infra, the tick's own formula
	overhead += float(state.budgets.get("office", 0))
	return salary + int(round(overhead / float(maxi(heads, 1))))

static func payroll_wk(state: GameState) -> int:
	var total := 0
	for e in state.employees:
		total += int((e as Dictionary).get("salary", 0))
	for h in state.pipeline:
		total += int((h as Dictionary).get("salary", 0))
	return total

static func loaded_payroll_wk(state: GameState) -> int:
	var total := 0
	for e in state.employees:
		total += loaded_cost(state, int((e as Dictionary).get("salary", 0)))
	for h in state.pipeline:
		total += loaded_cost(state, int((h as Dictionary).get("salary", 0)))
	return total

## What the office lever is expected to carry at office era and up.
static func expected_benefits(state: GameState) -> int:
	if era_idx(state.era) < 2:
		return 0
	return BENEFITS_PER_HEAD * (state.employees.size() + state.pipeline.size())

static func benefits_short(state: GameState) -> bool:
	var want := expected_benefits(state)
	return want > 0 and int(state.budgets.get("office", 0)) < want

## The severance invoice for one person — quoted before the deed, always.
static func severance_for(state: GameState, e: Dictionary) -> int:
	return int(e.get("salary", 0)) * severance_weeks(state.era, tenure_of(state, e))

# ── the market reads ─────────────────────────────────────────────────────────

static func open_role_row(state: GameState, role: String) -> Dictionary:
	for r in state.open_roles:
		if String((r as Dictionary).get("role", "")) == role_row(role):
			return r
	return {}

static func waiting_for(state: GameState, role: String) -> int:
	var n := 0
	for a in state.applicants:
		if String((a as Dictionary).get("role", "")) == role:
			n += 1
	return n

## THE ATTRACTIVENESS of one advert, 0..1. Real analogue, term by term: posted
## wages direct search (a better offer draws more AND better people); the 0.8×
## cliff is the reservation wage — below it nobody applies at all; hype, morale
## and era are employer brand, priced into applications instead of into wages.
static func attractiveness(state: GameState, role: String, offered: int) -> float:
	var market := market_salary(role, state.era)
	var ratio := clampf(float(offered) / maxf(float(market), 1.0), 0.0, 2.0)
	if ratio < 0.8:
		return 0.0                                    # THE FLOOR: silence
	return clampf(0.35 + 1.1 * (ratio - 1.0)
			+ float(state.hype) / 250.0
			+ (float(state.morale) - 50.0) / 250.0
			+ 0.06 * float(era_idx(state.era)), 0.0, 1.0)

## Expected applicants per week for one open role — the number the desk prints
## beside the advert, so the price/flow trade is visible before the press.
static func arrival_rate(state: GameState, row: Dictionary) -> float:
	var role := String(row.get("role", "engineer"))
	var lam := attractiveness(state, role, int(row.get("offered_salary", 0))) * 6.0
	# CROWDING: one vacancy only absorbs so much attention (matching congestion)
	lam *= CROWD_HALF / (CROWD_HALF + float(waiting_for(state, role)))
	# THE STALE ROLE: applicants read "open two months" as a warning
	if state.week - int(row.get("opened_week", state.week)) >= STALE_WEEKS:
		lam *= 0.5
	if SimEngine.has_status(state, "talent_magnet"):
		lam *= 1.5
	# THE PAID PIPELINE: one recruiter ×1.75, two ×2.5
	lam *= 1.0 + 0.75 * float(recruiters_active(state))
	return lam

## Every settable advert passes this — 0.5× to 2.0× the market rate.
static func clamp_advert(market: int, offered: int) -> int:
	return clampi(offered, int(round(float(market) * 0.5)), int(round(float(market) * 2.0)))

## And every settable salary — up to 2.5×, because keeping a star is allowed to
## cost more than hiring one.
static func clamp_salary(market: int, salary: int) -> int:
	return clampi(salary, int(round(float(market) * 0.5)), int(round(float(market) * 2.5)))

## The named ladder both steppers walk (this house has no sliders).
static func salary_steps(market: int, top: float = 2.0) -> Array:
	var out: Array = []
	for mult in [0.5, 0.6, 0.7, 0.8, 0.9, 1.0, 1.1, 1.25, 1.4, 1.6, 1.8, 2.0, 2.25, 2.5]:
		if float(mult) > top + 0.001:
			break
		out.append(float(int(round(float(market) * float(mult) / 10.0)) * 10))
	return out

## How many more heads this era has desks for. The pipeline counts: it is already
## on the payroll (the engine's own can_hire() forgets that — spec §10.1).
static func seats_left(state: GameState) -> int:
	return maxi(state.staff_cap() - state.employees.size() - state.pipeline.size(), 0)

# ═════════════════════ HIRE / REJECT / FIRE / RAISE (the desk's API) ══════════

## Post an advert. Refuses a duplicate role, a locked role, a shut market and a
## full house — the engine is the bouncer, the desk only prints the reason.
static func open_role(state: GameState, role: String, offered: int) -> bool:
	var r := role_row(role)
	if not market_open(state.era) or not role_unlocked(r, state.era):
		return false
	if not open_role_row(state, r).is_empty():
		return false
	if state.open_roles.size() >= seats_left(state):
		return false
	var market := market_salary(r, state.era)
	state.open_roles.append({"role": r, "offered_salary": clamp_advert(market, offered),
		"opened_week": state.week, "seats": 1})
	return true

static func set_role_salary(state: GameState, role: String, offered: int) -> void:
	var row := open_role_row(state, role)
	if row.is_empty():
		return
	row["offered_salary"] = clamp_advert(market_salary(String(row["role"]), state.era), offered)

## Close the requisition. The people already waiting stay waiting — they simply
## run out of patience like everyone else does.
static func close_role(state: GameState, role: String) -> void:
	var r := role_row(role)
	for i in state.open_roles.size():
		if String((state.open_roles[i] as Dictionary).get("role", "")) == r:
			state.open_roles.remove_at(i)
			return

## Requisition batching, hq only.
static func set_seats(state: GameState, role: String, seats: int) -> void:
	var row := open_role_row(state, role)
	if row.is_empty():
		return
	row["seats"] = clampi(seats, 1, seat_cap(state.era))

static func set_recruiters(state: GameState, n: int) -> void:
	state.recruiters = clampi(n, 0, recruiter_cap(state.era))

## THE ADVERT IS THE MAGNET, THE ASK IS THE CONTRACT: a hire is booked at what
## the candidate asked, never at what the role advertised. They join the existing
## two-week onboarding pipeline — paid at once, productive when it graduates.
static func hire_applicant(state: GameState, idx: int) -> Dictionary:
	if idx < 0 or idx >= state.applicants.size():
		return {}
	if seats_left(state) <= 0:
		return {}
	var a: Dictionary = state.applicants[idx]
	state.pipeline.append({"name": String(a.get("name", "a hire")),
		"role": String(a.get("role", "engineer")),
		"salary": int(a.get("ask", 1200)), "weeks_in": 0,
		"quirk": String(a.get("quirk", "")), "skill": skill_of(a)})
	state.applicants.remove_at(idx)
	var row := open_role_row(state, String(a.get("role", "engineer")))
	if not row.is_empty():
		row["seats"] = int(row.get("seats", 1)) - 1
		if int(row["seats"]) <= 0:
			close_role(state, String(row["role"]))
	return {"name": String(a.get("name", "a hire")), "role": String(a.get("role", "engineer")),
		"salary": int(a.get("ask", 1200)), "skill": skill_of(a)}

static func reject_applicant(state: GameState, idx: int) -> void:
	if idx >= 0 and idx < state.applicants.size():
		state.applicants.remove_at(idx)

## LETTING SOMEONE GO COSTS REAL MONEY, ALWAYS (docs/design/DECISIONS.md — no
## for-cause waiver; the world charges you anyway, which is also the anti-exploit
## against fire-and-rehire cycling). The invoice accrues now and is BOOKED by
## next week's money section, so the P&L identity never has to bend for it.
## The −8 morale is layoff survivor syndrome: firings depress the people who stay.
static func fire_employee(state: GameState, idx: int) -> Dictionary:
	if idx < 0 or idx >= state.employees.size():
		return {}
	var e: Dictionary = state.employees[idx]
	var weeks := severance_weeks(state.era, tenure_of(state, e))
	var pay := int(e.get("salary", 0))
	var out := {"name": String(e.get("name", "someone")), "severance": pay * weeks,
		"weeks": weeks, "salary": pay, "tenure": tenure_of(state, e)}
	state.employees.remove_at(idx)
	state.severance_due += int(out["severance"])
	# THE RECEIPT IS WRITTEN NOW and printed next week, as a finished line: the
	# arithmetic that produced it (weeks × salary × tenure band) cannot survive
	# in a state that only carries the total.
	var notes: Array = state.get_meta("severance_notes", [])
	notes.append("severance: $%s (%d wks × $%s — tenure %d wks)" % [
		money(int(out["severance"])), weeks, money(pay), int(out["tenure"])])
	state.set_meta("severance_notes", notes)
	state.morale = clampi(state.morale - 8, 0, 100)
	return out

## A raise, clamped to the market band. Crossing 0.95× fair pay clears the ask
## and buys the small morale bump that fixing an injustice actually buys.
static func grant_raise(state: GameState, idx: int, new_salary: int) -> int:
	if idx < 0 or idx >= state.employees.size():
		return 0
	var e: Dictionary = state.employees[idx]
	var paid := clamp_salary(market_salary(role_of(e), state.era), new_salary)
	e["salary"] = paid
	var fair := fair_pay(state, e)
	if float(paid) >= float(fair) * 0.95:
		if bool(e.get("wants_raise", false)):
			e["wants_raise"] = false
			e["asked_week"] = -1
			state.morale = clampi(state.morale + 2, 0, 100)
		e["underpaid_since"] = -1
	return paid

# ═══════════════════════════ THE POACH INTERFACE ═════════════════════════════
## The rivals lane rolls the poach and owns the steal receipt; this lane owns the
## roster and the consequences (docs/design/00-spine.md §13).

## Who a raider would bid for: the best person paid furthest under their worth.
## Real analogue: Lazear's raiding model — outside firms bid precisely for high
## ability paid below marginal product; 1.25× is the raider's margin. {} = nobody.
## `market_salary` is what THIS person is worth (their skill at the going rate),
## and `pay_gap` = (worth − salary) / worth ≥ 0.2 is the same test as ≥ 1.25×.
static func poach_target(state: GameState) -> Dictionary:
	var best := -1
	var best_skill := -1
	var best_gap := -1.0
	for i in state.employees.size():
		var e: Dictionary = state.employees[i]
		var sal := maxi(int(e.get("salary", 0)), 1)
		var gap := float(fair_pay(state, e)) / float(sal)
		if gap < 1.25:
			continue
		var sk := skill_of(e)
		if sk > best_skill or (sk == best_skill and gap > best_gap):
			best = i
			best_skill = sk
			best_gap = gap
	if best < 0:
		return {}
	var pick: Dictionary = state.employees[best]
	var salary := maxi(int(pick.get("salary", 0)), 1)
	var worth := fair_pay(state, pick)
	return {"index": best, "name": String(pick.get("name", "someone")),
		"role": role_of(pick), "skill": skill_of(pick), "salary": salary,
		"fair": worth, "market_salary": worth,
		"gap_pct": (float(worth) / float(salary) - 1.0) * 100.0,
		"pay_gap": float(worth - salary) / float(maxi(worth, 1))}

## THE STEAL LANDED. They leave now, no severance (they were not let go), and the
## room feels it exactly the way a resignation feels.
static func poach_lands(state: GameState, index: int, rival: String = "a rival") -> Dictionary:
	if index < 0 or index >= state.employees.size():
		return {}
	var e: Dictionary = state.employees[index]
	var worth := fair_pay(state, e)
	var out := {"name": String(e.get("name", "someone")), "role": role_of(e),
		"skill": skill_of(e), "salary": int(e.get("salary", 0)), "rival": rival,
		"line": "%s left for %s: paid %.2f× market and somebody noticed" % [
			String(e.get("name", "someone")), rival,
			float(int(e.get("salary", 0))) / maxf(float(worth), 1.0)]}
	state.employees.remove_at(index)
	state.morale = clampi(state.morale - 6, 0, 100)
	_mark_poach(state, rival, String(out["name"]))
	return out

## THE STEAL FAILED — and that is not the end of it. COUNTER-OFFER DYNAMICS
## (docs/design/DECISIONS.md): being courted teaches a person exactly what they
## are worth, so their ask hardens. Mechanically the resignation clock is already
## two weeks in, so the ordinary 0.85× tolerance no longer buys time — only a
## real raise to fair pay keeps them, and only if it lands next week.
static func poach_failed(state: GameState, index: int, rival: String = "a rival") -> Dictionary:
	if index < 0 or index >= state.employees.size():
		return {}
	var e: Dictionary = state.employees[index]
	_mark_poach(state, rival, String(e.get("name", "someone")))
	var worth := fair_pay(state, e)
	return {"index": index, "name": String(e.get("name", "someone")), "role": role_of(e),
		"salary": int(e.get("salary", 0)), "fair": worth, "market_salary": worth, "rival": rival,
		"line": _harden_ask(state, index, rival)}

## The counter-offer itself, in one place: the ask is now hard, and the clock it
## runs on is already two weeks in — the ordinary 0.85× tolerance has stopped
## buying time. Returns the receipt line, or "" when there is nobody to harden.
static func _harden_ask(state: GameState, index: int, rival: String) -> String:
	if index < 0 or index >= state.employees.size():
		return ""
	var e: Dictionary = state.employees[index]
	e["wants_raise"] = true
	e["asked_week"] = state.week - 2
	if int(e.get("underpaid_since", -1)) < 0:
		e["underpaid_since"] = state.week
	return "%s turned %s down and came back with a number: $%s/wk against a market of $%s" % [
		String(e.get("name", "someone")), rival,
		money(int(e.get("salary", 0))), money(fair_pay(state, e))]

## THE COUNTER-OFFER SEASON (docs/design/DECISIONS.md). The rivals lane resolves
## its own poach at tick §6a — AFTER this section — and leaves a marker when the
## call failed. Being courted teaches a person exactly what they are worth, so
## the first thing this desk does the following week is read that marker and
## harden the ask. The marker is consumed, so one failed call raises one ask.
static func _consume_counter_offer(state: GameState, rep: Dictionary) -> void:
	if int(state.get_meta("poach_failed_wk", -1)) < 0:
		return
	var who := String(state.get_meta("poach_failed_name", ""))
	state.set_meta("poach_failed_wk", -1)
	state.set_meta("poach_failed_name", "")
	for i in state.employees.size():
		if String((state.employees[i] as Dictionary).get("name", "")) != who:
			continue
		var line := _harden_ask(state, i, String(state.get_meta("poach_rival", "a rival")))
		if line != "":
			rep["events"].append(line)
		return

static func _mark_poach(state: GameState, rival: String, who: String) -> void:
	state.set_meta("poach_wk", state.week)
	state.set_meta("poach_rival", rival)
	state.set_meta("poach_name", who)

# ═════════════════════════════ THE WEEKLY TICK ═══════════════════════════════

## Tick §3b: arrivals (salt 20/21) → applicant decay (22) → review cycle, raise
## asks and resignations (23). The roster must be FINAL here: §4 reads it for
## morale and §9 pays it, so a quitter is off this week's payroll.
static func tick_pre(state: GameState, rep: Dictionary) -> void:
	rep["applicants_new"] = 0
	_arrivals(state, rep)
	_decay(state, rep)
	_ladder(state, rep)
	# THE FLOOR DRAGS WITHOUT MANAGERS — named, with the number, so the fix reads
	# as "hire a manager" and never as "hire more of everyone".
	var sm := span_mult(state)
	if sm < 1.0:
		rep["lines"].append("span of control: %d heads, %d manager(s) — the floor runs at %d%%"
			% [state.employees.size() - manager_count(state), manager_count(state),
			int(round(sm * 100.0))])

## The money section. The severance was incurred at the desk and is booked HERE,
## one week later, so the week that pays it is the week that prints it.
static func tick_money(state: GameState, rep: Dictionary, m: Dictionary) -> void:
	var due := int(state.severance_due)
	if due > 0:
		m["severance"] = float(m.get("severance", 0.0)) + float(due)
		var notes: Array = state.get_meta("severance_notes", [])
		if notes.is_empty():
			rep["lines"].append("severance: $%s — the invoice for letting someone go" % money(due))
		else:
			for n in notes:
				rep["lines"].append(String(n))
		state.severance_due = 0
		state.set_meta("severance_notes", [])
	var rc := recruiters_active(state)
	if rc > 0:
		var cost := RECRUITER_FEE * rc
		m["recruiting"] = float(m.get("recruiting", 0.0)) + float(cost)
		rep["lines"].append("recruiter on retainer: −$%s/wk (applicant flow ×%.2f)"
			% [money(cost), 1.0 + 0.75 * float(rc)])

## Nothing in this lane needs the finished payroll.
static func tick_post(_state: GameState, _rep: Dictionary) -> void:
	pass

# ── 3b(a) ARRIVALS ───────────────────────────────────────────────────────────
static func _arrivals(state: GameState, rep: Dictionary) -> void:
	if state.open_roles.is_empty():
		return
	var r20 := SimEngine.rng_for(state, SimEngine.SALT_LABOR_ARRIVALS)
	var r21 := SimEngine.rng_for(state, SimEngine.SALT_LABOR_STATS)
	var r24 := SimEngine.rng_for(state, SimEngine.SALT_LABOR_POOLS)
	var born := 0
	for row in state.open_roles:
		var rd: Dictionary = row
		var role := String(rd.get("role", "engineer"))
		var market := market_salary(role, state.era)
		var offered := int(rd.get("offered_salary", market))
		# Binomial(10, λ/10): 0..10 arrivals with mean λ — Poisson-shaped weekly
		# vacancy yield, capped so a runaway advert cannot flood the desk.
		var p := minf(arrival_rate(state, rd), 10.0) / 10.0
		var count := 0
		for _i in 10:
			if r20.randf() < p:
				count += 1
		if count <= 0:
			continue
		var ratio := float(offered) / maxf(float(market), 1.0)
		for _c in count:
			var sk := _draw_skill(r21, ratio)
			var ask := clamp_salary(market,
				_round10(float(market) * float(SKILL_ASK[sk]) * r21.randf_range(0.90, 1.15)))
			var src := "inbound"
			# THE REFERRAL: a happy team is a recruiting channel, and a referred
			# candidate takes a little less to join a shop somebody vouched for.
			if born == 0 and state.morale >= 70:
				src = "referral"
				ask = _round10(float(ask) * 0.95)
			state.applicants.append({"name": _pool_name(state, r24), "role": role,
				"skill": sk, "ask": ask, "quirk": QUIRK_POOL[r24.randi() % QUIRK_POOL.size()],
				"one_liner": "", "applied_week": state.week, "source": src})
			born += 1
		rep["lines"].append("%d applied for %s (advert $%s vs market $%s)"
			% [count, role.to_upper(), money(offered), money(market)])
	rep["applicants_new"] = born

## Overpay attracts talent: above 1.25× market a weak draw is rerolled once — a
## better offer improves the POOL, not only its size.
static func _draw_skill(rng: RandomNumberGenerator, ratio: float) -> int:
	var sk := _weighted_skill(rng.randf())
	if sk <= 2 and ratio >= 1.25:
		sk = _weighted_skill(rng.randf())
	return sk

static func _weighted_skill(u: float) -> int:
	var acc := 0.0
	for i in SKILL_WEIGHTS.size():
		acc += SKILL_WEIGHTS[i]
		if u < acc:
			return i + 1
	return 5

## A name nobody in the building already answers to (≤5 tries, then it stands).
static func _pool_name(state: GameState, rng: RandomNumberGenerator) -> String:
	var taken := taken_names(state)
	var nm := ""
	for _i in 5:
		nm = WorldGen.person_name(rng)
		if not taken.has(nm):
			return nm
	return nm

static func taken_names(state: GameState) -> Array:
	var out: Array = []
	if state.founder_name != "":
		out.append(state.founder_name)
	for c in state.cofounders:
		out.append(String((c as Dictionary).get("name", "")))
	for e in state.employees:
		out.append(String((e as Dictionary).get("name", "")))
	for h in state.pipeline:
		out.append(String((h as Dictionary).get("name", "")))
	for a in state.applicants:
		out.append(String((a as Dictionary).get("name", "")))
	return out

# ── 3b(b) PATIENCE ───────────────────────────────────────────────────────────
## Candidate off-market decay: two weeks of grace, then a skill-weighted weekly
## roll (the good ones are holding competing offers), then a hard shelf-life.
## Real analogue: recruiting's "the best are gone in ten days", on a weekly tick.
static func _decay(state: GameState, rep: Dictionary) -> void:
	if state.applicants.is_empty():
		return
	var r22 := SimEngine.rng_for(state, SimEngine.SALT_LABOR_PATIENCE)
	var kept: Array = []
	for a in state.applicants:
		var ad: Dictionary = a
		var waiting := state.week - int(ad.get("applied_week", state.week))
		var gone := false
		if waiting >= PATIENCE_CAP:
			gone = true                                   # offer shelf-life, hard
		elif waiting > GRACE_WEEKS:
			var p := 0.20 + 0.06 * float(skill_of(ad))
			if state.era == "garage":
				p -= 0.05                                 # scrappy joiners hold fewer offers
			gone = r22.randf() < p
		if not gone:
			kept.append(ad)
			continue
		var role := String(ad.get("role", "engineer"))
		var row := open_role_row(state, role)
		var line := ""
		if row.is_empty():
			line = "%s stopped waiting on %s after %d wks (the role closed under them)" % [
				String(ad.get("name", "someone")), role.to_upper(), waiting]
		else:
			line = "%s stopped waiting on %s after %d wks (your advert: %.2f× market)" % [
				String(ad.get("name", "someone")), role.to_upper(), waiting,
				float(int(row.get("offered_salary", 0)))
					/ maxf(float(market_salary(role, state.era)), 1.0)]
		rep["lines"].append(line)
		if skill_of(ad) >= 4:
			rep["events"].append(line)                    # losing a good one is a beat
	state.applicants = kept

# ── 3b(c) THE REVIEW CYCLE, THE ASKS, THE RESIGNATIONS ───────────────────────
static func _ladder(state: GameState, rep: Dictionary) -> void:
	_consume_counter_offer(state, rep)
	var short_benefits := benefits_short(state)
	if short_benefits:
		# A REAL OFFICE EXPECTS BENEFITS: the office lever IS the benefits budget
		# (benefits ≈ 30% of comp at an established firm). Unfunded, it costs
		# morale and it brings every pay conversation forward.
		rep["lines"].append("a real office expects benefits: office $%s vs $%s expected"
			% [money(int(state.budgets.get("office", 0))), money(expected_benefits(state))])
		state.morale = clampi(state.morale - 1, 0, 100)
	# THE COMP REVIEW (office up): real companies synchronise pay conversations,
	# which is why underpayment surfaces all at once and never quietly.
	if era_idx(state.era) >= 2 and state.week % REVIEW_CYCLE == 0:
		var compared := 0
		for e in state.employees:
			var ed: Dictionary = e
			if float(int(ed.get("salary", 0))) < float(fair_pay(state, ed)) * 0.85:
				compared += 1
				if not bool(ed.get("wants_raise", false)):
					ed["wants_raise"] = true
					ed["asked_week"] = state.week
		if compared > 0:
			rep["lines"].append("review week: %d people compare their pay to the market" % compared)
	if state.employees.is_empty():
		return
	var r23 := SimEngine.rng_for(state, SimEngine.SALT_LABOR_LADDER)
	var kept: Array = []
	for e in state.employees:
		var ed: Dictionary = e
		var fair := fair_pay(state, ed)
		var salary := int(ed.get("salary", 0))
		var ratio := float(salary) / maxf(float(fair), 1.0)
		if ratio < 0.85:
			if int(ed.get("underpaid_since", -1)) < 0:
				ed["underpaid_since"] = state.week        # the receipt's clock starts
		else:
			ed["underpaid_since"] = -1
		if not bool(ed.get("wants_raise", false)):
			var asked := false
			if ratio < 0.60:
				# equity theory: insulting pay never waits for a review cycle
				asked = true
			elif ratio < 0.85:
				var p := 0.15
				if state.era == "garage":
					p *= 0.5                              # nobody benchmarks in a garage
				if short_benefits:
					p += 0.05
				asked = r23.randf() < p
			if asked:
				ed["wants_raise"] = true
				ed["asked_week"] = state.week
				rep["events"].append("%s wants a raise: $%d now, market says $%d (%.2f×)"
					% [String(ed.get("name", "someone")), salary, fair, ratio])
			kept.append(ed)
			continue
		if ratio >= 0.85:
			ed["wants_raise"] = false                     # paid up: the ladder resets
			ed["asked_week"] = -1
			kept.append(ed)
			continue
		# THE EFFICIENCY-WAGE QUIT FUNCTION: quit rates rise as the relative wage
		# falls, and the better they are the better their outside option. Three
		# weeks of being ignored is certain — by then they are already interviewing.
		var since := state.week - int(ed.get("asked_week", state.week))
		var quits := false
		if since >= 3:
			quits = true
		elif since >= 1:
			quits = r23.randf() < 0.20 + 0.05 * float(skill_of(ed))
		if not quits:
			kept.append(ed)
			continue
		var since_wk := int(ed.get("underpaid_since", -1))
		var weeks := maxi(state.week - since_wk, 1) if since_wk >= 0 else maxi(since, 1)
		rep["events"].append("%s resigned: paid %.2f× market for %d weeks"
			% [String(ed.get("name", "someone")), ratio, weeks])
		state.morale = clampi(state.morale - 6, 0, 100)   # no severance: they left
	state.employees = kept

# ═════════════════ §4 SKILL ECONOMICS — the tick's own math ══════════════════
## Within-occupation productivity dispersion is real and large (output SD ≈ 20-48%
## of the mean in complex work), which is the 1..5 linear spread. Every function
## below returns EXACTLY the pre-wave number for a skill-3 roster below floor
## era: `default_v` is what the tick computed before this lane existed.
## Drops: no team chemistry, and no skill growth on the job — a flat roster stays
## readable, and training is a later wave.

static func _skill_sum(state: GameState, role: String) -> float:
	var total := 0.0
	for e in state.employees:
		if role_of(e as Dictionary).to_lower().contains(role):
			total += float(skill_of(e as Dictionary))
	return total

## SALES → CLOSING CAPACITY. Quota-carrying rep math: a skill-3 seller is exactly
## the 3.0 the tick used to hardcode, a closer is 5.0, a bad hire is 1.0.
static func sales_capacity(state: GameState, default_v: float) -> float:
	if state.employees.is_empty():
		return default_v
	return span_mult(state) * _skill_sum(state, "sales")

## DESIGNERS → ADOPTION POLISH. Design quality lifts conversion and word of
## mouth; capped at +30%, because polish is not a growth strategy.
static func design_mult(state: GameState) -> float:
	if state.employees.is_empty():
		return 1.0
	return 1.0 + minf(0.03 * span_mult(state) * _skill_sum(state, "designer"), 0.30)

## SUPPORT → RETENTION. The service-profit chain: a skill-3 support head is worth
## about $1,500/wk of care budget, and the existing 30% churn cap still caps.
static func care_eff(state: GameState, b_care: float) -> float:
	if state.employees.is_empty():
		return b_care
	return b_care + span_mult(state) * 500.0 * _skill_sum(state, "support")

## ENGINEERS → PRODUCT. A skill-3 engineer ships +0.5 quality a week with no
## budget at all, which is what an engineer IS.
static func rnd_gain(state: GameState, default_v: float) -> float:
	if state.employees.is_empty():
		return default_v
	return default_v + span_mult(state) * _skill_sum(state, "engineer") / 6.0

## ENGINEERS → DEBT. The same hands pay down what the same hands wrote.
static func debt_paydown(state: GameState, default_v: float) -> float:
	if state.employees.is_empty():
		return default_v
	return default_v + span_mult(state) * _skill_sum(state, "engineer") * 0.10

## OPS → THE UNFORESEEN. Operational maturity cuts unplanned cost, floored at
## −60%. Deliberately NOT span-damped: firefighting does not need a manager.
static func ops_mult(state: GameState) -> float:
	if state.employees.is_empty():
		return 1.0
	return maxf(0.4, 1.0 - 0.08 * _skill_sum(state, "ops"))

# ═════════════════════════ DM CONTEXT AND ATTENTION ══════════════════════════

## DM context lines, section 6 of the DIRECTIVES block (docs/design/00-spine.md §5).
## The DM narrates these receipts; it never re-prices them.
static func directives(state: GameState) -> Array[String]:
	var out: Array[String] = []
	for row in state.open_roles:
		var rd: Dictionary = row
		var role := String(rd.get("role", "engineer"))
		var waiting := waiting_for(state, role)
		var best_skill := 0
		var best_ask := 0
		for a in state.applicants:
			var ad: Dictionary = a
			if String(ad.get("role", "")) == role and skill_of(ad) > best_skill:
				best_skill = skill_of(ad)
				best_ask = int(ad.get("ask", 0))
		if waiting > 0:
			out.append("- Hiring: %d applicants for %s (best: skill %d, asks $%d/wk)."
				% [waiting, role, best_skill, best_ask])
		else:
			out.append("- Hiring: %s advertised at $%d (market $%d) and nobody has applied."
				% [role, int(rd.get("offered_salary", 0)), market_salary(role, state.era)])
	if int(state.get_meta("poach_wk", -99)) == state.week:
		out.append("- POACH: %s is courting %s ($%d/wk, underpaid)."
			% [String(state.get_meta("poach_rival", "a rival")),
			String(state.get_meta("poach_name", "someone")), _poached_salary(state)])
	for e in state.employees:
		var ed: Dictionary = e
		if bool(ed.get("wants_raise", false)):
			out.append("- %s (%s) wants a raise; refusing much longer risks a resignation."
				% [String(ed.get("name", "someone")), role_of(ed)])
	if benefits_short(state):
		out.append("- The office expects benefits: the office lever is $%d for %d staff."
			% [int(state.budgets.get("office", 0)),
			state.employees.size() + state.pipeline.size()])
	return out

static func _poached_salary(state: GameState) -> int:
	var nm := String(state.get_meta("poach_name", ""))
	for e in state.employees:
		if String((e as Dictionary).get("name", "")) == nm:
			return int((e as Dictionary).get("salary", 0))
	return 0

## Attention rows — the crew desk. Every label is what the garage ticker prints
## verbatim, so it names the business problem and never the mechanic.
static func attention(state: GameState) -> Array:
	var out: Array = []
	if state.applicants.size() > 0:
		out.append({"desk": "crew", "key": "applicants_waiting", "severity": 1,
			"label": ("%d waiting on your advert" % state.applicants.size()).left(40)})
	var wanter := ""
	var leaving := ""
	for e in state.employees:
		var ed: Dictionary = e
		if not bool(ed.get("wants_raise", false)):
			continue
		if wanter == "":
			wanter = String(ed.get("name", "someone"))
		if leaving == "" and state.week - int(ed.get("asked_week", state.week)) >= 2:
			leaving = String(ed.get("name", "someone"))
	if wanter != "":
		out.append({"desk": "crew", "key": "wants_raise", "severity": 2,
			"label": ("%s wants market pay" % wanter).left(40)})
	if leaving != "":
		out.append({"desk": "crew", "key": "quit_risk", "severity": 3,
			"label": ("%s resigns next week unpaid" % leaving).left(40)})
	for row in state.open_roles:
		var rd: Dictionary = row
		var role := String(rd.get("role", "engineer"))
		var age := state.week - int(rd.get("opened_week", state.week))
		if age >= STALE_WEEKS and waiting_for(state, role) == 0:
			out.append({"desk": "crew", "key": "silent_role", "severity": 2,
				"label": ("%s open %d wks, nobody applied" % [role, age]).left(40)})
			break
	if span_mult(state) < 1.0:
		out.append({"desk": "crew", "key": "span_thin", "severity": 2,
			"label": ("the floor runs at %d%% — too few managers"
				% int(round(span_mult(state) * 100.0))).left(40)})
	if int(state.get_meta("poach_wk", -99)) == state.week:
		out.append({"desk": "crew", "key": "poach_attempt", "severity": 3,
			"label": ("a rival is courting %s"
				% String(state.get_meta("poach_name", "your best"))).left(40)})
	return out

# ═══════════════════════ THE LLM DRESSING SEAM (§8.1) ════════════════════════
## Applicants are BORN playable with pool names and quirks. The one batch call
## replaces dressing fields IN PLACE when it lands; it never touches a number,
## and a reply that does not match the week's arrivals is discarded whole.

## The user payload for the dressing call, or {} when nobody arrived this week
## (no arrivals → no call fires).
static func dressing_payload(state: GameState) -> Dictionary:
	var fresh: Array = []
	for a in state.applicants:
		var ad: Dictionary = a
		if int(ad.get("applied_week", -1)) == state.week:
			fresh.append({"role": String(ad.get("role", "engineer")), "skill": skill_of(ad),
				"ask": int(ad.get("ask", 0)), "source": String(ad.get("source", "inbound"))})
	if fresh.is_empty():
		return {}
	var team: Array = []
	for e in state.employees:
		team.append(String((e as Dictionary).get("name", "")))
	return {"company": {"name": state.company_name, "idea": state.company_idea,
			"what": state.biz_what, "who": state.biz_who, "era": state.era},
		"team": team, "taken_names": taken_names(state), "candidates": fresh}

## Land a reply. Returns how many candidates were dressed; 0 means the whole
## reply was discarded and the pool dressing stands — which is a complete card
## either way, so nothing in the game is ever waiting on this.
static func dress_applicants_rows(state: GameState, rows: Array) -> int:
	var fresh: Array = []
	for i in state.applicants.size():
		if int((state.applicants[i] as Dictionary).get("applied_week", -1)) == state.week:
			fresh.append(i)
	if fresh.is_empty() or rows.size() != fresh.size():
		return 0
	var taken := taken_names(state)
	for i in fresh.size():
		if not (rows[i] is Dictionary):
			return 0
		var nm := String((rows[i] as Dictionary).get("name", "")).strip_edges()
		if nm == "":
			return 0
		var own := String((state.applicants[fresh[i]] as Dictionary).get("name", ""))
		if nm != own and taken.has(nm):
			return 0
	for i in fresh.size():
		var rd: Dictionary = rows[i]
		var ad: Dictionary = state.applicants[fresh[i]]
		ad["name"] = String(rd.get("name", "")).strip_edges().left(40)
		var q := String(rd.get("quirk", "")).strip_edges()
		if q != "":
			ad["quirk"] = q.left(60)
		ad["one_liner"] = String(rd.get("one_liner", "")).strip_edges().left(90)
	return fresh.size()

# ─────────────────────────────── small hands ─────────────────────────────────
static func _round10(v: float) -> int:
	return int(round(v / 10.0)) * 10

## The desk's money hand, so a receipt and a card read the same number the same way.
static func money(n: int) -> String:
	var s := str(absi(n))
	var out := ""
	while s.length() > 3:
		out = "," + s.substr(s.length() - 3) + out
		s = s.substr(0, s.length() - 3)
	return ("-" if n < 0 else "") + s + out
