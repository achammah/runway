using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — THE COMPANY · "threats", the command center (twin of
    /// desk_threats_page.gd). W2 lane: L-COMPANY.
    /// THE QUESTION: "what could kill us?"
    ///
    /// HERO: the loudest attention item BIG with its desk named + the count
    /// of the rest. THE LIST: every attention row — severity dot, plain
    /// label, an age chip once a row has stood ≥2 weeks, the desk word as a
    /// pressable jump (JumpToAsk: rows carrying a `control` land spotlit on
    /// the switch). Ordered severity first, then age — the longest-ignored
    /// rises. NO FOLDING: a long list IS the message. THE SPILLOVER (clocks,
    /// conditions, standing costs) stays, compressed under the list. DO LANE:
    /// [fix first — top item]. One list behind every bang.
    /// </summary>
    public static class DeskThreatsPage
    {
        public const string Question = "what could kill us?";

        public static string[] HeroSummary(GameState s)
        {
            List<AttentionItem> rows = Ordered(s);
            if (rows.Count == 0)
                return new[] { "nothing is shouting", "that never lasts" };
            return new[] { string.Format("{0} live", rows.Count),
                string.Format("{0} — {1}", rows[0].Label, DeskWord(rows[0].Desk)) };
        }

        /// The engine's attention registry still speaks the old tab names
        /// ("crew", "pricing", "the ledger") — the page shows the desk the
        /// binder actually opens (FocusDesk translates the jump itself).
        static readonly Dictionary<string, string> DeskWordMap =
            new Dictionary<string, string>
            {
                { "vitals", "this week" }, { "the ledger", "spend" },
                { "pricing", "offers" }, { "product", "what we make" },
                { "crew", "team" }, { "pipeline", "in motion" },
                { "factory", "the works" }, { "catalog", "offers" },
                { "bank", "the bank" }, { "cap", "cap table" },
                { "street", "the street" }, { "ledger", "spend" },
            };

        static string DeskWord(string d)
        {
            string outp;
            return DeskWordMap.TryGetValue(d ?? "", out outp) ? outp : d;
        }

        /// THE COMMAND CENTER'S OWN ORDER: severity first, then AGE — between
        /// two rows shouting equally loud, the one ignored longest rises. The
        /// engine's order is kept as the stable tiebreak.
        static List<AttentionItem> Ordered(GameState s)
        {
            List<AttentionItem> rows = SimEngine.AttentionItems(s);
            var idx = new Dictionary<AttentionItem, int>();
            for (int i = 0; i < rows.Count; i++) idx[rows[i]] = i;
            rows.Sort((a, c) =>
            {
                if (a.Severity != c.Severity) return c.Severity.CompareTo(a.Severity);
                if (a.SinceWk != c.SinceWk) return a.SinceWk.CompareTo(c.SinceWk);
                return idx[a].CompareTo(idx[c]);
            });
            return rows;
        }

        public static void Draw(BinderScreen b)
        {
            GameState s = b.State;
            List<AttentionItem> rows = Ordered(s);

            // S1 — a fully quiet company: teach what the page is FOR
            if (rows.Count == 0 && s.Clocks.Count == 0 && s.Statuses.Count == 0
                && s.Commitments.Count == 0)
            {
                DeskKit.ZeroState(b, new DeskKit.ZeroStateCfg
                {
                    WillShow = "every red mark in the game — one list, loudest first",
                    WouldLine = "each row WOULD name its ask, wear its age in weeks, and walk you to the switch that fixes it",
                    ActionLabel = "back to this week",
                    ActionCb = () => b.FocusDesk("this week"),
                    WakesHint = "wakes the first time anything goes red — the tab wears the loudest mark",
                });
                return;
            }

            float y = 6f;

            // HERO — the loudest item BIG, its desk named, the count of the rest
            if (rows.Count == 0)
                y = DeskKit.HeroBand(b, "nothing is shouting",
                    "that never lasts — the clocks below keep ticking", DrawnUI.Ink);
            else
                y = DeskKit.HeroBand(b,
                    string.Format("{0} — {1}", rows[0].Label, DeskWord(rows[0].Desk)),
                    rows.Count > 1
                        ? string.Format("{0} more on the list, loudest first", rows.Count - 1)
                        : "the only thing shouting this week",
                    rows[0].Severity >= 3 ? DeskKit.Alert : DrawnUI.Ink);

            // THE LIST — every row, no folding: a long list IS the message
            for (int i = 0; i < rows.Count; i++)
            {
                AttentionItem it = rows[i];
                int age = s.Week - it.SinceWk;
                DeskKit.SevDot(b, DeskKit.XId, y + 6f, it.Severity);
                DeskKit.FitLine(b, it.Label, DeskKit.XId + 36f, y, 28f,
                    it.Severity >= 3 ? DrawnUI.Coral : DrawnUI.WithAlpha(DrawnUI.Ink, 0.85f),
                    age >= 2 ? 750f : 800f);
                // S5 — the age chip: a row that has stood ≥2 weeks says so
                if (age >= 2)
                    DeskKit.ClockChip(b, DeskKit.XId + 800f, y + 2f,
                        string.Format("{0} wks", age));
                // S2b — the row itself knows its switch: JumpToAsk reads the
                // row's desk AND its control key, so a filled control lands
                // spotlit; the source leaves the free back pill
                AttentionItem itNow = it;
                DeskKit.Word(b, DeskWord(it.Desk) + " ->", DeskKit.XId + 900f, y - 4f,
                    () => b.JumpToAsk(itNow, "threats"), DeskKit.Status, DrawnUI.Coral, 220f);
                y += 46f;
            }
            y += 10f;

            // THE SPILLOVER — the clocks, the weather, the standing costs
            if (s.Clocks.Count > 0 || s.Statuses.Count > 0 || s.Commitments.Count > 0)
            {
                y = DeskKit.PenRule(b, y + 4f);
                for (int i = 0; i < s.Clocks.Count && y <= 740f; i++)
                {
                    Clock cd = s.Clocks[i];
                    DrawnChart.Mount(b.Content, "clock",
                        DrawnChart.Clock(26, DrawnUI.Coral, DrawnUI.Ink),
                        DeskKit.XId, y + 2f, 26f, 26f);
                    DeskKit.FitLine(b, string.Format("in {0} wks: {1}",
                        cd.WeeksLeft, cd.Consequence),
                        DeskKit.XId + 36f, y, DeskKit.Detail, DrawnUI.Coral, 1060f);
                    y += 36f;
                }
                for (int i = 0; i < s.Statuses.Count && y <= 740f; i++)
                {
                    Status sd = s.Statuses[i];
                    StatusDef def = SimEngine.StatusEffect(sd.Name);
                    bool buff = def != null && def.Kind == "buff";
                    DeskKit.FitLine(b, string.Format("{0} {1} — {2} wks left",
                        buff ? "helping:" : "hurting:",
                        (sd.Name ?? "").Replace("_", " "), sd.WeeksLeft),
                        DeskKit.XId + 36f, y, DeskKit.Detail,
                        buff ? DrawnUI.Sage : DrawnUI.Coral, 1060f);
                    y += 36f;
                }
                for (int i = 0; i < s.Commitments.Count && y <= 740f; i++)
                {
                    Commitment cm = s.Commitments[i];
                    // Law 2 — the amount rides its own right-aligned column
                    DeskKit.FitLine(b, string.Format("standing: {0} — {1} more wks",
                        cm.Name, cm.WeeksLeft),
                        DeskKit.XId + 36f, y, DeskKit.Detail, DrawnUI.Blue, 800f);
                    TextMeshProUGUI cv = DeskKit.FitLine(b,
                        "$" + GameUi.Money(Math.Abs(cm.CashWk)) + "/wk",
                        DeskKit.XId + 860f, y, DeskKit.Detail, DrawnUI.Blue, 200f);
                    cv.alignment = TextAlignmentOptions.TopRight;
                    y += 36f;
                }
            }

            // S3 — the one thing to do here: walk to the loudest switch
            if (rows.Count > 0)
            {
                AttentionItem top = rows[0];
                DeskKit.DoLane(b, new List<DeskKit.DoAction>
                {
                    new DeskKit.DoAction
                    {
                        Label = "fix first — " + top.Label,
                        Cb = () => b.JumpToAsk(top, "threats"),
                        Tier = "",
                    },
                });
            }

            DeskKit.Footer(b,
                string.Format("{0} rows live · the loudest is what the tab wears", rows.Count),
                "this same list is what THE PRE-ROLL REVIEW reads before any dice — "
                + "fix them, or roll and live with it · every row names the desk that owns the fix",
                "", 820f, 852f);
        }

        public static void Handle(BinderScreen b, string id)
        {
            if (!string.IsNullOrEmpty(id) && id.StartsWith("go:"))
                b.FocusDesk(id.Substring(3), "", "threats");
        }

        // ── the desk conventions (S8) — the rail reads these ─────────────────

        public static bool IsDormant(GameState _s) { return false; }

        /// The rail's right-aligned word: how many things are shouting.
        public static string MicroStatus(GameState s)
        {
            int n = SimEngine.AttentionItems(s).Count;
            return n > 0 ? string.Format("{0} live", n) : "";
        }
    }
}
