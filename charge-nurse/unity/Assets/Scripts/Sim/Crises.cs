// Charge Nurse sim core — crises (plan §7). Short (≤ 3 days), never game over, no gore; never during the guide or the
// first forgiving days, never at night. Day-wide ones (flu week, supply delay, inspection) are announced on the morning
// board so the player can prepare; the others land at a random minute of the live shift with a visible effect.
using System;
using System.Collections.Generic;

namespace ChargeNurse.Sim
{
    public sealed partial class Simulation
    {
        bool EventEligible(EventDef e)
        {
            if (S.Day < e.MinDay) return false;
            if (S.Events.Exists(a => a.Id == e.Id)) return false;
            if (S.Planned.Exists(a => a.Id == e.Id)) return false;
            if ((e.Id == "scanner_down") && !S.Rooms.Exists(r => r.Kind != "or")) return false;
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
                for (int i = 0; i < w.Length; i++) w[i] = pool[i].Weight * (pool[i].Id == "sick_calls" ? 0.5 + S.Burnout / 50.0 : 1.0);
                var e = pool[Math.Max(0, Rng.PickWeighted(w))];
                if (e.Announced) Fire(e.Id);
                else S.Planned.Add(new PlannedEvent { Id = e.Id, At = Gd.DayStart + 60 + Rng.Frand() * (Gd.DayMinutes - 180) });
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

        void TickEvents(double dt)
        {
            foreach (var r in S.Rooms) if (r.Down > 0) r.Down = Math.Max(0, r.Down - dt);
            for (int i = S.Events.Count - 1; i >= 0; i--)
            {
                var e = S.Events[i];
                if (e.Left <= 0) continue;
                e.Left -= dt;
                if (e.Left <= 0) { S.Events.RemoveAt(i); Emit("crisis_over", -1, -1, 0, e.Id); }
            }
        }

        EventDef Def(string id) => Gd.Events.Find(e => e.Id == id) ?? new EventDef { Id = id };

        /// <summary>Applies a crisis now (also used by tests and the balance bot). Returns false when it found no target.</summary>
        public bool Fire(string id)
        {
            var e = Def(id);
            switch (id)
            {
                case "flu_surge":
                    S.Events.Add(new ActiveEvent { Id = id, UntilDay = S.Day + Math.Max(1, e.Days) - 1, Mult = Has("surge_stock") ? 1 + (e.Mult - 1) * 0.5 : e.Mult });
                    break;
                case "supply_delay":
                    S.Events.Add(new ActiveEvent { Id = id, UntilDay = S.Day + Math.Max(1, e.Days) - 1, Mult = Has("surge_stock") ? 1 + (e.Mult - 1) * 0.3 : e.Mult });
                    break;
                case "inspection":
                    S.Events.Add(new ActiveEvent { Id = id, UntilDay = S.Day });
                    break;
                case "crash":
                {
                    // a multi-vehicle crash: a burst of mixed arrivals through the ambulance bay (tagged by the crews)
                    // sized to the hospital: half the trauma-centre burst in the clinic, three quarters in the town hospital
                    double scale = Chapter <= 1 ? 0.5 : Chapter == 2 ? 0.75 : 1.0;
                    int n = Math.Max(2, (int)Math.Round(Rng.RangeI(Math.Max(1, e.Min), Math.Max(e.Min, e.Max)) * scale));
                    var mix = new[] { 0.3, 0.35, 0.25, 0.1 };
                    for (int k = 0; k < n; k++) NewPatient(PickSev(mix), true);
                    S.Events.Add(new ActiveEvent { Id = id, UntilDay = S.Day, Left = 120, Mult = n });
                    Emit("crisis", n, -1, n, id);
                    return true;
                }
                case "power_outage":
                    if (Has("generator")) { Emit("crisis", -1, -1, 0, "power_saved"); return true; }
                    foreach (var r in S.Rooms) r.Down = Math.Max(r.Down, e.Minutes);
                    S.Events.Add(new ActiveEvent { Id = id, UntilDay = S.Day, Left = e.Minutes });
                    break;
                case "scanner_down":
                {
                    var rooms = S.Rooms.FindAll(r => r.Kind != "or" && r.Down <= 0);
                    if (rooms.Count == 0) return false;
                    var r = rooms[Rng.RangeI(0, rooms.Count - 1)];
                    r.Down = e.Minutes;
                    S.Events.Add(new ActiveEvent { Id = id, UntilDay = S.Day, Target = r.Kind, Left = e.Minutes });
                    break;
                }
                case "sick_calls":
                {
                    var teams = new List<string>();
                    foreach (var t in Teams.Pool) if (Team(t) >= 2) teams.Add(t);
                    if (teams.Count == 0) return false;
                    S.SickTeam = teams[Rng.RangeI(0, teams.Count - 1)];
                    S.Events.Add(new ActiveEvent { Id = id, UntilDay = S.Day, Target = S.SickTeam });
                    break;
                }
                case "norovirus":
                {
                    // the isolation plan closes free beds of one ward for two days
                    string ward = "";
                    int bestN = 0;
                    foreach (var w in new[] { "obs", "general", "surgical" }) { int n = BedsIn(w); if (n >= 3 && n > bestN) { bestN = n; ward = w; } }
                    if (ward == "") return false;
                    int closed = 0;
                    foreach (var b in S.Beds)
                        if (closed < e.Beds && !b.Bay && WardOf(b) == ward && b.Patient < 0 && b.State != BedState.Closed) { b.State = BedState.Closed; b.Left = 0; closed++; }
                    if (closed == 0) return false;
                    S.Events.Add(new ActiveEvent { Id = id, UntilDay = S.Day + Math.Max(1, e.Days) - 1, Target = ward, Mult = closed });
                    break;
                }
                default: return false;
            }
            Emit("crisis", -1, -1, e.Mult, id);
            return true;
        }

        /// <summary>Crises active now (for the morning board and the HUD chips).</summary>
        public List<string> ActiveCrises()
        {
            var o = new List<string>();
            foreach (var e in S.Events) if (!o.Contains(e.Id)) o.Add(e.Id);
            return o;
        }
    }
}
