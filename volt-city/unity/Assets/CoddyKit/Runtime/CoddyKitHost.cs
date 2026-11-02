// CoddyKit (Unity) — the one persistent kit object (DontDestroyOnLoad), the Unity counterpart of the Godot autoloads
// (Ads, Iap, PerfDevice/PerfGuard, ReviewGate). Boot order:
//   1. QualityTiers.Init()   — before the first 3D frame (device class, player's choice, GPU breadcrumb)
//   2. CoddyPurchases        — cached rights first (ad_free must be right offline), then RevenueCat
//   3. HouseAdsService       — house banner + interstitial (house only), ads-config fetch
//   4. RewardedController    — UMP consent -> the ONE MobileAds.Initialize -> rewarded loads (AdMob = rewarded only)
//   5. CoddyReview           — session counted
// Game code reaches everything through CoddyKitHost.Instance (alias Kit). UNCOMPILED until the editor is installed.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using CoddyKit.Ads;
using CoddyKit.Ui;

namespace CoddyKit
{
    public sealed class CoddyKitHost : MonoBehaviour
    {
        public static CoddyKitHost Instance { get; private set; }

        public KitConfig Config { get; private set; }
        public string Dir { get; private set; }
        public PanelSettings Panel { get; private set; }
        public QualityTiers Tiers { get; private set; }
        public CoddyPurchases Purchases { get; private set; }
        public HouseAdsService Ads { get; private set; }
        public RewardedController Rewarded { get; private set; }
        public RcAdTracker RcAds { get; private set; }
        public CoddyReview Review { get; private set; }
        public UiToastHost Toasts { get; private set; }
        public static bool DebugBuild => Debug.isDebugBuild;

        readonly List<(double due, Action act)> timers = new List<(double, Action)>();
        JsonStateFile rewardedFile;

        static double UnixNow() => (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

        /// <summary>Creates the kit (once). panel = the kit PanelSettings (Resources/CoddyPanelSettings, made by ProjectSetup).</summary>
        public static CoddyKitHost Boot(KitConfig config, PanelSettings panel)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("CoddyKit");
            DontDestroyOnLoad(go);
            var host = go.AddComponent<CoddyKitHost>();
            Instance = host;
            host.Init(config, panel);
            return host;
        }

        void Init(KitConfig config, PanelSettings panel)
        {
            Config = config;
            Panel = panel;
            Dir = Path.Combine(Application.persistentDataPath, "coddykit");
            Directory.CreateDirectory(Dir);
            Debug.Log("[CoddyKit] bridges: " + (CoddyServices.Registered.Count > 0 ? string.Join(", ", CoddyServices.Registered) : "none (fakes)"));

            Tiers = new QualityTiers(Dir);
            Tiers.Init();

            Purchases = new CoddyPurchases(Dir, config.AdFreeEntitlements, UnixNow);
            Purchases.Start(config.RevenueCatApiKey);

            if (panel != null)
            {
                var toastGo = new GameObject("CoddyToasts");
                toastGo.transform.SetParent(transform, false);
                var doc = toastGo.AddComponent<UIDocument>();
                doc.panelSettings = panel;
                doc.sortingOrder = 95;
                doc.rootVisualElement.pickingMode = PickingMode.Ignore;
                doc.rootVisualElement.style.position = Position.Absolute;
                doc.rootVisualElement.style.left = 0; doc.rootVisualElement.style.right = 0;
                doc.rootVisualElement.style.top = 0; doc.rootVisualElement.style.bottom = 0;
                Toasts = toastGo.AddComponent<UiToastHost>();
                Toasts.Init(doc.rootVisualElement);
            }

            Ads = gameObject.AddComponent<HouseAdsService>();
            Ads.Init(config, Dir, () => Purchases.AdFree, panel);

            RcAds = new RcAdTracker
            {
                Ready = () => CoddyServices.Purchases.Configured,
                Sink = e => CoddyServices.Purchases.TrackAdEvent(e),
            };
            rewardedFile = new JsonStateFile(Path.Combine(Dir, "coddyads", "rewarded.json")).Load();
            var rules = new RewardedRules(new SystemClock(), rewardedFile.Data)
            {
                BudgetSingle = config.RewardedBudgetSingle,
                BudgetDaily = config.RewardedBudgetDaily,
            };
            foreach (var kv in config.RewardedLimits) rules.Limits[kv.Key] = kv.Value;
            Rewarded = new RewardedController(rules, CoddyServices.Consent, CoddyServices.Rewarded, CoddyServices.Analytics, RcAds)
            {
                Mobile = Application.platform == RuntimePlatform.Android,
                UnitId = RewardedController.UnitFor(config.RewardedUnitRelease, DebugBuild),
                AdFree = () => Purchases.AdFree,
                PauseGame = p =>
                {
                    Ads.Showing = p;
                    config.PauseGame?.Invoke(p);
                },
                Schedule = (s, a) => timers.Add((Time.realtimeSinceStartupAsDouble + s, a)),
                OnVideoCompleted = () => Ads.RewardedCompleted(),
                Persist = () =>
                {
                    rewardedFile.Data = rules.Usage;
                    rewardedFile.Save();
                },
            };
            Ads.RemoteChanged += remote =>
            {
                Rewarded.SetRemote(remote.RewardedEnabled);
                RcAds.SetEnabled(remote.RcAdTracking);
            };
            Rewarded.Start();

            Review = new CoddyReview(Dir, config.ReviewMinPlayS, UnixNow);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            Tiers?.Tick(dt);
            if (timers.Count == 0) return;
            double t = Time.realtimeSinceStartupAsDouble;
            for (int i = timers.Count - 1; i >= 0; i--)
            {
                if (timers[i].due > t) continue;
                var a = timers[i].act;
                timers.RemoveAt(i);
                a?.Invoke();
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) Review?.Save();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
