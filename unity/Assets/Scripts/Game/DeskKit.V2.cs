using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// THE DESKKIT v2 PRIMITIVES — the twin of the v2 half of components.gd
    /// (binder rework, DAG2): the ledger family, the didactic zone, the kanban
    /// wall, the ticket/receipt, the ownership instruments and the rung-3
    /// faces. Pixel source: docs/design/mockups/06 (ledger sheet), 07 (ADJUST
    /// column), 03 (quartet), 14 (arrange), 16 (the wall), 18 (ownership).
    /// Same cursor idiom as the first half.
    /// </summary>
    public static partial class DeskKit
    {
        /// The alarm system's red (DECISIONS: alarm-red). Coral stays
        /// money-out; ALERT means act.
        public static readonly Color Alert = DrawnUI.Hex("D93425");
        public static readonly Color Kraft = DrawnUI.Hex("DDBE8C");
        public static readonly Color Kraft2 = DrawnUI.Hex("CBA96F");
        public static readonly Color Paper2 = DrawnUI.Hex("F6F0DE");
        public static readonly Color CardTint = DrawnUI.Hex("EFE6CE");
        public static readonly Color SageBand = new Color(0.561f, 0.647f, 0.51f, 0.14f);
        public static readonly Color KraftBand = new Color(0.796f, 0.663f, 0.435f, 0.22f);

        /// LEDGER GEOMETRY (mockup 06/07): the row-number gutter, the ADJUST
        /// column hosting the two SEPARATE stepper squares, the row pitches.
        public const float LgRowNum = 34f;
        public const float LgAdjust = 92f;
        public const float LgPad = 14f;
        public const float LgRowH = 40f;
        public const float LgHeadH = 34f;
        public const float LgSecH = 30f;
        public const float LgTotH = 48f;
        public const float AdjBtn = 27f;
        public const float AdjGap = 7f;

        // ── THE LEDGER SHEET ───────────────────────────────────────────────

        public sealed class LedgerCol
        {
            public string Label = "";
            public float W = 120f;
            public string Align = "left";   // left | right | center
            public float X;                 // set by LedgerSheet
        }

        public sealed class LedgerBox
        {
            public float X, Y, W, Cursor;
            public List<LedgerCol> Cols = new List<LedgerCol>();
            public int AmountI;
            public bool Adjust;
            public float AdjustX;
            public int RowN;
        }

        public sealed class LedgerRowCfg
        {
            public Color? Col;
            public bool Dim;
            public Action OnMinus;
            public Action OnPlus;
            public bool AtMin;
            public bool AtMax;
            public Action OnPress;
        }

        /// <summary>
        /// ONE ACCOUNTING PRIMITIVE for every money desk (bills/spend/team/the
        /// bank's BOOKS): small-caps header with the unit said once, faint row
        /// numbers, vertical column rules, the amount column on the green
        /// ledger band, single rule above a subtotal, DOUBLE rule above the
        /// total, and the total must equal the hero's number. Rows advance
        /// Cursor; LedgerEnd draws the border and band round what was written.
        /// </summary>
        public static LedgerBox LedgerSheet(BinderScreen b, float x, float y, float w,
                                            IList<LedgerCol> columns, int amount,
                                            bool adjust, string unit)
        {
            var box = new LedgerBox { X = x, Y = y, W = w, Cursor = y + LgHeadH,
                                      AmountI = amount, Adjust = adjust };
            float cx = x + LgPad + LgRowNum;
            for (int i = 0; i < columns.Count; i++)
            {
                LedgerCol c = columns[i];
                c.X = cx;
                if (string.IsNullOrEmpty(c.Align)) c.Align = i == amount ? "right" : "left";
                box.Cols.Add(c);
                cx += c.W;
                if (adjust && i == amount) cx += LgAdjust;
            }
            if (adjust) box.AdjustX = box.Cols[amount].X + box.Cols[amount].W;
            DrawnUI.Fill(b.Content, "lhead", Paper2, x, y, w, LgHeadH).raycastTarget = false;
            for (int i = 0; i < box.Cols.Count; i++)
            {
                LedgerCol c = box.Cols[i];
                TextMeshProUGUI l = b.L((c.Label ?? "").ToUpper(), c.X, y + 8f, 18f,
                                        Ink(0.42f), c.W - 8f);
                if (c.Align == "right") l.alignment = TextAlignmentOptions.TopRight;
            }
            if (adjust)
            {
                TextMeshProUGUI al = b.L("ADJUST", box.AdjustX, y + 8f, 18f, Ink(0.42f),
                                         LgAdjust - 6f);
                al.alignment = TextAlignmentOptions.Top;
            }
            if (!string.IsNullOrEmpty(unit))
            {
                TextMeshProUGUI ul = b.L(unit, x + w - 320f - LgPad, y + 8f, 18f,
                                         Ink(0.42f), 320f);
                ul.alignment = TextAlignmentOptions.TopRight;
            }
            HRule(b, x, y + LgHeadH - 2f, w, DrawnUI.Ink, 2.4f);
            return box;
        }

        /// <summary>One book row. Editable rows pass OnMinus/OnPlus and get the
        /// two SEPARATE squares in the ADJUST column; obligations pass none and
        /// stay bare — the stepper law.</summary>
        public static float LedgerRow(BinderScreen b, LedgerBox sh, IList<string> cells,
                                      LedgerRowCfg cfg = null)
        {
            cfg = cfg ?? new LedgerRowCfg();
            float y = sh.Cursor;
            sh.RowN += 1;
            b.L(sh.RowN.ToString(), sh.X + LgPad, y + 10f, 16f, Ink(0.25f), LgRowNum - 6f);
            for (int i = 0; i < cells.Count && i < sh.Cols.Count; i++)
            {
                LedgerCol c = sh.Cols[i];
                bool isAmount = i == sh.AmountI;
                Color col = isAmount ? (cfg.Dim ? Ink(0.42f) : (cfg.Col ?? DrawnUI.Ink))
                                     : (cfg.Dim || i > 0 ? Ink(0.6f) : DrawnUI.Ink);
                // S6 — book cells receive free text (generated buys lines):
                // one measured line per cell, never a wrap over the rule
                TextMeshProUGUI l = FitLine(b, cells[i] ?? "", c.X, y + 6f,
                                            isAmount ? 22f : 21f, col, c.W - 10f);
                if (c.Align == "right") l.alignment = TextAlignmentOptions.TopRight;
                else if (c.Align == "center") l.alignment = TextAlignmentOptions.Top;
            }
            if (sh.Adjust && (cfg.OnMinus != null || cfg.OnPlus != null))
                AdjustPair(b, sh.AdjustX + (LgAdjust - AdjBtn * 2f - AdjGap) * 0.5f,
                           y + (LgRowH - AdjBtn) * 0.5f - 2f, cfg.OnMinus, cfg.OnPlus,
                           cfg.AtMin, cfg.AtMax);
            if (cfg.OnPress != null)
            {
                Button hit = Word(b, "", sh.X, y, cfg.OnPress, Detail, DrawnUI.Ink, sh.W * 0.5f);
                hit.GetComponent<RectTransform>().sizeDelta = new Vector2(sh.W * 0.5f, LgRowH);
            }
            sh.Cursor = y + LgRowH;
            HRule(b, sh.X, sh.Cursor - 1f, sh.W, Ink(0.12f), 1.6f);
            return sh.Cursor;
        }

        /// <summary>A SECTION row: small caps on a kraft wash.</summary>
        public static float LedgerSection(BinderScreen b, LedgerBox sh, string label)
        {
            float y = sh.Cursor;
            DrawnUI.Fill(b.Content, "lsec", KraftBand, sh.X, y, sh.W, LgSecH).raycastTarget = false;
            b.L((label ?? "").ToUpper(), sh.X + LgPad + LgRowNum, y + 3f, 18f, Ink(0.6f),
                sh.W - LgPad * 2f);
            sh.Cursor = y + LgSecH;
            HRule(b, sh.X, sh.Cursor - 1f, sh.W, Ink(0.12f), 1.6f);
            return sh.Cursor;
        }

        /// <summary>THE ACCOUNTING RULES LAW, half one: the subtotal's single
        /// rule above, the accountant's smaller hand, the effect note.</summary>
        public static float LedgerSubtotal(BinderScreen b, LedgerBox sh, string label,
                                           string amount, string note = "")
        {
            float y = sh.Cursor;
            HRule(b, sh.X, y, sh.W, Ink(0.6f), 2f);
            LedgerCol acol = sh.Cols[sh.AmountI];
            b.L(label, sh.X + LgPad + LgRowNum, y + 7f, 19f, Ink(0.6f),
                Mathf.Max(acol.X - sh.X - LgPad - LgRowNum - 12f, 80f));
            TextMeshProUGUI av = b.L(amount, acol.X, y + 5f, 22f, DrawnUI.Ink, acol.W - 10f);
            av.alignment = TextAlignmentOptions.TopRight;
            if (!string.IsNullOrEmpty(note))
            {
                float nx = acol.X + acol.W + (sh.Adjust ? LgAdjust : 0f) + 10f;
                b.L(note, nx, y + 8f, 18f, Ink(0.6f), sh.X + sh.W - nx - LgPad);
            }
            sh.Cursor = y + LgRowH;
            HRule(b, sh.X, sh.Cursor - 1f, sh.W, Ink(0.12f), 1.6f);
            return sh.Cursor;
        }

        /// <summary>THE ACCOUNTING RULES LAW, half two: the TOTAL under a
        /// DOUBLE rule on the card tint — must equal the hero's number.</summary>
        public static float LedgerTotal(BinderScreen b, LedgerBox sh, string label,
                                        string amount, Color? col = null)
        {
            float y = sh.Cursor;
            HRule(b, sh.X, y, sh.W, DrawnUI.Ink, 2.2f);
            HRule(b, sh.X, y + 4f, sh.W, DrawnUI.Ink, 2.2f);
            DrawnUI.Fill(b.Content, "ltot", CardTint, sh.X, y + 6f, sh.W, LgTotH - 6f)
                .raycastTarget = false;
            b.L((label ?? "").ToUpper(), sh.X + LgPad + LgRowNum, y + 12f, 24f,
                DrawnUI.Ink, sh.W * 0.5f);
            LedgerCol acol = sh.Cols[sh.AmountI];
            TextMeshProUGUI av = b.L(amount, acol.X, y + 8f, 30f, col ?? DrawnUI.Ink,
                                     acol.W - 10f);
            av.alignment = TextAlignmentOptions.TopRight;
            sh.Cursor = y + LgTotH;
            return sh.Cursor;
        }

        /// <summary>The quiet accounting MEMO row under the total.</summary>
        public static float LedgerMemo(BinderScreen b, LedgerBox sh, string label,
                                       string amount = "", string note = "")
        {
            float y = sh.Cursor;
            HRule(b, sh.X, y, sh.W, Ink(0.12f), 1.6f);
            b.L(label, sh.X + LgPad + LgRowNum, y + 8f, 19f, Ink(0.6f), sh.W * 0.4f);
            LedgerCol acol = sh.Cols[sh.AmountI];
            if (!string.IsNullOrEmpty(amount))
            {
                TextMeshProUGUI av = b.L(amount, acol.X, y + 6f, 20f, Ink(0.6f), acol.W - 10f);
                av.alignment = TextAlignmentOptions.TopRight;
            }
            if (!string.IsNullOrEmpty(note))
            {
                float nx = acol.X + acol.W + (sh.Adjust ? LgAdjust : 0f) + 10f;
                b.L(note, nx, y + 8f, 18f, Ink(0.6f), sh.X + sh.W - nx - LgPad);
            }
            sh.Cursor = y + LgRowH;
            return sh.Cursor;
        }

        /// <summary>Close the book: the green band down the amount column, the
        /// vertical column rules, the outer edge. Returns the next page y.</summary>
        public static float LedgerEnd(BinderScreen b, LedgerBox sh)
        {
            float h = sh.Cursor - sh.Y;
            LedgerCol acol = sh.Cols[sh.AmountI];
            var band = DrawnUI.Fill(b.Content, "lband", SageBand, acol.X - 6f,
                                    sh.Y + LgHeadH, acol.W + 2f, h - LgHeadH);
            band.raycastTarget = false;
            band.transform.SetAsFirstSibling();
            for (int i = 1; i < sh.Cols.Count; i++)
                VRule(b, sh.Cols[i].X - 8f, sh.Y + LgHeadH, h - LgHeadH, Ink(0.12f), 1.6f);
            var edge = DrawnUI.Rect(b.Content, "ledge", sh.X, sh.Y, sh.W, h);
            DrawnUI.AddInkEdge(edge, new Vector2(sh.W, h), new DrawnUI.PaperStyle
            {
                ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                StepsPerEdge = 16, Jitter = 0.8f, Thickness = 2.6f, Seed = 31,
            });
            return sh.Y + h + 14f;
        }

        /// <summary>THE STEPPER LAW (owner): two SEPARATE drawn squares — − and
        /// +, each its own wobbly box, a visible gap — never a joined chip.</summary>
        public static void AdjustPair(BinderScreen b, float x, float y, Action onMinus,
                                      Action onPlus, bool atMin = false, bool atMax = false)
        {
            AdjBtnDraw(b, "−", x, y, atMin, onMinus);
            AdjBtnDraw(b, "+", x + AdjBtn + AdjGap, y, atMax, onPlus);
        }

        static void AdjBtnDraw(BinderScreen b, string glyph, float x, float y, bool dead,
                               Action onPress)
        {
            var box = DrawnUI.Fill(b.Content, "adj", Paper2, x, y, AdjBtn, AdjBtn);
            box.raycastTarget = false;
            DrawnUI.Fill(b.Content, "adjsh", new Color(0f, 0f, 0f, 0.2f), x + 2f,
                         y + AdjBtn - 2f, AdjBtn, 2f).raycastTarget = false;
            DrawnUI.AddInkEdge(box.rectTransform, new Vector2(AdjBtn, AdjBtn),
                new DrawnUI.PaperStyle
                {
                    ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                    StepsPerEdge = 5, Jitter = 0.8f, Thickness = 2.4f, Seed = 37,
                });
            Action act = () => { };
            if (!dead && onPress != null)
                act = () => { b.Desk.Remove("armed"); onPress(); b.Refresh(); };
            GameUi.InkWord(b.Content, glyph, x, y - 3f, AdjBtn, AdjBtn, 20f,
                dead ? Ink(0.35f) : DrawnUI.Ink, act);
        }

        // ── THE NUMBERED ZONE ──────────────────────────────────────────────

        /// <summary>THE DIDACTIC SPINE (the bank's Meeting, promoted
        /// binder-wide): a numbered badge, a small-caps title, the zone's
        /// one-line LESSON in the header. Returns a CardBox-shaped frame.</summary>
        public static CardBox Zone(BinderScreen b, float x, float y, float w, float h,
                                   int num, string title, string lesson)
        {
            var edge = DrawnUI.Rect(b.Content, "zone", x, y, w, h);
            DrawnUI.AddInkEdge(edge, new Vector2(w, h), new DrawnUI.PaperStyle
            {
                ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                StepsPerEdge = 14, Jitter = 1.2f, Thickness = 2.6f,
                Seed = 43 + Mathf.Abs((int)x % 7),
            });
            var badge = DrawnUI.Fill(b.Content, "badge", DrawnUI.Yellow, x + 12f, y + 10f,
                                     34f, 34f);
            badge.raycastTarget = false;
            DrawnUI.AddInkEdge(badge.rectTransform, new Vector2(34f, 34f),
                new DrawnUI.PaperStyle
                {
                    ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                    StepsPerEdge = 6, Jitter = 0.8f, Thickness = 2.6f, Seed = 47 + num,
                });
            TextMeshProUGUI n = b.L(num.ToString(), x + 12f, y + 12f, 22f, DrawnUI.Ink, 34f);
            n.alignment = TextAlignmentOptions.Top;
            b.L((title ?? "").ToUpper(), x + 58f, y + 12f, 24f, DrawnUI.Ink, w - 70f);
            if (!string.IsNullOrEmpty(lesson))
                b.L(lesson, x + 58f, y + 44f, Law, Ink(0.6f), w - 70f);
            float cy = y + (string.IsNullOrEmpty(lesson) ? 52f : 78f);
            return new CardBox
            {
                ContentX = x + CardPad, ContentY = cy, Cursor = cy,
                MoneyX = x + w - CardPad, Bottom = y + h, X = x, Y = y, W = w, H = h,
            };
        }

        // ── THE KANBAN WALL ────────────────────────────────────────────────

        public sealed class WallCol
        {
            public float X, Y, W, H, Cursor, ContentX;
        }

        /// <summary>A wall column: kraft header band, the one-line meaning.</summary>
        public static WallCol WallColumn(BinderScreen b, float x, float y, float w, float h,
                                         string head, string sub)
        {
            DrawnUI.Fill(b.Content, "wch", DrawnUI.WithAlpha(Kraft, 0.45f), x, y, w, 54f)
                .raycastTarget = false;
            var edge = DrawnUI.Rect(b.Content, "wcol", x, y, w, h);
            DrawnUI.AddInkEdge(edge, new Vector2(w, h), new DrawnUI.PaperStyle
            {
                ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                StepsPerEdge = 14, Jitter = 1.2f, Thickness = 2.6f,
                Seed = 43 + Mathf.Abs((int)x % 7),
            });
            b.L((head ?? "").ToUpper(), x + 10f, y + 4f, 22f, DrawnUI.Ink, w - 20f);
            if (!string.IsNullOrEmpty(sub))
                b.L(sub, x + 10f, y + 30f, 17f, Ink(0.6f), w - 20f);
            return new WallCol { X = x, Y = y, W = w, H = h, Cursor = y + 62f, ContentX = x + 8f };
        }

        public sealed class WallCardCfg
        {
            public string Title = "";
            public IList<string> Facts = new List<string>();
            public float Progress = -1f;
            public bool Ready;
            public int Sev;
            public Action OnPress;
        }

        /// <summary>One wall card — one anatomy everywhere; the READY variant
        /// wears the alarm red (red means act, and SHIP is an act).</summary>
        public static float WallCard(BinderScreen b, WallCol col, WallCardCfg cfg)
        {
            float x = col.ContentX;
            float y = col.Cursor;
            float w = col.W - 16f;
            float h = 40f + cfg.Facts.Count * 24f + (cfg.Progress >= 0f ? 16f : 0f);
            DrawnUI.Fill(b.Content, "wcsh", new Color(0f, 0f, 0f, 0.16f), x + 4f, y + 5f,
                         w, h).raycastTarget = false;
            var body = DrawnUI.Fill(b.Content, "wcard", cfg.Ready ? Alert : CardTint,
                                    x, y, w, h);
            body.raycastTarget = false;
            DrawnUI.AddInkEdge(body.rectTransform, new Vector2(w, h), new DrawnUI.PaperStyle
            {
                ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 2f,
                StepsPerEdge = 9, Jitter = 1.4f, Thickness = 2.4f,
                Seed = 53 + Mathf.Abs((int)(x + y) % 5),
            });
            if (cfg.Sev > 0) SevDot(b, x + w - SevBox - 4f, y + 6f, cfg.Sev);
            // S6 — wall titles and facts are generated: measured, one line
            FitLine(b, cfg.Title, x + 10f, y + 6f, 22f,
                cfg.Ready ? Color.white : DrawnUI.Ink,
                w - (cfg.Sev > 0 ? SevBox + 22f : 20f));
            float fy = y + 36f;
            for (int i = 0; i < cfg.Facts.Count; i++)
            {
                FitLine(b, cfg.Facts[i], x + 10f, fy, 17f,
                    cfg.Ready ? new Color(1f, 1f, 1f, 0.85f) : Ink(0.65f), w - 20f);
                fy += 24f;
            }
            if (cfg.Progress >= 0f) Meter(b, x + 10f, fy + 2f, w - 20f, cfg.Progress,
                                          DrawnUI.Sage, "");
            if (cfg.OnPress != null)
            {
                Button hit = Word(b, "", x, y, cfg.OnPress, Detail, DrawnUI.Ink, w);
                hit.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
            }
            col.Cursor = y + h + 10f;
            return col.Cursor;
        }

        // ── THE TICKET / RECEIPT ───────────────────────────────────────────

        public sealed class TicketLine
        {
            public string Label = "";
            public string Value = "";
            public Color? Col;
        }

        /// <summary>The priced slip of paper: dashed rules head and foot, the
        /// price line under a DOUBLE rule.</summary>
        public static float Ticket(BinderScreen b, float x, float y, float w, string title,
                                   IList<TicketLine> lines, string totalLabel,
                                   string totalValue, string foot, Color? totalCol = null)
        {
            bool hasTotal = !string.IsNullOrEmpty(totalValue);
            float h = 46f + lines.Count * 32f + (hasTotal ? 44f : 8f)
                      + (string.IsNullOrEmpty(foot) ? 0f : 30f) + 14f;
            var body = DrawnUI.Fill(b.Content, "ticket", DrawnUI.Hex("FBF6E8"), x, y, w, h);
            body.raycastTarget = false;
            DrawnUI.AddInkEdge(body.rectTransform, new Vector2(w, h), new DrawnUI.PaperStyle
            {
                ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                StepsPerEdge = 14, Jitter = 1f, Thickness = 2.6f, Seed = 59,
            });
            DashRule(b, x + 10f, y + 34f, w - 20f);
            // S6 — ticket titles and lines carry generated words: measured
            FitLine(b, (title ?? "").ToUpper(), x + 14f, y + 6f, 20f, Ink(0.6f), w - 28f);
            float ly = y + 44f;
            for (int i = 0; i < lines.Count; i++)
            {
                FitLine(b, lines[i].Label, x + 14f, ly, 21f, Ink(0.85f), w * 0.6f);
                TextMeshProUGUI v = FitLine(b, lines[i].Value, x + 14f, ly, 21f,
                                            lines[i].Col ?? DrawnUI.Ink, w - 28f);
                v.alignment = TextAlignmentOptions.TopRight;
                ly += 32f;
            }
            if (hasTotal)
            {
                HRule(b, x + 10f, ly + 2f, w - 20f, DrawnUI.Ink, 2f);
                HRule(b, x + 10f, ly + 6f, w - 20f, DrawnUI.Ink, 2f);
                b.L(totalLabel ?? "the price", x + 14f, ly + 12f, 22f, DrawnUI.Ink, w * 0.6f);
                TextMeshProUGUI tv = b.L(totalValue, x + 14f, ly + 10f, 26f,
                                         totalCol ?? DrawnUI.Coral, w - 28f);
                tv.alignment = TextAlignmentOptions.TopRight;
                ly += 44f;
            }
            if (!string.IsNullOrEmpty(foot))
            {
                b.L(foot, x + 14f, ly + 2f, 17f, Ink(0.5f), w - 28f);
                ly += 30f;
            }
            DashRule(b, x + 10f, ly + 6f, w - 20f);
            return y + h + 14f;
        }

        // ── OWNERSHIP INSTRUMENTS ──────────────────────────────────────────

        public sealed class CapRow
        {
            public string Label = "";
            public float Pct;
            public Color? Col;
            public string Note = "";
        }

        /// <summary>CAP BARS: the holders as horizontal share bars.</summary>
        public static float CapBars(BinderScreen b, float x, float y, float w,
                                    IList<CapRow> rows)
        {
            float track = w - 260f - 210f;
            for (int i = 0; i < rows.Count; i++)
            {
                CapRow r = rows[i];
                float pct = Mathf.Clamp(r.Pct, 0f, 100f);
                b.L(r.Label, x, y, Detail, DrawnUI.Ink, 250f);
                float bw = Mathf.Max(track * pct / 100f, 8f);
                var bar = DrawnUI.Fill(b.Content, "capbar",
                                       DrawnUI.WithAlpha(r.Col ?? DrawnUI.Sage, 0.6f),
                                       x + 260f, y + 2f, bw, 24f);
                bar.raycastTarget = false;
                DrawnUI.AddInkEdge(bar.rectTransform, new Vector2(bw, 24f),
                    new DrawnUI.PaperStyle
                    {
                        ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                        StepsPerEdge = 6, Jitter = 1f, Thickness = 2.5f, Seed = (int)y,
                    });
                // R9 — at 100% the +16 offset still walked the pct into the note
                // column (track end + 16 lands 6px past its left edge): the label
                // clears the bar's end AND stops short of the note lane.
                bool hasNote = !string.IsNullOrEmpty(r.Note);
                float pctX = Mathf.Min(x + 260f + bw + 16f,
                    hasNote ? x + w - 200f - 84f : x + w - 92f);
                b.L(pct.ToString("0.0") + "%", pctX, y, Detail, Ink(0.85f), 90f);
                if (hasNote) b.L(r.Note, x + w - 200f, y + 2f, 17f, Ink(0.5f), 200f);
                y += 40f;
            }
            return y + 6f;
        }

        public sealed class DilStep
        {
            public string Label = "";
            public float Pct;
            public string Note = "";
        }

        /// <summary>THE DILUTION STORY: the shrinking-bar timeline — % down but
        /// paper value up, the core dilution lesson.</summary>
        public static float DilutionBar(BinderScreen b, float x, float y, float w,
                                        IList<DilStep> steps)
        {
            if (steps.Count == 0) return y;
            float cell = Mathf.Min(w / steps.Count, 190f);
            const float BarH = 120f;
            for (int i = 0; i < steps.Count; i++)
            {
                DilStep d = steps[i];
                float pct = Mathf.Clamp(d.Pct, 0f, 100f);
                float cx = x + i * cell;
                float fillH = BarH * pct / 100f;
                var m = DrawnUI.Fill(b.Content, "dil",
                                     DrawnUI.WithAlpha(i == 0 ? DrawnUI.Sage : DrawnUI.Blue, 0.6f),
                                     cx + 20f, y + BarH - fillH, 46f, fillH);
                m.raycastTarget = false;
                DrawnUI.AddInkEdge(m.rectTransform, new Vector2(46f, fillH),
                    new DrawnUI.PaperStyle
                    {
                        ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                        StepsPerEdge = 8, Jitter = 0.9f, Thickness = 2.5f, Seed = 19 + i,
                    });
                b.L(pct.ToString("0") + "%", cx + 74f, y + BarH - fillH - 4f, 19f,
                    DrawnUI.Ink, cell - 78f);
                b.L(d.Label, cx + 4f, y + BarH + 8f, 17f, Ink(0.75f), cell - 10f);
                if (!string.IsNullOrEmpty(d.Note))
                    b.L(d.Note, cx + 4f, y + BarH + 32f, 15f, Ink(0.5f), cell - 10f);
            }
            return y + BarH + 64f;
        }

        // ── THE RUNG-3 FACES ───────────────────────────────────────────────

        /// <summary>HERO PLATE: the version/name plate.</summary>
        public static float HeroPlate(BinderScreen b, float x, float y, string name,
                                      string version, string note = "")
        {
            const float W = 420f;
            var body = DrawnUI.Fill(b.Content, "plate", Paper2, x, y, W, 78f);
            body.raycastTarget = false;
            DrawnUI.AddInkEdge(body.rectTransform, new Vector2(W, 78f),
                new DrawnUI.PaperStyle
                {
                    ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                    StepsPerEdge = 12, Jitter = 1.2f, Thickness = 2.6f, Seed = 61,
                });
            // S6 — the plate's name and note are generated: measured, one line
            FitLine(b, name, x + 16f, y + 6f, 30f, DrawnUI.Ink, W - 130f);
            if (!string.IsNullOrEmpty(version)) b.L(version, x + W - 110f, y + 10f, 26f,
                                                    DrawnUI.Coral, 100f);
            if (!string.IsNullOrEmpty(note)) FitLine(b, note, x + 16f, y + 46f, 17f,
                                                     Ink(0.6f), W - 32f);
            return y + 92f;
        }

        public sealed class HeroRowCfg
        {
            public string Name = "";
            public string Facts = "";
            public string Value = "";
            public Color? Col;
            public int Sev;
            public Action OnPress;
        }

        /// <summary>HERO ROW (rung-3 read face, DECISIONS default B): one calm
        /// row per product/site/unit; press opens the thing's own page.</summary>
        public static float HeroRow(BinderScreen b, float y, HeroRowCfg cfg)
        {
            float x = XId;
            if (cfg.Sev > 0) SevDot(b, x, y + 10f, cfg.Sev);
            float nx = x + (cfg.Sev > 0 ? SevBox + 10f : 0f);
            b.L(cfg.Name, nx, y, Row, DrawnUI.Ink, 380f);
            b.L(cfg.Facts, x + 420f, y + 4f, Detail, Ink(0.65f), 480f);
            TextMeshProUGUI v = b.L(cfg.Value, x + 900f, y, Row, cfg.Col ?? DrawnUI.Ink, 220f);
            v.alignment = TextAlignmentOptions.TopRight;
            if (cfg.OnPress != null)
            {
                Button hit = Word(b, "", x, y - 4f, cfg.OnPress, Detail, DrawnUI.Ink, 880f);
                hit.GetComponent<RectTransform>().sizeDelta = new Vector2(880f, 44f);
            }
            PenRule(b, y + 44f, x, 1120f, Ink(0.14f), (int)y % 23);
            return y + 58f;
        }

        /// <summary>FOLDER: the kraft folder face with its count, pressable.</summary>
        public static float Folder(BinderScreen b, float x, float y, float w, string title,
                                   string countNote, Action onPress = null)
        {
            const float H = 96f;
            DrawnUI.Fill(b.Content, "foldsh", new Color(0f, 0f, 0f, 0.16f), x + 5f, y + 8f,
                         w, H - 14f).raycastTarget = false;
            var tab = DrawnUI.Fill(b.Content, "foldtab", Kraft2, x + 10f, y, w * 0.3f, 16f);
            tab.raycastTarget = false;
            var body = DrawnUI.Fill(b.Content, "folder", Kraft, x, y + 14f, w, H - 18f);
            body.raycastTarget = false;
            DrawnUI.AddInkEdge(body.rectTransform, new Vector2(w, H - 18f),
                new DrawnUI.PaperStyle
                {
                    ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                    StepsPerEdge = 10, Jitter = 1.4f, Thickness = 2.6f, Seed = 59,
                });
            b.L(title, x + 16f, y + 26f, Row, DrawnUI.Ink, w - 32f);
            if (!string.IsNullOrEmpty(countNote)) b.L(countNote, x + 16f, y + 60f, 17f,
                                                      Ink(0.6f), w - 32f);
            if (onPress != null)
            {
                Button hit = Word(b, "", x, y, onPress, Detail, DrawnUI.Ink, w);
                hit.GetComponent<RectTransform>().sizeDelta = new Vector2(w, H);
            }
            return y + H + 14f;
        }

        // ── CHIPS, BINS & FOLDS ────────────────────────────────────────────

        public sealed class ChipCfg
        {
            public string Text = "";
            public string Kind = "person";   // person | machine | spend
            public bool Selected;
            public Action OnPress;
        }

        /// <summary>A CHIP — the arrange mode's movable element. Returns the x
        /// the next chip may start at (chips flow horizontally). Named
        /// ChipToken here because the board's own Chip class holds the name in
        /// this engine; the GDScript twin keeps `chip`.</summary>
        public static float ChipToken(BinderScreen b, float x, float y, ChipCfg cfg)
        {
            string full = cfg.Kind == "spend" ? "$ " + cfg.Text : cfg.Text;
            float tw = full.Length * 10f + 8f;   // structural stand-in for measure
            float w = tw + 26f;
            DrawnUI.Fill(b.Content, "chipsh", new Color(0f, 0f, 0f, 0.18f), x + 2f, y + 2f,
                         w, 34f).raycastTarget = false;
            var body = DrawnUI.Fill(b.Content, "chip", CardTint, x, y, w, 34f);
            body.raycastTarget = false;
            DrawnUI.AddInkEdge(body.rectTransform, new Vector2(w, 34f),
                new DrawnUI.PaperStyle
                {
                    ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                    StepsPerEdge = 7, Jitter = 0.9f,
                    Thickness = cfg.Selected ? 3f : 2.3f,
                    Seed = 61 + Mathf.Abs((int)x % 11),
                });
            b.L(full, x + 13f, y + 3f, 19f, cfg.Selected ? DrawnUI.Coral : DrawnUI.Ink,
                tw + 8f);
            if (cfg.OnPress != null)
            {
                Button hit = Word(b, "", x, y, cfg.OnPress, Detail, DrawnUI.Ink, w);
                hit.GetComponent<RectTransform>().sizeDelta = new Vector2(w, 34f);
            }
            return x + w + 10f;
        }

        public sealed class BinCfg
        {
            public string Title = "";
            public string Note = "";
            public bool Ghost;
            public bool Closing;
            public Action OnPress;
        }

        /// <summary>THE ARRANGE BIN — a labeled container chips move into (a
        /// site, SHARED/HQ, or the dashed "+ new" ghost).</summary>
        public static CardBox Bin(BinderScreen b, float x, float y, float w, float h,
                                  BinCfg cfg)
        {
            if (!cfg.Ghost)
            {
                var body = DrawnUI.Fill(b.Content, "bin", Paper2, x, y, w, h);
                body.raycastTarget = false;
                DrawnUI.AddInkEdge(body.rectTransform, new Vector2(w, h),
                    new DrawnUI.PaperStyle
                    {
                        ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                        StepsPerEdge = 12, Jitter = 1.1f,
                        Thickness = cfg.Closing ? 3.2f : 2.7f,
                        Seed = 67 + Mathf.Abs((int)x % 13),
                    });
                if (cfg.Closing)
                    DrawnUI.Fill(b.Content, "binring", DrawnUI.WithAlpha(Alert, 0.4f),
                                 x - 3f, y - 3f, w + 6f, 3f).raycastTarget = false;
                b.L((cfg.Title ?? "").ToUpper(), x + 12f, y + 6f, 21f, DrawnUI.Ink, w - 24f);
                if (!string.IsNullOrEmpty(cfg.Note))
                    b.L(cfg.Note, x + 12f, y + 34f, 15f, Ink(0.5f), w - 24f);
            }
            else
            {
                DashRule(b, x, y, w);
                DashRule(b, x, y + h, w);
                TextMeshProUGUI g = b.L("+ new\n(a priced door)", x + 8f, y + h * 0.5f - 30f,
                                        19f, Ink(0.6f), w - 16f);
                g.alignment = TextAlignmentOptions.Top;
            }
            if (cfg.OnPress != null)
            {
                Button hit = Word(b, "", x, y, cfg.OnPress, Detail, DrawnUI.Ink, w);
                hit.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
            }
            return new CardBox
            {
                ContentX = x + 12f, ContentY = y + 58f, Cursor = y + 58f,
                MoneyX = x + w - 12f, Bottom = y + h, X = x, Y = y, W = w, H = h,
            };
        }

        /// <summary>THE FOLD ROW — the collapse ladder's honest tail: "the
        /// other N ▸" as a dashed row that opens the crowd.</summary>
        public static float FoldRow(BinderScreen b, float x, float y, int n, string label,
                                    Action onPress = null)
        {
            if (n <= 0) return y;
            DashRule(b, x, y + 20f, 1120f * 0.35f);
            // S6 — the label half is generated (counts + a lane's words): measured
            string text = FitText(b, "the other " + n + " " + label, 384f, Detail);
            if (onPress != null)
                Word(b, text + "  ->", x + 1120f * 0.36f, y - 2f, onPress, Detail,
                     Ink(0.6f), 420f);
            else
                FitLine(b, text, x + 1120f * 0.36f, y + 4f, Detail, Ink(0.5f), 420f);
            DashRule(b, x + 1120f * 0.36f + 440f, y + 20f, 1120f - (1120f * 0.36f + 440f));
            return y + 44f;
        }

        /// <summary>THE DEADLINE CLOCK CHIP — alert-red, white words. Returns
        /// end x.</summary>
        public static float ClockChip(BinderScreen b, float x, float y, string text)
        {
            float tw = text.Length * 8f + 8f;
            float w = tw + 20f;
            var body = DrawnUI.Fill(b.Content, "clockchip", Alert, x, y, w, 28f);
            body.raycastTarget = false;
            DrawnUI.AddInkEdge(body.rectTransform, new Vector2(w, 28f),
                new DrawnUI.PaperStyle
                {
                    ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                    StepsPerEdge = 6, Jitter = 0.7f, Thickness = 2.2f, Seed = 71,
                });
            b.L(text, x + 10f, y + 1f, 17f, Color.white, tw + 8f);
            return x + w + 8f;
        }

        // ── the desk-stub furniture ────────────────────────────────────────

        /// <summary>THE QUESTION LINE (DAG2 W1): rides the sheet's quiet
        /// bottom-right corner on pages that embed a shipped desk.
        /// R7/R9 — duplicate captions die: a desk whose teaching foot already
        /// says the question declares b.Desk["foot_carries_question"] = true
        /// and this yields.</summary>
        public static void HeroQuestion(BinderScreen b, string q)
        {
            object fc;
            if (b.Desk.TryGetValue("foot_carries_question", out fc) && fc is bool
                && (bool)fc) return;
            TextMeshProUGUI l = b.L(q, 560f, 846f, Law, Ink(0.4f), 560f);
            l.alignment = TextAlignmentOptions.TopRight;
        }

        /// <summary>A page that exists but is not built yet: the hero question
        /// AS the hero and an honest pen note — never a blank sheet.</summary>
        public static float UnderConstruction(BinderScreen b, string big, string question,
                                              string note)
        {
            float y = HeroBand(b, big, question, DrawnUI.Ink, 6f, false);
            y += 8f;
            b.L("· " + note, XId + 20f, y, Status, Ink(0.6f), 1060f);
            y += 60f;
            b.L("this desk is on the drafting table — its numbers land with the next wave",
                XId + 20f, y, Law, Ink(0.4f), 1060f);
            return y + 40f;
        }

        // ═══════════ THE UX SPINE PRIMITIVES (13-binder-ux, DAG3) ══════════
        // The nine systems' kit half — the twin of components.gd's DAG3
        // section: the zero state (S1), the ask strip (S2a), the DO lane
        // (S3), the receipt popover press map (S4), the delta layer (S5), the
        // measure law (S6), the arm family (S9) and the suggestion probe.

        /// THE DO LANE'S ONE ANCHOR (S3): bottom-right of the pane, above the
        /// money desks' teaching foot (806).
        public const float DoLaneY = 762f;

        /// THE SLOT GRID (14-quiet R5): fixed vertical slots, identical on
        /// every desk. hero 6-96 · ask strip 96-118 (drawn only when red;
        /// content NEVER creeps into the slot when it is empty) · content 126
        /// to DoLaneY-8 · the DO lane · the teaching foot. Deep stacks fold
        /// (FoldRow) rather than slide the foot.
        public const float HeroY0 = 6f;
        public const float HeroY1 = 96f;
        public const float StripY = 96f;
        public const float StripH = 22f;
        public const float ContentY0 = 126f;
        public const float FootY = 806f;

        /// THE CHIP SIZE (14-quiet R8): the smallest sanctioned print — tier
        /// notes, micro-statuses, badge counts. Strays at 11-15px snap here or
        /// to Detail, nothing in between.
        public const float ChipS = 17f;

        /// THE ANNOTATION BUDGET (14-quiet R2): beyond the tab's own red, a
        /// pane carries at most THREE attention annotations.
        public const int MarkBudget = 3;

        // ── R2 · THE ARBITER (14-quiet, the quiet law) ─────────────────────

        public sealed class Mark
        {
            public string Kind = "";      // strip | hero_delta | row_dot
            public float Priority;
            public string Line = "";      // strip
            public float X, Y, W;         // strip / hero_delta
            public bool Up;               // hero_delta
            public Rect RowRect;          // row_dot
            public bool Bad;              // row_dot
        }

        /// <summary>MARKS REGISTER, THE KIT RENDERS: a desk never draws an
        /// attention annotation directly — it registers and at end-of-draw the
        /// binder renders the top THREE by priority, dropping the rest. kind ∈
        /// {strip (100, always wins its slot), hero_delta (50, only ONE per
        /// pane — R4), row_dot (by |change| or caller priority)}.</summary>
        public static void NoteMark(BinderScreen b, Mark m)
        {
            b.Marks.Add(m);
        }

        /// <summary>The binder calls this once, after the desk's draw returns.</summary>
        public static void RenderMarks(BinderScreen b)
        {
            var rows = new List<Mark>(b.Marks);
            b.Marks.Clear();
            rows.Sort((a, c) => c.Priority.CompareTo(a.Priority));
            int kept = 0, strips = 0, heroes = 0;
            foreach (Mark m in rows)
            {
                if (kept >= MarkBudget) break;
                switch (m.Kind)
                {
                    case "strip":
                        if (strips >= 1) continue;
                        strips++;
                        DrawStrip(b, m);
                        break;
                    case "hero_delta":
                        if (heroes >= 1) continue;   // R4 — ONE DELTA PER PANE
                        heroes++;
                        DrawDelta(b, m);
                        break;
                    case "row_dot":
                        DrawRowDot(b, m);
                        break;
                    default:
                        continue;
                }
                kept++;
            }
        }

        /// The strip's drawing half (R6 — THE RED SINGLETON: the one red line).
        static void DrawStrip(BinderScreen b, Mark m)
        {
            TextMeshProUGUI l = FitLine(b, m.Line, m.X, StripY, Detail, Alert, m.W);
            l.gameObject.name = "quietmark_strip";
        }

        /// The delta's drawing half — the sage/coral triangle beside the hero.
        static void DrawDelta(BinderScreen b, Mark m)
        {
            var img = DrawnUI.Fill(b.Content, "quietmark_delta",
                m.Up ? DrawnUI.Sage : DrawnUI.Coral, m.X, m.Y, 16f, 14f);
            img.sprite = TriSprite(m.Up ? 0 : 1, 16, 14);
            img.raycastTarget = false;
        }

        /// THE GUTTER DOT's drawing half (R3): a 6px filled dot in the LEFT
        /// GUTTER of the row's rect (x = rect.x − 14, centered vertically) —
        /// sage when the change is good/neutral, coral when the caller says
        /// bad. Never an ellipse round content (the circle fought the row's
        /// own borders and doubled the ink; the spotlight/tour keeps its own).
        static void DrawRowDot(BinderScreen b, Mark m)
        {
            var dot = DrawnUI.Fill(b.Content, "quietmark_dot",
                m.Bad ? DrawnUI.Coral : DrawnUI.Sage,
                m.RowRect.x - 14f, m.RowRect.y + m.RowRect.height * 0.5f - 3f, 6f, 6f);
            dot.sprite = DrawnUI.DiscSprite(3f, 1);
            dot.raycastTarget = false;
        }

        /// <summary>R6/R10 AUDIT — annotations the arbiter rendered this draw
        /// (tagged quietmark_*), so both engines' gates count the same way.</summary>
        public static int AnnotationCount(BinderScreen b)
        {
            int c = 0;
            foreach (Transform t in b.Content.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("quietmark_", StringComparison.Ordinal)) c++;
            return c;
        }

        /// <summary>ALERT-colored TEXT objects per pane. Row clock chips are
        /// exempt by construction — they are drawn chips (a red polygon under
        /// WHITE words), not text labels colored ALERT.</summary>
        public static int AlertTextCount(BinderScreen b)
        {
            int c = 0;
            foreach (TextMeshProUGUI t in
                     b.Content.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (string.IsNullOrEmpty(t.text) || t.text.Trim().Length == 0) continue;
                Color col = t.color;
                if (Mathf.Abs(col.r - Alert.r) < 0.01f && Mathf.Abs(col.g - Alert.g) < 0.01f
                    && Mathf.Abs(col.b - Alert.b) < 0.01f) c++;
            }
            return c;
        }

        /// <summary>THE TIER SAID ON THE CONTROL (S9): the three confirm
        /// grammars keep their mechanics and learn to introduce themselves.</summary>
        public static string TierWord(string tier)
        {
            switch (tier)
            {
                case "two-tap": return "two-tap";
                case "sign": return "sign for it";
                case "type": return "type the word";
            }
            return "";
        }

        /// <summary>THE ARM FAMILY'S ONE BODY (S9): a paper capsule behind the
        /// words — the ADJUST square's grammar at word scale — with the tier
        /// in small print inside. An armed one trades its ink edge for the
        /// pen.</summary>
        public static Button PaperWord(BinderScreen b, string text, string note,
                                       float x, float y, float size, Color col,
                                       float w, bool armed = false,
                                       Action onPress = null, bool disarms = true)
        {
            float tw = DrawnUI.MeasureWidth(text ?? "", size);
            // R8 — the tier note prints at the chip size, never an 11-15px stray
            float nw = string.IsNullOrEmpty(note) ? 0f : DrawnUI.MeasureWidth(note, ChipS) + 10f;
            float bw = tw + nw + 22f;
            float bh = Mathf.Min(size + 20f, 44f);
            var box = DrawnUI.Rect(b.Content, "paperbtn", x - 8f, y + 2f, bw, bh);
            DrawnUI.Fill(box, "pbsh", new Color(0f, 0f, 0f, 0.16f), 2f, 3f, bw, bh)
                .raycastTarget = false;
            DrawnUI.Fill(box, "pbody", Paper2, 0f, 0f, bw, bh).raycastTarget = false;
            var edge = DrawnUI.Fill(box, "pbedge", armed ? DrawnUI.Coral : DrawnUI.Ink,
                                    -3f, -3f, bw + 6f, bh + 6f);
            edge.sprite = DrawnUI.WobbleRectSprite(Mathf.Max((int)bw, 4), Mathf.Max((int)bh, 4),
                1f, 2.4f, 8, 0.9f, 73 + Mathf.Abs((int)x % 7), 3);
            edge.raycastTarget = false;
            if (!string.IsNullOrEmpty(note))
            {
                var n = DrawnUI.HandLabel(b.Content, note, x + tw + 8f,
                    y + Mathf.Max(size * 0.5f - 1f, 8f), ChipS, Ink(0.45f), nw + 6f);
                n.raycastTarget = false;
            }
            return Word(b, text, x, y, onPress, size, col, w, disarms);
        }

        // ── S1 · the zero state ────────────────────────────────────────────

        public sealed class ZeroStateCfg
        {
            public string WillShow = "";
            public string WouldLine = "";
            public string ActionLabel = "";
            public Action ActionCb;
            public string WakesHint = "";
        }

        /// <summary>NO DESK OPENS ON BARE FURNITURE (S1): the empty desk is a
        /// TEACHING state — what the page WILL show (display type), what one
        /// unit WOULD earn (dim hand ink, honest subjunctive), the ONE action
        /// available now, and when the desk comes alive.</summary>
        public static void ZeroState(BinderScreen b, ZeroStateCfg cfg)
        {
            const float W = 860f;
            float x = XId + (1120f - W) * 0.5f;
            float y = 226f;
            if (!string.IsNullOrEmpty(cfg.WillShow))
            {
                var l = DrawnUI.DisplayLabel(b.Content, cfg.WillShow, x, y, 40f,
                    DrawnUI.Ink, W, TextAlignmentOptions.Top);
                l.raycastTarget = false;
                y += Mathf.Max(BinderScreen.Height(l), 54f) + 24f;
            }
            if (!string.IsNullOrEmpty(cfg.WouldLine))
            {
                var l2 = b.L(cfg.WouldLine, x, y, Row, Ink(0.55f), W);
                l2.alignment = TextAlignmentOptions.Top;
                y += Mathf.Max(BinderScreen.Height(l2), 34f) + 38f;
            }
            if (!string.IsNullOrEmpty(cfg.ActionLabel))
            {
                float tw = DrawnUI.MeasureWidth(cfg.ActionLabel, Row);
                float bx = XId + (1120f - tw) * 0.5f;
                PaperWord(b, cfg.ActionLabel, "", bx, y, Row, DrawnUI.Ink, tw + 40f,
                          false, cfg.ActionCb);
                b.MarkControl("zero_action", new Rect(bx - 8f, y, tw + 30f, 48f));
            }
            if (!string.IsNullOrEmpty(cfg.WakesHint))
            {
                var l3 = b.L(cfg.WakesHint, x, 806f, Law, Ink(0.45f), W);
                l3.alignment = TextAlignmentOptions.Top;
            }
        }

        // ── S2a · the ask strip + its data ─────────────────────────────────

        /// <summary>The data half, shared with the quartet cards: this desk's
        /// attention labels, old desk words aliased on BOTH sides.</summary>
        public static List<string> GetAsks(GameState state, string deskId)
        {
            string want = BinderScreen.DeskAlias(deskId);
            var outp = new List<string>();
            foreach (AttentionItem it in SimEngine.AttentionItems(state))
                if (BinderScreen.DeskAlias(it.Desk) == want) outp.Add(it.Label ?? "");
            return outp;
        }

        /// <summary>RED SPEAKS ON THE PAGE (S2, the spend fix made law): one
        /// measured red line — "!  &lt;asks&gt; — &lt;verb&gt;" — in the STRIP SLOT of
        /// any desk carrying attention. Returns whether it will draw.
        /// R5 — the strip renders AT StripY always; the y argument is
        /// DEPRECATED and ignored (kept so every shipped call site lands
        /// unchanged). Content never re-flows when the strip is absent.
        /// R2 — registers with the arbiter (priority 100: the strip always
        /// wins its slot); rendered at end-of-draw with the pane's marks.</summary>
        public static bool AskStrip(BinderScreen b, string deskId, float x, float yDeprecated,
                                    float w, string verbHint)
        {
            List<string> asks = GetAsks(b.State, deskId);
            if (asks.Count == 0) return false;
            string line = "!  " + string.Join(" · ", asks);
            if (!string.IsNullOrEmpty(verbHint)) line += " — " + verbHint;
            NoteMark(b, new Mark { Kind = "strip", Priority = 100f, Line = line, X = x, W = w });
            return true;
        }

        // ── S3 · the DO lane ───────────────────────────────────────────────

        public sealed class DoAction
        {
            public string Label = "";
            public Action Cb;
            public string Tier = "";   // "" | "two-tap" | "sign" | "type"
        }

        /// <summary>EVERY DESK'S PRIMARY ACTIONS IN ONE SLOT (S3): up to three
        /// paper word-buttons, right-aligned on the DoLaneY anchor, one
        /// grammar "verb — object", each saying its tier. The focused one
        /// wears the pen ring; ENTER presses it, TAB cycles (binder-side).
        /// Controls register as "do_0".."do_2".</summary>
        public static void DoLane(BinderScreen b, IList<DoAction> actions,
                                  float baseY = -1f)
        {
            if (baseY < 0f) baseY = DoLaneY;
            int n = Mathf.Min(actions.Count, 3);
            if (n <= 0) return;
            b.ResetDoLane();
            int focus = b.DoFocus();
            var caps = new List<string>();
            var notes = new List<string>();
            var widths = new List<float>();
            float total = 0f;
            for (int i = 0; i < n; i++)
            {
                DoAction a = actions[i];
                string cap = FitText(b, a.Label ?? "", 330f, Detail);
                object armedObj;
                bool armedNow = b.Desk.TryGetValue("armed", out armedObj) && armedObj != null
                                && armedObj.ToString() == "do_" + i;
                if (a.Tier == "two-tap" && armedNow)
                    cap = FitText(b, (a.Label ?? "") + " — sure?", 360f, Detail);
                string note = TierWord(a.Tier ?? "");
                float tw = DrawnUI.MeasureWidth(cap, Detail);
                float nw = string.IsNullOrEmpty(note) ? 0f
                    : DrawnUI.MeasureWidth(note, ChipS) + 10f;
                caps.Add(cap);
                notes.Add(note);
                widths.Add(tw + nw + 24f);
                total += tw + nw + 24f + (i > 0 ? 14f : 0f);
            }
            float x = XId + 1120f - total;
            for (int i = 0; i < n; i++)
            {
                DoAction a = actions[i];
                string id = "do_" + i;
                object cur;
                bool isArmed = b.Desk.TryGetValue("armed", out cur) && cur != null
                               && cur.ToString() == id;
                float bw = widths[i];
                Action cb = a.Cb;
                string tier = a.Tier ?? "";
                Button btn = null;
                Action press;
                if (tier == "two-tap")
                {
                    string capNow = caps[i];
                    float bxNow = x;
                    press = () =>
                    {
                        object now;
                        bool armedNow2 = b.Desk.TryGetValue("armed", out now) && now != null
                                         && now.ToString() == id;
                        if (armedNow2)
                        {
                            b.Desk.Remove("armed");
                            SignStroke(b, btn, capNow, bxNow, baseY, () =>
                            {
                                if (cb != null) cb();
                                b.Refresh();
                            });
                            return;
                        }
                        b.Desk["armed"] = id;
                        b.Refresh();
                    };
                }
                else if (tier == "sign")
                {
                    string capNow = caps[i];
                    float bxNow = x;
                    press = () =>
                    {
                        b.Desk.Remove("armed");
                        SignStroke(b, btn, capNow, bxNow, baseY, () =>
                        {
                            if (cb != null) cb();
                            b.Refresh();
                        });
                    };
                }
                else
                {
                    // plain — and "type": the press opens the desk's own typed flow
                    press = () =>
                    {
                        b.Desk.Remove("armed");
                        if (cb != null) cb();
                        b.Refresh();
                    };
                }
                btn = PaperWord(b, caps[i], notes[i], x, baseY, Detail,
                    isArmed ? DrawnUI.Coral : DrawnUI.Ink, bw, isArmed, press, false);
                var rect = new Rect(x - 8f, baseY + 2f, bw + 16f, 44f);
                b.MarkControl(id, rect);
                b.RegisterDo(btn);
                if (i == focus)
                {
                    var ring = DrawnUI.Fill(b.Content, "doring",
                        DrawnUI.WithAlpha(DrawnUI.Coral, 0.8f),
                        rect.x - 4f, rect.y - 4f, rect.width + 8f, rect.height + 8f);
                    ring.sprite = DrawnUI.WobbleRectSprite((int)(rect.width + 8f),
                        (int)(rect.height + 8f), 1f, 3f, 9, 1.1f, 79, 3);
                    ring.raycastTarget = false;
                }
                x += bw + 14f;
            }
        }

        // ── S4 · press any number (the receipt press map) ──────────────────

        /// <summary>A PRESSABLE REGION → THE RECEIPT POPOVER: the terms that
        /// made the number, on a small paper card near the press. Dismissed by
        /// any press or Esc; Esc will NOT pop the desk while one is open.</summary>
        public static void PressReceipt(BinderScreen b, Rect rect, string title,
                                        IList<TicketLine> lines)
        {
            Button hit = Word(b, "", rect.x, rect.y, null, Detail, DrawnUI.Ink, rect.width);
            hit.GetComponent<RectTransform>().sizeDelta = new Vector2(rect.width, rect.height);
            string tl = title;
            IList<TicketLine> ls = lines;
            Vector2 at = new Vector2(rect.x, rect.yMax + 8f);
            hit.onClick.AddListener(() => b.Popover(tl, ls, at));
        }

        /// <summary>The control-id overload: register against a rect a desk
        /// marked during this draw.</summary>
        public static void PressReceipt(BinderScreen b, string controlId, string title,
                                        IList<TicketLine> lines)
        {
            if (!b.HasControl(controlId)) return;
            PressReceipt(b, b.ControlRect(controlId), title, lines);
        }

        /// <summary>The convenience: a label that IS its own receipt — drawn
        /// with a subtle underdot marking pressability.</summary>
        public static TextMeshProUGUI ReceiptNumber(BinderScreen b, float x, float y,
                                                    string text, float size, Color col,
                                                    string title, IList<TicketLine> lines)
        {
            TextMeshProUGUI l = b.L(text, x, y, size, col, 520f);
            float tw = DrawnUI.MeasureWidth(text ?? "", size);
            float lh = BinderScreen.LineBox(l.font, size);
            var dot = DrawnUI.Fill(b.Content, "underdot",
                DrawnUI.WithAlpha(DrawnUI.Coral, 0.75f), x + tw * 0.5f - 3f, y + lh - 2f,
                7f, 7f);
            dot.sprite = DrawnUI.DiscSprite(3.5f, 1);
            dot.raycastTarget = false;
            PressReceipt(b, new Rect(x - 4f, y - 2f, tw + 8f, lh + 8f), title, lines);
            return l;
        }

        // ── S5 · the delta layer ───────────────────────────────────────────

        /// <summary>WHAT CHANGED, beside the hero: a small drawn triangle —
        /// sage up, coral down, nothing when equal. Drawn, never typed (the
        /// hand font carries no ▲/▼). R4 — registers with the arbiter
        /// (priority 50); only the FIRST hero_delta renders per pane.</summary>
        public static void DeltaArrow(BinderScreen b, float x, float y, float now,
                                      float prev)
        {
            if (Mathf.Abs(now - prev) < 0.000001f) return;
            NoteMark(b, new Mark
            {
                Kind = "hero_delta", Priority = 50f, X = x, Y = y, Up = now > prev,
            });
        }

        /// <summary>A row that moved since the binder was last opened. R3 —
        /// the rendering is THE GUTTER DOT now, never the drawn ellipse (the
        /// circle fought the row's own borders and doubled the ink; the
        /// spotlight/tour keeps its own ring). `bad` colours the dot coral;
        /// `priority` is |change| when the caller has one — the arbiter keeps
        /// the worst row and drops the rest (R2).</summary>
        public static void PenCircle(BinderScreen b, Rect rect, bool bad = false,
                                     float priority = 10f)
        {
            NoteMark(b, new Mark
            {
                Kind = "row_dot", Priority = priority, RowRect = rect, Bad = bad,
            });
        }

        static readonly Dictionary<string, Sprite> _triSprites =
            new Dictionary<string, Sprite>();

        /// A tiny filled triangle sprite, tinted by the Image that mounts it —
        /// the delta glyph (and the back pill's arrow) the hand font never
        /// carried. dir: 0 = up, 1 = down, 2 = left.
        internal static Sprite TriSprite(int dir, int w, int h)
        {
            string key = "tri|" + dir + "|" + w + "|" + h;
            Sprite cached;
            if (_triSprites.TryGetValue(key, out cached) && cached != null) return cached;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            var on = new Color32(255, 255, 255, 255);
            if (dir == 2)
            {
                // apex at the left edge, full height at the right
                for (int x = 0; x < w; x++)
                {
                    float frac = (float)x / (w - 1);
                    int half = Mathf.RoundToInt(h * 0.5f * frac);
                    int cy = h / 2;
                    for (int y = cy - half; y <= cy + half - 1; y++)
                        if (y >= 0 && y < h) px[y * w + x] = on;
                }
            }
            else
            {
                for (int y = 0; y < h; y++)
                {
                    // texture row 0 is the bottom: an up arrow is widest there
                    float frac = dir == 0 ? 1f - (float)y / (h - 1) : (float)y / (h - 1);
                    int half = Mathf.RoundToInt(w * 0.5f * frac);
                    int cx = w / 2;
                    for (int x = cx - half; x <= cx + half - 1; x++)
                        if (x >= 0 && x < w) px[y * w + x] = on;
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
            _triSprites[key] = sp;
            return sp;
        }

        // ── S6 · the measure law ───────────────────────────────────────────

        /// <summary>GENERATED TEXT NEVER WRAPS BY SURPRISE: one line, measured,
        /// ellipsized at its declared width — TMP NoWrap + Ellipsis + a fixed
        /// rect. The Godot/TMP differences die inside.</summary>
        public static TextMeshProUGUI FitLine(BinderScreen b, string text, float x,
                                              float y, float size, Color col, float w)
        {
            TextMeshProUGUI t = b.L(text, x, y, size, col, w);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.rectTransform.sizeDelta =
                new Vector2(w, BinderScreen.LineBox(t.font, size) + 2f);
            return t;
        }

        /// <summary>The paragraph half: wraps to maxLines, then ellipsizes.</summary>
        public static TextMeshProUGUI FitPar(BinderScreen b, string text, float x,
                                             float y, float size, Color col, float w,
                                             int maxLines)
        {
            TextMeshProUGUI t = b.L(text, x, y, size, col, w);
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.maxVisibleLines = Mathf.Max(maxLines, 1);
            t.rectTransform.sizeDelta = new Vector2(w,
                (BinderScreen.LineBox(t.font, size) + 2f) * Mathf.Max(maxLines, 1));
            return t;
        }

        /// <summary>The string half, for Button captions and baked draws: the
        /// measured trim the spend book proved, promoted kit-wide.</summary>
        public static string FitText(BinderScreen b, string s, float w, float size)
        {
            if (DrawnUI.MeasureWidth(s ?? "", size) <= w) return s ?? "";
            string t = s;
            while (t.Length > 1 && DrawnUI.MeasureWidth(t + "…", size) > w)
                t = t.Substring(0, t.Length - 1);
            return t.TrimEnd() + "…";
        }

        // ── the suggestion interface (S14 — B-LOG's feed) ──────────────────

        /// <summary>DESKS MAY SPEAK UP: a desk exposes
        /// `public static List&lt;Dictionary&lt;string, object&gt;&gt; Suggestions(GameState)`
        /// with rows {label, kind: "prefill"|"jump", payload}; this gathers
        /// them (reflection-probed, absent = quiet) and stamps each row with
        /// its source desk id under "desk".</summary>
        public static List<Dictionary<string, object>> CollectSuggestions(
            GameState state, IList<string> deskIds)
        {
            var outp = new List<Dictionary<string, object>>();
            foreach (string id in deskIds)
            {
                var mi = BinderScreen.DeskStaticMethod(id, "Suggestions");
                if (mi == null) continue;
                object rows = null;
                try { rows = mi.Invoke(null, new object[] { state }); }
                catch (Exception) { continue; }
                var list = rows as System.Collections.IEnumerable;
                if (list == null) continue;
                foreach (object r in list)
                {
                    var d = r as Dictionary<string, object>;
                    if (d == null) continue;
                    var copy = new Dictionary<string, object>(d);
                    copy["desk"] = id;
                    outp.Add(copy);
                }
            }
            return outp;
        }

        /// <summary>THE COUNT BADGE — the binder-bang idiom with a number in
        /// it: the LOCK IN button's outstanding-attention count.</summary>
        public static RectTransform CountBadge(RectTransform parent, float x, float y,
                                               int count)
        {
            var root = DrawnUI.Rect(parent, "countbadge", x, y, 28f, 28f);
            var chip = DrawnUI.Fill(root, "cb", Alert, 0f, 0f, 28f, 28f);
            chip.raycastTarget = false;
            DrawnUI.AddInkEdge(chip.rectTransform, new Vector2(28f, 28f),
                new DrawnUI.PaperStyle
                {
                    ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                    StepsPerEdge = 5, Jitter = 0.6f, Thickness = 2.2f, Seed = 13,
                });
            // R8 — badge counts print at the chip size, never an 11-15px stray
            var t = DrawnUI.DisplayLabel(root, count.ToString(), 0f, 4f, ChipS,
                Color.white, 28f, TextAlignmentOptions.Center);
            t.raycastTarget = false;
            return root;
        }

        // ── the small drawn helpers the v2 primitives compose from ─────────

        static void HRule(BinderScreen b, float x, float y, float w, Color col, float t)
        {
            DrawnUI.Fill(b.Content, "hrule", col, x, y, w, t).raycastTarget = false;
        }

        static void VRule(BinderScreen b, float x, float y, float h, Color col, float t)
        {
            DrawnUI.Fill(b.Content, "vrule", col, x, y, t, h).raycastTarget = false;
        }

        static void DashRule(BinderScreen b, float x, float y, float w)
        {
            for (float dx = 0f; dx < w; dx += 16f)
                DrawnUI.Fill(b.Content, "dash", Ink(0.45f), x + dx, y,
                             Mathf.Min(9f, w - dx), 2f).raycastTarget = false;
        }
    }
}
