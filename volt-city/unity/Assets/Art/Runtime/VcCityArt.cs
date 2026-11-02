// Volt City city art (P2 runtime sketch for P3): builds the diorama from the Blender models + layout.json with the
// Low budget of ART section 3 — draw groups: terrain + sky (one combined mesh) 1 · districts 8 · plants (built set
// combined) 1 · grid (lines + substations combined) 1 · rotors 1 · trucks 1 · cars 1 · fault markers 1 · trees 1 = 16.
// Light ripple: one material instance per district (same shader / variant, SRP Batcher compatible) with its own
// _Ripple = (front, target); a 0.6 s coroutine moves the front only while a district switches (no per-frame work
// otherwise). Corridor / plant / battery / reservoir states are shader globals written only on change.
// Fallback if the per-district materials are ever unwanted: a MaterialPropertyBlock per district renderer (still one
// draw each; drops the SRP Batcher for those 8 renderers) — measure in P3 and record the choice in ART-IDS.md.
// Uncompiled until the Unity project exists (P3): references are assigned in the inspector (the importer keeps the
// model assets under Assets/Art/Models; layout.json is a TextAsset).
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoltCity.Art
{
    public class VcCityArt : MonoBehaviour
    {
        [System.Serializable] public class DistrictRef { public string id; public Mesh mesh; }
        [System.Serializable] public class PlantRef { public string type; public Mesh[] levels = new Mesh[3]; }

        [Header("Models (Assets/Art/Models)")]
        public Mesh terrain, sky, gridLines, substation, substationSmart;
        public Mesh truck, car, faultMarker, tree, windBlades;
        public DistrictRef[] districts;
        public PlantRef[] plants;
        public TextAsset layoutJson;

        [Header("Materials (one per tier, swapped by the graphics policy)")]
        public Material worldMedium, worldLow, propMedium, propLow, blades;

        public bool low = true;
        public const float RippleSeconds = 0.6f;

        readonly Dictionary<string, MeshRenderer> districtRenderers = new();
        readonly Dictionary<string, Material> districtMats = new();
        readonly Dictionary<string, int> plantLevels = new();
        readonly Vector4[] corridor = new Vector4[16];
        readonly float[] plantOn = { 1, 1, 1, 1, 1, 1 };
        MeshFilter plantsFilter, gridFilter;
        VcInstancedProps trucks, cars, markers, trees, rotors;
        Layout layout;

        static readonly int RippleId = Shader.PropertyToID("_Ripple");
        static readonly int MeshKindId = Shader.PropertyToID("_MeshKind");

        [System.Serializable] class LDistrict { public string id; public float[] box_min; public float[] box_max; public float[] substation; }
        [System.Serializable] class LHub { public string id; public float[] pos; }
        [System.Serializable] class LWind { public float[] site; public List<float[]> hubs; }
        [System.Serializable] class Layout { public List<LDistrict> districts; public List<LHub> hubs; }

        public void Build()
        {
            layout = JsonUtility.FromJson<Layout>(layoutJson.text);
            var mat = low ? worldLow : worldMedium;
            // terrain + sky = one mesh (one draw)
            Spawn("Terrain", Combine(new[] { terrain, sky }), Kind(mat, 0), false);
            foreach (var d in districts)
            {
                var m = Kind(mat, 1);
                m.SetVector(RippleId, new Vector4(2f, 0f, 0f, 0f));
                districtMats[d.id] = m;
                districtRenderers[d.id] = Spawn("District_" + d.id, d.mesh, m, !low);
            }
            plantsFilter = Spawn("Plants", new Mesh(), Kind(mat, 3), !low).GetComponent<MeshFilter>();
            gridFilter = Spawn("Grid", BuildGrid(new HashSet<string>()), Kind(mat, 2), !low).GetComponent<MeshFilter>();
            var pm = low ? propLow : propMedium;
            truckMat = PropKind(pm, 0);
            trucks = new VcInstancedProps(truck, truckMat, !low);
            cars = new VcInstancedProps(car, PropKind(pm, 1), !low);
            markers = new VcInstancedProps(faultMarker, PropKind(pm, 2), false);
            trees = new VcInstancedProps(tree, PropKind(pm, 3), !low);
            bladesMat = blades != null ? new Material(blades) { enableInstancing = true } : null;
            rotors = new VcInstancedProps(windBlades, bladesMat, !low);
            for (int i = 0; i < 16; i++)
                corridor[i] = new Vector4(3f, 0f, 0f, 0f);     // nothing built until the sim says so
            PushGlobals();
        }

        // ---------------------------------------------------------------------------------------------- cosmetics (P4)
        Material truckMat, bladesMat;

        /// <summary>Sold looks (docs/IAP.md, ART-IDS.md): truck livery on the truck material (Medium AND Low prop shaders),
        /// candy blades on the blade material, neon skyline + art deco pylons as globals (VcWorldCore, every tier).
        /// Call on change only (Volt City P4 LookState.Sig).</summary>
        public void SetLooks(Color liveryA, Color liveryB, float bladeStyle, float skylineStyle, float pylonDeco)
        {
            if (truckMat != null)
            {
                truckMat.SetColor("_LiveryA", liveryA);
                truckMat.SetColor("_LiveryB", liveryB);
            }
            if (bladesMat != null) bladesMat.SetFloat("_BladeStyle", bladeStyle);
            Shader.SetGlobalFloat("_VcSkylineStyle", skylineStyle);
            Shader.SetGlobalFloat("_VcPylonDeco", pylonDeco);
        }

        // ---------------------------------------------------------------------------------------------- districts
        public void SetDistrictLit(string id, bool lit, bool animate = true)
        {
            var m = districtMats[id];
            if (!animate)
            {
                m.SetVector(RippleId, new Vector4(2f, lit ? 1f : 0f, 0f, 0f));
                return;
            }
            StartCoroutine(Ripple(m, lit ? 1f : 0f));
        }

        IEnumerator Ripple(Material m, float target)
        {
            for (float t = 0f; t < RippleSeconds; t += Time.deltaTime)
            {
                m.SetVector(RippleId, new Vector4(Mathf.Lerp(0f, 1.3f, t / RippleSeconds), target, 0f, 0f));
                yield return null;
            }
            m.SetVector(RippleId, new Vector4(2f, target, 0f, 0f));
        }

        // ---------------------------------------------------------------------------------------------- grid / plants
        public void SetCorridor(int index, int state, float flow)
        {
            var v = new Vector4(state, flow, 0f, 0f);
            if (corridor[index] == v)
                return;
            corridor[index] = v;
            Shader.SetGlobalVectorArray("_VcCorridorState", corridor);
        }

        public void SetSmartSubstations(HashSet<string> smart) => gridFilter.sharedMesh = BuildGrid(smart);

        public void SetPlantLevels(Dictionary<string, int> levels)
        {
            var parts = new List<Mesh>();
            foreach (var p in plants)
                if (levels.TryGetValue(p.type, out int l) && l > 0)
                    parts.Add(p.levels[l - 1]);
            plantsFilter.sharedMesh = Combine(parts.ToArray());
            plantLevels.Clear();
            foreach (var kv in levels)
                plantLevels[kv.Key] = kv.Value;
        }

        public void SetPlantOutput(int typeIndex, float fraction)
        {
            if (Mathf.Approximately(plantOn[typeIndex], fraction))
                return;
            plantOn[typeIndex] = fraction;
            Shader.SetGlobalFloatArray("_VcPlantOn", plantOn);
        }

        public VcInstancedProps Trucks => trucks;
        public VcInstancedProps Cars => cars;
        public VcInstancedProps Markers => markers;
        public VcInstancedProps Trees => trees;
        public VcInstancedProps Rotors => rotors;

        void LateUpdate()
        {
            // one submit per batch; instance arrays are rewritten only inside VcInstancedProps when something changed
            trucks?.Draw();
            cars?.Draw();
            markers?.Draw();
            trees?.Draw();
            rotors?.Draw();
        }

        // ---------------------------------------------------------------------------------------------- helpers
        void PushGlobals()
        {
            Shader.SetGlobalVectorArray("_VcCorridorState", corridor);
            Shader.SetGlobalFloatArray("_VcPlantOn", plantOn);
            Shader.SetGlobalFloat("_VcGridFlow", .6f);
            Shader.SetGlobalFloat("_VcBatteryCharge", .6f);
            Shader.SetGlobalFloat("_VcReservoirLevel", .8f);
            Shader.SetGlobalVectorArray("_VcPaints", new Vector4[]
            {
                C("eeeeea"), C("b9bec4"), C("4a4f57"), C("c8433a"), C("3b67b5"), C("2f8f8a"), C("5c8f4e"), C("d8c08a"),
            });
        }

        static Vector4 C(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c.linear;
        }

        Mesh BuildGrid(HashSet<string> smart)
        {
            var parts = new List<CombineInstance> { new() { mesh = gridLines, transform = Matrix4x4.identity } };
            foreach (var d in layout.districts)
                parts.Add(new CombineInstance
                {
                    mesh = smart.Contains(d.id) ? substationSmart : substation,
                    transform = Matrix4x4.Translate(new Vector3(d.substation[0], d.substation[1], d.substation[2])),
                });
            foreach (var h in layout.hubs)
                parts.Add(new CombineInstance { mesh = substation, transform = Matrix4x4.Translate(new Vector3(h.pos[0], h.pos[1], h.pos[2])) });
            var m = new Mesh { indexFormat = IndexFormat.UInt32 };
            m.CombineMeshes(parts.ToArray(), true, true, false);   // substations keep uv.x = -1: never hidden by a corridor
            return m;
        }

        static Mesh Combine(Mesh[] meshes)
        {
            var parts = new CombineInstance[meshes.Length];
            for (int i = 0; i < meshes.Length; i++)
                parts[i] = new CombineInstance { mesh = meshes[i], transform = Matrix4x4.identity };
            var m = new Mesh { indexFormat = IndexFormat.UInt32 };
            m.CombineMeshes(parts, true, true, false);
            return m;
        }

        static Material Kind(Material shared, int kind)
        {
            var m = new Material(shared);
            m.SetFloat(MeshKindId, kind);
            return m;
        }

        static Material PropKind(Material shared, int kind)
        {
            var m = new Material(shared) { enableInstancing = true };
            m.SetFloat("_PropKind", kind);
            return m;
        }

        MeshRenderer Spawn(string name, Mesh mesh, Material mat, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = shadows;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return r;
        }
    }
}
