using System;
using System.Collections.Generic;

namespace Runway.Core
{
    /// <summary>The pivot executor's receipt — what fired and what it cost.</summary>
    public sealed class PivotReceipt
    {
        public bool Ok;
        public string Kind = "";
        public string Reason = "";
        public List<string> Lines = new List<string>();
        public int LostCustomers;
        public int KeptCustomers;
        public int LossPct;
        public int OldVersion;
        public int BetsDead;
        public int DebtCleared;
        public int DealsKnocked;
        public int WellDrained;
        public int DealsDead;
        public string OldWho = "";
        public string NewWho = "";
    }

    /// <summary>The armed intent: kind (audience|product) + optional target.</summary>
    public sealed class PivotArmed
    {
        public string Kind = "";
        public string Target = "";
    }

    /// <summary>The desk's PREVIEW — computed from live state, mutating nothing.</summary>
    public sealed class PivotPreview
    {
        public string Kind = "";
        public int CustomersLost;      // audience: all of them
        public int CustomersAtRisk;    // product: the roll decides
        public int Well;
        public int DealsDead;
        public int DealsKnocked;
        public int BetsDead;
        public int DebtCleared;
        public string VersionFrom = "";
        public string VersionTo = "";
        public string Version = "";
        public int Debts;
    }

    /// <summary>
    /// LANE — THE PIVOT (the escape hatch). Twin of
    /// game/src/core/lanes/sim_pivot.gd; spec: docs/design/DECISIONS.md
    /// § THE PIVOT + docs/design/12-binder-rework-2.md § pivot.
    ///
    /// AUDIENCE PIVOT: customers → 0, market-side learning dies (deals, leads,
    /// channel learning, the content well, beliefs re-fog); the product, the
    /// team, the cash and the debts survive.
    /// PRODUCT PIVOT: a uniform 50–100% roll takes customers; product advances
    /// die (version → v0.1, bets, platform, tech debt clears); channel/sales
    /// learning, the well, the relationships, the cash and the debts survive.
    /// Enterprise deals survive as named leads knocked to the first meeting.
    ///
    /// OP-DRIVEN: no tick seam. The arm flags are durable state.Flags entries
    /// (byte-identical save keys); ResolveArmed fires at the LOCK IN seam.
    ///
    /// SALT: SALT_PIVOT_LOSS = 170 (fresh decade 170-179, pivot). The two
    /// engines do not share PRNG internals — the roll is pinned per-engine.
    /// </summary>
    public static class SimPivot
    {
        public const int SALT_PIVOT_LOSS = 170;

        public const string FlagAud = "pivot_armed_audience";
        public const string FlagProd = "pivot_armed_product";

        public static readonly string[] AUDIENCES = { "Enterprise", "SMB", "Consumer" };
        public static readonly string[] CRAFTS = { "Software", "Hardware", "Marketplace", "Service" };

        // ── THE ARM / DISARM SURFACE ───────────────────────────────────────

        public static bool ArmAudience(GameState state, string newWho)
        {
            if (Array.IndexOf(AUDIENCES, newWho) < 0 || newWho == state.BizWho) return false;
            Disarm(state);
            state.Flags.Add(FlagAud + ":" + newWho);
            state.LogAction(string.Format(
                "ARMED the audience pivot: {0} → {1} (fires at LOCK IN)", state.BizWho, newWho));
            return true;
        }

        public static bool ArmProduct(GameState state, string newWhat = "")
        {
            if (newWhat != "" && Array.IndexOf(CRAFTS, newWhat) < 0) return false;
            Disarm(state);
            state.Flags.Add(FlagProd + (newWhat != "" ? ":" + newWhat : ""));
            state.LogAction(string.Format("ARMED the product pivot{0} (fires at LOCK IN)",
                newWhat != "" ? ": craft → " + newWhat : ""));
            return true;
        }

        /// <summary>Esc-grade abandon: every pivot intent dies, nothing else moves.</summary>
        public static void Disarm(GameState state)
        {
            for (int i = state.Flags.Count - 1; i >= 0; i--)
            {
                string f = state.Flags[i] ?? "";
                if (f == FlagAud || f == FlagProd
                    || f.StartsWith(FlagAud + ":") || f.StartsWith(FlagProd + ":"))
                    state.Flags.RemoveAt(i);
            }
        }

        /// <summary>The armed intent, or null.</summary>
        public static PivotArmed Armed(GameState state)
        {
            for (int i = 0; i < state.Flags.Count; i++)
            {
                string s = state.Flags[i] ?? "";
                if (s == FlagAud || s.StartsWith(FlagAud + ":"))
                    return new PivotArmed { Kind = "audience",
                        Target = s.Length > FlagAud.Length ? s.Substring(FlagAud.Length + 1) : "" };
                if (s == FlagProd || s.StartsWith(FlagProd + ":"))
                    return new PivotArmed { Kind = "product",
                        Target = s.Length > FlagProd.Length ? s.Substring(FlagProd.Length + 1) : "" };
            }
            return null;
        }

        /// <summary>The LOCK IN seam calls this once per week-turn. Null when
        /// nothing was armed.</summary>
        public static PivotReceipt ResolveArmed(GameState state)
        {
            PivotArmed a = Armed(state);
            if (a == null) return null;
            Disarm(state);
            return a.Kind == "audience"
                ? PivotAudience(state, a.Target)
                : PivotProduct(state, a.Target);
        }

        // ── THE EXECUTORS (DM op names, FIXED) ─────────────────────────────

        /// <summary>AUDIENCE PIVOT — customers → 0, the market side dies whole,
        /// the product and the team survive.</summary>
        public static PivotReceipt PivotAudience(GameState state, string newWho)
        {
            if (Array.IndexOf(AUDIENCES, newWho) < 0 || newWho == state.BizWho)
                return new PivotReceipt { Ok = false, Kind = "audience",
                    Reason = "the new audience must be a real one you do not already serve" };
            string oldWho = state.BizWho;
            int lost = state.Traction;
            int well = (int)Math.Round(state.ContentEquity);
            int deals = state.Leads.Count;
            // what dies: the market side, whole
            state.Traction = 0;
            state.Leads = new List<Lead>();
            state.Logos = new List<Logo>();
            state.PipeUnits = 0.0;
            state.PipeChurnAcc = 0.0;
            state.PipeStats = new PipeStats();
            state.ContentEquity = 0.0;
            state.Beliefs = null;       // re-fog: the next tick reseeds first guesses
            // the new market: the world reprices itself for who you now serve
            state.BizWho = newWho;
            state.Theta = SimEngine.DefaultTheta(state.BizWhat, newWho);
            // the record
            state.Pivots += 1;
            state.SetFlag("pivoted");
            state.LogAction(string.Format(
                "THE PIVOT (audience): {0} → {1} — {2} customers released", oldWho, newWho, lost));
            var lines = new List<string>
            {
                string.Format("audience pivot: {0} → {1}", oldWho, newWho),
                string.Format("{0} customers released — traction starts over", lost),
                string.Format("{0} named deals died with the market that held them", deals),
                string.Format("the content well drained (${0} of equity)", well),
                "market beliefs re-fogged — the first guesses return",
                "the product survives as built · the team stays · the debts stay",
            };
            return new PivotReceipt { Ok = true, Kind = "audience", Lines = lines,
                LostCustomers = lost, WellDrained = well, DealsDead = deals,
                OldWho = oldWho, NewWho = newWho };
        }

        /// <summary>PRODUCT PIVOT — the 50–100% roll decides who stays; the
        /// product advances die; the market learning survives.</summary>
        public static PivotReceipt PivotProduct(GameState state, string newWhat = "")
        {
            if (newWhat != "" && Array.IndexOf(CRAFTS, newWhat) < 0)
                return new PivotReceipt { Ok = false, Kind = "product",
                    Reason = "the new craft is not one the world knows" };
            string oldWhat = state.BizWhat;
            int oldProduct = state.Product;
            int oldDebt = (int)Math.Round(state.TechDebt);
            int betsDead = state.Bets.Count;
            int before = state.Traction;
            // the roll: uniform 50–100% of the customers walk (SALT_PIVOT_LOSS)
            double loss = SimEngine.RngForSalt(state, SALT_PIVOT_LOSS).RandfRange(0.5, 1.0);
            int kept = (int)Math.Floor(before * (1.0 - loss));
            int lost = before - kept;
            state.Traction = kept;
            // what dies: the product side, whole
            state.Product = 10;             // v0.62 → v0.1
            state.Bets = new List<Bet>();
            state.PlatformLevel = 0;
            state.TechDebt = 0.0;           // the debt clears with its codebase
            state.Features = new List<Feature>();
            state.ServedTotal = 0;          // serving practice was practice ON the product
            if (state.Hardware != null)
            {
                state.Hardware.Stock = 0;              // shelved units of a dead product
                state.Hardware.ProducedTotal = 0;      // the build curve restarts
                state.Hardware.DemandEma = 0.0;
                state.Hardware.ProductionTarget = -1;
            }
            // the relationships survive: named deals knock back to the first meeting
            int knocked = state.Leads.Count;
            for (int i = 0; i < state.Leads.Count; i++)
            {
                state.Leads[i].Stage = "meeting";
                state.Leads[i].AgeWeeks = 0;
            }
            // signed logos are customers: the same roll decides who stays (newest kept)
            int keepLogos = (int)Math.Round(state.Logos.Count * (1.0 - loss));
            if (keepLogos < state.Logos.Count)
                state.Logos = state.Logos.GetRange(state.Logos.Count - keepLogos, keepLogos);
            state.PipeUnits = 0.0;          // unnamed interest was in the old product
            // PipeStats stays — CAC and cycle learning are the sales team's
            // the craft, when it changes; the world reprices what you now make
            if (newWhat != "") state.BizWhat = newWhat;
            state.Theta = SimEngine.DefaultTheta(state.BizWhat, state.BizWho);
            // the record
            state.Pivots += 1;
            state.SetFlag("pivoted");
            state.LogAction(string.Format(
                "THE PIVOT (product): v0.{0} → v0.1{1} — {2} of {3} customers walked",
                Gd.Maxi(1, oldProduct / 10),
                newWhat != "" ? " · craft → " + state.BizWhat : "", lost, before));
            var lines = new List<string>
            {
                "product pivot" + (newWhat != ""
                    ? string.Format(": {0} → {1}", oldWhat, state.BizWhat) : ""),
                string.Format("the roll took {0}% — {1} of {2} customers walked",
                    (int)Math.Round(loss * 100.0), lost, before),
                string.Format("v0.{0} → v0.1 — the advances died with the codebase",
                    Gd.Maxi(1, oldProduct / 10)),
                string.Format("{0} bets died on the wall · the plumbing debt cleared (−{1})",
                    betsDead, oldDebt),
                string.Format("{0} named deals knocked back to the first meeting", knocked),
                "channel learning, the well and the relationships survive · the debts stay",
            };
            return new PivotReceipt { Ok = true, Kind = "product", Lines = lines,
                LostCustomers = lost, KeptCustomers = kept,
                LossPct = (int)Math.Round(loss * 100.0),
                OldVersion = Gd.Maxi(1, oldProduct / 10),
                BetsDead = betsDead, DebtCleared = oldDebt, DealsKnocked = knocked };
        }

        // ── THE PREVIEW (pure, no mutation) ────────────────────────────────

        /// <summary>Computed from live state, never asserted, mutating nothing.
        /// The product roll shows its honest RANGE — dice at the press.</summary>
        public static PivotPreview Preview(GameState state, string kind)
        {
            int debts = SimBank.DebtTotal(state);
            if (kind == "audience")
                return new PivotPreview
                {
                    Kind = "audience",
                    CustomersLost = state.Traction,
                    Well = (int)Math.Round(state.ContentEquity),
                    DealsDead = state.Leads.Count,
                    Version = "v0." + Gd.Maxi(1, state.Product / 10),
                    Debts = debts,
                };
            return new PivotPreview
            {
                Kind = "product",
                CustomersAtRisk = state.Traction,
                VersionFrom = "v0." + Gd.Maxi(1, state.Product / 10),
                VersionTo = "v0.1",
                BetsDead = state.Bets.Count,
                DebtCleared = (int)Math.Round(state.TechDebt),
                DealsKnocked = state.Leads.Count,
                Debts = debts,
            };
        }

        // ── THE SPINE'S ENTRY POINTS (lane-module shape; never wired) ──────

        public static void TickPre(GameState state, WeeklyReport rep) { }

        public static void TickMoney(GameState state, WeeklyReport rep, MoneyWork m) { }

        public static void TickPost(GameState state, WeeklyReport rep) { }

        /// <summary>DM context: an armed pivot is the week's loudest fact — the
        /// DM builds the tension and never resolves it.</summary>
        public static List<string> Directives(GameState state)
        {
            PivotArmed a = Armed(state);
            if (a == null) return new List<string>();
            string toward = a.Target != "" ? " toward " + a.Target : "";
            return new List<string>
            {
                string.Format("THE PIVOT: the founder has ARMED a {0} pivot{1}. It resolves at "
                    + "this week's LOCK IN — narrate the held breath; do not resolve it yourself.",
                    a.Kind, toward),
            };
        }

        /// <summary>Attention: the armed pivot is a sev-3 alarm until it fires
        /// or is disarmed. (Fan-in rides the coordinator package.)</summary>
        public static List<AttentionItem> Attention(GameState state)
        {
            PivotArmed a = Armed(state);
            if (a == null) return new List<AttentionItem>();
            return new List<AttentionItem>
            {
                new AttentionItem { Desk = "pivot", Key = "pivot_armed", Severity = 3,
                    Label = "the pivot is armed — it fires at LOCK IN" },
            };
        }
    }
}
