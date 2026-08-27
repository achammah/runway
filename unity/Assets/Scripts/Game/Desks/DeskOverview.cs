using System.Collections.Generic;
using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// THE DASHBOARD QUARTET (twin of desk_overview.gd; DECISIONS #5, mockup
    /// 03): pressing an OPEN divider's header opens the group overview — a
    /// grid of cards, one per page, each card the page's hero and the button
    /// to the page. The grid wraps as groups grew past four.
    /// </summary>
    public static class DeskOverview
    {
        const float CardW = 548f, CardH = 208f, Gap = 24f;

        public static void Draw(BinderScreen b, int gi)
        {
            var sev = b.DeskSeverities();
            b.L(BinderScreen.GroupNames[gi].ToUpper() + " — the group at a glance",
                DeskKit.XId, 6f, DeskKit.TitleSize);
            b.L("a card is its page's hero — press it to open the page", DeskKit.XId, 52f,
                DeskKit.Law, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 800f);
            string[] desks = GroupDesksOf(gi);
            float y = 96f;
            for (int i = 0; i < desks.Length; i++)
            {
                string id = desks[i];
                float cx = DeskKit.XId + (i % 2) * (CardW + Gap);
                float cy = y + (i / 2) * (CardH + Gap);
                int s;
                sev.TryGetValue(id, out s);
                Card(b, cx, cy, id, SummaryFor(b, id), s);
            }
        }

        static string[] GroupDesksOf(int gi)
        {
            switch (gi)
            {
                case 0: return new[] { "offers", "customers", "in motion", "growth" };
                case 1: return new[] { "spend", "team", "recruitment", "bills",
                                       "the bank", "the works" };
                case 2: return new[] { "what we make", "cap table", "the raise",
                                       "the street", "threats", "pivot" };
                default: return new[] { "this week", "history", "events" };
            }
        }

        /// One quartet card — and (DAG3) the S5 delta line when the hero moved
        /// since the last open, plus the S2 ask line naming WHAT the red
        /// wants, not just that it wants.
        static void Card(BinderScreen b, float x, float y, string id, string[] s,
                         int severity)
        {
            DeskKit.CardBox f = DeskKit.CardFrame(b, x, y, CardW, CardH, id);
            if (severity > 0) DeskKit.SevDot(b, x + CardW - 78f, y + 16f, severity);
            string big = s[0];
            b.L(big, f.ContentX, f.ContentY + 6f, DeskKit.HeroBig,
                severity >= 2 ? DeskKit.Alert : DrawnUI.Ink, CardW - DeskKit.CardPad * 2f);
            b.L(s[1], f.ContentX, f.ContentY + 78f, DeskKit.Detail,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.65f), CardW - DeskKit.CardPad * 2f);
            // the delta line: the seen store remembers last open's hero verbatim
            string prev = b.SeenPrev(id, "quartet");
            bool moved = b.Seen(id, "quartet", big);
            if (moved && prev != "" && severity <= 0)
                DeskKit.FitLine(b, "was " + prev + " when you last looked", f.ContentX,
                    y + CardH - 44f, DeskKit.Law, DrawnUI.Blue,
                    CardW - DeskKit.CardPad * 2f);
            if (severity > 0)
            {
                List<string> asks = DeskKit.GetAsks(b.State, id);
                string askLine = asks.Count > 0
                    ? "!  " + string.Join(" · ", asks)
                    : "needs you — the red climbed here from the page";
                DeskKit.FitLine(b, askLine, f.ContentX, y + CardH - 44f, DeskKit.Law,
                    DeskKit.Alert, CardW - DeskKit.CardPad * 2f);
            }
            string did = id;
            var hit = DeskKit.Word(b, "", x, y, () => b.OpenPage(did), DeskKit.Detail,
                                   DrawnUI.Ink, CardW);
            hit.GetComponent<RectTransform>().sizeDelta = new Vector2(CardW, CardH);
        }

        /// The hero each stub declares, routed by desk id.
        public static string[] SummaryFor(BinderScreen b, string id)
        {
            GameState s = b.State;
            switch (id)
            {
                case "offers": return DeskOffers.HeroSummary(s);
                case "customers": return DeskCustomersPage.HeroSummary(s);
                case "in motion": return DeskInMotion.HeroSummary(s);
                case "growth": return DeskGrowth.HeroSummary(s);
                case "spend": return DeskSpend.HeroSummary(s);
                case "team": return DeskTeam.HeroSummary(s);
                case "recruitment": return DeskRecruit.HeroSummary(s);
                case "bills": return DeskBills.HeroSummary(s);
                case "the bank": return DeskBankPage.HeroSummary(s);
                case "the works": return DeskWorks.HeroSummary(s);
                case "what we make": return DeskMake.HeroSummary(s);
                case "cap table": return DeskCapPage.HeroSummary(s);
                case "the raise": return DeskRaise.HeroSummary(s);
                case "the street": return DeskStreetPage.HeroSummary(s);
                case "threats": return DeskThreatsPage.HeroSummary(s);
                case "pivot": return DeskPivot.HeroSummary(s);
                case "this week": return DeskThisWeek.HeroSummary(s);
                case "history": return DeskHistory.HeroSummary(s);
                case "events": return DeskEvents.HeroSummary(s);
                case "the offer": return DeskOffer.HeroSummary(s);
            }
            return new[] { "—", "" };
        }

        public static void Handle(BinderScreen b, string id) { }
    }
}
