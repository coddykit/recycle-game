// Volt City art import rules (P2). Unity is not installed on the build Mac yet: this file is written against the
// Unity 6 LTS editor API and is compiled for the first time in P3 — fix and note anything the compiler reports there.
//
// Every model under Assets/Art/Models/** (the Blender pipeline's FBX; blender/build_art.py):
//   * one mesh, no materials imported (the game assigns the shared VoltCity/* materials), no cameras / lights / blend
//     shapes / animation, normals imported, NO tangents;
//   * Mesh Compression OFF and Read/Write ON (the data UVs must stay exact; plants / grid / substations are merged at
//     runtime), Generate Lightmap UVs OFF (it would overwrite UV1 = window style + base height), keep vertex order;
//   * bake axis conversion; then the imported bounds are compared with the sidecar's unity_bounds_* and a wrong axis
//     convention is corrected in place (rotate 180 about Y, or mirror X / Z + flip winding) and logged — an
//     unmatchable model is an import ERROR (the Godot-era lesson: a silently wrong import costs days).
// The .glb twins are reference / validation files (blender/validate_art.py); do not install a glTF importer package
// in the game project or they import as duplicates.
// Assets/Art/Textures/surface.png: linear (sRGB off: it is data), mipmaps, repeat, trilinear, aniso 4, max 256.
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VoltCity.Art.Editor
{
    public class VcArtImport : AssetPostprocessor
    {
        // bump when an import rule changes: Unity re-imports every asset this postprocessor touches
        public override uint GetVersion() => 2;

        const string ModelRoot = "Assets/Art/Models/";
        const string Surface = "Assets/Art/Textures/surface.png";

        [System.Serializable]
        class Sidecar
        {
            public string id;
            public string material;
            public int triangles;
            public float[] unity_bounds_min;
            public float[] unity_bounds_max;
        }

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelRoot) || !assetPath.EndsWith(".fbx"))
                return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.bakeAxisConversion = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.importBlendShapes = false;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.isReadable = true;
            mi.optimizeMeshPolygons = false;
            mi.optimizeMeshVertices = false;
            mi.weldVertices = false;
            mi.generateSecondaryUV = false;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.None;
            mi.indexFormat = ModelImporterIndexFormat.Auto;
            mi.keepQuads = false;
        }

        void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(ModelRoot) || !assetPath.EndsWith(".fbx"))
                return;
            string sidePath = Path.ChangeExtension(assetPath, ".json");
            if (!File.Exists(sidePath))
            {
                Debug.LogError($"[VcArtImport] {assetPath}: sidecar {sidePath} missing");
                return;
            }
            var side = JsonUtility.FromJson<Sidecar>(File.ReadAllText(sidePath));
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length != 1)
            {
                Debug.LogError($"[VcArtImport] {assetPath}: expected one mesh, found {filters.Length}");
                return;
            }
            var mf = filters[0];
            var mesh = mf.sharedMesh;
            // the node transform is baked into the vertices so a model placed at the origin is in city space
            var m = mf.transform.localToWorldMatrix * root.transform.worldToLocalMatrix;
            var v = mesh.vertices;
            var n = mesh.normals;
            for (int i = 0; i < v.Length; i++)
            {
                v[i] = m.MultiplyPoint3x4(v[i]);
                if (n.Length == v.Length)
                    n[i] = m.MultiplyVector(n[i]).normalized;
            }
            var want = new Bounds();
            want.SetMinMax(V(side.unity_bounds_min), V(side.unity_bounds_max));
            float tol = Mathf.Max(0.05f, want.size.magnitude * 0.002f);
            // candidates: as imported, rotated 180 about Y, mirrored X, mirrored Z
            Vector3[] scales = { new(1, 1, 1), new(-1, 1, -1), new(-1, 1, 1), new(1, 1, -1) };
            int pick = -1;
            for (int k = 0; k < scales.Length && pick < 0; k++)
            {
                var b = BoundsOf(v, scales[k]);
                if (Close(b.min, want.min, tol) && Close(b.max, want.max, tol))
                    pick = k;
            }
            if (pick < 0)
            {
                Debug.LogError($"[VcArtImport] {assetPath}: bounds {BoundsOf(v, Vector3.one)} do not match the sidecar {want} in any axis convention");
                return;
            }
            var s = scales[pick];
            for (int i = 0; i < v.Length; i++)
            {
                v[i] = Vector3.Scale(v[i], s);
                if (n.Length == v.Length)
                    n[i] = Vector3.Scale(n[i], s);
            }
            mesh.vertices = v;
            if (n.Length == v.Length)
                mesh.normals = n;
            if (s.x * s.z < 0)
            {
                for (int sm = 0; sm < mesh.subMeshCount; sm++)
                {
                    var t = mesh.GetTriangles(sm);
                    for (int i = 0; i < t.Length; i += 3)
                        (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
                    mesh.SetTriangles(t, sm);
                }
            }
            mesh.RecalculateBounds();
            mf.transform.localPosition = Vector3.zero;
            mf.transform.localRotation = Quaternion.identity;
            mf.transform.localScale = Vector3.one;
            if (pick != 0)
                Debug.Log($"[VcArtImport] {assetPath}: axis convention corrected ({(pick == 1 ? "rotate 180 Y" : pick == 2 ? "mirror X" : "mirror Z")})");
            if (mesh.colors32 == null || mesh.colors32.Length != v.Length)
                Debug.LogError($"[VcArtImport] {assetPath}: vertex colours missing (material codes live in the alpha)");
            var uv2 = new System.Collections.Generic.List<Vector2>();
            mesh.GetUVs(1, uv2);
            if (uv2.Count != v.Length)
                Debug.LogError($"[VcArtImport] {assetPath}: UV1 (window style / base height) missing");
            int tris = mesh.triangles.Length / 3;
            if (tris != side.triangles)
                Debug.LogError($"[VcArtImport] {assetPath}: {tris} triangles, sidecar says {side.triangles}");
        }

        void OnPreprocessTexture()
        {
            // UI images (icons, splash / curtain, onboarding heroes) are drawn ~1:1 by UI Toolkit: no mipmaps, and they
            // ignore the Low tier's globalTextureMipmapLimit (1), which drew the 720 px curtain at 360 px — the "pixelated"
            // pager on the 10-28 device frame. Bilinear, high-quality compression (ASTC 4x4 on Android).
            if (assetPath.StartsWith("Assets/Resources/ui/") || assetPath.StartsWith("Assets/Art/Splash/"))
            {
                var ui = (TextureImporter)assetImporter;
                ui.mipmapEnabled = false;
                ui.ignoreMipmapLimit = true;
                ui.filterMode = FilterMode.Bilinear;
                ui.wrapMode = TextureWrapMode.Clamp;
                ui.textureCompression = TextureImporterCompression.CompressedHQ;
                ui.npotScale = TextureImporterNPOTScale.None;
                return;
            }
            if (assetPath != Surface)
                return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = false;
            ti.mipmapEnabled = true;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.filterMode = FilterMode.Trilinear;
            ti.anisoLevel = 4;
            ti.maxTextureSize = 256;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
        }

        static Vector3 V(float[] a) => new(a[0], a[1], a[2]);

        static bool Close(Vector3 a, Vector3 b, float tol) =>
            Mathf.Abs(a.x - b.x) <= tol && Mathf.Abs(a.y - b.y) <= tol && Mathf.Abs(a.z - b.z) <= tol;

        static Bounds BoundsOf(Vector3[] v, Vector3 s)
        {
            var b = new Bounds(Vector3.Scale(v[0], s), Vector3.zero);
            foreach (var p in v)
                b.Encapsulate(Vector3.Scale(p, s));
            return b;
        }
    }
}
