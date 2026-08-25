class_name DeskCustomers
extends RefCounted
## DESK — the binder's `customers` tab. Spec: docs/design/04-funnel-channels.md
##
## `binder.gd` dispatches the tab body here and passes ITSELF, so this file draws
## through the binder's own helpers and never reaches into the sheet directly.
##
## WHAT IS HERE NOW is today's shipped fog-of-war customer page, moved verbatim
## off the binder so the lane REPLACES a working baseline instead of a blank
## file. The lane's job (04): the funnel READS at their analytics gates on this
## branch only — the spend controls live on the ledger, and the Enterprise
## branch belongs wholesale to the pipeline lane below.
##
## The bar every surface ships at (docs/design/00-spine.md §11): readable first
## pass by a tired player; concepts named in real business terms with a teaching
## line where a number first appears; no dead ends and every state leavable;
## drawn in the game's hand, never a SaaS panel. The shared components live in
## game/src/ui/components.gd (DeskKit) — use them, never fork them.

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
	if state.analytics_level <= 0:
		b.label("%d customers, give or take." % state.traction, Vector2(100, 10), 46)
		b.label("Traffic seems… decent? Someone signed up on Tuesday. The numbers live in a notebook you lost.",
			Vector2(10, 110), 30, Color(Binder.INK, 0.7))
		b.label("(invest in analytics to see the funnel)", Vector2(10, 210), 26, Binder.PEN)
		return
	b.label("%d customers" % state.traction, Vector2(100, 10), 46)
	b.label("customers, weekly:", Vector2(10, 100), 24, Color(Binder.INK, 0.6))
	b.spark(b.series("customers"), Vector2(10, 132), Vector2(1120, 200), Binder.SAGE)
	var th := state.theta
	# WORKING ASSUMPTIONS (owner: nobody knows their TAM on day one): the
	# binder shows what the founder BELIEVES; operating and analytics refine it
	var believed_tam := float(state.beliefs.get("tam", th.get("tam", 100000.0)))
	var pen_pct := float(state.traction) / maxf(believed_tam, 1.0) * 100.0
	b.label("market, as you believe it: ~%s buyers (%.1f%% reached) · a customer stays ≈ %d wks" % [
		b.fmt(int(believed_tam)), pen_pct,
		int(state.beliefs.get("lifetime_wk", th.get("lifetime_wk", 40)))],
		Vector2(10, 356), 27)
	b.label("working assumptions — they sharpen as you learn", Vector2(10, 392), 22,
		Color(Binder.INK, 0.5))
	if state.analytics_level >= 2:
		var mk := float(state.marketing_budget)
		var cac := "∞" if mk <= 0.0 else "$%d" % int(mk / maxf(1.0, mk / 900.0))
		b.label("price ×%.2f · marketing $%s/wk · CAC roughly %s" % [
			state.price_mult, b.fmt(state.marketing_budget), cac], Vector2(10, 404), 28)
		b.label("lifetime ≈ %d wks at v0.%d quality" % [
			int(float(th.get("lifetime_wk", 40.0)) * (0.4 + float(state.product) / 100.0 * 1.2)),
			state.product], Vector2(10, 448), 28)
	if state.analytics_level >= 3:
		b.label("the funnel is fully lit: organic + word-of-mouth + paid, all measured. You are the analytics now.",
			Vector2(10, 500), 26, Binder.SAGE)

## A press inside this desk. `id` is whatever the desk's own draw registered.
static func handle(_b, _id: String) -> void:
	pass

## THE ENTERPRISE BRANCH belongs to the pipeline lane, drawn inside this desk.
## The call site is planted so neither lane has to edit the other's file.
static func draw_enterprise(b) -> void:
	DeskPipeline.draw_board(b)
