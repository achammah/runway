using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Runway.App;

namespace Runway.Game
{
    /// <summary>
    /// THE SHELF SCROLLS (owner: "scrolling or categories" — both). A ScrollContainer
    /// is a piece of software on a sheet of paper, so this is the smallest thing that
    /// does the job: a clipped viewport, a content rect, the wheel, and a drawn track
    /// with a coral thumb that only moves when the reader does.
    ///
    /// It answers the wheel only while the pointer is actually over the shelf, so a
    /// page with two scrollable things never scrolls the wrong one.
    ///
    /// THE THUMB HANGS OFF THE PAGE, NOT OFF THE TRACK. Godot's `_ShelfBar` is a
    /// Control positioned ON the track, so the `ty` its `_draw()` computes is
    /// track-relative and correct. Unity's thumb is a sibling of the track under the
    /// page, and `DrawnUI.SetTopLeft` writes an anchoredPosition against that PARENT
    /// — so writing Godot's `ty` straight into it teleported the thumb to the top of
    /// the page the moment anything drew. The track's top is captured at Attach and
    /// every draw is absolute again, matching _ShelfBar._draw() exactly:
    ///
    ///     th = max(trackH * clamp(viewH / contentH, 0.1, 1), 30)
    ///     y  = trackTop + 4 + (trackH - 8 - th) * frac
    /// </summary>
    public sealed class ShelfScroll : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        /// draw_line(..., 6.0) — the coral stroke Godot lays the thumb down with.
        const float ThumbW = 6f;
        /// The track is drawn inset 4px at each end, so the thumb travels trackH - 8.
        const float Inset = 4f;
        const float MinThumb = 30f;
        /// Below this there is nothing to scroll and Godot draws no thumb at all.
        const float Dead = 8f;

        RectTransform _content;
        Image _thumb;
        float _viewH;
        float _contentH;
        float _trackTop;
        float _trackH;
        float _scroll;
        /// NaN so the FIRST draw always lands and any invalidation always redraws.
        float _lastDrawn = float.NaN;
        bool _over;

        public float Scroll { get { return _scroll; } }
        public float MaxScroll { get { return Mathf.Max(_contentH - _viewH, 0f); } }
        public float TrackTop { get { return _trackTop; } }
        public float TrackHeight { get { return _trackH; } }

        /// Where the thumb's top edge actually sits on the page right now — the
        /// number the teleport bug got wrong, and the one the evidence asserts.
        public float ThumbTop
        {
            get { return _thumb == null ? 0f : DrawnUI.TopLeftY(_thumb.rectTransform); }
        }

        /// `viewport` is the clipped rect; `content` is what moves inside it. The
        /// caller builds the thumb sitting ON the track's top, so that is where the
        /// track's top is read from; the track's HEIGHT comes off the sibling the
        /// caller drew it with (both pages name it "track") and falls back to the
        /// viewport height when there is none.
        public static ShelfScroll Attach(RectTransform viewport, RectTransform content,
                                         float viewH, float contentH, Image thumb)
        {
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            var s = viewport.gameObject.AddComponent<ShelfScroll>();
            s._content = content;
            s._viewH = viewH;
            s._contentH = contentH;
            s._thumb = thumb;
            s._trackTop = thumb != null ? DrawnUI.TopLeftY(thumb.rectTransform) : 0f;
            s._trackH = TrackHeightOf(thumb, viewH);
            s.Apply();
            return s;
        }

        /// The bar Godot sizes the thumb against is the TRACK, not the viewport — on
        /// the draft page they differ (476 against 484), and the difference is the
        /// thumb overshooting the bottom of the line it rides. The track is the
        /// pencil line the caller drew the thumb ONTO, so it is found by name AND by
        /// standing in the same column: a page with two shelves has two tracks, and
        /// the first one in the hierarchy is not necessarily this one's.
        static float TrackHeightOf(Image thumb, float fallback)
        {
            if (thumb == null) return fallback;
            Transform parent = thumb.transform.parent;
            if (parent == null) return fallback;
            RectTransform tr = thumb.rectTransform;
            float mid = tr.anchoredPosition.x + tr.sizeDelta.x * 0.5f;
            float nearest = 12f;
            float found = fallback;
            for (int i = 0; i < parent.childCount; i++)
            {
                var rt = parent.GetChild(i) as RectTransform;
                if (rt == null || rt.name != "track" || rt.rect.height <= 1f) continue;
                float d = Mathf.Abs(rt.anchoredPosition.x + rt.sizeDelta.x * 0.5f - mid);
                if (d > nearest) continue;
                nearest = d;
                found = rt.rect.height;
            }
            return found;
        }

        /// THE SHELF GREW — the book's first entry landed, the bag was refilled. The
        /// redraw early-out is CLEARED here: the scroll offset has not moved, so
        /// without this the resized thumb is never drawn again, which is why the
        /// book's thumb never appeared at all after the relayout.
        public void SetContentHeight(float h)
        {
            _contentH = h;
            _lastDrawn = float.NaN;
            Apply();
        }

        /// Drive the shelf from somewhere other than the wheel.
        public void ScrollTo(float v)
        {
            _scroll = v;
            Apply();
        }

        public void OnPointerEnter(PointerEventData e) { _over = true; }
        public void OnPointerExit(PointerEventData e) { _over = false; }

        void Update()
        {
            if (_over)
            {
                float wheel = Input.mouseScrollDelta.y;
                if (Mathf.Abs(wheel) > 0.01f) _scroll -= wheel * 60f;
            }
            Apply();
        }

        void Apply()
        {
            float maxs = Mathf.Max(_contentH - _viewH, 0f);
            _scroll = Mathf.Clamp(_scroll, 0f, maxs);
            if (_scroll == _lastDrawn) return;
            _lastDrawn = _scroll;
            if (_content != null) DrawnUI.SetTopLeft(_content, 0f, -_scroll);
            if (_thumb == null) return;

            var rt = _thumb.rectTransform;
            float th = Mathf.Max(_trackH * Mathf.Clamp01(_viewH / Mathf.Max(_contentH, 1f)), MinThumb);
            float frac = maxs <= 0f ? 0f : Mathf.Clamp01(_scroll / maxs);
            float travel = Mathf.Max(_trackH - 2f * Inset - th, 0f);
            rt.sizeDelta = new Vector2(ThumbW, th);
            DrawnUI.SetTopLeft(rt, rt.anchoredPosition.x, _trackTop + Inset + travel * frac);
            _thumb.enabled = maxs > Dead;
        }
    }
}
