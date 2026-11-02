// Volt City P4 — EditMode tests of money + services (engine-free; Unity Test Runner + today .NET 8 via
// Tools/game_dotnet_check.sh): the IAP catalogue = IAP.md, entitlement -> ad-free (only Remove Ads / the big packs), restore
// idempotence, consumables once per transaction + the daily pre-purchase cap, the Shop tab rules (one pill per card, policy
// strip = one tap, prices only from the store), offers, rewarded caps / budgets / ad-free-without-video / guide, house-ad
// gate for ad_free, sold looks visible, city slots, the analytics catalogue and the audio files + licences.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CoddyKit;
using CoddyKit.Ads;
using NUnit.Framework;
using VoltCity.Game;
using VoltCity.Sim;

namespace VoltCity.Game.Tests
{
    public class P4Tests
    {
        static GameData gd;
        static Loc loc;
        static string unity;
        static IapCatalog cat;
        static readonly string[] AdFreeProducts = { "noads", "premium", "grid_pass", "bundle" };

        [OneTimeSetUp]
        public void Load()
        {
            string env = Environment.GetEnvironmentVariable("VOLTCITY_UNITY");
            unity = !string.IsNullOrEmpty(env) ? env : Directory.GetCurrentDirectory();
            gd = GameData.LoadDirectory(Path.Combine(unity, "Assets", "Resources", "data"));
            Assert.IsTrue(gd.Ok, string.Join("; ", gd.Errors));
            loc = Loc.FromCsv(Strings(), "en");
            cat = IapCatalog.Parse(File.ReadAllText(Path.Combine(unity, "Assets", "Resources", "data", "iap.json")));
            Assert.IsEmpty(cat.Errors);
        }

        [TearDown]
        public void Reset() => CoddyServices.ResetToFakes();

        static string Strings() => File.ReadAllText(Path.Combine(unity, "Assets", "Resources", "i18n", "strings.csv"));

        static string TempDir()
        {
            string d = Path.Combine(Path.GetTempPath(), "voltcity-p4-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(d);
            return d;
        }

        /// <summary>A career + the real kit purchase layer on the fake store (grants = the catalogue's).</summary>
        sealed class Rig
        {
            public ManualClock Clock = new ManualClock();
            public FakePurchases Store = new FakePurchases();
            public GameSession Session;
            public CoddyPurchases Purchases;
            public Commerce Shop;
            public NullAnalytics Log = new NullAnalytics();
            public string KitDir;
            public int Changed;

            public Rig(string dir = null, FakePurchases store = null, bool prices = true)
            {
                if (store != null) Store = store;
                foreach (var p in cat.Products.Values) Store.Grants[p.Id] = p.Grants.ToArray();
                CoddyServices.Purchases = Store;
                dir ??= TempDir();
                KitDir = Path.Combine(dir, "coddykit");
                Session = new GameSession(gd, Path.Combine(dir, "voltcity")) { Now = () => (long)Clock.UnixNow };
                Session.LoadOrNew(7);
                Purchases = new CoddyPurchases(KitDir, new[] { "noads" }, () => Clock.UnixNow);
                Purchases.Start("goog_test");
                Shop = new Commerce(cat, Purchases, Session) { LocSource = () => loc, Analytics = (n, p) => Log.Event(n, p) };
                Shop.Changed += () => Changed++;
                if (prices) foreach (string s in cat.Order) Shop.SetPrice(s, "$" + cat.Products[s].Usd.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        // ============================================================================================ catalogue

        [Test]
        public void Catalogue_MatchesIapMd_TenLanguages_PlayLimits_NoTrial()
        {
            Assert.AreEqual(23, cat.Order.Count, "IAP.md 24 minus `scenarios` (reserved: its content does not exist yet)");
            CollectionAssert.AreEquivalent(AdFreeProducts, cat.AdFreeProducts);
            CollectionAssert.AreEqual(new[] { "noads" }, cat.AdFreeFeatures);
            var langs = new[] { "en", "tr", "de", "es", "fr", "pt", "ru", "ja", "ko", "hi" };
            CollectionAssert.AreEqual(langs, cat.Langs);
            var trial = new Regex(@"free\s+trial|try\s+(it\s+)?free|subscription", RegexOptions.IgnoreCase);
            var ents = new HashSet<string>();
            foreach (string slug in cat.Order)
            {
                var p = cat.Products[slug];
                Assert.AreEqual("com.coddykit.voltcity." + slug, p.Id);
                Assert.IsTrue(p.Usd > 0, slug);
                Assert.AreEqual(AdFreeProducts.Contains(slug), cat.GrantsAdFree(slug), slug + ": only Remove Ads + the big packs remove ads (§3/§8)");
                Assert.AreEqual(cat.GrantsAdFree(slug), p.AdFree, slug);
                foreach (string l in langs)
                {
                    string t = cat.Title(slug, l), d = cat.Desc(slug, l);
                    Assert.IsTrue(t.Length > 0 && t.Length <= 55, $"{slug} {l} title");
                    Assert.IsTrue(d.Length > 0 && d.Length <= 200, $"{slug} {l} desc {d.Length}");
                    Assert.IsFalse(trial.IsMatch(t + " " + d), $"{slug} {l}: trial / subscription wording");
                }
                if (p.Consumable)
                {
                    Assert.IsEmpty(p.Grants, slug + ": consumables grant no entitlement");
                    Assert.AreEqual(gd.ConsumableDays[slug], p.DaysX10 / 10.0, 1e-9, slug + ": shop amount = sim amount");
                    StringAssert.Contains("never restored", cat.Desc(slug, "en"), slug + ": store text says it is not restored");
                }
                else foreach (string g in p.Grants) ents.Add(g);
                if (p.AdFree) StringAssert.Contains("ad-free", cat.Desc(slug, "en").ToLowerInvariant(), slug + ": the store text says ad-free");
            }
            CollectionAssert.AreEquivalent(cat.Entitlements, ents);
            Assert.IsFalse(cat.Order.Contains("scenarios"));
            CollectionAssert.AreEqual(cat.Order, gd.IapProducts, "the sim reads the same sold list");
            // bundle = every permanent entitlement; Style Collection = the 6 looks
            CollectionAssert.AreEquivalent(cat.Entitlements, cat.Products["bundle"].Grants);
            Assert.AreEqual(6, cat.Products["cosmetics_all"].Grants.Count);
            // cheapest consumable = cash_s (the rewarded budget's reference pack)
            var cheapest = cat.Order.Where(s => cat.Products[s].Consumable && !cat.Products[s].OfferOnly).OrderBy(s => cat.Products[s].Usd).First();
            Assert.AreEqual("cash_s", cheapest);
        }

        [Test]
        public void StoreFiles_AreFreshAndCoverTheCatalogue()
        {
            string game = Path.GetDirectoryName(unity);
            var rc = MiniJson.TryParseObject(File.ReadAllText(Path.Combine(game, "store", "iap", "rc-catalog.json")));
            var play = MiniJson.TryParseObject(File.ReadAllText(Path.Combine(game, "store", "iap", "catalog.json")));
            Assert.NotNull(rc);
            Assert.NotNull(play);
            Assert.AreEqual("com.coddykit.voltcity", MiniJson.Str(rc, "package"));
            Assert.AreEqual(cat.Order.Count, MiniJson.Arr(rc, "products").Count);
            Assert.AreEqual(cat.Order.Count, MiniJson.Arr(play, "products").Count);
            foreach (var o in MiniJson.Arr(rc, "products"))
            {
                string slug = MiniJson.Str(o, "slug");
                var tiers = MiniJson.Obj(o, "price_tiers_usd");
                Assert.AreEqual(4, tiers.Count, slug + ": 4 PPP tiers");
                Assert.AreEqual(cat.Products[slug].Consumable ? 0 : cat.Products[slug].Grants.Count, MiniJson.Arr(o, "entitlements").Count, slug);
            }
        }

        // ============================================================================================ ad-free + purchases

        [Test]
        public void Entitlement_OnlyAdFreeProductsRemoveAds_HouseAdsFollow()
        {
            var r = new Rig();
            foreach (string slug in new[] { "cash_s", "speed", "dispatcher", "livery_sunset", "cosmetics_all", "starter", "slots", "offline_plus" })
            {
                Assert.IsTrue(r.Shop.Buy(slug), slug);
                Assert.IsFalse(r.Shop.AdFree, slug + " must not remove ads (§3: no 'any purchase')");
            }
            var pacing = new HouseInterstitialPacing(new Dictionary<string, object> { { "play_s", 99999.0 } }) { SessionS = 9999 };
            Assert.AreEqual("", pacing.Blocker(r.Clock.UnixNow, r.Shop.AdFree, false), "a non-ad-free buyer still sees house interstitials");
            foreach (string slug in AdFreeProducts)
            {
                var x = new Rig();
                Assert.IsTrue(x.Shop.Buy(slug), slug);
                Assert.IsTrue(x.Shop.AdFree, slug + " removes ads");
                Assert.AreEqual("ad_free", pacing.Blocker(x.Clock.UnixNow, x.Shop.AdFree, false), slug + ": no house interstitial");
                Assert.AreEqual("owned", x.Shop.WhyNot(slug));
                Assert.AreEqual("noads" == slug ? "owned" : x.Shop.WhyNot("noads"), x.Shop.WhyNot("noads"), "Remove Ads shows Owned under a pack");
            }
            var y = new Rig();
            y.Shop.Buy("premium");
            Assert.AreEqual("owned", y.Shop.WhyNot("noads"), "the Edition covers Remove Ads: never sold twice");
            Assert.AreEqual("owned", y.Shop.WhyNot("forecast_plus"));
            Assert.AreEqual("", y.Shop.WhyNot("grid_pass"), "the Pass still adds the neon skyline");
        }

        [Test]
        public void Restore_IsIdempotent_AndNeverRedeliversConsumables()
        {
            string dir = TempDir();
            var r = new Rig(dir);
            Assert.IsTrue(r.Shop.Buy("premium"));
            Assert.IsTrue(r.Shop.Buy("cash_m"));
            long cash = r.Session.Sim.S.Cash;
            var active = new HashSet<string>(r.Purchases.Cache.Active);
            int receipts = r.Session.Sim.S.Receipts.Count;
            int changedBefore = 0;
            r.Purchases.Changed += () => changedBefore++;
            r.Shop.Restore();
            r.Shop.Restore();
            Assert.AreEqual(0, changedBefore, "the same CustomerInfo twice changes nothing");
            CollectionAssert.AreEquivalent(active, r.Purchases.Cache.Active);
            Assert.AreEqual(cash, r.Session.Sim.S.Cash, "restore never pays a consumable again");
            Assert.AreEqual(receipts, r.Session.Sim.S.Receipts.Count);
            Assert.IsTrue(r.Shop.AdFree);
            Assert.AreEqual(2, r.Log.Log.Count(n => n == "restore_result"));

            // a new phone (empty kit cache, same store account): restore brings the rights, not the cash
            var fresh = new Rig(TempDir(), r.Store);
            Assert.IsTrue(fresh.Shop.AdFree, "the start-up CustomerInfo already brings the rights");
            long freshCash = fresh.Session.Sim.S.Cash;
            fresh.Shop.Restore();
            Assert.IsTrue(fresh.Shop.AdFree);
            Assert.IsTrue(fresh.Shop.Has("theme_gold"));
            Assert.AreEqual(freshCash, fresh.Session.Sim.S.Cash);

            // offline: an error keeps the cached rights (a buyer stays ad-free on a plane)
            r.Store.Offline = true;
            r.Shop.Restore();
            Assert.IsTrue(r.Shop.AdFree);
            // a refund (fresh CustomerInfo without it) removes them
            r.Store.Offline = false;
            r.Store.Active.Clear();
            r.Shop.Restore();
            Assert.IsFalse(r.Shop.AdFree);
        }

        [Test]
        public void Consumables_OncePerTransaction_DailyCapIsAPrePurchaseGate()
        {
            var r = new Rig();
            long day = r.Session.Sim.DayIncome();
            long c0 = r.Session.Sim.S.Cash;
            Assert.IsTrue(r.Shop.Buy("cash_s"));
            Assert.AreEqual(c0 + day, r.Session.Sim.S.Cash, "1 day of income");
            Assert.IsTrue(r.Shop.Buy("cash_l"));
            Assert.AreEqual(c0 + day + day * 5, r.Session.Sim.S.Cash);
            Assert.IsTrue(r.Shop.Buy("cash_m"));
            Assert.AreEqual("daily_cap", r.Shop.WhyNot("cash_s"), "<= 3 packs a UTC day");
            Assert.IsFalse(r.Shop.Buy("cash_s"));
            var pill = r.Shop.Pill("cash_s", loc, "t");
            Assert.IsFalse(pill.Enabled, "the pill is disabled before the store sheet opens");
            Assert.AreEqual("", r.Shop.WhyNot("speed"), "the cap is for packs only");
            // a pack the store already charged is ALWAYS delivered, even at the cap (kit fix)
            long c1 = r.Session.Sim.S.Cash;
            PurchaseOutcome outcome = default;
            r.Purchases.Buy(cat.Products["cash_s"].Id, o => outcome = o);
            Assert.IsTrue(outcome.Success);
            Assert.AreEqual(c1 + day, r.Session.Sim.S.Cash);
            // the same transaction id never pays twice (sim receipts + kit ledger)
            var again = r.Session.Sim.GrantPurchaseFunds(outcome.TransactionId, "cash_s");
            Assert.IsTrue(again.Duplicate);
            Assert.AreEqual(c1 + day, r.Session.Sim.S.Cash);
            Assert.AreEqual("already_delivered", r.Purchases.Ledger.ConfirmedBlocker(outcome.TransactionId));
            // next UTC day: packs again
            r.Clock.Advance(86400);
            Assert.AreEqual("", r.Shop.WhyNot("cash_s"));
        }

        [Test]
        public void Purchase_Failures_AndNoPrice_NeverInvent()
        {
            var r = new Rig(prices: false);
            Assert.AreEqual("no_price", r.Shop.WhyNot("noads"));
            var pill = r.Shop.Pill("noads", loc, "t");
            Assert.AreEqual(Commerce.Loading, pill.Label, "unloaded price = …, never an invented one");
            Assert.IsFalse(pill.Enabled);
            Assert.IsFalse(r.Shop.Buy("noads"));
            r.Shop.SetPrice("noads", "€3,49");
            Assert.AreEqual("€3,49", r.Shop.Pill("noads", loc, "t").Label, "the store's displayPrice as is");
            r.Store.Offline = true;
            Assert.IsTrue(r.Shop.Buy("noads"));
            Assert.IsFalse(r.Shop.AdFree);
            Assert.AreEqual("", r.Shop.Pending, "a failed purchase never leaves the pill stuck");
            Assert.Contains("purchase_fail", r.Log.Log);
        }

        // ============================================================================================ shop tab

        [Test]
        public void Counts_PluralRules_TenLanguages()
        {
            // Patron #172: "1 faults". Count phrases go through Loc.N with CLDR plural categories per language.
            Assert.AreEqual("one", Loc.PluralCategory("en", 1)); Assert.AreEqual("other", Loc.PluralCategory("en", 0));
            Assert.AreEqual("one", Loc.PluralCategory("fr", 0)); Assert.AreEqual("one", Loc.PluralCategory("pt", 1)); Assert.AreEqual("other", Loc.PluralCategory("hi", 2));
            Assert.AreEqual("one", Loc.PluralCategory("ru", 21)); Assert.AreEqual("few", Loc.PluralCategory("ru", 3)); Assert.AreEqual("many", Loc.PluralCategory("ru", 11));
            Assert.AreEqual("many", Loc.PluralCategory("ru", 5)); Assert.AreEqual("few", Loc.PluralCategory("ru", 24)); Assert.AreEqual("other", Loc.PluralCategory("ja", 1));
            var en = Loc.FromCsv(Strings(), "en");
            Assert.AreEqual("1 fault", en.N("count.faults", 1)); Assert.AreEqual("2 faults", en.N("count.faults", 2));
            Assert.AreEqual("1 truck ready", en.N("count.trucks_ready", 1)); Assert.AreEqual("0 trucks ready", en.N("count.trucks_ready", 0));
            var ru = Loc.FromCsv(Strings(), "ru");
            Assert.AreEqual("1 поломка", ru.N("count.faults", 1)); Assert.AreEqual("3 поломки", ru.N("count.faults", 3)); Assert.AreEqual("5 поломок", ru.N("count.faults", 5));
            var plural1 = new Regex(@"(^|\s)1 [A-Za-zÀ-ÿ]+s\b");
            foreach (string lang in Loc.Languages)
            {
                var l = Loc.FromCsv(Strings(), lang);
                foreach (string key in new[] { "count.faults", "count.trucks_ready", "count.trucks_depot", "count.dark_min", "count.sheds" })
                    foreach (long n in new long[] { 0, 1, 2, 5, 11, 21 })
                    {
                        string t = l.N(key, n);
                        Assert.IsFalse(t.Contains("{") || t.Contains(key), lang + " " + key + " " + n + ": " + t);
                        StringAssert.Contains(n.ToString(), t, lang + " " + key);
                        if (n == 1) Assert.IsFalse(plural1.IsMatch(t), lang + " " + key + ": " + t);
                    }
            }
        }

        [Test]
        public void CityHookup_SaysWhyNot_OrOffersTheActivePrice()
        {
            // Patron #173: a grey "$2,000" read as disabled. Not possible yet = lock chip + reason (price in the line);
            // possible = the active price pill.
            var r = new Rig();
            var sim = r.Session.Sim;
            Row Hook() => Tabs.Build("city", sim, loc).Rows.First(x => x.Id.StartsWith("hookup:"));
            var h = Hook();
            Assert.AreEqual("status", h.Actions[0].Kind, "cash short: a status chip");
            Assert.AreEqual(loc.T("why.cash"), h.Actions[0].Label);
            StringAssert.Contains("2,000", h.Sub, "the price stays visible");
            sim.S.Cash = 50000;
            h = Hook();
            Assert.AreEqual("primary", h.Actions[0].Kind, "affordable between shifts: the active price pill");
            Assert.IsTrue(h.Actions[0].Enabled);
            StringAssert.StartsWith("hookup:", h.Actions[0].Id);
            Assert.IsTrue(sim.StartShift());
            h = Hook();
            Assert.AreEqual("status", h.Actions[0].Kind);
            Assert.AreEqual(loc.T("why.live"), h.Actions[0].Label, "during a shift: 'Between shifts only'");
        }

        [Test]
        public void ShopTab_BillingUnavailable_SaysSo_WithRetry()
        {
            // device pass 10-28: with no store prices (billing unavailable) the shop showed only its header
            var r = new Rig(prices: false);
            Assert.IsFalse(r.Shop.StoreReady);
            var actions = new GameActions(r.Session, loc) { Extras = new Extras { Shop = r.Shop } };
            var tv = Tabs.Build("shop", r.Session.Sim, loc, null, actions.Extras);
            var row = tv.Rows.FirstOrDefault(x => x.Id == "shop.unavailable");
            Assert.IsNotNull(row, "a 'store unavailable' line");
            Assert.AreEqual(1, tv.Rows.IndexOf(row), "right under the policy strip, above the products");
            Assert.AreEqual(loc.T("shop.unavailable"), row.Title);
            Assert.AreEqual("shop:retry", row.Actions[0].Id);
            Assert.IsTrue(actions.Run("shop:retry").Ok, "retry asks the store again");
            Assert.Greater(tv.Rows.Count(x => x.Id.StartsWith("prod:")), 0, "the products stay listed (pills wait for prices)");
            var ok = new Rig();
            Assert.IsFalse(Tabs.Build("shop", ok.Session.Sim, loc, null, new Extras { Shop = ok.Shop }).Rows.Any(x => x.Id == "shop.unavailable"));
        }

        [Test]
        public void ShopTab_OnePillPerCard_PolicyStripOneTap_TenLanguages()
        {
            var r = new Rig();
            var actions = new GameActions(r.Session, loc) { Extras = new Extras { Shop = r.Shop } };
            var tv = Tabs.Build("shop", r.Session.Sim, loc, null, actions.Extras);
            var strip = tv.Rows[0];
            Assert.AreEqual("policy.strip", strip.Id, "the policy strip leads the shop");
            Assert.AreEqual("policy", strip.Open, "the whole strip is one tap target");
            StringAssert.Contains("without paying", strip.Title);
            Assert.IsFalse(strip.Wrap);
            Assert.IsTrue(actions.Run(strip.Open).Ok, "ONE tap opens it");
            var open = Tabs.Build("shop", r.Session.Sim, loc, null, actions.Extras).Rows[0];
            Assert.IsTrue(open.Wrap);
            Assert.AreEqual(loc.T("shop.policy.full"), open.Sub);
            StringAssert.Contains("Remove Ads", open.Sub);
            Assert.IsTrue(actions.Run("policy").Ok);
            Assert.IsFalse(Tabs.Build("shop", r.Session.Sim, loc, null, actions.Extras).Rows[0].Wrap, "the next tap closes it");

            var products = tv.Rows.Where(x => x.Id.StartsWith("prod:")).ToList();
            Assert.AreEqual(cat.Order.Count(s => !cat.Products[s].OfferOnly), products.Count, "offer-only products appear only as an active offer");
            foreach (var row in tv.Rows) Assert.LessOrEqual(row.Actions.Count, 1, row.Id + ": one pill per card");
            foreach (var row in products)
            {
                Assert.AreEqual(1, row.Actions.Count, row.Id);
                Assert.AreEqual("iap:" + row.Id.Substring(5), row.Actions[0].Id);
                Assert.IsTrue(row.Actions[0].Enabled);
            }
            Assert.AreEqual("ghost", tv.Rows.Last().Actions[0].Kind, "Restore is the ghost pill at the end");
            Assert.AreEqual("restore_purchases", actions.Run("restore").Open);
            // ad-free badge only on the ad-free products
            foreach (var row in products) Assert.AreEqual(AdFreeProducts.Contains(row.Id.Substring(5)), row.Badge == loc.T("shop.badge.ad_free"), row.Id);

            // bought: Owned (disabled) + the look under "Your looks" with Use / In use
            Assert.IsTrue(actions.Run("iap:livery_arctic").Ok);
            tv = Tabs.Build("shop", r.Session.Sim, loc, null, actions.Extras);
            var owned = tv.Rows.First(x => x.Id == "prod:livery_arctic");
            Assert.AreEqual(loc.T("shop.owned"), owned.Actions[0].Label);
            Assert.IsFalse(owned.Actions[0].Enabled);
            var look = tv.Rows.First(x => x.Id == "look:livery_arctic");
            Assert.AreEqual(loc.T("shop.in_use"), look.Actions[0].Label, "a new look is put on at once");
            Assert.IsTrue(actions.Run("look:livery_arctic").Ok);
            Assert.AreEqual("", r.Shop.Look.Truck);
            Assert.AreEqual("unavailable", actions.Run("look:livery_sunset").Reason, "an unowned look cannot be used");

            // every language: no raw key / placeholder, product names from the catalogue
            foreach (string l in Loc.Languages)
            {
                var lx = Loc.FromCsv(Strings(), l);
                r.Shop.LocSource = () => lx;
                var v = Tabs.Build("shop", r.Session.Sim, lx, null, actions.Extras);
                foreach (var row in v.Rows)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(row.Title), l + " " + row.Id);
                    Assert.IsFalse(row.Title.Contains("{") || (row.Sub ?? "").Contains("{"), l + " " + row.Id);
                    Assert.IsFalse(Regex.IsMatch(row.Title, @"^[a-z_]+\.[a-z_.]+$"), l + " raw key " + row.Title);
                    Assert.IsFalse(row.Title.Contains("com.coddykit"), "never a raw store id");
                }
                var card = r.Shop.ProductCard("bundle", lx);
                Assert.AreEqual(cat.Title("bundle", l), card.Title);
                Assert.NotNull(card.Primary);
            }
        }

        [Test]
        public void LockedFeature_OpensTheShopOnThatProduct()
        {
            var r = new Rig();
            var actions = new GameActions(r.Session, loc) { Extras = new Extras { Shop = r.Shop } };
            Assert.AreEqual("shop:dispatcher", actions.Run("shop:dispatcher").Open);
            r.Shop.Focus = "dispatcher";
            var row = r.Shop.ShopTab(loc).Rows.First(x => x.Id == "prod:dispatcher");
            Assert.IsTrue(row.Selected, "the product the shop was opened on is highlighted");
        }

        // ============================================================================================ offers

        [Test]
        public void Offers_FollowTheRules()
        {
            var rec = new OfferRecord();
            Func<string, bool> none = _ => false;
            OfferContext C(int day, string ev = "", bool first = false, bool tut = false, bool live = false, int away = 0, int stars = 3, int ch = 1) =>
                new OfferContext { Day = day, Event = ev, FirstSession = first, Tutorial = tut, LiveShift = live, AwayDays = away, Stars = stars, Chapter = ch };
            Assert.AreEqual("", Offers.Pick(cat, rec, C(2), none), "starter waits for day 3");
            Assert.AreEqual("", Offers.Pick(cat, rec, C(4, first: true), none), "never the first session");
            Assert.AreEqual("", Offers.Pick(cat, rec, C(4, tut: true), none), "never in the guide");
            Assert.AreEqual("", Offers.Pick(cat, rec, C(4, live: true), none), "never during a live shift");
            Assert.AreEqual("starter", Offers.Pick(cat, rec, C(4), none));
            Assert.AreEqual("", Offers.Pick(cat, rec, C(4), g => g == "forecast_plus" || g == "livery_sunset"), "never what the player owns");
            Offers.Shown(rec, "starter", C(4));
            Assert.AreEqual("", Offers.Pick(cat, rec, C(4, "comeback", away: 5), none), "<= 1 offer per session");
            rec.ThisSession = 0;
            Assert.AreEqual("", Offers.Pick(cat, rec, C(6), none), "3 days of quiet after an offer");
            Assert.AreEqual("", Offers.Pick(cat, rec, C(7), none), "starter once per game");
            Assert.AreEqual("", Offers.Pick(cat, rec, C(19), none));
            Assert.AreEqual("bundle", Offers.Pick(cat, rec, C(21), none));
            Assert.AreEqual("", Offers.Pick(cat, rec, C(30, "comeback", away: 2), none), "comeback needs >= 3 days away");
            Assert.AreEqual("comeback", Offers.Pick(cat, rec, C(30, "comeback", away: 3), none));
            Assert.AreEqual("", Offers.Pick(cat, rec, C(40, "storm_report", stars: 2), none), "storm kit only after a 1-star storm shift");
            Assert.AreEqual("storm_kit", Offers.Pick(cat, rec, C(40, "storm_report", stars: 1, ch: 2), none));
            Offers.Shown(rec, "storm_kit", C(40, "storm_report", stars: 1, ch: 2));
            rec.ThisSession = 0;
            Assert.AreEqual("", Offers.Pick(cat, rec, C(50, "storm_report", stars: 1, ch: 2), none), "once per chapter");
            Assert.AreEqual("storm_kit", Offers.Pick(cat, rec, C(50, "storm_report", stars: 1, ch: 3), none));
            var back = OfferRecord.FromJson(MiniJson.TryParseObject(MiniJson.Write(rec.ToJson())));
            Assert.AreEqual(rec.Shown("starter"), back.Shown("starter"), "persisted (a restart never re-arms an offer)");
            Assert.AreEqual(rec.LastDay, back.LastDay);
            Assert.IsTrue(Offers.StormShift(new List<string> { "squirrel", "thunderstorm" }));
            Assert.IsFalse(Offers.StormShift(new List<string> { "heatwave" }));
        }

        [Test]
        public void Commerce_ActiveOffer_ShowsOnceAndLeavesWhenBought()
        {
            var r = new Rig();
            r.Session.Settings.Sessions = 3;
            r.Session.Settings.FirstSeen = (long)r.Clock.UnixNow - 4 * 86400;
            string slug = r.Shop.TryOffer(r.Shop.OfferCtx("", false, false));
            Assert.AreEqual("starter", slug);
            Assert.AreEqual("", r.Shop.TryOffer(r.Shop.OfferCtx("comeback", false, false, 9)), "one offer per session");
            var tv = r.Shop.ShopTab(loc);
            Assert.AreEqual("prod:starter", tv.Rows[1].Id, "the session's offer comes right after the policy strip");
            Assert.AreEqual(loc.T("shop.badge.offer"), tv.Rows[1].Badge);
            Assert.IsTrue(r.Shop.Buy("starter"));
            Assert.AreEqual("", r.Shop.ActiveOffer);
            Assert.IsTrue(r.Shop.Has("forecast_plus") && r.Shop.Has("livery_sunset"));
            Assert.AreEqual(1, r.Session.Settings.OfferRecord.Shown("starter"));
        }

        // ============================================================================================ rewarded

        [Test]
        public void Rewarded_CapsMatchTheSim_AndTheBudgetFitsTheCheapestPack()
        {
            var kit = VoltRewards.KitLimits();
            CollectionAssert.AreEquivalent(gd.Placements.Keys, kit.Keys);
            CollectionAssert.AreEquivalent(VoltRewards.Placements, kit.Keys);
            foreach (var kv in gd.Placements)
            {
                Assert.AreEqual(kv.Value.Cap, kit[kv.Key].Daily, kv.Key + " daily cap");
                Assert.AreEqual(kv.Value.CooldownS, kit[kv.Key].CooldownS, 1e-9, kv.Key + " cooldown");
                Assert.AreEqual(VoltRewards.IsCash(kv.Key), kv.Value.Cash, kv.Key + " cash");
                Assert.IsFalse(cat.Entitlements.Contains(kv.Key), "never reward what is sold");
            }
            // units: cash_s (the cheapest pack) = 1 day = 1000 units
            Assert.AreEqual(10, cat.Products["cash_s"].DaysX10);
            Assert.AreEqual("", RewardedRules.CheckBudget(VoltRewards.UnitsSingleMax, VoltRewards.UnitsDailyMax, VoltRewards.UnitsPerDay));
            Assert.AreEqual("", RewardedRules.CheckBudget(gd.RewardSingleMaxDay, gd.RewardDailyMaxDay, 1.0), "sim caps in days");
            // IAP.md value check: offline at cap 0.10 day + 3 x 0.075 day + non-cash tokens <= 0.5 day
            double offlineDay = gd.OfflineShare / gd.DayShifts;   // night pay at the 8 h cap, in days of income
            double daily = offlineDay + 3 * 0.5 * (1.0 / gd.DayShifts) + 5 * 0.001;
            Assert.LessOrEqual(daily, 0.5, "all rewards of a day <= 1/2 of cash_s");
        }

        static (Rig r, VoltRewards rw, FakeRewarded ads, NullAnalytics log, RewardedRules rules) RewardRig(bool adFree, bool loaded = true)
        {
            var r = new Rig();
            var log = new NullAnalytics();
            var ads = new FakeRewarded { Available = loaded };
            var rules = new RewardedRules(r.Clock) { BudgetSingle = VoltRewards.UnitsSingleMax, BudgetDaily = VoltRewards.UnitsDailyMax };
            foreach (var kv in VoltRewards.KitLimits()) rules.Limits[kv.Key] = kv.Value;
            var ctl = new RewardedController(rules, new FakeConsent(), ads, log, new RcAdTracker()) { UnitId = "unit", AdFree = () => adFree };
            ctl.Start();
            var rw = new VoltRewards(() => r.Session.Sim, ctl) { Now = () => (long)r.Clock.UnixNow, AdFree = () => adFree };
            return (r, rw, ads, log, rules);
        }

        static void PlayToReport(Simulation sim)
        {
            Assert.IsTrue(sim.StartShift());
            for (int k = 0; k < 12 && sim.Phase == "live"; k++)
            {
                sim.RunSteps(sim.Gd.Steps / 8);
                if (sim.Phase != "live") break;
                foreach (var f in sim.Grid.OpenFaults()) if (f.Crew < 0) sim.Queue(Cmd.SendCrew(f.Id));
            }
            sim.RunSteps(sim.Gd.Steps);
            Assert.AreEqual("report", sim.Phase);
        }

        [Test]
        public void Rewarded_VideoOnlyWhenLoaded_GrantAfterEarned_CapsPersist()
        {
            var (r, rw, ads, log, rules) = RewardRig(adFree: false);
            var sim = r.Session.Sim;
            PlayToReport(sim);
            Assume.That(sim.S.Report.Profit > 0, "the scripted shift makes a profit");
            var o = rw.Get("shift_bonus");
            Assert.IsTrue(o.Show);
            Assert.IsTrue(o.Video);
            Assert.LessOrEqual(o.Units, VoltRewards.UnitsSingleMax);
            long cash = sim.S.Cash;
            int granted = 0;
            rw.Granted += (p, res) => granted++;
            var row = rw.Row("shift_bonus", loc);
            Assert.AreEqual("reward:shift_bonus", row.Actions[0].Id);
            StringAssert.StartsWith("▶", row.Actions[0].Label, "the video is announced on the pill");
            Assert.IsTrue(rw.Claim("shift_bonus"));
            Assert.AreEqual(1, ads.ShowCalls);
            Assert.AreEqual(1, granted);
            Assert.AreEqual(cash + o.Amount, sim.S.Cash);
            Assert.IsFalse(rw.Get("shift_bonus").Show, "once per report");
            Assert.Contains(RewardedEvents.Completed, log.Log);
            Assert.IsFalse(rw.Get("emergency_import").Show, "import only in a live deficit");

            // a video closed early grants nothing
            var (earlyRig, earlyRw, ads2, _, _) = RewardRig(adFree: false);
            PlayToReport(earlyRig.Session.Sim);
            ads2.CloseWithoutEarn = true;
            long c2 = earlyRig.Session.Sim.S.Cash;
            Assert.IsTrue(earlyRw.Claim("shift_bonus"));
            Assert.AreEqual(c2, earlyRig.Session.Sim.S.Cash);

            // no ad loaded and not ad-free: no button at all
            var (noAdsRig, noAdsRw, _, _, _) = RewardRig(adFree: false, loaded: false);
            PlayToReport(noAdsRig.Session.Sim);
            Assert.IsFalse(noAdsRw.Get("shift_bonus").Show);
            Assert.IsNull(noAdsRw.Row("shift_bonus", loc));

            // caps persist across a restart (kit usage file + sim save)
            var again = new RewardedRules(r.Clock, MiniJson.TryParseObject(MiniJson.Write(rules.Usage)));
            foreach (var kv in VoltRewards.KitLimits()) again.Limits[kv.Key] = kv.Value;
            Assert.AreEqual(2, again.Remaining("shift_bonus"));
            Assert.Greater(again.CooldownLeft("shift_bonus"), 0);
        }

        [Test]
        public void Rewarded_AdFreeGetsItWithoutVideo_SameCaps_NeverInTheGuide()
        {
            var (r, rw, ads, log, rules) = RewardRig(adFree: true, loaded: false);
            var sim = r.Session.Sim;
            PlayToReport(sim);
            var o = rw.Get("shift_bonus");
            Assert.IsTrue(o.Show, "ad_free: offered even without a loaded ad");
            Assert.IsFalse(o.Video);
            Assert.IsFalse(rw.Row("shift_bonus", loc).Actions[0].Label.StartsWith("▶"));
            Assert.IsTrue(rw.Claim("shift_bonus"));
            Assert.AreEqual(0, ads.ShowCalls, "no video for ad_free");
            Assert.Contains(RewardedEvents.SkippedPaidUser, log.Log);
            Assert.AreEqual(2, rules.Remaining("shift_bonus"), "same caps as everyone");

            var (g, grw, _, _, _) = RewardRig(adFree: true);
            g.Session.Sim.TutorialSafe = true;
            PlayToReport(g.Session.Sim);
            Assert.IsFalse(grw.Get("shift_bonus").Show, "never in the guide");
        }

        [Test]
        public void Rewarded_OfflineDouble_And_Overtime_OnTheirCards()
        {
            var (r, rw, _, _, _) = RewardRig(adFree: false);
            var sim = r.Session.Sim;
            PlayToReport(sim);
            sim.Collect();
            r.Session.Save();
            r.Clock.Advance(6 * 3600);
            var off = r.Session.Resume();
            Assert.NotNull(off);
            var ext = new Extras { Rewards = rw };
            var card = Cards.Offline(off, loc, ext);
            var x2 = card.Rows.FirstOrDefault(x => x.Id == "rw:offline_x2");
            Assert.NotNull(x2, "double-it on the welcome-back card");
            long cash = sim.S.Cash;
            Assert.IsTrue(rw.Claim("offline_x2"));
            Assert.AreEqual(cash + off.Cash, sim.S.Cash);
            Assert.IsNull(Cards.Offline(off, loc, ext).Rows.FirstOrDefault(x => x.Id == "rw:offline_x2"), "1 a day");

            // crew overtime: a live fault's card offers the instant repair of THAT fault
            // a forced storm still obeys the planner's "no storm right after a 1-star shift" cap; the unattended shift above
            // can end on 1 star (it did when this test ran alone or in Unity's order), so start from a good shift
            sim.S.LastStars = 3;
            Assert.IsTrue(sim.StartShift(null, new List<string> { "thunderstorm" }));
            Fault f = null;
            for (int i = 0; i < 400 && f == null && sim.Phase == "live"; i++) { sim.RunSteps(2); f = sim.Grid.OpenFaults().FirstOrDefault(); }
            Assume.That(f != null, "the storm makes a fault");
            var fc = Cards.FaultCard(f.Id, sim, loc, new DispatchFlow(), ext);
            var ot = fc.Rows.FirstOrDefault(x => x.Id == "rw:crew_overtime");
            Assert.NotNull(ot);
            Assert.AreEqual("reward:crew_overtime:" + f.Id, ot.Actions[0].Id);
            var actions = new GameActions(r.Session, loc) { Extras = ext };
            Assert.IsTrue(actions.Run(ot.Actions[0].Id).Ok);
            sim.RunSteps(2);
            Assert.IsFalse(sim.Grid.FindFault(f.Id)?.Open ?? false, "repaired by the overtime crew");
        }

        // ============================================================================================ looks + slots

        [Test]
        public void SoldLooks_AreDrawn_OnLowAndMedium()
        {
            var sel = new Dictionary<string, string>();
            var none = Looks.Resolve(sel, _ => true);
            foreach (string look in Looks.All)
            {
                var s = new Dictionary<string, string>();
                Looks.Equip(s, look);
                var st = Looks.Resolve(s, x => x == look);
                Assert.AreNotEqual(none.Sig(), st.Sig(), look + " changes what is drawn");
                bool visible = st.LiveryA != Looks.DefaultLiveryA || st.BladeStyle > 0 || st.SkylineStyle > 0 || st.PylonDeco > 0 || st.RoomClass != "";
                Assert.IsTrue(visible, look + " reaches a uniform / theme class");
                Assert.AreEqual(none.Sig(), Looks.Resolve(s, _ => false).Sig(), look + ": never drawn when not owned");
            }
            // every look product in the catalogue is one of these (no sold look without a renderer)
            foreach (string slug in cat.Order)
                foreach (string g in cat.Products[slug].Grants)
                    if (g.StartsWith("livery_") || g.StartsWith("theme_") || g.StartsWith("room_") || g.Contains("pylons") || g.Contains("blades") || g.Contains("skyline"))
                        CollectionAssert.Contains(Looks.All, g);
            // the uniforms exist in the Medium AND the Low shaders, outside any VC_LOW branch
            string sh = Path.Combine(unity, "Assets", "Art", "Shaders");
            foreach (string f in new[] { "VcProp.shader", "VcPropLow.shader" }) StringAssert.Contains("_LiveryA", File.ReadAllText(Path.Combine(sh, f)), f);
            StringAssert.Contains("VcWorldCore.hlsl", File.ReadAllText(Path.Combine(sh, "VcWorldLow.shader")));
            string core = File.ReadAllText(Path.Combine(sh, "VcWorldCore.hlsl"));
            foreach (string u in new[] { "_VcSkylineStyle", "_VcPylonDeco" })
            {
                int at = core.IndexOf(u + " > 0.5", StringComparison.Ordinal) >= 0 ? core.IndexOf(u + " > 0.5", StringComparison.Ordinal) : core.IndexOf(u + ")", StringComparison.Ordinal);
                Assert.Greater(at, 0, u);
                string before = core.Substring(0, at);
                int opens = Regex.Matches(before, @"#if\s+defined\(VC_LOW\)|#if\s+!defined\(VC_LOW\)").Count;
                int closes = Regex.Matches(before, @"#endif").Count;
                Assert.LessOrEqual(opens, closes, u + " is not inside a tier branch");
            }
            StringAssert.Contains("_BladeStyle", File.ReadAllText(Path.Combine(sh, "VcBlades.shader")));
            string uss = File.ReadAllText(Path.Combine(unity, "Assets", "Resources", "ui", "VoltCity.uss"));
            StringAssert.Contains(".vc-theme--retro", uss);
            StringAssert.Contains(".vc-theme--gold", uss);
            // theme_gold = truck AND room; a livery bought later swaps only the truck
            var gold = new Dictionary<string, string>();
            Looks.Equip(gold, "theme_gold");
            Looks.Equip(gold, "livery_sunset");
            var mix = Looks.Resolve(gold, _ => true);
            Assert.AreEqual("livery_sunset", mix.Truck);
            Assert.AreEqual("theme_gold", mix.Room);
        }

        [Test]
        public void CitySlots_OneFree_FourWithTheEntitlement_SeparateSaves()
        {
            string dir = TempDir();
            var r = new Rig(dir);
            Assert.AreEqual(1, r.Shop.SlotCount);
            r.Shop.Buy("slots");
            Assert.AreEqual(4, r.Shop.SlotCount);
            var g = new Rig();
            g.Shop.Buy("grid_pass");
            Assert.AreEqual(4, g.Shop.SlotCount, "the Pass includes the slots");
            Assert.AreEqual("career.json", GameSession.SlotFile(1));
            Assert.AreEqual("career_3.json", GameSession.SlotFile(3));
            r.Session.Save();
            long cash1 = r.Session.Sim.S.Cash;
            r.Session.Settings.Slot = 3;
            r.Session.Settings.Save();
            var s3 = new GameSession(gd, Path.Combine(dir, "voltcity"));
            Assert.IsFalse(s3.LoadOrNew(99), "slot 3 starts a new city");
            s3.Sim.S.Cash = cash1 + 12345;
            s3.Save();
            Assert.IsTrue(File.Exists(Path.Combine(dir, "voltcity", "career_3.json")));
            Assert.AreEqual(cash1, s3.SlotInfo(1).cash, "slot 1 untouched");
            Assert.IsFalse(s3.SlotInfo(2).ok);
        }

        // ============================================================================================ analytics + audio

        [Test]
        public void Analytics_EveryEventIsInTheCatalogue_AndFirebaseValid()
        {
            foreach (var kv in VoltAnalytics.Events)
            {
                Assert.IsTrue(Regex.IsMatch(kv.Key, "^[A-Za-z][A-Za-z0-9_]{0,39}$"), kv.Key);
                Assert.LessOrEqual(kv.Value.Length, 25, kv.Key);
                foreach (string p in kv.Value) Assert.IsTrue(Regex.IsMatch(p, "^[A-Za-z][A-Za-z0-9_]{0,39}$"), kv.Key + "." + p);
            }
            var sink = new NullAnalytics();
            var a = new VoltAnalytics(sink);
            a.Event("session_start", null);
            a.Event("made_up_event", null);
            a.Event("purchase_start", new Dictionary<string, object> { { "product", "noads" }, { "email", "x" } });
            a.Event("purchase_start", new Dictionary<string, object> { { "product", "noads" } });
            Assert.AreEqual(3, a.Rejected.Count, string.Join(",", a.Rejected));
            CollectionAssert.AreEqual(new[] { "purchase_start" }, sink.Log);

            // every literal event name the code logs is in the catalogue (game + kit)
            var names = new HashSet<string>();
            var rx = new Regex(@"(?:Analytics\??(?:\.Invoke)?\(|\.Event\(|Log\()\s*""([a-z_]+)""");
            foreach (string root in new[] { "Scripts", "CoddyKit" })
                foreach (string f in Directory.GetFiles(Path.Combine(unity, "Assets", root), "*.cs", SearchOption.AllDirectories))
                {
                    if (f.Contains("Tests")) continue;
                    foreach (Match m in rx.Matches(File.ReadAllText(f))) names.Add(m.Groups[1].Value);
                }
            Assert.Greater(names.Count, 15);
            foreach (string n in names) Assert.IsTrue(VoltAnalytics.Events.ContainsKey(n), "event not in the catalogue: " + n);
            foreach (string n in new[] { RewardedEvents.OfferShown, RewardedEvents.OfferAccepted, RewardedEvents.Completed, RewardedEvents.SkippedPaidUser })
                Assert.IsTrue(VoltAnalytics.Events.ContainsKey(n), n);

            // career events map + validate
            var sim = new Simulation(gd, 3);
            PlayToReport(sim);
            int ok = 0;
            foreach (var e in sim.DrainEvents())
            {
                var x = VoltAnalytics.FromSim(e, sim);
                if (!x.HasValue) continue;
                Assert.AreEqual("", VoltAnalytics.Validate(x.Value.Key, x.Value.Value), x.Value.Key);
                ok++;
            }
            Assert.GreaterOrEqual(ok, 2, "shift_started + shift_completed");
            // the doc lists every event
            string doc = File.ReadAllText(Path.Combine(Path.GetDirectoryName(unity), "docs", "ANALYTICS.md"));
            foreach (string n in VoltAnalytics.Events.Keys) StringAssert.Contains("`" + n + "`", doc, "docs/ANALYTICS.md misses " + n);
        }

        [Test]
        public void Audio_EveryCueHasAFileAndALicence_MusicOffByDefault_LowLimits()
        {
            string dir = Path.Combine(unity, "Assets", "Resources", "audio");
            string doc = File.ReadAllText(Path.Combine(Path.GetDirectoryName(unity), "docs", "AUDIO.md"));
            foreach (var c in SoundCues.All)
            {
                Assert.IsTrue(File.Exists(Path.Combine(dir, c.Id + ".ogg")), c.Id);
                StringAssert.Contains("`" + c.Id + ".ogg`", doc, "AUDIO.md licence row for " + c.Id);
            }
            foreach (string f in Directory.GetFiles(dir, "*.ogg")) Assert.NotNull(SoundCues.Get(Path.GetFileNameWithoutExtension(f)), "unused file " + f);
            // every cue a sim event can ask for exists
            var sim = new Simulation(gd, 5);
            Assert.IsTrue(sim.StartShift(null, new List<string> { "thunderstorm" }));
            sim.RunSteps(gd.Steps * 2);
            foreach (var e in sim.DrainEvents())
            {
                string id = SoundCues.ForSim(e);
                if (id != "") Assert.NotNull(SoundCues.Get(id), e.Type + " -> " + id);
            }
            foreach (var t in new[] { "shift_done", "chapter", "bought", "hooked_up", "offline", "reward", "purchase_funds", "import", "crew_sent", "repaired" })
                Assert.NotNull(SoundCues.Get(SoundCues.ForSim(new SimEvent(t).With("stars", 3L))), t);
            Assert.AreEqual("ui_error", SoundCues.ForAction("buy:x", false, "cash"));
            Assert.AreEqual("music_calm", SoundCues.MusicFor(sim, false));
            var st = GameSettings.Load(TempDir());
            Assert.IsFalse(st.Music, "music is off until the player turns it on");
            Assert.IsTrue(st.Effects);
            var low = SoundCues.Limits(0);
            Assert.AreEqual(3, low.Voices);
            Assert.IsFalse(low.Ambience, "no ambience bed on Low");
            Assert.Greater(SoundCues.Limits(2).Voices, SoundCues.Limits(1).Voices);
            long bytes = Directory.GetFiles(dir, "*.ogg").Sum(f => new FileInfo(f).Length);
            Assert.Less(bytes, 2_500_000, "audio stays small (Low phones)");
        }
    }
}
