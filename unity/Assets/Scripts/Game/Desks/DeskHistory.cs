using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — THE LOG · "history", the run's own ledger (twin of
    /// desk_history.gd). W2 lane: L-COMPANY.
    /// THE QUESTION: "how did we get here?"
    ///
    /// Sparklines (cash · customers) above THE BOOK: a ledger-sheet row per
    /// week — wk · cash · net · customers · the headline · receipts ->. Older
    /// weeks fold into a subtotal (the collapse ladder; the recent rows are
    /// the money-nearest and never hide); the TOTAL is double-ruled; answered
    /// momentary tabs file as flagged memo rows.
    /// </summary>
    public static class DeskHistory
    {
        public const string Question = "how did we get here?";

        public const int FaceUp = 7;
        public const int FaceUpAll = 14;

        /// Signed money the game's way: −$16,150, never $-16,150.
        static string Signed(long n)
        {
            return (n < 0 ? "−$" : "$") + F(Math.Abs(n));
        }

        static string F(long n)
        {
            return Math.Abs(n).ToString("#,##0", CultureInfo.InvariantCulture)
                .Insert(0, n < 0 ? "-" : "");
        }

        public static string[] HeroSummary(GameState s)
        {
            int n = s.MetricHistory.Count;
            if (n == 0)
                return new[] { "a blank book", "the first week writes the first row" };
            return new[] { string.Format("{0} weeks", n),
                "the run's own ledger — receipts behind each row" };
        }

        struct EraSpan { public string Era; public int FromWk; }

        /// The engine logs "MOVED UP: garage -> office (reason)" (the Godot
        /// twin writes a → arrow) — the era word sits between the arrow and
        /// the parenthesis. {era, from_wk} oldest first, always from wk 1.
        static List<EraSpan> EraSpans(GameState s)
        {
            var moves = new List<EraSpan>();
            for (int i = 0; i < s.History.Count; i++)
            {
                string e = s.History[i].Entry ?? "";
                if (!e.StartsWith("MOVED UP:", StringComparison.Ordinal)
                    && !e.StartsWith("MOVED DOWN:", StringComparison.Ordinal)) continue;
                int arrow = e.IndexOf('→');
                if (arrow < 0)
                {
                    arrow = e.IndexOf("-> ", StringComparison.Ordinal);
                    if (arrow >= 0) arrow += 1;
                }
                if (arrow < 0) continue;
                string tail = e.Substring(arrow + 1).Trim();
                int par = tail.IndexOf('(');
                if (par >= 0) tail = tail.Substring(0, par);
                tail = tail.Trim();
                if (tail.Length > 0)
                    moves.Add(new EraSpan { Era = tail, FromWk = s.History[i].Week });
            }
            var spans = new List<EraSpan>
            {
                new EraSpan { Era = moves.Count > 0 ? "the early road" : s.Era, FromWk = 1 },
            };
            spans.AddRange(moves);
            return spans;
        }

        static int Net(MetricSnapshot row)
        {
            return row.Net.HasValue ? row.Net.Value : row.Revenue - row.Burn;
        }

        /// <summary>The week's headline: the event title the action log kept,
        /// else what the founder wrote, else a quiet week.</summary>
        static string Headline(GameState s, int wk)
        {
            for (int i = 0; i < s.History.Count; i++)
            {
                if (s.History[i].Week != wk) continue;
                string e = s.History[i].Entry ?? "";
                int a = e.IndexOf("event '", StringComparison.Ordinal);
                if (a >= 0)
                {
                    int z = e.IndexOf('\'', a + 7);
                    if (z > a) return e.Substring(a + 7, z - a - 7);
                }
            }
            for (int i = 0; i < s.RunHistory.Count; i++)
                if (s.RunHistory[i].Wk == wk && !string.IsNullOrEmpty(s.RunHistory[i].Said))
                    return s.RunHistory[i].Said;
            return "a quiet week";
        }

        static RunHistoryEntry WeekReceipts(GameState s, int wk)
        {
            for (int i = 0; i < s.RunHistory.Count; i++)
                if (s.RunHistory[i].Wk == wk) return s.RunHistory[i];
            return null;
        }

        public static void Draw(BinderScreen b)
        {
            GameState s = b.State;
            object mode;
            if (b.Desk.TryGetValue("mode", out mode) && Convert.ToString(mode) == "receipts")
            {
                object wkv;
                b.Desk.TryGetValue("wk", out wkv);
                DrawReceipts(b, s, wkv != null ? Convert.ToInt32(wkv) : 0);
                return;
            }
            List<MetricSnapshot> rows = s.MetricHistory;
            float y = DeskKit.HeroBand(b, string.Format("{0} weeks on the books", rows.Count),
                "the run's own ledger — a row per week, the receipts behind each",
                DrawnUI.Ink);
            if (rows.Count == 0)
            {
                DeskKit.Empty(b, DeskKit.XId, y,
                    "the book is blank — the first LOCK IN writes the first row.",
                    "play the week; the ledger remembers everything after that.");
                return;
            }

            // SPARKLINES — the shape of the run before the rows
            b.L("cash", DeskKit.XId, y, 20f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 200f);
            b.Spark("cash", DeskKit.XId, y + 26f, 540f, 84f, DrawnUI.Blue);
            b.L("customers", DeskKit.XId + 580f, y, 20f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 200f);
            b.Spark("customers", DeskKit.XId + 580f, y + 26f, 540f, 84f, DrawnUI.Sage);
            y += 124f;

            // THE BOOK — the ledger sheet, older weeks folded, recent face-up
            object allv;
            bool allOpen = b.Desk.TryGetValue("all", out allv) && allv is bool && (bool)allv;
            int face = allOpen ? FaceUpAll : FaceUp;
            DeskKit.LedgerBox sheet = DeskKit.LedgerSheet(b, DeskKit.XId, y, 1120f,
                new List<DeskKit.LedgerCol>
                {
                    new DeskKit.LedgerCol { Label = "wk", W = 64f },
                    new DeskKit.LedgerCol { Label = "cash", W = 150f, Align = "right" },
                    new DeskKit.LedgerCol { Label = "net", W = 132f, Align = "right" },
                    new DeskKit.LedgerCol { Label = "customers", W = 140f, Align = "right" },
                    new DeskKit.LedgerCol { Label = "the headline", W = 400f },
                    new DeskKit.LedgerCol { Label = "", W = 100f },
                }, 2, false, "cash & net in $, at week's end");
            // THE ERA SECTIONS (the collapse law): folded weeks group under the
            // era stamps the action log wrote; each extra section trades two
            // face-up rows so the sheet keeps its height budget.
            List<EraSpan> spans = EraSpans(s);
            int faceAdj = Math.Max(3, face - 2 * (spans.Count - 1));
            int older = rows.Count - faceAdj;
            if (older > 0)
            {
                if (spans.Count <= 1)
                {
                    int subNet = 0;
                    for (int i = 0; i < older; i++) subNet += Net(rows[i]);
                    DeskKit.LedgerSection(b, sheet, string.Format("the road so far — wk {0}–{1}",
                        rows[0].Wk, rows[older - 1].Wk));
                    DeskKit.LedgerSubtotal(b, sheet,
                        string.Format("subtotal — {0} folded weeks", older),
                        Signed(subNet), "open the whole book below");
                }
                else
                {
                    int spanI = 0;
                    int secStart = 0;
                    int sub = 0;
                    for (int i = 0; i < older; i++)
                    {
                        int wkI = rows[i].Wk;
                        while (spanI + 1 < spans.Count && wkI >= spans[spanI + 1].FromWk)
                        {
                            if (i > secStart)
                            {
                                DeskKit.LedgerSection(b, sheet, string.Format("{0} — wk {1}–{2}",
                                    spans[spanI].Era, rows[secStart].Wk, rows[i - 1].Wk));
                                DeskKit.LedgerSubtotal(b, sheet,
                                    string.Format("subtotal — {0} weeks", i - secStart),
                                    Signed(sub), "");
                                secStart = i;
                                sub = 0;
                            }
                            spanI += 1;
                        }
                        sub += Net(rows[i]);
                    }
                    DeskKit.LedgerSection(b, sheet, string.Format("{0} — wk {1}–{2}",
                        spans[spanI].Era, rows[secStart].Wk, rows[older - 1].Wk));
                    DeskKit.LedgerSubtotal(b, sheet,
                        string.Format("subtotal — {0} weeks", older - secStart),
                        Signed(sub), "open the whole book below");
                }
            }
            int totalNet = 0;
            for (int i = 0; i < rows.Count; i++) totalNet += Net(rows[i]);
            for (int i = Math.Max(older, 0); i < rows.Count; i++)
            {
                MetricSnapshot row = rows[i];
                int net = Net(row);
                int wk = row.Wk;
                DeskKit.LedgerRow(b, sheet, new List<string>
                {
                    wk.ToString(), "$" + F(row.Cash),
                    (net >= 0 ? "+$" : "−$") + F(Math.Abs(net)),
                    F(row.Customers),
                    Headline(s, wk).Length > 44
                        ? Headline(s, wk).Substring(0, 44) : Headline(s, wk),
                    "receipts ->",
                }, new DeskKit.LedgerRowCfg
                {
                    Col = net >= 0 ? DrawnUI.Sage : DrawnUI.Coral,
                    OnPress = () => { b.Desk["mode"] = "receipts"; b.Desk["wk"] = wk; },
                });
            }
            DeskKit.LedgerTotal(b, sheet, "the run so far",
                (totalNet >= 0 ? "+$" : "−$") + F(Math.Abs(totalNet)),
                totalNet >= 0 ? DrawnUI.Sage : DrawnUI.Coral);
            // FILINGS — answered momentary tabs file here as flagged rows
            if (s.ExitValue > 0)
                DeskKit.LedgerMemo(b, sheet, "★ filed: the company was sold",
                    "$" + F(s.ExitValue), "the buyout was accepted");
            else if (s.Mna == null && s.MnaLastWeek > 0)
                DeskKit.LedgerMemo(b, sheet, "★ filed: a buyout offer came and went",
                    "", string.Format("around wk {0} — answered or expired", s.MnaLastWeek));
            y = DeskKit.LedgerEnd(b, sheet);
            if (older > 0 && !allOpen && y <= 800f)
                DeskKit.FoldRow(b, DeskKit.XId, y, older, "weeks",
                    () => { b.Desk["all"] = true; });
            else if (allOpen && rows.Count > FaceUpAll)
                b.L(string.Format("+{0} earlier weeks stay folded in the subtotal",
                    rows.Count - FaceUpAll), DeskKit.XId, y + 4f, 17f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 500f);

            DeskKit.Footer(b,
                string.Format("cash: ${0} at wk {1} -> ${2} now · {3} today{4}",
                    F(rows[0].Cash), rows[0].Wk, F(s.Cash), s.EraDisplayName(),
                    s.Pivots > 0
                        ? string.Format(" · {0} pivots on the record", s.Pivots) : ""),
                "a row per week: what the week earned, what it cost, and the receipts "
                + "behind it · the total must square with the bank",
                "", 820f, 852f);
        }

        static void DrawReceipts(BinderScreen b, GameState s, int wk)
        {
            DeskKit.Back(b, "← the book", () =>
            {
                b.Desk.Remove("mode");
                b.Desk.Remove("wk");
            });
            float y = 64f;
            y = DeskKit.HeroBand(b, string.Format("week {0} — the receipts", wk),
                Headline(s, wk), DrawnUI.Ink, y);
            RunHistoryEntry rd = WeekReceipts(s, wk);
            if (rd == null)
                DeskKit.Empty(b, DeskKit.XId, y,
                    "no receipts survive for this week.",
                    "the DM's memory keeps the recent past verbatim and compresses the rest.");
            else
            {
                if (!string.IsNullOrEmpty(rd.Said))
                {
                    var l = b.L(string.Format("the move: \"{0}\"", rd.Said), DeskKit.XId, y,
                        DeskKit.Status, DrawnUI.WithAlpha(DrawnUI.Ink, 0.85f), 1100f);
                    y += Mathf.Max(BinderScreen.Height(l), 32f) + 6f;
                }
                string vr = rd.Verdict ?? "";
                if (rd.Roll != 0)
                    vr = vr == "" ? "d20=" + rd.Roll : vr + " · d20=" + rd.Roll;
                if (vr != "")
                {
                    b.L(vr, DeskKit.XId, y, DeskKit.Detail, DrawnUI.Blue, 1100f);
                    y += 34f;
                }
                y = DeskKit.PenRule(b, y + 4f);
                if (!string.IsNullOrEmpty(rd.Fx))
                {
                    var fl = b.L("· " + rd.Fx, DeskKit.XId, y, DeskKit.Detail,
                        DrawnUI.WithAlpha(DrawnUI.Ink, 0.75f), 1100f);
                    y += Mathf.Max(BinderScreen.Height(fl), 28f) + 2f;
                }
            }
            for (int i = 0; i < s.MetricHistory.Count; i++)
            {
                MetricSnapshot row = s.MetricHistory[i];
                if (row.Wk != wk) continue;
                int net = Net(row);
                DeskKit.Ticket(b, DeskKit.XId, y + 8f, 560f, "the week, in numbers",
                    new List<DeskKit.TicketLine>
                    {
                        new DeskKit.TicketLine { Label = "cash at week's end",
                            Value = "$" + F(row.Cash) },
                        new DeskKit.TicketLine { Label = "the week's net",
                            Value = (net >= 0 ? "+$" : "−$") + F(Math.Abs(net)),
                            Col = net >= 0 ? DrawnUI.Sage : DrawnUI.Coral },
                        new DeskKit.TicketLine { Label = "customers",
                            Value = F(row.Customers) },
                    }, "", "", "");
                break;
            }
        }

        public static void Handle(BinderScreen b, string id)
        {
            if (id.StartsWith("wk:"))
            {
                b.Desk["mode"] = "receipts";
                b.Desk["wk"] = int.Parse(id.Substring(3));
            }
            else if (id == "all") b.Desk["all"] = true;
            else if (id == "back")
            {
                b.Desk.Remove("mode");
                b.Desk.Remove("wk");
            }
        }
    }
}
