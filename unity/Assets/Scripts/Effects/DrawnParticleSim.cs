using UnityEngine;

namespace Runway.Effects
{
    /// <summary>
    /// THE SIMULATION, HAND-ROLLED — the half of a particle system that moves things.
    ///
    /// WHY NOT UnityEngine.ParticleSystem. This project's Packages/manifest.json is a
    /// hand-trimmed list of built-in modules and `com.unity.modules.particlesystem` is
    /// not on it, so `ParticleSystem` does not resolve at all here. Adding it is one
    /// line in a SHARED file, which is not this lane's to write — and it turns out the
    /// dependency buys nothing: the whole of what these three effects need is emission,
    /// a lifetime, gravity, a little Perlin wander and a fade at both ends, which is
    /// this file. What it costs instead of a module: no local-vs-world gravity
    /// ambiguity, no degrees-vs-radians trap, a `Step(dt)` the editor can drive by hand
    /// rather than `Simulate`, and a smaller player.
    ///
    /// ZERO ALLOCATION AFTER WARMUP. One array of particle structs, sized once to the
    /// ceiling; a dead particle is swapped with the last live one and the count drops,
    /// so nothing is ever allocated, freed or compacted while the effect runs. The
    /// random source is a seeded xorshift on the stack — deterministic, so the same
    /// effect films the same way twice.
    ///
    /// EVERYTHING IS IN THE HOST'S PIXELS. The sim knows nothing about transforms:
    /// positions, speeds and gravity are all in the drawing surface's own local
    /// coordinates, x right and y UP, which is what DrawnParticleView draws into.
    /// </summary>
    public sealed class DrawnParticleSim
    {
        internal struct P
        {
            public float x, y;
            public float vx, vy;
            public float age, life;
            public float size;
            public float rot, spin;      // degrees, degrees/second
            public Color32 col;          // rgb fixed at birth, alpha ramped every step
            public byte alpha0;
            public uint seed;            // stable for the particle's life: picks its cell
        }

        // ── the shape of the thing (set once, before Play) ─────────────────────

        /// Emit anywhere in this box, in local pixels: centre and size.
        public Vector2 BoxCentre;
        public Vector2 BoxSize;

        /// Or emit off a point in a fan: an arc in degrees, 0 = +x, counter-clockwise.
        public bool Radial;
        public Vector2 Origin;
        public float OriginRadius;
        public float ArcFrom, ArcTo;
        public float SpeedMin, SpeedMax;

        /// A constant drift added to every particle at birth.
        public Vector2 DriftMin, DriftMax;

        /// Downward is negative, like the surface it draws on. Pixels per second².
        public float Gravity;

        /// Lazy Perlin wander. Frequency is cycles per PIXEL, so 0.006 is a cell about
        /// 170px across — the difference between drifting and vibrating.
        public float NoiseStrength;
        public float NoiseFrequency = 0.006f;
        public float NoiseScroll = 0.15f;

        public float LifeMin = 1f, LifeMax = 2f;
        public float SizeMin = 4f, SizeMax = 8f;
        public float RotMin, RotMax;             // degrees
        public float SpinMin, SpinMax;           // degrees per second

        public Color ColorA = Color.white, ColorB = Color.white;
        public float AlphaMin = 1f, AlphaMax = 1f;

        /// Fractions of a life spent arriving and leaving. Nothing pops.
        public float FadeIn = 0.15f, FadeOut = 0.25f;

        /// A fed system emits `Rate` a second forever; a burst emits once and is done.
        public bool Loop = true;
        public float Rate = 4f;
        public int BurstMin = 6, BurstMax = 10;

        /// Seconds of simulation run at Play so the air is already inhabited on the
        /// first frame the screen is seen.
        public float Prewarm;

        // ── the state ──────────────────────────────────────────────────────────

        internal P[] Pool;
        internal int Count;

        uint _rng;
        float _emitAcc;
        float _time;
        bool _playing;

        public int Capacity { get { return Pool != null ? Pool.Length : 0; } }
        public int Live { get { return Count; } }
        public bool Feeding { get { return Loop && _playing; } }

        // one particle, readable from outside the assembly — the evidence pass needs
        // to say what it saw without the pool becoming public
        public float DebugX(int i) { return Pool[i].x; }
        public float DebugY(int i) { return Pool[i].y; }
        public float DebugSize(int i) { return Pool[i].size; }
        public int DebugAlpha(int i) { return Pool[i].col.a; }

        public DrawnParticleSim(int capacity, int seed)
        {
            Pool = new P[Mathf.Max(capacity, 1)];
            _rng = (uint)seed | 1u;          // xorshift never leaves zero
        }

        public void Play()
        {
            _playing = true;
            if (!Loop)
            {
                int n = Mathf.Clamp(RandInt(BurstMin, BurstMax), 0, Pool.Length);
                for (int i = 0; i < n; i++) Spawn();
                _playing = false;            // one burst, then it only falls
            }
            if (Prewarm > 0f)
            {
                int steps = Mathf.CeilToInt(Prewarm * 30f);
                for (int i = 0; i < steps; i++) Step(1f / 30f);
            }
        }

        /// Stop feeding; what is in the air still lives out its life and fades.
        public void Fade() { _playing = false; }

        public void Step(float dt)
        {
            if (dt <= 0f) return;
            if (dt > 0.1f) dt = 0.1f;        // a hitch must not teleport the air
            _time += dt;

            if (_playing && Loop && Rate > 0f)
            {
                _emitAcc += dt * Rate;
                while (_emitAcc >= 1f)
                {
                    _emitAcc -= 1f;
                    Spawn();
                }
            }

            float scroll = _time * NoiseScroll;
            for (int i = 0; i < Count; i++)
            {
                Pool[i].age += dt;
                if (Pool[i].age >= Pool[i].life)
                {
                    Count--;
                    if (i != Count) Pool[i] = Pool[Count];
                    i--;
                    continue;
                }

                if (Gravity != 0f) Pool[i].vy += Gravity * dt;
                Pool[i].x += Pool[i].vx * dt;
                Pool[i].y += Pool[i].vy * dt;

                if (NoiseStrength > 0f)
                {
                    float fx = Pool[i].x * NoiseFrequency;
                    float fy = Pool[i].y * NoiseFrequency;
                    float n1 = Mathf.PerlinNoise(fx + scroll, fy) - 0.5f;
                    float n2 = Mathf.PerlinNoise(fy - scroll, fx + 31.7f) - 0.5f;
                    Pool[i].x += n1 * NoiseStrength * dt * 2f;
                    Pool[i].y += n2 * NoiseStrength * dt * 2f;
                }

                Pool[i].rot += Pool[i].spin * dt;

                float k = Pool[i].age / Pool[i].life;
                float ramp = 1f;
                if (FadeIn > 0f && k < FadeIn) ramp = k / FadeIn;
                else if (FadeOut > 0f && k > 1f - FadeOut) ramp = (1f - k) / FadeOut;
                Pool[i].col.a = (byte)Mathf.Clamp(
                    Mathf.RoundToInt(Pool[i].alpha0 * ramp), 0, 255);
            }
        }

        // ── birth ──────────────────────────────────────────────────────────────

        void Spawn()
        {
            if (Count >= Pool.Length) return;
            int i = Count++;

            if (Radial)
            {
                float a = Rand(ArcFrom, ArcTo) * Mathf.Deg2Rad;
                float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                float r = Rand(0f, OriginRadius);
                float sp = Rand(SpeedMin, SpeedMax);
                Pool[i].x = Origin.x + cs * r;
                Pool[i].y = Origin.y + sn * r;
                Pool[i].vx = cs * sp;
                Pool[i].vy = sn * sp;
            }
            else
            {
                Pool[i].x = BoxCentre.x + Rand(-0.5f, 0.5f) * BoxSize.x;
                Pool[i].y = BoxCentre.y + Rand(-0.5f, 0.5f) * BoxSize.y;
                Pool[i].vx = 0f;
                Pool[i].vy = 0f;
            }
            Pool[i].vx += Rand(DriftMin.x, DriftMax.x);
            Pool[i].vy += Rand(DriftMin.y, DriftMax.y);

            Pool[i].age = 0f;
            Pool[i].life = Rand(LifeMin, LifeMax);
            Pool[i].size = Rand(SizeMin, SizeMax);
            Pool[i].rot = Rand(RotMin, RotMax);
            Pool[i].spin = Rand(SpinMin, SpinMax);

            Color c = Color.Lerp(ColorA, ColorB, Rand01());
            Pool[i].alpha0 = (byte)Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Clamp01(Rand(AlphaMin, AlphaMax)) * 255f), 0, 255);
            Pool[i].col = new Color32(
                (byte)Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f),
                FadeIn > 0f ? (byte)0 : Pool[i].alpha0);
            Pool[i].seed = NextRaw();
        }

        // ── the dice ───────────────────────────────────────────────────────────

        uint NextRaw()
        {
            _rng ^= _rng << 13;
            _rng ^= _rng >> 17;
            _rng ^= _rng << 5;
            return _rng;
        }

        float Rand01() { return (NextRaw() & 0xFFFFFFu) / 16777216f; }

        float Rand(float a, float b) { return a == b ? a : a + (b - a) * Rand01(); }

        int RandInt(int a, int b)
        {
            if (b <= a) return a;
            return a + (int)(NextRaw() % (uint)(b - a + 1));
        }
    }
}
