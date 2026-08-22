using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Runway.App
{
    /// <summary>
    /// The hover the .gd buttons carry: font_hover_color, plus the small scale pop the
    /// title's paper cards and slot dossiers do on mouse_entered. Unity's built-in
    /// ColorTint transition can only MULTIPLY a colour, and ink to coral is not a
    /// multiply, so the swap is done here.
    /// </summary>
    public sealed class HoverTint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        TMP_Text _label;
        Color _normal;
        Color _hover;
        RectTransform _scaleTarget;
        float _hoverScale = 1f;
        float _t;
        bool _in;

        public void Setup(TMP_Text label, Color normal, Color hover,
                          RectTransform scaleTarget, float hoverScale)
        {
            _label = label;
            _normal = normal;
            _hover = hover;
            _scaleTarget = scaleTarget;
            _hoverScale = hoverScale;
            if (_label != null) _label.color = normal;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _in = true;
            if (_label != null) _label.color = _hover;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _in = false;
            if (_label != null) _label.color = _normal;
        }

        void Update()
        {
            if (_scaleTarget == null || Mathf.Approximately(_hoverScale, 1f)) return;
            float want = _in ? _hoverScale : 1f;
            // 0.08s in, 0.1s out — the two tween lengths in title_screen.gd
            float speed = _in ? 1f / 0.08f : 1f / 0.1f;
            _t = Mathf.MoveTowards(_scaleTarget.localScale.x, want, speed
                                   * Mathf.Abs(_hoverScale - 1f) * Time.unscaledDeltaTime);
            _scaleTarget.localScale = new Vector3(_t, _t, 1f);
        }
    }
}
