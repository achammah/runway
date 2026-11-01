class_name SceneRoomPicker
extends RefCounted
## Era × company-state → composed scene id (LANE-WIRING).
##
## The room IS the save file: which painted scene you are looking at must be a
## fact about the company, not a constant. This picker is the single place that
## decision lives, so the room screen, the era-transition beat and any future
## screen all agree.
##
##   var id := SceneRoomPicker.scene_id_for(state)      # "office_starving"
##   if id != room.scene_id: room.load_scene(id)
##
## Every id is fallback-checked against the assets on disk, so a scene the art
## lane has not produced yet degrades to its era's steady scene, never to a
## blank screen.

const ERA_BASE := {
	"garage": "garage",
	"coworking": "coworking_steady",
	"office": "office_steady",
	"floor": "floor_steady",
	"hq": "hq_steady",
}

## Moment scenes are event/flag driven — they beat the mood scene for one beat.
const MOMENT_SCENES := {
	"server_fire": "office_server_fire",
	"layoff": "floor_layoff_day",
	"press_ambush": "press_ambush",
	"yc_interview": "yc_interview",
	"yc_demo_day": "yc_demo_day",
	"demo_day_prep": "coworking_demo_day_prep",
	"launch_day": "launch_day",
	"pivot_night": "pivot_night",
	"hackathon": "hackathon_night",
	"first_customer": "first_customer_call",
	"bell": "nasdaq_bell",
}

## A scene exists if ANY of its renderable pieces is on disk: the inpainted
## base, the flat still, or the first animation frame.
static func has_scene(id: String) -> bool:
	if id == "":
		return false
	var d := "res://assets/scenes/%s" % id
	return ResourceLoader.exists(d + "/room_bg.png") \
		or ResourceLoader.exists(d + "/scene.png") \
		or ResourceLoader.exists(d + "/anim/frame_01.png")

## The mood of the company, in one word: starving | thriving | night | steady.
static func mood_for(state: GameState) -> String:
	if state == null:
		return "steady"
	if state.weeks_in_red > 0:
		return "starving"
	var burn := state.burn_per_week()
	if burn > 0 and state.cash < burn * 3:
		return "starving"
	if state.morale >= 70 and (burn <= 0 or state.revenue_per_week() > 0):
		return "thriving"
	# the garage has a fourth mood: alone at 2am
	if state.era == "garage" and state.cofounders.is_empty() and state.employees.is_empty():
		return "night"
	return "steady"

## THE call the room screen makes every time state changes.
static func scene_id_for(state: GameState) -> String:
	if state == null:
		return "garage"
	var era := String(state.era)
	var mood := mood_for(state)
	var candidates: Array[String] = []
	match mood:
		"starving":
			candidates.append("%s_starving" % era)
		"thriving":
			candidates.append("%s_thriving" % era)
		"night":
			candidates.append("garage_night_solo")
	candidates.append(String(ERA_BASE.get(era, "garage")))
	candidates.append("garage")
	for c in candidates:
		if has_scene(c):
			return c
	return "garage"

## A one-beat override (an event fired, a stage moment): returns "" when the
## moment has no art, so callers can just fall through to scene_id_for().
static func scene_id_for_moment(moment: String) -> String:
	var id := String(MOMENT_SCENES.get(moment, ""))
	return id if has_scene(id) else ""

## Flags the run can raise that deserve their own painted moment this week.
static func moment_from_flags(state: GameState) -> String:
	if state == null:
		return ""
	for pair in [["server_fire", "server_fire"], ["layoff_day", "layoff"],
			["press_ambush", "press_ambush"], ["yc_interview", "yc_interview"],
			["yc_demo_day", "yc_demo_day"], ["launched", "launch_day"]]:
		if state.has_flag(String(pair[0])):
			var id := scene_id_for_moment(String(pair[1]))
			if id != "":
				return id
	return ""
