using System;
using UnityEngine;
using Runway.App;

namespace Runway.Effects
{
    /// <summary>
    /// THE AIR, DRAWN. Three particle systems share one hand and one 4-cell sheet:
    /// a soft dust dot, a hand-cut paper scrap with an ink edge, a warm ember, and a
    /// wide out-of-focus blur. All four are RASTERISED AT RUNTIME by the same
    /// alpha-max/over compositor DrawnUI bakes its wobbled edges with, so a mote and
    /// a card border come out of the same pen. Nothing here is a prefab, an imported
    /// sprite or a material on disk.
    ///
    /// TWO HALVES, NEITHER OF THEM A RENDERER. <see cref="DrawnParticleSim"/> moves
    /// the specks; <see cref="DrawnParticleView"/> draws them as UI geometry. Boot's
    /// canvas is ScreenSpaceOverlay, which paints AFTER every camera, so anything a
    /// Renderer draws sits behind the whole game — drawn into the canvas instead, the
    /// effect sits in the drawn stack like any other card: sibling order, screen fades
    /// and masks all apply, at one draw call.
    ///
    /// THE KILL-SWITCH is the runtime environment, not a scripting define, so the
    /// effect can be turned off for a shot without a recompile:
    ///   RUNWAY_FX_PARTICLES absent or "1"  ->  on
    ///   RUNWAY_FX_PARTICLES "0"            ->  every entry point returns null and
    ///                                          builds nothing at all.
    /// </summary>
    public static class ParticleInk
    {
        public const string KillSwitch = "RUNWAY_FX_PARTICLES";

        /// Read live on every entry point — it is called once per screen build, never
        /// per frame, so there is nothing to cache and nothing to reset for a test.
        public static bool On
        {
            get { return Env.Get(KillSwitch, "1").Trim() != "0"; }
        }

        // ── the sheet ──────────────────────────────────────────────────────────

        public const int CellPx = 64;
        public const int SheetSide = 128;

        static Texture2D _sheet;

        /// 128x128 RGBA32, four 64px cells, baked once and held for the session.
        public static Texture2D Sheet
        {
            get
            {
                if (_sheet == null) _sheet = BakeSheet();
                return _sheet;
            }
        }

        /// UV window of a cell, inset one texel so bilinear sampling can never drag
        /// a neighbour's ink into the quad. (uMin, vMin, uMax, vMax).
        static Vector4 Uv(int col, int row)
        {
            const float t = 1f / SheetSide;
            float u0 = col * 0.5f + t;
            float u1 = (col + 1) * 0.5f - t;
            float v0 = 1f - (row + 1) * 0.5f + t;
            float v1 = 1f - row * 0.5f - t;
            return new Vector4(u0, v0, u1, v1);
        }

        public static readonly Vector4 UvDot = Uv(0, 0);
        public static readonly Vector4 UvScrap = Uv(1, 0);
        public static readonly Vector4 UvEmber = Uv(0, 1);
        public static readonly Vector4 UvBlur = Uv(1, 1);

        // ── mounting a system ──────────────────────────────────────────────────

        /// Each effect gets its own nested canvas so its per-frame vertex rebuild
        /// never dirties the screen's canvas — the difference between 0.02ms and a
        /// rebuild storm over a screen full of paper. Set false before an entry point
        /// if a host ever needs the effect inside its own batch instead.
        public static bool NestedCanvas = true;

        /// Build the pair: the graphic that draws, and the pool that feeds it.
        ///
        /// The graphic's local space IS the sim's space: a particle at (0, 0) sits at
        /// the centre of the host rect, x right and y UP, so there is no mapping
        /// anywhere between the two halves.
        internal static DrawnParticleView Mount(RectTransform parent, string name,
                                                int capacity, int seed,
                                                Vector4 cellA, Vector4 cellB,
                                                uint secondaryMask,
                                                out DrawnParticleSim sim)
        {
            sim = null;
            if (parent == null) return null;

            var rt = DrawnUI.FullRect(parent, name);
            if (NestedCanvas) rt.gameObject.AddComponent<Canvas>();
            // THE CANVAS RENDERER IS ADDED BY HAND. Graphic carries
            // [RequireComponent(typeof(CanvasRenderer))], but a component added to a
            // bare GameObject from code does not always get it — and a Graphic whose
            // canvasRenderer is null fails SILENTLY: Graphic.Rebuild returns before
            // OnPopulateMesh, so the system simulates perfectly and draws nothing.
            if (rt.GetComponent<CanvasRenderer>() == null)
                rt.gameObject.AddComponent<CanvasRenderer>();
            var view = rt.gameObject.AddComponent<DrawnParticleView>();

            sim = new DrawnParticleSim(capacity, seed);
            view.Bind(sim, Sheet, cellA, cellB, secondaryMask);
            return view;
        }

        /// A point written the way every .gd coordinate in this port is written —
        /// x right, y DOWN from the top-left of the host rect — turned into the
        /// graphic's own centre-origin local space.
        internal static Vector2 ToLocal(RectTransform rt, float x, float y)
        {
            float w = rt.rect.width, h = rt.rect.height;
            if (w < 2f) w = RunwayPaths.StageWidth;
            if (h < 2f) h = RunwayPaths.StageHeight;
            return new Vector2(x - w * 0.5f, h * 0.5f - y);
        }

        // ══ the mini-rasteriser, in the DrawnUI hand ═══════════════════════════

        static Texture2D BakeSheet()
        {
            const int W = SheetSide;
            const int H = SheetSide;
            var px = new Color32[W * H];
            var clear = new Color32(255, 255, 255, 0);
            for (int i = 0; i < px.Length; i++) px[i] = clear;

            DrawDot(px, 0, 0);
            DrawScrap(px, CellPx, 0);
            DrawEmber(px, 0, CellPx);
            DrawBlur(px, CellPx, CellPx);

            // the canvas is built top-left down like every .gd file reads; Unity
            // textures start bottom-left, so the rows flip exactly once, here
            var flipped = new Color32[px.Length];
            for (int y = 0; y < H; y++)
                Array.Copy(px, y * W, flipped, (H - 1 - y) * W, W);

            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.name = "runway/particle-ink";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.SetPixels32(flipped);
            tex.Apply(false, false);
            return tex;
        }

        /// DUST — a speck with a firm middle and a halo that gives up slowly, so forty
        /// of them read as air and not as forty circles. The middle is what carries at
        /// a 12px quad; without it a mote is a pinprick nobody can see.
        static void DrawDot(Color32[] px, int ox, int oy)
        {
            float cx = ox + 32f, cy = oy + 32f;
            const float core = 4.5f;
            const float halo = 30f;
            for (int y = oy; y < oy + CellPx; y++)
            {
                for (int x = ox; x < ox + CellPx; x++)
                {
                    float d = Dist(x + 0.5f, y + 0.5f, cx, cy);
                    if (d >= halo) continue;
                    float a = d <= core ? 1f
                        : Mathf.Pow(1f - (d - core) / (halo - core), 1.7f);
                    Max(px, x, y, 255, 255, 255, a);
                }
            }
        }

        /// A HAND-CUT SCRAP — a jittered cream quad, a wobbled ink edge round it and
        /// one line of writing on it, because a scrap torn off the page has words.
        /// This cell carries its OWN colour (cream body, ink edge); the others are
        /// white and take their colour from the particle.
        static void DrawScrap(Color32[] px, int ox, int oy)
        {
            Color32 ink = C32(DrawnUI.Ink);
            Color32 cream = C32(DrawnUI.Cream);
            // the fringe outside the paper darkens toward ink, never toward white
            for (int y = oy; y < oy + CellPx; y++)
                for (int x = ox; x < ox + CellPx; x++)
                    px[y * SheetSide + x] = new Color32(ink.r, ink.g, ink.b, 0);

            // THE EDGE IS WALKED, NOT DRAWN — three jittered samples along every side,
            // the way DrawnUI.WobbleRectSprite walks a card's border, so no two sides
            // of the paper are straight and no corner is square.
            var rng = new System.Random(51);
            const float hw = 27f, hh = 19f;
            var corner = new[]
            {
                new Vector2(ox + 32f - hw, oy + 32f - hh),
                new Vector2(ox + 32f + hw, oy + 32f - hh),
                new Vector2(ox + 32f + hw, oy + 32f + hh),
                new Vector2(ox + 32f - hw, oy + 32f + hh),
            };
            var edge = new Vector2[12];
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = corner[i], b = corner[(i + 1) % 4];
                for (int k = 0; k < 3; k++)
                {
                    Vector2 p = Vector2.Lerp(a, b, k / 3f);
                    edge[i * 3 + k] = new Vector2(p.x + Jit(rng, 2.3f), p.y + Jit(rng, 2.3f));
                }
            }

            FillPoly(px, ox, oy, edge, cream, 1f);
            StrokeLoop(px, ox, oy, edge, 2.2f, ink, 1f);

            // it is a scrap OF THE PAGE, so it has the page's writing on it
            for (int line = 0; line < 2; line++)
            {
                var written = new Vector2[7];
                for (int i = 0; i < written.Length; i++)
                    written[i] = new Vector2(ox + 12f + i * 6.6f,
                                             oy + 26f + line * 12f + Jit(rng, 1.1f));
                StrokeOpen(px, ox, oy, written, 1.5f, ink, 0.5f);
            }
        }

        /// AN EMBER — a hot core that holds its shape at 6px and a glow that gives up
        /// slowly. Alpha only; the coral/yellow arrives as the particle's colour.
        static void DrawEmber(Color32[] px, int ox, int oy)
        {
            float cx = ox + 32f, cy = oy + 32f;
            const float core = 10f;
            const float halo = 30f;
            for (int y = oy; y < oy + CellPx; y++)
            {
                for (int x = ox; x < ox + CellPx; x++)
                {
                    float d = Dist(x + 0.5f, y + 0.5f, cx, cy);
                    if (d >= halo) continue;
                    float a = d <= core ? 1f
                        : Mathf.Pow(1f - (d - core) / (halo - core), 1.8f);
                    Max(px, x, y, 255, 255, 255, a);
                }
            }
        }

        /// OUT OF FOCUS — the same speck seen past the plane of focus. One mote in
        /// eight and one ember in four wear it, which is the whole of the depth.
        static void DrawBlur(Color32[] px, int ox, int oy)
        {
            float cx = ox + 32f, cy = oy + 32f, r = 30f;
            for (int y = oy; y < oy + CellPx; y++)
            {
                for (int x = ox; x < ox + CellPx; x++)
                {
                    float d = Dist(x + 0.5f, y + 0.5f, cx, cy);
                    if (d >= r) continue;
                    Max(px, x, y, 255, 255, 255, 0.55f * Mathf.Pow(1f - d / r, 2.4f));
                }
            }
        }

        // ── the compositor ─────────────────────────────────────────────────────

        static float Dist(float x, float y, float cx, float cy)
        {
            float dx = x - cx, dy = y - cy;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        static float Jit(System.Random rng, float amount)
        {
            return (float)(rng.NextDouble() * 2.0 - 1.0) * amount;
        }

        static Color32 C32(Color c)
        {
            return new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f),
                               (byte)Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f),
                               (byte)Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f), 255);
        }

        /// Alpha-MAX, the way DrawnUI stamps ink: a stroke crossing itself does not
        /// darken into a blot.
        static void Max(Color32[] px, int x, int y, byte r, byte g, byte b, float a)
        {
            if (a <= 0f) return;
            int want = Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255);
            int i = y * SheetSide + x;
            if (px[i].a >= want) return;
            px[i] = new Color32(r, g, b, (byte)want);
        }

        /// Alpha-OVER, for the one cell that has two colours in it.
        static void Over(Color32[] px, int x, int y, byte r, byte g, byte b, float a)
        {
            if (a <= 0f) return;
            if (a > 1f) a = 1f;
            int i = y * SheetSide + x;
            Color32 d = px[i];
            int na = Mathf.RoundToInt(a * 255f);
            px[i] = new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(d.r + (r - d.r) * a), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(d.g + (g - d.g) * a), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(d.b + (b - d.b) * a), 0, 255),
                (byte)Mathf.Max(d.a, na));
        }

        static void Stamp(Color32[] px, int ox, int oy, float cx, float cy, float r,
                          Color32 col, float alphaMul)
        {
            int x0 = Mathf.Max(Mathf.FloorToInt(cx - r - 1f), ox);
            int x1 = Mathf.Min(Mathf.CeilToInt(cx + r + 1f), ox + CellPx - 1);
            int y0 = Mathf.Max(Mathf.FloorToInt(cy - r - 1f), oy);
            int y1 = Mathf.Min(Mathf.CeilToInt(cy + r + 1f), oy + CellPx - 1);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float a = Mathf.Clamp01(r - Dist(x + 0.5f, y + 0.5f, cx, cy) + 0.5f);
                    if (a <= 0f) continue;
                    Over(px, x, y, col.r, col.g, col.b, a * alphaMul);
                }
            }
        }

        static void StrokeOpen(Color32[] px, int ox, int oy, Vector2[] pts,
                               float thickness, Color32 col, float alphaMul)
        {
            float r = Mathf.Max(thickness * 0.5f, 0.5f);
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1];
                int steps = Mathf.Max(Mathf.CeilToInt(Vector2.Distance(a, b) * 2f), 1);
                for (int s = 0; s <= steps; s++)
                {
                    Vector2 p = Vector2.Lerp(a, b, (float)s / steps);
                    Stamp(px, ox, oy, p.x, p.y, r, col, alphaMul);
                }
            }
        }

        static void StrokeLoop(Color32[] px, int ox, int oy, Vector2[] pts,
                               float thickness, Color32 col, float alphaMul)
        {
            var closed = new Vector2[pts.Length + 1];
            Array.Copy(pts, closed, pts.Length);
            closed[pts.Length] = pts[0];
            StrokeOpen(px, ox, oy, closed, thickness, col, alphaMul);
        }

        /// Even-odd fill with a 2x2 sample per pixel — enough anti-aliasing that a
        /// 24px scrap does not read as a staircase when it tumbles.
        static void FillPoly(Color32[] px, int ox, int oy, Vector2[] poly,
                             Color32 col, float alphaMul)
        {
            for (int y = oy; y < oy + CellPx; y++)
            {
                for (int x = ox; x < ox + CellPx; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++)
                            if (Inside(poly, x + 0.25f + sx * 0.5f, y + 0.25f + sy * 0.5f))
                                hits++;
                    if (hits == 0) continue;
                    Over(px, x, y, col.r, col.g, col.b, hits * 0.25f * alphaMul);
                }
            }
        }

        static bool Inside(Vector2[] p, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            {
                if ((p[i].y > y) == (p[j].y > y)) continue;
                float dy = p[j].y - p[i].y;
                if (Mathf.Abs(dy) < 0.0001f) continue;
                if (x < (p[j].x - p[i].x) * (y - p[i].y) / dy + p[i].x) inside = !inside;
            }
            return inside;
        }
    }
}
