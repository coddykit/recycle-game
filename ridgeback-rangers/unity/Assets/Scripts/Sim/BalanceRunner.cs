// Ridgeback Rangers sim core — headless balance bot (plan §15 P1). Plays like a sensible new player: feeds by the board's
// suggestion, sends the nearest free ranger to the most urgent pin, digs when a Handler can be spared, checks the incubator
// temperature, rehomes hatchlings, and buys paddocks / zones / rangers / tram cars when it keeps a buffer. Deterministic per seed.
using System;
using System.Collections.Generic;
using System.Text;

namespace RidgebackRangers.Sim
{
    public sealed class BalanceReport
    {
        public readonly List<DayStats> Days = new List<DayStats>();
        public double Cash, Cp, Rating;
        public int Chapter, Paddocks, Animals, Species, Rangers;
        public double MinCash = double.MaxValue;
        public bool Finite = true;
        public readonly List<string> Log = new List<string>();

        public string Table()
        {
            var sb = new StringBuilder();
            sb.AppendLine("day  visit  rides walk%  pins ok/fail  tickets  other  costs    net  stars   cp");
            foreach (var d in Days)
                sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{0,3} {1,6} {2,6} {3,5:0.0} {4,5} {5,3}/{6,-3} {7,8:0} {8,6:0} {9,6:0} {10,6:0}   {11}/9 {12,4:0}",
                    d.Day, d.Visitors, d.Rides, d.Visitors <= 0 ? 0 : 100.0 * d.Walked / d.Visitors, d.PinsOpened, d.PinsResolved, d.PinsFailed,
                    d.Tickets, d.Income - d.Tickets, d.Costs, d.Net, d.Stars, d.CpEarned));
            sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "cash {0:0} cp {1:0} rating {2:0.0} chapter {3} paddocks {4} animals {5} species {6} rangers {7} minCash {8:0}",
                Cash, Cp, Rating, Chapter, Paddocks, Animals, Species, Rangers, MinCash));
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
            rep.Cash = sim.S.Cash; rep.Cp = sim.S.Cp; rep.Rating = sim.S.Rating; rep.Chapter = sim.Chapter; rep.Paddocks = sim.S.Paddocks.Count;
            rep.Animals = sim.S.Animals.Count; rep.Species = sim.S.Found.Count; rep.Rangers = sim.S.Rangers.Count;
            return rep;
        }

        public static void PlayDay(Simulation sim, BalanceReport rep)
        {
            Plan(sim, rep);
            sim.StartDay();
            int guard = 0;
            while (sim.Phase == Phases.Live && guard++ < 100000)
            {
                sim.AdvanceMinutes(3);
                sim.DrainEvents();
                Live(sim, 3 / sim.Gd.GameMinPerRealS);
                if (double.IsNaN(sim.S.Cash) || double.IsInfinity(sim.S.Cash)) rep.Finite = false;
                rep.MinCash = Math.Min(rep.MinCash, sim.S.Cash);
            }
            sim.DrainEvents();
        }

        static void Live(Simulation sim, double realS)
        {
            var s = sim.S;
            // the incubator runs on real time: 3 game minutes = ~1.4 s at 1x
            for (int i = 0; i < s.Eggs.Count; i++) if (!s.Eggs[i].Checked) sim.CheckTemp(i, 50);
            sim.Advance(Math.Min(1.0, realS));
            // the most urgent pin first, nearest free ranger of its role
            var pins = new List<PinState>(s.Pins);
            pins.Sort((a, b) => a.Deadline.CompareTo(b.Deadline));
            foreach (var p in pins)
            {
                if (p.Ranger >= 0) continue;
                int r = sim.FreeRanger(sim.RoleFor(p.Kind), p.Node);
                if (r >= 0) sim.Send(p.Id, r);
            }
            // dig when a Handler can be spared
            if (sim.FreeCount(Roles.Handler) >= 2 || (sim.FreeCount(Roles.Handler) >= 1 && !s.Pins.Exists(p => sim.RoleFor(p.Kind) == Roles.Handler)))
                for (int d = sim.Gd.DigSites.Count - 1; d >= 0; d--)
                    if (sim.CanDig(d) == "") { sim.Dig(d); break; }
            // hatchlings in the nursery get a home
            for (int i = 0; i < s.Animals.Count; i++)
                if (s.Animals[i].Paddock < 0) { int p = sim.SuggestPaddock(s.Animals[i].Species); if (p >= 0) sim.MoveAnimal(i, p); }
        }

        static void Plan(Simulation sim, BalanceReport rep)
        {
            var s = sim.S;
            foreach (var z in s.Zones) sim.SetFeedPlan(z, sim.FeedNeed(z) + 2);
            double buffer = Math.Max(1200, sim.DayIncome() * 0.6);
            bool Try(string id, double extra = 0)
            {
                var c = sim.CanBuy(id);
                if (!c.Ok || s.Cash - c.Cost < buffer + extra) return false;
                sim.Buy(id);
                rep.Log.Add("day " + s.Day + " " + id);
                return true;
            }
            foreach (var z in sim.Gd.Zones) Try("zone:" + z.Id);
            // paddocks in open zones, more when the nursery holds animals
            foreach (var n in sim.Gd.Nodes) if (n.Kind == "paddock" && sim.Unlocked(n.Zone)) Try("paddock:" + n.Id);
            var last = s.History.Count > 0 ? s.History[s.History.Count - 1] : null;
            if (last != null)
            {
                foreach (var k in last.Failed) Try("hire:" + sim.RoleFor(k));
                if (last.Visitors > 0 && last.Walked > last.Visitors * 0.08) Try("tram_car");
            }
            for (int p = 0; p < s.Paddocks.Count; p++)
            {
                if (sim.FenceTooWeak(p) || s.Paddocks[p].Integrity < 0.7) Try("fence:" + p);
                if (sim.BiomeOf(s.Paddocks[p]) == "dune") { Try("water:" + p); Try("shade:" + p); }
                if (sim.SlotsUsed(p) >= sim.PaddockSlots(p) - 1 && s.Animals.Exists(a => a.Paddock < 0)) Try("expand:" + p);
            }
            Try("cafe", 500);
            Try("gift", 1000);
            if (s.EggsWaiting.Count > 0) Try("incubator");
            if (s.Day >= 4) Try("hire:" + Roles.Handler, 800);
            Try("chief", 2000);
        }
    }
}
