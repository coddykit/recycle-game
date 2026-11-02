// Charge Nurse — the diorama's procedural geometry. Everything in the world shares ONE material (URP Simple Lit on the
// 8×8 palette texture Resources/materials/palette.png): a model picks colours by pointing its UVs at palette swatches, so
// the SRP batcher draws the whole campus with a handful of batches (fleet §10: Low ≤ 150 draws, the plan targets ≤ 120).
// A hospital theme swaps the palette texture (zero extra draws). Builders merge many boxes into one mesh per room;
// nothing here runs per frame.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChargeNurse.World
{
    /// <summary>Palette swatch indices (Tools/make_palette.py writes the same order).</summary>
    public static class Pal
    {
        public const int Grass = 0, GrassDark = 1, Paving = 2, Floor = 3, Concrete = 4, WallCream = 5, WallMint = 6, WallBlue = 7,
            Roof = 8, RoofGrey = 9, Brand = 10, Panel = 11, Sheet = 12, Curtain = 13, Corridor = 14, Glass = 15,
            Wood = 16, Tree = 17, Trunk = 18, Locked = 19, Window = 20, TagGreen = 21, TagYellow = 22, TagOrange = 23,
            TagRed = 24, Siren = 25, OrFloor = 26, Gold = 27, Lavender = 28, Forest = 29, TealDark = 30, Retro = 31,
            Skin = 32, Gown = 33, Nurse = 34, Doctor = 35, Porter = 36, Cleaner = 37, Trousers = 38, Clerk = 39,
            Tech = 40, Mattress = 41, Machine = 42, Screen = 43, Rubber = 44, White = 45, Asphalt = 46, Hedge = 47;
        public const int Size = 8;
        public static readonly int[] Tags = { TagGreen, TagYellow, TagOrange, TagRed };

        public static Vector2 Uv(int swatch)
        {
            int x = swatch % Size, y = swatch / Size;
            return new Vector2((x + 0.5f) / Size, 1f - (y + 0.5f) / Size);
        }
    }

    public sealed class MeshKit
    {
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector3> n = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int> t = new List<int>();
        public int Vertices => v.Count;

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int swatch)
        {
            var nn = Vector3.Cross(b - a, d - a).normalized;
            int i = v.Count;
            var u = Pal.Uv(swatch);
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            for (int k = 0; k < 4; k++) { n.Add(nn); uv.Add(u); }
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
            t.Add(i); t.Add(i + 2); t.Add(i + 3);   // clockwise = Unity front face (fleet §0 fix from bottle-route)
        }

        /// <summary>Axis box on the ground rotated by yaw (degrees). top swatch may differ (roofs, sheets).</summary>
        public void Box(Vector3 bottomCenter, Vector3 size, float yaw, int side, int top = -1, bool bottom = false)
        {
            if (top < 0) top = side;
            var q = Quaternion.Euler(0, yaw, 0);
            float x = size.x * 0.5f, z = size.z * 0.5f, h = size.y;
            Vector3 P(float px, float py, float pz) => bottomCenter + q * new Vector3(px, py, pz);
            var p000 = P(-x, 0, -z); var p100 = P(x, 0, -z); var p110 = P(x, h, -z); var p010 = P(-x, h, -z);
            var p001 = P(-x, 0, z); var p101 = P(x, 0, z); var p111 = P(x, h, z); var p011 = P(-x, h, z);
            Quad(p000, p010, p110, p100, side);   // front (-z)
            Quad(p101, p111, p011, p001, side);   // back
            Quad(p001, p011, p010, p000, side);   // left
            Quad(p100, p110, p111, p101, side);   // right
            Quad(p010, p011, p111, p110, top);    // top
            if (bottom) Quad(p000, p100, p101, p001, side);
        }

        /// <summary>A flat strip on the ground from a to b (corridors), y lifted a little to avoid z-fighting.</summary>
        public void Strip(Vector3 a, Vector3 b, float width, float y, int swatch)
        {
            var dir = (b - a); dir.y = 0;
            if (dir.sqrMagnitude < 1e-6f) return;
            var side = Vector3.Cross(Vector3.up, dir.normalized) * (width * 0.5f);
            a.y = b.y = y;
            Quad(a - side, b - side, b + side, a + side, swatch);
        }

        /// <summary>A flat rectangle (room floors) centred at c.</summary>
        public void Floor(Vector3 c, float w, float d, float y, int swatch)
        {
            float x = w * 0.5f, z = d * 0.5f;
            Quad(new Vector3(c.x - x, y, c.z - z), new Vector3(c.x - x, y, c.z + z), new Vector3(c.x + x, y, c.z + z), new Vector3(c.x + x, y, c.z - z), swatch);
        }

        /// <summary>A low-poly tree (trunk + two stacked boxes).</summary>
        public void Tree(Vector3 at, float s, float yaw)
        {
            Box(at, new Vector3(0.3f, 1.4f, 0.3f) * s, yaw, Pal.Trunk);
            Box(at + Vector3.up * 1.2f * s, new Vector3(1.8f, 1.5f, 1.8f) * s, yaw + 15f, Pal.Tree);
            Box(at + Vector3.up * 2.6f * s, new Vector3(1.1f, 1.0f, 1.1f) * s, yaw + 40f, Pal.Tree);
        }

        /// <summary>Cut-away room: floor, tall back + side walls, a knee-high front wall (the camera looks in from the south).
        /// Polish pass: the back wall carries glass windows facing the camera (one quad each, ~3.5 m apart); a locked wing is a
        /// closed exterior block with two dark window bands on its front (plan §8 low plan: exterior boxes with window
        /// textures) and, off Low, a roof parapet.</summary>
        public void Room(Vector3 c, float w, float d, int floor, int wall, bool open, bool detail = false)
        {
            Floor(c, w, d, 0.06f, floor);
            float x = w * 0.5f, z = d * 0.5f, th = 0.25f;
            if (!open)
            {
                Box(c, new Vector3(w, 3.2f, d), 0, Pal.Locked, Pal.RoofGrey);
                float zf = c.z - z - 0.01f;
                WallQuad(c.x - x + 0.5f, c.x + x - 0.5f, 0.9f, 1.5f, zf, Pal.Window);
                WallQuad(c.x - x + 0.5f, c.x + x - 0.5f, 2.0f, 2.6f, zf, Pal.Window);
                if (detail) Box(c + new Vector3(0, 3.2f, 0), new Vector3(w + 0.2f, 0.25f, d + 0.2f), 0, Pal.Panel, Pal.RoofGrey);
                return;
            }
            Box(c + new Vector3(0, 0, z), new Vector3(w, 2.6f, th), 0, wall, Pal.Panel);
            Box(c + new Vector3(-x, 0, 0), new Vector3(th, 2.6f, d), 0, wall, Pal.Panel);
            Box(c + new Vector3(x, 0, 0), new Vector3(th, 2.6f, d), 0, wall, Pal.Panel);
            Box(c + new Vector3(-x * 0.55f, 0, -z), new Vector3(w * 0.45f, 0.5f, th), 0, wall, Pal.Panel);   // door gap in the middle
            Box(c + new Vector3(x * 0.55f, 0, -z), new Vector3(w * 0.45f, 0.5f, th), 0, wall, Pal.Panel);
            int windows = Mathf.Max(1, Mathf.FloorToInt(w / 3.5f));
            float step = w / windows, zb = c.z + z - th * 0.5f - 0.01f;
            for (int i = 0; i < windows; i++)
            {
                float wx = c.x - x + step * (i + 0.5f);
                WallQuad(wx - 0.7f, wx + 0.7f, 1.15f, 2.15f, zb, Pal.Glass);
            }
        }

        /// <summary>A quad on a wall that faces the camera (−z), from x0..x1, y0..y1 at depth z.</summary>
        public void WallQuad(float x0, float x1, float y0, float y1, float z, int swatch)
        {
            if (x1 <= x0) return;
            Quad(new Vector3(x0, y0, z), new Vector3(x0, y1, z), new Vector3(x1, y1, z), new Vector3(x1, y0, z), swatch);
        }

        /// <summary>Parking lot (asphalt + bay lines) — flat quads only.</summary>
        public void Parking(Vector3 c, int bays, float bayW = 2.6f, float bayD = 5f)
        {
            float w = bays * bayW;
            Floor(c, w + 1f, bayD + 1f, 0.03f, Pal.Asphalt);
            for (int i = 0; i <= bays; i++)
            {
                float lx = c.x - w * 0.5f + i * bayW;
                Strip(new Vector3(lx, 0, c.z - bayD * 0.5f + 0.3f), new Vector3(lx, 0, c.z + bayD * 0.5f), 0.12f, 0.045f, Pal.White);
            }
        }

        /// <summary>The Low / fallback entrance porch: two posts, a slab with a teal top, glass doors (4 boxes).</summary>
        public void Porch(Vector3 at)
        {
            Box(at + new Vector3(0, 0, 0.1f), new Vector3(3.0f, 2.3f, 0.12f), 0, Pal.Glass);
            Box(at + new Vector3(-2.8f, 0, -1.7f), new Vector3(0.22f, 2.55f, 0.22f), 0, Pal.Panel);
            Box(at + new Vector3(2.8f, 0, -1.7f), new Vector3(0.22f, 2.55f, 0.22f), 0, Pal.Panel);
            Box(at + new Vector3(0, 2.55f, -1.0f), new Vector3(6.4f, 0.16f, 2.4f), 0, Pal.White, Pal.Brand);
        }

        // ------------------------------------------------------------------ merging kit models (static props, 0 extra draws)

        static readonly List<Vector3> tv = new List<Vector3>(), tn = new List<Vector3>();
        static readonly List<Vector2> tuv = new List<Vector2>();
        static readonly List<int> tt = new List<int>();

        /// <summary>Appends a Blender-kit prefab (Resources/kit, imported readable) at pos / yaw into this mesh — the same
        /// placement HospitalWorld.Prop gives an instance (the root's rotation replaced by the yaw, its scale kept). False when
        /// the prefab is missing or a mesh is not readable: the caller then uses its procedural fallback.</summary>
        public bool Append(GameObject prefab, Vector3 pos, float yaw)
        {
            if (prefab == null) return false;
            var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0) return false;
            foreach (var f in filters) if (f.sharedMesh == null || !f.sharedMesh.isReadable) return false;
            var root = prefab.transform;
            var place = Matrix4x4.TRS(pos, Quaternion.Euler(0, yaw, 0), root.localScale);
            foreach (var f in filters) AppendMesh(f.sharedMesh, place * (root.worldToLocalMatrix * f.transform.localToWorldMatrix));
            return true;
        }

        void AppendMesh(Mesh mesh, Matrix4x4 m)
        {
            int baseV = v.Count;
            mesh.GetVertices(tv); mesh.GetNormals(tn); mesh.GetUVs(0, tuv);
            for (int i = 0; i < tv.Count; i++)
            {
                v.Add(m.MultiplyPoint3x4(tv[i]));
                n.Add(i < tn.Count ? m.MultiplyVector(tn[i]).normalized : Vector3.up);
                uv.Add(i < tuv.Count ? tuv[i] : Pal.Uv(Pal.Machine));
            }
            bool flip = m.determinant < 0;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                mesh.GetTriangles(tt, s);
                for (int i = 0; i + 2 < tt.Count; i += 3)
                {
                    t.Add(baseV + tt[i]);
                    t.Add(baseV + (flip ? tt[i + 2] : tt[i + 1]));
                    t.Add(baseV + (flip ? tt[i + 1] : tt[i + 2]));
                }
            }
        }

        /// <summary>Appends another builder's geometry (procedural fallbacks) at pos / yaw.</summary>
        public void Append(MeshKit other, Vector3 pos, float yaw)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            int baseV = v.Count;
            for (int i = 0; i < other.v.Count; i++) { v.Add(pos + q * other.v[i]); n.Add(q * other.n[i]); uv.Add(other.uv[i]); }
            foreach (int i in other.t) t.Add(baseV + i);
        }

        public Mesh Build(string name)
        {
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            m.UploadMeshData(true);   // no CPU copy kept (memory budget)
            return m;
        }

        /// <summary>A mesh whose UVs change later (the corridor heat map): keeps the CPU copy.</summary>
        public Mesh BuildDynamic(string name, out List<Vector2> uvs)
        {
            var m = new Mesh { name = name };
            m.MarkDynamic();
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            uvs = new List<Vector2>(uv);
            return m;
        }

        // ------------------------------------------------------------------ props (fallbacks when the Blender kit is absent)

        public static Mesh Bed(bool bay)
        {
            var k = new MeshKit();
            k.Box(new Vector3(0, 0, 0), new Vector3(1.0f, 0.55f, 2.1f), 0, Pal.Panel, Pal.Mattress);
            k.Box(new Vector3(0, 0.55f, 0.15f), new Vector3(0.95f, 0.12f, 1.6f), 0, Pal.Sheet);
            k.Box(new Vector3(0, 0.55f, 0.85f), new Vector3(0.7f, 0.18f, 0.35f), 0, Pal.White);    // pillow
            k.Box(new Vector3(0, 0, 1.05f), new Vector3(1.05f, 1.0f, 0.08f), 0, Pal.Panel);       // headboard
            if (bay)
            {
                k.Box(new Vector3(-0.8f, 0, 0), new Vector3(0.05f, 2.0f, 2.2f), 0, Pal.Curtain);    // curtain
                k.Box(new Vector3(0.75f, 0, 0.8f), new Vector3(0.35f, 1.2f, 0.35f), 0, Pal.Machine);  // monitor stand
                k.Box(new Vector3(0.75f, 1.2f, 0.8f), new Vector3(0.45f, 0.35f, 0.1f), 0, Pal.Screen);
            }
            return k.Build(bay ? "bay" : "bed");
        }

        public static Mesh Desk() => DeskKit().Build("desk");

        public static MeshKit DeskKit()
        {
            var k = new MeshKit();
            k.Box(Vector3.zero, new Vector3(2.6f, 1.05f, 0.8f), 0, Pal.Brand, Pal.Wood);
            k.Box(new Vector3(-0.6f, 1.05f, 0.1f), new Vector3(0.5f, 0.35f, 0.08f), 0, Pal.Screen);
            return k;
        }

        public static Mesh Chairs(int n) => ChairsKit(n).Build("chairs" + n);

        public static MeshKit ChairsKit(int n)
        {
            var k = new MeshKit();
            for (int i = 0; i < n; i++)
            {
                float x = (i - (n - 1) * 0.5f) * 0.9f;
                k.Box(new Vector3(x, 0, 0), new Vector3(0.7f, 0.45f, 0.65f), 0, Pal.Machine, Pal.Brand);
                k.Box(new Vector3(x, 0.45f, 0.28f), new Vector3(0.7f, 0.5f, 0.1f), 0, Pal.Brand);
            }
            return k;
        }

        public static Mesh Machine(string kind)
        {
            var k = new MeshKit();
            switch (kind)
            {
                case "ct":
                    k.Box(new Vector3(0, 0, 0.6f), new Vector3(2.4f, 2.2f, 0.9f), 0, Pal.White, Pal.Panel);
                    k.Box(new Vector3(0, 0.9f, 0.08f), new Vector3(0.9f, 0.9f, 0.1f), 0, Pal.Window);
                    k.Box(new Vector3(0, 0, -0.8f), new Vector3(0.7f, 0.75f, 2.0f), 0, Pal.Machine, Pal.Sheet);
                    break;
                case "lab":
                    k.Box(Vector3.zero, new Vector3(3.0f, 0.95f, 0.8f), 0, Pal.Panel, Pal.White);
                    for (int i = 0; i < 3; i++) k.Box(new Vector3(-1 + i, 0.95f, 0), new Vector3(0.45f, 0.5f, 0.45f), 0, Pal.Machine, Pal.Screen);
                    break;
                case "or":
                    k.Box(Vector3.zero, new Vector3(0.9f, 0.9f, 2.0f), 0, Pal.Machine, Pal.Curtain);
                    k.Box(new Vector3(0, 2.4f, 0), new Vector3(1.2f, 0.2f, 1.2f), 0, Pal.Panel, Pal.Screen);   // lamp
                    k.Box(new Vector3(1.2f, 0, 0.6f), new Vector3(0.6f, 1.3f, 0.5f), 0, Pal.Machine, Pal.Screen);
                    break;
                default: // xray
                    k.Box(new Vector3(0, 0, 0), new Vector3(0.8f, 0.8f, 2.0f), 0, Pal.Machine, Pal.Sheet);
                    k.Box(new Vector3(0, 0, 1.2f), new Vector3(0.35f, 2.4f, 0.35f), 0, Pal.Panel);
                    k.Box(new Vector3(0, 1.8f, 0.6f), new Vector3(0.6f, 0.4f, 1.2f), 0, Pal.Panel, Pal.Brand);
                    break;
            }
            return k.Build("machine_" + kind);
        }

        public static Mesh Ambulance()
        {
            var k = new MeshKit();
            k.Box(new Vector3(0, 0.35f, 1.6f), new Vector3(2.0f, 1.6f, 1.3f), 0, Pal.White);
            k.Box(new Vector3(0, 0.35f, -0.6f), new Vector3(2.1f, 2.2f, 3.2f), 0, Pal.White);
            k.Box(new Vector3(0, 1.2f, -0.6f), new Vector3(2.14f, 0.35f, 3.0f), 0, Pal.TagRed);
            k.Box(new Vector3(0, 1.3f, 2.27f), new Vector3(1.7f, 0.6f, 0.05f), 0, Pal.Window);
            k.Box(new Vector3(0, 2.55f, 0.6f), new Vector3(1.2f, 0.18f, 0.3f), 0, Pal.Siren);
            for (int i = -1; i <= 1; i += 2) for (int j = -1; j <= 1; j += 2) k.Box(new Vector3(i * 1.0f, 0, j * 1.3f), new Vector3(0.35f, 0.8f, 0.8f), 0, Pal.Rubber);
            return k.Build("ambulance");
        }

        /// <summary>The chunky canonical figure (people), ~1.8 m; drawn ×1.4 so it reads at campus zoom. shirt = role colour.</summary>
        public static Mesh Figure(int shirt, int legs = Pal.Trousers)
        {
            var k = new MeshKit();
            k.Box(new Vector3(-0.17f, 0, 0), new Vector3(0.26f, 0.8f, 0.3f), 0, legs);
            k.Box(new Vector3(0.17f, 0, 0), new Vector3(0.26f, 0.8f, 0.3f), 0, legs);
            k.Box(new Vector3(0, 0.8f, 0), new Vector3(0.7f, 0.65f, 0.4f), 0, shirt);
            k.Box(new Vector3(-0.45f, 0.85f, 0), new Vector3(0.18f, 0.55f, 0.22f), 0, shirt);
            k.Box(new Vector3(0.45f, 0.85f, 0), new Vector3(0.18f, 0.55f, 0.22f), 0, shirt);
            k.Box(new Vector3(0, 1.45f, 0), new Vector3(0.42f, 0.4f, 0.42f), 0, Pal.Skin);
            return k.Build("figure_" + shirt);
        }
    }
}
