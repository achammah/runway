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
    /// ten pen-labelled tabs, doodle icons, charts drawn as wobbly polylines.
    ///
    /// FOG OF WAR: precision follows analytics_level (0-3). At 0 the customer page says
    /// "traffic seems decent"; invest in analytics — a writable move — and the pages
    /// sharpen. The dashboard you EARN is a mechanic, not a view.
    ///
    /// THE SHEET IS THE FRAME, THE DESKS ARE THE PAGES. Vitals, the ledger and threats
    /// are drawn here; every other tab body lives in its own file under Game/Desks/ and
    /// is handed this component (docs/design/HOOKS.md), so a subsystem grows its page
    /// without ever opening this file. Desks draw through the public hand below (L,
    /// InkWord, Spark, Content, State) and share the drawn components in DeskKit.cs.
    /// </summary>
    public sealed class BinderScreen : MonoBehaviour
    {
        /// TEN TABS AT PITCH 120 (00-spine section 10, DECISIONS #1): the sheet is 1240
        /// wide, 24 + 10x120 = 1224 fits, buttons are 118x44 and the longest label
        /// ("the street") measures about 110px at 23px in the hand. THE PEN RING AND
        /// THE BANGS READ THESE CONSTANTS TOO — the ring desynced from the button row
        /// twice when a pitch was re-typed somewhere else.
        static readonly string[] Tabs =
        {
            "vitals", "the ledger", "the bank", "pricing", "customers",
            "product", "crew", "cap table", "the street", "threats",
        };
        const float TabX0 = 24f;
        const float TabPitch = 120f;
        const float TabW = 118f;
        const float TabH = 44f;

        /// [budget key, the word on the page, what the money actually does]. The
        /// key and the word part company for the top lever: the state key
        /// migrated to `ads` when the four acquisition channels landed, while
        /// the founder still calls the whole top of the funnel MARKETING until
        /// the channels unlock at coworking.
        public static readonly int[] LeverSteps = { 0, 250, 500, 1000, 2000, 4000, 8000 };

        // THE PEN RING, from `_Clipboard._draw`: an ellipse centred on the tab's own
        // middle (TabX0 + tab*TabPitch + TabW/2, 76) with radii 62 and 26, wobbled ±2
        // and stroked 3.5 — the radius came down with the pitch when the bank made the
        // row ten tabs long. The sprite is the
        // ink's own box — its pad is the jitter plus half the stroke plus a margin —
        // and it mounts 1:1, so the ring is the size the hand drew and not a squashed
        // circle. A 60r circle stretched into a 130×52 cell measured 127×50 of ink
        // against Godot's ~143×59, at 607 coral pixels against 1321, and its stroke ran
        // 3.28 across but 1.31 top and bottom — a stretched circle holds no one width.
        const float RingRx = 62f;                       // 118-wide tabs, at pitch 120
        const float RingRy = 26f;
        const float RingPad = 6f;                       // ceil(2 + 3.5/2 + 2)
        const float RingW = RingRx * 2f + RingPad * 2f;   // 136
        const float RingH = RingRy * 2f + RingPad * 2f;   // 64
        const float RingTop = 76f - RingH * 0.5f;         // 44
        static float RingX(int tab)
        {
            return TabX0 + tab * TabPitch + TabW * 0.5f - RingW * 0.5f;
        }

        GameState _st;
        RectTransform _sheet;
        RectTransform _content;
        Image _tabRing;
        readonly Dictionary<string, TextMeshProUGUI> _bangs =
            new Dictionary<string, TextMeshProUGUI>();
        int _tab;

        /// DESK-LOCAL STATE, one visit long (10-interface-language section 4.8): a
        /// desk's page mode, its expanded row, its armed control. Never saved, cleared
        /// on every tab change, dead with this object — so reopening the binder is
        /// always a clean read of state and no half-finished act survives a close.
        public readonly Dictionary<string, object> Desk = new Dictionary<string, object>();

        public GameState State { get { return _st; } }
        public RectTransform Content { get { return _content; } }

        /// TAB AND B TOGGLE, THEY DO NOT FIGHT. Both the room and the binder listen for
        /// the same keys, so without this the room re-opened the binder in the very
        /// frame the binder dismissed itself.
        public static bool IsOpen { get; private set; }

        /// `onDesk` opens the binder ON a desk — the pre-roll review's "go fix it"
        /// arrives with the loudest attention row's own desk name, so the founder lands
        /// looking at the thing the world stopped them for.
        public static BinderScreen Open(GameState state, string onDesk = "")
        {
            if (IsOpen) return null;
            var boot = Boot.Instance;
            if (boot == null || state == null) return null;
            var rt = DrawnUI.FullRect(boot.OverlayLayer, "binder");
            var b = rt.gameObject.AddComponent<BinderScreen>();
            b._st = state;
            IsOpen = true;
            Runway.Audio.RunwayMix.SetState("binder");
            if (!string.IsNullOrEmpty(onDesk))
            {
                int i = System.Array.IndexOf(Tabs, onDesk);
                if (i >= 0) b._tab = i;
            }
            b.BuildParts();
            return b;
        }

        public void FocusDesk(string deskName)
        {
            int i = System.Array.IndexOf(Tabs, deskName);
            if (i < 0) return;
            Desk.Clear();
            _tab = i;
            Refresh();
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
                GameUi.InkWord(_sheet, Tabs[i], TabX0 + i * TabPitch, 54f, TabW, TabH, 23f,
                    DrawnUI.Ink, () =>
                    {
                        // a desk's page mode dies when you leave the page
                        if (_tab != idx) Desk.Clear();
                        _tab = idx;
                        Refresh();
                    });
                // THE WARNING BANGS (owner: "! warnings on tab where things
                // are unset"). EVERY tab carries one now: the engine's attention
                // registry decides which ones light up (00-spine section 4), so
                // a desk that grows a new warning needs no change here — it
                // files a registry row instead. The bang hangs on the tab's own
                // shoulder, DERIVED from the tab width, so the row and its marks
                // can never drift apart again.
                var bang = DrawnUI.HandLabel(_sheet, "!", TabX0 + i * TabPitch + TabW - 27f,
                    42f, 30f, DrawnUI.Coral, 30f);
                bang.gameObject.SetActive(false);
                _bangs[Tabs[i]] = bang;
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

        /// ESC POPS BEFORE IT CLOSES (10-interface-language section 4.2): inside a
        /// desk's state machine Esc walks DETAIL/WRITE/WAIT/REVIEW back to the list and
        /// disarms an armed control; only from a tab's base state does it shut the
        /// binder.
        void Update()
        {
            if (!_armed) { _armed = true; return; }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (DeskPop()) return;
                Dismiss();
                return;
            }
            // A DESK MAY OWN A WRITE FIELD (01's write-in): while one holds the
            // keyboard, a typed "b" is a letter, not a dismissal.
            var sel = UnityEngine.EventSystems.EventSystem.current != null
                ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;
            bool writing = sel != null && sel.GetComponent<TMPro.TMP_InputField>() != null;
            if (!writing && (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.B))) Dismiss();
        }

        /// One step back inside the current desk. True = something was popped, so the
        /// press is spent and the binder stays open.
        public bool DeskPop()
        {
            if (Desk.ContainsKey("armed")) { Desk.Remove("armed"); Refresh(); return true; }
            object mode;
            if (Desk.TryGetValue("mode", out mode) && mode != null && mode.ToString().Length > 0)
            {
                Desk["mode"] = "";
                Desk.Remove("row");
                Refresh();
                return true;
            }
            return false;
        }

        void Dismiss()
        {
            if (this != null && gameObject != null) Destroy(gameObject);
        }

        // ══ composition ════════════════════════════════════════════════════════

        public void Refresh()
        {
            // ONE list behind every mark on this sheet: a tab wears the bang of
            // its loudest attention item, and an alarm (3) is coral where a note
            // (1) is ink.
            var worst = new Dictionary<string, int>();
            foreach (AttentionItem it in SimEngine.AttentionItems(_st))
            {
                int had;
                worst.TryGetValue(it.Desk ?? "", out had);
                worst[it.Desk ?? ""] = Gd.Maxi(had, it.Severity);
            }
            foreach (KeyValuePair<string, TextMeshProUGUI> kv in _bangs)
            {
                int sev;
                worst.TryGetValue(kv.Key, out sev);
                kv.Value.gameObject.SetActive(sev > 0);
                kv.Value.color = sev == 1 ? DrawnUI.Ink : DrawnUI.Coral;
            }
            for (int i = _content.childCount - 1; i >= 0; i--)
                Destroy(_content.GetChild(i).gameObject);
            if (_tabRing != null)
                DrawnUI.SetTopLeft(_tabRing.rectTransform, RingX(_tab), RingTop);
            // THE DESK DISPATCH (docs/design/HOOKS.md): a tab a subsystem owns is drawn
            // by its own desk file, handed this component. Vitals, the ledger and
            // threats are the frame's own pages and stay here.
            switch (_tab)
            {
                case 0: TabVitals(); break;
                case 1: DeskLedger.Draw(this); break;
                case 2: DeskBank.Draw(this); break;
                case 3: DeskCatalog.Draw(this); break;
                case 4: DeskCustomers.Draw(this); break;
                case 5: DeskProduct.Draw(this); break;
                case 6: DeskCrew.Draw(this); break;
                case 7: DeskCap.Draw(this); break;
                case 8: DeskStreet.Draw(this); break;
                default: TabThreats(); break;
            }
        }

        /// THE PRESS ROUTER: a desk that prefers id-dispatch to closures registers its
        /// controls against an id and answers in its own Handle(). The tab rebuilds
        /// afterwards, so a handler only ever has to write state.
        public void DeskPress(string deskName, string id)
        {
            switch (deskName)
            {
                case "catalog": DeskCatalog.Handle(this, id); break;
                case "crew": DeskCrew.Handle(this, id); break;
                case "street": DeskStreet.Handle(this, id); break;
                case "customers": DeskCustomers.Handle(this, id); break;
                case "pipeline": DeskPipeline.Handle(this, id); break;
                case "bank": DeskBank.Handle(this, id); break;
                case "product": DeskProduct.Handle(this, id); break;
                case "factory": DeskFactory.Handle(this, id); break;
                case "cap": DeskCap.Handle(this, id); break;
            }
            Refresh();
        }

        // ── what a desk may touch ──────────────────────────────────────────────
        // The public half of this component: the drawing hand every desk file and the
        // shared component kit draw through.

        public TextMeshProUGUI L(string text, float x, float y, float size = 30f,
                                 Color? col = null, float w = 1100f)
        {
            return DrawnUI.HandLabel(_content, text, x, y, size, col ?? DrawnUI.Ink, w,
                                     TextAlignmentOptions.TopLeft);
        }

        public void Icon(string name, float x, float y, float side = 72f)
        {
            string p = ArtCache.IconPath(name);
            if (!RunwayPaths.ArtExists(p)) return;
            GameUi.Picture(_content, "icon", p, x, y, side, side);
        }

        public List<float> Series(string key)
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
        public void Spark(string key, float x, float y, float w, float h, Color col)
        {
            DrawnChart.MountSpark(_content, Series(key), col, x, y, w, h);
        }

        /// A vessel with a level — product's tech debt, and any "how full is it" read.
        /// draw_rect(rect, INK, false, t) is a rect OUTLINE: four bars whose centre
        /// lines are the rect's own edges, which is where Godot puts a thick unfilled
        /// rect. Without the outline the level floats and the jar is not a jar.
        public void JarEdge(float x, float y, float w, float h, float t)
        {
            float half = t * 0.5f;
            DrawnUI.Fill(_content, "jaredge", DrawnUI.Ink, x - half, y - half, w + t, t);
            DrawnUI.Fill(_content, "jaredge", DrawnUI.Ink, x - half, y + h - half, w + t, t);
            DrawnUI.Fill(_content, "jaredge", DrawnUI.Ink, x - half, y - half, t, h + t);
            DrawnUI.Fill(_content, "jaredge", DrawnUI.Ink, x + w - half, y - half, t, h + t);
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
            // ONE HONEST DEBT FIGURE across shark, bank and venture notes (06
            // section 9): the single LoanPrincipal field stopped being the whole
            // story the week the structured notes landed.
            int debtOwed = SimBank.DebtTotal(_st);
            int noteCount = _st.Loans.Count + (_st.LoanPrincipal > 0 ? 1 : 0);
            L(string.Format("burn: rent ${0} · payroll ${1} · marketing ${2}{3}",
                GameUi.Money(rent), GameUi.Money(payroll),
                GameUi.Money(_st.LastPnl != null ? _st.LastPnl.Marketing
                             : Gd.ToInt(SimFunnel.SpendTotal(_st))),
                debtOwed > 0
                    ? "  ·  DEBT $" + GameUi.Money(debtOwed) + " across " + noteCount
                      + " notes (worst " + Gd.RoundToInt(SimBank.WorstRate(_st) * 100.0) + "%/wk)"
                    : ""),
                10f, 432f, 27f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.8f));
            L("valuation, if anyone asked: $" + GameUi.Money(SimEngine.Valuation(_st)), 10f, 486f);
            // THE PRICE LINE OWNS 532–566 AT 27px, so the hype caption cannot start
            // at 556: it was written over the line above it and its own spark's wash
            // was drawn over it in turn. 574 clears both, and the spark still lands
            // inside the 760 pane.
            L(string.Format("price ×{0:0.00}  ·  the market is {1}", _st.PriceMult,
                _st.MarketTrend > 1.05 ? "warm" : (_st.MarketTrend < 0.95 ? "cold" : "even")),
                10f, 532f, 27f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.8f));
            // the hype chart moved here when the roadmap took the product sheet (07)
            L("hype:", 10f, 574f, 24f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f));
            Spark("hype", 10f, 606f, 1120f, 120f, DrawnUI.Yellow);
        }

        // ── tab 1: THE LEDGER — the levers, the math, the truth about the money ──


        /// SimEngine parks the week's unit economics on the state as ONE nested map.
        /// Fresh from the tick it is a Dictionary; loaded back off disk Newtonsoft
        /// hands it over as a JObject — so this reads both and answers 0 for neither.
        public double UnitEcon(string key)
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

        public int Budget(string cat)
        {
            switch (cat)
            {
                case "ads": return _st.Budgets.Ads;
                case "content": return _st.Budgets.Content;
                case "referrals": return _st.Budgets.Referrals;
                case "outbound": return _st.Budgets.Outbound;
                case "sales": return _st.Budgets.Sales;
                case "care": return _st.Budgets.Care;
                case "rnd": return _st.Budgets.Rnd;
                case "office": return _st.Budgets.Office;
            }
            return 0;
        }

        public void SetBudget(string cat, int v)
        {
            switch (cat)
            {
                case "ads": _st.Budgets.Ads = v; break;
                case "content": _st.Budgets.Content = v; break;
                case "referrals": _st.Budgets.Referrals = v; break;
                case "outbound": _st.Budgets.Outbound = v; break;
                case "sales": _st.Budgets.Sales = v; break;
                case "care": _st.Budgets.Care = v; break;
                case "rnd": _st.Budgets.Rnd = v; break;
                case "office": _st.Budgets.Office = v; break;
            }
        }

        public int Step(int cur, int dir)
        {
            int idx = 0;
            for (int i = 0; i < LeverSteps.Length; i++) if (LeverSteps[i] <= cur) idx = i;
            idx = Gd.Clampi(idx + dir, 0, LeverSteps.Length - 1);
            return Gd.Mini(LeverSteps[idx], SimEngine.EraSpendCap(_st.Era));
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
        public static float Height(TextMeshProUGUI t)
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

        // ── tab 9: threats & promises ──────────────────────────────────────────

        const int ClockSide = 30;         // the footprint the ⏰ had at 30px type

        void TabThreats()
        {
            L("threats & promises", 10f, 6f, 40f);
            float y = 80f;
            // WHAT NEEDS A HAND, in one place (00-spine sections 4 and 11):
            // every attention item at warn or above, loudest first. This is the
            // same list the tab bangs, the garage badge and the pre-roll review
            // read — so a desk that is shouting can never be shouting only
            // somewhere the player is not looking.
            List<AttentionItem> wants = SimEngine.PrerollItems(_st);
            if (wants.Count > 0)
            {
                int shown = 0;
                foreach (AttentionItem it in wants)
                {
                    if (shown >= 12)
                    {
                        L(string.Format("+{0} more — the desks have the details", wants.Count - shown),
                          10f, y, 26f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f));
                        y += 44f;
                        break;
                    }
                    L(string.Format("! {0}  ·  {1}", it.Label, it.Desk), 10f, y, 28f,
                      it.Severity >= 3 ? DrawnUI.Coral : DrawnUI.WithAlpha(DrawnUI.Ink, 0.85f));
                    y += 44f;
                    shown += 1;
                }
                y += 12f;
            }
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
                // THE WORD IS THE MARK. ▲/▼/↻ are all absent from the hand and only
                // render at all through the borrowed face; the word says the same
                // thing in the same ink on every machine, and §3.3 asks for it anyway
                // — read the page in grey and every state is still there.
                L(string.Format("{0} {1} — {2} wks left", buff ? "helping:" : "hurting:",
                    (s.Name ?? "").Replace("_", " "), s.WeeksLeft), 10f, y, 30f,
                    buff ? DrawnUI.Sage : DrawnUI.Coral);
                y += 52f;
            }
            for (int i = 0; i < _st.Commitments.Count; i++)
            {
                Commitment c = _st.Commitments[i];
                L(string.Format("standing: {0} — ${1}/wk for {2} more wks",
                    c.Name, c.CashWk, c.WeeksLeft), 10f, y, 30f, DrawnUI.Blue);
                y += 52f;
            }
            // THE PAGE STATES ITS OWN LAW, like every desk does (2.7). Reading order
            // ends on the lesson: this sheet is the overflow, and the thing it has to
            // teach is that these rows are ranked and that the desks hold the
            // controls.
            L("the rules of this page: everything the company is shouting about, loudest first · "
              + "a CLOCK fires on its week · a CONDITION expires on its own · a STANDING cost bills "
              + "until it runs out · nothing is fixed here, and every row names the desk that owns it",
              10f, 734f, 21f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 1100f);
        }
    }
}
