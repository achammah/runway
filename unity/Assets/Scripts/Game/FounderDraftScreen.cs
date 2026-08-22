using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// THE FOUNDER DRAFT — founder_draft_screen.gd, ported. Seven pages, not a form:
    ///
    ///   0 FIRST, YOUR NAME      one big write-in, prefilled with a dealt name
    ///   1 CHOOSE YOUR FOUNDER   dark stage, spotlight, the founder BIG and animated,
    ///                           the stat sheet beside them, the roster on paper below
    ///   2 NAME YOUR STARTUP     the name, the pitch, and the idea machine
    ///   3 THE SHAPE OF IT       what you sell, and who to
    ///   4 THE CREW              cofounders, equity, vesting, the live cap table
    ///   5 FIRST MONEY           three cards; money now costs equity forever
    ///   6 PACK YOUR BAG         four slots, a shelf in sections, a shipping label
    ///
    /// THIS FILE IS THE HOST: the model every page reads and writes, the curtain-wipe
    /// between them, the cap-table arithmetic, the YC-canon trap detector, and the one
    /// door out. Each page builds itself in its own file against this — the same split
    /// the original keeps between `_build_*` and the shared `_refresh_capline`.
    ///
    /// THE BAG PAGE STARTS THE WORLD: entering page 6 hands the pitch to the driver,
    /// which generates the bible behind the player, so the birth screen usually only
    /// shows for a breath.
    /// </summary>
    public sealed class FounderDraftScreen : AppScreen
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            ScreenRegistry.Register(AppState.Draft, typeof(FounderDraftScreen));
        }

        public static readonly string[] StatNames = { "build", "sell", "raise", "recruit", "grit" };
        public static readonly string[] StatLabels = { "BUILD", "SELL", "RAISE", "RECRUIT", "GRIT" };
        public const int MaxCofounders = 4;
        public static readonly string[] Roles = { "Sales", "Business", "Tech", "Hustler", "The Idea Friend" };
        public static readonly string[] Commitments = { "Full-time", "Part-time" };

        /// CHOOSE YOUR FOUNDER reserves two bands so nothing can drift into anything
        /// else as copy changes: the roster owns everything below DockBandTop, and the
        /// founder's sheet must end above SheetBottomMax.
        public const float DockBandTop = 848f;
        public const float SheetBottomMax = 840f;

        public static readonly string[] NameA =
        {
            "Snack", "Loop", "Byte", "Nap", "Quill", "Moss", "Pling", "Drift",
            "Stack", "Fern", "Bolt", "Mono", "Husk", "Pivot", "Blob",
        };
        public static readonly string[] NameB =
        {
            "ly", ".io", "ify", "base", "deck", "nest", "flow", "ora", "ium", "sy", "let", "kit",
        };
        public static readonly string[] IdeaPre =
        {
            "AI", "Blockchain", "Artisanal", "Enterprise", "Vegan", "Quantum",
            "Subscription", "Voice-first", "B2B", "Peer-to-peer", "Serverless", "Emotional",
        };
        public static readonly string[] IdeaForm =
        {
            "copilot", "marketplace", "superapp", "API", "subscription box", "robot",
            "assistant", "platform", "dashboard", "wearable",
        };
        public static readonly string[] IdeaFor =
        {
            "compliance", "dog grooming", "funerals", "tax returns", "meal prep", "parking",
            "therapy", "laundry", "weddings", "napping", "HOAs", "expense reports",
            "houseplants", "breakups",
        };

        // ── the model every page shares ────────────────────────────────────────
        public ContentDb Deck;
        public JArray Archetypes = new JArray();
        public JArray Fundings = new JArray();
        public JObject SelArch;
        public JObject SelFund;
        public int SelIndex;
        public readonly List<DraftCofounder> Cofounders = new List<DraftCofounder>();
        public readonly List<string> Bag = new List<string>();
        public string BizWhat = "Software";
        public string BizWho = "Consumer";
        public string FounderName = "";
        public string CompanyName = "";
        public string CompanyIdea = "";
        public Rng Prng;

        readonly RectTransform[] _pages = new RectTransform[7];
        int _page;
        bool _wiping;

        DraftSignPage _sign;
        DraftSelectPage _select;
        DraftNamePage _name;
        DraftShapePage _shape;
        DraftCrewPage _crew;
        DraftMoneyPage _money;
        DraftBagPage _bagPage;

        public DraftSelectPage SelectPage { get { return _select; } }
        public DraftCrewPage CrewPage { get { return _crew; } }
        public DraftMoneyPage MoneyPage { get { return _money; } }
        public DraftBagPage BagPage { get { return _bagPage; } }

        // ══ build ══════════════════════════════════════════════════════════════

        protected override void OnBuild()
        {
            Prng = new Rng((ulong)DateTime.UtcNow.Ticks);
            Deck = RunDriver.Current != null ? RunDriver.Current.Deck : new ContentDb();
            Archetypes = Deck.Archetypes;
            Fundings = Deck.Fundings;

            // THE STAGE, on every page. It can never fall back to a blank screen: a
            // night field always, the painted stage over it when it is on disk.
            DrawnUI.FullFill(Rect, "night", GameUi.Night, true);
            if (RunwayPaths.ArtExists("env/stage.png"))
            {
                var stageRt = DrawnUI.FullRect(Rect, "stage");
                var stage = stageRt.gameObject.AddComponent<RawImage>();
                stage.raycastTarget = false;
                stage.enabled = false;
                ArtCache.Load("env/stage.png", tex =>
                {
                    if (stage == null || tex == null) return;
                    stage.texture = tex;
                    stage.enabled = true;
                });
            }
            else
            {
                Spotlight(Rect);
            }
            Runway.Effects.GlowSprites.Apply(Rect, Runway.Effects.GlowScene.SelectStage);

            if (Archetypes.Count > 0) SelArch = Archetypes[0] as JObject;

            _sign = new DraftSignPage(this);
            _select = new DraftSelectPage(this);
            _name = new DraftNamePage(this);
            _shape = new DraftShapePage(this);
            _crew = new DraftCrewPage(this);
            _money = new DraftMoneyPage(this);
            _bagPage = new DraftBagPage(this);

            _pages[0] = _sign.Build();
            _pages[1] = _select.Build();
            _pages[2] = _name.Build();
            _pages[3] = _shape.Build();
            _pages[4] = _crew.Build();
            _pages[5] = _money.Build();
            _pages[6] = _bagPage.Build();

            ShowPage(0);
            if (Archetypes.Count > 0) _select.Select(0, false);
        }

        /// Procedural stage if the painted backdrop is missing: cone, pool, floor line.
        static void Spotlight(RectTransform parent)
        {
            float w = RunwayPaths.StageWidth;
            float h = RunwayPaths.StageHeight;
            DrawnUI.Fill(parent, "floor", DrawnUI.Hex("2C343B"), 0f, h * 0.78f, w, h * 0.22f);
            DrawnUI.Fill(parent, "floorline", new Color(0.05f, 0.05f, 0.05f, 1f), 0f, h * 0.78f, w, 3f);
            for (int i = 0; i < 14; i++)
            {
                float k = i / 13f;
                float cw = Mathf.Lerp(w * 0.16f, w * 0.44f, k);
                DrawnUI.Fill(parent, "cone", DrawnUI.WithAlpha(DrawnUI.Cream, 0.012f),
                             (w - cw) * 0.5f, h * 0.86f * k, cw, h * 0.86f / 14f + 1f);
            }
            var pool = DrawnUI.Rect(parent, "pool", w * 0.27f, h * 0.81f, w * 0.46f, h * 0.10f);
            var img = pool.gameObject.AddComponent<Image>();
            img.sprite = DrawnUI.RingSprite(48f, 1f, 0f, 5, 2, true);
            img.color = DrawnUI.WithAlpha(DrawnUI.Cream, 0.20f);
            img.raycastTarget = false;
        }

        // ══ the page furniture every page shares ═══════════════════════════════

        /// The dim every page but the select stage lays over the stage.
        public static void Dim(RectTransform page)
        {
            DrawnUI.FullFill(page, "dim", new Color(0.11f, 0.13f, 0.16f, 0.84f), true);
        }

        /// A hand-lettered heading with a coral rule under it, measured to the word.
        public static TextMeshProUGUI Heading(RectTransform page, string text, float size,
                                              float x, float y)
        {
            var t = DrawnUI.HandLabel(page, text, x, y, size, DrawnUI.Cream);
            float w = DrawnUI.MeasureWidth(text, size);
            GameUi.HandRule(page, x + 2f, y + size * 1.48f, w, DrawnUI.Coral, 6);
            return t;
        }

        /// The paper card every "next"/"back" door on this flow is cut from — the SAME
        /// paper as every sheet on it (`_paper_card` builds a PaperEdge, not the title
        /// screen's card), leaning by its own x exactly as `int(b.position.x) % 5` does.
        public Button Nav(RectTransform page, string text, float x, float y, float w, float h,
                          float size, Action onClick)
        {
            return DrawnUI.PaperButton(page, text, x, y, w, h, size, DrawnUI.Ink,
                                       DrawnUI.CoralDark, onClick, 1.045f,
                                       GameUi.DraftPaper((int)x % 5));
        }

        // ══ page switching ═════════════════════════════════════════════════════

        public int Page { get { return _page; } }

        /// Curtain-wipe page transition: two night panels close, the page swaps, they
        /// open. The one gesture that makes seven screens read as one sitting.
        public void TransitionTo(int pageIndex)
        {
            if (_wiping) return;
            StartCoroutine(Wipe(pageIndex));
        }

        IEnumerator Wipe(int pageIndex)
        {
            _wiping = true;
            var top = DrawnUI.Fill(Rect, "wipe_top", DrawnUI.Ink, 0f, 0f, RunwayPaths.StageWidth, 0f);
            var bottom = DrawnUI.Fill(Rect, "wipe_bottom", DrawnUI.Ink, 0f,
                                      RunwayPaths.StageHeight, RunwayPaths.StageWidth, 0f);
            top.raycastTarget = true;
            bottom.raycastTarget = true;
            float t = 0f;
            while (t < 0.22f)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / 0.22f);
                float e = k * k;                                  // TRANS_QUAD, EASE_IN
                top.rectTransform.sizeDelta = new Vector2(RunwayPaths.StageWidth, 520f * e);
                bottom.rectTransform.sizeDelta = new Vector2(RunwayPaths.StageWidth, 520f * e);
                DrawnUI.SetTopLeft(bottom.rectTransform, 0f,
                    Mathf.Lerp(RunwayPaths.StageHeight, 504f, e));
                yield return null;
            }
            ShowPage(pageIndex);
            // the bag page REBUILDS itself on entry, which makes it the last sibling —
            // the wipe has to reclaim the top or the new page shows through the panels
            top.transform.SetAsLastSibling();
            bottom.transform.SetAsLastSibling();
            t = 0f;
            while (t < 0.22f)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / 0.22f);
                float e = DrawnUI.EaseOutCubic(k);
                top.rectTransform.sizeDelta = new Vector2(RunwayPaths.StageWidth, 520f * (1f - e));
                bottom.rectTransform.sizeDelta = new Vector2(RunwayPaths.StageWidth, 520f * (1f - e));
                DrawnUI.SetTopLeft(bottom.rectTransform, 0f,
                    Mathf.Lerp(504f, RunwayPaths.StageHeight, e));
                yield return null;
            }
            Destroy(top.gameObject);
            Destroy(bottom.gameObject);
            _wiping = false;
        }

        public void ShowPage(int i)
        {
            _page = Mathf.Clamp(i, 0, _pages.Length - 1);
            // THE SHELF DEPENDS ON THE TRADE chosen two pages back, so the bag page is
            // rebuilt on every entry rather than dealt once at boot.
            if (_page == 6 && _pages[6] != null)
            {
                Destroy(_pages[6].gameObject);
                _bagPage = new DraftBagPage(this);
                _pages[6] = _bagPage.Build();
            }
            for (int p = 0; p < _pages.Length; p++)
                if (_pages[p] != null) _pages[p].gameObject.SetActive(p == _page);

            if (_page == 1) _select.Entrance();
            if (_page == 2) _name.Entrance();
            if (_page >= 3) RefreshCapLine();
            if (_page == 6) PrefetchWorld();
        }

        /// THE BAG PAGE STARTS THE WORLD: by the time the founder is packing, the pitch
        /// is written — the bible generates in the background so BIRTH usually only
        /// shows for a breath.
        void PrefetchWorld()
        {
            var driver = RunDriver.Current;
            if (driver == null) return;
            string nm = (CompanyName ?? "").Trim();
            if (nm.Length == 0) return;
            driver.PrefetchWorld(nm, (CompanyIdea ?? "").Trim(), BizWhat, BizWho);
        }

        // ══ the cap table ══════════════════════════════════════════════════════

        /// Investors dilute EVERYONE pro-rata: every founding share is multiplied by this.
        public double Dilution()
        {
            if (SelFund == null) return 1.0;
            return 1.0 - ContentDb.Num(SelFund, "equity_cost", 0.0) / 100.0;
        }

        public double FounderPct()
        {
            double pct = 100.0;
            for (int i = 0; i < Cofounders.Count; i++) pct -= Cofounders[i].Equity;
            return pct * Dilution();
        }

        public int DayOneCash()
        {
            int cash = 8000;
            if (SelArch != null) cash += ContentDb.Int(SelArch, "start_cash_bonus", 0);
            if (SelFund != null) cash += ContentDb.Int(SelFund, "cash", 0);
            for (int i = 0; i < Bag.Count; i++) cash += Deck.CashValue(Bag[i]);
            return cash;
        }

        public int BagSlotsUsed()
        {
            int used = 0;
            for (int i = 0; i < Bag.Count; i++) used += Deck.CarryCost(Bag[i]);
            return used;
        }

        /// Every page that can change the split calls this; the pages that draw one
        /// answer to it.
        public void RefreshCapLine()
        {
            if (_crew != null) _crew.Refresh();
            if (_money != null) _money.Refresh();
            if (_bagPage != null) _bagPage.Refresh();
        }

        // ══ the YC-canon trap detector ═════════════════════════════════════════

        public List<string[]> ComputeTraps()
        {
            var traps = new List<string[]>();
            double founderPct = FounderPct();
            int n = Cofounders.Count;
            if (n == 0)
                traps.Add(new[] { "solo",
                    "Solo founder — nobody to split the 2am dread with. Burnout bleed, investors squint." });
            for (int i = 0; i < n; i++)
            {
                DraftCofounder cf = Cofounders[i];
                bool ft = cf.Commitment == "Full-time";
                double eq = cf.Equity;
                if (ft && cf.Role != "The Idea Friend" && eq < 10.0)
                    traps.Add(new[] { "trap_underpaid_cofounder", string.Format(
                        "A full-time {0} at {1:0}% — insulting splits breed resentment. It WILL come up again.",
                        cf.Role.ToLower(), eq) });
                if (!ft && eq >= 15.0)
                    traps.Add(new[] { "trap_part_timer_rich", string.Format(
                        "A part-timer holding {0:0}% — real equity for half presence. Launch week will find them 'at a thing'.",
                        eq) });
                if (cf.Role == "The Idea Friend" && eq >= 10.0)
                    traps.Add(new[] { "trap_idea_tax", string.Format(
                        "{0:0}% for the idea. Ideas are free; execution is the company. They will have notes.", eq) });
                if (!cf.Vesting)
                    traps.Add(new[] { "trap_no_vesting",
                        "No vesting, no cliff — if they walk, the shares walk with them. Forever. The classic." });
            }
            if (n >= 3)
                traps.Add(new[] { "trap_too_many_cooks", string.Format(
                    "{0} cofounders — every decision becomes a senate hearing.", n) });
            if (founderPct < 50.0)
                traps.Add(new[] { "trap_lost_majority", string.Format(
                    "You hold {0:0}% on day one — everyone else combined outvotes you.", founderPct) });
            if (traps.Count == 0 && n >= 1 && n <= 2)
            {
                bool ok = true;
                for (int i = 0; i < n; i++)
                {
                    DraftCofounder cf = Cofounders[i];
                    if (cf.Commitment != "Full-time" || !cf.Vesting || cf.Equity < 15.0) ok = false;
                }
                if (ok)
                    traps.Add(new[] { "healthy_split",
                        "✓ Near-equal, full-time, vested. The essays would be proud. (This pays off.)" });
            }
            return traps;
        }

        // ══ the door ═══════════════════════════════════════════════════════════

        public string BlockedReason()
        {
            if (SelArch == null) return "PICK A FOUNDER FIRST";
            if (SelFund == null) return "PICK YOUR FIRST MONEY";
            if (FounderPct() <= 5.0) return "YOU KEPT TOO LITTLE — TAKE EQUITY BACK";
            return "";
        }

        public void DoLaunch()
        {
            if (BlockedReason().Length > 0) return;
            var result = new DraftResult
            {
                Archetype = SelArch,
                Funding = SelFund,
                CompanyName = (CompanyName ?? "").Trim().Length > 0 ? CompanyName.Trim() : "Untitled Inc",
                FounderName = (FounderName ?? "").Trim().Length > 0
                    ? FounderName.Trim() : WorldGen.PersonName(Prng),
                CompanyIdea = (CompanyIdea ?? "").Trim(),
                BizWhat = BizWhat,
                BizWho = BizWho,
            };
            for (int i = 0; i < Cofounders.Count; i++)
            {
                DraftCofounder c = Cofounders[i];
                result.Cofounders.Add(new DraftCofounder
                {
                    Name = c.Name, Role = c.Role, Commitment = c.Commitment,
                    Equity = c.Equity, Vesting = c.Vesting,
                });
            }
            result.Items.AddRange(Bag);
            List<string[]> traps = ComputeTraps();
            for (int i = 0; i < traps.Count; i++)
                if (!result.Traps.Contains(traps[i][0])) result.Traps.Add(traps[i][0]);
            Finish(result);
        }

        // ══ page 1 keys ════════════════════════════════════════════════════════

        void Update()
        {
            if (_page != 1 || _wiping) return;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                _select.Select(SelIndex - 1, true);
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                _select.Select(SelIndex + 1, true);
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
                _select.LockIn();
        }
    }
}
