class_name DeskArrange
extends RefCounted
## THE ARRANGE MODE — the works' WRITE view (DECISIONS: ARRANGE MODE + ARRANGE
## EDITS THE BINS; mockup 14). One neutral assignment layout regardless of the
## read face: divisions as labeled BINS (+ SHARED/HQ + the dashed ghost),
## elements as CHIPS. TWO PRESSES, NO DRAG: press a chip, press its new home.
## CHIP MOVES CONFIRM ONE BY ONE (a pre-move receipt each); BIN OPERATIONS
## stage into one composite receipt (the teardown wizard); Esc abandons the
## whole staged change through the binder's desk-mode pop.
##
## PAPER vs BRICK: moving a person prices the relocation + a ramp week; a
## machine prices shipping + a week offline; tags (offer -> product, spend line
## -> division/shared) are FREE — paper is paper. Bound elements never move by
## hand: rent belongs to its roof, serving costs to their offer, interest to
## its note. Bin verbs: ✎ rename free (ink) / re-lease priced (brick);
## ✕ the teardown wizard — every element decided, ONE receipt, the payback
## line derived; the ghost bin is the SAME open_site door as the written move.
##
## Every price on every receipt is the ENGINE's own quote (week-stable), so
## the preview and the booking can never disagree.

const BIN_W := 176.0
const BIN_H := 200.0
const BIN_GAP := 12.0

static func draw(b) -> void:
	var s: GameState = b.state
	DeskKit.back(b, "back to the works", func() -> void:
		b.desk["mode"] = ""
		for k in ["chip_k", "staged2", "teardown", "open_roof", "edit", "arrange_axis"]:
			b.desk.erase(k))
	# ── the submodes own the whole sheet
	if bool(b.desk.get("open_roof", false)):
		_open_roof_sheet(b, s)
		return
	var td := String(b.desk.get("teardown", ""))
	if td != "":
		_teardown_sheet(b, s, td)
		return
	b.label("ARRANGE — press a thing, then press its new home", Vector2(230.0, 8.0),
		DeskKit.STATUS, Color(DeskKit.INK, 0.6), 620.0)
	b.label("ARRANGING · Esc exits", Vector2(DeskKit.X_ID + 890.0, 10.0), DeskKit.DETAIL,
		DeskKit.PEN, 230.0)
	var axis := _axis(b, s)
	var y := 58.0
	if axis == "product":
		y = _product_bins(b, s, y)
	else:
		y = _site_bins(b, s, y)
		if String(b.desk.get("edit", "")) != "":
			y += 104.0   # the open edit panel's room under its bin
	# ── the axis toggle, only when both axes are real
	if SimDivisions.products_count(s) >= 2 and axis == "site":
		DeskKit.word(b, "arrange the paper instead (offers -> products) ->",
			Vector2(DeskKit.X_ID, y), func() -> void:
				b.desk["arrange_axis"] = "product"
				b.desk.erase("chip_k")
				b.desk.erase("staged2"), DeskKit.DETAIL, DeskKit.BLUE, 560.0)
		y += 44.0
	elif axis == "product":
		DeskKit.word(b, "back to the roofs (people, machines, spend) ->",
			Vector2(DeskKit.X_ID, y), func() -> void:
				b.desk["arrange_axis"] = "site"
				b.desk.erase("chip_k")
				b.desk.erase("staged2"), DeskKit.DETAIL, DeskKit.BLUE, 560.0)
		y += 44.0
	# ── THE LOCKED STRIP: what never moves by hand, so the player learns
	# which costs follow which objects
	b.label("bound to their objects (never move by hand): rent -> its roof · serving costs -> their offer · interest -> its note",
		Vector2(DeskKit.X_ID, y), DeskKit.LAW, Color(DeskKit.INK, 0.5), 1100.0)
	y += 40.0
	# ── the staged chip move: ONE at a time, its own receipt, two-tap
	var staged: Dictionary = b.desk.get("staged2", {})
	if staged.is_empty():
		var picked := String(b.desk.get("chip_k", ""))
		DeskKit.footer(b, {"y": 806.0,
			"computed": "ink is free · brick is priced · obligations survive removal",
			"rules": ("now press its new home — the receipt prints before anything books" if picked != ""
				else "two presses stage a move; chip moves confirm one by one"), "rules_y": 840.0})
		return
	_staged_receipt(b, s, staged, y)

# ─────────────────────────────── the bins ────────────────────────────────────

static func _axis(b, s: GameState) -> String:
	var ax := String(b.desk.get("arrange_axis", "site"))
	if ax == "product" and SimDivisions.products_count(s) < 2:
		ax = "site"
	return ax

## The site axis: home roof + opened roofs (verbs on the opened) + SHARED/HQ
## + the ghost. Chips live INSIDE their bins; the crowd folds to a count.
static func _site_bins(b, s: GameState, y: float) -> float:
	var ids: Array = [""]
	for site in s.sites:
		ids.append(String((site as Dictionary).get("id", "")))
	var shown_sites := mini(ids.size(), 4)
	var picked := String(b.desk.get("chip_k", ""))
	var bx := DeskKit.X_ID
	for i in shown_sites:
		var id := String(ids[i])
		bx = _one_bin(b, s, bx, y, id, picked)
	if ids.size() > shown_sites:
		b.label("+%d more roofs" % (ids.size() - shown_sites), Vector2(bx + 4.0, y + 8.0),
			DeskKit.DETAIL, Color(DeskKit.INK, 0.5), 150.0)
	# SHARED/HQ: the honest bin — spend-line chips live here (or on their roof)
	var shared := DeskKit.bin(b, bx, y, BIN_W, BIN_H, {
		"title": "SHARED / HQ", "note": "what has no roof",
		"on_press": func() -> void: _press_bin(b, s, "shared")})
	var cy := float(shared.get("cursor", y + 58.0))
	var cx := float(shared.get("content_x", bx + 12.0))
	var n_shared := 0
	for li in s.spend_book.size():
		var l: Dictionary = s.spend_book[li]
		if String(l.get("division", "")) != "":
			continue
		if n_shared < 2:
			var key := "s:%d" % li
			DeskKit.chip(b, cx, cy, {"text": String(l.get("name", "line")).left(12),
				"kind": "spend", "selected": picked == key,
				"on_press": func() -> void: _press_chip(b, key)})
			cy += 42.0
		n_shared += 1
	if n_shared > 2:
		b.label("+%d more" % (n_shared - 2), Vector2(cx, cy + 2.0), DeskKit.LAW,
			Color(DeskKit.INK, 0.5), 120.0)
	elif n_shared == 0:
		b.label("you · brand ads", Vector2(cx, cy + 2.0), DeskKit.LAW, Color(DeskKit.INK, 0.45), 150.0)
	bx += BIN_W + BIN_GAP
	# THE GHOST: the same open_site door as the written move — priced first
	DeskKit.bin(b, bx, y, BIN_W, BIN_H, {"ghost": true, "on_press": func() -> void:
		b.desk["open_roof"] = true})
	return y + BIN_H + 22.0

static func _one_bin(b, s: GameState, bx: float, y: float, id: String, picked: String) -> float:
	var is_home := id == ""
	var counts := _bin_counts(s, id)
	var red := SimDivisions.marked_until(s, "works_red", id) >= SimDivisions.RED_WEEKS if not is_home else false
	var f := DeskKit.bin(b, bx, y, BIN_W, BIN_H, {
		"title": SimDivisions._roof_name(s, id).left(14),
		"note": counts, "closing": red,
		"on_press": func() -> void: _press_bin(b, s, id)})
	var cy := float(f.get("cursor", y + 58.0))
	var cx := float(f.get("content_x", bx + 12.0))
	var chips := 0
	for ei in s.employees.size():
		var e: Dictionary = s.employees[ei]
		if String(e.get("site", "")) != id:
			continue
		if chips < 2:
			var key := "e:%d" % ei
			DeskKit.chip(b, cx, cy, {"text": String(e.get("name", "?")).get_slice(" ", 0),
				"kind": "person", "selected": picked == key,
				"on_press": func() -> void: _press_chip(b, key)})
			cy += 42.0
		chips += 1
	var m_chips := 0
	var eq: Array = s.hardware.get("equipment", [])
	for mi in eq.size():
		var m: Dictionary = eq[mi]
		if String(m.get("site", "")) != id:
			continue
		if chips < 2 and m_chips < 1:
			var mkey := "m:%d" % mi
			DeskKit.chip(b, cx, cy, {"text": String(m.get("name", "?")).left(12),
				"kind": "machine", "selected": picked == mkey,
				"on_press": func() -> void: _press_chip(b, mkey)})
			cy += 42.0
			chips += 1
		m_chips += 1
	var crowd := _crowd_count(s, id) - mini(chips, 2)
	if crowd > 0:
		b.label("+%d more" % crowd, Vector2(cx, cy + 2.0), DeskKit.LAW, Color(DeskKit.INK, 0.5), 120.0)
	# THE BIN VERBS (opened roofs only): edit (ink/brick) · close (the wizard).
	# Words, not glyphs — the hand font carries no pencil and no cross.
	if not is_home:
		DeskKit.word(b, "edit", Vector2(bx + 6.0, y + BIN_H - 40.0), func() -> void:
			b.desk["edit"] = id
			b.desk["open_roof"] = false, DeskKit.LAW, Color(DeskKit.INK, 0.7), 70.0)
		# R6 — coral, not the alarm red: the closing bin's drawn ALERT ring
		# already carries the alarm; a verb is a control, not a siren
		DeskKit.word(b, "close", Vector2(bx + 86.0, y + BIN_H - 40.0), func() -> void:
			b.desk["teardown"] = id, DeskKit.LAW, DeskKit.PEN, 80.0)
	# the edit panel rides just under its bin (draw() budgets the room)
	if String(b.desk.get("edit", "")) == id and not is_home:
		_edit_panel(b, s, id, bx, y + BIN_H + 4.0)
	return bx + BIN_W + BIN_GAP

static func _bin_counts(s: GameState, id: String) -> String:
	var heads := 0
	for e in s.employees:
		if String((e as Dictionary).get("site", "")) == id:
			heads += 1
	if id == "" and s.sites.is_empty():
		heads += 1   # the founder's own hands live here
	var hands := "%d hand%s" % [heads, "" if heads == 1 else "s"]
	var site := SimDivisions.site_by_id(s, id)
	if site.is_empty():
		return hands + " · the era's roof" if id == "" else hands
	return "%s · rent $%d/wk" % [hands, int(site.get("rent_wk", 0))]

static func _crowd_count(s: GameState, id: String) -> int:
	var n := 0
	for e in s.employees:
		if String((e as Dictionary).get("site", "")) == id:
			n += 1
	for m in (s.hardware.get("equipment", []) as Array):
		if String((m as Dictionary).get("site", "")) == id:
			n += 1
	return n

## The product axis: PAPER bins — offers re-file for free (tags are ink).
static func _product_bins(b, s: GameState, y: float) -> float:
	var picked := String(b.desk.get("chip_k", ""))
	var pids: Array = []
	for o in s.offers:
		var pid := String((o as Dictionary).get("product_id", ""))
		if not pids.has(pid):
			pids.append(pid)
	var bx := DeskKit.X_ID
	for i in mini(pids.size(), 5):
		var pid2 := String(pids[i])
		var f := DeskKit.bin(b, bx, y, BIN_W + 30.0, BIN_H, {
			"title": ("the flagship" if pid2 == "" else pid2).left(14),
			"note": "a grouping of offers — paper",
			"on_press": func() -> void: _press_bin(b, s, "p:" + pid2)})
		var cy := float(f.get("cursor", y + 58.0))
		var cx := float(f.get("content_x", bx + 12.0))
		var n := 0
		for oi in s.offers.size():
			var od: Dictionary = s.offers[oi]
			if String(od.get("product_id", "")) != pid2:
				continue
			if n < 3:
				var key := "o:%d" % oi
				DeskKit.chip(b, cx, cy, {"text": String(od.get("name", "?")).left(14),
					"kind": "spend", "selected": picked == key,
					"on_press": func() -> void: _press_chip(b, key)})
				cy += 42.0
			n += 1
		if n > 3:
			b.label("+%d more" % (n - 3), Vector2(cx, cy + 2.0), DeskKit.LAW, Color(DeskKit.INK, 0.5), 120.0)
		bx += BIN_W + 30.0 + BIN_GAP
	b.label("PAPER divisions restructure FREE — a product is a grouping of offers, and regrouping is ink",
		Vector2(DeskKit.X_ID, y + BIN_H + 6.0), DeskKit.LAW, Color(DeskKit.INK, 0.5), 1100.0)
	return y + BIN_H + 40.0

# ─────────────────────── two presses, one receipt ────────────────────────────

static func _press_chip(b, key: String) -> void:
	if String(b.desk.get("chip_k", "")) == key:
		b.desk.erase("chip_k")
	else:
		b.desk["chip_k"] = key
		b.desk.erase("staged2")

static func _press_bin(b, s: GameState, target: String) -> void:
	var key := String(b.desk.get("chip_k", ""))
	if key == "":
		return
	var kind := key.get_slice(":", 0)
	var idx := int(key.get_slice(":", 1))
	# a fresh quote for the receipt — the engine's own numbers, week-stable
	var st := {"kind": kind, "idx": idx, "to": target}
	match kind:
		"e":
			if target.begins_with("p:") or target == "shared":
				return   # people move between roofs, not onto paper
			st["quote"] = SimDivisions.reassign_quote(s, idx, target)
		"m":
			if target.begins_with("p:") or target == "shared":
				return
			st["quote"] = SimDivisions.move_quote(s, idx, target)
		"o":
			if not target.begins_with("p:"):
				return   # offers are paper — they file under products
			st["quote"] = {}
		"s":
			if target.begins_with("p:"):
				return
			st["quote"] = {}
	b.desk["staged2"] = st
	b.desk.erase("chip_k")

## THE PRE-MOVE RECEIPT: the truth before the two-tap; tear it up for free.
static func _staged_receipt(b, s: GameState, st: Dictionary, y: float) -> void:
	var kind := String(st.get("kind", ""))
	var idx := int(st.get("idx", 0))
	var target := String(st.get("to", ""))
	var q: Dictionary = st.get("quote", {})
	var lines: Array = []
	var total := ""
	var total_col := DeskKit.PEN
	var title := "the staged change — nothing is booked yet"
	var fire := Callable()
	match kind:
		"e":
			var nm := String((s.employees[idx] as Dictionary).get("name", "?")) if idx < s.employees.size() else "?"
			lines = [{"label": "%s -> %s" % [nm, SimDivisions._roof_name(s, target)],
				"value": "$%s now" % str(int(q.get("fee", 0)))},
				{"label": "the ramp at the new roof", "value": "1 wk at zero"}]
			total = "$%d now" % int(q.get("fee", 0))
			fire = func() -> void:
				var res := SimDivisions.reassign_employee(s, idx, target)
				b.desk["note"] = String(res.get("why", "")) if not bool(res.get("ok", false)) else ""
				b.desk.erase("staged2")
		"m":
			var mn := String(((s.hardware.get("equipment", []) as Array)[idx] as Dictionary).get("name", "?")) \
				if idx < (s.hardware.get("equipment", []) as Array).size() else "?"
			lines = [{"label": "%s -> %s (shipping)" % [mn, SimDivisions._roof_name(s, target)],
				"value": "$%s now" % str(int(q.get("fee", 0)))},
				{"label": "off the floor", "value": "1 wk offline"}]
			total = "$%d now" % int(q.get("fee", 0))
			fire = func() -> void:
				var res := SimDivisions.move_machine(s, idx, target)
				b.desk["note"] = String(res.get("why", "")) if not bool(res.get("ok", false)) else ""
				b.desk.erase("staged2")
		"o":
			var onm := String((s.offers[idx] as Dictionary).get("name", "?")) if idx < s.offers.size() else "?"
			var pid := target.trim_prefix("p:")
			lines = [{"label": "%s files under %s" % [onm, "the flagship" if pid == "" else pid],
				"value": "free — ink"}]
			total = "$0 — paper is paper"
			total_col = DeskKit.SAGE
			fire = func() -> void:
				SimDivisions.tag_offer(s, idx, pid)
				b.desk.erase("staged2")
		"s":
			var snm := String((s.spend_book[idx] as Dictionary).get("name", "?")) if idx < s.spend_book.size() else "?"
			lines = [{"label": "%s files under %s" % [snm,
				"SHARED/HQ" if target == "shared" or target == "" else SimDivisions._roof_name(s, target)],
				"value": "free — ink"}]
			total = "$0 — paper is paper"
			total_col = DeskKit.SAGE
			fire = func() -> void:
				SimDivisions.tag_spend_line(s, idx, "" if target == "shared" else target)
				b.desk.erase("staged2")
	var end_y := DeskKit.ticket(b, DeskKit.X_ID, y, 560.0, {
		"title": title, "lines": lines, "total_label": "the price of the move",
		"total_value": total, "total_col": total_col,
		"foot": "the engine quoted this — signing books exactly these numbers"})
	DeskKit.arm(b, "arrange_confirm", "CONFIRM the move", "press again — it books now",
		Vector2(620.0, y + 30.0), fire, 360.0)
	DeskKit.word(b, "tear it up", Vector2(620.0, y + 84.0), func() -> void:
		b.desk.erase("staged2"), DeskKit.STATUS, Color(DeskKit.INK, 0.7), 240.0)
	var note := String(b.desk.get("note", ""))
	if note != "":
		b.label(note, Vector2(620.0, y + 130.0), DeskKit.DETAIL, DeskKit.PEN, 500.0)
	DeskKit.footer(b, {"y": maxf(end_y, 760.0), "computed": "",
		"rules": "Esc abandons the whole staged change", "rules_y": 840.0})

# ───────────────────────── the ✎ edit panel (brick + ink) ────────────────────

static func _edit_panel(b, s: GameState, id: String, x: float, y: float) -> void:
	var site := SimDivisions.site_by_id(s, id)
	if site.is_empty():
		return
	var px := clampf(x, DeskKit.X_ID, 700.0)
	b.label("✎ %s — rename is ink; the roof is brick" % String(site.get("name", "?")),
		Vector2(px, y), DeskKit.DETAIL, DeskKit.INK, 420.0)
	DeskKit.word(b, "rename (free) ->", Vector2(px, y + 30.0), func() -> void:
		var pool: Array = SimDivisions.NAME_POOL
		var cur := String(site.get("name", ""))
		var i := (pool.find(cur) + 1) % pool.size()
		SimDivisions.rename_site(s, id, String(pool[i])), DeskKit.DETAIL, DeskKit.BLUE, 200.0)
	var up := SimDivisions.relase_quote(s, id, 1)
	var down := SimDivisions.relase_quote(s, id, -1)
	DeskKit.arm(b, "relase_up_" + id, "bigger roof — rent $%d/wk" % int(up.get("new_rent", 0)),
		"press again — $%d books, a moving week" % int(up.get("fee", 0)),
		Vector2(px + 210.0, y + 24.0), func() -> void:
			SimDivisions.edit_site(s, id, 1)
			b.desk.erase("edit"), 340.0, DeskKit.DETAIL)
	DeskKit.arm(b, "relase_dn_" + id, "smaller roof — rent $%d/wk" % int(down.get("new_rent", 0)),
		"press again — $%d books, a moving week" % int(down.get("fee", 0)),
		Vector2(px + 560.0, y + 24.0), func() -> void:
			SimDivisions.edit_site(s, id, -1)
			b.desk.erase("edit"), 340.0, DeskKit.DETAIL)

# ─────────────────── the ✕ teardown wizard (one receipt) ─────────────────────

## Every element of the bin, one decision each, composed into ONE closing
## receipt with the payback line DERIVED — then the two-tap on the total.
static func _teardown_sheet(b, s: GameState, id: String) -> void:
	var site := SimDivisions.site_by_id(s, id)
	if site.is_empty():
		b.desk.erase("teardown")
		return
	b.label("CLOSING %s — every piece decided, one total" % String(site.get("name", "?")).to_upper(),
		Vector2(230.0, 8.0), DeskKit.STATUS, DeskKit.INK, 700.0)
	var y := 56.0
	var decisions := _decisions(b, s, id)
	# ── THE PEOPLE: move (priced) or let go (severance ALWAYS owed)
	var any := false
	for ei in s.employees.size():
		var e: Dictionary = s.employees[ei]
		if String(e.get("site", "")) != id:
			continue
		any = true
		var key := "e:%d" % ei
		var cur := String(decisions.get(key, "go"))
		b.label(String(e.get("name", "?")), Vector2(DeskKit.X_ID + 10.0, y), DeskKit.DETAIL, DeskKit.INK, 240.0)
		var sev := SimLabor.severance_for(s, e)
		var caption := ("let go — severance $%d" % sev) if cur == "go" \
			else "move -> %s (+$%d)" % [SimDivisions._roof_name(s, cur.trim_prefix("move:")), int(round(SimDivisions.pb(s, "relocation_fee")))]
		DeskKit.word(b, caption, Vector2(DeskKit.X_ID + 260.0, y - 6.0), func() -> void:
			b.desk["td_" + key] = _next_decision(s, id, cur), DeskKit.DETAIL,
			DeskKit.PEN if cur == "go" else DeskKit.BLUE, 460.0)
		y += 40.0
	# ── THE THINGS: move (shipping) or sell at half
	var eq: Array = s.hardware.get("equipment", [])
	for mi in eq.size():
		var m: Dictionary = eq[mi]
		if String(m.get("site", "")) != id:
			continue
		any = true
		var mkey := "m:%d" % mi
		var mcur := String(decisions.get(mkey, "sell"))
		b.label(String(m.get("name", "?")), Vector2(DeskKit.X_ID + 10.0, y), DeskKit.DETAIL, DeskKit.INK, 240.0)
		var mcaption := ("sell at half — +$%d" % SimFactory.resale_value(String(m.get("id", "")))) if mcur == "sell" \
			else "move -> %s (+$%d, 1 wk offline)" % [SimDivisions._roof_name(s, mcur.trim_prefix("move:")), int(round(SimDivisions.pb(s, "machine_shipping")))]
		DeskKit.word(b, mcaption, Vector2(DeskKit.X_ID + 260.0, y - 6.0), func() -> void:
			b.desk["td_" + mkey] = _next_machine_decision(s, id, mcur), DeskKit.DETAIL,
			DeskKit.SAGE if mcur == "sell" else DeskKit.BLUE, 460.0)
		y += 40.0
	if not any:
		b.label("nothing lives under this roof but the lease and its customers",
			Vector2(DeskKit.X_ID + 10.0, y), DeskKit.DETAIL, Color(DeskKit.INK, 0.6), 700.0)
		y += 40.0
	# ── THE COMPOSITE RECEIPT, engine-quoted whole
	var q := SimDivisions.close_quote(s, id, decisions)
	var lines: Array = []
	for l in q.get("lines", []):
		var ld: Dictionary = l
		var amt := int(ld.get("amount", 0))
		lines.append({"label": String(ld.get("label", "")),
			"value": "%s$%s" % ["+" if amt >= 0 else "−", _commas(absi(amt))],
			"col": DeskKit.SAGE if amt >= 0 else DeskKit.PEN})
	lines.append({"label": "≈%d transfer (with churn risk)" % int(q.get("kept", 0)), "value": "kept, fragile"})
	lines.append({"label": "≈%d lost with the roof" % int(q.get("lost", 0)),
		"value": "−$%s/wk" % _commas(int(q.get("lost_rev_wk", 0))), "col": DeskKit.PEN})
	lines.append({"label": "bills that die with the roof",
		"value": "+$%s/wk freed" % _commas(int(q.get("freed_wk", 0))), "col": DeskKit.SAGE})
	var net := int(q.get("net_now", 0))
	var payback := int(q.get("payback_wk", -1))
	var margin_wk := int(q.get("site_margin_wk", 0))
	var foot := ""
	if payback >= 0:
		foot = "%s loses $%s/wk — this closing pays back in ≈%d weeks" % [
			String(site.get("name", "?")), _commas(absi(mini(margin_wk, 0))), payback]
	else:
		foot = "the roof still earns its keep — closing frees no net money"
	var end_y := DeskKit.ticket(b, DeskKit.X_ID + 40.0, y + 6.0, 620.0, {
		"title": "CLOSING %s — ONE TOTAL" % String(site.get("name", "?")).to_upper(),
		"lines": lines, "total_label": "closing costs, net, today",
		"total_value": "%s$%s" % ["+" if net >= 0 else "−", _commas(absi(net))],
		"total_col": DeskKit.SAGE if net >= 0 else DeskKit.PEN, "foot": foot})
	DeskKit.arm(b, "close_site_" + id, "CLOSE IT — two-tap",
		"press again — the roof comes down for %s$%s" % ["+" if net >= 0 else "−", _commas(absi(net))],
		Vector2(700.0, y + 40.0), func() -> void:
			SimDivisions.close_site(s, id, _decisions(b, s, id))
			for k in b.desk.keys().duplicate():
				if String(k).begins_with("td_"):
					b.desk.erase(k)
			b.desk.erase("teardown"), 380.0)
	DeskKit.word(b, "or Esc keeps %s" % String(site.get("name", "?")), Vector2(700.0, y + 94.0),
		func() -> void: b.desk.erase("teardown"), DeskKit.DETAIL, Color(DeskKit.INK, 0.6), 320.0)
	DeskKit.footer(b, {"y": maxf(end_y + 6.0, 760.0),
		"computed": "severance is always owed · machines sell at half · the lease breaks at %d weeks of rent" % int(round(SimDivisions.pb(s, "lease_break_weeks"))),
		"rules": "the customers are decided FOR you — some transfer fragile, the rest die with the roof", "rules_y": 840.0})

static func _decisions(b, s: GameState, id: String) -> Dictionary:
	var out := {}
	for ei in s.employees.size():
		if String((s.employees[ei] as Dictionary).get("site", "")) == id:
			out["e:%d" % ei] = String(b.desk.get("td_e:%d" % ei, "go"))
	var eq: Array = s.hardware.get("equipment", [])
	for mi in eq.size():
		if String((eq[mi] as Dictionary).get("site", "")) == id:
			out["m:%d" % mi] = String(b.desk.get("td_m:%d" % mi, "sell"))
	return out

## The decision word cycles: go -> move:<home> -> move:<each other roof> -> go.
static func _next_decision(s: GameState, closing_id: String, cur: String) -> String:
	var dests: Array = [""]
	for site in s.sites:
		var sid := String((site as Dictionary).get("id", ""))
		if sid != closing_id:
			dests.append(sid)
	if cur == "go":
		return "move:" + String(dests[0])
	var at := dests.find(cur.trim_prefix("move:"))
	if at >= 0 and at < dests.size() - 1:
		return "move:" + String(dests[at + 1])
	return "go"

static func _next_machine_decision(s: GameState, closing_id: String, cur: String) -> String:
	var nxt := _next_decision(s, closing_id, "go" if cur == "sell" else cur)
	return "sell" if nxt == "go" else nxt

# ──────────────── the ghost bin: the open_site door, priced ──────────────────

static func _open_roof_sheet(b, s: GameState) -> void:
	var q := SimDivisions.quote_site(s)
	var pack := int(q.get("pack", 0))
	b.label("A NEW ROOF — the same door as the written move, priced before you sign",
		Vector2(230.0, 8.0), DeskKit.STATUS, DeskKit.INK, 800.0)
	var nm := String(b.desk.get("roof_name", String(q.get("name", "the new roof"))))
	var y := 64.0
	b.label("the sign over the door:", Vector2(DeskKit.X_ID + 10.0, y), DeskKit.DETAIL,
		Color(DeskKit.INK, 0.6), 260.0)
	DeskKit.word(b, "%s  (another name ->)" % nm, Vector2(DeskKit.X_ID + 280.0, y - 6.0), func() -> void:
		var pool: Array = SimDivisions.NAME_POOL
		var i := (pool.find(nm) + 1) % pool.size()
		b.desk["roof_name"] = String(pool[i]), DeskKit.STATUS, DeskKit.BLUE, 420.0)
	y += 52.0
	var lines: Array = []
	for pl in SimDivisions.pack_lines(pack):
		lines.append({"label": String((pl as Dictionary).get("label", "")),
			"value": "$%s" % _commas(int((pl as Dictionary).get("amount", 0)))})
	lines.append({"label": "rent, from the first Monday", "value": "$%s/wk" % _commas(int(q.get("rent_wk", 0)))})
	lines.append({"label": "local wages", "value": "×%.2f" % float(q.get("wage_mult", 1.0))})
	lines.append({"label": "demand ramps on its own curve", "value": "≈12 wks"})
	var end_y := DeskKit.ticket(b, DeskKit.X_ID + 40.0, y, 560.0, {
		"title": "OPENING %s" % nm.to_upper(), "lines": lines,
		"total_label": "the pack, signed today", "total_value": "−$%s" % _commas(pack),
		"total_col": DeskKit.PEN,
		"foot": "the price book quoted this at run start — the path was visible before the decision"})
	var can := s.cash >= pack and pack <= SimEngine.era_spend_cap(s.era)
	if can:
		DeskKit.arm(b, "open_roof", "OPEN THE ROOF", "press again — $%s signs now" % _commas(pack),
			Vector2(660.0, y + 40.0), func() -> void:
				SimDivisions.open_site(s, nm)
				b.desk.erase("open_roof")
				b.desk.erase("roof_name"), 380.0)
	else:
		b.label("the pack refuses: %s" % ("$%s short" % _commas(pack - s.cash) if s.cash < pack
			else "past what a %s can sign for" % s.era),
			Vector2(660.0, y + 44.0), DeskKit.DETAIL, DeskKit.PEN, 420.0)
	DeskKit.word(b, "not today", Vector2(660.0, y + 96.0), func() -> void:
		b.desk.erase("open_roof")
		b.desk.erase("roof_name"), DeskKit.STATUS, Color(DeskKit.INK, 0.7), 200.0)
	DeskKit.footer(b, {"y": maxf(end_y, 760.0),
		"computed": "one op, two doors — the written move and this bin sign the SAME receipt",
		"rules": "Esc keeps the money", "rules_y": 840.0})

static func _commas(n: int) -> String:
	var t := str(absi(n))
	var out := ""
	while t.length() > 3:
		out = "," + t.substr(t.length() - 3) + out
		t = t.substr(0, t.length() - 3)
	return ("-" if n < 0 else "") + t + out

static func handle(b, id: String) -> void:
	if id == "leave":
		b.desk["mode"] = ""
