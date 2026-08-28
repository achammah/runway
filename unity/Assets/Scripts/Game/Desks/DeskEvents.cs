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
    /// per line, "\tanswered" appended once answered — device-local UX
    /// state, never in a save).
    ///
    /// DAG3 (13-binder § events): action letters carry their DO inline —
    /// [answer] lands on the ask's desk via JumpToAsk (spotlit when the row
    /// names its control), [read terms] opens the paper's desk. Pressing the
    /// DO auto-files the letter with the answered mark and the filed row
    /// wears ✓. The LOG divider badge counts unread ACTION letters only
    /// (UnreadActionCount — the rail reads it).
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
            Dictionary<string, string> read = ReadMarks(s);
            int unread = 0;
            string newest = "";
            for (int i = 0; i < letters.Count; i++)
            {
                if (read.ContainsKey(letters[i].Key)) continue;
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

        /// <summary>THE RAIL'S NUMBER (13-binder § events): unread ACTION
        /// letters only — mail that needs an answer and has not even been
        /// opened. The LOG divider badge renders this.</summary>
        public static int UnreadActionCount(GameState s)
        {
            Dictionary<string, string> marks = ReadMarks(s);
            int n = 0;
            List<Letter> letters = Letters(s);
            for (int i = 0; i < letters.Count; i++)
                if (letters[i].Action && !marks.ContainsKey(letters[i].Key)) n += 1;
            return n;
        }

        // ── S8/S10 — the rail speaks for the desk (probe-guarded, never must) ──

        /// The tray never sleeps — the world writes in every era.
        public static bool IsDormant(GameState s)
        {
            return false;
        }

        /// The tab's four characters: how much mail waits.
        public static string MicroStatus(GameState s)
        {
            Dictionary<string, string> marks = ReadMarks(s);
            int unread = 0;
            List<Letter> letters = Letters(s);
            for (int i = 0; i < letters.Count; i++)
                if (!marks.ContainsKey(letters[i].Key)) unread += 1;
            return unread > 0 ? unread + " unread" : "";
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

        /// One key per line; "key\tanswered" once answered. A bare line from
        /// an older tray reads as plain read — no book loses its marks.
        static Dictionary<string, string> ReadMarks(GameState s)
        {
            var outp = new Dictionary<string, string>();
            try
            {
                if (File.Exists(MarksPath(s)))
                    foreach (string line in File.ReadAllLines(MarksPath(s)))
                    {
                        if (string.IsNullOrEmpty(line)) continue;
                        int tab = line.IndexOf('\t');
                        if (tab < 0) outp[line] = "";
                        else outp[line.Substring(0, tab)] = line.Substring(tab + 1);
                    }
            }
            catch (Exception) { }
            return outp;
        }

        static void MarkRead(GameState s, string key)
        {
            WriteMark(s, key, "");
        }

        /// The answered mark — the same store, a stronger value; re-reading
        /// never downgrades it.
        static void MarkAnswered(GameState s, string key)
        {
            WriteMark(s, key, "answered");
        }

        static void WriteMark(GameState s, string key, string value)
        {
            try
            {
                Dictionary<string, string> marks = ReadMarks(s);
                string cur;
                if (marks.TryGetValue(key, out cur) && cur == "answered"
                    && value != "answered") return;
                if (marks.TryGetValue(key, out cur) && cur == value) return;
                marks[key] = value;
                var lines = new List<string>();
                foreach (KeyValuePair<string, string> kv in marks)
                    lines.Add(kv.Value == "" ? kv.Key : kv.Key + "\t" + kv.Value);
                File.WriteAllLines(MarksPath(s), lines.ToArray());
            }
            catch (Exception) { }
        }

        static bool IsAnswered(Dictionary<string, string> marks, string key)
        {
            string v;
            return marks.TryGetValue(key, out v) && v == "answered";
        }

        // ── the page ───────────────────────────────────────────────────────

        public static void Draw(BinderScreen b)
        {
            GameState s = b.State;
            List<Letter> letters = Letters(s);
            Dictionary<string, string> read = ReadMarks(s);
            var unread = new List<Letter>();
            var readByWk = new SortedDictionary<int, List<Letter>>();
            for (int i = 0; i < letters.Count; i++)
            {
                Letter ld = letters[i];
                if (read.ContainsKey(ld.Key))
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
            // S5 — the arrow beside the count: more or less mail than last open
            string prev = b.SeenPrev("events", "unread");
            bool movedMail = b.Seen("events", "unread", unread.Count.ToString());
            int prevN;
            if (movedMail && int.TryParse(prev, out prevN))
                DeskKit.DeltaArrow(b,
                    DeskKit.XId + DrawnUI.MeasureWidth(hs[0], DeskKit.HeroBig) + 16f,
                    34f, unread.Count, prevN);
            // S4 — the hero count is pressable: the tray, counted out
            int actionN = 0;
            for (int i = 0; i < letters.Count; i++) if (letters[i].Action) actionN += 1;
            DeskKit.PressReceipt(b, new Rect(DeskKit.XId, 6f, 460f, 64f),
                "the tray, counted", new List<DeskKit.TicketLine>
                {
                    new DeskKit.TicketLine { Label = "letters on file",
                        Value = letters.Count.ToString() },
                    new DeskKit.TicketLine { Label = "need an answer",
                        Value = actionN.ToString(),
                        Col = actionN > 0 ? DrawnUI.Coral : DrawnUI.Ink },
                    new DeskKit.TicketLine { Label = "unread",
                        Value = unread.Count.ToString() },
                });

            if (letters.Count == 0)
            {
                // S1 — the zero state teaches what the tray WILL hold and
                // points at the desk that makes the world start writing.
                DeskKit.ZeroState(b, new DeskKit.ZeroStateCfg
                {
                    WillShow = "letters and notices — the world writing to you",
                    WouldLine = "a letter files with its week stamp, its money in "
                        + "its own column, and its desk one press away",
                    ActionLabel = "read the street — the rivals",
                    ActionCb = () => { b.FocusDesk("the street", "", "events"); },
                    WakesHint = "the tray fills as the world acts — deadlines, "
                        + "applications, asks, the rivals' moves",
                });
                return;
            }
            // THE UNREAD — bold, newest first, the dot on action letters.
            // THE COLLAPSE LAW: a letter that needs an answer never folds away;
            // the newest quiet letters fill whatever the face-up cap has left.
            var faceUp = new List<Letter>();
            var quiet = new List<Letter>();
            for (int i = 0; i < unread.Count; i++)
            {
                if (unread[i].Action) faceUp.Add(unread[i]);
                else quiet.Add(unread[i]);
            }
            int quietSlots = Math.Max(UnreadCap - faceUp.Count, 0);
            for (int i = 0; i < Math.Min(quiet.Count, quietSlots); i++)
                faceUp.Add(quiet[i]);
            faceUp.Sort((a, c) => c.Wk.CompareTo(a.Wk));
            for (int i = 0; i < faceUp.Count; i++)
                y = LetterRow(b, s, y, faceUp[i], false, false);
            int hidden = unread.Count - faceUp.Count;
            if (hidden > 0)
            {
                b.L(string.Format("+{0} more unread below the fold", hidden),
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
                            y = LetterRow(b, s, y, pile[k], true,
                                IsAnswered(read, pile[k].Key));
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

        /// One letter row (twin of _letter_row): dot when action (✓ once
        /// answered) · the text · the money · the stamp · the DO. An action
        /// letter's DO is its verb — [answer] via JumpToAsk, [read terms] on
        /// offer/board paper — and pressing it auto-files the letter with the
        /// answered mark. Quiet letters keep the plain desk jump.
        static float LetterRow(BinderScreen b, GameState s, float y, Letter ld,
                               bool isRead, bool answered)
        {
            float x = DeskKit.XId;
            if (answered)
                b.L("✓", x + 4f, y - 2f, DeskKit.Detail, DrawnUI.Sage, 30f);
            else if (ld.Action)
                DeskKit.SevDot(b, x, y + 4f, 2);
            // letter texts carry world names (S6): one measured line, no wrap
            DeskKit.FitLine(b, ld.Text, x + 36f, y, isRead ? DeskKit.Detail : 26f,
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
            if (ld.Action && !answered)
            {
                string verb = key.StartsWith("mna:") || key.StartsWith("buyout:")
                    || key.StartsWith("board:") ? "read terms" : "answer";
                Letter cap = ld;
                DeskKit.Word(b, verb + " ->", x + 962f, y - 4f, () =>
                {
                    MarkAnswered(b.State, key);
                    DoJump(b, cap);
                }, DeskKit.Detail, DrawnUI.Coral, 190f);
            }
            else
                DeskKit.Word(b, dsk + " ->", x + 962f, y - 4f, () =>
                {
                    MarkRead(b.State, key);
                    b.FocusDesk(dsk, "", "events");
                }, DeskKit.Detail,
                    isRead ? DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f) : DrawnUI.Coral, 190f);
            return y + 38f;
        }

        /// THE ANSWER'S LANDING (twin of _do_jump, best-effort mapping): the
        /// attention row this letter stands behind — same desk once aliased,
        /// the name token preferred when the key carries one (ask:NAME:wk) —
        /// JumpToAsk'd so a named control lands spotlit; else a plain
        /// FocusDesk. Either way the back pill remembers "events".
        static void DoJump(BinderScreen b, Letter ld)
        {
            string want = BinderScreen.DeskAlias(ld.Desk);
            string nameTok = "";
            if (ld.Key.StartsWith("ask:"))
            {
                string[] parts = ld.Key.Split(':');
                if (parts.Length >= 2) nameTok = parts[1];
            }
            AttentionItem fallback = null;
            foreach (AttentionItem r in SimEngine.AttentionItems(b.State))
            {
                if (BinderScreen.DeskAlias(r.Desk) != want) continue;
                if (nameTok != "" && (r.Label ?? "")
                    .IndexOf(nameTok, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    b.JumpToAsk(r, "events");
                    return;
                }
                if (fallback == null) fallback = r;
            }
            if (fallback != null && !string.IsNullOrEmpty(fallback.Control))
                b.JumpToAsk(fallback, "events");
            else
                b.FocusDesk(ld.Desk, "", "events");
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
