using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;

namespace Runway.Effects
{
    /// <summary>
    /// THE ROOM PAINTS ITSELF IN. When the director delivers a room, the old swap
    /// cross-faded it over the cream wall in 0.4s — a dissolve, which is the one
    /// transition a hand-drawn world cannot afford. This lays the same picture down
    /// the way it was made: fourteen broad brush strokes, in a painter's order,
    /// over one second on the game's own 12fps drawn clock.
    ///
    /// HOW IT WORKS — no shader, no stencil, no full-res compositing.
    /// The painting is whole and opaque from frame one. What animates is a COVER
    /// laid over it: a full-rect RawImage tinted the room's own cream, whose ALPHA
    /// comes from one of twelve tiny 192x128 masks. Each mask is the same brush-time
    /// field cut at a later moment, so swapping the cover's texture erodes the cream
    /// away in stroke shapes. Bilinear upscaling to 1536x1024 does the softening a
    /// brush edge wants for free.
    ///
    /// The cover is a CHILD of the room image, so the room's floor tint and horizon
    /// rule — which are siblings drawn after it — stay exactly where they were for
    /// the whole reveal. Nothing pops on either end.
    ///
    /// COST. The twelve masks are the same every time (they do not depend on the
    /// picture), so they are built once per session and held: 12 x 192 x 128 RGBA
    /// = 1.2MB. Playback is one `texture =` assignment every 83ms and nothing else,
    /// so there is no per-frame allocation once the reveal has started.
    ///
    /// KILL-SWITCH. Runtime environment variable RUNWAY_FX_REVEAL: absent or "1"
    /// paints; "0" restores the old behaviour exactly (texture swap + 0.4s fade).
    /// </summary>
    public static class InkReveal
    {
        // ── the contract ───────────────────────────────────────────────────────

        /// absent / "1" → the room paints · "0" → the old cross-fade, unchanged
        public const string Switch = "RUNWAY_FX_REVEAL";

        /// how many cut masks the reveal steps through
        public const int Frames = 12;

        /// the whole reveal, start to finish. 12 steps / 1.0s == the 12fps the room
        /// already breathes on, so this moves like everything else drawn.
        public const float Seconds = 1.0f;

        public const int MaskW = 192;
        public const int MaskH = 128;

        /// the child the reveal builds on the room image
        public const string CoverName = "inkcover";

        /// what the cover is painted in — the garage wall's own cream, so the parts
        /// not yet painted are indistinguishable from the paper behind the room
        public static Color CoverColor = DrawnUI.Cream;

        // ── how a stroke behaves ───────────────────────────────────────────────

        /// the wet edge, in time units. 0.010 of a 1.0s reveal is about 1.6 mask
        /// pixels of ramp — a brush edge, not an airbrush.
        const float Feather = 0.010f;

        /// every time is clamped below this so the LAST cut is guaranteed empty and
        /// the cover can be torn down without a pop
        const float MaxTime = 1f - Feather - 0.002f;

        /// the bristle spreads outward from the spine rather than landing flat
        const float EdgeLag = 0.010f;

        /// how fast paint creeps into a gap no stroke reached
        const float CreepStep = 0.0035f;

        const float Never = 2f;
        const int Seed = 27;

        /// how long the reveal ignores input — two of the room's own 12fps frames
        const float Deaf = 0.18f;

        static Texture2D[] _masks;
        static int _gen;

        // ══ the entry points ═══════════════════════════════════════════════════

        /// The kill-switch, read live so a harness can flip it between runs.
        public static bool Enabled
        {
            get
            {
                string v = Env.Get(Switch, "1");
                if (v == null) return true;
                v = v.Trim().ToLowerInvariant();
                return !(v == "0" || v == "off" || v == "false" || v == "no");
            }
        }

        /// PUT THIS PICTURE IN THIS IMAGE, AND PAINT IT IN. Safe to call again while
        /// a previous reveal is still running: the old cover is torn down and its
        /// coroutine retires on the generation counter.
        public static void Begin(RawImage roomImage, Texture2D newRoom)
        {
            if (roomImage == null) return;
            if (newRoom != null) roomImage.texture = newRoom;
            roomImage.enabled = true;
            if (!Enabled) { Instant(roomImage, newRoom); return; }

            MonoBehaviour host = Host(roomImage);
            if (host == null) { Instant(roomImage, newRoom); return; }

            RawImage cover = Attach(roomImage, newRoom);
            if (cover == null) { Instant(roomImage, newRoom); return; }
            host.StartCoroutine(Play(cover, Seconds, _gen));
        }

        /// THE OLD BEHAVIOUR, KEPT WHOLE — what the room did before this lane existed:
        /// the picture goes in and cross-fades up over 0.4s. This is what the
        /// kill-switch buys, so turning the effect off is a true no-op.
        public static void Instant(RawImage roomImage, Texture2D newRoom)
        {
            if (roomImage == null) return;
            if (newRoom != null) roomImage.texture = newRoom;
            roomImage.enabled = true;
            ClearCover(roomImage);
            CanvasGroup g = DrawnUI.Group(roomImage.rectTransform);
            MonoBehaviour host = Host(roomImage);
            if (host == null) { g.alpha = 1f; return; }
            g.alpha = 0f;
            host.StartCoroutine(DrawnUI.FadeTo(g, 1f, 0.4f));
        }

        /// Build the cover and put the picture in, WITHOUT starting a clock. The
        /// runtime path uses this and then plays it; the editor film uses this and
        /// steps it by hand, so both film the same pixels.
        public static RawImage Attach(RawImage roomImage, Texture2D newRoom)
        {
            if (roomImage == null) return null;
            if (newRoom != null) roomImage.texture = newRoom;
            roomImage.enabled = true;
            // the painting is whole from the first frame — the cover is what moves
            DrawnUI.Group(roomImage.rectTransform).alpha = 1f;

            ClearCover(roomImage);
            _gen++;

            RectTransform rt = DrawnUI.FullRect(roomImage.rectTransform, CoverName);
            var cover = rt.gameObject.AddComponent<RawImage>();
            cover.raycastTarget = false;       // a click skips, it never lands here
            cover.color = CoverColor;
            cover.texture = Masks()[0];
            return cover;
        }

        /// Show one cut. Frame 0 is the first stroke landing; frame Frames-1 is empty.
        public static void Step(RawImage cover, int frame)
        {
            if (cover == null) return;
            Texture2D[] m = Masks();
            cover.texture = m[Mathf.Clamp(frame, 0, m.Length - 1)];
        }

        /// The whole reveal. After the first frame this allocates nothing: the masks
        /// are held, the clock is a float, and the only work is one texture swap
        /// every 1/12 of a second.
        public static IEnumerator Play(RawImage cover, float seconds, int gen)
        {
            Texture2D[] masks = Masks();
            float per = Mathf.Max(seconds, 0.01f) / Frames;
            float t = 0f;
            int shown = -1;
            while (true)
            {
                if (cover == null || gen != _gen) yield break;
                int k = Mathf.FloorToInt(t / per);
                // DEAF FOR TWO FRAMES. A late render lands the same instant the click
                // that closed the beat is still down; without this the reveal would be
                // skipped by the very press that asked for it. Same guard the binder
                // uses against its own opening key.
                if (t > Deaf && Skipped()) k = Frames - 1;
                if (k > Frames - 1) k = Frames - 1;
                if (k < 0) k = 0;
                if (k != shown)
                {
                    shown = k;
                    cover.texture = masks[k];
                }
                if (k >= Frames - 1) break;
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (cover != null) UnityEngine.Object.Destroy(cover.gameObject);
        }

        /// CLICK = DONE. The room being painted is an authored second, not a wait.
        static bool Skipped()
        {
            return Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)
                   || Input.anyKeyDown;
        }

        static MonoBehaviour Host(RawImage roomImage)
        {
            if (roomImage != null && roomImage.isActiveAndEnabled) return roomImage;
            if (Boot.Instance != null) return Boot.Instance;
            return null;
        }

        static void ClearCover(RawImage roomImage)
        {
            if (roomImage == null) return;
            Transform t = roomImage.transform;
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                Transform c = t.GetChild(i);
                if (c == null || c.name != CoverName) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(c.gameObject);
                else UnityEngine.Object.DestroyImmediate(c.gameObject);
            }
        }

        // ══ the masks ══════════════════════════════════════════════════════════

        /// The twelve cuts, built once and held for the session — they are the same
        /// strokes whatever the picture is, so a weekly repaint pays nothing.
        public static Texture2D[] Masks()
        {
            if (_masks != null && _masks.Length == Frames && _masks[0] != null) return _masks;
            Build();
            return _masks;
        }

        /// Drop the held masks — for a harness that wants to rebuild after tuning.
        public static void Forget()
        {
            if (_masks == null) return;
            for (int i = 0; i < _masks.Length; i++)
            {
                if (_masks[i] == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(_masks[i]);
                else UnityEngine.Object.DestroyImmediate(_masks[i]);
            }
            _masks = null;
        }

        static void Build()
        {
            int n = MaskW * MaskH;
            var time = new float[n];
            var laid = new byte[n];
            for (int i = 0; i < n; i++) time[i] = Never;

            var rng = new System.Random(Seed);
            Stroke[] strokes = Order;
            for (int s = 0; s < strokes.Length; s++)
            {
                float t0 = (float)s / strokes.Length;
                float t1 = (float)(s + 1) / strokes.Length;
                Lay(time, strokes[s], t0, t1, rng);
            }
            for (int i = 0; i < n; i++) if (time[i] < Never) laid[i] = 1;

            Creep(time, laid);
            Pace(time);
            Rough(time);

            _masks = new Texture2D[Frames];
            for (int k = 0; k < Frames; k++) _masks[k] = Cut(time, (k + 1f) / Frames);
        }

        // ── the strokes, in the order a hand would lay them ────────────────────
        //
        // Normalised TOP-LEFT coordinates (the coordinates every .gd file is in):
        // A and C are the ends, B pulls the spine into a curve, W is the brush width
        // as a fraction of the canvas height. The ORDER is the whole effect — a
        // confident diagonal, its cross, then blocking in, then the edges, then the
        // last dabs. Any order that walks one way across the canvas is a wipe.

        struct Stroke
        {
            public float Ax, Ay, Bx, By, Cx, Cy, W;
            public Stroke(float ax, float ay, float bx, float by, float cx, float cy, float w)
            {
                Ax = ax; Ay = ay; Bx = bx; By = by; Cx = cx; Cy = cy; W = w;
            }
        }

        static readonly Stroke[] Order =
        {
            //          A                B                C            width
            new Stroke(0.02f, 0.78f,  0.50f, 0.52f,  0.98f, 0.30f,  0.30f), //  1 the first sweep
            new Stroke(0.10f, 0.06f,  0.50f, 0.42f,  0.92f, 0.88f,  0.28f), //  2 the cross
            new Stroke(0.04f, 0.99f,  0.38f, 0.86f,  0.74f, 0.70f,  0.28f), //  3 block in low
            new Stroke(0.30f, 0.10f,  0.64f, 0.18f,  0.99f, 0.09f,  0.26f), //  4 block in high
            new Stroke(0.99f, 0.54f,  0.58f, 0.62f,  0.14f, 0.44f,  0.28f), //  5 back across
            new Stroke(0.05f, 0.28f,  0.24f, 0.44f,  0.46f, 0.66f,  0.26f), //  6 down the left
            new Stroke(0.97f, 0.95f,  0.60f, 0.97f,  0.22f, 0.90f,  0.26f), //  7 along the floor
            new Stroke(0.02f, 0.12f,  0.06f, 0.38f,  0.03f, 0.64f,  0.24f), //  8 the left edge
            new Stroke(0.99f, 0.04f,  0.96f, 0.34f,  0.99f, 0.68f,  0.24f), //  9 the right edge
            new Stroke(0.18f, 0.02f,  0.50f, 0.07f,  0.82f, 0.02f,  0.22f), // 10 the top edge
            new Stroke(0.14f, 0.99f,  0.50f, 0.94f,  0.88f, 0.99f,  0.22f), // 11 the bottom edge
            new Stroke(0.66f, 0.28f,  0.82f, 0.52f,  0.97f, 0.78f,  0.24f), // 12 the far corner
            new Stroke(0.34f, 0.34f,  0.54f, 0.48f,  0.74f, 0.56f,  0.24f), // 13 back to the middle
            new Stroke(0.02f, 0.46f,  0.34f, 0.72f,  0.68f, 0.92f,  0.24f), // 14 the last dab
        };

        /// One stroke, stamped down the spine so the brush TRAVELS: the pixel time
        /// runs from the stroke's own start to its own end, which is why a cut lands
        /// mid-stroke and reads as a hand moving rather than a shape appearing.
        static void Lay(float[] time, Stroke st, float t0, float t1, System.Random rng)
        {
            Vector2 a = ToMask(st.Ax, st.Ay);
            Vector2 b = ToMask(st.Bx, st.By);
            Vector2 c = ToMask(st.Cx, st.Cy);
            float half = st.W * MaskH * 0.5f;
            float len = Vector2.Distance(a, b) + Vector2.Distance(b, c);
            int steps = Mathf.Max(Mathf.CeilToInt(len * 1.5f), 12);
            float phase = (float)rng.NextDouble() * 6.2831853f;

            for (int i = 0; i <= steps; i++)
            {
                float u = (float)i / steps;
                Vector2 p = Bez(a, b, c, u);
                p.x += Jit(rng, 0.7f);
                p.y += Jit(rng, 0.7f);
                float taper = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 0.35f);
                float r = half * (0.22f + 0.78f * taper) * (1f + 0.10f * Mathf.Sin(u * 14f + phase));
                Stamp(time, p.x, p.y, r, Mathf.Lerp(t0, t1, u));
            }
        }

        static Vector2 ToMask(float nx, float ny)
        {
            // authored top-left down; the texture runs bottom-left up. Flipped once,
            // here, exactly like DrawnUI.Bake flips the ink canvas once.
            return new Vector2(nx * MaskW, (1f - ny) * MaskH);
        }

        static Vector2 Bez(Vector2 a, Vector2 b, Vector2 c, float u)
        {
            float k = 1f - u;
            return a * (k * k) + b * (2f * k * u) + c * (u * u);
        }

        static float Jit(System.Random rng, float amount)
        {
            return (float)(rng.NextDouble() * 2.0 - 1.0) * amount;
        }

        /// One dab. Earliest time wins — once a pixel is painted it stays painted,
        /// so a later stroke crossing an earlier one never re-opens it.
        static void Stamp(float[] time, float cx, float cy, float r, float t)
        {
            if (r <= 0f) return;
            int x0 = Mathf.Max(Mathf.FloorToInt(cx - r), 0);
            int x1 = Mathf.Min(Mathf.CeilToInt(cx + r), MaskW - 1);
            int y0 = Mathf.Max(Mathf.FloorToInt(cy - r), 0);
            int y1 = Mathf.Min(Mathf.CeilToInt(cy + r), MaskH - 1);
            float rr = r * r;
            for (int y = y0; y <= y1; y++)
            {
                int row = y * MaskW;
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - cx;
                    float dy = y + 0.5f - cy;
                    float d2 = dx * dx + dy * dy;
                    if (d2 > rr) continue;
                    float v = t + EdgeLag * (d2 / rr);
                    int i = row + x;
                    if (v < time[i]) time[i] = v;
                }
            }
        }

        /// PAINT CREEPS INTO WHAT THE BRUSH MISSED. A gap no stroke reached takes the
        /// time of its nearest painted neighbour plus a little, so a hole fills from
        /// its own rim outward instead of appearing whole at the end. Two chamfer
        /// sweeps each way; authored pixels are never touched.
        static void Creep(float[] time, byte[] laid)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                for (int y = 0; y < MaskH; y++)
                    for (int x = 0; x < MaskW; x++)
                        Relax(time, laid, x, y, -1, 0, 0, -1);
                for (int y = MaskH - 1; y >= 0; y--)
                    for (int x = MaskW - 1; x >= 0; x--)
                        Relax(time, laid, x, y, 1, 0, 0, 1);
            }
            for (int i = 0; i < time.Length; i++)
                if (time[i] >= Never) time[i] = MaxTime;
        }

        static void Relax(float[] time, byte[] laid, int x, int y,
                          int ax, int ay, int bx, int by)
        {
            int i = y * MaskW + x;
            if (laid[i] != 0) return;
            float best = time[i];
            float n = At(time, x + ax, y + ay);
            if (n + CreepStep < best) best = n + CreepStep;
            n = At(time, x + bx, y + by);
            if (n + CreepStep < best) best = n + CreepStep;
            time[i] = best;
        }

        static float At(float[] time, int x, int y)
        {
            if (x < 0 || y < 0 || x >= MaskW || y >= MaskH) return Never;
            return time[y * MaskW + x];
        }

        /// EQUAL PAINT PER FRAME. The broad opening strokes cover most of the canvas
        /// before the narrow ones get their turn, so an even time slice per stroke
        /// lays nine tenths of the room in half a second and then idles — which reads
        /// as a stall, and a stall is the one thing an authored second cannot have.
        /// This re-spaces every moment through the field's own distribution, so each
        /// twelfth of the second paints a twelfth of the room. The remap is strictly
        /// increasing, so no pixel ever changes its place in the order: the strokes
        /// and their travel are untouched, only the pacing is.
        static void Pace(float[] time)
        {
            const int Bins = 1024;
            var hist = new int[Bins];
            for (int i = 0; i < time.Length; i++) hist[Bin(time[i])]++;

            var cdf = new float[Bins];
            int run = 0;
            for (int b = 0; b < Bins; b++)
            {
                run += hist[b];
                cdf[b] = (float)run / time.Length;
            }
            for (int i = 0; i < time.Length; i++)
            {
                int b = Bin(time[i]);
                float lo = b == 0 ? 0f : cdf[b - 1];
                float f = time[i] * Bins - b;      // ordering INSIDE a bin survives too
                time[i] = (lo + (cdf[b] - lo) * f) * MaxTime;
            }
        }

        static int Bin(float t)
        {
            int b = (int)(t * 1024);
            if (b < 0) return 0;
            if (b > 1023) return 1023;
            return b;
        }

        /// THE BRISTLES. Five incommensurate waves push each pixel's moment a little
        /// early or late, so the wet edge is ragged at several scales instead of being
        /// a clean contour, and a stroke's tip breaks up the way a dry brush does.
        /// Amplitude is about a third of a frame.
        static void Rough(float[] time)
        {
            for (int y = 0; y < MaskH; y++)
            {
                int row = y * MaskW;
                for (int x = 0; x < MaskW; x++)
                {
                    float n = Mathf.Sin(x * 0.29f + y * 0.17f) * 0.40f
                            + Mathf.Sin(x * 0.11f - y * 0.43f) * 0.28f
                            + Mathf.Sin(x * 0.71f + y * 0.59f) * 0.16f
                            + Mathf.Sin(x * 1.31f - y * 1.07f) * 0.10f
                            + Mathf.Sin(x * 2.17f + y * 1.93f) * 0.06f;
                    int i = row + x;
                    time[i] = Mathf.Clamp(time[i] + n * 0.034f, 0f, MaxTime);
                }
            }
        }

        /// One cut of the time field. Alpha 255 is cream still on the glass; alpha 0
        /// is painting. White RGB throughout so the bilinear blow-up to full stage
        /// never drags a dark fringe across the wet edge.
        static Texture2D Cut(float[] time, float cutoff)
        {
            var px = new Color32[MaskW * MaskH];
            for (int i = 0; i < px.Length; i++)
            {
                float a = Mathf.Clamp01((time[i] - cutoff) / Feather);
                px[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
            var tex = new Texture2D(MaskW, MaskH, TextureFormat.RGBA32, false);
            tex.name = "inkmask";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }
    }
}
