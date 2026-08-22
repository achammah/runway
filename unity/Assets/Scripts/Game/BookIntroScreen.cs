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
    /// THE FIRST PAGE OF THE BOOK — book_intro_screen.gd, ported.
    ///
    /// The run opens the way a business memoir opens: the founder's own first entry,
    /// written that night, scrollable — and below it FIELD NOTES, what the founder
    /// THINKS they know. Working assumptions, plainly marked as such, never the hidden
    /// truth. SETTLE IN closes the book and the game begins; the entry never replays.
    ///
    /// TWO GATES ON ONE DOOR (owner: never wait on an empty log with an unlocked door
    /// — the door is what must wait):
    ///   · THE ENTRY. Until day one lands the page says it is being written and the
    ///     notes stay hidden, because notes floating under a placeholder read as noise.
    ///   · THE PAINT. While the garage is still rendering, SETTLE IN yields to a
    ///     breathing line; the director releases it on success OR final failure.
    /// </summary>
    public sealed class BookIntroScreen : AppScreen
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            ScreenRegistry.Register(AppState.Book, typeof(BookIntroScreen));
        }

        const float ColW = 1060f;

        RectTransform _viewport;
        RectTransform _column;
        TextMeshProUGUI _entry;
        TextMeshProUGUI _paintLine;
        RectTransform _door;
        readonly List<RectTransform> _notes = new List<RectTransform>();
        ShelfScroll _scroll;
        float _columnH;
        bool _waiting = true;
        bool _holdingEntry = true;
        bool _holdingPaint;
        float _t;

        GameState State
        {
            get { return RunDriver.Current != null ? RunDriver.Current.State : null; }
        }

        protected override void OnBuild()
        {
            DrawnUI.FullFill(Rect, "ground", DrawnUI.Hex("22262B"), true);
            var sheet = GameUi.PaperSheet(Rect, 168f, 42f, 1200f, 916f, 3, 4f, null, "sheet");
            // `_Sheet._draw` opens on draw_rect(Rect2(8, 12, w, h), Color(0, 0, 0, 0.3)).
            // The book is the one page that is nothing BUT a sheet on a dark ground, so
            // its shadow carries the whole sense of a real page: at 0.18, thrown 7×9,
            // it lay flat on the desk instead of lifting off it.
            var shadow = sheet.Find("shadow");
            if (shadow != null)
            {
                var img = shadow.GetComponent<Image>();
                if (img != null) img.color = new Color(0f, 0f, 0f, 0.3f);
                DrawnUI.SetTopLeft(shadow as RectTransform, 8f, 12f);
            }

            string company = State != null ? State.CompanyName : "";
            DrawnUI.HandLabel(Rect, company.ToUpper() + " — a founder's logbook",
                              220f, 76f, 42f, DrawnUI.Ink, 1080f);
            DrawnUI.HandLabel(Rect, "entry one — the night the lease was signed",
                              222f, 132f, 26f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f), 1080f);

            _viewport = DrawnUI.Rect(Rect, "viewport", 220f, 182f, 1080f, 660f);
            _column = DrawnUI.Rect(_viewport, "column", 0f, 0f, ColW, 660f);

            _entry = Add("the first entry is being written…", 30f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f));
            BuildFieldNotes();

            // the drawn scrollbar
            var trackImg = DrawnUI.Fill(Rect, "track", DrawnUI.WithAlpha(DrawnUI.Ink, 0.18f),
                                        1324f, 182f, 3f, 660f);
            trackImg.raycastTarget = false;
            var thumbRt = DrawnUI.Rect(Rect, "thumb", 1322f, 182f, 7f, 60f);
            var thumb = thumbRt.gameObject.AddComponent<Image>();
            thumb.color = DrawnUI.WithAlpha(DrawnUI.Coral, 0.85f);
            thumb.raycastTarget = false;
            _scroll = ShelfScroll.Attach(_viewport, _column, 660f, _columnH, thumb);

            _door = DrawnUI.Rect(Rect, "door", 1020f, 878f, 320f, 64f);
            GameUi.InkWord(_door, "SETTLE IN  →", 0f, 0f, 320f, 64f, 40f, DrawnUI.Coral,
                           () => Finish());
            _door.gameObject.SetActive(false);

            // the entry may already be in hand (the prefetch landed before the book
            // opened); otherwise the driver feeds it the moment it does
            var payload = Payload as string;
            var driver = RunDriver.Current;
            if (driver != null)
            {
                driver.FoundingLanded += FeedEntry;
                driver.PaintSettled += SetPaintDone;
            }
            if (!string.IsNullOrEmpty(payload)) FeedEntry(payload);
        }

        void OnDestroy()
        {
            var driver = RunDriver.Current;
            if (driver == null) return;
            driver.FoundingLanded -= FeedEntry;
            driver.PaintSettled -= SetPaintDone;
        }

        // ── the entry ──────────────────────────────────────────────────────────

        /// The founding arrives whenever the prefetch lands — before or after this
        /// screen opened. An EMPTY entry never opens the notes: the placeholder keeps
        /// breathing and the retry upstream gets its chance.
        public void FeedEntry(string text)
        {
            if (text == null || text.Trim().Length == 0) return;
            _waiting = false;
            _holdingEntry = false;
            if (_entry != null)
            {
                _entry.text = text;
                _entry.color = DrawnUI.Ink;
                _entry.ForceMeshUpdate();
                Relayout();
            }
            for (int i = 0; i < _notes.Count; i++)
                if (_notes[i] != null) _notes[i].gameObject.SetActive(true);
            var driver = RunDriver.Current;
            if (driver != null)
            {
                driver.BookShowedEntry = true;
                // THE BOOK HOLDS UNTIL THE PAINT DRIES (owner: never show the default
                // room): if the founding's own scene is still rendering, the door waits.
                if (driver.WarmPaint == PaintStatus.Painting) HoldForPaint();
            }
            RefreshDoor();
        }

        void HoldForPaint()
        {
            if (_holdingPaint) return;
            _holdingPaint = true;
            if (_paintLine == null)
                _paintLine = DrawnUI.HandLabel(Rect, "✎ painting your garage…", 1000f, 894f, 30f,
                                               DrawnUI.Coral, 400f);
            RefreshDoor();
        }

        public void SetPaintDone()
        {
            _holdingPaint = false;
            if (_paintLine != null) { Destroy(_paintLine.gameObject); _paintLine = null; }
            RefreshDoor();
        }

        /// ONE gate for the door: SETTLE IN exists only when the entry is on the page
        /// and no paint hold remains.
        void RefreshDoor()
        {
            if (_door == null) return;
            bool open = !_holdingEntry && !_holdingPaint;
            if (_door.gameObject.activeSelf != open) _door.gameObject.SetActive(open);
        }

        // ── the field notes ────────────────────────────────────────────────────

        void BuildFieldNotes()
        {
            GameState s = State;
            AddRule();
            Add("FIELD NOTES — what I think I know", 32f, DrawnUI.Pen);
            double tam = 0.0, life = 40.0;
            string marketLine = "";
            if (s != null)
            {
                tam = s.Beliefs != null ? s.Beliefs.Tam : (s.Theta != null ? s.Theta.Tam : 0.0);
                life = s.Beliefs != null && s.Beliefs.LifetimeWk > 0.0
                    ? s.Beliefs.LifetimeWk : (s.Theta != null ? s.Theta.LifetimeWk : 40.0);
                object ml = s.GetMeta("market_line", "");
                marketLine = ml != null ? ml.ToString() : "";
            }
            Add(string.Format(
                "the market, as far as I can tell: ~{0} people who might buy this. A customer "
                + "probably stays ≈ {1} weeks. All of this is a guess I will be correcting for "
                + "months.{2}", GameUi.Compact(Gd.ToInt(tam)), Gd.ToInt(life),
                marketLine.Length > 0 ? "  (" + marketLine + ")" : ""),
                27f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.8f));

            Add("the money in town:", 29f, DrawnUI.Blue);
            if (s != null)
                for (int i = 0; i < s.Investors.Count; i++)
                {
                    Investor inv = s.Investors[i];
                    Add(string.Format("{0} — {1}. \"{2}\"", inv.Name, inv.Archetype, inv.Thesis),
                        25f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f));
                }

            Add("already selling to my customers:", 29f, DrawnUI.Blue);
            if (s != null)
                for (int i = 0; i < s.Rivals.Count; i++)
                {
                    Rival rv = s.Rivals[i];
                    string what = rv.What ?? "";
                    Add(string.Format("{0} — looks {1}{2}", rv.Name, SimEngine.Fuzz(rv.Strength),
                        what.Length > 0 ? ". " + what : ""),
                        25f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f));
                }

            Add("everything above is honest. none of it is verified.", 24f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f));

            // hidden until the entry lands
            for (int i = 1; i < _notes.Count; i++)
                if (_notes[i] != null) _notes[i].gameObject.SetActive(false);
        }

        TextMeshProUGUI Add(string text, float size, Color col)
        {
            var t = DrawnUI.HandLabel(_column, text, 0f, _columnH, size, col, ColW,
                                      TextAlignmentOptions.TopLeft);
            t.ForceMeshUpdate();
            float h = Mathf.Max(t.preferredHeight, size * 1.4f);
            t.rectTransform.sizeDelta = new Vector2(ColW, h);
            _columnH += h + 18f;
            _notes.Add(t.rectTransform);
            return t;
        }

        void AddRule()
        {
            var rt = DrawnUI.Rect(_column, "rule", 0f, _columnH + 10f, ColW, 8f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = DrawnUI.WobbleLineSprite(1060, 3f, 33, 1.5f, 5, 4);
            img.color = DrawnUI.WithAlpha(DrawnUI.Ink, 0.35f);
            img.raycastTarget = false;
            _columnH += 26f;
            _notes.Add(rt);
        }

        /// The entry grew: everything under it moves down and the scroller is told.
        void Relayout()
        {
            _columnH = 0f;
            for (int i = 0; i < _notes.Count; i++)
            {
                RectTransform rt = _notes[i];
                if (rt == null) continue;
                DrawnUI.SetTopLeft(rt, 0f, _columnH);
                var t = rt.GetComponent<TextMeshProUGUI>();
                float h = 26f;
                if (t != null)
                {
                    t.ForceMeshUpdate();
                    h = Mathf.Max(t.preferredHeight, t.fontSize * 1.4f);
                    rt.sizeDelta = new Vector2(ColW, h);
                    h += 18f;
                }
                _columnH += h;
            }
            if (_column != null) _column.sizeDelta = new Vector2(ColW, Mathf.Max(_columnH, 660f));
            if (_scroll != null) _scroll.SetContentHeight(_columnH);
        }

        void Update()
        {
            _t += Time.unscaledDeltaTime;
            // A18 #11: on a first run nothing subscribes the driver to the
            // director yet, so PaintSettled can have NO raiser while this door
            // holds — the poll is the belt to the event's braces. The door
            // opens on done OR failed; only live painting holds it.
            if (_holdingPaint && Boot.Instance != null && Boot.Instance.Director != null
                && Boot.Instance.Director.WarmStatus != Runway.Llm.PaintStatus.Painting)
                SetPaintDone();
            if (_paintLine != null)
                _paintLine.color = DrawnUI.WithAlpha(DrawnUI.Coral,
                    0.65f + 0.35f * Mathf.Sin(_t * 2.4f));
            if (_waiting && _entry != null)
                _entry.color = DrawnUI.WithAlpha(DrawnUI.Ink,
                    0.32f + 0.18f * Mathf.Sin(_t * 2.2f));
        }
    }
}
