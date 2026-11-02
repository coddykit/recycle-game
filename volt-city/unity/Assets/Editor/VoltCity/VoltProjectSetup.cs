// Volt City — editor setup: applies the CoddyKit fleet policy with Volt City's values, creates the URP tier assets, the
// kit PanelSettings and the Boot scene (camera + curtain UIDocument + VoltBoot), and registers the build scenes.
// Runs automatically ONCE on the first editor open (when Assets/Settings/URP-Medium.asset does not exist yet), from the
// menu "Coddy/Volt City/Apply Project Setup", and from CI before every build (VoltAndroidBuild).
// After the first open: commit ProjectSettings/*, Assets/Settings/*, Assets/Scenes/Boot.unity, the PanelSettings asset and
// ALL generated .meta files in one commit (docs/UNITY.md §3). UNCOMPILED until the Unity editor is installed.
using System.Collections.Generic;
using System.IO;
using CoddyKit.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using VoltCity.App;

namespace VoltCity.EditorTools
{
    [InitializeOnLoad]
    public static class VoltProjectSetup
    {
        public const string BootScene = "Assets/Scenes/Boot.unity";
        public const string CityScene = "Assets/Scenes/City.unity"; // P3
        public const string CurtainArt = "Assets/Art/Splash/curtain.png"; // P2

        public static readonly GameSpec Spec = new GameSpec
        {
            Company = "CoddyKit",
            Product = VoltGameConfig.ProductName,
            Package = VoltGameConfig.Package,
            Version = "0.1.0",
            SplashBackground = new Color(10 / 255f, 23 / 255f, 33 / 255f, 1f), // #0A1721 (P2 splash background)
        };

        static VoltProjectSetup()
        {
            ProjectSpecRegistry.Current = Spec;
            if (Application.isBatchMode) return; // CI calls Apply explicitly
            if (!File.Exists(Spec.SettingsDir + "/URP-Medium.asset"))
                EditorApplication.delayCall += () => Apply();
        }

        [MenuItem("Coddy/Volt City/Apply Project Setup")]
        public static void Apply()
        {
            ProjectSetup.Apply(Spec);
            VoltFonts.Apply(ProjectSetup.EnsurePanelSettings(Spec));
            EnsureBootScene();
            AssetDatabase.SaveAssets();
            var problems = ProjectValidator.Problems(Spec);
            if (problems.Count == 0) Debug.Log("[VoltCity] project setup applied: PROJECT_VALID");
            else Debug.LogWarning("[VoltCity] project setup applied, remaining problems:\n- " + string.Join("\n- ", problems));
        }

        public static void EnsureBootScene()
        {
            if (!File.Exists(BootScene))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(BootScene));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camGo = new GameObject("Main Camera");
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Spec.SplashBackground;
                cam.cullingMask = 0; // the boot draws only the UI curtain
                camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
                camGo.tag = "MainCamera";

                var bootGo = new GameObject("Boot");
                var doc = bootGo.AddComponent<UIDocument>();
                doc.panelSettings = ProjectSetup.EnsurePanelSettings(Spec);
                doc.sortingOrder = 80;
                var boot = bootGo.AddComponent<VoltBoot>();
                boot.panelSettings = doc.panelSettings;
                boot.curtainDocument = doc;
                boot.curtainArt = AssetDatabase.LoadAssetAtPath<Texture2D>(CurtainArt);
                EditorSceneManager.SaveScene(scene, BootScene);
            }
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(BootScene, true) };
            if (File.Exists(CityScene)) scenes.Add(new EditorBuildSettingsScene(CityScene, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
