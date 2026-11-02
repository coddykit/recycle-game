// CoddyKit (Unity) — device safe area that also keeps the UI off the Android navigation bar (port of ui_safe_area.gd).
// Every Unity game uses SafeArea.Rect() / Edges() / Bottom() instead of Screen.safeArea. Why the engine value is not enough:
// the game runs fullscreen (PlayerSettings androidStartInFullscreen + renderOutsideSafeArea), the system bars are hidden,
// and Screen.safeArea then covers only the display cutout — but the navigation bar comes back OVER the game on an edge
// swipe (Samsung Game Booster keeps it up). So we reserve the navigation-bar insets IGNORING visibility
// (WindowInsets.getInsetsIgnoringVisibility(navigationBars), API 30+; stable insets on API 24-29) — left / right / bottom,
// never the top. Edge-anchored overlays (onboarding, coach, toasts, Skip, notices) use OverlayEdges(): + the status bar.
// Desktop frames / tests: env CODDY_SAFE_SIM="1080x2400:0,110,0,126" or SafeArea.Simulate(...).
// Cost: a few JNI calls at most once per RefreshS (cached), nothing per frame. UNCOMPILED until the editor is installed.
using System;
using UnityEngine;

namespace CoddyKit
{
    public static class SafeArea
    {
        public const float RefreshS = 1f;
        const int TypeStatusBars = 1;      // WindowInsets.Type.statusBars()
        const int TypeNavigationBars = 2;  // WindowInsets.Type.navigationBars()

        static Insets nav;
        static float status;
        static float readAt = -999f;
        static bool simRead;
        static int simW, simH;
        static Insets simInsets;
        /// <summary>Tests: forced navigation-bar insets (physical px); null = read the device.</summary>
        public static Insets? NavOverride;

        public static void Simulate(int screenW, int screenH, Insets insets)
        {
            simRead = true;
            simW = screenW;
            simH = screenH;
            simInsets = insets;
        }

        public static void StopSimulating() { simRead = true; simW = simH = 0; }

        public static bool Simulating
        {
            get
            {
                if (!simRead)
                {
                    simRead = true;
                    SafeAreaMath.ParseSimulation(Environment.GetEnvironmentVariable("CODDY_SAFE_SIM"), out simW, out simH, out simInsets);
                }
                return simW > 0 && simH > 0;
            }
        }

        public static int ScreenW => Simulating ? simW : Screen.width;
        public static int ScreenH => Simulating ? simH : Screen.height;

        /// <summary>Safe rect in physical px, TOP-LEFT origin, clear of cutout + navigation bar.</summary>
        public static IRect Rect()
        {
            if (Simulating)
                return new IRect((int)simInsets.Left, (int)simInsets.Top, simW - (int)(simInsets.Left + simInsets.Right), simH - (int)(simInsets.Top + simInsets.Bottom));
            Rect s = Screen.safeArea;
            IRect engine = SafeAreaMath.FromUnity(s.x, s.y, s.width, s.height, Screen.height);
            return SafeAreaMath.Combine(engine, Screen.width, Screen.height, NavInsets());
        }

        /// <summary>Gaps in panel px (left, top, right, bottom) for a panel of canvasW × canvasH covering the screen.</summary>
        public static Insets Edges(float canvasW, float canvasH) => SafeAreaMath.ToCanvas(Rect(), ScreenW, ScreenH, canvasW, canvasH);

        /// <summary>Edges + the status bar at the top (visible or not) — for edge-anchored overlays. Zero off a phone.</summary>
        public static Insets OverlayEdges(float canvasW, float canvasH)
        {
            if (!Simulating && !Application.isMobilePlatform) return Insets.Zero;
            return SafeAreaMath.OverlayEdges(Rect(), ScreenW, ScreenH, canvasW, canvasH, StatusTop());
        }

        /// <summary>What a bottom bar / the house banner keeps free under itself (panel px).</summary>
        public static float Bottom(float canvasW, float canvasH) => Edges(canvasW, canvasH).Bottom;

        public static float StatusTop()
        {
            if (Simulating) return simInsets.Top;
            if (Application.platform != RuntimePlatform.Android) return 0f;
            NavInsets();
            return status;
        }

        /// <summary>Forget the cache (rotation, navigation-mode change).</summary>
        public static void Refresh() { readAt = -999f; }

        public static Insets NavInsets()
        {
            if (NavOverride.HasValue) return NavOverride.Value;
            if (Application.platform != RuntimePlatform.Android) return Insets.Zero;
            float now = Time.realtimeSinceStartup;
            if (now - readAt < RefreshS) return nav;
            readAt = now;
            nav = ReadAndroid(nav);
            return nav;
        }

        static Insets ReadAndroid(Insets last)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    if (activity == null) return last;
                    using (var window = activity.Call<AndroidJavaObject>("getWindow"))
                    using (var decor = window?.Call<AndroidJavaObject>("getDecorView"))
                    using (var wi = decor?.Call<AndroidJavaObject>("getRootWindowInsets"))
                    {
                        if (wi == null) return last; // not attached yet: retry after RefreshS
                        int sdk;
                        using (var version = new AndroidJavaClass("android.os.Build$VERSION")) sdk = version.GetStatic<int>("SDK_INT");
                        if (sdk >= 30)
                        {
                            using (var navIns = wi.Call<AndroidJavaObject>("getInsetsIgnoringVisibility", TypeNavigationBars))
                            using (var stIns = wi.Call<AndroidJavaObject>("getInsetsIgnoringVisibility", TypeStatusBars))
                            {
                                if (stIns != null) status = SafeAreaMath.ParseAndroidInsets(stIns.Call<string>("toString")).Top;
                                if (navIns == null) return last;
                                var v = SafeAreaMath.ParseAndroidInsets(navIns.Call<string>("toString"));
                                return new Insets(v.Left, 0, v.Right, v.Bottom);
                            }
                        }
                        // API 24-29: stable insets = system bars as if shown (the top one is the status bar)
                        status = wi.Call<int>("getStableInsetTop");
                        return new Insets(wi.Call<int>("getStableInsetLeft"), 0, wi.Call<int>("getStableInsetRight"), wi.Call<int>("getStableInsetBottom"));
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CoddyKit] SafeArea JNI: " + e.Message);
                return last;
            }
#else
            return last;
#endif
        }

        /// <summary>Physical px per dp (Screen.dpi / 160; 2.75 when the OS reports nothing on a phone, 1 on desktop).</summary>
        public static float PxPerDp()
        {
            float dpi = Screen.dpi;
            if (dpi <= 0f) return Application.isMobilePlatform ? 2.75f : 1f;
            return dpi / 160f;
        }
    }
}
