// Semester Rush — the shop: catalogue (IapCatalog, Resources/data/iap.json) + the kit's purchase layer (CoddyPurchases on
// RevenueCat) + the career. Engine-free (CoddyPurchases lives in CoddyKit.Core) so EditMode tests drive the real flow
// with FakePurchases.
//   * ad_free comes ONLY from the kit's EntitlementCache (`noads`, granted by noads / premium / route_pass / bundle) —
//     never from "bought anything" (fleet §3). House banner / interstitial and the rewarded "no video" path all read
//     CoddyPurchases.AdFree. Legacy buyers: not applicable (new game, no earlier purchases).
//   * permanent rights = RC entitlements (cached offline; Restore = a fresh CustomerInfo, idempotent);
//   * consumables: the store confirms -> CoddyPurchases.Deliver(storeId, txn) -> sim.GrantCash(days) once per transaction
//     (txn remembered in settings) -> save. ≤ 3 packs per UTC day is a PRE-purchase gate. Never restored.
//   * prices only from the store (displayPrice), "…" until they arrive; no trial, no subscription (fleet §1).
//   * offers: starter (day 3-7 after a day report, once), event kit (match day), comeback (≥ 3 days away); one per session,
//     3 days apart; never in the guide.
using System;
using System.Collections.Generic;
using CoddyKit;
using SemesterRush.Sim;

namespace SemesterRush.Game
{
    public sealed class Commerce
    {
        public const string Loading = "…";
        /// <summary>RC entitlements the sim turns into perks (never saved in the career).</summary>
        public static readonly string[] PerkEntitlements = { "route_pass", "dispatcher_plus", "offline_plus", "speed", "gold_livery" };
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
            if (p.Slug == "starter" && Sim.CanBuy("truck:van").Reason != "max")
            {
                int id = 0;
                foreach (var t in Sim.S.Trucks) id = Math.Max(id, t.Id + 1);
                Sim.S.Trucks.Add(new TruckState { Id = id, Type = "van", AtNode = Sim.Gd.DepotNode });
            }
            if (!string.IsNullOrEmpty(txn)) session.Settings.MarkDelivered(txn);
            session.Settings.Save();
            session.Save();
            return true;
        }

        /// <summary>The Edition's starter cash, once per device (the entitlement may arrive by purchase or restore).</summary>
        void GrantOneTimeBonuses()
        {
            if (Sim == null || !Has("gold_livery") || session.Settings.Flag("premium_cash")) return;
            session.Settings.SetFlag("premium_cash");
            Sim.GrantCash(2.0);
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

        /// <summary>ev = day_report | match_day | resume. Returns the offer slug shown now, or "".</summary>
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
