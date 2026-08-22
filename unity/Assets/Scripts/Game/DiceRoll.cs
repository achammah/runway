using System;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;

namespace Runway.Game
{
    /// <summary>
    /// THE TABLE ROLL, pre-rendered — dice_roll.gd, ported.
    ///
    /// Each number 1-20 ships as a spritesheet baked from a video: the coral cup
    /// rattles, lifts, and a decorated d20 tumbles out and settles on the rolled
    /// number. The engine's seeded d20 picks WHICH sheet plays; the number IS the
    /// roll, and low numbers already mean bad luck downstream.
    ///
    /// Sheets: Art/dice/roll_NN.png — an 8x5 grid of 512px frames at 12fps, ~3.3s,
    /// with alpha, so the cup and the die sit straight on the felt: no card, no
    /// frame, just the drawing and the light. A missing sheet degrades to a silent
    /// skip — the beat still tells the player what they rolled.
    ///
    /// FULL HEIGHT (owner: "fill screen in height so we avoid video cropping"): the
    /// clip is square, so height is the constraint and the loop takes all of it.
    /// </summary>
    public sealed class DiceRoll : MonoBehaviour
    {
        const int Cols = 8;
        const int Frames = 40;
        const float Fps = 12f;
        const float Cell = 512f;
        const float HoldLast = 0.7f;   // the settled number is READ, not glimpsed

        public event Action Finished;
        public bool Settled { get; private set; }

        Image _shade;
        Image _felt;
        SheetLoop _loop;
        float _t;
        bool _done;

        public static DiceRoll Create(RectTransform parent, int n)
        {
            var rt = DrawnUI.FullRect(parent, "dice");
            var dr = rt.gameObject.AddComponent<DiceRoll>();
            dr.BuildParts(n);
            return dr;
        }

        void BuildParts(int n)
        {
            var rt = GetComponent<RectTransform>();
            // the ceremony owns its beat: nothing behind it may be pressed
            _shade = DrawnUI.FullFill(rt, "shade", new Color(0.11f, 0.095f, 0.08f, 0f), true);
            float side = Mathf.Min(RunwayPaths.StageWidth, RunwayPaths.StageHeight) * 1.16f;
            var feltRt = DrawnUI.Rect(rt, "felt",
                (RunwayPaths.StageWidth - side) * 0.5f, (RunwayPaths.StageHeight - side) * 0.5f,
                side, side);
            _felt = feltRt.gameObject.AddComponent<Image>();
            _felt.sprite = DrawnUI.RingSprite(64f, 1f, 0f, 5, 2, true);
            _felt.color = new Color(0.145f, 0.125f, 0.10f, 0f);
            _felt.raycastTarget = false;

            int roll = Mathf.Clamp(n, 1, 20);
            string art = string.Format("dice/roll_{0:00}.png", roll);
            if (!RunwayPaths.ArtExists(art))
            {
                // A MISSING SHEET IS A SILENT SKIP — but not a SYNCHRONOUS one: the
                // caller subscribes to Finished on the line after Create(), so firing
                // it here would strand the cup on screen with nobody listening.
                Debug.LogWarning("RUNWAY! no dice sheet for " + roll + " — ceremony skipped");
                StartCoroutine(SkipNextFrame());
                return;
            }
            float square = RunwayPaths.StageHeight;
            _loop = SheetLoop.AttachAt(rt, "cup",
                (RunwayPaths.StageWidth - square) * 0.5f, 0f, square, square);
            _loop.PlaySheet(art, Cols, Frames, Fps, true, Cell, Cell);
            // the settled number is HELD, not glimpsed. A coroutine rather than
            // Invoke(): a string method name is the one call IL2CPP stripping can lose.
            _loop.Finished += () => StartCoroutine(HoldThenFinish());

            // a click skips to the settled number — a reader is never held
            var btn = gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = _shade;
            btn.onClick.AddListener(Complete);
        }

        void Update()
        {
            if (_done) return;
            _t += Time.unscaledDeltaTime;
            // the room fades to black UNDER the cup over 0.55s; by the time the die is
            // tumbling the table is fully its own screen
            float a = Mathf.Clamp01(_t / 0.55f);
            if (_shade != null) _shade.color = new Color(0.11f, 0.095f, 0.08f, a);
            if (_felt != null) _felt.color = new Color(0.145f, 0.125f, 0.10f, a);
        }

        System.Collections.IEnumerator SkipNextFrame()
        {
            yield return null;
            Complete();
        }

        System.Collections.IEnumerator HoldThenFinish()
        {
            Runway.Effects.Impulse.DieSettled();   // D4: the table takes the weight
            yield return new WaitForSecondsRealtime(HoldLast);
            Complete();
        }

        void Complete()
        {
            if (_done) return;
            _done = true;
            Settled = true;
            var f = Finished;
            if (f != null) f();
        }

        void OnDestroy()
        {
            if (_loop != null) _loop.Release();
        }
    }
}
