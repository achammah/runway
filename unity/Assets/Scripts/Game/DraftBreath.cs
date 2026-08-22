using TMPro;
using UnityEngine;
using Runway.App;

namespace Runway.Game
{
    /// <summary>
    /// THE PAGE BREATHES ON THE PAGE'S OWN CLOCK.
    ///
    /// The founder sways, floats on his own breath, his shadow answers the float, the
    /// title leans a hair and the LOCK IN card pulses. Every one of those is a fresh
    /// transform each frame, and a fresh transform is a repaint. Quantising the clock
    /// to the 12fps the art is drawn at keeps the identical motion and lets the frames
    /// in between be swallowed — the same trick the original plays with BREATH_FPS.
    /// </summary>
    public sealed class DraftBreath : MonoBehaviour
    {
        const float BreathFps = 12f;

        RectTransform _hero;
        RectTransform _shadow;
        TextMeshProUGUI _title;
        RectTransform _lock;
        float _heroBaseY;
        float _last = -1f;

        public void Bind(RectTransform hero, RectTransform shadow, TextMeshProUGUI title,
                         RectTransform lockBtn, float heroBaseY)
        {
            _hero = hero;
            _shadow = shadow;
            _title = title;
            _lock = lockBtn;
            _heroBaseY = heroBaseY;
        }

        void Update()
        {
            float t = Mathf.Floor(Time.unscaledTime * BreathFps) / BreathFps;
            if (Mathf.Approximately(t, _last)) return;
            _last = t;
            if (_hero != null)
            {
                // the holder pivots on the feet, so the sway is a rotation and the
                // breath is a straight bob of its anchored y
                GameUi.Tilt(_hero, Mathf.Sin(t * 1.1f) * 0.02f);
                _hero.anchoredPosition = new Vector2(_hero.anchoredPosition.x,
                                                     _heroBaseY - Mathf.Sin(t * 2.2f) * 4f);
            }
            if (_shadow != null)
            {
                float sx = 1f - Mathf.Sin(t * 2.2f) * 0.03f;
                _shadow.localScale = new Vector3(sx, 1f, 1f);
            }
            if (_title != null)
                GameUi.Tilt(_title.rectTransform, Mathf.Sin(t * 0.7f) * 0.004f);
            if (_lock != null)
            {
                float p = 1f + Mathf.Sin(t * 3f) * 0.02f;
                _lock.localScale = new Vector3(p, p, 1f);
            }
        }
    }
}
