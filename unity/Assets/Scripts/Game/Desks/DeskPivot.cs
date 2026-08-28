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
    /// DESK — THE COMPANY · "pivot", the escape hatch (twin of desk_pivot.gd).
    /// W2 lane: L-COMPANY. THE QUESTION: "what survives if we change course?"
    /// Spec: DECISIONS.md § THE PIVOT + 12-binder-rework-2.md § pivot.
    ///
    /// 1 THE TWO DOORS (exact costs on each + ONE line of history per door;
    /// debts survive both, said on both) · 2 THE PREVIEW (a two-column KEEP
    /// (sage) / DIES (red) ledger computed from live state; every number
    /// pressable — its receipt says the source: "31 customers — all Consumer
    /// traction"; the product roll shows its honest RANGE) · 3 THE WEEK
    /// AFTER · 4 THE ARM (the word PIVOT typed + the two-tap; Esc keeps the
    /// company). The armed pivot resolves at the next LOCK IN.
    /// </summary>
    public static class DeskPivot
    {
        public const string Question = "what survives if we change course?";

        static readonly string[] AudDies =
        {
            "customers -> 0 — traction starts over",
            "named deals and leads — dead with their market",
            "channel learning + the content well — drained",
            "the market re-fogs — your beliefs reset",
        };
        const string AudLives = "survives: the product, as built · the team · the cash";
        static readonly string[] ProdDies =
        {
            "customers — a 50–100% roll decides who walks",
            "the version -> v0.1 — every advance dies",
            "bets and platform die · tech debt clears",
            "named deals knock back to the first meeting",
        };
        const string ProdLives = "survives: channel + sales learning · the well · the cash";
        const string DebtsLine = "the debts survive. the bank does not forget.";
        /// One line of history per door — how these choices tend to go.
        const string AudFrame = "audience pivots are rarer — and bloodier: the market resets to zero";
        const string ProdFrame = "the common door — the audience stays while the machine reboots";

        public static string[] HeroSummary(GameState s)
        {
            PivotArmed a = SimPivot.Armed(s);
            if (a != null)
                return new[] { "ARMED",
                    string.Format("the {0} pivot fires at the next LOCK IN", a.Kind) };
            return new[] { "two doors",
                "audience pivot · product pivot — the debts survive both" };
        }

        static string DeskStr(BinderScreen b, string key)
        {
            object v;
            return b.Desk.TryGetValue(key, out v) && v != null ? Convert.ToString(v) : "";
        }

        public static void Draw(BinderScreen b)
        {
            GameState s = b.State;
            PivotArmed a = SimPivot.Armed(s);
            if (a != null)
            {
                DrawArmed(b, s, a);
                return;
            }
            string door = DeskStr(b, "mode");
            string target = DeskStr(b, "chip");

            // HERO
            float y = DeskKit.HeroBand(b, "two doors",
                "the escape hatch — the money survives; what burns depends on the axis",
                DrawnUI.Ink);

            // 1 · THE TWO DOORS — the doors speak for themselves (the lesson
            // line's room now carries each door's one line of history)
            DeskKit.CardBox z1 = DeskKit.Zone(b, DeskKit.XId, y, 1120f, 296f, 1,
                "the two doors", "");
            Door(b, s, z1, 0f, "audience", door, target);
            Door(b, s, z1, 560f, "product", door, target);
            y = z1.Bottom + 12f;

            // 2 · THE PREVIEW — a KEEP / DIES ledger, computed, not asserted;
            // every number pressable, its receipt naming the source (S4)
            DeskKit.CardBox z2 = DeskKit.Zone(b, DeskKit.XId, y, 1120f, 192f, 2,
                "the preview", "");
            float py = z2.Cursor - 8f;
            if (door == "")
                DeskKit.Empty(b, z2.ContentX, py,
                    "pick a door — the preview prices it against the live books.", "");
            else
            {
                PivotPreview pv = SimPivot.Preview(s, door);
                float cx = z2.ContentX;
                DeskKit.FitLine(b, "KEEP", cx, py, 21f, DrawnUI.Sage, 200f);
                // R6 — coral, not the alarm red: a column header is a label, and
                // the pane's one red line is the ask strip
                DeskKit.FitLine(b, "DIES", cx + 560f, py, 21f, DrawnUI.Coral, 200f);
                py += 24f;
                float ky = py;
                foreach (LedgerRow kr in KeepRows(s, door, pv))
                {
                    ColRow(b, cx, ky, kr, DrawnUI.Sage);
                    ky += 24f;
                }
                float dy = py;
                foreach (LedgerRow dr in DiesRows(s, door, pv))
                {
                    ColRow(b, cx + 560f, dy, dr, DrawnUI.Coral);
                    dy += 24f;
                }
                // the debts, once, across the whole ledger — kept, against you
                float by = py + 96f;
                DeskKit.FitLine(b, "the debts stay owed — the bank does not forget",
                    cx, by, 20f, DrawnUI.Coral, 700f);
                TextMeshProUGUI bv = DeskKit.FitLine(b, "$" + GameUi.Money(pv.Debts),
                    z2.MoneyX - 200f, by, 20f, DrawnUI.Coral, 200f);
                bv.alignment = TextAlignmentOptions.TopRight;
                DeskKit.PressReceipt(b, new Rect(z2.MoneyX - 200f, by - 2f, 200f, 24f),
                    "the bank's ledger", new List<DeskKit.TicketLine>
                    {
                        new DeskKit.TicketLine { Label = "owed to the bank",
                            Value = "$" + GameUi.Money(pv.Debts) },
                        new DeskKit.TicketLine { Label = "survives any pivot — audience or product",
                            Value = "" },
                    });
            }
            y = z2.Bottom + 12f;

            // 3 · THE WEEK AFTER — ONE measured line, so nothing ever
            // crosses into zone 4 (LONG-TEXT LAW; the budget stays authored)
            DeskKit.CardBox z3 = DeskKit.Zone(b, DeskKit.XId, y, 1120f, 70f, 3,
                "the week after", "");
            b.L("demand ramps from zero · the DM narrates the pivot week · topics and paintings regenerate",
                z3.ContentX, z3.ContentY - 18f, DeskKit.Detail,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 1070f);
            y = z3.Bottom + 12f;

            // 4 · THE ARM — the typed word, then the two-tap
            DeskKit.CardBox z4 = DeskKit.Zone(b, DeskKit.XId, y, 1120f, 118f, 4,
                "the arm", "type PIVOT, then press twice — Esc keeps the company");
            bool ready = door != "" && (door != "audience" || target != "");
            bool typedOk = DeskStr(b, "typed").Trim().ToUpper() == "PIVOT";
            float ax = z4.ContentX;
            float ay = z4.Cursor - 8f;
            if (!ready)
                b.L("choose the door first"
                    + (door == "audience" && target == "" ? " — and where you are going" : ""),
                    ax, ay, DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f), 700f);
            else
            {
                TypeField(b, ax, ay);
                if (typedOk)
                {
                    string doorNow = door;
                    string targetNow = target;
                    DeskKit.Arm(b, "pivot_fire", "arm the pivot — it fires at LOCK IN",
                        ArmCaption(s, door, target), ax + 460f, ay - 6f, () =>
                        {
                            if (doorNow == "audience") SimPivot.ArmAudience(b.State, targetNow);
                            else SimPivot.ArmProduct(b.State, targetNow);
                        }, 620f);
                }
                else
                    b.L("the word unlocks the arm — deliberate, not accidental",
                        ax + 460f, ay + 2f, DeskKit.Detail,
                        DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f), 620f);
            }

            // R7 — duplicate captions die: both door cards already carry
            // DebtsLine; the foot says it once less.
            DeskKit.Footer(b, "",
                string.Format("pivot #{0} would be this run's — rare, deliberate, dangerous",
                    s.Pivots + 1), "", DeskKit.FooterY, 856f);
        }

        static void Door(BinderScreen b, GameState s, DeskKit.CardBox z, float dx,
                         string kind, string door, string target)
        {
            float x = z.ContentX + dx;
            float y = z.Cursor - 6f;
            bool chosen = door == kind;
            string title = kind == "audience" ? "AUDIENCE PIVOT" : "PRODUCT PIVOT";
            b.L(title + (chosen ? "  · chosen" : ""), x, y, DeskKit.Row,
                chosen ? DrawnUI.Coral : DrawnUI.Ink, 520f);
            string[] dies = kind == "audience" ? AudDies : ProdDies;
            float ly = y + 38f;
            for (int i = 0; i < dies.Length; i++)
            {
                b.L("× " + dies[i], x, ly, DeskKit.Detail,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.8f), 530f);
                ly += 24f;
            }
            b.L(kind == "audience" ? AudLives : ProdLives, x, ly, DeskKit.Detail,
                DrawnUI.Sage, 530f);
            ly += 24f;
            b.L(DebtsLine, x, ly, DeskKit.Detail, DrawnUI.Coral, 530f);
            ly += 24f;
            // one line of history — how this door tends to go
            b.L(kind == "audience" ? AudFrame : ProdFrame, x, ly, DeskKit.Law,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f), 530f);
            ly += 26f;
            if (chosen)
            {
                float cx = x;
                if (kind == "audience")
                {
                    for (int i = 0; i < SimPivot.AUDIENCES.Length; i++)
                    {
                        string who = SimPivot.AUDIENCES[i];
                        if (who == s.BizWho) continue;
                        string w = who;
                        cx = DeskKit.ChipToken(b, cx, ly, new DeskKit.ChipCfg
                        {
                            Text = w, Selected = target == w,
                            OnPress = () => { b.Desk["chip"] = w; },
                        });
                    }
                }
                else
                {
                    cx = DeskKit.ChipToken(b, cx, ly, new DeskKit.ChipCfg
                    {
                        Text = "same craft, reborn", Selected = target == "",
                        OnPress = () => { b.Desk["chip"] = ""; },
                    });
                    for (int i = 0; i < SimPivot.CRAFTS.Length; i++)
                    {
                        string what = SimPivot.CRAFTS[i];
                        if (what == s.BizWhat) continue;
                        string w2 = what;
                        cx = DeskKit.ChipToken(b, cx, ly, new DeskKit.ChipCfg
                        {
                            Text = w2, Selected = target == w2,
                            OnPress = () => { b.Desk["chip"] = w2; },
                        });
                    }
                }
            }
            else
            {
                Button hit = DeskKit.Word(b, "", x - 6f, y - 4f, () =>
                {
                    b.Desk["mode"] = kind;
                    b.Desk.Remove("chip");
                }, DeskKit.Detail, DrawnUI.Ink, 540f);
                hit.GetComponent<RectTransform>().sizeDelta = new Vector2(544f, 240f);
            }
        }

        sealed class LedgerRow
        {
            public string Label = "";
            public string Value = "";
            public string Title = "";
            public List<DeskKit.TicketLine> Lines;
        }

        /// One ledger row inside a KEEP/DIES column: label left, the number
        /// right — and the number wears its receipt (S4) when the row has one.
        static void ColRow(BinderScreen b, float x, float y, LedgerRow r, Color valCol)
        {
            DeskKit.FitLine(b, r.Label, x, y, 20f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.8f), 320f);
            TextMeshProUGUI v = DeskKit.FitLine(b, r.Value, x + 330f, y, 20f,
                valCol, 190f);
            v.alignment = TextAlignmentOptions.TopRight;
            if (r.Lines != null && r.Lines.Count > 0)
                DeskKit.PressReceipt(b, new Rect(x + 330f, y - 2f, 190f, 24f),
                    r.Title, r.Lines);
        }

        static DeskKit.TicketLine Tl(string label, string value)
        {
            return new DeskKit.TicketLine { Label = label, Value = value };
        }

        /// What the chosen door KEEPS, numbers sourced from the live books.
        static List<LedgerRow> KeepRows(GameState s, string door, PivotPreview pv)
        {
            if (door == "audience")
                return new List<LedgerRow>
                {
                    new LedgerRow { Label = "the product, as built", Value = pv.Version,
                        Title = pv.Version + " — the product survives whole",
                        Lines = new List<DeskKit.TicketLine>
                        {
                            Tl("product score", string.Format("{0} of 100", s.Product)),
                            Tl("carried whole through an audience pivot", ""),
                        } },
                    new LedgerRow { Label = "the team",
                        Value = string.Format("{0} people", s.Employees.Count),
                        Title = string.Format("{0} people stay", s.Employees.Count),
                        Lines = new List<DeskKit.TicketLine>
                        {
                            Tl("on payroll", s.Employees.Count.ToString()),
                            Tl("the team survives the market", ""),
                        } },
                    new LedgerRow { Label = "the cash", Value = "$" + GameUi.Money(s.Cash),
                        Title = "the cash survives",
                        Lines = new List<DeskKit.TicketLine>
                        {
                            Tl("cash on hand", "$" + GameUi.Money(s.Cash)),
                            Tl("cash never burns in a pivot", ""),
                        } },
                };
            int well = (int)Math.Round(s.ContentEquity);
            return new List<LedgerRow>
            {
                new LedgerRow { Label = "channel + sales learning", Value = "kept" },
                new LedgerRow { Label = "the content well", Value = "$" + GameUi.Money(well),
                    Title = "the well survives a product pivot",
                    Lines = new List<DeskKit.TicketLine>
                    {
                        Tl("content equity built", "$" + GameUi.Money(well)),
                        Tl("the channel still remembers you", ""),
                    } },
                new LedgerRow { Label = "the cash", Value = "$" + GameUi.Money(s.Cash),
                    Title = "the cash survives",
                    Lines = new List<DeskKit.TicketLine>
                    {
                        Tl("cash on hand", "$" + GameUi.Money(s.Cash)),
                        Tl("cash never burns in a pivot", ""),
                    } },
                new LedgerRow { Label = "tech debt",
                    Value = string.Format("clears −{0}", pv.DebtCleared),
                    Title = "the rebuild pays the debt",
                    Lines = new List<DeskKit.TicketLine>
                    {
                        Tl("tech debt on the books", pv.DebtCleared.ToString()),
                        Tl("a fresh v0.1 owes nobody", ""),
                    } },
            };
        }

        /// What the chosen door KILLS — the numbers say where they came from.
        static List<LedgerRow> DiesRows(GameState s, string door, PivotPreview pv)
        {
            if (door == "audience")
                return new List<LedgerRow>
                {
                    new LedgerRow { Label = "customers walk",
                        Value = string.Format("all {0}", pv.CustomersLost),
                        Title = string.Format("{0} customers — all {1} traction",
                            pv.CustomersLost, s.BizWho),
                        Lines = new List<DeskKit.TicketLine>
                        {
                            Tl("traction on the books", pv.CustomersLost.ToString()),
                            Tl("audience", s.BizWho),
                            Tl("an audience pivot starts traction at zero", ""),
                        } },
                    new LedgerRow { Label = "the content well drains",
                        Value = "$" + GameUi.Money(pv.Well),
                        Title = "the well dies with its market",
                        Lines = new List<DeskKit.TicketLine>
                        {
                            Tl("content equity built", "$" + GameUi.Money(pv.Well)),
                            Tl("drained — the channel forgets you", ""),
                        } },
                    new LedgerRow { Label = "named deals die",
                        Value = pv.DealsDead.ToString(),
                        Title = string.Format("{0} deals — dead with their market",
                            pv.DealsDead),
                        Lines = new List<DeskKit.TicketLine>
                        {
                            Tl("named deals on the board", pv.DealsDead.ToString()),
                            Tl("their buyers live in the old market", ""),
                        } },
                    new LedgerRow { Label = "the market re-fogs", Value = "beliefs reset" },
                };
            return new List<LedgerRow>
            {
                new LedgerRow { Label = "customers — the roll decides",
                    Value = string.Format("50–100% of {0}", pv.CustomersAtRisk),
                    Title = string.Format("{0} customers — all {1} traction",
                        pv.CustomersAtRisk, s.BizWho),
                    Lines = new List<DeskKit.TicketLine>
                    {
                        Tl("traction on the books", pv.CustomersAtRisk.ToString()),
                        Tl("the die is cast at the press, not the preview", ""),
                    } },
                new LedgerRow { Label = "the version",
                    Value = string.Format("{0} -> {1}", pv.VersionFrom, pv.VersionTo),
                    Title = "every advance dies",
                    Lines = new List<DeskKit.TicketLine>
                    {
                        Tl("version today", pv.VersionFrom),
                        Tl("the rebuild starts at", pv.VersionTo),
                    } },
                new LedgerRow { Label = "bets on the wall",
                    Value = pv.BetsDead.ToString(),
                    Title = string.Format("{0} bets die with the build", pv.BetsDead),
                    Lines = new List<DeskKit.TicketLine>
                    {
                        Tl("bets on the wall", pv.BetsDead.ToString()),
                        Tl("platform bets die with the platform", ""),
                    } },
                new LedgerRow { Label = "named deals knock back",
                    Value = pv.DealsKnocked.ToString(),
                    Title = string.Format("{0} deals return to the first meeting",
                        pv.DealsKnocked),
                    Lines = new List<DeskKit.TicketLine>
                    {
                        Tl("named deals on the board", pv.DealsKnocked.ToString()),
                        Tl("they will want to see the new build first", ""),
                    } },
            };
        }

        static List<string[]> PreviewLines(GameState s, string door, out List<Color> cols)
        {
            PivotPreview pv = SimPivot.Preview(s, door);
            var rows = new List<string[]>();
            cols = new List<Color>();
            if (door == "audience")
            {
                rows.Add(new[] { "customers walk", "all " + pv.CustomersLost });
                cols.Add(DrawnUI.Coral);
                rows.Add(new[] { "the content well drains", "$" + GameUi.Money(pv.Well) });
                cols.Add(DrawnUI.Coral);
                rows.Add(new[] { "named deals die", pv.DealsDead.ToString() });
                cols.Add(DrawnUI.Coral);
                rows.Add(new[] { "the debts stay owed", "$" + GameUi.Money(pv.Debts) });
                cols.Add(DrawnUI.Coral);
                return rows;
            }
            rows.Add(new[] { "customers at the roll's mercy",
                string.Format("50–100% of {0}", pv.CustomersAtRisk) });
            cols.Add(DrawnUI.Coral);
            rows.Add(new[] { "the version",
                string.Format("{0} -> {1}", pv.VersionFrom, pv.VersionTo) });
            cols.Add(DrawnUI.Coral);
            rows.Add(new[] { string.Format(
                "bets die on the wall · debt clears · {0} deals knock back",
                pv.DealsKnocked),
                string.Format("{0} bets · −{1} debt", pv.BetsDead, pv.DebtCleared) });
            cols.Add(DrawnUI.Coral);
            rows.Add(new[] { "the debts stay owed", "$" + GameUi.Money(pv.Debts) });
            cols.Add(DrawnUI.Coral);
            return rows;
        }

        static string ArmCaption(GameState s, string door, string target)
        {
            if (door == "audience")
                return string.Format("press again: {0} customers -> 0, the market dies — {1} next",
                    s.Traction, target);
            return string.Format("press again: the roll takes 50–100% of {0} customers",
                s.Traction);
        }

        static void DrawArmed(BinderScreen b, GameState s, PivotArmed a)
        {
            // R6 — the strip below is the pane's one alarm-red line; the
            // hero wears coral heat instead, and its sentence stops repeating
            // the strip's verb (duplicate captions die — R7).
            float y = DeskKit.HeroBand(b, "ARMED",
                string.Format("the {0} pivot fires at the next LOCK IN", a.Kind),
                DrawnUI.Coral);
            // S2 — the armed desk is red: the strip names the ask in its own slot (R5)
            DeskKit.AskStrip(b, "pivot", DeskKit.XId, y, 1120f,
                "disarm below keeps the company");
            List<string[]> rows = PreviewLines(s, a.Kind, out List<Color> cols);
            var lines = new List<DeskKit.TicketLine>();
            for (int i = 0; i < rows.Count; i++)
                lines.Add(new DeskKit.TicketLine { Label = rows[i][0], Value = rows[i][1],
                    Col = DrawnUI.Ink });
            y = DeskKit.Ticket(b, DeskKit.XId, y + 6f, 720f,
                "what fires at LOCK IN" + (a.Target != "" ? " — toward " + a.Target : ""),
                lines, "the price in cash", "$0 — the price is the traction",
                "the DM narrates the week it fires · new topics and paintings follow");
            DeskKit.Word(b, "disarm — keep the company", DeskKit.XId, y + 10f,
                () => SimPivot.Disarm(b.State), DeskKit.Row, DrawnUI.Ink, 520f);
            // S2b — the threats row's jump lands spotlit on the way out
            b.MarkControl("disarm", new Rect(DeskKit.XId - 6f, y + 6f, 532f, 44f));
            DeskKit.Footer(b,
                "armed pivots read as a sev-3 alarm — the tab wears it until this fires",
                DebtsLine, "", 820f, 852f);
        }

        /// <summary>The typed-word field: bare hand-font input, the paper is
        /// the field. Crossing the PIVOT threshold refreshes once so the arm
        /// appears (twin of desk_pivot.gd's _type_field).</summary>
        static void TypeField(BinderScreen b, float x, float y)
        {
            var go = new GameObject("typefield", typeof(RectTransform));
            go.SetActive(false);
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(b.Content, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(300f, 44f);
            rt.anchoredPosition = new Vector2(x, -(y - 6f));
            var hit = go.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            var textRt = DrawnUI.FullRect(rt, "text");
            var text = textRt.gameObject.AddComponent<TextMeshProUGUI>();
            if (DrawnUI.Hand != null) text.font = DrawnUI.Hand;
            text.fontSize = 28f;
            text.color = DrawnUI.Ink;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.richText = false;
            var phRt = DrawnUI.FullRect(rt, "ph");
            var ph = phRt.gameObject.AddComponent<TextMeshProUGUI>();
            if (DrawnUI.Hand != null) ph.font = DrawnUI.Hand;
            ph.fontSize = 28f;
            ph.color = DrawnUI.WithAlpha(DrawnUI.Ink, 0.28f);
            ph.alignment = TextAlignmentOptions.MidlineLeft;
            ph.text = "type PIVOT";
            var field = go.AddComponent<TMP_InputField>();
            field.textViewport = rt;
            field.textComponent = text;
            field.placeholder = ph;
            field.customCaretColor = true;
            field.caretColor = DrawnUI.Coral;
            field.text = DeskStr(b, "typed");
            field.onValueChanged.AddListener(t =>
            {
                bool wasOk = DeskStr(b, "typed").Trim().ToUpper() == "PIVOT";
                b.Desk["typed"] = t;
                bool nowOk = t.Trim().ToUpper() == "PIVOT";
                if (wasOk != nowOk) b.Refresh();
            });
            go.SetActive(true);
            DeskKit.PenRule(b, y + 34f, x, 300f, DrawnUI.WithAlpha(DrawnUI.Coral, 0.6f), 3);
        }

        public static void Handle(BinderScreen b, string id)
        {
            switch (id)
            {
                case "door:audience":
                    b.Desk["mode"] = "audience";
                    b.Desk.Remove("chip");
                    break;
                case "door:product":
                    b.Desk["mode"] = "product";
                    b.Desk.Remove("chip");
                    break;
                case "disarm":
                    SimPivot.Disarm(b.State);
                    break;
            }
        }

        // ── the desk conventions (S8) — the rail reads these ─────────────────

        public static bool IsDormant(GameState _s) { return false; }

        /// The rail's right-aligned word: silence, until the hatch is armed.
        public static string MicroStatus(GameState s)
        {
            return SimPivot.Armed(s) != null ? "ARMED" : "";
        }
    }
}
