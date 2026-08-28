using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — REVENUE · "customers" = THE SCOREBOARD (DECISIONS: owner pick E).
    /// Twin of game/src/ui/desks/desk_customers_page.gd — same rows, same
    /// words, same fog.
    /// THE QUESTION THIS DESK ANSWERS: "who is coming and staying?"
    ///
    /// The hero is the score: the count big, this week's +won/−lost colored,
    /// the kept% beside them, the run-long chart as a wash. Under it: THE
    /// FUNNEL, SMALL and WHAT ONE IS WORTH, plus the audience-density card —
    /// Consumer = cohort retention bars · SMB = the biggest-5 strip ·
    /// Enterprise = the logo grid with seats + renewal clocks.
    ///
    /// ANALYTICS FOG RULES PRESERVED: what the founder cannot see renders as
    /// "?" shapes, never as absence. The Enterprise STAGE BOARD lives on
    /// "in motion" now — this page keeps the score for every audience.
    ///
    /// DAG3 (13-binder-ux · customers): the fog line sells its own unlock —
    /// one press lands on the spend book (desk-level until the spend lane
    /// names the analytics line's control id); kept% is pressable into the
    /// cohort receipt; won/lost wear the S5 arrows; week one is the S1
    /// teaching state.
    /// </summary>
    public static class DeskCustomersPage
    {
        public const string Question = "who is coming and staying?";

        const float CardsY = 330f;
        const float LeftX = 10f;
        const float LeftW = 468f;
        const float RightX = 492f;
        const float RightW = 638f;

        static readonly Color Pos = DrawnUI.Hex("5D7A50");

        // ── the dispatch ───────────────────────────────────────────────────

        /// The quartet card IS the page's hero verbatim (DECISIONS).
        public static string[] HeroSummary(GameState s)
        {
            int won, lost, kept;
            string line = Score(s, out won, out lost, out kept);
            return new[] { s.Traction + " customers", line };
        }

        public static void Draw(BinderScreen b)
        {
            GameState s = b.State;
            if (s.Traction <= 0 && s.MetricHistory.Count == 0)
            {
                // S1 — the desk before anyone has arrived is a TEACHING state
                DeskKit.ZeroState(b, new DeskKit.ZeroStateCfg
                {
                    WillShow = "who is coming and staying — the score",
                    WouldLine = "the count big, each week's +won and −lost beside it, and how "
                        + "many of a class of 100 are still here twelve weeks on",
                    ActionLabel = "fund the first channel",
                    ActionCb = () => b.FocusDesk("growth", "", "customers"),
                    WakesHint = "wakes with the first locked week — analytics decides how much "
                        + "of it you can see",
                });
                return;
            }
            int an = SimFunnel.Analytics(s);
            int won, lost, kept;
            string line = Score(s, out won, out lost, out kept);
            Hero(b, s, an, won, lost, kept);
            FunnelCard(b, s, an);
            float y = WorthCard(b, s, an);
            DensityCard(b, s, an, y);
            Foot(b, s);
        }

        public static void Handle(BinderScreen b, string id)
        {
        }

        // ── the score ──────────────────────────────────────────────────────

        /// This week's score, from the funnel and the binder's own history.
        /// The fog hides COUNTS, never events: what the line claims tracks the
        /// week's real arrivals, and only the precision is lost with the notebook.
        static string FogLine(int won)
        {
            if (won == 1)
                return "Someone signed up on Tuesday. The exact trail lives in a notebook you lost.";
            if (won >= 2)
                return "A handful of signups this week — the exact count lives in a notebook you lost.";
            return "Traffic seems… quiet. Nobody new this week, as far as you can tell. The numbers live in a notebook you lost.";
        }

        static string Score(GameState s, out int won, out int lost, out int kept)
        {
            Dictionary<string, double> f = SimFunnel.Funnel(s);
            won = -1;
            lost = -1;
            if (f.Count > 0)
            {
                won = Gd.RoundToInt(SimFunnel.Num(f, "adds"));
                int n = s.MetricHistory.Count;
                if (n >= 2)
                {
                    int prev = s.MetricHistory[n - 2].Customers;
                    lost = Math.Max(prev + won - s.Traction, 0);
                }
            }
            kept = KeptPct(s);
            var parts = new List<string>();
            parts.Add(won >= 0
                ? "+" + won + " won · −" + (lost >= 0 ? lost.ToString(CultureInfo.InvariantCulture) : "?") + " lost this week"
                : "no week on the books yet");
            parts.Add(kept >= 0
                ? "kept ≈" + kept + "% after 12 weeks"
                : "kept ? — analytics sees who stays");
            return string.Join(" · ", parts);
        }

        /// Survival of a class of 100 at week 12 — engine terms, no invention.
        static int KeptPct(GameState s)
        {
            if (SimFunnel.Analytics(s) < 2) return -1;
            Theta th = s.Theta ?? new Theta();
            double residence = Gd.Maxf(th.LifetimeWk * (0.4 + s.Product / 100.0 * 1.2), 2.0);
            return Gd.RoundToInt(Math.Pow(Gd.Maxf(1.0 - 1.0 / residence, 0.0), 12.0) * 100.0);
        }

        // ── the hero ───────────────────────────────────────────────────────

        static void Hero(BinderScreen b, GameState s, int an, int won, int lost, int kept)
        {
            string big = s.Traction.ToString(CultureInfo.InvariantCulture);
            if (an <= 0) big += ", give or take";
            b.L(big, DeskKit.XId, 6f, DeskKit.HeroBig, DrawnUI.Ink, 700f);
            // structural measure stand-in (the ChipToken idiom): the display
            // hand is not loaded headless, so the twin spaces by count
            float bx = DeskKit.XId + big.Length * 30f + 30f;
            b.L("customers", bx, 34f, DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 200f);
            if (won >= 0)
            {
                b.L("+" + won + " won", bx + 150f, 10f, 27f, Pos, 200f);
                b.L("−" + (lost >= 0 ? lost.ToString(CultureInfo.InvariantCulture) : "?") + " lost",
                    bx + 150f, 44f, 27f, DrawnUI.Coral, 200f);
                // S5 — the hero's arrow: won against the binder's last open.
                // R4 — the lost line's second arrow died; a moved lost count
                // is a gutter dot (coral when losses grew) and the arbiter
                // keeps the worst row (R2).
                string wprev = b.SeenPrev("customers", "won");
                int wp;
                if (b.Seen("customers", "won", won.ToString(CultureInfo.InvariantCulture))
                    && int.TryParse(wprev, out wp))
                {
                    float ww = DrawnUI.MeasureWidth("+" + won + " won", 27f);
                    DeskKit.DeltaArrow(b, bx + 150f + ww + 8f, 14f, won, wp);
                }
                if (lost >= 0)
                {
                    string lprev = b.SeenPrev("customers", "lost");
                    int lp;
                    if (b.Seen("customers", "lost", lost.ToString(CultureInfo.InvariantCulture))
                        && int.TryParse(lprev, out lp))
                        DeskKit.PenCircle(b, new Rect(bx + 150f, 44f, 120f, 30f),
                            lost > lp, Mathf.Abs(lost - lp));
                }
            }
            if (kept >= 0)
            {
                // S4 — kept% IS its own receipt: the cohort's terms one press down
                string ktext = "kept ≈" + kept + "%";
                float ktw = DrawnUI.MeasureWidth(ktext, 34f);
                DeskKit.ReceiptNumber(b, 830f + 290f - ktw, 10f, ktext, 34f, DrawnUI.Sage,
                    "kept — a class of 100", CohortLines(s));
            }
            else
            {
                TextMeshProUGUI kl = b.L("kept ?", 830f, 10f, 34f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.4f), 290f);
                kl.alignment = TextAlignmentOptions.TopRight;
            }
            TextMeshProUGUI ks = b.L(kept >= 0 ? "still here after 12 weeks"
                : "invest in analytics to see who stays", 700f, 52f, 17f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 420f);
            ks.alignment = TextAlignmentOptions.TopRight;
            // S2 — red speaks ON the page: this desk's asks in one measured
            // line. R5 — the strip renders in its own slot (96-118); nothing
            // below shifts.
            DeskKit.AskStrip(b, "customers", DeskKit.XId, 84f, 1100f,
                "the doors are on IN MOTION and GROWTH");
            if (an <= 0)
            {
                b.L(FogLine(won),
                    DeskKit.XId, DeskKit.ContentY0, DeskKit.Detail,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 1100f);
                // S2 — the fog sells its own unlock: one press lands on the
                // spend book. R6 — coral, not the alarm red: the strip is the
                // pane's one red line, and this is a door, not an alarm.
                DeskKit.Word(b, "the notebook is for sale — fund analytics on the spend book ->",
                    DeskKit.XId, DeskKit.ContentY0 + 32f, () => b.FocusDesk("spend", "", "customers"),
                    DeskKit.Detail, DrawnUI.Coral, 900f);
                DeskKit.PenRule(b, DeskKit.ContentY0 + 70f);
                b.L("the whole run — the chart returns with a notebook that survives (analytics)",
                    DeskKit.XId, DeskKit.ContentY0 + 84f, DeskKit.Law,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.4f), 1100f);
                return;
            }
            b.L("the whole run", DeskKit.XId, DeskKit.ContentY0, DeskKit.Law,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f), 400f);
            DeskKit.PenRule(b, 150f);
            b.Spark("customers", 10f, 166f, 1120f, 148f, DrawnUI.Sage);
        }

        /// S4 — the cohort receipt's lines: the same math KeptPct runs, said
        /// in its terms. The probe photographs exactly this content.
        static List<DeskKit.TicketLine> CohortLines(GameState s)
        {
            Theta th = s.Theta ?? new Theta();
            double lift = 0.4 + s.Product / 100.0 * 1.2;
            double residence = Gd.Maxf(th.LifetimeWk * lift, 2.0);
            double keep = Gd.Maxf(1.0 - 1.0 / residence, 0.0);
            return new List<DeskKit.TicketLine>
            {
                new DeskKit.TicketLine { Label = "one stays about",
                    Value = Gd.RoundToInt(residence) + " wks" },
                new DeskKit.TicketLine { Label = "of 100 who join, week 4",
                    Value = Gd.RoundToInt(Math.Pow(keep, 4.0) * 100.0) + " left" },
                new DeskKit.TicketLine { Label = "week 8",
                    Value = Gd.RoundToInt(Math.Pow(keep, 8.0) * 100.0) + " left" },
                new DeskKit.TicketLine { Label = "week 12",
                    Value = Gd.RoundToInt(Math.Pow(keep, 12.0) * 100.0) + " left",
                    Col = DrawnUI.Sage },
                new DeskKit.TicketLine { Label = "product v0." + s.Product + " lifts residence",
                    Value = string.Format(CultureInfo.InvariantCulture, "×{0:0.0}", lift) },
            };
        }

        /// S8 — the scoreboard never sleeps; the S1 state teaches week one.
        public static bool IsDormant(GameState s)
        {
            return false;
        }

        /// S8 — the rail's four-character read: the count, plainly.
        public static string MicroStatus(GameState s)
        {
            return s.Traction > 0 ? s.Traction.ToString(CultureInfo.InvariantCulture) : "";
        }

        // ── the funnel, small ──────────────────────────────────────────────

        /// Four narrowing mouths; a fogged stage keeps its mouth, loses its number.
        static void FunnelCard(BinderScreen b, GameState s, int an)
        {
            DeskKit.CardBox frame = DeskKit.CardFrame(b, LeftX, CardsY, LeftW, 384f,
                "the funnel, small");
            float fx = frame.ContentX;
            float fy = frame.ContentY;
            Dictionary<string, double> f = SimFunnel.Funnel(s);
            bool known = an >= 1 && f.Count > 0;
            var stages = new List<DeskKit.Stage>
            {
                new DeskKit.Stage { Label = "reach",
                    ValueText = GameUi.Money(Gd.RoundToInt(SimFunnel.Num(f, "reach_total"))), Known = known },
                new DeskKit.Stage { Label = "leads",
                    ValueText = GameUi.Money(Gd.RoundToInt(SimFunnel.Num(f, "leads_total"))), Known = known },
                new DeskKit.Stage { Label = "signed",
                    ValueText = GameUi.Money(Gd.RoundToInt(SimFunnel.Num(f, "adds"))), Known = known },
                new DeskKit.Stage { Label = "kept",
                    ValueText = GameUi.Money(s.Traction), Known = true },
            };
            fy = DeskKit.FunnelShape(b, fx, fy, LeftW - DeskKit.CardPad * 2f, stages);
            if (!known)
            {
                string why = s.AnalyticsLevel <= 0
                    ? "invest in analytics to see the funnel"
                    : (an < 1
                        ? "the funnel is dark here: attribution needs an office, not a garage."
                        : "no week on the books yet — the funnel is measured, not predicted.");
                // the caption stays INSIDE the card whatever height the funnel drew
                b.L(why, fx, Math.Min(fy - 4f, CardsY + 384f - 56f), DeskKit.Law,
                    s.AnalyticsLevel <= 0 ? DrawnUI.Coral : DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f),
                    LeftW - 60f);
            }
        }

        // ── what one is worth ──────────────────────────────────────────────

        static float WorthCard(BinderScreen b, GameState s, int an)
        {
            DeskKit.CardBox frame = DeskKit.CardFrame(b, RightX, CardsY, RightW, 196f,
                "what one is worth");
            bool fogged = an < 1;
            double arpu = UnitEcon(s, "arpu");
            int cac = Gd.ToInt(UnitEcon(s, "cac"));
            int ltv = Gd.ToInt(UnitEcon(s, "ltv"));
            Theta th = s.Theta ?? new Theta();
            int stays = Gd.ToInt(s.Beliefs != null && s.Beliefs.LifetimeWk > 0.0
                ? s.Beliefs.LifetimeWk : th.LifetimeWk);
            Color fogCol = DrawnUI.WithAlpha(DrawnUI.Ink, 0.4f);
            DeskKit.MoneyRow(b, frame, "pays weekly / costs to win (CAC)",
                fogged ? "?" : string.Format(CultureInfo.InvariantCulture, "${0:0} / {1}",
                    arpu, cac > 0 ? "$" + GameUi.Money(cac) : "?"),
                fogged ? fogCol : DrawnUI.Ink);
            DeskKit.MoneyRow(b, frame, "stays about",
                fogged ? "?" : stays + " wks", fogged ? fogCol : DrawnUI.Ink);
            DeskKit.MoneyRow(b, frame, "worth, lifetime (LTV)",
                fogged ? "?" : (ltv > 0 ? "$" + GameUi.Money(ltv) : "?"),
                !fogged && ltv > 0 ? Pos : fogCol);
            return frame.Bottom;
        }

        // ── the audience-density card ──────────────────────────────────────

        static void DensityCard(BinderScreen b, GameState s, int an, float top)
        {
            float y = top + 14f;
            const float H = 150f;
            switch (s.BizWho)
            {
                case "Enterprise": LogoGrid(b, s, y, H); break;
                case "SMB": BiggestFive(b, s, y, H); break;
                default: CohortBars(b, s, an, y, H); break;
            }
        }

        /// CONSUMER: cohort retention bars — a class of 100, k weeks later.
        static void CohortBars(BinderScreen b, GameState s, int an, float y, float h)
        {
            DeskKit.CardBox frame = DeskKit.CardFrame(b, RightX, y, RightW, h,
                "the cohorts — a class of 100");
            float cx = frame.ContentX;
            float cy = frame.ContentY - 6f;
            bool known = an >= 2;
            Theta th = s.Theta ?? new Theta();
            double residence = Gd.Maxf(th.LifetimeWk * (0.4 + s.Product / 100.0 * 1.2), 2.0);
            int[] weeks = { 4, 8, 12, 16 };
            for (int i = 0; i < weeks.Length; i++)
            {
                double frac = Math.Pow(Gd.Maxf(1.0 - 1.0 / residence, 0.0), weeks[i]);
                string note = "wk " + weeks[i] + " — "
                    + (known ? Gd.RoundToInt(frac * 100.0) + " of 100" : "?") + " still here";
                DeskKit.Meter(b, cx, cy, 300f, known ? (float)frac : 0f,
                    known ? DrawnUI.Sage : DrawnUI.WithAlpha(DrawnUI.Ink, 0.2f), note);
                cy += 24f;
            }
            if (!known)
                b.L("unlocks at analytics 2 — below that nobody counts who stays",
                    cx + 320f, y + 56f, DeskKit.ChipS, DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f), 290f);
        }

        /// SMB: the biggest-5 strip — named when accounts have names, counted
        /// honestly until they do.
        static void BiggestFive(BinderScreen b, GameState s, float y, float h)
        {
            DeskKit.CardBox frame = DeskKit.CardFrame(b, RightX, y, RightW, h,
                "the biggest five");
            float cx = frame.ContentX;
            float cy = frame.ContentY;
            if (s.Logos.Count == 0)
            {
                // Law 2 — the amount rides the card's money column, not a sentence
                double arpu = SimEngine.OffersArpu(s);
                DeskKit.MoneyRow(b, frame, s.Traction + " small shops each pay about",
                    "$" + GameUi.Money(Gd.RoundToInt(Gd.Maxf(arpu, 0.0))) + "/wk",
                    DrawnUI.Ink);
                DeskKit.Empty(b, cx, cy + 48f,
                    "no account big enough to name yet.", "");
                return;
            }
            List<int> idx = LogosBySeats(s);
            float x = cx;
            for (int n = 0; n < idx.Count && n < 5; n++)
            {
                Logo lg = s.Logos[idx[n]];
                x = DeskKit.ChipToken(b, x, cy, new DeskKit.ChipCfg
                {
                    Text = (lg.Name ?? "?") + " — " + lg.Seats,
                    Kind = "person",
                });
            }
        }

        /// ENTERPRISE: the logo grid — seats on every card, the renewal clock
        /// when the pipeline says one is close enough to plan around.
        static void LogoGrid(BinderScreen b, GameState s, float y, float h)
        {
            DeskKit.CardBox frame = DeskKit.CardFrame(b, RightX, y, RightW, h,
                "the logos — " + s.Logos.Count + " signed");
            float cx = frame.ContentX;
            float cy = frame.ContentY;
            if (s.Logos.Count == 0)
            {
                // short enough to live inside the card's width
                DeskKit.Empty(b, cx, cy,
                    "no logos yet.",
                    "contracts come from the IN MOTION board", true);
                return;
            }
            List<int> idx = LogosBySeats(s);
            float x = cx;
            int row = 0;
            int shown = 0;
            for (int n = 0; n < idx.Count; n++)
            {
                if (row >= 2) break;
                Logo lg = s.Logos[idx[n]];
                int due = lg.RenewalWk - s.Week;
                x = DeskKit.ChipToken(b, x, cy, new DeskKit.ChipCfg
                {
                    Text = (lg.Name ?? "?") + " — " + lg.Seats + " seats",
                    Kind = "person",
                });
                if (due > 0 && due <= 4)
                    x = DeskKit.ClockChip(b, x, cy + 3f, "renews " + due + " wk");
                shown++;
                if (x > RightX + RightW - 220f)
                {
                    x = cx;
                    cy += 44f;
                    row++;
                }
            }
            DeskKit.More(b, cx, cy + 44f, idx.Count - shown, "logos hold behind these");
        }

        static List<int> LogosBySeats(GameState s)
        {
            var idx = new List<int>();
            for (int i = 0; i < s.Logos.Count; i++) idx.Add(i);
            idx.Sort((a, c) =>
            {
                int sa = s.Logos[a].Seats;
                int sc = s.Logos[c].Seats;
                if (sa != sc) return sc.CompareTo(sa);
                return a.CompareTo(c);
            });
            return idx;
        }

        // ── the foot ───────────────────────────────────────────────────────

        static void Foot(BinderScreen b, GameState s)
        {
            int an = SimFunnel.Analytics(s);
            Dictionary<string, double> f = SimFunnel.Funnel(s);
            string computed = "";
            if (an >= 3 && f.Count > 0)
            {
                string best = "";
                double bestCac = 0.0;
                for (int i = 0; i < SimFunnel.Mix.Length; i++)
                {
                    double c = SimFunnel.Num(f, "cac_" + SimFunnel.Mix[i]);
                    if (c > 0.0 && (best.Length == 0 || c < bestCac))
                    {
                        best = SimFunnel.Mix[i];
                        bestCac = c;
                    }
                }
                computed = string.Format(CultureInfo.InvariantCulture,
                    "the truth: conv {0:0.0}% · close {1}% · {2}",
                    SimFunnel.Num(f, "conv") * 100.0,
                    Gd.RoundToInt(SimFunnel.Num(f, "close_rate") * 100.0),
                    best.Length > 0 ? "cheapest customer: " + best.ToUpper()
                        : "no channel has bought a customer yet");
            }
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
            DeskKit.Footer(b, computed,
                "the rules of this desk: REACH is what money bought · a LEAD is reach that answered · "
                + "only closing capacity signs them · churn is a leaky bucket, and care patches it",
                warning, 806f, 840f);
        }

        /// The binder's own defensive unit-econ reader (fresh Dictionary or a
        /// loaded JObject — the DeskCustomers.cs idiom, kept private here too).
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
    }
}
