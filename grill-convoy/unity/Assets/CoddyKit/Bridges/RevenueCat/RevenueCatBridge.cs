// CoddyKit (Unity) — RevenueCat purchases bridge (RC Unity SDK, package com.revenuecat.purchases-unity >= 7.0.0 via
// asmdef versionDefines -> CODDY_RC). One-time products only ("inapp"; no subscription, no trial in the fleet).
// Entitlements = CustomerInfo.Entitlements.Active keys. Consumable transaction id = the newest NonSubscriptionTransaction
// of that product (idempotent delivery in CoddyPurchases' ledger).
// RC AD TRACKING (games/CLAUDE.md §5b): RC Unity SDK 9.x has Purchases.AdTracker (verified against 9.16.0 on
// 2026-10-28): loaded / displayed / opened / revenue / failed_to_load with mediator AdMob. Revenue only from AdMob's
// OnAdPaid values (micros + currency + precision unchanged), never invented. Compiled against RC 9.16.0 (Unity 6000.3).
#if CODDY_RC
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CoddyKit.Bridges
{
    public static class RevenueCatBridgeRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            CoddyServices.Purchases = new RcPurchasesProvider();
            CoddyServices.Registered.Add("RevenueCat");
#endif
        }
    }

    public sealed class RcPurchasesProvider : IPurchasesProvider
    {
        Purchases rc;
        readonly Dictionary<string, Purchases.StoreProduct> products = new Dictionary<string, Purchases.StoreProduct>();
        public bool Configured { get; private set; }
        public event Action<ICollection<string>> EntitlementsChanged;

        internal void Raise(Purchases.CustomerInfo info) => EntitlementsChanged?.Invoke(Active(info));

        static ICollection<string> Active(Purchases.CustomerInfo info)
        {
            var list = new List<string>();
            if (info?.Entitlements?.Active != null) list.AddRange(info.Entitlements.Active.Keys);
            return list;
        }

        public void Configure(string apiKey, string appUserId)
        {
            if (Configured) return;
            var go = new GameObject("RevenueCat");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.SetActive(false); // set useRuntimeSetup before the component's Start
            rc = go.AddComponent<Purchases>();
            rc.useRuntimeSetup = true;
            var listener = go.AddComponent<RcCustomerInfoListener>();
            listener.Owner = this;
            rc.listener = listener;
            go.SetActive(true);
            // RC 9.x creates its native wrapper in Purchases.Start() (the next frame): configuring right after AddComponent
            // threw a NullReferenceException on the device (10-28 smoke). Configure once Start has run; until then every
            // call answers "not_configured" (the kit retries prices / restore on the next shop open).
            listener.StartCoroutine(ConfigureAfterStart(apiKey, appUserId));
        }

        System.Collections.IEnumerator ConfigureAfterStart(string apiKey, string appUserId)
        {
            yield return null;
            yield return null;
            try
            {
                var builder = Purchases.PurchasesConfiguration.Builder.Init(apiKey);
                if (!string.IsNullOrEmpty(appUserId)) builder = builder.SetAppUserId(appUserId);
                rc.Configure(builder.Build());
                Configured = true;
                // the kit's first Refresh() ran before this: push the entitlements now
                rc.GetCustomerInfo((info, error) => { if (error == null) Raise(info); });
            }
            catch (Exception e) { Debug.LogWarning("[CoddyKit] RevenueCat configure failed: " + e.Message); }
        }

        public void FetchEntitlements(Action<ICollection<string>, string> done)
        {
            if (!Configured) { done?.Invoke(null, "not_configured"); return; }
            rc.GetCustomerInfo((info, error) => done?.Invoke(error == null ? Active(info) : null, error?.Message ?? ""));
        }

        public void FetchProducts(IList<string> productIds, Action<IList<StoreProduct>, string> done)
        {
            if (!Configured) { done?.Invoke(new List<StoreProduct>(), "not_configured"); return; }
            var ids = new string[productIds.Count];
            productIds.CopyTo(ids, 0);
            rc.GetProducts(ids, (list, error) =>
            {
                var outList = new List<StoreProduct>();
                if (list != null)
                    foreach (var p in list)
                    {
                        products[p.Identifier] = p;
                        outList.Add(new StoreProduct { Id = p.Identifier, Title = p.Title, Description = p.Description, DisplayPrice = p.PriceString });
                    }
                done?.Invoke(outList, error?.Message ?? "");
            }, "inapp");
        }

        public void Purchase(string productId, Action<PurchaseOutcome> done)
        {
            if (!Configured) { done?.Invoke(new PurchaseOutcome { ProductId = productId, Error = "not_configured" }); return; }
            rc.PurchaseProduct(productId, result =>
            {
                // RC 9.x: one PurchaseResult (customer info, cancelled flag, error, the transaction itself)
                var info = result?.CustomerInfo;
                var error = result?.Error;
                bool cancelled = result != null && result.UserCancelled;
                string tx = result?.StoreTransaction?.TransactionIdentifier;
                done?.Invoke(new PurchaseOutcome
                {
                    Success = result != null && error == null && !cancelled,
                    Cancelled = cancelled,
                    Error = error?.Message ?? (result == null ? "no_result" : ""),
                    ProductId = productId,
                    TransactionId = string.IsNullOrEmpty(tx) ? LatestTransaction(info, productId) : tx,
                    ActiveEntitlements = error == null ? Active(info) : null,
                });
            }, "inapp");
        }

        static string LatestTransaction(Purchases.CustomerInfo info, string productId)
        {
            string best = "";
            DateTime when = DateTime.MinValue;
            if (info?.NonSubscriptionTransactions == null) return best;
            foreach (var t in info.NonSubscriptionTransactions)
            {
                if (t.ProductIdentifier != productId || t.PurchaseDate < when) continue;
                when = t.PurchaseDate;
                best = t.TransactionIdentifier;
            }
            return best;
        }

        public void Restore(Action<ICollection<string>, string> done)
        {
            if (!Configured) { done?.Invoke(null, "not_configured"); return; }
            rc.RestorePurchases((info, error) => done?.Invoke(error == null ? Active(info) : null, error?.Message ?? ""));
        }

        public bool TrackAdEvent(AdTrackEvent e)
        {
            var t = rc != null && Configured ? rc.AdTracker : null;
            if (t == null) return false;
            var med = RevenueCat.AdTracker.MediatorName.AdMob;
            var fmt = e.Format == "rewarded" ? RevenueCat.AdTracker.Format.Rewarded : RevenueCat.AdTracker.Format.Other;
            string unit = e.Unit ?? "", imp = e.AdId ?? "", place = string.IsNullOrEmpty(e.Placement) ? null : e.Placement;
            switch (e.Kind)
            {
                case "loaded": t.TrackAdLoaded(new RevenueCat.AdLoadedData(med, fmt, unit, imp, null, place)); return true;
                case "displayed": t.TrackAdDisplayed(new RevenueCat.AdDisplayedData(med, fmt, unit, imp, null, place)); return true;
                case "opened": t.TrackAdOpened(new RevenueCat.AdOpenedData(med, fmt, unit, imp, null, place)); return true;
                case "failed_to_load":
                    int? code = int.TryParse(e.Error, out int c) ? c : (int?)null;
                    t.TrackAdFailedToLoad(new RevenueCat.AdFailedToLoadData(med, fmt, unit, place, code));
                    return true;
                case "revenue":
                    if (string.IsNullOrEmpty(e.Currency)) return false; // no paid value from AdMob = nothing to report
                    t.TrackAdRevenue(new RevenueCat.AdRevenueData(med, fmt, unit, imp, e.ValueMicros, e.Currency, Precision(e.Precision), null, place));
                    return true;
                default: return false;
            }
        }

        // GoogleMobileAds AdValue.PrecisionType: 0 Unknown, 1 Estimated, 2 PublisherProvided, 3 Precise
        static RevenueCat.AdTracker.Precision Precision(int gma) => gma switch
        {
            3 => RevenueCat.AdTracker.Precision.Exact,
            2 => RevenueCat.AdTracker.Precision.PublisherDefined,
            1 => RevenueCat.AdTracker.Precision.Estimated,
            _ => RevenueCat.AdTracker.Precision.Unknown,
        };
    }
}
#endif
