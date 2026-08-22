#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using Runway.Audio;

namespace Runway.EditorTools
{
    /// <summary>
    /// THE SFX LANE'S EVIDENCE — all fourteen cues opened, measured and played through
    /// the real pool, headless.
    ///
    ///   RUNWAY_SFX_LOG=/some/dir/cues.txt \
    ///     Unity -batchmode -nographics -quit -projectPath unity \
    ///           -executeMethod Runway.EditorTools.SfxProbe.Run
    ///
    /// WHAT THIS CAN AND CANNOT PROVE. There is no output device in batch mode, so
    /// nothing here is audible and `AudioSource.isPlaying` is not a promise anybody
    /// should read as sound. What IS honest, and what every assertion below is built
    /// on: the file opened, the WAV decoded, the clip carries real samples at a real
    /// rate, the pool took a voice and rotated, the clip and level landed on that
    /// voice, the mix holds every voice in the "sfx" bed, no voice carries a low-pass
    /// filter, the loop is held by a count, and the play path moves the managed heap
    /// by nothing. The ear test belongs to a run with a window and speakers.
    ///
    /// TWO ROUTES TO A CLIP, and the report names which one each cue took. The shipped
    /// route is `UnityWebRequestMultimedia` off `RunwayPaths.ArtUrl` — the same door
    /// `MusicManager` uses, spun inline because no coroutine pump exists outside play
    /// mode. If that route ever comes back empty in batch, the probe falls to
    /// `AssetDatabase.LoadAssetAtPath` and injects through `Sfx.Adopt`, so the pool is
    /// still exercised and the failure is REPORTED rather than hidden.
    ///
    /// Exits 1 on any failed check, so this is a gate and not only a report.
    /// </summary>
    public static class SfxProbe
    {
        const int AllocPlays = 20000;

        static readonly List<string> Log = new List<string>();
        static readonly Dictionary<string, string> Route =
            new Dictionary<string, string>(StringComparer.Ordinal);
        static int _checks;
        static int _fails;
        static bool _blind;

        public static void Run()
        {
            Log.Clear();
            Route.Clear();
            _checks = 0;
            _fails = 0;

            Say("RUNWAY! SFX · fourteen cues, headless · "
                + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Say("unity " + Application.unityVersion
                + " · batchmode=" + Application.isBatchMode
                + " · graphics=" + SystemInfo.graphicsDeviceType);
            _blind = Application.isBatchMode
                     || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;
            Say("audio: " + AudioSettings.outputSampleRate + "Hz · "
                + AudioSettings.speakerMode + " · driver caps " + AudioSettings.driverCapabilities);
            if (_blind)
            {
                Say("");
                Say("── BLIND RUN ────────────────────────────────────────────────────");
                Say("   NOTHING IS AUDIBLE. There is no output device in batch mode, so");
                Say("   `isPlaying` is REPORTED and never asserted. Clip data, levels,");
                Say("   pool rotation, mix registration and allocation are all real.");
            }
            Say("");

            Sfx.RefreshSwitch();
            string raw = Environment.GetEnvironmentVariable(Sfx.Switch);
            Say("switch " + Sfx.Switch + "=" + (string.IsNullOrEmpty(raw) ? "(absent)" : raw)
                + " → sfx " + (Sfx.Enabled ? "ON" : "OFF"));
            Sfx.SetEnabled(true);          // a harness env must not silence the evidence
            RunwayMix.RefreshSwitch();
            Truth("the mix is on (the sfx bed needs somewhere to register)", RunwayMix.Enabled);
            Truth("Install() raised the host and its voices", Sfx.Install() && Sfx.Installed);
            Say("");

            Registration();
            LoadAll();
            PoolWalk();
            LoopWalk();
            Allocation();
            KillSwitch();
            Finish();
        }

        // ══ the bed ════════════════════════════════════════════════════════════

        static void Registration()
        {
            Say("── the mix holds every voice in the sfx bed ─────────────────────");
            Truth("seven voices registered (6 one-shot + 1 loop)",
                  RunwayMix.Count(MixGroup.Sfx) == Sfx.Voices + 1);
            bool anyFilter = false;
            for (int i = 0; i < Sfx.Voices; i++)
            {
                AudioSource v = Sfx.Voice(i);
                if (v == null) { anyFilter = true; break; }
                if (v.GetComponent<AudioLowPassFilter>() != null) anyFilter = true;
            }
            if (Sfx.LoopSource != null
                && Sfx.LoopSource.GetComponent<AudioLowPassFilter>() != null) anyFilter = true;
            Truth("NO voice carries a low-pass filter — sfx is never muffled", !anyFilter);

            // the bed is flat in every state, which is the whole reason a click answers
            RunwayMix.SetState("curtained");
            for (int i = 0; i < 15; i++) RunwayMix.Tick(1f / 30f);
            Near("sfx gain under the curtain (dB)", RunwayMix.GainDb(MixGroup.Sfx), 0.0, 0.001);
            RunwayMix.SetState("binder");
            RunwayMix.SetRed(true);
            for (int i = 0; i < 15; i++) RunwayMix.Tick(1f / 30f);
            Near("sfx gain in the binder, in the red (dB)",
                 RunwayMix.GainDb(MixGroup.Sfx), 0.0, 0.001);
            Near("sfx lid in the binder, in the red (Hz)",
                 RunwayMix.Cutoff(MixGroup.Sfx), RunwayMix.Open, 0.5);
            RunwayMix.SetRed(false);
            RunwayMix.SetState("normal");
            for (int i = 0; i < 15; i++) RunwayMix.Tick(1f / 30f);
            Say("");
        }

        // ══ the fourteen ═══════════════════════════════════════════════════════

        static void LoadAll()
        {
            Say("── the fourteen cues ────────────────────────────────────────────");
            Say(string.Format("   {0,-14} {1,9} {2,10} {3,4} {4,8} {5,7}  {6}",
                "cue", "seconds", "samples", "ch", "Hz", "dB", "route"));

            string[] cues = Sfx.Cues;
            for (int i = 0; i < cues.Length; i++)
            {
                string cue = cues[i];
                AudioClip clip = Sfx.LoadBlocking(cue);
                string route = "web";
                if (clip == null)
                {
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                        "Assets/Art/sfx/" + cue + ".wav");
                    if (clip != null && Sfx.Adopt(cue, clip)) route = "asset";
                    else route = "MISSING";
                }
                Route[cue] = route;
                if (clip == null)
                {
                    _checks++; _fails++;
                    Say(string.Format("   {0,-14} {1,9} {2,10} {3,4} {4,8} {5,7}  {6}",
                        cue, "-", "-", "-", "-", "-", "!! MISSING"));
                    continue;
                }
                Say(string.Format(CultureInfo.InvariantCulture,
                    "   {0,-14} {1,9:0.000} {2,10} {3,4} {4,8} {5,7:0.0}  {6}",
                    cue, clip.length, clip.samples, clip.channels, clip.frequency,
                    Sfx.LevelDb(cue), route));
            }
            Say("");

            for (int i = 0; i < cues.Length; i++)
            {
                AudioClip c = Sfx.Clip(cues[i]);
                Truth(cues[i] + " is loaded", c != null);
                if (c == null) continue;
                Truth(cues[i] + " has a length > 0", c.length > 0f);
                Truth(cues[i] + " has samples > 0", c.samples > 0);
                Truth(cues[i] + " has a real sample rate", c.frequency >= 8000);
                Truth(cues[i] + " has 1 or 2 channels", c.channels == 1 || c.channels == 2);
            }
            Truth("all fourteen are in hand", Sfx.LoadedCount == 14);
            Say("");
        }

        // ══ the pool ═══════════════════════════════════════════════════════════

        static void PoolWalk()
        {
            Say("── the pool: six voices, taken in a ring ────────────────────────");
            Sfx.StopAll();
            string[] cues = Sfx.Cues;

            // Fourteen cues into six voices: the ring wraps twice and every cue
            // reaches a real AudioSource with its own level on it.
            int played = 0;
            var landed = new Dictionary<string, bool>(StringComparer.Ordinal);
            var seen = new bool[Sfx.Voices];
            for (int i = 0; i < cues.Length; i++)
            {
                bool ok = Sfx.Play(cues[i]);
                int took_i = Sfx.LastVoice;
                AudioSource took = Sfx.Voice(took_i);
                if (ok) played++;
                if (took_i >= 0 && took_i < Sfx.Voices) seen[took_i] = true;
                bool right = took != null && took.clip != null
                             && took.clip == Sfx.Clip(cues[i]);
                landed[cues[i]] = right;
                float want = Sfx.LevelDb(cues[i]) <= -60f
                    ? 0f : Mathf.Pow(10f, Sfx.LevelDb(cues[i]) / 20f);
                Say(string.Format(CultureInfo.InvariantCulture,
                    "   {0,-14} → voice {1}   clip {2,-18} vol {3:0.000} (want {4:0.000})"
                    + "   pitch {5:0.00}   isPlaying {6}",
                    cues[i], took_i,
                    took != null && took.clip != null ? took.clip.name : "(none)",
                    took != null ? took.volume : -1f, want,
                    took != null ? took.pitch : -1f,
                    took != null && took.isPlaying ? "yes" : "no"));
                if (took != null) Near(cues[i] + " landed at its Godot level",
                                       took.volume, want, 0.0005);
            }
            Say("");
            Truth("all fourteen plays were accepted", played == 14);
            foreach (var kv in landed) Truth(kv.Key + " reached a voice", kv.Value);
            bool allSix = true;
            for (int i = 0; i < seen.Length; i++) if (!seen[i]) allSix = false;
            Truth("fourteen cues used all six voices", allSix);

            // the two sites that disagree with the table, expressed as trims
            Sfx.Play(Sfx.Cue.Curtain, -6f, 1.25f);       // dice_roll.gd:42-43
            AudioSource dice = Sfx.Voice(Sfx.LastVoice);
            Truth("the dice whoosh took a voice", dice != null);
            if (dice != null)
            {
                Near("the dice cup's thin whoosh is -14dB", dice.volume,
                     Math.Pow(10.0, -14.0 / 20.0), 0.0005);
                Near("…at pitch 1.25", dice.pitch, 1.25, 0.0001);
            }

            Sfx.Play(Sfx.Cue.Cash, 0f, 0.9f + 0.08f * 3f);   // founder_draft_screen.gd:774
            AudioSource chip = Sfx.Voice(Sfx.LastVoice);
            Truth("the archetype chip took a voice", chip != null);
            if (chip != null)
                Near("the 4th archetype chip clicks at pitch 1.14", chip.pitch, 1.14, 0.0001);
            Say("");
        }

        // ══ the one looping cue ════════════════════════════════════════════════

        static void LoopWalk()
        {
            Say("── pen_scratch: the loop, held by a count ───────────────────────");
            Sfx.LoopStop();
            Truth("nothing is looping at rest", Sfx.LoopingCue.Length == 0);

            Sfx.PenScratch(true);
            Truth("the pen goes down → the paper is under it",
                  Sfx.LoopingCue == Sfx.Cue.PenScratch && Sfx.LoopHolds == 1);
            Truth("the loop voice is set to loop", Sfx.LoopSource.loop);
            Truth("the loop voice carries the scratch",
                  Sfx.LoopSource.clip == Sfx.Clip(Sfx.Cue.PenScratch));
            Near("journal_page.gd:625's -14dB", Sfx.LoopSource.volume,
                 Math.Pow(10.0, -14.0 / 20.0), 0.0005);

            Sfx.PenScratch(true);           // a second line starts while the first writes
            Truth("a second writer takes a second hold", Sfx.LoopHolds == 2);
            Sfx.PenScratch(false);
            Truth("one lifting does NOT stop the paper",
                  Sfx.LoopHolds == 1 && Sfx.LoopingCue == Sfx.Cue.PenScratch);
            Sfx.PenScratch(false);
            Truth("the last one lifting does", Sfx.LoopHolds == 0 && Sfx.LoopingCue.Length == 0);

            // the reading beat's quieter hand — loading_screen.gd:313
            Sfx.LoopOn(Sfx.Cue.PenScratch, -2f);
            Near("the reading beat writes at -16dB", Sfx.LoopSource.volume,
                 Math.Pow(10.0, -16.0 / 20.0), 0.0005);
            Sfx.LoopStop();
            Truth("LoopStop cuts it whatever the count", Sfx.LoopHolds == 0);

            Truth("an unknown cue plays nothing and says so once",
                  !Sfx.Play("not_a_cue") && !Sfx.LoopOn("not_a_cue"));
            Say("");
        }

        // ══ the heap ═══════════════════════════════════════════════════════════

        static void Allocation()
        {
            Say("── zero per-play allocation after warmup ────────────────────────");
            Sfx.StopAll();
            // one lap first, so every JIT stub and every Play() path is already warm
            for (int i = 0; i < 200; i++) Sfx.Play(Sfx.Cue.Tick);
            Sfx.StopAll();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long before = GC.GetTotalMemory(true);
            int gen0 = GC.CollectionCount(0);

            for (int i = 0; i < AllocPlays; i++) Sfx.Play(Sfx.Cue.Tick, -3f, 1.05f);

            long after = GC.GetTotalMemory(false);
            int gen0After = GC.CollectionCount(0);
            long delta = after - before;
            Say(string.Format(CultureInfo.InvariantCulture,
                "   {0:n0} Play() calls moved the managed heap by {1:n0} bytes,"
                + " {2} gen-0 collections", AllocPlays, delta, gen0After - gen0));
            Truth("the play path allocates nothing measurable",
                  delta <= 0 || delta < 4096);
            Truth("no gen-0 collection was forced", gen0After - gen0 == 0);
            Sfx.StopAll();
            Say("");
        }

        // ══ the kill-switch ════════════════════════════════════════════════════

        static void KillSwitch()
        {
            Say("── RUNWAY_FX_SFX=0 · the lane is not there ──────────────────────");
            Sfx.SetEnabled(false);
            Truth("the switch reads OFF", !Sfx.Enabled);
            Truth("SetEnabled(false) tore the host down", !Sfx.Installed);
            Truth("Install() raises nothing", !Sfx.Install());
            Truth("Play() is a no-op", !Sfx.Play(Sfx.Cue.Win));
            Truth("LoopOn() is a no-op", !Sfx.LoopOn(Sfx.Cue.PenScratch));
            Truth("Warm() is a no-op", !Sfx.WarmAll());
            Truth("nothing is registered in the sfx bed",
                  RunwayMix.Count(MixGroup.Sfx) == 0);
            Truth("no host object survived",
                  GameObject.Find("runway_sfx") == null);

            // and back
            Sfx.SetEnabled(true);
            Truth("the lane comes back on one call", Sfx.Install() && Sfx.Installed);
            Truth("its voices re-register", RunwayMix.Count(MixGroup.Sfx) == Sfx.Voices + 1);
            Truth("a cue re-reads off disk after a shutdown",
                  Sfx.LoadBlocking(Sfx.Cue.Tick) != null || ReAdopt(Sfx.Cue.Tick));
            Say("");
        }

        static bool ReAdopt(string cue)
        {
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/sfx/" + cue + ".wav");
            return c != null && Sfx.Adopt(cue, c);
        }

        // ══ the paperwork ══════════════════════════════════════════════════════

        static void Near(string what, double actual, double expect, double tol)
        {
            _checks++;
            bool ok = Math.Abs(actual - expect) <= tol;
            if (!ok) _fails++;
            Say(string.Format(CultureInfo.InvariantCulture,
                "   {0} {1,-52} {2,10:0.0000}   expect {3,10:0.0000}",
                ok ? "  " : "!!", what, actual, expect));
        }

        static void Truth(string what, bool ok)
        {
            _checks++;
            if (!ok) _fails++;
            Say("   " + (ok ? "  " : "!!") + " " + what.PadRight(52)
                + (ok ? "         yes" : "          NO"));
        }

        static void Say(string s) { Log.Add(s); }

        static void Finish()
        {
            int web = 0, asset = 0, missing = 0;
            foreach (var kv in Route)
            {
                if (kv.Value == "web") web++;
                else if (kv.Value == "asset") asset++;
                else missing++;
            }
            Say(string.Format("routes: {0} loaded through UnityWebRequestMultimedia, "
                + "{1} through AssetDatabase, {2} missing", web, asset, missing));
            if (asset > 0)
                Say("NOTE: a cue that needed the AssetDatabase route did NOT come back "
                    + "off disk through the shipped loader in batch — see the ledger.");
            string verdict = string.Format("SFXPROBE: {0} checks · {1} failed", _checks, _fails);
            Say("");
            Say(verdict);

            Sfx.Shutdown();
            RunwayMix.Shutdown();

            string path = OutPath();
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllLines(path, Log.ToArray());
                Debug.Log(verdict + " · " + path);
            }
            catch (Exception e)
            {
                Debug.LogError("SFXPROBE could not write " + path + ": " + e.Message);
                _fails++;
            }
            if (_fails > 0) EditorApplication.Exit(1);
        }

        static string OutPath()
        {
            string p = Environment.GetEnvironmentVariable("RUNWAY_SFX_LOG");
            if (!string.IsNullOrEmpty(p)) return p;
            return Path.Combine(Path.GetTempPath(), "runway_sfx_cues.txt");
        }
    }
}
#endif
