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
    /// PAGE 1 — CHOOSE YOUR FOUNDER. A dark stage, a spotlight, the selected founder
    /// BIG and breathing centre-stage, the stat sheet at the side, the roster standing
    /// on one sheet of paper at the bottom.
    ///
    /// TWO LEDGERS ON ONE SHEET, and BOTH ARE CLICKABLE (owner: "double clicks on all
    /// the details of the characters like a D&D game"): the five COMPETENCES that get
    /// rolled, and under them the six TRAITS that never do — because a founder is more
    /// than their verbs, and every one of those six is a rule the engine actually runs.
    /// The answer is a torn note laid over the bottom of the sheet, in the same pen.
    ///
    /// THE TRAIT COPY IS THE ENGINE'S OWN. SimEngine.TRAIT_RULES is the single copy, so
    /// the promise on this card and the rule at the table can never drift apart.
    /// </summary>
    public sealed class DraftSelectPage
    {
        /// The five muscles, in the engine's words — same tip widget as the traits.
        static readonly Dictionary<string, string> StatRules = new Dictionary<string, string>
        {
            { "build", "Governs building moves: your level adds (level − 3) to the d20 when the week's plan is product work. Focus 4+ rolls build at advantage. R&D budget ships ≈ +1 product per $1,200/wk; product quality gates adoption AND retention." },
            { "sell", "Governs selling moves: adds (level − 3) to the d20. Sell also sets your weekly closing capacity — each level ≈ +0.8 customers/wk of go-to-market cap. Charisma 4+ rolls sell at advantage." },
            { "raise", "Governs fundraising moves: adds (level − 3) to the d20. Credibility + network ≥ 8 opens doors (advantage on every raise) and warms term sheets — up to 8% less equity asked." },
            { "recruit", "Governs hiring moves: adds (level − 3) to the d20. Charisma 4+ rolls recruit at advantage. Hires onboard for 2 weeks, paid before productive." },
            { "grit", "Governs pushing-through moves: adds (level − 3) to the d20. Exhaustion 4+ forces disadvantage; stamina ≤ 2 while tired does too. Luck 4+ rerolls a natural 1 anywhere." },
        };

        readonly FounderDraftScreen _s;
        RectTransform _page;
        RectTransform _panel;
        RectTransform _pipsHost;
        RectTransform _traitHost;
        DraftLoop _hero;
        RectTransform _heroHolder;
        RectTransform _heroShadow;
        TextMeshProUGUI _title;
        TextMeshProUGUI _dName;
        TextMeshProUGUI _dTag;
        TextMeshProUGUI _dCash;
        TextMeshProUGUI _dPerk;
        RectTransform _tip;
        TextMeshProUGUI _tipHead;
        TextMeshProUGUI _tipBody;
        RectTransform _lockBtn;
        readonly List<RectTransform> _chips = new List<RectTransform>();
        float _heroBaseY;
        bool _stamping;
        DraftBreath _breath;

        public DraftSelectPage(FounderDraftScreen s) { _s = s; }

        public RectTransform Build()
        {
            _page = DrawnUI.FullRect(_s.Rect, "page_select");
            Runway.Effects.Motes.DraftSpotlight(_page);
            _title = FounderDraftScreen.Heading(_page, "CHOOSE YOUR FOUNDER", 58f, 60f, 28f);

            // the idle sheets are cut to one house frame, so the feet land at 345/368
            // of the canvas — the shadow is centred on that real baseline, not on the
            // bottom of the rect, which is what left the founder standing 55px above
            // his own shadow.
            _heroShadow = GameUi.Shadow(_page, 465f, 742f, 300f, 46f).rectTransform;
            // THE HOLDER PIVOTS ON THE FEET. The original sways the founder about
            // pivot_offset (280, 560) — the point standing on the lit floor — so the
            // holder carries that pivot and the loop simply fills it. Without it the
            // sway swings the shoes out from under him.
            _heroHolder = DrawnUI.Rect(_page, "heroholder", 335f, 240f, 560f, 560f);
            _heroHolder.pivot = new Vector2(0.5f, 0f);
            _heroHolder.anchoredPosition = new Vector2(335f + 280f, -(240f + 560f));
            _heroBaseY = _heroHolder.anchoredPosition.y;
            _hero = DraftLoop.Attach(_heroHolder, "hero", 0f, 0f, 560f, 560f);

            BuildSheet();
            BuildRoster();

            _lockBtn = DrawnUI.PaperButton(_page, "LOCK IN  →", 1230f, 880f, 260f, 84f, 36f,
                DrawnUI.Ink, DrawnUI.CoralDark, LockIn).GetComponent<RectTransform>();

            _breath = _page.gameObject.AddComponent<DraftBreath>();
            _breath.Bind(_heroHolder, _heroShadow, _title, _lockBtn, _heroBaseY);
            return _page;
        }

        // ── the stat sheet ─────────────────────────────────────────────────────

        void BuildSheet()
        {
            _panel = GameUi.PaperSheet(_page, 936f, 72f, 540f,
                FounderDraftScreen.SheetBottomMax - 72f, 1, 4f, null, "sheet");
            GameUi.Tilt(_panel, -0.008f);

            _dName = DrawnUI.HandLabel(_panel, "", 44f, 20f, 46f, DrawnUI.Ink, 470f);
            _dTag = DrawnUI.HandLabel(_panel, "", 44f, 82f, 26f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.9f), 470f);
            GameUi.HandRule(_panel, 44f, 146f, 140f, DrawnUI.Coral, 7);

            _pipsHost = DrawnUI.Rect(_panel, "pips", 0f, 0f, 540f, 460f);

            // EVERY ROW IS A RULE (owner: all the details clickable, D&D style): an
            // invisible button over each pip row opens the same tip the traits use.
            for (int i = 0; i < FounderDraftScreen.StatNames.Length; i++)
            {
                string sname = FounderDraftScreen.StatNames[i];
                var row = DrawnUI.Rect(_panel, "statrow", 44f, 172f + i * 52f, 470f, 46f);
                var hit = row.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                hit.raycastTarget = true;
                var b = row.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.targetGraphic = hit;
                b.onClick.AddListener(() => ShowStatTip(sname));
            }

            DrawnUI.HandLabel(_panel, "HIDDEN TRAITS", 44f, 434f, 22f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.62f));
            DrawnUI.HandLabel(_panel, "click any trait for what it does", 232f, 436f, 20f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f));
            _traitHost = DrawnUI.Rect(_panel, "traits", 0f, 0f, 540f, 620f);

            for (int i = 0; i < GameState.TRAIT_NAMES.Count; i++)
            {
                string tname = GameState.TRAIT_NAMES[i];
                float cx = 44f + (i / 3) * GameUi.TraitColW - 6f;
                float cy = 462f + (i % 3) * GameUi.TraitRowH;
                var tb = DrawnUI.Rect(_panel, "traitrow", cx, cy,
                    GameUi.TraitColW - 4f, GameUi.TraitRowH - 6f);
                var hit = tb.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                hit.raycastTarget = true;
                var b = tb.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.targetGraphic = hit;
                b.onClick.AddListener(() => ShowTraitTip(tname));
            }

            DrawnUI.HandLabel(_panel, "IN THE BANK, DAY ONE", 44f, 614f, 23f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f));
            _dCash = DrawnUI.HandLabel(_panel, "", 44f, 638f, 40f, DrawnUI.Sage, 470f);
            DrawnUI.HandLabel(_panel, "PERK", 44f, 692f, 23f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f));
            _dPerk = DrawnUI.HandLabel(_panel, "", 44f, 714f, 25f, DrawnUI.Ink, 470f);

            // THE ANSWER TO A CLICK: a torn note laid over the bottom of the sheet, in
            // the same pen. It covers the money and the perk while it is open, and one
            // more click puts it away — a footnote, not a second screen.
            _tip = GameUi.PaperSheet(_panel, 28f, 604f, 486f, 142f, 2, 3.5f, DrawnUI.Coral, "tip");
            GameUi.Tilt(_tip, 0.006f);
            _tipHead = DrawnUI.HandLabel(_tip, "", 22f, 12f, 26f, DrawnUI.Ink, 440f);
            GameUi.HandRule(_tip, 22f, 44f, 100f, DrawnUI.WithAlpha(DrawnUI.Sage, 0.9f), 8);
            _tipBody = DrawnUI.HandLabel(_tip, "", 22f, 58f, 21f, DrawnUI.Ink, 440f);
            _tipBody.rectTransform.sizeDelta = new Vector2(440f, 92f);
            GameUi.InkWord(_tip, "×", 430f, 4f, 48f, 42f, 30f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f), () => _tip.gameObject.SetActive(false));
            _tip.gameObject.SetActive(false);
        }

        // ── the roster ─────────────────────────────────────────────────────────

        void BuildRoster()
        {
            int n = _s.Archetypes.Count;
            if (n == 0) return;
            float rowW = n * 142f - 14f;
            float x0 = (RunwayPaths.StageWidth - rowW) * 0.5f;
            // the cast stands ON one sheet of paper: the sheet is the only drawn
            // surface, and each founder is an object standing on it
            GameUi.PaperSheet(_page, x0 - 28f, FounderDraftScreen.DockBandTop,
                              rowW + 56f, 140f, 3, 4f, null, "dock");
            for (int i = 0; i < n; i++)
            {
                var arch = _s.Archetypes[i] as JObject;
                int index = i;
                var chip = DrawnUI.Rect(_page, "chip", x0 + i * 142f,
                    FounderDraftScreen.DockBandTop + 12f, 128f, 128f);
                var hit = chip.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                hit.raycastTarget = true;
                GameUi.Shadow(chip, 22f, 106f, 84f, 16f);
                var port = GameUi.Picture(chip, "port",
                    ArtCache.SpritePath(ContentDb.Str(arch, "sprite")), 0f, 0f, 128f, 118f);
                var ring = GameUi.PenRing(chip, -9f, -8f, 146f, 144f, DrawnUI.Coral, i, 5f);
                ring.gameObject.SetActive(false);
                var b = chip.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.targetGraphic = hit;
                b.onClick.AddListener(() => Select(index, true));
                _chips.Add(chip);
            }
        }

        // ── selecting ──────────────────────────────────────────────────────────

        public void Select(int i, bool animate)
        {
            int n = _s.Archetypes.Count;
            if (n == 0) return;
            _s.SelIndex = ((i % n) + n) % n;               // wrapi
            _s.SelArch = _s.Archetypes[_s.SelIndex] as JObject;
            JObject arch = _s.SelArch;

            for (int c = 0; c < _chips.Count; c++)
            {
                bool selected = c == _s.SelIndex;
                Transform ring = _chips[c].Find("ring");
                if (ring != null) ring.gameObject.SetActive(selected);
                Transform port = _chips[c].Find("port");
                // only the PORTRAIT mutes, and it mutes by a light grey multiply at
                // FULL opacity — fading ink toward cream is what washed these out
                var img = port != null ? port.GetComponent<RawImage>() : null;
                if (img != null)
                    img.color = selected ? Color.white : new Color(0.74f, 0.74f, 0.74f, 1f);
                DrawnUI.SetTopLeft(_chips[c], _chips[c].anchoredPosition.x,
                    FounderDraftScreen.DockBandTop + (selected ? 2f : 12f));
            }

            _dName.text = ContentDb.Str(arch, "name");
            _dTag.text = ContentDb.Str(arch, "tagline");
            int cashTotal = 8000 + ContentDb.Int(arch, "start_cash_bonus", 0);
            _dCash.text = "$" + GameUi.Money(cashTotal);
            _dCash.color = cashTotal >= 8000 ? DrawnUI.Sage : DrawnUI.Coral;
            _dPerk.text = "★ " + ContentDb.Str(arch, "perk");
            if (_tip != null) _tip.gameObject.SetActive(false);

            RedrawPips(arch);
            if (_hero != null) _hero.Play(ContentDb.Str(arch, "id"), ContentDb.Str(arch, "sprite"));
        }

        void RedrawPips(JObject arch)
        {
            for (int i = _pipsHost.childCount - 1; i >= 0; i--)
                Object.Destroy(_pipsHost.GetChild(i).gameObject);
            for (int i = _traitHost.childCount - 1; i >= 0; i--)
                Object.Destroy(_traitHost.GetChild(i).gameObject);

            var stats = new Dictionary<string, int>();
            var statsJ = arch != null ? arch["stats"] as JObject : null;
            if (statsJ != null)
                foreach (var kv in statsJ) stats[kv.Key] = ContentDb.Int(statsJ, kv.Key, 0);
            GameUi.StatPips(_pipsHost, 44f, 172f, stats,
                FounderDraftScreen.StatNames, FounderDraftScreen.StatLabels);

            var traits = new Dictionary<string, int>();
            var traitsJ = arch != null ? arch["traits"] as JObject : null;
            if (traitsJ != null)
                foreach (var kv in traitsJ) traits[kv.Key] = ContentDb.Int(traitsJ, kv.Key, 3);
            GameUi.TraitPips(_traitHost, 44f, 462f, traits, GameState.TRAIT_NAMES);
        }

        void ShowTraitTip(string tname)
        {
            if (_tip == null || _s.SelArch == null) return;
            var traits = _s.SelArch["traits"] as JObject;
            int level = ContentDb.Int(traits, tname, 3);
            string head = string.Format("{0}  {1}/5", tname.ToUpper(), level);
            if (_tip.gameObject.activeSelf && _tipHead.text == head)
            {
                _tip.gameObject.SetActive(false);
                return;
            }
            string rule;
            if (!SimEngine.TRAIT_RULES.TryGetValue(tname, out rule)) rule = "";
            _tipHead.text = head;
            _tipBody.text = rule;
            _tip.gameObject.SetActive(true);
        }

        void ShowStatTip(string sname)
        {
            if (_tip == null || _s.SelArch == null) return;
            var stats = _s.SelArch["stats"] as JObject;
            int level = ContentDb.Int(stats, sname, 0);
            string head = string.Format("{0}  {1}/5", sname.ToUpper(), level);
            if (_tip.gameObject.activeSelf && _tipHead.text == head)
            {
                _tip.gameObject.SetActive(false);
                return;
            }
            string rule;
            if (!StatRules.TryGetValue(sname, out rule)) rule = "";
            _tipHead.text = head;
            _tipBody.text = rule;
            _tip.gameObject.SetActive(true);
        }

        /// the hero walks IN (owner: select needed motion): a slide from stage left
        /// with a settle, the shadow fading up under the feet
        public void Entrance()
        {
            if (_heroHolder == null) return;
            var boot = Boot.Instance;
            if (boot == null) return;
            boot.StartCoroutine(WalkOn(_heroHolder, _heroShadow));
        }

        static System.Collections.IEnumerator WalkOn(RectTransform hero, RectTransform shadow)
        {
            var hg = DrawnUI.Group(hero);
            var sg = shadow != null ? DrawnUI.Group(shadow) : null;
            float homeX = hero.anchoredPosition.x;
            float t = 0f;
            hg.alpha = 0f;
            if (sg != null) sg.alpha = 0f;
            while (t < 0.34f)
            {
                t += Time.unscaledDeltaTime;
                float k = DrawnUI.EaseOutCubic(t / 0.34f);
                if (hero == null) yield break;
                hero.anchoredPosition = new Vector2(Mathf.Lerp(homeX - 90f, homeX, k),
                                                    hero.anchoredPosition.y);
                hg.alpha = Mathf.Min(1f, k / 0.65f);
                if (sg != null && t > 0.12f) sg.alpha = Mathf.Min(1f, (t - 0.12f) / 0.22f);
                yield return null;
            }
            hero.anchoredPosition = new Vector2(homeX, hero.anchoredPosition.y);
            hg.alpha = 1f;
            if (sg != null) sg.alpha = 1f;
        }

        public void LockIn()
        {
            if (_s.SelArch == null || _stamping) return;
            var boot = Boot.Instance;
            if (boot == null) { _s.TransitionTo(2); return; }
            _stamping = true;
            boot.StartCoroutine(Stamp());
        }

        System.Collections.IEnumerator Stamp()
        {
            var stamp = DrawnUI.HandLabel(_page, "LOCKED IN", 400f, 380f, 110f, DrawnUI.Coral, 900f);
            GameUi.Tilt(stamp.rectTransform, -0.14f);
            var g = DrawnUI.Group(stamp.rectTransform);
            g.alpha = 0f;
            float t = 0f;
            while (t < 0.14f)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / 0.14f);
                stamp.rectTransform.localScale = Vector3.one * Mathf.Lerp(2.6f, 1f, k * k);
                g.alpha = Mathf.Min(1f, k / 0.7f);
                yield return null;
            }
            stamp.rectTransform.localScale = Vector3.one;
            g.alpha = 1f;
            yield return new WaitForSecondsRealtime(0.45f);
            if (stamp != null) Object.Destroy(stamp.gameObject);
            _stamping = false;
            _s.TransitionTo(2);
        }
    }
}
