#if !RUNWAY_FX_USHOTS_OFF
using System.Collections.Generic;
using UnityEngine;
using Runway.Core;
using Runway.Game;

namespace Runway.App
{
    /// <summary>
    /// THE SAME COMPANIES, TRANSCRIBED — every fixture the Godot harnesses build,
    /// field for field, so the two sets of pictures are photographs of one state.
    ///
    ///   · DRIFTDECK  (new_screens_shot.gd) the fake save in slot 2 that puts CONTINUE
    ///     on the title menu and one filled dossier in the slot table.
    ///   · FERNORA    (new_screens_shot.gd) the wellness company the book intro opens
    ///     on: seeded beliefs, a market line, one investor, one rival.
    ///   · PIVOTFLOW  (binder_shot.gd) week 14, lively on purpose — customers, crew,
    ///     debt, rivals, statuses, a loan, two offers, thirteen weeks of history — so
    ///     all nine tabs have something to lay out.
    ///
    /// WHERE THE STATE HAS TO GO. The Godot screens are handed a GameState
    /// (`bk.setup(st2)`, `b.setup(s)`); the Unity twins read `RunDriver.Current.State`
    /// instead. So a fixture is installed by starting a fresh run through the driver's
    /// own public door and then writing the same fields onto the state it made — no
    /// private seam, and every screen that reaches for the run finds this one.
    /// </summary>
    public static class UnityShotsFixtures
    {
        // ══ DRIFTDECK — the save the title menu reads ═══════════════════════════

        public const int DriftdeckSlot = 2;

        /// THE HARNESS DOES NOT EAT A COMPANY. `new_screens_shot.gd` writes its fake
        /// save straight over slot 2 and then calls `clear_run()`, which in this port
        /// would delete a real saved run the player might be halfway through. The
        /// occupant is read out first and put back by ClearDriftdeckSave — the same
        /// leave-it-as-you-found-it rule howto_shot.gd keeps for its seen mark.
        static string _slotBackup;
        static int _activeBackup = -1;

        /// new_screens_shot.gd: active slot 2, company Driftdeck, founder Zara Duval,
        /// week 7, seed 777 — written before the title is built so CONTINUE exists.
        public static void WriteDriftdeckSave()
        {
            _slotBackup = RunwayPaths.ReadAllTextOrEmpty(SaveSlots.Path(DriftdeckSlot));
            _activeBackup = SaveSlots.ActiveSlot;

            var s = new GameState
            {
                CompanyName = "Driftdeck",
                FounderName = "Zara Duval",
                Week = 7,
            };
            var rec = new RunRecord { SeedValue = 777 };
            SaveSlots.ActiveSlot = DriftdeckSlot;
            if (!RunSave.Save(DriftdeckSlot, s, rec))
                Debug.LogError("USHOTS could not write the Driftdeck save — the title menu "
                               + "will show NEW GAME only and n2_slot_panel will be empty.");
        }

        /// The twin of `SaveSystem.clear_run()` right after the slot panel is shot —
        /// and, if slot 2 already held a run, the line that gives it back.
        public static void ClearDriftdeckSave()
        {
            SaveSlots.Clear(DriftdeckSlot);
            if (!string.IsNullOrEmpty(_slotBackup))
            {
                if (RunwayPaths.WriteAllText(SaveSlots.Path(DriftdeckSlot), _slotBackup))
                    Debug.Log("USHOTS put slot " + DriftdeckSlot + "'s own run back.");
                else
                    Debug.LogError("USHOTS COULD NOT RESTORE slot " + DriftdeckSlot
                                   + " — its saved run is gone. Contents were "
                                   + _slotBackup.Length + " chars.");
            }
            _slotBackup = null;
            if (_activeBackup > 0) SaveSlots.ActiveSlot = _activeBackup;
            _activeBackup = -1;
        }

        // ══ FERNORA — the book intro's world ════════════════════════════════════

        /// The entry `bk.feed_entry(...)` hands the page, verbatim.
        public const string FernoraEntry =
            "The key sticks, then gives. I sign the lease on the hood of a borrowed car and "
            + "carry the first box in alone. Two treatment rooms, a reception desk the last "
            + "tenant abandoned, and a smell of paint that will outlast my savings. We are "
            + "promising tired people one calm hour that starts on time. It will cost eleven "
            + "thousand a month before a single towel is warm. Tonight that number looks "
            + "enormous, and I write it down anyway so tomorrow it looks like a plan.";

        public static GameState InstallFernora()
        {
            GameState s = FreshRunState();
            if (s == null) return null;
            s.CompanyName = "Fernora";
            // biz_what / biz_who are left at their defaults exactly as the .gd does —
            // the harness sets theta directly instead of going through the shape page.
            s.Theta = SimEngine.DefaultTheta("Service", "Consumer");
            SimEngine.SeedBeliefs(s);
            s.SetMeta("market_line", "a market that books calm by the hour");
            s.Investors = new List<Investor>
            {
                new Investor
                {
                    Name = "Steamline Partners",
                    Archetype = "the operator VC",
                    Thesis = "wellness works when it sells a repeatable escape to people "
                             + "who cannot leave their jobs",
                },
            };
            s.Rivals = new List<Rival>
            {
                new Rival
                {
                    Name = "Brume House",
                    Strength = 45.0,
                    What = "polished urban thermal spas for office workers",
                },
            };
            return s;
        }

        // ══ PIVOTFLOW — every binder tab with something on it ═══════════════════

        public static GameState InstallPivotflow()
        {
            GameState s = FreshRunState();
            if (s == null) return null;
            s.SimSeed = 99;
            s.Week = 14;
            s.Cash = 31500;
            s.Traction = 210;
            s.Product = 58;
            s.Morale = 44;
            s.Hype = 61;
            s.FounderName = "Lena Voss";
            s.CompanyName = "Pivotflow";
            s.BizWhat = "Software";
            s.BizWho = "SMB";
            s.Theta = SimEngine.DefaultTheta(s.BizWhat, s.BizWho);
            s.AnalyticsLevel = 1;
            s.TechDebt = 55.0;
            s.Exhaustion = 3;
            s.LoanPrincipal = 12000;
            s.MarketingBudget = 400;
            s.PriceMult = 1.1;
            s.FounderPct = 61.0;

            s.Offers = new List<Offer>
            {
                new Offer
                {
                    Name = "standard session", Unit = "per session", FairPrice = 70.0,
                    Elasticity = 2.6, UnitCost = 18.0, Price = 0.0, Weight = 0.7,
                },
                new Offer
                {
                    Name = "premium package", Unit = "per package", FairPrice = 180.0,
                    Elasticity = 2.0, UnitCost = 55.0, Price = 500.0, Weight = 0.3,
                },
            };

            // The .gd writes `{"role": 0, "commitment": 0}` — indices into
            // founder_draft_screen.gd's ROLES / COMMITMENTS. Core's Cofounder is typed,
            // so the fixture carries what those indices MEAN: role 0 = "Sales",
            // commitment 0 = "Full-time".
            s.Cofounders = new List<Cofounder>
            {
                new Cofounder
                {
                    Name = "Nico Ferreira", Role = "Sales", Commitment = "Full-time",
                    Equity = 25.0, Vesting = "4y/1y cliff",
                },
            };
            s.Employees = new List<Employee>
            {
                new Employee { Name = "Priya Voss", Role = "engineer", Salary = 1500, Burnout = 30 },
            };

            s.Investors = new List<Investor>
            {
                new Investor
                {
                    Name = "Harborline Syndicate",
                    Archetype = "the operator VC",
                    Thesis = "belgian wellness works when it sells a repeatable three-hour "
                             + "escape to people who cannot leave their jobs, not a lifestyle pivot",
                    Trait = "carries a stopwatch and notices queue lengths before introductions",
                    Coords = new List<double> { -0.3, 0.2 },
                },
                new Investor
                {
                    Name = "Soft Peak Capital",
                    Archetype = "the thesis tourist",
                    Thesis = "the interesting part of this market is the ritual: work, rain, "
                             + "traffic, then warmth — package the ritual, not the room",
                    Trait = "arrives with a photographer and calls everyone part of the narrative",
                    Coords = new List<double> { 0.1, 0.6 },
                },
            };

            // LIVE-LENGTH text on purpose: the street tab once stacked itself the first
            // week the LLM wrote three full-sentence tactics (owner photo).
            s.Rivals = new List<Rival>
            {
                new Rival
                {
                    Name = "Solacely", Strength = 45.0,
                    What = "legacy suite via trade associations",
                    WeeksSinceMove = 1,
                    Tactics = new List<string>
                    {
                        "sells monthly rain-recovery memberships that renew before anyone reads the invoice",
                        "gives local employers discounted weekday vouchers, filling the quiet hours",
                        "offers free prosecco on thursday evenings, because hydration",
                    },
                },
                new Rival
                {
                    Name = "Eterna", Strength = 25.0,
                    What = "memorial pages with aggressive SEO",
                    WeeksSinceMove = 3,
                    Tactics = new List<string>
                    {
                        "undercuts massage prices with tightly timed 25-minute slots",
                        "partners with beauty influencers for carefully cropped testimonials",
                        "runs last-minute WhatsApp deals whenever the steam rooms sit empty",
                    },
                },
            };

            SimEngine.AddStatus(s, "investor_pressure", 2);
            SimEngine.AddStatus(s, "word_of_mouth", 3);
            SimEngine.AddClock(s, 3, "the bridge loan comes due");

            // thirteen weeks on the charts. `product` is in the .gd dict and read by
            // nothing — binder.gd's _series only ever asks for cash, customers, morale,
            // debt and hype — and Core's MetricSnapshot has no field for it.
            s.MetricHistory = new List<MetricSnapshot>();
            for (int w = 1; w < 14; w++)
            {
                s.MetricHistory.Add(new MetricSnapshot
                {
                    Wk = w,
                    Cash = 60000 - w * 2200,
                    Customers = (int)System.Math.Pow(w, 1.7),   // double, like GDScript's pow()
                    Morale = 70 - w * 2,
                });
            }
            return s;
        }

        // ══ the one seam onto the run ═══════════════════════════════════════════

        /// A blank run through the driver's own public door, so every screen that reads
        /// `RunDriver.Current.State` finds the fixture that is about to be written into it.
        static GameState FreshRunState()
        {
            RunDriver driver = RunDriver.Current;
            if (driver == null)
            {
                Debug.LogError("USHOTS: no RunDriver — the book intro and the binder read the "
                               + "run through it, so those shots would photograph an empty page.");
                return null;
            }
            driver.BeginFreshRun(false);
            GameState s = driver.State;
            if (s == null)
                Debug.LogError("USHOTS: BeginFreshRun left no state behind.");
            return s;
        }
    }
}
#endif
