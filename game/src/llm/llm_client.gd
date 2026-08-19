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
	"required": ["interpreted_as", "reality_check", "narration", "verdict", "effects"],
	"properties": {
		"interpreted_as": {"type": "string", "maxLength": 160},
		"reality_check": {"type": "string", "maxLength": 240},
		# THE WEEK'S SCENE, not a caption: 120-180 words in 3-4 paragraphs, read on its
		# own screen while the art renders. 320 chars truncated it mid-sentence.
		"narration": {"type": "string", "maxLength": 1400},
		"verdict": {"type": "string", "enum": ["brilliant", "fine", "risky", "backfired"]},
		"effects": {
			"type": "array", "minItems": 0, "maxItems": 3,
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
			model = String(env.get("OPENAI_MODEL", "gpt-5-mini"))
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

func enabled() -> bool:
	return provider != "" and api_key != ""

## Fire an async structured request. cb receives the parsed Dictionary ({} on failure).
## Never blocks; each call is independent.
func request_json(system_prompt: String, user_prompt: String, schema: Dictionary, cb: Callable, opts: Dictionary = {}) -> void:
	if not enabled():
		cb.call({})
		return
	var http := HTTPRequest.new()
	add_child(http)
	http.timeout = 35.0
	http.request_completed.connect(_on_completed.bind(http, cb))
	var headers: PackedStringArray
	var body: Dictionary
	if provider == "openai":
		headers = PackedStringArray([
			"Content-Type: application/json",
			"Authorization: Bearer " + api_key,
		])
		body = {
			"model": director_model if opts.get("director", false) else model,
			"messages": [
				{"role": "system", "content": system_prompt},
				{"role": "user", "content": user_prompt},
			],
			"response_format": {
				"type": "json_schema",
				"json_schema": {"name": "structured_reply", "strict": true, "schema": schema},
			},
		}
		if http.request(OPENAI_URL, headers, HTTPClient.METHOD_POST, JSON.stringify(body)) != OK:
			http.queue_free()
			cb.call({})
	elif provider == "anthropic":
		headers = PackedStringArray([
			"Content-Type: application/json",
			"x-api-key: " + api_key,
			"anthropic-version: 2023-06-01",
		])
		body = {
			"model": director_model if opts.get("director", false) else model,
			# a 180-word narration plus the other fields does not fit in 700
			"max_tokens": int(opts.get("max_tokens", 1400)),
			"system": system_prompt,
			"messages": [{"role": "user", "content": user_prompt}],
			"output_config": {"format": {"type": "json_schema", "schema": schema}},
		}
		if http.request(ANTHROPIC_URL, headers, HTTPClient.METHOD_POST, JSON.stringify(body)) != OK:
			http.queue_free()
			cb.call({})

func _on_completed(result: int, code: int, _h: PackedStringArray, body: PackedByteArray, http: HTTPRequest, cb: Callable) -> void:
	http.queue_free()
	if result != HTTPRequest.RESULT_SUCCESS or code < 200 or code >= 300:
		push_warning("LLM request failed (result=%d http=%d)" % [result, code])
		cb.call({})
		return
	var parsed = JSON.parse_string(body.get_string_from_utf8())
	if parsed == null:
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
	cb.call(data if data is Dictionary else {})
