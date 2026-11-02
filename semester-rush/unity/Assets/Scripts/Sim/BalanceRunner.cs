// Semester Rush sim core — headless balance bot (plan section 15 P1: 30 min / 2 h / 7 days of play). Plays like a sensible
// new player: auto-fills the timetable every Sunday, opens gates, hires a professor for every faculty, builds halls when
// classes do not fit, a canteen / crew when students go hungry, a dorm and marshals when they are late, the library,
// upgrades in order, unlocks the next zone when prestige allows, grows the intake when the rooms have seats.
// Deterministic per seed.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SemesterRush.Sim
{
    public sealed class BalanceReport
    {
        public readonly List<WeekStats> Weeks = new List<WeekStats>();
        public readonly List<double> CashAfter = new List<double>(), PrestigeAfter = new List<double>();
        public readonly List<int> Students = new List<int>();
        public double Cash, Prestige;
        public int Chapter, Buildings, Profs, ResearchDone;
        public double MinCash = double.MaxValue;
        public bool Finite = true;
        public readonly List<string> Log = new List<string>();

        public string Table()
        {
            var sb = new StringBuilder();
            sb.AppendLine("week stud  late%  hungry held/req grade  income  costs    net  prestige   cash");
            for (int i = 0; i < Weeks.Count; i++)
            {
                var d = Weeks[i];
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0,3}{1} {2,4} {3,6:0.0} {4,6} {5,4}/{6,-3} {7,5:0.00} {8,7:0} {9,6:0} {10,6:0} {11,7:0} {12,7:0}",
                    d.Week, d.Exam ? "E" : " ", Students[i], d.LateRate * 100, d.Unfed, d.Held, d.Required, d.Grade, d.Income, d.Costs, d.Net, PrestigeAfter[i], CashAfter[i]));
            }
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "cash {0:0} prestige {1:0} chapter {2} buildings {3} profs {4} upgrades {5} minCash {6:0}",
                Cash, Prestige, Chapter, Buildings, Profs, ResearchDone, MinCash));
            return sb.ToString();
        }
    }

    public static class BalanceRunner
    {
        public static BalanceReport Run(GameData gd, long seed, int weeks, bool skipTutorial = true, Action<Simulation> eachWeek = null)
        {
            var sim = new Simulation(gd, seed);
            if (skipTutorial) sim.EndTutorial();
            var rep = new BalanceReport();
            while (true)
            {
                eachWeek?.Invoke(sim);
                PlayWeek(sim, rep);
                rep.Weeks.Add(sim.S.History[sim.S.History.Count - 1]);
                rep.CashAfter.Add(sim.S.Cash);
                rep.PrestigeAfter.Add(sim.S.Prestige);
                rep.Students.Add(sim.TotalStudents);
                if (rep.Weeks.Count >= weeks) break;
                Manage(sim, rep, true);
                sim.NextWeek();
            }
            rep.Cash = sim.S.Cash; rep.Prestige = sim.S.Prestige; rep.Chapter = sim.Chapter; rep.Buildings = sim.S.Buildings.Count;
            rep.Profs = sim.S.Profs.Count; rep.ResearchDone = sim.S.Research.Count;
            return rep;
        }

        public static void PlayWeek(Simulation sim, BalanceReport rep)
        {
            Manage(sim, rep, true);
            sim.StartWeek();
            int guard = 0;
            while (sim.Phase == Phases.Live && guard++ < 100000)
            {
                sim.AdvanceMinutes(10);
                sim.AdvanceResearch(10 / sim.Gd.GameMinPerRealS);   // upgrades run on real time
                sim.DrainEvents();
                Manage(sim, rep, false);
                if (double.IsNaN(sim.S.Cash) || double.IsInfinity(sim.S.Cash)) rep.Finite = false;
                rep.MinCash = Math.Min(rep.MinCash, sim.S.Cash);
            }
            sim.DrainEvents();
        }

        static void Manage(Simulation sim, BalanceReport rep, bool calm)
        {
            var s = sim.S;
            var gd = sim.Gd;
            if (s.Till > 300) sim.CollectTill();
            if (!calm) return;
            double buffer = Math.Max(1500, sim.DayIncome() * 3);
            if (s.Phase == Phases.Report && sim.ExamWeek) sim.SetIntake(Seats(sim) >= MaxCohort(sim) * 1.2 ? Intakes.Grow : Intakes.Keep);
            // zones first (the chapter goal)
            foreach (var z in gd.Zones)
                if (sim.CanBuy("zone:" + z.Id).Ok && s.Cash - z.Cost > buffer * 0.5) { sim.Buy("zone:" + z.Id); rep.Log.Add("week " + s.Week + " zone " + z.Id); }
            // professors: one per faculty, a second when the faculty teaches more than 8 classes a week
            foreach (var f in gd.Faculties)
            {
                int have = s.Profs.FindAll(p => p.Faculty == f.Index).Count;
                int classes = 0;
                for (int c = 0; c < gd.Courses.Count; c++) if (sim.CourseOn(c) && sim.FacultyIndexOfCourse(c) == f.Index) classes += gd.Courses[c].PerWeek;
                if (classes == 0) continue;
                if ((have == 0 || classes > 8 * have) && sim.CanBuy("professor:" + f.Id).Ok && (have == 0 || s.Cash - gd.ProfHire > buffer)) sim.Buy("professor:" + f.Id);
            }
            for (int c = 0; c < gd.Courses.Count; c++)
                if (sim.CourseOn(c) && s.CourseProf[c] < 0)
                    for (int p = 0; p < s.Profs.Count; p++) if (sim.AssignProf(c, p) == "") break;
            // gates are cheap shortcuts
            foreach (var e in gd.Edges) if (e.Type == "gate" && sim.CanBuy("gate:" + e.Index).Ok && s.Cash > buffer) sim.Buy("gate:" + e.Index);
            if (s.Phase == Phases.Plan)
            {
                sim.AutoFill(sim.SmartFill);
                var last = s.History.Count > 0 ? s.History[s.History.Count - 1] : null;
                // rooms: build where classes do not fit, a lab for lab courses
                if (sim.Missing > 0 || (last != null && Seats(sim) < sim.TotalStudents * 0.8)) BuildFirst(sim, rep, n => gd.Building(n.Building).Teaching && NeedsRoom(sim, gd.Building(n.Building)), buffer * 0.3);
                if (last != null && last.Unfed > 40) { if (!BuildFirst(sim, rep, n => n.Building == "canteen", buffer)) Hire(sim, Depts.Canteen, buffer); }
                if (last != null && last.LateRate > 0.08)
                {
                    if (!BuildFirst(sim, rep, n => n.Building == "dorm", buffer)) Hire(sim, Depts.Marshals, buffer);
                }
                if (sim.BuiltOf("library").Count == 0) BuildFirst(sim, rep, n => n.Building == "library", buffer);
                int labs = 0;
                foreach (var p in s.Plan) if (gd.Courses[p.Course].Room == Rooms.Lab) labs++;
                if (labs > 0 && sim.Staff(Depts.LabTechs) < 2) Hire(sim, Depts.LabTechs, buffer * 0.5);
                if (sim.ExamWeek && sim.Staff(Depts.Registry) < 3) Hire(sim, Depts.Registry, buffer * 0.5);
                if (s.Researching == "")
                    foreach (var r in gd.Research)
                        if (sim.CanResearch(r.Id) == "" && s.Cash - r.Cost > buffer) { sim.StartResearch(r.Id); rep.Log.Add("week " + s.Week + " research " + r.Id); break; }
                if (sim.Has("staggered_bells") && last != null && last.LateRate > 0.05) sim.SetStaggered(true);
                if (sim.Has("wide_paths") && last != null && last.LateRate > 0.05)
                    foreach (var e in gd.Edges) if (sim.CanBuy("widen:" + e.Index).Ok && s.Cash - gd.WidenCost > buffer * 1.5) { sim.Buy("widen:" + e.Index); break; }
                sim.AutoFill(sim.SmartFill);
            }
        }

        static bool NeedsRoom(Simulation sim, BuildingDef def)
        {
            if (def.Room == Rooms.Lab) { for (int c = 0; c < sim.Gd.Courses.Count; c++) if (sim.CourseOn(c) && sim.Gd.Courses[c].Room == Rooms.Lab) return true; return false; }
            return true;
        }

        static int MaxCohort(Simulation sim)
        {
            int n = 0;
            for (int c = 0; c < sim.S.Cohorts.Count; c++) if (sim.CohortOn(c)) n = Math.Max(n, sim.S.Cohorts[c].Size);
            return n;
        }

        static int Seats(Simulation sim)
        {
            int n = 0;
            foreach (var b in sim.S.Buildings) if (b.Built && sim.DefOf(b).Teaching) n = Math.Max(n, sim.DefOf(b).Seats);
            return n;
        }

        static bool BuildFirst(Simulation sim, BalanceReport rep, Func<NodeDef, bool> want, double buffer)
        {
            foreach (var n in sim.Gd.Nodes)
            {
                if (n.Kind != "site" || !want(n)) continue;
                var c = sim.CanBuy("build:" + n.Id);
                if (c.Ok && sim.S.Cash - c.Cost > buffer) { sim.Buy("build:" + n.Id); rep.Log.Add("week " + sim.S.Week + " build " + n.Id); return true; }
            }
            return false;
        }

        static void Hire(Simulation sim, string dept, double buffer)
        {
            var c = sim.CanBuy("staff:" + dept);
            if (c.Ok && sim.S.Cash - c.Cost > buffer) sim.Buy("staff:" + dept);
        }
    }
}
