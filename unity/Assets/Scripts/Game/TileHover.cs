using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Runway.Game
{
    /// <summary>
    /// A THING ON THE SHELF, PICKED UP AND LOOKED AT. Hovering an object grows it a
    /// little and opens its detail card — the affordance the bag page runs on, because
    /// nothing on that shelf is a tile with a label any more.
    ///
    /// Unity's Selectable transitions can only tint a graphic, and these are streamed
    /// RawImages with a contact shadow behind them, so the pop is done here.
    /// </summary>
    public sealed class TileHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        Action _onEnter;
        RectTransform _rt;
        float _t = 1f;
        bool _in;

        public void Bind(Action onEnter)
        {
            _onEnter = onEnter;
            _rt = GetComponent<RectTransform>();
        }

        public void OnPointerEnter(PointerEventData e)
        {
            _in = true;
            if (_onEnter != null) _onEnter();
        }

        public void OnPointerExit(PointerEventData e) { _in = false; }

        void Update()
        {
            float want = _in ? 1.08f : 1f;
            if (Mathf.Approximately(_t, want)) return;
            _t = Mathf.MoveTowards(_t, want, Time.unscaledDeltaTime / (_in ? 0.08f : 0.1f) * 0.08f);
            if (_rt != null) _rt.localScale = new Vector3(_t, _t, 1f);
        }
    }
}
