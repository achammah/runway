using UnityEngine;
using Runway.App;

namespace Runway.Effects
{
    /// <summary>
    /// D5b — THE LOCK-IN BURST. The most consequential click in the game already has a
    /// ceremony: the pen strikes a line under the words and the latch clicks. This is
    /// the half-second after — six to ten scraps of the page itself thrown off the
    /// strike, tumbling, pulled straight back down, gone inside a second.
    ///
    /// IT IS PAPER, NOT CONFETTI. Each scrap is a hand-cut cream quad with a wobbled
    /// ink edge and a line of writing on it, rasterised by the same hand that draws
    /// every card in the game, and it tumbles on its own axis at a couple of turns a
    /// second while gravity takes it. Nothing sparkles, nothing lingers: 0.8s of life
    /// and the page is clean again, because the week is what the player is looking at,
    /// not the effect.
    ///
    /// KILL-SWITCH: RUNWAY_FX_PARTICLES=0 makes every entry point return null having
    /// built nothing.
    /// </summary>
    public sealed class Scraps : MonoBehaviour
    {
        /// The burst window from the checklist: 6 to 10 scraps, 0.8s of life.
        public const int MinCount = 6;
        public const int MaxCount = 10;
        public const float Life = 0.8f;

        DrawnParticleView _view;

        public DrawnParticleView View { get { return _view; } }
        public DrawnParticleSim Sim { get { return _view != null ? _view.Sim : null; } }
        public int Live { get { return _view != null ? _view.Live : 0; } }

        // ══ entry points ═══════════════════════════════════════════════════════

        /// THE ONE-LINER. Burst from the centre of `at`, on the surface `at` already
        /// lives on — so a burst off a button on a leaning page leans with the page.
        public static Scraps Burst(RectTransform at)
        {
            if (at == null) return null;
            return Burst(at.parent as RectTransform, at);
        }

        /// The same burst, drawn on a surface of the caller's choosing — for a strike
        /// that should throw its paper OVER the book rather than onto the page.
        public static Scraps Burst(RectTransform host, RectTransform at)
        {
            if (!ParticleInk.On || host == null || at == null) return null;
            Scraps s = Build(host);
            if (s == null) return null;
            // exact through the transforms, so pivots, leans and nested rects all land
            Vector3 world = at.TransformPoint(at.rect.center);
            Vector3 local = s._view.rectTransform.InverseTransformPoint(world);
            Fire(s, new Vector2(local.x, local.y));
            return s;
        }

        /// Fully explicit: `x`/`y` are Godot coordinates in `host` — x right, y DOWN
        /// from its top-left, like every other coordinate in this port.
        public static Scraps BurstAt(RectTransform host, float x, float y)
        {
            if (!ParticleInk.On || host == null) return null;
            Scraps s = Build(host);
            if (s == null) return null;
            Fire(s, ParticleInk.ToLocal(s._view.rectTransform, x, y));
            return s;
        }

        // ══ the burst ══════════════════════════════════════════════════════════

        /// EVERY LOCK IS ITS OWN BURST. A fixed seed would throw the same six scraps
        /// on the same six paths every week of every run, which is the sort of thing
        /// a player notices by week four without knowing why. The counter walks the
        /// seed forward per press and stays reproducible inside a session.
        static int _bursts;

        static Scraps Build(RectTransform host)
        {
            DrawnParticleSim sim;
            var view = ParticleInk.Mount(host, "scraps", MaxCount, 17 + _bursts++ * 7919,
                                         ParticleInk.UvScrap, ParticleInk.UvScrap, 0u,
                                         out sim);
            if (view == null) return null;

            // a fan that opens upward off the strike, leaning a little with the pen
            sim.Radial = true;
            sim.OriginRadius = 9f;
            sim.ArcFrom = -12f;
            sim.ArcTo = 188f;
            sim.SpeedMin = 300f;
            sim.SpeedMax = 560f;
            sim.Gravity = -1700f;            // px/s², the fall that makes it paper
            sim.LifeMin = Life - 0.08f;
            sim.LifeMax = Life + 0.08f;
            // the quad is the SHEET's frame; the paper inside it is about 4/5 of that
            sim.SizeMin = 30f;
            sim.SizeMax = 48f;
            sim.RotMin = 0f;
            sim.RotMax = 360f;
            sim.SpinMin = -640f;             // degrees/s: nearly two turns a second
            sim.SpinMax = 640f;
            sim.ColorA = Color.white;
            sim.ColorB = DrawnUI.Hex("FFF3DC");
            sim.AlphaMin = 1f;
            sim.AlphaMax = 1f;
            sim.FadeIn = 0.05f;
            sim.FadeOut = 0.32f;
            sim.Loop = false;
            sim.BurstMin = MinCount;
            sim.BurstMax = MaxCount;

            var scraps = view.gameObject.AddComponent<Scraps>();
            scraps._view = view;
            return scraps;
        }

        static void Fire(Scraps s, Vector2 localOrigin)
        {
            s._view.Sim.Origin = localOrigin;
            s._view.Sim.Play();
            // one burst, then the paper is gone and so is everything that drew it
            if (Application.isPlaying) Destroy(s.gameObject, Life + 0.6f);
        }
    }
}
