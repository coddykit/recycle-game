// Semester Rush — batch-mode Android build (CI entry, jenkins/build_unity_android.sh):
//   Unity -batchmode -nographics -quit -projectPath semester-rush/unity -buildTarget Android \
//         -executeMethod SemesterRush.EditorTools.SemesterAndroidBuild.Run -logFile -
// Env (never printed): SEMESTERRUSH_VERSION_NAME, SEMESTERRUSH_VERSION_CODE (date +%s on the agent; missing = build-time seconds,
//   a release below 1.79e9 fails closed — ResolveVersionCode),
//   SEMESTERRUSH_KEYSTORE (path to upload.jks), SEMESTERRUSH_KEYSTORE_PASS, SEMESTERRUSH_KEY_ALIAS, SEMESTERRUSH_KEY_PASS,
//   SEMESTERRUSH_OUT (default build/android), SEMESTERRUSH_RELEASE=production|internal|dev,
//   SEMESTERRUSH_ADMOB_APP_ID (ADMOB_APP_ID_SEMESTERRUSH_ANDROID from store-automation/.env; required for internal/production — a
//   dev build without it uses Google's public sample app id), SEMESTERRUSH_APK=1 (dev only: an installable APK for the local
//   emulator smoke instead of the AAB).
// Steps: apply the project policy -> validate (fail closed) -> production gates (release ids present) -> BuildPlayer AAB
// with public debug symbols (Play "native debug symbols" upload in the SAME edit, games/CLAUDE.md §10 / LESSONS A12).
// Exit code 0 = AAB (or dev APK) written. Compiled + run on Unity 6000.3.27f1 (2026-10-28).
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CoddyKit.EditorTools;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using SemesterRush.App;

namespace SemesterRush.EditorTools
{
    public static class SemesterAndroidBuild
    {
        static string Env(string k, string def = "") => Environment.GetEnvironmentVariable(k) is string v && v != "" ? v : def;

        public static void Run()
        {
            int code = 1;
            try { code = Build(); }
            catch (Exception e) { Debug.LogError("[SemesterRush] BUILD_FAILED exception: " + e); }
            EditorApplication.Exit(code);
        }

        static int Build()
        {
            SemesterProjectSetup.Apply();
            // app icon: every slot set (owner's C3b), never Unity's default icon
            SemesterIcons.Apply();
            var iconProblems = SemesterIcons.Problems();
            if (iconProblems.Count > 0)
            {
                Debug.LogError("[SemesterRush] BUILD_FAILED icon slots empty (Unity default icon would ship):\n- " + string.Join("\n- ", iconProblems));
                return 8;
            }
            var problems = ProjectValidator.Problems(SemesterProjectSetup.Spec);
            if (problems.Count > 0)
            {
                Debug.LogError("[SemesterRush] BUILD_FAILED project invalid:\n- " + string.Join("\n- ", problems));
                return 2;
            }
            string release = Env("SEMESTERRUSH_RELEASE", "dev");
            if (release == "production" || release == "internal")
            {
                if (SemesterGameConfig.AdMobRewardedAndroid == "" || SemesterGameConfig.RevenueCatAndroidKey == "")
                {
                    Debug.LogError("[SemesterRush] BUILD_FAILED release ids empty (AdMob rewarded unit / RevenueCat key) — P4/P8 Patron step");
                    return 3;
                }
            }

            string versionName = Env("SEMESTERRUSH_VERSION_NAME", PlayerSettings.bundleVersion);
            int versionCode = ResolveVersionCode(Env("SEMESTERRUSH_VERSION_CODE"), release, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            if (versionCode <= 0)
            {
                Debug.LogError("[SemesterRush] BUILD_FAILED versionCode for a " + release + " build must be build-time seconds (>= "
                               + MinReleaseVersionCode + "), got SEMESTERRUSH_VERSION_CODE='" + Env("SEMESTERRUSH_VERSION_CODE") + "'");
                return 7;
            }
            PlayerSettings.Android.bundleVersionCode = versionCode;
            PlayerSettings.bundleVersion = versionName;

            string keystore = Env("SEMESTERRUSH_KEYSTORE");
            if (keystore != "")
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keystore;
                PlayerSettings.Android.keystorePass = Env("SEMESTERRUSH_KEYSTORE_PASS");
                PlayerSettings.Android.keyaliasName = Env("SEMESTERRUSH_KEY_ALIAS", "upload");
                PlayerSettings.Android.keyaliasPass = Env("SEMESTERRUSH_KEY_PASS");
            }
            else if (release != "dev")
            {
                Debug.LogError("[SemesterRush] BUILD_FAILED no signing keystore for a " + release + " build");
                return 4;
            }

            // AdMob app id (GMA's manifest step stops the build when it is empty). The real id comes from the CI env only and
            // is put back to the public sample after the build, so no real id is ever left in the committed settings asset.
            string appId = Env("SEMESTERRUSH_ADMOB_APP_ID");
            if (release != "dev" && (appId == "" || appId == GmaSampleAppId))
            {
                Debug.LogError("[SemesterRush] BUILD_FAILED SEMESTERRUSH_ADMOB_APP_ID missing for a " + release + " build — P8 Patron step");
                return 6;
            }
            bool gma = SetGmaAndroidAppId(appId == "" ? GmaSampleAppId : appId);

            bool apk = release == "dev" && Env("SEMESTERRUSH_APK") == "1";
            string outDir = Env("SEMESTERRUSH_OUT", "build/android");
            Directory.CreateDirectory(outDir);
            string aab = Path.Combine(outDir, apk ? "semester-rush.apk" : "semester-rush.aab");
            EditorUserBuildSettings.buildAppBundle = !apk;
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = aab,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = release == "dev" ? BuildOptions.Development : BuildOptions.None,
            };
            // Firebase config: semester-rush/config/google-services.json lives OUTSIDE the Unity tree (preflight's secret
            // gate); copied in for this build only, and everything Firebase's resolver generates from it is removed after.
            bool firebaseCfg = PlaceFirebaseConfig(release);
            BuildReport report;
            try { report = BuildPipeline.BuildPlayer(options); }
            finally { if (firebaseCfg) RemoveFirebaseConfig(); }
            // never leave signing secrets in the (restored-on-exit) ProjectSettings of the CI checkout
            PlayerSettings.Android.keystorePass = "";
            PlayerSettings.Android.keyaliasPass = "";
            if (gma && appId != "") SetGmaAndroidAppId(GmaSampleAppId);
            EditorUserBuildSettings.buildAppBundle = true;
            var s = report.summary;
            Debug.Log($"[SemesterRush] BUILD_{(s.result == BuildResult.Succeeded ? "OK" : "FAILED")} {s.result} size={s.totalSize} time={s.totalTime} out={aab} vc={PlayerSettings.Android.bundleVersionCode}");
            File.WriteAllText(Path.Combine(outDir, "version_code.txt"), PlayerSettings.Android.bundleVersionCode.ToString());
            return s.result == BuildResult.Succeeded ? 0 : 5;
        }

        /// <summary>Lowest versionCode a release (internal / production) may carry: the fleet's build-time seconds
        /// (`date +%s`, 1.79e9 in 2026-10). The committed ProjectSettings value is 1, so a build without the env var used to
        /// come out as versionCode 1 (S2's second dev build) — never again for a release.</summary>
        public const long MinReleaseVersionCode = 1_790_000_000;
        /// <summary>Play's ceiling for versionCode.</summary>
        public const long MaxVersionCode = 2_100_000_000;

        /// <summary>The versionCode for this build: SEMESTERRUSH_VERSION_CODE when it is a number, else the build time in Unix
        /// seconds (same scheme as every fleet AAB). Release builds fail closed (0) below <see cref="MinReleaseVersionCode"/>
        /// or above Play's ceiling; dev builds take any positive number.</summary>
        public static int ResolveVersionCode(string env, string release, long nowUnixSeconds)
        {
            long vc = long.TryParse(env ?? "", out long v) && v > 0 ? v : nowUnixSeconds;
            if (vc <= 0 || vc > MaxVersionCode) return 0;
            if (release != "dev" && vc < MinReleaseVersionCode) return 0;
            return (int)vc;
        }

        const string FirebaseSrc = "../config/google-services.json";
        const string FirebaseDst = "Assets/google-services.json";
        static readonly string[] FirebaseGenerated =
        {
            "Assets/google-services.json", "Assets/google-services.json.meta",
            "Assets/StreamingAssets/google-services-desktop.json", "Assets/StreamingAssets/google-services-desktop.json.meta",
        };
        const string FirebaseAndroidLib = "Assets/Plugins/Android/FirebaseApp.androidlib";

        /// <summary>True when the Firebase package is installed and the config was placed. A dev build without the config
        /// keeps the null analytics sink (the bridge checks google_app_id at runtime); internal / production fail closed.</summary>
        static bool PlaceFirebaseConfig(string release)
        {
            bool pkg = Type.GetType("Firebase.Editor.GenerateXmlFromGoogleServicesJson, Firebase.Editor") != null
                       || AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Firebase.Editor");
            if (!pkg) return false;
            if (!File.Exists(FirebaseSrc))
            {
                if (release != "dev") throw new Exception("[SemesterRush] BUILD_FAILED semester-rush/config/google-services.json missing for a " + release + " build");
                Debug.LogWarning("[SemesterRush] dev build without Firebase config: analytics stay a null sink");
                return false;
            }
            File.Copy(FirebaseSrc, FirebaseDst, true);
            AssetDatabase.ImportAsset(FirebaseDst, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[SemesterRush] Firebase config placed for the build");
            return true;
        }

        static void RemoveFirebaseConfig()
        {
            foreach (var f in FirebaseGenerated) if (File.Exists(f)) File.Delete(f);
            if (Directory.Exists(FirebaseAndroidLib)) Directory.Delete(FirebaseAndroidLib, true);
            if (File.Exists(FirebaseAndroidLib + ".meta")) File.Delete(FirebaseAndroidLib + ".meta");
            AssetDatabase.Refresh();
            Debug.Log("[SemesterRush] Firebase config removed after the build");
        }

        /// <summary>Google's documented sample AdMob app id (public test value, serves test ads only).</summary>
        public const string GmaSampleAppId = "ca-app-pub-3940256099942544~3347511713";

        /// <summary>Writes the Android app id into GMA's settings asset (internal editor class, reached by reflection so this
        /// assembly compiles without the package). False when the GMA package is not installed.</summary>
        public static bool SetGmaAndroidAppId(string id)
        {
            var t = Type.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings, GoogleMobileAds.Editor");
            var load = t?.GetMethod("LoadInstance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var settings = load?.Invoke(null, null) as UnityEngine.Object;
            var prop = t?.GetProperty("GoogleMobileAdsAndroidAppId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (settings == null || prop == null) return false;
            if ((prop.GetValue(settings) as string) != id)
            {
                prop.SetValue(settings, id);
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
            return true;
        }
    }
}
