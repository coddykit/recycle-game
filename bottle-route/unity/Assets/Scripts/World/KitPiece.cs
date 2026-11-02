// Bottle Route — Blender-kit pieces that are MERGED into the district / depot meshes (buildings, stadium, depot compound;
// polish pass 2026-11-02). tools/art/kit.py writes them as Resources/kit/<name>.bytes (BRKM v1): Unity space, flat
// normals, palette UVs, uint16 clockwise-front triangles. A TextAsset needs no readable-mesh import flag and no FBX axis
// guess; it is parsed once per name (cached) when the diorama is (re)built — never per frame.
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BottleRoute.World
{
    public sealed class KitPiece
    {
        public Vector3[] V, N;
        public Vector2[] Uv;
        public int[] Sw;   // palette swatch per vertex (for the wall / roof swaps)
        public int[] T;
        public int Tris => T.Length / 3;

        static readonly Dictionary<string, KitPiece> Cache = new Dictionary<string, KitPiece>();

        /// <summary>Resources/kit/<name>.bytes, or null when missing / malformed (callers keep the procedural fallback).</summary>
        public static KitPiece Load(string name)
        {
            if (Cache.TryGetValue(name, out var km)) return km;
            var ta = Resources.Load<TextAsset>("kit/" + name);
            km = ta != null ? Parse(ta.bytes) : null;
            Cache[name] = km;
            return km;
        }

        public static KitPiece Parse(byte[] b)
        {
            if (b == null || b.Length < 16 || b[0] != 'B' || b[1] != 'R' || b[2] != 'K' || b[3] != 'M') return null;
            using (var r = new BinaryReader(new MemoryStream(b)))
            {
                r.ReadBytes(4);
                if (r.ReadUInt16() != 1) return null;
                r.ReadUInt16();
                int nv = (int)r.ReadUInt32(), ni = (int)r.ReadUInt32();
                if (nv <= 0 || ni <= 0 || ni % 3 != 0 || b.Length != 16 + nv * 32 + ni * 2) return null;
                var km = new KitPiece { V = new Vector3[nv], N = new Vector3[nv], Uv = new Vector2[nv], Sw = new int[nv], T = new int[ni] };
                for (int i = 0; i < nv; i++)
                {
                    km.V[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    km.N[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    var uv = new Vector2(r.ReadSingle(), r.ReadSingle());
                    km.Uv[i] = uv;
                    int sx = Mathf.Clamp((int)(uv.x * Pal.Size), 0, Pal.Size - 1), sy = Mathf.Clamp((int)((1f - uv.y) * Pal.Size), 0, Pal.Size - 1);
                    km.Sw[i] = sy * Pal.Size + sx;
                }
                for (int i = 0; i < ni; i++)
                {
                    int t = r.ReadUInt16();
                    if (t >= nv) return null;
                    km.T[i] = t;
                }
                return km;
            }
        }
    }
}
