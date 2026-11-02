// CoddyKit (Unity) — Google Mobile Ads bridge: REWARDED ONLY + UMP consent (games/CLAUDE.md §2/§4/§5, owner 2026-10-25:
// no AdMob interstitial / banner exists in the kit). Compiles only with the package com.google.ads.mobile >= 9.0.0
// (asmdef versionDefines -> CODDY_ADMOB). Real provider on an Android device; the editor keeps the fakes.
// Rules kept here: MobileAds.Initialize is called by RewardedController only (via Initialize) and nothing loads before its
// callback; consent (UMP) is gathered before Initialize; callbacks are raised on the Unity main thread; RC ad tracking
// events: loaded / displayed (impression) / opened (click) / revenue (OnAdPaid micros + currency + precision unchanged)
// / failed_to_load. max_ad_content_rating T (13+ audience, not Designed for Families).
// Compiled against GMA 11.6.0 on Unity 6000.3.27f1 (2026-10-28).
#if CODDY_ADMOB
using System;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace CoddyKit.Bridges
{
    public static class AdMobBridgeRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            CoddyServices.Consent = new UmpConsentProvider();
            CoddyServices.Rewarded = new GmaRewardedProvider();
            CoddyServices.Registered.Add("AdMob+UMP");
#endif
        }
    }

    public sealed class UmpConsentProvider : IConsentProvider
    {
        // UMP raises its callbacks on a Java thread (seen on device 2026-10-28: MobileAds.SetRequestConfiguration from the
        // Gather callback threw "get_isPlaying can only be called from the main thread"). Constructed in Register
        // (BeforeSceneLoad, main thread), so this is Unity's main-thread context; every callback is posted back to it.
        readonly System.Threading.SynchronizationContext main = System.Threading.SynchronizationContext.Current;

        void OnMain(Action a)
        {
            if (main == null || main == System.Threading.SynchronizationContext.Current) a();
            else main.Post(_ => a(), null);
        }

        public void Gather(Action<bool, string> done)
        {
            var inner = done;
            done = (can, err) => OnMain(() => { PushConsentMode(); inner?.Invoke(can, err); });
            var request = new ConsentRequestParameters { TagForUnderAgeOfConsent = false };
            ConsentInformation.Update(request, updateError =>
            {
                if (updateError != null)
                {
                    // offline / misconfigured: a previous session's consent may still allow ads
                    done?.Invoke(ConsentInformation.CanRequestAds(), updateError.Message);
                    return;
                }
                ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
                {
                    done?.Invoke(ConsentInformation.CanRequestAds(), formError?.Message ?? "");
                });
            });
        }

        public bool CanRequestAds => ConsentInformation.CanRequestAds();

        public bool PrivacyOptionsRequired =>
            ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

        public void ShowPrivacyOptions(Action<string> done)
        {
            ConsentForm.ShowPrivacyOptionsForm(err => OnMain(() => { PushConsentMode(); done?.Invoke(err?.Message ?? ""); }));
        }

        /// <summary>Consent mode v2 from what UMP stored (IAB TCF v2 keys in the default SharedPreferences). Unknown
        /// consent status = UMP has not answered = everything stays denied.</summary>
        static void PushConsentMode()
        {
            bool answered = ConsentInformation.ConsentStatus == ConsentStatus.Obtained || ConsentInformation.ConsentStatus == ConsentStatus.NotRequired;
            int gdpr = -1;
            string purposes = "";
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var pm = new AndroidJavaClass("android.preference.PreferenceManager");
                using var prefs = pm.CallStatic<AndroidJavaObject>("getDefaultSharedPreferences", activity);
                gdpr = prefs.Call<int>("getInt", "IABTCF_gdprApplies", -1);
                purposes = prefs.Call<string>("getString", "IABTCF_PurposeConsents", "") ?? "";
            }
            catch (Exception e) { Debug.LogWarning("[CoddyKit] TCF read: " + e.Message); answered = false; }
#endif
            var signals = ConsentMode.FromTcf(answered, gdpr, purposes);
            Debug.Log("[CoddyKit] consent mode " + signals);
            CoddyServices.ApplyConsentMode(signals);
        }
    }

    public sealed class GmaRewardedProvider : IRewardedProvider
    {
        RewardedAd ad;
        string adId = "";
        string unit = "";
        string placement = "";
        int loads;
        public bool Initialized { get; private set; }
        public bool IsLoaded => ad != null && ad.CanShowAd();
        public event Action<AdTrackEvent> Tracked;

        public void Initialize(Action done)
        {
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            MobileAds.SetRequestConfiguration(new RequestConfiguration { MaxAdContentRating = MaxAdContentRating.T });
            MobileAds.Initialize(_ =>
            {
                Initialized = true;
                done?.Invoke();
            });
        }

        public void Load(string unitId, Action<bool, string> done)
        {
            if (!Initialized) { done?.Invoke(false, "not_initialized"); return; } // never load before init (crash rule)
            unit = unitId;
            RewardedAd.Load(unitId, new AdRequest(), (loaded, error) =>
            {
                if (error != null || loaded == null)
                {
                    Tracked?.Invoke(new AdTrackEvent { Kind = "failed_to_load", Format = "rewarded", Unit = unitId, Error = error != null ? error.GetCode().ToString() : "null_ad" });
                    done?.Invoke(false, error?.GetMessage() ?? "null_ad");
                    return;
                }
                ad?.Destroy();
                ad = loaded;
                adId = unitId + "#" + (++loads);
                string id = adId;
                ad.OnAdImpressionRecorded += () => Tracked?.Invoke(new AdTrackEvent { Kind = "displayed", AdId = id, Format = "rewarded", Unit = unit, Placement = placement });
                ad.OnAdClicked += () => Tracked?.Invoke(new AdTrackEvent { Kind = "opened", AdId = id, Format = "rewarded", Unit = unit, Placement = placement });
                ad.OnAdPaid += v => Tracked?.Invoke(new AdTrackEvent
                {
                    Kind = "revenue", AdId = id, Format = "rewarded", Unit = unit, Placement = placement,
                    ValueMicros = v.Value, Currency = v.CurrencyCode, Precision = (int)v.Precision,
                });
                Tracked?.Invoke(new AdTrackEvent { Kind = "loaded", AdId = id, Format = "rewarded", Unit = unitId });
                done?.Invoke(true, "");
            });
        }

        public void Show(string placementName, Action onEarned, Action onClosed, Action<string> onFailed)
        {
            if (!IsLoaded) { onFailed?.Invoke("not_loaded"); return; }
            placement = placementName ?? "";
            var shown = ad;
            ad = null;
            shown.OnAdFullScreenContentClosed += () =>
            {
                shown.Destroy();
                onClosed?.Invoke();
            };
            shown.OnAdFullScreenContentFailed += e =>
            {
                shown.Destroy();
                onFailed?.Invoke(e?.GetMessage() ?? "show_failed");
            };
            shown.Show(_ => onEarned?.Invoke());
        }
    }
}
#endif
