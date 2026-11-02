// Bottle Route app icon (placeholder set from tools/art until the owner picks a candidate at P7; bottle-route/store/icon-candidates/).
// Sets EVERY icon slot so Unity's default icon can never ship: Player Settings default icon (Unknown target) + Android
// adaptive (background + foreground, all 6 densities), round (6) and legacy (6). Called by BottleAndroidBuild before every
// build (fails the build when a slot is empty) and from the command line to write ProjectSettings once:
//   Unity -batchmode -nographics -projectPath bottle-route/unity -executeMethod BottleRoute.EditorTools.BottleIcons.ApplyAndExit
// Static gate without the editor: Tools/icon_check.py (check.sh); AAB gate: icon_check.py --aab (build_unity_android.sh).
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace BottleRoute.EditorTools
{
    public static class BottleIcons
    {
        public const string Dir = "Assets/Art/Icon/";
        public const string Full = Dir + "icon_full.png";            // 1024, full-bleed square (default icon)
        public const string Foreground = Dir + "icon_adaptive_fg.png"; // 432, transparent, art inside the 66 dp safe zone
        public const string Background = Dir + "icon_adaptive_bg.png"; // 432, background layer
        public const string Round = Dir + "icon_round.png";           // 512, circle with transparent corners
        public const string Legacy = Dir + "icon_legacy.png";         // 512, rounded square with transparent corners

        static Texture2D Load(string path)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t == null) throw new InvalidOperationException("[BottleRoute] icon texture missing: " + path);
            return t;
        }

        public static void Apply()
        {
            Texture2D full = Load(Full), fg = Load(Foreground), bg = Load(Background), round = Load(Round), legacy = Load(Legacy);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { full }, IconKind.Any);

            var android = NamedBuildTarget.Android;
            var adaptive = PlayerSettings.GetPlatformIcons(android, AndroidPlatformIconKind.Adaptive);
            foreach (var icon in adaptive) icon.SetTextures(bg, fg); // layer 0 = background, 1 = foreground
            PlayerSettings.SetPlatformIcons(android, AndroidPlatformIconKind.Adaptive, adaptive);

            var rounds = PlayerSettings.GetPlatformIcons(android, AndroidPlatformIconKind.Round);
            foreach (var icon in rounds) icon.SetTexture(round);
            PlayerSettings.SetPlatformIcons(android, AndroidPlatformIconKind.Round, rounds);

            var legacies = PlayerSettings.GetPlatformIcons(android, AndroidPlatformIconKind.Legacy);
            foreach (var icon in legacies) icon.SetTexture(legacy);
            PlayerSettings.SetPlatformIcons(android, AndroidPlatformIconKind.Legacy, legacies);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Every slot that would fall back to Unity's default icon (empty = OK).</summary>
        public static List<string> Problems()
        {
            var p = new List<string>();
            var defaults = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            if (defaults == null || defaults.Length == 0 || defaults[0] == null) p.Add("default icon (Player Settings) empty");
            var android = NamedBuildTarget.Android;
            void Check(PlatformIconKind kind, string name, int layers)
            {
                var icons = PlayerSettings.GetPlatformIcons(android, kind);
                if (icons == null || icons.Length == 0) { p.Add(name + ": no slots"); return; }
                foreach (var icon in icons)
                {
                    var tex = icon.GetTextures();
                    int set = 0;
                    if (tex != null) foreach (var t in tex) if (t != null) set++;
                    if (set < layers) p.Add($"{name} {icon.width}x{icon.height}: {set}/{layers} textures");
                }
            }
            Check(AndroidPlatformIconKind.Adaptive, "adaptive", 2);
            Check(AndroidPlatformIconKind.Round, "round", 1);
            Check(AndroidPlatformIconKind.Legacy, "legacy", 1);
            return p;
        }

        public static void ApplyAndExit()
        {
            int code = 0;
            try
            {
                Apply();
                var problems = Problems();
                if (problems.Count > 0)
                {
                    Debug.LogError("[BottleRoute] ICONS_FAILED\n- " + string.Join("\n- ", problems));
                    code = 1;
                }
                else Debug.Log("[BottleRoute] ICONS_OK all slots set (default + adaptive 6 + round 6 + legacy 6)");
            }
            catch (Exception e) { Debug.LogError("[BottleRoute] ICONS_FAILED " + e); code = 1; }
            EditorApplication.Exit(code);
        }
    }
}
