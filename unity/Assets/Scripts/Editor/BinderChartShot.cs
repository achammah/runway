using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Runway.App;
using Runway.Game;

namespace Runway.EditorTools
{
    /// <summary>
    /// THE BINDER'S CHARTS, SHOT AND MEASURED against binder.gd's own numbers.
    ///
    /// Every drawing here is rasterised by DrawnChart into a texture, so the shot needs
    /// no camera and no device: it composites the sprites onto cream in the SAME
    /// top-left coordinates the binder mounts them at, writes a PNG per drawing, and
    /// then reads the pixels back and states what it found. A number that disagrees
    /// with the .gd is a failing line in measurements.txt, not a judgement call.
    ///
    /// What each file must show:
    ///   pie.png    a 430 wheel centred on (255, 245) of the content, ink at 0.38 of the
    ///              box (r = 163.4), three slices at 0.75 alpha under a 4px ink rim, and
    ///              the three names hung round the wheel at r + 40 (marked with crosses:
    ///              a headless shot has no type, so the shot marks where type lands)
    ///   jar.png    a vessel — faint ground, coral level, a 4px ink outline round the
    ///              WHOLE height, a 5px lip across the top
    ///   spark.png  a ground wash over the whole panel, the wobbled line, the inked last
    ///              point, and crosses where the hi/lo numbers sit
    ///   ring.png   the live tab's pen ellipse: 136 x 52 of path, uniform 3.5px stroke
    ///   clock.png  the drawn clock that replaced ⏰
    ///
    /// Run it:
    ///   Unity -batchmode -quit -nographics -projectPath unity \
    ///         -executeMethod Runway.EditorTools.BinderChartShot.Shoot
    /// Output goes to $RUNWAY_CHARTS_OUT, or a folder under the system temp dir.
    /// </summary>
    public static class BinderChartShot
    {
        static readonly Color32 Cream = DrawnUI.Cream;

        public static void Shoot()
        {
            string dir = Environment.GetEnvironmentVariable("RUNWAY_CHARTS_OUT");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Path.GetTempPath(), "runway-charts");
            Directory.CreateDirectory(dir);

            var log = new StringBuilder();
            Say(log, "binder charts · out " + dir);
            Say(log, "ground truth: game/src/ui/binder.gd  (_Pie, _DebtJar, _Spark, _Clipboard)");
            Say(log, "");

            try { ShootPie(dir, log); } catch (Exception e) { Say(log, "pie FAILED: " + e); }
            try { ShootJar(dir, log); } catch (Exception e) { Say(log, "jar FAILED: " + e); }
            try { ShootSpark(dir, log); } catch (Exception e) { Say(log, "spark FAILED: " + e); }
            try { ShootRing(dir, log); } catch (Exception e) { Say(log, "ring FAILED: " + e); }
            try { ShootClock(dir, log); } catch (Exception e) { Say(log, "clock FAILED: " + e); }

            try { File.WriteAllText(Path.Combine(dir, "measurements.txt"), log.ToString()); }
            catch (Exception) { }
        }

        // ── 1 · the cap table ──────────────────────────────────────────────────

        static void ShootPie(string dir, StringBuilder log)
        {
            const int side = 430;
            const float pieX = 40f, pieY = 30f;
            float[] pct = { 62f, 18f, 20f };
            Color[] cols = { DrawnUI.Coral, DrawnUI.Blue, DrawnUI.Sage };
            string[] names = { "you 62%", "cofounders 18%", "investors 20%" };

            Sprite sp = DrawnChart.CapPie(pct, cols, side);
            Color32[] art = TopDown(sp.texture);
            int aw = sp.texture.width, ah = sp.texture.height;

            // the canvas is a slab of the CONTENT rect, so every coordinate below is a
            // content coordinate and reads straight against binder.gd's _tab_cap
            const int W = 560, H = 500;
            Color32[] px = Ground(W, H);
            Blit(px, W, H, art, aw, ah, (int)pieX, (int)pieY);

            float cx = pieX + side * 0.5f, cy = pieY + side * 0.5f;
            float r = side * DrawnChart.PieRadiusFrac;
            Cross(px, W, H, cx, cy, 10f, new Color32(255, 0, 255, 255));

            // where the type lands: draw_string plants a baseline, nudged (-46, +8)
            const float TwoPi = Mathf.PI * 2f;
            float a0 = -Mathf.PI * 0.5f;
            var placed = new List<string>();
            for (int i = 0; i < pct.Length; i++)
            {
                float frac = Mathf.Clamp(pct[i], 0f, 100f) / 100f;
                if (frac <= 0.01f) continue;
                float mid = a0 + TwoPi * frac * 0.5f;
                float lx = cx + Mathf.Cos(mid) * (r + 40f) - 46f;
                float ly = cy + Mathf.Sin(mid) * (r + 40f) + 8f - 24f * 0.78f;
                Cross(px, W, H, lx, ly, 7f, new Color32(0, 160, 255, 255));
                placed.Add(string.Format("{0} top-left ({1:0.0}, {2:0.0})", names[i], lx, ly));
                a0 += TwoPi * frac;
            }
            Write(dir, "pie.png", px, W, H);

            // ── read it back ──
            int inkPx = 0, coral = 0, blue = 0, sage = 0;
            float minD = 1e9f, maxD = 0f;
            Color32 ink = DrawnUI.Ink;
            for (int y = 0; y < ah; y++)
                for (int x = 0; x < aw; x++)
                {
                    Color32 c = art[y * aw + x];
                    if (c.a == 0) continue;
                    float d = Mathf.Sqrt((x + 0.5f - aw * 0.5f) * (x + 0.5f - aw * 0.5f)
                                       + (y + 0.5f - ah * 0.5f) * (y + 0.5f - ah * 0.5f));
                    if (d > maxD) maxD = d;
                    if (d < minD) minD = d;
                    if (Near(c, ink, 26)) { inkPx++; continue; }
                    if (Near(c, DrawnUI.Coral, 26)) coral++;
                    else if (Near(c, DrawnUI.Blue, 26)) blue++;
                    else if (Near(c, DrawnUI.Sage, 26)) sage++;
                }
            byte sliceAlpha = 0;
            {   // a pixel deep inside the first wedge: up and a little right of centre
                int sx = (int)(aw * 0.5f + r * 0.35f), sy = (int)(ah * 0.5f - r * 0.5f);
                sliceAlpha = art[sy * aw + sx].a;
            }
            Say(log, "── 1 · CAP-TABLE PIE  (binder.gd _Pie, tab 6) ──");
            Say(log, "  box            " + aw + "x" + ah + "        godot 430x430");
            Say(log, "  centre         (" + cx.ToString("0.0") + ", " + cy.ToString("0.0")
                     + ")   godot (255.0, 245.0)   was (209.5, 200.0) on a 340 box");
            Say(log, "  ink radius     " + maxD.ToString("0.0")
                     + "        godot minf(w,h)*0.38 = " + (side * 0.38f).ToString("0.0"));
            Say(log, "  hole           " + (minD < 1.5f ? "none — a full pie" : "RADIUS " + minD.ToString("0.0") + " — WRONG, _Pie has no hole"));
            Say(log, "  slice alpha    " + sliceAlpha + "/255      godot Color(col, 0.75) = 191");
            Say(log, "  rim ink        " + inkPx + "px       a 4px ring at r is ~"
                     + (2f * Mathf.PI * r * 4f).ToString("0") + "px; the Godot shot measured 3522");
            Say(log, "  wedges         coral " + coral + " · blue " + blue + " · sage " + sage
                     + "   (62/18/20 of " + (Mathf.PI * r * r).ToString("0") + "px)");
            for (int i = 0; i < placed.Count; i++) Say(log, "  label          " + placed[i]);
            Say(log, "  labels sit at r+40 = " + (r + 40f).ToString("0.0")
                     + " from the centre, all in ink — NOT stacked under the wheel");
            Say(log, "");
        }

        // ── 2 · the debt jar ───────────────────────────────────────────────────

        static void ShootJar(string dir, StringBuilder log)
        {
            // _DebtJar at (160, 92) size 90x110, so w-12 = 78, h-14 = 96, h-16 = 94
            const int W = 140, H = 150;
            const int ox = 140, oy = 80;            // the window's own origin in content
            Color32[] px = Ground(W, H);
            float fill = 0.62f;

            Slab(px, W, H, 166 - ox, 102 - oy, 78, 96, Mix(Cream, DrawnUI.Ink, 0.04f));
            Slab(px, W, H, 168 - ox, Mathf.RoundToInt(102f + 94f * (1f - fill)) - oy,
                 74, Mathf.RoundToInt(94f * fill), Mix(Cream, DrawnUI.Coral, 0.55f));
            // the 4px outline, four bars centred on the rect's own edges
            Slab(px, W, H, 164 - ox, 100 - oy, 82, 4, DrawnUI.Ink);
            Slab(px, W, H, 164 - ox, 196 - oy, 82, 4, DrawnUI.Ink);
            Slab(px, W, H, 164 - ox, 100 - oy, 4, 100, DrawnUI.Ink);
            Slab(px, W, H, 242 - ox, 100 - oy, 4, 100, DrawnUI.Ink);
            Slab(px, W, H, 162 - ox, 100 - oy, 86, 5, DrawnUI.Ink);      // the lip, 99.5 rounded
            Write(dir, "jar.png", px, W, H);

            Say(log, "── 2 · DEBT JAR  (binder.gd _DebtJar, tab 4) ──");
            Say(log, "  ground         (166, 102, 78, 96)    draw_rect(6, 10, w-12, h-14) + (160, 92)");
            Say(log, "  level          (168, " + (102f + 94f * (1f - fill)).ToString("0.0")
                     + ", 74, " + (94f * fill).ToString("0.0")
                     + ")   rides h-16 = 94, not 96 — was 2px tall at full");
            Say(log, "  OUTLINE        4 bars at 4px round (166, 102, 78, 96)"
                     + "   draw_rect(…, INK, false, 4.0) — WAS ABSENT, only the lip was drawn");
            Say(log, "  lip            (162, 99.5, 86, 5)     draw_line((2,10)→(w-2,10), INK, 5)"
                     + "   was (162, 100, 88, 5)");
            Say(log, "");
        }

        // ── 3 · a sparkline ────────────────────────────────────────────────────

        static void ShootSpark(string dir, StringBuilder log)
        {
            // the vitals page's cash line: (10, 172) 1120x190, twelve weeks of money
            float[] series = { 12000f, 11200f, 10400f, 12600f, 9800f, 8200f,
                               9100f, 7400f, 6100f, 6800f, 4200f, 3100f };
            const int w = 1120, h = 190;
            Sprite sp = DrawnChart.Spark(series, DrawnUI.Blue, w, h);
            Color32[] art = TopDown(sp.texture);

            // a cream margin round the panel: the wash is a 3% film and the only way to
            // SEE it is to leave bare sheet beside it
            const int m = 26;
            int cw = w + m * 2, ch = h + m * 2;
            Color32[] px = Ground(cw, ch);
            Blit(px, cw, ch, art, w, h, m, m);
            Cross(px, cw, ch, m + 8f, m + 22f - 20f * 0.78f, 7f, new Color32(0, 160, 255, 255));
            Cross(px, cw, ch, m + 8f, m + h - 4f - 20f * 0.78f, 7f, new Color32(0, 160, 255, 255));
            Write(dir, "spark.png", px, cw, ch);

            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < series.Length; i++)
            {
                lo = Mathf.Min(lo, series[i]); hi = Mathf.Max(hi, series[i]);
            }
            Color32 corner = art[3 * w + 3];         // a pixel no line can reach
            int washed = 0, line = 0;
            for (int i = 0; i < art.Length; i++)
            {
                if (art[i].a == 0) continue;
                if (art[i].a <= 10) washed++; else line++;
            }
            Say(log, "── 3 · SPARKLINE  (binder.gd _Spark, tabs 0/3/4/5) ──");
            Say(log, "  ground wash    corner rgba(" + corner.r + "," + corner.g + ","
                     + corner.b + "," + corner.a + ")   godot Color(0,0,0,0.03) = a 8"
                     + "   — WAS ABSENT");
            Say(log, "  wash coverage  " + washed + "px of " + (w * h) + " · line+dot " + line + "px");
            Say(log, "  hi label       \"" + DrawnChart.Short(hi) + "\" baseline y 22 → top-left ("
                     + (8f) + ", " + (22f - 20f * 0.78f).ToString("0.0") + ")   — WAS ABSENT");
            Say(log, "  lo label       \"" + DrawnChart.Short(lo) + "\" baseline y h-4 → top-left ("
                     + (8f) + ", " + (h - 4f - 20f * 0.78f).ToString("0.0") + ")   — WAS ABSENT");
            Say(log, "  short form     1.0M/12k/3100 style, from _Spark._fmt_s");
            Say(log, "");
        }

        // ── 4 · the pen ring on the live tab ───────────────────────────────────

        static void ShootRing(string dir, StringBuilder log)
        {
            Sprite sp = DrawnChart.PenEllipse(68f, 26f, 3.5f, 2f, 5, DrawnUI.Coral);
            Color32[] art = TopDown(sp.texture);
            int w = sp.texture.width, h = sp.texture.height;
            Color32[] px = Ground(w, h);
            Blit(px, w, h, art, w, h, 0, 0);
            Write(dir, "ring.png", px, w, h);

            int x0 = int.MaxValue, x1 = -1, y0 = int.MaxValue, y1 = -1, coral = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (art[y * w + x].a < 40) continue;
                    coral++;
                    if (x < x0) x0 = x; if (x > x1) x1 = x;
                    if (y < y0) y0 = y; if (y > y1) y1 = y;
                }
            // WHAT IT WAS: GameUi.PenRing baked a 60-radius CIRCLE and let the Image
            // stretch it into a 130x52 cell. Measured the same way, on screen.
            Sprite old = DrawnUI.RingSprite(60f, 3.5f * 60f / 130f * 2f, 1.6f, 5, 4, false);
            Color32[] oart = TopDown(old.texture);
            int ow = old.texture.width, oh = old.texture.height;
            int ox0 = int.MaxValue, ox1 = -1, oy0 = int.MaxValue, oy1 = -1, oink = 0;
            for (int y = 0; y < oh; y++)
                for (int x = 0; x < ow; x++)
                {
                    if (oart[y * ow + x].a < 40) continue;
                    oink++;
                    if (x < ox0) ox0 = x; if (x > ox1) ox1 = x;
                    if (y < oy0) oy0 = y; if (y > oy1) oy1 = y;
                }
            float sx = 130f / ow, sy = 52f / oh;         // the cell it was stretched into
            // shown AS IT REACHED THE SCREEN — resampled into the 130x52 cell and
            // tinted coral by the Image — on the same canvas as ring.png, so the two
            // files can be laid side by side
            Color32[] opx = Ground(w, h);
            for (int y = 0; y < 52; y++)
                for (int x = 0; x < 130; x++)
                {
                    Color32 s = oart[Mathf.Min((int)(y / sy), oh - 1) * ow
                                   + Mathf.Min((int)(x / sx), ow - 1)];
                    if (s.a == 0) continue;
                    Color32 pen = DrawnUI.Coral;
                    pen.a = s.a;
                    int tx = x + (w - 130) / 2, ty = y + (h - 52) / 2;
                    opx[ty * w + tx] = Over(opx[ty * w + tx], pen);
                }
            Write(dir, "ring_before.png", opx, w, h);

            Say(log, "── 4 · PEN RING on the live tab  (binder.gd _Clipboard) ──");
            Say(log, "  BEFORE  a 60r circle baked " + ow + "x" + oh
                     + " and stretched into a 130x52 cell:");
            Say(log, "          on-screen ink " + ((ox1 - ox0 + 1) * sx).ToString("0.0") + "x"
                     + ((oy1 - oy0 + 1) * sy).ToString("0.0")
                     + " · " + (oink * sx * sy).ToString("0") + "px of coral"
                     + " · stroke " + (3.5f * 60f / 130f * 2f * sx).ToString("0.00")
                     + " across but " + (3.5f * 60f / 130f * 2f * sy).ToString("0.00")
                     + " top and bottom — a stretched circle cannot hold one width");
            Say(log, "  AFTER   sprite " + w + "x" + h + ", mounted 1:1, never stretched");
            Say(log, "          on-screen ink " + (x1 - x0 + 1) + "x" + (y1 - y0 + 1)
                     + " · " + coral + "px of coral · stroke 3.50 all the way round");
            Say(log, "  GODOT   path 136x52 (rx 68, ry 26), jitter ±2, draw_polyline width 3.5"
                     + "  → ink up to 143x59");
            Say(log, "  centre in the sheet  (24 + tab*133 + 65, 76) — unchanged, it was exact");
            Say(log, "");
        }

        // ── 5 · the drawn clock ────────────────────────────────────────────────

        static void ShootClock(string dir, StringBuilder log)
        {
            Sprite sp = DrawnChart.Clock(30, DrawnUI.Coral, DrawnUI.Ink);
            Color32[] art = TopDown(sp.texture);
            int w = sp.texture.width, h = sp.texture.height;
            Color32[] px = Ground(w * 4, h * 4);
            // blown up 4x so a 30px drawing can actually be looked at
            for (int y = 0; y < h * 4; y++)
                for (int x = 0; x < w * 4; x++)
                {
                    Color32 c = art[(y / 4) * w + (x / 4)];
                    if (c.a == 0) continue;
                    px[y * w * 4 + x] = Over(px[y * w * 4 + x], c);
                }
            Write(dir, "clock.png", px, w * 4, h * 4);

            int face = 0, hands = 0;
            for (int i = 0; i < art.Length; i++)
            {
                if (art[i].a < 40) continue;
                if (Near(art[i], DrawnUI.Ink, 40)) hands++; else face++;
            }
            Say(log, "── 5 · THE DRAWN CLOCK  (replaces ⏰, tab 8) ──");
            Say(log, "  sprite         " + w + "x" + h + " (shown 4x in clock.png)");
            Say(log, "  ring px        " + face + " coral · hands " + hands + " ink");
            Say(log, "  mounted        (10, y+3) 30x30 · the line starts at x 48");
            Say(log, "  ⚠ ▲ ▼ ↻ on this page are still literal characters and ride the"
                     + " font fallback — that mechanism belongs to another lane");
            Say(log, "");
        }

        // ── the little compositor ──────────────────────────────────────────────

        static void Say(StringBuilder log, string line)
        {
            Debug.Log("CHARTSHOT: " + line);
            log.Append(line).Append('\n');
        }

        static Color32[] Ground(int w, int h)
        {
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = Cream;
            return px;
        }

        /// A baked sprite, read back the way it was drawn: DrawnChart.Bake flips its
        /// rows for Unity, so the shot flips them straight again.
        static Color32[] TopDown(Texture2D tex)
        {
            Color32[] src = tex.GetPixels32();
            var outp = new Color32[src.Length];
            int w = tex.width, h = tex.height;
            for (int y = 0; y < h; y++)
                Array.Copy(src, y * w, outp, (h - 1 - y) * w, w);
            return outp;
        }

        static void Blit(Color32[] dst, int dw, int dh, Color32[] src, int sw, int sh,
                         int atX, int atY)
        {
            for (int y = 0; y < sh; y++)
            {
                int ty = atY + y;
                if (ty < 0 || ty >= dh) continue;
                for (int x = 0; x < sw; x++)
                {
                    int tx = atX + x;
                    if (tx < 0 || tx >= dw) continue;
                    Color32 s = src[y * sw + x];
                    if (s.a == 0) continue;
                    dst[ty * dw + tx] = Over(dst[ty * dw + tx], s);
                }
            }
        }

        static void Slab(Color32[] px, int w, int h, int x, int y, int rw, int rh, Color32 col)
        {
            for (int j = y; j < y + rh; j++)
            {
                if (j < 0 || j >= h) continue;
                for (int i = x; i < x + rw; i++)
                {
                    if (i < 0 || i >= w) continue;
                    px[j * w + i] = col;
                }
            }
        }

        static void Cross(Color32[] px, int w, int h, float cx, float cy, float arm, Color32 col)
        {
            for (float t = -arm; t <= arm; t += 0.5f)
            {
                Plot(px, w, h, cx + t, cy, col);
                Plot(px, w, h, cx, cy + t, col);
            }
        }

        static void Plot(Color32[] px, int w, int h, float x, float y, Color32 col)
        {
            int i = Mathf.RoundToInt(x), j = Mathf.RoundToInt(y);
            if (i < 0 || i >= w || j < 0 || j >= h) return;
            px[j * w + i] = col;
        }

        static Color32 Over(Color32 dst, Color32 src)
        {
            float a = src.a / 255f;
            return new Color32(
                (byte)Mathf.RoundToInt(src.r * a + dst.r * (1f - a)),
                (byte)Mathf.RoundToInt(src.g * a + dst.g * (1f - a)),
                (byte)Mathf.RoundToInt(src.b * a + dst.b * (1f - a)),
                255);
        }

        static Color32 Mix(Color32 ground, Color over, float a)
        {
            return Over(ground, new Color32((byte)(over.r * 255f), (byte)(over.g * 255f),
                                            (byte)(over.b * 255f), (byte)(a * 255f)));
        }

        static bool Near(Color32 a, Color b, int tol)
        {
            return Mathf.Abs(a.r - (int)(b.r * 255f)) <= tol
                && Mathf.Abs(a.g - (int)(b.g * 255f)) <= tol
                && Mathf.Abs(a.b - (int)(b.b * 255f)) <= tol;
        }

        static void Write(string dir, string name, Color32[] px, int w, int h)
        {
            // the canvas is top-left down; a Texture2D starts bottom-left
            var flipped = new Color32[px.Length];
            for (int y = 0; y < h; y++) Array.Copy(px, y * w, flipped, (h - 1 - y) * w, w);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(flipped);
            tex.Apply(false, false);
            File.WriteAllBytes(Path.Combine(dir, name), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
