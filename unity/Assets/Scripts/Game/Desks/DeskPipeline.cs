using System.Collections.Generic;
using System.Globalization;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — the binder's `customers` tab, Enterprise branch.
    /// Spec: docs/design/05-enterprise-pipeline.md section 12
    ///
    /// DeskCustomers dispatches the Enterprise page here and passes the
    /// BinderScreen ITSELF, so this file draws through the binder's own helpers
    /// and never reaches into the sheet directly.
    ///
    /// WHAT THIS PAGE IS: the founder's wall calendar. Named accounts sitting in
    /// named gates, each with its size, its warmth and its age; the logos already
    /// signed; and one blue line at the bottom that names win rate, sales cycle
    /// and cost per signed seat using the run's OWN numbers. There are no
    /// controls at all — a deal is pushed by a written move (push_lead), never by
    /// a button — and that absence is the lesson: enterprise sales is attention,
    /// not clicking.
    ///
    /// FOG OF WAR DOES NOT APPLY HERE. The board renders at analytics 0, because
    /// the pipeline is the founder's own calendar: fog hides what the MARKET is
    /// doing, never what is on your own desk. The believed-market lines keep
    /// their gates on the funnel branch next door.
    ///
    /// The bar every surface ships at (00-spine section 11): readable first pass
    /// by a tired player; concepts named in real business terms with a teaching
    /// line where a number first appears; no dead ends and every state leavable;
    /// drawn in the game's hand, never a SaaS panel. The shared components live
    /// in Game/DeskKit.cs — the stage board is DeskKit.Board().
    ///
    /// TWIN LAW: this file and game/src/ui/desks/desk_pipeline.gd draw the same
    /// rows at the same coordinates.
    /// </summary>
    public static class DeskPipeline
    {
        /// <summary>
        /// Does the pipeline own the customers page on this run? THE HANDOVER:
        /// the board is real now, so an Enterprise run gets it and every other
        /// run keeps today's funnel page, untouched. Nobody had to edit
        /// DeskCustomers.cs for this.
        /// </summary>
        public static bool OwnsPage(BinderScreen b)
        {
            return b != null && b.State != null && b.State.BizWho == "Enterprise";
        }

        /// <summary>Draw the stage board, lead chips, signed-logos strip and teaching footer.</summary>
        public static void Draw(BinderScreen b)
        {
            DrawBoard(b);
        }

        /// <summary>A press inside this desk. There are none: the board carries no
        /// controls by design (spec section 12) — the pipeline moves on written
        /// moves and on the dice.</summary>
        public static void Handle(BinderScreen b, string id)
        {
        }

        /// <summary>Drawn INSIDE the customers desk on Enterprise runs.</summary>
        public static void DrawBoard(BinderScreen b)
        {
            GameState st = b.State;
            b.Icon("customers", 10f, 6f);
            b.L(string.Format(CultureInfo.InvariantCulture, "{0} customers · {1} logos signed",
                st.Traction, st.Logos.Count), 100f, 6f, DeskKit.HeroSize);
            b.L(string.Format(CultureInfo.InvariantCulture,
                "the pipeline — {0} live · {1} seats in motion · pool {2} waiting",
                st.Leads.Count, SimPipeline.SeatsInMotion(st), Gd.F(st.PipeUnits, 0)),
                100f, 64f, 24f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 1020f);

            // THE STAGE BOARD — the mental model being taught. The columns ARE
            // the lesson: a deal is not "in the funnel", it is sitting at a named
            // gate waiting to clear it. PROCUREMENT appears from the office era,
            // exactly when deal size makes a buyer's IT department wake up.
            int hidden;
            List<DeskKit.Column> cols = Columns(st, out hidden);
            float y = DeskKit.Board(b, 96f, cols, string.Format(CultureInfo.InvariantCulture,
                "no deals on the board yet — marketing books the meetings, and {0} seats of interest are already waiting in the pool",
                Gd.F(st.PipeUnits, 0)));
            if (hidden > 0)
            {
                y = DeskKit.More(b, DeskKit.XId, y, hidden, "sit deeper in those columns");
            }

            // THE SIGNED LOGOS. Closed business stays visible as named accounts —
            // and at floor/hq a renewal announces itself before it arrives.
            DeskKit.Rule(b, 596f);
            b.L(LogoStrip(st), DeskKit.XId, 610f, 22f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.75f), 1100f);

            // THE TEACHING FOOTER — win rate, sales cycle, CAC-per-seat against
            // what a seat actually pays, in the run's own numbers, with the era's
            // coach line under it saying what this pipeline can and cannot do.
            string coach;
            if (!SimPipeline.COACH.TryGetValue(st.Era ?? "garage", out coach)) { coach = ""; }
            DeskKit.Footer(b, FooterLine(st), coach, "");
        }

        // ── the board ────────────────────────────────────────────────────
        /// <summary>One column per live stage, each holding its deals
        /// hottest-first. Four chips fit a column at the kit's pitch; the rest
        /// are counted honestly underneath.</summary>
        private static List<DeskKit.Column> Columns(GameState st, out int hidden)
        {
            hidden = 0;
            string[] stages = st.EraIndex() >= 2
                ? new[] { "meeting", "pilot", "procurement", "contract" }
                : new[] { "meeting", "pilot", "contract" };
            int decay = SimPipeline.DecayFor(st);
            List<int> order = SimPipeline.LeadsByHeat(st);
            var cols = new List<DeskKit.Column>();
            foreach (string stage in stages)
            {
                var col = new DeskKit.Column { Head = stage };
                foreach (int i in order)
                {
                    Lead lead = st.Leads[i];
                    if ((lead.Stage ?? "meeting") != stage) { continue; }
                    if (col.Chips.Count >= 4) { hidden += 1; continue; }
                    // THE HEAT WORD WEARS THE RAMP (1.1, and 05 §12): coral, yell,
                    // sage — ONE word, never the line. Folded into Facts it was just
                    // more ink, and the whole point of the chip — is this deal
                    // warming or dying — went grey.
                    var chip = new DeskKit.Chip
                    {
                        Name = lead.Name,
                        FactsLead = string.Format(CultureInfo.InvariantCulture, "{0} seats", lead.Seats),
                        Heat = SimPipeline.HeatWord(lead.Heat),
                        Facts = string.Format(CultureInfo.InvariantCulture, "wk {0}", lead.AgeWeeks),
                        Flavor = lead.Flavor ?? "",
                    };
                    // the coral clock: a deal two weeks from dying of no-decision says so
                    int dies = SimPipeline.WeeksToCold(lead.Heat, decay);
                    if (dies <= 2)
                    {
                        chip.Note = string.Format(CultureInfo.InvariantCulture, "dies in {0} wk{1}",
                            dies, dies == 1 ? "" : "s");
                    }
                    col.Chips.Add(chip);
                }
                cols.Add(col);
            }
            return cols;
        }

        // ── the logos strip ──────────────────────────────────────────────
        /// <summary>Biggest accounts first, with a renewal countdown once one is
        /// close enough to plan around (floor/hq only — before that there is no
        /// annual contract to lose).</summary>
        private static string LogoStrip(GameState st)
        {
            if (st.Logos.Count == 0)
            {
                return "logos: none signed yet — a contract is the only way an enterprise customer arrives";
            }
            var idx = new List<int>();
            for (int i = 0; i < st.Logos.Count; i++) { idx.Add(i); }
            idx.Sort((a, c) =>
            {
                int sa = st.Logos[a].Seats;
                int sc = st.Logos[c].Seats;
                if (sa != sc) { return sc.CompareTo(sa); }
                return a.CompareTo(c);
            });
            var parts = new List<string>();
            for (int n = 0; n < Gd.Mini(idx.Count, 8); n++)
            {
                Logo lg = st.Logos[idx[n]];
                int due = lg.RenewalWk - st.Week;
                parts.Add(due > 0 && due <= 4
                    ? string.Format(CultureInfo.InvariantCulture, "{0} ({1}, renews in {2} wks)",
                        lg.Name, lg.Seats, due)
                    : string.Format(CultureInfo.InvariantCulture, "{0} ({1})", lg.Name, lg.Seats));
            }
            string outp = "logos: " + string.Join(" · ", parts.ToArray());
            if (idx.Count > 8)
            {
                outp += string.Format(CultureInfo.InvariantCulture, " · +{0} more", idx.Count - 8);
            }
            return outp;
        }

        // ── the teaching footer ──────────────────────────────────────────
        /// <summary>The four numbers an enterprise founder has to learn to say out
        /// loud. Each one stays a "?" until the run has actually earned it — a
        /// made-up win rate teaches worse than an honest blank.</summary>
        private static string FooterLine(GameState st)
        {
            PipeStats ps = SimPipeline.Stats(st);
            int decided = ps.Signed + ps.Lost;
            string win = decided <= 0 ? "?" : string.Format(CultureInfo.InvariantCulture,
                "{0}/{1} ({2}%)", ps.Signed, decided,
                Gd.RoundToInt(100.0 * ps.Signed / decided));
            string cycle = ps.Signed <= 0 ? "?"
                : Gd.RoundToInt((double)ps.CycleSum / ps.Signed).ToString(CultureInfo.InvariantCulture);
            string cost = ps.SeatsSigned <= 0 ? "?"
                : "$" + Gd.RoundToInt(ps.Spend / ps.SeatsSigned).ToString(CultureInfo.InvariantCulture);
            return string.Format(CultureInfo.InvariantCulture,
                "win rate {0} · avg cycle {1} wks · cost per signed seat ≈ {2} · a seat pays ≈ ${3}/wk",
                win, cycle, cost, Gd.F(SimPipeline.UnitRevWk(st), 0));
        }
    }
}
