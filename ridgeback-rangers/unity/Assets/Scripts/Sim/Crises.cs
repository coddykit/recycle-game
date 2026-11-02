// Ridgeback Rangers sim core — island events (plan §7) and the daily Ranger Call (2-4 small pins at random minutes, the
// retention lever of plan §6). Events are short (one day), never game over, never during the guide or the forgiving first
// days. Day-wide ones (heat wave, school trip, supply boat late, dust storm) are announced on the morning plan so the player
// can prepare (more crates, a water point, a second tram car); a rain squall lands at a random minute of the live day.
using System;
using System.Collections.Generic;

namespace RidgebackRangers.Sim
{
    public sealed partial class Simulation
    {
        static readonly HashSet<string> Announced = new HashSet<string> { "heat_wave", "school_trip", "supply_late", "dust_storm" };

        bool EventEligible(EventDef e)
        {
            if (S.Day < e.MinDay) return false;
            if (!string.IsNullOrEmpty(e.Zone) && !Unlocked(e.Zone)) return false;
            if (S.Events.Exists(a => a.Id == e.Id)) return false;
            if (S.Planned.Exists(a => a.Id == e.Id && !a.Call)) return false;
            return true;
        }

        void PlanDay()
        {
            S.Planned.Clear();
            if (S.Tutorial) return;
            // the daily Ranger Call
            int calls = Gd.CallsPerDay[Math.Min(Chapter - 1, Gd.CallsPerDay.Length - 1)];
            for (int k = 0; k < calls && Gd.CallKinds.Count > 0; k++)
                S.Planned.Add(new PlannedEvent { Id = Gd.CallKinds[Rng.RangeI(0, Gd.CallKinds.Count - 1)], Call = true, At = Gd.DayStart + 30 + Rng.Frand() * (Gd.DayMinutes - 150) });
            if (S.Day <= Gd.EventFreeDays) return;
            double expected = Gd.EventsPerDay[Math.Min(Chapter, Gd.EventsPerDay.Length - 1)];
            int n = Sample(expected);
            for (int k = 0; k < n; k++)
            {
                var pool = Gd.Events.FindAll(EventEligible);
                if (pool.Count == 0) break;
                var w = new double[pool.Count];
                for (int i = 0; i < w.Length; i++) w[i] = pool[i].Weight;
                var e = pool[Rng.PickWeighted(w)];
                if (Announced.Contains(e.Id)) Fire(e.Id);
                else S.Planned.Add(new PlannedEvent { Id = e.Id, At = Gd.DayStart + 60 + Rng.Frand() * (Gd.DayMinutes - 180) });
            }
        }

        void RunPlanned()
        {
            for (int i = S.Planned.Count - 1; i >= 0; i--)
            {
                if (S.Planned[i].At > S.Minute) continue;
                var pe = S.Planned[i];
                S.Planned.RemoveAt(i);
                if (pe.Call) { int id = ForcePin(pe.Id); var p = Pin(id); if (p != null) p.Call = true; }
                else Fire(pe.Id);
            }
        }

        void ExpireEvents()
        {
            for (int i = S.Events.Count - 1; i >= 0; i--)
            {
                var e = S.Events[i];
                if (e.Until <= 0 || S.Minute < e.Until) continue;
                S.Events.RemoveAt(i);
                if (e.Id == "rain_squall") S.Pins.RemoveAll(p => p.Kind == PinKinds.Flood && p.Ranger < 0);   // the water drains away
                Emit("event_end", -1, -1, 0, e.Id);
            }
        }

        EventDef Def(string id) => Gd.Events.Find(e => e.Id == id) ?? new EventDef { Id = id, Mult = 1, Days = 1, Min = 1, Max = 1, BusyMin = 120 };

        /// <summary>Applies an island event now (also used by tests and the balance bot). False when it found nothing to hit.</summary>
        public bool Fire(string id)
        {
            var e = Def(id);
            switch (id)
            {
                case "heat_wave":
                case "school_trip":
                case "supply_late":
                case "dust_storm":
                    S.Events.Add(new ActiveEvent { Id = id, UntilDay = S.Day + Math.Max(1, e.Days) - 1, Mult = e.Mult, Zone = e.Zone ?? "" });
                    Emit("crisis", -1, -1, e.Mult, id);
                    return true;
                case "rain_squall":
                {
                    if (S.Phase != Phases.Live) return false;
                    S.Events.Add(new ActiveEvent { Id = id, UntilDay = S.Day, Mult = e.Mult, Until = S.Minute + e.BusyMin });
                    int n = Rng.RangeI(Math.Max(1, e.Min), Math.Max(e.Min, e.Max));
                    var zones = new List<string>(S.Zones);
                    for (int k = 0; k < n && zones.Count > 0; k++)
                    {
                        int zi = Rng.RangeI(0, zones.Count - 1);
                        int p = S.Paddocks.FindIndex(pd => ZoneOf(pd) == zones[zi]);
                        if (p >= 0) OpenPin(PinKinds.Flood, p, -1);
                        zones.RemoveAt(zi);
                    }
                    Emit("crisis", -1, -1, n, id);
                    return true;
                }
            }
            return false;
        }

        /// <summary>Events active today (for the morning plan card and the HUD chips).</summary>
        public List<string> ActiveCrises()
        {
            var o = new List<string>();
            foreach (var e in S.Events) if (e.Until <= 0 || S.Minute < e.Until) o.Add(e.Id);
            if (TramHalted) o.Add("escape");
            return o;
        }
    }
}
