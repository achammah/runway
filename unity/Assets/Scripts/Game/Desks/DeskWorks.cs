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
    /// DESK — COSTS · "the works" (twin of desk_works.gd). W2: L-DIVWORKS.
    /// THE QUESTION: "can we serve what they want, and what does one cost?"
    /// Four numbered zones in the business's OWN units; contents climb three
    /// rungs — boutique -> house (demand mix + ticket book) -> empire (THE
    /// LINEUP, hero rows, face B; press a row -> its rung-2 works). States:
    /// mode "arrange" -> DeskArrange · page "capacity" -> the assets-and-relief
    /// DETAIL · row &lt;site&gt; · ticket &lt;i&gt; · slice.
    /// </summary>
    public static class DeskWorks
    {
        public const string Question = "can we serve what they want, and what does one cost?";

        public static string[] HeroSummary(GameState s)
        {
            if (s.Offers.Count == 0)
                return new[] { "the works",
                    "the desk reads your offers for its ticket — nothing on the shelf yet" };
            Dictionary<string, object> w = SimWorks.WeekView(s);
            Dictionary<string, string> vw = SimWorks.Vocab(s);
            string unit = vw["unit_word"];
            if (SimDivisions.Rung(s) >= 3)
            {
                int n = Gd.Maxi(SimDivisions.SiteDivisions(s), SimDivisions.ProductsCount(s));
                return new[] { string.Format("{0} roofs · {1} {2}s a week", n,
                        Gd.RoundToInt(SimWorks.Num(w, "served_units")), unit),
                    "every line keeps its own books — the works is a book of books now" };
            }
            if (s.BizWhat == "Software")
                return new[] { string.Format("{0} {1}s under a ceiling of {2}",
                        Gd.RoundToInt(SimWorks.Num(w, "served_units")), unit,
                        Gd.RoundToInt(SimWorks.Num(w, "ceiling"))),
                    "software scales, support doesn't — the care team is the ceiling" };
            return new[] { string.Format("{0} {1}s wanted · capacity for {2}",
                    Gd.RoundToInt(SimWorks.Num(w, "demand_units")), unit,
                    Gd.RoundToInt(SimWorks.Num(w, "capacity_units"))),
                string.Format("a {0} you cannot take is revenue that walks out the door", unit) };
        }

        static string DS(BinderScreen b, string key)
        {
            object v;
            return b.Desk.TryGetValue(key, out v) && v != null ? v.ToString() : "";
        }

        static int DI(BinderScreen b, string key)
        {
            object v;
            if (b.Desk.TryGetValue(key, out v) && v != null)
            {
                try { return Convert.ToInt32(v, CultureInfo.InvariantCulture); }
                catch { return 0; }
            }
            return 0;
        }

        // ═══════════════════════════════ DRAW ═════════════════════════════

        public static void Draw(BinderScreen b)
        {
            GameState s = b.State;
            if (DS(b, "mode") == "arrange") { DeskArrange.Draw(b); return; }
            foreach (string k in new[] { "teardown", "open_roof", "edit", "staged2", "chip_k" })
                b.Desk.Remove(k);
            DeskKit.Word(b, "arrange ->", DeskKit.XId + 980f, 6f,
                () => { b.Desk["mode"] = "arrange"; }, DeskKit.Status, DrawnUI.Blue, 160f);
            if (s.Offers.Count == 0) { DrawEmpty(b, s); return; }
            if (DS(b, "page") == "capacity") { CapacitySheet(b, s, DS(b, "row")); return; }
            string opened = DS(b, "row");
            if (SimDivisions.Rung(s) >= 3 && opened.Length == 0) { Empire(b, s); return; }
            HouseOrBoutique(b, s, opened);
        }

        static void DrawEmpty(BinderScreen b, GameState s)
        {
            float y = DeskKit.HeroBand(b, "the works",
                "every business has works — the cost of delivering what you sell");
            y = DeskKit.Empty(b, DeskKit.XId, y,
                "nothing is on the shelf yet, so there is nothing to serve.",
                "define an offer on the OFFERS desk — its cost lines become the unit ticket here",
                true);
            if (s.BizWhat == "Service")
                b.L(string.Format(
                    "meanwhile the hands are real: capacity {0} slots/wk (the founder + the crew)",
                    Gd.RoundToInt(SimWorks.ServiceCapacity(s))), DeskKit.XId, y + 8f,
                    DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 1080f);
            DeskKit.Footer(b, "the works reads your team for hands and your offers for the ticket",
                "one desk, every running cost of delivering", "", 806f, 840f);
            DeskKit.HeroQuestion(b, Question);
        }

        // ─────────────────────── rungs 1-2 (one roof) ──────────────────────

        static void HouseOrBoutique(BinderScreen b, GameState s, string openedSite)
        {
            Dictionary<string, object> w = SimWorks.WeekView(s);
            Dictionary<string, string> vw = SimWorks.Vocab(s);
            string unit = vw["unit_word"];
            bool scoped = openedSite.Length > 0;
            if (scoped) DeskKit.Back(b, "back to the lineup", () => b.Desk.Remove("row"));
            double demand = SimWorks.Num(w, "demand_units");
            double cap = SimWorks.Num(w, "capacity_units");
            double served = SimWorks.Num(w, "served_units");
            double walk = SimWorks.Num(w, "walk_units");
            string big, line;
            double mrow = BlendedMargin(s);
            if (scoped)
            {
                Dictionary<string, object> rd = null;
                foreach (Dictionary<string, object> r in SimDivisions.WorksBook(s, "site"))
                    if ((string)r["id"] == openedSite) rd = r;
                rd = rd ?? new Dictionary<string, object>();
                demand = SimWorks.Num(rd, "wanted");
                cap = SimWorks.Num(rd, "slots");
                served = SimWorks.Num(rd, "served");
                walk = Gd.Maxf(demand - served, 0.0);
                big = string.Format("{0} · {1} {2}s a week", rd.ContainsKey("name") ? rd["name"] : "?",
                    Gd.RoundToInt(served), unit);
                line = "this roof's own book — rent, hands and learning under one name";
            }
            else if (s.BizWhat == "Software")
            {
                // short enough to keep the corner block's lane at any scale
                big = string.Format("{0} of {1} {2}s served", Gd.RoundToInt(served),
                    Gd.RoundToInt(SimWorks.Num(w, "ceiling")), unit);
                line = "software doesn't turn people away — it slowly serves everyone worse";
            }
            else
            {
                big = string.Format("{0} {1}s wanted · {2} for {3}", Gd.RoundToInt(demand),
                    unit, vw["capacity_word"], Gd.RoundToInt(cap));
                line = "each one leaves at its price and costs real hands, rooms and parts";
            }
            // the hero big never runs under the corner block (measured, not hoped)
            float y = DeskKit.HeroBand(b, Fit(big, 660f, DeskKit.HeroBig), line,
                DrawnUI.Ink, scoped ? 44f : 6f);
            // the corner block ends clear of the arrange -> word at XId+980
            TextMeshProUGUI mv = b.L("margin each  " + Money(mrow), 700f, scoped ? 48f : 10f,
                DeskKit.Status, DrawnUI.Ink, 240f);
            mv.alignment = TextAlignmentOptions.TopRight;
            if (walk >= 1.0)
            {
                TextMeshProUGUI wv = b.L(Gd.RoundToInt(walk) + " turned away", 700f,
                    scoped ? 82f : 44f, DeskKit.Detail, DrawnUI.Coral, 240f);
                wv.alignment = TextAlignmentOptions.TopRight;
            }
            bool house = SimDivisions.Rung(s) >= 2 && !scoped;
            y = house ? ZoneDemandMix(b, s, y, unit) : ZoneCapbars(b, s, y, w, unit, openedSite);
            y = ZoneTicket(b, s, y, house);
            y = CapacityBand(b, s, y, openedSite);
            double unbilled = SimWorks.Num(w, "unbilled");
            // the foot rides BELOW the last zone when the stack runs deep —
            // the blue line never prints across zone 4 (the scrolling QA law)
            float fy = Math.Max(806f, y + 4f);
            DeskKit.Footer(b,
                "the works reads your team for hands, your offers for the ticket — one desk, every cost of delivering",
                "", unbilled >= 1.0
                    ? string.Format("${0}/wk walks away — relief valves or hires close it",
                        M(unbilled)) : "", fy, fy + 34f);
            DeskKit.HeroQuestion(b, Question);
        }

        static float ZoneCapbars(BinderScreen b, GameState s, float y,
            Dictionary<string, object> w, string unit, string site)
        {
            Dictionary<string, string> vw = SimWorks.Vocab(s);
            double demand = SimWorks.Num(w, "demand_units");
            double cap = SimWorks.Num(w, "capacity_units");
            double relief = SimWorks.Num(w, "relief_used");
            if (site.Length > 0)
            {
                foreach (Dictionary<string, object> r in SimDivisions.WorksBook(s, "site"))
                    if ((string)r["id"] == site)
                    {
                        demand = SimWorks.Num(r, "wanted");
                        cap = SimWorks.Num(r, "slots");
                        relief = 0.0;
                    }
            }
            double hi = Gd.Maxf(Gd.Maxf(demand, cap + relief), 1.0);
            string lesson = string.Format("a {0} you cannot take is revenue that walks out the door", unit);
            double over = 0.0;
            if (s.BizWhat == "Software")
            {
                lesson = "past the ceiling nothing walks away — replies slip and churn bites";
                over = SimWorks.Num(w, "over");
            }
            else if (s.BizWhat == "Marketplace")
            {
                lesson = "a buyer who finds an empty shelf rarely knocks twice";
            }
            var rows = new List<DeskKit.CapRow>
            {
                new DeskKit.CapRow { Label = "they want", Pct = (float)(demand / hi * 100.0),
                    Col = DeskKit.Kraft2, Note = Gd.RoundToInt(demand) + "/wk" },
                new DeskKit.CapRow { Label = Left(vw["capacity_word"], 18),
                    Pct = (float)(cap / hi * 100.0),
                    Col = (over > 0.0 || demand > cap + relief + 0.5) ? DrawnUI.Coral : DrawnUI.Sage,
                    Note = Gd.RoundToInt(cap) + "/wk" },
            };
            if (relief >= 1.0)
                rows.Add(new DeskKit.CapRow { Label = Left(vw["relief_word"], 18),
                    Pct = (float)(relief / hi * 100.0), Col = DrawnUI.Blue,
                    Note = "+" + Gd.RoundToInt(relief) + "/wk" });
            float h = 88f + rows.Count * 40f;
            DeskKit.CardBox z = DeskKit.Zone(b, DeskKit.XId, y, 1120f, h, 1, "CAN WE SERVE?", lesson);
            DeskKit.CapBars(b, z.ContentX, z.ContentY, 1000f, rows);
            return y + h + 10f;
        }

        static float ZoneDemandMix(BinderScreen b, GameState s, float y, string unit)
        {
            var rows = new List<Dictionary<string, object>>();
            foreach (Dictionary<string, object> r in SimDivisions.WorksBook(s, "offer"))
                if ((string)r["kind"] == "offer") rows.Add(r);
            rows.Sort((a, bb) =>
            {
                double ga = SimWorks.Num(a, "wanted") - SimWorks.Num(a, "served");
                double gb = SimWorks.Num(bb, "wanted") - SimWorks.Num(bb, "served");
                return gb.CompareTo(ga);
            });
            int shown = Gd.Mini(rows.Count, 4);
            int folded = rows.Count - shown;
            float h = 88f + 34f + shown * 40f + 48f + (folded > 0 ? 44f : 0f) + 34f;
            // the unit note rides the lesson — the sheet's own top-right slot
            // would print it across the GAP column header on this wide sheet
            DeskKit.CardBox z = DeskKit.Zone(b, DeskKit.XId, y, 1120f, h, 1,
                "CAN WE SERVE? — THE DEMAND MIX",
                string.Format("offers share one pool; the gap lands on what you deprioritize — figures in {0}s/wk", unit));
            DeskKit.LedgerBox sheet = DeskKit.LedgerSheet(b, z.ContentX, z.ContentY, 1070f,
                new List<DeskKit.LedgerCol>
                {
                    new DeskKit.LedgerCol { Label = "offer", W = 420f },
                    new DeskKit.LedgerCol { Label = "wanted", W = 170f, Align = "right" },
                    new DeskKit.LedgerCol { Label = "served", W = 170f, Align = "right" },
                    new DeskKit.LedgerCol { Label = "gap", W = 170f, Align = "right" },
                }, 3, false, "");
            // THE ACCOUNTING RULES LAW: the totals sum the rounded figures the
            // rows actually show, so the sheet squares with itself on camera
            int wantT = 0, servedT = 0;
            for (int i = 0; i < shown; i++)
            {
                Dictionary<string, object> rd = rows[i];
                int wr = Gd.RoundToInt(SimWorks.Num(rd, "wanted"));
                int sr = Gd.RoundToInt(SimWorks.Num(rd, "served"));
                int gap = wr - sr;
                wantT += wr;
                servedT += sr;
                DeskKit.LedgerRow(b, sheet, new[] { (string)rd["name"],
                        wr.ToString(), sr.ToString(),
                        gap >= 1 ? "−" + gap : "—" },
                    new DeskKit.LedgerRowCfg { Col = gap >= 1 ? DrawnUI.Coral : DrawnUI.Ink });
            }
            for (int i2 = shown; i2 < rows.Count; i2++)
            {
                wantT += Gd.RoundToInt(SimWorks.Num(rows[i2], "wanted"));
                servedT += Gd.RoundToInt(SimWorks.Num(rows[i2], "served"));
            }
            int gapT = wantT - servedT;
            DeskKit.LedgerTotal(b, sheet, "THE WEEK",
                gapT >= 1 ? "−" + gapT : "0",
                gapT >= 1 ? DrawnUI.Coral : DrawnUI.Ink);
            float endY = DeskKit.LedgerEnd(b, sheet);
            if (folded > 0)
                endY = DeskKit.FoldRow(b, z.ContentX, endY - 8f, folded, "offers share the pool too");
            DeskKit.Meter(b, z.ContentX, endY - 6f, 560f,
                (float)Gd.Clampf(servedT / Gd.Maxf(wantT, 0.001), 0.0, 1.0), DrawnUI.Sage,
                string.Format("the shared pool — {0} of {1}", servedT, wantT));
            return y + h + 10f;
        }

        static float ZoneTicket(BinderScreen b, GameState s, float y, bool house)
        {
            if (!house)
            {
                Dictionary<string, object> t = SimWorks.UnitTicket(s, FlagshipI(s));
                var raw = (List<Dictionary<string, object>>)t["lines"];
                var lines = new List<DeskKit.TicketLine>();
                for (int i = 0; i < raw.Count && i < 3; i++)
                    lines.Add(new DeskKit.TicketLine { Label = (string)raw[i]["label"],
                        Value = "$" + M(SimWorks.Num(raw[i], "amount")) });
                if (raw.Count > 3)
                {
                    double rest = 0.0;
                    for (int j = 3; j < raw.Count; j++) rest += SimWorks.Num(raw[j], "amount");
                    lines.Add(new DeskKit.TicketLine { Label = "everything else", Value = "$" + M(rest) });
                }
                lines.Add(new DeskKit.TicketLine { Label = "sells for", Value = "$" + M(SimWorks.Num(t, "sells")) });
                float th = 46f + lines.Count * 32f + 44f + 30f + 14f;
                float h = 84f + th;
                DeskKit.CardBox z = DeskKit.Zone(b, DeskKit.XId, y, 1120f, h, 2, "WHAT ONE COSTS",
                    string.Format("practice makes every {0} cheaper — the learning curve is real money",
                        SimWorks.Vocab(s)["unit_word"]));
                double lc = SimWorks.Num(t, "lc", 1.0);
                double margin = SimWorks.Num(t, "margin");
                DeskKit.Ticket(b, z.ContentX + 560f, z.ContentY - 6f, 470f,
                    "ONE " + ((string)s.Offers[FlagshipI(s)].Name).ToUpper(), lines,
                    "margin, each", Money(margin),
                    lc < 0.995 ? string.Format("learning ×{0:F2} — {1} served so far", lc, Commas(s.ServedTotal))
                        : "the curve starts once volume does",
                    margin >= 0.0 ? DrawnUI.Sage : DrawnUI.Coral);
                b.L("costs, each — the offer's own cost lines,\nlearning applied at the total, never per line",
                    z.ContentX, z.ContentY + 8f, DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 520f);
                return y + h + 10f;
            }
            var rows2 = new List<Dictionary<string, object>>();
            foreach (Dictionary<string, object> r in SimDivisions.WorksBook(s, "offer"))
                if ((string)r["kind"] == "offer") rows2.Add(r);
            int shown2 = Gd.Mini(rows2.Count, 4);
            int folded2 = rows2.Count - shown2;
            float h2 = 88f + 34f + shown2 * 40f + 48f + (folded2 > 0 ? 44f : 0f);
            int opened = DI(b, "ticket");
            // the opened ticket beside the book is often the taller column —
            // the zone holds whichever is tallest, so the ticket never
            // crosses the zone's edge
            Dictionary<string, object> pre = SimWorks.UnitTicket(s,
                Gd.Clampi(opened, 0, s.Offers.Count - 1));
            var preRaw = (List<Dictionary<string, object>>)pre["lines"];
            int preLines = Gd.Mini(preRaw.Count, 3) + (preRaw.Count > 3 ? 1 : 0) + 1;
            bool preFoot = SimWorks.Num(pre, "lc") < 0.995;
            h2 = Mathf.Max(h2, 84f + 46f + preLines * 32f + 44f + 30f + (preFoot ? 14f : 0f) + 12f);
            DeskKit.CardBox z2 = DeskKit.Zone(b, DeskKit.XId, y, 1120f, h2, 2,
                "WHAT ONE COSTS — THE TICKET BOOK",
                "one row per offer; press a row and its ticket opens itemized");
            DeskKit.LedgerBox sheet2 = DeskKit.LedgerSheet(b, z2.ContentX, z2.ContentY, 620f,
                new List<DeskKit.LedgerCol>
                {
                    new DeskKit.LedgerCol { Label = "offer", W = 250f },
                    new DeskKit.LedgerCol { Label = "costs each", W = 120f, Align = "right" },
                    new DeskKit.LedgerCol { Label = "margin", W = 120f, Align = "right" },
                }, 2, false, "");
            double blended = 0.0, volume = 0.0;
            for (int i = 0; i < shown2; i++)
            {
                Dictionary<string, object> rd = rows2[i];
                int idx = int.Parse(((string)rd["id"]).Substring(6), CultureInfo.InvariantCulture);
                int captured = idx;
                DeskKit.LedgerRow(b, sheet2, new[]
                    { (string)rd["name"] + (idx == opened ? "  (open)" : ""),
                        "$" + M(SimWorks.Num(rd, "unit_cost")),
                        Money(SimWorks.Num(rd, "margin_each")) },
                    new DeskKit.LedgerRowCfg
                    {
                        Col = SimWorks.Num(rd, "margin_each") >= 0.0 ? DrawnUI.Sage : DrawnUI.Coral,
                        OnPress = () => { b.Desk["ticket"] = captured; },
                    });
            }
            foreach (Dictionary<string, object> r2 in rows2)
            {
                blended += SimWorks.Num(r2, "margin_each") * SimWorks.Num(r2, "vol");
                volume += SimWorks.Num(r2, "vol");
            }
            DeskKit.LedgerTotal(b, sheet2, "BLENDED", Money(blended / Gd.Maxf(volume, 0.001)),
                blended >= 0.0 ? DrawnUI.Sage : DrawnUI.Coral);
            float endY2 = DeskKit.LedgerEnd(b, sheet2);
            if (folded2 > 0)
                DeskKit.FoldRow(b, z2.ContentX, endY2 - 10f, folded2, "offers in the book");
            int ti = Gd.Clampi(opened, 0, s.Offers.Count - 1);
            Dictionary<string, object> t2 = SimWorks.UnitTicket(s, ti);
            var raw2 = (List<Dictionary<string, object>>)t2["lines"];
            var tl = new List<DeskKit.TicketLine>();
            for (int i3 = 0; i3 < raw2.Count && i3 < 3; i3++)
                tl.Add(new DeskKit.TicketLine { Label = (string)raw2[i3]["label"],
                    Value = "$" + M(SimWorks.Num(raw2[i3], "amount")) });
            // the lines past three fold into one honest row — the ticket still squares
            if (raw2.Count > 3)
            {
                double rest2 = 0.0;
                for (int j2 = 3; j2 < raw2.Count; j2++)
                    rest2 += SimWorks.Num(raw2[j2], "amount");
                tl.Add(new DeskKit.TicketLine { Label = "everything else", Value = "$" + M(rest2) });
            }
            tl.Add(new DeskKit.TicketLine { Label = "sells for", Value = "$" + M(SimWorks.Num(t2, "sells")) });
            double margin2 = SimWorks.Num(t2, "margin");
            double lc2 = SimWorks.Num(t2, "lc");
            DeskKit.Ticket(b, z2.ContentX + 660f, z2.ContentY - 6f, 400f,
                ((string)s.Offers[ti].Name).ToUpper() + " — OPENED", tl, "margin, each",
                Money(margin2),
                lc2 < 0.995 ? string.Format("learning ×{0:0.00} applied at the total", lc2) : "",
                margin2 >= 0.0 ? DrawnUI.Sage : DrawnUI.Coral);
            return y + h2 + 10f;
        }

        static float CapacityBand(BinderScreen b, GameState s, float y, string site)
        {
            Dictionary<string, object> w = SimWorks.WeekView(s);
            string facts = "";
            switch (s.BizWhat)
            {
                case "Service":
                {
                    int heads = 0;
                    foreach (Employee e in s.Employees)
                    {
                        string role = e.Role ?? "";
                        if (!(role.Contains("sales") || role.Contains("marketing"))) heads += 1;
                    }
                    double slots = site.Length > 0 ? SimWorks.CapacityOfSite(s, site)
                        : SimWorks.ServiceCapacity(s);
                    facts = string.Format("HANDS ×{0} — {1} slots/wk",
                        heads + (s.Sites.Count == 0 || site.Length == 0 ? 1 : 0),
                        Gd.RoundToInt(slots));
                    break;
                }
                case "Software":
                    facts = string.Format("the care team holds {0} seats · servers are not the bottleneck",
                        Gd.RoundToInt(SimWorks.SoftwareCeiling(s)));
                    break;
                case "Marketplace":
                    facts = string.Format("SELLERS ×{0} — feed {1} orders/wk", SimWorks.SellerPool(s),
                        Gd.RoundToInt(SimWorks.Num(w, "capacity_units")));
                    break;
                case "Hardware":
                    facts = string.Format("MACHINES ×{0} — {1} units/wk",
                        s.Hardware != null ? s.Hardware.Equipment.Count : 0,
                        Gd.RoundToInt(SimFactory.Capacity(s)));
                    break;
            }
            PenRow(b, y, 3, "WHAT MAKES THE CAPACITY", facts, () => { b.Desk["page"] = "capacity"; });
            PenRow(b, y + 52f, 4, "THE RELIEF VALVES", ReliefLine(s), () => { b.Desk["page"] = "capacity"; });
            return y + 108f;
        }

        static string ReliefLine(GameState s)
        {
            switch (s.BizWhat)
            {
                case "Service":
                {
                    int v = SimWorks.ReliefGet(s, "freelance");
                    return v > 0 ? string.Format("freelancers up to {0}/wk — ${1} each", v,
                            Gd.RoundToInt(SimDivisions.Pb(s, "freelance_rate")))
                        : "no freelancers booked — the valve is closed";
                }
                case "Software":
                {
                    int bv = SimWorks.ReliefGet(s, "burst");
                    return bv > 0 ? "cloud burst +" + bv + " seats provisioned"
                        : "no burst provisioned — the ceiling is the ceiling";
                }
                case "Marketplace":
                {
                    int rv = SimWorks.ReliefGet(s, "recruit_supply");
                    return rv > 0 ? "recruitment push $" + rv + "/wk"
                        : "no recruitment push — supply grows on its own";
                }
            }
            return "the subcontract shop is "
                + (SimWorks.ReliefGet(s, "subcontract") > 0 ? "ON" : "OFF");
        }

        static void PenRow(BinderScreen b, float y, int num, string title, string facts, Action onPress)
        {
            b.L(num + " · " + title, DeskKit.XId + 8f, y, DeskKit.Detail, DrawnUI.Ink, 360f);
            b.L(facts, DeskKit.XId + 400f, y, DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.65f), 560f);
            DeskKit.Word(b, "open ->", DeskKit.XId + 980f, y - 6f, onPress, DeskKit.Detail,
                DrawnUI.Blue, 130f);
            DeskKit.PenRule(b, y + 40f, DeskKit.XId, 1120f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.14f), (int)y % 17);
        }

        // ── THE DETAIL SHEET: zones 3+4 full size ──────────────────────────

        static void CapacitySheet(BinderScreen b, GameState s, string site)
        {
            DeskKit.Back(b, "back to the works", () => b.Desk.Remove("page"));
            Dictionary<string, string> vw = SimWorks.Vocab(s);
            float y = DeskKit.HeroBand(b, "what makes the capacity",
                string.Format("your capacity is {0} — and bought help is dearer per {1}, but dearer beats turned away",
                    vw["capacity_word"], vw["unit_word"]), DrawnUI.Ink, 44f);
            var rows = new List<string[]>();
            switch (s.BizWhat)
            {
                case "Service":
                {
                    string best = "";
                    int bestSkill = -1;
                    int serving = 0;
                    foreach (Employee e in s.Employees)
                    {
                        string role = e.Role ?? "";
                        if (role.Contains("sales") || role.Contains("marketing")) continue;
                        serving += 1;
                        if (e.Skill > bestSkill) { bestSkill = e.Skill; best = e.Name ?? ""; }
                    }
                    rows.Add(new[] { "HANDS" + (best.Length > 0 ? " — " + best + " leads" : " — the founder's own"),
                        "×" + (serving + 1), Gd.RoundToInt(SimWorks.ServiceCapacity(s)) + " slots/wk",
                        "ramping hands give zero this week" });
                    if (s.Sites.Count > 0)
                        rows.Add(new[] { "ROOFS", "×" + SimDivisions.SiteDivisions(s), "—",
                            "each roof holds its own hands" });
                    break;
                }
                case "Software":
                {
                    rows.Add(new[] { "THE SERVERS", "—",
                        Gd.RoundToInt(SimWorks.SoftwareCeiling(s) * 1.5) + " seats", "not the bottleneck" });
                    int careHeads = 0;
                    foreach (Employee e2 in s.Employees)
                        if ((e2.Role ?? "").Contains("support")) careHeads += 1;
                    rows.Add(new[] { "THE CARE TEAM", "×" + careHeads,
                        Gd.RoundToInt(SimWorks.SoftwareCeiling(s)) + " seats",
                        "THE ceiling — hire or fund care to raise it" });
                    break;
                }
                case "Marketplace":
                    rows.Add(new[] { "THE SELLER POOL", "×" + SimWorks.SellerPool(s),
                        Gd.RoundToInt(SimWorks.MarketplaceSupply(s)) + " orders/wk",
                        "grows with the buyers, lags your growth" });
                    break;
                case "Hardware":
                {
                    var eq = s.Hardware != null ? s.Hardware.Equipment : new List<EquipmentItem>();
                    foreach (EquipmentItem m in eq)
                    {
                        rows.Add(new[] { m.Name ?? "?",
                            string.IsNullOrEmpty(m.Site) ? "" : SimDivisions.RoofName(s, m.Site),
                            "+" + (int)m.CapacityAdd + " units/wk", "resale ≈ half" });
                        if (rows.Count >= 4) break;
                    }
                    if (eq.Count == 0)
                        rows.Add(new[] { "THE BENCH", "—",
                            Gd.RoundToInt(SimFactory.Capacity(s)) + " units/wk",
                            "hands only — machines live on WHAT WE MAKE" });
                    break;
                }
            }
            float h3 = 88f + 34f + rows.Count * 40f + 12f;
            DeskKit.CardBox z3 = DeskKit.Zone(b, DeskKit.XId, y, 1120f, h3, 3,
                "WHAT MAKES THE CAPACITY", "assets group like the team does — best named, the crowd counted");
            DeskKit.LedgerBox sheet = DeskKit.LedgerSheet(b, z3.ContentX, z3.ContentY, 1070f,
                new List<DeskKit.LedgerCol>
                {
                    new DeskKit.LedgerCol { Label = "asset group", W = 360f },
                    new DeskKit.LedgerCol { Label = "count", W = 150f, Align = "right" },
                    new DeskKit.LedgerCol { Label = "gives", W = 200f, Align = "right" },
                    new DeskKit.LedgerCol { Label = "note", W = 300f },
                }, 2, false, "");
            foreach (string[] r in rows) DeskKit.LedgerRow(b, sheet, r);
            DeskKit.LedgerEnd(b, sheet);
            y += h3 + 12f;
            y = ZoneRelief(b, s, y);
            DeskKit.Footer(b, "letting hands go is never free — severance is always owed (-> team)",
                "every valve priced against in-house — dearer each, but dearer beats turned away",
                "", 806f, 840f);
            DeskKit.HeroQuestion(b, Question);
        }

        static float ZoneRelief(BinderScreen b, GameState s, float y)
        {
            var cats = new List<string[]>();
            switch (s.BizWhat)
            {
                case "Service":
                    cats.Add(new[] { "freelance", "freelancers, up to", "/wk",
                        string.Format("${0} each vs ${1} in-house",
                            Gd.RoundToInt(SimDivisions.Pb(s, "freelance_rate")),
                            M(SimWorks.BaseUnitCost(s))) });
                    break;
                case "Software":
                    cats.Add(new[] { "burst", "cloud burst, plus", " seats",
                        "queue persists — burst closes at most 60%" });
                    break;
                case "Marketplace":
                    cats.Add(new[] { "recruit_supply", "recruitment push", "$/wk",
                        "≈1 seller per $35, each feeds ≈2.5 orders/wk" });
                    break;
                case "Hardware":
                    cats.Add(new[] { "subcontract", "the subcontract shop", "",
                        string.Format("×{0:F2} unit cost — their margin, none of your learning",
                            SimFactory.SubMult(s.Era)) });
                    break;
            }
            float h = 88f + cats.Count * 52f + 8f;
            DeskKit.CardBox z = DeskKit.Zone(b, DeskKit.XId, y, 1120f, h, 4, "THE RELIEF VALVES",
                "the valve is a standing lever — it answers this week and every week until you close it");
            float ry = z.ContentY;
            foreach (string[] c in cats)
            {
                string cat = c[0];
                int v = SimWorks.ReliefGet(s, cat);
                int[] stepsI = SimWorks.ReliefSteps(cat);
                var steps = new List<double>();
                foreach (int st in stepsI) steps.Add(st);
                b.L(c[1], z.ContentX, ry, DeskKit.Status, DrawnUI.Ink, 340f);
                TextMeshProUGUI vv = b.L(cat == "subcontract" ? (v > 0 ? "ON" : "OFF") : v + c[2],
                    z.ContentX + 350f, ry, DeskKit.Status, DrawnUI.Coral, 170f);
                vv.alignment = TextAlignmentOptions.TopRight;
                b.L(c[3], z.ContentX + 560f, ry + 6f, DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 420f);
                if (cat == "subcontract")
                    DeskKit.AdjustPair(b, z.ContentX + 990f, ry + 4f,
                        () => SimWorks.ReliefSet(s, cat, 0),
                        () => SimWorks.ReliefSet(s, cat, 1), v <= 0, v >= 1);
                else
                    DeskKit.AdjustPair(b, z.ContentX + 990f, ry + 4f,
                        () => SimWorks.ReliefSet(s, cat, (int)DeskKit.Ladder(steps, v, -1)),
                        () => SimWorks.ReliefSet(s, cat, (int)DeskKit.Ladder(steps, v, 1)),
                        DeskKit.AtMin(steps, v), DeskKit.AtMax(steps, v));
                ry += 52f;
            }
            return y + h + 10f;
        }

        // ─────────────────────── rung 3: THE EMPIRE ────────────────────────

        static void Empire(BinderScreen b, GameState s)
        {
            List<string> axes = SimDivisions.SliceAxes(s);
            string slice = DS(b, "slice");
            if (slice.Length == 0 || !axes.Contains(slice)) slice = SimDivisions.DefaultSlice(s);
            List<Dictionary<string, object>> book = SimDivisions.WorksBook(s, slice);
            var divs = new List<Dictionary<string, object>>();
            foreach (Dictionary<string, object> r in book)
                if ((string)r["kind"] != "shared") divs.Add(r);
            Dictionary<string, object> shared = book[book.Count - 1];
            Dictionary<string, string> vw = SimWorks.Vocab(s);
            string unit = vw["unit_word"];
            Dictionary<string, object> w = SimWorks.WeekView(s);
            // THE ACCOUNTING RULES LAW: the hero is the sum of the rows the
            // page shows (every division + relief), so the lineup squares
            int dispTotal = 0;
            foreach (Dictionary<string, object> dv in divs)
                dispTotal += Gd.RoundToInt(SimWorks.Num(dv, "vol"));
            int reliefT = Gd.RoundToInt(SimWorks.Num(w, "relief_used"));
            dispTotal += reliefT;
            float y = DeskKit.HeroBand(b, string.Format("{0} {1} · {2} {3}s a week", divs.Count,
                    AxisWord(slice, divs.Count), dispTotal, unit),
                "every line keeps its own books — press one and its whole works opens");
            if (axes.Count > 1)
            {
                string nextAxis = axes[(axes.IndexOf(slice) + 1) % axes.Count];
                DeskKit.Word(b, string.Format("sliced by {0} — slice by {1} instead ▸",
                        AxisWord(slice, 2), AxisWord(nextAxis, 2)),
                    DeskKit.XId + 640f, y - 40f, () => { b.Desk["slice"] = nextAxis; },
                    DeskKit.Detail, DrawnUI.Blue, 420f);
            }
            else
            {
                b.L("sliced by " + AxisWord(slice, 2), DeskKit.XId + 760f, y - 36f,
                    DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 320f);
            }
            int shown = Gd.Mini(divs.Count, 5);
            for (int i = 0; i < shown; i++)
            {
                Dictionary<string, object> rd = divs[i];
                double margin = SimWorks.Num(rd, "margin_each");
                string facts = string.Format("{0}% used · {1}/wk · makes each for ${2}",
                    Gd.RoundToInt(SimWorks.Num(rd, "util") * 100.0),
                    Gd.RoundToInt(SimWorks.Num(rd, "vol")), M(SimWorks.Num(rd, "unit_cost")));
                string note = (string)rd["note"];
                if (note.Length > 0 && facts.Length + note.Length <= 48) facts += " · " + note;
                string id = (string)rd["id"];
                y = DeskKit.HeroRow(b, y, new DeskKit.HeroRowCfg
                {
                    Name = (string)rd["name"], Facts = facts, Value = Money(margin),
                    Col = margin >= 0.0 ? DrawnUI.Sage : DrawnUI.Coral,
                    Sev = Convert.ToInt32(rd["sev"], CultureInfo.InvariantCulture),
                    OnPress = slice == "site" ? () => { b.Desk["row"] = id; } : (Action)null,
                });
            }
            if (divs.Count > shown)
            {
                int fn = divs.Count - shown;
                y = DeskKit.FoldRow(b, DeskKit.XId, y, fn,
                    fn == 1 ? "healthy line holds steady" : "healthy lines hold steady");
            }
            // relief serves on top of the roofs — the hero counts it, name it
            if (reliefT >= 1)
            {
                b.L("+ relief hands — " + reliefT + " " + unit + (reliefT == 1 ? "" : "s")
                    + "/wk on top of the roofs",
                    DeskKit.XId + 24f, y + 2f, DeskKit.Detail, DrawnUI.Blue, 700f);
                y += 34f;
            }
            if (slice == "site")
            {
                DeskKit.Word(b, string.Format(
                        "+ a new roof — the pack quotes ≈${0} (lease · fit-out · hires) ▸",
                        Commas(SimDivisions.OpenPackCost(s))),
                    DeskKit.XId, y, () =>
                    {
                        b.Desk["mode"] = "arrange";
                        b.Desk["open_roof"] = true;
                    }, DeskKit.Detail, DrawnUI.Blue, 720f);
                y += 46f;
            }
            if (divs.Count >= 2) y = ScaleLesson(b, divs, y, unit);
            b.L("SHARED / HQ — " + shared["note"], DeskKit.XId, y, DeskKit.Detail,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 800f);
            TextMeshProUGUI sv = b.L("−$" + Commas(-Convert.ToInt32(shared["net_wk"],
                    CultureInfo.InvariantCulture)) + "/wk", DeskKit.XId + 880f, y - 4f,
                DeskKit.Status, DrawnUI.Ink, 230f);
            sv.alignment = TextAlignmentOptions.TopRight;
            DeskKit.Footer(b,
                "unit economics differ by roof — rent, local wages and each roof's own learning; that is the whole lesson of scale",
                "press any line and its whole rung-2 works opens for that roof", "", 806f, 840f);
            DeskKit.HeroQuestion(b, Question);
        }

        static float ScaleLesson(BinderScreen b, List<Dictionary<string, object>> divs,
            float y, string unit)
        {
            Dictionary<string, object> best = divs[0];
            Dictionary<string, object> worst = divs[0];
            foreach (Dictionary<string, object> rd in divs)
            {
                if (SimWorks.Num(rd, "vol") <= 0.0) continue;
                if (SimWorks.Num(rd, "unit_cost") < SimWorks.Num(best, "unit_cost", 1e9)) best = rd;
                if (SimWorks.Num(rd, "unit_cost") > SimWorks.Num(worst, "unit_cost")) worst = rd;
            }
            if (ReferenceEquals(best, worst)) return y;
            double diff = SimWorks.Num(worst, "unit_cost") - SimWorks.Num(best, "unit_cost");
            DeskKit.Ticket(b, DeskKit.XId + 40f, y, 330f,
                ((string)best["name"]).ToUpper() + "'S " + unit.ToUpper(),
                new List<DeskKit.TicketLine>(), "costs, each",
                "$" + M(SimWorks.Num(best, "unit_cost")), "", DrawnUI.Sage);
            DeskKit.Ticket(b, DeskKit.XId + 720f, y, 330f,
                ((string)worst["name"]).ToUpper() + "'S " + unit.ToUpper(),
                new List<DeskKit.TicketLine>(), "costs, each",
                "$" + M(SimWorks.Num(worst, "unit_cost")), "", DrawnUI.Coral);
            TextMeshProUGUI mid = b.L(string.Format(
                    "+${0} every {1} —\nrent, wages and learning, nothing else", M(diff), unit),
                DeskKit.XId + 390f, y + 40f, DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 320f);
            mid.alignment = TextAlignmentOptions.Top;
            return y + 156f;
        }

        // ─────────────────────────── the helpers ───────────────────────────

        static int FlagshipI(GameState s)
        {
            if (SimFactory.Active(s)) return Gd.Maxi(SimFactory.FlagshipIndex(s), 0);
            return 0;
        }

        static double BlendedMargin(GameState s)
        {
            double m = 0.0, v = 0.0;
            foreach (Dictionary<string, object> r in SimDivisions.WorksBook(s, "offer"))
            {
                if ((string)r["kind"] != "offer") continue;
                m += SimWorks.Num(r, "margin_each") * SimWorks.Num(r, "vol");
                v += SimWorks.Num(r, "vol");
            }
            return m / Gd.Maxf(v, 0.001);
        }

        static string AxisWord(string axis, int n)
        {
            if (axis == "site") return n != 1 ? "roofs" : "roof";
            if (axis == "product") return n != 1 ? "products" : "product";
            return n != 1 ? "offers" : "offer";
        }

        static string Left(string s, int n)
        {
            return s.Length > n ? s.Substring(0, n) : s;
        }

        /// LONG-TEXT LAW: a line that would run under the hero's corner block
        /// is measured and trimmed, never left to collide.
        internal static string Fit(string s, float w, float size)
        {
            if (DrawnUI.MeasureWidth(s, size) <= w) return s;
            string t = s;
            while (t.Length > 1 && DrawnUI.MeasureWidth(t + "…", size) > w)
                t = t.Substring(0, t.Length - 1);
            return t.TrimEnd() + "…";
        }

        /// Signed money for a value column: −$73.54, never $-73.54.
        internal static string Money(double v)
        {
            return v < 0.0 ? "−$" + M(-v) : "$" + M(v);
        }

        internal static string M(double v)
        {
            if (Math.Abs(v - Math.Round(v)) >= 0.005 && Math.Abs(v) < 100.0)
                return v.ToString("F2", CultureInfo.InvariantCulture);
            return Commas(Gd.RoundToInt(v));
        }

        internal static string Commas(int n)
        {
            string t = Math.Abs(n).ToString(CultureInfo.InvariantCulture);
            string outp = "";
            while (t.Length > 3)
            {
                outp = "," + t.Substring(t.Length - 3) + outp;
                t = t.Substring(0, t.Length - 3);
            }
            return (n < 0 ? "-" : "") + t + outp;
        }

        public static void Handle(BinderScreen b, string id)
        {
            object mode;
            if (b.Desk.TryGetValue("mode", out mode) && mode != null
                && mode.ToString() == "arrange")
            {
                DeskArrange.Handle(b, id);
                return;
            }
            if (id == "leave")
            {
                b.Desk.Remove("page");
                b.Desk.Remove("row");
            }
        }
    }
}
