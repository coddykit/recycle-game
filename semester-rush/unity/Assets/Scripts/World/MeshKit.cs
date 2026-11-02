// Semester Rush — the diorama's procedural geometry. Everything in the world shares ONE material (URP Simple Lit on the
// 8×8 palette texture Resources/materials/palette.png): a model picks colours by pointing its UVs at palette swatches, so
// the SRP batcher draws the whole campus with a handful of batches (fleet §10: Low ≤ 150 draws, the plan targets ≤ 120).
// Builders merge many boxes into one mesh per zone; nothing here runs per frame. The building props below are the
// fallbacks when the Blender kit (Resources/kit/*.fbx, tools/art/kit.py) is missing.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SemesterRush.World
{
    /// <summary>Palette swatch indices (Tools/make_palette.py writes the same order; tools/art/kit.py uses them too).</summary>
    public static class Pal
    {
        public const int Grass = 0, GrassDark = 1, Path = 2, PathLine = 3, Stone = 4, WallCream = 5, Brick = 6, WallBlue = 7,
            RoofRed = 8, Slate = 9, Brand = 10, Trim = 11, White = 12, Ivy = 13, WindowLight = 14, Water = 15, Sand = 16,
            Tree = 17, Trunk = 18, Locked = 19, Window = 20, HeatOk = 21, HeatBusy = 22, HeatJam = 23, Warning = 24, Yellow = 25, Wood = 26,
            Gold = 27, ThemeIvy = 28, ThemeModern = 29, ThemeDesert = 30, Copper = 31, SkinTone = 32, Trousers = 38;
        /// <summary>Faculty colours in data order (business, literature, law, philosophy, physics, chemistry, maths).</summary>
        public static readonly int[] Faculty = { 33, 34, 35, 36, 37, 40, 41 };
        public const int Size = 8;

        public static Vector2 Uv(int swatch)
        {
            int x = swatch % Size, y = swatch / Size;
            return new Vector2((x + 0.5f) / Size, 1f - (y + 0.5f) / Size);
        }
    }

    /// <summary>Raw geometry of a Blender dressing prop (Resources/kit/merge, readable import) for merging into a zone /
    /// path mesh. Loaded once per name; null = missing or not readable (the caller keeps its procedural fallback).</summary>
    public sealed class KitGeo
    {
        public Vector3[] V, N;
        public Vector2[] Uv;
        public int[] T;
        public int Tris => T.Length / 3;

        static readonly Dictionary<string, KitGeo> cache = new Dictionary<string, KitGeo>();

        public static KitGeo Load(string name)
        {
            if (cache.TryGetValue(name, out var g)) return g;
            var prefab = Resources.Load<GameObject>("kit/merge/" + name);
            var mf = prefab != null ? prefab.GetComponentInChildren<MeshFilter>() : null;
            var mesh = mf != null ? mf.sharedMesh : null;
            if (mesh != null && mesh.isReadable)
            {
                // relative to the prefab root: CampusWorld places kit props with their root at identity (Prop()).
                var m = prefab.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                var vs = mesh.vertices;
                var ns = mesh.normals;
                var us = mesh.uv;
                if (ns.Length != vs.Length) ns = new Vector3[vs.Length];
                if (us.Length != vs.Length) { us = new Vector2[vs.Length]; for (int i = 0; i < us.Length; i++) us[i] = Pal.Uv(Pal.Stone); }
                for (int i = 0; i < vs.Length; i++) { vs[i] = m.MultiplyPoint3x4(vs[i]); ns[i] = m.MultiplyVector(ns[i]).normalized; }
                g = new KitGeo { V = vs, N = ns, Uv = us, T = mesh.triangles };
            }
            cache[name] = g;
            return g;
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

        /// <summary>A gable roof over a box footprint (two slopes + two triangles).</summary>
        public void Gable(Vector3 baseCenter, float w, float d, float h, int swatch)
        {
            float x = w * 0.5f, z = d * 0.5f;
            var a = baseCenter + new Vector3(-x, 0, -z); var b = baseCenter + new Vector3(x, 0, -z);
            var c = baseCenter + new Vector3(x, 0, z); var e = baseCenter + new Vector3(-x, 0, z);
            var r0 = baseCenter + new Vector3(-x, h, 0); var r1 = baseCenter + new Vector3(x, h, 0);
            Quad(a, r0, r1, b, swatch);
            Quad(c, r1, r0, e, swatch);
            Tri(b, r1, c, swatch);
            Tri(e, r0, a, swatch);
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 c, int swatch)
        {
            var nn = Vector3.Cross(b - a, c - a).normalized;
            int i = v.Count;
            var u = Pal.Uv(swatch);
            v.Add(a); v.Add(b); v.Add(c);
            for (int k = 0; k < 3; k++) { n.Add(nn); uv.Add(u); }
            t.Add(i); t.Add(i + 1); t.Add(i + 2);   // clockwise = Unity front face (fleet §0 fix from bottle-route)
        }

        /// <summary>A flat strip on the ground from a to b (paths), y lifted a little to avoid z-fighting.</summary>
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
            v.Add(new Vector3(c.x, y, c.z)); n.Add(Vector3.up); uv.Add(u);
            for (int s = 0; s <= segs; s++)
            {
                float a = s * Mathf.PI * 2f / segs;
                v.Add(new Vector3(c.x + Mathf.Cos(a) * r, y, c.z + Mathf.Sin(a) * r)); n.Add(Vector3.up); uv.Add(u);
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

        /// <summary>Merges a Blender dressing prop at pos, turned by yaw (degrees), uniformly scaled.</summary>
        public void Add(KitGeo g, Vector3 pos, float yaw, float scale)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            int i0 = v.Count;
            for (int i = 0; i < g.V.Length; i++) { v.Add(pos + q * (g.V[i] * scale)); n.Add(q * g.N[i]); uv.Add(g.Uv[i]); }
            for (int i = 0; i < g.T.Length; i++) t.Add(i0 + g.T[i]);
        }

        /// <summary>Merges another (procedural) kit at pos, turned by yaw, uniformly scaled.</summary>
        public void Add(MeshKit o, Vector3 pos, float yaw, float scale)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            int i0 = v.Count;
            for (int i = 0; i < o.v.Count; i++) { v.Add(pos + q * (o.v[i] * scale)); n.Add(q * o.n[i]); uv.Add(o.uv[i]); }
            for (int i = 0; i < o.t.Count; i++) t.Add(i0 + o.t[i]);
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

        /// <summary>A brick hall: w × d footprint, h tall, windows in rows, a pediment entrance (front = -z).</summary>
        static void Hall(MeshKit k, float w, float d, float h, int wall, int roof)
        {
            k.Box(Vector3.zero, new Vector3(w, h, d), 0, wall, Pal.Slate);
            k.Gable(new Vector3(0, h, 0), w + 0.6f, d + 0.6f, h * 0.35f, roof);
            for (float x = -w * 0.5f + 1.6f; x < w * 0.5f - 1f; x += 2.4f)
                for (float y = 1.4f; y < h - 1f; y += 2.6f) k.Box(new Vector3(x, y, -d * 0.5f - 0.05f), new Vector3(1.1f, 1.5f, 0.1f), 0, Pal.Window);
            k.Box(new Vector3(0, 0, -d * 0.5f - 0.8f), new Vector3(w * 0.3f, 0.5f, 1.6f), 0, Pal.Stone);
            k.Box(new Vector3(0, 0.5f, -d * 0.5f - 0.3f), new Vector3(w * 0.22f, h * 0.55f, 0.6f), 0, Pal.Trim);
        }

        public static Mesh Building(string def)
        {
            var k = new MeshKit();
            switch (def)
            {
                case "hall_s": Hall(k, 10, 8, 6, Pal.Brick, Pal.RoofRed); break;
                case "hall_m": Hall(k, 14, 10, 8, Pal.Brick, Pal.RoofRed); break;
                case "hall_l":
                    Hall(k, 18, 13, 9, Pal.WallCream, Pal.Slate);
                    k.Box(new Vector3(0, 9, 0), new Vector3(5, 3, 5), 0, Pal.Trim);
                    break;
                case "lab":
                    k.Box(Vector3.zero, new Vector3(14, 7, 10), 0, Pal.WallBlue, Pal.Slate);
                    for (float x = -5.5f; x <= 5.5f; x += 2.2f) k.Box(new Vector3(x, 2, -5.05f), new Vector3(1.6f, 3.6f, 0.1f), 0, Pal.WindowLight);
                    k.Box(new Vector3(4, 7, 2), new Vector3(1.2f, 3.5f, 1.2f), 0, Pal.Stone);
                    break;
                case "library":
                    Hall(k, 13, 10, 7, Pal.WallCream, Pal.Slate);
                    for (float x = -4.5f; x <= 4.5f; x += 3f) k.Box(new Vector3(x, 0, -5.8f), new Vector3(0.8f, 6.5f, 0.8f), 0, Pal.Trim);
                    break;
                case "canteen":
                    k.Box(Vector3.zero, new Vector3(12, 5, 9), 0, Pal.WallCream, Pal.RoofRed);
                    k.Box(new Vector3(0, 3.6f, -5.2f), new Vector3(12.4f, 0.3f, 1.6f), 0, Pal.Brand);
                    k.Box(new Vector3(0, 1, -4.55f), new Vector3(9, 2.4f, 0.1f), 0, Pal.WindowLight);
                    break;
                case "dorm":
                    k.Box(Vector3.zero, new Vector3(9, 12, 9), 0, Pal.Brick, Pal.Slate);
                    for (float y = 1.5f; y < 11; y += 2.4f)
                        for (float x = -3f; x <= 3f; x += 2f) k.Box(new Vector3(x, y, -4.55f), new Vector3(1f, 1.3f, 0.1f), 0, Pal.Window);
                    break;
                case "admin":
                    Hall(k, 11, 9, 7, Pal.WallCream, Pal.Slate);
                    k.Box(new Vector3(0, 9.2f, 0), new Vector3(2.4f, 4f, 2.4f), 0, Pal.Trim, Pal.Copper);
                    break;
                default: k.Box(Vector3.zero, new Vector3(8, 5, 8), 0, Pal.WallCream); break;
            }
            return k.Build("b_" + def);
        }

        /// <summary>Scaffolding on a plot under construction.</summary>
        public static Mesh Scaffold()
        {
            var k = new MeshKit();
            k.Box(Vector3.zero, new Vector3(12, 0.4f, 9), 0, Pal.Stone);
            for (int i = -1; i <= 1; i += 2) for (int j = -1; j <= 1; j += 2) k.Box(new Vector3(i * 5.5f, 0, j * 4f), new Vector3(0.3f, 7, 0.3f), 0, Pal.Yellow);
            k.Box(new Vector3(0, 3.5f, -4f), new Vector3(11.4f, 0.3f, 0.3f), 0, Pal.Yellow);
            k.Box(new Vector3(0, 6.8f, -4f), new Vector3(11.4f, 0.3f, 0.3f), 0, Pal.Yellow);
            k.Box(new Vector3(-2, 0.4f, 0), new Vector3(5, 3, 4), 0, Pal.Brick);
            return k.Build("scaffold");
        }

        /// <summary>An empty plot (low stone pad with corner posts).</summary>
        public static Mesh Plot()
        {
            var k = new MeshKit();
            k.Box(Vector3.zero, new Vector3(11, 0.25f, 8), 0, Pal.Sand);
            for (int i = -1; i <= 1; i += 2) for (int j = -1; j <= 1; j += 2) k.Box(new Vector3(i * 5.3f, 0, j * 3.8f), new Vector3(0.25f, 1.2f, 0.25f), 0, Pal.Wood);
            return k.Build("plot");
        }

        /// <summary>An iron gate in a hedge: open = the bars swing aside.</summary>
        public static Mesh Gate(bool open) => GateKit(open).Build(open ? "gate_open" : "gate_closed");

        /// <summary>The gate's procedural geometry (Low tier, or when kit/merge/gate_* is missing); merged into the paths.</summary>
        public static MeshKit GateKit(bool open)
        {
            var k = new MeshKit();
            k.Box(new Vector3(-4.5f, 0, 0), new Vector3(5, 1.6f, 1.2f), 0, Pal.Ivy);
            k.Box(new Vector3(4.5f, 0, 0), new Vector3(5, 1.6f, 1.2f), 0, Pal.Ivy);
            k.Box(new Vector3(-2, 0, 0), new Vector3(0.6f, 3.2f, 0.6f), 0, Pal.Stone);
            k.Box(new Vector3(2, 0, 0), new Vector3(0.6f, 3.2f, 0.6f), 0, Pal.Stone);
            if (open)
            {
                k.Box(new Vector3(-1.9f, 0.2f, -1.4f), new Vector3(0.12f, 2.4f, 2.6f), 0, Pal.Window);
                k.Box(new Vector3(1.9f, 0.2f, -1.4f), new Vector3(0.12f, 2.4f, 2.6f), 0, Pal.Window);
            }
            else k.Box(new Vector3(0, 0.2f, 0), new Vector3(3.4f, 2.4f, 0.12f), 0, Pal.Window);
            return k;
        }

        /// <summary>The bus stop at the entrance (commuters arrive here).</summary>
        public static Mesh BusStop()
        {
            var k = new MeshKit();
            k.Box(Vector3.zero, new Vector3(6, 0.2f, 2.4f), 0, Pal.Stone);
            k.Box(new Vector3(-2.7f, 0, 0.8f), new Vector3(0.2f, 2.6f, 0.2f), 0, Pal.Slate);
            k.Box(new Vector3(2.7f, 0, 0.8f), new Vector3(0.2f, 2.6f, 0.2f), 0, Pal.Slate);
            k.Box(new Vector3(0, 2.6f, 0.2f), new Vector3(6.2f, 0.2f, 2.2f), 0, Pal.Brand);
            k.Box(new Vector3(0, 0.2f, 1.0f), new Vector3(5.4f, 2.2f, 0.1f), 0, Pal.WindowLight);
            k.Box(new Vector3(-6, 0, -2), new Vector3(7, 2.6f, 2.6f), 0, Pal.Yellow);   // a parked bus front
            return k.Build("bus_stop");
        }

        /// <summary>The chunky canonical student (bell-change crowd), ~2.6 units tall. shirt = faculty colour.</summary>
        public static Mesh Figure(int shirt)
        {
            var k = new MeshKit();
            k.Box(new Vector3(-0.35f, 0, 0), new Vector3(0.5f, 1.1f, 0.6f), 0, Pal.Trousers);
            k.Box(new Vector3(0.35f, 0, 0), new Vector3(0.5f, 1.1f, 0.6f), 0, Pal.Trousers);
            k.Box(new Vector3(0, 1.1f, 0), new Vector3(1.4f, 1.1f, 0.8f), 0, shirt);
            k.Box(new Vector3(0, 2.2f, 0), new Vector3(0.8f, 0.7f, 0.8f), 0, Pal.SkinTone);
            k.Box(new Vector3(0, 1.2f, 0.6f), new Vector3(1f, 0.9f, 0.45f), 0, Pal.Brand);   // backpack
            return k.Build("figure_" + shirt);
        }

        /// <summary>A flat dot for the far crowd (one instanced draw per colour).</summary>
        public static Mesh Dot(int swatch, float r)
        {
            var k = new MeshKit();
            k.Disc(Vector3.zero, r, 0.3f, swatch, 6);
            return k.Build("dot_" + swatch);
        }

        /// <summary>A unit strip along +z (length 1, width 1) for the heat ribbons (scaled per path).</summary>
        public static Mesh Ribbon(int swatch)
        {
            var k = new MeshKit();
            k.Quad(new Vector3(-0.5f, 0.25f, 0), new Vector3(-0.5f, 0.25f, 1), new Vector3(0.5f, 0.25f, 1), new Vector3(0.5f, 0.25f, 0), swatch);
            return k.Build("ribbon_" + swatch);
        }
    }
}
