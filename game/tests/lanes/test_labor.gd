extends RefCounted
## LANE SUITE — labor. Spec: docs/design/02-labor-market.md (§ twin test pins).
##
## THE STUB the spine planted. `tests/sim_engine_test.gd` calls run() after the
## engine's own checks and hands over `ok`, the same assert the whole suite
## uses: ok.call(condition, "what this pins"). Target 6-10 checks for this lane.
##
## The porting law: a check lands HERE first, then in the same order in
## unity/Runway.Core.Tests/Lanes/LaborTests.cs. Same checks, same order, same
## logic — the two engines do not share PRNG internals, so never pin a draw
## across them, only behaviour.

static func run(_ok: Callable) -> void:
	pass
