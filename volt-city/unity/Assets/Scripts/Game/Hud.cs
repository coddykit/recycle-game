// Volt City — HUD model (UI.md section 6): cash (compact), rating, shift clock + phase chip, speed pill, balance needle
// (green ±3 %, amber ±5 %, red beyond; colour is never the only signal: icon + "Short 4 MW"), goal line (always visible).
using System;
using VoltCity.Sim;

namespace VoltCity.Game
{
    public static class Hud
    {
        public const float MaxDeg = 60f;
        /// <summary>Needle full scale in per-mille of demand (±10 %).</summary>
        public const int FullScalePm = 100;

        public static NeedleBand Band(int pm, GameData gd)
        {
            int a = Math.Abs(pm);
            if (a <= gd.BandPct * 10) return NeedleBand.Green;
            if (a <= gd.ShedPct * 10) return NeedleBand.Amber;
            return NeedleBand.Red;
        }

        public static float Deg(int pm)
        {
            float d = Math.Max(-MaxDeg, Math.Min(MaxDeg, pm * MaxDeg / FullScalePm));
            return (float)Math.Round(d * 2f) / 2f;
        }

        /// <summary>Phase chip of the diorama day.</summary>
        public static string PhaseKey(double hour)
        {
            if (hour >= 5 && hour < 9) return "phase.morning";
            if (hour >= 9 && hour < 17) return "phase.day";
            if (hour >= 17 && hour < 21.5) return "phase.evening";
            return "phase.night";
        }

        public static HudView Build(Simulation sim, Loc loc)
        {
            var gd = sim.Gd;
            var s = sim.S;
            var v = new HudView
            {
                Cash = Fmt.MoneyShort(s.Cash, loc.Lang),
                Rating = Fmt.Rating(s.RatingX100, loc.Lang),
                Speed = s.Speed + "×",
                Live = s.Phase == "live",
                Paused = s.Paused,
                Goal = Goals.Line(sim, loc),
            };
            var g = sim.Grid;
            if (s.Phase == "live" && g != null)
            {
                v.Clock = Fmt.Clock(g.HourOfDay);
                v.Phase = loc.T(PhaseKey(g.HourOfDay));
                v.ShiftProgress = g.Steps > 0 ? (float)g.Step / g.Steps : 0f;
                v.NeedlePm = g.NeedlePm;
                v.Band = Band(g.NeedlePm, gd);
                v.NeedleDeg = Deg(g.NeedlePm);
                long gap = (long)g.LastSupply - g.LastDemand;
                if (v.Band == NeedleBand.Green) { v.NeedleLabel = loc.T("hud.balanced"); v.NeedleIcon = "check"; }
                else if (gap < 0) { v.NeedleLabel = loc.F("hud.short", "mw", Fmt.Mw(-gap, loc.Lang)); v.NeedleIcon = "down"; }
                else { v.NeedleLabel = loc.F("hud.surplus", "mw", Fmt.Mw(gap, loc.Lang)); v.NeedleIcon = "up"; }
            }
            else
            {
                v.Clock = s.Phase == "report" ? loc.T("phase.report") : loc.F("hud.shift_n", "n", s.ShiftNo + 1);
                v.Phase = s.Phase == "report" ? "" : loc.T("phase.idle");
                v.Band = NeedleBand.Green;
                v.NeedleLabel = loc.T(s.Phase == "report" ? "hud.report_ready" : "hud.between");
                v.NeedleIcon = "check";
            }
            return v;
        }

        /// <summary>One-line explainer under the needle (coach step 2 opens it).</summary>
        public static string Explainer(Simulation sim, Loc loc)
        {
            var g = sim.Grid;
            if (g == null) return loc.T("needle.explain.idle");
            return loc.F("needle.explain", "supply", Fmt.Mw(g.LastSupply, loc.Lang), "demand", Fmt.Mw(g.LastDemand, loc.Lang));
        }
    }

    /// <summary>The goal line + the Goals sheet.</summary>
    public static class Goals
    {
        /// <summary>Next district to hook up (hookup order), or "" when all are connected.</summary>
        public static string NextDistrict(Simulation sim)
        {
            foreach (var d in sim.Gd.Districts) if (!sim.S.IsHooked(d.Id)) return d.Id;
            return "";
        }

        public static string Line(Simulation sim, Loc loc)
        {
            var gd = sim.Gd;
            var s = sim.S;
            // the 30-second hook's follow-up (DESIGN section 4): first the Riverside line upgrade
            if (s.ShiftNo >= 1 && !s.Owned.ContainsKey("line_riverside_1") && gd.Investment.TryGetValue("line_riverside_1", out var first))
                return loc.F("goal.invest", "name", Names.Investment(loc, gd, first), "cost", Fmt.Money(first.Cost, loc.Lang));
            string next = NextDistrict(sim);
            if (next != "")
            {
                var d = gd.District[next];
                return loc.F("goal.hookup", "district", Names.District(loc, next), "rating", Fmt.Rating((int)Math.Round(d.RatingReq * 100), loc.Lang),
                    "cost", Fmt.Money(d.Hookup, loc.Lang));
            }
            var ch = gd.Chapters[gd.Chapters.Count - 1];
            if (s.Ch5Done < 0)
                return loc.F("goal.final", "n", Math.Max(0, ch.GoalGoodShifts - s.Ch5Good), "rating", Fmt.Rating((int)Math.Round(ch.GoalRating * 100), loc.Lang));
            return loc.T("goal.endless");
        }

        /// <summary>Goals sheet: chapter, next hookup, cheapest affordable investment, 3-star shifts.</summary>
        public static TabView Sheet(Simulation sim, Loc loc)
        {
            var gd = sim.Gd;
            var s = sim.S;
            var t = new TabView { Id = "goals", Title = loc.T("goals.title"), Sub = loc.F("goals.sub", "n", s.Chapter), Icon = "flag" };
            t.Add(new Row { Id = "goal.chapter", Title = loc.T("chapter." + s.Chapter + ".t"), Sub = loc.T("chapter." + s.Chapter + ".b"), Icon = "flag" });
            string next = NextDistrict(sim);
            if (next != "")
            {
                var d = gd.District[next];
                string why = Economy.HookupWhyNot(sim, next);
                var r = t.Add(new Row
                {
                    Id = "goal.hookup", Title = loc.F("goals.hookup", "district", Names.District(loc, next)),
                    Sub = loc.F("goals.hookup_sub", "rating", Fmt.Rating((int)Math.Round(d.RatingReq * 100), loc.Lang), "now", Fmt.Rating(s.RatingX100, loc.Lang)),
                    Icon = "city", Open = "card:district:" + next, Target = "goals.hookup",
                });
                r.Act("hookup:" + next, Fmt.Money(d.Hookup, loc.Lang), why == "", "primary", "goals.hookup.pill");
            }
            var buy = Economy.BuyableNow(sim);
            foreach (var b in buy)
            {
                if (b.Kind != "investment") continue;
                var x = gd.Investment[b.Id];
                t.Add(new Row { Id = "goal.buy", Title = loc.F("goals.buy", "name", Names.Investment(loc, gd, x)), Sub = Names.InvestmentDesc(loc, gd, x), Icon = "build" })
                    .Act("buy:" + x.Id, Fmt.Money(x.Cost, loc.Lang));
                break;
            }
            t.Add(new Row { Id = "goal.stars", Title = loc.T("goals.stars"), Sub = loc.F("goals.stars_sub", "n", s.Stats.Stars3, "shifts", s.Stats.Shifts), Icon = "star" });
            return t;
        }
    }
}
