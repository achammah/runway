extends SceneTree
## L-OWN's standalone gate: runs ONLY the ownership lane suite with an honest
## exit code, so the lane can be verified while other lanes are mid-flight.
## The full suite (tests/sim_engine_test.gd) preloads the same file — this
## runner adds no second copy of any check.
##
## Run: godot --headless --path game --script tests/run_own.gd; echo EC=$?

func _init() -> void:
	var checks := [0]
	var fails: Array[String] = []
	var ok := func(cond: bool, msg: String) -> void:
		checks[0] += 1
		if not cond:
			fails.append(msg)
	var suite := load("res://tests/lanes/test_ownership.gd")
	suite.run(ok)
	for f in fails:
		print("FAIL: " + f)
	print("%d ownership checks, %d failed" % [checks[0], fails.size()])
	print("OWNERSHIP PASS" if fails.is_empty() else "OWNERSHIP FAIL")
	quit(1 if fails.size() > 0 else 0)
