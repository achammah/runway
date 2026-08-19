class_name RunRecord
extends RefCounted
## Full run logging: every event, choice and effect. Drives the autopsy screen
## and (later) replay/bug repro. Generated cards are marked with tier.

var seed_value: int = 0
var entries: Array = []   # {week, kind, event_id, tier, title, choice, effects_log}

func log_scramble(banked: Array, left_behind: Array) -> void:
	entries.append({
		"week": 0, "kind": "scramble",
		"title": "The Leap (Act 0)",
		"banked": banked.duplicate(), "left": left_behind.duplicate(),
	})

func log_event(week: int, ev: Dictionary, choice_label: String, effects_log: Array) -> void:
	entries.append({
		"week": week, "kind": "event",
		"event_id": ev.get("id", "generated"),
		"tier": ev.get("tier", "authored"),
		"title": ev.get("title", "?"),
		"choice": choice_label,
		"effects": effects_log.duplicate(),
	})

func log_death(week: int, cause: String) -> void:
	entries.append({"week": week, "kind": "death", "title": cause})

## Trace the causal chain for the autopsy: walk back from death.
func causal_lines() -> Array[String]:
	var lines: Array[String] = []
	for e in entries:
		match e.get("kind", ""):
			"scramble":
				lines.append("Night 0 — grabbed: %s" % ", ".join(e.get("banked", [])))
				if not e.get("left", []).is_empty():
					lines.append("Night 0 — left behind: %s" % ", ".join(e.get("left", [])))
			"event":
				var tag := " *" if e.get("tier", "") == "generated" else ""
				lines.append("Week %d — %s → \"%s\"%s" % [e.get("week", 0), e.get("title", "?"), e.get("choice", "?"), tag])
			"death":
				lines.append("Week %d — DIED: %s" % [e.get("week", 0), e.get("title", "?")])
	return lines
