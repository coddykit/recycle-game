// Semester Rush — game layer EditMode tests (NUnit; also under plain .NET via Tools/dotnet_check.sh).
//   guide: every step advances only on its action, wrong taps never advance, skip -> replay, relaunch mid-step recovers,
//   the whole guide completes; saves: relaunch keeps the career, unreadable files are kept; shop: ad_free only from noads
//   products, cheap packs never, consumables once per transaction; rewards: kit caps = balance caps; analytics catalogue;
//   strings: every key the code uses exists, placeholders match in all 10 languages, IAP catalogue in sync.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CoddyKit;
using NUnit.Framework;
using SemesterRush.Sim;
using SemesterRush.Sim.Tests;

namespace SemesterRush.Game.Tests
{
    static class G
    {
        public static string TempDir()
        {
            string d = Path.Combine(Path.GetTempPath(), "br-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(d);
            return d;
        }

        public static (GameSession s, GameActions a, Coach c) NewGuide(string dir = null)
        {
            var s = new GameSession(TestData.Gd(), dir ?? TempDir()) { Now = () => 1_800_000_000 };
            s.LoadOrNew(5);
            var a = new GameActions(s);
            var c = new Coach { Persist = (step, done) => { s.Settings.CoachStep = step; s.Settings.CoachDone = done; s.Settings.Save(); } };
            a.Allowed = c.Allows;
            a.Ran += (id, r) => c.OnAction(s.Sim, a, id, r);
            c.Begin(s.Sim, a, s.Settings.CoachStep);
            return (s, a, c);
        }

        /// <summary>Lets the world catch up for a guide step that waits for it (the first bell, the first class, the week).</summary>
        public static void Run(Coach c, GameSession s, GameActions a, int frames = 2000)
        {
            for (int i = 0; i < frames && c.Active && c.FastForward(s.Sim); i++) c.Drive(s.Sim, a, 0.1);
            c.Drive(s.Sim, a, 0.05);
        }

        public static Loc Loc() => SemesterRush.Game.Loc.FromCsv(TestData.Read("Assets/Resources/i18n/strings.csv"));
    }

    public class GuideTests
    {
        /// <summary>The right action for each step (filled from the career: hall node, guide course, gate edge, lunch fix).</summary>
        static string Right(int step, GameSession s, Coach c)
        {
            var sim = s.Sim;
            int hall = sim.MainHall(), g = sim.Gd.CourseIndex(sim.Gd.GuideCourse);
            switch (step)
            {
                case 1: return "hall:" + hall;
                case 2: return "cell:0:0";
                case 3: return "course:" + g;
                case 4: return "prof:" + g + ":0";
                case 5: return "start_week";
                case 7: return "gate:" + Coach.GuideGate(sim);
                case 8: return "till";
                case 9: return "buy:build:" + Coach.LibrarySite(sim);
                case 10: { var f = c.Fix; return f == null ? "" : "move:" + f.Placement + ":" + f.Day + ":" + f.Slot + ":" + f.Node; }
                case 11: return "report";
            }
            return "";
        }

        [Test]
        public void TheWholeGuideCompletesWithTheRightActions()
        {
            var (s, a, c) = G.NewGuide();
            var events = new List<string>();
            var steps = new List<int>();
            c.Analytics = (n, p) => { events.Add(n); if (n == "onboarding_step") steps.Add((int)(long)p["step"]); };
            int guard = 0;
            while (c.Active && guard++ < 40)
            {
                int step = c.Step;
                if (c.FastForward(s.Sim) || step == 6) { G.Run(c, s, a); continue; }
                string act = Right(step, s, c);
                Assert.AreNotEqual("", act, "step " + step + " has an action");
                var r = a.Run(act);
                Assert.IsTrue(r.Ok, "step " + step + " action " + act + " failed: " + r.Error);
                G.Run(c, s, a);
            }
            Assert.IsFalse(c.Active, "guide finished at step " + c.Step);
            CollectionAssert.AreEqual(Enumerable.Range(1, Coach.Steps).ToArray(), steps.ToArray(), "every step, in order, none skipped");
            Assert.IsFalse(s.Sim.S.Tutorial, "crises may start after the guide");
            Assert.IsTrue(s.Settings.CoachDone);
            Assert.AreEqual(Phases.Report, s.Sim.Phase, "the guide ends on the first week's grades");
            Assert.IsTrue(s.Sim.S.OpenGates.Contains(Coach.GuideGate(s.Sim)));
            Assert.IsNotNull(s.Sim.BuildingAt(s.Sim.Gd.Node(Coach.LibrarySite(s.Sim))));
            Assert.Greater(s.Sim.S.Prestige, s.Gd.StartPrestige, "the first rise of prestige");
            Assert.Contains("onboarding_complete", events);
        }

        [Test]
        public void WrongTapsNeverAdvanceAndRepeatTheHint()
        {
            var (s, a, c) = G.NewGuide();
            Assert.AreEqual(1, c.Step);
            var r = a.Run("start_week");
            Assert.IsFalse(r.Ok);
            Assert.AreEqual("blocked", r.Error);
            Assert.AreEqual(1, c.Step);
            Assert.AreEqual(1, c.WrongTaps);
            Assert.AreEqual(1, c.Flash);
            Assert.IsFalse(a.Run("buy:staff:marshals").Ok, "no shortcuts during the guide");
            Assert.IsTrue(a.Run("site:" + s.Sim.Gd.Node("site_library")).Ok, "info cards stay open to look at");
            Assert.AreEqual(1, c.Step);
            Assert.IsTrue(a.Run(Right(1, s, c)).Ok);
            Assert.AreEqual(2, c.Step);
            Assert.IsFalse(a.Run("cell:2:3").Ok, "only Monday 9:00");
            Assert.AreEqual(2, c.Step);
        }

        [Test]
        public void EveryStepHasATargetAndABubbleText()
        {
            var loc = G.Loc();
            for (int i = 1; i <= Coach.Steps; i++)
            {
                Assert.IsFalse(string.IsNullOrEmpty(Coach.Targets[i]), "target " + i);
                Assert.IsTrue(loc.Has("coach." + i), "coach." + i);
            }
        }

        [Test]
        public void TimeIsPausedWhileTheGuideWaitsForThePlayer()
        {
            var (s, a, c) = G.NewGuide();
            for (int i = 1; i <= 5; i++) a.Run(Right(i, s, c));
            Assert.AreEqual(6, c.Step);
            Assert.IsTrue(c.FastForward(s.Sim), "the first bell is watched");
            G.Run(c, s, a);
            Assert.AreEqual(7, c.Step);
            double m = s.Sim.S.Minute;
            c.Drive(s.Sim, a, 1.0);
            s.Sim.Advance(1.0);
            Assert.AreEqual(m, s.Sim.S.Minute, 1e-9, "paused at the gate step");
        }

        [Test]
        public void SkipThenReplayFromSettings()
        {
            var (s, a, c) = G.NewGuide();
            var events = new List<string>();
            c.Analytics = (n, p) => events.Add(n);
            a.Run(Right(1, s, c));
            c.Skip(s.Sim);
            Assert.IsFalse(c.Active);
            Assert.IsTrue(s.Settings.CoachDone);
            Assert.Contains("onboarding_skip", events);
            Assert.IsTrue(a.Run("buy:staff:marshals").Ok, "everything is allowed after a skip");
            s.Settings.ReplayGuide();
            Assert.IsFalse(s.Settings.CoachDone);
            Assert.AreEqual(0, s.Settings.CoachStep);
            var c2 = new Coach();
            a.Allowed = c2.Allows;
            c2.Begin(s.Sim, a, s.Settings.CoachStep);
            Assert.IsTrue(c2.Active);
            Assert.AreEqual(1, c2.Step);
        }

        [Test]
        public void RelaunchWithTheBoardClosedGoesBackToTheHall()
        {
            string dir = G.TempDir();
            var (s, a, c) = G.NewGuide(dir);
            a.Run(Right(1, s, c)); a.Run(Right(2, s, c));
            Assert.AreEqual(3, c.Step);
            s.Save();
            var (s2, a2, c2) = G.NewGuide(dir);   // relaunch: the open board (UI state) is gone
            Assert.AreEqual(1, c2.Step, "tap the hall again, never stuck at 'pick Business 101'");
            Assert.IsTrue(a2.Run(Right(1, s2, c2)).Ok);
            Assert.AreEqual(2, c2.Step);
        }

        [Test]
        public void RelaunchAfterStartContinuesWithTheWeekRunning()
        {
            string dir = G.TempDir();
            var (s, a, c) = G.NewGuide(dir);
            for (int i = 1; i <= 5; i++) a.Run(Right(i, s, c));
            s.Save();
            var (s2, a2, c2) = G.NewGuide(dir);
            Assert.AreEqual(6, c2.Step);
            G.Run(c2, s2, a2);
            Assert.AreEqual(7, c2.Step);
        }

        [Test]
        public void TheGateLowersTheLateCounterInTheGuide()
        {
            var (s, a, c) = G.NewGuide();
            for (int i = 1; i <= 5; i++) a.Run(Right(i, s, c));
            G.Run(c, s, a);
            Assert.AreEqual(7, c.Step);
            Assert.Greater(s.Sim.S.LateToday, 0, "the first bell: commuters walk the long way and are late");
            int next = s.Sim.S.Plan.Find(p => p.Day == 0 && p.Slot > 0).Slot;
            int before = s.Sim.EstimateLate(0, next);
            Assert.IsTrue(a.Run(Right(7, s, c)).Ok);
            Assert.Less(s.Sim.EstimateLate(0, next), Math.Max(1, before));
        }

        [Test]
        public void NoRewardsAndNoCrisesInTheGuide()
        {
            var (s, a, c) = G.NewGuide();
            Assert.IsTrue(s.Sim.S.Tutorial);
            Assert.IsFalse(s.Sim.CrisesAllowed);
            Assert.IsFalse(s.Sim.RewardOffer("daily_bonus", 1_800_000_000).Ok);
        }
    }

    public class SessionTests
    {
        [Test]
        public void RelaunchKeepsTheCareer()
        {
            string dir = G.TempDir();
            var s = new GameSession(TestData.Gd(), dir) { Now = () => 1_800_000_000 };
            Assert.IsFalse(s.LoadOrNew(3));
            s.Sim.EndTutorial();
            s.Sim.S.Cash = 12345;
            Assert.AreEqual("", s.Save());
            var s2 = new GameSession(TestData.Gd(), dir) { Now = () => 1_800_000_100 };
            Assert.IsTrue(s2.LoadOrNew(99));
            Assert.AreEqual(12345, s2.Sim.S.Cash, 1e-6);
        }

        [Test]
        public void UnreadableSaveIsKeptAndTheBackupLoads()
        {
            string dir = G.TempDir();
            var s = new GameSession(TestData.Gd(), dir) { Now = () => 1_800_000_000 };
            s.LoadOrNew(3);
            s.Sim.S.Cash = 777;
            s.Save();
            s.Sim.S.Cash = 888;
            s.Save();   // .bak = the 777 save
            File.WriteAllText(s.SavePath, "{ broken");
            var events = new List<string>();
            var s2 = new GameSession(TestData.Gd(), dir) { Now = () => 1_800_000_100, Analytics = (n, p) => events.Add(n) };
            Assert.IsTrue(s2.LoadOrNew(9));
            Assert.Greater(s2.Sim.S.Cash, 700);
            Assert.Contains("save_unreadable", events);
            Assert.IsTrue(Directory.GetFiles(dir).Any(f => f.Contains(".unreadable")), "the unreadable file is kept, never deleted");
        }

        [Test]
        public void OfflineResumePaysAndSaves()
        {
            string dir = G.TempDir();
            long now = 1_800_000_000;
            var s = new GameSession(TestData.Gd(), dir) { Now = () => now };
            s.LoadOrNew(3);
            s.Sim.EndTutorial();
            s.Sim.S.History.Add(new WeekStats { Tuition = 8000 });
            s.Save();
            now += 3 * 3600;
            var o = s.Resume();
            Assert.IsNotNull(o);
            Assert.Greater(o.Amount, 0);
            Assert.Greater(s.Sim.S.OfflinePending, 0);
        }
    }

    public class ShopTests
    {
        static (Commerce shop, FakePurchases fake, GameSession s) Shop()
        {
            var fake = new FakePurchases();
            CoddyServices.Purchases = fake;
            var cat = IapCatalog.Parse(TestData.Read("Assets/Resources/data/iap.json"));
            Assert.AreEqual(0, cat.Errors.Count, string.Join(";", cat.Errors));
            foreach (var p in cat.Products.Values) fake.Grants[p.Id] = p.Grants.ToArray();
            var s = new GameSession(TestData.Gd(), G.TempDir()) { Now = () => 1_800_000_000 };
            s.LoadOrNew(3);
            s.Sim.EndTutorial();
            var kit = new CoddyPurchases(G.TempDir(), Commerce.AdFreeEntitlements, () => 1_800_000_000);
            kit.Start("test_key");
            var shop = new Commerce(cat, kit, s);
            foreach (var slug in cat.Order) shop.SetPrice(slug, "$1");
            return (shop, fake, s);
        }

        [Test]
        public void OnlyRemoveAdsAndBigPacksMakeTheGameAdFree()
        {
            var cat = IapCatalog.Parse(TestData.Read("Assets/Resources/data/iap.json"));
            var adFree = cat.Order.Where(cat.GrantsAdFree).OrderBy(x => x).ToArray();
            CollectionAssert.AreEqual(new[] { "bundle", "noads", "premium" }, adFree, "fleet §3/§8 table (plan: + dean_pass in v1.1)");
            foreach (var cheap in new[] { "cash_s", "cash_xl", "starter", "exam_kit", "comeback", "speed", "automation", "offline_plus", "cosmetics_all", "theme_ivy" })
                Assert.IsFalse(cat.GrantsAdFree(cheap), cheap);
        }

        [Test]
        public void BuyingACheapPackDoesNotRemoveAds_RemoveAdsDoes()
        {
            var (shop, fake, s) = Shop();
            Assert.IsTrue(shop.Buy("speed"));
            Assert.IsFalse(shop.AdFree);
            Assert.IsTrue(shop.Buy("cash_s"));
            Assert.IsFalse(shop.AdFree);
            Assert.IsTrue(shop.Buy("noads"));
            Assert.IsTrue(shop.AdFree);
            Assert.AreEqual("owned", shop.WhyNot("noads"));
        }

        [Test]
        public void ConsumableDeliversOncePerTransaction()
        {
            var (shop, fake, s) = Shop();
            double cash = s.Sim.S.Cash;
            Assert.IsTrue(shop.Buy("cash_s"));
            double after = s.Sim.S.Cash;
            Assert.AreEqual(cash + s.Sim.DayIncome(), after, 1.0);
            Assert.IsTrue(s.Settings.Delivered("GPA.fake-1"));
        }

        [Test]
        public void OffersAreNeverShownInTheGuideAndOnlyOnTheirDays()
        {
            var (shop, fake, s) = Shop();
            Assert.AreEqual("", shop.TryOffer("week_report", tutorial: true));
            Assert.AreEqual("", shop.TryOffer("week_report", tutorial: false), "day 0 is too early for the starter");
            Assert.AreEqual("offer_only", shop.WhyNot("starter"));
            Assert.AreEqual("comeback", shop.TryOffer("resume", false, awayDays: 4));
            Assert.AreEqual("", shop.WhyNot("comeback"));
            Assert.AreEqual("", shop.TryOffer("resume", false, awayDays: 5), "one offer per session");
        }

        [Test]
        public void ExamKitArmsTheExamWeek()
        {
            var (shop, fake, s) = Shop();
            s.Settings.FirstSeen = 1_800_000_000 - 2 * 86400;
            Assert.AreEqual("offer_only", shop.WhyNot("exam_kit"));
            Assert.AreEqual("exam_kit", shop.TryOffer("exam_week", false));
            Assert.IsTrue(shop.Buy("exam_kit"));
            Assert.IsTrue(s.Sim.S.ExamKit, "invigilators + backup generator this exam week");
            Assert.IsFalse(shop.AdFree);
        }

        [Test]
        public void StarterGivesFundsAndOneMarshalButNotAdFree()
        {
            var (shop, fake, s) = Shop();
            s.Settings.FirstSeen = 1_800_000_000 - 4 * 86400;
            int marshals = s.Sim.Staff(Depts.Marshals);
            double cash = s.Sim.S.Cash;
            Assert.AreEqual("starter", shop.TryOffer("week_report", false));
            Assert.IsTrue(shop.Buy("starter"));
            Assert.AreEqual(marshals + 1, s.Sim.Staff(Depts.Marshals));
            Assert.Greater(s.Sim.S.Cash, cash);
            Assert.IsFalse(shop.AdFree);
        }

        [Test]
        public void PerksReachTheSim()
        {
            var (shop, fake, s) = Shop();
            Assert.AreEqual(s.Gd.FreeMaxSpeed, s.Sim.MaxSpeed);
            shop.Buy("speed");
            s.ApplyPerks(shop.Has);
            Assert.AreEqual(s.Gd.PaidMaxSpeed, s.Sim.MaxSpeed);
        }

        [Test]
        public void NoTrialOrSubscriptionWordingAnywhere()
        {
            string csv = TestData.Read("Assets/Resources/i18n/strings.csv").ToLowerInvariant();
            foreach (var w in new[] { "free trial", "try free", "subscription", "subscribe" }) StringAssert.DoesNotContain(w, csv);
            string iap = TestData.Read("Assets/Resources/data/iap.json");
            StringAssert.DoesNotContain("\"subscription\"", iap);
        }
    }

    public class RewardAndAnalyticsTests
    {
        [Test]
        public void KitRewardCapsEqualTheBalanceData()
        {
            var gd = TestData.Gd();
            var kit = SemesterRewards.KitLimits();
            CollectionAssert.AreEquivalent(gd.Rewards.Keys, kit.Keys);
            foreach (var kv in gd.Rewards)
            {
                Assert.AreEqual(kv.Value.PerDay, kit[kv.Key].Daily, kv.Key);
                Assert.AreEqual(kv.Value.CooldownS, kit[kv.Key].CooldownS, 1e-9, kv.Key);
            }
            Assert.LessOrEqual(SemesterRewards.UnitsSingleMax * 5, SemesterRewards.UnitsPerDay, "single ≤ 1/5 of the cheapest pack");
            Assert.LessOrEqual(SemesterRewards.UnitsDailyMax * 2, SemesterRewards.UnitsPerDay, "daily ≤ 1/2");
            var cat = IapCatalog.Parse(TestData.Read("Assets/Resources/data/iap.json"));
            int cheapest = cat.Products.Values.Where(p => p.Consumable && !p.OfferOnly).Min(p => p.DaysX10);
            Assert.AreEqual(10, cheapest, "cash_s = 1 day = 1000 units");
        }

        [Test]
        public void EveryAnalyticsEventTheGameSendsIsInTheCatalogue()
        {
            foreach (var n in new[] { "onboarding_step", "onboarding_complete", "onboarding_skip", "save_unreadable", "rewarded_completed", "rewarded_skipped_paid_user", "purchase_success" })
                Assert.AreEqual("", GameEvents.Validate(n, new Dictionary<string, object> { { "step", 3L } }), n);
            Assert.AreNotEqual("", GameEvents.Validate("player_email", null));
            Assert.AreNotEqual("", GameEvents.Validate("onboarding_step", new Dictionary<string, object> { { "who", "a@b.c" } }));
            // the names used in code
            string root = Path.Combine(TestData.UnityDir(), "Assets", "Scripts");
            var rx = new Regex("Analytics\\(\"([a-z_]+)\"");
            foreach (var f in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                foreach (Match m in rx.Matches(File.ReadAllText(f)))
                    Assert.IsTrue(GameEvents.Known.Contains(m.Groups[1].Value), Path.GetFileName(f) + ": " + m.Groups[1].Value);
        }

        [Test]
        public void ReviewMomentsWaitForTheGuide()
        {
            var sim = TestData.NewSim();
            var h = new ReviewHook();
            var good = new SimEvent { Type = "week_done", A = 2, B = 4, V = 0.85 };
            Assert.AreEqual("", h.Check(good, sim, inGuide: true));
            Assert.AreEqual("good_week", h.TakeDeferred(false));
            Assert.AreEqual("", h.TakeDeferred(false));
            Assert.AreEqual("good_week", h.Check(good, sim, false));
            Assert.AreEqual("", h.Check(new SimEvent { Type = "week_done", A = 3, B = 9, V = 0.5 }, sim, false));
            sim.S.History.Add(new WeekStats { Trips = 400, Late = 0 });
            Assert.AreEqual("zero_late_week", h.Check(new SimEvent { Type = "week_done", A = 1, B = 0, V = 0.7 }, sim, false), "plan: first week with zero late students");
        }
    }

    public class StringTests
    {
        [Test]
        public void TableParsesWithTenLanguagesAndNoDuplicates()
        {
            var loc = G.Loc();
            Assert.AreEqual(0, loc.Errors.Count, string.Join("; ", loc.Errors));
            Assert.Greater(loc.Keys.Count, 300);
        }

        [Test]
        public void EveryKeyTheCodeUsesExists()
        {
            var loc = G.Loc();
            string root = Path.Combine(TestData.UnityDir(), "Assets", "Scripts");
            var rx = new Regex("\\.(?:T|F|N)\\(\"([a-z0-9_.]+)\"");
            var missing = new List<string>();
            foreach (var f in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                foreach (Match m in rx.Matches(File.ReadAllText(f)))
                {
                    string k = m.Groups[1].Value;
                    if (k.EndsWith(".")) continue;   // prefix + runtime id
                    if (!loc.Has(k)) missing.Add(Path.GetFileName(f) + ": " + k);
                }
            CollectionAssert.IsEmpty(missing);
        }

        [Test]
        public void DynamicKeysExist()
        {
            var loc = G.Loc();
            var gd = TestData.Gd();
            foreach (var n in gd.Nodes) if (n.Kind != "junction") Assert.IsTrue(loc.Has("place." + n.Id), n.Id);
            foreach (var z in gd.Zones) Assert.IsTrue(loc.Has("zone." + z.Id), z.Id);
            foreach (var r in gd.Research) { Assert.IsTrue(loc.Has("research." + r.Id + ".t"), r.Id); Assert.IsTrue(loc.Has("research." + r.Id + ".d"), r.Id); }
            foreach (var b in new[] { "learning", "life", "mobility", "admin" }) Assert.IsTrue(loc.Has("research.branch." + b), b);
            foreach (var e in gd.Events) { Assert.IsTrue(loc.Has("crisis." + e.Id + ".t"), e.Id); Assert.IsTrue(loc.Has("crisis." + e.Id + ".b"), e.Id); }
            foreach (var f in gd.Faculties) { Assert.IsTrue(loc.Has("faculty." + f.Id), f.Id); foreach (var p in f.Profs) Assert.IsTrue(loc.Has("prof." + p), p); }
            foreach (var c in gd.Courses) { Assert.IsTrue(loc.Has("course." + c.Id), c.Id); Assert.IsTrue(loc.Has("course." + c.Id + ".code"), c.Id); }
            foreach (var c in gd.Cohorts) Assert.IsTrue(loc.Has("cohort." + c.Id), c.Id);
            foreach (var b in gd.Buildings.Keys) { Assert.IsTrue(loc.Has("building." + b), b); Assert.IsTrue(loc.Has("building." + b + ".d"), b); }
            foreach (var d in Depts.All) { Assert.IsTrue(loc.Has("staff." + d), d); Assert.IsTrue(loc.Has("staff." + d + ".d"), d); }
            foreach (var o in new[] { "concede", "talk", "retain", "let_go" }) Assert.IsTrue(loc.Has("decision." + o), o);
            foreach (var i in new[] { Intakes.Keep, Intakes.Grow, Intakes.Select }) { Assert.IsTrue(loc.Has("intake." + i), i); Assert.IsTrue(loc.Has("intake." + i + ".d"), i); }
            for (int d = 0; d < gd.DaysPerWeek; d++) Assert.IsTrue(loc.Has("day." + d) && loc.Has("day.short." + d), "day " + d);
            foreach (var p in Simulation.RewardPlacements) Assert.IsTrue(loc.Has("reward." + p + ".t") && loc.Has("reward." + p + ".b"), p);
            foreach (var w in new[] { "past", "room_busy", "cohort_busy", "prof_busy", "prof_tired", "enough", "needs_lab", "needs_hall", "not_room", "building", "locked",
                "cash", "prestige", "owned", "max", "research", "invalid", "no_prof", "wrong_faculty", "no_room_left", "nothing_missing", "phase", "busy", "requires", "none", "no_cell", "no_board" })
                Assert.IsTrue(loc.Has("err." + w), "err." + w);
            foreach (var w in new[] { "no_prof", "prof_off", "seats", "closed", "building" }) Assert.IsTrue(loc.Has("warn." + w), "warn." + w);
            foreach (var t in new[] { "lunch", "late", "quad", "science", "dorm", "lab", "exam", "staggered", "shuttle", "widen", "decision", "sick", "pipe", "snow", "open_day", "report" })
                Assert.IsTrue(loc.Has("tip." + t), "tip." + t);
        }

        [Test]
        public void PlaceholdersMatchInEveryLanguage()
        {
            var loc = G.Loc();
            var bad = new List<string>();
            foreach (var k in loc.Keys)
            {
                var en = Loc.Placeholders(loc.Cell(k, "en"));
                foreach (var l in Loc.Languages)
                {
                    string cell = loc.Cell(k, l);
                    if (cell == "") continue;
                    if (!en.SequenceEqual(Loc.Placeholders(cell))) bad.Add(l + " " + k);
                }
            }
            CollectionAssert.IsEmpty(bad);
        }

        [Test]
        public void EveryLanguageIsComplete()
        {
            var loc = G.Loc();
            var missing = new Dictionary<string, int>();
            foreach (var l in Loc.Languages)
            {
                int n = loc.Keys.Count(k => loc.Cell(k, l) == "");
                if (n > 0) missing[l] = n;
            }
            Assert.IsEmpty(missing, string.Join(", ", missing.Select(kv => kv.Key + ":" + kv.Value)));
        }

        [Test]
        public void ButtonsStayShortInLongLanguages()
        {
            // overflow guard for the 48 dp pills (fleet §9 "10 dilde metin taşmaz"; de / ru run longest)
            var loc = G.Loc();
            var shortKeys = loc.Keys.Where(k => k.StartsWith("tab.") || k.StartsWith("day.short.") || k.EndsWith(".code") || k == "hud.start_week" || k == "hud.next_week" || k == "hud.report"
                || k == "coach.skip" || k == "intro.start" || k.StartsWith("shop.group.")).ToList();
            var bad = new List<string>();
            foreach (var k in shortKeys)
                foreach (var l in Loc.Languages)
                {
                    string c = loc.Cell(k, l);
                    if (c.Length > 22) bad.Add(l + " " + k + " (" + c.Length + ")");
                }
            CollectionAssert.IsEmpty(bad);
        }

        [Test]
        public void IapCatalogueIsInSyncWithStrings()
        {
            var cat = IapCatalog.Parse(TestData.Read("Assets/Resources/data/iap.json"));
            var loc = G.Loc();
            foreach (var slug in cat.Order)
                foreach (var l in Loc.Languages)
                {
                    string want = loc.Cell("iap." + slug + ".t", l);
                    if (want == "") want = loc.Cell("iap." + slug + ".t", "en");
                    Assert.AreEqual(want, cat.Title(slug, l), slug + " " + l + " (run Tools/build_iap.py)");
                }
        }
    }
}
