using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
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
    /// write→wait→review cost-lines road, and the drop arm lives on the
    /// detail sheet behind its two-tap.
    ///
    /// AUDIENCE VARIANTS: Consumer rows carry units/wk under the name;
    /// Enterprise adds the named-account per-seat line under the table, read
    /// from the pipeline. Fair-price backstop and the "!" unpriced warning
    /// are preserved.
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
            big = "$" + Gd.RoundToInt(arpu) + " in · $" + Gd.RoundToInt(cogs)
                  + " out → $" + Gd.RoundToInt(arpu - cogs);
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
            string big, line;
            double arpu, cogs;
            Hero(s, out big, out line, out arpu, out cogs);
            float y = DeskKit.HeroBand(b, big, line);
            if (s.Offers.Count == 0)
            {
                y = DeskKit.Empty(b, DeskKit.XId, y,
                    "the world hasn't defined your offers yet — they arrive with the bible.",
                    "a company with nothing on the shelf earns nothing: write down what you sell.");
                DefineDoor(b, y + 12f);
                Foot(b, s);
                return;
            }
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
            float cardH = DeskKit.CardHead + 30f + shown.Count * RowH + 10f;
            DeskKit.CardBox frame = DeskKit.CardFrame(b, 10f, y, 1120f, cardH, "the rate card");
            float cy = frame.ContentY;
            cy = HeadRow(b, cy);
            for (int n = 0; n < shown.Count; n++)
                cy = Row(b, cy, shown[n], s, lc, fm);
            y = frame.Bottom + 10f;
            if (folded > 0)
                y = DeskKit.FoldRow(b, DeskKit.XId, y, folded, "offers, healthy",
                    () => b.Desk["mode"] = "all");
            if (s.BizWho == "Enterprise")
                y = NamedAccountsLine(b, s, y);
            DefineDoor(b, y + 6f);
            Foot(b, s);
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
            b.L(sub, ColNameX, y + 27f, 15f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f), ColNameW);
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
            b.L(vWord, ColVerdictX, y + 2f, 22f, vCol, ColVerdictW);
            int idx = i;
            DeskKit.Expand(b, ColExpandX, y - 4f, () =>
            {
                b.Desk["mode"] = "detail";
                b.Desk["row"] = idx;
            });
            List<double> steps = DeskCatalog.PriceSteps(o);
            Offer oc = o;
            DeskKit.AdjustPair(b, ColAdjustX, y + 4f,
                () => DeskCatalog.PriceStep(oc, -1),
                () => DeskCatalog.PriceStep(oc, 1),
                DeskKit.AtMin(steps, price), DeskKit.AtMax(steps, price));
            DeskKit.PenRule(b, y + RowH - 8f, ColNameX, 1120f - 36f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.12f), 11 + i);
            return y + RowH;
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
        }

        /// The teaching foot. WARNINGS OUTRANK WISDOM.
        static void Foot(BinderScreen b, GameState s)
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
                warning, 806f, 840f);
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
            float cardH = DeskKit.CardHead + 30f + n * RowH + 10f;
            DeskKit.CardBox frame = DeskKit.CardFrame(b, 10f, 64f, 1120f, cardH,
                "the whole shelf — " + n + " offers");
            float cy = frame.ContentY;
            cy = HeadRow(b, cy);
            for (int i = 0; i < n; i++)
                cy = Row(b, cy, i, s, lc, fm);
            Foot(b, s);
        }
    }
}
