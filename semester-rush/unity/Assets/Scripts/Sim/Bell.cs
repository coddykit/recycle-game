// Semester Rush sim core — the bell change (plan section 4.2, the core of the game). Ten minutes before every slot each
// cohort with a class walks from where it is (the previous room, the canteen after lunch, the library during a gap, or
// home: dorm beds for residents, the bus stop for commuters) to its room along the shortest open path. Students on the
// same path at the same time slow each other down (load over capacity per minute); marshals widen the busiest paths,
// staggered bells split the faculties into two waves, a gate opens a shortcut. Walking speeds spread ±20 %: the share
// whose walk takes longer than the bell change is late. Computed ONCE per bell change (analytic, no agents); the diorama
// draws the trips (dots + heat ribbon) from the cached result.
//   Lunch: at 12:00 / 13:30 every cohort eats in its first free lunch slot; seats = canteens × crew; the rest go hungry.
//   Classes end: attendance (late, seats, hunger, flu) × quality (professor, room, library, labs) -> grade points + tuition.
using System;
using System.Collections.Generic;

namespace SemesterRush.Sim
{
    public sealed class Trip
    {
        public int Cohort, From, To, Count, Group;
        public List<int> Path = new List<int>();
        public double Minutes, Late;
    }

    public sealed class BellResult
    {
        public int Day, Slot;
        public readonly List<Trip> Trips = new List<Trip>();
        public double[] Heat = new double[0];     // per edge: load / capacity of the bell change (0 = nobody)
        public int Students, Late;
        public bool Counted;
    }

    public sealed partial class Simulation
    {
        BellResult bell;
        /// <summary>The last bell change (the diorama draws it while InBell). Recomputed for display after a load.</summary>
        public BellResult Bell
        {
            get
            {
                if (bell == null && InBell) bell = ComputeBell(S.Day, BellSlotAt(S.Minute), false);
                return bell;
            }
        }

        public void RebuildGraph()
        {
            graph = new PathGraph(Gd, EdgeOpen, e => e.Len / Gd.WalkSpeed / (e.Type == "shuttle" ? Gd.ShuttleSpeed : 1.0));
        }

        public bool EdgeOpen(EdgeDef e)
        {
            if (!Unlocked(Gd.Nodes[e.A].Zone) || !Unlocked(Gd.Nodes[e.B].Zone)) return false;
            if (e.Type == "gate") return S.OpenGates.Contains(e.Index);
            if (e.Type == "shuttle") return Has("shuttle");
            return true;
        }

        public double EdgeCap(int e)
        {
            var d = Gd.Edges[e];
            double cap = d.Type == "gate" ? Gd.GateCap : d.Type == "shuttle" ? Gd.ShuttleCap : Gd.PathCap;
            if (S.Widened.Contains(e)) cap *= Gd.WidenMult;
            foreach (var ev in S.Events) if (ev.Id == "protest") cap *= ev.Mult;
            return cap;
        }

        double WalkSpeedNow()
        {
            double v = Gd.WalkSpeed * (Has("bike_lanes") ? Gd.BikeSpeed : 1.0);
            foreach (var ev in S.Events) if (ev.Id == "snow_day") v *= ev.Mult;
            return v;
        }

        public bool Active(string eventId) => S.Events.Exists(e => e.Id == eventId);

        /// <summary>Where a cohort starts the walk to its class at day/slot: (node, share) pairs.</summary>
        List<(int node, double share)> Origins(int cohort, int day, int slot, int dest)
        {
            var o = new List<(int, double)>();
            if (slot > 0)
            {
                int prev = CohortAt(cohort, day, slot - 1);
                if (prev >= 0 && !IsClosedNow(ActivePlan[prev].Node, Gd.Slots[slot - 1])) { o.Add((ActivePlan[prev].Node, 1.0)); return o; }
                if (S.Cohorts[cohort].Lunch == slot - 1)
                {
                    int best = -1; double bm = double.MaxValue;
                    foreach (int cn in BuiltOf("canteen")) { double m = graph.Minutes(cn, dest); if (m < bm) { bm = m; best = cn; } }
                    if (best >= 0) { o.Add((best, 1.0)); return o; }
                }
                bool earlier = false;
                for (int k = 0; k < slot; k++) if (CohortAt(cohort, day, k) >= 0) earlier = true;
                var libs = BuiltOf("library");
                if (earlier && libs.Count > 0) { o.Add((libs[0], 1.0)); return o; }
            }
            double res = ResidentShare;
            int beds = Beds;
            if (res > 0 && beds > 0)
                foreach (var b in S.Buildings)
                {
                    if (!b.Built || (DefOf(b)?.Beds ?? 0) <= 0) continue;
                    o.Add((b.Node, res * DefOf(b).Beds / beds));
                }
            if (res < 1) o.Add((Gd.EntranceNode, 1 - res));
            return o;
        }

        /// <summary>The bell change before slot on day (counted = adds late / trips to today's and the week's stats).</summary>
        BellResult ComputeBell(int day, int slot, bool counted)
        {
            var r = new BellResult { Day = day, Slot = slot, Heat = new double[Gd.Edges.Count], Counted = counted };
            if (slot < 0 || graph == null) return r;
            var plan = ActivePlan;
            for (int i = 0; i < plan.Count; i++)
            {
                var p = plan[i];
                if (p.Day != day || p.Slot != slot || !CourseOn(p.Course)) continue;
                if (IsClosedNow(p.Node, Gd.Slots[slot])) continue;
                int cohort = Gd.Courses[p.Course].CohortIndex;
                int size = S.Cohorts[cohort].Size;
                int group = S.Staggered && Has("staggered_bells") ? Gd.Faculty(Gd.Cohorts[cohort].Faculty).Index % 2 : 0;
                int left = size;
                var origins = Origins(cohort, day, slot, p.Node);
                for (int k = 0; k < origins.Count; k++)
                {
                    int count = k == origins.Count - 1 ? left : (int)Math.Round(size * origins[k].share);
                    count = Math.Max(0, Math.Min(left, count));
                    left -= count;
                    if (count <= 0) continue;
                    var t = new Trip { Cohort = cohort, From = origins[k].node, To = p.Node, Count = count, Group = group };
                    t.Path = origins[k].node == p.Node ? new List<int> { p.Node } : graph.Path(origins[k].node, p.Node);
                    r.Trips.Add(t);
                }
            }
            // loads per wave
            double loadMult = Active("open_day") ? Gd.OpenDayLoad : 1.0;
            var load = new double[2, Gd.Edges.Count];
            foreach (var t in r.Trips)
                foreach (int e in graph.Edges(t.Path)) load[t.Group, e] += t.Count * loadMult;
            // marshals stand on the busiest paths
            var cap = new double[Gd.Edges.Count];
            var ratio = new List<(int e, double r)>();
            for (int e = 0; e < cap.Length; e++)
            {
                cap[e] = EdgeCap(e);
                double l = Math.Max(load[0, e], load[1, e]);
                if (l > 0) ratio.Add((e, l / (cap[e] * Gd.BellMin)));
            }
            ratio.Sort((a, b) => b.r.CompareTo(a.r) != 0 ? b.r.CompareTo(a.r) : a.e.CompareTo(b.e));
            int marshals = Staff(Depts.Marshals) + S.ExtraMarshals;
            for (int k = 0; k < marshals && k < ratio.Count; k++) cap[ratio[k].e] *= 1 + Gd.MarshalCap;
            double speed = WalkSpeedNow();
            double timeMult = (Has("signposts") ? Gd.SignpostTime : 1.0) * (Active("fire_drill") ? Gd.DrillTime : 1.0);
            for (int e = 0; e < cap.Length; e++) r.Heat[e] = Math.Max(load[0, e], load[1, e]) / (cap[e] * Gd.BellMin);
            var lateByCohort = new Dictionary<int, double>();
            foreach (var t in r.Trips)
            {
                double m = 0;
                foreach (int e in graph.Edges(t.Path))
                {
                    var ed = Gd.Edges[e];
                    double free = ed.Len / speed / (ed.Type == "shuttle" ? Gd.ShuttleSpeed : 1.0);
                    double over = load[t.Group, e] / (cap[e] * Gd.BellMin) - 1.0;
                    m += free * (1 + Gd.Congestion * Math.Max(0, over));
                }
                t.Minutes = m * timeMult;
                t.Late = LateShare(t.Minutes);
                r.Students += t.Count;
                int late = (int)Math.Round(t.Late * t.Count);
                r.Late += late;
                lateByCohort[t.Cohort] = (lateByCohort.TryGetValue(t.Cohort, out double v) ? v : 0) + late;
            }
            if (counted)
            {
                foreach (var kv in lateByCohort) S.Cohorts[kv.Key].Late[slot] = Math.Min(1, kv.Value / Math.Max(1.0, S.Cohorts[kv.Key].Size));
                S.LateToday += r.Late; S.TripsToday += r.Students;
                S.ThisWeek.Late += r.Late; S.ThisWeek.Trips += r.Students;
            }
            return r;
        }

        /// <summary>Share of a cohort later than the bell when the mean walk takes m minutes (speeds spread ±Spread).</summary>
        public double LateShare(double m)
        {
            double s = Math.Max(0.01, Gd.Spread), w = Gd.BellMin;
            double hi = m * (1 + s), lo = m * (1 - s);
            if (hi <= w) return 0;
            if (lo >= w) return 1;
            return (hi - w) / (hi - lo);
        }

        /// <summary>Busiest load / capacity per path this week (not saved; the Campus sheet offers wider paths where it is high).</summary>
        public double[] PeakHeat { get; private set; } = new double[0];

        void BellChange(int slot)
        {
            bell = ComputeBell(S.Day, slot, true);
            if (PeakHeat.Length != bell.Heat.Length || (S.Day == 0 && slot == 0)) PeakHeat = new double[bell.Heat.Length];
            for (int e = 0; e < bell.Heat.Length; e++) PeakHeat[e] = Math.Max(PeakHeat[e], bell.Heat[e]);
            S.ExtraMarshals = 0;
            S.Events.RemoveAll(e => e.Id == "fire_drill");
            Emit("bell", slot, bell.Late, bell.Students);
        }

        /// <summary>The topology or the marshals changed during a bell change: walk it again (the late counter updates).</summary>
        void Rewalk()
        {
            if (!InBell) return;
            int slot = BellSlotAt(S.Minute);
            if (bell != null && bell.Counted && bell.Slot == slot && bell.Day == S.Day)
            {
                S.LateToday -= bell.Late; S.TripsToday -= bell.Students;
                S.ThisWeek.Late -= bell.Late; S.ThisWeek.Trips -= bell.Students;
                foreach (var t in bell.Trips) S.Cohorts[t.Cohort].Late[slot] = 0;
                bell = ComputeBell(S.Day, slot, true);
                Emit("bell", slot, bell.Late, bell.Students);
            }
            else bell = ComputeBell(S.Day, slot, false);
        }

        /// <summary>A bell change estimate for the board (no stats): late students if the day ran with today's plan.</summary>
        public int EstimateLate(int day, int slot) => ComputeBell(day, slot, false).Late;

        void Lunch(int slot)
        {
            var eaters = new List<int>();
            double demand = 0;
            bool last = slot == Gd.LunchSlots[Gd.LunchSlots.Length - 1];
            for (int c = 0; c < S.Cohorts.Count; c++)
            {
                if (!CohortOn(c) || S.Cohorts[c].Lunch != -1) continue;
                if (CohortAt(c, S.Day, slot) >= 0) { if (last) S.Cohorts[c].Lunch = -2; continue; }
                eaters.Add(c);
                demand += S.Cohorts[c].Size;
            }
            if (Active("open_day")) demand *= 1.3;
            double meals = Meals;
            double unfed = demand <= 0 ? 0 : Math.Max(0, 1 - meals / demand);
            int hungry = 0;
            foreach (int c in eaters)
            {
                S.Cohorts[c].Lunch = slot;
                S.Cohorts[c].Unfed = unfed;
                hungry += (int)Math.Round(S.Cohorts[c].Size * unfed);
            }
            S.ThisWeek.Unfed += hungry;
            Emit("lunch", slot, hungry, demand, meals.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>Quality of a class: professor (or substitute), room, library, labs + technicians, upgrades.</summary>
        public double Quality(Placement p, out string note)
        {
            note = "";
            var c = Gd.Courses[p.Course];
            double q;
            if (ExamWeek) q = 1.0;
            else
            {
                int prof = S.CourseProf[p.Course];
                if (prof < 0 || prof >= S.Profs.Count) { note = "no_prof"; return 0; }
                q = S.Profs[prof].Skill;
                if (S.Profs[prof].OffDay == AbsDay)
                {
                    // a colleague of the faculty who is free this slot covers, else a substitute
                    bool cover = false;
                    for (int k = 0; k < S.Profs.Count && !cover; k++)
                        if (k != prof && S.Profs[k].Faculty == S.Profs[prof].Faculty && S.Profs[k].OffDay != AbsDay && !ProfBusy(k, p.Day, p.Slot)) { cover = true; q = S.Profs[k].Skill * 0.95; }
                    if (!cover) { q *= Gd.SubProfMult; note = "substitute"; }
                }
                if (Has("smart_boards")) q += Gd.SmartBoards;
                if (Has("faculty_dev")) q += Gd.FacultyDev;
                foreach (int lib in BuiltOf("library")) { q += (Gd.Building("library").Quality) * (Has("library_hours") ? 2 : 1); break; }
                if (c.Room == Rooms.Lab)
                {
                    int labs = 0;
                    foreach (var o in ActivePlan) if (o.Day == p.Day && o.Slot == p.Slot && Gd.Courses[o.Course].Room == Rooms.Lab) labs++;
                    if (labs > Staff(Depts.LabTechs)) { q *= Gd.LabUntechMult; note = "no_tech"; }
                    if (Has("lab_equipment")) q += Gd.LabEquipment;
                }
            }
            return Math.Max(0, Math.Min(1.15, q));
        }

        void EndClasses(int slot)
        {
            var plan = ActivePlan;
            bool exam = ExamWeek;
            int invigNeed = 0;
            if (exam)
                foreach (var p in plan) if (p.Day == S.Day && p.Slot == slot && CourseOn(p.Course)) invigNeed += (int)Math.Ceiling(S.Cohorts[Gd.Courses[p.Course].CohortIndex].Size / Gd.InvigilatorPer);
            int invigHave = Staff(Depts.Registry) + (S.ExamKit ? 2 : 0);
            for (int i = 0; i < plan.Count; i++)
            {
                var p = plan[i];
                if (p.Day != S.Day || p.Slot != slot || !CourseOn(p.Course)) continue;
                int cohort = Gd.Courses[p.Course].CohortIndex;
                var cs = S.Cohorts[cohort];
                if (IsClosedNow(p.Node, Gd.Slots[slot]) || BuildingAt(p.Node).ClosedUntil > Gd.Slots[slot])
                {
                    Emit("class_done", p.Course, p.Node, 0, "closed");
                    continue;
                }
                double q = Quality(p, out string note);
                if (q <= 0) { Emit("class_done", p.Course, p.Node, 0, note); continue; }
                double late = cs.Late[slot];
                double credit = Gd.LateCredit * (Has("tutoring") ? 1.0 / Gd.TutoringLate : 1.0);
                double att = (1 - late) + late * Math.Min(1, credit);
                if (slot > Gd.LunchSlots[0])
                {
                    if (cs.Lunch == -2) att *= Gd.HungerMult;
                    else if (cs.Lunch >= 0 && cs.Lunch < slot) att *= 1 - cs.Unfed * (1 - Gd.HungerMult);
                }
                foreach (var ev in S.Events) if (ev.Id == "dorm_flu") att *= 1 - ResidentShare * (1 - ev.Mult);
                int seats = DefOf(BuildingAt(p.Node)).Seats;
                double seated = Math.Min(1.0, (double)seats / Math.Max(1, cs.Size));
                double attending = cs.Size * seated * Math.Min(1, (1 - late) + late);
                double score;
                if (exam)
                {
                    double prep = cs.SemWeeks > 0 ? cs.SemSum / cs.SemWeeks : 0.6;
                    double invig = invigHave >= invigNeed ? 1.0 : Gd.UninvigilatedMult;
                    double power = 1.0;
                    foreach (var ev in S.Events) if (ev.Id == "power_outage" && ev.Target == slot && !Has("backup_power") && !S.ExamKit) power = 0.7;
                    score = Math.Min(1, att * seated * invig * power * Math.Min(1, prep + 0.1) * cs.Aptitude);
                    double fee = attending * Gd.ExamFee;
                    S.Cash += fee;
                    S.ThisWeek.ExamFees += fee;
                }
                else
                {
                    score = Math.Min(1, att * seated * q * cs.Aptitude);
                    double fee = attending * Gd.Tuition * PrestigeMult;
                    S.Till += fee;
                    S.ThisWeek.Tuition += fee;
                }
                S.WeekScore[p.Course] += score;
                S.WeekHeld[p.Course]++;
                S.ThisWeek.Held++;
                S.StudentHours += (long)Math.Round(attending);
                Emit("class_done", p.Course, p.Node, score, note);
            }
        }
    }
}
