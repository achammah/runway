class_name DeskPipeline
extends RefCounted
## DESK — the binder's `customers` tab, Enterprise branch.
## Spec: docs/design/05-enterprise-pipeline.md §12
##
## `DeskCustomers` dispatches the Enterprise page here and passes the Binder
## ITSELF, so this file draws through the binder's own helpers and never reaches
## into the sheet directly.
##
## WHAT THIS PAGE IS: the founder's wall calendar. Named accounts sitting in
## named gates, each with its size, its warmth and its age; the logos already
## signed; and one blue line at the bottom that names win rate, sales cycle and
## cost per signed seat using the run's OWN numbers. There are no controls at
## all — a deal is pushed by a written move (`push_lead`), never by a button —
## and that absence is the lesson: enterprise sales is attention, not clicking.
##
## FOG OF WAR DOES NOT APPLY HERE. The board renders at analytics 0, because the
## pipeline is the founder's own calendar: fog hides what the MARKET is doing,
## never what is on your own desk. The believed-market lines keep their gates on
## the funnel branch next door.
##
## The bar every surface ships at (docs/design/00-spine.md §11): readable first
## pass by a tired player; concepts named in real business terms with a teaching
## line where a number first appears; no dead ends and every state leavable;
## drawn in the game's hand, never a SaaS panel. The shared components live in
## game/src/ui/components.gd (DeskKit) — the stage board is DeskKit.board().

## Does the pipeline own the customers page on this run? THE HANDOVER: the board
## is real now, so an Enterprise run gets it and every other run keeps today's
## funnel page, untouched. Nobody had to edit desk_customers.gd for this.
static func owns_page(b) -> bool:
	return String(b.state.biz_who) == "Enterprise"

## Draw the stage board, lead chips, signed-logos strip and teaching footer. `b`
## is the Binder itself (untyped to keep the two files free of a cyclic class
## dependency).
static func draw(b) -> void:
	draw_board(b)

## A press inside this desk. There are none: the board carries no controls by
## design (spec §12) — the pipeline moves on written moves and on the dice.
static func handle(_b, _id: String) -> void:
	pass

## Drawn INSIDE the customers desk on Enterprise runs (DeskCustomers calls this).
static func draw_board(b) -> void:
	var state: GameState = b.state
	b.icon("customers", Vector2(10, 6))
	b.label("%d customers · %d logos signed" % [state.traction, state.logos.size()],
		Vector2(100, 6), DeskKit.HERO)
	b.label("the pipeline — %d live · %d seats in motion · pool %.0f waiting" % [
		state.leads.size(), SimPipeline.seats_in_motion(state), state.pipe_units],
		Vector2(100, 64), 24, Color(DeskKit.INK, 0.6), 1020.0)

	# THE STAGE BOARD — the mental model being taught. The columns ARE the lesson:
	# a deal is not "in the funnel", it is sitting at a named gate waiting to
	# clear it. PROCUREMENT appears from the office era, exactly when deal size
	# makes a buyer's IT department wake up (spec §8).
	var cols := _columns(state)
	var hidden := 0
	for c in cols:
		hidden += int((c as Dictionary).get("hidden", 0))
	var board_end := DeskKit.board(b, 96.0, cols,
		"no deals on the board yet — marketing books the meetings, and %.0f seats of interest are already waiting in the pool" % state.pipe_units)
	if hidden > 0:
		DeskKit.more(b, Vector2(DeskKit.X_ID, board_end), hidden, "sit deeper in those columns")

	# THE SIGNED LOGOS. Closed business stays visible as named accounts — and at
	# floor/hq a renewal announces itself before it arrives.
	DeskKit.rule(b, 596.0)
	b.label(_logo_strip(state), Vector2(DeskKit.X_ID, 610.0), 22,
		Color(DeskKit.INK, 0.75), 1100.0)

	# THE TEACHING FOOTER — win rate, sales cycle, CAC-per-seat against what a
	# seat actually pays, in the run's own numbers, with the era's coach line
	# under it saying what this stage of the pipeline can and cannot do.
	DeskKit.footer(b, {
		"computed": _footer_line(state),
		"rules": String(SimPipeline.COACH.get(state.era, "")),
	})

# ── the board ───────────────────────────────────────────────────────────────
## One column per live stage, each holding its deals hottest-first. Four chips
## fit a column at the kit's pitch; the rest are counted honestly underneath.
static func _columns(state: GameState) -> Array:
	var stages: Array = ["meeting", "pilot", "contract"]
	if state.era_index() >= 2:
		stages = ["meeting", "pilot", "procurement", "contract"]
	var decay := SimPipeline.decay_for(state)
	var order := SimPipeline.leads_by_heat(state)
	var cols: Array = []
	for stage in stages:
		var chips: Array = []
		var hidden := 0
		for i in order:
			var lead: Dictionary = state.leads[i]
			if String(lead.get("stage", "meeting")) != stage:
				continue
			if chips.size() >= 4:
				hidden += 1
				continue
			var heat := int(lead.get("heat", 0))
			# THE HEAT WORD WEARS THE RAMP (§1.1, and 05 §12): coral → yell → sage,
			# ONE word, never the line. Folded into `facts` it was just more ink,
			# and the whole point of the chip — is this deal warming or dying — went
			# grey. The kit draws `heat` on its own row in the ramp's colour.
			var chip := {
				"name": String(lead.get("name", "a prospect")),
				"facts_lead": "%d seats" % int(lead.get("seats", 0)),
				"heat": SimPipeline.heat_word(heat),
				"facts": "wk %d" % int(lead.get("age_weeks", 0)),
				"flavor": String(lead.get("flavor", "")),
			}
			# the coral clock: a deal two weeks from dying of no-decision says so
			var dies := SimPipeline.weeks_to_cold(heat, decay)
			if dies <= 2:
				chip["note"] = "dies in %d wk%s" % [dies, "" if dies == 1 else "s"]
			chips.append(chip)
		cols.append({"head": stage, "chips": chips, "hidden": hidden})
	return cols

# ── the logos strip ─────────────────────────────────────────────────────────
## Biggest accounts first, with a renewal countdown once one is close enough to
## plan around (floor/hq only — before that there is no annual contract to lose).
static func _logo_strip(state: GameState) -> String:
	if state.logos.is_empty():
		return "logos: none signed yet — a contract is the only way an enterprise customer arrives"
	var idx: Array = []
	for i in state.logos.size():
		idx.append(i)
	idx.sort_custom(func(a: int, c: int) -> bool:
		var sa := int((state.logos[a] as Dictionary).get("seats", 0))
		var sc := int((state.logos[c] as Dictionary).get("seats", 0))
		if sa != sc:
			return sa > sc
		return a < c)
	var parts: Array[String] = []
	for n in mini(idx.size(), 8):
		var lg: Dictionary = state.logos[idx[n]]
		var due := int(lg.get("renewal_wk", 0)) - state.week
		if due > 0 and due <= 4:
			parts.append("%s (%d, renews in %d wks)" % [String(lg.get("name", "?")),
				int(lg.get("seats", 0)), due])
		else:
			parts.append("%s (%d)" % [String(lg.get("name", "?")), int(lg.get("seats", 0))])
	var out := "logos: " + " · ".join(PackedStringArray(parts))
	if idx.size() > 8:
		out += " · +%d more" % (idx.size() - 8)
	return out

# ── the teaching footer ─────────────────────────────────────────────────────
## The four numbers an enterprise founder has to learn to say out loud. Each one
## stays a "?" until the run has actually earned it — a made-up win rate teaches
## worse than an honest blank.
static func _footer_line(state: GameState) -> String:
	var st: Dictionary = state.pipe_stats
	var signed := int(st.get("signed", 0))
	var lost := int(st.get("lost", 0))
	var seats := int(st.get("seats_signed", 0))
	var spend := float(st.get("spend", 0.0))
	var decided := signed + lost
	var win := "?" if decided <= 0 else "%d/%d (%d%%)" % [signed, decided,
		int(round(100.0 * float(signed) / float(decided)))]
	var cycle := "?" if signed <= 0 else "%d" % int(round(float(st.get("cycle_sum", 0)) / float(signed)))
	var cost := "?" if seats <= 0 else "$%d" % int(round(spend / float(seats)))
	return "win rate %s · avg cycle %s wks · cost per signed seat ≈ %s · a seat pays ≈ $%.0f/wk" % [
		win, cycle, cost, SimPipeline.unit_rev_wk(state)]
