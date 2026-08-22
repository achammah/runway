using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;
using Runway.Audio;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// PAGE 6 — PACK YOUR BAG. Four slots. Everything else stays in your old life.
    ///
    /// EVERYTHING YOU OWN IS ONE SHEET OF PAPER with your things laid out ON it. They
    /// used to be fifteen cream tiles with printed borders — a sticker sheet. The tile
    /// was doing one real job, giving dark ink art a light ground on a near-black
    /// stage, so that job moved to ONE drawn surface and the items became objects on
    /// it, each with a contact shadow and a pen ring when packed.
    ///
    /// THE SHELF HAS SECTIONS AND IT SCROLLS (owner asked for both): gear, the pitch,
    /// comforts, and — when the trade earns them — your trade's own tools.
    ///
    /// WHAT IS PACKED IS WRITTEN ON THE BOX. A moving box already stands here doing
    /// nothing, and a shipping label is exactly what a moving box is written on, so the
    /// manifest is stuck to it and the third cream panel is gone.
    ///
    /// EXACTLY WHAT IT DOES TO YOU (owner: "we should see EXACTLY how each buffs/nerfs
    /// CRUCIAL characteristics"): the arithmetic sits above the joke, and the running
    /// total under the shelf is computed BY THE ENGINE — this screen never does trait
    /// maths of its own.
    /// </summary>
    public sealed class DraftBagPage
    {
        readonly FounderDraftScreen _s;
        RectTransform _page;
        readonly Dictionary<string, RectTransform> _tiles = new Dictionary<string, RectTransform>();

        RawImage _detailArt;
        TextMeshProUGUI _detailName;
        TextMeshProUGUI _detailBlurb;
        TextMeshProUGUI _detailCost;
        RectTransform _detailMods;
        RectTransform _detailPanel;

        TextMeshProUGUI _slotsLabel;
        TextMeshProUGUI _emptyNote;
        RectTransform _packedRow;
        TextMeshProUGUI _summary;
        TextMeshProUGUI _loadoutNote;
        RectTransform _loadoutHost;
        Button _launch;
        TextMeshProUGUI _launchWord;
        Vector2 _boxAnchor = new Vector2(1230f, 560f);

        public DraftBagPage(FounderDraftScreen s) { _s = s; }

        public RectTransform Build()
        {
            _page = DrawnUI.FullRect(_s.Rect, "page_bag");
            FounderDraftScreen.Dim(_page);
            FounderDraftScreen.Heading(_page, "PACK YOUR BAG", 56f, 60f, 26f);
            DrawnUI.HandLabel(_page, "4 slots. Everything else stays in your old life.",
                64f, 116f, 28f, DrawnUI.WithAlpha(DrawnUI.Cream, 0.85f));

            BuildShelf();
            BuildDetail();
            BuildBox();
            BuildSummary();

            _s.Nav(_page, "←", 48f, 930f, 100f, 70f, 30f, () => _s.TransitionTo(5));
            _launch = _s.Nav(_page, "SIGN & QUIT YOUR JOB  →", 1050f, 920f, 450f, 84f, 32f,
                             _s.DoLaunch);
            _launchWord = _launch.GetComponentInChildren<TextMeshProUGUI>();
            Refresh();
            return _page;
        }

        // ── everything you own ─────────────────────────────────────────────────

        void BuildShelf()
        {
            GameUi.PaperSheet(_page, 44f, 166f, 676f, 566f, 2, 4f, null, "shelf");
            DrawnUI.HandLabel(_page, "everything you own", 78f, 178f, 28f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.62f));
            GameUi.HandRule(_page, 80f, 214f, 604f, DrawnUI.WithAlpha(DrawnUI.Sage, 0.8f), 9);

            var buckets = new Dictionary<string, List<JObject>>
            {
                { "GEAR", new List<JObject>() }, { "THE PITCH", new List<JObject>() },
                { "COMFORTS", new List<JObject>() }, { "YOUR TRADE", new List<JObject>() },
            };
            for (int i = 0; i < _s.Deck.ItemList.Count; i++)
            {
                JObject def = _s.Deck.ItemList[i];
                var rqWhat = def["requires_what"] as JArray;
                var rqWho = def["requires_who"] as JArray;
                if (rqWhat != null && rqWhat.Count > 0 && !Contains(rqWhat, _s.BizWhat)) continue;
                if (rqWho != null && rqWho.Count > 0 && !Contains(rqWho, _s.BizWho)) continue;
                var tags = def["tags"] as JArray;
                if ((rqWhat != null && rqWhat.Count > 0) || (rqWho != null && rqWho.Count > 0))
                    buckets["YOUR TRADE"].Add(def);
                else if (Contains(tags, "morale")) buckets["COMFORTS"].Add(def);
                else if (Contains(tags, "sales") || Contains(tags, "marketing"))
                    buckets["THE PITCH"].Add(def);
                else buckets["GEAR"].Add(def);
            }

            var viewport = DrawnUI.Rect(_page, "shelfview", 52f, 228f, 660f, 484f);
            var grid = DrawnUI.Rect(viewport, "shelfgrid", 0f, 0f, 660f, 484f);
            float px = 12f;
            float py = 10f;
            int gi = 0;
            string[] order = { "GEAR", "THE PITCH", "COMFORTS", "YOUR TRADE" };
            for (int b = 0; b < order.Length; b++)
            {
                List<JObject> list = buckets[order[b]];
                if (list.Count == 0) continue;
                if (px > 12f) { px = 12f; py += 132f; }
                DrawnUI.HandLabel(grid, order[b].ToLower(), px + 6f, py - 6f, 22f,
                    DrawnUI.WithAlpha(DrawnUI.Sage, 0.95f));
                py += 26f;
                for (int i = 0; i < list.Count; i++)
                {
                    JObject def = list[i];
                    if (px > 12f + 4f * 122f) { px = 12f; py += 138f; }
                    gi++;
                    AddTile(grid, def, px, py, gi);
                    px += 122f;
                }
            }
            float contentH = py + 138f;
            grid.sizeDelta = new Vector2(660f, contentH);

            // the drawn track: pencil line, coral thumb
            var track = DrawnUI.Fill(_page, "track", DrawnUI.WithAlpha(DrawnUI.Ink, 0.15f),
                                     710f, 232f, 3f, 476f);
            track.raycastTarget = false;
            var thumbRt = DrawnUI.Rect(_page, "thumb", 708f, 232f, 7f, 60f);
            var thumb = thumbRt.gameObject.AddComponent<Image>();
            thumb.color = DrawnUI.WithAlpha(DrawnUI.Coral, 0.85f);
            thumb.raycastTarget = false;
            ShelfScroll.Attach(viewport, grid, 484f, contentH, thumb);
            if (contentH > 484f)
                DrawnUI.HandLabel(_page, "▼ scroll — there's more on the shelf", 78f, 740f, 22f,
                    DrawnUI.WithAlpha(DrawnUI.Cream, 0.75f));
        }

        void AddTile(RectTransform grid, JObject def, float x, float y, int gi)
        {
            string id = ContentDb.Str(def, "id");
            var tile = DrawnUI.Rect(grid, "tile", x, y, 112f, 112f);
            var hit = tile.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            GameUi.Shadow(tile, 18f, 94f, 76f, 14f);
            GameUi.Picture(tile, "art", ArtCache.SpritePath(id), 0f, 4f, 112f, 104f, tex =>
            {
                // THE SHELF IS NEVER EMPTY. New things arrive before anyone has drawn
                // them; until the object exists it is a hand-labelled card, which is
                // what an unpacked thing in a cardboard box looks like anyway.
                if (tex != null) return;
                var card = GameUi.PaperSheet(tile, 8f, 6f, 96f, 84f, gi, 2.5f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.42f), "card");
                DrawnUI.HandLabel(card, ContentDb.Str(def, "name", "?"), 6f, 10f, 19f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.85f), 84f, TextAlignmentOptions.TopLeft);
            });
            var ring = GameUi.PenRing(tile, -10f, -8f, 132f, 128f, DrawnUI.Coral, gi, 5f);
            ring.gameObject.SetActive(false);

            var b = tile.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.targetGraphic = hit;
            b.onClick.AddListener(() => Toggle(id, ContentDb.Int(def, "carry_cost", 1)));
            var hover = tile.gameObject.AddComponent<TileHover>();
            hover.Bind(() => ShowDetail(def));
            _tiles[id] = tile;
        }

        // ── what the thing is FOR ──────────────────────────────────────────────

        void BuildDetail()
        {
            _detailPanel = GameUi.PaperSheet(_page, 760f, 178f, 340f, 452f, 4, 4f, null, "detail");
            GameUi.Tilt(_detailPanel, 0.007f);
            var artRt = DrawnUI.Rect(_detailPanel, "detailart", 95f, 18f, 150f, 150f);
            _detailArt = artRt.gameObject.AddComponent<RawImage>();
            _detailArt.raycastTarget = false;
            _detailArt.enabled = false;
            // the thing's NAME is `_dlabel`; the arithmetic strip, the joke and the slot
            // cost under it are all `_label` — one printed line on a written card
            _detailName = DrawnUI.DisplayLabel(_detailPanel, "", 10f, 176f, 30f, DrawnUI.Ink,
                                               320f, TextAlignmentOptions.TopLeft);
            GameUi.HandRule(_detailPanel, 115f, 226f, 110f, DrawnUI.Coral, 10);
            _detailMods = DrawnUI.Rect(_detailPanel, "mods", 14f, 240f, 312f, 34f);
            _detailBlurb = DrawnUI.HandLabel(_detailPanel, "", 30f, 280f, 26f, DrawnUI.Ink, 280f,
                                             TextAlignmentOptions.TopLeft);
            _detailCost = DrawnUI.HandLabel(_detailPanel, "", 16f, 408f, 24f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 308f, TextAlignmentOptions.TopLeft);
            if (_s.Deck.ItemList.Count > 0) ShowDetail(_s.Deck.ItemList[0]);
        }

        void ShowDetail(JObject def)
        {
            if (_detailName == null || def == null) return;
            string id = ContentDb.Str(def, "id");
            GameUi.Rebind(_detailArt, ArtCache.SpritePath(id), 95f, 18f, 150f, 150f);
            _detailName.text = ContentDb.Str(def, "name");
            _detailBlurb.text = ContentDb.Str(def, "blurb");
            for (int i = _detailMods.childCount - 1; i >= 0; i--)
                Object.Destroy(_detailMods.GetChild(i).gameObject);
            GameUi.TokenLine(_detailMods, 0f, 0f, 312f, ModTokens(def["trait_mods"] as JObject), 23f);
            int cost = ContentDb.Int(def, "carry_cost", 1);
            _detailCost.text = string.Format("takes {0} slot{1}{2}", cost, cost > 1 ? "s" : "",
                _s.Bag.Contains(id) ? "  ·  PACKED ✓" : "");
        }

        /// One item's trait arithmetic, gains before costs, in the trait order the whole
        /// game speaks. Sage gives, coral takes; a thing that bends nothing says so
        /// rather than leaving a silence the player has to interpret.
        static List<KeyValuePair<string, Color>> ModTokens(JObject mods)
        {
            var gains = new List<KeyValuePair<string, Color>>();
            var costs = new List<KeyValuePair<string, Color>>();
            for (int i = 0; i < GameState.TRAIT_NAMES.Count; i++)
            {
                string t = GameState.TRAIT_NAMES[i];
                int v = ContentDb.Int(mods, t, 0);
                if (v > 0)
                    gains.Add(new KeyValuePair<string, Color>(
                        string.Format("+{0} {1}", v, t.ToUpper()), DrawnUI.Sage));
                else if (v < 0)
                    costs.Add(new KeyValuePair<string, Color>(
                        string.Format("−{0} {1}", -v, t.ToUpper()), DrawnUI.Coral));
            }
            if (gains.Count == 0 && costs.Count == 0)
                return new List<KeyValuePair<string, Color>>
                {
                    new KeyValuePair<string, Color>("bends none of your traits",
                        DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f)),
                };
            gains.AddRange(costs);
            return gains;
        }

        // ── the box and its shipping label ─────────────────────────────────────

        void BuildBox()
        {
            // Godot alpha-trims env_boxes (408x408 → 280x396 of real ink)
            // before fitting; untrimmed, the square aspect shrank the stack to
            // 340x340 and drowned the bottom box under the label (P3). The
            // same trim here is a uvRect window + the trimmed aspect.
            {
                var boxRt = DrawnUI.Rect(_page, "box", 1147f, 126f, 325f, 460f);
                var boxImg = boxRt.gameObject.AddComponent<UnityEngine.UI.RawImage>();
                boxImg.raycastTarget = false;
                boxImg.enabled = false;
                boxImg.uvRect = new Rect(63f / 408f, 7f / 408f, 280f / 408f, 396f / 408f);
                ArtCache.Load(ArtCache.SpritePath("env_boxes"), tex =>
                {
                    if (boxImg == null || tex == null) return;
                    boxImg.texture = tex;
                    boxImg.enabled = true;
                });
            }
            var tag = GameUi.PaperSheet(_page, 1178f, 352f, 288f, 238f, 3, 4f, null, "label");
            GameUi.TiltCentre(tag, -0.018f);
            _boxAnchor = new Vector2(1178f + 144f, 352f + 119f);
            // the label's HEADING is `_dlabel`; the ruled lines under it, "nothing packed
            // yet." and every packed name are `_label` — a printed form, filled in by hand
            _slotsLabel = DrawnUI.DisplayLabel(tag, "IN THE BAG · 0/4", 18f, 12f, 28f,
                                               DrawnUI.Ink, 252f);
            GameUi.HandRule(tag, 18f, 52f, 252f, DrawnUI.WithAlpha(DrawnUI.Sage, 0.8f), 11);
            // four printed rules, one per slot: ruled, the same emptiness reads as a
            // form waiting to be filled in rather than a blank cream field
            for (int i = 0; i < 4; i++)
                GameUi.HandRule(tag, 18f, 96f + i * 36f, 252f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.15f), 12 + i);
            _emptyNote = DrawnUI.HandLabel(tag, "nothing packed yet.", 18f, 76f, 25f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 252f);
            _packedRow = DrawnUI.Rect(tag, "packed", 18f, 72f, 252f, 148f);
        }

        void RefreshPacked()
        {
            if (_packedRow == null) return;
            for (int i = _packedRow.childCount - 1; i >= 0; i--)
                Object.Destroy(_packedRow.GetChild(i).gameObject);
            if (_emptyNote != null) _emptyNote.gameObject.SetActive(_s.Bag.Count == 0);
            for (int i = 0; i < _s.Bag.Count; i++)
            {
                string id = _s.Bag[i];
                var line = DrawnUI.Rect(_packedRow, "line", 0f, i * 36f, 252f, 36f);
                var hit = line.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                hit.raycastTarget = true;
                DrawnUI.HandLabel(line, "✓", 0f, -2f, 26f, DrawnUI.Coral, 24f);
                DrawnUI.HandLabel(line, _s.Deck.ItemName(id), 26f, -2f, 26f, DrawnUI.Ink, 224f);
                var b = line.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.targetGraphic = hit;
                string pick = id;
                b.onClick.AddListener(() => Toggle(pick, 0));   // take it back out
            }
        }

        // ── the running total ──────────────────────────────────────────────────

        void BuildSummary()
        {
            GameUi.PaperSheet(_page, 48f, 788f, 940f, 122f, 4, 3f, null, "summary");
            _summary = DrawnUI.HandLabel(_page, "", 70f, 796f, 28f, DrawnUI.Ink, 896f);
            _summary.textWrappingMode = TextWrappingModes.NoWrap;
            _loadoutHost = DrawnUI.Rect(_page, "loadout", 70f, 834f, 896f, 32f);
            _loadoutNote = DrawnUI.HandLabel(_page, "", 70f, 862f, 20f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 890f, TextAlignmentOptions.TopLeft);
        }

        // ── packing ────────────────────────────────────────────────────────────

        void Toggle(string id, int cost)
        {
            if (_s.Bag.Contains(id))
            {
                _s.Bag.Remove(id);
                Paint(id, false);
            }
            else
            {
                if (_s.BagSlotsUsed() + cost > 4) return;   // the bag is full; nothing moves
                _s.Bag.Add(id);
                Paint(id, true);
            }
            // AFTER the full-bag refusal, never before it: `_toggle_bag` returns out of
            // the shake branch without ever reaching its `_sfx_click.play()`, so a bag
            // that will not take the thing stays silent and the refusal reads as one.
            Sfx.Cash();
            _s.RefreshCapLine();
        }

        void Paint(string id, bool packed)
        {
            RectTransform tile;
            if (!_tiles.TryGetValue(id, out tile) || tile == null) return;
            Transform ring = tile.Find("ring");
            if (ring != null) ring.gameObject.SetActive(packed);
            Transform art = tile.Find("art");
            var img = art != null ? art.GetComponent<RawImage>() : null;
            if (img != null)
                img.color = packed ? Color.white : new Color(0.9f, 0.9f, 0.9f, 1f);
        }

        public void Refresh()
        {
            if (_page == null) return;
            if (_slotsLabel != null)
                _slotsLabel.text = string.Format("IN THE BAG · {0}/4", _s.BagSlotsUsed());
            RefreshPacked();
            if (_summary != null)
            {
                int n = _s.Cofounders.Count;
                string co = (_s.CompanyName ?? "").Trim();
                _summary.text = string.Format(
                    "{0} · {1} · {2} {3} · you keep {4:0}% · ~${5} day one",
                    co.Length > 0 ? co : "?",
                    _s.SelArch != null ? ContentDb.Str(_s.SelArch, "name", "?") : "?",
                    n, n == 1 ? "cofounder" : "cofounders",
                    _s.FounderPct(), GameUi.Money(_s.DayOneCash()));
            }
            RefreshLoadout();
            if (_launchWord != null)
            {
                string blocked = _s.BlockedReason();
                _launchWord.text = blocked.Length > 0 ? blocked : "SIGN & QUIT YOUR JOB  →";
                _launchWord.color = blocked.Length > 0
                    ? DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f) : DrawnUI.Ink;
            }
        }

        /// THE BAG TOTALLED, COMPUTED BY THE ENGINE — a scratch state carries the
        /// archetype and what is packed, and SimEngine answers what that founder now
        /// is, in its own words. What LANDS, not what was promised: a +1 on a trait
        /// already at 5 is a slot spent on nothing, and the player is told so here.
        void RefreshLoadout()
        {
            if (_loadoutHost == null) return;
            for (int i = _loadoutHost.childCount - 1; i >= 0; i--)
                Object.Destroy(_loadoutHost.GetChild(i).gameObject);

            var probe = new GameState();
            var baseTraits = _s.SelArch != null ? _s.SelArch["traits"] as JObject : null;
            if (baseTraits != null)
            {
                var d = new Dictionary<string, int>();
                foreach (var kv in baseTraits) d[kv.Key] = ContentDb.Int(baseTraits, kv.Key, 3);
                probe.Traits = d;
            }
            probe.Items = new List<string>(_s.Bag);

            var tokens = new List<KeyValuePair<string, Color>>
            {
                new KeyValuePair<string, Color>("your loadout:",
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f)),
            };
            var gains = new List<KeyValuePair<string, Color>>();
            var costs = new List<KeyValuePair<string, Color>>();
            var capped = new List<string>();
            for (int i = 0; i < GameState.TRAIT_NAMES.Count; i++)
            {
                string t = GameState.TRAIT_NAMES[i];
                int raw = probe.ItemTraitDelta(t);
                if (raw == 0) continue;
                int baseV;
                if (!probe.Traits.TryGetValue(t, out baseV)) baseV = 3;
                int landed = probe.TraitLevel(t) - Gd.Clampi(baseV, 1, 5);
                if (landed > 0)
                    gains.Add(new KeyValuePair<string, Color>(
                        string.Format("+{0} {1}", landed, t), DrawnUI.Sage));
                else if (landed < 0)
                    costs.Add(new KeyValuePair<string, Color>(
                        string.Format("−{0} {1}", -landed, t), DrawnUI.Coral));
                if (landed != raw) capped.Add(t);
            }
            if (gains.Count == 0 && costs.Count == 0)
                tokens.Add(new KeyValuePair<string, Color>("nothing bent yet",
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.4f)));
            else { tokens.AddRange(gains); tokens.AddRange(costs); }
            GameUi.TokenLine(_loadoutHost, 0f, 0f, 896f, tokens, 25f, false);

            if (_loadoutNote == null) return;
            List<string> eff = SimEngine.TraitEffects(probe);
            string note = eff.Count == 0
                ? "nothing switched on yet — the founder card says what each trait unlocks"
                : "in play:  " + string.Join("   ·   ", eff.ToArray());
            if (capped.Count > 0)
                note += "      (already at the ceiling: " + string.Join(", ", capped.ToArray()) + ")";
            _loadoutNote.text = note;
        }

        static bool Contains(JArray arr, string v)
        {
            if (arr == null) return false;
            foreach (JToken t in arr) if (t.ToString() == v) return true;
            return false;
        }
    }
}
