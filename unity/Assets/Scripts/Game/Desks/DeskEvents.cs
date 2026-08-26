using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — THE LOG · "events", the mail (twin of desk_events.gd). W2 lane:
    /// L-COMPANY. THE QUESTION: "what has the world sent us?"
    ///
    /// The inbox STREAM: letters derived from the run's own durable records —
    /// deadlines, the weather turning, applications, asks, filed notes,
    /// signed paper, the rivals' moves — newest first, unread bold, each with
    /// its week stamp and desk jump. Threats = the standing dangers RANKED;
    /// events = the stream AS IT HAPPENED; an action letter wears the dot and
    /// stands in threats until answered.
    ///
    /// READ-MARKS live beside the tour flag in the engine's local store,
    /// keyed per-run by sim seed (mail_read_&lt;seed&gt;.txt, one letter key
    /// per line — device-local UX state, never in a save).
    /// </summary>
    public static class DeskEvents
    {
        public const string Question = "what has the world sent us?";

        public const int UnreadCap = 9;

        sealed class Letter
        {
            public string Key = "";
            public int Wk;
            public string Stamp = "";
            public string Text = "";
            public string Value = "";
            public string Desk = "";
            public bool Action;
        }

        static string F(long n)
        {
            return Math.Abs(n).ToString("#,##0", CultureInfo.InvariantCulture)
                .Insert(0, n < 0 ? "-" : "");
        }

        public static string[] HeroSummary(GameState s)
        {
            List<Letter> letters = Letters(s);
            HashSet<string> read = ReadMarks(s);
            int unread = 0;
            string newest = "";
            for (int i = 0; i < letters.Count; i++)
            {
                if (read.Contains(letters[i].Key)) continue;
                unread += 1;
                if (newest == "") newest = letters[i].Text;
            }
            if (letters.Count == 0)
                return new[] { "an empty tray", "the world writes as it acts" };
            if (unread == 0)
                return new[] { "all read",
                    string.Format("{0} letters filed by week", letters.Count) };
            return new[] { string.Format("{0} unread", unread), newest };
        }

        static List<Letter> Letters(GameState s)
        {
            var outp = new List<Letter>();
            if (s.Mna != null && !string.IsNullOrEmpty(s.Mna.Buyer))
                outp.Add(new Letter
                {
                    Key = string.Format("mna:{0}:{1}", s.Mna.Buyer, s.Mna.ExpiresWeek),
                    Wk = Math.Max(s.MnaLastWeek, 1),
                    Stamp = string.Format("answer by wk {0}", s.Mna.ExpiresWeek),
                    Text = string.Format("{0} makes an offer for the company", s.Mna.Buyer),
                    Value = "$" + F(s.Mna.Price), Desk = "cap table", Action = true,
                });
            if (s.BuyoutOffer != null && s.BuyoutOffer.Count > 0)
            {
                object bo;
                string buyer = s.BuyoutOffer.TryGetValue("buyer", out bo) && bo != null
                    ? Convert.ToString(bo) : "someone";
                object cash;
                long cashV = s.BuyoutOffer.TryGetValue("cash", out cash) && cash != null
                    ? Convert.ToInt64(cash) : 0;
                outp.Add(new Letter
                {
                    Key = "buyout:" + buyer, Wk = s.Week, Stamp = "on the table",
                    Text = string.Format("{0} wants to buy the company — the fine print waits",
                        buyer),
                    Value = "$" + F(cashV), Desk = "cap table", Action = true,
                });
            }
            if (s.Board != null && s.Board.ReviewWeek >= s.Week)
                outp.Add(new Letter
                {
                    Key = "board:" + s.Board.ReviewWeek, Wk = s.Week,
                    Stamp = string.Format("review wk {0}", s.Board.ReviewWeek),
                    Text = "the board writes: the revenue covenant is coming due",
                    Value = "$" + F(s.Board.TargetRevenue) + "/wk",
                    Desk = "cap table", Action = s.Board.Strikes > 0,
                });
            for (int i = 0; i < s.Clocks.Count; i++)
            {
                Clock cd = s.Clocks[i];
                int fire = s.Week + cd.WeeksLeft;
                string cons = cd.Consequence ?? "";
                outp.Add(new Letter
                {
                    Key = string.Format("clock:{0}:{1}",
                        cons.Length > 20 ? cons.Substring(0, 20) : cons, fire),
                    Wk = fire, Stamp = string.Format("fires wk {0}", fire),
                    Text = "a deadline: " + cons, Value = "", Desk = "threats",
                    Action = true,
                });
            }
            string[] weather = { "winter_watch", "boom_watch", "funding_winter", "boom" };
            for (int i = 0; i < weather.Length; i++)
            {
                if (!SimEngine.HasStatus(s, weather[i])) continue;
                outp.Add(new Letter
                {
                    Key = "weather:" + weather[i], Wk = s.Week,
                    Stamp = string.Format("{0} wks left", SimStreet.WeeksLeft(s, weather[i])),
                    Text = SimStreet.BANNER[weather[i]].ToLowerInvariant(),
                    Value = "", Desk = "the street", Action = false,
                });
            }
            for (int i = 0; i < s.Applicants.Count; i++)
            {
                Applicant ad = s.Applicants[i];
                outp.Add(new Letter
                {
                    Key = string.Format("apply:{0}:{1}", ad.Name, ad.AppliedWeek),
                    Wk = ad.AppliedWeek, Stamp = string.Format("wk {0}", ad.AppliedWeek),
                    Text = string.Format("{0} applied — {1}", ad.Name, ad.Role),
                    Value = "", Desk = "team", Action = false,
                });
            }
            for (int i = 0; i < s.Employees.Count; i++)
            {
                Employee ed = s.Employees[i];
                if (!ed.WantsRaise) continue;
                int wk = ed.AskedWeek >= 0 ? ed.AskedWeek : s.Week;
                outp.Add(new Letter
                {
                    Key = string.Format("ask:{0}:{1}", ed.Name, wk), Wk = wk,
                    Stamp = string.Format("wk {0}", wk),
                    Text = string.Format("{0} asks about money", ed.Name),
                    Value = "", Desk = "team", Action = true,
                });
            }
            for (int i = 0; i < s.Loans.Count; i++)
            {
                Loan ld = s.Loans[i];
                outp.Add(new Letter
                {
                    Key = string.Format("loan:{0}:{1}", ld.Kind, ld.TakenWeek),
                    Wk = ld.TakenWeek, Stamp = string.Format("wk {0}", ld.TakenWeek),
                    Text = string.Format("the {0} note, filed — the Mondays are booked",
                        ld.Kind ?? "bank"),
                    Value = "$" + F(ld.Balance), Desk = "the bank", Action = ld.Missed > 0,
                });
            }
            for (int i = 0; i < s.Instruments.Count; i++)
            {
                Instrument idd = s.Instruments[i];
                outp.Add(new Letter
                {
                    Key = string.Format("paper:{0}:{1}", idd.Holder, idd.SignedWk),
                    Wk = idd.SignedWk, Stamp = string.Format("wk {0}", idd.SignedWk),
                    Text = string.Format("signed: a {0} from {1}", idd.Kind, idd.Holder),
                    Value = "$" + F(idd.Amount), Desk = "cap table", Action = false,
                });
            }
            for (int i = 0; i < s.Rivals.Count; i++)
            {
                Rival rd = s.Rivals[i];
                if (rd.Log == null) continue;
                for (int k = 0; k < rd.Log.Count; k++)
                {
                    string line = rd.Log[k] ?? "";
                    int colon = line.IndexOf(':');
                    if (!line.StartsWith("wk") || colon <= 2) continue;
                    int wk2;
                    if (!int.TryParse(line.Substring(2, colon - 2), out wk2)) continue;
                    outp.Add(new Letter
                    {
                        Key = string.Format("rival:{0}:{1}", rd.Name, wk2), Wk = wk2,
                        Stamp = string.Format("wk {0}", wk2),
                        Text = string.Format("{0}: {1}", rd.Name,
                            line.Substring(colon + 1).Trim()),
                        Value = "", Desk = "the street", Action = false,
                    });
                }
            }
            if (s.PriceBook != null && s.PriceBook.Count > 0)
                outp.Add(new Letter
                {
                    Key = "pricebook:1", Wk = 1, Stamp = "wk 1",
                    Text = "the price book arrived — every structural door, priced in advance",
                    Value = "", Desk = "the works", Action = false,
                });
            // newest first; ties keep builder order (stable sort by index)
            var idx = new Dictionary<Letter, int>();
            for (int i = 0; i < outp.Count; i++) idx[outp[i]] = i;
            outp.Sort((a, c) => a.Wk != c.Wk ? c.Wk.CompareTo(a.Wk)
                : idx[a].CompareTo(idx[c]));
            return outp;
        }

        // ── the read-marks (the tour flag's own store, per-run keyed) ──────

        static string MarksPath(GameState s)
        {
            return Path.Combine(Application.persistentDataPath,
                string.Format("mail_read_{0}.txt", s.SimSeed));
        }

        static HashSet<string> ReadMarks(GameState s)
        {
            var outp = new HashSet<string>();
            try
            {
                if (File.Exists(MarksPath(s)))
                    foreach (string line in File.ReadAllLines(MarksPath(s)))
                        if (!string.IsNullOrEmpty(line)) outp.Add(line);
            }
            catch (Exception) { }
            return outp;
        }

        static void MarkRead(GameState s, string key)
        {
            try
            {
                HashSet<string> marks = ReadMarks(s);
                if (marks.Add(key))
                {
                    var lines = new List<string>(marks);
                    File.WriteAllLines(MarksPath(s), lines.ToArray());
                }
            }
            catch (Exception) { }
        }

        // ── the page ───────────────────────────────────────────────────────

        public static void Draw(BinderScreen b)
        {
            GameState s = b.State;
            List<Letter> letters = Letters(s);
            HashSet<string> read = ReadMarks(s);
            var unread = new List<Letter>();
            var readByWk = new SortedDictionary<int, List<Letter>>();
            for (int i = 0; i < letters.Count; i++)
            {
                Letter ld = letters[i];
                if (read.Contains(ld.Key))
                {
                    if (!readByWk.ContainsKey(ld.Wk)) readByWk[ld.Wk] = new List<Letter>();
                    readByWk[ld.Wk].Add(ld);
                }
                else unread.Add(ld);
            }

            // HERO — the unread count answers the question
            string[] hs = HeroSummary(s);
            float y = DeskKit.HeroBand(b, hs[0], hs[1],
                unread.Count > 0 && unread[0].Action ? DeskKit.Alert : DrawnUI.Ink);

            if (letters.Count == 0)
                DeskKit.Empty(b, DeskKit.XId, y,
                    "the tray is empty — the world has not written yet.",
                    "deadlines, applications, filed notes and the street's moves all land here.");
            // THE UNREAD — bold, newest first, the dot on action letters
            for (int i = 0; i < Math.Min(unread.Count, UnreadCap); i++)
                y = LetterRow(b, s, y, unread[i], false);
            if (unread.Count > UnreadCap)
            {
                b.L(string.Format("+{0} more unread below the fold", unread.Count - UnreadCap),
                    DeskKit.XId + 36f, y, 17f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 500f);
                y += 30f;
            }

            // THE READ — folded by week, reopened a week at a time
            object owv;
            int openWk = b.Desk.TryGetValue("openwk", out owv) && owv != null
                ? Convert.ToInt32(owv) : -1;
            if (readByWk.Count > 0)
            {
                y = DeskKit.PenRule(b, y + 6f);
                var wks = new List<int>(readByWk.Keys);
                wks.Sort((a, c) => c.CompareTo(a));
                for (int i = 0; i < wks.Count && y <= 760f; i++)
                {
                    int wk2 = wks[i];
                    List<Letter> pile = readByWk[wk2];
                    if (wk2 == openWk)
                    {
                        b.L(string.Format("wk {0} — read:", wk2), DeskKit.XId, y, 17f,
                            DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 300f);
                        y += 26f;
                        for (int k = 0; k < pile.Count; k++)
                            y = LetterRow(b, s, y, pile[k], true);
                    }
                    else
                    {
                        int wkv = wk2;
                        DeskKit.Word(b, string.Format("wk {0} — {1} read  ->", wkv, pile.Count),
                            DeskKit.XId, y - 4f, () => { b.Desk["openwk"] = wkv; },
                            DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f), 420f);
                        y += 34f;
                    }
                }
            }

            int actions = 0;
            for (int i = 0; i < letters.Count; i++) if (letters[i].Action) actions += 1;
            DeskKit.Footer(b,
                string.Format("{0} letters on file · {1} need an answer",
                    letters.Count, actions),
                "threats ranks the standing dangers — this page is the stream as it "
                + "happened · an action letter also stands in threats until answered",
                "", 820f, 852f);
        }

        static float LetterRow(BinderScreen b, GameState s, float y, Letter ld, bool isRead)
        {
            float x = DeskKit.XId;
            if (ld.Action) DeskKit.SevDot(b, x, y + 4f, 2);
            b.L(ld.Text, x + 36f, y, isRead ? DeskKit.Detail : 26f,
                isRead ? DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f) : DrawnUI.Ink, 600f);
            if (!string.IsNullOrEmpty(ld.Value))
            {
                TextMeshProUGUI v = b.L(ld.Value, x + 640f, y, DeskKit.Detail,
                    isRead ? DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f) : DrawnUI.Ink, 170f);
                v.alignment = TextAlignmentOptions.TopRight;
            }
            b.L(ld.Stamp, x + 826f, y + 2f, 17f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f), 130f);
            string dsk = ld.Desk;
            string key = ld.Key;
            DeskKit.Word(b, dsk + " ->", x + 962f, y - 4f, () =>
            {
                MarkRead(b.State, key);
                b.FocusDesk(dsk);
            }, DeskKit.Detail, DrawnUI.Coral, 190f);
            return y + 38f;
        }

        public static void Handle(BinderScreen b, string id)
        {
            if (id.StartsWith("go:"))
            {
                string[] parts = id.Substring(3).Split('|');
                if (parts.Length == 2) MarkRead(b.State, parts[1]);
                b.FocusDesk(parts[0]);
            }
            else if (id.StartsWith("openwk:"))
                b.Desk["openwk"] = int.Parse(id.Substring(7));
        }
    }
}
