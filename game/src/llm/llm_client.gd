class_name LlmClient
extends Node
## Async LLM client for the Simulation Engine (PRD §7). Provider-agnostic:
## OpenAI (chat completions + json_schema response_format) or Anthropic
## (messages + output_config json_schema structured outputs).
## Key comes from res://.env — the game runs fully without one.
## Callback-based: each request gets its own HTTPRequest, so event prefetch
## and free-move adjudication can run in parallel.

var provider: String = ""
var api_key: String = ""
var model: String = ""
var assess_model: String = ""
var clarify_model: String = ""
var director_model: String = ""   # optionally stronger model for Tier-3 run direction

const OPENAI_URL := "https://api.openai.com/v1/chat/completions"
const ANTHROPIC_URL := "https://api.anthropic.com/v1/messages"

## Schema for generated event cards (shared shape with authored cards).
const EVENT_SCHEMA := {
	"type": "object",
	"additionalProperties": false,
	"required": ["title", "body", "choices"],
	"properties": {
		"title": {"type": "string", "maxLength": 60},
		"body": {"type": "string", "maxLength": 420},
		"choices": {
			"type": "array", "minItems": 2, "maxItems": 4,
			"items": {
				"type": "object",
				"additionalProperties": false,
				"required": ["label", "effects"],
				"properties": {
					"label": {"type": "string", "maxLength": 48},
					"effects": {
						"type": "array", "minItems": 1, "maxItems": 4,
						"items": {
							"type": "object",
							"additionalProperties": false,
							"required": ["op", "v"],
							"properties": {
								"op": {"type": "string", "enum": ["cash_delta", "product_delta", "traction_delta", "morale_delta", "hype_delta", "set_flag"]},
								"v": {"type": ["number", "string"]}
							}
						}
					}
				}
			}
		}
	}
}

## Schema for adjudicating a player's free-form move.
const ADJUDICATE_SCHEMA := {
	"type": "object",
	"additionalProperties": false,
	"required": ["interpreted_as", "reality_check", "narration", "verdict", "effects",
		"headline", "scene", "cast", "roll", "traits", "memory", "journal_note"],
	"properties": {
		"interpreted_as": {"type": "string", "maxLength": 160},
		"reality_check": {"type": "string", "maxLength": 240},
		# THE WEEK'S SCENE, not a caption: 120-180 words in 3-4 paragraphs, read on its
		# own screen while the art renders. 320 chars truncated it mid-sentence.
		"narration": {"type": "string", "maxLength": 2400},
		"verdict": {"type": "string", "enum": ["brilliant", "fine", "risky", "backfired"]},
		# ONE CALL RETURNS THE WHOLE TURN: the text the player reads while the art
		# renders, AND everything needed to build the scene. Splitting these into two
		# calls would put a second round-trip on the critical path of every week.
		"headline": {"type": "string", "maxLength": 90},
		# THE DICE. The client rolls a d20 BEFORE the call and sends it; the DM
		# judges the plan into a DC and a governing stat, and narrates the outcome
		# that roll earned. The roll is shown to the player mid-ceremony, so the
		# fairness is visible: same plan, different die, different week.
		"roll": {
			"type": "object", "additionalProperties": false,
			"required": ["stat", "dc"],
			"properties": {
				"stat": {"type": "string", "enum": ["build", "sell", "raise", "recruit", "grit"]},
				"dc": {"type": "integer", "minimum": 2, "maximum": 19},
			},
		},
		"scene": {
			"type": "object", "additionalProperties": false,
			"required": ["family", "place", "time", "condition", "framing", "novel_place", "beat"],
			"properties": {
				"family": {"type": "string", "enum": ["home_retreat", "scrappy_workspace",
					"legit_workspace", "money", "customer", "institutional", "transit",
					"social", "body_mind", "endings"]},
				"place": {"type": "string", "maxLength": 40},
				"time": {"type": "string", "enum": ["day", "night", "small_hours"]},
				"condition": {"type": "string", "enum": ["thriving", "steady", "in_the_red"]},
				"framing": {"type": "string", "enum": ["wide", "medium"]},
				# filled ONLY when the library will not hold this place. The director
				# generates a new empty room from it, then keeps it.
				"novel_place": {"type": "string", "maxLength": 220},
				"beat": {"type": "string", "maxLength": 160},
			},
		},
		# 1-3 tags from the fixed trait enum — the founder-archetype epilogue
		"traits": {
			"type": "array", "minItems": 0, "maxItems": 3,
			"items": {"type": "string", "enum": ["long_term", "short_term",
				"risk_taker", "risk_averse", "data_driven", "intuition_driven",
				"quality_focused", "speed_focused", "hands_on", "delegator",
				"collaborative", "independent", "diplomatic", "confrontational"]}
		},
		# THE COMPACTED MEMORY: the DM's own ≤120-word third-person summary of
		# the run so far, replacing the previous one. The engine hard-caps it.
		"memory": {"type": "string", "maxLength": 1200},
		# THE LOG LINE: 1-2 sentences in the FOUNDER'S OWN first-person hand for
		# the journal page — different words than the narration, the way a diary
		# entry differs from the chapter it summarizes.
		"journal_note": {"type": "string", "maxLength": 220},
		"cast": {
			"type": "array", "minItems": 0, "maxItems": 5,
			"items": {
				"type": "object", "additionalProperties": false,
				"required": ["who", "mood", "doing"],
				"properties": {
					"who": {"type": "string", "enum": ["founder", "sales", "business", "tech",
						"hustler", "idea_friend"]},
					"mood": {"type": "string", "enum": ["fine", "burnt", "gone"]},
					"doing": {"type": "string", "maxLength": 70},
				},
			},
		},
		"effects": {
			"type": "array", "minItems": 0, "maxItems": 4,
			"items": {
				"type": "object",
				"additionalProperties": false,
				# WHY IS NOT OPTIONAL: every delta names its in-world cause, and the
				# journal prints it ("+$1,200 — the pilot invoice cleared").
				"required": ["op", "v", "why", "weeks", "cat"],
				"properties": {
					"op": {"type": "string", "enum": ["cash_delta", "product_delta",
						"traction_delta", "morale_delta", "hype_delta", "set_flag",
						"status", "clock", "set_price", "set_marketing", "hire", "take_loan",
						"spend", "set_budget"]},
					"v": {"type": ["number", "string"]},
					"why": {"type": "string", "maxLength": 90},
					# status: duration · clock: weeks until it fires · all other ops: 1
					"weeks": {"type": "integer", "minimum": 1, "maximum": 12},
					# spend/set_budget only: where the money goes. "" for every other op.
					"cat": {"type": "string", "enum": ["", "marketing", "sales", "care", "rnd", "one_off"]}
				}
			}
		}
	}
}

## Schema for the clarify pre-pass (luna): one reluctant follow-up question.
const CLARIFY_SCHEMA := {
	"type": "object", "additionalProperties": false,
	"required": ["needs_clarification", "question", "kind"],
	"properties": {
		"needs_clarification": {"type": "boolean"},
		"question": {"type": "string", "maxLength": 90},
		"kind": {"type": "string", "enum": ["amount", "target", "resource", "price", "other"]},
	},
}

## Schema for run-start world generation: the bible born from the pitch.
const WORLD_SCHEMA := {
	"type": "object", "additionalProperties": false,
	"required": ["market", "investors", "rivals"],
	"properties": {
		"market": {
			"type": "object", "additionalProperties": false,
			"required": ["tam_buyers", "customer_patience_weeks", "one_liner"],
			"properties": {
				"tam_buyers": {"type": "integer", "minimum": 2000, "maximum": 5000000},
				"customer_patience_weeks": {"type": "integer", "minimum": 6, "maximum": 200},
				"one_liner": {"type": "string", "maxLength": 140},
			},
		},
		"investors": {
			"type": "array", "minItems": 3, "maxItems": 3,
			"items": {
				"type": "object", "additionalProperties": false,
				"required": ["name", "archetype", "thesis", "trait", "bond", "flaw", "secret"],
				"properties": {
					"name": {"type": "string", "maxLength": 40},
					"archetype": {"type": "string", "enum": ["the momentum fund",
						"the contrarian angel", "the operator VC", "the shark", "the thesis tourist"]},
					"thesis": {"type": "string", "maxLength": 200},
					"trait": {"type": "string", "maxLength": 80},
					"bond": {"type": "string", "maxLength": 90},
					"flaw": {"type": "string", "maxLength": 80},
					"secret": {"type": "string", "maxLength": 90},
				},
			},
		},
		"rivals": {
			"type": "array", "minItems": 2, "maxItems": 2,
			"items": {
				"type": "object", "additionalProperties": false,
				"required": ["name", "what_they_do", "strength", "tactics"],
				"properties": {
					"name": {"type": "string", "maxLength": 30},
					"what_they_do": {"type": "string", "maxLength": 140},
					"strength": {"type": "string", "enum": ["struggling", "scrappy", "strong", "dominant"]},
					"tactics": {"type": "array", "minItems": 3, "maxItems": 3,
						"items": {"type": "string", "maxLength": 60}},
				},
			},
		},
	},
}

## Schema for the Tier-3 run director: the run's narrative arcs.
const ARC_SCHEMA := {
	"type": "object",
	"additionalProperties": false,
	"required": ["arcs"],
	"properties": {
		"arcs": {
			"type": "array", "minItems": 1, "maxItems": 3,
			"items": {
				"type": "object",
				"additionalProperties": false,
				"required": ["arc_id", "kind", "premise", "actors", "beats", "escalation_rule"],
				"properties": {
					"arc_id": {"type": "string", "maxLength": 40},
					"kind": {"type": "string", "enum": ["rival", "press", "cofounder", "investor", "customer"]},
					"premise": {"type": "string", "maxLength": 240},
					"actors": {"type": "array", "minItems": 1, "maxItems": 3, "items": {"type": "string", "maxLength": 60}},
					"beats": {
						"type": "array", "minItems": 1, "maxItems": 5,
						"items": {
							"type": "object",
							"additionalProperties": false,
							"required": ["era", "directive"],
							"properties": {
								"era": {"type": "string", "enum": ["garage", "coworking", "office", "floor", "hq"]},
								"directive": {"type": "string", "maxLength": 200}
							}
						}
					},
					"escalation_rule": {"type": "string", "maxLength": 160}
				}
			}
		}
	}
}

func setup(env: Dictionary) -> void:
	var openai_key := String(env.get("OPENAI_API_KEY", ""))
	var anthropic_key := String(env.get("ANTHROPIC_API_KEY", ""))
	provider = String(env.get("LLM_PROVIDER", ""))
	if provider == "":
		if openai_key != "":
			provider = "openai"
		elif anthropic_key != "":
			provider = "anthropic"
	match provider:
		"openai":
			api_key = openai_key
			# Default measured head-to-head on this exact prompt and schema, twice each.
			# On a routine turn: luna 5.2s, terra 6-7s. On the hard retreat-ladder turn:
			# luna 7.1s, terra 12.4s — and luna also caught the burnt cofounder and the
			# missed_payroll flag that terra missed there. Both held the ladder itself.
			# The adjudication gates the whole week, so the faster equal-quality model wins.
			model = String(env.get("OPENAI_MODEL", "gpt-5.6-luna"))
			# THE TWO-TIER SPLIT (owner): the ASSESSMENT (adjudicator) runs terra —
			# the deepest judgment in the game; the CLARIFY pre-pass runs luna —
			# one cheap question, speed is the feature.
			assess_model = String(env.get("OPENAI_ASSESS_MODEL", "gpt-5.6-terra"))
			clarify_model = String(env.get("OPENAI_CLARIFY_MODEL", "gpt-5.6-luna"))
		"anthropic":
			api_key = anthropic_key
			model = String(env.get("ANTHROPIC_MODEL", "claude-haiku-4-5-20251001"))
	match provider:
		"openai":
			director_model = String(env.get("OPENAI_DIRECTOR_MODEL", model))
		"anthropic":
			director_model = String(env.get("ANTHROPIC_DIRECTOR_MODEL", model))
	if director_model == "":
		director_model = model
	if api_key == "":
		provider = ""

func _model_for(opts: Dictionary) -> String:
	if opts.get("director", false):
		return director_model
	match String(opts.get("tier", "")):
		"assess":
			return assess_model if assess_model != "" else model
		"clarify", "founding":
			# the founding is pure prose — the fast writer model, on the fast
			# lane: day one must not take a minute
			return clarify_model if clarify_model != "" else model
	return model

## assessment = terra FAST (deep judgment, still on the week's critical path);
## clarify = luna NORMAL (cheap, one question, no need for the fast lane).
func _service_tier_for(opts: Dictionary) -> String:
	if String(opts.get("tier", "")) == "clarify":
		return "default"
	if OS.has_environment("RUNWAY_LLM_TIER"):
		return OS.get_environment("RUNWAY_LLM_TIER")
	return "fast"

func enabled() -> bool:
	return provider != "" and api_key != ""

## Fire an async structured request. cb receives the parsed Dictionary ({} on failure).
## Never blocks; each call is independent.
func request_json(system_prompt: String, user_prompt: String, schema: Dictionary, cb: Callable, opts: Dictionary = {}) -> void:
	if not enabled():
		if cb.is_valid():
			cb.call({})
		return
	var http := HTTPRequest.new()
	add_child(http)
	# 35s cut off real terra founding calls in the shipped app (the book showed
	# an empty entry and settle-in paid the whole call again). 90s is the cap;
	# the beat/curtain narrate the wait.
	http.timeout = 90.0
	http.request_completed.connect(_on_completed.bind(http, cb))
	var headers: PackedStringArray
	var body: Dictionary
	if provider == "openai":
		headers = PackedStringArray([
			"Content-Type: application/json",
			"Authorization: Bearer " + api_key,
		])
		body = {
			"model": _model_for(opts),
			"messages": [
				{"role": "system", "content": system_prompt},
				{"role": "user", "content": user_prompt},
			],
			"response_format": {
				"type": "json_schema",
				"json_schema": {"name": "structured_reply", "strict": true, "schema": schema},
			},
			# FAST MODE. The adjudication sits on the critical path of every week: the
			# player cannot start reading until it returns, and the scene cannot start
			# rendering until it names the place. Measured on this exact prompt and
			# schema: 13.5s and 12.7s standard, 7.0s and 6.0s fast — about half, with
			# no change in output (same place chosen, same narration length).
			# It costs a per-token premium. Set RUNWAY_LLM_TIER=standard to opt out.
			"service_tier": _service_tier_for(opts),
		}
		if http.request(OPENAI_URL, headers, HTTPClient.METHOD_POST, JSON.stringify(body)) != OK:
			http.queue_free()
			if cb.is_valid():
				cb.call({})
	elif provider == "anthropic":
		headers = PackedStringArray([
			"Content-Type: application/json",
			"x-api-key: " + api_key,
			"anthropic-version: 2023-06-01",
		])
		body = {
			"model": _model_for(opts),
			# a 180-word narration plus the other fields does not fit in 700
			"max_tokens": int(opts.get("max_tokens", 1400)),
			"system": system_prompt,
			"messages": [{"role": "user", "content": user_prompt}],
			"output_config": {"format": {"type": "json_schema", "schema": schema}},
		}
		if http.request(ANTHROPIC_URL, headers, HTTPClient.METHOD_POST, JSON.stringify(body)) != OK:
			http.queue_free()
			if cb.is_valid():
				cb.call({})

func _on_completed(result: int, code: int, _h: PackedStringArray, body: PackedByteArray, http: HTTPRequest, cb: Callable) -> void:
	http.queue_free()
	if result != HTTPRequest.RESULT_SUCCESS or code < 200 or code >= 300:
		# print, not push_warning: release builds swallow warnings, and this
		# line is the only witness a shipped session gets
		print("LLM request FAILED (result=%d http=%d): %s" % [result, code,
			body.get_string_from_utf8().left(300)])
		if cb.is_valid():
			cb.call({})
		return
	var parsed = JSON.parse_string(body.get_string_from_utf8())
	if parsed == null:
		if cb.is_valid():
			cb.call({})
		return
	var text := ""
	if provider == "openai":
		var choices: Array = parsed.get("choices", [])
		if not choices.is_empty():
			text = String(choices[0].get("message", {}).get("content", ""))
	else:
		for block in parsed.get("content", []):
			if block.get("type", "") == "text":
				text += String(block.get("text", ""))
	var data = JSON.parse_string(text)
	if cb.is_valid():
		cb.call(data if data is Dictionary else {})
