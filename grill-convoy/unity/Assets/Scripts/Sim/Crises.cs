// Grill Convoy sim core — crises (design §7, MVP set of 6): halftime stampede (match days at the stadium, on the
// calendar), rain and heat (the weather forecast, a day ahead), propane low, grease flare-up and a rival truck (announced in
// the morning plan). Short, telegraphed, never game over — the cost is lost sales, waste and pitch reputation; every one has
// a free in-game answer (all hands / second window, move the truck or shorten the menu, drink fridge, swap the bottle or
// restock run, extinguish / clean, counter-offer or move). None during the guide or the first forgiving days.
using System;
using System.Collections.Generic;

namespace GrillConvoy.Sim
{
    public sealed partial class Simulation
    {
        bool EventEligible(EventDef e)
        {
            if (e.Kind != "random" && e.Kind != "announced") return false;
            if (e.Weight <= 0 || S.Day < e.MinDay) return false;
            if (S.Events.Exists(a => a.Id == e.Id) || S.Planned.Exists(a => a.Id == e.Id)) return false;
            if (e.Id == "rival" && RivalPitch(e) < 0) return false;
            return true;
        }

        int RivalPitch(EventDef e)
        {
            int best = -1;
            foreach (var t in S.Trucks)
            {
                if (t.Pitch < 0 || !NodeAt(t.Pitch).Rival) continue;
                if (PitchState(t.Pitch).Rep >= e.Rep) best = t.Pitch;
            }
            return best;
        }

        void PlanDay()
        {
            S.Planned.Clear();
            if (MatchDay(S.Day) && PitchOpen(Gd.Pitches.Find(n => NodeAt(n).Match))) S.Planned.Add(new PlannedEvent { Id = "halftime", At = Gd.HalftimeFrom });
            if (!CrisesAllowed) return;
            if (S.Weather == "hot" || S.Weather == "rain") S.Planned.Add(new PlannedEvent { Id = S.Weather == "hot" ? "heat" : "rain", At = Gd.DayStart });
            double expected = Gd.EventsPerDay[Math.Min(Chapter, Gd.EventsPerDay.Length - 1)];
            int n = Sample(expected);
            for (int k = 0; k < n; k++)
            {
                var pool = Gd.Events.FindAll(EventEligible);
                if (pool.Count == 0) break;
                var w = new double[pool.Count];
                for (int i = 0; i < w.Length; i++) w[i] = pool[i].Weight;
                var e = pool[Rng.PickWeighted(w)];
                if (e.Kind == "announced") Fire(e.Id);
                else S.Planned.Add(new PlannedEvent { Id = e.Id, At = Gd.DayStart + 60 + Rng.Frand() * (Gd.DayMinutes - 150) });
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

        int RandomOpenTruck(Predicate<TruckState> ok)
        {
            var idx = new List<int>();
            for (int i = 0; i < S.Trucks.Count; i++) if (S.Trucks[i].Open && ok(S.Trucks[i])) idx.Add(i);
            return idx.Count == 0 ? -1 : idx[Rng.RangeI(0, idx.Count - 1)];
        }

        /// <summary>Applies a crisis now (also used by tests and the balance bot). Returns false when it found no target.</summary>
        public bool Fire(string id)
        {
            var e = Gd.Event(id) ?? new EventDef { Id = id };
            switch (id)
            {
                case "halftime":
                {
                    int stadium = Gd.Pitches.Find(n => NodeAt(n).Match);
                    Emit("crisis", -1, stadium, Gd.HalftimeMult, id);
                    return true;
                }
                case "heat":
                case "rain":
                    Emit("crisis", -1, -1, 0, id);
                    return true;
                case "propane":
                {
                    int ti = RandomOpenTruck(t => t.Propane > Gd.Svc.PropaneLow);
                    if (ti < 0) return false;
                    S.Trucks[ti].Propane = Math.Min(S.Trucks[ti].Propane, e.Level);
                    Emit("crisis", ti, -1, 0, id);
                    return true;
                }
                case "flare":
                {
                    int ti = RandomOpenTruck(t => !t.Flaring);
                    if (ti < 0) return false;
                    Flare(ti);
                    return true;
                }
                case "rival":
                {
                    int node = RivalPitch(e);
                    if (node < 0) return false;
                    S.Events.Add(new ActiveEvent { Id = id, UntilDay = S.Day, Target = node, Mult = e.Mult });
                    Emit("crisis", -1, node, e.Mult, id);
                    return true;
                }
            }
            return false;
        }

        /// <summary>Crises active now (for the morning plan card and the HUD chips).</summary>
        public List<string> ActiveCrises()
        {
            var o = new List<string>();
            foreach (var e in S.Events) if (!o.Contains(e.Id)) o.Add(e.Id);
            if (S.Weather == "hot" && !o.Contains("heat")) o.Add("heat");
            if (S.Weather == "rain" && !o.Contains("rain")) o.Add("rain");
            if (MatchDay(S.Day) && PitchOpen(Gd.Pitches.Find(n => NodeAt(n).Match))) o.Add("match_day");
            foreach (var t in S.Trucks) { if (t.Flaring) { o.Add("flare"); break; } }
            foreach (var t in S.Trucks) { if (t.Open && t.Propane <= Gd.Svc.PropaneLow) { o.Add("propane"); break; } }
            return o;
        }
    }
}
