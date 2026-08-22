using System;
using System.Collections;
using UnityEngine;

namespace Runway.App
{
    /// <summary>
    /// EVERY SCREEN IS A CONTROL THAT BUILDS ITSELF. The Godot game has one scene and
    /// constructs all UI in code; this port keeps that, so a screen here is a
    /// MonoBehaviour on a full-rect child of the stage that paints itself in OnBuild()
    /// and emits Done when its door is used.
    ///
    /// The contract, in full:
    ///   OnBuild()          build the whole screen — called once, synchronously, the
    ///                      frame the screen is created. No blank frame, ever.
    ///   Finish(result)     the door: raises Done exactly once.
    ///   Payload            whatever the transition handed in ({} for most screens).
    ///   Close()            fade out and destroy. Boot does this on a swap.
    /// </summary>
    public abstract class AppScreen : MonoBehaviour
    {
        /// This screen's own rect: full-stage, top-left anchored, Godot coordinates.
        public RectTransform Rect { get; private set; }

        public CanvasGroup Group { get; private set; }

        /// Whatever the flow handed this screen. Screens that need nothing ignore it.
        public object Payload { get; set; }

        /// The door. `result` is the screen's own payload out — a draft result, a slot
        /// number, or null for a plain "next".
        public event Action<object> Done;

        bool _built;
        bool _finished;

        protected Boot App { get { return Boot.Instance; } }

        /// Called by Boot immediately after AddComponent. Never call it yourself.
        public void Build(RectTransform rect)
        {
            if (_built) return;
            _built = true;
            Rect = rect;
            Group = DrawnUI.Group(rect);
            OnBuild();
        }

        protected abstract void OnBuild();

        /// Raise Done once. A click that lands on both a button and the page behind it
        /// must open the door once — the exact bug how_to_screen.gd guards with _gone.
        protected void Finish(object result = null)
        {
            if (_finished) return;
            _finished = true;
            var d = Done;
            if (d != null) d(result);
        }

        public bool Finished { get { return _finished; } }

        /// Fade this screen out, then destroy it.
        public virtual void Close(float secs = 0.18f)
        {
            if (this == null || gameObject == null) return;
            if (secs <= 0f || Group == null)
            {
                Destroy(gameObject);
                return;
            }
            var boot = Boot.Instance;
            if (boot == null) { Destroy(gameObject); return; }
            boot.StartCoroutine(CloseRoutine(secs));
        }

        IEnumerator CloseRoutine(float secs)
        {
            Group.blocksRaycasts = false;
            yield return DrawnUI.FadeTo(Group, 0f, secs);
            if (this != null && gameObject != null) Destroy(gameObject);
        }

        /// The 0.18s fade every swap in main.gd rides in on.
        public IEnumerator FadeIn(float secs = 0.18f)
        {
            if (Group == null) yield break;
            Group.alpha = 0f;
            yield return DrawnUI.FadeTo(Group, 1f, secs);
        }

        /// Convenience: run a coroutine even if this screen is torn down mid-flight.
        protected Coroutine Run(IEnumerator routine)
        {
            return StartCoroutine(routine);
        }
    }
}
