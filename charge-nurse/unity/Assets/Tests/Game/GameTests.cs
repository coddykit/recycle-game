// Charge Nurse — game layer EditMode tests (NUnit; also under plain .NET via Tools/dotnet_check.sh).
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
using ChargeNurse.Sim;
using ChargeNurse.Sim.Tests;

namespace ChargeNurse.Game.Tests
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

        /// <summary>Lets the world catch up for a guide step that waits for it (van driving, count line, baler).</summary>
        public static void Run(Coach c, GameSession s, GameActions a, int frames = 400)
        {
            c.Drive(s.Sim, a, 0.01);
            for (int i = 0; i < frames && c.Active && c.FastForward(s.Sim); i++) c.Drive(s.Sim, a, 0.1);
            c.Drive(s.Sim, a, 0.01);
        }

        public static Loc Loc() => ChargeNurse.Game.Loc.FromCsv(TestData.Read("Assets/Resources/i18n/strings.csv"));
    }

    public class GuideTests
    {
        static readonly string[] Right = { "", "select_patient:1", "tag:1:0", "assign:0", "discharge:1", "clean:0", "ambulance:accept", "buy:room:xray", "buy:hire:porter", "lens", "handover" };

        [Test]
        public void TheWholeGuideCompletesWithTheRightActions()
        {
            var (s, a, c) = G.NewGuide();
            var events = new List<string>();
            c.Analytics = (n, p) => events.Add(n);
            for (int step = 1; step <= Coach.Steps; step++)
            {
                if (!c.Active) break;
                G.Run(c, s, a);   // the world catches up where the guide waits for it (treatment, the shift to 18:30)
                Assert.AreEqual(step, c.Step, "step order");
                var r = a.Run(Right[step]);
                Assert.IsTrue(r.Ok, "step " + step + " action " + Right[step] + " failed: " + r.Error);
            }
            G.Run(c, s, a);
            Assert.IsFalse(c.Active, "guide finished at step " + c.Step);
            Assert.IsFalse(s.Sim.S.Tutorial, "crises may start after the guide");
            Assert.IsTrue(s.Settings.CoachDone);
            Assert.AreEqual(Phases.Report, s.Sim.Phase, "the first full shift ends with its report");
            Assert.IsFalse(s.Sim.S.History.Last().Rushed, "a calm handover");
            Assert.AreEqual(2, s.Sim.Team(Teams.Porter));
            Assert.IsTrue(s.Sim.Built("xray"));
            Assert.Greater(s.Sim.S.History.Last().Treated, 5, "the guide's shift treated walk-ins too");
            Assert.Contains("onboarding_complete", events);
            Assert.AreEqual(Coach.Steps, events.Count(e => e == "onboarding_step"));
        }

        [Test]
        public void WrongTapsNeverAdvanceAndRepeatTheHint()
        {
            var (s, a, c) = G.NewGuide();
            Assert.AreEqual(1, c.Step);
            var r = a.Run("buy:hire:porter");
            Assert.IsFalse(r.Ok);
            Assert.AreEqual("blocked", r.Error);
            Assert.AreEqual(1, c.Step);
            Assert.AreEqual(1, c.WrongTaps);
            Assert.AreEqual(1, c.Flash);
            Assert.IsTrue(a.Run("select_patient:1").Ok);
            Assert.AreEqual(2, c.Step);
            // a wrong colour explains why and repeats the step
            Assert.IsFalse(a.Run("tag:1:2").Ok);
            Assert.AreEqual(2, c.Step);
            Assert.AreEqual("coach.2.wrong", c.BubbleKey);
            Assert.AreEqual(-1, s.Sim.P(1).Tag, "the wrong colour was not applied");
            Assert.IsTrue(a.Run("tag:1:0").Ok);
            Assert.AreEqual(3, c.Step);
            Assert.AreEqual("coach.3", c.BubbleKey);
        }

        [Test]
        public void EveryStepHasATargetAndABubbleText()
        {
            var loc = G.Loc();
            for (int i = 1; i <= Coach.Steps; i++)
            {
                Assert.IsFalse(string.IsNullOrEmpty(Coach.Targets[i]), "target " + i);
                Assert.IsFalse(string.IsNullOrEmpty(Coach.Need[i]), "action " + i);
                Assert.IsTrue(loc.Has("coach." + i), "coach." + i);
            }
            Assert.IsTrue(loc.Has("coach.2.wrong"));
        }

        [Test]
        public void TimeIsPausedWhileTheGuideWaitsForThePlayer()
        {
            var (s, a, c) = G.NewGuide();
            for (int i = 1; i <= 3; i++) a.Run(Right[i]);
            Assert.AreEqual(4, c.Step);
            c.Drive(s.Sim, a, 0.01);
            Assert.IsTrue(c.FastForward(s.Sim), "the treatment runs fast");
            G.Run(c, s, a);
            Assert.AreEqual(Stage.Ready, s.Sim.P(1).Stage);
            Assert.IsFalse(c.FastForward(s.Sim));
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
            a.Run("select_patient:1");
            c.Skip(s.Sim);
            Assert.IsFalse(c.Active);
            Assert.IsTrue(s.Settings.CoachDone);
            Assert.IsFalse(s.Sim.S.Tutorial);
            Assert.IsTrue(s.Sim.S.ArrivalsOn, "walk-ins start after a skip");
            Assert.Contains("onboarding_skip", events);
            Assert.IsTrue(a.Run("buy:hire:nurse").Ok, "everything is allowed after a skip");
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
        public void RelaunchMidStepGoesBackToTappingThePatient()
        {
            string dir = G.TempDir();
            var (s, a, c) = G.NewGuide(dir);
            a.Run(Right[1]); a.Run(Right[2]);
            Assert.AreEqual(3, c.Step);
            s.Save();
            var (s2, a2, c2) = G.NewGuide(dir);   // relaunch: the selection (UI state) is gone
            Assert.AreEqual(1, c2.Step, "tap the patient again, never stuck at 'send to the bay'");
            Assert.IsTrue(a2.Run("select_patient:1").Ok);
            Assert.AreEqual(3, c2.Step, "already green: straight to the bay");
            Assert.IsTrue(a2.Run("assign:1").Ok);
            Assert.AreEqual(4, c2.Step);
        }

        [Test]
        public void RelaunchDuringTheTreatmentContinues()
        {
            string dir = G.TempDir();
            var (s, a, c) = G.NewGuide(dir);
            for (int i = 1; i <= 3; i++) a.Run(Right[i]);
            s.Save();
            var (s2, a2, c2) = G.NewGuide(dir);
            Assert.AreEqual(4, c2.Step);
            G.Run(c2, s2, a2);
            Assert.IsTrue(a2.Run("discharge:1").Ok);
            Assert.AreEqual(5, c2.Step);
        }

        [Test]
        public void NoRewardsAndNoCrisesInTheGuide()
        {
            var (s, a, c) = G.NewGuide();
            Assert.IsTrue(s.Sim.S.Tutorial);
            Assert.IsFalse(s.Sim.RewardOffer("daily_bonus", 1_800_000_000).Ok);
            Assert.AreEqual(0, s.Sim.S.Planned.Count + s.Sim.S.Events.Count);
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
            CollectionAssert.AreEqual(new[] { "bundle", "noads", "premium" }, adFree, "fleet §3/§8 table (chief_pass joins in v1.1)");
            foreach (var cheap in new[] { "cash_s", "cash_xl", "starter", "speed", "theme_modern", "theme_retro", "theme_pediatric" })
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
            long now = 1_800_000_000 + 4 * 86400L;
            s.Now = () => now;
            Assert.AreEqual("starter", shop.TryOffer("day_report", false));
            Assert.AreEqual("", shop.WhyNot("starter"));
            Assert.AreEqual("", shop.TryOffer("day_report", false), "one offer per session");
            int porters = s.Sim.Team(Teams.Porter);
            Assert.IsTrue(shop.Buy("starter"));
            Assert.AreEqual(porters + 1, s.Sim.Team(Teams.Porter), "the starter's extra porter");
            Assert.IsFalse(shop.AdFree, "the starter never removes ads");
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
        public void TheEditionGivesItsStarterSuppliesOncePerDevice()
        {
            var (shop, fake, s) = Shop();
            double cash = s.Sim.S.Cash;
            Assert.IsTrue(shop.Buy("premium"));
            Assert.IsTrue(shop.AdFree);
            Assert.AreEqual(cash + s.Sim.DayIncome(), s.Sim.S.Cash, 1.0);
            shop.Restore();
            Assert.AreEqual(cash + s.Sim.DayIncome(), s.Sim.S.Cash, 1.0, "a restore never pays twice");
            Assert.AreEqual("owned", shop.WhyNot("premium"));
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
            var kit = NurseRewards.KitLimits();
            CollectionAssert.AreEquivalent(gd.Rewards.Keys, kit.Keys);
            foreach (var kv in gd.Rewards)
            {
                Assert.AreEqual(kv.Value.PerDay, kit[kv.Key].Daily, kv.Key);
                Assert.AreEqual(kv.Value.CooldownS, kit[kv.Key].CooldownS, 1e-9, kv.Key);
            }
            Assert.LessOrEqual(NurseRewards.UnitsSingleMax * 5, NurseRewards.UnitsPerDay, "single ≤ 1/5 of the cheapest pack");
            Assert.LessOrEqual(NurseRewards.UnitsDailyMax * 2, NurseRewards.UnitsPerDay, "daily ≤ 1/2");
            var cat = IapCatalog.Parse(TestData.Read("Assets/Resources/data/iap.json"));
            int cheapest = cat.Products.Values.Where(p => p.Consumable && !p.OfferOnly).Min(p => p.DaysX10);
            Assert.AreEqual(10, cheapest, "cash_s = 1 day = 1000 units");
        }

        [Test]
        public void EveryAnalyticsEventTheGameSendsIsInTheCatalogue()
        {
            foreach (var n in new[] { "onboarding_step", "onboarding_complete", "onboarding_skip", "save_unreadable", "rewarded_completed", "rewarded_skipped_paid_user", "purchase_success", "lens_used" })
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
            Assert.AreEqual("", h.Check(good, sim, inGuide: true));
            Assert.AreEqual("good_shift", h.TakeDeferred(false));
            Assert.AreEqual("", h.TakeDeferred(false));
            Assert.AreEqual("good_shift", h.Check(good, sim, false));
            Assert.AreEqual("wing", h.Check(new SimEvent { Type = "wing", Text = "town" }, sim, false));
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
            foreach (var w in gd.Wings) { Assert.IsTrue(loc.Has("wing." + w.Id), w.Id); Assert.IsTrue(loc.Has("wing." + w.Id + ".d"), w.Id); }
            foreach (var r in gd.Research) { Assert.IsTrue(loc.Has("research." + r.Id + ".t"), r.Id); Assert.IsTrue(loc.Has("research." + r.Id + ".d"), r.Id); }
            foreach (var b in new[] { "care", "flow", "comfort", "resilience" }) Assert.IsTrue(loc.Has("research.branch." + b), b);
            foreach (var r in gd.Research) Assert.IsTrue(new[] { "care", "flow", "comfort", "resilience" }.Contains(r.Branch), r.Id);
            foreach (var e in gd.Events) { Assert.IsTrue(loc.Has("crisis." + e.Id + ".t"), e.Id); Assert.IsTrue(loc.Has("crisis." + e.Id + ".b"), e.Id); }
            foreach (var st in Stage.All) Assert.IsTrue(loc.Has("stage." + st), st);
            foreach (var st in new[] { BedState.Clean, BedState.Busy, BedState.Dirty, BedState.Cleaning, BedState.Closed }) Assert.IsTrue(loc.Has("bedstate." + st), st);
            foreach (var t in Teams.All) { Assert.IsTrue(loc.Has("team." + t), t); Assert.IsTrue(loc.Has("team." + t + ".d"), t); }
            foreach (var t in Sev.Ids) Assert.IsTrue(loc.Has("tag." + t), t);
            for (int sv = 0; sv < Sev.N; sv++) for (int k = 0; k < 3; k++) Assert.IsTrue(loc.Has("clue." + sv + "." + k));
            foreach (var w in new[] { "obs", "general", "surgical", "icu" }) Assert.IsTrue(loc.Has("ward." + w), w);
            foreach (var r in gd.Rooms.Keys) { Assert.IsTrue(loc.Has("room." + r), r); Assert.IsTrue(loc.Has("room." + r + ".d"), r); }
            foreach (var q in QueueRules.All) Assert.IsTrue(loc.Has("rule." + q), q);
            foreach (var k in new[] { "smooth", "discharge", "dirty", "beds", "porters", "or", "doctors", "bays", "triage", "tests", "reception" })
            { Assert.IsTrue(loc.Has("lens." + k), k); Assert.IsTrue(loc.Has("lens.short." + k), k); }
            foreach (var p in Simulation.RewardPlacements) { Assert.IsTrue(loc.Has("reward." + p + ".t"), p); Assert.IsTrue(loc.Has("reward." + p + ".b"), p); }
            foreach (var ph in new[] { "plan", "live", "report", "handover" }) Assert.IsTrue(loc.Has("phase." + ph), ph);
            foreach (var t in new[] { "board", "staff", "build", "research", "shop" }) Assert.IsTrue(loc.Has("tab." + t), t);
            foreach (var t in new[] { "gold_theme", "theme_modern", "theme_retro", "theme_pediatric" }) Assert.IsTrue(loc.Has("theme." + t), t);
            foreach (var e in new[] { "invalid", "too_late", "untagged", "not_ready", "bay_dirty", "bay_busy", "no_patient", "not_dirty", "no_call", "window", "phase",
                "unknown", "max", "cash", "owned", "wing", "locked", "reputation", "context", "not_built", "research", "busy", "running", "requires" })
                Assert.IsTrue(loc.Has("err." + e), e);
            // every first-time tip the coach can name
            var tipRx = new Regex("\"(tip\\.[a-z_]+)\"");
            foreach (Match m in tipRx.Matches(TestData.Read("Assets/Scripts/Game/Coach.cs"))) Assert.IsTrue(loc.Has(m.Groups[1].Value), m.Groups[1].Value);
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
            var shortKeys = loc.Keys.Where(k => k.StartsWith("tab.") || k.StartsWith("tag.") || k.StartsWith("lens.short.") || k == "hud.start_day" || k == "hud.handover" || k == "hud.next_day"
                || k == "amb.accept" || k == "amb.divert" || k == "coach.skip" || k == "intro.start" || k.StartsWith("shop.group.")).ToList();
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
