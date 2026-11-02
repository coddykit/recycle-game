// Semester Rush sim core — crises (plan section 7). Short (a day or two), never game over, light satire; never during the
// guide or the first week. Snow and open days are known in the morning (plan around them); the others land at a random
// minute of the day. Two ask the dean to choose (protest, a poached professor) with a sensible default at the deadline.
// "Call the city for help" (rewarded, one per day) ends a running one.
using System;
using System.Collections.Generic;

namespace SemesterRush.Sim
{
    public sealed partial class Simulation
    {
        bool EventEligible(EventDef e)
        {
            if (S.Week < e.MinWeek) return false;
            if (e.Exam != ExamWeek && e.Exam) return false;
            if (e.Id == "dorm_flu" && Beds <= 0) return false;
            if (e.Id == "poached" && S.Profs.Count < 2) return false;
            if (e.Decision && S.Pending != null) return false;
            if (S.Events.Exists(a => a.Id == e.Id)) return false;
            if (S.Planned.Exists(a => a.Id == e.Id)) return false;
            return true;
        }

        public bool CrisesAllowed => !S.Tutorial && S.Week > Gd.EventFreeWeeks;

        void PlanDay()
        {
            S.Planned.Clear();
            if (!CrisesAllowed) return;
            double expected = Gd.EventsPerDay[Math.Min(Chapter, Gd.EventsPerDay.Length - 1)];
            if (ExamWeek) expected += 0.3;
            int n = (int)Math.Floor(expected);
            if (Rng.Frand() < expected - n) n++;
            for (int k = 0; k < n; k++)
            {
                var pool = Gd.Events.FindAll(EventEligible);
                if (pool.Count == 0) break;
                var w = new double[pool.Count];
                for (int i = 0; i < w.Length; i++) w[i] = pool[i].Weight * (pool[i].Exam ? 3 : 1);
                var e = pool[Rng.PickWeighted(w)];
                if (e.Announce) Fire(e.Id);
                else S.Planned.Add(new PlannedEvent { Id = e.Id, At = Gd.Slots[0] + Rng.Frand() * (Gd.Slots[Gd.SlotCount - 1] - Gd.Slots[0]) });
            }
        }

        void RunPlanned(double upTo)
        {
            for (int i = S.Planned.Count - 1; i >= 0; i--)
            {
                if (S.Planned[i].At > upTo) continue;
                string id = S.Planned[i].Id;
                S.Planned.RemoveAt(i);
                Fire(id);
            }
        }

        EventDef Def(string id) => Gd.Events.Find(e => e.Id == id) ?? new EventDef { Id = id, Mult = 1, Days = 1, ClosedMin = 300 };

        int Pick(List<int> idx) => idx.Count == 0 ? -1 : idx[Rng.RangeI(0, idx.Count - 1)];

        /// <summary>Applies a crisis now (also used by tests and the balance bot). False when it found no target.</summary>
        public bool Fire(string id)
        {
            var e = Def(id);
            switch (id)
            {
                case "snow_day":
                    S.Events.Add(new ActiveEvent { Id = id, UntilDay = AbsDay, Mult = Gd.SnowSpeed });
                    Emit("crisis", -1, -1, 0, id);
                    return true;
                case "open_day":
                    S.Events.Add(new ActiveEvent { Id = id, UntilDay = AbsDay, Mult = e.Bonus });
                    Emit("crisis", -1, -1, 0, id);
                    return true;
                case "dorm_flu":
                    if (Beds <= 0) return false;
                    S.Events.Add(new ActiveEvent { Id = id, UntilDay = AbsDay + Math.Max(1, e.Days) - 1, Mult = e.Mult });
                    Emit("crisis", -1, -1, 0, id);
                    return true;
                case "fire_drill":
                    S.Events.Add(new ActiveEvent { Id = id, UntilDay = AbsDay });
                    Emit("crisis", -1, -1, 0, id);
                    return true;
                case "sick_prof":
                {
                    var idx = new List<int>();
                    for (int p = 0; p < S.Profs.Count; p++)
                        if (S.Profs[p].OffDay != AbsDay && ActivePlan.Exists(x => x.Day == S.Day && S.CourseProf[x.Course] == p && !Past(x.Day, x.Slot))) idx.Add(p);
                    int pi = Pick(idx);
                    if (pi < 0) return false;
                    S.Profs[pi].OffDay = AbsDay;
                    Emit("crisis", pi, -1, 0, id);
                    return true;
                }
                case "burst_pipe":
                {
                    var idx = new List<int>();
                    foreach (var b in S.Buildings)
                        if (b.Built && DefOf(b).Teaching && ActivePlan.Exists(x => x.Day == S.Day && x.Node == b.Node && !Past(x.Day, x.Slot))) idx.Add(b.Node);
                    int node = Pick(idx);
                    if (node < 0) return false;
                    BuildingAt(node).ClosedUntil = S.Minute + e.ClosedMin / (1.0 + 0.5 * Staff(Depts.Maintenance));
                    Emit("crisis", node, -1, BuildingAt(node).ClosedUntil, id);
                    return true;
                }
                case "protest":
                    S.Pending = new Decision { Id = id, Deadline = S.Minute + Gd.DecisionMin };
                    Emit("crisis", -1, -1, 0, id);
                    Emit("decision", -1, -1, 0, id);
                    return true;
                case "poached":
                {
                    var idx = new List<int>();
                    for (int p = 0; p < S.Profs.Count; p++) if (!S.Profs[p].Leaving) idx.Add(p);
                    int pi = Pick(idx);
                    if (pi < 0) return false;
                    S.Pending = new Decision { Id = id, Target = pi, Deadline = S.Minute + Gd.DecisionMin };
                    Emit("crisis", pi, -1, 0, id);
                    Emit("decision", pi, -1, 0, id);
                    return true;
                }
                case "power_outage":
                {
                    var slots = new List<int>();
                    for (int k = 0; k < Gd.SlotCount; k++)
                        if (!Past(S.Day, k) && ActivePlan.Exists(x => x.Day == S.Day && x.Slot == k)) slots.Add(k);
                    int slot = Pick(slots);
                    if (slot < 0) return false;
                    S.Events.Add(new ActiveEvent { Id = id, UntilDay = AbsDay, Target = slot });
                    Emit("crisis", slot, -1, 0, id);
                    return true;
                }
            }
            return false;
        }

        /// <summary>Options of the open decision: protest -> concede | talk; poached -> retain | let_go.</summary>
        public static string[] Options(string decision) => decision == "protest" ? new[] { "concede", "talk" } : decision == "poached" ? new[] { "retain", "let_go" } : new string[0];

        /// <summary>The cost of an option in $ (for the card).</summary>
        public double OptionCost(string option) => option == "concede" ? Math.Round(0.5 * DayIncome()) : option == "retain" ? 2 * Gd.ProfWage : 0;

        string DefaultOption(Decision d) => d.Id == "poached" ? (S.Cash >= OptionCost("retain") ? "retain" : "let_go") : "talk";

        public string Decide(string id, string option)
        {
            var d = S.Pending;
            if (d == null || d.Id != id) return "invalid";
            if (Array.IndexOf(Options(id), option) < 0) return "invalid";
            double cost = OptionCost(option);
            switch (option)
            {
                case "concede": S.Cash -= cost; S.ThisWeek.Concessions += cost; break;
                case "talk": S.Events.Add(new ActiveEvent { Id = "protest", UntilDay = AbsDay, Mult = Def("protest").Mult }); Rewalk(); break;
                case "retain": S.Cash -= cost; S.ThisWeek.Concessions += cost; break;
                case "let_go": if (d.Target >= 0 && d.Target < S.Profs.Count) S.Profs[d.Target].Leaving = true; break;
            }
            S.Pending = null;
            Emit("decided", d.Target, -1, cost, id + ":" + option);
            return "";
        }

        void EveningEvents()
        {
            foreach (var e in S.Events)
            {
                if (e.Id != "open_day") continue;
                double rate = S.TripsToday <= 0 ? 0 : (double)S.LateToday / S.TripsToday;
                double bonus = Math.Round(e.Mult * DayIncome() * Math.Max(0, 1 - rate * 2));
                S.Cash += bonus;
                S.ThisWeek.EventIncome += bonus;
                if (rate < 0.1) S.Prestige = Math.Min(Gd.PrestigeMax, S.Prestige + 2);
                Emit("open_day_done", -1, -1, bonus);
            }
        }

        /// <summary>The crisis "Call the city for help" would end now ("" = none).</summary>
        public string HelpableCrisis()
        {
            foreach (var id in new[] { "snow_day", "dorm_flu", "power_outage", "protest" }) if (Active(id)) return id;
            foreach (var b in S.Buildings) if (b.ClosedUntil > S.Minute) return "burst_pipe";
            foreach (var p in S.Profs) if (p.OffDay == AbsDay) return "sick_prof";
            if (S.Pending != null && S.Pending.Id == "protest") return "protest";
            return "";
        }

        void EndCrisis(string id)
        {
            switch (id)
            {
                case "burst_pipe": foreach (var b in S.Buildings) b.ClosedUntil = 0; break;
                case "sick_prof": foreach (var p in S.Profs) if (p.OffDay == AbsDay) p.OffDay = -1; break;
                case "protest": if (S.Pending != null && S.Pending.Id == "protest") S.Pending = null; S.Events.RemoveAll(e => e.Id == "protest"); break;
                default: S.Events.RemoveAll(e => e.Id == id); break;
            }
            Rewalk();
            Emit("crisis_end", -1, -1, 0, id);
        }

        /// <summary>Crises active now (HUD chips).</summary>
        public List<string> ActiveCrises()
        {
            var o = new List<string>();
            foreach (var e in S.Events) if (!o.Contains(e.Id)) o.Add(e.Id);
            foreach (var b in S.Buildings) if (b.ClosedUntil > S.Minute) { o.Add("burst_pipe"); break; }
            foreach (var p in S.Profs) if (p.OffDay == AbsDay) { o.Add("sick_prof"); break; }
            if (S.Pending != null && !o.Contains(S.Pending.Id)) o.Add(S.Pending.Id);
            return o;
        }
    }
}
