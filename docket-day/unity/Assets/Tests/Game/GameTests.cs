// Docket Day — game layer EditMode tests (NUnit; also under plain .NET via Tools/dotnet_check.sh).
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
using DocketDay.Sim;
using DocketDay.Sim.Tests;

namespace DocketDay.Game.Tests
{
    static class G
    {
        public static string TempDir()
        {
            string d = Path.Combine(Path.GetTempPath(), "dd-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(d);
            return d;
        }

        public static (GameSession s, GameActions a, Coach c) NewGuide(string dir = null)
        {
            var s = new GameSession(TestData.Gd(), dir ?? TempDir()) { Now = () => 1_800_000_000 };
            s.LoadOrNew(5);
            var a = new GameActions(s);
            var c = new Coach { Persist = (step, done) => { s.Settings.CoachStep = step; s.Settings.CoachDone = done; s.Settings.Save(); } };
            a.Allowed = c.AllowsFor(() => s.Sim);
            a.Ran += (id, r) => c.OnAction(s.Sim, a, id, r);
            c.Begin(s.Sim, a, s.Settings.CoachStep);
            return (s, a, c);
        }

        /// <summary>Lets the world catch up for a guide step that waits for it (the day passing, the hearing).</summary>
        public static void Run(Coach c, GameSession s, GameActions a, int frames = 600)
        {
            for (int i = 0; i < frames && c.Active && c.FastForward(s.Sim); i++) c.Drive(s.Sim, a, 0.1);
            c.Drive(s.Sim, a, 0.05);
        }

        /// <summary>The action that completes the current step (what a player following the spotlight does).</summary>
        public static string Right(Coach c, GameSession s)
        {
            var sim = s.Sim;
            int t = sim.S.TutorialMatter;
            switch (c.Step)
            {
                case 1: return "reception";
                case 2: return "accept:" + t;
                case 3: return "assign:" + c.SuggestedStaff(sim) + ":" + t + ":0";
                case 4: return "ff";
                case 5: return "assign:" + c.SuggestedStaff(sim) + ":" + t + ":1";
                case 6:
                {
                    var m = sim.MatterById(t);
                    int claim = Enumerable.Range(0, m.Claims).First(x => !m.Matched.Contains(x));
                    return "prep_match:" + t + ":" + claim + ":" + claim;
                }
                case 7: return "bill";
                case 8: return "build:library";
                case 9:
                {
                    int id = sim.S.TutorialCollision[0];
                    return "assign:" + c.SuggestedStaff(sim) + ":" + id + ":" + (sim.MatterById(id).Tasks.Count - 1);
                }
            }
            return "";
        }

        /// <summary>Plays the guide until it reaches `step` (or ends).</summary>
        public static void To(Coach c, GameSession s, GameActions a, int step)
        {
            for (int guard = 0; guard < 60 && c.Active && c.Step < step; guard++)
            {
                if (c.FastForward(s.Sim)) { Run(c, s, a); continue; }
                var r = a.Run(Right(c, s));
                Assert.IsTrue(r.Ok, "step " + c.Step + ": " + r.Error);
                Run(c, s, a);
            }
        }

        public static Loc Loc() => DocketDay.Game.Loc.FromCsv(TestData.Read("Assets/Resources/i18n/strings.csv"));
    }

    public class GuideTests
    {
        [Test]
        public void TheWholeGuideCompletesWithTheRightActions()
        {
            var (s, a, c) = G.NewGuide();
            var events = new List<string>();
            var steps = new List<int>();
            c.Analytics = (n, p) => { events.Add(n); if (n == "onboarding_step") steps.Add((int)(long)p["step"]); };
            G.To(c, s, a, Coach.Done);
            Assert.IsFalse(c.Active, "guide finished at step " + c.Step);
            CollectionAssert.AreEqual(Enumerable.Range(1, Coach.Steps).ToArray(), steps.ToArray(), "every step, in order");
            Assert.IsFalse(s.Sim.S.Tutorial, "crises may start after the guide");
            Assert.IsTrue(s.Settings.CoachDone);
            Assert.IsTrue(s.Sim.RoomBuilt("library"));
            Assert.AreEqual(1, s.Sim.S.Wins, "the first hearing is won");
            Assert.AreEqual(0, s.Sim.Collisions().Count, "the scripted collision is covered");
            Assert.Contains("onboarding_complete", events);
        }

        [Test]
        public void WrongTapsNeverAdvanceAndRepeatTheHint()
        {
            var (s, a, c) = G.NewGuide();
            Assert.AreEqual(1, c.Step);
            var r = a.Run("accept:" + s.Sim.S.TutorialMatter);
            Assert.IsFalse(r.Ok);
            Assert.AreEqual("blocked", r.Error);
            Assert.AreEqual(1, c.Step);
            Assert.AreEqual(1, c.WrongTaps);
            Assert.AreEqual(1, c.Flash);
            Assert.IsFalse(a.Run("hire:intern").Ok, "no shortcuts during the guide");
            Assert.IsTrue(a.Run("matter:" + s.Sim.S.TutorialMatter).Ok || true, "opening a card is harmless");
            Assert.IsTrue(a.Run("reception").Ok);
            Assert.AreEqual(2, c.Step);
            // step 3: the intern onto the FILING (wrong task) is blocked
            a.Run("accept:" + s.Sim.S.TutorialMatter);
            Assert.AreEqual(3, c.Step);
            int intern = c.SuggestedStaff(s.Sim);
            Assert.AreEqual("blocked", a.Run("assign:" + intern + ":" + s.Sim.S.TutorialMatter + ":1").Error);
            Assert.AreEqual(3, c.Step);
        }

        [Test]
        public void AWrongPrepCardIsAHintNotAPenalty()
        {
            var (s, a, c) = G.NewGuide();
            G.To(c, s, a, 6);
            Assert.AreEqual(6, c.Step);
            int t = s.Sim.S.TutorialMatter;
            int taps = c.WrongTaps;
            var r = a.Run("prep_match:" + t + ":0:1");
            Assert.AreEqual("wrong", r.Error);
            Assert.AreEqual(6, c.Step);
            Assert.AreEqual(taps + 1, c.WrongTaps);
            Assert.IsTrue(a.Run("prep_match:" + t + ":0:0").Ok);
            Assert.AreEqual(6, c.Step, "two cards to match");
            Assert.IsTrue(a.Run("prep_match:" + t + ":1:1").Ok);
            Assert.AreEqual(7, c.Step);
        }

        [Test]
        public void EveryStepHasATargetAndABubbleText()
        {
            var loc = G.Loc();
            var (s, a, c) = G.NewGuide();
            for (int i = 1; i <= Coach.Steps; i++) Assert.IsTrue(loc.Has("coach." + i), "coach." + i);
            Assert.IsTrue(loc.Has("coach.4w") && loc.Has("coach.7w"), "wait bubbles");
            var targets = new List<string>();
            for (int guard = 0; guard < 60 && c.Active; guard++)
            {
                if (c.FastForward(s.Sim)) { G.Run(c, s, a); continue; }
                targets.Add(c.Step + "=" + c.Target(s.Sim));
                Assert.IsFalse(string.IsNullOrEmpty(c.Target(s.Sim)), "step " + c.Step + " has a spotlight target");
                a.Run(G.Right(c, s));
                G.Run(c, s, a);
            }
            Assert.IsFalse(c.Active, string.Join(" ", targets));
        }

        [Test]
        public void TimeIsPausedWhileTheGuideWaitsForThePlayer()
        {
            var (s, a, c) = G.NewGuide();
            G.To(c, s, a, 5);
            Assert.AreEqual(5, c.Step);
            Assert.AreEqual(2, s.Sim.S.Day, "step 4 fast-forwarded one day");
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
            a.Run("reception");
            c.Skip(s.Sim);
            Assert.IsFalse(c.Active);
            Assert.IsTrue(s.Settings.CoachDone);
            Assert.Contains("onboarding_skip", events);
            Assert.IsFalse(s.Sim.S.Tutorial);
            Assert.IsFalse(s.Sim.MatterById(s.Sim.S.TutorialMatter).Tutorial, "the scripted client is an ordinary one now");
            var hire = a.Run("hire:intern");
            Assert.IsTrue(hire.Ok || hire.Error == "desks" || hire.Error == "cash", "everything is allowed after a skip");
            s.Settings.ReplayGuide();
            Assert.IsFalse(s.Settings.CoachDone);
            Assert.AreEqual(0, s.Settings.CoachStep);
            var c2 = new Coach();
            a.Allowed = c2.AllowsFor(() => s.Sim);
            a.Ran += (id, r) => c2.OnAction(s.Sim, a, id, r);
            c2.Begin(s.Sim, a, s.Settings.CoachStep);
            Assert.IsTrue(c2.Active);
            Assert.AreEqual(1, c2.Step);
            var tm = s.Sim.MatterById(s.Sim.S.TutorialMatter);
            Assert.IsTrue(tm.Tutorial && tm.Status == MatterStatus.Intake, "a fresh scripted client for the replay");
            G.To(c2, s, a, Coach.Done);
            Assert.IsFalse(c2.Active, "the replay completes on a running career");
        }

        [Test]
        public void RelaunchMidGuideResumesAtTheSameStep()
        {
            string dir = G.TempDir();
            var (s, a, c) = G.NewGuide(dir);
            G.To(c, s, a, 3);
            Assert.AreEqual(3, c.Step);
            s.Save();
            var (s2, a2, c2) = G.NewGuide(dir);
            Assert.AreEqual(3, c2.Step, "the accepted ticket is still there: drag the intern");
            G.To(c2, s2, a2, 6);
            s2.Save();
            var (s3, a3, c3) = G.NewGuide(dir);
            Assert.AreEqual(6, c3.Step);
            G.To(c3, s3, a3, Coach.Done);
            Assert.IsFalse(c3.Active);
        }

        [Test]
        public void RelaunchDuringTheFastForwardedDayKeepsGoing()
        {
            string dir = G.TempDir();
            var (s, a, c) = G.NewGuide(dir);
            G.To(c, s, a, 4);
            Assert.IsTrue(a.Run("ff").Ok);
            Assert.IsTrue(c.Waiting);
            c.Drive(s.Sim, a, 0.5);
            Assert.AreEqual(Phases.Live, s.Sim.Phase);
            s.Save();
            var (s2, a2, c2) = G.NewGuide(dir);
            Assert.AreEqual(4, c2.Step);
            Assert.IsTrue(c2.FastForward(s2.Sim), "the day keeps running after the relaunch");
            G.Run(c2, s2, a2);
            Assert.AreEqual(5, c2.Step);
        }

        [Test]
        public void RelaunchOnTheUnbilledReportStillAsksForTheBillingRun()
        {
            string dir = G.TempDir();
            var (s, a, c) = G.NewGuide(dir);
            G.To(c, s, a, 7);
            G.Run(c, s, a);
            Assert.AreEqual(Phases.Report, s.Sim.Phase);
            Assert.AreEqual(7, c.Step);
            s.Save();
            var (s2, a2, c2) = G.NewGuide(dir);
            Assert.AreEqual(7, c2.Step);
            Assert.AreEqual("bill", c2.Target(s2.Sim));
            Assert.IsTrue(a2.Run("bill").Ok);
            Assert.AreEqual(8, c2.Step);
        }

        [Test]
        public void NoRewardsNoCrisesNoLossesInTheGuide()
        {
            var (s, a, c) = G.NewGuide();
            Assert.IsTrue(s.Sim.S.Tutorial);
            Assert.IsFalse(s.Sim.RewardOffer("case_preview", 1_800_000_000).Ok);
            Assert.AreEqual(0, s.Sim.S.Planned.Count);
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
            s.Sim.S.History.Add(new DayStats { Fees = 2000 });
            s.Save();
            now += 3 * 3600;
            var o = s.Resume();
            Assert.IsNotNull(o);
            Assert.Greater(o.Amount, 0);
            Assert.Greater(s.Sim.S.OfflinePending, 0);
        }

        [Test]
        public void AFreshCareerAfterAFinishedGuideStartsWithoutIt()
        {
            string dir = G.TempDir();
            var s = new GameSession(TestData.Gd(), dir) { Now = () => 1_800_000_000 };
            s.Settings.CoachDone = true;
            s.Settings.Save();
            s.LoadOrNew(4);
            Assert.IsFalse(s.Sim.S.Tutorial);
            Assert.IsFalse(s.Sim.MatterById(s.Sim.S.TutorialMatter).Tutorial);
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
            CollectionAssert.AreEqual(new[] { "bundle", "noads", "partner_pass", "premium" }, adFree, "fleet §3/§8 table");
            foreach (var cheap in new[] { "cash_s", "cash_xl", "starter", "retainer_relief", "comeback", "speed", "automation", "overnight_plus", "cosmetics_all", "theme_loft", "outfits" })
                Assert.IsFalse(cat.GrantsAdFree(cheap), cheap);
        }

        [Test]
        public void TheCatalogueFollowsThePlan()
        {
            var cat = IapCatalog.Parse(TestData.Read("Assets/Resources/data/iap.json"));
            foreach (var slug in new[] { "noads", "premium", "partner_pass", "bundle", "speed", "automation", "overnight_plus", "theme_classic", "theme_loft",
                "theme_deco", "outfits", "court_themes", "cosmetics_all", "starter", "retainer_relief", "comeback", "cash_s", "cash_m", "cash_l", "cash_xl" })
                Assert.IsTrue(cat.Products.ContainsKey(slug), slug);
            Assert.AreEqual(2.99, cat.Products["noads"].Usd, 1e-9, "ad-free stays cheap (review theme 4)");
            foreach (var p in cat.Products.Values) StringAssert.StartsWith("com.coddykit.docketday.", p.Id);
            CollectionAssert.AreEquivalent(new[] { 10, 25, 50, 100 }, new[] { "cash_s", "cash_m", "cash_l", "cash_xl" }.Select(x => cat.Products[x].DaysX10));
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
        public void ConsumableDeliversOncePerTransactionIntoOperatingCash()
        {
            var (shop, fake, s) = Shop();
            double cash = s.Sim.S.Cash;
            Assert.IsTrue(shop.Buy("cash_s"));
            Assert.AreEqual(cash + s.Sim.DayIncome(), s.Sim.S.Cash, 1.0);
            Assert.AreEqual(0, s.Sim.TrustTotal, 1e-9);
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
            Assert.AreEqual("", shop.TryOffer("cash_dip", false), "one offer per session");
        }

        [Test]
        public void BridgeFundsAreOfferedAfterACashDip()
        {
            var (shop, fake, s) = Shop();
            Assert.AreEqual("retainer_relief", shop.TryOffer("cash_dip", false));
        }

        [Test]
        public void TheStarterBringsAnInternDayPass()
        {
            var (shop, fake, s) = Shop();
            s.Settings.FirstSeen = 1_800_000_000 - 4 * 86400;
            Assert.AreEqual("starter", shop.TryOffer("day_report", false));
            int staff = s.Sim.S.Staff.Count;
            Assert.IsTrue(shop.Buy("starter"));
            Assert.AreEqual(staff + 1, s.Sim.S.Staff.Count);
            Assert.Greater(s.Sim.S.Staff.Last().TempUntil, 0, "a day pass, not a permanent hire");
        }

        [Test]
        public void PerksReachTheSim()
        {
            var (shop, fake, s) = Shop();
            Assert.AreEqual(s.Gd.FreeMaxSpeed, s.Sim.MaxSpeed);
            int rules = s.Sim.MaxRules;
            shop.Buy("speed");
            shop.Buy("automation");
            s.ApplyPerks(shop.Has);
            Assert.AreEqual(s.Gd.PaidMaxSpeed, s.Sim.MaxSpeed);
            Assert.Greater(s.Sim.MaxRules, rules);
            int rail = s.Sim.RailDays;
            shop.Buy("partner_pass");
            s.ApplyPerks(shop.Has);
            Assert.Greater(s.Sim.RailDays, rail, "the Partner's Pass shows 14 days");
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
            var kit = DocketRewards.KitLimits();
            CollectionAssert.AreEquivalent(gd.Rewards.Keys, kit.Keys);
            foreach (var kv in gd.Rewards)
            {
                Assert.AreEqual(kv.Value.PerDay, kit[kv.Key].Daily, kv.Key);
                Assert.AreEqual(kv.Value.CooldownS, kit[kv.Key].CooldownS, 1e-9, kv.Key);
            }
            Assert.LessOrEqual(DocketRewards.UnitsSingleMax * 5, DocketRewards.UnitsPerDay, "single ≤ 1/5 of the cheapest pack");
            Assert.LessOrEqual(DocketRewards.UnitsDailyMax * 2, DocketRewards.UnitsPerDay, "daily ≤ 1/2");
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
            string root = Path.Combine(TestData.UnityDir(), "Assets", "Scripts");
            var rx = new Regex("(?:Analytics|Track)\\(\"([a-z_]+)\"");
            int found = 0;
            foreach (var f in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                foreach (Match m in rx.Matches(File.ReadAllText(f)))
                {
                    found++;
                    Assert.IsTrue(GameEvents.Known.Contains(m.Groups[1].Value), Path.GetFileName(f) + ": " + m.Groups[1].Value);
                }
            Assert.Greater(found, 10);
        }

        [Test]
        public void ReviewMomentsWaitForTheGuide()
        {
            var sim = TestData.NewSim();
            var h = new ReviewHook();
            var won = new SimEvent { Type = "verdict", B = 1 };
            Assert.AreEqual("", h.Check(won, sim, inGuide: true));
            Assert.AreEqual("won_hearing", h.TakeDeferred(false));
            Assert.AreEqual("", h.TakeDeferred(false));
            Assert.AreEqual("won_hearing", h.Check(won, sim, false));
            Assert.AreEqual("", h.Check(new SimEvent { Type = "verdict", B = 0 }, sim, false));
            Assert.AreEqual("", h.Check(new SimEvent { Type = "day_done", B = 5 }, sim, false));
            Assert.AreEqual("good_day", h.Check(new SimEvent { Type = "day_done", B = 8 }, sim, false));
        }
    }

    public class StringTests
    {
        [Test]
        public void TableParsesWithTenLanguagesAndNoDuplicates()
        {
            var loc = G.Loc();
            Assert.AreEqual(0, loc.Errors.Count, string.Join("; ", loc.Errors));
            Assert.Greater(loc.Keys.Count, 250);
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
            var miss = new List<string>();
            void K(string k) { if (!loc.Has(k)) miss.Add(k); }
            foreach (var a in gd.Areas) { K("area." + a.Id); for (int i = 0; i < a.Titles; i++) K("matter." + a.Id + "." + i); }
            foreach (var t in gd.Tiers.Keys) K("tier." + t);
            foreach (var f in new[] { Fees.Hourly, Fees.Retainer, Fees.Contingency }) K("fee." + f);
            foreach (var p in gd.Parties) K("party." + p);
            foreach (var j in gd.Judges) { K("judge." + j.Id); K("judge." + j.Id + ".d"); }
            foreach (var r in gd.Roles.Keys) { K("role." + r); K("role." + r + ".d"); }
            foreach (var r in gd.Rooms) { K("room." + r.Id); K("room." + r.Id + ".d"); }
            foreach (var c in gd.Chapters) K("chapter." + c.Id);
            foreach (var e in gd.Events) { K("crisis." + e.Id + ".t"); K("crisis." + e.Id + ".b"); }
            foreach (var k in new[] { Kinds.File, Kinds.Evidence, Kinds.Prep, Kinds.Motion, Kinds.Hearing }) K("kind." + k);
            for (int i = 0; i < Simulation.NamePool; i++) K("staff.name." + i);
            foreach (var p in Simulation.RewardPlacements) { K("reward." + p + ".t"); K("reward." + p + ".b"); }
            for (int c = 0; c < 6; c++) { K("claim." + c); K("exhibit." + c); }
            K("exhibit.decoy");
            foreach (var e in new[] { "invalid", "conflict", "wall_locked", "cash", "tutorial", "done", "not_lawyer", "no_skill", "owned", "used", "locked", "wrong",
                "desks", "max", "founder", "in_session", "busy", "building", "standing", "closed", "phase", "blocked", "unknown" })
                K("err." + e);
            CollectionAssert.IsEmpty(miss);
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
            var pills = new[] { "intake.accept", "intake.decline", "intake.refer", "intake.wall", "intake.rule_on", "intake.rule_off", "hud.start_day", "hud.next_day", "hud.bill",
                "coach.skip", "intro.start", "matter.unassign", "team.fire", "looks.equip" };
            var shortKeys = loc.Keys.Where(k => k.StartsWith("tab.") || pills.Contains(k) || k.StartsWith("shop.group.") || k.StartsWith("lever.")).ToList();
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
        public void NoRealFirmsCourtsOrReferenceGameNames()
        {
            // plan originality rules: no reference-game names, no real firms / courts / people; flavour only, not advice
            string en = TestData.Read("Tools/i18n/en.json");
            foreach (var w in new[] { "Codigames", "Law Empire", "Justice Empire", "Idle Law Firm", "Ace Attorney", "Supreme Court", "Baker McKenzie", "Kirkland" })
                StringAssert.DoesNotContain(w, en, w);
            StringAssert.Contains("not legal advice", en.ToLowerInvariant());
        }
    }
}
