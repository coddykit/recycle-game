// Volt City — the game's kit configuration (the Unity counterpart of the Godot project.godot [coddy_ads] block).
// Values from docs/IAP.md (F0, Patron 2026-10-27). Release ids are the REAL ones created by the Patron in P8 (2026-10-28:
// AdMob rewarded unit, RevenueCat project projadb75502 / Play app app02e71609c2 public SDK key; never a made-up id,
// games/CLAUDE.md §5). Debug builds still use Google's test units. jenkins/preflight.py fails a production build if emptied.
// UNCOMPILED until the Unity editor is installed.
using System.Collections.Generic;
using CoddyKit;
using CoddyKit.Ads;
using UnityEngine;
using VoltCity.Game;

namespace VoltCity.App
{
    public static class VoltGameConfig
    {
        public const string AppId = "volt-city";                 // ads-config X-App-ID, house-ad host id
        public const string Package = "com.coddykit.voltcity";
        public const string ProductName = "Grid Keeper";          // store title: "Grid Keeper: Energy Tycoon" (docs/NAME_CHECK.md, P6)

        // ---- P4/P8 (Patron): real ids. Keep the constant names: jenkins/preflight.py reads them. ----
        public const string AdMobRewardedAndroid = ""; // ADMOB_REWARDED_VOLTCITY_ANDROID
        public const string RevenueCatAndroidKey = ""; // RC public SDK key (project Grid Keeper)
        // AdMob APP id  (ADMOB_APP_ID_VOLTCITY_ANDROID): set at build time from
        // VOLTCITY_ADMOB_APP_ID (jenkins/Jenkinsfile) by VoltAndroidBuild; the committed GoogleMobileAdsSettings keeps the sample.

        /// <summary>RC entitlements that remove forced ads (§3/§8): noads is granted by noads / premium / grid_pass / bundle.</summary>
        public static readonly string[] AdFreeEntitlements = { "noads" };

        /// <summary>Reward units = milli-days of income (cash_s = 1 day = 1000). §4: single <= 1/5 (200), daily <= 1/2 (500).
        /// One source: VoltCity.Game.VoltRewards (EditMode-tested against balance.json and the cheapest pack).</summary>
        public const int CheapestPackUnits = VoltRewards.UnitsPerDay;
        public const int RewardSingleMax = VoltRewards.UnitsSingleMax;
        public const int RewardDailyMax = VoltRewards.UnitsDailyMax;

        /// <summary>docs/IAP.md §3 — 4 placements (= balance.json rewards.placements). Non-cash placements ask for 1 unit.</summary>
        public static Dictionary<string, RewardedLimit> RewardedLimits() => VoltRewards.KitLimits();

        public static readonly string[] Languages = { "en", "tr", "de", "es", "fr", "pt", "ru", "ja", "ko", "hi" };

        /// <summary>Until the P5 localisation exists: the device language if it is one of the 10, else English.</summary>
        public static string DeviceLanguage()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Turkish: return "tr";
                case SystemLanguage.German: return "de";
                case SystemLanguage.Spanish: return "es";
                case SystemLanguage.French: return "fr";
                case SystemLanguage.Portuguese: return "pt";
                case SystemLanguage.Russian: return "ru";
                case SystemLanguage.Japanese: return "ja";
                case SystemLanguage.Korean: return "ko";
                case SystemLanguage.Hindi: return "hi";
                default: return "en";
            }
        }

        public static KitConfig Kit()
        {
            return new KitConfig
            {
                AppId = AppId,
                Platform = "android",
                Kids = false,
                RewardedUnitRelease = AdMobRewardedAndroid,
                RewardedLimits = RewardedLimits(),
                RewardedBudgetSingle = RewardSingleMax,
                RewardedBudgetDaily = RewardDailyMax,
                RevenueCatApiKey = RevenueCatAndroidKey,
                AdFreeEntitlements = AdFreeEntitlements,
                UiLanguage = DeviceLanguage,
                ReviewMinPlayS = 240.0,
            };
        }
    }
}
