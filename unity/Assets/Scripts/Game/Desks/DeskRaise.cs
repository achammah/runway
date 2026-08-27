using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — THE COMPANY · "the raise" (twin of desk_raise.gd). W2 lane:
    /// L-OWN: the four pipeline columns (radar -> conversations -> terms ->
    /// wired), THE COMPARISON (two sheets priced honestly + the SAFE-stack
    /// warning), the six-instrument glossary, the founder-time banner.
    /// Signing runs op_sign_instrument behind the two-tap arm; a no-shop
    /// freezes the pens. All numbers come from SimOwnership/SimEngine.
    /// </summary>
    public static class DeskRaise
    {
        public const string Question = "who would fund us next, and at what true price?";

        /// S8 — the raise sleeps through the garage: no paper signed, no raise
        /// opened, nothing to pipeline. The tab stays on the map at 60%; it
        /// wakes the week the era turns or money moves.
        public static bool IsDormant(GameState s)
        {
            return s.Era == "garage" && (s.Instruments == null || s.Instruments.Count == 0)
                   && !(s.RaiseState != null && s.RaiseState.Active);
        }

        /// S10 — the tab's four-character read. Quiet while dormant; awake,
        /// the interest score is the desk in one glance.
        public static string MicroStatus(GameState s)
        {
            if (IsDormant(s)) return "";
            int terms = SimOwnership.StagesIn(s, "terms").Count;
            if (terms > 0) return terms + " offer" + (terms == 1 ? "" : "s");
            return Gd.F(s.RaiseState != null ? s.RaiseState.InterestScore : 0.0, 0) + "/100";
        }

        public static string[] HeroSummary(GameState s)
        {
            int terms = SimOwnership.StagesIn(s, "terms").Count;
            int motion = InMotion(s);
            if (terms + motion == 0)
                return new[] { "the raise", "interest "
                    + Gd.F(s.RaiseState != null ? s.RaiseState.InterestScore : 0.0, 0)
                    + "/100 — inbound comes to traction, not to wishes" };
            return new[] { terms + " offer" + (terms == 1 ? "" : "s") + " on the table",
                motion + " investor" + (motion == 1 ? "" : "s")
                + " in motion — the buyer buys a piece of you" };
        }

        static int InMotion(GameState s)
        {
            return SimOwnership.StagesIn(s, "radar").Count
                + SimOwnership.StagesIn(s, "conversations").Count
                + SimOwnership.StagesIn(s, "terms").Count;
        }

        static string Ds(Dictionary<string, object> d, string k, string dv)
        {
            object v;
            return d != null && d.TryGetValue(k, out v) && v != null ? Convert.ToString(v) : dv;
        }

        static int Di(Dictionary<string, object> d, string k, int dv)
        {
            object v;
            if (d != null && d.TryGetValue(k, out v) && v != null)
            {
                try { return Convert.ToInt32(v); } catch { return dv; }
            }
            return dv;
        }

        static double Dd(Dictionary<string, object> d, string k, double dv)
        {
            object v;
            if (d != null && d.TryGetValue(k, out v) && v != null)
            {
                try { return Convert.ToDouble(v); } catch { return dv; }
            }
            return dv;
        }

        static bool Dbo(Dictionary<string, object> d, string k, bool dv)
        {
            object v;
            if (d != null && d.TryGetValue(k, out v) && v != null)
            {
                try { return Convert.ToBoolean(v); } catch { return dv; }
            }
            return dv;
        }

        public static void Draw(BinderScreen b)
        {
            GameState state = b.State;
            List<Dictionary<string, object>> terms = SimOwnership.StagesIn(state, "terms");
            // the hero keeps to its own lane: when the vignette rides the
            // header (x600..732, left of the x740 banner) the big line wears
            // its compact form and the sentence trims — no collisions; with
            // no image the full wording keeps the whole band
            bool hasVig = PitchVignette(b);
            string big = terms.Count + " offer" + (terms.Count == 1 ? "" : "s")
                + " on the table · " + InMotion(state) + " in motion";
            string sentence =
                "raising is a pipeline, like customers — except the buyer buys a piece of you.";
            if (hasVig)
            {
                big = terms.Count + " offer" + (terms.Count == 1 ? "" : "s")
                    + " · " + InMotion(state) + " in motion";
                sentence = "raising is a pipeline, like customers —";
            }
            float y = DeskKit.HeroBand(b, big, sentence);
            if (state.RaiseState != null && state.RaiseState.Active)
            {
                TextMeshProUGUI t1 = b.L("the raise eats ≈"
                    + Gd.F(state.RaiseState.FounderTimeTax * 100.0, 0) + "% of your week",
                    740f, 10f, DeskKit.Detail, DrawnUI.Coral, 380f);
                t1.alignment = TextAlignmentOptions.TopRight;
                TextMeshProUGUI t2 = b.L("the shop slows while you pitch — that is real",
                    740f, 40f, 18f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 380f);
                t2.alignment = TextAlignmentOptions.TopRight;
            }
            else
            {
                TextMeshProUGUI t3 = b.L("investor interest "
                    + Gd.F(state.RaiseState != null ? state.RaiseState.InterestScore : 0.0, 0) + "/100",
                    740f, 10f, DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 380f);
                t3.alignment = TextAlignmentOptions.TopRight;
            }

            // ── zone 1 · WHO'S IN MOTION
            DeskKit.CardBox z1 = DeskKit.Zone(b, DeskKit.XId, y, 1120f, 262f, 1, "who's in motion",
                "inbound comes to traction, not to wishes — outbound is yours to spend time on");
            const float colW = 268f;
            float cx = z1.ContentX - 6f;
            const float colH = 176f;
            // THE COLLAPSE LAW per column: one card face-up (the loudest),
            // the crowd folds to an honest count — a column never spills.
            DeskKit.WallCol c1 = DeskKit.WallColumn(b, cx, z1.Cursor, colW, colH, "on the radar", "");
            List<Dictionary<string, object>> radar = SimOwnership.StagesIn(state, "radar");
            List<string> outbound = SimOwnership.OutboundTargets(state);
            if (radar.Count > 0)
            {
                string nm = Ds(radar[0], "name", "?");
                DeskKit.WallCard(b, c1, new DeskKit.WallCardCfg { Title = nm,
                    Facts = new List<string> { Dbo(radar[0], "inbound", false)
                        ? "inbound — saw the growth" : "outbound — your list" },
                    OnPress = () => SimOwnership.OpPitchInvestor(b.State, nm) });
                FoldNote(b, c1, radar.Count - 1, colW, colH);
            }
            else if (outbound.Count > 0)
            {
                DeskKit.WallCard(b, c1, new DeskKit.WallCardCfg
                {
                    Title = outbound.Count + " outbound target" + (outbound.Count == 1 ? "" : "s"),
                    Facts = new List<string> { "pitch one — costs a week's focus" },
                    OnPress = () => SimOwnership.OpPitchInvestor(b.State),
                });
            }
            DeskKit.WallCol c2 = DeskKit.WallColumn(b, cx + colW + 16f, z1.Cursor, colW, colH,
                "conversations", "");
            List<Dictionary<string, object>> convs = SimOwnership.StagesIn(state, "conversations");
            if (convs.Count > 0)
            {
                string doubt = Ds(convs[0], "doubt", "");
                DeskKit.WallCard(b, c2, new DeskKit.WallCardCfg { Title = Ds(convs[0], "name", "?"),
                    Facts = new List<string> { doubt == "" ? "asked for real numbers"
                        : "noticed: " + doubt },
                    Sev = doubt == "" ? 0 : 2 });
                FoldNote(b, c2, convs.Count - 1, colW, colH);
            }
            DeskKit.WallCol c3 = DeskKit.WallColumn(b, cx + (colW + 16f) * 2f, z1.Cursor, colW, colH,
                "terms on the table", "");
            if (terms.Count > 0)
            {
                var t = terms[0].ContainsKey("terms") ? (Dictionary<string, object>)terms[0]["terms"]
                    : new Dictionary<string, object>();
                DeskKit.WallCard(b, c3, new DeskKit.WallCardCfg { Title = Ds(terms[0], "name", "?"),
                    Ready = true,
                    Facts = new List<string> { TermsFact(t), "expires wk " + Di(t, "expires_wk", 0) } });
                FoldNote(b, c3, terms.Count - 1, colW, colH);
            }
            DeskKit.WallCol c4 = DeskKit.WallColumn(b, cx + (colW + 16f) * 3f, z1.Cursor, colW, colH,
                "signed & wired", "");
            if (state.Instruments.Count > 0)
            {
                Instrument idd = state.Instruments[state.Instruments.Count - 1];
                DeskKit.WallCard(b, c4, new DeskKit.WallCardCfg
                {
                    Title = idd.Kind + " — " + idd.Holder,
                    Facts = new List<string> { "$" + SimOwnership.Money(idd.Amount)
                        + " · wk " + idd.SignedWk },
                });
                FoldNote(b, c4, state.Instruments.Count - 1, colW, colH);
            }
            // the authored empty line sits BELOW the four boxes, never across them
            if (InMotion(state) == 0 && state.Instruments.Count == 0)
                DeskKit.Empty(b, z1.ContentX + 8f, z1.Cursor + colH + 8f,
                    "", "nobody is knocking yet — traction is the doorbell", true);
            y += 262f + 10f;

            // ── zone 2 · THE COMPARISON
            DeskKit.CardBox z2 = DeskKit.Zone(b, DeskKit.XId, y, 1120f, 348f, 2, "the comparison",
                "two term sheets never say their true price — this card does");
            if (terms.Count == 0)
            {
                DeskKit.Empty(b, z2.ContentX, z2.Cursor + 8f, "no terms on the table.",
                    "conversations become sheets when the data room holds — growth, margin, runway", true);
            }
            else
            {
                ComparisonTicket(b, state, z2.ContentX, z2.Cursor, terms[0]);
                if (terms.Count >= 2)
                    ComparisonTicket(b, state, z2.ContentX + 375f, z2.Cursor, terms[1]);
                else
                    b.L("one sheet is not a comparison — a second set of terms teaches the price of the first.",
                        z2.ContentX + 385f, z2.Cursor + 10f, DeskKit.Detail,
                        DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f), 330f);
                double stack = SimOwnership.StackDilutionAt(state, SimEngine.Valuation(state));
                if (stack > 0.0)
                    b.L("THE SAFE STACK: with the old paper, ≈" + Gd.F(stack, 0)
                        + "% converts AT ONCE at the next priced round. Deferred is not free.",
                        z2.ContentX + 762f, z2.Cursor + 6f, 19f, DrawnUI.Coral, 340f);
                b.L("participating preferred would be flagged here in red — predatory.",
                    z2.ContentX + 762f, z2.Cursor + 130f, 17f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f), 340f);
            }
            y += 348f + 10f;

            // ── zone 3 · EVERY WAY MONEY COMES IN
            DeskKit.CardBox z3 = DeskKit.Zone(b, DeskKit.XId, y, 1120f, 108f, 3,
                "every way money comes in", "");
            b.L("angel check · SAFE · convertible note · priced round · bridge · venture debt (-> the bank) · secondary — six characters, from a friend's check to selling your own slice",
                z3.ContentX, z3.Cursor, 18f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 1070f);
            b.L(Costline(state), z3.ContentX, z3.Cursor + 28f, DeskKit.Law, DrawnUI.Blue, 1070f);
        }

        static void ComparisonTicket(BinderScreen b, GameState state, float x, float y,
            Dictionary<string, object> entry)
        {
            var t = entry.ContainsKey("terms") ? (Dictionary<string, object>)entry["terms"]
                : new Dictionary<string, object>();
            string nm = Ds(entry, "name", "?");
            string kind = Ds(t, "kind", "safe");
            var lines = new List<DeskKit.TicketLine>
            {
                new DeskKit.TicketLine { Label = "money now",
                    Value = "$" + SimOwnership.Money(Di(t, "amount", 0)) },
            };
            if (kind == "priced")
            {
                double topup = Dd(t, "pool_topup_pct", 0.0);
                lines.Add(new DeskKit.TicketLine { Label = "dilution today",
                    Value = Gd.F(Dd(t, "pct", 0.0), 1) + "%"
                        + (topup > 0.0 ? " + " + Gd.F(topup, 0) + "% pool" : ""),
                    Col = DrawnUI.Coral });
                bool part = Dbo(t, "participating", false);
                lines.Add(new DeskKit.TicketLine { Label = "preferences",
                    Value = part ? "participating — PREDATORY" : "1× non-participating · fair",
                    Col = part ? DrawnUI.Coral : DrawnUI.Ink });
                lines.Add(new DeskKit.TicketLine { Label = "board · control",
                    Value = "1 seat + no-shop " + Di(t, "no_shop_wks", 4) + " wks",
                    Col = DrawnUI.Coral });
            }
            else
            {
                var pseudo = new Instrument { Kind = kind, Amount = Di(t, "amount", 0),
                    Cap = Di(t, "cap", 0), Discount = Dd(t, "discount", 0.0),
                    Rate = Dd(t, "rate", 0.0), SignedWk = state.Week, Pct = 0.0 };
                lines.Add(new DeskKit.TicketLine { Label = "dilution today", Value = "0%",
                    Col = DrawnUI.Sage });
                lines.Add(new DeskKit.TicketLine { Label = "dilution at the next round",
                    Value = "≈" + Gd.F(SimOwnership.ConvertPctAt(pseudo,
                        SimEngine.Valuation(state), state.Week), 1) + "%",
                    Col = DrawnUI.Coral });
                if (kind == "note" || kind == "bridge")
                    lines.Add(new DeskKit.TicketLine { Label = "the fuse",
                        Value = "matures wk " + Di(t, "maturity_wk", 0), Col = DrawnUI.Coral });
                else
                    lines.Add(new DeskKit.TicketLine { Label = "board · control", Value = "none" });
            }
            string character = kind == "safe" ? "fast money, deferred pain"
                : kind == "note" ? "a fuse under fast money"
                : kind == "bridge" ? "insiders keeping you alive" : "real partner, real price";
            float endY = DeskKit.Ticket(b, x, y, 360f, nm + " — the " + kind, lines,
                "character", character, "", DrawnUI.Ink);
            if (SimOwnership.NoShopUntil(state) > state.Week)
            {
                b.L("no-shop holds until wk " + SimOwnership.NoShopUntil(state)
                    + " — the pens are down", x, endY - 8f, 17f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f), 360f);
            }
            else
            {
                string nmv = nm;
                // a long name trims with an honest ellipsis, never mid-word blunt
                string cap = nm.ToUpperInvariant();
                if (cap.Length > 22) cap = cap.Substring(0, 21).TrimEnd() + "…";
                DeskKit.Arm(b, "sign_" + nm, "SIGN " + cap,
                    "press again — the cap table redraws", x, endY - 10f,
                    () => SimOwnership.OpSignInstrument(b.State, nmv), 350f, DeskKit.Detail);
            }
        }

        /// <summary>THE PITCH ILLUSTRATION: a generated vignette at
        /// user://illus_pitch.png rides the header BEHIND the hero when it
        /// exists; the plain header IS the fallback — numbers never wait.</summary>
        static bool PitchVignette(BinderScreen b)
        {
            try
            {
                string p = RunwayPaths.User("illus_pitch.png");
                if (!System.IO.File.Exists(p)) return false;
                byte[] bytes = System.IO.File.ReadAllBytes(p);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(bytes)) return false;
                var host = DrawnUI.Rect(b.Content, "pitch_vignette", 600f, 2f, 132f, 132f);
                var img = new GameObject("img",
                    typeof(RectTransform), typeof(UnityEngine.UI.RawImage))
                    .GetComponent<UnityEngine.UI.RawImage>();
                img.texture = tex;
                img.rectTransform.SetParent(host, false);
                DrawnUI.SetTopLeft(img.rectTransform, 0f, 0f);
                img.rectTransform.sizeDelta = new Vector2(132f, 132f);
                img.color = new Color(1f, 1f, 1f, 0.9f);
                img.raycastTarget = false;
                return true;
            }
            catch (Exception) { return false; }
        }

        static void FoldNote(BinderScreen b, DeskKit.WallCol col, int n, float colW, float colH)
        {
            if (n > 0)
                b.L("+" + n + " more wait behind this one", col.ContentX + 2f,
                    col.Y + colH - 26f, 16f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), colW - 20f);
        }

        static string TermsFact(Dictionary<string, object> t)
        {
            switch (Ds(t, "kind", ""))
            {
                case "safe":
                    return "SAFE $" + SimOwnership.MoneyShort(Di(t, "amount", 0)).TrimStart('$')
                        + " · cap " + SimOwnership.MoneyShort(Di(t, "cap", 0));
                case "note":
                    return "note $" + SimOwnership.MoneyShort(Di(t, "amount", 0)).TrimStart('$')
                        + " · fuse wk " + Di(t, "maturity_wk", 0);
                case "bridge":
                    return "bridge $" + SimOwnership.MoneyShort(Di(t, "amount", 0)).TrimStart('$')
                        + " — insiders";
            }
            return SimOwnership.MoneyShort(Di(t, "amount", 0)) + " at "
                + SimOwnership.MoneyShort(Di(t, "valuation", 0)) + " pre";
        }

        static string Costline(GameState state)
        {
            if (SimOwnership.NoShopUntil(state) > state.Week)
                return "no-shop honored: other terms freeze until wk " + SimOwnership.NoShopUntil(state);
            if (SimOwnership.StagesIn(state, "terms").Count > 0)
                return "signing -> the cap table redraws + covenants arm · walking away is allowed — the best deal is sometimes none";
            return "the data room reads YOUR binder — weak pages become named doubts";
        }

        public static void Handle(BinderScreen b, string id) { }
    }
}
