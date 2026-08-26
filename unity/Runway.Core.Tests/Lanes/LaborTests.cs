using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Runway.Core;

namespace Runway.CoreTests
{
    /// <summary>
    /// LANE SUITE — labor. Spec: docs/design/02-labor-market.md section 9 (the six twin pins).
    ///
    /// Program.cs calls Run after the engine's own checks and hands over `ok`,
    /// the same assert the whole suite uses: ok(condition, "what this pins").
    ///
    /// The porting law: a check lands FIRST in game/tests/lanes/test_labor.gd,
    /// then here in the same order. Same checks, same order, same logic — the
    /// two engines do not share PRNG internals, so nothing below pins a draw
    /// across them, only behaviour.
    /// </summary>
    public static class LaborTests
    {
        /// <summary>The lane's own fixture: a coworking company, because the
        /// market does not open until there is a company to open it for.</summary>
        static GameState NewState(int seed = 42)
        {
            var s = new GameState();
            s.SimSeed = seed;
            s.Week = 5;
            s.Era = "coworking";
            s.Cash = 200000;
            s.Traction = 40;
            s.Product = 50;
            s.Morale = 60;
            s.Hype = 0;
            s.BizWhat = "Software";
            s.BizWho = "SMB";
            s.Theta = SimEngine.DefaultTheta(s.BizWhat, s.BizWho);
            return s;
        }

        static List<string> Tick(GameState s, int weeks)
        {
            var lines = new List<string>();
            for (int i = 0; i < weeks; i++)
            {
                s.Week += 1;
                WeeklyReport rep = SimEngine.WeeklyTick(s);
                lines.AddRange(rep.Lines);
                lines.AddRange(rep.Events);
            }
            return lines;
        }

        static string Joined(List<string> lines)
        {
            return string.Join("\n", lines.ToArray());
        }

        public static void Run(Action<bool, string> ok)
        {
            // ── PIN 1: THE FLOOR, AND DETERMINISM ────────────────────────────
            // Below 80% of the market rate nobody applies at all — the
            // reservation wage is a cliff, not a slope, and the desk has to be
            // able to teach that.
            int mkEng = SimLabor.MarketSalary("engineer", "coworking");
            GameState quiet = NewState();
            ok(SimLabor.OpenRoleAt(quiet, "engineer", (int)(mkEng * 0.7)),
                "a role opens at 0.70x market");
            Tick(quiet, 4);
            ok(quiet.Applicants.Count == 0,
                "nobody applies below the 0.8x reservation wage (4 wks of silence)");

            GameState loud = NewState();
            loud.Hype = 40;
            loud.Morale = 70;
            SimLabor.OpenRoleAt(loud, "engineer", mkEng);
            Tick(loud, 3);
            ok(Cands(loud).Count >= 1, "an advert AT the market rate draws people");
            GameState twin = NewState();
            twin.Hype = 40;
            twin.Morale = 70;
            SimLabor.OpenRoleAt(twin, "engineer", mkEng);
            Tick(twin, 3);
            bool same = Cands(twin).Count == Cands(loud).Count;
            if (same)
            {
                for (int i = 0; i < Cands(loud).Count; i++)
                {
                    var a = Cands(loud)[i];
                    var b = Cands(twin)[i];
                    if (CandS(a, "name") != CandS(b, "name")
                        || CandI(a, "skill") != CandI(b, "skill")
                        || CandI(a, "ask") != CandI(b, "ask"))
                    {
                        same = false;
                        break;
                    }
                }
            }
            ok(same, "the same seed draws the same people, name for name");

            // ── PIN 2: ASK BOUNDS + THE DECAY WINDOW ─────────────────────────
            // Morale stays under 70 here on purpose: the referral discount is a
            // real 0.95x on the first arrival of a week, and it would sit outside
            // the band this pin exists to police.
            GameState flood = NewState();
            flood.Hype = 80;
            SimLabor.OpenRoleAt(flood, "engineer", (int)(mkEng * 1.5));
            Tick(flood, 3);
            ok(Cands(flood).Count >= 6, "an advert at 1.5x market floods the desk");
            // the union of the two documented curves (labor first-week draw and
            // recruitment band interpolation, rounded to 10) — +-10 slack.
            bool bounded = true;
            foreach (var a in Cands(flood))
            {
                int sk = CandI(a, "skill");
                if (sk < 1 || sk > 5)
                {
                    bounded = false;
                    break;
                }
                double bse = mkEng * SimLabor.AskMult(sk);
                double blo = mkEng * 0.85, bhi = mkEng * 1.25;
                double recBase = blo + (bhi - blo) * (sk - 1) / 4.0;
                double loU = Math.Min(bse * 0.90, recBase * 0.95) - 10.0;
                double hiU = Math.Max(bse * 1.15, recBase * 1.12) + 10.0;
                int ask = CandI(a, "ask");
                if (ask < loU || ask > hiU)
                {
                    bounded = false;
                    break;
                }
            }
            ok(bounded, "every ask sits on the skill curve within its noise band");

            GameState patient = NewState();
            int w0 = patient.Week;
            patient.Applicants.Add(new Applicant
            {
                Name = "Halden Rook", Role = "engineer", Skill = 4, Ask = mkEng,
                Quirk = "waits", OneLiner = "", AppliedWeek = w0, Source = "inbound",
            });
            Tick(patient, 1);
            bool here1 = HasApplicant(patient, "Halden Rook");
            Tick(patient, 1);
            ok(here1 && HasApplicant(patient, "Halden Rook"),
                "two weeks of grace: a fresh candidate never evaporates");
            List<string> goneLines = Tick(patient, 3);
            ok(!HasApplicant(patient, "Halden Rook"),
                "the offer shelf-life is hard: gone by week five, whatever the seed");
            ok(Joined(goneLines).Contains("Halden Rook"),
                "and the desk printed why they stopped waiting");

            // ── PIN 3: THE ERA GATES ─────────────────────────────────────────
            ok(SimLabor.SeveranceWeeks("garage", 10) == 1
               && SimLabor.SeveranceWeeks("coworking", 10) == 2,
                "severance: a garage handshake is 1 wk, a coworking exit 2");
            ok(SimLabor.SeveranceWeeks("office", 10) == 2
               && SimLabor.SeveranceWeeks("office", 30) == 3
               && SimLabor.SeveranceWeeks("office", 100) == 4
               && SimLabor.SeveranceWeeks("hq", 10) == 3,
                "severance bands by tenure at office, and the hq floor is 3");
            ok(!SimLabor.RoleUnlocked("manager", "coworking")
               && SimLabor.RoleUnlocked("manager", "floor"),
                "managers unlock with the floor they manage");
            ok(!SimLabor.RoleUnlocked("designer", "garage")
               && SimLabor.RoleUnlocked("designer", "coworking"),
                "specialists unlock when there is a company to specialise in");

            GameState thin = NewState();
            thin.Era = "floor";
            for (int i = 0; i < 12; i++)
            {
                thin.Employees.Add(new Employee { Name = "IC", Role = "engineer",
                    Salary = 2600, Burnout = 10, Skill = 3, HiredWeek = 1 });
            }
            ok(Gd.Absf(SimLabor.SpanMult(thin) - 0.5) < 0.001,
                "12 heads and no manager: the floor runs at the 50% floor");
            for (int i = 0; i < 2; i++)
            {
                thin.Employees.Add(new Employee { Name = "Mgr", Role = "manager",
                    Salary = 3000, Burnout = 10, Skill = 3, HiredWeek = 1 });
            }
            ok(Gd.Absf(SimLabor.SpanMult(thin) - 1.0) < 0.001,
                "two managers cover twelve reports: span is whole again");
            thin.Era = "office";
            ok(Gd.Absf(SimLabor.SpanMult(thin) - 1.0) < 0.001,
                "below the floor era the founder manages everyone: span is always 1.0");

            GameState lean = OfficeTwin(0);
            GameState fed = OfficeTwin(1000);
            Tick(lean, 4);
            Tick(fed, 4);
            ok(lean.Morale < fed.Morale,
                "a real office expects benefits: the unfunded twin's morale is lower");

            // ── PIN 4: THE HIRE FLOW, AND SKILL PAYS ─────────────────────────
            GameState hiring = NewState();
            SimLabor.OpenRoleAt(hiring, "engineer", mkEng);
            hiring.Applicants.Add(new Applicant
            {
                Name = "Mara Voss", Role = "engineer", Skill = 5, Ask = 2400,
                Quirk = "negotiates via long silences", OneLiner = "",
                AppliedWeek = hiring.Week, Source = "inbound",
            });
            Dictionary<string, object> hired = SimLabor.HireApplicant(hiring, 0);
            ok(hired != null && hiring.Pipeline.Count == 1
               && hiring.Pipeline[0].Salary == 2400 && hiring.OpenRoles.Count == 0,
                "the ASK is the contract, and the last seat closes the role");
            hiring.Week += 1;
            WeeklyReport rep1 = SimEngine.WeeklyTick(hiring);
            ok(hiring.Employees.Count == 0 && rep1.Burn >= 2400,
                "onboarding is paid before it is productive");
            hiring.Week += 1;
            SimEngine.WeeklyTick(hiring);
            ok(hiring.Employees.Count == 1 && hiring.Employees[0].Skill == 5
               && hiring.Employees[0].HiredWeek == hiring.Week,
                "the graduate carries the skill that was hired, and a tenure clock");

            GameState closers = SalesTwin(5);
            GameState duds = SalesTwin(1);
            closers.Week += 1;
            duds.Week += 1;
            WeeklyReport repHi = SimEngine.WeeklyTick(closers);
            WeeklyReport repLo = SimEngine.WeeklyTick(duds);
            ok(repHi.Adds > repLo.Adds,
                "two closers land more than two duds: skill IS the capacity");

            // ── PIN 5: THE FIRE RECEIPT ──────────────────────────────────────
            GameState boss = FireTwin();
            int moraleBefore = boss.Morale;
            Dictionary<string, object> slip = SimLabor.FireEmployee(boss, 0);
            ok(boss.Employees.Count == 0 && slip != null
               && (int)slip["severance"] == 3000 && boss.Morale == moraleBefore - 8,
                "letting go: 2 wks of $1,500 owed, and the room takes the -8");
            GameState control = FireTwin();
            control.Employees.RemoveAt(0);        // the same roster, no invoice
            boss.Week += 1;
            control.Week += 1;
            SimEngine.WeeklyTick(boss);
            SimEngine.WeeklyTick(control);
            Pnl pnl = boss.LastPnl;
            Pnl plain = control.LastPnl;
            ok(pnl != null && plain != null && pnl.Severance == 3000
               && pnl.Burn >= plain.Burn + 3000,
                "the invoice lands on NEXT week's books, in its own P&L lane");
            ok(pnl != null && pnl.Net == pnl.Revenue - pnl.Burn - pnl.LiabilitiesWk
               - pnl.Interest - pnl.Tax,
                "and the P&L identity still holds the week severance is paid");

            // ── PIN 6: THE UNDERPAY LADDER, AND THE POACH QUERY ──────────────
            GameState stiffed = NewState();
            stiffed.Morale = 70;
            stiffed.Employees.Add(new Employee { Name = "Nico Bell", Role = "engineer",
                Salary = 750, Burnout = 10, Skill = 3, HiredWeek = 1 });
            stiffed.Week += 1;
            SimEngine.WeeklyTick(stiffed);
            ok(stiffed.Employees.Count == 1 && stiffed.Employees[0].WantsRaise,
                "paid half of fair: the ask is immediate, not a dice roll");
            Dictionary<string, object> mark = SimLabor.PoachTarget(stiffed);
            ok(mark != null && (string)mark["name"] == "Nico Bell"
               && (double)mark["gap_pct"] >= 25.0,
                "a raider bids for exactly the person paid furthest under their worth");
            List<string> quitLines = Tick(stiffed, 3);
            ok(stiffed.Employees.Count == 0 && Joined(quitLines).Contains("resigned"),
                "three weeks of being ignored ends in a resignation, with the ratio");

            GameState paid = NewState();
            paid.Employees.Add(new Employee { Name = "Priya Ines", Role = "engineer",
                Salary = 1350, Burnout = 10, Skill = 3, HiredWeek = 1 });
            ok(SimLabor.PoachTarget(paid) == null,
                "nobody bids for a person already paid near the market");

            // ── THE SEAMS OTHER LANES CALL ───────────────────────────────────
            // The dressing plumbing and the poach handoff are cross-lane
            // contracts, and an untested contract between two lanes is the one
            // that breaks.
            GameState seam = SeamState();
            JObject payload = SimLabor.DressingPayload(seam);
            ok(payload != null && ((JArray)payload["candidates"]).Count == 2
               && payload["taken_names"].ToString().Contains("Priya Voss"),
                "the dressing payload carries THIS week's arrivals, and the names already taken");
            var good = new JArray
            {
                new JObject { {"name","Mara Voss"}, {"quirk","negotiates via long silences"},
                              {"one_liner","I fix what you broke."} },
                new JObject { {"name","Bo Halloway"}, {"quirk","sends follow-ups at 5am sharp"},
                              {"one_liner","Available, alarmingly."} },
            };
            ok(SimLabor.DressApplicants(seam, good) == 2
               && seam.Applicants[0].Name == "Mara Voss" && seam.Applicants[0].Ask == 1500
               && seam.Applicants[0].Skill == 3,
                "a dressing reply changes the words and never a number");
            var shortReply = new JArray { new JObject { {"name","Only One"}, {"quirk",""}, {"one_liner",""} } };
            var thief = new JArray
            {
                new JObject { {"name","Priya Voss"}, {"quirk",""}, {"one_liner",""} },
                new JObject { {"name","Someone"}, {"quirk",""}, {"one_liner",""} },
            };
            ok(SimLabor.DressApplicants(seam, shortReply) == 0
               && SimLabor.DressApplicants(seam, thief) == 0
               && seam.Applicants[0].Name == "Mara Voss",
                "a reply that miscounts or steals a name is discarded whole; the pool stands");
            Dictionary<string, object> mark2 = SimLabor.PoachTarget(seam);
            ok(mark2 != null && (int)mark2["index"] == 0 && (int)mark2["market_salary"] == 1875
               && (double)mark2["pay_gap"] >= 0.2,
                "the poach target answers in the shape the rivals lane asks for");
            SimLabor.PoachFailed(seam, 0, "Vantage");
            ok(seam.Employees[0].WantsRaise && seam.Employees[0].AskedWeek == seam.Week - 2
               && Gd.ToInt(seam.GetMetaF("poach_wk", -1.0)) == seam.Week,
                "a FAILED poach hardens the ask: two weeks already gone off the clock");
            ok(SimLabor.GrantRaise(seam, 0, 1875) == 1875 && !seam.Employees[0].WantsRaise
               && SimLabor.PoachTarget(seam) == null,
                "paying fair clears the ask and the raider loses interest in the same breath");
            int moraleWas = seam.Morale;
            SimLabor.PoachLands(seam, 0, "Vantage");
            ok(seam.Employees.Count == 0 && seam.Morale == moraleWas - 6
               && seam.SeveranceDue == 0,
                "a landed poach costs the head and the room — but never severance, they left");

            // THE HANDOFF THE RIVALS LANE ACTUALLY USES: it resolves its own
            // poach at tick 6a and leaves a marker; the next 3b reads it and the
            // ask hardens.
            GameState courted = NewState();
            courted.Morale = 70;
            courted.Employees.Add(new Employee { Name = "Ivo Marsh", Role = "engineer",
                Salary = 900, Burnout = 10, Skill = 3, HiredWeek = 1 });
            courted.SetMeta("poach_failed_wk", courted.Week);
            courted.SetMeta("poach_failed_name", "Ivo Marsh");
            courted.Week += 1;
            WeeklyReport courtedRep = SimEngine.WeeklyTick(courted);
            // Either they are asking now or they already walked on the hardened
            // clock — the same lesson, and which one it is belongs to the dice,
            // not to this pin.
            ok((courted.Employees.Count == 0 || courted.Employees[0].WantsRaise)
               && Gd.ToInt(courted.GetMetaF("poach_failed_wk", 0.0)) < 0
               && Joined(courtedRep.Events).Contains("came back with a number"),
                "a failed poach the rivals lane resolved still starts counter-offer season, once");
        }

        /// <summary>An underpaid star, two candidates who arrived this week and
        /// one who did not — everything the dressing call and the poach handoff
        /// have to get right.</summary>
        static GameState SeamState()
        {
            GameState s = NewState(7);
            s.Week = 9;
            s.CompanyName = "Pivotflow";
            s.FounderName = "Lena Voss";
            s.Employees.Add(new Employee { Name = "Priya Voss", Role = "engineer",
                Salary = 700, Burnout = 10, Skill = 4, HiredWeek = 2 });
            s.Applicants = new List<Applicant>
            {
                new Applicant { Name = "Pool One", Role = "engineer", Skill = 3, Ask = 1500,
                    Quirk = "q1", OneLiner = "", AppliedWeek = 9, Source = "inbound" },
                new Applicant { Name = "Pool Two", Role = "engineer", Skill = 5, Ask = 2400,
                    Quirk = "q2", OneLiner = "", AppliedWeek = 9, Source = "referral" },
                new Applicant { Name = "Old Hand", Role = "sales", Skill = 2, Ask = 1000,
                    Quirk = "q3", OneLiner = "", AppliedWeek = 6, Source = "inbound" },
            };
            return s;
        }

        static List<Dictionary<string, object>> Cands(GameState s)
        {
            return s.Recruitment != null && s.Recruitment.Candidates != null
                ? s.Recruitment.Candidates : new List<Dictionary<string, object>>();
        }

        static string CandS(Dictionary<string, object> c, string k)
        {
            object v; return c.TryGetValue(k, out v) && v != null ? v.ToString() : "";
        }

        static int CandI(Dictionary<string, object> c, string k)
        {
            object v; return c.TryGetValue(k, out v) && v != null ? Convert.ToInt32(v) : 0;
        }

        static bool HasApplicant(GameState s, string nm)
        {
            for (int i = 0; i < s.Applicants.Count; i++)
            {
                if ((s.Applicants[i].Name ?? "") == nm) return true;
            }
            // the reconcile (L-OWN): candidates drain into recruitment at tick start
            foreach (var c in Cands(s))
                if (CandS(c, "name") == nm && CandS(c, "stage") != "lost") return true;
            return false;
        }

        /// <summary>Three office-era staff paid exactly the market rate, so
        /// nothing but the benefits expectation can move morale between the
        /// twins.</summary>
        static GameState OfficeTwin(int officeBudget)
        {
            GameState s = NewState();
            s.Era = "office";
            s.Budgets.Office = officeBudget;
            for (int i = 0; i < 3; i++)
            {
                s.Employees.Add(new Employee { Name = "Staff", Role = "engineer",
                    Salary = 2000, Burnout = 10, Skill = 3, HiredWeek = 1 });
            }
            return s;
        }

        /// <summary>A launched company with a real market in front of it, so
        /// demand is well past what two sellers can close and the gtm clamp is
        /// what decides the week.</summary>
        static GameState SalesTwin(int skill)
        {
            GameState s = NewState();
            s.SetFlag("launched");
            s.Traction = 600;
            s.Theta.Tam = 5000000.0;
            s.Hype = 40;
            int pay = Gd.RoundToInt(SimLabor.MarketSalary("sales", "coworking")
                                    * SimLabor.AskMult(skill));
            for (int i = 0; i < 2; i++)
            {
                s.Employees.Add(new Employee { Name = "Seller", Role = "sales",
                    Salary = pay, Burnout = 10, Skill = skill, HiredWeek = 1 });
            }
            return s;
        }

        /// <summary>One office-era engineer on $1,500 with ten weeks behind them:
        /// the 2-week band.</summary>
        static GameState FireTwin()
        {
            GameState s = NewState();
            s.Era = "office";
            s.Morale = 70;
            s.Employees.Add(new Employee { Name = "Ivo Marsh", Role = "engineer",
                Salary = 1500, Burnout = 10, Skill = 3, HiredWeek = s.Week - 10 });
            return s;
        }
    }
}
