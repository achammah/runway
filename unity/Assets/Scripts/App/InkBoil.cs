using UnityEngine;
using UnityEngine.UI;

namespace Runway.App
{
    /// <summary>
    /// One boiling line. It holds the three held drawings of its own ink and shows
    /// whichever one the shared clock is on — it has NO Update of its own, because a
    /// screen carries forty of these and forty Update calls to change a texture pointer
    /// is forty too many. Registration is the whole per-instance life: on when the
    /// object comes up, off when it goes down, and the clock stops when the last one
    /// leaves.
    ///
    /// THE PHASE. Every element cycling in lockstep reads as the screen blinking. Each
    /// instance therefore starts on its own frame of the three, so at any instant the
    /// page holds a mix — which is what a hand-inked page does, and what "alive but
    /// calm" means. The clock still advances everybody on one tick, so the cost is one
    /// pass over one list, eight times a second.
    ///
    /// THE SWAP. `CanvasRenderer.SetTexture` puts the drawing on screen without dirtying
    /// the graphic, so a boil costs no canvas rebuild, no mesh regeneration and no
    /// allocation. A graphic that rebuilds for its own reasons resets its texture to the
    /// bake; the next tick (≤125ms) puts the boil back, and the bake is the correct
    /// drawing in the meantime, so nothing is ever wrong on screen.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InkBoil : MonoBehaviour
    {
        Graphic _ink;
        Texture2D[] _frames;
        Sprite[] _sprites;
        int _phase;
        int _shown = -1;

        /// The drawing on screen right now — what the shot harness reads back.
        public Texture2D Shown
        {
            get
            {
                if (_frames == null) return null;
                return _frames[_shown < 0 ? 0 : _shown];
            }
        }

        /// Which of the three is up, before the phase offset is taken off.
        public int ShownIndex { get { return _shown < 0 ? 0 : _shown; } }

        public Texture2D FrameAt(int i)
        {
            if (_frames == null || i < 0 || i >= _frames.Length) return null;
            return _frames[i];
        }

        internal void Bind(Graphic ink, Texture2D[] frames, Sprite[] sprites)
        {
            _ink = ink;
            _frames = frames;
            _sprites = sprites;
            _phase = (GetInstanceID() & 0x7FFFFFFF) % DrawnBoil.Frames;
            _shown = -1;
            if (isActiveAndEnabled) InkBoilClock.Register(this);
        }

        void OnEnable()
        {
            _shown = -1;
            if (_frames != null) InkBoilClock.Register(this);
        }

        void OnDisable()
        {
            InkBoilClock.Unregister(this);
            Rest();
        }

        /// Put the bake back — a screen that is not boiling shows the canonical drawing.
        void Rest()
        {
            if (_frames == null || _ink == null) return;
            _shown = -1;
            Put(_frames[0], _sprites != null ? _sprites[0] : null);
        }

        /// Called by the clock, once per tick, for every live instance.
        internal void ShowFrame(int frame)
        {
            if (_frames == null || _ink == null) return;
            int i = frame + _phase;
            if (i >= DrawnBoil.Frames) i -= DrawnBoil.Frames;
            if (i == _shown) return;
            _shown = i;
            Put(_frames[i], _sprites != null ? _sprites[i] : null);
        }

        void Put(Texture2D tex, Sprite spr)
        {
            if (DrawnBoil.SwapMode == DrawnBoil.Swap.Sprite && spr != null)
            {
                Image img = _ink as Image;
                if (img != null) { img.overrideSprite = spr; return; }
            }
            if (tex == null) return;
            CanvasRenderer cr = _ink.canvasRenderer;
            if (cr != null) cr.SetTexture(tex);
        }
    }
}
