using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Runway.App
{
    /// <summary>
    /// A PLACE TO WRITE, in the game's own language — paper_input.gd, ported.
    ///
    /// The draft screens shipped white rounded rectangles on a dark panel: a web form
    /// laid over a drawn stage. Nothing about a white box belongs in this game. So this
    /// is a torn strip of the same paper the log book is made of, with a printed rule
    /// to write along and a pen caret. There is no box, no border radius, no fill
    /// behind the glyphs — the paper IS the field.
    ///
    ///     var f = PaperInput.Create(parent, 252f, 528f, 1030f, 112f,
    ///                               "PASTE YOUR OPENAI API KEY", "sk-…", 28f);
    ///     f.Submitted += t => ...;
    ///     print(f.Value);
    /// </summary>
    public sealed class PaperInput : MonoBehaviour
    {
        public event Action<string> Changed;
        public event Action<string> Submitted;

        RectTransform _rt;
        TMP_InputField _edit;
        TextMeshProUGUI _labelText;
        Image _rule;
        Sprite _ruleThin;
        Sprite _ruleThick;
        bool _wasFocused;

        public string Value
        {
            get { return _edit == null ? "" : _edit.text.Trim(); }
        }

        public void SetValue(string t)
        {
            if (_edit != null) _edit.text = t ?? "";
        }

        public void GrabWriteFocus()
        {
            if (_edit != null) _edit.ActivateInputField();
        }

        /// Re-issue the lead-in above the line — the keys screen scolds with it.
        public void SetLabel(string label)
        {
            if (_labelText != null) _labelText.text = label ?? "";
        }

        public static PaperInput Create(RectTransform parent, float x, float y,
                                        float w, float h, string label,
                                        string placeholder = "", float sizePx = 40f)
        {
            var rt = DrawnUI.Rect(parent, "paperinput", x, y, w, h);
            var pi = rt.gameObject.AddComponent<PaperInput>();
            pi.BuildParts(w, h, label, placeholder, sizePx);
            return pi;
        }

        void BuildParts(float w, float h, string label, string placeholder, float sizePx)
        {
            _rt = GetComponent<RectTransform>();

            // ── the torn strip ────────────────────────────────────────────────
            var shadow = DrawnUI.Fill(_rt, "shadow", new Color(0f, 0f, 0f, 0.20f), 5f, 7f, w, h);
            shadow.raycastTarget = false;
            var sheet = DrawnUI.Fill(_rt, "sheet", DrawnUI.Cream, 0f, 0f, w, h);
            sheet.raycastTarget = false;
            DrawnUI.AddInkEdge(_rt, new Vector2(w, h), new DrawnUI.PaperStyle
            {
                ShadowOffset = Vector2.zero,
                ShadowAlpha = 0f,
                Inset = 3f,
                StepsPerEdge = 14,
                Jitter = 1.5f,
                Thickness = 3f,
                Seed = 11,
            });

            float top = 6f;
            if (!string.IsNullOrEmpty(label))
            {
                _labelText = DrawnUI.HandLabel(_rt, label, 26f, top, 24f,
                                               DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f));
                top += 30f;
            }

            // ── the rule you write along ──────────────────────────────────────
            float ruleY = h - 22f;
            _ruleThin = DrawnUI.WobbleLineSprite(Mathf.RoundToInt(w - 52f), 2f, 41, 1.2f, 5, 4);
            _ruleThick = DrawnUI.WobbleLineSprite(Mathf.RoundToInt(w - 52f), 3f, 41, 1.2f, 5, 4);
            var ruleRt = DrawnUI.Rect(_rt, "rule", 26f - 4f, ruleY - 4f, w - 52f + 8f, 9f);
            _rule = ruleRt.gameObject.AddComponent<Image>();
            _rule.sprite = _ruleThin;
            _rule.color = DrawnUI.WithAlpha(DrawnUI.Sage, 0.75f);
            _rule.raycastTarget = false;

            // ── the field ─────────────────────────────────────────────────────
            var fieldGo = new GameObject("edit", typeof(RectTransform));
            fieldGo.SetActive(false);   // configure before TMP_InputField wakes
            var frt = fieldGo.GetComponent<RectTransform>();
            frt.SetParent(_rt, false);
            frt.anchorMin = new Vector2(0f, 1f);
            frt.anchorMax = new Vector2(0f, 1f);
            frt.pivot = new Vector2(0f, 1f);
            frt.sizeDelta = new Vector2(Mathf.Max(w - 52f, 40f), Mathf.Max(h - top - 18f, sizePx));
            frt.anchoredPosition = new Vector2(26f, -top);

            var hit = fieldGo.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);   // no fill: the paper IS the field
            hit.raycastTarget = true;

            var viewport = DrawnUI.FullRect(frt, "viewport");
            viewport.gameObject.AddComponent<RectMask2D>();

            var textRt = DrawnUI.FullRect(viewport, "text");
            var text = textRt.gameObject.AddComponent<TextMeshProUGUI>();
            if (DrawnUI.Hand != null) text.font = DrawnUI.Hand;
            text.fontSize = sizePx;
            text.color = DrawnUI.Ink;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.richText = false;

            var phRt = DrawnUI.FullRect(viewport, "placeholder");
            var ph = phRt.gameObject.AddComponent<TextMeshProUGUI>();
            if (DrawnUI.Hand != null) ph.font = DrawnUI.Hand;
            ph.fontSize = sizePx;
            ph.color = DrawnUI.WithAlpha(DrawnUI.Ink, 0.28f);
            ph.alignment = TextAlignmentOptions.Center;
            ph.enableWordWrapping = false;
            ph.richText = false;
            ph.text = placeholder ?? "";

            _edit = fieldGo.AddComponent<TMP_InputField>();
            _edit.transition = Selectable.Transition.None;
            _edit.targetGraphic = hit;
            _edit.textViewport = viewport;
            _edit.textComponent = text;
            _edit.placeholder = ph;
            _edit.lineType = TMP_InputField.LineType.SingleLine;
            _edit.customCaretColor = true;
            _edit.caretColor = DrawnUI.Pen;
            _edit.caretWidth = 2;
            _edit.selectionColor = DrawnUI.WithAlpha(DrawnUI.Pen, 0.22f);
            _edit.richText = false;
            _edit.restoreOriginalTextOnEscape = false;

            fieldGo.SetActive(true);

            _edit.onValueChanged.AddListener(t => { var c = Changed; if (c != null) c(t); });
            _edit.onSubmit.AddListener(t => { var s = Submitted; if (s != null) s(t); });
        }

        /// The rule under the writing thickens on focus, the way a pen presses harder.
        /// Polled rather than event-driven: onSelect/onDeselect have moved between TMP
        /// versions and isFocused has not.
        void Update()
        {
            if (_edit == null || _rule == null) return;
            bool focused = _edit.isFocused;
            if (focused == _wasFocused) return;
            _wasFocused = focused;
            _rule.sprite = focused ? _ruleThick : _ruleThin;
            _rule.color = focused ? DrawnUI.Pen : DrawnUI.WithAlpha(DrawnUI.Sage, 0.75f);
        }
    }
}
