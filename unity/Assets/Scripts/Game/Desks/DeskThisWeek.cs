using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
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
    /// HERO week + era + the situation (pressable — the receipt says the
    /// terms behind the verdict) · 1 THE CARD (the event, art slot; without a
    /// seeded card, THE OUTCOME VIEW: last week's consequence lines with desk
    /// jumps) · 2 YOUR MOVE (the composer + THE WEEK'S CHIPS — every desk's
    /// suggestion as a pressable chip; prefill APPENDS to the draft, jump
    /// walks there) · 3 ARMED THIS WEEK (the staged receipt list) · 4 LOCK IN
    /// (the die, behind THE PRE-ROLL REVIEW; the press lives in the DO lane
    /// wearing the garage's outstanding-count badge).
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

        /// The chips sweep every desk on the rail — the binder's own GROUPS,
        /// never a local list.
        static readonly string[] AllDesks = BuildAllDesks();

        static string[] BuildAllDesks()
        {
            var outp = new List<string>();
            foreach (string[] g in BinderScreen.GroupDesks) outp.AddRange(g);
            return outp.ToArray();
        }

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
            string band = SimEngine.HealthBand(s);
            string heroLine = string.Format("{0} · {1}", s.EraDisplayName(),
                band.ToLowerInvariant());
            float y = DeskKit.HeroBand(b, string.Format("week {0}", s.Week),
                heroLine, DrawnUI.Ink);
            // S4 — the hero is pressable: the terms behind the verdict word
            int net = PnlNet(s);
            DeskKit.PressReceipt(b, new Rect(DeskKit.XId, 6f, 560f, 100f),
                "the week's terms", new List<DeskKit.TicketLine>
                {
                    new DeskKit.TicketLine { Label = "cash",
                        Value = "$" + GameUi.Money(s.Cash) },
                    new DeskKit.TicketLine { Label = "the week's net",
                        Value = (net >= 0 ? "+$" : "−$") + GameUi.Money(Math.Abs(net)),
                        Col = net >= 0 ? DrawnUI.Sage : DrawnUI.Coral },
                    new DeskKit.TicketLine { Label = "runway = cash ÷ net burn",
                        Value = SimEngine.RunwayWeeks(s) + " wk" },
                    new DeskKit.TicketLine { Label = "the verdict",
                        Value = band.ToLowerInvariant() },
                });
            // S5 — the pen circles the situation line when the band moved
            if (b.Seen("this week", "band", band))
                DeskKit.PenCircle(b, new Rect(DeskKit.XId, 72f,
                    Mathf.Min(DrawnUI.MeasureWidth(heroLine, DeskKit.Row), 900f), 34f));

            // 1 · THE CARD — the event and its bite, art slot left; without a
            // seeded card, THE OUTCOME VIEW: last week's consequence lines,
            // each naming the desk where that number is edited.
            DeskKit.CardBox z1 = DeskKit.Zone(b, DeskKit.XId, y, 1120f, 120f, 1,
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
                    tx, z1.ContentY - 16f, DeskKit.Row,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.75f), 1000f);
                List<string[]> cons = Consequences(s);
                if (cons.Count == 0)
                    b.L("this week's card opens with the journal", tx, z1.ContentY + 16f,
                        DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 900f);
                else
                {
                    float cy = z1.ContentY + 12f;
                    for (int ci = 0; ci < Math.Min(cons.Count, 2); ci++)
                    {
                        DeskKit.FitLine(b, cons[ci][0], tx, cy, DeskKit.Detail,
                            DrawnUI.WithAlpha(DrawnUI.Ink, 0.75f),
                            850f - (tx - z1.ContentX));
                        string cdesk = cons[ci][1];
                        if (cdesk != "")
                            DeskKit.Word(b, cdesk + " ->", z1.ContentX + 890f, cy - 6f,
                                () => { b.FocusDesk(cdesk, "", "this week"); },
                                DeskKit.Detail, DrawnUI.Coral, 200f);
                        cy += 28f;
                    }
                }
            }
            y = z1.Bottom + 12f;

            // 2 · YOUR MOVE — the composer, and under its rule THE WEEK'S
            // CHIPS: what the desks suggest, adopt-only.
            DeskKit.CardBox z2 = DeskKit.Zone(b, DeskKit.XId, y, 1120f, 168f, 2,
                "your move", "plain words — the world asks a question when a number is missing");
            MoveField(b, z2.ContentX, z2.Cursor - 6f);
            ChipStrip(b, s, z2.ContentX, z2.Cursor + 46f);
            y = z2.Bottom + 12f;

            // 3 · ARMED THIS WEEK — everything staged since the last roll
            List<string[]> staged = StagedRows(s, out List<Color> stagedCols);
            float z3H = 66f + Math.Max(Math.Min(staged.Count, 3) * 30f, 28f) + 10f;
            DeskKit.CardBox z3 = DeskKit.Zone(b, DeskKit.XId, y, 1120f, z3H, 3,
                "armed this week", "the receipt list — what the week will carry into the roll");
            float ry = z3.Cursor - 8f;
            if (staged.Count == 0)
                b.L("nothing staged since the last roll — steppers, signatures and arranges land here",
                    z3.ContentX, ry, DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 1040f);
            for (int i = 0; i < Math.Min(staged.Count, 3); i++)
            {
                b.L("· " + staged[i][0], z3.ContentX, ry, DeskKit.Detail, stagedCols[i], 1000f);
                ry += 30f;
            }
            if (staged.Count > 3)
                b.L(string.Format("+{0} more in the log", staged.Count - 3),
                    z3.ContentX, ry, 17f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 400f);
            y = z3.Bottom + 12f;

            // 4 · LOCK IN — the die, behind THE PRE-ROLL REVIEW. The zone
            // explains and holds the intercept; the PRESS lives in the DO lane.
            bool inPreroll = DeskStr(b, "mode") == "preroll";
            DeskKit.CardBox z4 = DeskKit.Zone(b, DeskKit.XId, y, 1120f,
                inPreroll ? 128f : 112f, 4, "lock in",
                "the die is cast at the press — the review reads the threats list first");
            float ay = z4.Cursor - 6f;
            List<AttentionItem> outstanding = SimEngine.PrerollItems(s);
            if (inPreroll)
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
                    if (outstanding.Count > 0) b.JumpToAsk(outstanding[0], "this week");
                }, DeskKit.Status, DrawnUI.Ink, 200f);
            }
            else if (LockHook != null)
            {
                if (outstanding.Count == 0)
                    b.L("nothing outstanding — the week is ready to roll",
                        z4.ContentX, ay, DeskKit.Detail,
                        DrawnUI.WithAlpha(DrawnUI.Ink, 0.65f), 900f);
                else
                    b.L(string.Format("{0} outstanding — the review will stop you once",
                        outstanding.Count), z4.ContentX, ay, DeskKit.Detail,
                        DrawnUI.Coral, 700f);
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

            // S3 — THE DO LANE: the die and the pen, one slot.
            var actions = new List<DeskKit.DoAction>();
            if (LockHook != null && !inPreroll)
                actions.Add(new DeskKit.DoAction
                {
                    Label = "LOCK IN — roll the week",
                    Cb = () =>
                    {
                        if (SimEngine.PrerollItems(b.State).Count == 0) FireLock(b);
                        else b.Desk["mode"] = "preroll";
                    },
                    Tier = "",
                });
            actions.Add(new DeskKit.DoAction
            {
                Label = "write the move",
                Cb = () => { b.Desk["focus_move"] = true; },
                Tier = "",
            });
            DeskKit.DoLane(b, actions);
            // the outstanding-count badge rides the LOCK IN mirror (garage idiom)
            int nAtt = SimEngine.AttentionItems(s).Count;
            if (LockHook != null && !inPreroll && nAtt > 0 && b.HasControl("do_0"))
            {
                Rect r0 = b.ControlRect("do_0");
                DeskKit.CountBadge(b.Content, r0.x + r0.width - 12f, r0.y - 14f, nAtt);
            }

            DeskKit.Footer(b, "",
                "one move a week · the DM judges the plan into a DC and the die decides "
                + "· clarify answers append after \" — \"", "", DeskKit.FooterY, 856f);
        }

        static int PnlNet(GameState s)
        {
            object box = s.GetMeta("pnl", null);
            var dict = box as IDictionary<string, object>;
            if (dict != null)
            {
                object v;
                if (dict.TryGetValue("net", out v) && v != null)
                    try { return Convert.ToInt32(v); } catch (Exception) { }
            }
            var jo = box as JObject;
            if (jo != null && jo["net"] != null)
                try { return (int)jo["net"]; } catch (Exception) { }
            return 0;
        }

        /// THE OUTCOME VIEW's consequence lines (twin of _consequences): last
        /// week's booked effects as [text, desk] pairs — the desk read from
        /// the why-text when it names one, else from the op's own ledger.
        static List<string[]> Consequences(GameState s)
        {
            var outp = new List<string[]>();
            object dmO;
            var dm = s.LastOutcome != null
                && s.LastOutcome.TryGetValue("dm", out dmO) ? dmO as JObject : null;
            var effects = dm != null ? dm["effects"] as JArray : null;
            if (effects == null) return outp;
            foreach (JToken t in effects)
            {
                var d = t as JObject;
                if (d == null) continue;
                string op = ContentDb.Str(d, "op");
                string why = ContentDb.Str(d, "why");
                if (why.Length == 0 || op == "set_flag") continue;
                int v = ContentDb.Int(d, "v", 0);
                string noun = "";
                if (op == "product_delta") noun = "product";
                else if (op == "traction_delta") noun = "customers";
                else if (op == "morale_delta") noun = "morale";
                else if (op == "hype_delta") noun = "hype";
                string amt = (v >= 0 ? "+" : "−")
                    + (op == "cash_delta"
                        ? "$" + GameUi.Money(Mathf.Abs(v)) : Mathf.Abs(v).ToString());
                string text = noun != ""
                    ? string.Format("{0} {1} — {2}", amt, noun, why)
                    : string.Format("{0} — {1}", amt, why);
                outp.Add(new[] { text, ConsequenceDesk(op, why) });
            }
            return outp;
        }

        static string ConsequenceDesk(string op, string why)
        {
            string low = why.ToLowerInvariant();
            foreach (string d in AllDesks)
                if (d != "this week" && low.Contains(d)) return d;
            switch (op)
            {
                case "cash_delta": return "the bank";
                case "traction_delta": return "customers";
                case "product_delta": return "what we make";
                case "morale_delta": return "team";
                case "hype_delta": return "the street";
            }
            return "";
        }

        /// THE WEEK'S CHIPS (13-binder § this week, twin of _chip_strip):
        /// every desk-suggested action in one pressable strip. Prefill chips
        /// APPEND to the draft — never overwrite; jump chips walk to the
        /// suggesting desk (payload = the control) and leave a back pill.
        static void ChipStrip(BinderScreen b, GameState s, float x, float y)
        {
            List<Dictionary<string, object>> rows =
                DeskKit.CollectSuggestions(s, AllDesks);
            if (rows.Count == 0)
            {
                b.L("no desk is suggesting a move yet — suggestions land here as chips",
                    x, y + 6f, 17f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f), 1000f);
                return;
            }
            b.L("suggested:", x, y + 6f, 17f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 110f);
            float cx = x + 104f;
            float limit = DeskKit.XId + 1120f - 18f;
            for (int i = 0; i < rows.Count; i++)
            {
                Dictionary<string, object> rd = rows[i];
                object kv;
                bool jump = rd.TryGetValue("kind", out kv) && kv != null
                    && Convert.ToString(kv) == "jump";
                object lv;
                string raw = rd.TryGetValue("label", out lv) && lv != null
                    ? Convert.ToString(lv) : "";
                // generated labels come pre-fit (S6)
                string cap = DeskKit.FitText(b, raw, 240f, 19f) + (jump ? " ->" : "");
                // ChipToken's own measure stand-in: len*10+8 text + 26 box + 10 gap
                float w = cap.Length * 10f + 8f + 26f + 10f;
                if (cx + w > limit)
                {
                    b.L(string.Format("+{0} more", rows.Count - i), cx, y + 6f, 17f,
                        DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 120f);
                    break;
                }
                object dv;
                string deskId = rd.TryGetValue("desk", out dv) && dv != null
                    ? Convert.ToString(dv) : "";
                // payload shapes in the wild: prefill = the draft text
                // (string); jump = a control id string OR {desk, control}
                // (the shipped desks' form) — accept both, never crash.
                object pv;
                rd.TryGetValue("payload", out pv);
                string jumpDesk = deskId;
                string jumpControl = "";
                string prefillText = pv is string ? (string)pv
                    : pv != null && !(pv is IDictionary<string, object>)
                        ? Convert.ToString(pv) : "";
                var pd = pv as IDictionary<string, object>;
                if (pd != null)
                {
                    object jd;
                    if (pd.TryGetValue("desk", out jd) && jd != null)
                        jumpDesk = Convert.ToString(jd);
                    object jc;
                    if (pd.TryGetValue("control", out jc) && jc != null)
                        jumpControl = Convert.ToString(jc);
                }
                else if (pv is string) jumpControl = (string)pv;
                Action press = jump
                    ? (Action)(() => { b.FocusDesk(jumpDesk, jumpControl, "this week"); })
                    : () => { AdoptPrefill(prefillText); };
                cx = DeskKit.ChipToken(b, cx, y, new DeskKit.ChipCfg
                {
                    Text = cap, Kind = "person", OnPress = press,
                });
            }
        }

        /// Prefill = APPEND. A chip never eats a half-written move: the
        /// suggestion joins after " — " — the composer's own append grammar
        /// (a literal newline renders as nothing in the single-line field).
        static void AdoptPrefill(string text)
        {
            if (Draft.Trim().Length == 0) Draft = text;
            else Draft += " — " + text;
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
        /// in the static Draft so closing the binder never eats a move.
        /// Registers "move_field" (S2b); the DO lane's [write the move] sets
        /// Desk["focus_move"] and the next draw hands it the caret.</summary>
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
            b.MarkControl("move_field", new Rect(x, y, 1060f, 44f));
            object fm;
            if (b.Desk.TryGetValue("focus_move", out fm) && fm is bool && (bool)fm)
            {
                b.Desk.Remove("focus_move");
                field.ActivateInputField();
                field.caretPosition = Draft.Length;
                b.Spotlight(new Rect(x, y, 1060f, 44f));
            }
            DeskKit.PenRule(b, y + 40f, x, 1060f, DrawnUI.WithAlpha(DrawnUI.Sage, 0.75f), 5);
        }

        static void FireLock(BinderScreen b)
        {
            if (LockHook != null) LockHook(Draft.Trim());
        }

        // ── S8/S10 — the rail speaks for the desk (probe-guarded, never must) ──

        /// The landing tab never sleeps — the week is always being played.
        public static bool IsDormant(GameState s)
        {
            return false;
        }

        /// The tab's four characters: how much the week already carries.
        public static string MicroStatus(GameState s)
        {
            int n = StagedRows(s, out _).Count;
            return n > 0 ? n + " armed" : "";
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
