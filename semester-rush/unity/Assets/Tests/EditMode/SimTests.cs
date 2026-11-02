// Semester Rush — engine-free sim tests (NUnit). Same files run in the Unity Test Runner (EditMode) and under plain .NET
// (Tools/dotnet_check.sh). Data is read from Assets/Resources/data on disk.
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SemesterRush.Sim;

namespace SemesterRush.Sim.Tests
{
    public static class TestData
    {
        static GameData cached;

        public static string UnityDir()
        {
            string env = Environment.GetEnvironmentVariable("SEMESTERRUSH_UNITY");
            if (!string.IsNullOrEmpty(env) && Directory.Exists(env)) return env;
            // Unity Test Runner: the working directory is the project root
            string d = Directory.GetCurrentDirectory();
            for (int i = 0; i < 8 && d != null; i++)
            {
                if (Directory.Exists(Path.Combine(d, "Assets", "Resources", "data"))) return d;
                d = Path.GetDirectoryName(d);
            }
            return Directory.GetCurrentDirectory();
        }

        public static string Read(string rel) => File.ReadAllText(Path.Combine(UnityDir(), rel));

        public static GameData Gd()
        {
            if (cached != null) return cached;
            string dir = Path.Combine(UnityDir(), "Assets", "Resources", "data");
            cached = GameData.FromTexts(n => File.ReadAllText(Path.Combine(dir, n + ".json")));
            return cached;
        }

        public static Simulation NewSim(long seed = 7, bool tutorial = false)
        {
            var s = new Simulation(Gd(), seed);
            if (!tutorial) s.EndTutorial();
            return s;
        }

        /// <summary>Runs the live week until the given day minute (or the week end).</summary>
        public static void RunTo(Simulation s, int day, double minute)
        {
            int guard = 0;
            while (s.Phase == Phases.Live && (s.S.Day < day || (s.S.Day == day && s.S.Minute < minute)) && guard++ < 100000) s.AdvanceMinutes(1);
        }

        public static void RunWeek(Simulation s)
        {
            if (s.Phase == Phases.Plan) s.StartWeek();
            int guard = 0;
            while (s.Phase == Phases.Live && guard++ < 100000) s.AdvanceMinutes(10);
        }

        public static int Guide(Simulation s) => s.Gd.CourseIndex(s.Gd.GuideCourse);

        /// <summary>The gate next to the bus stop (the guide's shortcut).</summary>
        public static int Gate(Simulation s) => s.Gd.Edges.Find(e => e.Type == "gate" && (e.A == s.Gd.EntranceNode || e.B == s.Gd.EntranceNode)).Index;
    }

    public class DataTests
    {
        [Test]
        public void DataLoadsWithoutErrors()
        {
            var gd = TestData.Gd();
            Assert.IsTrue(gd.Ok, string.Join("; ", gd.Errors));
            Assert.AreEqual(3, gd.Zones.Count, "MVP chapters 1-3");
            Assert.AreEqual(5, gd.SlotCount);
            Assert.GreaterOrEqual(gd.Research.Count, 16);
            Assert.GreaterOrEqual(gd.Events.Count, 8, "8 crises (MVP)");
            Assert.AreEqual(5, Depts.All.Length, "five departments + the professors = six staff groups");
        }

        [Test]
        public void ResearchNeverLongerThanTwentyMinutes()
        {
            foreach (var r in TestData.Gd().Research) Assert.LessOrEqual(r.Minutes, 20, r.Id);
        }

        [Test]
        public void EveryNodeIsReachableOnceEverythingIsOpen()
        {
            var s = TestData.NewSim();
            s.S.Zones.Clear();
            foreach (var z in s.Gd.Zones) s.S.Zones.Add(z.Id);
            s.RebuildGraph();
            foreach (var n in s.Gd.Nodes) Assert.Less(s.Graph.Minutes(s.Gd.EntranceNode, n.Index), PathGraph.Inf, n.Id);
        }

        [Test]
        public void EveryCourseFitsSomeRoomTypeOnTheMap()
        {
            var gd = TestData.Gd();
            foreach (var c in gd.Courses)
            {
                bool room = false;
                foreach (var n in gd.Nodes) if (gd.Building(n.Building)?.Room == c.Room && gd.Zone(n.Zone).Chapter <= gd.Zone(gd.Faculty(gd.Cohorts[c.CohortIndex].Faculty).Zone).Chapter) room = true;
                Assert.IsTrue(room, c.Id);
            }
        }
    }

    public class CoreTests
    {
        [Test]
        public void NewCareerStartsInThePlanWithTheGuideCellFree()
        {
            var s = TestData.NewSim(tutorial: true);
            Assert.AreEqual(Phases.Plan, s.Phase);
            Assert.AreEqual(1, s.S.Week);
            Assert.AreEqual(110, s.TotalStudents, "two Business cohorts");
            Assert.AreEqual(1, s.S.Profs.Count);
            int g = TestData.Guide(s);
            Assert.AreEqual(-1, s.S.CourseProf[g], "the guide gives Business 101 its professor");
            Assert.AreEqual(1, s.Missing, "the previous dean left one class for the guide");
            Assert.AreEqual(-1, s.At(s.Gd.GuideDay, s.Gd.GuideSlot, s.MainHall()));
            Assert.AreEqual("", s.CanPlace(g, s.Gd.GuideDay, s.Gd.GuideSlot, s.MainHall()));
        }

        [Test]
        public void TimeDoesNotMoveInThePlanOrWhilePaused()
        {
            var s = TestData.NewSim();
            double m = s.S.Minute;
            s.Advance(1.0);
            Assert.AreEqual(m, s.S.Minute, 1e-9);
            s.StartWeek();
            s.SetPaused(true);
            s.Advance(1.0);
            Assert.AreEqual(m, s.S.Minute, 1e-9);
            s.SetPaused(false);
            s.Advance(1.0);
            Assert.Greater(s.S.Minute, m);
        }

        [Test]
        public void BellChangesRunSlowerThanClasses()
        {
            var s = TestData.NewSim();
            s.StartWeek();
            TestData.RunTo(s, 0, 531);
            Assert.IsTrue(s.InBell, "Monday starts with the 08:50 bell");
            double bellRate = s.MinutesPerSecond;
            TestData.RunTo(s, 0, 545);
            Assert.IsFalse(s.InBell);
            Assert.Greater(s.MinutesPerSecond, bellRate * 2, "the walk can be watched");
        }

        [Test]
        public void AWeekRunsMondayToFridayAndEndsInAReport()
        {
            var s = TestData.NewSim();
            var days = new List<int>();
            s.StartWeek();
            int guard = 0;
            while (s.Phase == Phases.Live && guard++ < 10000)
            {
                s.AdvanceMinutes(10);
                foreach (var e in s.DrainEvents()) if (e.Type == "day_done") days.Add(e.A);
            }
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, days);
            Assert.AreEqual(Phases.Report, s.Phase);
            Assert.AreEqual(1, s.S.History.Count);
            Assert.IsTrue(s.NextWeek());
            Assert.AreEqual(2, s.S.Week);
            Assert.AreEqual(Phases.Plan, s.Phase);
        }
    }

    public class TimetableTests
    {
        [Test]
        public void RoomsCohortsAndProfessorsCannotBeDoubleBooked()
        {
            var s = TestData.NewSim();
            s.S.Cash = 50000;
            int g = TestData.Guide(s), hall = s.MainHall();
            Assert.AreEqual("", s.Buy("build:site_hall_east"));
            int east = s.Gd.Node("site_hall_east");
            s.BuildingAt(east).BuildLeft = 0;
            Assert.AreEqual("", s.Place(g, 0, 0, hall));
            Assert.AreEqual("enough", s.CanPlace(g, 3, 4, east), "a course meets its weekly count, no more");
            var p = s.S.Plan.Find(x => x.Course != g);
            int otherCohortCourse = s.Gd.Courses.FindIndex(c => c.CohortIndex != s.Gd.Courses[p.Course].CohortIndex && c.CohortIndex >= 0 && s.CourseOn(c.Index));
            s.Remove(s.S.Plan.FindIndex(x => x.Course == otherCohortCourse && !(x.Day == p.Day && x.Slot == p.Slot)));
            Assert.AreEqual("room_busy", s.CanPlace(otherCohortCourse, p.Day, p.Slot, p.Node));
            Assert.AreEqual("not_room", s.CanPlace(1, 4, 4, s.Gd.Node("canteen")), "a canteen is not a classroom");
            // the same cohort in two rooms at once
            int sameCohort = s.Gd.Courses.FindIndex(c => c.CohortIndex == s.Gd.Courses[p.Course].CohortIndex && c.Index != p.Course);
            s.Remove(s.S.Plan.FindIndex(x => x.Course == sameCohort && !(x.Day == p.Day && x.Slot == p.Slot)));
            Assert.AreEqual("cohort_busy", s.CanPlace(sameCohort, p.Day, p.Slot, east));
        }

        [Test]
        public void ProfessorTeachesAtMostThreeClassesADay()
        {
            var s = TestData.NewSim();
            s.S.Cash = 50000;
            Assert.AreEqual(3, s.Gd.ProfMaxPerDay);
            s.S.Plan.Clear();
            s.Buy("build:site_hall_east");
            s.S.Buildings[s.S.Buildings.Count - 1].BuildLeft = 0;
            for (int c = 0; c < 4; c++) s.S.CourseProf[c] = 0;
            int hall = s.MainHall(), east = s.Gd.Node("site_hall_east");
            Assert.AreEqual("", s.Place(0, 0, 0, hall));
            Assert.AreEqual("", s.Place(1, 0, 1, hall));
            Assert.AreEqual("", s.Place(2, 0, 3, hall));
            Assert.AreEqual("prof_tired", s.CanPlace(3, 0, 4, east));
            Assert.AreEqual("prof_busy", s.CanPlace(3, 0, 0, east));
        }

        [Test]
        public void LabCoursesNeedALab()
        {
            var s = TestData.NewSim();
            s.Buy("zone:science");   // not affordable / prestige: force it
            s.S.Zones.Add("science"); s.S.Zones.Add("quad");
            int phy = s.Gd.CourseIndex("phy150");
            s.S.Cohorts[s.Gd.Courses[phy].CohortIndex].Enrolled = true;
            s.S.Cohorts[s.Gd.Courses[phy].CohortIndex].Size = 40;
            Assert.AreEqual("needs_lab", s.CanPlace(phy, 2, 2, s.MainHall()));
        }

        [Test]
        public void PastCellsCannotChangeWhileTheWeekRuns()
        {
            var s = TestData.NewSim();
            s.AutoFill(false);
            s.StartWeek();
            TestData.RunTo(s, 1, 700);
            Assert.IsTrue(s.Past(0, 4));
            Assert.IsTrue(s.Past(1, 1));
            Assert.IsFalse(s.Past(1, 2));
            int i = s.S.Plan.FindIndex(p => p.Day == 0);
            Assert.AreEqual("past", s.Remove(i));
        }

        [Test]
        public void AutoFillPlacesEveryClassThatFits()
        {
            var s = TestData.NewSim();
            Assert.AreEqual(1, s.Missing);
            Assert.AreEqual(1, s.AutoFill(false));
            Assert.AreEqual(0, s.Missing);
        }

        [Test]
        public void SmartAutoFillSpreadsLunch()
        {
            var s = TestData.NewSim();
            s.S.Plan.Clear();
            s.AutoFill(false);
            double basicHungry = 0, smartHungry = 0;
            for (int d = 0; d < 5; d++) basicHungry += Math.Max(0, s.LunchDemand(d, 2) - s.Meals);
            s.S.Plan.Clear();
            s.S.Research.Add("auto_fill_plus");
            s.AutoFill(true);
            for (int d = 0; d < 5; d++) smartHungry += Math.Max(0, s.LunchDemand(d, 2) - s.Meals);
            Assert.Greater(basicHungry, 0, "the basic fill leaves a 12:00 crunch (the guide's lunch lesson)");
            Assert.Less(smartHungry, basicHungry);
        }

        [Test]
        public void TheLunchFixMovesAClassIntoTwelveAndFeedsEveryone()
        {
            var s = TestData.NewSim();
            s.AutoFill(false);
            var fix = s.FindLunchFix();
            Assert.IsNotNull(fix);
            double before = s.LunchDemand(fix.Day, fix.Slot);
            Assert.Greater(before, s.Meals);
            Assert.AreEqual("", s.Move(fix.Placement, fix.Day, fix.Slot, fix.Node));
            Assert.LessOrEqual(s.LunchDemand(fix.Day, fix.Slot), s.Meals);
        }

        [Test]
        public void AssigningABusyProfessorIsRefused()
        {
            var s = TestData.NewSim();
            s.S.Cash = 50000;
            s.Buy("build:site_hall_east");
            s.S.Buildings[s.S.Buildings.Count - 1].BuildLeft = 0;
            s.S.Plan.Clear();
            int hall = s.MainHall(), east = s.Gd.Node("site_hall_east");
            s.S.CourseProf[0] = -1; s.S.CourseProf[2] = 0;
            s.Place(2, 1, 1, hall);
            s.Place(0, 1, 1, east);
            Assert.AreEqual("prof_busy", s.AssignProf(0, 0));
            Assert.AreEqual(-1, s.S.CourseProf[0]);
        }
    }

    public class BellTests
    {
        [Test]
        public void CommutersAreLateOnTheLongWayAndTheGateFixesIt()
        {
            var s = TestData.NewSim();
            s.AutoFill(false);
            int slot = s.S.Plan.Find(p => p.Day == 0).Slot;
            Assert.Greater(s.EstimateLate(0, slot), 0, "the bus stop to the main hall is the long way round");
            Assert.AreEqual("", s.Buy("gate:" + TestData.Gate(s)));
            Assert.AreEqual(0, s.EstimateLate(0, slot), "the gate is the shortcut");
        }

        [Test]
        public void CrowdsOnOnePathSlowEachOtherDown()
        {
            var s = TestData.NewSim();
            Assert.AreEqual(0, s.LateShare(7.0));
            Assert.AreEqual(1, s.LateShare(14.0));
            Assert.That(s.LateShare(10.0), Is.InRange(0.4, 0.6));
            s.AutoFill(false);
            s.Buy("gate:" + TestData.Gate(s));
            var p = s.S.Plan.Find(x => x.Day == 0 && x.Slot == 0);
            s.S.Cohorts[s.Gd.Courses[p.Course].CohortIndex].Size = 400;
            Assert.Greater(s.EstimateLate(0, 0), 0, "400 students on one gate path");
            s.S.Staff[Depts.Marshals] = 4;
            int withMarshals = s.EstimateLate(0, 0);
            s.S.Staff[Depts.Marshals] = 0;
            Assert.Less(withMarshals, s.EstimateLate(0, 0), "marshals widen the busiest paths");
        }

        [Test]
        public void DormResidentsWalkFromTheDorm()
        {
            var s = TestData.NewSim();
            s.S.Cash = 50000;
            s.Buy("build:site_dorm_a");
            s.S.Buildings[s.S.Buildings.Count - 1].BuildLeft = 0;
            Assert.Greater(s.ResidentShare, 0.5);
            s.AutoFill(false);
            s.StartWeek();
            TestData.RunTo(s, 0, 535);
            var b = s.Bell;
            Assert.IsNotNull(b);
            Assert.IsTrue(b.Trips.Exists(t => t.From == s.Gd.Node("site_dorm_a")));
        }

        [Test]
        public void LunchSeatsLimitWhoEats()
        {
            var s = TestData.NewSim();
            s.AutoFill(false);
            s.StartWeek();
            TestData.RunTo(s, 0, 725);
            int ls = s.Gd.LunchSlots[0];
            double demand = s.LunchDemand(0, ls);
            var eaters = new List<CohortState>();
            for (int c = 0; c < s.S.Cohorts.Count; c++) if (s.CohortOn(c) && s.S.Cohorts[c].Lunch == ls) eaters.Add(s.S.Cohorts[c]);
            Assert.IsNotEmpty(eaters);
            if (demand > s.Meals) Assert.Greater(eaters[0].Unfed, 0);
        }

        [Test]
        public void ClassesEarnTuitionIntoTheTillAndGrades()
        {
            var s = TestData.NewSim();
            s.AutoFill(false);
            s.S.CourseProf[TestData.Guide(s)] = 0;
            s.StartWeek();
            TestData.RunTo(s, 0, 625);
            Assert.Greater(s.S.Till, 0);
            double cash = s.S.Cash;
            double got = s.CollectTill();
            Assert.Greater(got, 0);
            Assert.AreEqual(cash + got, s.S.Cash, 1e-6);
            Assert.Greater(s.S.WeekScore[s.S.Plan.Find(p => p.Day == 0 && p.Slot == 0).Course], 0);
        }

        [Test]
        public void AClassWithoutAProfessorDoesNotRun()
        {
            var s = TestData.NewSim();
            int g = TestData.Guide(s);
            s.Place(g, 0, 0, s.MainHall());
            s.StartWeek();
            TestData.RunTo(s, 0, 625);
            Assert.AreEqual(0, s.S.WeekHeld[g]);
        }
    }

    public class EconomyTests
    {
        [Test]
        public void WeekEndsWithGradesWagesAndPrestige()
        {
            var s = TestData.NewSim();
            s.AutoFill(false);
            s.S.CourseProf[TestData.Guide(s)] = 0;
            s.Buy("gate:" + TestData.Gate(s));
            double p0 = s.S.Prestige;
            TestData.RunWeek(s);
            var w = s.S.History[0];
            Assert.Greater(w.Grade, 0.5);
            Assert.Greater(w.Wages, 0);
            Assert.Greater(w.Upkeep, 0);
            Assert.Greater(w.Tuition, 0);
            Assert.AreEqual(8, w.Held);
            Assert.GreaterOrEqual(s.S.Prestige, p0);
        }

        [Test]
        public void TheFirstWeeksAndTheGuideNeverLowerPrestige()
        {
            var s = TestData.NewSim(tutorial: true);
            s.S.Plan.Clear();     // nothing taught: a terrible week
            double p0 = s.S.Prestige;
            TestData.RunWeek(s);
            Assert.AreEqual(p0, s.S.Prestige, 1e-9);
        }

        [Test]
        public void NoCrisesDuringTheGuideOrTheFirstWeek()
        {
            var seen = new List<string>();
            var first = TestData.NewSim(seed: 11);     // guide skipped: the first week is still calm
            first.AutoFill(false);
            first.StartWeek();
            while (first.Phase == Phases.Live) { first.AdvanceMinutes(10); foreach (var e in first.DrainEvents()) if (e.Type == "crisis") seen.Add(e.Text + "@1"); }
            var s = TestData.NewSim(tutorial: true);   // the guide never ends here: no crisis in three weeks
            s.AutoFill(false);
            for (int w = 0; w < 3; w++)
            {
                s.StartWeek();
                while (s.Phase == Phases.Live) { s.AdvanceMinutes(10); foreach (var e in s.DrainEvents()) if (e.Type == "crisis") seen.Add(e.Text + "@" + s.S.Week); }
                s.NextWeek();
            }
            CollectionAssert.IsEmpty(seen);
        }

        [Test]
        public void PurchasesCheckCashPrestigeAndZones()
        {
            var s = TestData.NewSim();
            Assert.AreEqual("prestige", s.CanBuy("zone:quad").Reason);
            Assert.AreEqual("locked", s.CanBuy("zone:science").Reason);
            Assert.AreEqual("locked", s.CanBuy("build:site_hall_q1").Reason);
            s.S.Prestige = 300;
            s.S.Cash = 100;
            Assert.AreEqual("cash", s.CanBuy("zone:quad").Reason);
            s.S.Cash = 50000;
            Assert.AreEqual("", s.Buy("zone:quad"));
            Assert.IsTrue(s.CohortOn(s.Gd.CohortIndex("law1")), "the quad's faculties enroll at once");
            Assert.AreEqual("", s.Buy("professor:law"));
            Assert.AreEqual(s.S.Profs.Count - 1, s.S.CourseProf[s.Gd.CourseIndex("law101")], "a new professor takes the faculty's open courses");
            Assert.AreEqual("research", s.CanBuy("widen:0").Reason);
            Assert.AreEqual("owned", s.CanBuy("build:site_library").Reason == "owned" ? "owned" : (s.Buy("build:site_library") == "" ? "owned" : "x"));
        }

        [Test]
        public void ConstructionTakesGameTimeAndCrewsWorkInParallel()
        {
            var s = TestData.NewSim();
            s.S.Cash = 50000;
            s.Buy("build:site_library");
            s.Buy("build:site_dorm_a");
            Assert.IsFalse(s.IsOpen(s.Gd.Node("site_library")));
            s.StartWeek();
            TestData.RunTo(s, 2, 900);
            Assert.IsTrue(s.IsOpen(s.Gd.Node("site_library")), "both build at once (no single-builder wall)");
            Assert.IsTrue(s.IsOpen(s.Gd.Node("site_dorm_a")));
        }

        [Test]
        public void ResearchRunsOnRealTimeAndCatchesUpOffline()
        {
            var s = TestData.NewSim();
            Assert.AreEqual("", s.StartResearch("signposts"));
            s.Advance(30);
            Assert.AreEqual("signposts", s.S.Researching);
            s.Touch(1_000_000);
            s.Offline(1_000_000 + 3600);
            Assert.IsTrue(s.Has("signposts"));
        }

        [Test]
        public void OfflinePayIsCappedAndScalesWithOfflinePlus()
        {
            var s = TestData.NewSim();
            s.S.History.Add(new WeekStats { Tuition = 4000 });
            s.Touch(1000);
            var o = s.Offline(1000 + 10 * 3600);
            Assert.IsNotNull(o);
            Assert.IsTrue(o.Capped);
            Assert.AreEqual(4, o.Hours, 1e-9);
            var s2 = TestData.NewSim();
            s2.S.History.Add(new WeekStats { Tuition = 4000 });
            s2.SetPerks(new Dictionary<string, bool> { { "offline_plus", true } });
            s2.Touch(1000);
            var o2 = s2.Offline(1000 + 10 * 3600);
            Assert.Greater(o2.Amount, o.Amount * 2);
        }

        [Test]
        public void GrantCashUsesTheUsualDayIncome()
        {
            var s = TestData.NewSim();
            s.S.History.Add(new WeekStats { Tuition = 5000 });
            double c = s.S.Cash;
            Assert.AreEqual(1000, s.GrantCash(1), 1e-6);
            Assert.AreEqual(c + 1000, s.S.Cash, 1e-6);
        }

        [Test]
        public void ExamWeekClosesTheSemesterAndTheIntakeApplies()
        {
            var s = TestData.NewSim();
            s.AutoFill(false);
            s.S.CourseProf[TestData.Guide(s)] = 0;
            s.Buy("gate:" + TestData.Gate(s));
            for (int w = 1; w < s.Gd.WeeksPerSemester; w++) { TestData.RunWeek(s); s.NextWeek(); }
            Assert.IsTrue(s.ExamWeek);
            Assert.AreEqual(0, s.Missing, "the registry drafts the exam plan");
            int before = s.TotalStudents;
            TestData.RunWeek(s);
            var w6 = s.S.History[s.S.History.Count - 1];
            Assert.IsTrue(w6.Exam);
            Assert.Greater(w6.SemesterGrade, 0.3);
            Assert.Greater(w6.Grants, 0);
            Assert.AreEqual("", s.SetIntake(Intakes.Grow));
            s.NextWeek();
            Assert.Greater(s.TotalStudents, before);
            Assert.AreEqual(2, s.Semester);
            Assert.IsFalse(s.ExamWeek);
        }
    }

    public class CrisisTests
    {
        [Test]
        public void SickProfessorGetsCoverOrASubstitute()
        {
            var s = TestData.NewSim();
            s.AutoFill(false);
            s.S.CourseProf[TestData.Guide(s)] = 0;
            s.StartWeek();
            Assert.IsTrue(s.Fire("sick_prof"));
            var p = s.S.Plan.Find(x => x.Day == 0);
            Assert.Less(s.Quality(p, out string note), s.S.Profs[0].Skill);
            Assert.AreEqual("substitute", note);
            Assert.Contains("sick_prof", s.ActiveCrises());
        }

        [Test]
        public void BurstPipeClosesARoomForAWhile()
        {
            var s = TestData.NewSim();
            s.AutoFill(false);
            s.StartWeek();
            Assert.IsTrue(s.Fire("burst_pipe"));
            Assert.IsTrue(s.IsClosedNow(s.MainHall(), s.S.Minute + 1));
            Assert.AreEqual("burst_pipe", s.HelpableCrisis());
        }

        [Test]
        public void DecisionsHaveADefaultAtTheDeadline()
        {
            var s = TestData.NewSim();
            s.AutoFill(false);
            s.StartWeek();
            Assert.IsTrue(s.Fire("protest"));
            Assert.IsNotNull(s.S.Pending);
            s.AdvanceMinutes(s.Gd.DecisionMin + 2);
            Assert.IsNull(s.S.Pending, "talk is the default");
            Assert.IsTrue(s.Active("protest"));
        }

        [Test]
        public void PoachedProfessorLeavesAtTheWeekEndUnlessRetained()
        {
            var s = TestData.NewSim();
            s.S.Cash = 10000;
            s.Buy("professor:business");
            s.StartWeek();
            Assert.IsTrue(s.Fire("poached"));
            int target = s.S.Pending.Target;
            Assert.AreEqual("", s.Decide("poached", "let_go"));
            Assert.IsTrue(s.S.Profs[target].Leaving);
            TestData.RunWeek(s);
            s.NextWeek();
            Assert.AreEqual(1, s.S.Profs.Count);
        }

        [Test]
        public void SnowSlowsEveryoneDown()
        {
            var s = TestData.NewSim();
            s.AutoFill(false);
            s.Buy("gate:" + TestData.Gate(s));
            s.StartWeek();
            int slot = s.S.Plan.Find(p => p.Day == 0 && p.Slot > 0)?.Slot ?? 1;
            int dry = s.EstimateLate(0, slot);
            s.Fire("snow_day");
            Assert.Greater(s.EstimateLate(0, slot), dry);
        }
    }

    public class RewardTests
    {
        [Test]
        public void RewardsAreNeverOfferedDuringTheGuide()
        {
            var s = TestData.NewSim(tutorial: true);
            foreach (var p in Simulation.RewardPlacements) Assert.IsFalse(s.RewardOffer(p, 1_800_000_000).Ok, p);
        }

        [Test]
        public void DailyCapAndCooldownPersistInTheCareer()
        {
            var s = TestData.NewSim();
            long now = 1_800_000_000;
            Assert.IsTrue(s.GrantReward("daily_bonus", now).Ok);
            Assert.AreEqual("cap", s.RewardOffer("daily_bonus", now + 60).Reason);
            var back = SaveCodec.Decode(SaveCodec.Encode(s), s.Gd).Sim;
            Assert.AreEqual("cap", back.RewardOffer("daily_bonus", now + 60).Reason, "a relaunch does not reset the cap");
            Assert.IsTrue(back.RewardOffer("daily_bonus", now + 86400).Ok, "next UTC day");
            s.StartWeek();
            Assert.IsTrue(s.GrantReward("extra_marshals", now).Ok);
            s.AdvanceMinutes(20);
            Assert.AreEqual("cooldown", s.RewardOffer("extra_marshals", now + 60).Reason);
        }

        [Test]
        public void CashRewardsStayUnderOneFifthOfTheCheapestPack()
        {
            var s = TestData.NewSim();
            s.S.History.Add(new WeekStats { Tuition = 5000 });
            s.S.OfflinePending = 100000;
            Assert.LessOrEqual(s.RewardOffer("offline_x2", 1_800_000_000).Amount, 0.2 * s.DayIncome() + 1);
            Assert.LessOrEqual(s.RewardOffer("daily_bonus", 1_800_000_000).Amount, 0.2 * s.DayIncome() + 1);
        }

        [Test]
        public void CityHelpOnlyDuringACrisisAndEndsIt()
        {
            var s = TestData.NewSim();
            s.AutoFill(false);
            s.StartWeek();
            Assert.AreEqual("context", s.RewardOffer("city_help", 1_800_000_000).Reason);
            s.Fire("snow_day");
            Assert.IsTrue(s.GrantReward("city_help", 1_800_000_000).Ok);
            Assert.IsFalse(s.Active("snow_day"));
        }

        [Test]
        public void ExtraMarshalsHelpTheNextBellOnly()
        {
            var s = TestData.NewSim();
            s.StartWeek();
            Assert.IsTrue(s.GrantReward("extra_marshals", 1_800_000_000).Ok);
            Assert.AreEqual(s.Gd.Rewards["extra_marshals"].Count, s.S.ExtraMarshals);
            s.AdvanceMinutes(1);
            TestData.RunTo(s, 0, 625);
            Assert.AreEqual(0, s.S.ExtraMarshals, "used by one bell change");
        }
    }

    public class SaveTests
    {
        [Test]
        public void RoundTripMidBellIsIdenticalAndKeepsPlaying()
        {
            var s = TestData.NewSim();
            s.AutoFill(false);
            s.S.Cash = 50000;
            s.Buy("build:site_dorm_a");
            s.StartWeek();
            TestData.RunTo(s, 1, 625);
            string a = SaveCodec.Encode(s);
            var r = SaveCodec.Decode(a, s.Gd);
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(a, SaveCodec.Encode(r.Sim));
            TestData.RunWeek(s);
            TestData.RunWeek(r.Sim);
            Assert.AreEqual(SaveCodec.Encode(s), SaveCodec.Encode(r.Sim), "deterministic after a reload");
        }

        [Test]
        public void DecoderRefusesWhatTheSimNeverWrites()
        {
            var s = TestData.NewSim();
            string good = SaveCodec.Encode(s);
            Assert.AreEqual("wrong_game", SaveCodec.Decode(good.Replace("\"semester-rush\"", "\"other\""), s.Gd).Error);
            Assert.AreEqual("phase", SaveCodec.Decode(good.Replace("\"phase\":\"plan\"", "\"phase\":\"lunch\""), s.Gd).Error);
            Assert.AreEqual("invalid_json", SaveCodec.Decode("{oops", s.Gd).Error);
            Assert.AreEqual("zone", SaveCodec.Decode(good.Replace("\"zones\":[\"college\"]", "\"zones\":[\"mars\"]"), s.Gd).Error);
        }

        [Test]
        public void DecoderAcceptsNegativeCashTheReportAndAnOpenDecision()
        {
            var s = TestData.NewSim();
            s.AutoFill(false);
            s.StartWeek();
            s.Fire("protest");
            s.S.Cash = -4321;
            var r = SaveCodec.Decode(SaveCodec.Encode(s), s.Gd);
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(-4321, r.Sim.S.Cash, 1e-9);
            Assert.AreEqual("protest", r.Sim.S.Pending.Id);
            TestData.RunWeek(s);
            Assert.IsTrue(SaveCodec.Decode(SaveCodec.Encode(s), s.Gd).Ok);
        }

        [Test]
        public void FuzzEveryTenMinutesOfAWeekDecodes()
        {
            // fleet §14: every state the sim really produces (bell changes, lunch, crises, construction, decisions)
            var s = TestData.NewSim(seed: 3);
            s.S.Cash = 40000;
            s.Buy("build:site_library");
            s.Buy("build:site_hall_east");
            s.AutoFill(false);
            s.S.CourseProf[TestData.Guide(s)] = 0;
            for (int week = 0; week < 3; week++)
            {
                s.StartWeek();
                if (week == 1) { s.Fire("burst_pipe"); s.Fire("protest"); s.Fire("sick_prof"); }
                int n = 0;
                while (s.Phase == Phases.Live)
                {
                    s.AdvanceMinutes(10);
                    if (++n % 3 != 0) continue;
                    var r = SaveCodec.Decode(SaveCodec.Encode(s), s.Gd);
                    Assert.IsTrue(r.Ok, "week " + s.S.Week + " day " + s.S.Day + " minute " + s.S.Minute + ": " + r.Error);
                }
                Assert.IsTrue(SaveCodec.Decode(SaveCodec.Encode(s), s.Gd).Ok, "report");
                s.NextWeek();
            }
        }
    }

    public class BalanceTests
    {
        [Test]
        public void SevenWeeksStayFiniteProfitableAndNeverStuck()
        {
            var rep = BalanceRunner.Run(TestData.Gd(), 1, 7);
            TestContext.WriteLine("BALANCE 7 weeks\n" + rep.Table());
            Assert.IsTrue(rep.Finite);
            Assert.Greater(rep.Cash, TestData.Gd().StartCash, "profitable");
            Assert.Greater(rep.MinCash, 0, "a sensible player never goes broke");
            foreach (var w in rep.Weeks) Assert.Greater(w.Held, 0, "every week teaches");
            Assert.Greater(rep.Prestige, TestData.Gd().StartPrestige);
        }

        [Test]
        public void FirstWeekFeelsGood()
        {
            var rep = BalanceRunner.Run(TestData.Gd(), 2, 1);
            var w = rep.Weeks[0];
            Assert.Greater(w.Net, 0);
            Assert.Greater(w.Grade, 0.6);
        }

        [Test]
        public void TwentyWeeksReachTheQuadWithoutAWall()
        {
            foreach (long seed in new long[] { 1, 2, 3 })
            {
                var rep = BalanceRunner.Run(TestData.Gd(), seed, 20);
                if (seed == 1) TestContext.WriteLine("BALANCE 20 weeks\n" + rep.Table());
                Assert.GreaterOrEqual(rep.Chapter, 2, "seed " + seed);
                Assert.IsTrue(rep.Finite);
                Assert.Greater(rep.MinCash, -2000, "seed " + seed);
            }
        }

        /// <summary>Plan section 6 content map at 1× (a week ≈ 4.5 real minutes): the Quad opens at ~1 h (weeks 10–17),
        /// the Science Campus at ~3 h (weeks 30–50), ±25 % — docs/BALANCE.md.</summary>
        [Test]
        public void ChaptersOpenOnThePlansClock()
        {
            foreach (long seed in new long[] { 1, 2, 3 })
            {
                var rep = BalanceRunner.Run(TestData.Gd(), seed, 50);
                int quad = ZoneWeek(rep, "quad"), science = ZoneWeek(rep, "science");
                TestContext.WriteLine("seed " + seed + ": quad after week " + quad + ", science after week " + science);
                Assert.That(quad, Is.InRange(10, 17), "seed " + seed + " quad");
                Assert.That(science, Is.InRange(30, 50), "seed " + seed + " science");
                Assert.Less(rep.Prestige, TestData.Gd().PrestigeMax, "seed " + seed + ": prestige keeps headroom for chapters 4-6");
            }
        }

        static int ZoneWeek(BalanceReport rep, string zone)
        {
            foreach (var l in rep.Log)
                if (l.EndsWith(" zone " + zone)) return int.Parse(l.Split(' ')[1]);
            return 999;
        }
    }
}
