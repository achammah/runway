using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;

namespace Runway.Game
{
    /// <summary>
    /// THE TWO BLOCKS THAT ARE NOT JUST INK — journal_page.gd's `icon_row` and
    /// `write_field`, split out of the page shell they belong to.
    ///
    /// `JournalPage` owns the paper: the silhouette, the printed rules every baseline
    /// lands on, the four zones and the cascade between them. These two build ON that
    /// geometry and are the only page elements with an internal layout of their own —
    /// a row whose captions decide its height, and a field that has to fit between the
    /// last written line and the controls fence.
    ///
    /// They reach into the page through a small INTERNAL surface (`Cascade`, `Cursor`,
    /// `SetCursor`, `MarkWrote`, `Overrun`, `HardFloor`, `RevealHand`). That surface is
    /// deliberately not public: a page HOST may only ever add content.
    /// </summary>
    public static class PageBlocks
    {
        // ══ a row of selectable icons ══════════════════════════════════════════

        /// State is DRAWN and choosing is a pen mark — never a button, never a
        /// bordered chip.
        ///
        /// THE CAPTION DECIDES THE CELL, not the other way round, and captions are
        /// drawn at the COLUMN width so two neighbours can never overlap — that is what
        /// let "Buy fans. Boring. Works." print straight through the line below it, and
        /// what clipped "Enterprise" to "Enterpris".
        ///
        /// THE PICTURE IS NEVER WHAT IS LEFT OVER: the caller's cell height is the
        /// row's BUDGET, the caption comes out of it first, and if the rest falls under
        /// IconMinH the ROW GROWS rather than the drawing shrinking — a 10px portrait
        /// cannot be rescued and a taller row can.
        public static RectTransform IconRow(JournalPage p, IList<RowItem> items,
                                            Vector2 cell, string zone)
        {
            p.Cascade(zone);
            float y = p.Cursor(zone);
            Vector2 sp = p.SpanAt(y + cell.y * 0.5f);
            float avail = sp.y - sp.x;
            int n = Mathf.Max(items.Count, 1);
            float step = Mathf.Min(cell.x + 28f, avail / n);
            float x0 = sp.x + (avail - step * n) * 0.5f;
            float capW = Mathf.Max(step - 14f, 60f);

            var caps = new List<string>();
            float capH = 0f;
            bool drawsIcon = false;
            for (int i = 0; i < items.Count; i++)
            {
                string c = CapLines(items[i].Text ?? "", capW);
                caps.Add(c);
                capH = Mathf.Max(capH, CountLines(c) * p.LineAdvance(JournalPage.SizeBody) * 0.62f
                                       + JournalPage.SizeBody * 0.5f);
                if (!string.IsNullOrEmpty(items[i].Art)) drawsIcon = true;
            }
            capH = Mathf.Max(capH, JournalPage.SizeBody * 1.2f);

            // a caption-only row reserves NO picture space — an empty 96px strip above
            // the words is dead paper, and dead paper pushes the writing off the sheet
            float iconH = 0f;
            if (drawsIcon)
            {
                iconH = Mathf.Max(cell.y - capH - JournalPage.CapGap, JournalPage.IconMinH);
                // THE ROW GIVES BEFORE THE PAGE DOES: a smaller jar is a jar, but a
                // prompt printed on the room is a broken page.
                float over = (y + iconH + JournalPage.CapGap + capH) - p.HardFloor();
                if (over > 0f) iconH = Mathf.Max(iconH - over, JournalPage.IconMinH);
            }
            float cellH = iconH + (drawsIcon ? JournalPage.CapGap : 0f) + capH;

            // the full-width band must never swallow a click meant for a slot, so it
            // carries no graphic of its own
            var row = DrawnUI.Rect(p.Space, "row", 0f, y, JournalPage.SheetWidth, cellH);
            var slots = new List<RectTransform>();
            var ids = new List<string>();
            p.RegisterRow(row, ids);

            for (int i = 0; i < items.Count; i++)
            {
                RowItem it = items[i];
                var slot = DrawnUI.Rect(row, "slot", x0 + step * i, 0f, capW, cellH);
                var hit = slot.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                // an option that has not been inked yet cannot be chosen
                hit.raycastTarget = p.Instant;
                var ring = GameUi.PenRing(slot, -6f, -4f, capW + 12f, cellH + 8f,
                                          DrawnUI.Coral, 9 + i);
                ring.gameObject.SetActive(false);
                if (!string.IsNullOrEmpty(it.Art))
                    GameUi.Picture(slot, "icon", ResolveArt(it.Art), 0f, 0f, capW, iconH);
                var cap = DrawnUI.HandLabel(slot, caps[i], 0f, cellH - capH,
                    JournalPage.SizeBody, DrawnUI.Ink, capW, TextAlignmentOptions.Top);
                cap.rectTransform.sizeDelta = new Vector2(capW, capH);

                string id = it.Id ?? i.ToString();
                ids.Add(id);
                var btn = slot.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = hit;
                RectTransform capturedRow = row;
                btn.onClick.AddListener(() => p.Select(capturedRow, id));
                if (!p.Instant) DrawnUI.Group(slot).alpha = 0f;
                slots.Add(slot);
            }
            if (!p.Instant)
                p.RevealHand.Enqueue("icons", slots, PageReveal.IconsSecs(slots.Count));

            p.SetCursor(zone, y + cellH + JournalPage.Gap);
            p.MarkWrote(zone);
            p.Overrun(zone);
            return row;
        }

        static string ResolveArt(string art)
        {
            if (string.IsNullOrEmpty(art)) return "";
            if (art.EndsWith(".png")) return art;
            return ArtCache.SpritePath(art);
        }

        static int CountLines(string s)
        {
            if (string.IsNullOrEmpty(s)) return 1;
            int n = 1;
            for (int i = 0; i < s.Length; i++) if (s[i] == '\n') n++;
            return n;
        }

        /// A caption capped at three wrapped lines, cut with an ellipsis past that.
        /// Three wrapped lines is where a caption stops being a label and starts being
        /// the paragraph that shoves the writing field off the sheet. Explicit newlines
        /// are the caller's layout and are kept as line breaks.
        static string CapLines(string text, float w)
        {
            var outp = new List<string>();
            string[] segs = (text ?? "").Split('\n');
            for (int s = 0; s < segs.Length; s++)
            {
                if (outp.Count >= JournalPage.CapMaxLines) break;
                string[] words = segs[s].Split(' ');
                string cur = "";
                for (int i = 0; i < words.Length; i++)
                {
                    if (words[i].Length == 0) continue;
                    string trial = cur.Length == 0 ? words[i] : cur + " " + words[i];
                    if (DrawnUI.MeasureWidth(trial, JournalPage.SizeBody) <= w || cur.Length == 0)
                        cur = trial;
                    else
                    {
                        outp.Add(cur);
                        cur = words[i];
                        if (outp.Count >= JournalPage.CapMaxLines) break;
                    }
                }
                if (cur.Length > 0 && outp.Count < JournalPage.CapMaxLines) outp.Add(cur);
                else if (cur.Length > 0 && outp.Count >= JournalPage.CapMaxLines)
                {
                    outp[JournalPage.CapMaxLines - 1] =
                        outp[JournalPage.CapMaxLines - 1] + " …";
                    break;
                }
            }
            return string.Join("\n", outp.ToArray());
        }

        // ══ the written move ═══════════════════════════════════════════════════

        /// THE FREE WRITTEN MOVE — the originality of the game. The player writes what
        /// they actually do and the world adjudicates it. Deliberately NOT a widget:
        /// no box, no border, no fill. THE RULED LINE IS THE FIELD.
        ///
        /// SCROLL, DO NOT GROW. Growing to fit pushed a long written move straight
        /// through the bottom of the page; the field keeps the height the zone allows
        /// and scrolls inside it, so the player can write as much as they like and the
        /// paper still holds.
        public static TMP_InputField WriteField(JournalPage p, string prompt, string zone)
        {
            p.Cascade(zone);
            // When the sheet is nearly spent the PROMPT is the line that steps aside —
            // the ruled hint and the resting nib already say "write here", and a prompt
            // printed on the page curl says something worse about the whole book.
            float roomNow = p.HardFloor() - p.Snap(p.Cursor(zone));
            if (!string.IsNullOrEmpty(prompt)
                && roomNow >= p.LineAdvance(JournalPage.SizeBody) + p.RulePitch() * 2f)
                p.Line(prompt, true, zone);
            p.Cascade(zone);

            float y = p.Cursor(zone);
            Vector2 sp = p.SpanAt(y + JournalPage.SizeBody);
            float pitch = p.RulePitch();

            // TWO FULL SLOTS MINIMUM, up to five when the page has room — on the
            // decision spread the field IS the page. The CONTROLS FENCE wins over the
            // zone, and the PAPER EDGE wins over the fence: on the rare page whose
            // upstream content lands deep, the floor bends to the sheet rather than
            // inking the room. The field scrolls, so a squeezed field still takes any
            // length of writing.
            float hgt = Mathf.Max(pitch * 2f, Mathf.Min(pitch * 5f, p.HardFloor() - y - 8f));
            hgt = Mathf.Min(hgt, p.HardFloor() - y - 8f);
            hgt = Mathf.Max(hgt, pitch * 2f) + 12f;
            hgt = Mathf.Min(hgt, p.WritableBottom() - y - 2f);
            hgt = Mathf.Max(hgt, pitch * 1.2f);

            float w = sp.y - sp.x;
            // the ruling you write along, at the PAGE'S pitch, so typed ink rides the
            // printed rules like every drawn line and can never be struck through
            int len = Mathf.RoundToInt(w);
            Sprite ruleSprite = DrawnUI.WobbleLineSprite(len, 3f, 33, 1.1f, 17, 4);
            for (float ry = pitch; ry < hgt + 2f; ry += pitch)
            {
                var rrt = DrawnUI.Rect(p.Space, "writerule", sp.x - 3f, y + ry + 1f, len + 6f, 9f);
                var rimg = rrt.gameObject.AddComponent<Image>();
                rimg.sprite = ruleSprite;
                rimg.color = DrawnUI.WithAlpha(DrawnUI.Coral, 0.45f);
                rimg.raycastTarget = false;
            }
            // the pen nib resting at the first rule until you have written something
            var nib = DrawnUI.Rect(p.Space, "nib", sp.x - 3f, y + pitch - 17f, 9f, 9f);
            var nibImg = nib.gameObject.AddComponent<Image>();
            nibImg.sprite = DrawnUI.RingSprite(9f, 1f, 0f, 5, 2, true);
            nibImg.color = DrawnUI.WithAlpha(DrawnUI.Coral, 0.9f);
            nibImg.raycastTarget = false;

            var fieldGo = new GameObject("write", typeof(RectTransform));
            fieldGo.SetActive(false);          // configure before TMP_InputField wakes
            var frt = fieldGo.GetComponent<RectTransform>();
            frt.SetParent(p.Space, false);
            frt.anchorMin = new Vector2(0f, 1f);
            frt.anchorMax = new Vector2(0f, 1f);
            frt.pivot = new Vector2(0f, 1f);
            frt.sizeDelta = new Vector2(w, hgt);
            frt.anchoredPosition = new Vector2(sp.x, -y);

            var hit = fieldGo.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);   // no fill: the paper IS the field
            hit.raycastTarget = true;

            var viewport = DrawnUI.FullRect(frt, "viewport");
            viewport.gameObject.AddComponent<RectMask2D>();
            var textRt = DrawnUI.FullRect(viewport, "text");
            var text = textRt.gameObject.AddComponent<TextMeshProUGUI>();
            if (DrawnUI.Hand != null) text.font = DrawnUI.Hand;
            text.fontSize = JournalPage.SizeBody;
            text.color = DrawnUI.Ink;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.richText = false;
            text.lineSpacing = (pitch - JournalPage.FontHeight(JournalPage.SizeBody)) * 0.5f;

            // ghost handwriting says "this is yours to fill" before the first keystroke
            var phRt = DrawnUI.FullRect(viewport, "placeholder");
            var ph = phRt.gameObject.AddComponent<TextMeshProUGUI>();
            if (DrawnUI.Hand != null) ph.font = DrawnUI.Hand;
            ph.fontSize = JournalPage.SizeBody;
            ph.color = DrawnUI.WithAlpha(DrawnUI.Ink, 0.30f);
            ph.alignment = TextAlignmentOptions.TopLeft;
            ph.textWrappingMode = TextWrappingModes.Normal;
            ph.richText = false;
            ph.text = "";

            var input = fieldGo.AddComponent<TMP_InputField>();
            input.transition = Selectable.Transition.None;
            input.targetGraphic = hit;
            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = ph;
            // ENTER LOCKS THE WEEK. MultiLineSubmit is the one line type where Return
            // raises onSubmit instead of inserting a newline, which is the Godot
            // `KEY_ENTER and not shift` contract without a key handler of our own.
            input.lineType = TMP_InputField.LineType.MultiLineSubmit;
            input.customCaretColor = true;
            input.caretColor = DrawnUI.Pen;
            input.caretWidth = 2;
            input.selectionColor = DrawnUI.WithAlpha(DrawnUI.Pen, 0.22f);
            input.richText = false;
            input.restoreOriginalTextOnEscape = false;
            fieldGo.SetActive(true);
            p.SetInput(input);

            input.onValueChanged.AddListener(t =>
            {
                nibImg.enabled = t.Trim().Length == 0;
                p.RaiseWritten(t);
            });

            // THE FIELD IS INVISIBLE BY DESIGN, which makes it undiscoverable unless it
            // already has focus (owner: "I actually cannot write at all"). So the page
            // hands it the keyboard the moment it opens — or, while the page is still
            // writing itself in, the moment the ruled line arrives, because focus under
            // a half-written page would let typed ink land above the pen.
            if (p.Instant) input.ActivateInputField();
            else
            {
                DrawnUI.Group(frt).alpha = 0f;
                p.RevealHand.Enqueue("field", frt, 0.22f);
            }

            // do not let the trailing gap push past the boundary: it is space AFTER the
            // last element, not space the element needs
            p.SetCursor(zone, Mathf.Min(y + hgt + JournalPage.Gap,
                                        Mathf.Max(p.ZoneBottom(zone), y + hgt)));
            p.MarkWrote(zone);
            p.Overrun(zone);
            return input;
        }
    }
}
