class_name DeskCustomers
extends RefCounted
## DESK — the binder's `customers` tab. Spec: docs/design/04-funnel-channels.md §6.2
##
## `binder.gd` dispatches the tab body here and passes ITSELF, so this file draws
## through the binder's own helpers and never reaches into the sheet directly.
##
## THE FUNNEL IS THE PAGE, AND ANALYTICS IS THE LIGHT. Every read below is the
## engine's own intermediate value — reach, leads, signed, conversion, the
## closing ceiling, per-channel CAC — revealed at the level the founder can
## actually see: `an = min(analytics_level, era cap)`. A garage has no data stack
## to buy, so full attribution is an office-era capability, and the fog at an=0
## is the shipped page, unchanged, on purpose.
##
## The bar every surface ships at (docs/design/00-spine.md §11): readable first
## pass by a tired player; concepts named in real business terms with a teaching
## line where a number first appears; no dead ends and every state leavable;
## drawn in the game's hand, never a SaaS panel. The shared components live in
## game/src/ui/components.gd (DeskKit) — use them, never fork them.

## THE FUNNEL BLOCK's own vertical map. The bars are DeskKit's (label gutter,
## 40+460×v/max bar, the number ON the row), so the CAC read sits BELOW them in
## a four-cell strip rather than beside them — the kit's bar row is a full-pane
## row and a column at x740 would be written straight through by the widest one.
const Y_FUNNEL := 316.0
const Y_BARS := 348.0
const Y_CAC := 480.0
const Y_CAC_ROW := 508.0
const Y_MARKET := 544.0
const Y_ASSUME := 578.0
const Y_COHORT := 610.0
const Y_LIFETIME := 644.0
const Y_TRUTH := 684.0
const CAC_CELL := 280.0

## Draw the funnel reads at their analytics gates (non-Enterprise branch). `b` is
## the Binder itself (untyped to keep the two files free of a cyclic class
## dependency).
static func draw(b) -> void:
	var state: GameState = b.state
	# THE BRANCH IS THE PLANTED SEAM: an Enterprise run's customers page is the
	# pipeline's stage board, drawn by its own lane, and NEITHER LANE EDITS THE
	# OTHER'S FILE. The pipeline says when it owns the page (`owns_page`) — until
	# it does, this branch never fires and the funnel page below is what every
	# run gets, exactly as it does today.
	if DeskPipeline.owns_page(b):
		draw_enterprise(b)
		return
	b.icon("customers", Vector2(10, 6))
	# AN = 0: the fog, word for word as it shipped. Nothing about the funnel
	# exists for a founder who never bought the means to see it.
	if state.analytics_level <= 0:
		b.label("%d customers, give or take." % state.traction, Vector2(100, 10), 46)
		b.label("Traffic seems… hard to read. The numbers live in a notebook you lost.",
			Vector2(10, 110), 30, Color(Binder.INK, 0.7))
		b.label("(invest in analytics to see the funnel)", Vector2(10, 210), 26, Binder.PEN)
		_footer(b, state)
		return
	var an := SimFunnel.analytics(state)
	b.label("%d customers" % state.traction, Vector2(100, 10), 46)
	b.label("customers, weekly:", Vector2(10, 100), 24, Color(Binder.INK, 0.6))
	# 30px reclaimed from the spark buys the funnel block below it
	b.spark(b.series("customers"), Vector2(10, 130), Vector2(1120, 170), Binder.SAGE)
	var th := state.theta
	if an >= 1:
		_funnel(b, state)
		_cac(b, state)
	else:
		# the level is bought but the ERA refuses it: say which, so the player
		# knows this is a stage gate and not a broken page
		b.label("the funnel is dark here: attribution needs an office, not a garage.",
			Vector2(10, Y_FUNNEL), 26, Color(Binder.INK, 0.6))
	# WORKING ASSUMPTIONS (owner: nobody knows their TAM on day one): the binder
	# shows what the founder BELIEVES; operating and analytics refine it
	var believed_tam := float(state.beliefs.get("tam", th.get("tam", 100000.0)))
	var pen_pct := float(state.traction) / maxf(believed_tam, 1.0) * 100.0
	var y_market := Y_MARKET if an >= 1 else 356.0
	b.label("market, as you believe it: ~%s buyers (%.1f%% reached) · a customer stays ≈ %d wks" % [
		b.fmt(int(believed_tam)), pen_pct,
		int(state.beliefs.get("lifetime_wk", th.get("lifetime_wk", 40)))],
		Vector2(10, y_market), 27)
	b.label("working assumptions — they sharpen as you learn", Vector2(10, y_market + 34.0), 22,
		Color(Binder.INK, 0.5))
	if an >= 2:
		_cohort(b, state)
	if an >= 3:
		_truth(b, state)
	_footer(b, state)

## THE DESK STATES ITS OWN LAWS (§2.7). Every other desk ends on its lesson and
## this one used to end on whatever the last analytics gate happened to unlock —
## a page whose bottom edge moved with a purchase. The rules line is the funnel's
## whole pedagogy in one breath; the warning outranks it when money is buying
## nobody, which is the one thing on this page a founder must not scroll past.
static func _footer(b, state: GameState) -> void:
	var f := SimFunnel.funnel(state)
	var warning := ""
	for k in SimFunnel.MIX:
		if SimFunnel.num(f, "cac_" + k) <= 0.0 \
				and SimFunnel.num(f, "spend_" + k) >= SimFunnel.BURN_SPEND:
			warning = "%s is BURNING: $%s/wk bought nobody last week — a channel with no CAC has no price" % [
				k.to_upper(), b.fmt(int(SimFunnel.num(f, "spend_" + k)))]
			break
	DeskKit.footer(b, {
		"rules": "the rules of this desk: REACH is what money bought · a LEAD is reach that answered · "
			+ "only closing capacity signs them · CAC is spend ÷ signed, per channel · churn is a leaky bucket, and care patches it",
		"warning": warning,
		"y": DeskKit.FOOTER_Y, "rules_y": DeskKit.RULES_Y,
	})

## THE FUNNEL, three pen strokes of shrinking length — the SHAPE is the lesson
## before any number lands. REACH is what money bought; LEADS is what converted;
## SIGNED is what the closing capacity actually let in.
static func _funnel(b, state: GameState) -> void:
	var f := SimFunnel.funnel(state)
	b.label("the funnel, last week:", Vector2(10, Y_FUNNEL), 24, Color(Binder.INK, 0.6))
	if f.is_empty():
		DeskKit.empty(b, Vector2(10, Y_BARS),
			"no week on the books yet — the funnel is measured, not predicted.",
			"lock in a week and this fills with reach, leads and signings.")
		return
	var reach := SimFunnel.num(f, "reach_total")
	var leads := SimFunnel.num(f, "leads_total")
	var signed := SimFunnel.num(f, "adds")
	var close_rate := SimFunnel.num(f, "close_rate")
	var signed_txt := "%d" % int(round(signed))
	if close_rate < 0.9:
		signed_txt += " · ceiling %d hit" % int(round(SimFunnel.num(f, "gtm_cap")))
	DeskKit.bars(b, Vector2(10, Y_BARS), [
		{"label": "reach", "value": reach, "col": Binder.BLUE,
			"text": "%s bought" % b.fmt(int(round(reach)))},
		{"label": "leads", "value": leads, "col": Binder.YELL,
			"text": "%d · conv %.1f%%" % [int(round(leads)), SimFunnel.num(f, "conv") * 100.0]},
		{"label": "signed", "value": signed, "col": Binder.SAGE, "text": signed_txt},
	], 46.0)

## PER-CHANNEL CAC is the teaching heart of this desk: which dollar buys a
## customer cheapest. One coral word does the alarm work.
static func _cac(b, state: GameState) -> void:
	var f := SimFunnel.funnel(state)
	if f.is_empty():
		return
	b.label("CAC by channel — what one customer cost, and which dollar bought them",
		Vector2(10, Y_CAC), 22, Color(Binder.INK, 0.6))
	var x := 10.0
	for k in SimFunnel.MIX:
		var spend := SimFunnel.num(f, "spend_" + k)
		var cac := SimFunnel.num(f, "cac_" + k)
		var txt := "%s —" % k
		var col := Color(Binder.INK, 0.5)
		if cac > 0.0:
			txt = "%s $%s · $%s/wk" % [k, b.fmt(int(round(cac))), b.fmt(int(spend))]
			col = Color(Binder.INK, 0.85)
		elif spend >= SimFunnel.BURN_SPEND:
			# money that bought nobody, said out loud, in the one alarm colour
			txt = "%s burning · $%s/wk" % [k, b.fmt(int(spend))]
			col = Binder.PEN
		elif spend > 0.0:
			txt = "%s — · $%s/wk" % [k, b.fmt(int(spend))]
		b.label(txt, Vector2(x, Y_CAC_ROW), 24, col, CAC_CELL - 10.0)
		x += CAC_CELL

## RETENTION, by its real name. The second analytics level buys the cohort read:
## what a hundred customers from twelve weeks ago look like today.
static func _cohort(b, state: GameState) -> void:
	var th := state.theta
	var ue: Dictionary = state.get_meta("unit_econ", {})
	var residence := maxf(float(th.get("lifetime_wk", 40.0))
			* (0.4 + float(state.product) / 100.0 * 1.2), 2.0)
	var care_cut := 30.0 * (1.0 - exp(-float(state.budgets.get("care", 0)) / 1500.0))
	var churn_wk := 100.0 / residence * float(th.get("churn_mult", 1.0)) * (1.0 - care_cut / 100.0)
	var survive := pow(maxf(1.0 - 1.0 / residence, 0.0), 12.0) * 100.0
	b.label("of 100 who joined 12 wks ago, ~%d are still here · churn %.1f%%/wk · care trims %d%%" % [
		int(round(survive)), churn_wk, int(round(care_cut))], Vector2(10, Y_COHORT), 26)
	var cacv := int(ue.get("cac", 0))
	var pb := int(ue.get("payback_wk", 0))
	b.label("lifetime ≈ %d wks at v0.%d · blended CAC %s · payback %s" % [
		int(round(residence)), state.product,
		("$%s" % b.fmt(cacv)) if cacv > 0 else "—",
		("%d wks" % pb) if pb > 0 else "—"], Vector2(10, Y_LIFETIME), 24, Color(Binder.INK, 0.8))

## LEVEL 3 PAYS IN DATA, not a compliment: the real market against the believed
## one, the two rates that decide everything, and the channel to put money in.
static func _truth(b, state: GameState) -> void:
	var th := state.theta
	var f := SimFunnel.funnel(state)
	var best := ""
	var best_cac := 0.0
	for k in SimFunnel.MIX:
		var c := SimFunnel.num(f, "cac_" + k)
		if c > 0.0 and (best == "" or c < best_cac):
			best = k
			best_cac = c
	b.label("the truth: ~%s buyers (you believed %s) · conv %.1f%% · close %d%% · %s" % [
		b.fmt(int(float(th.get("tam", 100000.0)))),
		b.fmt(int(float(state.beliefs.get("tam", th.get("tam", 100000.0))))),
		SimFunnel.num(f, "conv") * 100.0,
		int(round(SimFunnel.num(f, "close_rate") * 100.0)),
		# ONE MEASURED LINE at 1100px: the desk's own law line sits 50px under this
		# one, and the long form of the tail wrapped straight into it.
		("cheapest customer: %s" % best.to_upper()) if best != ""
			else "no channel has bought a customer yet"],
		Vector2(10, Y_TRUTH), 26, Binder.SAGE, 1100.0)

## A press inside this desk. `id` is whatever the desk's own draw registered.
static func handle(_b, _id: String) -> void:
	pass

## THE ENTERPRISE BRANCH belongs to the pipeline lane, drawn inside this desk.
## The call site is planted so neither lane has to edit the other's file.
static func draw_enterprise(b) -> void:
	DeskPipeline.draw_board(b)
