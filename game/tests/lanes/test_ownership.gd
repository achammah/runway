extends RefCounted
## LANE SUITE — the ownership cluster (DAG2 W2 L-OWN, live). Spec:
## docs/design/DECISIONS.md (THE OWNERSHIP CLUSTER, THE ESOP THREAD, THE
## OFFER) + docs/design/DAG2.md. These pins replace the W1 stub-neutrality
## pins: the lane is no longer neutral — the checks now hold its MATH.
##
## The porting law: a check lands HERE first, then in the same order in
## unity/Runway.Core.Tests/Lanes/OwnershipTests.cs. Same checks, same order,
## byte-identical messages. Stochastic paths run bounded per-seed loops —
## deterministic per engine, never a flake.

static func _state() -> GameState:
	var s := GameState.new()
	s.sim_seed = 4242
	s.week = 12
	s.cash = 60_000
	s.traction = 30
	s.product = 50
	s.morale = 70
	s.hype = 30
	s.biz_what = "Software"
	s.biz_who = "SMB"
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	return s

static func run(ok: Callable) -> void:
	# ── 1 · MIGRATION: the legacy pool seeds the esop, one-way, idempotent
	var s1 := _state()
	s1.option_pool_pct = 10.0
	SimEngine.weekly_tick(s1)
	ok.call(absf(float(s1.esop.get("pool_pct", 0.0)) - 10.0) < 0.001
		and (s1.esop.get("granted", [1]) as Array).is_empty(),
		"ownership: the legacy option_pool_pct seeds esop.pool_pct one-way")
	s1.week += 1
	SimEngine.weekly_tick(s1)
	ok.call(absf(float(s1.esop.get("pool_pct", 0.0)) - 10.0) < 0.001,
		"ownership: the pool migration is idempotent across ticks")

	# ── 2 · MIGRATION: the legacy labor market drains into recruitment
	var s2 := _state()
	s2.era = "coworking"
	s2.open_roles = [{"role": "sales", "offered_salary": 1200, "opened_week": 10, "seats": 1}]
	s2.applicants = [{"name": "Ade Okafor", "role": "sales", "skill": 4, "ask": 1400,
		"applied_week": 11, "source": "inbound"}]
	SimEngine.weekly_tick(s2)
	ok.call(s2.open_roles.is_empty() and s2.applicants.is_empty()
		and (s2.recruitment.get("roles", []) as Array).size() == 1
		and (s2.recruitment.get("candidates", []) as Array).size() >= 1,
		"ownership: open_roles and applicants migrate into recruitment as the source of truth")
	var mrole: Dictionary = (s2.recruitment.get("roles", []) as Array)[0]
	ok.call(int(mrole.get("band_lo", 0)) > 0 and int(mrole.get("band_hi", 0)) > int(mrole.get("band_lo", 0)),
		"ownership: a migrated seat carries the labor market's band")

	# ── 3 · VESTING: 208-wk linear, 52-wk cliff, computed never stored
	ok.call(SimOwnership.vest_frac(51) == 0.0 and absf(SimOwnership.vest_frac(52) - 0.25) < 0.001
		and absf(SimOwnership.vest_frac(104) - 0.5) < 0.001
		and SimOwnership.vest_frac(208) == 1.0 and SimOwnership.vest_frac(300) == 1.0,
		"ownership: the vest curve holds the cliff and the 208-wk line")
	var s3 := _state()
	s3.esop = {"pool_pct": 10.0, "granted": [{"emp_id": "june_park", "pct": 0.8, "vest_start_wk": 0}]}
	ok.call(absf(SimOwnership.vested_pct(s3, "june_park", 104) - 0.4) < 0.001,
		"ownership: vested_pct reads the grant halfway at wk 104")

	# ── 4 · THE LEAVER RULE: vested kept, unvested returns to the pool
	var s4 := _state()
	s4.week = 104
	s4.esop = {"pool_pct": 10.0, "granted": [{"emp_id": "june_park", "pct": 0.8, "vest_start_wk": 0}]}
	SimEngine.weekly_tick(s4)   # june is on no roster — the grant crystallizes
	var g4: Dictionary = (s4.esop["granted"] as Array)[0]
	ok.call(String(g4.get("emp_id", "")) == "left:june_park"
		and absf(float(g4.get("pct", 0.0)) - 0.4) < 0.01,
		"ownership: a leaver keeps vested and the unvested returns to the pool")
	ok.call(absf(SimOwnership.pool_free(s4) - 9.6) < 0.02
		and absf(SimOwnership.vested_pct(s4, "june_park", s4.week) - 0.4) < 0.01,
		"ownership: the freed pool space and the kept vested both read true")

	# ── 5 · CONVERSION: pct = amount / min(cap, pre × (1 − discount))
	var safe := {"kind": "safe", "amount": 150_000, "cap": 4_000_000, "discount": 0.2,
		"rate": 0.0, "signed_wk": 9, "pct": 0.0}
	ok.call(absf(SimOwnership.convert_pct_at(safe, 6_000_000.0, 12) - 3.75) < 0.001,
		"ownership: the cap side binds the conversion (150k at 4M = 3.75%)")
	var safe2 := {"kind": "safe", "amount": 150_000, "cap": 0, "discount": 0.2,
		"rate": 0.0, "signed_wk": 9, "pct": 0.0}
	ok.call(absf(SimOwnership.convert_pct_at(safe2, 6_000_000.0, 12) - 3.125) < 0.001,
		"ownership: the discount side binds when no cap does (4.8M basis = 3.125%)")
	var note := {"kind": "note", "amount": 150_000, "cap": 4_000_000, "discount": 0.0,
		"rate": 0.003, "signed_wk": 0, "pct": 0.0}
	ok.call(SimOwnership.amount_due(note, 52) == 173_400,
		"ownership: a note accrues simple interest into its due amount")

	# ── 6 · THE PRICED CLOSE: stack converts, seams run, the pie stays 100
	var s6 := _state()
	s6.era = "office"
	s6.instruments = [{"kind": "safe", "holder": "Fern Capital", "amount": 150_000,
		"cap": 4_000_000, "discount": 0.2, "rate": 0.0, "maturity_wk": 0, "pct": 0.0,
		"prefs": 0.0, "protective": false, "drag_threshold": 0.0, "signed_wk": 9}]
	s6.raise_state = {"stages": [{"name": "Halden Ventures", "stage": "terms",
		"arrived_wk": 10, "terms": {"kind": "priced", "valuation": 2_500_000,
		"amount": 500_000, "pct": 16.7, "prefs": 1.0, "protective": true,
		"drag_threshold": 60.0, "board_seat": true, "no_shop_wks": 4,
		"pool_topup_pct": 10.0, "expires_wk": 15}}],
		"interest_score": 50.0, "active": true, "founder_time_tax": 0.3}
	var cash_before := s6.cash
	var line6 := SimOwnership.op_sign_instrument(s6, "Halden")
	ok.call(line6 != "" and s6.cash == cash_before + 500_000,
		"ownership: a priced round wires its cash as one-shot event money")
	ok.call(s6.rounds_raised.size() == 1 and not s6.board.is_empty(),
		"ownership: the priced close rides apply_round and the board covenant seam")
	var conv: Dictionary = s6.instruments[0]
	ok.call(float(conv.get("pct", 0.0)) > 0.0,
		"ownership: the SAFE stack converts, whole, at the priced event")
	var total := s6.founder_pct + float(s6.esop.get("pool_pct", 0.0))
	for inst in s6.instruments:
		total += float((inst as Dictionary).get("pct", 0.0))
	ok.call(absf(total - 100.0) < 0.5,
		"ownership: after the close the slices still sum to the whole pie")
	ok.call(absf(state_pool(s6) - s6.option_pool_pct) < 0.001,
		"ownership: esop.pool_pct and the legacy mirror agree after the shuffle")
	ok.call(not bool(s6.raise_state.get("active", false))
		and SimOwnership.no_shop_until(s6) > s6.week,
		"ownership: the wire ends the raise and arms the no-shop freeze")

	# ── 7 · INTEREST: deterministic, and traction moves it
	var s7a := _state()
	var s7b := _state()
	ok.call(absf(SimOwnership.interest_score_calc(s7a) - SimOwnership.interest_score_calc(s7b)) < 0.0001,
		"ownership: the interest score is a deterministic read")
	s7b.traction = 500
	ok.call(SimOwnership.interest_score_calc(s7b) > SimOwnership.interest_score_calc(s7a),
		"ownership: traction raises investor interest")

	# ── 8 · THE KNOCK: a hot company gets inbound within the bounded window
	var s8 := _state()
	s8.traction = 3000
	s8.hype = 90
	s8.macro_season = "boom"
	s8.cash = 500_000
	s8.investors = [{"name": "Harborline Syndicate", "thesis": "momentum"}]
	var knocked := false
	for _i in 20:
		s8.week += 1
		SimEngine.weekly_tick(s8)
		if not (s8.raise_state.get("stages", []) as Array).is_empty():
			knocked = true
			break
	ok.call(knocked, "ownership: inbound knocks come to traction (bounded window)")

	# ── 9 · PITCH then TERMS: the data room decides, the tax is real
	var s9 := _state()
	s9.cash = 200_000
	s9.last_growth = 0.08
	s9.set_meta("pnl", {"revenue": 900, "net": 120})
	s9.investors = [{"name": "Bell & Weir", "thesis": "durable margin"}]
	var pline := SimOwnership.op_pitch_investor(s9, "Bell & Weir")
	ok.call(pline != "" and bool(s9.raise_state.get("active", false))
		and absf(float(s9.raise_state.get("founder_time_tax", 0.0)) - 0.3) < 0.001,
		"ownership: pitching opens the conversation and the founder-time tax bites")
	SimOwnership.tick_pre(s9, {"lines": [], "events": []})
	ok.call(SimEngine.has_status(s9, SimOwnership.RAISE_STATUS) == SimEngine.STATUS.has(SimOwnership.RAISE_STATUS),
		"ownership: the raise drag arms exactly when the status catalog knows it")
	var terms_seen := false
	for _j in 6:
		s9.week += 1
		s9.set_meta("pnl", {"revenue": 900, "net": 120})
		var rep9 := {"lines": [], "events": [], "fired_clocks": [], "expired": []}
		SimOwnership.tick_post(s9, rep9)
		if not stages_in(s9, "terms").is_empty():
			terms_seen = true
			break
	ok.call(terms_seen, "ownership: a healthy data room turns the conversation into terms")
	if terms_seen:
		var t9: Dictionary = (stages_in(s9, "terms")[0] as Dictionary).get("terms", {})
		ok.call(int(t9.get("amount", 0)) > 0 and int(t9.get("expires_wk", 0)) > s9.week,
			"ownership: drafted terms carry banded money and a shelf life")

	# ── 10 · THE ACCEPTANCE CURVE: cash climbs it; profiles bend it
	var s10 := _state()
	var merc := {"ask": 540, "profile": "mercenary"}
	var miss := {"ask": 540, "profile": "missionary"}
	ok.call(SimOwnership.acceptance_odds(s10, merc, 560, 0.0) > SimOwnership.acceptance_odds(s10, merc, 500, 0.0),
		"ownership: more cash raises acceptance odds")
	ok.call(SimOwnership.acceptance_odds(s10, miss, 520, 0.4) - SimOwnership.acceptance_odds(s10, miss, 520, 0.0)
		> SimOwnership.acceptance_odds(s10, merc, 520, 0.4) - SimOwnership.acceptance_odds(s10, merc, 520, 0.0),
		"ownership: options move a missionary more than a mercenary")
	ok.call(SimOwnership.acceptance_odds(s10, merc, 5000, 5.0) <= 95.0
		and SimOwnership.acceptance_odds(s10, merc, 10, 0.0) >= 5.0,
		"ownership: acceptance odds stay inside the 5–95 clamp")

	# ── 11 · SEND then SIGN: the hire rides the labor pipeline, the grant lands
	var s11 := _state()
	s11.era = "coworking"
	s11.esop = {"pool_pct": 10.0, "granted": []}
	s11.recruitment = {"roles": [{"id": "role_sales_10", "seat": "sales",
		"band_lo": 1060, "band_hi": 1560, "advert_wk": 0, "opened_wk": 10}],
		"candidates": [{"id": "cand_12_0", "role_id": "role_sales_10", "name": "Dana Kovic",
		"ask": 900, "profile": "mercenary", "skill": 4, "stage": "interviewed",
		"arrived_wk": 11}], "offers_out": []}
	var joined := false
	for _k in 6:
		var cand11 := SimOwnership.cand_by_id(s11, "cand_12_0")
		if String(cand11.get("stage", "")) == "joined":
			joined = true
			break
		if String(cand11.get("stage", "")) == "interviewed":
			SimOwnership.op_send_offer(s11, "cand_12_0", 1400, 0.4)
		s11.week += 1
		SimEngine.weekly_tick(s11)
	ok.call(joined, "ownership: a rich offer gets signed inside the bounded window")
	var hired := false
	for h in s11.pipeline:
		if String((h as Dictionary).get("name", "")) == "Dana Kovic":
			hired = true
	for e in s11.employees:
		if String((e as Dictionary).get("name", "")) == "Dana Kovic":
			hired = true
	ok.call(hired, "ownership: the signed candidate rides the existing labor hire path")
	ok.call(SimOwnership.granted_pct(s11, "dana_kovic") > 0.0,
		"ownership: the offer's options become a real grant at signing")
	ok.call((s11.recruitment.get("roles", []) as Array).is_empty(),
		"ownership: a filled seat closes its requisition")

	# ── 12 · THE POOL GATE: an empty pool blocks equity offers
	var s12 := _state()
	s12.esop = {"pool_pct": 0.5, "granted": [{"emp_id": "x_y", "pct": 0.5, "vest_start_wk": 12}]}
	s12.recruitment = {"roles": [], "candidates": [{"id": "c1", "role_id": "", "name": "Tom Beck",
		"ask": 900, "profile": "mercenary", "skill": 3, "stage": "interviewed",
		"arrived_wk": 11}], "offers_out": []}
	ok.call(SimOwnership.op_send_offer(s12, "c1", 1000, 1.0) == "",
		"ownership: an empty pool refuses the equity offer")
	ok.call(SimOwnership.op_send_offer(s12, "c1", 1000, 0.0) != "",
		"ownership: the same offer goes out cash-only")

	# ── 13 · THE ADVERT LANE: recruit_ads bills and the identity holds
	var s13 := _state()
	s13.recruitment = {"roles": [{"id": "r1", "seat": "sales", "band_lo": 1000,
		"band_hi": 1500, "advert_wk": 60, "opened_wk": 12},
		{"id": "r2", "seat": "engineer", "band_lo": 1200, "band_hi": 1800,
		"advert_wk": 40, "opened_wk": 12}], "candidates": [], "offers_out": []}
	SimEngine.weekly_tick(s13)
	var pnl13: Dictionary = s13.get_meta("pnl", {})
	ok.call(int(pnl13.get("recruit_ads", -1)) == 100,
		"ownership: the advert lane bills exactly the seats' spend")
	ok.call(int(pnl13.get("net", 0)) == int(pnl13.get("revenue", 0)) - int(pnl13.get("burn", 0))
		- int(pnl13.get("liabilities_wk", 0)) - int(pnl13.get("interest", 0)) - int(pnl13.get("tax", 0)),
		"ownership: the P&L identity holds with the advert lane live")

	# ── 14 · ESOP IS NON-CASH: paper moves no money through the tick
	var s14a := _state()
	var s14b := _state()
	s14b.esop = {"pool_pct": 10.0, "granted": [{"emp_id": "someone_here", "pct": 2.0,
		"vest_start_wk": 12}]}
	s14b.employees = []
	SimEngine.weekly_tick(s14a)
	SimEngine.weekly_tick(s14b)
	ok.call(s14a.cash == s14b.cash,
		"ownership: the pool and its grants never enter the cash identity")

	# ── 15 · THE DRESSING: a board offer gains structure the same week
	var s15 := _state()
	s15.week = 30
	s15.mna = {"buyer": "Larkspur Depot", "why": "milestone", "premium": 1.2,
		"price": 2_400_000, "expires_week": 32}
	SimOwnership.tick_post(s15, {"lines": [], "events": []})
	ok.call(not s15.buyout_offer.is_empty()
		and String(s15.buyout_offer.get("buyer", "")) == "Larkspur Depot",
		"ownership: the board's offer gets dressed into the structured folder")
	ok.call(int(s15.buyout_offer.get("cash", 0)) + int(s15.buyout_offer.get("stock", 0))
		+ int(s15.buyout_offer.get("earnout", 0)) == 2_400_000,
		"ownership: cash + stock + earnout compose exactly the headline")
	ok.call(int(s15.buyout_offer.get("expires_wk", 0)) == 32
		and s15.buyout_offer.has("fishy_flags"),
		"ownership: the folder carries the board's clock and its computed flags")

	# ── 16 · THE WATERFALL: debts, prefs-or-convert, the vested split
	var s16 := _state()
	s16.week = 220
	s16.founder_pct = 58.0
	s16.cofounders = [{"name": "Mara Voss", "role": 1, "equity": 17.0, "equity_diluted": 17.0}]
	s16.loan_principal = 7_350
	s16.esop = {"pool_pct": 10.0, "granted": [{"emp_id": "june_park", "pct": 0.4,
		"vest_start_wk": 0}]}
	s16.instruments = [{"kind": "priced", "holder": "Fern Capital", "amount": 400_000,
		"cap": 0, "discount": 0.0, "rate": 0.0, "maturity_wk": 0, "pct": 15.0,
		"prefs": 1.0, "protective": true, "drag_threshold": 60.0, "signed_wk": 31}]
	var wf := SimOwnership.waterfall(s16, 4_200_000)
	ok.call(int(wf.get("debts", 0)) == 7_350,
		"ownership: the bank is paid first, always")
	var fern_take := row_take(wf, "Fern Capital")
	ok.call(fern_take > 400_000,
		"ownership: a 15% stake converts when it beats the 1x preference — computed")
	var expected_you := int(round((4_200_000.0 - 7_350.0) * 58.0 / 90.4))
	ok.call(absi(int(wf.get("your_take", 0)) - expected_you) <= 2,
		"ownership: your take is the pro-rata of the post-debt pot")
	ok.call(int(wf.get("esop_take", 0)) > 0,
		"ownership: the vested ESOP holders get paid too")
	var wf_low := SimOwnership.waterfall(s16, 8_000)
	ok.call(int(wf_low.get("your_take", 0)) == 0,
		"ownership: below the breakeven the preferences eat everything")

	# ── 17 · THE POWERS: protective leans by its take; the drag row shows
	var pw := SimOwnership.powers(s16, 4_200_000)
	var fern_yes := false
	var drag_row := false
	for p in pw:
		var pd: Dictionary = p
		if String(pd.get("who", "")) == "Fern Capital" and not bool(pd.get("blocks", true)):
			fern_yes = true
		if String(pd.get("who", "")) == "drag-along":
			drag_row = true
	ok.call(fern_yes and drag_row,
		"ownership: the powers resolve from the instruments signed years early")

	# ── 18 · RESOLUTION: accept exits, negotiate once, decline cools
	var s18 := _state()
	s18.week = 30
	s18.mna = {"buyer": "Larkspur Depot", "why": "milestone", "premium": 1.2,
		"price": 2_400_000, "expires_week": 32}
	SimOwnership.tick_post(s18, {"lines": [], "events": []})
	var before_price := int(s18.buyout_offer.get("headline", 0))
	ok.call(SimOwnership.buyout_negotiate(s18) != "" and bool(s18.buyout_offer.get("countered", false))
		and int(s18.buyout_offer.get("headline", 0)) != 0
		and int(s18.mna.get("price", 0)) == int(s18.buyout_offer.get("headline", 0)),
		"ownership: one counter reprices the world once, mirrored to the courtship")
	ok.call(SimOwnership.buyout_negotiate(s18) == "",
		"ownership: the second counter finds no room")
	ok.call(before_price > 0, "ownership: the counter started from a real headline")
	var line18 := SimOwnership.buyout_accept(s18)
	ok.call(line18 != "" and s18.exit_value > 0 and s18.has_flag("acquired_exit")
		and s18.mna.is_empty() and s18.buyout_offer.is_empty(),
		"ownership: accepting runs the waterfall and ends the run through the exit seam")
	var s19 := _state()
	s19.week = 30
	s19.hype = 40
	s19.mna = {"buyer": "Vantiv Group", "why": "rival", "premium": 1.0,
		"price": 900_000, "expires_week": 32}
	SimOwnership.tick_post(s19, {"lines": [], "events": []})
	ok.call(SimOwnership.buyout_decline(s19) != "" and s19.hype == 42
		and s19.mna.is_empty() and s19.buyout_offer.is_empty() and s19.mna_last_week == 30,
		"ownership: declining clears the table, the street hears, the cooldown starts")

	# ── 19 · ATTENTION: the rows name their desks and fit the ticker
	var s20 := _state()
	s20.week = 80
	s20.instruments = [{"kind": "note", "holder": "R. Osei", "amount": 40_000,
		"cap": 500_000, "discount": 0.1, "rate": 0.003, "maturity_wk": 70, "pct": 0.0,
		"prefs": 0.0, "protective": false, "drag_threshold": 0.0, "signed_wk": 20}]
	s20.buyout_offer = {"buyer": "X", "headline": 100, "cash": 100, "stock": 0,
		"earnout": 0, "expires_wk": 81, "fishy_flags": []}
	s20.recruitment = {"roles": [{"id": "r", "seat": "sales", "band_lo": 1, "band_hi": 2,
		"advert_wk": 0, "opened_wk": 1}], "candidates": [{"id": "c9", "role_id": "r",
		"name": "Priya Nair", "ask": 900, "profile": "mercenary", "skill": 3,
		"stage": "offer", "arrived_wk": 79}],
		"offers_out": [{"candidate_id": "c9", "cash_wk": 900, "options_pct": 0.0,
		"expires_wk": 82, "sent_wk": 80}]}
	var rows20 := SimOwnership.attention(s20)
	var desks := {}
	var fits := true
	for r in rows20:
		desks[String((r as Dictionary).get("desk", ""))] = true
		if String((r as Dictionary).get("label", "")).length() > 40:
			fits = false
	ok.call(desks.has("the raise") and desks.has("the offer") and desks.has("recruitment"),
		"ownership: matured notes, live buyouts and offers-out all raise their hands")
	ok.call(fits, "ownership: every attention label fits the 40-char ticker")

	# ── 20 · REPLAY: a full ownership state ticks identically twice
	var r1 := _state()
	var r2 := _state()
	for s_r in [r1, r2]:
		var sv: GameState = s_r
		sv.esop = {"pool_pct": 10.0, "granted": []}
		sv.instruments = [{"kind": "safe", "holder": "Fern Capital", "amount": 150_000,
			"cap": 4_000_000, "discount": 0.2, "rate": 0.0, "maturity_wk": 0, "pct": 0.0,
			"prefs": 0.0, "protective": false, "drag_threshold": 0.0, "signed_wk": 9}]
		sv.recruitment = {"roles": [{"id": "r1", "seat": "sales", "band_lo": 1000,
			"band_hi": 1500, "advert_wk": 60, "opened_wk": 12}], "candidates": [],
			"offers_out": []}
		sv.raise_state = {"stages": [], "interest_score": 0.0, "active": true,
			"founder_time_tax": 0.3}
		SimEngine.weekly_tick(sv)
	ok.call(r1.cash == r2.cash and r1.traction == r2.traction
		and (r1.recruitment.get("candidates", []) as Array).size()
			== (r2.recruitment.get("candidates", []) as Array).size(),
		"ownership: the ownership cluster replays exactly on one seed")

# ── the suite's own small hands ──────────────────────────────────────────────

static func state_pool(s: GameState) -> float:
	return float(s.esop.get("pool_pct", 0.0))

static func stages_in(s: GameState, stage: String) -> Array:
	var out: Array = []
	for st in s.raise_state.get("stages", []):
		if String((st as Dictionary).get("stage", "")) == stage:
			out.append(st)
	return out

static func row_take(wf: Dictionary, holder: String) -> int:
	for r in wf.get("rows", []):
		if String((r as Dictionary).get("holder", "")) == holder:
			return int((r as Dictionary).get("take", 0))
	return 0
