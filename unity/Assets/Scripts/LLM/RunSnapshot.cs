using Newtonsoft.Json.Linq;

namespace Runway.Llm
{
    /// <summary>
    /// WHAT THE NARRATOR IS TOLD, and nothing else.
    ///
    /// event_generator.gd reaches straight into GameState, SimEngine and WorldGen. This
    /// port does not: the composers take a SNAPSHOT, so the LLM layer never references
    /// a Runway.Core type and the two lanes can be written, changed and tested apart.
    /// The run lane fills one of these per call — every field below is exactly one
    /// expression in the original, named after it:
    ///
    ///     Digest          state.to_digest()
    ///     Signals         SimEngine.signals(state)
    ///     BibleDigest     WorldGen.bible_digest(state)   ("" = no bible yet)
    ///     StorySoFar      state.story_so_far
    ///     RunHistory      state.run_history              (the composer slices the last 3)
    ///     ArcDirectives   state.active_arc_directives()
    ///     TraitSheet      state.trait_sheet()
    ///     RunwayWeeks     SimEngine.runway_weeks(state)
    ///     StatusCatalog   SimEngine.STATUS.keys()
    ///
    /// Anything left null or empty simply drops its block out of the message, which is
    /// what the `if not ... .is_empty()` guards in the original do.
    /// </summary>
    public sealed class RunSnapshot
    {
        // ── the sandwich ──────────────────────────────────────────────────────
        public JObject Digest;
        public JObject Signals;
        public string BibleDigest = "";
        public string StorySoFar = "";
        public JArray RunHistory;
        public string[] ArcDirectives;
        public JToken TraitSheet;
        public JArray Arcs;
        public string[] PlayedEvents;

        // ── the deterministic directives ──────────────────────────────────────
        public int RunwayWeeks = 99;
        public int Exhaustion;
        public float TechDebt;
        public bool Launched;
        public ClockRow[] Clocks;
        public OfferRow[] Offers;
        /// The nine subsystems' directive lines, already in the spine's section
        /// order — SimEngine.LaneDirectives(state) on the run side. Carried as
        /// plain strings so this lane still references no Runway.Core type.
        public string[] LaneDirectives;

        // ── the clarify pre-pass' small state ─────────────────────────────────
        public int Cash;
        public int Week;
        public string Era = "garage";
        public int Traction;
        public string[] Crew;
        public string[] Items;
        public JToken Budgets;

        // ── the sentinel's ground truth ───────────────────────────────────────
        public string[] InvestorNames;
        public string[] RivalNames;
        public string[] StatusCatalog;

        // ── the pitch ─────────────────────────────────────────────────────────
        public string CompanyName = "";
        public string CompanyIdea = "";
        public string BizWhat = "";
        public string BizWho = "";

        public bool HasBible
        {
            get
            {
                return (InvestorNames != null && InvestorNames.Length > 0)
                       || (RivalNames != null && RivalNames.Length > 0);
            }
        }

        public struct ClockRow
        {
            public int WeeksLeft;
            public string Consequence;
        }

        public struct OfferRow
        {
            public string Name;
            public float Price;
            public string Unit;
            public float FairPrice;
            public bool PriceSet;
        }
    }
}
