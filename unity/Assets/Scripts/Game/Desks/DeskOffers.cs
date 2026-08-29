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
    /// DESK — REVENUE · "offers" = THE RATE CARD (DECISIONS: owner pick D).
    /// Twin of game/src/ui/desks/desk_offers.gd — same rows, same columns,
    /// same words.
    /// THE QUESTION THIS DESK ANSWERS: "what do we sell and what does each
    /// sale earn?"
    ///
    /// One columned card of what we sell: a row per offer, a column per truth
    /// — price big, street, serve, margin, the demand verdict as ONE colored
    /// word — with the two SEPARATE −/+ squares in a dedicated ADJUST column
    /// (the stepper law) and the expand mark opening the shipped five-state
    /// detail machine unchanged. detail / write / wait / review are delegated
    /// whole to DeskCatalog: the DEFINE-AN-OFFER door is the same
    /// write->wait->review cost-lines road, and the drop arm lives on the
    /// detail sheet behind its two-tap.
    ///
    /// AUDIENCE VARIANTS: Consumer rows carry units/wk under the name;
    /// Enterprise adds the named-account per-seat line under the table, read
    /// from the pipeline. Fair-price backstop and the "!" unpriced warning
    /// are preserved.
    ///
    /// DAG3 (13-binder-ux · offers): the verdict word is a DOOR — press it
    /// and the street's read opens as a paper card with THE FAIR BAND drawn
    /// (demand's own thresholds on the price axis, your price dotted); the
    /// DO lane carries [set price — …] [add an offer]; the pen circles
    /// verdicts that moved since the last open; the empty shelf is the S1
    /// teaching state.
    /// </summary>
    public static class DeskOffers
    {
        public const string Question = "what do we sell and what does each sale earn?";

        // The rate card's own column grammar (inside the card at x10 w1120).
        const float ColNameX = 28f;
        const float ColNameW = 300f;
        const float ColPriceX = 340f;
        const float ColPriceW = 130f;
        const float ColStreetX = 480f;
        const float ColStreetW = 105f;
        const float ColServeX = 595f;
        const float ColServeW = 100f;
        const float ColMarginX = 705f;
        const float ColMarginW = 115f;
        const float ColVerdictX = 836f;
        const float ColVerdictW = 150f;
        const float ColExpandX = 986f;
        const float ColAdjustX = 1028f;
        const float RowH = 52f;
        const int ListShow = 6;

        static readonly Color Pos = DrawnUI.Hex("5D7A50");

        // ── the dispatch ───────────────────────────────────────────────────

        /// The quartet card IS the page's hero verbatim (DECISIONS).
        public static string[] HeroSummary(GameState s)
        {
            string big, line;
            double arpu, cogs;
            Hero(s, out big, out line, out arpu, out cogs);
            return new[] { big, line };
        }

        public static void Draw(BinderScreen b)
        {
            string mode = Mode(b);
            if (mode.StartsWith("verdict:", StringComparison.Ordinal))
            {
                // S4 — the verdict opened: the rate card stays under the paper
                // card; Esc or any press closes the read first (desk-mode pop)
                RateCard(b);
                int row;
                VerdictCard(b, b.State,
                    int.TryParse(mode.Substring(8), out row) ? row : -1);
                return;
            }
            switch (mode)
            {
                case "detail":
                case "write":
                case "wait":
                case "review":
                    // the shipped five-state machine, whole — one writer, one road
                    DeskCatalog.Draw(b);
                    return;
                case "all":
                    AllOffers(b);
                    return;
                default:
                    // Esc walked out of a sub-state: a proposal no longer on
                    // screen is a proposal that no longer exists.
                    b.Desk.Remove("pending");
                    b.Desk.Remove("house");
                    b.Desk.Remove("refused");
                    b.Desk.Remove("short");
                    RateCard(b);
                    return;
            }
        }

        public static void Handle(BinderScreen b, string id)
        {
        }

        static string Mode(BinderScreen b)
        {
            object mv;
            return b.Desk.TryGetValue("mode", out mv) ? (mv as string ?? "") : "";
        }

        // ── the hero ───────────────────────────────────────────────────────

        /// What one customer's week earns, across the shelf — the tab's answer.
        static void Hero(GameState s, out string big, out string line,
                         out double arpu, out double cogs)
        {
            arpu = SimEngine.OffersArpu(s);
            cogs = 0.0;
            if (s.Offers.Count == 0)
            {
                big = "nothing on the shelf";
                line = "a company with nothing to sell earns nothing — write down what you sell";
                arpu = -1.0;
                return;
            }
            if (arpu < 0.0)
            {
                big = s.Offers.Count + " offers, unpriced";
                line = "the shelf bills at the street's rate until you set your own prices";
                return;
            }
            cogs = SimEngine.OffersCogsPerCustomer(s);
            string offersWord = s.Offers.Count == 1 ? "one offer" : s.Offers.Count + " offers";
            // at 0 customers the unit story is a PROMISE, not money — say so
            if (s.Traction <= 0)
            {
                big = "$0/wk — nobody pays yet";
                line = "one customer's week would earn $" + Gd.RoundToInt(arpu) + " in · $"
                       + Gd.RoundToInt(cogs) + " out -> $" + Gd.RoundToInt(arpu - cogs)
                       + ", across " + offersWord;
                return;
            }
            big = "$" + Gd.RoundToInt(arpu) + " in · $" + Gd.RoundToInt(cogs)
                  + " out -> $" + Gd.RoundToInt(arpu - cogs);
            line = "what one customer's week earns you, across " + offersWord + " on the shelf";
        }

        // ── the rate card ──────────────────────────────────────────────────

        /// A right-aligned cell — every dollar in a column ends on one line.
        static void R(BinderScreen b, string text, float x, float y, float sz,
                      Color col, float w)
        {
            TextMeshProUGUI l = b.L(text, x, y, sz, col, w);
            l.alignment = TextAlignmentOptions.TopRight;
        }

        static void RateCard(BinderScreen b)
        {
            GameState s = b.State;
            if (s.Offers.Count == 0)
            {
                // S1 — the empty shelf is a TEACHING page, never bare furniture
                DeskKit.ZeroState(b, new DeskKit.ZeroStateCfg
                {
                    WillShow = "what you sell, and what each sale earns",
                    WouldLine = "a row per offer — your price, the street's rate, the cost to "
                        + "serve, the margin, and one word saying how demand reads it",
                    ActionLabel = "+ define a new offer",
                    ActionCb = () => b.Desk["mode"] = "write",
                    WakesHint = "the shelf fills with the bible — an unpriced offer bills at "
                        + "the street's rate until you set your own",
                });
                return;
            }
            string big, line;
            double arpu, cogs;
            Hero(s, out big, out line, out arpu, out cogs);
            float y = DeskKit.HeroBand(b, big, line);
            // S5 — the hero's arrow: what one customer nets, vs the last open
            if (arpu >= 0.0)
            {
                int net = Gd.RoundToInt(arpu - cogs);
                string prev = b.SeenPrev("offers", "hero");
                int prevN;
                if (b.Seen("offers", "hero", net.ToString(CultureInfo.InvariantCulture))
                    && int.TryParse(prev, out prevN))
                {
                    float bw = DrawnUI.MeasureWidth(big, DeskKit.HeroBig);
                    DeskKit.DeltaArrow(b, DeskKit.XId + bw + 14f, 26f, net, prevN);
                }
            }
            // S2 — red speaks ON the page: the pricing asks in one measured
            // line. R5 — the strip renders in its own slot (96-118); content
            // holds its position whether or not the desk is red.
            DeskKit.AskStrip(b, "offers", DeskKit.XId, y, 1100f, "set the price below");
            // the per-customer two-bar: pays against serve
            if (arpu >= 0.0)
            {
                y = DeskKit.TwoBar(b, DeskKit.XId, y, 700f,
                    "pays", "$" + GameUi.Money(Gd.RoundToInt(arpu)), new List<float> { (float)arpu },
                    "serve", "$" + GameUi.Money(Gd.RoundToInt(cogs)),
                    new List<float> { Mathf.Max((float)cogs, 0f) });
                y += 4f;
            }
            double lc = SimEngine.LearningCurve(s);
            double fm = SimEngine.StreetFairMult(s);
            int war = Gd.RoundToInt((1.0 - fm) * 100.0);
            if (war > 0)
            {
                b.L("price war: the street's reference is " + war
                    + "% down — the same price reads dearer this week",
                    DeskKit.XId, y, DeskKit.Law, DrawnUI.Coral, 1100f);
                y += 28f;
            }
            List<int> shown;
            int folded;
            VisibleRows(s, lc, out shown, out folded);
            // THE OPEN OFFER'S CARD (owner: the loose stack read as clutter —
            // the shelf stays whole for context, the open ledger lives in ONE
            // framed card below, in the kit's own grammar).
            int openI = b.Desk.ContainsKey("open_row") ? Convert.ToInt32(b.Desk["open_row"]) : -1;
            bool openVis = openI >= 0 && openI < s.Offers.Count;
            List<int> rows = shown;
            int hidden = folded;
            if (openVis && shown.Count > 4)
            {
                rows = shown.GetRange(0, 4);
                if (!rows.Contains(openI)) rows[3] = openI;
                hidden = folded + shown.Count - 4;
            }
            float cardH = DeskKit.CardHead + 30f + rows.Count * RowH + 26f;
            DeskKit.CardBox frame = DeskKit.CardFrame(b, 10f, y, 1120f, cardH, "the rate card");
            float cy = frame.ContentY;
            cy = HeadRow(b, cy);
            for (int n = 0; n < rows.Count; n++)
                cy = Row(b, cy, rows[n], s, lc, fm);
            y = frame.Bottom + 10f;
            if (openVis)
                y = OpenCard(b, y, openI, s, lc, fm);
            if (hidden > 0)
                y = DeskKit.FoldRow(b, DeskKit.XId, y, hidden, "offers, healthy",
                    () => b.Desk["mode"] = "all");
            if (s.BizWho == "Enterprise")
                y = NamedAccountsLine(b, s, y);
            DefineDoor(b, y + 6f);
            // a tall page pushes its own lane and foot down and SCROLLS
            float baseY = Mathf.Max(DeskKit.DoLaneY, y + 52f);
            DoLaneDraw(b, s, baseY);
            FireSpot(b);
            Foot(b, s, baseY + 44f);
        }

        /// THE COLLAPSE LADDER: six rows face-up, the ones closest to money
        /// never hidden — unpriced or losing offers are promoted into view.
        static void VisibleRows(GameState s, double lc, out List<int> shown, out int folded)
        {
            int n = s.Offers.Count;
            shown = new List<int>();
            if (n <= ListShow)
            {
                for (int i = 0; i < n; i++) shown.Add(i);
                folded = 0;
                return;
            }
            var hot = new List<int>();
            var calm = new List<int>();
            for (int i = 0; i < n; i++)
            {
                Offer o = s.Offers[i];
                bool unpriced = o.Price <= 0.0 && !o.PriceSet;
                if (unpriced || SimCatalog.NeverPays(o, lc)) hot.Add(i);
                else calm.Add(i);
            }
            shown.AddRange(hot);
            for (int i = 0; i < calm.Count && shown.Count < ListShow; i++)
                shown.Add(calm[i]);
            shown.Sort();
            folded = n - shown.Count;
        }

        /// The small-caps header band — a column per truth.
        static float HeadRow(BinderScreen b, float y)
        {
            Color dim = DrawnUI.WithAlpha(DrawnUI.Ink, 0.42f);
            b.L("OFFER", ColNameX, y, 18f, dim, ColNameW);
            R(b, "PRICE", ColPriceX, y, 18f, dim, ColPriceW);
            R(b, "STREET", ColStreetX, y, 18f, dim, ColStreetW);
            R(b, "SERVE", ColServeX, y, 18f, dim, ColServeW);
            R(b, "MARGIN", ColMarginX, y, 18f, dim, ColMarginW);
            b.L("DEMAND", ColVerdictX, y, 18f, dim, ColVerdictW);
            R(b, "ADJUST", ColAdjustX - 8f, y, 18f, dim, 80f);
            return DeskKit.PenRule(b, y + 24f, ColNameX, 1120f - 36f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.25f), 7) + 2f;
        }

        /// One offer, one row, one column per truth.
        static float Row(BinderScreen b, float y, int i, GameState s, double lc, double fm)
        {
            Offer o = s.Offers[i];
            b.L((o.Name ?? "?").ToUpper(), ColNameX, y, 24f, DrawnUI.Ink, ColNameW);
            string sub = o.Unit ?? "";
            if (s.BizWho == "Consumer")
            {
                double units = s.Traction * o.Weight * SimEngine.OfferCadence(o.Unit ?? "");
                sub += " · ≈" + Gd.RoundToInt(units) + "/wk";
            }
            b.L(sub, ColNameX, y + 27f, DeskKit.ChipS, DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f), ColNameW);
            double price = o.Price;
            double fair = o.FairPrice * fm;
            double margin = SimCatalog.Contribution(o, lc, fm);
            string vWord;
            Color vCol;
            VerdictWord(o, price, fm, lc, out vWord, out vCol);
            if (price > 0.0)
                R(b, "$" + GameUi.Money(Gd.RoundToInt(price)), ColPriceX, y, 26f,
                  DrawnUI.Ink, ColPriceW);
            else if (o.PriceSet)
                R(b, "$0", ColPriceX, y, 26f, DrawnUI.Blue, ColPriceW);
            else
                // THE FAIR-PRICE BACKSTOP: unpriced bills at the going rate
                R(b, "! $" + GameUi.Money(Gd.RoundToInt(fair)), ColPriceX, y, 26f,
                  DrawnUI.Coral, ColPriceW);
            R(b, "$" + GameUi.Money(Gd.RoundToInt(fair)), ColStreetX, y + 3f, 21f,
              DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), ColStreetW);
            R(b, "$" + GameUi.Money(Gd.RoundToInt(SimCatalog.ServedUnitCost(o, lc))),
              ColServeX, y + 3f, 21f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), ColServeW);
            R(b, "$" + GameUi.Money(Gd.RoundToInt(margin)), ColMarginX, y + 1f, 23f,
              margin > 0.0 ? Pos : DrawnUI.Coral, ColMarginW);
            // S4 — the verdict word is a door: press → the street's read
            int idx = i;
            Button vbtn = DeskKit.Word(b, vWord, ColVerdictX, y - 4f,
                () => b.Desk["mode"] = "verdict:" + idx, 22f, vCol, ColVerdictW);
            vbtn.GetComponent<RectTransform>().sizeDelta = new Vector2(ColVerdictW, 44f);
            // S5/R3 — a moved verdict earns the gutter dot (coral when the
            // new word is a losing one); the arbiter keeps the worst row (R2)
            if (b.Seen("offers", "vd_" + (o.Name ?? i.ToString(CultureInfo.InvariantCulture)), vWord))
            {
                float vtw = Mathf.Min(DrawnUI.MeasureWidth(vWord, 22f), ColVerdictW);
                DeskKit.PenCircle(b, new Rect(ColVerdictX, y + 2f, vtw, 24f),
                    vCol == DrawnUI.Coral);
            }
            int nowOpen = b.Desk.ContainsKey("open_row") ? Convert.ToInt32(b.Desk["open_row"]) : -1;
            DeskKit.Expand(b, ColExpandX, y - 4f, () =>
                b.Desk["open_row"] = (b.Desk.ContainsKey("open_row")
                    ? Convert.ToInt32(b.Desk["open_row"]) : -1) == idx ? -1 : idx,
                nowOpen == idx);
            List<double> steps = DeskCatalog.PriceSteps(o);
            Offer oc = o;
            DeskKit.AdjustPair(b, ColAdjustX, y + 4f,
                () => DeskCatalog.PriceStep(oc, -1),
                () => DeskCatalog.PriceStep(oc, 1),
                DeskKit.AtMin(steps, price), DeskKit.AtMax(steps, price));
            // S2b — the row's switch has a name: focus lands on ADJUST
            var adjRect = new Rect(ColAdjustX - 4f, y, 96f, 44f);
            b.MarkControl("adjust_" + i, adjRect);
            if (price <= 0.0 && !o.PriceSet && !b.HasControl("set_price"))
                b.MarkControl("set_price", adjRect);
            if (SimCatalog.NeverPays(o, lc) && !b.HasControl("losing_price"))
                b.MarkControl("losing_price", adjRect);
            DeskKit.PenRule(b, y + RowH - 8f, ColNameX, 1120f - 36f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.12f), 11 + i);
            return y + RowH;
        }

        /// THE OPEN OFFER'S CARD — the whole ledger in one frame, the kit's
        /// grammar: the price dial, the world's itemised costs, the labor
        /// line, and ONE lane holding the sprint and the drop.
        static float OpenCard(BinderScreen b, float y, int i, GameState s,
                              double lc, double fm)
        {
            Offer o = s.Offers[i];
            int era = s.EraIndex();
            double capl = Gd.Clampf(o.CapacityPerUnit, 0.1, 40.0);
            bool svc = s.BizWhat == "Service";
            var clines = era >= 1 && o.CostLines != null ? o.CostLines : new List<CostLine>();
            var flines = era >= 1 && o.FixedLines != null ? o.FixedLines : new List<CostLine>();
            float cardH = 250f + (era >= 2 ? 52f : 0f) + (svc ? 26f : 0f)
                + (clines.Count + flines.Count) * 24f
                + (clines.Count + flines.Count > 0 ? 24f : 0f);
            DeskKit.CardBox frame = DeskKit.CardFrame(b, 10f, y, 1120f, cardH,
                "the open offer — " + (o.Name ?? "?"), false,
                1120f - DeskKit.CardPad * 2f);
            float cx = frame.ContentX;
            float cy = frame.ContentY;
            List<double> steps = DeskCatalog.PriceSteps(o);
            double cur = o.Price;
            double fair = o.FairPrice * fm;
            Offer oc = o;
            int idx = i;
            cy = DeskKit.Stepper(b, cy, new DeskKit.StepRow
            {
                Name = "price", X = cx,
                Why = "the going rate is $" + GameUi.Money(Gd.RoundToInt(fair))
                    + " — you name what you charge",
                Value = cur > 0.0 ? "$" + GameUi.Money(Gd.RoundToInt(cur)) + " per unit"
                    : "unpriced — bills at $" + GameUi.Money(Gd.RoundToInt(fair)),
                Effect = "margin $" + GameUi.Money(Gd.RoundToInt(SimCatalog.Contribution(o, lc, fm))) + "/unit",
                XVal = DeskKit.XValue, Pitch = 64f,
                AtMin = DeskKit.AtMin(steps, cur), AtMax = DeskKit.AtMax(steps, cur),
                OnMinus = () => DeskCatalog.PriceStep(oc, -1),
                OnPlus = () => DeskCatalog.PriceStep(oc, 1),
            });
            double fair0 = Math.Max(o.FairPrice, 1.0);
            if (clines.Count + flines.Count > 0)
            {
                b.L("what one sale costs — the world set these when the offer was written:",
                    cx, cy, DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 1060f);
                cy += 24f;
                foreach (CostLine ld in clines)
                {
                    b.L(string.Format(CultureInfo.InvariantCulture,
                        "{0} — ${1}/unit ({2}% of the going rate)", ld.Label ?? "line",
                        GameUi.Money(Gd.RoundToInt(ld.Amount)),
                        Gd.RoundToInt(ld.Amount / fair0 * 100.0)),
                        cx + 18f, cy, DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 1040f);
                    cy += 24f;
                }
                foreach (CostLine fd in flines)
                {
                    b.L(string.Format(CultureInfo.InvariantCulture,
                        "{0} — ${1}/wk, sold or not", fd.Label ?? "line",
                        GameUi.Money(Gd.RoundToInt(fd.Amount))),
                        cx + 18f, cy, DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 1040f);
                    cy += 24f;
                }
            }
            b.L(string.Format(CultureInfo.InvariantCulture,
                "= serve ${0}/unit (×{1:0.00} today) · standing tools ${2}/wk",
                GameUi.Money(Gd.RoundToInt(SimCatalog.ServedUnitCost(o, lc))), lc,
                GameUi.Money(Gd.RoundToInt(o.FixedWk))),
                cx, cy, DeskKit.Detail, DrawnUI.Blue, 1060f);
            cy += 28f;
            int be = SimCatalog.BreakEven(o, lc, fm);
            b.L(be < 0
                ? "every sale loses $" + GameUi.Money(Gd.RoundToInt(-SimCatalog.Contribution(o, lc, fm))) + " at this price"
                : string.Format(CultureInfo.InvariantCulture,
                    "break-even: {0} sales/wk pay the standing costs", be),
                cx, cy, DeskKit.Detail,
                be < 0 ? DrawnUI.Coral : DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f), 1060f);
            cy += 28f;
            if (svc)
            {
                double slots = SimWorks.ServiceCapacity(s);
                b.L(string.Format(CultureInfo.InvariantCulture,
                    "one {0} = {1:0.0} hours of hands · today's crew: ≈ {2}/wk before hiring",
                    (o.Unit ?? "unit").StartsWith("per ") ? o.Unit.Substring(4) : o.Unit,
                    capl, capl > 0.0 ? (int)(slots / capl) : 0),
                    cx, cy, DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f), 1060f);
                cy += 26f;
            }
            if (era >= 2)
            {
                double w = o.Weight;
                cy = DeskKit.Stepper(b, cy, new DeskKit.StepRow
                {
                    Name = "weight", X = cx,
                    Value = string.Format(CultureInfo.InvariantCulture, "{0:0.0} of the wallet", w),
                    Effect = string.Format(CultureInfo.InvariantCulture, "shelf ∑{0:0.0} of {1:0.0}",
                        SimCatalog.ShelfWeight(s), SimCatalog.ShelfWeightCap),
                    XVal = DeskKit.XValue, Pitch = 52f,
                    AtMin = DeskKit.AtMin(DeskCatalog.WeightSteps, w),
                    AtMax = w >= SimCatalog.MaxWeight - 0.001,
                    OnMinus = () => oc.Weight = Gd.Clampf(DeskKit.Ladder(DeskCatalog.WeightSteps, oc.Weight, -1),
                        SimCatalog.MinWeight, SimCatalog.MaxWeight),
                    OnPlus = () => oc.Weight = Gd.Clampf(DeskKit.Ladder(DeskCatalog.WeightSteps, oc.Weight, 1),
                        SimCatalog.MinWeight, SimCatalog.MaxWeight),
                });
            }
            string nm = o.Name ?? idx.ToString(CultureInfo.InvariantCulture);
            bool hasSprint = false;
            for (int bi = 0; bi < s.Bets.Count; bi++)
            {
                Bet bd = s.Bets[bi];
                if (bd.Kind == "cost_down" && (bd.Offer ?? "") == nm && !bd.Shipped)
                    hasSprint = true;
            }
            if (hasSprint)
                b.L("a cost sprint is on the roadmap — the team is on it",
                    cx, cy + 6f, DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f), 600f);
            else
                DeskKit.Arm(b, "sprint_" + nm, "cut the serve cost — a team sprint",
                    "3 R&D-weeks of the team — sure?", cx, cy + 2f,
                    () => SimRoadmap.AddCostDownBet(s, nm), 420f, 20f);
            DeskKit.Arm(b, "drop_" + nm, "drop this offer ×", "sure? it disappears ×",
                790f, cy + 2f, () =>
                {
                    SimCatalog.RemoveOffer(s, idx);
                    s.LogAction("DROPPED the offer: " + nm);
                    b.Desk["open_row"] = -1;
                }, 300f, 20f);
            return frame.Bottom + 10f;
        }

        /// THE DEMAND VERDICT — one colored word, the heat ramp, never a sentence.
        static void VerdictWord(Offer o, double price, double fm, double lc,
                                out string word, out Color col)
        {
            if (price <= 0.0 && o.PriceSet) { word = "free on purpose"; col = DrawnUI.Blue; return; }
            if (price <= 0.0) { word = "unpriced"; col = DrawnUI.Coral; return; }
            if (SimCatalog.NeverPays(o, lc)) { word = "loses money"; col = DrawnUI.Coral; return; }
            double dem = SimEngine.OfferDemand(o, price, fm);
            if (dem >= 1.15) { word = "a deal"; col = DrawnUI.Sage; return; }
            if (dem <= 0.25) { word = "absurd"; col = DrawnUI.Coral; return; }
            if (dem < 0.85) { word = "pricey"; col = DrawnUI.Yellow; return; }
            word = "fair";
            col = DrawnUI.WithAlpha(DrawnUI.Ink, 0.8f);
        }

        /// ENTERPRISE RETROFIT: per-seat + named accounts, fed by the pipeline.
        static float NamedAccountsLine(BinderScreen b, GameState s, float y)
        {
            int seats = 0;
            for (int i = 0; i < s.Logos.Count; i++) seats += s.Logos[i].Seats;
            string text = s.Logos.Count == 0
                ? "named accounts: none signed yet — a contract is the first discount conversation"
                : "named accounts: " + s.Logos.Count + " logos · " + seats
                  + " seats · a seat bills ≈ $"
                  + GameUi.Money(Gd.RoundToInt(SimPipeline.UnitRevWk(s)))
                  + "/wk — discounts live on the signed contracts";
            b.L(text, DeskKit.XId, y, DeskKit.Law, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 1100f);
            return y + 30f;
        }

        /// THE DEFINE-AN-OFFER DOOR, or the honest reason it is shut.
        static void DefineDoor(BinderScreen b, float y)
        {
            string shut = SimCatalog.ShelfFullLine(b.State);
            if (!string.IsNullOrEmpty(shut))
            {
                b.L(shut, DeskKit.XId, y + 8f, DeskKit.Detail,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 900f);
                return;
            }
            DeskKit.Word(b, "+ define a new offer", DeskKit.XId, y,
                () => b.Desk["mode"] = "write", DeskKit.Status, DrawnUI.Ink, 340f);
            b.MarkControl("offer_form", new Rect(DeskKit.XId, y, 340f, 40f));
        }

        /// The teaching foot. WARNINGS OUTRANK WISDOM.
        static void Foot(BinderScreen b, GameState s, float baseY = 806f)
        {
            double lc = SimEngine.LearningCurve(s);
            double fm = SimEngine.StreetFairMult(s);
            string computed = "";
            double arpu = SimEngine.OffersArpu(s);
            if (arpu >= 0.0)
            {
                double cpc = SimEngine.OffersCogsPerCustomer(s);
                computed = string.Format(CultureInfo.InvariantCulture,
                    "unit economics: ≈ ${0:0.0} ARPU − ${1:0.0} COGS = ${2:0.0} contribution per customer per week -> ≈ ${3}/wk at {4} customers",
                    arpu, cpc, arpu - cpc,
                    GameUi.Money(Gd.RoundToInt((arpu - cpc) * s.Traction)), s.Traction);
            }
            string warning = "";
            for (int i = 0; i < s.Offers.Count; i++)
            {
                Offer od = s.Offers[i];
                if (SimCatalog.NeverPays(od, lc))
                {
                    warning = "'" + (string.IsNullOrEmpty(od.Name) ? "an offer" : od.Name)
                        + "' never pays for itself — every sale loses $"
                        + GameUi.Money(Gd.RoundToInt(-SimCatalog.Contribution(od, lc, fm)));
                    break;
                }
            }
            DeskKit.Footer(b, computed,
                "price at the street's level and demand is fair · dropping an offer (open it, then drop) migrates its customers to the shelf — or churns them",
                warning, baseY, baseY + 34f);
        }

        // ── the unfolded shelf ─────────────────────────────────────────────

        /// The fold opened: every offer on one sheet, no hero.
        static void AllOffers(BinderScreen b)
        {
            GameState s = b.State;
            DeskKit.Back(b, "back to the rate card", () => b.Desk["mode"] = "");
            double lc = SimEngine.LearningCurve(s);
            double fm = SimEngine.StreetFairMult(s);
            int n = s.Offers.Count;
            float cardH = DeskKit.CardHead + 30f + n * RowH + 26f;
            DeskKit.CardBox frame = DeskKit.CardFrame(b, 10f, 64f, 1120f, cardH,
                "the whole shelf — " + n + " offers");
            float cy = frame.ContentY;
            cy = HeadRow(b, cy);
            for (int i = 0; i < n; i++)
                cy = Row(b, cy, i, s, lc, fm);
            float base2 = Mathf.Max(DeskKit.DoLaneY, frame.Bottom + 16f);
            DoLaneDraw(b, s, base2);
            FireSpot(b);
            Foot(b, s, base2 + 44f);
        }

        // ── S3 · the DO lane + the focus walk ──────────────────────────────

        /// The desk's primary acts in the ONE slot: price the row that needs
        /// it (first unpriced, else the flagship), or add to the shelf. The
        /// price press walks the hand to that row's own ADJUST squares.
        static void DoLaneDraw(BinderScreen b, GameState s, float baseY = -1f)
        {
            int t = PriceTarget(s);
            var actions = new List<DeskKit.DoAction>();
            if (t >= 0)
            {
                string tn = string.IsNullOrEmpty(s.Offers[t].Name) ? "the offer" : s.Offers[t].Name;
                int tNow = t;
                actions.Add(new DeskKit.DoAction
                {
                    Label = "set price — " + tn,
                    Tier = "",
                    Cb = () =>
                    {
                        if (!b.HasControl("adjust_" + tNow)) b.Desk["mode"] = "all";
                        b.Desk["spot"] = "adjust_" + tNow;
                    },
                });
            }
            if (string.IsNullOrEmpty(SimCatalog.ShelfFullLine(s)))
                actions.Add(new DeskKit.DoAction
                {
                    Label = "add an offer",
                    Tier = "",
                    Cb = () => b.Desk["mode"] = "write",
                });
            DeskKit.DoLane(b, actions, baseY);
        }

        /// The DO lane's object: the first unpriced offer, else the flagship
        /// (the heaviest weight — where most of the money walks through).
        static int PriceTarget(GameState s)
        {
            if (s.Offers.Count == 0) return -1;
            for (int i = 0; i < s.Offers.Count; i++)
                if (s.Offers[i].Price <= 0.0 && !s.Offers[i].PriceSet) return i;
            int best = 0;
            for (int i = 1; i < s.Offers.Count; i++)
                if (s.Offers[i].Weight > s.Offers[best].Weight) best = i;
            return best;
        }

        /// A DO press asked for a spotlight; the registry filled during THIS
        /// draw, so the walk fires after every row has marked its switch.
        static void FireSpot(BinderScreen b)
        {
            object sv;
            string sid = b.Desk.TryGetValue("spot", out sv) ? (sv as string ?? "") : "";
            if (sid.Length == 0) return;
            b.Desk.Remove("spot");
            if (b.HasControl(sid)) b.Spotlight(b.ControlRect(sid));
        }

        // ── S4 · the street's read (the fair band) ─────────────────────────

        /// THE VERDICT, OPENED: a paper card saying the street math in
        /// receipt lines with THE FAIR BAND DRAWN — the price axis, the
        /// stretch demand calls fair, the street's rate ticked, your price
        /// dotted onto it. Any press or Esc closes the read first.
        static void VerdictCard(BinderScreen b, GameState s, int i)
        {
            if (i < 0 || i >= s.Offers.Count)
            {
                b.Desk["mode"] = "";
                return;
            }
            Offer o = s.Offers[i];
            double lc = SimEngine.LearningCurve(s);
            double fm = SimEngine.StreetFairMult(s);
            double fair = Gd.Maxf(o.FairPrice * fm, 0.01);
            double e = Gd.Maxf(o.Elasticity, 0.05);
            double price = o.Price;
            bool unpriced = price <= 0.0 && !o.PriceSet;
            double billed = unpriced ? fair : price;
            string vWord;
            Color vCol;
            VerdictWord(o, price, fm, lc, out vWord, out vCol);
            Button catcher = DeskKit.Word(b, "", 0f, 0f, () => b.Desk["mode"] = "",
                DeskKit.Detail, DrawnUI.Ink, 1140f);
            catcher.GetComponent<RectTransform>().sizeDelta = new Vector2(1140f, 880f);
            List<DeskKit.TicketLine> lines = StreetLines(b, s, o, price, fair, lc, fm, unpriced);
            float cardH = 56f + 132f + lines.Count * 30f + 18f;
            DeskKit.CardBox frame = DeskKit.CardFrame(b, 290f, 200f, 560f, cardH,
                "the street's read — one word, priced");
            float cx = frame.ContentX;
            float cy = frame.ContentY;
            double lo = fair * Math.Pow(1.15, -1.0 / e);
            double hiP = fair * Math.Pow(0.85, -1.0 / e);
            double absurd = fair * Math.Pow(0.25, -1.0 / e);
            double pmax = Gd.Maxf(Gd.Maxf(absurd * 1.15, billed * 1.2), fair * 1.6);
            FairBand(b, cx, cy, 560f - DeskKit.CardPad * 2f, 116f,
                fair, lo, hiP, absurd, billed, pmax, vCol);
            float moneyX = frame.MoneyX;
            float ly = cy + 132f;
            for (int n = 0; n < lines.Count; n++)
            {
                DeskKit.FitLine(b, lines[n].Label, cx, ly, 19f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.85f), 300f);
                TextMeshProUGUI v = DeskKit.FitLine(b, lines[n].Value, cx + 310f, ly, 19f,
                    lines[n].Col ?? DrawnUI.Ink, moneyX - cx - 310f);
                v.alignment = TextAlignmentOptions.TopRight;
                ly += 30f;
            }
        }

        /// THE FAIR BAND, in fills: the sage stretch demand calls fair,
        /// yellow to where it turns absurd, coral past that, the street's
        /// rate ticked in ink, your price dotted down onto its bead. (The
        /// Godot twin draws the same geometry freehand.)
        static void FairBand(BinderScreen b, float x, float y, float w, float h,
                             double fair, double lo, double hi, double absurd,
                             double price, double pmax, Color vcol)
        {
            float ax = y + h - 34f;
            float sc = w / (float)Gd.Maxf(pmax, 0.01);
            DrawnUI.Fill(b.Content, "fb_fair", DrawnUI.WithAlpha(DrawnUI.Sage, 0.45f),
                x + (float)lo * sc, ax - 26f, (float)(hi - lo) * sc, 26f).raycastTarget = false;
            DrawnUI.Fill(b.Content, "fb_pricey", DrawnUI.WithAlpha(DrawnUI.Yellow, 0.30f),
                x + (float)hi * sc, ax - 26f,
                (float)(Math.Min(absurd, pmax) - hi) * sc, 26f).raycastTarget = false;
            if (absurd < pmax)
                DrawnUI.Fill(b.Content, "fb_absurd", DrawnUI.WithAlpha(DrawnUI.Coral, 0.25f),
                    x + (float)absurd * sc, ax - 26f,
                    (float)(pmax - absurd) * sc, 26f).raycastTarget = false;
            DrawnUI.Fill(b.Content, "fb_axis", DrawnUI.Ink, x, ax, w, 2.4f).raycastTarget = false;
            DrawnUI.Fill(b.Content, "fb_tick", DrawnUI.Ink, x + (float)fair * sc - 1f,
                ax - 30f, 2.2f, 36f).raycastTarget = false;
            b.L("street $" + Gd.RoundToInt(fair), x + (float)fair * sc - 34f, ax + 8f, 15f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 140f);
            b.L("fair", x + (float)lo * sc + 6f, ax - 48f, 15f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f), 60f);
            float px = Mathf.Clamp(x + (float)price * sc, x, x + w);
            for (float yy = y + 14f; yy < ax - 6f; yy += 11f)
                DrawnUI.Fill(b.Content, "fb_dot", DrawnUI.Coral, px - 1.2f, yy, 2.4f,
                    Mathf.Min(6f, ax - 6f - yy)).raycastTarget = false;
            var bead = DrawnUI.Fill(b.Content, "fb_bead", vcol, px - 6f, ax - 19f, 12f, 12f);
            bead.raycastTarget = false;
            DrawnUI.AddInkEdge(bead.rectTransform, new Vector2(12f, 12f),
                new DrawnUI.PaperStyle
                {
                    ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                    StepsPerEdge = 5, Jitter = 0.7f, Thickness = 2f, Seed = 31,
                });
            b.L("you: $" + Gd.RoundToInt(price), Mathf.Clamp(px - 30f, x, x + w - 84f),
                y - 2f, 15f, DrawnUI.Coral, 120f);
        }

        /// The receipt lines: the exact terms the verdict is made of — demand
        /// is (price/fair)^−elasticity, clamped at ×2. Engine numbers only.
        static List<DeskKit.TicketLine> StreetLines(BinderScreen b, GameState s, Offer o,
                double price, double fair, double lc, double fm, bool unpriced)
        {
            string vWord;
            Color vCol;
            VerdictWord(o, price, fm, lc, out vWord, out vCol);
            double e = Gd.Maxf(o.Elasticity, 0.05);
            double dem = SimEngine.OfferDemand(o, unpriced ? fair : price, fm);
            var lines = new List<DeskKit.TicketLine>
            {
                new DeskKit.TicketLine { Label = string.IsNullOrEmpty(o.Name) ? "the offer" : o.Name,
                    Value = vWord, Col = vCol },
                new DeskKit.TicketLine { Label = "your price",
                    Value = unpriced
                        ? "unpriced — bills $" + GameUi.Money(Gd.RoundToInt(fair))
                        : "$" + GameUi.Money(Gd.RoundToInt(price)) },
                new DeskKit.TicketLine { Label = "the street pays",
                    Value = "$" + GameUi.Money(Gd.RoundToInt(fair)) },
                new DeskKit.TicketLine { Label = "demand at this price",
                    Value = string.Format(CultureInfo.InvariantCulture, "×{0:0.00}", dem) },
                new DeskKit.TicketLine { Label = "the fair band",
                    Value = "$" + GameUi.Money(Gd.RoundToInt(fair * Math.Pow(1.15, -1.0 / e)))
                        + " – $" + GameUi.Money(Gd.RoundToInt(fair * Math.Pow(0.85, -1.0 / e))) },
            };
            if (SimCatalog.NeverPays(o, lc))
                lines.Add(new DeskKit.TicketLine { Label = "every sale loses",
                    Value = "$" + GameUi.Money(Gd.RoundToInt(-SimCatalog.Contribution(o, lc, fm))),
                    Col = DrawnUI.Coral });
            else
                lines.Add(new DeskKit.TicketLine { Label = "serve costs / margin",
                    Value = "$" + GameUi.Money(Gd.RoundToInt(SimCatalog.ServedUnitCost(o, lc)))
                        + " / $" + GameUi.Money(Gd.RoundToInt(SimCatalog.Contribution(o, lc, fm))) });
            return lines;
        }

        // ── S8 · the rail's own two reads ──────────────────────────────────

        /// Pricing never sleeps: the first real decision lives here from
        /// week one.
        public static bool IsDormant(GameState s)
        {
            return false;
        }

        /// The rail's four-character read — the shelf's average asking price.
        public static string MicroStatus(GameState s)
        {
            double sum = 0.0;
            int n = 0;
            for (int i = 0; i < s.Offers.Count; i++)
            {
                if (s.Offers[i].Price > 0.0)
                {
                    sum += s.Offers[i].Price;
                    n++;
                }
            }
            return n == 0 ? "" : "$" + Gd.RoundToInt(sum / n) + " avg";
        }
    }
}
