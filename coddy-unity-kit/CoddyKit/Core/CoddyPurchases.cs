// CoddyKit (Unity) — purchases on RevenueCat (IPurchasesProvider; the bridge CoddyKit.RevenueCat wraps the RC Unity SDK).
// Entitlement ids = the RC entitlements of the game's catalogue (Volt City docs/IAP.md: 14 entitlements; ad-free =
// `noads`). The cached set keeps an offline player's rights; a fresh CustomerInfo replaces it (refunds remove rights).
// Consumables: delivered once per store transaction id, <= ConsumableDailyCap per UTC day, never restored; the game's
// Deliver callback grants the funds (sim: idempotent grant_purchase_funds). Prices only ever come from the store
// (StoreProduct.DisplayPrice). No trial / subscription in the fleet. Engine-free: lives in CoddyKit.Core since Volt City P4
// (moved from Runtime so the game layer + its EditMode tests drive the real purchase flow with FakePurchases).
using System;
using System.Collections.Generic;
using System.IO;

namespace CoddyKit
{
    public sealed class CoddyPurchases
    {
        public readonly EntitlementCache Cache;
        public readonly ConsumableLedger Ledger = new ConsumableLedger();
        public IPurchasesProvider Provider => CoddyServices.Purchases;
        public bool AdFree => Cache.AdFree;
        /// <summary>No more consumable packs today (UTC): the shop disables them BEFORE the store sheet opens.</summary>
        public bool ConsumablesAtCap => Ledger.AtCap(now());
        public bool Has(string entitlement) => Cache.Has(entitlement);
        /// <summary>Entitlements / ad-free changed (refresh the shop, hide the banner at its next check).</summary>
        public event Action Changed;
        /// <summary>Consumable product ids (the game's catalogue). Their purchase calls Deliver(productId, transactionId).</summary>
        public readonly HashSet<string> Consumables = new HashSet<string>();
        /// <summary>Grants a consumable; returns false when the game could not (then the ledger does not record it).</summary>
        public Func<string, string, bool> Deliver = (product, tx) => false;

        readonly JsonStateFile cacheFile;
        readonly JsonStateFile ledgerFile;
        readonly Func<double> now;

        public CoddyPurchases(string kitDir, IEnumerable<string> adFreeEntitlements, Func<double> unixNow)
        {
            now = unixNow;
            Cache = new EntitlementCache(adFreeEntitlements);
            cacheFile = new JsonStateFile(Path.Combine(kitDir, "iap", "entitlements.json")).Load();
            ledgerFile = new JsonStateFile(Path.Combine(kitDir, "iap", "consumables.json")).Load();
            Cache.FromJson(cacheFile.Data);
            Ledger.FromJson(ledgerFile.Data);
        }

        public void Start(string apiKey, string appUserId = null)
        {
            Provider.EntitlementsChanged += active => Apply(active);
            if (string.IsNullOrEmpty(apiKey)) return; // purchases off until the RC key exists (P4); cached rights still count
            if (!Provider.Configured) Provider.Configure(apiKey, appUserId);
            Refresh();
        }

        public void Refresh(Action<string> done = null)
        {
            Provider.FetchEntitlements((active, err) =>
            {
                if (string.IsNullOrEmpty(err)) Apply(active); // an error (offline) keeps the cached rights
                done?.Invoke(err ?? "");
            });
        }

        void Apply(ICollection<string> active)
        {
            if (active == null) return;
            if (Cache.Replace(active, now()))
            {
                cacheFile.Data = Cache.ToJson();
                cacheFile.Save();
                Changed?.Invoke();
            }
        }

        public void Buy(string productId, Action<PurchaseOutcome> done)
        {
            Provider.Purchase(productId, o =>
            {
                if (o.Success)
                {
                    if (o.ActiveEntitlements != null) Apply(o.ActiveEntitlements);
                    if (Consumables.Contains(productId))
                    {
                        // the store charged: deliver once per transaction id (never refused by the daily cap — that is the
                        // shop's pre-purchase gate, Ledger.AtCap); a game that cannot deliver reports deliver_failed
                        string block = Ledger.ConfirmedBlocker(o.TransactionId);
                        if (block == "" && Deliver(productId, o.TransactionId))
                        {
                            Ledger.Record(o.TransactionId, now());
                            ledgerFile.Data = Ledger.ToJson();
                            ledgerFile.Save();
                        }
                        else o.Error = block != "" ? block : "deliver_failed";
                    }
                }
                done?.Invoke(o);
            });
        }

        public void Restore(Action<string> done)
        {
            Provider.Restore((active, err) =>
            {
                if (string.IsNullOrEmpty(err)) Apply(active);
                done?.Invoke(err ?? "");
            });
        }
    }

    /// <summary>Play In-App Review at a wow moment (ReviewGate rules; the OS sheet only).</summary>
    public sealed class CoddyReview
    {
        public readonly ReviewGate Gate;
        readonly JsonStateFile file;
        readonly Func<double> now;
        float saveTimer;
        public Func<ReviewGate.Context> Context = () => new ReviewGate.Context();

        public CoddyReview(string kitDir, double minPlayS, Func<double> unixNow)
        {
            now = unixNow;
            file = new JsonStateFile(Path.Combine(kitDir, "review.json")).Load();
            Gate = ReviewGate.FromJson(file.Data);
            Gate.MinPlayS = minPlayS;
            Gate.SessionStarted();
            Save();
        }

        /// <summary>Real play time only (the game calls it from its play loop: not in menus, not paused).</summary>
        public void TickPlay(float dt)
        {
            Gate.Tick(dt);
            saveTimer += dt;
            if (saveTimer > 30f) { saveTimer = 0f; Save(); }
        }

        /// <summary>The game reports a wow moment ("first_3star_storm_shift", "district_lit"…). Returns true when the OS
        /// sheet was requested (an attempt is counted either way once requested; the OS may still show nothing).</summary>
        public bool Wow(string moment)
        {
            if (!Gate.MayAsk(Context(), now())) return false;
            Gate.Asked(now());
            Save();
            CoddyServices.Analytics.Event("review_prompt", new Dictionary<string, object> { { "moment", moment ?? "" }, { "attempt", (long)Gate.Asks } });
            CoddyServices.Review.Request((launched, err) => { });
            return true;
        }

        public void Save()
        {
            file.Data = Gate.ToJson();
            file.Save();
        }
    }
}
