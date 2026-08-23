using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
            if (_edit == null) return;
            _edit.ActivateInputField();
            // the pen goes to the END of what is already written, never in front of
            // it: TMP leaves the caret at string position 0 when focus arrives from
            // code, so a re-prompted key would have been typed backwards
            _edit.stringPosition = (_edit.text ?? "").Length;
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
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.richText = false;

            var phRt = DrawnUI.FullRect(viewport, "placeholder");
            var ph = phRt.gameObject.AddComponent<TextMeshProUGUI>();
            if (DrawnUI.Hand != null) ph.font = DrawnUI.Hand;
            ph.fontSize = sizePx;
            ph.color = DrawnUI.WithAlpha(DrawnUI.Ink, 0.28f);
            ph.alignment = TextAlignmentOptions.Center;
            ph.textWrappingMode = TextWrappingModes.NoWrap;
            ph.richText = false;
            ph.text = placeholder ?? "";

            _edit = fieldGo.AddComponent<TMP_InputField>();
            _edit.transition = Selectable.Transition.None;
            _edit.targetGraphic = hit;
            _edit.textViewport = viewport;
            _edit.textComponent = text;
            _edit.placeholder = ph;
            _edit.lineType = TMP_InputField.LineType.SingleLine;
            _edit.richText = false;
            _edit.restoreOriginalTextOnEscape = false;
            Editable(_edit, DrawnUI.Pen);

            fieldGo.SetActive(true);

            _edit.onValueChanged.AddListener(t => { var c = Changed; if (c != null) c(t); });
            _edit.onSubmit.AddListener(t => { var s = Submitted; if (s != null) s(t); });
        }

        // ══ the feel of writing in it ══════════════════════════════════════════

        /// EVERY FIELD IN THIS GAME IS BUILT FROM CODE, so the feel of editing in one
        /// is built from code too. TMP's defaults are close to native and two of them
        /// are not, in exactly this construction:
        ///
        /// ONE — `onFocusSelectAll` ships ON, and TMP_InputField.OnPointerDown gates
        /// its ENTIRE caret block behind `hadFocusBefore || !m_OnFocusSelectAll`. So a
        /// click into a field that does not already hold the keyboard selects the whole
        /// entry instead of landing the pen where it was pointed, and the next
        /// character typed wipes a week of writing. It also means the FIRST double
        /// click on a cold field cannot select a word, because the pass that would have
        /// done it never runs. Off.
        ///
        /// TWO — TRIPLE CLICK DOES NOT EXIST in TMP_InputField. OnPointerDown counts to
        /// two and stops; there is no select-the-line and no select-the-lot. Cmd-A does
        /// work (KeyPressed reads EventModifiers.Command on Apple platforms, off
        /// SystemInfo, not a #define), and the third click is added here.
        ///
        /// The caret is TMP's own: 0.85 blinks a second, stated rather than inherited,
        /// and CaretBlink() opens with `m_CaretVisible = true` so ActivateInputField
        /// shows a caret on the first frame the field holds focus rather than up to
        /// half a blink later.
        public static TMP_InputField Editable(TMP_InputField f, Color pen)
        {
            if (f == null) return null;
            f.customCaretColor = true;
            f.caretColor = pen;
            f.caretWidth = 2;
            f.selectionColor = DrawnUI.WithAlpha(pen, 0.22f);
            f.caretBlinkRate = 0.85f;
            f.onFocusSelectAll = false;
            if (f.GetComponent<TripleClickSelectsAll>() == null)
                f.gameObject.AddComponent<TripleClickSelectsAll>();
            return f;
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

    /// THE THIRD CLICK, which TMP_InputField does not have. It rides on the SAME
    /// GameObject as the field: ExecuteEvents hands a pointer-click to every component
    /// on the target that implements the interface, so the field still gets its own
    /// click and this only adds to it.
    ///
    /// `clickCount` is the input module's own run-of-clicks counter (a click inside
    /// 0.3s of the last one raises it), so nothing here has to time anything. Two
    /// clicks have already selected the word by the time the third lands; this widens
    /// the same selection to the whole entry, which is what every text field on this
    /// machine does.
    [RequireComponent(typeof(TMP_InputField))]
    public sealed class TripleClickSelectsAll : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData e)
        {
            if (e == null || e.button != PointerEventData.InputButton.Left) return;
            if (e.clickCount < 3) return;
            var f = GetComponent<TMP_InputField>();
            if (f == null) return;
            int n = (f.text ?? "").Length;
            if (n == 0) return;
            // TMP's own SelectAll(), in its public spelling: RAW STRING indices, so
            // nothing here can be off by one against the caret's own index space
            f.selectionStringAnchorPosition = n;
            f.selectionStringFocusPosition = 0;
            // AND THE WASH HAS TO BE ASKED FOR. Those two setters only raise a dirty
            // FLAG; the string indices are turned into caret indices, and the highlight
            // drawn, inside OnFillVBO — which runs on a geometry rebuild that nothing
            // here has caused. The field's own pointer-down for this very click has
            // already been and gone, and the blink coroutine stops marking the geometry
            // the moment a selection exists, so without this the third click selects
            // the entry silently and the player sees no wash at all. (Measured: the
            // first cut of this handler shipped exactly that.)
            f.ForceLabelUpdate();
        }
    }
}
