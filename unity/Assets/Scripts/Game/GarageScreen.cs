using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Runway.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;
using Runway.Core;
using Runway.Llm;

namespace Runway.Game
{
    /// <summary>
    /// THE GARAGE VIEW — garage_view_screen.gd, ported. The 60 Seconds! principle: the
    /// room IS the save file. Everything you own is visibly in it, the money is a
    /// physical pile, product is the whiteboard, users are the wall chart, your crew
    /// sits here with their mood drawn on them, and the room decays with morale.
    ///
    /// Decisions happen in THE JOURNAL: a paper book with the week's situation and a
    /// line to WRITE YOUR OWN MOVE, which the world adjudicates.
    ///
    /// TWO ROOMS, AND ONLY TWO. When the director delivers this week's painting it
    /// BECOMES the room — the player looks up from the page into the room their
    /// decision made. Until then the room is drawn from the shipped object sprites:
    /// the money pile, the whiteboard, the wall chart, the things in your bag, the
    /// decay that tracks morale and the badges that track flags. The Godot build has a
    /// third and fourth rung (the assembled stage and the spot-patch room, both built
    /// on background libraries this project does not carry); they are out of scope and
    /// noted as such in the compile-risk ledger.
    /// </summary>
    public sealed class GarageScreen : AppScreen
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            ScreenRegistry.Register(AppState.Garage, typeof(GarageScreen));
        }

        const string Gv = "sprites/gv/";
        const float BreathFps = 12f;

        static readonly string[] FunFacts =
        {
            "FUN FACT — Slack began as the chat tool inside a failed video game called Glitch.",
            "FUN FACT — Instagram pivoted from Burbn, a check-in app with too many features.",
            "FUN FACT — YouTube launched as a video dating site. Nobody dated.",
            "FUN FACT — Twitter came out of Odeo, a podcast platform made obsolete by Apple.",
            "FUN FACT — Shopify was a snowboard shop that liked its own checkout better than its boards.",
            "FUN FACT — Netflix mailed DVDs for a decade before the pivot that ate television.",
            "FUN FACT — Nintendo made playing cards for 80 years before video games.",
            "FUN FACT — Nokia started as a paper mill. Then rubber boots. Then phones.",
        };

        /// where each ownable thing lives in the room: position, then its height
        static readonly string[] ItemSpotIds =
        {
            "itm_laptop", "itm_dads_server", "itm_houseplant", "itm_guitar", "itm_savings_jar",
            "itm_energy_drinks", "itm_paddle", "itm_hoodie", "itm_textbook", "itm_goodwill",
            "itm_dignity", "itm_idea_napkin", "itm_roommate",
        };
        static readonly Vector3[] ItemSpots =
        {
            new Vector3(390f, 545f, 110f), new Vector3(1040f, 700f, 210f),
            new Vector3(1252f, 420f, 110f), new Vector3(120f, 620f, 240f),
            new Vector3(1345f, 430f, 95f), new Vector3(545f, 555f, 85f),
            new Vector3(742f, 505f, 62f), new Vector3(50f, 560f, 120f),
            new Vector3(640f, 575f, 75f), new Vector3(455f, 236f, 72f),
            new Vector3(1090f, 250f, 62f), new Vector3(668f, 560f, 58f),
            new Vector3(250f, 872f, 110f),
        };

        // ── the room ───────────────────────────────────────────────────────────
        RectTransform _room;
        RectTransform _noteLayer;
        RectTransform _journal;
        RawImage _composed;
        Image _redVignette;
        TextMeshProUGUI _hudLabel;
        TextMeshProUGUI _moneyLabel;
        RectTransform _moneyTag;
        RectTransform _capPaper;
        TextMeshProUGUI _capLabel;
        RectTransform _openBtn;
        TextMeshProUGUI _openWord;
        TextMeshProUGUI _paintRibbon;
        readonly Dictionary<string, RawImage> _spots = new Dictionary<string, RawImage>();
        /// what the STATE wants shown, kept apart from what is actually on screen — a
        /// painting hides the whole drawn room without the state forgetting its own room
        readonly Dictionary<string, bool> _wanted = new Dictionary<string, bool>();
        readonly List<RectTransform> _crew = new List<RectTransform>();
        bool _painted;

        JournalSpreads _spreads;
        float _t;
        float _lastBreath = -1f;
        int _lastCash;
        bool _weekOpened;
        bool _over;

        /// The room currently on stage. The turn hands it the week's painting.
        public static GarageScreen Room;

        public bool WorldBusy { get; set; }
        public bool Adjudicating
        {
            get { return _spreads != null && _spreads.Adjudicating; }
        }
        public bool JournalOpen
        {
            get { return _journal != null && _journal.gameObject.activeSelf; }
        }
        public string ComposedPath { get; private set; }

        public GameState State
        {
            get { return RunDriver.Current != null ? RunDriver.Current.State : null; }
        }
        public RunDriver Driver { get { return RunDriver.Current; } }
        public ContentDb Deck { get { return Driver != null ? Driver.Deck : null; } }
        public JObject CurrentEvent = new JObject();
        public JObject LastOutcome = new JObject();
        public readonly List<string> WeekLog = new List<string>();
        public readonly List<string> Departures = new List<string>();
        public readonly Dictionary<string, int> WeekPrev = new Dictionary<string, int>();

        // ══ build ══════════════════════════════════════════════════════════════

        protected override void OnBuild()
        {
            Room = this;
            TurnRunner.Room = this;
            if (State == null)
            {
                DrawnUI.FullFill(Rect, "ground", DrawnUI.Hex("22262B"), true);
                DrawnUI.HandLabel(Rect, "no run behind this room — start a new game",
                    0f, 480f, 34f, DrawnUI.Cream, RunwayPaths.StageWidth,
                    TextAlignmentOptions.Top);
                return;
            }
            _lastCash = State.Cash;
            if (LastOutcome.Count == 0 && Driver != null && Driver.LastOutcome != null)
                LastOutcome = (JObject)Driver.LastOutcome.DeepClone();

            _room = DrawnUI.FullRect(Rect, "room");
            BuildRoom();
            Runway.Effects.Motes.GarageBulb(_room);
            BuildHud();
            Runway.Effects.GlowSprites.Apply(_room, Runway.Effects.GlowScene.Garage);

            _redVignette = DrawnUI.FullFill(Rect, "red", new Color(0.85f, 0.3f, 0.25f, 0f));
            _noteLayer = DrawnUI.FullRect(Rect, "notes");
            _journal = DrawnUI.FullRect(Rect, "journal");
            _journal.gameObject.SetActive(false);
            _spreads = new JournalSpreads(this, _journal);

            SyncRoom(true);
            StartWeek();
        }

        void OnDestroy()
        {
            if (Room == this) Room = null;
            if (TurnRunner.Room == this) TurnRunner.Room = null;
        }

        /// THE PLAIN ROOM: cream ground, a floor band, and every object this company
        /// actually owns standing in it. It is the FLOOR of the room, never the finished
        /// picture — the moment the director delivers a painting, that becomes the room.
        void BuildRoom()
        {
            DrawnUI.FullFill(_room, "wall", DrawnUI.Cream, true);
            DrawnUI.Fill(_room, "floor", DrawnUI.WithAlpha(DrawnUI.Sage, 0.22f),
                         0f, RunwayPaths.StageHeight * 0.72f,
                         RunwayPaths.StageWidth, RunwayPaths.StageHeight * 0.28f);
            var horizon = DrawnUI.Rect(_room, "horizon", 0f, RunwayPaths.StageHeight * 0.72f - 4f,
                                       RunwayPaths.StageWidth, 10f);
            var hImg = horizon.gameObject.AddComponent<Image>();
            hImg.sprite = DrawnUI.WobbleLineSprite((int)RunwayPaths.StageWidth, 4f, 61, 2.2f, 21, 4);
            hImg.color = DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f);
            hImg.raycastTarget = false;

            Spot("money", Gv + "money_1.png", 70f, 760f, 180f, true);
            Spot("board", Gv + "board_1.png", 200f, 270f, 210f, true);
            Spot("chart", Gv + "chart_1.png", 952f, 300f, 150f, true);
            Spot("decay_trash", Gv + "decay_trash.png", 320f, 850f, 120f, false);
            Spot("decay_pizza", Gv + "decay_pizza.png", 430f, 830f, 140f, false);
            Spot("decay_flies", Gv + "decay_flies.png", 700f, 560f, 80f, false);
            Spot("decay_graffiti", Gv + "decay_graffiti.png", 1000f, 490f, 140f, false);
            Spot("badge_camp", Gv + "badge_camp.png", 520f, 250f, 110f, false);
            Spot("badge_launched", Gv + "badge_launched.png", 640f, 240f, 130f, false);
            for (int i = 0; i < ItemSpotIds.Length; i++)
            {
                Vector3 s = ItemSpots[i];
                Spot("item_" + ItemSpotIds[i], ArtCache.SpritePath(ItemSpotIds[i]),
                     s.x, s.y, s.z, false, ItemSpotIds[i]);
            }
            BuildCrew();
        }

        /// T6 — click a thing, get the paper note saying what it is for.
        void Spot(string key, string art, float x, float y, float height, bool visibleNow,
                  string itemId = null)
        {
            var img = GameUi.Picture(_room, key, art, x, y, height * 1.6f, height);
            img.gameObject.SetActive(visibleNow);
            _spots[key] = img;
            _wanted[key] = visibleNow;
            if (itemId == null) return;
            var hit = img.rectTransform.gameObject.AddComponent<Button>();
            img.raycastTarget = true;
            hit.transition = Selectable.Transition.None;
            hit.targetGraphic = img;
            string id = itemId;
            float ax = x;
            float ay = y;
            hit.onClick.AddListener(() => ItemNote(id, ax, ay));
        }

        void ItemNote(string itemId, float x, float y)
        {
            for (int i = _noteLayer.childCount - 1; i >= 0; i--)
                Destroy(_noteLayer.GetChild(i).gameObject);
            JObject def = Deck != null ? Deck.Item(itemId) : null;
            string blurb = ContentDb.Str(def, "blurb");
            float noteH = 60f + Mathf.Ceil(blurb.Length / 34f) * 30f;
            var note = GameUi.PaperSheet(_noteLayer, Mathf.Clamp(x - 80f, 12f, 1224f),
                Mathf.Max(12f, y - noteH - 10f), 300f, noteH, 5, 3f, null, "note");
            GameUi.Tilt(note, 0.015f);
            DrawnUI.HandLabel(note, Deck != null ? Deck.ItemName(itemId) : itemId,
                              14f, 6f, 24f, DrawnUI.Ink, 272f);
            DrawnUI.HandLabel(note, blurb, 14f, 40f, 24f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.8f), 272f, TextAlignmentOptions.Top);
            Sfx.Tick();
            StartCoroutine(FadeNoteAway(note));
        }

        IEnumerator FadeNoteAway(RectTransform note)
        {
            var g = DrawnUI.Group(note);
            g.alpha = 0f;
            yield return DrawnUI.FadeTo(g, 1f, 0.12f);
            yield return new WaitForSecondsRealtime(2.6f);
            yield return DrawnUI.FadeTo(g, 0f, 0.35f);
            if (note != null) Destroy(note.gameObject);
        }

        /// Sprite crew is the ART-LESS fallback. In a painted room nobody is pasted in:
        /// the people arrive painted into the composed scene or they do not arrive.
        void BuildCrew()
        {
            for (int i = 0; i < _crew.Count; i++) if (_crew[i] != null) Destroy(_crew[i].gameObject);
            _crew.Clear();
            if (State == null) return;
            float[] xs = { 612f, 800f, 964f, 1120f, 470f };
            var f = DrawnUI.Rect(_room, "founder", xs[0] - 12f, 617f, 214f, 214f);
            GameUi.Picture(f, "art", ArtCache.SpritePath("chr_arch_" + State.ArchetypeId),
                           0f, 0f, 214f, 214f);
            _crew.Add(f);
            int slot = 1;
            for (int i = 0; i < State.Cofounders.Count && slot < xs.Length; i++, slot++)
            {
                Cofounder cf = State.Cofounders[i];
                int loy = Driver != null ? Driver.Loyalty(i) : 70;
                string mood = loy > 70 ? "happy" : (loy > 30 ? "neutral" : "resentful");
                string slug = CrewSlug(cf.Role);
                var c = DrawnUI.Rect(_room, "cofounder", xs[slot], 628f, 200f, 200f);
                GameUi.Picture(c, "art",
                    string.Format("sprites/cf_{0}_{1}.png", slug, mood), 0f, 0f, 200f, 200f);
                _crew.Add(c);
            }
            for (int i = 0; i < State.Employees.Count && slot < xs.Length; i++, slot++)
            {
                string bs = GameState.BurnoutState(State.Employees[i].Burnout);
                string mood = bs == "cooked" || bs == "gone" ? "resentful"
                    : (bs == "frayed" ? "neutral" : "happy");
                var e = DrawnUI.Rect(_room, "hire", xs[slot], 628f, 200f, 200f);
                GameUi.Picture(e, "art", string.Format("sprites/cf_technical_{0}.png", mood),
                               0f, 0f, 200f, 200f);
                _crew.Add(e);
            }
        }

        static string CrewSlug(string role)
        {
            string k = (role ?? "").ToLower();
            if (k.Contains("business") || k.Contains("sales")) return "business";
            if (k.Contains("design")) return "design";
            if (k.Contains("idea") || k.Contains("hustler")) return "idea";
            return "technical";
        }

        void BuildHud()
        {
            var plate = GameUi.PaperSheet(Rect, 24f, 14f, 430f, 52f, 1, 3f, null, "hud");
            GameUi.Tilt(plate, -0.004f);
            _hudLabel = DrawnUI.HandLabel(plate, "", 16f, 4f, 29f, DrawnUI.Ink, 400f);

            _moneyTag = GameUi.PaperSheet(Rect, 64f, 700f, 180f, 48f, 2, 3f, null, "moneytag");
            GameUi.Tilt(_moneyTag, -0.02f);
            _moneyLabel = DrawnUI.HandLabel(_moneyTag, "$0", 14f, 4f, 29f, DrawnUI.Ink, 160f);

            _capPaper = GameUi.PaperSheet(Rect, 1164f, 306f, 118f, 138f, 3, 3f, null, "cap");
            GameUi.Tilt(_capPaper, 0.045f);
            _capLabel = DrawnUI.HandLabel(_capPaper, "", 0f, 34f, 24f, DrawnUI.Ink, 118f,
                                          TextAlignmentOptions.Top);

            _openBtn = DrawnUI.Rect(Rect, "openjournal", 560f, 930f, 420f, 76f);
            var style = DrawnUI.PaperStyle.Button;
            style.Seed = 14;
            var card = DrawnUI.PaperCard(_openBtn, new Vector2(420f, 76f), 0f, 0f, style, "card");
            card.SetSiblingIndex(0);
            _openWord = DrawnUI.HandLabel(_openBtn, "OPEN THE JOURNAL", 0f, 0f, 30f,
                                          DrawnUI.Ink, 420f, TextAlignmentOptions.Center);
            _openWord.rectTransform.anchorMin = Vector2.zero;
            _openWord.rectTransform.anchorMax = Vector2.one;
            _openWord.rectTransform.offsetMin = Vector2.zero;
            _openWord.rectTransform.offsetMax = Vector2.zero;
            var hit = _openBtn.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            var ob = _openBtn.gameObject.AddComponent<Button>();
            ob.transition = Selectable.Transition.None;
            ob.targetGraphic = hit;
            ob.onClick.AddListener(OpenJournal);

            // the binder's doorway: a smaller drawn tab beside the journal button
            var bb = GameUi.PaperSheet(Rect, 1272f, 936f, 240f, 56f, 4, 3f, null, "bindertab");
            DrawnUI.HandLabel(bb, "THE BINDER (TAB)", 0f, 12f, 24f, DrawnUI.Ink, 240f,
                              TextAlignmentOptions.Top);
            var bh = bb.gameObject.AddComponent<Image>();
            bh.color = new Color(0f, 0f, 0f, 0f);
            bh.raycastTarget = true;
            var bbtn = bb.gameObject.AddComponent<Button>();
            bbtn.transition = Selectable.Transition.None;
            bbtn.targetGraphic = bh;
            bbtn.onClick.AddListener(OpenBinder);
        }

        // ══ the painted room ═══════════════════════════════════════════════════

        /// SHOW A COMPOSED ROOM. Public: the week loop hands this the scene its own turn
        /// produced, and the room BECOMES that scene instead of throwing it away.
        public bool AdoptComposed(string path, bool aligned)
        {
            if (string.IsNullOrEmpty(path) || _room == null) return false;
            if (_composed == null)
            {
                var rt = DrawnUI.FullRect(_room, "composed");
                _composed = rt.gameObject.AddComponent<RawImage>();
                _composed.raycastTarget = false;
                _composed.enabled = false;
                rt.SetSiblingIndex(1);
            }
            ComposedPath = path;
            var boot = Boot.Instance;
            if (boot == null) return false;
            boot.StartCoroutine(SheetLoop.LoadTexture(RunwayPaths.FileUrl(path), tex =>
            {
                if (_composed == null || tex == null) return;
                _composed.texture = tex;
                _composed.enabled = true;
                // the painting is the room now: nothing drawn is laid over it, because
                // its walls are not the walls the sprites were placed against
                HideDrawnRoom(true);
                GarageInk.Apply(_composed, tex);
            }));
            return true;
        }

        /// THE SWAP IS ATOMIC: everything the drawn room put on screen goes down
        /// together, or there are two garages in the frame.
        void HideDrawnRoom(bool hidden)
        {
            _painted = hidden;
            foreach (var kv in _spots)
            {
                if (kv.Value == null) continue;
                bool want;
                if (!_wanted.TryGetValue(kv.Key, out want)) want = false;
                kv.Value.gameObject.SetActive(want && !hidden);
            }
            for (int i = 0; i < _crew.Count; i++)
                if (_crew[i] != null) _crew[i].gameObject.SetActive(!hidden);
            if (_capPaper != null) _capPaper.gameObject.SetActive(!hidden);
            // in a room we did not lay out, the only ground we can trust is the calm
            // top strip the composition law keeps clear
            if (_moneyTag != null)
                DrawnUI.SetTopLeft(_moneyTag, hidden ? 24f : 64f, hidden ? 76f : 700f);
        }

        /// THE HONEST ROOM (owner: the stock room read as a bug while day-one art
        /// rendered): while a render is in flight the room says so, in pen.
        public void SetPainting(bool on)
        {
            if (on && _paintRibbon == null)
            {
                _paintRibbon = DrawnUI.HandLabel(Rect, "✎ your room is being painted…",
                                                 40f, 986f, 26f, DrawnUI.Coral, 600f);
            }
            else if (!on && _paintRibbon != null)
            {
                Destroy(_paintRibbon.gameObject);
                _paintRibbon = null;
            }
        }

        // ══ the room reflects the state ════════════════════════════════════════

        public void SyncRoom(bool instant = false)
        {
            if (State == null) return;
            _hudLabel.text = string.Format("{0}  ·  WEEK {1}",
                (State.CompanyName ?? "").ToUpper(), State.Week);

            int mtier = 1;
            if (State.Cash > 30000) mtier = 4;
            else if (State.Cash > 12000) mtier = 3;
            else if (State.Cash > 3000) mtier = 2;
            Retex("money", Gv + "money_" + mtier + ".png", 70f, 760f, 180f);
            Retex("board", Gv + "board_" + Gd.Clampi(1 + State.Product / 26, 1, 4) + ".png",
                  200f, 270f, 210f);
            int ctier = 1;
            if (State.Traction > 60) ctier = 4;
            else if (State.Traction > 20) ctier = 3;
            else if (State.Traction > 4) ctier = 2;
            Retex("chart", Gv + "chart_" + ctier + ".png", 952f, 300f, 150f);

            for (int i = 0; i < ItemSpotIds.Length; i++)
                Show("item_" + ItemSpotIds[i], State.HasItem(ItemSpotIds[i]));
            Show("decay_pizza", State.Morale < 30);
            Show("decay_trash", State.Morale < 45);
            Show("decay_flies", State.Morale < 22);
            Show("decay_graffiti", State.Morale < 15);
            Show("badge_camp", State.HasFlag("camp_alum"));
            Show("badge_launched", State.HasFlag("first_user"));

            _moneyLabel.text = GameUi.Cash(State.Cash);
            _moneyLabel.color = State.Cash < State.BurnPerWeek() * 2 ? DrawnUI.Coral : DrawnUI.Ink;
            if (_capLabel != null)
                _capLabel.text = string.Format("{0:0}%\nyours", State.FounderPct);
            if (!instant && State.Cash != _lastCash)
            {
                Sfx.Cash();
                _moneyLabel.color = State.Cash > _lastCash ? DrawnUI.Sage : DrawnUI.Coral;
            }
            _lastCash = State.Cash;
            BuildCrew();
            if (_painted) HideDrawnRoom(true);   // the crew was just rebuilt under it
        }

        void Show(string key, bool visible)
        {
            _wanted[key] = visible;
            RawImage img;
            if (!_spots.TryGetValue(key, out img) || img == null) return;
            img.gameObject.SetActive(visible && !_painted);
        }

        void Retex(string key, string art, float x, float y, float h)
        {
            RawImage img;
            if (!_spots.TryGetValue(key, out img) || img == null) return;
            GameUi.Rebind(img, art, x, y, h * 1.6f, h);
        }

        void Update()
        {
            if (State == null) return;
            _t += Time.unscaledDeltaTime;
            if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.B)) OpenBinder();

            // THE ROOM BREATHES ON THE ROOM'S OWN CLOCK — 12fps, like everything drawn.
            float t = Mathf.Floor(_t * BreathFps) / BreathFps;
            if (Mathf.Approximately(t, _lastBreath)) return;
            _lastBreath = t;
            if (_openBtn != null && _openBtn.gameObject.activeSelf)
            {
                float p = 1f + Mathf.Sin(t * 3.2f) * 0.03f;
                _openBtn.localScale = new Vector3(p, p, 1f);
            }
            Runway.Effects.GlowSprites.SetRed(State.Cash < 0);
            Runway.Audio.RunwayMix.SetRed(State.Cash < 0);
            if (_redVignette != null)
            {
                // the room itself panics about money: pulsing red edges when starving
                float a = 0f;
                if (State.WeeksInRed > 0) a = 0.10f + 0.08f * Mathf.Sin(t * 2.4f);
                else if (State.Cash < State.BurnPerWeek() * 2) a = 0.05f + 0.03f * Mathf.Sin(t * 1.6f);
                _redVignette.color = new Color(0.85f, 0.3f, 0.25f, a);
            }
            if (_paintRibbon != null)
                _paintRibbon.color = DrawnUI.WithAlpha(DrawnUI.Coral,
                    0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * 0.9f)));
        }

        void OpenBinder()
        {
            Sfx.CardFlip(); BinderScreen.Open(State);
        }

        // ══ the journal ════════════════════════════════════════════════════════

        public void OpenJournal()
        {
            if (_openBtn != null) _openBtn.gameObject.SetActive(false);
            _journal.gameObject.SetActive(true); Sfx.CardFlip();
            _spreads.Open();
            // THE PAD COMES UP OFF THE DESK (owner: "a nice normal animation to open"):
            // it rises from below with a slight straightening, the dim fades with it.
            StartCoroutine(RaisePad());
        }

        IEnumerator RaisePad()
        {
            var g = DrawnUI.Group(_journal);
            float t = 0f;
            while (t < 0.28f)
            {
                t += Time.unscaledDeltaTime;
                float k = DrawnUI.EaseOutCubic(t / 0.28f);
                if (_journal == null) yield break;
                _journal.anchoredPosition = new Vector2(0f, Mathf.Lerp(-90f, 0f, k));
                GameUi.Tilt(_journal, Mathf.Lerp(0.012f, 0f, k));
                g.alpha = Mathf.Min(1f, k / 0.7f);
                yield return null;
            }
            _journal.anchoredPosition = Vector2.zero;
            GameUi.Tilt(_journal, 0f);
            g.alpha = 1f;
        }

        public void CloseJournal()
        {
            Sfx.CardFlip();
            StartCoroutine(DropPad());
        }

        IEnumerator DropPad()
        {
            var g = DrawnUI.Group(_journal);
            float t = 0f;
            while (t < 0.2f)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / 0.2f);
                if (_journal == null) yield break;
                _journal.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, -70f, k * k));
                g.alpha = 1f - k;
                yield return null;
            }
            _journal.gameObject.SetActive(false);
            _journal.anchoredPosition = Vector2.zero;
            g.alpha = 1f;
            if (_openBtn != null && !_over) _openBtn.gameObject.SetActive(true);
        }

        // ══ the weekly loop ════════════════════════════════════════════════════

        /// WEEK 1 MUST EXIST. The counter opens at 1 and only advances once a week has
        /// actually been PLAYED, so the first page anyone reads says WEEK 1.
        public void StartWeek()
        {
            GameState st = State;
            if (st == null) return;
            if (_weekOpened || st.Week > 1) st.Week += 1;
            _weekOpened = true;
            WeekLog.Clear();
            Departures.Clear();
            _spreads.ResetWeek();

            // the people consumable: loyalty drains every week; empty = they walk
            for (int i = st.Cofounders.Count - 1; i >= 0; i--)
            {
                int loy = Driver.Loyalty(i) - 6;
                Driver.SetLoyalty(i, loy);
                if (loy > 0) continue;
                Cofounder cf = st.Cofounders[i];
                string who = (cf.Role ?? "?") + " cofounder";
                bool vested = !string.IsNullOrEmpty(cf.Vesting);
                if (vested)
                {
                    st.FounderPct += (cf.EquityDiluted.HasValue ? cf.EquityDiluted.Value : cf.Equity) * 0.75;
                    Departures.Add(who + " walked. The cliff clawed back most of their shares.");
                }
                else
                {
                    Departures.Add(who + " walked — WITH every share. No vesting. The classic.");
                }
                st.Cofounders.RemoveAt(i);
                st.Morale = Gd.Maxi(0, st.Morale - 10);
                WeekLog.Add(who + " left: −10 morale");
                Driver.Record.LogEvent(st.Week,
                    new JObject { ["id"] = "departure", ["title"] = who + " quit" },
                    "loyalty ran dry", null);
            }

            int burn = st.BurnPerWeek();
            st.Cash -= burn;
            WeekLog.Add(string.Format("rent + ramen{0}: −${1}",
                st.Employees.Count == 0 ? "" : " + payroll", GameUi.Money(burn)));
            List<string> staff = st.WeeklyStaffTick();
            for (int i = 0; i < staff.Count; i++) WeekLog.Add(staff[i]);
            if (st.Cash < 0 && st.Employees.Count > 0)
            {
                st.NoteMissedPayroll();
                WeekLog.Add("payroll missed. they noticed.");
            }

            int passiveBuild = 2 + st.Competence("build");
            if (st.HasItem("itm_laptop")) passiveBuild += 2;
            if (st.HasItem("itm_dads_server")) passiveBuild += 1;
            st.Product = Gd.Clampi(st.Product + passiveBuild, 0, 100);
            WeekLog.Add("shipped: +" + passiveBuild + " product");
            if (st.Product >= 40)
            {
                int gained = 1 + st.Competence("sell") / 2 + st.Hype / 20;
                st.Traction += gained;
                WeekLog.Add("new users: +" + gained);
            }
            st.Morale += st.Competence("grit") / 3;
            if (st.HasItem("itm_houseplant"))
            {
                st.Morale += 1;
                WeekLog.Add("the plant listened: +1 morale");
            }
            if (st.StructureId == "solo")
            {
                st.Morale -= 1;
                WeekLog.Add("the 2am dread, alone: −1 morale");
            }

            // T12 — money IS the food: you starve over weeks, not instantly
            if (st.Cash < 0)
            {
                st.WeeksInRed += 1;
                st.Morale = Gd.Maxi(0, st.Morale - 6);
                WeekLog.Add(string.Format(
                    "IN THE RED — week {0} of 3. Payroll is a promise now.", st.WeeksInRed));
                for (int i = 0; i < st.Cofounders.Count; i++)
                    Driver.SetLoyalty(i, Driver.Loyalty(i) - 10);
                if (st.WeeksInRed >= 3)
                {
                    Die(string.Format(
                        "Ramen Zero — three weeks without money. The runway ended, week {0}.",
                        st.Week));
                    return;
                }
            }
            else
            {
                if (st.WeeksInRed > 0) WeekLog.Add("back in the black. everyone exhales.");
                st.WeeksInRed = 0;
            }
            st.ClampiMeters();

            if (st.HasFlag("payroll_crisis") || st.HasFlag("down_round"))
            {
                string why = st.HasFlag("payroll_crisis") ? "missed payroll twice" : "down round";
                GameState.EraMove down = st.Demote(why);
                st.Flags.Remove("down_round");
                if (down.Changed)
                    Departures.Add("MOVED DOWN — " + why + ". The boxes barely fit the shame.");
            }
            GameState.EraMove up = st.AdvanceEraIfReady();
            if (up.Changed)
                Sfx.Win();
            if (up.Changed)
                Departures.Add(string.Format("MOVED UP — {0} → {1}. Bigger room, bigger rent.",
                    up.From, up.To));

            // a timebomb that reaches zero IS this week's card — it outranks the draw
            for (int i = st.Timebombs.Count - 1; i >= 0; i--)
            {
                st.Timebombs[i].WeeksLeft -= 1;
                if (st.Timebombs[i].WeeksLeft > 0) continue;
                string evId = st.Timebombs[i].Event ?? "";
                st.Timebombs.RemoveAt(i);
                JObject bomb;
                if (Deck != null && Deck.Events.TryGetValue(evId, out bomb) && bomb != null)
                {
                    CurrentEvent = bomb;
                    AfterWeekSetup();
                    return;
                }
            }

            CurrentEvent = NextCard();
            AfterWeekSetup();
        }

        /// THE INVISIBLE SEAM: a generated card if one is pooled and it is not a
        /// near-duplicate of a recent week, else the authored deck deals.
        JObject NextCard()
        {
            var boot = Boot.Instance;
            if (boot != null && boot.Generator != null)
            {
                JObject gen = boot.Generator.TakeGeneratedCard(CoreSnapshot.From(State));
                if (gen != null) return gen;
            }
            JObject authored = Deck != null ? Deck.DrawAuthored(State, Driver.Rng) : null;
            return authored ?? new JObject();
        }

        void AfterWeekSetup()
        {
            SyncRoom();
            var boot = Boot.Instance;
            if (boot != null && boot.Generator != null)
                boot.Generator.Prefetch(CoreSnapshot.From(State));
            if (Driver != null) Driver.SaveIfWeekTurned();
            if (_openBtn != null) _openBtn.gameObject.SetActive(true);
            if (_openWord != null)
                _openWord.text = "OPEN THE JOURNAL — WEEK " + State.Week + " AWAITS";
        }

        /// THE DREAD BEAT: lights out, a tick, then the new week reveals itself.
        public void NextWeek()
        {
            if (_over) return;
            StartCoroutine(DreadBeat());
        }

        IEnumerator DreadBeat()
        {
            _journal.gameObject.SetActive(false);
            if (_openBtn != null) _openBtn.gameObject.SetActive(false);
            var dark = DrawnUI.FullFill(Rect, "dark", new Color(0.05f, 0.05f, 0.06f, 0f), true);
            Sfx.Tick();
            string fact = FunFacts[Driver.Rng.RandiRange(0, FunFacts.Length - 1)];
            var line = DrawnUI.HandLabel(Rect, fact, 240f, 490f, 22f,
                DrawnUI.WithAlpha(DrawnUI.Cream, 0f), 1060f, TextAlignmentOptions.Top);
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / 0.35f);
                dark.color = new Color(0.05f, 0.05f, 0.06f, k);
                line.color = DrawnUI.WithAlpha(DrawnUI.Cream, k);
                yield return null;
            }
            yield return new WaitForSecondsRealtime(1.1f);
            yield return DrawnUI.FadeTo(DrawnUI.Group(line.rectTransform), 0f, 0.3f);
            if (line != null) Destroy(line.gameObject);
            StartWeek();
            t = 0f;
            while (t < 0.5f)
            {
                t += Time.unscaledDeltaTime;
                dark.color = new Color(0.05f, 0.05f, 0.06f, 1f - Mathf.Clamp01(t / 0.5f));
                yield return null;
            }
            if (dark != null) Destroy(dark.gameObject);
        }

        public void Die(string cause)
        {
            if (State == null) return;
            State.Dead = true;
            State.DeathCause = cause;
            Sfx.Death();
            if (Driver != null && Driver.Record != null) Driver.Record.LogDeath(State.Week, cause);
            _over = true;
            StartCoroutine(EndAfter(new JObject { ["death"] = cause }, 1f));
        }

        public void Victory()
        {
            _over = true;
            StartCoroutine(EndAfter(new JObject { ["victory"] = true }, 0.3f));
        }

        IEnumerator EndAfter(JObject result, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (Driver != null) Driver.AfterGrind(result);
        }

        public bool Over { get { return _over; } }
    }
}
