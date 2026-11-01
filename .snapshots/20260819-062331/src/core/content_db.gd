class_name ContentDb
extends RefCounted
## JSON content pipeline: loads items and event cards, answers eligibility queries.

var items: Dictionary = {}    # id -> item def
var events: Dictionary = {}   # id -> event def

func load_all() -> void:
	items.clear()
	events.clear()
	var idata := _load_json("res://data/items.json")
	for it in idata.get("items", []):
		items[it["id"]] = it
	var dir := DirAccess.open("res://data/events")
	if dir:
		for f in dir.get_files():
			if f.ends_with(".json"):
				var edata := _load_json("res://data/events/" + f)
				for ev in edata.get("events", []):
					events[ev["id"]] = ev

func _load_json(path: String) -> Dictionary:
	if not FileAccess.file_exists(path):
		push_warning("content file missing: " + path)
		return {}
	var txt := FileAccess.get_file_as_string(path)
	var parsed = JSON.parse_string(txt)
	if parsed == null or not (parsed is Dictionary):
		push_error("content file failed to parse: " + path)
		return {}
	return parsed

## Events whose requirements match the current state (draw pool).
func eligible_events(state: GameState) -> Array:
	var pool: Array = []
	for ev in events.values():
		if float(ev.get("weight", 1)) <= 0.0:
			continue  # weight 0 = only reachable via timebomb/weight_future
		if not ev.get("era", ["garage"]).has(state.era):
			continue
		if _requires_ok(ev.get("requires", {}), state):
			pool.append(ev)
	return pool

func _requires_ok(req: Dictionary, state: GameState) -> bool:
	if req.has("items_any"):
		var any_hit := false
		for id in req["items_any"]:
			if state.has_item(id):
				any_hit = true
		if not any_hit:
			return false
	if req.has("flags_all"):
		for f in req["flags_all"]:
			if not state.has_flag(f):
				return false
	if req.has("flags_none"):
		for f in req["flags_none"]:
			if state.has_flag(f):
				return false
	if req.has("cash_lte") and state.cash > int(req["cash_lte"]):
		return false
	if req.has("cash_gte") and state.cash < int(req["cash_gte"]):
		return false
	if req.has("product_gte") and state.product < int(req["product_gte"]):
		return false
	if req.has("morale_lte") and state.morale > int(req["morale_lte"]):
		return false
	if req.has("traction_gte") and state.traction < int(req["traction_gte"]):
		return false
	if req.has("hype_gte") and state.hype < int(req["hype_gte"]):
		return false
	if req.has("week_gte") and state.week < int(req["week_gte"]):
		return false
	if req.has("staff_gte") and state.employees.size() < int(req["staff_gte"]):
		return false
	return true
