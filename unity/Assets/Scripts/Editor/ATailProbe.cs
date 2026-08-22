using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Runway.App;
using Runway.Core;
using Runway.Game;
using Runway.Llm;

namespace Runway.EditorTests
{
    /// <summary>
    /// THE A-TAIL PROBE, INSIDE THE REAL EDITOR — checklist A15/A16/A17.
    ///
    /// unity/Runway.ATail.Tests is the same suite under `dotnet run`, where the four
    /// UnityEngine symbols the save layer touches are supplied by a shim. This probe
    /// exists so the shim can never be the reason a check passes: it runs the SAME
    /// assertions against the real UnityEngine, the real Newtonsoft the editor loads,
    /// and the real StreamingAssets — and it adds the half the console host cannot
    /// reach at all, LlmClient, which is a MonoBehaviour.
    ///
    /// NOTHING TOUCHES THE PLAYER'S FOLDER. RunwayPaths caches UserDir in a private
    /// static; the probe repoints that cache at a temp directory and REFUSES TO RUN if
    /// the redirect does not take, so a renamed field aborts the probe instead of
    /// writing into ~/Library/Application Support/Runway. The cache is restored and the
    /// temp tree deleted on the way out, whatever happens.
    ///
    /// Run:
    ///   Unity -batchmode -quit -nographics -projectPath unity \
    ///         -executeMethod Runway.EditorTests.ATailProbe.Run -logFile -
    /// </summary>
    public static class ATailProbe
    {
        static int _checks;
        static readonly List<string> _failures = new List<string>();
        static readonly List<string> _captured = new List<string>();
        static string _section = "";
        static string _temp;
        static string _savedUserDir;
        static bool _redirected;

        const string KEY = "sk-probe-EDITORCANARY-4a71bb903c";
        const string KEY2 = "sk-probe-SECONDCANARY-118fd2";

        // ── the harness ────────────────────────────────────────────────────────

        static void Section(string s)
        {
            _section = s;
            Debug.Log("ATAIL ── " + s);
        }

        static void Ok(bool cond, string msg)
        {
            _checks += 1;
            if (cond) { Debug.Log("ATAIL   ok   " + msg); return; }
            _failures.Add(_section + " · " + msg);
            Debug.Log("ATAIL   FAIL " + msg);
        }

        static void Capture(string condition, string stack, LogType type)
        {
            _captured.Add(type + ": " + condition);
        }

        // ── entry point ────────────────────────────────────────────────────────

        public static void Run()
        {
            Application.logMessageReceived += Capture;
            int code = 1;
            try
            {
                if (!Redirect())
                {
                    Debug.LogError("ATAIL ABORT: could not repoint RunwayPaths.UserDir at a temp "
                                   + "directory — refusing to write into the player's folder.");
                    Finish(1);
                    return;
                }
                A15();
                A16();
                A17();
                code = _failures.Count == 0 ? 0 : 1;
            }
            catch (Exception e)
            {
                _failures.Add("UNCAUGHT " + e.GetType().Name + ": " + e.Message);
                Debug.Log("ATAIL   FAIL uncaught " + e);
                code = 1;
            }
            finally
            {
                Restore();
                Application.logMessageReceived -= Capture;
            }
            Finish(code);
        }

        static void Finish(int code)
        {
            foreach (string f in _failures) Debug.Log("ATAIL FAILURE · " + f);
            Debug.Log("ATAIL " + _checks + " checks run, " + _failures.Count + " failed");
            Debug.Log(code == 0 ? "ATAIL PROBE PASS" : "ATAIL PROBE FAIL");
            Console.Out.Flush();
            EditorApplication.Exit(code);
        }

        /// The one seam: RunwayPaths.UserDir memoises into a private static. Setting it
        /// before the first read moves every user file this probe writes into a temp
        /// tree — and the assertion below is what makes that safe to rely on.
        static bool Redirect()
        {
            FieldInfo f = typeof(RunwayPaths).GetField("_userDir",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (f == null) return false;
            _savedUserDir = f.GetValue(null) as string;
            _temp = Path.Combine(Path.GetTempPath(),
                "runway-atail-probe-" + Guid.NewGuid().ToString("N").Substring(0, 10));
            string userDir = Path.Combine(_temp, "Library", "Application Support", "Runway");
            Directory.CreateDirectory(userDir);
            f.SetValue(null, userDir);
            _redirected = true;
            if (!RunwayPaths.UserDir.StartsWith(_temp, StringComparison.Ordinal)) return false;
            if (!RunwayPaths.User("keys.env").StartsWith(_temp, StringComparison.Ordinal)) return false;
            if (!SaveSlots.Path(1).StartsWith(_temp, StringComparison.Ordinal)) return false;
            if (!Env.KeysPath.StartsWith(_temp, StringComparison.Ordinal)) return false;
            Debug.Log("ATAIL sandbox: " + RunwayPaths.UserDir);
            return true;
        }

        static void Restore()
        {
            try
            {
                if (_redirected)
                {
                    FieldInfo f = typeof(RunwayPaths).GetField("_userDir",
                        BindingFlags.NonPublic | BindingFlags.Static);
                    if (f != null) f.SetValue(null, _savedUserDir);
                }
                Env.Reload();
                if (_temp != null && Directory.Exists(_temp)) Directory.Delete(_temp, true);
            }
            catch (Exception e) { Debug.Log("ATAIL teardown note: " + e.Message); }
        }

        // ══ A15 ════════════════════════════════════════════════════════════════

        static void A15()
        {
            Section("A15 · save round-trip in the editor runtime");
            ContentDb.InstallCoreReader();

            GameState s = Progressed();
            Ok(s.Week == 4, "three weekly ticks ran through the editor's own Runway.Core");

            string j1 = JObject.FromObject(s).ToString(Formatting.None);
            GameState back = JObject.Parse(j1).ToObject<GameState>();
            string j2 = JObject.FromObject(back).ToString(Formatting.None);
            Ok(j1 == j2, "state JSON out == state JSON in (" + j1.Length + " chars)"
                         + (j1 == j2 ? "" : Diff(j1, j2)));

            FieldInfo[] fields = typeof(GameState)
                .GetFields(BindingFlags.Public | BindingFlags.Instance);
            var broken = new List<string>();
            foreach (FieldInfo f in fields)
                if (Ser(f.GetValue(s)) != Ser(f.GetValue(back))) broken.Add(f.Name);
            Ok(broken.Count == 0, fields.Length + " public fields swept"
                + (broken.Count == 0 ? ", all survive" : " — BROKEN: " + string.Join(", ", broken)));

            var rec = new RunRecord { SeedValue = s.SimSeed };
            rec.LogEvent(2, new JObject { ["id"] = "ev", ["title"] = "a week" }, "[wrote] kept going",
                new List<string> { "cash_delta -100 — the sign" });
            Ok(RunSave.Save(1, s, rec), "RunSave.Save wrote through the editor's file layer");
            GameState st2;
            RunRecord rec2;
            Ok(RunSave.Load(1, out st2, out rec2), "RunSave.Load read it back");
            Ok(RunSave.Save(2, st2, rec2), "and saved the reloaded run to the next slot");
            string a = JObject.Parse(File.ReadAllText(SaveSlots.Path(1)))["state"].ToString(Formatting.None);
            string b = JObject.Parse(File.ReadAllText(SaveSlots.Path(2)))["state"].ToString(Formatting.None);
            Ok(a == b, "the two save files carry an identical state block" + (a == b ? "" : Diff(a, b)));
            Ok(rec2 != null && rec2.SeedValue == rec.SeedValue
               && rec2.Entries.Count == rec.Entries.Count, "the record survives the file trip");
            SaveSlots.Clear(1);
            SaveSlots.Clear(2);
        }

        static GameState Progressed()
        {
            var s = new GameState();
            s.SimSeed = 20260817L;
            s.Week = 1;
            s.Cash = 24000;
            s.Product = 26;
            s.Traction = 4;
            s.BizWhat = "Service";
            s.BizWho = "SMB";
            s.CompanyName = "Bellwether Baths";
            s.CompanyIdea = "mobile sauna trailers rented to office parks by the afternoon";
            s.FounderName = "Ines Marchetti";
            s.ArchetypeName = "The Ex-FAANG PM";
            s.Theta = SimEngine.DefaultTheta(s.BizWhat, s.BizWho);
            s.Items = new List<string> { "itm_alumni_ring" };
            s.SetFlag("launched");
            s.Budgets = new Budgets { Marketing = 300, Sales = 150, Care = 200, Rnd = 400 };
            WorldGen.Build(s);
            for (int i = 0; i < s.Offers.Count; i++) s.Offers[i].Price = s.Offers[i].FairPrice;
            SimEngine.AddStatus(s, "word_of_mouth", 4);
            SimEngine.AddClock(s, 6, "the trailer lease is up for renewal");
            s.Commitments.Add(new Commitment { Name = "the trailer lease", CashWk = -420, WeeksLeft = 8 });
            s.Employees.Add(new Employee { Name = "Marisol Vega", Role = "support", Salary = 900, Burnout = 44 });
            s.Cofounders.Add(new Cofounder { Name = "Tobias Renn", Role = "Tech",
                Commitment = "Full-time", Equity = 18.0, Vesting = "4y/1y cliff" });
            for (int w = 0; w < 3; w++) { s.Week += 1; SimEngine.WeeklyTick(s); }
            s.Era = "coworking";
            s.Pipeline.Add(new PipelineHire { Name = "Nadia Kroll", Role = "sales", Salary = 1200, WeeksIn = 1 });
            s.Exhaustion = 3;
            s.LoanPrincipal = 6200;
            return s;
        }

        // ══ A16 ════════════════════════════════════════════════════════════════

        static void A16()
        {
            Section("A16 · three slots in the editor runtime");
            for (int i = 1; i <= SaveSlots.SlotCount; i++) SaveSlots.Clear(i);
            Ok(RunwayPaths.UserDir.StartsWith(_temp, StringComparison.Ordinal),
                "every slot file lands in the sandbox, never the player's folder");

            RunRecord r1, r2, r3;
            GameState s1 = Co("Bellwether Baths", "Ines Marchetti", 12, "coworking", 8100, out r1);
            GameState s2 = Co("Halyard Coffee", "Dov Aarens", 3, "garage", 15200, out r2);
            GameState s3 = Co("Nightjar Optics", "Priya Venn", 31, "office", 240000, out r3);
            Ok(RunSave.Save(1, s1, r1) && RunSave.Save(2, s2, r2) && RunSave.Save(3, s3, r3),
                "three slots written");

            var rows = new SaveSlotInfo[SaveSlots.SlotCount];
            for (int i = 0; i < rows.Length; i++) rows[i] = SaveSlots.Read(i + 1);
            Ok(rows[0].Exists && rows[0].Company == "Bellwether Baths"
               && rows[0].Founder == "Ines Marchetti" && rows[0].Week == 12,
                "row 1 · " + Line(rows[0]));
            Ok(rows[1].Exists && rows[1].Company == "Halyard Coffee" && rows[1].Week == 3,
                "row 2 · " + Line(rows[1]));
            Ok(rows[2].Exists && rows[2].Company == "Nightjar Optics" && rows[2].Week == 31,
                "row 3 · " + Line(rows[2]));
            Ok(SaveSlots.Ago(rows[0].Timestamp) == "1 min ago"
               && SaveSlots.Ago(SaveSlots.Now - 7200) == "2 h ago"
               && SaveSlots.Ago(SaveSlots.Now - 3 * 86400) == "3 days ago",
                "the ago ladder reads as title_screen.gd writes it");

            RunRecord r2b;
            GameState s2b = Co("Kestrel Freight", "Ana Boye", 1, "garage", 9000, out r2b);
            Ok(RunSave.Save(2, s2b, r2b), "slot 2 overwritten");
            Ok(SaveSlots.Read(2).Company == "Kestrel Freight"
               && !File.ReadAllText(SaveSlots.Path(2)).Contains("Halyard"),
                "the overwrite truncated — no tail of the old run survives");
            Ok(SaveSlots.Read(1).Company == "Bellwether Baths"
               && SaveSlots.Read(3).Company == "Nightjar Optics", "the other desks are untouched");

            SaveSlots.Clear(3);
            Ok(!SaveSlots.Exists(3) && !SaveSlots.Read(3).Exists, "slot 3 deleted, reads as empty desk");

            GameState back;
            RunRecord backRec;
            Ok(RunSave.Load(1, out back, out backRec), "CONTINUE loads slot 1");
            Ok(back.CompanyName == "Bellwether Baths" && back.Week == 12 && back.Era == "coworking"
               && back.Cash == 8100 && back.Statuses.Count == s1.Statuses.Count
               && back.Clocks.Count == s1.Clocks.Count,
                "the run resumes whole: week " + back.Week + ", " + back.Era + ", $" + back.Cash);
            var rngA = new Rng((ulong)(backRec.SeedValue + back.Week));
            var rngB = new Rng((ulong)(r1.SeedValue + s1.Week));
            Ok(rngA.RandiRange(1, 20) == rngB.RandiRange(1, 20),
                "the rng RunDriver.ResumeSavedRun rebuilds is the same stream");

            File.WriteAllText(SaveSlots.Path(3), "{ this is not json at all");
            Ok(!SaveSlots.Read(3).Exists, "an unparseable slot reads as an empty desk, no throw");
            GameState nul;
            RunRecord nulr;
            Ok(!RunSave.Load(3, out nul, out nulr) && nul == null && nulr == null,
                "and CONTINUE refuses it, leaving both halves null");
            for (int i = 1; i <= SaveSlots.SlotCount; i++) SaveSlots.Clear(i);
        }

        static string Line(SaveSlotInfo s)
        {
            return s.Company + "  |  " + s.Founder + " · week " + s.Week
                   + " · last played " + SaveSlots.Ago(s.Timestamp);
        }

        static GameState Co(string name, string founder, int week, string era, int cash, out RunRecord rec)
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
            s.Theta = SimEngine.DefaultTheta(s.BizWhat, s.BizWho);
            s.SetFlag("launched");
            SimEngine.AddStatus(s, "word_of_mouth", 2);
            SimEngine.AddClock(s, 4, "the landlord wants an answer");
            WorldGen.Build(s);
            rec = new RunRecord { SeedValue = s.SimSeed };
            rec.LogEvent(week, new JObject { ["id"] = "ev_seed", ["title"] = "opening " + name },
                "[wrote] opened the doors", new List<string> { "cash_delta -100 — the sign" });
            return s;
        }

        // ══ A17 ════════════════════════════════════════════════════════════════

        static void A17()
        {
            Section("A17 · the key desk, and the reload that brings the LLM up");

            Ok(Env.KeysPath.StartsWith(_temp, StringComparison.Ordinal),
                "keys.env resolves into the sandbox (" + Env.KeysPath + ")");
            if (File.Exists(Env.KeysPath)) File.Delete(Env.KeysPath);
            Env.Reload();
            Ok(!Env.KeysFileExists, "no keys.env: this is the gate Boot.BootFlow reads on first boot");

            var svc = new GameObject("atail-services");
            LlmClient llm = svc.AddComponent<LlmClient>();
            try
            {
                llm.Setup(Env.Reload());
                Ok(!llm.Enabled && llm.Provider == "",
                    "with no key the client comes up DISABLED — the authored world carries the game");

                // the keys screen's own two buttons, in order
                Ok(Env.SaveOpenAiKey(KEY), "the keys desk wrote keys.env");
                Ok(File.Exists(Env.KeysPath) && Env.KeysFileExists, "the gate file exists");
                Ok(File.ReadAllText(Env.KeysPath) == "OPENAI_API_KEY=" + KEY + "\n",
                    "one line, one key, user folder only");
                Ok(Env.OpenAiKey == KEY, "the layered env reports the key the player pasted");

                // BOOT.NOTIFYKEYSCHANGED, verbatim: re-layer, re-setup, and the world lives
                llm.Setup(Env.Reload());
                Ok(llm.Enabled, "reload brings the LLM UP without a restart");
                Ok(llm.Provider == "openai", "provider inferred from the key alone: " + llm.Provider);
                Ok(llm.ApiKey == KEY, "the client holds the key the desk wrote");
                Ok(llm.Model == "gpt-5.6-luna" && llm.AssessModel == "gpt-5.6-terra"
                   && llm.ClarifyModel == "gpt-5.6-luna",
                    "the two-tier split is wired: " + llm.AssessModel + " assesses, "
                    + llm.ClarifyModel + " clarifies");
                Ok(llm.DirectorModel.Length > 0, "the director model falls back to " + llm.DirectorModel);

                // the generator on top of it
                var gen = svc.AddComponent<EventGenerator>();
                gen.Setup(llm);
                Ok(gen.Llm == llm, "EventGenerator takes the live client");

                // a second paste re-lands
                Ok(Env.SaveOpenAiKey(KEY2), "a second paste overwrites the file");
                llm.Setup(Env.Reload());
                Ok(llm.Enabled && llm.ApiKey == KEY2, "and the client picks up the new key");

                // "play without": the gate is answered and the world goes quiet again
                Ok(Env.SaveKeyless(), "'play without' wrote the keyless marker");
                Ok(Env.KeysFileExists, "the gate stays answered — the desk will not ask twice");
                Ok(File.ReadAllText(Env.KeysPath).IndexOf("sk-", StringComparison.Ordinal) < 0,
                    "the keyless file holds no key");
                llm.Setup(Env.Reload());
                Ok(!llm.Enabled && llm.Provider == "" && llm.ApiKey == "",
                    "and the client comes back down to authored-only");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(svc);
            }

            // ── the leak sweep, on Unity's own log stream ──────────────────────
            Section("A17b · the key never reaches the Unity console");
            Env.SaveOpenAiKey(KEY);
            RunwayPaths.WriteAllText(Path.Combine("/dev/null", "nope", Env.KeysFileName),
                "OPENAI_API_KEY=" + KEY + "\n");
            File.WriteAllText(SaveSlots.Path(1), "{ not json — OPENAI_API_KEY=" + KEY + " }");
            SaveSlots.Read(1);
            GameState g;
            RunRecord rr;
            RunSave.Load(1, out g, out rr);
            SaveSlots.Clear(1);

            var leaks = new List<string>();
            foreach (string line in _captured)
                if (line.Contains(KEY) || line.Contains(KEY2)) leaks.Add(line);
            Ok(leaks.Count == 0,
                "no key canary in the " + _captured.Count + " console lines this probe produced"
                + (leaks.Count == 0 ? "" : " — LEAKED: " + string.Join(" | ", leaks)));

            if (File.Exists(Env.KeysPath)) File.Delete(Env.KeysPath);
            Env.Reload();
        }

        // ── helpers ────────────────────────────────────────────────────────────

        static string Ser(object v)
        {
            try { return JsonConvert.SerializeObject(v, Formatting.None); }
            catch (Exception e) { return "<<unserializable: " + e.Message + ">>"; }
        }

        static string Diff(string a, string b)
        {
            int n = Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++)
                if (a[i] != b[i])
                {
                    int from = Math.Max(0, i - 30);
                    return "  [diff at " + i + ": …" + a.Substring(from, Math.Min(80, a.Length - from))
                           + "  vs  …" + b.Substring(from, Math.Min(80, b.Length - from)) + "]";
                }
            return "  [lengths " + a.Length + " vs " + b.Length + "]";
        }
    }
}
