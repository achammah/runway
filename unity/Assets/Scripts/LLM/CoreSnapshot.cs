using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Runway.Core;

namespace Runway.Llm
{
    /// <summary>
    /// THE ONE PLACE THE NARRATOR MEETS THE ENGINE.
    ///
    /// event_generator.gd reaches into GameState, SimEngine and WorldGen from inside
    /// every composer. This port keeps the composers pure — they take a RunSnapshot —
    /// and does all the reaching here, once, through Runway.Core's public API only.
    /// Nothing in this file writes to state.
    ///
    ///     generator.Adjudicate(CoreSnapshot.From(state), card, move, cb, dice);
    ///
    /// Every line below is the expression the original used, named after it.
    /// </summary>
    public static class CoreSnapshot
    {
        public static RunSnapshot From(GameState state)
        {
            var s = new RunSnapshot();
            if (state == null) return s;

            // ── the sandwich ──────────────────────────────────────────────────
            s.Digest = JObject.FromObject(state.ToDigest());
            s.Signals = JObject.FromObject(SimEngine.Signals(state));
            s.BibleDigest = WorldGen.BibleDigest(state);
            s.StorySoFar = state.StorySoFar ?? "";
            s.RunHistory = state.RunHistory != null && state.RunHistory.Count > 0
                ? JArray.FromObject(state.RunHistory)
                : new JArray();
            s.ArcDirectives = state.ActiveArcDirectives().ToArray();
            s.TraitSheet = JObject.FromObject(state.TraitSheet());
            s.Arcs = state.Arcs != null && state.Arcs.Count > 0
                ? JArray.FromObject(state.Arcs)
                : new JArray();
            s.PlayedEvents = state.PlayedEvents != null
                ? state.PlayedEvents.ToArray()
                : new string[0];

            // ── the deterministic directives ──────────────────────────────────
            s.RunwayWeeks = SimEngine.RunwayWeeks(state);
            s.Exhaustion = state.Exhaustion;
            s.TechDebt = (float)state.TechDebt;
            s.Launched = state.HasFlag("launched");

            var clocks = new List<RunSnapshot.ClockRow>();
            if (state.Clocks != null)
            {
                foreach (Clock c in state.Clocks)
                    clocks.Add(new RunSnapshot.ClockRow
                    {
                        WeeksLeft = c.WeeksLeft,
                        Consequence = c.Consequence ?? "",
                    });
            }
            s.Clocks = clocks.ToArray();

            var offers = new List<RunSnapshot.OfferRow>();
            if (state.Offers != null)
            {
                foreach (Offer o in state.Offers)
                    offers.Add(new RunSnapshot.OfferRow
                    {
                        Name = o.Name ?? "",
                        Price = (float)o.Price,
                        Unit = o.Unit ?? "",
                        FairPrice = (float)o.FairPrice,
                        PriceSet = o.PriceSet,
                        ServeCost = (float)(o.UnitCost * SimEngine.LearningCurve(state)),
                    });
            }
            s.Offers = offers.ToArray();
            // the nine subsystems' lines, already in the spine's section order —
            // this is the one place Core and the narrator's lane meet
            s.LaneDirectives = SimEngine.LaneDirectives(state).ToArray();

            // ── the clarify pre-pass' small state ─────────────────────────────
            s.Cash = state.Cash;
            s.Week = state.Week;
            s.Era = state.Era ?? "garage";
            s.Traction = state.Traction;
            s.Crew = CrewNames(state);
            s.Items = state.Items != null ? state.Items.ToArray() : new string[0];
            s.Budgets = state.Budgets != null ? JObject.FromObject(state.Budgets) : new JObject();
            var siteNames = new List<string>();
            if (state.Sites != null)
                foreach (Site st in state.Sites) siteNames.Add(st.Name ?? "");
            s.SiteNames = siteNames.ToArray();

            // ── the sentinel's ground truth ───────────────────────────────────
            var investors = new List<string>();
            if (state.Investors != null)
                foreach (Investor inv in state.Investors) investors.Add(inv.Name ?? "");
            s.InvestorNames = investors.ToArray();

            var rivals = new List<string>();
            if (state.Rivals != null)
                foreach (Rival rv in state.Rivals) rivals.Add(rv.Name ?? "");
            s.RivalNames = rivals.ToArray();
            var leadNames = new List<string>();
            if (state.Leads != null)
                foreach (Lead ld in state.Leads) leadNames.Add(ld.Name ?? "");
            s.LeadNames = leadNames.ToArray();
            var logoNames = new List<string>();
            if (state.Logos != null)
                foreach (Logo lg in state.Logos) logoNames.Add(lg.Name ?? "");
            s.LogoNames = logoNames.ToArray();

            var statuses = new List<string>(SimEngine.STATUS.Keys);
            s.StatusCatalog = statuses.ToArray();

            // ── the pitch ─────────────────────────────────────────────────────
            s.CompanyName = state.CompanyName ?? "";
            s.CompanyIdea = state.CompanyIdea ?? "";
            s.BizWhat = state.BizWhat ?? "";
            s.BizWho = state.BizWho ?? "";
            return s;
        }

        /// event_generator.gd's _crew_names(): you first, then the cofounders who have
        /// a name, then every employee.
        public static string[] CrewNames(GameState state)
        {
            var outNames = new List<string>();
            if (state == null) return outNames.ToArray();
            if (!string.IsNullOrEmpty(state.FounderName))
                outNames.Add(state.FounderName + " (you)");
            if (state.Cofounders != null)
            {
                foreach (Cofounder cf in state.Cofounders)
                {
                    string n = (cf.Name ?? "").Trim();
                    if (n.Length > 0) outNames.Add(n);
                }
            }
            if (state.Employees != null)
            {
                foreach (Employee em in state.Employees) outNames.Add(em.Name ?? "");
            }
            return outNames.ToArray();
        }
    }
}
