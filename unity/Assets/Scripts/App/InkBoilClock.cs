using System.Collections.Generic;
using UnityEngine;

namespace Runway.App
{
    /// <summary>
    /// THE ONE CLOCK. Hand-drawn boil is shot on a held frame: the whole drawing turns
    /// over together, eight times a second. So there is exactly one timer in the build
    /// and exactly one Update, and every `InkBoil` on screen is a row in its list.
    ///
    /// IT IDLES. The clock's own behaviour is disabled the moment the last InkBoil goes
    /// down, so a screen with no drawn ink — or a build with RUNWAY_FX_BOIL=0, where no
    /// InkBoil is ever made — pays not even an empty Update. The first registration
    /// wakes it again.
    ///
    /// IT ALLOCATES NOTHING. The list is built once and walked by index; a tick writes
    /// one texture pointer per live element and takes no closure, no enumerator and no
    /// string. `Time.unscaledDeltaTime` for the same reason everything else in this
    /// build uses it: the ink must keep breathing if the world is ever paused.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InkBoilClock : MonoBehaviour
    {
        static InkBoilClock _clock;
        static readonly List<InkBoil> _live = new List<InkBoil>(64);
        static float _carry;
        static int _frame;

        /// Which held drawing the whole screen is on.
        public static int Frame { get { return _frame; } }

        /// How many lines are boiling right now.
        public static int LiveCount { get { return _live.Count; } }

        /// True only while there is something to boil.
        public static bool Ticking { get { return _clock != null && _clock.enabled; } }

        internal static void Register(InkBoil boil)
        {
            if (boil == null) return;
            if (_live.Contains(boil)) { boil.ShowFrame(_frame); return; }
            _live.Add(boil);
            Wake();
            boil.ShowFrame(_frame);
        }

        internal static void Unregister(InkBoil boil)
        {
            int i = _live.IndexOf(boil);
            if (i >= 0) _live.RemoveAt(i);
            if (_live.Count == 0 && _clock != null) _clock.enabled = false;
        }

        static void Wake()
        {
            if (_clock == null)
            {
                var go = new GameObject("~InkBoilClock");
                go.hideFlags = HideFlags.HideAndDontSave;
                if (Application.isPlaying) DontDestroyOnLoad(go);
                _clock = go.AddComponent<InkBoilClock>();
            }
            _clock.enabled = true;
        }

        void Update()
        {
            int n = _live.Count;
            if (n == 0) { enabled = false; return; }

            _carry += Time.unscaledDeltaTime;
            float step = 1f / DrawnBoil.Fps;
            if (_carry < step) return;

            int steps = (int)(_carry / step);
            _carry -= steps * step;
            _frame = (_frame + steps) % DrawnBoil.Frames;
            Paint();
        }

        static void Paint()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                InkBoil boil = _live[i];
                if (boil == null) { _live.RemoveAt(i); continue; }
                boil.ShowFrame(_frame);
            }
            if (_live.Count == 0 && _clock != null) _clock.enabled = false;
        }

        /// Hold the whole screen on one held drawing — how the shot harness films the
        /// three frames without waiting 1/8s of real time between them.
        public static void SetFrame(int frame)
        {
            int f = frame % DrawnBoil.Frames;
            if (f < 0) f += DrawnBoil.Frames;
            _frame = f;
            _carry = 0f;
            Paint();
        }

        /// Empty the clock and take it off the scene — between harness runs, and for the
        /// kill-switch matrix.
        public static void Shutdown()
        {
            _live.Clear();
            _carry = 0f;
            _frame = 0;
            if (_clock == null) return;
            GameObject go = _clock.gameObject;
            _clock = null;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }
}
