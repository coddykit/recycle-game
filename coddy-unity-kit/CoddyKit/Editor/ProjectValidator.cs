// CoddyKit (Unity) — reads the project back and lists every deviation from the fleet policy (ProjectSetup header).
// Used by the EditMode test (VoltCity.App.Tests), the CI build (fails closed) and the menu "Coddy/Validate Project".
// UNCOMPILED until the Unity editor is installed.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace CoddyKit.EditorTools
{
    public static class ProjectValidator
    {
        public static List<string> Problems(GameSpec spec)
        {
            var p = new List<string>();
            var android = NamedBuildTarget.Android;
            void Check(bool ok, string msg) { if (!ok) p.Add(msg); }

            Check(PlayerSettings.GetApplicationIdentifier(android) == spec.Package, "package must be " + spec.Package);
            Check(PlayerSettings.GetScriptingBackend(android) == ScriptingImplementation.IL2CPP, "Android scripting backend must be IL2CPP");
            Check(PlayerSettings.Android.targetArchitectures == AndroidArchitecture.ARM64, "Android architectures must be ARM64 only");
            Check((int)PlayerSettings.Android.minSdkVersion == spec.MinSdk, "minSdk must be " + spec.MinSdk);
            Check((int)PlayerSettings.Android.targetSdkVersion >= spec.TargetSdk, "targetSdk must be >= " + spec.TargetSdk);
            if (spec.Landscape)
            {
                Check(PlayerSettings.defaultInterfaceOrientation == UIOrientation.AutoRotation || PlayerSettings.defaultInterfaceOrientation == UIOrientation.LandscapeLeft, "orientation must be landscape");
                Check(!PlayerSettings.allowedAutorotateToPortrait && !PlayerSettings.allowedAutorotateToPortraitUpsideDown, "portrait must be off");
            }
            else
            {
                Check(PlayerSettings.defaultInterfaceOrientation == UIOrientation.Portrait, "orientation must be Portrait");
                Check(!PlayerSettings.allowedAutorotateToLandscapeLeft && !PlayerSettings.allowedAutorotateToLandscapeRight, "landscape must be off");
            }
            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            Check(!PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android) && apis.Length == 2 && apis[0] == GraphicsDeviceType.Vulkan && apis[1] == GraphicsDeviceType.OpenGLES3,
                "graphics APIs must be [Vulkan, OpenGLES3] (explicit)");
            Check(PlayerSettings.colorSpace == ColorSpace.Linear, "colour space must be Linear (P2 vertex colours)");
            Check(!PlayerSettings.SplashScreen.show && !PlayerSettings.SplashScreen.showUnityLogo, "Unity splash / logo must be off (own splash)");
            Check(EditorUserBuildSettings.buildAppBundle, "Android build must produce an AAB");
            var ps = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
            var vcc = ps.FindProperty("VertexChannelCompressionMask"); // Unity 6000.3: no public PlayerSettings API any more
            Check(vcc != null && (vcc.intValue & (int)(VertexChannelCompressionFlags.TexCoord0 | VertexChannelCompressionFlags.TexCoord1)) == 0,
                "vertex compression must be off for TexCoord0 / TexCoord1 (P2 data UVs)");
            var input = ps.FindProperty("activeInputHandler");
            Check(input != null && input.intValue == 1, "Active Input Handling must be 'Input System Package (New)'");
            Check(ProjectSetup.AdvancedTextEnabled() == true, "UI Toolkit Advanced Text Generator must be on (Hindi shaping; kit USS uses it)");

            // quality tiers
            Check(QualitySettings.names.SequenceEqual(ProjectSetup.TierNames), "quality levels must be exactly Low, Medium, High");
            for (int i = 0; i < QualitySettings.count && i < 3; i++)
            {
                var a = QualitySettings.GetRenderPipelineAssetAt(i) as UniversalRenderPipelineAsset;
                var t = TierSpec.For((Tier)i);
                if (a == null) { p.Add($"quality level {i} has no URP asset"); continue; }
                Check(Mathf.Approximately(a.renderScale, t.RenderScale), $"{ProjectSetup.TierNames[i]}: render scale {a.renderScale} != {t.RenderScale}");
                Check(a.msaaSampleCount == t.Msaa, $"{ProjectSetup.TierNames[i]}: MSAA {a.msaaSampleCount} != {t.Msaa}");
                Check(a.supportsMainLightShadows == t.MainLightShadows, $"{ProjectSetup.TierNames[i]}: main light shadows must be {t.MainLightShadows}");
                Check(!a.supportsHDR, $"{ProjectSetup.TierNames[i]}: HDR must be off on mobile");
                Check(a.useSRPBatcher, $"{ProjectSetup.TierNames[i]}: SRP batcher must be on");
                Check(TierSpec.Validate(t) == "", $"{ProjectSetup.TierNames[i]}: tier table invalid: {TierSpec.Validate(t)}");
            }
            Check(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset, "default render pipeline must be URP");

            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(spec.PanelSettingsPath);
            Check(panel != null && panel.themeStyleSheet != null, "kit PanelSettings with the Coddy runtime theme missing (" + spec.PanelSettingsPath + ")");
            return p;
        }

        [MenuItem("Coddy/Validate Project")]
        static void Menu()
        {
            var spec = ProjectSpecRegistry.Current;
            if (spec == null) { Debug.LogError("[CoddyKit] no GameSpec registered (ProjectSpecRegistry.Current)"); return; }
            var p = Problems(spec);
            if (p.Count == 0) Debug.Log("[CoddyKit] PROJECT_VALID: 0 problems");
            else Debug.LogError("[CoddyKit] PROJECT_INVALID:\n- " + string.Join("\n- ", p));
        }
    }

    /// <summary>The game registers its spec ([InitializeOnLoad] in its editor assembly) so kit menus can find it.</summary>
    public static class ProjectSpecRegistry
    {
        public static GameSpec Current;
    }
}
