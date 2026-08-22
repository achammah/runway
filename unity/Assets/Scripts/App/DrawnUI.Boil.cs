using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Runway.App
{
    /// <summary>
    /// LINE-BOIL — the ink stops being a photograph of a drawing and becomes a drawing
    /// that is being drawn. Hand-animated line art "boils": each held frame is a fresh
    /// pass of the same pen, so every border creeps by a hair and the page reads alive.
    ///
    /// HOW IT ATTACHES TO WHAT IS ALREADY THERE. `DrawnUI` bakes every ink edge, rule,
    /// ring and vertical rule into a cached texture keyed by (shape, size, style, seed).
    /// This kit takes such a baked texture and derives TWO more of it — the same drawing,
    /// redrawn — and an `InkBoil` component cycles the three at 8fps on one shared clock.
    ///
    /// WHY THE VARIANTS ARE WARPED, NOT RE-SEEDED. Re-running the rasteriser with seed+1
    /// gives an INDEPENDENT wobble, so a vertex can jump up to twice the jitter between
    /// frames — a visible crawl, well past the 1.5px the look is allowed. Instead each
    /// variant displaces the baked texture through a smooth low-frequency field built
    /// from `System.Random(seed + 1)` (direction) and `System.Random(seed + 2)` (radius),
    /// and the two fields are held 60° apart at equal radius. Three points at 0, v and w
    /// with |v| = |w| = |v - w| = r: EVERY frame-to-frame move is exactly r ≤ Amplitude,
    /// by construction, in any order the clock plays them. The pen wanders; it never
    /// walks. The interior of a filled shape maps onto itself, so fills do not move —
    /// only the alpha gradient at the stroke, which is the whole point.
    ///
    /// WHAT NEVER BOILS. Text. `Apply` refuses any TMP or UGUI text graphic, and only
    /// accepts a graphic whose texture is a readable, fully-mapped ink bake. Readability
    /// alone rejects the film sheets and every photographed asset in the game.
    ///
    /// KILL-SWITCH: the environment variable RUNWAY_FX_BOIL. Absent or "1" = on, "0" =
    /// off, read through `Env` so the project .env and keys.env answer it too. Off means
    /// `Apply` returns null and not one texture is baked, so the build is the build it
    /// was before this lane existed.
    /// </summary>
    public static class DrawnBoil
    {
        /// The held drawings in the cycle: the bake plus two redraws of it.
        public const int Frames = 3;

        /// Sketch-frame rate. 8fps is the rate hand-drawn boil is shot on — fast enough
        /// to live, slow enough that the eye reads a drawing and not a vibration.
        public const float Fps = 8f;

        /// The ceiling D1c holds the look to. Amplitude is peak displacement, the same
        /// quantity the rasteriser's `jitter` is, and every frame-to-frame move equals it.
        public const float MaxAmplitude = 1.5f;

        public const string Switch = "RUNWAY_FX_BOIL";

        /// "no seed given — derive one from the texture's own content".
        public const int NoSeed = int.MinValue;

        /// Peak displacement in pixels. Clamped to MaxAmplitude on the way in.
        public static float Amplitude
        {
            get { return _amplitude; }
            set { _amplitude = Mathf.Clamp(value, 0f, MaxAmplitude); }
        }
        static float _amplitude = 1.1f;

        /// A bake above this many pixels is left alone: three copies of a very large ink
        /// texture is memory the room budget would rather spend on sheets.
        public static int MaxPixels = 1 << 21;   // 2,097,152 — a 1448² bake

        /// Redraws are upload-and-forget, so their CPU copy is dropped and a boiling
        /// card costs half. The shot harness turns this on to read the drawings back.
        public static bool KeepReadable = false;

        /// How the swap reaches the screen. `Renderer` writes the texture straight onto
        /// the CanvasRenderer, which costs no canvas rebuild and no allocation; `Sprite`
        /// assigns `overrideSprite` instead and is the fallback if a platform ever
        /// disagrees about the first.
        public enum Swap { Renderer, Sprite }
        public static Swap SwapMode = Swap.Renderer;

        // ── the kill-switch ────────────────────────────────────────────────────

        static bool? _on;

        /// Absent or "1" = on; "0" = off. Settable so the kill-switch matrix (D8) and
        /// the shot harness can flip it without touching the process environment.
        public static bool Enabled
        {
            get
            {
                if (_on.HasValue) return _on.Value;
                string v = "1";
                try { v = Env.Get(Switch, "1"); }
                catch (Exception) { v = "1"; }
                _on = !string.Equals(v.Trim(), "0", StringComparison.Ordinal);
                return _on.Value;
            }
            set { _on = value; }
        }

        /// Forget a cached read of the switch — after Env.Reload(), or between harness runs.
        public static void ForgetSwitch() { _on = null; }

        /// Drop every redraw and every eligibility verdict. For the kill-switch matrix
        /// and the shot harness, which build the same kit more than once per session.
        public static void Forget()
        {
            InkBoilBake.Forget();
        }

        // ── the one entry point ────────────────────────────────────────────────

        /// Make one baked ink graphic boil. Returns the component, or null when the
        /// switch is off or the graphic is not eligible ink. Safe to call twice.
        public static InkBoil Apply(Graphic ink) { return Apply(ink, NoSeed); }

        /// The same, with the rasteriser seed the bake was made from, so the redraws of
        /// one card are the same redraws every session.
        public static InkBoil Apply(Graphic ink, int seed)
        {
            // a BUTTON's edge never boils: on a pressable it reads as
            // vibration (owner live-play); Godot's paper buttons hold still
            if (ink != null && ink.GetComponentInParent<UnityEngine.UI.Button>() != null) return null;
            if (!Enabled || ink == null) return null;
            if (IsText(ink)) return null;

            InkBoil had = ink.GetComponent<InkBoil>();
            if (had != null) return had;

            Sprite baseSprite;
            Texture2D tex = InkTexture(ink, out baseSprite);
            if (tex == null) return null;
            if (tex.width * tex.height > MaxPixels) return null;
            if (DrawScale(ink, tex) * Amplitude > MaxAmplitude) return null;

            InkBoilBake.Set set = InkBoilBake.Variants(tex, baseSprite, seed, Amplitude);
            if (set == null) return null;

            InkBoil boil = ink.gameObject.AddComponent<InkBoil>();
            boil.Bind(ink, set.Textures, set.Sprites);
            return boil;
        }

        /// Every eligible ink graphic under one screen, in one call — the coverage net
        /// for the twenty-odd places a screen sets `img.sprite = DrawnUI.SomethingSprite(...)`
        /// itself. Returns how many came alive. Text, flat colour fills, photographed art
        /// and film sheets are all refused by the eligibility test, not by a name list.
        public static int Sweep(Component root)
        {
            if (!Enabled || root == null) return 0;
            Image[] found = root.GetComponentsInChildren<Image>(true);
            int n = 0;
            for (int i = 0; i < found.Length; i++)
                if (Apply(found[i]) != null) n++;
            return n;
        }

        // ── eligibility ────────────────────────────────────────────────────────

        /// How far the bake is stretched to fill its rect. A 104px disc drawn across a
        /// 400px spotlight pool magnifies every texture pixel by four, and with it the
        /// boil — so a bake drawn well above 1:1 is left alone rather than made to
        /// wobble by six. Below 1:1 the move only shrinks, which is always allowed.
        static float DrawScale(Graphic g, Texture2D tex)
        {
            RectTransform rt = g.rectTransform;
            if (rt == null || tex.width <= 0 || tex.height <= 0) return 1f;
            UnityEngine.Rect r = rt.rect;
            float sx = Mathf.Abs(r.width) / tex.width;
            float sy = Mathf.Abs(r.height) / tex.height;
            return Mathf.Max(sx, sy);
        }

        static bool IsText(Graphic g)
        {
            if (g is TMP_Text) return true;
            if (g is Text) return true;
            // a graphic sharing its object with a label is that label's furniture
            return g.GetComponent<TMP_Text>() != null;
        }

        /// The ink bake behind a graphic, or null if this graphic is not one.
        /// A DrawnUI bake is: a readable RGBA32 Texture2D, mapped whole, whose colour is
        /// pure white everywhere the alpha is not zero (the rasteriser writes white and
        /// lets `Graphic.color` do the ink) — a signature no photograph passes.
        static Texture2D InkTexture(Graphic g, out Sprite baseSprite)
        {
            baseSprite = null;
            Texture2D tex = null;

            Image img = g as Image;
            if (img != null)
            {
                Sprite s = img.sprite;
                if (s == null) return null;
                tex = s.texture as Texture2D;
                if (tex == null) return null;
                UnityEngine.Rect r = s.textureRect;
                if (Mathf.RoundToInt(r.width) != tex.width || Mathf.RoundToInt(r.height) != tex.height)
                    return null;
                baseSprite = s;
            }
            else
            {
                RawImage raw = g as RawImage;
                if (raw == null) return null;
                tex = raw.texture as Texture2D;
                if (tex == null) return null;
                UnityEngine.Rect uv = raw.uvRect;
                if (uv.x != 0f || uv.y != 0f || uv.width != 1f || uv.height != 1f) return null;
            }

            if (!tex.isReadable) return null;
            if (tex.width < 4 || tex.height < 4) return null;
            // the last gate — "is this a drawing at all" — is the bake's, because the
            // bake reads the pixels anyway and can answer it exactly instead of by
            // sampling; a texture it refuses is remembered as refused.
            return tex;
        }
    }

    /// <summary>
    /// The redraw. One bake in, three held drawings out, remembered per texture so the
    /// forty cards wearing the same 420x76 edge pay for it once.
    /// </summary>
    internal static class InkBoilBake
    {
        internal sealed class Set
        {
            public Texture2D[] Textures;
            public Sprite[] Sprites;
        }

        /// Displacement-field cell in pixels. Small enough that a long rule bends along
        /// its length rather than sliding, large enough that a 4px stroke stays a stroke.
        const int Cell = 24;

        /// Ink-occupancy block. Must exceed MaxAmplitude + 2 so an empty dilated block
        /// truly cannot be reached by a sample.
        const int Block = 8;

        static readonly Dictionary<Texture2D, Set> _cache = new Dictionary<Texture2D, Set>();

        internal static Set Variants(Texture2D src, Sprite baseSprite, int seed, float amp)
        {
            if (src == null) return null;

            Set set;
            if (_cache.TryGetValue(src, out set))
            {
                if (set == null || set.Textures == null || set.Textures[1] == null) return null;
                EnsureSprites(set, baseSprite);
                return set;
            }

            set = Build(src, seed, amp);
            _cache[src] = set;
            if (set != null) EnsureSprites(set, baseSprite);
            return set;
        }

        /// Forget every bake — the harness runs the same kit several times per session.
        internal static void Forget() { _cache.Clear(); }

        internal static int Cached { get { return _cache.Count; } }

        static void EnsureSprites(Set set, Sprite template)
        {
            if (set.Sprites != null || template == null) return;
            var spr = new Sprite[DrawnBoil.Frames];
            spr[0] = template;
            float ppu = template.pixelsPerUnit > 0f ? template.pixelsPerUnit : 100f;
            UnityEngine.Rect r = template.rect;
            var pivot = new Vector2(r.width > 0f ? template.pivot.x / r.width : 0.5f,
                                    r.height > 0f ? template.pivot.y / r.height : 0.5f);
            for (int i = 1; i < DrawnBoil.Frames; i++)
            {
                Texture2D t = set.Textures[i];
                if (t == null) { spr[i] = template; continue; }
                try
                {
                    spr[i] = Sprite.Create(t, new UnityEngine.Rect(0f, 0f, t.width, t.height),
                                           pivot, ppu, 0u, SpriteMeshType.FullRect);
                    spr[i].name = "boil" + i;
                }
                catch (Exception) { spr[i] = template; }
            }
            set.Sprites = spr;
        }

        static Set Build(Texture2D src, int seed, float amp)
        {
            int w = src.width, h = src.height;
            Color32[] s;
            try { s = src.GetPixels32(); }
            catch (Exception) { return null; }
            if (s == null || s.Length != w * h) return null;

            if (seed == DrawnBoil.NoSeed) seed = ContentSeed(s, w, h);
            // kept small and positive: System.Random(int.MinValue) is not safe everywhere,
            // and seed + 1 / seed + 2 must not be able to overflow
            seed = seed & 0x3FFFFFF;
            amp = Mathf.Clamp(amp, 0f, DrawnBoil.MaxAmplitude);

            bool[] near = NearInk(s, w, h);
            if (near == null) return null;

            // ── the two fields, 60° apart at one radius ────────────────────────
            int nx = w / Cell + 2, ny = h / Cell + 2;
            var vx = new float[nx * ny];
            var vy = new float[nx * ny];
            var wx = new float[nx * ny];
            var wy = new float[nx * ny];
            var dir = new System.Random(seed + 1);
            var rad = new System.Random(seed + 2);
            const double Sixty = Math.PI / 3.0;
            for (int i = 0; i < vx.Length; i++)
            {
                double th = dir.NextDouble() * Math.PI * 2.0;
                double r = rad.NextDouble() * amp;
                vx[i] = (float)(Math.Cos(th) * r);
                vy[i] = (float)(Math.Sin(th) * r);
                wx[i] = (float)(Math.Cos(th + Sixty) * r);
                wy[i] = (float)(Math.Sin(th + Sixty) * r);
            }

            // A card edge is a thin frame in a large transparent field, and the field is
            // the same in all three drawings. So both redraws START as a copy of the
            // bake — one memcpy — and only the blocks that can see ink are re-sampled.
            // On the game's biggest sheet that is a tenth of the pixels.
            var a = new Color32[w * h];
            var b = new Color32[w * h];
            Array.Copy(s, a, s.Length);
            Array.Copy(s, b, s.Length);

            int bw = (w + Block - 1) / Block;
            int bh = (h + Block - 1) / Block;

            for (int by = 0; by < bh; by++)
            {
                int yTop = by * Block, yEnd = Mathf.Min(yTop + Block, h);
                int blockRow = by * bw;
                for (int bx = 0; bx < bw; bx++)
                {
                    if (!near[blockRow + bx]) continue;
                    int xLeft = bx * Block, xEnd = Mathf.Min(xLeft + Block, w);

                    for (int y = yTop; y < yEnd; y++)
                    {
                        float gy = (float)y / Cell;
                        int j0 = (int)gy;
                        float fy = gy - j0;
                        int j1 = Mathf.Min(j0 + 1, ny - 1);
                        int rowOut = y * w;

                        for (int x = xLeft; x < xEnd; x++)
                        {
                            float gx = (float)x / Cell;
                            int i0 = (int)gx;
                            float fx = gx - i0;
                            int i1 = Mathf.Min(i0 + 1, nx - 1);

                            float w00 = (1f - fx) * (1f - fy);
                            float w10 = fx * (1f - fy);
                            float w01 = (1f - fx) * fy;
                            float w11 = fx * fy;
                            int n00 = j0 * nx + i0, n10 = j0 * nx + i1;
                            int n01 = j1 * nx + i0, n11 = j1 * nx + i1;

                            float dax = vx[n00] * w00 + vx[n10] * w10 + vx[n01] * w01 + vx[n11] * w11;
                            float day = vy[n00] * w00 + vy[n10] * w10 + vy[n01] * w01 + vy[n11] * w11;
                            float dbx = wx[n00] * w00 + wx[n10] * w10 + wx[n01] * w01 + wx[n11] * w11;
                            float dby = wy[n00] * w00 + wy[n10] * w10 + wy[n01] * w01 + wy[n11] * w11;

                            a[rowOut + x] = Sample(s, w, h, x - dax, y - day);
                            b[rowOut + x] = Sample(s, w, h, x - dbx, y - dby);
                        }
                    }
                }
            }

            var set = new Set();
            set.Textures = new Texture2D[DrawnBoil.Frames];
            set.Textures[0] = src;
            set.Textures[1] = Wrap(a, w, h, src, "boil1");
            set.Textures[2] = Wrap(b, w, h, src, "boil2");
            if (set.Textures[1] == null || set.Textures[2] == null) return null;
            return set;
        }

        /// A block grid of "there is ink within one block of here". The transparent field
        /// inside a card edge is most of the texture and costs nothing to skip.
        ///
        /// The same pass answers "is this a DrawnUI bake at all": the rasteriser writes
        /// pure white and lets `Graphic.color` be the ink, so one coloured pixel under a
        /// non-zero alpha means a photograph, and a photograph is never redrawn. Null
        /// here is the refusal, and it is exact rather than sampled because the pass had
        /// to read every pixel regardless.
        static bool[] NearInk(Color32[] s, int w, int h)
        {
            int bw = (w + Block - 1) / Block, bh = (h + Block - 1) / Block;
            var ink = new bool[bw * bh];
            bool any = false;
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                int br = (y / Block) * bw;
                for (int x = 0; x < w; x++)
                {
                    Color32 c = s[row + x];
                    if (c.a == 0) continue;
                    if (c.r != 255 || c.g != 255 || c.b != 255) return null;
                    ink[br + x / Block] = true;
                    any = true;
                }
            }
            if (!any) return null;
            var near = new bool[bw * bh];
            for (int by = 0; by < bh; by++)
            {
                for (int bx = 0; bx < bw; bx++)
                {
                    bool touches = false;
                    for (int dy = -1; dy <= 1 && !touches; dy++)
                    {
                        int ny = by + dy;
                        if (ny < 0 || ny >= bh) continue;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx2 = bx + dx;
                            if (nx2 < 0 || nx2 >= bw) continue;
                            if (ink[ny * bw + nx2]) { touches = true; break; }
                        }
                    }
                    near[by * bw + bx] = touches;
                }
            }
            return near;
        }

        /// Bilinear, in premultiplied alpha so a stroke's soft edge cannot fringe.
        static Color32 Sample(Color32[] s, int w, int h, float u, float v)
        {
            int x0 = Mathf.FloorToInt(u), y0 = Mathf.FloorToInt(v);
            float fx = u - x0, fy = v - y0;
            int x1 = x0 + 1, y1 = y0 + 1;
            x0 = Mathf.Clamp(x0, 0, w - 1); x1 = Mathf.Clamp(x1, 0, w - 1);
            y0 = Mathf.Clamp(y0, 0, h - 1); y1 = Mathf.Clamp(y1, 0, h - 1);

            Color32 p00 = s[y0 * w + x0], p10 = s[y0 * w + x1];
            Color32 p01 = s[y1 * w + x0], p11 = s[y1 * w + x1];
            float w00 = (1f - fx) * (1f - fy), w10 = fx * (1f - fy);
            float w01 = (1f - fx) * fy, w11 = fx * fy;

            float alpha = p00.a * w00 + p10.a * w10 + p01.a * w01 + p11.a * w11;
            if (alpha < 0.5f) return new Color32(255, 255, 255, 0);

            float pr = p00.r * p00.a * w00 + p10.r * p10.a * w10 + p01.r * p01.a * w01 + p11.r * p11.a * w11;
            float pg = p00.g * p00.a * w00 + p10.g * p10.a * w10 + p01.g * p01.a * w01 + p11.g * p11.a * w11;
            float pb = p00.b * p00.a * w00 + p10.b * p10.a * w10 + p01.b * p01.a * w01 + p11.b * p11.a * w11;

            return new Color32(Byte(pr / alpha), Byte(pg / alpha), Byte(pb / alpha), Byte(alpha));
        }

        static byte Byte(float f)
        {
            int i = Mathf.RoundToInt(f);
            if (i < 0) i = 0;
            if (i > 255) i = 255;
            return (byte)i;
        }

        static Texture2D Wrap(Color32[] px, int w, int h, Texture2D like, string name)
        {
            try
            {
                var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
                t.name = (like != null ? like.name : "ink") + "." + name;
                t.wrapMode = like != null ? like.wrapMode : TextureWrapMode.Clamp;
                t.filterMode = like != null ? like.filterMode : FilterMode.Bilinear;
                t.SetPixels32(px);
                // the redraws are only ever DRAWN, so the CPU copy is dropped on upload
                // and a boiling card costs half of what three readable copies would
                t.Apply(false, !DrawnBoil.KeepReadable);
                return t;
            }
            catch (Exception e)
            {
                Debug.LogWarning("RUNWAY! line-boil could not bake a redraw (" + e.Message + ")");
                return null;
            }
        }

        /// A seed from the drawing itself, so a bake reached without its recipe still
        /// boils the same way every session.
        static int ContentSeed(Color32[] s, int w, int h)
        {
            int seed = 17;
            seed = seed * 31 + w;
            seed = seed * 31 + h;
            int step = Mathf.Max(s.Length / 64, 1);
            for (int i = 0; i < s.Length; i += step) seed = seed * 31 + s[i].a;
            return seed & 0x3FFFFFF;
        }
    }
}
