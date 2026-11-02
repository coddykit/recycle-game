// Pour & Roast — engine-free sim tests (NUnit). Same files run in the Unity Test Runner (EditMode) and under plain .NET
// (Tools/dotnet_check.sh). Data is read from Assets/Resources/data on disk.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PourAndRoast.Sim;

namespace PourAndRoast.Sim.Tests
{
    public static class TestData
    {
        static GameData cached;

        public static string UnityDir()
        {
            string env = Environment.GetEnvironmentVariable("POURANDROAST_UNITY");
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

        /// <summary>A sim with the Roastery Hall owned (and plenty of cash), working the roastery venue.</summary>
        public static Simulation RoasterySim(long seed = 7)
        {
            var s = NewSim(seed);
            s.S.Cash = 100000;
            s.S.Rating = 4.5;
            Assert.AreEqual("", s.Buy("venue:corner"));
            Assert.AreEqual("", s.Buy("venue:roastery"));
            return s;
        }

        /// <summary>Live day with no random customers (tutorial mode keeps the sim quiet) for hand-made tickets.</summary>
        public static Simulation QuietLive(long seed = 7)
        {
            var s = NewSim(seed, tutorial: true);
            s.StartDay();
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
            Assert.AreEqual(3, gd.Venues.Count, "MVP: cart, corner cafe, roastery hall");
            Assert.GreaterOrEqual(gd.Research.Count, 20);
        }

        [Test]
        public void MvpContentCountsMatchThePlan()
        {
            var gd = TestData.Gd();
            Assert.AreEqual(10, gd.Menu.Count(m => !m.IsPastry), "10 drinks");
            Assert.AreEqual(5, gd.Menu.Count(m => m.IsPastry), "5 pastries");
            Assert.AreEqual(4, gd.Origins.Count, "4 green origins");
            Assert.AreEqual(2, gd.Contracts.Count, "2 wholesale contracts");
            Assert.AreEqual(4, gd.Roles.Count, "barista, cashier, runner, roaster assistant");
            Assert.AreEqual(5, gd.Customers.Count, "commuter, tourist, regular, student, influencer");
            Assert.AreEqual(6, gd.Events.Count, "6 MVP crises");
            Assert.AreEqual(6, gd.Rewards.Count, "6 rewarded placements");
        }

        [Test]
        public void ResearchNeverLongerThanTwentyMinutes()
        {
            foreach (var r in TestData.Gd().Research) Assert.LessOrEqual(r.Minutes, 20, r.Id);
        }

        [Test]
        public void EveryVenueServesItsTierAndTheCartHasNoIcedDrinks()
        {
            var gd = TestData.Gd();
            foreach (var m in gd.Menu.Where(m => m.Iced)) Assert.GreaterOrEqual(m.Tier, 2, m.Id + " needs an ice maker");
            Assert.IsTrue(gd.StartMenu.All(id => gd.Item(id).Tier == 1));
        }
    }

    public class BarTests
    {
        [Test]
        public void NewCareerStartsAtTheCartInThePlanWithOneBarista()
        {
            var sim = TestData.NewSim(1, tutorial: true);
            Assert.AreEqual(Phases.Plan, sim.Phase);
            Assert.AreEqual("cart", sim.S.Venue);
            Assert.AreEqual(1, sim.S.Staff.Count);
            Assert.IsTrue(sim.S.Tutorial);
            Assert.AreEqual(Weathers.Rain, sim.S.Forecast[1], "the guide's 'rain tomorrow'");
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
        public void NoRandomCustomersInTheGuide_CustomersAfter()
        {
            var sim = TestData.QuietLive();
            sim.AdvanceMinutes(60);
            Assert.AreEqual(0, sim.S.Today.Customers);
            var s2 = TestData.NewSim();
            s2.StartDay();
            s2.AdvanceMinutes(60);
            Assert.Greater(s2.S.Today.Customers, 5);
        }

        [Test]
        public void EspressoIsMadeShelvedAndHandedOverForMoney()
        {
            var sim = TestData.QuietLive();
            var t = sim.TutorialOrder("espresso");
            double cash = sim.S.Cash;
            sim.HoldShelf = true;
            sim.AdvanceMinutes(5);
            Assert.IsTrue(t.Ready, "one shot is done in a few minutes");
            Assert.AreEqual(1, sim.ReadyCount, "held on the shelf");
            Assert.AreEqual(1, sim.HandoverAll());
            Assert.Greater(sim.S.Cash, cash);
            Assert.AreEqual(1, sim.S.Today.Served);
            Assert.AreEqual(1, sim.S.Reviews.Count, "a review card on the wall");
        }

        [Test]
        public void ShelfHandsOverByItselfWhenNotHeld()
        {
            var sim = TestData.QuietLive();
            sim.TutorialOrder("espresso");
            sim.AdvanceMinutes(8);
            Assert.AreEqual(0, sim.S.Tickets.Count);
            Assert.AreEqual(1, sim.S.Today.Served);
        }

        [Test]
        public void LatteNeedsMilkAndTheSteamWand()
        {
            var sim = TestData.QuietLive();
            double milk = sim.S.Milk[0];
            var t = sim.TutorialOrder("latte");
            sim.HoldShelf = true;
            sim.AdvanceMinutes(1);
            Assert.AreEqual(Tasks.Shot, sim.NextTask(t.Items[0]));
            sim.AdvanceMinutes(6);
            Assert.IsTrue(t.Ready);
            Assert.AreEqual(milk - sim.Gd.MilkPerDrink, sim.S.Milk[0], 1e-9);
        }

        [Test]
        public void WithoutMilkALatteFails_OatSubstitutesForWhole()
        {
            var sim = TestData.QuietLive();
            sim.S.Milk[0] = 0;
            var t = sim.TutorialOrder("latte");
            sim.HoldShelf = true;
            sim.AdvanceMinutes(6);
            Assert.IsTrue(t.Items[0].Sub, "oat stood in for whole milk");
            sim.S.Milk[0] = sim.S.Milk[1] = 0;
            var t2 = sim.TutorialOrder("latte");
            sim.AdvanceMinutes(6);
            Assert.IsTrue(t2.Items[0].Failed);
        }

        [Test]
        public void BatchingMakesTwoSameDrinksFaster()
        {
            double Time(bool batch)
            {
                var sim = TestData.QuietLive();
                sim.SetBatch(batch);
                sim.HoldShelf = true;
                var a = sim.TutorialOrder("espresso");
                var b = sim.TutorialOrder("espresso");
                double m0 = sim.S.Minute;
                for (int i = 0; i < 200 && !(a.Ready && b.Ready); i++) sim.AdvanceMinutes(0.1);
                return sim.S.Minute - m0;
            }
            Assert.Less(Time(true), Time(false) - 0.2);
        }

        [Test]
        public void FlaggedTicketLeads_DeadlineRuleOrdersByDueTime()
        {
            var sim = TestData.QuietLive();
            var a = sim.TutorialOrder("espresso");
            var b = sim.TutorialOrder("espresso");
            sim.Flag(b.Id);
            Assert.AreEqual(b.Id, sim.WorkOrder()[0].Id);
            sim.Flag(b.Id);
            var p = sim.TutorialOrder("preorder");
            p.Due = sim.S.Minute + 1;
            sim.SetRule(Rules.Deadline);
            Assert.AreEqual(p.Id, sim.WorkOrder()[0].Id);
            sim.SetRule(Rules.Fifo);
            Assert.AreEqual(a.Id, sim.WorkOrder()[0].Id);
        }

        [Test]
        public void BrewStartsTheTicketAtOnce()
        {
            var sim = TestData.QuietLive();
            sim.SetPaused(true);
            var t = sim.TutorialOrder("espresso");
            Assert.AreEqual("", sim.Brew(t.Id));
            Assert.AreNotEqual(-1, t.Items[0].Worker, "the barista took it even with time paused");
        }

        [Test]
        public void WalkInsLeaveWhenTheirTicketIsNotReadyInTime_NoTicketStaysStuck()
        {
            var sim = TestData.NewSim(3);
            sim.StartDay();
            sim.S.Staff.Clear();   // nobody behind the bar
            sim.AdvanceMinutes(90);
            Assert.Greater(sim.S.Today.Walked, 0);
            foreach (var t in sim.S.Tickets) Assert.Less(sim.S.Minute - t.Arrived, sim.Gd.Customers[t.Cust].Patience + 1 + (t.Pre ? 999 : 0));
        }

        [Test]
        public void FullRailTurnsWalkInsAway()
        {
            var sim = TestData.NewSim(4);
            sim.S.Cash = 10000; sim.S.Rating = 4.5;
            sim.Buy("venue:corner");
            sim.StartDay();
            sim.S.Staff.Clear();
            sim.AdvanceMinutes(40);
            Assert.Greater(sim.S.Today.Balked, 0);
            Assert.LessOrEqual(sim.S.Tickets.Count(t => !t.Pre && !t.Ready), sim.RailCap);
        }

        [Test]
        public void PrepHotServesSimpleCoffeeAtOnce()
        {
            var sim = TestData.QuietLive();
            Assert.AreEqual("", sim.Prep("hot"));
            var t = sim.TutorialOrder("americano");
            Assert.IsTrue(sim.ItemDone(t.Items[0]));
            Assert.AreEqual(sim.Gd.PrepHotCups - 1, sim.S.PrepHot);
        }

        [Test]
        public void DialInDriftsMakesBitterShots_DialInRestores()
        {
            var sim = TestData.QuietLive();
            sim.Venue.Tune = 20;
            var t = sim.TutorialOrder("espresso");
            sim.HoldShelf = true;
            sim.AdvanceMinutes(4);
            Assert.Less(t.Items[0].Q, sim.S.BeanQ - 10, "off-tune shots are bitter");
            Assert.AreEqual("", sim.DialIn());
            Assert.AreEqual(100, sim.Venue.Tune, 1e-9);
            sim.AdvanceMinutes(60);
            Assert.Less(sim.Venue.Tune, 100, "the grind drifts again");
        }

        [Test]
        public void JammedGrinderStopsShotsUntilRepaired()
        {
            var sim = TestData.QuietLive();
            Assert.IsTrue(sim.Jam("test"));
            var t = sim.TutorialOrder("espresso");
            sim.AdvanceMinutes(5);
            Assert.AreEqual(Tasks.Shot, sim.NextTask(t.Items[0]));
            Assert.AreEqual(-1, t.Items[0].Worker);
            Assert.AreEqual("", sim.Repair());
            sim.AdvanceMinutes(3);
            Assert.AreNotEqual(Tasks.Shot, sim.NextTask(t.Items[0]));
        }

        [Test]
        public void JamClearsByItselfAfterAWhile()
        {
            var sim = TestData.QuietLive();
            sim.Jam("test");
            sim.AdvanceMinutes(sim.Gd.JamAutoFixMin + 1);
            Assert.IsFalse(sim.Jammed);
        }

        [Test]
        public void ASecondBaristaTakesTheSteamWandAndHeadsCapShots()
        {
            var sim = TestData.QuietLive();
            sim.S.Cash = 5000;
            Assert.AreEqual("", sim.Buy("staff:barista"));
            var b = sim.S.Staff.Where(s => s.Role == Roles.Barista).ToList();
            Assert.AreEqual(2, b.Count);
            Assert.AreEqual(Stations.Espresso, b[0].Station);
            Assert.AreEqual(Stations.Milk, b[1].Station);
            Assert.AreEqual("", sim.Assign(b[1].Id, Stations.Espresso));
            sim.TutorialOrder("espresso"); sim.TutorialOrder("americano");
            sim.HoldShelf = true;
            sim.AdvanceMinutes(0.5);
            Assert.LessOrEqual(sim.ShotsBusy, sim.Venue.Heads, "one group head at the cart");
        }
    }

    public class RoasteryTests
    {
        [Test]
        public void RoasteryIsLockedUntilTheHallIsOwned()
        {
            var sim = TestData.NewSim();
            sim.S.Cash = 10000;
            Assert.AreEqual("locked", sim.BuyGreen("lowland"));
            Assert.AreEqual("locked", sim.CanRoast("lowland", Profiles.Medium));
        }

        [Test]
        public void SweetSpotDropBeatsLettingTheDrumRunOut()
        {
            var sim = TestData.RoasterySim();
            Assert.AreEqual("", sim.BuyGreen("lowland"));
            var b = new BatchState { Origin = "lowland", Profile = Profiles.Medium };
            double sweet = sim.RoastQuality(b, 0.78, "tap"), auto = sim.RoastQuality(b, 1.0, "auto"), early = sim.RoastQuality(b, 0.4, "tap");
            Assert.Greater(sweet, auto);
            Assert.Greater(auto, early);
            Assert.AreEqual("", sim.StartRoast("lowland", Profiles.Medium));
            Assert.AreEqual("busy", sim.CanRoast("lowland", Profiles.Medium));
            sim.AdvanceResearch(sim.Gd.RoastRealS * 0.78);
            Assert.IsTrue(sim.InSweetSpot);
            Assert.AreEqual("", sim.Drop());
            Assert.AreEqual(1, sim.S.Resting.Count);
            Assert.AreEqual(sweet, sim.S.Resting[0].Q, 0.1);
        }

        [Test]
        public void LightAndDarkProfilesNeedTheResearch()
        {
            var sim = TestData.RoasterySim();
            sim.BuyGreen("valley");
            Assert.AreEqual("research", sim.CanRoast("valley", Profiles.Dark));
            sim.S.Research.Add("roast_profiles");
            Assert.AreEqual("", sim.CanRoast("valley", Profiles.Dark));
        }

        [Test]
        public void BeansRestThenBlendIntoTheHouseBeans()
        {
            var sim = TestData.RoasterySim();
            sim.BuyGreen("lowland");
            sim.StartRoast("lowland", Profiles.Medium);
            sim.AdvanceResearch(sim.Gd.RoastRealS * 0.8);
            sim.Drop();
            double kg = sim.S.BeanKg, q = sim.S.BeanQ;
            Assert.AreEqual(0, sim.Blend(), 1e-9, "resting a day first");
            sim.StartDay(); sim.AdvanceMinutes(sim.Gd.DayMinutes + 1); sim.NextDay();
            double blended = sim.Blend();
            Assert.Greater(blended, 0);
            Assert.Greater(sim.S.BeanQ, q, "own roast beats the supplier beans");
        }

        [Test]
        public void TheAssistantDropsByHimself()
        {
            var sim = TestData.RoasterySim();
            sim.S.Research.Add("roast_assistant");
            Assert.AreEqual("", sim.Buy("staff:roaster"));
            sim.BuyGreen("lowland");
            sim.StartRoast("lowland", Profiles.Medium);
            sim.AdvanceResearch(sim.Gd.RoastRealS);
            Assert.IsNull(sim.S.Roast);
            Assert.AreEqual(1, sim.S.Resting.Count);
        }

        [Test]
        public void ContractsPayOnDeliveryAndCostAFeeWhenShort()
        {
            var sim = TestData.RoasterySim();
            Assert.AreEqual("", sim.ToggleContract("bistro"));
            var c = sim.ContractOf("bistro");
            sim.S.BeanKg = 50; sim.S.BeanQ = 80;
            int every = sim.Gd.Contract("bistro").Every;
            for (int d = 0; d <= every; d++) { if (d > 0) sim.NextDay(); sim.StartDay(); sim.AdvanceMinutes(sim.Gd.DayMinutes + 1); }
            Assert.AreEqual(1, c.Delivered);
            Assert.Greater(sim.S.History.Last().Wholesale, 0);
            sim.S.BeanKg = 0;
            sim.S.AutoBeans = false;
            for (int d = 0; d < every; d++) { sim.NextDay(); sim.StartDay(); sim.S.BeanKg = 0; sim.AdvanceMinutes(sim.Gd.DayMinutes + 1); }
            Assert.AreEqual(1, c.Missed);
            Assert.Less(c.PayMult, 1);
        }
    }

    public class EconomyTests
    {
        [Test]
        public void DayEndsWithAReportCostsAndNoOpenTickets()
        {
            var sim = TestData.NewSim();
            sim.StartDay();
            sim.AdvanceMinutes(sim.Gd.DayMinutes + 1);
            Assert.AreEqual(Phases.Report, sim.Phase);
            var d = sim.S.History.Last();
            Assert.Greater(d.Wages, 0);
            Assert.Greater(d.Rent, 0);
            Assert.Greater(d.Served, 0);
            Assert.AreEqual(0, sim.S.Tickets.Count, "closing time closes the rail");
            Assert.IsTrue(sim.NextDay());
            Assert.AreEqual(2, sim.S.Day);
            Assert.AreEqual(Phases.Plan, sim.Phase);
        }

        [Test]
        public void FirstDaysAndTheGuideNeverLowerTheRating()
        {
            var sim = TestData.NewSim();
            sim.S.Staff.Clear();   // a terrible day
            double r = sim.S.Rating;
            sim.StartDay();
            sim.AdvanceMinutes(sim.Gd.DayMinutes + 1);
            Assert.GreaterOrEqual(sim.S.Rating, r);
        }

        [Test]
        public void NoCrisesDuringTheGuideOrTheFirstDays()
        {
            var sim = TestData.NewSim(5, tutorial: true);
            Assert.IsFalse(sim.CrisesAllowed);
            sim.Venue.Wear = 100;
            sim.StartDay();
            for (int i = 0; i < 30; i++) sim.TutorialOrder("espresso");
            sim.AdvanceMinutes(300);
            Assert.IsFalse(sim.Jammed, "wear never jams the grinder in the guide");
            var s2 = TestData.NewSim();
            Assert.IsFalse(s2.CrisesAllowed, "day 1 is a free day");
            s2.S.Day = 10;
            Assert.IsTrue(s2.CrisesAllowed);
        }

        [Test]
        public void PurchasesCheckCashRatingAndChapters()
        {
            var sim = TestData.NewSim();
            sim.S.Cash = 0;
            Assert.AreEqual("cash", sim.CanBuy("staff:barista").Reason);
            sim.S.Cash = 100000;
            Assert.AreEqual("rating", sim.CanBuy("venue:corner").Reason);
            Assert.AreEqual("locked", sim.CanBuy("venue:roastery").Reason, "one chapter at a time");
            Assert.AreEqual("research", sim.CanBuy("staff:roaster").Reason);
            Assert.AreEqual("locked", sim.CanBuy("ice").Reason, "the cart has no ice maker");
            sim.S.Rating = 4;
            Assert.AreEqual("", sim.Buy("venue:corner"));
            Assert.AreEqual("corner", sim.S.Venue, "you move in");
            Assert.AreEqual("owned", sim.CanBuy("venue:corner").Reason);
            Assert.IsTrue(sim.S.Menu.Contains("flat_white"), "the corner cafe's menu opens");
        }

        [Test]
        public void TheOldVenueEarnsBranchIncome()
        {
            var sim = TestData.NewSim();
            sim.S.Cash = 100000; sim.S.Rating = 4;
            sim.Buy("venue:corner");
            sim.StartDay();
            sim.AdvanceMinutes(sim.Gd.DayMinutes + 1);
            Assert.GreaterOrEqual(sim.S.History.Last().Branches, sim.Gd.Venue("cart").AutoFloor);
            sim.NextDay();
            Assert.AreEqual("", sim.SwitchVenue("cart"));
            Assert.AreEqual("cart", sim.S.Venue);
            sim.StartDay();
            Assert.AreEqual("phase", sim.SwitchVenue("corner"), "switch branches in the morning plan only");
        }

        [Test]
        public void ResearchRunsOnRealTimeAndCatchesUpOffline()
        {
            var sim = TestData.NewSim();
            sim.S.Cash = 5000;
            Assert.AreEqual("", sim.StartResearch("tamping_mat"));
            sim.Advance(1.0);
            Assert.AreEqual("tamping_mat", sim.S.Researching, "plan phase: still running on real time");
            sim.Touch(1000);
            sim.Offline(1000 + 3600);
            Assert.IsTrue(sim.Has("tamping_mat"));
        }

        [Test]
        public void OfflinePayIsCappedAndScalesWithOfflinePlus()
        {
            var sim = TestData.NewSim();
            sim.S.History.Add(new DayStats { Revenue = 2000 });
            sim.Touch(1000);
            var o = sim.Offline(1000 + 20 * 3600);
            Assert.IsTrue(o.Capped);
            Assert.AreEqual(sim.Gd.OfflineCapH, o.Hours, 1e-9);
            var s2 = TestData.NewSim();
            s2.S.History.Add(new DayStats { Revenue = 2000 });
            s2.SetPerks(new Dictionary<string, bool> { { "offline_plus", true } });
            s2.Touch(1000);
            var o2 = s2.Offline(1000 + 20 * 3600);
            Assert.Greater(o2.Amount, o.Amount);
        }

        [Test]
        public void GrantCashUsesTheUsualDayIncome()
        {
            var sim = TestData.NewSim();
            sim.S.History.Add(new DayStats { Revenue = 1000 });
            double c = sim.S.Cash;
            Assert.AreEqual(1000, sim.GrantCash(1), 1e-9);
            Assert.AreEqual(c + 1000, sim.S.Cash, 1e-9);
        }

        [Test]
        public void MilkSpoilsOvernightAndTheOrderArrivesAtOpening()
        {
            var sim = TestData.NewSim();
            sim.S.Milk[0] = 10;
            sim.StartDay();
            sim.S.Staff.Clear();
            sim.AdvanceMinutes(sim.Gd.DayMinutes + 1);
            Assert.AreEqual(10 * (1 - sim.Gd.MilkSpoil), sim.S.Milk[0], 1e-6);
            sim.NextDay();
            sim.StartDay();
            sim.AdvanceMinutes(1);
            Assert.AreEqual(5 + sim.S.MilkOrder[0], sim.S.Milk[0], 1e-6);
        }
    }

    public class RewardTests
    {
        const long Now = 1_800_000_000;

        [Test]
        public void RewardsAreNeverOfferedDuringTheGuide()
        {
            var sim = TestData.NewSim(1, tutorial: true);
            foreach (var p in Simulation.RewardPlacements) Assert.IsFalse(sim.RewardOffer(p, Now).Ok, p);
        }

        [Test]
        public void DailyCapAndCooldownPersistInTheCareer()
        {
            var sim = TestData.NewSim();
            sim.StartDay();
            Assert.IsTrue(sim.GrantReward("extra_barista", Now).Ok);
            sim.S.HelperUntil = -1;
            Assert.AreEqual("cooldown", sim.RewardOffer("extra_barista", Now + 10).Reason);
            var r = SaveCodec.Decode(SaveCodec.Encode(sim), sim.Gd);
            Assert.AreEqual("cooldown", r.Sim.RewardOffer("extra_barista", Now + 10).Reason, "a relaunch does not reset it");
            Assert.IsTrue(sim.GrantReward("extra_barista", Now + 1000).Ok); sim.S.HelperUntil = -1;
            Assert.IsTrue(sim.GrantReward("extra_barista", Now + 2000).Ok); sim.S.HelperUntil = -1;
            Assert.AreEqual("cap", sim.RewardOffer("extra_barista", Now + 3000).Reason);
            Assert.IsTrue(sim.RewardOffer("extra_barista", Now + 86400).Ok, "a new UTC day");
        }

        [Test]
        public void CashRewardsStayUnderOneFifthOfTheCheapestPack()
        {
            var sim = TestData.NewSim();
            sim.S.History.Add(new DayStats { Revenue = 1000 });
            var o = sim.RewardOffer("daily_bonus", Now);
            Assert.IsTrue(o.Ok);
            Assert.LessOrEqual(o.Amount, 0.2 * sim.DayIncome() + 1e-9);
            sim.S.OfflinePending = 5000;
            Assert.LessOrEqual(sim.RewardOffer("offline_x2", Now).Amount, 0.2 * sim.DayIncome() + 1e-9);
        }

        [Test]
        public void TheExtraBaristaWorksTheBar()
        {
            var sim = TestData.NewSim();
            sim.StartDay();
            sim.S.Staff.Clear();
            Assert.IsTrue(sim.GrantReward("extra_barista", Now).Ok);
            Assert.IsTrue(sim.HelperOn);
            sim.AdvanceMinutes(40);
            Assert.Greater(sim.S.Today.Served, 0, "the helper made coffee with nobody else behind the bar");
        }

        [Test]
        public void CrisisLastResortFixesTheJam_EmergencyStockNeedsLowStock()
        {
            var sim = TestData.NewSim();
            sim.StartDay();
            Assert.IsFalse(sim.RewardOffer("crisis_halve", Now).Ok);
            sim.Jam("t");
            var r = sim.GrantReward("crisis_halve", Now);
            Assert.IsTrue(r.Ok);
            Assert.IsFalse(sim.Jammed);
            Assert.AreEqual("context", sim.RewardOffer("emergency_stock", Now).Reason);
            sim.S.Milk[0] = 0;
            Assert.IsTrue(sim.GrantReward("emergency_stock", Now).Ok);
            Assert.Greater(sim.S.Milk[0], 0);
        }
    }

    public class SaveTests
    {
        [Test]
        public void RoundTripMidDayIsIdenticalAndKeepsPlaying()
        {
            var sim = TestData.NewSim(11);
            sim.S.Cash = 5000;
            sim.Buy("staff:barista");
            sim.SetBatch(true);
            sim.StartDay();
            sim.AdvanceMinutes(37.5);
            sim.Jam("t");
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
            Assert.IsFalse(SaveCodec.Decode(ok.Replace("\"role\":\"barista\"", "\"role\":\"wizard\""), gd).Ok);
            Assert.IsFalse(SaveCodec.Decode(ok.Replace("\"phase\":\"plan\"", "\"phase\":\"party\""), gd).Ok);
            Assert.IsFalse(SaveCodec.Decode(ok.Replace("\"venue\":\"cart\"", "\"venue\":\"moon\""), gd).Ok);
        }

        [Test]
        public void DecoderAcceptsNegativeCashTheReportScreenAndARoastInTheDrum()
        {
            var sim = TestData.RoasterySim();
            sim.BuyGreen("lowland");
            sim.StartRoast("lowland", Profiles.Medium);
            sim.AdvanceResearch(5);
            sim.S.Cash = -1234.5;
            sim.StartDay();
            sim.AdvanceMinutes(sim.Gd.DayMinutes + 1);
            Assert.AreEqual(Phases.Report, sim.Phase);
            var r = SaveCodec.Decode(SaveCodec.Encode(sim), sim.Gd);
            Assert.IsTrue(r.Ok, r.Error);
            Assert.IsNotNull(r.Sim.S.Roast);
            Assert.IsTrue(r.Sim.NextDay());
        }

        [Test]
        public void FuzzEveryMinuteOfADayDecodes()
        {
            // fleet §14 save fuzz: guide phase, purchase moment, mid-shot, batched pair, jam, roast, report — 0 unreadable saves
            var sim = TestData.NewSim(21, tutorial: true);
            sim.S.Cash = 1e5;
            sim.S.Rating = 4.2;
            sim.StartDay();
            sim.TutorialOrder("espresso"); sim.TutorialOrder("latte");
            int bad = 0;
            var errors = new HashSet<string>();
            for (int m = 0; m < sim.Gd.DayMinutes + 2; m++)
            {
                if (m == 10) sim.Buy("staff:barista");
                if (m == 12) sim.SetBatch(true);
                if (m == 20) sim.EndTutorial();
                if (m == 40) sim.Buy("venue:corner");
                if (m == 60) sim.Jam("fuzz");
                if (m == 70) sim.Prep("hot");
                if (m == 90) sim.Buy("milk_run");
                sim.AdvanceMinutes(1);
                var r = SaveCodec.Decode(SaveCodec.Encode(sim), sim.Gd);
                if (!r.Ok) { bad++; errors.Add(r.Error); }
            }
            Assert.AreEqual(0, bad, string.Join(",", errors));
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
            Assert.AreEqual(0, rep.StuckTickets, "the stuck-ticket rule: nothing waits on the rail for 2 h");
            foreach (var d in rep.Days) { Assert.Greater(d.Served, 50, "day " + d.Day); Assert.Greater(d.Net, 0, "day " + d.Day); }
            Assert.Greater(rep.Rating, TestData.Gd().StartRating, "a sensible player's reputation grows");
            Assert.Less(rep.Days.Average(d => d.WalkRate), 0.1, "no wall of walk-outs");
        }

        [Test]
        public void FirstDayFeelsGood()
        {
            var rep = BalanceRunner.Run(TestData.Gd(), 3, 1);
            var d = rep.Days[0];
            Assert.Greater(d.Net, 0, "day 1 earns money");
            Assert.Less(d.WalkRate, 0.12, "day 1 is not a wall of walk-outs");
            Assert.GreaterOrEqual(d.AvgReview, 3.0);
        }

        [Test]
        public void TwentyFiveDaysReachTheCornerCafe()
        {
            var rep = BalanceRunner.Run(TestData.Gd(), 9, 25);
            TestContext.Progress.WriteLine("BALANCE 25 days seed 9\n" + rep.Table() + string.Join("\n", rep.Log));
            Assert.GreaterOrEqual(rep.Chapter, 2, "Corner Cafe within 25 days (~1.5 h of play)");
            Assert.AreEqual(0, rep.StuckTickets);
        }
    }
}
