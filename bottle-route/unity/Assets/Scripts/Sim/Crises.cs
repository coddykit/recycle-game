// Bottle Route sim core — crises (design §7). Short (≤ 2 days), never game over; never during the guide or the first
// forgiving days. Day-wide ones (heat wave, match day, fuel spike, bale crash) are announced in the morning plan so the
// player can prepare (pre-position a truck at the stadium); the others land at a random minute of the live day.
using System;
using System.Collections.Generic;

namespace BottleRoute.Sim
{
    public sealed partial class Simulation
    {
        static readonly HashSet<string> Announced = new HashSet<string> { "heat_wave", "match_day", "fuel_spike", "bale_crash" };

        bool EventEligible(EventDef e)
        {
            if (S.Day < e.MinDay) return false;
            if (!string.IsNullOrEmpty(e.District) && !Unlocked(e.District)) return false;
            if (S.Events.Exists(a => a.Id == e.Id)) return false;
            if (S.Planned.Exists(a => a.Id == e.Id)) return false;
            return true;
        }

        void PlanDay()
        {
            S.Planned.Clear();
            if (S.Tutorial || S.Day <= Gd.EventFreeDays) return;
            double expected = Gd.EventsPerDay[Math.Min(Chapter, Gd.EventsPerDay.Length - 1)];
            int n = Sample(expected);
            for (int k = 0; k < n; k++)
            {
                var pool = Gd.Events.FindAll(EventEligible);
                if (pool.Count == 0) break;
                var w = new double[pool.Count];
                for (int i = 0; i < w.Length; i++) w[i] = EventWeight(pool[i]);
                var e = pool[Rng.PickWeighted(w)];
                if (Announced.Contains(e.Id)) Fire(e.Id);
                else S.Planned.Add(new PlannedEvent { Id = e.Id, At = Gd.DayStart + 60 + Rng.Frand() * (Gd.DayMinutes - 120) });
            }
        }

        void RunPlanned()
        {
            for (int i = S.Planned.Count - 1; i >= 0; i--)
            {
                if (S.Planned[i].At > S.Minute) continue;
                string id = S.Planned[i].Id;
                S.Planned.RemoveAt(i);
                Fire(id);
            }
        }

        EventDef Def(string id) => Gd.Events.Find(e => e.Id == id) ?? new EventDef { Id = id, Mult = 1, Days = 1 };

        int RandomPoint(Predicate<PointState> ok)
        {
            var idx = new List<int>();
            for (int i = 0; i < S.Points.Count; i++) if (ok(S.Points[i])) idx.Add(i);
            return idx.Count == 0 ? -1 : idx[Rng.RangeI(0, idx.Count - 1)];
        }

        /// <summary>Applies a crisis now (also used by tests and the balance bot). Returns false when it found no target.</summary>
        public bool Fire(string id)
        {
            var e = Def(id);
            switch (id)
            {
                case "heat_wave":
                case "fuel_spike":
                    S.Events.Add(new ActiveEvent { Id = id, UntilDay = S.Day + Math.Max(1, e.Days) - 1, Mult = e.Mult });
                    Emit("crisis", -1, -1, e.Mult, id);
                    return true;
                case "bale_crash":
                {
                    int m = Rng.RangeI(0, Mats.N - 1);
                    S.Events.Add(new ActiveEvent { Id = id, UntilDay = S.Day + Math.Max(1, e.Days) - 1, Mult = e.Mult, Target = m });
                    Emit("crisis", m, -1, e.Mult, id);
                    return true;
                }
                case "match_day":
                    S.Events.Add(new ActiveEvent { Id = id, UntilDay = S.Day, Mult = e.Mult, From = e.From, To = e.To });
                    Emit("crisis", -1, -1, e.From, id);
                    return true;
                case "jam":
                {
                    int i = RandomPoint(p => p.IsMachine && !p.Jammed);
                    if (i < 0) return false;
                    Jam(i, "crisis");
                    Emit("crisis", i, -1, 0, id);
                    return true;
                }
                case "sensor_offline":
                {
                    int i = RandomPoint(p => p.IsMachine && !p.SensorOffline && !(Has("sensor_redundancy") && p.Sensor));
                    if (i < 0) return false;
                    var p = S.Points[i];
                    p.SensorOffline = true;
                    p.FrozenPct = Math.Min(0.4, DisplayPct(p));   // "the app says available"
                    Emit("crisis", i, -1, 0, id);
                    return true;
                }
                case "bulk_returner":
                {
                    int i = RandomPoint(p => p.IsMachine && !p.Jammed);
                    if (i < 0) return false;
                    int n = Rng.RangeI(e.Min, e.Max);
                    if (S.Depot.Centre)
                    {
                        // the counting centre takes big hauls: no blocked machine, it counts them itself at the lower
                        // bulk fee and sends the sorted material straight to the baler / glass bay
                        double[] split = { Math.Round(n * 0.6), Math.Round(n * 0.3), 0 };
                        split[Mats.Glass] = n - split[Mats.Pet] - split[Mats.Alu];
                        for (int m = 0; m < Mats.N; m++) S.Depot.BalerIn[m] += split[m] * Gd.Materials[m].Kg;
                        double fee = n * Gd.BulkFee * FeeMult;
                        S.Cash += fee;
                        S.Today.Fees += fee;
                        Emit("crisis", i, -1, n, "bulk_centre");
                        return true;
                    }
                    Deposit(i, n, new[] { 0.6, 0.3, 0.1 }, -1);
                    S.Points[i].BusyUntil = S.Minute + e.BusyMin;
                    Emit("crisis", i, -1, n, id);
                    return true;
                }
                case "breakdown":
                {
                    int ti = S.Trucks.FindIndex(t => t.Status == TruckStatus.Drive);
                    if (ti < 0) return false;
                    Breakdown(ti, e.BrokenMin);
                    return true;
                }
                case "baler_down":
                    if (S.Depot.BalerDown) return false;
                    BalerDown("crisis");
                    Emit("crisis", -1, -1, 0, id);
                    return true;
                case "glass_break":
                {
                    int ti = S.Trucks.FindIndex(t => t.Load[Mats.Glass] > 0);
                    if (ti < 0) return false;
                    GlassBreak(ti);
                    return true;
                }
            }
            return false;
        }

        void Breakdown(int ti, double minutes)
        {
            var t = S.Trucks[ti];
            if (t.Status == TruckStatus.Broken) return;
            t.ResumeStatus = t.Status;
            t.Status = TruckStatus.Broken;
            t.TimeLeft = minutes;
            Emit("crisis", ti, -1, minutes, "breakdown");
        }

        void GlassBreak(int ti)
        {
            var t = S.Trucks[ti];
            double lost = t.Load[Mats.Glass];
            t.Load[Mats.Glass] = 0;
            Emit("crisis", ti, -1, lost, "glass_break");
        }

        /// <summary>Crises active today (for the morning plan card and the HUD chips).</summary>
        public List<string> ActiveCrises()
        {
            var o = new List<string>();
            if (InSeason) o.Add("season");
            foreach (var e in S.Events) o.Add(e.Id);
            if (S.Depot.BalerDown) o.Add("baler_down");
            foreach (var p in S.Points) { if (p.Jammed) { o.Add("jam"); break; } }
            foreach (var t in S.Trucks) { if (t.Status == TruckStatus.Broken) { o.Add("breakdown"); break; } }
            return o;
        }
    }
}
