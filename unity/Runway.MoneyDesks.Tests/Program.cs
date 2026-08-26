using System;
using Runway.Core.Tests;

namespace Runway.MoneyDesks.Tests
{
    /// <summary>
    /// The L-MONEY lane's standalone gate: runs the shared MoneyDesksTests
    /// checks with an honest exit code (0 = green). The shared suite runs
    /// the SAME file once Program.cs registers it at integration.
    /// </summary>
    public static class Program
    {
        static int _checks;
        static bool _failed;

        public static int Main()
        {
            Action<bool, string> ok = (cond, msg) =>
            {
                _checks += 1;
                if (!cond)
                {
                    _failed = true;
                    Console.Error.WriteLine("FAIL: " + msg);
                }
            };
            MoneyDesksTests.Run(ok);
            Console.WriteLine("MONEY SUITE (C#): " + _checks + " checks, "
                              + (_failed ? "FAILED" : "all green"));
            return _failed ? 1 : 0;
        }
    }
}
