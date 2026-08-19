class_name EffectOps
extends RefCounted
## Bounded effect-op interpreter (Dossier §6.2). The whitelist + clamps here are
## the LLM safety keystone: authored AND generated cards run through the same ops,
## no op can directly kill, deaths only via armed timebombs or the upkeep loop.
## Cash stakes scale with era (a Series-A office plays for more than a garage);
## feelings (morale/hype) never do.

const BASE_CLAMPS := {
	"cash_delta": [-5000, 5000],
	"product_delta": [-20, 20],
	"traction_delta": [-50, 50],
	"traction_mult": [0.7, 1.5],
	"morale_delta": [-20, 20],
	"hype_delta": [-15, 15],
	"equity_delta": [-40, 0],
	"dilute_pct": [0, 35],
	"salary": [0, 30000],
	"timebomb_weeks": [1, 8],
}
## cash_delta and traction_delta ranges multiply by era scale.
const ERA_CASH_SCALE := {"garage": 1.0, "coworking": 8.0, "office": 30.0, "floor": 120.0, "hq": 500.0}
const ERA_TRACTION_SCALE := {"garage": 1.0, "coworking": 3.0, "office": 10.0, "floor": 40.0, "hq": 100.0}

static func op_whitelist() -> Array[String]:
	return ["cash_delta", "product_delta", "traction_delta", "traction_mult",
		"morale_delta", "hype_delta", "equity_delta", "set_flag", "clear_flag",
		"grant_item", "destroy_item", "arm_timebomb", "weight_future",
		"dilute_pct", "hire", "fire_role", "investor_board_seat", "raise_round",
		"accept_acquisition"]

static func _clamped(op: String, v: float, state: GameState = null) -> float:
	if not BASE_CLAMPS.has(op):
		return v
	var c: Array = BASE_CLAMPS[op]
	var lo := float(c[0])
	var hi := float(c[1])
	if state != null and op == "cash_delta":
		var s := float(ERA_CASH_SCALE.get(state.era, 1.0))
		lo *= s; hi *= s
	elif state != null and op == "traction_delta":
		var s := float(ERA_TRACTION_SCALE.get(state.era, 1.0))
		lo *= s; hi *= s
	return clampf(v, lo, hi)

## Applies one effect dict to state. Returns a human-readable log line.
static func apply(effect: Dictionary, state: GameState) -> String:
	var op := String(effect.get("op", ""))
	match op:
		"cash_delta":
			var v := int(_clamped(op, float(effect.get("v", 0)), state))
			state.cash += v
			return "cash %+d" % v
		"product_delta":
			var v := int(_clamped(op, float(effect.get("v", 0))))
			state.product += v
			return "product %+d" % v
		"traction_delta":
			var v := int(_clamped(op, float(effect.get("v", 0)), state))
			state.traction += v
			return "traction %+d" % v
		"traction_mult":
			var m := _clamped(op, float(effect.get("v", 1.0)))
			state.traction = int(round(state.traction * m))
			return "traction x%.2f" % m
		"morale_delta":
			var v := int(_clamped(op, float(effect.get("v", 0))))
			state.morale += v
			return "morale %+d" % v
		"hype_delta":
			var v := int(_clamped(op, float(effect.get("v", 0))))
			state.hype += v
			return "hype %+d" % v
		"equity_delta":
			var v := _clamped(op, float(effect.get("v", 0)))
			state.founder_pct += v
			return "founder %+.0f%%" % v
		"dilute_pct":
			# a round: EVERYONE dilutes pro-rata (Dossier cap-table rule)
			var x := _clamped(op, float(effect.get("v", 0)))
			state.dilute_all(x)
			return "everyone diluted by %.0f%%" % x
		"raise_round":
			var rd := String(effect.get("v", ""))
			if rd != "" and not state.rounds_raised.has(rd):
				state.rounds_raised.append(rd)
				state.set_flag(rd + "_raised" if not rd.begins_with("series") else rd)
			return "round closed: " + rd
		"investor_board_seat":
			state.board_seats_investor += 1
			if state.board_seats_investor >= state.board_seats_founder:
				state.set_flag("board_control_lost")
			return "an investor takes a board seat"
		"hire":
			if not state.can_hire():
				return "no desk left to hire into"
			var e := {
				"name": String(effect.get("name", "New Hire")),
				"role": String(effect.get("role", "generalist")),
				"salary": int(_clamped("salary", float(effect.get("salary", 1500)))),
				"burnout": 0,
				"quirk": String(effect.get("quirk", "")),
			}
			state.employees.append(e)
			return "hired %s (%s)" % [e["name"], e["role"]]
		"fire_role":
			var role := String(effect.get("v", ""))
			for e in state.employees:
				if String(e.get("role", "")) == role:
					state.employees.erase(e)
					state.morale = clampi(state.morale - 6, 0, 100)
					return "let %s go" % String(e.get("name", role))
			return ""
		"accept_acquisition":
			var mult := clampf(float(effect.get("v", 0.5)), 0.2, 1.0)
			state.exit_value = int(state.valuation() * mult)
			state.set_flag("acquired_exit")
			return "you shook the hand. it's over."
		"set_flag":
			var f := String(effect.get("v", ""))
			state.set_flag(f)
			return "flag: " + f
		"clear_flag":
			state.flags.erase(String(effect.get("v", "")))
			return ""
		"grant_item":
			var id := String(effect.get("v", ""))
			if id != "" and not state.items.has(id):
				state.items.append(id)
			return "got: " + id
		"destroy_item":
			var id := String(effect.get("v", ""))
			state.items.erase(id)
			return "lost: " + id
		"arm_timebomb":
			var weeks := int(_clamped("timebomb_weeks", float(effect.get("weeks", 2))))
			var ev := String(effect.get("event", ""))
			if ev != "":
				state.timebombs.append({"weeks_left": weeks, "event": ev})
			return "…something is ticking"
		"weight_future":
			var ev := String(effect.get("v", ""))
			if ev != "":
				state.future_weights.append(ev)
			return ""
		_:
			push_warning("unknown effect op rejected: " + op)
			return ""
	return ""

static func apply_all(effects: Array, state: GameState) -> Array[String]:
	var log: Array[String] = []
	for e in effects:
		if e is Dictionary:
			var line := apply(e, state)
			if line != "":
				log.append(line)
	state.clampi_meters()
	return log
