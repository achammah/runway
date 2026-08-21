class_name SaveSystem
extends RefCounted
## C7 — run save + profile save. Versioned JSON at user://; a run can resume
## mid-week; the profile accumulates run history + discovered endings across runs.

const RUN_PATH := "user://run_save.json"
const PROFILE_PATH := "user://profile.json"
const VERSION := 2

# ---------- run save ----------

static func save_run(state: GameState, record: RunRecord) -> void:
	var doc := {
		"version": VERSION,
		"state": {
			"week": state.week, "era": state.era,
			"archetype_id": state.archetype_id, "archetype_name": state.archetype_name,
			"competences": state.competences, "structure_id": state.structure_id,
			"company_name": state.company_name, "company_idea": state.company_idea,
			"founder_name": state.founder_name,
			"biz_what": state.biz_what, "biz_who": state.biz_who,
			"funding_id": state.funding_id, "pivots": state.pivots,
			"last_outcome": state.last_outcome,
			"run_history": state.run_history,
			"sim_seed": state.sim_seed, "theta": state.theta,
			"statuses": state.statuses, "clocks": state.clocks,
			"commitments": state.commitments, "pipeline": state.pipeline,
			"price_mult": state.price_mult, "marketing_budget": state.marketing_budget,
			"budgets": state.budgets, "beliefs": state.beliefs,
			"analytics_level": state.analytics_level, "tech_debt": state.tech_debt,
			"fatigue": state.fatigue, "exhaustion": state.exhaustion,
			"loan_principal": state.loan_principal, "market_trend": state.market_trend,
			"last_growth": state.last_growth, "rivals": state.rivals,
			"investors": state.investors, "xp": state.xp, "level": state.level,
			"traits_tally": state.traits_tally, "story_so_far": state.story_so_far,
			"xp_spent": state.xp_spent, "metric_history": state.metric_history,
			"played_events": state.played_events,
			"weeks_in_red": state.weeks_in_red, "history": state.history,
			"cofounders": state.cofounders, "employees": state.employees,
			"cash": state.cash, "product": state.product, "traction": state.traction,
			"morale": state.morale, "hype": state.hype, "founder_pct": state.founder_pct,
			"items": state.items, "flags": state.flags, "timebombs": state.timebombs,
			"future_weights": state.future_weights,
			"rounds_raised": state.rounds_raised,
			"board_seats_founder": state.board_seats_founder,
			"board_seats_investor": state.board_seats_investor,
			"exit_value": state.exit_value,
			"missed_payrolls": state.missed_payrolls,
		},
		"record": {"seed_value": record.seed_value, "entries": record.entries},
	}
	var f := FileAccess.open(RUN_PATH, FileAccess.WRITE)
	f.store_string(JSON.stringify(doc))
	f.close()

static func has_run() -> bool:
	return FileAccess.file_exists(RUN_PATH)

static func load_run() -> Dictionary:
	## Returns {state: GameState, record: RunRecord} or {} on any mismatch.
	if not has_run():
		return {}
	var parsed = JSON.parse_string(FileAccess.get_file_as_string(RUN_PATH))
	if not (parsed is Dictionary) or int(parsed.get("version", 0)) != VERSION:
		return {}
	var sd: Dictionary = parsed.get("state", {})
	var state := GameState.new()
	for k in sd:
		if k in state:
			state.set(k, sd[k])
	# typed-array repair (JSON gives untyped Arrays)
	var items_t: Array[String] = []
	for v in sd.get("items", []): items_t.append(String(v))
	state.items = items_t
	var flags_t: Array[String] = []
	for v in sd.get("flags", []): flags_t.append(String(v))
	state.flags = flags_t
	var fw_t: Array[String] = []
	for v in sd.get("future_weights", []): fw_t.append(String(v))
	state.future_weights = fw_t
	var rd: Dictionary = parsed.get("record", {})
	var record := RunRecord.new()
	record.seed_value = int(rd.get("seed_value", 0))
	record.entries = rd.get("entries", [])
	return {"state": state, "record": record}

static func clear_run() -> void:
	if has_run():
		DirAccess.remove_absolute(ProjectSettings.globalize_path(RUN_PATH))

# ---------- profile (meta across runs) ----------

static func load_profile() -> Dictionary:
	if not FileAccess.file_exists(PROFILE_PATH):
		return {"version": VERSION, "runs": [], "endings_seen": [], "best_payout": 0}
	var parsed = JSON.parse_string(FileAccess.get_file_as_string(PROFILE_PATH))
	return parsed if parsed is Dictionary else {"version": VERSION, "runs": [], "endings_seen": [], "best_payout": 0}

static func record_run_end(state: GameState, cause: String) -> Dictionary:
	var prof := load_profile()
	var payout := state.payout_today()
	prof["runs"].append({
		"company": state.company_name, "archetype": state.archetype_name,
		"weeks": state.week, "era": state.era, "cause": cause,
		"payout": payout, "founder_pct": state.founder_pct,
		"pivots": state.pivots,
	})
	while (prof["runs"] as Array).size() > 50:
		(prof["runs"] as Array).pop_front()
	if not (prof["endings_seen"] as Array).has(cause):
		prof["endings_seen"].append(cause)
	prof["best_payout"] = maxi(int(prof.get("best_payout", 0)), payout)
	var f := FileAccess.open(PROFILE_PATH, FileAccess.WRITE)
	f.store_string(JSON.stringify(prof))
	f.close()
	clear_run()
	return prof
