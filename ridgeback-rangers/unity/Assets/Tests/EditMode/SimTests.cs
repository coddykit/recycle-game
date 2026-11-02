// Ridgeback Rangers — engine-free sim tests (NUnit). Same files run in the Unity Test Runner (EditMode) and under plain .NET
// (Tools/dotnet_check.sh). Data is read from Assets/Resources/data on disk.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RidgebackRangers.Sim;

namespace RidgebackRangers.Sim.Tests
{
    public static class TestData
    {
        static GameData cached;

        public static string UnityDir()
        {
            string env = Environment.GetEnvironmentVariable("RIDGEBACKRANGERS_UNITY");
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

        /// <summary>A live day with everything quiet (no random pins from hunger: stations full).</summary>
        public static Simulation Live(long seed = 7)
        {
            var s = NewSim(seed);
            s.StartDay();
            return s;
        }

        public static int Ranger(Simulation s, string role) => s.S.Rangers.FindIndex(r => r.Role == role && !r.Busy);
    }

    public class DataTests
    {
        [Test]
        public void DataLoadsWithoutErrors()
        {
            var gd = TestData.Gd();
            Assert.IsTrue(gd.Ok, string.Join("; ", gd.Errors));
            Assert.AreEqual(3, gd.Zones.Count, "MVP: Landing Cove, Fern Valley, Dune Mesa");
            Assert.AreEqual(8, gd.Species.Count, "MVP species");
            Assert.AreEqual(2, gd.Species.Select(s => s.Biome).Distinct().Count(), "MVP: 2 biomes");
            Assert.AreEqual(4, gd.Species.Select(s => s.Family).Distinct().Count(), "4 shared model families");
        }

        [Test]
        public void EveryNodeReachableFromTheHq()
        {
            var gd = TestData.Gd();
            foreach (var n in gd.Nodes) Assert.Less(gd.Graph.Km(gd.HqNode, n.Index), RoadGraph.Inf, n.Id);
            var p = gd.Graph.Path(gd.HqNode, gd.Node("d_p2"));
            Assert.AreEqual(gd.HqNode, p[0]);
            Assert.AreEqual(gd.Node("d_p2"), p[p.Count - 1]);
            Assert.AreEqual(gd.Graph.Km(gd.HqNode, gd.Node("d_p2")), gd.Graph.PathKm(p), 1e-6);
        }

        [Test]
        public void HatchingNeverLongerThanTwentyMinutes_AndDigSitesNameRealSpecies()
        {
            var gd = TestData.Gd();
            foreach (var s in gd.Species) Assert.LessOrEqual(s.HatchMin, 20, s.Id);
            foreach (var d in gd.DigSites) { Assert.GreaterOrEqual(d.Species.Count, 2, d.Id); foreach (var s in d.Species) Assert.IsNotNull(gd.Sp(s), s); }
        }

        [Test]
        public void HarmonyIsSymmetricAndBossySkittishIsTheWorst()
        {
            var gd = TestData.Gd();
            foreach (var a in new[] { "calm", "skittish", "bossy" })
                foreach (var b in new[] { "calm", "skittish", "bossy" })
                    Assert.AreEqual(gd.Harmony(a, b), gd.Harmony(b, a), 1e-9, a + "-" + b);
            Assert.Less(gd.Harmony("bossy", "skittish"), gd.Harmony("calm", "calm"));
            var sim = TestData.NewSim();
            var ken = new AnimalState { Species = "kentrosaurus" };
            var para = new AnimalState { Species = "parasaurolophus" };
            var tri = new AnimalState { Species = "triceratops" };
            Assert.Less(sim.Harmony(ken, para, "forest"), 0.3, "a bossy and a skittish species squabble");
            Assert.Greater(sim.Harmony(tri, tri, "forest"), 0.95, "a herd of its own kind gets on");
            Assert.Less(sim.Harmony(tri, tri, "dune"), sim.Harmony(tri, tri, "forest"), "the wrong biome hurts");
        }
    }

    public class CoreTests
    {
        [Test]
        public void NewCareerStartsInThePlanWithThreeAnimalsTwoPaddocksFiveRangers()
        {
            var sim = TestData.NewSim(1, tutorial: true);
            Assert.AreEqual(Phases.Plan, sim.Phase);
            Assert.AreEqual(2, sim.S.Paddocks.Count);
            Assert.AreEqual(3, sim.S.Animals.Count);
            Assert.AreEqual(5, sim.S.Rangers.Count);
            foreach (var r in Roles.All) Assert.GreaterOrEqual(sim.CountRole(r), 1, r);
            Assert.IsTrue(sim.S.Tutorial);
            Assert.AreEqual("triceratops", sim.S.Animals[0].Species, "the guide's hungry triceratops");
            CollectionAssert.AreEquivalent(new[] { "triceratops", "maiasaura" }, sim.S.Found);
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
        public void VisitorsArrivePayTicketsAndRideTheTram()
        {
            var sim = TestData.Live(3);
            Assert.IsTrue(sim.S.GateOpen, "after the guide the gate opens every morning");
            double cash = sim.S.Cash;
            sim.AdvanceMinutes(120);
            Assert.Greater(sim.S.Today.Visitors, 10);
            Assert.Greater(sim.S.Today.Tickets, 0);
            Assert.Greater(sim.S.Today.Rides, 0, "the tram ran loops by its timetable");
            Assert.Greater(sim.S.Today.Joy, 0);
            Assert.Greater(sim.S.Cash, cash - sim.S.Today.Feed - 1);
        }

        [Test]
        public void ATooSmallTramMakesVisitorsWalk()
        {
            var a = TestData.Live(5);
            var b = TestData.Live(5);
            b.S.Tram.Cars = 4;
            a.SetTramPolicy(TramPolicy.Interval, 30);
            a.S.Rating = b.S.Rating = 90;
            a.AdvanceMinutes(400);
            b.AdvanceMinutes(400);
            Assert.Greater(a.S.Today.Walked, b.S.Today.Walked, "queues outgrow one slow tram car");
        }

        [Test]
        public void VarietyBeatsAMonoHerd()
        {
            var sim = TestData.NewSim();
            foreach (var x in sim.S.Animals) { x.Mood = 0.6; x.Health = Health.Ok; }
            double mixed = sim.ZoneAppeal("cove");
            sim.S.Animals[2].Species = "triceratops";
            double mono = sim.ZoneAppeal("cove");
            Assert.Greater(mixed, mono, "two species beat three of one kind (visitors score variety)");
            Assert.AreEqual(sim.Gd.Variety[0], 0.8, 1e-9);
        }

        [Test]
        public void EscapeAlarmHaltsTheTramUntilAHandlerEscortsTheAnimalBack()
        {
            var sim = TestData.Live(9);
            sim.AdvanceMinutes(30);
            int pin = sim.ForcePin(PinKinds.Escape, 0);
            Assert.Greater(pin, 0);
            Assert.IsTrue(sim.TramHalted);
            double pos = sim.S.Tram.Pos;
            bool run = sim.S.Tram.Running;
            sim.AdvanceMinutes(5);
            Assert.AreEqual(pos, sim.S.Tram.Pos, 1e-9, "no tram movement during an escape alarm");
            Assert.AreEqual(run, sim.S.Tram.Running);
            Assert.AreEqual("", sim.Send(pin, TestData.Ranger(sim, Roles.Handler)));
            for (int i = 0; i < 120 && sim.Pin(pin) != null; i++) sim.AdvanceMinutes(1);
            Assert.IsNull(sim.Pin(pin));
            Assert.IsFalse(sim.TramHalted);
        }
    }

    public class PinTests
    {
        [Test]
        public void EmptyStationAndAHungryHerdOpenAPinAndAFeederFixesIt()
        {
            var sim = TestData.Live(2);
            int pin = sim.ForcePin(PinKinds.Hungry, 0);
            var p = sim.Pin(pin);
            Assert.AreEqual(PinKinds.Hungry, p.Kind);
            Assert.AreEqual("role", sim.Send(pin, TestData.Ranger(sim, Roles.Vet)), "only a Feeder takes a hungry herd");
            int f = TestData.Ranger(sim, Roles.Feeder);
            double eta = sim.EtaMin(f, pin);
            Assert.Greater(eta, 1);
            Assert.AreEqual("", sim.Send(pin, f));
            Assert.AreEqual(sim.Gd.BarnNode, sim.S.Rangers[f].Path[sim.S.Rangers[f].Path.Count - 1], "the Feeder fetches crates at the barn first");
            int guard = 0;
            while (sim.Pin(pin) != null && guard++ < 200) sim.AdvanceMinutes(1);
            Assert.IsNull(sim.Pin(pin));
            Assert.Greater(sim.S.Stations["cove"].Crates, 0);
            Assert.Less(sim.S.Animals[0].Hunger, sim.Gd.HungryPinAt);
            Assert.AreEqual(1, sim.S.Today.PinsResolved);
        }

        [Test]
        public void AnIgnoredPinFailsAtItsDeadlineAndCostsCoinsNotProgress()
        {
            var sim = TestData.Live(4);
            int pin = sim.ForcePin(PinKinds.Sick, 0);
            var p = sim.Pin(pin);
            int animal = p.Animal;
            double cash = sim.S.Cash;
            sim.AdvanceMinutes(p.Deadline - sim.S.Minute + 1);
            Assert.IsNull(sim.Pin(pin));
            Assert.AreEqual(1, sim.S.Today.PinsFailed);
            Assert.Contains(PinKinds.Sick, sim.S.Today.Failed);
            Assert.AreEqual(Health.Off, sim.S.Animals[animal].Health, "off exhibit for the day — never hurt");
            Assert.Less(sim.S.Cash, cash + sim.S.Today.Income);
            Assert.AreEqual(3, sim.S.Animals.Count, "no animal is ever lost");
            sim.AdvanceMinutes(sim.Gd.DayMinutes);
            sim.NextDay();
            Assert.AreEqual(Health.Ok, sim.S.Animals[animal].Health, "back on exhibit the next morning");
        }

        [Test]
        public void APinWithARangerOnTheWayNeverFails()
        {
            var sim = TestData.Live(6);
            int pin = sim.ForcePin(PinKinds.Fence, 1);
            Assert.AreEqual("", sim.Send(pin, TestData.Ranger(sim, Roles.Tech)));
            sim.Pin(pin).Deadline = sim.S.Minute + 1;
            sim.AdvanceMinutes(5);
            Assert.AreEqual(0, sim.S.Today.PinsFailed);
        }

        [Test]
        public void PinsAreDeduplicatedPerPaddockAndKind()
        {
            var sim = TestData.Live(8);
            Assert.Greater(sim.OpenPin(PinKinds.Squabble, 1, -1), 0);
            Assert.AreEqual(-1, sim.OpenPin(PinKinds.Squabble, 1, -1));
        }

        [Test]
        public void LowHarmonyPaddocksSquabbleMore()
        {
            int Count(string a, string b)
            {
                var sim = TestData.Live(12);
                sim.S.Animals[0].Species = a;
                sim.S.Animals[1].Species = b;
                sim.S.Paddocks[0].Fence = 3;
                int n = 0;
                for (int m = 0; m < 500; m++)
                {
                    sim.AdvanceMinutes(1);
                    foreach (var p in sim.S.Pins.ToList()) if (p.Kind == PinKinds.Squabble && p.Paddock == 0) { n++; sim.S.Pins.Remove(p); }
                    foreach (var x in sim.S.Animals) { x.Hunger = 0.1; x.Health = Health.Ok; }
                    sim.S.Paddocks[0].Integrity = 1;
                    if (sim.Phase != Phases.Live) break;
                }
                return n;
            }
            Assert.Greater(Count("kentrosaurus", "parasaurolophus"), Count("triceratops", "triceratops"));
        }

        [Test]
        public void ChiefRangerAnswersPinsLateButByItself()
        {
            var sim = TestData.Live(13);
            sim.S.Rating = 80;
            sim.S.Cash = 50000;
            Assert.AreEqual("", sim.Buy("chief"));
            int pin = sim.ForcePin(PinKinds.Fence, 0);
            sim.AdvanceMinutes(2);
            Assert.AreEqual(-1, sim.Pin(pin).Ranger, "the Chief waits a few minutes");
            sim.AdvanceMinutes(sim.Gd.ChiefDelayMin + 1);
            Assert.GreaterOrEqual(sim.Pin(pin)?.Ranger ?? 1, 0, "the Chief sent someone");
        }

        [Test]
        public void NoPinFailsDuringTheGuide()
        {
            var sim = TestData.NewSim(14, tutorial: true);
            sim.StartDay();
            int pin = sim.ForcePin(PinKinds.Hungry, 0);
            sim.AdvanceMinutes(300);
            Assert.IsNotNull(sim.Pin(pin));
            Assert.AreEqual(0, sim.S.Today.PinsFailed);
            Assert.IsFalse(sim.S.GateOpen, "the guide opens the gate by hand");
        }
    }

    public class HatcheryTests
    {
        [Test]
        public void DigTripsBringShardsAndAFullSetBecomesAnEgg()
        {
            var sim = TestData.Live(15);
            sim.S.Cp = 500;
            string target = sim.DigTarget(0);
            Assert.AreEqual("parasaurolophus", target, "the chart is deterministic: the first species not in the log yet");
            int trips = 0;
            while (sim.S.Eggs.Count == 0 && trips < 6)
            {
                Assert.AreEqual("", sim.Dig(0), "trip " + trips);
                for (int m = 0; m < 400 && sim.S.Rangers.Exists(r => r.Dig >= 0); m++) sim.AdvanceMinutes(1);
                trips++;
                if (sim.Phase != Phases.Live) { sim.NextDay(); sim.StartDay(); }
            }
            Assert.AreEqual(2, trips, "50 % per trip");
            Assert.AreEqual(target, sim.S.Eggs[0].Species);
            Assert.Contains(target, sim.S.Found);
        }

        [Test]
        public void EggsWarmOnRealTimeTwiceAsFastWhenTheTemperatureIsChecked()
        {
            var a = TestData.NewSim(16);
            var b = TestData.NewSim(16);
            a.PlaceEgg("maiasaura");
            b.PlaceEgg("maiasaura");
            Assert.AreEqual("too_cold", b.CheckTemp(0, 5));
            Assert.AreEqual("", b.CheckTemp(0, 50));
            a.Advance(1.0); b.Advance(1.0);
            Assert.Greater(a.S.Eggs[0].LeftS, b.S.Eggs[0].LeftS);
            for (int i = 0; i < 3000 && b.S.Eggs.Count > 0; i++) { b.Advance(1.0); if (b.S.Eggs.Count > 0 && !b.S.Eggs[0].Checked) b.CheckTemp(0, 50); }
            Assert.AreEqual(0, b.S.Eggs.Count);
            var baby = b.S.Animals[b.S.Animals.Count - 1];
            Assert.IsTrue(baby.Young);
            Assert.AreEqual("maiasaura", baby.Species);
            Assert.AreEqual(1, b.S.Hatched);
        }

        [Test]
        public void HatchlingsGoToAPaddockWithRoomOrTheNursery()
        {
            var sim = TestData.NewSim(17);
            Assert.AreEqual(1, sim.SuggestPaddock("maiasaura"), "joins its own kind");
            for (int i = 0; i < 6; i++) sim.AddAnimal("camarasaurus", 0, false);
            for (int i = 0; i < 6; i++) sim.AddAnimal("camarasaurus", 1, false);
            Assert.AreEqual(-1, sim.SuggestPaddock("camarasaurus"), "no room anywhere -> the nursery");
        }

        [Test]
        public void YoungAnimalsGrowUpAfterTwoDays()
        {
            var sim = TestData.NewSim(18);
            var a = sim.AddAnimal("maiasaura", 1, true);
            for (int d = 0; d < 2; d++) { sim.StartDay(); sim.AdvanceMinutes(sim.Gd.DayMinutes + 1); sim.NextDay(); }
            Assert.IsFalse(a.Young);
        }
    }

    public class EconomyTests
    {
        [Test]
        public void DayEndsWithAReportStarsAndCosts()
        {
            var sim = TestData.Live(19);
            sim.AdvanceMinutes(sim.Gd.DayMinutes + 1);
            Assert.AreEqual(Phases.Report, sim.Phase);
            var d = sim.S.History.Last();
            Assert.Greater(d.Wages, 0);
            Assert.Greater(d.Upkeep, 0);
            Assert.Greater(d.CpEarned, 0, "Care Score earns Conservation Points");
            Assert.That(d.Stars, Is.InRange(0, 9));
            Assert.IsTrue(sim.NextDay());
            Assert.AreEqual(2, sim.S.Day);
            Assert.AreEqual(Phases.Plan, sim.Phase);
        }

        [Test]
        public void FeedBoardDeliversInTheMorningAndCostsCrates()
        {
            var sim = TestData.NewSim(20);
            sim.SetFeedPlan("cove", 9);
            sim.StartDay();
            Assert.AreEqual(9, sim.S.Stations["cove"].Crates, 1e-9);
            Assert.AreEqual(9 * sim.Gd.CrateCost, sim.S.Today.Feed, 1e-9);
            Assert.Greater(sim.FeedNeed("cove"), 2);
        }

        [Test]
        public void FirstDaysAndTheGuideNeverLowerTheRating()
        {
            var sim = TestData.Live(21);
            double r = sim.S.Rating;
            foreach (var p in sim.S.Pins.ToList()) sim.S.Pins.Remove(p);
            sim.S.Animals.ForEach(a => { a.Hunger = 1; a.Mood = 0; });
            sim.AdvanceMinutes(sim.Gd.DayMinutes + 1);
            Assert.GreaterOrEqual(sim.S.Rating, r);
        }

        [Test]
        public void NoIslandEventsDuringTheGuideOrTheFirstDays()
        {
            var sim = TestData.NewSim(22);
            for (int d = 0; d < sim.Gd.EventFreeDays; d++)
            {
                sim.StartDay(); sim.AdvanceMinutes(sim.Gd.DayMinutes + 1);
                Assert.IsFalse(sim.S.Events.Any(), "day " + sim.S.Day);
                sim.NextDay();
                Assert.IsFalse(sim.S.Planned.Any(p => !p.Call), "day " + sim.S.Day);
            }
        }

        [Test]
        public void PurchasesCheckCashRatingAndZones()
        {
            var sim = TestData.NewSim(23);
            sim.S.Rating = 30;
            Assert.AreEqual("locked", sim.CanBuy("paddock:f_p1").Reason);
            Assert.AreEqual("rating", sim.CanBuy("zone:fern").Reason);
            Assert.AreEqual("locked", sim.CanBuy("zone:dune").Reason, "chapters open in order");
            sim.S.Rating = 60;
            Assert.AreEqual("cash", sim.CanBuy("zone:fern").Reason);
            sim.S.Cash = 100000;
            Assert.AreEqual("", sim.Buy("zone:fern"));
            Assert.IsTrue(sim.S.Stations.ContainsKey("fern"));
            Assert.AreEqual("", sim.Buy("paddock:f_p1"));
            Assert.AreEqual("owned", sim.CanBuy("paddock:f_p1").Reason);
            Assert.AreEqual("", sim.Buy("fence:0"));
            Assert.AreEqual(2, sim.S.Paddocks[0].Fence);
            for (int i = sim.CountRole(Roles.Vet); i < sim.RangerCap; i++) Assert.AreEqual("", sim.Buy("hire:vet"));
            Assert.AreEqual("max", sim.CanBuy("hire:vet").Reason);
            Assert.AreEqual(2, sim.Chapter, "Fern Valley = chapter 2");
        }

        [Test]
        public void OfflinePayIsCappedAndScalesWithNightShift()
        {
            var sim = TestData.NewSim(24);
            sim.S.History.Add(new DayStats { Tickets = 3000 });
            sim.Touch(1_000_000);
            var o = sim.Offline(1_000_000 + 3600 * 20);
            Assert.IsTrue(o.Capped);
            Assert.AreEqual(sim.Gd.OfflineCapH, o.Hours, 1e-9);
            var sim2 = TestData.NewSim(24);
            sim2.S.History.Add(new DayStats { Tickets = 3000 });
            sim2.SetPerks(new Dictionary<string, bool> { { "offline_plus", true } });
            sim2.Touch(1_000_000);
            var o2 = sim2.Offline(1_000_000 + 3600 * 20);
            Assert.Greater(o2.Amount, o.Amount);
        }

        [Test]
        public void GrantCashUsesTheUsualDayIncome()
        {
            var sim = TestData.NewSim(25);
            Assert.AreEqual(sim.Gd.DayIncomeFloor, sim.DayIncome(), 1e-9);
            double c = sim.S.Cash;
            sim.GrantCash(2.5);
            Assert.AreEqual(c + 2.5 * sim.Gd.DayIncomeFloor, sim.S.Cash, 1e-6);
        }
    }

    public class RewardTests
    {
        const long Now = 1_800_000_000;

        [Test]
        public void RewardsAreNeverOfferedDuringTheGuide()
        {
            var sim = TestData.NewSim(26, tutorial: true);
            foreach (var p in Simulation.RewardPlacements) Assert.IsFalse(sim.RewardOffer(p, Now).Ok, p);
        }

        [Test]
        public void DailyCapAndCooldownPersistInTheCareer()
        {
            var sim = TestData.Live(27);
            for (int i = 0; i < 3; i++) sim.ForcePin(PinKinds.Fence, i % 2);
            sim.ForcePin(PinKinds.Squabble, 1);
            sim.ForcePin(PinKinds.Sick, 0);
            long t = Now;
            Assert.IsTrue(sim.GrantReward("borrow_ranger", t).Ok);
            Assert.AreEqual("cooldown", sim.RewardOffer("borrow_ranger", t + 10).Reason);
            Assert.IsTrue(sim.GrantReward("borrow_ranger", t + 700).Ok);
            Assert.IsTrue(sim.GrantReward("borrow_ranger", t + 1400).Ok);
            Assert.AreEqual("cap", sim.RewardOffer("borrow_ranger", t + 2100).Reason);
            var back = SaveCodec.Decode(SaveCodec.Encode(sim), sim.Gd).Sim;
            Assert.AreEqual("cap", back.RewardOffer("borrow_ranger", t + 2100).Reason, "a relaunch does not reset the cap");
        }

        [Test]
        public void CashRewardsStayUnderTheirShareOfTheCheapestPack()
        {
            var sim = TestData.NewSim(28);
            sim.S.OfflinePending = 1e6;
            var o = sim.RewardOffer("dawn_double", Now);
            Assert.IsTrue(o.Ok);
            Assert.LessOrEqual(o.Amount, sim.Gd.Rewards["dawn_double"].Days * sim.DayIncome() + 0.5);
            double daily = 0;
            foreach (var r in sim.Gd.Rewards.Values) { Assert.LessOrEqual(r.Days, sim.Gd.RewardSingleMaxDays + 1e-9, r.Id); daily += r.Days * r.PerDay; }
            Assert.LessOrEqual(daily, 0.5, "all rewards of a UTC day ≤ 1/2 of the cheapest pack (1 day)");
        }

        [Test]
        public void BorrowedRangerTakesThePinAndLeaves()
        {
            var sim = TestData.Live(29);
            int pin = sim.ForcePin(PinKinds.Sick, 0);
            int before = sim.S.Rangers.Count;
            Assert.IsTrue(sim.GrantReward("borrow_ranger", Now, pin).Ok);
            Assert.AreEqual(before + 1, sim.S.Rangers.Count);
            for (int i = 0; i < 60 && sim.Pin(pin) != null; i++) sim.AdvanceMinutes(1);
            Assert.IsNull(sim.Pin(pin));
            Assert.AreEqual(before, sim.S.Rangers.Count, "the borrowed ranger went home");
        }

        [Test]
        public void StormShelterOnlyWithFenceTroubleAndRepairsEverything()
        {
            var sim = TestData.Live(30);
            Assert.AreEqual("context", sim.RewardOffer("storm_shelter", Now).Reason);
            sim.ForcePin(PinKinds.Fence, 0);
            sim.ForcePin(PinKinds.Escape, 1);
            Assert.IsTrue(sim.GrantReward("storm_shelter", Now).Ok);
            Assert.IsFalse(sim.S.Pins.Any(p => p.Kind == PinKinds.Fence || p.Kind == PinKinds.Escape));
            Assert.IsTrue(sim.S.Paddocks.All(p => p.Integrity >= 1));
        }

        [Test]
        public void ScoutTipRevealsAHiddenSpeciesAndWarmCutsTenMinutes()
        {
            var sim = TestData.NewSim(31);
            Assert.IsTrue(sim.DigRevealed(0, 2), "Landing Cove: its first two species are already in the log");
            Assert.AreEqual("context", sim.RewardOffer("scout_tip", Now).Reason, "Fern Valley is still closed");
            sim.S.Zones.Add("fern");
            Assert.IsFalse(sim.DigRevealed(1, 2));
            Assert.IsTrue(sim.GrantReward("scout_tip", Now).Ok);
            Assert.IsTrue(sim.DigRevealed(1, 2));
            Assert.AreEqual("context", sim.RewardOffer("warm_incubator", Now + 9999).Reason);
            sim.PlaceEgg("triceratops");
            double left = sim.S.Eggs[0].LeftS;
            Assert.IsTrue(sim.GrantReward("warm_incubator", Now + 9999).Ok);
            Assert.AreEqual(Math.Max(1, left - sim.Gd.WarmS), sim.S.Eggs[0].LeftS, 1e-9);
        }
    }

    public class SaveTests
    {
        [Test]
        public void RoundTripMidDayIsIdenticalAndKeepsPlaying()
        {
            var sim = TestData.Live(32);
            sim.AdvanceMinutes(37);
            int pin = sim.ForcePin(PinKinds.Hungry, 0);
            sim.Send(pin, TestData.Ranger(sim, Roles.Feeder));
            sim.ForcePin(PinKinds.Escape, 1);
            sim.PlaceEgg("maiasaura");
            sim.AdvanceMinutes(3);
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
            Assert.IsFalse(SaveCodec.Decode(ok.Replace("\"sp\":\"maiasaura\"", "\"sp\":\"unicorn\""), gd).Ok);
            Assert.IsFalse(SaveCodec.Decode(ok.Replace("\"role\":\"vet\"", "\"role\":\"pilot\""), gd).Ok);
            Assert.IsFalse(SaveCodec.Decode(ok.Replace("\"phase\":\"plan\"", "\"phase\":\"party\""), gd).Ok);
        }

        [Test]
        public void DecoderAcceptsNegativeCashTheNurseryAndTheReportScreen()
        {
            var sim = TestData.Live(33);
            sim.S.Cash = -1234.5;
            sim.MoveAnimal(2, -1);
            sim.AdvanceMinutes(sim.Gd.DayMinutes + 1);
            Assert.AreEqual(Phases.Report, sim.Phase);
            var r = SaveCodec.Decode(SaveCodec.Encode(sim), sim.Gd);
            Assert.IsTrue(r.Ok, r.Error);
            Assert.IsTrue(r.Sim.NextDay());
        }

        [Test]
        public void FuzzEveryMinuteOfADayDecodes()
        {
            // fleet §14 save fuzz: guide phase, purchase moment, rangers mid-road, tram mid-loop, an egg, the report — 0 unreadable saves
            var sim = TestData.NewSim(34, tutorial: true);
            sim.S.Cash = 1e5;
            sim.S.Cp = 200;
            sim.StartDay();
            int bad = 0;
            for (int m = 0; m < sim.Gd.DayMinutes + 2; m++)
            {
                if (m == 5) sim.Send(sim.ForcePin(PinKinds.Hungry, 0), TestData.Ranger(sim, Roles.Feeder));
                if (m == 10) sim.OpenGate();
                if (m == 30) sim.Buy("tram_car");
                if (m == 60) sim.EndTutorial();
                if (m == 61) sim.Dig(0);
                if (m == 90) sim.PlaceEgg("triceratops");
                if (m == 120) sim.ForcePin(PinKinds.Escape, 1);
                if (m == 200) sim.Fire("rain_squall");
                if (m % 40 == 0) foreach (var p in sim.S.Pins.ToList()) { int r = sim.FreeRanger(sim.RoleFor(p.Kind), p.Node); if (r >= 0 && p.Ranger < 0) sim.Send(p.Id, r); }
                sim.AdvanceMinutes(1);
                var res = SaveCodec.Decode(SaveCodec.Encode(sim), sim.Gd);
                if (!res.Ok) bad++;
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
            TestContext.Progress.WriteLine("BALANCE 7 days seed 42\n" + rep.Table() + string.Join("\n", rep.Log));
            Assert.IsTrue(rep.Finite);
            Assert.AreEqual(7, rep.Days.Count);
            double week = 0;
            foreach (var d in rep.Days) { Assert.Greater(d.Rides, 0, "day " + d.Day); week += d.Net; }
            Assert.Greater(week, 0, "a sensible player earns over a week (money also went into paddocks / zones)");
            Assert.Greater(rep.Days[6].Income, rep.Days[0].Income, "the sanctuary grows: day 7 takes more than day 1");
            Assert.Greater(rep.Days.Sum(d => d.PinsOpened), 7, "there is something to do every day");
            Assert.Greater(rep.Animals, 3, "the hatchery grew the herd");
        }

        [Test]
        public void FirstDayFeelsGood()
        {
            var rep = BalanceRunner.Run(TestData.Gd(), 3, 1);
            var d = rep.Days[0];
            Assert.Greater(d.Net, 0, "day 1 earns money");
            Assert.Less((double)d.Walked / Math.Max(1, d.Visitors), 0.25, "day 1 is not a wall of walking visitors");
            Assert.GreaterOrEqual(d.Stars, 5);
        }

        [Test]
        public void TwentyDaysReachFernValleyAndMore()
        {
            var rep = BalanceRunner.Run(TestData.Gd(), 9, 20);
            TestContext.Progress.WriteLine("BALANCE 20 days seed 9\n" + rep.Table() + string.Join("\n", rep.Log));
            Assert.GreaterOrEqual(rep.Chapter, 2, "Fern Valley open within 20 days (~90 min of play)");
            Assert.GreaterOrEqual(rep.Species, 4);
        }
    }
}
