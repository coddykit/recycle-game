// CoddyKit (Unity) EditMode tests: house ads (relatedness, pacing, banner rotation, sizing, texts) and the rewarded flow.
// Engine-free (see CoreTests.cs header).
using System.Collections.Generic;
using NUnit.Framework;
using CoddyKit;
using CoddyKit.Ads;

namespace CoddyKit.Tests
{
    public class HouseAdsTests
    {
        const string Host = "volt-city";

        static Dictionary<string, object> Creative(string id, bool hero = true)
        {
            var c = new Dictionary<string, object>
            {
                { "promotedAppId", id },
                { "playUrl", "https://play.google.com/store/apps/details?id=com.coddykit." + id.Replace("-", "") },
                { "iconUrl", "https://cdn.example/" + id + ".png" },
                { "title", id },
            };
            if (hero) c["heroImageUrls"] = new List<object> { "https://cdn.example/" + id + "-1.webp" };
            return c;
        }

        static Dictionary<string, object> Config(List<object> banner, List<object> inter, string version = "v1")
        {
            return new Dictionary<string, object>
            {
                { "adsEnabled", true }, { "version", version }, { "rewardedEnabled", true },
                { "houseAds", new Dictionary<string, object>
                    {
                        { "banner", new Dictionary<string, object> { { "enabled", true }, { "perAppBudget", 2L }, { "resetDays", 7L }, { "sequence", banner } } },
                        { "interstitial", new Dictionary<string, object> { { "enabled", true }, { "perAppBudget", 3L }, { "resetDays", 7L }, { "sequence", inter } } },
                    } },
            };
        }

        [Test]
        public void RelatedOrderPoolsFormatsDropsHostAndKids()
        {
            var cfg = Config(
                new List<object> { Creative("money-district"), Creative("volt-city"), Creative("block-forge") },
                new List<object> { Creative("disaster-command"), Creative("money-district"), Creative("unknown-game") });
            Assert.IsTrue(HouseRelated.Apply(cfg, Host));
            Assert.IsFalse(HouseRelated.Apply(cfg, Host), "idempotent");
            var seq = MiniJson.Arr(MiniJson.Obj(MiniJson.Obj(cfg, "houseAds"), "banner"), "sequence");
            var ids = new List<string>();
            foreach (var o in seq) ids.Add(MiniJson.Str(o, "promotedAppId"));
            Assert.AreEqual(new[] { "disaster-command", "money-district", "unknown-game" }, ids.ToArray());
            Assert.Less(HouseRelated.Rank(Host, "disaster-command"), HouseRelated.Rank(Host, "money-district"));
            Assert.AreEqual(1000, HouseRelated.Rank(Host, "unknown-game"));
        }

        [Test]
        public void FullFleetListKeptWholeAndInterstitialTotalCapped()
        {
            // owner 2026-10-28: the backend serves EVERY other game; the client keeps them all (TopN = 0), relatedness order,
            // ids missing from this table last (never cut), never the host; the interstitial total stays budget × min(n, 10).
            Assert.AreEqual(0, HouseRelated.TopN);
            var known = HouseRelated.Related[Host];
            Assert.GreaterOrEqual(known.Length, 20);
            var backend = new List<object>();
            for (int i = known.Length - 1; i >= 0; i--) backend.Add(Creative(known[i]));
            backend.Insert(3, Creative(Host));
            backend.Insert(5, Creative("block-forge"));
            backend.Add(Creative("zz-newer-game"));
            backend.Add(Creative("aa-newer-game"));
            var cfg = Config(new List<object>(backend), new List<object>(backend));
            Assert.IsTrue(HouseRelated.Apply(cfg, Host));
            var want = new List<string>(known) { "zz-newer-game", "aa-newer-game" };
            foreach (string f in new[] { "banner", "interstitial" })
            {
                var ids = new List<string>();
                foreach (var o in MiniJson.Arr(MiniJson.Obj(MiniJson.Obj(cfg, "houseAds"), f), "sequence")) ids.Add(MiniJson.Str(o, "promotedAppId"));
                Assert.AreEqual(want.ToArray(), ids.ToArray(), f + ": every game kept, in order");
            }
            var cfgs = new HouseAdsConfig();
            cfgs.Apply(cfg, "");
            var seq = cfgs.Sequence("interstitial", Host);
            Assert.AreEqual(want.Count, seq.Count);
            var p = new HouseInterstitialPacing(null);
            double now = 1_800_000_000;
            int shown = 0;
            while (shown < 200 && p.Next(seq, 3, 7, now) != null) { p.MarkShown(now); shown++; }
            Assert.AreEqual(3 * HouseInterstitialPacing.HouseCapGames, shown, "30 per round, not 3 × " + seq.Count);
        }

        [Test]
        public void ConfigVersionAndKillSwitches()
        {
            var h = new HouseAdsConfig();
            var off = new Dictionary<string, object> { { "adsEnabled", false }, { "rewardedEnabled", false }, { "rcAdTracking", false } };
            Assert.IsFalse(h.Apply(off, ""));
            Assert.IsNull(h.Config);
            Assert.IsFalse(h.RewardedEnabled);
            Assert.IsFalse(h.RcAdTracking);
            Assert.IsTrue(h.Apply(Config(new List<object>(), new List<object>(), "v2"), "v1"), "version change");
            Assert.IsFalse(h.Apply(Config(new List<object>(), new List<object>(), "v2"), "v2"));
        }

        [Test]
        public void InterstitialPacingGraces()
        {
            var p = new HouseInterstitialPacing(null);
            double now = 1_800_000_000;
            Assert.AreEqual("install_grace", p.Blocker(now, false, false));
            for (int i = 0; i < 600; i++) p.Tick(1.0);
            Assert.AreEqual("", p.Blocker(now, false, false), "600 s total + 600 s session");
            Assert.AreEqual("ad_free", p.Blocker(now, true, false));
            p.MarkShown(now);
            Assert.AreEqual("min_gap", p.Blocker(now + 100, false, false));
            Assert.AreEqual("", p.Blocker(now + 240, false, false));
            p.RewardedCompleted(now + 240);
            Assert.AreEqual("quiet_after_rewarded", p.Blocker(now + 300, false, false));
            Assert.AreEqual("", p.Blocker(now + 240 + 240 + 1, false, false));
            p.Tick(3600); // a resume after an hour is not play time
            Assert.AreEqual(600.0, p.PlayS, 1e-9);
        }

        [Test]
        public void InterstitialRotationBudgetAndReset()
        {
            var cfgs = new HouseAdsConfig();
            cfgs.Apply(Config(new List<object>(), new List<object> { Creative("disaster-command"), Creative("train-baron"), Creative("volt-city"), Creative("no-art", false) }), "");
            var seq = cfgs.Sequence("interstitial", Host);
            Assert.AreEqual(3, seq.Count, "host dropped; a creative with only an icon stays eligible");
            var p = new HouseInterstitialPacing(null);
            double now = 1_800_000_000;
            var seen = new List<string>();
            for (int i = 0; i < 9; i++)
            {
                var c = p.Next(seq, 3, 7, now);
                Assert.IsNotNull(c, "shown " + i);
                seen.Add(MiniJson.Str(c, "promotedAppId"));
                p.MarkShown(now);
            }
            Assert.AreEqual("train-baron", seen[0], "most related first (fleet table: train-baron 5 > disaster-command 4)");
            Assert.AreNotEqual(seen[0], seen[1], "one step further every card");
            Assert.IsNull(p.Next(seq, 3, 7, now), "3 × 3 spent");
            Assert.IsNotNull(p.Next(seq, 3, 7, now + 7 * 86400), "reset after resetDays");
            p.OnVersionChanged("v9");
            Assert.AreEqual(0, p.Count);
        }

        [Test]
        public void BannerRotationTilesBudgetGrace()
        {
            var r = new HouseBannerRotation { OnPhone = true };
            var ready = new List<Dictionary<string, object>> { Creative("a"), Creative("b"), Creative("c") };
            var two = r.Current(ready, 2, 2);
            Assert.AreEqual(2, two.Count);
            Assert.AreNotEqual(MiniJson.Str(two[0], "promotedAppId"), MiniJson.Str(two[1], "promotedAppId"));
            var fresh = r.OnShown(two, 100, true);
            Assert.AreEqual(2, fresh.Count);
            Assert.AreEqual(0, fresh[0].slot);
            Assert.AreEqual(0, r.OnShown(two, 101, false).Count, "relayout is not an impression");
            Assert.IsFalse(r.TapAccepted(100.5), "tap grace");
            Assert.IsTrue(r.TapAccepted(101.6));
            r.Tick(45);
            var next = r.Current(ready, 2, 2);
            Assert.AreEqual("c", MiniJson.Str(next[0], "promotedAppId"), "rotation advanced by the tile count");
            Assert.AreEqual(4, r.IndexToPersist, "next launch starts after the creatives shown");
            r.Input(10, true, false);
            Assert.IsFalse(r.Calm(20), "finger down");
            r.Input(20, false, true);
            Assert.IsFalse(r.Calm(22));
            Assert.IsTrue(r.Calm(23.1));
            var back = new HouseBannerRotation();
            back.FromJson(MiniJson.TryParseObject(MiniJson.Write(r.ToJson())));
            Assert.AreEqual(r.IndexToPersist, back.Index);
        }

        [Test]
        public void BannerRotationStartsANewRoundWhenBudgetsRunOut()
        {
            var r = new HouseBannerRotation();
            var ready = new List<Dictionary<string, object>> { Creative("a"), Creative("b") };
            r.Counts["a"] = 2;
            r.Counts["b"] = 2;
            Assert.AreEqual(1, r.Current(ready, 1, 2).Count, "never an empty bar because of the budget");
        }

        [Test]
        public void BannerSizeFollowsTheFleetBar()
        {
            var m = BannerMetrics.Compute(720, 1560, 1.75f); // fleet baseline: 1 dp = 1.75 canvas px
            Assert.AreEqual(102f, m.Height);
            Assert.LessOrEqual(m.Share, 0.12f);
            Assert.AreEqual(77f, m.BtnH, "44 dp Install");
            var capped = BannerMetrics.Compute(1600, 400, 1.75f); // short landscape: 12 % cap
            Assert.AreEqual(48f, capped.Height);
            Assert.IsTrue(HouseBannerRotation.Dual(1600, 720, 1.75f));
            Assert.IsFalse(HouseBannerRotation.Dual(720, 1560, 1.75f));
            Assert.IsTrue(HouseBannerRotation.Dual(1100, 1560, 1.75f), ">= 600 dp portrait tablet");
        }

        [Test]
        public void SloganInstallShortTitleAndLinks()
        {
            var c = Creative("disaster-command");
            c["sloganI18n"] = new Dictionary<string, object> { { "en", "Lead the rescue" }, { "de", "Kostenlos spielen" }, { "ja", "救助を指揮せよ、街を守れ、今すぐ始めよう" } };
            Assert.AreEqual("Lead the rescue", HouseText.SloganFor(c, "en"));
            Assert.AreEqual("", HouseText.SloganFor(c, "de"), "no free promise");
            Assert.AreEqual("", HouseText.SloganFor(c, "ja"), "CJK > 17");
            Assert.AreEqual("", HouseText.SloganFor(c, "fr"), "never another language");
            Assert.AreEqual("Laden", HouseText.InstallText("de"));
            Assert.AreEqual("इंस्टॉल", HouseText.InstallText("hi"));
            Assert.AreEqual("Herunterladen", HouseText.InstallText("de", "Herunterladen"));
            Assert.AreEqual("Disaster Command", HouseText.ShortTitle(c, "Disaster Command: Evacuation", "en"));
            Assert.AreEqual("", HouseText.ShortTitle(c, "Volt", "en"));
            string link = HouseText.StoreLink("https://play.google.com/store/apps/details?id=x", null, Host, "banner");
            StringAssert.Contains("&referrer=utm_source%3Dvolt-city%26utm_medium%3Dhouse_banner%26utm_campaign%3Dcoddyads", link);
            Assert.AreEqual("pt", HouseText.Lang2("pt-BR"));
        }

        [Test]
        public void HeroFramesPreferLanguageAndOrientation()
        {
            var c = Creative("x");
            c["heroI18n"] = new Dictionary<string, object>
            {
                { "de", new Dictionary<string, object> { { "landscape", new List<object> { "https://h/de-l.webp" } } } },
                { "en", new Dictionary<string, object> { { "portrait", new List<object> { "https://h/en-p1.webp", "https://h/en-p2.webp", "https://h/en-p1.webp", "http://h/3", "https://h/4" } } } },
            };
            Assert.AreEqual(new[] { "https://h/en-p1.webp", "https://h/en-p2.webp", "http://h/3" }, HouseText.HeroUrls(c, "de", true).ToArray());
            Assert.AreEqual(new[] { "https://h/de-l.webp" }, HouseText.HeroUrls(c, "de", false).ToArray());
            Assert.AreEqual(new[] { "https://cdn.example/x-1.webp" }, HouseText.HeroUrls(Creative("x"), "de", true).ToArray());
        }
    }

    public class RewardedTests
    {
        ManualClock clock;
        FakeConsent consent;
        FakeRewarded ads;
        NullAnalytics analytics;
        FakePurchases rc;
        RcAdTracker tracker;
        RewardedController ctl;
        bool adFree;

        [SetUp]
        public void SetUp()
        {
            adFree = false; // NUnit reuses the fixture instance across tests
            clock = new ManualClock();
            consent = new FakeConsent();
            ads = new FakeRewarded();
            analytics = new NullAnalytics();
            rc = new FakePurchases();
            tracker = new RcAdTracker { Ready = () => true, Sink = e => rc.TrackAdEvent(e) };
            var rules = new RewardedRules(clock) { BudgetSingle = 20, BudgetDaily = 50 };
            rules.Limits["shift_bonus"] = new RewardedLimit(3, 600);
            ctl = new RewardedController(rules, consent, ads, analytics, tracker) { UnitId = RewardedController.TestUnitAndroid, AdFree = () => adFree };
        }

        [Test]
        public void NothingLoadsBeforeConsentAndInit()
        {
            consent.Can = false;
            ctl.Start();
            Assert.AreEqual(0, ads.InitCalls, "no consent -> no MobileAds.Initialize");
            Assert.AreEqual(0, ads.LoadCalls);
            Assert.IsFalse(ctl.Ready("shift_bonus"));
        }

        [Test]
        public void GrantOnceAfterEarnedAndClosedInEitherOrder()
        {
            ctl.Start();
            Assert.AreEqual(1, ads.InitCalls);
            Assert.AreEqual(1, ads.LoadCalls);
            Assert.IsTrue(ctl.Ready("shift_bonus", 30));
            int got = 0, grants = 0;
            Assert.IsTrue(ctl.Show("shift_bonus", 30, u => { got = u; grants++; }));
            Assert.AreEqual(20, got, "single cap");
            Assert.AreEqual(1, grants);
            ads.EarnBeforeClose = false;
            clock.Advance(601);
            Assert.IsTrue(ctl.Show("shift_bonus", 30, u => { got = u; grants++; }));
            Assert.AreEqual(2, grants);
            Assert.AreEqual(20, got);
            clock.Advance(601);
            Assert.IsTrue(ctl.Ready("shift_bonus", 30), "daily budget 50 - 40 = 10 left");
            Assert.IsTrue(ctl.Show("shift_bonus", 30, u => { got = u; grants++; }));
            Assert.AreEqual(10, got);
            Assert.IsFalse(ctl.Ready("shift_bonus", 30), "3 / day");
            CollectionAssert.Contains(analytics.Log, RewardedEvents.OfferShown);
            CollectionAssert.Contains(analytics.Log, RewardedEvents.Completed);
        }

        [Test]
        public void ClosedWithoutEarningGrantsNothingAndCooldownHolds()
        {
            ctl.Start();
            ads.CloseWithoutEarn = true;
            int grants = 0;
            bool closed = false;
            Assert.IsTrue(ctl.Show("shift_bonus", 5, u => grants++, () => closed = true));
            Assert.AreEqual(0, grants);
            Assert.IsTrue(closed);
            Assert.IsTrue(ctl.Ready("shift_bonus", 5), "a skipped video does not use the cap");
        }

        [Test]
        public void AdFreeGetsTheCappedRewardWithoutVideo()
        {
            adFree = true;
            ctl.Start();
            int got = 0;
            Assert.IsTrue(ctl.Show("shift_bonus", 30, u => got = u));
            Assert.AreEqual(20, got);
            Assert.AreEqual(0, ads.ShowCalls);
            CollectionAssert.Contains(analytics.Log, RewardedEvents.SkippedPaidUser);
            Assert.AreEqual(0, rc.AdEvents.FindAll(e => e.Kind == "displayed").Count, "video-less rewards never reach RC");
        }

        [Test]
        public void RemoteSwitchHidesOffersButNotForAdFree()
        {
            ctl.Start();
            ctl.SetRemote(false);
            Assert.IsFalse(ctl.Ready("shift_bonus"));
            adFree = true;
            Assert.IsTrue(ctl.Ready("shift_bonus"));
        }

        [Test]
        public void FailedLoadRetriesAfterSixtySeconds()
        {
            var scheduled = new List<double>();
            ctl.Schedule = (s, a) => scheduled.Add(s);
            ads.FailLoad = true;
            ctl.Start();
            Assert.AreEqual(new[] { 60.0 }, scheduled.ToArray());
            Assert.AreEqual(1, rc.AdEvents.FindAll(e => e.Kind == "failed_to_load").Count);
        }

        [Test]
        public void RcTrackerDedupesQueuesAndKillSwitch()
        {
            bool ready = false;
            var sent = new List<AdTrackEvent>();
            var t = new RcAdTracker { Ready = () => ready, Sink = e => { sent.Add(e); return true; } };
            t.Track(new AdTrackEvent { Kind = "loaded", AdId = "a1" });
            t.Track(new AdTrackEvent { Kind = "loaded", AdId = "a1" });
            t.Track(new AdTrackEvent { Kind = "revenue", AdId = "a1" }); // no value: never invented
            Assert.AreEqual(1, t.Queue.Count);
            ready = true;
            t.Flush();
            Assert.AreEqual(1, sent.Count);
            t.SetEnabled(false);
            t.Track(new AdTrackEvent { Kind = "displayed", AdId = "a1" });
            Assert.AreEqual(1, sent.Count);
        }

        [Test]
        public void BudgetRuleHelper()
        {
            Assert.AreEqual("", RewardedRules.CheckBudget(0.2, 0.5, 1.0));
            Assert.AreNotEqual("", RewardedRules.CheckBudget(0.25, 0.5, 1.0));
            Assert.AreNotEqual("", RewardedRules.CheckBudget(0.2, 0.6, 1.0));
        }
    }
}
