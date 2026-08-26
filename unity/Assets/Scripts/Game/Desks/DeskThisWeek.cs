using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — THE LOG · "this week", the desk you play from (twin of
    /// desk_this_week.gd). W2 lane: L-COMPANY. The binder's landing tab.
    /// THE QUESTION: "what happened, and what's our move?"
    ///
    /// HERO week + era + the situation · 1 THE CARD (the event, art slot) ·
    /// 2 YOUR MOVE (the written-move composer — the host's existing flow
    /// judges the words; clarify answers append after " — ") · 3 ARMED THIS
    /// WEEK (the staged-changes receipt list from the action log) · 4 LOCK IN
    /// (the die, behind THE PRE-ROLL REVIEW intercept).
    ///
    /// THE HOST SEAM: WeekCard {title, line, icon} + LockHook(text) + Draft —
    /// statics the host screen seeds each week; until the package lands the
    /// desk shows what it can prove and points the roll at the journal.
    /// </summary>
    public static class DeskThisWeek
    {
        public const string Question = "what happened, and what's our move?";

        public static Dictionary<string, object> WeekCard = new Dictionary<string, object>();
        public static Action<string> LockHook;
        public static string Draft = "";

        static string CardStr(string key)
        {
            object v;
            return WeekCard.TryGetValue(key, out v) && v != null ? Convert.ToString(v) : "";
        }

        public static string[] HeroSummary(GameState s)
        {
            string line = CardStr("title");
            if (line == "") line = "the desk you play from";
            return new[] { string.Format("week {0}", s.Week), line };
        }

        static string DeskStr(BinderScreen b, string key)
        {
            object v;
            return b.Desk.TryGetValue(key, out v) && v != null ? Convert.ToString(v) : "";
        }

        public static void Draw(BinderScreen b)
        {
            GameState s = b.State;

            // HERO — the week, the era, the situation in plain words
            float y = DeskKit.HeroBand(b, string.Format("week {0}", s.Week),
                string.Format("{0} · {1}", s.EraDisplayName(),
                    SimEngine.HealthBand(s).ToLowerInvariant()), DrawnUI.Ink);

            // 1 · THE CARD — the event and its bite, art slot left
            DeskKit.CardBox z1 = DeskKit.Zone(b, DeskKit.XId, y, 1120f, 132f, 1,
                "the card", "");
            string title = CardStr("title");
            string icon = CardStr("icon");
            if (icon != "")
                b.Icon(icon, z1.ContentX, z1.ContentY - 16f, 64f);
            float tx = z1.ContentX + (icon != "" ? 80f : 0f);
            if (title != "")
            {
                b.L(title, tx, z1.ContentY - 14f, DeskKit.Row, DrawnUI.Ink, 1000f - tx);
                b.L(CardStr("line"), tx, z1.ContentY + 20f, DeskKit.Detail,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.65f), 1020f - tx);
            }
            else
            {
                object lt;
                string lastT = s.LastOutcome != null
                    && s.LastOutcome.TryGetValue("title", out lt) && lt != null
                    ? Convert.ToString(lt) : "";
                b.L(lastT != "" ? "last week: " + lastT : "the first week is a blank page",
                    tx, z1.ContentY - 14f, DeskKit.Row,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.75f), 1000f);
                b.L("this week's card opens with the journal", tx, z1.ContentY + 20f,
                    DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 900f);
            }
            y = z1.Bottom + 12f;

            // 2 · YOUR MOVE — the composer
            DeskKit.CardBox z2 = DeskKit.Zone(b, DeskKit.XId, y, 1120f, 128f, 2,
                "your move", "plain words — the world asks a question when a number is missing");
            MoveField(b, z2.ContentX, z2.Cursor - 6f);
            y = z2.Bottom + 12f;

            // 3 · ARMED THIS WEEK — everything staged since the last roll
            List<string[]> staged = StagedRows(s, out List<Color> stagedCols);
            float z3H = 66f + Math.Max(Math.Min(staged.Count, 4) * 30f, 28f) + 10f;
            DeskKit.CardBox z3 = DeskKit.Zone(b, DeskKit.XId, y, 1120f, z3H, 3,
                "armed this week", "the receipt list — what the week will carry into the roll");
            float ry = z3.Cursor - 8f;
            if (staged.Count == 0)
                b.L("nothing staged since the last roll — steppers, signatures and arranges land here",
                    z3.ContentX, ry, DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 1040f);
            for (int i = 0; i < Math.Min(staged.Count, 4); i++)
            {
                b.L("· " + staged[i][0], z3.ContentX, ry, DeskKit.Detail, stagedCols[i], 1000f);
                ry += 30f;
            }
            if (staged.Count > 4)
                b.L(string.Format("+{0} more in the log", staged.Count - 4),
                    z3.ContentX, ry, 17f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 400f);
            y = z3.Bottom + 12f;

            // 4 · LOCK IN — the die, behind THE PRE-ROLL REVIEW
            DeskKit.CardBox z4 = DeskKit.Zone(b, DeskKit.XId, y, 1120f, 128f, 4,
                "lock in", "the die is cast at the press — the review reads the threats list first");
            float ay = z4.Cursor - 6f;
            List<AttentionItem> outstanding = SimEngine.PrerollItems(s);
            if (DeskStr(b, "mode") == "preroll")
            {
                int shown = 0;
                for (int i = 0; i < outstanding.Count && shown < 2; i++)
                {
                    AttentionItem it = outstanding[i];
                    b.L(string.Format("{0}{1} — {2}", it.Severity >= 3 ? "! " : "",
                        it.Desk, it.Label), z4.ContentX, ay, DeskKit.Detail,
                        DrawnUI.Coral, 700f);
                    ay += 28f;
                    shown += 1;
                }
                if (outstanding.Count > 2)
                    b.L(string.Format("+{0} more on the threats page", outstanding.Count - 2),
                        z4.ContentX, ay, 17f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 400f);
                DeskKit.Word(b, "roll anyway", z4.ContentX + 760f, z4.Cursor - 6f, () =>
                {
                    b.Desk.Remove("mode");
                    FireLock(b);
                }, DeskKit.Status, DrawnUI.Coral, 200f);
                DeskKit.Word(b, "go fix it", z4.ContentX + 760f, z4.Cursor + 34f, () =>
                {
                    b.Desk.Remove("mode");
                    if (outstanding.Count > 0) b.FocusDesk(outstanding[0].Desk);
                }, DeskKit.Status, DrawnUI.Ink, 200f);
            }
            else if (LockHook != null)
            {
                DeskKit.Word(b, "LOCK IN — roll the week", z4.ContentX, ay, () =>
                {
                    if (SimEngine.PrerollItems(b.State).Count == 0) FireLock(b);
                    else b.Desk["mode"] = "preroll";
                }, DeskKit.Row, DrawnUI.Ink, 480f);
                if (outstanding.Count > 0)
                    b.L(string.Format("{0} outstanding — the review will stop you once",
                        outstanding.Count), z4.ContentX + 520f, ay + 6f, DeskKit.Detail,
                        DrawnUI.Coral, 460f);
            }
            else
            {
                b.L("the journal rolls the week — close the binder (TAB) and press LOCK IN",
                    z4.ContentX, ay, DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f), 900f);
                if (outstanding.Count > 0)
                    b.L(string.Format("{0} outstanding items wait in the review",
                        outstanding.Count), z4.ContentX, ay + 28f, DeskKit.Detail,
                        DrawnUI.Coral, 700f);
            }

            DeskKit.Footer(b, "",
                "one move a week · the DM judges the plan into a DC and the die decides "
                + "· clarify answers append after \" — \"", "", DeskKit.FooterY, 856f);
        }

        static List<string[]> StagedRows(GameState s, out List<Color> cols)
        {
            var rows = new List<string[]>();
            cols = new List<Color>();
            if (SimPivot.Armed(s) != null)
            {
                rows.Add(new[] { "THE PIVOT — armed, fires at this LOCK IN" });
                cols.Add(DeskKit.Alert);
            }
            if (s.HasFlag("fundraising_open"))
            {
                rows.Add(new[] { "term sheets on the table — they expire" });
                cols.Add(DrawnUI.Coral);
            }
            for (int i = 0; i < s.History.Count; i++)
            {
                if (s.History[i].Week != s.Week) continue;
                rows.Add(new[] { s.History[i].Entry ?? "" });
                cols.Add(DrawnUI.WithAlpha(DrawnUI.Ink, 0.8f));
            }
            return rows;
        }

        /// <summary>The composer's paper (twin of _move_field): the words live
        /// in the static Draft so closing the binder never eats a move.</summary>
        static void MoveField(BinderScreen b, float x, float y)
        {
            var go = new GameObject("movefield", typeof(RectTransform));
            go.SetActive(false);
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(b.Content, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(1060f, 44f);
            rt.anchoredPosition = new Vector2(x, -y);
            var hit = go.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            var textRt = DrawnUI.FullRect(rt, "text");
            var text = textRt.gameObject.AddComponent<TextMeshProUGUI>();
            if (DrawnUI.Hand != null) text.font = DrawnUI.Hand;
            text.fontSize = 28f;
            text.color = DrawnUI.Ink;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.richText = false;
            var phRt = DrawnUI.FullRect(rt, "ph");
            var ph = phRt.gameObject.AddComponent<TextMeshProUGUI>();
            if (DrawnUI.Hand != null) ph.font = DrawnUI.Hand;
            ph.fontSize = 28f;
            ph.color = DrawnUI.WithAlpha(DrawnUI.Ink, 0.28f);
            ph.alignment = TextAlignmentOptions.MidlineLeft;
            ph.text = "what do we do this week?";
            var field = go.AddComponent<TMP_InputField>();
            field.textViewport = rt;
            field.textComponent = text;
            field.placeholder = ph;
            field.customCaretColor = true;
            field.caretColor = DrawnUI.Coral;
            field.text = Draft;
            field.onValueChanged.AddListener(t => { Draft = t; });
            go.SetActive(true);
            DeskKit.PenRule(b, y + 40f, x, 1060f, DrawnUI.WithAlpha(DrawnUI.Sage, 0.75f), 5);
        }

        static void FireLock(BinderScreen b)
        {
            if (LockHook != null) LockHook(Draft.Trim());
        }

        public static void Handle(BinderScreen b, string id)
        {
            switch (id)
            {
                case "lock":
                    if (SimEngine.PrerollItems(b.State).Count == 0) FireLock(b);
                    else b.Desk["mode"] = "preroll";
                    break;
                case "pre:roll":
                    b.Desk.Remove("mode");
                    FireLock(b);
                    break;
                case "pre:fix":
                    List<AttentionItem> items = SimEngine.PrerollItems(b.State);
                    b.Desk.Remove("mode");
                    if (items.Count > 0) b.FocusDesk(items[0].Desk);
                    break;
            }
        }
    }
}
