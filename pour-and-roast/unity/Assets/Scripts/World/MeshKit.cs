// Pour & Roast — the diorama's procedural geometry. Everything in the world shares ONE material (URP Simple Lit on the
// 8×8 palette texture Resources/materials/palette.png): a model picks colours by pointing its UVs at palette swatches, so
// the SRP batcher draws the whole town with a handful of batches (fleet §10: Low ≤ 150 draws, the plan targets ≤ 120).
// Builders merge many boxes into one mesh per district; nothing here runs per frame.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PourAndRoast.World
{
    /// <summary>Palette swatch indices (Tools/make_palette.py writes the same order).</summary>
    public static class Pal
    {
        public const int Grass = 0, GrassDark = 1, Road = 2, RoadLine = 3, Concrete = 4, WallCream = 5, WallTerracotta = 6, WallBlue = 7,
            RoofRed = 8, RoofGrey = 9, Brand = 10, Panel = 11, TruckWhite = 12, TruckGreen = 13, Depot = 14, Water = 15, Sand = 16,
            Tree = 17, Trunk = 18, Locked = 19, Window = 20, Pet = 21, Alu = 22, Glass = 23, Warning = 24, Yellow = 25, Litter = 26,
            Gold = 27, Sunset = 28, Forest = 29, Tropic = 30, Retro = 31, SkinTone = 32, ShirtA = 33, ShirtB = 34, ShirtC = 35, ShirtD = 36, ShirtE = 37, Trousers = 38;
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

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int swatch)
        {
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

        public static Mesh Machine(bool big)
        {
            var k = new MeshKit();
            float h = big ? 5.2f : 4.2f, w = big ? 4.4f : 3.2f;
            k.Box(Vector3.zero, new Vector3(w, h, 2.4f), 0, Pal.Brand, Pal.Brand);
            k.Box(new Vector3(0, h * 0.45f, -1.25f), new Vector3(w * 0.7f, h * 0.35f, 0.1f), 0, Pal.Panel);
            k.Box(new Vector3(0, h * 0.82f, -1.25f), new Vector3(w * 0.45f, h * 0.12f, 0.1f), 0, Pal.Window);
            k.Box(new Vector3(0, h, 0), new Vector3(w + 0.4f, 0.35f, 2.8f), 0, Pal.Panel);
            return k.Build(big ? "machine_m" : "machine_s");
        }

        public static Mesh Shop()
        {
            var k = new MeshKit();
            k.Box(Vector3.zero, new Vector3(6f, 4f, 5f), 0, Pal.WallCream, Pal.RoofRed);
            k.Box(new Vector3(0, 2.6f, -2.7f), new Vector3(6.4f, 0.3f, 1.6f), 0, Pal.Brand);
            k.Box(new Vector3(-1.5f, 0, -3f), new Vector3(1.4f, 1.2f, 1f), 0, Pal.Pet);
            return k.Build("shop");
        }

        public static Mesh Horeca()
        {
            var k = new MeshKit();
            k.Box(Vector3.zero, new Vector3(7f, 4.5f, 6f), 0, Pal.WallTerracotta, Pal.RoofGrey);
            for (int i = -1; i <= 1; i += 2)
            {
                k.Box(new Vector3(i * 2.4f, 0, -5f), new Vector3(0.2f, 2.6f, 0.2f), 0, Pal.Trunk);
                k.Box(new Vector3(i * 2.4f, 2.6f, -5f), new Vector3(2.6f, 0.25f, 2.6f), 45, Pal.Sunset);
            }
            k.Box(new Vector3(3.6f, 0, 1f), new Vector3(1.2f, 1.4f, 1.6f), 0, Pal.Glass);
            return k.Build("horeca");
        }

        public static Mesh Truck(string type)
        {
            var k = new MeshKit();
            switch (type)
            {
                case "bike":
                    k.Box(new Vector3(0, 0.5f, 0.8f), new Vector3(1.4f, 1.3f, 1.8f), 0, Pal.TruckWhite);
                    k.Box(new Vector3(0, 0, -1.2f), new Vector3(0.3f, 1.6f, 0.3f), 0, Pal.Window);
                    k.Box(new Vector3(0, 0, 0), new Vector3(0.25f, 0.6f, 3.2f), 0, Pal.Window);
                    break;
                case "compactor":
                    k.Box(new Vector3(0, 0.6f, 2.6f), new Vector3(2.6f, 2.6f, 1.8f), 0, Pal.TruckWhite);
                    k.Box(new Vector3(0, 0.6f, -0.8f), new Vector3(2.8f, 3.2f, 5f), 0, Pal.TruckGreen);
                    k.Box(new Vector3(0, 1.6f, 3.52f), new Vector3(2.2f, 1f, 0.05f), 0, Pal.Window);
                    for (int i = -1; i <= 1; i += 2) for (int j = -1; j <= 1; j += 2) k.Box(new Vector3(i * 1.3f, 0, j * 2f), new Vector3(0.5f, 1.1f, 1.1f), 0, Pal.Window);
                    break;
                default:
                    k.Box(new Vector3(0, 0.5f, 1.6f), new Vector3(2.2f, 2f, 1.4f), 0, Pal.TruckWhite);
                    k.Box(new Vector3(0, 0.5f, -0.6f), new Vector3(2.3f, 2.5f, 3.2f), 0, Pal.TruckWhite);
                    k.Box(new Vector3(0, 1.4f, 2.32f), new Vector3(1.9f, 0.8f, 0.05f), 0, Pal.Window);
                    k.Box(new Vector3(0, 1.2f, -0.6f), new Vector3(2.35f, 0.5f, 3f), 0, Pal.Brand);
                    for (int i = -1; i <= 1; i += 2) for (int j = -1; j <= 1; j += 2) k.Box(new Vector3(i * 1.1f, 0, j * 1.4f), new Vector3(0.4f, 0.9f, 0.9f), 0, Pal.Window);
                    break;
            }
            return k.Build("truck_" + type);
        }

        /// <summary>The chunky canonical walker (crowd), ~2.6 units tall so it reads at map zoom. shirt = archetype colour.</summary>
        public static Mesh Figure(int shirt)
        {
            var k = new MeshKit();
            k.Box(new Vector3(-0.35f, 0, 0), new Vector3(0.5f, 1.1f, 0.6f), 0, Pal.Trousers);
            k.Box(new Vector3(0.35f, 0, 0), new Vector3(0.5f, 1.1f, 0.6f), 0, Pal.Trousers);
            k.Box(new Vector3(0, 1.1f, 0), new Vector3(1.4f, 1.1f, 0.8f), 0, shirt);
            k.Box(new Vector3(0, 2.2f, 0), new Vector3(0.8f, 0.7f, 0.8f), 0, Pal.SkinTone);
            k.Box(new Vector3(0.95f, 0.7f, 0), new Vector3(0.6f, 0.8f, 0.6f), 0, Pal.Pet);   // the bottle bag
            return k.Build("figure_" + shirt);
        }
    }
}
