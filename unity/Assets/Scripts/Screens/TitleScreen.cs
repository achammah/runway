using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;

namespace Runway.Screens
{
    /// <summary>
    /// THE LIVING TITLE — title_screen.gd, ported.
    ///
    /// The whole scene is ONE coherent living painting: a 48-frame loop played at
    /// 12fps, streamed off disk. The FIRST frame comes up now and the rest arrive
    /// while the title breathes — the fix for a launch that was SUPER SLOW when all 48
    /// full-screen frames loaded before the first pixel.
    ///
    /// Any key arms the menu (a harness keeps the old any-key contract and goes
    /// straight to a fresh run). NEW GAME and CONTINUE are REAL paper buttons — cream
    /// card, wobbled ink border, the word above the paper — and either one opens the
    /// slot table on its own full screen: the title art dims away and three big paper
    /// dossiers sit on the stage.
    /// </summary>
    public sealed class TitleScreen : AppScreen
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            ScreenRegistry.Register(AppState.Title, typeof(TitleScreen));
        }

        const string VideoFormat = "title/video/frame_{0:00}.png";
        const int VideoFrames = 48;
        const float VideoFps = 12f;

        static readonly Color CreamM = DrawnUI.Hex("F2EAD3");
        static readonly Color InkM = DrawnUI.Hex("1E1E1E");
        static readonly Color PenM = DrawnUI.Hex("E86A5C");

        RectTransform _root;      // the painting, which breathes
        RectTransform _menu;
        SheetLoop _film;
        bool _armed;
        bool _menuOpen;
        float _t;

        protected override void OnBuild()
        {
            _root = DrawnUI.FullRect(Rect, "painting");
            _root.pivot = new Vector2(0.5f, 0.5f);

            if (RunwayPaths.ArtExists(string.Format(VideoFormat, 1)))
            {
                _film = SheetLoop.Attach(_root, "film");
                _film.PlaySequence(VideoFormat, VideoFrames, VideoFps);
            }
            else
            {
                // no film on disk: the painted still is the title, unchanged
                var rt = DrawnUI.FullRect(_root, "still");
                var raw = rt.gameObject.AddComponent<RawImage>();
                raw.raycastTarget = false;
                Run(LoadStill(raw));
            }

            // which build am I actually running — the question that cost a whole session
            DrawnUI.HandLabel(Rect, BuildStamp.Value, 16f, 996f, 18f,
                              new Color(0.12f, 0.12f, 0.12f, 0.4f));

            Run(Arm());
        }

        IEnumerator LoadStill(RawImage raw)
        {
            string url = RunwayPaths.ArtUrl("title/title_screen.png");
            if (url.Length == 0) yield break;
            Texture2D tex = null;
            yield return SheetLoop.LoadTexture(url, t => tex = t);
            if (tex != null && raw != null) raw.texture = tex;
        }

        IEnumerator Arm()
        {
            yield return new WaitForSecondsRealtime(0.4f);
            _armed = true;
        }

        void Update()
        {
            _t += Time.unscaledDeltaTime;
            // cinematic breathe — on the 12fps clock the painting itself runs at
            float breathe = 1f + 0.012f * Mathf.Sin(Mathf.Floor(_t * 12f) / 12f * 0.5f);
            if (_root != null) _root.localScale = new Vector3(breathe, breathe, 1f);

            if (!_armed || _menuOpen) return;
            bool pressed = Input.anyKeyDown
                           || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
            if (!pressed) return;
            _armed = false;

            // harnesses keep the old any-key contract; a person gets the menu
            var boot = Boot.Instance;
            if (boot != null && (boot.Harness || Env.Flag("RUNWAY_FIRSTFLOW")))
            {
                Finish(null);
                return;
            }
            ShowMenu();
        }

        // ── THE MENU (owner: two buttons that ease in after the key press) ─────

        void ShowMenu()
        {
            _menuOpen = true;
            if (_menu != null) Destroy(_menu.gameObject);
            _menu = DrawnUI.FullRect(Rect, "menu");
            BuildMenuButtons();
        }

        void BuildMenuButtons()
        {
            ClearMenu();
            SaveSlotInfo[] slots = Slots();
            bool anySave = false;
            for (int i = 0; i < slots.Length; i++) if (slots[i].Exists) anySave = true;

            // the rules, always one click away
            DrawnUI.FlatButton(_menu, "how it works", 1310f, 962f, 200f, 44f, 24f,
                               DrawnUI.WithAlpha(CreamM, 0.6f), PenM,
                               () => Boot.Instance.OpenOverlay(AppOverlay.HowTo));

            // the key is never locked away: reopen the desk anytime
            DrawnUI.FlatButton(_menu, "api key", 1140f, 962f, 150f, 44f, 24f,
                               DrawnUI.WithAlpha(CreamM, 0.6f), PenM,
                               () => Boot.Instance.OpenOverlay(AppOverlay.Keys));

            MenuButton("NEW GAME", 694f, 0.05f, () => PickSlot(true));
            if (anySave) MenuButton("CONTINUE", 790f, 0.18f, () => PickSlot(false));
            // no saves: NEW GAME is the only door
        }

        void MenuButton(string text, float y, float delay, System.Action onClick)
        {
            var style = DrawnUI.PaperStyle.Button;
            style.Seed = 594 + 360;   // _PaperBtn: int(position.x) + int(size.x)
            var btn = DrawnUI.PaperButton(_menu, text, 594f, y + 26f, 360f, 72f, 40f,
                                          InkM, PenM, onClick, 1.045f, style);
            Run(EnterUp(btn.GetComponent<RectTransform>(), y + 26f, y, delay, 0.3f, 0.34f));
        }

        /// modulate.a 0 -> 1 over 0.3 and position:y easing up over 0.34, after `delay`
        IEnumerator EnterUp(RectTransform rt, float fromY, float toY, float delay,
                            float fadeSecs, float riseSecs)
        {
            var g = DrawnUI.Group(rt);
            g.alpha = 0f;
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            if (rt == null) yield break;
            Run(DrawnUI.FadeTo(g, 1f, fadeSecs));
            yield return DrawnUI.RiseTo(rt, fromY, toY, riseSecs);
        }

        // ── THE SLOT TABLE, its own full screen ───────────────────────────────

        /// new_mode: clicking an empty card starts there (occupied = overwrite it);
        /// continue mode: only occupied cards respond.
        void PickSlot(bool newMode)
        {
            ClearMenu();

            var veil = DrawnUI.FullFill(_menu, "veil", DrawnUI.WithAlpha(DrawnUI.Stage, 0f), true);
            Run(Veil(veil));

            var title = DrawnUI.HandLabel(_menu,
                newMode ? "WHERE DOES THIS ONE LIVE?" : "YOUR COMPANIES",
                120f, 96f, 52f, CreamM);
            var tg = DrawnUI.Group(title.rectTransform);
            tg.alpha = 0f;
            Run(DrawnUI.FadeTo(tg, 1f, 0.35f));

            DrawnUI.HandLabel(_menu,
                newMode ? "a slot with a company in it gets overwritten" : "pick one to continue",
                124f, 172f, 27f, DrawnUI.WithAlpha(CreamM, 0.65f));

            SaveSlotInfo[] slots = Slots();
            for (int i = 0; i < slots.Length; i++) SlotCard(slots[i], i, newMode);

            DrawnUI.FlatButton(_menu, "←  back", 110f, 930f, 200f, 56f, 30f,
                               DrawnUI.WithAlpha(CreamM, 0.8f), PenM,
                               BuildMenuButtons);
        }

        IEnumerator Veil(Image veil)
        {
            float t = 0f;
            while (t < 0.3f)
            {
                t += Time.unscaledDeltaTime;
                if (veil == null) yield break;
                veil.color = DrawnUI.WithAlpha(DrawnUI.Stage, Mathf.Lerp(0f, 0.94f, t / 0.3f));
                yield return null;
            }
            if (veil != null) veil.color = DrawnUI.WithAlpha(DrawnUI.Stage, 0.94f);
        }

        void SlotCard(SaveSlotInfo s, int i, bool newMode)
        {
            const float CardW = 1160f;
            const float CardH = 196f;
            float restY = 250f + i * 226f;
            float startY = restY + 26f;
            bool exists = s.Exists;
            int slotN = s.Slot > 0 ? s.Slot : i + 1;

            var card = DrawnUI.Rect(_menu, "slot" + slotN, 190f, startY, CardW, CardH);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = new Vector2(190f + CardW * 0.5f, -(startY + CardH * 0.5f));

            var hit = card.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            var inner = DrawnUI.FullRect(card, "inner");
            var style = DrawnUI.PaperStyle.Button;
            style.Seed = 190 + (int)CardW;
            DrawnUI.PaperCard(inner, new Vector2(CardW, CardH), 0f, 0f, style);

            DrawnUI.HandLabel(inner, exists ? s.Company : "empty desk", 44f, 30f, 44f,
                              exists ? InkM : DrawnUI.WithAlpha(InkM, 0.42f));
            string det = exists
                ? string.Format("{0} · week {1} · last played {2}{3}",
                    s.Founder, s.Week, SaveSlots.Ago(s.Timestamp),
                    exists && newMode ? "   — overwrites" : "")
                : (newMode ? "start here" : "nothing yet");
            DrawnUI.HandLabel(inner, det, 46f, 108f, 28f, DrawnUI.WithAlpha(InkM, 0.65f));
            DrawnUI.HandLabel(inner, "slot " + slotN, 1020f, 30f, 30f, PenM);

            var btn = card.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = hit;
            bool live = exists || newMode;
            btn.interactable = live;
            btn.onClick.AddListener(() =>
            {
                if (newMode) Finish(new TitleChoice(slotN, true));
                else if (exists) Finish(new TitleChoice(slotN, false));
            });
            if (live)
            {
                var tint = card.gameObject.AddComponent<HoverTint>();
                tint.Setup(null, InkM, InkM, card, 1.02f);
            }
            Run(EnterUp(card, startY, restY, 0.07f * i, 0.3f, 0.32f));
        }

        SaveSlotInfo[] Slots()
        {
            var boot = Boot.Instance;
            if (boot != null && boot.Driver != null)
            {
                SaveSlotInfo[] rows = boot.Driver.ListSlots();
                if (rows != null && rows.Length > 0) return rows;
            }
            var fallback = new SaveSlotInfo[SaveSlots.SlotCount];
            for (int i = 0; i < fallback.Length; i++) fallback[i] = SaveSlots.Read(i + 1);
            return fallback;
        }

        void ClearMenu()
        {
            if (_menu == null) return;
            for (int i = _menu.childCount - 1; i >= 0; i--)
                Destroy(_menu.GetChild(i).gameObject);
        }
    }
}
