using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;

namespace Runway.Screens
{
    /// <summary>
    /// HOW THIS WORLD WORKS — howto_screen.gd, ported. The old static four-panel sheet
    /// graded 20%, so the rules are taught as three pages, each one a baked loop
    /// playing large inside an inked film frame, with the REAL rule the engine runs
    /// written underneath it in plain words. Shown once, then GOT IT.
    /// </summary>
    public sealed class HowToScreen : AppScreen
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            ScreenRegistry.Register(AppState.HowTo, typeof(HowToScreen));
            ScreenRegistry.RegisterOverlay(AppOverlay.HowTo, typeof(HowToScreen));
        }

        // the baked loops: 5x8 grids of 1024x576 frames, one page each
        const int LoopCols = 5;
        const int LoopFrames = 40;
        const float LoopFps = 12f;

        static readonly UnityEngine.Rect SheetR = new UnityEngine.Rect(88f, 24f, 1360f, 976f);
        static readonly UnityEngine.Rect FrameR = new UnityEngine.Rect(198f, 116f, 1140f, 642f);

        static readonly string[] Titles =
        {
            "YOU WRITE. THE DIE DECIDES.",
            "THE WORLD ANSWERS.",
            "MONEY IS THE FOOD.",
        };

        static readonly string[] Caps =
        {
            "Write your week's move in the journal. A d20 rolls the moment you commit — your five muscles (build, sell, raise, recruit, grit) add to it, the world sets the difficulty.",
            "Beat the difficulty by 5 and it's brilliant. Miss by 3 and it backfires, expensively. Cash moves, people remember, promises come due.",
            "Rent, payroll and every budget burn weekly. Set marketing, sales, care and R&D in THE LEDGER (TAB). A customer costs money to win and pays back over their stay. Three weeks below zero and it's over.",
        };

        static readonly string[] Loops =
        {
            "title/howto_1.png",
            "title/howto_2.png",
            "title/howto_3.png",
        };

        const string SeenFile = "seen_howto_v2.unity";

        /// user://seen_howto_v2 — a versioned flag, so the video tutorial shows once
        /// even for veterans of the old sheet.
        public static bool Seen
        {
            get
            {
                try { return System.IO.File.Exists(RunwayPaths.User(SeenFile)); }
                catch (System.Exception) { return false; }
            }
        }

        SheetLoop _loop;
        RectTransform _frame;
        TextMeshProUGUI _title;
        TextMeshProUGUI _caption;
        RectTransform _button;
        TextMeshProUGUI _word;
        readonly List<Image> _dots = new List<Image>();   // the coral disc + its ink ring
        readonly List<Image> _off = new List<Image>();    // the dim ring for every other page
        int _page;
        int _count = 3;

        protected override void OnBuild()
        {
            DrawnUI.FullFill(Rect, "bg", DrawnUI.Hex("22262B"), true);

            // no loop shipped at all: one page, never blank
            bool any = false;
            for (int i = 0; i < Loops.Length; i++) if (RunwayPaths.ArtExists(Loops[i])) any = true;
            _count = any ? 3 : 1;

            // the page furniture, which never moves
            var sheet = DrawnUI.PaperCard(Rect, new Vector2(SheetR.width, SheetR.height),
                                          SheetR.x, SheetR.y, DrawnUI.PaperStyle.Sheet, "sheet");

            _frame = DrawnUI.Rect(Rect, "frame", FrameR.x, FrameR.y, FrameR.width, FrameR.height);
            var backing = _frame.gameObject.AddComponent<Image>();
            backing.color = DrawnUI.Cream;
            backing.raycastTarget = false;
            _loop = SheetLoop.Attach(_frame, "loop");

            // the film frame: thick ink edge, sprocket ticks down both outer margins
            var edgeHost = DrawnUI.Rect(Rect, "film", FrameR.x - 4f, FrameR.y - 4f,
                                        FrameR.width + 8f, FrameR.height + 8f);
            DrawnUI.AddInkEdge(edgeHost, new Vector2(FrameR.width + 8f, FrameR.height + 8f),
                               new DrawnUI.PaperStyle
                               {
                                   ShadowOffset = Vector2.zero,
                                   ShadowAlpha = 0f,
                                   Inset = 0f,
                                   StepsPerEdge = 14,
                                   Jitter = 2.2f,
                                   Thickness = 6f,
                                   Seed = 9,
                               });
            int holes = Mathf.FloorToInt(FrameR.height / 78f);
            for (int i = 0; i < holes; i++)
            {
                float y = FrameR.y + 14f + (FrameR.height - 28f) * i / Mathf.Max(holes - 1, 1);
                var tick = DrawnUI.WithAlpha(DrawnUI.Ink, 0.4f);
                DrawnUI.Fill(Rect, "sprocket", tick, FrameR.x - 31f, y - 9f, 14f, 18f);
                DrawnUI.Fill(Rect, "sprocket", tick, FrameR.xMax + 17f, y - 9f, 14f, 18f);
            }

            // both of these are draw_string()/draw_multiline_string() in the original:
            // the y they are given is a BASELINE, and neither passes through a theme,
            // so neither gets the Label leading
            _title = DrawnUI.InkString(Rect, Titles[0], 90f, 56f, DrawnUI.Ink);
            _caption = DrawnUI.HandLabel(Rect, Caps[0], 238f,
                                         812f - DrawnUI.Ascent(DrawnUI.Hand, 30f), 30f,
                                         DrawnUI.WithAlpha(DrawnUI.Ink, 0.85f), 1060f,
                                         TextAlignmentOptions.Top, DrawnUI.StringLeading);

            BuildDots();
            BuildButton();

            // the whole page is a click target too — nobody hunts for the button
            var hit = DrawnUI.FullFill(Rect, "page", new Color(0f, 0f, 0f, 0f), true);
            hit.transform.SetSiblingIndex(1);   // under the furniture, over the background
            var pageBtn = hit.gameObject.AddComponent<Button>();
            pageBtn.transition = Selectable.Transition.None;
            pageBtn.targetGraphic = hit;
            pageBtn.onClick.AddListener(Advance);

            Show(0);
        }

        /// Three page dots, in Godot's three layers. The one you are on is a solid
        /// coral disc at r=11 with a full-strength INK ring drawn over it; the others
        /// are a single dim ring at r=9. Each host is the size the sprite BAKES at —
        /// a 28px ring stretched into a 32px box is a dot a sixth too big, and the
        /// hand-drawn wobble stretches with it.
        void BuildDots()
        {
            const float Cy = 952f;
            int onSide = DrawnUI.RingSide(11f, 3);    // the current dot, disc and ring
            int offSide = DrawnUI.RingSide(9f, 3);    // the others
            for (int i = 0; i < _count; i++)
            {
                float cx = 768f + (i - (_count - 1) * 0.5f) * 46f;

                var offRt = DrawnUI.Rect(Rect, "dot", cx - offSide * 0.5f, Cy - offSide * 0.5f,
                                         offSide, offSide);
                var offImg = offRt.gameObject.AddComponent<Image>();
                offImg.sprite = DrawnUI.RingSprite(9f, 3f, 1.2f, 20 + i, 3, false);
                offImg.color = DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f);
                offImg.raycastTarget = false;

                var discRt = DrawnUI.Rect(Rect, "dot-on", cx - onSide * 0.5f, Cy - onSide * 0.5f,
                                          onSide, onSide);
                var discImg = discRt.gameObject.AddComponent<Image>();
                discImg.sprite = DrawnUI.DiscSprite(11f, 3);
                discImg.color = DrawnUI.Pen;
                discImg.raycastTarget = false;

                var ringRt = DrawnUI.FullRect(discRt, "ring");
                var ringImg = ringRt.gameObject.AddComponent<Image>();
                ringImg.sprite = DrawnUI.RingSprite(11f, 3f, 1.2f, 20 + i, 3, false);
                ringImg.color = DrawnUI.Ink;
                ringImg.raycastTarget = false;

                _off.Add(offImg);
                _dots.Add(discImg);
            }
        }

        void BuildButton()
        {
            _button = DrawnUI.Rect(Rect, "next", SheetR.xMax - 48f - 300f, 918f, 300f, 70f);
            var hit = _button.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            // the card itself is baked per size by RebuildCard, from Show()
            _word = DrawnUI.HandLabel(_button, "NEXT  →", 0f, 0f, 34f, DrawnUI.Pen, 300f,
                                      TextAlignmentOptions.Center);
            _word.rectTransform.anchorMin = Vector2.zero;
            _word.rectTransform.anchorMax = Vector2.one;
            _word.rectTransform.offsetMin = Vector2.zero;
            _word.rectTransform.offsetMax = Vector2.zero;

            var btn = _button.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = hit;
            btn.onClick.AddListener(Advance);
        }

        /// next page, or the door out on the last one
        void Advance()
        {
            if (_page + 1 < _count) Show(_page + 1);
            else Leave();
        }

        void Leave()
        {
            if (Finished) return;   // a click landing on the button fires both doors
            RunwayPaths.WriteAllText(RunwayPaths.User(SeenFile), "1");
            Finish();
        }

        /// page furniture + the loop for it, loaded one at a time: each sheet is a
        /// 5120x4608 texture and three of them at once is memory nobody needs
        void Show(int i)
        {
            _page = i;
            _title.text = Titles[Mathf.Clamp(i, 0, Titles.Length - 1)];
            _caption.text = Caps[Mathf.Clamp(i, 0, Caps.Length - 1)];

            if (_loop != null)
            {
                _loop.Release();
                if (RunwayPaths.ArtExists(Loops[i])) _loop.PlaySheet(Loops[i], LoopCols, LoopFrames, LoopFps);
            }

            for (int d = 0; d < _dots.Count; d++)
            {
                bool on = d == _page;
                _dots[d].gameObject.SetActive(on);     // the disc carries the ink ring
                if (d < _off.Count) _off[d].enabled = !on;
            }

            // the long word rides a notch smaller, or its card crowds the page dots
            bool last = _page + 1 >= _count;
            string txt = last ? "GOT IT — LET'S FOUND SOMETHING  →" : "NEXT  →";
            float sz = last ? 32f : 34f;
            float w = DrawnUI.MeasureWidth(txt, sz) + 68f;
            _word.text = txt;
            _word.fontSize = sz;
            _button.sizeDelta = new Vector2(w, 70f);
            DrawnUI.SetTopLeft(_button, SheetR.xMax - 48f - w, 918f);
            RebuildCard(w);
        }

        /// the card is baked per size, so a resized button gets a fresh edge
        void RebuildCard(float w)
        {
            var old = _button.Find("card");
            if (old != null) Destroy(old.gameObject);
            var style = DrawnUI.PaperStyle.Button;
            style.ShadowAlpha = 0.28f;
            style.Seed = 12;
            var card = DrawnUI.PaperCard(_button, new Vector2(w, 70f), 0f, 0f, style, "card");
            card.SetSiblingIndex(0);
        }
    }
}
