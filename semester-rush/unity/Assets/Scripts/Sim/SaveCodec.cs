// Semester Rush sim core — save codec (fleet §14). Encode = canonical JSON of the whole career (a live day included: a bell
// change in progress, a closed hall, a hall under construction, an open decision, the week report, the guide). Decode
// never throws and never touches a running game; it accepts every state the sim can produce (negative cash after a bad
// week, an empty timetable, a professor on sick leave) and refuses only what the sim can never write (unknown nodes /
// courses / research, indices out of range, another game's file).
// Keeping unreadable files (.unreadable copies, .bak fallback) is CoddyKit.SaveRepository's job.
using System;
using System.Collections.Generic;

namespace SemesterRush.Sim
{
    public sealed class DecodeResult
    {
        public bool Ok;
        public string Error = "";
        public Simulation Sim;
    }

    public static class SaveCodec
    {
        public static string Encode(Simulation sim) => Json.Write(sim.Snapshot());

        public static DecodeResult Decode(string text, GameData gd)
        {
            if (gd == null || !gd.Ok) return Fail("no_data");
            object raw = Json.TryParse(text);
            if (!(raw is JObj o)) return Fail("invalid_json");
            try { return Decode(o, gd); }
            catch (Exception e) { return Fail("decode_exception_" + e.GetType().Name); }
        }

        static DecodeResult Fail(string e) => new DecodeResult { Error = e };

        static DecodeResult Decode(JObj o, GameData gd)
        {
            if (J.S(o, "game") != "semester-rush") return Fail("wrong_game");
            int v = J.I(o, "v", -1);
            if (v < 1 || v > State.Version) return Fail("version");
            var s = new State
            {
                Seed = J.L(o, "seed", 1), RngState = J.L(o, "rng", 1), Week = J.I(o, "week", 1), Day = J.I(o, "day"), Minute = J.D(o, "minute", gd.DayStart),
                Phase = J.S(o, "phase", Phases.Plan), Speed = J.I(o, "speed", 1), Paused = J.B(o, "paused"), Cash = J.D(o, "cash"), Prestige = J.D(o, "prestige"),
                Till = J.D(o, "till"), Researching = J.S(o, "researching"), ResearchLeftS = J.D(o, "research_left"), Tutorial = J.B(o, "tutorial"),
                Staggered = J.B(o, "staggered"), ExtraMarshals = Math.Max(0, J.I(o, "extra_marshals")), ExamKit = J.B(o, "exam_kit"), Intake = J.S(o, "intake", Intakes.Keep),
                OfflinePending = J.D(o, "offline_pending"), LastSeen = J.L(o, "last_seen"), LateToday = Math.Max(0, J.I(o, "late_today")),
                TripsToday = Math.Max(0, J.I(o, "trips_today")), StudentHours = J.L(o, "student_hours"), BestGrade = J.D(o, "best_grade"), ZeroLateWeeks = J.I(o, "zero_late_weeks"),
            };
            if (!Finite(s.Minute) || !Finite(s.Cash) || !Finite(s.Prestige) || !Finite(s.Till) || !Finite(s.ResearchLeftS) || !Finite(s.OfflinePending)) return Fail("not_finite");
            if (s.Week < 1 || s.Week > 1000000) return Fail("week");
            if (s.Day < 0 || s.Day >= gd.DaysPerWeek) return Fail("day");
            if (!Phases.Valid(s.Phase)) return Fail("phase");
            if (s.Minute < gd.DayStart - 1 || s.Minute > gd.DayEnd + 1) return Fail("minute");
            if (s.Speed < 1 || s.Speed > 8) return Fail("counts");
            if (!Intakes.Valid(s.Intake)) s.Intake = Intakes.Keep;
            s.Zones = J.StrList(o, "zones");
            foreach (var z in s.Zones) if (gd.Zone(z) == null) return Fail("zone");
            if (s.Zones.Count == 0) return Fail("zone");
            s.Research = J.StrList(o, "research");
            foreach (var r in s.Research) if (!gd.ResearchById.ContainsKey(r)) return Fail("research");
            if (s.Researching != "" && !gd.ResearchById.ContainsKey(s.Researching)) return Fail("research");
            s.Seen = J.StrList(o, "seen");

            foreach (var b in J.Arr(o, "buildings") ?? new List<object>())
            {
                var bs = BuildingState.Read(b);
                if (bs.Node < 0 || bs.Node >= gd.Nodes.Count) return Fail("building_node");
                var n = gd.Nodes[bs.Node];
                if ((n.Kind != "site" && n.Kind != "building") || n.Building != bs.Def) return Fail("building_def");
                if (!Finite(bs.BuildLeft) || !Finite(bs.ClosedUntil)) return Fail("not_finite");
                s.Buildings.Add(bs);
            }
            foreach (int e in JW.Ints(o, "gates")) { if (e < 0 || e >= gd.Edges.Count || gd.Edges[e].Type != "gate") return Fail("gate"); if (!s.OpenGates.Contains(e)) s.OpenGates.Add(e); }
            foreach (int e in JW.Ints(o, "widened")) { if (e < 0 || e >= gd.Edges.Count) return Fail("widen"); if (!s.Widened.Contains(e)) s.Widened.Add(e); }
            var cohorts = J.Arr(o, "cohorts") ?? new List<object>();
            if (cohorts.Count > gd.Cohorts.Count) return Fail("cohorts");
            foreach (var c in cohorts) s.Cohorts.Add(CohortState.Read(c, gd.SlotCount));
            foreach (var p in J.Arr(o, "profs") ?? new List<object>())
            {
                var ps = ProfState.Read(p);
                if (ps.Faculty < 0 || ps.Faculty >= gd.Faculties.Count) return Fail("prof_faculty");
                s.Profs.Add(ps);
            }
            s.CourseProf = JW.Ints(o, "course_prof");
            if (s.CourseProf.Count > gd.Courses.Count) return Fail("course_prof");
            foreach (int p in s.CourseProf) if (p < -1 || p >= s.Profs.Count) return Fail("course_prof");
            if (J.Obj(o, "staff") is JObj st) foreach (var kv in st) { if (!gd.Staff.ContainsKey(kv.Key)) return Fail("staff"); s.Staff[kv.Key] = Math.Max(0, J.I(kv.Value)); }
            foreach (var key in new[] { "plan", "exams" })
                foreach (var p in J.Arr(o, key) ?? new List<object>())
                {
                    var pl = Placement.Read(p);
                    if (pl == null || pl.Course < 0 || pl.Course >= gd.Courses.Count || pl.Day < 0 || pl.Day >= gd.DaysPerWeek || pl.Slot < 0 || pl.Slot >= gd.SlotCount
                        || pl.Node < 0 || pl.Node >= gd.Nodes.Count) return Fail("placement");
                    (key == "plan" ? s.Plan : s.Exams).Add(pl);
                }
            s.WeekScore = JW.DblList(o, "week_score");
            s.WeekHeld = JW.Ints(o, "week_held");
            if (s.WeekScore.Count > gd.Courses.Count || s.WeekHeld.Count > gd.Courses.Count) return Fail("week_score");
            foreach (var e in J.Arr(o, "events") ?? new List<object>()) s.Events.Add(ActiveEvent.Read(e));
            foreach (var e in J.Arr(o, "planned") ?? new List<object>()) s.Planned.Add(PlannedEvent.Read(e));
            s.Pending = Decision.Read(J.Obj(o, "pending"));
            if (s.Pending != null && Simulation.Options(s.Pending.Id).Length == 0) s.Pending = null;
            s.ThisWeek = WeekStats.Read(J.Obj(o, "this_week"));
            foreach (var h in J.Arr(o, "history") ?? new List<object>()) s.History.Add(WeekStats.Read(h));
            if (J.Obj(o, "rewards") is JObj rw) foreach (var kv in rw) s.Rewards[kv.Key] = RewardUse.Read(kv.Value);
            return new DecodeResult { Ok = true, Sim = new Simulation(gd, s) };
        }

        static bool Finite(double d) => !double.IsNaN(d) && !double.IsInfinity(d);
    }
}
