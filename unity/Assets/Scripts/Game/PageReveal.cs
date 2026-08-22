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
    /// THE PAGE WRITES ITSELF IN — journal_page.gd's performance, on its own.
    ///
    /// Every element used to pop in fully formed, which reads as a text engine filling
    /// a template. The page now plays the way a hand fills a sheet: lines appear left
    /// to right, drawings fade up in order, and the ruled writing line arrives last,
    /// ready. ONE CLICK ANYWHERE SKIPS THE WHOLE PERFORMANCE — a reader is never held.
    ///
    /// THE BUDGET SCALES THE HAND, NEVER THE PAGE: a long page writes faster, a short
    /// one savours it, and nothing ever takes longer than `Budget`.
    ///
    /// AN ELEMENT BECOMES INTERACTIVE THE MOMENT IT IS FULLY INK, never before — a
    /// choice you cannot see yet is a choice you cannot mean.
    /// </summary>
    public sealed class PageReveal
    {
        public const float WriteCps = 80f;
        public const float TitleCps = 34f;
        const float Budget = 3.6f;       // the longest any page may spend arriving
        const float IconIn = 0.22f;     // one drawing's fade-up
        const float IconStagger = 0.09f; // the beat between neighbours — felt, not implied

        readonly MonoBehaviour _host;
        readonly List<object[]> _seq = new List<object[]>();   // {kind, payload, secs}
        Action _onFieldShown;
        bool _queued;
        bool _gateOpen = true;

        public bool Revealing { get; private set; }

        public PageReveal(MonoBehaviour host) { _host = host; }

        /// The field grabs the keyboard when it lands, and not before: grabbing focus
        /// under a half-written page would let typed ink arrive above the pen.
        public void OnFieldShown(Action a) { _onFieldShown = a; }

        public void Enqueue(string kind, object payload, float secs)
        {
            _seq.Add(new object[] { kind, payload, secs });
            if (_queued || _host == null) return;
            _queued = true;
            // hosts compose a page synchronously right after Build(), so one deferred
            // frame lands after the LAST element and the whole page plays as one hand
            _host.StartCoroutine(QueueRoutine());
        }

        /// The sheet is still flying in: hold the ink until it has landed.
        public void CloseGate() { _gateOpen = false; }

        public void OpenGate()
        {
            _gateOpen = true;
            if (_queued && !Revealing) Play();
        }

        IEnumerator QueueRoutine()
        {
            yield return null;
            Play();
        }

        void Play()
        {
            if (_seq.Count == 0 || Revealing || !_gateOpen || _host == null) return;
            Revealing = true;
            _host.StartCoroutine(Routine());
        }

        IEnumerator Routine()
        {
            float total = 0f;
            for (int i = 0; i < _seq.Count; i++) total += (float)_seq[i][2];
            float speed = Mathf.Max(total / Budget, 1f);
            for (int i = 0; i < _seq.Count; i++)
            {
                object[] it = _seq[i];
                float secs = (float)it[2] / speed;
                switch ((string)it[0])
                {
                    case "line":
                    {
                        Runway.Audio.Sfx.PenScratch(true);
                        var l = it[1] as TextMeshProUGUI;
                        if (l == null) break;
                        int n = l.text.Length;
                        float t = 0f;
                        while (t < secs)
                        {
                            t += Time.unscaledDeltaTime;
                            if (l == null) break;
                            l.maxVisibleCharacters = Mathf.RoundToInt(n * Mathf.Clamp01(t / secs));
                            yield return null;
                        }
                        if (l != null) l.maxVisibleCharacters = n;
                        break;
                    }
                    case "icons":
                    {
                        Runway.Audio.Sfx.LoopStop();   // the pen lifts for drawings
                        var slots = it[1] as List<RectTransform>;
                        if (slots == null) break;
                        float t = 0f;
                        while (t < secs)
                        {
                            t += Time.unscaledDeltaTime;
                            for (int s = 0; s < slots.Count; s++)
                            {
                                if (slots[s] == null) continue;
                                DrawnUI.Group(slots[s]).alpha =
                                    Mathf.Clamp01((t - IconStagger * s) / IconIn);
                            }
                            yield return null;
                        }
                        Wake(slots);
                        break;
                    }
                    case "field":
                    case "fade":
                    {
                        var rt = it[1] as RectTransform;
                        if (rt == null) break;
                        yield return DrawnUI.FadeTo(DrawnUI.Group(rt), 1f, secs);
                        if ((string)it[0] == "field" && _onFieldShown != null) _onFieldShown();
                        break;
                    }
                }
            }
            Finish();
        }

        static void Wake(List<RectTransform> slots)
        {
            for (int s = 0; s < slots.Count; s++)
            {
                if (slots[s] == null) continue;
                DrawnUI.Group(slots[s]).alpha = 1f;
                var img = slots[s].GetComponent<Image>();
                if (img != null) img.raycastTarget = true;   // ink first, then pressable
            }
        }

        /// Everything lands NOW: ink, drawings, the field, the keyboard. Also the only
        /// exit — the sequence funnels here whether it played out or was skipped.
        public void Finish()
        {
            Runway.Audio.Sfx.LoopStop();
            for (int i = 0; i < _seq.Count; i++)
            {
                object[] it = _seq[i];
                switch ((string)it[0])
                {
                    case "line":
                    {
                        var l = it[1] as TextMeshProUGUI;
                        if (l != null) l.maxVisibleCharacters = l.text.Length;
                        break;
                    }
                    case "icons":
                    {
                        var slots = it[1] as List<RectTransform>;
                        if (slots != null) Wake(slots);
                        break;
                    }
                    case "field":
                    case "fade":
                    {
                        var rt = it[1] as RectTransform;
                        if (rt != null) DrawnUI.Group(rt).alpha = 1f;
                        break;
                    }
                }
            }
            _seq.Clear();
            _queued = false;
            Revealing = false;
        }

        /// What an element deserves at an unhurried hand, before the page budget.
        public static float LineSecs(string text, float size, float titleSize)
        {
            float cps = size >= titleSize ? TitleCps : WriteCps;
            return Mathf.Max((text ?? "").Length / cps, 0.12f);
        }

        public static float IconsSecs(int count)
        {
            return IconIn + IconStagger * count;
        }
    }
}
