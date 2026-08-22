using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;
using Runway.Audio;

namespace Runway.Game
{
    /// <summary>
    /// PAGE 3 — THE SHAPE OF IT. What you sell, and who to. Two rows of paper cards
    /// laid straight on the stage, each with the object that stands for the trade.
    ///
    /// This shapes every week that follows: the engine's whole theta — how many buyers
    /// exist, how long they stay, what they pay, how fast anything ships — is derived
    /// from these two words, and so is the shelf on the bag page.
    ///
    /// THE PICKED CARD IS CIRCLED, NOT LIT. Selection marks the card; growing it would
    /// change the layout every time the player looked at a different option, which
    /// reads as the grid slipping.
    /// </summary>
    public sealed class DraftShapePage
    {
        static readonly string[][] What =
        {
            new[] { "Software", "itm_laptop", "Ships fast. Scales free. Everyone is doing it, and that is the problem." },
            new[] { "Hardware", "itm_dads_server", "Real things for real shelves. Slower, costlier, defensible." },
            new[] { "Marketplace", "env_boxes", "You own the middle. Nothing works until both sides show up." },
            new[] { "Service", "itm_idea_napkin", "You ARE the product. Revenue on day one, margins made of hours." },
        };

        static readonly string[][] Who =
        {
            new[] { "Enterprise", "env_calendar", "Huge contracts, glacial sales. Bring patience and a blazer." },
            new[] { "SMB", "itm_textbook", "Thousands of small checks. They churn when the card expires." },
            new[] { "Consumer", "env_tv", "Millions of maybes. You need volume, virality, and luck." },
        };

        readonly FounderDraftScreen _s;
        readonly List<RectTransform> _whatCards = new List<RectTransform>();
        readonly List<RectTransform> _whoCards = new List<RectTransform>();
        readonly List<string> _whatNames = new List<string>();
        readonly List<string> _whoNames = new List<string>();

        public DraftShapePage(FounderDraftScreen s) { _s = s; }

        public RectTransform Build()
        {
            var page = DrawnUI.FullRect(_s.Rect, "page_shape");
            FounderDraftScreen.Dim(page);
            FounderDraftScreen.Heading(page, "THE SHAPE OF IT", 56f, 60f, 26f);
            DrawnUI.HandLabel(page, "This shapes every week that follows.", 64f, 116f, 27f,
                DrawnUI.WithAlpha(DrawnUI.Cream, 0.85f));

            // WHAT and FOR WHO are `_dlabel` — the two section heads on this page
            DrawnUI.DisplayLabel(page, "WHAT", 64f, 172f, 30f, DrawnUI.Yellow);
            // four WHATs share the row three used to fill: the cards slim down together
            float wcard = What.Length > 3 ? 340f : 440f;
            float wstep = (RunwayPaths.StageWidth - 128f - wcard) / Mathf.Max(What.Length - 1, 1);
            for (int i = 0; i < What.Length; i++)
            {
                RectTransform card = Card(page, What[i], 64f + i * wstep, 226f, wcard, true, i);
                _whatCards.Add(card);
                _whatNames.Add(What[i][0]);
            }

            DrawnUI.DisplayLabel(page, "FOR WHO", 64f, 552f, 30f, DrawnUI.Yellow);
            for (int i = 0; i < Who.Length; i++)
            {
                RectTransform card = Card(page, Who[i], 64f + i * 470f, 606f, 440f, false, i);
                _whoCards.Add(card);
                _whoNames.Add(Who[i][0]);
            }
            Restyle();

            _s.Nav(page, "←", 48f, 940f, 100f, 64f, 30f, () => _s.TransitionTo(2));
            _s.Nav(page, "NEXT: THE CREW  →", 1120f, 930f, 380f, 80f, 32f, () => _s.TransitionTo(4));
            return page;
        }

        RectTransform Card(RectTransform page, string[] spec, float x, float y, float w,
                           bool isWhat, int i)
        {
            var card = GameUi.PaperSheet(page, x, y, w, 300f, i, 4f, null, "card");
            GameUi.Tilt(card, (isWhat ? 0.006f : -0.005f)
                              * (x < 500f ? 1f : (x > 900f ? -1f : 0.45f)));
            GameUi.Picture(card, "icon", ArtCache.SpritePath(spec[1]),
                           w * 0.5f - 62f, 12f, 124f, 124f);
            // the trade's NAME is `_dlabel`; the line under it is `_label`, and the check
            // chip is a Label built by hand on `_font` — the tick stays in the writing one
            DrawnUI.DisplayLabel(card, spec[0].ToUpper(), 20f, 128f, 30f, DrawnUI.Ink, w - 40f,
                                 TextAlignmentOptions.Top);
            DrawnUI.HandLabel(card, spec[2], 28f, 184f, w >= 440f ? 27f : 24f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.88f), w - 56f, TextAlignmentOptions.Top);
            var chk = DrawnUI.HandLabel(card, "✓", w - 44f, 6f, 34f, DrawnUI.Coral, 40f);
            chk.name = "chk";
            chk.gameObject.SetActive(false);

            // the hit box IS the card: an Image on the root paints under its own
            // children, so a transparent one catches the click without covering the ink
            var hit = card.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            var b = card.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.targetGraphic = hit;
            string pick = spec[0];
            b.onClick.AddListener(() =>
            {
                if (isWhat) _s.BizWhat = pick; else _s.BizWho = pick;
                Sfx.Cash();
                Restyle();
            });
            return card;
        }

        void Restyle()
        {
            for (int i = 0; i < _whatCards.Count; i++)
                Mark(_whatCards[i], _whatNames[i] == _s.BizWhat);
            for (int i = 0; i < _whoCards.Count; i++)
                Mark(_whoCards[i], _whoNames[i] == _s.BizWho);
        }

        /// The picked card is circled in coral pen; the rest stay paper, just quieter.
        /// Paper is OPAQUE — fading a card with alpha lets the stage lighting bleed
        /// through it as a seam across its face, so the unpicked go grey, never faint.
        static void Mark(RectTransform card, bool sel)
        {
            Transform edge = card.Find("edge");
            var img = edge != null ? edge.GetComponent<Image>() : null;
            if (img != null) img.color = sel ? DrawnUI.Coral : DrawnUI.Ink;
            Transform paper = card.Find("paper");
            var body = paper != null ? paper.GetComponent<Image>() : null;
            if (body != null)
                body.color = sel ? DrawnUI.Cream
                                 : new Color(DrawnUI.Cream.r * 0.93f, DrawnUI.Cream.g * 0.92f,
                                             DrawnUI.Cream.b * 0.90f, 1f);
            Transform chk = card.Find("chk");
            if (chk != null) chk.gameObject.SetActive(sel);
        }
    }
}
