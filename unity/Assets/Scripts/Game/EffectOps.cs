using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// THE BOUNDED EFFECT-OP INTERPRETER — effect_ops.gd, ported.
    ///
    /// This is the LLM safety keystone: authored cards and generated verdicts run
    /// through the SAME whitelist and the SAME clamps, no op can kill directly, and
    /// deaths only ever come from an armed timebomb or the upkeep loop. Cash stakes
    /// scale with the era (a Series-A office plays for more than a garage); feelings
    /// never do.
    ///
    /// It lives in the run lane rather than in Runway.Core because it is the only
    /// place that has to speak JSON — Core stays typed and file-free.
    /// </summary>
    public static class EffectOps
    {
        static readonly Dictionary<string, double[]> BaseClamps = new Dictionary<string, double[]>
        {
            { "cash_delta", new[] { -5000.0, 5000.0 } },
            { "product_delta", new[] { -20.0, 20.0 } },
            { "traction_delta", new[] { -50.0, 50.0 } },
            { "traction_mult", new[] { 0.7, 1.5 } },
            { "morale_delta", new[] { -20.0, 20.0 } },
            { "hype_delta", new[] { -15.0, 15.0 } },
            { "equity_delta", new[] { -40.0, 0.0 } },
            { "dilute_pct", new[] { 0.0, 35.0 } },
            { "salary", new[] { 0.0, 30000.0 } },
            { "timebomb_weeks", new[] { 1.0, 8.0 } },
        };

        static readonly Dictionary<string, double> EraCashScale = new Dictionary<string, double>
        {
            { "garage", 1.0 }, { "coworking", 8.0 }, { "office", 30.0 },
            { "floor", 120.0 }, { "hq", 500.0 },
        };

        static readonly Dictionary<string, double> EraTractionScale = new Dictionary<string, double>
        {
            { "garage", 1.0 }, { "coworking", 3.0 }, { "office", 10.0 },
            { "floor", 40.0 }, { "hq", 100.0 },
        };

        public static readonly string[] Whitelist =
        {
            "cash_delta", "product_delta", "traction_delta", "traction_mult",
            "morale_delta", "hype_delta", "equity_delta", "set_flag", "clear_flag",
            "grant_item", "destroy_item", "arm_timebomb", "weight_future",
            "dilute_pct", "hire", "fire_role", "investor_board_seat", "raise_round",
            "accept_acquisition",
        };

        static double Clamped(string op, double v, GameState state)
        {
            double[] c;
            if (!BaseClamps.TryGetValue(op, out c)) return v;
            double lo = c[0];
            double hi = c[1];
            if (state != null && op == "cash_delta")
            {
                double s;
                if (!EraCashScale.TryGetValue(state.Era ?? "garage", out s)) s = 1.0;
                lo *= s; hi *= s;
            }
            else if (state != null && op == "traction_delta")
            {
                double s;
                if (!EraTractionScale.TryGetValue(state.Era ?? "garage", out s)) s = 1.0;
                lo *= s; hi *= s;
            }
            return Gd.Clampf(v, lo, hi);
        }

        /// One effect onto state. Returns the receipt line, or "" for a silent op.
        public static string Apply(JObject effect, GameState state)
        {
            if (effect == null || state == null) return "";
            string op = ContentDb.Str(effect, "op");
            switch (op)
            {
                case "cash_delta":
                {
                    int v = Gd.ToInt(Clamped(op, ContentDb.Num(effect, "v", 0.0), state));
                    state.Cash += v;
                    return string.Format("cash {0}{1}", v >= 0 ? "+" : "", v);
                }
                case "product_delta":
                {
                    int v = Gd.ToInt(Clamped(op, ContentDb.Num(effect, "v", 0.0), null));
                    state.Product += v;
                    return string.Format("product {0}{1}", v >= 0 ? "+" : "", v);
                }
                case "traction_delta":
                {
                    int v = Gd.ToInt(Clamped(op, ContentDb.Num(effect, "v", 0.0), state));
                    state.Traction += v;
                    return string.Format("traction {0}{1}", v >= 0 ? "+" : "", v);
                }
                case "traction_mult":
                {
                    double m = Clamped(op, ContentDb.Num(effect, "v", 1.0), null);
                    state.Traction = Gd.RoundToInt(state.Traction * m);
                    return string.Format("traction x{0:0.00}", m);
                }
                case "morale_delta":
                {
                    int v = Gd.ToInt(Clamped(op, ContentDb.Num(effect, "v", 0.0), null));
                    state.Morale += v;
                    return string.Format("morale {0}{1}", v >= 0 ? "+" : "", v);
                }
                case "hype_delta":
                {
                    int v = Gd.ToInt(Clamped(op, ContentDb.Num(effect, "v", 0.0), null));
                    state.Hype += v;
                    return string.Format("hype {0}{1}", v >= 0 ? "+" : "", v);
                }
                case "equity_delta":
                {
                    double v = Clamped(op, ContentDb.Num(effect, "v", 0.0), null);
                    state.FounderPct += v;
                    return string.Format("founder {0}{1:0}%", v >= 0.0 ? "+" : "", v);
                }
                case "dilute_pct":
                {
                    double x = Clamped(op, ContentDb.Num(effect, "v", 0.0), null);
                    state.DiluteAll(x);
                    return string.Format("everyone diluted by {0:0}%", x);
                }
                case "raise_round":
                {
                    string rd = ContentDb.Str(effect, "v");
                    if (rd.Length > 0 && !state.RoundsRaised.Contains(rd))
                    {
                        state.RoundsRaised.Add(rd);
                        state.SetFlag(rd.StartsWith("series") ? rd : rd + "_raised");
                    }
                    return "round closed: " + rd;
                }
                case "investor_board_seat":
                    state.BoardSeatsInvestor += 1;
                    if (state.BoardSeatsInvestor >= state.BoardSeatsFounder)
                        state.SetFlag("board_control_lost");
                    return "an investor takes a board seat";
                case "hire":
                {
                    if (!state.CanHire()) return "no desk left to hire into";
                    var e = new Employee
                    {
                        Name = ContentDb.Str(effect, "name", "New Hire"),
                        Role = ContentDb.Str(effect, "role", "generalist"),
                        Salary = Gd.ToInt(Clamped("salary", ContentDb.Num(effect, "salary", 1500.0), null)),
                        Burnout = 0,
                        Quirk = ContentDb.Str(effect, "quirk"),
                    };
                    state.Employees.Add(e);
                    return string.Format("hired {0} ({1})", e.Name, e.Role);
                }
                case "fire_role":
                {
                    string role = ContentDb.Str(effect, "v");
                    for (int i = 0; i < state.Employees.Count; i++)
                    {
                        if (state.Employees[i].Role == role)
                        {
                            string nm = state.Employees[i].Name;
                            state.Employees.RemoveAt(i);
                            state.Morale = Gd.Clampi(state.Morale - 6, 0, 100);
                            return "let " + (string.IsNullOrEmpty(nm) ? role : nm) + " go";
                        }
                    }
                    return "";
                }
                case "accept_acquisition":
                {
                    double mult = Gd.Clampf(ContentDb.Num(effect, "v", 0.5), 0.2, 1.0);
                    state.ExitValue = Gd.ToInt(state.Valuation() * mult);
                    state.SetFlag("acquired_exit");
                    return "you shook the hand. it's over.";
                }
                case "set_flag":
                {
                    string f = ContentDb.Str(effect, "v");
                    state.SetFlag(f);
                    return "flag: " + f;
                }
                case "clear_flag":
                    state.Flags.Remove(ContentDb.Str(effect, "v"));
                    return "";
                case "grant_item":
                {
                    string id = ContentDb.Str(effect, "v");
                    if (id.Length > 0 && !state.Items.Contains(id)) state.Items.Add(id);
                    return "got: " + id;
                }
                case "destroy_item":
                {
                    string id = ContentDb.Str(effect, "v");
                    state.Items.Remove(id);
                    return "lost: " + id;
                }
                case "arm_timebomb":
                {
                    int weeks = Gd.ToInt(Clamped("timebomb_weeks", ContentDb.Num(effect, "weeks", 2.0), null));
                    string ev = ContentDb.Str(effect, "event");
                    if (ev.Length > 0)
                        state.Timebombs.Add(new Timebomb { WeeksLeft = weeks, Event = ev });
                    return "…something is ticking";
                }
                case "weight_future":
                {
                    string ev = ContentDb.Str(effect, "v");
                    if (ev.Length > 0) state.FutureWeights.Add(ev);
                    return "";
                }
                default:
                    Debug.LogWarning("RUNWAY! unknown effect op rejected: " + op);
                    return "";
            }
        }

        public static List<string> ApplyAll(JToken effects, GameState state)
        {
            var log = new List<string>();
            var arr = effects as JArray;
            if (arr == null || state == null) return log;
            foreach (JToken t in arr)
            {
                var e = t as JObject;
                if (e == null) continue;
                string line = Apply(e, state);
                if (!string.IsNullOrEmpty(line)) log.Add(line);
            }
            state.ClampiMeters();
            return log;
        }
    }
}
