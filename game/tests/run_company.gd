extends SceneTree
## L-COMPANY's standalone gate — hermetic, no network, no window.
## Runs the pivot lane suite (tests/lanes/test_pivot.gd — the same file the
## registered runner preloads once the coordinator lands the package) plus the
## quartet hero-provider pins for the six L-COMPANY desks.
##
## Run: godot --headless --path . --script tests/run_company.gd
## Exit code 0 = every check held; 1 = at least one failed (printed).

var _checks := 0
var _failed := false

func _ok(cond: bool, msg: String) -> void:
	_checks += 1
	if not cond:
		_failed = true
		push_error("FAIL: " + msg)
		print("FAIL: " + msg)

func _init() -> void:
	call_deferred("_go")

func _state() -> GameState:
	var s := GameState.new()
	s.sim_seed = 987_654
	s.week = 21
	s.era = "office"
	s.cash = 42_000
	s.traction = 96
	s.product = 58
	s.morale = 64
	s.hype = 38
	s.biz_what = "Software"
	s.biz_who = "SMB"
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	s.set_flag("launched")
	s.content_equity = 900.0
	s.rivals = [{"name": "Vantage", "strength": 66.0, "tactics": ["undercut"],
		"weeks_since_move": 0, "secret": "", "vigor": 62.0, "hype": 30.0,
		"focus": "price", "price_posture": 0.9, "last_action": "price_cut",
		"log": ["wk19: cut prices", "wk20: quiet", "wk21: cut prices"],
		"cooldowns": {}, "sniffing": 0}]
	s.clocks = [{"weeks_left": 3, "consequence": "the bridge loan comes due"}]
	s.applicants = [{"name": "Mara Voss", "role": "engineer", "skill": 4,
		"ask": 1_700, "quirk": "", "one_liner": "", "applied_week": 20,
		"source": "inbound"}]
	s.employees = [{"name": "Tomas Beck", "role": "sales", "salary": 1_100,
		"burnout": 30, "skill": 3, "hired_week": 9, "wants_raise": true,
		"asked_week": 19, "site": ""}]
	for w in range(1, 21):
		s.metric_history.append({"wk": w, "cash": 60_000 - w * 900,
			"customers": w * 5, "revenue": 120 * w, "burn": 100 * w + 800,
			"morale": 70 - w, "debt": 12, "hype": 20, "net": 20 * w - 800})
	s.run_history.append({"wk": 20, "said": "ship the beta to the list",
		"heard": "", "verdict": "fine", "roll": "d20=14 vs DC 9 (build)",
		"fx": ["status: word_of_mouth for 3 wks — the beta landed"]})
	s.history.append({"week": 20, "entry": "event 'The beta window' — wrote: ship it (fine)"})
	s.history.append({"week": 21, "entry": "wrote: chase the enterprise lead"})
	return s

func _go() -> void:
	await process_frame

	# ── the pivot lane suite, exactly as the registered runner will call it
	var pivot_suite := preload("res://tests/lanes/test_pivot.gd")
	pivot_suite.run(Callable(self, "_ok"))

	# ── the quartet hero providers: one number + one sentence, real values
	var s := _state()
	var hs := DeskStreetPage.hero_summary(s)
	_ok(String(hs.get("big", "")) in ["funding winter", "a boom", "tailwinds",
		"headwinds", "a calm street"],
		"street hero: the big word is one of the season's own")
	_ok(String(hs.get("line", "")) != "", "street hero: the sentence exists")

	var ht := DeskThreatsPage.hero_summary(s)
	var live := SimEngine.attention_items(s).size()
	if live > 0:
		_ok(String(ht.get("big", "")) == "%d live" % live,
			"threats hero: the count matches the registry")
	else:
		_ok(String(ht.get("big", "")) == "nothing is shouting",
			"threats hero: quiet page says so")

	var hp := DeskPivot.hero_summary(s)
	_ok(String(hp.get("big", "")) == "two doors", "pivot hero: two doors when unarmed")
	SimPivot.arm_product(s, "")
	_ok(String(DeskPivot.hero_summary(s).get("big", "")) == "ARMED",
		"pivot hero: ARMED once the flag is set")
	SimPivot.disarm(s)

	var hw := DeskThisWeek.hero_summary(s)
	_ok(String(hw.get("big", "")) == "week 21", "this week hero: the week number")

	var hh := DeskHistory.hero_summary(s)
	_ok(String(hh.get("big", "")) == "20 weeks", "history hero: the row count")

	# ── history: the headline parser and receipt lookup
	_ok(DeskHistory._headline(s, 20) == "The beta window",
		"history: the headline is the event the log remembers")
	_ok(DeskHistory._headline(s, 7) == "a quiet week",
		"history: a week with no record reads as quiet")
	_ok(String(DeskHistory._week_receipts(s, 20).get("verdict", "")) == "fine",
		"history: the receipts land behind their week")

	# ── events: the letters derive from the records, newest first
	var letters := DeskEvents._letters(s)
	_ok(letters.size() >= 5, "events: the records write a real stream")
	var have_clock := false
	var have_apply := false
	var have_ask := false
	var have_rival := false
	for l in letters:
		var key := String((l as Dictionary).get("key", ""))
		if key.begins_with("clock:"):
			have_clock = true
			_ok(int((l as Dictionary).get("wk", 0)) == 24,
				"events: a deadline letter is stamped at its fire week")
		if key.begins_with("apply:"):
			have_apply = true
		if key.begins_with("ask:"):
			have_ask = true
			_ok(bool((l as Dictionary).get("action", false)),
				"events: an ask is an action letter")
		if key.begins_with("rival:"):
			have_rival = true
	_ok(have_clock and have_apply and have_ask and have_rival,
		"events: deadlines, applications, asks and rival moves all land")
	for i in range(letters.size() - 1):
		_ok(int((letters[i] as Dictionary).get("wk", 0))
			>= int((letters[i + 1] as Dictionary).get("wk", 0)),
			"events: newest first, always")
	var hero_events := DeskEvents.hero_summary(s)
	_ok(String(hero_events.get("big", "")).ends_with("unread"),
		"events hero: the unread count leads")
	# read-marks: mark one, the count drops; scratch file cleaned after
	var first_key := String((letters[0] as Dictionary).get("key", ""))
	DeskEvents._mark_read(s, first_key)
	_ok(DeskEvents._read_marks(s).has(first_key),
		"events: a press files a durable read-mark")
	DirAccess.remove_absolute(ProjectSettings.globalize_path(DeskEvents._marks_path(s)))

	# ── the street: word maps and the wire never print a raw float
	_ok(DeskStreetPage._came_at_you(s.rivals[0]),
		"street: a price cut counts as coming at YOU")
	_ok(DeskStreetPage._wire_rows(s).size() >= 1,
		"street: the undercutting rival reaches the wire")

	print("")
	print("%d checks run" % _checks)
	if _failed:
		print("L-COMPANY FAIL")
		quit(1)
		return
	print("L-COMPANY PASS")
	quit(0)
