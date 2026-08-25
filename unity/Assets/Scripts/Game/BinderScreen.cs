using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// THE OPERATIONS BINDER — binder.gd, ported. The founder's dashboard, in the
    /// game's own hand. Never a SaaS panel: a clipboard sheet over the dimmed room,
    /// nine pen-labelled tabs, doodle icons, charts drawn as wobbly polylines.
    ///
    /// FOG OF WAR: precision follows analytics_level (0-3). At 0 the customer page says
    /// "traffic seems decent"; invest in analytics — a writable move — and the pages
    /// sharpen. The dashboard you EARN is a mechanic, not a view.
    ///
    /// THE LEDGER AND PRICING ARE THE ONLY TABS THAT WRITE. Every other page reads the
    /// engine; those two set the four weekly levers and the price of each offer, which
    /// is the whole of the player's standing-order interface.
    /// </summary>
    public sealed class BinderScreen : MonoBehaviour
    {
        static readonly string[] Tabs =
        {
            "vitals", "the ledger", "pricing", "customers", "product",
            "crew", "cap table", "the street", "threats",
        };

        static readonly string[][] Levers =
        {
            new[] { "marketing", "reach — more people hear of you; saturates past ~$2k" },
            new[] { "sales", "closing — every $600/wk closes like one more part-time seller" },
            new[] { "care", "retention — up to 30% less churn as care approaches $3k" },
            new[] { "rnd", "product — ships ~+1 quality per $1,200/wk and pays down debt" },
            new[] { "office", "the office — food, perks, benefits; morale climbs toward +3/wk by ~$2k" },
        };
        static readonly int[] LeverSteps = { 0, 250, 500, 1000, 2000, 4000, 8000 };

        // THE PEN RING, from `_Clipboard._draw`: an ellipse centred on (24 + tab*133 +
        // 65, 76) with radii 68 and 26, wobbled ±2 and stroked 3.5. The sprite is the
        // ink's own box — its pad is the jitter plus half the stroke plus a margin —
        // and it mounts 1:1, so the ring is the size the hand drew and not a squashed
        // circle. A 60r circle stretched into a 130×52 cell measured 127×50 of ink
        // against Godot's ~143×59, at 607 coral pixels against 1321, and its stroke ran
        // 3.28 across but 1.31 top and bottom — a stretched circle holds no one width.
        const float RingRx = 68f;
        const float RingRy = 26f;
        const float RingPad = 6f;                       // ceil(2 + 3.5/2 + 2)
        const float RingW = RingRx * 2f + RingPad * 2f;   // 148
        const float RingH = RingRy * 2f + RingPad * 2f;   // 64
        const float RingTop = 76f - RingH * 0.5f;         // 44
        static float RingX(int tab) { return 24f + tab * 133f + 65f - RingW * 0.5f; }

        GameState _st;
        RectTransform _sheet;
        RectTransform _content;
        Image _tabRing;
        readonly Dictionary<string, TextMeshProUGUI> _bangs =
            new Dictionary<string, TextMeshProUGUI>();
        int _tab;

        /// TAB AND B TOGGLE, THEY DO NOT FIGHT. Both the room and the binder listen for
        /// the same keys, so without this the room re-opened the binder in the very
        /// frame the binder dismissed itself.
        public static bool IsOpen { get; private set; }

        public static BinderScreen Open(GameState state)
        {
            if (IsOpen) return null;
            var boot = Boot.Instance;
            if (boot == null || state == null) return null;
            var rt = DrawnUI.FullRect(boot.OverlayLayer, "binder");
            var b = rt.gameObject.AddComponent<BinderScreen>();
            b._st = state;
            IsOpen = true;
            Runway.Audio.RunwayMix.SetState("binder");
            b.BuildParts();
            return b;
        }

        void OnDestroy() { IsOpen = false; Runway.Audio.RunwayMix.SetState("normal"); }

        void BuildParts()
        {
            var rt = GetComponent<RectTransform>();
            GameUi.Scrim(rt, new Color(0.05f, 0.05f, 0.06f, 0.55f), Dismiss);

            _sheet = GameUi.PaperSheet(rt, 148f, 52f, 1240f, 920f, 7, 4f, null, "clipboard");
            // the clipboard's own shadow: `_Clipboard._draw` opens on draw_rect(Rect2(
            // 8, 12, w, h), Color(0, 0, 0, 0.25)) — a heavier, further-thrown shadow
            // than the shared sheet's, because this board is held over a dimmed room
            var board = _sheet.Find("shadow");
            if (board != null)
            {
                var boardImg = board.GetComponent<Image>();
                if (boardImg != null) boardImg.color = new Color(0f, 0f, 0f, 0.25f);
                DrawnUI.SetTopLeft(board as RectTransform, 8f, 12f);
            }
            // the clip at the top
            var clip = DrawnUI.Fill(_sheet, "clip", DrawnUI.Yellow, 1240f * 0.5f - 70f, -18f, 140f, 34f);
            clip.raycastTarget = false;
            DrawnUI.AddInkEdge(clip.rectTransform, new Vector2(140f, 34f), new DrawnUI.PaperStyle
            {
                ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 2f,
                StepsPerEdge = 6, Jitter = 1f, Thickness = 4f, Seed = 7,
            });

            // THE PEN RING AND THE RULE GO DOWN BEFORE THE WORDS. `_Clipboard._draw`
            // paints both, and a Godot node draws under its own children — so the ring
            // passes BEHIND the tab it circles, never across it.
            _tabRing = DrawnChart.Mount(_sheet, "tabring",
                DrawnChart.PenEllipse(RingRx, RingRy, 3.5f, 2f, 5, DrawnUI.Coral),
                RingX(0), RingTop, RingW, RingH);
            DrawnUI.Fill(_sheet, "tabrule", DrawnUI.WithAlpha(DrawnUI.Ink, 0.25f),
                         30f, 108f, 1180f, 2f);

            for (int i = 0; i < Tabs.Length; i++)
            {
                int idx = i;
                GameUi.InkWord(_sheet, Tabs[i], 24f + i * 133f, 54f, 130f, 44f, 23f,
                    DrawnUI.Ink, () => { _tab = idx; Refresh(); });
                // THE WARNING BANGS (owner: "! warnings on tab where things
                // are unset"): pricing = something bills at the going rate ·
                // the ledger = losing money · cap table = term sheets waiting.
                if (Tabs[i] == "pricing" || Tabs[i] == "the ledger" || Tabs[i] == "cap table")
                {
                    var bang = DrawnUI.HandLabel(_sheet, "!", 24f + i * 133f + 103f,
                        42f, 30f, DrawnUI.Coral, 30f);
                    bang.gameObject.SetActive(false);
                    _bangs[Tabs[i]] = bang;
                }
            }

            GameUi.InkWord(_sheet, "×", 1180f, 8f, 52f, 52f, 46f, DrawnUI.Coral, Dismiss);
            _content = DrawnUI.Rect(_sheet, "content", 40f, 118f, 1160f, 760f);

            // the clipboard comes UP off the desk, like everything else in this game
            var g = DrawnUI.Group(rt);
            g.alpha = 0f;
            StartCoroutine(DrawnUI.FadeTo(g, 1f, 0.2f));
            Refresh();
        }

        /// A component added mid-frame can still be reached by the same Update pass, so
        /// the key that OPENED the binder must not be the key that closes it. One frame
        /// of deafness is the whole fix.
        bool _armed;

        void Update()
        {
            if (!_armed) { _armed = true; return; }
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab)
                || Input.GetKeyDown(KeyCode.B))
                Dismiss();
        }

        void Dismiss()
        {
            if (this != null && gameObject != null) Destroy(gameObject);
        }

        // ══ composition ════════════════════════════════════════════════════════

        void Refresh()
        {
            TextMeshProUGUI bang2;
            if (_bangs.TryGetValue("pricing", out bang2))
                bang2.gameObject.SetActive(SimEngine.OffersAnyUnpriced(_st));
            if (_bangs.TryGetValue("the ledger", out bang2))
                bang2.gameObject.SetActive(_st.LastPnl != null && _st.LastPnl.Net < 0);
            if (_bangs.TryGetValue("cap table", out bang2))
                bang2.gameObject.SetActive(_st.HasFlag("fundraising_open"));
            for (int i = _content.childCount - 1; i >= 0; i--)
                Destroy(_content.GetChild(i).gameObject);
            if (_tabRing != null)
                DrawnUI.SetTopLeft(_tabRing.rectTransform, RingX(_tab), RingTop);
            switch (_tab)
            {
                case 0: TabVitals(); break;
                case 1: TabLedger(); break;
                case 2: TabPricing(); break;
                case 3: TabCustomers(); break;
                case 4: TabProduct(); break;
                case 5: TabCrew(); break;
                case 6: TabCap(); break;
                case 7: TabStreet(); break;
                default: TabThreats(); break;
            }
        }

        TextMeshProUGUI L(string text, float x, float y, float size = 30f,
                          Color? col = null, float w = 1100f)
        {
            return DrawnUI.HandLabel(_content, text, x, y, size, col ?? DrawnUI.Ink, w,
                                     TextAlignmentOptions.TopLeft);
        }

        void Icon(string name, float x, float y, float side = 72f)
        {
            string p = ArtCache.IconPath(name);
            if (!RunwayPaths.ArtExists(p)) return;
            GameUi.Picture(_content, "icon", p, x, y, side, side);
        }

        List<float> Series(string key)
        {
            var outp = new List<float>();
            for (int i = 0; i < _st.MetricHistory.Count; i++)
            {
                MetricSnapshot m = _st.MetricHistory[i];
                switch (key)
                {
                    case "cash": outp.Add(m.Cash); break;
                    case "customers": outp.Add(m.Customers); break;
                    case "morale": outp.Add(m.Morale); break;
                    case "debt": outp.Add(m.Debt); break;
                    case "hype": outp.Add(m.Hype); break;
                }
            }
            return outp;
        }

        /// One `_Spark`, whole: the ground wash, the wobbled line, and the hi/lo numbers
        /// in its corners. The short-series line comes with it, so an empty chart is
        /// still a panel of the sheet rather than a hole in it.
        void Spark(string key, float x, float y, float w, float h, Color col)
        {
            DrawnChart.MountSpark(_content, Series(key), col, x, y, w, h);
        }

        // ── tab 0: vitals ──────────────────────────────────────────────────────

        void TabVitals()
        {
            Icon("cash", 10f, 6f);
            L("$" + GameUi.Money(_st.Cash) + " in the bank", 100f, 10f, 46f);
            L(SimEngine.HealthBand(_st), 100f, 66f, 30f,
              SimEngine.RunwayWeeks(_st) <= 10 ? DrawnUI.Coral : DrawnUI.Sage);
            L("cash, drawn weekly:", 10f, 140f, 24f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f));
            Spark("cash", 10f, 172f, 1120f, 190f, DrawnUI.Blue);
            MetricSnapshot last = _st.MetricHistory.Count > 0
                ? _st.MetricHistory[_st.MetricHistory.Count - 1] : new MetricSnapshot();
            L(string.Format("last week: ${0} in · ${1} out",
                GameUi.Money(last.Revenue), GameUi.Money(last.Burn)), 10f, 386f);
            int payroll = 0;
            for (int i = 0; i < _st.Employees.Count; i++) payroll += _st.Employees[i].Salary;
            int rent;
            if (!GameState.ERA_RENT.TryGetValue(_st.Era, out rent)) rent = 150;
            L(string.Format("burn: rent ${0} · payroll ${1} · marketing ${2}{3}",
                GameUi.Money(rent), GameUi.Money(payroll), GameUi.Money(_st.MarketingBudget),
                _st.LoanPrincipal > 0
                    ? "  ·  LOAN OWED $" + GameUi.Money(_st.LoanPrincipal) + " (18%/wk)" : ""),
                10f, 432f, 27f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.8f));
            L("valuation, if anyone asked: $" + GameUi.Money(SimEngine.Valuation(_st)), 10f, 486f);
            L(string.Format("price ×{0:0.00}  ·  the market is {1}", _st.PriceMult,
                _st.MarketTrend > 1.05 ? "warm" : (_st.MarketTrend < 0.95 ? "cold" : "even")),
                10f, 532f, 27f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.8f));
        }

        // ── tab 1: THE LEDGER — the levers, the math, the truth about the money ──

        void TabLedger()
        {
            L("the ledger — where this week's money goes", 10f, 6f, 38f);
            float y = 78f;
            for (int i = 0; i < Levers.Length; i++)
            {
                string cat = Levers[i][0];
                int cur = Budget(cat);
                L(cat.ToUpper(), 10f, y, 30f);
                L(Levers[i][1], 10f, y + 38f, 23f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f));
                L("$" + GameUi.Money(cur) + "/wk", 520f, y + 6f, 32f, DrawnUI.Coral, 200f);
                // WHAT THIS MONEY IS DOING RIGHT NOW, from the engine's own formulas
                L(LeverEffect(cat, cur), 688f, y + 12f, 24f,
                  DrawnUI.WithAlpha(DrawnUI.Ink, 0.75f), 300f);
                string c = cat;
                int at = cur;
                GameUi.InkWord(_content, "−", 1000f, y, 52f, 46f, 40f, DrawnUI.Ink, () =>
                {
                    SetBudget(c, Step(at, -1));
                    Refresh();
                });
                GameUi.InkWord(_content, "+", 1064f, y, 52f, 46f, 40f, DrawnUI.Ink, () =>
                {
                    SetBudget(c, Step(at, 1));
                    Refresh();
                });
                y += 92f;
            }
            // the math, honestly
            int leverSum = _st.Budgets.Sum();
            int rw = SimEngine.RunwayWeeks(_st);
            y += 6f;
            L("the math", 10f, y, 30f, DrawnUI.Blue);
            double arpu = UnitEcon("arpu");
            int cac = Gd.ToInt(UnitEcon("cac"));
            int ltv = Gd.ToInt(UnitEcon("ltv"));
            int pb = Gd.ToInt(UnitEcon("payback_wk"));
            // THE DOLLAR SIGN BELONGS TO THE SENTENCE, not to the number. The ledger
            // says "$?" when it does not know yet — a bare "?" reads as a missing word.
            L(string.Format(
                "a customer pays ≈ ${0:0}/wk · costs ${1} to win (CAC) · is worth ${2} over their stay (LTV) · pays back in {3}",
                arpu, cac > 0 ? GameUi.Money(cac) : "?",
                ltv > 0 ? GameUi.Money(ltv) : "?", pb > 0 ? pb + " wks" : "—"),
                10f, y + 40f, 25f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.75f));
            L(string.Format("levers total ${0}/wk · runway {1}", GameUi.Money(leverSum),
                rw < 999 ? rw + " weeks" : "gaining money"), 10f, y + 108f, 28f);
            // THE WEEK, HONESTLY (owner: a real business sim knows its running
            // cost): the engine's own P&L record, every lane, the bottom line.
            Pnl pnl = _st.LastPnl;
            float ry = y + 148f;
            if (pnl != null)
            {
                L("last week, honestly:", 10f, ry, 28f, DrawnUI.Blue);
                L(string.Format("in ${0}   ·   serving cost ${1}{2}",
                    GameUi.Money(pnl.Revenue), GameUi.Money(pnl.Cogs),
                    pnl.Learning < 0.995
                        ? string.Format("  (learning curve ×{0:0.00})", pnl.Learning) : ""),
                    10f, ry + 40f, 25f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.8f));
                L(string.Format("out: rent ${0} · payroll ${1} · infra ${2} · levers ${3}{4}{5}",
                    GameUi.Money(pnl.Rent), GameUi.Money(pnl.Payroll), GameUi.Money(pnl.Infra),
                    GameUi.Money(pnl.Marketing + pnl.Sales + pnl.Care + pnl.Rnd + pnl.Office),
                    pnl.Incident > 0 ? " · unforeseen $" + GameUi.Money(pnl.Incident) : "",
                    pnl.LiabilitiesWk > 0 ? " · standing costs $" + GameUi.Money(pnl.LiabilitiesWk) + "/wk" : ""),
                    10f, ry + 76f, 25f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.8f), 1100f);
                L(string.Format("THE BOTTOM LINE: {0}${1} a week",
                    pnl.Net >= 0 ? "+" : "−", GameUi.Money(Math.Abs(pnl.Net))),
                    10f, ry + 116f, 30f, pnl.Net >= 0 ? DrawnUI.Sage : DrawnUI.Coral);
                ry += 164f;
            }
            if (rw <= 4 && rw < 999)
            {
                L(string.Format("⚠ this spend kills the company in {0} weeks — cut it or earn it", rw),
                  10f, ry, 28f, DrawnUI.Coral);
                ry += 40f;
            }
            if (_st.Cash < 0)
            {
                L(string.Format("THE RED: {0} of 3 weeks below zero. At three, it's over.",
                    _st.WeeksInRed), 10f, ry, 28f, DrawnUI.Coral);
                ry += 44f;
            }
            L("the rules of this world: reach saturates · only capacity closes · churn is a "
              + "leaky bucket · debt slows everything · three weeks below zero ends it",
              10f, ry + 8f, 22f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f));
        }

        /// SimEngine parks the week's unit economics on the state as ONE nested map.
        /// Fresh from the tick it is a Dictionary; loaded back off disk Newtonsoft
        /// hands it over as a JObject — so this reads both and answers 0 for neither.
        double UnitEcon(string key)
        {
            object box = _st.GetMeta("unit_econ", null);
            if (box == null) return 0.0;
            var dict = box as IDictionary<string, object>;
            if (dict != null)
            {
                object v;
                if (!dict.TryGetValue(key, out v) || v == null) return 0.0;
                double d;
                return double.TryParse(v.ToString(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out d) ? d : 0.0;
            }
            var jo = box as Newtonsoft.Json.Linq.JObject;
            return jo != null ? ContentDb.Num(jo, key, 0.0) : 0.0;
        }

        int Budget(string cat)
        {
            switch (cat)
            {
                case "marketing": return _st.Budgets.Marketing;
                case "sales": return _st.Budgets.Sales;
                case "care": return _st.Budgets.Care;
                case "rnd": return _st.Budgets.Rnd;
                case "office": return _st.Budgets.Office;
            }
            return 0;
        }

        void SetBudget(string cat, int v)
        {
            switch (cat)
            {
                case "marketing": _st.Budgets.Marketing = v; break;
                case "sales": _st.Budgets.Sales = v; break;
                case "care": _st.Budgets.Care = v; break;
                case "rnd": _st.Budgets.Rnd = v; break;
                case "office": _st.Budgets.Office = v; break;
            }
        }

        int Step(int cur, int dir)
        {
            int idx = 0;
            for (int i = 0; i < LeverSteps.Length; i++) if (LeverSteps[i] <= cur) idx = i;
            idx = Gd.Clampi(idx + dir, 0, LeverSteps.Length - 1);
            return Gd.Mini(LeverSteps[idx], SimEngine.EraSpendCap(_st.Era));
        }

        /// the engine's live math for one lever, in one plain phrase
        string LeverEffect(string cat, int v)
        {
            double sat = _st.Theta != null ? _st.Theta.CacSat : 900.0;
            switch (cat)
            {
                case "marketing":
                    return v > 0
                        ? string.Format("reach ×{0:0.00}",
                            1.0 + 1.4 * (1.0 - Mathf.Exp(-(float)(v / sat))))
                        : "no reach bought";
                case "sales":
                    return v > 0 ? string.Format("+{0:0.0} closers of capacity", v / 600f)
                                 : "founder sells alone";
                case "care":
                    return v > 0 ? string.Format("churn −{0}%",
                        Mathf.RoundToInt(30f * (1f - Mathf.Exp(-v / 1500f)))) : "nobody picks up";
                case "rnd":
                    return v > 0 ? string.Format("+{0:0.0} product/wk, debt melts", v / 1200f)
                                 : "no extra shipping";
                case "office":
                    return v > 0 ? string.Format("+{0:0.0} morale/wk",
                        3.0 * (1.0 - Mathf.Exp(-v / 800f))) : "instant coffee, cold room";
            }
            return "";
        }

        // ── tab 2: PRICING ─────────────────────────────────────────────────────

        void TabPricing()
        {
            L("pricing — what " + _st.CompanyName + " sells", 10f, 6f, 36f);
            if (_st.Offers.Count == 0)
            {
                L("the world hasn't defined your offers yet — they arrive with the bible.",
                  10f, 90f, 28f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f));
                return;
            }
            float y = 84f;
            for (int i = 0; i < _st.Offers.Count; i++)
            {
                Offer o = _st.Offers[i];
                L((o.Name ?? "?").ToUpper() + "  ·  " + (o.Unit ?? ""), 10f, y, 30f);
                double ucEff = o.UnitCost * SimEngine.LearningCurve(_st);
                L(string.Format("the street charges ≈ ${0}  ·  costs you ≈ ${1} to serve",
                    GameUi.Money(Gd.ToInt(o.FairPrice)), GameUi.Money(Gd.RoundToInt(ucEff))),
                    10f, y + 38f, 23f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f), 600f);
                if (o.Price <= 0.0 && o.PriceSet)
                    L("FREE ON PURPOSE — pays in users, not dollars", 430f, y + 6f, 27f,
                      DrawnUI.Blue, 540f);
                else if (o.Price <= 0.0)
                    L("! no price set — billing at the going rate $" + GameUi.Money(Gd.ToInt(o.FairPrice)),
                      430f, y + 6f, 27f, DrawnUI.Coral, 540f);
                else
                {
                    double dem = SimEngine.OfferDemand(o, o.Price);
                    string verdict = dem > 0.85 && dem < 1.15 ? "about fair"
                        : (dem >= 1.15 ? string.Format("a deal — demand ×{0:0.0}", dem)
                        : (dem > 0.25 ? string.Format("pricey — {0}% of fair demand", (int)(dem * 100.0))
                        : "absurd — ~nobody buys"));
                    L(string.Format("${0}  ·  margin ${1}/unit  ·  {2}", GameUi.Money(Gd.ToInt(o.Price)),
                        GameUi.Money(Gd.RoundToInt(o.Price - ucEff)), verdict),
                      430f, y + 6f, 28f, dem > 0.25 ? DrawnUI.Ink : DrawnUI.Coral, 540f);
                }
                Offer captured = o;
                GameUi.InkWord(_content, "−", 1000f, y, 52f, 46f, 40f, DrawnUI.Ink, () =>
                {
                    PriceStep(captured, -1);
                    Refresh();
                });
                GameUi.InkWord(_content, "+", 1064f, y, 52f, 46f, 40f, DrawnUI.Ink, () =>
                {
                    PriceStep(captured, 1);
                    Refresh();
                });
                y += 104f;
            }
            double arpu2 = SimEngine.OffersArpu(_st);
            if (arpu2 >= 0.0)
            {
                double cpc = SimEngine.OffersCogsPerCustomer(_st);
                L(string.Format(
                    "all offers together: ≈ ${0:0.0} in − ${1:0.0} serving = ${2:0.0} margin per customer per week  →  ≈ ${3}/wk at {4} customers",
                    arpu2, cpc, arpu2 - cpc,
                    GameUi.Money(Gd.ToInt((arpu2 - cpc) * _st.Traction)), _st.Traction),
                    10f, y + 10f, 26f, DrawnUI.Blue, 1100f);
            }
            L("the curve: price at the street's level and demand is fair · discount and demand "
              + "grows · overprice and it dies fast", 10f, y + 56f, 22f,
              DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f));
        }

        /// price steps: a sensible ladder around the fair price (0 = off sale)
        static void PriceStep(Offer o, int dir)
        {
            double fair = o.FairPrice > 0.0 ? o.FairPrice : 10.0;
            var steps = new List<double> { 0.0 };
            double[] mults = { 0.4, 0.55, 0.7, 0.85, 1.0, 1.15, 1.35, 1.6, 2.0, 2.6, 3.5, 5.0 };
            for (int i = 0; i < mults.Length; i++)
                steps.Add(Gd.Maxf(Gd.Round(fair * mults[i]), 1.0));
            int idx = 0;
            for (int i = 0; i < steps.Count; i++) if (steps[i] <= o.Price) idx = i;
            idx = Gd.Clampi(idx + dir, 0, steps.Count - 1);
            o.Price = steps[idx];
            o.PriceSet = true;   // the founder chose this — $0 included (a conscious giveaway)
        }

        // ── tab 3: customers (fog of war) ──────────────────────────────────────

        void TabCustomers()
        {
            Icon("customers", 10f, 6f);
            if (_st.AnalyticsLevel <= 0)
            {
                L(_st.Traction + " customers, give or take.", 100f, 10f, 46f);
                L("Traffic seems… decent? Someone signed up on Tuesday. The numbers live in a "
                  + "notebook you lost.", 10f, 110f, 30f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f));
                L("(invest in analytics to see the funnel)", 10f, 210f, 26f, DrawnUI.Coral);
                return;
            }
            L(_st.Traction + " customers", 100f, 10f, 46f);
            L("customers, weekly:", 10f, 100f, 24f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f));
            Spark("customers", 10f, 132f, 1120f, 200f, DrawnUI.Sage);
            double tam = _st.Beliefs != null && _st.Beliefs.Tam > 0.0
                ? _st.Beliefs.Tam : (_st.Theta != null ? _st.Theta.Tam : 100000.0);
            double life = _st.Beliefs != null && _st.Beliefs.LifetimeWk > 0.0
                ? _st.Beliefs.LifetimeWk : (_st.Theta != null ? _st.Theta.LifetimeWk : 40.0);
            L(string.Format(
                "market, as you believe it: ~{0} buyers ({1:0.0}% reached) · a customer stays ≈ {2} wks",
                GameUi.Money(Gd.ToInt(tam)), _st.Traction / Gd.Maxf(tam, 1.0) * 100.0, Gd.ToInt(life)),
                10f, 356f, 27f);
            L("working assumptions — they sharpen as you learn", 10f, 392f, 22f,
              DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f));
            if (_st.AnalyticsLevel >= 2)
            {
                // the second analytics level BUYS the CAC read — dropping it left the
                // upgrade paying for a line the player already had
                double mk = _st.MarketingBudget;
                string cac = mk <= 0.0 ? "∞"
                    : "$" + Gd.ToInt(mk / Gd.Maxf(1.0, mk / 900.0));
                L(string.Format("price ×{0:0.00} · marketing ${1}/wk · CAC roughly {2}",
                    _st.PriceMult, GameUi.Money(_st.MarketingBudget), cac), 10f, 404f, 28f);
                L(string.Format("lifetime ≈ {0} wks at v0.{1} quality",
                    Gd.ToInt(life * (0.4 + _st.Product / 100.0 * 1.2)), _st.Product), 10f, 448f, 28f);
            }
            if (_st.AnalyticsLevel >= 3)
                L("the funnel is fully lit: organic + word-of-mouth + paid, all measured. "
                  + "You are the analytics now.", 10f, 500f, 26f, DrawnUI.Sage);
        }

        // ── tab 4: product ─────────────────────────────────────────────────────

        void TabProduct()
        {
            Icon("product", 10f, 6f);
            L("v0." + _st.Product, 100f, 10f, 46f);
            L("tech debt:", 10f, 110f, 28f);
            // THE DEBT JAR — `_DebtJar._draw` at position (160, 92), size 90×110. It is
            // a VESSEL: a faint ground, a coral level, a 4px ink outline round the whole
            // height and a heavier line across the lip. Without the outline the level
            // floats and the jar is not a jar. Every number below is the .gd's own,
            // with the jar's (160, 92) already added in.
            DrawnUI.Fill(_content, "jarback", DrawnUI.WithAlpha(DrawnUI.Ink, 0.04f), 166f, 102f, 78f, 96f);
            float fill = Mathf.Clamp01((float)_st.TechDebt / 100f);
            // the level rides h-16 = 94, not the ground's 96
            DrawnUI.Fill(_content, "jarfill", DrawnUI.WithAlpha(DrawnUI.Coral, 0.55f),
                168f, 102f + 94f * (1f - fill), 74f, 94f * fill);
            JarEdge(166f, 102f, 78f, 96f, 4f);
            // draw_line((2, 10) → (w-2, 10), INK, 5.0): a stroke CENTRED on the lip
            DrawnUI.Fill(_content, "jarlip", DrawnUI.Ink, 162f, 99.5f, 86f, 5f);
            double risk = Gd.Maxf((_st.TechDebt - 40.0) / 250.0, 0.0) * 100.0;
            L(string.Format("outage odds ≈ {0}% weekly", Gd.ToInt(risk)), 290f, 120f, 28f,
              risk > 10.0 ? DrawnUI.Coral : DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f));
            L("debt, weekly:", 10f, 236f, 24f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f));
            Spark("debt", 10f, 268f, 1120f, 170f, DrawnUI.Coral);
            L("hype:", 10f, 470f, 28f);
            Spark("hype", 120f, 452f, 1010f, 130f, DrawnUI.Yellow);
        }

        /// draw_rect(rect, INK, false, t): a rect OUTLINE, four bars whose centre lines
        /// are the rect's own edges — which is where Godot puts a thick unfilled rect.
        void JarEdge(float x, float y, float w, float h, float t)
        {
            float half = t * 0.5f;
            DrawnUI.Fill(_content, "jaredge", DrawnUI.Ink, x - half, y - half, w + t, t);
            DrawnUI.Fill(_content, "jaredge", DrawnUI.Ink, x - half, y + h - half, w + t, t);
            DrawnUI.Fill(_content, "jaredge", DrawnUI.Ink, x - half, y - half, t, h + t);
            DrawnUI.Fill(_content, "jaredge", DrawnUI.Ink, x + w - half, y - half, t, h + t);
        }

        // ── tab 5: crew ────────────────────────────────────────────────────────

        void TabCrew()
        {
            Icon("you", 10f, 6f);
            string who = (_st.FounderName ?? "").Length > 0 ? _st.FounderName : "the founder";
            L(string.Format("{0} — lvl {1} · XP {2}/{3} spent · exhaustion {4}/6", who,
                _st.Level, _st.XpSpent, _st.Xp, _st.Exhaustion), 100f, 20f, 32f);
            var stats = new List<string>();
            for (int i = 0; i < FounderDraftScreen.StatNames.Length; i++)
                stats.Add(FounderDraftScreen.StatNames[i] + " "
                          + _st.Competence(FounderDraftScreen.StatNames[i]));
            L(string.Join("  ·  ", stats.ToArray()), 100f, 64f, 27f,
              DrawnUI.WithAlpha(DrawnUI.Ink, 0.8f));
            float y = 130f;
            RunDriver driver = RunDriver.Current;
            for (int i = 0; i < _st.Cofounders.Count; i++)
            {
                Cofounder cf = _st.Cofounders[i];
                Icon("cofd_tech", 10f, y);
                string nm = (cf.Name ?? "").Trim();
                L(string.Format("{0}{1} cofounder · {2:0}% equity · loyalty {3}",
                    nm.Length > 0 ? nm + " — " : "", cf.Role,
                    cf.EquityDiluted.HasValue ? cf.EquityDiluted.Value : cf.Equity,
                    driver != null ? driver.Loyalty(i) : 70), 100f, y + 16f, 28f);
                y += 84f;
            }
            for (int i = 0; i < _st.Employees.Count; i++)
            {
                Employee e = _st.Employees[i];
                Icon("employee", 10f, y);
                L(string.Format("{0} — {1} · ${2}/wk · burnout {3}", e.Name, e.Role,
                    GameUi.Money(e.Salary), e.Burnout), 100f, y + 16f, 28f);
                y += 84f;
            }
            for (int i = 0; i < _st.Pipeline.Count; i++)
            {
                PipelineHire h = _st.Pipeline[i];
                Icon("employee", 10f, y);
                L(string.Format("{0} — {1} · ONBOARDING (paid, not yet productive)", h.Name, h.Role),
                  100f, y + 16f, 28f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f));
                y += 84f;
            }
            L("morale:", 10f, y + 10f, 28f);
            Spark("morale", 120f, y - 8f, 1000f, 120f, DrawnUI.Sage);
        }

        // ── tab 6: cap table ───────────────────────────────────────────────────

        void TabCap()
        {
            double founder = _st.FounderPct;
            double cof = 0.0;
            for (int i = 0; i < _st.Cofounders.Count; i++)
                cof += _st.Cofounders[i].EquityDiluted.HasValue
                    ? _st.Cofounders[i].EquityDiluted.Value : _st.Cofounders[i].Equity;
            double investors = Gd.Maxf(100.0 - founder - cof, 0.0);
            // THE WHEEL IS 430 WIDE AND ITS INK IS AT 0.38 OF THAT. A 340 box put the
            // centre at (210, 200) where the original has it at (255, 245), and every
            // label hung off it inherited the error.
            var pcts = new[] { (float)founder, (float)cof, (float)investors };
            var cols = new[] { DrawnUI.Coral, DrawnUI.Blue, DrawnUI.Sage };
            var names = new[] {
                string.Format("you {0:0}%", founder),
                string.Format("cofounders {0:0}%", cof),
                string.Format("investors {0:0}%", investors),
            };
            DrawnChart.Mount(_content, "pie", DrawnChart.CapPie(pcts, cols, PieSide),
                             PieX, PieY, PieSide, PieSide);
            PieLabels(pcts, names);

            float y = 60f;
            L("rounds:", 540f, 30f, 32f, DrawnUI.Ink, 560f);
            if (_st.RoundsRaised.Count == 0)
                L("none yet. every point of the company is still on this table.",
                  540f, y + 20f, 27f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 560f);
            for (int i = 0; i < _st.RoundsRaised.Count; i++)
            {
                L("· " + _st.RoundsRaised[i] + " — closed", 540f, y + 20f, 28f, DrawnUI.Ink, 560f);
                y += 44f;
            }
            L("valuation $" + GameUi.Money(SimEngine.Valuation(_st)), 540f, y + 80f, 30f,
              DrawnUI.Ink, 560f);
            L("your slice today: $" + GameUi.Money(
                Gd.ToInt(SimEngine.Valuation(_st) * _st.FounderPct / 100.0)),
                540f, y + 128f, 30f, DrawnUI.Coral, 560f);
            // what the NEXT round would cost, so dilution is never a surprise
            int val = SimEngine.Valuation(_st);
            if (val > 0)
            {
                int ask = (int)(val * 0.10);
                double fairPct = (double)ask / (val + ask) * 100.0;
                double warm = SimEngine.WarmthPct(_st);
                double asked = fairPct * 1.3 * (1.0 - warm / 100.0);
                L(string.Format(
                    "raise ~${0} now → investors ask ≈ {1:0}%{2} · your {3:0}% would become ≈ {4:0}%",
                    GameUi.Money(ask), asked,
                    warm > 0.0 ? string.Format(" ({0:0}% off — they know you)", warm) : "",
                    _st.FounderPct, _st.FounderPct * (1.0 - asked / 100.0)),
                    540f, y + 186f, 24f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 620f);
            }
            if (_st.HasFlag("fundraising_open"))
                L("! TERM SHEETS ARE ON THE TABLE — sign in the journal before they expire",
                  40f, 480f, 27f, DrawnUI.Coral, 1100f);
        }

        const int PieSide = 430;      // `pie.set_deferred("size", Vector2(430, 430))`
        const float PieX = 40f;
        const float PieY = 30f;

        /// THE NAMES GO ROUND THE WHEEL, NOT UNDER IT. `_Pie._draw` walks the slices a
        /// second time and hangs each label at the MIDDLE of its own arc, 40px outside
        /// the ink, all in plain ink — a stacked legend beside the chart is a different
        /// drawing and it stopped saying which colour was whose.
        /// draw_string plants a BASELINE, and the original nudges it by (-46, +8).
        void PieLabels(IList<float> pct, IList<string> names)
        {
            const float TwoPi = Mathf.PI * 2f;
            float cx = PieX + PieSide * 0.5f;
            float cy = PieY + PieSide * 0.5f;
            float rr = PieSide * DrawnChart.PieRadiusFrac + 40f;
            float a0 = -Mathf.PI * 0.5f;                 // twelve o'clock
            for (int i = 0; i < pct.Count; i++)
            {
                float frac = Mathf.Clamp(pct[i], 0f, 100f) / 100f;
                if (frac <= 0.01f) continue;             // a sliver gets no name
                float mid = a0 + TwoPi * frac * 0.5f;
                float px = cx + Mathf.Cos(mid) * rr;
                float py = cy + Mathf.Sin(mid) * rr;
                L(names[i], px - 46f, py + 8f - 24f * 0.78f, 24f, DrawnUI.Ink, 0f);
                a0 += TwoPi * frac;
            }
        }

        // ── tab 7: the street ──────────────────────────────────────────────────

        /// Wrapped text is MEASURED, never assumed one line — fixed steps stacked the
        /// street on itself the first week a thesis wrapped.
        void TabStreet()
        {
            L("the street", 10f, 6f, 40f);
            float y = 80f;
            for (int i = 0; i < _st.Rivals.Count; i++)
            {
                Rival r = _st.Rivals[i];
                L(string.Format("{0} — {1}", r.Name, SimEngine.Fuzz(r.Strength)), 10f, y, 32f);
                string plays = "plays: " + string.Join(", ", r.Tactics.ToArray());
                var lbl = L(plays, 30f, y + 42f, 26f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 1070f);
                y += 50f + Height(lbl) + 18f;
            }
            L("the money:", 10f, y + 10f, 32f);
            y += 64f;
            for (int i = 0; i < _st.Investors.Count; i++)
            {
                Investor d = _st.Investors[i];
                L(string.Format("{0} ({1})", d.Name, d.Archetype), 10f, y, 29f);
                string quote = string.Format("\"{0}\"  ·  {1}", d.Thesis, d.Trait);
                var lbl = L(quote, 30f, y + 38f, 25f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.65f), 1070f);
                y += 44f + Height(lbl) + 16f;
            }
        }

        /// `_wrap_h`, WHICH IS NOT preferredHeight. binder.gd advances the street by
        /// `_font.get_multiline_string_size(...).y`, and that number is a LEADING-FREE
        /// INTEGER SUM: FreeType hands Godot a whole-pixel ascent and a whole-pixel
        /// descent, every line box is worth exactly their sum, N lines are worth N of
        /// them, and the theme's 3px gap never enters because a raw Font call goes
        /// through no theme at all.
        ///
        /// TMP's preferredHeight is a different shape: `1.354 x size` of FRACTIONAL
        /// first line plus `N - 1` pitches that DO carry the leading. At 26px that is
        /// 35.2 + 40(N-1) against Godot's 37N — 1.8px SHORT on a one-line block, 1.2
        /// LONG on two, 4.2 long on three — and the street's `y` carries every one of
        /// those errors down the page, so the rivals and the money below them drifted
        /// further the more of them there were.
        static float Height(TextMeshProUGUI t)
        {
            if (t == null) return 0f;
            t.ForceMeshUpdate();
            // the lines TMP actually LAID OUT, so the advance can never disagree with
            // what is on the sheet even where its wrap point differs from Godot's
            int lines = Mathf.Max(1, t.textInfo.lineCount);
            return lines * GodotLineBox(t.font, t.fontSize);
        }

        /// One Godot line box in pixels — ceil(ascent) + ceil(descent) at that size.
        /// The rounding lives in DrawnUI.GodotLineSpacing, which hands the difference
        /// between Godot's box and TMP's over as hundredths of the size, so this adds
        /// TMP's own pitch back rather than keeping a second copy of the font ratios.
        /// StringLeading, not LabelLeading: `get_multiline_string_size` has no theme.
        static float GodotLineBox(TMP_FontAsset f, float size)
        {
            if (size <= 0f) return 0f;
            float tmp = size * 1.354f;                  // Patrick Hand's hhea, if asked
            if (f != null && f.faceInfo.pointSize > 0f)
            {
                float ps = f.faceInfo.pointSize;
                tmp = (f.faceInfo.lineHeight > 0f
                       ? f.faceInfo.lineHeight
                       : f.faceInfo.ascentLine - f.faceInfo.descentLine) / ps * size;
            }
            float godot = tmp + DrawnUI.GodotLineSpacing(f, size, DrawnUI.StringLeading)
                                * size * 0.01f;
            // two ceilings summed is a whole number of pixels; rounding here clears the
            // float dust the trip through hundredths-of-the-size leaves behind
            return Mathf.Max(Mathf.Round(godot), 1f);
        }

        // ── tab 8: threats & promises ──────────────────────────────────────────

        const int ClockSide = 30;         // the footprint the ⏰ had at 30px type

        void TabThreats()
        {
            L("threats & promises", 10f, 6f, 40f);
            float y = 80f;
            if (_st.Clocks.Count == 0 && _st.Statuses.Count == 0 && _st.Commitments.Count == 0)
                L("nothing ticking. that never lasts.", 10f, y, 30f,
                  DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f));
            for (int i = 0; i < _st.Clocks.Count; i++)
            {
                // THE CLOCK IS DRAWN, NOT TYPED. The original heads this line with ⏰,
                // a glyph the hand font has never carried; a drawn face is both the
                // truer style and the one thing that cannot come out a hollow box.
                DrawnChart.Mount(_content, "clock",
                    DrawnChart.Clock(ClockSide, DrawnUI.Coral, DrawnUI.Ink),
                    10f, y + 3f, ClockSide, ClockSide);
                L(string.Format("in {0} wks: {1}", _st.Clocks[i].WeeksLeft,
                    _st.Clocks[i].Consequence), 10f + ClockSide + 8f, y, 30f, DrawnUI.Coral);
                y += 52f;
            }
            for (int i = 0; i < _st.Statuses.Count; i++)
            {
                Status s = _st.Statuses[i];
                StatusDef def = SimEngine.StatusEffect(s.Name);
                bool buff = def != null && def.Kind == "buff";
                L(string.Format("{0} {1} — {2} wks left", buff ? "▲" : "▼",
                    (s.Name ?? "").Replace("_", " "), s.WeeksLeft), 10f, y, 30f,
                    buff ? DrawnUI.Sage : DrawnUI.Coral);
                y += 52f;
            }
            for (int i = 0; i < _st.Commitments.Count; i++)
            {
                Commitment c = _st.Commitments[i];
                L(string.Format("↻ {0}: ${1}/wk for {2} more wks", c.Name, c.CashWk, c.WeeksLeft),
                  10f, y, 30f, DrawnUI.Blue);
                y += 52f;
            }
        }
    }
}
