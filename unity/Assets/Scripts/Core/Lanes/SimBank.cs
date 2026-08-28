using System;
using System.Collections.Generic;
using System.Globalization;

namespace Runway.Core
{
    /// <summary>
    /// LANE 06 — THE BANK &amp; THE STATE (credit, interest, tax). Spec: docs/design/06-finance.md
    ///
    /// Deliberate borrowing at health-priced terms, repayment control, profit
    /// tax, break-even and a cash forecast. Every mechanic here mirrors a real
    /// finance instrument, is called by its real name on the sheet, and every
    /// receipt says WHY — a loan payment is an expense AND a balance-sheet
    /// move, and the line that prints it says so.
    ///
    /// NOTHING HERE ROLLS DICE. The quote is a pure function of the books, so
    /// there is no salt to spend and nothing to reroll-scum.
    ///
    /// The spine calls, in tick order (00-spine section 1, HOOKS.md):
    ///   TickPre    tick 9 — notes settle before the money is assembled
    ///   TickMoney  the money section — the schedule, the miss ladder, the sweep
    ///   TaxWk      the state, charged last, on what is left after interest
    ///   TickPost   after the record — the taxman's receipt, net-30, break-even
    /// and outside the tick: Directives feeds the DM block, Attention feeds
    /// every bang in the game through SimEngine.AttentionItems.
    ///
    /// TWIN LAW: this file and game/src/core/lanes/sim_bank.gd carry the same
    /// logic in the same order. The engines do NOT share PRNG internals, so
    /// parity means same checks and same behaviour, never a byte-equal draw.
    /// </summary>
    public static class SimBank
    {
        // ── THE DEBT SEAM ────────────────────────────────────────────────
        /// <summary>
        /// FLIPPED. This lane owns every note now: the structured Loans list,
        /// honest risk-priced rates, level-payment amortization, the miss
        /// ladder. The engine's legacy shark block (18%/wk compounded,
        /// auto-repaid above $2,000) is retired by this constant, and a legacy
        /// LoanPrincipal folds into a shark note the first time the week ticks,
        /// so no save loses a dollar of debt.
        /// </summary>
        public const bool OwnsDebt = true;

        /// THE SHARK NEVER MOVES (existing pinned law): 18%/wk, health-blind,
        /// always available through the DM's take_loan, and it feeds first.
        public const double SharkRate = 0.18;
        public const double RateCap = 0.12;      // the desk can never touch the shark's price
        public const int MinDraw = 1000;         // below this the paperwork outweighs the money
        public const int ClawTrigger = 2000;     // the shark's auto-claw, unchanged from legacy
        public const int ClawKeep = 1500;
        public const double WarrantPct = 0.25;   // DECISIONS.md: venture debt nibbles the cap table
        public const double SweepRate = 0.001;   // 0.1%/wk ~ 5%/yr on idle cash, hq only
        public const int SweepFloor = 100000;
        public const double TaxRate = 0.20;
        public const int TaxEra = 2;             // office — below it the company is off the radar
        public const double ReceivableFrac = 0.25;
        public const int ReceivableWk = 4;
        public const int ForecastWeeks = 4;

        /// THE LADDERS the desk's steppers walk. The engine re-clamps every
        /// write, so the UI is never trusted with a bound.
        public static readonly int[] BorrowSteps = { 1000, 2000, 5000, 10000, 20000, 50000, 100000 };
        public static readonly int[] TermsEarly = { 4, 8 };
        public static readonly int[] TermsFull = { 4, 8, 12, 26 };
        public static readonly int[] TermsVenture = { 12, 26 };

        /// The taxman's receipt cannot be written where the tax is computed:
        /// TaxWk is handed the working record, not the week's report. So the
        /// slips wait here between the money section and TickPost, which owns
        /// the finished record and can read the charge back. Cleared each tick.
        static readonly List<string> _slips = new List<string>();

        // ══ READ HELPERS (pure) ══════════════════════════════════════════

        /// <summary>
        /// THE ONE DEBT READING anything is allowed to use — vitals, the DM,
        /// the desk. Sums the structured notes AND a legacy LoanPrincipal that
        /// has not met a tick yet, so a pre-migration save never reads as
        /// debt-free.
        /// </summary>
        public static int DebtTotal(GameState state)
        {
            int total = state.LoanPrincipal;
            foreach (Loan l in state.Loans) total += l.Balance;
            return total;
        }

        /// <summary>The worst rate on the books — what the vitals line names.</summary>
        public static double WorstRate(GameState state)
        {
            double worst = state.LoanPrincipal > 0 ? SharkRate : 0.0;
            foreach (Loan l in state.Loans)
                if (l.Balance > 0) worst = Gd.Maxf(worst, l.RateWk);
            return worst;
        }

        /// <summary>The floor under a quote: a small business pays a
        /// small-business premium until it has a balance sheet behind it.</summary>
        public static double EraRateFloor(GameState state)
        {
            return state.Era == "coworking" ? 0.04 : 0.02;
        }

        /// <summary>
        /// THE RISK-PRICED QUOTE. Real analogue: SMB lending priced off
        /// debt-service coverage and time in business — runway proxies default
        /// probability, a revenue slump proxies coverage, era proxies track
        /// record. Simplification drops: credit files, personal guarantees,
        /// collateral haircuts. Deliberately rng-free.
        /// </summary>
        public static double BankRateWk(GameState state)
        {
            int rw = SimEngine.RunwayWeeks(state);
            double health = Gd.Clampf((12.0 - rw) / 12.0, 0.0, 1.0);
            double slump = Gd.Clampf(-state.LastGrowth / 0.25, 0.0, 1.0);
            double rate = 0.03 + 0.07 * health + 0.02 * slump - 0.005 * state.EraIndex();
            return Gd.Clampf(rate, EraRateFloor(state), RateCap);
        }

        /// <summary>Venture debt carries a cheaper coupon because the lender
        /// takes warrants instead — and here it really does.</summary>
        public static double VentureRateWk(GameState state)
        {
            return Gd.Maxf(BankRateWk(state) - 0.01, 0.02);
        }

        /// <summary>The revenue expression RunwayWeeks uses, so the cap and the
        /// runway can never disagree about how big this company is.</summary>
        public static double RevWk(GameState state)
        {
            double a = SimEngine.OffersArpu(state);
            if (a < 0.0) a = state.Theta.ArpuWk * state.PriceMult;
            return state.Traction * a;
        }

        /// <summary>
        /// WHAT THE BANK WILL LEND AT ALL, before what you already owe. No bank
        /// answers a garage; a coworking line is sized off revenue alone; from
        /// the office up the era's own spend cap joins it as a balance-sheet proxy.
        /// </summary>
        public static int BorrowCap(GameState state)
        {
            double r = RevWk(state);
            switch (state.Era)
            {
                case "coworking":
                    return Gd.Clampi(Gd.ToInt(4.0 * r), 0, 10000);
                case "office":
                case "floor":
                    return Gd.Clampi(Gd.ToInt(8.0 * r + 0.25 * SimEngine.EraSpendCap(state.Era)), 0, 150000);
                case "hq":
                    return Gd.Clampi(Gd.ToInt((8.0 * r + 0.25 * SimEngine.EraSpendCap(state.Era)) * 1.5), 0, 500000);
            }
            return 0;
        }

        /// <summary>What is left of the line. Shark balances do not count
        /// against it — they are off-book by nature, which is most of what
        /// makes them a shark.</summary>
        public static int BorrowHeadroom(GameState state)
        {
            int used = 0;
            foreach (Loan l in state.Loans)
                if (l.Kind != "shark") used += l.Balance;
            return Gd.Maxi(BorrowCap(state) - used, 0);
        }

        /// <summary>Venture debt is sized off the last equity round (30% — the
        /// market's own heuristic), so it is a post-raise instrument by
        /// construction. Never raised, never available.</summary>
        public static int VentureCap(GameState state)
        {
            if (state.EraIndex() < 3) return 0;
            int used = 0;
            foreach (Loan l in state.Loans)
                if (l.Kind == "venture") used += l.Balance;
            return Gd.Maxi(Gd.ToInt(0.30 * state.LastRoundAmount) - used, 0);
        }

        /// <summary>The terms this era's paper comes in.</summary>
        public static int[] TermOptions(GameState state, string kind = "bank")
        {
            if (kind == "venture") return TermsVenture;
            return state.EraIndex() <= 1 ? TermsEarly : TermsFull;
        }

        /// <summary>
        /// THE LEVEL-PAYMENT ANNUITY — the standard installment loan, and the
        /// whole amortization lesson in one line. Real analogue: a
        /// fixed-payment term note. Simplification drops: day-count conventions
        /// and origination fees (the weekly tick IS the period).
        /// </summary>
        public static int LoanPaymentWk(int principal, double rate, int term)
        {
            if (principal <= 0) return 0;
            if (term <= 0) return principal;
            if (rate <= 0.0) return (int)Math.Ceiling((double)principal / term);
            return (int)Math.Ceiling(principal * rate / (1.0 - Math.Pow(1.0 + rate, -(double)term)));
        }

        /// <summary>
        /// How many payments are left at THIS payment and THIS balance — the
        /// honest count, so a missed week visibly lengthens the note. -1 = the
        /// payment no longer covers the interest and the note never clears.
        /// </summary>
        public static int NoteWeeksLeft(int balance, double rate, int pay)
        {
            if (balance <= 0) return 0;
            if (pay <= 0) return -1;
            if (rate <= 0.0) return (int)Math.Ceiling((double)balance / pay);
            double owed = balance * rate;
            if (pay <= owed) return -1;
            return (int)Math.Ceiling(-Math.Log(1.0 - balance * rate / pay) / Math.Log(1.0 + rate));
        }

        /// <summary>
        /// CREDIT LOCK IS DERIVED, NEVER A FLAG: two misses on a live note and
        /// the bank stops answering. Self-healing — repay the distressed note
        /// and the lock lifts. Defaulting cannot launder it, because a sharked
        /// note keeps its missed count.
        /// </summary>
        public static bool CreditLocked(GameState state)
        {
            foreach (Loan l in state.Loans)
                if (l.Missed >= 2 && l.Balance > 0) return true;
            return false;
        }

        /// <summary>The weekly debt service the books are committed to: a bank
        /// note's level payment, a venture note's coupon. The shark is not a
        /// schedule, it is a claw, so it stays out of the fixed-cost reading.</summary>
        public static int DebtServiceWk(GameState state)
        {
            int total = 0;
            foreach (Loan l in state.Loans)
            {
                if (l.Balance <= 0) continue;
                if (l.Kind == "bank") total += Gd.Mini(l.PayWk, l.Balance);
                else if (l.Kind == "venture") total += (int)Math.Ceiling(l.Balance * l.RateWk);
            }
            return total;
        }

        static double StatusMult(GameState state, string key, int minWeeks = 1)
        {
            double mult = 1.0;
            foreach (Status s in state.Statuses)
            {
                if (s.WeeksLeft < minWeeks) continue;
                StatusDef d = SimEngine.StatusEffect(s.Name);
                switch (key)
                {
                    case "adopt_mult": mult *= d.AdoptMult; break;
                    case "churn_mult": mult *= d.ChurnMult; break;
                    default: mult *= d.ArpuMult; break;
                }
            }
            return mult;
        }

        static int Payroll(GameState state)
        {
            int p = 0;
            foreach (Employee e in state.Employees) p += e.Salary;
            foreach (PipelineHire h in state.Pipeline) p += h.Salary;   // paid before productive
            return p;
        }

        static double BudgetSum(GameState state)
        {
            return state.MarketingBudget + state.Budgets.Sum();
        }

        static int StandingLiab(GameState state, int minWeeks = 1)
        {
            int owed = 0;
            foreach (Commitment c in state.Commitments)
            {
                if (c.WeeksLeft < minWeeks) continue;
                owed += Gd.Absi(Gd.Mini(c.CashWk, 0));
            }
            return owed;
        }

        static int EraRent(GameState state)
        {
            int rent;
            return GameState.ERA_RENT.TryGetValue(state.Era, out rent) ? rent : 150;
        }

        /// <summary>
        /// TEXTBOOK CVP: how many customers the fixed costs need before the
        /// machine feeds itself. Real analogue: contribution-margin break-even.
        /// Simplification drops: incidents (noise — they live in the forecast)
        /// and tax (it scales after profit; break-even is pre-tax by
        /// definition). -1 = no count breaks even.
        /// </summary>
        public static int BreakEvenCustomers(GameState state)
        {
            double arpuR = SimEngine.OffersArpu(state);
            if (arpuR < 0.0) arpuR = state.Theta.ArpuWk * state.PriceMult;
            double arpu = arpuR * StatusMult(state, "arpu_mult");
            double burnMult = state.Theta.BurnMult;
            double varPc = SimEngine.OffersCogsPerCustomer(state) + 0.05 * burnMult;
            double margin = arpu - varPc;
            if (margin <= 0.0) return -1;
            double fixedWk = (EraRent(state) + Payroll(state) + 50 + BudgetSum(state)) * burnMult;
            fixedWk += SimEngine.OffersFixedWk(state);
            fixedWk += StandingLiab(state);
            fixedWk += DebtServiceWk(state);
            return (int)Math.Ceiling(fixedWk / margin);
        }

        /// <summary>What one customer contributes a week after the cost of
        /// serving them — the number break-even divides into.</summary>
        public static double ContributionMargin(GameState state)
        {
            double arpuR = SimEngine.OffersArpu(state);
            if (arpuR < 0.0) arpuR = state.Theta.ArpuWk * state.PriceMult;
            return arpuR * StatusMult(state, "arpu_mult")
                   - (SimEngine.OffersCogsPerCustomer(state) + 0.05 * state.Theta.BurnMult);
        }

        // ══ THE DESK'S WRITE PATH ════════════════════════════════════════

        /// <summary>
        /// SIGN A NOTE. The engine is the bouncer: the desk asks, this clamps,
        /// and a refusal comes back as null which the desk turns into a printed
        /// reason. Returns the note it wrote.
        /// </summary>
        public static Loan SignNote(GameState state, string kind, int amount, int term)
        {
            if (CreditLocked(state)) return null;
            double rate;
            int cap;
            int[] terms;
            if (kind == "venture")
            {
                if (state.EraIndex() < 3) return null;
                cap = Gd.Mini(VentureCap(state), BorrowHeadroom(state));
                rate = VentureRateWk(state);
                terms = TermsVenture;
            }
            else
            {
                kind = "bank";
                if (state.EraIndex() < 1) return null;
                cap = BorrowHeadroom(state);
                rate = BankRateWk(state);
                terms = TermOptions(state, "bank");
            }
            if (cap < MinDraw) return null;
            int draw = Gd.Clampi(amount, MinDraw, cap);
            int t = Array.IndexOf(terms, term) >= 0 ? term : terms[0];
            var note = new Loan
            {
                Kind = kind, Principal = draw, Balance = draw, RateWk = rate,
                TermWk = t, TakenWeek = state.Week,
                PayWk = kind == "venture" ? 0 : LoanPaymentWk(draw, rate, t),
                Missed = 0,
            };
            state.Loans.Add(note);
            state.Cash += draw;
            if (kind == "venture")
            {
                // THE WARRANT NIBBLE (DECISIONS.md): a cheaper coupon is never
                // free — the lender takes a slice of the company instead.
                state.DiluteAll(WarrantPct);
                state.LogAction(string.Format(CultureInfo.InvariantCulture,
                    "signed venture debt: +${0} at {1:0.0}%/wk, interest-only, balloon in {2} wks (warrants {3:0.00}%)",
                    draw, rate * 100.0, t, WarrantPct));
            }
            else
            {
                state.LogAction(string.Format(CultureInfo.InvariantCulture,
                    "signed a bank note: +${0} at {1:0.0}%/wk for {2} wks (${3}/wk)",
                    draw, rate * 100.0, t, note.PayWk));
            }
            return note;
        }

        /// <summary>
        /// EARLY REPAY — the prepayment reward is the interest you never pay.
        /// No penalty (simplification: prepayment penalties dropped; rare in
        /// small-business notes and pure friction here). The $500 ramen guard
        /// stands: the founder still eats. Returns what was actually paid.
        /// </summary>
        public static int RepayNote(GameState state, int idx)
        {
            if (idx < 0 || idx >= state.Loans.Count) return 0;
            Loan note = state.Loans[idx];
            int pay = Gd.Mini(state.Cash - GameState.RAMEN_PER_WEEK, note.Balance);
            if (pay <= 0) return 0;
            state.Cash -= pay;
            note.Balance -= pay;
            state.LogAction(string.Format(CultureInfo.InvariantCulture,
                "repaid ${0} of the {1} note", pay, note.Kind));
            if (note.Balance <= 0) state.Loans.RemoveAt(idx);
            return pay;
        }

        // ══ THE TICK ═════════════════════════════════════════════════════

        /// <summary>
        /// Tick 9, before the money is assembled: the legacy note joins the
        /// list. MIGRATION BY CONSTRUCTION — the engine is the only mutator, so
        /// this works headless and in both engines with no migrator code and no
        /// save bump. A $10,000 shark stays a $10,000 shark; only its shape changes.
        /// </summary>
        public static void TickPre(GameState state, WeeklyReport rep)
        {
            _slips.Clear();
            if (state.LoanPrincipal > 0)
            {
                state.Loans.Add(new Loan
                {
                    Kind = "shark", Principal = state.LoanPrincipal,
                    Balance = state.LoanPrincipal, RateWk = SharkRate, TermWk = 0,
                    TakenWeek = state.Week, PayWk = 0, Missed = 0,
                });
                state.LoanPrincipal = 0;
            }
        }

        /// <summary>
        /// The money section (9c). Every note accrues, pays what it can, and
        /// says so. Interest is ACCRUED, not paid — a missed week still bills
        /// the P&amp;L, because that is what accrual accounting means and the
        /// receipt says "owe", not "paid".
        /// </summary>
        public static void TickMoney(GameState state, WeeklyReport rep, MoneyWork m)
        {
            var kept = new List<Loan>();
            // PRINCIPAL IS NOT A P&L LANE — paying down a balance is a
            // balance-sheet move, not an expense, which is exactly why the
            // ledger prints it beside interest and tax rather than inside burn.
            // Carried as a meta because it describes the week, not the company.
            int principalWk = 0;
            foreach (Loan note in state.Loans)
            {
                int bal = note.Balance;
                if (bal <= 0) continue;
                double rate = note.RateWk;
                string kind = note.Kind;
                int interest = (int)Math.Ceiling(bal * rate);
                m.Interest += interest;
                if (kind == "shark")
                {
                    // THE SHARK'S CHARACTER, verbatim from the legacy block: it
                    // compounds whether you look or not, and it takes
                    // everything above walking-around money the moment there is any.
                    bal += interest;
                    note.Balance = bal;
                    rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                        "the loan compounds: +${0} interest (owe ${1})", interest, bal));
                    if (state.Cash > ClawTrigger)
                    {
                        int claw = Gd.Mini(state.Cash - ClawKeep, bal);
                        state.Cash -= claw;
                        bal -= claw;
                        principalWk += claw;
                        note.Balance = bal;
                        rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                            "auto-repaid ${0} of the loan", claw));
                    }
                    if (bal <= 0)
                        rep.Lines.Add("the shark is paid off — nothing feeds first any more");
                }
                else if (kind == "venture")
                {
                    // INTEREST-ONLY, THEN THE BALLOON — the real venture-debt shape.
                    int balloonWk = note.TakenWeek + note.TermWk;
                    if (state.Cash >= interest)
                    {
                        state.Cash -= interest;
                        rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                            "the venture note takes its coupon: −${0}, interest only — ${1} principal still waits",
                            interest, bal));
                    }
                    else
                    {
                        Miss(state, rep, note, "venture note", interest, interest);
                    }
                    bal = note.Balance;
                    if (state.Week >= balloonWk && bal > 0 && note.Kind == "venture")
                    {
                        if (state.Cash >= bal)
                        {
                            state.Cash -= bal;
                            principalWk += bal;
                            rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                                "the balloon landed and you covered it: −${0} — the venture note closes", bal));
                            bal = 0;
                            note.Balance = 0;
                        }
                        else
                        {
                            // THE WORKOUT (real distressed refi): the paper
                            // re-papers harder rather than the company dying of a date.
                            double wrate = Gd.Minf(rate + 0.02, RateCap);
                            note.Kind = "bank";
                            note.RateWk = wrate;
                            note.TermWk = 8;
                            note.TakenWeek = state.Week;
                            note.PayWk = LoanPaymentWk(bal, wrate, 8);
                            rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                                "the balloon came due with no cash behind it: the note re-papers at {0:0.0}%/wk over 8 wks — a workout, not a rescue",
                                wrate * 100.0));
                        }
                    }
                }
                else
                {
                    // THE AMORTIZED BANK NOTE. The split in the receipt IS the
                    // lesson: part of a payment is rent on the money, part of
                    // it is the money.
                    int payWk = note.PayWk;
                    int due = Gd.Mini(payWk, bal + interest);
                    if (payWk > 0 && state.Cash >= due)
                    {
                        state.Cash -= due;
                        int principalPaid = due - interest;
                        principalWk += Gd.Maxi(principalPaid, 0);
                        bal = bal + interest - due;
                        note.Balance = bal;
                        if (bal <= 0)
                        {
                            rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                                "the bank's draw: −${0} (${1} interest · ${2} principal) — the bank note is PAID, the folder closes",
                                due, interest, Gd.Maxi(principalPaid, 0)));
                        }
                        else
                        {
                            int left = NoteWeeksLeft(bal, rate, payWk);
                            rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                                "the bank's draw: −${0} (${1} interest · ${2} principal) — ${3} left, {4}",
                                due, interest, Gd.Maxi(principalPaid, 0), bal,
                                left >= 0 ? left + " wks" : "no end at this payment"));
                        }
                    }
                    else
                    {
                        Miss(state, rep, note, "bank", payWk > 0 ? due : interest, interest);
                    }
                }
                if (note.Balance > 0) kept.Add(note);
            }
            state.Loans = kept;
            state.SetMeta("bank_principal_wk", principalWk);
            // THE TREASURY SWEEP (hq): idle cash is not free money sitting
            // still, it is a money-market balance earning its keep. Credited
            // against the interest lane, because that lane is the cost of money
            // in both directions.
            if (state.Era == "hq" && state.Cash > SweepFloor)
            {
                int sweep = Gd.ToInt(SweepRate * (state.Cash - SweepFloor));
                if (sweep > 0)
                {
                    state.Cash += sweep;
                    m.Interest -= sweep;
                    rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                        "the sweep account pays ${0} on idle cash — money at rest still earns", sweep));
                }
            }
        }

        /// <summary>
        /// THE MISS LADDER. A payment cash cannot cover is SKIPPED, never drawn
        /// into the red — banks do not overdraw you, and rent and payroll
        /// already do. Real analogues in order: delinquency, default interest
        /// after a covenant breach, then a charged-off debt sold to collections.
        /// </summary>
        static void Miss(GameState state, WeeklyReport rep, Loan note, string what,
                         int due, int interest)
        {
            note.Missed += 1;
            note.Balance += interest;              // unpaid interest capitalizes
            state.Morale = Gd.Clampi(state.Morale - 3, 0, 100);
            rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                "MISSED the {0} (${1} due, ${2} in hand) — the balance grows",
                what, due, Gd.Maxi(state.Cash, 0)));
            if (note.Missed == 2)
            {
                double repriced = Gd.Minf(note.RateWk + 0.02, RateCap);
                note.RateWk = repriced;
                rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "the bank repriced the risk: {0:0.0}%/wk now — a covenant breach costs interest",
                    repriced * 100.0));
            }
            else if (note.Missed >= 3)
            {
                note.Kind = "shark";
                note.RateWk = SharkRate;
                note.PayWk = 0;
                note.TermWk = 0;
                SimEngine.AddStatus(state, "collections_calls", 4);
                rep.Lines.Add("sold to the collectors — 18%/wk now, and investors do check your credit");
            }
        }

        /// <summary>
        /// THE STATE, charged last. Corporate income tax on EBT — earnings
        /// BEFORE tax and AFTER interest, because interest is deductible on a
        /// real P&amp;L, and that ordering IS the lesson. Real analogue:
        /// estimated-tax prepayments. Simplification drops: quarterly filing
        /// (every other lane is weekly) and the 80% NOL offset limit.
        /// </summary>
        public static int TaxWk(GameState state, MoneyWork m)
        {
            if (state.EraIndex() < TaxEra) return 0;   // cash-basis, below the radar
            double netOps = m.Revenue - m.Burn - m.LiabilitiesWk;
            int ebt = Gd.RoundToInt(netOps - m.Interest);
            if (ebt < 0)
            {
                // THE LOSS CARRYFORWARD: without it one good week inside a
                // losing month pays tax while the company bleeds, which reads
                // as a bug to any founder.
                state.TaxLossCarry += -ebt;
                return 0;
            }
            int shelter = Gd.Mini(state.TaxLossCarry, ebt);
            state.TaxLossCarry -= shelter;
            int tax = Gd.RoundToInt(TaxRate * (ebt - shelter));
            if (shelter > 0)
                _slips.Add(string.Format(CultureInfo.InvariantCulture,
                    "old losses shelter ${0} of profit — no tax on that slice", shelter));
            if (tax <= 0) return 0;
            state.Cash -= tax;
            string line = string.Format(CultureInfo.InvariantCulture,
                "the taxman's cut: −${0} (20% of EBT ${1} — profit after interest)", tax, ebt - shelter);
            if (!state.HasFlag("tax_noticed"))
            {
                state.SetFlag("tax_noticed");
                line = "now you're on the radar: " + line;
            }
            _slips.Add(line);
            return tax;
        }

        /// <summary>
        /// After the record is written (9e/9f): the taxman's receipt reads the
        /// finished week back, the net-30 float moves, and the first break-even
        /// crossing gets the beat it deserves.
        /// </summary>
        public static void TickPost(GameState state, WeeklyReport rep)
        {
            for (int i = 0; i < _slips.Count; i++) rep.Lines.Add(_slips[i]);
            _slips.Clear();
            Pnl pnl = state.LastPnl;
            // 9e RECEIVABLES (floor+) — working-capital float, the net-30
            // reality of enterprise-scale revenue. P&L revenue is unchanged
            // (accrual): this desk teaches profit != cash with real numbers.
            // Simplification drops: bad debt — collections always arrive.
            int matured = 0;
            var kept = new List<Commitment>();
            foreach (Commitment r in state.Receivables)
            {
                r.WeeksLeft -= 1;
                if (r.WeeksLeft <= 0) matured += r.CashWk;
                else kept.Add(r);
            }
            state.Receivables = kept;
            if (matured > 0)
            {
                state.Cash += matured;
                rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "a net-30 invoice cleared: +${0} — the cash finally caught up with the profit", matured));
            }
            if (state.EraIndex() >= 3 && pnl != null)
            {
                int invoiced = Gd.ToInt(ReceivableFrac * pnl.Revenue);
                if (invoiced > 0)
                {
                    state.Cash -= invoiced;
                    state.Receivables.Add(new Commitment
                    {
                        Name = "invoiced on net-30", CashWk = invoiced, WeeksLeft = ReceivableWk,
                    });
                    rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                        "invoiced ${0} on net-30 — booked now, cash in {1} weeks", invoiced, ReceivableWk));
                }
            }
            // 9f BREAK-EVEN, the first crossing only — a milestone, not a meter.
            int be = BreakEvenCustomers(state);
            if (be > 0 && state.Traction >= be && !state.HasFlag("broke_even"))
            {
                state.SetFlag("broke_even");
                rep.Events.Add(string.Format(CultureInfo.InvariantCulture,
                    "BREAK-EVEN — {0} customers now feed the machine.", be));
            }
        }

        // ══ THE FORECAST (pure, expectation-only) ════════════════════════

        /// <summary>One projected week of the cash model.</summary>
        public sealed class ForecastWeek
        {
            public int Wk;
            public int Cash;
            public int Net;
            public int Revenue;
        }

        /// <summary>
        /// THE FP&amp;A 13-WEEK CASH MODEL, scaled to the game's 4-week
        /// attention span. PURE: it operates on locals and never touches state.
        /// EXPECTATION-ONLY: no draw anywhere, so the strip is a plan, not a
        /// prophecy — which is why it says "before surprises" on the sheet.
        ///
        /// Included and evolving: adds, churn, revenue, cogs, infra, the loan
        /// schedule (payments assumed made), receivables maturities, tax on
        /// projected EBT with a local copy of the carryforward, statuses only
        /// while they are still alive. Frozen: market_trend (its walk is rng),
        /// rival pressure, hype, product, payroll, prices, budgets, era.
        /// Excluded and named: incidents, new standing liabilities, DM effects,
        /// morale and outage rolls.
        /// </summary>
        public static List<ForecastWeek> ForecastCash(GameState state, int weeks = ForecastWeeks)
        {
            var outp = new List<ForecastWeek>();
            Theta th = state.Theta;
            if (th == null) return outp;
            double N = th.Tam;
            double A = state.Traction;
            double cash = state.Cash;
            int carry = state.TaxLossCarry;
            int eraI = state.EraIndex();
            // local copies — nothing below may reach a live object
            var notes = new List<Loan>();
            foreach (Loan l in state.Loans)
                notes.Add(new Loan
                {
                    Kind = l.Kind, Principal = l.Principal, Balance = l.Balance,
                    RateWk = l.RateWk, TermWk = l.TermWk, TakenWeek = l.TakenWeek,
                    PayWk = l.PayWk, Missed = l.Missed,
                });
            var recv = new List<Commitment>();
            foreach (Commitment r in state.Receivables)
                recv.Add(new Commitment { Name = r.Name, CashWk = r.CashWk, WeeksLeft = r.WeeksLeft });
            // the frozen half of the world
            double pressure = 0.0;
            foreach (Rival rv in state.Rivals) pressure += rv.Strength;
            pressure = Gd.Minf(pressure / Gd.Maxf(state.Rivals.Count, 1.0) / 100.0 * 0.5, 0.45);
            double hypeMult = 0.6 + state.Hype / 100.0 * 0.9;
            double bMk = state.Budgets.Acquisition() + state.MarketingBudget;
            double bSales = state.Budgets.Sales;
            double bCare = state.Budgets.Care;
            double bRnd = state.Budgets.Rnd;
            double bOffice = state.Budgets.Office;
            double mkMult = SimFunnel.ReachMult(state, bMk,
                1.0 + 1.4 * (1.0 - Math.Exp(-bMk / th.CacSat)));
            bool launched = state.HasFlag("launched");
            double qualityGate = 0.2 + state.Product / 100.0 * 0.8;
            double residence = th.LifetimeWk * (0.4 + state.Product / 100.0 * 1.2);
            double careMult = 1.0 - 0.30 * (1.0 - Math.Exp(-bCare / 1500.0));
            double pricePain = SimEngine.OffersPricePain(state);
            double priceDemand = Math.Pow(Gd.Maxf(state.PriceMult, 0.1), -1.5);
            double offerMult = SimEngine.OffersDemandMult(state);
            if (offerMult >= 0.0) priceDemand = offerMult;
            priceDemand = Gd.Clampf(priceDemand, 0.1, 3.0);
            int salesHeads = 0;
            foreach (Employee e in state.Employees)
                if ((e.Role ?? "").Contains("sales")) salesHeads += 1;
            double capScale = 1.0;
            if (state.BizWho == "SMB") capScale = 3.0;
            else if (state.BizWho == "Consumer") capScale = 40.0;
            double gtmCap = (1.5 + 0.8 * state.Competence("sell") + 3.0 * salesHeads
                             + bMk / 400.0 + bSales / 600.0) * capScale;
            double arpuBase = SimEngine.OffersArpu(state);
            if (arpuBase < 0.0) arpuBase = th.ArpuWk * state.PriceMult;
            double cogsPc = SimEngine.OffersCogsPerCustomer(state);
            double offerFixed = SimEngine.OffersFixedWk(state);
            double burnMult = th.BurnMult;
            double rent = EraRent(state);
            double payroll = Payroll(state);
            double levers = bMk + bSales + bCare + bRnd + bOffice;
            for (int w = 1; w <= Gd.Maxi(weeks, 1); w++)
            {
                // STATUSES COUNT ONLY WHILE THEY ARE STILL ALIVE in that week,
                // and the arithmetic is the tick's own: section 2 decrements
                // before section 8 reads, so a status with WeeksLeft k survives
                // into projected week w iff k > w.
                double sAdopt = StatusMult(state, "adopt_mult", w + 1);
                double sChurn = StatusMult(state, "churn_mult", w + 1);
                double sArpu = StatusMult(state, "arpu_mult", w + 1);
                double P = Gd.Maxf(N - A, 0.0);
                double pEff = th.AdoptP * hypeMult * mkMult * sAdopt * state.MarketTrend
                              * (1.0 - pressure) * qualityGate * (launched ? 1.0 : 0.0);
                double wom = th.AdoptIc * A * P / Gd.Maxf(N, 1.0) * sAdopt
                             * (1.0 - pressure) * qualityGate * (launched ? 1.0 : 0.5);
                double adds = Gd.Minf((pEff * P + wom) * priceDemand, gtmCap);
                double churn = A / Gd.Maxf(residence, 2.0) * th.ChurnMult * sChurn * careMult * pricePain;
                A = Gd.Maxf(A + adds - churn, 0.0);
                double revenue = A * arpuBase * sArpu;
                double cogs = A * cogsPc;
                double infra = 50.0 + A * 0.05;
                double burn = (rent + payroll + infra + levers) * burnMult + cogs + offerFixed;
                cash += revenue - burn;
                // the loan schedule, payments assumed made — a forecast that
                // assumed a default would be a threat, not a plan
                double interest = 0.0;
                foreach (Loan note in notes)
                {
                    int bal = note.Balance;
                    if (bal <= 0) continue;
                    int dueInt = (int)Math.Ceiling(bal * note.RateWk);
                    interest += dueInt;
                    if (note.Kind == "shark")
                    {
                        bal += dueInt;
                        if (cash > ClawTrigger)
                        {
                            double claw = Gd.Minf(cash - ClawKeep, bal);
                            cash -= claw;
                            bal -= Gd.ToInt(claw);
                        }
                    }
                    else if (note.Kind == "venture")
                    {
                        cash -= dueInt;
                        if (state.Week + w >= note.TakenWeek + note.TermWk)
                        {
                            cash -= bal;
                            bal = 0;
                        }
                    }
                    else
                    {
                        int due = Gd.Mini(note.PayWk, bal + dueInt);
                        cash -= due;
                        bal = bal + dueInt - due;
                    }
                    note.Balance = Gd.Maxi(bal, 0);
                }
                double liab = StandingLiab(state, w);
                cash -= liab;
                double netOps = revenue - burn - liab;
                double ebt = netOps - interest;
                double tax = 0.0;
                if (eraI >= TaxEra)
                {
                    if (ebt < 0.0)
                    {
                        carry += Gd.RoundToInt(-ebt);
                    }
                    else
                    {
                        double shelter = Gd.Minf(carry, ebt);
                        carry -= Gd.RoundToInt(shelter);
                        tax = Math.Round(TaxRate * (ebt - shelter), MidpointRounding.AwayFromZero);
                        cash -= tax;
                    }
                }
                // the net-30 float: what clears this week, and what this week defers
                var keptR = new List<Commitment>();
                foreach (Commitment r in recv)
                {
                    r.WeeksLeft -= 1;
                    if (r.WeeksLeft <= 0) cash += r.CashWk;
                    else keptR.Add(r);
                }
                recv = keptR;
                if (eraI >= 3)
                {
                    double invoiced = Gd.ToInt(ReceivableFrac * revenue);
                    cash -= invoiced;
                    recv.Add(new Commitment { CashWk = Gd.ToInt(invoiced), WeeksLeft = ReceivableWk });
                }
                if (state.Era == "hq" && cash > SweepFloor)
                {
                    double sweep = Gd.ToInt(SweepRate * (cash - SweepFloor));
                    cash += sweep;
                    interest -= sweep;
                }
                outp.Add(new ForecastWeek
                {
                    Wk = state.Week + w, Cash = Gd.RoundToInt(cash),
                    Net = Gd.RoundToInt(netOps - interest - tax), Revenue = Gd.RoundToInt(revenue),
                });
            }
            return outp;
        }

        // ══ WHAT THE REST OF THE GAME READS ══════════════════════════════

        /// <summary>DM context lines, section 10 of the DIRECTIVES block. The
        /// DM narrates the debt; it never prices it.</summary>
        public static List<string> Directives(GameState state)
        {
            var outp = new List<string>();
            int shown = 0;
            foreach (Loan l in state.Loans)
            {
                if (l.Balance <= 0 || shown >= 2) continue;
                shown += 1;
                if (l.Kind == "shark")
                {
                    outp.Add(string.Format(CultureInfo.InvariantCulture,
                        "- Loan: ${0} at 18.0%/wk; the shark feeds before anyone else.", l.Balance));
                }
                else if (l.Kind == "venture")
                {
                    outp.Add(string.Format(CultureInfo.InvariantCulture,
                        "- Loan: ${0} at {1:0.0}%/wk; interest only, balloon in {2} wks.",
                        l.Balance, l.RateWk * 100.0,
                        Gd.Maxi(l.TakenWeek + l.TermWk - state.Week, 0)));
                }
                else
                {
                    int left = NoteWeeksLeft(l.Balance, l.RateWk, l.PayWk);
                    outp.Add(string.Format(CultureInfo.InvariantCulture,
                        "- Loan: ${0} at {1:0.0}%/wk; payment ${2} due in {3} wks.",
                        l.Balance, l.RateWk * 100.0, l.PayWk, Gd.Maxi(left, 0)));
                }
            }
            if (CreditLocked(state))
                outp.Add("- The bank has stopped answering: a note is in default and the collectors are calling.");
            if (state.HasFlag("tax_noticed"))
            {
                Pnl p = state.LastPnl;
                int ebt = p == null ? 0 : p.Revenue - p.Burn - p.LiabilitiesWk - p.Interest;
                outp.Add(string.Format(CultureInfo.InvariantCulture,
                    "- The taxman takes 20% of profit now (EBT ${0} last week).", ebt));
            }
            return outp;
        }

        /// <summary>
        /// Attention rows — the bank (00-spine section 4). Each label is 40
        /// characters or less of pedagogy: the garage ticker prints it verbatim.
        /// </summary>
        public static List<AttentionItem> Attention(GameState state)
        {
            var rows = new List<AttentionItem>();
            int service = DebtServiceWk(state);
            string label = "";
            // the switch the red lands on (S2b): the distressed note's card,
            // else the borrow stepper (the cash-cliff case)
            string ctl = "borrow";
            if (service > 0 && state.Cash < 2 * service)
                label = "a note payment you cannot cover";
            for (int i = 0; i < state.Loans.Count; i++)
            {
                Loan l = state.Loans[i];
                if (l.Balance <= 0) continue;
                if (l.Missed >= 1) { label = "missed a note — the balance grows"; ctl = "note_" + i; }
                if (l.Kind == "venture")
                {
                    int toBalloon = l.TakenWeek + l.TermWk - state.Week;
                    if (toBalloon <= 2 && state.Cash < l.Balance)
                    { label = "balloon due soon — no cash for it"; ctl = "note_" + i; }
                }
            }
            if (label.Length > 0)
                rows.Add(new AttentionItem
                {
                    Desk = "the bank", Key = "debt_distress", Severity = 3, Label = label,
                    Control = ctl,
                });
            if (state.HasFlag("tax_noticed") && !state.HasFlag("tax_seen"))
                rows.Add(new AttentionItem
                {
                    Desk = "the bank", Key = "first_tax", Severity = 2,
                    Label = "the taxman found you — profit is taxed",
                });
            if (state.HasFlag("broke_even") && !state.HasFlag("broke_even_seen"))
                rows.Add(new AttentionItem
                {
                    Desk = "the bank", Key = "broke_even", Severity = 1,
                    Label = "BREAK-EVEN crossed — see the bank",
                });
            return rows;
        }
    }
}
