// Docket Day — the diorama's procedural geometry. Everything in the world shares ONE material (URP Simple Lit on the
// 8×8 palette texture Resources/materials/palette.png): a model picks colours by pointing its UVs at palette swatches, so
// the SRP batcher draws the office tower, the street and the courthouse with a handful of batches (fleet §10: Low ≤ 150
// draws, the plan targets ≤ 120). Builders merge many boxes into one mesh per floor; nothing here runs per frame.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DocketDay.World
{
    /// <summary>Palette swatch indices (Tools/make_palette.py writes the same order).</summary>
    public static class Pal
    {
        public const int Grass = 0, GrassDark = 1, Road = 2, RoadLine = 3, Concrete = 4, WallCream = 5, WallBrick = 6, WallBlue = 7,
            RoofRed = 8, RoofGrey = 9, Brand = 10, Wood = 11, Paper = 12, Teal = 13, Stone = 14, Window = 15, Carpet = 16,
            Tree = 17, Trunk = 18, Locked = 19, Dark = 20, Folder = 21, Metal = 22, Lamp = 23, Warning = 24, Amber = 25, Robe = 26,
            Gold = 27, Loft = 28, Classic = 29, Deco = 30, Sand = 31, SkinTone = 32, ShirtA = 33, ShirtB = 34, ShirtC = 35, ShirtD = 36, ShirtE = 37,
            Trousers = 38, White = 39;
        public const int Size = 8;

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
        public int Triangles => t.Count / 3;

        /// <summary>Placement applied to everything added (lets a procedural fallback land where the kit model would).</summary>
        public Matrix4x4 Xf = Matrix4x4.identity;
        bool HasXf => Xf != Matrix4x4.identity;

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int swatch)
        {
            if (HasXf) { a = Xf.MultiplyPoint3x4(a); b = Xf.MultiplyPoint3x4(b); c = Xf.MultiplyPoint3x4(c); d = Xf.MultiplyPoint3x4(d); }
            var nn = Vector3.Cross(b - a, d - a).normalized;
            int i = v.Count;
            var u = Pal.Uv(swatch);
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            for (int k = 0; k < 4; k++) { n.Add(nn); uv.Add(u); }
            t.Add(i); t.Add(i + 2); t.Add(i + 1);
            t.Add(i); t.Add(i + 3); t.Add(i + 2);
        }

        /// <summary>Axis box on the ground rotated by yaw (degrees). top swatch may differ (roofs).</summary>
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

        /// <summary>A flat strip on the ground from a to b (roads), y lifted a little to avoid z-fighting.</summary>
        public void Strip(Vector3 a, Vector3 b, float width, float y, int swatch)
        {
            var dir = (b - a); dir.y = 0;
            if (dir.sqrMagnitude < 1e-6f) return;
            var side = Vector3.Cross(Vector3.up, dir.normalized) * (width * 0.5f);
            a.y = b.y = y;
            Quad(a - side, b - side, b + side, a + side, swatch);
        }

        /// <summary>A flat disc (crossings, plazas) with segs segments.</summary>
        public void Disc(Vector3 c, float r, float y, int swatch, int segs = 10)
        {
            var u = Pal.Uv(swatch);
            int i0 = v.Count;
            var up = HasXf ? Xf.MultiplyVector(Vector3.up).normalized : Vector3.up;
            v.Add(Xf.MultiplyPoint3x4(new Vector3(c.x, y, c.z))); n.Add(up); uv.Add(u);
            for (int s = 0; s <= segs; s++)
            {
                float a = s * Mathf.PI * 2f / segs;
                v.Add(Xf.MultiplyPoint3x4(new Vector3(c.x + Mathf.Cos(a) * r, y, c.z + Mathf.Sin(a) * r))); n.Add(up); uv.Add(u);
            }
            for (int s = 0; s < segs; s++) { t.Add(i0); t.Add(i0 + 2 + s); t.Add(i0 + 1 + s); }
        }

        /// <summary>A low-poly tree (trunk + two stacked boxes).</summary>
        public void Tree(Vector3 at, float s, float yaw)
        {
            Box(at, new Vector3(0.6f, 2.2f, 0.6f) * s, yaw, Pal.Trunk);
            Box(at + Vector3.up * 2f * s, new Vector3(3.2f, 2.4f, 3.2f) * s, yaw + 15f, Pal.Tree);
            Box(at + Vector3.up * 4.2f * s, new Vector3(2f, 1.6f, 2f) * s, yaw + 40f, Pal.Tree);
        }

        /// <summary>Appends a readable mesh (a Blender kit prop on the palette UVs) under matrix m. remap = swatch pairs
        /// {from, to, from, to…}: faces painted with `from` take `to` (office styles recolour the walls / trim).
        /// Build-time only (allocates the source arrays once); never per frame.</summary>
        public bool Append(Mesh src, Matrix4x4 m, int[] remap = null)
        {
            if (src == null || !src.isReadable) return false;
            var sv = src.vertices; var sn = src.normals; var su = src.uv; var st = src.triangles;
            if (sv.Length == 0 || st.Length == 0) return false;
            int i0 = v.Count;
            Vector2[] from = null, to = null;
            if (remap != null && remap.Length >= 2)
            {
                from = new Vector2[remap.Length / 2]; to = new Vector2[remap.Length / 2];
                for (int k = 0; k < from.Length; k++) { from[k] = Pal.Uv(remap[2 * k]); to[k] = Pal.Uv(remap[2 * k + 1]); }
            }
            for (int k = 0; k < sv.Length; k++)
            {
                v.Add(m.MultiplyPoint3x4(sv[k]));
                n.Add(sn.Length == sv.Length ? m.MultiplyVector(sn[k]).normalized : Vector3.up);
                var u = su.Length == sv.Length ? su[k] : Pal.Uv(Pal.Concrete);
                if (from != null)
                    for (int r = 0; r < from.Length; r++)
                        if (Mathf.Abs(u.x - from[r].x) < 1e-3f && Mathf.Abs(u.y - from[r].y) < 1e-3f) { u = to[r]; break; }
                uv.Add(u);
            }
            bool mirror = m.determinant < 0;
            for (int k = 0; k < st.Length; k += 3)
            {
                t.Add(i0 + st[k]);
                if (mirror) { t.Add(i0 + st[k + 2]); t.Add(i0 + st[k + 1]); }
                else { t.Add(i0 + st[k + 1]); t.Add(i0 + st[k + 2]); }
            }
            return true;
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

        // ------------------------------------------------------------------ props (fallbacks when the Blender kit is absent)

        public static Mesh Desk() { var k = new MeshKit(); Desk(k); return k.Build("desk"); }

        public static void Desk(MeshKit k)
        {
            k.Box(Vector3.zero, new Vector3(2.4f, 1.0f, 1.2f), 0, Pal.Wood, Pal.Wood);
            k.Box(new Vector3(0, 1.0f, 0.3f), new Vector3(0.9f, 0.6f, 0.08f), 0, Pal.Dark);           // monitor
            k.Box(new Vector3(0.7f, 1.0f, -0.2f), new Vector3(0.5f, 0.12f, 0.4f), 0, Pal.Folder);   // file stack
            k.Box(new Vector3(0, 0, -1.0f), new Vector3(0.8f, 0.6f, 0.8f), 0, Pal.Brand);             // chair
        }

        public static Mesh Reception() { var k = new MeshKit(); Reception(k); return k.Build("reception"); }

        public static void Reception(MeshKit k)
        {
            k.Box(Vector3.zero, new Vector3(4.2f, 1.2f, 1.2f), 0, Pal.Brand, Pal.Wood);
            k.Box(new Vector3(-1.2f, 1.2f, 0), new Vector3(0.6f, 0.7f, 0.5f), 0, Pal.Lamp);
            k.Box(new Vector3(1f, 1.2f, 0.2f), new Vector3(0.9f, 0.55f, 0.08f), 0, Pal.Dark);
        }

        public static Mesh Bookshelf() { var k = new MeshKit(); Bookshelf(k); return k.Build("bookshelf"); }

        public static void Bookshelf(MeshKit k)
        {
            k.Box(Vector3.zero, new Vector3(3f, 3.4f, 0.8f), 0, Pal.Wood, Pal.Wood);
            for (int r = 0; r < 3; r++) k.Box(new Vector3(0, 0.3f + r * 1.05f, -0.42f), new Vector3(2.6f, 0.7f, 0.05f), 0, r % 2 == 0 ? Pal.Carpet : Pal.Classic);
        }

        public static Mesh Coffee() { var k = new MeshKit(); Coffee(k); return k.Build("coffee"); }

        public static void Coffee(MeshKit k)
        {
            k.Box(Vector3.zero, new Vector3(2f, 1.0f, 1f), 0, Pal.Wood, Pal.Concrete);
            k.Box(new Vector3(-0.4f, 1f, 0), new Vector3(0.7f, 0.9f, 0.6f), 0, Pal.Metal, Pal.Dark);
            k.Box(new Vector3(0.5f, 1f, 0), new Vector3(0.25f, 0.3f, 0.25f), 0, Pal.White);
        }

        public static Mesh Cabinet() { var k = new MeshKit(); Cabinet(k); return k.Build("cabinet"); }

        public static void Cabinet(MeshKit k)
        {
            for (int i = -1; i <= 1; i++) k.Box(new Vector3(i * 0.95f, 0, 0), new Vector3(0.85f, 2.2f, 0.9f), 0, Pal.Metal, Pal.Metal);
        }

        public static Mesh Conference() { var k = new MeshKit(); Conference(k); return k.Build("conference"); }

        public static void Conference(MeshKit k)
        {
            k.Box(Vector3.zero, new Vector3(4.6f, 0.95f, 2f), 0, Pal.Wood, Pal.Wood);
            for (int i = -1; i <= 1; i++) for (int s = -1; s <= 1; s += 2) k.Box(new Vector3(i * 1.4f, 0, s * 1.5f), new Vector3(0.7f, 0.6f, 0.7f), 0, Pal.Dark);
        }

        public static Mesh Boxes() { var k = new MeshKit(); Boxes(k); return k.Build("boxes"); }

        public static void Boxes(MeshKit k)
        {
            k.Box(Vector3.zero, new Vector3(1.2f, 0.8f, 0.9f), 10, Pal.Folder);
            k.Box(new Vector3(1.1f, 0, 0.2f), new Vector3(1f, 0.8f, 0.9f), -8, Pal.Sand);
            k.Box(new Vector3(0.5f, 0.8f, 0.1f), new Vector3(1f, 0.7f, 0.8f), 4, Pal.Folder);
        }

        public static Mesh Board() { var k = new MeshKit(); Board(k); return k.Build("board"); }

        public static void Board(MeshKit k)
        {
            k.Box(Vector3.zero, new Vector3(3.2f, 2.6f, 0.2f), 0, Pal.White);
            k.Box(new Vector3(-0.6f, 1.2f, -0.12f), new Vector3(0.8f, 0.6f, 0.02f), 0, Pal.Folder);
            k.Box(new Vector3(0.7f, 1.6f, -0.12f), new Vector3(0.7f, 0.5f, 0.02f), 0, Pal.Warning);
        }

        public static Mesh Courthouse() { var k = new MeshKit(); Courthouse(k); return k.Build("courthouse"); }

        public static void Courthouse(MeshKit k)
        {
            k.Box(Vector3.zero, new Vector3(26f, 1.2f, 4f), 0, Pal.Stone);                                  // steps
            k.Box(new Vector3(0, 0, 3.5f), new Vector3(24f, 2.0f, 3f), 0, Pal.Stone);
            k.Box(new Vector3(0, 2f, 9f), new Vector3(24f, 11f, 12f), 0, Pal.Stone, Pal.RoofGrey);          // hall
            for (int i = 0; i < 6; i++) k.Box(new Vector3(-10f + i * 4f, 2f, 3.6f), new Vector3(1.4f, 10f, 1.4f), 0, Pal.White);   // columns
            k.Box(new Vector3(0, 12f, 4f), new Vector3(25f, 1.4f, 3.2f), 0, Pal.Stone);                      // architrave
            k.Box(new Vector3(0, 13.4f, 4.5f), new Vector3(20f, 2.4f, 2.4f), 0, Pal.Stone, Pal.RoofGrey);    // pediment block
            k.Box(new Vector3(0, 2f, 2.9f), new Vector3(3.2f, 5f, 0.2f), 0, Pal.Wood);                       // door
        }

        public static Mesh Lamp() { var k = new MeshKit(); Lamp(k); return k.Build("lamp"); }

        public static void Lamp(MeshKit k)
        {
            k.Box(Vector3.zero, new Vector3(0.25f, 4.2f, 0.25f), 0, Pal.Dark);
            k.Box(new Vector3(0, 4.2f, 0), new Vector3(0.7f, 0.6f, 0.7f), 0, Pal.Amber);
        }

        public static Mesh Bench() { var k = new MeshKit(); Bench(k); return k.Build("bench"); }

        public static void Bench(MeshKit k)
        {
            k.Box(Vector3.zero, new Vector3(2.2f, 0.5f, 0.7f), 0, Pal.Wood);
            k.Box(new Vector3(0, 0.5f, 0.3f), new Vector3(2.2f, 0.7f, 0.12f), 0, Pal.Wood);
        }

        /// <summary>The chunky canonical person (staff + clients), ~2.6 units tall so it reads at diorama zoom. shirt = role colour.
        /// folder: carries a manila folder (staff) instead of nothing (clients).</summary>
        public static Mesh Figure(int shirt, bool folder = true)
        {
            var k = new MeshKit();
            k.Box(new Vector3(-0.35f, 0, 0), new Vector3(0.5f, 1.1f, 0.6f), 0, Pal.Trousers);
            k.Box(new Vector3(0.35f, 0, 0), new Vector3(0.5f, 1.1f, 0.6f), 0, Pal.Trousers);
            k.Box(new Vector3(0, 1.1f, 0), new Vector3(1.4f, 1.1f, 0.8f), 0, shirt);
            k.Box(new Vector3(0, 2.2f, 0), new Vector3(0.8f, 0.7f, 0.8f), 0, Pal.SkinTone);
            if (folder) k.Box(new Vector3(0.95f, 0.8f, 0), new Vector3(0.2f, 0.8f, 0.7f), 0, Pal.Folder);
            return k.Build("figure_" + shirt + (folder ? "_f" : ""));
        }
    }
}
