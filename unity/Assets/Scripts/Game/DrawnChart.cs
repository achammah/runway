using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;

namespace Runway.Game
{
    /// <summary>
    /// THE CHARTS THIS GAME DRAWS, and no others: the draft page's cap-table donut,
    /// the binder's own cap-table PIE, its wobbly sparkline, the pen ellipse that
    /// rings the live tab and the little clock that heads a ticking threat.
    ///
    /// TWO CAP TABLES, TWO DRAWINGS. `Donut` is founder_draft_screen.gd's
    /// `CapTableDonut` — a ring, cut to the card. `CapPie` is binder.gd's `_Pie` — a
    /// FULL pie at 0.38 of the box, slices at three-quarter alpha under a 4px ink rim.
    /// They are different classes in the original and stay different here.
    ///
    /// Godot draws these with draw_colored_polygon / draw_polyline every frame. Unity
    /// has no immediate-mode canvas, so — exactly as DrawnUI does with its ink edges —
    /// each chart is rasterised ONCE into a texture and cached by its own values. A
    /// donut only re-bakes when the split actually changes, which is when somebody
    /// pressed a button.
    ///
    /// The wobble is seeded, so the same numbers give the same drawing every time.
    /// </summary>
    public static class DrawnChart
    {
        static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        /// The cap table: slices swept clockwise from twelve o'clock, ring cut out of
        /// the middle. `innerFrac` 0 gives a plain pie.
        public static Sprite Donut(IList<float> pct, IList<Color> colors, int side,
                                   float innerFrac = 0.55f)
        {
            string key = Key("donut", side, innerFrac, pct, colors);
            Sprite cached;
            if (_cache.TryGetValue(key, out cached) && cached != null) return cached;

            side = Mathf.Max(side, 16);
            var px = NewCanvas(side, side);
            float c = side * 0.5f;
            float rOut = c - 4f;
            float rIn = rOut * innerFrac;
            // ANGLES ARE MEASURED CLOCKWISE FROM TWELVE, the way a cap table is read,
            // so a slice is a plain [from, to) interval in one number and no pixel can
            // land between two of them.
            float acc = 0f;
            var bounds = new List<float[]>();      // {from, to, r, g, b}
            for (int i = 0; i < pct.Count; i++)
            {
                float sweep = Mathf.PI * 2f * Mathf.Clamp(pct[i], 0f, 100f) / 100f;
                if (sweep <= 0.0001f) continue;
                Color col = i < colors.Count ? colors[i] : DrawnUI.Sage;
                bounds.Add(new[] { acc, acc + sweep, col.r, col.g, col.b });
                acc += sweep;
            }
            const float TwoPi = Mathf.PI * 2f;
            for (int y = 0; y < side; y++)
            {
                for (int x = 0; x < side; x++)
                {
                    float dx = x + 0.5f - c;
                    float dy = c - (y + 0.5f);      // the canvas is built top-left down
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > rOut || d < rIn) continue;
                    float ang = Mathf.Atan2(dx, dy);      // 0 at twelve, growing clockwise
                    if (ang < 0f) ang += TwoPi;
                    for (int s = 0; s < bounds.Count; s++)
                    {
                        if (ang >= bounds[s][0] && ang < bounds[s][1])
                        {
                            px[y * side + x] = new Color32(
                                (byte)(bounds[s][2] * 255f), (byte)(bounds[s][3] * 255f),
                                (byte)(bounds[s][4] * 255f), 255);
                            break;
                        }
                    }
                }
            }
            Sprite sp = Bake(px, side, side);
            _cache[key] = sp;
            return sp;
        }

        // ── the binder's cap table: binder.gd class _Pie ────────────────────────

        /// `_Pie` draws at 0.38 of the SHORT side, never edge to edge. Everything the
        /// binder places around the wheel — the labels at r + 40 — is measured off this
        /// one number, so it lives here and both callers read it.
        public const float PieRadiusFrac = 0.38f;

        /// binder.gd's `_Pie._draw`, pixel for pixel:
        ///   · slices swept clockwise from twelve, each `Color(col, 0.75)`
        ///   · a 4px INK rim over the lot — `draw_arc(c, r, 0, TAU, 64, INK, 4.0)`
        /// The labels are NOT here: `draw_string` needs the hand, so the binder hangs
        /// them round the wheel itself. `PieRadiusFrac` is the geometry they share.
        public static Sprite CapPie(IList<float> pct, IList<Color> colors, int side)
        {
            string key = Key("cappie", side, 0f, pct, colors);
            Sprite cached;
            if (_cache.TryGetValue(key, out cached) && cached != null) return cached;

            side = Mathf.Max(side, 16);
            var px = NewCanvas(side, side);
            float c = side * 0.5f;
            float r = side * PieRadiusFrac;

            // ANGLES ARE MEASURED CLOCKWISE FROM TWELVE, the way a cap table is read,
            // so a slice is a plain [from, to) interval in one number and no pixel can
            // land between two of them.
            const float TwoPi = Mathf.PI * 2f;
            float acc = 0f;
            var bounds = new List<float[]>();      // {from, to, r, g, b}
            for (int i = 0; i < pct.Count; i++)
            {
                float frac = Mathf.Clamp(pct[i], 0f, 100f) / 100f;
                if (frac <= 0.001f) continue;      // `_Pie` skips these and does not sweep
                Color col = i < colors.Count ? colors[i] : DrawnUI.Sage;
                bounds.Add(new[] { acc, acc + TwoPi * frac, col.r, col.g, col.b });
                acc += TwoPi * frac;
            }
            for (int y = 0; y < side; y++)
            {
                for (int x = 0; x < side; x++)
                {
                    float dx = x + 0.5f - c;
                    float dy = c - (y + 0.5f);      // the canvas is built top-left down
                    if (dx * dx + dy * dy > r * r) continue;
                    float ang = Mathf.Atan2(dx, dy);      // 0 at twelve, growing clockwise
                    if (ang < 0f) ang += TwoPi;
                    for (int s = 0; s < bounds.Count; s++)
                    {
                        if (ang >= bounds[s][0] && ang < bounds[s][1])
                        {
                            px[y * side + x] = new Color32(
                                (byte)(bounds[s][2] * 255f), (byte)(bounds[s][3] * 255f),
                                (byte)(bounds[s][4] * 255f), 191);      // Color(col, 0.75)
                            break;
                        }
                    }
                }
            }
            // the rim rides OVER three-quarter-alpha wedges, so it blends rather than
            // taking the brightest alpha — alpha-max would eat its soft edge
            var rim = new List<Vector2>();
            for (int i = 0; i <= 64; i++)
            {
                float t = TwoPi * i / 64f;
                rim.Add(new Vector2(c + Mathf.Cos(t) * r, c + Mathf.Sin(t) * r));
            }
            BlendStroke(px, side, side, rim, 4f, DrawnUI.Ink);

            Sprite sp = Bake(px, side, side);
            _cache[key] = sp;
            return sp;
        }

        /// The binder's weekly line: a wobbled polyline with the last point inked.
        public static Sprite Spark(IList<float> series, Color col, int w, int h)
        {
            string key = Key("spark", w, h, series, new[] { col });
            Sprite cached;
            if (_cache.TryGetValue(key, out cached) && cached != null) return cached;

            w = Mathf.Max(w, 16);
            h = Mathf.Max(h, 12);
            var px = NewCanvas(w, h);
            // THE GROUND WASH — `_Spark._draw` opens with draw_rect(size, Color(0,0,0,
            // 0.03)) and it is the reason a sparkline reads as a panel of the sheet
            // rather than a line floating on cream. It is laid down BEFORE the return
            // for a short series, so an empty chart still has its ground.
            var wash = new Color32(0, 0, 0, 8);          // 0.03 * 255
            for (int i = 0; i < px.Length; i++) px[i] = wash;
            if (series != null && series.Count >= 2)
            {
                float lo = float.MaxValue;
                float hi = float.MinValue;
                for (int i = 0; i < series.Count; i++)
                {
                    lo = Mathf.Min(lo, series[i]);
                    hi = Mathf.Max(hi, series[i]);
                }
                if (hi - lo < 1f) hi = lo + 1f;
                var rng = new System.Random(13);
                var pts = new List<Vector2>();
                for (int i = 0; i < series.Count; i++)
                {
                    float x = 8f + (w - 16f) * i / (series.Count - 1);
                    float y = h - 10f - (h - 24f) * (series[i] - lo) / (hi - lo);
                    pts.Add(new Vector2(x, y + (float)(rng.NextDouble() * 2.0 - 1.0)));
                }
                Stroke(px, w, h, pts, 4f, col);
                Disc(px, w, h, pts[pts.Count - 1].x, pts[pts.Count - 1].y, 6f, DrawnUI.Coral);
            }
            Sprite sp = Bake(px, w, h);
            _cache[key] = sp;
            return sp;
        }

        /// A sparkline, WHOLE: the wash, the line, and the two numbers `_Spark._draw`
        /// writes into its corners — hi at baseline y 22, lo at baseline y h-4, both in
        /// the hand at 20px and ink at 0.45. Without them a spark says "it moved" and
        /// never says how far, which is the one thing the founder is reading it for.
        /// A series too short to draw keeps the wash and says so, exactly as there.
        public static Image MountSpark(RectTransform parent, IList<float> series, Color col,
                                       float x, float y, float w, float h)
        {
            Image img = Mount(parent, "spark", Spark(series, col, (int)w, (int)h), x, y, w, h);
            if (series == null || series.Count < 2)
            {
                // draw_string(f, Vector2(12, size.y * 0.55), …, 24) — a BASELINE
                DrawnUI.HandLabel(parent, "not enough weeks on record yet",
                                  x + 12f, y + h * 0.55f - 24f * 0.78f, 24f,
                                  DrawnUI.WithAlpha(DrawnUI.Ink, 0.4f));
                return img;
            }
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < series.Count; i++)
            {
                lo = Mathf.Min(lo, series[i]);
                hi = Mathf.Max(hi, series[i]);
            }
            if (hi - lo < 1f) hi = lo + 1f;
            Color faint = DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f);
            DrawnUI.HandLabel(parent, Short(hi), x + 8f, y + 22f - 20f * 0.78f, 20f, faint);
            DrawnUI.HandLabel(parent, Short(lo), x + 8f, y + h - 4f - 20f * 0.78f, 20f, faint);
            return img;
        }

        /// `_Spark._fmt_s`: a number small enough to sit in the corner of a chart.
        public static string Short(float v)
        {
            if (Mathf.Abs(v) >= 1000000f)
                return (v / 1000000f).ToString("0.0",
                    System.Globalization.CultureInfo.InvariantCulture) + "M";
            if (Mathf.Abs(v) >= 1000f)
                return (v / 1000f).ToString("0",
                    System.Globalization.CultureInfo.InvariantCulture) + "k";
            return v.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        }

        // ── the pen marks the binder draws by hand ──────────────────────────────

        /// THE RING ROUND THE LIVE TAB IS AN ELLIPSE, not a circle squashed into a box.
        /// binder.gd walks 33 points of `cos(t) * 68, sin(t) * 26` and strokes them at a
        /// UNIFORM 3.5px; a baked circle stretched to a wide short rect thins its own
        /// stroke top and bottom and comes out a size small. So it is walked here too.
        /// The sprite is exactly the ink's own box — mount it 1:1 and never scale it.
        public static Sprite PenEllipse(float rx, float ry, float thickness, float jitter,
                                        int seed, Color col)
        {
            string key = string.Format("ellipse|{0}|{1}|{2}|{3}|{4}|{5}", rx, ry, thickness,
                                       jitter, seed, ColorUtility.ToHtmlStringRGB(col));
            Sprite cached;
            if (_cache.TryGetValue(key, out cached) && cached != null) return cached;

            int pad = Mathf.CeilToInt(jitter + thickness * 0.5f + 2f);
            int w = Mathf.CeilToInt(rx * 2f) + pad * 2;
            int h = Mathf.CeilToInt(ry * 2f) + pad * 2;
            var px = NewCanvas(w, h);
            var rng = new System.Random(seed);
            var pts = new List<Vector2>();
            for (int i = 0; i < 33; i++)
            {
                float t = Mathf.PI * 2f * i / 32f;
                pts.Add(new Vector2(
                    w * 0.5f + Mathf.Cos(t) * rx + (float)(rng.NextDouble() * 2.0 - 1.0) * jitter,
                    h * 0.5f + Mathf.Sin(t) * ry + (float)(rng.NextDouble() * 2.0 - 1.0) * jitter));
            }
            Stroke(px, w, h, pts, thickness, col);

            Sprite sp = Bake(px, w, h);
            _cache[key] = sp;
            return sp;
        }

        /// THE TICKING CLOCK, DRAWN. The threats page heads every clock with one, and
        /// the original leans on an emoji the hand font has never carried — so it is
        /// drawn instead: a wobbled ring in the line's own colour and two ink hands.
        /// Nothing here can fall back to a box.
        public static Sprite Clock(int side, Color face, Color hands)
        {
            string key = string.Format("clock|{0}|{1}|{2}", side,
                ColorUtility.ToHtmlStringRGB(face), ColorUtility.ToHtmlStringRGB(hands));
            Sprite cached;
            if (_cache.TryGetValue(key, out cached) && cached != null) return cached;

            side = Mathf.Max(side, 10);
            var px = NewCanvas(side, side);
            float c = side * 0.5f;
            float r = c - 3f;
            var rng = new System.Random(11);
            var ring = new List<Vector2>();
            for (int i = 0; i < 21; i++)
            {
                float t = Mathf.PI * 2f * i / 20f;
                float rr = r + (float)(rng.NextDouble() * 2.0 - 1.0) * 0.7f;
                ring.Add(new Vector2(c + Mathf.Cos(t) * rr, c + Mathf.Sin(t) * rr));
            }
            Stroke(px, side, side, ring, side * 0.085f, face);
            // the minute hand straight up, the hour hand short and out to four — the
            // two-hand silhouette that reads as a clock at 30px and at 14
            float hw = Mathf.Max(side * 0.07f, 1.6f);
            Stroke(px, side, side, new List<Vector2> {
                new Vector2(c, c), new Vector2(c, c - r * 0.72f) }, hw, hands);
            Stroke(px, side, side, new List<Vector2> {
                new Vector2(c, c), new Vector2(c + r * 0.42f, c + r * 0.42f) }, hw, hands);

            Sprite sp = Bake(px, side, side);
            _cache[key] = sp;
            return sp;
        }

        /// A chart, mounted. Returns the Image so a caller can move it.
        public static Image Mount(RectTransform parent, string name, Sprite sprite,
                                  float x, float y, float w, float h)
        {
            var rt = DrawnUI.Rect(parent, name, x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.raycastTarget = false;
            return img;
        }

        // ── the rasteriser ─────────────────────────────────────────────────────

        static string Key(string kind, float a, float b, IList<float> nums, IList<Color> cols)
        {
            var sb = new StringBuilder(kind);
            sb.Append('|').Append(a).Append('|').Append(b);
            if (nums != null)
                for (int i = 0; i < nums.Count; i++) sb.Append('|').Append(nums[i].ToString("0.0"));
            if (cols != null)
                for (int i = 0; i < cols.Count; i++) sb.Append('#').Append(ColorUtility.ToHtmlStringRGB(cols[i]));
            return sb.ToString();
        }

        static Color32[] NewCanvas(int w, int h)
        {
            var px = new Color32[w * h];
            var clear = new Color32(255, 255, 255, 0);
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            return px;
        }

        static void Stroke(Color32[] px, int w, int h, List<Vector2> pts, float thickness, Color col)
        {
            var c32 = new Color32((byte)(col.r * 255f), (byte)(col.g * 255f),
                                  (byte)(col.b * 255f), 255);
            float r = Mathf.Max(thickness * 0.5f, 0.5f);
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                float len = Vector2.Distance(pts[i], pts[i + 1]);
                int steps = Mathf.Max(Mathf.CeilToInt(len * 2f), 1);
                for (int s = 0; s <= steps; s++)
                {
                    Vector2 p = Vector2.Lerp(pts[i], pts[i + 1], (float)s / steps);
                    Disc(px, w, h, p.x, p.y, r, col);
                }
            }
        }

        /// The same stroke, but composited OVER what is already there instead of taking
        /// the brighter alpha. A rim laid on a half-transparent wedge needs this: with
        /// alpha-max the wedge wins wherever the rim's edge is softer than 0.75 and the
        /// ink comes out gnawed.
        static void BlendStroke(Color32[] px, int w, int h, List<Vector2> pts,
                                float thickness, Color col)
        {
            float r = Mathf.Max(thickness * 0.5f, 0.5f);
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                float len = Vector2.Distance(pts[i], pts[i + 1]);
                int steps = Mathf.Max(Mathf.CeilToInt(len * 2f), 1);
                for (int s = 0; s <= steps; s++)
                {
                    Vector2 p = Vector2.Lerp(pts[i], pts[i + 1], (float)s / steps);
                    BlendDisc(px, w, h, p.x, p.y, r, col);
                }
            }
        }

        static void BlendDisc(Color32[] px, int w, int h, float cx, float cy, float r, Color col)
        {
            int x0 = Mathf.Max(Mathf.FloorToInt(cx - r - 1f), 0);
            int x1 = Mathf.Min(Mathf.CeilToInt(cx + r + 1f), w - 1);
            int y0 = Mathf.Max(Mathf.FloorToInt(cy - r - 1f), 0);
            int y1 = Mathf.Min(Mathf.CeilToInt(cy + r + 1f), h - 1);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - cx;
                    float dy = y + 0.5f - cy;
                    float sa = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    if (sa <= 0f) continue;
                    int idx = y * w + x;
                    Color32 dst = px[idx];
                    float da = dst.a / 255f;
                    float outA = sa + da * (1f - sa);
                    if (outA <= 0.0001f) continue;
                    float k = da * (1f - sa);
                    px[idx] = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt((col.r * 255f * sa + dst.r * k) / outA), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt((col.g * 255f * sa + dst.g * k) / outA), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt((col.b * 255f * sa + dst.b * k) / outA), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(outA * 255f), 0, 255));
                }
            }
        }

        static void Disc(Color32[] px, int w, int h, float cx, float cy, float r, Color col)
        {
            int x0 = Mathf.Max(Mathf.FloorToInt(cx - r - 1f), 0);
            int x1 = Mathf.Min(Mathf.CeilToInt(cx + r + 1f), w - 1);
            int y0 = Mathf.Max(Mathf.FloorToInt(cy - r - 1f), 0);
            int y1 = Mathf.Min(Mathf.CeilToInt(cy + r + 1f), h - 1);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - cx;
                    float dy = y + 0.5f - cy;
                    float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    if (a <= 0f) continue;
                    int idx = y * w + x;
                    byte want = (byte)Mathf.RoundToInt(a * 255f);
                    if (px[idx].a >= want) continue;
                    px[idx] = new Color32((byte)(col.r * 255f), (byte)(col.g * 255f),
                                          (byte)(col.b * 255f), want);
                }
            }
        }

        /// The canvas is built top-left down, the way every .gd file reads; Unity
        /// textures start bottom-left, so the rows are flipped exactly once, here.
        static Sprite Bake(Color32[] px, int w, int h)
        {
            var flipped = new Color32[px.Length];
            for (int y = 0; y < h; y++)
                Array.Copy(px, y * w, flipped, (h - 1 - y) * w, w);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.SetPixels32(flipped);
            tex.Apply(false, false);
            return Sprite.Create(tex, new UnityEngine.Rect(0f, 0f, w, h),
                                 new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
        }
    }
}
