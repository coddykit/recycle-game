// CoddyKit (Unity) — the fleet's Android player / quality policy, applied through the editor API (the committed
// ProjectSettings YAML carries the same values for the first open; this script is the source of truth and the build
// re-applies it, ProjectValidator checks it). Generic: a game passes its GameSpec (VoltCity.Editor.VoltProjectSetup).
//   * Android only, IL2CPP, ARM64 only, minSdk 25 (Unity 6000.3 floor), targetSdk 36 (Play requires API 36 for new apps, 2026), portrait only (GameSpec.Landscape = landscape only), AAB.
//   * Graphics APIs: Vulkan first, OpenGL ES 3 fallback (Unity picks the first the device supports). Fleet lesson A11
//     (Godot Vulkan "device lost" on PowerVR) + Vitals 10-27 (IMG BXM GLES shader-compiler crash): PowerVR / IMG must be
//     put on the Vulkan deny list (Player Settings > Android > Vulkan Settings > Device Filter Lists) — see docs/UNITY.md
//     §5; the GPU breadcrumb (QualityTiers) covers the GLES side.
//   * Linear colour, no HDR on mobile, SRP batcher on, no Unity splash / logo (allowed on every licence since Unity 6),
//     own splash background #0A1721, fullscreen + render outside the safe area (the kit pads by SafeArea).
//   * Managed stripping Medium + engine code stripping + IL2CPP "optimize size" (APK size risk, docs/UNITY.md §6).
//   * Three quality levels Low / Medium / High, each with its own URP asset (games/CLAUDE.md §10 / TierSpec):
//       Low    render scale 0.85, MSAA off, no shadows, no HDR, no depth / opaque texture
//       Medium 1.0, MSAA 2x, main-light shadows 1 cascade, 1024 map
//       High   1.0, MSAA 4x, soft main-light shadows, 2048 map
// UNCOMPILED until the Unity editor is installed.
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace CoddyKit.EditorTools
{
    public sealed class GameSpec
    {
        public string Company = "CoddyKit";
        public string Product = "";
        public string Package = "";
        public string Version = "0.1.0";
        public Color SplashBackground = new Color(0.039f, 0.090f, 0.129f, 1f);
        public string SettingsDir = "Assets/Settings";
        public string PanelSettingsPath = "Assets/CoddyKit/UI/Resources/CoddyPanelSettings.asset";
        public string ThemePath = "Assets/CoddyKit/UI/CoddyRuntimeTheme.tss";
        /// <summary>false = portrait only (default, every game so far); true = landscape only (left + right).</summary>
        public bool Landscape = false;
        /// <summary>Panel reference: the SHORT side is always 720 px (fleet 1 dp = 1.75 px baseline). Landscape: 1280 x 720.</summary>
        public Vector2Int ReferenceResolution = new Vector2Int(720, 1280);
        public Vector2Int PanelReference => Landscape ? new Vector2Int(Mathf.Max(ReferenceResolution.x, ReferenceResolution.y), Mathf.Min(ReferenceResolution.x, ReferenceResolution.y)) : ReferenceResolution;
        public int MinSdk = 25; // Unity 6000.3: AndroidApiLevel25 is the lowest the editor accepts (24 was dropped)
        public int TargetSdk = 36; // Play refused 35 for the first upload ("Target SDK of artifact is too low", 2026-10-28); fleet Godot games are 36
    }

    public static class ProjectSetup
    {
        public static readonly string[] TierNames = { "Low", "Medium", "High" };

        /// <summary>Everything, idempotent. Returns the URP assets (Low, Medium, High).</summary>
        public static UniversalRenderPipelineAsset[] Apply(GameSpec spec)
        {
            ApplyPlayer(spec);
            var assets = ApplyQuality(spec);
            EnsurePanelSettings(spec);
            SetAdvancedText(true);
            AssetDatabase.SaveAssets();
            return assets;
        }

        // UI Toolkit's Advanced Text Generator (HarfBuzz/ICU: Hindi shaping, docs/UNITY.md section 4). The kit USS asks for
        // it (-unity-text-generator: advanced); with the project switch off Unity 6000.3 logs an error on every panel
        // update. The switch lives in ProjectSettings/UIToolkitProjectSettings.asset behind an internal editor class.
        static readonly Type UitkSettings = Type.GetType("UnityEditor.UIElements.UIToolkitProjectSettings, UnityEditor.UIElementsModule");

        public static bool? AdvancedTextEnabled()
        {
            var prop = UitkSettings?.GetProperty("enableAdvancedText", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            return prop?.GetValue(null) as bool?;
        }

        public static void SetAdvancedText(bool on)
        {
            var prop = UitkSettings?.GetProperty("enableAdvancedText", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop == null || !prop.CanWrite) { Debug.LogWarning("[CoddyKit] UIToolkitProjectSettings.enableAdvancedText not found"); return; }
            if (prop.GetValue(null) is bool cur && cur == on) return;
            prop.SetValue(null, on);
        }

        public static void ApplyPlayer(GameSpec spec)
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.companyName = spec.Company;
            PlayerSettings.productName = spec.Product;
            PlayerSettings.bundleVersion = string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "1.0" ? spec.Version : PlayerSettings.bundleVersion;
            PlayerSettings.SetApplicationIdentifier(android, spec.Package);
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCompilerConfiguration(android, Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetIl2CppCodeGeneration(android, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Medium);
            PlayerSettings.SetApiCompatibilityLevel(android, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)spec.MinSdk;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)spec.TargetSdk;
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            PlayerSettings.Android.optimizedFramePacing = true;
            PlayerSettings.Android.minifyRelease = true;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.GameActivity;

            if (spec.Landscape)
            {
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
                PlayerSettings.allowedAutorotateToPortrait = false;
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
                PlayerSettings.allowedAutorotateToLandscapeLeft = true;
                PlayerSettings.allowedAutorotateToLandscapeRight = true;
            }
            else
            {
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
                PlayerSettings.allowedAutorotateToPortrait = true;
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
                PlayerSettings.allowedAutorotateToLandscapeLeft = false;
                PlayerSettings.allowedAutorotateToLandscapeRight = false;
            }

            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.gpuSkinning = true;
            PlayerSettings.SetMobileMTRendering(android, true);

            // P2 hand-over: UV0 / UV1 carry data the shaders read at full precision
            // (Unity 6000.3: PlayerSettings.vertexChannelCompressionMask is no longer public -> serialized PlayerSettings field)
            var vccPs = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
            var vcc = vccPs.FindProperty("VertexChannelCompressionMask");
            if (vcc != null)
            {
                vcc.intValue &= ~(int)(VertexChannelCompressionFlags.TexCoord0 | VertexChannelCompressionFlags.TexCoord1);
                vccPs.ApplyModifiedPropertiesWithoutUndo();
            }
            else Debug.LogWarning("[CoddyKit] PlayerSettings.VertexChannelCompressionMask not found");

            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.backgroundColor = spec.SplashBackground;

            EditorUserBuildSettings.buildAppBundle = true;
#pragma warning disable 618
            EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Public;
#pragma warning restore 618

            // Active Input Handling = Input System package only (1). Not public API: the serialized PlayerSettings field.
            var ps = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
            var input = ps.FindProperty("activeInputHandler");
            if (input != null && input.intValue != 1)
            {
                input.intValue = 1;
                ps.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        public static UniversalRenderPipelineAsset[] ApplyQuality(GameSpec spec)
        {
            Directory.CreateDirectory(spec.SettingsDir);
            string rendererPath = spec.SettingsDir + "/URP-Renderer.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }
            renderer.renderingMode = UnityEngine.Rendering.Universal.RenderingMode.Forward;
            renderer.postProcessData = null; // no post on any tier in P0 (extra effects are Medium+/High-only P3 work)
            EditorUtility.SetDirty(renderer);

            var assets = new UniversalRenderPipelineAsset[3];
            for (int i = 0; i < 3; i++)
            {
                var tier = TierSpec.For((Tier)i);
                string path = $"{spec.SettingsDir}/URP-{TierNames[i]}.asset";
                var a = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (a == null)
                {
                    a = UniversalRenderPipelineAsset.Create(renderer);
                    AssetDatabase.CreateAsset(a, path);
                }
                a.renderScale = tier.RenderScale;
                a.msaaSampleCount = tier.Msaa;
                a.supportsHDR = false;
                a.useSRPBatcher = true;
                a.supportsCameraDepthTexture = false;
                a.supportsCameraOpaqueTexture = false;
                a.shadowDistance = tier.MainLightShadows ? tier.ShadowDistance : 0f;
                a.shadowCascadeCount = 1;
                var so = new SerializedObject(a);
                SetBool(so, "m_MainLightShadowsSupported", tier.MainLightShadows);
                SetBool(so, "m_SoftShadowsSupported", tier.SoftShadows);
                SetBool(so, "m_AdditionalLightShadowsSupported", false);
                SetInt(so, "m_AdditionalLightsRenderingMode", 0); // disabled: the city is lit by sun + emissive windows
                SetInt(so, "m_MainLightShadowmapResolution", i == 2 ? 2048 : 1024);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(a);
                assets[i] = a;
            }

            // quality levels: exactly Low / Medium / High with their URP asset
            var qs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = qs.FindProperty("m_QualitySettings");
            // a fresh Unity 6 project starts with 6 levels (Very Low .. Ultra): keep the first three and rename them
            // (U0 2026-11-02; Volt City's committed QualitySettings.asset already had 3)
            if (levels != null && levels.arraySize > 3)
            {
                while (levels.arraySize > 3) levels.DeleteArrayElementAtIndex(levels.arraySize - 1);
                var cur = qs.FindProperty("m_CurrentQuality");
                if (cur != null && cur.intValue > 2) cur.intValue = 1;
                qs.ApplyModifiedPropertiesWithoutUndo();
                qs.Update();
            }
            if (levels != null && levels.arraySize == 3)
            {
                for (int i = 0; i < 3; i++)
                {
                    var lv = levels.GetArrayElementAtIndex(i);
                    lv.FindPropertyRelative("name").stringValue = TierNames[i];
                    lv.FindPropertyRelative("customRenderPipeline").objectReferenceValue = assets[i];
                    lv.FindPropertyRelative("vSyncCount").intValue = 0;
                    var mip = lv.FindPropertyRelative("globalTextureMipmapLimit");
                    if (mip != null) mip.intValue = TierSpec.For((Tier)i).TextureMipLimit;
                }
                qs.ApplyModifiedPropertiesWithoutUndo();
            }
            else Debug.LogError("[CoddyKit] QualitySettings must have exactly 3 levels (Low, Medium, High) — fix ProjectSettings/QualitySettings.asset");
            GraphicsSettings.defaultRenderPipeline = assets[1];
            return assets;
        }

        public static PanelSettings EnsurePanelSettings(GameSpec spec)
        {
            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(spec.PanelSettingsPath);
            if (ps == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(spec.PanelSettingsPath));
                ps = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(ps, spec.PanelSettingsPath);
            }
            ps.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(spec.ThemePath);
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = spec.PanelReference;
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = spec.Landscape ? 1f : 0f; // the short side is the 720 px reference (fleet 1 dp = 1.75 px baseline)
            ps.clearColor = false;
            EditorUtility.SetDirty(ps);
            return ps;
        }

        static void SetBool(SerializedObject so, string name, bool v)
        {
            var p = so.FindProperty(name);
            if (p == null) Debug.LogError($"[CoddyKit] URP field {name} not found (URP version changed?)");
            else p.boolValue = v;
        }

        static void SetInt(SerializedObject so, string name, int v)
        {
            var p = so.FindProperty(name);
            if (p == null) Debug.LogError($"[CoddyKit] URP field {name} not found (URP version changed?)");
            else p.intValue = v;
        }
    }
}
