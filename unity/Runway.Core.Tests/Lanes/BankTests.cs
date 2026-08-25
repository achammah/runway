using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Runway.Core;

namespace Runway.CoreTests
{
    /// <summary>
    /// LANE SUITE — bank. Spec: docs/design/06-finance.md section 12 (the twin pins).
    ///
    /// Program.cs calls Run after the engine's own checks and hands over `ok`,
    /// the same assert the whole suite uses: ok(condition, "what it pins").
    ///
    /// The porting law: a check lands FIRST in game/tests/lanes/test_bank.gd,
    /// then here in the same order. Same checks, same order, same logic — the
    /// two engines do not share PRNG internals, so never pin a draw across them,
    /// only behaviour. Nothing in this lane rolls dice at all, so every number
    /// below is arithmetic a reader can check by hand.
    /// </summary>
    public static class BankTests
    {
        /// The suite's own fixture, identical to Program.NewState so the hand
        /// arithmetic in the comments stays checkable: Software/SMB theta
        /// (arpu 14, tam 60k, burn_mult 1.0), garage era, $50,000 in the bank.
        private static GameState NewState()
        {
            var s = new GameState();
            s.SimSeed = 42;
            s.Week = 5;
            s.Cash = 50000;
            s.Traction = 40;
            s.Product = 50;
            s.Morale = 70;
            s.Hype = 40;
            s.BizWhat = "Software";
            s.BizWho = "SMB";
            s.Theta = SimEngine.DefaultTheta(s.BizWhat, s.BizWho);
            return s;
        }

        /// <summary>A fresh office-era company — the tax pins need several, and
        /// each charge mutates the state it is charged against.</summary>
        private static GameState Office()
        {
            GameState s = NewState();
            s.Era = "office";
            return s;
        }

        private static Loan Note(string kind, int bal, double rate, int term, int wk, int pay)
        {
            return new Loan
            {
                Kind = kind, Principal = bal, Balance = bal, RateWk = rate,
                TermWk = term, TakenWeek = wk, PayWk = pay, Missed = 0,
            };
        }

        private static MoneyWork Books(double revenue, int burn, int liab, double interest)
        {
            return new MoneyWork
            {
                Revenue = revenue, Burn = burn, LiabilitiesWk = liab, Interest = interest,
            };
        }

        private static string S(object v)
        {
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public static void Run(Action<bool, string> ok)
        {
            // ── 1. MIGRATION + THE SHARK LAW. The legacy LoanPrincipal becomes
            // a structured shark note at tick time and not one dollar changes:
            // 18%/wk still compounds, the claw still takes the walking money.
            GameState mg = NewState();
            mg.Cash = 500;
            mg.Traction = 0;
            mg.LoanPrincipal = 10000;
            SimEngine.WeeklyTick(mg);
            ok(mg.Loans.Count == 1 && mg.Loans[0].Kind == "shark",
               "a legacy loan migrates into one shark note");
            ok(mg.LoanPrincipal == 0, "the legacy field empties once it has migrated");
            ok(SimBank.DebtTotal(mg) >= 11800,
               "18%/wk still compounds after the migration (owe " + S(SimBank.DebtTotal(mg)) + ")");
            GameState claw = NewState();
            claw.Traction = 0;
            claw.LoanPrincipal = 5000;
            SimEngine.WeeklyTick(claw);
            ok(SimBank.DebtTotal(claw) == 0 && claw.Cash < 50000,
               "the shark's claw still repays out of any cash above $2,000");

            // ── 2. THE CREDIT LADDER. Each era's access is the one the spec's
            // table names, and the desk's quote can never touch the shark's 18%.
            GameState gar = NewState();
            ok(SimBank.BorrowHeadroom(gar) == 0, "no bank answers a garage — headroom is $0");
            GameState cwk = NewState();
            cwk.Era = "coworking";
            cwk.Traction = 500;                       // rev_wk = 500 x 14 = $7,000/wk
            ok(SimBank.BorrowHeadroom(cwk) == 10000,
               "a coworking micro-line caps at $10,000 (" + S(SimBank.BorrowHeadroom(cwk)) + ")");
            ok(Gd.Absf(SimBank.BankRateWk(cwk) - 0.04) < 0.0005,
               "the small-business premium floors the coworking quote at 4.0%/wk");
            GameState des = NewState();
            des.Era = "office";
            des.Cash = -1000;                         // runway 0 -> health 1.0
            des.LastGrowth = -0.5;                    // a 50% slump -> slump 1.0
            ok(Gd.Absf(SimBank.BankRateWk(des) - 0.11) < 0.0005,
               "desperate office books quote the top of the band, 11.0%/wk ("
               + SimBank.BankRateWk(des).ToString("0.000", CultureInfo.InvariantCulture) + ")");
            ok(SimBank.BankRateWk(des) < SimBank.SharkRate,
               "the desk's worst quote is still cheaper than the shark");
            GameState hlt = NewState();
            hlt.Era = "office";
            hlt.Traction = 4000;                      // profitable -> runway 999 -> health 0
            hlt.LastGrowth = 0.2;
            ok(Gd.Absf(SimBank.BankRateWk(hlt) - 0.02) < 0.0005,
               "healthy office books quote the 2.0% floor ("
               + SimBank.BankRateWk(hlt).ToString("0.000", CultureInfo.InvariantCulture) + ")");
            GameState vd = NewState();
            vd.Era = "floor";
            ok(SimBank.VentureCap(vd) == 0, "venture debt is locked until a round has closed");
            vd.LastRoundAmount = 100000;
            ok(SimBank.VentureCap(vd) == 30000,
               "venture debt sizes at 30% of the last round (" + S(SimBank.VentureCap(vd)) + ")");
            GameState swp = NewState();
            swp.Era = "hq";
            swp.Traction = 0;
            swp.Cash = 300000;
            SimEngine.WeeklyTick(swp);
            ok(swp.LastPnl != null && swp.LastPnl.Interest == -200,
               "hq sweeps 0.1%/wk on idle cash into the interest lane as income ("
               + S(swp.LastPnl == null ? 0 : swp.LastPnl.Interest) + ")");

            // ── 3. THE ANNUITY. $10,000 at 4%/wk over 8 weeks pays $1,486/wk,
            // closes in exactly 8 ticks with cash to spare, ~$1,888 of interest.
            ok(SimBank.LoanPaymentWk(10000, 0.04, 8) == 1486,
               "the level payment is $1,486/wk (" + S(SimBank.LoanPaymentWk(10000, 0.04, 8)) + ")");
            GameState an = NewState();
            an.Era = "office";
            an.Cash = 200000;
            an.Traction = 0;
            an.Loans = new List<Loan> { Note("bank", 10000, 0.04, 8, an.Week, 1486) };
            int interestSum = 0;
            bool identity = true;
            for (int w = 0; w < 8; w++)
            {
                an.Week += 1;
                SimEngine.WeeklyTick(an);
                Pnl p = an.LastPnl;
                interestSum += p.Interest;
                if (p.Net != p.Revenue - p.Burn - p.LiabilitiesWk - p.Interest - p.Tax)
                    identity = false;
            }
            ok(an.Loans.Count == 0, "the note closes in exactly eight payments");
            ok(Gd.Absi(interestSum - 1888) <= 40,
               "the eight payments cost about $1,888 in interest (" + S(interestSum) + ")");
            ok(identity, "the P&L identity holds every week the note is being paid");

            // ── 4. THE MISS LADDER. Skipped, never overdrawn: capitalize,
            // reprice, then sell the paper — and repaying lifts the lock.
            GameState ms = NewState();
            ms.Era = "office";
            ms.Cash = 0;
            ms.Traction = 0;
            ms.Loans = new List<Loan> { Note("bank", 10000, 0.04, 8, ms.Week, 1486) };
            GameState ctl = NewState();
            ctl.Era = "office";
            ctl.Cash = 0;
            ctl.Traction = 0;
            ms.Week += 1;
            ctl.Week += 1;
            SimEngine.WeeklyTick(ms);
            SimEngine.WeeklyTick(ctl);
            Loan n0 = ms.Loans[0];
            ok(n0.Missed == 1 && n0.Balance == 10400,
               "a missed payment capitalizes its interest instead of overdrawing you ("
               + S(n0.Balance) + ")");
            ok(ms.Morale == ctl.Morale - 3, "a missed payment costs three points of morale");
            ms.Week += 1;
            SimEngine.WeeklyTick(ms);
            Loan n1 = ms.Loans[0];
            ok(Gd.Absf(n1.RateWk - 0.06) < 0.0005 && SimBank.CreditLocked(ms),
               "the second miss reprices the risk +2% and locks the bank out ("
               + n1.RateWk.ToString("0.000", CultureInfo.InvariantCulture) + ")");
            ms.Week += 1;
            SimEngine.WeeklyTick(ms);
            Loan n2 = ms.Loans[0];
            ok(n2.Kind == "shark" && Gd.Absf(n2.RateWk - 0.18) < 0.0005,
               "the third miss sells the note to the collectors at 18%/wk");
            ok(SimEngine.HasStatus(ms, "collections_calls"),
               "collections install the status investors can smell");
            ms.Cash = 100000;
            SimBank.RepayNote(ms, 0);
            ok(!SimBank.CreditLocked(ms) && ms.Loans.Count == 0,
               "repaying the distressed note lifts the credit lock");

            // ── 5. TAX. 20% of EBT — after interest, from the office up, with
            // losses carried forward so one good week in a bad month is untaxed.
            GameState tx = Office();
            int taxFlat = SimBank.TaxWk(tx, Books(12000.0, 9938, 0, 0.0));
            ok(taxFlat == 412, "an EBT of $2,062 is taxed $412 (" + S(taxFlat) + ")");
            ok(tx.Cash == 50000 - 412, "the tax actually leaves the bank account");
            ok(tx.HasFlag("tax_noticed"), "the first charge puts the company on the radar");
            ok(SimBank.TaxWk(NewState(), Books(12000.0, 9938, 0, 0.0)) == 0,
               "identical books in a garage are taxed nothing");
            GameState cf = Office();
            SimBank.TaxWk(cf, Books(0.0, 1000, 0, 0.0));
            ok(cf.TaxLossCarry == 1000, "a losing week banks its loss as a carryforward");
            ok(SimBank.TaxWk(cf, Books(1000.0, 0, 0, 0.0)) == 0 && cf.TaxLossCarry == 0,
               "the carryforward shelters the next $1,000 of profit and is spent doing it");
            ok(SimBank.TaxWk(Office(), Books(12000.0, 9938, 0, 1000.0)) == 212,
               "interest is deducted BEFORE the tax — EBT, not operating profit");

            // ── 6. BREAK-EVEN + FORECAST PURITY.
            GameState be = NewState();
            be.Era = "coworking";
            be.Traction = 0;
            be.Offers = new List<Offer>
            {
                new Offer { Name = "a session", Unit = "per session", Price = 40.0,
                    PriceSet = true, FairPrice = 40.0, UnitCost = 10.0, Elasticity = 2.0, Weight = 1.0 },
            };
            // margin = $40 − ($10 serving + $0.05 infra) = $29.95 · fixed = rent
            // 600 + infra 50 = $650 · 650 / 29.95 = 21.7 → 22 customers
            ok(SimBank.BreakEvenCustomers(be) == 22,
               "break-even is fixed costs over contribution margin: 22 customers ("
               + S(SimBank.BreakEvenCustomers(be)) + ")");
            GameState loss = NewState();
            loss.Era = "coworking";
            loss.Offers = new List<Offer>
            {
                new Offer { Name = "a session", Unit = "per session", Price = 5.0,
                    PriceSet = true, FairPrice = 40.0, UnitCost = 10.0, Elasticity = 2.0, Weight = 1.0 },
            };
            ok(SimBank.BreakEvenCustomers(loss) == -1,
               "no count breaks even when a customer costs more than they pay");
            GameState fp = NewState();
            fp.Traction = 0;
            fp.Cash = 10000;
            string before = JsonConvert.SerializeObject(fp);
            List<SimBank.ForecastWeek> rows = SimBank.ForecastCash(fp, 4);
            string after = JsonConvert.SerializeObject(fp);
            ok(before == after, "the forecast is pure — it leaves the state byte-identical");
            ok(rows.Count == 4, "the forecast runs the four weeks it was asked for");
            // hand math, week 1: no customers, nothing launched, so burn is rent
            // $150 + infra $50 and nothing else. $10,000 − $200 = $9,800, net −$200.
            ok(rows[0].Cash == 9800 && rows[0].Net == -200,
               "week one of the forecast matches a noise-stripped tick by hand ("
               + S(rows[0].Cash) + ")");
            GameState fl = NewState();
            fl.Traction = 0;
            fl.Cash = 10000;
            fl.Loans = new List<Loan> { Note("bank", 10000, 0.04, 8, fl.Week, 1486) };
            List<SimBank.ForecastWeek> frows = SimBank.ForecastCash(fl, 1);
            ok(frows[0].Cash == 9800 - 1486,
               "the forecast pays the loan schedule it can see (" + S(frows[0].Cash) + ")");
            GameState fr = NewState();
            fr.Era = "floor";
            fr.Traction = 0;
            fr.Cash = 10000;
            fr.Receivables = new List<Commitment>
            {
                new Commitment { Name = "an old invoice", CashWk = 4000, WeeksLeft = 1 },
            };
            List<SimBank.ForecastWeek> rrows = SimBank.ForecastCash(fr, 1);
            ok(rrows[0].Cash == 10000 - 12050 + 4000,
               "the forecast lands the net-30 invoices that mature inside it ("
               + S(rrows[0].Cash) + ")");
            GameState sn = NewState();
            SimEngine.WeeklyTick(sn);
            MetricSnapshot last = sn.MetricHistory[sn.MetricHistory.Count - 1];
            ok(last.Net.HasValue, "every history row now carries the week's net");
        }
    }
}
