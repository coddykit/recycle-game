// Charge Nurse — engine-free sim tests (NUnit). Same files run in the Unity Test Runner (EditMode) and under plain .NET
// (Tools/dotnet_check.sh). Data is read from Assets/Resources/data on disk.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ChargeNurse.Sim;

namespace ChargeNurse.Sim.Tests
{
    public static class TestData
    {
        static GameData cached;

        public static string UnityDir()
        {
            string env = Environment.GetEnvironmentVariable("CHARGENURSE_UNITY");
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

        /// <summary>A career after the guide (the guide's patient #1 is still waiting, walk-ins on, no crises planned yet).</summary>
        public static Simulation NewSim(long seed = 7, bool tutorial = false)
        {
            var s = new Simulation(Gd(), seed) { EmitCrowd = false };
            if (!tutorial) s.EndTutorial();
            return s;
        }

        /// <summary>A quiet career: no walk-ins, no waiting patient — tests add exactly the patients they need.</summary>
        public static Simulation Quiet(long seed = 7)
        {
            var s = NewSim(seed);
            s.S.Patients.Clear();
            s.S.ArrivalsOn = false;
            return s;
        }

        /// <summary>A registered patient waiting for a colour (no test, not admitted unless asked).</summary>
        public static Patient Walk(Simulation sim, int sev, bool admit = false, string test = "")
        {
            var p = new Patient { Id = sim.S.NextPatient++, Sev = sev, Stage = Stage.Triage, Admit = admit, Ward = admit ? "obs" : "", Test = test };
            sim.S.Patients.Add(p);
            return p;
        }
    }

    public class DataTests
    {
        [Test]
        public void DataLoadsWithoutErrors()
        {
            var gd = TestData.Gd();
            Assert.IsTrue(gd.Ok, string.Join("; ", gd.Errors));
            Assert.AreEqual(3, gd.Wings.Count, "MVP: clinic, town hospital, city general");
            Assert.GreaterOrEqual(gd.Research.Count, 16);
            Assert.AreEqual(8, gd.Events.Count, "MVP: 8 crises");
            Assert.AreEqual(5, Teams.Pool.Length + 0, "5 hospital-wide teams + technicians per room = 6 staff teams");
        }

        [Test]
        public void EveryZoneIsReachableAlongTheCorridors()
        {
            var gd = TestData.Gd();
            foreach (var a in gd.Zones)
                foreach (var b in gd.Zones)
                {
                    double m = gd.Metres(a.Index, b.Index);
                    Assert.Less(m, 500, a.Id + " -> " + b.Id);
                    Assert.AreEqual(m, gd.Metres(b.Index, a.Index), 1e-9);
                }
        }

        [Test]
        public void UpgradesFitInOneShiftAndTwentyRealMinutes()
        {
            var gd = TestData.Gd();
            foreach (var r in gd.Research)
            {
                Assert.LessOrEqual(r.Minutes, gd.DayMinutes, r.Id);
                Assert.LessOrEqual(r.Minutes / gd.GameMinPerRealS, 20 * 60, r.Id);
            }
        }
    }

    public class CoreTests
    {
        [Test]
        public void NewCareerStartsInTheClinicWithOneWaitingPatient()
        {
            var sim = new Simulation(TestData.Gd(), 1);
            Assert.AreEqual(Phases.Plan, sim.Phase);
            Assert.IsTrue(sim.S.Tutorial);
            Assert.AreEqual(new List<string> { "clinic" }, sim.S.Wings);
            Assert.AreEqual(2, sim.Team(Teams.Nurse));
            Assert.AreEqual(1, sim.Team(Teams.Doctor));
            Assert.AreEqual(2, sim.Bays);
            Assert.AreEqual(4, sim.BedsIn("obs"));
            Assert.AreEqual(1, sim.S.Patients.Count);
            var p = sim.S.Patients[0];
            Assert.AreEqual(1, p.Id);
            Assert.AreEqual(Stage.Triage, p.Stage);
            Assert.AreEqual(Sev.Green, p.Sev);
            Assert.IsTrue(p.Guided);
            Assert.IsFalse(sim.S.ArrivalsOn, "the guide holds walk-ins");
        }

        [Test]
        public void TimeDoesNotMoveInThePlanOrWhilePaused()
        {
            var sim = TestData.NewSim();
            double m = sim.S.Minute;
            sim.Advance(1);
            Assert.AreEqual(m, sim.S.Minute);
            sim.StartDay();
            sim.SetPaused(true);
            sim.Advance(1);
            Assert.AreEqual(m, sim.S.Minute);
            sim.SetPaused(false);
            sim.Advance(1);
            Assert.AreEqual(m + sim.Gd.GameMinPerRealS, sim.S.Minute, 1e-6);
        }

        [Test]
        public void AWalkInIsTriagedTreatedDischargedAndPays()
        {
            var sim = TestData.Quiet();
            var p = TestData.Walk(sim, Sev.Yellow);
            double cash = sim.S.Cash;
            sim.StartDay();
            sim.AdvanceMinutes(120);
            Assert.IsNull(sim.P(p.Id), "gone home");
            Assert.AreEqual(1, sim.S.Today.Treated);
            Assert.Greater(sim.S.Today.Fees, 0);
            Assert.Greater(sim.S.Cash, cash - 1, "paid more than the supplies");
            Assert.IsTrue(sim.S.Beds.Where(b => b.Bay).All(b => b.State == BedState.Clean), "the bay was cleaned after");
        }

        [Test]
        public void AdmittedPatientBoardsInTheBayWhenTheWardIsFull()
        {
            var sim = TestData.Quiet();
            foreach (var b in sim.S.Beds.Where(b => !b.Bay)) { b.State = BedState.Busy; b.Patient = 999; }
            var p = TestData.Walk(sim, Sev.Orange, admit: true);
            sim.StartDay();
            sim.AdvanceMinutes(90);
            Assert.AreEqual(Stage.Board, p.Stage, "treated, admitted, no ward bed");
            Assert.GreaterOrEqual(p.Bed, 0);
            Assert.IsTrue(sim.S.Beds[p.Bed].Bay, "boarding in the ED bay");
            Assert.AreEqual("beds", sim.FindBottleneck().Key);
            // a bed frees up: the porter takes the patient to the ward, the bay turns dirty
            var wb = sim.S.Beds.First(b => !b.Bay);
            wb.State = BedState.Clean; wb.Patient = -1;
            sim.AdvanceMinutes(30);
            Assert.AreEqual(Stage.Stay, p.Stage);
            Assert.IsFalse(sim.S.Beds[p.Bed].Bay);
        }

        [Test]
        public void ARedCardTakesTheDoctorFromAGreenOne()
        {
            var sim = TestData.Quiet();
            var g = TestData.Walk(sim, Sev.Green);
            g.Tag = Sev.Green; g.Stage = Stage.Bay;
            sim.StartDay();
            sim.AdvanceMinutes(1);
            Assert.AreEqual(Stage.Treat, g.Stage);
            Assert.IsTrue(g.Doc);
            var r = TestData.Walk(sim, Sev.Red);
            r.Tag = Sev.Red; r.Stage = Stage.Bay;
            sim.AdvanceMinutes(1);
            Assert.IsTrue(r.Doc, "red is seen at once");
            Assert.IsFalse(g.Doc, "the green patient waits a little");
        }

        [Test]
        public void DirtyBedsWaitForACleanerOrThePlayer()
        {
            var sim = TestData.Quiet();
            sim.S.Teams[Teams.Cleaner] = 1;
            foreach (var b in sim.S.Beds) b.State = BedState.Dirty;
            sim.StartDay();
            sim.AdvanceMinutes(1);
            Assert.AreEqual(1, sim.S.Beds.Count(b => b.State == BedState.Cleaning), "one cleaner, one bed at a time (bays first)");
            Assert.IsTrue(sim.S.Beds.First(b => b.State == BedState.Cleaning).Bay);
            int dirty = sim.S.Beds.FindIndex(b => b.State == BedState.Dirty);
            Assert.AreEqual("", sim.CleanBed(dirty));
            Assert.AreEqual(BedState.Clean, sim.S.Beds[dirty].State);
            Assert.AreEqual("not_dirty", sim.CleanBed(dirty));
        }
    }

    public class TriageTests
    {
        [Test]
        public void NursesGetMostColoursRight()
        {
            var sim = TestData.Quiet(3);
            for (int i = 0; i < 200; i++) TestData.Walk(sim, i % 4);
            sim.S.Teams[Teams.Nurse] = 12;
            sim.StartDay();
            sim.AdvanceMinutes(60);
            var tagged = sim.S.Patients.Where(p => p.Tag >= 0).ToList();
            Assert.Greater(tagged.Count, 100);
            double right = tagged.Count(p => p.Tag == p.Sev) / (double)tagged.Count;
            Assert.That(right, Is.InRange(0.7, 0.95));
        }

        [Test]
        public void ThePlayerTagSkipsTheQueueAndCanBeChangedUntilTheDoctor()
        {
            var sim = TestData.Quiet();
            var p = TestData.Walk(sim, Sev.Orange);
            Assert.AreEqual("", sim.TagPatient(p.Id, Sev.Orange));
            Assert.AreEqual(Stage.Bay, p.Stage);
            Assert.AreEqual("", sim.TagPatient(p.Id, Sev.Red), "re-tag while waiting");
            Assert.AreEqual(Sev.Red, p.Tag);
            Assert.AreEqual("invalid", sim.TagPatient(p.Id, 7));
            sim.StartDay();
            sim.AdvanceMinutes(2);
            Assert.AreEqual("too_late", sim.TagPatient(p.Id, Sev.Green));
        }

        [Test]
        public void AnUnderTriagedPatientWaitsTooLongAndGetsWorse()
        {
            var sim = TestData.Quiet();
            sim.S.Day = 5;
            foreach (var b in sim.S.Beds.Where(b => b.Bay)) { b.State = BedState.Busy; b.Patient = 999; }   // no bay
            var p = TestData.Walk(sim, Sev.Orange);
            sim.TagPatient(p.Id, Sev.Green);   // wrong colour: waits like a green
            sim.StartDay();
            sim.AdvanceMinutes(sim.Gd.SafeWait[Sev.Orange] + 2);
            Assert.IsTrue(p.Worsened);
            Assert.AreEqual(Sev.Red, p.Sev);
            Assert.AreEqual(1, sim.S.Today.Worsened);
        }

        [Test]
        public void NobodyWorsensOrLeavesInTheGuide()
        {
            var sim = TestData.NewSim(tutorial: true);
            foreach (var b in sim.S.Beds.Where(b => b.Bay)) { b.State = BedState.Busy; b.Patient = 999; }
            var p = TestData.Walk(sim, Sev.Red);
            p.Tag = Sev.Red; p.Stage = Stage.Bay;
            sim.StartDay();
            sim.AdvanceMinutes(300);
            Assert.IsFalse(p.Worsened);
            Assert.IsNotNull(sim.P(p.Id));
        }
    }

    public class LensTests
    {
        [Test]
        public void TheLensNamesTheRealBottleneck()
        {
            var sim = TestData.Quiet();
            Assert.AreEqual("smooth", sim.FindBottleneck().Key);
            // ready-to-go patients holding bays while others wait: discharge
            for (int i = 0; i < 2; i++)
            {
                var p = TestData.Walk(sim, Sev.Green);
                p.Tag = 0; p.Stage = Stage.Ready; p.Seen = true; p.Bed = i;
                sim.S.Beds[i].State = BedState.Busy; sim.S.Beds[i].Patient = p.Id;
            }
            for (int i = 0; i < 3; i++) { var w = TestData.Walk(sim, Sev.Yellow); w.Tag = 1; w.Stage = Stage.Bay; }
            var b = sim.Lens();
            Assert.AreEqual("discharge", b.Key);
            Assert.AreEqual(Teams.Nurse, b.Team);
            Assert.IsTrue(sim.DrainEvents().Exists(e => e.Type == "lens" && e.Text == "discharge"));
        }

        [Test]
        public void DirtyBedsAreTheBottleneckWhenPeopleWaitForThem()
        {
            var sim = TestData.Quiet();
            foreach (var bd in sim.S.Beds) bd.State = BedState.Dirty;
            for (int i = 0; i < 3; i++) { var w = TestData.Walk(sim, Sev.Yellow); w.Tag = 1; w.Stage = Stage.Bay; }
            Assert.AreEqual("dirty", sim.FindBottleneck().Key);
        }
    }

    public class ShiftTests
    {
        [Test]
        public void HandoverOnlyInTheWindowAndItEndsTheShiftCalmly()
        {
            var sim = TestData.Quiet();
            sim.StartDay();
            Assert.AreEqual("window", sim.Handover());
            sim.S.Fatigue[Teams.Nurse] = 60;
            sim.AdvanceMinutes(sim.Gd.HandoverFrom - sim.S.Minute + 1);
            Assert.IsTrue(sim.HandoverWindow);
            Assert.AreEqual("", sim.Handover());
            Assert.AreEqual(Phases.Report, sim.Phase);
            Assert.IsFalse(sim.S.History.Last().Rushed);
            Assert.AreEqual(0, sim.S.Fatigue[Teams.Nurse], 1e-9, "a calm handover rests the team");
        }

        [Test]
        public void NoHandoverMeansARushedOneWithLostNotes()
        {
            var sim = TestData.Quiet();
            sim.StartDay();
            sim.AdvanceMinutes(sim.Gd.DayEnd - sim.S.Minute - 5);
            foreach (var b in sim.S.Beds.Where(b => b.Bay)) { b.State = BedState.Busy; b.Patient = 999; }
            for (int i = 0; i < 3; i++) { var w = TestData.Walk(sim, Sev.Green); w.Tag = 0; w.Stage = Stage.Bay; }
            sim.S.Fatigue[Teams.Doctor] = 80;
            sim.AdvanceMinutes(10);
            Assert.AreEqual(Phases.Report, sim.Phase);
            Assert.IsTrue(sim.S.History.Last().Rushed);
            Assert.AreEqual(80 * sim.Gd.RushedKeep, sim.S.Fatigue[Teams.Doctor], 1.0, "rushed: the team stays tired");
        }

        [Test]
        public void TheNightTeamKeepsTreatingAtReducedCapacity()
        {
            var sim = TestData.Quiet();
            sim.StartDay();
            sim.AdvanceMinutes(sim.Gd.DayEnd - sim.S.Minute - 1);
            for (int i = 0; i < 6; i++) TestData.Walk(sim, Sev.Green);
            sim.AdvanceMinutes(2);
            Assert.AreEqual(Phases.Report, sim.Phase);
            Assert.AreEqual(6, sim.S.History.Last().NightTreated);
            Assert.AreEqual(sim.Gd.DayStart, sim.S.Minute);
            Assert.IsTrue(sim.NextDay());
            Assert.AreEqual(2, sim.S.Day);
            Assert.AreEqual(Phases.Plan, sim.Phase);
        }
    }

    public class AmbulanceTests
    {
        [Test]
        public void AcceptBringsATaggedPatientStraightIn_DivertCostsReputation()
        {
            var sim = TestData.Quiet();
            sim.S.Ambulance = new AmbulanceCall { Sev = Sev.Red, Left = 10 };
            Assert.AreEqual("", sim.DecideAmbulance(true));
            var p = sim.S.Patients.Last();
            Assert.IsTrue(p.Amb);
            Assert.AreEqual(Sev.Red, p.Tag);
            Assert.IsTrue(p.Stage == Stage.Bay || p.Stage == Stage.Test);
            sim.S.Ambulance = new AmbulanceCall { Sev = Sev.Orange, Left = 10 };
            sim.DecideAmbulance(false);
            Assert.AreEqual(1, sim.S.Today.Diverted);
            Assert.AreEqual("none", sim.DecideAmbulance(true));
        }

        [Test]
        public void AnUnansweredCallIsAcceptedUnlessTheDiversionRuleSaysFull()
        {
            var sim = TestData.Quiet();
            sim.StartDay();
            sim.S.Ambulance = new AmbulanceCall { Sev = Sev.Orange, Left = 3 };
            sim.AdvanceMinutes(4);
            Assert.IsNull(sim.S.Ambulance);
            Assert.AreEqual(1, sim.S.Today.Ambulances);
            Assert.AreEqual("research", sim.SetDivert(80));
            sim.S.Research.Add("diversion_rules");
            Assert.AreEqual("", sim.SetDivert(80));
            foreach (var b in sim.S.Beds.Where(b => b.Bay)) { b.State = BedState.Busy; b.Patient = 999; }
            sim.S.Ambulance = new AmbulanceCall { Sev = Sev.Orange, Left = 3 };
            sim.AdvanceMinutes(4);
            Assert.AreEqual(1, sim.S.Today.Diverted);
        }
    }

    public class EconomyTests
    {
        static void PlayDay(Simulation sim)
        {
            sim.StartDay();
            sim.AdvanceMinutes(sim.Gd.DayMinutes + 1);
        }

        [Test]
        public void TheShiftEndsWithAReportStarsAndCosts()
        {
            var sim = TestData.NewSim(5);
            PlayDay(sim);
            var d = sim.S.History.Last();
            Assert.AreEqual(Phases.Report, sim.Phase);
            Assert.Greater(d.Arrivals, 0);
            Assert.Greater(d.Wages, 0);
            Assert.Greater(d.Upkeep, 0);
            Assert.That(d.Stars, Is.InRange(0, 9));
            Assert.IsTrue(sim.DrainEvents().Exists(e => e.Type == "day_done"));
        }

        [Test]
        public void TheGuideAndTheFirstDaysNeverLowerReputation()
        {
            var sim = TestData.NewSim(5);
            sim.S.Teams[Teams.Doctor] = 0;   // a terrible day
            double r = sim.S.Reputation;
            PlayDay(sim);
            Assert.GreaterOrEqual(sim.S.Reputation, r);
        }

        [Test]
        public void NoCrisesDuringTheGuideOrTheFirstDays()
        {
            var sim = TestData.NewSim(5);
            for (int d = 1; d <= sim.Gd.EventFreeDays; d++)
            {
                Assert.AreEqual(0, sim.S.Planned.Count + sim.S.Events.Count, "day " + d);
                PlayDay(sim);
                sim.NextDay();
            }
        }

        [Test]
        public void PurchasesCheckCashWingsAndRooms()
        {
            var sim = TestData.NewSim();
            sim.S.Cash = 100;
            Assert.AreEqual("cash", sim.CanBuy("hire:nurse").Reason);
            sim.S.Cash = 1e6;
            Assert.AreEqual("", sim.Buy("hire:nurse"));
            Assert.AreEqual(3, sim.Team(Teams.Nurse));
            Assert.AreEqual("wing", sim.CanBuy("room:lab").Reason);
            Assert.AreEqual("wing", sim.CanBuy("bed:general").Reason);
            Assert.AreEqual("locked", sim.CanBuy("wing:city").Reason);
            sim.S.Reputation = 10;
            Assert.AreEqual("reputation", sim.CanBuy("wing:town").Reason);
            sim.S.Reputation = 90;
            Assert.AreEqual("", sim.Buy("wing:town"));
            Assert.Greater(sim.BedsIn("general"), 0, "the wing opens with a few beds");
            Assert.AreEqual("", sim.Buy("room:xray"));
            Assert.AreEqual("owned", sim.CanBuy("room:xray").Reason);
            Assert.AreEqual(1, sim.RoomOf("xray").Techs);
            Assert.AreEqual("", sim.Buy("tech:xray"));
            Assert.AreEqual("not_built", sim.CanBuy("tech:lab").Reason);
            for (int i = 0; i < 10; i++) sim.Buy("bay");
            Assert.AreEqual(8, sim.Bays, "clinic + town slots");
            Assert.AreEqual("wing", sim.CanBuy("bay").Reason);
            Assert.AreEqual("context", sim.CanBuy("surge").Reason, "no surge day");
            sim.Fire("flu_surge");
            Assert.IsTrue(sim.CanBuy("surge").Ok);
            sim.Buy("surge");
            Assert.AreEqual(sim.Team(Teams.Doctor) + 1, sim.Avail(Simulation.TDoctor));
        }

        [Test]
        public void UpgradesRunOnShiftTimeOnly()
        {
            var sim = TestData.NewSim();
            sim.S.Cash = 1e5;
            Assert.AreEqual("", sim.StartResearch("triage_training"));
            Assert.AreEqual("busy", sim.CanResearch("porter_tablets"));
            sim.Advance(1);   // plan phase: no shift time
            Assert.AreEqual("triage_training", sim.S.Researching);
            sim.StartDay();
            sim.AdvanceMinutes(sim.Gd.ResearchById["triage_training"].Minutes + 1);
            Assert.IsTrue(sim.Has("triage_training"));
            Assert.AreEqual(0.95, sim.Accuracy, 1e-9);
            Assert.AreEqual("wing", sim.CanResearch("night_team"));
            Assert.AreEqual("requires", sim.CanResearch("quick_clean"));
        }

        [Test]
        public void OfflinePayIsCappedAndScalesWithOfflinePlus()
        {
            var sim = TestData.NewSim();
            sim.S.History.Add(new DayStats { Fees = 4000 });
            sim.Touch(1000);
            var o = sim.Offline(1000 + 10 * 3600);
            Assert.IsNotNull(o);
            Assert.IsTrue(o.Capped);
            Assert.AreEqual(sim.Gd.OfflineCapH, o.Hours, 1e-9);
            double plain = o.Amount;
            sim.SetPerks(new Dictionary<string, bool> { { "offline_plus", true } });
            sim.Touch(1000);
            var o2 = sim.Offline(1000 + 10 * 3600);
            Assert.Greater(o2.Amount, plain * 2);
            Assert.IsNull(sim.Offline(1000 + 10 * 3600 + 30), "too short");
        }

        [Test]
        public void GrantCashUsesTheUsualDayIncome()
        {
            var sim = TestData.NewSim();
            Assert.AreEqual(sim.Gd.DayIncomeFloor, sim.DayIncome());
            sim.S.History.Add(new DayStats { Fees = 3000 });
            double c = sim.S.Cash;
            Assert.AreEqual(1500, sim.GrantCash(0.5), 1e-6);
            Assert.AreEqual(c + 1500, sim.S.Cash, 1e-6);
        }
    }

    public class CrisisTests
    {
        [Test]
        public void EveryCrisisFiresWithAVisibleEffectAndNeverEndsTheGame()
        {
            foreach (var e in TestData.Gd().Events)
            {
                var sim = TestData.NewSim(11);
                sim.S.Cash = 1e5;
                sim.Buy("room:xray");
                sim.S.Teams[Teams.Nurse] = 3;
                sim.StartDay();
                sim.AdvanceMinutes(30);
                Assert.IsTrue(sim.Fire(e.Id), e.Id);
                Assert.IsTrue(sim.ActiveCrises().Count > 0 || e.Id == "power_outage" && sim.Has("generator"), e.Id + " shows on the HUD");
                sim.AdvanceMinutes(sim.Gd.DayMinutes);
                Assert.AreEqual(Phases.Report, sim.Phase, e.Id);
                Assert.IsTrue(sim.NextDay(), e.Id);
            }
        }

        [Test]
        public void SpecificEffects()
        {
            var sim = TestData.NewSim(11);
            sim.S.Cash = 1e5;
            sim.Buy("room:xray");
            sim.Fire("scanner_down");
            Assert.Greater(sim.RoomOf("xray").Down, 0);
            sim.Fire("norovirus");
            Assert.AreEqual(2, sim.S.Beds.Count(b => b.State == BedState.Closed));
            int n = sim.S.Patients.Count;
            sim.Fire("crash");
            Assert.Greater(sim.S.Patients.Count, n);
            double rate = sim.ArrivalRate();
            sim.Fire("flu_surge");
            Assert.Greater(sim.ArrivalRate(), rate);
            sim.S.Research.Add("generator");
            sim.Fire("power_outage");
            Assert.IsFalse(sim.S.Events.Exists(e => e.Id == "power_outage"), "the generator keeps the power on");
        }
    }

    public class RewardTests
    {
        const long Now = 1_800_000_000;

        [Test]
        public void RewardsAreNeverOfferedDuringTheGuide()
        {
            var sim = TestData.NewSim(tutorial: true);
            foreach (var p in Simulation.RewardPlacements) Assert.AreEqual("tutorial", sim.RewardOffer(p, Now).Reason, p);
        }

        [Test]
        public void DailyCapAndCooldownPersistInTheCareer()
        {
            var sim = TestData.NewSim();
            sim.StartDay();
            Assert.IsTrue(sim.GrantReward("float_nurse", Now).Ok);
            Assert.AreEqual("context", sim.RewardOffer("float_nurse", Now + 1000).Reason, "one float at a time");
            sim.S.FloatLeft = 0;
            Assert.AreEqual("cooldown", sim.RewardOffer("float_nurse", Now + 10).Reason);
            var re = SaveCodec.Decode(SaveCodec.Encode(sim), sim.Gd).Sim;
            Assert.AreEqual("cooldown", re.RewardOffer("float_nurse", Now + 10).Reason, "a relaunch does not reset it");
            Assert.IsTrue(sim.GrantReward("daily_bonus", Now).Ok);
            Assert.AreEqual("cap", sim.RewardOffer("daily_bonus", Now + 60).Reason);
            Assert.IsTrue(sim.RewardOffer("daily_bonus", Now + 86400).Ok, "a new UTC day");
        }

        [Test]
        public void CashRewardsStayUnderOneFifthOfTheCheapestPack()
        {
            var sim = TestData.NewSim();
            sim.S.History.Add(new DayStats { Fees = 5000 });
            sim.S.OfflinePending = 1e6;
            double day = sim.DayIncome();
            foreach (var p in new[] { "offline_x2", "daily_bonus" })
                Assert.LessOrEqual(sim.RewardOffer(p, Now).Amount, day * 0.2 + 1e-6, p);
        }

        [Test]
        public void TheNeighbourHospitalHelpsOnlyInACrisisOnce()
        {
            var sim = TestData.Quiet();
            sim.StartDay();
            for (int i = 0; i < 8; i++) TestData.Walk(sim, Sev.Green);
            Assert.AreEqual("context", sim.RewardOffer("call_neighbor", Now).Reason, "no crisis");
            sim.S.Events.Add(new ActiveEvent { Id = "flu_surge", UntilDay = sim.S.Day });
            var o = sim.RewardOffer("call_neighbor", Now);
            Assert.IsTrue(o.Ok);
            Assert.AreEqual(5, o.Amount);
            var r = sim.GrantReward("call_neighbor", Now);
            Assert.AreEqual(5, r.Amount);
            Assert.AreEqual(3, sim.S.Patients.Count);
            Assert.AreEqual(0, sim.S.Today.Lwbs + sim.S.Today.Transferred, "no reputation cost");
            Assert.AreEqual("context", sim.RewardOffer("call_neighbor", Now + 10000).Reason, "once per crisis");
        }

        [Test]
        public void FloatStaffJoinsTheBusiestTeamForAnHour()
        {
            var sim = TestData.Quiet();
            sim.StartDay();
            for (int i = 0; i < 6; i++) TestData.Walk(sim, Sev.Green);   // a triage queue: nurses
            int before = sim.Avail(Simulation.TNurse);
            Assert.IsTrue(sim.GrantReward("float_nurse", Now).Ok);
            Assert.AreEqual(Teams.Nurse, sim.S.FloatTeam);
            Assert.AreEqual(before + 1, sim.Avail(Simulation.TNurse));
            sim.AdvanceMinutes(61);
            Assert.AreEqual(before, sim.Avail(Simulation.TNurse));
        }
    }

    public class SaveTests
    {
        [Test]
        public void RoundTripMidShiftIsIdenticalAndKeepsPlaying()
        {
            var sim = TestData.NewSim(11);
            sim.S.Cash = 1e5;
            sim.Buy("room:xray");
            sim.StartDay();
            sim.AdvanceMinutes(240);
            sim.Fire("scanner_down");
            string a = SaveCodec.Encode(sim);
            var r = SaveCodec.Decode(a, sim.Gd);
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(a, SaveCodec.Encode(r.Sim));
            sim.AdvanceMinutes(100);
            r.Sim.EmitCrowd = false;
            r.Sim.AdvanceMinutes(100);
            Assert.AreEqual(SaveCodec.Encode(sim), SaveCodec.Encode(r.Sim), "deterministic after a reload");
        }

        [Test]
        public void DecoderRefusesWhatTheSimNeverWrites()
        {
            var gd = TestData.Gd();
            Assert.IsFalse(SaveCodec.Decode("", gd).Ok);
            Assert.IsFalse(SaveCodec.Decode("{\"game\":\"bottle-route\",\"v\":1}", gd).Ok);
            var sim = TestData.NewSim();
            string ok = SaveCodec.Encode(sim);
            Assert.IsTrue(SaveCodec.Decode(ok, gd).Ok);
            Assert.IsFalse(SaveCodec.Decode(ok.Replace("\"phase\":\"plan\"", "\"phase\":\"party\""), gd).Ok);
            Assert.IsFalse(SaveCodec.Decode(ok.Replace("\"stage\":\"triage\"", "\"stage\":\"teleport\""), gd).Ok);
            Assert.IsFalse(SaveCodec.Decode(ok.Replace("\"queue\":\"balanced\"", "\"queue\":\"vip\""), gd).Ok);
        }

        [Test]
        public void DecoderAcceptsNegativeCashAndTheReportScreen()
        {
            var sim = TestData.NewSim();
            sim.S.Cash = -1234.5;
            sim.StartDay();
            sim.AdvanceMinutes(sim.Gd.DayMinutes + 1);
            Assert.AreEqual(Phases.Report, sim.Phase);
            var r = SaveCodec.Decode(SaveCodec.Encode(sim), sim.Gd);
            Assert.IsTrue(r.Ok, r.Error);
            Assert.IsTrue(r.Sim.NextDay());
        }

        [Test]
        public void FuzzEveryMinuteOfAShiftDecodes()
        {
            // fleet §14 save fuzz: guide phase, purchase moments, porters mid-corridor, surgery, a crisis, the report — 0 unreadable
            var sim = TestData.NewSim(21, tutorial: true);
            sim.S.Cash = 1e6;
            sim.S.Reputation = 95;
            sim.StartArrivals();
            sim.StartDay();
            int bad = 0;
            for (int m = 0; m < sim.Gd.DayMinutes + 2; m++)
            {
                if (m == 10) sim.Buy("room:xray");
                if (m == 30) { sim.Buy("wing:town"); sim.Buy("room:lab"); }
                if (m == 60) sim.EndTutorial();
                if (m == 61) { sim.Buy("wing:city"); sim.Buy("room:or"); sim.Buy("room:ct"); sim.Buy("hire:doctor"); }
                if (m == 120) sim.Fire("crash");
                if (m == 200) sim.Fire("norovirus");
                if (m == 300) sim.S.Ambulance = new AmbulanceCall { Sev = Sev.Red, Left = 5 };
                sim.AdvanceMinutes(1);
                var r = SaveCodec.Decode(SaveCodec.Encode(sim), sim.Gd);
                if (!r.Ok) { bad++; TestContext.Progress.WriteLine("minute " + m + ": " + r.Error); }
            }
            Assert.AreEqual(0, bad);
        }
    }

    public class BalanceTests
    {
        [Test]
        public void SevenDaysStayFiniteProfitableAndNeverStuck()
        {
            var rep = BalanceRunner.Run(TestData.Gd(), 42, 7);
            TestContext.Progress.WriteLine("BALANCE 7 days seed 42\n" + rep.Table());
            Assert.IsTrue(rep.Finite);
            Assert.AreEqual(7, rep.Days.Count);
            double week = 0;
            foreach (var d in rep.Days) { Assert.Greater(d.Treated, 0, "day " + d.Day); week += d.Net; }
            Assert.Greater(week, 0, "a sensible player earns over a week (money also went into rooms / staff)");
            // polish-pass tempo: the Town Hospital opens around shift 13-14 (~1 h), so week 1 grows the clinic, not the wing
            Assert.Greater(rep.Beds, 6, "the hospital grows: more beds than the starting six");
            Assert.Greater(rep.Staff, 6, "the hospital grows: more staff than the starting six teams");
            Assert.Greater(rep.MinCash, 0, "never broke");
            Assert.GreaterOrEqual(rep.Days.Average(d => d.Stars), 4.5, "no wall: most shifts go well");
        }

        [Test]
        public void FirstDayFeelsGood()
        {
            foreach (long seed in new long[] { 3, 9, 42 })
            {
                var d = BalanceRunner.Run(TestData.Gd(), seed, 1).Days[0];
                Assert.Greater(d.Net, 0, "day 1 earns money");
                Assert.Less(d.BadRate, 0.2, "day 1 is not a wall of waiting");
            }
        }

        [Test]
        public void TwentyDaysReachTheTownHospital()
        {
            var rep = BalanceRunner.Run(TestData.Gd(), 9, 20);
            TestContext.Progress.WriteLine("BALANCE 20 days seed 9\n" + rep.Table() + string.Join("\n", rep.Log));
            Assert.GreaterOrEqual(rep.Chapter, 2, "Town Hospital within 20 shifts (~80 min of play)");
            Assert.Greater(rep.Beds, 8);
        }

        [Test]
        public void ChaptersArriveOnThePlanClock()
        {
            // plan §6: chapter 2 at ~1 h, chapter 3 at ~3 h of play; a shift = 4 real min at 1x (+ plan / report screens)
            // -> Town Hospital around shift 15, City General around shift 45, within about ±25 %
            foreach (long seed in new long[] { 9, 42 })
            {
                var rep = BalanceRunner.Run(TestData.Gd(), seed, 56);
                int town = -1, city = -1;
                foreach (var l in rep.Log)
                {
                    var p = l.Split(' ');
                    if (p.Length < 3) continue;
                    if (p[2] == "wing:town" && town < 0) town = int.Parse(p[1]);
                    if (p[2] == "wing:city" && city < 0) city = int.Parse(p[1]);
                }
                TestContext.Progress.WriteLine("BALANCE seed " + seed + ": Town Hospital day " + town + ", City General day " + city);
                Assert.That(town, Is.InRange(11, 19), "Town Hospital at ~1 h (seed " + seed + ")");
                Assert.That(city, Is.InRange(34, 56), "City General at ~3 h (seed " + seed + ")");
            }
        }
    }
}
