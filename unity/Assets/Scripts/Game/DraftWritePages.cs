using System.Collections;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// PAGE 0 — WHO IS DOING THIS (owner: the name comes before the character).
    /// One big write-in, prefilled with a dealt name, redealt on a whim. Everything
    /// after this — archetype, company, world — happens to THIS person.
    /// </summary>
    public sealed class DraftSignPage
    {
        readonly FounderDraftScreen _s;
        PaperInput _founderEdit;

        public DraftSignPage(FounderDraftScreen s) { _s = s; }

        public RectTransform Build()
        {
            var page = DrawnUI.FullRect(_s.Rect, "page_sign");
            FounderDraftScreen.Dim(page);
            FounderDraftScreen.Heading(page, "FIRST, YOUR NAME", 58f, 470f, 200f);
            DrawnUI.HandLabel(page, "it goes on the lease, the deck, and every apology",
                478f, 296f, 28f, DrawnUI.WithAlpha(DrawnUI.Cream, 0.8f));

            _founderEdit = PaperInput.Create(page, 438f, 400f, 660f, 150f, "SIGNED", "", 52f);
            _s.FounderName = WorldGen.PersonName(_s.Prng);
            _founderEdit.SetValue(_s.FounderName);
            _founderEdit.Changed += t => _s.FounderName = t;

            _s.Nav(page, "DEAL ME ANOTHER", 568f, 596f, 400f, 62f, 24f, () =>
            {
                _s.FounderName = WorldGen.PersonName(_s.Prng);
                _founderEdit.SetValue(_s.FounderName);
            });

            _s.Nav(page, "CHOOSE YOUR FOUNDER  →", 1010f, 900f, 470f, 76f, 30f, () =>
            {
                if ((_s.FounderName ?? "").Trim().Length == 0)
                {
                    _s.FounderName = WorldGen.PersonName(_s.Prng);
                    _founderEdit.SetValue(_s.FounderName);
                }
                _s.TransitionTo(1);
            });
            return page;
        }
    }

    /// <summary>
    /// PAGE 2 — NAME YOUR STARTUP. The name, the pitch the world will hold you to,
    /// and the idea machine for when nerve fails. The founder you just picked stands
    /// beside it and watches you type.
    /// </summary>
    public sealed class DraftNamePage
    {
        readonly FounderDraftScreen _s;
        PaperInput _nameEdit;
        PaperInput _ideaEdit;
        DraftLoop _witness;
        RectTransform _page;
        bool _spinning;

        public DraftNamePage(FounderDraftScreen s) { _s = s; }

        public RectTransform Build()
        {
            _page = DrawnUI.FullRect(_s.Rect, "page_name");
            FounderDraftScreen.Dim(_page);
            FounderDraftScreen.Heading(_page, "NAME YOUR STARTUP", 58f, 430f, 120f);

            GameUi.Shadow(_page, 210f, 934f, 190f, 32f);
            _witness = DraftLoop.Attach(_page, "witness", 130f, 540f, 330f, 410f);

            _nameEdit = PaperInput.Create(_page, 560f, 300f, 660f, 132f, "THE NAME", "Mossflow", 44f);
            _nameEdit.Changed += t => _s.CompanyName = t;
            _ideaEdit = PaperInput.Create(_page, 500f, 470f, 780f, 124f,
                "WHAT IT DOES — the world will hold you to this",
                "an app that walks your dog, badly", 34f);
            _ideaEdit.Changed += t => _s.CompanyIdea = t;

            _s.Nav(_page, "SPIN THE IDEA MACHINE", 690f, 636f, 400f, 68f, 26f, Spin);
            DrawnUI.HandLabel(_page, "or type your own. braver.", 760f, 716f, 24f,
                              DrawnUI.WithAlpha(DrawnUI.Cream, 0.7f));

            _s.Nav(_page, "←", 60f, 900f, 90f, 70f, 30f, () => _s.TransitionTo(1));
            _s.Nav(_page, "TO THE FOUNDING  →", 1150f, 890f, 340f, 84f, 32f, () => _s.TransitionTo(3));

            Reroll();
            return _page;
        }

        /// The witness arrives with the page: up from below, fading in, then breathing
        /// on the archetype that was actually drafted.
        public void Entrance()
        {
            if (_witness == null) return;
            if (_s.SelArch != null)
                _witness.Play(ContentDb.Str(_s.SelArch, "id"), ContentDb.Str(_s.SelArch, "sprite"));
            var boot = Boot.Instance;
            if (boot != null) boot.StartCoroutine(Rise(_witness.Rt));
        }

        static IEnumerator Rise(RectTransform rt)
        {
            if (rt == null) yield break;
            var g = DrawnUI.Group(rt);
            float home = DrawnUI.TopLeftY(rt);
            float t = 0f;
            while (t < 0.3f)
            {
                t += Time.unscaledDeltaTime;
                float k = DrawnUI.EaseOutCubic(t / 0.3f);
                if (rt == null) yield break;
                DrawnUI.SetTopLeft(rt, rt.anchoredPosition.x, Mathf.Lerp(home + 40f, home, k));
                g.alpha = Mathf.Min(1f, k / 0.8f);
                yield return null;
            }
            DrawnUI.SetTopLeft(rt, rt.anchoredPosition.x, home);
            g.alpha = 1f;
        }

        void Spin()
        {
            if (_spinning) return;
            var boot = Boot.Instance;
            if (boot == null) { Reroll(); return; }
            boot.StartCoroutine(SpinRoutine());
        }

        IEnumerator SpinRoutine()
        {
            _spinning = true;
            for (int i = 0; i < 6; i++)
            {
                Reroll();
                yield return new WaitForSecondsRealtime(0.07f + i * 0.03f);
            }
            _spinning = false;
        }

        void Reroll()
        {
            string nm = FounderDraftScreen.NameA[_s.Prng.RandiRange(0, FounderDraftScreen.NameA.Length - 1)]
                        + FounderDraftScreen.NameB[_s.Prng.RandiRange(0, FounderDraftScreen.NameB.Length - 1)];
            string idea = string.Format("{0} {1} for {2}",
                FounderDraftScreen.IdeaPre[_s.Prng.RandiRange(0, FounderDraftScreen.IdeaPre.Length - 1)],
                FounderDraftScreen.IdeaForm[_s.Prng.RandiRange(0, FounderDraftScreen.IdeaForm.Length - 1)],
                FounderDraftScreen.IdeaFor[_s.Prng.RandiRange(0, FounderDraftScreen.IdeaFor.Length - 1)]);
            _s.CompanyName = nm;
            _s.CompanyIdea = idea;
            if (_nameEdit != null) _nameEdit.SetValue(nm);
            if (_ideaEdit != null) _ideaEdit.SetValue(idea);
        }
    }
}
