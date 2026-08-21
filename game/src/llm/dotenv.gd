class_name DotEnv
extends RefCounted
## Minimal .env loader. Looks for res://.env (project dir). Lines: KEY=value, # comments.

## user://keys.env (written by the in-game keys screen) OVERRIDES res://.env —
## a shipped build has no project .env, and the player's keys live in user data.
static func load_env() -> Dictionary:
	var env := _parse("res://.env")
	var user_env := _parse("user://keys.env")
	for k in user_env:
		env[k] = user_env[k]
	return env

static func _parse(path: String) -> Dictionary:
	var env := {}
	if not FileAccess.file_exists(path):
		return env
	var txt := FileAccess.get_file_as_string(path)
	for raw_line in txt.split("\n"):
		var line := raw_line.strip_edges()
		if line == "" or line.begins_with("#"):
			continue
		var eq := line.find("=")
		if eq <= 0:
			continue
		var key := line.substr(0, eq).strip_edges()
		var val := line.substr(eq + 1).strip_edges()
		if val.begins_with("\"") and val.ends_with("\"") and val.length() >= 2:
			val = val.substr(1, val.length() - 2)
		env[key] = val
	return env
