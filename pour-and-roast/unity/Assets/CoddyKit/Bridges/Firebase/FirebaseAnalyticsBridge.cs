// CoddyKit (Unity) — Firebase Analytics bridge (package com.google.firebase.analytics >= 12, Google's tarball; asmdef
// versionDefines -> CODDY_FIREBASE). Replaces the null analytics sink with FirebaseAnalytics.LogEvent on an Android
// device — but ONLY when the build carries a Firebase config (the google_app_id string resource that the Firebase
// Android resolver generates from google-services.json). The config is never committed into the Unity tree: the build
// copies pour-and-roast/config/google-services.json (not committed) into Assets for the build and removes it afterwards
// (BottleAndroidBuild). A build without it keeps the null sink — never a crash, never an invented id.
// Event names / params: the kit's (rewarded_*, onboarding_*, save_unreadable, gpu_shader_crash, vc_*); values are
// mapped to long / double / string (Firebase's three types), names cut to 40 chars, string values to 100.
#if CODDY_FIREBASE
using System;
using System.Collections.Generic;
using Firebase.Analytics;
using UnityEngine;

namespace CoddyKit.Bridges
{
    public static class FirebaseBridgeRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!HasConfig()) { CoddyServices.Registered.Add("Firebase(no config)"); return; }
            var sink = new FirebaseAnalyticsSink();
            CoddyServices.Analytics = new ConsentGatedAnalytics(sink);   // no event before consent mode is applied
            // consent mode v2: denied until UMP answers (the manifest defaults say the same before this code runs)
            CoddyServices.ConsentModeSink = SetConsent;
            SetConsent(CoddyServices.LastConsentMode);
            CoddyServices.Registered.Add("Firebase");
            Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(t =>
            {
                sink.Ready = t.Result == Firebase.DependencyStatus.Available;
            });
#endif
        }

        static void SetConsent(ConsentSignals s)
        {
            try
            {
                FirebaseAnalytics.SetConsent(new Dictionary<ConsentType, ConsentStatus>
                {
                    { ConsentType.AnalyticsStorage, s.AnalyticsStorage ? ConsentStatus.Granted : ConsentStatus.Denied },
                    { ConsentType.AdStorage, s.AdStorage ? ConsentStatus.Granted : ConsentStatus.Denied },
                    { ConsentType.AdUserData, s.AdUserData ? ConsentStatus.Granted : ConsentStatus.Denied },
                    { ConsentType.AdPersonalization, s.AdPersonalization ? ConsentStatus.Granted : ConsentStatus.Denied },
                });
            }
            catch (Exception e) { Debug.LogWarning("[CoddyKit] firebase consent: " + e.Message); }
        }

        static bool HasConfig()
        {
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var res = activity.Call<AndroidJavaObject>("getResources");
                string pkg = activity.Call<string>("getPackageName");
                return res.Call<int>("getIdentifier", "google_app_id", "string", pkg) != 0;
            }
            catch (Exception) { return false; }
        }
    }

    public sealed class FirebaseAnalyticsSink : IAnalytics
    {
        public volatile bool Ready;
        readonly Queue<(string, Dictionary<string, object>)> early = new Queue<(string, Dictionary<string, object>)>();

        public void Event(string name, Dictionary<string, object> parameters)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (!Ready)
            {
                if (early.Count < 64) early.Enqueue((name, parameters));   // events before dependency check finished
                return;
            }
            while (early.Count > 0) { var e = early.Dequeue(); Log(e.Item1, e.Item2); }
            Log(name, parameters);
        }

        static void Log(string name, Dictionary<string, object> p)
        {
            try
            {
                string n = name.Length > 40 ? name.Substring(0, 40) : name;
                if (p == null || p.Count == 0) { FirebaseAnalytics.LogEvent(n); return; }
                var list = new List<Parameter>(p.Count);
                foreach (var kv in p)
                {
                    string k = kv.Key.Length > 40 ? kv.Key.Substring(0, 40) : kv.Key;
                    switch (kv.Value)
                    {
                        case long l: list.Add(new Parameter(k, l)); break;
                        case int i: list.Add(new Parameter(k, (long)i)); break;
                        case bool b: list.Add(new Parameter(k, b ? 1L : 0L)); break;
                        case double d: list.Add(new Parameter(k, d)); break;
                        case float f: list.Add(new Parameter(k, (double)f)); break;
                        default:
                            string s = kv.Value?.ToString() ?? "";
                            list.Add(new Parameter(k, s.Length > 100 ? s.Substring(0, 100) : s));
                            break;
                    }
                }
                FirebaseAnalytics.LogEvent(n, list.ToArray());
            }
            catch (Exception e) { Debug.LogWarning("[CoddyKit] firebase event " + name + ": " + e.Message); }
        }
    }
}
#endif
