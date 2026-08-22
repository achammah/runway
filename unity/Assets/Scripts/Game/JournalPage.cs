using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;

namespace Runway.Game
{
    /// One cell of an icon row: an id to answer with, a caption, and — optionally —
    /// a drawing, named the way the sprite folder names it ("itm_laptop", "gv/chart_1")
    /// or as a full art-relative path ("journal_icons/cash.png").
    public struct RowItem
    {
        public string Id;
        public string Text;
        public string Art;

        public static RowItem Of(string id, string text, string art = null)
        {
            return new RowItem { Id = id, Text = text, Art = art };
        }
    }

    /// <summary>
    /// THE LOG BOOK PAGE SHELL — journal_page.gd, ported.
    ///
    /// WHY THIS EXISTS. The page rules (one sheet, one hand, text that never leaves
    /// the paper, choices marked in pen, diegetic arrows) were written as prose three
    /// times and broken three times. Prose rules get forgotten; a constructor cannot.
    /// So the shell owns the geometry and the type, and a page script may only ADD
    /// CONTENT through the API — it is never handed a font, a size, or a free
    /// position, so it cannot pick a wrong one.
    ///
    /// THE PAGE IS DIVIDED INTO FOUR ZONES, fixed in advance, so every page in the
    /// book has the same anatomy:
    ///   TITLE     the one hand-lettered heading
    ///   BODY      what is happening, in the founder's hand
    ///   ENDING    the payload: drawings, selectable icons, or the written move
    ///   CONTROLS  navigation and commit, nothing else
    ///
    /// ZONES CASCADE, IN BOTH DIRECTIONS. A zone boundary is a BUDGET and may be
    /// crossed — the next zone moves down and the page still reads. A zone whose
    /// neighbours above are still empty floats UP to the first free rule, because
    /// five blank rules under the title is dead paper that then pushes the writing
    /// prompt off the bottom. The one boundary that is REAL is the paper's own edge:
    /// past it ink lands on the room, which is the defect this page is rejected for.
    ///
    /// THE SHEET IS DRAWN, NOT PHOTOGRAPHED. The Godot original lays this out over
    /// `assets/ui/logbook_page.png` and reads the writable silhouette out of a zones
    /// file extracted from that PNG's alpha. Neither ships in this project, so the
    /// paper is drawn with the same hand every other card in the game uses and the
    /// silhouette is the rectangle the drawn sheet actually is — which is what the
    /// original's own `span_at()` returns anyway, because it rotates one Control to
    /// match the drawn lean and works upright inside it.
    /// </summary>
    public sealed class JournalPage : MonoBehaviour
    {
        // ── the geometry, measured off the original ────────────────────────────
        static readonly Vector2 ArtPx = new Vector2(1095f, 1462f);
        static readonly Vector2 PageSize = new Vector2(862f, 1152f);
        static readonly Vector2 PagePos = new Vector2(337f, -24f);
        const float PageTilt = -0.012f;                       // a hair on top of the drawn lean
        static readonly Vector2 PaperOriginTex = new Vector2(74f, 152f);
        static readonly Vector2 PaperSizeTex = new Vector2(858f, 1232f);
        const float PaperTilt = -0.069f;                      // the lean drawn INTO the paper
        const float Scale = 862f / 1095f;
        static readonly Vector2 SheetPos = new Vector2(PaperOriginTex.x * Scale, PaperOriginTex.y * Scale);
        static readonly Vector2 SheetSize = new Vector2(PaperSizeTex.x * Scale, PaperSizeTex.y * Scale);
        const float MarginX = 46f;
        const float MarginTop = 26f;
        const float MarginBot = 44f;

        /// 17 printed rules fit inside the sheet's margins, so this allocation is what
        /// the paper can physically hold.
        static readonly string[] ZoneOrder = { "title", "body", "ending", "controls" };
        static readonly int[] ZoneRules = { 3, 4, 7, 2 };

        /// TWO SIZES. There is no third — the owner's defect was "different font size
        /// and style, as if it was not written".
        public const float SizeTitle = 64f;
        public const float SizeBody = 34f;
        internal const float Gap = 22f;
        /// AN ICON SMALLER THAN THIS IS NOT AN ICON, IT IS A SPECK.
        internal const float IconMinH = 96f;
        internal const float CapGap = 8f;
        internal const int CapMaxLines = 3;

        const float RuleFirstFrac = 0.17784f;
        const float RulePitchFrac = 0.04446f;

        /// THE PAGE IS WRITTEN, NOT PRINTED: lines arrive left to right, drawings fade
        /// up in order, the ruled writing line lands last. The performance itself lives
        /// in `PageReveal` — this file decides WHAT goes on the paper, that one decides
        /// how the hand puts it there.

        public event Action<string> ChoiceMade;
        public event Action<string> Written;
        public event Action PrevPage;
        public event Action NextPage;

        /// Re-reading an old page: everything is already ink.
        public bool Instant;
        /// A composed week image to stand behind the sheet.
        public string BackdropPath = "";

        /// Page-local content space — everything lands here, in PAPER coordinates.
        public RectTransform Space { get; private set; }

        /// The hand that writes this page in. Built on first use so a page composed
        /// before Build() cannot lose its first line.
        PageReveal Reveal
        {
            get
            {
                if (_reveal == null)
                {
                    _reveal = new PageReveal(this);
                    _reveal.OnFieldShown(() =>
                    {
                        if (_input != null) _input.ActivateInputField();
                    });
                }
                return _reveal;
            }
        }

        RectTransform _sheet;
        TMP_InputField _input;
        readonly Dictionary<string, float> _cursor = new Dictionary<string, float>();
        readonly Dictionary<string, float[]> _zonePx = new Dictionary<string, float[]>();
        readonly Dictionary<string, bool> _wrote = new Dictionary<string, bool>();
        PageReveal _reveal;
        float _topPad;
        float _paperBot;
        float _lastBlockY;
        bool _built;
        string _tag = "";

        public static JournalPage Create(RectTransform parent)
        {
            var rt = DrawnUI.FullRect(parent, "journalpage");
            return rt.gameObject.AddComponent<JournalPage>();
        }

        // ══ build ══════════════════════════════════════════════════════════════

        public void Build(string titleText)
        {
            if (_built) return;
            _built = true;
            _tag = titleText ?? "";
            var root = GetComponent<RectTransform>();

            // a generated week image outranks the stock ground as the room behind
            if (!string.IsNullOrEmpty(BackdropPath))
            {
                var bgRt = DrawnUI.FullRect(root, "backdrop");
                var bg = bgRt.gameObject.AddComponent<RawImage>();
                bg.raycastTarget = false;
                bg.enabled = false;
                var boot = Boot.Instance;
                if (boot != null)
                    boot.StartCoroutine(SheetLoop.LoadTexture(RunwayPaths.FileUrl(BackdropPath), tex =>
                    {
                        if (bg == null || tex == null) return;
                        bg.texture = tex;
                        bg.enabled = true;
                    }));
                // the page is the subject; the room is where you are
                DrawnUI.FullFill(root, "dim", new Color(0f, 0f, 0f, 0.45f));
            }

            _sheet = DrawnUI.Rect(root, "sheet", PagePos.x, PagePos.y, PageSize.x, PageSize.y);
            GameUi.TiltCentre(_sheet, PageTilt);

            // THE PAPER LEANS. Rather than wrap text to a diagonal, one Control is
            // rotated to match the drawn lean; inside it the paper IS an upright
            // rectangle, so plain axis-aligned layout is correct by construction.
            var paper = DrawnUI.Rect(_sheet, "paper", SheetPos.x, SheetPos.y,
                                     SheetSize.x, SheetSize.y);
            GameUi.Tilt(paper, PaperTilt);
            var shadow = DrawnUI.Fill(paper, "shadow", new Color(0f, 0f, 0f, 0.28f),
                                      9f, 12f, SheetSize.x, SheetSize.y);
            shadow.raycastTarget = false;
            var body = DrawnUI.Fill(paper, "cream", DrawnUI.Cream, 0f, 0f, SheetSize.x, SheetSize.y);
            body.raycastTarget = false;
            DrawnUI.AddInkEdge(paper, SheetSize, new DrawnUI.PaperStyle
            {
                ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 4f,
                StepsPerEdge = 18, Jitter = 2f, Thickness = 3.5f, Seed = 3,
            });
            Space = paper;

            // CONTENT LIVES ONLY WHERE THE PAPER IS ACTUALLY ON SCREEN.
            float sheetTop = PagePos.y + SheetPos.y;
            float visTop = Mathf.Max(0f, -sheetTop) + MarginTop;
            float visBot = Mathf.Min(SheetSize.y, RunwayPaths.StageHeight - sheetTop) - MarginBot;
            _topPad = visTop;
            _paperBot = visTop + Mathf.Max(visBot - visTop, 120f);

            // ZONES ARE MEASURED IN PRINTED RULES, never in fractions of the artwork.
            float pitch = RulePitch();
            float y0 = (RuleFirstFrac * ArtPx.y - PaperOriginTex.y) * Scale;
            float y = Mathf.Max(y0, _topPad);
            PrintRuling(y0, pitch);
            for (int i = 0; i < ZoneOrder.Length; i++)
            {
                float h = ZoneRules[i] * pitch;
                _cursor[ZoneOrder[i]] = y;
                _zonePx[ZoneOrder[i]] = new[] { y, y + h };
                y += h;
            }

            if (!string.IsNullOrEmpty(titleText)) Title(titleText);
        }

        /// The ruling the page is written on — the same faint sage the beat's card uses.
        void PrintRuling(float first, float pitch)
        {
            int len = Mathf.RoundToInt(SheetSize.x - MarginX * 2f);
            Sprite s = DrawnUI.WobbleLineSprite(len, 2f, 33, 1.1f, 12, 3);
            for (float y = first; y < _paperBot - 4f; y += pitch)
            {
                var rt = DrawnUI.Rect(Space, "rule", MarginX - 3f, y + 4f, len + 6f, 7f);
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = s;
                img.color = DrawnUI.WithAlpha(DrawnUI.Sage, 0.28f);
                img.raycastTarget = false;
            }
        }

        // ══ the writable silhouette ════════════════════════════════════════════

        /// Writable span at a y inside the SHEET, as [left, right]. Constant, because
        /// `Space` is already rotated to the paper's lean.
        public Vector2 SpanAt(float y) { return new Vector2(MarginX, SheetSize.x - MarginX); }

        public float ZoneBottom(string zone)
        {
            float[] z;
            return _zonePx.TryGetValue(zone, out z) ? z[1] : WritableBottom();
        }

        /// THE ONE BOUNDARY THAT IS REAL: past this, ink lands on the room.
        public float WritableBottom()
        {
            return _paperBot > 0f ? _paperBot : SheetSize.y - MarginBot;
        }

        /// The last two printed rules belong to CONTROLS — the lock line and the
        /// arrows — and nothing else may ever reach them.
        internal float HardFloor() { return WritableBottom() - 2f * RulePitch(); }

        public float RoomLeft(string zone = "ending")
        {
            return ZoneBottom(zone) - Cursor(zone);
        }

        /// The paper that is REALLY left before the controls fence, after every
        /// cascade — the number a host must consult before adding a drawing row.
        public float RoomToFence(string zone = "ending")
        {
            Cascade(zone);
            return HardFloor() - Snap(Cursor(zone));
        }

        internal float Cursor(string zone)
        {
            float v;
            return _cursor.TryGetValue(zone, out v) ? v : 0f;
        }

        bool Wrote(string zone)
        {
            bool v;
            return _wrote.TryGetValue(zone, out v) && v;
        }

        internal void Cascade(string zone)
        {
            int idx = Array.IndexOf(ZoneOrder, zone);
            if (idx <= 0) return;
            float floorY = 0f;
            for (int i = 0; i < idx; i++)
                if (Wrote(ZoneOrder[i])) floorY = Mathf.Max(floorY, Cursor(ZoneOrder[i]));
            if (Wrote(zone)) _cursor[zone] = Snap(Mathf.Max(Cursor(zone), floorY));
            else if (floorY > 0f) _cursor[zone] = Snap(Mathf.Max(floorY, _topPad));
        }

        /// Pitch of the printed ruling, in SHEET-local pixels.
        public float RulePitch() { return RulePitchFrac * ArtPx.y * Scale; }

        /// The next printed rule at or after y, so a baseline always lands on one.
        /// THE EPSILON IS LOAD-BEARING: every zone starts at first + k*pitch, which is
        /// a whole number in arithmetic and a hair above it in floats — and a hair
        /// above sent ceil() to the NEXT rule, throwing away a full line at the top of
        /// every zone and at every cascade.
        internal float Snap(float y)
        {
            float first = (RuleFirstFrac * ArtPx.y - PaperOriginTex.y) * Scale;
            float pitch = RulePitch();
            if (pitch <= 1f) return y;
            if (y <= first) return first;
            return first + Mathf.Ceil((y - first) / pitch - 0.01f) * pitch;
        }

        /// A line occupies whole rules: body text ONE, a big title two. The 0.78 is
        /// load-bearing — a font's reported height includes padding handwriting does
        /// not visually occupy.
        internal float LineAdvance(float sz)
        {
            float pitch = RulePitch();
            float h = FontHeight(sz);
            if (pitch <= 1f) return h * 1.08f;
            return pitch * Mathf.Max(1f, Mathf.Ceil(h * 0.78f / pitch));
        }

        /// TMP metrics are not Godot metrics, so the hand's line box is approximated
        /// once, here. Patrick Hand at 34 measures ~46px tall; the factor keeps body
        /// text on ONE rule and the 64px title on two, which is the whole contract.
        public static float FontHeight(float sz) { return sz * 1.35f; }

        // ══ content ════════════════════════════════════════════════════════════

        public void Title(string text)
        {
            Shaped(text, SizeTitle, DrawnUI.Ink, "title", true);
        }

        public void Line(string text, bool faint = false, string zone = "body")
        {
            Shaped(text, SizeBody, faint ? DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f) : DrawnUI.Ink,
                   zone, false);
        }

        /// Prose that must leave room for what follows it: SHRINK BEFORE CUTTING
        /// (owner: "text is being too much cut, so unclear") — a smaller hand keeps
        /// the whole thought; the ellipsis only survives as the final fallback.
        public void LineFitted(string text, float reserve, string zone = "body", bool faint = false)
        {
            Cascade(zone);
            float start = Snap(Cursor(zone));
            float avail = HardFloor() - start - reserve;
            float[] ladder = { SizeBody, 30f, 27f };
            for (int i = 0; i < ladder.Length; i++)
            {
                int fitS = Mathf.Max(Mathf.FloorToInt(avail / LineAdvance(ladder[i])), 1);
                if (WrapLines(text, ladder[i]).Count <= fitS)
                {
                    Shaped(text, ladder[i],
                        faint ? DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f) : DrawnUI.Ink, zone, false);
                    return;
                }
            }
            // even a starved page keeps TWO small lines — one line cut mid-sentence
            // reads as a rendering bug, not a diary
            int fit = Mathf.Max(Mathf.FloorToInt(avail / LineAdvance(27f)), 2);
            List<string> lines = WrapLines(text, 27f);
            string told = text;
            if (lines.Count > fit)
            {
                var kept = lines.GetRange(0, fit);
                string lastl = kept[fit - 1];
                int cut = lastl.LastIndexOf(' ');
                kept[fit - 1] = (cut > 24 ? lastl.Substring(0, cut) : lastl) + " …";
                told = string.Join(" ", kept.ToArray());
            }
            Shaped(told, 27f, faint ? DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f) : DrawnUI.Ink, zone, false);
        }

        /// Lay text out line by line against the real paper edges, snapping every
        /// baseline to a printed rule. Content stops at the fence; a line that will not
        /// fit is CUT with an ellipsis, because prose is the one thing on this page
        /// that can lose a tail and still work.
        void Shaped(string text, float sz, Color col, string zone, bool centre)
        {
            Cascade(zone);
            float y = Snap(Cursor(zone));
            float lh = LineAdvance(sz);
            float fence = zone == "controls" ? WritableBottom() : HardFloor();
            _lastBlockY = y;
            List<string> lines = WrapLines(text, sz);
            bool placed = false;
            TextMeshProUGUI last = null;
            for (int i = 0; i < lines.Count; i++)
            {
                if (y + lh > fence + 0.5f && placed)
                {
                    if (last != null)
                    {
                        string t = last.text;
                        int cut = t.LastIndexOf(' ');
                        last.text = (cut > 24 ? t.Substring(0, cut) : t) + " …";
                    }
                    Debug.LogWarning(string.Format(
                        "RUNWAY! JournalPage[{0}]: {1} cut {2} line(s) at the paper's fence — "
                        + "shorten this copy.", _tag, zone, lines.Count - i));
                    break;
                }
                last = Place(lines[i], sz, col, y, lh, centre);
                placed = true;
                y += lh;
            }
            _cursor[zone] = y + Gap;
            _wrote[zone] = true;
            Overrun(zone);
        }

        TextMeshProUGUI Place(string text, float sz, Color col, float y, float lh, bool centre)
        {
            Vector2 sp = SpanAt(y);
            float x = sp.x;
            if (centre)
            {
                float w = DrawnUI.MeasureWidth(text, sz);
                x = sp.x + Mathf.Max((sp.y - sp.x - w) * 0.5f, 0f);
            }
            var t = DrawnUI.HandLabel(Space, text, x, y, sz, col, sp.y - x,
                                      TextAlignmentOptions.TopLeft);
            t.textWrappingMode = TextWrappingModes.NoWrap;      // every line is already wrapped by hand
            t.rectTransform.sizeDelta = new Vector2(sp.y - x, lh);
            if (!Instant)
            {
                t.maxVisibleCharacters = 0;
                Reveal.Enqueue("line", t, PageReveal.LineSecs(text, sz, SizeTitle));
            }
            return t;
        }

        /// Greedy wrap against the constant writable span — ONE implementation, so
        /// measuring for a budget and placing for real can never disagree.
        List<string> WrapLines(string text, float sz)
        {
            var outp = new List<string>();
            Vector2 sp = SpanAt(0f);
            float avail = sp.y - sp.x;
            if (string.IsNullOrEmpty(text)) return outp;
            string[] words = text.Split(' ');
            string cur = "";
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length == 0) continue;
                string trial = cur.Length == 0 ? words[i] : cur + " " + words[i];
                if (DrawnUI.MeasureWidth(trial, sz) <= avail || cur.Length == 0) cur = trial;
                else { outp.Add(cur); cur = words[i]; }
            }
            if (cur.Length > 0) outp.Add(cur);
            return outp;
        }

        // ── icon rows ──────────────────────────────────────────────────────────

        /// A row of selectable icons, and the written move. Both are built by
        /// `PageBlocks` against the geometry this file owns — they are the two biggest
        /// blocks on any page and they are the only two that are not just ink.
        public RectTransform IconRow(IList<RowItem> items, Vector2 cell, string zone = "ending")
        {
            return PageBlocks.IconRow(this, items, cell, zone);
        }

        public TMP_InputField WriteField(string prompt = "...or write what you actually do",
                                         string zone = "ending")
        {
            return PageBlocks.WriteField(this, prompt, zone);
        }

        internal void Select(RectTransform row, string id)
        {
            int chosen = IndexOfId(row, id);
            for (int i = 0; i < row.childCount; i++)
            {
                var slot = row.GetChild(i) as RectTransform;
                if (slot == null) continue;
                bool mine = i == chosen;
                DrawnUI.Group(slot).alpha = mine ? 1f : 0.55f;
                Transform ring = slot.Find("ring");
                if (ring != null) ring.gameObject.SetActive(mine);
            }
            var cm = ChoiceMade;
            if (cm != null) cm(id);
        }

        readonly Dictionary<RectTransform, List<string>> _rowIds =
            new Dictionary<RectTransform, List<string>>();

        int IndexOfId(RectTransform row, string id)
        {
            List<string> ids;
            if (_rowIds.TryGetValue(row, out ids)) return ids.IndexOf(id);
            return -1;
        }

        // ── the seam onto PageBlocks ───────────────────────────────────────────

        /// The few doors `PageBlocks` needs into this file's private geometry. They are
        /// internal, not public: a page host may only ever ADD CONTENT.
        internal static float SheetWidth { get { return SheetSize.x; } }
        internal PageReveal RevealHand { get { return Reveal; } }
        internal void SetCursor(string zone, float v) { _cursor[zone] = v; }
        internal void MarkWrote(string zone) { _wrote[zone] = true; }
        internal void SetInput(TMP_InputField f) { _input = f; }
        internal void RegisterRow(RectTransform row, List<string> ids) { _rowIds[row] = ids; }
        internal void RaiseWritten(string s) { var w = Written; if (w != null) w(s); }

        public string WrittenText()
        {
            return _input != null ? _input.text.Trim() : "";
        }

        public TMP_InputField InputField() { return _input; }

        public void SetWritten(string t)
        {
            if (_input != null) _input.text = t ?? "";
        }

        public void FocusWriting()
        {
            if (_input != null) _input.ActivateInputField();
        }

        // ── the margin, and the way onward ─────────────────────────────────────

        /// A founder annotates their own log: a star when the world said brilliant, a
        /// hard double strike when it said backfired.
        public void MarginMark(string kind)
        {
            var rt = DrawnUI.Rect(Space, "mark", 16f, _lastBlockY - 6f, 34f, 40f);
            if (kind == "star")
            {
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = DrawnUI.RingSprite(13f, 3.5f, 2.6f, 31, 3, false);
                img.color = DrawnUI.Coral;
                img.raycastTarget = false;
            }
            else
            {
                GameUi.PenCross(rt, 34f, 40f);
            }
            if (!Instant)
            {
                var g = DrawnUI.Group(rt);
                g.alpha = 0f;
                Reveal.Enqueue("fade", rt, 0.18f);
            }
        }

        /// Navigation lives in the CONTROLS zone and is drawn, never chrome.
        public void Arrows(bool showPrev, bool showNext)
        {
            float y = WritableBottom() - 56f;
            Vector2 sp = SpanAt(y + 26f);
            if (showPrev)
                Arrow(sp.x, y, false, () => { var p = PrevPage; if (p != null) p(); });
            if (showNext)
                Arrow(sp.y - 90f, y, true, () => { var n = NextPage; if (n != null) n(); });
        }

        void Arrow(float x, float y, bool forward, Action onClick)
        {
            var rt = DrawnUI.Rect(Space, "arrow", x, y, 90f, 52f);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            var glyph = DrawnUI.HandLabel(rt, forward ? "→" : "←", 0f, 0f, 40f, DrawnUI.Ink, 90f,
                                          TextAlignmentOptions.Center);
            glyph.rectTransform.anchorMin = Vector2.zero;
            glyph.rectTransform.anchorMax = Vector2.one;
            glyph.rectTransform.offsetMin = Vector2.zero;
            glyph.rectTransform.offsetMax = Vector2.zero;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = hit;
            btn.onClick.AddListener(() => onClick());
            var tint = rt.gameObject.AddComponent<HoverTint>();
            tint.Setup(glyph, DrawnUI.Ink, DrawnUI.Coral, null, 1f);
            if (!Instant)
            {
                var g = DrawnUI.Group(rt);
                g.alpha = 0f;
                Reveal.Enqueue("fade", rt, 0.18f);
            }
        }

        /// The only thing warned about is the fault the page is REJECTED for: ink past
        /// the bottom edge of the paper, printed onto the room. A zone sliding down is
        /// the cascade working, not a fault.
        internal void Overrun(string zone)
        {
            float y = Cursor(zone);
            float bot = WritableBottom();
            if (y <= bot) return;
            float over = y - bot;
            int lines = Mathf.Max(1, Mathf.CeilToInt(over / Mathf.Max(LineAdvance(SizeBody), 1f)));
            Debug.LogWarning(string.Format(
                "RUNWAY! JournalPage[{0}]: {1} ran {2:0}px off the bottom of the paper — cut {3} "
                + "line(s) of copy from this page, shorten the captions, or move {1} to the next sheet.",
                _tag, zone, over, lines));
        }

        // ══ the page turn: paper moves, the room does not ══════════════════════

        /// The new sheet arrives the way a turned page lands: from the side you are
        /// heading, a touch rotated, settling with a small overshoot. Only the SHEET
        /// moves — the room behind is the same room, so it holds still.
        public void EnterTurn(int dir)
        {
            if (_sheet == null) return;
            Reveal.CloseGate();
            StartCoroutine(EnterRoutine(dir));
        }

        IEnumerator EnterRoutine(int dir)
        {
            Vector2 home = _sheet.anchoredPosition;
            Vector2 from = home + new Vector2(150f * dir, -18f);
            var g = DrawnUI.Group(_sheet);
            g.alpha = 0f;
            _sheet.anchoredPosition = from;
            GameUi.Tilt(_sheet, PageTilt + 0.05f * dir);
            // the new paper WAITS 80ms while the old one clears: that gap is what makes
            // two drawings read as pages of one book instead of a crossfade
            yield return new WaitForSecondsRealtime(0.08f);
            float t = 0f;
            while (t < 0.24f)
            {
                t += Time.unscaledDeltaTime;
                float k = DrawnUI.EaseOutCubic(t / 0.24f);
                if (_sheet == null) yield break;
                _sheet.anchoredPosition = Vector2.Lerp(from, home, k);
                GameUi.Tilt(_sheet, Mathf.Lerp(PageTilt + 0.05f * dir, PageTilt, k));
                g.alpha = Mathf.Min(1f, k * 2f);
                yield return null;
            }
            _sheet.anchoredPosition = home;
            GameUi.Tilt(_sheet, PageTilt);
            g.alpha = 1f;
            Reveal.OpenGate();
        }

        /// The old sheet leaves UNDER the new one: it moves first and only then
        /// vanishes — a page that dissolves in place reads as a crossfade, not as a
        /// lifted sheet of paper. The node frees itself when it is gone.
        public void ExitTurn(int dir)
        {
            StartCoroutine(ExitRoutine(dir));
        }

        IEnumerator ExitRoutine(int dir)
        {
            var self = GetComponent<RectTransform>();
            var g = DrawnUI.Group(self);
            g.blocksRaycasts = false;
            if (_sheet == null) { Destroy(gameObject); yield break; }
            Vector2 home = _sheet.anchoredPosition;
            Vector2 to = home + new Vector2(-280f * dir, -44f);
            float t = 0f;
            while (t < 0.18f)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / 0.18f);
                if (_sheet == null) break;
                _sheet.anchoredPosition = Vector2.Lerp(home, to, k);
                GameUi.Tilt(_sheet, Mathf.Lerp(PageTilt, PageTilt - 0.08f * dir, k));
                g.alpha = 1f - k;
                yield return null;
            }
            if (this != null && gameObject != null) Destroy(gameObject);
        }

        // ══ the performance: the page writes itself in ═════════════════════════

        /// One click anywhere on an arriving page finishes the writing; it never also
        /// chooses, focuses, or turns anything.
        public bool Revealing { get { return Reveal.Revealing; } }

        public void FinishReveal() { Reveal.Finish(); }
    }
}
