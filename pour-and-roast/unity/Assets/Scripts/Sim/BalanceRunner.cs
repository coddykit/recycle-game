// Pour & Roast sim core — headless balance bot (design §15 P1: 30 min / 2 h / 7 days). Plays like a sensible new player:
// deadline-first rule + batching, dials the grinder in when the shots go bitter, fixes a jam, sizes the milk order from
// yesterday's demand, hires when customers walk out, buys equipment / research / the next venue when it keeps a cash
// buffer; in the roastery it buys green lots, roasts (taps Drop in the sweet spot), blends and signs contracts.
// Deterministic per seed.
using System;
using System.Collections.Generic;
using System.Text;

namespace PourAndRoast.Sim
{
    public sealed class BalanceReport
    {
        public readonly List<DayStats> Days = new List<DayStats>();
        public readonly List<double> CashEnd = new List<double>();
        public readonly List<double> RatingEnd = new List<double>();
        public double Cash, Rating, BeanQ;
        public int Chapter, Staff, ResearchDone, Venues;
        public double MinCash = double.MaxValue;
        public bool Finite = true;
        public int StuckTickets;
        public readonly List<string> Log = new List<string>();

        public string Table()
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("day venue    wx     cust served walk%  items  revenue  costs    net  review rating   cash");
            for (int i = 0; i < Days.Count; i++)
            {
                var d = Days[i];
                sb.AppendLine(string.Format(ci, "{0,3} {1,-8} {2,-6} {3,4} {4,6} {5,5:0.0} {6,6} {7,8:0} {8,6:0} {9,6:0} {10,6:0.00} {11,6:0.00} {12,6:0}",
                    d.Day, d.Venue, d.Weather, d.Customers, d.Served, d.WalkRate * 100, d.Items, d.Revenue + d.Tips, d.Costs, d.Net, d.AvgReview,
                    i < RatingEnd.Count ? RatingEnd[i] : 0, i < CashEnd.Count ? CashEnd[i] : 0));
            }
            sb.AppendLine(string.Format(ci, "cash {0:0} rating {1:0.00} chapter {2} venues {3} staff {4} research {5} beanQ {6:0} minCash {7:0}",
                Cash, Rating, Chapter, Venues, Staff, ResearchDone, BeanQ, MinCash));
            return sb.ToString();
        }
    }

    public static class BalanceRunner
    {
        public static BalanceReport Run(GameData gd, long seed, int days, bool skipTutorial = true, Action<Simulation> eachDay = null)
        {
            var sim = new Simulation(gd, seed) { EmitCrowd = false };
            if (skipTutorial) sim.EndTutorial();
            var rep = new BalanceReport();
            while (sim.S.Day <= days)
            {
                eachDay?.Invoke(sim);
                PlayDay(sim, rep);
                rep.Days.Add(sim.S.History[sim.S.History.Count - 1]);
                rep.CashEnd.Add(sim.S.Cash);
                rep.RatingEnd.Add(sim.S.Rating);
                if (sim.S.Day >= days) break;
                sim.NextDay();
            }
            rep.Cash = sim.S.Cash; rep.Rating = sim.S.Rating; rep.Chapter = sim.Chapter; rep.Staff = sim.S.Staff.Count;
            rep.ResearchDone = sim.S.Research.Count; rep.Venues = sim.S.Venues.Count; rep.BeanQ = sim.S.BeanQ;
            return rep;
        }

        public static void PlayDay(Simulation sim, BalanceReport rep)
        {
            Plan(sim, rep);
            sim.StartDay();
            int guard = 0;
            while (sim.Phase == Phases.Live && guard++ < 100000)
            {
                sim.AdvanceMinutes(2);
                sim.AdvanceResearch(2 / sim.Gd.GameMinPerRealS);   // research + roaster run on real time: 2 game min = 0.67 s at 1×
                sim.DrainEvents();
                Live(sim);
                foreach (var t in sim.S.Tickets) if (!t.Ready && sim.S.Minute - t.Arrived > 120) rep.StuckTickets++;
                if (double.IsNaN(sim.S.Cash) || double.IsInfinity(sim.S.Cash)) rep.Finite = false;
                rep.MinCash = Math.Min(rep.MinCash, sim.S.Cash);
            }
            sim.DrainEvents();
        }

        static void Live(Simulation sim)
        {
            var s = sim.S;
            if (sim.Venue.Tune < sim.Gd.TuneOff + 5) sim.DialIn();
            if (sim.Jammed) sim.Repair();
            if (sim.MilkLow && s.RunArriveAt < 0 && s.MilkArriveAt < 0 && s.Minute < sim.Gd.DayEnd - 120 && sim.CanBuy("milk_run").Ok && s.Cash > 400) sim.Buy("milk_run");
            if (sim.IceLow && sim.CanBuy("ice_bag").Ok && s.Cash > 300) sim.Buy("ice_bag");
            if (sim.InSweetSpot) sim.Drop();
            if (sim.RoasteryOpen && s.Roast == null) Roast(sim);
        }

        static void Roast(Simulation sim)
        {
            var s = sim.S;
            int best = -1; double bq = 0;
            for (int i = 0; i < sim.Gd.Origins.Count; i++)
            {
                var o = sim.Gd.Origins[i];
                if (!sim.OriginOpen(o)) continue;
                if (s.Green[i] < 1 && (sim.CanBuy("green:" + o.Id).Ok && s.Cash - sim.LotCost(i) > 1500)) sim.BuyGreen(o.Id);
                if (s.Green[i] >= 1 && o.Q > bq) { bq = o.Q; best = i; }
            }
            if (best < 0) return;
            var ob = sim.Gd.Origins[best];
            string prof = sim.ProfileOpen(ob.Best) ? ob.Best : Profiles.Medium;
            sim.StartRoast(ob.Id, prof);
        }

        static void Plan(Simulation sim, BalanceReport rep)
        {
            var s = sim.S;
            var gd = sim.Gd;
            sim.SetRule(Rules.Deadline);
            sim.SetBatch(true);
            sim.Blend();
            // milk sized from yesterday's demand
            if (s.History.Count > 0)
            {
                var y = s.History[s.History.Count - 1];
                sim.SetMilkOrder(0, Math.Max(4, y.MilkUsedW * 1.15 + 2 - s.Milk[0]));
                sim.SetMilkOrder(1, Math.Max(2, y.MilkUsedO * 1.15 + 1 - s.Milk[1]));
            }
            if (s.Forecast.Count > 0 && s.Forecast[0] == Weathers.Rain) sim.Prep("hot");
            if (s.Forecast.Count > 0 && s.Forecast[0] == Weathers.Heat && sim.Tier >= 2) sim.Prep("ice");
            double buffer = Math.Max(250, sim.DayIncome() * 0.6);
            var last = s.History.Count > 0 ? s.History[s.History.Count - 1] : null;
            // the next venue is the chapter goal
            foreach (var d in gd.Venues)
            {
                var c = sim.CanBuy("venue:" + d.Id);
                if (c.Ok && s.Cash - c.Cost > buffer) { sim.Buy("venue:" + d.Id); rep.Log.Add("day " + s.Day + " venue " + d.Id); }
            }
            bool Try(string id, double extra = 0)
            {
                var c = sim.CanBuy(id);
                if (!c.Ok || s.Cash - c.Cost < buffer + extra) return false;
                sim.Buy(id);
                rep.Log.Add("day " + s.Day + " " + id);
                return true;
            }
            int baristas = sim.CountRole(Roles.Barista, s.Venue);
            if (baristas < 2 || last != null && last.WalkRate > 0.05) Try("staff:barista");
            if (baristas >= 2 && sim.Venue.Heads < 2) Try("heads");
            if (last != null && last.WalkRate > 0.08) { if (!Try("heads")) Try("wands"); }
            if (last != null && last.Balked > 5) Try("rail");
            if (sim.Venue.Wear > 60) Try("service_grinder");
            Try("seats");
            if (s.Day >= 4) Try("staff:cashier");
            if (s.Researching == "")
                foreach (var r in gd.Research)
                    if (sim.CanResearch(r.Id) == "" && s.Cash - r.Cost > buffer * 1.3) { sim.StartResearch(r.Id); rep.Log.Add("day " + s.Day + " research " + r.Id); break; }
            Try("decor", buffer);
            Try("grinder", buffer);
            if (sim.Tier >= 2 && sim.Venue.Ice < 2) Try("ice");
            if (sim.RoasteryOpen)
            {
                Try("staff:roaster");
                foreach (var c in gd.Contracts) if (sim.CanSign(c.Id) == "" && s.BeanQ >= c.MinQ && s.BeanKg > c.Kg * 2) sim.ToggleContract(c.Id);
                Try("drum", buffer * 2);
                if (s.Roast == null) Roast(sim);
            }
        }
    }
}
