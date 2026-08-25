using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — the binder's `the bank` tab, the tenth. Spec: docs/design/06-finance.md
    /// Approved in docs/design/DECISIONS.md #1: the ledger keeps the levers and
    /// the compact weekly P&amp;L; borrowing, notes, the forecast, the tax block
    /// and the full grouped statement live HERE, at full width.
    ///
    /// BinderScreen dispatches the tab body here and passes ITSELF, so this file
    /// draws through the binder's own helpers and never reaches into the sheet
    /// directly.
    ///
    /// WHAT IS HERE NOW IS A PLACEHOLDER, and the whole body is the lane's to
    /// replace: the sheet must never be blank paper, so until 06 lands this page
    /// states what the desk is for and what the company owes today, in the desk's
    /// own voice. Nothing has been relocated off the ledger yet — moving the loan
    /// content is 06's job, so that the two halves move in one commit and no week
    /// renders with the money in neither place.
    ///
    /// The bar every surface ships at (00-spine section 11): readable first pass
    /// by a tired player; concepts named in real business terms with a teaching
    /// line where a number first appears; no dead ends and every state leavable;
    /// drawn in the game's hand, never a SaaS panel. The shared components live
    /// in Game/DeskKit.cs — the borrow/term steppers are DeskKit.Stepper(), and
    /// SIGN THE NOTE is DeskKit.Review().
    ///
    /// TWIN LAW: this file and game/src/ui/desks/desk_bank.gd draw the same rows
    /// at the same coordinates.
    /// </summary>
    public static class DeskBank
    {
        /// <summary>Draw the borrow/repay controls, notes list, forecast, tax block, full statement.</summary>
        public static void Draw(BinderScreen b)
        {
            GameState st = b.State;
            float y = DeskKit.Title(b, "the bank — money, debt, and the taxman");
            // WHAT THE COMPANY OWES, TODAY. The shark is the only lender a garage
            // has, and it says so in one cold clause.
            if (st.LoanPrincipal > 0)
            {
                b.L("THE SHARK — $" + GameUi.Money(st.LoanPrincipal)
                    + " owed at 18%/wk, and it feeds first",
                    DeskKit.XId, y, DeskKit.Row, DrawnUI.Coral, 1100f);
                y += 40f;
                b.L("that is $" + GameUi.Money(Gd.RoundToInt(st.LoanPrincipal * 0.18))
                    + " a week in interest alone, before anything you sell",
                    DeskKit.XId, y, DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.75f), 1100f);
                y += 44f;
            }
            else
            {
                y = DeskKit.Empty(b, DeskKit.XId, y,
                    "you owe nobody anything. rare, and worth noticing.",
                    "debt buys time, and time is the only thing a runway is made of.");
                y += 10f;
            }
            // THE ERA GATE, taught rather than greyed out (00-spine section 9).
            if (st.Era == "garage")
            {
                b.L("no bank answers a garage — only the shark does.",
                    DeskKit.XId, y, DeskKit.Status, DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 1100f);
                y += 38f;
                b.L("a desk somewhere other than your kitchen is what puts you on their radar.",
                    DeskKit.XId, y, DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 1100f);
            }
            else
            {
                b.L("the bank returns your calls now — terms, notes and the taxman land on this desk.",
                    DeskKit.XId, y, DeskKit.Status, DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 1100f);
            }
            DeskKit.Footer(b, "",
                "the rules of this desk: a LOAN is rented money — you pay for the time, not the "
                + "amount · INTEREST bills every week, sold or not · the taxman takes his cut of "
                + "profit, never of revenue", "");
        }

        /// <summary>A press inside this desk. `id` is whatever Draw registered.</summary>
        public static void Handle(BinderScreen b, string id)
        {
        }
    }
}
