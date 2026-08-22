using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;
using Runway.Audio;

namespace Runway.Game
{
    /// <summary>
    /// PAGE 5 — FIRST MONEY. Who pays for week one? Money now costs equity forever.
    ///
    /// Three paper cards lying straight on the stage, each with the object that stands
    /// for where the money came from, the number it adds, and — in coral, because it is
    /// the part nobody reads twice — what it takes out of the cap table for good.
    /// </summary>
    public sealed class DraftMoneyPage
    {
        struct Face
        {
            public string Icon, Big, Cost, Flavor;
            public bool CostIsCoral;
        }

        static readonly Dictionary<string, Face> Faces = new Dictionary<string, Face>
        {
            { "bootstrap", new Face { Icon = "itm_savings_jar", Big = "+$0",
                Cost = "you keep 100%", CostIsCoral = false,
                Flavor = "Your savings and nothing else. Ramen. Focus. Freedom." } },
            { "fnf", new Face { Icon = "itm_goodwill", Big = "+$15,000",
                Cost = "−5% · dilutes EVERYONE", CostIsCoral = true,
                Flavor = "Awkward Thanksgiving if this fails." } },
            { "angel", new Face { Icon = "itm_dignity", Big = "+$50,000",
                Cost = "−12% · dilutes EVERYONE", CostIsCoral = true,
                Flavor = "The angel replies only in voice memos." } },
        };

        readonly FounderDraftScreen _s;
        readonly List<RectTransform> _cards = new List<RectTransform>();
        readonly List<JObject> _funds = new List<JObject>();
        TextMeshProUGUI _preview;

        public DraftMoneyPage(FounderDraftScreen s) { _s = s; }

        public RectTransform Build()
        {
            var page = DrawnUI.FullRect(_s.Rect, "page_money");
            FounderDraftScreen.Dim(page);
            FounderDraftScreen.Heading(page, "FIRST MONEY", 56f, 60f, 26f);
            DrawnUI.HandLabel(page, "Who pays for week one? Money now costs equity forever.",
                64f, 116f, 28f, DrawnUI.WithAlpha(DrawnUI.Cream, 0.85f));

            float[] leans = { 0.006f, -0.004f, 0.005f };
            for (int i = 0; i < _s.Fundings.Count; i++)
            {
                var f = _s.Fundings[i] as JObject;
                if (f == null) continue;
                Face d;
                if (!Faces.TryGetValue(ContentDb.Str(f, "id"), out d)) d = new Face();
                var card = GameUi.PaperSheet(page, 120f + i * 440f, 210f, 400f, 520f, i,
                                             4f, null, "fund");
                GameUi.Tilt(card, leans[i % 3]);
                GameUi.Picture(card, "icon", ArtCache.SpritePath(d.Icon ?? ""), 125f, 30f, 150f, 150f);
                // NAME AND NUMBER ARE `_dlabel`. What it costs and the joke under it are
                // `_label` — the money is printed on the card, the terms are written on it.
                DrawnUI.DisplayLabel(card, ContentDb.Str(f, "name"), 20f, 196f, 30f, DrawnUI.Ink,
                                     360f, TextAlignmentOptions.Top);
                DrawnUI.DisplayLabel(card, d.Big ?? "", 20f, 252f, 40f, DrawnUI.Ink, 360f,
                                     TextAlignmentOptions.Top);
                DrawnUI.HandLabel(card, d.Cost ?? "", 20f, 330f, 32f,
                    d.CostIsCoral ? DrawnUI.Coral : DrawnUI.Sage, 360f, TextAlignmentOptions.Top);
                DrawnUI.HandLabel(card, d.Flavor ?? "", 30f, 404f, 27f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.8f), 340f, TextAlignmentOptions.Top);

                var hit = card.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                hit.raycastTarget = true;
                var b = card.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.targetGraphic = hit;
                JObject pick = f;
                b.onClick.AddListener(() =>
                {
                    _s.SelFund = pick;
                    Sfx.Cash();
                    _s.RefreshCapLine();
                });
                _cards.Add(card);
                _funds.Add(f);
            }

            GameUi.PaperSheet(page, 328f, 774f, 880f, 66f, 2, 3f, null, "strip");
            _preview = DrawnUI.HandLabel(page, "", 328f, 788f, 28f, DrawnUI.Ink, 880f,
                                         TextAlignmentOptions.Top);

            _s.Nav(page, "←", 48f, 930f, 100f, 70f, 30f, () => _s.TransitionTo(4));
            _s.Nav(page, "NEXT: PACK YOUR BAG  →", 1060f, 920f, 440f, 84f, 32f, () =>
            {
                if (_s.SelFund == null) return;
                _s.TransitionTo(6);
            });
            Refresh();
            return page;
        }

        public void Refresh()
        {
            for (int i = 0; i < _cards.Count; i++)
                Mark(_cards[i], _funds[i] == _s.SelFund);
            if (_preview == null) return;
            if (_s.SelFund == null)
            {
                _preview.text = "pick one — the donut remembers forever";
                return;
            }
            string co = (_s.CompanyName ?? "").Trim();
            _preview.text = string.Format("You'd keep {0:0}% of {1} · ~${2} in the bank on day one",
                _s.FounderPct(), co.Length > 0 ? co : "the company", GameUi.Money(_s.DayOneCash()));
        }

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
        }
    }
}
