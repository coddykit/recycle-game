// Charge Nurse sim core — headless balance bot (design §15 P1: 30 min / 2 h / 7 days). Plays like a sensible new player:
// suggested routes for idle trucks, sells bales when the yard fills or the price is good, banks the till, sends
// technicians, buys machines / upgrades / research / districts when it can afford a buffer. Deterministic per seed.
using System;
using System.Collections.Generic;
using System.Text;

namespace ChargeNurse.Sim
{
    public sealed class BalanceReport
    {
        public readonly List<DayStats> Days = new List<DayStats>();
        public double Cash, Rating;
        public int Chapter, Points, Trucks, ResearchDone;
        public double MinCash = double.MaxValue;
        public bool Finite = true;
        public readonly List<string> Log = new List<string>();

        public string Table()
        {
            var sb = new StringBuilder();
            sb.AppendLine("day  arr  turned%  coll   km   perKm  fees  sales  costs   net   stars  rating");
            foreach (var d in Days)
                sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{0,3} {1,5} {2,7:0.0} {3,5} {4,5:0} {5,6:0.0} {6,5:0} {7,6:0} {8,6:0} {9,6:0}   {10}/9",
                    d.Day, d.Arrivals, d.TurnedRate * 100, d.Collected, d.Km, d.PerKm, d.Fees, d.Sales, d.Costs, d.Net, d.Stars));
            sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "cash {0:0} rating {1:0.0} chapter {2} points {3} trucks {4} research {5} minCash {6:0}",
                Cash, Rating, Chapter, Points, Trucks, ResearchDone, MinCash));
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
                if (sim.S.Day >= days) break;
                sim.NextDay();
            }
            rep.Cash = sim.S.Cash; rep.Rating = sim.S.Rating; rep.Chapter = sim.Chapter; rep.Points = sim.S.Points.Count;
            rep.Trucks = sim.S.Trucks.Count; rep.ResearchDone = sim.S.Research.Count;
            return rep;
        }

        public static void PlayDay(Simulation sim, BalanceReport rep)
        {
            Manage(sim, rep, true);
            sim.StartDay();
            int guard = 0;
            while (sim.Phase == Phases.Live && guard++ < 100000)
            {
                sim.AdvanceMinutes(5);
                sim.AdvanceResearch(5 / sim.Gd.GameMinPerRealS);   // research runs on real time: 5 game min = 2 s at 1×
                sim.DrainEvents();
                Manage(sim, rep, false);
                if (double.IsNaN(sim.S.Cash) || double.IsInfinity(sim.S.Cash)) rep.Finite = false;
                rep.MinCash = Math.Min(rep.MinCash, sim.S.Cash);
            }
            sim.DrainEvents();
            Manage(sim, rep, true);
        }

        static void Manage(Simulation sim, BalanceReport rep, bool calm)
        {
            var s = sim.S;
            // dispatch idle trucks
            for (int ti = 0; ti < s.Trucks.Count; ti++)
            {
                if (s.Trucks[ti].Busy || s.Trucks[ti].Auto || sim.FreeDrivers <= 0 || s.Phase == Phases.Report) continue;
                var r = sim.SuggestRoute(ti);
                if (r.Count == 0 && s.Phase == Phases.Live) r = sim.SuggestRoute(ti, true);
                if (r.Count > 0) sim.Dispatch(ti, r);
            }
            // technicians
            for (int i = 0; i < s.Points.Count; i++) if (s.Points[i].Jammed && !s.Points[i].TechCalled) sim.SendTech("point", i);
            if (s.Depot.BalerDown) sim.SendTech("baler", 0);
            // till + bales
            if (s.Depot.Till > 200) sim.CollectTill();
            for (int m = 0; m < Mats.N; m++)
                if (s.Depot.Bales[m] > 0 && (sim.MarketMult(m) >= 1.05 || s.Depot.Yard >= s.Depot.YardCap - 2 || (m == Mats.Glass && sim.GlassBayFull))) sim.Sell(m);
            if (!calm) return;
            double buffer = Math.Max(800, sim.DayIncome() * 0.8);
            // districts first (the chapter goal), then research, then the network
            foreach (var d in sim.Gd.Districts) if (sim.CanBuy("district:" + d.Id).Ok && s.Cash - d.Cost > buffer) { sim.Buy("district:" + d.Id); rep.Log.Add("day " + s.Day + " district " + d.Id); }
            if (s.Researching == "")
                foreach (var r in sim.Gd.Research)
                    if (sim.CanResearch(r.Id) == "" && s.Cash - r.Cost > buffer * 1.5) { sim.StartResearch(r.Id); rep.Log.Add("day " + s.Day + " research " + r.Id); break; }
            if (sim.CanBuy("dispatcher").Ok && s.Cash - sim.Gd.Price("dispatcher") > buffer) sim.Buy("dispatcher");
            if (s.Drivers < s.Trucks.Count && sim.CanBuy("driver").Ok && s.Cash - 300 > buffer) sim.Buy("driver");
            // overflowing points: bigger bin, then a new machine nearby
            for (int i = 0; i < s.Points.Count; i++)
            {
                var p = s.Points[i];
                if (p.Turned > 8 && sim.CanBuy("bin_m:" + i).Ok && s.Cash - 700 > buffer) sim.Buy("bin_m:" + i);
                if (p.Turned > 8 && sim.CanBuy("sensor:" + i).Ok && s.Cash - 250 > buffer) sim.Buy("sensor:" + i);
            }
            foreach (var n in sim.Gd.Nodes)
            {
                if (n.Kind != "site") continue;
                string id = (n.Site == "machine" ? "machine:" : n.Site + ":") + n.Id;
                var c = sim.CanBuy(id);
                if (c.Ok && s.Cash - c.Cost > buffer * 1.5) { sim.Buy(id); rep.Log.Add("day " + s.Day + " bought " + id); }
            }
            if (s.Depot.IntakeTotal > sim.CountPerMinute * 120 && sim.CanBuy("line").Ok && s.Cash - sim.Gd.Price("line") > buffer) sim.Buy("line");
            int pointsPerTruck = s.Points.Count / Math.Max(1, s.Trucks.Count);
            if (pointsPerTruck > 4 && sim.CanBuy("truck:van").Ok && s.Cash - 2500 > buffer * 2) sim.Buy("truck:van");
            if (s.Techs == 0 && s.Day > 3 && sim.CanBuy("tech").Ok && s.Cash - 300 > buffer) sim.Buy("tech");
        }
    }
}
