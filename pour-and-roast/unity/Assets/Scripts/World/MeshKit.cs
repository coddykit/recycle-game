// Pour & Roast — the diorama's procedural geometry. Everything in the cafe shares ONE material (URP Simple Lit on the
// 8×8 palette texture Resources/materials/palette.png): a model picks colours by pointing its UVs at palette swatches, so
// the SRP batcher draws the whole venue with a handful of batches (fleet §10: Low ≤ 150 draws, the plan targets ≤ 120).
// Builders merge many boxes into one mesh; nothing here runs per frame. The props are fallbacks for the Blender kit
// (Resources/kit/*.fbx, tools/art/kit.py builds the same shapes with bevels).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PourAndRoast.World
{
    /// <summary>Palette swatch indices (Tools/make_palette.py writes the same order).</summary>
    public static class Pal
    {
        public const int FloorWood = 0, FloorDark = 1, Quay = 2, QuayDark = 3, WallCream = 4, WallSage = 5, WallBrick = 6, Trim = 7,
            CounterTop = 8, Walnut = 9, Brand = 10, Steel = 11, SteelDark = 12, Black = 13, Copper = 14, Water = 15,
            AwningCream = 16, Teal = 17, Plant = 18, PlantDark = 19, Window = 20, Coffee = 21, Milk = 22, GreenBean = 23,
            Warning = 24, Gold = 25, Sack = 26, Pastry = 27, Chalk = 28, Nordic = 29, Deco = 30, DecoGold = 31,
            SkinTone = 32, ShirtA = 33, ShirtB = 34, ShirtC = 35, ShirtD = 36, ShirtE = 37, Trousers = 38, Apron = 39,
            SkinTone2 = 40, Hair = 41, Locked = 42, Sea = 43, ApronWine = 44, Cup = 45, Slate = 46, White = 47;
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

        /// <summary>Axis box standing on bottomCenter, rotated by yaw (degrees). The top swatch may differ.</summary>
        public void Box(Vector3 bottomCenter, Vector3 size, float yaw, int side, int top = -1, bool bottom = false)
        {
            if (top < 0) top = side;
            var q = Quaternion.Euler(0, yaw, 0);
            float x = size.x * 0.5f, z = size.z * 0.5f, h = size.y;
            Vector3 P(float px, float py, float pz) => bottomCenter + q * new Vector3(px, py, pz);
            var p000 = P(-x, 0, -z); var p100 = P(x, 0, -z); var p110 = P(x, h, -z); var p010 = P(-x, h, -z);
            var p001 = P(-x, 0, z); var p101 = P(x, 0, z); var p111 = P(x, h, z); var p011 = P(-x, h, z);
            Quad(p000, p010, p110, p100, side);   // front (-z, faces the camera)
            Quad(p101, p111, p011, p001, side);   // back
            Quad(p001, p011, p010, p000, side);   // left
            Quad(p100, p110, p111, p101, side);   // right
            Quad(p010, p011, p111, p110, top);    // top
            if (bottom) Quad(p000, p100, p101, p001, side);
        }

        /// <summary>A low cylinder approximated by an n-gon prism (cups, pots, the roaster drum, wheels).</summary>
        public void Cyl(Vector3 bottomCenter, float r, float h, int side, int top = -1, int segs = 8)
        {
            if (top < 0) top = side;
            var ut = Pal.Uv(top);
            int c0 = v.Count;
            v.Add(bottomCenter + Vector3.up * h); n.Add(Vector3.up); uv.Add(ut);
            for (int s = 0; s < segs; s++)
            {
                float a0 = s * Mathf.PI * 2f / segs, a1 = (s + 1) * Mathf.PI * 2f / segs;
                var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * r;
                var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * r;
                Quad(bottomCenter + d1, bottomCenter + d1 + Vector3.up * h, bottomCenter + d0 + Vector3.up * h, bottomCenter + d0, side);
            }
            int ring = v.Count;
            for (int s = 0; s <= segs; s++)
            {
                float a = s * Mathf.PI * 2f / segs;
                v.Add(bottomCenter + Vector3.up * h + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r); n.Add(Vector3.up); uv.Add(ut);
            }
            for (int s = 0; s < segs; s++) { t.Add(c0); t.Add(ring + s + 1); t.Add(ring + s); }
        }

        /// <summary>A flat rectangle on the ground (floors, rugs), y lifted a little against z-fighting.</summary>
        public void Floor(Vector3 center, Vector2 size, float y, int swatch)
        {
            float x = size.x * 0.5f, z = size.y * 0.5f;
            Quad(new Vector3(center.x - x, y, center.z - z), new Vector3(center.x - x, y, center.z + z), new Vector3(center.x + x, y, center.z + z), new Vector3(center.x + x, y, center.z - z), swatch);
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
        // 1 unit = 1 m, pivot bottom-centre, front = -Z (towards the camera).

        public static Mesh Cart()
        {
            var k = new MeshKit();
            k.Box(new Vector3(0, 0.35f, 0), new Vector3(2.6f, 0.75f, 1.1f), 0, Pal.Walnut, Pal.CounterTop);
            for (int i = -1; i <= 1; i += 2) k.Cyl(new Vector3(i * 1.0f, 0, -0.56f), 0.32f, 0.06f, Pal.Black, Pal.SteelDark, 10);
            for (int i = -1; i <= 1; i += 2) k.Box(new Vector3(i * 1.2f, 1.1f, 0.45f), new Vector3(0.08f, 1.4f, 0.08f), 0, Pal.Trim);
            k.Box(new Vector3(0, 2.5f, 0.1f), new Vector3(3.0f, 0.08f, 1.6f), 0, Pal.Brand, Pal.AwningCream);   // awning
            k.Box(new Vector3(0, 0.45f, -0.57f), new Vector3(1.6f, 0.4f, 0.04f), 0, Pal.Chalk);             // chalk menu (no text)
            return k.Build("cart");
        }

        public static Mesh Counter(float width)
        {
            var k = new MeshKit();
            k.Box(Vector3.zero, new Vector3(width, 1.0f, 0.8f), 0, Pal.Walnut, Pal.CounterTop);
            k.Box(new Vector3(0, 0.05f, -0.41f), new Vector3(width - 0.2f, 0.12f, 0.02f), 0, Pal.Copper);
            return k.Build("counter");
        }

        public static Mesh BackBar(float width)
        {
            var k = new MeshKit();
            k.Box(Vector3.zero, new Vector3(width, 0.9f, 0.6f), 0, Pal.Trim, Pal.CounterTop);
            k.Box(new Vector3(0, 1.7f, 0.2f), new Vector3(width, 0.06f, 0.3f), 0, Pal.Walnut);     // shelf
            return k.Build("back_bar");
        }

        public static Mesh Espresso(int heads)
        {
            var k = new MeshKit();
            float w = 0.5f + 0.35f * Mathf.Max(1, heads);
            k.Box(Vector3.zero, new Vector3(w, 0.45f, 0.5f), 0, Pal.Black, Pal.Steel);
            k.Box(new Vector3(0, 0.45f, 0.05f), new Vector3(w - 0.1f, 0.12f, 0.4f), 0, Pal.Steel);
            for (int h = 0; h < Mathf.Max(1, heads); h++)
            {
                float x = -w * 0.5f + 0.42f + h * 0.35f;
                k.Box(new Vector3(x, 0.2f, -0.28f), new Vector3(0.12f, 0.08f, 0.12f), 0, Pal.SteelDark);
                k.Cyl(new Vector3(x, 0.02f, -0.3f), 0.05f, 0.08f, Pal.Cup, Pal.Coffee, 6);
            }
            k.Box(new Vector3(w * 0.5f + 0.04f, 0.25f, -0.15f), new Vector3(0.04f, 0.25f, 0.04f), 0, Pal.Steel);   // steam wand
            return k.Build("espresso_" + heads);
        }

        public static Mesh Grinder()
        {
            var k = new MeshKit();
            k.Box(Vector3.zero, new Vector3(0.22f, 0.35f, 0.28f), 0, Pal.Black);
            k.Cyl(new Vector3(0, 0.35f, 0), 0.11f, 0.22f, Pal.Window, Pal.Coffee, 8);
            return k.Build("grinder");
        }

        public static Mesh Fridge()
        {
            var k = new MeshKit();
            k.Box(Vector3.zero, new Vector3(0.7f, 0.85f, 0.55f), 0, Pal.Steel, Pal.CounterTop);
            k.Box(new Vector3(0, 0.12f, -0.28f), new Vector3(0.55f, 0.6f, 0.02f), 0, Pal.Window);
            for (int i = 0; i < 3; i++) k.Box(new Vector3(-0.15f + i * 0.15f, 0.2f, -0.18f), new Vector3(0.1f, 0.22f, 0.1f), 0, Pal.Milk);
            return k.Build("fridge");
        }

        public static Mesh PastryCase()
        {
            var k = new MeshKit();
            k.Box(Vector3.zero, new Vector3(1.0f, 1.0f, 0.7f), 0, Pal.Walnut, Pal.CounterTop);
            k.Box(new Vector3(0, 1.0f, 0), new Vector3(0.95f, 0.4f, 0.6f), 0, Pal.Window);
            for (int i = 0; i < 3; i++) k.Box(new Vector3(-0.3f + i * 0.3f, 1.05f, 0), new Vector3(0.18f, 0.08f, 0.14f), 20 * i, Pal.Pastry);
            return k.Build("pastry_case");
        }

        public static Mesh Shelf()
        {
            var k = new MeshKit();
            k.Box(Vector3.zero, new Vector3(0.8f, 0.05f, 0.35f), 0, Pal.Walnut);
            k.Box(new Vector3(0, 0.3f, 0), new Vector3(0.8f, 0.04f, 0.35f), 0, Pal.Walnut);
            for (int i = -1; i <= 1; i += 2) k.Box(new Vector3(i * 0.38f, 0, 0), new Vector3(0.04f, 0.35f, 0.35f), 0, Pal.Trim);
            return k.Build("pickup_shelf");
        }

        public static Mesh IceMaker()
        {
            var k = new MeshKit();
            k.Box(Vector3.zero, new Vector3(0.6f, 0.9f, 0.6f), 0, Pal.Steel, Pal.SteelDark);
            k.Box(new Vector3(0, 0.55f, -0.31f), new Vector3(0.4f, 0.2f, 0.02f), 0, Pal.Window);
            return k.Build("ice_maker");
        }

        public static Mesh Roaster()
        {
            var k = new MeshKit();
            k.Box(Vector3.zero, new Vector3(1.1f, 0.7f, 0.9f), 0, Pal.Black);
            k.Cyl(new Vector3(0, 0.7f, 0.05f), 0.42f, 0.55f, Pal.Brand, Pal.Copper, 10);
            k.Box(new Vector3(0, 1.25f, 0.05f), new Vector3(0.36f, 0.25f, 0.36f), 0, Pal.Copper);       // hopper
            k.Cyl(new Vector3(0.75f, 0, -0.1f), 0.45f, 0.35f, Pal.SteelDark, Pal.Coffee, 10);            // cooling tray
            k.Box(new Vector3(-0.2f, 1.25f, 0.3f), new Vector3(0.12f, 1.2f, 0.12f), 0, Pal.SteelDark);   // chimney
            return k.Build("roaster");
        }

        public static Mesh Table()
        {
            var k = new MeshKit();
            k.Cyl(Vector3.zero, 0.06f, 0.72f, Pal.Trim, Pal.Trim, 6);
            k.Cyl(new Vector3(0, 0.72f, 0), 0.45f, 0.05f, Pal.Walnut, Pal.CounterTop, 10);
            for (int i = -1; i <= 1; i += 2)
            {
                k.Box(new Vector3(i * 0.7f, 0, 0), new Vector3(0.4f, 0.45f, 0.4f), 0, Pal.Trim, Pal.Walnut);
                k.Box(new Vector3(i * 0.88f, 0.45f, 0), new Vector3(0.05f, 0.45f, 0.4f), 0, Pal.Trim);
            }
            k.Cyl(new Vector3(0.12f, 0.77f, 0), 0.05f, 0.09f, Pal.Cup, Pal.Coffee, 6);
            return k.Build("table");
        }

        public static Mesh Rack()
        {
            var k = new MeshKit();
            for (int i = 0; i < 3; i++) k.Box(new Vector3(0, i * 0.55f, 0), new Vector3(1.2f, 0.04f, 0.45f), 0, Pal.SteelDark);
            for (int i = -1; i <= 1; i += 2) k.Box(new Vector3(i * 0.58f, 0, 0), new Vector3(0.04f, 1.2f, 0.45f), 0, Pal.Steel);
            for (int i = 0; i < 4; i++) k.Box(new Vector3(-0.42f + i * 0.28f, 0.6f, 0), new Vector3(0.2f, 0.3f, 0.18f), 0, Pal.Sack, Pal.Coffee);
            return k.Build("rest_rack");
        }

        public static Mesh Bench()
        {
            var k = new MeshKit();
            k.Box(Vector3.zero, new Vector3(1.4f, 0.85f, 0.6f), 0, Pal.Walnut, Pal.CounterTop);
            k.Box(new Vector3(-0.3f, 0.85f, 0), new Vector3(0.3f, 0.35f, 0.2f), 0, Pal.Sack, Pal.Coffee);
            k.Box(new Vector3(0.25f, 0.85f, 0), new Vector3(0.35f, 0.12f, 0.3f), 0, Pal.Steel);    // scale
            return k.Build("packing_bench");
        }

        public static Mesh Sack()
        {
            var k = new MeshKit();
            k.Box(Vector3.zero, new Vector3(0.55f, 0.65f, 0.4f), 8, Pal.Sack, Pal.GreenBean);
            return k.Build("sack");
        }

        public static Mesh Plant()
        {
            var k = new MeshKit();
            k.Cyl(Vector3.zero, 0.22f, 0.35f, Pal.WallBrick, Pal.Coffee, 8);
            k.Box(new Vector3(0, 0.35f, 0), new Vector3(0.5f, 0.5f, 0.5f), 20, Pal.Plant);
            k.Box(new Vector3(0, 0.75f, 0), new Vector3(0.35f, 0.35f, 0.35f), 55, Pal.PlantDark);
            return k.Build("plant");
        }

        /// <summary>The chunky canonical figure (customers, baristas), ~1.7 m tall. apron = staff.</summary>
        public static Mesh Figure(int shirt, int apron = -1, int skin = Pal.SkinTone)
        {
            var k = new MeshKit();
            k.Box(new Vector3(-0.11f, 0, 0), new Vector3(0.17f, 0.8f, 0.2f), 0, Pal.Trousers);
            k.Box(new Vector3(0.11f, 0, 0), new Vector3(0.17f, 0.8f, 0.2f), 0, Pal.Trousers);
            k.Box(new Vector3(0, 0.8f, 0), new Vector3(0.46f, 0.55f, 0.26f), 0, shirt);
            if (apron >= 0) k.Box(new Vector3(0, 0.55f, -0.135f), new Vector3(0.4f, 0.75f, 0.02f), 0, apron);
            k.Box(new Vector3(-0.29f, 0.85f, 0), new Vector3(0.12f, 0.48f, 0.14f), 0, shirt);
            k.Box(new Vector3(0.29f, 0.85f, 0), new Vector3(0.12f, 0.48f, 0.14f), 0, shirt);
            k.Box(new Vector3(0, 1.35f, 0), new Vector3(0.3f, 0.32f, 0.3f), 0, skin);
            k.Box(new Vector3(0, 1.6f, 0.02f), new Vector3(0.32f, 0.1f, 0.32f), 0, Pal.Hair);
            return k.Build("figure_" + shirt + "_" + apron);
        }

        /// <summary>A takeaway cup on the pickup shelf (instanced).</summary>
        public static Mesh CupMesh(int sleeve)
        {
            var k = new MeshKit();
            k.Cyl(Vector3.zero, 0.05f, 0.12f, Pal.Cup, Pal.White, 6);
            k.Cyl(new Vector3(0, 0.04f, 0), 0.052f, 0.04f, sleeve, sleeve, 6);
            return k.Build("cup_" + sleeve);
        }
    }
}
