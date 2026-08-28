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
    /// DESK — COSTS · "the bank" = THE MEETING (twin of desk_bank_page.gd;
    /// DAG2 W2 L-MONEY; DECISIONS: didactic rework A). Four numbered zones:
    /// 1 YOUR STANDING (the rate DERIVED from SimBank.BankRateWk's own
    /// inputs), 2 WHAT YOU OWE (each note anatomized — the Monday split, the
    /// unmoving interest-only bar — plus THE REFINANCE row), 3 NEW MONEY
    /// (separate −/+ and THE RECEIPT before SIGN), 4 IF A MONDAY IS MISSED
    /// (the engine's real ladder as stairs). THE COLLAPSE LADDER ON THE
    /// ZONES: 1–2 always open; 3 and 4 share the lower page — one open, the
    /// other a numbered bar (press to swap). BOOKS mode = the full statement
    /// restyled onto THE LEDGER SHEET.
    ///
    /// DAG3 (13-binder-ux): THE RECEIPT re-inks on every stepper press (a
    /// brief alpha dip); the locked standing renders its unlock as a
    /// CHECKLIST read from the real lock state; DO lane [borrow] [repay —
    /// worst note] [refinance] as available; note cards register "note_&lt;i&gt;"
    /// and the borrow stepper "borrow" for spotlit landings; the ask strip
    /// names the red; the hero wears its S5 delta. The DO lane rides the
    /// meeting only — the books are a read view.
    /// </summary>
    public static class DeskBankPage
    {
        public const string Question = "what do we owe and can we borrow?";

        const float SheetX = 10f;
        const float ZoneW = 1120f;
        const float YRules = 844f;
        const int NotesMax = 2;
        const float CardGap = 18f;
        /// The refinance executor lands with L-DIVWORKS (refinance_note);
        /// until the coordinator flips this the preview renders disabled.
        const bool RefiWired = true;

        static readonly Color[] StairCols =
            { DrawnUI.Hex("F6F0DE"), DrawnUI.Hex("F2D6B8"), DrawnUI.Hex("D93425") };
        static readonly Color Pos = DrawnUI.Hex("5D7A50");

        sealed class LiveNote
        {
            public int Idx = -1;
            public Loan Note;
        }

        /// S8 — the bank sleeps through a debtless garage: no bank answers,
        /// nothing owed, nothing to read. Any debt wakes the tab.
        public static bool IsDormant(GameState s)
        {
            return s.EraIndex() < 1 && SimBank.DebtTotal(s) <= 0;
        }

        /// S10 — the rail's four-character read: what is owed.
        public static string MicroStatus(GameState s)
        {
            int debt = SimBank.DebtTotal(s);
            if (debt <= 0) return "";
            if (debt >= 1000) return "$" + (debt / 1000.0).ToString("0.0") + "k";
            return "$" + debt;
        }

        public static string[] HeroSummary(GameState s)
        {
            return new[] { "debt $" + GameUi.Money(SimBank.DebtTotal(s)),
                "the bank quotes " + (SimBank.BankRateWk(s) * 100.0).ToString("0.0") + "%/wk" };
        }

        public static void Draw(BinderScreen b)
        {
            GameState state = b.State;
            // THE ACK PATTERN (kept from the shipped desk).
            if (state.HasFlag("tax_noticed")) state.SetFlag("tax_seen");
            if (state.HasFlag("broke_even")) state.SetFlag("broke_even_seen");
            if (DStr(b, "mode") == "books") { DrawBooks(b, state); return; }
            DrawMeeting(b, state);
        }

        // ══════════════════════════ THE MEETING ═════════════════════════════

        static void DrawMeeting(BinderScreen b, GameState state)
        {
            int debt = SimBank.DebtTotal(state);
            int service = MondayOut(state);
            double rate = SimBank.BankRateWk(state);

            string big = "we owe $" + GameUi.Money(debt);
            b.L(big, SheetX, 6f, DeskKit.HeroSize, DrawnUI.Ink, 560f);
            float bw = DrawnUI.MeasureWidth(big, DeskKit.HeroSize);
            // S5 — which way the debt moved since the last open (prev first)
            string prevDebt = b.SeenPrev("the bank", "debt");
            b.Seen("the bank", "debt", debt.ToString());
            float capX = SheetX + bw + 14f;
            int prevDebtI;
            if (prevDebt != "" && int.TryParse(prevDebt, out prevDebtI) && prevDebtI != debt)
            {
                DeskKit.DeltaArrow(b, SheetX + bw + 10f, 26f, debt, prevDebtI);
                capX += 26f;
            }
            b.L("· $" + GameUi.Money(service) + " leaves every Monday", capX, 22f,
                DeskKit.Row, Ink(0.7f), 420f);
            b.L(HeroSentence(state), SheetX, 62f, DeskKit.Detail, Ink(0.6f), 700f);
            var opinion = b.L("the bank's opinion of you: " + (rate * 100.0).ToString("0.0")
                + "%/wk — why? see YOUR STANDING", SheetX, 8f, DeskKit.Law, Ink(0.6f), ZoneW);
            opinion.alignment = TMPro.TextAlignmentOptions.TopRight;
            DeskKit.Word(b, "BOOKS ▸ the full statement", 910f, 34f,
                () => { b.Desk["mode"] = "books"; }, DeskKit.Law, Ink(0.75f), 220f);
            string clockTxt = DeadlineText(state);
            if (clockTxt != "") DeskKit.ClockChip(b, 910f, 68f, clockTxt);

            // S2a — red speaks on the page: the strip gets its own y under
            // the hero sentence and pushes the zones down only when it drew
            float y = 96f;
            if (DeskKit.AskStrip(b, "the bank", SheetX, 88f, 1000f,
                    "find the Monday or repay the note"))
                y = 118f;
            y = ZoneStanding(b, state, y, rate);
            y = ZoneOwed(b, state, y);
            // zones 3 and 4 share the lower page: one open, the other a bar
            if (DBool(b, "zone4"))
            {
                y = Zone3Bar(b, state, y, rate);
                y = ZoneStairs(b, state, y);
            }
            else
            {
                y = ZoneNewMoney(b, state, y, rate);
                y = Zone4Bar(b, y);
            }
            ForecastStrip(b, state, y);
            DoLaneRow(b, state);
            Foot(b, state);
        }

        /// S3 — the meeting's primary actions, one slot, as available:
        /// borrow opens zone 3, repay pays the dearest filed note down (the
        /// existing two-tap op), refinance fires the tail line's swap (sign).
        static void DoLaneRow(BinderScreen b, GameState state)
        {
            var actions = new List<DeskKit.DoAction>();
            bool garage = state.EraIndex() < 1;
            bool locked = SimBank.CreditLocked(state);
            int headroom = SimBank.BorrowHeadroom(state);
            if (!garage && !locked && headroom >= SimBank.MinDraw)
                actions.Add(new DeskKit.DoAction { Label = "borrow — up to $" + GameUi.Money(headroom),
                    Cb = () => { b.Desk["zone4"] = false; }, Tier = "" });
            int worst = WorstNote(state);
            if (worst >= 0)
            {
                int quote = Math.Min(state.Cash - GameState.RAMEN_PER_WEEK,
                    state.Loans[worst].Balance);
                if (quote > 0)
                {
                    int widx = worst;
                    actions.Add(new DeskKit.DoAction
                    {
                        Label = "repay — the " + (state.Loans[worst].RateWk * 100.0).ToString("0.0")
                            + "% note",
                        Cb = () => SimBank.RepayNote(state, widx), Tier = "two-tap",
                    });
                }
            }
            int ridx = RefiNote(state);
            if (RefiWired && ridx >= 0 && !locked)
            {
                Loan note = state.Loans[ridx];
                if (SimBank.BankRateWk(state) < note.RateWk)
                {
                    int rterm = Math.Max(note.TermWk - (state.Week - note.TakenWeek), 4);
                    actions.Add(new DeskKit.DoAction
                    {
                        Label = "refinance — today's "
                            + (SimBank.BankRateWk(state) * 100.0).ToString("0.0") + "%",
                        Cb = () => SimWorks.OpRefinanceNote(state,
                            new Dictionary<string, object> { { "old_id", ridx }, { "weeks", rterm } }),
                        Tier = "sign",
                    });
                }
            }
            DeskKit.DoLane(b, actions);
        }

        /// The dearest live FILED note — the one repay answers first.
        static int WorstNote(GameState state)
        {
            int best = -1;
            double bestRate = -1.0;
            for (int i = 0; i < state.Loans.Count; i++)
            {
                if (state.Loans[i].Balance <= 0) continue;
                if (state.Loans[i].RateWk > bestRate)
                {
                    bestRate = state.Loans[i].RateWk;
                    best = i;
                }
            }
            return best;
        }

        /// Zone 1 — the rate derived from the engine's own inputs; only the
        /// terms that actually move it print. Garage/lock teach instead.
        static float ZoneStanding(BinderScreen b, GameState state, float y, double rate)
        {
            bool garage = state.EraIndex() < 1;
            bool locked = SimBank.CreditLocked(state);
            if (garage || locked)
            {
                float hh = locked ? 172f : 132f;
                var zz = DeskKit.Zone(b, SheetX, y, ZoneW, hh, 1, "your standing",
                    "— the rate is not a constant; it is what the bank thinks of your books");
                if (garage)
                {
                    b.L("no bank answers a garage — only the shark does, at 18%/wk.",
                        zz.ContentX, zz.ContentY - 4f, DeskKit.Detail, DrawnUI.Ink, 1060f);
                    b.L("banks lend against books, and a garage has none — a desk somewhere real puts you on their radar.",
                        zz.ContentX, zz.ContentY + 22f, DeskKit.Law, Ink(0.6f), 1060f);
                }
                else
                {
                    b.L("the bank stopped answering — a note is in default and the collectors are calling.",
                        zz.ContentX, zz.ContentY - 4f, DeskKit.Detail, DrawnUI.Coral, 1060f);
                    UnlockChecklist(b, state, zz.ContentX, zz.ContentY + 26f);
                    b.L("repay the distressed note and the lock lifts — it is derived, never a grudge.",
                        zz.ContentX, zz.ContentY + 62f, DeskKit.Law, Ink(0.6f), 1060f);
                }
                return y + hh + 4f;
            }
            int rw = SimEngine.RunwayWeeks(state);
            double health = Gd.Clampf((12.0 - rw) / 12.0, 0.0, 1.0);
            double slump = Gd.Clampf(-state.LastGrowth / 0.25, 0.0, 1.0);
            double eraDisc = 0.005 * state.EraIndex();
            double raw = 0.03 + 0.07 * health + 0.02 * slump - eraDisc;
            var rows = new List<string[]>();
            var cols = new List<Color>();
            rows.Add(new[] { "every company starts at", "3.0%" });
            cols.Add(Ink(0.85f));
            if (health > 0.0)
            {
                rows.Add(new[] { "only " + rw + " weeks of runway worries them",
                    "+" + (health * 7.0).ToString("0.0") + "%" });
                cols.Add(DrawnUI.Coral);
            }
            if (slump > 0.0)
            {
                rows.Add(new[] { "revenue slipping "
                    + Math.Round(Math.Abs(state.LastGrowth) * 100.0) + "% worries them",
                    "+" + (slump * 2.0).ToString("0.0") + "%" });
                cols.Add(DrawnUI.Coral);
            }
            if (eraDisc > 0.0)
            {
                rows.Add(new[] { state.Era + "-era track record reassures them",
                    "−" + (eraDisc * 100.0).ToString("0.0") + "%" });
                cols.Add(Pos);
            }
            string totalLabel = "your rate — repriced as your books change";
            if (rate > raw + 0.0005) totalLabel = "your rate — the small-business floor holds it here";
            else if (rate < raw - 0.0005) totalLabel = "your rate — capped; nobody prices above the shark";
            rows.Add(new[] { totalLabel, (rate * 100.0).ToString("0.0") + "%" });
            cols.Add(DrawnUI.Ink);
            float h = 78f + rows.Count * 20f + 19f;
            var z = DeskKit.Zone(b, SheetX, y, ZoneW, h, 1, "your standing",
                "— the rate is not a constant; it is what the bank thinks of your books");
            float cx = z.ContentX;
            float ry = z.ContentY - 6f;
            for (int i = 0; i < rows.Count; i++)
            {
                bool last = i == rows.Count - 1;
                if (last) { ry += 7f; DeskKit.PenRule(b, ry - 3f, cx, 620f, Ink(0.6f)); }
                b.L(rows[i][0], cx, ry + 2f, 17f, last ? cols[i] : Ink(0.75f), 530f);
                var v = b.L(rows[i][1], cx, ry + 2f, 17f, cols[i], 620f);
                v.alignment = TMPro.TextAlignmentOptions.TopRight;
                ry += 20f;
            }
            b.L("they would lend you up to $" + GameUi.Money(SimBank.BorrowHeadroom(state))
                + " today — the credit line your books earn", cx + 680f, z.ContentY + 2f,
                DeskKit.Law, Ink(0.6f), 370f);
            return y + h + 4f;
        }

        /// The unlock as a CHECKLIST, read from the REAL lock state: the
        /// distressed note frees itself one covered Monday at a time
        /// (NoteWeeksLeft at its own payment), so the boxes are its Mondays —
        /// done filled, the rest waiting. A note with no schedule (sharked,
        /// or a payment under water) has one box: repay it whole.
        static void UnlockChecklist(BinderScreen b, GameState state, float x, float y)
        {
            int idx = -1;
            for (int i = 0; i < state.Loans.Count; i++)
                if (state.Loans[i].Missed >= 2 && state.Loans[i].Balance > 0) { idx = i; break; }
            if (idx < 0) return;
            Loan nd = state.Loans[idx];
            int bal = nd.Balance;
            int left = SimBank.NoteWeeksLeft(bal, nd.RateWk, nd.PayWk);
            if (nd.PayWk <= 0 || left < 0)
            {
                DeskKit.Pips(b, x, y + 6f, 0, 1);
                DeskKit.FitLine(b, "the unlock: repay the collectors in full — $"
                    + GameUi.Money(bal) + ", the only door", x + 40f, y + 2f,
                    DeskKit.Detail, DrawnUI.Ink, 900f);
                return;
            }
            int done = Math.Max(nd.TermWk - left, 0);
            int total = done + left;
            int boxes = Math.Min(total, 12);
            DeskKit.Pips(b, x, y + 6f,
                total > 12 ? Mathf.RoundToInt((float)done / Math.Max(total, 1) * boxes) : done,
                boxes);
            DeskKit.FitLine(b, "the unlock: " + done + " clean Monday" + (done == 1 ? "" : "s")
                + " of " + total + " — $" + GameUi.Money(nd.PayWk) + " each, none missed",
                x + boxes * 21f + 18f, y + 2f, DeskKit.Detail, DrawnUI.Ink, 700f);
        }

        /// Zone 2 — every note cut open + the refinance tail line.
        static float ZoneOwed(BinderScreen b, GameState state, float y)
        {
            List<LiveNote> notes = LiveNotes(state);
            int shown = Math.Min(notes.Count, NotesMax);
            const float CardH = 122f;
            float h = 78f + (shown > 0 ? CardH + 24f + 8f : 34f);
            var z = DeskKit.Zone(b, SheetX, y, ZoneW, h, 2, "what you owe",
                "— a loan is a bar you are painting over; watch which loans never shrink");
            float cx = z.ContentX;
            float cy = z.ContentY;
            if (notes.Count == 0)
            {
                b.L("you owe nobody anything. rare, and worth noticing — debt buys time, and time is what a runway is made of.",
                    cx, cy, DeskKit.Detail, Ink(0.6f), 1060f);
                return y + h + 4f;
            }
            for (int k = 0; k < shown; k++)
                NoteCard(b, state, notes[k], cx + k * 552f, cy - 6f, 532f, CardH);
            TailLine(b, state, notes.Count - shown, cx, cy + CardH + 6f);
            return y + h + 4f;
        }

        static void NoteCard(BinderScreen b, GameState state, LiveNote n, float x, float y,
                             float w, float h)
        {
            int idx = n.Idx;
            Loan note = n.Note;
            string kind = note.Kind ?? "shark";
            int bal = note.Balance;
            int principal = Math.Max(note.Principal > 0 ? note.Principal : bal, 1);
            double rate = note.RateWk;
            int interest = (int)Math.Ceiling(bal * rate);
            string title;
            string chip;
            switch (kind)
            {
                case "bank": title = "bank note — term"; chip = "shrinks as you pay"; break;
                case "venture": title = "venture note — interest only"; chip = "never shrinks"; break;
                default: title = "the shark — interest only"; chip = "feeds first"; break;
            }
            var frame = DeskKit.CardFrame(b, x, y, w, h, title);
            // S2b — the card is a landing pad: bills' interest row and the
            // pre-roll arrive here spotlit ("note_<i>"; legacy shark = -1)
            b.MarkControl("note_" + idx, new Rect(x, y, w, h));
            float cx = frame.ContentX;
            float cy = frame.ContentY;
            b.L(chip, x + w - 176f, y + 14f, 15f, kind == "bank" ? Pos : DrawnUI.Coral, 162f);
            int paid = Math.Max(principal - bal, 0);
            DeskKit.Meter(b, cx, cy, w - 240f, (float)paid / Math.Max(principal, bal),
                kind == "bank" ? DrawnUI.Sage : DrawnUI.Coral);
            b.L("paid off $" + GameUi.Money(paid), cx, cy + 24f, 15f, Ink(0.55f), 200f);
            b.L("still owe $" + GameUi.Money(bal), cx + 210f, cy + 24f, 15f, DrawnUI.Ink, 200f);
            // the Monday split, one compact line — the lesson in the arithmetic
            string split;
            switch (kind)
            {
                case "bank":
                {
                    int pay = Math.Min(note.PayWk, bal + interest);
                    int down = Math.Max(pay - interest, 0);
                    int left = SimBank.NoteWeeksLeft(bal, rate, note.PayWk);
                    split = "$" + GameUi.Money(pay) + "/Mon = $" + GameUi.Money(down)
                        + " down + $" + GameUi.Money(interest) + " fee · "
                        + (left >= 0 ? left + " left" : "no end");
                    break;
                }
                case "venture":
                {
                    int toBalloon = Math.Max(note.TakenWeek + note.TermWk - state.Week, 0);
                    split = "$" + GameUi.Money(interest) + "/Mon all fee · balloon $"
                        + GameUi.Money(bal) + " in " + toBalloon + " wks";
                    break;
                }
                default:
                    split = "$" + GameUi.Money(interest) + "/wk in fees — claws above $"
                        + GameUi.Money(SimBank.ClawTrigger);
                    break;
            }
            // the attention token leads — a trimmed tail never hides a missed Monday
            if (note.Missed > 0) split = "missed " + note.Missed + " · " + split;
            // the split line keeps its own lane: it ends before the repay arm
            // and trims with an ellipsis instead of printing under it
            int quote = Math.Min(state.Cash - GameState.RAMEN_PER_WEEK, bal);
            bool hasArm = idx >= 0 && quote > 0;
            TextMeshProUGUI sl = b.L(split, cx, cy + 42f, 15f,
                (kind != "bank" || note.Missed > 0) ? DrawnUI.Coral : Ink(0.7f),
                hasArm ? (w - 36f - 212f) : (w - 36f));
            sl.enableWordWrapping = false;
            sl.overflowMode = TextOverflowModes.Ellipsis;
            if (hasArm)
            {
                int fireIdx = idx;
                DeskKit.Arm(b, "repay_" + idx, "repay ▸", "−$" + GameUi.Money(quote) + " now — sure?",
                    x + w - 216f, cy + 34f, () => SimBank.RepayNote(state, fireIdx), 200f, 17f);
            }
        }

        /// The line under the cards: filed-note count + THE REFINANCE row.
        static void TailLine(BinderScreen b, GameState state, int hidden, float x, float y)
        {
            var parts = new List<string>();
            if (hidden > 0)
                parts.Add(hidden + " more note" + (hidden == 1 ? "" : "s") + " filed");
            int idx = RefiNote(state);
            if (idx >= 0)
            {
                Loan note = state.Loans[idx];
                int bal = note.Balance;
                double oldRate = note.RateWk;
                double newRate = SimBank.BankRateWk(state);
                int fee = PriceBookInt(state, "refinance_break_fee", 350);
                if (newRate >= oldRate)
                    parts.Add("refinance: today's " + (newRate * 100.0).ToString("0.0")
                        + "% beats nothing against the " + (oldRate * 100.0).ToString("0.0") + "% note");
                else
                {
                    int rem = SimBank.NoteWeeksLeft(bal, oldRate, note.PayWk);
                    int newPay = SimBank.LoanPaymentWk(bal, newRate, Math.Max(rem, 4));
                    parts.Add("refinance: swap " + (oldRate * 100.0).ToString("0.0") + "% for "
                        + (newRate * 100.0).ToString("0.0") + "% — fee $" + GameUi.Money(fee)
                        + " · $" + GameUi.Money(note.PayWk) + " -> $" + GameUi.Money(newPay) + "/Mon"
                        + (RefiWired ? "" : " · papers arrive with the works wave"));
                }
            }
            if (parts.Count == 0) return;
            b.L(string.Join(" · ", parts), x, y, DeskKit.Law, Ink(0.42f), 1060f);
        }

        /// Zone 3 — new money: the truth printed before the pen moves.
        static float ZoneNewMoney(BinderScreen b, GameState state, float y, double rate)
        {
            bool locked = SimBank.CreditLocked(state);
            int headroom = SimBank.BorrowHeadroom(state);
            bool garage = state.EraIndex() < 1;
            bool dead = garage || locked || headroom < SimBank.MinDraw;
            float h = dead ? 124f : 240f;
            var z = DeskKit.Zone(b, SheetX, y, ZoneW, h, 3, "new money",
                "— before you sign, the receipt shows what the money truly costs");
            float cx = z.ContentX;
            float cy = z.ContentY;
            if (dead)
            {
                b.L(DeadReason(garage, locked), cx, cy, DeskKit.Detail, Ink(0.6f), 1060f);
                return y + h + 4f;
            }
            int floorAmt = Math.Max(headroom, SimBank.MinDraw);
            int borrow = Gd.Clampi(DInt(b, "borrow", Math.Min(10000, floorAmt)),
                SimBank.MinDraw, floorAmt);
            int[] terms = SimBank.TermOptions(state, "bank");
            int term = DInt(b, "term", terms[Math.Min(1, terms.Length - 1)]);
            if (Array.IndexOf(terms, term) < 0) term = terms[0];
            var borrowSteps = new List<double>();
            foreach (int s in SimBank.BorrowSteps) borrowSteps.Add(s);
            var termSteps = new List<double>();
            foreach (int t in terms) termSteps.Add(t);
            // every stepper press re-inks THE RECEIPT (S4): the flag rides
            // the desk dict through the refresh, and the redraw dips its ink
            MoneyLine(b, cx, cy, "borrow", "$" + GameUi.Money(borrow),
                () => { b.Desk["borrow"] = Gd.Clampi((int)DeskKit.Ladder(borrowSteps, borrow, -1),
                    SimBank.MinDraw, floorAmt); b.Desk["flick"] = true; },
                () => { b.Desk["borrow"] = Gd.Clampi((int)DeskKit.Ladder(borrowSteps, borrow, 1),
                    SimBank.MinDraw, floorAmt); b.Desk["flick"] = true; },
                borrow <= SimBank.MinDraw, borrow >= headroom);
            // S2b — the borrow stepper is a landing pad ("borrow")
            b.MarkControl("borrow", new Rect(cx - 8f, cy - 6f, 560f, 46f));
            MoneyLine(b, cx, cy + 40f, "pay it back over", term + " weeks",
                () => { b.Desk["term"] = (int)DeskKit.Ladder(termSteps, term, -1);
                    b.Desk["flick"] = true; },
                () => { b.Desk["term"] = (int)DeskKit.Ladder(termSteps, term, 1);
                    b.Desk["flick"] = true; },
                term <= terms[0], term >= terms[terms.Length - 1]);
            b.L("at your rate  " + (rate * 100.0).ToString("0.0") + "%/wk — set by your standing",
                cx, cy + 82f, DeskKit.Law, Ink(0.6f), 520f);
            int vcap = Math.Min(SimBank.VentureCap(state), headroom);
            if (vcap >= SimBank.MinDraw)
            {
                double vrate = SimBank.VentureRateWk(state);
                DeskKit.Arm(b, "venture", "or venture debt: $" + GameUi.Money(vcap) + " at "
                    + (vrate * 100.0).ToString("0.0") + "% — take it ▸",
                    "interest-only · balloon in " + SimBank.TermsVenture[0] + " wks · "
                    + SimBank.WarrantPct.ToString("0.00") + "% warrants — sure?",
                    cx, cy + 108f, () => SimBank.SignNote(state, "venture", vcap,
                        SimBank.TermsVenture[0]), 520f, 17f);
            }
            // THE RECEIPT, compact: the three numbers and the pen
            int pay2 = SimBank.LoanPaymentWk(borrow, rate, term);
            int allIn = pay2 * term;
            float rx = cx + 620f;
            const float RcptW = 420f;
            var inked = new List<TextMeshProUGUI>();
            inked.Add(b.L("THE RECEIPT — shorter term: smaller price, heavier Mondays",
                rx, cy - 6f, 15f, Ink(0.5f), RcptW));
            inked.AddRange(RcptRow(b, rx, cy + 16f, RcptW, "every Monday",
                "$" + GameUi.Money(pay2), DrawnUI.Ink));
            inked.AddRange(RcptRow(b, rx, cy + 40f, RcptW, "you will hand back, in all",
                "$" + GameUi.Money(allIn), DrawnUI.Ink));
            DeskKit.PenRule(b, cy + 64f, rx, RcptW, Ink(0.8f));
            DeskKit.PenRule(b, cy + 68f, rx, RcptW, Ink(0.8f));
            inked.AddRange(RcptRow(b, rx, cy + 74f, RcptW, "THE PRICE OF THE MONEY",
                "$" + GameUi.Money(Math.Max(allIn - borrow, 0)), DrawnUI.Coral));
            // THE PEN FLICK (S4): a stepper press just rewrote the numbers —
            // the ink dips and settles, so the re-print is FELT, not inferred
            object flick;
            if (b.Desk.TryGetValue("flick", out flick) && flick is bool && (bool)flick)
            {
                b.Desk.Remove("flick");
                b.StartCoroutine(ReInk(inked));
            }
            Button sign = DeskKit.Word(b, "[ SIGN FOR IT ]", rx + 100f, cy + 104f, null,
                DeskKit.Detail, DrawnUI.Ink, 220f);
            sign.onClick.AddListener(() => DeskKit.SignStroke(b, sign, "[ SIGN FOR IT ]",
                rx + 100f, cy + 104f, () =>
                {
                    SimBank.SignNote(state, "bank", borrow, term);
                    b.Desk.Remove("borrow");
                    b.Refresh();
                }));
            return y + h + 4f;
        }

        /// One receipt row; returns its labels so the pen flick can re-ink.
        static List<TextMeshProUGUI> RcptRow(BinderScreen b, float x, float y, float w,
                                             string label, string val, Color col)
        {
            var l = b.L(label, x, y, 17f, Ink(0.85f), w - 120f);
            var v = b.L(val, x, y, 18f, col, w);
            v.alignment = TMPro.TextAlignmentOptions.TopRight;
            return new List<TextMeshProUGUI> { l, v };
        }

        /// The pen dips (alpha 0.25) and settles to full over ~0.18s.
        static System.Collections.IEnumerator ReInk(List<TextMeshProUGUI> labels)
        {
            foreach (TextMeshProUGUI l in labels)
                if (l != null) l.alpha = 0.25f;
            float t = 0f;
            while (t < 0.18f)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Lerp(0.25f, 1f, Mathf.Clamp01(t / 0.18f));
                foreach (TextMeshProUGUI l in labels)
                    if (l != null) l.alpha = a;
                yield return null;
            }
            foreach (TextMeshProUGUI l in labels)
                if (l != null) l.alpha = 1f;
        }

        static string DeadReason(bool garage, bool locked)
        {
            if (locked) return "the bank won't quote — clear the collectors first.";
            if (garage) return "no bank answers a garage — the shark (18%/wk) is the only desperate door.";
            return "no revenue, no line — a bank lends against what customers already pay you.";
        }

        static void MoneyLine(BinderScreen b, float x, float y, string label, string value,
                              Action onMinus, Action onPlus, bool atMin, bool atMax)
        {
            b.L(label, x, y + 4f, DeskKit.Detail, Ink(0.85f), 200f);
            b.L(value, x + 210f, y, DeskKit.Row, DrawnUI.Ink, 210f);
            DeskKit.AdjustPair(b, x + 440f, y + 4f, onMinus, onPlus, atMin, atMax);
        }

        /// The folded bars — zones 3 and 4 swap which of them is open.
        static float Zone3Bar(BinderScreen b, GameState state, float y, double rate)
        {
            bool locked = SimBank.CreditLocked(state);
            bool garage = state.EraIndex() < 1;
            int headroom = SimBank.BorrowHeadroom(state);
            string text;
            if (garage || locked || headroom < SimBank.MinDraw)
                text = "3 · NEW MONEY — " + DeadReason(garage, locked).TrimEnd('.') + " ▸";
            else
            {
                int borrow = Gd.Clampi(DInt(b, "borrow", 10000), SimBank.MinDraw,
                    Math.Max(headroom, SimBank.MinDraw));
                int[] terms = SimBank.TermOptions(state, "bank");
                int term = DInt(b, "term", terms[Math.Min(1, terms.Length - 1)]);
                if (Array.IndexOf(terms, term) < 0) term = terms[0];
                text = "3 · NEW MONEY — borrow $" + GameUi.Money(borrow) + " over " + term
                    + " weeks -> $" + GameUi.Money(SimBank.LoanPaymentWk(borrow, rate, term))
                    + " every Monday ▸";
            }
            return Bar(b, y, text, () => { b.Desk["zone4"] = false; });
        }

        /// Compact on purpose: this bar shares the lower page with the DO
        /// lane (S3, right-aligned at 762) — a short door never runs under
        /// the lane's buttons; the ladder's story waits inside the zone.
        static float Zone4Bar(BinderScreen b, float y)
        {
            DeskKit.PenRule(b, y + 2f, SheetX, ZoneW, Ink(0.2f));
            DeskKit.Word(b, "4 · IF A MONDAY IS MISSED — the three stairs ▸",
                SheetX + 8f, y + 8f, () => { b.Desk["zone4"] = true; }, 19f, Ink(0.7f), 330f);
            return y + 42f;
        }

        static float Bar(BinderScreen b, float y, string text, Action onPress)
        {
            DeskKit.PenRule(b, y + 2f, SheetX, ZoneW, Ink(0.2f));
            DeskKit.Word(b, text, SheetX + 8f, y + 8f, onPress, 19f, Ink(0.7f), ZoneW - 16f);
            return y + 42f;
        }

        /// Zone 4 — the engine's own miss ladder (SimBank's miss), as stairs.
        static float ZoneStairs(BinderScreen b, GameState state, float y)
        {
            const float H = 182f;
            var z = DeskKit.Zone(b, SheetX, y, ZoneW, H, 4, "if a monday is missed",
                "— the ladder is written into every loan; read it before you need it");
            float cx = z.ContentX;
            float baseY = z.Bottom - 12f;
            float sw = (ZoneW - CardGap * 2f - 36f) / 3f;
            Stair(b, cx, baseY, sw, 56f, "1st miss",
                "the balance grows — unpaid interest joins the debt", 0);
            Stair(b, cx + sw + 6f, baseY, sw, 72f, "2nd miss",
                "repriced +2%/wk — and the bank stops answering", 1);
            Stair(b, cx + (sw + 6f) * 2f, baseY, sw, 88f, "3rd miss",
                "sold to the collectors — 18%/wk, the shark's price", 2);
            return y + H + 4f;
        }

        static void Stair(BinderScreen b, float x, float baseY, float w, float h,
                          string head, string line, int i)
        {
            float y = baseY - h;
            DrawnUI.Fill(b.Content, "stair", StairCols[i], x, y, w, h).raycastTarget = false;
            DrawnUI.Fill(b.Content, "se", DrawnUI.Ink, x, y, w, 2.6f).raycastTarget = false;
            DrawnUI.Fill(b.Content, "se", DrawnUI.Ink, x, y + h - 2.6f, w, 2.6f).raycastTarget = false;
            DrawnUI.Fill(b.Content, "se", DrawnUI.Ink, x, y, 2.6f, h).raycastTarget = false;
            DrawnUI.Fill(b.Content, "se", DrawnUI.Ink, x + w - 2.6f, y, 2.6f, h).raycastTarget = false;
            Color ink = i == 2 ? Color.white : DrawnUI.Ink;
            b.L(head, x + 10f, y + 2f, 18f, ink, w - 20f);
            b.L(line, x + 10f, y + 24f, 13f,
                i == 2 ? new Color(1f, 1f, 1f, 0.9f) : Ink(0.6f), w - 20f);
        }

        /// The cash-ahead strip: the forecast's own cells, before surprises.
        static void ForecastStrip(BinderScreen b, GameState state, float y)
        {
            List<SimBank.ForecastWeek> rows = SimBank.ForecastCash(state, SimBank.ForecastWeeks);
            // the strip yields to the DO lane's slot (S3) — in deep stacks it
            // waits for a shallower week rather than printing under buttons
            if (rows.Count == 0 || y + 50f > Mathf.Min(YRules - 6f, DeskKit.DoLaneY - 8f)) return;
            b.L("cash ahead, if nothing changes:", SheetX, y + 12f, DeskKit.Law, Ink(0.6f), 240f);
            float x = SheetX + 250f;
            foreach (SimBank.ForecastWeek r in rows)
            {
                DeskKit.CardFrame(b, x, y, 128f, 48f, "");
                b.L("wk " + r.Wk, x + 10f, y + 2f, 14f, Ink(0.5f), 108f);
                int c = r.Cash;
                string txt = c < 0 ? "−$" + (Math.Abs((double)c) / 1000.0).ToString("0.0") + "k"
                                   : "$" + (c / 1000.0).ToString("0.0") + "k";
                b.L(txt, x + 10f, y + 18f, 19f, c < 0 ? DrawnUI.Coral : DrawnUI.Ink, 108f);
                x += 140f;
            }
            b.L("before surprises", x + 10f, y + 14f, DeskKit.Law, Ink(0.42f), 200f);
        }

        /// One line at the page foot: the warning outranks the laws.
        static void Foot(BinderScreen b, GameState state)
        {
            string warning = "";
            if (SimBank.CreditLocked(state))
                warning = "a note is in default: the collectors are calling and investors do check your credit";
            else if (SimBank.DebtServiceWk(state) > 0
                     && state.Cash < 2 * SimBank.DebtServiceWk(state))
                warning = "the repayment cliff: $" + GameUi.Money(SimBank.DebtServiceWk(state))
                    + " a week is due and there is $" + GameUi.Money(state.Cash) + " in the bank";
            if (warning != "")
                b.L(warning, SheetX, YRules, DeskKit.Law, DrawnUI.Coral, 1100f);
            else
                b.L("a loan is rented money — you pay for the time, not the amount · interest bills sold or not · repaying early is the only discount",
                    SheetX, YRules, DeskKit.Law, Ink(0.5f), 1100f);
        }

        // ══════════════════════════ THE BOOKS ═══════════════════════════════

        /// The full statement on THE LEDGER SHEET: three bands, every nonzero
        /// lane a row; the crowd beyond the page's budget folds into one
        /// honest row; NET double-ruled = the engine's own identity.
        static void DrawBooks(BinderScreen b, GameState state)
        {
            DeskKit.Title(b, "the books — last week, line by line");
            DeskKit.Back(b, "back to the meeting", () => { b.Desk["mode"] = ""; }, 880f, 16f);
            Pnl pnl = state.LastPnl;
            if (pnl == null)
            {
                DeskKit.Empty(b, SheetX, 120f,
                    "no week has closed yet — the books open after the first LOCK IN.",
                    "a P&L is a record of what happened, and nothing has.");
                return;
            }
            var sheet = DeskKit.LedgerSheet(b, SheetX, 64f, ZoneW, new List<DeskKit.LedgerCol>
            {
                new DeskKit.LedgerCol { Label = "the week's books", W = 560f },
                new DeskKit.LedgerCol { Label = "$/wk", W = 150f, Align = "right" },
                new DeskKit.LedgerCol { Label = "note", W = 330f },
            }, 1, false, "all figures $/week");
            int revenue = pnl.Revenue;
            DeskKit.LedgerSection(b, sheet, "money in");
            DeskKit.LedgerRow(b, sheet, new[] { state.Traction + " customers paid",
                "$" + GameUi.Money(revenue),
                pnl.Learning < 0.995 ? "learning ×" + pnl.Learning.ToString("0.00")
                    + " — scale earns its margin" : "" }, new DeskKit.LedgerRowCfg());
            // ── the operation: every nonzero lane
            var op = new List<object[]>();
            Op(op, "serving the customers (cogs)", pnl.Cogs, "");
            Op(op, "built in-house", pnl.Production, "hardware pays at the bench");
            Op(op, "bought outside", pnl.Subcontract, "");
            Op(op, "rent", pnl.Rent, "");
            Op(op, "site rents", pnl.SiteRent, "the other roofs");
            Op(op, "payroll", pnl.Payroll, "");
            Op(op, "infra", pnl.Infra, "");
            Op(op, "the catalog's tools", pnl.OfferFixed, "");
            Op(op, "machine upkeep + carrying", pnl.EquipUpkeep + pnl.Carrying, "");
            Op(op, "marketing — the mix", pnl.Marketing, "-> growth");
            Op(op, "sales · care · rnd · office",
                pnl.Sales + pnl.Care + pnl.Rnd + pnl.Office, "-> spend");
            Op(op, "the unforeseen", pnl.Incident, "nobody planned it");
            Op(op, "severance", pnl.Severance, "always owed");
            Op(op, "recruiting + adverts", pnl.Recruiting + pnl.RecruitAds, "");
            Op(op, "relief valves", pnl.Relief, "overflow served outside");
            int rowsSum = 0;
            foreach (object[] r in op) rowsSum += (int)r[1];
            int burn = pnl.Burn;
            if (burn != rowsSum)
                Op(op, "the world's overhead multiplier", burn - rowsSum, "the era taxes every line");
            Op(op, "standing costs", pnl.LiabilitiesWk, "they run out, slowly");
            DeskKit.LedgerSection(b, sheet, "money out — the operation");
            if (op.Count > 9)
            {
                int rest = 0;
                for (int j = 8; j < op.Count; j++) rest += (int)op[j][1];
                op = op.GetRange(0, 8);
                op.Add(new object[] { "the smaller lines, together", rest,
                    "each one still on the receipts" });
            }
            foreach (object[] r in op)
                DeskKit.LedgerRow(b, sheet, new[] { (string)r[0],
                    "$" + GameUi.Money((int)r[1]), (string)r[2] }, new DeskKit.LedgerRowCfg());
            DeskKit.LedgerSection(b, sheet, "the bank & the state");
            if (pnl.Interest != 0)
                DeskKit.LedgerRow(b, sheet, new[] { "interest — the cost of debt",
                    "$" + GameUi.Money(pnl.Interest), "outside burn, on purpose" },
                    new DeskKit.LedgerRowCfg());
            if (pnl.Tax != 0)
                DeskKit.LedgerRow(b, sheet, new[] { "the taxman", "$" + GameUi.Money(pnl.Tax),
                    "20% of profit, after interest" }, new DeskKit.LedgerRowCfg());
            int outTotal = burn + pnl.LiabilitiesWk + pnl.Interest + pnl.Tax;
            DeskKit.LedgerSubtotal(b, sheet, "subtotal — out", "$" + GameUi.Money(outTotal));
            int net = pnl.Net;
            DeskKit.LedgerTotal(b, sheet, "net, the week",
                (net >= 0 ? "+$" : "−$") + GameUi.Money(Math.Abs(net)),
                net >= 0 ? DrawnUI.Sage : DrawnUI.Coral);
            int rw = SimEngine.RunwayWeeks(state);
            DeskKit.LedgerMemo(b, sheet, "cash $" + GameUi.Money(state.Cash) + " · runway at this net",
                rw < 999 ? rw + " wks" : "gaining", "");
            SecondMemo(b, sheet, state);
            DeskKit.LedgerEnd(b, sheet);
            b.L("read it top to bottom: in, the operation, the bank and the state — interest and tax sit outside burn, the real P&L shape",
                SheetX, YRules, DeskKit.Law, Ink(0.5f), 1100f);
        }

        static void Op(List<object[]> op, string label, int amount, string note)
        {
            if (amount != 0) op.Add(new object[] { label, amount, note });
        }

        /// The second memo slot, by teaching priority: principal -> NOL ->
        /// net-30 -> break-even.
        static void SecondMemo(BinderScreen b, DeskKit.LedgerBox sheet, GameState state)
        {
            int principal = (int)state.GetMetaF("bank_principal_wk", 0.0);
            if (principal > 0)
            {
                DeskKit.LedgerMemo(b, sheet, "principal paid $" + GameUi.Money(principal), "",
                    "a balance-sheet move, not a cost");
                return;
            }
            if (state.TaxLossCarry > 0)
            {
                DeskKit.LedgerMemo(b, sheet, "losses carried forward",
                    "$" + GameUi.Money(state.TaxLossCarry),
                    "they shelter the next profits before the taxman sees them");
                return;
            }
            int owed = 0;
            foreach (Commitment r in state.Receivables) owed += r.CashWk;
            if (owed > 0)
            {
                DeskKit.LedgerMemo(b, sheet, "net-30 float", "$" + GameUi.Money(owed),
                    "invoiced, not yet in the bank — profit is not cash");
                return;
            }
            int be = SimBank.BreakEvenCustomers(state);
            if (be > 0)
                DeskKit.LedgerMemo(b, sheet, "break-even " + be + " customers",
                    state.Traction + " now", "each contributes $"
                    + SimBank.ContributionMargin(state).ToString("0.0") + "/wk");
        }

        // ── shared reads ─────────────────────────────────────────────────────

        static List<LiveNote> LiveNotes(GameState state)
        {
            var outN = new List<LiveNote>();
            if (state.LoanPrincipal > 0)
                outN.Add(new LiveNote { Idx = -1, Note = new Loan { Kind = "shark",
                    Principal = state.LoanPrincipal, Balance = state.LoanPrincipal,
                    RateWk = SimBank.SharkRate, PayWk = 0, TermWk = 0,
                    TakenWeek = state.Week, Missed = 0 } });
            for (int i = 0; i < state.Loans.Count; i++)
                if (state.Loans[i].Balance > 0)
                    outN.Add(new LiveNote { Idx = i, Note = state.Loans[i] });
            return outN;
        }

        static int RefiNote(GameState state)
        {
            for (int i = 0; i < state.Loans.Count; i++)
                if ((state.Loans[i].Kind ?? "") == "bank" && state.Loans[i].Balance > 0)
                    return i;
            return -1;
        }

        static int MondayOut(GameState state)
        {
            int total = SimBank.DebtServiceWk(state);
            if (state.LoanPrincipal > 0)
                total += (int)Math.Ceiling(state.LoanPrincipal * SimBank.SharkRate);
            foreach (Loan l in state.Loans)
                if ((l.Kind ?? "") == "shark" && l.Balance > 0)
                    total += (int)Math.Ceiling(l.Balance * l.RateWk);
            return total;
        }

        static string HeroSentence(GameState state)
        {
            int shrinking = 0;
            int frozen = 0;
            foreach (LiveNote n in LiveNotes(state))
            {
                if ((n.Note.Kind ?? "") == "bank") shrinking += 1;
                else frozen += 1;
            }
            if (shrinking + frozen == 0)
                return "no debt on the books — the credit line below is what the bank would answer.";
            if (shrinking > 0 && frozen > 0)
                return "some of this shrinks as you pay it; some never will. zone 2 shows which.";
            if (frozen > 0)
                return "interest-only money: the Mondays buy time, never the debt itself.";
            return "amortizing money: every Monday buys a little more of the debt back.";
        }

        static string DeadlineText(GameState state)
        {
            foreach (Loan l in state.Loans)
            {
                if (l.Balance <= 0) continue;
                if ((l.Kind ?? "") == "venture")
                {
                    int toBalloon = l.TakenWeek + l.TermWk - state.Week;
                    if (toBalloon <= 3)
                        return "balloon due in " + Math.Max(toBalloon, 0) + " wk"
                               + (toBalloon == 1 ? "" : "s");
                }
                if ((l.Kind ?? "") == "shark" || l.Missed >= 1) return "the shark feeds first";
            }
            if (state.LoanPrincipal > 0) return "the shark feeds first";
            return "";
        }

        static int PriceBookInt(GameState state, string key, int dflt)
        {
            if (state.PriceBook == null) return dflt;
            object v;
            if (!state.PriceBook.TryGetValue(key, out v) || v == null) return dflt;
            try { return Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture); }
            catch { return dflt; }
        }

        static string DStr(BinderScreen b, string key)
        {
            object v;
            return b.Desk.TryGetValue(key, out v) && v != null ? v.ToString() : "";
        }

        static bool DBool(BinderScreen b, string key)
        {
            object v;
            return b.Desk.TryGetValue(key, out v) && v is bool && (bool)v;
        }

        static int DInt(BinderScreen b, string key, int dflt)
        {
            object v;
            if (!b.Desk.TryGetValue(key, out v) || v == null) return dflt;
            try { return Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture); }
            catch { return dflt; }
        }

        static Color Ink(float a) { return DrawnUI.WithAlpha(DrawnUI.Ink, a); }

        public static void Handle(BinderScreen b, string id)
        {
            // every control on this desk carries its own closure
        }
    }
}
