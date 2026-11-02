// Ridgeback Rangers — game layer EditMode tests (NUnit; also under plain .NET via Tools/dotnet_check.sh).
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
using RidgebackRangers.Sim;
using RidgebackRangers.Sim.Tests;

namespace RidgebackRangers.Game.Tests
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
            s.Sim.EmitCrowd = false;
            var a = new GameActions(s);
            var c = new Coach { Persist = (step, done) => { s.Settings.CoachStep = step; s.Settings.CoachDone = done; s.Settings.Save(); } };
            a.Allowed = c.Allows;
            a.Ran += (id, r) => c.OnAction(s.Sim, a, id, r);
            c.Begin(s.Sim, a, s.Settings.CoachStep);
            return (s, a, c);
        }

        /// <summary>Lets the world catch up for a guide step that waits for it (the Feeder driving, visitors, the rest of the day).</summary>
        public static void Run(Coach c, GameSession s, GameActions a, int frames = 400)
        {
            for (int i = 0; i < frames && c.Active && c.FastForward(s.Sim); i++) c.Drive(s.Sim, a, 0.1);
            c.Drive(s.Sim, a, 0.05);
        }

        public static Loc Loc() => RidgebackRangers.Game.Loc.FromCsv(TestData.Read("Assets/Resources/i18n/strings.csv"));
    }

    public class GuideTests
    {
        /// <summary>The right action for the guide's current step (ids are live: pins, rangers).</summary>
        static string[] Right(GameSession s, GameActions a, Coach c)
        {
            var sim = s.Sim;
            PinState Pin(string k) => sim.S.Pins.Find(p => p.Kind == k);
            int R(string role) => sim.S.Rangers.FindIndex(r => r.Role == role && !r.Busy);
            switch (c.Step)
            {
                case 1: return new[] { "badge" };
                case 2: return new[] { "camera" };
                case 3: return new[] { "pin:" + Pin(PinKinds.Hungry).Id };
                case 4: return new[] { "send:" + Pin(PinKinds.Hungry).Id + ":" + R(Roles.Feeder) };
                case 5: return new[] { "fed_ok" };
                case 6: return new[] { "gate" };
                case 7: return new[] { "tram_go" };
                case 8: return new[] { "pin:" + Pin(PinKinds.Squabble).Id, "send:" + Pin(PinKinds.Squabble).Id + ":" + R(Roles.Handler) };
                case 9: return new[] { "animal:" + Coach.CompareA, "animal:" + Coach.CompareB };
                case 10: return new[] { "buy:fence:" + Coach.SquabblePaddock };
                case 11: return new[] { "next_day" };
                case 12: return new[] { "temp:0:50" };
            }
            return new string[0];
        }

        static void Do(GameSession s, GameActions a, Coach c)
        {
            G.Run(c, s, a);
            int step = c.Step;
            foreach (var id in Right(s, a, c))
            {
                var r = a.Run(id);
                Assert.IsTrue(r.Ok, "step " + step + " action " + id + " failed: " + r.Error);
            }
        }

        [Test]
        public void TheWholeGuideCompletesWithTheRightActions()
        {
            var (s, a, c) = G.NewGuide();
            var events = new List<string>();
            c.Analytics = (n, p) => events.Add(n);
            for (int step = 1; step <= Coach.Steps && c.Active; step++)
            {
                G.Run(c, s, a);
                Assert.AreEqual(step, c.Step, "step order");
                Do(s, a, c);
                if (c.Active) Assert.AreEqual(step + 1, c.Step, "step " + step + " advanced");
            }
            Assert.IsFalse(c.Active, "guide finished at step " + c.Step);
            Assert.IsFalse(s.Sim.S.Tutorial, "pins may fail and events may start after the guide");
            Assert.IsTrue(s.Settings.CoachDone);
            Assert.AreEqual(2, s.Sim.S.Day, "the guide ran the whole first day");
            Assert.AreEqual(2, s.Sim.S.Paddocks[Coach.SquabblePaddock].Fence);
            Assert.IsTrue(s.Sim.S.Eggs.Count > 0 && s.Sim.S.Eggs[0].Checked, "the gift egg is warming");
            Assert.Contains("onboarding_complete", events);
            Assert.AreEqual(Coach.Steps, events.Count(e => e == "onboarding_step"));
        }

        [Test]
        public void WrongTapsNeverAdvanceAndRepeatTheHint()
        {
            var (s, a, c) = G.NewGuide();
            Assert.AreEqual(1, c.Step);
            var r = a.Run("gate");
            Assert.IsFalse(r.Ok);
            Assert.AreEqual("blocked", r.Error);
            Assert.AreEqual(1, c.Step);
            Assert.AreEqual(1, c.WrongTaps);
            Assert.AreEqual(1, c.Flash);
            Assert.IsFalse(a.Run("buy:hire:vet").Ok, "no shortcuts during the guide");
            Assert.AreEqual(1, c.Step);
            Assert.IsTrue(a.Run("camera").Ok, "panning is always fine");
            Assert.AreEqual(1, c.Step);
            Assert.IsTrue(a.Run("badge").Ok);
            Assert.AreEqual(2, c.Step);
        }

        [Test]
        public void SendingTheWrongRoleRepeatsTheHint()
        {
            var (s, a, c) = G.NewGuide();
            Do(s, a, c); Do(s, a, c); Do(s, a, c);
            Assert.AreEqual(4, c.Step);
            var pin = s.Sim.S.Pins.Find(p => p.Kind == PinKinds.Hungry);
            int vet = s.Sim.S.Rangers.FindIndex(x => x.Role == Roles.Vet);
            Assert.IsFalse(a.Run("send:" + pin.Id + ":" + vet).Ok);
            Assert.AreEqual(4, c.Step);
            Assert.Greater(c.WrongTaps, 0);
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
            for (int i = 1; i <= 4; i++) Do(s, a, c);
            Assert.AreEqual(5, c.Step);
            Assert.IsTrue(c.FastForward(s.Sim), "the world runs while the Feeder drives the crate");
            G.Run(c, s, a);
            Assert.IsFalse(c.FastForward(s.Sim));
            Assert.AreEqual("fed", c.Target);
            double m = s.Sim.S.Minute;
            c.Drive(s.Sim, a, 1.0);
            s.Sim.Advance(1.0);
            Assert.AreEqual(m, s.Sim.S.Minute, 1e-9, "paused at a tap step");
        }

        [Test]
        public void SkipThenReplayFromSettings()
        {
            var (s, a, c) = G.NewGuide();
            var events = new List<string>();
            c.Analytics = (n, p) => events.Add(n);
            a.Run("badge");
            c.Skip(s.Sim);
            Assert.IsFalse(c.Active);
            Assert.IsTrue(s.Settings.CoachDone);
            Assert.Contains("onboarding_skip", events);
            Assert.IsTrue(s.Sim.S.GateOpen, "a skipped guide leaves the park open");
            var hire = a.Run("buy:hire:vet");
            Assert.IsTrue(hire.Ok || hire.Error == "cash", "everything is allowed after a skip");
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
        public void RelaunchAfterSelectingThePinGoesBackToTappingIt()
        {
            string dir = G.TempDir();
            var (s, a, c) = G.NewGuide(dir);
            Do(s, a, c); Do(s, a, c); Do(s, a, c);
            Assert.AreEqual(4, c.Step);
            s.Save();
            var (s2, a2, c2) = G.NewGuide(dir);   // relaunch: the selection (UI state) is gone
            Assert.AreEqual(3, c2.Step, "tap the pin again, never stuck at 'send a Feeder'");
            Do(s2, a2, c2);
            Assert.AreEqual(4, c2.Step);
        }

        [Test]
        public void RelaunchAfterSendingTheFeederContinuesWithTheCrate()
        {
            string dir = G.TempDir();
            var (s, a, c) = G.NewGuide(dir);
            for (int i = 1; i <= 4; i++) Do(s, a, c);
            s.Save();
            var (s2, a2, c2) = G.NewGuide(dir);
            Assert.AreEqual(5, c2.Step);
            G.Run(c2, s2, a2);
            Assert.IsTrue(a2.Run("fed_ok").Ok);
            Assert.AreEqual(6, c2.Step);
        }

        [Test]
        public void RelaunchAfterTheDayEndedLandsOnTheReportStep()
        {
            string dir = G.TempDir();
            var (s, a, c) = G.NewGuide(dir);
            for (int i = 1; i <= 10; i++) Do(s, a, c);
            Assert.AreEqual(11, c.Step);
            G.Run(c, s, a);
            Assert.AreEqual(Phases.Report, s.Sim.Phase);
            s.Save();
            var (s2, a2, c2) = G.NewGuide(dir);
            Assert.AreEqual(11, c2.Step);
            Assert.AreEqual("next_day", c2.Target);
        }

        [Test]
        public void NoRewardsNoFailedPinsAndNoEventsInTheGuide()
        {
            var (s, a, c) = G.NewGuide();
            Assert.IsTrue(s.Sim.S.Tutorial);
            foreach (var p in Simulation.RewardPlacements) Assert.IsFalse(s.Sim.RewardOffer(p, 1_800_000_000).Ok, p);
            for (int i = 1; i <= 11; i++) Do(s, a, c);
            Assert.AreEqual(0, s.Sim.S.History[0].PinsFailed);
            Assert.AreEqual(0, s.Sim.S.Events.Count);
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
            s.Sim.S.History.Add(new DayStats { Tickets = 2000 });
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
            CollectionAssert.AreEqual(new[] { "bundle", "noads", "premium", "ranger_pass" }, adFree, "plan §10 / fleet §3/§8 table");
            foreach (var cheap in new[] { "cash_s", "cash_xl", "starter", "storm_kit", "speed", "offline_plus", "cosmetics_all", "skin_tram_sunset" })
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
            Assert.AreEqual("", shop.TryOffer("day_report", tutorial: true));
            Assert.AreEqual("", shop.TryOffer("day_report", tutorial: false), "day 0 is too early for the starter");
            Assert.AreEqual("offer_only", shop.WhyNot("starter"));
            Assert.AreEqual("comeback", shop.TryOffer("resume", false, awayDays: 4));
            Assert.AreEqual("", shop.WhyNot("comeback"));
            Assert.AreEqual("", shop.TryOffer("resume", false, awayDays: 5), "one offer per session");
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
            var kit = RidgebackRewards.KitLimits();
            CollectionAssert.AreEquivalent(gd.Rewards.Keys, kit.Keys);
            foreach (var kv in gd.Rewards)
            {
                Assert.AreEqual(kv.Value.PerDay, kit[kv.Key].Daily, kv.Key);
                Assert.AreEqual(kv.Value.CooldownS, kit[kv.Key].CooldownS, 1e-9, kv.Key);
            }
            Assert.LessOrEqual(RidgebackRewards.UnitsSingleMax * 5, RidgebackRewards.UnitsPerDay, "single ≤ 1/5 of the cheapest pack");
            Assert.LessOrEqual(RidgebackRewards.UnitsDailyMax * 2, RidgebackRewards.UnitsPerDay, "daily ≤ 1/2");
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
            var good = new SimEvent { Type = "day_done", B = 7 };
            Assert.AreEqual("zone", ReviewHook.Moment(new SimEvent { Type = "zone", Text = "fern" }, sim));
            Assert.AreEqual("", h.Check(good, sim, inGuide: true));
            Assert.AreEqual("good_day", h.TakeDeferred(false));
            Assert.AreEqual("", h.TakeDeferred(false));
            Assert.AreEqual("good_day", h.Check(good, sim, false));
            Assert.AreEqual("", h.Check(new SimEvent { Type = "day_done", B = 3 }, sim, false));
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
            foreach (var n in gd.Nodes) if (n.Kind == "paddock" || n.Kind == "dig") Assert.IsTrue(loc.Has("place." + n.Id), n.Id);
            foreach (var z in gd.Zones) { Assert.IsTrue(loc.Has("zone." + z.Id), z.Id); Assert.IsTrue(loc.Has("zone." + z.Id + ".d"), z.Id); }
            foreach (var sp in gd.Species) { Assert.IsTrue(loc.Has("sp." + sp.Id), sp.Id); Assert.IsTrue(loc.Has("sp." + sp.Id + ".d"), sp.Id); }
            foreach (var e in gd.Events) { Assert.IsTrue(loc.Has("crisis." + e.Id + ".t"), e.Id); Assert.IsTrue(loc.Has("crisis." + e.Id + ".b"), e.Id); }
            Assert.IsTrue(loc.Has("crisis.escape.t"));
            foreach (var k in PinKinds.All) { Assert.IsTrue(loc.Has("pin." + k), k); Assert.IsTrue(loc.Has("pin." + k + ".d"), k); Assert.IsTrue(loc.Has("tip." + (k == PinKinds.Sick ? "vet" : k == PinKinds.Hungry ? "report" : k)), k); }
            foreach (var r in Roles.All) { Assert.IsTrue(loc.Has("role." + r), r); Assert.IsTrue(loc.Has("role." + r + ".d"), r); }
            foreach (var st in new[] { "idle", "travel", "work", "back", "dig" }) Assert.IsTrue(loc.Has("ranger.status." + st), st);
            foreach (var t in new[] { "calm", "skittish", "bossy" }) Assert.IsTrue(loc.Has("temper." + t), t);
            foreach (var d in new[] { "fern", "fruit" }) Assert.IsTrue(loc.Has("diet." + d), d);
            foreach (var b in new[] { "forest", "dune" }) Assert.IsTrue(loc.Has("biome." + b), b);
            foreach (var h in new[] { "ok", "green", "yellow", "red", "off" }) Assert.IsTrue(loc.Has("health." + h), h);
            foreach (var p in Simulation.RewardPlacements) { Assert.IsTrue(loc.Has("reward." + p + ".t"), p); Assert.IsTrue(loc.Has("reward." + p + ".b"), p); }
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
            var shortKeys = loc.Keys.Where(k => k.StartsWith("tab.") || k == "pin.send" || k == "hud.start_day" || k == "hud.next_day" || k == "coach.skip" || k == "intro.start" || k.StartsWith("shop.group.")).ToList();
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
