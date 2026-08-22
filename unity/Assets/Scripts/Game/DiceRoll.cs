using System;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;
using Runway.Audio;

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
    ///
    /// THE TABLE ARRIVES ON THE ROOM (dice_roll.gd's own header: "the page stays
    /// visible and darkens under the cup — no popping felt card"). Live-play #179,
    /// owner: "background looks REALLY bad" — the cup was rolling on a flat dark
    /// disc over black, because the veil ran all the way to opaque and a vignette
    /// was painted under the die. The page the week was written on stays readable
    /// underneath; the disc is gone.
    /// </summary>
    public sealed class DiceRoll : MonoBehaviour
    {
        const int Cols = 8;
        const int Frames = 40;
        const float Fps = 12f;
        const float Cell = 512f;
        const float HoldLast = 0.7f;   // the settled number is READ, not glimpsed

        /// How dark the page goes under the cup, and how long it takes to get there.
        /// It STOPS at the ceiling: the room or the open journal reads through it for
        /// the whole roll, which is the only thing that makes the ceremony feel like
        /// it happens on the desk instead of in a lightbox.
        public const float VeilCeiling = 0.55f;
        public const float VeilRise = 0.55f;

        /// The ink the page darkens under — Godot's Color(0.11, 0.095, 0.08).
        public static Color VeilAt(float t)
        {
            return new Color(0.11f, 0.095f, 0.08f,
                             Mathf.Clamp01(t / VeilRise) * VeilCeiling);
        }

        public event Action Finished;
        public bool Settled { get; private set; }

        Image _veil;
        SheetLoop _loop;
        float _t;
        bool _done;

        /// The veil the ceremony owns its beat with: nothing behind it may be
        /// pressed. Built here so an evidence probe raises the SHIPPING one.
        public static Image Veil(RectTransform parent)
        {
            return DrawnUI.FullFill(parent, "veil", VeilAt(0f), true);
        }

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
            // ONE VEIL, NO FELT. The disc under the die was a 1.16x-of-the-screen
            // bake that only ever read as a grey plate once the veil behind it had
            // gone opaque; with the page showing through there is nothing for it to
            // sit on and nothing for it to do.
            _veil = Veil(rt);

            int roll = Mathf.Clamp(n, 1, 20);
            string art = string.Format("dice/roll_{0:00}.png", roll);
            if (!SheetOnHand(art, roll))
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
            // THE TABLE ARRIVES ON A BREATH OF CURTAIN, THEN THE CUP RATTLES. dice_roll.gd
            // opens its whoosh at -14dB / 1.25x — the curtain cue's own -8 with a -6 trim
            // on top, thinner and higher than the one the menus sweep on — and starts the
            // rattle with the sheet. A missing sheet returned above, so neither sounds
            // over a ceremony that is being skipped.
            Sfx.Curtain(-6f, 1.25f);
            Sfx.DiceRattle();
            // the settled number is HELD, not glimpsed. A coroutine rather than
            // Invoke(): a string method name is the one call IL2CPP stripping can lose.
            _loop.Finished += () => StartCoroutine(HoldThenFinish());

            // a click skips to the settled number — a reader is never held
            var btn = gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = _veil;
            btn.onClick.AddListener(Complete);
        }

        /// THE SHEET MAY BE BAKED RATHER THAN STREAMED. Build.EnsureSheets stages
        /// the twenty cup films into Resources/Sheets, and SheetLoop reaches for
        /// that copy first, so "no file under Art/dice" is not "no sheet" — asking
        /// the streamed tree alone would skip a ceremony whose film is right there.
        /// The Resources probe only runs when the streamed file is genuinely absent,
        /// and the texture it loads is the very one SheetLoop is about to play.
        static bool SheetOnHand(string relative, int roll)
        {
            if (RunwayPaths.ArtExists(relative)) return true;
            return Resources.Load<Texture2D>(string.Format("Sheets/roll_{0:00}", roll)) != null;
        }

        void Update()
        {
            if (_done) return;
            _t += Time.unscaledDeltaTime;
            // the page darkens UNDER the cup over 0.55s and STOPS at the ceiling —
            // it never blanks, so the week you just wrote is still there behind the
            // die that is deciding it
            if (_veil != null) _veil.color = VeilAt(_t);
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
