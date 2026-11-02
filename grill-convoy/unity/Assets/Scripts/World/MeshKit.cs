// Grill Convoy — the diorama's procedural geometry. Everything in the world shares ONE material (URP Simple Lit on the
// 8×8 palette texture Resources/materials/palette.png): a model picks colours by pointing its UVs at palette swatches, so
// the SRP batcher draws the whole city with a handful of batches (fleet §10: Low ≤ 150 draws; plan §8: city map ≤ 80).
// Builders merge many boxes into one mesh per district; nothing here runs per frame.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GrillConvoy.World
{
    /// <summary>Palette swatch indices (Tools/make_palette.py writes the same order).</summary>
    public static class Pal
    {
        public const int Grass = 0, GrassDark = 1, Road = 2, RoadLine = 3, Paving = 4, WallCream = 5, WallTerracotta = 6, WallTeal = 7,
            RoofRed = 8, RoofGrey = 9, Brand = 10, Steel = 11, TruckCream = 12, TruckTeal = 13, Commissary = 14, Water = 15, Sand = 16,
            Tree = 17, Trunk = 18, Locked = 19, Window = 20, Bun = 21, Patty = 22, Cheese = 23, Warning = 24, Yellow = 25, Lettuce = 26,
            Gold = 27, Sunset = 28, Neon = 29, Retro = 30, Wood = 31, SkinTone = 32, ShirtA = 33, ShirtB = 34, ShirtC = 35, ShirtD = 36, ShirtE = 37,
            Trousers = 38, White = 39, Steam = 40, Flame = 41, Lights = 42, Stadium = 43;
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
        public void Disc(Vector3 c, float r, float y, int swatch, int segs = 12)
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

        /// <summary>A beach umbrella / plaza parasol: pole + flat octagon-ish canopy (two crossed boxes).</summary>
        public void Umbrella(Vector3 at, float s, int canopy)
        {
            Box(at, new Vector3(0.2f, 3.2f, 0.2f) * s, 0, Pal.Steel);
            Box(at + Vector3.up * 3.0f * s, new Vector3(3.6f, 0.35f, 3.6f) * s, 0, canopy);
            Box(at + Vector3.up * 3.0f * s, new Vector3(3.6f, 0.35f, 3.6f) * s, 45, canopy);
        }

        /// <summary>Copies a Blender-kit piece (palette UVs already set) into this mesh through m. swatch ≥ 0 repaints every
        /// face with one swatch (locked districts read as grey silhouettes). A mirroring matrix flips the winding back.</summary>
        public void Append(Vector3[] pv, Vector3[] pn, Vector2[] puv, int[] pt, Matrix4x4 m, int swatch = -1)
        {
            if (pv == null || pt == null || pv.Length == 0) return;
            int i0 = v.Count;
            var nm = m.inverse.transpose;
            var su = swatch >= 0 ? Pal.Uv(swatch) : Vector2.zero;
            bool hasN = pn != null && pn.Length == pv.Length, hasUv = puv != null && puv.Length == pv.Length;
            for (int i = 0; i < pv.Length; i++)
            {
                v.Add(m.MultiplyPoint3x4(pv[i]));
                n.Add(hasN ? nm.MultiplyVector(pn[i]).normalized : Vector3.up);
                uv.Add(swatch >= 0 || !hasUv ? (swatch >= 0 ? su : Pal.Uv(Pal.Locked)) : puv[i]);
            }
            bool flip = m.determinant < 0f;
            for (int i = 0; i + 2 < pt.Length; i += 3)
            {
                t.Add(i0 + pt[i]);
                t.Add(i0 + (flip ? pt[i + 2] : pt[i + 1]));
                t.Add(i0 + (flip ? pt[i + 1] : pt[i + 2]));
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

        /// <summary>A burger truck, forward +Z, serving hatch + awning on BOTH long sides (whichever side faces the crowd).</summary>
        public static Mesh Truck(string type)
        {
            var k = new MeshKit();
            bool big = type == "box";
            float len = big ? 7.2f : 6.0f, w = big ? 2.6f : 2.4f, h = big ? 3.0f : 2.7f;
            k.Box(new Vector3(0, 0.55f, len * 0.36f), new Vector3(w * 0.95f, h * 0.7f, len * 0.26f), 0, Pal.TruckCream, Pal.TruckCream);   // cab
            k.Box(new Vector3(0, 1.25f + h * 0.3f, len * 0.47f), new Vector3(w * 0.85f, h * 0.28f, 0.06f), 0, Pal.Window);                // windscreen
            k.Box(new Vector3(0, 0.55f, -len * 0.12f), new Vector3(w, h, len * 0.72f), 0, big ? Pal.TruckTeal : Pal.Brand, Pal.Steel);     // kitchen box
            for (int s = -1; s <= 1; s += 2)
            {
                k.Box(new Vector3(s * (w * 0.5f + 0.02f), 0.55f + h * 0.42f, -len * 0.12f), new Vector3(0.06f, h * 0.36f, len * 0.46f), 0, Pal.Window);  // hatch
                k.Box(new Vector3(s * (w * 0.5f + 0.55f), 0.55f + h * 0.8f, -len * 0.12f), new Vector3(1.1f, 0.12f, len * 0.5f), 0, Pal.Sunset);      // awning
                k.Box(new Vector3(s * (w * 0.5f + 0.25f), 0.55f + h * 0.36f, -len * 0.12f), new Vector3(0.5f, 0.1f, len * 0.46f), 0, Pal.Steel);       // counter
            }
            k.Box(new Vector3(0, 0.55f + h, -len * 0.3f), new Vector3(0.5f, 0.7f, 0.5f), 0, Pal.Steel);   // vent hood (steam source)
            k.Box(new Vector3(0, 0.55f + h, len * 0.05f), new Vector3(w * 0.7f, 0.9f, 0.12f), 0, Pal.Yellow);   // roof sign (no text)
            for (int i = -1; i <= 1; i += 2) for (int j = -1; j <= 1; j += 2) k.Box(new Vector3(i * w * 0.42f, 0, j * len * 0.3f), new Vector3(0.45f, 0.9f, 0.9f), 0, Pal.Window);
            return k.Build("truck_" + type);
        }

        public static Mesh Commissary()
        {
            var k = new MeshKit();
            k.Box(Vector3.zero, new Vector3(26, 9, 16), 0, Pal.Commissary, Pal.RoofGrey);
            k.Box(new Vector3(-6, 0, -8.1f), new Vector3(8, 5, 0.3f), 0, Pal.Steel);           // loading door
            k.Box(new Vector3(6, 4, -8.1f), new Vector3(9, 2.4f, 0.3f), 0, Pal.Window);        // windows
            k.Box(new Vector3(0, 9, 2), new Vector3(4, 3, 4), 0, Pal.Steel);                    // cold-room unit
            k.Box(new Vector3(0, 6.6f, -8.4f), new Vector3(14, 1.6f, 0.4f), 0, Pal.Brand);     // sign band (no text)
            return k.Build("commissary");
        }

        /// <summary>The chunky canonical figure (crowd), ~2.6 units tall so it reads at map zoom. shirt = customer type colour.</summary>
        public static Mesh Figure(int shirt, bool burger)
        {
            var k = new MeshKit();
            k.Box(new Vector3(-0.35f, 0, 0), new Vector3(0.5f, 1.1f, 0.6f), 0, Pal.Trousers);
            k.Box(new Vector3(0.35f, 0, 0), new Vector3(0.5f, 1.1f, 0.6f), 0, Pal.Trousers);
            k.Box(new Vector3(0, 1.1f, 0), new Vector3(1.4f, 1.1f, 0.8f), 0, shirt);
            k.Box(new Vector3(0, 2.2f, 0), new Vector3(0.8f, 0.7f, 0.8f), 0, Pal.SkinTone);
            if (burger)
            {
                k.Box(new Vector3(0.95f, 1.1f, 0.2f), new Vector3(0.7f, 0.25f, 0.7f), 0, Pal.Bun);
                k.Box(new Vector3(0.95f, 1.35f, 0.2f), new Vector3(0.75f, 0.15f, 0.75f), 0, Pal.Patty);
                k.Box(new Vector3(0.95f, 1.5f, 0.2f), new Vector3(0.7f, 0.25f, 0.7f), 0, Pal.Bun);
            }
            return k.Build("figure_" + shirt + (burger ? "_b" : ""));
        }

        /// <summary>A striped window awning (looks: the store's awning set swaps the stripes).</summary>
        public static Mesh Awning(int a, int b)
        {
            var k = new MeshKit();
            for (int i = 0; i < 6; i++) k.Box(new Vector3(-2.5f + i * 1.0f, 0, 0), new Vector3(1.0f, 0.14f, 1.3f), 0, i % 2 == 0 ? a : b);
            return k.Build("awning_" + a);
        }

        /// <summary>A sidewalk menu board (looks: chalk or neon), no text.</summary>
        public static Mesh Board(int face)
        {
            var k = new MeshKit();
            k.Box(Vector3.zero, new Vector3(1.4f, 0.2f, 0.6f), 0, Pal.Wood);
            k.Box(new Vector3(0, 0.2f, 0), new Vector3(1.2f, 1.8f, 0.15f), 0, face);
            for (int i = 0; i < 3; i++) k.Box(new Vector3(-0.15f, 0.6f + i * 0.4f, -0.09f), new Vector3(0.6f, 0.07f, 0.02f), 0, Pal.White);
            return k.Build("board_" + face);
        }

        /// <summary>A soft puff (steam card stand-in: three offset boxes), white.</summary>
        public static Mesh Puff()
        {
            var k = new MeshKit();
            k.Box(Vector3.zero, new Vector3(1.2f, 0.9f, 1.2f), 20, Pal.Steam);
            k.Box(new Vector3(0.5f, 0.5f, 0.2f), new Vector3(0.9f, 0.8f, 0.9f), 50, Pal.Steam);
            k.Box(new Vector3(-0.4f, 0.6f, -0.3f), new Vector3(0.8f, 0.7f, 0.8f), 70, Pal.Steam);
            return k.Build("puff");
        }

        /// <summary>The cartoon flare-up pulse over a grill (no injuries, no destruction — plan §5.8).</summary>
        public static Mesh Flame()
        {
            var k = new MeshKit();
            k.Box(Vector3.zero, new Vector3(1.6f, 1.4f, 1.6f), 45, Pal.Flame);
            k.Box(new Vector3(0, 1.2f, 0), new Vector3(1.0f, 1.2f, 1.0f), 20, Pal.Yellow);
            k.Box(new Vector3(0, 2.2f, 0), new Vector3(0.5f, 0.8f, 0.5f), 0, Pal.Flame);
            return k.Build("flame");
        }
    }
}
