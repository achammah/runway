using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// THE DESK KIT — the drawn components every binder desk is built from, and
    /// the twin of game/src/ui/components.gd. Binding source:
    /// docs/design/10-interface-language.md section 2 (the component library) and
    /// section 1 (palette, type scale, paper, motion). Lanes USE these; a lane
    /// that forks one has shipped a second design system.
    ///
    /// Every function takes the BinderScreen as `b` and draws through its public
    /// hand (b.L, b.Content, b.State, b.Desk, b.Refresh), so a desk file never
    /// reaches into the sheet and the whole binder keeps one voice.
    ///
    /// THE CURSOR IDIOM: a component returns the y it ENDED at, measured, never
    /// assumed — pass that y to the next one. Nothing here reads a fixed step for
    /// wrapping text (the street stacked on itself the week a thesis wrapped).
    /// </summary>
    public static class DeskKit
    {
        /// THE TYPE SCALE (section 1.3) — six bands with roles. A size may flex a
        /// step to fit a measured line; a band never skips (a receipt is never
        /// set at ROW).
        public const float HeroSize = 46f;
        public const float TitleSize = 38f;
        public const float Row = 30f;
        public const float Status = 27f;
        public const float Detail = 23f;
        public const float Law = 21f;

        /// THE COLUMN GRAMMAR OF A DESK ROW (section 1.4): identity, then state,
        /// then live effect, then controls. Eyes travel left to right, from what
        /// it is to what you can do.
        public const float XId = 10f;
        public const float XValue = 430f;
        public const float XLever = 520f;
        public const float XEffect = 688f;
        public const float XExpand = 936f;
        public const float XMinus = 1000f;
        public const float XPlus = 1064f;
        public const float BtnW = 52f;
        public const float BtnH = 46f;
        public const float PaneW = 1160f;
        public const float PaneH = 760f;
        public const int ListCap = 6;      // six cards, then "+N more" — nothing scrolls
        public const float FooterY = 700f; // the computed-stats line
        public const float RulesY = 734f;  // the desk-law line, or the warning that outranks it

        /// The keyless path is never a degraded screen — it is the same desk with
        /// a dry footnote (section 2.12).
        public const string HouseNote = "the street shrugged — house numbers";

        static Color Ink(float a) { return DrawnUI.WithAlpha(DrawnUI.Ink, a); }

        // ── the desk's own head ────────────────────────────────────────────────

        /// <summary>The desk's name-line. Returns the y the body may start at.</summary>
        public static float Title(BinderScreen b, string text, float y = 6f)
        {
            b.L(text, XId, y, TitleSize);
            return y + 72f;
        }

        /// <summary>The one number the desk is about, with its name riding along.</summary>
        public static float HeroLine(BinderScreen b, string number, string caption, float y = 6f)
        {
            b.L(number, 100f, y, HeroSize);
            if (caption.Length == 0) return y + 68f;
            b.L(caption, 100f, y + 56f, Row, Ink(0.7f));
            return y + 104f;
        }

        /// <summary>A 2px ink@0.25 rule across the pane — the divider between groups.</summary>
        public static float Rule(BinderScreen b, float y, float x = XId, float w = 1120f)
        {
            DrawnUI.Fill(b.Content, "rule", Ink(0.25f), x, y + 1f, w, 2f).raycastTarget = false;
            return y + 16f;
        }

        // ── 2.1 the world-clamped stepper ──────────────────────────────────────

        /// <summary>One row of a world-clamped stepper. See <see cref="StepRow"/>.</summary>
        public sealed class StepRow
        {
            public string Name = "";
            public string Why = "";
            public string Value = "";
            public string Effect = "";
            public string Bound = "";        // the reason printed at the bound
            public float Pitch = 78f;
            public float XVal = XLever;
            public bool AtMin;
            public bool AtMax;
            public bool Disabled;
            public Action OnMinus;
            public Action OnPlus;
        }

        /// <summary>
        /// THE GAME'S ONLY SLIDER. Set a number the world allows, one deliberate
        /// notch at a time — where a spec says slider, build this (drag has no
        /// pen). A stepper with no live-effect string does not ship: mechanics
        /// visible at the point of decision is house law.
        /// </summary>
        public static float Stepper(BinderScreen b, float y, StepRow s)
        {
            Color body = s.Disabled ? Ink(0.35f) : DrawnUI.Ink;
            b.L((s.Name ?? "").ToUpper(), XId, y, 28f, body);
            if (!string.IsNullOrEmpty(s.Why))
                b.L(s.Why, XId, y + 34f, Law, Ink(s.Disabled ? 0.35f : 0.6f), 480f);
            // THE BOUND PRINTS ITS REASON, and the two ways of saying it never
            // overlap: the note rides the value line while it fits in that column,
            // and drops into the effect column when it does not. (Unfitted,
            // "$100,000  (era cap)" wrote itself straight through "no bank answers
            // a garage".)
            string effect = s.Effect ?? "";
            float valW = XEffect - s.XVal - 8f;
            string valText = s.Value ?? "";
            if (!string.IsNullOrEmpty(s.Bound))
            {
                string joined = valText + "  " + s.Bound;
                if (DrawnUI.MeasureWidth(joined, Row) <= valW) valText = joined;
                else effect = effect.Length == 0 ? s.Bound : s.Bound + " · " + effect;
            }
            b.L(valText, s.XVal, y + 4f, Row, s.Disabled ? Ink(0.35f) : DrawnUI.Coral, valW);
            // WHAT THIS NUMBER IS DOING RIGHT NOW, in the engine's own formula, or
            // — at a bound, disabled, or honestly zero — why it is doing nothing.
            b.L(effect, XEffect, y + 12f, Detail, Ink(s.Disabled ? 0.35f : 0.75f), 300f);
            Glyph(b, "−", XMinus, y, s.Disabled || s.AtMin, s.OnMinus);
            Glyph(b, "+", XPlus, y, s.Disabled || s.AtMax, s.OnPlus);
            return y + s.Pitch;
        }

        /// A dead glyph dims and does nothing — the reason is already printed beside it.
        static void Glyph(BinderScreen b, string text, float x, float y, bool dead, Action onPress)
        {
            if (dead)
            {
                // the dead glyph sits where the live one sat — a Button centres its
                // word in the box, so the label has to as well or the row limps
                DrawnUI.HandLabel(b.Content, text, x, y + 2f, 40f, Ink(0.35f), BtnW,
                                  TextAlignmentOptions.Center);
                return;
            }
            Action fire = () =>
            {
                b.Desk.Remove("armed");   // any other control disarms the armed one
                if (onPress != null) onPress();
                b.Refresh();
            };
            var word = GameUi.InkWord(b.Content, text, x, y, BtnW, BtnH, 40f, DrawnUI.Ink, fire);
            // HOLD TO REPEAT (09's bench spec, kit-owned so every desk gets it):
            // after 0.45s held, the glyph re-fires 5×/s. Refresh rebuilds the
            // pane, so the repeater dies with the button — no ghost press.
            var rep = word.gameObject.AddComponent<GlyphRepeater>();
            rep.Fire = fire;
        }

        /// <summary>The hold-to-repeat engine for a stepper glyph.</summary>
        sealed class GlyphRepeater : MonoBehaviour,
            UnityEngine.EventSystems.IPointerDownHandler,
            UnityEngine.EventSystems.IPointerUpHandler,
            UnityEngine.EventSystems.IPointerExitHandler
        {
            public Action Fire;
            bool _down;
            float _t;

            public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e)
            {
                _down = true;
                _t = 0f;
            }

            public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) { _down = false; }
            public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { _down = false; }

            void Update()
            {
                if (!_down || Fire == null) return;
                _t += Time.unscaledDeltaTime;
                if (_t >= 0.45f)
                {
                    _t -= 0.2f;
                    Fire();
                }
            }
        }

        /// <summary>
        /// THE NAMED LADDER every stepper walks — lever amounts, fair-price
        /// multiples, borrow sizes, salaries, terms. The engine re-clamps on
        /// write; the UI is never trusted.
        /// </summary>
        public static double Ladder(IList<double> steps, double cur, int dir)
        {
            if (steps == null || steps.Count == 0) return cur;
            int idx = 0;
            for (int i = 0; i < steps.Count; i++) if (steps[i] <= cur) idx = i;
            idx = Gd.Clampi(idx + dir, 0, steps.Count - 1);
            return steps[idx];
        }

        public static bool AtMin(IList<double> steps, double cur)
        {
            return steps != null && steps.Count > 0 && cur <= steps[0];
        }

        public static bool AtMax(IList<double> steps, double cur)
        {
            return steps != null && steps.Count > 0 && cur >= steps[steps.Count - 1];
        }

        // ── 2.2 the expand affordance ──────────────────────────────────────────

        /// <summary>
        /// The expand mark: a DRAWN triangle, never a typed glyph. The hand font
        /// carries no geometric shapes at all (checked: U+25B8, U+25B2, U+25CF are
        /// all absent), so a typed one would arrive in somebody else's face —
        /// which is the same bug as a tofu box wearing a disguise. One row
        /// expands, and the expansion REPLACES the list with a full-pane DETAIL.
        /// </summary>
        public static void Expand(BinderScreen b, float x, float y, Action onPress)
        {
            var img = DrawnChart.Mount(b.Content, "expand", Tri(24, DrawnUI.Ink),
                                       x + 14f, y + 11f, 24f, 24f);
            GameUi.InkWord(b.Content, "", x, y, BtnW, BtnH, 40f, DrawnUI.Ink, () =>
            {
                if (onPress != null) onPress();
                b.Refresh();
            });
            img.transform.SetAsLastSibling();
        }

        /// <summary>The way back out of any sub-state, first thing readable (4.1).</summary>
        public static void Back(BinderScreen b, string text, Action onPress,
                                float x = 10f, float y = 6f)
        {
            Word(b, text, x, y, onPress, Status, DrawnUI.Ink, 300f);
        }

        // ── 2.3 THE REVIEW CARD ────────────────────────────────────────────────

        /// <summary>One adjustable group of a review card: lines and their blue sum.</summary>
        public sealed class ReviewGroup
        {
            public string Caption = "";
            public List<StepRow> Lines = new List<StepRow>();
            public string Sum = "";
            public Color SumCol = DrawnUI.Blue;
        }

        /// <summary>A proposal awaiting the founder's pen. See <see cref="Review"/>.</summary>
        public sealed class ReviewCard
        {
            public string Banner = "";
            public List<string> Read = new List<string>();
            public List<ReviewGroup> Groups = new List<ReviewGroup>();
            public string Verdict = "";      // the lesson line, coral
            public string Note = "";         // the keyless provenance footnote
            public string Refused = "";      // the engine declined on confirm
            public string Confirm = "sign it";
            public string Cancel = "tear it up";
            public Action OnConfirm;
            public Action OnCancel;
        }

        /// <summary>
        /// A PROPOSAL AWAITING THE FOUNDER'S PEN — the load-bearing component. The
        /// world (an LLM, or the engine itself) hands over a filled-in paper form;
        /// the founder adjusts the adjustable lines and signs it into the books,
        /// or tears it up. NOTHING AN LLM WROTE EVER ENTERS STATE UNREVIEWED.
        ///
        /// It renders on the same pane, same cursor, same hand as a DETAIL page —
        /// it IS a desk sheet, not a dialog. No scrim, no floating card. What
        /// marks it as pending is the coral banner.
        /// </summary>
        public static float Review(BinderScreen b, ReviewCard c, float y = 6f)
        {
            b.L(c.Banner, XId, y, Status, DrawnUI.Coral, 1100f);
            y += 44f;
            y = Rule(b, y);
            for (int i = 0; i < c.Read.Count; i++)
            {
                TextMeshProUGUI l = b.L(c.Read[i], XId, y, Detail, Ink(0.8f), 1100f);
                y += Mathf.Max(BinderScreen.Height(l), 26f) + 6f;
            }
            y += 8f;
            for (int g = 0; g < c.Groups.Count; g++)
            {
                ReviewGroup grp = c.Groups[g];
                if (!string.IsNullOrEmpty(grp.Caption))
                {
                    b.L(grp.Caption, XId, y, Detail, Ink(0.6f), 900f);
                    y += 32f;
                }
                for (int i = 0; i < grp.Lines.Count; i++)
                {
                    StepRow ln = grp.Lines[i];
                    if (ln.Pitch >= 78f) ln.Pitch = 52f;
                    if (ln.XVal == XLever) ln.XVal = XValue;
                    y = Stepper(b, y, ln);
                }
                if (!string.IsNullOrEmpty(grp.Sum))
                {
                    // THE BLUE LINE DOES THE ARITHMETIC OUT LOUD — the patient accountant
                    TextMeshProUGUI sl = b.L(grp.Sum, XId + 18f, y, Detail, grp.SumCol, 1080f);
                    y += Mathf.Max(BinderScreen.Height(sl), 26f) + 14f;
                }
            }
            if (!string.IsNullOrEmpty(c.Verdict))
            {
                TextMeshProUGUI v = b.L(c.Verdict, XId, y, Status, DrawnUI.Coral, 1100f);
                y += Mathf.Max(BinderScreen.Height(v), 30f) + 10f;
            }
            if (!string.IsNullOrEmpty(c.Refused))
            {
                TextMeshProUGUI r = b.L(c.Refused, XId, y, Status, DrawnUI.Coral, 1100f);
                y += Mathf.Max(BinderScreen.Height(r), 30f) + 10f;
            }
            if (!string.IsNullOrEmpty(c.Note))
            {
                b.L(c.Note, XId, y, Law, Ink(0.5f), 1100f);
                y += 34f;
            }
            y += 10f;
            // confirm first, cancel second and never coral — cancel is safe, not scary
            float cy = y;
            Button confirm = null;
            confirm = Word(b, c.Confirm, XId, cy, () =>
            {
                // THE SIGNATURE BEAT: the stroke draws under the words, THEN the
                // books change
                SignStroke(b, confirm, c.Confirm, XId, cy, () =>
                {
                    if (c.OnConfirm != null) c.OnConfirm();
                    b.Refresh();
                });
            }, Row, DrawnUI.Ink, 420f, false);
            Word(b, c.Cancel, XId + 440f, cy, () =>
            {
                if (c.OnCancel != null) c.OnCancel();
                b.Refresh();
            }, Row, Ink(0.7f), 320f);
            return cy + 56f;
        }

        // ── 2.4 the card grid ──────────────────────────────────────────────────

        /// <summary>One action on a card: a word, or the reason it is not one.</summary>
        public sealed class CardAction
        {
            public string Text = "";
            public string Reason = "";     // printed where the word was, at ink 0.35
            public Action On;
        }

        /// <summary>One card in a grid — applicant, bet, note, machine, term sheet.</summary>
        public sealed class CardRow
        {
            public string Name = "";
            public string Flavor = "";
            public string Dense = "";
            public int Pips = -1;
            public float PipsX;
            public float Pitch = 66f;
            public List<CardAction> Actions = new List<CardAction>();
        }

        /// <summary>
        /// ONE CARD ANATOMY, three densities. Line 1 is the name and the deciding
        /// numbers WITH THEIR ANCHORS (a number without its anchor is not a
        /// decision); line 2 is the world's voice; line 3, on dense cards, is cost
        /// and odds. Actions sit at the stepper columns.
        /// </summary>
        public static float Card(BinderScreen b, float y, CardRow c)
        {
            b.L(c.Name, XId, y, Row, DrawnUI.Ink, 900f);
            if (c.Pips >= 0 && c.PipsX > 0f) Pips(b, c.PipsX, y + 8f, c.Pips);
            if (!string.IsNullOrEmpty(c.Flavor))
                b.L(c.Flavor, XId, y + 34f, Detail, Ink(0.45f), 900f);
            if (!string.IsNullOrEmpty(c.Dense))
                b.L(c.Dense, XId, y + 66f, Detail, Ink(0.65f), 900f);
            float ax = c.Actions.Count > 1 ? 940f : XMinus;
            for (int i = 0; i < c.Actions.Count; i++)
            {
                CardAction a = c.Actions[i];
                if (!string.IsNullOrEmpty(a.Reason))
                    // A CAP THAT BITES SAYS SO where the action was
                    b.L(a.Reason, ax, y + 8f, Detail, Ink(0.35f), 200f);
                else
                    Word(b, a.Text, ax, y, a.On, Status, DrawnUI.Ink, 160f);
                ax += 172f;
            }
            return y + c.Pitch;
        }

        /// <summary>
        /// Five marks, filled coral under an ink edge — never a bare number for a
        /// 1-5 scale. EVERY BOX IS INKED, on and off alike: a bare coral square is
        /// a UI element; a bordered one is a box somebody filled in.
        /// </summary>
        public static void Pips(BinderScreen b, float x, float y, int filled, int total = 5)
        {
            for (int i = 0; i < total; i++)
            {
                bool on = i < filled;
                var fill = DrawnUI.Fill(b.Content, "pip",
                    on ? DrawnUI.WithAlpha(DrawnUI.Coral, 0.85f) : Ink(0.06f),
                    x + i * 21f, y, 17f, 13f);
                fill.raycastTarget = false;
                var edge = DrawnUI.AddInkEdge(fill.rectTransform, new Vector2(17f, 13f),
                    new DrawnUI.PaperStyle
                    {
                        ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                        StepsPerEdge = 5, Jitter = 0.5f, Thickness = on ? 2.5f : 2f, Seed = 7,
                    });
                edge.color = on ? DrawnUI.Ink : Ink(0.32f);
            }
        }

        // ── 2.5 the stage board ────────────────────────────────────────────────

        /// <summary>One two-line chip on a stage board.</summary>
        public sealed class Chip
        {
            public string Name = "";
            public string Facts = "";
            public string Heat = "";      // the heat WORD wears its color (10-interface §1.1)
            public string Note = "";      // the coral clock, when one is running
            public string Flavor = "";
        }

        /// <summary>One named gate of a stage board, and what is sitting in it.</summary>
        public sealed class Column
        {
            public string Head = "";
            public List<Chip> Chips = new List<Chip>();
        }

        /// <summary>
        /// NAMED THINGS MOVING THROUGH NAMED GATES — pen-ruled columns, two-line
        /// chips, and NO CONTROLS AT ALL: the board is the founder's wall
        /// calendar, and hands move deals in the story, not by dragging.
        /// </summary>
        public static float Board(BinderScreen b, float y, IList<Column> columns,
                                  string emptyLine = "")
        {
            if (columns == null || columns.Count == 0)
                return Empty(b, XId, y, emptyLine, "");
            float colW = 1120f / columns.Count;
            int live = 0;
            for (int ci = 0; ci < columns.Count; ci++)
            {
                Column c = columns[ci];
                float cx = XId + ci * colW;
                b.L((c.Head ?? "").ToUpper(), cx, y, 26f, DrawnUI.Ink, colW - 16f);
                if (ci > 0)
                    DrawnUI.Fill(b.Content, "vrule", Ink(0.25f), cx - 12f, y, 2f, 300f)
                           .raycastTarget = false;
                float cy = y + 44f;
                live += c.Chips.Count;
                for (int i = 0; i < c.Chips.Count; i++)
                {
                    Chip ch = c.Chips[i];
                    TextMeshProUGUI nm = b.L(ch.Name, cx, cy, 26f, DrawnUI.Ink, colW - 20f);
                    cy += Mathf.Max(BinderScreen.Height(nm), 30f);
                    if (!string.IsNullOrEmpty(ch.Facts))
                    {
                        b.L(ch.Facts, cx, cy, Detail, Ink(0.7f), colW - 20f);
                        cy += 28f;
                    }
                    if (!string.IsNullOrEmpty(ch.Heat))
                    {
                        // the heat WORD wears its color (10-interface §1.1; 05 §12)
                        b.L(ch.Heat, cx, cy, Detail, HeatCol(ch.Heat), colW - 20f);
                        cy += 28f;
                    }
                    if (!string.IsNullOrEmpty(ch.Note))
                    {
                        b.L(ch.Note, cx, cy, Detail, DrawnUI.Coral, colW - 20f);
                        cy += 28f;
                    }
                    if (c.Chips.Count <= 3 && !string.IsNullOrEmpty(ch.Flavor))
                    {
                        b.L(ch.Flavor, cx, cy, 18f, Ink(0.45f), colW - 20f);
                        cy += 24f;
                    }
                    cy += 10f;
                }
            }
            if (live == 0 && !string.IsNullOrEmpty(emptyLine))
                b.L(emptyLine, XId, y + 60f, Status, Ink(0.6f), 1100f);
            return y + 320f;
        }

        // ── 2.6 the action log ─────────────────────────────────────────────────

        /// <summary>An actor's rap sheet: who they are, how they stand, what they did.</summary>
        public sealed class LogRow
        {
            public string Identity = "";
            public string Posture = "";      // word-maps, never raw floats
            public string Plays = "";
            public List<string> Trail = new List<string>();   // last 3, oldest first
        }

        /// <summary>
        /// A RAP SHEET: who this actor is, how they are standing, and the last
        /// three things they did, each stamped with its week, oldest first — so
        /// the line reads the way time does.
        /// </summary>
        public static float LogBlock(BinderScreen b, float y, LogRow r)
        {
            b.L(r.Identity, XId, y, 32f, DrawnUI.Ink, 1100f);
            y += 44f;
            if (!string.IsNullOrEmpty(r.Posture))
            {
                TextMeshProUGUI p = b.L(r.Posture, 30f, y, Detail, Ink(0.8f), 1070f);
                y += Mathf.Max(BinderScreen.Height(p), 28f) + 4f;
            }
            if (!string.IsNullOrEmpty(r.Plays))
            {
                TextMeshProUGUI pl = b.L(r.Plays, 30f, y, 26f, Ink(0.7f), 1070f);
                y += Mathf.Max(BinderScreen.Height(pl), 30f) + 4f;
            }
            if (r.Trail != null && r.Trail.Count > 0)
            {
                TextMeshProUGUI t = b.L(string.Join("  ·  ", r.Trail.ToArray()), 30f, y,
                                        Detail, Ink(0.7f), 1070f);
                y += Mathf.Max(BinderScreen.Height(t), 28f);
            }
            return y + 18f;
        }

        // ── 2.7 the teaching footer ────────────────────────────────────────────

        /// <summary>
        /// THE DESK STATES ITS OWN LAWS. Blue when it computes from the run's own
        /// numbers; ink 0.5 when it states the standing rules. WARNINGS OUTRANK
        /// WISDOM: when the pane's warning slot fires, the rules line yields. The
        /// computed line never yields — it is content.
        /// </summary>
        public static void Footer(BinderScreen b, string computed, string rules,
                                  string warning, float y = FooterY, float rulesY = RulesY)
        {
            if (!string.IsNullOrEmpty(computed))
                b.L(computed, XId, y, Law, DrawnUI.Blue, 1100f);
            if (!string.IsNullOrEmpty(warning))
            {
                b.L(warning, XId, rulesY, Law, DrawnUI.Coral, 1100f);
                return;
            }
            if (!string.IsNullOrEmpty(rules))
                b.L(rules, XId, rulesY, Law, Ink(0.5f), 1100f);
        }

        // ── 2.9 the two-tap arm ────────────────────────────────────────────────

        /// <summary>
        /// IRREVERSIBLE OR EXPENSIVE ACTS GET A VISIBLE COST AND A SECOND CHANCE —
        /// without a dialog box. The first press re-captions the SAME control in
        /// coral, carrying the price or the consequence; the second fires.
        /// Anything that rebuilds or leaves disarms it, and only one control on a
        /// pane is ever armed.
        ///
        /// Arm iff the act destroys something a later week cannot rebuild, or
        /// books an immediate real cost. Steppers, hires, repayments and
        /// navigation never arm.
        /// </summary>
        public static Button Arm(BinderScreen b, string id, string plain, string armedCaption,
                                 float x, float y, Action onFire, float w = 300f,
                                 float size = Status)
        {
            object cur;
            bool isArmed = b.Desk.TryGetValue("armed", out cur) && cur != null
                           && cur.ToString() == id;
            string caption = isArmed ? armedCaption : plain;
            Button btn = null;
            btn = Word(b, caption, x, y, () =>
            {
                object now;
                bool armedNow = b.Desk.TryGetValue("armed", out now) && now != null
                                && now.ToString() == id;
                if (armedNow)
                {
                    b.Desk.Remove("armed");
                    SignStroke(b, btn, caption, x, y, () =>
                    {
                        if (onFire != null) onFire();
                        b.Refresh();
                    });
                    return;
                }
                b.Desk["armed"] = id;   // arming a second control disarms the first
                b.Refresh();
            }, size, isArmed ? DrawnUI.Coral : DrawnUI.Ink, w, false);
            return btn;
        }

        /// <summary>
        /// THE SIGNATURE BEAT (section 1.6.4): a coral rule draws under the pressed
        /// words in 0.14s, holds 0.10s, and only then does the act fire. The most
        /// consequential click in the game must never feel like a menu.
        /// </summary>
        public static void SignStroke(BinderScreen b, Button btn, string text,
                                      float x, float y, Action onDone)
        {
            var boot = Boot.Instance;
            if (boot == null) { if (onDone != null) onDone(); return; }
            boot.StartCoroutine(StrokeThen(b, text, x, y, onDone));
        }

        static IEnumerator StrokeThen(BinderScreen b, string text, float x, float y,
                                      Action onDone)
        {
            float w = DrawnUI.MeasureWidth(text, Row);
            var rt = DrawnUI.Rect(b.Content, "stroke", x - 4f, y + 36f, w + 12f, 10f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = DrawnUI.WobbleLineSprite(Mathf.RoundToInt(w + 12f), 4f, 24, 1.4f, 23, 4);
            img.color = DrawnUI.Coral;
            img.raycastTarget = false;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
            img.fillAmount = 0f;
            float t = 0f;
            while (t < 0.14f)
            {
                t += Time.unscaledDeltaTime;
                if (img != null) img.fillAmount = Mathf.Clamp01(t / 0.14f);
                yield return null;
            }
            if (img != null) img.fillAmount = 1f;
            yield return new WaitForSecondsRealtime(0.10f);
            if (onDone != null) onDone();
        }

        /// <summary>
        /// A flat word button — the binder's only kind of button. The hitbox pads
        /// to the 44px minimum even when the word is short.
        /// </summary>
        public static Button Word(BinderScreen b, string text, float x, float y, Action onPress,
                                  float size = Status, Color? col = null, float w = 200f,
                                  bool disarms = true)
        {
            return GameUi.InkWord(b.Content, text, x, y, Mathf.Max(w, 160f), 46f, size,
                col ?? DrawnUI.Ink, () =>
                {
                    if (disarms) b.Desk.Remove("armed");
                    if (onPress != null) onPress();
                    if (disarms) b.Refresh();
                }, TextAlignmentOptions.Left);
        }

        // ── 2.10 the drawn instruments ─────────────────────────────────────────

        /// <summary>One bar of a comparison: its name, its value, and its number.</summary>
        public sealed class BarRow
        {
            public string Label = "";
            public float Value;
            public string Text = "";
            public Color Col = DrawnUI.Blue;
        }

        /// <summary>
        /// HORIZONTAL PEN-STROKE BARS: w = 40 + 460 x v/max, a tinted fill under a
        /// seeded ink outline, the label and the value ON the bar row. A chart
        /// without its number is decoration, and decoration does not ship. Bar
        /// maxima are the VISIBLE set's max, never all-time.
        /// </summary>
        public static float Bars(BinderScreen b, float x, float y, IList<BarRow> rows,
                                 float pitch = 52f)
        {
            float hi = 0f;
            for (int i = 0; i < rows.Count; i++) hi = Mathf.Max(hi, rows[i].Value);
            for (int i = 0; i < rows.Count; i++)
            {
                BarRow r = rows[i];
                float w = 40f + 460f * (r.Value / Mathf.Max(hi, 1f));
                b.L((r.Label ?? "").ToUpper(), x, y, Detail, DrawnUI.Ink, 200f);
                var fill = DrawnUI.Fill(b.Content, "bar", DrawnUI.WithAlpha(r.Col, 0.6f),
                                        x + 170f, y + 2f, w, 26f);
                fill.raycastTarget = false;
                DrawnUI.AddInkEdge(fill.rectTransform, new Vector2(w, 26f), new DrawnUI.PaperStyle
                {
                    ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                    StepsPerEdge = 6, Jitter = 1f, Thickness = 2.5f, Seed = 3 + i,
                });
                b.L(r.Text, x + 180f + w, y, Detail, Ink(0.8f), 420f);
                y += pitch;
            }
            return y;
        }

        /// <summary>The spark, with its caption — the existing chart idiom, one call.</summary>
        public static float Spark(BinderScreen b, string key, float x, float y, float w, float h,
                                  Color col, string caption = "")
        {
            if (!string.IsNullOrEmpty(caption))
            {
                b.L(caption, x, y, 24f, Ink(0.6f), 600f);
                y += 32f;
            }
            b.Spark(key, x, y, w, h, col);
            return y + h + 12f;
        }

        /// <summary>
        /// The heat ramp (section 1.1): coral to yell to sage, colouring ONE WORD,
        /// never a line and never a fill.
        /// </summary>
        public static Color HeatCol(string word)
        {
            switch (word)
            {
                case "hot":
                case "healthy":
                case "flush":
                case "warm+":
                    return DrawnUI.Sage;
                case "warm":
                case "steady":
                    return DrawnUI.Yellow;
            }
            return DrawnUI.Coral;
        }

        // ── 2.11 the empty states ──────────────────────────────────────────────

        /// <summary>
        /// A DESK WITH NOTHING STILL TEACHES: the fact, then the tell or the
        /// mechanism that fills it. Never blank space, never "No data" — and the
        /// invitation names the MECHANISM, not a button.
        /// </summary>
        public static float Empty(BinderScreen b, float x, float y, string fact, string tell,
                                  bool pointer = false)
        {
            if (!string.IsNullOrEmpty(fact))
            {
                TextMeshProUGUI f = b.L(fact, x, y, Status, Ink(0.65f), 1100f);
                y += Mathf.Max(BinderScreen.Height(f), 32f) + 6f;
            }
            if (!string.IsNullOrEmpty(tell))
            {
                TextMeshProUGUI t = b.L(tell, x, y, Detail,
                                        pointer ? DrawnUI.Coral : Ink(0.5f), 1100f);
                y += Mathf.Max(BinderScreen.Height(t), 28f);
            }
            return y + 10f;
        }

        /// <summary>Six, then the truth about the rest (the grid math).</summary>
        public static float More(BinderScreen b, float x, float y, int n,
                                 string tail = "wait behind these")
        {
            if (n <= 0) return y;
            b.L(string.Format("+{0} more {1}", n, tail), x, y, Detail, Ink(0.5f), 900f);
            return y + 32f;
        }

        // ── 2.12 waiting & keyless states ──────────────────────────────────────

        /// <summary>
        /// HOW WAITING LOOKS IN A PAPER WORLD: one breathing line and a cancel
        /// word. No spinner, no dots, no progress bar. The subject is always the
        /// fiction — the street, the world, the dice — never "loading" and never
        /// the vendor. Cancel is real: leaving drops the reply on arrival.
        /// </summary>
        public static float Wait(BinderScreen b, float x, float y, string phrase, Action onCancel)
        {
            TextMeshProUGUI l = b.L(phrase, x, y, Status, Ink(0.6f), 700f);
            l.gameObject.AddComponent<DeskBreath>().Target = l;
            Word(b, "cancel", x, y + 44f, onCancel, Detail, Ink(0.7f), 160f);
            return y + 100f;
        }

        // ── the drawn triangle ─────────────────────────────────────────────────

        static readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();

        /// A right-pointing triangle, rasterised once and kept: the expand mark's
        /// ink, drawn rather than typed.
        static Sprite Tri(int side, Color col)
        {
            string key = "tri|" + side + "|" + ColorUtility.ToHtmlStringRGBA(col);
            Sprite cached;
            if (_sprites.TryGetValue(key, out cached) && cached != null) return cached;
            side = Mathf.Max(side, 6);
            var tex = new Texture2D(side, side, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[side * side];
            var clear = new Color32(255, 255, 255, 0);
            Color32 ink = col;
            for (int yy = 0; yy < side; yy++)
            {
                float fy = (yy + 0.5f) / side;
                float span = 1f - Mathf.Abs(fy * 2f - 1f);      // widest at the middle
                for (int xx = 0; xx < side; xx++)
                {
                    float fx = (xx + 0.5f) / side;
                    px[yy * side + xx] = fx <= span ? ink : clear;
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            Sprite sp = Sprite.Create(tex, new UnityEngine.Rect(0f, 0f, side, side),
                                      new Vector2(0.5f, 0.5f), 100f);
            _sprites[key] = sp;
            return sp;
        }
    }

    /// <summary>
    /// THE BREATH, quantized to 12fps (section 1.6.1): a desk's WAIT line pulses
    /// its alpha between 0.45 and 0.75 on the hand's own clock. Nothing pulses
    /// smoothly; smooth is chrome.
    /// </summary>
    public sealed class DeskBreath : MonoBehaviour
    {
        const float BreathFps = 12f;
        public TextMeshProUGUI Target;

        void Update()
        {
            if (Target == null) return;
            float t = Mathf.Floor(Time.unscaledTime * BreathFps) / BreathFps;
            Color c = Target.color;
            c.a = 0.6f + 0.15f * Mathf.Sin(t * 3f);
            Target.color = c;
        }
    }
}
