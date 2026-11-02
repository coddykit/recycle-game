// Volt City instanced props (P2 plan; the Godot "MultiMesh" of the original art plan). One batch per kind =
// one draw: crew trucks, cars, fault markers, trees, wind rotors (Graphics.RenderMeshInstanced, GPU instancing on the
// VoltCity/Prop* and VoltCity/Blades materials).
// Rules carried over from the Godot fleet lessons:
//  * per-instance data arrays (matrices, _InstCustom, _InstColor) are rewritten ONLY when an instance changes
//    (MarkDirty); an unchanged frame re-submits the cached arrays (one RenderMeshInstanced call per kind, no loops);
//  * _InstColor is always WHITE for every instance (never rely on a zero default: the Godot "black crowd" bug);
//  * _InstCustom holds small bounded values (boom 0..1, shape / paint index, phase 0..1, rotor rad/s + phase 0..2pi):
//    exact in half precision, never absolute time or age;
//  * Low: trees 50 % (every second layout tree), cars <= 20 and moved at 15 Hz by the traffic code (P3); Medium: all.
//  * Map-sized batches: Unity frustum-culls a RenderMeshInstanced batch by its worldBounds only; with the whole city in
//    view on Low that is the right trade (cells would add draws). Medium / High may split trees into <= 128 m cells
//    when zoomed in (CellSize) — measure in P3.
// Uncompiled until the Unity project exists (P3).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoltCity.Art
{
    public class VcInstancedProps
    {
        public const int MaxPerBatch = 511;   // GLES3 constant-buffer safe size for 2 x float4 per instance + matrices

        readonly Mesh mesh;
        readonly Material material;
        readonly List<Matrix4x4> matrices = new();
        readonly List<Vector4> custom = new();
        readonly MaterialPropertyBlock block = new();
        readonly Vector4[] customArr = new Vector4[MaxPerBatch];
        readonly Vector4[] colourArr = new Vector4[MaxPerBatch];
        RenderParams rp;
        bool dirty = true;
        Bounds bounds;

        static readonly int CustomId = Shader.PropertyToID("_InstCustom");
        static readonly int ColourId = Shader.PropertyToID("_InstColor");

        public VcInstancedProps(Mesh mesh, Material material, bool castShadows)
        {
            this.mesh = mesh;
            this.material = material;
            material.enableInstancing = true;
            rp = new RenderParams(material)
            {
                shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                receiveShadows = castShadows,
                matProps = block,
            };
            for (int i = 0; i < MaxPerBatch; i++)
                colourArr[i] = Vector4.one;          // white, always
        }

        public int Count => matrices.Count;

        public void Clear()
        {
            matrices.Clear();
            custom.Clear();
            dirty = true;
        }

        public int Add(Vector3 pos, float yawDeg, float scale, Vector4 data)
        {
            if (matrices.Count >= MaxPerBatch)
                return -1;
            matrices.Add(Matrix4x4.TRS(pos, Quaternion.Euler(0f, yawDeg, 0f), Vector3.one * scale));
            custom.Add(data);
            dirty = true;
            return matrices.Count - 1;
        }

        /// Only when this instance really changed (position / heading / boom / pulse phase...).
        public void Set(int i, Vector3 pos, float yawDeg, float scale, Vector4 data)
        {
            var m = Matrix4x4.TRS(pos, Quaternion.Euler(0f, yawDeg, 0f), Vector3.one * scale);
            if (matrices[i] == m && custom[i] == data)
                return;
            matrices[i] = m;
            custom[i] = data;
            dirty = true;
        }

        /// Call once per frame from the city's LateUpdate (Low: the traffic code itself runs at 15 Hz).
        public void Draw()
        {
            if (matrices.Count == 0)
                return;
            if (dirty)
            {
                custom.CopyTo(customArr);
                block.SetVectorArray(CustomId, customArr);
                block.SetVectorArray(ColourId, colourArr);
                bounds = new Bounds(matrices[0].GetPosition(), Vector3.one * 10f);
                foreach (var m in matrices)
                    bounds.Encapsulate(m.GetPosition());
                bounds.Expand(60f);
                rp.worldBounds = bounds;
                rp.matProps = block;
                dirty = false;
            }
            Graphics.RenderMeshInstanced(rp, mesh, 0, matrices);
        }
    }
}
