using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — REVENUE · "growth" = THE MARKET GARDEN (DECISIONS: owner pick E,
    /// with the two generated layers). Twin of game/src/ui/desks/desk_growth.gd.
    /// THE QUESTION THIS DESK ANSWERS: "where does next week's demand come from?"
    ///
    /// Four plots, one per channel: topics from state.Topics (LLM dressing,
    /// generated once per run), the garden set as the shipped fallback so a
    /// keyless run plays word for word. Illustration slots read the painter's
    /// cache (garden_&lt;key&gt;.png under the user dir); the drawn instruments are
    /// the instant placeholder and the permanent fallback — numbers, verdicts
    /// and steppers never wait on an image.
    ///
    /// Money law: separate −/+ per channel; the era cap clamps the SUM and the
    /// meter under the hero shows it; word of mouth is the honest unbuyable
    /// line. Verdicts are WORDS COMPUTED FROM THE FUNNEL — the audience flips
    /// fall out of the data, never a hardcode.
    ///
    /// DAG3 (13-binder-ux · growth): a verdict chip is a DOOR — press it and
    /// the channel's own curve opens drawn (the saturating character, the
    /// knee ticked, your spend dotted), street math in receipt lines under
    /// it; a stepper press the CAP refuses pulses the meter coral (the
    /// ceiling made felt); [balance the mix — suggest] lays the SAME total
    /// even-marginal across the plots as ADOPT rows (spend-book pattern —
    /// nothing ever applies itself); the empty garden is the S1 state.
    /// </summary>
    public static class DeskGrowth
    {
        public const string Question = "where does next week's demand come from?";

        const float PlotW = 553f;
        const float PlotH = 206f;
        const float PlotY = 224f;

        sealed class Topic
        {
            public string Name = "";
            public string Line = "";
        }

        /// THE FALLBACK TOPIC LIBRARY — the garden set.
        static readonly Dictionary<string, Topic> DefaultTopics = new Dictionary<string, Topic>
        {
            { "ads", new Topic { Name = "cut flowers",
                Line = "bloom now, wilt fast — paid reach runs only while fed" } },
            { "content", new Topic { Name = "the orchard",
                Line = "slow, then generous — compounds funded, rots starved" } },
            { "referrals", new Topic { Name = "the bees",
                Line = "multiply what's healthy — only a liked product has promoters" } },
            { "outbound", new Topic { Name = "the stall",
                Line = "staffed, certain, dear — quota knocking on doors" } },
        };

        // ── the dispatch ───────────────────────────────────────────────────

        public static string[] HeroSummary(GameState s)
        {
            string big, line;
            HeroText(s, out big, out line);
            return new[] { big, line };
        }

        public static void Draw(BinderScreen b)
        {
            GameState s = b.State;
            Dictionary<string, double> f0 = SimFunnel.Funnel(s);
            if (f0.Count == 0 && SimFunnel.SpendTotal(s) <= 0.0)
            {
                // S1 — the untouched garden is a TEACHING state: the four
                // characters said once, the first $250 one press away
                DeskKit.ZeroState(b, new DeskKit.ZeroStateCfg
                {
                    WillShow = "where next week's demand comes from",
                    WouldLine = "four plots, four characters — ads pour while fed, content "
                        + "compounds, referrals multiply a liked product, outbound knocks on doors",
                    ActionLabel = "put the first $250 into ads",
                    ActionCb = () => b.SetBudget("ads", 250),
                    WakesHint = "verdicts and CAC arrive with the first locked week — "
                        + "the era caps what the whole mix may spend",
                });
                return;
            }
            string mode = ModeOf(b);
            Garden(b, s, f0, mode == "suggest");
            if (mode.StartsWith("curve:", StringComparison.Ordinal))
                CurveCard(b, s, mode.Substring(6));
        }

        static string ModeOf(BinderScreen b)
        {
            object mv;
            return b.Desk.TryGetValue("mode", out mv) ? (mv as string ?? "") : "";
        }

        /// The whole garden sheet — hero, cap meter, four plots, wom, foot.
        /// In suggest mode the yield lines give way to the ADOPT rows.
        static void Garden(BinderScreen b, GameState s, Dictionary<string, double> f0,
                           bool suggesting)
        {
            string big, line;
            HeroText(s, out big, out line);
            b.L(big, DeskKit.XId, 6f, DeskKit.HeroBig, DrawnUI.Ink, 760f);
            b.L(line, DeskKit.XId, 74f, DeskKit.Row, DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 740f);
            // S5 — the hero against the last open: what the mix bought
            if (f0.Count > 0)
            {
                int bought = Gd.RoundToInt(SimFunnel.Num(f0, "signed_ads")
                    + SimFunnel.Num(f0, "signed_content")
                    + SimFunnel.Num(f0, "signed_referrals")
                    + SimFunnel.Num(f0, "signed_outbound"));
                string gp = b.SeenPrev("growth", "hero");
                int gpN;
                if (b.Seen("growth", "hero", bought.ToString(CultureInfo.InvariantCulture))
                    && int.TryParse(gp, out gpN))
                {
                    float hbw = DrawnUI.MeasureWidth(big, DeskKit.HeroBig);
                    DeskKit.DeltaArrow(b, DeskKit.XId + hbw + 10f, 26f, bought, gpN);
                }
            }
            int cap = SimEngine.EraSpendCap(s.Era);
            TextMeshProUGUI cl = b.L("the " + s.Era + " era allows $" + GameUi.Money(cap) + "/wk",
                790f, 12f, 24f, DrawnUI.Ink, 340f);
            cl.alignment = TextAlignmentOptions.TopRight;
            double tm = SimFunnel.TeamMult(s);
            int heads = SimFunnel.MkHeads(s);
            TextMeshProUGUI tl = b.L(heads > 0
                ? string.Format(CultureInfo.InvariantCulture, "{0} marketing head{1} sharpen{2} it all ×{3:0.00}",
                    heads, heads == 1 ? "" : "s", heads == 1 ? "s" : "", tm)
                : "a marketing head would sharpen it all ×1.12",
                730f, 48f, 17f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 400f);
            tl.alignment = TextAlignmentOptions.TopRight;
            DeskKit.PenRule(b, 128f);
            double total = SimFunnel.SpendTotal(s);
            DeskKit.Meter(b, DeskKit.XId, 152f, 560f, (float)(total / Gd.Maxf(cap, 1.0)),
                DrawnUI.Sage, "$" + GameUi.Money(Gd.ToInt(total)) + " of the $"
                + GameUi.Money(cap) + " the era allows");
            // the refused press made FELT: a coral pulse breathes once
            object pv;
            if (b.Desk.TryGetValue("cap_pulse", out pv) && pv is bool && (bool)pv)
            {
                b.Desk.Remove("cap_pulse");
                CapPulse(b, DeskKit.XId - 4f, 146f, 568f, 30f);
            }
            Dictionary<string, int> split = suggesting ? EvenSplit(s) : new Dictionary<string, int>();
            if (suggesting && SplitDiffers(s, b, split))
            {
                // the whole-mix adopt (spend-book pattern): one arm, all four
                int t = 0;
                for (int i = 0; i < SimFunnel.Mix.Length; i++)
                    t += split.ContainsKey(SimFunnel.Mix[i]) ? split[SimFunnel.Mix[i]] : 0;
                Dictionary<string, int> splitNow = split;
                DeskKit.Arm(b, "adopt_mix_all", "adopt the whole split — $" + GameUi.Money(t) + "/wk",
                    "set all four plots — sure?", 620f, 180f, () =>
                    {
                        for (int i = 0; i < SimFunnel.Mix.Length; i++)
                        {
                            string k = SimFunnel.Mix[i];
                            b.SetBudget(k, splitNow.ContainsKey(k) ? splitNow[k] : 0);
                        }
                    }, 330f, 17f);
            }
            for (int i = 0; i < SimFunnel.Mix.Length; i++)
            {
                float px = 10f + (i % 2) * (PlotW + 14f);
                float py = PlotY + (i / 2) * (PlotH + 14f);
                Plot(b, s, SimFunnel.Mix[i], px, py, split);
            }
            // WORD OF MOUTH — the honest unbuyable row
            string womtxt = f0.Count > 0
                ? "word of mouth: ≈" + Gd.RoundToInt(SimFunnel.Num(f0, "wom"))
                  + " joined free this week — not for sale, earned"
                : "word of mouth: not for sale — it arrives when joiners bring friends";
            b.L(womtxt, DeskKit.XId, PlotY + 2f * PlotH + 34f, DeskKit.Detail,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 1100f);
            // S3 — the one primary act: the even-marginal walk (ADOPT-only)
            if (suggesting)
            {
                TextMeshProUGUI hint = b.L("nothing applies itself — adopt per plot, Esc keeps your mix",
                    560f, DeskKit.DoLaneY + 10f, 17f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f), 570f);
                hint.alignment = TextAlignmentOptions.TopRight;
            }
            else if (SplitDiffers(s, b, EvenSplit(s)))
            {
                DeskKit.DoLane(b, new List<DeskKit.DoAction>
                {
                    new DeskKit.DoAction
                    {
                        Label = "balance the mix — suggest",
                        Tier = "",
                        Cb = () => b.Desk["mode"] = "suggest",
                    },
                });
            }
            Foot(b, s);
        }

        public static void Handle(BinderScreen b, string id)
        {
        }

        // ── the hero ───────────────────────────────────────────────────────

        static void HeroText(GameState s, out string big, out string line)
        {
            int total = Gd.ToInt(SimFunnel.SpendTotal(s));
            Dictionary<string, double> f = SimFunnel.Funnel(s);
            if (f.Count == 0)
            {
                // money wears its commas everywhere on the desk
                big = "$" + GameUi.Money(total) + "/wk into the garden";
                line = "the first locked week measures what a dollar buys in each channel";
                return;
            }
            double bought = SimFunnel.Num(f, "signed_ads") + SimFunnel.Num(f, "signed_content")
                + SimFunnel.Num(f, "signed_referrals") + SimFunnel.Num(f, "signed_outbound");
            int cac = Gd.ToInt(SimFunnel.Num(f, "blended_cac"));
            int wom = Gd.RoundToInt(SimFunnel.Num(f, "wom"));
            big = "$" + GameUi.Money(total) + "/wk buys ≈"
                + Gd.RoundToInt(bought) + " customers";
            line = (cac > 0 ? "CAC $" + GameUi.Money(cac)
                : "CAC not yet knowable") + ", and word of mouth adds ≈" + wom + " more for free";
        }

        // ── one plot ───────────────────────────────────────────────────────

        static void Plot(BinderScreen b, GameState s, string key, float x, float y,
                         Dictionary<string, int> split)
        {
            Topic topic = TopicOf(s, key);
            DeskKit.CardBox frame = DeskKit.CardFrame(b, x, y, PlotW, PlotH,
                key + " — " + topic.Name);
            float cx = frame.ContentX;
            float cy = frame.ContentY;
            // S2b — the plot is a named landing: jumps spotlight it whole
            b.MarkControl("plot_" + key, new Rect(x, y, PlotW, PlotH));
            // S4 — the verdict is a DOOR: press → the channel's curve, drawn
            string vWord;
            Color vCol;
            Verdict(s, key, out vWord, out vCol);
            string keyNow = key;
            Button vbtn = DeskKit.Word(b, vWord, x + PlotW - 250f, y + 10f,
                () => b.Desk["mode"] = "curve:" + keyNow, 20f, vCol, 232f);
            vbtn.GetComponent<RectTransform>().sizeDelta = new Vector2(232f, 40f);
            PlotArt(b, key, cx, cy);
            float tx = cx + 140f;
            float tw = PlotW - DeskKit.CardPad * 2f - 140f;
            b.L(topic.Line, tx, cy - 2f, 16f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f), tw);
            int cur = b.Budget(key);
            int shownCur = cur + (key == "ads" ? s.MarketingBudget + s.Budgets.Marketing : 0);
            b.L("$" + GameUi.Money(shownCur) + "/wk", tx, cy + 40f, 27f, DrawnUI.Coral, 180f);
            int down = StepTo(b, s, key, cur, -1);
            int up = StepTo(b, s, key, cur, 1);
            string cat = key;
            // the CAP's refusal stays a live press — it answers by pulsing
            // the meter (the ceiling made FELT); only the ladder top goes dead
            bool ladderTop = cur >= BinderScreen.LeverSteps[BinderScreen.LeverSteps.Length - 1];
            bool capped = up == cur && !ladderTop;
            DeskKit.AdjustPair(b, tx + 196f, cy + 46f,
                () => b.SetBudget(cat, down),
                capped ? (Action)(() => b.Desk["cap_pulse"] = true)
                       : () => b.SetBudget(cat, up),
                down == cur, up == cur && !capped);
            b.MarkControl("mix_" + key, new Rect(tx + 192f, cy + 42f, 100f, 44f));
            // the yield line — or, in suggest mode, the ADOPT row
            if (split.Count == 0)
            {
                b.L(YieldLine(s, key, up == cur && cur > 0), tx, cy + 78f, 16f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), tw);
            }
            else
            {
                int sug = split.ContainsKey(key) ? split[key] : cur;
                if (sug == cur)
                    b.L("this plot already sits even", tx, cy + 78f, 16f,
                        DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f), tw);
                else
                {
                    int sugNow = sug;
                    DeskKit.Arm(b, "adopt_mix_" + key,
                        "suggested $" + GameUi.Money(sug) + " — adopt",
                        "set $" + GameUi.Money(sug) + "/wk — sure?", tx, cy + 74f,
                        () => b.SetBudget(cat, sugNow), 300f, 17f);
                }
            }
        }

        /// The topic for one plot: the world's own words, or the garden set.
        /// state.Topics arrives as a Dictionary fresh and a JObject off disk —
        /// this reads both and falls back for neither shape.
        static Topic TopicOf(GameState s, string key)
        {
            object growth = null;
            if (s.Topics != null) s.Topics.TryGetValue("growth", out growth);
            var gd = growth as IDictionary<string, object>;
            if (gd != null)
            {
                object t;
                if (gd.TryGetValue(key, out t))
                {
                    var td = t as IDictionary<string, object>;
                    if (td != null)
                    {
                        object nm, ln;
                        td.TryGetValue("name", out nm);
                        td.TryGetValue("line", out ln);
                        if (nm != null && nm.ToString().Length > 0)
                            return new Topic { Name = nm.ToString(), Line = ln != null ? ln.ToString() : "" };
                    }
                }
            }
            var gj = growth as Newtonsoft.Json.Linq.JObject;
            if (gj != null)
            {
                var tj = gj[key] as Newtonsoft.Json.Linq.JObject;
                if (tj != null)
                {
                    string nm = (string)tj["name"] ?? "";
                    if (nm.Length > 0)
                        return new Topic { Name = nm, Line = (string)tj["line"] ?? "" };
                }
            }
            Topic dflt;
            return DefaultTopics.TryGetValue(key, out dflt) ? dflt
                : new Topic { Name = key, Line = "" };
        }

        /// THE STEP a press would land on: the ledger's ladder, era-capped, and
        /// a step UP that would push the whole mix past the era's ceiling is
        /// refused (the SUM is what the engine clamps).
        static int StepTo(BinderScreen b, GameState s, string key, int cur, int dir)
        {
            int cap = SimEngine.EraSpendCap(s.Era);
            int[] steps = BinderScreen.LeverSteps;
            int idx = 0;
            for (int i = 0; i < steps.Length; i++) if (steps[i] <= cur) idx = i;
            idx = Gd.Clampi(idx + dir, 0, steps.Length - 1);
            int want = Gd.Mini(steps[idx], cap);
            if (dir <= 0) return want;
            int others = 0;
            for (int i = 0; i < SimFunnel.Mix.Length; i++)
                if (SimFunnel.Mix[i] != key) others += b.Budget(SimFunnel.Mix[i]);
            if (others + want > cap) return cur;
            return want;
        }

        /// WHAT THIS MONEY IS DOING RIGHT NOW — the funnel lane's own words,
        /// with the ceiling's reason when the mix is pinned.
        static string YieldLine(GameState s, string key, bool pinned)
        {
            string live = SimFunnel.LeverEffect(s, key);
            Dictionary<string, double> f = SimFunnel.Funnel(s);
            double cac = SimFunnel.Num(f, "cac_" + key);
            if (cac > 0.0) live += " · CAC $" + Gd.RoundToInt(cac);
            if (pinned) live += " · the mix is at the era's ceiling";
            return live;
        }

        /// THE VERDICT WORDS — computed from the funnel's own parameters.
        static void Verdict(GameState s, string key, out string word, out Color col)
        {
            Dictionary<string, double> f = SimFunnel.Funnel(s);
            double spend = SimFunnel.SpendOf(s, key);
            Color dim = DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f);
            Color plain = DrawnUI.WithAlpha(DrawnUI.Ink, 0.8f);
            switch (key)
            {
                case "ads":
                    if (spend <= 0.0) { word = "unfunded"; col = dim; return; }
                    if (SimFunnel.ReachAds(s) < 3.0) { word = "a drop in the ocean"; col = DrawnUI.Coral; return; }
                    double sat = SimFunnel.AdsSat(s);
                    if (spend >= 1.2 * sat) { word = "past the knee"; col = DrawnUI.Coral; return; }
                    if (spend >= 0.6 * sat) { word = "near the knee"; col = DrawnUI.Yellow; return; }
                    if (Cheapest(f) == "ads") { word = "the engine"; col = DrawnUI.Sage; return; }
                    word = "pouring"; col = plain; return;
                case "content":
                    double eq = s.ContentEquity;
                    if (spend <= 0.0)
                    {
                        if (eq >= 0.05) { word = "rotting −7%/wk"; col = DrawnUI.Coral; return; }
                        word = "nothing written"; col = dim; return;
                    }
                    if (SimFunnel.ContentTarget(s) > eq + 0.02) { word = "compounding"; col = DrawnUI.Sage; return; }
                    if (eq >= 0.6) { word = "the well is deep"; col = DrawnUI.Sage; return; }
                    word = "filling"; col = plain; return;
                case "referrals":
                    if (SimFunnel.Happy(s) < SimFunnel.HappyFloor)
                    {
                        word = "gate closed (v0." + s.Product + ")";
                        col = spend > 0.0 ? DrawnUI.Coral : dim;
                        return;
                    }
                    if (spend <= 0.0) { word = "gate open, unfunded"; col = plain; return; }
                    word = string.Format(CultureInfo.InvariantCulture, "gate open ×{0:0.0}",
                        1.0 + SimFunnel.RefGain(s));
                    col = DrawnUI.Sage;
                    return;
                case "outbound":
                    double aud = SimFunnel.Of(s).ObAud;
                    if (aud < 0.5) { word = "nobody answers a cold call"; col = dim; return; }
                    if (spend <= 0.0) { word = "no lists worked"; col = dim; return; }
                    if (Cheapest(f) == "outbound") { word = "the engine"; col = DrawnUI.Sage; return; }
                    double cac = SimFunnel.Num(f, "cac_outbound");
                    double blended = SimFunnel.Num(f, "blended_cac");
                    if (cac > 0.0 && blended > 0.0 && cac > 1.5 * blended)
                    { word = "sure but dear"; col = DrawnUI.Yellow; return; }
                    word = "knocking"; col = plain; return;
            }
            word = "";
            col = DrawnUI.Ink;
        }

        /// The channel that bought customers cheapest last week, or "".
        static string Cheapest(Dictionary<string, double> f)
        {
            string best = "";
            double bestCac = 0.0;
            for (int i = 0; i < SimFunnel.Mix.Length; i++)
            {
                double c = SimFunnel.Num(f, "cac_" + SimFunnel.Mix[i]);
                if (c > 0.0 && (best.Length == 0 || c < bestCac))
                {
                    best = SimFunnel.Mix[i];
                    bestCac = c;
                }
            }
            return best;
        }

        // ── the even-marginal split (S3/S15, ADOPT-only) ───────────────────

        /// THE EVEN-MARGINAL SPLIT — the classic lesson on the engine's own
        /// curves: re-lay the SAME total so the next dollar buys about the
        /// same everywhere. Greedy over the ladder; gates zero a closed
        /// channel; the cap bounds the total. Empty = nothing to lay out.
        static Dictionary<string, int> EvenSplit(GameState s)
        {
            var alloc = new Dictionary<string, int>
            {
                { "ads", 0 }, { "content", 0 }, { "referrals", 0 }, { "outbound", 0 },
            };
            Dictionary<string, double> f = SimFunnel.Funnel(s);
            if (f.Count == 0) return new Dictionary<string, int>();
            int cap = SimEngine.EraSpendCap(s.Era);
            int budget = Math.Min(Gd.ToInt(SimFunnel.SpendTotal(s)), cap);
            if (budget < BinderScreen.LeverSteps[1]) return new Dictionary<string, int>();
            SimFunnel.Channel ch = SimFunnel.Of(s);
            double tm = SimFunnel.TeamMult(s);
            double ee = SimFunnel.EraEff(s);
            double adds = Gd.Maxf(SimFunnel.Num(f, "adds"), 0.5);
            double reachPerAdd = Gd.Maxf(SimFunnel.Num(f, "reach_total") / adds, 1.0);
            double happy = SimFunnel.Happy(s);
            double womBase = SimFunnel.Num(f, "wom") / Gd.Maxf(1.0 + SimFunnel.RefGain(s), 1.0);
            var ceils = new Dictionary<string, double>
            {
                { "ads", ch.AdsA * ee * tm },
                { "content", ch.ConA * ee * tm },
                { "referrals", happy >= SimFunnel.HappyFloor
                    ? ch.RefA * happy * tm * womBase * reachPerAdd : 0.0 },
                { "outbound", 0.0 },
            };
            var sats = new Dictionary<string, double>
            {
                { "ads", SimFunnel.AdsSat(s) },
                { "content", Gd.Maxf(ch.ConSat, 1.0) },
                { "referrals", Gd.Maxf(ch.RefSat, 1.0) },
                { "outbound", 1.0 },
            };
            double obMarginal = ch.ObAud >= 0.5 ? SimFunnel.ObReachPerK / 1000.0 * ch.ObAud : 0.0;
            int[] steps = BinderScreen.LeverSteps;
            int spent = 0;
            while (true)
            {
                string best = "";
                double bestM = 0.0;
                for (int i = 0; i < SimFunnel.Mix.Length; i++)
                {
                    string key = SimFunnel.Mix[i];
                    int cur = alloc[key];
                    int ni = LadderIdx(cur) + 1;
                    if (ni >= steps.Length) continue;
                    int nxt = Math.Min(steps[ni], cap);
                    if (nxt <= cur || spent - cur + nxt > budget) continue;
                    double gain = key == "outbound"
                        ? obMarginal * (nxt - cur)
                        : ceils[key] * (Math.Exp(-cur / sats[key]) - Math.Exp(-nxt / sats[key]));
                    double m = gain / (nxt - cur);
                    if (m > bestM + 0.000001)
                    {
                        bestM = m;
                        best = key;
                    }
                }
                if (best.Length == 0 || bestM <= 0.0) break;
                int bn = Math.Min(steps[LadderIdx(alloc[best]) + 1], cap);
                spent += bn - alloc[best];
                alloc[best] = bn;
            }
            return spent <= 0 ? new Dictionary<string, int>() : alloc;
        }

        /// The rung at or under a value (off-ladder values land below).
        static int LadderIdx(int cur)
        {
            int idx = 0;
            for (int i = 0; i < BinderScreen.LeverSteps.Length; i++)
                if (BinderScreen.LeverSteps[i] <= cur) idx = i;
            return idx;
        }

        /// Whether the suggestion would move anything — empty never does.
        static bool SplitDiffers(GameState s, BinderScreen b, Dictionary<string, int> split)
        {
            if (split.Count == 0) return false;
            for (int i = 0; i < SimFunnel.Mix.Length; i++)
            {
                string k = SimFunnel.Mix[i];
                if ((split.ContainsKey(k) ? split[k] : 0) != b.Budget(k)) return true;
            }
            return false;
        }

        // ── S4 · the curve, drawn (press a verdict) ────────────────────────

        /// THE VERDICT, OPENED: the channel's own curve — the saturating
        /// character (or outbound's straight line), the knee ticked, your
        /// spend dotted onto it — street math in receipt lines below. Any
        /// press or Esc closes the read first (the desk-mode chain).
        static void CurveCard(BinderScreen b, GameState s, string key)
        {
            if (Array.IndexOf(SimFunnel.Mix, key) < 0)
            {
                b.Desk["mode"] = "";
                return;
            }
            Button catcher = DeskKit.Word(b, "", 0f, 0f, () => b.Desk["mode"] = "",
                DeskKit.Detail, DrawnUI.Ink, 1140f);
            catcher.GetComponent<RectTransform>().sizeDelta = new Vector2(1140f, 880f);
            double spend = SimFunnel.SpendOf(s, key);
            List<DeskKit.TicketLine> lines = CurveLines(b, s, key, spend);
            float cardH = 56f + 206f + lines.Count * 30f + 18f;
            string title = key != "outbound" ? key + " — the curve"
                : "outbound — the straight line";
            DeskKit.CardBox frame = DeskKit.CardFrame(b, 250f, 150f, 640f, cardH, title);
            float cx = frame.ContentX;
            float cy = frame.ContentY;
            bool linear = key == "outbound";
            double sat = 1.0;
            switch (key)
            {
                case "ads": sat = SimFunnel.AdsSat(s); break;
                case "content": sat = Gd.Maxf(SimFunnel.Of(s).ConSat, 1.0); break;
                case "referrals": sat = Gd.Maxf(SimFunnel.Of(s).RefSat, 1.0); break;
            }
            double xmax = linear ? Gd.Maxf(spend * 2.0, 2000.0)
                : Gd.Maxf(2.4 * sat, spend * 1.15 + 250.0);
            bool dim = key == "referrals" && SimFunnel.Happy(s) < SimFunnel.HappyFloor;
            CurveArt(b, cx, cy, 640f - DeskKit.CardPad * 2f, 190f, linear, dim, sat, xmax, spend);
            float moneyX = frame.MoneyX;
            float ly = cy + 206f;
            for (int n = 0; n < lines.Count; n++)
            {
                DeskKit.FitLine(b, lines[n].Label, cx, ly, 19f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.85f), 340f);
                TextMeshProUGUI v = DeskKit.FitLine(b, lines[n].Value, cx + 350f, ly, 19f,
                    lines[n].Col ?? DrawnUI.Ink, moneyX - cx - 350f);
                v.alignment = TextAlignmentOptions.TopRight;
                ly += 30f;
            }
        }

        /// THE CURVE, in dotted fills (the Godot twin draws it freehand —
        /// same geometry, same anchors): axis, the sampled sweep, the knee
        /// ticked, your spend dotted down onto its bead.
        static void CurveArt(BinderScreen b, float x, float y, float w, float h,
                             bool linear, bool dim, double sat, double xmax, double spend)
        {
            float ax = y + h - 30f;
            float top = y + 10f;
            float span = ax - top;
            double F(double v) => linear ? v / Gd.Maxf(xmax, 1.0)
                : 1.0 - Math.Exp(-v / Gd.Maxf(sat, 1.0));
            double ymaxV = Gd.Maxf(F(xmax), 0.001);
            Color inkC = DrawnUI.WithAlpha(DrawnUI.Ink, dim ? 0.35f : 1f);
            DrawnUI.Fill(b.Content, "cv_ax", DrawnUI.Ink, x, ax, w, 2.2f).raycastTarget = false;
            DrawnUI.Fill(b.Content, "cv_ay", DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f),
                x, top - 4f, 1.6f, span + 4f).raycastTarget = false;
            for (int k = 0; k < 33; k++)
            {
                double fx = xmax * k / 32.0;
                float px0 = x + w * (float)(fx / xmax);
                float py0 = ax - span * (float)(F(fx) / ymaxV);
                DrawnUI.Fill(b.Content, "cv_pt", inkC, px0 - 2f, py0 - 2f, 4f, 4f)
                    .raycastTarget = false;
            }
            if (!linear && sat < xmax)
            {
                float kx = x + w * (float)(sat / xmax);
                for (float yy = top; yy < ax; yy += 10f)
                    DrawnUI.Fill(b.Content, "cv_knee", DrawnUI.WithAlpha(DrawnUI.Ink, 0.35f),
                        kx - 0.8f, yy, 1.6f, Mathf.Min(5f, ax - yy)).raycastTarget = false;
                b.L("the knee", kx - 26f, ax + 6f, 14f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f), 90f);
            }
            float px = Mathf.Clamp(x + w * (float)(spend / Gd.Maxf(xmax, 1.0)), x, x + w);
            float py = ax - span * (float)(F(spend) / ymaxV);
            for (float y2 = top; y2 < ax - 4f; y2 += 11f)
                DrawnUI.Fill(b.Content, "cv_dot", DrawnUI.Coral, px - 1.1f, y2, 2.2f,
                    Mathf.Min(6f, ax - 4f - y2)).raycastTarget = false;
            var bead = DrawnUI.Fill(b.Content, "cv_bead", DrawnUI.Coral, px - 6f, py - 6f, 12f, 12f);
            bead.raycastTarget = false;
            DrawnUI.AddInkEdge(bead.rectTransform, new Vector2(12f, 12f),
                new DrawnUI.PaperStyle
                {
                    ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                    StepsPerEdge = 5, Jitter = 0.7f, Thickness = 2f, Seed = 37,
                });
            b.L("you: $" + Gd.RoundToInt(spend), Mathf.Clamp(px - 40f, x, x + w - 110f),
                top - 2f, 14f, DrawnUI.Coral, 110f);
            if (dim)
                b.L("the gate is closed", x + w * 0.32f, ax - span * 0.5f, 16f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 200f);
        }

        /// The receipt lines under the drawing — the funnel's numbers only.
        static List<DeskKit.TicketLine> CurveLines(BinderScreen b, GameState s, string key,
                                                   double spend)
        {
            Dictionary<string, double> f = SimFunnel.Funnel(s);
            string vWord;
            Color vCol;
            Verdict(s, key, out vWord, out vCol);
            var lines = new List<DeskKit.TicketLine>
            {
                new DeskKit.TicketLine { Label = "the verdict", Value = vWord, Col = vCol },
                new DeskKit.TicketLine { Label = "spend now",
                    Value = "$" + GameUi.Money(Gd.ToInt(spend)) + "/wk" },
            };
            if (f.Count > 0)
            {
                lines.Add(new DeskKit.TicketLine { Label = "bought last week",
                    Value = string.Format(CultureInfo.InvariantCulture, "≈{0:0.0} customers",
                        SimFunnel.Num(f, "signed_" + key)) });
                double cac = SimFunnel.Num(f, "cac_" + key);
                lines.Add(new DeskKit.TicketLine { Label = "CAC last week",
                    Value = cac > 0.0 ? "$" + GameUi.Money(Gd.RoundToInt(cac))
                        : "not yet knowable" });
            }
            switch (key)
            {
                case "ads":
                    lines.Add(new DeskKit.TicketLine { Label = "the knee sits at",
                        Value = "$" + GameUi.Money(Gd.RoundToInt(SimFunnel.AdsSat(s))) + "/wk" });
                    break;
                case "content":
                    lines.Add(new DeskKit.TicketLine { Label = "this spend funds level",
                        Value = Gd.RoundToInt(SimFunnel.ContentTarget(s) * 100.0) + "%" });
                    lines.Add(new DeskKit.TicketLine { Label = "equity today",
                        Value = Gd.RoundToInt(s.ContentEquity * 100.0) + "%" });
                    break;
                case "referrals":
                    if (SimFunnel.Happy(s) < SimFunnel.HappyFloor)
                        lines.Add(new DeskKit.TicketLine { Label = "the gate",
                            Value = "closed (v0." + s.Product + ")", Col = DrawnUI.Coral });
                    else
                        lines.Add(new DeskKit.TicketLine { Label = "word of mouth ×",
                            Value = string.Format(CultureInfo.InvariantCulture, "{0:0.00}",
                                1.0 + SimFunnel.RefGain(s)) });
                    break;
                case "outbound":
                    lines.Add(new DeskKit.TicketLine { Label = "reach per $1k",
                        Value = "≈" + Gd.RoundToInt(SimFunnel.ObReachPerK * SimFunnel.Of(s).ObAud) });
                    lines.Add(new DeskKit.TicketLine { Label = "closing bought",
                        Value = string.Format(CultureInfo.InvariantCulture, "+{0:0.0}",
                            SimFunnel.ObClosers(s)) });
                    break;
            }
            return lines;
        }

        // ── S8/S15 · the desk's own voice ──────────────────────────────────

        /// The garden is live from the garage; the S1 state covers week one.
        public static bool IsDormant(GameState s)
        {
            return false;
        }

        /// The rail's four-character read: what the week's mix costs.
        public static string MicroStatus(GameState s)
        {
            int total = Gd.ToInt(SimFunnel.SpendTotal(s));
            if (total <= 0) return "";
            if (total < 1000) return "$" + total + "/wk";
            return string.Format(CultureInfo.InvariantCulture, "${0:0.0}k/wk", total / 1000.0);
        }

        /// S15 — the desk speaks up: the even-marginal walk, as a jump chip.
        /// (The reflection collector has no BinderScreen, so the compare
        /// reads the state's own budget fields the way SpendOf does.)
        public static List<Dictionary<string, object>> Suggestions(GameState s)
        {
            var outp = new List<Dictionary<string, object>>();
            Dictionary<string, int> split = EvenSplit(s);
            if (split.Count == 0) return outp;
            bool differs = false;
            for (int i = 0; i < SimFunnel.Mix.Length; i++)
            {
                string k = SimFunnel.Mix[i];
                int cur = Gd.ToInt(SimFunnel.SpendOf(s, k))
                    - (k == "ads" ? s.MarketingBudget + s.Budgets.Marketing : 0);
                if ((split.ContainsKey(k) ? split[k] : 0) != cur)
                {
                    differs = true;
                    break;
                }
            }
            if (!differs) return outp;
            outp.Add(new Dictionary<string, object>
            {
                { "label", "balance the mix — growth" },
                { "kind", "jump" },
                { "payload", new Dictionary<string, object> { { "control", "do_0" } } },
            });
            return outp;
        }

        /// The refusal made FELT: a coral wash breathes once over the cap
        /// meter and fades (~0.45s; the next refresh clears the object).
        static void CapPulse(BinderScreen b, float x, float y, float w, float h)
        {
            var rt = DrawnUI.Rect(b.Content, "cappulse", x, y, w, h);
            var wash = DrawnUI.Fill(rt, "cp_wash", DrawnUI.WithAlpha(DrawnUI.Coral, 0.45f),
                0f, 0f, w, h);
            wash.raycastTarget = false;
            DrawnUI.AddInkEdge(rt, new Vector2(w, h), new DrawnUI.PaperStyle
            {
                ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                StepsPerEdge = 8, Jitter = 0.9f, Thickness = 3f, Seed = 41,
            });
            CanvasGroup g = DrawnUI.Group(rt);
            g.alpha = 0.9f;
            b.StartCoroutine(DrawnUI.FadeTo(g, 0f, 0.45f));
        }

        // ── the foot ───────────────────────────────────────────────────────

        static void Foot(BinderScreen b, GameState s)
        {
            Dictionary<string, double> f = SimFunnel.Funnel(s);
            string computed = "";
            if (f.Count > 0)
            {
                double bought = SimFunnel.Num(f, "signed_ads") + SimFunnel.Num(f, "signed_content")
                    + SimFunnel.Num(f, "signed_referrals") + SimFunnel.Num(f, "signed_outbound");
                int cac = Gd.ToInt(SimFunnel.Num(f, "blended_cac"));
                computed = "last week: bought ≈" + Gd.RoundToInt(bought) + " + free ≈"
                    + Gd.RoundToInt(SimFunnel.Num(f, "wom") + SimFunnel.Num(f, "organic"))
                    + " = " + Gd.RoundToInt(SimFunnel.Num(f, "adds")) + " joined · blended CAC "
                    + (cac > 0 ? "$" + GameUi.Money(cac) : "not yet knowable");
            }
            DeskKit.Footer(b, computed,
                "four levers, four characters — ads pour, content compounds, referrals multiply, outbound knocks",
                "", 806f, 840f);
        }

        // ── the illustration slot ──────────────────────────────────────────

        /// The painter's cache when it exists; the drawn instrument always —
        /// a half-written cache never leaves a hole.
        static void PlotArt(BinderScreen b, string key, float x, float y)
        {
            DrawnInstrument(b, key, x, y, 116f, 92f);
            try
            {
                string p = RunwayPaths.User("gen_illustrations/" + b.State.SimSeed + "_p"
                    + b.State.Pivots + "/plot_" + key + ".png");
                if (System.IO.File.Exists(p))
                {
                    byte[] bytes = System.IO.File.ReadAllBytes(p);
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(bytes))
                    {
                        var rt = DrawnUI.Rect(b.Content, "gardenimg", x, y, 116f, 92f);
                        var img = rt.gameObject.AddComponent<RawImage>();
                        img.texture = tex;
                        img.raycastTarget = false;
                    }
                }
            }
            catch (Exception) { /* the drawn instrument already stands */ }
        }

        /// THE DRAWN INSTRUMENTS — the garden's four characters, palette only.
        /// (This engine paints with fills and ink edges; the Godot twin draws
        /// the same shapes freehand — same slots, same palette, same story.)
        static void DrawnInstrument(BinderScreen b, string key, float x, float y,
                                    float w, float h)
        {
            var box = DrawnUI.Rect(b.Content, "plotart", x, y, w, h);
            switch (key)
            {
                case "ads":
                    for (int i = 0; i < 3; i++)
                    {
                        float sx = w * (0.22f + 0.28f * i);
                        float top = h * (0.30f + 0.08f * (i % 2));
                        DrawnUI.Fill(box, "stem", DrawnUI.Ink, sx - 1.3f, top, 2.6f,
                            h * 0.92f - top).raycastTarget = false;
                        var bloom = DrawnUI.Fill(box, "bloom",
                            DrawnUI.WithAlpha(DrawnUI.Coral, i == 2 ? 0.5f : 0.85f),
                            sx - 8f, top - 15f, 16f, 16f);
                        bloom.raycastTarget = false;
                        DrawnUI.AddInkEdge(bloom.rectTransform, new Vector2(16f, 16f),
                            new DrawnUI.PaperStyle
                            {
                                ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                                StepsPerEdge = 5, Jitter = 0.8f, Thickness = 2.2f, Seed = 73 + i,
                            });
                    }
                    break;
                case "content":
                    DrawnUI.Fill(box, "trunk", DrawnUI.Ink, w * 0.5f - 1.5f, h * 0.38f, 3f,
                        h * 0.54f).raycastTarget = false;
                    float[] cxs = { 0.5f, 0.31f, 0.71f };
                    float[] cys = { 0.26f, 0.36f, 0.34f };
                    for (int i = 0; i < 3; i++)
                    {
                        var can = DrawnUI.Fill(box, "canopy", DrawnUI.WithAlpha(DrawnUI.Sage, 0.85f),
                            w * cxs[i] - 13f, h * cys[i] - 13f, 26f, 26f);
                        can.raycastTarget = false;
                        DrawnUI.AddInkEdge(can.rectTransform, new Vector2(26f, 26f),
                            new DrawnUI.PaperStyle
                            {
                                ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                                StepsPerEdge = 6, Jitter = 0.9f, Thickness = 2.2f, Seed = 79 + i,
                            });
                    }
                    break;
                case "referrals":
                    float[] bxs = { 0.32f, 0.64f, 0.72f };
                    float[] bys = { 0.42f, 0.26f, 0.62f };
                    for (int i = 0; i < 3; i++)
                    {
                        var bee = DrawnUI.Fill(box, "bee", DrawnUI.WithAlpha(DrawnUI.Yellow, 0.9f),
                            w * bxs[i] - 9f, h * bys[i] - 6f, 18f, 12f);
                        bee.raycastTarget = false;
                        DrawnUI.AddInkEdge(bee.rectTransform, new Vector2(18f, 12f),
                            new DrawnUI.PaperStyle
                            {
                                ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                                StepsPerEdge = 5, Jitter = 0.7f, Thickness = 2f, Seed = 83 + i,
                            });
                        DrawnUI.Fill(box, "stripe", DrawnUI.Ink, w * bxs[i] - 3f,
                            h * bys[i] - 6f, 1.6f, 12f).raycastTarget = false;
                    }
                    for (int d = 0; d < 5; d++)
                        DrawnUI.Fill(box, "trail", DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f),
                            w * 0.16f + d * 9f, h * 0.78f - d * 4f, 5f, 1.8f).raycastTarget = false;
                    break;
                case "outbound":
                    var body = DrawnUI.Fill(box, "stallbody", DrawnUI.Hex("DDBE8C"),
                        w * 0.2f, h * 0.36f, w * 0.6f, h * 0.5f);
                    body.raycastTarget = false;
                    DrawnUI.AddInkEdge(body.rectTransform, new Vector2(w * 0.6f, h * 0.5f),
                        new DrawnUI.PaperStyle
                        {
                            ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                            StepsPerEdge = 8, Jitter = 1f, Thickness = 2.6f, Seed = 89,
                        });
                    var roof = DrawnUI.Fill(box, "roof", DrawnUI.Hex("CBA96F"),
                        w * 0.16f, h * 0.2f, w * 0.68f, h * 0.16f);
                    roof.raycastTarget = false;
                    DrawnUI.AddInkEdge(roof.rectTransform, new Vector2(w * 0.68f, h * 0.16f),
                        new DrawnUI.PaperStyle
                        {
                            ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                            StepsPerEdge = 6, Jitter = 0.9f, Thickness = 2.4f, Seed = 97,
                        });
                    var door = DrawnUI.Rect(box, "door", w * 0.44f, h * 0.58f, w * 0.14f, h * 0.28f);
                    DrawnUI.AddInkEdge(door, new Vector2(w * 0.14f, h * 0.28f),
                        new DrawnUI.PaperStyle
                        {
                            ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                            StepsPerEdge = 5, Jitter = 0.8f, Thickness = 2.4f, Seed = 101,
                        });
                    break;
            }
        }
    }
}
