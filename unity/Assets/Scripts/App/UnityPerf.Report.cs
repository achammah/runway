#if !RUNWAY_FX_UPERF_OFF
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Runway.App
{
    /// <summary>
    /// WHAT THE PROBE WRITES DOWN. `UnityPerf.cs` walks the screens and holds the
    /// stopwatch; this half owns the two tables it leaves behind, so the measuring
    /// and the reporting can be read — and argued with — separately.
    ///
    /// THE E6 JOIN IS MECHANICAL. Every column here names the perf_probe.gd column
    /// it answers, in a legend printed into the file itself, so the side-by-side is
    /// a paste and not a translation:
    ///
    ///   rebuild/s ↔ redraw/s   canvas/s ↔ —        fps ↔ fps
    ///   frame ms  ↔ —          pk ms    ↔ pk prc   draws ↔ draws
    ///   tex MB    ↔ vram MB    alloc MB ↔ stat MB  mono MB ↔ —
    ///   gc/s      ↔ —          objects  ↔ nodes    —     ↔ pk phy (no physics here)
    ///
    /// EVERY NUMBER IS ALLOWED TO SAY n/a. A column that could not be read — the
    /// UGUI rebuild queues under a renamed field, draw calls in a player build,
    /// Profiler counters in a non-development build — prints n/a rather than a zero
    /// that would read as "free".
    /// </summary>
    public sealed partial class UnityPerf
    {
        // ── what the walk fills in ─────────────────────────────────────────────
        readonly List<Row> _rows = new List<Row>();
        readonly List<Leg> _legs = new List<Leg>();
        readonly List<string> _notes = new List<string>();
        double _worstMs;
        string _worstWhere = "";
        float _soakSecs;
        int _soakSwaps;

        /// A finished row of the per-screen table.
        sealed class Row
        {
            public string Label = "";
            public double RebuildPerSec = -1.0;
            public double CanvasPerSec;
            public double Fps;
            public double FrameMs;
            public double PeakMs;
            public double Draws = -1.0;
            public double TexMb = -1.0;
            public double AllocMb;
            public double MonoMb;
            public double GcPerSec;
            public int Objects;
            public string Note = "";
            public string Blame = "";
        }

        /// One screen's share of the soak, summed across every visit it gets.
        sealed class Leg
        {
            public string Name = "";
            public int Visits;
            public float Secs;
            public int Frames;
            public double SumMs;
            public double MaxMs;
            public double BuildMs;      // the worst construction frame across visits
            public int Over50;
            public int Over100;
            public int Doubled;         // E3: frames that took 1.5x the cap period
            public readonly int[] Hist = new int[257];
        }

        void Note(string line)
        {
            if (!string.IsNullOrEmpty(line)) _notes.Add(line);
        }

        Leg LegFor(string name)
        {
            for (int i = 0; i < _legs.Count; i++) if (_legs[i].Name == name) return _legs[i];
            var l = new Leg { Name = name };
            _legs.Add(l);
            return l;
        }

        /// Fold one held leg into its screen's running totals. The construction
        /// frame is lifted OUT of the hitch counts and given its own column: a
        /// screen is built synchronously in one frame here exactly as it is in the
        /// game, so that frame is the honest cost of a swap and not a surprise.
        void Record(string name, Watch w)
        {
            Leg leg = LegFor(name);
            leg.Visits += 1;
            leg.Secs += w.Secs;
            leg.Frames += w.Frames;
            leg.SumMs += w.SumMs;
            leg.Over50 += w.Over50;
            leg.Over100 += w.Over100;
            leg.Doubled += w.Doubled;
            if (w.MaxMs > leg.MaxMs) leg.MaxMs = w.MaxMs;
            if (w.FirstMs > leg.BuildMs) leg.BuildMs = w.FirstMs;
            for (int i = 0; i < w.Hist.Length; i++) leg.Hist[i] += w.Hist[i];
            if (w.MaxMs > _worstMs) { _worstMs = w.MaxMs; _worstWhere = name; }
            if (w.FirstMs > HitchMs)
            {
                if (w.FirstMs > 100.0) leg.Over100 -= 1;
                leg.Over50 -= 1;
            }
        }

        // ══ formatting ═════════════════════════════════════════════════════════

        /// Invariant, always — a French locale would otherwise emit "8,53" into a
        /// markdown table nobody can join.
        static string F(double v, int dp)
        {
            if (v < 0.0) return "n/a";
            return v.ToString("F" + dp.ToString(CultureInfo.InvariantCulture),
                              CultureInfo.InvariantCulture);
        }

        static string I(int v)
        {
            return v < 0 ? "n/a" : v.ToString(CultureInfo.InvariantCulture);
        }

        static double PercentileOf(int[] hist, int frames, double pct)
        {
            if (frames <= 0) return -1.0;
            int want = (int)Math.Ceiling(frames * pct);
            int seen = 0;
            for (int i = 0; i < hist.Length; i++)
            {
                seen += hist[i];
                if (seen >= want) return i;
            }
            return hist.Length - 1;
        }

        string Preamble(string kind)
        {
            var sb = new StringBuilder();
            sb.Append("_").Append(kind).Append(" · ")
              .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
              .Append(" · build ").Append(BuildStamp.Value)
              .Append(" · ").Append(SystemInfo.processorType)
              .Append(" · gfx ").Append(SystemInfo.graphicsDeviceType)
              .Append(Application.isBatchMode ? " (batchmode, no window)" : " (windowed)")
              .Append(" · frame cap ")
              .Append(_capApplied <= 0 ? "off" : _capApplied.ToString(CultureInfo.InvariantCulture))
              .Append(" · keyless: no keys, no art, generator disabled_\n");
            if (Blind())
                sb.Append("\n> **BLIND RUN — nothing was presented.** "
                          + (Application.isBatchMode
                             ? "This is a batchmode run: the device came up and the canvas really "
                               + "did update, but there is no window, so no frame was ever "
                               + "presented and nothing paced the loop. **`fps`, `frame ms` and "
                               + "`draws` are meaningless here** — a `fps` in the thousands is the "
                               + "Update loop spinning, not the game running. Everything CPU-side "
                               + "IS real: `rebuild/s`, `tex MB`, `alloc MB`, `mono MB`, `gc/s`, "
                               + "`objects`, and the `pk ms` spikes that come from texture uploads."
                             : "There is no graphics device at all, so every render number below "
                               + "is a flat zero rather than a measurement.")
                          + " For the fps and draw-call rows, run the built `.app` (or a windowed "
                          + "editor) with the same variables.\n");
            return sb.ToString();
        }

        /// The ten loudest repainters of the window just closed, by rate — the port
        /// of `_report_blame`. Empty unless RUNWAY_UPERF_BLAME=1.
        string TopBlame(float secs)
        {
            if (_blame == null || _blame.Count == 0) return "";
            var keys = new List<string>(_blame.Keys);
            keys.Sort((a, b) => _blame[b].CompareTo(_blame[a]));
            var sb = new StringBuilder();
            int n = Mathf.Min(10, keys.Count);
            for (int i = 0; i < n; i++)
                sb.Append(string.Format(CultureInfo.InvariantCulture,
                    "  - `{0,9:0.0}/s`  {1}\n", _blame[keys[i]] / Mathf.Max(secs, 0.0001f), keys[i]));
            return sb.ToString();
        }

        string BlameBlock()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _rows.Count; i++)
                if (_rows[i].Blame.Length > 0)
                    sb.Append("\n- **").Append(_rows[i].Label).Append("**\n").Append(_rows[i].Blame);
            if (sb.Length == 0) return "";
            return "\n**Who is repainting** (`RUNWAY_UPERF_BLAME=1`) — the element, where it "
                 + "lives, and how many times a second it asked to be rebuilt.\n" + sb + "\n";
        }

        string Notes()
        {
            if (_notes.Count == 0) return "";
            var sb = new StringBuilder("\n**What the probe could not measure**\n\n");
            for (int i = 0; i < _notes.Count; i++) sb.Append("- ").Append(_notes[i]).Append("\n");
            return sb.ToString();
        }

        // ══ the per-screen table ═══════════════════════════════════════════════

        string TableMd()
        {
            var sb = new StringBuilder();
            sb.Append("# RUNWAY! Unity — energy probe\n\n");
            sb.Append(Preamble(string.Format(CultureInfo.InvariantCulture,
                "{0:0.0}s settle + {1:0.0}s watch per screen", Settle, Window)));
            sb.Append("\n| screen | rebuild/s | canvas/s | fps | frame ms | pk ms | draws "
                      + "| tex MB | alloc MB | mono MB | gc/s | objects | note |\n");
            sb.Append("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|\n");
            for (int i = 0; i < _rows.Count; i++)
            {
                Row r = _rows[i];
                sb.Append("| ").Append(r.Label)
                  .Append(" | ").Append(F(r.RebuildPerSec, 1))
                  .Append(" | ").Append(F(r.CanvasPerSec, 1))
                  .Append(" | ").Append(F(r.Fps, 1))
                  .Append(" | ").Append(F(r.FrameMs, 2))
                  .Append(" | ").Append(F(r.PeakMs, 2))
                  .Append(" | ").Append(F(r.Draws, 0))
                  .Append(" | ").Append(F(r.TexMb, 1))
                  .Append(" | ").Append(F(r.AllocMb, 1))
                  .Append(" | ").Append(F(r.MonoMb, 1))
                  .Append(" | ").Append(F(r.GcPerSec, 2))
                  .Append(" | ").Append(I(r.Objects))
                  .Append(" | ").Append(r.Note)
                  .Append(" |\n");
            }
            sb.Append(BlameBlock());
            sb.Append(Legend());
            sb.Append(Notes());
            return sb.ToString();
        }

        static string Legend()
        {
            return "\n**The columns, and the perf_probe.gd column each one answers**\n\n"
                 + "| this table | Godot twin | what it is |\n|---|---|---|\n"
                 + "| rebuild/s | redraw/s | UI elements queued for a layout or graphic "
                 + "rebuild, per second. A 12fps baked loop writes one uvRect per baked frame "
                 + "and queues NOTHING, so anything here is a real repaint. |\n"
                 + "| canvas/s | — | Canvas.willRenderCanvases firings per second: the canvas "
                 + "update tick, which tracks fps. It says the loop ran, not that work happened. |\n"
                 + "| fps | fps | presented frames per second. Headroom, not cost. |\n"
                 + "| frame ms | — | mean Time.unscaledDeltaTime. Only meaningful with the frame "
                 + "cap OFF, which is why this table runs uncapped. |\n"
                 + "| pk ms | pk prc | the worst single frame in the window — the spike gauge. |\n"
                 + "| draws | draws | UnityStats.drawCalls, sampled 4x/s. Editor only: a player "
                 + "build reads n/a. |\n"
                 + "| tex MB | vram MB | Texture.currentTextureMemory while this screen is up. |\n"
                 + "| alloc MB | stat MB | Profiler.GetTotalAllocatedMemoryLong. Reads 0 in a "
                 + "non-development player build. |\n"
                 + "| mono MB | — | Profiler.GetMonoUsedSizeLong — the managed heap in use. |\n"
                 + "| gc/s | — | System.GC.CollectionCount(0..2) deltas per second. |\n"
                 + "| objects | nodes | live Transforms in the whole run. |\n"
                 + "| — | pk phy | no twin: this port runs no physics at all. |\n"
                 + "\nThe `floor after` rows are taken with a bare stage. A floor that keeps "
                 + "climbing is a screen that never gave its sheets back. The small drawn "
                 + "sprites are held for the session on purpose (ArtCache); run again with "
                 + "`RUNWAY_UPERF_UNLOAD=1` to sweep before each floor and separate "
                 + "held-on-purpose from leaked.\n";
        }

        // ══ the hitch hunt ═════════════════════════════════════════════════════

        string SoakMd()
        {
            var sb = new StringBuilder();
            sb.Append("\n\n# RUNWAY! Unity — 10-minute hitch hunt\n\n");
            sb.Append(Preamble(string.Format(CultureInfo.InvariantCulture,
                "{0:0}s scripted sit · {1} screen swaps", _soakSecs, _soakSwaps)));
            sb.Append("\n| screen | visits | secs | frames | avg ms | p99 ms | max ms | "
                      + "build ms | >50ms | >100ms | doubled |\n");
            sb.Append("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|\n");

            int frames = 0, over50 = 0, over100 = 0, doubled = 0;
            double sumMs = 0.0, maxBuild = 0.0;
            float secs = 0f;
            var all = new int[257];
            for (int i = 0; i < _legs.Count; i++)
            {
                Leg l = _legs[i];
                int f = Mathf.Max(l.Frames, 1);
                sb.Append("| ").Append(l.Name)
                  .Append(" | ").Append(I(l.Visits))
                  .Append(" | ").Append(F(l.Secs, 0))
                  .Append(" | ").Append(I(l.Frames))
                  .Append(" | ").Append(F(l.SumMs / f, 2))
                  .Append(" | ").Append(F(PercentileOf(l.Hist, l.Frames, 0.99), 0))
                  .Append(" | ").Append(F(l.MaxMs, 2))
                  .Append(" | ").Append(F(l.BuildMs, 2))
                  .Append(" | ").Append(I(Mathf.Max(l.Over50, 0)))
                  .Append(" | ").Append(I(Mathf.Max(l.Over100, 0)))
                  .Append(" | ").Append(CapFrameMs > 0.0 ? I(l.Doubled) : "n/a")
                  .Append(" |\n");
                frames += l.Frames;
                secs += l.Secs;
                sumMs += l.SumMs;
                over50 += Mathf.Max(l.Over50, 0);
                over100 += Mathf.Max(l.Over100, 0);
                doubled += l.Doubled;
                if (l.BuildMs > maxBuild) maxBuild = l.BuildMs;
                for (int b = 0; b < all.Length; b++) all[b] += l.Hist[b];
            }
            sb.Append("| **all** | ").Append(I(_soakSwaps))
              .Append(" | ").Append(F(secs, 0))
              .Append(" | ").Append(I(frames))
              .Append(" | ").Append(F(sumMs / Mathf.Max(frames, 1), 2))
              .Append(" | ").Append(F(PercentileOf(all, frames, 0.99), 0))
              .Append(" | ").Append(F(_worstMs, 2))
              .Append(" | ").Append(F(maxBuild, 2))
              .Append(" | ").Append(I(over50))
              .Append(" | ").Append(I(over100))
              .Append(" | ").Append(CapFrameMs > 0.0 ? I(doubled) : "n/a")
              .Append(" |\n\n");

            sb.Append(over50 == 0
                ? "**HITCH-FREE while sitting.** No frame past 50ms once a screen was up.\n"
                : string.Format(CultureInfo.InvariantCulture,
                    "**{0} frame(s) past 50ms while sitting**, worst {1:0.0}ms on `{2}`. "
                    + "Every one of them is a miss against the award bar.\n",
                    over50, _worstMs, _worstWhere));
            sb.Append(string.Format(CultureInfo.InvariantCulture,
                "\n**build ms** is the single frame that CONSTRUCTS the screen, counted apart "
                + "from the sitting frames: worst {0:0.0}ms across {1} swaps. A screen is built "
                + "synchronously in one frame here exactly as it is in the game, so this is the "
                + "real cost of a swap — measured, not hidden.\n", maxBuild, _soakSwaps));
            if (CapFrameMs > 0.0)
                sb.Append(string.Format(CultureInfo.InvariantCulture,
                    "\n**doubled** is E3, frame pacing: frames that took more than 1.5x the "
                    + "{0:0.0}ms cap period, i.e. a two-frame stutter rather than a hitch. "
                    + "{1} of {2} frames.\n", CapFrameMs, doubled, frames));
            sb.Append(Notes());
            return sb.ToString();
        }
    }
}
#endif
