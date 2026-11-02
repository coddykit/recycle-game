// Charge Nurse sim core — headless balance bot (plan §15 P1: 30 min / 2 h / 7 days, no wall). Plays like a sensible new
// player who does not micro-manage: answers ambulances, hands over in the window, and between shifts spends on what the
// bottleneck lens named most often today (the right team, beds, bays, a technician), opens rooms / wings / upgrades when
// it can keep a buffer. Deterministic per seed.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ChargeNurse.Sim
{
    public sealed class BalanceReport
    {
        public readonly List<DayStats> Days = new List<DayStats>();
        public double Cash, Reputation, MinCash = double.MaxValue;
        public int Chapter, Beds, Staff, ResearchDone;
        public bool Finite = true;
        public readonly List<string> Log = new List<string>();

        public string Table()
        {
            var sb = new StringBuilder();
            sb.AppendLine("day  arr treat bad  wait blk%   fees  ward  costs    net stars");
            foreach (var d in Days)
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0,3} {1,4} {2,5} {3,3} {4,5:0} {5,4:0} {6,6:0} {7,5:0} {8,6:0} {9,6:0}   {10}/9",
                    d.Day, d.Arrivals, d.Treated, d.Bad, d.AvgWait, d.BlockedShare * 100, d.Fees, d.Ward, d.Costs, d.Net, d.Stars));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "cash {0:0} reputation {1:0.0} chapter {2} beds {3} staff {4} upgrades {5} minCash {6:0}",
                Cash, Reputation, Chapter, Beds, Staff, ResearchDone, MinCash));
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
            rep.Cash = sim.S.Cash; rep.Reputation = sim.S.Reputation; rep.Chapter = sim.Chapter; rep.Beds = sim.S.Beds.Count;
            foreach (var kv in sim.S.Teams) rep.Staff += kv.Value;
            foreach (var r in sim.S.Rooms) rep.Staff += r.Techs;
            rep.ResearchDone = sim.S.Research.Count;
            return rep;
        }

        public static void PlayDay(Simulation sim, BalanceReport rep)
        {
            var seen = new Dictionary<string, int>();
            Spend(sim, rep, seen);
            sim.StartDay();
            int guard = 0;
            while (sim.Phase == Phases.Live && guard++ < 100000)
            {
                sim.AdvanceMinutes(5);
                sim.DrainEvents();
                if (sim.Phase != Phases.Live) break;
                if (sim.S.Ambulance != null) sim.DecideAmbulance(sim.EdLoad() < 1.3);
                var b = sim.FindBottleneck();
                if (b.Key != "smooth") seen[b.Key + "|" + Detail(sim, b)] = (seen.TryGetValue(b.Key + "|" + Detail(sim, b), out int n) ? n : 0) + 1;
                if (sim.SurgeDay && !sim.S.Surge) Try(sim, rep, "surge", 0);
                if (sim.HandoverWindow) sim.Handover();
                if (double.IsNaN(sim.S.Cash) || double.IsInfinity(sim.S.Cash)) rep.Finite = false;
                rep.MinCash = Math.Min(rep.MinCash, sim.S.Cash);
            }
            sim.DrainEvents();
            Spend(sim, rep, seen);
        }

        static string Detail(Simulation sim, Bottleneck b)
        {
            if (b.Key == "beds" && b.Zone >= 0) return sim.Gd.Zones[b.Zone].Ward;
            if (b.Key == "tests" && b.Zone >= 0) return sim.Gd.Zones[b.Zone].Test;
            return "";
        }

        static bool Try(Simulation sim, BalanceReport rep, string id, double buffer)
        {
            var c = sim.CanBuy(id);
            if (!c.Ok || sim.S.Cash - c.Cost < buffer) return false;
            sim.Buy(id);
            rep.Log.Add("day " + sim.S.Day + " " + id);
            return true;
        }

        static void Spend(Simulation sim, BalanceReport rep, Dictionary<string, int> seen)
        {
            var s = sim.S;
            double buffer = Math.Max(500, sim.DayIncome() * 0.4);
            // the chapter goal first, then the rooms a new player opens on the way
            if (sim.SurgeDay) Try(sim, rep, "surge", 0);
            foreach (var w in sim.Gd.Wings) Try(sim, rep, "wing:" + w.Id, buffer);
            Try(sim, rep, "room:xray", buffer * 0.5);
            Try(sim, rep, "room:lab", buffer * 1.5);
            if (sim.Unlocked("city")) { Try(sim, rep, "room:ct", buffer * 1.5); Try(sim, rep, "room:or", buffer * 2); }
            // what blocked the flow most today
            var keys = new List<KeyValuePair<string, int>>(seen);
            keys.Sort((a, b) => b.Value.CompareTo(a.Value));
            int bought = 0;
            foreach (var kv in keys)
            {
                if (bought >= 3 || kv.Value < 4) break;
                var parts = kv.Key.Split('|');
                string id = parts[0] switch
                {
                    "discharge" => "hire:nurse", "triage" => "hire:nurse", "dirty" => "hire:cleaner", "porters" => "hire:porter",
                    "doctors" => "hire:doctor", "reception" => "hire:clerk", "bays" => "bay",
                    "beds" => "bed:" + (parts[1] != "" ? parts[1] : "obs"), "tests" => "tech:" + parts[1], _ => "",
                };
                if (id == "bay" && sim.CanBuy("bay").Reason == "max") id = "hire:doctor";
                if (id == "bed:obs" && sim.CanBuy(id).Reason == "max") id = sim.Unlocked("town") ? "bed:general" : "hire:nurse";
                if (id != "" && Try(sim, rep, id, buffer)) bought++;
            }
            if (s.Researching == "")
                foreach (var r in sim.Gd.Research)
                    if (sim.CanResearch(r.Id) == "" && s.Cash - r.Cost > buffer * 1.3) { sim.StartResearch(r.Id); rep.Log.Add("day " + s.Day + " upgrade " + r.Id); break; }
        }
    }
}
