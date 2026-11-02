// Docket Day — engine-free sim tests (NUnit). Same files run in the Unity Test Runner (EditMode) and under plain .NET
// (Tools/dotnet_check.sh). Data is read from Assets/Resources/data on disk.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using DocketDay.Sim;

namespace DocketDay.Sim.Tests
{
    public static class TestData
    {
        static GameData cached;

        public static string UnityDir()
        {
            string env = Environment.GetEnvironmentVariable("DOCKETDAY_UNITY");
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

        /// <summary>A career after the guide (the scripted ticket becomes an ordinary client).</summary>
        public static Simulation NewSim(long seed = 7, bool tutorial = false)
        {
            var s = new Simulation(Gd(), seed);
            if (!tutorial) s.EndTutorial();
            return s;
        }

        /// <summary>A fresh ordinary S matter of this area accepted today (hearing date fixed for the test).</summary>
        public static MatterState Open(Simulation sim, string area = "traffic", int hearingIn = 3, int slot = 0)
        {
            var m = sim.Walkin();
            if (m == null) { foreach (var x in sim.Intakes.ToList()) if (!x.Tutorial) sim.Decline(x.Id); m = sim.Walkin(); }
            m.Area = area;
            m.Fee = sim.Gd.Area(area).Fee;
            m.Tier = "S";
            m.Claims = sim.Gd.Area(area).Claims;
            m.Opponent = sim.Gd.Parties.Count - 2;
            m.Client = sim.Gd.Parties.Count - 3 - (m.Id % 10);
            Assert.AreEqual("", sim.Accept(m.Id), "accept");
            var h = m.HearingTask;
            h.Due = sim.S.Day + hearingIn;
            h.Slot = slot;
            foreach (var t in m.Tasks) if (t.Kind != Kinds.Hearing) t.Due = Math.Min(t.Due, h.Due);
            return m;
        }

        public static void RunDay(Simulation sim)
        {
            if (sim.Phase == Phases.Plan) sim.StartDay();
            sim.AdvanceMinutes(sim.Gd.DayMinutes + 5);
        }
    }

    public class DataTests
    {
        [Test]
        public void DataLoadsWithoutErrors()
        {
            var gd = TestData.Gd();
            Assert.IsTrue(gd.Ok, string.Join("; ", gd.Errors));
            Assert.AreEqual(5, gd.Areas.Count, "MVP: five practice areas");
            Assert.AreEqual(7, gd.Roles.Count, "MVP: seven staff types");
            Assert.AreEqual(4, gd.Judges.Count, "MVP: four judge archetypes");
            Assert.AreEqual(3, gd.Chapters.Count, "MVP: chapters 1-3");
            Assert.AreEqual(4, gd.Events.Count, "MVP: four crisis types");
        }

        [Test]
        public void BuildsAreMinutesNeverHours()
        {
            foreach (var r in TestData.Gd().Rooms) Assert.LessOrEqual(r.BuildS, 20 * 60, r.Id);
        }

        [Test]
        public void ADayIsThreeRealMinutes()
        {
            Assert.AreEqual(180, TestData.Gd().RealSecondsPerDay, 1e-6);
        }
    }

    public class CoreTests
    {
        [Test]
        public void NewCareerStartsInThePlanWithTheFounderAndThreeHelpers()
        {
            var sim = TestData.NewSim(1, tutorial: true);
            Assert.AreEqual(Phases.Plan, sim.Phase);
            CollectionAssert.AreEqual(new[] { "partner", "associate", "paralegal", "intern" }, sim.S.Staff.Select(s => s.Role).ToArray());
            Assert.IsTrue(sim.IsFounder(sim.S.Staff[0]));
            Assert.IsTrue(sim.S.Tutorial);
            var t = sim.MatterById(sim.S.TutorialMatter);
            Assert.IsNotNull(t, "the guide's first client waits at reception");
            Assert.AreEqual(MatterStatus.Intake, t.Status);
            Assert.AreEqual(sim.Gd.Upcoming, sim.Upcoming.Count());
        }

        [Test]
        public void TimeDoesNotMoveInThePlanOrWhilePaused()
        {
            var sim = TestData.NewSim();
            sim.Advance(1.0);
            Assert.AreEqual(sim.Gd.DayStart, sim.S.Minute, 1e-9);
            sim.StartDay();
            sim.SetPaused(true);
            sim.Advance(1.0);
            Assert.AreEqual(sim.Gd.DayStart, sim.S.Minute, 1e-9);
            sim.SetPaused(false);
            sim.Advance(1.0);
            Assert.AreEqual(sim.Gd.DayStart + sim.Gd.GameMinPerRealS, sim.S.Minute, 1e-6);
        }

        [Test]
        public void NoMoneyCapAnywhere()
        {
            var sim = TestData.NewSim();
            sim.S.Cash = 9_000_000;
            TestData.RunDay(sim);
            sim.Bill();
            Assert.Greater(sim.S.Cash, 8_000_000, "no vault / stored-cash cap (a reference-game mechanic we never use)");
        }
    }

    public class MatterTests
    {
        [Test]
        public void AcceptBuildsDatedTasksAndBooksALawyer()
        {
            var sim = TestData.NewSim();
            var m = sim.Walkin();
            Assert.AreEqual(MatterStatus.Intake, m.Status);
            m.Opponent = sim.Gd.Parties.Count - 2;
            Assert.AreEqual("", sim.Accept(m.Id));
            Assert.AreEqual(MatterStatus.Open, m.Status);
            var h = m.HearingTask;
            Assert.IsNotNull(h);
            Assert.Greater(h.Due, sim.S.Day);
            Assert.IsTrue(sim.RoleOf(sim.StaffById(h.Staff)).Lawyer, "a lawyer is booked by default");
            foreach (var t in m.Tasks) if (t.Kind != Kinds.Hearing) Assert.LessOrEqual(t.Due, h.Due, t.Kind);
        }

        [Test]
        public void ConflictBlocksAPlainAcceptButReferOrAWallWork()
        {
            var sim = TestData.NewSim();
            sim.S.Chapter = 2;
            var a = TestData.Open(sim);
            var b = sim.Walkin();
            b.Opponent = a.Client;
            Assert.IsTrue(sim.Conflict(b));
            Assert.AreEqual("conflict", sim.Accept(b.Id));
            double cash = sim.S.Cash;
            Assert.AreEqual("", sim.Accept(b.Id, wall: true));
            Assert.AreEqual(cash - sim.Gd.WallCost, sim.S.Cash, 1e-6);
            var c = sim.Walkin();
            c.Client = a.Opponent;
            Assert.IsTrue(sim.Conflict(c));
            double wip = sim.S.Wip;
            Assert.AreEqual("", sim.Refer(c.Id));
            Assert.Greater(sim.S.Wip, wip, "a referral pays a small fee");
        }

        [Test]
        public void WalkinsWaitThenWalkOut()
        {
            var sim = TestData.NewSim();
            sim.StartDay();
            var m = sim.Walkin();
            Assert.IsNotNull(m);
            sim.AdvanceMinutes(sim.Gd.PatienceMin + 2);
            Assert.IsNull(sim.MatterById(m.Id));
            Assert.GreaterOrEqual(sim.S.Today.WalkedOut, 1);
        }

        [Test]
        public void StaffWorkEarliestDeadlineFirstAndHourlyWorkIsBilled()
        {
            var sim = TestData.NewSim();
            var a = TestData.Open(sim, "traffic", 4);
            var b = TestData.Open(sim, "traffic", 2);
            int para = sim.S.Staff.First(s => s.Role == "paralegal").Id;
            int fa = a.Tasks.FindIndex(t => t.Kind == Kinds.File), fb = b.Tasks.FindIndex(t => t.Kind == Kinds.File);
            a.Tasks[fa].Due = sim.S.Day + 3;
            b.Tasks[fb].Due = sim.S.Day + 1;
            Assert.AreEqual("", sim.Assign(para, a.Id, fa));
            Assert.AreEqual("", sim.Assign(para, b.Id, fb));
            Assert.AreEqual(b.Id, sim.NextTaskOf(para).m.Id, "earliest deadline first");
            sim.StartDay();
            sim.AdvanceMinutes(60);
            Assert.Greater(b.Tasks[fb].Done, 0);
            Assert.AreEqual(0, a.Tasks[fa].Done, 1e-9);
            Assert.Greater(sim.S.Wip, 0, "hourly work is on today's invoices");
        }

        [Test]
        public void RetainerWorkDrawsFromTrustAndContingencyIsNotBilled()
        {
            var sim = TestData.NewSim();
            sim.S.Chapter = 2;
            var r = TestData.Open(sim, "tenancy", 4);
            r.Fee = Fees.Retainer;
            r.Trust = 700;
            var c = TestData.Open(sim, "small_claims", 4);
            int para = sim.S.Staff.First(s => s.Role == "paralegal").Id, intern = sim.S.Staff.First(s => s.Role == "intern").Id;
            sim.Assign(para, r.Id, r.Tasks.FindIndex(t => t.Kind == Kinds.File));
            sim.Assign(intern, c.Id, c.Tasks.FindIndex(t => t.Kind == Kinds.Evidence));
            sim.StartDay();
            sim.AdvanceMinutes(120);
            Assert.Less(r.Trust, 700, "retainer work is drawn from the client's trust money");
            Assert.AreEqual(sim.TrustTotal, r.Trust, 1e-6);
            Assert.AreEqual(0, c.Billed, 1e-9, "contingency work is paid by the result, not by the hour");
        }

        [Test]
        public void EvidenceRevealsTheTwist()
        {
            var sim = TestData.NewSim();
            var m = TestData.Open(sim);
            m.Revealed = false;
            m.Twist = 0.08;
            Assert.Greater(sim.ShownOdds(m), sim.Odds(m), "the card shows odds without the hidden twist");
            int ev = m.Tasks.FindIndex(t => t.Kind == Kinds.Evidence);
            sim.Assign(sim.S.Staff.First(s => s.Role == "intern").Id, m.Id, ev);
            sim.StartDay();
            sim.AdvanceMinutes(400);
            Assert.IsTrue(m.Tasks[ev].Finished);
            Assert.IsTrue(m.Revealed);
            Assert.AreEqual(sim.ShownOdds(m), sim.Odds(m), 1e-9);
        }

        [Test]
        public void PrepTableAcceptsOnlyTheRightCardsAndRaisesTheOdds()
        {
            var sim = TestData.NewSim();
            var m = TestData.Open(sim);
            Assert.AreEqual("locked", sim.PrepMatch(m.Id, 0, 0), "evidence first");
            m.Task(Kinds.Evidence).Finished = true;
            sim.Fire("sick_day");   // nothing to do with the prep table; keep the paralegal fallback honest
            foreach (var s in sim.S.Staff.Where(x => x.Role == "paralegal").ToList()) sim.Fire(s.Id);
            double before = sim.Odds(m);
            Assert.AreEqual("wrong", sim.PrepMatch(m.Id, 0, 1));
            Assert.AreEqual("", sim.PrepMatch(m.Id, 0, 0));
            Assert.AreEqual("", sim.PrepMatch(m.Id, 1, 1));
            Assert.IsTrue(m.PuzzleSolved);
            Assert.Greater(sim.Odds(m), before);
        }

        [Test]
        public void SettleClosesTheMatterWithACertainFee()
        {
            var sim = TestData.NewSim();
            var m = TestData.Open(sim, "small_claims");
            double fee = sim.SettleFee(m);
            Assert.Greater(fee, 0);
            Assert.Less(fee, sim.WinFee(m));
            double wip = sim.S.Wip;
            Assert.AreEqual("", sim.Settle(m.Id));
            Assert.IsNull(sim.MatterById(m.Id));
            Assert.AreEqual(wip + fee, sim.S.Wip, 1e-6);
            Assert.AreEqual(1, sim.S.Settles);
        }

        [Test]
        public void HearingStartsAtItsSlotAndEndsInAVerdict()
        {
            var sim = TestData.NewSim();
            var m = TestData.Open(sim, "traffic", 0, 0);
            var events = new List<SimEvent>();
            sim.StartDay();
            sim.AdvanceMinutes(sim.Gd.HearingSlots[0] - sim.Gd.DayStart + 1);
            events.AddRange(sim.DrainEvents());
            Assert.AreEqual(MatterStatus.Hearing, m.Status);
            Assert.IsTrue(events.Exists(e => e.Type == "hearing_start" && e.A == m.Id));
            int r = sim.Resolve(m.Id, 1);
            Assert.GreaterOrEqual(r, 0);
            Assert.IsNull(sim.MatterById(m.Id));
            Assert.Greater(sim.S.Wip, 0, "won or lost, the hearing pays (a lost one a small lesson fee)");
        }

        [Test]
        public void ASessionEndsByItselfWhenNobodyTapsTheScene()
        {
            var sim = TestData.NewSim();
            var m = TestData.Open(sim, "traffic", 0, 0);
            sim.StartDay();
            sim.AdvanceMinutes(sim.Gd.HearingSlots[0] - sim.Gd.DayStart + sim.Gd.HearingSessionMin + 3);
            Assert.IsNull(sim.MatterById(m.Id));
            Assert.AreEqual(1, sim.S.Today.Wins + sim.S.Today.Losses);
        }

        [Test]
        public void AnEmptyChairGetsOneAutomaticContinuanceNeverALostMatter()
        {
            var sim = TestData.NewSim();
            var m = TestData.Open(sim, "traffic", 0, 0);
            sim.Unassign(m.Id, m.Tasks.Count - 1);
            sim.StartDay();
            sim.AdvanceMinutes(sim.Gd.HearingSlots[0] - sim.Gd.DayStart + 1);
            Assert.AreEqual(MatterStatus.Open, m.Status);
            Assert.IsTrue(m.Continued);
            Assert.AreEqual(sim.S.Day + sim.Gd.ContinuanceDays, m.HearingTask.Due);
        }

        [Test]
        public void CollisionsAreFoundAndCovered()
        {
            var sim = TestData.NewSim();
            var a = TestData.Open(sim, "traffic", 2, 0);
            var b = TestData.Open(sim, "small_claims", 2, 0);
            a.HearingTask.Staff = 0; b.HearingTask.Staff = 0;
            var c = sim.Collisions();
            Assert.AreEqual(2, c.Count(x => x.Kind == "double"));
            int assoc = sim.S.Staff.First(s => s.Role == "associate").Id;
            Assert.AreEqual("not_lawyer", sim.Assign(sim.S.Staff.First(s => s.Role == "intern").Id, b.Id, b.Tasks.Count - 1), "only lawyers attend hearings");
            Assert.AreEqual("", sim.Assign(assoc, b.Id, b.Tasks.Count - 1));
            Assert.AreEqual(0, sim.Collisions().Count);
            b.HearingTask.Staff = 0;
            double cash = sim.S.Cash;
            Assert.AreEqual("", sim.Visiting(b.Id));
            Assert.Less(sim.S.Cash, cash);
            Assert.AreEqual(0, sim.Collisions().Count, "visiting counsel covers");
        }

        [Test]
        public void AContinuanceIsAllowedOncePerMatter()
        {
            var sim = TestData.NewSim();
            var m = TestData.Open(sim, "traffic", 2);
            int due = m.HearingTask.Due;
            Assert.AreEqual("", sim.Continuance(m.Id));
            Assert.AreEqual(due + sim.Gd.ContinuanceDays, m.HearingTask.Due);
            Assert.AreEqual("used", sim.Continuance(m.Id));
        }

        [Test]
        public void OddsRiseWithPrepAndFallWithAMissingFiling()
        {
            var sim = TestData.NewSim();
            var m = TestData.Open(sim);
            double raw = sim.Odds(m);
            foreach (var t in m.Tasks) if (t.Kind != Kinds.Hearing) { t.Done = t.Need; t.Finished = true; }
            Assert.Greater(sim.Odds(m), raw + 0.2);
            Assert.LessOrEqual(sim.Odds(m), sim.Gd.OddsMax);
        }

        [Test]
        public void AMissedFilingIsSanctionedOnceAndStaysOnTheRail()
        {
            var sim = TestData.NewSim();
            sim.S.Day = 5;
            var m = TestData.Open(sim, "traffic", 3);
            var f = m.Task(Kinds.File);
            foreach (var t in m.Tasks) if (t.Kind != Kinds.Hearing) { t.Staff = Attend.None; t.Due = m.HearingTask.Due; }
            f.Due = sim.S.Day;
            double st = sim.AreaStanding("traffic");
            TestData.RunDay(sim);
            Assert.IsTrue(f.Late);
            Assert.AreEqual(1, sim.S.Today.Late);
            Assert.Greater(sim.S.Today.Sanctions, 0);
            Assert.Less(sim.AreaStanding("traffic"), st);
            Assert.IsNotNull(sim.MatterById(m.Id), "never deleted");
            sim.NextDay();
            TestData.RunDay(sim);
            Assert.AreEqual(0, sim.S.Today.Late, "sanctioned once");
        }

        [Test]
        public void AutoAssignStaffsEveryTaskAndTheClerkPicksFilings()
        {
            var sim = TestData.NewSim();
            var m = TestData.Open(sim);
            foreach (var t in m.Tasks) t.Staff = Attend.None;
            sim.AutoAssign(true);
            Assert.IsTrue(m.Tasks.All(t => t.Staff != Attend.None));
            sim.S.Chapter = 2;
            sim.S.Cash = 5000;
            Assert.AreEqual("", sim.Hire("clerk"));
            var n = TestData.Open(sim);
            foreach (var t in n.Tasks) if (t.Kind != Kinds.Hearing) t.Staff = Attend.None;
            sim.StartDay();
            sim.AdvanceMinutes(15);
            Assert.AreEqual("clerk", sim.StaffById(n.Task(Kinds.File).Staff).Role);
        }

        [Test]
        public void StandingInstructionsAutoAcceptPlainMatters()
        {
            var sim = TestData.NewSim();
            Assert.AreEqual("", sim.SetRule("traffic", true));
            Assert.AreEqual("max", sim.SetRule("small_claims", true), "one free instruction");
            int accepted = 0;
            for (int i = 0; i < 12; i++)
            {
                var m = sim.Walkin();
                if (m == null) break;
                if (m.Status == MatterStatus.Open) accepted++;
                else if (!m.Tutorial) sim.Decline(m.Id);
            }
            Assert.Greater(accepted, 0);
        }
    }

    public class EconomyTests
    {
        [Test]
        public void DayEndsWithAReportCostsAndStars()
        {
            var sim = TestData.NewSim();
            double cash = sim.S.Cash;
            TestData.RunDay(sim);
            Assert.AreEqual(Phases.Report, sim.Phase);
            var d = sim.S.History.Last();
            Assert.Greater(d.Salaries, 0);
            Assert.Greater(d.Rent, 0);
            Assert.AreEqual(cash - d.Salaries - d.Rent - d.Sanctions - d.Visiting - d.Walls, sim.S.Cash, 1e-6, "costs are paid at the day end; income at the billing run");
            Assert.That(d.Stars, Is.InRange(0, 9));
        }

        [Test]
        public void TheBillingRunMovesTodaysWorkOnce_NextDayBillsAForgottenDay()
        {
            var sim = TestData.NewSim();
            TestData.Open(sim);
            sim.AutoAssign(true);
            TestData.RunDay(sim);
            double wip = sim.S.Wip, cash = sim.S.Cash;
            Assert.Greater(wip, 0);
            Assert.AreEqual(wip, sim.Bill(), 0.01);
            Assert.AreEqual(cash + wip, sim.S.Cash, 0.01);
            Assert.AreEqual(-1, sim.Bill(), "once");
            sim.NextDay();
            TestData.RunDay(sim);
            double w2 = sim.S.Wip, c2 = sim.S.Cash;
            sim.NextDay();
            Assert.AreEqual(c2 + w2, sim.S.Cash, 0.01, "an unbilled day is billed on the way, nothing lost");
        }

        [Test]
        public void HiringNeedsTheChapterDesksAndCash()
        {
            var sim = TestData.NewSim();
            sim.S.Cash = 100000;
            Assert.AreEqual("locked", sim.CanHire("investigator").Reason);
            Assert.AreEqual("locked", sim.CanHire("partner").Reason);
            Assert.AreEqual("", sim.Hire("paralegal"));
            Assert.AreEqual("desks", sim.CanHire("intern").Reason, "the garage has 5 desks");
            Assert.AreEqual("", sim.Build("desk"));
            sim.AdvanceBuild(1000);
            Assert.AreEqual("", sim.Hire("intern"));
            Assert.AreEqual("founder", sim.Fire(0));
        }

        [Test]
        public void RoomsBuildOnRealTimeAndWork()
        {
            var sim = TestData.NewSim();
            sim.S.Cash = 5000;
            Assert.AreEqual("", sim.Build("library"));
            Assert.AreEqual("busy", sim.CanBuild("coffee").Reason, "one build at a time");
            Assert.IsFalse(sim.RoomBuilt("library"));
            sim.AdvanceBuild(sim.Gd.Room("library").BuildS + 1);
            Assert.IsTrue(sim.RoomBuilt("library"));
            Assert.Greater(sim.WorkMult(Kinds.Prep), 1.0);
            Assert.AreEqual("owned", sim.CanBuild("library").Reason);
        }

        [Test]
        public void MovingUpNeedsStandingClosedMattersAndCash()
        {
            var sim = TestData.NewSim();
            Assert.AreEqual("standing", sim.CanMoveUp());
            for (int i = 0; i < sim.S.Standing.Length; i++) sim.S.Standing[i] = 4;   // 2 chapter-1 areas × 4 = 8 ≥ the Main Street gate (6.8)
            Assert.AreEqual("closed", sim.CanMoveUp());
            sim.S.Closed = 100;
            sim.S.Cash = 0;
            Assert.AreEqual("cash", sim.CanMoveUp());
            sim.S.Cash = 1_000_000;
            Assert.AreEqual("", sim.MoveUp());
            Assert.AreEqual(2, sim.Chapter);
            Assert.IsTrue(sim.AreaUnlocked("tenancy"));
            Assert.IsFalse(sim.AreaUnlocked("contract"));
        }

        [Test]
        public void TheGuideAndTheFirstDaysNeverLowerStanding()
        {
            var sim = TestData.NewSim(3, tutorial: true);
            double st = sim.AreaStanding("traffic");
            for (int d = 0; d < 3; d++)
            {
                TestData.RunDay(sim);
                sim.NextDay();
            }
            Assert.GreaterOrEqual(sim.AreaStanding("traffic"), st);
        }

        [Test]
        public void NoCrisesDuringTheGuideOrTheFirstDays()
        {
            var sim = TestData.NewSim(5);
            for (int d = 1; d <= sim.Gd.EventFreeDays; d++)
            {
                Assert.IsFalse(sim.S.Planned.Exists(p => p.Id != "walkin"), "day " + d);
                TestData.RunDay(sim);
                Assert.IsFalse(sim.DrainEvents().Exists(e => e.Type == "crisis"), "day " + d);
                sim.NextDay();
            }
        }

        [Test]
        public void OfflinePayIsCappedAndOvernightPlusDoublesTheCap()
        {
            var sim = TestData.NewSim();
            sim.S.History.Add(new DayStats { Fees = 3000 });
            sim.Touch(1000);
            var o = sim.Offline(1000 + 12 * 3600);
            Assert.IsTrue(o.Capped);
            Assert.AreEqual(sim.Gd.OfflineCapH, o.Hours, 1e-9);
            sim.SetPerks(new Dictionary<string, bool> { { "overnight_plus", true } });
            var o2 = sim.Offline(1000 + 24 * 3600 + 12 * 3600);
            Assert.AreEqual(sim.Gd.OfflineCapHPlus, o2.Hours, 1e-9);
            Assert.Greater(o2.Amount, o.Amount);
        }

        [Test]
        public void GrantCashUsesTheUsualDayIncome()
        {
            var sim = TestData.NewSim();
            double cash = sim.S.Cash;
            double got = sim.GrantCash(2.5);
            Assert.AreEqual(Math.Round(2.5 * sim.DayIncome()), got, 1e-9);
            Assert.AreEqual(cash + got, sim.S.Cash, 1e-9);
            Assert.AreEqual(0, sim.TrustTotal, 1e-9, "trust money is never bought");
        }
    }

    public class CrisisTests
    {
        [Test]
        public void EveryCrisisLandsAndHasALever()
        {
            var sim = TestData.NewSim();
            sim.S.Day = 6;
            var m = TestData.Open(sim, "traffic", 3);
            sim.StartDay();
            int tasks = m.Tasks.Count;
            Assert.IsTrue(sim.Fire("surprise_motion"));
            Assert.AreEqual(tasks + 1, m.Tasks.Count);
            Assert.AreEqual(Kinds.Hearing, m.Tasks[m.Tasks.Count - 1].Kind, "the hearing stays last");
            Assert.IsTrue(sim.Fire("power_cut"));
            CollectionAssert.Contains(sim.ActiveCrises(), "power_cut");
            Assert.IsTrue(sim.Fire("sick_day"));
            var sick = sim.S.Staff.First(s => s.Sick);
            Assert.AreEqual(0, sick.HoursLeft, 1e-9);
            Assert.IsFalse(sim.IsFounder(sick));
            int judge = m.Judge;
            Assert.IsTrue(sim.Fire("substitute_judge"));
            Assert.AreNotEqual(judge, m.Judge);
        }

        [Test]
        public void APowerCutStopsFilingsForAnHour()
        {
            var sim = TestData.NewSim();
            var m = TestData.Open(sim);
            var f = m.Task(Kinds.File);
            sim.Assign(sim.S.Staff.First(s => s.Role == "paralegal").Id, m.Id, m.Tasks.IndexOf(f));
            sim.StartDay();
            sim.Fire("power_cut");
            sim.AdvanceMinutes(30);
            Assert.AreEqual(0, f.Done, 1e-9);
            sim.AdvanceMinutes(60);
            Assert.Greater(f.Done, 0);
        }
    }

    public class RewardTests
    {
        const long Now = 1_800_000_000;

        [Test]
        public void RewardsAreNeverOfferedDuringTheGuide()
        {
            var sim = TestData.NewSim(1, tutorial: true);
            foreach (var p in Simulation.RewardPlacements) Assert.AreEqual("tutorial", sim.RewardOffer(p, Now).Reason, p);
        }

        [Test]
        public void BillingBoostOnlyOnTheReportBeforeTheBillingRun()
        {
            var sim = TestData.NewSim();
            Assert.AreEqual("context", sim.RewardOffer("billing_boost", Now).Reason);
            TestData.Open(sim);
            sim.AutoAssign(true);
            TestData.RunDay(sim);
            var o = sim.RewardOffer("billing_boost", Now);
            Assert.IsTrue(o.Ok);
            Assert.LessOrEqual(o.Amount, sim.Gd.RewardSingleMaxDays * sim.DayIncome() + 1e-6, "single ≤ 1/5 of the cheapest pack");
            double wip = sim.S.Wip;
            Assert.IsTrue(sim.GrantReward("billing_boost", Now).Ok);
            Assert.AreEqual(wip + o.Amount, sim.S.Wip, 1e-6);
            Assert.AreEqual("cap", sim.RewardOffer("billing_boost", Now + 10).Reason);
        }

        [Test]
        public void DailyCapAndCooldownPersistInTheCareer()
        {
            var sim = TestData.NewSim();
            Assert.IsTrue(sim.GrantReward("case_preview", Now).Ok);
            Assert.IsTrue(sim.Upcoming.All(m => m.Revealed));
            sim.Walkin();   // a new upcoming client, not revealed yet
            var reloaded = SaveCodec.Decode(SaveCodec.Encode(sim), sim.Gd).Sim;
            Assert.AreEqual("cooldown", reloaded.RewardOffer("case_preview", Now + 60).Reason);
            Assert.IsTrue(reloaded.GrantReward("case_preview", Now + 601).Ok);
            reloaded.Walkin();
            Assert.AreEqual("cap", reloaded.RewardOffer("case_preview", Now + 1300).Reason);
            Assert.IsTrue(reloaded.RewardOffer("case_preview", Now + 86400).Ok, "a new UTC day");
        }

        [Test]
        public void CashRewardsStayUnderTheFleetCaps()
        {
            var sim = TestData.NewSim();
            sim.S.OfflinePending = 1_000_000;
            var o = sim.RewardOffer("offline_x2", Now);
            Assert.LessOrEqual(o.Amount, sim.Gd.RewardSingleMaxDays * sim.DayIncome() + 1e-6);
            double daily = 0;
            foreach (var kv in sim.Gd.Rewards) if (Simulation.RewardIsCash(kv.Key)) daily += kv.Value.PerDay * sim.Gd.RewardSingleMaxDays;
            Assert.LessOrEqual(daily, 0.5 + 1e-9, "all cash rewards of a day ≤ 1/2 of the cheapest pack");
        }
    }

    public class SaveTests
    {
        [Test]
        public void RoundTripMidDayIsIdenticalAndKeepsPlaying()
        {
            var sim = TestData.NewSim(11);
            for (int i = 0; i < 3; i++) TestData.Open(sim, i % 2 == 0 ? "traffic" : "small_claims", 1 + i);
            sim.AutoAssign(true);
            sim.StartDay();
            sim.AdvanceMinutes(120);
            string a = SaveCodec.Encode(sim);
            var r = SaveCodec.Decode(a, sim.Gd);
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(a, SaveCodec.Encode(r.Sim));
            sim.AdvanceMinutes(200);
            r.Sim.AdvanceMinutes(200);
            Assert.AreEqual(SaveCodec.Encode(sim), SaveCodec.Encode(r.Sim), "deterministic after a reload");
        }

        [Test]
        public void DecoderRefusesWhatTheSimNeverWrites()
        {
            var gd = TestData.Gd();
            Assert.IsFalse(SaveCodec.Decode("{", gd).Ok);
            Assert.AreEqual("wrong_game", SaveCodec.Decode("{\"game\":\"bottle-route\",\"v\":1}", gd).Error);
            var sim = TestData.NewSim();
            string ok = SaveCodec.Encode(sim);
            Assert.IsFalse(SaveCodec.Decode(ok.Replace("\"role\":\"intern\"", "\"role\":\"wizard\""), gd).Ok);
            Assert.IsFalse(SaveCodec.Decode(ok.Replace("\"phase\":\"plan\"", "\"phase\":\"lunch\""), gd).Ok);
        }

        [Test]
        public void DecoderAcceptsNegativeCashASessionAndTheUnbilledReport()
        {
            var sim = TestData.NewSim();
            var m = TestData.Open(sim, "traffic", 0, 0);
            sim.S.Cash = -4321;
            sim.StartDay();
            sim.AdvanceMinutes(sim.Gd.HearingSlots[0] - sim.Gd.DayStart + 1);
            Assert.AreEqual(MatterStatus.Hearing, m.Status);
            var r = SaveCodec.Decode(SaveCodec.Encode(sim), sim.Gd);
            Assert.IsTrue(r.Ok, r.Error);
            Assert.GreaterOrEqual(r.Sim.Resolve(m.Id, 0), 0, "the session resumes after the reload");
            TestData.RunDay(r.Sim);
            Assert.AreEqual(Phases.Report, r.Sim.Phase);
            var r2 = SaveCodec.Decode(SaveCodec.Encode(r.Sim), sim.Gd);
            Assert.IsTrue(r2.Ok, r2.Error);
            Assert.IsFalse(r2.Sim.S.Billed);
        }

        [Test]
        public void FuzzEveryTenMinutesOfBusyDaysDecodes()
        {
            // fleet §14 save_fuzz: guide phase, collisions, sessions, late filings, day passes, crises, report screens
            var sim = TestData.NewSim(21, tutorial: true);
            var gd = sim.Gd;
            int bad = 0, n = 0;
            void Check()
            {
                n++;
                var r = SaveCodec.Decode(SaveCodec.Encode(sim), gd);
                if (!r.Ok) { bad++; TestContext.WriteLine("day " + sim.S.Day + " min " + sim.S.Minute + ": " + r.Error); }
            }
            Check();
            sim.Accept(sim.S.TutorialMatter);
            Check();
            sim.SpawnTutorialCollision();
            Check();
            sim.EndTutorial();
            sim.S.Cash = 50000;
            for (int day = 0; day < 8; day++)
            {
                foreach (var m in sim.Intakes.ToList()) { if (sim.Conflict(m)) sim.Refer(m.Id); else sim.Accept(m.Id); }
                if (day % 2 == 0) sim.AutoAssign(true);
                if (day == 3) { sim.AddDayPass(); sim.S.Chapter = 2; sim.Hire("clerk"); }
                if (day == 5) sim.Build("library");
                sim.StartDay();
                if (day >= 4) { sim.Fire("surprise_motion"); sim.Fire("sick_day"); sim.Fire("power_cut"); sim.Fire("substitute_judge"); }
                while (sim.Phase == Phases.Live)
                {
                    sim.AdvanceMinutes(10);
                    Check();
                }
                Check();
                if (day % 3 == 0) sim.Bill();
                Check();
                sim.NextDay();
            }
            Assert.AreEqual(0, bad, "unreadable saves out of " + n);
        }
    }

    public class BalanceTests
    {
        [Test]
        public void SevenDaysStayFiniteProfitableAndNeverStuck()
        {
            var rep = BalanceRunner.Run(TestData.Gd(), 42, 7);
            TestContext.WriteLine("BALANCE 7 days seed 42\n" + rep.Table());
            Assert.IsTrue(rep.Finite);
            Assert.AreEqual(7, rep.Days.Count);
            Assert.Greater(rep.Days.Sum(d => d.Net), 0, "a week earns money");
            Assert.Greater(rep.Days.Skip(4).Average(d => d.Income), rep.Days.Take(2).Average(d => d.Income), "the firm grows");
            Assert.Greater(rep.Closed, 3, "matters reach a verdict or a settlement in the first week");
            Assert.Greater(rep.MinCash, -1000, "no hole in week one");
        }

        [Test]
        public void FirstDayIsForgiving()
        {
            var rep = BalanceRunner.Run(TestData.Gd(), 3, 1);
            var d = rep.Days[0];
            Assert.Greater(d.Net, -200, "day 1 never digs a hole");
            Assert.AreEqual(0, d.Late);
        }

        [Test]
        public void MainStreetComesAtThePlansPace()
        {
            // plan §6: chapter 2 at ~1.5 h of 1× play = ~30 days of 3 min (polish pass 2026-11-02: the bot gets there on
            // days 27–31); not in the first 20 days (it used to take 14), and always within 40
            var early = BalanceRunner.Run(TestData.Gd(), 9, 20);
            Assert.AreEqual(1, early.Chapter, "Main Street is not a first-hour unlock");
            var rep = BalanceRunner.Run(TestData.Gd(), 9, 40);
            TestContext.WriteLine(rep.Table() + string.Join("\n", rep.Log));
            Assert.GreaterOrEqual(rep.Chapter, 2);
            Assert.IsTrue(rep.Finite);
            Assert.Greater(rep.Wins, rep.Losses * 2, "a prepared firm wins most hearings");
        }
    }
}
