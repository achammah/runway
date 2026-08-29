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
						"status", "clock", "set_price", "price_offer", "set_marketing", "hire", "take_loan",
						"spend", "set_budget", "push_lead",
							"open_site", "close_site", "reassign_employee", "move_machine",
							"tag_offer", "tag_spend_line", "refinance_note", "fire_account",
							"retire_product", "pivot_audience", "pivot_product",
							"pitch_investor", "sign_instrument", "send_offer", "set_relief",
							"draft_offer"]},
					"v": {"type": ["number", "string"]},
					"why": {"type": "string", "maxLength": 90},
					# status: duration · clock: weeks until it fires · all other ops: 1
					"weeks": {"type": "integer", "minimum": 1, "maximum": 12},
					# WHERE IT LANDS — a free string, not an enum, because four ops
					# now share this field and only one has a closed vocabulary
					# (docs/design/00-spine.md §7):
					#   spend       a short label for the outlay
					#   set_budget  a lever: marketing, sales, care, rnd, office
					#   price_offer the offer's name, matched fuzzily
					#   push_lead   the lead's name, matched fuzzily
					# "" for every other op. The executor guards each case, so an
					# unrecognised value degrades to a sane lane, never a crash.
					"cat": {"type": "string", "maxLength": 40}
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

## Schema for pricing a founder-written offer: the street answers with terms.
## The intake's follow-up round: the street either understands the offer or
## asks up to 3 multiple-choice questions about the FACTS that set its terms.
const OFFER_CLARIFY_SCHEMA := {
	"type": "object", "additionalProperties": false,
	"required": ["ready", "questions"],
	"properties": {
		"ready": {"type": "boolean"},
		"questions": {"type": "array", "minItems": 0, "maxItems": 3,
			"items": {"type": "object", "additionalProperties": false,
				"required": ["q", "options"],
				"properties": {
					"q": {"type": "string", "maxLength": 120},
					"options": {"type": "array", "minItems": 2, "maxItems": 4,
						"items": {"type": "string", "maxLength": 40}}}}},
	},
}

const OFFER_SCHEMA := {
	"type": "object", "additionalProperties": false,
	"required": ["name", "desc", "unit", "fair_price", "elasticity", "weight",
		"street_read", "capacity_per_unit", "variable_costs", "fixed_costs_wk"],
	"properties": {
		"name": {"type": "string", "maxLength": 40},
		"desc": {"type": "string", "maxLength": 110},
		"street_read": {"type": "string", "maxLength": 140},
		"capacity_per_unit": {"type": "number", "minimum": 0.1, "maximum": 40},
		"unit": {"type": "string", "enum": ["per session", "per month", "per order",
			"per unit", "per year", "per hour", "per package", "per kit"]},
		"fair_price": {"type": "number", "minimum": 1, "maximum": 50000},
		"elasticity": {"type": "number", "minimum": 0.5, "maximum": 3.0},
		"weight": {"type": "number", "minimum": 0.2, "maximum": 3.0},
		"variable_costs": {"type": "array", "minItems": 1, "maxItems": 4,
			"items": {"type": "object", "additionalProperties": false,
				"required": ["label", "amount"],
				"properties": {"label": {"type": "string", "maxLength": 24},
					"amount": {"type": "number", "minimum": 0, "maximum": 25000}}}},
		"fixed_costs_wk": {"type": "array", "minItems": 0, "maxItems": 3,
			"items": {"type": "object", "additionalProperties": false,
				"required": ["label", "amount"],
				"properties": {"label": {"type": "string", "maxLength": 24},
					"amount": {"type": "number", "minimum": 0, "maximum": 5000}}}},
	},
}

## Schema for the one batch candidate-dressing call (02 §8.1): the engine
## already decided every number; the model only writes the people.
const CANDIDATES_SCHEMA := {
	"type": "object", "additionalProperties": false, "required": ["candidates"],
	"properties": {"candidates": {"type": "array", "minItems": 1, "maxItems": 10,
		"items": {"type": "object", "additionalProperties": false,
			"required": ["name", "quirk", "one_liner"],
			"properties": {
				"name": {"type": "string", "maxLength": 40},
				"quirk": {"type": "string", "maxLength": 60},
				"one_liner": {"type": "string", "maxLength": 90},
			}}}},
}

## Schema for the one batch lead-naming call (05 §10): the engine already
## decided seats, stages and spawn counts; the model only names companies.
const LEAD_SCHEMA := {
	"type": "object", "additionalProperties": false, "required": ["leads"],
	"properties": {"leads": {"type": "array", "minItems": 1, "maxItems": 3,
		"items": {"type": "object", "additionalProperties": false,
			"required": ["name", "one_liner"],
			"properties": {
				"name": {"type": "string", "maxLength": 30},
				"one_liner": {"type": "string", "maxLength": 90}}}}},
}

## Schema for the one batch bet-dressing call (07 §10): the engine priced
## every card; the model only writes the words and picks a rung.
const BETS_SCHEMA := {
	"type": "object", "additionalProperties": false, "required": ["bets"],
	"properties": {"bets": {"type": "array", "minItems": 1, "maxItems": 3,
		"items": {"type": "object", "additionalProperties": false,
			"required": ["name", "desc", "kind", "ambition"],
			"properties": {
				"name": {"type": "string", "maxLength": 28},
				"desc": {"type": "string", "maxLength": 90},
				"kind": {"type": "string", "enum": ["quality", "retention", "reach", "platform"]},
				"ambition": {"type": "integer", "minimum": 1, "maximum": 3},
			}}}},
}

## Schema for run-start world generation: the bible born from the pitch.
## DAG2 (DECISIONS.md): the SAME one call also births the binder's generated
## content — identity, the four growth plots, the works vocabulary, the org
## spend book, THE PRICE BOOK and the birth features. The LLM proposes inside
## the stated bands; WorldGen.apply_birth clamps again engine-side (the law).
const WORLD_SCHEMA := {
	"type": "object", "additionalProperties": false,
	"required": ["market", "investors", "rivals", "identity", "growth_topics",
		"works_terms", "spend_book", "price_book", "birth_features"],
	"properties": {
		# WHO WE ARE, in the world's dry words — the product desk's header.
		"identity": {
			"type": "object", "additionalProperties": false,
			"required": ["one_liner", "who_for"],
			"properties": {
				"one_liner": {"type": "string", "maxLength": 140},
				"who_for": {"type": "string", "maxLength": 80},
			},
		},
		# THE MARKET GARDEN's four plots. Dressing ONLY: each channel keeps its
		# engine character verbatim in whatever world the model invents —
		# ads instant-and-saturating, content a compounding stock that rots
		# starved, referrals an NPS-gated multiplier, outbound quota knocking.
		"growth_topics": {
			"type": "object", "additionalProperties": false,
			"required": ["ads", "content", "referrals", "outbound"],
			"properties": {
				"ads": {"type": "object", "additionalProperties": false,
					"required": ["name", "one_line", "buys", "why"],
					"properties": {"name": {"type": "string", "maxLength": 28},
						"one_line": {"type": "string", "maxLength": 110},
						"buys": {"type": "string", "maxLength": 120},
						"why": {"type": "string", "maxLength": 140}}},
				"content": {"type": "object", "additionalProperties": false,
					"required": ["name", "one_line", "buys", "why"],
					"properties": {"name": {"type": "string", "maxLength": 28},
						"one_line": {"type": "string", "maxLength": 110},
						"buys": {"type": "string", "maxLength": 120},
						"why": {"type": "string", "maxLength": 140}}},
				"referrals": {"type": "object", "additionalProperties": false,
					"required": ["name", "one_line", "buys", "why"],
					"properties": {"name": {"type": "string", "maxLength": 28},
						"one_line": {"type": "string", "maxLength": 110},
						"buys": {"type": "string", "maxLength": 120},
						"why": {"type": "string", "maxLength": 140}}},
				"outbound": {"type": "object", "additionalProperties": false,
					"required": ["name", "one_line", "buys", "why"],
					"properties": {"name": {"type": "string", "maxLength": 28},
						"one_line": {"type": "string", "maxLength": 110},
						"buys": {"type": "string", "maxLength": 120},
						"why": {"type": "string", "maxLength": 140}}},
			},
		},
		# THE WORKS' native vocabulary: what one sold thing is called, what the
		# capacity is made of, what the overflow relief valve is called.
		"works_terms": {
			"type": "object", "additionalProperties": false,
			"required": ["unit_word", "capacity_word", "relief_word"],
			"properties": {
				"unit_word": {"type": "string", "maxLength": 16},
				"capacity_word": {"type": "string", "maxLength": 28},
				"relief_word": {"type": "string", "maxLength": 28},
			},
		},
		# THE ORG SPEND BOOK: 6-10 lines fitted to THIS business. The model
		# invents rows, never math — bucket ∈ the four engine levers, and the
		# engine's lever value stays the SUM of its lines.
		"spend_book": {
			"type": "array", "minItems": 6, "maxItems": 10,
			"items": {
				"type": "object", "additionalProperties": false,
				"required": ["name", "buys", "amt", "bucket", "contract_notice"],
				"properties": {
					"name": {"type": "string", "maxLength": 28},
					"buys": {"type": "string", "maxLength": 60},
					"amt": {"type": "number", "minimum": 0, "maximum": 400},
					"bucket": {"type": "string", "enum": ["sales", "care", "rnd", "office"]},
					"contract_notice": {"type": "integer", "minimum": 0, "maximum": 12},
				},
			},
		},
		# THE PRICE BOOK: the whole structural price schedule, visible from
		# week 1 so expansion can be planned, not discovered. Bands here are
		# the engine's; WorldGen clamps again on apply.
		"price_book": {
			"type": "object", "additionalProperties": false,
			"required": ["open_site_pack", "relocation_fee", "machine_shipping",
				"lease_break_weeks", "contract_notice_wks", "refinance_break_fee",
				"freelance_rate", "subcontract_rate", "account_fire_penalty"],
			"properties": {
				"open_site_pack": {"type": "number", "minimum": 6000, "maximum": 40000},
				"relocation_fee": {"type": "number", "minimum": 100, "maximum": 1500},
				"machine_shipping": {"type": "number", "minimum": 150, "maximum": 4000},
				"lease_break_weeks": {"type": "integer", "minimum": 4, "maximum": 16},
				"contract_notice_wks": {"type": "integer", "minimum": 2, "maximum": 12},
				"refinance_break_fee": {"type": "number", "minimum": 100, "maximum": 2000},
				"freelance_rate": {"type": "number", "minimum": 15, "maximum": 300},
				"subcontract_rate": {"type": "number", "minimum": 10, "maximum": 250},
				"account_fire_penalty": {"type": "number", "minimum": 200, "maximum": 5000},
			},
		},
		# BIRTH FEATURES: what the thing is made of on day one. 3-6 rows for
		# state.features; jobs are the fixed contribution classes.
		"birth_features": {
			"type": "array", "minItems": 3, "maxItems": 6,
			"items": {
				"type": "object", "additionalProperties": false,
				"required": ["name", "job", "keep_wk", "unit_cost_add"],
				"properties": {
					"name": {"type": "string", "maxLength": 28},
					"job": {"type": "string", "enum": ["pull", "keep", "charge", "plumbing"]},
					"keep_wk": {"type": "number", "minimum": 0, "maximum": 150},
					"unit_cost_add": {"type": "number", "minimum": 0, "maximum": 40},
				},
			},
		},
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
	# TWO CLOCKS ON EVERY REQUEST. The soft one is HTTPRequest.timeout — but
	# that clock has been caught SLEEPING through wedged sockets on macOS (the
	# render ladder learned it first; then a founding hung >90s and the book
	# waited forever on an entry that was never coming). So a hard scene-tree
	# watchdog races every request: if it wins, the request is cancelled and
	# the caller's failure path (retry, fallback) actually runs.
	# founding/clarify are prose on the fast lane — a wedged attempt must die
	# fast enough that the retry still lands inside the player's wait.
	var tier := String(opts.get("tier", ""))
	var wd := float(opts.get("watchdog_s",
			50.0 if tier in ["clarify", "founding"] else 100.0))
	# 35s once cut off real terra founding calls (the book showed an empty
	# entry and settle-in paid the whole call again) — the soft cap stays
	# generous, just under the hard one.
	http.timeout = wd - 5.0
	if OS.get_environment("RUNWAY_LLM_NO_SOFT") == "1":
		http.timeout = 0.0   # probe-only: simulate the soft clock sleeping
	var fired := {"v": false}
	http.request_completed.connect(_on_completed.bind(http, cb, fired))
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
		# fault injection: RUNWAY_LLM_URL points the client at a black hole so
		# the watchdog/retry ladder can be proven without touching the network
		var url := OS.get_environment("RUNWAY_LLM_URL") \
				if OS.has_environment("RUNWAY_LLM_URL") else OPENAI_URL
		if http.request(url, headers, HTTPClient.METHOD_POST, JSON.stringify(body)) != OK:
			http.queue_free()
			if cb.is_valid():
				cb.call({})
		else:
			_watchdog(http, cb, fired, wd)
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
		else:
			_watchdog(http, cb, fired, wd)

## The hard clock. Exactly one of (_on_completed, _watchdog) may answer the
## caller — the `fired` guard is the referee.
func _watchdog(http: HTTPRequest, cb: Callable, fired: Dictionary, seconds: float) -> void:
	await get_tree().create_timer(seconds).timeout
	if fired["v"] or not is_instance_valid(http):
		return
	fired["v"] = true
	print("LLM WATCHDOG fired after %.0fs — cancelling the wedged request" % seconds)
	http.cancel_request()
	http.queue_free()
	if cb.is_valid():
		cb.call({})

func _on_completed(result: int, code: int, _h: PackedStringArray, body: PackedByteArray, http: HTTPRequest, cb: Callable, fired: Dictionary) -> void:
	if fired["v"]:
		http.queue_free()
		return
	fired["v"] = true
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
