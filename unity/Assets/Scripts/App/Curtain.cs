using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Runway.App
{
    /// <summary>
    /// THE THEATER CURTAIN — curtain.gd, ported. Locking the week drops it INSTANTLY,
    /// because the click always answers, and it rises on whatever the world prepared
    /// behind it: the reading beat, the new room, the next act. The title screen opens
    /// behind it too: the house is shut for a breath, then it parts.
    ///
    /// Drawn, never chrome: two coral drapes with wobbly ink edges and a scalloped
    /// valance. Once it holds shut, the baked sway loop takes the frame over, so the
    /// wait breathes instead of freezing — and the sheet is BORROWED, not kept: 94MB
    /// asked for when the curtain drops and given back the moment it lifts.
    ///
    /// THE CURTAIN CARRIES NO DICE. The pre-rendered cup clip is the one and only die
    /// on screen; while the curtain is shut it only breathes the considering line.
    ///
    ///     var c = Curtain.Create(stage);
    ///     yield return c.Close();     // 0.45s
    ///     ... build what is behind ...
    ///     yield return c.Open();      // 0.55s
    /// </summary>
    public sealed class Curtain : MonoBehaviour
    {
        public const string LoopArt = "title/curtain_loop.png";
        const int LoopCols = 5;
        const int LoopFrames = 40;
        const float LoopFps = 12f;

        /// day one reads differently — main.gd sets this before the drop.
        public string ConsideringLine = "the world considers your week…";

        RectTransform _rt;
        RectTransform _drapes;
        RectTransform _left;
        RectTransform _right;
        RectTransform _valance;
        SheetLoop _sway;
        RawImage _swayImage;
        TextMeshProUGUI _line;
        CanvasGroup _group;
        Image _swallow;

        float _t;          // 0 = fully open (offstage), 1 = fully shut
        float _shutFor;
        bool _loopRequested;
        Coroutine _tween;

        public bool IsShut { get { return _t > 0.98f; } }
        public float Shut { get { return _t; } }
        /// for a caller that wants to fade the whole curtain rather than sweep it
        public CanvasGroup Group { get { return _group; } }

        public static Curtain Create(RectTransform stage)
        {
            var rt = DrawnUI.FullRect(stage, "curtain");
            var c = rt.gameObject.AddComponent<Curtain>();
            c.BuildParts();
            return c;
        }

        void BuildParts()
        {
            _rt = GetComponent<RectTransform>();
            _group = DrawnUI.Group(_rt);
            // shut curtains swallow clicks so nothing behind them can be pressed
            // mid-swap — Godot does it with MOUSE_FILTER_STOP; here it takes a
            // transparent raycast target, because a CanvasGroup blocks nothing on its own
            _swallow = DrawnUI.FullFill(_rt, "swallow", new Color(0f, 0f, 0f, 0f), true);
            float w = RunwayPaths.StageWidth;
            float h = RunwayPaths.StageHeight;

            _drapes = DrawnUI.FullRect(_rt, "drapes");

            _left = Panel("left", true, w, h);
            _right = Panel("right", false, w, h);

            // the valance: a band across the top with twelve scallops hanging off it
            _valance = DrawnUI.Rect(_drapes, "valance", 0f, 0f, w, 46f + w / 24f);
            var band = DrawnUI.Fill(_valance, "band", DrawnUI.CoralDark, 0f, 0f, w, 46f);
            band.raycastTarget = false;
            Sprite disc = DrawnUI.RingSprite(w / 24f, 3f, 1.2f, 41, 3, true);
            for (int s = 0; s < 12; s++)
            {
                float cx = w * (s + 0.5f) / 12f;
                float r = w / 24f;
                var srt = DrawnUI.Rect(_valance, "scallop", cx - r - 3f, 46f - r - 3f,
                                       r * 2f + 6f, r * 2f + 6f);
                var img = srt.gameObject.AddComponent<Image>();
                img.sprite = disc;
                img.color = DrawnUI.CoralDark;
                img.raycastTarget = false;
            }

            // the sway sheet sits over the drapes once it lands
            _sway = SheetLoop.Attach(_rt, "sway");
            _swayImage = _sway.GetComponent<RawImage>();
            _swayImage.enabled = false;

            _line = DrawnUI.HandLabel(_rt, "", 0f, h * 0.5f - 40f * 0.78f, 40f,
                                      new Color(0.95f, 0.92f, 0.83f, 0f), w,
                                      TextAlignmentOptions.Top);

            gameObject.SetActive(false);   // offstage: nothing to breathe until it drops
            Apply();
        }

        RectTransform Panel(string name, bool leftSide, float w, float h)
        {
            var p = DrawnUI.Rect(_drapes, name, 0f, 0f, w * 0.5f + 14f, h);
            var body = p.gameObject.AddComponent<Image>();
            body.color = DrawnUI.Coral;
            body.raycastTarget = false;
            // four darker vertical swags per panel, at fixed fractions of its width
            for (int f = 0; f < 4; f++)
            {
                var swag = new GameObject("fold", typeof(RectTransform), typeof(Image));
                var srt = swag.GetComponent<RectTransform>();
                srt.SetParent(p, false);
                float x0 = 0.18f + 0.22f * f;
                srt.anchorMin = new Vector2(x0, 0f);
                srt.anchorMax = new Vector2(x0 + 0.055f, 1f);
                srt.offsetMin = Vector2.zero;
                srt.offsetMax = Vector2.zero;
                var si = swag.GetComponent<Image>();
                si.color = DrawnUI.WithAlpha(DrawnUI.CoralDark, 0.55f);
                si.raycastTarget = false;
            }
            // the wobbly INK edge on the meeting side
            Sprite edge = DrawnUI.WobbleVLineSprite(Mathf.RoundToInt(h), 4f, 33, 2.5f,
                                                    leftSide ? 41 : 43, 4);
            var ert = new GameObject("edge", typeof(RectTransform), typeof(Image));
            var e = ert.GetComponent<RectTransform>();
            e.SetParent(p, false);
            e.anchorMin = new Vector2(leftSide ? 1f : 0f, 0f);
            e.anchorMax = new Vector2(leftSide ? 1f : 0f, 1f);
            e.pivot = new Vector2(0.5f, 0.5f);
            e.sizeDelta = new Vector2(11f, 0f);
            e.anchoredPosition = Vector2.zero;
            var ei = ert.GetComponent<Image>();
            ei.sprite = edge;
            ei.color = DrawnUI.Ink;
            ei.raycastTarget = false;
            return p;
        }

        // ── the sweep ──────────────────────────────────────────────────────────

        public IEnumerator Close(float secs = 0.45f)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (_swallow != null) _swallow.raycastTarget = true;
            RequestLoop();
            if (_tween != null) StopCoroutine(_tween);
            _tween = StartCoroutine(Sweep(_t, 1f, secs, DrawnUI.EaseOutCubic));
            yield return _tween;
        }

        public IEnumerator Open(float secs = 0.55f)
        {
            if (!gameObject.activeSelf) yield break;   // never opened; nothing to part
            if (_tween != null) StopCoroutine(_tween);
            _tween = StartCoroutine(Sweep(_t, 0f, secs, DrawnUI.EaseInOutCubic));
            yield return _tween;
            if (_swallow != null) _swallow.raycastTarget = false;
            ReleaseLoop();
            gameObject.SetActive(false);
        }

        /// Straight to shut with no sweep — the title's reveal starts from a shut house.
        public void SnapShut()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _t = 1f;
            _shutFor = 0f;
            if (_swallow != null) _swallow.raycastTarget = true;
            RequestLoop();
            Apply();
        }

        IEnumerator Sweep(float from, float to, float secs, Func<float, float> ease)
        {
            float t = 0f;
            while (t < secs)
            {
                t += Time.unscaledDeltaTime;
                _t = Mathf.Lerp(from, to, ease(secs <= 0f ? 1f : t / secs));
                Apply();
                yield return null;
            }
            _t = to;
            Apply();
        }

        void Update()
        {
            if (_t <= 0.98f)
            {
                _shutFor = 0f;
                if (_swayImage != null && _swayImage.enabled) _swayImage.enabled = false;
                if (_line != null) _line.color = DrawnUI.WithAlpha(_line.color, 0f);
                return;
            }
            _shutFor += Time.unscaledDeltaTime;

            // the baked sway IS the curtain once it lands; until then the drapes carry it
            if (_sway != null && _sway.HasArt && _swayImage != null && !_swayImage.enabled)
            {
                _swayImage.enabled = true;
                if (_drapes != null) _drapes.gameObject.SetActive(false);
            }
            if (_shutFor > 0.9f && _line != null)
            {
                _line.text = ConsideringLine;
                float a = Mathf.Clamp01((_shutFor - 0.9f) * 2f)
                          * (0.75f + 0.25f * Mathf.Sin(_shutFor * 2.2f));
                _line.color = new Color(0.95f, 0.92f, 0.83f, a);
                float bob = Mathf.Sin(_shutFor * 1.3f) * 4f;
                DrawnUI.SetTopLeft(_line.rectTransform,
                                   0f, RunwayPaths.StageHeight * 0.5f - 40f * 0.78f + bob);
            }
        }

        void Apply()
        {
            float w = RunwayPaths.StageWidth;
            float half = w * 0.5f * _t;
            float panelW = half + 14f;
            if (_left != null)
            {
                _left.sizeDelta = new Vector2(panelW, RunwayPaths.StageHeight);
                DrawnUI.SetTopLeft(_left, -14f, 0f);
            }
            if (_right != null)
            {
                _right.sizeDelta = new Vector2(panelW, RunwayPaths.StageHeight);
                DrawnUI.SetTopLeft(_right, w - panelW + 14f, 0f);
            }
            if (_valance != null)
            {
                var g = _valance.GetComponent<CanvasGroup>();
                if (g == null) g = _valance.gameObject.AddComponent<CanvasGroup>();
                g.alpha = Mathf.Min(_t * 2f, 1f);
            }
            if (_drapes != null && !_drapes.gameObject.activeSelf && _t <= 0.98f)
                _drapes.gameObject.SetActive(true);
        }

        // ── the borrowed sheet ─────────────────────────────────────────────────

        void RequestLoop()
        {
            if (_loopRequested || _sway == null) return;
            if (!RunwayPaths.ArtExists(LoopArt)) return;
            _loopRequested = true;
            _sway.PlaySheet(LoopArt, LoopCols, LoopFrames, LoopFps);
        }

        void ReleaseLoop()
        {
            if (!_loopRequested) return;
            _loopRequested = false;
            if (_sway != null) _sway.Release();
            if (_swayImage != null) _swayImage.enabled = false;
            if (_drapes != null) _drapes.gameObject.SetActive(true);
        }
    }
}
