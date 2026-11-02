// Pour & Roast — game layer EditMode tests (NUnit; also under plain .NET via Tools/dotnet_check.sh).
//   guide: every step advances only on its action, wrong taps never advance, skip -> replay, relaunch mid-step recovers,
//   the whole guide completes; saves: relaunch keeps the career, unreadable files are kept, slots are separate; shop:
//   ad_free only from noads products, cheap packs never, consumables once per transaction; rewards: kit caps = balance
//   caps; analytics catalogue; strings: every key the code uses exists, placeholders match in all 10 languages, IAP
//   catalogue in sync.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CoddyKit;
using NUnit.Framework;
using PourAndRoast.Sim;
using PourAndRoast.Sim.Tests;

namespace PourAndRoast.Game.Tests
{
    static class G
    {
        public static string TempDir()
        {
            string d = Path.Combine(Path.GetTempPath(), "pr-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(d);
            return d;
        }

        public static (GameSession s, GameActions a, Coach c) NewGuide(string dir = null)
        {
            var s = new GameSession(TestData.Gd(), dir ?? TempDir()) { Now = () => 1_800_000_000 };
            s.LoadOrNew(5);
            s.Sim.EmitCrowd = false;
            var a = new GameActions(s);
            var c = new Coach { Persist = (step, done) => { s.Settings.CoachStep = step; s.Settings.CoachDone = done; s.Settings.Save(); } };
            a.Allowed = c.Allows;
            a.Ran += (id, r) => c.OnAction(s.Sim, a, id, r);
            c.Begin(s.Sim, a, s.Settings.CoachStep);
            return (s, a, c);
        }

        /// <summary>Frames of the guide driving the world (fast while it waits for the world, paused otherwise).</summary>
        public static void Run(Coach c, GameSession s, GameActions a, int frames = 600)
        {
            for (int i = 0; i < frames && c.Active && c.FastForward(s.Sim); i++) c.Drive(s.Sim, a, 0.1);
            c.Drive(s.Sim, a, 0.05);
        }

        /// <summary>The right action for the current step (ticket ids come from the career).</summary>
        public static string Right(Coach c, GameSession s, GameActions a)
        {
            switch (c.Step)
            {
                case 2: return "select_ticket:" + s.Sim.S.Tickets[0].Id;
                case 8: return "flag:" + s.Sim.S.Tickets.First(t => t.Pre).Id;
                case 12: return "rule:deadline";
                default: return new[] { "", "start_day", "", "brew", "handover", "fridge", "buy:staff:barista", "batch:on", "", "dial_in", "buy:seats", "prep:hot" }[c.Step];
            }
        }

        public static Loc Loc() => PourAndRoast.Game.Loc.FromCsv(TestData.Read("Assets/Resources/i18n/strings.csv"));
    }

    public class GuideTests
    {
        [Test]
        public void TheWholeGuideCompletesWithTheRightActions()
        {
            var (s, a, c) = G.NewGuide();
            var events = new List<string>();
            c.Analytics = (n, p) => events.Add(n);
            for (int step = 1; step <= Coach.Steps; step++)
            {
                Assert.IsTrue(c.Active, "guide still running at step " + step);
                G.Run(c, s, a);
                Assert.AreEqual(step, c.Step, "step order");
                string right = G.Right(c, s, a);
                var r = a.Run(right);
                Assert.IsTrue(r.Ok, "step " + step + " action " + right + " failed: " + r.Error);
            }
            G.Run(c, s, a);
            Assert.IsFalse(c.Active, "guide finished at step " + c.Step);
            Assert.IsFalse(s.Sim.S.Tutorial, "customers + crises may start after the guide");
            Assert.IsTrue(s.Settings.CoachDone);
            Assert.AreEqual(2, s.Sim.CountRole(Roles.Barista));
            Assert.IsTrue(s.Sim.S.Batch);
            Assert.AreEqual(Rules.Deadline, s.Sim.S.Rule);
            Assert.AreEqual(1, s.Sim.Venue.Seats);
            Assert.Contains("onboarding_complete", events);
            Assert.AreEqual(Coach.Steps, events.Count(e => e == "onboarding_step"));
        }

        [Test]
        public void EveryStepNeedsItsOwnActionAndWrongOnesNeverAdvance()
        {
            var (s, a, c) = G.NewGuide();
            for (int step = 1; step <= Coach.Steps; step++)
            {
                G.Run(c, s, a);
                Assert.AreEqual(step, c.Step);
                int flash = c.Flash;
                var wrong = a.Run(step == 7 ? "rule:vip" : "batch:on");
                Assert.IsFalse(wrong.Ok, "step " + step + " accepted a wrong action");
                Assert.AreEqual("blocked", wrong.Error);
                Assert.AreEqual(step, c.Step, "a wrong tap never advances");
                Assert.AreEqual(flash + 1, c.Flash, "the hint pulses again");
                Assert.IsTrue(a.Run(G.Right(c, s, a)).Ok);
            }
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
            Assert.IsTrue(loc.Has("coach.done"));
        }

        [Test]
        public void TimeIsPausedWhileTheGuideWaitsForThePlayer_FastWhileItWaitsForTheWorld()
        {
            var (s, a, c) = G.NewGuide();
            a.Run("start_day");
            G.Run(c, s, a);
            Assert.AreEqual(2, c.Step);
            double m = s.Sim.S.Minute;
            c.Drive(s.Sim, a, 1.0);
            s.Sim.Advance(1.0);
            Assert.AreEqual(m, s.Sim.S.Minute, 1e-9, "paused at a tap step");
            a.Run(G.Right(c, s, a));
            a.Run("brew");
            Assert.AreEqual(4, c.Step);
            Assert.IsTrue(c.FastForward(s.Sim), "the shot is pulled fast");
            G.Run(c, s, a);
            Assert.IsTrue(s.Sim.S.Tickets[0].Ready);
            Assert.IsFalse(c.FastForward(s.Sim), "the cup waits on the shelf for the player's tap");
        }

        [Test]
        public void SkipThenReplayFromSettings()
        {
            var (s, a, c) = G.NewGuide();
            var events = new List<string>();
            c.Analytics = (n, p) => events.Add(n);
            a.Run("start_day");
            c.Skip(s.Sim);
            Assert.IsFalse(c.Active);
            Assert.IsTrue(s.Settings.CoachDone);
            Assert.Contains("onboarding_skip", events);
            Assert.IsTrue(a.Run("rule:vip").Ok, "everything is allowed after a skip");
            s.Settings.ReplayGuide();
            Assert.IsFalse(s.Settings.CoachDone);
            Assert.AreEqual(0, s.Settings.CoachStep);
            var c2 = new Coach();
            a.Allowed = c2.Allows;
            c2.Begin(s.Sim, a, s.Settings.CoachStep);
            Assert.IsTrue(c2.Active);
            Assert.LessOrEqual(c2.Step, 2, "the replay starts from the top (the day is already open: at the first ticket)");
            Assert.IsTrue(s.Sim.S.Tutorial, "a replay runs in the guide's safe mode");
        }

        [Test]
        public void RelaunchBeforeBrewingGoesBackToPickingTheTicket()
        {
            string dir = G.TempDir();
            var (s, a, c) = G.NewGuide(dir);
            a.Run("start_day"); G.Run(c, s, a);
            a.Run(G.Right(c, s, a));
            Assert.AreEqual(3, c.Step);
            s.Save();
            var (s2, a2, c2) = G.NewGuide(dir);   // relaunch: the selection (UI state) is gone
            Assert.AreEqual(2, c2.Step, "pick the ticket again, never stuck at 'tap the espresso machine'");
            Assert.IsTrue(a2.Run(G.Right(c2, s2, a2)).Ok);
            Assert.AreEqual(3, c2.Step);
        }

        [Test]
        public void RelaunchAfterTheHandoverContinuesWithTheMilk()
        {
            string dir = G.TempDir();
            var (s, a, c) = G.NewGuide(dir);
            for (int i = 1; i <= 4; i++) { G.Run(c, s, a); Assert.IsTrue(a.Run(G.Right(c, s, a)).Ok, "step " + i); }
            Assert.AreEqual(5, c.Step);
            s.Save();
            var (s2, a2, c2) = G.NewGuide(dir);
            Assert.AreEqual(5, c2.Step);
            Assert.IsTrue(s2.Sim.S.Tickets.Any(), "the latte order is there");
        }

        [Test]
        public void NoRewardsNoCustomersAndNoCrisesInTheGuide()
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
        public void SaveSlotsAreSeparateCareers()
        {
            string dir = G.TempDir();
            var s = new GameSession(TestData.Gd(), dir, 0) { Now = () => 1_800_000_000 };
            s.LoadOrNew(3); s.Sim.S.Cash = 111; s.Save();
            var s2 = new GameSession(TestData.Gd(), dir, 2) { Now = () => 1_800_000_000 };
            Assert.IsFalse(s2.LoadOrNew(4), "slot 3 starts a new career");
            Assert.IsFalse(s2.Sim.S.Tutorial, "an extra slot skips the guide");
            s2.Sim.S.Cash = 222; s2.Save();
            Assert.IsTrue(s.SlotUsed(0) && s.SlotUsed(2) && !s.SlotUsed(1));
            var back = new GameSession(TestData.Gd(), dir, 0) { Now = () => 1_800_000_000 };
            back.LoadOrNew(5);
            Assert.AreEqual(111, back.Sim.S.Cash, 1e-9);
        }

        [Test]
        public void OfflineResumePaysAndSaves()
        {
            string dir = G.TempDir();
            long now = 1_800_000_000;
            var s = new GameSession(TestData.Gd(), dir) { Now = () => now };
            s.LoadOrNew(3);
            s.Sim.EndTutorial();
            s.Sim.S.History.Add(new DayStats { Revenue = 2000 });
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
        public void CatalogueHasThePlansTwentyTwoSkusAndNoItemAboveTwentyDollars()
        {
            var cat = IapCatalog.Parse(TestData.Read("Assets/Resources/data/iap.json"));
            Assert.AreEqual(22, cat.Order.Count);
            foreach (var p in cat.Products.Values) Assert.LessOrEqual(p.Usd, 19.99, p.Slug);
            Assert.AreEqual(2.99, cat.Products["noads"].Usd, 1e-9, "Remove Ads is always its own cheap product");
        }

        [Test]
        public void OnlyRemoveAdsAndBigPacksMakeTheGameAdFree()
        {
            var cat = IapCatalog.Parse(TestData.Read("Assets/Resources/data/iap.json"));
            var adFree = cat.Order.Where(cat.GrantsAdFree).OrderBy(x => x).ToArray();
            CollectionAssert.AreEqual(new[] { "bundle", "noads", "premium", "roast_pass" }, adFree, "fleet §3/§8 table");
            foreach (var cheap in new[] { "cash_s", "cash_xl", "starter", "crisis", "speed", "automation", "slots", "offline_plus", "cosmetics_all", "theme_nordic" })
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
            Assert.AreEqual(cash + s.Sim.DayIncome(), s.Sim.S.Cash, 1.0);
            Assert.IsTrue(s.Settings.Delivered("GPA.fake-1"));
        }

        [Test]
        public void EditionGivesStarterBeansOnceAndAnExtraSlot()
        {
            var (shop, fake, s) = Shop();
            Assert.AreEqual(1, shop.SaveSlots);
            double kg = s.Sim.S.BeanKg;
            Assert.IsTrue(shop.Buy("premium"));
            Assert.AreEqual(kg + 10, s.Sim.S.BeanKg, 1e-6);
            Assert.Greater(s.Sim.S.BeanQ, s.Sim.Gd.StartBeansQ);
            Assert.AreEqual(2, shop.SaveSlots);
            shop.Restore();
            Assert.AreEqual(kg + 10, s.Sim.S.BeanKg, 1e-6, "a restore never gives the beans twice");
            Assert.IsTrue(shop.Buy("slots"));
            Assert.AreEqual(5, shop.SaveSlots);
        }

        [Test]
        public void OffersAreNeverShownInTheGuideAndOnlyOnTheirDays()
        {
            var (shop, fake, s) = Shop();
            Assert.AreEqual("", shop.TryOffer("day_report", tutorial: true));
            Assert.AreEqual("", shop.TryOffer("day_report", tutorial: false), "day 0 is too early for the starter");
            Assert.AreEqual("offer_only", shop.WhyNot("starter"));
            Assert.AreEqual("comeback", shop.TryOffer("resume", false, awayDays: 4));
            Assert.AreEqual("", shop.WhyNot("comeback"));
            Assert.AreEqual("", shop.TryOffer("venue", false), "one offer per session");
        }

        [Test]
        public void MilkRunKitRefillsTheFridge()
        {
            var (shop, fake, s) = Shop();
            s.Sim.S.Milk[0] = 0;
            Assert.AreEqual("crisis", shop.TryOffer("milk_late", false));
            Assert.IsTrue(shop.Buy("crisis"));
            Assert.Greater(s.Sim.S.Milk[0], 0);
        }

        [Test]
        public void PerksReachTheSim()
        {
            var (shop, fake, s) = Shop();
            Assert.AreEqual(s.Gd.FreeMaxSpeed, s.Sim.MaxSpeed);
            shop.Buy("speed");
            s.ApplyPerks(shop.Has);
            Assert.AreEqual(s.Gd.PaidMaxSpeed, s.Sim.MaxSpeed);
            Assert.IsFalse(s.Sim.AutoTune);
            shop.Buy("automation");
            s.ApplyPerks(shop.Has);
            Assert.IsTrue(s.Sim.AutoTune, "Head Barista+ keeps the grinder tuned");
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
            var kit = RoastRewards.KitLimits();
            CollectionAssert.AreEquivalent(gd.Rewards.Keys, kit.Keys);
            foreach (var kv in gd.Rewards)
            {
                Assert.AreEqual(kv.Value.PerDay, kit[kv.Key].Daily, kv.Key);
                Assert.AreEqual(kv.Value.CooldownS, kit[kv.Key].CooldownS, 1e-9, kv.Key);
            }
            Assert.LessOrEqual(RoastRewards.UnitsSingleMax * 5, RoastRewards.UnitsPerDay, "single ≤ 1/5 of the cheapest pack");
            Assert.LessOrEqual(RoastRewards.UnitsDailyMax * 2, RoastRewards.UnitsPerDay, "daily ≤ 1/2");
            var cat = IapCatalog.Parse(TestData.Read("Assets/Resources/data/iap.json"));
            int cheapest = cat.Products.Values.Where(p => p.Consumable && !p.OfferOnly).Min(p => p.DaysX10);
            Assert.AreEqual(10, cheapest, "cash_s = 1 day = 1000 units");
            Assert.LessOrEqual(gd.Rewards["daily_bonus"].Days, gd.RewardSingleMaxDays);
        }

        [Test]
        public void EveryAnalyticsEventTheGameSendsIsInTheCatalogue()
        {
            foreach (var n in new[] { "onboarding_step", "onboarding_complete", "onboarding_skip", "save_unreadable", "rewarded_completed", "rewarded_skipped_paid_user", "purchase_success" })
                Assert.AreEqual("", GameEvents.Validate(n, new Dictionary<string, object> { { "step", 3L } }), n);
            Assert.AreNotEqual("", GameEvents.Validate("player_email", null));
            Assert.AreNotEqual("", GameEvents.Validate("onboarding_step", new Dictionary<string, object> { { "who", "a@b.c" } }));
            string root = Path.Combine(TestData.UnityDir(), "Assets", "Scripts");
            var rx = new Regex("(?:Analytics|Track)\\(\"([a-z_]+)\"");
            foreach (var f in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                foreach (Match m in rx.Matches(File.ReadAllText(f)))
                    Assert.IsTrue(GameEvents.Known.Contains(m.Groups[1].Value), Path.GetFileName(f) + ": " + m.Groups[1].Value);
        }

        [Test]
        public void ReviewMomentsWaitForTheGuide()
        {
            var sim = TestData.NewSim();
            var h = new ReviewHook();
            var good = new SimEvent { Type = "day_done", B = 4 };
            Assert.AreEqual("", h.Check(good, sim, inGuide: true));
            Assert.AreEqual("good_day", h.TakeDeferred(false));
            Assert.AreEqual("", h.TakeDeferred(false));
            Assert.AreEqual("good_day", h.Check(good, sim, false));
            Assert.AreEqual("", h.Check(new SimEvent { Type = "day_done", B = 2 }, sim, false));
            Assert.AreEqual("great_roast", h.Check(new SimEvent { Type = "roast_done", V = 90 }, sim, false));
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
            var missing = new List<string>();
            void Need(string k) { if (!loc.Has(k)) missing.Add(k); }
            foreach (var v in gd.Venues) { Need("venue." + v.Id); Need("venue." + v.Id + ".d"); }
            foreach (var m in gd.Menu) { Need("menu." + m.Id); Need("menu." + m.Id + ".short"); }
            foreach (var c in gd.Customers) Need("cust." + c.Id);
            foreach (var r in gd.Research) { Need("research." + r.Id + ".t"); Need("research." + r.Id + ".d"); }
            foreach (var b in new[] { "bar", "cafe", "roastery" }) Need("research.branch." + b);
            foreach (var e in gd.Events) { Need("crisis." + e.Id + ".t"); Need("crisis." + e.Id + ".b"); }
            Need("crisis.wholesale_short.t"); Need("crisis.wholesale_short.b");
            foreach (var r in gd.Roles.Keys) { Need("role." + r); Need("buy.staff." + r + ".d"); }
            foreach (var t in gd.Traits.Keys) Need("trait." + t);
            foreach (var st in Stations.All) Need("station." + st);
            foreach (var r in Rules.All) { Need("rule." + r); Need("rule." + r + ".d"); }
            foreach (var w in Weathers.All) Need("weather." + w);
            foreach (var o in gd.Origins) { Need("origin." + o.Id); Need("origin." + o.Id + ".d"); }
            foreach (var p in Profiles.All) Need("profile." + p);
            foreach (var c in gd.Contracts) { Need("contract." + c.Id); Need("contract." + c.Id + ".d"); }
            foreach (var s in gd.Suppliers.Keys) Need("supplier." + s);
            foreach (var u in new[] { "heads", "wands", "grinder", "ice", "rail", "seats", "decor", "drum", "service_grinder", "milk_run", "ice_bag", "beans" }) { Need("buy." + u); Need("buy." + u + ".d"); }
            foreach (var p in RoastRewards.Placements) { Need("reward." + p + ".t"); Need("reward." + p + ".b"); }
            foreach (var why in new[] { "great", "good", "slow", "late", "bitter", "weak", "sold_out", "no_oat", "walked" }) Need("review." + why);
            foreach (var e in new[] { "cash", "rating", "locked", "research", "max", "owned", "busy", "phase", "invalid", "no_green", "nothing_ready", "nothing_rested", "no_ticket", "started", "ready", "last", "role", "blocked", "unknown" }) Need("err." + e);
            CollectionAssert.IsEmpty(missing);
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
            var shortKeys = loc.Keys.Where(k => k.StartsWith("tab.") || k.StartsWith("menu.") && k.EndsWith(".short") || k == "hud.open" || k == "hud.next_day" || k == "coach.skip"
                || k == "intro.start" || k.StartsWith("shop.group.") || k.StartsWith("station.") || k == "ticket.brew" || k == "ticket.flag" || k == "ticket.unflag" || k == "ticket.cancel" || k.StartsWith("hud.batch_")).ToList();
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

        [Test]
        public void NoRealCoffeeChainsOrTheReferenceGameInTheTexts()
        {
            string csv = TestData.Read("Assets/Resources/i18n/strings.csv").ToLowerInvariant();
            foreach (var w in new[] { "starbucks", "costa", "tim hortons", "dunkin", "codigames", "idle coffee shop", "nescafe", "nespresso", "lavazza" })
                StringAssert.DoesNotContain(w, csv);
        }
    }
}
