extends SceneTree
## STANDALONE RUNNER — the L-MONEY lane checks (tests/lanes/test_money_desks.gd)
## with an honest exit code, so the lane gates green without touching the
## shared suite. The same file registers into tests/sim_engine_test.gd at
## integration (one preload line — the coordinator's).
##
## Run: godot --headless --path . --script tests/money_suite.gd ; echo EC=$?

var _checks := 0
var _failed := false

func _init() -> void:
	call_deferred("_go")

func _go() -> void:
	await process_frame
	var ok := func(cond: bool, msg: String) -> void:
		_checks += 1
		if not cond:
			_failed = true
			push_error("FAIL: " + msg)
	var suite = load("res://tests/lanes/test_money_desks.gd")
	suite.run(ok)
	print("MONEY SUITE: %d checks, %s" % [_checks, "FAILED" if _failed else "all green"])
	quit(1 if _failed else 0)
