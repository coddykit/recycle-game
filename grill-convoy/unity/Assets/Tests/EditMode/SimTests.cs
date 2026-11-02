// Grill Convoy — engine-free sim tests (NUnit). Same files run in the Unity Test Runner (EditMode) and under plain .NET
// (Tools/dotnet_check.sh). Data is read from Assets/Resources/data on disk.
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using GrillConvoy.Sim;

namespace GrillConvoy.Sim.Tests
{
    public static class TestData
    {
        static GameData cached;

        public static string UnityDir()
        {
            string env = Environment.GetEnvironmentVariable("GRILLCONVOY_UNITY");
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
            var s = new Simulation(Gd(), seed) { EmitCrowd = false };
            if (!tutorial) s.EndTutorial();
            return s;
        }
    }

    public class DataTests
    {
        [Test]
        public void DataLoadsWithoutErrors()
        {
            var gd = TestData.Gd();
            Assert.IsTrue(gd.Ok, string.Join("; ", gd.Errors));
            Assert.AreEqual(4, gd.Districts.Count);
            Assert.GreaterOrEqual(gd.Research.Count, 20);
        }

        [Test]
        public void EverySiteReachableFromDepotByVan_AndAlleysBlockTheCompactor()
        {
            var gd = TestData.Gd();
            foreach (var n in gd.Nodes)
            {
                Assert.Less(gd.Graph.Km(gd.DepotNode, n.Index, true), RoadGraph.Inf, n.Id);
                if (n.Kind == "site" && n.Alley)
                    Assert.IsFalse(gd.Graph.Reachable(gd.DepotNode, n.Index, false) && !n.Alley, n.Id);
            }
            int church = gd.Node("o_church");
            Assert.AreEqual(RoadGraph.Inf, gd.Graph.Km(gd.DepotNode, church, false), "the church alley is van/bike only");
        }

        [Test]
        public void PathsAreConnectedAndMatchKm()
        {
            var gd = TestData.Gd();
            int a = gd.DepotNode, b = gd.Node("u_dorms");
            var p = gd.Graph.Path(a, b);
            Assert.AreEqual(a, p[0]);
            Assert.AreEqual(b, p[p.Count - 1]);
            Assert.AreEqual(gd.Graph.Km(a, b), gd.Graph.PathKm(p), 1e-6);
        }

        [Test]
        public void ResearchNeverLongerThanTwentyMinutes()
        {
            foreach (var r in TestData.Gd().Research) Assert.LessOrEqual(r.Minutes, 20, r.Id);
        }
    }

    public class CoreTests
    {
        [Test]
        public void NewCareerStartsInThePlanWithThreeMachinesTwoVansOneDriver()
        {
            var sim = TestData.NewSim(1, tutorial: true);
            Assert.AreEqual(Phases.Plan, sim.Phase);
            Assert.AreEqual(3, sim.S.Points.Count);
            Assert.AreEqual(2, sim.S.Trucks.Count);
            Assert.AreEqual(1, sim.S.Drivers);
            Assert.IsTrue(sim.S.Tutorial);
            Assert.Greater(sim.FillPct(sim.S.Points[0]), 0.75, "the first machine starts almost full (guide step 2)");
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
        public void MachineFillsThenTurnsCitizensAway()
        {
            var sim = TestData.NewSim();
            var p = sim.S.Points[0];
            sim.FillTo(p, 1.0);
            sim.StartDay();
            int turned = p.Turned;
            for (int i = 0; i < 40; i++) sim.Deposit(0, 20, new[] { 1.0, 0, 0 }, 0);
            Assert.Greater(p.Turned, turned);
            Assert.IsTrue(sim.IsFull(p) || sim.FillPct(p) > 0.95);
        }

        [Test]
        public void EstimateWithoutSensorDiffersFromLiveSensor()
        {
            var sim = TestData.NewSim();
            var p = sim.S.Points[1];
            sim.StartDay();
            sim.Fire("heat_wave");
            sim.AdvanceMinutes(120);
            double est = sim.DisplayPct(p);
            p.Sensor = true;
            Assert.AreEqual(sim.FillPct(p), sim.DisplayPct(p), 1e-9, "a sensor shows the real bar");
            Assert.AreNotEqual(sim.FillPct(p), est, "without a sensor the bar is an estimate (heat wave not in it)");
        }

        [Test]
        public void JammedMachineTurnsEveryoneAwayUntilATechnicianFixesIt()
        {
            var sim = TestData.NewSim();
            sim.S.Techs = 1;
            sim.StartDay();
            sim.Jam(0, "test");
            int t0 = sim.S.Points[0].Turned;
            Assert.AreEqual(0, sim.Deposit(0, 3, new[] { 1.0, 0, 0 }, 0));
            Assert.AreEqual(t0 + 1, sim.S.Points[0].Turned);
            Assert.AreEqual("", sim.SendTech("point", 0));
            Assert.AreEqual("already", sim.SendTech("point", 0));
            sim.AdvanceMinutes(90);
            Assert.IsFalse(sim.S.Points[0].Jammed);
        }
    }

    public class FleetTests
    {
        [Test]
        public void TheCrewClearsAJamWhenTheyEmptyTheMachine()
        {
            var sim = TestData.NewSim();
            sim.Jam(0, "t");
            Assert.Contains(0, sim.SuggestRoute(0), "a jammed machine is always due");
            sim.StartDay();
            sim.Dispatch(0, new List<int> { 0 });
            for (int i = 0; i < 200 && sim.S.Points[0].Jammed; i++) sim.AdvanceMinutes(1);
            Assert.IsFalse(sim.S.Points[0].Jammed);
        }

        [Test]
        public void DispatchNeedsAFreeDriver()
        {
            var sim = TestData.NewSim();
            Assert.AreEqual("", sim.Dispatch(0, new List<int> { 0 }));
            Assert.AreEqual("no_driver", sim.Dispatch(1, new List<int> { 1 }));
            sim.S.Cash = 10000;
            Assert.AreEqual("", sim.Buy("driver"));
            Assert.AreEqual("", sim.Dispatch(1, new List<int> { 1 }));
        }

        [Test]
        public void TruckEmptiesMachinesUnloadsAndTheDepotEarnsFees()
        {
            var sim = TestData.NewSim();
            double before = sim.FillPct(sim.S.Points[0]);
            Assert.Greater(before, 0.5);
            sim.StartDay();
            Assert.AreEqual("", sim.Dispatch(0, new List<int> { 0, 1 }));
            bool serviced = false, unloaded = false;
            for (int i = 0; i < 300 && !unloaded; i++)
            {
                sim.AdvanceMinutes(1);
                foreach (var e in sim.DrainEvents())
                {
                    if (e.Type == "serviced" && e.B == 0) serviced = true;
                    if (e.Type == "unloaded") unloaded = true;
                }
            }
            Assert.IsTrue(serviced && unloaded);
            Assert.Less(sim.FillPct(sim.S.Points[0]), before);
            Assert.Greater(sim.S.Today.Km, 0);
            Assert.Greater(sim.S.Today.Fuel, 0);
            sim.AdvanceMinutes(120);
            Assert.Greater(sim.S.Depot.Till + sim.S.Today.Fees, 0, "the count line earned handling fees");
            Assert.Greater(sim.CollectTill(), 0);
        }

        [Test]
        public void GlassFillsTheTruckByWeightBeforeVolume()
        {
            var sim = TestData.NewSim();
            var t = sim.S.Trucks[0];
            t.Load[Mats.Glass] = 1700;  // 646 kg of 700, only 1530 l of 1800
            Assert.IsTrue(sim.LoadLimitedByWeight(t));
            Assert.Greater(sim.LoadPct(t), 0.9);
        }

        [Test]
        public void CompactorTruckCannotServeAlleyPoints()
        {
            var sim = TestData.NewSim();
            sim.S.Cash = 1e6;
            sim.S.Districts.Add("oldtown");
            sim.S.Research.Add("compactor_truck");
            sim.S.Research.Add("fuel_saver");
            Assert.AreEqual("", sim.Buy("machine:o_church"));
            Assert.AreEqual("", sim.Buy("truck:compactor"));
            int church = sim.S.Points.Count - 1, comp = sim.S.Trucks.Count - 1;
            Assert.IsFalse(sim.CanServe(sim.S.Trucks[comp], sim.S.Points[church]));
            Assert.IsTrue(sim.CanServe(sim.S.Trucks[0], sim.S.Points[church]));
        }

        [Test]
        public void SuggestRouteServesDuePointsOnlyAndRespectsPolicies()
        {
            var sim = TestData.NewSim();
            foreach (var p in sim.S.Points) p.Sensor = true;
            sim.FillTo(sim.S.Points[0], 0.9);
            sim.FillTo(sim.S.Points[1], 0.2);
            sim.FillTo(sim.S.Points[2], 0.85);
            sim.SetPolicy(2, Policies.Call, 0);
            var r = sim.SuggestRoute(0);
            CollectionAssert.Contains(r, 0);
            CollectionAssert.DoesNotContain(r, 1);
            CollectionAssert.DoesNotContain(r, 2, "on-call points wait for a manual route");
            sim.SetPolicy(1, Policies.Threshold, 60);
            sim.FillTo(sim.S.Points[1], 0.65);
            CollectionAssert.Contains(sim.SuggestRoute(0), 1);
        }

        [Test]
        public void EstimateGivesKmCostAndLoad()
        {
            var sim = TestData.NewSim();
            var e = sim.Estimate(0, new List<int> { 0, 1, 2 });
            Assert.IsTrue(e.Ok);
            Assert.Greater(e.Km, 1);
            Assert.Greater(e.Cost, 0);
            Assert.Greater(e.Minutes, 10);
            Assert.Greater(e.LoadPct, 0);
        }

        [Test]
        public void RecallSendsTheTruckHome()
        {
            var sim = TestData.NewSim();
            sim.StartDay();
            sim.Dispatch(0, new List<int> { 0, 1, 2 });
            sim.AdvanceMinutes(2);
            Assert.AreEqual("", sim.Recall(0));
            for (int i = 0; i < 200 && sim.S.Trucks[0].Busy; i++) sim.AdvanceMinutes(1);
            Assert.IsFalse(sim.S.Trucks[0].Busy);
        }

        [Test]
        public void DispatcherDrivesAutoTrucksByPolicy()
        {
            var sim = TestData.NewSim();
            sim.S.Cash = 1e5;
            sim.S.Research.Add("route_optimizer");
            sim.S.Research.Add("dispatcher");
            Assert.AreEqual("", sim.Buy("dispatcher"));
            Assert.IsTrue(sim.S.Trucks[0].Auto);
            foreach (var p in sim.S.Points) p.Sensor = true;
            sim.FillTo(sim.S.Points[0], 0.95);
            sim.StartDay();
            sim.AdvanceMinutes(2);
            Assert.IsTrue(sim.S.Trucks[0].Busy, "the Dispatcher sent the auto truck to the full machine");
        }

        [Test]
        public void AutoSlotsAreCapped()
        {
            var sim = TestData.NewSim();
            Assert.AreEqual("no_dispatcher", sim.SetAuto(0, true));
            sim.S.Depot.Dispatcher = true;
            for (int i = 0; i < 4; i++) { sim.S.Trucks.Add(new TruckState { Id = 10 + i, Type = "van", AtNode = sim.Gd.DepotNode }); }
            int ok = 0;
            for (int i = 0; i < sim.S.Trucks.Count; i++) if (sim.SetAuto(i, true) == "") ok++;
            Assert.AreEqual(sim.Gd.DispatcherSlots, ok);
            sim.SetPerks(new Dictionary<string, bool> { { "dispatcher_plus", true } });
            Assert.AreEqual(sim.Gd.DispatcherSlots + sim.Gd.PlusSlots, sim.AutoSlots);
        }
    }

    public class DepotTests
    {
        [Test]
        public void CountLineThenBalerMakesBalesAndTheyCanBeSold()
        {
            var sim = TestData.NewSim();
            sim.S.Depot.Intake[Mats.Pet] = 2500;   // 75 kg PET
            sim.S.Depot.Intake[Mats.Alu] = 2500;   // 37.5 kg alu
            sim.StartDay();
            sim.AdvanceMinutes(400);
            Assert.Greater(sim.S.Depot.Bales[Mats.Pet] + sim.S.Depot.Bales[Mats.Alu], 0);
            double cash = sim.S.Cash;
            double got = sim.Sell(Mats.Pet) + sim.Sell(Mats.Alu);
            Assert.Greater(got, 0);
            Assert.AreEqual(cash + got, sim.S.Cash, 1e-6);
            Assert.AreEqual(0, sim.Sell(Mats.Pet));
        }

        [Test]
        public void FullYardStopsTheBaler_AndBalerDownStopsIt()
        {
            var sim = TestData.NewSim();
            sim.S.Depot.Bales[Mats.Pet] = sim.S.Depot.YardCap;
            sim.S.Depot.BalerIn[Mats.Pet] = 500;
            sim.StartDay();
            sim.AdvanceMinutes(60);
            Assert.AreEqual(500, sim.S.Depot.BalerIn[Mats.Pet], 1e-9);
            sim.Sell(Mats.Pet);
            sim.BalerDown("test");
            sim.AdvanceMinutes(60);
            Assert.AreEqual(500, sim.S.Depot.BalerIn[Mats.Pet], 1e-9);
        }

        [Test]
        public void BaleCrashLowersOnlyThatMaterial()
        {
            var sim = TestData.NewSim();
            double[] before = { sim.BalePrice(0), sim.BalePrice(1), sim.BalePrice(2) };
            sim.Fire("bale_crash");
            var e = sim.S.Events.Find(x => x.Id == "bale_crash");
            for (int m = 0; m < Mats.N; m++)
                if (m == e.Target) Assert.Less(sim.BalePrice(m), before[m]);
                else Assert.AreEqual(before[m], sim.BalePrice(m), 1e-9);
        }

        [Test]
        public void CountingCentreTakesBulkHaulsWithoutBlockingAMachine()
        {
            var sim = TestData.NewSim();
            sim.StartDay();
            sim.Fire("bulk_returner");
            bool blocked = sim.S.Points.Exists(p => p.BusyUntil > sim.S.Minute);
            Assert.IsTrue(blocked);
            var sim2 = TestData.NewSim();
            sim2.S.Depot.Centre = true;
            sim2.StartDay();
            double cash = sim2.S.Cash;
            sim2.Fire("bulk_returner");
            Assert.IsFalse(sim2.S.Points.Exists(p => p.BusyUntil > sim2.S.Minute));
            Assert.Greater(sim2.S.Cash, cash);
        }
    }

    public class EconomyTests
    {
        static void PlayDay(Simulation sim) => BalanceRunner.PlayDay(sim, new BalanceReport());

        [Test]
        public void DayEndsWithAReportStarsAndCosts()
        {
            var sim = TestData.NewSim();
            PlayDay(sim);
            Assert.AreEqual(Phases.Report, sim.Phase);
            var d = sim.S.History[sim.S.History.Count - 1];
            Assert.Greater(d.Wages, 0);
            Assert.Greater(d.Collected, 0);
            Assert.That(d.Stars, Is.InRange(0, 9));
            Assert.IsTrue(sim.NextDay());
            Assert.AreEqual(2, sim.S.Day);
            Assert.AreEqual(Phases.Plan, sim.Phase);
        }

        [Test]
        public void TrucksComeHomeAtDayEnd()
        {
            var sim = TestData.NewSim();
            sim.StartDay();
            sim.AdvanceMinutes(sim.Gd.DayMinutes - 3);
            sim.Dispatch(0, new List<int> { 0, 1, 2 });
            sim.AdvanceMinutes(10);
            Assert.AreEqual(Phases.Report, sim.Phase);
            foreach (var t in sim.S.Trucks) Assert.IsFalse(t.Busy);
        }

        [Test]
        public void FirstDaysAndTheGuideNeverLowerTheRating()
        {
            var sim = TestData.NewSim(3, tutorial: true);
            double r0 = sim.S.Rating;
            sim.StartDay();
            sim.AdvanceMinutes(sim.Gd.DayMinutes + 5);   // nobody empties anything
            Assert.GreaterOrEqual(sim.S.Rating, r0);
        }

        [Test]
        public void NoCrisesDuringTheGuideOrTheFirstDays()
        {
            var sim = TestData.NewSim(5);
            for (int d = 0; d < sim.Gd.EventFreeDays; d++)
            {
                PlayDay(sim);
                foreach (var e in sim.DrainEvents()) Assert.AreNotEqual("crisis", e.Type);
                sim.NextDay();
            }
            Assert.AreEqual(sim.Gd.EventFreeDays + 1, sim.S.Day);
        }

        [Test]
        public void PurchasesCheckCashResearchAndDistricts()
        {
            var sim = TestData.NewSim();
            sim.S.Cash = 100;
            Assert.AreEqual("cash", sim.Buy("machine:h_quay"));
            sim.S.Cash = 1e6;
            Assert.AreEqual("", sim.Buy("machine:h_quay"));
            Assert.AreEqual("owned", sim.Buy("machine:h_quay"));
            Assert.AreEqual("locked", sim.Buy("machine:o_square"));
            Assert.AreEqual("research", sim.Buy("sensor:0"));
            Assert.AreEqual("", sim.Buy("bin_m:0"), "the bigger bin needs no research (guide step 13)");
            sim.S.Rating = 10;
            Assert.AreEqual("rating", sim.Buy("district:oldtown"));
            sim.S.Rating = 60;
            Assert.AreEqual("", sim.Buy("district:oldtown"));
            Assert.AreEqual("", sim.Buy("machine:o_square"));
            Assert.AreEqual("locked", sim.Buy("district:stadium"), "chapters unlock in order");
        }

        [Test]
        public void ResearchRunsOnRealTimeAndCatchesUpOffline()
        {
            var sim = TestData.NewSim();
            sim.S.Cash = 1e5;
            Assert.AreEqual("", sim.StartResearch("sensors"));
            Assert.AreEqual("busy", sim.StartResearch("route_optimizer"));
            sim.Advance(1.0);
            Assert.IsFalse(sim.Has("sensors"));
            sim.Touch(1000);
            sim.Offline(1000 + 3600);
            Assert.IsTrue(sim.Has("sensors"));
            Assert.AreEqual("requires", sim.CanResearch("jam_guard"));
        }

        [Test]
        public void OfflinePayIsCappedAndScalesWithOfflinePlus()
        {
            var sim = TestData.NewSim();
            sim.S.History.Add(new DayStats { Fees = 1000 });
            const long t0 = 1_800_000_000;
            sim.Touch(t0);
            Assert.IsNull(sim.Offline(t0 + 30), "short absence pays nothing");
            sim.Touch(t0);
            var a = sim.Offline(t0 + 100 * 3600);
            Assert.IsTrue(a.Capped);
            Assert.AreEqual(sim.Gd.OfflineCapH, a.Hours, 1e-9);
            sim.SetPerks(new Dictionary<string, bool> { { "offline_plus", true } });
            sim.Touch(t0);
            var b = sim.Offline(t0 + 100 * 3600);
            Assert.Greater(b.Amount, a.Amount);
        }

        [Test]
        public void GrantCashUsesTheUsualDayIncome()
        {
            var sim = TestData.NewSim();
            double floor = sim.Gd.DayIncomeFloor;
            Assert.AreEqual(floor, sim.DayIncome());
            Assert.AreEqual(floor * 2.5, sim.GrantCash(2.5), 1e-9);
        }
    }

    public class RewardTests
    {
        [Test]
        public void RewardsAreNeverOfferedDuringTheGuide()
        {
            var sim = TestData.NewSim(1, tutorial: true);
            Assert.AreEqual("tutorial", sim.RewardOffer("daily_bonus", 100000).Reason);
        }

        [Test]
        public void DailyCapAndCooldownPersistInTheCareer()
        {
            var sim = TestData.NewSim();
            long now = 86400L * 20000 + 100;
            Assert.IsTrue(sim.GrantReward("fuel_voucher", now).Ok);
            Assert.AreEqual("context", sim.RewardOffer("fuel_voucher", now + 5000).Reason, "a voucher is waiting");
            sim.S.FuelVoucher = false;
            Assert.AreEqual("cooldown", sim.RewardOffer("fuel_voucher", now + 10).Reason);
            Assert.IsTrue(sim.GrantReward("fuel_voucher", now + 1000).Ok);
            sim.S.FuelVoucher = false;
            Assert.AreEqual("cap", sim.RewardOffer("fuel_voucher", now + 5000).Reason);
            var back = SaveCodec.Decode(SaveCodec.Encode(sim), sim.Gd).Sim;
            back.S.FuelVoucher = false;
            Assert.AreEqual("cap", back.RewardOffer("fuel_voucher", now + 5000).Reason, "caps survive a relaunch");
            Assert.IsTrue(back.RewardOffer("fuel_voucher", now + 86400).Ok, "a new UTC day resets the cap");
        }

        [Test]
        public void CashRewardsStayUnderOneFifthOfTheCheapestPack()
        {
            var sim = TestData.NewSim();
            sim.S.OfflinePending = 1e6;
            long now = 86400L * 20000;
            double single = sim.Gd.RewardSingleMaxDays * sim.DayIncome();
            Assert.LessOrEqual(sim.RewardOffer("offline_x2", now).Amount, single + 1e-9);
            Assert.LessOrEqual(sim.RewardOffer("daily_bonus", now).Amount, single + 1e-9);
            Assert.LessOrEqual(sim.Gd.RewardSingleMaxDays, 0.2 + 1e-9);
        }

        [Test]
        public void EmergencyEmptyNeedsAFullMachineAndEmptiesIt()
        {
            var sim = TestData.NewSim();
            long now = 86400L * 20000;
            foreach (var p in sim.S.Points) sim.FillTo(p, 0.3);
            Assert.AreEqual("context", sim.RewardOffer("emergency_empty", now).Reason);
            sim.FillTo(sim.S.Points[1], 1.0);
            var r = sim.GrantReward("emergency_empty", now);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(0, sim.Containers(sim.S.Points[1]), 1e-9);
            Assert.Greater(sim.S.Depot.IntakeTotal, 0);
        }

        [Test]
        public void BalerRescueOnlyWhenTheBalerIsDown()
        {
            var sim = TestData.NewSim();
            long now = 86400L * 20000;
            Assert.IsFalse(sim.RewardOffer("baler_rescue", now).Ok);
            sim.BalerDown("t");
            Assert.IsTrue(sim.GrantReward("baler_rescue", now).Ok);
            Assert.IsFalse(sim.S.Depot.BalerDown);
        }
    }

    public class SaveTests
    {
        [Test]
        public void RoundTripMidDayIsIdenticalAndKeepsPlaying()
        {
            var sim = TestData.NewSim(11);
            sim.StartDay();
            sim.Dispatch(0, new List<int> { 0, 1, 2 });
            sim.AdvanceMinutes(37);
            sim.Jam(2, "t");
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
            Assert.IsFalse(SaveCodec.Decode("{\"game\":\"volt-city\",\"v\":1}", gd).Ok);
            var sim = TestData.NewSim();
            string ok = SaveCodec.Encode(sim);
            Assert.IsFalse(SaveCodec.Decode(ok.Replace("\"type\":\"van\"", "\"type\":\"ufo\""), gd).Ok);
            Assert.IsFalse(SaveCodec.Decode(ok.Replace("\"phase\":\"plan\"", "\"phase\":\"party\""), gd).Ok);
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
        public void FuzzEveryMinuteOfADayDecodes()
        {
            // fleet §14 save fuzz: guide phase, purchase moment, mid-route, unloading, report — 0 unreadable saves
            var sim = TestData.NewSim(21, tutorial: true);
            sim.S.Cash = 1e5;
            sim.StartDay();
            sim.Dispatch(0, new List<int> { 0, 1, 2 });
            int bad = 0;
            for (int m = 0; m < sim.Gd.DayMinutes + 2; m++)
            {
                if (m == 30) sim.Buy("machine:h_quay");
                if (m == 60) sim.Buy("driver");
                if (m == 61) sim.Dispatch(1, sim.SuggestRoute(1, true));
                if (m == 200) sim.EndTutorial();
                sim.AdvanceMinutes(1);
                var r = SaveCodec.Decode(SaveCodec.Encode(sim), sim.Gd);
                if (!r.Ok) bad++;
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
            foreach (var d in rep.Days) { Assert.Greater(d.Collected, 0, "day " + d.Day); week += d.Net; }
            Assert.Greater(week, 0, "a sensible player earns over a week (money also went into research / machines)");
            Assert.Greater(rep.Days[6].Net, rep.Days[0].Net, "the network grows: day 7 earns more than day 1");
            Assert.Less(rep.Days[6].TurnedRate, 0.15, "no wall of turned-away citizens after the forgiving days");
        }

        [Test]
        public void FirstDayFeelsGood()
        {
            var rep = BalanceRunner.Run(TestData.Gd(), 3, 1);
            var d = rep.Days[0];
            Assert.Greater(d.Net, 0, "day 1 earns money");
            Assert.Less(d.TurnedRate, 0.25, "day 1 is not a wall of turned-away citizens");
        }

        [Test]
        public void TwentyDaysReachOldTownAndMore()
        {
            var rep = BalanceRunner.Run(TestData.Gd(), 9, 20);
            TestContext.Progress.WriteLine("BALANCE 20 days seed 9\n" + rep.Table() + string.Join("\n", rep.Log));
            Assert.GreaterOrEqual(rep.Chapter, 2, "Old Town unlocked within 20 days (~80 min of play)");
            Assert.Greater(rep.Points, 3);
        }
    }
}
