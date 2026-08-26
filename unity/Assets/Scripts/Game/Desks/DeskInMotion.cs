using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — REVENUE · "in motion" = THE TRIPTYCH (DECISIONS: C2×C1 · S1 · E2).
    /// Twin of game/src/ui/desks/desk_in_motion.gd — same pages, same words.
    /// THE QUESTION THIS DESK ANSWERS: "who is on the way to becoming money?"
    ///
    /// AUDIENCE-NATIVE, never stretched: Consumer = THE RIVER × THE SOURCES;
    /// SMB = THE HOT LIST (rank 1 is the week's journal move); Enterprise =
    /// THE STAGE BOARD (columns narrowing, ≤3 cards + "+N", column press
    /// opens the focused list, past ~8 live deals the cards go slim, dying
    /// deal in red). No controls move a deal: pushes ride the journal.
    ///
    /// THE RIVER'S HISTORY: weekly origin splits ride MetricSnapshot
    /// (adds/adds_org/adds_wom/adds_chan). Until the engine writes them (the
    /// lane's coordinator package) old rows draw as ghost bars — the fog is
    /// drawn, never faked; the freshest week always speaks from the funnel.
    /// </summary>
    public static class DeskInMotion
    {
        public const string Question = "who is on the way to becoming money?";

        const int RiverWeeks = 8;
        const int HotShow = 5;
        const int BoardCards = 3;
        const int SlimAt = 9;

        static readonly Color Pos = DrawnUI.Hex("5D7A50");

        // ── the dispatch ───────────────────────────────────────────────────

        public static string[] HeroSummary(GameState s)
        {
            Dictionary<string, double> f = SimFunnel.Funnel(s);
            switch (s.BizWho)
            {
                case "Enterprise":
                    return new[] { s.Leads.Count + " deals",
                        "≈" + SimPipeline.SeatsInMotion(s) + " seats in motion — the stage board" };
                case "SMB":
                    return new[] { SmbInMotion(s, f) + " in motion",
                        "≈" + Gd.RoundToInt(SimFunnel.Num(f, "adds")) + " will land — the hot list" };
                default:
                    return new[] { Gd.RoundToInt(SimFunnel.Num(f, "adds")) + " joining a week",
                        "the river — joiners by origin, word of mouth measured" };
            }
        }

        public static void Draw(BinderScreen b)
        {
            GameState s = b.State;
            string mode = Mode(b);
            switch (s.BizWho)
            {
                case "Enterprise":
                    if (mode.StartsWith("col:", StringComparison.Ordinal))
                        EntColumnFocus(b, s, mode.Substring(4));
                    else Enterprise(b, s);
                    return;
                case "SMB":
                    if (mode == "smb_all") SmbAll(b, s);
                    else Smb(b, s);
                    return;
                default:
                    if (mode == "sources") ConsumerSourcesAll(b, s);
                    else Consumer(b, s);
                    return;
            }
        }

        public static void Handle(BinderScreen b, string id)
        {
        }

        static string Mode(BinderScreen b)
        {
            object mv;
            return b.Desk.TryGetValue("mode", out mv) ? (mv as string ?? "") : "";
        }

        // ═══ CONSUMER ══════════════════════════════════════════════════════

        static void Consumer(BinderScreen b, GameState s)
        {
            Dictionary<string, double> f = SimFunnel.Funnel(s);
            double adds = SimFunnel.Num(f, "adds");
            Dictionary<string, double> prev = SimFunnel.FunnelPrev(s);
            double delta = adds - SimFunnel.Num(prev, "adds");
            string big = Gd.RoundToInt(adds) + " joining a week";
            b.L(big, DeskKit.XId, 6f, DeskKit.HeroBig, DrawnUI.Ink, 700f);
            if (prev.Count > 0)
            {
                float bx = DeskKit.XId + big.Length * 26f + 24f;
                b.L((delta >= 0.0 ? "+" : "−") + Math.Abs(Gd.RoundToInt(delta)) + " vs last week",
                    bx, 22f, 27f, delta >= 0.0 ? Pos : DrawnUI.Coral, 260f);
            }
            double wom = SimFunnel.Num(f, "wom");
            double factor = wom / Gd.Maxf(adds - wom, 1.0);
            TextMeshProUGUI wl = b.L(string.Format(CultureInfo.InvariantCulture,
                "each joiner brings ≈{0:0.0} more", factor), 760f, 10f, 27f, DrawnUI.Ink, 370f);
            wl.alignment = TextAlignmentOptions.TopRight;
            TextMeshProUGUI ws = b.L("word of mouth, measured", 760f, 46f, 17f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 370f);
            ws.alignment = TextAlignmentOptions.TopRight;
            b.L("consumer — nobody has a name until they pay: the page is rates, sources and word of mouth",
                DeskKit.XId, 74f, DeskKit.Law, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 1100f);
            float y = DeskKit.PenRule(b, 112f) + 8f;
            y = RiverCard(b, s, y);
            SourcesCard(b, s, y);
            TasteCard(b, s, y);
            ConsumerFoot(b, s);
        }

        sealed class RiverRow
        {
            public int Wk;
            public bool Known;
            public double Total;
            public double[] Segs = new double[0];   // bought, friends, walked in
        }

        /// THE COHORT RIVER — each bar one week's joiners, stacked by origin.
        static float RiverCard(BinderScreen b, GameState s, float y)
        {
            DeskKit.CardBox frame = DeskKit.CardFrame(b, 10f, y, 1120f, 268f,
                "the weeks flow in — each bar is one week's joiners, colored by where they came from");
            float cx = frame.ContentX;
            float cy = frame.ContentY;
            List<RiverRow> rows = RiverRows(s);
            if (rows.Count == 0)
            {
                DeskKit.Empty(b, cx, cy + 8f,
                    "no week on the books yet — the river is measured, not predicted.",
                    "lock in a week and the first bar arrives");
                return frame.Bottom + 14f;
            }
            double hi = 1.0;
            for (int i = 0; i < rows.Count; i++) hi = Gd.Maxf(hi, rows[i].Total);
            const float RegionH = 126f;
            float cell = (1120f - DeskKit.CardPad * 2f) / rows.Count;
            for (int i = 0; i < rows.Count; i++)
            {
                RiverRow r = rows[i];
                float barH = r.Known ? Mathf.Max(RegionH * (float)(r.Total / hi), 8f) : RegionH * 0.4f;
                float bx = cx + i * cell + (cell - 64f) * 0.5f;
                RiverBar(b, bx, cy + RegionH - barH, 64f, barH, r);
                TextMeshProUGUI nl = b.L(r.Known ? Gd.RoundToInt(r.Total).ToString(CultureInfo.InvariantCulture) : "?",
                    bx - 20f, cy + RegionH + 6f, 20f,
                    i == rows.Count - 1 ? DrawnUI.Ink : DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 104f);
                nl.alignment = TextAlignmentOptions.Top;
                TextMeshProUGUI wl2 = b.L("wk " + r.Wk, bx - 20f, cy + RegionH + 32f, 14f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.4f), 104f);
                wl2.alignment = TextAlignmentOptions.Top;
            }
            b.L("bought", cx, cy + RegionH + 58f, 17f, DrawnUI.Blue, 120f);
            b.L("friends", cx + 100f, cy + RegionH + 58f, 17f, DrawnUI.Yellow, 120f);
            b.L("walked in", cx + 210f, cy + RegionH + 58f, 17f, DrawnUI.Sage, 140f);
            b.L("the river rises when word of mouth does — friends are the middle color",
                cx + 400f, cy + RegionH + 58f, 17f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f), 680f);
            return frame.Bottom + 14f;
        }

        /// One week of the river: stacked origin fills under one ink edge; a
        /// ghost week keeps its outline and loses its wash.
        static void RiverBar(BinderScreen b, float x, float y, float w, float h, RiverRow r)
        {
            var box = DrawnUI.Rect(b.Content, "river", x, y, w, h);
            if (r.Known)
            {
                double total = 0.0;
                for (int i = 0; i < r.Segs.Length; i++) total += Gd.Maxf(r.Segs[i], 0.0);
                Color[] cols = { DrawnUI.Blue, DrawnUI.Yellow, DrawnUI.Sage };
                float yy = h;
                for (int i = 0; i < r.Segs.Length && i < 3; i++)
                {
                    float sh = h * (float)(Gd.Maxf(r.Segs[i], 0.0) / Gd.Maxf(total, 0.001));
                    yy -= sh;
                    DrawnUI.Fill(box, "seg", DrawnUI.WithAlpha(cols[i], 0.6f), 0f, yy, w, sh)
                        .raycastTarget = false;
                }
            }
            DrawnUI.AddInkEdge(box, new Vector2(w, h), new DrawnUI.PaperStyle
            {
                ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                StepsPerEdge = 7, Jitter = 1f, Thickness = r.Known ? 2.4f : 2f,
                Seed = 29 + Mathf.Abs((int)x % 17),
            });
            if (!r.Known)
            {
                TextMeshProUGUI q = b.L("?", x, y + h * 0.5f - 14f, 22f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.4f), w);
                q.alignment = TextAlignmentOptions.Top;
            }
        }

        /// The last 8 weeks' origin rows, oldest first. The C# snapshot gains
        /// its adds keys with the coordinator package — until then every older
        /// week is a ghost and the freshest week speaks from the funnel.
        static List<RiverRow> RiverRows(GameState s)
        {
            var outRows = new List<RiverRow>();
            int n = s.MetricHistory.Count;
            int start = Math.Max(n - RiverWeeks, 0);
            for (int i = start; i < n; i++)
            {
                MetricSnapshot m = s.MetricHistory[i];
                if (m.Adds.HasValue)
                    outRows.Add(new RiverRow
                    {
                        Wk = m.Wk, Known = true, Total = m.Adds.Value,
                        Segs = new[] { m.AddsChan ?? 0.0, m.AddsWom ?? 0.0, m.AddsOrg ?? 0.0 },
                    });
                else
                    outRows.Add(new RiverRow { Wk = m.Wk, Known = false });
            }
            Dictionary<string, double> f = SimFunnel.Funnel(s);
            if (f.Count > 0 && outRows.Count > 0)
            {
                RiverRow last = outRows[outRows.Count - 1];
                if (!last.Known && last.Wk == Gd.ToInt(SimFunnel.Num(f, "wk")))
                {
                    double chan = SimFunnel.Num(f, "signed_ads") + SimFunnel.Num(f, "signed_content")
                        + SimFunnel.Num(f, "signed_referrals") + SimFunnel.Num(f, "signed_outbound");
                    last.Known = true;
                    last.Total = SimFunnel.Num(f, "adds");
                    last.Segs = new[] { chan, SimFunnel.Num(f, "wom"), SimFunnel.Num(f, "organic") };
                }
            }
            return outRows;
        }

        sealed class Source
        {
            public string Name = "";
            public double V;
            public Color Col;
        }

        /// WHERE THEY COME FROM — this week's ranked sources, top 4 + "+N more".
        static void SourcesCard(BinderScreen b, GameState s, float y)
        {
            DeskKit.CardBox frame = DeskKit.CardFrame(b, 10f, y, 640f, 236f,
                "where they come from — this week");
            float cx = frame.ContentX;
            float cy = frame.ContentY;
            Dictionary<string, double> f = SimFunnel.Funnel(s);
            if (f.Count == 0)
            {
                DeskKit.Empty(b, cx, cy, "no week on the books yet.", "");
                return;
            }
            List<Source> src = Sources(f);
            double hi = 1.0;
            for (int i = 0; i < src.Count && i < 4; i++) hi = Gd.Maxf(hi, src[i].V);
            float by = cy;
            for (int i = 0; i < src.Count && i < 4; i++)
            {
                Source r = src[i];
                b.L(r.Name.ToUpper(), cx, by, 18f, DrawnUI.Ink, 170f);
                float w = 24f + 300f * (float)(r.V / hi);
                DeskKit.Meter(b, cx + 180f, by, w, 1f, r.Col,
                    Gd.RoundToInt(r.V).ToString(CultureInfo.InvariantCulture));
                by += 36f;
            }
            if (src.Count > 4)
                DeskKit.Word(b, "+" + (src.Count - 4) + " more ->", cx, by - 4f,
                    () => b.Desk["mode"] = "sources", DeskKit.Law,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 200f);
        }

        /// The named streams, ranked. Words stay plain; the funnel's numbers carry.
        static List<Source> Sources(Dictionary<string, double> f)
        {
            var src = new List<Source>
            {
                new Source { Name = "word of mouth", V = SimFunnel.Num(f, "wom"), Col = DrawnUI.Yellow },
                new Source { Name = "the ads", V = SimFunnel.Num(f, "signed_ads"), Col = DrawnUI.Blue },
                new Source { Name = "the library", V = SimFunnel.Num(f, "signed_content"), Col = DrawnUI.Blue },
                new Source { Name = "referrals", V = SimFunnel.Num(f, "signed_referrals"), Col = DrawnUI.Yellow },
                new Source { Name = "cold outreach", V = SimFunnel.Num(f, "signed_outbound"), Col = DrawnUI.Blue },
                new Source { Name = "walked in", V = SimFunnel.Num(f, "organic"), Col = DrawnUI.Sage },
            };
            src.Sort((a, c) => c.V.CompareTo(a.V));
            return src;
        }

        /// THE TASTE TEST — tried -> stayed, and the note ads can't argue with.
        static void TasteCard(BinderScreen b, GameState s, float y)
        {
            DeskKit.CardBox frame = DeskKit.CardFrame(b, 666f, y, 464f, 236f, "the taste test");
            Dictionary<string, double> f = SimFunnel.Funnel(s);
            if (f.Count == 0)
            {
                DeskKit.Empty(b, frame.ContentX, frame.ContentY, "no week on the books yet.", "");
                return;
            }
            double leads = SimFunnel.Num(f, "leads_total");
            double chan = SimFunnel.Num(f, "signed_ads") + SimFunnel.Num(f, "signed_content")
                + SimFunnel.Num(f, "signed_referrals") + SimFunnel.Num(f, "signed_outbound");
            double stayed = leads >= 1.0 ? chan / leads * 100.0 : 0.0;
            DeskKit.MoneyRow(b, frame, "tried it this week", GameUi.Money(Gd.RoundToInt(leads)));
            DeskKit.MoneyRow(b, frame, "stayed to pay",
                leads >= 1.0 ? Gd.RoundToInt(stayed) + "%" : "—");
            DeskKit.MoneyRow(b, frame, "a point of staying ≈",
                leads >= 1.0 ? string.Format(CultureInfo.InvariantCulture, "+{0:0.0}/wk", leads * 0.01) : "—",
                Pos);
            b.L("care and product quality move this number — ads can't",
                frame.ContentX, frame.Bottom - 40f, 17f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 420f);
        }

        static void ConsumerFoot(BinderScreen b, GameState s)
        {
            Dictionary<string, double> f = SimFunnel.Funnel(s);
            string computed = "";
            if (f.Count > 0)
                computed = "the week: bought "
                    + Gd.RoundToInt(SimFunnel.Num(f, "signed_ads") + SimFunnel.Num(f, "signed_content")
                        + SimFunnel.Num(f, "signed_referrals") + SimFunnel.Num(f, "signed_outbound"))
                    + " · friends " + Gd.RoundToInt(SimFunnel.Num(f, "wom"))
                    + " · walked in " + Gd.RoundToInt(SimFunnel.Num(f, "organic"))
                    + " = " + Gd.RoundToInt(SimFunnel.Num(f, "adds")) + " joined";
            DeskKit.Footer(b, computed,
                "a consumer pipeline is sources × conversion — nobody has a name until they pay",
                "", 806f, 840f);
        }

        /// The fold opened: every source, one sheet.
        static void ConsumerSourcesAll(BinderScreen b, GameState s)
        {
            DeskKit.Back(b, "back to the river", () => b.Desk["mode"] = "");
            Dictionary<string, double> f = SimFunnel.Funnel(s);
            List<Source> src = Sources(f);
            var rows = new List<DeskKit.BarRow>();
            for (int i = 0; i < src.Count; i++)
                rows.Add(new DeskKit.BarRow
                {
                    Label = src[i].Name,
                    Value = (float)src[i].V,
                    Col = src[i].Col,
                    Text = Gd.RoundToInt(src[i].V) + " this week",
                });
            b.L("every source, counted", DeskKit.XId, 60f, 24f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 600f);
            DeskKit.Bars(b, DeskKit.XId, 100f, rows, 52f);
            ConsumerFoot(b, s);
        }

        // ═══ SMB ═══════════════════════════════════════════════════════════

        static int SmbInMotion(GameState s, Dictionary<string, double> f)
        {
            return s.Leads.Count + Gd.RoundToInt(SimFunnel.Num(f, "leads_total"));
        }

        static void Smb(BinderScreen b, GameState s)
        {
            Dictionary<string, double> f = SimFunnel.Funnel(s);
            string big = SmbInMotion(s, f) + " in motion";
            b.L(big, DeskKit.XId, 6f, DeskKit.HeroBig, DrawnUI.Ink, 620f);
            float bx = DeskKit.XId + big.Length * 26f + 24f;
            b.L("· ≈" + Gd.RoundToInt(SimFunnel.Num(f, "adds")) + " will land", bx, 26f,
                24f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 260f);
            TextMeshProUGUI cr = b.L(f.Count > 0
                ? "close rate " + Gd.RoundToInt(SimFunnel.Num(f, "close_rate") * 100.0) + "%"
                : "close rate ?", 800f, 14f, 27f, DrawnUI.Ink, 330f);
            cr.alignment = TextAlignmentOptions.TopRight;
            b.L("SMB — dozens of small shops, a handful worth chasing by name",
                DeskKit.XId, 74f, DeskKit.Law, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 1100f);
            float y = DeskKit.PenRule(b, 112f) + 8f;
            HotList(b, s, f, y, HotShow);
            DeskKit.Footer(b, "",
                "SMB is a hybrid: name the five that deserve a dinner, count the forty that don't · rank 1 is this week's journal move",
                "", 806f, 840f);
        }

        /// CLOSEST TO MONEY — ranked by revenue-if-landed × closeness.
        static float HotList(BinderScreen b, GameState s, Dictionary<string, double> f,
                             float y, int show)
        {
            List<int> ranked = Ranked(s);
            DeskKit.CardBox frame = DeskKit.CardFrame(b, 10f, y, 1120f,
                DeskKit.CardHead + Mathf.Max(Math.Min(ranked.Count, show), 1) * 58f + 64f,
                "closest to money");
            float cy = frame.ContentY;
            double unit = SimPipeline.UnitRevWk(s);
            if (ranked.Count == 0)
                cy = DeskKit.Empty(b, frame.ContentX, cy,
                    "no shop is worth a dinner yet — the crowd moves on its own.",
                    "a named account arrives when one grows big enough to chase");
            for (int n = 0; n < ranked.Count && n < show; n++)
            {
                Lead lead = s.Leads[ranked[n]];
                int heat = lead.Heat;
                int dies = SimPipeline.WeeksToCold(heat, SimPipeline.DecayFor(s));
                string facts = lead.Seats + " seats · " + (lead.Stage ?? "meeting");
                if (n == 0) facts += " · the move";
                string value = "≈$" + GameUi.Money(Gd.RoundToInt(lead.Seats * unit)) + "/wk";
                TextMeshProUGUI rn = b.L((n + 1).ToString(CultureInfo.InvariantCulture),
                    frame.ContentX, cy + 4f, 22f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.4f), 30f);
                rn.alignment = TextAlignmentOptions.TopRight;
                b.L(lead.Name ?? "a prospect", 60f, cy, DeskKit.Row, DrawnUI.Ink, 330f);
                b.L(facts, 400f, cy + 4f, DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.65f), 330f);
                string hw = SimPipeline.HeatWord(heat);
                b.L(hw, 740f, cy + 4f, DeskKit.Detail, DeskKit.HeatCol(hw), 90f);
                if (dies <= 2) DeskKit.ClockChip(b, 830f, cy + 4f, dies + " wk");
                TextMeshProUGUI vl = b.L(value, 920f, cy, DeskKit.Row, DrawnUI.Ink, 190f);
                vl.alignment = TextAlignmentOptions.TopRight;
                DeskKit.PenRule(b, cy + 44f, 40f, 1060f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.12f), 13 + n);
                cy += 58f;
            }
            // the named leads past the face-up cap count here too, so
            // face-up + the other N = the hero's number (never hidden math)
            int crowd = Gd.RoundToInt(SimFunnel.Num(f, "leads_total"))
                + Math.Max(ranked.Count - Math.Min(ranked.Count, show), 0);
            int lands = Gd.RoundToInt(SimFunnel.Num(f, "adds"));
            b.L("the other " + crowd + " — small shops moving on their own · ≈" + lands
                + " land weekly", 60f, cy + 2f, DeskKit.Detail,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f), 800f);
            if (ranked.Count > show)
                DeskKit.Word(b, "the full list ->", 920f, cy - 4f,
                    () => b.Desk["mode"] = "smb_all", DeskKit.Law,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 190f);
            return frame.Bottom + 10f;
        }

        /// revenue-if-landed × closeness: seats × $/seat × (stage reached × heat).
        static List<int> Ranked(GameState s)
        {
            var stages = new Dictionary<string, double>
            {
                { "meeting", 1.0 }, { "pilot", 2.0 }, { "procurement", 3.0 }, { "contract", 4.0 },
            };
            double unit = SimPipeline.UnitRevWk(s);
            var idx = new List<int>();
            for (int i = 0; i < s.Leads.Count; i++) idx.Add(i);
            idx.Sort((a, c) =>
            {
                Lead la = s.Leads[a];
                Lead lc = s.Leads[c];
                double sa = la.Seats * unit
                    * (stages.ContainsKey(la.Stage ?? "meeting") ? stages[la.Stage ?? "meeting"] : 1.0)
                    * (la.Heat + 20.0);
                double sc = lc.Seats * unit
                    * (stages.ContainsKey(lc.Stage ?? "meeting") ? stages[lc.Stage ?? "meeting"] : 1.0)
                    * (lc.Heat + 20.0);
                if (Math.Abs(sa - sc) > 0.0001) return sc.CompareTo(sa);
                return a.CompareTo(c);
            });
            return idx;
        }

        /// The fold opened: every named account, ranked, one sheet.
        static void SmbAll(BinderScreen b, GameState s)
        {
            DeskKit.Back(b, "back to the hot list", () => b.Desk["mode"] = "");
            Dictionary<string, double> f = SimFunnel.Funnel(s);
            HotList(b, s, f, 64f, s.Leads.Count);
            DeskKit.Footer(b, "",
                "every named account, ranked by revenue-if-landed × closeness",
                "", 806f, 840f);
        }

        // ═══ ENTERPRISE ════════════════════════════════════════════════════

        static void Enterprise(BinderScreen b, GameState s)
        {
            EntHero(b, s);
            float y = DeskKit.PenRule(b, 112f) + 8f;
            if (s.Leads.Count >= SlimAt) EntSlim(b, s, y);
            else EntBoard(b, s, y);
            EntFoot(b, s);
        }

        static void EntHero(BinderScreen b, GameState s)
        {
            string big = s.Leads.Count + " deals";
            b.L(big, DeskKit.XId, 6f, DeskKit.HeroBig, DrawnUI.Ink, 460f);
            float bx = DeskKit.XId + big.Length * 26f + 24f;
            b.L("· ≈" + SimPipeline.SeatsInMotion(s) + " seats in motion", bx, 26f,
                24f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 320f);
            PipeStats st = s.PipeStats ?? new PipeStats();
            int signedN = st.Signed;
            int decided = signedN + st.Lost;
            string win = decided <= 0 ? "?"
                : Gd.RoundToInt(100.0 * signedN / decided) + "%";
            string cycle = signedN <= 0 ? "?"
                : Gd.RoundToInt((double)st.CycleSum / signedN).ToString(CultureInfo.InvariantCulture);
            TextMeshProUGUI wl = b.L("win rate " + win + " · cycle " + cycle + " wks",
                770f, 10f, 27f, DrawnUI.Ink, 360f);
            wl.alignment = TextAlignmentOptions.TopRight;
            int seats = 0;
            for (int i = 0; i < s.Logos.Count; i++) seats += s.Logos[i].Seats;
            TextMeshProUGUI ll = b.L(s.Logos.Count + " logos · " + seats + " seats live",
                770f, 48f, 17f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 360f);
            ll.alignment = TextAlignmentOptions.TopRight;
            b.L("enterprise — every buyer has a name and a dinner budget",
                DeskKit.XId, 74f, DeskKit.Law, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 1100f);
        }

        static string[] EntStages(GameState s)
        {
            return s.EraIndex() >= 2
                ? new[] { "meeting", "pilot", "procurement", "contract" }
                : new[] { "meeting", "pilot", "contract" };
        }

        /// THE STAGE BOARD — columns narrowing like the funnel.
        static void EntBoard(BinderScreen b, GameState s, float y)
        {
            string[] stages = EntStages(s);
            double[] ratios = stages.Length == 4
                ? new[] { 1.12, 1.0, 0.9, 0.78 }
                : new[] { 1.12, 1.0, 0.78 };
            float gaps = (stages.Length - 1) * 12f;
            double rsum = 0.0;
            for (int i = 0; i < ratios.Length; i++) rsum += ratios[i];
            List<int> order = SimPipeline.LeadsByHeat(s);
            int decay = SimPipeline.DecayFor(s);
            float x = 10f;
            int live = 0;
            const float ColH = 520f;
            for (int si = 0; si < stages.Length; si++)
            {
                string stage = stages[si];
                float w = (1120f - gaps) * (float)(ratios[si] / rsum);
                DeskKit.WallCol col = DeskKit.WallColumn(b, x, y, w, ColH, stage,
                    stage == "contract" ? "signed deals join the logos" : "");
                // the header is the door to the focused list
                string stageId = stage;
                Button head = DeskKit.Word(b, "", x, y,
                    () => b.Desk["mode"] = "col:" + stageId, DeskKit.Law, DrawnUI.Ink, w);
                head.GetComponent<RectTransform>().sizeDelta = new Vector2(w, 54f);
                var here = new List<int>();
                for (int i = 0; i < order.Count; i++)
                    if ((s.Leads[order[i]].Stage ?? "meeting") == stage) here.Add(order[i]);
                live += here.Count;
                for (int n = 0; n < here.Count && n < BoardCards; n++)
                {
                    Lead lead = s.Leads[here[n]];
                    int heat = lead.Heat;
                    int dies = SimPipeline.WeeksToCold(heat, decay);
                    var facts = new List<string>
                    {
                        lead.Seats + " seats · " + SimPipeline.HeatWord(heat) + " · wk " + lead.AgeWeeks,
                    };
                    if (dies <= 2)
                        facts.Add("dies in " + dies + " wk" + (dies == 1 ? "" : "s"));
                    DeskKit.WallCard(b, col, new DeskKit.WallCardCfg
                    {
                        Title = lead.Name ?? "a prospect",
                        Facts = facts,
                        Ready = dies <= 2,
                    });
                }
                if (here.Count > BoardCards)
                    b.L("+" + (here.Count - BoardCards), x + 10f, col.Cursor + 2f, 21f,
                        DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), w - 20f);
                x += w + 12f;
            }
            if (live == 0)
                b.L(string.Format(CultureInfo.InvariantCulture,
                    "no deals on the board yet — marketing books the meetings, and {0:0} seats of interest are already waiting in the pool",
                    s.PipeUnits), DeskKit.XId, y + 120f, DeskKit.Status,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 1100f);
        }

        /// Past ~8 live deals the cards compress to slim rows, hottest first.
        static void EntSlim(BinderScreen b, GameState s, float y)
        {
            b.L("the board, compressed — " + s.Leads.Count + " live deals, hottest first",
                DeskKit.XId, y, DeskKit.Law, DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f), 900f);
            y += 30f;
            int decay = SimPipeline.DecayFor(s);
            double unit = SimPipeline.UnitRevWk(s);
            List<int> order = SimPipeline.LeadsByHeat(s);
            int shown = 0;
            for (int i = 0; i < order.Count; i++)
            {
                if (shown >= 10) break;
                Lead lead = s.Leads[order[i]];
                int heat = lead.Heat;
                int dies = SimPipeline.WeeksToCold(heat, decay);
                bool dying = dies <= 2;
                y = DeskKit.HeroRow(b, y, new DeskKit.HeroRowCfg
                {
                    Name = lead.Name ?? "a prospect",
                    Facts = (lead.Stage ?? "meeting") + " · " + lead.Seats + " seats · "
                        + SimPipeline.HeatWord(heat) + " · wk " + lead.AgeWeeks
                        + (dying ? " · dies in " + dies + " wk" : ""),
                    Value = "≈$" + GameUi.Money(Gd.RoundToInt(lead.Seats * unit)) + "/wk",
                    Col = dying ? DrawnUI.Coral : DrawnUI.Ink,
                    Sev = dying ? 3 : 0,
                });
                shown++;
            }
            DeskKit.More(b, DeskKit.XId, y, s.Leads.Count - shown, "sit colder below these");
        }

        /// A column's focused list — the header press opened it.
        static void EntColumnFocus(BinderScreen b, GameState s, string stage)
        {
            DeskKit.Back(b, "back to the board", () => b.Desk["mode"] = "");
            b.L(stage.ToUpper(), DeskKit.XId, 58f, DeskKit.TitleSize, DrawnUI.Ink, 500f);
            float y = 120f;
            int decay = SimPipeline.DecayFor(s);
            double unit = SimPipeline.UnitRevWk(s);
            bool any = false;
            List<int> order = SimPipeline.LeadsByHeat(s);
            for (int i = 0; i < order.Count; i++)
            {
                Lead lead = s.Leads[order[i]];
                if ((lead.Stage ?? "meeting") != stage) continue;
                any = true;
                int heat = lead.Heat;
                int dies = SimPipeline.WeeksToCold(heat, decay);
                bool dying = dies <= 2;
                y = DeskKit.HeroRow(b, y, new DeskKit.HeroRowCfg
                {
                    Name = lead.Name ?? "a prospect",
                    Facts = lead.Seats + " seats · " + SimPipeline.HeatWord(heat)
                        + " · wk " + lead.AgeWeeks
                        + (dying ? " · dies in " + dies + " wk" : "")
                        + (string.IsNullOrEmpty(lead.Flavor) ? "" : " · " + lead.Flavor),
                    Value = "≈$" + GameUi.Money(Gd.RoundToInt(lead.Seats * unit)) + "/wk",
                    Col = dying ? DrawnUI.Coral : DrawnUI.Ink,
                    Sev = dying ? 3 : 0,
                });
            }
            if (!any)
                DeskKit.Empty(b, DeskKit.XId, y, "nothing sits at this gate this week.", "");
            EntFoot(b, s);
        }

        static void EntFoot(BinderScreen b, GameState s)
        {
            PipeStats st = s.PipeStats ?? new PipeStats();
            int signedN = st.Signed;
            int lost = st.Lost;
            int seats = st.SeatsSigned;
            int decided = signedN + lost;
            string win = decided <= 0 ? "?"
                : signedN + "/" + decided + " (" + Gd.RoundToInt(100.0 * signedN / decided) + "%)";
            string cycle = signedN <= 0 ? "?"
                : Gd.RoundToInt((double)st.CycleSum / signedN).ToString(CultureInfo.InvariantCulture);
            string cost = seats <= 0 ? "?" : "$" + Gd.RoundToInt(st.Spend / seats);
            string coach;
            SimPipeline.COACH.TryGetValue(s.Era, out coach);
            DeskKit.Footer(b, string.Format(CultureInfo.InvariantCulture,
                "win rate {0} · avg cycle {1} wks · cost per signed seat ≈ {2} · a seat pays ≈ ${3:0}/wk",
                win, cycle, cost, SimPipeline.UnitRevWk(s)),
                "deals move on written moves — the journal pushes them (a push moves heat, never a stage) · "
                + (coach ?? ""), "", 806f, 840f);
        }
    }
}
