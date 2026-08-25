using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — the binder's `the ledger` tab. Spec: docs/design/04-funnel-channels.md section 6.1
    ///
    /// BinderScreen dispatches the tab body here and passes ITSELF, so this file
    /// draws through the binder's own helpers and never reaches into the sheet
    /// directly.
    ///
    /// EIGHT LEVERS IN TWO BLOCKS. The four acquisition channels ARE the growth
    /// strategy — one blended lever hid the core decision — so they get their own
    /// sub-block at a tighter 58px pitch under one MARKETING header, above the
    /// divider; the org levers keep their fuller 62px rows below it. Every row
    /// prints what its money is doing RIGHT NOW, out of the engine's own formulas:
    /// that is house law (10-interface-language 2.1), and for the channels it is
    /// also the whole lesson — the era discount, the compounding stock, the NPS gate.
    ///
    /// THE SHEET NEVER SCROLLS AND NEVER OVERFLOWS. Eight rows, the unit economics,
    /// the P&amp;L, the bank, the bottom line and a warning all have to live inside
    /// 760px, so the lower half sits at FIXED slots and exactly one line ever
    /// yields its slot (the unit-econ line, and only when both warnings fire).
    ///
    /// TWIN LAW: this file and game/src/ui/desks/desk_ledger.gd draw the same rows
    /// at the same coordinates.
    /// </summary>
    public static class DeskLedger
    {
        // ── the two blocks ───────────────────────────────────────────────────
        /// [budget key, the word on the page, what the money actually does].
        static readonly string[][] ChannelLevers =
        {
            new[] { "ads", "ads", "paid reach — instant, saturates hard; runs only while fed" },
            new[] { "content", "content", "the library — slow to build, works while you sleep, rots if starved" },
            new[] { "referrals", "referrals", "promoters talking — multiplies word of mouth; needs product + care" },
            new[] { "outbound", "outbound", "lists and cold calls — buys reach AND closing; born for enterprise" },
        };
        static readonly string[][] OrgLevers =
        {
            new[] { "sales", "sales", "closing — every $600/wk closes like one more part-time seller" },
            new[] { "care", "care", "retention — up to 30% less churn as care approaches $3k" },
            new[] { "rnd", "rnd", "product — ships ~+1 quality per $1,200/wk and pays down debt" },
            new[] { "office", "office", "the office — food, perks, benefits; morale climbs toward +3/wk by ~$2k" },
        };

        /// ONE COLUMN GRAMMAR down the whole sheet (10-interface-language 1.4):
        /// identity, money, live effect, controls. Eight rows share it, or the
        /// page reads as two pages taped together.
        const float XValue = 455f;
        const float XEffect = 640f;
        const float XMinus = 1000f;
        const float XPlus = 1064f;

        /// THE FIXED LOWER HALF, measured against the 760px pane.
        const float YHeader = 62f;
        const float YChannels = 96f;
        const float ChannelPitch = 58f;
        const float YDivider = 333f;
        const float YOrg = 340f;
        const float OrgPitch = 62f;
        const float YUnit = 592f;
        const float YIn = 626f;
        const float YOut = 660f;
        const float YBottom = 694f;
        const float YRules = 734f;

        public static void Draw(BinderScreen b)
        {
            GameState st = b.State;
            b.L("the ledger — where this week's money goes", 10f, 6f, 38f);

            // ── THE MIX: its total and the blended CAC stay in view while you
            // step it, because the question this block answers is "what does a
            // customer cost".
            int chTotal = 0;
            for (int i = 0; i < ChannelLevers.Length; i++) chTotal += b.Budget(ChannelLevers[i][0]);
            Dictionary<string, double> fn = SimFunnel.Funnel(st);
            int blCac = Gd.ToInt(SimFunnel.Num(fn, "blended_cac"));
            b.L("MARKETING — the funnel mix · $" + GameUi.Money(chTotal) + "/wk · blended CAC "
                + (blCac > 0 ? "$" + GameUi.Money(blCac) : "not yet knowable"),
                10f, YHeader, 24f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f));
            float y = YChannels;
            for (int i = 0; i < ChannelLevers.Length; i++)
            {
                string cat = ChannelLevers[i][0];
                int cur = b.Budget(cat);
                b.L(ChannelLevers[i][1].ToUpper(), 10f, y, 24f);
                b.L(ChannelLevers[i][2], 10f, y + 27f, 18f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 430f);
                b.L("$" + GameUi.Money(cur) + "/wk", XValue, y + 2f, 26f, DrawnUI.Coral, 175f);
                // THE BOUND PRINTS ITS REASON where the effect was (2.1): a step
                // the world refuses is a lesson about the era, not a dead button.
                int up = ChannelStep(b, cat, cur, 1);
                string eff = up == cur && cur > 0
                    ? "the mix is at the era's ceiling"
                    : SimFunnel.LeverEffect(st, cat);
                b.L(eff, XEffect, y + 8f, 20f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.75f), 340f);
                string c = cat;
                int down = ChannelStep(b, cat, cur, -1);
                int upv = up;
                GameUi.InkWord(b.Content, "−", XMinus, y + 2f, 52f, 44f, 40f, DrawnUI.Ink, () =>
                {
                    b.SetBudget(c, down);
                    b.Refresh();
                });
                GameUi.InkWord(b.Content, "+", XPlus, y + 2f, 52f, 44f, 40f, DrawnUI.Ink, () =>
                {
                    b.SetBudget(c, upv);
                    b.Refresh();
                });
                y += ChannelPitch;
            }
            DeskKit.Rule(b, YDivider);

            // ── the org levers: same words, same controls, the fuller pitch
            y = YOrg;
            for (int i = 0; i < OrgLevers.Length; i++)
            {
                string cat = OrgLevers[i][0];
                int cur = b.Budget(cat);
                b.L(OrgLevers[i][1].ToUpper(), 10f, y, 28f);
                b.L(OrgLevers[i][2], 10f, y + 32f, 21f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 430f);
                b.L("$" + GameUi.Money(cur) + "/wk", XValue, y + 4f, 30f, DrawnUI.Coral, 175f);
                b.L(LeverEffect(st, cat, cur), XEffect, y + 12f, 20f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.75f), 340f);
                string c = cat;
                int at = cur;
                GameUi.InkWord(b.Content, "−", XMinus, y, 52f, 46f, 40f, DrawnUI.Ink, () =>
                {
                    b.SetBudget(c, b.Step(at, -1));
                    b.Refresh();
                });
                GameUi.InkWord(b.Content, "+", XPlus, y, 52f, 46f, 40f, DrawnUI.Ink, () =>
                {
                    b.SetBudget(c, b.Step(at, 1));
                    b.Refresh();
                });
                y += OrgPitch;
            }

            // ── the math, honestly, at fixed slots
            int rw = SimEngine.RunwayWeeks(st);
            var warns = new List<string>();
            if (rw <= 4 && rw < 999)
                warns.Add(string.Format(CultureInfo.InvariantCulture,
                    "⚠ this spend kills the company in {0} weeks — cut it or earn it", rw));
            if (st.Cash < 0)
                warns.Add(string.Format(CultureInfo.InvariantCulture,
                    "THE RED: {0} of 3 weeks below zero. At three, it's over.", st.WeeksInRed));
            // WARNINGS OUTRANK WISDOM (2.7), and when BOTH fire the unit-econ line
            // yields its slot to the first — the only line here that gives way.
            if (warns.Count >= 2)
            {
                b.L(warns[0], 10f, YUnit, 24f, DrawnUI.Coral, 1100f);
            }
            else
            {
                double arpu = b.UnitEcon("arpu");
                int cac = Gd.ToInt(b.UnitEcon("cac"));
                int ltv = Gd.ToInt(b.UnitEcon("ltv"));
                int pb = Gd.ToInt(b.UnitEcon("payback_wk"));
                b.L(string.Format(CultureInfo.InvariantCulture,
                    "a customer pays ≈ ${0:0}/wk · costs ${1} to win (CAC) · is worth ${2} over their stay (LTV) · pays back in {3}",
                    arpu, cac > 0 ? GameUi.Money(cac) : "?",
                    ltv > 0 ? GameUi.Money(ltv) : "?", pb > 0 ? pb + " wks" : "—"),
                    10f, YUnit, 23f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.75f), 1100f);
            }

            int leverSum = st.Budgets.Sum();
            Pnl pnl = st.LastPnl;
            if (pnl != null)
            {
                b.L(string.Format(CultureInfo.InvariantCulture, "last week: in ${0} · serving ${1}{2}",
                    GameUi.Money(pnl.Revenue), GameUi.Money(pnl.Cogs),
                    pnl.Learning < 0.995
                        ? string.Format(CultureInfo.InvariantCulture, "  (learning ×{0:0.00})", pnl.Learning) : ""),
                    10f, YIn, 24f, DrawnUI.Blue, 1100f);
                b.L(OutLine(pnl), 10f, YOut, 24f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.8f), 1100f);
                // BREAK-EVEN (06) closes the bottom line: the number of customers
                // that ends the argument, and how far away it is right now.
                int be = SimBank.BreakEvenCustomers(st);
                string beTxt = be > 0
                    ? " · break-even " + GameUi.Money(be) + " (" + GameUi.Money(st.Traction) + " now)"
                    : "";
                b.L(string.Format(CultureInfo.InvariantCulture,
                    "THE BOTTOM LINE: {0}${1} a week · levers total ${2}/wk · runway {3}{4}",
                    pnl.Net >= 0 ? "+" : "−", GameUi.Money(Math.Abs(pnl.Net)),
                    GameUi.Money(leverSum), rw < 999 ? rw + " weeks" : "gaining money", beTxt),
                    10f, YBottom, 27f, pnl.Net >= 0 ? DrawnUI.Sage : DrawnUI.Coral, 1100f);
            }
            else
            {
                b.L(string.Format(CultureInfo.InvariantCulture, "levers total ${0}/wk · runway {1}",
                    GameUi.Money(leverSum), rw < 999 ? rw + " weeks" : "gaining money"),
                    10f, YBottom, 27f);
            }

            // ── THE COST OF MONEY, on its own line: interest and tax sit OUTSIDE
            // burn (00-spine 2), which is the whole pedagogy — operating profit,
            // then the bank, then the state. The full statement lives on THE BANK.
            int interest = pnl != null ? pnl.Interest : 0;
            int tax = pnl != null ? pnl.Tax : 0;
            int principal = Gd.ToInt(st.GetMetaF("bank_principal_wk", 0.0));
            if (interest + tax + principal > 0)
            {
                b.L("the bank & the state: interest $" + GameUi.Money(interest)
                    + " · principal $" + GameUi.Money(principal)
                    + " · tax $" + GameUi.Money(tax),
                    600f, YUnit + 34f, 20f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 540f);
            }

            // ── ONE SLOT AT THE FOOT: the loudest warning, else the laws of this world
            if (warns.Count > 0)
            {
                b.L(warns[warns.Count - 1], 10f, YRules, 20f, DrawnUI.Coral, 1100f);
            }
            else
            {
                b.L("the rules of this world: reach saturates · content compounds · only capacity "
                    + "closes · churn is a leaky bucket · three weeks below zero ends it",
                    10f, YRules, 20f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f));
            }
        }

        // ── THE COMPACT "out:" LINE ──────────────────────────────────────────
        // Nine lanes can all bill in one week and the sheet still has ONE line
        // for it (00-spine 11). The four standing costs always print; every lane
        // that spent something adds its own named tail, in the fixed order below,
        // until the line is full — and what does not fit says so and points at
        // the desk that keeps the full books. A lane that spent nothing renders
        // nothing at all, so a quiet week (or a run with no factory) reads
        // exactly as it always did.

        /// <summary>What one 24px line of the hand holds across the 1100px column.
        /// Counted, not measured, so both engines break the line in the same place.</summary>
        const int OutChars = 126;

        static string OutLine(Pnl pnl)
        {
            string line = string.Format(CultureInfo.InvariantCulture,
                "out: rent ${0} · payroll ${1} · infra ${2} · levers ${3}",
                GameUi.Money(pnl.Rent), GameUi.Money(pnl.Payroll), GameUi.Money(pnl.Infra),
                GameUi.Money(pnl.Marketing + pnl.Sales + pnl.Care + pnl.Rnd + pnl.Office));
            var tails = new List<string>();
            int[] vals =
            {
                pnl.OfferFixed,   // 01 tools, licenses, storage
                pnl.Severance,    // 02 the firing invoice
                pnl.Recruiting,   // 02 the recruiter's retainer
                pnl.Production,   // 09 built in house
                pnl.Subcontract,  // 09 someone else's line
                pnl.EquipUpkeep,  // 09 machines do not maintain themselves
                pnl.Carrying,     // 09 stock costs money to sit still
                pnl.Incident,
            };
            string[] names =
            {
                "catalog", "severance", "recruiting", "production",
                "subcontract", "upkeep", "carrying", "unforeseen",
            };
            for (int i = 0; i < vals.Length; i++)
                if (vals[i] > 0) tails.Add(" · " + names[i] + " $" + GameUi.Money(vals[i]));
            // the standing commitments are a RATE, not a one-off, so they carry /wk
            if (pnl.LiabilitiesWk > 0)
                tails.Add(" · standing $" + GameUi.Money(pnl.LiabilitiesWk) + "/wk");
            int shown = 0;
            while (shown < tails.Count)
            {
                int left = tails.Count - shown - 1;
                string over = left == 0 ? "" : string.Format(CultureInfo.InvariantCulture,
                    " · +{0} lanes — the bank keeps the full books", left);
                if (line.Length + tails[shown].Length + over.Length > OutChars) break;
                line += tails[shown];
                shown += 1;
            }
            if (shown < tails.Count)
                line += string.Format(CultureInfo.InvariantCulture,
                    " · +{0} lanes — the bank keeps the full books", tails.Count - shown);
            return line;
        }

        /// <summary>
        /// THE ERA CAP CLAMPS THE CHANNEL SUM (DECISIONS.md — funnel). A step up
        /// that would push the whole mix past the era's ceiling is refused, and the
        /// row prints why: clamping per lever would let four channels quadruple
        /// what one garage is allowed to spend on reach.
        /// </summary>
        static int ChannelStep(BinderScreen b, string cat, int cur, int dir)
        {
            int want = b.Step(cur, dir);
            if (dir <= 0) return want;
            int others = 0;
            for (int i = 0; i < ChannelLevers.Length; i++)
                if (ChannelLevers[i][0] != cat) others += b.Budget(ChannelLevers[i][0]);
            return others + want > SimEngine.EraSpendCap(b.State.Era) ? cur : want;
        }

        /// the engine's live math for one lever, in one plain phrase
        static string LeverEffect(GameState st, string cat, int v)
        {
            switch (cat)
            {
                case "ads":
                case "content":
                case "referrals":
                case "outbound":
                    // the four channels compute their own: the era discount, the
                    // compounding stock and the NPS gate all live in the lane
                    return SimFunnel.LeverEffect(st, cat);
                case "sales":
                    return v > 0 ? string.Format(CultureInfo.InvariantCulture,
                        "+{0:0.0} closers of capacity", v / 600f) : "founder sells alone";
                case "care":
                    return v > 0 ? string.Format(CultureInfo.InvariantCulture, "churn −{0}%",
                        Mathf.RoundToInt(30f * (1f - Mathf.Exp(-v / 1500f)))) : "nobody picks up";
                case "rnd":
                    return v > 0 ? string.Format(CultureInfo.InvariantCulture,
                        "+{0:0.0} product/wk, debt melts", v / 1200f) : "no extra shipping";
                case "office":
                    return v > 0 ? string.Format(CultureInfo.InvariantCulture, "+{0:0.0} morale/wk",
                        3.0 * (1.0 - Mathf.Exp(-v / 800f))) : "instant coffee, cold room";
            }
            return "";
        }
    }
}
