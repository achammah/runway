using System;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;

namespace Runway.EditorTools
{
    /// <summary>
    /// THE TYPE, SHOT AND MEASURED. Four pictures and one list of numbers, against
    /// the same numbers read out of Godot with a headless probe:
    ///
    ///   heading.png  the display hand beside the writing hand at the same size,
    ///                each with a coral tick at the width Godot measured for it
    ///   glyphs.png   every character the game writes that Patrick Hand does not
    ///                own — a box here is a box in the game
    ///   pitch.png    a wrapped body block with an ink hairline ruled at Godot's
    ///                pitch: the lines sit on the rules or they do not
    ///   dots.png     the how-to page dots at their Godot geometry, boxed in coral
    ///
    /// Run it:
    ///   Unity -batchmode -quit -projectPath unity \
    ///         -executeMethod Runway.EditorTools.TypeShot.Shoot
    /// Output goes to $RUNWAY_TYPE_OUT, or a folder under the system temp dir.
    /// Leave -nographics OFF: these shots need a device to rasterise type with.
    /// </summary>
    public static class TypeShot
    {
        const int W = 1536;
        const int H = 1024;

        // what the Godot probe measured, so the numbers travel with the picture
        const float GodotHeadDisplay = 639f;   // "CHOOSE YOUR FOUNDER" @58, Baloo2
        const float GodotHeadHand = 515f;      // the same words in Patrick Hand
        const float GodotNext = 85f;           // "NEXT  " @34, hand, no arrow
        const float GodotNextArrow = 119f;     // "NEXT  →" @34, hand + system fallback
        const float GodotGotIt = 418f;         // "GOT IT — …SOMETHING  " @32
        const float GodotBodyPitch = 45f;      // a Label at 30: 32 + 10 + 3

        public static void Shoot()
        {
            string dir = Environment.GetEnvironmentVariable("RUNWAY_TYPE_OUT");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Path.GetTempPath(), "runway-type");
            Directory.CreateDirectory(dir);

            var log = new StringBuilder();
            Say(log, "type probe · out " + dir);

            // the bake normally lands on a delayCall the editor may never reach in
            // -batchmode -quit, and a font resolved before it caches the runtime build
            try { Runway.App.FontBaker.BakeAll(); }
            catch (Exception e) { Say(log, "bake pass: " + e.Message); }

            try
            {
                Faces(log);
                Metrics(log);
                Widths(log);
                Coverage(log);

                Shot(dir, log, "heading", HeadingShot);
                Shot(dir, log, "glyphs", GlyphShot);
                Shot(dir, log, "pitch", PitchShot);
                Shot(dir, log, "dots", DotShot);
            }
            catch (Exception e)
            {
                Say(log, "FAILED: " + e);
            }

            try { File.WriteAllText(Path.Combine(dir, "measurements.txt"), log.ToString()); }
            catch (Exception) { }
        }

        static void Say(StringBuilder log, string line)
        {
            Debug.Log("TYPESHOT: " + line);
            log.Append(line).Append('\n');
        }

        static string N(float v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }

        // ── the numbers ────────────────────────────────────────────────────────

        static void Faces(StringBuilder log)
        {
            Face(log, "hand", DrawnUI.Hand);
            Face(log, "display", DrawnUI.Display);
            Face(log, "glyphs", DrawnUI.Glyphs);
            TMP_FontAsset h = DrawnUI.Hand;
            if (h != null && h.fallbackFontAssetTable != null)
                Say(log, "hand fallbacks: " + h.fallbackFontAssetTable.Count
                         + (h.fallbackFontAssetTable.Count > 0 && h.fallbackFontAssetTable[0] != null
                            ? " (" + h.fallbackFontAssetTable[0].name + ")" : ""));
        }

        static void Face(StringBuilder log, string what, TMP_FontAsset f)
        {
            if (f == null) { Say(log, what + ": NONE"); return; }
            var fi = f.faceInfo;
            Say(log, what + ": " + f.name + " · pointSize " + N(fi.pointSize)
                     + " scale " + N(fi.scale)
                     + " ascent " + N(fi.ascentLine) + " (" + N(fi.ascentLine / fi.pointSize) + "x)"
                     + " descent " + N(fi.descentLine) + " (" + N(-fi.descentLine / fi.pointSize) + "x)"
                     + " lineHeight " + N(fi.lineHeight) + " (" + N(fi.lineHeight / fi.pointSize) + "x)"
                     + " · " + f.characterTable.Count + " characters"
                     + " · atlas " + f.atlasPopulationMode);
        }

        /// The pitch every ported body block runs at, measured off the shaped text
        /// rather than trusted from the setting.
        static void Metrics(StringBuilder log)
        {
            Say(log, "line pitch — measured baseline to baseline, against Godot's");
            RectTransform host = Park();
            float[] sizes = { 19f, 21f, 24f, 26f, 28f, 30f, 34f, 48f, 58f };
            for (int i = 0; i < sizes.Length; i++)
            {
                float s = sizes[i];
                float godot = Mathf.Ceil(1.042f * s) + Mathf.Ceil(0.312f * s) + DrawnUI.LabelLeading;
                float got = Pitch(host, s, DrawnUI.LabelLeading, null);
                float raw = 1.354f * s;
                Say(log, "  @" + s.ToString("0") + "  godot " + N(godot)
                         + "  unity " + N(got)
                         + "  (was " + N(raw) + ", " + N(100f * (raw / godot - 1f)) + "%)"
                         + "  err " + N(got - godot) + "px");
            }
            float d30 = Pitch(host, 30f, DrawnUI.LabelLeading, DrawnUI.Display);
            Say(log, "  display @30  godot " + N(Mathf.Ceil(1.078f * 30f) + Mathf.Ceil(0.524f * 30f) + 3f)
                     + "  unity " + N(d30));
            float ml = Pitch(host, 30f, DrawnUI.StringLeading, null);
            Say(log, "  draw_multiline_string @30 (no theme leading)  godot 42.00  unity " + N(ml));

            // where the first baseline of a transcribed draw_string() actually lands
            var t = DrawnUI.InkString(host, "YOU WRITE. THE DIE DECIDES.", 90f, 56f, DrawnUI.Ink);
            t.ForceMeshUpdate();
            float top = DrawnUI.TopLeftY(t.rectTransform);
            float baseline = top - t.textInfo.lineInfo[0].baseline;
            float wasTop = 90f - 56f * 0.78f;
            Say(log, "draw_string baseline @56: asked 90.00, landed " + N(baseline)
                     + "  · box top now " + N(top) + ", was " + N(wasTop)
                     + " → the old guess put the baseline at "
                     + N(wasTop + DrawnUI.Ascent(DrawnUI.Hand, 56f))
                     + ", " + N(wasTop + DrawnUI.Ascent(DrawnUI.Hand, 56f) - 90f) + "px low");
            UnityEngine.Object.DestroyImmediate(t.gameObject);
        }

        static float Pitch(RectTransform host, float size, float leading, TMP_FontAsset font)
        {
            string body = "one two three four five six seven eight nine ten eleven twelve "
                        + "thirteen fourteen fifteen sixteen seventeen eighteen nineteen twenty";
            TextMeshProUGUI t = font == null
                ? DrawnUI.HandLabel(host, body, 0f, 0f, size, DrawnUI.Ink, 420f,
                                    TextAlignmentOptions.TopLeft, leading)
                : DrawnUI.DisplayLabel(host, body, 0f, 0f, size, DrawnUI.Ink, 420f,
                                       TextAlignmentOptions.TopLeft, leading);
            t.ForceMeshUpdate();
            float p = 0f;
            if (t.textInfo != null && t.textInfo.lineCount >= 2)
                p = t.textInfo.lineInfo[0].baseline - t.textInfo.lineInfo[1].baseline;
            UnityEngine.Object.DestroyImmediate(t.gameObject);
            return p;
        }

        static void Widths(StringBuilder log)
        {
            Say(log, "widths — DrawnUI.MeasureWidth against Godot's get_string_size");
            Pair(log, "NEXT  ", 34f, null, GodotNext);
            Pair(log, "NEXT  →", 34f, null, GodotNextArrow);
            Pair(log, "GOT IT — LET'S FOUND SOMETHING  ", 32f, null, GodotGotIt);
            Pair(log, "CHOOSE YOUR FOUNDER", 58f, null, GodotHeadHand);
            Pair(log, "CHOOSE YOUR FOUNDER", 58f, DrawnUI.Display, GodotHeadDisplay);
            Say(log, "  howto button: NEXT  → card = " + N(DrawnUI.MeasureWidth("NEXT  →", 34f) + 68f)
                     + " (godot " + N(GodotNextArrow + 68f) + ")");
            Say(log, "  howto button: GOT IT … card = "
                     + N(DrawnUI.MeasureWidth("GOT IT — LET'S FOUND SOMETHING  →", 32f) + 68f)
                     + " (godot 518.00)");

            // the same words on a hand with NO borrowed face behind it — which is what
            // shipped, and what made the card two dozen pixels wide of Godot's
            float bare1 = Bare("NEXT  →", 34f);
            float bare2 = Bare("GOT IT — LET'S FOUND SOMETHING  →", 32f);
            Say(log, "  no borrowed face, pen position: NEXT  → card = "
                     + N(bare1 + 68f) + " (" + N(bare1 - GodotNextArrow) + "px over godot)"
                     + " · GOT IT … card = " + N(bare2 + 68f) + " (" + N(bare2 - 450f) + "px over)");
            float old1 = BareBox("NEXT  →", 34f);
            float old2 = BareBox("GOT IT — LET'S FOUND SOMETHING  →", 32f);
            Say(log, "  THE SHIPPED PATH (no borrowed face + GetPreferredValues): NEXT  → card = "
                     + N(old1 + 68f) + " (" + N(old1 - GodotNextArrow) + "px over godot)"
                     + " · GOT IT … card = " + N(old2 + 68f) + " (" + N(old2 - 450f) + "px over)");
            Say(log, "  who draws the arrow:  bare hand -> " + Who(_bareRuler, '→')
                     + "   ·  with the borrowed face -> " + Who(_ruler(), '→'));
        }

        static void Pair(StringBuilder log, string text, float size, TMP_FontAsset f, float godot)
        {
            float got = DrawnUI.MeasureWidth(text, size, f);
            float was = Boxed(text, size, f);
            Say(log, "  '" + text + "' @" + size.ToString("0")
                     + (f == null ? " hand" : " display")
                     + " -> " + N(got) + "  godot " + N(godot)
                     + "  err " + N(got - godot) + "px (" + N(100f * (got / godot - 1f)) + "%)"
                     + "  · GetPreferredValues was " + N(was)
                     + " (" + N(was - godot) + "px over)");
        }

        static TextMeshProUGUI _measured;

        /// A ruler on the real hand, so the two can be asked the same question.
        static TextMeshProUGUI _ruler()
        {
            if (_measured == null)
            {
                var rt = DrawnUI.Rect(Park(), "liveruler", -5000f, -5000f, 4000f, 200f);
                _measured = rt.gameObject.AddComponent<TextMeshProUGUI>();
                _measured.raycastTarget = false;
                _measured.textWrappingMode = TextWrappingModes.NoWrap;
                if (DrawnUI.Hand != null) _measured.font = DrawnUI.Hand;
            }
            return _measured;
        }

        /// Which face actually supplied a character, and how wide its advance was.
        static string Who(TMP_Text t, char c)
        {
            if (t == null) return "no ruler";
            t.fontSize = 34f;
            t.text = "N" + c;
            try
            {
                t.ForceMeshUpdate(true, true);
                TMP_TextInfo info = t.textInfo;
                if (info == null || info.characterCount < 2) return "no shaping";
                TMP_CharacterInfo ci = info.characterInfo[1];
                float adv = ci.xAdvance - info.characterInfo[0].xAdvance;
                string face = ci.fontAsset != null ? ci.fontAsset.name : "NOTHING";
                return face + " (advance " + N(adv) + "px @34, visible " + ci.isVisible + ")";
            }
            catch (Exception e) { return "failed: " + e.Message; }
        }

        static TextMeshProUGUI _bareRuler;

        /// The hand with nothing hung off it, built fresh from the .ttf — the state
        /// the game shipped in, where an arrow was a box of whatever width TMP's own
        /// default happened to use.
        static float Bare(string text, float size)
        {
            if (_bareRuler == null)
            {
                Font ttf = Resources.Load<Font>("Fonts/PatrickHand-Regular");
                TMP_FontAsset bare = ttf != null ? TMP_FontAsset.CreateFontAsset(ttf) : null;
                var rt = DrawnUI.Rect(Park(), "bareruler", -5000f, -5000f, 4000f, 200f);
                _bareRuler = rt.gameObject.AddComponent<TextMeshProUGUI>();
                _bareRuler.raycastTarget = false;
                _bareRuler.textWrappingMode = TextWrappingModes.NoWrap;
                if (bare != null) _bareRuler.font = bare;
            }
            return BarePen(text, size);
        }

        /// The shipped call, verbatim: the old ruler's own GetPreferredValues.
        static float BareBox(string text, float size)
        {
            Bare(text, size);   // makes sure the bare ruler exists
            _bareRuler.fontSize = size;
            try { return _bareRuler.GetPreferredValues(text).x; }
            catch (Exception) { return 0f; }
        }

        static float BarePen(string text, float size)
        {
            _bareRuler.fontSize = size;
            _bareRuler.text = text;
            try
            {
                _bareRuler.ForceMeshUpdate(true, true);
                TMP_TextInfo info = _bareRuler.textInfo;
                if (info != null && info.characterCount > 0)
                    return info.characterInfo[info.characterCount - 1].xAdvance;
            }
            catch (Exception) { }
            return 0f;
        }

        static TextMeshProUGUI _boxRuler;

        /// What MeasureWidth used to return: TMP's preferred BOX, atlas padding and all.
        static float Boxed(string text, float size, TMP_FontAsset f)
        {
            if (_boxRuler == null)
            {
                var rt = DrawnUI.Rect(Park(), "boxruler", -5000f, -5000f, 4000f, 200f);
                _boxRuler = rt.gameObject.AddComponent<TextMeshProUGUI>();
                _boxRuler.raycastTarget = false;
                _boxRuler.textWrappingMode = TextWrappingModes.NoWrap;
            }
            _boxRuler.font = f != null ? f : DrawnUI.Hand;
            _boxRuler.fontSize = size;
            try { return _boxRuler.GetPreferredValues(text).x; }
            catch (Exception) { return 0f; }
        }

        static void Coverage(StringBuilder log)
        {
            var missHand = new StringBuilder();
            var missDisplay = new StringBuilder();
            for (int i = 0; i < DrawnUI.GlyphSet.Length; i++)
            {
                char c = DrawnUI.GlyphSet[i];
                if (DrawnUI.Hand == null || !DrawnUI.Hand.HasCharacter(c, true, true)) missHand.Append(c);
                if (DrawnUI.Display == null || !DrawnUI.Display.HasCharacter(c, true, true)) missDisplay.Append(c);
            }
            Say(log, "glyph coverage over \"" + DrawnUI.GlyphSet + "\"");
            Say(log, "  hand    still missing: "
                     + (missHand.Length == 0 ? "none" : missHand.ToString()));
            Say(log, "  display still missing: "
                     + (missDisplay.Length == 0 ? "none" : missDisplay.ToString()));
            string five = "★✓⏰⚠☐";
            var missFive = new StringBuilder();
            for (int i = 0; i < five.Length; i++)
                if (DrawnUI.Hand == null || !DrawnUI.Hand.HasCharacter(five[i], true, true))
                    missFive.Append(five[i]);
            Say(log, "  the five asked for (U+2605 U+2713 U+23F0 U+26A0 U+2610): "
                     + (missFive.Length == 0 ? "ALL RESOLVE" : "MISSING " + missFive));
        }

        // ── the pictures ───────────────────────────────────────────────────────

        static void Shot(string dir, StringBuilder log, string name, Action<RectTransform> build)
        {
            GameObject camGo = null, canvasGo = null;
            RenderTexture rt = null;
            try
            {
                Camera cam;
                RectTransform stage;
                Stage(out camGo, out canvasGo, out cam, out rt, out stage);
                build(stage);
                Canvas.ForceUpdateCanvases();

                var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                cam.Render();
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new UnityEngine.Rect(0f, 0f, W, H), 0, 0);
                tex.Apply(false, false);
                RenderTexture.active = prev;

                string p = Path.Combine(dir, name + ".png");
                File.WriteAllBytes(p, tex.EncodeToPNG());
                Say(log, "wrote " + p + "  · ink " + Ink(tex) + "px");
                UnityEngine.Object.DestroyImmediate(tex);
            }
            catch (Exception e)
            {
                Say(log, name + " shot FAILED: " + e.Message);
            }
            finally
            {
                if (rt != null) { RenderTexture.active = null; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
                if (canvasGo != null) UnityEngine.Object.DestroyImmediate(canvasGo);
                if (camGo != null) UnityEngine.Object.DestroyImmediate(camGo);
            }
        }

        /// How much of the frame is not paper — a blank shot is a device that never
        /// rasterised, and the numbers above are still worth reading.
        static int Ink(Texture2D tex)
        {
            Color32[] px = tex.GetPixels32();
            Color32 cream = DrawnUI.Cream;
            int n = 0;
            for (int i = 0; i < px.Length; i++)
                if (Mathf.Abs(px[i].r - cream.r) > 8 || Mathf.Abs(px[i].g - cream.g) > 8
                    || Mathf.Abs(px[i].b - cream.b) > 8) n++;
            return n;
        }

        static void Stage(out GameObject camGo, out GameObject canvasGo, out Camera cam,
                          out RenderTexture rt, out RectTransform stage)
        {
            rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            rt.Create();

            camGo = new GameObject("~typecam", typeof(Camera));
            camGo.hideFlags = HideFlags.HideAndDontSave;
            cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = DrawnUI.Cream;
            cam.targetTexture = rt;
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            canvasGo = new GameObject("~typecanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.hideFlags = HideFlags.HideAndDontSave;
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

            DrawnUI.FullFill(stage, "paper", DrawnUI.Cream);
        }

        static RectTransform _park;

        /// A rect off the side of nowhere, for text that is measured and never shown.
        static RectTransform Park()
        {
            if (_park == null)
            {
                var go = new GameObject("~typepark", typeof(RectTransform), typeof(Canvas));
                go.hideFlags = HideFlags.HideAndDontSave;
                go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                _park = go.GetComponent<RectTransform>();
            }
            return _park;
        }

        // ── shot 1 · the two hands, at the width Godot measured ────────────────

        static void HeadingShot(RectTransform s)
        {
            DrawnUI.HandLabel(s, "THE DISPLAY HAND — Baloo2 Bold, what _font_d writes",
                              60f, 40f, 24f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f));
            DrawnUI.DisplayLabel(s, "CHOOSE YOUR FOUNDER", 60f, 90f, 58f, DrawnUI.Ink);
            Tick(s, 60f + GodotHeadDisplay, 84f, 90f, DrawnUI.Coral);
            DrawnUI.HandLabel(s, "godot: " + N(GodotHeadDisplay) + "px",
                              60f + GodotHeadDisplay + 12f, 108f, 22f, DrawnUI.Coral);

            DrawnUI.HandLabel(s, "THE WRITING HAND — Patrick Hand, the same words at the same size",
                              60f, 220f, 24f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f));
            DrawnUI.HandLabel(s, "CHOOSE YOUR FOUNDER", 60f, 270f, 58f, DrawnUI.Ink);
            Tick(s, 60f + GodotHeadHand, 264f, 90f, DrawnUI.Coral);
            DrawnUI.HandLabel(s, "godot: " + N(GodotHeadHand) + "px",
                              60f + GodotHeadHand + 12f, 288f, 22f, DrawnUI.Coral);

            DrawnUI.HandLabel(s, "the display hand under its own rule — _rule_under measures the "
                              + "heading and draws the coral to fit", 60f, 420f, 22f,
                              DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 1400f);
            DrawnUI.DisplayLabel(s, "PACK YOUR BAG", 60f, 460f, 56f, DrawnUI.Ink);
            DrawnUI.Rule(s, 62f, 460f + 56f * 1.48f, DrawnUI.MeasureWidth("PACK YOUR BAG", 56f, DrawnUI.Display),
                         DrawnUI.Coral, 4f, 4, 1.5f, 21);

            DrawnUI.HandLabel(s, "the same rule cut to the WRITING hand's width — the gap this fixes",
                              60f, 600f, 22f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 1400f);
            DrawnUI.HandLabel(s, "PACK YOUR BAG", 60f, 640f, 56f, DrawnUI.Ink);
            DrawnUI.Rule(s, 62f, 640f + 56f * 1.48f, DrawnUI.MeasureWidth("PACK YOUR BAG", 56f),
                         DrawnUI.Coral, 4f, 4, 1.5f, 21);

            DrawnUI.HandLabel(s, "paper buttons stay in the WRITING hand — title_screen.gd never "
                              + "loads Baloo2", 60f, 800f, 22f,
                              DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 1400f);
            DrawnUI.PaperButton(s, "LOCK IT IN", 60f, 840f, 300f, 92f, 40f,
                                DrawnUI.Ink, DrawnUI.Coral, null);
        }

        static void Tick(RectTransform s, float x, float y, float h, Color c)
        {
            DrawnUI.Fill(s, "tick", c, x, y, 2f, h);
        }

        // ── shot 2 · the borrowed characters ───────────────────────────────────

        static void GlyphShot(RectTransform s)
        {
            DrawnUI.HandLabel(s, "EVERY CHARACTER PATRICK HAND DOES NOT OWN", 60f, 40f, 30f, DrawnUI.Ink);
            DrawnUI.HandLabel(s, "a box here is a box in the game", 60f, 82f, 22f,
                              DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f));

            float x = 60f, y = 150f;
            for (int i = 0; i < DrawnUI.GlyphSet.Length; i++)
            {
                char c = DrawnUI.GlyphSet[i];
                DrawnUI.HandLabel(s, c.ToString(), x, y, 54f, DrawnUI.Ink, 100f,
                                  TextAlignmentOptions.Top);
                DrawnUI.HandLabel(s, "U+" + ((int)c).ToString("X4"), x, y + 76f, 16f,
                                  DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f), 100f,
                                  TextAlignmentOptions.Top);
                x += 108f;
                if (x > 1380f) { x = 60f; y += 130f; }
            }

            y += 160f;
            DrawnUI.HandLabel(s, "the five the report named, in a sentence:", 60f, y, 24f,
                              DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f));
            DrawnUI.HandLabel(s, "★ MILESTONE · VESTED ✓ · ⏰ in 3 wks · ⚠ this spend kills it · ☐ unpacked",
                              60f, y + 40f, 36f, DrawnUI.Ink, 1420f);
            DrawnUI.HandLabel(s, "and the arrow the buttons all end on:  NEXT  →   ←  back",
                              60f, y + 110f, 36f, DrawnUI.Coral, 1420f);
        }

        // ── shot 3 · the pitch, ruled at Godot's ───────────────────────────────

        static void PitchShot(RectTransform s)
        {
            DrawnUI.HandLabel(s, "BODY PITCH — the hairlines are ruled at Godot's "
                              + N(GodotBodyPitch) + "px", 60f, 34f, 28f, DrawnUI.Ink);

            const float BodyY = 120f;
            const float BodyX = 80f;
            string body = "RUNWAY! is a fully generative survival game. There is no script: "
                        + "your market, your rivals, your investors, every week's consequences "
                        + "and every picture of your office are invented on the spot, for this "
                        + "run only. Nobody else will ever play your company.";

            // the rules first, so the type sits ON them
            var probe = DrawnUI.HandLabel(s, body, BodyX, BodyY, 30f, DrawnUI.Ink, 740f);
            probe.ForceMeshUpdate();
            float first = BodyY - probe.textInfo.lineInfo[0].baseline;
            for (int i = 0; i < 6; i++)
            {
                float ry = first + GodotBodyPitch * i;
                DrawnUI.Fill(s, "rule", DrawnUI.WithAlpha(DrawnUI.Coral, 0.55f), BodyX - 30f, ry, 800f, 1f);
                DrawnUI.HandLabel(s, N(ry - first) + "", BodyX + 780f, ry - 14f, 18f,
                                  DrawnUI.WithAlpha(DrawnUI.Coral, 0.8f));
            }
            probe.transform.SetAsLastSibling();

            DrawnUI.HandLabel(s, "measured pitch: " + N(probe.textInfo.lineCount >= 2
                                  ? probe.textInfo.lineInfo[0].baseline - probe.textInfo.lineInfo[1].baseline
                                  : 0f) + "px   ·   TMP's own would be " + N(1.354f * 30f) + "px",
                              BodyX, BodyY + 260f, 24f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 900f);

            // the how-to caption is a draw_multiline_string: no theme, no leading
            const float CapY = 560f;
            DrawnUI.HandLabel(s, "the how-to caption is draw_multiline_string — no theme, "
                              + "so no leading: 42px", 60f, CapY - 60f, 24f,
                              DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 1400f);
            var cap = DrawnUI.HandLabel(s, HowToCaption, 238f,
                                        CapY - DrawnUI.Ascent(DrawnUI.Hand, 30f), 30f,
                                        DrawnUI.WithAlpha(DrawnUI.Ink, 0.85f), 1060f,
                                        TextAlignmentOptions.Top, DrawnUI.StringLeading);
            cap.ForceMeshUpdate();
            DrawnUI.Fill(s, "rule", DrawnUI.WithAlpha(DrawnUI.Coral, 0.55f), 208f, CapY, 1120f, 1f);
            DrawnUI.Fill(s, "rule", DrawnUI.WithAlpha(DrawnUI.Coral, 0.55f), 208f, CapY + 42f, 1120f, 1f);
            cap.transform.SetAsLastSibling();

            // and the how-to title, a draw_string at baseline 90 relocated to 760 here
            DrawnUI.HandLabel(s, "the how-to title is draw_string at a BASELINE — the coral rule "
                              + "IS the baseline", 60f, 720f, 24f,
                              DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 1400f);
            DrawnUI.Fill(s, "rule", DrawnUI.WithAlpha(DrawnUI.Coral, 0.55f), 200f, 840f, 1140f, 1f);
            var title = DrawnUI.InkString(s, "YOU WRITE. THE DIE DECIDES.", 840f, 56f, DrawnUI.Ink);
            title.transform.SetAsLastSibling();
            DrawnUI.HandLabel(s, "the old 0.78 guess put it here →", 200f,
                              840f - 56f * 0.78f - 30f, 20f, DrawnUI.WithAlpha(DrawnUI.Blue, 0.9f));
            DrawnUI.Fill(s, "rule", DrawnUI.WithAlpha(DrawnUI.Blue, 0.5f), 200f,
                         840f - 56f * 0.78f + DrawnUI.Ascent(DrawnUI.Hand, 56f), 1140f, 1f);
        }

        const string HowToCaption =
            "Write your week's move in the journal. A d20 rolls the moment you commit — "
            + "your five muscles (build, sell, raise, recruit, grit) add to it, the world "
            + "sets the difficulty.";

        // ── shot 4 · the how-to page dots ──────────────────────────────────────

        static void DotShot(RectTransform s)
        {
            DrawnUI.HandLabel(s, "HOW-TO PAGE DOTS — coral box = the r=11 circle Godot draws",
                              60f, 40f, 28f, DrawnUI.Ink);

            Row(s, 200f, 3, 0, "page 1 of 3");
            Row(s, 380f, 3, 1, "page 2 of 3");
            Row(s, 560f, 3, 2, "page 3 of 3");

            // the same dots blown up 6x, so the wobble and the ring are readable
            DrawnUI.HandLabel(s, "the current dot at 6x — coral disc r=11, INK ring r=11 over it",
                              60f, 700f, 24f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 1400f);
            int on = DrawnUI.RingSide(11f, 3);
            var big = DrawnUI.Rect(s, "big", 200f, 750f, on * 6f, on * 6f);
            var bd = big.gameObject.AddComponent<Image>();
            bd.sprite = DrawnUI.DiscSprite(11f, 3);
            bd.color = DrawnUI.Pen;
            var br = DrawnUI.FullRect(big, "ring");
            var bri = br.gameObject.AddComponent<Image>();
            bri.sprite = DrawnUI.RingSprite(11f, 3f, 1.2f, 20, 3, false);
            bri.color = DrawnUI.Ink;

            int off = DrawnUI.RingSide(9f, 3);
            var big2 = DrawnUI.Rect(s, "big2", 200f + on * 6f + 60f, 750f + (on - off) * 3f,
                                    off * 6f, off * 6f);
            var bo = big2.gameObject.AddComponent<Image>();
            bo.sprite = DrawnUI.RingSprite(9f, 3f, 1.2f, 20, 3, false);
            bo.color = DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f);

            DrawnUI.HandLabel(s, "current: bakes at " + on + "px, was stretched into 32 (+"
                              + N(100f * (32f / on - 1f)) + "%)",
                              200f, 750f + on * 6f + 14f, 20f,
                              DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), on * 6f);
            DrawnUI.HandLabel(s, "other: bakes at " + off + "px, was stretched into 32 (+"
                              + N(100f * (32f / off - 1f)) + "%)",
                              200f + on * 6f + 60f, 750f + on * 6f + 14f, 20f,
                              DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), off * 6f);
        }

        /// One row of dots at exactly the geometry howto_screen.gd draws, with the
        /// r=11 circle boxed in coral so an oversized host shows immediately.
        static void Row(RectTransform s, float cy, int count, int page, string caption)
        {
            int onSide = DrawnUI.RingSide(11f, 3);
            int offSide = DrawnUI.RingSide(9f, 3);
            for (int i = 0; i < count; i++)
            {
                float cx = 768f + (i - (count - 1) * 0.5f) * 46f;
                DrawnUI.Fill(s, "box", DrawnUI.WithAlpha(DrawnUI.Coral, 0.25f),
                             cx - 11f, cy - 11f, 22f, 22f);
                if (i == page)
                {
                    var d = DrawnUI.Rect(s, "on", cx - onSide * 0.5f, cy - onSide * 0.5f, onSide, onSide);
                    var di = d.gameObject.AddComponent<Image>();
                    di.sprite = DrawnUI.DiscSprite(11f, 3);
                    di.color = DrawnUI.Pen;
                    var r = DrawnUI.FullRect(d, "ring");
                    var ri = r.gameObject.AddComponent<Image>();
                    ri.sprite = DrawnUI.RingSprite(11f, 3f, 1.2f, 20 + i, 3, false);
                    ri.color = DrawnUI.Ink;
                }
                else
                {
                    var o = DrawnUI.Rect(s, "off", cx - offSide * 0.5f, cy - offSide * 0.5f, offSide, offSide);
                    var oi = o.gameObject.AddComponent<Image>();
                    oi.sprite = DrawnUI.RingSprite(9f, 3f, 1.2f, 20 + i, 3, false);
                    oi.color = DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f);
                }
            }
            DrawnUI.HandLabel(s, caption, 400f, cy - 16f, 24f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f));
        }
    }
}
