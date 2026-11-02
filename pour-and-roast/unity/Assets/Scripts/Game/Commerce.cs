// Pour & Roast — the shop: catalogue (IapCatalog, Resources/data/iap.json) + the kit's purchase layer (CoddyPurchases on
// RevenueCat) + the career. Engine-free (CoddyPurchases lives in CoddyKit.Core) so EditMode tests drive the real flow
// with FakePurchases.
//   * ad_free comes ONLY from the kit's EntitlementCache (`noads`, granted by noads / premium / roast_pass / bundle) —
//     never from "bought anything" (fleet §3). House banner / interstitial and the rewarded "no video" path all read
//     CoddyPurchases.AdFree. Legacy buyers: not applicable (new game, no earlier purchases).
//   * permanent rights = RC entitlements (cached offline; Restore = a fresh CustomerInfo, idempotent);
//   * consumables: the store confirms -> CoddyPurchases.Deliver(storeId, txn) -> sim.GrantCash(days) once per transaction
//     (txn remembered in settings) -> save. ≤ 3 packs per UTC day is a PRE-purchase gate. Never restored. The starter adds
//     a barista, the Milk Run Kit refills milk + ice (a crisis kit, never the only answer to a crisis).
//   * prices only from the store (displayPrice), "…" until they arrive; no trial, no subscription (fleet §1).
//   * offers: starter (day 3-7 after a day report, once), Milk Run Kit (a late milk van), comeback (≥ 3 days away),
//     New-Venue Pack (a venue unlock); one per session, 3 days apart; never in the guide.
using System;
using System.Collections.Generic;
using CoddyKit;
using PourAndRoast.Sim;

namespace PourAndRoast.Game
{
    public sealed class Commerce
    {
        public const string Loading = "…";
        /// <summary>RC entitlements the sim turns into perks (never saved in the career).</summary>
        public static readonly string[] PerkEntitlements = { "speed", "automation", "roast_pass", "offline_plus", "gold_trim", "slot_plus", "slots" };
        public static readonly string[] AdFreeEntitlements = { "noads" };

        public readonly IapCatalog Cat;
        public readonly CoddyPurchases Purchases;
        readonly GameSession session;
        readonly Dictionary<string, string> prices = new Dictionary<string, string>();
        public Action<string, Dictionary<string, object>> Analytics = (n, p) => { };
        public Action<string, string> Toast = (t, k) => { };
        public Action<string> Cue = _ => { };
        public Func<Loc> LocSource = () => null;
        public event Action Changed;
        public string Pending { get; private set; } = "";
        public string ActiveOffer { get; private set; } = "";
        public bool StoreReady => prices.Count > 0;
        double lastFunds;
        int offersThisSession;
        readonly Action onKitChanged;

        public Commerce(IapCatalog cat, CoddyPurchases purchases, GameSession session)
        {
            Cat = cat;
            Purchases = purchases;
            this.session = session;
            Purchases.Ledger.DailyCap = cat.DailyPackLimit;
            Purchases.Consumables.Clear();
            foreach (string id in cat.ConsumableIds()) Purchases.Consumables.Add(id);
            Purchases.Deliver = Deliver;
            onKitChanged = () => { GrantOneTimeBonuses(); Changed?.Invoke(); };
            Purchases.Changed += onKitChanged;
        }

        public void Dispose()
        {
            Purchases.Changed -= onKitChanged;
            Purchases.Deliver = (product, tx) => false;
        }

        Simulation Sim => session.Sim;
        public bool Has(string entitlement) => Purchases.Has(entitlement);
        public bool AdFree => Purchases.AdFree;
        public bool Owned(string slug) => Cat.Owned(slug, Has);
        /// <summary>Career save slots: 1 free, the Head Roaster Edition +1, Extra Saves +3.</summary>
        public int SaveSlots => 1 + (Has("slot_plus") ? 1 : 0) + (Has("slots") ? 3 : 0);

        public string Price(string slug) => Cat.Products.TryGetValue(slug, out var p) && prices.TryGetValue(p.Id, out var s) && !string.IsNullOrEmpty(s) ? s : "";

        public void LoadPrices(Action<string> done = null)
        {
            Purchases.Provider.FetchProducts(Cat.StoreIds(), (list, err) =>
            {
                if (list != null) foreach (var p in list) if (!string.IsNullOrEmpty(p.DisplayPrice)) prices[p.Id] = p.DisplayPrice;
                Changed?.Invoke();
                done?.Invoke(err ?? "");
            });
        }

        /// <summary>Tests / a provider without product queries.</summary>
        public void SetPrice(string slug, string displayPrice) { if (Cat.Products.TryGetValue(slug, out var p)) prices[p.Id] = displayPrice; }

        /// <summary>"" = may be bought now, else owned | pending | daily_cap | no_price | unknown | offer_only.</summary>
        public string WhyNot(string slug)
        {
            if (!Cat.Products.TryGetValue(slug, out var p)) return "unknown";
            if (Pending != "") return "pending";
            if (p.OfferOnly && ActiveOffer != slug) return "offer_only";
            if (!p.Consumable && Owned(slug)) return "owned";
            if (p.Consumable && Purchases.ConsumablesAtCap) return "daily_cap";
            if (Price(slug) == "") return "no_price";
            return "";
        }

        /// <summary>The shop's visible products: offers only while active, owned permanents shown as Owned.</summary>
        public List<string> Visible(string group)
        {
            var l = new List<string>();
            foreach (string slug in Cat.Order)
            {
                var p = Cat.Products[slug];
                if (p.Group != group) continue;
                if (p.OfferOnly && ActiveOffer != slug) continue;
                l.Add(slug);
            }
            return l;
        }

        public bool Buy(string slug)
        {
            string why = WhyNot(slug);
            var loc = LocSource();
            if (why == "daily_cap") Toast(loc?.T("shop.cap") ?? "", "warning");
            if (why == "no_price") Toast(loc?.T("shop.unavailable") ?? "", "warning");
            if (why != "") return false;
            var p = Cat.Products[slug];
            Pending = slug;
            lastFunds = 0;
            Analytics("purchase_start", new Dictionary<string, object> { { "product", slug } });
            Purchases.Buy(p.Id, o =>
            {
                Pending = "";
                var l = LocSource();
                if (o.Success && (string.IsNullOrEmpty(o.Error) || o.Error == "already_delivered"))
                {
                    Analytics("purchase_success", new Dictionary<string, object> { { "product", slug }, { "kind", p.Kind } });
                    if (p.Consumable) Toast(l != null ? l.F("shop.funds", "cash", Fmt.Money((long)Math.Round(lastFunds), l.Lang)) : "", "success");
                    else Toast(l != null ? l.F("shop.bought", "name", Cat.Title(slug, l.Lang)) : "", "success");
                    Cue("purchase");
                    if (ActiveOffer == slug) ActiveOffer = "";
                    GrantOneTimeBonuses();
                }
                else if (o.Cancelled) Analytics("purchase_cancel", new Dictionary<string, object> { { "product", slug } });
                else
                {
                    Analytics("purchase_fail", new Dictionary<string, object> { { "product", slug }, { "reason", Short(o.Error) } });
                    Toast(l?.T("shop.failed") ?? "", "warning");
                }
                Changed?.Invoke();
            });
            return true;
        }

        /// <summary>CoddyPurchases' consumable hook: the store charged transaction txn for storeId. Idempotent per txn.</summary>
        bool Deliver(string storeId, string txn)
        {
            var p = Cat.ByStoreId(storeId);
            if (p == null || !p.Consumable || Sim == null) return false;
            if (!string.IsNullOrEmpty(txn) && session.Settings.Delivered(txn)) return true;
            lastFunds = Sim.GrantCash(p.DaysX10 / 10.0);
            if (p.Slug == "starter" && Sim.CanBuy("staff:barista").Reason != "max" && Sim.CanBuy("staff:barista").Reason != "invalid")
            {
                Sim.S.Cash += Sim.CanBuy("staff:barista").Cost;   // the barista is part of the pack
                Sim.Buy("staff:barista");
            }
            if (p.Slug == "crisis")
            {
                Sim.S.Milk[0] += Sim.Gd.RunL[0];
                Sim.S.Milk[1] += Sim.Gd.RunL.Length > 1 ? Sim.Gd.RunL[1] : 0;
                if (Sim.IceCap > 0) Sim.S.Ice += Sim.Gd.IceBagKg;
            }
            if (!string.IsNullOrEmpty(txn)) session.Settings.MarkDelivered(txn);
            session.Settings.Save();
            session.Save();
            return true;
        }

        /// <summary>The Edition's starter beans (a sack of good roast + a day of cash), once per device (the entitlement may
        /// arrive by purchase or restore).</summary>
        void GrantOneTimeBonuses()
        {
            if (Sim == null || !Has("gold_trim") || session.Settings.Flag("premium_beans")) return;
            session.Settings.SetFlag("premium_beans");
            var st = Sim.S;
            double kg = 10, q = 80, total = st.BeanKg + kg;
            st.BeanQ = System.Math.Round((st.BeanQ * System.Math.Max(0, st.BeanKg) + q * kg) / total, 2);
            st.BeanKg = total;
            Sim.GrantCash(1.0);
            session.Settings.Save();
            session.Save();
        }

        public void Restore(Action<string> done = null)
        {
            Purchases.Restore(err =>
            {
                bool ok = string.IsNullOrEmpty(err);
                Analytics("restore_result", new Dictionary<string, object> { { "ok", ok ? 1L : 0L }, { "active", (long)Purchases.Cache.Active.Count } });
                var l = LocSource();
                Toast(l?.T(ok ? "shop.restored" : "shop.restore_failed") ?? "", ok ? "success" : "warning");
                Changed?.Invoke();
                done?.Invoke(err ?? "");
            });
        }

        static string Short(string s) => string.IsNullOrEmpty(s) ? "unknown" : s.Length > 40 ? s.Substring(0, 40) : s;

        // ------------------------------------------------------------------------------------------------ offers

        /// <summary>ev = day_report | milk_late | resume | venue. Returns the offer slug shown now, or "".</summary>
        public string TryOffer(string ev, bool tutorial, int awayDays = 0)
        {
            if (tutorial || ActiveOffer != "" || offersThisSession >= Math.Max(1, Cat.OffersPerSession)) return "";
            var st = session.Settings;
            long now = session.Now();
            if (st.LastOfferAt > 0 && now - st.LastOfferAt < Cat.OfferCooldownDays * 86400L) return "";
            int day = (int)Math.Max(0, (now - (st.FirstSeen > 0 ? st.FirstSeen : now)) / 86400);
            foreach (var kv in Cat.Offers)
            {
                var r = kv.Value;
                if (r.Event != ev || !Cat.Products.ContainsKey(r.Slug)) continue;
                if (day < r.DayMin || day > r.DayMax || awayDays < r.AwayDaysMin) continue;
                int shown = Count(st, r.Slug);
                if (shown >= r.PerGame) continue;
                if (r.Slug == "starter" && AdFree) continue;   // payers do not need the starter nudge
                ActiveOffer = r.Slug;
                offersThisSession++;
                st.LastOfferAt = now;
                st.SetFlag("offer:" + r.Slug + ":" + (shown + 1));
                st.Save();
                Analytics("offer_shown", new Dictionary<string, object> { { "offer", r.Slug } });
                Changed?.Invoke();
                return r.Slug;
            }
            return "";
        }

        static int Count(GameSettings st, string slug)
        {
            int n = 0;
            while (st.Flag("offer:" + slug + ":" + (n + 1))) n++;
            return n;
        }

        public void DismissOffer() { if (ActiveOffer != "") { ActiveOffer = ""; Changed?.Invoke(); } }
    }
}
