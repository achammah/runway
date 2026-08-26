class_name DeskEvents
extends RefCounted
## DESK — THE LOG · "events", the mail. W2 lane: L-COMPANY.
## THE QUESTION THIS DESK ANSWERS: "what has the world sent us?"
## Spec: docs/design/12-binder-rework-2.md § events.
##
## The inbox STREAM: letters and notices derived from the run's own durable
## records — deadlines, the weather turning, applications, asks, filed notes,
## signed paper, the rivals' moves — newest first, unread bold, each with its
## week stamp and its desk jump. Distinction from threats: threats = the
## standing dangers RANKED; events = the stream AS IT HAPPENED. A letter that
## requires action wears the dot and stands in threats until answered.
##
## READ-MARKS are durable but never game-state: they live beside the tour
## flag in the user:// settings store, keyed per-run by sim_seed
## (user://mail_read_<seed>.json — {letter_key: true}). No engine field is
## touched; a save carries no read-marks, exactly like the tour.
## Read letters FOLD BY WEEK; the fold row reopens them.

const QUESTION := "what has the world sent us?"

const UNREAD_CAP := 9      ## unread letters face-up before the honest tail

static func hero_summary(state) -> Dictionary:
	var s: GameState = state
	var letters := _letters(s)
	var read := _read_marks(s)
	var unread := 0
	var newest := ""
	for l in letters:
		if not read.has(String((l as Dictionary).get("key", ""))):
			unread += 1
			if newest == "":
				newest = String((l as Dictionary).get("text", ""))
	if letters.is_empty():
		return {"big": "an empty tray", "line": "the world writes as it acts"}
	if unread == 0:
		return {"big": "all read", "line": "%d letters filed by week" % letters.size()}
	return {"big": "%d unread" % unread, "line": newest}

# ── the mail itself ───────────────────────────────────────────────────────────

## Every letter the run's records can prove, newest first. A letter:
## {key, wk, stamp, text, value, desk, action}. `value` is the letter's money,
## kept OUT of the sentence (Law 2 — money lives in columns).
static func _letters(s: GameState) -> Array:
	var out: Array = []
	# the buyout on the table — the loudest letter there is
	if not s.mna.is_empty():
		var buyer := String(s.mna.get("buyer", "someone"))
		var exp := int(s.mna.get("expires_week", s.week))
		out.append({"key": "mna:%s:%d" % [buyer, exp], "wk": maxi(s.mna_last_week, 1),
			"stamp": "answer by wk %d" % exp,
			"text": "%s makes an offer for the company" % buyer,
			"value": "$%s" % _fmt(int(s.mna.get("price", 0))),
			"desk": "cap table", "action": true})
	# the momentary buyout desk (the ownership lane's structured offer)
	if not s.buyout_offer.is_empty():
		var b2 := String(s.buyout_offer.get("buyer", "someone"))
		out.append({"key": "buyout:%s" % b2, "wk": s.week,
			"stamp": "on the table",
			"text": "%s wants to buy the company — the fine print waits" % b2,
			"value": "$%s" % _fmt(int(s.buyout_offer.get("cash", 0))),
			"desk": "cap table", "action": true})
	# the board's covenant letter
	if not s.board.is_empty() and int(s.board.get("review_week", 0)) >= s.week:
		out.append({"key": "board:%d" % int(s.board.get("review_week", 0)),
			"wk": s.week, "stamp": "review wk %d" % int(s.board.get("review_week", 0)),
			"text": "the board writes: the revenue covenant is coming due",
			"value": "$%s/wk" % _fmt(int(s.board.get("target_revenue", 0))),
			"desk": "cap table", "action": int(s.board.get("strikes", 0)) > 0})
	# deadlines — cliffs coming
	for c in s.clocks:
		var cd: Dictionary = c
		var fire := s.week + int(cd.get("weeks_left", 0))
		out.append({"key": "clock:%s:%d" % [String(cd.get("consequence", "")).left(20), fire],
			"wk": fire, "stamp": "fires wk %d" % fire,
			"text": "a deadline: %s" % String(cd.get("consequence", "")),
			"value": "", "desk": "threats", "action": true})
	# the weather turning — the street's own announcements
	for key in ["winter_watch", "boom_watch", "funding_winter", "boom"]:
		if SimEngine.has_status(s, String(key)):
			out.append({"key": "weather:%s" % key, "wk": s.week,
				"stamp": "%d wks left" % SimStreet.weeks_left(s, String(key)),
				"text": String(SimStreet.BANNER.get(key, "")).to_lower(),
				"value": "", "desk": "the street", "action": false})
	# people writing in — applications and asks
	for ap in s.applicants:
		var ad: Dictionary = ap
		out.append({"key": "apply:%s:%d" % [String(ad.get("name", "")),
			int(ad.get("applied_week", 0))], "wk": int(ad.get("applied_week", 0)),
			"stamp": "wk %d" % int(ad.get("applied_week", 0)),
			"text": "%s applied — %s" % [String(ad.get("name", "?")),
				String(ad.get("role", ""))],
			"value": "", "desk": "team", "action": false})
	for e in s.employees:
		var ed: Dictionary = e
		if bool(ed.get("wants_raise", false)):
			var wk := int(ed.get("asked_week", s.week))
			out.append({"key": "ask:%s:%d" % [String(ed.get("name", "")), wk],
				"wk": wk, "stamp": "wk %d" % wk,
				"text": "%s asks about money" % String(ed.get("name", "?")),
				"value": "", "desk": "team", "action": true})
	# the bank's filed letters
	for l in s.loans:
		var ld: Dictionary = l
		out.append({"key": "loan:%s:%d" % [String(ld.get("kind", "")),
			int(ld.get("taken_week", 0))], "wk": int(ld.get("taken_week", 0)),
			"stamp": "wk %d" % int(ld.get("taken_week", 0)),
			"text": "the %s note, filed — the Mondays are booked"
				% String(ld.get("kind", "bank")),
			"value": "$%s" % _fmt(int(ld.get("balance", 0))),
			"desk": "the bank", "action": int(ld.get("missed", 0)) > 0})
	# signed ownership paper
	for inst in s.instruments:
		var idd: Dictionary = inst
		out.append({"key": "paper:%s:%d" % [String(idd.get("holder", "")),
			int(idd.get("signed_wk", 0))], "wk": int(idd.get("signed_wk", 0)),
			"stamp": "wk %d" % int(idd.get("signed_wk", 0)),
			"text": "signed: a %s from %s" % [String(idd.get("kind", "")),
				String(idd.get("holder", "?"))],
			"value": "$%s" % _fmt(int(idd.get("amount", 0))),
			"desk": "cap table", "action": false})
	# the rivals' moves, from their own rap sheets (entries carry "wkN: act")
	for rv in s.rivals:
		var rd: Dictionary = rv
		for lg in rd.get("log", []):
			var line := String(lg)
			var colon := line.find(":")
			if not line.begins_with("wk") or colon <= 2:
				continue
			var wk2 := int(line.substr(2, colon - 2))
			out.append({"key": "rival:%s:%d" % [String(rd.get("name", "")), wk2],
				"wk": wk2, "stamp": "wk %d" % wk2,
				"text": "%s: %s" % [String(rd.get("name", "?")),
					line.substr(colon + 1).strip_edges()],
				"value": "", "desk": "the street", "action": false})
	# the price book arriving with the world
	if not s.price_book.is_empty():
		out.append({"key": "pricebook:1", "wk": 1, "stamp": "wk 1",
			"text": "the price book arrived — every structural door, priced in advance",
			"value": "", "desk": "the works", "action": false})
	# newest first; ties keep builder order (stable via index)
	var idx := 0
	for l2 in out:
		(l2 as Dictionary)["_i"] = idx
		idx += 1
	out.sort_custom(func(a: Dictionary, c: Dictionary) -> bool:
		if int(a.get("wk", 0)) != int(c.get("wk", 0)):
			return int(a.get("wk", 0)) > int(c.get("wk", 0))
		return int(a.get("_i", 0)) < int(c.get("_i", 0)))
	for l3 in out:
		(l3 as Dictionary).erase("_i")
	return out

# ── the read-marks (the tour flag's own store, per-run keyed) ────────────────

static func _marks_path(s: GameState) -> String:
	return "user://mail_read_%d.json" % s.sim_seed

static func _read_marks(s: GameState) -> Dictionary:
	if not FileAccess.file_exists(_marks_path(s)):
		return {}
	var parsed = JSON.parse_string(FileAccess.get_file_as_string(_marks_path(s)))
	return parsed if parsed is Dictionary else {}

static func _mark_read(s: GameState, key: String) -> void:
	var marks := _read_marks(s)
	marks[key] = true
	var f := FileAccess.open(_marks_path(s), FileAccess.WRITE)
	if f != null:
		f.store_string(JSON.stringify(marks))
		f.close()

# ── the page ──────────────────────────────────────────────────────────────────

static func draw(b) -> void:
	var s: GameState = b.state
	var letters := _letters(s)
	var read := _read_marks(s)
	var unread: Array = []
	var read_by_wk := {}
	for l in letters:
		var ld: Dictionary = l
		if read.has(String(ld.get("key", ""))):
			var wk := int(ld.get("wk", 0))
			if not read_by_wk.has(wk):
				read_by_wk[wk] = []
			(read_by_wk[wk] as Array).append(ld)
		else:
			unread.append(ld)

	# HERO — the unread count answers the question
	var hs := hero_summary(s)
	var y := DeskKit.hero_band(b, String(hs.get("big", "")), String(hs.get("line", "")),
		DeskKit.ALERT if unread.size() > 0 and bool((unread[0] as Dictionary).get("action", false))
		else DeskKit.INK)

	if letters.is_empty():
		DeskKit.empty(b, Vector2(DeskKit.X_ID, y),
			"the tray is empty — the world has not written yet.",
			"deadlines, applications, filed notes and the street's moves all land here.")
	# THE UNREAD — bold, newest first, the dot on action letters. THE COLLAPSE
	# LAW: a letter that needs an answer never folds away; the newest quiet
	# letters fill whatever the face-up cap has left.
	var face_up: Array = []
	var quiet: Array = []
	for lu in unread:
		if bool((lu as Dictionary).get("action", false)):
			face_up.append(lu)
		else:
			quiet.append(lu)
	var quiet_slots := maxi(UNREAD_CAP - face_up.size(), 0)
	for qi in mini(quiet.size(), quiet_slots):
		face_up.append(quiet[qi])
	face_up.sort_custom(func(a, c) -> bool:
		return int((a as Dictionary).get("wk", 0)) > int((c as Dictionary).get("wk", 0)))
	for ld2 in face_up:
		y = _letter_row(b, s, y, ld2 as Dictionary, false)
	var hidden := unread.size() - face_up.size()
	if hidden > 0:
		b.label("+%d more unread below the fold" % hidden,
			Vector2(DeskKit.X_ID + 36.0, y), 17, Color(DeskKit.INK, 0.5), 500.0)
		y += 30.0

	# THE READ — folded by week, reopened a week at a time
	var open_wk := int(b.desk.get("openwk", -1))
	if not read_by_wk.is_empty():
		y = DeskKit.pen_rule(b, y + 6.0)
		var wks := read_by_wk.keys()
		wks.sort_custom(func(a, c) -> bool: return int(a) > int(c))
		for wk2 in wks:
			if y > 760.0:
				break
			var pile: Array = read_by_wk[wk2]
			if int(wk2) == open_wk:
				b.label("wk %d — read:" % int(wk2), Vector2(DeskKit.X_ID, y), 17,
					Color(DeskKit.INK, 0.5), 300.0)
				y += 26.0
				for ld3 in pile:
					y = _letter_row(b, s, y, ld3, true)
			else:
				var wkv := int(wk2)
				DeskKit.word(b, "wk %d — %d read  ->" % [wkv, pile.size()],
					Vector2(DeskKit.X_ID, y - 4.0), func() -> void:
						b.desk["openwk"] = wkv,
					DeskKit.DETAIL, Color(DeskKit.INK, 0.55), 420.0)
				y += 34.0

	DeskKit.footer(b, {
		"computed": "%d letters on file · %d need an answer" % [letters.size(),
			_action_count(letters)],
		"rules": "threats ranks the standing dangers — this page is the stream as it "
			+ "happened · an action letter also stands in threats until answered",
		"y": 820.0, "rules_y": 852.0})

static func _action_count(letters: Array) -> int:
	var n := 0
	for l in letters:
		if bool((l as Dictionary).get("action", false)):
			n += 1
	return n

## One letter row: dot when action · the text (bold when unread) · the money
## in its own column · the stamp · the desk jump. The press marks it read.
static func _letter_row(b, s: GameState, y: float, ld: Dictionary, is_read: bool) -> float:
	var x := DeskKit.X_ID
	if bool(ld.get("action", false)):
		DeskKit.sev_dot(b, x, y + 4.0, 2)
	b.label(String(ld.get("text", "")), Vector2(x + 36.0, y),
		26 if not is_read else DeskKit.DETAIL,
		DeskKit.INK if not is_read else Color(DeskKit.INK, 0.55), 600.0)
	var val := String(ld.get("value", ""))
	if val != "":
		var v: Label = b.label(val, Vector2(x + 640.0, y), DeskKit.DETAIL,
			DeskKit.INK if not is_read else Color(DeskKit.INK, 0.55), 170.0)
		v.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	b.label(String(ld.get("stamp", "")), Vector2(x + 826.0, y + 2.0), 17,
		Color(DeskKit.INK, 0.45), 130.0)
	var dsk := String(ld.get("desk", ""))
	var key := String(ld.get("key", ""))
	DeskKit.word(b, dsk + " ->", Vector2(x + 962.0, y - 4.0), func() -> void:
		_mark_read(b.state, key)
		b.focus_desk(dsk), DeskKit.DETAIL, DeskKit.PEN, 190.0)
	return y + 38.0

static func handle(b, id: String) -> void:
	if id.begins_with("go:"):
		var parts := id.substr(3).split("|")
		if parts.size() == 2:
			_mark_read(b.state, String(parts[1]))
		b.focus_desk(String(parts[0]))
	elif id.begins_with("openwk:"):
		b.desk["openwk"] = int(id.substr(7))

static func _fmt(n: int) -> String:
	var t := str(absi(n))
	var out := ""
	while t.length() > 3:
		out = "," + t.substr(t.length() - 3) + out
		t = t.substr(0, t.length() - 3)
	return ("-" if n < 0 else "") + t + out
