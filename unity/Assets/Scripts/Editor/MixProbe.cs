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
    /// D7 EVIDENCE — the mix walked headless.
    ///
    /// Audio is INAUDIBLE in batch mode: there is no output device, and -nographics
    /// gives the DSP nothing to run into. So the ear is not the proof here — the state
    /// math is. This registers six dummy AudioSources across the three beds, walks
    /// every state (including the layered ones), and asserts the exact volume and
    /// cutoff of every source and every group, both MID-LERP and at rest, against
    /// numbers derived here by hand rather than read back from the controller:
    ///
    ///     volume = base × 10^(dB/20)          the dB→amplitude law
    ///     mid dB = (from + to) / 2            a straight lerp, halfway through 0.3s
    ///     mid Hz = sqrt(from × to)            the geometric midpoint of the sweep
    ///
    /// Update never ticks in edit mode, so the walk drives RunwayMix.Tick(dt) by hand
    /// with a fixed step: 0.15s for the mid-lerp reads and 1/30s frames to settle.
    ///
    ///   Unity -batchmode -nographics -quit -projectPath unity \
    ///         -executeMethod Runway.EditorTools.MixProbe.Run
    ///
    /// Writes RUNWAY_MIX_LOG (or a temp file) and exits 1 on any failed assertion.
    /// </summary>
    public static class MixProbe
    {
        const float Frame = 1f / 30f;
        const double Open = 22000.0;

        static readonly List<string> Log = new List<string>();
        static int _checks;
        static int _fails;

        // the cast: two sources per bed, distinct base levels so a mix that OVERWRITES
        // instead of multiplying cannot pass
        static AudioSource _mA, _mB, _sA, _sB, _wA, _wB;
        const float BaseMa = 0.80f, BaseMb = 0.35f;
        const float BaseSa = 1.00f, BaseSb = 0.60f;
        const float BaseWa = 0.70f, BaseWb = 0.25f;
        const float BorrowedCut = 5000f;    // _mB brings its own filter to the party

        public static void Run()
        {
            Log.Clear();
            _checks = 0;
            _fails = 0;

            Say("RUNWAY! D7 · mix state walk · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Say("unity " + Application.unityVersion + " · batchmode -nographics");
            Say("CAVEAT: nothing is audible here. There is no output device in batch, so");
            Say("this proves the NUMBERS the mix writes, not the sound they make. The ear");
            Say("test belongs to a run with a window and speakers.");
            Say("");

            RunwayMix.RefreshSwitch();
            string raw = Environment.GetEnvironmentVariable("RUNWAY_FX_MIX");
            Say("switch RUNWAY_FX_MIX=" + (string.IsNullOrEmpty(raw) ? "(absent)" : raw)
                + " → mix " + (RunwayMix.Enabled ? "ON" : "OFF"));
            Truth("kill-switch is ON for the walk", RunwayMix.Enabled);
            if (!RunwayMix.Enabled) { Finish(); return; }

            Truth("Install() raised the driver", RunwayMix.Install() != null);
            Say("");

            Cast();
            Walk();
            Teardown();
            KillSwitch();
            Finish();
        }

        // ══ the cast ═══════════════════════════════════════════════════════════

        static void Cast()
        {
            Say("── registration ─────────────────────────────────────────────────");
            _mA = Dummy("music_a", BaseMa);
            _mB = Dummy("music_b", BaseMb);
            var borrowed = _mB.gameObject.AddComponent<AudioLowPassFilter>();
            borrowed.cutoffFrequency = BorrowedCut;
            borrowed.enabled = true;
            _sA = Dummy("sfx_a", BaseSa);
            _sB = Dummy("sfx_b", BaseSb);
            _wA = Dummy("world_a", BaseWa);
            _wB = Dummy("world_b", BaseWb);

            Truth("music_a registered", RunwayMix.RegisterSource(_mA, "music"));
            Truth("music_b registered", RunwayMix.RegisterSource(_mB, "Music"));      // case
            Truth("sfx_a registered", RunwayMix.RegisterSource(_sA, MixGroup.Sfx));
            Truth("sfx_b registered", RunwayMix.RegisterSource(_sB, " sfx "));        // trim
            Truth("world_a registered", RunwayMix.RegisterSource(_wA, "world"));
            Truth("world_b registered", RunwayMix.RegisterSource(_wB, "world"));
            Truth("a second registration is a no-op",
                  RunwayMix.RegisterSource(_mA, "world") && RunwayMix.Count(MixGroup.World) == 2);
            Truth("an unknown bed is refused", !RunwayMix.RegisterSource(_wA, "drums"));
            Truth("music bed holds 2", RunwayMix.Count(MixGroup.Music) == 2);
            Truth("sfx bed holds 2", RunwayMix.Count(MixGroup.Sfx) == 2);
            Truth("world bed holds 2", RunwayMix.Count(MixGroup.World) == 2);

            Truth("music sources carry a filter", Filter(_mA) != null && Filter(_mB) != null);
            Truth("world sources carry a filter", Filter(_wA) != null && Filter(_wB) != null);
            Truth("SFX CARRIES NO FILTER AT ALL — no DSP block, ever",
                  Filter(_sA) == null && Filter(_sB) == null);
            Say("");
        }

        static AudioSource Dummy(string name, float volume)
        {
            var go = new GameObject("mixprobe_" + name, typeof(AudioSource));
            go.hideFlags = HideFlags.DontSave;
            var src = go.GetComponent<AudioSource>();
            src.playOnAwake = false;
            src.volume = volume;
            return src;
        }

        // ══ the walk ═══════════════════════════════════════════════════════════

        static void Walk()
        {
            // at rest in normal, straight off registration
            Rest("normal (at registration)", 0, Open, 0, Open, 0, Open);

            RunwayMix.SetState("curtained");
            Mid("curtained @0.15s", -3, Open, 0, Open, -3, Math.Sqrt(Open * 1200.0));
            Settle();
            Rest("curtained", -6, Open, 0, Open, -6, 1200);

            RunwayMix.SetState("normal");
            Settle();
            Rest("normal", 0, Open, 0, Open, 0, Open);

            RunwayMix.SetState("binder");
            Mid("binder @0.15s", -1.5, Math.Sqrt(Open * 900.0), 0, Open,
                                 -3.0, Math.Sqrt(Open * 700.0));
            Settle();
            Rest("binder", -3, 900, 0, Open, -6, 700);

            RunwayMix.SetState("normal");
            Settle();
            Rest("normal", 0, Open, 0, Open, 0, Open);

            RunwayMix.SetState("red");
            Mid("red @0.15s", -1.5, Math.Sqrt(Open * 2400.0), 0, Open,
                              -1.5, Math.Sqrt(Open * 2400.0));
            Settle();
            Rest("red (cash < 0)", -3, 2400, 0, Open, -3, 2400);

            // the layer: a moment over the standing bed
            RunwayMix.SetState("curtained");
            Settle();
            Rest("curtained + red", -9, 2400, 0, Open, -9, 1200);
            Truth("the resolved name says both", RunwayMix.StateName == "curtained+red");

            // the curtain lifts and the run is STILL in the red
            RunwayMix.SetState("normal");
            Settle();
            Rest("normal while red → the red bed", -3, 2400, 0, Open, -3, 2400);
            Truth("the bed survived the moment", RunwayMix.StateName == "red");

            // the binder, opened while starving
            RunwayMix.SetState("binder");
            Settle();
            Rest("binder + red", -6, 900, 0, Open, -9, 700);

            RunwayMix.SetState("normal");
            RunwayMix.SetRed(false);
            Settle();
            Rest("out of the red", 0, Open, 0, Open, 0, Open);

            // idling must not drift
            for (int i = 0; i < 60; i++) RunwayMix.Tick(Frame);
            Rest("normal after 2s of idle", 0, Open, 0, Open, 0, Open);

            // a source that joins mid-state joins IN state
            RunwayMix.SetState("binder");
            Settle();
            var late = Dummy("late_world", 0.5f);
            RunwayMix.RegisterSource(late, "world");
            Near("late world source is already muffled", late.volume,
                 0.5 * Math.Pow(10.0, -6.0 / 20.0), 0.0005);
            Near("late world source's lid is down", Filter(late).cutoffFrequency, 700, 1.0);
            Truth("late world source's filter is live", Filter(late).enabled);
            Truth("late source unregisters clean", RunwayMix.Unregister(late));
            Near("unregistered source got its own level back", late.volume, 0.5, 0.0005);
            Truth("unregistered source lost the filter we added", Filter(late) == null);
            UnityEngine.Object.DestroyImmediate(late.gameObject);

            RunwayMix.SetState("normal");
            Settle();
            Say("");
        }

        static void Settle()
        {
            for (int i = 0; i < 12; i++) RunwayMix.Tick(Frame);   // 0.4s > the 0.3s fade
            Truth("settled", RunwayMix.Settled);
        }

        // ══ the assertions ═════════════════════════════════════════════════════

        /// One tick of exactly half the fade: every number must sit on the midpoint.
        static void Mid(string label, double mDb, double mCut,
                        double sDb, double sCut, double wDb, double wCut)
        {
            RunwayMix.Tick(RunwayMix.Fade * 0.5f);
            Truth(label + " is mid-transition", !RunwayMix.Settled);
            Board(label, mDb, mCut, sDb, sCut, wDb, wCut);
        }

        static void Rest(string label, double mDb, double mCut,
                         double sDb, double sCut, double wDb, double wCut)
        {
            Board(label, mDb, mCut, sDb, sCut, wDb, wCut);
        }

        static void Board(string label, double mDb, double mCut,
                          double sDb, double sCut, double wDb, double wCut)
        {
            Say("── " + label + "  [" + RunwayMix.StateName + "]");
            Say("   " + RunwayMix.Describe());
            Bed("music", MixGroup.Music, mDb, mCut, _mA, BaseMa, _mB, BaseMb);
            Bed("sfx", MixGroup.Sfx, sDb, sCut, _sA, BaseSa, _sB, BaseSb);
            Bed("world", MixGroup.World, wDb, wCut, _wA, BaseWa, _wB, BaseWb);
        }

        static void Bed(string name, MixGroup g, double db, double cut,
                        AudioSource a, float baseA, AudioSource b, float baseB)
        {
            Near(name + " bed gain (dB)", RunwayMix.GainDb(g), db, 0.01);
            Near(name + " bed lid (Hz)", RunwayMix.Cutoff(g), cut, Tol(cut));
            double gain = Math.Pow(10.0, db / 20.0);
            Near(name + "_a volume", a.volume, baseA * gain, 0.0005);
            Near(name + "_b volume", b.volume, baseB * gain, 0.0005);
            var fa = Filter(a);
            var fb = Filter(b);
            if (g == MixGroup.Sfx)
            {
                Truth(name + " carries no filter", fa == null && fb == null);
                return;
            }
            if (fa == null || fb == null)
            {
                Truth(name + " filter present", false);
                return;
            }
            Near(name + "_a cutoff", fa.cutoffFrequency, cut, Tol(cut));
            Near(name + "_b cutoff", fb.cutoffFrequency, cut, Tol(cut));
            bool lid = cut < Open - 1.0;
            Truth(name + " filter " + (lid ? "engaged" : "bypassed"),
                  fa.enabled == lid && fb.enabled == lid);
        }

        /// Tight on purpose: a lid that is not moving must be EXACT, and a lid mid-sweep
        /// may only carry the float error of one log/exp round trip (~0.005Hz at 7kHz).
        static double Tol(double hz) { return Math.Max(0.05, hz * 0.00001); }

        // ══ handing everything back ════════════════════════════════════════════

        static void Teardown()
        {
            Say("── shutdown: every source handed back as it was found ───────────");
            RunwayMix.SetState("binder");     // shut down mid-duck, the hard case
            for (int i = 0; i < 3; i++) RunwayMix.Tick(Frame);
            RunwayMix.Shutdown();

            Near("music_a back to its own level", _mA.volume, BaseMa, 0.0005);
            Near("music_b back to its own level", _mB.volume, BaseMb, 0.0005);
            Near("sfx_a untouched", _sA.volume, BaseSa, 0.0005);
            Near("world_a back to its own level", _wA.volume, BaseWa, 0.0005);
            Truth("the filter WE added to music_a is gone", Filter(_mA) == null);
            Truth("the filter WE added to world_a is gone", Filter(_wA) == null);
            Truth("music_b's OWN filter survived", Filter(_mB) != null);
            if (Filter(_mB) != null)
            {
                Near("music_b's own cutoff restored", Filter(_mB).cutoffFrequency,
                     BorrowedCut, 1.0);
                Truth("music_b's own filter left enabled", Filter(_mB).enabled);
            }
            Truth("every bed is empty", RunwayMix.Count(MixGroup.Music) == 0
                  && RunwayMix.Count(MixGroup.Sfx) == 0
                  && RunwayMix.Count(MixGroup.World) == 0);
            Say("");
        }

        // ══ the kill-switch ════════════════════════════════════════════════════

        static void KillSwitch()
        {
            Say("── RUNWAY_FX_MIX=0 · the lane is not there ──────────────────────");
            string had = Environment.GetEnvironmentVariable("RUNWAY_FX_MIX");
            Environment.SetEnvironmentVariable("RUNWAY_FX_MIX", "0");
            RunwayMix.RefreshSwitch();

            Truth("switch reads OFF", !RunwayMix.Enabled);
            Truth("Install() raises nothing", RunwayMix.Install() == null);

            var quiet = Dummy("switch_off", 0.9f);
            Truth("registration is refused", !RunwayMix.RegisterSource(quiet, "music"));
            Truth("NO filter component was added", Filter(quiet) == null);
            RunwayMix.SetState("binder");
            RunwayMix.SetRed(true);
            for (int i = 0; i < 30; i++) RunwayMix.Tick(Frame);
            Near("the source keeps the level its own code set", quiet.volume, 0.9, 0.0001);
            Truth("still no filter after a full second of ticks", Filter(quiet) == null);
            Near("music_a is still untouched too", _mA.volume, BaseMa, 0.0005);
            UnityEngine.Object.DestroyImmediate(quiet.gameObject);

            Environment.SetEnvironmentVariable("RUNWAY_FX_MIX", had);
            RunwayMix.RefreshSwitch();
            Truth("switch reads back ON", RunwayMix.Enabled);
            Say("");
        }

        // ══ the paperwork ══════════════════════════════════════════════════════

        static AudioLowPassFilter Filter(AudioSource s)
        {
            return s == null ? null : s.GetComponent<AudioLowPassFilter>();
        }

        static void Near(string what, double actual, double expect, double tol)
        {
            _checks++;
            bool ok = Math.Abs(actual - expect) <= tol;
            if (!ok) _fails++;
            Say(string.Format(CultureInfo.InvariantCulture,
                "   {0} {1,-44} {2,12:0.0000}   expect {3,12:0.0000}",
                ok ? "  " : "!!", what, actual, expect));
        }

        static void Truth(string what, bool ok)
        {
            _checks++;
            if (!ok) _fails++;
            Say("   " + (ok ? "  " : "!!") + " " + what.PadRight(44)
                + (ok ? "         yes" : "          NO"));
        }

        static void Say(string s)
        {
            Log.Add(s);
        }

        static void Finish()
        {
            Cleanup();
            string verdict = string.Format("MIXPROBE: {0} checks · {1} failed", _checks, _fails);
            Say("");
            Say(verdict);
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
                Debug.LogError("MIXPROBE could not write " + path + ": " + e.Message);
                _fails++;
            }
            if (_fails > 0) EditorApplication.Exit(1);
        }

        static void Cleanup()
        {
            RunwayMix.Shutdown();
            Drop(_mA); Drop(_mB); Drop(_sA); Drop(_sB); Drop(_wA); Drop(_wB);
            _mA = _mB = _sA = _sB = _wA = _wB = null;
        }

        static void Drop(AudioSource s)
        {
            if (s != null) UnityEngine.Object.DestroyImmediate(s.gameObject);
        }

        static string OutPath()
        {
            string p = Environment.GetEnvironmentVariable("RUNWAY_MIX_LOG");
            if (!string.IsNullOrEmpty(p)) return p;
            return Path.Combine(Path.GetTempPath(), "runway_mix_states.txt");
        }
    }
}
#endif
