using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Runway.Effects;

namespace Runway.EditorTools
{
    /// <summary>
    /// D4's own evidence. Drives Impulse's spring by hand — no play mode, no window,
    /// no rendering — and writes the sampled offset curve out as CSV plus a verdict
    /// file, so the claim "the stage returns EXACTLY to rest" is a number somebody
    /// else can read rather than a promise.
    ///
    ///   RUNWAY_D4_OUT=/some/dir Unity -batchmode -quit -nographics \
    ///     -projectPath unity -executeMethod Runway.EditorTools.ImpulseProbe.Run
    ///
    /// Output defaults to unity/Logs/d4. The process exits 1 if any check fails, so
    /// this is usable as a gate and not only as a report.
    ///
    /// WHY IT CAN RUN WITHOUT PLAY MODE: Impulse.Step is a pure function of the shot
    /// state and a delta. The shipped driver hands it Time.unscaledDeltaTime; this
    /// hands it a fixed delta, and both produce identical motion — which is itself
    /// one of the checks below (240fps, 60fps and 30fps must agree).
    /// </summary>
    public static class ImpulseProbe
    {
        // the rest pose is deliberately NOT the origin and NOT unit scale: returning
        // to (0,0)x1 would prove nothing about returning to REST.
        static readonly Vector2 RestPos = new Vector2(13.5f, -7.25f);
        static readonly Vector3 RestScale = new Vector3(0.97f, 1.03f, 1f);

        static readonly List<string> _csv = new List<string>();
        static readonly StringBuilder _report = new StringBuilder();
        static int _checks;
        static int _fails;

        public static void Run()
        {
            _csv.Clear();
            _report.Length = 0;
            _checks = 0;
            _fails = 0;
            _csv.Add("case,frame,t_ms,dx_px,dy_px,scale_delta");

            Line("RUNWAY! D4 IMPULSE — spring probe");
            Line("stage rest pose used: pos " + V2(RestPos) + "  scale " + V3(RestScale));
            Line("");

            var rt = MakeRect();
            try
            {
                Impulse.Bind(rt);
                ForceSwitch(null);                    // absent == on
                Check("kill-switch absent means on", Impulse.Enabled);

                Line("── A. THE SHAKE — backfired verdict, 6px / 250ms ──────────────");
                var s240 = Case("shake_240", rt, 1f / 240f, FireShake);
                var s60 = Case("shake_60", rt, 1f / 60f, FireShake);
                var s30 = Case("shake_30", rt, 1f / 30f, FireShake);
                Report("shake @240fps", s240);
                Report("shake @60fps ", s60);
                Report("shake @30fps ", s30);
                FrameRow("shake @30fps, px", s30, false);
                Check("shake peaks at the asked 6px (>=5.5)", s30.PeakPos >= 5.5f);
                Check("shake never exceeds the asked 6px", s30.PeakPos <= 6.0001f);
                Check("shake crosses back through rest (3 swings)", s30.Swings == 3);
                Check("shake stays on its axis (no stray Y)", s30.PeakY == 0f);
                Check("shake does not touch scale", s30.PeakScale == 0f);
                Check("shake amplitude is frame-rate independent",
                      Mathf.Abs(s240.PeakPos - s30.PeakPos) < 0.05f
                      && Mathf.Abs(s60.PeakPos - s30.PeakPos) < 0.05f);
                Check("shake lasts about 250ms at 30fps (8 frames)", s30.Frames == 8);
                Rest("shake @30fps", rt, s30);

                Line("");
                Line("── B. THE PUNCH — die settled, 1.02 / 120ms ───────────────────");
                var p240 = Case("punch_240", rt, 1f / 240f, FirePunch);
                var p60 = Case("punch_60", rt, 1f / 60f, FirePunch);
                var p30 = Case("punch_30", rt, 1f / 30f, FirePunch);
                Report("punch @240fps", p240);
                Report("punch @60fps ", p60);
                Report("punch @30fps ", p30);
                FrameRow("punch @30fps, scale delta", p30, true);
                Check("punch swells to the asked 2% (>=1.9%)", p30.PeakScale >= 0.019f);
                Check("punch never exceeds the asked 2%", p30.PeakScale <= 0.020001f);
                Check("punch never overshoots below rest (critical damping)",
                      p30.MinScale >= -1e-7f);
                Check("punch does not move the stage", p30.PeakPos == 0f);
                Check("punch is frame-rate independent",
                      Mathf.Abs(p240.PeakScale - p30.PeakScale) < 0.0005f
                      && Mathf.Abs(p60.PeakScale - p30.PeakScale) < 0.0005f);
                Check("punch lasts about 120ms at 30fps (4 frames)", p30.Frames == 4);
                Rest("punch @30fps", rt, p30);

                Line("");
                Line("── C. BOTH AT ONCE — the die settles into a backfired week ────");
                var both = Case("shake_and_punch_30", rt, 1f / 30f, FireBoth);
                Report("shake+punch @30fps", both);
                Check("both channels move together",
                      both.PeakPos >= 5.5f && both.PeakScale >= 0.019f);
                Rest("shake+punch @30fps", rt, both);

                Line("");
                Line("── D. RESTRAINT — only one verdict is allowed to move the frame ");
                CheckVerdict("brilliant", false);
                CheckVerdict("fine", false);
                CheckVerdict("risky", false);
                CheckVerdict("backfired", true);
                Impulse.Rest();
                Check("a null verdict is survivable", VerdictSilent(null));

                Line("");
                Line("── E. KILL-SWITCH — RUNWAY_FX_IMPULSE=0 ──────────────────────");
                Snap(rt);
                ForceSwitch("0");
                Check("RUNWAY_FX_IMPULSE=0 reads as off", !Impulse.Enabled);
                Impulse.Shake(Impulse.BackfiredPx, Impulse.BackfiredMs);
                Impulse.Punch(Impulse.DieScale, Impulse.DieMs);
                Impulse.DieSettled();
                Impulse.Verdict("backfired");
                Check("nothing goes live with the switch off", !Impulse.Busy);
                Impulse.Step(1f / 30f);
                Check("the switch-off target is untouched, exactly", AtRest(rt));
                ForceSwitch("1");
                Check("RUNWAY_FX_IMPULSE=1 reads as on", Impulse.Enabled);
                ForceSwitch(null);
                Check("an absent switch reads as on", Impulse.Enabled);

                Line("");
                Line("── F. THE HEADLINE ASSERT ─────────────────────────────────────");
                Snap(rt);
                Impulse.Verdict("backfired");
                Impulse.DieSettled();
                int guard = 4096;
                while (Impulse.Busy && guard-- > 0) Impulse.Step(1f / 30f);
                bool exact = AtRest(rt);
                Line("  after settle:  pos delta = " + R(rt.anchoredPosition.x - RestPos.x)
                     + ", " + R(rt.anchoredPosition.y - RestPos.y));
                Line("                 scale delta = " + R(rt.localScale.x - RestScale.x)
                     + ", " + R(rt.localScale.y - RestScale.y)
                     + ", " + R(rt.localScale.z - RestScale.z));
                Check("position delta after settle is EXACTLY 0", exact);
                Check("the run terminated (no runaway spring)", guard > 0);
            }
            finally
            {
                Impulse.Unbind();
                ForceSwitch(null);
                if (rt != null) UnityEngine.Object.DestroyImmediate(rt.gameObject);
            }

            Line("");
            Line(_fails == 0
                 ? "PASS — " + _checks + "/" + _checks + " checks"
                 : "FAIL — " + _fails + " of " + _checks + " checks failed");

            string dir = OutDir();
            string csvPath = Path.Combine(dir, "impulse_curve.csv");
            string txtPath = Path.Combine(dir, "impulse_probe.txt");
            File.WriteAllText(csvPath, string.Join("\n", _csv.ToArray()) + "\n");
            File.WriteAllText(txtPath, _report.ToString());
            Debug.Log(_report.ToString());
            Debug.Log("D4 PROBE wrote " + csvPath + " and " + txtPath);

            if (_fails > 0) EditorApplication.Exit(1);
        }

        // ══ the cases ══════════════════════════════════════════════════════════

        static void FireShake() { Impulse.Verdict("backfired"); }
        static void FirePunch() { Impulse.DieSettled(); }
        static void FireBoth() { Impulse.DieSettled(); Impulse.Verdict("backfired"); }

        struct Sampled
        {
            public int Frames;
            public float PeakPos, PeakY, PeakScale, MinScale;
            public int Swings;
            public float LastDx, LastDy, LastScale;
            public List<float> Curve;
            public List<float> ScaleCurve;
        }

        /// Fire, then step at a fixed delta until the spring says it is done,
        /// recording every frame. Nothing here reads Time — the delta is the input.
        static Sampled Case(string tag, RectTransform rt, float dt, Action fire)
        {
            Snap(rt);
            var r = new Sampled();
            r.Curve = new List<float>();
            r.ScaleCurve = new List<float>();
            fire();
            if (!Impulse.Busy) return r;

            float t = 0f;
            int sign = 0;
            int guard = 20000;
            while (guard-- > 0)
            {
                Impulse.Step(dt);
                t += dt;
                float dx = rt.anchoredPosition.x - RestPos.x;
                float dy = rt.anchoredPosition.y - RestPos.y;
                float ds = rt.localScale.x / RestScale.x - 1f;
                r.Frames++;
                r.Curve.Add(dx);
                r.ScaleCurve.Add(ds);
                if (Mathf.Abs(dx) > r.PeakPos) r.PeakPos = Mathf.Abs(dx);
                if (Mathf.Abs(dy) > r.PeakY) r.PeakY = Mathf.Abs(dy);
                if (ds > r.PeakScale) r.PeakScale = ds;
                if (ds < r.MinScale) r.MinScale = ds;
                int s = dx > 1e-4f ? 1 : (dx < -1e-4f ? -1 : 0);
                if (s != 0 && s != sign) { r.Swings++; sign = s; }
                _csv.Add(tag + "," + r.Frames.ToString(CultureInfo.InvariantCulture) + ","
                         + F(t * 1000f) + "," + F(dx) + "," + F(dy) + "," + F(ds));
                r.LastDx = dx; r.LastDy = dy; r.LastScale = ds;
                if (!Impulse.Busy) break;
            }
            return r;
        }

        // ══ the checks ═════════════════════════════════════════════════════════

        static void Rest(string what, RectTransform rt, Sampled r)
        {
            Check(what + " lands on rest — position delta EXACTLY 0",
                  r.LastDx == 0f && r.LastDy == 0f);
            Check(what + " lands on rest — scale delta EXACTLY 0",
                  rt.localScale.x == RestScale.x && rt.localScale.y == RestScale.y
                  && rt.localScale.z == RestScale.z);
        }

        static void CheckVerdict(string band, bool shouldMove)
        {
            Impulse.Rest();
            Impulse.Verdict(band);
            bool moved = Impulse.Busy;
            Impulse.Rest();
            Check("verdict \"" + band + "\" " + (shouldMove ? "shakes" : "is silent"),
                  moved == shouldMove);
        }

        static bool VerdictSilent(string band)
        {
            try { Impulse.Verdict(band); } catch (Exception) { return false; }
            bool quiet = !Impulse.Busy;
            Impulse.Rest();
            return quiet;
        }

        static bool AtRest(RectTransform rt)
        {
            return rt.anchoredPosition.x == RestPos.x
                && rt.anchoredPosition.y == RestPos.y
                && rt.localScale.x == RestScale.x
                && rt.localScale.y == RestScale.y
                && rt.localScale.z == RestScale.z;
        }

        static void Check(string what, bool ok)
        {
            _checks++;
            if (!ok) _fails++;
            Line("  [" + (ok ? "ok  " : "FAIL") + "] " + what);
        }

        // ══ plumbing ═══════════════════════════════════════════════════════════

        static RectTransform MakeRect()
        {
            var go = new GameObject("d4-probe-stage", typeof(RectTransform));
            go.hideFlags = HideFlags.HideAndDontSave;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1536f, 1024f);
            rt.anchoredPosition = RestPos;
            rt.localScale = RestScale;
            return rt;
        }

        /// Put the rect back on its rest pose and drop anything in flight, so each
        /// case starts from the same place.
        static void Snap(RectTransform rt)
        {
            Impulse.Rest();
            rt.anchoredPosition = RestPos;
            rt.localScale = RestScale;
        }

        static void ForceSwitch(string value)
        {
            Environment.SetEnvironmentVariable(Impulse.SwitchVar, value);
            Impulse.RefreshSwitch();
        }

        static void Report(string label, Sampled r)
        {
            Line("  " + label + "  frames " + r.Frames
                 + "  peak " + F(r.PeakPos) + "px"
                 + "  peak scale +" + F(r.PeakScale)
                 + "  swings " + r.Swings
                 + "  last " + R(r.LastDx) + "px / " + R(r.LastScale));
        }

        static void FrameRow(string label, Sampled r, bool scale)
        {
            var sb = new StringBuilder("    " + label + ":  ");
            List<float> src = scale ? r.ScaleCurve : r.Curve;
            for (int i = 0; i < src.Count; i++)
            {
                sb.Append(src[i] >= 0f ? "+" : "");
                sb.Append(src[i].ToString(scale ? "0.0000" : "0.000", CultureInfo.InvariantCulture));
                sb.Append("  ");
            }
            Line(sb.ToString());
        }

        static string OutDir()
        {
            string d = Environment.GetEnvironmentVariable("RUNWAY_D4_OUT");
            if (string.IsNullOrEmpty(d))
                d = Path.Combine(Application.dataPath, "../Logs/d4");
            Directory.CreateDirectory(d);
            return d;
        }

        static void Line(string s) { _report.Append(s).Append('\n'); }
        static string F(float v) { return v.ToString("0.000000", CultureInfo.InvariantCulture); }
        static string R(float v) { return v.ToString("R", CultureInfo.InvariantCulture); }
        static string V2(Vector2 v) { return "(" + R(v.x) + ", " + R(v.y) + ")"; }
        static string V3(Vector3 v) { return "(" + R(v.x) + ", " + R(v.y) + ", " + R(v.z) + ")"; }
    }
}
