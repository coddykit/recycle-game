// CoddyKit (Unity) — the seams to the vendor SDKs. Engine-free interfaces; the real implementations live in the bridge
// assemblies that only compile when their package is installed (asmdef versionDefines + defineConstraints):
//   CoddyKit.AdMob      (com.google.ads.mobile)              -> IRewardedProvider + IConsentProvider (UMP)
//   CoddyKit.RevenueCat (com.revenuecat.purchases-unity)     -> IPurchasesProvider
//   CoddyKit.PlayReview (com.google.play.review)             -> IReviewProvider
// Without a package the kit runs on the fakes below (editor, tests, a build without that SDK = feature off, never a crash).
// Bridges register themselves in CoddyServices at [RuntimeInitializeOnLoadMethod(BeforeSceneLoad)].
using System;
using System.Collections.Generic;

namespace CoddyKit
{
    public interface IAnalytics
    {
        void Event(string name, Dictionary<string, object> parameters);
    }

    public interface IConsentProvider
    {
        /// <summary>UMP: update consent info, show the form when required, then report whether ads may be requested.</summary>
        void Gather(Action<bool, string> done);
        bool CanRequestAds { get; }
        bool PrivacyOptionsRequired { get; }
        void ShowPrivacyOptions(Action<string> done);
    }

    /// <summary>One ad SDK event for RevenueCat ad tracking (games/CLAUDE.md §5b). Kind: loaded / displayed / opened /
    /// revenue / failed_to_load. Revenue only from the SDK's paid callback (micros, currency, precision unchanged).</summary>
    public struct AdTrackEvent
    {
        public string Kind;
        public string AdId;        // per loaded ad instance (dedupe key)
        public string Format;      // "rewarded"
        public string Unit;
        public string Placement;
        public long ValueMicros;
        public string Currency;
        public int Precision;
        public string Error;
    }

    public interface IRewardedProvider
    {
        bool Initialized { get; }
        /// <summary>MobileAds.Initialize. The ONLY caller is RewardedController (fleet rule: one init site, nothing loads
        /// before it completed — Train Baron 1.1.2 crash cluster).</summary>
        void Initialize(Action done);
        bool IsLoaded { get; }
        void Load(string unitId, Action<bool, string> done);
        /// <summary>Shows the loaded ad. onEarned may come before or after onClosed; onFailed replaces onClosed.</summary>
        void Show(string placement, Action onEarned, Action onClosed, Action<string> onFailed);
        event Action<AdTrackEvent> Tracked;
    }

    public struct StoreProduct
    {
        public string Id;
        public string Title;
        public string Description;
        public string DisplayPrice; // always the store's string; the game never invents a price
        public bool Consumable;
    }

    public struct PurchaseOutcome
    {
        public bool Success;
        public bool Cancelled;
        public string Error;
        public string ProductId;
        public string TransactionId;
        public ICollection<string> ActiveEntitlements;
    }

    public interface IPurchasesProvider
    {
        bool Configured { get; }
        void Configure(string apiKey, string appUserId);
        void FetchEntitlements(Action<ICollection<string>, string> done);
        void FetchProducts(IList<string> productIds, Action<IList<StoreProduct>, string> done);
        void Purchase(string productId, Action<PurchaseOutcome> done);
        void Restore(Action<ICollection<string>, string> done);
        event Action<ICollection<string>> EntitlementsChanged;
        /// <summary>RevenueCat ad event (§5b). Providers whose SDK has no ad-tracking API return false (reported in the
        /// release report; no invented revenue).</summary>
        bool TrackAdEvent(AdTrackEvent e);
    }

    public interface IReviewProvider
    {
        /// <summary>Requests the OS in-app review sheet. launched=false when the OS / plugin could not start the flow.</summary>
        void Request(Action<bool, string> done);
    }

    /// <summary>Registry the bridges fill at startup. Never null: missing SDK = a fake that does nothing harmful.</summary>
    public static class CoddyServices
    {
        public static IAnalytics Analytics = new NullAnalytics();
        public static IConsentProvider Consent = new FakeConsent();
        public static IRewardedProvider Rewarded = new FakeRewarded { Available = false };
        public static IPurchasesProvider Purchases = new FakePurchases();
        public static IReviewProvider Review = new NullReview();
        /// <summary>Consent mode v2 receiver (Firebase bridge); the UMP bridge calls it once UMP has answered (ConsentMode.cs).</summary>
        public static System.Action<ConsentSignals> ConsentModeSink = s => { };
        public static ConsentSignals LastConsentMode = ConsentSignals.Denied;
        /// <summary>True once UMP's answer has been handed to the consent sink; ConsentGatedAnalytics holds events until then.</summary>
        public static bool ConsentModeApplied;
        public static event System.Action ConsentApplied;

        public static void ApplyConsentMode(ConsentSignals s)
        {
            LastConsentMode = s;
            ConsentModeSink?.Invoke(s);          // Firebase SetConsent first ...
            ConsentModeApplied = true;
            ConsentApplied?.Invoke();            // ... then the held events
        }

        /// <summary>Names of the real bridges that registered (boot log, preflight).</summary>
        public static readonly List<string> Registered = new List<string>();

        public static void ResetToFakes()
        {
            Analytics = new NullAnalytics();
            Consent = new FakeConsent();
            Rewarded = new FakeRewarded { Available = false };
            Purchases = new FakePurchases();
            Review = new NullReview();
            Registered.Clear();
            ConsentModeSink = s => { };
            LastConsentMode = ConsentSignals.Denied;
            ConsentModeApplied = false;
            ConsentApplied = null;
        }
    }

    // ---------------------------------------------------------------- fakes (editor, tests, SDK not installed)

    public sealed class NullAnalytics : IAnalytics
    {
        public readonly List<string> Log = new List<string>();
        public void Event(string name, Dictionary<string, object> p) { Log.Add(name); }
    }

    public sealed class FakeConsent : IConsentProvider
    {
        public bool Can = true;
        public bool Required;
        public int Gathered;
        public void Gather(Action<bool, string> done) { Gathered++; done?.Invoke(Can, ""); }
        public bool CanRequestAds => Can;
        public bool PrivacyOptionsRequired => Required;
        public void ShowPrivacyOptions(Action<string> done) { done?.Invoke(""); }
    }

    public sealed class FakeRewarded : IRewardedProvider
    {
        public bool Available = true;
        public bool FailLoad;
        public int InitCalls, LoadCalls, ShowCalls;
        public bool EarnBeforeClose = true;
        public bool CloseWithoutEarn;
        bool loaded;
        public bool Initialized { get; private set; }
        public bool IsLoaded => loaded;
        public event Action<AdTrackEvent> Tracked;

        public void Initialize(Action done)
        {
            InitCalls++;
            Initialized = Available;
            done?.Invoke();
        }

        public void Load(string unitId, Action<bool, string> done)
        {
            if (!Initialized) throw new InvalidOperationException("Load before Initialize (fleet crash rule)");
            LoadCalls++;
            loaded = Available && !FailLoad;
            if (loaded) Tracked?.Invoke(new AdTrackEvent { Kind = "loaded", AdId = "fake" + LoadCalls, Format = "rewarded", Unit = unitId });
            else Tracked?.Invoke(new AdTrackEvent { Kind = "failed_to_load", Format = "rewarded", Unit = unitId, Error = "3" });
            done?.Invoke(loaded, loaded ? "" : "no_fill");
        }

        public void Show(string placement, Action onEarned, Action onClosed, Action<string> onFailed)
        {
            ShowCalls++;
            if (!loaded) { onFailed?.Invoke("not_loaded"); return; }
            loaded = false;
            Tracked?.Invoke(new AdTrackEvent { Kind = "displayed", AdId = "fake" + LoadCalls, Format = "rewarded", Placement = placement });
            if (CloseWithoutEarn) { onClosed?.Invoke(); return; }
            if (EarnBeforeClose) { onEarned?.Invoke(); onClosed?.Invoke(); }
            else { onClosed?.Invoke(); onEarned?.Invoke(); }
        }
    }

    public sealed class FakePurchases : IPurchasesProvider
    {
        public readonly HashSet<string> Active = new HashSet<string>();
        public readonly Dictionary<string, string[]> Grants = new Dictionary<string, string[]>();
        public bool Offline;
        public bool Configured { get; private set; }
        public event Action<ICollection<string>> EntitlementsChanged;
        public readonly List<AdTrackEvent> AdEvents = new List<AdTrackEvent>();
        int tx;

        public void Configure(string apiKey, string appUserId) { Configured = true; }

        public void FetchEntitlements(Action<ICollection<string>, string> done)
        {
            if (Offline) done?.Invoke(null, "offline"); else done?.Invoke(new List<string>(Active), "");
        }

        public void FetchProducts(IList<string> productIds, Action<IList<StoreProduct>, string> done)
        {
            done?.Invoke(new List<StoreProduct>(), Offline ? "offline" : "");
        }

        public void Purchase(string productId, Action<PurchaseOutcome> done)
        {
            if (Offline) { done?.Invoke(new PurchaseOutcome { Error = "offline", ProductId = productId }); return; }
            if (Grants.TryGetValue(productId, out var g)) foreach (string e in g) Active.Add(e);
            tx++;
            EntitlementsChanged?.Invoke(new List<string>(Active));
            done?.Invoke(new PurchaseOutcome { Success = true, ProductId = productId, TransactionId = "GPA.fake-" + tx, ActiveEntitlements = new List<string>(Active) });
        }

        public void Restore(Action<ICollection<string>, string> done) => FetchEntitlements(done);

        public bool TrackAdEvent(AdTrackEvent e)
        {
            AdEvents.Add(e);
            return true;
        }
    }

    public sealed class NullReview : IReviewProvider
    {
        public int Requests;
        public void Request(Action<bool, string> done) { Requests++; done?.Invoke(false, "no_review_plugin"); }
    }
}
