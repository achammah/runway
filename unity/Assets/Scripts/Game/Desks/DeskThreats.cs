using System.Collections.Generic;
using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — the binder's `threats` tab. Spec: docs/design/11-binder-rework.md
    /// section threats, and the attention registry in 00-spine sections 4 and 11.
    ///
    /// BinderScreen dispatches the tab body here and passes ITSELF, so this file
    /// draws through the binder's own helpers and never reaches into the sheet
    /// directly.
    ///
    /// THE QUESTION THIS DESK ANSWERS: "what could kill us?" — the whole
    /// company's shouting, in one place, loudest first.
    ///   1 THE SPILLOVER — every attention item at warn or above, ranked. This is
    ///     the same list the tab bangs, the garage badge and the pre-roll review
    ///     read, so a desk that is shouting can never be shouting only somewhere
    ///     the player is not looking. Every row names the desk that owns the fix.
    ///   2 THE CLOCKS — what fires, and in how many weeks, each led by a drawn face.
    ///   3 THE CONDITIONS — what helps and what hurts, with the weeks left.
    ///   4 THE STANDING COSTS — what bills every week until it runs out.
    ///
    /// The bar every surface ships at (00-spine section 11): readable first pass
    /// by a tired player; concepts named in real business terms with a teaching
    /// line where a number first appears; no dead ends and every state leavable;
    /// drawn in the game's hand, never a SaaS panel. The shared components live
    /// in Game/DeskKit.cs — use them, never fork them.
    ///
    /// TWIN LAW: this file and game/src/ui/desks/desk_threats.gd draw the same
    /// rows at the same coordinates.
    /// </summary>
    public static class DeskThreats
    {
        /// The drawn clock's footprint — the space the ⏰ had at 30px type.
        public const int ClockSide = 30;
        /// The spillover cap: twelve rows, then the truth about the rest.
        public const int SpillCap = 12;

        /// <summary>Draw the overflow page.</summary>
        public static void Draw(BinderScreen b)
        {
            GameState st = b.State;
            b.L("threats & promises", 10f, 6f, 40f);
            float y = 80f;
            // WHAT NEEDS A HAND, in one place (00-spine sections 4 and 11):
            // every attention item at warn or above, loudest first. This is the
            // same list the tab bangs, the garage badge and the pre-roll review
            // read — so a desk that is shouting can never be shouting only
            // somewhere the player is not looking.
            List<AttentionItem> wants = SimEngine.PrerollItems(st);
            if (wants.Count > 0)
            {
                int shown = 0;
                foreach (AttentionItem it in wants)
                {
                    if (shown >= SpillCap)
                    {
                        b.L(string.Format("+{0} more — the desks have the details",
                            wants.Count - shown), 10f, y, 26f,
                            DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f));
                        y += 44f;
                        break;
                    }
                    b.L(string.Format("! {0}  ·  {1}", it.Label, it.Desk), 10f, y, 28f,
                        it.Severity >= 3 ? DrawnUI.Coral
                                         : DrawnUI.WithAlpha(DrawnUI.Ink, 0.85f));
                    y += 44f;
                    shown += 1;
                }
                y += 12f;
            }
            if (st.Clocks.Count == 0 && st.Statuses.Count == 0 && st.Commitments.Count == 0)
                b.L("nothing ticking. that never lasts.", 10f, y, 30f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f));
            for (int i = 0; i < st.Clocks.Count; i++)
            {
                // THE CLOCK IS DRAWN, NOT TYPED. The original heads this line with
                // ⏰, a glyph the hand font has never carried; a drawn face is both
                // the truer style and the one thing that cannot come out a hollow box.
                DrawnChart.Mount(b.Content, "clock",
                    DrawnChart.Clock(ClockSide, DrawnUI.Coral, DrawnUI.Ink),
                    10f, y + 3f, ClockSide, ClockSide);
                b.L(string.Format("in {0} wks: {1}", st.Clocks[i].WeeksLeft,
                    st.Clocks[i].Consequence), 10f + ClockSide + 8f, y, 30f, DrawnUI.Coral);
                y += 52f;
            }
            for (int i = 0; i < st.Statuses.Count; i++)
            {
                Status s = st.Statuses[i];
                StatusDef def = SimEngine.StatusEffect(s.Name);
                bool buff = def != null && def.Kind == "buff";
                // THE WORD IS THE MARK. ▲/▼/↻ are all absent from the hand and only
                // render at all through the borrowed face; the word says the same
                // thing in the same ink on every machine, and section 3.3 asks for it
                // anyway — read the page in grey and every state is still there.
                b.L(string.Format("{0} {1} — {2} wks left", buff ? "helping:" : "hurting:",
                    (s.Name ?? "").Replace("_", " "), s.WeeksLeft), 10f, y, 30f,
                    buff ? DrawnUI.Sage : DrawnUI.Coral);
                y += 52f;
            }
            for (int i = 0; i < st.Commitments.Count; i++)
            {
                Commitment c = st.Commitments[i];
                b.L(string.Format("standing: {0} — ${1}/wk for {2} more wks",
                    c.Name, c.CashWk, c.WeeksLeft), 10f, y, 30f, DrawnUI.Blue);
                y += 52f;
            }
            // THE PAGE STATES ITS OWN LAW, like every desk does (2.7). Reading order
            // ends on the lesson: this sheet is the overflow, and the thing it has to
            // teach is that these rows are ranked and that the desks hold the controls.
            b.L("the rules of this page: everything the company is shouting about, loudest first · "
                + "a CLOCK fires on its week · a CONDITION expires on its own · a STANDING cost bills "
                + "until it runs out · nothing is fixed here, and every row names the desk that owns it",
                10f, 734f, 21f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 1100f);
        }

        /// <summary>A press inside this desk. `id` is whatever Draw registered —
        /// the rework's pressable rows jump to the desk that owns the fix, which is
        /// a FocusDesk on the binder and never a mutation here.</summary>
        public static void Handle(BinderScreen b, string id)
        {
            if (!string.IsNullOrEmpty(id) && id.StartsWith("go:"))
                b.FocusDesk(id.Substring(3));
        }
    }
}
