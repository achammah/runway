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
            string big, line;
            HeroText(s, out big, out line);
            b.L(big, DeskKit.XId, 6f, DeskKit.HeroBig, DrawnUI.Ink, 760f);
            b.L(line, DeskKit.XId, 74f, DeskKit.Row, DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 740f);
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
            for (int i = 0; i < SimFunnel.Mix.Length; i++)
            {
                float px = 10f + (i % 2) * (PlotW + 14f);
                float py = PlotY + (i / 2) * (PlotH + 14f);
                Plot(b, s, SimFunnel.Mix[i], px, py);
            }
            // WORD OF MOUTH — the honest unbuyable row
            Dictionary<string, double> f = SimFunnel.Funnel(s);
            string womtxt = f.Count > 0
                ? "word of mouth: ≈" + Gd.RoundToInt(SimFunnel.Num(f, "wom"))
                  + " joined free this week — not for sale, earned"
                : "word of mouth: not for sale — it arrives when joiners bring friends";
            b.L(womtxt, DeskKit.XId, PlotY + 2f * PlotH + 34f, DeskKit.Detail,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 1100f);
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
                big = "$" + total.ToString(CultureInfo.InvariantCulture) + "/wk into the garden";
                line = "the first locked week measures what a dollar buys in each channel";
                return;
            }
            double bought = SimFunnel.Num(f, "signed_ads") + SimFunnel.Num(f, "signed_content")
                + SimFunnel.Num(f, "signed_referrals") + SimFunnel.Num(f, "signed_outbound");
            int cac = Gd.ToInt(SimFunnel.Num(f, "blended_cac"));
            int wom = Gd.RoundToInt(SimFunnel.Num(f, "wom"));
            big = "$" + total.ToString(CultureInfo.InvariantCulture) + "/wk buys ≈"
                + Gd.RoundToInt(bought) + " customers";
            line = (cac > 0 ? "CAC $" + cac.ToString(CultureInfo.InvariantCulture)
                : "CAC not yet knowable") + ", and word of mouth adds ≈" + wom + " more for free";
        }

        // ── one plot ───────────────────────────────────────────────────────

        static void Plot(BinderScreen b, GameState s, string key, float x, float y)
        {
            Topic topic = TopicOf(s, key);
            DeskKit.CardBox frame = DeskKit.CardFrame(b, x, y, PlotW, PlotH,
                key + " — " + topic.Name);
            float cx = frame.ContentX;
            float cy = frame.ContentY;
            string vWord;
            Color vCol;
            Verdict(s, key, out vWord, out vCol);
            TextMeshProUGUI vl = b.L(vWord, x + PlotW - 250f, y + 14f, 20f, vCol, 232f);
            vl.alignment = TextAlignmentOptions.TopRight;
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
            DeskKit.AdjustPair(b, tx + 196f, cy + 46f,
                () => b.SetBudget(cat, down),
                () => b.SetBudget(cat, up),
                down == cur, up == cur);
            b.L(YieldLine(s, key, up == cur && cur > 0), tx, cy + 78f, 16f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), tw);
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
