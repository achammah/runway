extends SceneTree
## Proves the hard watchdog: a request into a black hole MUST come back as a
## failed callback (exactly once) in ~watchdog seconds — not hang forever the
## way HTTPRequest.timeout has been caught doing on macOS.
##   RUNWAY_LLM_URL=http://10.255.255.1:9/v1 godot --headless -s tests/watchdog_probe.gd

const LlmClientScript := preload("res://src/llm/llm_client.gd")

func _init() -> void:
	var llm := LlmClientScript.new()
	root.add_child.call_deferred(llm)
	await process_frame
	llm.setup({"OPENAI_API_KEY": "sk-probe-not-real", "LLM_PROVIDER": "openai"})
	var t0 := Time.get_ticks_msec()
	var calls := {"n": 0}
	print("PROBE firing into the black hole (watchdog 6s)…")
	llm.request_json("you are a probe", "answer anything",
		{"type": "object", "properties": {"ok": {"type": "boolean"}},
			"required": ["ok"], "additionalProperties": false},
		func(res: Dictionary) -> void:
			calls["n"] += 1
			var dt := (Time.get_ticks_msec() - t0) / 1000.0
			print("PROBE callback #%d after %.1fs (empty=%s)" % [calls["n"], dt, str(res.is_empty())]),
		{"tier": "founding", "watchdog_s": 6.0})
	# wait past the watchdog, then long enough to catch any double-fire
	await create_timer(14.0).timeout
	var dt := (Time.get_ticks_msec() - t0) / 1000.0
	if calls["n"] == 1:
		print("WATCHDOG PROBE PASS — one callback, %.1fs elapsed" % dt)
	else:
		print("WATCHDOG PROBE FAIL — %d callbacks" % calls["n"])
	quit()
