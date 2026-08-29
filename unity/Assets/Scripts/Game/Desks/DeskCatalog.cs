using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;
using Runway.Core;
using Runway.Llm;

namespace Runway.Game
{
    /// <summary>
    /// DESK — the binder's `pricing` tab. Spec: docs/design/01-catalog.md section 7
    ///
    /// BinderScreen dispatches the tab body here and passes ITSELF, so this file
    /// draws through the binder's own hand and never reaches into the sheet
    /// directly. The drawn components come from Game/DeskKit.cs — a desk that
    /// forks one has shipped a second design system.
    ///
    /// THE SHOPKEEPER'S SHELF (10-interface-language section 5.1). Five states,
    /// one sheet:
    ///
    ///   LIST     every offer's unit economics, scannable in four seconds
    ///   DETAIL   one offer's whole ledger: price, every cost line, break-even
    ///   WRITE    the founder describes something new in plain words
    ///   WAIT     the street is pricing it (cancellable, callback-guarded)
    ///   REVIEW   the proposal, adjustable, awaiting the founder's pen
    ///
    /// THE OWNER'S REQUIREMENT: nothing an LLM wrote ever enters the books unseen.
    /// Every road to a new offer ends on the REVIEW card, and the card's confirm
    /// is the ONLY call to SimCatalog.AddOffer on this desk.
    ///
    /// Desk-local state lives in b.Desk and dies with the object (HOOKS.md):
    /// `mode` and `row` are the reserved keys Esc pops, `armed` is the kit's
    /// two-tap arm, and `pending` / `house` / `text` / `refused` are this desk's
    /// own. None of it is ever saved — closing the binder discards a proposal
    /// mid-air, which is the same law from the other side.
    ///
    /// TWIN LAW: this file and game/src/ui/desks/desk_catalog.gd draw the same
    /// rows at the same coordinates.
    /// </summary>
    public static class DeskCatalog
    {
        // ── the named ladders ──────────────────────────────────────────────────
        // Every stepper walks a named ladder and the engine re-clamps on write;
        // the UI is never trusted (10-interface-language section 2.1).
        static readonly double[] PriceMults =
            { 0.4, 0.55, 0.7, 0.85, 1.0, 1.15, 1.35, 1.6, 2.0, 2.6, 3.5, 5.0 };
        static readonly double[] VarMults =
            { 0.0, 0.02, 0.05, 0.08, 0.12, 0.16, 0.22, 0.30, 0.40, 0.50 };
        static readonly List<double> FixedSteps = new List<double>
            { 0, 5, 10, 15, 25, 40, 60, 90, 140, 220, 350, 550, 900, 1400, 2200, 3500, 5000 };
        internal static readonly List<double> WeightSteps = new List<double>
            { 0.2, 0.4, 0.6, 0.8, 1.0, 1.3, 1.6, 2.0, 2.5, 3.0 };

        const float RowPitch = 62f;
        const float RowsY = 84f;
        /// The last y a row may START at: the growth invitation and the two footer
        /// lines own everything under it.
        const float ListBottom = 620f;
        /// The shelf itself never holds more than 8 (SimCatalog.EraOfferCap), so
        /// this cap can only bite on a save that arrived from somewhere else — and
        /// then it says so rather than hiding a row behind nothing.
        const int ListMax = 8;

        // ── the desk-local keys ────────────────────────────────────────────────

        static string Get(BinderScreen b, string key)
        {
            object v;
            return b.Desk.TryGetValue(key, out v) && v != null ? v.ToString() : "";
        }

        static Offer Pending(BinderScreen b)
        {
            object v;
            return b.Desk.TryGetValue("pending", out v) ? v as Offer : null;
        }

        static int RowIndex(BinderScreen b)
        {
            object v;
            if (!b.Desk.TryGetValue("row", out v) || v == null) return -1;
            return Convert.ToInt32(v, CultureInfo.InvariantCulture);
        }

        static string Money(double v) { return GameUi.Money(Gd.RoundToInt(v)); }

        static Color Ink(float a) { return DrawnUI.WithAlpha(DrawnUI.Ink, a); }

        // ── the dispatch ───────────────────────────────────────────────────────

        /// <summary>Draw the five-state pricing machine.</summary>
        public static void Draw(BinderScreen b)
        {
            string mode = Get(b, "mode");
            if (mode.Length == 0)
            {
                // Esc walked out of REVIEW (or the tab was re-entered): a proposal
                // that is no longer on screen is a proposal that no longer exists.
                b.Desk.Remove("pending");
                b.Desk.Remove("house");
                b.Desk.Remove("refused");
                b.Desk.Remove("short");
            }
            switch (mode)
            {
                case "detail": Detail(b); break;
                case "write": Write(b); break;
                case "clarify": ClarifyState(b); break;
                case "wait": WaitState(b); break;
                case "review": ReviewState(b); break;
                default: List(b); break;
            }
        }

        /// <summary>A press inside this desk. Every control here carries its own
        /// closure, so the id router stays unused — it exists for desks that
        /// prefer it.</summary>
        public static void Handle(BinderScreen b, string id)
        {
        }

        // ── 7.1  THE LIST ──────────────────────────────────────────────────────

        static void List(BinderScreen b)
        {
            GameState st = b.State;
            DeskKit.Title(b, "pricing — what " + st.CompanyName + " sells");
            if (st.Offers == null || st.Offers.Count == 0)
            {
                float y0 = DeskKit.Empty(b, DeskKit.XId, 96f,
                    "the world hasn't defined your offers yet — they arrive with the bible.",
                    "a company with nothing on the shelf earns nothing: write down what you sell.");
                NewOfferWord(b, y0 + 12f);
                DeskKit.Footer(b, "",
                    "COGS bills only when you sell · fixed bills either way · price − variable = contribution margin",
                    "");
                return;
            }
            double lc = SimEngine.LearningCurve(st);
            double fm = SimEngine.StreetFairMult(st);
            float y = RowsY;
            // PRICING DURING A WAR IS THE DECISION THIS TAB EXISTS FOR (03 5.1): a
            // rival cutting prices moves THE STREET'S reference, not yours, so every
            // verdict below is measured against the lower number and the page says why.
            int war = WarPct(fm);
            if (war > 0)
            {
                b.L(string.Format(CultureInfo.InvariantCulture,
                        "price war: the street's reference is {0}% down — the same price reads dearer this week", war),
                    DeskKit.XId, 58f, DeskKit.Law, DrawnUI.Coral, 1100f);
                y += 26f;
            }
            // ROWS ARE MEASURED, AND THEY STOP WHILE THERE IS STILL PAPER. A price
            // war or a floor-era discount read makes the verdict wrap, which grows
            // the row; eight grown rows do not fit 760px, so the list closes on
            // "+N more" rather than writing the last one over the footer.
            int shown = 0;
            int room = Gd.Mini(st.Offers.Count, ListMax);
            for (int i = 0; i < room; i++)
            {
                if (y + RowPitch > ListBottom) break;
                y = Row(b, y, i, lc, fm);
                shown++;
            }
            y = DeskKit.More(b, DeskKit.XId, y, st.Offers.Count - shown,
                "are on the shelf behind these");
            NewOfferWord(b, y + 8f);
            ListFooter(b, lc, fm);
        }

        /// The bottom of the sheet: what the catalog earns, and the laws that made
        /// it. WARNINGS OUTRANK WISDOM — when a price is losing money on every
        /// sale, the rules line yields to the lesson.
        static void ListFooter(BinderScreen b, double lc, double fm)
        {
            GameState st = b.State;
            string computed = "";
            string rules = "the curve: price at the street's level and demand is fair · discount and demand grows · overprice and it dies fast";
            if (st.EraIndex() >= 1)
            {
                double arpu = SimEngine.OffersArpu(st);
                if (arpu >= 0.0)
                {
                    double cpc = SimEngine.OffersCogsPerCustomer(st);
                    computed = string.Format(CultureInfo.InvariantCulture,
                        "unit economics: ≈ ${0:0.0} ARPU − ${1:0.0} COGS = ${2:0.0} contribution per customer per week  ->  ≈ ${3}/wk at {4} customers",
                        arpu, cpc, arpu - cpc, Money((arpu - cpc) * st.Traction), st.Traction);
                }
                rules = "COGS bills only when you sell · fixed bills either way · price − variable = contribution margin";
            }
            string warning = "";
            foreach (Offer o in st.Offers)
            {
                if (!SimCatalog.NeverPays(o, lc)) continue;
                warning = string.Format(CultureInfo.InvariantCulture,
                    "'{0}' never pays for itself — every sale loses ${1}",
                    o.Name, Money(-SimCatalog.Contribution(o, lc, fm)));
                break;
            }
            DeskKit.Footer(b, computed, rules, warning);
        }

        /// THE GROWTH INVITATION, or the honest reason it is shut. A dead button
        /// with no reason beside it is the one thing a desk may never show.
        static void NewOfferWord(BinderScreen b, float y)
        {
            string shut = SimCatalog.ShelfFullLine(b.State);
            if (shut.Length > 0)
            {
                b.L(shut, DeskKit.XId, y + 10f, DeskKit.Detail, Ink(0.5f), 900f);
                return;
            }
            DeskKit.Word(b, "+ sell something new", DeskKit.XId, y,
                () => { b.Desk["mode"] = "write"; }, DeskKit.Status, DrawnUI.Ink, 340f);
        }

        /// ONE COLLAPSED ROW (10-interface-language section 2.2): the name, the
        /// fine print truly fine, the verdict where the eye lands, and the controls
        /// on the right.
        static float Row(BinderScreen b, float y, int i, double lc, double fm)
        {
            GameState st = b.State;
            Offer o = st.Offers[i];
            int idx = i;
            b.L((o.Name ?? "?").ToUpper() + "  ·  " + (o.Unit ?? ""),
                DeskKit.XId, y, 28f, DrawnUI.Ink, 400f);
            b.L(Receipts(b, o, lc, fm), DeskKit.XId, y + 32f, 20f, Ink(0.55f), 410f);
            // THE ROW IS AS TALL AS ITS VERDICT. `about fair (−2% vs street)` and
            // `absurd — ~nobody buys` both wrap the status column, and at a fixed
            // 62px the second line was written into the next offer's own verdict —
            // three rows of status that no longer lined up with the three names.
            float statusH = Status(b, o, y, lc, fm);
            DeskKit.Expand(b, DeskKit.XExpand, y, () =>
            {
                b.Desk["mode"] = "detail";
                b.Desk["row"] = idx;
            });
            List<double> steps = PriceSteps(o);
            double cur = o.Price;
            StepBtn(b, "−", DeskKit.XMinus, y, DeskKit.AtMin(steps, cur),
                () => PriceStep(o, -1));
            StepBtn(b, "+", DeskKit.XPlus, y, DeskKit.AtMax(steps, cur),
                () => PriceStep(o, 1));
            return y + Mathf.Max(RowPitch, statusH + 14f);
        }

        /// The receipts under a name. The garage sees one number, because at the
        /// garage price is the only dial; real cost accounting appears at
        /// coworking (01 section 5).
        static string Receipts(BinderScreen b, Offer o, double lc, double fm)
        {
            double served = SimCatalog.ServedUnitCost(o, lc);
            if (b.State.EraIndex() < 1) return "serve ≈ $" + Money(served);
            string learned = lc < 0.995
                ? string.Format(CultureInfo.InvariantCulture, " (×{0:0.00} learned)", lc) : "";
            return string.Format(CultureInfo.InvariantCulture,
                "serve ≈ ${0}{1} · fixed ${2}/wk · margin ${3}/unit",
                Money(served), learned, Money(o.FixedWk),
                Money(SimCatalog.Contribution(o, lc, fm)));
        }

        /// THE THREE-STATE STATUS COLUMN: a giveaway the founder chose, a price
        /// nobody named, or the price with its verdict. COLOUR NEVER CARRIES ALONE
        /// — every one of them says it in words first.
        static float Status(BinderScreen b, Offer o, float y, double lc, double fm)
        {
            string text;
            Color col = DrawnUI.Ink;
            if (o.Price <= 0.0 && o.PriceSet)
            {
                text = "FREE ON PURPOSE — pays in users, not dollars";
                col = DrawnUI.Blue;
            }
            else if (o.Price <= 0.0)
            {
                text = "! billing at the going rate $" + Money(o.FairPrice * fm);
                col = DrawnUI.Coral;
            }
            else
            {
                text = string.Format(CultureInfo.InvariantCulture, "${0}  ·  margin ${1}/unit  ·  {2}",
                    Money(o.Price), Money(SimCatalog.Contribution(o, lc, fm)),
                    Verdict(b.State, o, o.Price, fm));
                if (SimCatalog.NeverPays(o, lc) || SimEngine.OfferDemand(o, o.Price, fm) <= 0.25)
                    col = DrawnUI.Coral;
            }
            TextMeshProUGUI l = b.L(text, DeskKit.XValue, y + 4f, 26f, col, 480f);
            return 4f + Mathf.Max(BinderScreen.Height(l), 34f);
        }

        /// What the street makes of this price, in words. Discounting only gets
        /// NAMED as a strategy at office, where portfolio management unlocks.
        static string Verdict(GameState st, Offer o, double price, double fm)
        {
            double dem = SimEngine.OfferDemand(o, price, fm);
            string word = "about fair";
            if (dem >= 1.15)
                word = string.Format(CultureInfo.InvariantCulture, "a deal — demand ×{0:0.0}", dem);
            else if (dem <= 0.25) word = "absurd — ~nobody buys";
            else if (dem < 0.85)
                word = string.Format(CultureInfo.InvariantCulture,
                    "pricey — {0}% of fair demand", (int)(dem * 100.0));
            double street = o.FairPrice * fm;
            if (st.EraIndex() >= 2 && street > 0.0 && price < street)
                word += string.Format(CultureInfo.InvariantCulture, " (−{0}% vs street)",
                    Gd.RoundToInt((1.0 - price / street) * 100.0));
            return word;
        }

        /// How far a rival's price war has moved the going rate, in whole percent.
        /// 0 in a quiet week — the street's reference IS the list price until
        /// somebody cuts.
        static int WarPct(double fm) { return Gd.RoundToInt((1.0 - fm) * 100.0); }

        // ── 7.2  THE DETAIL ────────────────────────────────────────────────────

        /// One offer, the whole pane — the row's fine print with room to think.
        /// Only one is ever open, and DETAIL REPLACES the list rather than pushing
        /// it down.
        static void Detail(BinderScreen b)
        {
            GameState st = b.State;
            int i = RowIndex(b);
            if (st.Offers == null || i < 0 || i >= st.Offers.Count)
            {
                b.Desk["mode"] = "";        // the offer was dropped out from under the page
                b.Desk.Remove("row");
                List(b);
                return;
            }
            Offer o = st.Offers[i];
            int era = st.EraIndex();
            double lc = SimEngine.LearningCurve(st);
            double fm = SimEngine.StreetFairMult(st);
            string nm = o.Name ?? "an offer";
            DeskKit.Back(b, "back to all offers", () =>
            {
                b.Desk["mode"] = "";
                b.Desk.Remove("row");
            });
            // DROPPING IS INSTANT BEHIND THE ARM (DECISIONS.md): the lost revenue
            // is the natural cost of the decision; the second tap is the only
            // ceremony it gets.
            DeskKit.Arm(b, "drop", "drop this offer ×", "sure? it disappears ×",
                880f, 6f, () =>
                {
                    SimCatalog.RemoveOffer(st, i);
                    st.LogAction("DROPPED the offer: " + nm);
                    b.Desk["mode"] = "";
                    b.Desk.Remove("row");
                }, 260f, 24f);
            float y = 58f;
            b.L(nm.ToUpper() + " · " + (o.Unit ?? ""), DeskKit.XId, y, 32f, DrawnUI.Ink, 860f);
            y += 34f;
            string learnedTxt = lc < 0.995
                ? string.Format(CultureInfo.InvariantCulture, " (learning ×{0:0.00})", lc) : "";
            // the going rate is THE STREET'S, and a rival's war moves it: the
            // reference every number below is measured against says so in the same
            // breath (03 section 5.1)
            int war = WarPct(fm);
            b.L(string.Format(CultureInfo.InvariantCulture,
                    "the street charges ≈ ${0}{1} · a sale costs ≈ ${2} to serve{3} · fixed ${4}/wk",
                    Money(o.FairPrice * fm),
                    war > 0 ? string.Format(CultureInfo.InvariantCulture, " (price war: −{0}%)", war) : "",
                    Money(SimCatalog.ServedUnitCost(o, lc)), learnedTxt, Money(o.FixedWk)),
                DeskKit.XId, y, 24f, Ink(0.6f), 1100f);
            y += 32f;
            y = PriceRow(b, y, o, era, lc, fm);
            y = CostStory(b, y, o, era, lc, fm);
            y = SprintArm(b, y, o);
            if (era >= 2) y = WeightRow(b, y, o);
            // THE FLOOR UNLOCK, whenever a line still fits on the sheet. Every
            // pitch above was tightened so the densest offer the engine allows — 4
            // variable lines, 3 standing ones, the weight row — still leaves it
            // room; a founder does not lose the era's whole lesson for itemising
            // honestly.
            if (era >= 3 && y + 30f <= DeskKit.PaneH) y = MiniPnl(b, y, o, lc, fm);
        }

        /// THE PRICE ROW — the founder's one strategic dial, with the margin it makes.
        internal static float PriceRow(BinderScreen b, float y, Offer o, int era, double lc, double fm)
        {
            List<double> steps = PriceSteps(o);
            double cur = o.Price;
            string value = "$" + Money(cur) + " per unit";
            if (cur <= 0.0)
                value = o.PriceSet ? "$0 — free on purpose"
                                   : "unpriced — bills at $" + Money(o.FairPrice * fm);
            string why = "the going rate is $" + Money(o.FairPrice * fm) + " — you name what you charge";
            if (era >= 1)
                why = "contribution margin $" + Money(SimCatalog.Contribution(o, lc, fm))
                      + "/unit (price − variable cost)";
            return DeskKit.Stepper(b, y, new DeskKit.StepRow
            {
                Name = "price", Why = why, Value = value,
                Effect = cur > 0.0 ? Verdict(b.State, o, cur, fm) : "not on sale at a named price",
                XVal = DeskKit.XValue, Pitch = 68f,
                AtMin = DeskKit.AtMin(steps, cur), AtMax = DeskKit.AtMax(steps, cur),
                OnMinus = () => PriceStep(o, -1),
                OnPlus = () => PriceStep(o, 1),
            });
        }

        /// THE FINE PRINT (coworking+): what one sale costs, line by line, and what
        /// the week costs whether or not anything sells. The garage gets the same
        /// two totals on one stepper each — the LINES ARE STILL THE STORED TRUTH
        /// underneath, so nothing is lost when coworking reveals them (01 section 5).
        
        /// THE BLUE LINE DOES THE ARITHMETIC OUT LOUD — the patient accountant.
        /// THE COST STORY — READ-ONLY (owner: the player never dials a cost;
        /// the world set this service's costs, stated and explained; cutting
        /// them is a BUILD). The full page and the open row draw this block.
        internal static float CostStory(BinderScreen b, float y, Offer o, int era,
                                        double lc, double fm)
        {
            double fair = Math.Max(o.FairPrice, 1.0);
            b.L("what one sale costs — the world set these when the offer was written",
                DeskKit.XId, y, DeskKit.Detail, Ink(0.55f), 900f);
            y += 30f;
            if (era >= 1 && o.CostLines != null && o.CostLines.Count > 0)
            {
                for (int li = 0; li < o.CostLines.Count; li++)
                {
                    CostLine ld = o.CostLines[li];
                    b.L(string.Format(CultureInfo.InvariantCulture,
                        "{0} — ${1} ({2}% of the going rate)", ld.Label ?? "line",
                        Money(ld.Amount), (int)Math.Round(ld.Amount / fair * 100.0)),
                        40f, y, DeskKit.Detail, Ink(0.7f), 900f);
                    y += 26f;
                }
            }
            y = SumLine(b, y, string.Format(CultureInfo.InvariantCulture,
                "= serve cost ${0}/unit (served at ×{1:0.00} today) · standing tools ${2}/wk",
                Money(o.UnitCost), lc, Money(o.FixedWk)), DrawnUI.Blue);
            int be = SimCatalog.BreakEven(o, lc, fm);
            if (be < 0)
                y = SumLine(b, y, string.Format(CultureInfo.InvariantCulture,
                    "this price never pays for itself — every sale loses ${0}",
                    Money(-SimCatalog.Contribution(o, lc, fm))), DrawnUI.Coral);
            else
                y = SumLine(b, y, string.Format(CultureInfo.InvariantCulture,
                    "break-even: {0} sales/wk pay the standing costs", be), DrawnUI.Blue);
            b.L("costs only fall when the team rebuilds how this one is made — a cost sprint below",
                DeskKit.XId, y, DeskKit.Law, Ink(0.45f), 1080f);
            return y + 26f;
        }

        /// THE SPRINT ARM — the one road to a cheaper serve: a real roadmap bet
        /// the team builds (R&D capacity, the dice at ship). Two-tap.
        internal static float SprintArm(BinderScreen b, float y, Offer o)
        {
            string nm = o.Name ?? "";
            bool has = false;
            for (int i = 0; i < b.State.Bets.Count; i++)
            {
                Bet bd = b.State.Bets[i];
                if (bd.Kind == "cost_down" && (bd.Offer ?? "") == nm && !bd.Shipped)
                    has = true;
            }
            if (has)
            {
                b.L("a cost sprint for this offer is on the roadmap — the team is on it",
                    DeskKit.XId, y, DeskKit.Detail, Ink(0.55f), 900f);
                return y + 30f;
            }
            GameState stS = b.State;
            DeskKit.Arm(b, "sprint_" + nm, "start a cost sprint — the team rebuilds it",
                "3 R&D-weeks of the team — sure?", DeskKit.XId, y,
                () => SimRoadmap.AddCostDownBet(stS, nm), 560f, 22f);
            return y + 44f;
        }

        static float SumLine(BinderScreen b, float y, string text, Color col)
        {
            TextMeshProUGUI l = b.L(text, DeskKit.XId + 18f, y, 22f, col, 1080f);
            return y + Mathf.Max(BinderScreen.Height(l), 26f) + 6f;
        }

        /// One cost line on its own stepper. `variable` walks the fair-relative
        /// ladder, fixed walks absolute dollars; every press re-syncs, so the
        /// totals can never drift from the receipts that explain them.
        
        /// THE SHELF METER (office+): weight is share-of-wallet, and the wallet is
        /// finite. The bound prints its own reason, which IS the lesson.
        internal static float WeightRow(BinderScreen b, float y, Offer o)
        {
            GameState st = b.State;
            double cur = o.Weight;
            double others = SimCatalog.ShelfWeight(st) - cur;
            double ceiling = Gd.Minf(SimCatalog.MaxWeight, SimCatalog.ShelfWeightCap - others);
            return DeskKit.Stepper(b, y, new DeskKit.StepRow
            {
                Name = "shelf weight",
                Why = "the slice of a customer's wallet this one claims",
                Value = string.Format(CultureInfo.InvariantCulture, "{0:0.0}", cur),
                Effect = string.Format(CultureInfo.InvariantCulture, "shelf: ∑{0:0.0} of {1:0.0} used",
                    SimCatalog.ShelfWeight(st), SimCatalog.ShelfWeightCap),
                Bound = cur >= ceiling - 0.001 ? "(the shelf is full)" : "",
                XVal = DeskKit.XValue, Pitch = 66f,
                AtMin = DeskKit.AtMin(WeightSteps, cur), AtMax = cur >= ceiling - 0.001,
                OnMinus = () => o.Weight = Gd.Clampf(DeskKit.Ladder(WeightSteps, cur, -1),
                    SimCatalog.MinWeight, ceiling),
                OnPlus = () => o.Weight = Gd.Clampf(DeskKit.Ladder(WeightSteps, cur, 1),
                    SimCatalog.MinWeight, ceiling),
            });
        }

        /// THE OFFER'S OWN P&L (floor+): a product line reads like a small company.
        internal static float MiniPnl(BinderScreen b, float y, Offer o, double lc, double fm)
        {
            GameState st = b.State;
            double sales = st.Traction * o.Weight * SimEngine.OfferCadence(o.Unit);
            double inc = sales * SimEngine.OfferBilledPrice(o, fm);
            double variable = sales * SimCatalog.ServedUnitCost(o, lc);
            double fixedWk = o.FixedWk;
            string text = string.Format(CultureInfo.InvariantCulture,
                "this offer, a week at current volume: ≈{0} sales -> ${1} in − ${2} variable − ${3} fixed = ${4} contribution",
                Gd.RoundToInt(sales), Money(inc), Money(variable), Money(fixedWk),
                Money(inc - variable - fixedWk));
            TextMeshProUGUI l = b.L(text, DeskKit.XId, y, 22f, DrawnUI.Blue, 1100f);
            return y + Mathf.Max(BinderScreen.Height(l), 26f);
        }

        // ── 7.3  THE WRITE ─────────────────────────────────────────────────────

        /// THE OFFER FORM (owner: a real interface — name, full description,
        /// what it bundles, the unit wish, who it is for). Fields survive a
        /// rebuild in b.Desk under f_*; the street reads them as structured input.
        static readonly string[] UnitHints = { "let the street pick", "per session",
            "per month", "per order", "per unit", "per year", "per hour",
            "per package", "per kit" };

        static void FormField(BinderScreen b, float y, string key, string label,
                              string placeholder, float height, bool single)
        {
            b.L(label, DeskKit.XId, y, 18f, Ink(0.5f), 400f);
            TMP_InputField te = WriteField(b, DeskKit.XId, y + 22f, 1140f, height,
                placeholder, Get(b, key));
            te.fontAsset = null;   // keep WriteField's own font path
            if (single) te.lineType = TMP_InputField.LineType.SingleLine;
            DeskKit.Rule(b, y + 22f + height + 4f);
            string k = key;
            te.onValueChanged.AddListener(v => b.Desk[k] = v);
            if (key == "f_name") te.ActivateInputField();
        }

        static void Write(BinderScreen b)
        {
            // a journal-drafted offer lands here pre-filled, once
            if (Get(b, "f_desc").Length == 0
                && !string.IsNullOrEmpty(b.State.OfferDraft))
            {
                b.Desk["f_desc"] = b.State.OfferDraft;
                b.State.OfferDraft = "";
            }
            // THE BINDER'S OWN STATIONERY (owner sign-off: shape C) — a
            // pre-printed sheet, agnostic of the business.
            b.L("NEW OFFER — INTAKE SHEET", DeskKit.XId, 8f, 30f, DrawnUI.Ink, 700f);
            var stamp = b.L("RUNWAY! form 01-C", 920f, 12f, 16f, Ink(0.45f), 200f);
            stamp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 1.7f);
            DeskKit.PenRule(b, 48f);
            FormField(b, 56f, "f_name", "offer, called",
                "its working name — the street may tidy it", 34f, true);
            FormField(b, 128f, "f_desc", "in plain words, what it is",
                "write it the way you would say it out loud…", 96f, false);
            FormField(b, 262f, "f_includes", "a buyer walks away with",
                "the pieces: the work, the time, the materials, the follow-up…", 70f, false);
            FormField(b, 370f, "f_audience", "for (optional)",
                "which of your customers this is aimed at", 34f, true);
            // billed (circle one): every unit on one row, the chosen in the pen
            b.L("billed (circle one)", DeskKit.XId, 446f, 18f, Ink(0.5f), 400f);
            string chosen = Get(b, "f_unit");
            if (chosen.Length == 0) chosen = UnitHints[0];
            float ux = DeskKit.XId;
            float uy = 470f;
            foreach (string u in UnitHints)
            {
                float uw = DrawnUI.MeasureWidth(u, 20f) + 26f;
                if (ux + uw > 1130f) { ux = DeskKit.XId; uy += 34f; }
                string uv = u;
                DeskKit.Word(b, uv, ux, uy, () => b.Desk["f_unit"] = uv, 20f,
                    uv == chosen ? DrawnUI.Coral : Ink(0.55f), uw);
                if (uv == chosen) DeskKit.PenRule(b, uy + 26f, ux, uw - 20f);
                ux += uw + 10f;
            }
            float by = uy + 44f;
            DeskKit.Word(b, "send it to the street", DeskKit.XId, by, () => Submit(b),
                DeskKit.Row, DrawnUI.Coral, 340f);
            DeskKit.Word(b, "never mind", 380f, by, () =>
            {
                b.Desk["mode"] = "";
                ClearSheet(b);
            }, DeskKit.Row, Ink(0.7f), 200f);
            if (Get(b, "short").Length > 0)
                b.L("a few words of description at least — the street can't price a shrug",
                    DeskKit.XId, by + 46f, DeskKit.Status, DrawnUI.Coral, 900f);
            DeskKit.Footer(b, "",
                "the street writes the terms — costs are the world's; the price stays yours", "");
        }

        static void ClearSheet(BinderScreen b)
        {
            foreach (string k in new[] { "text", "f_name", "f_desc", "f_includes",
                "f_audience", "f_unit", "fields", "oq", "oa" }) b.Desk.Remove(k);
        }

        /// THE STREET'S FOLLOW-UP (owner: generated clarification, multiple
        /// choice): each question offers its options as words; every answer
        /// inked = pricing fires with the q/a pairs attached.
        static void ClarifyState(BinderScreen b)
        {
            var oq = b.Desk.ContainsKey("oq") ? b.Desk["oq"] as JArray : null;
            var oa = b.Desk.ContainsKey("oa") ? b.Desk["oa"] as Dictionary<string, string> : null;
            if (oq == null) { b.Desk["mode"] = "write"; Write(b); return; }
            if (oa == null) { oa = new Dictionary<string, string>(); b.Desk["oa"] = oa; }
            b.L("THE STREET HAS QUESTIONS", DeskKit.XId, 8f, 30f, DrawnUI.Ink, 700f);
            b.L("\"" + Get(b, "f_name") + "\" — " + Gd.Left(Get(b, "f_desc"), 90),
                DeskKit.XId, 52f, 20f, Ink(0.55f), 1100f);
            DeskKit.PenRule(b, 84f);
            float y = 100f;
            for (int i = 0; i < oq.Count; i++)
            {
                var qd = oq[i] as JObject;
                string q = qd != null ? (string)qd["q"] ?? "" : "";
                b.L(q, DeskKit.XId, y, 24f, DrawnUI.Ink, 1100f);
                y += 36f;
                string picked;
                oa.TryGetValue(i.ToString(CultureInfo.InvariantCulture), out picked);
                float ox = DeskKit.XId + 20f;
                var opts = qd != null ? qd["options"] as JArray : null;
                if (opts != null)
                    foreach (var ov in opts)
                    {
                        string o = (string)ov ?? "";
                        float ow = DrawnUI.MeasureWidth(o, 22f) + 28f;
                        if (ox + ow > 1130f) { ox = DeskKit.XId + 20f; y += 36f; }
                        int qi = i;
                        string oval = o;
                        DeskKit.Word(b, oval, ox, y, () =>
                        {
                            var oa2 = b.Desk["oa"] as Dictionary<string, string>;
                            if (oa2 != null)
                                oa2[qi.ToString(CultureInfo.InvariantCulture)] = oval;
                        }, 22f, oval == picked ? DrawnUI.Coral : Ink(0.6f), ow);
                        if (oval == picked) DeskKit.PenRule(b, y + 28f, ox, ow - 20f);
                        ox += ow + 10f;
                    }
                y += 48f;
            }
            bool allIn = oa.Count >= oq.Count && oq.Count > 0;
            if (allIn)
                DeskKit.Word(b, "that's everything — price it", DeskKit.XId, y + 6f,
                    () => FirePrice(b), DeskKit.Row, DrawnUI.Coral, 420f);
            else
                b.L("answer each — then the street prices it", DeskKit.XId, y + 6f,
                    DeskKit.Detail, Ink(0.45f), 600f);
            DeskKit.Word(b, "never mind", 500f, y + 6f, () => b.Desk["mode"] = "write",
                DeskKit.Row, Ink(0.7f), 200f);
        }

        /// THE PAPER IS THE FIELD: no box, no fill, no chrome — the rule underneath
        /// is the only thing that says "write here". Built the way
        /// PageBlocks.WriteField builds one, minus the journal's page geometry;
        /// the binder is square, so an axis-aligned mask is safe here.
        static TMP_InputField WriteField(BinderScreen b, float x, float y, float w, float h,
                                         string placeholder, string startText)
        {
            var fieldGo = new GameObject("write", typeof(RectTransform));
            fieldGo.SetActive(false);          // configure before TMP_InputField wakes
            var frt = fieldGo.GetComponent<RectTransform>();
            frt.SetParent(b.Content, false);
            frt.anchorMin = new Vector2(0f, 1f);
            frt.anchorMax = new Vector2(0f, 1f);
            frt.pivot = new Vector2(0f, 1f);
            frt.sizeDelta = new Vector2(w, h);
            frt.anchoredPosition = new Vector2(x, -y);

            var hit = fieldGo.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            RectTransform viewport = DrawnUI.FullRect(frt, "viewport");
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform textRt = DrawnUI.FullRect(viewport, "text");
            var text = textRt.gameObject.AddComponent<TextMeshProUGUI>();
            if (DrawnUI.Hand != null) text.font = DrawnUI.Hand;
            text.fontSize = 28f;
            text.color = DrawnUI.Ink;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.richText = false;

            RectTransform phRt = DrawnUI.FullRect(viewport, "placeholder");
            var ph = phRt.gameObject.AddComponent<TextMeshProUGUI>();
            if (DrawnUI.Hand != null) ph.font = DrawnUI.Hand;
            ph.fontSize = 28f;
            ph.color = Ink(0.30f);
            ph.alignment = TextAlignmentOptions.TopLeft;
            ph.textWrappingMode = TextWrappingModes.Normal;
            ph.richText = false;
            ph.text = placeholder ?? "";

            var input = fieldGo.AddComponent<TMP_InputField>();
            input.transition = Selectable.Transition.None;
            input.targetGraphic = hit;
            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = ph;
            input.lineType = TMP_InputField.LineType.MultiLineSubmit;
            input.richText = false;
            input.restoreOriginalTextOnEscape = false;
            PaperInput.Editable(input, DrawnUI.Pen);
            fieldGo.SetActive(true);
            input.text = startText ?? "";
            return input;
        }

        /// The one road out of WRITE. Keyed: the street prices it and the reply
        /// lands on the review card. Keyless: the house numbers arrive instantly
        /// and the card is identical, with one dry footnote (01 section 8.4).
        static void Submit(BinderScreen b)
        {
            string desc = Get(b, "f_desc").Trim();
            if (desc.Length == 0) desc = Get(b, "text").Trim();
            if (desc.Length < 3)
            {
                b.Desk["short"] = "1";
                b.Refresh();     // Enter has no rebuild of its own; the answer must still land
                return;
            }
            b.Desk.Remove("short");
            string hint = Get(b, "f_unit");
            var fields = new JObject
            {
                ["name"] = Gd.Left(Get(b, "f_name").Trim(), 40),
                ["description"] = Gd.Left(desc, 500),
                ["includes"] = Gd.Left(Get(b, "f_includes").Trim(), 300),
                ["unit_hint"] = hint.StartsWith("let the street") ? "" : hint,
                ["audience_note"] = Gd.Left(Get(b, "f_audience").Trim(), 120),
            };
            b.Desk["text"] = desc;   // the wait card + the house fallback read one line
            b.Desk["fields"] = fields;
            EventGenerator gen = Street(b);
            if (gen != null)
            {
                // UNDERSTAND FIRST, PRICE SECOND: the street may ask up to 3
                // multiple-choice questions before it writes the terms
                b.Desk["mode"] = "wait";
                gen.ClarifyOfferIntake(OfferPayload(b.State, fields), cres =>
                {
                    if (b == null || b.gameObject == null) return;
                    if (Get(b, "mode") != "wait") return;
                    var qs = cres != null ? cres["questions"] as JArray : null;
                    bool ready = cres == null || cres["ready"] == null || (bool)cres["ready"];
                    if (ready || qs == null || qs.Count == 0) { FirePrice(b); return; }
                    b.Desk["oq"] = qs;
                    b.Desk["oa"] = new Dictionary<string, string>();
                    b.Desk["mode"] = "clarify";
                    b.Refresh();
                });
                b.Refresh();
                return;
            }
            b.Desk["pending"] = Proposal(b.State, SimCatalog.DraftTerms(b.State, desc));
            b.Desk["house"] = "1";
            b.Desk["mode"] = "review";
            b.Refresh();
        }

        /// What the street is told, byte-synced with the Godot payload
        /// (event_generator.gd price_offer_idea): the company, its stage, and the
        /// founder's own words. `era` is in there because stage-scaled tooling
        /// comes from the PROPOSAL, never from a runtime multiplier (01 D2).
        static JObject OfferPayload(GameState st, object ideaOrFields)
        {
            var company = new JObject
            {
                ["name"] = st.CompanyName ?? "",
                ["idea"] = st.CompanyIdea ?? "",
                ["what"] = st.BizWhat ?? "",
                ["who"] = st.BizWho ?? "",
                ["era"] = st.Era ?? "",
            };
            var fj = ideaOrFields as JObject;
            JObject no = fj ?? new JObject
            {
                ["name"] = "",
                ["description"] = Gd.Left((ideaOrFields as string) ?? "", 500),
                ["includes"] = "", ["unit_hint"] = "", ["audience_note"] = "",
            };
            return new JObject
            {
                ["company"] = company,
                ["new_offer"] = no,
            };
        }

        /// THE REPLY COMES BACK. CANCEL IS REAL, so this is the gate rather than
        /// the hand-off: the desk must still be alive AND still be the desk that
        /// asked, or the terms land on the floor. A binder closed mid-flight took
        /// its desk state with it, and no offer ever appears unreviewed.
        ///
        /// An empty answer is not a failure state, only a quieter one: the house
        /// numbers fill the same card, with the one dry footnote (01 section 8.4).
        /// The priced call, with any clarify answers attached as binding facts.
        static void FirePrice(BinderScreen b)
        {
            var fields = b.Desk.ContainsKey("fields") ? b.Desk["fields"] as JObject : new JObject();
            var oq = b.Desk.ContainsKey("oq") ? b.Desk["oq"] as JArray : null;
            var oa = b.Desk.ContainsKey("oa") ? b.Desk["oa"] as Dictionary<string, string> : null;
            if (oq != null && oq.Count > 0 && fields != null)
            {
                var clar = new JArray();
                for (int i = 0; i < oq.Count; i++)
                {
                    var qd = oq[i] as JObject;
                    string a;
                    if (oa == null || !oa.TryGetValue(i.ToString(CultureInfo.InvariantCulture), out a))
                        a = "";
                    clar.Add(new JObject { ["q"] = qd != null ? (string)qd["q"] ?? "" : "",
                                           ["a"] = a });
                }
                fields["clarifications"] = clar;
            }
            string desc = Get(b, "text");
            b.Desk["mode"] = "wait";
            EventGenerator gen = Street(b);
            if (gen == null)
            {
                b.Desk["pending"] = Proposal(b.State, SimCatalog.DraftTerms(b.State, desc));
                b.Desk["house"] = "1";
                b.Desk["mode"] = "review";
                b.Refresh();
                return;
            }
            gen.PriceOfferIdea(OfferPayload(b.State, fields), res => Land(b, desc, res));
            b.Refresh();
        }

        static void Land(BinderScreen b, string idea, JObject res)
        {
            if (b == null || b.gameObject == null) return;
            if (Get(b, "mode") != "wait") return;
            bool house = res == null || res.Count == 0;
            b.Desk["pending"] = Proposal(b.State,
                house ? SimCatalog.DraftTerms(b.State, idea) : TermsFromJson(res));
            if (house) b.Desk["house"] = "1"; else b.Desk.Remove("house");
            b.Desk["mode"] = "review";
            b.Refresh();
        }

        /// The street's answer, read into the shape the desk already speaks. The
        /// schema forces the unit enum and every numeric range; everything else
        /// dies in the clamps behind the review card's confirm.
        static Offer TermsFromJson(JObject res)
        {
            var o = new Offer
            {
                Name = (string)res["name"] ?? "an offer",
                Unit = (string)res["unit"] ?? "per order",
                FairPrice = res["fair_price"] != null ? (double)res["fair_price"] : 1.0,
                Elasticity = res["elasticity"] != null ? (double)res["elasticity"] : 2.0,
                Weight = res["weight"] != null ? (double)res["weight"] : 1.0,
                Desc = (string)res["desc"] ?? "",
                StreetRead = (string)res["street_read"] ?? "",
                CapacityPerUnit = res["capacity_per_unit"] != null
                    ? (double)res["capacity_per_unit"] : 1.0,
                Price = 0.0,
            };
            o.CostLines = LinesFromJson(res["variable_costs"] as JArray);
            o.FixedLines = LinesFromJson(res["fixed_costs_wk"] as JArray);
            return o;
        }

        static List<CostLine> LinesFromJson(JArray arr)
        {
            var outp = new List<CostLine>();
            if (arr == null) return outp;
            foreach (JToken t in arr)
            {
                var jo = t as JObject;
                if (jo == null) continue;
                outp.Add(new CostLine
                {
                    Label = (string)jo["label"] ?? "",
                    Amount = jo["amount"] != null ? (double)jo["amount"] : 0.0,
                });
            }
            return outp;
        }

        // ── 7.4  THE WAIT ──────────────────────────────────────────────────────

        /// One breathing line and a cancel word — no spinner, no dots, no progress
        /// bar, and the subject is always the fiction (10-interface-language 2.12).
        ///
        /// CANCEL IS REAL: leaving drops the reply on arrival. Every callback
        /// guards the object's liveness AND the mode before it touches anything,
        /// so a proposal nobody is waiting for lands on the floor, not the shelf.
        static void WaitState(BinderScreen b)
        {
            DeskKit.Title(b, "pricing — what " + b.State.CompanyName + " sells");
            DeskKit.Wait(b, DeskKit.XId, 96f, "the street is pricing it…", () =>
            {
                b.Desk["mode"] = "";
                b.Desk.Remove("text");
            });
            b.L("\"" + Get(b, "text") + "\"", DeskKit.XId, 200f, DeskKit.Detail,
                Ink(0.45f), 1100f);
        }

        /// THE STREET'S OWN VOICE (01 section 8, L1) — one request_json on the
        /// "clarify" tier, EventGenerator.PriceOfferIdea, which maps plain words to
        /// plausible market terms and concrete cost labels in this business's own
        /// vocabulary. That is the thing a model does well and a lookup table
        /// cannot; every number it returns still dies in AddOffer's clamps, and the
        /// founder still signs the card.
        ///
        /// Null when there is no key, no generator, or the model is down: the road
        /// simply is not there, and WRITE goes straight to REVIEW on house numbers.
        /// Keyless is not a degraded screen — it is the same desk with a dry
        /// footnote (01 section 8.4).
        static EventGenerator Street(BinderScreen b)
        {
            Boot boot = Boot.Instance;
            if (boot == null || boot.Generator == null) return null;
            if (boot.Generator.Llm == null || !boot.Generator.Llm.Enabled) return null;
            return boot.Generator;
        }

        // ── 7.5  THE REVIEW ────────────────────────────────────────────────────

        /// A PROPOSAL AWAITING THE FOUNDER'S PEN. Same renderer language as DETAIL,
        /// three changes: the coral banner replaces the way back, the price row
        /// becomes a note (one decision at a time — the bang walks the founder back
        /// here to price it), and the bottom carries the two words that end the
        /// state.
        ///
        /// THE LINES ARE THE ONLY ADJUSTABLE THING. Name, unit, fair price,
        /// elasticity and weight are the STREET'S read, not the founder's lever —
        /// arguing with the market about what it pays is not a game mechanic, and
        /// the founder's real levers are price (later) and costs (now).
        static void ReviewState(BinderScreen b)
        {
            GameState st = b.State;
            Offer p = Pending(b);
            if (p == null)
            {
                b.Desk["mode"] = "";
                List(b);
                return;
            }
            double lc = SimEngine.LearningCurve(st);
            // NO WAR MULTIPLIER HERE, on purpose. This card is the street's
            // DURABLE terms — what this audience pays for this kind of thing — and
            // a rival's price cut is a condition on what it bills THIS WEEK, not on
            // what the market is worth. The founder meets that number on the list
            // and the detail card, where the price decision actually happens;
            // folding it into the quote would teach that a transient status changed
            // the market itself.
            double fair = Gd.Maxf(p.FairPrice, 1.0);
            int era = st.EraIndex();
            var groups = new List<DeskKit.ReviewGroup>();
            if (era < 1)
            {
                double vcur = p.UnitCost;
                groups.Add(new DeskKit.ReviewGroup
                {
                    Caption = "what one sale costs to serve",
                    Lines = new List<DeskKit.StepRow>
                    {
                        ReviewTotal(p, "serve cost", "$" + Money(vcur) + "/unit",
                            string.Format(CultureInfo.InvariantCulture, "{0}% of fair",
                                Gd.RoundToInt(vcur / fair * 100.0)),
                            VarSteps(fair), vcur, true),
                    },
                    Sum = string.Format(CultureInfo.InvariantCulture,
                        "= variable cost ${0}/unit · served at ×{1:0.00} today", Money(vcur), lc),
                });
                double fcur = p.FixedWk;
                groups.Add(new DeskKit.ReviewGroup
                {
                    Caption = "standing costs — every week, sold or not",
                    Lines = new List<DeskKit.StepRow>
                    {
                        ReviewTotal(p, "tools", "$" + Money(fcur) + "/wk",
                            "billed sold or not", FixedSteps, fcur, false),
                    },
                    Sum = ReviewFixedSum(p, lc),
                });
            }
            else
            {
                groups.Add(new DeskKit.ReviewGroup
                {
                    Caption = "what one sale costs — variable",
                    Lines = ReviewLines(p, p.CostLines, fair, lc, true),
                    Sum = string.Format(CultureInfo.InvariantCulture,
                        "= variable cost ${0}/unit · served at ×{1:0.00} today",
                        Money(p.UnitCost), lc),
                });
                groups.Add(new DeskKit.ReviewGroup
                {
                    Caption = "standing costs — every week, sold or not",
                    Lines = ReviewLines(p, p.FixedLines, fair, lc, false),
                    Sum = ReviewFixedSum(p, lc),
                });
            }
            DeskKit.Review(b, new DeskKit.ReviewCard
            {
                Banner = "THE INTAKE SHEET — the street's terms are in",
                Read = ReviewRead(b, p, fair),
                Groups = groups,
                Verdict = SimCatalog.BreakEven(p, lc) >= 0 ? ""
                    : "this price never pays for itself — every sale loses $"
                      + Money(-SimCatalog.Contribution(p, lc)),
                Note = Get(b, "house").Length > 0 ? DeskKit.HouseNote : "",
                Refused = Get(b, "refused"),
                Confirm = "put it on the shelf", Cancel = "tear it up",
                OnConfirm = () => Shelve(b, p),
                OnCancel = () =>
                {
                    b.Desk["mode"] = "";
                    ClearSheet(b);
                },
            });
        }

        /// THE ONLY CALL TO AddOffer ON THIS DESK. A raced cap comes back as a
        /// printed reason in the desk's own voice, never a silently-dead button.
        static void Shelve(BinderScreen b, Offer p)
        {
            // the reason is read AT THE PRESS, not at the render: a week can pass
            // between the two, and a stale explanation is worse than none
            string shut = SimCatalog.ShelfFullLine(b.State);
            Offer made = SimCatalog.AddOffer(b.State, p.Name, p.Unit, p.FairPrice,
                p.UnitCost, p.Elasticity, p.Weight, p.CostLines, p.FixedLines);
            if (made == null)
            {
                b.Desk["refused"] = shut.Length > 0
                    ? shut : "the shelf refused it — drop something first";
                return;
            }
            b.State.LogAction(string.Format(CultureInfo.InvariantCulture,
                "NEW OFFER shelved: {0} ({1}) — street ${2}",
                made.Name, made.Unit, Gd.RoundToInt(made.FairPrice)));
            b.Desk["mode"] = "";
            ClearSheet(b);
        }

        static List<DeskKit.StepRow> ReviewLines(Offer p, List<CostLine> lines,
                                                 double fair, double lc, bool variable)
        {
            var outp = new List<DeskKit.StepRow>();
            if (lines == null) return outp;
            foreach (CostLine cl in lines)
            {
                CostLine ld = cl;
                List<double> steps = variable ? VarSteps(fair) : FixedSteps;
                double amount = ld.Amount;
                string effect = string.Format(CultureInfo.InvariantCulture, "{0}% of fair",
                    Gd.RoundToInt(amount / fair * 100.0));
                if (!variable)
                {
                    double margin = SimCatalog.Contribution(p, lc);
                    effect = margin > 0.0
                        ? string.Format(CultureInfo.InvariantCulture, "{0} sales/wk pays it",
                            (int)Math.Ceiling(amount / margin))
                        : "no margin to pay it";
                }
                // READ-ONLY (owner: the world sets an offer's costs — a founder
                // who could dial them would dial them to zero)
                outp.Add(new DeskKit.StepRow
                {
                    Name = ld.Label ?? "line",
                    Value = variable ? "$" + Money(amount) : "$" + Money(amount) + "/wk",
                    Effect = effect, Pitch = 34f, Static = true,
                });
            }
            return outp;
        }

        /// GARAGE TOTALS MODE (01 section 5): one stepper for the whole variable
        /// sheet, one for the whole standing sheet. The lines behind them are
        /// scaled proportionally and kept, so nothing the street itemised is lost
        /// when coworking reveals it.
        static DeskKit.StepRow ReviewTotal(Offer p, string nm, string value, string effect,
                                           List<double> steps, double cur, bool variable)
        {
            // READ-ONLY (owner: the world's costs, stated — never a founder's dial)
            return new DeskKit.StepRow
            {
                Name = nm, Value = value, Effect = effect, Pitch = 34f, Static = true,
            };
        }

        
        static string ReviewFixedSum(Offer p, double lc)
        {
            int be = SimCatalog.BreakEven(p, lc);
            if (be < 0)
                return "= $" + Money(p.FixedWk) + "/wk · nothing here pays for it yet";
            return string.Format(CultureInfo.InvariantCulture,
                "= ${0}/wk · break-even: {1} sales/wk pay for it", Money(p.FixedWk), be);
        }

        /// The review's read block: identity, the world's own line, its visible
        /// reasoning, the labor reality, then the unpriced law.
        static List<string> ReviewRead(BinderScreen b, Offer p, double fair)
        {
            var read = new List<string>();
            // YOUR SIDE, condensed — the sheet's words stay visible above the terms
            string yours = Gd.Left(Get(b, "f_desc"), 110);
            if (yours.Length > 0) read.Add("you wrote: " + yours);
            var oqR = b.Desk.ContainsKey("oq") ? b.Desk["oq"] as JArray : null;
            var oaR = b.Desk.ContainsKey("oa") ? b.Desk["oa"] as Dictionary<string, string> : null;
            if (oqR != null && oaR != null)
                for (int i = 0; i < oqR.Count; i++)
                {
                    string a;
                    if (oaR.TryGetValue(i.ToString(CultureInfo.InvariantCulture), out a)
                        && a.Length > 0)
                    {
                        var qd = oqR[i] as JObject;
                        read.Add("— " + (qd != null ? (string)qd["q"] ?? "" : "")
                            + "  ->  " + a);
                    }
                }
            read.Add(string.Format(CultureInfo.InvariantCulture,
                "{0} · {1} — the street charges ≈ ${2} · elasticity {3} · weight {4:0.0}",
                (p.Name ?? "an offer").ToUpper(), p.Unit ?? "", Money(fair),
                ElasticityWord(p.Elasticity), p.Weight));
            if (!string.IsNullOrEmpty(p.Desc)) read.Add(p.Desc);
            if (!string.IsNullOrEmpty(p.StreetRead))
                read.Add("the street's read: " + p.StreetRead);
            if (b.State.BizWhat == "Service" && p.CapacityPerUnit > 0.0)
            {
                double slots = SimWorks.ServiceCapacity(b.State);
                if (slots > 0.0)
                    read.Add(string.Format(CultureInfo.InvariantCulture,
                        "one {0} takes ≈ {1:0.0} hours of hands — today's crew serves ≈ {2}/wk before hiring",
                        (p.Unit ?? "unit").StartsWith("per ") ? p.Unit.Substring(4) : p.Unit,
                        p.CapacityPerUnit, (int)(slots / p.CapacityPerUnit)));
            }
            read.Add("arrives unpriced — it bills at the going rate ≈ $" + Money(fair)
                + " until you price it");
            return read;
        }

        /// Elasticity in words: how hard demand punishes a price above the going
        /// rate. A raw engine float never prints (10-interface-language 3.8).
        static string ElasticityWord(double e)
        {
            if (e >= 2.4) return "steep";
            if (e >= 1.5) return "typical";
            return "gentle";
        }

        /// The proposal, in the shape the whole desk already reads. The street
        /// answers in the LLM's own vocabulary and the keyless draft answers in
        /// exactly the same one, so there is ONE road in and one shape afterwards —
        /// an offer, synced, priced at nothing.
        static Offer Proposal(GameState st, Offer terms)
        {
            var p = new Offer
            {
                Name = (terms.Name ?? "an offer").Length > 40
                    ? terms.Name.Substring(0, 40) : (terms.Name ?? "an offer"),
                Unit = string.IsNullOrEmpty(terms.Unit) ? "per order" : terms.Unit,
                FairPrice = Gd.Clampf(terms.FairPrice, 1.0, 50000.0),
                Elasticity = Gd.Clampf(terms.Elasticity, 0.5, 3.0),
                Weight = Gd.Clampf(terms.Weight, SimCatalog.MinWeight,
                    Gd.Minf(SimCatalog.MaxWeight,
                        Gd.Maxf(SimCatalog.WeightRoom(st), SimCatalog.MinWeight))),
                UnitCost = terms.UnitCost,
                Desc = Gd.Left(terms.Desc ?? "", 110),
                StreetRead = Gd.Left(terms.StreetRead ?? "", 140),
                CapacityPerUnit = Gd.Clampf(terms.CapacityPerUnit, 0.1, 40.0),
                Price = 0.0,
            };
            List<CostLine> cl = SimCatalog.SanitizeLines(terms.CostLines, SimCatalog.MaxCostLines);
            // A MODEL THAT ANSWERED WITH A LUMP SUM still gets a receipt: one
            // honest line beats a total nobody can argue with.
            if (cl.Count == 0 && p.UnitCost > 0.0)
                cl.Add(new CostLine { Label = "cost to serve", Amount = p.UnitCost });
            if (cl.Count > 0) p.CostLines = cl;
            List<CostLine> fl = SimCatalog.SanitizeLines(terms.FixedLines, SimCatalog.MaxFixedLines);
            if (fl.Count > 0) p.FixedLines = fl;
            SimEngine.SyncOfferCosts(p);
            return p;
        }

        // ── ladders and presses ────────────────────────────────────────────────

        /// The price ladder: off sale, then the fair-price multiples. Duplicates
        /// are dropped so no press is ever a dead press on a cheap offer.
        public static List<double> PriceSteps(Offer o)
        {
            double fair = Gd.Maxf(o.FairPrice, 1.0);
            var steps = new List<double> { 0.0 };
            for (int i = 0; i < PriceMults.Length; i++)
                steps.Add(Gd.Maxf(Gd.Round(fair * PriceMults[i]), 1.0));
            return Dedupe(steps);
        }

        /// The variable-line ladder, relative to what the street charges — a cost
        /// line is only ever meaningful as a share of the price it eats.
        public static List<double> VarSteps(double fair)
        {
            var steps = new List<double>();
            for (int i = 0; i < VarMults.Length; i++)
                steps.Add(Gd.Round(Gd.Maxf(fair, 1.0) * VarMults[i]));
            return Dedupe(steps);
        }

        static List<double> Dedupe(List<double> a)
        {
            var outp = new List<double>();
            for (int i = 0; i < a.Count; i++)
                if (outp.Count == 0 || Math.Abs(outp[outp.Count - 1] - a[i]) > 0.001)
                    outp.Add(a[i]);
            return outp;
        }

        /// <summary>price steps: the founder's choice, $0 included (a conscious giveaway).</summary>
        public static void PriceStep(Offer o, int dir)
        {
            o.Price = DeskKit.Ladder(PriceSteps(o), o.Price, dir);
            o.PriceSet = true;
        }

        /// Step the VARIABLE TOTAL and let the lines follow proportionally (garage
        /// totals mode). A sheet with no lines yet gets the total as its one line,
        /// so the next era still has receipts to reveal.
        public static void ScaleVariable(Offer o, int dir)
        {
            double fair = Gd.Maxf(o.FairPrice, 1.0);
            double cur = o.UnitCost;
            double target = DeskKit.Ladder(VarSteps(fair), cur, dir);
            if (o.CostLines == null || o.CostLines.Count == 0)
                o.CostLines = new List<CostLine>
                    { new CostLine { Label = "cost to serve", Amount = target } };
            else if (cur <= 0.001)
            {
                double each = target / o.CostLines.Count;
                foreach (CostLine l in o.CostLines) l.Amount = each;
            }
            else
            {
                double k = target / cur;
                foreach (CostLine l in o.CostLines) l.Amount = l.Amount * k;
            }
            SimEngine.SyncOfferCosts(o);
        }

        /// The same move on the standing sheet.
        public static void ScaleFixed(Offer o, int dir)
        {
            double cur = o.FixedWk;
            double target = DeskKit.Ladder(FixedSteps, cur, dir);
            if (o.FixedLines == null || o.FixedLines.Count == 0)
                o.FixedLines = new List<CostLine>
                    { new CostLine { Label = "tools & subscriptions", Amount = target } };
            else if (cur <= 0.001)
            {
                double each = target / o.FixedLines.Count;
                foreach (CostLine l in o.FixedLines) l.Amount = each;
            }
            else
            {
                double k = target / cur;
                foreach (CostLine l in o.FixedLines) l.Amount = l.Amount * k;
            }
            SimEngine.SyncOfferCosts(o);
        }

        /// The row-inline stepper glyph. The kit's own glyph belongs to its 78px
        /// stepper row; a 62px list row cannot host one, so the two live side by
        /// side — same 52x46 target, same dim-at-bound law, same coral hover.
        static void StepBtn(BinderScreen b, string text, float x, float y, bool dead,
                            Action onPress)
        {
            if (dead)
            {
                // the dead glyph sits where the live one sat — a Button centres its
                // word in the box, so the label has to as well or the row limps
                DrawnUI.HandLabel(b.Content, text, x, y + 2f, 40f, Ink(0.35f),
                                  DeskKit.BtnW, TextAlignmentOptions.Center);
                return;
            }
            GameUi.InkWord(b.Content, text, x, y, DeskKit.BtnW, DeskKit.BtnH, 40f,
                DrawnUI.Ink, () =>
                {
                    b.Desk.Remove("armed");   // any other control disarms the armed one
                    onPress();
                    b.Refresh();
                });
        }
    }
}
