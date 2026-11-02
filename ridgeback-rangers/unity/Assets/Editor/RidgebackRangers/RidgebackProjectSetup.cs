// Ridgeback Rangers — editor setup: applies the CoddyKit fleet policy with this game's values (ProjectSetup: Android IL2CPP
// ARM64, minSdk 25, targetSdk 36, Vulkan + GLES3, URP tier assets, no Unity splash), creates the palette materials, the
// Boot scene (camera + curtain UIDocument + RidgebackBoot) and the Region scene (camera rig, sun, RegionWorld, TapInput, UI
// document + RidgebackMain), and registers both build scenes. Runs once on the first editor open (when
// Assets/Settings/URP-Medium.asset or the Region scene is missing), from "Coddy/Ridgeback Rangers/Apply Project Setup" and from
// CI before every build (RidgebackAndroidBuild). After the first open commit ProjectSettings/*, Assets/Settings/*,
// Assets/Scenes/*, Assets/Resources/materials/*.mat, the PanelSettings asset and ALL generated .meta files in one commit.
using System.Collections.Generic;
using System.IO;
using CoddyKit;
using CoddyKit.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using RidgebackRangers.App;
using RidgebackRangers.UI;
using RidgebackRangers.World;

namespace RidgebackRangers.EditorTools
{
    [InitializeOnLoad]
    public static class RidgebackProjectSetup
    {
        public const string BootScene = "Assets/Scenes/Boot.unity";
        public const string RegionScene = "Assets/Scenes/Region.unity";
        public const string PaletteTex = "Assets/Resources/materials/palette.png";
        public const string PaletteMat = "Assets/Resources/materials/palette.mat";
        public const string PaletteLowMat = "Assets/Resources/materials/palette_low.mat";
        public const string CurtainArt = "Assets/Resources/ui/curtain.png";

        public static readonly GameSpec Spec = new GameSpec
        {
            Company = "CoddyKit",
            Product = RidgebackGameConfig.ProductName,
            Package = RidgebackGameConfig.Package,
            Version = "0.1.0",
            SplashBackground = new Color(0.059f, 0.231f, 0.180f, 1f), // #0F3B2E
        };

        static RidgebackProjectSetup()
        {
            ProjectSpecRegistry.Current = Spec;
            if (Application.isBatchMode) return;
            if (!File.Exists(Spec.SettingsDir + "/URP-Medium.asset") || !File.Exists(RegionScene))
                EditorApplication.delayCall += () => Apply();
        }

        [MenuItem("Coddy/Ridgeback Rangers/Apply Project Setup")]
        public static void Apply()
        {
            ProjectSetup.Apply(Spec);
            RidgebackFonts.Apply(ProjectSetup.EnsurePanelSettings(Spec));
            EnsureMaterials();
            EnsureBootScene();
            EnsureRegionScene();
            RegisterScenes();
            AssetDatabase.SaveAssets();
            var problems = ProjectValidator.Problems(Spec);
            if (problems.Count == 0) Debug.Log("[RidgebackRangers] project setup applied: PROJECT_VALID");
            else Debug.LogWarning("[RidgebackRangers] project setup applied, remaining problems:\n- " + string.Join("\n- ", problems));
        }

        /// <summary>Medium/High: URP Simple Lit (Blinn-Phong, cheap); Low: the same shader, no specular, no shadows received.</summary>
        public static void EnsureMaterials()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PaletteTex);
            var shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            void Make(string path, bool low)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
                m.shader = shader;
                m.SetTexture("_BaseMap", tex);
                m.mainTexture = tex;
                m.SetColor("_BaseColor", Color.white);
                m.enableInstancing = true;
                m.SetFloat("_SpecularHighlights", 0f);
                if (m.HasProperty("_ReceiveShadows")) m.SetFloat("_ReceiveShadows", low ? 0f : 1f);
                if (low) m.EnableKeyword("_RECEIVE_SHADOWS_OFF"); else m.DisableKeyword("_RECEIVE_SHADOWS_OFF");
                m.DisableKeyword("_SPECULAR_COLOR");
                EditorUtility.SetDirty(m);
            }
            Make(PaletteMat, false);
            Make(PaletteLowMat, true);
        }

        static GameObject Cam(Color bg, bool ui)
        {
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bg;
            if (ui) cam.cullingMask = 0;
            camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            camGo.tag = "MainCamera";
            return camGo;
        }

        public static void EnsureBootScene()
        {
            if (File.Exists(BootScene)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(BootScene));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Cam(Spec.SplashBackground, true);
            var bootGo = new GameObject("Boot");
            var doc = bootGo.AddComponent<UIDocument>();
            doc.panelSettings = ProjectSetup.EnsurePanelSettings(Spec);
            doc.sortingOrder = 80;
            var boot = bootGo.AddComponent<RidgebackBoot>();
            boot.panelSettings = doc.panelSettings;
            boot.curtainDocument = doc;
            boot.curtainArt = AssetDatabase.LoadAssetAtPath<Texture2D>(CurtainArt);
            EditorSceneManager.SaveScene(scene, BootScene);
        }

        public static void EnsureRegionScene()
        {
            if (File.Exists(RegionScene)) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = Cam(new Color(0.55f, 0.78f, 0.86f), false);
            var cam = camGo.GetComponent<Camera>();
            cam.fieldOfView = CameraRig.Fov;
            var rig = camGo.AddComponent<CameraRig>();
            rig.cam = cam;
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.66f, 0.70f);

            var worldGo = new GameObject("World");
            var world = worldGo.AddComponent<RegionWorld>();
            world.cam = cam;
            world.rig = rig;
            world.sun = sun;
            world.tap = worldGo.AddComponent<TapInput>();

            var uiGo = new GameObject("UI");
            var doc = uiGo.AddComponent<UIDocument>();
            doc.panelSettings = ProjectSetup.EnsurePanelSettings(Spec);
            doc.sortingOrder = 10;
            var main = uiGo.AddComponent<RidgebackMain>();
            main.uiDocument = doc;
            main.world = world;
            EditorSceneManager.SaveScene(scene, RegionScene);
        }

        public static void RegisterScenes()
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(BootScene, true) };
            if (File.Exists(RegionScene)) scenes.Add(new EditorBuildSettingsScene(RegionScene, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
