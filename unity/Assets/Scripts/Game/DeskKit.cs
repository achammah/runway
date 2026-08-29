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
    public static partial class DeskKit
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

        /// THE REWORK GRAMMAR (docs/design/11-binder-rework.md): hero band, then
        /// 2-4 paper cards, then the teaching foot. Every number below is
        /// kit-owned so a desk spends nothing but a y cursor — per-desk
        /// arithmetic is how nine pages drift apart.
        public const float HeroBig = 56f;      // the hero band's number (the 44-64 band)
        public const float HeroSlot = 120f;    // the instrument slot, caller-drawn
        public const float BandMin = 108f;     // the band is never shorter than its instrument
        public const float AirRow = 18f;       // >=18px between rows (Law 6 — air is a feature)
        public const float AirCard = 24f;      // >=24px between cards
        public const float CardPad = 18f;      // paper edge to content, both sides
        public const float CardTitle = 24f;    // the card's pen title
        public const float CardHead = 52f;     // title band: the first row starts this far down
        public const float CardCtrl = 128f;    // the +/- gutter a card reserves for its rows
        public const float MoneySize = 26f;    // a card row (Law 5's row band)
        public const float MoneyPitch = 44f;   // 26px of type + the 18px of air
        public const float TwoBarH = 30f;      // one pen-stroke bar
        public const float TwoBarGap = 4f;     // between two segment strokes of one bar
        public const float TwoBarPitch = 56f;  // between the two bars
        public const float TwoBarLab = 96f;    // the end-label column, left of the bar
        public const float TwoBarNum = 220f;   // the value text's room, right of the longest bar
        public const float FunnelLab = 120f;   // the stage name, left of the mouth
        public const float FunnelH = 66f;      // one trapezoid
        public const float FunnelGap = 8f;     // between two mouths
        public const float FunnelNarrow = 0.18f;  // each stage keeps 18% less width
        public const float MeterH = 22f;       // the drawn fill (fuse, progress, share)
        public const float Grid2W = 540f;      // a lever cell: 2 x 540 + 40 = the pane's 1120
        public const float Grid2Gap = 40f;
        public const float Grid2H = 120f;
        public const float SevBox = 26f;       // the severity dot's footprint

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
            /// a STATED line — the world's number, no squares to press
            public bool Static;
            /// label column x (a card's content pad; default the pane edge)
            public float X = XId;
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
            b.L((s.Name ?? "").ToUpper(), s.X, y, 28f, body);
            if (!string.IsNullOrEmpty(s.Why))
                b.L(s.Why, s.X, y + 34f, Law, Ink(s.Disabled ? 0.35f : 0.6f), 480f);
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
            // STRICT SINGLE LINE (owner: a wrapped value wrote itself through
            // the line below) — value and effect clip with an ellipsis
            var vl = b.L(valText, s.XVal, y + 4f, Row, s.Disabled ? Ink(0.35f) : DrawnUI.Coral, valW);
            vl.enableWordWrapping = false;
            vl.overflowMode = TextOverflowModes.Ellipsis;
            // WHAT THIS NUMBER IS DOING RIGHT NOW, in the engine's own formula, or
            // — at a bound, disabled, or honestly zero — why it is doing nothing.
            var el = b.L(effect, XEffect, y + 12f, Detail, Ink(s.Disabled ? 0.35f : 0.75f), 300f);
            el.enableWordWrapping = false;
            el.overflowMode = TextOverflowModes.Ellipsis;
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
        public static void Expand(BinderScreen b, float x, float y, Action onPress,
                                  bool open = false)
        {
            var img = DrawnChart.Mount(b.Content, "expand", Tri(24, DrawnUI.Ink),
                                       x + 14f, y + 11f, 24f, 24f);
            // an OPEN row's mark points down — the row is unwrapped in place
            if (open) img.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
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
                    if (ln.Static)
                    {
                        // a STATED line — the world's number, no squares
                        b.L(ln.Name, XId + 18f, y, Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.8f), 360f);
                        b.L(ln.Value, XValue, y, Detail, DrawnUI.Ink, 240f);
                        b.L(ln.Effect, XValue + 250f, y, Detail,
                            DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 400f);
                        y += ln.Pitch > 0f && ln.Pitch < 78f ? ln.Pitch : 34f;
                        continue;
                    }
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
            // confirm first, cancel second and never coral — cancel is safe, not
            // scary. S9: the sign-tier control wears the family capsule and says so.
            float cy = y;
            Button confirm = null;
            confirm = PaperWord(b, c.Confirm, TierWord("sign"), XId, cy, Row,
                DrawnUI.Ink, 420f, false, () =>
            {
                // THE SIGNATURE BEAT: the stroke draws under the words, THEN the
                // books change
                SignStroke(b, confirm, c.Confirm, XId, cy, () =>
                {
                    if (c.OnConfirm != null) c.OnConfirm();
                    b.Refresh();
                });
            }, false);
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
            public string FactsLead = "";  // what sits AHEAD of the heat word
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
            // THE RULES ARE COUNTED FIRST. 2.5 allows "headers alone when the chips
            // make the columns obvious" — and on an empty board there are no chips,
            // so the pen-ruled columns had nothing to separate and drew themselves
            // straight through the authored empty line instead.
            int live = 0;
            for (int ci = 0; ci < columns.Count; ci++) live += columns[ci].Chips.Count;
            bool ruled = live > 0;
            live = 0;
            for (int ci = 0; ci < columns.Count; ci++)
            {
                Column c = columns[ci];
                float cx = XId + ci * colW;
                b.L((c.Head ?? "").ToUpper(), cx, y, 26f, DrawnUI.Ink, colW - 16f);
                if (ci > 0 && ruled)
                    DrawnUI.Fill(b.Content, "vrule", Ink(0.25f), cx - 12f, y, 2f, 300f)
                           .raycastTarget = false;
                float cy = y + 44f;
                live += c.Chips.Count;
                for (int i = 0; i < c.Chips.Count; i++)
                {
                    Chip ch = c.Chips[i];
                    TextMeshProUGUI nm = b.L(ch.Name, cx, cy, 26f, DrawnUI.Ink, colW - 20f);
                    cy += Mathf.Max(BinderScreen.Height(nm), 30f);
                    // THE FACTS LINE IS ONE ROW WITH ONE COLOURED WORD (1.1's heat
                    // ramp, 05 §12): `6 seats · warm · wk 1` colours only "warm". A
                    // chip that folded the heat into Facts printed it in the same
                    // grey as the seat count and the whole point of the board — is
                    // this deal warming or dying — went with it; a chip that gave
                    // heat its own ROW cost the column a third of its height. Three
                    // measured segments, one line.
                    string facts = ch.Facts ?? "";
                    if (!string.IsNullOrEmpty(ch.Heat))
                    {
                        string lead = ch.FactsLead ?? "";
                        if (lead.Length == 0 && facts.Length > 0) { lead = facts; facts = ""; }
                        float fx = cx;
                        if (lead.Length > 0)
                        {
                            TextMeshProUGUI ll = b.L(lead + "  ·  ", fx, cy, Detail, Ink(0.7f),
                                                     colW - 20f);
                            ll.ForceMeshUpdate();
                            fx += ll.textInfo.lineCount > 0
                                ? ll.textBounds.size.x : 0f;
                        }
                        TextMeshProUGUI hl = b.L(ch.Heat, fx, cy, Detail, HeatCol(ch.Heat),
                                                 colW - 20f);
                        if (facts.Length > 0)
                        {
                            hl.ForceMeshUpdate();
                            fx += hl.textBounds.size.x;
                            b.L("  ·  " + facts, fx, cy, Detail, Ink(0.7f), colW - 20f);
                        }
                        cy += 28f;
                    }
                    else if (facts.Length > 0)
                    {
                        b.L(facts, cx, cy, Detail, Ink(0.7f), colW - 20f);
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
            // S9 — ONE ARM FAMILY: the confirm control wears the paper capsule
            // and says its tier in small print, so the player learns the danger
            // scale by shape.
            btn = PaperWord(b, caption, TierWord("two-tap"), x, y, size,
                isArmed ? DrawnUI.Coral : DrawnUI.Ink, w, isArmed, () =>
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
            }, false);
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

        // ══ THE REWORK PRIMITIVES (11-binder-rework) ═══════════════════════════
        // The owner's verdict on the first binder was "confusing, not clear, not
        // beautiful": every row the same size, money hidden inside prose, no
        // paper structure, and nothing DRAWN on the page where the densest
        // numbers live. These eight are the answer, built once for both engines.
        // A desk composes them against a single y cursor and does no arithmetic
        // of its own.

        /// <summary>
        /// A HAND-DRAWN RULE — the wobbled twin of <see cref="Rule"/>, for the
        /// rework's own structure. Rule() keeps its dead-straight 2px line
        /// because eight shipped desks measure themselves against it; a fresh
        /// page rules itself with the pen.
        /// </summary>
        public static float PenRule(BinderScreen b, float y, float x = XId, float w = 1120f,
                                    Color? col = null, int seed = 5)
        {
            DrawnUI.Rule(b.Content, x, y + 3f, w, col ?? Ink(0.25f), 2f, seed, 1.1f, 19);
            return y + 16f;
        }

        /// <summary>
        /// LAW 1 — THE HERO ANSWERS THE TAB'S QUESTION. One big number, one plain
        /// sentence under it, and an optional drawn instrument beside it: the
        /// answer to "how is this doing?" in one second, before the eye reaches a
        /// single card.
        ///
        /// `instrument` reserves the left HeroSlot and nothing else — THE CALLER
        /// DRAWS THE INSTRUMENT (a jar, a meter, a clock, a pie), because only
        /// the desk knows what shape its own idea has. Returns the band's bottom
        /// y: hand it to the first card and the page composes itself.
        /// </summary>
        public static float HeroBand(BinderScreen b, string bigText, string sentence,
                                     Color? col = null, float y = 6f, bool instrument = false)
        {
            float x = XId + (instrument ? HeroSlot : 0f);
            float w = PaneW - x - 40f;
            b.L(bigText, x, y, HeroBig, col ?? DrawnUI.Ink, w);
            float bottom = y + 74f;
            if (!string.IsNullOrEmpty(sentence))
            {
                // ONE PLAIN SENTENCE, in words a tired founder reads without
                // decoding — the number said again in English, never a second
                // one. R5/R7 — the sentence sits at +58 so its ink clears the
                // strip slot (96-118) with the air floor intact.
                TextMeshProUGUI s = b.L(sentence, x, y + 58f, Row, Ink(0.7f), w);
                bottom = y + 58f + Mathf.Max(BinderScreen.Height(s), 34f);
            }
            bottom = Mathf.Max(bottom, y + BandMin);
            PenRule(b, bottom + 10f);
            return bottom + 26f;
        }

        /// <summary>A card's own geometry, so a row inside it does no arithmetic.</summary>
        public sealed class CardBox
        {
            public float ContentX;    // where a label starts
            public float ContentY;    // where the first row sits
            public float Cursor;      // the running y MoneyRow advances
            public float MoneyX;      // where a value ENDS
            public float Bottom;      // where the card ends
            public float X, Y, W, H;
        }

        /// <summary>
        /// LAW 3 — CARDS, NOT LISTS. A wobbled paper card cut by the same scissors
        /// as every other card in the game (the draft-card recipe: shadow
        /// (7,9)@0.18, an ink edge walked in 13 steps with 2.1 of jitter, seeded
        /// by the card's own x so neighbours are visibly hand-cut and never
        /// cloned). The body is the same cream held one shade up to the light,
        /// which is what makes a card read as lying ON the clipboard rather than
        /// being a hole cut in it.
        ///
        /// `controls` reserves the +/- gutter — pass it and the money column stays
        /// put whether or not a given row carries a stepper (Law 2: one column,
        /// always).
        /// </summary>
        /// titleW > 0 caps the title's width (clipped, ellipsis) so a card whose
        /// title band carries a right-aligned control never wears the two overlapped.
        public static CardBox CardFrame(BinderScreen b, float x, float y, float w, float h,
                                           string title, bool controls = false,
                                           float titleW = 0f)
        {
            var root = DrawnUI.Rect(b.Content, "card", x, y, w, h);
            DrawnUI.Fill(root, "shadow", new Color(0f, 0f, 0f, 0.18f), 7f, 9f, w, h);
            // THE PAPER SITS INSIDE THE WOBBLE, not behind it. Godot fills the ink
            // edge's own polygon; Unity's edge is a baked stroke with no interior,
            // so the fill is pulled in past the wobble's inward swing (inset 4.5 +
            // jitter 2.1) and the sliver of bare sheet that leaves outside the
            // stroke is cream on cream — invisible, where four right angles poking
            // through a hand-drawn edge are not.
            const float Body = 7f;
            DrawnUI.Fill(root, "paper", DrawnUI.Cream, Body, Body, w - Body * 2f,
                         h - Body * 2f);
            DrawnUI.Fill(root, "lift", new Color(1f, 1f, 1f, 0.07f), Body, Body,
                         w - Body * 2f, h - Body * 2f);
            DrawnUI.AddInkEdge(root, new Vector2(w, h), new DrawnUI.PaperStyle
            {
                ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 4.5f,
                StepsPerEdge = 13, Jitter = 2.1f, Thickness = 3f,
                Seed = 17 + Mathf.Abs((int)x % 5),
            });
            if (!string.IsNullOrEmpty(title))
            {
                // UPPERCASE IS THE BINDER'S BOLD (1.3) — one hand, and emphasis is
                // size, caps or the pen, never a second font.
                var tl = b.L(title.ToUpper(), x + CardPad, y + 12f, CardTitle, DrawnUI.Ink,
                    titleW > 0f ? titleW : w - CardPad * 2f);
                tl.enableWordWrapping = false;
                tl.overflowMode = TextOverflowModes.Ellipsis;
            }
            float contentY = y + (string.IsNullOrEmpty(title) ? CardPad : CardHead);
            return new CardBox
            {
                ContentX = x + CardPad,
                ContentY = contentY,
                Cursor = contentY,
                MoneyX = x + w - CardPad - (controls ? CardCtrl : 0f),
                Bottom = y + h,
                X = x, Y = y, W = w, H = h,
            };
        }

        /// <summary>
        /// LAW 2 — MONEY LIVES IN COLUMNS, NEVER IN SENTENCES. The label at the
        /// card's left, the value RIGHT-ALIGNED so every dollar on the card ends
        /// on one line, and — when the row is a lever — the +/- glyphs in the
        /// gutter the frame reserved.
        ///
        /// The frame carries the cursor, so a desk writes four rows in four lines
        /// and never adds a pitch by hand. Returns the next y as well, for a
        /// caller that wants to interleave something else.
        /// </summary>
        public static float MoneyRow(BinderScreen b, CardBox f, string label, string value,
                                     Color? col = null, Action onMinus = null,
                                     Action onPlus = null, bool atMin = false,
                                     bool atMax = false)
        {
            float y = f.Cursor;
            b.L(label, f.ContentX, y, MoneySize, Ink(0.85f), f.MoneyX - f.ContentX - 8f);
            TextMeshProUGUI v = b.L(value, f.ContentX, y, MoneySize, col ?? DrawnUI.Ink,
                                    f.MoneyX - f.ContentX);
            v.alignment = TextAlignmentOptions.TopRight;
            if (onMinus != null || onPlus != null)
            {
                Glyph(b, "−", f.MoneyX + 16f, y - 6f, atMin, onMinus);
                Glyph(b, "+", f.MoneyX + 76f, y - 6f, atMax, onPlus);
            }
            f.Cursor = y + MoneyPitch;
            return f.Cursor;
        }

        /// <summary>
        /// LAW 4 — DRAW THE SHAPE OF THE IDEA. In and out is not two sentences, it
        /// is two bars of different length: the SHAPE lands before a single digit
        /// does. Both bars share one scale (the larger total fills the track), and
        /// either may be segmented by lane — the segments are separate pen strokes
        /// with a hair of paper between them, so a reader counts the lanes without
        /// a legend.
        ///
        /// aFracSegments / bFracSegments are the lane MAGNITUDES in one unit; they
        /// sum to that bar's total. A plain unsegmented bar is a one-item array.
        /// </summary>
        public static float TwoBar(BinderScreen b, float x, float y, float w,
                                   string aLabel, string aValText, IList<float> aFracSegments,
                                   string bLabel, string bValText, IList<float> bFracSegments,
                                   Color? aCol = null, Color? bCol = null)
        {
            float ta = SumSegs(aFracSegments);
            float tb = SumSegs(bFracSegments);
            float hi = Mathf.Max(Mathf.Max(ta, tb), 1f);
            float track = Mathf.Max(w - TwoBarLab - TwoBarNum, 120f);
            y = OneBar(b, x, y, track, aLabel, aValText, aFracSegments, ta, hi,
                       aCol ?? DrawnUI.Sage, 3);
            y = OneBar(b, x, y, track, bLabel, bValText, bFracSegments, tb, hi,
                       bCol ?? DrawnUI.Coral, 9);
            return y;
        }

        static float SumSegs(IList<float> vals)
        {
            float t = 0f;
            if (vals != null)
                for (int i = 0; i < vals.Count; i++) t += Mathf.Max(vals[i], 0f);
            return t;
        }

        static float OneBar(BinderScreen b, float x, float y, float track, string lab,
                            string valText, IList<float> segs, float total, float hi,
                            Color col, int seed)
        {
            b.L((lab ?? "").ToUpper(), x, y + 2f, MoneySize, DrawnUI.Ink, TwoBarLab - 8f);
            float full = track * (total / hi);
            float bx = x + TwoBarLab;
            float drawn = 0f;
            var live = new List<float>();
            if (segs != null)
                for (int i = 0; i < segs.Count; i++) if (segs[i] > 0f) live.Add(segs[i]);
            if (live.Count == 0) live.Add(0f);
            for (int i = 0; i < live.Count; i++)
            {
                // EVERY LANE KEEPS A VISIBLE STROKE: a $12 line beside a $1,400 one
                // still has to be countable, so no segment is thinner than the pen.
                float sw = Mathf.Max(full * (live[i] / Mathf.Max(total, 1f)) - TwoBarGap, 6f);
                var fill = DrawnUI.Fill(b.Content, "seg", DrawnUI.WithAlpha(col, 0.6f),
                                        bx + drawn, y, sw, TwoBarH);
                fill.raycastTarget = false;
                DrawnUI.AddInkEdge(fill.rectTransform, new Vector2(sw, TwoBarH),
                    new DrawnUI.PaperStyle
                    {
                        ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                        StepsPerEdge = 6, Jitter = 1f, Thickness = 2.5f, Seed = seed + i * 7,
                    });
                drawn += sw + TwoBarGap;
            }
            b.L(valText, bx + Mathf.Max(drawn, 8f) + 4f, y + 2f, MoneySize, Ink(0.85f),
                TwoBarNum - 12f);
            return y + TwoBarPitch;
        }

        /// <summary>One narrowing mouth of a funnel.</summary>
        public sealed class Stage
        {
            public string Label = "";
            public string ValueText = "";
            public bool Known = true;      // false = the fog, drawn rather than hidden
            public Color? Col;
        }

        /// <summary>
        /// THE FUNNEL IS A FUNNEL — four narrowing pen trapezoids, the number
        /// written inside each mouth. The SHAPE is the lesson before any figure
        /// lands, and a stage the company has not earned the eyesight to see keeps
        /// its mouth and loses its number: Known = false draws the outline faint
        /// and writes "?", so the fog is visible rather than absent (3.10).
        /// </summary>
        public static float FunnelShape(BinderScreen b, float x, float y, float w,
                                        IList<Stage> stages)
        {
            int n = stages == null ? 0 : Mathf.Min(stages.Count, 4);
            if (n <= 0) return y;
            float fx = x + FunnelLab;
            float fw = w - FunnelLab;
            float fh = n * (FunnelH + FunnelGap) - FunnelGap;
            DrawnChart.Mount(b.Content, "funnel", FunnelSprite(Mathf.RoundToInt(fw),
                Mathf.RoundToInt(fh), stages, n), fx, y, fw, fh);
            for (int i = 0; i < n; i++)
            {
                Stage st = stages[i];
                float sy = y + i * (FunnelH + FunnelGap);
                b.L((st.Label ?? "").ToUpper(), x, sy + 20f, Detail,
                    Ink(st.Known ? 0.8f : 0.4f), FunnelLab - 10f);
                float mouth = fw * (1f - FunnelNarrow * (i + 0.5f));
                TextMeshProUGUI lbl = b.L(st.Known ? st.ValueText : "?",
                    fx + (fw - mouth) * 0.5f, sy + 14f, Row,
                    st.Known ? DrawnUI.Ink : Ink(0.4f), mouth);
                lbl.alignment = TextAlignmentOptions.Top;
            }
            return y + fh + 12f;
        }

        /// <summary>
        /// A DRAWN FILL — the fuse, the progress vessel laid flat, any "how far
        /// along is it". A pen outline round the whole track, a tinted wash to
        /// `frac`, and the words after it: a chart without its number is
        /// decoration, and decoration does not ship.
        /// </summary>
        public static float Meter(BinderScreen b, float x, float y, float w, float frac,
                                  Color col, string label = "")
        {
            frac = Mathf.Clamp01(frac);
            var ground = DrawnUI.Fill(b.Content, "meter", Ink(0.06f), x, y, w, MeterH);
            ground.raycastTarget = false;
            if (frac > 0f)
                DrawnUI.Fill(b.Content, "meterfill", DrawnUI.WithAlpha(col, 0.6f),
                             x, y, w * frac, MeterH).raycastTarget = false;
            DrawnUI.AddInkEdge(ground.rectTransform, new Vector2(w, MeterH),
                new DrawnUI.PaperStyle
                {
                    ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                    StepsPerEdge = 8, Jitter = 0.9f, Thickness = 2.5f, Seed = 19,
                });
            if (!string.IsNullOrEmpty(label))
                b.L(label, x + w + 14f, y - 6f, Detail, Ink(0.8f), 420f);
            return y + MeterH + 12f;
        }

        /// <summary>One cell of the 2x2 lever grid.</summary>
        public sealed class Cell
        {
            public string Name = "";
            public string Value = "";
            public string Effect = "";
            public Action OnMinus;
            public Action OnPlus;
            public bool AtMin;
            public bool AtMax;
        }

        /// <summary>
        /// THE 2x2 LEVER GRID — eight stacked stepper rows were the ledger's worst
        /// wall of same-weight text; four compact cells are the same levers read
        /// in a glance. Each cell: the NAME small, the money big and in the
        /// founder's own pen, the effect in one word, and the +/- tight against
        /// the cell's right edge.
        /// </summary>
        public static float Grid2(BinderScreen b, float x, float y, IList<Cell> cells)
        {
            int n = cells == null ? 0 : Mathf.Min(cells.Count, 4);
            for (int i = 0; i < n; i++)
            {
                Cell c = cells[i];
                float cx = x + (i % 2) * (Grid2W + Grid2Gap);
                float cy = y + (i / 2) * Grid2H;
                b.L((c.Name ?? "").ToUpper(), cx, cy, 22f, DrawnUI.Ink, 320f);
                b.L(c.Value, cx, cy + 28f, MoneySize, DrawnUI.Coral, 380f);
                b.L(c.Effect, cx, cy + 62f, 18f, Ink(0.6f), Grid2W - 130f);
                Glyph(b, "−", cx + Grid2W - 116f, cy + 18f, c.AtMin, c.OnMinus);
                Glyph(b, "+", cx + Grid2W - 58f, cy + 18f, c.AtMax, c.OnPlus);
                // the ruled ledger under each cell: structure without a box round it
                PenRule(b, cy + Grid2H - 18f, cx, Grid2W, Ink(0.14f), 11 + i);
            }
            return y + ((n + 1) / 2) * Grid2H + 8f;
        }

        /// <summary>
        /// THE ATTENTION DOT — heat as a shape, so the threats list ranks itself
        /// before a word is read. A note is ink and quiet; a warning is the pen;
        /// an alarm is the same pen, simply bigger, with a loop drawn round it.
        /// NOTHING HERE PULSES: the screen is allowed one pulsing element and the
        /// ticker owns it (2.8).
        /// </summary>
        public static void SevDot(BinderScreen b, float x, float y, int severity)
        {
            severity = Mathf.Clamp(severity, 1, 3);
            float r = severity == 1 ? 7f : (severity == 2 ? 8f : 11f);
            Color col = severity == 1 ? Ink(0.5f) : DrawnUI.Coral;
            int side = DrawnUI.RingSide(r, 2);
            float c = SevBox * 0.5f - side * 0.5f;
            var dot = DrawnChart.Mount(b.Content, "sevdot",
                DrawnUI.RingSprite(r, 1.5f, 0.7f, 27, 2, true), x + c, y + c, side, side);
            dot.color = col;
            if (severity < 3) return;
            int rs = DrawnUI.RingSide(r + 4f, 2);
            var halo = DrawnChart.Mount(b.Content, "sevhalo",
                DrawnUI.RingSprite(r + 4f, 2f, 0.8f, 27, 2, false),
                x + SevBox * 0.5f - rs * 0.5f, y + SevBox * 0.5f - rs * 0.5f, rs, rs);
            halo.color = DrawnUI.WithAlpha(DrawnUI.Coral, 0.55f);
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

        // ── the funnel, rasterised ─────────────────────────────────────────────
        // Godot walks four wobbled polylines every frame; Unity has no
        // immediate-mode canvas, so the whole funnel bakes ONCE into a texture
        // and is cached by its own values — the same trade DrawnChart makes for
        // the pie, the spark and the clock. The wobble is seeded, so the same
        // stages give the same drawing every session.

        /// <summary>
        /// The four mouths as one sprite: a tinted wash inside each trapezoid, a
        /// wobbled ink outline round it, and nothing at all inside a fogged stage
        /// but its faint edge.
        /// </summary>
        public static Sprite FunnelSprite(int w, int h, IList<Stage> stages, int n)
        {
            var sb = new System.Text.StringBuilder("funnel|");
            sb.Append(w).Append('|').Append(h);
            for (int i = 0; i < n; i++)
                sb.Append('|').Append(stages[i].Known ? '1' : '0')
                  .Append(ColorUtility.ToHtmlStringRGB(StageCol(stages[i], i)));
            string key = sb.ToString();
            Sprite cached;
            if (_sprites.TryGetValue(key, out cached) && cached != null) return cached;

            w = Mathf.Max(w, 8);
            h = Mathf.Max(h, 8);
            var px = new Color32[w * h];
            var clear = new Color32(255, 255, 255, 0);
            for (int i = 0; i < px.Length; i++) px[i] = clear;

            for (int i = 0; i < n; i++)
            {
                Stage st = stages[i];
                bool known = st.Known;
                Color col = StageCol(st, i);
                float top = w * (1f - FunnelNarrow * i);
                float bot = w * (1f - FunnelNarrow * (i + 1));
                float y0 = i * (FunnelH + FunnelGap);
                float y1 = y0 + FunnelH;
                var quad = new[]
                {
                    new Vector2((w - top) * 0.5f, y0), new Vector2((w + top) * 0.5f, y0),
                    new Vector2((w + bot) * 0.5f, y1), new Vector2((w - bot) * 0.5f, y1),
                };
                if (known) FillQuad(px, w, h, quad, DrawnUI.WithAlpha(col, 0.5f));
                var rng = new System.Random(41 + i * 3);
                var pts = new List<Vector2>();
                for (int k = 0; k < 4; k++)
                {
                    Vector2 a = quad[k];
                    Vector2 bb = quad[(k + 1) % 4];
                    for (int s = 0; s < 7; s++)
                    {
                        Vector2 p = Vector2.Lerp(a, bb, s / 7f);
                        pts.Add(new Vector2(p.x + Wob(rng, 1.4f), p.y + Wob(rng, 1.4f)));
                    }
                }
                pts.Add(pts[0]);
                InkStroke(px, w, h, pts, known ? 3f : 2f,
                          known ? DrawnUI.Ink : Ink(0.35f));
            }

            // the canvas is built top-left down, the way every .gd file reads;
            // Unity textures start bottom-left, so the rows flip exactly once
            var flipped = new Color32[px.Length];
            for (int y = 0; y < h; y++) Array.Copy(px, y * w, flipped, (h - 1 - y) * w, w);
            var tex2 = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex2.wrapMode = TextureWrapMode.Clamp;
            tex2.filterMode = FilterMode.Bilinear;
            tex2.SetPixels32(flipped);
            tex2.Apply(false, false);
            Sprite fs = Sprite.Create(tex2, new UnityEngine.Rect(0f, 0f, w, h),
                                      new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
            _sprites[key] = fs;
            return fs;
        }

        /// The funnel's own ramp, top to bottom: the world's blue, the heat of a
        /// lead, then the sage of a customer who actually arrived.
        static Color StageCol(Stage st, int i)
        {
            if (st.Col.HasValue) return st.Col.Value;
            switch (i)
            {
                case 0: return DrawnUI.Blue;
                case 1: return DrawnUI.Yellow;
            }
            return DrawnUI.Sage;
        }

        static float Wob(System.Random rng, float amount)
        {
            return (float)(rng.NextDouble() * 2.0 - 1.0) * amount;
        }

        /// A trapezoid whose two horizontal edges are its top and bottom — one
        /// scanline per row, so no polygon rasteriser is needed for the one shape
        /// this kit draws.
        static void FillQuad(Color32[] px, int w, int h, Vector2[] q, Color col)
        {
            int y0 = Mathf.Max(Mathf.FloorToInt(q[0].y), 0);
            int y1 = Mathf.Min(Mathf.CeilToInt(q[2].y), h);
            float span = Mathf.Max(q[2].y - q[0].y, 0.001f);
            for (int y = y0; y < y1; y++)
            {
                float t = Mathf.Clamp01((y + 0.5f - q[0].y) / span);
                float lx = Mathf.Lerp(q[0].x, q[3].x, t);
                float rx = Mathf.Lerp(q[1].x, q[2].x, t);
                int a = Mathf.Max(Mathf.CeilToInt(lx), 0);
                int b2 = Mathf.Min(Mathf.FloorToInt(rx), w - 1);
                for (int x = a; x <= b2; x++) Blend(px, y * w + x, col);
            }
        }

        /// One wobbled stroke, composited OVER the wash rather than taking the
        /// brighter alpha: with alpha-max the wash wins wherever the stroke's edge
        /// is softer than it, and the ink comes out gnawed.
        static void InkStroke(Color32[] px, int w, int h, List<Vector2> pts,
                              float thickness, Color col)
        {
            float r = Mathf.Max(thickness * 0.5f, 0.5f);
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                float len = Vector2.Distance(pts[i], pts[i + 1]);
                int steps = Mathf.Max(Mathf.CeilToInt(len * 2f), 1);
                for (int s = 0; s <= steps; s++)
                {
                    Vector2 p = Vector2.Lerp(pts[i], pts[i + 1], (float)s / steps);
                    int x0 = Mathf.Max(Mathf.FloorToInt(p.x - r - 1f), 0);
                    int x1 = Mathf.Min(Mathf.CeilToInt(p.x + r + 1f), w - 1);
                    int yy0 = Mathf.Max(Mathf.FloorToInt(p.y - r - 1f), 0);
                    int yy1 = Mathf.Min(Mathf.CeilToInt(p.y + r + 1f), h - 1);
                    for (int y = yy0; y <= yy1; y++)
                        for (int x = x0; x <= x1; x++)
                        {
                            float dx = x + 0.5f - p.x;
                            float dy = y + 0.5f - p.y;
                            float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                            if (a <= 0f) continue;
                            Blend(px, y * w + x, new Color(col.r, col.g, col.b, a * col.a));
                        }
                }
            }
        }

        static void Blend(Color32[] px, int idx, Color src)
        {
            Color32 dst = px[idx];
            float sa = Mathf.Clamp01(src.a);
            float da = dst.a / 255f;
            float outA = sa + da * (1f - sa);
            if (outA <= 0.0001f) return;
            float k = da * (1f - sa);
            px[idx] = new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt((src.r * 255f * sa + dst.r * k) / outA), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt((src.g * 255f * sa + dst.g * k) / outA), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt((src.b * 255f * sa + dst.b * k) / outA), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(outA * 255f), 0, 255));
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
