using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;
using Runway.Game;

namespace Runway.EditorTools
{
    /// <summary>
    /// THE ROUTED TYPE, PHOTOGRAPHED. One frame of the CHOOSE YOUR FOUNDER region built
    /// through the SHIPPING calls — `FounderDraftScreen.Heading`, `GameUi.StatPips`,
    /// `GameUi.TraitPips`, `DrawnUI.PaperButton` — so which hand each element ended up
    /// in can be looked at rather than asserted about.
    ///
    /// WHY A SINGLE FRAME AND NOT A LIST OF ASSERTS. "Is this Baloo2?" is a question a
    /// pixel answers in a glance and a boolean answers badly: Patrick Hand and Baloo2
    /// Bold both resolve, both draw, and a wrong route is a screen that is quietly a
    /// fifth too narrow rather than a screen that fails. The frame carries its own
    /// control: the same heading is drawn TWICE, once through the shipping path and
    /// once the way it read before this pass, each under a rule measured in its own
    /// hand, with a coral tick at the width Godot's own probe reported.
    ///
    /// WHAT IS IN THE FRAME
    ///   · the page heading, display hand, under a rule measured on the display hand
    ///   · the same heading in the writing hand under a hand-measured rule (the before)
    ///   · the founder's sheet: name (display) over tagline (hand), five stat rows
    ///     (display labels), HIDDEN TRAITS (display) beside its hint (hand), six trait
    ///     rows (display names, hand deltas), the day-one number (display) under its
    ///     caption (hand)
    ///   · the draft's LOCK IN card (display caption, draft paper) beside the title
    ///     screen's card (writing hand, title paper) — both hands and both papers
    ///
    /// Runs headless WITH a graphics device — leave `-nographics` OFF or nothing
    /// rasterises:
    ///
    ///   Unity -batchmode -quit -projectPath unity \
    ///         -executeMethod Runway.EditorTools.RouteShot.Run
    ///
    /// Output goes to $RUNWAY_ROUTE_OUT (default /tmp/d-route).
    /// </summary>
    public static class RouteShot
    {
        const int W = 1536;
        const int H = 1024;

        /// What the Godot probe measured for the page heading, in each hand — the same
        /// two numbers `TypeShot` ticks against.
        const float GodotHeadDisplay = 639f;   // "CHOOSE YOUR FOUNDER" @58, Baloo2 Bold
        const float GodotHeadHand = 515f;      // the same words in Patrick Hand

        static readonly StringBuilder _log = new StringBuilder();

        public static void Run()
        {
            string dir = Environment.GetEnvironmentVariable("RUNWAY_ROUTE_OUT");
            if (string.IsNullOrEmpty(dir)) dir = "/tmp/d-route";
            Directory.CreateDirectory(dir);

            Say("RUNWAY! ROUTE · which hand each element ended up in · "
                + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Say("unity " + Application.unityVersion + " · batchmode=" + Application.isBatchMode
                + " · graphics=" + SystemInfo.graphicsDeviceType);
            Say("out: " + dir);
            Say("");

            // the bake normally lands on a delayCall the editor never reaches under
            // -batchmode -quit, and a font resolved before it caches the runtime build
            try { FontBaker.BakeAll(); }
            catch (Exception e) { Say("bake pass: " + e.Message); }

            GameObject rig = null;
            RenderTexture rt = null;
            try
            {
                Camera cam;
                RectTransform stage;
                Rig(out rig, out cam, out rt, out stage);

                Faces();
                Widths();

                Frame(stage);
                Canvas.ForceUpdateCanvases();
                Capture(cam, rt, Path.Combine(dir, "route-select.png"));
            }
            catch (Exception e)
            {
                Say("FAILED: " + e);
            }
            finally
            {
                if (rt != null)
                {
                    RenderTexture.active = null;
                    rt.Release();
                    UnityEngine.Object.DestroyImmediate(rt);
                }
                if (rig != null) UnityEngine.Object.DestroyImmediate(rig);
            }

            try { File.WriteAllText(Path.Combine(dir, "measurements.txt"), _log.ToString()); }
            catch (Exception) { }
        }

        static void Say(string line)
        {
            Debug.Log("ROUTESHOT: " + line);
            _log.Append(line).Append('\n');
        }

        static string N(float v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }

        // ══ the numbers that travel with the picture ═══════════════════════════

        static void Faces()
        {
            Face("hand   ", DrawnUI.Hand);
            Face("display", DrawnUI.Display);
            // a display font that fell through to the hand is a frame where EVERY route
            // below looks correct and none of them did anything — say so out loud
            if (DrawnUI.Display != null && DrawnUI.Display == DrawnUI.Hand)
                Say("  !! THE DISPLAY HAND FELL BACK TO THE WRITING ONE — the picture "
                    + "below proves nothing about routing, only that nothing crashed");
            Say("");
        }

        static void Face(string what, TMP_FontAsset f)
        {
            if (f == null) { Say(what + ": NONE"); return; }
            var fi = f.faceInfo;
            Say(what + ": " + f.name + " · pointSize " + N(fi.pointSize)
                + " ascent " + N(fi.ascentLine / Mathf.Max(fi.pointSize, 1f)) + "x"
                + " · " + f.characterTable.Count + " characters");
        }

        static void Widths()
        {
            Say("the heading, measured in both hands, against Godot's own probe");
            float d = DrawnUI.MeasureWidth("CHOOSE YOUR FOUNDER", 58f, DrawnUI.Display);
            float h = DrawnUI.MeasureWidth("CHOOSE YOUR FOUNDER", 58f);
            Say("  display -> " + N(d) + "  godot " + N(GodotHeadDisplay)
                + "  err " + N(d - GodotHeadDisplay) + "px");
            Say("  hand    -> " + N(h) + "  godot " + N(GodotHeadHand)
                + "  err " + N(h - GodotHeadHand) + "px");
            Say("  the rule under this heading was cut to the HAND's width before this "
                + "pass: " + N(d - h) + "px short of the word it underlines");
            Say("");
            Say("the trait rule — TraitPips measures the word it just wrote, in the "
                + "hand it wrote it in");
            string[] words = { "PARANOIA", "LUCK", "CHARISMA" };
            for (int i = 0; i < words.Length; i++)
                Say("  " + words[i].PadRight(9) + " display " + N(DrawnUI.MeasureWidth(words[i], 19f, DrawnUI.Display))
                    + "  hand " + N(DrawnUI.MeasureWidth(words[i], 19f))
                    + "   (capped at 124)");
            Say("");
        }

        // ══ the frame ══════════════════════════════════════════════════════════

        static void Frame(RectTransform s)
        {
            // THE SHIPPING HEADING. Straight through FounderDraftScreen.Heading, which is
            // what every one of the seven draft pages opens with.
            FounderDraftScreen.Heading(s, "CHOOSE YOUR FOUNDER", 58f, 60f, 28f);
            Tick(s, 60f + GodotHeadDisplay, 22f, 108f);
            DrawnUI.HandLabel(s, "godot measured " + N(GodotHeadDisplay) + "px ↑",
                              60f + GodotHeadDisplay - 258f, 142f, 20f,
                              DrawnUI.WithAlpha(DrawnUI.Coral, 0.9f), 250f,
                              TextAlignmentOptions.TopRight);

            // THE CONTROL — the same words the way they read before the route: the
            // writing hand under a rule cut to the writing hand's width.
            DrawnUI.HandLabel(s, "the same heading in the WRITING hand — the before",
                              60f, 208f, 20f, DrawnUI.WithAlpha(DrawnUI.Cream, 0.55f), 640f);
            DrawnUI.HandLabel(s, "CHOOSE YOUR FOUNDER", 60f, 236f, 58f, DrawnUI.Cream);
            GameUi.HandRule(s, 62f, 236f + 58f * 1.48f,
                            DrawnUI.MeasureWidth("CHOOSE YOUR FOUNDER", 58f), DrawnUI.Coral, 6);
            Tick(s, 60f + GodotHeadDisplay, 230f, 108f);

            Sheet(s);
            Donut(s);
            Cards(s);
        }

        /// THE CAP TABLE'S HOLE, at the crew page's own geometry. It is the one place on
        /// the flow where the route is also a LAYOUT change: `CapTableDonut._draw()`
        /// writes two separate draw_strings in Baloo2 Bold — the number at 34 on a 90px
        /// centred field, "yours" at 18 on a 64px one at 70% ink, 28px of baseline apart
        /// — and the port had folded them into one wrapped two-line label, which gave
        /// the word under the number the number's own size and alpha. draw_string takes
        /// a BASELINE, so each top-left here is that baseline less the display ascent.
        /// Drawn on the crew page's own paper so the ink reads against the right ground.
        static void Donut(RectTransform s)
        {
            DrawnUI.HandLabel(s, "the cap table's hole — two draw_strings, not one "
                              + "wrapped label", 60f, 420f, 20f,
                              DrawnUI.WithAlpha(DrawnUI.Cream, 0.55f), 700f);

            const float SheetX = 100f, SheetY = 452f;
            var paper = GameUi.PaperSheet(s, SheetX, SheetY, 258f, 310f, 2, 4f, null, "donutsheet");
            GameUi.Tilt(paper, 0.012f);

            // the donut sits 26/16 inside its sheet on the crew page (1240/206 against
            // 1214/190); the same offsets are kept here so the hole lands where it does
            float dx = SheetX + 26f;
            float dy = SheetY + 16f;
            DrawnChart.Mount(s, "donut",
                DrawnChart.Donut(new[] { 62f, 25f, 13f },
                                 new[] { DrawnUI.Sage, DrawnUI.Blue, DrawnUI.Coral }, 210),
                dx, dy, 210f, 210f);
            float cx = dx + 105f;
            float cy = dy + 105f;
            DrawnUI.DisplayLabel(s, "62%", cx - 42f,
                cy + 4f - DrawnUI.Ascent(DrawnUI.Display, 34f), 34f, DrawnUI.Ink, 90f,
                TextAlignmentOptions.Top);
            DrawnUI.DisplayLabel(s, "yours", cx - 32f,
                cy + 32f - DrawnUI.Ascent(DrawnUI.Display, 18f), 18f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 64f, TextAlignmentOptions.Top);
            DrawnUI.HandLabel(s, "the cap table", SheetX + 22f, SheetY + 240f, 26f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 218f, TextAlignmentOptions.Top);
        }

        /// The founder's sheet, transcribed from DraftSelectPage: the same coordinates,
        /// the same calls, so what is photographed is what the page builds.
        static void Sheet(RectTransform s)
        {
            var panel = GameUi.PaperSheet(s, 936f, 72f, 540f,
                FounderDraftScreen.SheetBottomMax - 72f, 1, 4f, null, "sheet");
            GameUi.Tilt(panel, -0.008f);

            DrawnUI.DisplayLabel(panel, "THE GARAGE HACKER", 44f, 20f, 46f, DrawnUI.Ink, 470f);
            DrawnUI.HandLabel(panel, "ships at 3am, sells at noon, sleeps never", 44f, 82f, 26f,
                              DrawnUI.WithAlpha(DrawnUI.Ink, 0.9f), 470f);
            GameUi.HandRule(panel, 44f, 146f, 140f, DrawnUI.Coral, 7);

            var stats = new Dictionary<string, int>
            {
                { "build", 5 }, { "sell", 1 }, { "raise", 2 }, { "recruit", 2 }, { "grit", 4 },
            };
            GameUi.StatPips(panel, 44f, 172f, stats,
                            FounderDraftScreen.StatNames, FounderDraftScreen.StatLabels);

            DrawnUI.DisplayLabel(panel, "HIDDEN TRAITS", 44f, 434f, 22f,
                                 DrawnUI.WithAlpha(DrawnUI.Ink, 0.62f));
            DrawnUI.HandLabel(panel, "click any trait for what it does", 232f, 436f, 20f,
                              DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f));

            var traits = new Dictionary<string, int>();
            var deltas = new Dictionary<string, int>();
            IList<string> names = Runway.Core.GameState.TRAIT_NAMES;
            for (int i = 0; i < names.Count; i++)
            {
                traits[names[i]] = 1 + (i % 5);
                if (i == 0) deltas[names[i]] = 1;
                if (i == 2) deltas[names[i]] = -1;
            }
            // the deltas are asked for HERE ONLY, so the one element on this ledger that
            // stays in the writing hand is in the frame beside the ones that did not
            GameUi.TraitPips(panel, 44f, 462f, traits, names, deltas);

            DrawnUI.HandLabel(panel, "IN THE BANK, DAY ONE", 44f, 614f, 23f,
                              DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f));
            DrawnUI.DisplayLabel(panel, "$12,000", 44f, 638f, 40f, DrawnUI.Sage, 470f);
            DrawnUI.HandLabel(panel, "PERK", 44f, 692f, 23f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f));
            DrawnUI.HandLabel(panel, "★ ships one extra product a week, forever", 44f, 714f, 25f,
                              DrawnUI.Ink, 470f);
        }

        /// Both cards and both papers in one band: the draft's LOCK IN in the display
        /// hand on the draft's softer paper, the title screen's card in the writing hand
        /// on its own harder one.
        static void Cards(RectTransform s)
        {
            // both cards stay left of x=936: the captions are CREAM, and cream on the
            // stat sheet's cream paper is a caption nobody can read
            DrawnUI.HandLabel(s, "the draft's card — DISPLAY hand", 200f, 800f, 20f,
                              DrawnUI.WithAlpha(DrawnUI.Cream, 0.55f), 300f);
            DrawnUI.PaperButton(s, "LOCK IN  →", 200f, 840f, 300f, 84f, 36f,
                                DrawnUI.Ink, DrawnUI.CoralDark, null, 1.045f,
                                GameUi.DraftPaper(200 % 5), DrawnUI.Display);

            DrawnUI.HandLabel(s, "the title screen's card — WRITING hand", 550f, 800f, 20f,
                              DrawnUI.WithAlpha(DrawnUI.Cream, 0.55f), 340f);
            DrawnUI.PaperButton(s, "LOCK IN  →", 550f, 840f, 300f, 84f, 36f,
                                DrawnUI.Ink, DrawnUI.CoralDark, null, 1.045f,
                                DrawnUI.PaperStyle.Button);
        }

        static void Tick(RectTransform s, float x, float y, float h)
        {
            DrawnUI.Fill(s, "tick", DrawnUI.WithAlpha(DrawnUI.Coral, 0.85f), x, y, 2f, h);
        }

        // ══ the rig ════════════════════════════════════════════════════════════

        static void Rig(out GameObject rig, out Camera cam, out RenderTexture rt,
                        out RectTransform stage)
        {
            rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            rt.Create();

            rig = new GameObject("~routerig");
            rig.hideFlags = HideFlags.HideAndDontSave;

            var camGo = new GameObject("~routecam", typeof(Camera));
            camGo.transform.SetParent(rig.transform, false);
            cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = GameUi.Night;      // the stage every draft page stands on
            cam.targetTexture = rt;
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            var canvasGo = new GameObject("~routecanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(rig.transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            var stageGo = new GameObject("stage", typeof(RectTransform));
            stage = stageGo.GetComponent<RectTransform>();
            stage.SetParent(canvasGo.transform, false);
            stage.anchorMin = new Vector2(0f, 1f);
            stage.anchorMax = new Vector2(0f, 1f);
            stage.pivot = new Vector2(0f, 1f);
            stage.sizeDelta = new Vector2(W, H);
            stage.anchoredPosition = Vector2.zero;

            DrawnUI.FullFill(stage, "night", GameUi.Night);
        }

        static void Capture(Camera cam, RenderTexture rt, string path)
        {
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            cam.Render();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new UnityEngine.Rect(0f, 0f, W, H), 0, 0);
            tex.Apply(false, false);
            RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Say("wrote " + path + "  · " + Lit(tex) + " lit pixels of " + (W * H));
            UnityEngine.Object.DestroyImmediate(tex);
        }

        /// How much of the frame is not the night field — a blank shot is a device that
        /// never rasterised, and the numbers above are still worth reading.
        static int Lit(Texture2D tex)
        {
            Color32[] px = tex.GetPixels32();
            Color32 night = GameUi.Night;
            int n = 0;
            for (int i = 0; i < px.Length; i++)
                if (Mathf.Abs(px[i].r - night.r) > 8 || Mathf.Abs(px[i].g - night.g) > 8
                    || Mathf.Abs(px[i].b - night.b) > 8) n++;
            return n;
        }
    }
}
