// Semester Rush sim core — engine independent (no UnityEngine). C# 9 / .NET Standard 2.1.
// One career: a young campus. A week = Mon..Fri, five class slots a day with a 10-minute bell change before each one.
//   plan   (Sunday, paused)  — the timetable board (course × day × slot × room), purchases, staff;
//   live                     — bell changes (students walk the path graph; congestion makes them late), classes earn
//                              tuition and grades, lunch at 12:00 / 13:30 (canteen seats), crises;
//   report (Friday 16:20)    — weekly grades -> prestige, wages + upkeep; exam week ends the semester (intake choice).
// Partial class: this file = construction, time, queries; Timetable.cs = placements, rules, auto-fill;
// Bell.cs = bell-change flows, lunch, classes; Economy.cs = week end, semester, purchases, research, offline, rewards;
// Crises.cs = events.
using System;
using System.Collections.Generic;

namespace SemesterRush.Sim
{
    public sealed class SimEvent
    {
        public string Type;     // bell, class_done, lunch, crisis, decision, built, zone, research_done, day_done, week_done, ...
        public int A = -1, B = -1;
        public double V;
        public string Text = "";
        public override string ToString() => Type + "(" + A + "," + B + "," + V + "," + Text + ")";
    }

    public sealed partial class Simulation
    {
        public readonly GameData Gd;
        public State S;
        public readonly Rng Rng;
        readonly List<SimEvent> events = new List<SimEvent>();
        readonly Dictionary<string, bool> perks = new Dictionary<string, bool>();
        PathGraph graph;

        public Simulation(GameData gd, long seed)
        {
            Gd = gd ?? throw new ArgumentNullException(nameof(gd));
            S = new State { Seed = seed <= 0 ? 1 : seed };
            Rng = new Rng(S.Seed);
            NewCareer();
            S.RngState = Rng.State;
        }

        /// <summary>From a decoded save (SaveCodec).</summary>
        public Simulation(GameData gd, State s)
        {
            Gd = gd ?? throw new ArgumentNullException(nameof(gd));
            S = s;
            Rng = new Rng(1) { State = s.RngState <= 0 ? 1 : s.RngState };
            Normalize();
            RebuildGraph();
        }

        public JObj Snapshot()
        {
            S.RngState = Rng.State;
            return S.ToJson();
        }

        void NewCareer()
        {
            S.Week = 1; S.Day = 0;
            S.Minute = Gd.DayStart;
            S.Phase = Phases.Plan;
            S.Cash = Gd.StartCash;
            S.Prestige = Gd.StartPrestige;
            foreach (var z in Gd.StartZones) S.Zones.Add(z);
            foreach (var n in Gd.Nodes)
                if (n.Kind == "building") S.Buildings.Add(new BuildingState { Node = n.Index, Def = n.Building });
            foreach (var d in Depts.All) S.Staff[d] = Gd.StartStaff.TryGetValue(d, out int k) ? k : 0;
            Normalize();
            foreach (var f in Gd.StartProfs) AddProf(Gd.Faculty(f).Index, false);
            foreach (var c in Gd.Cohorts) if (Unlocked(Gd.Faculty(c.Faculty).Zone)) Enroll(c.Index);
            // the previous dean's timetable: everything but the guide's first class (Monday 09:00, the main hall)
            int guide = Gd.CourseIndex(Gd.GuideCourse);
            for (int ci = 0; ci < Gd.Courses.Count; ci++) if (CohortOn(Gd.Courses[ci].CohortIndex) && ci != guide) S.CourseProf[ci] = ProfFor(ci);
            RebuildGraph();
            int hall = MainHall();
            AutoFill(false, new Placement { Course = guide, Day = Gd.GuideDay, Slot = Gd.GuideSlot, Node = hall });
            S.ThisWeek = NewWeekStats();
        }

        /// <summary>Sizes every per-course / per-cohort list to the data (new saves, old saves after a data update).</summary>
        void Normalize()
        {
            while (S.Cohorts.Count < Gd.Cohorts.Count) S.Cohorts.Add(new CohortState());
            while (S.CourseProf.Count < Gd.Courses.Count) S.CourseProf.Add(-1);
            while (S.WeekScore.Count < Gd.Courses.Count) S.WeekScore.Add(0);
            while (S.WeekHeld.Count < Gd.Courses.Count) S.WeekHeld.Add(0);
            foreach (var c in S.Cohorts) if (c.Late == null || c.Late.Length != Gd.SlotCount) c.Late = new double[Gd.SlotCount];
            foreach (var d in Depts.All) if (!S.Staff.ContainsKey(d)) S.Staff[d] = 0;
            if (S.ThisWeek.Cohort.Length != Gd.Cohorts.Count) S.ThisWeek.Cohort = new double[Gd.Cohorts.Count];
        }

        WeekStats NewWeekStats() => new WeekStats { Week = S.Week, Exam = ExamWeek, Cohort = new double[Gd.Cohorts.Count] };

        void Enroll(int cohort)
        {
            var c = S.Cohorts[cohort];
            if (c.Enrolled) return;
            c.Enrolled = true;
            c.Size = Gd.Cohorts[cohort].Size;
            c.Aptitude = 1;
        }

        int AddProf(int faculty, bool assign)
        {
            var f = Gd.Faculties[faculty];
            int k = S.Profs.FindAll(p => p.Faculty == faculty).Count;
            string name = f.Profs.Find(nm => !S.Profs.Exists(p => p.Name == nm)) ?? f.Profs[k % f.Profs.Count];
            S.Profs.Add(new ProfState { Name = name, Faculty = faculty, Skill = Gd.ProfSkill[Math.Min(k, Gd.ProfSkill.Length - 1)] });
            int idx = S.Profs.Count - 1;
            if (assign)
                for (int ci = 0; ci < Gd.Courses.Count; ci++)
                    if (S.CourseProf[ci] < 0 && FacultyIndexOfCourse(ci) == faculty) AssignProf(ci, idx);
            return idx;
        }

        /// <summary>The faculty's least-loaded professor for a course (start state / bot), -1 when none.</summary>
        int ProfFor(int course)
        {
            int fac = FacultyIndexOfCourse(course), best = -1, bestLoad = int.MaxValue;
            for (int p = 0; p < S.Profs.Count; p++)
            {
                if (S.Profs[p].Faculty != fac || S.Profs[p].Leaving) continue;
                int load = 0;
                for (int c = 0; c < S.CourseProf.Count; c++) if (S.CourseProf[c] == p) load++;
                if (load < bestLoad) { bestLoad = load; best = p; }
            }
            return best;
        }

        // ------------------------------------------------------------------------------------------------ queries

        public List<SimEvent> DrainEvents()
        {
            var o = new List<SimEvent>(events);
            events.Clear();
            return o;
        }

        void Emit(string type, int a = -1, int b = -1, double v = 0, string text = "") => events.Add(new SimEvent { Type = type, A = a, B = b, V = v, Text = text ?? "" });

        public string Phase => S.Phase;
        public PathGraph Graph => graph;
        public bool Has(string research) => S.Research.Contains(research);
        public bool Unlocked(string zone) => S.Zones.Contains(zone);
        public bool Perk(string id) => perks.TryGetValue(id, out bool b) && b;
        public int Chapter
        {
            get
            {
                int c = 1;
                foreach (var z in Gd.Zones) if (Unlocked(z.Id)) c = Math.Max(c, z.Chapter);
                return c;
            }
        }
        public int MaxSpeed => Perk("speed") ? Gd.PaidMaxSpeed : Gd.FreeMaxSpeed;
        public int Semester => (S.Week - 1) / Math.Max(1, Gd.WeeksPerSemester) + 1;
        public int WeekOfSemester => (S.Week - 1) % Math.Max(1, Gd.WeeksPerSemester) + 1;
        public bool ExamWeek => WeekOfSemester == Gd.WeeksPerSemester;
        public int AbsDay => (S.Week - 1) * Gd.DaysPerWeek + S.Day;
        public double PrestigeMult => Gd.PrestigeMultMin + Gd.PrestigeMultSpan * Math.Max(0, Math.Min(Gd.PrestigeMax, S.Prestige)) / Gd.PrestigeMax;
        public int Staff(string dept) => S.Staff.TryGetValue(dept, out int n) ? n : 0;
        public bool CohortOn(int c) => c >= 0 && c < S.Cohorts.Count && S.Cohorts[c].Enrolled && S.Cohorts[c].Size > 0;
        public int FacultyIndexOfCourse(int course) => Gd.Faculty(Gd.Cohorts[Gd.Courses[course].CohortIndex].Faculty).Index;
        public bool CourseOn(int course) => course >= 0 && course < Gd.Courses.Count && CohortOn(Gd.Courses[course].CohortIndex);
        public int TotalStudents { get { int n = 0; for (int c = 0; c < S.Cohorts.Count; c++) if (CohortOn(c)) n += S.Cohorts[c].Size; return n; } }

        public BuildingState BuildingAt(int node) => S.Buildings.Find(b => b.Node == node);
        public BuildingDef DefOf(BuildingState b) => Gd.Building(b.Def);
        public bool IsOpen(int node) { var b = BuildingAt(node); return b != null && b.Built; }
        public bool IsClosedNow(int node, double minute) { var b = BuildingAt(node); return b == null || !b.Built || b.ClosedUntil > minute; }
        public int MainHall() { foreach (var b in S.Buildings) if (b.Built && DefOf(b)?.Kind == "hall") return b.Node; return -1; }

        public List<int> BuiltOf(string kind)
        {
            var o = new List<int>();
            foreach (var b in S.Buildings) if (b.Built && DefOf(b)?.Kind == kind) o.Add(b.Node);
            return o;
        }

        public int Beds { get { int n = 0; foreach (var b in S.Buildings) if (b.Built) n += DefOf(b)?.Beds ?? 0; return n; } }
        public double ResidentShare { get { int t = TotalStudents; return t <= 0 ? 0 : Math.Min(1.0, (double)Beds / t); } }

        /// <summary>Lunch seats per lunch slot: canteens × (1 + crew bonus) + food trucks.</summary>
        public double Meals
        {
            get
            {
                double m = 0;
                foreach (var b in S.Buildings) if (b.Built) m += DefOf(b)?.Meals ?? 0;
                m *= 1 + Gd.CrewMeals * Staff(Depts.Canteen);
                if (Has("food_trucks")) m += Gd.FoodTruckMeals;
                return m;
            }
        }

        public bool InBell => S.Phase == Phases.Live && BellSlotAt(S.Minute) >= 0;
        /// <summary>The slot whose bell change is running at this minute, -1 none.</summary>
        public int BellSlotAt(double minute)
        {
            for (int k = 0; k < Gd.SlotCount; k++) if (minute >= Gd.Slots[k] - Gd.BellMin - 1e-9 && minute < Gd.Slots[k] - 1e-9) return k;
            return -1;
        }
        /// <summary>The slot whose class runs at this minute, -1 none.</summary>
        public int ClassSlotAt(double minute)
        {
            for (int k = 0; k < Gd.SlotCount; k++) if (minute >= Gd.Slots[k] - 1e-9 && minute < Gd.Slots[k] + Gd.ClassMin - 1e-9) return k;
            return -1;
        }

        // ------------------------------------------------------------------------------------------------ time

        public void SetSpeed(int speed) => S.Speed = Math.Max(1, Math.Min(MaxSpeed, speed));
        public void SetPaused(bool paused) => S.Paused = paused;

        /// <summary>Store perks (never saved): speed, automation, offline_plus.</summary>
        public void SetPerks(Dictionary<string, bool> p)
        {
            perks.Clear();
            if (p != null) foreach (var kv in p) perks[kv.Key] = kv.Value;
            if (S.Speed > MaxSpeed) S.Speed = MaxSpeed;
        }

        /// <summary>Game minutes per real second now: slow during a bell change so the walk can be watched.</summary>
        public double MinutesPerSecond => (InBell ? Gd.BellMinPerRealS : Gd.GameMinPerRealS) * Math.Max(1, S.Speed);

        /// <summary>Every frame with real seconds. Research runs on real time in every phase; the week only while live.</summary>
        public void Advance(double realDt)
        {
            if (realDt <= 0 || double.IsNaN(realDt)) return;
            realDt = Math.Min(realDt, 1.0);   // a hitch never jumps the sim
            TickResearch(realDt);
            if (S.Phase != Phases.Live || S.Paused) return;
            double left = realDt;
            int guard = 0;
            while (left > 1e-9 && S.Phase == Phases.Live && guard++ < 200)
            {
                double mps = MinutesPerSecond;
                double step = Math.Min(1.0, left * mps);
                Step(step);
                left -= step / mps;
            }
        }

        /// <summary>Advances game minutes directly (tests, the guide's fast-forward, the balance bot). Stops at the week end.</summary>
        public void AdvanceMinutes(double minutes)
        {
            while (minutes > 1e-9 && S.Phase == Phases.Live)
            {
                double step = Math.Min(1.0, minutes);
                Step(step);
                minutes -= step;
            }
        }

        void Step(double dt)
        {
            double from = S.Minute, to = S.Minute + dt;
            RunPlanned(to);
            Construction(dt);
            for (int k = 0; k < Gd.SlotCount; k++)
            {
                double bell = Gd.Slots[k] - Gd.BellMin, start = Gd.Slots[k], end = Gd.Slots[k] + Gd.ClassMin;
                if (Crossed(from, to, bell)) BellChange(k);
                if (Crossed(from, to, start)) { if (Gd.IsLunchSlot(k)) Lunch(k); Emit("class_start", k); }
                if (Crossed(from, to, end)) EndClasses(k);
            }
            if (S.Pending != null && to >= S.Pending.Deadline) Decide(S.Pending.Id, DefaultOption(S.Pending));
            S.Minute = to;
            if (S.Minute >= Gd.DayEnd - 1e-9) EndDay();
        }

        static bool Crossed(double from, double to, double t) => from < t - 1e-9 && to >= t - 1e-9;

        void Construction(double dt)
        {
            double speed = 1 + Gd.MaintBuildSpeed * Staff(Depts.Maintenance);
            foreach (var b in S.Buildings)
            {
                if (b.BuildLeft <= 0) continue;
                b.BuildLeft = Math.Max(0, b.BuildLeft - dt * speed);
                if (b.BuildLeft <= 0) { Emit("built", b.Node, -1, 0, b.Def); RebuildGraph(); }
            }
        }

        /// <summary>Plan -> live (Monday 08:50).</summary>
        public bool StartWeek()
        {
            if (S.Phase != Phases.Plan) return false;
            S.Phase = Phases.Live;
            S.Paused = false;
            S.Minute = Gd.DayStart;
            StartDayState();
            Emit("week_live", S.Week);
            return true;
        }

        void StartDayState()
        {
            foreach (var c in S.Cohorts) { c.Lunch = -1; c.Unfed = 0; for (int k = 0; k < c.Late.Length; k++) c.Late[k] = 0; }
            foreach (var b in S.Buildings) b.ClosedUntil = 0;
            S.LateToday = 0; S.TripsToday = 0;
            bell = null;
            PlanDay();
            if (Perk("automation") && AutoRepairOn) AutoRepair();
        }

        /// <summary>The Registrar's auto-repair (automation perk; the player can turn it off in the timetable).</summary>
        public bool AutoRepairOn = true;

        void EndDay()
        {
            S.Minute = Gd.DayEnd;
            if (S.TripsToday > 0 && S.LateToday == 0) S.ThisWeek.OnTimeDays++;
            EveningEvents();
            Emit("day_done", S.Day, S.LateToday, S.TripsToday);
            S.Events.RemoveAll(e => e.UntilDay <= AbsDay);
            foreach (var p in S.Profs) if (p.OffDay == AbsDay) p.OffDay = -1;
            if (S.Day < Gd.DaysPerWeek - 1)
            {
                S.Day++;
                S.Minute = Gd.DayStart;
                StartDayState();
                Emit("day_start", S.Day);
                return;
            }
            EndWeek();
        }
    }
}
