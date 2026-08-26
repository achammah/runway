using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — COSTS · "recruitment" (twin of desk_recruit.gd). W2 lane:
    /// L-OWN: the open seats (band from the labor market, advert stepper with
    /// the live ≈applicants/wk read), the candidate wall (applied →
    /// interviewed → offer out → joined; interviewing costs the founder's
    /// week), THE OFFER COMPOSER (cash + options steppers, live acceptance
    /// odds, the pool-after line, SEND armed). All odds come from
    /// SimOwnership.AcceptanceOdds — the desk recomputes nothing.
    /// </summary>
    public static class DeskRecruit
    {
        public const string Question = "who are we hiring, and will they say yes?";

        public const int CashStep = 10;
        public const double OptStep = 0.1;

        public static string[] HeroSummary(GameState s)
        {
            int seats = Roles(s).Count;
            int motion = CandidatesIn(s, new[] { "applied", "interviewed", "offer" }).Count;
            if (seats == 0 && motion == 0)
                return new[] { "hiring", "no seats open — the pipeline starts with an advert" };
            int offers = Offers(s).Count;
            return new[] { seats + " seat" + (seats == 1 ? "" : "s") + " open",
                motion + " candidate" + (motion == 1 ? "" : "s") + " in motion · "
                + offers + " offer" + (offers == 1 ? "" : "s") + " out" };
        }

        static List<Dictionary<string, object>> Roles(GameState s)
        {
            return s.Recruitment != null ? s.Recruitment.Roles : new List<Dictionary<string, object>>();
        }

        static List<Dictionary<string, object>> Offers(GameState s)
        {
            return s.Recruitment != null ? s.Recruitment.OffersOut : new List<Dictionary<string, object>>();
        }

        static List<Dictionary<string, object>> CandidatesIn(GameState s, string[] stages)
        {
            var outp = new List<Dictionary<string, object>>();
            if (s.Recruitment == null) return outp;
            foreach (var c in s.Recruitment.Candidates)
                if (Array.IndexOf(stages, Ds(c, "stage", "")) >= 0) outp.Add(c);
            return outp;
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

        static string DState(BinderScreen b, string key, string dv)
        {
            object v;
            return b.Desk.TryGetValue(key, out v) && v != null ? Convert.ToString(v) : dv;
        }

        public static void Draw(BinderScreen b)
        {
            GameState state = b.State;
            if (DState(b, "mode", "") == "seats") { DrawSeatsPage(b, state); return; }
            List<Dictionary<string, object>> roles = Roles(state);
            List<Dictionary<string, object>> offers = Offers(state);
            int motion = CandidatesIn(state, new[] { "applied", "interviewed", "offer" }).Count;
            float y = DeskKit.HeroBand(b,
                roles.Count + " seat" + (roles.Count == 1 ? "" : "s") + " open · " + motion + " in motion",
                "hiring is a pipeline too — and the offer is a design: cash, equity, title.");
            if (offers.Count > 0)
            {
                Dictionary<string, object> off = offers[0];
                Dictionary<string, object> cand = SimOwnership.CandById(state, Ds(off, "candidate_id", ""));
                string first = Ds(cand, "name", "someone").Split(' ')[0];
                DeskKit.ClockChip(b, 800f, 12f, first + "'s offer expires in "
                    + Gd.Maxi(Di(off, "expires_wk", 0) - state.Week, 0) + " wk");
            }
            TextMeshProUGUI t2 = b.L("interviews cost founder time", 740f, 44f, 18f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 380f);
            t2.alignment = TextAlignmentOptions.TopRight;

            // ── zone 1 · THE OPEN SEATS
            DeskKit.CardBox z1 = DeskKit.Zone(b, DeskKit.XId, y, 1120f, 174f, 1, "the open seats",
                "every seat carries the market's band before you advertise");
            if (roles.Count == 0)
                DeskKit.Empty(b, z1.ContentX, z1.Cursor + 4f, "no seats open.",
                    "open one — the advert is the magnet, the band is the market", true);
            for (int i = 0; i < Gd.Mini(roles.Count, 2); i++)
            {
                Dictionary<string, object> rd = roles[i];
                float fx = z1.ContentX + i * 552f;
                DeskKit.CardBox fr = DeskKit.CardFrame(b, fx, z1.Cursor - 4f, 532f, 96f,
                    Ds(rd, "seat", "?").ToUpperInvariant() + " · band $"
                    + SimOwnership.Money(Di(rd, "band_lo", 0)) + "–"
                    + SimOwnership.Money(Di(rd, "band_hi", 0)) + "/wk", true);
                string rid = Ds(rd, "id", "");
                int adv = Di(rd, "advert_wk", 0);
                DeskKit.MoneyRow(b, fr, "advert  -> ≈"
                    + Gd.F(SimOwnership.ArrivalRateR(state, rd), 1) + " applicants/wk",
                    "$" + adv + "/wk", DrawnUI.Ink,
                    () => SimOwnership.SetAdvert(b.State, rid, adv - CashStep),
                    () => SimOwnership.SetAdvert(b.State, rid, adv + CashStep),
                    adv <= 0, adv >= 400);
            }
            if (roles.Count > 2)
                b.L("+" + (roles.Count - 2) + " more seat(s)", z1.ContentX + 8f, z1.Bottom - 26f,
                    17f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 300f);
            DeskKit.Word(b, "open a seat", z1.ContentX + 940f, z1.Y + 8f,
                () => { b.Desk["mode"] = "seats"; }, DeskKit.Detail,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 170f);
            y += 174f + 10f;

            // ── zone 2 · THE CANDIDATES
            DeskKit.CardBox z2 = DeskKit.Zone(b, DeskKit.XId, y, 1120f, 258f, 2, "the candidates",
                "interviewing costs your week; ghosting costs your name");
            const float colW = 268f;
            const float colH = 162f;
            float cx = z2.ContentX - 6f;
            string[][] heads = { new[] { "applied", "applied" }, new[] { "interviewed", "interviewed" },
                new[] { "offer out", "offer" }, new[] { "joined", "joined" } };
            for (int hi = 0; hi < heads.Length; hi++)
            {
                DeskKit.WallCol col = DeskKit.WallColumn(b, cx + hi * (colW + 16f), z2.Cursor,
                    colW, colH, heads[hi][0], "");
                string stage = heads[hi][1];
                int shown = 0;
                List<Dictionary<string, object>> cands = CandidatesIn(state, new[] { stage });
                foreach (var cd in cands)
                {
                    if (shown >= 1) break;
                    string cid = Ds(cd, "id", "");
                    var facts = new List<string> { SeatWord(state, cd) + " · asks $"
                        + SimOwnership.Money(Di(cd, "ask", 0)) };
                    var cfg = new DeskKit.WallCardCfg { Title = Ds(cd, "name", "?"), Facts = facts };
                    switch (stage)
                    {
                        case "applied":
                            cfg.OnPress = () => SimOwnership.Interview(b.State, cid);
                            break;
                        case "interviewed":
                            facts.Add(Ds(cd, "profile", ""));
                            cfg.OnPress = () =>
                            {
                                b.Desk["cand"] = cid;
                                b.Desk.Remove("cash");
                                b.Desk.Remove("opt");
                            };
                            if (DState(b, "cand", "") == cid) cfg.Sev = 1;
                            break;
                        case "offer":
                            cfg.Ready = true;
                            Dictionary<string, object> off2 = OfferFor(state, cid);
                            if (off2 != null)
                                facts.Add("$" + SimOwnership.Money(Di(off2, "cash_wk", 0)) + "/wk + "
                                    + Gd.F(Dd(off2, "options_pct", 0.0), 1) + "%");
                            break;
                        default:
                            facts.Add("wk " + Di(cd, "arrived_wk", 0) + " — in");
                            break;
                    }
                    DeskKit.WallCard(b, col, cfg);
                    shown += 1;
                }
                if (cands.Count > shown)
                    b.L("+" + (cands.Count - shown) + " more wait behind", col.ContentX + 2f,
                        col.Y + colH - 26f, 16f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), colW - 20f);
            }
            y += 258f + 10f;

            // ── zone 3 · THE OFFER COMPOSER
            DeskKit.CardBox z3 = DeskKit.Zone(b, DeskKit.XId, y, 1120f, 284f, 3, "the offer composer",
                "comp is a mix, the pool is finite · signed -> TEAM grows a vesting bar · declined -> the market hears");
            Dictionary<string, object> cand2 = ComposerTarget(b, state);
            if (cand2 == null)
                DeskKit.Empty(b, z3.ContentX, z3.Cursor + 8f, "nobody is at the offer stage.",
                    "interview a candidate — the composer opens on whoever you pick", true);
            else
                Composer(b, state, z3, cand2);

        }

        static void Composer(BinderScreen b, GameState state, DeskKit.CardBox z3,
            Dictionary<string, object> cand)
        {
            string cid = Ds(cand, "id", "");
            int ask = Di(cand, "ask", 0);
            object cv;
            int cash = b.Desk.TryGetValue("cash", out cv) && cv != null
                ? Convert.ToInt32(cv) : ask - (ask % 10);
            object ov;
            double opt = b.Desk.TryGetValue("opt", out ov) && ov != null
                ? Convert.ToDouble(ov) : 0.0;
            double free = SimOwnership.PoolFree(state);
            DeskKit.CardBox fr = DeskKit.CardFrame(b, z3.ContentX, z3.Cursor - 2f, 480f, 140f,
                "the mix — " + Ds(cand, "name", "?"), true);
            DeskKit.MoneyRow(b, fr, "cash", "$" + cash + "/wk", DrawnUI.Ink,
                () => { b.Desk["cand"] = cid; b.Desk["cash"] = Gd.Maxi(cash - CashStep, 10); },
                () => { b.Desk["cand"] = cid; b.Desk["cash"] = cash + CashStep; },
                cash <= 10, false);
            DeskKit.MoneyRow(b, fr, "options · 4yr/1yr",
                Gd.F(opt, 1) + "% · " + Gd.F(free - opt, 1) + "% left", DrawnUI.Ink,
                () => { b.Desk["cand"] = cid; b.Desk["opt"] = Gd.Maxf(opt - OptStep, 0.0); },
                () => { b.Desk["cand"] = cid; b.Desk["opt"] = Gd.Minf(opt + OptStep, free); },
                opt <= 0.0, opt + 0.0001 >= free);
            double odds = SimOwnership.AcceptanceOdds(state, cand, cash, opt);
            double dCash = SimOwnership.AcceptanceOdds(state, cand, cash + 30, opt) - odds;
            double dOpt = SimOwnership.AcceptanceOdds(state, cand, cash, opt + 0.2) - odds;
            bool mercenary = Ds(cand, "profile", "") == "mercenary";
            double ratio = cash / Gd.Maxf(ask, 1.0);
            string reads = ratio >= 1.05 ? "rich" : ratio >= 0.97 ? "fair"
                : ratio >= 0.85 ? "fair, cash-light" : "thin";
            var lines = new List<DeskKit.TicketLine>
            {
                new DeskKit.TicketLine { Label = "her ask", Value = "$" + SimOwnership.Money(ask)
                    + " " + (mercenary ? "cash-leaning" : "mission-leaning") },
                new DeskKit.TicketLine { Label = "this mix reads", Value = reads },
            };
            DeskKit.Ticket(b, z3.ContentX + 510f, z3.Cursor - 2f, 380f,
                "will " + Ds(cand, "name", "?").Split(' ')[0] + " say yes?", lines,
                "acceptance odds", "≈" + Gd.RoundToInt(odds) + "%",
                "+$30 cash " + (dCash >= 0 ? "+" : "") + Gd.RoundToInt(dCash)
                + "pts · +0.2% opt " + (dOpt >= 0 ? "+" : "") + Gd.RoundToInt(dOpt) + "pts",
                odds >= 60.0 ? DrawnUI.Sage : DrawnUI.Coral);
            float armY = fr.Bottom + 10f;
            if (SimLabor.SeatsLeft(state) <= 0)
            {
                b.L("the house is full — no desk to offer", z3.ContentX, armY,
                    DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f), 360f);
            }
            else
            {
                DeskKit.Arm(b, "send_offer", "SEND THE OFFER", "press again — the offer goes out",
                    z3.ContentX, armY, () =>
                    {
                        SimOwnership.OpSendOffer(b.State, cid, cash, opt);
                        b.Desk.Remove("cash");
                        b.Desk.Remove("opt");
                    }, 360f, DeskKit.Detail);
            }
        }

        static void DrawSeatsPage(BinderScreen b, GameState state)
        {
            DeskKit.Back(b, "back to recruitment", () => { b.Desk.Remove("mode"); });
            float y = 64f;
            y = DeskKit.HeroBand(b, "open a seat",
                "the advert is the magnet, the ask is the contract — the band is the market's, not yours.",
                DrawnUI.Ink, y);
            if (!SimLabor.MarketOpen(state.Era))
            {
                DeskKit.Empty(b, DeskKit.XId + 20f, y + 10f,
                    "nobody answers an advert taped to a garage door.",
                    "the labor market opens at coworking — until then, hire the people you know", true);
                return;
            }
            foreach (string seat in new[] { "engineer", "sales", "designer", "ops", "support", "manager" })
            {
                if (!SimLabor.RoleUnlocked(seat, state.Era)) continue;
                Dictionary<string, object> band = SimOwnership.BandFor(state, seat);
                bool taken = RoleOpenFor(state, seat) != null;
                string seatV = seat;
                Action press = null;
                if (!taken)
                    press = () =>
                    {
                        SimOwnership.OpenSeat(b.State, seatV);
                        b.Desk.Remove("mode");
                    };
                y = DeskKit.HeroRow(b, y, new DeskKit.HeroRowCfg { Name = seat,
                    Facts = "band $" + SimOwnership.Money(Di(band, "lo", 0)) + "–"
                        + SimOwnership.Money(Di(band, "hi", 0)) + "/wk · ≈$40/wk advert",
                    Value = taken ? "already open" : "open",
                    Col = taken ? DrawnUI.WithAlpha(DrawnUI.Ink, 0.4f) : DrawnUI.Ink,
                    OnPress = press });
            }
            DeskKit.Footer(b, "seats left this era: " + SimLabor.SeatsLeft(state),
                "Esc goes back — an advert starts billing the week it opens", "", 812f, 846f);
        }

        // ───────────────────── the page's own reads ────────────────────────

        static string SeatWord(GameState state, Dictionary<string, object> cand)
        {
            if (state.Recruitment == null) return "?";
            foreach (var r in state.Recruitment.Roles)
                if (Ds(r, "id", "") == Ds(cand, "role_id", "")) return Ds(r, "seat", "?");
            string rid = Ds(cand, "role_id", "");
            if (rid == "") return "the seat closed";
            string[] bits = rid.Length > 5 ? rid.Substring(5).Split('_') : new[] { "?" };
            return SimLabor.RoleRow(bits[0]);
        }

        static Dictionary<string, object> OfferFor(GameState state, string candId)
        {
            foreach (var off in Offers(state))
                if (Ds(off, "candidate_id", "") == candId) return off;
            return null;
        }

        static Dictionary<string, object> RoleOpenFor(GameState state, string seat)
        {
            foreach (var r in Roles(state))
                if (SimLabor.RoleRow(Ds(r, "seat", "")) == SimLabor.RoleRow(seat)) return r;
            return null;
        }

        static Dictionary<string, object> ComposerTarget(BinderScreen b, GameState state)
        {
            string want = DState(b, "cand", "");
            if (want != "")
            {
                Dictionary<string, object> cand = SimOwnership.CandById(state, want);
                if (cand != null && Ds(cand, "stage", "") == "interviewed") return cand;
            }
            List<Dictionary<string, object>> pool = CandidatesIn(state, new[] { "interviewed" });
            return pool.Count > 0 ? pool[0] : null;
        }

        public static void Handle(BinderScreen b, string id) { }
    }
}
