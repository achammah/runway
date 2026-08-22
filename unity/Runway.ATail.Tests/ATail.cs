using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Runway.App;
using Runway.Core;
using Runway.Game;

namespace Runway.ATailTests
{
    /// <summary>
    /// THE A-TAIL SUITE — checklist A15 (save round-trip), A16 (slots), A17 (key desk).
    ///
    /// It runs the SHIPPED sources: RunwayPaths, SaveSlots, Env, RunSave, RunRecord and
    /// the whole of Runway.Core compile into this assembly straight out of
    /// Assets/Scripts (see the .csproj). UnityShim.cs supplies the four UnityEngine
    /// symbols those files touch, so nothing here is a re-implementation of the code
    /// under test.
    ///
    /// NOTHING TOUCHES THE PLAYER'S FOLDER. RunwayPaths.UserDir resolves off $HOME on
    /// macOS; the suite repoints $HOME at a fresh temp sandbox before the first call and
    /// asserts the redirect took, so a broken seam fails the run instead of writing into
    /// ~/Library/Application Support/Runway.
    ///
    /// Run: $HOME/.dotnet/dotnet run --project unity/Runway.ATail.Tests
    /// </summary>
    public static class ATail
    {
        // ── the harness ────────────────────────────────────────────────────────
        static int _checks;
        static readonly List<string> _failures = new List<string>();
        static readonly List<string> _defects = new List<string>();
        static readonly List<string> _notes = new List<string>();
        static string _section = "";

        static void Section(string name)
        {
            _section = name;
            Console.WriteLine();
            Console.WriteLine("── " + name);
        }

        static void Ok(bool cond, string msg)
        {
            _checks += 1;
            if (cond)
            {
                Console.WriteLine("   ok   " + msg);
            }
            else
            {
                _failures.Add(_section + " · " + msg);
                Console.WriteLine("   FAIL " + msg);
            }
        }

        static void Note(string msg)
        {
            _notes.Add(_section + " · " + msg);
            Console.WriteLine("   note " + msg);
        }

        /// A shipped-code bug, not a test bug: it still fails the run (the checklist item
        /// is not at 100%), but it is reported with the exact one-line fix beside it.
        static void Defect(bool held, string what, string fix)
        {
            _checks += 1;
            if (held)
            {
                Console.WriteLine("   ok   " + what);
                return;
            }
            _defects.Add(_section + " · " + what + "\n      FIX: " + fix);
            Console.WriteLine("   BUG  " + what);
            Console.WriteLine("        FIX: " + fix);
        }

        // ── the sandbox ────────────────────────────────────────────────────────
        static string _sandbox;
        static string _home;
        static string _projectEnv;
        static string _realStreaming;

        const string SECRET = "sk-atail-LEAKCANARY-9f31c07a2b4e";
        const string SECRET2 = "sk-atail-SECONDCANARY-77d1aa93";
        const string PROCESS_KEY = "sk-atail-PROCESSCANARY-0b55ee12";
        const string PROJECT_KEY = "sk-atail-PROJECTLAYER-3c9d";

        public static int Main(string[] args)
        {
            Console.WriteLine("RUNWAY! A-TAIL suite — A15 save round-trip · A16 slots · A17 key desk");

            Setup();
            try
            {
                A15();
                A16();
                A17();
            }
            catch (Exception e)
            {
                _failures.Add("UNCAUGHT " + e.GetType().Name + ": " + e.Message);
                Console.WriteLine();
                Console.WriteLine("   FAIL uncaught " + e);
            }
            finally
            {
                Teardown();
            }

            Console.WriteLine();
            if (_notes.Count > 0)
            {
                Console.WriteLine("NOTES (" + _notes.Count + "):");
                foreach (string n in _notes) Console.WriteLine("  · " + n);
                Console.WriteLine();
            }
            if (_defects.Count > 0)
            {
                Console.WriteLine("SHIPPED-CODE DEFECTS (" + _defects.Count + "):");
                foreach (string d in _defects) Console.WriteLine("  · " + d);
                Console.WriteLine();
            }
            if (_failures.Count > 0)
            {
                Console.WriteLine("HARNESS FAILURES (" + _failures.Count + "):");
                foreach (string f in _failures) Console.WriteLine("  · " + f);
                Console.WriteLine();
            }
            int bad = _failures.Count + _defects.Count;
            Console.WriteLine(_checks + " checks run · " + (_checks - bad) + " held · "
                              + _defects.Count + " shipped defects · " + _failures.Count + " harness failures");
            if (bad > 0)
            {
                Console.WriteLine("A-TAIL FAIL");
                return 1;
            }
            Console.WriteLine("A-TAIL PASS");
            return 0;
        }

        static void Setup()
        {
            _sandbox = Path.Combine(Path.GetTempPath(),
                "runway-atail-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)
                + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            _home = Path.Combine(_sandbox, "home");
            Directory.CreateDirectory(_home);
            Directory.CreateDirectory(Path.Combine(_sandbox, "app", "Assets"));
            Directory.CreateDirectory(Path.Combine(_sandbox, "app", "Assets", "StreamingAssets"));
            Directory.CreateDirectory(Path.Combine(_sandbox, "persist"));

            // THE SEAM: RunwayPaths.UserDir is $HOME/Library/Application Support/Runway
            // on macOS. Repointing $HOME for this process only moves every user file the
            // shipped code writes into the sandbox, and leaves the real folder alone.
            Environment.SetEnvironmentVariable("HOME", _home);
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);

            UnityEngine.Application.platform = UnityEngine.RuntimePlatform.OSXEditor;
            UnityEngine.Application.dataPath = Path.Combine(_sandbox, "app", "Assets");
            UnityEngine.Application.streamingAssetsPath =
                Path.Combine(_sandbox, "app", "Assets", "StreamingAssets");
            UnityEngine.Application.persistentDataPath = Path.Combine(_sandbox, "persist");
            _projectEnv = Path.Combine(_sandbox, "app", ".env");   // == dataPath/../.env

            // Core reads no files itself: hand it the SAME StreamingAssets Unity ships,
            // straight off disk, so items.json trait mods are the shipped ones.
            _realStreaming = ResolveStreamingAssets();
            CoreFiles.Reader = name =>
            {
                string p = Path.Combine(_realStreaming, name);
                return File.Exists(p) ? File.ReadAllText(p) : string.Empty;
            };
            GameState.ResetItemTraitTable();

            Console.WriteLine("   sandbox: " + _sandbox);
            Console.WriteLine("   content: " + _realStreaming);
        }

        static void Teardown()
        {
            try
            {
                if (_sandbox != null && Directory.Exists(_sandbox)) Directory.Delete(_sandbox, true);
            }
            catch (Exception) { /* a leftover temp dir is not a test failure */ }
        }

        static string ResolveStreamingAssets()
        {
            var probes = new List<string>();
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                probes.Add(Path.Combine(dir.FullName, "Assets", "StreamingAssets"));
                probes.Add(Path.Combine(dir.FullName, "unity", "Assets", "StreamingAssets"));
                dir = dir.Parent;
            }
            var cwd = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (cwd != null)
            {
                probes.Add(Path.Combine(cwd.FullName, "Assets", "StreamingAssets"));
                probes.Add(Path.Combine(cwd.FullName, "unity", "Assets", "StreamingAssets"));
                cwd = cwd.Parent;
            }
            foreach (string p in probes)
                if (File.Exists(Path.Combine(p, "items.json"))) return p;
            throw new FileNotFoundException("could not locate Assets/StreamingAssets/items.json");
        }

        static string RepoScriptsDir()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                string p = Path.Combine(dir.FullName, "unity", "Assets", "Scripts");
                if (Directory.Exists(p)) return p;
                p = Path.Combine(dir.FullName, "Assets", "Scripts");
                if (Directory.Exists(p)) return p;
                dir = dir.Parent;
            }
            return "";
        }

        // ═══════════════════════════════════════════════════════════════════════
        // A15 — SAVE ROUND-TRIP
        // ═══════════════════════════════════════════════════════════════════════

        static void A15()
        {
            Section("A15 · save round-trip after a 3-week run");

            RunRecord rec;
            GameState orig = ThreeWeekRun(out rec);
            Ok(orig.Week == 4, "the fixture drove three weekly ticks (week " + orig.Week + ")");
            Ok(orig.MetricHistory.Count >= 3,
                "the engine wrote " + orig.MetricHistory.Count + " metric snapshots");

            // ── 1. object -> json -> object -> json, byte for byte ──────────────
            string j1 = JObject.FromObject(orig).ToString(Formatting.None);
            GameState back = JObject.Parse(j1).ToObject<GameState>();
            Ok(back != null, "the state deserializes back into a GameState");
            string j2 = JObject.FromObject(back).ToString(Formatting.None);
            GameState back2 = JObject.Parse(j2).ToObject<GameState>();
            string j3 = JObject.FromObject(back2).ToString(Formatting.None);

            const string CoordsFix =
                "GameState.cs:92 — `[JsonProperty(\"coords\", ObjectCreationHandling = "
                + "ObjectCreationHandling.Replace)] public List<double> Coords = new List<double> "
                + "{ 0.0, 0.0 };` (or drop the { 0.0, 0.0 } initializer). Newtonsoft's default "
                + "ObjectCreationHandling.Auto APPENDS into a pre-populated collection, so every "
                + "load prepends two more zeros and shifts the real pair further right — and "
                + "WorldGen.InvestorDcMod reads Coords[0]/[1], which are 0,0 after one CONTINUE.";

            Defect(string.Equals(j1, j2, StringComparison.Ordinal),
                "pass 1 == pass 2 (" + j1.Length + " chars)" + FirstDiff(j1, j2), CoordsFix);
            Defect(string.Equals(j2, j3, StringComparison.Ordinal),
                "pass 2 == pass 3 (the round trip is a fixed point)" + FirstDiff(j2, j3), CoordsFix);
            byte[] b1 = Encoding.UTF8.GetBytes(j1);
            byte[] b2 = Encoding.UTF8.GetBytes(j2);
            bool byteSame = b1.Length == b2.Length;
            if (byteSame)
                for (int i = 0; i < b1.Length; i++) if (b1[i] != b2[i]) { byteSame = false; break; }
            Defect(byteSame, "byte-identical UTF8 (" + b1.Length + " vs " + b2.Length + " bytes)",
                CoordsFix);

            // the growth is UNBOUNDED: each load adds two more zeros, forever
            if (orig.Investors.Count > 0)
            {
                int c0 = orig.Investors[0].Coords.Count;
                int c1 = back.Investors[0].Coords.Count;
                int c2 = back2.Investors[0].Coords.Count;
                Defect(c0 == c1 && c1 == c2,
                    "investor coords stay two long across three loads (" + c0 + " -> " + c1
                    + " -> " + c2 + ")", CoordsFix);
                if (c1 > c0)
                    Note("the save file GROWS on every CONTINUE: +2 doubles per investor per load, "
                         + "and the meaningful pair moves to Coords[" + (c1 - 2) + "] while the "
                         + "engine reads Coords[0]/[1].");
            }

            // ── 2. the reflection sweep: EVERY public field ─────────────────────
            // Two fixtures, because a live run cannot legally carry Dead/DeathCause and a
            // dead one cannot carry a live pipeline — the union must cover all of them.
            RunRecord deadRec;
            GameState dead = ThreeWeekRun(out deadRec);
            dead.Dead = true;
            dead.DeathCause = "Founder Flatline — morale hit zero in week 4.";
            GameState deadBack = JObject.Parse(JObject.FromObject(dead).ToString(Formatting.None))
                                        .ToObject<GameState>();

            var fresh = new GameState();
            FieldInfo[] fields = typeof(GameState)
                .GetFields(BindingFlags.Public | BindingFlags.Instance);
            var notExercised = new List<string>();
            var broken = new List<string>();
            foreach (FieldInfo f in fields)
            {
                string a = Ser(f.GetValue(orig));
                string b = Ser(f.GetValue(back));
                string d = Ser(f.GetValue(fresh));
                string deadA = Ser(f.GetValue(dead));
                string deadB = Ser(f.GetValue(deadBack));
                if (a == d && deadA == d) notExercised.Add(f.Name);
                if (a != b) broken.Add(f.Name + ": " + Left(a, 90) + "  ->  " + Left(b, 90));
                else if (deadA != deadB) broken.Add(f.Name + " (dead fixture)");
            }
            Console.WriteLine("   ....  " + fields.Length + " public GameState fields swept, "
                              + (fields.Length - notExercised.Count) + " carry a non-default value");
            Defect(broken.Count == 0,
                "every one of the " + fields.Length + " public fields survives the round trip"
                + (broken.Count == 0 ? "" : " — BROKEN: " + string.Join(" | ", broken)), CoordsFix);
            Ok(notExercised.Count == 0,
                "every field was actually exercised (a default value proves nothing)"
                + (notExercised.Count == 0 ? "" : " — still default: "
                   + string.Join(", ", notExercised)));

            // ── 2b. the same root cause, one level down: a dictionary field ────
            var sparse = new GameState();
            sparse.Competences = new Dictionary<string, int> { { "build", 5 } };
            sparse.Traits = new Dictionary<string, int> { { "luck", 5 } };
            GameState sparseBack = JObject.Parse(JObject.FromObject(sparse).ToString(Formatting.None))
                                          .ToObject<GameState>();
            Defect(sparseBack.Competences.Count == 1 && sparseBack.Traits.Count == 1,
                "a state whose competences/traits hold ONE key comes back with one "
                + "(got " + sparseBack.Competences.Count + " / " + sparseBack.Traits.Count + ")",
                "same root cause as coords — the five/six default keys in GameState.cs:226 and "
                + "GameState.cs:238 are merged back in on load. One serializer setting fixes the "
                + "whole class: RunSave.cs:64 — `state = sd.ToObject<GameState>(JsonSerializer."
                + "Create(new JsonSerializerSettings { ObjectCreationHandling = "
                + "ObjectCreationHandling.Replace }));` and the same for record on line 67.");

            // ── 2c. THE CONTROL: does the proposed one-line fix actually close it? ─
            // Nothing shipped is edited. The same shipped GameState is round-tripped
            // through the serializer the fix asks RunSave.Load to use, so the fix target
            // is proven rather than guessed at.
            var replaceSettings = new JsonSerializerSettings
            {
                ObjectCreationHandling = ObjectCreationHandling.Replace,
            };
            JsonSerializer replaceSer = JsonSerializer.Create(replaceSettings);
            string f1 = JObject.FromObject(orig).ToString(Formatting.None);
            GameState fixedBack = JObject.Parse(f1).ToObject<GameState>(replaceSer);
            string f2 = JObject.FromObject(fixedBack).ToString(Formatting.None);
            GameState fixedBack2 = JObject.Parse(f2).ToObject<GameState>(replaceSer);
            string f3 = JObject.FromObject(fixedBack2).ToString(Formatting.None);
            Ok(string.Equals(f1, f2, StringComparison.Ordinal)
               && string.Equals(f2, f3, StringComparison.Ordinal),
                "CONTROL: with ObjectCreationHandling.Replace the SAME shipped state is "
                + "byte-identical across three passes (" + f1.Length + " chars) — the one-line "
                + "fix closes A15" + FirstDiff(f1, f2));
            GameState sparseFixed = JObject.Parse(JObject.FromObject(sparse).ToString(Formatting.None))
                                           .ToObject<GameState>(replaceSer);
            Ok(sparseFixed.Competences.Count == 1 && sparseFixed.Traits.Count == 1,
                "CONTROL: and the dictionary defaults stop being resurrected too");
            var fixedFields = new List<string>();
            foreach (FieldInfo f in fields)
                if (Ser(f.GetValue(orig)) != Ser(f.GetValue(fixedBack))) fixedFields.Add(f.Name);
            Ok(fixedFields.Count == 0,
                "CONTROL: all " + fields.Length + " fields survive under the fix"
                + (fixedFields.Count == 0 ? "" : " — still broken: " + string.Join(", ", fixedFields)));

            // ── 3. the SAVE FILE, out and in and out again ─────────────────────
            Ok(RunSave.Save(1, orig, rec), "RunSave.Save wrote slot 1");
            string file1 = File.ReadAllText(SaveSlots.Path(1));
            GameState st2;
            RunRecord rec2;
            Ok(RunSave.Load(1, out st2, out rec2), "RunSave.Load read slot 1 back");
            Ok(RunSave.Save(2, st2, rec2), "RunSave.Save wrote the reloaded run to slot 2");
            string file2 = File.ReadAllText(SaveSlots.Path(2));

            JObject d1 = JObject.Parse(file1);
            JObject d2 = JObject.Parse(file2);
            string s1 = d1["state"].ToString(Formatting.None);
            string s2 = d2["state"].ToString(Formatting.None);
            Defect(string.Equals(s1, s2, StringComparison.Ordinal),
                "state JSON out == state JSON in, diff empty (" + s1.Length + " chars)"
                + FirstDiff(s1, s2), CoordsFix);
            Ok(string.Equals(d1["record"].ToString(Formatting.None),
                             d2["record"].ToString(Formatting.None), StringComparison.Ordinal),
                "record JSON survives the same trip");
            Ok((int)d1["version"] == RunSave.Version && (int)d2["version"] == RunSave.Version,
                "both files carry version " + RunSave.Version);
            long ts1 = (long)d1["meta"]["ts"];
            long ts2 = (long)d2["meta"]["ts"];
            Ok(ts1 > 0 && ts2 > 0, "meta.ts is stamped on both writes");
            Note("meta.ts is re-stamped at every Save (" + ts1 + " -> " + ts2
                 + ") — by design, so the whole FILE is not byte-identical across two saves; "
                 + "the state and record blocks are");
            Ok(string.Equals(d1["meta"]["company"].ToString(), orig.CompanyName, StringComparison.Ordinal)
               && string.Equals(d1["meta"]["founder"].ToString(), orig.FounderName, StringComparison.Ordinal)
               && (int)d1["meta"]["week"] == orig.Week,
                "meta mirrors the state the title table reads");

            // the loaded record is the same record
            Ok(rec2 != null && rec2.SeedValue == rec.SeedValue,
                "record seed survives (" + (rec2 != null ? rec2.SeedValue : -1) + ")");
            Ok(rec2 != null && rec2.Entries.Count == rec.Entries.Count,
                "record entries survive (" + (rec2 != null ? rec2.Entries.Count : -1) + ")");
            Ok(rec2 != null
               && string.Join("\n", rec2.CausalLines()) == string.Join("\n", rec.CausalLines()),
                "the causal chain reads back identically");

            // ── 4. the semantic layer under the JSON: state.Meta value types ────
            Section("A15b · what the JSON keeps but the TYPE loses (state.meta)");
            var probe = new GameState();
            probe.SetMeta("prev_revenue", 1234.0);          // SimEngine writes a double
            probe.SetMeta("cf_loyalty_0", 42);              // RunDriver writes an int
            probe.SetMeta("fundraising_week", 7);           // WeekCommit writes an int
            probe.SetMeta("market_line", "a quiet market");
            probe.SetMeta("unit_econ", new Dictionary<string, object>
            {
                { "arpu", 5.5 }, { "cac", 120 }, { "ltv", 400 },
            });
            string pj = JObject.FromObject(probe).ToString(Formatting.None);
            GameState pback = JObject.Parse(pj).ToObject<GameState>();
            Ok(string.Equals(pj, JObject.FromObject(pback).ToString(Formatting.None),
                             StringComparison.Ordinal),
                "meta round-trips byte-identically as JSON");

            Ok(Math.Abs(pback.GetMetaF("prev_revenue", -1.0) - 1234.0) < 0.0001,
                "prev_revenue (double) is still readable after a load — the growth math holds");
            Ok(pback.GetMeta("market_line", "").ToString() == "a quiet market",
                "market_line (string) is still readable after a load");
            Ok(pback.GetMeta("unit_econ", null) is JObject,
                "unit_econ comes back as a JObject, not a Dictionary "
                + "(BinderScreen.UnitEcon reads both — checked)");

            double loyaltyFresh = probe.GetMetaF("cf_loyalty_0", 70.0);
            double loyaltyBack = pback.GetMetaF("cf_loyalty_0", 70.0);
            Ok(Math.Abs(loyaltyFresh - 70.0) < 0.0001 && Math.Abs(loyaltyBack - 70.0) < 0.0001,
                "int-typed meta reads as the DEFAULT through GetMetaF, before AND after a save "
                + "(fresh " + loyaltyFresh + ", loaded " + loyaltyBack + ")");
            Note("GetMetaF tests `v is double`, so every int written with SetMeta reads back as the "
                 + "caller's fallback. Live callers: RunDriver.Loyalty (cf_loyalty_N, always 70) and "
                 + "JournalSpreads:383 (fundraising_week, always this week). Not a save bug — a "
                 + "GameState.GetMetaF bug the save makes permanent. Fix: "
                 + "`if (Meta.TryGetValue(key, out v) && v != null) return Convert.ToDouble(v, "
                 + "CultureInfo.InvariantCulture);` inside try/catch.");

            // ── 5. a save is never a key store ─────────────────────────────────
            Ok(!file1.Contains("sk-") && !file1.Contains("API_KEY"),
                "no api key anywhere in a save file");
        }

        /// A run that has actually been played: a world, a crew, a bag, three hostile
        /// weekly ticks, and every field of GameState carrying something real.
        static GameState ThreeWeekRun(out RunRecord rec)
        {
            var s = new GameState();
            s.SimSeed = 20260817L;
            s.Week = 1;
            s.Era = "garage";
            s.Cash = 24000;
            s.Product = 26;
            s.Traction = 4;
            s.Morale = 63;
            s.Hype = 12;
            s.BizWhat = "Service";
            s.BizWho = "SMB";
            s.CompanyName = "Bellwether Baths";
            s.CompanyIdea = "mobile sauna trailers rented to office parks by the afternoon";
            s.FounderName = "Ines Marchetti";
            s.ArchetypeId = "ex_faang";
            s.ArchetypeName = "The Ex-FAANG PM";
            s.StructureId = "team";
            s.FundingId = "friends_family";
            s.Competences = new Dictionary<string, int>
            {
                { "build", 4 }, { "sell", 3 }, { "raise", 4 }, { "recruit", 3 }, { "grit", 2 }
            };
            s.Traits = new Dictionary<string, int>
            {
                { "charisma", 3 }, { "luck", 2 }, { "network", 4 },
                { "focus", 3 }, { "credibility", 5 }, { "stamina", 2 }
            };
            s.Theta = SimEngine.DefaultTheta(s.BizWhat, s.BizWho);
            s.Items = new List<string> { "itm_alumni_ring", "itm_houseplant" };
            s.Cofounders = new List<Cofounder>
            {
                new Cofounder { Name = "Tobias Renn", Role = "Tech", Commitment = "Full-time",
                                Equity = 18.0, Vesting = "4y/1y cliff", EquityDiluted = 16.2 },
            };
            s.Employees = new List<Employee>
            {
                new Employee { Name = "Marisol Vega", Role = "support", Salary = 900,
                               Burnout = 44, Quirk = "answers every ticket in verse" },
            };
            s.Pipeline = new List<PipelineHire>
            {
                new PipelineHire { Name = "Otto Baye", Role = "engineer", Salary = 1500,
                                   WeeksIn = 0, Quirk = "keeps a paper changelog" },
            };
            s.SetFlag("launched");
            s.SetFlag("has_cofounder");
            s.Budgets = new Budgets { Marketing = 300, Sales = 150, Care = 200, Rnd = 400 };
            s.MarketingBudget = 250;
            s.PriceMult = 1.15;
            s.AnalyticsLevel = 2;
            s.LoanPrincipal = 4000;
            s.Fatigue = 31.5;
            s.TechDebt = 18.5;
            s.Exhaustion = 2;
            s.MarketTrend = 1.04;
            s.Pivots = 1;
            s.Xp = 3;
            s.Level = 2;
            s.XpSpent = 1;
            s.CeremonyPayout = 2500;
            s.WeeksInRed = 1;
            s.MissedPayrolls = 1;
            s.ExitValue = 350000;
            s.BoardSeatsFounder = 2;
            s.BoardSeatsInvestor = 1;
            s.StorySoFar = "three weeks in, the trailer smells of eucalyptus and diesel.";
            s.DeathCause = "";
            s.Dead = false;

            // the world the run was born into (investors, rivals, offers)
            WorldGen.Build(s);
            for (int i = 0; i < s.Offers.Count; i++) s.Offers[i].Price = s.Offers[i].FairPrice;

            s.Timebombs.Add(new Timebomb { WeeksLeft = 5, Event = "ev_landlord_visit" });
            s.FutureWeights.Add("ev_first_corporate_client");
            s.PlayedEvents.Add("The pilot customer wants a discount");
            s.PlayedEvents.Add("A trailer tyre gives out on the ring road");
            s.Arcs.Add(new Arc
            {
                Kind = "rival",
                Actors = new List<string> { "Steamhaus" },
                Beats = new List<ArcBeat>
                {
                    new ArcBeat { Era = "garage", Directive = "Steamhaus undercuts the afternoon rate." },
                    new ArcBeat { Era = "coworking", Directive = "Steamhaus poaches the pilot client." },
                },
            });
            s.TraitsTally["hands_on"] = 2;
            s.TraitsTally["risk_taker"] = 1;
            s.RoundsRaised.Add("pre-seed");
            s.RunHistory.Add(new RunHistoryEntry
            {
                Wk = 1, Said = "drove the trailer to the business park myself",
                Verdict = "fine", Roll = 12, Fx = "traction_delta 3 — two offices signed up",
            });
            s.LogAction("wrote: drove the trailer to the business park myself");
            SimEngine.AddStatus(s, "word_of_mouth", 4);
            SimEngine.AddClock(s, 6, "the trailer lease is up for renewal");
            s.Commitments.Add(new Commitment { Name = "the trailer lease", CashWk = -420, WeeksLeft = 8 });
            s.SetMeta("market_line", "an afternoon market that never quite admits it is a luxury");
            s.SetMeta("cf_loyalty_0", 64);

            // THREE HOSTILE WEEKS through the real engine
            for (int w = 0; w < 3; w++)
            {
                s.Week += 1;
                WeeklyReport r = SimEngine.WeeklyTick(s);
                s.LogAction("week " + s.Week + ": " + (r.Lines.Count > 0 ? r.Lines[0] : "quiet"));
                s.RunHistory.Add(new RunHistoryEntry
                {
                    Wk = s.Week, Said = "kept the trailer on the road",
                    Verdict = w == 1 ? "risky" : "fine", Roll = 9 + w,
                    Fx = "cash_delta " + (-500 - w * 10) + " — diesel and towels",
                });
            }
            // WHERE THREE WEEKS LEFT THE COMPANY. The tick consumes some of the state it
            // is handed (the pipeline onboards, the loan is repaid, exhaustion resets), so
            // the fields a SAVE has to carry are set on the far side of the ticks — this
            // is the desk the player walks away from, not the one they sat down at.
            s.Era = "coworking";
            s.SetFlag("moved_up_coworking");
            s.Pipeline.Add(new PipelineHire
            {
                Name = "Nadia Kroll", Role = "sales", Salary = 1200, WeeksIn = 1,
                Quirk = "cold-calls standing up",
            });
            s.Exhaustion = 3;
            s.LoanPrincipal = 6200;
            SimEngine.ApplyRound(s, 60000, 12.0);          // dilutes FounderPct, appends the ladder
            s.BoardSeatsFounder = 3;
            s.BoardSeatsInvestor = 1;
            s.LastOutcome = new Dictionary<string, object>
            {
                { "title", "The pilot customer wants a discount" },
                { "verdict", "fine" },
                { "said", "offered them a standing Thursday slot instead" },
                { "heard", "a volume deal without the volume" },
                { "narration", "They take the Thursday. The invoice is smaller and steadier." },
                { "reality", "" },
            };

            rec = new RunRecord { SeedValue = s.SimSeed };
            rec.LogEvent(0, new JObject { ["id"] = "draft", ["title"] = "The Founding of Bellwether Baths" },
                "Ines Marchetti (The Ex-FAANG PM) · 1 cofounder(s) · friends & family · kept 82%", null);
            rec.LogEvent(2, new JObject { ["id"] = "ev_discount", ["tier"] = "authored",
                                          ["title"] = "The pilot customer wants a discount" },
                "[wrote] offered them a standing Thursday slot instead",
                new List<string> { "traction_delta +3 — two offices signed up", "cash_delta -420 — the lease" });
            return s;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // A16 — THREE SLOTS
        // ═══════════════════════════════════════════════════════════════════════

        static void A16()
        {
            Section("A16 · slots: write 3, list, overwrite, delete, continue");

            // THE SAFETY ASSERTION FIRST: nothing below may touch the real user folder.
            string userDir = RunwayPaths.UserDir;
            Ok(userDir.StartsWith(_home, StringComparison.Ordinal),
                "UserDir is inside the sandbox (" + userDir + ")");
            Ok(userDir.EndsWith("Library/Application Support/Runway", StringComparison.Ordinal),
                "and it resolved through the real macOS branch, not a fallback");
            if (!userDir.StartsWith(_home, StringComparison.Ordinal))
            {
                Ok(false, "ABORT: the $HOME seam did not take — refusing to write slot files");
                return;
            }

            for (int i = 1; i <= SaveSlots.SlotCount; i++) SaveSlots.Clear(i);

            Ok(SaveSlots.Path(0) == SaveSlots.Path(1) && SaveSlots.Path(9) == SaveSlots.Path(3),
                "slot numbers clamp to 1..3 (a bad index cannot address a fourth file)");
            Ok(Path.GetFileName(SaveSlots.Path(2)) == "run_slot_2.unity.json",
                "the file carries the .unity suffix so a side-by-side Godot build is safe");

            for (int i = 1; i <= SaveSlots.SlotCount; i++)
                Ok(!SaveSlots.Read(i).Exists, "slot " + i + " starts as an empty desk");

            // ── three slots, three different companies ─────────────────────────
            RunRecord r1, r2, r3;
            GameState s1 = Company("Bellwether Baths", "Ines Marchetti", 12, "coworking", 8100, out r1);
            GameState s2 = Company("Halyard Coffee", "Dov Aarens", 3, "garage", 15200, out r2);
            GameState s3 = Company("Nightjar Optics", "Priya Venn", 31, "office", 240000, out r3);

            Ok(RunSave.Save(1, s1, r1), "slot 1 written");
            Ok(RunSave.Save(2, s2, r2), "slot 2 written");
            Ok(RunSave.Save(3, s3, r3), "slot 3 written");
            Ok(File.Exists(SaveSlots.Path(1)) && File.Exists(SaveSlots.Path(2))
               && File.Exists(SaveSlots.Path(3)), "three files on disk, one per slot");

            // ── the slot table (TitleScreen.SlotCard reads exactly these) ──────
            SaveSlotInfo[] rows = ListSlots();
            Ok(rows.Length == 3, "the table has three rows");
            Ok(rows[0].Exists && rows[0].Company == "Bellwether Baths"
               && rows[0].Founder == "Ines Marchetti" && rows[0].Week == 12 && rows[0].Slot == 1,
                "row 1: " + MetaLine(rows[0]));
            Ok(rows[1].Exists && rows[1].Company == "Halyard Coffee"
               && rows[1].Founder == "Dov Aarens" && rows[1].Week == 3,
                "row 2: " + MetaLine(rows[1]));
            Ok(rows[2].Exists && rows[2].Company == "Nightjar Optics"
               && rows[2].Founder == "Priya Venn" && rows[2].Week == 31,
                "row 3: " + MetaLine(rows[2]));

            long now = SaveSlots.Now;
            Ok(rows[0].Timestamp > 0 && Math.Abs(now - rows[0].Timestamp) <= 5,
                "last-played stamps are unix-now (" + rows[0].Timestamp + ")");
            Ok(SaveSlots.Ago(rows[0].Timestamp) == "1 min ago",
                "a save made just now reads '" + SaveSlots.Ago(rows[0].Timestamp) + "'");
            Ok(SaveSlots.Ago(now - 7200) == "2 h ago"
               && SaveSlots.Ago(now - 3 * 86400) == "3 days ago"
               && SaveSlots.Ago(0) == "a while ago",
                "the ago ladder matches title_screen.gd word for word");

            // ── overwrite: a NEW GAME on an occupied slot ──────────────────────
            long lenBefore = new FileInfo(SaveSlots.Path(2)).Length;
            RunRecord r2b;
            GameState s2b = Company("Kestrel Freight", "Ana Boye", 1, "garage", 9000, out r2b);
            Ok(RunSave.Save(2, s2b, r2b), "slot 2 overwritten with a different run");
            long lenAfter = new FileInfo(SaveSlots.Path(2)).Length;
            SaveSlotInfo o2 = SaveSlots.Read(2);
            Ok(o2.Exists && o2.Company == "Kestrel Freight" && o2.Founder == "Ana Boye"
               && o2.Week == 1, "the row now reads " + MetaLine(o2));
            Ok(SaveSlots.Read(1).Company == "Bellwether Baths"
               && SaveSlots.Read(3).Company == "Nightjar Optics",
                "the other two desks are untouched by the overwrite");
            string raw2 = File.ReadAllText(SaveSlots.Path(2));
            Ok(raw2.TrimEnd().EndsWith("}", StringComparison.Ordinal) && raw2.IndexOf("Halyard") < 0,
                "the overwrite TRUNCATED — no tail of the old run survives ("
                + lenBefore + " -> " + lenAfter + " bytes)");
            GameState chk;
            RunRecord chkr;
            Ok(RunSave.Load(2, out chk, out chkr) && chk.CompanyName == "Kestrel Freight",
                "and the overwritten slot still loads");

            // ── delete: a run that ended clears its desk ───────────────────────
            SaveSlots.Clear(3);
            Ok(!File.Exists(SaveSlots.Path(3)), "slot 3's file is gone");
            Ok(!SaveSlots.Read(3).Exists, "slot 3 reads as an empty desk again");
            Ok(!SaveSlots.Exists(3), "SaveSlots.Exists agrees");
            SaveSlots.Clear(3);
            Ok(true, "clearing an already-empty slot is silent and safe");
            Ok(SaveSlots.Read(1).Exists && SaveSlots.Read(2).Exists,
                "deleting one desk leaves the other two");

            // ── continue: restore slot 1 whole ─────────────────────────────────
            GameState back;
            RunRecord backRec;
            Ok(RunSave.Load(1, out back, out backRec), "CONTINUE loads slot 1");
            Ok(back != null && backRec != null, "both halves of the run come back");
            Defect(JObject.FromObject(back).ToString(Formatting.None)
                   == JObject.FromObject(s1).ToString(Formatting.None),
                "the restored state is identical to the saved one, field for field"
                + FirstDiff(JObject.FromObject(s1).ToString(Formatting.None),
                            JObject.FromObject(back).ToString(Formatting.None)),
                "the A15 coords/dictionary defect, seen from the CONTINUE door — same fix "
                + "(RunSave.cs:64 ObjectCreationHandling.Replace, or GameState.cs:92).");
            Ok(back.CompanyName == "Bellwether Baths" && back.Week == 12
               && back.Era == "coworking" && back.Cash == 8100,
                "the run resumes where it stopped: week " + back.Week + ", " + back.Era
                + ", $" + back.Cash);
            Ok(back.Statuses.Count == s1.Statuses.Count && back.Clocks.Count == s1.Clocks.Count
               && back.Offers.Count == s1.Offers.Count && back.Investors.Count == s1.Investors.Count,
                "statuses, clocks, offers and investors all come back");
            Ok(backRec.SeedValue == r1.SeedValue && backRec.Entries.Count == r1.Entries.Count,
                "the record comes back with its seed and its entries");
            // the rng the driver rebuilds off the restored save
            var rng = new Rng((ulong)(backRec.SeedValue + back.Week));
            var rng2 = new Rng((ulong)(r1.SeedValue + s1.Week));
            Ok(rng.RandiRange(1, 20) == rng2.RandiRange(1, 20),
                "the resumed rng is the same stream RunDriver.ResumeSavedRun builds");

            // ── the corrupt desk must look free, never take the title down ─────
            Section("A16b · slots that will not parse");
            File.WriteAllText(SaveSlots.Path(3), "{ this is not json at all");
            SaveSlotInfo bad = SaveSlots.Read(3);
            Ok(!bad.Exists, "an unparseable slot reads as an empty desk (no throw)");
            GameState nul;
            RunRecord nulr;
            Ok(!RunSave.Load(3, out nul, out nulr) && nul == null && nulr == null,
                "and RunSave.Load refuses it, leaving BOTH halves null (never half a company)");

            File.WriteAllText(SaveSlots.Path(3), "");
            Ok(!SaveSlots.Read(3).Exists, "an empty file reads as an empty desk");
            Ok(!RunSave.Load(3, out nul, out nulr), "and does not load");

            // meta-only: parses, so the TABLE shows a dossier the LOADER will refuse
            File.WriteAllText(SaveSlots.Path(3),
                "{\"version\":2,\"meta\":{\"company\":\"Ghost Ltd\",\"founder\":\"nobody\",\"week\":5,\"ts\":"
                + SaveSlots.Now + "}}");
            SaveSlotInfo ghost = SaveSlots.Read(3);
            Ok(ghost.Exists && ghost.Company == "Ghost Ltd",
                "a meta-only file draws a dossier on the title");
            Ok(!RunSave.Load(3, out nul, out nulr),
                "but CONTINUE refuses it — Boot falls through to BeginFreshRun, no freeze");
            Note("a state-less save is the one shape where the table and the loader disagree: "
                 + "the card is drawn, the click starts a NEW run in that slot. Authored, not silent "
                 + "(Boot.StartRun line 249-257), but nothing on screen SAYS the old company is gone. "
                 + "Fix target: SaveSlots.Read could require doc[\"state\"] as JObject != null before "
                 + "setting row.Exists.");

            // state without record: the record half is rebuilt rather than refused
            var lone = new JObject
            {
                ["version"] = 2,
                ["meta"] = SaveSlots.Meta("Lone State", "solo", 2),
                ["state"] = JObject.FromObject(s2b),
            };
            File.WriteAllText(SaveSlots.Path(3), lone.ToString(Formatting.None));
            Ok(RunSave.Load(3, out nul, out nulr) && nul != null && nulr != null
               && nulr.Entries.Count == 0,
                "a save with no record block loads with a fresh empty RunRecord");

            for (int i = 1; i <= SaveSlots.SlotCount; i++) SaveSlots.Clear(i);
            Ok(!SaveSlots.Exists(1) && !SaveSlots.Exists(2) && !SaveSlots.Exists(3),
                "the sandbox desks are clear again");
        }

        static SaveSlotInfo[] ListSlots()
        {
            var rows = new SaveSlotInfo[SaveSlots.SlotCount];
            for (int i = 0; i < rows.Length; i++) rows[i] = SaveSlots.Read(i + 1);
            return rows;
        }

        /// The exact string TitleScreen.SlotCard prints under the company name.
        static string MetaLine(SaveSlotInfo s)
        {
            if (!s.Exists) return "empty desk / nothing yet";
            return string.Format(CultureInfo.InvariantCulture, "{0}  |  {1} · week {2} · last played {3}",
                s.Company, s.Founder, s.Week, SaveSlots.Ago(s.Timestamp));
        }

        static GameState Company(string name, string founder, int week, string era, int cash,
                                 out RunRecord rec)
        {
            var s = new GameState();
            s.SimSeed = 1000L + week;
            s.CompanyName = name;
            s.FounderName = founder;
            s.Week = week;
            s.Era = era;
            s.Cash = cash;
            s.Product = 30 + week;
            s.Traction = week * 3;
            s.BizWhat = "Software";
            s.BizWho = "SMB";
            s.Theta = SimEngine.DefaultTheta(s.BizWhat, s.BizWho);
            s.SetFlag("launched");
            SimEngine.AddStatus(s, "word_of_mouth", 2);
            SimEngine.AddClock(s, 4, "the landlord wants an answer");
            WorldGen.Build(s);
            s.LogAction("started " + name);
            rec = new RunRecord { SeedValue = s.SimSeed };
            rec.LogEvent(week, new JObject { ["id"] = "ev_seed", ["title"] = "opening " + name },
                "[wrote] opened the doors", new List<string> { "cash_delta -100 — the sign" });
            return s;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // A17 — THE KEY DESK
        // ═══════════════════════════════════════════════════════════════════════

        static void A17()
        {
            Section("A17 · the key desk: keys.env, the layering, and the leak sweep");

            Ok(Env.KeysPath.StartsWith(_home, StringComparison.Ordinal),
                "keys.env resolves inside the sandbox (" + Env.KeysPath + ")");
            if (File.Exists(Env.KeysPath)) File.Delete(Env.KeysPath);
            Env.Reload();
            Ok(!Env.KeysFileExists, "no keys.env yet — this is the first-boot gate Boot reads");

            // ── layer 1: the project/dev .env ──────────────────────────────────
            File.WriteAllText(_projectEnv,
                "# the dev env, lowest layer\n"
                + "OPENAI_API_KEY=" + PROJECT_KEY + "\n"
                + "RUNWAY_LLM_TIER=standard\n"
                + "QUOTED=\"a quoted value\"\n"
                + "\n"
                + "MALFORMED\n"
                + "=novalue\n");
            Env.Reload();
            Dictionary<string, string> layered = Env.Load();
            Ok(layered.ContainsKey("OPENAI_API_KEY") && layered["OPENAI_API_KEY"] == PROJECT_KEY,
                "the project .env is the bottom layer");
            Ok(layered.ContainsKey("QUOTED") && layered["QUOTED"] == "a quoted value",
                "double quotes are stripped, dotenv.gd style");
            Ok(!layered.ContainsKey("MALFORMED") && !layered.ContainsKey(""),
                "comment, blank, keyless and valueless lines are all skipped");
            Ok(Env.Flag("RUNWAY_LLM_TIER") && !Env.Flag("RUNWAY_NOT_SET"),
                "Flag() is set-and-non-empty, exactly like OS.get_environment");

            // ── layer 2: the keys screen's own file, ON TOP of the project ─────
            Ok(Env.SaveOpenAiKey(SECRET), "the keys desk wrote keys.env");
            Ok(File.Exists(Env.KeysPath) && Env.KeysFileExists,
                "the gate file exists — the first-boot keys screen will not show again");
            string keysBody = File.ReadAllText(Env.KeysPath);
            Ok(keysBody == "OPENAI_API_KEY=" + SECRET + "\n",
                "one line, one key, nothing else in the file");
            Ok(Env.Load()["OPENAI_API_KEY"] == SECRET,
                "the USER file overrides the project .env in the layered stack");
            Ok(Env.OpenAiKey == SECRET, "Env.OpenAiKey is the key the player pasted");
            Ok(Env.Load()["RUNWAY_LLM_TIER"] == "standard",
                "a key the user file does not mention still comes from the project layer");
            Ok(Env.SaveOpenAiKey("   " + SECRET + "  \n") && Env.OpenAiKey == SECRET,
                "a pasted key is trimmed before it is written");

            // ── layer 3: a real process variable, on top of both ───────────────
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", PROCESS_KEY);
            Ok(Env.Get("OPENAI_API_KEY") == PROCESS_KEY,
                "a live process variable outranks both files through Env.Get "
                + "(scene_director.gd reads OS.get_environment first, so the renderer and the "
                + "narrator must read the same stack)");
            Ok(Env.Load()["OPENAI_API_KEY"] == SECRET,
                "…and Env.Load() still reports the FILE layering underneath it");
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", "   ");
            Defect(Env.Get("OPENAI_API_KEY") == SECRET,
                "a whitespace-only process variable does not shadow the file "
                + "(got '" + Env.Get("OPENAI_API_KEY") + "')",
                "Env.cs:62 — trim BEFORE the empty test: `if (live != null) { live = live.Trim(); "
                + "if (live.Length > 0) return live; }`. As shipped, `export OPENAI_API_KEY=\" \"` "
                + "returns \"\" from Get(), LlmClient.Setup sees no key, and the game drops to "
                + "authored-only with a keys.env sitting right there.");
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
            Ok(Env.Get("OPENAI_API_KEY") == SECRET, "unset again: the user file answers");

            // ── the cache: Load() is sticky until Reload() ─────────────────────
            File.WriteAllText(Env.KeysPath, "OPENAI_API_KEY=" + SECRET2 + "\n");
            Ok(Env.Get("OPENAI_API_KEY") == SECRET,
                "Load() is cached — an edit on disk is invisible until Reload()");
            Env.Reload();
            Ok(Env.Get("OPENAI_API_KEY") == SECRET2,
                "Reload() re-layers, which is what Boot.NotifyKeysChanged calls");
            Note("Env.Reload() is called by SaveOpenAiKey/SaveKeyless and by "
                 + "Boot.NotifyKeysChanged (Boot.cs:196-200), which re-runs LlmClient.Setup — "
                 + "that is the whole 'reload brings the LLM up' path. The MonoBehaviour half is "
                 + "exercised in the editor probe, not here.");

            // ── play without: the gate is answered, the key is not there ───────
            Ok(Env.SaveKeyless(), "'play without' wrote the keyless marker");
            Ok(Env.KeysFileExists, "the gate is answered — Boot goes to the studio card");
            Ok(File.ReadAllText(Env.KeysPath).IndexOf("sk-", StringComparison.Ordinal) < 0,
                "the keyless file holds no key at all");
            Ok(Env.OpenAiKey == PROJECT_KEY,
                "keyless still inherits a dev .env key when one exists (by design, KeysScreen:93)");
            File.Delete(_projectEnv);
            Env.Reload();
            Ok(Env.OpenAiKey == "", "with no dev .env either, keyless is genuinely keyless");
            Ok(Env.Get("OPENAI_API_KEY", "fallback") == "fallback",
                "and Get() hands back the caller's fallback, never null");

            // ── THE LEAK SWEEP ────────────────────────────────────────────────
            Section("A17b · the key never reaches a log line");

            int before = UnityEngine.Debug.Lines.Count;
            // every failure path in this layer that prints, driven on purpose
            Env.SaveOpenAiKey(SECRET);
            RunwayPaths.WriteAllText(Path.Combine("/dev/null", "nope", Env.KeysFileName),
                "OPENAI_API_KEY=" + SECRET + "\n");
            RunwayPaths.ReadAllTextOrEmpty(Path.Combine(_home, "no-such-dir", "x.json"));
            File.WriteAllText(SaveSlots.Path(1), "{ not json — OPENAI_API_KEY=" + SECRET + " }");
            SaveSlots.Read(1);
            GameState g;
            RunRecord rr;
            RunSave.Load(1, out g, out rr);
            SaveSlots.Clear(1);
            int printed = UnityEngine.Debug.Lines.Count - before;
            Ok(printed > 0, "the failure paths above printed " + printed + " lines to witness them");

            var leaks = new List<string>();
            foreach (string line in UnityEngine.Debug.Lines)
            {
                if (line.Contains(SECRET) || line.Contains(SECRET2)
                    || line.Contains(PROCESS_KEY) || line.Contains(PROJECT_KEY))
                    leaks.Add(Left(line, 160));
            }
            Ok(leaks.Count == 0,
                "no key canary in any of the " + UnityEngine.Debug.Lines.Count
                + " lines this suite made the shipped code print"
                + (leaks.Count == 0 ? "" : " — LEAKED: " + string.Join(" | ", leaks)));

            // ── the static half: no shipped log line even NAMES a key ──────────
            string scripts = RepoScriptsDir();
            Ok(scripts.Length > 0, "found the shipped sources to scan (" + scripts + ")");
            if (scripts.Length > 0)
            {
                string[] files = Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories);
                string[] forbidden = { "ApiKey", "OpenAiKey", "okey", "OPENAI_API_KEY", "ANTHROPIC_API_KEY" };
                var hits = new List<string>();
                var logCall = new Regex(@"Debug\.(Log|LogWarning|LogError)\s*\(", RegexOptions.Compiled);
                foreach (string f in files)
                {
                    string src = File.ReadAllText(f);
                    foreach (Match m in logCall.Matches(src))
                    {
                        string arg = BalancedArg(src, m.Index + m.Length - 1);
                        foreach (string bad in forbidden)
                            if (arg.IndexOf(bad, StringComparison.Ordinal) >= 0)
                                hits.Add(Path.GetFileName(f) + ": " + Left(arg.Replace("\n", " "), 120));
                    }
                }
                Ok(hits.Count == 0,
                    "no Debug.Log* argument in " + files.Length
                    + " shipped files names a key variable"
                    + (hits.Count == 0 ? "" : " — " + string.Join(" | ", hits)));

                // and the one place the key is on the wire is a header, not a body
                string llm = File.ReadAllText(Path.Combine(scripts, "LLM", "LlmClient.cs"));
                Ok(llm.Contains("\"Authorization\", \"Bearer \" + ApiKey")
                   && llm.Contains("\"x-api-key\", ApiKey"),
                    "the key rides request HEADERS only (never the logged payload)");
                string dir = File.ReadAllText(Path.Combine(scripts, "LLM", "SceneDirector.cs"));
                Ok(dir.Contains("SetRequestHeader(\"x-openai-api-key\", okey)"),
                    "the renderer sends the same key as a header to the middleware");
                Note("the middleware endpoint is a third-party host "
                     + "(nano-banana-production-e03b.up.railway.app, SceneDirector.cs:47-50) and the "
                     + "player's OpenAI key is forwarded to it in the x-openai-api-key header. "
                     + "That is the shipped design; the keys screen's promise reads 'never sent "
                     + "anywhere but OpenAI' (KeysScreen.cs:69). Wording, not a leak.");
            }

            if (File.Exists(Env.KeysPath)) File.Delete(Env.KeysPath);
            Env.Reload();
        }

        // ── little helpers ─────────────────────────────────────────────────────

        static string BalancedArg(string src, int openParen)
        {
            int depth = 0;
            for (int i = openParen; i < src.Length; i++)
            {
                char c = src[i];
                if (c == '(') depth++;
                else if (c == ')')
                {
                    depth--;
                    if (depth == 0) return src.Substring(openParen + 1, i - openParen - 1);
                }
                if (i - openParen > 4000) break;
            }
            return "";
        }

        static string Ser(object v)
        {
            try { return JsonConvert.SerializeObject(v, Formatting.None); }
            catch (Exception e) { return "<<unserializable: " + e.Message + ">>"; }
        }

        static string Left(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= n ? s : s.Substring(0, n) + "…";
        }

        static string FirstDiff(string a, string b)
        {
            if (string.Equals(a, b, StringComparison.Ordinal)) return "";
            int n = Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++)
            {
                if (a[i] != b[i])
                {
                    int from = Math.Max(0, i - 40);
                    return "  [diff at " + i + ": …" + Left(a.Substring(from), 90)
                           + "  vs  …" + Left(b.Substring(from), 90) + "]";
                }
            }
            return "  [diff: lengths " + a.Length + " vs " + b.Length + "]";
        }
    }
}
