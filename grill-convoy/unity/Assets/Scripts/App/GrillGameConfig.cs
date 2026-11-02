// Grill Convoy — the game's kit configuration (the Unity counterpart of a Godot project.godot [coddy_ads] block).
// Release ids are EMPTY until the Patron creates them in P8 (AdMob rewarded unit, RevenueCat public SDK key) — never a
// made-up id (fleet §5). Debug builds use Google's test rewarded unit; a release build with an empty id has no rewarded
// video (buttons hidden) and no purchases (fake store) — the preflight must fail a production build until they are set.
using System.Collections.Generic;
using CoddyKit;
using CoddyKit.Ads;
using UnityEngine;
using GrillConvoy.Game;

namespace GrillConvoy.App
{
    public static class GrillGameConfig
    {
        public const string AppId = "grill-convoy";                 // ads-config X-App-ID, house-ad host id
        public const string Package = "com.coddykit.grillconvoy";
        public const string ProductName = "Grill Convoy";           // store title "Grill Convoy: Burger Trucks" (27 chars, plan §11)
        public const string PrivacyUrl = "https://www.coddykit.com/pages/privacy-policy";

        // ---- P8 (Patron): real ids. Keep the constant names (a preflight reads them). ----
        public const string AdMobRewardedAndroid = "";   // ADMOB_REWARDED_GRILLCONVOY_ANDROID (store-automation/.env)
        public const string RevenueCatAndroidKey = "";   // RC public SDK key goog_… (project Grill Convoy)

        /// <summary>RC entitlements that remove forced ads (fleet §3/§8): noads, granted by noads / premium / convoy_pass / bundle.</summary>
        public static readonly string[] AdFreeEntitlements = Commerce.AdFreeEntitlements;

        public static Dictionary<string, RewardedLimit> RewardedLimits() => GrillRewards.KitLimits();

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
                RewardedBudgetSingle = GrillRewards.UnitsSingleMax,
                RewardedBudgetDaily = GrillRewards.UnitsDailyMax,
                RevenueCatApiKey = RevenueCatAndroidKey,
                AdFreeEntitlements = AdFreeEntitlements,
                UiLanguage = DeviceLanguage,
                ReviewMinPlayS = 240.0,
            };
        }
    }
}
