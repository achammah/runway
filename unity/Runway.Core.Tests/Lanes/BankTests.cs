using System;
using Runway.Core;

namespace Runway.CoreTests
{
    /// <summary>
    /// LANE SUITE — bank. Spec: docs/design/06-finance.md (twin test pins).
    ///
    /// THE STUB the spine planted. Program.cs calls Run after the engine's own
    /// checks and hands over `ok`, the same assert the whole suite uses:
    /// ok(condition, "what this pins"). Target 6-10 checks for this lane.
    ///
    /// The porting law: a check lands FIRST in game/tests/lanes/test_bank.gd,
    /// then here in the same order. Same checks, same order, same logic — the
    /// two engines do not share PRNG internals, so never pin a draw across
    /// them, only behaviour.
    /// </summary>
    public static class BankTests
    {
        public static void Run(Action<bool, string> ok)
        {
        }
    }
}
