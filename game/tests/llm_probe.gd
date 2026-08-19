extends SceneTree
## Live probe: verifies the Simulation Engine on the configured model —
## one Tier-2 card generation and one free-move adjudication.
func _init() -> void:
	call_deferred("_go")

func _go() -> void:
	await process_frame
	var llm := LlmClient.new()
	llm.setup(DotEnv.load_env())
	root.add_child(llm)
	if not llm.enabled():
		print("LLM OFF"); quit(1); return
	print("model: %s/%s" % [llm.provider, llm.model])
	var gen := EventGenerator.new(llm)
	root.add_child(gen)
	var state := GameState.new()
	state.company_name = "Driftly"
	state.company_idea = "AI copilot for dog grooming"
	state.biz_what = "Software"
	state.biz_who = "SMB"
	state.funding_id = "bootstrap"
	state.cash = 4200
	state.traction = 9
	state.product = 35
	state.items.append_array(["itm_laptop", "itm_guitar", "itm_houseplant"])
	state.cofounders = [{"role": "Technical", "commitment": "Full-time", "equity": 30.0, "vesting": true}]
	var ev := {"title": "The Groomer Revolt", "body": "Your nine SMB customers found out the AI recommends the same haircut for every dog. They want a refund, together, in a group chat."}
	gen.adjudicate(state, ev, "I get on a video call with all nine groomers, admit the bug, and offer them 3 months free while my technical cofounder ships per-breed models this week.", func(r: Dictionary):
		if r.is_empty():
			print("ADJUDICATE FAIL")
		else:
			print("verdict: ", r.get("verdict"))
			print("narration: ", r.get("narration"))
			print("effects: ", JSON.stringify(r.get("effects")))
		quit(0))
