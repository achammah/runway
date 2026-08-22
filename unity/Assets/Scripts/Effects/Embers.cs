using UnityEngine;
using Runway.App;

namespace Runway.Effects
{
    /// <summary>
    /// D5c — THE RUNWAY IS ON FIRE, SO IT SHEDS. The title painting burns along its
    /// left edge in every one of the 48 frames, and in the Godot build ten embers lift
    /// off that fire and die out above it. This is that, in the same numbers.
    ///
    /// TWIN OF title_screen.gd's ember block, kept honest:
    ///   ten of them, lifting out of x 60..600 / y 430..690 — inside the fire layer,
    ///   which sits at (-20, 390) and is 750x456;
    ///   4 to 9 pixels across, coral E86A5C running to yellow F4B942;
    ///   rising 90 to 190 pixels over 1.6 to 3.2 seconds, wandering 40 either way;
    ///   fading up over the first fifth of that life and out over the last third,
    ///   peaking between half and nine-tenths opacity — never solid.
    /// What this build adds is that they are DRAWN rather than square: a hot core with
    /// a glow that gives up slowly, and one in four wearing the out-of-focus cell so
    /// the fire has depth as well as height.
    ///
    /// KILL-SWITCH: RUNWAY_FX_PARTICLES=0 makes every entry point return null having
    /// built nothing.
    /// </summary>
    public sealed class Embers : MonoBehaviour
    {
        /// The checklist's window: 8 to 12 alive. The rate averages ten against a
        /// 2.4s life and the pool is capped at twelve.
        public const int Ceiling = 12;

        DrawnParticleView _view;

        public DrawnParticleView View { get { return _view; } }
        public DrawnParticleSim Sim { get { return _view != null ? _view.Sim : null; } }
        public int Live { get { return _view != null ? _view.Live : 0; } }

        /// Stop feeding the fire and let what is in the air burn out — the title's
        /// menu can quieten the painting without a blink.
        public void Fade()
        {
            if (_view != null && _view.Sim != null) _view.Sim.Fade();
        }

        // ══ entry points ═══════════════════════════════════════════════════════

        /// THE GENERAL FORM. `band` is the stretch of fire the embers lift out of,
        /// in the host's own Godot coordinates — x right, y DOWN from its top-left.
        public static Embers Apply(RectTransform parent, Rect band, int seed = 5)
        {
            if (!ParticleInk.On || parent == null) return null;
            if (band.width < 4f || band.height < 4f) return null;

            DrawnParticleSim sim;
            var view = ParticleInk.Mount(parent, "embers", Ceiling, seed,
                                         ParticleInk.UvEmber, ParticleInk.UvBlur, 3u,
                                         out sim);
            if (view == null) return null;

            sim.BoxCentre = ParticleInk.ToLocal(view.rectTransform,
                                                band.x + band.width * 0.5f,
                                                band.y + band.height * 0.5f);
            sim.BoxSize = new Vector2(band.width, band.height);
            sim.LifeMin = 1.6f;
            sim.LifeMax = 3.2f;
            // the quad is the glow; the hot middle the eye reads as the ember is
            // about a third of it, which lands on the original's 4-9 pixel squares
            sim.SizeMin = 10f;
            sim.SizeMax = 22f;
            // 90 to 190 px of lift over the life, and 40 either side of the wander
            sim.DriftMin = new Vector2(-16f, 40f);
            sim.DriftMax = new Vector2(16f, 80f);
            sim.NoiseStrength = 14f;
            sim.NoiseFrequency = 0.010f;
            sim.NoiseScroll = 0.5f;
            sim.RotMin = 20f;
            sim.RotMax = 60f;                // the 0.7 radians the original sits them at
            sim.SpinMin = -34f;
            sim.SpinMax = 34f;
            sim.ColorA = DrawnUI.Coral;
            sim.ColorB = DrawnUI.Yellow;
            sim.AlphaMin = 0.5f;
            sim.AlphaMax = 0.9f;
            sim.FadeIn = 0.20f;
            sim.FadeOut = 0.30f;
            sim.Loop = true;
            sim.Rate = 4.2f;                 // ~10 alive against a 2.4s average life
            sim.Prewarm = 4f;                // the fire is already lit at frame one
            sim.Play();

            var embers = view.gameObject.AddComponent<Embers>();
            embers._view = view;
            return embers;
        }

        /// THE TITLE'S FIRE — the band title_screen.gd spawns its ten embers in,
        /// unchanged, so the two builds burn in the same place.
        public static Embers TitleFire(RectTransform parent)
        {
            return Apply(parent, new Rect(60f, 430f, 540f, 260f), 5);
        }
    }
}
