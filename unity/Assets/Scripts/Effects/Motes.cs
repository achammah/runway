using UnityEngine;
using Runway.App;

namespace Runway.Effects
{
    /// <summary>
    /// D5a — DUST IN THE BEAM. A spotlight with nothing in it is a flat wash; the same
    /// spotlight with forty specks turning over in it is a room with air in it. That is
    /// the whole job: the select stage's bulb and the garage's bulb get a beam full of
    /// slow dust that nobody is meant to notice.
    ///
    /// RESTRAINT IS THE SPEC. Forty is the ceiling and the honest count is nearer
    /// thirty, they move at walking-pace-divided-by-ten, they fade in and out at the
    /// ends of their lives so nothing ever pops, and the beam mask takes their alpha
    /// away as they wander out of the light. If a player looks at one of these, it is
    /// too strong.
    ///
    /// One mote in eight is drawn with the out-of-focus cell instead of the dot, which
    /// is the entire depth of field and costs nothing — see DrawnParticleView.
    ///
    /// KILL-SWITCH: RUNWAY_FX_PARTICLES=0 makes every entry point return null having
    /// built nothing.
    /// </summary>
    public sealed class Motes : MonoBehaviour
    {
        /// The hard ceiling from the checklist. The pool is capped here, not tuned to
        /// here — the rate is set so the beam averages nearer thirty alive.
        public const int Ceiling = 40;

        DrawnParticleView _view;

        public DrawnParticleView View { get { return _view; } }
        public DrawnParticleSim Sim { get { return _view != null ? _view.Sim : null; } }
        public int Live { get { return _view != null ? _view.Live : 0; } }

        /// Let the beam empty itself gracefully rather than blinking off.
        public void Fade()
        {
            if (_view != null && _view.Sim != null) _view.Sim.Fade();
        }

        // ══ entry points ═══════════════════════════════════════════════════════

        /// THE GENERAL FORM. `beam` is written in the host's own Godot coordinates —
        /// x right, y DOWN from the host's top-left — and describes the beam's
        /// BOUNDING box: it is `topWidth` across at `beam.y` and `beam.width` across
        /// at `beam.y + beam.height`, centred on the box.
        public static Motes Apply(RectTransform parent, Rect beam, float topWidth,
                                  Color tint, float alphaLow = 0.10f,
                                  float alphaHigh = 0.30f, int seed = 91)
        {
            if (!ParticleInk.On || parent == null) return null;
            if (beam.width < 8f || beam.height < 8f) return null;

            DrawnParticleSim sim;
            var view = ParticleInk.Mount(parent, "motes", Ceiling, seed,
                                         ParticleInk.UvDot, ParticleInk.UvBlur, 7u, out sim);
            if (view == null) return null;

            float cx = beam.x + beam.width * 0.5f;
            Vector2 top = ParticleInk.ToLocal(view.rectTransform, cx, beam.y);
            Vector2 bottom = ParticleInk.ToLocal(view.rectTransform, cx,
                                                 beam.y + beam.height);
            view.SetCone(top.x, top.y, topWidth * 0.5f, bottom.y, beam.width * 0.5f);

            sim.BoxCentre = new Vector2(top.x, (top.y + bottom.y) * 0.5f);
            sim.BoxSize = new Vector2(beam.width, beam.height);
            sim.LifeMin = 6f;
            sim.LifeMax = 12f;
            // the QUAD is the halo; the speck the eye reads is about a third of it
            sim.SizeMin = 8f;
            sim.SizeMax = 20f;
            // dust SETTLES, mostly, and changes its mind on the way down
            sim.DriftMin = new Vector2(-7f, -13f);
            sim.DriftMax = new Vector2(7f, 3f);
            sim.NoiseStrength = 9f;
            sim.NoiseFrequency = 0.005f;
            sim.NoiseScroll = 0.12f;
            sim.ColorA = tint;
            sim.ColorB = tint;
            sim.AlphaMin = alphaLow;
            sim.AlphaMax = alphaHigh;
            sim.FadeIn = 0.14f;
            sim.FadeOut = 0.20f;
            sim.Loop = true;
            sim.Rate = 3.4f;                 // ~30 alive against a 9s average life
            sim.Prewarm = 12f;               // the beam is already full on frame one
            sim.Play();

            var motes = view.gameObject.AddComponent<Motes>();
            motes._view = view;
            return motes;
        }

        /// THE SELECT STAGE'S BULB — the cone FounderDraftScreen.Spotlight draws, to
        /// the pixel: centred on the stage, 0.16 of it across at the top, 0.44 across
        /// at the floor, dying at 0.86 of the way down. Cream dust, because the stage
        /// behind it is night.
        public static Motes DraftSpotlight(RectTransform parent)
        {
            const float w = RunwayPaths.StageWidth;
            const float h = RunwayPaths.StageHeight;
            float wide = w * 0.44f;
            return Apply(parent,
                         new Rect((w - wide) * 0.5f, 0f, wide, h * 0.86f),
                         w * 0.16f, DrawnUI.Cream, 0.14f, 0.38f, 91);
        }

        /// THE GARAGE'S BULB — the room is cream, so its dust is INK at a whisper:
        /// specks seen against the light, not glowing in it. The beam hangs over the
        /// desk and the crew and stops at the floor line.
        public static Motes GarageBulb(RectTransform parent)
        {
            return Apply(parent, new Rect(400f, 0f, 600f, 780f), 190f,
                         DrawnUI.Ink, 0.09f, 0.26f, 37);
        }
    }
}
