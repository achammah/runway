using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;

namespace Runway.Game
{
    /// <summary>
    /// THE BIRTH SCREEN — birth_screen.gd, ported. Shown the instant the founding
    /// papers are signed, while the world bible is generated (owner: "a proper first
    /// loading screen — RUNWAY! and creating your world"). Drawn, breathing, never
    /// chrome, and never a frozen draft page.
    ///
    /// TWO PHASES, ONE CLOCK. THE ARRIVAL plays ONCE — the room is empty and taped
    /// shut, the founder walks in from the left, pulls the tape, kneels — and its last
    /// frame IS the loop's frame 0, so the hand-over is a straight cut with nothing to
    /// see. THE ARRIVAL IS THEN SPENT: its sheet is 59MB that will never be drawn
    /// again, and this screen stays up until the whole world is written, so it is given
    /// back the moment the loop takes over.
    ///
    /// The real painted logotype is the intent; the drawn title is only the fallback
    /// for a build with no title layers on disk.
    /// </summary>
    public sealed class BirthScreen : AppScreen
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            ScreenRegistry.Register(AppState.Birth, typeof(BirthScreen));
        }

        const string IntroArt = "title/birth_intro.png";
        const string LoopArt = "title/birth_loop.png";
        const string TypeArt = "title/layers/type_main.png";
        const int IntroCols = 5;
        const int IntroFrames = 24;
        const int LoopCols = 5;
        const int LoopFrames = 40;
        const float Fps = 12f;

        /// main flips this per phase.
        public string StatusLine = "creating your world";

        SheetLoop _loop;
        RawImage _logo;
        RectTransform _logoRt;
        RectTransform _logoHolder;
        TextMeshProUGUI _line;
        bool _hasArt;
        bool _introDone;
        float _t;

        protected override void OnBuild()
        {
            _hasArt = RunwayPaths.ArtExists(LoopArt) || RunwayPaths.ArtExists(IntroArt);
            DrawnUI.FullFill(Rect, "ground", _hasArt ? DrawnUI.Cream : DrawnUI.Hex("22262B"), true);

            if (_hasArt)
            {
                // THE ART FILLS THE FRAME (owner: "should feel the whole frame like the
                // title") — no card, no border.
                _loop = SheetLoop.Attach(Rect, "birth");
                if (RunwayPaths.ArtExists(IntroArt))
                {
                    _loop.Finished += OnIntroDone;
                    _loop.PlaySheet(IntroArt, IntroCols, IntroFrames, Fps, true);
                }
                else
                {
                    _introDone = true;
                    _loop.PlaySheet(LoopArt, LoopCols, LoopFrames, Fps);
                }
            }
            else
            {
                // the spotlight the run is born under (drawn fallback only)
                var pool = DrawnUI.Rect(Rect, "pool", RunwayPaths.StageWidth * 0.08f,
                    RunwayPaths.StageHeight * 0.13f, RunwayPaths.StageWidth * 0.84f,
                    RunwayPaths.StageHeight * 0.84f);
                var img = pool.gameObject.AddComponent<Image>();
                img.sprite = DrawnUI.RingSprite(48f, 1f, 0f, 5, 2, true);
                img.color = new Color(1f, 1f, 1f, 0.035f);
                img.raycastTarget = false;
            }

            // RUNWAY! — the painted logotype itself, bobbing. It is ink art, so it only
            // reads over the cream loop; the dark fallback keeps the drawn title.
            // THE BOB RIDES A HOLDER, never the logo's own rect: the aspect fit writes
            // that rect the moment the picture lands and would fight the bob for it.
            _logoHolder = DrawnUI.FullRect(Rect, "logoholder");
            if (_hasArt && RunwayPaths.ArtExists(TypeArt))
            {
                float lw = RunwayPaths.StageWidth * 0.62f;
                _logoRt = DrawnUI.Rect(_logoHolder, "logo", (RunwayPaths.StageWidth - lw) * 0.5f,
                                       RunwayPaths.StageHeight * 0.12f, lw, lw * 0.42f);
                _logo = _logoRt.gameObject.AddComponent<RawImage>();
                _logo.raycastTarget = false;
                _logo.enabled = false;
                float boxX = (RunwayPaths.StageWidth - lw) * 0.5f;
                float boxY = RunwayPaths.StageHeight * 0.12f;
                ArtCache.Load(TypeArt, tex =>
                {
                    if (_logo == null || tex == null) return;
                    _logo.texture = tex;
                    _logo.enabled = true;
                    PlaceType(_logoRt, tex, boxX, boxY, lw, lw * 0.6f);
                });
            }
            else
            {
                float ty = _hasArt ? RunwayPaths.StageHeight * 0.17f : RunwayPaths.StageHeight * 0.42f;
                _logoRt = DrawnUI.Rect(_logoHolder, "logo", 0f, ty, RunwayPaths.StageWidth, 170f);
                DrawnUI.HandLabel(_logoRt, "RUNWAY", 0f, 0f, 132f,
                    _hasArt ? DrawnUI.Ink : DrawnUI.Cream, RunwayPaths.StageWidth,
                    TextAlignmentOptions.Top);
                DrawnUI.HandLabel(_logoRt, "!", RunwayPaths.StageWidth * 0.5f + 200f, 0f, 132f,
                    DrawnUI.Pen, 120f, TextAlignmentOptions.Top);
            }

            // creating your world… — ONE label on a soft dark pill (VD2: the
            // halo copies were static while the line animated its dots, so
            // the two drifted into a double-struck smear over the film).
            float ly = _hasArt ? RunwayPaths.StageHeight * 0.90f : RunwayPaths.StageHeight * 0.66f;
            if (_hasArt)
            {
                var pill = DrawnUI.Fill(Rect, "line_pill",
                    new Color(0.13f, 0.15f, 0.17f, 0.55f),
                    RunwayPaths.StageWidth * 0.5f - 250f, ly - 9f, 500f, 52f);
                pill.raycastTarget = false;
            }
            _line = DrawnUI.HandLabel(Rect, StatusLine, 0f, ly, 34f,
                DrawnUI.Cream, RunwayPaths.StageWidth,
                TextAlignmentOptions.Top);
        }

        /// THE TYPE HANGS FROM ITS TOP — it is not centred in a box. Godot draws it
        /// at `Rect2((w - lw) * 0.5, h * 0.12, lw, lh)`: the top edge IS h * 0.12,
        /// and the height simply follows the picture's own aspect.
        ///
        /// `GameUi.Fit` is TextureRect.STRETCH_KEEP_ASPECT_CENTERED, so it centres
        /// what it draws inside the box it is handed. A 980x267 logotype fitted to
        /// 952x571 came out 259px tall in a 571px box — 156px of slack, halved, put
        /// the painted RUNWAY! that far below where Godot pins it. Fit still does
        /// the sizing (it keeps the aspect, and it clamps a taller picture to the
        /// box rather than overflowing it); only the top is pinned back.
        public static void PlaceType(RectTransform rt, Texture2D tex,
                                     float boxX, float boxY, float boxW, float boxH)
        {
            if (rt == null || tex == null) return;
            GameUi.Fit(rt, tex, boxX, boxY, boxW, boxH);
            DrawnUI.SetTopLeft(rt, boxX + (boxW - rt.sizeDelta.x) * 0.5f, boxY);
        }

        void OnIntroDone()
        {
            if (_introDone || _loop == null) return;
            _introDone = true;
            _loop.Release();      // 59MB that will never be drawn again
            _loop.PlaySheet(LoopArt, LoopCols, LoopFrames, Fps);
        }

        /// ONE REPAINT PER BAKED FRAME: the bob and the breath ride the same 12fps clock
        /// the art is drawn at, which is what they look like anyway.
        int _fr = -1;

        void Update()
        {
            _t += Time.unscaledDeltaTime;
            int fr = Mathf.FloorToInt(_t * Fps);
            if (fr == _fr) return;
            _fr = fr;
            if (_logoHolder != null)
                _logoHolder.anchoredPosition = new Vector2(0f, -Mathf.Sin(_t * 1.4f) * 4f);
            if (_line != null)
            {
                int dots = 1 + Mathf.FloorToInt(Mathf.Repeat(_t * 1.6f, 3f));
                _line.text = StatusLine + new string('.', dots);
                float a = 0.82f + 0.18f * Mathf.Sin(_t * 2.4f);   // breathes, never unreadable
                _line.color = DrawnUI.WithAlpha(_hasArt ? DrawnUI.Ink : DrawnUI.Cream, a);
            }
        }
    }
}
