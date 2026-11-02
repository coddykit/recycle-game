// Volt City P4 — the shop: catalogue (IapCatalog) + the kit's purchase layer (CoddyPurchases on RevenueCat) + the career.
// Engine-free (CoddyPurchases lives in CoddyKit.Core), so the EditMode tests drive the real flow with FakePurchases.
//   * ad_free comes ONLY from the kit's EntitlementCache (`noads`, granted by noads / premium / grid_pass / bundle) —
//     never from "bought anything" (games/CLAUDE.md §3). The house banner / interstitial and the rewarded "no video"
//     path all read the same CoddyPurchases.AdFree.
//   * permanent rights = RC entitlements (cached offline; restore = a fresh CustomerInfo, idempotent);
//   * consumables: the store confirms -> CoddyPurchases.Deliver(storeId, txn) -> sim.GrantPurchaseFunds(txn) (idempotent per
//     transaction, kept in the career save) -> save. <= 3 packs per UTC day is a PRE-purchase gate (pill disabled);
//     never restored. Prices only from the store (displayPrice), "…" until they arrive; no trial, no subscription.
//   * the Shop tab (UI.md section 3 + kit compact shop): policy strip (ONE tap opens / closes it) -> active offer ->
//     Ad-free and packs -> upgrades -> looks -> budget -> your looks -> Restore. One pill per card.
using System;
using System.Collections.Generic;
using CoddyKit;
using VoltCity.Sim;

namespace VoltCity.Game
{
    public sealed class Commerce
    {
        public const string Loading = "…";
        public readonly IapCatalog Cat;
        public readonly CoddyPurchases Purchases;
        readonly GameSession session;
        readonly Dictionary<string, string> prices = new Dictionary<string, string>();
        public Action<string, Dictionary<string, object>> Analytics = (n, p) => { };
        /// <summary>(text, kind: success | info | warning) for the UI toast host.</summary>
        public Action<string, string> Toast = (t, k) => { };
        /// <summary>A purchase / restore / look change happened (refresh perks, looks, banner, shop).</summary>
        public event Action Changed;
        /// <summary>Sound cue hook ("purchase").</summary>
        public Action<string> Cue = _ => { };
        public Func<Loc> LocSource = () => null;

        public string Pending { get; private set; } = "";
        public bool PolicyOpen { get; private set; }
        /// <summary>The product the shop was opened on (locked feature tap, offer) — highlighted.</summary>
        public string Focus = "";
        /// <summary>This session's one-off offer ("" = none).</summary>
        public string ActiveOffer { get; private set; } = "";
        public bool StoreReady => prices.Count > 0;

        public Commerce(IapCatalog cat, CoddyPurchases purchases, GameSession session)
        {
            Cat = cat;
            Purchases = purchases;
            this.session = session;
            Purchases.Ledger.DailyCap = cat.DailyPackLimit;
            Purchases.Consumables.Clear();
            foreach (string id in cat.ConsumableIds()) Purchases.Consumables.Add(id);
            Purchases.Deliver = Deliver;
            onKitChanged = () => Changed?.Invoke();
            Purchases.Changed += onKitChanged;
        }

        readonly Action onKitChanged;

        /// <summary>The scene that owns this shop is going away (tier change / city slot switch reload the scene; the kit
        /// object lives on): stop listening to the kit and stop delivering into this career.</summary>
        public void Dispose()
        {
            Purchases.Changed -= onKitChanged;
            Purchases.Deliver = (product, tx) => false;
        }

        Simulation Sim => session.Sim;
        public bool Has(string entitlement) => Purchases.Has(entitlement);
        public bool AdFree => Purchases.AdFree;
        public bool Owned(string slug) => Cat.Owned(slug, Has);
        /// <summary>City save slots: 1, or 4 with Extra City Slots (also in the Edition / Pass / Collection).</summary>
        public int SlotCount => Has("slots") ? 4 : 1;
        public string Price(string slug) => Cat.Products.TryGetValue(slug, out var p) && prices.TryGetValue(p.Id, out var s) && !string.IsNullOrEmpty(s) ? s : "";

        // =========================================================================================== store

        /// <summary>Asks the store for every product's displayPrice (call at boot and when the shop opens without prices).</summary>
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

        /// <summary>"" = this product may be bought now, else why not (owned, pending, daily_cap, no_price, unknown).</summary>
        public string WhyNot(string slug)
        {
            if (!Cat.Products.TryGetValue(slug, out var p)) return "unknown";
            if (Pending != "") return "pending";
            if (!p.Consumable && Owned(slug)) return "owned";
            if (p.Consumable && Purchases.ConsumablesAtCap) return "daily_cap";
            if (Price(slug) == "") return "no_price";
            return "";
        }

        /// <summary>Opens the store sheet for this product. False when it is not buyable now (nothing happens).</summary>
        public bool Buy(string slug)
        {
            string why = WhyNot(slug);
            var loc = LocSource();
            if (why == "daily_cap") Toast(loc?.T("shop.cap") ?? "", "warning");
            if (why == "no_price") Toast(loc?.T("shop.unavailable") ?? "", "warning");
            if (why != "") return false;
            var p = Cat.Products[slug];
            Pending = slug;
            Analytics("purchase_start", new Dictionary<string, object> { { "product", slug } });
            lastFunds = 0;
            Purchases.Buy(p.Id, o =>
            {
                Pending = "";
                var l = LocSource();
                if (o.Success && (string.IsNullOrEmpty(o.Error) || o.Error == "already_delivered"))
                {
                    Analytics("purchase_success", new Dictionary<string, object> { { "product", slug }, { "kind", p.Kind } });
                    if (p.Consumable)
                        Toast(l != null ? l.F("shop.funds", "cash", Fmt.Money(lastFunds, l.Lang)) : "", "success");
                    else
                    {
                        foreach (string g in p.Grants) if (Array.IndexOf(Looks.All, g) >= 0) EquipIfFree(g);
                        Toast(l != null ? l.F("shop.bought", "name", Cat.Title(slug, l.Lang)) : "", "success");
                    }
                    Cue("purchase");
                    if (Focus == slug) Focus = "";
                    if (ActiveOffer == slug) ActiveOffer = "";
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

        long lastFunds;

        /// <summary>CoddyPurchases' consumable hook: the store charged transaction txn for storeId.</summary>
        bool Deliver(string storeId, string txn)
        {
            var p = Cat.ByStoreId(storeId);
            if (p == null || !p.Consumable) return false;
            var r = Sim.GrantPurchaseFunds(txn, p.Slug);
            if (!r.Ok) return false;
            lastFunds = r.Granted;
            session.Save();
            return true;
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

        // =========================================================================================== looks

        public Dictionary<string, string> Selection => session.Settings.Looks;
        public LookState Look => Looks.Resolve(Selection, Has);

        public bool ToggleLook(string look)
        {
            if (!Has(look)) return false;
            var sel = Selection;   // a copy: change it, then store it
            bool on = Looks.Toggle(sel, look);
            session.Settings.Looks = sel;
            session.Settings.Save();
            Analytics("look_equip", new Dictionary<string, object> { { "look", look }, { "on", on ? 1L : 0L } });
            Changed?.Invoke();
            return true;
        }

        /// <summary>A new look is put on at once when its slot(s) are free (a pack never swaps a look the player chose).</summary>
        void EquipIfFree(string look)
        {
            var sel = Selection;
            foreach (string slot in Looks.Slots(look))
                if (sel.TryGetValue(slot, out var v) && !string.IsNullOrEmpty(v) && Has(v)) return;
            Looks.Equip(sel, look);
            session.Settings.Looks = sel;
            session.Settings.Save();
        }

        // =========================================================================================== offers

        public OfferContext OfferCtx(string ev, bool tutorial, bool live, int awayDays = 0, int stars = 3)
        {
            var st = session.Settings;
            long first = st.FirstSeen > 0 ? st.FirstSeen : session.Now();
            return new OfferContext
            {
                Day = (int)Math.Max(0, (session.Now() - first) / 86400), Event = ev ?? "", Tutorial = tutorial, FirstSession = st.Sessions <= 1,
                LiveShift = live, AwayDays = awayDays, Stars = stars, Chapter = Sim.S.Chapter,
            };
        }

        /// <summary>Checks the offer rules for this moment; when one fits it becomes ActiveOffer (and is recorded as shown).
        /// Returns the slug or "".</summary>
        public string TryOffer(OfferContext ctx)
        {
            if (ActiveOffer != "") return "";
            var rec = session.Settings.OfferRecord;
            rec.ThisSession = offersThisSession;
            string slug = Offers.Pick(Cat, rec, ctx, Has);
            if (slug == "") return "";
            Offers.Shown(rec, slug, ctx);
            offersThisSession = rec.ThisSession;
            session.Settings.OfferRecord = rec;
            session.Settings.Save();
            ActiveOffer = slug;
            Analytics("offer_shown", new Dictionary<string, object> { { "offer", slug } });
            Changed?.Invoke();
            return slug;
        }

        int offersThisSession;

        // =========================================================================================== views

        public void TogglePolicy() => PolicyOpen = !PolicyOpen;

        /// <summary>The pill of one product row: Owned (disabled) · … (pending / no price) · price.</summary>
        public RowAction Pill(string slug, Loc loc, string target)
        {
            var p = Cat.Products[slug];
            string why = WhyNot(slug);
            if (why == "owned") return new RowAction { Id = "noop", Label = loc.T("shop.owned"), Enabled = false, Kind = "ghost", Target = target };
            if (why == "pending" && Pending == slug) return new RowAction { Id = "noop", Label = Loading, Enabled = false, Kind = "primary", Target = target };
            string price = Price(slug);
            return new RowAction { Id = "iap:" + slug, Label = price != "" ? price : Loading, Enabled = why == "", Kind = p.Consumable ? "tonal" : "primary", Target = target };
        }

        public long FundsFor(string slug) =>
            Cat.Products.TryGetValue(slug, out var p) && p.Consumable ? (long)Math.Round(Sim.DayIncome() * p.DaysX10 / 10.0, MidpointRounding.AwayFromZero) : 0;

        public Row ProductRow(string slug, Loc loc, string section)
        {
            var p = Cat.Products[slug];
            var r = new Row
            {
                Id = "prod:" + slug, Section = section, Icon = p.Icon, Title = Cat.Title(slug, loc.Lang), Target = "shop.prod." + slug,
                Open = "card:product:" + slug, Selected = Focus == slug,
            };
            if (p.Consumable)
                r.Sub = WhyNot(slug) == "daily_cap" ? loc.T("shop.cap") : loc.F("shop.cash_now", "cash", Fmt.Money(FundsFor(slug), loc.Lang));
            else r.Sub = Cat.Desc(slug, loc.Lang);
            if (p.AdFree && !Owned(slug)) r.Badge = loc.T("shop.badge.ad_free");
            if (slug == ActiveOffer) r.Badge = loc.T("shop.badge.offer");
            r.Actions.Add(Pill(slug, loc, "shop.prod." + slug + ".buy"));
            return r;
        }

        public TabView ShopTab(Loc loc)
        {
            var t = new TabView { Id = "shop", Icon = "bag", Title = loc.T("tab.shop"), Sub = loc.T("shop.sub") };
            // policy strip: the whole card is ONE tap target that opens / closes the full text (kit TapGesture, release-only)
            t.Add(new Row
            {
                Id = "policy.strip", Icon = "tip", Title = loc.T("shop.policy.t"), Sub = PolicyOpen ? loc.T("shop.policy.full") : loc.T("shop.policy.b"),
                Open = "policy", Target = "policy.strip", Wrap = PolicyOpen,
            });
            // billing unavailable (no Play account / offline / store down): say so, with a retry, above the products
            // (device pass 10-28: only the header showed, no line saying why)
            if (!StoreReady)
                t.Add(new Row { Id = "shop.unavailable", Icon = "cloud", Title = loc.T("shop.unavailable"), Target = "shop.retry" })
                    .Act("shop:retry", loc.T("shop.retry"), true, "tonal", "shop.retry");
            if (ActiveOffer != "" && Cat.Products.ContainsKey(ActiveOffer))
                t.Add(ProductRow(ActiveOffer, loc, Cat.Group("offer", loc.Lang)));
            foreach (string g in Cat.Groups)
            {
                if (g == "offer") continue;
                string sec = Cat.Group(g, loc.Lang);
                foreach (string slug in Cat.Order)
                {
                    var p = Cat.Products[slug];
                    if (p.Group != g || p.OfferOnly) continue;
                    t.Add(ProductRow(slug, loc, sec));
                }
            }
            string mine = loc.T("shop.sec.mine");
            var sel = Selection;
            foreach (string look in Looks.All)
            {
                if (!Has(look)) continue;
                bool on = Looks.InUse(sel, look);
                string title = look == "theme_gold" ? loc.T("look.theme_gold.t") : Cat.Title(look, loc.Lang);
                string sub = look == "theme_gold" ? loc.T("look.theme_gold.d") : "";
                t.Add(new Row { Id = "look:" + look, Section = mine, Icon = "star", Title = title, Sub = sub, Target = "shop.look." + look })
                    .Act("look:" + look, loc.T(on ? "shop.in_use" : "shop.use"), true, on ? "primary" : "tonal", "shop.look." + look + ".use");
            }
            t.Add(new Row { Id = "shop.restore", Section = loc.T("settings.purchases"), Icon = "restore", Title = loc.T("shop.restore.t"), Sub = loc.T("shop.restore.b") })
                .Act("restore", loc.T("shop.restore"), Pending == "", "ghost", "shop.restore");
            return t;
        }

        /// <summary>Tapping a product card: the whole description + what a pack contains + the one buy button.</summary>
        public TabView ProductCard(string slug, Loc loc)
        {
            var t = new TabView { Id = "card:product:" + slug, Icon = "bag" };
            if (!Cat.Products.TryGetValue(slug, out var p)) return t;
            t.Icon = p.Icon;
            t.Title = Cat.Title(slug, loc.Lang);
            t.Sub = Cat.Desc(slug, loc.Lang);
            if (p.Consumable)
                t.Add(new Row { Id = "pc.funds", Icon = "cash", Title = loc.F("shop.cash_now", "cash", Fmt.Money(FundsFor(slug), loc.Lang)) });
            foreach (string part in p.Parts)
                if (Cat.Products.ContainsKey(part))
                    t.Add(new Row { Id = "pc.part:" + part, Icon = Cat.Products[part].Icon, Title = Cat.Title(part, loc.Lang), Badge = Owned(part) ? loc.T("shop.owned") : "" });
            var pill = Pill(slug, loc, "card.product.buy");
            t.Primary = pill;
            return t;
        }
    }
}
