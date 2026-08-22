using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;

namespace Runway.Screens
{
    /// <summary>
    /// THE STUDIO CARD — studio_card.gd, ported. "ASSEM STUDIO" fades in and out before
    /// the title, the way a real release opens. Click skips. Drawn: ink stage, cream
    /// lettering, one coral underline that draws itself while the name holds.
    /// </summary>
    public sealed class StudioCard : AppScreen
    {
        /// total life: fade in 0.7, hold, fade out 0.6
        const float Hold = 2.6f;

        static readonly Color Cream = DrawnUI.Hex("F2EAD3");
        static readonly Color Pen = DrawnUI.Hex("E86A5C");

        float _t;
        Image _underline;
        CanvasGroup _fade;

        protected override void OnBuild()
        {
            float w = RunwayPaths.StageWidth;
            float h = RunwayPaths.StageHeight;

            DrawnUI.FullFill(Rect, "bg", DrawnUI.Hex("22262B"), true);

            var body = DrawnUI.FullRect(Rect, "card");
            _fade = DrawnUI.Group(body);
            _fade.alpha = 0f;

            const float NameSize = 76f;
            const string Name = "ASSEM STUDIO";
            DrawnUI.InkString(body, Name, h * 0.5f, NameSize, Cream, w);

            // the underline draws itself during the hold
            float textW = DrawnUI.MeasureWidth(Name, NameSize);
            float x0 = (w - textW) * 0.5f;
            var rule = DrawnUI.Rule(body, x0, h * 0.5f + 22f, Mathf.Max(textW, 1f), Pen, 5f, 11, 2f, 17);
            _underline = rule;
            _underline.type = Image.Type.Filled;
            _underline.fillMethod = Image.FillMethod.Horizontal;
            _underline.fillOrigin = (int)Image.OriginHorizontal.Left;
            _underline.fillAmount = 0f;

            DrawnUI.InkString(body, "presents", h * 0.5f + 74f, 30f,
                              DrawnUI.WithAlpha(Cream, 0.55f), w);

            // a click skips — a card is a courtesy, never a wait
            var hit = DrawnUI.FullFill(Rect, "skip", new Color(0f, 0f, 0f, 0f), true);
            var btn = hit.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = hit;
            btn.onClick.AddListener(() => Finish());
        }

        void Update()
        {
            _t += Time.unscaledDeltaTime;
            if (_t >= Hold)
            {
                Finish();
                return;
            }
            if (_fade != null) _fade.alpha = Alpha();
            if (_underline != null)
                _underline.fillAmount = Mathf.Clamp01((_t - 0.6f) / 0.5f);
            if (Input.anyKeyDown) Finish();
        }

        /// fade in over 0.7, fade out over the last 0.6, full in between
        float Alpha()
        {
            if (_t < 0.7f) return _t / 0.7f;
            if (_t > Hold - 0.6f) return Mathf.Max((Hold - _t) / 0.6f, 0f);
            return 1f;
        }
    }
}
