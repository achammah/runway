using TMPro;
using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — the binder's `the street` tab. Spec: docs/design/03-rivals-macro.md
    ///
    /// BinderScreen dispatches the tab body here and passes ITSELF, so this file
    /// draws through the binder's own helpers and never reaches into the sheet
    /// directly.
    ///
    /// WHAT IS HERE NOW is today's shipped street page — rivals and the money —
    /// moved verbatim off the binder so the lane REPLACES a working baseline
    /// instead of a blank file. The lane's job (03): the macro banner across the
    /// top, rival blocks grown from two lines to four (posture words, what they
    /// fight on, the last-3 action log — never raw floats), investors compressed
    /// to a line each once a third rival exists. The action log component is
    /// DeskKit.LogBlock().
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
        /// <summary>
        /// Draw the macro banner and the four-line rival blocks.
        ///
        /// Wrapped text is MEASURED, never assumed one line — fixed steps stacked
        /// the street on itself the first week a thesis wrapped.
        /// </summary>
        public static void Draw(BinderScreen b)
        {
            GameState st = b.State;
            b.L("the street", 10f, 6f, 40f);
            float y = 80f;
            for (int i = 0; i < st.Rivals.Count; i++)
            {
                Rival r = st.Rivals[i];
                b.L(string.Format("{0} — {1}", r.Name, SimEngine.Fuzz(r.Strength)), 10f, y, 32f);
                string plays = "plays: " + string.Join(", ", r.Tactics.ToArray());
                TextMeshProUGUI lbl = b.L(plays, 30f, y + 42f, 26f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 1070f);
                y += 50f + BinderScreen.Height(lbl) + 18f;
            }
            b.L("the money:", 10f, y + 10f, 32f);
            y += 64f;
            for (int i = 0; i < st.Investors.Count; i++)
            {
                Investor d = st.Investors[i];
                b.L(string.Format("{0} ({1})", d.Name, d.Archetype), 10f, y, 29f);
                string quote = string.Format("\"{0}\"  ·  {1}", d.Thesis, d.Trait);
                TextMeshProUGUI lbl = b.L(quote, 30f, y + 38f, 25f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.65f), 1070f);
                y += 44f + BinderScreen.Height(lbl) + 16f;
            }
        }

        /// <summary>A press inside this desk. `id` is whatever Draw registered.</summary>
        public static void Handle(BinderScreen b, string id)
        {
        }
    }
}
