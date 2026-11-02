// Ridgeback Rangers — UI fonts (P5): dynamic TextCore font assets for the Noto subsets in Resources/ui/fonts (cut by
// Tools/subset_fonts.py), Bold as the weight-700 face, the fallback chain JP -> KR -> Devanagari -> Symbols2 -> Symbols,
// and a PanelTextSettings asset on the kit PanelSettings. Dynamic atlases start empty (nothing baked: the CJK
// glyphs rasterise on first use). Called by RidgebackProjectSetup.Apply; idempotent.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace RidgebackRangers.EditorTools
{
    public static class RidgebackFonts
    {
        public const string Dir = "Assets/Resources/ui/fonts";
        public const string TextSettingsPath = "Assets/Resources/ui/fonts/RidgebackPanelText.asset";
        static readonly string[] Faces = { "NotoSans", "NotoSansJP", "NotoSansKR", "NotoSansDevanagari", "NotoSansSymbols2", "NotoSansSymbols" };

        public static void Apply(PanelSettings panel)
        {
            var main = new List<FontAsset>();
            foreach (var face in Faces)
            {
                var regular = Ensure(face + "-Regular");
                if (regular == null) { Debug.LogError("[RidgebackRangers] font missing: " + face + " (run Tools/subset_fonts.py)"); return; }
                var bold = Ensure(face + "-Bold");
                if (bold != null)
                {
                    var table = regular.fontWeightTable;
                    table[7].regularTypeface = bold;   // -unity-font-style: bold -> the real 700 face, not faux bold
                    EditorUtility.SetDirty(regular);
                }
                ClearOnBuild(regular);
                if (bold != null) ClearOnBuild(bold);
                main.Add(regular);
            }
            var fallbacks = main.GetRange(1, main.Count - 1);
            main[0].fallbackFontAssetTable = new List<FontAsset>(fallbacks);
            EditorUtility.SetDirty(main[0]);

            var ts = AssetDatabase.LoadAssetAtPath<PanelTextSettings>(TextSettingsPath);
            if (ts == null)
            {
                ts = ScriptableObject.CreateInstance<PanelTextSettings>();
                AssetDatabase.CreateAsset(ts, TextSettingsPath);
            }
            ts.defaultFontAsset = main[0];
            ts.fallbackFontAssets = new List<FontAsset>(fallbacks);
            EditorUtility.SetDirty(ts);
            if (panel != null && panel.textSettings != ts)
            {
                panel.textSettings = ts;
                EditorUtility.SetDirty(panel);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[RidgebackRangers] fonts: " + main.Count + " faces, fallbacks " + fallbacks.Count);
        }

        // the public API has no setter: the serialized flag drops the glyphs an editor play session rasterised into the
        // asset, so a build (and git) never carries a half-filled atlas
        static void ClearOnBuild(FontAsset fa)
        {
            var so = new SerializedObject(fa);
            var p = so.FindProperty("m_ClearDynamicDataOnBuild");
            if (p != null && !p.boolValue) { p.boolValue = true; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        static FontAsset Ensure(string name)
        {
            string ttf = Dir + "/" + name + ".ttf", path = Dir + "/" + name + ".asset";
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttf);
            if (font == null) return null;
            var fa = AssetDatabase.LoadAssetAtPath<FontAsset>(path);
            if (fa != null) return fa;
            fa = FontAsset.CreateFontAsset(font, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fa.name = name;
            AssetDatabase.CreateAsset(fa, path);
            foreach (var t in fa.atlasTextures)
            {
                if (t == null) continue;
                t.name = name + " Atlas";
                AssetDatabase.AddObjectToAsset(t, fa);
            }
            fa.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            AssetDatabase.SaveAssets();
            return fa;
        }
    }
}
