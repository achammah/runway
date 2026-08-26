using System;
using System.Collections.Generic;
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
    /// label, the desk word as a pressable jump (FocusDesk). NO FOLDING: a
    /// long list IS the message. THE SPILLOVER (clocks, conditions, standing
    /// costs) stays, compressed under the list. One list behind every bang.
    /// </summary>
    public static class DeskThreatsPage
    {
        public const string Question = "what could kill us?";

        public static string[] HeroSummary(GameState s)
        {
            List<AttentionItem> rows = SimEngine.AttentionItems(s);
            if (rows.Count == 0)
                return new[] { "nothing is shouting", "that never lasts" };
            return new[] { string.Format("{0} live", rows.Count),
                string.Format("{0} — {1}", rows[0].Label, rows[0].Desk) };
        }

        public static void Draw(BinderScreen b)
        {
            GameState s = b.State;
            List<AttentionItem> rows = SimEngine.AttentionItems(s);
            float y;

            // HERO — the loudest item BIG, its desk named, the count of the rest
            if (rows.Count == 0)
                y = DeskKit.HeroBand(b, "nothing is shouting",
                    "that never lasts — the clocks below keep ticking", DrawnUI.Ink);
            else
                y = DeskKit.HeroBand(b,
                    string.Format("{0} — {1}", rows[0].Label, rows[0].Desk),
                    rows.Count > 1
                        ? string.Format("{0} more on the list, loudest first", rows.Count - 1)
                        : "the only thing shouting this week",
                    rows[0].Severity >= 3 ? DeskKit.Alert : DrawnUI.Ink);

            // THE LIST — every row, no folding: a long list IS the message
            for (int i = 0; i < rows.Count; i++)
            {
                AttentionItem it = rows[i];
                DeskKit.SevDot(b, DeskKit.XId, y + 6f, it.Severity);
                b.L(it.Label, DeskKit.XId + 36f, y, 28f,
                    it.Severity >= 3 ? DrawnUI.Coral : DrawnUI.WithAlpha(DrawnUI.Ink, 0.85f),
                    800f);
                string dsk = it.Desk;
                DeskKit.Word(b, dsk + " ->", DeskKit.XId + 900f, y - 4f,
                    () => b.FocusDesk(dsk), DeskKit.Status, DrawnUI.Coral, 220f);
                y += 46f;
            }
            y += 10f;

            // THE SPILLOVER — the clocks, the weather, the standing costs
            if (s.Clocks.Count == 0 && s.Statuses.Count == 0
                && s.Commitments.Count == 0 && rows.Count == 0)
            {
                b.L("nothing ticking. that never lasts.", DeskKit.XId, y, 30f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f));
                y += 44f;
            }
            if (s.Clocks.Count > 0 || s.Statuses.Count > 0 || s.Commitments.Count > 0)
            {
                y = DeskKit.PenRule(b, y + 4f);
                for (int i = 0; i < s.Clocks.Count && y <= 780f; i++)
                {
                    Clock cd = s.Clocks[i];
                    DrawnChart.Mount(b.Content, "clock",
                        DrawnChart.Clock(26, DrawnUI.Coral, DrawnUI.Ink),
                        DeskKit.XId, y + 2f, 26f, 26f);
                    b.L(string.Format("in {0} wks: {1}", cd.WeeksLeft, cd.Consequence),
                        DeskKit.XId + 36f, y, DeskKit.Detail, DrawnUI.Coral, 1060f);
                    y += 36f;
                }
                for (int i = 0; i < s.Statuses.Count && y <= 780f; i++)
                {
                    Status sd = s.Statuses[i];
                    StatusDef def = SimEngine.StatusEffect(sd.Name);
                    bool buff = def != null && def.Kind == "buff";
                    b.L(string.Format("{0} {1} — {2} wks left",
                        buff ? "helping:" : "hurting:",
                        (sd.Name ?? "").Replace("_", " "), sd.WeeksLeft),
                        DeskKit.XId + 36f, y, DeskKit.Detail,
                        buff ? DrawnUI.Sage : DrawnUI.Coral, 1060f);
                    y += 36f;
                }
                for (int i = 0; i < s.Commitments.Count && y <= 780f; i++)
                {
                    Commitment cm = s.Commitments[i];
                    b.L(string.Format("standing: {0} — ${1}/wk for {2} more wks",
                        cm.Name, Math.Abs(cm.CashWk), cm.WeeksLeft),
                        DeskKit.XId + 36f, y, DeskKit.Detail, DrawnUI.Blue, 1060f);
                    y += 36f;
                }
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
                b.FocusDesk(id.Substring(3));
        }
    }
}
