// Semester Rush sim core — engine independent. The whole saved career (State) and its parts. Every class writes and reads
// itself (ToJson / Read); reads are lenient (defaults for missing fields) so older saves load (fleet §14: the codec accepts
// every state the sim can produce — negative cash after a bad week, a bell change in progress, a hall under construction,
// a closed hall, an open crisis decision, the week report, a guide step).
using System;
using System.Collections.Generic;

namespace SemesterRush.Sim
{
    public static class Phases
    {
        public const string Plan = "plan", Live = "live", Report = "report";
        public static bool Valid(string p) => p == Plan || p == Live || p == Report;
    }

    public static class Intakes
    {
        public const string Keep = "keep", Grow = "grow", Select = "select";
        public static bool Valid(string s) => s == Keep || s == Grow || s == Select;
    }

    static class JW
    {
        public static List<object> Arr(double[] a) { var l = new List<object>(); foreach (var x in a) l.Add(x); return l; }
        public static List<object> Arr(IEnumerable<double> a) { var l = new List<object>(); foreach (var x in a) l.Add(x); return l; }
        public static List<object> Arr(IEnumerable<int> a) { var l = new List<object>(); foreach (var x in a) l.Add((long)x); return l; }
        public static List<object> Arr(IEnumerable<string> a) { var l = new List<object>(); foreach (var x in a) l.Add(x); return l; }
        public static double[] Dbl(object o, string key, int len)
        {
            var r = new double[len];
            var a = J.Arr(o, key);
            if (a != null) for (int i = 0; i < len && i < a.Count; i++) r[i] = Finite(J.D(a[i]));
            return r;
        }
        public static List<double> DblList(object o, string key) { var r = new List<double>(); foreach (var x in J.Arr(o, key) ?? new List<object>()) r.Add(Finite(J.D(x))); return r; }
        public static List<int> Ints(object o, string key) { var r = new List<int>(); foreach (var x in J.Arr(o, key) ?? new List<object>()) r.Add(J.I(x)); return r; }
        public static double Finite(double d) => double.IsNaN(d) || double.IsInfinity(d) ? 0 : d;
        public static double D(object o, string key, double def = 0) => Finite(J.D(o, key, def));
    }

    /// <summary>One class session in the weekly timetable (or one exam in the exam plan): course × day × slot × room.</summary>
    public sealed class Placement
    {
        public int Course, Day, Slot, Node;
        public List<object> ToJson() => new List<object> { (long)Course, (long)Day, (long)Slot, (long)Node };
        public static Placement Read(object o)
        {
            var l = o as List<object>;
            if (l == null || l.Count < 4) return null;
            return new Placement { Course = J.I(l[0], -1), Day = J.I(l[1], -1), Slot = J.I(l[2], -1), Node = J.I(l[3], -1) };
        }
    }

    public sealed class BuildingState
    {
        public int Node;
        public string Def = "";
        public double BuildLeft;          // game minutes of construction left (0 = open)
        public double ClosedUntil;        // day minute a burst pipe keeps it shut (today only)
        public bool Built => BuildLeft <= 0;
        public JObj ToJson() => new JObj { ["node"] = (long)Node, ["def"] = Def, ["build"] = BuildLeft, ["closed"] = ClosedUntil };
        public static BuildingState Read(object o) => new BuildingState { Node = J.I(o, "node", -1), Def = J.S(o, "def"), BuildLeft = Math.Max(0, JW.D(o, "build")), ClosedUntil = JW.D(o, "closed") };
    }

    public sealed class ProfState
    {
        public string Name = "";
        public int Faculty;
        public double Skill = 0.8;
        public int OffDay = -1;           // absolute day index of a sick day
        public bool Leaving;              // poached: leaves at the week end
        public JObj ToJson() => new JObj { ["name"] = Name, ["fac"] = (long)Faculty, ["skill"] = Skill, ["off"] = (long)OffDay, ["leaving"] = Leaving };
        public static ProfState Read(object o) => new ProfState { Name = J.S(o, "name"), Faculty = J.I(o, "fac"), Skill = JW.D(o, "skill", 0.8), OffDay = J.I(o, "off", -1), Leaving = J.B(o, "leaving") };
    }

    public sealed class CohortState
    {
        public bool Enrolled;
        public int Size;
        public double Aptitude = 1;
        public double SemSum;             // weekly grades this semester
        public int SemWeeks;
        // today
        public int Lunch = -1;            // slot they ate in, -1 not yet, -2 no free lunch slot
        public double Unfed;              // share that found no seat at lunch
        public double[] Late;             // per slot: share late for that slot's class

        public JObj ToJson() => new JObj
        {
            ["on"] = Enrolled, ["size"] = (long)Size, ["apt"] = Aptitude, ["sem_sum"] = SemSum, ["sem_weeks"] = (long)SemWeeks,
            ["lunch"] = (long)Lunch, ["unfed"] = Unfed, ["late"] = JW.Arr(Late ?? new double[0]),
        };

        public static CohortState Read(object o, int slots) => new CohortState
        {
            Enrolled = J.B(o, "on"), Size = Math.Max(0, J.I(o, "size")), Aptitude = JW.D(o, "apt", 1), SemSum = JW.D(o, "sem_sum"), SemWeeks = Math.Max(0, J.I(o, "sem_weeks")),
            Lunch = J.I(o, "lunch", -1), Unfed = Math.Max(0, Math.Min(1, JW.D(o, "unfed"))), Late = JW.Dbl(o, "late", slots),
        };
    }

    public sealed class ActiveEvent
    {
        public string Id;
        public int UntilDay;              // absolute day index, inclusive
        public int Target = -1;
        public double Mult = 1;
        public JObj ToJson() => new JObj { ["id"] = Id, ["until"] = (long)UntilDay, ["target"] = (long)Target, ["mult"] = Mult };
        public static ActiveEvent Read(object o) => new ActiveEvent { Id = J.S(o, "id"), UntilDay = J.I(o, "until"), Target = J.I(o, "target", -1), Mult = JW.D(o, "mult", 1) };
    }

    public sealed class PlannedEvent
    {
        public string Id;
        public double At;                 // day minute today
        public JObj ToJson() => new JObj { ["id"] = Id, ["at"] = At };
        public static PlannedEvent Read(object o) => new PlannedEvent { Id = J.S(o, "id"), At = JW.D(o, "at") };
    }

    /// <summary>A crisis that asks the dean to choose (protest, poached professor). Default after the deadline.</summary>
    public sealed class Decision
    {
        public string Id = "";
        public int Target = -1;
        public double Deadline;
        public JObj ToJson() => new JObj { ["id"] = Id, ["target"] = (long)Target, ["deadline"] = Deadline };
        public static Decision Read(object o) => o == null ? null : new Decision { Id = J.S(o, "id"), Target = J.I(o, "target", -1), Deadline = JW.D(o, "deadline") };
    }

    public sealed class WeekStats
    {
        public int Week;
        public bool Exam;
        public double Tuition, Rent, Grants, EventIncome, ExamFees, Wages, Upkeep, Overtime, Concessions, Scholarships;
        public int Required, Held, Trips, Late, Unfed, OnTimeDays;
        public double Grade, PrestigeDelta, SemesterGrade;
        public double[] Cohort = new double[0];   // weekly grade per cohort (-1 = not enrolled)
        public double Income => Tuition + Rent + Grants + EventIncome + ExamFees;
        public double Costs => Wages + Upkeep + Overtime + Concessions + Scholarships;
        public double Net => Income - Costs;
        public double LateRate => Trips <= 0 ? 0 : (double)Late / Trips;

        public JObj ToJson() => new JObj
        {
            ["week"] = (long)Week, ["exam"] = Exam, ["tuition"] = Tuition, ["rent"] = Rent, ["grants"] = Grants, ["event"] = EventIncome, ["exam_fees"] = ExamFees,
            ["wages"] = Wages, ["upkeep"] = Upkeep, ["overtime"] = Overtime, ["concessions"] = Concessions, ["scholarships"] = Scholarships,
            ["required"] = (long)Required, ["held"] = (long)Held, ["trips"] = (long)Trips, ["late"] = (long)Late, ["unfed"] = (long)Unfed, ["on_time_days"] = (long)OnTimeDays,
            ["grade"] = Grade, ["prestige"] = PrestigeDelta, ["sem_grade"] = SemesterGrade, ["cohort"] = JW.Arr(Cohort),
        };

        public static WeekStats Read(object o)
        {
            var w = new WeekStats
            {
                Week = J.I(o, "week"), Exam = J.B(o, "exam"), Tuition = JW.D(o, "tuition"), Rent = JW.D(o, "rent"), Grants = JW.D(o, "grants"), EventIncome = JW.D(o, "event"),
                ExamFees = JW.D(o, "exam_fees"), Wages = JW.D(o, "wages"), Upkeep = JW.D(o, "upkeep"), Overtime = JW.D(o, "overtime"), Concessions = JW.D(o, "concessions"),
                Scholarships = JW.D(o, "scholarships"), Required = J.I(o, "required"), Held = J.I(o, "held"), Trips = J.I(o, "trips"), Late = J.I(o, "late"),
                Unfed = J.I(o, "unfed"), OnTimeDays = J.I(o, "on_time_days"), Grade = JW.D(o, "grade"), PrestigeDelta = JW.D(o, "prestige"), SemesterGrade = JW.D(o, "sem_grade"),
            };
            w.Cohort = JW.DblList(o, "cohort").ToArray();
            return w;
        }
    }

    public sealed class RewardUse
    {
        public long UtcDay;
        public int Count;
        public long LastAt;
        public JObj ToJson() => new JObj { ["day"] = UtcDay, ["n"] = (long)Count, ["at"] = LastAt };
        public static RewardUse Read(object o) => new RewardUse { UtcDay = J.L(o, "day"), Count = J.I(o, "n"), LastAt = J.L(o, "at") };
    }

    /// <summary>The saved career.</summary>
    public sealed class State
    {
        public const int Version = 1;
        public long Seed = 1, RngState = 1;
        public int Week = 1, Day;          // Day = 0..4 (Mon..Fri)
        public double Minute = 530;
        public string Phase = Phases.Plan;
        public int Speed = 1;
        public bool Paused;
        public double Cash, Prestige, Till;
        public List<string> Zones = new List<string>();
        public List<string> Research = new List<string>();
        public string Researching = "";
        public double ResearchLeftS;
        public List<BuildingState> Buildings = new List<BuildingState>();
        public List<int> OpenGates = new List<int>();
        public List<int> Widened = new List<int>();
        public List<CohortState> Cohorts = new List<CohortState>();
        public List<ProfState> Profs = new List<ProfState>();
        public List<int> CourseProf = new List<int>();
        public Dictionary<string, int> Staff = new Dictionary<string, int>();
        public List<Placement> Plan = new List<Placement>();
        public List<Placement> Exams = new List<Placement>();
        public List<double> WeekScore = new List<double>();   // per course: sum of session scores this week
        public List<int> WeekHeld = new List<int>();          // per course: sessions held this week
        public List<ActiveEvent> Events = new List<ActiveEvent>();
        public List<PlannedEvent> Planned = new List<PlannedEvent>();
        public Decision Pending;
        public WeekStats ThisWeek = new WeekStats { Week = 1 };
        public List<WeekStats> History = new List<WeekStats>();
        public bool Tutorial = true;       // guide running: no crises, prestige can only rise
        public bool Staggered;             // staggered bells policy (upgrade)
        public int ExtraMarshals;          // rewarded: marshals for the next bell change
        public bool ExamKit;               // exam kit offer: invigilators + backup generator this exam week
        public string Intake = Intakes.Keep;
        public Dictionary<string, RewardUse> Rewards = new Dictionary<string, RewardUse>();
        public double OfflinePending;
        public long LastSeen;
        public int LateToday, TripsToday;
        public long StudentHours;          // career: student-sessions taught
        public double BestGrade;
        public int ZeroLateWeeks;
        public List<string> Seen = new List<string>();

        public JObj ToJson()
        {
            var bs = new List<object>(); foreach (var b in Buildings) bs.Add(b.ToJson());
            var cs = new List<object>(); foreach (var c in Cohorts) cs.Add(c.ToJson());
            var ps = new List<object>(); foreach (var p in Profs) ps.Add(p.ToJson());
            var pl = new List<object>(); foreach (var p in Plan) pl.Add(p.ToJson());
            var ex = new List<object>(); foreach (var p in Exams) ex.Add(p.ToJson());
            var ev = new List<object>(); foreach (var e in Events) ev.Add(e.ToJson());
            var pe = new List<object>(); foreach (var e in Planned) pe.Add(e.ToJson());
            var hi = new List<object>(); foreach (var h in History) hi.Add(h.ToJson());
            var rw = new JObj(); foreach (var kv in Rewards) rw[kv.Key] = kv.Value.ToJson();
            var st = new JObj(); foreach (var kv in Staff) st[kv.Key] = (long)kv.Value;
            return new JObj
            {
                ["game"] = "semester-rush", ["v"] = (long)Version, ["seed"] = Seed, ["rng"] = RngState, ["week"] = (long)Week, ["day"] = (long)Day, ["minute"] = Minute,
                ["phase"] = Phase, ["speed"] = (long)Speed, ["paused"] = Paused, ["cash"] = Cash, ["prestige"] = Prestige, ["till"] = Till,
                ["zones"] = JW.Arr(Zones), ["research"] = JW.Arr(Research), ["researching"] = Researching, ["research_left"] = ResearchLeftS,
                ["buildings"] = bs, ["gates"] = JW.Arr(OpenGates), ["widened"] = JW.Arr(Widened), ["cohorts"] = cs, ["profs"] = ps, ["course_prof"] = JW.Arr(CourseProf),
                ["staff"] = st, ["plan"] = pl, ["exams"] = ex, ["week_score"] = JW.Arr(WeekScore), ["week_held"] = JW.Arr(WeekHeld), ["events"] = ev, ["planned"] = pe,
                ["pending"] = Pending?.ToJson(), ["this_week"] = ThisWeek.ToJson(), ["history"] = hi, ["tutorial"] = Tutorial, ["staggered"] = Staggered,
                ["extra_marshals"] = (long)ExtraMarshals, ["exam_kit"] = ExamKit, ["intake"] = Intake, ["rewards"] = rw, ["offline_pending"] = OfflinePending,
                ["last_seen"] = LastSeen, ["late_today"] = (long)LateToday, ["trips_today"] = (long)TripsToday, ["student_hours"] = StudentHours,
                ["best_grade"] = BestGrade, ["zero_late_weeks"] = (long)ZeroLateWeeks, ["seen"] = JW.Arr(Seen),
            };
        }
    }
}
