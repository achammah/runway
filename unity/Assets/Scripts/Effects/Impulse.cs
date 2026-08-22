using UnityEngine;
using Runway.App;

namespace Runway.Effects
{
    /// <summary>
    /// WEIGHT — the hand-rolled spring that lets the stage take a hit.
    ///
    /// Two events in the whole game are allowed to move the frame:
    ///   · the week BACKFIRED  → Shake(6px, 250ms)  — the room flinches
    ///   · the die SETTLED     → Punch(1.02, 120ms) — the table takes the weight
    /// Nothing else. A fine week is fine; a brilliant week is brilliant; neither
    /// shoves the player. RESTRAINT IS THE LAW, and it is written into this file
    /// rather than left to whoever calls it: <see cref="Verdict"/> is a no-op for
    /// every band except "backfired".
    ///
    /// NO PACKAGES, NO CINEMACHINE. Each shot is a critically damped spring —
    /// x'' + 2ω·x' + ω²·x = 0, ζ = 1 exactly — and it is advanced by its CLOSED FORM
    /// rather than by an integrator, because ζ = 1 has one:
    ///
    ///     A = x                B = v + ω·x               e = exp(-ω·h)
    ///     x' = (A + B·h)·e     v' = (B·(1 - ω·h) - ω·A)·e
    ///
    /// That matters for more than tidiness. A stepped integrator bled ~15% of the
    /// amplitude at 30fps, so Shake(6px) delivered 4.8px and the number in the call
    /// meant nothing; the closed form is exact for any h, so 6px is 6px whether the
    /// frame is 4ms or 33ms — measured identical to four decimals at 240/60/30fps.
    ///
    /// Critical damping means the spring itself CANNOT overshoot: every crossing of
    /// zero in a shake is an authored kick (the Godot original's bag-refusal shake is
    /// literally `[-6, +6, -4, 0]`), never a wobble. That is what keeps 6px reading as
    /// a flinch rather than a rattle.
    ///
    /// THE SPRING IS A PURE FUNCTION OF STATE + dt. <see cref="Step"/> is public and
    /// takes its own delta, so the effect can be driven by the game (the component's
    /// own Update) or stepped deterministically by a harness with no play mode at all.
    ///
    /// EXACT REST IS A CONTRACT, not an approximation. The rest pose is captured the
    /// moment the stage goes from still to moving, every frame writes `rest + offset`
    /// absolutely (never accumulates), the output is multiplied by a tail gain that
    /// reaches exactly 0 at the end of the shot, and the last act of the last shot is
    /// to ASSIGN the captured rest back. Position delta and scale delta after settle
    /// are bitwise zero.
    ///
    /// ZERO PER-FRAME ALLOCATION: fixed slot array of structs, static kick schedules,
    /// no closures, no LINQ, no strings, nothing new'd after the one driver object.
    ///
    /// KILL-SWITCH: the runtime environment variable RUNWAY_FX_IMPULSE.
    /// Absent or "1" → on. "0" (also "off"/"false"/"no") → Shake and Punch become
    /// no-ops and the stage is never touched at all.
    ///
    ///     Impulse.Verdict(band);     // backfired shakes; fine/brilliant/risky do not
    ///     Impulse.DieSettled();      // the 2% punch-in
    /// </summary>
    public sealed class Impulse : MonoBehaviour
    {
        /// The kill-switch's environment variable.
        public const string SwitchVar = "RUNWAY_FX_IMPULSE";

        // ── the house numbers (D4b) ────────────────────────────────────────────
        /// A week that backfired: 6px, 250ms.
        public const float BackfiredPx = 6f;
        public const float BackfiredMs = 250f;
        /// The die settling: a 2% punch-in over 120ms.
        public const float DieScale = 1.02f;
        public const float DieMs = 120f;

        // ── the solver's constants ─────────────────────────────────────────────
        const int Slots = 8;
        /// Where the tail gain starts easing the residue to a mathematical zero.
        const float TailFrom = 0.80f;
        /// ω for each kind, in units of 1/life. Tuned against the sampled curve so
        /// that the FIRST 30fps frame after the hit lands on the peak — at 30fps a
        /// 120ms punch is four frames and a 250ms shake is eight, so a peak that
        /// falls between samples is a peak the player never sees. Measured: shake
        /// 5.99 of 6.00px and punch 0.0200 of 0.0200, identical at 240/60/30fps.
        const float ShakeOmega = 8f;
        const float PunchOmega = 3.5f;
        const float E = 2.7182818f;

        /// Alternating kicks, as fractions of the shot's life and of its amplitude.
        /// The shape is the Godot bag-refusal shake — out, back past, out, dead still.
        static readonly float[] ShakeAt = { 0f, 0.30f, 0.58f };
        static readonly float[] ShakeGain = { 1f, -0.62f, 0.34f };
        /// A punch is one hit and one settle.
        static readonly float[] PunchAt = { 0f };
        static readonly float[] PunchGain = { 1f };

        /// The house shake axis. The original shakes horizontally and so does this.
        static readonly Vector2 DefaultDir = new Vector2(1f, 0f);

        // ── one shot of spring ─────────────────────────────────────────────────
        struct Shot
        {
            public bool Live;
            public bool IsScale;   // false = position offset, true = uniform scale
            public float T;        // seconds elapsed
            public float Life;     // seconds total
            public float Omega;
            public float X;        // spring position, normalised so a unit kick peaks at 1
            public float V;        // spring velocity
            public float Amp;      // pixels (shake) or scale delta (punch)
            public float DirX;
            public float DirY;
            public int Kick;       // how many scheduled kicks have been spent
        }

        static readonly Shot[] _shots = new Shot[Slots];
        static int _liveCount;

        static RectTransform _bound;
        static Vector2 _restPos;
        static Vector3 _restScale;
        static Impulse _driver;
        static int _switch = -1;   // -1 unread, 0 off, 1 on

        // ══ the switch ═════════════════════════════════════════════════════════

        /// False when RUNWAY_FX_IMPULSE says so. Read once, then cached.
        public static bool Enabled
        {
            get
            {
                if (_switch < 0)
                {
                    string v = Env.Get(SwitchVar, "1").Trim().ToLowerInvariant();
                    _switch = (v == "0" || v == "off" || v == "false" || v == "no") ? 0 : 1;
                }
                return _switch == 1;
            }
        }

        /// Re-read the environment (after keys.env changes, or for the switch matrix).
        /// Anything in flight is settled first, so a switch-off never strands an offset.
        public static void RefreshSwitch()
        {
            Rest();
            _switch = -1;
        }

        /// Force the switch without touching the environment — the D8 matrix run.
        public static void SetEnabled(bool on)
        {
            Rest();
            _switch = on ? 1 : 0;
        }

        // ══ the target ═════════════════════════════════════════════════════════

        /// What the offsets are applied to. Defaults to Boot's Stage — the fixed
        /// 1536x1024 rect the whole game draws under — and never reaches into Boot
        /// for anything else.
        public static RectTransform Target
        {
            get
            {
                if (_bound != null) return _bound;
                Boot b = Boot.Instance;
                return b != null ? b.Stage : null;
            }
        }

        /// Point the effect at a rect of your own (a harness, or one screen instead of
        /// the whole stage). Anything in flight is settled onto the OLD target first.
        public static void Bind(RectTransform rt)
        {
            Rest();
            _bound = rt;
        }

        /// Back to Boot's Stage.
        public static void Unbind()
        {
            Rest();
            _bound = null;
        }

        // ══ the two entry points ═══════════════════════════════════════════════

        /// A positional flinch: `px` pixels of first swing over `ms` milliseconds,
        /// along `dir`. The house axis is horizontal.
        public static void Shake(float px, float ms)
        {
            Shake(px, ms, DefaultDir);
        }

        public static void Shake(float px, float ms, Vector2 dir)
        {
            float len = dir.magnitude;
            float dx, dy;
            if (len < 0.0001f) { dx = DefaultDir.x; dy = DefaultDir.y; }
            else { dx = dir.x / len; dy = dir.y / len; }
            Fire(false, px, ms, ShakeOmega, dx, dy);
        }

        /// A weight punch: the stage swells to `scale` (1.02 = 2%) and settles back
        /// over `ms` milliseconds. It never overshoots on the way home.
        public static void Punch(float scale, float ms)
        {
            Fire(true, scale - 1f, ms, PunchOmega, 0f, 0f);
        }

        // ══ the two hookups, with the restraint law inside them ════════════════

        /// THE ONE VERDICT THAT MOVES THE FRAME. "backfired" flinches; "fine",
        /// "brilliant" and "risky" are silent — a good week is never shoved, and a
        /// mixed one is not a punishment. Safe to call with any band, or with null.
        public static void Verdict(string band)
        {
            if (band == null) return;
            if (!band.Equals("backfired", System.StringComparison.OrdinalIgnoreCase)) return;
            Shake(BackfiredPx, BackfiredMs);
        }

        /// The die has stopped moving on the felt.
        public static void DieSettled()
        {
            Punch(DieScale, DieMs);
        }

        // ══ install / rest ═════════════════════════════════════════════════════

        /// Optional: bring the driver up early and print the switch state once. The
        /// effect installs itself on the first Shake/Punch, so nothing has to call this.
        public static void Install()
        {
            Debug.Log("RUNWAY! impulse: " + (Enabled ? "on" : "off (" + SwitchVar + "=0)"));
            EnsureDriver();
        }

        /// Everything in flight is dropped and the target is put back EXACTLY where it
        /// rested. Called on bind, on switch changes, and by the run when it cancels.
        public static void Rest()
        {
            bool had = _liveCount > 0;
            for (int i = 0; i < Slots; i++) _shots[i].Live = false;
            _liveCount = 0;
            if (!had) return;
            RectTransform t = Target;
            if (t != null) SnapRest(t);
            if (_driver != null) _driver.enabled = false;
        }

        /// True while the stage is being moved by this effect.
        public static bool Busy { get { return _liveCount > 0; } }

        /// The rest pose currently held (empty offsets when nothing is in flight).
        public static Vector2 RestPosition { get { return _restPos; } }
        public static Vector3 RestScale { get { return _restScale; } }

        // ══ the pump ═══════════════════════════════════════════════════════════

        /// Advance every live shot by `dt` and write the summed offset onto the target.
        /// PUBLIC on purpose: the game drives it from Update, a harness drives it by
        /// hand, and both get bit-identical motion for the same deltas.
        public static void Step(float dt)
        {
            if (_liveCount <= 0) return;
            if (dt <= 0f) return;
            RectTransform t = Target;
            if (t == null)
            {
                // the stage went away under us — drop everything rather than chase it
                for (int i = 0; i < Slots; i++) _shots[i].Live = false;
                _liveCount = 0;
                if (_driver != null) _driver.enabled = false;
                return;
            }

            float ox = 0f, oy = 0f, sc = 0f;
            int live = 0;
            for (int i = 0; i < Slots; i++)
            {
                if (!_shots[i].Live) continue;
                Advance(i, dt);
                if (!_shots[i].Live) continue;
                live++;
                float o = Output(i);
                if (_shots[i].IsScale) sc += o;
                else { ox += o * _shots[i].DirX; oy += o * _shots[i].DirY; }
            }
            _liveCount = live;

            // THE LAST ACT OF THE LAST SHOT: rest is ASSIGNED, not eased to. The tail
            // gain has already brought the offset to a mathematical zero, so this is a
            // continuous landing that also happens to be bitwise exact.
            if (live == 0)
            {
                SnapRest(t);
                if (_driver != null) _driver.enabled = false;
                return;
            }
            Apply(t, ox, oy, sc);
        }

        void Update()
        {
            // unscaled, like every other animation in this build (COMPILE-RISKS B13)
            Step(Time.unscaledDeltaTime);
        }

        void OnDestroy()
        {
            if (_driver == this) _driver = null;
        }

        // ══ the machinery ══════════════════════════════════════════════════════

        static void Fire(bool isScale, float amp, float ms, float omegaScale,
                         float dx, float dy)
        {
            if (!Enabled) return;
            if (ms <= 0f) return;
            if (amp > -0.00001f && amp < 0.00001f) return;
            RectTransform t = Target;
            if (t == null) return;

            // the rest pose belongs to the moment the stage STOPS being still
            if (_liveCount <= 0) CaptureRest(t);

            int slot = -1;
            for (int i = 0; i < Slots; i++) { if (!_shots[i].Live) { slot = i; break; } }
            if (slot < 0) return;   // eight simultaneous impulses is already too many

            float life = ms / 1000f;
            float omega = omegaScale / life;

            _shots[slot].Live = true;
            _shots[slot].IsScale = isScale;
            _shots[slot].T = 0f;
            _shots[slot].Life = life;
            _shots[slot].Omega = omega;
            _shots[slot].X = 0f;
            _shots[slot].V = 0f;
            _shots[slot].Amp = amp;
            _shots[slot].DirX = dx;
            _shots[slot].DirY = dy;
            _shots[slot].Kick = 0;
            _liveCount++;

            EnsureDriver();
        }

        /// The closed-form critically-damped step, cut at every scheduled kick so the
        /// kick lands at its exact time rather than at the next frame boundary. At
        /// most four segments per frame — the exp calls are the whole cost.
        static void Advance(int i, float dt)
        {
            float[] at = _shots[i].IsScale ? PunchAt : ShakeAt;
            float[] gain = _shots[i].IsScale ? PunchGain : ShakeGain;
            float w = _shots[i].Omega;
            float unit = w * E;            // a kick of `unit` peaks the spring at 1.0
            float life = _shots[i].Life;

            float remain = dt;
            while (remain > 0f)
            {
                // spend every kick that is due at or before now
                while (_shots[i].Kick < at.Length
                       && at[_shots[i].Kick] * life <= _shots[i].T + 1e-7f)
                {
                    _shots[i].V += unit * gain[_shots[i].Kick];
                    _shots[i].Kick++;
                }

                // run to the next kick, the end of the shot, or the end of the frame
                float h = remain;
                if (_shots[i].Kick < at.Length)
                {
                    float toKick = at[_shots[i].Kick] * life - _shots[i].T;
                    if (toKick < h) h = toKick;
                }
                float toEnd = life - _shots[i].T;
                if (toEnd < h) h = toEnd;
                if (h <= 0f) { _shots[i].T = life; break; }   // never spin

                float e = Mathf.Exp(-w * h);
                float a = _shots[i].X;
                float b = _shots[i].V + w * a;
                _shots[i].X = (a + b * h) * e;
                _shots[i].V = (b * (1f - w * h) - w * a) * e;
                _shots[i].T += h;
                remain -= h;

                if (_shots[i].T >= life) break;
            }

            if (_shots[i].T >= life) _shots[i].Live = false;
        }

        /// The shot's contribution, with the tail gain that lands it on a true zero.
        static float Output(int i)
        {
            float life = _shots[i].Life;
            float u = life > 0f ? _shots[i].T / life : 1f;
            float g = 1f;
            if (u > TailFrom)
            {
                float x = (u - TailFrom) / (1f - TailFrom);
                if (x > 1f) x = 1f;
                g = 1f - (x * x * (3f - 2f * x));   // smoothstep down; exactly 0 at u = 1
            }
            return _shots[i].Amp * _shots[i].X * g;
        }

        static void CaptureRest(RectTransform t)
        {
            _restPos = t.anchoredPosition;
            _restScale = t.localScale;
        }

        static void Apply(RectTransform t, float ox, float oy, float sc)
        {
            t.anchoredPosition = new Vector2(_restPos.x + ox, _restPos.y + oy);
            float k = 1f + sc;
            t.localScale = new Vector3(_restScale.x * k, _restScale.y * k, _restScale.z);
        }

        static void SnapRest(RectTransform t)
        {
            t.anchoredPosition = _restPos;
            t.localScale = _restScale;
        }

        /// ONE DRIVER, and never in edit mode — a harness steps the spring itself.
        static void EnsureDriver()
        {
            if (!Application.isPlaying) return;
            if (_driver != null) { _driver.enabled = true; return; }
            var go = new GameObject("impulse");
            DontDestroyOnLoad(go);
            _driver = go.AddComponent<Impulse>();
        }
    }
}
