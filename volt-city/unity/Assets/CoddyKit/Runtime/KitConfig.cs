// CoddyKit (Unity) — what a game tells the kit at boot (the Unity counterpart of project.godot [coddy_ads] + the
// Ads.* callables a Godot game sets). UNCOMPILED until the Unity editor is installed on the Mini.
using System;
using System.Collections.Generic;
using CoddyKit.Ads;

namespace CoddyKit
{
    public sealed class KitConfig
    {
        /// <summary>ads-config X-App-ID / house-ad host id (e.g. "volt-city"). Never promoted to itself.</summary>
        public string AppId = "";
        public string Platform = "android";
        /// <summary>Kids / Families game: no house banner, no house interstitial (games/CLAUDE.md §2).</summary>
        public bool Kids;

        /// <summary>Release AdMob rewarded unit (store-automation/.env ADMOB_REWARDED_&lt;SLUG&gt;_ANDROID). Empty = no rewarded
        /// video in release. Debug builds always use Google's test unit.</summary>
        public string RewardedUnitRelease = "";
        public Dictionary<string, RewardedLimit> RewardedLimits = new Dictionary<string, RewardedLimit>();
        public int RewardedBudgetSingle;
        public int RewardedBudgetDaily;

        /// <summary>RevenueCat public Android SDK key (goog_…). Empty = purchases off (fake store in the editor).</summary>
        public string RevenueCatApiKey = "";
        /// <summary>RC entitlements that remove forced ads (games/CLAUDE.md §3 / §8). Volt City: "noads".</summary>
        public string[] AdFreeEntitlements = new string[0];

        /// <summary>The game's UI language (2 letters) — the one source for every ad text.</summary>
        public Func<string> UiLanguage = () => "en";
        /// <summary>The game's own "Install" translation (null = kit table).</summary>
        public Func<string> InstallLabel = () => null;
        /// <summary>false during onboarding / guide, purchase & payment dialogs, full-screen modals, the shop.</summary>
        public Func<bool> BannerAllowed = () => true;
        /// <summary>true while a boot / splash / loading curtain is up (no banner over it).</summary>
        public Func<bool> Loading = () => false;
        /// <summary>The game's current graphics tier is Low (still hero frame, no pan / cross-fade).</summary>
        public Func<bool> LowEnd = () => false;
        /// <summary>Pause / resume the game around full-screen ads (true = pause).</summary>
        public Action<bool> PauseGame = _ => { };

        /// <summary>Review gate minimum play time (180-300 s per game, fleet default 240).</summary>
        public double ReviewMinPlayS = 240.0;
    }
}
