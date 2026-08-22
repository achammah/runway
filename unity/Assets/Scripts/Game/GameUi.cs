using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;

namespace Runway.Game
{
    /// <summary>
    /// THE FEW DRAWN PARTS DrawnUI DOES NOT ALREADY OWN.
    ///
    /// DrawnUI is the hand: paper, ink edges, rules, handwriting, paper buttons. This
    /// adds only what the draft, the book and the room need on top of it and nothing
    /// else — the inner classes of founder_draft_screen.gd and garage_view_screen.gd
    /// that are genuinely missing from the kit:
    ///
    ///   PaperSheet     PaperEdge — a cream card with a leaning wobble, no button
    ///   Picture        a streamed drawing, aspect-fitted into a box (TextureRect)
    ///   PenRing        InkTag(shape=1) — the coral loop that marks a choice
    ///   PenCross       the scribble that retires a taken card
    ///   Shadow         EllipseShadow — the contact shadow under a standing object
    ///   Pips           StatPips / TraitPips — the two ledgers on the founder card
    ///   Money          the thousands separator every screen prints money with
    ///   Tilt           Godot rotation → Unity rotation, in ONE place
    ///
    /// GODOT ROTATES CLOCKWISE, UNITY ANTICLOCKWISE. Every `.rotation = r` in the
    /// .gd files is negated exactly once, here, so a transcribed lean leans the way
    /// the original did instead of mirroring it.
    /// </summary>
    public static class GameUi
    {
        public static readonly Color Night = DrawnUI.Hex("39434B");

        // ── geometry ───────────────────────────────────────────────────────────

        /// Godot `node.rotation = r` (radians, clockwise) on a Unity RectTransform.
        public static void Tilt(RectTransform rt, float godotRadians)
        {
            if (rt == null) return;
            rt.localRotation = Quaternion.Euler(0f, 0f, -godotRadians * Mathf.Rad2Deg);
        }

        /// Rotate about the CENTRE of a top-left rect without moving its corner —
        /// the shape `pivot_offset = size / 2` gives in Godot.
        public static void TiltCentre(RectTransform rt, float godotRadians)
        {
            if (rt == null) return;
            Vector2 size = rt.sizeDelta;
            Vector2 topLeft = rt.anchoredPosition;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(topLeft.x + size.x * 0.5f, topLeft.y - size.y * 0.5f);
            Tilt(rt, godotRadians);
        }

        // ── paper ──────────────────────────────────────────────────────────────

        /// founder_draft_screen.gd's PaperEdge, as ONE set of numbers: the shadow at
        /// (7, 9) at 0.18, and an ink border walked in 13 steps to the edge, 2.1px of
        /// wobble, sitting `thickness/2 + 3` inside the paper. Every card AND every
        /// paper button on this flow is cut with these — the draft's hand is heavier
        /// and its shadow softer than the title screen's, and a button carrying the
        /// title's (4, 5) at 0.35 reads as a different piece of paper on the same page.
        /// `lean` picks the wobble, exactly as `e.lean = int(b.position.x) % 5` does.
        public static DrawnUI.PaperStyle DraftPaper(int lean = 0, float thickness = 4f)
        {
            return new DrawnUI.PaperStyle
            {
                ShadowOffset = new Vector2(7f, 9f),
                ShadowAlpha = 0.18f,
                Inset = thickness * 0.5f + 3f,
                StepsPerEdge = 13,
                Jitter = 2.1f,
                Thickness = thickness,
                Seed = 17 + lean,
            };
        }

        /// The same paper as a plain sheet: a real shadow, a cream body and the inked
        /// border. No button, no word.
        public static RectTransform PaperSheet(RectTransform parent, float x, float y,
                                               float w, float h, int lean = 0,
                                               float thickness = 4f, Color? edge = null,
                                               string name = "sheet")
        {
            DrawnUI.PaperStyle st = DraftPaper(lean, thickness);
            var root = DrawnUI.Rect(parent, name, x, y, w, h);
            var shadow = DrawnUI.Fill(root, "shadow", new Color(0f, 0f, 0f, st.ShadowAlpha),
                                      st.ShadowOffset.x, st.ShadowOffset.y, w, h);
            shadow.raycastTarget = false;
            var body = DrawnUI.Fill(root, "paper", DrawnUI.Cream, 0f, 0f, w, h);
            body.raycastTarget = false;
            st.ShadowOffset = Vector2.zero;      // the sheet drew its own, above
            st.ShadowAlpha = 0f;
            var img = DrawnUI.AddInkEdge(root, new Vector2(w, h), st);
            img.color = edge ?? DrawnUI.Ink;
            return root;
        }

        // ── drawings ───────────────────────────────────────────────────────────

        /// A streamed drawing, KEEP_ASPECT_CENTERED inside (x, y, w, h). Returns the
        /// RawImage so a caller can tint it; the rect shrinks to the picture's own
        /// aspect the moment it lands and is centred in the box it was given.
        public static RawImage Picture(RectTransform parent, string name, string artPath,
                                       float x, float y, float w, float h,
                                       Action<Texture2D> onLoaded = null)
        {
            var rt = DrawnUI.Rect(parent, name, x, y, w, h);
            var img = rt.gameObject.AddComponent<RawImage>();
            img.color = Color.white;
            img.raycastTarget = false;
            img.enabled = false;                 // nothing to show until it lands
            float bx = x, by = y, bw = w, bh = h;
            ArtCache.Load(artPath, tex =>
            {
                if (img == null || rt == null) return;
                if (tex == null)
                {
                    if (onLoaded != null) onLoaded(null);
                    return;
                }
                img.texture = tex;
                img.enabled = true;
                Fit(rt, tex, bx, by, bw, bh);
                if (onLoaded != null) onLoaded(tex);
            });
            return img;
        }

        /// TextureRect.STRETCH_KEEP_ASPECT_CENTERED, applied once.
        public static void Fit(RectTransform rt, Texture2D tex,
                               float boxX, float boxY, float boxW, float boxH)
        {
            if (rt == null || tex == null || tex.width <= 0 || tex.height <= 0) return;
            float ar = (float)tex.width / tex.height;
            float dw = boxW;
            float dh = dw / ar;
            if (dh > boxH) { dh = boxH; dw = dh * ar; }
            rt.sizeDelta = new Vector2(dw, dh);
            DrawnUI.SetTopLeft(rt, boxX + (boxW - dw) * 0.5f, boxY + (boxH - dh) * 0.5f);
        }

        /// The same drawing, but bound to a RawImage that already exists.
        ///
        /// NOTHING GOES DARK ON A MAYBE. The picture that is up stays up until the
        /// replacement is actually in hand: blanking the image first and never putting
        /// it back on a miss is how a page ends up with a permanent hole in it, and
        /// this flow can NEVER fall back to a blank screen. The one thing taken down
        /// is a drawing of something ELSE that cannot be replaced — a wrong picture
        /// under a right name is worse than an honest gap — which is why the image
        /// remembers which path it is showing.
        public static void Rebind(RawImage img, string artPath,
                                  float boxX, float boxY, float boxW, float boxH)
        {
            if (img == null) return;
            var rt = img.rectTransform;
            var mark = img.GetComponent<ShownArt>();
            if (mark == null) mark = img.gameObject.AddComponent<ShownArt>();
            bool alreadyUp = img.enabled && img.texture != null && mark.Path == artPath;
            ArtCache.Load(artPath, tex =>
            {
                if (img == null || rt == null) return;
                if (tex == null)
                {
                    if (!alreadyUp && img.enabled) { img.enabled = false; mark.Path = null; }
                    return;
                }
                img.texture = tex;
                img.enabled = true;
                mark.Path = artPath;
                Fit(rt, tex, boxX, boxY, boxW, boxH);
            }, true);   // a swap on a live screen is never background work
        }

        // ── pen marks ──────────────────────────────────────────────────────────

        /// InkTag(shape = 1): the coral loop the log book circles a chosen thing with.
        /// The baked ring is round and stretched to the cell, which is what a hand
        /// drawing round a tall cell does anyway.
        public static Image PenRing(RectTransform parent, float x, float y, float w, float h,
                                    Color color, int seed = 3, float thickness = 4f)
        {
            var rt = DrawnUI.Rect(parent, "ring", x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = DrawnUI.RingSprite(60f, thickness * 60f / Mathf.Max(w, h) * 2f,
                                            1.6f, seed, 4, false);
            img.color = color;
            img.raycastTarget = false;
            img.type = Image.Type.Simple;
            return img;
        }

        /// PenCross: two hard scribbled slashes over a card that is already spent.
        public static RectTransform PenCross(RectTransform parent, float w, float h)
        {
            var rt = DrawnUI.Rect(parent, "cross", 0f, 0f, w, h);
            AddSlash(rt, w * 0.08f, h * 0.10f, w * 0.92f, h * 0.90f, 29);
            AddSlash(rt, w * 0.92f, h * 0.12f, w * 0.08f, h * 0.88f, 31);
            return rt;
        }

        static void AddSlash(RectTransform parent, float x0, float y0, float x1, float y1, int seed)
        {
            float dx = x1 - x0;
            float dy = y1 - y0;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 4f) return;
            var rt = DrawnUI.Rect(parent, "slash", x0, y0 - 6f, len, 12f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x0, -y0);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = DrawnUI.WobbleLineSprite(Mathf.RoundToInt(len), 5f, 22, 3.5f, seed, 5);
            img.color = DrawnUI.WithAlpha(DrawnUI.Coral, 0.78f);
            img.raycastTarget = false;
            rt.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
        }

        /// EllipseShadow: the contact patch under something standing on a lit floor.
        public static Image Shadow(RectTransform parent, float x, float y, float w, float h)
        {
            var rt = DrawnUI.Rect(parent, "shadow", x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = DrawnUI.RingSprite(48f, 1f, 0f, 5, 2, true);
            img.color = new Color(0.04f, 0.04f, 0.04f, 0.35f);
            img.raycastTarget = false;
            return img;
        }

        /// One wobbled rule, coral by default — HandRule, cleared of its descenders.
        public static Image HandRule(RectTransform parent, float x, float y, float length,
                                     Color? color = null, int seed = 4)
        {
            return DrawnUI.Rule(parent, x, y + 6f, length, color ?? DrawnUI.Coral, 6f, seed, 1.8f, 30);
        }

        // ── the two ledgers on the founder card ────────────────────────────────

        /// StatPips: five chunky pip rows, 52px to the row.
        public static void StatPips(RectTransform parent, float x, float y,
                                    IDictionary<string, int> stats, string[] names, string[] labels)
        {
            for (int i = 0; i < names.Length; i++)
            {
                float ry = y + i * 52f;
                DrawnUI.HandLabel(parent, labels[i], x, ry + 2f, 26f, DrawnUI.Ink, 150f);
                int v;
                if (stats == null || !stats.TryGetValue(names[i], out v)) v = 0;
                for (int p = 0; p < 5; p++)
                {
                    float px = x + 158f + p * 60f;
                    bool on = p < v;
                    var fill = DrawnUI.Fill(parent, "pip",
                        on ? DrawnUI.Coral : DrawnUI.WithAlpha(DrawnUI.Ink, 0.06f),
                        px, ry + 7f, 48f, 28f);
                    fill.raycastTarget = false;
                    var edge = DrawnUI.AddInkEdge(fill.rectTransform, new Vector2(48f, 28f),
                        new DrawnUI.PaperStyle
                        {
                            ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 1f,
                            StepsPerEdge = 5, Jitter = 0.6f,
                            Thickness = on ? 2.5f : 2f, Seed = 7,
                        });
                    edge.color = on ? DrawnUI.Ink : DrawnUI.WithAlpha(DrawnUI.Ink, 0.32f);
                }
            }
        }

        public const float TraitColW = 235f;
        public const float TraitRowH = 44f;

        /// TraitPips: the six, in the size of a footnote — two columns of three, the
        /// name in ink over a rule, five small pips each, and the swing the packed bag
        /// put on them written beside in the founder's own pen.
        ///
        /// EVERY PIP IS INKED, on and off alike — the same hand that draws the stat
        /// pips, a third of the size. A bare coral square with no border is a UI
        /// element; a bordered one is a box somebody filled in.
        public static void TraitPips(RectTransform parent, float x, float y,
                                     IDictionary<string, int> traits, IList<string> names,
                                     IDictionary<string, int> deltas = null)
        {
            for (int i = 0; i < names.Count; i++)
            {
                float cx = x + (i / 3) * TraitColW;
                float cy = y + (i % 3) * TraitRowH;
                string t = names[i];
                int v;
                if (traits == null || !traits.TryGetValue(t, out v)) v = 3;
                int d;
                if (deltas == null || !deltas.TryGetValue(t, out d)) d = 0;
                v = Mathf.Clamp(v + d, 1, 5);
                string word = t.ToUpper();
                DrawnUI.HandLabel(parent, word, cx, cy, 19f, DrawnUI.Ink, 124f);
                // THE RULE IS MEASURED, NOT COUNTED. Eleven pixels a letter makes
                // "PARANOIA" and "LUCK" the wrong lengths in this hand; the original
                // asks the font how wide the word actually is and stops at 124.
                DrawnUI.Fill(parent, "dot", DrawnUI.WithAlpha(DrawnUI.Ink, 0.22f),
                             cx, cy + 26f,
                             Mathf.Min(DrawnUI.MeasureWidth(word, 19f), 124f), 1.5f);
                for (int p = 0; p < 5; p++)
                {
                    bool on = p < v;
                    var fill = DrawnUI.Fill(parent, "tp",
                        on ? DrawnUI.Coral : DrawnUI.WithAlpha(DrawnUI.Ink, 0.07f),
                        cx + 128f + p * 17f, cy + 5f, 13f, 13f);
                    fill.raycastTarget = false;
                    var edge = DrawnUI.AddInkEdge(fill.rectTransform, new Vector2(13f, 13f),
                        new DrawnUI.PaperStyle
                        {
                            ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 0.8f,
                            StepsPerEdge = 4, Jitter = 0.35f,
                            Thickness = on ? 1.6f : 1.4f, Seed = 7,
                        });
                    edge.color = on ? DrawnUI.Ink : DrawnUI.WithAlpha(DrawnUI.Ink, 0.30f);
                }
                // What the bag did to this trait, in sage for a gain and coral for a
                // cost. It lives in the 26px gutter between the last pip and the next
                // column, so it is set a size down from the original's 20 and pushed
                // right against the column edge — at 20 it touches the next word.
                if (d != 0)
                    DrawnUI.HandLabel(parent, (d > 0 ? "+" : "") + d, cx + 206f, cy + 1f,
                                      17f, d > 0 ? DrawnUI.Sage : DrawnUI.Coral, 27f,
                                      TextAlignmentOptions.TopRight);
            }
        }

        // ── words ──────────────────────────────────────────────────────────────

        /// _fmt_money: "12,500" / "-300". Every screen in the game prints money this way.
        public static string Money(int v)
        {
            string t = Mathf.Abs(v).ToString();
            string outp = "";
            while (t.Length > 3)
            {
                outp = "," + t.Substring(t.Length - 3) + outp;
                t = t.Substring(0, t.Length - 3);
            }
            return (v < 0 ? "-" : "") + t + outp;
        }

        /// "-$300", never "$-300": the minus belongs to the money.
        public static string Cash(int v)
        {
            return v < 0 ? "-$" + Money(-v) : "$" + Money(v);
        }

        public static string Compact(int n)
        {
            if (n >= 1000000) return (n / 1000000f).ToString("0.0") + "M";
            if (n >= 1000) return (n / 1000) + "k";
            return n.ToString();
        }

        /// ModStrip: one line of trait arithmetic in the founder's own pen — what a
        /// thing gives in sage, what it costs in coral, separated the way the log book
        /// separates anything: a middle dot. A Label cannot hold two colours and a
        /// rich-text box would be the one web control on a screen where all is drawn,
        /// so the tokens are measured and laid out by hand.
        ///
        /// A caption ending in a colon takes a space, not a dot: the dots separate the
        /// terms of the sum, they do not follow the heading.
        public static RectTransform TokenLine(RectTransform parent, float x, float y, float w,
                                              IList<KeyValuePair<string, Color>> tokens,
                                              float size, bool centred = true)
        {
            var host = DrawnUI.Rect(parent, "tokens", x, y, w, size * 1.6f);
            if (tokens == null || tokens.Count == 0) return host;
            const string sep = "  ·  ";
            float sepW = DrawnUI.MeasureWidth(sep, size);
            var widths = new float[tokens.Count];
            float total = 0f;
            for (int i = 0; i < tokens.Count; i++)
            {
                widths[i] = DrawnUI.MeasureWidth(tokens[i].Key, size);
                total += widths[i];
                if (i < tokens.Count - 1)
                    total += tokens[i].Key.EndsWith(":") ? size * 0.4f : sepW;
            }
            float cx = centred ? Mathf.Max((w - total) * 0.5f, 0f) : 0f;
            for (int i = 0; i < tokens.Count; i++)
            {
                var t = DrawnUI.HandLabel(host, tokens[i].Key, cx, 0f, size, tokens[i].Value,
                                          widths[i] + 8f);
                t.textWrappingMode = TextWrappingModes.NoWrap;
                cx += widths[i];
                if (i >= tokens.Count - 1) continue;
                if (tokens[i].Key.EndsWith(":")) { cx += size * 0.4f; continue; }
                var d = DrawnUI.HandLabel(host, sep, cx, 0f, size,
                                          DrawnUI.WithAlpha(DrawnUI.Ink, 0.4f), sepW + 8f);
                d.textWrappingMode = TextWrappingModes.NoWrap;
                cx += sepW;
            }
            return host;
        }

        /// A flat word-only button that answers with a coral hover — the shape every
        /// journal control and binder tab uses.
        public static Button InkWord(RectTransform parent, string text,
                                     float x, float y, float w, float h, float size,
                                     Color word, Action onClick,
                                     TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            return DrawnUI.FlatButton(parent, text, x, y, w, h, size, word, DrawnUI.Coral,
                                      onClick, align);
        }

        /// A full-stage click swallower — the dim behind a modal, and its dismissal.
        public static Image Scrim(RectTransform parent, Color color, Action onClick)
        {
            var img = DrawnUI.FullFill(parent, "scrim", color, true);
            if (onClick != null)
            {
                var b = img.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.targetGraphic = img;
                b.onClick.AddListener(() => onClick());
            }
            return img;
        }
    }

    /// WHICH DRAWING A PICTURE IS CURRENTLY SHOWING. Rebind needs to tell "the same
    /// picture is already up" from "that is a drawing of something else" when a swap
    /// comes back empty, and the answer has to die with the object rather than sit in
    /// a static table keyed by an image that was destroyed six screens ago.
    public sealed class ShownArt : MonoBehaviour
    {
        public string Path;
    }
}
