using System;
using System.Collections.Generic;
using System.Globalization;
using Runway.Core;

namespace Runway.CoreTests
{
    /// <summary>
    /// LANE SUITE — catalog. Spec: docs/design/01-catalog.md section 9 (twin pins).
    ///
    /// Program.cs calls Run after the engine's own checks and hands over `ok`, the
    /// same assert the whole suite uses: ok(condition, "what this pins").
    ///
    /// The porting law: a check lands FIRST in game/tests/lanes/test_catalog.gd,
    /// then here in the same order. Same checks, same order, same logic — the two
    /// engines do not share PRNG internals, so never pin a draw across them, only
    /// behaviour.
    /// </summary>
    public static class CatalogTests
    {
        static GameState NewState()
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

        static string S(object v)
        {
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public static void Run(Action<bool, string> ok)
        {
            // ── P1 — THE ITEMIZED TRUTH SYNCS AND CLAMPS. Totals can never drift
            // from their receipts, and receipts can never exceed the world.
            var it = new Offer
            {
                Name = "boxed lunch", Unit = "per order", FairPrice = 70.0,
                Elasticity = 2.0, Weight = 1.0, Price = 0.0,
                CostLines = new List<CostLine>
                {
                    new CostLine { Label = "ingredients", Amount = 12.0 },
                    new CostLine { Label = "packaging", Amount = 10.0 },
                },
                FixedLines = new List<CostLine>
                {
                    new CostLine { Label = "kitchen license", Amount = 30.0 },
                },
            };
            SimEngine.SyncOfferCosts(it);
            ok(Math.Abs(it.UnitCost - 22.0) < 0.01, "unit_cost = sum of variable lines (22)");
            ok(Math.Abs(it.FixedWk - 30.0) < 0.01, "fixed_wk = sum of fixed lines (30)");
            var greedy = new Offer
            {
                FairPrice = 70.0,
                CostLines = new List<CostLine>
                {
                    new CostLine { Label = "a", Amount = 900.0 },
                    new CostLine { Label = "b", Amount = 900.0 },
                    new CostLine { Label = "c", Amount = 900.0 },
                },
            };
            SimEngine.SyncOfferCosts(greedy);
            ok(Math.Abs(greedy.CostLines[0].Amount - 35.0) < 0.01,
                "a hostile line clamps to half of fair (35)");
            ok(Math.Abs(greedy.UnitCost - 63.0) < 0.01,
                "the variable total clamps to 0.9x fair (63)");

            // ── P2 — THE offer_fixed LANE EXISTS AND THE P&L IDENTITY HOLDS. A
            // standing tool cost is a real lane of burn, not a silent subtraction.
            GameState fx = NewState();
            fx.Traction = 10;
            fx.SetFlag("launched");
            fx.Offers = new List<Offer>
            {
                new Offer
                {
                    Name = "s", Unit = "per session", Price = 70.0, FairPrice = 70.0,
                    UnitCost = 20.0, Weight = 1.0, FixedWk = 120.0,
                    FixedLines = new List<CostLine>
                    {
                        new CostLine { Label = "booking tool", Amount = 120.0 },
                    },
                },
            };
            SimEngine.WeeklyTick(fx);
            Pnl pnlFx = fx.LastPnl;
            ok(pnlFx.OfferFixed == 120, "the catalog's fixed lane bills $120");
            ok(pnlFx.Net == pnlFx.Revenue - pnlFx.Burn - pnlFx.LiabilitiesWk
                            - pnlFx.Interest - pnlFx.Tax,
                "the P&L identity balances with the offer_fixed lane inside burn");

            // ── P3 — LEARNING CUTS THE VARIABLE TOTAL ONLY; FIXED NEVER LEARNS. A
            // license does not get cheaper because you served customers.
            GameState lcS = NewState();
            lcS.ServedTotal = 1000;
            lcS.Offers = new List<Offer>
            {
                new Offer
                {
                    Name = "s", Unit = "per session", Price = 70.0, FairPrice = 70.0,
                    UnitCost = 22.0, Weight = 1.0, FixedWk = 30.0,
                },
            };
            double cpc = SimEngine.OffersCogsPerCustomer(lcS);
            ok(cpc > 14.2 && cpc < 14.6, "learning serves 22 at ~14.4 (x0.655)");
            ok(Math.Abs(SimEngine.OffersFixedWk(lcS) - 30.0) < 0.01,
                "fixed lines never learn (30)");

            // ── P4 — HOSTILE NUMBERS CLAMP; THE ERA SHELF REFUSES THE OVERFLOW. The
            // door narrows before the engine's own clamps ever see the terms.
            GameState capS = NewState();
            capS.Era = "coworking";                    // EraOfferCap 3
            Offer o1 = SimCatalog.AddOffer(capS, "big thing", "per unit",
                900000.0, 900000.0, 99.0, 99.0);
            ok(o1 != null && o1.FairPrice == 50000.0 && o1.UnitCost <= 45000.0
                          && o1.Elasticity == 3.0 && o1.Weight <= 3.0,
                "hostile terms pass every clamp");
            SimCatalog.AddOffer(capS, "b", "per order", 40.0, 10.0, 2.0, 1.0);
            SimCatalog.AddOffer(capS, "c", "per order", 40.0, 10.0, 2.0, 1.0);
            Offer o4 = SimCatalog.AddOffer(capS, "d", "per order", 40.0, 10.0, 2.0, 1.0);
            ok(o4 == null && capS.Offers.Count == 3,
                "coworking shelves three offers, the fourth is refused");

            // ── P5 — THE KEYLESS DRAFT IS SEEDED, ITEMIZED, AND IN BAND. Same seed,
            // same week, same draft: a replay shelves the identical offer.
            GameState dr = NewState();                 // seed 42, week 5, SMB
            Offer d1 = SimCatalog.DraftTerms(dr, "a weekend workshop");
            Offer d2 = SimCatalog.DraftTerms(dr, "a weekend workshop");
            ok(d1.Unit == "per session" && Math.Abs(d1.FairPrice - d2.FairPrice) < 0.01,
                "the keyless draft is seeded and repeatable");
            ok(d1.FairPrice >= 32.0 && d1.FairPrice <= 52.0,
                "an SMB draft prices inside the jittered band (40x[0.8,1.3])");
            ok(d1.CostLines != null && d1.CostLines.Count == 2
                && d1.FixedLines != null && d1.FixedLines.Count == 1,
                "the draft itemizes: 2 variable lines + 1 fixed line");

            // ── P6 — A CONSCIOUS GIVEAWAY EARNS $0 AND STILL COSTS TO SERVE. The
            // lesson the free tier teaches: revenue is a choice, COGS is not.
            GameState fr = NewState();
            fr.Traction = 50;
            fr.SetFlag("launched");
            fr.Offers = new List<Offer>
            {
                new Offer
                {
                    Name = "free tier", Unit = "per session", Price = 0.0,
                    PriceSet = true, FairPrice = 70.0, UnitCost = 18.0, Weight = 1.0,
                },
            };
            WeeklyReport rFr = SimEngine.WeeklyTick(fr);
            ok(rFr.Revenue == 0, "free on purpose earns $0");
            // EXACT, and immune to another lane's adoption tuning: COGS is every
            // customer the giveaway holds, times what one of them costs to serve,
            // learning x1.0 at a standing start.
            ok(fr.Traction > 50 && fr.LastPnl.Cogs == Gd.RoundToInt(fr.Traction * 18.0),
                "the giveaway grew the base and paid COGS on every one of them ("
                    + S(fr.Traction) + " x $18 = $" + S(fr.LastPnl.Cogs) + ")");

            // ── P7 — THE SHELF IS A WALLET, AND A LOSING PRICE RAISES ITS HAND.
            // The count cap is not the only door: a customer's weekly budget is
            // finite, so three flagship-weight offers fill it and the next is refused.
            GameState wal = NewState();
            wal.Era = "office";          // EraOfferCap 5 — count is not the binding limit
            SimCatalog.AddOffer(wal, "one", "per order", 40.0, 10.0, 2.0, 3.0);
            SimCatalog.AddOffer(wal, "two", "per order", 40.0, 10.0, 2.0, 3.0);
            Offer w3 = SimCatalog.AddOffer(wal, "three", "per order", 40.0, 10.0, 2.0, 3.0);
            ok(w3 == null && Math.Abs(SimCatalog.ShelfWeight(wal) - 6.0) < 0.01,
                "sum-of-weight 6.0 fills the wallet and the shelf refuses the next offer");
            // a shelf that arrived over the cap by another road is trimmed, with a receipt
            GameState tam = NewState();
            tam.Offers = new List<Offer>
            {
                new Offer { Name = "a", Unit = "per order", FairPrice = 40.0, Weight = 5.0 },
                new Offer { Name = "b", Unit = "per order", FairPrice = 40.0, Weight = 5.0 },
            };
            var tamRep = new WeeklyReport();
            SimCatalog.TickPre(tam, tamRep);
            ok(SimCatalog.ShelfWeight(tam) <= 6.001 && tamRep.Lines.Count == 1,
                "a tampered shelf is trimmed to sum 6.0 and says so in the week's receipts");
            // the one lesson a founder must not miss — and the one that is NOT a mistake
            GameState los = NewState();
            los.Offers = new List<Offer>
            {
                new Offer
                {
                    Name = "underwater", Unit = "per order", Price = 10.0, PriceSet = true,
                    FairPrice = 70.0, UnitCost = 18.0, Weight = 1.0,
                },
            };
            List<AttentionItem> losRows = SimCatalog.Attention(los);
            GameState gift = NewState();
            gift.Offers = new List<Offer>
            {
                new Offer
                {
                    Name = "free tier", Unit = "per order", Price = 0.0, PriceSet = true,
                    FairPrice = 70.0, UnitCost = 18.0, Weight = 1.0,
                },
            };
            ok(losRows.Count == 1 && losRows[0].Key == "losing_price"
                && losRows[0].Severity == 2 && losRows[0].Label.Length <= 40
                && SimCatalog.Attention(gift).Count == 0,
                "a price under its variable cost raises a warn; a chosen giveaway does not");
        }
    }
}
