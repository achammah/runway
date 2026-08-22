using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// PAGE 4 — THE CREW. Recruit cofounders, split the company, and watch the cap
    /// table answer. They will remember the split.
    ///
    /// ONE FOUNDING SHEET PER PERSON, cut from the same paper as everything else, with
    /// the equity, the commitment and the vesting on it as pen marks rather than form
    /// controls. A sheet whose terms are a trap is edged in coral instead of ink.
    ///
    /// THE CALL TAKES THE SCREEN. Not a dim over the crew — a dim leaves the ghost of
    /// the cards, the donut and a SECOND title showing through, and the eye cannot tell
    /// which screen it is on. The crew is hidden outright; the five paper cards lie
    /// straight on the same stage every other page stands on.
    /// </summary>
    public sealed class DraftCrewPage
    {
        static readonly Dictionary<string, string[]> RoleInfo = new Dictionary<string, string[]>
        {
            { "Sales", new[] { "Covers SELL — turns strangers into revenue", "sales" } },
            { "Business", new[] { "Covers RAISE and the spreadsheet — makes money appear", "business" } },
            { "Tech", new[] { "Covers BUILD — ships the product", "tech" } },
            { "Hustler", new[] { "Covers GRIT — does whatever nobody else will", "hustler" } },
            { "The Idea Friend", new[] { "Had the idea. That is the whole thing.", "idea" } },
        };

        /// Art for the new roles is still being drawn. Until it lands a role borrows
        /// the nearest existing portrait rather than rendering an empty card.
        static readonly Dictionary<string, string> ArtFallback = new Dictionary<string, string>
        {
            { "sales", "business" }, { "tech", "technical" }, { "hustler", "design" },
        };

        readonly FounderDraftScreen _s;
        RectTransform _page;
        RectTransform _body;
        RectTransform _row;
        RectTransform _recruitLayer;
        Image _donut;
        TextMeshProUGUI _donutLabel;

        public DraftCrewPage(FounderDraftScreen s) { _s = s; }

        public RectTransform Build()
        {
            _page = DrawnUI.FullRect(_s.Rect, "page_crew");
            FounderDraftScreen.Dim(_page);

            _body = DrawnUI.FullRect(_page, "crewbody");
            FounderDraftScreen.Heading(_body, "THE CREW", 56f, 60f, 26f);
            DrawnUI.HandLabel(_body,
                "Recruit cofounders. Split the company. They will remember the split.",
                64f, 116f, 28f, DrawnUI.WithAlpha(DrawnUI.Cream, 0.85f));

            _row = DrawnUI.Rect(_body, "crewrow", 24f, 190f, 1176f, 512f);

            // the cap table is a chart pinned up beside the crew, not a pie floating in
            // the dark: drawn on paper and captioned in the founder's own hand
            var sheet = GameUi.PaperSheet(_body, 1214f, 190f, 258f, 310f, 2, 4f, null, "donutsheet");
            GameUi.Tilt(sheet, 0.012f);
            _donut = DrawnChart.Mount(_body, "donut",
                DrawnChart.Donut(new[] { 100f }, new[] { DrawnUI.Sage }, 210),
                1240f, 206f, 210f, 210f);
            _donutLabel = DrawnUI.HandLabel(_body, "100%\nyours", 1240f, 286f, 30f, DrawnUI.Ink,
                                            210f, TextAlignmentOptions.Top);
            DrawnUI.HandLabel(_body, "the cap table", 1236f, 430f, 26f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 218f, TextAlignmentOptions.Top);

            _s.Nav(_body, "←", 48f, 930f, 100f, 70f, 30f, () => _s.TransitionTo(3));
            _s.Nav(_body, "NEXT: FIRST MONEY  →", 1090f, 920f, 410f, 84f, 32f,
                   () => _s.TransitionTo(5));

            _recruitLayer = DrawnUI.FullRect(_page, "recruit");
            _recruitLayer.gameObject.SetActive(false);
            Refresh();
            return _page;
        }

        // ══ the cap table ══════════════════════════════════════════════════════

        public void Refresh()
        {
            if (_page == null) return;
            double founderPct = _s.FounderPct();
            if (_donut != null)
            {
                var pcts = new List<float> { Mathf.Max(0f, (float)founderPct) };
                var cols = new List<Color> { DrawnUI.Sage };
                Color[] cfCols = { DrawnUI.Blue, DrawnUI.Yellow,
                                   DrawnUI.Hex("A78BBA"), DrawnUI.Hex("D98E7E") };
                for (int i = 0; i < _s.Cofounders.Count; i++)
                {
                    pcts.Add((float)(_s.Cofounders[i].Equity * _s.Dilution()));
                    cols.Add(cfCols[i % cfCols.Length]);
                }
                if (_s.SelFund != null && ContentDb.Num(_s.SelFund, "equity_cost", 0.0) > 0.0)
                {
                    pcts.Add((float)ContentDb.Num(_s.SelFund, "equity_cost", 0.0));
                    cols.Add(DrawnUI.Coral);
                }
                _donut.sprite = DrawnChart.Donut(pcts, cols, 210);
            }
            if (_donutLabel != null)
                _donutLabel.text = string.Format("{0:0}%\nyours", founderPct);
            RebuildCrew(founderPct);
        }

        void RebuildCrew(double founderPct)
        {
            if (_row == null) return;
            for (int i = _row.childCount - 1; i >= 0; i--)
                Object.Destroy(_row.GetChild(i).gameObject);

            int n = _s.Cofounders.Count;
            int slots = n + 1 + (n < FounderDraftScreen.MaxCofounders ? 1 : 0);
            float gap = 16f;
            float cw = Mathf.Clamp((_row.sizeDelta.x - (slots - 1) * gap) / slots, 170f, 234f);
            float ch = 500f;
            float x0 = (_row.sizeDelta.x - (slots * cw + (slots - 1) * gap)) * 0.5f;

            // ── the YOU card ──────────────────────────────────────────────────
            bool outvoted = founderPct < 50.0;
            var you = GameUi.PaperSheet(_row, x0, 0f, cw, ch, 0, outvoted ? 6f : 4f,
                outvoted ? DrawnUI.Coral : DrawnUI.Ink, "you");
            if (_s.SelArch != null)
                GameUi.Picture(you, "port", ArtCache.SpritePath(ContentDb.Str(_s.SelArch, "sprite")),
                               25f, 18f, cw - 50f, 180f);
            string yname = "YOU";
            string signed = (_s.FounderName ?? "").Trim();
            if (signed.Length > 0) yname = signed.Split(' ')[0].ToUpper();
            DrawnUI.HandLabel(you, yname + " · CEO", 0f, 210f, 28f, DrawnUI.Ink, cw,
                              TextAlignmentOptions.Top);
            DrawnUI.HandLabel(you, string.Format("{0:0}%", founderPct), 0f, 262f, 64f,
                outvoted ? DrawnUI.Coral : DrawnUI.Sage, cw, TextAlignmentOptions.Top);
            DrawnUI.HandLabel(you, "your slice", 0f, 358f, 26f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), cw, TextAlignmentOptions.Top);
            string cname = (_s.CompanyName ?? "").Trim();
            DrawnUI.HandLabel(you, cname.Length > 0 ? cname : "your company", 10f, 404f, 30f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.8f), cw - 20f, TextAlignmentOptions.Top);
            DrawnUI.HandLabel(you, outvoted ? "OUTVOTED!" : "is yours to run", 0f,
                outvoted ? 446f : 448f, outvoted ? 30f : 26f,
                outvoted ? DrawnUI.Coral : DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), cw,
                TextAlignmentOptions.Top);

            // ── one sheet per cofounder ───────────────────────────────────────
            for (int i = 0; i < n; i++)
            {
                DraftCofounder cf = _s.Cofounders[i];
                int index = i;
                bool noVest = !cf.Vesting;
                var card = GameUi.PaperSheet(_row, x0 + (i + 1) * (cw + gap), 0f, cw, ch,
                    i + 1, noVest ? 6f : 4f, noVest ? DrawnUI.Coral : DrawnUI.Ink, "cf");
                string slug = RoleSlug(cf.Role);
                string mood = MoodOf(cf, n);
                string art = CofounderArt(slug, mood);
                if (art.Length > 0) GameUi.Picture(card, "port", art, 30f, 14f, cw - 60f, 176f);

                GameUi.InkWord(card, "✕", cw - 54f, 10f, 44f, 44f, 24f, DrawnUI.Coral, () =>
                {
                    _s.Cofounders.RemoveAt(index);
                    _s.RefreshCapLine();
                });
                if (cf.Name.Length == 0) cf.Name = WorldGen.PersonName(_s.Prng);
                DrawnUI.HandLabel(card, cf.Name.Split(' ')[0] + " · " + cf.Role.ToUpper(),
                    0f, 196f, 24f, DrawnUI.Ink, cw, TextAlignmentOptions.Top);

                bool ft = cf.Commitment == "Full-time";
                GameUi.InkWord(card, ft ? "FULL-TIME" : "PART-TIME ⚠", 20f, 238f, cw - 40f, 46f,
                    24f, ft ? DrawnUI.Sage : DrawnUI.Coral, () =>
                    {
                        cf.Commitment = ft ? "Part-time" : "Full-time";
                        _s.RefreshCapLine();
                    });

                GameUi.InkWord(card, "−", 14f, 298f, 52f, 56f, 34f, DrawnUI.Coral, () =>
                {
                    cf.Equity = Mathf.Max(1f, (float)cf.Equity - 5f);
                    _s.RefreshCapLine();
                });
                DrawnUI.HandLabel(card, string.Format("{0:0}%", cf.Equity), 68f, 296f, 48f,
                    DrawnUI.Ink, cw - 136f, TextAlignmentOptions.Top);
                GameUi.InkWord(card, "+", cw - 66f, 298f, 52f, 56f, 34f, DrawnUI.Sage, () =>
                {
                    cf.Equity = Mathf.Min(60f, (float)cf.Equity + 5f);
                    _s.RefreshCapLine();
                });

                GameUi.InkWord(card, noVest ? "NO VESTING ⚠" : "VESTED ✓", 20f, 370f, cw - 40f,
                    46f, 24f, noVest ? DrawnUI.Coral : DrawnUI.Sage, () =>
                    {
                        cf.Vesting = !cf.Vesting;
                        _s.RefreshCapLine();
                    });

                string moodWord = mood == "happy" ? "☀ thrilled"
                    : (mood == "neutral" ? "steady" : "⛈ resentful…");
                Color moodCol = mood == "happy" ? DrawnUI.Sage
                    : (mood == "neutral" ? DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f) : DrawnUI.Coral);
                DrawnUI.HandLabel(card, moodWord, 0f, 438f, 28f, moodCol, cw,
                                  TextAlignmentOptions.Top);
            }

            // ── the empty chair ───────────────────────────────────────────────
            if (n < FounderDraftScreen.MaxCofounders)
            {
                var slot = GameUi.PaperSheet(_row, x0 + (n + 1) * (cw + gap), 0f, cw, ch, 4, 3f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), "empty");
                DrawnUI.HandLabel(slot, "☎", 0f, 150f, 78f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f), cw, TextAlignmentOptions.Top);
                DrawnUI.HandLabel(slot, "+ RECRUIT", 0f, 266f, 32f, DrawnUI.Ink, cw,
                    TextAlignmentOptions.Top);
                DrawnUI.HandLabel(slot, "an empty chair", 0f, 316f, 26f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), cw, TextAlignmentOptions.Top);
                var hit = slot.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                hit.raycastTarget = true;
                var b = slot.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.targetGraphic = hit;
                b.onClick.AddListener(OpenRecruit);
            }
        }

        // ══ who do you call ════════════════════════════════════════════════════

        void OpenRecruit()
        {
            if (_s.Cofounders.Count >= FounderDraftScreen.MaxCofounders) return;
            for (int i = _recruitLayer.childCount - 1; i >= 0; i--)
                Object.Destroy(_recruitLayer.GetChild(i).gameObject);
            _recruitLayer.gameObject.SetActive(true);
            if (_body != null) _body.gameObject.SetActive(false);

            GameUi.Scrim(_recruitLayer, new Color(0.05f, 0.05f, 0.06f, 0.55f), CloseRecruit);
            FounderDraftScreen.Heading(_recruitLayer, "WHO DO YOU CALL?", 48f, 188f, 214f);

            for (int i = 0; i < FounderDraftScreen.Roles.Length; i++)
            {
                string role = FounderDraftScreen.Roles[i];
                bool taken = RoleTaken(role);
                float[] leans = { -0.007f, 0.005f, -0.004f, 0.006f, -0.005f };
                var card = GameUi.PaperSheet(_recruitLayer, 188f + i * 236f, 306f, 220f, 424f,
                                             i, 4f, null, "call");
                GameUi.Tilt(card, leans[i % 5]);
                string slug = RoleInfo[role][1];
                string art = CofounderArt(slug, "neutral");
                if (art.Length > 0) GameUi.Picture(card, "port", art, 30f, 16f, 160f, 190f);
                DrawnUI.HandLabel(card, role.ToUpper(), 10f, 216f, 26f, DrawnUI.Ink, 200f,
                                  TextAlignmentOptions.Top);
                DrawnUI.HandLabel(card, RoleInfo[role][0], 10f, 262f, 25f, DrawnUI.Ink, 200f,
                                  TextAlignmentOptions.Top);
                if (taken)
                {
                    // already on the cap table: scribbled out in pen, not dimmed —
                    // fading the sheet turned the paper brown, which reads as dirty
                    GameUi.PenCross(card, 220f, 424f);
                    DrawnUI.HandLabel(card, "ON BOARD", 10f, 372f, 26f, DrawnUI.Coral, 200f,
                                      TextAlignmentOptions.Top);
                    continue;
                }
                var hit = card.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                hit.raycastTarget = true;
                var b = card.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.targetGraphic = hit;
                string pickRole = role;
                b.onClick.AddListener(() =>
                {
                    CloseRecruit();
                    _s.Cofounders.Add(new DraftCofounder
                    {
                        Role = pickRole,
                        Commitment = "Full-time",
                        Equity = 25.0,
                        Vesting = true,
                        Name = WorldGen.PersonName(_s.Prng),
                    });
                    _s.RefreshCapLine();
                });
            }

            // the ask and the way out are ONE line under the row: same band, same size
            DrawnUI.HandLabel(_recruitLayer, "whoever you call will want ~25% of the company.",
                190f, 764f, 28f, DrawnUI.Coral, 760f);
            _s.Nav(_recruitLayer, "☎ nobody. hang up.", 1032f, 764f, 320f, 62f, 28f, CloseRecruit);
        }

        void CloseRecruit()
        {
            if (_recruitLayer != null) _recruitLayer.gameObject.SetActive(false);
            if (_body != null) _body.gameObject.SetActive(true);
        }

        // ── the little rules ───────────────────────────────────────────────────

        bool RoleTaken(string role)
        {
            for (int i = 0; i < _s.Cofounders.Count; i++)
                if (_s.Cofounders[i].Role == role) return true;
            return false;
        }

        static string RoleSlug(string role)
        {
            string[] info;
            if (RoleInfo.TryGetValue(role ?? "", out info)) return info[1];
            return "tech";
        }

        /// A cofounder portrait, trying the role's own art first, then its stand-in.
        /// Returns "" when neither exists so the card is simply a card.
        static string CofounderArt(string slug, string mood)
        {
            string direct = string.Format("sprites/cf_{0}_{1}.png", slug, mood);
            if (RunwayPaths.ArtExists(direct)) return direct;
            string alt;
            if (ArtFallback.TryGetValue(slug ?? "", out alt))
            {
                string sub = string.Format("sprites/cf_{0}_{1}.png", alt, mood);
                if (RunwayPaths.ArtExists(sub)) return sub;
            }
            return "";
        }

        static string MoodOf(DraftCofounder cf, int n)
        {
            double fair = 100.0 / (n + 1);
            bool ft = cf.Commitment == "Full-time";
            if ((ft && cf.Role != "The Idea Friend" && cf.Equity < 10.0) || cf.Equity <= fair * 0.45)
                return "resentful";
            if (cf.Equity >= fair * 1.15) return "happy";
            return "neutral";
        }
    }
}
