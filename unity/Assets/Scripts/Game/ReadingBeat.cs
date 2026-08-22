using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;

namespace Runway.Game
{
    /// <summary>
    /// THE BEAT BETWEEN WEEKS — loading_screen.gd, ported.
    ///
    /// Not a loading screen with a spinner: the READING beat. The week's consequences
    /// arrive within seconds of the lock; the painting takes 40-90s. Reading takes
    /// 30-60s. So the wait is spent doing the most interesting thing in the game —
    /// finding out what your decision actually did — and the room opens when you look up.
    ///
    /// Rules it holds itself to:
    ///  - The text is the content, not decoration. Lines arrive as if being written.
    ///  - It NEVER blocks on the image. A slow render still lets you read on.
    ///  - The player is never held once they are done reading AND the art is ready.
    ///  - No percentage. A pen stroke advances with real reported progress.
    ///
    ///     var l = ReadingBeat.Create(stage);
    ///     l.Begin("WEEK 7");
    ///     l.Say("You said", move);
    ///     l.Report(0.5f);
    ///     yield return l.Finish();
    /// </summary>
    public sealed class ReadingBeat : MonoBehaviour
    {
        const float SizeTitle = 56f;
        const float SizeBody = 34f;
        const float SizeLabel = 26f;
        /// Below this a screen reads as a glitch. Above it, a reader can settle.
        const float MinLife = 2f;
        /// Roughly how fast a person reads this prose — used only to PACE the reveal.
        const float ReadCps = 22f;
        const float CardW = 1080f;
        const float CardH = 868f;
        const float FooterH = 96f;
        const float TextH = CardH - 126f - FooterH;

        RectTransform _card;
        RectTransform _viewport;
        RectTransform _column;
        TextMeshProUGUI _more;
        TextMeshProUGUI _hint;
        Image _penFill;

        readonly List<string[]> _queue = new List<string[]>();
        readonly List<TextMeshProUGUI> _bodies = new List<TextMeshProUGUI>();
        float _t;
        float _nextRevealAt;
        float _target;
        float _amount;
        float _columnH;
        float _scroll;
        bool _done;
        bool _proceed;
        bool _draining;

        public static ReadingBeat Create(RectTransform parent)
        {
            var rt = DrawnUI.FullRect(parent, "beat");
            return rt.gameObject.AddComponent<ReadingBeat>();
        }

        public void Begin(string weekLabel)
        {
            var rt = GetComponent<RectTransform>();
            // nothing behind this is ready to touch
            var veil = DrawnUI.FullFill(rt, "veil", new Color(0.06f, 0.05f, 0.07f, 0.90f), true);
            var skip = gameObject.AddComponent<Button>();
            skip.transition = Selectable.Transition.None;
            skip.targetGraphic = veil;
            skip.onClick.AddListener(OnClick);

            _card = DrawnUI.Rect(rt, "card", 228f, 78f, CardW, CardH);
            var shadow = DrawnUI.Fill(_card, "shadow", new Color(0f, 0f, 0f, 0.24f), 9f, 11f, CardW, CardH);
            shadow.raycastTarget = false;
            var paper = DrawnUI.Fill(_card, "paper", DrawnUI.Cream, 0f, 0f, CardW, CardH);
            paper.raycastTarget = false;
            // faint ruling, so the text reads as written on paper
            for (float y = 132f; y < CardH - 90f; y += 44f)
            {
                var r = DrawnUI.Fill(_card, "rule", DrawnUI.WithAlpha(DrawnUI.Sage, 0.30f),
                                     84f, y, CardW - 168f, 1.5f);
                r.raycastTarget = false;
            }
            DrawnUI.AddInkEdge(_card, new Vector2(CardW, CardH), new DrawnUI.PaperStyle
            {
                ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 4f,
                StepsPerEdge = 20, Jitter = 1.7f, Thickness = 3f, Seed = 7,
            });

            DrawnUI.HandLabel(_card, weekLabel, 0f, 40f, SizeTitle, DrawnUI.Ink, CardW,
                              TextAlignmentOptions.Top);

            // THE TEXT MUST NEVER REACH THE FOOTER: the column is clipped inside a
            // viewport with a reserved footer band below it, and it scrolls itself as
            // beats arrive.
            _viewport = DrawnUI.Rect(_card, "viewport", 96f, 126f, 888f, TextH);
            _viewport.gameObject.AddComponent<RectMask2D>();
            _column = DrawnUI.Rect(_viewport, "column", 0f, 0f, 888f, TextH);

            // the pen stroke that tracks the real render
            var track = DrawnUI.Rect(_card, "track", 160f, CardH - FooterH + 22f, 760f, 26f);
            var faint = track.gameObject.AddComponent<Image>();
            faint.sprite = DrawnUI.WobbleLineSprite(760, 3f, 61, 2.2f, 3, 4);
            faint.color = DrawnUI.WithAlpha(DrawnUI.Ink, 0.13f);
            faint.raycastTarget = false;
            var fillRt = DrawnUI.Rect(track, "ink", 0f, 0f, 760f, 26f);
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            _penFill = fillRt.gameObject.AddComponent<Image>();
            _penFill.sprite = DrawnUI.WobbleLineSprite(760, 5f, 61, 2.2f, 3, 4);
            _penFill.color = DrawnUI.Coral;
            _penFill.raycastTarget = false;
            _penFill.type = Image.Type.Filled;
            _penFill.fillMethod = Image.FillMethod.Horizontal;
            _penFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _penFill.fillAmount = 0f;

            _more = DrawnUI.HandLabel(_card, "▼  more below — scroll", 0f, CardH - FooterH - 6f,
                SizeLabel, DrawnUI.WithAlpha(DrawnUI.Coral, 0.8f), CardW, TextAlignmentOptions.Top);
            _more.gameObject.SetActive(false);
            _hint = DrawnUI.HandLabel(_card, "the week is still developing... (click to catch up)",
                0f, CardH - FooterH + 54f, SizeLabel,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.4f), CardW, TextAlignmentOptions.Top);

            var g = DrawnUI.Group(GetComponent<RectTransform>());
            g.alpha = 0f;
            StartCoroutine(DrawnUI.FadeTo(g, 1f, 0.24f));
        }

        /// Queue one beat. `label` is the small lead-in ("You said"); "" is a bare
        /// paragraph. Beats are revealed in order, paced to reading speed.
        public void Say(string label, string body)
        {
            if (body == null || body.Trim().Length == 0) return;
            _queue.Add(new[] { label ?? "", body });
        }

        /// Real progress on the art, 0..1. Without it the stroke creeps and waits.
        public void Report(float p) { _target = Mathf.Clamp01(p); }

        /// Wait until the reader has had the whole text AND the art is done, then let
        /// them look up. Never returns before MinLife, never holds a finished reader.
        public IEnumerator Finish()
        {
            _target = 1f;
            while (_t < MinLife || _queue.Count > 0 || _amount < 0.995f) yield return null;
            // THE READER DECIDES WHEN TO LOOK UP: the hint flips to an invitation and
            // the beat holds until a click.
            _proceed = false;
            if (_hint != null)
            {
                _hint.text = "look up  →   (click)";
                _hint.color = DrawnUI.Coral;
            }
            while (!_proceed) yield return null;
            _done = true;
            var g = DrawnUI.Group(GetComponent<RectTransform>());
            yield return DrawnUI.FadeTo(g, 0f, 0.30f);
            if (this != null && gameObject != null) Destroy(gameObject);
        }

        /// The run ended under the beat: take it away without ceremony.
        public void Dismiss()
        {
            _done = true;
            if (this != null && gameObject != null) Destroy(gameObject);
        }

        void OnClick()
        {
            if (_queue.Count == 0 && !_draining && _amount >= 0.995f && _t >= MinLife) _proceed = true;
            else SkipReading();
        }

        void Update()
        {
            if (_done) return;
            _t += Time.unscaledDeltaTime;

            if (_queue.Count > 0 && _t >= _nextRevealAt)
            {
                string[] beat = _queue[0];
                _queue.RemoveAt(0);
                Reveal(beat[0], beat[1]);
                _nextRevealAt = _t + Mathf.Clamp(beat[1].Length / ReadCps, 1.2f, 9f);
            }

            float goal = _target > 0f ? _target : Mathf.Min(0.92f, _t / 60f);
            _amount = Mathf.MoveTowards(_amount, goal, Time.unscaledDeltaTime * 0.5f);
            if (_penFill != null) _penFill.fillAmount = _amount;

            // the wheel reads back and forth; a plain click catches up, then closes
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f) ScrollBy(-wheel * 64f);

            float maxs = Mathf.Max(_columnH - TextH, 0f);
            if (_more != null)
            {
                bool show = maxs > 8f && _scroll < maxs - 8f;
                if (_more.gameObject.activeSelf != show) _more.gameObject.SetActive(show);
                if (show)
                    _more.color = DrawnUI.WithAlpha(DrawnUI.Coral,
                        0.55f + 0.35f * Mathf.Sin(_t * 4f));
            }
        }

        void ScrollBy(float dy)
        {
            float maxs = Mathf.Max(_columnH - TextH, 0f);
            _scroll = Mathf.Clamp(_scroll + dy, 0f, maxs);
            if (_column != null) DrawnUI.SetTopLeft(_column, 0f, -_scroll);
        }

        void Reveal(string label, string body)
        {
            float y = _columnH;
            if (label.Length > 0)
            {
                DrawnUI.HandLabel(_column, label, 0f, y, SizeLabel,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f), 888f, TextAlignmentOptions.TopLeft);
                y += SizeLabel * 1.5f;
            }
            var b = DrawnUI.HandLabel(_column, body, 0f, y, SizeBody, DrawnUI.Ink, 888f,
                                      TextAlignmentOptions.TopLeft);
            b.ForceMeshUpdate();
            float h = Mathf.Max(b.preferredHeight, SizeBody * 1.4f);
            b.rectTransform.sizeDelta = new Vector2(888f, h);
            _bodies.Add(b);
            _columnH = y + h + 22f;
            if (_column != null) _column.sizeDelta = new Vector2(888f, Mathf.Max(_columnH, TextH));

            // WRITTEN, not faded: the beat is the week being put down in ink.
            float secs = ReadingBeatText.Apply(b, Mathf.Clamp(body.Length / 95f, 0.3f, 6.5f));
            StartCoroutine(WriteIn(b, secs));

            // keep the newest beat in view: the reader follows the pen down the page
            float maxs = Mathf.Max(_columnH - TextH, 0f);
            StartCoroutine(ScrollTo(maxs, 0.45f));
        }

        bool _skipAll;

        IEnumerator ClearSkip()
        {
            yield return null;
            yield return null;
            _skipAll = false;
        }

        IEnumerator WriteIn(TextMeshProUGUI t, float secs)
        {
            _draining = true;
            Runway.Audio.Sfx.PenScratch(true, -2f);
            int total = t.text.Length;
            t.maxVisibleCharacters = 0;
            float k = 0f;
            while (k < secs && t != null && !_skipAll)
            {
                k += Time.unscaledDeltaTime;
                t.maxVisibleCharacters = Mathf.RoundToInt(total * Mathf.Clamp01(k / secs));
                yield return null;
            }
            if (t != null) t.maxVisibleCharacters = total;
            _draining = false;
            Runway.Audio.Sfx.PenScratch(false);
        }

        IEnumerator ScrollTo(float to, float secs)
        {
            float from = _scroll;
            float k = 0f;
            while (k < secs)
            {
                k += Time.unscaledDeltaTime;
                _scroll = Mathf.Lerp(from, to, Mathf.Clamp01(k / secs));
                if (_column != null) DrawnUI.SetTopLeft(_column, 0f, -_scroll);
                yield return null;
            }
            _scroll = to;
            if (_column != null) DrawnUI.SetTopLeft(_column, 0f, -_scroll);
        }

        /// Everything lands NOW: queued beats spawn, writing lines complete, the clock
        /// considers the reading done. Only the render keeps its own pace.
        void SkipReading()
        {
            _skipAll = true;    // every write-in lands on its next tick, nothing is killed
            while (_queue.Count > 0)
            {
                string[] beat = _queue[0];
                _queue.RemoveAt(0);
                Reveal(beat[0], beat[1]);
            }
            _draining = false;
            Runway.Audio.Sfx.PenScratch(false);
            for (int i = 0; i < _bodies.Count; i++)
                if (_bodies[i] != null) _bodies[i].maxVisibleCharacters = _bodies[i].text.Length;
            _t = Mathf.Max(_t, MinLife);
            _nextRevealAt = 0f;
            ScrollBy(0f);
            // the flag is LATCHED for two frames: every write-in in flight has to be
            // resumed once to see it, and clearing it in this same call would leave
            // the ones already parked on `yield return null` still crawling.
            StartCoroutine(ClearSkip());
        }
    }
}
