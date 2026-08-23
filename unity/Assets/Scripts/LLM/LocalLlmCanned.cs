using System;
using Newtonsoft.Json.Linq;
using Runway.App;

namespace Runway.Llm
{
    /// <summary>
    /// THE ROUTER, PROVABLE TODAY, WITH NO MODEL ON DISK.
    ///
    /// The local pilot has two halves: the routing (does a clarify request leave the
    /// network path and come back the right shape?) and the generation (can a 4B model
    /// write the question?). The second half needs a 2.5 GB download and a package.
    /// The first half needs neither, and it is the half that can break silently — a
    /// seam that quietly still calls the network looks exactly like a seam that works.
    ///
    /// So: RUNWAY_LOCAL_LLM=1 RUNWAY_LOCAL_LLM_CANNED=1 puts a backend behind the
    /// router that answers clarify from a fixed deck. Every reply is schema-valid, and
    /// needs_clarification alternates true, false, true, … by call index, so a test can
    /// assert the exact reply it expects rather than "something came back".
    ///
    /// RUNWAY_LOCAL_LLM_CANNED=poison serves a reply that is deliberately OUT of schema
    /// (a 100-character question against a maxLength of 90, and a `kind` outside the
    /// enum) so the router's own schema guard can be proven to catch it rather than
    /// assumed to.
    ///
    /// It answers INSIDE Complete, on the calling thread. A real backend cannot and
    /// will not; this one does on purpose, because a synchronous answer is what lets an
    /// editor probe prove the network was never touched — the network path cannot
    /// produce a parsed object without first yielding, and outside play mode it cannot
    /// yield at all.
    /// </summary>
    public sealed class LocalLlmCanned : ILocalCompletion
    {
        public const string Flag = "RUNWAY_LOCAL_LLM_CANNED";

        /// Set and non-empty asks for it; "poison" also asks for the out-of-schema deck.
        public static bool Wanted
        {
            get
            {
                string v = Env.Get(Flag, "");
                return v == "1" || v == "poison";
            }
        }

        /// Serve replies that do NOT fit ClarifySchema, so the guard above can be seen
        /// working. Set from the environment, or by a probe directly.
        public bool Poison;

        int _calls;

        /// How many clarify requests this backend has answered. The alternation is a
        /// function of this and nothing else, so a test that resets it gets the same
        /// deck every time.
        public int Calls { get { return _calls; } }

        public LocalLlmCanned()
        {
            Poison = Env.Get(Flag, "") == "poison";
        }

        public bool Ready { get { return true; } }

        public bool Handles(string tier) { return tier == LocalLlmRouter.PilotTier; }

        public string Describe()
        {
            return "canned deck, no model" + (Poison ? " (POISONED — out of schema on purpose)" : "");
        }

        public void Cancel() { /* nothing is ever in flight */ }

        // ── the deck ────────────────────────────────────────────────────────────

        /// One question per `kind`, so the enum is exercised across a run of calls
        /// rather than only its first member. All are under the schema's 90 characters.
        static readonly string[] Kinds =
        {
            "amount", "target", "resource", "price", "other",
        };

        static readonly string[] Questions =
        {
            "How much of the runway are you putting behind this?",
            "Which of the three investors are you walking in to see?",
            "Who on the crew is doing this while you are out pitching?",
            "What are you charging for it once it ships?",
            "What does winning this week actually look like?",
        };

        public void Complete(string systemPrompt, string userPrompt, JObject schema,
                             string tier, int maxTokens, Action<JObject> cb)
        {
            if (cb == null) return;
            if (tier != LocalLlmRouter.PilotTier) { cb(null); return; }

            int n = _calls++;

            if (Poison)
            {
                // 100 characters against a maxLength of 90, and a kind nobody declared.
                cb(new JObject
                {
                    ["needs_clarification"] = true,
                    ["question"] = new string('x', 100),
                    ["kind"] = "not_a_kind",
                });
                return;
            }

            bool needs = (n % 2) == 0;      // true, false, true, … deterministically
            int pick = (n / 2) % Kinds.Length;

            cb(new JObject
            {
                ["needs_clarification"] = needs,
                ["question"] = needs ? Questions[pick] : "",
                ["kind"] = needs ? Kinds[pick] : "other",
            });
        }
    }
}
