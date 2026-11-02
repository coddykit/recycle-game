// Grill Convoy — engine-free sim tests (NUnit). Same files run in the Unity Test Runner (EditMode) and under plain .NET
// (Tools/dotnet_check.sh). Data is read from Assets/Resources/data on disk.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        /// <summary>A career on day 4+ (crises allowed) with the truck at home, ready for the morning plan.</summary>
        public static Simulation Morning(int day = 5, long seed = 7)
        {
            var s = NewSim(seed);
            s.S.Day = day;
            var t = s.S.Trucks[0];
            t.Status = TruckStatus.Idle;
            t.AtNode = s.Gd.DepotNode;
            return s;
        }

        public static void OpenAt(Simulation s, int ti, string pitch)
        {
            var t = s.S.Trucks[ti];
            t.Pitch = s.Gd.Node(pitch);
            t.AtNode = t.Pitch;
            t.Status = TruckStatus.Service;
        }
    }

    public class DataTests
    {
        [Test]
        public void DataLoadsWithoutErrors()
        {
            var gd = TestData.Gd();
            Assert.IsTrue(gd.Ok, string.Join("; ", gd.Errors));
            Assert.AreEqual(3, gd.Pitches.Count, "MVP: 3 pitches");
            Assert.AreEqual(8, gd.Cards.Count, "MVP: 8 menu cards");
            Assert.AreEqual(8, gd.StationDefs.Count, "MVP: 8 station types");
            Assert.AreEqual(5, gd.Archetypes.Count, "MVP: 5 customer types");
            Assert.AreEqual(2, gd.Vehicles.Count, "MVP: 2 trucks");
            Assert.GreaterOrEqual(gd.Research.Count, 20);
        }

        [Test]
        public void EveryPitchReachableFromTheCommissary_PathsMatchKm()
        {
            var gd = TestData.Gd();
            foreach (int p in gd.Pitches)
            {
                Assert.Less(gd.Graph.Km(gd.DepotNode, p), RoadGraph.Inf, gd.Nodes[p].Id);
                var path = gd.Graph.Path(gd.DepotNode, p);
                Assert.AreEqual(gd.DepotNode, path[0]);
                Assert.AreEqual(p, path[path.Count - 1]);
                Assert.AreEqual(gd.Graph.Km(gd.DepotNode, p), gd.Graph.PathKm(path), 1e-6);
            }
        }

        [Test]
        public void ResearchNeverLongerThanTwentyMinutes()
        {
            foreach (var r in TestData.Gd().Research) Assert.LessOrEqual(r.Minutes, 20, r.Id);
        }

        [Test]
        public void TheStadiumIsFurtherThanThePlaza()
        {
            var gd = TestData.Gd();
            Assert.Greater(gd.Graph.Km(gd.DepotNode, gd.Node("p_stadium")), gd.Graph.Km(gd.DepotNode, gd.Node("p_plaza")));
        }
    }

    public class CoreTests
    {
        [Test]
        public void NewCareerStartsWithTruckOneAtThePlazaWindowClosed()
        {
            var sim = TestData.NewSim(1, tutorial: true);
            Assert.AreEqual(Phases.Plan, sim.Phase);
            Assert.AreEqual(1, sim.S.Trucks.Count);
            var t = sim.S.Trucks[0];
            Assert.AreEqual(sim.Gd.Node("p_plaza"), t.AtNode);
            Assert.IsTrue(t.Open);
            Assert.AreEqual(2, t.Crew.Count);
            Assert.Greater(t.Load[Stock.Patty], 0);
            Assert.IsTrue(sim.S.Tutorial);
            Assert.IsTrue(sim.PitchOpen(sim.Gd.Node("p_plaza")));
            Assert.IsFalse(sim.PitchOpen(sim.Gd.Node("p_boardwalk")));
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
        public void CustomersQueueAndGetServed_SalesAndStockMove()
        {
            var sim = TestData.NewSim();
            sim.StartDay();
            int patties = sim.S.Trucks[0].Load[Stock.Patty];
            sim.AdvanceMinutes(180);   // into the lunch rush
            Assert.Greater(sim.S.Today.Served, 20);
            Assert.Greater(sim.S.Today.Sales, 0);
            Assert.Less(sim.S.Trucks[0].Load[Stock.Patty], patties);
        }

        [Test]
        public void ALongLineMakesPeopleWalkAway()
        {
            var sim = TestData.NewSim();
            var t = sim.S.Trucks[0];
            t.Crew.Clear();
            t.Crew.Add(Roles.Cashier);   // a one-person truck at lunch
            sim.StartDay();
            sim.AdvanceMinutes(200);
            Assert.Greater(sim.S.Today.Walked, 0);
            Assert.LessOrEqual(t.Queue.Count, sim.QueueCap(t) + 10);
        }

        [Test]
        public void ASoldOutTruckTurnsCustomersAway()
        {
            var sim = TestData.NewSim();
            sim.S.Trucks[0].Load[Stock.Patty] = 0;
            sim.StartDay();
            sim.AdvanceMinutes(150);
            Assert.Greater(sim.S.Today.SoldOut, 0);
            Assert.Greater(sim.S.Today.SelloutMin, 100);
            Assert.AreEqual(0, sim.S.Today.Served);
        }

        [Test]
        public void TheMenuBoardChangesWhoStops()
        {
            var a = TestData.NewSim(3);
            var b = TestData.NewSim(3);
            b.S.Trucks[0].Menu.Clear();
            b.S.Trucks[0].Menu.Add("kids");   // office workers do not want the kids' burger
            a.StartDay(); b.StartDay();
            a.AdvanceMinutes(240); b.AdvanceMinutes(240);
            Assert.Greater(a.S.Today.Arrivals, b.S.Today.Arrivals * 1.2);
        }
    }

    public class LineTests
    {
        [Test]
        public void StartLineIsWindowBound_RunnerAndCashierHelp()
        {
            var sim = TestData.NewSim();
            var t = sim.S.Trucks[0];
            var l0 = sim.Line(t);
            Assert.IsTrue(l0.Complete);
            Assert.AreEqual(Stations.Window, l0.Bottleneck, "no cashier: the window is the slowest stage");
            t.Crew.Add(Roles.Runner);
            Assert.Greater(sim.Line(t).Throughput, l0.Throughput, "the runner floats to the slowest stage");
            t.Crew[2] = Roles.Cashier;
            Assert.Greater(sim.Line(t).Throughput, l0.Throughput);
        }

        [Test]
        public void WalkingDistanceSlowsTheLine_AdjacencyBonusesSpeedItUp()
        {
            var sim = TestData.NewSim();
            var t = sim.S.Trucks[0];
            t.Crew.Add(Roles.Cashier);
            double near = sim.Line(t).Throughput;
            // grill to the far corner: two extra tiles of walking
            var g = t.Layout.Find(s => s.Type == Stations.Grill);
            g.X = 3; g.Y = 2;
            var far = sim.Line(t);
            Assert.Greater(far.WalkTiles, 0);
            Assert.Less(far.Throughput, near);
            g.X = 1; g.Y = 2;
            // toaster next to the bench: +15 % on the bench (guide step 9)
            t.Crew[0] = Roles.Griller; t.Crew[1] = Roles.Assembler;
            t.Layout.Add(new StationTile { Type = Stations.Grill, X = 2, Y = 2 });
            double before = sim.Line(t).Assembly;
            var toaster = t.Layout.Find(s => s.Type == Stations.Toaster);
            Assert.IsFalse(sim.Adjacent(t, Stations.Toaster, Stations.Assembly));
            Assert.AreEqual("", sim.Move(0, toaster.Y * sim.GridW(t) + toaster.X, 1 * sim.GridW(t) + 0));
            Assert.IsTrue(sim.Adjacent(t, Stations.Toaster, Stations.Assembly));
            Assert.Greater(sim.Line(t).Assembly, before);
        }

        [Test]
        public void MoveSwapsUndoRestoresAndPresetsRoundTrip()
        {
            var sim = TestData.NewSim();
            var t = sim.S.Trucks[0];
            int w = sim.GridW(t);
            string before = Json.Write(StationTile.WriteList(t.Layout));
            Assert.AreEqual("", sim.SavePreset(0, 0));
            Assert.AreEqual("", sim.Move(0, 0 * w + 1, 2 * w + 1), "window onto the grill tile: swap");
            Assert.AreEqual(Stations.Grill, t.Layout[sim.TileIndexAt(t, 1, 0)].Type);
            Assert.AreEqual("", sim.UndoLayout(0));
            Assert.AreEqual(before, Json.Write(StationTile.WriteList(t.Layout)));
            sim.Move(0, 3 * 1 + 2 * w, 0);
            Assert.AreEqual("", sim.LoadPreset(0, 0));
            Assert.AreEqual(Stations.Toaster, t.Layout[sim.TileIndexAt(t, 3, 2)].Type);
            Assert.AreEqual("invalid", sim.SavePreset(0, 1), "a second preset slot needs the Convoy Pass");
            sim.SetPerks(new Dictionary<string, bool> { { "convoy_pass", true } });
            Assert.AreEqual("", sim.SavePreset(0, 1));
        }

        [Test]
        public void AutoArrangeNeverMakesTheLineWorse()
        {
            var sim = TestData.NewSim();
            sim.S.Cash = 1e6;
            sim.S.Research.Add("fryer");
            sim.S.Research.Add("drink_fridge");
            foreach (var st in new[] { "grill", "assembly", "fryer", "fridge" }) Assert.AreEqual("", sim.Buy("station:0:" + st), st);
            var t = sim.S.Trucks[0];
            t.Crew.Add(Roles.Cashier);
            double before = sim.LayoutScore(t);
            string r = sim.AutoLayout(0);
            Assert.That(r, Is.EqualTo("").Or.EqualTo("no_better"));
            Assert.GreaterOrEqual(sim.LayoutScore(t), before - 1e-9);
            var seen = new HashSet<int>();
            foreach (var s in t.Layout) Assert.IsTrue(seen.Add(s.Y * 10 + s.X), "no two stations on one tile");
        }

        [Test]
        public void ACoreStationCannotBeSold_OthersRefundHalf()
        {
            var sim = TestData.NewSim();
            var t = sim.S.Trucks[0];
            int w = sim.GridW(t);
            Assert.AreEqual("last", sim.Remove(0, 0 * w + 1));
            double cash = sim.S.Cash;
            Assert.AreEqual("", sim.Remove(0, 2 * w + 3));
            Assert.AreEqual(cash + sim.Gd.StationDefs["toaster"].Price * 0.5, sim.S.Cash, 1e-6);
        }

        [Test]
        public void MenuBoardSlotsAndResearchGates()
        {
            var sim = TestData.NewSim();
            Assert.AreEqual("research", sim.ToggleCard(0, "double"));
            sim.S.Research.Add("card_double");
            sim.S.Research.Add("card_spicy");
            Assert.AreEqual("", sim.ToggleCard(0, "double"));
            Assert.AreEqual("board_full", sim.ToggleCard(0, "spicy"), "3 cards until the bigger board");
            Assert.AreEqual("", sim.ToggleCard(0, "classic"));
            Assert.AreEqual("", sim.ToggleCard(0, "cheese"));
            Assert.AreEqual("last", sim.ToggleCard(0, "double"));
            Assert.AreEqual("research", sim.SetTier(0, 2));
        }
    }

    public class FleetTests
    {
        [Test]
        public void StartOfDaySendsTrucksToTheirPitches()
        {
            var sim = TestData.Morning();
            var t = sim.S.Trucks[0];
            t.Pitch = sim.Gd.Node("p_plaza");
            sim.StartDay();
            Assert.AreEqual(TruckStatus.Drive, t.Status);
            for (int i = 0; i < 60 && !t.Open; i++) sim.AdvanceMinutes(1);
            Assert.IsTrue(t.Open);
            Assert.AreEqual(sim.Gd.Node("p_plaza"), t.AtNode);
            Assert.Greater(sim.S.Today.Km, 0);
            Assert.Greater(sim.S.Today.Fuel, 0);
        }

        [Test]
        public void RestockRunGoesHomeReloadsAndComesBack()
        {
            var sim = TestData.NewSim();
            var t = sim.S.Trucks[0];
            sim.StartDay();
            sim.AdvanceMinutes(30);
            t.Load[Stock.Patty] = 5;
            int fridge = sim.S.Commissary.Stock[Stock.Patty];
            Assert.AreEqual("", sim.Restock(0));
            Assert.AreEqual(0, t.Queue.Count, "the line disperses");
            bool home = false;
            for (int i = 0; i < 120 && !(home && t.Open); i++)
            {
                sim.AdvanceMinutes(1);
                if (t.Status == TruckStatus.Restock) home = true;
            }
            Assert.IsTrue(home, "it stopped at the commissary");
            Assert.IsTrue(t.Open, "and opened again at its pitch");
            Assert.Greater(t.Load[Stock.Patty], 5);
            Assert.Less(sim.S.Commissary.Stock[Stock.Patty], fridge + 5);
        }

        [Test]
        public void RecallClosesAndRelocateMovesMidDay()
        {
            var sim = TestData.NewSim();
            sim.S.Districts.Add("boardwalk");
            sim.StartDay();
            sim.AdvanceMinutes(20);
            Assert.AreEqual("", sim.AssignPitch(0, sim.Gd.Node("p_boardwalk")));
            var t = sim.S.Trucks[0];
            for (int i = 0; i < 80 && !t.Open; i++) sim.AdvanceMinutes(1);
            Assert.AreEqual(sim.Gd.Node("p_boardwalk"), t.AtNode);
            Assert.AreEqual("", sim.Recall(0));
            for (int i = 0; i < 80 && t.Busy; i++) sim.AdvanceMinutes(1);
            Assert.IsFalse(t.Busy);
            Assert.AreEqual("locked", sim.AssignPitch(0, sim.Gd.Node("p_stadium")));
        }

        [Test]
        public void TrucksComeHomeAtDayEndAndLeftoversPartlySpoil()
        {
            var sim = TestData.NewSim();
            sim.StartDay();
            sim.AdvanceMinutes(sim.Gd.DayMinutes + 1);
            Assert.AreEqual(Phases.Report, sim.Phase);
            foreach (var t in sim.S.Trucks) { Assert.IsFalse(t.Busy); Assert.AreEqual(0, t.Load[Stock.Patty]); }
            var sim2 = TestData.NewSim();
            sim2.S.Trucks[0].Crew.Clear();   // nobody cooks: everything comes back
            sim2.StartDay();
            sim2.AdvanceMinutes(sim2.Gd.DayMinutes + 1);
            Assert.Greater(sim2.S.History[0].Waste, 0, "night spoilage of the patties that came back");
        }
    }

    public class CommissaryTests
    {
        [Test]
        public void PrepCostsNowAndLandsAfterItsTime()
        {
            var sim = TestData.NewSim();
            sim.StartDay();
            double cash = sim.S.Cash;
            int stock = sim.S.Commissary.Stock[Stock.Patty];
            Assert.AreEqual("", sim.Prep(Stock.Patty));
            Assert.AreEqual(cash - sim.BatchCost(Stock.Patty), sim.S.Cash, 1e-6);
            sim.AdvanceMinutes(sim.Gd.StockDefs[Stock.Patty].PrepMin + 1);
            Assert.AreEqual(stock + sim.BatchSize(Stock.Patty), sim.S.Commissary.Stock[Stock.Patty]);
        }

        [Test]
        public void TheQueueAndTheFridgeAreCapped()
        {
            var sim = TestData.NewSim();
            sim.S.Cash = 1e6;
            for (int i = 0; i < Simulation.MaxPrepQueue; i++) Assert.AreEqual("", sim.Prep(Stock.Bun));
            Assert.AreEqual("queue_full", sim.Prep(Stock.Bun));
            var sim2 = TestData.NewSim();
            sim2.S.Cash = 1e6;
            sim2.S.Commissary.Stock[Stock.Patty] = sim2.Gd.StockDefs[Stock.Patty].Cap - 10;
            Assert.AreEqual("fridge_full", sim2.Prep(Stock.Patty));
        }

        [Test]
        public void LoadingNeedsTheTruckAtHomeAndRespectsCaps()
        {
            var sim = TestData.NewSim();
            Assert.AreEqual(0, sim.Load(0, Stock.Patty, 20, out string why));
            Assert.AreEqual("away", why, "truck #1 stands at the plaza on day 1");
            var m = TestData.Morning();
            int fridge = m.S.Commissary.Stock[Stock.Patty];
            int moved = m.Load(0, Stock.Patty, 1000, out _);
            Assert.AreEqual(Math.Min(fridge, m.TruckCap(m.S.Trucks[0], Stock.Patty) - 120), moved);
            Assert.AreEqual(-10, m.Load(0, Stock.Patty, -10, out _));
        }

        [Test]
        public void NextDayRepacksLikeYesterday()
        {
            var sim = TestData.NewSim();
            sim.S.Cash = 1e5;
            sim.StartDay();
            sim.AdvanceMinutes(sim.Gd.DayMinutes + 1);
            sim.NextDay();
            var t = sim.S.Trucks[0];
            Assert.Greater(t.Load[Stock.Patty], 0, "the crew packed the truck from the commissary");
            Assert.LessOrEqual(t.Load[Stock.Patty], t.Pack[Stock.Patty]);
        }

        [Test]
        public void SuggestedPackFollowsTheForecast()
        {
            var sim = TestData.Morning();
            sim.S.Trucks[0].Pitch = sim.Gd.Node("p_plaza");
            int plaza = sim.Gd.Node("p_plaza");
            double weekdayC = sim.ForecastCustomers(plaza);
            var weekday = sim.SuggestPack(0);
            sim.S.Day = 6;   // Saturday: the offices are closed
            Assert.Less(sim.ForecastCustomers(plaza), weekdayC * 0.7);
            sim.S.Day = 13;  // a Saturday, with rain
            sim.S.Weather = "rain";
            var weekend = sim.SuggestPack(0);
            Assert.Greater(weekday[Stock.Patty], weekend[Stock.Patty]);
            Assert.LessOrEqual(weekday[Stock.Patty], sim.TruckCap(sim.S.Trucks[0], Stock.Patty));
            Assert.AreEqual(0, weekday[Stock.Side], "no fryer, no fridge: no sides");
        }
    }

    public class CrisisTests
    {
        [Test]
        public void FlareUpHalvesTheGrillUntilExtinguished()
        {
            var sim = TestData.NewSim();
            sim.StartDay();
            var t = sim.S.Trucks[0];
            t.Crew.Add(Roles.Cashier);
            double before = sim.LiveThroughput(t);
            sim.Flare(0);
            Assert.IsTrue(t.Flaring);
            Assert.AreEqual(before * 0.5, sim.LiveThroughput(t), 1e-9);
            Assert.AreEqual("", sim.Extinguish(0));
            Assert.AreEqual(before, sim.LiveThroughput(t), 1e-9);
            sim.Flare(0);
            sim.AdvanceMinutes(sim.Gd.Svc.FlareMin + 1);
            Assert.IsFalse(t.Flaring, "a flare-up burns out by itself (never a hard lock)");
        }

        [Test]
        public void EmptyPropaneDarkensTheGrill_SwapCostsCashOrAKit()
        {
            var sim = TestData.NewSim();
            sim.StartDay();
            var t = sim.S.Trucks[0];
            t.Propane = 0;
            Assert.AreEqual(0, sim.LiveThroughput(t));
            double cash = sim.S.Cash;
            Assert.AreEqual("", sim.SwapPropane(0));
            Assert.AreEqual(cash - sim.Gd.Svc.PropaneSwap, sim.S.Cash, 1e-6);
            Assert.Greater(sim.LiveThroughput(t), 0);
            t.Propane = 0;
            sim.S.PropaneKits = 1;
            cash = sim.S.Cash;
            Assert.AreEqual("", sim.SwapPropane(0));
            Assert.AreEqual(cash, sim.S.Cash, 1e-6);
            Assert.AreEqual(0, sim.S.PropaneKits);
        }

        [Test]
        public void HalftimeStampedeIsOnTheCalendar()
        {
            var sim = TestData.NewSim();
            sim.S.Districts.Add("boardwalk");
            sim.S.Districts.Add("stadium");
            int stadium = sim.Gd.Node("p_stadium");
            int day = sim.Gd.MatchEvery;
            Assert.IsTrue(sim.MatchDay(day));
            Assert.IsFalse(sim.MatchDay(day + 1));
            sim.S.Day = day;
            sim.S.Minute = sim.Gd.HalftimeFrom + 1;
            Assert.IsTrue(sim.Halftime(stadium));
            Assert.AreEqual(sim.Gd.HalftimeMult, sim.EventMult(stadium), 1e-9);
            Assert.AreEqual(1.0, sim.EventMult(sim.Gd.Node("p_plaza")), 1e-9);
        }

        [Test]
        public void RivalTruckSplitsTheCrowd_CounterOfferWinsSomeBack()
        {
            var sim = TestData.NewSim();
            int plaza = sim.Gd.Node("p_plaza");
            sim.S.Day = 6;
            sim.PitchState(plaza).Rep = 60;
            Assert.IsTrue(sim.Fire("rival"));
            var t = sim.S.Trucks[0];
            double mult = sim.RivalMult(plaza, t);
            Assert.Less(mult, 1.0);
            Assert.AreEqual("", sim.Promo(0));
            Assert.Greater(sim.RivalMult(plaza, t), mult);
        }

        [Test]
        public void AllHandsBoostsThenCoolsDown()
        {
            var sim = TestData.NewSim();
            sim.StartDay();
            var t = sim.S.Trucks[0];
            double x = sim.LiveThroughput(t);
            Assert.AreEqual("", sim.AllHands(0));
            Assert.AreEqual(x * sim.Gd.Svc.AllHandsMult, sim.LiveThroughput(t), 1e-9);
            Assert.AreEqual("cooldown", sim.AllHands(0));
            sim.AdvanceMinutes(sim.Gd.Svc.AllHandsMin + 1);
            Assert.AreEqual(x, sim.LiveThroughput(t), 1e-6);
        }

        [Test]
        public void NoCrisesDuringTheGuideOrTheFirstDays()
        {
            var sim = TestData.NewSim(5);
            for (int d = 0; d < sim.Gd.EventFreeDays; d++)
            {
                BalanceRunner.PlayDay(sim, new BalanceReport());
                foreach (var e in sim.DrainEvents()) Assert.AreNotEqual("crisis", e.Type);
                Assert.AreEqual(0, sim.S.Planned.Count);
                sim.NextDay();
            }
            Assert.AreEqual(sim.Gd.EventFreeDays + 1, sim.S.Day);
            Assert.IsFalse(new[] { "hot", "rain" }.Contains(sim.S.History[0].Weather));
        }
    }

    public class EconomyTests
    {
        [Test]
        public void DayEndsWithAReportStarsAndCosts()
        {
            var sim = TestData.NewSim();
            BalanceRunner.PlayDay(sim, new BalanceReport());
            Assert.AreEqual(Phases.Report, sim.Phase);
            var d = sim.S.History[sim.S.History.Count - 1];
            Assert.Greater(d.Wages, 0);
            Assert.Greater(d.Permits, 0);
            Assert.Greater(d.Served, 0);
            Assert.That(d.Stars, Is.InRange(0, 9));
            Assert.IsTrue(sim.NextDay());
            Assert.AreEqual(2, sim.S.Day);
            Assert.AreEqual(Phases.Plan, sim.Phase);
        }

        [Test]
        public void FirstDaysAndTheGuideNeverLowerFame()
        {
            var sim = TestData.NewSim(3, tutorial: true);
            sim.S.Trucks[0].Crew.Clear();
            double f0 = sim.S.Fame;
            sim.StartDay();
            sim.AdvanceMinutes(sim.Gd.DayMinutes + 5);   // nobody cooks
            Assert.GreaterOrEqual(sim.S.Fame, f0);
        }

        [Test]
        public void PurchasesCheckCashFameChaptersAndResearch()
        {
            var sim = TestData.NewSim();
            sim.S.Cash = 100;
            Assert.AreEqual("cash", sim.Buy("hire:0:runner"));
            sim.S.Cash = 1e6;
            Assert.AreEqual("", sim.Buy("hire:0:runner"));
            Assert.AreEqual("max", sim.Buy("hire:0:cashier"), "the starter truck takes 3 crew");
            Assert.AreEqual("research", sim.Buy("station:0:fryer"));
            Assert.AreEqual("", sim.Buy("station:0:grill"));
            Assert.AreEqual("research", sim.Buy("station:0:window"), "a second window needs research");
            Assert.AreEqual("chapter", sim.Buy("truck:box"), "truck #2 comes with the stadium chapter");
            sim.S.Fame = 10;
            Assert.AreEqual("fame", sim.Buy("pitch:boardwalk"));
            Assert.AreEqual("locked", sim.Buy("pitch:stadium"), "pitches unlock in order");
            sim.S.Fame = 60;
            Assert.AreEqual("", sim.Buy("pitch:boardwalk"));
            Assert.AreEqual("", sim.Buy("pitch:stadium"));
            Assert.AreEqual("", sim.Buy("truck:box"));
            Assert.AreEqual(2, sim.S.Trucks.Count);
            Assert.AreEqual("max", sim.Buy("truck:box"));
            Assert.AreEqual("research", sim.Buy("extend:0"));
        }

        [Test]
        public void ResearchRunsOnRealTimeAndCatchesUpOffline()
        {
            var sim = TestData.NewSim();
            sim.S.Cash = 1e5;
            Assert.AreEqual("", sim.StartResearch("fryer"));
            Assert.AreEqual("busy", sim.StartResearch("card_spicy"));
            sim.Advance(1.0);
            Assert.IsFalse(sim.Has("fryer"));
            sim.Touch(1000);
            sim.Offline(1000 + 3600);
            Assert.IsTrue(sim.Has("fryer"));
            Assert.AreEqual("requires", sim.CanResearch("card_premium"));
            Assert.AreEqual("locked", sim.CanResearch("card_kids"), "the kids' card needs the Boardwalk");
        }

        [Test]
        public void OfflinePayIsCappedScalesWithRulesAndOfflinePlus()
        {
            var sim = TestData.NewSim();
            sim.S.History.Add(new DayStats { Sales = 1000 });
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
            sim.S.Research.Add("dispatcher");
            Assert.AreEqual("", sim.SetAuto(0, true));
            sim.Touch(t0);
            Assert.Greater(sim.Offline(t0 + 100 * 3600).Amount, b.Amount, "a convoy on rules earns more while away");
        }

        [Test]
        public void RuleSlotsAreCappedAndGrowWithThePassAndHeadCookPlus()
        {
            var sim = TestData.NewSim();
            Assert.AreEqual("research", sim.SetAuto(0, true));
            sim.S.Research.Add("dispatcher");
            Assert.AreEqual(sim.Gd.RuleSlots, sim.RuleSlots);
            sim.SetPerks(new Dictionary<string, bool> { { "convoy_pass", true }, { "automation", true } });
            Assert.AreEqual(sim.Gd.RuleSlots + sim.Gd.PassSlots + sim.Gd.PlusSlots, sim.RuleSlots);
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

    public class GuideSimTests
    {
        [Test]
        public void TheHandCookedTicketGoesThroughEveryStationInOrder()
        {
            var sim = TestData.NewSim(1, tutorial: true);
            sim.StartDay();
            sim.BeginHandTicket();
            Assert.AreEqual("order", sim.HandCook("flip"), "out of order does nothing");
            Assert.AreEqual("", sim.HandCook("ticket"));
            Assert.AreEqual("", sim.HandCook("grill"));
            Assert.AreEqual(3, sim.HandStage);
            Assert.AreEqual("order", sim.HandCook("flip"), "still sizzling");
            sim.AdvanceMinutes(3);
            Assert.AreEqual(4, sim.HandStage);
            Assert.AreEqual("", sim.HandCook("flip"));
            Assert.AreEqual("", sim.HandCook("assemble"));
            double cash = sim.S.Cash;
            Assert.AreEqual("", sim.HandCook("serve"));
            Assert.Greater(sim.S.Cash, cash);
            Assert.AreEqual(0, sim.HandStage);
            Assert.AreEqual(1, sim.S.Today.Served);
        }

        [Test]
        public void NoCustomersArriveWhileTheHandTicketIsOpen()
        {
            var sim = TestData.NewSim(1, tutorial: true);
            sim.StartDay();
            sim.BeginHandTicket();
            sim.AdvanceMinutes(60);
            Assert.AreEqual(1, sim.S.Trucks[0].Queue.Count);
            Assert.AreEqual(0, sim.S.Today.Served);
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
            sim.StartDay();
            Assert.IsTrue(sim.GrantReward("bonus_patties", now).Ok);
            Assert.AreEqual("cooldown", sim.RewardOffer("bonus_patties", now + 10).Reason);
            Assert.IsTrue(sim.GrantReward("bonus_patties", now + 700).Ok);
            Assert.IsTrue(sim.GrantReward("bonus_patties", now + 1400).Ok);
            Assert.AreEqual("cap", sim.RewardOffer("bonus_patties", now + 5000).Reason);
            var back = SaveCodec.Decode(SaveCodec.Encode(sim), sim.Gd).Sim;
            Assert.AreEqual("cap", back.RewardOffer("bonus_patties", now + 5000).Reason, "caps survive a relaunch");
            Assert.IsTrue(back.RewardOffer("bonus_patties", now + 86400).Ok, "a new UTC day resets the cap");
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
        public void CrisisRescueOnlyWhenATruckIsInTrouble()
        {
            var sim = TestData.NewSim();
            long now = 86400L * 20000;
            sim.StartDay();
            Assert.AreEqual("context", sim.RewardOffer("crisis_rescue", now).Reason);
            sim.Flare(0);
            Assert.IsTrue(sim.GrantReward("crisis_rescue", now).Ok);
            Assert.IsFalse(sim.S.Trucks[0].Flaring);
        }

        [Test]
        public void QuickPrepFinishesTheBatchInProgress()
        {
            var sim = TestData.NewSim();
            long now = 86400L * 20000;
            Assert.AreEqual("context", sim.RewardOffer("quick_prep", now).Reason);
            sim.Prep(Stock.Patty);
            int before = sim.S.Commissary.Stock[Stock.Patty];
            Assert.IsTrue(sim.GrantReward("quick_prep", now).Ok);
            Assert.AreEqual(before + sim.BatchSize(Stock.Patty), sim.S.Commissary.Stock[Stock.Patty]);
            Assert.AreEqual(0, sim.S.Commissary.Queue.Count);
        }

        [Test]
        public void ExtraRunnerHelpsOneOpenTruckForAWhile()
        {
            var sim = TestData.NewSim();
            long now = 86400L * 20000;
            Assert.AreEqual("context", sim.RewardOffer("extra_runner", now).Reason, "only while the day runs");
            sim.StartDay();
            double x = sim.Line(sim.S.Trucks[0]).Throughput;
            Assert.IsTrue(sim.GrantReward("extra_runner", now).Ok);
            Assert.Greater(sim.Line(sim.S.Trucks[0]).Throughput, x);
            sim.AdvanceMinutes(sim.Gd.Svc.HelperMin + 1);
            Assert.AreEqual(x, sim.Line(sim.S.Trucks[0]).Throughput, 1e-9);
        }
    }

    public class SaveTests
    {
        [Test]
        public void RoundTripMidDayIsIdenticalAndKeepsPlaying()
        {
            var sim = TestData.NewSim(11);
            sim.StartDay();
            sim.AdvanceMinutes(150);
            sim.Flare(0);
            sim.Prep(Stock.Patty);
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
            Assert.IsFalse(SaveCodec.Decode(ok.Replace("\"type\":\"starter\"", "\"type\":\"ufo\""), gd).Ok);
            Assert.IsFalse(SaveCodec.Decode(ok.Replace("\"phase\":\"plan\"", "\"phase\":\"party\""), gd).Ok);
            Assert.IsFalse(SaveCodec.Decode(ok.Replace("\"t\":\"toaster\"", "\"t\":\"jacuzzi\""), gd).Ok);
            Assert.IsFalse(SaveCodec.Decode(ok.Replace("\"griller\"", "\"astronaut\""), gd).Ok);
        }

        [Test]
        public void DecoderAcceptsNegativeCashTheReportAndAnEmptyCommissary()
        {
            var sim = TestData.NewSim();
            sim.S.Cash = -1234.5;
            sim.S.Commissary.Stock = new int[Stock.N];
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
            // fleet §14 save fuzz: guide hand ticket, purchase moment, mid-restock, flare-up, layout edit, report — 0 unreadable
            var sim = TestData.NewSim(21, tutorial: true);
            sim.S.Cash = 1e5;
            sim.StartDay();
            sim.BeginHandTicket();
            int bad = 0;
            for (int m = 0; m < sim.Gd.DayMinutes + 2; m++)
            {
                if (m == 2) sim.HandCook("ticket");
                if (m == 3) sim.HandCook("grill");
                if (m == 6) { sim.HandCook("flip"); sim.HandCook("assemble"); sim.HandCook("serve"); }
                if (m == 30) sim.Buy("hire:0:runner");
                if (m == 40) sim.Buy("station:0:grill");
                if (m == 41) sim.AutoLayout(0);
                if (m == 60) sim.Prep(Stock.Patty);
                if (m == 120) sim.EndTutorial();
                if (m == 130) sim.Flare(0);
                if (m == 200) sim.Restock(0);
                if (m == 210) sim.SavePreset(0, 0);
                sim.AdvanceMinutes(1);
                var r = SaveCodec.Decode(SaveCodec.Encode(sim), sim.Gd);
                if (!r.Ok) { bad++; TestContext.Progress.WriteLine("minute " + m + ": " + r.Error); }
            }
            Assert.AreEqual(0, bad);
        }

        [Test]
        public void FuzzSevenBotDaysEveryHourDecodes()
        {
            int bad = 0;
            BalanceRunner.Run(TestData.Gd(), 77, 7, true, sim =>
            {
                for (int h = 0; h < 3; h++)
                    if (!SaveCodec.Decode(SaveCodec.Encode(sim), sim.Gd).Ok) bad++;
            });
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
            foreach (var d in rep.Days) { Assert.Greater(d.Served, 0, "day " + d.Day); week += d.Net; }
            Assert.Greater(week, 0, "a sensible player earns over a week (money also went into stations / research)");
            double late = (rep.Days[4].Net + rep.Days[5].Net + rep.Days[6].Net) / 3.0;
            Assert.Greater(late, rep.Days[0].Net, "the business grows: days 5-7 earn more than day 1");
            Assert.Greater(rep.MinCash, 0, "never broke");
        }

        [Test]
        public void FirstDayFeelsGood()
        {
            var rep = BalanceRunner.Run(TestData.Gd(), 3, 1);
            var d = rep.Days[0];
            Assert.Greater(d.Net, 0, "day 1 earns money");
            Assert.Less(d.LostRate, 0.35, "day 1 is not a wall of walk-aways");
        }

        [Test]
        public void PacingMatchesThePlanChapterTimes()
        {
            // plan §6: chapter 2 (Boardwalk) at ~1:30 h, chapter 3 (Stadium) at ~3:30 h; one day ≈ 5 real minutes at 1×
            // (4 min of service + morning / report). Accept ±25 %: Boardwalk days 15–23, Stadium days 33–53.
            foreach (long seed in new long[] { 9, 42 })
            {
                var rep = BalanceRunner.Run(TestData.Gd(), seed, 55);
                TestContext.Progress.WriteLine("BALANCE 55 days seed " + seed + "\n" + string.Join("\n", rep.Log));
                int board = UnlockDay(rep, "pitch boardwalk"), stadium = UnlockDay(rep, "pitch stadium");
                Assert.That(board, Is.InRange(15, 23), "seed " + seed + ": the Boardwalk at ~1:30 h");
                Assert.That(stadium, Is.InRange(33, 53), "seed " + seed + ": the Stadium at ~3:30 h");
                Assert.GreaterOrEqual(rep.Chapter, 3);
                Assert.Greater(rep.Stations, 4, "the truck grew");
                Assert.Greater(rep.MinCash, 0, "never broke");
            }
        }

        static int UnlockDay(BalanceReport rep, string what)
        {
            foreach (var l in rep.Log)
                if (l.EndsWith(what)) return int.Parse(l.Split(' ')[1]);
            return -1;
        }

        [Test]
        public void QuietDaysDoNotGiveTheThirdServiceStar()
        {
            // a quiet weekend at the offices: nobody walks away, but an idle crew earns at most 2 service stars
            var gd = TestData.Gd();
            Assert.Greater(gd.StarBusy, 0);
            int capped = 0, weekends = 0;
            var rep = BalanceRunner.Run(gd, 42, 28);
            foreach (var d in rep.Days)
            {
                if (d.Day <= gd.FameFreeDays || (d.Day - 1) % 7 < 5) continue;
                weekends++;
                if (d.StarService <= 2) capped++;
            }
            Assert.Greater(weekends, 0);
            Assert.Greater(capped, weekends / 2, "most quiet weekends stop at 2 service stars");
            int nine = 0;
            foreach (var d in rep.Days) if ((d.Day - 1) % 7 >= 5 && d.Stars == 9) nine++;
            Assert.LessOrEqual(nine, 1, "9/9 weekends are rare");
        }
    }
}
