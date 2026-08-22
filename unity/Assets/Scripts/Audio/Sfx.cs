using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Runway.App;

namespace Runway.Audio
{
    /// <summary>
    /// THE FOURTEEN CUES — every sound the player's own hands cause, ported from the
    /// Godot build's per-screen `AudioStreamPlayer` gardens into one static door.
    ///
    ///     Sfx.CardFlip();                       // the pad comes up off the desk
    ///     Sfx.Play(Sfx.Cue.Curtain, -6f, 1.25f); // dice_roll.gd:42 — thinner, higher
    ///     Sfx.LoopOn(Sfx.Cue.PenScratch);        // paper under the nib, while a line writes
    ///     Sfx.LoopOff(Sfx.Cue.PenScratch);       // …and off when the pen lifts
    ///
    /// WHY A POOL AND NOT A PLAYER PER SCREEN. Godot's screens each build their own
    /// `AudioStreamPlayer` per cue in `_ready()` and free them with the screen, so the
    /// same `cash.wav` exists five times over and a cue cannot outlive the screen that
    /// owns it. Here SIX voices live on one DontDestroyOnLoad host for the whole run,
    /// every clip is decoded ONCE, and a cue that starts as a screen tears down still
    /// finishes. Six is the measured ceiling of the original: the busiest instant in
    /// the game is a week turning (lock_week + cash + win + tick) and nothing in
    /// `main.gd` ever stacks more than four.
    ///
    /// THE LEVELS ARE THE ORIGINAL'S. Every `volume_db` and `pitch_scale` in the Godot
    /// source is transcribed into the table below, so a bare `Sfx.Curtain()` is
    /// `curtain.gd:81`'s -8dB and nothing at a call site has to remember it. The
    /// `volumeDb` argument is a TRIM ON TOP of that base, which is how the two sites that
    /// disagree are expressed: the dice cup's thin whoosh is `Curtain` at -6 trim
    /// (-8 -6 = -14dB, `dice_roll.gd:42`), and the loading beat's quieter scratch is
    /// `PenScratch` at -2 trim (-14 -2 = -16dB, `loading_screen.gd:313`).
    ///
    /// LAZY, PER CUE. Nothing is read off disk until a cue is first asked for — a run
    /// that never dies never decodes `death.wav`. The first ask of a cold cue starts
    /// the load and plays it on arrival only if it lands inside `LateWindow`; past
    /// that the ask is dropped and only the cache is warmed, because a whoosh that
    /// answers a click 400ms late reads as a fault, not as sound. `Sfx.Warm()` at a
    /// screen's build is the Godot `_ready()` behaviour when a first hit must land.
    ///
    /// ZERO PER-PLAY ALLOCATION AFTER WARMUP. The play path is a dictionary probe on a
    /// string key, two array reads, three float writes and `AudioSource.Play()`. No
    /// string is built, no list grows, no coroutine starts and no `PlayOneShot` list
    /// is touched. Everything that allocates — the host, the voices, the clips, the
    /// warning ledger — happens once, cold.
    ///
    /// EVERY VOICE IS REGISTERED WITH <see cref="RunwayMix"/> UNDER "sfx", which is the
    /// bed that is never ducked and never filtered in any state. Registration hands the
    /// mix the voice's level as a base; a per-shot level is a "foreign edit" the mix
    /// re-reads on its next tick, and since the sfx bed's gain is 0dB in all four
    /// states the two can never fight over the float.
    ///
    /// KILL-SWITCH — `RUNWAY_FX_SFX=0` (also `off`/`false`/`no`): no host, no voices, no
    /// registration, no file is ever opened and every entry point is a no-op that
    /// returns false. Absent or "1" is on. Read through `Env.Get`, so it can be set in
    /// `.env` or `keys.env` as well as the process environment, exactly like every
    /// other switch in this build. The measuring and photographing harnesses
    /// (`RUNWAY_USHOTS`, `RUNWAY_UPERF`, `RUNWAY_LANEWIRE`, `RUNWAY_SHOT`) silence it
    /// too — the same list `MusicManager` silences on.
    ///
    /// HEADLESS. Nothing here needs play mode. Outside play mode a cold cue loads
    /// INLINE (`LoadBlocking`) rather than through a coroutine, because no MonoBehaviour
    /// pump exists in the editor to advance one — so a probe's `Sfx.Play` behaves like
    /// the game's, one frame later. `Sfx.Adopt` injects a clip a harness already holds.
    /// </summary>
    public static class Sfx
    {
        /// The runtime kill-switch. Absent or "1" = on.
        public const string Switch = "RUNWAY_FX_SFX";

        /// Harnesses measure and photograph; a cue is noise in both.
        static readonly string[] Silence =
            { "RUNWAY_USHOTS", "RUNWAY_UPERF", "RUNWAY_LANEWIRE", "RUNWAY_SHOT" };

        /// One-shot voices. Four is the busiest instant `main.gd` ever asks for; six
        /// leaves two spare so a stacked week never steals a cue that is still sounding.
        public const int Voices = 6;

        /// A cold cue that lands later than this is cached, not played.
        public const float LateWindow = 0.25f;

        /// How long a blocking load may spin before it is called a miss.
        public const float LoadTimeout = 10f;

        // ══ the fourteen ═══════════════════════════════════════════════════════

        /// The cue names, as the files are named on disk.
        public static class Cue
        {
            public const string CardFlip = "card_flip";
            public const string Cash = "cash";
            public const string Curtain = "curtain";
            public const string Death = "death";
            public const string Deposit = "deposit";
            public const string DiceRattle = "dice_rattle";
            public const string LockWeek = "lock_week";
            public const string PenScratch = "pen_scratch";
            public const string PenScribble = "pen_scribble";
            public const string Pickup = "pickup";
            public const string Pivot = "pivot";
            public const string Step = "step";
            public const string Tick = "tick";
            public const string Win = "win";
        }

        /// Name · the level the Godot source gives it · whether the original loops it.
        /// A cue with no `volume_db` in the original carries 0dB, which is Godot's own
        /// default, so a bare call is byte-for-byte the original's loudness.
        static readonly string[] Names =
        {
            Cue.CardFlip, Cue.Cash, Cue.Curtain, Cue.Death, Cue.Deposit, Cue.DiceRattle,
            Cue.LockWeek, Cue.PenScratch, Cue.PenScribble, Cue.Pickup, Cue.Pivot,
            Cue.Step, Cue.Tick, Cue.Win,
        };

        static readonly float[] BaseDb =
        {
            0f,      // card_flip     — no volume_db in any of its 7 sites
            0f,      // cash          — founder_draft_screen.gd:115, garage, grind
            -8f,     // curtain       — curtain.gd:81
            0f,      // death         — garage_view_screen.gd:2935, grind_screen.gd:296
            0f,      // deposit       — founder_draft_screen.gd:891, garage:2883
            -6f,     // dice_rattle   — dice_roll.gd:49
            0f,      // lock_week     — garage_view_screen.gd:2308
            -14f,    // pen_scratch   — journal_page.gd:625
            -10f,    // pen_scribble  — journal_page.gd:626
            0f,      // pickup        — scramble3d_screen.gd:458
            0f,      // pivot         — NEVER PLAYED in the Godot build (see the ledger)
            0f,      // step          — scramble3d_screen.gd:404
            0f,      // tick          — garage_view_screen.gd:492
            0f,      // win           — finale_screen.gd:106, garage, grind, era
        };

        static readonly Dictionary<string, int> Index = BuildIndex();

        static Dictionary<string, int> BuildIndex()
        {
            var d = new Dictionary<string, int>(Names.Length, StringComparer.Ordinal);
            for (int i = 0; i < Names.Length; i++) d[Names[i]] = i;
            return d;
        }

        // ══ what is standing ═══════════════════════════════════════════════════

        static readonly AudioClip[] Clips = new AudioClip[14];
        static readonly bool[] Loading = new bool[14];
        static readonly bool[] Missing = new bool[14];
        static readonly float[] AskedAt = new float[14];

        static SfxHost _host;
        static AudioSource[] _pool;
        static AudioSource _loopVoice;
        static int _next;
        static int _last = -1;

        static string _loopCue = "";
        static int _loopHolds;

        static bool _switchRead;
        static bool _on = true;
        static List<string> _warned;

        // ══ the switch ═════════════════════════════════════════════════════════

        /// Read once and cached — a per-play environment lookup is a syscall the
        /// 100ms answer budget cannot spare.
        public static bool Enabled
        {
            get
            {
                if (_switchRead) return _on;
                _switchRead = true;
                _on = ReadSwitch();
                return _on;
            }
        }

        /// Forget the cached kill-switch and read the environment again.
        public static void RefreshSwitch() { _switchRead = false; }

        /// Force the switch without touching the environment at all — the kill-switch
        /// matrix's entry point, and the way a probe walks both sides.
        public static void SetEnabled(bool on)
        {
            _switchRead = true;
            _on = on;
            if (!on) Shutdown();
        }

        static bool ReadSwitch()
        {
            for (int i = 0; i < Silence.Length; i++)
            {
                string h = null;
                try { h = Environment.GetEnvironmentVariable(Silence[i]); }
                catch (Exception) { /* a sandboxed host simply has no harness set */ }
                if (!string.IsNullOrEmpty(h)) return false;
            }
            string sw = Env.Get(Switch, "1").Trim().ToLowerInvariant();
            return sw != "0" && sw != "off" && sw != "false" && sw != "no";
        }

        // ══ the entry point ════════════════════════════════════════════════════

        /// Raise the host and its voices. Idempotent, and with the switch off it
        /// raises nothing. Every other call does this itself, so the game never has to.
        public static bool Install()
        {
            if (!Enabled) return false;
            if (_host != null && _pool != null) return true;

            var go = new GameObject("runway_sfx");
            go.hideFlags = HideFlags.DontSave;              // never dirties a scene
            // DontDestroyOnLoad throws outside play mode; a harness's host is
            // scene-local and short-lived, which is all a harness needs.
            if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(go);
            _host = go.AddComponent<SfxHost>();

            _pool = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++) _pool[i] = MakeVoice(go);
            _loopVoice = MakeVoice(go);
            _loopVoice.loop = true;
            _next = 0;
            return true;
        }

        static AudioSource MakeVoice(GameObject go)
        {
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;      // 2D: this game has no room to be in
            src.volume = 1f;
            src.pitch = 1f;
            // The bed that is never ducked and never filtered — RunwayMix adds no
            // AudioLowPassFilter to an sfx voice at all, so a cue costs no DSP block.
            RunwayMix.RegisterSource(src, MixGroup.Sfx);
            return src;
        }

        /// Hand every voice back, drop every clip, and forget the host.
        public static void Shutdown()
        {
            if (_pool != null)
                for (int i = 0; i < _pool.Length; i++) Retire(_pool[i]);
            Retire(_loopVoice);
            _pool = null;
            _loopVoice = null;
            _loopCue = "";
            _loopHolds = 0;
            _next = 0;
            _last = -1;
            for (int i = 0; i < Clips.Length; i++)
            {
                Clips[i] = null;
                Loading[i] = false;
                Missing[i] = false;
                AskedAt[i] = 0f;
            }
            if (_host != null) { DestroyNow(_host.gameObject); _host = null; }
        }

        static void Retire(AudioSource src)
        {
            if (src == null) return;
            src.Stop();
            RunwayMix.Unregister(src);
        }

        // ══ playing ════════════════════════════════════════════════════════════

        /// ONE CUE, ONCE. `volumeDb` is a trim on top of the cue's own level and `pitch`
        /// multiplies its rate — both default to "exactly what the Godot build does".
        /// Returns false when the switch is off, the name is not a cue, or the clip is
        /// not yet in hand (in which case the load has just been started).
        public static bool Play(string cue, float volumeDb = 0f, float pitch = 1f)
        {
            if (!Enabled) return false;
            int i;
            if (!Index.TryGetValue(cue, out i)) { WarnUnknown(cue); return false; }
            if (!Install()) return false;

            AudioClip clip = Clips[i];
            if (clip == null)
            {
                // Outside play mode nothing pumps a coroutine, so a harness's ask
                // resolves inline and still sounds on this very call.
                if (!Application.isPlaying) { clip = LoadBlocking(i); if (clip == null) return false; }
                else { AskedAt[i] = Time.realtimeSinceStartup; Begin(i); return false; }
            }

            AudioSource src = Take();
            if (src == null) return false;
            src.clip = clip;
            src.volume = Amp(BaseDb[i] + volumeDb);
            src.pitch = pitch;
            src.Play();
            return true;
        }

        /// THE LOOPING CUE — `pen_scratch` under a writing hand, and nothing else in
        /// this build. Held by a COUNT, exactly as `loading_screen.gd`'s `_writing`
        /// counts its overlapping write-ins: two lines writing at once is one scratch,
        /// and the paper falls quiet when the last of them lifts.
        public static bool LoopOn(string cue, float volumeDb = 0f, float pitch = 1f)
        {
            if (!Enabled) return false;
            int i;
            if (!Index.TryGetValue(cue, out i)) { WarnUnknown(cue); return false; }
            if (!Install()) return false;

            if (string.Equals(_loopCue, cue, StringComparison.Ordinal))
            {
                _loopHolds++;
                return _loopVoice.isPlaying;
            }

            AudioClip clip = Clips[i];
            if (clip == null)
            {
                if (!Application.isPlaying) { clip = LoadBlocking(i); if (clip == null) return false; }
                else { AskedAt[i] = 0f; Begin(i); return false; }   // never played late
            }
            _loopVoice.Stop();
            _loopVoice.clip = clip;
            _loopVoice.volume = Amp(BaseDb[i] + volumeDb);
            _loopVoice.pitch = pitch;
            _loopVoice.Play();
            _loopCue = cue;
            _loopHolds = 1;
            return true;
        }

        /// Release one hold on the looping cue. The paper stops at zero, never before.
        public static bool LoopOff(string cue)
        {
            if (!Enabled || _loopVoice == null) return false;
            if (!string.Equals(_loopCue, cue, StringComparison.Ordinal)) return false;
            _loopHolds--;
            if (_loopHolds > 0) return true;
            _loopHolds = 0;
            _loopCue = "";
            _loopVoice.Stop();
            return true;
        }

        /// Cut the loop whatever its count — a screen tearing down mid-write.
        public static void LoopStop()
        {
            _loopHolds = 0;
            _loopCue = "";
            if (_loopVoice != null) _loopVoice.Stop();
        }

        /// Silence every voice without unregistering or dropping a clip.
        public static void StopAll()
        {
            if (_pool != null)
                for (int i = 0; i < _pool.Length; i++)
                    if (_pool[i] != null) _pool[i].Stop();
            LoopStop();
        }

        /// Round the ring, taking the first voice that is free; when all six are
        /// sounding the oldest by rotation gives way, which is the one nearest its end.
        static AudioSource Take()
        {
            for (int k = 0; k < Voices; k++)
            {
                int i = (_next + k) % Voices;
                AudioSource s = _pool[i];
                if (s == null || s.isPlaying) continue;
                _next = (i + 1) % Voices;
                _last = i;
                return s;
            }
            _last = _next;
            AudioSource steal = _pool[_next];
            _next = (_next + 1) % Voices;
            if (steal != null) steal.Stop();
            return steal;
        }

        static float Amp(float db)
        {
            return db <= -60f ? 0f : Mathf.Pow(10f, db / 20f);
        }

        // ══ the fourteen doors ═════════════════════════════════════════════════
        //
        // One per cue, so a hookup is a word rather than a string literal and a typo
        // is a compile error rather than a silent screen.

        public static bool CardFlip(float volumeDb = 0f, float pitch = 1f) { return Play(Cue.CardFlip, volumeDb, pitch); }
        public static bool Cash(float volumeDb = 0f, float pitch = 1f) { return Play(Cue.Cash, volumeDb, pitch); }
        public static bool Curtain(float volumeDb = 0f, float pitch = 1f) { return Play(Cue.Curtain, volumeDb, pitch); }
        public static bool Death(float volumeDb = 0f, float pitch = 1f) { return Play(Cue.Death, volumeDb, pitch); }
        public static bool Deposit(float volumeDb = 0f, float pitch = 1f) { return Play(Cue.Deposit, volumeDb, pitch); }
        public static bool DiceRattle(float volumeDb = 0f, float pitch = 1f) { return Play(Cue.DiceRattle, volumeDb, pitch); }
        public static bool LockWeek(float volumeDb = 0f, float pitch = 1f) { return Play(Cue.LockWeek, volumeDb, pitch); }
        public static bool PenScribble(float volumeDb = 0f, float pitch = 1f) { return Play(Cue.PenScribble, volumeDb, pitch); }
        public static bool Pickup(float volumeDb = 0f, float pitch = 1f) { return Play(Cue.Pickup, volumeDb, pitch); }
        public static bool Pivot(float volumeDb = 0f, float pitch = 1f) { return Play(Cue.Pivot, volumeDb, pitch); }
        public static bool Step(float volumeDb = 0f, float pitch = 1f) { return Play(Cue.Step, volumeDb, pitch); }
        public static bool Tick(float volumeDb = 0f, float pitch = 1f) { return Play(Cue.Tick, volumeDb, pitch); }
        public static bool Win(float volumeDb = 0f, float pitch = 1f) { return Play(Cue.Win, volumeDb, pitch); }

        /// The one cue the original LOOPS. `PenScratch(true)` starts a hold and
        /// `PenScratch(false)` releases it — the pen going down and coming up.
        public static bool PenScratch(bool on, float volumeDb = 0f)
        {
            return on ? LoopOn(Cue.PenScratch, volumeDb) : LoopOff(Cue.PenScratch);
        }

        // ══ warming ════════════════════════════════════════════════════════════

        /// Start every cue this screen is about to need, so its first hit is not the
        /// one that pays for the load. The Godot `_ready()` behaviour, opt-in.
        public static bool Warm(params string[] cues)
        {
            if (!Enabled || !Install()) return false;
            for (int c = 0; c < cues.Length; c++)
            {
                int i;
                if (!Index.TryGetValue(cues[c], out i)) { WarnUnknown(cues[c]); continue; }
                AskedAt[i] = 0f;               // warmed, never played on arrival
                Begin(i);
            }
            return true;
        }

        /// All fourteen. ~4.2MB of PCM decoded once, which is what the whole game ever
        /// holds — the biggest cue on disk is `death.wav` at 1.2MB.
        public static bool WarmAll() { return Warm(Names); }

        static void Begin(int i)
        {
            if (Clips[i] != null || Loading[i] || Missing[i]) return;
            Loading[i] = true;
            if (_host != null && Application.isPlaying) _host.StartCoroutine(LoadRoutine(i));
            else LoadBlocking(i);
        }

        static IEnumerator LoadRoutine(int i)
        {
            string url = RunwayPaths.ArtUrl("sfx/" + Names[i] + ".wav");
            if (url.Length == 0) { Miss(i, "not on disk"); yield break; }
            // ArtUrl goes through new Uri(path).AbsoluteUri, which is REQUIRED: this
            // project's own path carries a space and a hand-built "file://" + path
            // does not survive it.
            using (var req = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
            {
                yield return req.SendWebRequest();
                Finish(i, req);
            }
        }

        /// The same load with no coroutine — for the editor, for a batch harness, and
        /// for the first ask outside play mode. Spins the main thread, which is safe
        /// for a local file:// request and is never on the game's path.
        public static AudioClip LoadBlocking(string cue)
        {
            int i;
            if (!Index.TryGetValue(cue, out i)) { WarnUnknown(cue); return null; }
            return LoadBlocking(i);
        }

        static AudioClip LoadBlocking(int i)
        {
            if (Clips[i] != null) return Clips[i];
            if (Missing[i]) return null;
            Loading[i] = true;
            string url = RunwayPaths.ArtUrl("sfx/" + Names[i] + ".wav");
            if (url.Length == 0) { Miss(i, "not on disk"); return null; }
            using (var req = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
            {
                var op = req.SendWebRequest();
                float t0 = Time.realtimeSinceStartup;
                while (!op.isDone)
                {
                    if (Time.realtimeSinceStartup - t0 > LoadTimeout)
                    {
                        req.Abort();
                        // Disposing a request the same instant it is aborted has been
                        // reported unsafe (the shell's B8). The coroutine path waits a
                        // frame; a blocking path has no frame, so it waits 16ms.
                        System.Threading.Thread.Sleep(16);
                        Miss(i, "timed out after " + LoadTimeout + "s");
                        return null;
                    }
                    System.Threading.Thread.Sleep(1);
                }
                Finish(i, req);
            }
            return Clips[i];
        }

        static void Finish(int i, UnityWebRequest req)
        {
            Loading[i] = false;
            if (req.result != UnityWebRequest.Result.Success) { Miss(i, req.error); return; }
            AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
            if (clip == null || clip.samples <= 0) { Miss(i, "decoded empty"); return; }
            clip.name = "sfx_" + Names[i];
            Clips[i] = clip;
            // A cue that was ASKED FOR, not merely warmed, and landed while the moment
            // it belongs to is still on screen: it sounds now. Past the window it is
            // only cached, because a late answer reads as a fault.
            float asked = AskedAt[i];
            AskedAt[i] = 0f;
            if (asked <= 0f || Time.realtimeSinceStartup - asked > LateWindow) return;
            AudioSource src = Take();
            if (src == null) return;
            src.clip = clip;
            src.volume = Amp(BaseDb[i]);
            src.pitch = 1f;
            src.Play();
        }

        static void Miss(int i, string why)
        {
            Loading[i] = false;
            Missing[i] = true;
            AskedAt[i] = 0f;
            Debug.LogWarning("RUNWAY! sfx " + Names[i] + ".wav " + why
                             + " — that cue is silent for this run.");
        }

        /// A clip a harness already holds, injected under a cue name. The seam the
        /// editor probe uses when it wants to prove the POOL rather than the loader.
        public static bool Adopt(string cue, AudioClip clip)
        {
            int i;
            if (!Index.TryGetValue(cue, out i)) { WarnUnknown(cue); return false; }
            if (clip == null) return false;
            Clips[i] = clip;
            Loading[i] = false;
            Missing[i] = false;
            return true;
        }

        // ══ what the probe and the ledger read ═════════════════════════════════

        /// The fourteen names, in table order.
        public static string[] Cues { get { return Names; } }

        /// The Godot level a bare call carries.
        public static float LevelDb(string cue)
        {
            int i;
            return Index.TryGetValue(cue, out i) ? BaseDb[i] : 0f;
        }

        public static bool Loaded(string cue)
        {
            int i;
            return Index.TryGetValue(cue, out i) && Clips[i] != null;
        }

        public static AudioClip Clip(string cue)
        {
            int i;
            return Index.TryGetValue(cue, out i) ? Clips[i] : null;
        }

        public static bool Absent(string cue)
        {
            int i;
            return Index.TryGetValue(cue, out i) && Missing[i];
        }

        public static bool Installed { get { return _host != null && _pool != null; } }

        public static int LoadedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Clips.Length; i++) if (Clips[i] != null) n++;
                return n;
            }
        }

        /// The pool, read-only — a probe reads `clip`, `volume`, `pitch` and
        /// `isPlaying` off these rather than being told what they are.
        public static AudioSource Voice(int i)
        {
            if (_pool == null || i < 0 || i >= _pool.Length) return null;
            return _pool[i];
        }

        public static AudioSource LoopSource { get { return _loopVoice; } }
        public static string LoopingCue { get { return _loopCue; } }
        public static int LoopHolds { get { return _loopHolds; } }

        /// Where the ring is pointing.
        public static int NextVoice { get { return _next; } }

        /// Which voice the LAST one-shot actually took, -1 before the first. Read it
        /// rather than predicting it: a free voice further round the ring is preferred
        /// over a busy one at the head, so the ring is an order and not a formula.
        public static int LastVoice { get { return _last; } }

        // ══ internals ══════════════════════════════════════════════════════════

        static void WarnUnknown(string cue)
        {
            if (_warned == null) _warned = new List<string>(4);
            if (_warned.Contains(cue)) return;
            _warned.Add(cue);
            Debug.LogWarning("RUNWAY! sfx: no cue named \"" + cue + "\" — nothing played.");
        }

        static void DestroyNow(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
