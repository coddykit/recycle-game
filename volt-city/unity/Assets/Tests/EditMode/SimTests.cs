// Volt City P1 sim tests (Unity Test Framework, EditMode, NUnit). UNTESTED until Unity is installed on the Mini:
// the same scenarios passed in the P1 reference model (see docs/P1-VALIDATION.md).
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using VoltCity.Sim;

namespace VoltCity.Sim.Tests
{
    public class SimTests
    {
        static GameData gd;

        public static string DataDir()
        {
            string env = Environment.GetEnvironmentVariable("VOLTCITY_DATA");
            if (!string.IsNullOrEmpty(env)) return env;
            return Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Resources", "data");
        }

        [OneTimeSetUp]
        public void Load()
        {
            gd = GameData.LoadDirectory(DataDir());
            Assert.IsTrue(gd.Ok, string.Join("; ", gd.Errors));
        }

        static Simulation Live(long seed = 5, int days = 1, string weather = null, List<string> force = null)
        {
            var sim = SimProbe.CareerAfterDays(gd, seed, days);
            Assert.IsTrue(sim.StartShift(weather, force));
            return sim;
        }

        // ------------------------------------------------------------------------------------------- data

        [Test]
        public void PausedClock_PlayerOrderStillLands()
        {
            // device pass 10-28: a "+" tapped under a guide tip (clock stopped) did nothing until the tip closed — the row
            // kept the old setpoint and the assist asked for "+" again. Orders now land at once while paused.
            var sim = Live();
            var g = sim.Grid;
            int p = Grid.PSteam;
            int before = g.SetpointAfterPending(p);
            sim.SetPaused(true);
            Assert.IsTrue(sim.Queue(Cmd.Ramp("steam", +1)).Ok);
            int step = g.Step;
            sim.Advance(0.1);
            Assert.AreEqual(step, g.Step, "the clock stays stopped");
            Assert.AreEqual(Math.Min(10, before + 1), g.SetpointAfterPending(p), "the order counts while paused (row + assist projection)");
            Assert.AreEqual(0, sim.S.Pending.Count);
        }

        [Test]
        public void Data_TablesAndCurve()
        {
            Assert.AreEqual(8, gd.Districts.Count);
            Assert.AreEqual(50, gd.Investments.Count);
            Assert.AreEqual(15, gd.Events.Count);
            Assert.AreEqual(5, gd.Chapters.Count);
            Assert.AreEqual(8, gd.Corridors.Count);
            Assert.AreEqual(3, gd.Ties.Count);
            foreach (var p in gd.PlantDefs) Assert.AreEqual(3, p.CapacityMw.Length, p.Id);
            Assert.AreEqual(420, gd.Investments[0].Cost);
            Assert.AreEqual((long)Math.Round(420 * Math.Pow(1.15, 49)), gd.Investments[49].Cost);
            long total = 0;
            foreach (var x in gd.Investments) total += x.Cost;
            Assert.That(total, Is.InRange(3000000L, 3060000L));
            Assert.AreEqual("hospital_hill", gd.Districts[6].Id);
            int maxPrio = 0;
            foreach (var d in gd.Districts) maxPrio = Math.Max(maxPrio, d.Priority);
            Assert.AreEqual(maxPrio, gd.District["hospital_hill"].Priority, "Hospital Hill is shed last");
            Assert.AreEqual(960, gd.Steps);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, gd.Speeds);
            var kinds = new HashSet<string>();
            foreach (var x in gd.Investments) kinds.Add(x.Kind);
            foreach (var k in new[] { "plant", "line", "substation", "crew_truck", "crew_speed", "tree_trim", "dr", "weather_station", "dispatcher" })
                Assert.IsTrue(kinds.Contains(k), k);
            Assert.AreEqual(23, gd.IapProducts.Count, "IAP.md 24 minus `scenarios` (reserved until its content ships, P4)");
            Assert.AreEqual(25, gd.CrewNames.Count);
        }

        [Test]
        public void Rng_SameSeedSameStream()
        {
            var a = new Rng(42);
            var b = new Rng(42);
            for (int i = 0; i < 1000; i++) Assert.AreEqual(a.Next(), b.Next());
            Assert.AreNotEqual(Rng.Mix(1, 2), Rng.Mix(1, 3));
            var c = new Rng(7);
            for (int i = 0; i < 1000; i++) { double f = c.Frand(); Assert.That(f, Is.InRange(0.0, 1.0)); Assert.Less(f, 1.0); }
        }

        [Test]
        public void Json_IntegersRoundTripExactly()
        {
            var o = new JObj { ["a"] = 9007199254740991L, ["b"] = -5L, ["c"] = 0.1, ["d"] = new List<object> { 1L, "x", true, null } };
            var back = (JObj)Json.Parse(Json.Write(o));
            Assert.AreEqual(9007199254740991L, back["a"]);
            Assert.AreEqual(-5L, back["b"]);
            Assert.AreEqual(0.1, (double)back["c"]);
            Assert.AreEqual(Json.Write(o), Json.Write(back));
            Assert.IsNull(Json.TryParse("{\"a\":"));
        }

        // ------------------------------------------------------------------------------------------- determinism + fairness

        [Test]
        public void Determinism_InProcess()
        {
            string h1 = SimProbe.ScriptedShiftHash(gd, 77);
            Assert.AreEqual(h1, SimProbe.ScriptedShiftHash(gd, 77));
            Assert.AreNotEqual(h1, SimProbe.ScriptedShiftHash(gd, 78));
        }

        /// <summary>Run the EditMode suite in two Unity processes (or `BalanceRunner.Cli` twice): the second process compares
        /// with the hash the first one left in the temp folder.</summary>
        [Test]
        public void Determinism_AcrossProcesses()
        {
            string h = SimProbe.ScriptedShiftHash(gd, 77);
            // Compare processes of the same compiled sim, never a stale hash from a different code revision.
            string path = Path.Combine(Path.GetTempPath(), "voltcity_p1_shift_hash_" + gd.Fingerprint + "_" + typeof(SimTests).Module.ModuleVersionId + ".txt");
            int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
            if (File.Exists(path))
            {
                var parts = File.ReadAllText(path).Split(' ');
                if (parts.Length == 2 && parts[0] != pid.ToString()) Assert.AreEqual(parts[1], h, "hash differs from process " + parts[0]);
            }
            File.WriteAllText(path, pid + " " + h);
        }

        [Test]
        public void FixedStep_TickSizeDoesNotMatter()
        {
            string Run(double dt)
            {
                var sim = Live(9);
                while (sim.Phase == "live") sim.Advance(dt);
                return sim.S.Report.Hash;
            }
            string a = Run(0.25);
            Assert.AreEqual(a, Run(0.016));
            Assert.AreEqual(a, Run(1.7));
        }

        [Test]
        public void Fairness_TapsInsideOneStepWindowGiveOneResult()
        {
            string Tap(double offset)
            {
                var sim = Live(5);
                sim.Advance(25.0);
                sim.Advance(offset);
                var q = sim.Queue(Cmd.Ramp("steam", 1));
                Assert.IsTrue(q.Ok);
                Assert.AreEqual(gd.Plant["steam"].DelayS, q.EffectInS);
                while (sim.Phase == "live") sim.Advance(0.25);
                return sim.S.Report.Hash;
            }
            Assert.AreEqual(Tap(0.01), Tap(0.2));
            Assert.AreNotEqual(Tap(0.01), Tap(0.3));
        }

        [Test]
        public void Speeds_IntegerCompareAndPerk()
        {
            var sim = new Simulation(gd, 1);
            Assert.IsTrue(sim.SetSpeed(2)); Assert.AreEqual(2, sim.S.Speed);
            Assert.IsTrue(sim.SetSpeed(3)); Assert.AreEqual(3, sim.S.Speed);
            Assert.IsFalse(sim.SetSpeed(4)); Assert.AreEqual(3, sim.S.Speed);
            Assert.IsFalse(sim.SetSpeed(5));
            sim.SetCommercePerks(new Dictionary<string, bool> { ["speed4"] = true });
            Assert.IsTrue(sim.SetSpeed(4)); Assert.AreEqual(4, sim.S.Speed);
            sim.SetCommercePerks(null);
            Assert.AreEqual(1, sim.S.Speed, "speed 4 drops when the perk is gone");
            // 2x runs the shift in half the real time
            sim.StartShift();
            sim.SetSpeed(2);
            sim.Advance(10.0);
            Assert.AreEqual(80, sim.Grid.Step);
        }

        // ------------------------------------------------------------------------------------------- grid

        [Test]
        public void Tutorial_SafeShiftLosesNothing()
        {
            var sim = SimProbe.CareerAfterDays(gd, 3, 1);
            sim.TutorialSafe = true;
            sim.StartShift("rainy", new List<string> { "thunderstorm", "plant_trip", "squirrel" });
            Assert.IsTrue(sim.Queue(Cmd.ScriptFault("c_riverside")).Ok);
            sim.RunSteps(1);
            Assert.AreEqual(1, sim.Grid.OpenFaults().Count, "the guide's scripted spark");
            // nobody operates: still no auto shedding, no trips, no planned faults
            sim.RunSteps(2000);
            Assert.AreEqual(0, sim.S.Report.Shed);
            Assert.AreEqual(0, sim.S.Report.Trips);
            Assert.AreEqual(1, sim.S.Report.Faults);
            var s2 = Live(3);
            s2.Queue(Cmd.ScriptFault("c_riverside"));
            s2.RunSteps(1);
            Assert.AreEqual(0, s2.Grid.OpenFaults().Count, "script_fault only in the guide");
        }

        [Test]
        public void Shedding_LowestPriorityFirst()
        {
            // the whole city on one Old Smoky L1: the relays drop districts in priority order, never the hospital
            var sim = new Simulation(gd, 11);
            foreach (var d in gd.Districts) sim.S.Hooked[d.Id] = 0;
            sim.S.Chapter = 5;
            sim.StartShift("sunny", new List<string>());
            var order = new List<string>();
            while (sim.Phase == "live" && order.Count < 3)
            {
                sim.RunSteps(1);
                foreach (var e in sim.DrainEvents()) if (e.Type == "shed" && J.B(e.Data, "auto")) order.Add(e.Str("district"));
            }
            CollectionAssert.AreEqual(new[] { "stadium_row", "hilltop", "old_harbor" }, order);
            Assert.AreEqual(Grid.Lit, sim.Grid.DState[Array.IndexOf(sim.Grid.DIds, "hospital_hill")]);
        }

        [Test]
        public void Shedding_HospitalLastAfterSixtySecondBackup()
        {
            var sim = new Simulation(gd, 12);
            sim.S.Hooked.Clear();
            sim.S.Hooked["riverside"] = 0;
            sim.S.Hooked["hospital_hill"] = 0;
            sim.S.Carry.Steps["steam"] = 0;                      // nobody ramps: 11 MW against ~30 MW
            sim.StartShift("sunny", new List<string>());
            var seen = new List<string>();
            int backupAt = -1, endAt = -1;
            while (sim.Phase == "live" && endAt < 0)
            {
                sim.RunSteps(1);
                foreach (var e in sim.DrainEvents())
                {
                    if (e.Type == "shed") seen.Add(e.Str("district"));
                    if (e.Type == "backup_on") { backupAt = e.Step; seen.Add("backup"); }
                    if (e.Type == "backup_end") endAt = e.Step;
                }
            }
            Assert.AreEqual("riverside", seen[0], "the hospital goes last");
            Assert.IsTrue(seen.Contains("backup"));
            Assert.IsFalse(seen.Contains("hospital_hill"), "never shed while it has its backup");
            Assert.AreEqual(gd.HospitalBackupS * 4, endAt - backupAt, "backup generator runs 60 s");
        }

        [Test]
        public void Lines_OverheatTrip()
        {
            // Riverside grown +24 % on its base line in a heatwave evening: the line overheats and trips (dark until it recloses)
            var sim = new Simulation(gd, 21);
            sim.S.Plants["gas"] = 3; sim.S.Plants["steam"] = 3;
            sim.S.Hooked["riverside"] = 30;
            sim.StartShift("hot", new List<string> { "heatwave" });
            int dark = 0;
            bool tripped = false;
            int ri = Array.IndexOf(sim.Grid.DIds, "riverside");
            while (sim.Phase == "live")
            {
                SimProbe.DriveStep(sim);
                if (sim.Phase != "live") break;
                if (!sim.Grid.IsLit(ri)) dark++;
                foreach (var e in sim.DrainEvents()) if (e.Type == "line_trip" && e.Str("corridor") == "c_riverside") tripped = true;
            }
            Assert.IsTrue(tripped, "base line overheats");
            Assert.Greater(dark, 0);
        }

        [Test]
        public void Lines_RerouteThroughSmartSubstation()
        {
            bool LitDuringFault(bool tie)
            {
                var sim = new Simulation(gd, 22);
                if (tie) { sim.S.Ties.Add("sub_1"); sim.S.Lines["c_harbor"] = 2; }   // the partner line needs room
                sim.TutorialSafe = true;
                sim.StartShift("sunny", new List<string>());
                sim.Queue(Cmd.ScriptFault("c_riverside"));
                sim.RunSteps(2);
                return sim.Grid.IsLit(Array.IndexOf(sim.Grid.DIds, "riverside"));
            }
            Assert.IsFalse(LitDuringFault(false), "radial line down = district dark");
            Assert.IsTrue(LitDuringFault(true), "smart substation feeds it through the Old Harbor line");
        }

        [Test]
        public void Crews_TravelByRoadAndRepair()
        {
            var sim = new Simulation(gd, 4);                       // fresh city: no smart substation can reroute Old Harbor
            sim.TutorialSafe = true;                              // nobody operates: the guide keeps the grid safe meanwhile
            sim.StartShift("sunny", new List<string>());
            var g = sim.Grid;
            sim.Queue(Cmd.ScriptFault("c_harbor"));
            sim.RunSteps(1);
            var f = g.OpenFaults()[0];
            Assert.IsFalse(g.IsLit(Array.IndexOf(g.DIds, "old_harbor")));
            sim.Queue(Cmd.SendCrew(f.Id));
            int applyStep = g.Step;
            sim.RunSteps(1);
            var cr = g.Crews[f.Crew];
            int travel = g.TravelSteps(gd.Depot, f.Site);
            Assert.AreEqual(Math.Max(1, (int)Math.Ceiling(gd.RoadDistance(gd.Depot, f.Site) / (gd.CrewSpeedMps * g.CrewSpeedPct / 100.0) * 4.0)), travel);
            Assert.AreEqual(CrewTruck.Travel, cr.State);
            Assert.AreEqual(applyStep + travel, cr.Until);
            sim.RunSteps(travel + f.Repair + 2);
            Assert.IsFalse(f.Open);
            Assert.IsTrue(g.IsLit(Array.IndexOf(g.DIds, "old_harbor")));
            Assert.Greater(sim.DrainEvents().FindAll(e => e.Type == "repaired").Count, 0);
        }

        [Test]
        public void Events_CapsAndAntiTilt()
        {
            for (int n = 3; n < 600; n++)
            {
                var ctx = new PlanContext { Chapter = 1 + n % 5, ChapterShift = n % 13, LastStars = n % 3 == 0 ? 1 : 3 };
                foreach (var d in gd.Districts) ctx.Hooked.Add(d.Id);
                foreach (var p in GameData.Plants) ctx.Plants[p] = 2;
                var plan = Planner.Make(gd, 99, n, Planner.WeatherKindFor(gd, 99, n, "sunny"), ctx);
                Assert.LessOrEqual(Planner.NegativeCount(gd, plan), gd.MaxNegative, "shift " + n);
                if (ctx.LastStars <= 1) Assert.IsFalse(plan.Has("thunderstorm"), "no storm after a 1-star shift");
                var corr = new HashSet<string>();
                foreach (var f in plan.Faults) if (f.Kind == "line") Assert.IsTrue(corr.Add(f.Corridor), "two faults on one corridor");
            }
            for (int n = 0; n < gd.TutorialShifts; n++)
            {
                var ctx = new PlanContext { Chapter = 3 };
                ctx.Hooked.Add("old_harbor");
                var plan = Planner.Make(gd, 5, n, "sunny", ctx);
                Assert.AreEqual(0, Planner.NegativeCount(gd, plan), "tutorial shifts are calm");
            }
            // forecast = what happens (same career state)
            var sim = new Simulation(gd, 31);
            sim.S.ShiftNo = 5; sim.S.Chapter = 2;
            var fc = sim.Forecast()[0];
            sim.StartShift();
            Assert.AreEqual(Json.Write(fc.ToJson()), Json.Write(sim.S.Plan.ToJson()));
        }

        // ------------------------------------------------------------------------------------------- economy

        [Test]
        public void Economy_PrerequisitesHookupsChapters()
        {
            var sim = new Simulation(gd, 2);
            sim.S.Cash = 10000000;
            Assert.AreEqual("needs_district", Economy.WhyNot(sim, "wind_1"));
            Assert.AreEqual("locked_prev", Economy.WhyNot(sim, "gas_2"));
            Assert.AreEqual("needs_chapter", Economy.WhyNot(sim, "auto_baseload"));
            Assert.IsTrue(sim.Buy("gas_1"));
            Assert.AreEqual("owned", Economy.WhyNot(sim, "gas_1"));
            sim.S.RatingX100 = 150;
            Assert.AreEqual("rating", Economy.HookupWhyNot(sim, "hilltop"));
            Assert.AreEqual("locked_prev", Economy.HookupWhyNot(sim, "market_square"));
            sim.S.RatingX100 = 200;
            Assert.IsTrue(sim.Hookup("hilltop"));
            Assert.AreEqual(2, sim.S.Chapter);
            Assert.AreEqual("", Economy.WhyNot(sim, "wind_1"));
            long p0 = Economy.PriceMilli(sim);
            sim.Buy("wind_1");
            Assert.Greater(Economy.PriceMilli(sim), p0, "every investment raises the contract level");
            sim.SetCommercePerks(new Dictionary<string, bool> { ["dispatcher"] = true });
            Assert.AreEqual("", Economy.WhyNot(sim, "auto_baseload"), "the paid perk unlocks the assistant early");
            sim.StartShift();
            Assert.AreEqual("live", Economy.WhyNot(sim, "gas_2"), "no building during a live shift");
        }

        [Test]
        public void Offline_CappedAndPlus()
        {
            var sim = new Simulation(gd, 3);
            sim.S.LastNet = 1000;
            sim.Touch(BalanceRunner.Epoch);
            var o = sim.Offline(BalanceRunner.Epoch + 30 * 3600);
            Assert.IsTrue(o.Capped);
            Assert.AreEqual(8 * 3600, o.CountedS);
            Assert.AreEqual(700, o.Cash, "0.7 of one shift's net at the 8 h cap");
            sim.SetCommercePerks(new Dictionary<string, bool> { ["offline_plus"] = true });
            var p = sim.Offline(BalanceRunner.Epoch + 60 * 3600);
            Assert.AreEqual(16 * 3600, p.CountedS);
            Assert.AreEqual(2100, p.Cash, "16 h x1.5");
            Assert.IsNull(sim.Offline(BalanceRunner.Epoch + 60 * 3600 + 10), "under a minute pays nothing");
        }

        [Test]
        public void Rewards_CappedPersistedAndContextual()
        {
            var sim = SimProbe.CareerAfterDays(gd, 6, 1);
            long now = BalanceRunner.Epoch + 10 * 86400;
            long day = Economy.DayIncome(sim);
            Assert.AreEqual(Math.Max(gd.DayIncomeFloor, (long)Math.Round(sim.S.LastNet * 6.7, MidpointRounding.AwayFromZero)), day);
            Assert.IsFalse(sim.RewardOffer("shift_bonus", now).Ok, "only at the report");
            Policies.PlayShift(sim, "greedy");
            var off = sim.RewardOffer("shift_bonus", now);
            Assert.IsTrue(off.Ok);
            Assert.LessOrEqual(off.Amount, Economy.RewardCapSingle(sim), "single reward <= 1/5 of cash_s");
            Assert.IsTrue(sim.GrantReward("shift_bonus", now).Ok);
            Assert.IsFalse(sim.RewardOffer("shift_bonus", now).Ok, "once per report");
            sim.Collect();
            Policies.PlayShift(sim, "greedy");
            Assert.AreEqual("cooldown", sim.RewardOffer("shift_bonus", now + 60).Reason);
            // persisted: a reload keeps today's count and cash total
            var back = SaveCodec.Decode(SaveCodec.Encode(sim), gd);
            Assert.IsTrue(back.Ok, back.Error);
            Assert.AreEqual(1, back.Sim.S.Rewards.N["shift_bonus"]);
            long total = 0;
            for (int k = 0; k < 10; k++)
            {
                long t = now + 700 * (k + 1);
                if (back.Sim.Phase != "report") { back.Sim.Collect(); Policies.PlayShift(back.Sim, "greedy"); }
                var r = back.Sim.GrantReward("shift_bonus", t);
                if (r.Ok) total += r.Amount;
            }
            Assert.LessOrEqual(back.Sim.S.Rewards.CashToday, Economy.RewardCapDaily(back.Sim) + 1, "daily sum <= 1/2 of cash_s");
            Assert.LessOrEqual(back.Sim.S.Rewards.N["shift_bonus"], gd.Placements["shift_bonus"].Cap);
            Assert.IsFalse(sim.RewardOffer("emergency_import", now).Ok, "non-cash rewards only in a live deficit");
            Assert.AreEqual("unknown", sim.RewardOffer("free_premium", now).Reason, "nothing sold is ever a reward");
        }

        [Test]
        public void Store_FundsIdempotentAndPerks()
        {
            var sim = SimProbe.CareerAfterDays(gd, 8, 1);
            long cash = sim.S.Cash;
            long day = sim.DayIncome();
            var a = sim.GrantPurchaseFunds("GPA.1234-5678", "cash_m");
            Assert.IsTrue(a.Ok);
            Assert.AreEqual((long)Math.Round(day * 2.5, MidpointRounding.AwayFromZero), a.Granted);
            var b = sim.GrantPurchaseFunds("GPA.1234-5678", "cash_m");
            Assert.IsTrue(b.Duplicate);
            Assert.AreEqual(0, b.Granted);
            Assert.AreEqual(cash + a.Granted, sim.S.Cash);
            var back = SaveCodec.Decode(SaveCodec.Encode(sim), gd).Sim;
            Assert.IsTrue(back.GrantPurchaseFunds("GPA.1234-5678", "cash_m").Duplicate, "the receipt survives a reload");
            Assert.IsFalse(sim.GrantPurchaseFunds("GPA.x", "premium").Ok, "permanent products are rights, not cash");
            foreach (var h in sim.S.Receipts) Assert.AreEqual(64, h.Length, "only a hash of the transaction id is kept");
        }

        // ------------------------------------------------------------------------------------------- saves

        [Test]
        public void Save_ReopenLockMidShiftEqualsNeverClosed()
        {
            foreach (int stop in new[] { 1, 37, 400, 701, 959 })
            {
                var a = Live(9, 2, "rainy", new List<string> { "thunderstorm", "squirrel" });
                while (a.Phase == "live" && a.Grid.Step < stop) SimProbe.DriveStep(a);
                string text = SaveCodec.Encode(a);
                var r = SaveCodec.Decode(text, gd);
                Assert.IsTrue(r.Ok, "decode at " + stop + ": " + r.Error);
                Assert.AreEqual(text, SaveCodec.Encode(r.Sim), "re-encode is identical at " + stop);
                var b = r.Sim;
                SimProbe.DriveToEnd(a);
                SimProbe.DriveToEnd(b);
                Assert.AreEqual(a.S.Report.Hash, b.S.Report.Hash, "reopen-lock at step " + stop);
                Assert.AreEqual(a.S.Cash, b.S.Cash);
            }
        }

        [Test]
        public void Save_PreservesRouteCacheAcrossLineTripBoundary()
        {
            var sim = Live(77, 2);
            sim.RunSteps(1);
            int district = Array.FindIndex(sim.Grid.DRoute, r => r >= 0);
            Assert.GreaterOrEqual(district, 0);
            int route = sim.Grid.DRoute[district];
            // A line trips after Routes() within a completed step. The HUD/dispatcher still sees that step's cache.
            sim.Grid.COutUntil[route] = sim.Grid.Step + 20;
            string saved = SaveCodec.Encode(sim);
            var restored = SaveCodec.Decode(saved, gd);
            Assert.IsTrue(restored.Ok, restored.Error);
            CollectionAssert.AreEqual(sim.Grid.DRoute, restored.Sim.Grid.DRoute);
            Assert.AreEqual(sim.Grid.IsLit(district), restored.Sim.Grid.IsLit(district));
            SimProbe.DriveToEnd(sim);
            SimProbe.DriveToEnd(restored.Sim);
            Assert.AreEqual(SaveCodec.Encode(sim), SaveCodec.Encode(restored.Sim));
        }

        [Test]
        public void Save_RouteCacheRejectsForeignRoutesAndAcceptsOldSaves()
        {
            var sim = Live(77, 2);
            sim.RunSteps(40);
            var snapshot = sim.Snapshot();
            var shift = J.Obj(snapshot, "shift");
            shift.Remove("d_route");
            Assert.IsTrue(SaveCodec.Decode(Json.Write(snapshot), gd).Ok, "old saves reconstruct routes");
            shift["d_route"] = new List<object>();
            Assert.IsFalse(SaveCodec.Decode(Json.Write(snapshot), gd).Ok, "wrong array length");
            var routes = new List<object>();
            foreach (var route in sim.Grid.DRoute) routes.Add((long)route);
            routes[0] = 999L;
            shift["d_route"] = routes;
            Assert.IsFalse(SaveCodec.Decode(Json.Write(snapshot), gd).Ok, "out-of-range route");
        }

        [Test]
        public void Save_AcceptsEveryPhaseAndPendingTap()
        {
            var sim = SimProbe.CareerAfterDays(gd, 12, 1);
            Assert.IsTrue(SaveCodec.Decode(SaveCodec.Encode(sim), gd).Ok, "idle");
            sim.S.Cash = -5000;                                   // a bad shift can leave the city in debt
            Assert.IsTrue(SaveCodec.Decode(SaveCodec.Encode(sim), gd).Ok, "negative cash");
            sim.S.Cash = 100000;
            sim.StartShift();
            sim.Queue(Cmd.Ramp("steam", 1));                      // a tap waiting for the next step
            var r = SaveCodec.Decode(SaveCodec.Encode(sim), gd);
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(1, r.Sim.S.Pending.Count);
            SimProbe.DriveToEnd(sim);
            Assert.IsTrue(SaveCodec.Decode(SaveCodec.Encode(sim), gd).Ok, "report");
        }

        [Test]
        public void Save_FuzzZeroUnreadable()
        {
            int saves = 0, bad = 0;
            string firstError = "";
            var dts = new[] { 0.016, 0.017, 0.033, 0.016, 0.05, 0.25, 0.034 };
            for (int seed = 1; seed <= 4; seed++)
            {
                var sim = new Simulation(gd, seed * 7919 + 13);
                var rnd = new Random(seed);
                long now = BalanceRunner.Epoch;
                int frames = 0;
                while (sim.S.ShiftNo < 10 && frames < 200000)
                {
                    switch (sim.Phase)
                    {
                        case "idle":
                            Policies.BuyPhase(sim, "greedy");
                            sim.TutorialSafe = sim.S.ShiftNo == 0;
                            sim.StartShift();
                            break;
                        case "report":
                            if (rnd.Next(3) == 0) sim.GrantReward("shift_bonus", now);
                            sim.Collect();
                            break;
                        default:
                            if (frames % 30 == 0) foreach (var c in Dispatcher.Operate(sim, Policies.OperatorFor("greedy"))) sim.Queue(c);
                            sim.SetSpeed(1 + frames / 997 % 3);
                            sim.Advance(dts[frames % dts.Length]);
                            break;
                    }
                    frames++;
                    now += 1;
                    if (frames % 4000 == 0) { now += rnd.Next(30, 30000); sim.Offline(now); }
                    if (frames % 11 == 0)
                    {
                        saves++;
                        var r = SaveCodec.Decode(SaveCodec.Encode(sim), gd);
                        if (!r.Ok) { bad++; if (firstError == "") firstError = r.Error; }
                    }
                }
            }
            TestContext.WriteLine($"SAVE_FUZZ: {bad} / {saves} saves unreadable");
            Assert.Greater(saves, 1000);
            Assert.AreEqual(0, bad, "unreadable saves: " + firstError);
        }

        [Test]
        public void Save_GarbageIsRefusedNeverThrows()
        {
            string good = SaveCodec.Encode(SimProbe.CareerAfterDays(gd, 1, 1));
            var rnd = new Random(5);
            foreach (var bad in new[] { "", "null", "[]", "{}", "{\"schema\":99}", good.Substring(0, good.Length / 2), good.Replace("\"cash\":", "\"cash\":\"x\",\"_\":") })
                Assert.IsFalse(SaveCodec.Decode(bad, gd).Ok, bad.Length > 40 ? bad.Substring(0, 40) : bad);
            for (int k = 0; k < 300; k++)
            {
                var chars = good.ToCharArray();
                for (int m = 0; m < 3; m++) chars[rnd.Next(chars.Length)] = "0123456789{}[]\",:x-"[rnd.Next(19)];
                Assert.DoesNotThrow(() => SaveCodec.Decode(new string(chars), gd));
            }
        }

        // ------------------------------------------------------------------------------------------- review gate + misc

        [Test]
        public void Review_FirstShiftAndFirstRestoreMoments()
        {
            var sim = new Simulation(gd, 14);
            sim.TutorialSafe = true;
            sim.StartShift();
            sim.Queue(Cmd.ScriptFault("c_riverside"));
            var moments = new List<SimEvent>();
            int sent = 0;
            while (sim.Phase == "live")
            {
                if (sent == 0 && sim.Grid.OpenFaults().Count > 0) { sim.Queue(Cmd.SendCrew(sim.Grid.OpenFaults()[0].Id)); sent = 1; }
                if (sim.Grid != null && sim.Grid.Step > 100) sim.TutorialSafe = false;
                sim.Advance(0.25);
                moments.AddRange(sim.DrainEvents().FindAll(e => e.Type == "review_moment"));
            }
            Assert.AreEqual(2, moments.Count);
            Assert.AreEqual("first_district_restored", moments[0].Str("reason"));
            Assert.AreEqual("first_shift_completed", moments[1].Str("reason"));
            Assert.That(moments[1].Num("play_s"), Is.InRange(239L, 241L), "shift 1 ends ~4 min in at 1x");
            Assert.IsTrue(J.B(moments[1].Data, "ready"));
            sim.Collect();
            Policies.PlayShift(sim, "greedy");
            Assert.AreEqual(0, sim.DrainEvents().FindAll(e => e.Type == "review_moment").Count, "once per career");
        }

        [Test]
        public void Ascii_FrameAndStrip()
        {
            var sim = Live(2, 1, "rainy", new List<string> { "thunderstorm" });
            sim.RunSteps(500);
            string f = AsciiFrame.Frame(sim);
            StringAssert.Contains("NEEDLE", f);
            StringAssert.Contains("DISTRICTS", f);
            SimProbe.DriveToEnd(sim);
            Assert.AreEqual("(no live shift: report)", AsciiFrame.Frame(sim));
        }

        /// <summary>DESIGN section 7 gates, quick sample (the full 1,000-seed run is BalanceRunner.Cli "full" + "merge").</summary>
        [Test, Category("Balance"), Timeout(3600000)]
        public void Balance_QuickGates()
        {
            var log = new System.Text.StringBuilder();
            var gates = BalanceRunner.Evaluate(gd, BalanceRunner.Careers(gd, 6), BalanceRunner.Matrix(gd, 3), log);
            TestContext.WriteLine(log.ToString());
            foreach (var g in gates) Assert.IsTrue(g.Ok, g.Label);
        }
    }
}
