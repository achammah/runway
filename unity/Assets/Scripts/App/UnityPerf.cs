#if !RUNWAY_FX_UPERF_OFF
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;
using Runway.Core;
using Runway.Game;

namespace Runway.App
{
    /// <summary>
    /// THE ENERGY PROBE, UNITY SIDE — the twin of `game/tests/perf_probe.gd`.
    ///
    /// Stands each heavy screen up alone on the real stage, lets it settle, then
    /// watches it for three seconds and reports what it costs to sit there doing
    /// nothing but breathing. Same screens, same order, same floor lines between
    /// them, so the two tables join column-for-column.
    ///
    /// WHY FPS IS NOT THE COST. The Godot probe's own note: an empty tree also runs
    /// at ~118fps on this machine, so every screen reads the same and a real saving
    /// reads as no change. The heat is FULL-SCREEN work done a hundred-plus times a
    /// second for 12fps hand-drawn content. Godot's answer to that is redraw/s.
    /// Unity's answer is `rebuild/s`: how many UI elements were queued for a layout
    /// or graphic rebuild per second. A 12fps baked loop writes one RawImage uvRect
    /// per baked frame and queues NO rebuild at all — anything else is a repaint of
    /// an identical 1536x1024 frame.
    ///
    /// THE FRAME CAP IS LIFTED FOR THE TABLE. Boot pins `targetFrameRate = 30`
    /// (project.godot max_fps=30), which makes every screen read 33.3ms and hides
    /// the cost completely. So the per-screen table runs UNCAPPED — frame ms is then
    /// real work and fps is real headroom, which is what the Godot probe measured.
    /// The SOAK runs at the shipped 30, because a hitch hunt has to feel what the
    /// player feels. `RUNWAY_UPERF_CAP=&lt;n&gt;` overrides either.
    ///
    /// A floor line is taken with a bare stage between screens, so a screen that
    /// never gives its sheets back shows up as a floor that keeps climbing.
    ///
    /// KEYLESS BY CONSTRUCTION. The probe forces the harness switch, forces
    /// RUNWAY_NO_ART, strips the api keys out of the layered env before the client
    /// is ever set up, and sets `Generator.Disabled = true` — the same line the
    /// Godot probe writes ("no network in a heat measurement"). Nothing here can
    /// dial out and nothing here can spend. A screen that would need a live model is
    /// skipped and says so in the table.
    ///
    ///     RUNWAY_UPERF=/tmp/perf  &lt;player or editor&gt;          → the table
    ///     RUNWAY_UPERF=/tmp/perf RUNWAY_UPERF_SOAK=1 &lt;same&gt;   → the 10-min soak
    ///
    /// Both write to &lt;dir&gt;/unity_perf.md; the soak APPENDS to whatever the table
    /// left. Run it windowed and do not touch the keyboard while it walks — a key
    /// press reaches the title screen exactly as it would in the game.
    ///
    /// The columns and their Godot twins live in `UnityPerf.Report.cs`, which owns
    /// everything this file measures but does not itself decide.
    /// </summary>
    public sealed partial class UnityPerf : MonoBehaviour
    {
        // ── the switches ───────────────────────────────────────────────────────
        public const string DirVar = "RUNWAY_UPERF";
        public const string SoakVar = "RUNWAY_UPERF_SOAK";
        public const string SecsVar = "RUNWAY_UPERF_SOAK_SECS";
        public const string DwellVar = "RUNWAY_UPERF_DWELL";
        public const string CapVar = "RUNWAY_UPERF_CAP";
        public const string UnloadVar = "RUNWAY_UPERF_UNLOAD";
        public const string BlameVar = "RUNWAY_UPERF_BLAME";
        public const string SessionKey = "runway.uperf.dir";

        /// the watch itself, per screen — perf_probe.gd's WINDOW
        public const float Window = 3.0f;
        /// the settle the brief pins for every screen; the two rows that cannot use
        /// it (the birth arrival, the die mid-tumble) keep the Godot probe's own warm
        public const float Settle = 3.0f;
        /// a hitch, by the award bar
        public const float HitchMs = 50f;

        static UnityPerf _live;

        // ══ activation ═════════════════════════════════════════════════════════

        /// BEFORE Boot (which raises itself AfterSceneLoad), so the keyless clamp is
        /// already in the env by the time the LLM client is set up.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (_live != null) return;
            string dir = OutDir();
            if (dir.Length == 0) return;   // not probing: this file costs nothing

            GoKeyless();

            var go = new GameObject("RUNWAY! perf probe");
            DontDestroyOnLoad(go);
            _live = go.AddComponent<UnityPerf>();
            _live._dir = dir;
        }

        /// The output folder, from the process env, the layered .env, or — in the
        /// editor — the session the menu item stamped before it pressed Play.
        static string OutDir()
        {
            string d = Env.Get(DirVar, "");
#if UNITY_EDITOR
            if (d.Length == 0) d = UnityEditor.SessionState.GetString(SessionKey, "");
#endif
            return (d ?? "").Trim();
        }

        /// NO NETWORK IN A HEAT MEASUREMENT. Three clamps, none of them an edit to a
        /// shared file: the harness switch (no studio card, no save writes, and no
        /// curtain reveal fighting the curtain row), the art switch (the scene
        /// director never renders), and the keys themselves, lifted out of the
        /// layered env dictionary before anyone reads it.
        static void GoKeyless()
        {
            try
            {
                Environment.SetEnvironmentVariable("RUNWAY_LANEWIRE", "1");
                Environment.SetEnvironmentVariable("RUNWAY_NO_ART", "1");
                Dictionary<string, string> env = Env.Load();
                if (env != null)
                {
                    env.Remove("OPENAI_API_KEY");
                    env.Remove("ANTHROPIC_API_KEY");
                    env["LLM_PROVIDER"] = "none";
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("UPERF: could not clamp the environment — " + e.Message);
            }
        }

        // ── state ──────────────────────────────────────────────────────────────
        string _dir = "";
        Watch _watch;
        bool _watching;
        int _capApplied;

        void Start() { StartCoroutine(Run()); }

        IEnumerator Run()
        {
            yield return Prep();
            if (Boot.Instance == null)
            {
                Debug.LogError("UPERF: Boot never came up — nothing to measure.");
                Finish("");
                yield break;
            }
            bool soak = Env.Flag(SoakVar);
            if (soak) yield return Soak();
            else yield return Table();
            Finish(soak ? SoakMd() : TableMd());
        }

        // ══ prep ═══════════════════════════════════════════════════════════════

        IEnumerator Prep()
        {
            float waited = 0f;
            while (Boot.Instance == null && waited < 20f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            if (Boot.Instance == null) yield break;
            yield return null;   // one frame so the canvas has a size

            // LET BOOT FINISH ITS OWN FLOW FIRST. BootFlow raises the title a frame
            // or two in; clearing the stage before that lands would only have it put
            // back mid-measurement, and the first row is supposed to be BARE.
            float settle = 0f;
            while (Boot.Instance.CurrentScreen == null && settle < 3f)
            {
                settle += Time.unscaledDeltaTime;
                yield return null;
            }

            Boot boot = Boot.Instance;
            if (boot.Generator != null) boot.Generator.Disabled = true;
            if (boot.Llm != null && boot.Llm.Enabled)
                Note("the LLM client came up ENABLED despite the keyless clamp — "
                     + "read every row with suspicion.");

            // THE CAP IS THE MEASUREMENT'S BIGGEST LIE. Uncapped for the table,
            // shipped-30 for the soak, and whatever RUNWAY_UPERF_CAP says over both.
            int want = Env.Flag(SoakVar) ? 30 : 0;
            string over = Env.Get(CapVar, "");
            if (over.Length > 0)
            {
                int parsed;
                if (int.TryParse(over, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                    want = Mathf.Max(0, parsed);
            }
            _capApplied = want;
            CapFrameMs = want <= 0 ? 0.0 : 1000.0 / want;
            Application.targetFrameRate = want <= 0 ? -1 : want;
            QualitySettings.vSyncCount = 0;

            _watch = new Watch();
            if (Env.Flag(BlameVar)) _blame = new Dictionary<string, int>();
            Canvas.willRenderCanvases += OnWillRenderCanvases;

            Debug.Log(string.Format(
                "UPERF: {0} mode · out {1} · cap {2} · graphics {3} · unity {4}",
                Env.Flag(SoakVar) ? "SOAK" : "TABLE", _dir,
                want <= 0 ? "off" : want.ToString(CultureInfo.InvariantCulture),
                SystemInfo.graphicsDeviceType, Application.unityVersion));
        }

        void OnDestroy()
        {
            Canvas.willRenderCanvases -= OnWillRenderCanvases;
        }

        // ══ the watch ══════════════════════════════════════════════════════════

        /// One screen's three seconds, as counters rather than a list of frames — a
        /// growing list would allocate through the very column that counts GC.
        sealed class Watch
        {
            public float Secs;
            public int Frames;
            public double SumMs;
            public double MaxMs;
            public int Over50;
            public int Over100;
            public double FirstMs = -1.0;
            public long Rebuilds;
            public bool RebuildBlind;
            public long CanvasTicks;
            public double SumDraws;
            public int DrawSamples;
            public double SumTex;
            public double SumAlloc;
            public double SumMono;
            public int Gc0, Gc1, Gc2;
            public int Doubled;
            public readonly int[] Hist = new int[257];   // 1ms buckets; the last is 256+

            public void Frame(double ms)
            {
                Frames += 1;
                Secs += (float)(ms / 1000.0);
                SumMs += ms;
                if (FirstMs < 0.0) FirstMs = ms;
                if (ms > MaxMs) MaxMs = ms;
                if (ms > HitchMs) Over50 += 1;
                if (ms > 100.0) Over100 += 1;
                // E3, frame pacing: with a cap in force, a frame that took half again
                // as long as the cap period is a DOUBLED frame — the two-frame stutter
                // that reads as a judder in a film even when nothing hitches.
                if (CapFrameMs > 0.0 && ms > CapFrameMs * 1.5) Doubled += 1;
                int b = (int)ms;
                Hist[b < 0 ? 0 : (b > 256 ? 256 : b)] += 1;
            }
        }

        /// the cap's own frame period in ms, or 0 when the cap is off — the yardstick
        /// a doubled frame is measured against
        static double CapFrameMs;

        int _gc0Base, _gc1Base, _gc2Base;
        float _drawClock;

        void BeginWatch()
        {
            _watch = new Watch();
            if (_blame != null) _blame.Clear();
            _gc0Base = GC.CollectionCount(0);
            _gc1Base = GC.CollectionCount(1);
            _gc2Base = GC.CollectionCount(2);
            _drawClock = 0f;
            _watching = true;
        }

        void EndWatch()
        {
            _watching = false;
            _watch.Gc0 = GC.CollectionCount(0) - _gc0Base;
            _watch.Gc1 = GC.CollectionCount(1) - _gc1Base;
            _watch.Gc2 = GC.CollectionCount(2) - _gc2Base;
        }

        void OnWillRenderCanvases()
        {
            if (_watching && _watch != null) _watch.CanvasTicks += 1;
        }

        /// LATE, ON PURPOSE. Unity drains the layout and graphic rebuild queues in
        /// PostLateUpdate, after every LateUpdate has run — so this is the last
        /// moment the queues still hold everything this frame dirtied. (A rebuild
        /// requested from another script's own LateUpdate can land after this read;
        /// order inside LateUpdate is undefined. It is a proxy, not a ledger.)
        void LateUpdate()
        {
            if (!_watching || _watch == null) return;
            _watch.Frame(Time.unscaledDeltaTime * 1000.0);

            int q = QueuedRebuilds();
            if (q < 0) _watch.RebuildBlind = true;
            else _watch.Rebuilds += q;
            if (q > 0 && _blame != null) { Tally(_layoutQ, "layout"); Tally(_graphicQ, "graphic"); }

            _watch.SumAlloc += Profiler.GetTotalAllocatedMemoryLong() / 1048576.0;
            _watch.SumMono += Profiler.GetMonoUsedSizeLong() / 1048576.0;
            _watch.SumTex += TextureMb();

            // the draw-call read reflects into the editor assembly and boxes an int;
            // four times a second is plenty for an average and keeps gc/s honest
            _drawClock += Time.unscaledDeltaTime;
            if (_drawClock >= 0.25f)
            {
                _drawClock = 0f;
                int d = StatInt("drawCalls");
                if (d >= 0) { _watch.SumDraws += d; _watch.DrawSamples += 1; }
            }
        }

        static double TextureMb()
        {
            try { return Texture.currentTextureMemory / 1048576.0; }
            catch (Exception) { return -1.0; }
        }

        // ── who is repainting (RUNWAY_UPERF_BLAME=1) ───────────────────────────

        /// perf_probe.gd's `RUNWAY_PERF_BLAME`, ported: a storm with a total is a
        /// mystery, a storm with an address is a fix. Null unless asked for, because
        /// naming an element costs a string per dirty element per frame — which is
        /// exactly the allocation the gc/s column exists to catch.
        Dictionary<string, int> _blame;

        void Tally(IList<ICanvasElement> queue, string kind)
        {
            if (queue == null) return;
            for (int i = 0; i < queue.Count; i++)
            {
                ICanvasElement e = queue[i];
                if (e == null) continue;
                string key;
                try { key = kind + "  " + Where(e.transform) + "  [" + e.GetType().Name + "]"; }
                catch (Exception) { key = kind + "  <gone>"; }
                int n;
                _blame.TryGetValue(key, out n);
                _blame[key] = n + 1;
            }
        }

        /// The last five names on the way up, which is enough to point at a widget
        /// without printing the whole stage every time.
        static string Where(Transform t)
        {
            if (t == null) return "<null>";
            string path = t.name;
            Transform p = t.parent;
            for (int i = 0; i < 4 && p != null; i++, p = p.parent) path = p.name + "/" + path;
            return path;
        }

        /// Watch whatever is on the stage for `Window` seconds and write the row.
        IEnumerator Sample(string label, string note = "")
        {
            BeginWatch();
            float t = 0f;
            while (t < Window)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            EndWatch();
            Watch w = _watch;
            float secs = Mathf.Max(w.Secs, 0.0001f);
            int frames = Mathf.Max(w.Frames, 1);
            var row = new Row
            {
                Label = label,
                RebuildPerSec = w.RebuildBlind ? -1.0 : w.Rebuilds / secs,
                CanvasPerSec = w.CanvasTicks / secs,
                Fps = w.Frames / secs,
                FrameMs = w.SumMs / frames,
                PeakMs = w.MaxMs,
                Draws = w.DrawSamples > 0 ? w.SumDraws / w.DrawSamples : -1.0,
                TexMb = w.SumTex / frames,
                AllocMb = w.SumAlloc / frames,
                MonoMb = w.SumMono / frames,
                GcPerSec = (w.Gc0 + w.Gc1 + w.Gc2) / secs,
                Objects = LiveObjects(),
                Note = Blind() ? (note.Length > 0 ? note + "; BLIND" : "BLIND") : note,
                Blame = TopBlame(secs),
            };
            _rows.Add(row);
            Debug.Log(string.Format(
                "UPERF {0,-22} rebuild/s {1,7} fps {2,6:0.0} frame {3,6:0.00}ms pk {4,6:0.00}ms "
                + "draws {5,5} tex {6,7:0.0}MB alloc {7,7:0.0}MB",
                row.Label, F(row.RebuildPerSec, 1), row.Fps, row.FrameMs, row.PeakMs,
                F(row.Draws, 0), row.TexMb, row.AllocMb));
        }

        /// A run with nothing on screen is a run with no cost to measure — the same
        /// warning perf_probe.gd prints for a Godot that is not windowed.
        ///
        /// TWO WAYS TO BE BLIND, and the second one lies convincingly. `-nographics`
        /// has no device at all and every render number is a flat zero. BATCHMODE
        /// WITH a device is worse: Metal comes up, textures really are created, the
        /// canvas really does update — but there is no window, so nothing is ever
        /// presented, no vsync paces anything, and the loop spins at thousands of
        /// "frames" a second with zero draw calls. Everything CPU-side stays true
        /// (rebuild/s, tex MB, alloc MB, gc/s); fps, frame ms and draws do not.
        static bool Blind()
        {
            return Application.isBatchMode
                || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;
        }

        static int LiveObjects()
        {
            try { return FindObjectsByType<Transform>(FindObjectsSortMode.None).Length; }
            catch (Exception) { return -1; }
        }

        // ══ standing a screen up ═══════════════════════════════════════════════

        IEnumerator Hold(float secs)
        {
            float t = 0f;
            while (t < secs)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        /// Bare stage: every screen and overlay gone, the curtain left alone (it is
        /// Boot's, it lives on the top layer, and it has a row of its own).
        void ClearStage()
        {
            Boot boot = Boot.Instance;
            if (boot == null) return;
            Strip(boot.ScreenLayer);
            Strip(boot.OverlayLayer);
        }

        static void Strip(RectTransform layer)
        {
            if (layer == null) return;
            for (int i = layer.childCount - 1; i >= 0; i--)
                Destroy(layer.GetChild(i).gameObject);
        }

        /// The floor a screen left behind. The game never calls UnloadUnusedAssets,
        /// so neither does this — the honest floor is the one the player's session
        /// actually sits at. RUNWAY_UPERF_UNLOAD=1 forces the sweep instead, which
        /// separates "held on purpose" from "leaked".
        IEnumerator Floor(string label)
        {
            ClearStage();
            yield return Hold(0.6f);
            if (Env.Flag(UnloadVar))
            {
                Resources.UnloadUnusedAssets();
                GC.Collect();
                yield return Hold(0.6f);
            }
            yield return Hold(0.4f);
            ClearStage();        // anything the flow put back while we waited
            yield return null;
            yield return Sample(label);
        }

        AppScreen Stand(AppState state)
        {
            Boot boot = Boot.Instance;
            if (boot == null) return null;
            if (!ScreenRegistry.Has(state))
            {
                Note(state + " has no screen registered — row skipped.");
                return null;
            }
            try { return boot.Go(state); }
            catch (Exception e)
            {
                Note(state + " threw while building (" + e.Message + ") — row skipped.");
                return null;
            }
        }

        // ══ the table ══════════════════════════════════════════════════════════

        IEnumerator Table()
        {
            Boot boot = Boot.Instance;

            yield return Floor("00 bare stage");

            // 01 TITLE — 48 full-screen frames stream in behind the breathe
            if (Stand(AppState.Title) != null)
            {
                yield return Hold(Settle);
                yield return Sample("01 title");
            }
            yield return Floor("02 floor after title");

            // 03 DRAFT PAGE 1 — the founder stage, the idle loop, the traits block
            var draft = Stand(AppState.Draft) as FounderDraftScreen;
            if (draft != null)
            {
                yield return Hold(Settle);
                if (draft != null) draft.ShowPage(1);
                yield return Hold(0.8f);
                yield return Sample("03 draft page 1");
            }
            yield return Floor("04 floor after draft");

            // 05/06 BIRTH — the arrival plays once (24 frames at 12fps = 2.0s) and
            // hands over to the loop, so these are the same screen at two ages. The
            // arrival keeps perf_probe.gd's own 0.5s warm: a 3s settle would land the
            // window entirely inside the loop and measure that twice.
            if (Stand(AppState.Birth) != null)
            {
                yield return Hold(0.5f);
                yield return Sample("05 birth (intro)");
            }
            if (Stand(AppState.Birth) != null)
            {
                yield return Hold(Settle);
                yield return Sample("06 birth (loop)");
            }

            // 07 HOWTO — one 5x8 sheet of 1024x576 frames over the drawn page
            if (Stand(AppState.HowTo) != null)
            {
                yield return Hold(Settle);
                yield return Sample("07 howto");
            }

            // 08 CURTAIN — Boot holds ONE for the whole run. Shut, it plays the
            // borrowed 94MB sway; open, it takes itself offstage, and what it costs
            // there is what it costs for the other 99% of the game.
            ClearStage();
            yield return Hold(0.6f);
            if (boot.Curtain != null)
            {
                boot.Curtain.ConsideringLine = "the world considers your week…";
                boot.Curtain.SnapShut();
                yield return Hold(Settle);
                yield return Sample("08 curtain shut");
                yield return boot.Curtain.Open(0.55f);
                yield return Hold(1.5f);
                yield return Sample("08b curtain open (held)",
                    "Unity takes the open curtain offstage; Godot keeps it in the tree");
            }
            else
            {
                Note("Boot raised no curtain — rows 08/08b skipped.");
            }

            // 09 DICE MID-ROLL — 40 frames at 12fps is 3.33s plus a 0.7s hold, so the
            // window has to open early or the die has already settled. This is
            // perf_probe.gd's 0.4s warm, kept for exactly that reason.
            ClearStage();
            yield return Hold(0.6f);
            DiceRoll dice = null;
            try { dice = DiceRoll.Create(boot.ScreenLayer, 17); }
            catch (Exception e) { Note("the die would not roll (" + e.Message + ")."); }
            if (dice != null)
            {
                yield return Hold(0.4f);
                yield return Sample("09 dice mid-roll");
            }
            yield return Floor("10 floor after dice");

            // 11 GARAGE — the room needs a run behind it, so one is dealt from the
            // shipped deck with no network anywhere near it.
            string why = StandUpRun();
            if (why.Length > 0)
            {
                Note("the garage row was skipped: " + why);
            }
            else
            {
                yield return Hold(0.5f);
                if (Stand(AppState.Garage) != null)
                {
                    yield return Hold(Settle);
                    yield return Sample("11 garage");
                }
            }
            yield return Floor("12 floor after garage");
        }

        /// perf_probe.gd's `_build_garage`, transcribed: week 9 of a two-cofounder
        /// consultancy with one burnt-out engineer, a laptop and a houseplant.
        /// Returns "" on success, or the reason the row cannot be measured.
        string StandUpRun()
        {
            RunDriver drv = RunDriver.Current;
            if (drv == null) return "no RunDriver is installed";
            try
            {
                drv.BeginFreshRun(false);
                ContentDb deck = drv.Deck;
                var d = new DraftResult
                {
                    Archetype = Pick(deck.Archetypes, "consultant"),
                    Funding = Pick(deck.Fundings, "bootstrap"),
                    CompanyName = "Blobsworth Industrial",
                    FounderName = "Wren",
                    CompanyIdea = "Peer-to-peer subscription box for artisanal compliance software",
                    BizWhat = "Service",
                    BizWho = "Consumer",
                };
                d.Cofounders.Add(new DraftCofounder
                {
                    Name = "Ada", Role = "Tech", Commitment = "Full-time",
                    Equity = 22.0, Vesting = true,
                });
                d.Cofounders.Add(new DraftCofounder
                {
                    Name = "Milo", Role = "Business", Commitment = "Full-time",
                    Equity = 15.0, Vesting = true,
                });
                d.Items.Add("itm_laptop");
                d.Items.Add("itm_houseplant");
                drv.ApplyDraft(d);

                GameState st = drv.State;
                if (st == null) return "ApplyDraft left no state behind";
                st.Employees.Add(new Employee
                {
                    Name = "Priya", Role = "engineer", Salary = 1400,
                    Burnout = 78, Quirk = "rust evangelist",
                });
                PatchFixture(st);
                SimEngine.SeedBeliefs(st);
                return "";
            }
            catch (Exception e)
            {
                return e.GetType().Name + " — " + e.Message;
            }
        }

        /// THE ROOM MUST BE THE SAME ROOM EVERY VISIT. Building the garage runs
        /// `StartWeek`, which burns a week's cash — so an unattended soak that opens
        /// it thirty times would starve the company, and a run that starves calls
        /// `Die`, which swaps the autopsy screen in on top of the leg being measured.
        /// Re-pinning the fixture before each visit costs nothing and keeps every
        /// garage sample comparable to the first one.
        static void PatchFixture(GameState st)
        {
            if (st == null) return;
            st.Week = 9;
            st.Cash = 4200;
            st.Product = 38;
            st.Traction = 7;
            st.Morale = 31;
            st.Hype = 22;
            st.FounderPct = 41.0;
            st.WeeksInRed = 0;
            st.Dead = false;
        }

        static JObject Pick(JArray rows, string id)
        {
            if (rows == null || rows.Count == 0) return new JObject();
            for (int i = 0; i < rows.Count; i++)
            {
                var o = rows[i] as JObject;
                if (o != null && ContentDb.Str(o, "id") == id) return o;
            }
            return rows[0] as JObject ?? new JObject();
        }

        // ══ the soak ═══════════════════════════════════════════════════════════

        static readonly string[] SoakOrder =
        {
            "title", "draft p1", "birth loop", "howto", "curtain shut", "dice", "garage",
        };

        IEnumerator Soak()
        {
            float total = Num(SecsVar, 600f);
            float dwell = Num(DwellVar, 20f);
            float clock = 0f;
            int at = 0;
            string runFail = StandUpRun();
            if (runFail.Length > 0) Note("the garage leg was skipped: " + runFail);

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "UPERF SOAK: {0:0}s across {1} screens, {2:0}s each",
                total, SoakOrder.Length, dwell));

            while (clock < total)
            {
                string name = SoakOrder[at % SoakOrder.Length];
                at += 1;
                if (name == "garage" && runFail.Length > 0) continue;
                float slice = Mathf.Min(dwell, total - clock);
                if (slice <= 0.2f) break;
                yield return SoakLeg(name, slice);
                clock += slice;
            }
            _soakSecs = clock;
        }

        /// Stand the screen up, then hold it — counting the construction frame apart
        /// from the sitting frames, because a build spike and a sitting hitch are two
        /// different bugs and only one of them is a surprise.
        IEnumerator SoakLeg(string name, float secs)
        {
            Boot boot = Boot.Instance;
            ClearStage();
            yield return null;
            _soakSwaps += 1;

            FounderDraftScreen draft = null;
            switch (name)
            {
                case "title": Stand(AppState.Title); break;
                case "draft p1": draft = Stand(AppState.Draft) as FounderDraftScreen; break;
                case "birth loop": Stand(AppState.Birth); break;
                case "howto": Stand(AppState.HowTo); break;
                case "garage":
                    if (RunDriver.Current != null) PatchFixture(RunDriver.Current.State);
                    Stand(AppState.Garage);
                    break;
                case "curtain shut":
                    if (boot.Curtain != null) boot.Curtain.SnapShut();
                    break;
                case "dice":
                    try { DiceRoll.Create(boot.ScreenLayer, 17); }
                    catch (Exception e) { Note("soak: no die (" + e.Message + ")"); }
                    break;
            }

            BeginWatch();
            float t = 0f;
            bool paged = false;
            while (t < secs)
            {
                t += Time.unscaledDeltaTime;
                if (!paged && t > 1.6f && draft != null) { draft.ShowPage(1); paged = true; }
                yield return null;
            }
            EndWatch();

            if (name == "curtain shut" && boot.Curtain != null)
                yield return boot.Curtain.Open(0.55f);

            Record(name, _watch);
        }

        static float Num(string key, float fallback)
        {
            string v = Env.Get(key, "");
            float parsed;
            if (v.Length > 0 && float.TryParse(v, NumberStyles.Float,
                                               CultureInfo.InvariantCulture, out parsed))
                return parsed;
            return fallback;
        }

        // ══ landing ════════════════════════════════════════════════════════════

        void Finish(string body)
        {
            if (body.Length > 0 && _dir.Length > 0)
            {
                string path = Path.Combine(_dir, "unity_perf.md");
                try
                {
                    RunwayPaths.TryCreateDir(_dir);
                    if (Env.Flag(SoakVar) && File.Exists(path)) File.AppendAllText(path, body);
                    else File.WriteAllText(path, body);
                    Debug.Log("UPERF: wrote " + path);
                }
                catch (Exception e)
                {
                    Debug.LogError("UPERF: could not write " + path + " — " + e.Message);
                    Debug.Log(body);
                }
            }
            Debug.Log("UPERF PROBE DONE");
#if UNITY_EDITOR
            UnityEditor.SessionState.EraseString(SessionKey);
            if (Application.isBatchMode) { UnityEditor.EditorApplication.Exit(0); return; }
            UnityEditor.EditorApplication.isPlaying = false;
#endif
            Application.Quit();
        }

        // ══ the two reflected reads ════════════════════════════════════════════

        static bool _uiBound;
        static IList<ICanvasElement> _layoutQ;
        static IList<ICanvasElement> _graphicQ;

        /// UGUI drains `m_LayoutRebuildQueue` / `m_GraphicRebuildQueue` every frame
        /// and exposes neither. They are the only honest twin of Godot's redraw/s, so
        /// they are found ONCE by reflection — and then held as plain
        /// `IList&lt;ICanvasElement&gt;`, because `IndexedSet&lt;T&gt;` is an internal class that
        /// implements a public interface. After binding there is no reflection in the
        /// per-frame path at all, no boxing, and no allocation to pollute gc/s.
        /// A miss reads n/a in the table rather than quietly reporting a zero.
        static void BindUi()
        {
            if (_uiBound) return;
            _uiBound = true;
            try
            {
                Type t = typeof(CanvasUpdateRegistry);
                PropertyInfo inst = t.GetProperty("instance",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                object reg = inst != null ? inst.GetValue(null, null) : null;
                if (reg == null) return;
                FieldInfo lq = t.GetField("m_LayoutRebuildQueue",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo gq = t.GetField("m_GraphicRebuildQueue",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (lq != null) _layoutQ = lq.GetValue(reg) as IList<ICanvasElement>;
                if (gq != null) _graphicQ = gq.GetValue(reg) as IList<ICanvasElement>;
            }
            catch (Exception e)
            {
                Debug.LogWarning("UPERF: the UI rebuild queues are not readable — "
                                 + e.Message + ". rebuild/s will read n/a.");
            }
        }

        static int QueuedRebuilds()
        {
            BindUi();
            if (_layoutQ == null && _graphicQ == null) return -1;
            return (_layoutQ != null ? _layoutQ.Count : 0)
                 + (_graphicQ != null ? _graphicQ.Count : 0);
        }

        static bool _statsBound;
        static Type _statsType;
        static readonly Dictionary<string, PropertyInfo> _statProps =
            new Dictionary<string, PropertyInfo>();

        /// UnityStats is the editor's own Stats window, and the only place draw calls
        /// are countable without a package. Reached by name so this file still
        /// compiles into a player, where it simply reads n/a.
        static int StatInt(string name)
        {
            if (!_statsBound)
            {
                _statsBound = true;
                // batchmode never presents a frame, so the Stats window's counters
                // sit at a flat zero: n/a is the truthful answer, not "free"
                if (!Application.isBatchMode)
                    _statsType = Type.GetType("UnityEditor.UnityStats, UnityEditor")
                              ?? Type.GetType("UnityEditor.UnityStats, UnityEditor.CoreModule");
            }
            if (_statsType == null) return -1;
            PropertyInfo p;
            if (!_statProps.TryGetValue(name, out p))
            {
                try { p = _statsType.GetProperty(name, BindingFlags.Static | BindingFlags.Public); }
                catch (Exception) { p = null; }
                _statProps[name] = p;
            }
            if (p == null) return -1;
            try { return Convert.ToInt32(p.GetValue(null, null)); }
            catch (Exception) { return -1; }
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// The editor's two doors into the probe. The output folder is stamped into the
    /// session — which survives the domain reload that entering play mode causes —
    /// rather than into an environment variable, which does not.
    /// </summary>
    public static class UnityPerfEditor
    {
        [UnityEditor.MenuItem("RUNWAY!/Perf probe — the table")]
        public static void MenuTable() { Launch(false); }

        [UnityEditor.MenuItem("RUNWAY!/Perf probe — the 10-minute soak")]
        public static void MenuSoak() { Launch(true); }

        /// -executeMethod targets. The folder comes from RUNWAY_UPERF, or a temp dir.
        public static void BatchTable() { Launch(false); }
        public static void BatchSoak() { Launch(true); }

        static void Launch(bool soak)
        {
            string dir = Env.Get(UnityPerf.DirVar, "");
            if (dir.Trim().Length == 0)
                dir = Path.Combine(Path.GetTempPath(), "runway-uperf");
            RunwayPaths.TryCreateDir(dir);
            UnityEditor.SessionState.SetString(UnityPerf.SessionKey, dir);
            if (soak) Environment.SetEnvironmentVariable(UnityPerf.SoakVar, "1");
            Debug.Log("UPERF: entering play mode · out " + dir + (soak ? " · SOAK" : ""));
            UnityEditor.EditorApplication.EnterPlaymode();
        }
    }
#endif
}
#endif
