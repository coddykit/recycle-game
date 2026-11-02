// Ridgeback Rangers — the diorama's procedural geometry. Everything in the world shares ONE material (URP Simple Lit on the
// 8×8 palette texture Resources/materials/palette.png): a model picks colours by pointing its UVs at palette swatches, so
// the SRP batcher draws the whole island with a handful of batches (fleet §10: Low ≤ 150 draws, the plan targets ≤ 120).
// Builders merge many boxes into one mesh per zone / paddock; nothing here runs per frame. The Meshy dinosaurs keep their
// own texture (one material per species = its colour morph); the procedural Dino() is only the fallback.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RidgebackRangers.World
{
    /// <summary>Palette swatch indices (Tools/make_palette.py writes the same order).</summary>
    public static class Pal
    {
        public const int Grass = 0, GrassDark = 1, Path = 2, PathLine = 3, Stone = 4, WallCream = 5, WallTerracotta = 6, WallBlue = 7,
            RoofRed = 8, RoofSlate = 9, Brand = 10, Panel = 11, White = 12, Jungle = 13, Wood = 14, Water = 15, Sand = 16,
            Tree = 17, Trunk = 18, Locked = 19, Dark = 20, Steel = 21, Rock = 22, Fern = 23, Warning = 24, Yellow = 25, Mud = 26,
            Gold = 27, Sunset = 28, Forest = 29, Glacier = 30, Khaki = 31, SkinTone = 32, ShirtA = 33, ShirtB = 34, ShirtC = 35, ShirtD = 36, ShirtE = 37,
            Trousers = 38, PureWhite = 39, VolcanoRock = 40, Lava = 41, EggShell = 42, DinoOlive = 43;
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

        /// <summary>A flat strip on the ground from a to b (paths, rails), y lifted a little to avoid z-fighting.</summary>
        public void Strip(Vector3 a, Vector3 b, float width, float y, int swatch)
        {
            var dir = (b - a); dir.y = 0;
            if (dir.sqrMagnitude < 1e-6f) return;
            var side = Vector3.Cross(Vector3.up, dir.normalized) * (width * 0.5f);
            a.y = b.y = y;
            Quad(a - side, b - side, b + side, a + side, swatch);
        }

        /// <summary>A flat disc (crossings, plazas, the island) with segs segments; r2 &gt; 0 = an ellipse.</summary>
        public void Disc(Vector3 c, float r, float y, int swatch, int segs = 10, float r2 = 0f)
        {
            var u = Pal.Uv(swatch);
            if (r2 <= 0) r2 = r;
            int i0 = v.Count;
            v.Add(new Vector3(c.x, y, c.z)); n.Add(Vector3.up); uv.Add(u);
            for (int s = 0; s <= segs; s++)
            {
                float a = s * Mathf.PI * 2f / segs;
                v.Add(new Vector3(c.x + Mathf.Cos(a) * r, y, c.z + Mathf.Sin(a) * r2)); n.Add(Vector3.up); uv.Add(u);
            }
            for (int s = 0; s < segs; s++) { t.Add(i0); t.Add(i0 + 2 + s); t.Add(i0 + 1 + s); }
        }

        /// <summary>A low cone (the volcano, hills): base radius r, height h, segs facets; topR &gt; 0 = a crater rim.</summary>
        public void Cone(Vector3 c, float r, float h, int swatch, int segs = 12, float topR = 0f, int topSwatch = -1)
        {
            for (int s = 0; s < segs; s++)
            {
                float a0 = s * Mathf.PI * 2f / segs, a1 = (s + 1) * Mathf.PI * 2f / segs;
                var b0 = c + new Vector3(Mathf.Cos(a0) * r, 0, Mathf.Sin(a0) * r);
                var b1 = c + new Vector3(Mathf.Cos(a1) * r, 0, Mathf.Sin(a1) * r);
                var t0 = c + new Vector3(Mathf.Cos(a0) * topR, h, Mathf.Sin(a0) * topR);
                var t1 = c + new Vector3(Mathf.Cos(a1) * topR, h, Mathf.Sin(a1) * topR);
                Quad(b0, t0, t1, b1, swatch);
            }
            if (topR > 0) Disc(c + Vector3.up * (h - 0.5f), topR, c.y + h - 0.5f, topSwatch >= 0 ? topSwatch : swatch, segs);
        }

        /// <summary>A low-poly tree (trunk + two stacked boxes).</summary>
        public void Tree(Vector3 at, float s, float yaw, int leaves = Pal.Tree)
        {
            Box(at, new Vector3(0.6f, 2.2f, 0.6f) * s, yaw, Pal.Trunk);
            Box(at + Vector3.up * 2f * s, new Vector3(3.2f, 2.4f, 3.2f) * s, yaw + 15f, leaves);
            Box(at + Vector3.up * 4.2f * s, new Vector3(2f, 1.6f, 2f) * s, yaw + 40f, leaves);
        }

        /// <summary>A fern / palm clump: a short trunk with four leaf blades.</summary>
        public void Fern(Vector3 at, float s, float yaw)
        {
            Box(at, new Vector3(0.5f, 2.6f, 0.5f) * s, yaw, Pal.Trunk);
            for (int k = 0; k < 4; k++) Box(at + Vector3.up * 2.4f * s, new Vector3(0.7f, 0.25f, 3.2f) * s, yaw + k * 45f, Pal.Fern);
        }

        /// <summary>A fence ring around a paddock (posts + two rails), tier 1 wood, 2 steel, 3 double moat (+ water ditch).</summary>
        public void Fence(Vector3 c, float w, float d, int tier, float integrity)
        {
            int post = tier == 1 ? Pal.Wood : Pal.Steel;
            float h = tier == 1 ? 1.8f : 2.4f;
            Vector3[] corners = { c + new Vector3(-w / 2, 0, -d / 2), c + new Vector3(w / 2, 0, -d / 2), c + new Vector3(w / 2, 0, d / 2), c + new Vector3(-w / 2, 0, d / 2) };
            int broken = integrity < 0.6f ? 2 : integrity < 0.85f ? 1 : 0;
            for (int i = 0; i < 4; i++)
            {
                var a = corners[i]; var b = corners[(i + 1) % 4];
                float len = Vector3.Distance(a, b);
                int posts = Mathf.Max(2, Mathf.RoundToInt(len / 5f));
                float yaw = Mathf.Atan2(b.x - a.x, b.z - a.z) * Mathf.Rad2Deg;
                for (int p = 0; p <= posts; p++) Box(Vector3.Lerp(a, b, p / (float)posts), new Vector3(0.45f, h, 0.45f), yaw, post);
                bool gap = i == 0 && broken > 0;   // loose tiles show as a gap in the front rail
                for (int r = 0; r < 2; r++)
                {
                    float y = h * (0.4f + 0.45f * r);
                    if (!gap) Box(Vector3.Lerp(a, b, 0.5f) + Vector3.up * y, new Vector3(0.2f, 0.25f, len), yaw, tier == 3 ? Pal.Brand : post);
                    else
                    {
                        Box(Vector3.Lerp(a, b, 0.2f) + Vector3.up * y, new Vector3(0.2f, 0.25f, len * 0.4f), yaw, post);
                        if (broken < 2) Box(Vector3.Lerp(a, b, 0.85f) + Vector3.up * y, new Vector3(0.2f, 0.25f, len * 0.3f), yaw, post);
                    }
                }
            }
            if (tier == 3)
            {
                Strip(corners[0] - new Vector3(0, 0, 1.6f), corners[1] - new Vector3(0, 0, 1.6f), 1.6f, 0.13f, Pal.Water);
                Strip(corners[3] + new Vector3(0, 0, 1.6f), corners[2] + new Vector3(0, 0, 1.6f), 1.6f, 0.13f, Pal.Water);
            }
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

        public static Mesh Building(string kind)
        {
            var k = new MeshKit();
            switch (kind)
            {
                case "gate":
                    k.Box(new Vector3(-6, 0, 0), new Vector3(2.4f, 9, 2.4f), 0, Pal.Wood, Pal.Brand);
                    k.Box(new Vector3(6, 0, 0), new Vector3(2.4f, 9, 2.4f), 0, Pal.Wood, Pal.Brand);
                    k.Box(new Vector3(0, 8, 0), new Vector3(15, 2.2f, 1.6f), 0, Pal.Brand);
                    k.Box(new Vector3(-9, 0, 2), new Vector3(3, 3, 3), 0, Pal.WallCream, Pal.RoofRed);
                    break;
                case "barn":
                    k.Box(Vector3.zero, new Vector3(14, 7, 10), 0, Pal.WallTerracotta, Pal.RoofSlate);
                    k.Box(new Vector3(0, 0, -5.1f), new Vector3(5, 5, 0.2f), 0, Pal.Wood);
                    k.Box(new Vector3(9, 0, 0), new Vector3(4, 9, 4), 0, Pal.Steel, Pal.RoofRed);
                    break;
                case "vet":
                    k.Box(Vector3.zero, new Vector3(12, 6, 9), 0, Pal.White, Pal.Glacier);
                    k.Box(new Vector3(0, 6, 0), new Vector3(2.4f, 2.4f, 0.6f), 0, Pal.Warning);
                    break;
                case "hatchery":
                    k.Box(Vector3.zero, new Vector3(11, 5, 9), 0, Pal.WallCream, Pal.Glacier);
                    k.Cone(new Vector3(0, 5, 0), 4.5f, 4, Pal.Glacier, 8);
                    break;
                case "station":
                    k.Box(Vector3.zero, new Vector3(18, 0.6f, 8), 0, Pal.Stone);
                    for (int i = -1; i <= 1; i += 2) k.Box(new Vector3(i * 7, 0.6f, 0), new Vector3(0.6f, 5, 0.6f), 0, Pal.Wood);
                    k.Box(new Vector3(0, 5.6f, 0), new Vector3(19, 0.6f, 8.5f), 0, Pal.RoofRed);
                    break;
                case "view":
                    k.Box(Vector3.zero, new Vector3(6, 6, 6), 45, Pal.Wood, Pal.Panel);
                    k.Box(new Vector3(0, 6, 0), new Vector3(7, 0.6f, 7), 45, Pal.RoofRed);
                    k.Box(new Vector3(7, 0, -2), new Vector3(4, 2.2f, 3), 0, Pal.Wood, Pal.Fern);   // the feeding station
                    break;
                default:   // hq
                    k.Box(Vector3.zero, new Vector3(13, 6, 9), 0, Pal.Wood, Pal.Brand);
                    k.Box(new Vector3(0, 6, 0), new Vector3(1, 6, 1), 0, Pal.Steel);
                    k.Box(new Vector3(0.6f, 10, 0), new Vector3(1.8f, 1.2f, 0.1f), 0, Pal.Brand);
                    break;
            }
            return k.Build("bld_" + kind);
        }

        public static Mesh TramCar()
        {
            var k = new MeshKit();
            k.Box(new Vector3(0, 0.6f, 0), new Vector3(2.8f, 0.8f, 6f), 0, Pal.Brand);
            k.Box(new Vector3(0, 1.4f, 0), new Vector3(2.8f, 1.2f, 6f), 0, Pal.Khaki);
            for (int i = -1; i <= 1; i += 2) for (int j = -1; j <= 1; j += 2) k.Box(new Vector3(i * 1.2f, 2.6f, j * 2.8f), new Vector3(0.2f, 1.4f, 0.2f), 0, Pal.Wood);
            k.Box(new Vector3(0, 4.0f, 0), new Vector3(3.0f, 0.3f, 6.4f), 0, Pal.White);
            for (int i = -1; i <= 1; i += 2) for (int j = -1; j <= 1; j += 2) k.Box(new Vector3(i * 1.25f, 0, j * 2f), new Vector3(0.4f, 0.8f, 0.8f), 0, Pal.Dark);
            return k.Build("tram_car");
        }

        /// <summary>The fallback dinosaur per model family (~8 units long; the Meshy models replace it when present).</summary>
        public static Mesh Dino(string family)
        {
            var k = new MeshKit();
            int body = Pal.DinoOlive, belly = Pal.EggShell;
            switch (family)
            {
                case "longneck":
                    k.Box(new Vector3(0, 2.2f, 0), new Vector3(3.2f, 3, 5.5f), 0, body);
                    k.Box(new Vector3(0, 4.5f, 3.2f), new Vector3(1.1f, 6, 1.1f), 0, body);
                    k.Box(new Vector3(0, 10.2f, 3.6f), new Vector3(1.2f, 1.1f, 2f), 0, body);
                    k.Box(new Vector3(0, 2.6f, -4.2f), new Vector3(0.9f, 0.9f, 4f), 0, body);
                    break;
                case "duckbill":
                    k.Box(new Vector3(0, 2.4f, 0), new Vector3(2.4f, 2.6f, 4f), 0, body);
                    k.Box(new Vector3(0, 4.6f, 2.2f), new Vector3(1.4f, 1.6f, 2.2f), 0, body);
                    k.Box(new Vector3(0, 5.8f, 1.2f), new Vector3(0.4f, 0.4f, 2.4f), 0, Pal.Sunset);
                    k.Box(new Vector3(0, 2.6f, -3.4f), new Vector3(1.0f, 1.0f, 3.4f), 0, body);
                    break;
                case "plated":
                    k.Box(new Vector3(0, 1.4f, 0), new Vector3(2.8f, 2.2f, 5f), 0, body);
                    for (int i = -2; i <= 2; i++) k.Box(new Vector3(0, 3.4f, i * 0.9f), new Vector3(0.3f, 1.2f, 0.8f), 0, Pal.Sunset);
                    k.Box(new Vector3(0, 1.6f, 3.0f), new Vector3(1.2f, 1.2f, 1.6f), 0, body);
                    k.Box(new Vector3(0, 1.6f, -3.6f), new Vector3(0.8f, 0.8f, 3f), 0, body);
                    break;
                default:   // horned
                    k.Box(new Vector3(0, 1.4f, 0), new Vector3(3f, 2.4f, 4.6f), 0, body);
                    k.Box(new Vector3(0, 1.6f, 2.8f), new Vector3(2f, 1.8f, 1.8f), 0, body);
                    k.Box(new Vector3(0, 2.6f, 2.2f), new Vector3(3.4f, 2.2f, 0.3f), 0, Pal.Sunset);
                    k.Box(new Vector3(0, 2.4f, 3.9f), new Vector3(0.3f, 0.3f, 1.2f), 0, belly);
                    k.Box(new Vector3(0, 1.2f, -3.2f), new Vector3(0.8f, 0.8f, 2.4f), 0, body);
                    break;
            }
            if (family != "duckbill") for (int i = -1; i <= 1; i += 2) for (int j = -1; j <= 1; j += 2) k.Box(new Vector3(i * 1f, 0, j * 1.5f), new Vector3(0.8f, 1.6f, 0.8f), 0, body);
            else for (int i = -1; i <= 1; i += 2) k.Box(new Vector3(i * 0.8f, 0, -0.4f), new Vector3(0.8f, 1.8f, 0.9f), 0, body);
            return k.Build("dino_" + family);
        }

        /// <summary>The chunky canonical figure (crowd), ~2.6 units tall so it reads at map zoom. hat = a ranger's wide hat.</summary>
        public static Mesh Figure(int shirt, bool hat)
        {
            var k = new MeshKit();
            k.Box(new Vector3(-0.35f, 0, 0), new Vector3(0.5f, 1.1f, 0.6f), 0, Pal.Trousers);
            k.Box(new Vector3(0.35f, 0, 0), new Vector3(0.5f, 1.1f, 0.6f), 0, Pal.Trousers);
            k.Box(new Vector3(0, 1.1f, 0), new Vector3(1.4f, 1.1f, 0.8f), 0, shirt);
            k.Box(new Vector3(0, 2.2f, 0), new Vector3(0.8f, 0.7f, 0.8f), 0, Pal.SkinTone);
            if (hat) { k.Box(new Vector3(0, 2.85f, 0), new Vector3(1.7f, 0.12f, 1.7f), 0, Pal.Khaki); k.Box(new Vector3(0, 2.9f, 0), new Vector3(0.9f, 0.4f, 0.9f), 0, Pal.Khaki); }
            return k.Build("figure_" + shirt + (hat ? "_hat" : ""));
        }
    }
}
