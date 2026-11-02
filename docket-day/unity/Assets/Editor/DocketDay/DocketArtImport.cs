// Docket Day — import rules for the art (no glTF importer in this project; models arrive as FBX):
//   * kit + Meshy FBX: 1 unit = 1 m (Blender FBX_SCALE_UNITS), no imported materials (the game assigns the palette
//     material, or the model's own texture for the Meshy bales), no cameras / lights, normals kept; readable ON for the
//     small palette kit in Resources/kit (RegionWorld merges the static props into the street / tower / rooms meshes —
//     0 extra draws; a few thousand vertices), OFF for the Meshy characters in Art/ (memory);
//   * the palette texture: point filter, no mipmaps, uncompressed (8×8 px — any blur mixes neighbouring swatches);
//   * Meshy textures: max 1024 (the _low twins 512), ASTC on Android.
using UnityEditor;
using UnityEngine;

namespace DocketDay.EditorTools
{
    public sealed class DocketArtImport : AssetPostprocessor
    {
        // bump when the rules change so Unity re-imports the affected assets (v2: kit meshes readable for merging)
        public override uint GetVersion() => 2;

        void OnPreprocessModel()
        {
            if (!(assetImporter is ModelImporter m)) return;
            if (!assetPath.Contains("/Resources/kit/") && !assetPath.Contains("/Art/")) return;
            m.globalScale = 1f;
            m.useFileScale = true;
            m.materialImportMode = ModelImporterMaterialImportMode.None;
            m.importCameras = false;
            m.importLights = false;
            m.isReadable = assetPath.Contains("/Resources/kit/");
            m.importNormals = ModelImporterNormals.Import;
            m.animationType = ModelImporterAnimationType.None;
            m.importAnimation = false;
        }

        void OnPreprocessTexture()
        {
            if (!(assetImporter is TextureImporter t)) return;
            if (assetPath.EndsWith("materials/palette.png"))
            {
                t.filterMode = FilterMode.Point;
                t.mipmapEnabled = false;
                t.textureCompression = TextureImporterCompression.Uncompressed;
                t.wrapMode = TextureWrapMode.Clamp;
                t.sRGBTexture = true;
                return;
            }
            if (assetPath.Contains("/Resources/kit/") || assetPath.Contains("/Art/Meshy/"))
            {
                t.maxTextureSize = assetPath.EndsWith("_low.png") ? 512 : 1024;
                t.mipmapEnabled = true;
                var android = t.GetPlatformTextureSettings("Android");
                android.overridden = true;
                android.maxTextureSize = t.maxTextureSize;
                android.format = TextureImporterFormat.ASTC_6x6;
                t.SetPlatformTextureSettings(android);
            }
        }
    }
}
