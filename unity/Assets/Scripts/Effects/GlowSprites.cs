using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;

namespace Runway.Effects
{
    /// Which lamp this is. The temperature is baked INTO the gradient rather than
    /// tinted on top of it, because real light changes colour as it falls off: a
    /// bulb is white at the filament and amber at the edge of its pool, and a
    /// laptop is white at the panel and blue where it dies out.
    public enum GlowTint { Warm, Cool }

    /// The two places in the game that own light. One entry point, one argument.
    public enum GlowScene
    {
        /// the room: bulb core, the pool it throws, the floor it lands on, the
        /// laptop on the founder's desk, and the cold multiply for a red week
        Garage,
        /// CHOOSE YOUR FOUNDER: the throat of the beam, the beam itself, and the
        /// spill where the drawn cone's pool meets the dark floor
        SelectStage,
    }

    /// <summary>
    /// ONE LIGHT, AS A HANDLE. A glow is a RawImage carrying a generated radial
    /// gradient, drawn additively over the room. This is the thing a caller holds:
    /// it can be moved, dimmed, made to follow an object, or killed.
    ///
    /// AN INERT GLOW IS A REAL GLOW THAT OWNS NOTHING. When the kill-switch is off
    /// every factory hands back `Glow.Inert`, whose methods all do nothing, so a
    /// caller never has to null-check and no GameObject is ever created.
    /// </summary>
    public sealed class Glow
    {
        public static readonly Glow Inert = new Glow();

        internal RawImage Img;
        internal RectTransform Rt;
        internal float Base = 1f;          // the authored intensity
        internal float RedScale = 1f;      // what a red week leaves of it
        internal bool Breathe;
        internal float Phase;
        internal RectTransform FollowRt;   // the object this light belongs to
        internal Graphic FollowGraphic;
        internal Vector2 FollowOffset;
        internal float LastAlpha = -1f;

        public bool Live { get { return Img != null; } }
        public RectTransform Rect { get { return Rt; } }

        /// Re-aim the light. Centre, in the parent's Godot top-left coordinates.
        public void MoveTo(float centreX, float centreY)
        {
            if (Rt == null) return;
            Rt.anchoredPosition = new Vector2(centreX, -centreY);
        }

        public void Resize(float w, float h)
        {
            if (Rt == null) return;
            Rt.sizeDelta = new Vector2(w, h);
        }

        /// The authored intensity. What a red week does to it is applied on top.
        public void SetIntensity(float alpha)
        {
            if (Img == null) return;       // the inert one stays inert
            Base = Mathf.Max(0f, alpha);
            Img.color = new Color(1f, 1f, 1f, Base);
            LastAlpha = Base;
        }

        public void SetVisible(bool on)
        {
            if (Rt != null && Rt.gameObject.activeSelf != on) Rt.gameObject.SetActive(on);
        }

        /// THE LIGHT BELONGS TO THE OBJECT, NOT TO A COORDINATE. A followed glow
        /// tracks its object's centre and its visibility, so the laptop glow is on
        /// exactly when the founder owns a laptop, moves when the drawing lands and
        /// resizes itself into place, and goes out under a composed painting — with
        /// no hookup and no second copy of the room's layout table.
        public void FollowObject(RectTransform target, float dx = 0f, float dy = 0f)
        {
            if (Img == null) return;       // the inert one stays inert
            FollowRt = target;
            FollowGraphic = target != null ? target.GetComponent<Graphic>() : null;
            FollowOffset = new Vector2(dx, dy);
        }

        public void Kill()
        {
            if (Rt == null) return;
            GlowSprites.Gone(Rt.gameObject);
            Rt = null;
            Img = null;
        }
    }

    /// <summary>
    /// SOFT LIGHT, THE WAY THIS GAME DRAWS IT — D6.
    ///
    /// NOT a render-pipeline light. The game is one hand-drawn painting per screen
    /// and a URP migration would buy 2D lights at the price of every drawn material
    /// in the port; the checklist settles it (DEFERRED: URP migration — soft-light
    /// sprites chosen instead). So light here is what an illustrator does: a soft
    /// radial wash laid over the picture in ADD, plus a cold multiply over the whole
    /// room when the money runs out.
    ///
    /// WHAT IS GENERATED, AND WHY IT IS GENERATED. Two 256² radial gradients are
    /// rasterised at runtime, one warm and one cool. Nothing ships as a PNG because
    /// a gradient stretched from 300px to 1220px must stay smooth at both ends, and
    /// because the radius is WOBBLED by three low harmonics with a fixed seed — a
    /// pool of light with a perfectly circular edge is the one thing on this screen
    /// that would not look drawn.
    ///
    /// THE CLOCK IS THE ROOM'S CLOCK. Breathing is quantised to the same 12fps the
    /// garage breathes its journal button at, and rides `localScale` rather than
    /// colour, so a breathing light costs a transform write and never dirties the
    /// canvas mesh. Colour is written only while the red state is actually easing.
    ///
    /// KILL-SWITCH: environment `RUNWAY_FX_GLOWS`. Absent or "1" — on. "0" (also
    /// "off"/"false") — every factory returns an inert handle, no GameObject is
    /// created, no texture is baked, no component ticks.
    /// </summary>
    public sealed class GlowSprites : MonoBehaviour
    {
        // ── the dials ──────────────────────────────────────────────────────────
        public const string SwitchKey = "RUNWAY_FX_GLOWS";
        /// everything drawn in this game moves on twelves
        public const float BreathFps = 12f;
        public const float BreathSecs = 4f;
        public const float BreathAmount = 0.03f;     // ±3%
        public const float RedEaseSecs = 0.9f;
        public const int TextureSize = 256;

        /// D6b — the room multiplies toward 0.85 and goes cold. Mean channel 0.843,
        /// but the channels are pulled APART: blue is left almost alone (0.98) while
        /// red loses 28%, so the room does not just dim, it drains. A flat 0.85 on
        /// all three would read as "someone turned a dimmer down"; this reads as
        /// morning in a room nobody paid the heating on.
        public static readonly Color RedMultiply = new Color(0.72f, 0.83f, 0.98f, 1f);
        /// what the same dim looks like when no multiply material could be built
        public static readonly Color RedVeil = new Color(0.09f, 0.14f, 0.24f, 1f);
        public const float RedVeilAlpha = 0.19f;

        static readonly List<GlowSprites> Rigs = new List<GlowSprites>();
        static bool _redWanted;
        static bool? _enabled;

        // ── the switch ─────────────────────────────────────────────────────────

        /// Absent or "1" is on; "0", "off" and "false" are off. Read once.
        public static bool Enabled
        {
            get
            {
                if (_enabled.HasValue) return _enabled.Value;
                string v = "1";
                try { v = Env.Get(SwitchKey, "1").Trim(); }
                catch (Exception) { v = "1"; }
                _enabled = !(v == "0"
                             || string.Equals(v, "off", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(v, "false", StringComparison.OrdinalIgnoreCase));
                return _enabled.Value;
            }
        }

        /// For a harness that flips the environment after this class has answered once.
        public static void ForgetSwitch() { _enabled = null; }

        // ══ THE ONE ENTRY POINT ════════════════════════════════════════════════

        /// Light a screen. `host` is the rect the light belongs INSIDE — the garage's
        /// room rect, or the draft screen's own rect (so the beam sits over the stage
        /// art and under every page). Idempotent: a second call on the same host
        /// returns the rig already installed. Returns null when the switch is off.
        public static GlowSprites Apply(RectTransform host, GlowScene scene)
        {
            if (!Enabled || host == null) return null;
            var rig = host.gameObject.GetComponent<GlowSprites>();
            if (rig != null) return rig;
            rig = host.gameObject.AddComponent<GlowSprites>();
            rig.Install(host, scene);
            return rig;
        }

        /// THE RED STATE, from anywhere. The room's own loop calls this with
        /// `State.Cash < 0`; every installed rig eases to it over 0.9s. Cheap enough
        /// to call every frame — a repeat of the current answer does nothing.
        public static void SetRed(bool on)
        {
            _redWanted = on;
            for (int i = Rigs.Count - 1; i >= 0; i--)
                if (Rigs[i] == null) Rigs.RemoveAt(i);
        }

        public static bool RedOn { get { return _redWanted; } }

        /// Drive every rig by hand — the editor evidence harness, where there is no
        /// game loop to call Update for us.
        public static void StepAll(float dt)
        {
            for (int i = Rigs.Count - 1; i >= 0; i--)
            {
                if (Rigs[i] == null) { Rigs.RemoveAt(i); continue; }
                Rigs[i].Step(dt);
            }
        }

        // ══ the factory ════════════════════════════════════════════════════════

        /// ONE LIGHT. `centre` is the middle of the pool in the parent's Godot
        /// top-left coordinates, `size` the diameter it covers, `alpha` how hard it
        /// burns (0.10 is a wash, 0.45 is a filament). `breathe` gives it the slow
        /// ±3%/4s swell every bulb in this game has.
        public static Glow MakeGlow(RectTransform parent, Vector2 centre, float size,
                                    GlowTint tint, float alpha, bool breathe = false)
        {
            return MakeGlow(parent, centre, new Vector2(size, size), tint, alpha, breathe);
        }

        /// The same light, stretched — a beam is tall, a pool on a floor is wide.
        public static Glow MakeGlow(RectTransform parent, Vector2 centre, Vector2 size,
                                    GlowTint tint, float alpha, bool breathe = false)
        {
            if (!Enabled || parent == null) return Glow.Inert;

            var rt = DrawnUI.Rect(parent, tint == GlowTint.Warm ? "glow_warm" : "glow_cool",
                                  centre.x - size.x * 0.5f, centre.y - size.y * 0.5f,
                                  size.x, size.y);
            // it breathes about its own middle, never about a corner
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(centre.x, -centre.y);

            var img = rt.gameObject.AddComponent<RawImage>();
            img.texture = Sheet(tint);
            img.raycastTarget = false;
            Material m = AdditiveMaterial();
            if (m != null) img.material = m;
            img.color = new Color(1f, 1f, 1f, Mathf.Max(0f, alpha));

            var g = new Glow { Img = img, Rt = rt, Base = Mathf.Max(0f, alpha), Breathe = breathe };
            // a red week drains the warm light and leaves the cold one: the bulb is
            // the company's, the laptop screen does not care about payroll
            g.RedScale = tint == GlowTint.Warm ? 0.45f : 1f;
            g.LastAlpha = g.Base;

            // whichever rig this light was hung inside is the one that ticks it —
            // inactive included, because a page builds itself before it is shown
            var rig = parent.GetComponentInParent<GlowSprites>(true);
            if (rig != null && !rig._glows.Contains(g)) rig._glows.Add(g);
            return g;
        }

        /// THE COLD MULTIPLY. A full-rect layer over everything already drawn inside
        /// `parent` — the room, its objects, its crew, its painting AND its own
        /// glows, so a red week drains the light as well as the colour.
        public static Image MakeRedOverlay(RectTransform parent)
        {
            if (!Enabled || parent == null) return null;
            var img = DrawnUI.FullFill(parent, "glow_red", Color.white);
            Material m = MultiplyMaterial();
            if (m != null) img.material = m;
            else img.color = new Color(RedVeil.r, RedVeil.g, RedVeil.b, 0f);
            img.gameObject.SetActive(false);
            return img;
        }

        // ══ the rig ════════════════════════════════════════════════════════════

        readonly List<Glow> _glows = new List<Glow>();
        RectTransform _host;
        RectTransform _layer;
        Image _red;
        bool _redIsMultiply;
        bool _pinLast;
        float _t;
        float _lastBeat = -1f;
        float _redK;

        public GlowScene Scene { get; private set; }
        public int Count { get { return _glows.Count; } }
        /// 0 = the room is itself, 1 = fully in the red
        public float RedAmount { get { return _redK; } }

        void OnEnable() { if (!Rigs.Contains(this)) Rigs.Add(this); }

        void Install(RectTransform host, GlowScene scene)
        {
            // OnEnable does not run in the editor, and the evidence harness lives
            // there — a rig registers itself the moment it is built either way
            if (!Rigs.Contains(this)) Rigs.Add(this);
            _host = host;
            Scene = scene;
            _layer = DrawnUI.FullRect(host, "glow_layer");
            if (scene == GlowScene.Garage)
            {
                BuildGarage();
                _red = MakeRedOverlay(host);
                // Graphic.material never reads back null (it answers with the default
                // UI material), so the question has to be asked of the material itself
                _redIsMultiply = _red != null && MultiplyMaterial() != null;
                // the room rebuilds its crew every week, and a rebuilt crew is
                // appended AFTER us — the light has to climb back on top
                _pinLast = true;
            }
            else
            {
                BuildSelectStage();
                // the beam belongs UNDER the pages: whatever sibling index the
                // caller installed us at is the one that is correct
                _pinLast = false;
            }
            if (_redWanted) { _redK = 1f; ApplyRed(); Alphas(); }
            Beat(0f);
        }

        /// THE GARAGE, LIT. A bare bulb over the bench: the filament itself, the pool
        /// it throws down the room, the light landing on the floor, and the cold
        /// panel of the laptop on the founder's desk — the one light in the room that
        /// does not care whether the company can make payroll.
        void BuildGarage()
        {
            // VD5 (live play): the drawn room has NO pendant lamp — a bulb
            // core and wall pools with no source read as smears on the cream,
            // not light. Only lit things may glow: a whisper of warmth on the
            // floor under the crew, the laptop's cold panel, and the red law.
            // A composed painting carries its own light inside the picture.
            MakeGlow(_layer, new Vector2(742f, 815f), new Vector2(1100f, 300f),
                     GlowTint.Warm, 0.05f, true);

            var laptop = MakeGlow(_layer, new Vector2(478f, 586f), new Vector2(300f, 230f),
                                  GlowTint.Cool, 0.22f);
            // the room names its own object spots; the light asks the room where the
            // laptop is rather than keeping a second copy of the table
            Transform t = _host != null ? _host.Find("item_itm_laptop") : null;
            if (t != null) laptop.FollowObject(t as RectTransform, 0f, -14f);
        }

        /// CHOOSE YOUR FOUNDER, LIT. env/stage.png draws the cone with a hard ink
        /// edge: an apex at the top centre, a pool whose ellipse spans roughly
        /// x 370→1200 with its middle near y 878. The glow is matched to THAT — the
        /// throat where the lamp is, a haze down the cone, and a pool wide enough to
        /// spill past the drawn ellipse onto the dark floor beside it, which is the
        /// part a drawn cone can never have.
        void BuildSelectStage()
        {
            MakeGlow(_layer, new Vector2(768f, 40f), new Vector2(420f, 300f),
                     GlowTint.Warm, 0.16f);
            MakeGlow(_layer, new Vector2(768f, 430f), new Vector2(860f, 980f),
                     GlowTint.Warm, 0.10f);
            // wider than the drawn ellipse on purpose: the spill onto the dark floor
            // beside the pool is the part a hard ink edge can never have
            MakeGlow(_layer, new Vector2(785f, 878f), new Vector2(1260f, 380f),
                     GlowTint.Warm, 0.18f);
        }

        void Update()
        {
            Step(Time.unscaledDeltaTime);
        }

        /// One tick. Public so the editor harness can settle a state without a game
        /// loop; the game itself never calls it.
        public void Step(float dt)
        {
            _t += dt;

            float target = _redWanted ? 1f : 0f;
            if (!Mathf.Approximately(_redK, target))
            {
                _redK = Mathf.MoveTowards(_redK, target, dt / Mathf.Max(RedEaseSecs, 0.01f));
                ApplyRed();
                Alphas();
            }

            float q = Mathf.Floor(_t * BreathFps) / BreathFps;
            if (!Mathf.Approximately(q, _lastBeat))
            {
                _lastBeat = q;
                Beat(q);
            }
            Pin();
        }

        /// The room's own 12fps beat: the swell, and where the followed lights are.
        void Beat(float q)
        {
            float swell = 1f + Mathf.Sin(q * Mathf.PI * 2f / BreathSecs) * BreathAmount;
            for (int i = _glows.Count - 1; i >= 0; i--)
            {
                Glow g = _glows[i];
                if (g == null || g.Rt == null) { _glows.RemoveAt(i); continue; }
                if (g.Breathe)
                {
                    float s = g.Phase == 0f
                        ? swell
                        : 1f + Mathf.Sin((q + g.Phase) * Mathf.PI * 2f / BreathSecs) * BreathAmount;
                    g.Rt.localScale = new Vector3(s, s, 1f);
                }
                if (g.FollowRt != null) TrackObject(g);
            }
        }

        /// A followed light sits on its object's middle and shares its visibility.
        static void TrackObject(Glow g)
        {
            RectTransform t = g.FollowRt;
            if (t == null) return;
            bool on = t.gameObject.activeInHierarchy
                      && (g.FollowGraphic == null || g.FollowGraphic.enabled);
            g.SetVisible(on);
            if (!on) return;
            Vector2 ap = t.anchoredPosition;
            Vector2 size = t.rect.size;
            // both rects hang off the same top-left anchors, so the object's middle
            // is its corner plus half its size — down is negative in Unity's y
            g.Rt.anchoredPosition = new Vector2(ap.x + size.x * 0.5f + g.FollowOffset.x,
                                                ap.y - size.y * 0.5f - g.FollowOffset.y);
        }

        /// Warm light drains in the red; the laptop does not. Written only while the
        /// state is moving, so a steady room never touches a vertex colour.
        void Alphas()
        {
            for (int i = _glows.Count - 1; i >= 0; i--)
            {
                Glow g = _glows[i];
                if (g == null || g.Img == null) { _glows.RemoveAt(i); continue; }
                float a = g.Base * Mathf.Lerp(1f, g.RedScale, _redK);
                if (Mathf.Abs(a - g.LastAlpha) < 0.002f) continue;
                g.LastAlpha = a;
                g.Img.color = new Color(1f, 1f, 1f, a);
            }
        }

        void ApplyRed()
        {
            if (_red == null) return;
            bool on = _redK > 0.001f;
            if (_red.gameObject.activeSelf != on) _red.gameObject.SetActive(on);
            if (!on) return;
            _red.color = _redIsMultiply
                ? Color.Lerp(Color.white, RedMultiply, _redK)
                : new Color(RedVeil.r, RedVeil.g, RedVeil.b, RedVeilAlpha * _redK);
        }

        /// Light lies ON the room. Anything the room adds later (a crew rebuilt every
        /// week) lands above us until we climb back — checked as two integers, moved
        /// only on the frame it is actually wrong.
        void Pin()
        {
            if (!_pinLast || _host == null) return;
            int n = _host.childCount;
            if (_layer != null)
            {
                int want = _red != null ? n - 2 : n - 1;
                if (_layer.GetSiblingIndex() != want) _layer.SetAsLastSibling();
            }
            if (_red != null && _red.transform.GetSiblingIndex() != n - 1)
                _red.transform.SetAsLastSibling();
        }

        void OnDestroy()
        {
            Rigs.Remove(this);
            _glows.Clear();
        }

        // ══ the gradients ══════════════════════════════════════════════════════

        static Texture2D _warm;
        static Texture2D _cool;

        /// The generated radial sheet for a temperature. Baked once per session.
        public static Texture2D Sheet(GlowTint tint)
        {
            // SATURATION, NOT BRIGHTNESS. Light that adds is light that clips: a
            // near-white gradient over a cream wall goes to paper-white and the
            // drawing under it disappears. Both sheets run hot but COLOURED, so the
            // room gains temperature long before it gains luminance.
            if (tint == GlowTint.Cool)
            {
                if (_cool == null)
                    _cool = Bake(new Color(0.90f, 0.97f, 1f), new Color(0.32f, 0.60f, 1f), 91);
                return _cool;
            }
            if (_warm == null)
                _warm = Bake(new Color(1f, 0.90f, 0.71f), new Color(1f, 0.50f, 0.14f), 47);
            return _warm;
        }

        /// A radial gradient with a HAND on it: the radius is wobbled by three low
        /// harmonics and the falloff carries a little grain, so the pool's edge is
        /// drawn rather than computed. Colour runs hot-white at the filament to the
        /// rim colour where it dies; alpha is the falloff itself, which is what the
        /// additive blend multiplies by.
        static Texture2D Bake(Color core, Color rim, int seed)
        {
            int n = TextureSize;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.name = "runway_glow_" + seed;
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            var rng = new System.Random(seed);
            float p1 = (float)rng.NextDouble() * Mathf.PI * 2f;
            float p2 = (float)rng.NextDouble() * Mathf.PI * 2f;
            float p3 = (float)rng.NextDouble() * Mathf.PI * 2f;

            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                float dy = (y + 0.5f) / n * 2f - 1f;
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float th = Mathf.Atan2(dy, dx);
                    float wob = 1f + 0.055f * Mathf.Sin(th * 3f + p1)
                                   + 0.035f * Mathf.Sin(th * 5f + p2)
                                   + 0.020f * Mathf.Sin(th * 7f + p3);
                    float k = Mathf.Clamp01(1f - d / Mathf.Max(wob, 0.4f));
                    float a = k * k * (3f - 2f * k);                 // smoothstep
                    // ^1.7 rather than ^2.5: a bulb fills a room, it does not paint a
                    // disc on the wall — the softer knee is what carries the pool out
                    // to the crew and the floor instead of dying a foot from the
                    // filament
                    a = Mathf.Pow(a, 1.7f);
                    a *= 0.97f + 0.06f * (float)rng.NextDouble();    // the paper it lands on
                    a = Mathf.Clamp01(a);
                    Color c = Color.Lerp(rim, core, a);
                    px[y * n + x] = new Color32((byte)Mathf.RoundToInt(c.r * 255f),
                                                (byte)Mathf.RoundToInt(c.g * 255f),
                                                (byte)Mathf.RoundToInt(c.b * 255f),
                                                (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        // ══ the materials ══════════════════════════════════════════════════════

        static Material _add;
        static Material _mul;
        static bool _shaderTried;
        static Shader _shader;

        /// UI/Default cannot add: its blend is hard-coded and it has no _SrcBlend to
        /// set. Resources/Shaders/RunwayGlow is the same shader with the two factors
        /// exposed. If it cannot be found at all, a null material means the default
        /// UI material and a straight alpha blend — the light then washes toward its
        /// own colour instead of adding to what is under it, which over a dark room
        /// still reads as light. Degraded, never broken.
        static Shader GlowShader
        {
            get
            {
                if (_shaderTried) return _shader;
                _shaderTried = true;
                try
                {
                    _shader = Resources.Load<Shader>("Shaders/RunwayGlow");
                    if (_shader == null) _shader = Shader.Find("Runway/Glow");
                }
                catch (Exception) { _shader = null; }
                if (_shader == null)
                    Debug.LogWarning("RUNWAY! glow shader missing — soft light falls back "
                                     + "to alpha blending (Resources/Shaders/RunwayGlow).");
                return _shader;
            }
        }

        public static Material AdditiveMaterial()
        {
            if (_add != null) return _add;
            _add = Build(UnityEngine.Rendering.BlendMode.SrcAlpha,
                         UnityEngine.Rendering.BlendMode.One, "runway_glow_add");
            return _add;
        }

        public static Material MultiplyMaterial()
        {
            if (_mul != null) return _mul;
            _mul = Build(UnityEngine.Rendering.BlendMode.DstColor,
                         UnityEngine.Rendering.BlendMode.Zero, "runway_glow_multiply");
            return _mul;
        }

        static Material Build(UnityEngine.Rendering.BlendMode src,
                              UnityEngine.Rendering.BlendMode dst, string name)
        {
            Shader sh = GlowShader;
            if (sh == null) return null;
            try
            {
                var m = new Material(sh);
                m.name = name;
                m.hideFlags = HideFlags.HideAndDontSave;
                m.SetFloat("_SrcBlend", (float)src);
                m.SetFloat("_DstBlend", (float)dst);
                return m;
            }
            catch (Exception e)
            {
                Debug.LogWarning("RUNWAY! could not build the glow material (" + e.Message + ")");
                return null;
            }
        }

        /// Destroy that is legal in both modes — the evidence harness runs in the
        /// editor, where Destroy is deferred to a frame that never comes.
        internal static void Gone(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }
}
