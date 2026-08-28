using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// THE OFFER — the first MOMENTARY desk (twin of desk_offer.gd), filled:
    /// zone 1 decomposes the headline, zone 2 applies THE WATERFALL
    /// (SimOwnership.Waterfall, pure), zone 3 reads the fishy flags aloud
    /// (computed fields), zone 4 resolves WHO CAN SAY NO from the signed
    /// instruments. The DO lane (S3) carries the three answers in one slot —
    /// [accept — the company sells] (two-tap, the tier said on the control) ·
    /// [negotiate — one counter] (once spent, the lane drops it and the sheet
    /// says "counter spent" plainly) · [decline — the street hears]. The
    /// clock chip re-computes from state every draw, so it ticks weekly.
    /// W2 lane: L-OWN.
    /// </summary>
    public static class DeskOffer
    {
        public const string Question = "should we take their money?";

        public static string[] HeroSummary(GameState s)
        {
            if (s.BuyoutOffer.Count == 0)
                return new[] { "an offer", "cash vs stock vs earnout — read the small lines" };
            return new[] { Ds(s.BuyoutOffer, "buyer", "a buyer") + " offers "
                + SimOwnership.MoneyShort(Di(s.BuyoutOffer, "headline", 0)),
                "cash vs stock vs earnout — read the small lines" };
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
            Dictionary<string, object> bo = state.BuyoutOffer;
            if (bo.Count == 0)
            {
                // S1 — the letter is gone: the empty gold tab teaches what
                // summons it
                DeskKit.ZeroState(b, new DeskKit.ZeroStateCfg
                {
                    WillShow = "a buyout letter, decomposed",
                    WouldLine = "when a buyer writes, this gold tab appears — the headline split into cash, stock and earnout, the waterfall applied, the fine print read aloud",
                    ActionLabel = "fold the tab away",
                    ActionCb = () => b.ResolveMomentary("the offer"),
                    WakesHint = "summoned by the next letter — answered offers fold into HISTORY",
                });
                return;
            }
            int price = Di(bo, "headline", 0);
            int left = Gd.Maxi(Di(bo, "expires_wk", 0) - state.Week, 0);
            float y = DeskKit.HeroBand(b, Ds(bo, "buyer", "a buyer") + " offers $" + GameUi.Money(price),
                "this desk appeared when the letter did — it leaves when you answer.");
            // the clock is computed from state on every draw — it ticks weekly
            DeskKit.ClockChip(b, 880f, 12f, "expires in " + left + " wk" + (left == 1 ? "" : "s"));
            TextMeshProUGUI nb = b.L("while it lives, the raise is frozen by their no-shop ask",
                700f, 44f, 18f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 420f);
            nb.alignment = TextAlignmentOptions.TopRight;
            // S2 — the live letter IS the red: the strip says the ask in its
            // own slot (R5 — the zones hold their ground whether or not it draws)
            DeskKit.AskStrip(b, "the offer", DeskKit.XId, y, 1120f,
                "accept, counter, or decline below");

            // ── zone 1 · WHAT'S ON THE TABLE
            DeskKit.CardBox z1 = DeskKit.Zone(b, DeskKit.XId, y, 548f, 312f, 1, "what's on the table",
                "a headline is not money — read what the $"
                + SimOwnership.MoneyShort(price).TrimStart('$') + " is made of");
            DeskKit.Ticket(b, z1.ContentX, z1.Cursor - 2f, 500f,
                "the $" + SimOwnership.MoneyShort(price).TrimStart('$') + ", decomposed",
                new List<DeskKit.TicketLine>
                {
                    new DeskKit.TicketLine { Label = "cash at closing",
                        Value = "$" + GameUi.Money(Di(bo, "cash", 0)) },
                    new DeskKit.TicketLine { Label = "their stock (locked "
                        + (Di(bo, "lockup_wks", 0) / 4) + " months)",
                        Value = "$" + GameUi.Money(Di(bo, "stock", 0)), Col = DrawnUI.Coral },
                    new DeskKit.TicketLine { Label = "earnout (if targets hit)",
                        Value = "$" + GameUi.Money(Di(bo, "earnout", 0)), Col = DrawnUI.Coral },
                },
                "certain today", "$" + GameUi.Money(Di(bo, "cash", 0)) + " of $" + GameUi.Money(price),
                "and the handcuffs: you must stay " + (Di(bo, "retention_wks", 0) / 4)
                + " months for the stock to vest", DrawnUI.Ink);

            // ── zone 2 · WHO GETS WHAT
            DeskKit.CardBox z2 = DeskKit.Zone(b, DeskKit.XId + 572f, y, 548f, 312f, 2, "who gets what",
                "the waterfall, applied to this exact number — in order");
            Dictionary<string, object> wf = SimOwnership.Waterfall(state, price);
            var rows = (List<Dictionary<string, object>>)wf["rows"];
            int shown = 0;
            foreach (var rd in rows)
            {
                if (shown >= 3) break;
                DeskKit.MoneyRow(b, z2, Ds(rd, "holder", "?"), "$" + GameUi.Money(Di(rd, "take", 0)));
                shown += 1;
            }
            if (rows.Count > 3)
            {
                b.L("+" + (rows.Count - 3) + " more in line", z2.ContentX, z2.Cursor, 17f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 300f);
                z2.Cursor += 24f;
            }
            Dictionary<string, object> dec = SimOwnership.TakeDecomposed(bo, Di(wf, "your_take", 0));
            DeskKit.MoneyRow(b, z2, "YOU", "≈$" + GameUi.Money(Di(wf, "your_take", 0)), DrawnUI.Sage);
            b.L("= $" + GameUi.Money(Di(dec, "cash", 0)) + " cash + $" + GameUi.Money(Di(dec, "stock", 0))
                + " locked stock + $" + GameUi.Money(Di(dec, "earnout", 0)) + " maybe",
                z2.ContentX, z2.Cursor + 2f, 17f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 500f);
            y += 312f + 10f;

            // ── zone 3 · THE FINE PRINT, READ ALOUD
            DeskKit.CardBox z3 = DeskKit.Zone(b, DeskKit.XId, y, 548f, 216f, 3,
                "the fine print, read aloud",
                "some offers are written fishy on purpose");
            float fy = z3.Cursor;
            var flags = new List<string>();
            object fv;
            if (bo.TryGetValue("fishy_flags", out fv) && fv is System.Collections.IEnumerable en && !(fv is string))
                foreach (object o in en) flags.Add(Convert.ToString(o));
            foreach (string f in flags)
            {
                if (fy > z3.Bottom - 36f) break;
                float fx = DeskKit.ClockChip(b, z3.ContentX, fy, "FLAG");
                TextMeshProUGUI fl = b.L(f, fx + 6f, fy + 2f, 16f, DrawnUI.Ink,
                    z3.X + 534f - fx);
                fl.enableWordWrapping = false;
                fl.overflowMode = TextOverflowModes.Ellipsis;
                fy += 38f;
            }
            foreach (string c in CleanLines(bo))
            {
                if (fy > z3.Bottom - 36f) break;
                b.L("CLEAN", z3.ContentX, fy + 2f, 16f, DrawnUI.Sage, 60f);
                TextMeshProUGUI cl = b.L(c, z3.ContentX + 66f, fy + 2f, 16f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 448f);
                cl.enableWordWrapping = false;
                cl.overflowMode = TextOverflowModes.Ellipsis;
                fy += 38f;
            }
            if (flags.Count == 0 && CleanLines(bo).Count == 0)
                DeskKit.Empty(b, z3.ContentX, z3.Cursor + 6f,
                    "a plain offer — no small lines to trip on.", "");

            // ── zone 4 · WHO CAN SAY NO
            DeskKit.CardBox z4 = DeskKit.Zone(b, DeskKit.XId + 572f, y, 548f, 216f, 4, "who can say no",
                "the powers were signed at the raise, years early");
            float py = z4.Cursor;
            foreach (var pd in SimOwnership.Powers(state, price))
            {
                if (py > z4.Bottom - 36f) break;
                b.L(Ds(pd, "who", "?"), z4.ContentX, py, 19f, DrawnUI.Ink, 150f);
                Color col = Dbo(pd, "blocks", false) ? DrawnUI.Coral
                    : Ds(pd, "who", "") == "you" ? DrawnUI.Sage : DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f);
                string line = Ds(pd, "line", "");
                b.L(line, z4.ContentX + 156f, py, 17f, col, 360f);
                py += line.Length > 45 ? 44f : 26f;
                py += 8f;
            }
            y += 216f + 14f;

            // ── the three answers — the DO lane mirrors the whole desk (S3):
            // accept says its tier (S9's two-tap), the counter rides while it
            // lasts, decline is as pressable as yes
            var actions = new List<DeskKit.DoAction>
            {
                new DeskKit.DoAction
                {
                    Label = "accept — the company sells", Tier = "two-tap",
                    Cb = () =>
                    {
                        SimOwnership.BuyoutAccept(b.State);
                        b.ResolveMomentary("the offer");
                    },
                },
            };
            if (Dbo(bo, "countered", false))
                // the spent counter, said plainly where the answer lane lives
                DeskKit.FitLine(b, "NEGOTIATE — counter spent · one was all the room there was",
                    DeskKit.XId, DeskKit.DoLaneY + 10f, DeskKit.Detail,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f), 560f);
            else
                actions.Add(new DeskKit.DoAction
                {
                    Label = "negotiate — one counter", Tier = "",
                    Cb = () => SimOwnership.BuyoutNegotiate(b.State),
                });
            actions.Add(new DeskKit.DoAction
            {
                Label = "decline — the street hears", Tier = "",
                Cb = () =>
                {
                    SimOwnership.BuyoutDecline(b.State);
                    b.ResolveMomentary("the offer");
                },
            });
            DeskKit.DoLane(b, actions);
            DeskKit.Footer(b,
                "answered -> this tab folds into HISTORY · declined offers can sour, or come back higher",
                "the street hears everything", "", 812f, 846f);
        }

        static List<string> CleanLines(Dictionary<string, object> bo)
        {
            var outp = new List<string>();
            if (!Dbo(bo, "retention_carve", false))
                outp.Add("retention carved from the buyer's side, not yours — fair.");
            if (Di(bo, "earnout", 0) > 0 && Ds(bo, "earnout_controller", "") == "neutral")
                outp.Add("the earnout's targets are measured by a neutral auditor — as clean as earnouts get.");
            if (Di(bo, "lockup_wks", 0) > 0 && Di(bo, "lockup_wks", 0) < 52)
                outp.Add("the stock unlocks inside a year — short, as lockups go.");
            return outp;
        }

        static void Resolve(BinderScreen b)
        {
            b.ResolveMomentary("the offer");
        }

        public static void Handle(BinderScreen b, string id)
        {
            if (id == "resolve") Resolve(b);
        }

        // ── the desk conventions (S8) — the rail reads these ─────────────────

        public static bool IsDormant(GameState _s) { return false; }

        /// The rail's right-aligned word: the countdown, while the letter lives.
        public static string MicroStatus(GameState s)
        {
            if (s.BuyoutOffer.Count == 0) return "";
            object ew;
            int exp = s.BuyoutOffer.TryGetValue("expires_wk", out ew) && ew != null
                ? Convert.ToInt32(ew) : 0;
            return string.Format("{0} wk", Gd.Maxi(exp - s.Week, 0));
        }
    }
}
