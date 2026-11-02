// Semester Rush sim core — the timetable (plan section 4.1): course × day × slot × room. Real campus constraints, derived
// from room / time / professor / student rules (clean-room note in the plan): a room holds one class per slot and must be
// the right type (lab courses need a lab), a cohort sits one class per slot, a professor teaches one class per slot and at
// most 3 a day, a course meets its weekly count. Only future cells can change while the week runs. Exam week uses the
// exam plan (one exam per course, any teaching room). Auto-fill is greedy; the smart one (Registrar perk or the
// auto-fill upgrade) also weighs walking time and spreads lunch.
using System;
using System.Collections.Generic;

namespace SemesterRush.Sim
{
    public sealed class LunchFix
    {
        public int Placement, Day, Slot, Node;
    }

    public sealed partial class Simulation
    {
        /// <summary>The plan the week runs: the exam plan in exam week, else the weekly timetable.</summary>
        public List<Placement> ActivePlan => ExamWeek ? S.Exams : S.Plan;

        public int Needed(int course) => ExamWeek ? 1 : Gd.Courses[course].PerWeek;

        public int Count(int course)
        {
            int n = 0;
            foreach (var p in ActivePlan) if (p.Course == course) n++;
            return n;
        }

        public int At(int day, int slot, int node)
        {
            var plan = ActivePlan;
            for (int i = 0; i < plan.Count; i++) if (plan[i].Day == day && plan[i].Slot == slot && plan[i].Node == node) return i;
            return -1;
        }

        /// <summary>The placement of a cohort at a day + slot, -1 none.</summary>
        public int CohortAt(int cohort, int day, int slot)
        {
            var plan = ActivePlan;
            for (int i = 0; i < plan.Count; i++)
                if (plan[i].Day == day && plan[i].Slot == slot && Gd.Courses[plan[i].Course].CohortIndex == cohort) return i;
            return -1;
        }

        int ProfLoad(int prof, int day, int exceptPlacement = -1)
        {
            int n = 0;
            var plan = S.Plan;
            for (int i = 0; i < plan.Count; i++) if (i != exceptPlacement && plan[i].Day == day && S.CourseProf[plan[i].Course] == prof) n++;
            return n;
        }

        bool ProfBusy(int prof, int day, int slot, int exceptPlacement = -1)
        {
            var plan = S.Plan;
            for (int i = 0; i < plan.Count; i++)
                if (i != exceptPlacement && plan[i].Day == day && plan[i].Slot == slot && S.CourseProf[plan[i].Course] == prof) return true;
            return false;
        }

        /// <summary>True when the cell's bell has already rung this week (the past cannot be edited).</summary>
        public bool Past(int day, int slot)
        {
            if (S.Phase == Phases.Plan) return false;
            if (S.Phase == Phases.Report) return true;
            if (day != S.Day) return day < S.Day;
            return S.Minute >= Gd.Slots[slot] - Gd.BellMin - 1e-9;
        }

        /// <summary>"" when the course may meet at day/slot in room node, else why not.</summary>
        public string CanPlace(int course, int day, int slot, int node, int exceptPlacement = -1)
        {
            if (course < 0 || course >= Gd.Courses.Count) return "invalid";
            if (!CourseOn(course)) return "locked";
            if (day < 0 || day >= Gd.DaysPerWeek || slot < 0 || slot >= Gd.SlotCount) return "invalid";
            if (Past(day, slot)) return "past";
            var b = BuildingAt(node);
            var def = b != null ? DefOf(b) : null;
            if (def == null || !def.Teaching) return "not_room";
            if (!b.Built) return "building";
            var c = Gd.Courses[course];
            if (!ExamWeek && def.Room != c.Room) return c.Room == Rooms.Lab ? "needs_lab" : "needs_hall";
            var plan = ActivePlan;
            int count = 0;
            for (int i = 0; i < plan.Count; i++)
            {
                if (i == exceptPlacement) continue;
                var p = plan[i];
                if (p.Course == course) count++;
                if (p.Day != day || p.Slot != slot) continue;
                if (p.Node == node) return "room_busy";
                if (Gd.Courses[p.Course].CohortIndex == c.CohortIndex) return "cohort_busy";
            }
            if (count >= Needed(course)) return "enough";
            if (!ExamWeek)
            {
                int prof = S.CourseProf[course];
                if (prof >= 0)
                {
                    if (ProfBusy(prof, day, slot, exceptPlacement)) return "prof_busy";
                    if (ProfLoad(prof, day, exceptPlacement) >= Gd.ProfMaxPerDay) return "prof_tired";
                }
            }
            return "";
        }

        public string Place(int course, int day, int slot, int node)
        {
            string why = CanPlace(course, day, slot, node);
            if (why != "") return why;
            ActivePlan.Add(new Placement { Course = course, Day = day, Slot = slot, Node = node });
            Emit("placed", course, node, slot, day.ToString());
            return "";
        }

        public string Remove(int placement)
        {
            var plan = ActivePlan;
            if (placement < 0 || placement >= plan.Count) return "invalid";
            if (Past(plan[placement].Day, plan[placement].Slot)) return "past";
            plan.RemoveAt(placement);
            Emit("removed", placement);
            return "";
        }

        /// <summary>Moves a future class to another future cell (keeps it when the target is not allowed).</summary>
        public string Move(int placement, int day, int slot, int node)
        {
            var plan = ActivePlan;
            if (placement < 0 || placement >= plan.Count) return "invalid";
            var p = plan[placement];
            if (Past(p.Day, p.Slot)) return "past";
            string why = CanPlace(p.Course, day, slot, node, placement);
            if (why != "") return why;
            p.Day = day; p.Slot = slot; p.Node = node;
            Emit("moved", placement, node, slot, day.ToString());
            return "";
        }

        /// <summary>Assigns a professor of the course's faculty; refused when it would double-book or overload them.</summary>
        public string AssignProf(int course, int prof)
        {
            if (course < 0 || course >= Gd.Courses.Count) return "invalid";
            if (prof < 0 || prof >= S.Profs.Count) return "invalid";
            if (S.Profs[prof].Faculty != FacultyIndexOfCourse(course)) return "wrong_faculty";
            int old = S.CourseProf[course];
            if (old == prof) return "";
            S.CourseProf[course] = prof;
            for (int i = 0; i < S.Plan.Count; i++)
            {
                var p = S.Plan[i];
                if (p.Course != course) continue;
                if (ProfBusy(prof, p.Day, p.Slot, i) || ProfLoad(prof, p.Day) > Gd.ProfMaxPerDay) { S.CourseProf[course] = old; return "prof_busy"; }
            }
            Emit("prof", course, prof);
            return "";
        }

        /// <summary>Sessions still to place this week (the board's red counter).</summary>
        public int Missing
        {
            get
            {
                int n = 0;
                for (int c = 0; c < Gd.Courses.Count; c++) if (CourseOn(c)) n += Math.Max(0, Needed(c) - Count(c));
                return n;
            }
        }

        public int RequiredSessions
        {
            get { int n = 0; for (int c = 0; c < Gd.Courses.Count; c++) if (CourseOn(c)) n += Needed(c); return n; }
        }

        public bool SmartFill => Perk("automation") || Has("auto_fill_plus");

        /// <summary>Fills every missing future session. reserve = a cell + course kept free (the guide's first class).
        /// Returns the number placed.</summary>
        public int AutoFill(bool smart, Placement reserve = null)
        {
            int placed = 0;
            var rooms = new List<int>();
            foreach (var b in S.Buildings) if (b.Built && DefOf(b).Teaching) rooms.Add(b.Node);
            for (int ci = 0; ci < Gd.Courses.Count; ci++)
            {
                if (!CourseOn(ci)) continue;
                int want = Needed(ci) - (reserve != null && reserve.Course == ci ? 1 : 0);
                while (Count(ci) < want)
                {
                    double best = double.MaxValue;
                    Placement pick = null;
                    for (int day = 0; day < Gd.DaysPerWeek; day++)
                        for (int slot = 0; slot < Gd.SlotCount; slot++)
                            foreach (int node in rooms)
                            {
                                if (reserve != null && reserve.Day == day && reserve.Slot == slot && reserve.Node == node) continue;
                                if (CanPlace(ci, day, slot, node) != "") continue;
                                double cost = FillCost(ci, day, slot, node, smart);
                                if (cost < best - 1e-9) { best = cost; pick = new Placement { Course = ci, Day = day, Slot = slot, Node = node }; }
                            }
                    if (pick == null) break;
                    ActivePlan.Add(pick);
                    placed++;
                }
            }
            if (placed > 0) Emit("auto_fill", placed);
            return placed;
        }

        static readonly double[] SlotPref = { 0, 0.4, 2.2, 0.8, 1.2 };

        double FillCost(int course, int day, int slot, int node, bool smart)
        {
            var c = Gd.Courses[course];
            int cohort = c.CohortIndex;
            int size = S.Cohorts[cohort].Size;
            double cost = slot < SlotPref.Length ? SlotPref[slot] : slot;
            int dayLoad = 0;
            foreach (var p in ActivePlan)
                if (p.Day == day && Gd.Courses[p.Course].CohortIndex == cohort) { dayLoad++; if (p.Course == course) cost += 6; }
            cost += dayLoad * 1.6 + day * 0.05;
            int seats = DefOf(BuildingAt(node)).Seats;
            if (seats < size) cost += 4 + (size - seats) * 0.1;
            else cost += (seats - size) * 0.01;
            if (!smart) return cost;
            // smart: walking minutes from where the cohort is before this slot, and lunch spreading
            int from = slot > 0 && CohortAt(cohort, day, slot - 1) >= 0 ? ActivePlan[CohortAt(cohort, day, slot - 1)].Node : Gd.EntranceNode;
            double walk = graph != null ? graph.Minutes(from, node) : 0;
            if (walk < PathGraph.Inf) cost += Math.Max(0, walk - Gd.BellMin * 0.7) * 0.8;
            if (Gd.IsLunchSlot(slot) && slot == Gd.LunchSlots[0])
            {
                double demand = LunchDemand(day, Gd.LunchSlots[0]);
                cost += demand > Meals ? -3.0 : 1.5;
            }
            return cost;
        }

        /// <summary>Students who would eat at a lunch slot on a day with the current plan (first free lunch slot).</summary>
        public double LunchDemand(int day, int lunchSlot)
        {
            double n = 0;
            for (int c = 0; c < S.Cohorts.Count; c++)
            {
                if (!CohortOn(c)) continue;
                int eats = -1;
                foreach (int ls in Gd.LunchSlots) if (CohortAt(c, day, ls) < 0) { eats = ls; break; }
                if (eats == lunchSlot) n += S.Cohorts[c].Size;
            }
            return n;
        }

        /// <summary>The guide's lunch lesson: a future class that, moved into the first lunch slot of its day, sends its cohort
        /// to the second lunch slot and ends an overfull canteen. Null when no day ahead has a lunch crunch.</summary>
        public LunchFix FindLunchFix()
        {
            int ls = Gd.LunchSlots[0];
            for (int day = 0; day < Gd.DaysPerWeek; day++)
            {
                if (Past(day, ls)) continue;
                if (LunchDemand(day, ls) <= Meals + 1e-9) continue;
                var plan = ActivePlan;
                for (int i = 0; i < plan.Count; i++)
                {
                    var p = plan[i];
                    if (p.Day != day || Gd.IsLunchSlot(p.Slot) || Past(p.Day, p.Slot)) continue;
                    int cohort = Gd.Courses[p.Course].CohortIndex;
                    bool second = Gd.LunchSlots.Length > 1 && CohortAt(cohort, day, Gd.LunchSlots[1]) < 0;
                    if (!second) continue;
                    foreach (int node in RoomsFirst(p.Node))
                        if (CanPlace(p.Course, day, ls, node, i) == "") return new LunchFix { Placement = i, Day = day, Slot = ls, Node = node };
                }
            }
            return null;
        }

        IEnumerable<int> RoomsFirst(int node)
        {
            yield return node;
            foreach (var b in S.Buildings) if (b.Built && b.Node != node && DefOf(b).Teaching) yield return b.Node;
        }

        /// <summary>Registrar: each morning, today's classes in a closed room or without a working professor move to a free
        /// future cell of the same day when one exists.</summary>
        public int AutoRepair()
        {
            int moved = 0;
            var plan = ActivePlan;
            for (int i = 0; i < plan.Count; i++)
            {
                var p = plan[i];
                if (p.Day != S.Day || Past(p.Day, p.Slot)) continue;
                if (!IsClosedNow(p.Node, Gd.Slots[p.Slot])) continue;
                bool done = false;
                for (int slot = 0; slot < Gd.SlotCount && !done; slot++)
                    foreach (var b in S.Buildings)
                    {
                        if (!b.Built || !DefOf(b).Teaching || IsClosedNow(b.Node, Gd.Slots[slot])) continue;
                        if (Move(i, S.Day, slot, b.Node) == "") { moved++; done = true; break; }
                    }
            }
            return moved;
        }

        /// <summary>Conflicts / warnings of a placement for the board (seats short, no professor, professor off, closed room).</summary>
        public string Warning(Placement p)
        {
            if (p == null || !CourseOn(p.Course)) return "";
            var b = BuildingAt(p.Node);
            if (b == null || !b.Built) return "building";
            if (b.ClosedUntil > Gd.Slots[p.Slot] && p.Day == S.Day) return "closed";
            if (!ExamWeek)
            {
                int prof = S.CourseProf[p.Course];
                if (prof < 0) return "no_prof";
                if (S.Profs[prof].OffDay == AbsDay - S.Day + p.Day) return "prof_off";
            }
            if (DefOf(b).Seats < S.Cohorts[Gd.Courses[p.Course].CohortIndex].Size) return "seats";
            return "";
        }
    }
}
