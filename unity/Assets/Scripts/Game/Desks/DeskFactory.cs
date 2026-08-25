using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — THE BENCH, the hardware strip on the binder's `product` tab.
    /// Spec: docs/design/09-hardware.md 11 + INTERFACE DELTA · language:
    /// docs/design/10-interface-language.md 5.8 · band: docs/design/00-spine.md 11.
    ///
    /// BinderScreen dispatches the product tab to DeskProduct, which calls
    /// DrawBench(b) on Hardware runs and passes ITSELF, so this file draws
    /// through the binder's own hand and never reaches into the sheet. On every
    /// other run nothing here runs and the tab is what it was.
    ///
    /// THE BAND IS RULED, NOT INVENTED: 07-roadmap owns the tab above; the spine
    /// gave the bench y470-740 and 07 yields its footer line to make room. Every
    /// row below is measured into that band, and nothing of ours renders outside it.
    ///
    /// The strip is a working vocabulary lesson, not a dashboard: capacity
    /// UTILIZATION, CARRYING COST, the LEARNING CURVE, MAKE VS BUY and the FILL
    /// RATE are printed by name on the run's own numbers, in the same clause as
    /// the number's cause. The engine owns every figure — the desk reads, and
    /// every write goes back through a SimFactory clamp.
    ///
    /// TWIN LAW: this file and game/src/ui/desks/desk_factory.gd draw the same
    /// rows at the same coordinates.
    /// </summary>
    public static class DeskFactory
    {
        // ── THE BAND (docs/design/00-spine.md 11: y470-740, header + 6 rows) ──
        const float YRule = 470f;     // the band's top edge — the bench is one object
        const float YHead = 484f;     // what we build, and what one costs
        const float YStatus = 518f;   // row 1: stock · capacity · utilization · demand
        const float YBuild = 546f;    // row 2: the week's build order (stepper + AUTO)
        const float YMach = 594f;     // row 3: the fleet, and this week's casualty
        const float YBuy = 624f;      // row 4: buy a machine · sell one back
        const float YMvb = 670f;      // row 5: make vs buy, and what it would have saved
        const float YCarry = 716f;    // row 6: what the shelf costs to hold

        const float XAuto = 120f;     // the AUTO word sits in the stepper name's own gutter
        const float WAuto = 300f;
        const float XBuy2 = 560f;     // the next rung of the ladder, dimmed with its reason
        const float XSell = 870f;

        static Color Ink(float a) { return DrawnUI.WithAlpha(DrawnUI.Ink, a); }

        /// <summary>Draw THE BENCH strip: stock, capacity, machines, produce steppers.</summary>
        public static void Draw(BinderScreen b)
        {
            DrawBench(b);
        }

        /// <summary>Drawn INSIDE the product desk on Hardware runs.</summary>
        public static void DrawBench(BinderScreen b)
        {
            GameState st = b.State;
            if (!SimFactory.Active(st)) return;
            HardwareState hw = SimFactory.HwView(st);
            Dictionary<string, object> w = SimFactory.WeekBlock(st);
            bool fresh = w.Count > 0;
            DeskKit.Rule(b, YRule);
            Head(b, st);
            Status(b, st, hw, w, fresh);
            Build(b, st, hw);
            Machines(b, st, hw, w);
            Buy(b, st, hw);
            MakeVsBuy(b, st, hw, w, fresh);
            Carrying(b, st, hw);
        }

        // ── header: what the bench builds, and what one unit costs to build ───
        static void Head(BinderScreen b, GameState st)
        {
            double bas = SimFactory.UnitCost(st);
            double eff = bas * SimFactory.Learning(st);
            int pct = SimFactory.LearningPct(st);
            string name = SimFactory.FlagshipName(st);
            if (name.Length > 28) name = name.Substring(0, 28);
            string head = string.Format(CultureInfo.InvariantCulture,
                "THE BENCH — building: {0} · ${1}/unit", name, Money2(eff));
            if (pct > 0)
            {
                // the learning curve, named where the price of a unit is first shown
                head += string.Format(CultureInfo.InvariantCulture,
                    " (base ${0}, learning curve −{1}%)", Money2(bas), pct);
            }
            b.L(head, DeskKit.XId, YHead, DeskKit.Status, DrawnUI.Ink, 1120f);
        }

        // ── row 1: the four numbers every production decision needs ───────────
        /// <summary>
        /// UTILIZATION IS NAMED so idle capacity has a word: it reddens below
        /// half (you are paying upkeep on machines that did nothing) and at the
        /// ceiling (the next unit of demand has nowhere to go).
        /// </summary>
        static void Status(BinderScreen b, GameState st, HardwareState hw,
                           Dictionary<string, object> w, bool fresh)
        {
            double cap = SimFactory.Capacity(st);
            float x = DeskKit.XId;
            string seg = string.Format(CultureInfo.InvariantCulture,
                "stock: {0} units · capacity: {1}/wk · ", hw.Stock, Gd.ToInt(cap));
            b.L(seg, x, YStatus, DeskKit.Detail, Ink(0.85f), 700f);
            x += DrawnUI.MeasureWidth(seg, DeskKit.Detail);
            string utilTxt = "utilization: —";
            Color utilCol = Ink(0.5f);
            if (fresh)
            {
                int util = Gd.RoundToInt(D(w, "utilization", 0.0) * 100.0);
                utilTxt = "utilization: " + util + "%";
                utilCol = (util < 50 || util >= 100) ? DrawnUI.Coral : Ink(0.85f);
            }
            b.L(utilTxt, x, YStatus, DeskKit.Detail, utilCol, 320f);
            x += DrawnUI.MeasureWidth(utilTxt, DeskKit.Detail);
            // the forecast AUTO orders against — smoothed true demand, not what shipped
            b.L(string.Format(CultureInfo.InvariantCulture,
                " · demand ≈ {0}/wk (4-week smoothed forecast)", Gd.RoundToInt(hw.DemandEma)),
                x, YStatus, DeskKit.Detail, Ink(0.85f), 520f);
        }

        // ── row 2: the week's build order ─────────────────────────────────────
        /// <summary>
        /// The core weekly lever, on the house's own stepper: −/+ walk one unit,
        /// the engine re-clamps to capacity on every write, and AUTO hands the
        /// wheel back to the base-stock policy that keeps about four weeks of
        /// cover on the shelf.
        /// </summary>
        static void Build(BinderScreen b, GameState st, HardwareState hw)
        {
            double cap = SimFactory.Capacity(st);
            int ceiling = Gd.Maxi(Gd.ToInt(cap), 0);
            double eff = SimFactory.UnitCost(st) * SimFactory.Learning(st);
            bool autoOn = hw.ProductionTarget < 0;
            int shown = SimFactory.TargetNow(st, cap, eff);
            DeskKit.Stepper(b, YBuild, new DeskKit.StepRow
            {
                Name = "build",
                Value = shown + " units",
                Bound = (shown >= ceiling && ceiling > 0) ? "at capacity" : "",
                Effect = string.Format(CultureInfo.InvariantCulture, "${0} each = ${1} this week",
                    Money2(eff), GameUi.Money(Gd.RoundToInt(shown * eff))),
                AtMin = shown <= 0,
                AtMax = shown >= ceiling,
                Pitch = 0f,
                XVal = DeskKit.XValue,
                OnMinus = () => Handle(b, "build_minus"),
                OnPlus = () => Handle(b, "build_plus"),
            });
            // AUTO rides the stepper name's own gutter — it never crowds the −/+ pair
            DeskKit.Word(b, autoOn ? ("AUTO (" + shown + ") — tap to take over")
                                   : "set AUTO (4 wks of cover)",
                XAuto, YBuild, () => Handle(b, "build_auto"),
                DeskKit.Detail, autoOn ? DrawnUI.Coral : DrawnUI.Ink, WAuto);
        }

        // ── row 3: the fleet, priced ──────────────────────────────────────────
        /// <summary>
        /// What the machines contribute and what they cost every week, idle or
        /// not. The week's broken machine is struck through where it stands, so a
        /// breakdown is visible exactly where it happened.
        /// </summary>
        static void Machines(BinderScreen b, GameState st, HardwareState hw,
                             Dictionary<string, object> w)
        {
            List<EquipmentItem> eq = hw.Equipment;
            if (eq.Count == 0)
            {
                b.L(string.Format(CultureInfo.InvariantCulture,
                    "machines: none — the founder's hands are the whole line ({0}/wk, no upkeep)",
                    Gd.ToInt(hw.CapacityBase)),
                    DeskKit.XId, YMach, DeskKit.Detail, Ink(0.6f), 1120f);
                return;
            }
            int downIndex = I(w, "down_i", -1);
            float x = DeskKit.XId;
            const string head = "machines: ";
            b.L(head, x, YMach, DeskKit.Detail, Ink(0.6f), 200f);
            x += DrawnUI.MeasureWidth(head, DeskKit.Detail);
            // group the identical ones, but never fold the broken one into a group
            var order = new List<string>();
            var counts = new Dictionary<string, int>();
            var rows = new Dictionary<string, EquipmentItem>();
            for (int i = 0; i < eq.Count; i++)
            {
                if (i == downIndex) continue;
                string id = eq[i].Id ?? "";
                if (!counts.ContainsKey(id)) { counts[id] = 0; rows[id] = eq[i]; order.Add(id); }
                counts[id] = counts[id] + 1;
            }
            if (downIndex >= 0 && downIndex < eq.Count)
            {
                EquipmentItem d = eq[downIndex];
                string dtxt = (d.Name ?? "a machine") + " DOWN (+0 this week) · ";
                b.L(dtxt, x, YMach, DeskKit.Detail, DrawnUI.Coral, 620f);
                float dw = DrawnUI.MeasureWidth(d.Name ?? "", DeskKit.Detail);
                DeskKit.Rule(b, YMach + 12f, x, dw);   // the strike: it did not run
                x += DrawnUI.MeasureWidth(dtxt, DeskKit.Detail);
            }
            for (int i = 0; i < order.Count; i++)
            {
                EquipmentItem row = rows[order[i]];
                int n = counts[order[i]];
                string seg = string.Format(CultureInfo.InvariantCulture, "{0}{1} (+{2}/wk, ${3}/wk){4}",
                    row.Name ?? "machine", n > 1 ? " ×" + n : "",
                    Gd.ToInt(row.CapacityAdd * n), GameUi.Money(Gd.ToInt(row.UpkeepWk * n)),
                    i < order.Count - 1 ? " · " : "");
                b.L(seg, x, YMach, DeskKit.Detail, Ink(0.85f), 900f);
                x += DrawnUI.MeasureWidth(seg, DeskKit.Detail);
                if (x > 1000f) break;
            }
        }

        // ── row 4: capacity is bought in lumps, and sells back at half ────────
        /// <summary>
        /// The buy row shows what this week can actually sign for and the next
        /// rung above it, dimmed with the ENGINE's own refusal — a gate that
        /// hides itself teaches nothing. The sell word is armed: it books real
        /// money, so it prints the haircut in coral before it fires.
        /// </summary>
        static void Buy(BinderScreen b, GameState st, HardwareState hw)
        {
            b.L("buy:", DeskKit.XId, YBuy + 10f, DeskKit.Detail, Ink(0.6f), 60f);
            List<SimFactory.BuyCell> cells = SimFactory.BuyRow(st);
            for (int i = 0; i < cells.Count; i++)
            {
                SimFactory.BuyCell c = cells[i];
                SimFactory.Machine e = c.Entry;
                string id = e.Id;
                if (i == 0 && c.Ok)
                {
                    DeskKit.Word(b, string.Format(CultureInfo.InvariantCulture,
                            "{0}  ${1}  +{2}/wk  ${3}/wk upkeep", e.Name, GameUi.Money(e.Price),
                            Gd.ToInt(e.CapacityAdd), GameUi.Money(Gd.ToInt(e.UpkeepWk))),
                        70f, YBuy, () => Handle(b, "buy:" + id),
                        DeskKit.Law, DrawnUI.Ink, 470f);
                    continue;
                }
                // dimmed, wearing the reason: era gate, era spend cap, or cash short
                b.L(string.Format(CultureInfo.InvariantCulture, "{0} ${1} — {2}",
                        e.Name, GameUi.Money(e.Price), c.Why),
                    i == 0 ? 70f : XBuy2, YBuy + 12f, DeskKit.Law, Ink(0.35f),
                    i == 0 ? 470f : 300f);
            }
            if (hw.Equipment.Count > 0)
            {
                EquipmentItem last = hw.Equipment[hw.Equipment.Count - 1];
                int back = SimFactory.ResaleValue(last.Id);
                string lname = last.Name ?? "it";
                if (lname.Length > 20) lname = lname.Substring(0, 20);
                DeskKit.Arm(b, "sell_machine", "sell a machine",
                    string.Format(CultureInfo.InvariantCulture,
                        "sell {0} · ${1} back (half — the secondhand haircut)", lname, GameUi.Money(back)),
                    XSell, YBuy, () => Handle(b, "sell_last"), 280f, DeskKit.Law);
            }
        }

        // ── row 5: the classic overflow decision, priced ──────────────────────
        /// <summary>
        /// A contract manufacturer's quote is your marginal cost plus THEIR
        /// margin. The toggle names the trade and carries the era's own
        /// multiplier; the fill rate beside it is what the toggle would have
        /// saved last week.
        /// </summary>
        static void MakeVsBuy(BinderScreen b, GameState st, HardwareState hw,
                              Dictionary<string, object> w, bool fresh)
        {
            if (!SimFactory.SubUnlocked(st))
            {
                b.L("make vs buy — a contract manufacturer answers from the coworking era (overflow at 1.6× your unit cost, capped at your own footprint)",
                    DeskKit.XId, YMvb + 10f, DeskKit.Law, Ink(0.5f), 1120f);
                return;
            }
            bool on = hw.SubcontractOn;
            DeskKit.Word(b, string.Format(CultureInfo.InvariantCulture,
                    "make vs buy — overflow to a contract mfr at {0}×: {1}",
                    SimFactory.SubMult(st.Era).ToString("0.##", CultureInfo.InvariantCulture),
                    on ? "ON" : "off"),
                DeskKit.XId, YMvb, () => Handle(b, "mvb"),
                DeskKit.Status, on ? DrawnUI.Coral : DrawnUI.Ink, 660f);
            string fillTxt = "fill rate — no week on record yet";
            if (fresh)
            {
                fillTxt = string.Format(CultureInfo.InvariantCulture,
                    "fill rate {0}% — repeat buyers served", Gd.RoundToInt(D(w, "fill", 1.0) * 100.0));
            }
            b.L(fillTxt, 690f, YMvb + 10f, DeskKit.Detail, Ink(0.75f), 460f);
        }

        // ── row 6: money parked on shelves ────────────────────────────────────
        /// <summary>
        /// The band's teaching line, in the footer's own grammar: BLUE while it
        /// is only doing the arithmetic out loud, CORAL the moment the arithmetic
        /// is a warning.
        /// </summary>
        static void Carrying(BinderScreen b, GameState st, HardwareState hw)
        {
            int stock = hw.Stock;
            double rate = SimFactory.CarryingRate(st);
            string line = string.Format(CultureInfo.InvariantCulture,
                "carrying cost: ${0}/wk on {1} units (2% of unit cost every week — capital, shelves and obsolescence)",
                GameUi.Money(Gd.RoundToInt(stock * rate)), stock);
            bool warn = SimFactory.Overstock(st);
            if (warn)
            {
                line += " — OVERSTOCK: more than 8 weeks of cover is asleep";
            }
            else if (hw.ProducedTotal > 0)
            {
                line += " · " + GameUi.Money(hw.ProducedTotal) + " units built";
            }
            b.L(line, DeskKit.XId, YCarry, DeskKit.Law,
                warn ? DrawnUI.Coral : DrawnUI.Blue, 1120f);
        }

        // ── presses ───────────────────────────────────────────────────────────
        /// <summary>
        /// A press inside this desk. `id` is whatever Draw registered. Handlers
        /// only ever WRITE STATE — the kit rebuilds the pane afterwards — and
        /// every write lands through a SimFactory clamp, never straight into the
        /// state.
        /// </summary>
        public static void Handle(BinderScreen b, string id)
        {
            GameState st = b.State;
            if (!SimFactory.Active(st)) return;
            HardwareState hw = SimFactory.HwView(st);
            double cap = SimFactory.Capacity(st);
            double eff = SimFactory.UnitCost(st) * SimFactory.Learning(st);
            int shown = SimFactory.TargetNow(st, cap, eff);
            switch (id)
            {
                case "build_minus":
                    SimFactory.SetTarget(st, shown - 1);
                    break;
                case "build_plus":
                    SimFactory.SetTarget(st, shown + 1);
                    break;
                case "build_auto":
                    // no dead ends: AUTO hands the wheel over, and tapping it
                    // again takes the wheel back at exactly the number it was
                    // about to build
                    SimFactory.SetTarget(st, hw.ProductionTarget >= 0 ? -1 : shown);
                    break;
                case "mvb":
                    SimFactory.ToggleSubcontract(st);
                    break;
                case "sell_last":
                    SimFactory.SellEquipment(st, hw.Equipment.Count - 1);
                    break;
                default:
                    if (id != null && id.StartsWith("buy:"))
                        SimFactory.BuyEquipment(st, id.Substring(4));
                    break;
            }
        }

        // ── the two number formats this strip speaks ──────────────────────────
        static string Money2(double v)
        {
            return v.ToString("F2", CultureInfo.InvariantCulture);
        }

        static double D(Dictionary<string, object> w, string k, double dflt)
        {
            object v;
            if (w == null || !w.TryGetValue(k, out v) || v == null) return dflt;
            try { return System.Convert.ToDouble(v, CultureInfo.InvariantCulture); }
            catch (System.Exception) { return dflt; }
        }

        static int I(Dictionary<string, object> w, string k, int dflt)
        {
            object v;
            if (w == null || !w.TryGetValue(k, out v) || v == null) return dflt;
            try { return System.Convert.ToInt32(v, CultureInfo.InvariantCulture); }
            catch (System.Exception) { return dflt; }
        }
    }
}
