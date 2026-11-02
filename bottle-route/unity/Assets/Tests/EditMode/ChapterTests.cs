// Bottle Route — chapters 5-7 (plan §6): Industrial Estate (bale tenders, bulk counting centre), Riviera Resort (summer
// season, hotels), Hill Villages + Island (large remote machines, the ferry), unlock order, and an older save loading.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using BottleRoute.Sim;

namespace BottleRoute.Sim.Tests
{
    public class ChapterTests
    {
        static Simulation Open(params string[] districts)
        {
            var s = TestData.NewSim(5);
            s.S.Cash = 1e6;
            foreach (var d in districts) s.S.Districts.Add(d);
            return s;
        }

        static List<SimEvent> Of(Simulation sim, string type) => sim.DrainEvents().FindAll(e => e.Type == type);

        // ------------------------------------------------------------------------------------------------ districts

        [Test]
        public void NewDistrictsUnlockInOrderWithCostAndRating()
        {
            var sim = TestData.NewSim();
            sim.S.Cash = 1e7;
            sim.S.Rating = 100;
            Assert.AreEqual("locked", sim.CanBuy("district:industrial").Reason, "chapter 5 needs chapter 4 first");
            foreach (var d in new[] { "oldtown", "university", "stadium" }) Assert.AreEqual("", sim.Buy("district:" + d), d);
            Assert.AreEqual("locked", sim.CanBuy("district:riviera").Reason);
            double cash = sim.S.Cash;
            Assert.AreEqual("", sim.Buy("district:industrial"));
            Assert.AreEqual(cash - sim.Gd.District("industrial").Cost, sim.S.Cash, 1e-6);
            sim.S.Rating = sim.Gd.District("riviera").Rating - 1;
            Assert.AreEqual("rating", sim.CanBuy("district:riviera").Reason);
            sim.S.Rating = 100;
            Assert.AreEqual("", sim.Buy("district:riviera"));
            Assert.AreEqual("locked", sim.CanBuy("district:island").Reason, "the island comes after the hill villages");
            Assert.AreEqual("", sim.Buy("district:villages"));
            Assert.AreEqual("", sim.Buy("district:island"));
            Assert.AreEqual(7, sim.Chapter);
            // costs rise chapter by chapter: the post-stadium bank has somewhere to go
            Assert.Greater(sim.Gd.District("industrial").Cost, sim.Gd.District("stadium").Cost * 3);
            Assert.Greater(sim.Gd.District("villages").Cost, sim.Gd.District("riviera").Cost);
        }

        [Test]
        public void BulkCountingCentreComesWithTheIndustrialEstate()
        {
            var sim = TestData.NewSim();
            sim.S.Cash = 1e6;
            Assert.AreEqual("locked", sim.CanBuy("bulk_centre").Reason);
            double before = sim.CountPerMinute;
            sim.S.Districts.Add("industrial");
            Assert.AreEqual("", sim.Buy("bulk_centre"));
            Assert.AreEqual(before + sim.Gd.BulkCentrePerHour / 60.0, sim.CountPerMinute, 1e-9);
            Assert.AreEqual("owned", sim.CanBuy("bulk_centre").Reason);
        }

        [Test]
        public void LargeRemoteMachinesAndHotels()
        {
            var sim = Open("oldtown", "university", "stadium", "industrial", "riviera", "villages");
            Assert.AreEqual("invalid", sim.CanBuy("machine:v_mill").Reason, "a village site takes the large machine");
            Assert.AreEqual("", sim.Buy("machine_l:v_olive"));
            int i = sim.S.Points.Count - 1;
            var p = sim.S.Points[i];
            Assert.AreEqual("machine_l", p.Kind);
            Assert.IsTrue(p.IsMachine, "jams, sensors and compactors apply");
            Assert.Greater(sim.Cap(p), sim.Gd.PointKinds["machine_m"].CapL * 1.5);
            Assert.AreEqual(Policies.Threshold, p.Policy);
            Assert.AreEqual("owned", sim.CanBuy("bin_m:" + i).Reason, "already the big bin");
            Assert.AreEqual("research", sim.CanBuy("hotel:r_hotel_a").Reason);
            sim.S.Research.Add("horeca");
            Assert.AreEqual("", sim.Buy("hotel:r_hotel_a"));
            var h = sim.S.Points[sim.S.Points.Count - 1];
            Assert.AreEqual("hotel", h.Kind);
            Assert.IsFalse(h.IsMachine);
            Assert.Greater(sim.Cap(h), sim.Gd.PointKinds["horeca"].CapL);
        }

        // ------------------------------------------------------------------------------------------------ tenders

        [Test]
        public void TendersAreOfferedOnlyWithTheIndustrialEstateAndCapped()
        {
            var sim = TestData.NewSim();
            Assert.IsNull(sim.OfferTender(true), "no tenders before chapter 5");
            Assert.IsNull(TestData.NewSim(5, tutorial: true).OfferTender(true));
            sim.S.Districts.Add("industrial");
            for (int k = 0; k < sim.Gd.TenderMaxOpen; k++) Assert.IsNotNull(sim.OfferTender(true));
            Assert.IsNull(sim.OfferTender(true), "at most max_open at a time");
            foreach (var t in sim.S.Tenders)
            {
                var g = sim.Gd;
                Assert.GreaterOrEqual(t.Price, g.Materials[t.Mat].BalePrice * g.TenderPremiumMin - 1, "a premium over the usual price");
                Assert.LessOrEqual(t.Price, g.Materials[t.Mat].BalePrice * g.TenderPremiumMax + 1);
                Assert.GreaterOrEqual(t.DueDay, sim.S.Day + g.TenderDueMin);
                Assert.LessOrEqual(t.Bales, t.Mat == Mats.Glass ? sim.GlassBayCap : sim.S.Depot.YardCap, "the yard can hold it");
                Assert.IsFalse(t.Accepted);
            }
            int id = sim.S.Tenders[0].Id;
            Assert.AreEqual("", sim.DeclineTender(id));
            Assert.IsNull(sim.TenderById(id));
        }

        [Test]
        public void AcceptedTenderIsDeliveredAtItsPrice()
        {
            var sim = Open("industrial");
            var t = sim.OfferTender(true);
            Assert.AreEqual("phase", sim.DeliverTender(t.Id), "accept first");
            Assert.AreEqual("", sim.AcceptTender(t.Id));
            Assert.AreEqual(t.Bales, sim.BalesPromised(t.Mat));
            sim.S.Depot.Bales[t.Mat] = t.Bales - 1;
            Assert.IsFalse(sim.CanDeliver(t));
            Assert.AreEqual("no_bales", sim.DeliverTender(t.Id));
            sim.S.Depot.Bales[t.Mat] = t.Bales + 1;
            double cash = sim.S.Cash;
            Assert.AreEqual("", sim.DeliverTender(t.Id));
            Assert.AreEqual(cash + Math.Round(t.Bales * t.Price), sim.S.Cash, 1e-6);
            Assert.AreEqual(1, sim.S.Depot.Bales[t.Mat], "only the tender's bales leave the yard");
            Assert.AreEqual(0, sim.S.Tenders.Count);
            Assert.AreEqual(Math.Round(t.Bales * t.Price), sim.S.Today.Sales, 1e-6);
            Assert.AreEqual(1, Of(sim, "tender_done").Count);
        }

        [Test]
        public void AcceptedTenderDeliversItselfAtDayEnd()
        {
            var sim = Open("industrial");
            var t = sim.OfferTender(true);
            sim.AcceptTender(t.Id);
            sim.S.Depot.Bales[t.Mat] = t.Bales;
            sim.StartDay();
            sim.AdvanceMinutes(sim.Gd.DayMinutes + 1);
            Assert.AreEqual(Phases.Report, sim.Phase);
            Assert.IsNull(sim.TenderById(t.Id));
            Assert.AreEqual(1, Of(sim, "tender_done").Count);
            Assert.GreaterOrEqual(sim.S.History[sim.S.History.Count - 1].Sales, Math.Round(t.Bales * t.Price) - 1e-6);
        }

        [Test]
        public void UnfilledTenderRunsOutWithoutAPenalty_AndOffersLapse()
        {
            var sim = Open("industrial");
            var a = sim.OfferTender(true);
            var b = sim.OfferTender(true);
            sim.AcceptTender(a.Id);
            for (int m = 0; m < Mats.N; m++) sim.S.Depot.Bales[m] = 0;
            sim.S.Depot.BalerDown = true;   // no trucks out, no bales: the tender cannot be filled
            var seen = new List<SimEvent>();
            int lastDay = a.DueDay + 1;
            while (sim.S.Day < lastDay)
            {
                sim.StartDay();
                sim.AdvanceMinutes(sim.Gd.DayMinutes + 1);
                sim.NextDay();
                seen.AddRange(sim.DrainEvents());
            }
            var lapsed = seen.Find(e => e.Type == "tender_lapsed" && e.A == b.Id);
            var expired = seen.Find(e => e.Type == "tender_expired" && e.A == a.Id);
            Assert.IsNotNull(lapsed, "an offer nobody accepted lapses");
            Assert.IsNotNull(expired, "an accepted tender past its day runs out");
            Assert.IsNull(sim.TenderById(a.Id));
            Assert.IsNull(sim.TenderById(b.Id));
            foreach (var h in sim.S.History) Assert.AreEqual(0, h.Fines, "no fine for a missed tender");
        }

        // ------------------------------------------------------------------------------------------------ season

        [Test]
        public void SummerSeasonTriplesRivieraArrivalsInItsWindow()
        {
            var gd = TestData.Gd();
            var sim = Open("riviera");
            Assert.AreEqual("", sim.Buy("machine:r_beach"));
            var beach = sim.S.Points[sim.S.Points.Count - 1];
            var fish = sim.S.Points[0];
            int inDay = gd.SeasonStart + gd.SeasonEvery, outDay = inDay + gd.SeasonDays;
            Assert.IsTrue(sim.SeasonWindow(inDay));
            Assert.IsTrue(sim.SeasonWindow(outDay - 1));
            Assert.IsFalse(sim.SeasonWindow(outDay));
            Assert.IsFalse(sim.SeasonWindow(inDay - 1));
            sim.S.Day = inDay;
            Assert.IsTrue(sim.InSeason);
            Assert.AreEqual(gd.SeasonMult, sim.SeasonMult(beach), 1e-9);
            Assert.AreEqual(1.0, sim.SeasonMult(fish), 1e-9, "the harbour has no season");
            Assert.AreEqual(sim.NodeOf(beach).Rate * gd.HourCurve[sim.HourIndex] * gd.SeasonMult, sim.BaseRate(beach), 1e-9);
            CollectionAssert.Contains(sim.ActiveCrises(), "season");
            var heat = gd.Events.Find(e => e.Id == "heat_wave");
            Assert.AreEqual(heat.Weight * gd.SeasonHeatWeight, sim.EventWeight(heat), 1e-9, "heat waves are likelier in season");
            sim.S.Day = outDay;
            Assert.IsFalse(sim.InSeason);
            Assert.AreEqual(1.0, sim.SeasonMult(beach), 1e-9);
            Assert.AreEqual(heat.Weight, sim.EventWeight(heat), 1e-9);
            var plain = TestData.NewSim();
            plain.S.Day = inDay;
            Assert.IsFalse(plain.InSeason, "no season without the Riviera");
        }

        [Test]
        public void SeasonStartAndEndAreAnnouncedAndArrivalsRise()
        {
            var gd = TestData.Gd();
            var sim = Open("riviera");
            Assert.AreEqual("", sim.Buy("machine:r_beach"));
            Assert.AreEqual("", sim.Buy("bin_m:" + (sim.S.Points.Count - 1)));
            var beach = sim.S.Points[sim.S.Points.Count - 1];
            int inDay = gd.SeasonStart + gd.SeasonEvery;
            sim.S.Day = inDay - 1;
            sim.StartDay();
            sim.AdvanceMinutes(sim.Gd.DayMinutes + 1);
            int quiet = beach.Arrivals;
            sim.DrainEvents();
            sim.NextDay();
            Assert.AreEqual(1, Of(sim, "season_start").Count);
            Assert.IsTrue(sim.S.SeasonOn);
            sim.StartDay();
            sim.AdvanceMinutes(sim.Gd.DayMinutes + 1);
            Assert.Greater(beach.Arrivals, quiet * 2, "about three times the visitors (quiet day " + quiet + ")");
            sim.S.Day = inDay + gd.SeasonDays - 1;
            sim.DrainEvents();
            sim.NextDay();
            Assert.AreEqual(1, Of(sim, "season_end").Count);
            Assert.IsFalse(sim.S.SeasonOn);
        }

        // ------------------------------------------------------------------------------------------------ ferry

        [Test]
        public void TheIslandIsReachedOnlyByTheFerry()
        {
            var gd = TestData.Gd();
            int k = gd.Node("k_harbour");
            Assert.IsTrue(gd.Graph.HasFerry);
            var path = gd.Graph.Path(gd.DepotNode, k);
            int crossings = 0;
            for (int i = 1; i < path.Count; i++) if (gd.Graph.IsFerry(path[i - 1], path[i])) crossings++;
            Assert.AreEqual(1, crossings);
            Assert.Greater(gd.Graph.FerryKm(gd.DepotNode, k), 1);
            Assert.AreEqual(gd.Graph.Km(gd.DepotNode, k) - gd.Graph.FerryKm(gd.DepotNode, k), gd.Graph.RoadKm(gd.DepotNode, k), 1e-9);
            Assert.AreEqual(0, gd.Graph.FerryKm(gd.DepotNode, gd.Node("v_mill")), "the villages are reached by road");
            Assert.Greater(gd.Graph.Km(gd.DepotNode, gd.Node("v_spring")), gd.Graph.Km(gd.DepotNode, gd.Node("s_fair")) * 1.3, "the villages are far");
        }

        [Test]
        public void FerryLeavesEveryNMinutesFromTheDayStart()
        {
            var sim = TestData.NewSim();
            double s = sim.Gd.DayStart, ev = sim.Gd.FerryEveryMin;
            Assert.AreEqual(s, sim.NextFerry(s), 1e-9);
            Assert.AreEqual(s + ev, sim.NextFerry(s + 0.5), 1e-9);
            Assert.AreEqual(s + ev, sim.NextFerry(s + ev), 1e-9);
            Assert.AreEqual(s + 2 * ev, sim.NextFerry(s + ev + 1), 1e-9);
        }

        static (Simulation sim, int point) Island()
        {
            var sim = Open("oldtown", "university", "stadium", "industrial", "riviera", "villages", "island");
            Assert.AreEqual("", sim.Buy("machine_l:k_harbour"));
            int i = sim.S.Points.Count - 1;
            sim.FillTo(sim.S.Points[i], 0.5);
            return (sim, i);
        }

        [Test]
        public void EstimateIncludesTheFerryWaitAndTickets()
        {
            var (sim, i) = Island();
            var gd = sim.Gd;
            int k = gd.Node("k_harbour"), pier = gd.Node("r_pier");
            var e = sim.Estimate(0, new List<int> { i });
            Assert.IsTrue(e.Ok, e.Error);
            Assert.AreEqual(2, e.Ferries, "there and back");
            double arrive = gd.DayStart + gd.Graph.Km(gd.DepotNode, pier) / gd.Vehicles["van"].SpeedKmh * 60;
            double wait1 = sim.NextFerry(arrive) - arrive;
            Assert.Greater(wait1, 0, "the van reaches the pier between departures");
            Assert.GreaterOrEqual(e.FerryWait, wait1 - 1e-6);
            Assert.AreEqual(2 * gd.Graph.RoadKm(gd.DepotNode, k), e.Km, 1e-6, "km = road km (no fuel on the water)");
            Assert.AreEqual(e.Km * gd.Vehicles["van"].CostKm + 2 * gd.FerryTicket, e.Cost, 1e-6);
            double sail = 2 * gd.Graph.FerryKm(gd.DepotNode, k) / gd.FerrySpeedKmh * 60;
            Assert.AreEqual(e.Km / gd.Vehicles["van"].SpeedKmh * 60 + sail + e.FerryWait + gd.Vehicles["van"].ServiceMin + gd.UnloadBaseMin, e.Minutes, 1e-6);
            var main = sim.Estimate(0, new List<int> { 0 });
            Assert.AreEqual(0, main.Ferries);
            Assert.AreEqual(0, main.FerryWait);
        }

        [Test]
        public void TruckWaitsAtThePierSailsWithoutFuelAndSavesOnTheWay()
        {
            var (sim, i) = Island();
            var gd = sim.Gd;
            int pier = gd.Node("r_pier"), k = gd.Node("k_harbour");
            sim.StartDay();
            Assert.AreEqual("", sim.Dispatch(0, new List<int> { i }));
            var t = sim.S.Trucks[0];
            int guard = 0;
            while (t.FerryAt <= 0 && guard++ < 300) sim.AdvanceMinutes(1);
            Assert.Greater(t.FerryAt, sim.S.Minute, "queued for a later departure");
            Assert.IsTrue(sim.AtPier(t));
            var pos = sim.TruckPos(t);
            Assert.AreEqual(gd.Nodes[pier].X, pos.x, 1e-6);
            Assert.AreEqual(gd.Nodes[pier].Y, pos.y, 1e-6);
            Assert.AreEqual(gd.Graph.Km(gd.DepotNode, pier), sim.S.Today.Km, 1e-6);
            Assert.AreEqual(sim.S.Today.Km * gd.Vehicles["van"].CostKm + gd.FerryTicket, sim.S.Today.Fuel, 1e-6, "fuel + one ticket");
            Assert.AreEqual(1, Of(sim, "ferry_wait").Count);
            // save while queued: the reload continues identically
            var r = SaveCodec.Decode(SaveCodec.Encode(sim), gd);
            Assert.IsTrue(r.Ok, r.Error);
            r.Sim.EmitCrowd = false;
            double posKm = t.PathPos, kmAtPier = sim.S.Today.Km, fuelAtPier = sim.S.Today.Fuel;
            sim.AdvanceMinutes(1);
            Assert.AreEqual(posKm, t.PathPos, 1e-9, "no driving into the sea");
            // the boat: no fuel, no km on the report
            int bad = 0;
            guard = 0;
            while (t.FerryAt > 0 && guard++ < 300)
            {
                sim.AdvanceMinutes(1);
                if (!SaveCodec.Decode(SaveCodec.Encode(sim), gd).Ok) bad++;
            }
            Assert.AreEqual(0, bad, "every minute of the crossing decodes");
            Assert.AreEqual(kmAtPier, sim.S.Today.Km, 1e-9);
            Assert.AreEqual(fuelAtPier, sim.S.Today.Fuel, 1e-9);
            Assert.Greater(t.PathPos, posKm);
            // round trip home: road km both ways, two tickets
            guard = 0;
            while (t.Busy && sim.Phase == Phases.Live && guard++ < 600) sim.AdvanceMinutes(1);
            Assert.IsFalse(t.Busy, "home before the day ends");
            Assert.AreEqual(2 * gd.Graph.RoadKm(gd.DepotNode, k), sim.S.Today.Km, 1e-6);
            Assert.AreEqual(sim.S.Today.Km * gd.Vehicles["van"].CostKm + 2 * gd.FerryTicket, sim.S.Today.Fuel, 1e-6);
            Assert.Greater(sim.S.Points[i].Picked, 0);
            // the reloaded copy plays the same day
            r.Sim.AdvanceMinutes(sim.S.Minute - r.Sim.S.Minute);
            Assert.AreEqual(SaveCodec.Encode(sim), SaveCodec.Encode(r.Sim), "deterministic after a reload at the pier");
        }

        [Test]
        public void RecallAtThePierTurnsBackWithoutCrossing()
        {
            var (sim, i) = Island();
            sim.StartDay();
            sim.Dispatch(0, new List<int> { i });
            var t = sim.S.Trucks[0];
            int guard = 0;
            while (!sim.AtPier(t) && guard++ < 300) sim.AdvanceMinutes(1);
            Assert.IsTrue(sim.AtPier(t));
            Assert.AreEqual("", sim.Recall(0));
            Assert.AreEqual(0, t.FerryAt, 1e-9);
            Assert.AreEqual(sim.Gd.Node("r_pier"), t.AtNode);
            guard = 0;
            while (t.Busy && sim.Phase == Phases.Live && guard++ < 600) sim.AdvanceMinutes(1);
            Assert.AreEqual(0, sim.S.Points[i].Picked, "never reached the island");
        }

        // ------------------------------------------------------------------------------------------------ saves

        [Test]
        public void ASaveFromBeforeChapters5To7StillLoads()
        {
            // an older career has none of the chapter 5-7 fields: tenders, tender id, season flag, bulk centre, ferry clock
            var sim = TestData.NewSim(11);
            sim.StartDay();
            sim.Dispatch(0, new List<int> { 0, 1, 2 });
            sim.AdvanceMinutes(37);
            var o = sim.Snapshot();
            o.Remove("tenders"); o.Remove("tender_id"); o.Remove("season");
            ((JObj)o["depot"]).Remove("bulk_centre");
            foreach (var t in (List<object>)o["trucks"]) ((JObj)t).Remove("ferry");
            string old = Json.Write(o);
            StringAssert.DoesNotContain("tender", old);
            StringAssert.DoesNotContain("ferry\":", old);
            var r = SaveCodec.Decode(old, sim.Gd);
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(0, r.Sim.S.Tenders.Count);
            Assert.AreEqual(1, r.Sim.S.NextTenderId);
            Assert.IsFalse(r.Sim.S.SeasonOn);
            Assert.IsFalse(r.Sim.S.Depot.BulkCentre);
            Assert.AreEqual(SaveCodec.Encode(sim), SaveCodec.Encode(r.Sim), "reads back to the same career");
            r.Sim.EmitCrowd = false;
            sim.AdvanceMinutes(100);
            r.Sim.AdvanceMinutes(100);
            Assert.AreEqual(SaveCodec.Encode(sim), SaveCodec.Encode(r.Sim));
        }

        [Test]
        public void TendersRoundTripAndBadOnesAreRefused()
        {
            var sim = Open("industrial");
            var t = sim.OfferTender(true);
            sim.AcceptTender(t.Id);
            sim.Buy("bulk_centre");
            string a = SaveCodec.Encode(sim);
            var r = SaveCodec.Decode(a, sim.Gd);
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(a, SaveCodec.Encode(r.Sim));
            Assert.IsTrue(r.Sim.S.Depot.BulkCentre);
            Assert.AreEqual(t.Id + 1, r.Sim.S.NextTenderId);
            Assert.IsFalse(SaveCodec.Decode(a.Replace("\"mat\":" + t.Mat, "\"mat\":9"), sim.Gd).Ok);
        }
    }
}
