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
    /// </summary>
    public sealed class ShelfScroll : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        RectTransform _content;
        Image _thumb;
        float _viewH;
        float _contentH;
        float _scroll;
        float _lastDrawn = -1f;
        bool _over;

        /// `viewport` is the clipped rect; `content` is what moves inside it.
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
            return s;
        }

        public void SetContentHeight(float h) { _contentH = h; }

        public void OnPointerEnter(PointerEventData e) { _over = true; }
        public void OnPointerExit(PointerEventData e) { _over = false; }

        void Update()
        {
            if (_over)
            {
                float wheel = Input.mouseScrollDelta.y;
                if (Mathf.Abs(wheel) > 0.01f) _scroll -= wheel * 60f;
            }
            float maxs = Mathf.Max(_contentH - _viewH, 0f);
            _scroll = Mathf.Clamp(_scroll, 0f, maxs);
            if (Mathf.Approximately(_scroll, _lastDrawn)) return;
            _lastDrawn = _scroll;
            if (_content != null) DrawnUI.SetTopLeft(_content, 0f, -_scroll);
            if (_thumb != null)
            {
                var rt = _thumb.rectTransform;
                float trackH = _viewH;
                float th = Mathf.Max(trackH * Mathf.Clamp01(_viewH / Mathf.Max(_contentH, 1f)), 30f);
                float ty = maxs <= 0f ? 0f : (trackH - th) * (_scroll / maxs);
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, th);
                DrawnUI.SetTopLeft(rt, rt.anchoredPosition.x, ty);
                _thumb.enabled = maxs > 8f;
            }
        }
    }
}
