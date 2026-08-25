using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — the binder's `the street` tab. Spec: docs/design/03-rivals-macro.md section 11
    ///
    /// BinderScreen dispatches the tab body here and passes ITSELF, so this file
    /// draws through the binder's own helpers and never reaches into the sheet
    /// directly.
    ///
    /// THE PAGE, top to bottom: the weather, then the competition, then the money.
    ///   1 THE MACRO BANNER — the season in words beside the tab's name, and,
    ///     when a shock or its one-week warning is live, the authored line with
    ///     weeks left. Seasons must be readable BEFORE the money screens punish
    ///     you; the pre-announcement is the whole playable warning.
    ///   2 A BLOCK PER RIVAL — four lines through DeskKit.LogBlock: who they are,
    ///     how they stand (four word-reads, never a raw float), what they play,
    ///     and the last three things they did. Rivals become predictable through
    ///     their RECORD, not through hidden stats — pattern-reading is the skill
    ///     this page is teaching, and the word maps live once on SimStreet so the
    ///     two engines cannot drift apart.
    ///   3 THE MONEY — the investors, unchanged, compressing to a line each
    ///     exactly when the page needs the room (an hq third rival, a long
    ///     thesis, a live shock banner). Budgeted, then measured.
    ///
    /// The bar every surface ships at (00-spine section 11): readable first pass
    /// by a tired player; concepts named in real business terms with a teaching
    /// line where a number first appears; no dead ends and every state leavable;
    /// drawn in the game's hand, never a SaaS panel. The shared components live
    /// in Game/DeskKit.cs — use them, never fork them.
    ///
    /// TWIN LAW: this file and game/src/ui/desks/desk_street.gd draw the same
    /// rows at the same coordinates.
    /// </summary>
    public static class DeskStreet
    {
        /// <summary>What one investor entry costs the page: FullWk with a one-line
        /// thesis, TightWk as a name and archetype only, WrapWk once the thesis
        /// takes two lines. The budget picks the MODE up front so the section
        /// never mixes the two; the layout still MEASURES every wrap, and the
        /// room check reserves WrapWk — the worst case, not the common one — so
        /// the last entry can never hang off the bottom of the page. Anything
        /// that will not fit closes with "+N more".</summary>
        private const float FullWk = 88f;
        private const float TightWk = 38f;
        private const float WrapWk = 124f;

        /// <summary>
        /// Draw the macro banner and the four-line rival blocks.
        ///
        /// Wrapped text is MEASURED, never assumed one line — fixed steps stacked
        /// the street on itself the first week a thesis wrapped.
        /// </summary>
        public static void Draw(BinderScreen b)
        {
            GameState st = b.State;
            float y = DeskKit.Title(b, "the street");
            // the season rides the title line, in the desk's value column — one
            // glance tells you whether the weather is helping before you read a
            // single rival
            b.L(SimStreet.SeasonRead(st.MarketTrend), DeskKit.XValue, 18f, DeskKit.Row,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.75f), 720f);
            y = Banner(b, st, y);

            if (st.Rivals.Count == 0)
            {
                y = DeskKit.Empty(b, DeskKit.XId, y,
                    "nobody is competing with you this week.",
                    "that is rarer, and more temporary, than it feels.");
            }
            foreach (Rival r in st.Rivals)
            {
                var trail = new List<string>();
                for (int i = Mathf.Max(r.Log.Count - 3, 0); i < r.Log.Count; i++)
                {
                    trail.Add(r.Log[i]);
                }
                y = DeskKit.LogBlock(b, y, new DeskKit.LogRow
                {
                    Identity = string.Format("{0} — {1}", r.Name, SimEngine.Fuzz(r.Strength)),
                    Posture = SimStreet.PostureLine(r),
                    Plays = "plays: " + string.Join(", ", r.Tactics.ToArray()),
                    Trail = trail,
                });
            }

            b.L("the money:", DeskKit.XId, y + 4f, DeskKit.Row);
            y += 48f;
            Investors(b, st, y);
        }

        /// <summary>
        /// THE WEATHER STRIP. Line one is the season, drawn beside the title
        /// above; this draws line two — the authored shock line and how long it
        /// has left — and only while there is weather to report. Coral for a
        /// winter and its warning, sage for a boom: the page is colour-coded to
        /// which way the money is moving.
        /// </summary>
        private static float Banner(BinderScreen b, GameState st, float y)
        {
            string[] keys = { "funding_winter", "boom", "winter_watch", "boom_watch" };
            for (int i = 0; i < keys.Length; i++)
            {
                if (!SimEngine.HasStatus(st, keys[i])) { continue; }
                string text = SimStreet.BANNER[keys[i]];
                if (keys[i] == "funding_winter" || keys[i] == "boom")
                {
                    text += string.Format("  ·  {0} wks left", SimStreet.WeeksLeft(st, keys[i]));
                }
                bool warm = keys[i] == "boom" || keys[i] == "boom_watch";
                b.L(text, DeskKit.XId, y, DeskKit.Status,
                    warm ? DrawnUI.Sage : DrawnUI.Coral, 1100f);
                return y + 40f;
            }
            return y;
        }

        /// <summary>
        /// THE MONEY, still the founder's phone book. Full entries while the page
        /// has room; one line each when it does not — the street has to stay one
        /// page at every era, including the week an hq disruptor takes a third block.
        /// </summary>
        private static void Investors(BinderScreen b, GameState st, float y)
        {
            bool tight = y + st.Investors.Count * FullWk > DeskKit.PaneH;
            int shown = 0;
            foreach (Investor d in st.Investors)
            {
                if (shown >= DeskKit.ListCap || y + (tight ? TightWk : WrapWk) > DeskKit.PaneH)
                {
                    DeskKit.More(b, DeskKit.XId, y, st.Investors.Count - shown, "are in the book");
                    return;
                }
                b.L(string.Format("{0} ({1})", d.Name, d.Archetype), DeskKit.XId, y, 29f);
                if (tight)
                {
                    y += TightWk;
                }
                else
                {
                    string quote = string.Format("\"{0}\"  ·  {1}", d.Thesis, d.Trait);
                    TextMeshProUGUI lbl = b.L(quote, 30f, y + 38f, 25f,
                        DrawnUI.WithAlpha(DrawnUI.Ink, 0.65f), 1070f);
                    y += 44f + BinderScreen.Height(lbl) + 16f;
                }
                shown++;
            }
        }

        /// <summary>A press inside this desk. `id` is whatever Draw registered.
        /// The street is a page you READ — every control on it would be a lie,
        /// because none of this is yours to change from here.</summary>
        public static void Handle(BinderScreen b, string id)
        {
        }
    }
}
