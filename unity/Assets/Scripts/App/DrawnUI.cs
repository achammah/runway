using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Runway.App
{
    /// <summary>
    /// THE DRAWN LOOK, as a kit. The Godot original has no scenes and no theme: every
    /// screen builds itself in code and paints its own paper, its own wobbled ink edge
    /// and its own handwriting. This is that hand, ported — so a screen written against
    /// it looks like the game rather than like a web form laid over a drawn stage.
    ///
    /// COORDINATES ARE GODOT COORDINATES. Every helper takes top-left x/y inside a
    /// 1536x1024 stage, so a geometry read out of a .gd file transcribes unchanged.
    ///
    /// THE INK EDGE IS BAKED, NOT DRAWN PER FRAME. Godot re-runs _draw() with a seeded
    /// RandomNumberGenerator; Unity has no immediate-mode canvas, so the same wobble is
    /// rasterised once into a texture per (size, style) and cached for the session. The
    /// seed still governs the wobble, so the same card is the same card every time.
    /// </summary>
    public static class DrawnUI
    {
        // ── the palette, from the .gd constants ────────────────────────────────
        public static readonly Color Cream = Hex("F2EAD3");
        public static readonly Color Ink = Hex("1E1E1E");
        public static readonly Color Pen = Hex("E86A5C");    // coral
        public static readonly Color Coral = Hex("E86A5C");
        public static readonly Color CoralDark = Hex("C9503F");
        public static readonly Color Sage = Hex("8FA582");
        public static readonly Color Yellow = Hex("F4B942");
        public static readonly Color Blue = Hex("6E8CA0");
        public static readonly Color Stage = Hex("22262B");  // the clear colour

        public static Color Hex(string rgb)
        {
            Color c;
            if (ColorUtility.TryParseHtmlString("#" + rgb, out c)) return c;
            return Color.magenta;
        }

        public static Color WithAlpha(Color c, float a)
        {
            return new Color(c.r, c.g, c.b, a);
        }

        // ── the two hands ──────────────────────────────────────────────────────

        static TMP_FontAsset _hand;
        static bool _handTried;
        static TMP_FontAsset _display;
        static bool _displayTried;

        /// Patrick Hand, resolved through a ladder that never throws:
        ///   1. a TMP font asset baked into Resources/Fonts by an editor pass
        ///   2. the .ttf in Resources/Fonts, turned into a dynamic TMP asset at runtime
        ///   3. whatever TMP is configured to fall back on
        /// A null result is legal — TMP then draws in its own default and the game runs.
        public static TMP_FontAsset Hand
        {
            get
            {
                if (_handTried) return _hand;
                _handTried = true;

                _hand = Resources.Load<TMP_FontAsset>("Fonts/PatrickHand SDF");
                if (_hand == null)
                {
                    Font ttf = Resources.Load<Font>("Fonts/PatrickHand-Regular");
                    if (ttf != null)
                    {
                        try
                        {
                            _hand = TMP_FontAsset.CreateFontAsset(ttf);
                            if (_hand != null) _hand.name = "PatrickHand SDF (runtime)";
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning("RUNWAY! could not build the hand font at runtime ("
                                             + e.Message + ") — falling back to the TMP default.");
                        }
                    }
                }
                if (_hand == null)
                {
                    try { _hand = TMP_Settings.defaultFontAsset; }
                    catch (Exception) { _hand = null; }
                }
                if (_hand == null)
                    Debug.LogWarning("RUNWAY! no hand font: put PatrickHand-Regular.ttf in "
                                     + "Assets/Resources/Fonts/ and import the TMP essentials.");
                LendGlyphs(_hand);
                return _hand;
            }
        }

        /// Baloo2 Bold, THE OTHER HAND. Every .gd screen that has a heading carries a
        /// second font it calls `_font_d` and drives the display type through: the
        /// draft titles, the pip labels, the equity donut, the captions on paper
        /// buttons. Patrick Hand is about a fifth narrower at the same size — CHOOSE
        /// YOUR FOUNDER measures 639px in the display hand and 515px in the writing
        /// one at 58 — so a heading set in the wrong font under-fills its own rule.
        ///
        /// Same ladder as Hand, with Hand itself as the last rung: a missing display
        /// font is a screen that reads narrow, never a screen that fails to draw.
        public static TMP_FontAsset Display
        {
            get
            {
                if (_displayTried) return _display;
                _displayTried = true;

                _display = Resources.Load<TMP_FontAsset>("Fonts/Baloo2 SDF");
                if (_display == null)
                {
                    Font ttf = Resources.Load<Font>("Fonts/Baloo2-Bold");
                    if (ttf != null)
                    {
                        try
                        {
                            _display = TMP_FontAsset.CreateFontAsset(ttf);
                            if (_display != null) _display.name = "Baloo2 SDF (runtime)";
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning("RUNWAY! could not build the display font at runtime ("
                                             + e.Message + ") — headings fall back to the hand.");
                        }
                    }
                }
                if (_display == null)
                {
                    Debug.LogWarning("RUNWAY! no display font: put Baloo2-Bold.ttf in "
                                     + "Assets/Resources/Fonts/ — headings will read narrow.");
                    _display = Hand;
                    return _display;
                }
                LendGlyphs(_display);
                return _display;
            }
        }

        // ── the characters neither hand draws ──────────────────────────────────

        /// Everything the game writes that Patrick Hand's cmap does not contain.
        /// Godot never had to think about this: its font importer ships with
        /// `allow_system_fallback=true` and TextServer quietly borrows a face off the
        /// OS for ★ ✓ ⏰ ⚠ ☐ and even the plain arrow →. TMP borrows nothing, so
        /// every one of these came out a box. The same borrowing is made explicit
        /// here, baked to an atlas by the editor pass so it survives a build.
        public const string GlyphSet =
            "★✓⏰⚠☐☑✗✕☎☀⛈⚡"
            + "▲▼●►↻✎→←↔−≈≤≥";

        /// The faces the glyphs are borrowed FROM, best coverage first. Each entry is
        /// { file path, family name, style name }: the editor pass bakes from the
        /// path, the runtime rung asks the OS for the family. STIX Two Math carries
        /// every symbol above except the pencil and the cross; Arial Unicode carries
        /// those two. TMP walks a fallback chain, so both are hung in order and the
        /// first face that owns a character wins.
        static readonly string[][] GlyphFaces =
        {
            new[] { "/System/Library/Fonts/Supplemental/STIXTwoMath.otf", "STIX Two Math", "Regular" },
            new[] { "/System/Library/Fonts/Supplemental/Arial Unicode.ttf", "Arial Unicode MS", "Regular" },
        };

        /// Where the editor pass parks the bakes — index 0 is asked for first.
        public static string GlyphAssetName(int i)
        {
            return "Fonts/RunwayGlyphs " + i + " SDF";
        }

        public static int GlyphFaceCount { get { return GlyphFaces.Length; } }
        public static string GlyphFacePath(int i) { return GlyphFaces[i][0]; }
        public static string GlyphFaceFamily(int i) { return GlyphFaces[i][1]; }
        public static string GlyphFaceStyle(int i) { return GlyphFaces[i][2]; }

        static TMP_FontAsset _glyphs;
        static bool _glyphsTried;

        /// The head of the borrowed chain: a baked asset out of Resources if the
        /// editor pass ran, otherwise the OS face asked for by name, otherwise null
        /// (and the boxes come back, which is what shipped before this).
        public static TMP_FontAsset Glyphs
        {
            get
            {
                if (_glyphsTried) return _glyphs;
                _glyphsTried = true;

                var chain = new List<TMP_FontAsset>();
                for (int i = 0; i < GlyphFaces.Length; i++)
                {
                    TMP_FontAsset f = Resources.Load<TMP_FontAsset>(GlyphAssetName(i));
                    if (f == null)
                    {
                        try { f = TMP_FontAsset.CreateFontAsset(GlyphFaces[i][1], GlyphFaces[i][2], 90); }
                        catch (Exception) { f = null; }
                        if (f != null) f.name = "RunwayGlyphs " + i + " SDF (runtime)";
                    }
                    if (f != null) chain.Add(f);
                }
                if (chain.Count == 0)
                {
                    Debug.LogWarning("RUNWAY! no glyph face for ★ ✓ ⏰ ⚠ → — run "
                                     + "RUNWAY!/Rebuild the fonts so they bake into Resources.");
                    return null;
                }
                for (int i = 0; i + 1 < chain.Count; i++) Chain(chain[i], chain[i + 1]);
                _glyphs = chain[0];
                return _glyphs;
            }
        }

        /// Hangs the borrowed chain off a hand, once, without ever hanging a font off
        /// itself (TMP walks the table recursively and a loop is a hang, not an error).
        static void LendGlyphs(TMP_FontAsset host)
        {
            if (host == null) return;
            TMP_FontAsset g = Glyphs;
            if (g == null || g == host) return;
            Chain(host, g);
        }

        static void Chain(TMP_FontAsset host, TMP_FontAsset tail)
        {
            if (host == null || tail == null || host == tail) return;
            try
            {
                List<TMP_FontAsset> table = host.fallbackFontAssetTable;
                if (table == null)
                {
                    table = new List<TMP_FontAsset>();
                    host.fallbackFontAssetTable = table;
                }
                // TMP walks this table with `i < Count && table[i] != null` and so
                // STOPS DEAD at the first hole. The baked hand shipped with three
                // empty slots at the top, which is how a perfectly good glyph face
                // sat in the list and was never once asked for a character.
                for (int i = table.Count - 1; i >= 0; i--)
                    if (table[i] == null) table.RemoveAt(i);
                if (!table.Contains(tail)) table.Insert(0, tail);
            }
            catch (Exception) { }
        }

        // ── the metrics Godot lays type out on ─────────────────────────────────

        /// Godot's default theme drops three pixels between the lines of a Label. It
        /// is a constant and not a ratio, so at 19px it is a tenth of the line and at
        /// 58px it is a twentieth — which is exactly why a single "line spacing"
        /// number ported from one screen was wrong on the next.
        public const float LabelLeading = 3f;

        /// draw_string() and draw_multiline_string() go through no theme at all, so
        /// they get none of it.
        public const float StringLeading = 0f;

        static float AscentRatio(TMP_FontAsset f)
        {
            if (f != null && f.faceInfo.pointSize > 0f)
                return f.faceInfo.ascentLine / f.faceInfo.pointSize;
            return 1.042f;    // Patrick Hand's hhea ascender, 1042/1000
        }

        static float DescentRatio(TMP_FontAsset f)
        {
            if (f != null && f.faceInfo.pointSize > 0f)
                return -f.faceInfo.descentLine / f.faceInfo.pointSize;
            return 0.312f;    // Patrick Hand's hhea descender, -312/1000
        }

        static float LineRatio(TMP_FontAsset f)
        {
            if (f != null && f.faceInfo.pointSize > 0f && f.faceInfo.lineHeight > 0f)
                return f.faceInfo.lineHeight / f.faceInfo.pointSize;
            return AscentRatio(f) + DescentRatio(f);
        }

        /// How far below the top of a line its baseline sits. draw_string() is given
        /// a BASELINE and a TMP label is given a TOP, so every transcribed
        /// draw_string() subtracts this. It is the font's own ascent, unrounded,
        /// because that is the number TMP itself uses to place the first line.
        public static float Ascent(TMP_FontAsset f, float size)
        {
            return AscentRatio(f) * size;
        }

        public static float Ascent(float size) { return Ascent(Hand, size); }

        /// Godot's line pitch, expressed in TMP's units.
        ///
        /// TMP lays lines out on a continuous ratio — 1.354 x the size for Patrick
        /// Hand, straight off the hhea table. Godot lays them out the way FreeType
        /// does: the ascent rounded UP to a whole pixel, the descent rounded DOWN,
        /// and then the theme's leading on top. At 30 that is 32 + 10 + 3 = 45px
        /// against TMP's 40.6 — the ten per cent every ported body block was tight
        /// by. TMP's lineSpacing is measured in hundredths of the font size, so the
        /// whole difference is handed over as one number, per label, per size.
        public static float GodotLineSpacing(TMP_FontAsset f, float size, float leading)
        {
            if (size <= 0f) return 0f;
            float godot = Mathf.Ceil(AscentRatio(f) * size)
                        + Mathf.Ceil(DescentRatio(f) * size) + leading;
            float tmp = LineRatio(f) * size;
            return 100f * (godot - tmp) / size;
        }

        // ── rects, in Godot's top-left space ───────────────────────────────────

        /// A child rect at Godot coordinates: (x, y) is its TOP-LEFT inside `parent`.
        public static RectTransform Rect(RectTransform parent, string name,
                                         float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
            return rt;
        }

        /// A child rect that fills its parent — Control.PRESET_FULL_RECT.
        public static RectTransform FullRect(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// Move a rect built by Rect() without touching its anchors.
        public static void SetTopLeft(RectTransform rt, float x, float y)
        {
            rt.anchoredPosition = new Vector2(x, -y);
        }

        public static float TopLeftY(RectTransform rt)
        {
            return -rt.anchoredPosition.y;
        }

        // ── flat fills ─────────────────────────────────────────────────────────

        /// A solid rectangle. An Image with no sprite draws its colour, which is the
        /// closest thing Unity has to draw_rect().
        public static Image Fill(RectTransform parent, string name, Color color,
                                 float x, float y, float w, float h)
        {
            var rt = Rect(parent, name, x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Image FullFill(RectTransform parent, string name, Color color,
                                     bool blocksClicks = false)
        {
            var rt = FullRect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = blocksClicks;
            return img;
        }

        // ── handwriting ────────────────────────────────────────────────────────

        /// A Label: (x, y) is the TOP-LEFT of the text box, exactly like Label.position.
        /// `width` 0 means "no wrapping"; any other width wraps like custom_minimum_size.
        /// `leading` is Godot's Label theme constant — leave it alone for a Label and
        /// pass StringLeading for a transcribed draw_multiline_string(), which has no
        /// theme under it.
        public static TextMeshProUGUI HandLabel(RectTransform parent, string text,
                                                float x, float y, float size, Color color,
                                                float width = 0f,
                                                TextAlignmentOptions align = TextAlignmentOptions.TopLeft,
                                                float leading = LabelLeading)
        {
            return Written(Hand, parent, text, x, y, size, color, width, align, leading);
        }

        /// The same label in the display hand — every heading, pip label and paper
        /// caption the .gd screens set in `_font_d`.
        public static TextMeshProUGUI DisplayLabel(RectTransform parent, string text,
                                                   float x, float y, float size, Color color,
                                                   float width = 0f,
                                                   TextAlignmentOptions align = TextAlignmentOptions.TopLeft,
                                                   float leading = LabelLeading)
        {
            return Written(Display, parent, text, x, y, size, color, width, align, leading);
        }

        static TextMeshProUGUI Written(TMP_FontAsset font, RectTransform parent, string text,
                                       float x, float y, float size, Color color,
                                       float width, TextAlignmentOptions align, float leading)
        {
            float w = width > 0f ? width : RunwayPaths.StageWidth;
            var rt = Rect(parent, "label", x, y, w, size * 4f);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = width > 0f ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            // Godot's pitch, not TMP's — set BEFORE the box is measured, or the box is
            // sized for lines that are about to move apart
            t.lineSpacing = GodotLineSpacing(font, size, leading);
            // the box grows down from its top-left, never re-centres the line
            t.rectTransform.sizeDelta = new Vector2(w, Mathf.Max(size * 1.6f, PreferredHeight(t, w)));
            return t;
        }

        /// A centred line across the whole stage at Godot BASELINE y — the shape
        /// draw_string() takes. draw_string is handed a baseline and TMP is handed a
        /// top, so the font's own ascent is subtracted: 1.042 of the size for Patrick
        /// Hand, off its hhea table, not the 0.78 this used to guess.
        public static TextMeshProUGUI InkString(RectTransform parent, string text,
                                                float baselineY, float size, Color color,
                                                float stageWidth = 0f)
        {
            float w = stageWidth > 0f ? stageWidth : RunwayPaths.StageWidth;
            var t = HandLabel(parent, text, 0f, baselineY - Ascent(Hand, size), size, color, w,
                              TextAlignmentOptions.Top, StringLeading);
            return t;
        }

        static float PreferredHeight(TMP_Text t, float width)
        {
            try { return t.GetPreferredValues(t.text, width, 0f).y; }
            catch (Exception) { return t.fontSize * 1.6f; }
        }

        static TextMeshProUGUI _ruler;
        static RectTransform _rulerHost;

        /// Width of a string, for every place the original sizes a card from its word.
        /// Godot's get_string_size() is a sum of ADVANCES — where the pen ends up. TMP's
        /// GetPreferredValues() is a box, and the box carries the atlas padding the SDF
        /// needs on both sides, which made every measured card two dozen pixels wide of
        /// Godot's. The pen position is read straight off the shaped text instead.
        public static float MeasureWidth(string text, float size)
        {
            return MeasureWidth(text, size, null);
        }

        public static float MeasureWidth(string text, float size, TMP_FontAsset font)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            if (_ruler == null)
            {
                RectTransform host = RulerHost();
                if (host == null) return text.Length * size * 0.5f;
                var rt = Rect(host, "ruler", -5000f, -5000f, 4000f, size * 3f);
                _ruler = rt.gameObject.AddComponent<TextMeshProUGUI>();
                _ruler.raycastTarget = false;
                _ruler.textWrappingMode = TextWrappingModes.NoWrap;
                _ruler.overflowMode = TextOverflowModes.Overflow;
            }
            TMP_FontAsset f = font != null ? font : Hand;
            if (f != null) _ruler.font = f;
            _ruler.fontSize = size;
            _ruler.lineSpacing = 0f;
            _ruler.text = text;
            try
            {
                _ruler.ForceMeshUpdate(true, true);
                TMP_TextInfo info = _ruler.textInfo;
                if (info != null && info.characterCount > 0)
                {
                    float pen = info.characterInfo[info.characterCount - 1].xAdvance;
                    if (pen > 0f) return pen;
                }
            }
            catch (Exception) { }
            try { return _ruler.GetPreferredValues(text).x; }
            catch (Exception) { return text.Length * size * 0.5f; }
        }

        /// The stage when there is one; a parked canvas of its own when there is not,
        /// so an editor probe or a test can measure type without booting the game.
        static RectTransform RulerHost()
        {
            RectTransform stage = Boot.Instance != null ? Boot.Instance.Stage : null;
            if (stage != null) return stage;
            if (_rulerHost == null)
            {
                var go = new GameObject("~runway-ruler", typeof(RectTransform), typeof(Canvas));
                go.hideFlags = HideFlags.HideAndDontSave;
                var canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _rulerHost = go.GetComponent<RectTransform>();
            }
            return _rulerHost;
        }

        // ── paper ──────────────────────────────────────────────────────────────

        /// The style knobs the .gd cards differ on. Defaults are the title screen's
        /// paper button — the card the owner signed off as "a REAL paper button".
        public struct PaperStyle
        {
            public Vector2 ShadowOffset;
            public float ShadowAlpha;
            public float Inset;       // the ink edge sits this far inside the paper
            public int StepsPerEdge;
            public float Jitter;
            public float Thickness;
            public int Seed;

            public static PaperStyle Button
            {
                get
                {
                    return new PaperStyle
                    {
                        ShadowOffset = new Vector2(4f, 5f),
                        ShadowAlpha = 0.35f,
                        Inset = 2f,
                        StepsPerEdge = 10,
                        Jitter = 1.6f,
                        Thickness = 3.5f,
                        Seed = 12,
                    };
                }
            }

            /// The keys screen / how-to sheet: a bigger hand on a bigger sheet.
            public static PaperStyle Sheet
            {
                get
                {
                    return new PaperStyle
                    {
                        ShadowOffset = new Vector2(8f, 12f),
                        ShadowAlpha = 0.30f,
                        Inset = 3f,
                        StepsPerEdge = 16,
                        Jitter = 2f,
                        Thickness = 4f,
                        Seed = 6,
                    };
                }
            }
        }

        /// A cream sheet with a drop shadow and a hand-wobbled ink edge. The returned
        /// rect is the PAPER: children positioned inside it use paper coordinates.
        public static RectTransform PaperCard(RectTransform parent, Vector2 size,
                                              float x = 0f, float y = 0f,
                                              PaperStyle? style = null, string name = "paper")
        {
            PaperStyle st = style ?? PaperStyle.Button;
            var root = Rect(parent, name, x, y, size.x, size.y);

            var shadow = Fill(root, "shadow", new Color(0f, 0f, 0f, st.ShadowAlpha),
                              st.ShadowOffset.x, st.ShadowOffset.y, size.x, size.y);
            shadow.raycastTarget = false;

            var sheet = Fill(root, "sheet", Cream, 0f, 0f, size.x, size.y);
            sheet.raycastTarget = false;

            AddInkEdge(root, size, st);
            return root;
        }

        /// The wobbled ink rectangle on its own — for a card that already has a body.
        public static Image AddInkEdge(RectTransform root, Vector2 size, PaperStyle st)
        {
            int pad = Mathf.CeilToInt(st.Thickness + st.Jitter + 2f);
            Sprite s = WobbleRectSprite(Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y),
                                       st.Inset, st.Thickness, st.StepsPerEdge, st.Jitter,
                                       st.Seed, pad);
            var rt = Rect(root, "edge", -pad, -pad, size.x + pad * 2f, size.y + pad * 2f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = s;
            img.type = Image.Type.Simple;
            img.color = Ink;
            img.raycastTarget = false;
            DrawnBoil.Apply(img, st.Seed);
            return img;
        }

        /// A single hand-wobbled rule (keys screen's coral underline, the paper input's
        /// writing line). Baked the same way the edge is.
        public static Image Rule(RectTransform parent, float x, float y, float length,
                                 Color color, float thickness = 4f, int seed = 4,
                                 float jitter = 1.5f, int samples = 21)
        {
            int pad = Mathf.CeilToInt(thickness + jitter + 2f);
            Sprite s = WobbleLineSprite(Mathf.RoundToInt(length), thickness, samples, jitter,
                                        seed, pad);
            var rt = Rect(parent, "rule", x - pad, y - pad, length + pad * 2f, pad * 2f + 1f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = s;
            img.type = Image.Type.Simple;
            img.color = color;
            img.raycastTarget = false;
            DrawnBoil.Apply(img, seed);
            return img;
        }

        // ── buttons ────────────────────────────────────────────────────────────

        /// A paper button: cream card, wobbled edge, and the word re-issued ABOVE the
        /// paper — the exact assembly title_screen.gd builds, for the exact reason
        /// (the card paints over a Button's own label).
        ///
        /// THE WORD IS IN THE WRITING HAND UNLESS A CALLER SAYS OTHERWISE. title_screen.gd
        /// loads Patrick Hand and nothing else, so its cards stay in the hand; the draft's
        /// `_paper_card` re-issues its caption in `_font_d`, so every card on that flow
        /// passes `DrawnUI.Display`. One optional argument keeps both true at once.
        public static Button PaperButton(RectTransform parent, string text,
                                         float x, float y, float w, float h,
                                         float fontSize, Color word, Color wordHover,
                                         Action onClick, float hoverScale = 1.045f,
                                         PaperStyle? style = null,
                                         TMP_FontAsset font = null)
        {
            var rt = Rect(parent, "paperbutton", x, y, w, h);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x + w * 0.5f, -(y + h * 0.5f));

            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            var inner = FullRect(rt, "inner");
            var card = PaperCard(inner, new Vector2(w, h), 0f, 0f, style);
            card.anchorMin = new Vector2(0f, 1f);
            card.anchorMax = new Vector2(0f, 1f);
            card.pivot = new Vector2(0f, 1f);
            card.anchoredPosition = Vector2.zero;

            var label = Written(font != null ? font : Hand, inner, text, 0f, 0f, fontSize,
                                word, w, TextAlignmentOptions.Center, LabelLeading);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = hit;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var tint = rt.gameObject.AddComponent<HoverTint>();
            tint.Setup(label, word, wordHover, rt, hoverScale);
            return btn;
        }

        /// A flat word with no paper under it: the keys screen's two doors, the title's
        /// corner links, "← back". flat = true in Godot, every stylebox emptied.
        ///
        /// Same font rule as PaperButton: the hand by default, and the display hand for
        /// the callers whose Godot original stands on `_ink_button`, which sets `_font_d`.
        public static Button FlatButton(RectTransform parent, string text,
                                        float x, float y, float w, float h,
                                        float fontSize, Color word, Color wordHover,
                                        Action onClick,
                                        TextAlignmentOptions align = TextAlignmentOptions.Left,
                                        TMP_FontAsset font = null)
        {
            var rt = Rect(parent, "flatbutton", x, y, w, h);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            var label = Written(font != null ? font : Hand, rt, text, 0f, 0f, fontSize,
                                word, w, align, LabelLeading);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            label.alignment = align == TextAlignmentOptions.Left
                ? TextAlignmentOptions.MidlineLeft
                : (align == TextAlignmentOptions.Center ? TextAlignmentOptions.Center : align);

            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = hit;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var tint = rt.gameObject.AddComponent<HoverTint>();
            tint.Setup(label, word, wordHover, null, 1f);
            return btn;
        }

        // ── fades and easings (Godot tweens, as coroutines) ────────────────────

        public static CanvasGroup Group(RectTransform rt)
        {
            var g = rt.GetComponent<CanvasGroup>();
            if (g == null) g = rt.gameObject.AddComponent<CanvasGroup>();
            return g;
        }

        public static IEnumerator FadeTo(CanvasGroup g, float to, float secs)
        {
            if (g == null) yield break;
            float from = g.alpha;
            float t = 0f;
            while (t < secs)
            {
                t += Time.unscaledDeltaTime;
                if (g == null) yield break;
                g.alpha = Mathf.Lerp(from, to, secs <= 0f ? 1f : Mathf.Clamp01(t / secs));
                yield return null;
            }
            if (g != null) g.alpha = to;
        }

        /// TRANS_CUBIC + EASE_OUT, the ease every menu card rises on.
        public static float EaseOutCubic(float k)
        {
            k = Mathf.Clamp01(k) - 1f;
            return k * k * k + 1f;
        }

        public static float EaseInOutCubic(float k)
        {
            k = Mathf.Clamp01(k);
            if (k < 0.5f) return 4f * k * k * k;
            float f = 2f * k - 2f;
            return 0.5f * f * f * f + 1f;
        }

        public static IEnumerator RiseTo(RectTransform rt, float fromTopY, float toTopY, float secs)
        {
            if (rt == null) yield break;
            float t = 0f;
            SetTopLeftKeepPivot(rt, fromTopY);
            while (t < secs)
            {
                t += Time.unscaledDeltaTime;
                if (rt == null) yield break;
                float k = EaseOutCubic(secs <= 0f ? 1f : t / secs);
                SetTopLeftKeepPivot(rt, Mathf.Lerp(fromTopY, toTopY, k));
                yield return null;
            }
            if (rt != null) SetTopLeftKeepPivot(rt, toTopY);
        }

        /// Works for both rect conventions: a centre-pivot button keeps its own offset.
        static void SetTopLeftKeepPivot(RectTransform rt, float topY)
        {
            float half = rt.pivot.y == 1f ? 0f : rt.rect.height * (1f - rt.pivot.y);
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -(topY + half));
        }

        // ══ the ink rasteriser ═════════════════════════════════════════════════

        static readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();

        /// The wobbled rectangle every card in the game wears. Baked once per
        /// (size, style) and held for the session.
        public static Sprite WobbleRectSprite(int w, int h, float inset, float thickness,
                                              int stepsPerEdge, float jitter, int seed, int pad)
        {
            string key = string.Format("r|{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}",
                w, h, inset, thickness, stepsPerEdge, jitter, seed, pad);
            Sprite cached;
            if (_sprites.TryGetValue(key, out cached) && cached != null) return cached;

            int tw = Mathf.Max(w + pad * 2, 4);
            int th = Mathf.Max(h + pad * 2, 4);
            Color32[] px = NewCanvas(tw, th);

            var rng = new System.Random(seed);
            var pts = new List<Vector2>();
            var corners = new[]
            {
                new Vector2(inset, inset),
                new Vector2(w - inset, inset),
                new Vector2(w - inset, h - inset),
                new Vector2(inset, h - inset),
            };
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = corners[i];
                Vector2 b = corners[(i + 1) % 4];
                for (int k = 0; k < stepsPerEdge; k++)
                {
                    Vector2 p = Vector2.Lerp(a, b, (float)k / stepsPerEdge);
                    pts.Add(new Vector2(p.x + Rand(rng, jitter) + pad,
                                        p.y + Rand(rng, jitter) + pad));
                }
            }
            pts.Add(pts[0]);
            StrokePolyline(px, tw, th, pts, thickness);

            Sprite s = Bake(px, tw, th);
            _sprites[key] = s;
            return s;
        }

        /// One wobbled horizontal rule, baked the same way.
        public static Sprite WobbleLineSprite(int length, float thickness, int samples,
                                              float jitter, int seed, int pad)
        {
            string key = string.Format("l|{0}|{1}|{2}|{3}|{4}|{5}",
                length, thickness, samples, jitter, seed, pad);
            Sprite cached;
            if (_sprites.TryGetValue(key, out cached) && cached != null) return cached;

            int tw = Mathf.Max(length + pad * 2, 4);
            int th = Mathf.Max(pad * 2 + 1, 4);
            Color32[] px = NewCanvas(tw, th);

            var rng = new System.Random(seed);
            var pts = new List<Vector2>();
            int n = Mathf.Max(samples, 2);
            for (int i = 0; i < n; i++)
            {
                float fx = (float)i / (n - 1);
                pts.Add(new Vector2(pad + length * fx, th * 0.5f + Rand(rng, jitter)));
            }
            StrokePolyline(px, tw, th, pts, thickness);

            Sprite s = Bake(px, tw, th);
            _sprites[key] = s;
            return s;
        }

        /// The same rule stood on its end — the curtain's meeting edge.
        public static Sprite WobbleVLineSprite(int length, float thickness, int samples,
                                               float jitter, int seed, int pad)
        {
            string key = string.Format("v|{0}|{1}|{2}|{3}|{4}|{5}",
                length, thickness, samples, jitter, seed, pad);
            Sprite cached;
            if (_sprites.TryGetValue(key, out cached) && cached != null) return cached;

            int tw = Mathf.Max(pad * 2 + 1, 4);
            int th = Mathf.Max(length + pad * 2, 4);
            Color32[] px = NewCanvas(tw, th);

            var rng = new System.Random(seed);
            var pts = new List<Vector2>();
            int n = Mathf.Max(samples, 2);
            for (int i = 0; i < n; i++)
            {
                float fy = (float)i / (n - 1);
                pts.Add(new Vector2(tw * 0.5f + Rand(rng, jitter), pad + length * fy));
            }
            StrokePolyline(px, tw, th, pts, thickness);

            Sprite s = Bake(px, tw, th);
            _sprites[key] = s;
            return s;
        }

        /// A hand-wobbled ring — the how-to page dots.
        public static Sprite RingSprite(float radius, float thickness, float jitter,
                                        int seed, int pad, bool filled)
        {
            string key = string.Format("o|{0}|{1}|{2}|{3}|{4}|{5}",
                radius, thickness, jitter, seed, pad, filled);
            Sprite cached;
            if (_sprites.TryGetValue(key, out cached) && cached != null) return cached;

            int side = Mathf.Max(Mathf.CeilToInt(radius * 2f) + pad * 2, 4);
            Color32[] px = NewCanvas(side, side);
            var c = new Vector2(side * 0.5f, side * 0.5f);
            if (filled) Disc(px, side, side, c.x, c.y, radius, new Color32(255, 255, 255, 255));

            var rng = new System.Random(seed);
            var pts = new List<Vector2>();
            for (int i = 0; i < 17; i++)
            {
                float a = Mathf.PI * 2f * i / 16f;
                float r = radius + Rand(rng, jitter);
                pts.Add(new Vector2(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r));
            }
            StrokePolyline(px, side, side, pts, thickness);

            Sprite s = Bake(px, side, side);
            _sprites[key] = s;
            return s;
        }

        /// A solid disc with nothing drawn round it — draw_circle(). The how-to's
        /// current page dot is a coral disc with an INK ring laid over it, two
        /// different colours, so the fill cannot ride inside the ring's own sprite:
        /// one white bake tinted once comes out coral ring on coral disc.
        public static Sprite DiscSprite(float radius, int pad)
        {
            string key = string.Format("d|{0}|{1}", radius, pad);
            Sprite cached;
            if (_sprites.TryGetValue(key, out cached) && cached != null) return cached;

            int side = Mathf.Max(Mathf.CeilToInt(radius * 2f) + pad * 2, 4);
            Color32[] px = NewCanvas(side, side);
            Disc(px, side, side, side * 0.5f, side * 0.5f, radius, new Color32(255, 255, 255, 255));

            Sprite s = Bake(px, side, side);
            _sprites[key] = s;
            return s;
        }

        /// The side of the texture RingSprite/DiscSprite bake at that radius — a host
        /// rect wants the sprite's own size, or the wobble is stretched into a wider
        /// dot than the one Godot drew.
        public static int RingSide(float radius, int pad)
        {
            return Mathf.Max(Mathf.CeilToInt(radius * 2f) + pad * 2, 4);
        }

        static float Rand(System.Random rng, float amount)
        {
            return (float)(rng.NextDouble() * 2.0 - 1.0) * amount;
        }

        static Color32[] NewCanvas(int w, int h)
        {
            var px = new Color32[w * h];
            var clear = new Color32(255, 255, 255, 0);
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            return px;
        }

        static void StrokePolyline(Color32[] px, int w, int h, List<Vector2> pts, float thickness)
        {
            float r = Mathf.Max(thickness * 0.5f, 0.5f);
            var white = new Color32(255, 255, 255, 255);
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                Vector2 a = pts[i];
                Vector2 b = pts[i + 1];
                float len = Vector2.Distance(a, b);
                int steps = Mathf.Max(Mathf.CeilToInt(len * 2f), 1);
                for (int s = 0; s <= steps; s++)
                {
                    Vector2 p = Vector2.Lerp(a, b, (float)s / steps);
                    Disc(px, w, h, p.x, p.y, r, white);
                }
            }
        }

        /// One soft-edged dot of ink. Alpha-max rather than alpha-blend, so a stroke
        /// crossing itself does not darken into a blot.
        static void Disc(Color32[] px, int w, int h, float cx, float cy, float r, Color32 col)
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
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    if (a <= 0f) continue;
                    int idx = y * w + x;
                    byte want = (byte)Mathf.RoundToInt(a * col.a);
                    if (px[idx].a >= want) continue;
                    px[idx] = new Color32(col.r, col.g, col.b, want);
                }
            }
        }

        /// The canvas is built top-left down, the way every .gd file reads; Unity
        /// textures start bottom-left, so the rows are flipped exactly once, here.
        static Sprite Bake(Color32[] px, int w, int h)
        {
            var flipped = new Color32[px.Length];
            for (int y = 0; y < h; y++)
            {
                int src = y * w;
                int dst = (h - 1 - y) * w;
                Array.Copy(px, src, flipped, dst, w);
            }
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
