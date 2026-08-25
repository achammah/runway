class_name SaveSystem
extends RefCounted
## C7 — run save + profile save. Versioned JSON at user://; a run can resume
## mid-week; the profile accumulates run history + discovered endings across runs.

const RUN_PATH := "user://run_save.json"   # legacy single slot — migrates to slot 1
## THREE SLOTS (owner: saved games with last-played times on the title).
## active_slot is set by the title screen before a run starts or resumes.
const SLOTS := 3
static var active_slot: int = 1

static func slot_path(slot: int) -> String:
	return "user://run_slot_%d.json" % clampi(slot, 1, SLOTS)

## Legacy migration: an old run_save.json becomes slot 1 the first time
## anyone asks about slots.
static func _migrate_legacy() -> void:
	if FileAccess.file_exists(RUN_PATH) and not FileAccess.file_exists(slot_path(1)):
		var doc := FileAccess.get_file_as_string(RUN_PATH)
		var f := FileAccess.open(slot_path(1), FileAccess.WRITE)
		f.store_string(doc)
		f.close()
		DirAccess.remove_absolute(RUN_PATH)

## One row per slot for the title screen: exists, company, week, last-played.
static func list_slots() -> Array:
	_migrate_legacy()
	var out: Array = []
	for i in range(1, SLOTS + 1):
		var p := slot_path(i)
		if not FileAccess.file_exists(p):
			out.append({"slot": i, "exists": false})
			continue
		var parsed = JSON.parse_string(FileAccess.get_file_as_string(p))
		if not (parsed is Dictionary):
			out.append({"slot": i, "exists": false})
			continue
		var meta: Dictionary = (parsed as Dictionary).get("meta", {})
		var sd: Dictionary = (parsed as Dictionary).get("state", {})
		out.append({"slot": i, "exists": true,
			"company": String(meta.get("company", sd.get("company_name", "a company"))),
			"founder": String(meta.get("founder", sd.get("founder_name", ""))),
			"week": int(meta.get("week", sd.get("week", 0))),
			"ts": int(meta.get("ts", 0))})
	return out
const PROFILE_PATH := "user://profile.json"
const VERSION := 2

# ---------- run save ----------

static func save_run(state: GameState, record: RunRecord) -> void:
	var doc := {
		"version": VERSION,
		"meta": {"company": state.company_name, "founder": state.founder_name,
			"week": state.week, "ts": int(Time.get_unix_time_from_system())},
		"state": state_to_dict(state),
		"record": {"seed_value": record.seed_value, "entries": record.entries},
	}
	var f := FileAccess.open(slot_path(active_slot), FileAccess.WRITE)
	f.store_string(JSON.stringify(doc))
	f.close()

## THE ONE WRITER, pure and file-free — the mirror of state_from_dict. Keeping
## it a plain function means the twin suite can pin a full save/load round-trip
## without touching a real slot: a field left out of this dict is a field that
## silently stops being saved, which is exactly the class of bug that cost the
## learning curve its memory.
static func state_to_dict(state: GameState) -> Dictionary:
	return {
			"week": state.week, "era": state.era,
			"archetype_id": state.archetype_id, "archetype_name": state.archetype_name,
			"competences": state.competences, "traits": state.traits,
			"structure_id": state.structure_id,
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
			"budgets": state.budgets, "beliefs": state.beliefs, "offers": state.offers,
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
			# ── SUBSYSTEM STATE (docs/design/00-spine.md §8) — additive keys, so
			# VERSION stays 2: an old save simply lacks them and the loader
			# leaves each at the default declared in game_state.gd.
			"served_total": state.served_total,
			"open_roles": state.open_roles, "applicants": state.applicants,
			"recruiters": state.recruiters, "severance_due": state.severance_due,
			"content_equity": state.content_equity,
			"leads": state.leads, "logos": state.logos,
			"pipe_units": state.pipe_units, "pipe_churn_acc": state.pipe_churn_acc,
			"pipe_stats": state.pipe_stats,
			"loans": state.loans, "tax_loss_carry": state.tax_loss_carry,
			"last_round_amount": state.last_round_amount,
			"receivables": state.receivables,
			"bets": state.bets, "platform_level": state.platform_level,
			"board": state.board, "mna": state.mna,
			"mna_last_week": state.mna_last_week,
			"option_pool_pct": state.option_pool_pct,
			"founder_banked": state.founder_banked,
			"macro_season": state.macro_season,
			"hardware": state.hardware,
	}

static func has_run() -> bool:
	_migrate_legacy()
	return FileAccess.file_exists(slot_path(active_slot))

static func load_run() -> Dictionary:
	## Returns {state: GameState, record: RunRecord} or {} on any mismatch.
	if not has_run():
		return {}
	var parsed = JSON.parse_string(FileAccess.get_file_as_string(slot_path(active_slot)))
	if not (parsed is Dictionary) or int(parsed.get("version", 0)) != VERSION:
		return {}
	var state := state_from_dict(parsed.get("state", {}))
	var rd: Dictionary = parsed.get("record", {})
	var record := RunRecord.new()
	record.seed_value = int(rd.get("seed_value", 0))
	record.entries = rd.get("entries", [])
	return {"state": state, "record": record}

## THE ONE LOADER. `load_run` and the frozen-save fixture test both come through
## here, so a save-compat check exercises the code the game actually runs.
## Unknown keys are skipped and absent keys keep their declared default, which
## is what makes every additive subsystem field save-compatible at VERSION 2.
static func state_from_dict(sd: Dictionary) -> GameState:
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
	# a legacy `marketing` budget becomes paid ads before anything reads it
	SimEngine.migrate_budgets(state)
	return state

static func clear_run() -> void:
	if has_run():
		DirAccess.remove_absolute(ProjectSettings.globalize_path(slot_path(active_slot)))

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
