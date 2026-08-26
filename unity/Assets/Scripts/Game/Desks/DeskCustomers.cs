using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — the binder's `customers` tab. Spec: docs/design/04-funnel-channels.md section 6.2
    ///
    /// BinderScreen dispatches the tab body here and passes ITSELF, so this file
    /// draws through the binder's own helpers and never reaches into the sheet
    /// directly.
    ///
    /// THE FUNNEL IS THE PAGE, AND ANALYTICS IS THE LIGHT. Every read below is
    /// the engine's own intermediate value — reach, leads, signed, conversion,
    /// the closing ceiling, per-channel CAC — revealed at the level the founder
    /// can actually see: an = min(AnalyticsLevel, era cap). A garage has no data
    /// stack to buy, so full attribution is an office-era capability, and the fog
    /// at an=0 is the shipped page, unchanged, on purpose.
    ///
    /// The bar every surface ships at (00-spine section 11): readable first pass
    /// by a tired player; concepts named in real business terms with a teaching
    /// line where a number first appears; no dead ends and every state leavable;
    /// drawn in the game's hand, never a SaaS panel. The shared components live
    /// in Game/DeskKit.cs — use them, never fork them.
    ///
    /// TWIN LAW: this file and game/src/ui/desks/desk_customers.gd draw the same
    /// rows at the same coordinates.
    /// </summary>
    public static class DeskCustomers
    {
        /// THE FUNNEL BLOCK's own vertical map. The bars are DeskKit's (label
        /// gutter, 40+460 x v/max bar, the number ON the row), so the CAC read
        /// sits BELOW them in a four-cell strip rather than beside them — the
        /// kit's bar row is a full-pane row and a column at x740 would be written
        /// straight through by the widest one.
        const float YFunnel = 316f;
        const float YBars = 348f;
        const float YCac = 480f;
        const float YCacRow = 508f;
        const float YMarket = 544f;
        const float YCohort = 610f;
        const float YLifetime = 644f;
        const float YTruth = 684f;
        const float CacCell = 280f;

        /// <summary>Draw the funnel reads at their analytics gates (non-Enterprise branch).</summary>
        public static void Draw(BinderScreen b)
        {
            GameState st = b.State;
            // THE BRANCH IS THE PLANTED SEAM: an Enterprise run's customers page is
            // the pipeline's stage board, drawn by its own lane, and NEITHER LANE
            // EDITS THE OTHER'S FILE. The pipeline says when it owns the page
            // (OwnsPage) — until it does, this branch never fires and the funnel
            // page below is what every run gets, exactly as it does today.
            if (DeskPipeline.OwnsPage(b)) { DrawEnterprise(b); return; }

            b.Icon("customers", 10f, 6f);
            // AN = 0: the fog, word for word as it shipped. Nothing about the
            // funnel exists for a founder who never bought the means to see it.
            if (st.AnalyticsLevel <= 0)
            {
                b.L(st.Traction + " customers, give or take.", 100f, 10f, 46f);
                b.L("Traffic seems… decent? Someone signed up on Tuesday. The numbers live in a "
                    + "notebook you lost.", 10f, 110f, 30f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f));
                b.L("(invest in analytics to see the funnel)", 10f, 210f, 26f, DrawnUI.Coral);
                Footer(b, st);
                return;
            }
            int an = SimFunnel.Analytics(st);
            b.L(st.Traction + " customers", 100f, 10f, 46f);
            b.L("customers, weekly:", 10f, 100f, 24f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f));
            // 30px reclaimed from the spark buys the funnel block below it
            b.Spark("customers", 10f, 130f, 1120f, 170f, DrawnUI.Sage);
            if (an >= 1)
            {
                Funnel(b, st);
                Cac(b, st);
            }
            else
            {
                // the level is bought but the ERA refuses it: say which, so the
                // player knows this is a stage gate and not a broken page
                b.L("the funnel is dark here: attribution needs an office, not a garage.",
                    10f, YFunnel, 26f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f));
            }
            // WORKING ASSUMPTIONS (owner: nobody knows their TAM on day one): the
            // binder shows what the founder BELIEVES; operating and analytics refine it
            double tam = st.Beliefs != null && st.Beliefs.Tam > 0.0
                ? st.Beliefs.Tam : (st.Theta != null ? st.Theta.Tam : 100000.0);
            double life = st.Beliefs != null && st.Beliefs.LifetimeWk > 0.0
                ? st.Beliefs.LifetimeWk : (st.Theta != null ? st.Theta.LifetimeWk : 40.0);
            float yMarket = an >= 1 ? YMarket : 356f;
            b.L(string.Format(CultureInfo.InvariantCulture,
                "market, as you believe it: ~{0} buyers ({1:0.0}% reached) · a customer stays ≈ {2} wks",
                GameUi.Money(Gd.ToInt(tam)), st.Traction / Gd.Maxf(tam, 1.0) * 100.0, Gd.ToInt(life)),
                10f, yMarket, 27f);
            b.L("working assumptions — they sharpen as you learn", 10f, yMarket + 34f, 22f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f));
            if (an >= 2) Cohort(b, st);
            if (an >= 3) Truth(b, st);
            Footer(b, st);
        }

        /// <summary>THE DESK STATES ITS OWN LAWS (2.7). Every other desk ends on its
        /// lesson and this one used to end on whatever the last analytics gate
        /// happened to unlock — a page whose bottom edge moved with a purchase. The
        /// rules line is the funnel's whole pedagogy in one breath; the warning
        /// outranks it when money is buying nobody, which is the one thing on this
        /// page a founder must not scroll past.</summary>
        static void Footer(BinderScreen b, GameState st)
        {
            Dictionary<string, double> f = SimFunnel.Funnel(st);
            string warning = "";
            for (int i = 0; i < SimFunnel.Mix.Length; i++)
            {
                string k = SimFunnel.Mix[i];
                if (SimFunnel.Num(f, "cac_" + k) <= 0.0
                    && SimFunnel.Num(f, "spend_" + k) >= SimFunnel.BurnSpend)
                {
                    warning = k.ToUpper() + " is BURNING: $"
                        + GameUi.Money(Gd.ToInt(SimFunnel.Num(f, "spend_" + k)))
                        + "/wk bought nobody last week — a channel with no CAC has no price";
                    break;
                }
            }
            DeskKit.Footer(b, "",
                "the rules of this desk: REACH is what money bought · a LEAD is reach that answered · "
                + "only closing capacity signs them · CAC is spend ÷ signed, per channel · churn is a leaky bucket, and care patches it",
                warning);
        }

        /// <summary>
        /// THE FUNNEL, three pen strokes of shrinking length — the SHAPE is the
        /// lesson before any number lands. REACH is what money bought; LEADS is
        /// what converted; SIGNED is what the closing capacity actually let in.
        /// </summary>
        static void Funnel(BinderScreen b, GameState st)
        {
            Dictionary<string, double> f = SimFunnel.Funnel(st);
            b.L("the funnel, last week:", 10f, YFunnel, 24f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f));
            if (f.Count == 0)
            {
                DeskKit.Empty(b, 10f, YBars,
                    "no week on the books yet — the funnel is measured, not predicted.",
                    "lock in a week and this fills with reach, leads and signings.");
                return;
            }
            double reach = SimFunnel.Num(f, "reach_total");
            double leads = SimFunnel.Num(f, "leads_total");
            double signed = SimFunnel.Num(f, "adds");
            double closeRate = SimFunnel.Num(f, "close_rate");
            string signedTxt = Gd.RoundToInt(signed).ToString(CultureInfo.InvariantCulture);
            if (closeRate < 0.9)
                signedTxt += " · ceiling " + Gd.RoundToInt(SimFunnel.Num(f, "gtm_cap")) + " hit";
            DeskKit.Bars(b, 10f, YBars, new List<DeskKit.BarRow>
            {
                new DeskKit.BarRow { Label = "reach", Value = (float)reach, Col = DrawnUI.Blue,
                    Text = GameUi.Money(Gd.RoundToInt(reach)) + " bought" },
                new DeskKit.BarRow { Label = "leads", Value = (float)leads, Col = DrawnUI.Yellow,
                    Text = string.Format(CultureInfo.InvariantCulture, "{0} · conv {1:0.0}%",
                        Gd.RoundToInt(leads), SimFunnel.Num(f, "conv") * 100.0) },
                new DeskKit.BarRow { Label = "signed", Value = (float)signed, Col = DrawnUI.Sage,
                    Text = signedTxt },
            }, 46f);
        }

        /// <summary>
        /// PER-CHANNEL CAC is the teaching heart of this desk: which dollar buys a
        /// customer cheapest. One coral word does the alarm work.
        /// </summary>
        static void Cac(BinderScreen b, GameState st)
        {
            Dictionary<string, double> f = SimFunnel.Funnel(st);
            if (f.Count == 0) return;
            b.L("CAC by channel — what one customer cost, and which dollar bought them",
                10f, YCac, 22f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f));
            float x = 10f;
            for (int i = 0; i < SimFunnel.Mix.Length; i++)
            {
                string k = SimFunnel.Mix[i];
                double spend = SimFunnel.Num(f, "spend_" + k);
                double cac = SimFunnel.Num(f, "cac_" + k);
                string txt = k + " —";
                Color col = DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f);
                if (cac > 0.0)
                {
                    txt = k + " $" + GameUi.Money(Gd.RoundToInt(cac))
                          + " · $" + GameUi.Money((int)spend) + "/wk";
                    col = DrawnUI.WithAlpha(DrawnUI.Ink, 0.85f);
                }
                else if (spend >= SimFunnel.BurnSpend)
                {
                    // money that bought nobody, said out loud, in the one alarm colour
                    txt = k + " burning · $" + GameUi.Money((int)spend) + "/wk";
                    col = DrawnUI.Coral;
                }
                else if (spend > 0.0)
                {
                    txt = k + " — · $" + GameUi.Money((int)spend) + "/wk";
                }
                b.L(txt, x, YCacRow, 24f, col, CacCell - 10f);
                x += CacCell;
            }
        }

        /// <summary>
        /// RETENTION, by its real name. The second analytics level buys the cohort
        /// read: what a hundred customers from twelve weeks ago look like today.
        /// </summary>
        static void Cohort(BinderScreen b, GameState st)
        {
            Theta th = st.Theta ?? new Theta();
            double residence = Gd.Maxf(th.LifetimeWk * (0.4 + st.Product / 100.0 * 1.2), 2.0);
            double careCut = 30.0 * (1.0 - Math.Exp(-(st.Budgets != null ? st.Budgets.Care : 0) / 1500.0));
            double churnWk = 100.0 / residence * th.ChurnMult * (1.0 - careCut / 100.0);
            double survive = Math.Pow(Gd.Maxf(1.0 - 1.0 / residence, 0.0), 12.0) * 100.0;
            b.L(string.Format(CultureInfo.InvariantCulture,
                "of 100 who joined 12 wks ago, ~{0} are still here · churn {1:0.0}%/wk · care trims {2}%",
                Gd.RoundToInt(survive), churnWk, Gd.RoundToInt(careCut)), 10f, YCohort, 26f);
            int cac = Gd.ToInt(UnitEcon(st, "cac"));
            int pb = Gd.ToInt(UnitEcon(st, "payback_wk"));
            b.L(string.Format(CultureInfo.InvariantCulture,
                "lifetime ≈ {0} wks at v0.{1} · blended CAC {2} · payback {3}",
                Gd.RoundToInt(residence), st.Product,
                cac > 0 ? "$" + GameUi.Money(cac) : "—",
                pb > 0 ? pb + " wks" : "—"), 10f, YLifetime, 24f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.8f));
        }

        /// <summary>
        /// LEVEL 3 PAYS IN DATA, not a compliment: the real market against the
        /// believed one, the two rates that decide everything, and the channel to
        /// put money in.
        /// </summary>
        static void Truth(BinderScreen b, GameState st)
        {
            Theta th = st.Theta ?? new Theta();
            Dictionary<string, double> f = SimFunnel.Funnel(st);
            string best = "";
            double bestCac = 0.0;
            for (int i = 0; i < SimFunnel.Mix.Length; i++)
            {
                double c = SimFunnel.Num(f, "cac_" + SimFunnel.Mix[i]);
                if (c > 0.0 && (best.Length == 0 || c < bestCac)) { best = SimFunnel.Mix[i]; bestCac = c; }
            }
            double believed = st.Beliefs != null && st.Beliefs.Tam > 0.0 ? st.Beliefs.Tam : th.Tam;
            b.L(string.Format(CultureInfo.InvariantCulture,
                "the truth: ~{0} buyers (you believed {1}) · conv {2:0.0}% · close {3}% · {4}",
                GameUi.Money(Gd.ToInt(th.Tam)), GameUi.Money(Gd.ToInt(believed)),
                SimFunnel.Num(f, "conv") * 100.0,
                Gd.RoundToInt(SimFunnel.Num(f, "close_rate") * 100.0),
                // ONE MEASURED LINE at 1100px: the desk's own law line sits 50px
                // under this one, and the long form of the tail wrapped into it.
                best.Length > 0
                    ? "cheapest customer: " + best.ToUpper()
                    : "no channel has bought a customer yet"),
                10f, YTruth, 26f, DrawnUI.Sage, 1100f);
        }

        /// <summary>
        /// SimEngine parks the week's unit economics on the state as ONE nested
        /// map. Fresh from the tick it is a Dictionary; loaded back off disk
        /// Newtonsoft hands it over as a JObject — so this reads both and answers
        /// 0 for neither (the binder's own UnitEcon reader, which is private to it).
        /// </summary>
        static double UnitEcon(GameState st, string key)
        {
            object box = st.GetMeta("unit_econ", null);
            if (box == null) return 0.0;
            var dict = box as IDictionary<string, object>;
            if (dict != null)
            {
                object v;
                if (!dict.TryGetValue(key, out v) || v == null) return 0.0;
                double d;
                return double.TryParse(v.ToString(), NumberStyles.Any,
                                       CultureInfo.InvariantCulture, out d) ? d : 0.0;
            }
            var jo = box as Newtonsoft.Json.Linq.JObject;
            return jo != null ? ContentDb.Num(jo, key, 0.0) : 0.0;
        }

        /// <summary>A press inside this desk. `id` is whatever Draw registered.</summary>
        public static void Handle(BinderScreen b, string id)
        {
        }

        /// <summary>
        /// THE ENTERPRISE BRANCH belongs to the pipeline lane, drawn inside this
        /// desk. The call site is planted so neither lane edits the other's file.
        /// </summary>
        public static void DrawEnterprise(BinderScreen b)
        {
            DeskPipeline.DrawBoard(b);
        }
    }
}
