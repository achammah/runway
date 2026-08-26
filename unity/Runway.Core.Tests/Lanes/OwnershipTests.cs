using System;
using System.Collections.Generic;
using Runway.Core;

namespace Runway.CoreTests
{
    /// <summary>
    /// LANE SUITE — the ownership cluster (DAG2 W2 L-OWN, live). Spec:
    /// docs/design/DECISIONS.md (THE OWNERSHIP CLUSTER, THE ESOP THREAD, THE
    /// OFFER) + docs/design/DAG2.md. These pins replace the W1 stub-neutrality
    /// pins: the lane is no longer neutral — the checks now hold its MATH.
    ///
    /// The porting law: a check lands FIRST in
    /// game/tests/lanes/test_ownership.gd, then here in the same order,
    /// byte-identical messages. Stochastic paths run bounded per-seed loops —
    /// deterministic per engine, never a flake.
    /// </summary>
    public static class OwnershipTests
    {
        static GameState St()
        {
            var s = new GameState();
            s.SimSeed = 4242;
            s.Week = 12;
            s.Cash = 60000;
            s.Traction = 30;
            s.Product = 50;
            s.Morale = 70;
            s.Hype = 30;
            s.BizWhat = "Software";
            s.BizWho = "SMB";
            s.Theta = SimEngine.DefaultTheta(s.BizWhat, s.BizWho);
            return s;
        }

        static List<Dictionary<string, object>> StagesIn(GameState s, string stage)
        {
            return SimOwnership.StagesIn(s, stage);
        }

        static int RowTake(Dictionary<string, object> wf, string holder)
        {
            var rows = (List<Dictionary<string, object>>)wf["rows"];
            foreach (var r in rows)
            {
                object h;
                if (r.TryGetValue("holder", out h) && Convert.ToString(h) == holder)
                {
                    object t;
                    return r.TryGetValue("take", out t) ? Convert.ToInt32(t) : 0;
                }
            }
            return 0;
        }

        static int WfI(Dictionary<string, object> wf, string k)
        {
            object v;
            return wf.TryGetValue(k, out v) && v != null ? Convert.ToInt32(v) : 0;
        }

        static string DsS(Dictionary<string, object> d, string k)
        {
            object v;
            return d != null && d.TryGetValue(k, out v) && v != null ? Convert.ToString(v) : "";
        }

        static int DiI(Dictionary<string, object> d, string k)
        {
            object v;
            if (d != null && d.TryGetValue(k, out v) && v != null)
            {
                try { return Convert.ToInt32(v); } catch { return 0; }
            }
            return 0;
        }

        public static void Run(Action<bool, string> ok)
        {
            // ── 1 · MIGRATION: the legacy pool seeds the esop, one-way, idempotent
            GameState s1 = St();
            s1.OptionPoolPct = 10.0;
            SimEngine.WeeklyTick(s1);
            ok(s1.Esop != null && Gd.Absf(s1.Esop.PoolPct - 10.0) < 0.001
               && s1.Esop.Granted.Count == 0,
                "ownership: the legacy option_pool_pct seeds esop.pool_pct one-way");
            s1.Week += 1;
            SimEngine.WeeklyTick(s1);
            ok(Gd.Absf(s1.Esop.PoolPct - 10.0) < 0.001,
                "ownership: the pool migration is idempotent across ticks");

            // ── 2 · MIGRATION: the legacy labor market drains into recruitment
            GameState s2 = St();
            s2.Era = "coworking";
            s2.OpenRoles = new List<OpenRole> { new OpenRole { Role = "sales",
                OfferedSalary = 1200, OpenedWeek = 10, Seats = 1 } };
            s2.Applicants = new List<Applicant> { new Applicant { Name = "Ade Okafor",
                Role = "sales", Skill = 4, Ask = 1400, AppliedWeek = 11, Source = "inbound" } };
            SimEngine.WeeklyTick(s2);
            ok(s2.OpenRoles.Count == 0 && s2.Applicants.Count == 0
               && s2.Recruitment != null && s2.Recruitment.Roles.Count == 1
               && s2.Recruitment.Candidates.Count >= 1,
                "ownership: open_roles and applicants migrate into recruitment as the source of truth");
            Dictionary<string, object> mrole = s2.Recruitment.Roles[0];
            ok(DiI(mrole, "band_lo") > 0 && DiI(mrole, "band_hi") > DiI(mrole, "band_lo"),
                "ownership: a migrated seat carries the labor market's band");

            // ── 3 · VESTING: 208-wk linear, 52-wk cliff, computed never stored
            ok(SimOwnership.VestFrac(51) == 0.0 && Gd.Absf(SimOwnership.VestFrac(52) - 0.25) < 0.001
               && Gd.Absf(SimOwnership.VestFrac(104) - 0.5) < 0.001
               && SimOwnership.VestFrac(208) == 1.0 && SimOwnership.VestFrac(300) == 1.0,
                "ownership: the vest curve holds the cliff and the 208-wk line");
            GameState s3 = St();
            s3.Esop = new Esop { PoolPct = 10.0, Granted = new List<EsopGrant> {
                new EsopGrant { EmpId = "june_park", Pct = 0.8, VestStartWk = 0 } } };
            ok(Gd.Absf(SimOwnership.VestedPct(s3, "june_park", 104) - 0.4) < 0.001,
                "ownership: vested_pct reads the grant halfway at wk 104");

            // ── 4 · THE LEAVER RULE: vested kept, unvested returns to the pool
            GameState s4 = St();
            s4.Week = 104;
            s4.Esop = new Esop { PoolPct = 10.0, Granted = new List<EsopGrant> {
                new EsopGrant { EmpId = "june_park", Pct = 0.8, VestStartWk = 0 } } };
            SimEngine.WeeklyTick(s4);   // june is on no roster — the grant crystallizes
            EsopGrant g4 = s4.Esop.Granted[0];
            ok(g4.EmpId == "left:june_park" && Gd.Absf(g4.Pct - 0.4) < 0.01,
                "ownership: a leaver keeps vested and the unvested returns to the pool");
            ok(Gd.Absf(SimOwnership.PoolFree(s4) - 9.6) < 0.02
               && Gd.Absf(SimOwnership.VestedPct(s4, "june_park", s4.Week) - 0.4) < 0.01,
                "ownership: the freed pool space and the kept vested both read true");

            // ── 5 · CONVERSION: pct = amount / min(cap, pre × (1 − discount))
            var safe = new Instrument { Kind = "safe", Amount = 150000, Cap = 4000000,
                Discount = 0.2, Rate = 0.0, SignedWk = 9, Pct = 0.0 };
            ok(Gd.Absf(SimOwnership.ConvertPctAt(safe, 6000000.0, 12) - 3.75) < 0.001,
                "ownership: the cap side binds the conversion (150k at 4M = 3.75%)");
            var safe2 = new Instrument { Kind = "safe", Amount = 150000, Cap = 0,
                Discount = 0.2, Rate = 0.0, SignedWk = 9, Pct = 0.0 };
            ok(Gd.Absf(SimOwnership.ConvertPctAt(safe2, 6000000.0, 12) - 3.125) < 0.001,
                "ownership: the discount side binds when no cap does (4.8M basis = 3.125%)");
            var note = new Instrument { Kind = "note", Amount = 150000, Cap = 4000000,
                Discount = 0.0, Rate = 0.003, SignedWk = 0, Pct = 0.0 };
            ok(SimOwnership.AmountDue(note, 52) == 173400,
                "ownership: a note accrues simple interest into its due amount");

            // ── 6 · THE PRICED CLOSE: stack converts, seams run, the pie stays 100
            GameState s6 = St();
            s6.Era = "office";
            s6.Instruments = new List<Instrument> { new Instrument { Kind = "safe",
                Holder = "Fern Capital", Amount = 150000, Cap = 4000000, Discount = 0.2,
                Rate = 0.0, MaturityWk = 0, Pct = 0.0, Prefs = 0.0, Protective = false,
                DragThreshold = 0.0, SignedWk = 9 } };
            s6.RaiseState = new RaiseState { Stages = new List<Dictionary<string, object>> {
                new Dictionary<string, object> { { "name", "Halden Ventures" },
                    { "stage", "terms" }, { "arrived_wk", 10 },
                    { "terms", new Dictionary<string, object> { { "kind", "priced" },
                        { "valuation", 2500000 }, { "amount", 500000 }, { "pct", 16.7 },
                        { "prefs", 1.0 }, { "protective", true }, { "drag_threshold", 60.0 },
                        { "board_seat", true }, { "no_shop_wks", 4 },
                        { "pool_topup_pct", 10.0 }, { "expires_wk", 15 } } } } },
                InterestScore = 50.0, Active = true, FounderTimeTax = 0.3 };
            int cashBefore = s6.Cash;
            string line6 = SimOwnership.OpSignInstrument(s6, "Halden");
            ok(line6 != "" && s6.Cash == cashBefore + 500000,
                "ownership: a priced round wires its cash as one-shot event money");
            ok(s6.RoundsRaised.Count == 1 && s6.Board != null,
                "ownership: the priced close rides apply_round and the board covenant seam");
            Instrument conv = s6.Instruments[0];
            ok(conv.Pct > 0.0,
                "ownership: the SAFE stack converts, whole, at the priced event");
            double total = s6.FounderPct + (s6.Esop != null ? s6.Esop.PoolPct : 0.0);
            foreach (Instrument inst in s6.Instruments) total += inst.Pct;
            ok(Gd.Absf(total - 100.0) < 0.5,
                "ownership: after the close the slices still sum to the whole pie");
            ok(s6.Esop != null && Gd.Absf(s6.Esop.PoolPct - s6.OptionPoolPct) < 0.001,
                "ownership: esop.pool_pct and the legacy mirror agree after the shuffle");
            ok(!s6.RaiseState.Active && SimOwnership.NoShopUntil(s6) > s6.Week,
                "ownership: the wire ends the raise and arms the no-shop freeze");

            // ── 7 · INTEREST: deterministic, and traction moves it
            GameState s7a = St();
            GameState s7b = St();
            ok(Gd.Absf(SimOwnership.InterestScoreCalc(s7a) - SimOwnership.InterestScoreCalc(s7b)) < 0.0001,
                "ownership: the interest score is a deterministic read");
            s7b.Traction = 500;
            ok(SimOwnership.InterestScoreCalc(s7b) > SimOwnership.InterestScoreCalc(s7a),
                "ownership: traction raises investor interest");

            // ── 8 · THE KNOCK: a hot company gets inbound within the bounded window
            GameState s8 = St();
            s8.Traction = 3000;
            s8.Hype = 90;
            s8.MacroSeason = "boom";
            s8.Cash = 500000;
            s8.Investors = new List<Investor> { new Investor { Name = "Harborline Syndicate",
                Thesis = "momentum" } };
            bool knocked = false;
            for (int i = 0; i < 20; i++)
            {
                s8.Week += 1;
                SimEngine.WeeklyTick(s8);
                if (s8.RaiseState != null && s8.RaiseState.Stages.Count > 0)
                {
                    knocked = true;
                    break;
                }
            }
            ok(knocked, "ownership: inbound knocks come to traction (bounded window)");

            // ── 9 · PITCH then TERMS: the data room decides, the tax is real
            GameState s9 = St();
            s9.Cash = 200000;
            s9.LastGrowth = 0.08;
            s9.LastPnl = new Pnl { Revenue = 900, Net = 120 };
            s9.Investors = new List<Investor> { new Investor { Name = "Bell & Weir",
                Thesis = "durable margin" } };
            string pline = SimOwnership.OpPitchInvestor(s9, "Bell & Weir");
            ok(pline != "" && s9.RaiseState.Active
               && Gd.Absf(s9.RaiseState.FounderTimeTax - 0.3) < 0.001,
                "ownership: pitching opens the conversation and the founder-time tax bites");
            SimOwnership.TickPre(s9, new WeeklyReport());
            ok(SimEngine.HasStatus(s9, SimOwnership.RAISE_STATUS)
               == SimEngine.STATUS.ContainsKey(SimOwnership.RAISE_STATUS),
                "ownership: the raise drag arms exactly when the status catalog knows it");
            bool termsSeen = false;
            for (int j = 0; j < 6; j++)
            {
                s9.Week += 1;
                s9.LastPnl = new Pnl { Revenue = 900, Net = 120 };
                SimOwnership.TickPost(s9, new WeeklyReport());
                if (StagesIn(s9, "terms").Count > 0)
                {
                    termsSeen = true;
                    break;
                }
            }
            ok(termsSeen, "ownership: a healthy data room turns the conversation into terms");
            if (termsSeen)
            {
                var t9 = (Dictionary<string, object>)StagesIn(s9, "terms")[0]["terms"];
                ok(DiI(t9, "amount") > 0 && DiI(t9, "expires_wk") > s9.Week,
                    "ownership: drafted terms carry banded money and a shelf life");
            }

            // ── 10 · THE ACCEPTANCE CURVE: cash climbs it; profiles bend it
            GameState s10 = St();
            var merc = new Dictionary<string, object> { { "ask", 540 }, { "profile", "mercenary" } };
            var miss = new Dictionary<string, object> { { "ask", 540 }, { "profile", "missionary" } };
            ok(SimOwnership.AcceptanceOdds(s10, merc, 560, 0.0) > SimOwnership.AcceptanceOdds(s10, merc, 500, 0.0),
                "ownership: more cash raises acceptance odds");
            ok(SimOwnership.AcceptanceOdds(s10, miss, 520, 0.4) - SimOwnership.AcceptanceOdds(s10, miss, 520, 0.0)
               > SimOwnership.AcceptanceOdds(s10, merc, 520, 0.4) - SimOwnership.AcceptanceOdds(s10, merc, 520, 0.0),
                "ownership: options move a missionary more than a mercenary");
            ok(SimOwnership.AcceptanceOdds(s10, merc, 5000, 5.0) <= 95.0
               && SimOwnership.AcceptanceOdds(s10, merc, 10, 0.0) >= 5.0,
                "ownership: acceptance odds stay inside the 5–95 clamp");

            // ── 11 · SEND then SIGN: the hire rides the labor pipeline, the grant lands
            GameState s11 = St();
            s11.Era = "coworking";
            s11.Esop = new Esop { PoolPct = 10.0, Granted = new List<EsopGrant>() };
            s11.Recruitment = new Recruitment
            {
                Roles = new List<Dictionary<string, object>> { new Dictionary<string, object> {
                    { "id", "role_sales_10" }, { "seat", "sales" }, { "band_lo", 1060 },
                    { "band_hi", 1560 }, { "advert_wk", 0 }, { "opened_wk", 10 } } },
                Candidates = new List<Dictionary<string, object>> { new Dictionary<string, object> {
                    { "id", "cand_12_0" }, { "role_id", "role_sales_10" }, { "name", "Dana Kovic" },
                    { "ask", 900 }, { "profile", "mercenary" }, { "skill", 4 },
                    { "stage", "interviewed" }, { "arrived_wk", 11 } } },
                OffersOut = new List<Dictionary<string, object>>(),
            };
            bool joined = false;
            for (int k = 0; k < 6; k++)
            {
                Dictionary<string, object> cand11 = SimOwnership.CandById(s11, "cand_12_0");
                if (cand11 != null && DsS(cand11, "stage") == "joined")
                {
                    joined = true;
                    break;
                }
                if (cand11 != null && DsS(cand11, "stage") == "interviewed")
                    SimOwnership.OpSendOffer(s11, "cand_12_0", 1400, 0.4);
                s11.Week += 1;
                SimEngine.WeeklyTick(s11);
            }
            ok(joined, "ownership: a rich offer gets signed inside the bounded window");
            bool hired = false;
            foreach (PipelineHire h in s11.Pipeline)
                if (h.Name == "Dana Kovic") hired = true;
            foreach (Employee e in s11.Employees)
                if (e.Name == "Dana Kovic") hired = true;
            ok(hired, "ownership: the signed candidate rides the existing labor hire path");
            ok(SimOwnership.GrantedPct(s11, "dana_kovic") > 0.0,
                "ownership: the offer's options become a real grant at signing");
            ok(s11.Recruitment.Roles.Count == 0,
                "ownership: a filled seat closes its requisition");

            // ── 12 · THE POOL GATE: an empty pool blocks equity offers
            GameState s12 = St();
            s12.Esop = new Esop { PoolPct = 0.5, Granted = new List<EsopGrant> {
                new EsopGrant { EmpId = "x_y", Pct = 0.5, VestStartWk = 12 } } };
            s12.Recruitment = new Recruitment
            {
                Roles = new List<Dictionary<string, object>>(),
                Candidates = new List<Dictionary<string, object>> { new Dictionary<string, object> {
                    { "id", "c1" }, { "role_id", "" }, { "name", "Tom Beck" }, { "ask", 900 },
                    { "profile", "mercenary" }, { "skill", 3 }, { "stage", "interviewed" },
                    { "arrived_wk", 11 } } },
                OffersOut = new List<Dictionary<string, object>>(),
            };
            ok(SimOwnership.OpSendOffer(s12, "c1", 1000, 1.0) == "",
                "ownership: an empty pool refuses the equity offer");
            ok(SimOwnership.OpSendOffer(s12, "c1", 1000, 0.0) != "",
                "ownership: the same offer goes out cash-only");

            // ── 13 · THE ADVERT LANE: recruit_ads bills and the identity holds
            GameState s13 = St();
            s13.Recruitment = new Recruitment
            {
                Roles = new List<Dictionary<string, object>> {
                    new Dictionary<string, object> { { "id", "r1" }, { "seat", "sales" },
                        { "band_lo", 1000 }, { "band_hi", 1500 }, { "advert_wk", 60 },
                        { "opened_wk", 12 } },
                    new Dictionary<string, object> { { "id", "r2" }, { "seat", "engineer" },
                        { "band_lo", 1200 }, { "band_hi", 1800 }, { "advert_wk", 40 },
                        { "opened_wk", 12 } } },
                Candidates = new List<Dictionary<string, object>>(),
                OffersOut = new List<Dictionary<string, object>>(),
            };
            SimEngine.WeeklyTick(s13);
            ok(s13.LastPnl != null && s13.LastPnl.RecruitAds == 100,
                "ownership: the advert lane bills exactly the seats' spend");
            ok(s13.LastPnl.Net == s13.LastPnl.Revenue - s13.LastPnl.Burn
               - s13.LastPnl.LiabilitiesWk - s13.LastPnl.Interest - s13.LastPnl.Tax,
                "ownership: the P&L identity holds with the advert lane live");

            // ── 14 · ESOP IS NON-CASH: paper moves no money through the tick
            GameState s14a = St();
            GameState s14b = St();
            s14b.Esop = new Esop { PoolPct = 10.0, Granted = new List<EsopGrant> {
                new EsopGrant { EmpId = "someone_here", Pct = 2.0, VestStartWk = 12 } } };
            s14b.Employees = new List<Employee>();
            SimEngine.WeeklyTick(s14a);
            SimEngine.WeeklyTick(s14b);
            ok(s14a.Cash == s14b.Cash,
                "ownership: the pool and its grants never enter the cash identity");

            // ── 15 · THE DRESSING: a board offer gains structure the same week
            GameState s15 = St();
            s15.Week = 30;
            s15.Mna = new MnaOffer { Buyer = "Larkspur Depot", Why = "milestone",
                Premium = 1.2, Price = 2400000, ExpiresWeek = 32 };
            SimOwnership.TickPost(s15, new WeeklyReport());
            ok(s15.BuyoutOffer.Count > 0 && DsS(s15.BuyoutOffer, "buyer") == "Larkspur Depot",
                "ownership: the board's offer gets dressed into the structured folder");
            ok(DiI(s15.BuyoutOffer, "cash") + DiI(s15.BuyoutOffer, "stock")
               + DiI(s15.BuyoutOffer, "earnout") == 2400000,
                "ownership: cash + stock + earnout compose exactly the headline");
            ok(DiI(s15.BuyoutOffer, "expires_wk") == 32 && s15.BuyoutOffer.ContainsKey("fishy_flags"),
                "ownership: the folder carries the board's clock and its computed flags");

            // ── 16 · THE WATERFALL: debts, prefs-or-convert, the vested split
            GameState s16 = St();
            s16.Week = 220;
            s16.FounderPct = 58.0;
            s16.Cofounders = new List<Cofounder> { new Cofounder { Name = "Mara Voss",
                Equity = 17.0, EquityDiluted = 17.0 } };
            s16.LoanPrincipal = 7350;
            s16.Esop = new Esop { PoolPct = 10.0, Granted = new List<EsopGrant> {
                new EsopGrant { EmpId = "june_park", Pct = 0.4, VestStartWk = 0 } } };
            s16.Instruments = new List<Instrument> { new Instrument { Kind = "priced",
                Holder = "Fern Capital", Amount = 400000, Cap = 0, Discount = 0.0,
                Rate = 0.0, MaturityWk = 0, Pct = 15.0, Prefs = 1.0, Protective = true,
                DragThreshold = 60.0, SignedWk = 31 } };
            Dictionary<string, object> wf = SimOwnership.Waterfall(s16, 4200000);
            ok(WfI(wf, "debts") == 7350,
                "ownership: the bank is paid first, always");
            int fernTake = RowTake(wf, "Fern Capital");
            ok(fernTake > 400000,
                "ownership: a 15% stake converts when it beats the 1x preference — computed");
            int expectedYou = Gd.RoundToInt((4200000.0 - 7350.0) * 58.0 / 90.4);
            ok(Gd.Absi(WfI(wf, "your_take") - expectedYou) <= 2,
                "ownership: your take is the pro-rata of the post-debt pot");
            ok(WfI(wf, "esop_take") > 0,
                "ownership: the vested ESOP holders get paid too");
            Dictionary<string, object> wfLow = SimOwnership.Waterfall(s16, 8000);
            ok(WfI(wfLow, "your_take") == 0,
                "ownership: below the breakeven the preferences eat everything");

            // ── 17 · THE POWERS: protective leans by its take; the drag row shows
            List<Dictionary<string, object>> pw = SimOwnership.Powers(s16, 4200000);
            bool fernYes = false;
            bool dragRow = false;
            foreach (var pd in pw)
            {
                object bl;
                bool blocks = pd.TryGetValue("blocks", out bl) && Convert.ToBoolean(bl);
                if (DsS(pd, "who") == "Fern Capital" && !blocks) fernYes = true;
                if (DsS(pd, "who") == "drag-along") dragRow = true;
            }
            ok(fernYes && dragRow,
                "ownership: the powers resolve from the instruments signed years early");

            // ── 18 · RESOLUTION: accept exits, negotiate once, decline cools
            GameState s18 = St();
            s18.Week = 30;
            s18.Mna = new MnaOffer { Buyer = "Larkspur Depot", Why = "milestone",
                Premium = 1.2, Price = 2400000, ExpiresWeek = 32 };
            SimOwnership.TickPost(s18, new WeeklyReport());
            int beforePrice = DiI(s18.BuyoutOffer, "headline");
            bool countered = false;
            object cv18;
            string neg = SimOwnership.BuyoutNegotiate(s18);
            countered = s18.BuyoutOffer.TryGetValue("countered", out cv18) && Convert.ToBoolean(cv18);
            ok(neg != "" && countered && DiI(s18.BuyoutOffer, "headline") != 0
               && s18.Mna.Price == DiI(s18.BuyoutOffer, "headline"),
                "ownership: one counter reprices the world once, mirrored to the courtship");
            ok(SimOwnership.BuyoutNegotiate(s18) == "",
                "ownership: the second counter finds no room");
            ok(beforePrice > 0, "ownership: the counter started from a real headline");
            string line18 = SimOwnership.BuyoutAccept(s18);
            ok(line18 != "" && s18.ExitValue > 0 && s18.HasFlag("acquired_exit")
               && s18.Mna == null && s18.BuyoutOffer.Count == 0,
                "ownership: accepting runs the waterfall and ends the run through the exit seam");
            GameState s19 = St();
            s19.Week = 30;
            s19.Hype = 40;
            s19.Mna = new MnaOffer { Buyer = "Vantiv Group", Why = "rival", Premium = 1.0,
                Price = 900000, ExpiresWeek = 32 };
            SimOwnership.TickPost(s19, new WeeklyReport());
            ok(SimOwnership.BuyoutDecline(s19) != "" && s19.Hype == 42
               && s19.Mna == null && s19.BuyoutOffer.Count == 0 && s19.MnaLastWeek == 30,
                "ownership: declining clears the table, the street hears, the cooldown starts");

            // ── 19 · ATTENTION: the rows name their desks and fit the ticker
            GameState s20 = St();
            s20.Week = 80;
            s20.Instruments = new List<Instrument> { new Instrument { Kind = "note",
                Holder = "R. Osei", Amount = 40000, Cap = 500000, Discount = 0.1,
                Rate = 0.003, MaturityWk = 70, Pct = 0.0, Prefs = 0.0, Protective = false,
                DragThreshold = 0.0, SignedWk = 20 } };
            s20.BuyoutOffer = new Dictionary<string, object> { { "buyer", "X" },
                { "headline", 100 }, { "cash", 100 }, { "stock", 0 }, { "earnout", 0 },
                { "expires_wk", 81 }, { "fishy_flags", new List<string>() } };
            s20.Recruitment = new Recruitment
            {
                Roles = new List<Dictionary<string, object>> { new Dictionary<string, object> {
                    { "id", "r" }, { "seat", "sales" }, { "band_lo", 1 }, { "band_hi", 2 },
                    { "advert_wk", 0 }, { "opened_wk", 1 } } },
                Candidates = new List<Dictionary<string, object>> { new Dictionary<string, object> {
                    { "id", "c9" }, { "role_id", "r" }, { "name", "Priya Nair" }, { "ask", 900 },
                    { "profile", "mercenary" }, { "skill", 3 }, { "stage", "offer" },
                    { "arrived_wk", 79 } } },
                OffersOut = new List<Dictionary<string, object>> { new Dictionary<string, object> {
                    { "candidate_id", "c9" }, { "cash_wk", 900 }, { "options_pct", 0.0 },
                    { "expires_wk", 82 }, { "sent_wk", 80 } } },
            };
            List<AttentionItem> rows20 = SimOwnership.Attention(s20);
            var desks = new HashSet<string>();
            bool fits = true;
            foreach (AttentionItem r in rows20)
            {
                desks.Add(r.Desk);
                if (r.Label.Length > 40) fits = false;
            }
            ok(desks.Contains("the raise") && desks.Contains("the offer") && desks.Contains("recruitment"),
                "ownership: matured notes, live buyouts and offers-out all raise their hands");
            ok(fits, "ownership: every attention label fits the 40-char ticker");

            // ── 20 · REPLAY: a full ownership state ticks identically twice
            GameState r1 = St();
            GameState r2 = St();
            foreach (GameState sv in new[] { r1, r2 })
            {
                sv.Esop = new Esop { PoolPct = 10.0, Granted = new List<EsopGrant>() };
                sv.Instruments = new List<Instrument> { new Instrument { Kind = "safe",
                    Holder = "Fern Capital", Amount = 150000, Cap = 4000000, Discount = 0.2,
                    Rate = 0.0, MaturityWk = 0, Pct = 0.0, Prefs = 0.0, Protective = false,
                    DragThreshold = 0.0, SignedWk = 9 } };
                sv.Recruitment = new Recruitment
                {
                    Roles = new List<Dictionary<string, object>> { new Dictionary<string, object> {
                        { "id", "r1" }, { "seat", "sales" }, { "band_lo", 1000 },
                        { "band_hi", 1500 }, { "advert_wk", 60 }, { "opened_wk", 12 } } },
                    Candidates = new List<Dictionary<string, object>>(),
                    OffersOut = new List<Dictionary<string, object>>(),
                };
                sv.RaiseState = new RaiseState { Stages = new List<Dictionary<string, object>>(),
                    InterestScore = 0.0, Active = true, FounderTimeTax = 0.3 };
                SimEngine.WeeklyTick(sv);
            }
            ok(r1.Cash == r2.Cash && r1.Traction == r2.Traction
               && r1.Recruitment.Candidates.Count == r2.Recruitment.Candidates.Count,
                "ownership: the ownership cluster replays exactly on one seed");
        }
    }
}
