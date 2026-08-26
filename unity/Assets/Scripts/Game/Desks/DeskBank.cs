using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — the binder's `the bank` tab, the tenth. Spec: docs/design/06-finance.md
    /// Approved in DECISIONS.md #1 and ruled in 00-spine.md section 11: the
    /// ledger keeps the levers and the compact weekly P&amp;L; the quote, the
    /// borrowing controls, the notes, the forecast, the sparklines, the tax
    /// block and the FULL grouped statement live HERE, at full width.
    ///
    /// BinderScreen dispatches the tab body here and passes ITSELF, so this file
    /// draws through the binder's own helpers and never reaches into the sheet.
    ///
    /// A BANKER'S LETTER, not a form (10-interface-language section 5.2): the
    /// quote reads as a sentence about YOU with its reasons in the parenthesis,
    /// the preview does the amortization out loud before the pen touches paper,
    /// and SIGN THE NOTE carries the commit stroke so signing feels like
    /// signing. Notes stack like filed letters; the shark's line is one cold clause.
    ///
    /// TWO PAGE MODES behind one pen word, the crew idiom: "" is THE DESK
    /// (borrow, sign, repay, forecast) and "books" is THE BOOKS (the grouped
    /// statement, the sparklines, the tax block, the break-even arithmetic).
    /// Esc pops "books" back to the desk before it closes the binder.
    ///
    /// TWIN LAW: this file and game/src/ui/desks/desk_bank.gd draw the same rows
    /// at the same coordinates.
    /// </summary>
    public static class DeskBank
    {
        // ── THE SHEET'S OWN GRID (mirrored byte-for-byte in desk_bank.gd) ──
        const float YQuote = 78f;
        const float YCost = 116f;
        const float YRule1 = 150f;
        const float YBorrow = 168f;
        const float YTerm = 230f;
        const float YPreview = 294f;
        const float YSign = 330f;
        const float YRule2 = 388f;
        const float YNotes = 404f;
        const float NotePitch = 58f;
        const int NotesMax = 3;
        const float YForecast = 646f;
        /// THE BOOKS' LAST LINE never crosses into the teaching footer at 700.
        const float YBottomMax = 654f;
        const float XToggle = 860f;
        const float XRepay = 960f;
        const float XSpark = 600f;

        static Color Ink(float a) { return DrawnUI.WithAlpha(DrawnUI.Ink, a); }

        static double[] D(int[] a)
        {
            var d = new double[a.Length];
            for (int i = 0; i < a.Length; i++) d[i] = a[i];
            return d;
        }

        /// <summary>Draw the desk.</summary>
        public static void Draw(BinderScreen b)
        {
            GameState st = b.State;
            // THE ACK PATTERN (00-spine section 4/11): a milestone bang is a tap
            // on the shoulder, not a permanent badge — looking answers it.
            if (st.HasFlag("tax_noticed")) st.SetFlag("tax_seen");
            if (st.HasFlag("broke_even")) st.SetFlag("broke_even_seen");
            object mode;
            bool books = b.Desk.TryGetValue("mode", out mode) && mode != null
                         && mode.ToString() == "books";
            if (books) DrawBooks(b, st);
            else DrawDesk(b, st);
        }

        // ══ THE DESK ═════════════════════════════════════════════════════

        static void DrawDesk(BinderScreen b, GameState st)
        {
            DeskKit.Title(b, "the bank — money, debt, and the taxman");
            // THE PAGE TOGGLE. Words, never glyphs: the hand font carries no
            // geometric shapes at all, so a typed arrow arrives in another face.
            DeskKit.Word(b, "the full books", XToggle, 16f, () => { b.Desk["mode"] = "books"; },
                DeskKit.Status, Ink(0.75f), 260f);
            float y = st.EraIndex() < 1 ? GarageBlock(b, st) : QuoteBlock(b, st);
            y = NotesBlock(b, st, y);
            // THE FORECAST IS A FLOOR, NOT A SLOT. YForecast is where it sits on an
            // ordinary sheet; on a floor-era sheet the venture block and a fourth
            // filed note push the notes down, and a fixed slot is a line drawn
            // through them.
            if (st.EraIndex() >= 1) ForecastLine(b, st, Mathf.Max(YForecast, y + 8f));
            DeskFooter(b, st);
        }

        /// <summary>
        /// NO BANK ANSWERS A GARAGE (00-spine section 9). The gate is TAUGHT,
        /// never greyed out: the player learns why credit exists at each stage,
        /// and the shark is the garage's whole lesson about desperate money.
        /// </summary>
        static float GarageBlock(BinderScreen b, GameState st)
        {
            float y = YQuote;
            b.L("no bank answers a garage — only the shark does.",
                DeskKit.XId, y, DeskKit.Status, Ink(0.75f), 1100f);
            y += 38f;
            b.L("a desk somewhere other than your kitchen is what puts you on their radar — banks lend against books, and a garage has none.",
                DeskKit.XId, y, DeskKit.Detail, Ink(0.5f), 1100f);
            y += 40f;
            b.L("money costs/wk: shark 18%  ·  equity: forever",
                DeskKit.XId, y, DeskKit.Detail, Ink(0.75f), 1100f);
            return DeskKit.Rule(b, y + 40f);
        }

        /// <summary>THE BANKER SIZING YOU UP. Every number in the quote is a
        /// pure read of the books, so the reasons in the parenthesis ARE the price.</summary>
        static float QuoteBlock(BinderScreen b, GameState st)
        {
            double rate = SimBank.BankRateWk(st);
            int rw = SimEngine.RunwayWeeks(st);
            b.L(string.Format(CultureInfo.InvariantCulture,
                "quotes {0:0.0}%/wk against your books (runway {1} · growth {2}{3}% · {4} era)",
                rate * 100.0, rw < 999 ? rw + " wks" : "gaining money",
                st.LastGrowth >= 0.0 ? "+" : "−", Gd.RoundToInt(Gd.Absf(st.LastGrowth) * 100.0), st.Era),
                DeskKit.XId, YQuote, DeskKit.Status, DrawnUI.Ink, 1100f);
            // THE COST OF CAPITAL, all three prices side by side — the one
            // comparison a founder never makes early enough.
            b.L(string.Format(CultureInfo.InvariantCulture,
                "money costs/wk: bank {0:0.0}%  ·  shark 18%  ·  equity: forever", rate * 100.0),
                DeskKit.XId, YCost, DeskKit.Detail, Ink(0.75f), 1100f);
            DeskKit.Rule(b, YRule1);
            int headroom = SimBank.BorrowHeadroom(st);
            int[] terms = SimBank.TermOptions(st, "bank");
            object bv;
            int borrow = b.Desk.TryGetValue("borrow", out bv) && bv != null
                ? Convert.ToInt32(bv) : Gd.Mini(10000, Gd.Maxi(headroom, SimBank.MinDraw));
            borrow = Gd.Clampi(borrow, SimBank.MinDraw, Gd.Maxi(headroom, SimBank.MinDraw));
            object tv;
            int term = b.Desk.TryGetValue("term", out tv) && tv != null
                ? Convert.ToInt32(tv) : terms[Gd.Mini(1, terms.Length - 1)];
            if (Array.IndexOf(terms, term) < 0) term = terms[0];
            // where the block ACTUALLY ended: the venture note is the one line below
            // the sign row, and the rule under it follows the ink, not a constant
            float blockEnd = YRule2;   // the rule follows the ink, not a constant
            bool locked = SimBank.CreditLocked(st);
            bool noLine = headroom < SimBank.MinDraw;
            bool dead = locked || noLine;
            int ceiling = Gd.Maxi(headroom, SimBank.MinDraw);
            double[] borrowLadder = D(SimBank.BorrowSteps);
            double[] termLadder = D(terms);
            DeskKit.Stepper(b, YBorrow, new DeskKit.StepRow
            {
                Name = "borrow",
                Why = "the draw — cash today, rented at the rate above",
                Value = "$" + GameUi.Money(borrow),
                Bound = borrow >= headroom && !dead ? "(all the line allows)" : "",
                Effect = dead ? "" : string.Format(CultureInfo.InvariantCulture, "{0}% of a ${1} line",
                    Gd.RoundToInt((double)borrow / Gd.Maxf(headroom, 1.0) * 100.0), GameUi.Money(headroom)),
                Disabled = dead, Pitch = 62f,
                AtMin = borrow <= SimBank.MinDraw,
                AtMax = borrow >= headroom,
                OnMinus = () => { b.Desk["borrow"] = Gd.Clampi(
                    Gd.ToInt(DeskKit.Ladder(borrowLadder, borrow, -1)), SimBank.MinDraw, ceiling); },
                OnPlus = () => { b.Desk["borrow"] = Gd.Clampi(
                    Gd.ToInt(DeskKit.Ladder(borrowLadder, borrow, 1)), SimBank.MinDraw, ceiling); },
            });
            DeskKit.Stepper(b, YTerm, new DeskKit.StepRow
            {
                Name = "over",
                // THE WHY IS ONE MEASURED LINE. This sheet is a fixed grid, so a why
                // that wraps writes its second line straight through the preview.
                Why = "the term — longer weeks, smaller payment, more interest",
                Value = term + " weeks",
                Effect = dead ? "" : term + " payments",
                Disabled = dead, Pitch = 62f,
                AtMin = term <= terms[0],
                AtMax = term >= terms[terms.Length - 1],
                OnMinus = () => { b.Desk["term"] = Gd.ToInt(DeskKit.Ladder(termLadder, term, -1)); },
                OnPlus = () => { b.Desk["term"] = Gd.ToInt(DeskKit.Ladder(termLadder, term, 1)); },
            });
            // THE AMORTIZATION LESSON, DONE OUT LOUD before the pen moves: what
            // a week costs, what the whole note costs, and the difference.
            int pay = SimBank.LoanPaymentWk(borrow, rate, term);
            int allIn = pay * term;
            if (dead)
            {
                b.L("no terms to preview until the bank answers.",
                    DeskKit.XId, YPreview, DeskKit.Detail, Ink(0.5f), 1100f);
            }
            else
            {
                b.L(string.Format(CultureInfo.InvariantCulture,
                    "= ${0}/wk  ·  ≈${1} all-in (${2} interest — that is what the time costs)",
                    GameUi.Money(pay), GameUi.Money(allIn), GameUi.Money(Gd.Maxi(allIn - borrow, 0))),
                    DeskKit.XId, YPreview, DeskKit.Status, DrawnUI.Blue, 1100f);
            }
            if (locked)
            {
                b.L("the bank won't answer — clear the collectors first.",
                    DeskKit.XId, YSign + 8f, DeskKit.Status, DrawnUI.Coral, 1100f);
            }
            else if (noLine)
            {
                b.L("no revenue, no line — a bank lends against what customers already pay you.",
                    DeskKit.XId, YSign + 8f, DeskKit.Status, Ink(0.6f), 1100f);
            }
            else
            {
                // THE SIGNATURE BEAT: the stroke draws under the words, and only
                // then do the books change.
                int signBorrow = borrow;
                int signTerm = term;
                Button btn = null;
                btn = DeskKit.Word(b, "[ SIGN THE NOTE ]", DeskKit.XId, YSign, () =>
                {
                    DeskKit.SignStroke(b, btn, "[ SIGN THE NOTE ]", DeskKit.XId, YSign, () =>
                    {
                        SimBank.SignNote(st, "bank", signBorrow, signTerm);
                        b.Desk.Remove("borrow");
                        b.Refresh();
                    });
                }, DeskKit.Row, DrawnUI.Ink, 420f, false);
                // VENTURE DEBT rides the same block once a round has closed (floor+).
                int vcap = Gd.Mini(SimBank.VentureCap(st), headroom);
                if (vcap >= SimBank.MinDraw)
                {
                    double vrate = SimBank.VentureRateWk(st);
                    int vterm = SimBank.TermsVenture[0];
                    Button vbtn = null;
                    vbtn = DeskKit.Word(b, "[ take venture debt ]", 460f, YSign, () =>
                    {
                        DeskKit.SignStroke(b, vbtn, "[ take venture debt ]", 460f, YSign, () =>
                        {
                            SimBank.SignNote(st, "venture", vcap, vterm);
                            b.Refresh();
                        });
                    }, DeskKit.Status, Ink(0.8f), 380f, false);
                    // ONE MEASURED LINE at 690px, clear of the 46px word above it
                    // -- and the rule below moves down to meet it. Wrapped and
                    // fixed, this line was drawn through both the button and the
                    // divider, and its tail landed in WHAT YOU OWE.
                    float vy = YSign + 48f;
                    b.L(string.Format(CultureInfo.InvariantCulture,
                        "${0} at {1:0.0}%/wk · interest-only · balloon in {2} wks · {3:0.00}% in warrants",
                        GameUi.Money(vcap), vrate * 100.0, vterm, SimBank.WarrantPct),
                        460f, vy, DeskKit.Law, Ink(0.5f), 690f);
                    blockEnd = Mathf.Max(blockEnd, vy + 34f);
                }
            }
            return DeskKit.Rule(b, blockEnd);
        }

        /// <summary>THE FILED LETTERS. Each note carries what it costs, what is
        /// left, and how long — the cliff visible per note, never as one blended
        /// debt number.</summary>
        static float NotesBlock(BinderScreen b, GameState st, float y)
        {
            b.L("WHAT YOU OWE", DeskKit.XId, y, DeskKit.Detail, Ink(0.6f), 400f);
            y += 30f;
            if (st.Loans.Count == 0 && st.LoanPrincipal <= 0)
            {
                return DeskKit.Empty(b, DeskKit.XId, y,
                    "you owe nobody anything. rare, and worth noticing.",
                    "debt buys time, and time is the only thing a runway is made of.");
            }
            int shown = 0;
            // a legacy shark that has not met a tick yet still reads as debt
            if (st.LoanPrincipal > 0)
            {
                b.L("THE SHARK — $" + GameUi.Money(st.LoanPrincipal) + " (18%/wk, it feeds first)",
                    DeskKit.XId, y, DeskKit.Row, DrawnUI.Coral, 900f);
                b.L("$" + GameUi.Money((int)Math.Ceiling(st.LoanPrincipal * SimBank.SharkRate))
                    + "/wk in interest alone — it takes everything above $2,000 the week you have it",
                    DeskKit.XId, y + 34f, DeskKit.Detail, Ink(0.65f), 900f);
                y += NotePitch;
                shown += 1;
            }
            for (int i = 0; i < st.Loans.Count; i++)
            {
                if (shown >= NotesMax) break;
                Loan note = st.Loans[i];
                int bal = note.Balance;
                if (bal <= 0) continue;
                double rate = note.RateWk;
                string head, sub;
                if (note.Kind == "shark")
                {
                    head = "THE SHARK — $" + GameUi.Money(bal) + " (18%/wk, it feeds first)";
                    sub = "$" + GameUi.Money((int)Math.Ceiling(bal * rate))
                          + "/wk in interest alone — it takes everything above $2,000";
                }
                else if (note.Kind == "venture")
                {
                    int toBalloon = Gd.Maxi(note.TakenWeek + note.TermWk - st.Week, 0);
                    head = "venture note — $" + GameUi.Money(bal) + " owed";
                    sub = string.Format(CultureInfo.InvariantCulture,
                        "interest-only ${0}/wk · {1:0.0}%/wk · balloon ${2} in {3} wks",
                        GameUi.Money((int)Math.Ceiling(bal * rate)), rate * 100.0, GameUi.Money(bal), toBalloon);
                }
                else
                {
                    int left = SimBank.NoteWeeksLeft(bal, rate, note.PayWk);
                    head = "bank note — $" + GameUi.Money(bal) + " left";
                    sub = string.Format(CultureInfo.InvariantCulture, "${0}/wk · {1:0.0}%/wk · {2}",
                        GameUi.Money(note.PayWk), rate * 100.0,
                        left >= 0 ? left + " wks" : "no end at this payment");
                }
                if (note.Missed > 0) sub += " · missed " + note.Missed;
                b.L(head, DeskKit.XId, y, DeskKit.Row,
                    note.Kind == "shark" || note.Missed > 0 ? DrawnUI.Coral : DrawnUI.Ink, 900f);
                b.L(sub, DeskKit.XId, y + 34f, DeskKit.Detail, Ink(0.65f), 900f);
                // REPAY IS TWO-TAP because it books an immediate, irreversible
                // cash cost: the armed caption is where the invoice gets quoted.
                int idx = i;
                int quote = Gd.Mini(st.Cash - GameState.RAMEN_PER_WEEK, bal);
                if (quote > 0)
                {
                    DeskKit.Arm(b, "repay_" + idx, "repay",
                        "$" + GameUi.Money(quote) + " now — sure?", XRepay, y,
                        () => { SimBank.RepayNote(st, idx); }, 200f);
                }
                else
                {
                    b.L("nothing spare", XRepay, y + 8f, DeskKit.Detail, Ink(0.35f), 200f);
                }
                y += NotePitch;
                shown += 1;
            }
            int live = 0;
            foreach (Loan l in st.Loans) if (l.Balance > 0) live += 1;
            if (st.LoanPrincipal > 0) live += 1;
            return DeskKit.More(b, DeskKit.XId, y + 2f, live - shown, "notes are filed behind these");
        }

        /// <summary>THE FP&amp;A STRIP: what the plan does to the bank account,
        /// before surprises.</summary>
        static void ForecastLine(BinderScreen b, GameState st, float y)
        {
            List<SimBank.ForecastWeek> rows = SimBank.ForecastCash(st, SimBank.ForecastWeeks);
            // the teaching footer owns 700 down: a forecast with no room yields to it
            if (rows.Count == 0 || y + 34f > DeskKit.FooterY - 6f) return;
            var parts = new List<string>();
            bool below = false;
            foreach (SimBank.ForecastWeek r in rows)
            {
                if (r.Cash < 0) below = true;
                parts.Add(string.Format(CultureInfo.InvariantCulture,
                    r.Cash < 0 ? "−${0:0.0}k" : "${0:0.0}k", Math.Abs(r.Cash) / 1000.0));
            }
            b.L(string.Format(CultureInfo.InvariantCulture,
                "the next {0} weeks, as planned: {1} (before surprises)",
                rows.Count, string.Join(" -> ", parts.ToArray())),
                DeskKit.XId, y, DeskKit.Status,
                below ? DrawnUI.Coral : Ink(0.8f), 1100f);
        }

        /// <summary>THE DESK STATES ITS OWN LAWS, and the warning outranks them
        /// when one fires.</summary>
        static void DeskFooter(BinderScreen b, GameState st)
        {
            int be = SimBank.BreakEvenCustomers(st);
            string computed = be > 0
                ? string.Format(CultureInfo.InvariantCulture,
                    "break-even: {0} customers at these prices — {1} on the books · each one contributes ${2:0.0}/wk",
                    be, st.Traction, SimBank.ContributionMargin(st))
                : string.Format(CultureInfo.InvariantCulture,
                    "no count breaks even — each customer costs ${0:0.00} more than they pay",
                    Gd.Absf(SimBank.ContributionMargin(st)));
            string warning = "";
            int service = SimBank.DebtServiceWk(st);
            if (SimBank.CreditLocked(st))
                warning = "a note is in default: the collectors are calling and investors do check your credit";
            else if (service > 0 && st.Cash < 2 * service)
                warning = "the repayment cliff: $" + GameUi.Money(service)
                          + " a week is due and there is $" + GameUi.Money(st.Cash) + " in the bank";
            DeskKit.Footer(b, computed,
                "the rules of this desk: a LOAN is rented money — you pay for the time, not the "
                + "amount · INTEREST bills every week, sold or not · the taxman takes his cut of "
                + "profit, never of revenue · repaying early is the only discount there is",
                warning);
        }

        // ══ THE BOOKS ════════════════════════════════════════════════════

        /// <summary>
        /// THE FULL GROUPED STATEMENT (00-spine section 2 display split):
        /// IN -> COST OF SERVING -> KEEPING THE LIGHTS ON -> THE LEVERS -> THE
        /// UNPLANNED -> THE BANK &amp; THE STATE -> THE BOTTOM LINE. Grouped,
        /// because an income statement that is one flat list of lanes teaches
        /// nothing about which costs are which.
        /// </summary>
        static void DrawBooks(BinderScreen b, GameState st)
        {
            DeskKit.Title(b, "the books — last week, line by line");
            DeskKit.Back(b, "back to the bank", () => { b.Desk["mode"] = ""; }, XToggle, 16f);
            Pnl p = st.LastPnl;
            if (p == null)
            {
                DeskKit.Empty(b, DeskKit.XId, YQuote,
                    "no week has closed yet — the books open after the first LOCK IN.",
                    "a P&L is a record of what happened, and nothing has.");
                return;
            }
            float y = YQuote;
            y = Group(b, y, "IN", new[] { "revenue $" + GameUi.Money(p.Revenue) }, DrawnUI.Blue);
            string learn = p.Learning < 0.995
                ? string.Format(CultureInfo.InvariantCulture,
                    "  (learning ×{0:0.00} — scale earns its margin)", p.Learning) : "";
            var serving = new List<string> { "cogs $" + GameUi.Money(p.Cogs) + learn };
            // HARDWARE PAYS FOR ITS PARTS AT THE BENCH, not at the sale (09's
            // ruling), so the build lanes belong beside cogs rather than in a
            // lane of their own. Both are 0 off a Hardware run, so this line
            // simply does not exist there.
            string built = Some(new[]
            {
                new KeyValuePair<string, int>("built in-house", p.Production),
                new KeyValuePair<string, int>("bought outside", p.Subcontract),
            });
            if (built.Length > 0)
                serving.Add(built + " — hardware is paid at the bench, not at the sale");
            y = Group(b, y, "COST OF SERVING", serving.ToArray(), DrawnUI.Ink);
            var lights = new List<string>
            {
                "rent $" + GameUi.Money(p.Rent) + " · payroll $" + GameUi.Money(p.Payroll)
                    + " · infra $" + GameUi.Money(p.Infra),
            };
            string extra = Some(new[]
            {
                new KeyValuePair<string, int>("catalog", p.OfferFixed),
                new KeyValuePair<string, int>("upkeep", p.EquipUpkeep),
                new KeyValuePair<string, int>("carrying", p.Carrying),
            });
            if (extra.Length > 0) lights.Add(extra);
            y = Group(b, y, "KEEPING THE LIGHTS ON", lights.ToArray(), DrawnUI.Ink);
            y = Group(b, y, "THE LEVERS", new[]
            {
                "marketing $" + GameUi.Money(p.Marketing) + " · sales $" + GameUi.Money(p.Sales)
                    + " · care $" + GameUi.Money(p.Care) + " · rnd $" + GameUi.Money(p.Rnd)
                    + " · office $" + GameUi.Money(p.Office),
            }, DrawnUI.Ink);
            string unplanned = Some(new[]
            {
                new KeyValuePair<string, int>("the unforeseen", p.Incident),
                new KeyValuePair<string, int>("severance", p.Severance),
                new KeyValuePair<string, int>("recruiting", p.Recruiting),
                new KeyValuePair<string, int>("standing", p.LiabilitiesWk),
            });
            if (unplanned.Length > 0) y = Group(b, y, "THE UNPLANNED", new[] { unplanned }, DrawnUI.Ink);
            int principal = Gd.ToInt(st.GetMetaF("bank_principal_wk", 0.0));
            y = Group(b, y, "THE BANK & THE STATE", new[]
            {
                "interest $" + GameUi.Money(p.Interest) + " · principal $" + GameUi.Money(principal)
                    + " · tax $" + GameUi.Money(p.Tax),
            }, DrawnUI.Coral);
            int be = SimBank.BreakEvenCustomers(st);
            // THE BOTTOM LINE GETS THE WHOLE WIDTH and a ceiling. Nine lanes can all
            // bill in one week: the groups above are a measured cursor, so on a busy
            // week `y` arrives near the footer, and at 560px this line wrapped into
            // the desk laws. The right column ends well above YBottomMax.
            b.L("THE BOTTOM LINE: " + (p.Net >= 0 ? "+" : "−") + "$" + GameUi.Money(Gd.Absi(p.Net))
                + " a week  ·  " + (be > 0
                    ? "break-even " + be + " customers (" + st.Traction + " now)"
                    : "no count breaks even"),
                DeskKit.XId, Mathf.Min(y + 4f, YBottomMax), DeskKit.Row,
                p.Net >= 0 ? DrawnUI.Sage : DrawnUI.Coral, 1100f);
            // ── the right column: the two series the bank itself prices you on
            b.L("net, weekly:", XSpark, YQuote, 24f, Ink(0.6f), 600f);
            DrawnChart.MountSpark(b.Content, NetSeries(st), DrawnUI.Sage,
                                  XSpark, YQuote + 32f, 540f, 64f);
            float sy = YQuote + 32f + 64f + 12f;
            b.L("revenue, weekly:", XSpark, sy + 10f, 24f, Ink(0.6f), 600f);
            DrawnChart.MountSpark(b.Content, RevenueSeries(st), DrawnUI.Blue,
                                  XSpark, sy + 42f, 540f, 64f);
            sy = sy + 42f + 64f + 12f + 14f;
            b.L("THE TAXMAN", XSpark, sy, DeskKit.Detail, Ink(0.6f), 540f);
            sy += 30f;
            int ebt = p.Revenue - p.Burn - p.LiabilitiesWk - p.Interest;
            if (st.EraIndex() < SimBank.TaxEra)
            {
                sy = TaxLine(b, sy, "nothing yet — profit is taxed from the office era up. Cash-basis and below the radar until then.");
            }
            else
            {
                // SIGN OUTSIDE THE DOLLAR (10-interface-language 1.3): a loss-making
                // week reads −$13,804, never $-13,804.
                sy = TaxLine(b, sy, "20% of EBT — earnings after interest, before tax. Last week's EBT: "
                    + Signed(ebt) + " -> tax $" + GameUi.Money(p.Tax));
            }
            if (st.TaxLossCarry > 0)
            {
                sy = TaxLine(b, sy, "losses carried forward: $" + GameUi.Money(st.TaxLossCarry)
                    + " — they shelter the next profits before the taxman sees them");
            }
            if (st.Receivables.Count > 0)
            {
                int owed = 0;
                foreach (Commitment r in st.Receivables) owed += r.CashWk;
                sy = TaxLine(b, sy, "net-30 float: $" + GameUi.Money(owed)
                    + " invoiced and not yet in the bank — profit is not cash");
            }
            DeskKit.Footer(b,
                "burn is OPERATING spend only · interest and tax sit outside it, which is why the bottom line is smaller than in − out",
                "read it top to bottom: what came in, what serving cost, what the lights cost, "
                + "what you chose to spend, what nobody planned, what the bank and the state took",
                "");
        }

        /// <summary>ONE LINE OF THE TAXMAN BLOCK, cursor-advanced by MEASURED
        /// height. A fixed 56px step wrote the loss-carryforward line straight
        /// through the second line of the EBT sentence the week the numbers got
        /// long enough to wrap.</summary>
        static float TaxLine(BinderScreen b, float sy, string text)
        {
            TextMeshProUGUI l = b.L(text, XSpark, sy, DeskKit.Detail, Ink(0.7f), 540f);
            return sy + Mathf.Max(BinderScreen.Height(l), 28f) + 14f;
        }

        /// <summary>Money with the sign OUTSIDE the dollar (1.3): −$300, never
        /// $-300.</summary>
        static string Signed(int v)
        {
            return v < 0 ? "−$" + GameUi.Money(Gd.Absi(v)) : "$" + GameUi.Money(v);
        }

        /// <summary>One captioned group of the statement. Returns the y it ended at.</summary>
        static float Group(BinderScreen b, float y, string caption, string[] lines, Color col)
        {
            b.L(caption, DeskKit.XId, y, DeskKit.Detail, Ink(0.6f), 540f);
            y += 28f;
            for (int i = 0; i < lines.Length; i++)
            {
                var l = b.L(lines[i], DeskKit.XId + 18f, y, DeskKit.Status, col, 540f);
                y += Mathf.Max(BinderScreen.Height(l), 30f) + 4f;
            }
            return y + 8f;
        }

        /// <summary>The lanes that only exist some weeks, joined — and nothing
        /// at all when they are all zero, because a statement full of $0 rows
        /// teaches nothing.</summary>
        static string Some(KeyValuePair<string, int>[] lanes)
        {
            var parts = new List<string>();
            foreach (KeyValuePair<string, int> kv in lanes)
                if (kv.Value != 0) parts.Add(kv.Key + " $" + GameUi.Money(kv.Value));
            return string.Join(" · ", parts.ToArray());
        }

        /// <summary>The net series, with the honest fallback: a pre-finance
        /// history row has no `net` at all, so it reads as revenue − burn —
        /// close enough for history and exact from the week this lane landed.</summary>
        static List<float> NetSeries(GameState st)
        {
            var outp = new List<float>();
            foreach (MetricSnapshot m in st.MetricHistory)
                outp.Add(m.Net.HasValue ? m.Net.Value : m.Revenue - m.Burn);
            return outp;
        }

        static List<float> RevenueSeries(GameState st)
        {
            var outp = new List<float>();
            foreach (MetricSnapshot m in st.MetricHistory) outp.Add(m.Revenue);
            return outp;
        }

        /// <summary>A press inside this desk. Every control here carries its own
        /// closure, so nothing routes through the id dispatcher.</summary>
        public static void Handle(BinderScreen b, string id)
        {
        }
    }
}
