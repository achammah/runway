extends SceneTree
## STANDALONE RUNNER — the L-DIVWORKS lane suites only, with an honest exit
## code (the amended W2 gate: lane files verifiable without the full suite).
## The same run(ok) contract the main suite drives; nothing here is loaded by
## anything else.
##
## Run: godot --headless --path . --script tests/lanes/run_divworks.gd

var _checks := 0
var _failed := false

func _init() -> void:
	var okc := func(cond: bool, msg: String) -> void:
		_checks += 1
		if not cond:
			_failed = true
			printerr("FAIL: " + msg)
	for suite in [preload("res://tests/lanes/test_divisions.gd"),
			preload("res://tests/lanes/test_works.gd")]:
		suite.run(okc)
	if _failed:
		print("DIVWORKS SUITE FAILED (%d checks)" % _checks)
		quit(1)
	else:
		print("%d divworks checks held" % _checks)
		quit(0)
