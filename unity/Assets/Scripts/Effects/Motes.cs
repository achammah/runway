using UnityEngine;
using Runway.App;

namespace Runway.Effects
{
    /// <summary>
    /// D5a — DUST IN THE BEAM. A spotlight with nothing in it is a flat wash; the same
    /// spotlight with a few dozen specks turning over in it is a room with air in it.
    /// That is the whole job: the select stage's bulb and the garage's bulb get a beam
    /// full of slow dust that nobody is meant to notice.
    ///
    /// RESTRAINT IS THE SPEC. They move at walking-pace-divided-by-ten, they fade in
    /// and out at the ends of their lives so nothing ever pops, and the beam mask takes
    /// their alpha away as they wander out of the light. If a player looks at one of
    /// these, it is too strong.
    ///
    /// THE TWO BULBS DO NOT SHARE A COUNT. The garage runs the general form — a
    /// 40-deep pool fed at 3.4/s, standing near thirty. The select stage runs the
    /// ORIGINAL'S numbers, and the original draws exactly fourteen.
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

        /// THE PAIR, MOUNTED AND AIMED, with nothing configured and nothing playing.
        ///
        /// `beam` is written in the host's own Godot coordinates — x right, y DOWN
        /// from the host's top-left — and describes the beam's BOUNDING box: it is
        /// `topWidth` across at `beam.y` and `beam.width` across at `beam.y +
        /// beam.height`, centred on the box. That box is the CONE MASK.
        ///
        /// `emit` is where the dust is scattered, which is not always the beam: the
        /// select stage's original scatters its motes in a box of its own that sits
        /// inside the light rather than filling it.
        static DrawnParticleView Aim(RectTransform parent, Rect beam, float topWidth,
                                     Rect emit, int capacity, int seed,
                                     out DrawnParticleSim sim)
        {
            sim = null;
            var view = ParticleInk.Mount(parent, "motes", capacity, seed,
                                         ParticleInk.UvDot, ParticleInk.UvBlur, 7u, out sim);
            if (view == null) return null;

            float cx = beam.x + beam.width * 0.5f;
            Vector2 top = ParticleInk.ToLocal(view.rectTransform, cx, beam.y);
            Vector2 bottom = ParticleInk.ToLocal(view.rectTransform, cx,
                                                 beam.y + beam.height);
            view.SetCone(top.x, top.y, topWidth * 0.5f, bottom.y, beam.width * 0.5f);

            float ex = emit.x + emit.width * 0.5f;
            Vector2 eTop = ParticleInk.ToLocal(view.rectTransform, ex, emit.y);
            Vector2 eBottom = ParticleInk.ToLocal(view.rectTransform, ex,
                                                  emit.y + emit.height);
            sim.BoxCentre = new Vector2(eTop.x, (eTop.y + eBottom.y) * 0.5f);
            sim.BoxSize = new Vector2(emit.width, emit.height);
            return view;
        }

        static Motes Finish(DrawnParticleView view, DrawnParticleSim sim)
        {
            sim.Play();
            var motes = view.gameObject.AddComponent<Motes>();
            motes._view = view;
            return motes;
        }

        /// THE GENERAL FORM — dust scattered through the whole of a beam and left to
        /// settle. The garage's bulb is this; anything without numbers of its own is
        /// this.
        public static Motes Apply(RectTransform parent, Rect beam, float topWidth,
                                  Color tint, float alphaLow = 0.10f,
                                  float alphaHigh = 0.30f, int seed = 91)
        {
            if (!ParticleInk.On || parent == null) return null;
            if (beam.width < 8f || beam.height < 8f) return null;

            DrawnParticleSim sim;
            var view = Aim(parent, beam, topWidth, beam, Ceiling, seed, out sim);
            if (view == null) return null;

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
            return Finish(view, sim);
        }

        /// THE SELECT STAGE'S BULB — the cone FounderDraftScreen.Spotlight draws, to
        /// the pixel: centred on the stage, 0.16 of it across at the top, 0.44 across
        /// at the floor, dying at 0.86 of the way down.
        ///
        /// THE DUST IS THE ORIGINAL'S DUST, not the general form's. founder_draft_
        /// screen.gd:716-740 builds FOURTEEN motes on looping tweens and every number
        /// below is one of its numbers:
        ///
        ///   for i in 14                 fourteen, always — a tween per mote, so the
        ///                               count never breathes. The general form fed a
        ///                               40-deep pool at 3.4/s and stood at ~40.
        ///   randf_range(2.0, 4.5)       a cream square that small. The quad is three
        ///                               times the speck, so 6..13.5 here.
        ///   x 600..990, y 300..840      the box they are scattered in. NOT the beam:
        ///                               it is a slab inside the light, and scattering
        ///                               through the whole beam put dust in the dark
        ///                               top corners the cone then had to take away.
        ///   position.y -= 80..150       they RISE. Ours settled DOWN at ~5px/s, which
        ///     over dur, dur 5..9        is the one thing a lit dust mote never does.
        ///                               80..150 over 5..9s is 9..18px/s of lift.
        ///   color:a → 0.18..0.4         the peak each fades up to.
        ///   0.3·dur up, 0.25·dur down   with the tween's own hold that is a 1.25·dur
        ///                               cycle — 6.25..11.25s — of which 0.24 is the
        ///                               fade in and 0.20 the fade out.
        ///
        /// A tween has no wander, so the noise goes to nothing as well. Godot's
        /// `mote.rotation = 0.6` is not transcribed: it turns a SQUARE ColorRect, and
        /// the cell here is a round speck that looks the same at every angle.
        public static Motes DraftSpotlight(RectTransform parent)
        {
            if (!ParticleInk.On || parent == null) return null;
            const float w = RunwayPaths.StageWidth;
            const float h = RunwayPaths.StageHeight;
            float wide = w * 0.44f;

            DrawnParticleSim sim;
            var view = Aim(parent,
                           new Rect((w - wide) * 0.5f, 0f, wide, h * 0.86f), w * 0.16f,
                           new Rect(600f, 300f, 390f, 540f), DraftMotes, 91, out sim);
            if (view == null) return null;

            sim.LifeMin = 6.25f;
            sim.LifeMax = 11.25f;
            sim.SizeMin = 6f;
            sim.SizeMax = 13.5f;
            sim.DriftMin = new Vector2(0f, 9f);      // straight up, no lateral drift
            sim.DriftMax = new Vector2(0f, 18f);
            sim.NoiseStrength = 0f;
            sim.ColorA = DrawnUI.Cream;
            sim.ColorB = DrawnUI.Cream;
            sim.AlphaMin = 0.18f;
            sim.AlphaMax = 0.40f;
            sim.FadeIn = 0.24f;
            sim.FadeOut = 0.20f;
            sim.Loop = true;
            // the pool is the count: a spawn into a full pool is dropped, so this only
            // has to outrun the death rate (14 / 8.75s average life = 1.6/s) for the
            // beam to hold its fourteen from the first frame to the last
            sim.Rate = 3f;
            sim.Prewarm = 12f;
            return Finish(view, sim);
        }

        /// FOURTEEN, and the pool is capped there rather than tuned there — see
        /// DraftSpotlight. The cone mask takes a couple away at the top of the box
        /// where the light is still narrow, which is the beam doing its job.
        public const int DraftMotes = 14;

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
