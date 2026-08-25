class_name SimBoard
extends RefCounted
## LANE 08 — THE BOARD & M&A (covenants, offers, the exit). Spec: docs/design/08-board-mna.md
##
## One file because it is one arc: the check you took installs a room that
## measures you, the record you build in that room prices the next check, and
## the same record decides whether anyone ever offers to buy the whole thing.
##
##   THE BOARD is a plan of record. A priced round writes a growth covenant and
##   a quarterly review date; the review is DETERMINISTIC — revenue against a
##   bar, no dice — and it walks the real intervention ladder: a hard meeting,
##   then the coach a board sends before it does worse, then the reprice that is
##   a down round waiting to happen. Governance HARDENS with the company: an
##   angel's handshake in the garage, audit-committee cadence at hq.
##
##   M&A is a courtship on a clock. Offers arrive on their own dice (salt 100),
##   priced as a premium on YOUR standalone valuation, and every one of them
##   dies in two weeks unless it is signed. Writing any other move IS walking
##   away. At hq the IPO window opens as the alternative exit — weather, not a
##   decision: clean covenants in a market that is buying.
##
## The spine calls, in tick order (docs/design/00-spine.md §1, HOOKS.md):
##   tick_pre   tick §9 — nothing: this lane reads a week that is already closed
##   tick_money the money section — this lane owns NO P&L lane (the coach the
##              board sends bills through `commitments`, like any standing cost)
##   tick_post  §9c the board review, then §9d M&A lapse → generation → the
##              IPO window, both reading the revenue and valuation this week
##              just posted
## and outside the tick: directives() feeds the DM block, attention() feeds
## every bang in the game through SimEngine.attention_items, and the two
## journal seams draw the offer and take the signature.
##
## SALT (docs/design/00-spine.md §3): 100, M&A arrival + premium. ONE stream,
## drawn in the fixed trigger order below — the order is part of the
## determinism contract, so reordering the ladder re-rolls every run's history.
## The review has no salt at all: a covenant is arithmetic, and a board that
## rolled dice would not be teaching anything.
##
## THE MONEY LAW: every number here is the engine's. The DM narrates the room,
## names the firm behind "a quiet strategic" and gives the coach a face — it
## never decides met/missed, a strike, a price, a premium or an expiry.

# ── the covenant's constants ────────────────────────────────────────────────
## A pre-revenue raise still gets a concrete bar: the floor a board would plan
## from at each stage, so "grow 30%" is never 30% of nothing.
const ERA_REV_FLOOR := {"garage": 40, "coworking": 120, "office": 500,
	"floor": 2000, "hq": 8000}

## Growth persistence decay. Big bases grow slower and real boards plan for
## exactly that, so the ask softens as the company gets heavy.
const ERA_TARGET_MULT := {"garage": 1.0, "coworking": 1.0, "office": 0.9,
	"floor": 0.8, "hq": 0.65}

const REVIEW_CADENCE := 12       ## 12 weeks ≈ the quarterly board meeting
const GOODWILL_CAP := 3
const COACH_WEEKS := 6           ## a coaching engagement, not a lunch
const COACH_MIN := 250
const COACH_MAX := 2500
const FUNDING_MULT_FLOOR := 0.5  ## repeated strike-threes converge, never to zero
const SECONDARY_WINDOW := 4      ## weeks after a signing or a review that a founder sale is on the table

# ── M&A's constants ─────────────────────────────────────────────────────────
const NO_SHOP_WEEKS := 2         ## the exploding LOI, at game scale
const MNA_COOLDOWN := 10         ## corp dev does not re-approach the week after a lapse
const MNA_FIRST_WEEK := 6        ## nobody buys a company that is four weeks old
const SNIFF_LAPSE := 8           ## interest that never became an offer goes cold
const MIN_PRICE := 10_000

## The one-shot valuation bands a strategic notices you crossing.
const BANDS := [[50_000_000, "mna_band_50m"], [10_000_000, "mna_band_10m"],
	[2_000_000, "mna_band_2m"]]

## THE ARM (docs/design/10-interface-language.md §2.9). Selling the company is
## the heaviest act in the game, so the card takes two taps — and the journal
## seam has no screen-local bool to hold the first one. The key is
## (seed, week, card): a different run, a different week or a different offer
## can never inherit an arm, and nothing durable is invented to store it.
static var _armed_key := ""

# ═══════════════════════════ THE SPINE'S ENTRY POINTS ═══════════════════════
## Tick §9, before the money. Nothing: a covenant is measured against a week
## that has closed, and an offer is priced off a valuation that has not been
## computed yet. Both wait for tick_post — deliberately, not by omission.
static func tick_pre(_state: GameState, _rep: Dictionary) -> void:
	pass

## The money section. This lane writes no P&L lane of its own: the executive
## coach the board sends is a standing cost like any other and bills through
## `commitments`, so it shows up in the ledger under its own name with no new
## column to explain.
static func tick_money(_state: GameState, _rep: Dictionary, _m: Dictionary) -> void:
	pass

## After the record is written: §9c the board review against the covenant
## (deterministic — no dice, no salt), then §9d M&A offers and the IPO window
## (salt 100), priced off the growth this week just posted.
static func tick_post(state: GameState, rep: Dictionary) -> void:
	# A signed exit ends the run on the next week change. Nothing may reprice
	# the company between the signature and the ceremony.
	if state.exit_value > 0 or state.dead:
		return
	_review(state, rep)
	_mna(state, rep)
	_ipo_window(state, rep)

# ═══════════════════════════ §2 STAGE — what exists when ════════════════════
## Governance grows when the company does. Read LIVE at every review and every
## signing, mirroring how a real board hardens from an angel's phone call to
## audit-committee cadence: 0 garage · 1 coworking · 2 office · 3 floor · 4 hq.
static func board_stage(state: GameState) -> int:
	return state.era_index()

## The option pool a term sheet asks for at this stage, in points written
## PRE-money. Nothing below office: an angel does not paper an ESOP. Later
## rounds top up rather than create, so the hq ask is half.
static func pool_ask_pct(state: GameState) -> float:
	var stage := board_stage(state)
	if stage == 2 or stage == 3:
		return 10.0
	return 5.0 if stage >= 4 else 0.0

## The strike ceiling this stage can reach: no ladder at all in the garage, a
## two-rung ladder at coworking (the coach, and nothing harsher), the full
## three from office up.
static func strike_cap(state: GameState) -> int:
	var stage := board_stage(state)
	if stage <= 0:
		return 0
	return 2 if stage == 1 else 3

## THE COVENANT, %/quarter. Round by round it is T2D3: a seed/A company
## tripling ARR yearly compounds at ~31%/quarter, and each round resets the bar
## higher (30/35/40/45). The era multiplier is growth-persistence decay; the
## season is the board re-forecasting to the climate — a winter board asks for
## less, not for nothing, and a boom board asks for more.
static func board_target_pct(state: GameState) -> float:
	var rounds := state.rounds_raised.size()
	var base := 25.0 + 5.0 * float(mini(rounds, 4))
	var era_m := float(ERA_TARGET_MULT.get(state.era, 1.0))
	var mac_m := 1.0
	if state.macro_season == "winter":
		mac_m = 0.7
	elif state.macro_season == "boom":
		mac_m = 1.2
	return snappedf(clampf(base * era_m * mac_m, 10.0, 60.0), 1.0)

## THE GOVERNANCE RECORD, in points off (or onto) the next round's equity ask.
## A clean record IS lower perceived risk and a smaller ask; missed plans are a
## risk premium. `SimEngine.warmth_pct` adds this to trait warmth and clamps
## the sum to [0, 12] — the lane owns the governance half, the engine owns the
## reading. Zero while there is no board: nobody to have a record with.
static func warmth_delta(state: GameState) -> float:
	if state.board.is_empty():
		return 0.0
	return 2.0 * float(int(state.board.get("goodwill", 0))) \
		- 2.5 * float(int(state.board.get("strikes", 0)))

## Weeks until the next board review; −1 when there is no board. The journal's
## week-ahead line and the desk's countdown both read this.
static func board_review_in(state: GameState) -> int:
	if state.board.is_empty():
		return -1
	return maxi(int(state.board.get("review_week", 0)) - state.week, 0)

# ═══════════════ §3 ROUND CLOSE — seats, the pool shuffle, the covenant ═════
## SEAM (coordinator-planted): fires at the signature, both signing sites,
## immediately after `SimEngine.apply_round` has taken the investor's slice.
##
## THE POOL SHUFFLE, the standard term-sheet move: the option pool is written
## into the PRE-money, so its dilution lands on the founders' side and not on
## the investor's. apply_round has already multiplied the existing side by
## `inv_keep`; multiplying by `pool_keep` here lands on exactly the spec's
## `founder × pool_keep × inv_keep`, because multiplication commutes and the
## pool is never granted retroactively. The pool ITSELF is created pre-money
## and then diluted with everyone else — that is the (old × keep + new) × keep
## below, and it is the whole lesson: the slice comes out of you.
static func on_round_closed(state: GameState, amount: int, pct: float) -> void:
	var pool := clampf(pool_ask_pct(state), 0.0, 15.0)
	var pool_keep := 1.0 - pool / 100.0
	var inv_keep := 1.0 - clampf(pct, 0.0, 100.0) / 100.0
	if pool > 0.0:
		state.founder_pct = maxf(state.founder_pct * pool_keep, 1.0)
		for cf in state.cofounders:
			var cfd: Dictionary = cf
			cfd["equity_diluted"] = float(cfd.get("equity_diluted",
				cfd.get("equity", 0.0))) * pool_keep
	state.option_pool_pct = clampf(
		(state.option_pool_pct * pool_keep + pool) * inv_keep, 0.0, 100.0)

	# SEATS. A first priced round buys a seat; from the floor up, a third round
	# buys a second one. Three is the ceiling — past that the founder is
	# outvoted on their own cap table and the game stops being a game.
	var stage := board_stage(state)
	if stage >= 1:
		var earned := 1 + (1 if state.rounds_raised.size() >= 3 and stage >= 3 else 0)
		state.board_seats_investor = clampi(
			maxi(state.board_seats_investor, earned), 0, 3)

	# THE PLAN OF RECORD. The bar is set from the week's actual revenue or the
	# era's floor, whichever is higher, so a pre-revenue raise still owes a
	# concrete number. A new round INHERITS the record — a fresh board does not
	# forgive the last one's strikes.
	var pnl: Dictionary = state.get_meta("pnl", {})
	var base_rev := maxi(int(pnl.get("revenue", 0)), int(ERA_REV_FLOOR.get(state.era, 40)))
	var target_pct := board_target_pct(state)
	state.board = {
		"target_growth_pct": target_pct,
		"base_revenue": base_rev,
		"target_revenue": int(float(base_rev) * (1.0 + target_pct / 100.0)),
		"review_week": state.week + REVIEW_CADENCE,
		"strikes": int(state.board.get("strikes", 0)),
		"goodwill": int(state.board.get("goodwill", 0)),
	}
	# THE FORMATION RECEIPT: the obligations taken with the check enter the
	# written record in the same breath as the check itself.
	if stage == 0:
		state.log_action("the angel shook on it: %d%%/quarter is the number you said out loud — talk again wk %d"
			% [int(target_pct), int(state.board.review_week)])
	else:
		state.log_action("a board now sits between you and the company: %d investor seat(s) · growth covenant %d%%/quarter · first review wk %d"
			% [state.board_seats_investor, int(target_pct), int(state.board.review_week)])
	if pool > 0.0:
		state.log_action("the pool shuffle: a %d%% option pool written PRE-money — the dilution came out of your side, not theirs"
			% int(pool))

# ═══════════════════════ §4 THE REVIEW (deterministic) ══════════════════════
## Revenue against a bar, no dice. Fires the week the review lands and re-arms
## itself for the next quarter whichever way it went — a board that stops
## measuring you is not a board.
static func _review(state: GameState, rep: Dictionary) -> void:
	if state.board.is_empty():
		return
	if state.week < int(state.board.get("review_week", 1 << 30)):
		return
	var b: Dictionary = state.board
	var stage := board_stage(state)
	var pnl: Dictionary = state.get_meta("pnl", {})
	var measured := int(pnl.get("revenue", 0))
	var target := int(b.get("target_revenue", 0))

	# THE UPDATE YOU SENT. The adjudicator graded a written move weeks ago and
	# left a flag; the ENGINE converts it to goodwill here, so the LLM never
	# touched a board number.
	if state.has_flag("investor_update_sent"):
		b["goodwill"] = mini(int(b.get("goodwill", 0)) + 1, GOODWILL_CAP)
		state.flags.erase("investor_update_sent")
		rep["lines"].append("the update you sent bought patience — the room read it (+goodwill)")

	if measured >= target:
		b["strikes"] = maxi(int(b.get("strikes", 0)) - 1, 0)
		b["goodwill"] = mini(int(b.get("goodwill", 0)) + 1, GOODWILL_CAP)
		SimEngine.add_status(state, "board_delight", 4)
		if stage == 0:
			rep["lines"].append("the angel checked in — the numbers spoke for you: $%d/wk against the $%d you talked about. A quarter like that is cheap capital later (board_delight, 4 wks)"
				% [measured, target])
		else:
			rep["lines"].append("BOARD REVIEW — COVENANT MET: $%d/wk against the $%d bar. A clean quarter is cheap capital later (board_delight, 4 wks)"
				% [measured, target])
	elif stage == 0:
		# No board exists yet. An angel has expectations, not covenants: the
		# week is awkward and nothing goes on a record that does not exist.
		SimEngine.add_status(state, "investor_pressure", 3)
		rep["lines"].append("the angel checked in — $%d/wk against the $%d you talked about. Awkward calls all week (investor_pressure, 3 wks)"
			% [measured, target])
	else:
		var before := int(b.get("strikes", 0))
		var after := mini(before + 1, strike_cap(state))
		b["strikes"] = after
		SimEngine.add_status(state, "investor_pressure", 4)
		rep["lines"].append("BOARD REVIEW — COVENANT MISSED: $%d/wk against the $%d bar. Strike %d (investor_pressure, 4 wks)"
			% [measured, target, after])
		if stage >= 3:
			state.hype = clampi(state.hype - 2, 0, 100)   # board leaks travel
		# THE LADDER, on the rung it just reached — never re-fired for standing
		# still on it. Boards hire the CEO a coach before anything harsher.
		if before < 2 and after >= 2:
			var payroll := 0
			for e in state.employees:
				payroll += int((e as Dictionary).get("salary", 0))
			for h in state.pipeline:
				payroll += int((h as Dictionary).get("salary", 0))
			var coach_wk := clampi(int(float(payroll) * 0.05), COACH_MIN, COACH_MAX)
			state.commitments.append({"name": "the executive coach the board sent",
				"cash_wk": -coach_wk, "weeks_left": COACH_WEEKS})
			rep["events"].append("STRIKE TWO — the board sent a CEO coach: $%d/wk for six weeks. This is what boards do before they do worse"
				% coach_wk)
			if stage >= 4:
				state.hype = clampi(state.hype - 5, 0, 100)   # scrutiny is public now
		if before < 3 and after >= 3 and stage >= 2:
			var th: Dictionary = state.theta
			th["funding_mult"] = maxf(float(th.get("funding_mult", 1.0)) * 0.8, FUNDING_MULT_FLOOR)
			state.set_flag("down_round_threat")
			rep["events"].append("STRIKE THREE — the board reprices you: every future round now values the company 20% lower. That is a down round waiting to happen")

	# RE-ARM, both ways. The next quarter's bar is set from what you actually
	# did, with the era, the round count and the season all re-read live.
	b["base_revenue"] = maxi(measured, int(ERA_REV_FLOOR.get(state.era, 40)))
	b["target_growth_pct"] = board_target_pct(state)
	b["target_revenue"] = int(float(b["base_revenue"]) * (1.0 + float(b["target_growth_pct"]) / 100.0))
	b["review_week"] = state.week + REVIEW_CADENCE
	state.board = b

# ═════════════════════ §6 M&A — lapse, then the courtship ═══════════════════
static func _mna(state: GameState, rep: Dictionary) -> void:
	# LAPSE FIRST. LOIs die by lapse, and a leaked number destabilizes a team
	# while a public suitor validates the market — both, in the same week.
	if not state.mna.is_empty() and state.week > int(state.mna.get("expires_week", 0)):
		var lifeline := String(state.mna.get("why", "")) == "lifeline"
		var mor := 5 if lifeline else 3
		state.morale = clampi(state.morale - mor, 0, 100)
		state.hype = clampi(state.hype + 2, 0, 100)
		state.mna = {}
		state.mna_last_week = state.week
		_armed_key = ""
		_clear_sniff(state)
		rep["lines"].append("the no-shop lapsed — the offer is off the table. The team heard the number (−%d morale); so did the street (+2 hype)"
			% mor)
		return                       # one M&A beat a week, always

	if not state.mna.is_empty():
		return                       # a live offer blocks every other approach
	# INTEREST GOES COLD. A rival that asked about you and never wrote a sheet
	# stops asking; the street's flag comes down with it.
	_lapse_sniff(state)
	if state.week < MNA_FIRST_WEEK or state.week < state.mna_last_week + MNA_COOLDOWN:
		return

	# THE TRIGGER LADDER — first hit wins, drawn in this order forever.
	var r := SimEngine.rng_for(state, SimEngine.SALT_MNA)
	var v := SimEngine.valuation(state)
	var strong := _strongest_rival(state)
	var sniffer := _sniffing_rival(state)
	var why := ""
	var prem := 0.0
	var buyer := ""

	# 1 · THE LIFELINE. Distressed acqui-hire economics: they are pricing the
	# team and the shutdown avoided, not the business. It is the floor, so it
	# never rolls — a dying company with something worth taking gets the call.
	if (state.weeks_in_red >= 2 or SimEngine.runway_weeks(state) <= 2) \
			and (state.traction >= 5 or state.product >= 30):
		why = "lifeline"
		prem = 0.3 + r.randf() * 0.2
		buyer = _buyer_or(strong, 55.0, "a quiet strategic")
	# 2 · THE RIVAL. A consolidator buying a competitor — sometimes lowballing
	# a wounded one (0.9×). A rival that already asked about your price (the
	# street's sniff) is mid-courtship, so it writes far more often.
	elif not sniffer.is_empty() and r.randf() < 0.45:
		why = "rival"
		prem = 0.9 + r.randf() * 0.4
		buyer = String(sniffer.get("name", "a rival"))
	elif float(strong.get("strength", 0.0)) >= 70.0 and r.randf() < 0.20:
		why = "rival"
		prem = 0.9 + r.randf() * 0.4
		buyer = String(strong.get("name", "a rival"))
	# 3 · THE BOOM. Frothy-market multiple expansion: the same company is worth
	# more this quarter because money is cheap, which is exactly the lesson.
	elif state.macro_season == "boom" and v >= 500_000 and r.randf() < 0.15:
		why = "boom"
		prem = 1.2 + r.randf() * 0.6
		buyer = "a strategic riding the market"
	else:
		# 4 · THE MILESTONE. Crossing $2M / $10M / $50M puts you on a list.
		# One shot per band: the flag is stamped when the approach happens.
		var band := ""
		for row in BANDS:
			if v >= int(row[0]) and not state.has_flag(String(row[1])):
				band = String(row[1])
				break
		if band != "" and r.randf() < 0.35:
			why = "milestone"
			prem = 1.0 + r.randf() * 0.5
			buyer = _buyer_or(strong, 55.0, "a strategic who has been watching")
			state.set_flag(band)
	if why == "":
		return

	state.mna = {"buyer": buyer, "why": why, "premium": snappedf(prem, 0.01),
		"price": maxi(int(float(v) * prem), MIN_PRICE),
		"expires_week": state.week + NO_SHOP_WEEKS}
	state.mna_last_week = state.week
	_armed_key = ""
	if why == "rival":
		_clear_sniff(state)          # the courtship became a sheet; consumed
	rep["events"].append("AN OFFER FOR THE COMPANY: %s puts $%s on the table — a %d%% %s on your $%s standalone value. %s The no-shop clock runs %d weeks"
		% [buyer, money(int(state.mna.price)), int(round(absf(prem - 1.0) * 100.0)),
			premium_label(why, prem), money(v), premium_why(why, prem), NO_SHOP_WEEKS])

## What the premium IS, in the term a banker would use. Kept to a NOUN so it
## drops into the receipt's sentence frame without breaking it.
static func premium_label(why: String, prem: float) -> String:
	if why == "lifeline":
		return "acqui-hire discount"
	if prem >= 1.0:
		return "strategic premium"
	return "consolidator's discount"

## And why that number is that number — its own sentence, because a receipt
## that names a mechanism has to say what the mechanism means in the same breath.
static func premium_why(why: String, prem: float) -> String:
	if why == "lifeline":
		return "They are pricing the team and the shutdown avoided, not the business."
	if prem >= 1.0:
		return "That is what control of your customers is worth to somebody else."
	return "A consolidator buys a wounded competitor cheap."

# ═════════════════════════ §6 THE IPO WINDOW (hq only) ══════════════════════
## Weather, not a decision. It opens on clean governance in a receptive market
## and shuts in winters — and the reason it shut is the lesson.
static func _ipo_window(state: GameState, rep: Dictionary) -> void:
	var open_now := state.era == "hq" and state.traction >= 100 \
		and state.rounds_raised.size() >= 2 \
		and int(state.board.get("strikes", 0)) == 0 \
		and state.macro_season != "winter"
	if open_now and not state.has_flag("ipo_window"):
		state.set_flag("ipo_window")
		rep["events"].append("THE IPO WINDOW IS OPEN — clean covenants, a hundred believers, and a market that's buying. The bell is a journal card while it lasts")
	elif not open_now and state.has_flag("ipo_window"):
		state.flags.erase("ipo_window")
		_armed_key = ""
		var reason := "the numbers slipped"
		if state.macro_season == "winter":
			reason = "winter came"
		elif int(state.board.get("strikes", 0)) > 0:
			reason = "the board's strikes"
		rep["lines"].append("the IPO window closed — %s" % reason)

## What the bell would price the company at. Computed at the signature, never
## stored: an IPO pop is a market condition on the day, not a saved number.
static func ipo_price(state: GameState) -> int:
	return int(float(SimEngine.valuation(state)) * (1.35 if state.macro_season == "boom" else 1.1))

# ══════════════════ §7 THE JOURNAL SEAMS — draw, then sign ══════════════════
## SEAM (coordinator-planted): a journal offer block mirroring the term-sheet
## idiom. Empty dict = no card. The journal draws the title and the cards and
## routes every tap back through journal_pick; consequences live HERE.
##
## One block, in priority order — an exit clock outranks a bell, and a bell
## outranks taking money off the table.
static func journal_offer(state: GameState) -> Dictionary:
	if state.exit_value > 0 or state.dead:
		return {}
	if not state.mna.is_empty():
		var mo: Dictionary = state.mna
		var price := int(mo.get("price", 0))
		var slice := int(float(price) * state.founder_pct / 100.0)
		var left := maxi(int(mo.get("expires_week", 0)) - state.week, 0)
		var armed := _armed_key == _arm_key(state, "mna:accept:0")
		return {
			"title": "SOMEONE WANTS TO BUY THE COMPANY: $%s all-in · your %d%% = $%s · the no-shop ends in %d wk — or write anything else and let it lapse. Selling ends the run, so the card takes two taps." % [
				money(price), int(state.founder_pct), money(slice), left],
			"cards": [{"id": "mna:accept:0", "text": ("SELL — tap again" if armed
				else "%s  %s" % [String(mo.get("buyer", "a buyer")).split(" ")[0].left(9),
					money_short(price)])}],
		}
	if state.has_flag("ipo_window"):
		var bell := ipo_price(state)
		var bslice := int(float(bell) * state.founder_pct / 100.0)
		var barmed := _armed_key == _arm_key(state, "ipo:accept:0")
		return {
			"title": "THE BELL IS THERE TO RING: an IPO prices the company at $%s — your %d%% = $%s. Windows close. Ringing it ends the run, so the card takes two taps." % [
				money(bell), int(state.founder_pct), money(bslice)],
			"cards": [{"id": "ipo:accept:0", "text": ("RING IT — tap again" if barmed
				else "RING THE BELL  %s" % money_short(bell))}],
		}
	var bank := secondary_bank(state)
	if bank > 0:
		return {
			"title": "THE BOARD WILL LET YOU TAKE SOME OFF THE TABLE: sell 5 points of YOUR OWN stake at a 15%% discount to the round price — $%s banked, yours whatever happens to the company." % money(bank),
			"cards": [{"id": "sec:0", "text": "secondary %s" % money_short(bank)}],
		}
	return {}

## The signature. Returns the receipt the journal logs; "" is ignored entirely,
## which is what makes the first tap of a two-tap arm silent and harmless.
static func journal_pick(state: GameState, id: String) -> String:
	if state.exit_value > 0 or state.dead:
		return ""
	match id:
		"mna:accept:0":
			if state.mna.is_empty():
				return ""
			var key := _arm_key(state, id)
			if _armed_key != key:
				_armed_key = key            # tap one arms; the caption re-reads
				return ""
			var mo: Dictionary = state.mna
			state.exit_value = int(mo.get("price", 0))
			state.set_flag("acquired_exit")
			# A fire sale keeps its style multipliers (DECISIONS.md) — only the
			# name of the chip changes, because "SOLD AT THE TOP" is a lie at
			# 0.4x standalone. The finale reads this flag; the lane sets it.
			if String(mo.get("why", "")) == "lifeline":
				state.set_flag("soft_landing")
			state.mna = {}
			_armed_key = ""
			return "SOLD to %s for $%s (%s) — your %d%% pays $%s" % [
				String(mo.get("buyer", "a buyer")), money(state.exit_value),
				String(mo.get("why", "")), int(state.founder_pct),
				money(int(float(state.exit_value) * state.founder_pct / 100.0))]
		"ipo:accept:0":
			if not state.has_flag("ipo_window"):
				return ""
			var ikey := _arm_key(state, id)
			if _armed_key != ikey:
				_armed_key = ikey
				return ""
			state.exit_value = ipo_price(state)
			state.flags.erase("ipo_window")
			_armed_key = ""
			return "FILED. Priced at $%s — your %d%% pays $%s" % [
				money(state.exit_value), int(state.founder_pct),
				money(int(float(state.exit_value) * state.founder_pct / 100.0))]
		"sec:0":
			# NOT armed: a secondary is expensive, not irreversible, and the
			# price is printed on the card before the tap (§2.9's own test).
			var bank := secondary_bank(state)
			if bank <= 0:
				return ""
			state.founder_pct = maxf(state.founder_pct - 5.0, 1.0)
			state.founder_banked += bank
			state.set_flag(secondary_flag(state))
			return "SECONDARY: sold 5 points of YOUR OWN stake at a 15%% discount to the round price — $%s banked, yours whatever happens to the company" % money(bank)
	return ""

## What a secondary would bank right now, 0 when the door is shut. Five points
## of the company at a 15% discount, because secondaries price below the
## primary; the goodwill gate is board consent, which only trusted founders
## get, and one per round because the board signs a share purchase, not a tap.
static func secondary_bank(state: GameState) -> int:
	if board_stage(state) < 3 or state.board.is_empty():
		return 0
	if int(state.board.get("goodwill", 0)) < 2:
		return 0
	if state.has_flag(secondary_flag(state)):
		return 0
	if state.founder_pct <= 6.0:
		return 0
	# ONLY WHILE THE PAPERS ARE OUT. A board consents to a founder sale at the
	# table — the weeks right after a round closes or a review lands — not on a
	# random Tuesday. `review_week` is stamped week + 12 at both, so this is the
	# window after either, and it keeps the card from squatting on the page.
	if int(state.board.get("review_week", 0)) - state.week < REVIEW_CADENCE - SECONDARY_WINDOW:
		return 0
	return maxi(int(float(SimEngine.valuation(state)) * 0.05 * 0.85), 0)

## One secondary per round closed — the board signs a share purchase, not a tap.
## A flag, not a board field, so the two engines carry it identically.
static func secondary_flag(state: GameState) -> String:
	return "secondary_r%d" % state.rounds_raised.size()

# ═══════════════════ DIRECTIVES — what the DM is told, and told not to ══════
## Sections 12 (board) and 14 (M&A) of the DIRECTIVES block. The DM gives the
## boardroom a face and the courtship a dinner; it never decides an outcome,
## and the lines say so out loud where the temptation is strongest.
static func directives(state: GameState) -> Array[String]:
	var out: Array[String] = []
	if not state.board.is_empty():
		var due := board_review_in(state)
		var target := int(state.board.get("target_revenue", 0))
		var now_rev := int((state.get_meta("pnl", {}) as Dictionary).get("revenue", 0))
		if due <= 0:
			out.append("- BOARD REVIEW THIS WEEK: the covenant is $%d/wk revenue; the company sits at $%d/wk. The boardroom is part of this week's story." % [target, now_rev])
		elif due == 1:
			out.append("- The board reviews NEXT week: covenant $%d/wk, now $%d/wk. The founder can feel it." % [target, now_rev])
		if int(state.board.get("strikes", 0)) >= 2:
			out.append("- The board is one missed review from repricing the company. The coach's sessions are on the calendar.")
	if not state.mna.is_empty():
		out.append("- AN ACQUISITION OFFER IS ON THE TABLE: %s at $%s, no-shop ends week %d. Weave the courtship; only the journal card signs — never close or kill the deal yourself." % [
			String(state.mna.get("buyer", "a buyer")), money(int(state.mna.get("price", 0))),
			int(state.mna.get("expires_week", 0))])
	if state.has_flag("ipo_window"):
		out.append("- THE IPO WINDOW IS OPEN. Bankers circle; the bell is the founder's to ring in the journal, never yours.")
	return out

## ENGINE SIGNALS (docs/design/08-board-mna.md §9) — the two facts the DM needs
## stated flatly, so its narration can never contradict the ledger. Empty string
## when there is nothing to say: an absent key is how the composer stays quiet.
static func signal_line(state: GameState) -> String:
	if state.board.is_empty():
		return "no board — nobody to answer to"
	var pnl: Dictionary = state.get_meta("pnl", {})
	return "review wk%d (in %d): covenant $%d/wk · now $%d/wk · strikes %d · goodwill %d" % [
		int(state.board.get("review_week", 0)), board_review_in(state),
		int(state.board.get("target_revenue", 0)), int(pnl.get("revenue", 0)),
		int(state.board.get("strikes", 0)), int(state.board.get("goodwill", 0))]

static func mna_line(state: GameState) -> String:
	if state.mna.is_empty():
		return ""
	return "offer on the table: %s at $%d — no-shop ends wk%d" % [
		String(state.mna.get("buyer", "")), int(state.mna.get("price", 0)),
		int(state.mna.get("expires_week", 0))]

## ATTENTION ROWS (docs/design/00-spine.md §4) — the single list behind every
## bang, the garage ticker and the pre-roll review. Every row here is a
## time-boxed cap-table decision: a clock the founder must not roll past.
## Labels are ≤40 characters because the ticker prints them verbatim.
static func attention(state: GameState) -> Array:
	var rows: Array = []
	if not state.mna.is_empty():
		rows.append({"desk": "cap table", "key": "mna_offer", "severity": 3,
			"label": ("offer on the table — no-shop wk %d"
				% int(state.mna.get("expires_week", 0))).left(40)})
	if state.has_flag("ipo_window") and state.mna.is_empty():
		rows.append({"desk": "cap table", "key": "ipo_window", "severity": 2,
			"label": "the IPO window is open — windows close"})
	if not state.board.is_empty():
		var due := board_review_in(state)
		if due <= 1:
			# The ticker prints this verbatim in 40 characters, so the bar is
			# written short: a comma train would cost the deadline its words.
			rows.append({"desk": "cap table", "key": "board_review", "severity": 2,
				"label": ("board review %s — bar %s/wk" % [
					"this week" if due <= 0 else "next week",
					money_short(int(state.board.get("target_revenue", 0)))]).left(40)})
		if int(state.board.get("strikes", 0)) >= 2:
			rows.append({"desk": "cap table", "key": "board_strikes", "severity": 3,
				"label": ("strike %d — a reprice is the next rung"
					% int(state.board.get("strikes", 0))).left(40)})
	if secondary_bank(state) > 0:
		rows.append({"desk": "cap table", "key": "secondary", "severity": 1,
			"label": "the board will let you take some off"})
	return rows

# ─────────────────────────────── small hands ─────────────────────────────────
## The lane's money hand, so a receipt, a card and the desk read the same
## number the same way.
static func money(n: int) -> String:
	var s := str(absi(n))
	var out := ""
	while s.length() > 3:
		out = "," + s.substr(s.length() - 3) + out
		s = s.substr(0, s.length() - 3)
	return ("-" if n < 0 else "") + s + out

## A card caption has room for four characters and a unit, never a comma train.
static func money_short(n: int) -> String:
	var v := absf(float(n))
	var sign_s := "-" if n < 0 else ""
	if v >= 1_000_000_000.0:
		return "%s$%.1fB" % [sign_s, v / 1_000_000_000.0]
	if v >= 1_000_000.0:
		return "%s$%.1fM" % [sign_s, v / 1_000_000.0]
	if v >= 1_000.0:
		return "%s$%.0fk" % [sign_s, v / 1_000.0]
	return "%s$%d" % [sign_s, int(v)]

## THIS run, THIS week, THIS card. The instance id (not the seed) is what makes
## a replayed seed unable to inherit an arm: a new run is a new object.
static func _arm_key(state: GameState, id: String) -> String:
	return "%d:%d:%s" % [state.get_instance_id(), state.week, id]

static func _strongest_rival(state: GameState) -> Dictionary:
	var best: Dictionary = {}
	for rv in state.rivals:
		var rd: Dictionary = rv
		if best.is_empty() or float(rd.get("strength", 0.0)) > float(best.get("strength", 0.0)):
			best = rd
	return best

## The rival the street says is asking about your price (03 §5.7's handoff).
static func _sniffing_rival(state: GameState) -> Dictionary:
	if not state.has_flag("acquisition_sniff"):
		return {}
	for rv in state.rivals:
		var rd: Dictionary = rv
		if int(rd.get("sniffing", 0)) > 0:
			return rd
	return {}

static func _clear_sniff(state: GameState) -> void:
	for rv in state.rivals:
		(rv as Dictionary)["sniffing"] = 0
	state.flags.erase("acquisition_sniff")

static func _lapse_sniff(state: GameState) -> void:
	if not state.has_flag("acquisition_sniff"):
		return
	var live := false
	for rv in state.rivals:
		var rd: Dictionary = rv
		var wk := int(rd.get("sniffing", 0))
		if wk <= 0:
			continue
		if state.week - wk >= SNIFF_LAPSE:
			rd["sniffing"] = 0
		else:
			live = true
	if not live:
		state.flags.erase("acquisition_sniff")

static func _buyer_or(rival: Dictionary, floor_strength: float, fallback: String) -> String:
	if not rival.is_empty() and float(rival.get("strength", 0.0)) >= floor_strength:
		return String(rival.get("name", fallback))
	return fallback
