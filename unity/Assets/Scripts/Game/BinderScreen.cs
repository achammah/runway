using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;
using Runway.Core;
using Runway.Llm;

namespace Runway.Game
{
    /// <summary>
    /// THE RING BINDER — binder.gd, ported (DECISIONS § "Binder rework — owner
    /// picks", mockups/00 pick A). A kraft cover, drawn rings, a side rail of
    /// divider groups with colored index tabs poking left, and the open group
    /// fanning its pages.
    ///
    /// THE FRAME OWNS: the binder body, the rail, the alarm-red climb, the
    /// group overviews, the first-open tour, the momentary tab slot and the
    /// diegetic binder object. IT OWNS NO PAGE: every desk lives in its own
    /// file under Game/Desks/ and is handed this component.
    ///
    /// ESC CONTRACT: Esc pops desk states (armed → mode), then the overview,
    /// then closes the binder; TAB/B always close. The tour eats Esc as skip.
    /// </summary>
    public sealed class BinderScreen : MonoBehaviour
    {
        // ── the taxonomy (DECISIONS: 18 desks in 4 groups) ─────────────────
        public static readonly string[] GroupNames =
            { "REVENUE", "COSTS", "THE COMPANY", "THE LOG" };
        static readonly Color[] GroupCols =
            { DrawnUI.Sage, DrawnUI.Coral, DrawnUI.Blue, DrawnUI.Yellow };
        public static readonly string[][] GroupDesks =
        {
            new[] { "offers", "customers", "in motion", "growth" },
            new[] { "spend", "team", "recruitment", "bills", "the bank", "the works" },
            new[] { "what we make", "cap table", "the raise", "the street", "threats", "pivot" },
            new[] { "this week", "history", "events" },
        };

        /// THE OLD TEN TABS, kept as the LEGACY ORDER: the attention registry,
        /// the room's focus calls and the shot harnesses still speak these
        /// names, and `_tab` (poked by the harness) still indexes this list.
        static readonly string[] Tabs =
        {
            "vitals", "the ledger", "the bank", "pricing", "customers",
            "product", "crew", "cap table", "the street", "threats",
        };
        /// <summary>Old attention desk word → the 19-desk taxonomy id.</summary>
        public static string DeskAlias(string deskName)
        {
            string id;
            return LegacyToDesk.TryGetValue(deskName ?? "", out id) ? id : deskName;
        }

        static readonly Dictionary<string, string> LegacyToDesk =
            new Dictionary<string, string>
        {
            { "vitals", "this week" }, { "the ledger", "spend" },
            { "the bank", "the bank" }, { "pricing", "offers" },
            { "customers", "customers" }, { "product", "what we make" },
            { "crew", "team" }, { "cap table", "cap table" },
            { "the street", "the street" }, { "threats", "threats" },
            { "pipeline", "in motion" }, { "factory", "the works" },
            { "catalog", "offers" }, { "bank", "the bank" }, { "cap", "cap table" },
            { "street", "the street" }, { "ledger", "spend" },
        };

        // ── the frame geometry (mockups/00 A, in the 1536x1024 view) ───────
        const float FrameX = 16f, FrameY = 28f, FrameW = 1504f, FrameH = 968f;
        const float CoverW = 54f, RingW = 46f, StackX = 100f;
        const float RailX = 124f, RailBoxW = 182f, SheetRuleX = 318f;
        const float ContentX = 344f, ContentY = 36f, ContentW = 1160f, ContentH = 880f;

        /// THE BINDER PORTRAIT (DECISIONS, owner-corrected): a diegetic object
        /// on the room painting, bottom-left, replacing the doorway button.
        public const string PortraitFile = "binder_portrait.png";
        public static readonly Rect LabelRect = new Rect(0.26f, 0.42f, 0.48f, 0.13f);
        public const float LabelFontSize = 46f;
        public const float LabelMinPx = 10f;
        const string TourFlagFile = "seen_binder_tour.unity";

        public static readonly int[] LeverSteps = { 0, 250, 500, 1000, 2000, 4000, 8000 };

        GameState _st;
        RectTransform _frame;
        RectTransform _rail;
        RectTransform _content;
        int _openGroup = 3;                 // THE LOG opens first
        string _page = "this week";
        int _overview = -1;
        int _tourStep = -1;
        bool _tourDemoRed;
        readonly List<string[]> _momentary = new List<string[]>(); // {id, group, label, wks}

        /// LEGACY SHIM: the shot harness pokes `_tab` by reflection and calls
        /// Refresh — the shim maps it onto the new navigation.
        int _tab = -1;
        int _legacyApplied = -1;
        public bool TourEnabled = true;

        /// DESK-LOCAL STATE, one visit long — never saved, cleared on every
        /// page change, dead with this object.
        public readonly Dictionary<string, object> Desk = new Dictionary<string, object>();

        public GameState State { get { return _st; } }
        public RectTransform Content { get { return _content; } }
        public static bool IsOpen { get; private set; }

        // ── THE UX SPINE (13-binder-ux, DAG3) — the binder half of the nine
        // systems, twin of binder.gd's block.
        /// THE CONTROL REGISTRY (S2b): desks register rects while they draw;
        /// cleared at every refresh; FocusDesk spotlights a registered rect.
        readonly Dictionary<string, Rect> _controls = new Dictionary<string, Rect>();
        /// THE DO LANE's focus (S3/S7): the action ENTER presses; TAB cycles
        /// it while a lane is on the page (TAB still closes a lane-less page).
        int _doFocus;
        readonly List<Button> _doButtons = new List<Button>();
        /// THE BACK STACK (S7): cross-desk jumps push {fromDesk, label}; the
        /// pill at the rail's foot (or BACKSPACE) pops.
        readonly List<string[]> _backStack = new List<string[]>();
        string _crumb = "";
        RectTransform _popover;
        RectTransform _spot;
        /// THE SEEN STORE (S5): per-run last-shown values, beside the mail
        /// read-marks (binder_seen_<seed>.json); flushed on destroy.
        bool _seenLoaded;
        Dictionary<string, string> _seenStore = new Dictionary<string, string>();
        readonly Dictionary<string, string> _seenPending = new Dictionary<string, string>();

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
            b.BuildParts();
            if (!string.IsNullOrEmpty(onDesk)) b.FocusDesk(onDesk);
            return b;
        }

        /// Open the binder ON a desk — old names and new names both land.
        /// S2b — THE JUMP GETS TEETH: pass a controlId a desk registered
        /// during its draw and the landing spotlights that control. S7 — pass
        /// `source` and a "back to" pill waits at the rail's foot.
        public void FocusDesk(string deskName, string controlId = "", string source = "")
        {
            string id;
            if (!LegacyToDesk.TryGetValue(deskName ?? "", out id)) id = deskName;
            if (FindGroup(id) < 0) return;
            if (!string.IsNullOrEmpty(source))
            {
                string src;
                if (!LegacyToDesk.TryGetValue(source, out src)) src = source;
                if (src != id && FindGroup(src) >= 0)
                {
                    _backStack.Add(new[] { src, src });
                    while (_backStack.Count > 8) _backStack.RemoveAt(0);
                }
            }
            Desk.Clear();
            OpenPage(id);
            if (!string.IsNullOrEmpty(controlId) && _controls.ContainsKey(controlId))
                Spotlight(_controls[controlId]);
        }

        /// <summary>THE ONE JUMP EVERY RED ROW SHARES (S2): the pre-roll
        /// review and the threats page hand over the attention row itself;
        /// the desk word translates, the row's Control key lands spotlit.</summary>
        public void JumpToAsk(AttentionItem row, string source = "")
        {
            if (row == null) return;
            FocusDesk(row.Desk ?? "", row.Control ?? "", source);
        }

        void BackPop()
        {
            if (_backStack.Count == 0) return;
            string[] top = _backStack[_backStack.Count - 1];
            _backStack.RemoveAt(_backStack.Count - 1);
            if (FindGroup(top[0]) < 0) { Refresh(); return; }
            Desk.Clear();
            OpenPage(top[0]);
        }

        void OnDestroy()
        {
            SeenFlush();
            IsOpen = false;
            Runway.Audio.RunwayMix.SetState("normal");
        }

        void BuildParts()
        {
            var rt = GetComponent<RectTransform>();
            GameUi.Scrim(rt, new Color(0.05f, 0.05f, 0.06f, 0.55f), Dismiss);

            _frame = DrawnUI.Rect(rt, "frame", FrameX, FrameY, FrameW, FrameH);
            // the thrown shadow, the paper stack, the kraft cover, the ringbar
            DrawnUI.Fill(_frame, "shadow", new Color(0f, 0f, 0f, 0.25f), 10f, 14f,
                         FrameW, FrameH).raycastTarget = false;
            DrawnUI.Fill(_frame, "stack", DrawnUI.Cream, StackX, 0f, FrameW - StackX,
                         FrameH);
            DrawnUI.Fill(_frame, "cover", DeskKit.Kraft, 0f, 0f, CoverW, FrameH);
            DrawnUI.Fill(_frame, "ringbar", DeskKit.Kraft2, CoverW, 0f, RingW, FrameH);
            for (int r = 0; r < 3; r++)
            {
                float cy = FrameH * (0.25f + 0.25f * r);
                DrawnChart.Mount(_frame, "ring" + r,
                    DrawnChart.PenEllipse(17f, 17f, 3.4f, 0.6f, 7 + r, DrawnUI.Ink),
                    CoverW + RingW * 0.5f - 21f, cy - 21f, 42f, 42f);
                DrawnChart.Mount(_frame, "ring2" + r,
                    DrawnChart.PenEllipse(11f, 11f, 2.6f, 0.3f, 11 + r,
                                          DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f)),
                    CoverW + RingW * 0.5f - 14f, cy - 14f, 28f, 28f);
            }
            // the cover's rotated sticker
            var sticker = DrawnUI.Fill(_frame, "sticker", DeskKit.Paper2,
                                       CoverW * 0.5f - 14f, 56f, 28f, 128f);
            sticker.raycastTarget = false;
            var stickerLab = DrawnUI.HandLabel(_frame, "the binder", CoverW * 0.5f - 64f,
                                               106f, 17f, DrawnUI.Ink, 128f,
                                               TextAlignmentOptions.Center);
            stickerLab.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);
            // the punched margin (dashes) + the sheet's left rule
            for (float dy = 12f; dy < FrameH - 12f; dy += 16f)
                DrawnUI.Fill(_frame, "punch", DrawnUI.WithAlpha(DrawnUI.Ink, 0.25f),
                             StackX + 7f, dy, 2f, 9f).raycastTarget = false;
            DrawnUI.Fill(_frame, "sheetrule", DrawnUI.WithAlpha(DrawnUI.Ink, 0.25f),
                         SheetRuleX, 14f, 2.4f, FrameH - 28f).raycastTarget = false;
            DrawnUI.AddInkEdge(_frame, new Vector2(FrameW, FrameH), new DrawnUI.PaperStyle
            {
                ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 3f,
                StepsPerEdge = 20, Jitter = 2f, Thickness = 4f, Seed = 7,
            });

            _rail = DrawnUI.Rect(_frame, "rail", 0f, 0f, FrameW, FrameH);
            _content = DrawnUI.Rect(_frame, "content", ContentX, ContentY, ContentW,
                                    ContentH);
            GameUi.InkWord(_frame, "×", FrameW - 60f, 2f, 52f, 52f, 46f, DrawnUI.Coral,
                           Dismiss);

            var g = DrawnUI.Group(rt);
            g.alpha = 0f;
            StartCoroutine(DrawnUI.FadeTo(g, 1f, 0.2f));

            // the first open of an install: the tour
            if (TourEnabled && _tourStep < 0 && !TourSeen() && _legacyApplied < 0)
            {
                _tourStep = 0;
                TourApply();
            }
            Refresh();
        }

        bool _armed;

        /// ESC POPS BEFORE IT CLOSES: the tour eats Esc as skip; then an open
        /// receipt popover (S4 — one more level), then armed → mode, then the
        /// overview, then the binder. B always closes; TAB closes too UNLESS
        /// the page carries a DO lane — there TAB cycles the lane's focus
        /// (S7). 1-4 open groups, ←/→ walk pages, ENTER presses the focused
        /// DO action, BACKSPACE pops the back stack.
        void Update()
        {
            if (!_armed) { _armed = true; return; }
            var sel = UnityEngine.EventSystems.EventSystem.current != null
                ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;
            bool writing = sel != null && sel.GetComponent<TMP_InputField>() != null;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_tourStep >= 0) { TourFinish(); return; }
                if (_popover != null) { ClosePopover(); return; }
                if (DeskPop()) return;
                if (_overview >= 0) { _overview = -1; Refresh(); return; }
                Dismiss();
                return;
            }
            if (!writing && Input.GetKeyDown(KeyCode.Tab))
            {
                if (_tourStep >= 0) { TourFinish(); Dismiss(); return; }
                if (_doButtons.Count > 0)
                {
                    _doFocus = (_doFocus + 1) % _doButtons.Count;
                    Refresh();
                    return;
                }
                Dismiss();
                return;
            }
            if (!writing && Input.GetKeyDown(KeyCode.B))
            {
                if (_tourStep >= 0) TourFinish();
                Dismiss();
                return;
            }
            if (_tourStep >= 0 || writing) return;
            for (int k = 0; k < 4; k++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + k)) { PressGroup(k); return; }
            if (Input.GetKeyDown(KeyCode.LeftArrow)) { WalkPage(-1); return; }
            if (Input.GetKeyDown(KeyCode.RightArrow)) { WalkPage(1); return; }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (_doButtons.Count > 0)
                {
                    Button btn = _doButtons[Gd.Clampi(_doFocus, 0, _doButtons.Count - 1)];
                    if (btn != null && btn.interactable) btn.onClick.Invoke();
                }
                return;
            }
            if (Input.GetKeyDown(KeyCode.Backspace) && _backStack.Count > 0) BackPop();
        }

        /// ←/→ walk the open group's fan, momentary tabs included, wrapping.
        void WalkPage(int dir)
        {
            var pages = new List<string>(GroupDesks[_openGroup]);
            for (int i = 0; i < _momentary.Count; i++)
                if (int.Parse(_momentary[i][1]) == _openGroup) pages.Add(_momentary[i][0]);
            if (pages.Count == 0) return;
            int idx = pages.IndexOf(_page);
            idx = idx >= 0 ? (idx + dir + pages.Count) % pages.Count : 0;
            OpenPage(pages[idx]);
        }

        /// One step back inside the current desk. The arrange shell lives in
        /// Desk["mode"], so Esc abandoning a staged change is this same pop.
        public bool DeskPop()
        {
            if (Desk.ContainsKey("armed")) { Desk.Remove("armed"); Refresh(); return true; }
            object mode;
            if (Desk.TryGetValue("mode", out mode) && mode != null
                && mode.ToString().Length > 0)
            {
                Desk["mode"] = "";
                Desk.Remove("row");
                Desk.Remove("chip");
                Desk.Remove("staged");
                Refresh();
                return true;
            }
            return false;
        }

        void Dismiss()
        {
            if (this != null && gameObject != null) Destroy(gameObject);
        }

        // ── navigation ─────────────────────────────────────────────────────

        public void OpenPage(string id)
        {
            int gi = FindGroup(id);
            if (gi < 0) return;
            if (_page != id) { Desk.Clear(); _doFocus = 0; }
            _overview = -1;
            _openGroup = gi;
            _page = id;
            Refresh();
        }

        /// The divider-header press: closed → the group opens; open → the
        /// group overview (THE DASHBOARD QUARTET).
        public void PressGroup(int gi)
        {
            if (_tourStep >= 0) return;
            if (gi == _openGroup)
            {
                _overview = _overview == gi ? -1 : gi;
                Refresh();
                return;
            }
            _openGroup = gi;
            _overview = -1;
            _page = GroupDesks[gi].Length > 0 ? GroupDesks[gi][0] : _page;
            Desk.Clear();
            Refresh();
        }

        int FindGroup(string id)
        {
            for (int gi = 0; gi < GroupDesks.Length; gi++)
                if (Array.IndexOf(GroupDesks[gi], id) >= 0) return gi;
            for (int i = 0; i < _momentary.Count; i++)
                if (_momentary[i][0] == id) return int.Parse(_momentary[i][1]);
            return -1;
        }

        // ── momentary tabs ─────────────────────────────────────────────────

        /// THE MOMENTARY TAB SLOT: a gold page tab any desk can summon into a
        /// group; it folds away when resolved.
        public void SummonMomentary(string id, string groupName, string label, int wks)
        {
            for (int i = 0; i < _momentary.Count; i++)
                if (_momentary[i][0] == id)
                {
                    _momentary[i][2] = label;
                    _momentary[i][3] = wks.ToString();
                    return;
                }
            int gi = Array.IndexOf(GroupNames, groupName);
            if (gi < 0) gi = 2;
            _momentary.Add(new[] { id, gi.ToString(), label, wks.ToString() });
            if (_content != null) Refresh();
        }

        public void ResolveMomentary(string id)
        {
            for (int i = _momentary.Count - 1; i >= 0; i--)
                if (_momentary[i][0] == id) _momentary.RemoveAt(i);
            if (_page == id)
            {
                _page = GroupDesks[_openGroup][0];
                Desk.Clear();
            }
            if (_content != null) Refresh();
        }

        public void DebugSummonOffer()
        {
            SummonMomentary("the offer", "THE COMPANY", "THE OFFER", 3);
        }

        // ── composition ────────────────────────────────────────────────────

        public void Refresh()
        {
            // LEGACY SHIM: a harness that poked `_tab` gets the old tab's new
            // desk, with the Desk dict it seeded left exactly as found.
            if (_tab >= 0 && _tab < Tabs.Length)
            {
                string want;
                if (!LegacyToDesk.TryGetValue(Tabs[_tab], out want)) want = "this week";
                if (_tab != _legacyApplied || want != _page)
                {
                    _overview = -1;
                    _tourStep = -1;
                    int gi = FindGroup(want);
                    if (gi >= 0) { _openGroup = gi; _page = want; }
                }
                _legacyApplied = _tab;
            }
            if (_st != null)
            {
                if (_st.BuyoutOffer != null && _st.BuyoutOffer.Count > 0)
                {
                    object ew;
                    int wks = _st.BuyoutOffer.TryGetValue("expires_wk", out ew) && ew != null
                        ? Gd.Maxi(Convert.ToInt32(ew) - _st.Week, 0) : 0;
                    SummonMomentary("the offer", "THE COMPANY", "THE OFFER", wks);
                }
                else
                {
                    bool present = false;
                    for (int i = 0; i < _momentary.Count; i++)
                        if (_momentary[i][0] == "the offer") present = true;
                    if (present) ResolveMomentary("the offer");
                }
            }
            for (int i = _content.childCount - 1; i >= 0; i--)
                Destroy(_content.GetChild(i).gameObject);
            for (int i = _rail.childCount - 1; i >= 0; i--)
                Destroy(_rail.GetChild(i).gameObject);
            // the per-draw registries die with the sheet they described
            _controls.Clear();
            _doButtons.Clear();
            _crumb = "";
            _popover = null;
            _spot = null;
            BuildRail();
            if (_tourStep >= 0) { DeskTour.Draw(this, _tourStep); return; }
            if (_overview >= 0) { DeskOverview.Draw(this, _overview); return; }
            Dispatch(_page);
        }

        void Dispatch(string id)
        {
            switch (id)
            {
                case "offers": DeskOffers.Draw(this); break;
                case "customers": DeskCustomersPage.Draw(this); break;
                case "in motion": DeskInMotion.Draw(this); break;
                case "growth": DeskGrowth.Draw(this); break;
                case "spend": DeskSpend.Draw(this); break;
                case "team": DeskTeam.Draw(this); break;
                case "recruitment": DeskRecruit.Draw(this); break;
                case "bills": DeskBills.Draw(this); break;
                case "the bank": DeskBankPage.Draw(this); break;
                case "the works": DeskWorks.Draw(this); break;
                case "what we make": DeskMake.Draw(this); break;
                case "cap table": DeskCapPage.Draw(this); break;
                case "the raise": DeskRaise.Draw(this); break;
                case "the street": DeskStreetPage.Draw(this); break;
                case "threats": DeskThreatsPage.Draw(this); break;
                case "pivot": DeskPivot.Draw(this); break;
                case "history": DeskHistory.Draw(this); break;
                case "events": DeskEvents.Draw(this); break;
                case "the offer": DeskOffer.Draw(this); break;
                default: DeskThisWeek.Draw(this); break;
            }
        }

        /// THE PRESS ROUTER — old names keep working; new pages answer under
        /// their own names.
        public void DeskPress(string deskName, string id)
        {
            switch (deskName)
            {
                case "vitals": DeskVitals.Handle(this, id); break;
                case "threats": DeskThreats.Handle(this, id); break;
                case "catalog": DeskCatalog.Handle(this, id); break;
                case "crew": DeskCrew.Handle(this, id); break;
                case "street": DeskStreet.Handle(this, id); break;
                case "customers": DeskCustomers.Handle(this, id); break;
                case "pipeline": DeskPipeline.Handle(this, id); break;
                case "bank": DeskBank.Handle(this, id); break;
                case "product": DeskProduct.Handle(this, id); break;
                case "factory": DeskFactory.Handle(this, id); break;
                case "cap": DeskCap.Handle(this, id); break;
                case "works": DeskWorks.Handle(this, id); break;
                case "arrange": DeskArrange.Handle(this, id); break;
                case "offer": DeskOffer.Handle(this, id); break;
            }
            Refresh();
        }

        // ── the rail ───────────────────────────────────────────────────────

        /// The severity every desk wears — the engine's attention registry,
        /// its old desk words aliased onto the new pages.
        public Dictionary<string, int> DeskSeverities()
        {
            var worst = new Dictionary<string, int>();
            foreach (AttentionItem it in SimEngine.AttentionItems(_st))
            {
                string id;
                if (!LegacyToDesk.TryGetValue(it.Desk ?? "", out id)) id = it.Desk ?? "";
                int had;
                worst.TryGetValue(id, out had);
                worst[id] = Gd.Maxi(had, it.Severity);
            }
            if (_tourDemoRed) worst["threats"] = 3;
            return worst;
        }

        void BuildRail()
        {
            Dictionary<string, int> sev = DeskSeverities();
            float y = 24f;
            for (int gi = 0; gi < GroupNames.Length; gi++)
            {
                string[] desks = GroupDesks[gi];
                var moms = new List<string[]>();
                for (int i = 0; i < _momentary.Count; i++)
                    if (int.Parse(_momentary[i][1]) == gi) moms.Add(_momentary[i]);
                bool open = gi == _openGroup;
                int gSev = 0;
                for (int i = 0; i < desks.Length; i++)
                {
                    int s;
                    sev.TryGetValue(desks[i], out s);
                    gSev = Gd.Maxi(gSev, s);
                }
                float boxH = open ? 52f + (desks.Length + moms.Count) * 40f + 12f : 48f;
                // closed: the kraft stack shadow under the card
                if (!open)
                {
                    DrawnUI.Fill(_rail, "dstack", DeskKit.Kraft2, RailX + 2f, y + boxH - 2f,
                                 RailBoxW - 4f, 4f).raycastTarget = false;
                    DrawnUI.Fill(_rail, "dstack2", new Color(0f, 0f, 0f, 0.35f), RailX + 4f,
                                 y + boxH + 1f, RailBoxW - 8f, 4f).raycastTarget = false;
                }
                var body = DrawnUI.Fill(_rail, "divider", open ? DrawnUI.Cream : DeskKit.Kraft,
                                        RailX, y, RailBoxW, boxH);
                // the index tab, poking left — ALERT climbs onto it when the
                // group carries attention (the pulse is the QA wave's polish)
                Color tabCol = gSev > 0 ? DeskKit.Alert : GroupCols[gi];
                var tab = DrawnUI.Fill(_rail, "gtab", tabCol, RailX - 16f, y + 4f, 15f,
                                       boxH - 8f);
                tab.raycastTarget = false;
                DrawnUI.AddInkEdge(tab.rectTransform, new Vector2(15f, boxH - 8f),
                    new DrawnUI.PaperStyle
                    {
                        ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                        StepsPerEdge = 5, Jitter = 0.7f, Thickness = 2.6f, Seed = 11 + gi,
                    });
                DrawnUI.AddInkEdge(body.rectTransform, new Vector2(RailBoxW, boxH),
                    new DrawnUI.PaperStyle
                    {
                        ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                        StepsPerEdge = 10, Jitter = 1f, Thickness = 2.6f, Seed = 13 + gi,
                    });
                int gidx = gi;
                GameUi.InkWord(_rail, "", RailX, y, RailBoxW, 48f, 19f, DrawnUI.Ink,
                               () => PressGroup(gidx));
                DrawnUI.HandLabel(_rail, GroupNames[gi], RailX + 12f, y + 10f, 19f,
                                  DrawnUI.Ink, RailBoxW - 24f);
                var cnt = DrawnUI.HandLabel(_rail, GroupCount(gi), RailX + RailBoxW - 66f,
                                            y + 14f, 15f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f),
                                            58f, TextAlignmentOptions.TopRight);
                cnt.raycastTarget = false;
                // THE LOG's corner is a COUNT badge instead (13-binder §
                // events): unread ACTION letters only — the desk provides
                // the number.
                if (gi == 3 && !open)
                {
                    int nAct = DeskEvents.UnreadActionCount(_st);
                    if (nAct > 0)
                        DeskKit.CountBadge(_rail, RailX + RailBoxW - 26f, y - 6f, nAct);
                }
                else if (!open && gSev > 0)
                {
                    // the red bang chip on the closed divider header
                    var chip = DrawnUI.Fill(_rail, "bang", DeskKit.Alert,
                                            RailX + RailBoxW - 26f, y - 6f, 22f, 22f);
                    chip.raycastTarget = false;
                    DrawnUI.AddInkEdge(chip.rectTransform, new Vector2(22f, 22f),
                        new DrawnUI.PaperStyle
                        {
                            ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                            StepsPerEdge = 5, Jitter = 0.6f, Thickness = 2.2f, Seed = 17,
                        });
                    DrawnUI.DisplayLabel(_rail, "!", RailX + RailBoxW - 26f, y - 6f, 15f,
                                         Color.white, 22f, TextAlignmentOptions.Center);
                }
                if (open)
                {
                    DrawnUI.Fill(_rail, "drule", DrawnUI.WithAlpha(DrawnUI.Ink, 0.25f),
                                 RailX + 8f, y + 46f, RailBoxW - 16f, 2f).raycastTarget = false;
                    float py = y + 52f;
                    for (int i = 0; i < desks.Length; i++)
                    {
                        int s;
                        sev.TryGetValue(desks[i], out s);
                        PageTab(desks[i], py, s, false, "");
                        py += 40f;
                    }
                    for (int i = 0; i < moms.Count; i++)
                    {
                        PageTab(moms[i][0], py, 0, true, moms[i][3] + " wks");
                        py += 40f;
                    }
                }
                y += boxH + 12f;
            }
            // S7 — the back pill at the rail's foot: the way home from a
            // cross-desk jump, pressable, twinned with BACKSPACE.
            if (_backStack.Count > 0)
            {
                string[] top = _backStack[_backStack.Count - 1];
                float pillY = Mathf.Min(y + 2f, FrameH - 64f);
                DrawnUI.Fill(_rail, "backpillsh", new Color(0f, 0f, 0f, 0.16f),
                    RailX + 2f, pillY + 3f, RailBoxW, 40f).raycastTarget = false;
                var pill = DrawnUI.Fill(_rail, "backpill", DeskKit.Paper2, RailX, pillY,
                                        RailBoxW, 40f);
                pill.raycastTarget = false;
                DrawnUI.AddInkEdge(pill.rectTransform, new Vector2(RailBoxW, 40f),
                    new DrawnUI.PaperStyle
                    {
                        ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                        StepsPerEdge = 9, Jitter = 1f, Thickness = 2.4f, Seed = 101,
                    });
                var tri = DrawnUI.Fill(_rail, "backtri", DrawnUI.Ink, RailX + 22f,
                                       pillY + 12f, 12f, 16f);
                tri.sprite = DeskKit.TriSprite(2, 12, 16);   // the drawn left arrow
                tri.raycastTarget = false;
                var lab = DrawnUI.HandLabel(_rail, "back to " + top[1], RailX + 42f,
                    pillY + 8f, 17f, DrawnUI.Ink, RailBoxW - 52f);
                lab.raycastTarget = false;
                lab.textWrappingMode = TextWrappingModes.NoWrap;
                lab.overflowMode = TextOverflowModes.Ellipsis;
                GameUi.InkWord(_rail, "", RailX, pillY, RailBoxW, 40f, 17f, DrawnUI.Ink,
                               () => BackPop());
            }
        }

        /// One page tab in the fan: quiet row · active paper box · RED-filled
        /// with the white bang when its desk has attention · GOLD with the
        /// deadline clock when momentary (Baloo, slight lean).
        void PageTab(string id, float y, int severity, bool gold, string clockText)
        {
            float x = RailX + 8f, w = RailBoxW - 16f, h = 36f;
            // S10/S8 — a quiet desk MAY speak a micro-status and MAY be
            // dormant; red always outranks the dim, gold never carries either.
            string micro = "";
            bool dormant = false;
            if (!gold && severity <= 0)
            {
                micro = DeskMicro(id);
                dormant = DeskDormant(id);
            }
            float dim = dormant ? 0.6f : 1f;
            float mw = micro == ""
                ? 0f : Mathf.Min(DrawnUI.MeasureWidth(micro, 15f), 74f);
            if (gold)
            {
                var body = DrawnUI.Fill(_rail, "ptab_gold", DrawnUI.Yellow, x, y, w, h);
                body.rectTransform.localEulerAngles = new Vector3(0f, 0f, 1.7f);
                DrawnUI.AddInkEdge(body.rectTransform, new Vector2(w, h),
                    new DrawnUI.PaperStyle
                    {
                        ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                        StepsPerEdge = 6, Jitter = 0.8f, Thickness = 2.4f, Seed = 19,
                    });
                DrawnUI.DisplayLabel(_rail, id, x + 8f, y + 8f, 16f, DrawnUI.Ink, w - 60f);
                var cc = DrawnUI.Fill(_rail, "ptab_clock", DeskKit.Alert, x + w - 52f,
                                      y + 6f, 48f, 22f);
                cc.raycastTarget = false;
                DrawnUI.HandLabel(_rail, clockText, x + w - 47f, y + 7f, 14f, Color.white,
                                  44f);
            }
            else if (severity > 0)
            {
                var body = DrawnUI.Fill(_rail, "ptab_red", DeskKit.Alert, x, y, w, h);
                DrawnUI.AddInkEdge(body.rectTransform, new Vector2(w, h),
                    new DrawnUI.PaperStyle
                    {
                        ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                        StepsPerEdge = 6, Jitter = 0.8f, Thickness = 2.4f, Seed = 23,
                    });
                DrawnUI.HandLabel(_rail, id, x + 9f, y + 6f, 19f, Color.white, w - 44f);
                DrawnUI.DisplayLabel(_rail, "!", x + w - 26f, y + 4f, 22f, Color.white, 20f);
            }
            else if (id == _page && _overview < 0)
            {
                DrawnUI.Fill(_rail, "ptab_sh", new Color(0f, 0f, 0f, 0.18f), x + 2f, y + 2f,
                             w - 2f, h - 2f).raycastTarget = false;
                var body = DrawnUI.Fill(_rail, "ptab_on",
                    DrawnUI.WithAlpha(DeskKit.Paper2, dim), x, y, w - 2f, h - 2f);
                DrawnUI.AddInkEdge(body.rectTransform, new Vector2(w - 2f, h - 2f),
                    new DrawnUI.PaperStyle
                    {
                        ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                        StepsPerEdge = 6, Jitter = 0.8f, Thickness = 2.4f, Seed = 29,
                    });
                DrawnUI.HandLabel(_rail, id, x + 9f, y + 5f, 19f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, dim),
                    w - 18f - (mw > 0f ? mw + 6f : 0f));
            }
            else
            {
                DrawnUI.HandLabel(_rail, id, x + 9f, y + 5f, 19f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, dim),
                    w - 18f - (mw > 0f ? mw + 6f : 0f));
            }
            if (mw > 0f)
            {
                var mlab = DrawnUI.HandLabel(_rail, micro, x + w - 8f - mw, y + 8f, 15f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f * dim), mw + 2f,
                    TextAlignmentOptions.TopRight);
                mlab.raycastTarget = false;
                mlab.textWrappingMode = TextWrappingModes.NoWrap;
                mlab.overflowMode = TextOverflowModes.Ellipsis;
            }
            string did = id;
            GameUi.InkWord(_rail, "", x, y, w, h, 19f, DrawnUI.Ink, () => OpenPage(did));
        }

        /// S10 — the desk's micro-status, reflection-probed: a desk MAY expose
        /// `public static string MicroStatus(GameState)`; absent = "".
        string DeskMicro(string id)
        {
            var mi = DeskStaticMethod(id, "MicroStatus");
            if (mi == null || _st == null) return "";
            try { return mi.Invoke(null, new object[] { _st }) as string ?? ""; }
            catch (Exception) { return ""; }
        }

        /// S8 — era dimming, never hiding: `public static bool IsDormant(GameState)`.
        bool DeskDormant(string id)
        {
            var mi = DeskStaticMethod(id, "IsDormant");
            if (mi == null || _st == null) return false;
            try
            {
                object v = mi.Invoke(null, new object[] { _st });
                return v is bool && (bool)v;
            }
            catch (Exception) { return false; }
        }

        /// The live figure a closed divider carries.
        string GroupCount(int gi)
        {
            switch (gi)
            {
                case 0:
                {
                    double rev = PnlNum("revenue");
                    return rev > 0 ? "$" + GameUi.Money((int)rev) + "/wk" : "—";
                }
                case 1:
                {
                    double burn = PnlNum("burn");
                    return burn > 0 ? "$" + GameUi.Money((int)burn) + "/wk" : "—";
                }
                case 2: return "6";
                default: return "wk " + _st.Week;
            }
        }

        double PnlNum(string key)
        {
            object box = _st.GetMeta("pnl", null);
            var dict = box as IDictionary<string, object>;
            if (dict != null)
            {
                object v;
                if (dict.TryGetValue(key, out v) && v != null)
                {
                    double d;
                    if (double.TryParse(v.ToString(),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out d)) return d;
                }
                return 0.0;
            }
            var jo = box as Newtonsoft.Json.Linq.JObject;
            return jo != null ? ContentDb.Num(jo, key, 0.0) : 0.0;
        }

        // ── the tour ───────────────────────────────────────────────────────

        /// THE FIRST-OPEN TOUR: six steps — the four groups fanned with
        /// one-liners, the red demo, the handover. Click advances, Esc skips,
        /// once per install; the how-to screen replays by clearing the flag.
        public void TourAdvance()
        {
            _tourStep += 1;
            if (_tourStep > 5) { TourFinish(); return; }
            TourApply();
            Refresh();
        }

        public void TourApply()
        {
            _tourDemoRed = _tourStep == 4;
            if (_tourStep >= 0 && _tourStep <= 3)
            {
                _openGroup = _tourStep;
                _page = GroupDesks[_tourStep][0];
            }
            else if (_tourStep == 4) { _openGroup = 2; _page = "threats"; }
            else { _openGroup = 3; _page = "this week"; }
        }

        void TourFinish()
        {
            _tourStep = -1;
            _tourDemoRed = false;
            try { System.IO.File.WriteAllText(RunwayPaths.User(TourFlagFile), "1"); }
            catch (Exception) { }
            _openGroup = 3;
            _page = "this week";
            Desk.Clear();
            Refresh();
        }

        public static bool TourSeen()
        {
            try { return System.IO.File.Exists(RunwayPaths.User(TourFlagFile)); }
            catch (Exception) { return false; }
        }

        public static void ResetTour()
        {
            try
            {
                string p = RunwayPaths.User(TourFlagFile);
                if (System.IO.File.Exists(p)) System.IO.File.Delete(p);
            }
            catch (Exception) { }
        }

        // ── the binder, as an object ───────────────────────────────────────

        /// The portrait texture, or null — never awaited.
        public static Texture2D PortraitTexture()
        {
            try
            {
                string p = RunwayPaths.User(PortraitFile);
                if (!System.IO.File.Exists(p)) return null;
                byte[] bytes = System.IO.File.ReadAllBytes(p);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                return tex.LoadImage(bytes) ? tex : null;
            }
            catch (Exception) { return null; }
        }

        /// <summary>
        /// THE DIEGETIC BINDER (DECISIONS § THE BINDER PORTRAIT, corrected):
        /// the object on the room painting at the scene's bottom-left that
        /// REPLACES the binder doorway button. Portrait when cached, the drawn
        /// mini-binder (same silhouette) otherwise; the name overlaid on the
        /// label (omitted below LabelMinPx); the red "!" sticker when the
        /// company has attention items; press opens the binder.
        /// </summary>
        public static RectTransform MakeObject(RectTransform parent, GameState state,
                                               Action onOpen, float w = 210f,
                                               float h = 250f)
        {
            var root = DrawnUI.Rect(parent, "binder_object", 0f, 0f, w, h);
            Texture2D tex = PortraitTexture();
            if (tex != null)
            {
                var img = new GameObject("portrait",
                    typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                img.texture = tex;
                img.rectTransform.SetParent(root, false);
                DrawnUI.SetTopLeft(img.rectTransform, 0f, 0f);
                img.rectTransform.sizeDelta = new Vector2(w, h);
                img.raycastTarget = false;
            }
            else
            {
                // the drawn mini-binder, same silhouette: kraft body, spine,
                // rings, untidy papers, the four group tabs on the right edge
                for (int p = 0; p < 4; p++)
                    DrawnUI.Fill(root, "paper" + p, DeskKit.Paper2,
                                 w * 0.16f + p * w * 0.17f, h * 0.02f, w * 0.15f, h * 0.05f)
                        .raycastTarget = false;
                var body = DrawnUI.Fill(root, "body", DeskKit.Kraft, w * 0.06f, h * 0.07f,
                                        w * 0.82f, h * 0.86f);
                body.raycastTarget = false;
                DrawnUI.Fill(root, "spine", DeskKit.Kraft2, w * 0.06f, h * 0.07f, w * 0.11f,
                             h * 0.86f).raycastTarget = false;
                Color[] cols = { DrawnUI.Sage, DrawnUI.Coral, DrawnUI.Blue, DrawnUI.Yellow };
                for (int i = 0; i < 4; i++)
                {
                    var t = DrawnUI.Fill(root, "otab" + i, cols[i], w * 0.86f,
                                         h * (0.16f + 0.2f * i), w * 0.1f, h * 0.09f);
                    t.raycastTarget = false;
                    DrawnUI.AddInkEdge(t.rectTransform, new Vector2(w * 0.1f, h * 0.09f),
                        new DrawnUI.PaperStyle
                        {
                            ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                            StepsPerEdge = 4, Jitter = 0.6f, Thickness = 2.2f, Seed = 29 + i,
                        });
                }
                DrawnUI.AddInkEdge(body.rectTransform, new Vector2(w * 0.82f, h * 0.86f),
                    new DrawnUI.PaperStyle
                    {
                        ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 2f,
                        StepsPerEdge = 14, Jitter = 1.6f, Thickness = 3.4f, Seed = 29,
                    });
                var lab = DrawnUI.Fill(root, "labelpaper", DeskKit.Paper2,
                                       w * LabelRect.x, h * LabelRect.y,
                                       w * LabelRect.width, h * LabelRect.height);
                lab.raycastTarget = false;
                DrawnUI.AddInkEdge(lab.rectTransform,
                    new Vector2(w * LabelRect.width, h * LabelRect.height),
                    new DrawnUI.PaperStyle
                    {
                        ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                        StepsPerEdge = 6, Jitter = 0.7f, Thickness = 2.4f, Seed = 31,
                    });
            }
            // the label illustration (or monogram) above the name — DECISIONS
            // § THE THREE BINDER ILLUSTRATIONS. Textless vignette; initials
            // stand in until it exists.
            float logoH = h * LabelRect.height * 1.35f;
            float logoY = h * LabelRect.y - logoH - 2f;
            Texture2D logoTex = null;
            try
            {
                string lp = PortraitClient.LogoPath;
                if (System.IO.File.Exists(lp))
                {
                    byte[] lb = System.IO.File.ReadAllBytes(lp);
                    var lt = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (lt.LoadImage(lb)) logoTex = lt;
                }
            }
            catch { }
            if (logoTex != null)
            {
                var li = new GameObject("logo", typeof(RectTransform),
                    typeof(RawImage)).GetComponent<RawImage>();
                li.texture = logoTex;
                li.rectTransform.SetParent(root, false);
                DrawnUI.SetTopLeft(li.rectTransform, w * LabelRect.x, logoY);
                li.rectTransform.sizeDelta = new Vector2(w * LabelRect.width, logoH);
                li.raycastTarget = false;
            }
            else if (state != null && !string.IsNullOrEmpty(state.CompanyName))
            {
                string initials = "";
                foreach (string wp in state.CompanyName.Split(' '))
                    if (initials.Length < 2 && wp.Length > 0)
                        initials += char.ToUpperInvariant(wp[0]);
                var mono = DrawnUI.DisplayLabel(root, initials, w * LabelRect.x, logoY,
                    logoH * 0.7f, DrawnUI.Ink, w * LabelRect.width,
                    TextAlignmentOptions.Center);
                mono.raycastTarget = false;
            }
            // the name, overlaid — the image is generated with a BLANK label;
            // below LabelMinPx the name is omitted rather than illegible
            string company = state != null && !string.IsNullOrEmpty(state.CompanyName)
                ? state.CompanyName : "the company";
            float sz = LabelFontSize;
            float fit = w * LabelRect.width - 10f;
            while (sz > LabelMinPx && company.Length * sz * 0.44f > fit) sz -= 1f;
            if (sz >= LabelMinPx)
            {
                var nm = DrawnUI.HandLabel(root, company, w * LabelRect.x,
                                           h * LabelRect.y + h * LabelRect.height * 0.18f,
                                           sz, DrawnUI.Ink, w * LabelRect.width,
                                           TextAlignmentOptions.Center);
                nm.raycastTarget = false;
            }
            // the red "!" sticker — the attention feed reaching the scene
            int worst = 0;
            if (state != null)
                foreach (AttentionItem it in SimEngine.AttentionItems(state))
                    worst = Gd.Maxi(worst, it.Severity);
            if (worst > 0)
            {
                var chip = DrawnUI.Fill(root, "sticker", DeskKit.Alert, w - 40f, 8f, 30f,
                                        30f);
                chip.raycastTarget = false;
                DrawnUI.AddInkEdge(chip.rectTransform, new Vector2(30f, 30f),
                    new DrawnUI.PaperStyle
                    {
                        ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                        StepsPerEdge = 5, Jitter = 0.6f, Thickness = 2.2f, Seed = 37,
                    });
                DrawnUI.DisplayLabel(root, "!", w - 40f, 8f, 20f, Color.white, 30f,
                                     TextAlignmentOptions.Center);
            }
            GameUi.InkWord(root, "", 0f, 0f, w, h, 19f, DrawnUI.Ink,
                           onOpen ?? (Action)(() => { }));
            return root;
        }

        // ── what a desk may touch (unchanged public hand) ──────────────────

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

        public void Spark(string key, float x, float y, float w, float h, Color col)
        {
            DrawnChart.MountSpark(_content, Series(key), col, x, y, w, h);
        }

        public void JarEdge(float x, float y, float w, float h, float t)
        {
            float half = t * 0.5f;
            DrawnUI.Fill(_content, "jaredge", DrawnUI.Ink, x - half, y - half, w + t, t);
            DrawnUI.Fill(_content, "jaredge", DrawnUI.Ink, x - half, y + h - half, w + t, t);
            DrawnUI.Fill(_content, "jaredge", DrawnUI.Ink, x - half, y - half, t, h + t);
            DrawnUI.Fill(_content, "jaredge", DrawnUI.Ink, x + w - half, y - half, t, h + t);
        }

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

        // ── the UX spine's public hand (13-binder-ux, DAG3) ────────────────

        /// S2b — a desk registers its controls WHILE IT DRAWS (pane-local
        /// rects); the registry lives one frame and feeds FocusDesk's
        /// spotlight and the receipt press map.
        public void MarkControl(string id, Rect rect) { _controls[id] = rect; }

        public bool HasControl(string id) { return _controls.ContainsKey(id); }

        public Rect ControlRect(string id)
        {
            Rect r;
            return _controls.TryGetValue(id, out r) ? r : new Rect();
        }

        /// S3 — the DO lane registers its buttons in draw order; ENTER
        /// presses the focused one, TAB cycles.
        public void RegisterDo(Button btn) { _doButtons.Add(btn); }

        public void ResetDoLane() { _doButtons.Clear(); }

        public int DoFocus() { return _doFocus; }

        /// S2b — THE COACH'S SPOTLIGHT, reused: a scrim with a hole cut over
        /// one control, a pen ring breathing round it, gone after ~2s or at
        /// any input.
        public void Spotlight(Rect rect)
        {
            if (_spot != null) Destroy(_spot.gameObject);
            var rt = DrawnUI.Rect(_content, "spotlight", 0f, 0f, ContentW, ContentH);
            rt.gameObject.AddComponent<BinderSpot>().Build(rect, ContentW, ContentH);
            _spot = rt;
        }

        sealed class BinderSpot : MonoBehaviour
        {
            float _age;
            Image _ring;
            public void Build(Rect hole, float w, float h)
            {
                hole = new Rect(hole.x - 10f, hole.y - 10f, hole.width + 20f,
                                hole.height + 20f);
                var rt = GetComponent<RectTransform>();
                var col = new Color(0.05f, 0.05f, 0.06f, 0.38f);
                DrawnUI.Fill(rt, "s0", col, 0f, 0f, w, Mathf.Max(hole.y, 0f))
                    .raycastTarget = false;
                DrawnUI.Fill(rt, "s1", col, 0f, hole.yMax, w,
                    Mathf.Max(h - hole.yMax, 0f)).raycastTarget = false;
                DrawnUI.Fill(rt, "s2", col, 0f, hole.y, Mathf.Max(hole.x, 0f),
                    hole.height).raycastTarget = false;
                DrawnUI.Fill(rt, "s3", col, hole.xMax, hole.y,
                    Mathf.Max(w - hole.xMax, 0f), hole.height).raycastTarget = false;
                _ring = DrawnUI.Fill(rt, "sring", DrawnUI.Coral, hole.x - 3f, hole.y - 3f,
                                     hole.width + 6f, hole.height + 6f);
                _ring.sprite = DrawnUI.WobbleRectSprite(
                    Mathf.Max((int)(hole.width + 6f), 4), Mathf.Max((int)(hole.height + 6f), 4),
                    1f, 3.4f, 10, 1.6f, 91, 3);
                _ring.raycastTarget = false;
                // the scrim catches the first press and spends it on itself
                GameUi.InkWord(rt, "", 0f, 0f, w, h, 19f, DrawnUI.Ink,
                               () => Destroy(gameObject));
            }
            void Update()
            {
                _age += Time.deltaTime;
                if (_age > 2f || Input.anyKeyDown) { Destroy(gameObject); return; }
                if (_ring != null)
                {
                    float t = Mathf.Floor(Time.unscaledTime * 12f) / 12f;
                    Color c = _ring.color;
                    c.a = Mathf.Clamp01(0.7f + 0.3f * Mathf.Sin(t * 4f));
                    _ring.color = c;
                }
            }
        }

        /// S4 — THE RECEIPT POPOVER: a small paper card saying the terms that
        /// made a number, near the press. Any press or Esc dismisses it;
        /// while it is open Esc pops IT, never the desk.
        public void Popover(string title, IList<DeskKit.TicketLine> lines, Vector2 at)
        {
            ClosePopover();
            var rootp = DrawnUI.Rect(_content, "popover", 0f, 0f, ContentW, ContentH);
            GameUi.InkWord(rootp, "", 0f, 0f, ContentW, ContentH, 19f, DrawnUI.Ink,
                           ClosePopover);
            const float W = 380f;
            float h = 56f + lines.Count * 30f + 14f;
            float px = Mathf.Clamp(at.x, 4f, DeskKit.PaneW - W - 8f);
            float py = Mathf.Clamp(at.y, 4f, ContentH - h - 8f);
            DrawnUI.Fill(rootp, "popsh", new Color(0f, 0f, 0f, 0.2f), px + 6f, py + 8f,
                         W, h).raycastTarget = false;
            var body = DrawnUI.Fill(rootp, "popbody", DeskKit.Paper2, px, py, W, h);
            body.raycastTarget = false;
            DrawnUI.AddInkEdge(body.rectTransform, new Vector2(W, h),
                new DrawnUI.PaperStyle
                {
                    ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 2f,
                    StepsPerEdge = 13, Jitter = 1.8f, Thickness = 2.6f,
                    Seed = 97 + Mathf.Abs((int)px % 13),
                });
            var tl = DrawnUI.HandLabel(rootp, title, px + 14f, py + 6f, 20f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), W - 28f);
            tl.raycastTarget = false;
            tl.textWrappingMode = TextWrappingModes.NoWrap;
            tl.overflowMode = TextOverflowModes.Ellipsis;
            for (float dx = 10f; dx < W - 10f; dx += 16f)
                DrawnUI.Fill(rootp, "popdash", DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f),
                    px + dx, py + 38f, Mathf.Min(9f, W - 10f - dx), 2f).raycastTarget = false;
            float ly = py + 46f;
            for (int i = 0; i < lines.Count; i++)
            {
                var la = DrawnUI.HandLabel(rootp, lines[i].Label, px + 14f, ly, 19f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.85f), W * 0.58f);
                la.raycastTarget = false;
                var va = DrawnUI.HandLabel(rootp, lines[i].Value, px + 14f, ly, 19f,
                    lines[i].Col ?? DrawnUI.Ink, W - 28f, TextAlignmentOptions.TopRight);
                va.raycastTarget = false;
                ly += 30f;
            }
            _popover = rootp;
        }

        public void ClosePopover()
        {
            if (_popover != null) Destroy(_popover.gameObject);
            _popover = null;
        }

        /// S5 — THE SEEN STORE: record what this desk shows and learn whether
        /// it moved since the binder was last open. False on the very first
        /// sighting (news is what CHANGED, not what exists). Values are
        /// strings; the caller formats.
        public bool Seen(string desk, string key, string value)
        {
            SeenLoad();
            string k = desk + "/" + key;
            _seenPending[k] = value;
            string had;
            return _seenStore.TryGetValue(k, out had) && had != value;
        }

        /// The stored (last-open) value, "" when the store never saw the key.
        public string SeenPrev(string desk, string key)
        {
            SeenLoad();
            string had;
            return _seenStore.TryGetValue(desk + "/" + key, out had) ? had : "";
        }

        string SeenPath()
        {
            return RunwayPaths.User("binder_seen_" + (_st != null ? _st.SimSeed : 0)
                                    + ".json");
        }

        void SeenLoad()
        {
            if (_seenLoaded) return;
            _seenLoaded = true;
            try
            {
                string p = SeenPath();
                if (!System.IO.File.Exists(p)) return;
                var parsed = Newtonsoft.Json.JsonConvert
                    .DeserializeObject<Dictionary<string, string>>(
                        System.IO.File.ReadAllText(p));
                if (parsed != null) _seenStore = parsed;
            }
            catch (Exception) { }
        }

        void SeenFlush()
        {
            if (_seenPending.Count == 0) return;
            SeenLoad();
            try
            {
                var merged = new Dictionary<string, string>(_seenStore);
                foreach (KeyValuePair<string, string> kv in _seenPending)
                    merged[kv.Key] = kv.Value;
                System.IO.File.WriteAllText(SeenPath(),
                    Newtonsoft.Json.JsonConvert.SerializeObject(merged));
                _seenStore = merged;
                _seenPending.Clear();
            }
            catch (Exception) { }
        }

        /// S7 — THE BREADCRUMB: a drilled desk re-declares its trail every
        /// draw ("Lyon ‹ the works" at the sheet's top-left); it dies with the
        /// drill state because the next refresh simply isn't told again.
        public void PushCrumb(string label)
        {
            _crumb = label;
            var t = DrawnUI.HandLabel(_content, label + " ‹ " + _page, 10f, 0f, 21f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f), 520f);
            t.raycastTarget = false;
        }

        // ── the desk-type probes (S8/S10/S14 — desks MAY speak, never must) ─

        /// The one id→type map, so a probe or a suggestion sweep never reaches
        /// into Dispatch. Null off the taxonomy.
        public static Type DeskType(string id)
        {
            switch (id)
            {
                case "offers": return typeof(DeskOffers);
                case "customers": return typeof(DeskCustomersPage);
                case "in motion": return typeof(DeskInMotion);
                case "growth": return typeof(DeskGrowth);
                case "spend": return typeof(DeskSpend);
                case "team": return typeof(DeskTeam);
                case "recruitment": return typeof(DeskRecruit);
                case "bills": return typeof(DeskBills);
                case "the bank": return typeof(DeskBankPage);
                case "the works": return typeof(DeskWorks);
                case "what we make": return typeof(DeskMake);
                case "cap table": return typeof(DeskCapPage);
                case "the raise": return typeof(DeskRaise);
                case "the street": return typeof(DeskStreetPage);
                case "threats": return typeof(DeskThreatsPage);
                case "pivot": return typeof(DeskPivot);
                case "this week": return typeof(DeskThisWeek);
                case "history": return typeof(DeskHistory);
                case "events": return typeof(DeskEvents);
                case "the offer": return typeof(DeskOffer);
            }
            return null;
        }

        static readonly Dictionary<string, System.Reflection.MethodInfo> _methodCache =
            new Dictionary<string, System.Reflection.MethodInfo>();

        /// `has_method` for a desk's statics, cached — the twin of the Godot
        /// script-method probe.
        public static System.Reflection.MethodInfo DeskStaticMethod(string id, string name)
        {
            string ck = id + "/" + name;
            System.Reflection.MethodInfo mi;
            if (_methodCache.TryGetValue(ck, out mi)) return mi;
            Type t = DeskType(id);
            mi = t == null ? null : t.GetMethod(name,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            _methodCache[ck] = mi;
            return mi;
        }

        /// The Godot line box, public for the kit's measure law (S6).
        public static float LineBox(TMP_FontAsset f, float size)
        {
            return GodotLineBox(f, size);
        }

        /// `_wrap_h`'s twin — the Godot line box, not preferredHeight (the
        /// full story lives in the original comment; the math is unchanged).
        public static float Height(TextMeshProUGUI t)
        {
            if (t == null) return 0f;
            t.ForceMeshUpdate();
            int lines = Mathf.Max(1, t.textInfo.lineCount);
            return lines * GodotLineBox(t.font, t.fontSize);
        }

        static float GodotLineBox(TMP_FontAsset f, float size)
        {
            if (size <= 0f) return 0f;
            float tmp = size * 1.354f;
            if (f != null && f.faceInfo.pointSize > 0f)
            {
                float ps = f.faceInfo.pointSize;
                tmp = (f.faceInfo.lineHeight > 0f
                       ? f.faceInfo.lineHeight
                       : f.faceInfo.ascentLine - f.faceInfo.descentLine) / ps * size;
            }
            float godot = tmp + DrawnUI.GodotLineSpacing(f, size, DrawnUI.StringLeading)
                                * size * 0.01f;
            return Mathf.Max(Mathf.Round(godot), 1f);
        }
    }
}
