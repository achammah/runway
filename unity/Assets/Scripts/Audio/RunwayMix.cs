using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Runway.App;

namespace Runway.Audio
{
    /// <summary>
    /// The three beds every sound in this game belongs to. MUSIC is the OST loop,
    /// SFX is anything the player's own hands cause (a click, a page, a die), WORLD is
    /// the room itself (the hum, the street, the rain on the garage roof).
    ///
    /// SFX IS NEVER DUCKED AND NEVER FILTERED. Every input answers within 100ms with
    /// sound; a muffled click reads as a dropped input, so the state table leaves the
    /// SFX bed flat in all four states on purpose.
    /// </summary>
    public enum MixGroup { Music = 0, Sfx = 1, World = 2 }

    /// <summary>
    /// THE MIX THAT KNOWS WHAT IS HAPPENING — the AudioMixer asset this project cannot
    /// author headlessly, rebuilt in code.
    ///
    /// A .mixer is a binary asset whose snapshots only the editor's own inspector
    /// writes safely, so the snapshot PATTERN is what is ported: named states, one
    /// table of per-group numbers, a 0.3s lerp between them, and ONE Update driver
    /// that writes `AudioSource.volume` and `AudioLowPassFilter.cutoffFrequency`.
    ///
    ///     RunwayMix.RegisterSource(src, "music");    // once, where the source is made
    ///     RunwayMix.SetState("curtained");           // the curtain shut
    ///     RunwayMix.SetRed(state.Cash &lt; 0);          // the standing bed, every breath
    ///
    /// THE STATES (dB gain · low-pass cutoff in Hz, per group):
    ///
    ///                   music          sfx           world
    ///     normal       0 ·  open      0 · open      0 ·  open
    ///     curtained   -6 ·  open      0 · open     -6 ·  1200
    ///     binder      -3 ·   900      0 · open     -6 ·   700
    ///     red (bed)   -3 ·  2400      0 · open     -3 ·  2400
    ///
    /// RED IS A BED, NOT A MOMENT. Cash below zero is a condition the run sits inside
    /// for weeks; the curtain and the binder are moments that come and go over it. So
    /// red LAYERS on the moment: its dB adds, and its cutoff takes the lower lid of the
    /// two. Under the curtain in the red the music sits at -9dB behind a 2400Hz lid,
    /// and when the curtain lifts the mix returns to the RED bed, not to calm. That is
    /// the whole point: the room does not forget it is starving because a week turned.
    ///
    /// THE MIX MULTIPLIES, IT NEVER OVERWRITES. Whatever volume a source carries at
    /// registration is its BASE, and a state is a gain over that base. If something
    /// else moves the volume afterwards (a crossfade tween, a per-shot level) the base
    /// is re-read from it on the next tick, so a music manager and this controller can
    /// never fight over the same float.
    ///
    /// KILL-SWITCH — `RUNWAY_FX_MIX=0`: SetState no-ops, nothing registers, no filter
    /// component is ever added and every AudioSource keeps exactly the volume its own
    /// code set. Absent or "1" is on.
    ///
    /// HEADLESS: `Update` does not run outside play mode, so the driver is a thin
    /// wrapper over `Tick(dt)` — the whole mechanism lives there and any harness can
    /// step it by hand with a fixed dt. Update only feeds it `Time.unscaledDeltaTime`
    /// (unscaled everywhere, like every other animation in this build).
    ///
    /// ZERO ALLOCATION PER FRAME: three pre-built lists, six float arrays, plain index
    /// loops, no strings and no LINQ on the tick path. A `Voice` is allocated once, at
    /// registration.
    /// </summary>
    public sealed class RunwayMix : MonoBehaviour
    {
        /// The runtime kill-switch. Absent or "1" = on, "0" = off.
        public const string Switch = "RUNWAY_FX_MIX";

        /// Every state change takes exactly this long, whatever it is changing.
        public const float Fade = 0.3f;

        /// AudioLowPassFilter's ceiling: a lid at this height is no lid at all, and the
        /// filter component is switched OFF whenever the cutoff reaches it.
        public const float Open = 22000f;

        const int Groups = 3;
        const int Normal = 0, Curtained = 1, Binder = 2;

        static readonly string[] MomentNames = { "normal", "curtained", "binder" };

        // ══ the snapshot table ═════════════════════════════════════════════════

        struct Bus
        {
            public float Db;
            public float Cut;
            public Bus(float db, float cut) { Db = db; Cut = cut; }
        }

        /// [moment][group] — the numbers at the top of this file, in one place.
        static readonly Bus[][] Moments =
        {
            //            music                 sfx                   world
            new[] { new Bus(0f, Open),    new Bus(0f, Open), new Bus(0f, Open)    },
            new[] { new Bus(-6f, Open),   new Bus(0f, Open), new Bus(-6f, 1200f)  },
            new[] { new Bus(-3f, 900f),   new Bus(0f, Open), new Bus(-6f, 700f)   },
        };

        /// The standing bed, ADDED to whatever moment is on top of it.
        static readonly Bus[] RedBed =
        {
            new Bus(-3f, 2400f), new Bus(0f, Open), new Bus(-3f, 2400f),
        };

        // ══ what is registered ═════════════════════════════════════════════════

        sealed class Voice
        {
            public AudioSource Src;
            public AudioLowPassFilter Filter;
            public bool OwnsFilter;     // we added it, so we destroy it on the way out
            public float Base;          // the source's own level, which the state gains
            public float Written;       // the last volume WE wrote, to spot foreign edits
            public float FilterCut;     // what a borrowed filter had before we took it
            public bool FilterOn;
        }

        static readonly List<Voice>[] Voices =
        {
            new List<Voice>(8), new List<Voice>(16), new List<Voice>(8),
        };

        static readonly float[] CurDb = new float[Groups];
        static readonly float[] CurCut = { Open, Open, Open };
        static readonly float[] FromDb = new float[Groups];
        static readonly float[] FromCut = { Open, Open, Open };
        static readonly float[] ToDb = new float[Groups];
        static readonly float[] ToCut = { Open, Open, Open };

        static int _moment = Normal;
        static bool _red;
        static float _t = Fade;          // Fade = settled; 0 = a transition just started
        static RunwayMix _driver;
        static bool _switchRead;
        static bool _on = true;

        // ══ the switch ═════════════════════════════════════════════════════════

        /// Read once and cached — a per-frame environment lookup is a syscall nobody
        /// needs. Call RefreshSwitch() if the env can change under a running app.
        public static bool Enabled
        {
            get
            {
                if (_switchRead) return _on;
                _switchRead = true;
                _on = Env.Get(Switch, "1").Trim() != "0";
                return _on;
            }
        }

        /// Forget the cached kill-switch and read the environment again.
        public static void RefreshSwitch() { _switchRead = false; }

        // ══ the entry point ════════════════════════════════════════════════════

        /// The lane's one static entry point. Raising the driver is idempotent, and
        /// with the switch off it raises nothing at all. Register/SetState call it
        /// themselves, so the game never has to.
        public static RunwayMix Install()
        {
            if (!Enabled) return null;
            if (_driver != null) return _driver;
            var go = new GameObject("RunwayMix");
            go.hideFlags = HideFlags.DontSave;          // never dirties a scene
            if (Application.isPlaying) DontDestroyOnLoad(go);
            _driver = go.AddComponent<RunwayMix>();
            return _driver;
        }

        /// Hands every source back exactly as it was found and forgets everything.
        public static void Shutdown()
        {
            for (int g = 0; g < Groups; g++)
            {
                List<Voice> list = Voices[g];
                for (int i = 0; i < list.Count; i++) Restore(list[i]);
                list.Clear();
                CurDb[g] = 0f; FromDb[g] = 0f; ToDb[g] = 0f;
                CurCut[g] = Open; FromCut[g] = Open; ToCut[g] = Open;
            }
            _moment = Normal;
            _red = false;
            _t = Fade;
            if (_driver != null) { DestroyNow(_driver.gameObject); _driver = null; }
        }

        void Update() { Tick(Time.unscaledDeltaTime); }

        void OnDestroy() { if (_driver == this) _driver = null; }

        // ══ registration ═══════════════════════════════════════════════════════

        /// "music" / "sfx" / "world", case-insensitive. An unknown name leaves the
        /// source unmanaged and says so once, rather than guessing a bed for it.
        public static bool RegisterSource(AudioSource src, string group)
        {
            MixGroup g;
            if (!TryGroup(group, out g))
            {
                Debug.LogWarning("RUNWAY! mix: unknown group \"" + group
                                 + "\" — that source stays unmanaged.");
                return false;
            }
            return RegisterSource(src, g);
        }

        public static bool RegisterSource(AudioSource src, MixGroup group)
        {
            if (!Enabled || src == null) return false;
            for (int g = 0; g < Groups; g++)
            {
                List<Voice> known = Voices[g];
                for (int i = 0; i < known.Count; i++)
                    if (known[i].Src == src) return true;      // already ours
            }
            Install();
            int gi = (int)group;
            var v = new Voice { Src = src, Base = src.volume, Written = src.volume };
            if (EverMuffled(gi))
            {
                var f = src.GetComponent<AudioLowPassFilter>();
                if (f == null)
                {
                    f = src.gameObject.AddComponent<AudioLowPassFilter>();
                    f.cutoffFrequency = Open;
                    f.enabled = false;
                    v.OwnsFilter = true;
                }
                else
                {
                    v.FilterCut = f.cutoffFrequency;
                    v.FilterOn = f.enabled;
                }
                v.Filter = f;
            }
            Voices[gi].Add(v);
            ApplyVoice(v, gi);          // a source joining mid-state joins IN state
            return true;
        }

        /// Take a source back out and leave it as it was found.
        public static bool Unregister(AudioSource src)
        {
            if (src == null) return false;
            for (int g = 0; g < Groups; g++)
            {
                List<Voice> list = Voices[g];
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].Src != src) continue;
                    Restore(list[i]);
                    list.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        // ══ the states ═════════════════════════════════════════════════════════

        /// "normal" / "curtained" / "binder" / "red".
        ///
        /// "red" is the BED: it turns the standing condition on and leaves the moment
        /// where it was. "normal" clears the MOMENT only, so a run that is in the red
        /// falls back to the red bed rather than to calm — SetRed(false) is what leaves
        /// the red, and it is the same call that enters it.
        public static void SetState(string name)
        {
            if (!Enabled || string.IsNullOrEmpty(name)) return;
            if (Same(name, "red")) { Retarget(Normal, true); return; }
            if (Same(name, "normal")) { Retarget(Normal, _red); return; }
            if (Same(name, "curtained")) { Retarget(Curtained, _red); return; }
            if (Same(name, "binder")) { Retarget(Binder, _red); return; }
            Debug.LogWarning("RUNWAY! mix: unknown state \"" + name + "\" — holding "
                             + StateName + ".");
        }

        /// The standing bed. Safe to call every frame with the same value: identical
        /// targets never restart a transition and never log.
        public static void SetRed(bool red)
        {
            if (!Enabled) return;
            Retarget(_moment, red);
        }

        /// The resolved name — "curtained+red" when a moment sits over the bed.
        public static string StateName
        {
            get
            {
                if (_moment == Normal) return _red ? "red" : "normal";
                return _red ? MomentNames[_moment] + "+red" : MomentNames[_moment];
            }
        }

        /// True once the 0.3s lerp has arrived.
        public static bool Settled { get { return _t >= Fade; } }

        // ══ the one driver ═════════════════════════════════════════════════════

        /// THE WHOLE MECHANISM. Update feeds it the frame; a harness feeds it a fixed
        /// step. Nothing here allocates.
        public static void Tick(float dt)
        {
            if (!Enabled) return;
            if (_t < Fade)
            {
                _t = Mathf.Min(Fade, _t + (dt > 0f ? dt : 0f));
                float k = Fade <= 0f ? 1f : _t / Fade;
                for (int g = 0; g < Groups; g++)
                {
                    // GAIN LERPS IN dB, the scale the ear hears in — a straight lerp of
                    // the amplitude ducks fast then crawls, which reads as a fault.
                    CurDb[g] = Mathf.Lerp(FromDb[g], ToDb[g], k);
                    // THE LID LERPS GEOMETRICALLY: a sweep from 22kHz to 900Hz is heard
                    // in octaves, so the midpoint of the sweep is the geometric mean. A
                    // lid that is not moving is copied, never round-tripped through
                    // log/exp — that costs a few Hz of float and reads as a stray lid.
                    CurCut[g] = Mathf.Abs(ToCut[g] - FromCut[g]) < 0.001f
                        ? ToCut[g]
                        : Mathf.Exp(Mathf.Lerp(Mathf.Log(FromCut[g]),
                                               Mathf.Log(ToCut[g]), k));
                }
            }
            else
            {
                for (int g = 0; g < Groups; g++) { CurDb[g] = ToDb[g]; CurCut[g] = ToCut[g]; }
            }

            for (int g = 0; g < Groups; g++)
            {
                List<Voice> list = Voices[g];
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    Voice v = list[i];
                    if (v.Src == null) { list.RemoveAt(i); continue; }   // the room went
                    ApplyVoice(v, g);
                }
            }
        }

        // ══ what the probe and the ledger read ═════════════════════════════════

        public static float GainDb(MixGroup g) { return CurDb[(int)g]; }
        public static float Cutoff(MixGroup g) { return CurCut[(int)g]; }
        public static float TargetDb(MixGroup g) { return ToDb[(int)g]; }
        public static float TargetCutoff(MixGroup g) { return ToCut[(int)g]; }
        public static int Count(MixGroup g) { return Voices[(int)g].Count; }

        /// Where the mix IS, right now.
        public static string Describe() { return Line(CurDb, CurCut); }

        /// Where the mix is GOING.
        public static string DescribeTarget() { return Line(ToDb, ToCut); }

        static string Line(float[] db, float[] cut)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "music {0:0.0}dB/{1:0}Hz · sfx {2:0.0}dB/{3:0}Hz · world {4:0.0}dB/{5:0}Hz",
                db[0], cut[0], db[1], cut[1], db[2], cut[2]);
        }

        // ══ internals ══════════════════════════════════════════════════════════

        static void Retarget(int moment, bool red)
        {
            Install();
            _moment = moment;
            _red = red;
            bool moved = false;
            for (int g = 0; g < Groups; g++)
            {
                Bus m = Moments[moment][g];
                float db = m.Db + (red ? RedBed[g].Db : 0f);
                float cut = Mathf.Min(m.Cut, red ? RedBed[g].Cut : Open);
                if (Mathf.Abs(db - ToDb[g]) > 0.001f || Mathf.Abs(cut - ToCut[g]) > 0.5f)
                    moved = true;
                ToDb[g] = db;
                ToCut[g] = cut;
            }
            // The same numbers by another name is not a transition: no restart, no log.
            if (!moved) return;
            for (int g = 0; g < Groups; g++) { FromDb[g] = CurDb[g]; FromCut[g] = CurCut[g]; }
            _t = 0f;
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "MIX {0}: {1} over {2:0.00}s", StateName, DescribeTarget(), Fade));
        }

        static void ApplyVoice(Voice v, int g)
        {
            float gain = Mathf.Pow(10f, CurDb[g] / 20f);
            // SOMEBODY ELSE MOVED IT (a crossfade, a per-shot level): that is the new
            // base, not a fight. Re-read it through the gain we last applied.
            if (Mathf.Abs(v.Src.volume - v.Written) > 0.0001f)
                v.Base = gain > 0.0001f ? v.Src.volume / gain : v.Src.volume;
            float want = v.Base * gain;
            v.Src.volume = want;
            v.Written = want;
            if (v.Filter == null) return;
            v.Filter.cutoffFrequency = CurCut[g];
            bool lid = CurCut[g] < Open - 1f;
            if (v.Filter.enabled != lid) v.Filter.enabled = lid;   // no DSP when open
        }

        static void Restore(Voice v)
        {
            if (v.Src != null) v.Src.volume = v.Base;
            if (v.Filter == null) return;
            if (v.OwnsFilter) { DestroyNow(v.Filter); return; }
            v.Filter.cutoffFrequency = v.FilterCut;
            v.Filter.enabled = v.FilterOn;
        }

        /// Only the groups the table ever puts a lid on carry a filter component, so an
        /// SFX source costs no DSP block at all.
        static bool EverMuffled(int g)
        {
            for (int m = 0; m < Moments.Length; m++)
                if (Moments[m][g].Cut < Open - 1f) return true;
            return RedBed[g].Cut < Open - 1f;
        }

        static bool TryGroup(string name, out MixGroup g)
        {
            g = MixGroup.Sfx;
            if (string.IsNullOrEmpty(name)) return false;
            if (Same(name, "music")) { g = MixGroup.Music; return true; }
            if (Same(name, "sfx")) { g = MixGroup.Sfx; return true; }
            if (Same(name, "world")) { g = MixGroup.World; return true; }
            return false;
        }

        static bool Same(string a, string b)
        {
            return string.Equals(a.Trim(), b, System.StringComparison.OrdinalIgnoreCase);
        }

        static void DestroyNow(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }
    }
}
