using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;

namespace Runway.Game
{
    /// <summary>
    /// THE THREE CHARTS THIS GAME DRAWS, and no others: the cap-table donut, the
    /// binder's wobbly sparkline, and the debt jar.
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

        /// The binder's weekly line: a wobbled polyline with the last point inked.
        public static Sprite Spark(IList<float> series, Color col, int w, int h)
        {
            string key = Key("spark", w, h, series, new[] { col });
            Sprite cached;
            if (_cache.TryGetValue(key, out cached) && cached != null) return cached;

            w = Mathf.Max(w, 16);
            h = Mathf.Max(h, 12);
            var px = NewCanvas(w, h);
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
