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
                TextMeshProUGUI l = b.L(cells[i] ?? "", c.X, y + 6f, isAmount ? 22f : 21f,
                                        col, c.W - 10f);
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
            b.L(cfg.Title, x + 10f, y + 6f, 22f, cfg.Ready ? Color.white : DrawnUI.Ink,
                w - (cfg.Sev > 0 ? SevBox + 22f : 20f));
            float fy = y + 36f;
            for (int i = 0; i < cfg.Facts.Count; i++)
            {
                b.L(cfg.Facts[i], x + 10f, fy, 17f,
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
            b.L((title ?? "").ToUpper(), x + 14f, y + 6f, 20f, Ink(0.6f), w - 28f);
            float ly = y + 44f;
            for (int i = 0; i < lines.Count; i++)
            {
                b.L(lines[i].Label, x + 14f, ly, 21f, Ink(0.85f), w * 0.6f);
                TextMeshProUGUI v = b.L(lines[i].Value, x + 14f, ly, 21f,
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
                b.L(pct.ToString("0.0") + "%", x + 260f + bw + 10f, y, Detail,
                    Ink(0.85f), 90f);
                if (!string.IsNullOrEmpty(r.Note)) b.L(r.Note, x + w - 200f, y + 2f, 17f,
                                                       Ink(0.5f), 200f);
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
            b.L(name, x + 16f, y + 6f, 30f, DrawnUI.Ink, W - 130f);
            if (!string.IsNullOrEmpty(version)) b.L(version, x + W - 110f, y + 10f, 26f,
                                                    DrawnUI.Coral, 100f);
            if (!string.IsNullOrEmpty(note)) b.L(note, x + 16f, y + 46f, 17f, Ink(0.6f),
                                                 W - 32f);
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
            string text = "the other " + n + " " + label;
            if (onPress != null)
                Word(b, text + "  →", x + 1120f * 0.36f, y - 2f, onPress, Detail,
                     Ink(0.6f), 420f);
            else
                b.L(text, x + 1120f * 0.36f, y + 4f, Detail, Ink(0.5f), 420f);
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
        /// bottom-right corner on pages that embed a shipped desk.</summary>
        public static void HeroQuestion(BinderScreen b, string q)
        {
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
