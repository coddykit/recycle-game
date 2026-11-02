// Grill Convoy — the 2.5D city diorama of Cinder Bay (design §8): harbour commissary, road graph, three pitch "stages"
// (Office Plaza: paved square + office blocks; Boardwalk: deck along the beach + parasols; Stadium: the bowl), trucks that
// drive the roads and park with the hatch toward the crowd, the line in front of every open truck, steam over busy grills
// and the cartoon flare-up pulse. Built ONCE from the map data, rebuilt only when a pitch unlocks; trucks / crowd update
// from the sim each frame (Low: 15 Hz). Day -> evening light from the game clock (a light colour, free on every tier).
//   * 1 km of map = 100 world units; props are drawn larger than life so they read at map zoom.
//   * one palette material for everything (MeshKit) + instanced draws for the crowd, steam and flames.
//   * Low tier: no shadows (QualityTiers), ≤ 16 figures per line at 15 Hz, half the leavers, no steam puffs.
//   * Medium / High: Blender-kit district pieces (Resources/kit/d_*: office towers, townhouses, shops, beach kiosks, stadium
//     stands, harbour sheds + crane + boats, string lights, floodlights) merged INTO the district meshes — same draws as
//     the procedural blocks Low keeps (+1 harbour mesh on Medium / High only). docs/ART.md has the numbers.
//   * picking: a tap ray-casts the ground plane (no physics module) and picks the nearest truck / pitch / commissary.
using System;
using System.Collections.Generic;
using CoddyKit;
using UnityEngine;
using GrillConvoy.Sim;

namespace GrillConvoy.World
{
    public struct WorldHit
    {
        public string Kind;   // truck | pitch | commissary | none
        public int Index;     // truck index / pitch node
        public override string ToString() => Kind + ":" + Index;
    }

    public sealed class RegionWorld : MonoBehaviour
    {
        public const float Scale = 100f;   // world units per map km
        /// <summary>Yaw offset of the Blender kit trucks (verify in the editor: the cab must lead when driving).</summary>
        public const float KitYaw = 0f;

        public Camera cam;
        public CameraRig rig;
        public TapInput tap;
        public Light sun;

        public event Action<WorldHit> Picked;
        public Simulation Sim { get; private set; }
        public bool Low { get; private set; }
        public string Livery = "";

        Material mat, matInst;
        readonly Dictionary<string, GameObject> districtObjs = new Dictionary<string, GameObject>();
        readonly List<Transform> truckObjs = new List<Transform>();
        readonly List<string> truckTypes = new List<string>();
        Crowd crowd;
        Mesh puffMesh, flameMesh;
        readonly List<Matrix4x4> puffs = new List<Matrix4x4>(), flames = new List<Matrix4x4>();
        // cosmetics shown at every open truck (store "looks": crew outfits, window awnings, menu board styles)
        readonly Mesh[] crewMesh = new Mesh[2], awningMesh = new Mesh[2], boardMesh = new Mesh[2];
        readonly List<Matrix4x4> crewAt = new List<Matrix4x4>(), awningAt = new List<Matrix4x4>(), boardAt = new List<Matrix4x4>();
        bool uniforms, awnings, boards;
        int builtDistricts = -1;
        bool dirty = true;
        float slowAcc;
        MaterialPropertyBlock mpb;

        public static Vector3 ToWorld(double x, double y) => new Vector3((float)(x * Scale), 0f, (float)(y * Scale));
        public Vector3 NodePos(int node) { var n = Sim.Gd.Nodes[node]; return ToWorld(n.X, n.Y); }
        public Vector3 CommissaryPos => NodePos(Sim.Gd.DepotNode);
        public Vector3 PitchPos(int node) => NodePos(node);

        public void Init(Simulation sim, bool low)
        {
            Sim = sim;
            Low = low;
            mpb = new MaterialPropertyBlock();
            mat = Resources.Load<Material>(low ? "materials/palette_low" : "materials/palette") ?? Resources.Load<Material>("materials/palette");
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")); mat.mainTexture = Resources.Load<Texture2D>("materials/palette"); }
            matInst = new Material(mat) { enableInstancing = true, name = "palette_instanced" };
            puffMesh = MeshKit.Puff();
            flameMesh = MeshKit.Flame();
            crewMesh[0] = MeshKit.Figure(Pal.White, false);
            crewMesh[1] = MeshKit.Figure(Pal.Retro, false);
            awningMesh[0] = MeshKit.Awning(Pal.Sunset, Pal.White);
            awningMesh[1] = MeshKit.Awning(Pal.Retro, Pal.Yellow);
            boardMesh[0] = MeshKit.Board(Pal.Window);
            boardMesh[1] = MeshKit.Board(Pal.Neon);
            BuildStatic();
            crowd = new Crowd(this, matInst, low);
            if (tap != null) tap.Tapped += OnTap;
            if (rig != null) rig.Fit(UnlockedBounds(), true);
            MarkDirty();
        }

        /// <summary>A new career (slot switch): same city, other trucks.</summary>
        public void Rebind(Simulation sim)
        {
            Sim = sim;
            builtDistricts = -1;
            MarkDirty();
        }

        public void SetLow(bool low)
        {
            if (low == Low) return;
            Low = low;
            crowd?.SetLow(low);
            builtDistricts = -1;   // district meshes differ per tier (kit pieces on Medium / High only)
            MarkDirty();
        }

        public void MarkDirty() => dirty = true;

        // ------------------------------------------------------------------ static scene

        void BuildStatic()
        {
            var gd = Sim.Gd;
            var k = new MeshKit();
            k.Quad(new Vector3(-150, 0, -30), new Vector3(-150, 0, 420), new Vector3(750, 0, 420), new Vector3(750, 0, -30), Pal.Grass);
            k.Quad(new Vector3(-400, -0.5f, -400), new Vector3(-400, -0.5f, -30), new Vector3(1100, -0.5f, -30), new Vector3(1100, -0.5f, -400), Pal.Water);
            k.Strip(new Vector3(-150, 0, -38), new Vector3(750, 0, -38), 16f, 0.05f, Pal.Sand);
            foreach (var e in gd.Edges)
                k.Strip(ToWorld(gd.Nodes[e.A].X, gd.Nodes[e.A].Y), ToWorld(gd.Nodes[e.B].X, gd.Nodes[e.B].Y), 11f, 0.1f, Pal.Road);
            foreach (var n in gd.Nodes) k.Disc(ToWorld(n.X, n.Y), n.IsPitch ? 9f : 7f, 0.11f, Pal.Road);
            Spawn("Ground", k.Build("ground"), Vector3.zero);
            var c = Prop("commissary", MeshKit.Commissary, CommissaryPos + new Vector3(0, 0, 18f), 0);
            c.name = "Commissary";
        }

        GameObject Spawn(string name, Mesh mesh, Vector3 pos, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = Low ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows = !Low;
            return go;
        }

        /// <summary>Blender-kit model when present (Resources/kit/<name>), else the procedural fallback.</summary>
        GameObject Prop(string kitName, Func<Mesh> fallback, Vector3 pos, float yaw)
        {
            var prefab = Resources.Load<GameObject>("kit/" + kitName);
            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, transform);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = mat; r.shadowCastingMode = Low ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On; }
                go.transform.localPosition = pos;
            }
            else go = Spawn(kitName, fallback(), pos);
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            return go;
        }

        // ------------------------------------------------------------------ Blender-kit district pieces (Medium / High)

        sealed class KitPiece { public Vector3[] V, N; public Vector2[] Uv; public int[] T; public Matrix4x4 Local; }
        readonly Dictionary<string, KitPiece> kitPieces = new Dictionary<string, KitPiece>();

        /// <summary>Nominal size (x width, y height, z depth, metres) of the scalable pieces — tools/art/kit.py builds them so.</summary>
        static readonly Dictionary<string, Vector3> KitNominal = new Dictionary<string, Vector3>
        {
            { "d_office_a", new Vector3(16, 40, 14) }, { "d_office_b", new Vector3(16, 32, 14) }, { "d_house_a", new Vector3(12, 10, 10) },
            { "d_shop_a", new Vector3(12, 8, 10) }, { "d_kiosk", new Vector3(12, 6, 9) }, { "d_stand", new Vector3(22, 16, 10) },
        };

        /// <summary>Resources/kit/d_* read once (the importer marks only these readable); null = missing / unreadable.</summary>
        KitPiece Piece(string name)
        {
            if (kitPieces.TryGetValue(name, out var p)) return p;
            var prefab = Resources.Load<GameObject>("kit/" + name);
            var mf = prefab != null ? prefab.GetComponentInChildren<MeshFilter>() : null;
            var mesh = mf != null ? mf.sharedMesh : null;
            if (mesh != null && mesh.isReadable)
                p = new KitPiece
                {
                    V = mesh.vertices, N = mesh.normals, Uv = mesh.uv, T = mesh.triangles,
                    Local = Matrix4x4.Translate(-prefab.transform.position) * mf.transform.localToWorldMatrix,
                };
            kitPieces[name] = p;
            return p;
        }

        /// <summary>Merges a kit piece into the district mesh (Medium / High only; Low keeps the procedural blocks, so the Low
        /// scene is unchanged). size = target size for the scalable pieces (else scale 1). Front of a piece = its local +Z.
        /// Returns false when the caller should draw its procedural fallback.</summary>
        bool KitInto(MeshKit k, string name, Vector3 pos, float yaw, bool open, Vector3? size = null)
        {
            if (Low) return false;
            var p = Piece(name);
            if (p == null) return false;
            var sc = Vector3.one;
            if (size.HasValue && KitNominal.TryGetValue(name, out var nom))
                sc = new Vector3(size.Value.x / nom.x, size.Value.y / nom.y, size.Value.z / nom.z);
            k.Append(p.V, p.N, p.Uv, p.T, Matrix4x4.TRS(pos, Quaternion.Euler(0, yaw, 0), sc) * p.Local, open ? -1 : Pal.Locked);
            return true;
        }

        int ChapterOf(string district)
        {
            foreach (var d in Sim.Gd.Districts) if (d.Id == district) return d.Chapter;
            return 0;
        }

        /// <summary>The district that dresses an edge's street blocks: the later chapter, ties not the harbour — a shared road
        /// is built once (both ends used to build it, doubling the blocks).</summary>
        string EdgeOwner(NodeDef a, NodeDef b)
        {
            if (a.District == b.District) return a.District;
            int ca = ChapterOf(a.District), cb = ChapterOf(b.District);
            if (ca != cb) return ca > cb ? a.District : b.District;
            return a.District == "harbour" ? b.District : a.District;
        }

        void BuildDistrict(DistrictDef dd)
        {
            if (districtObjs.TryGetValue(dd.Id, out var old)) { Destroy(old); districtObjs.Remove(dd.Id); }
            bool harbour = dd.Id == "harbour";
            if (harbour && Low) return;   // Low: the harbour stays the bare commissary yard (0 extra draws)
            bool open = Sim.Unlocked(dd.Id);
            var gd = Sim.Gd;
            var rng = new System.Random(dd.Id.GetHashCode() & 0x7fffffff);
            var k = new MeshKit();
            int[] walls = { Pal.WallCream, Pal.WallTerracotta, Pal.WallTeal, Pal.WallCream };
            // street blocks along the district's roads
            foreach (var e in gd.Edges)
            {
                var na = gd.Nodes[e.A]; var nb = gd.Nodes[e.B];
                if (EdgeOwner(na, nb) != dd.Id) continue;
                Vector3 a = ToWorld(na.X, na.Y), b = ToWorld(nb.X, nb.Y);
                var dir = b - a; float len = dir.magnitude; if (len < 40f) continue;
                dir /= len;
                var side = Vector3.Cross(Vector3.up, dir);
                float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                for (float s = 26f; s < len - 26f; s += 18f + (float)rng.NextDouble() * 7f)
                    for (int sd = -1; sd <= 1; sd += 2)
                    {
                        var c = a + dir * s + side * sd * 18f;
                        if (c.z < -20f) continue;   // not on the beach
                        if (rng.NextDouble() < 0.25) { k.Tree(c, 1.4f, yaw); continue; }
                        float w = 10f + (float)rng.NextDouble() * 6f, h = (dd.Id == "plaza" ? 14f : 7f) + (float)rng.NextDouble() * (dd.Id == "plaza" ? 26f : 10f);
                        // kit: the facade faces the road, width along the street
                        string piece = h > 24f ? "d_office_b" : h > 10f ? "d_house_a" : "d_shop_a";
                        if (KitInto(k, piece, c, yaw - 90f * sd, open, new Vector3(w, h, 10f))) continue;
                        int wall = open ? walls[rng.Next(walls.Length)] : Pal.Locked;
                        int roof = open ? (rng.NextDouble() < 0.5 ? Pal.RoofRed : Pal.RoofGrey) : Pal.Locked;
                        k.Box(c, new Vector3(w, h, 10f), yaw, wall, roof);
                        if (open && !Low) k.Box(c + Vector3.up * h * 0.55f - side * sd * 5.05f, new Vector3(w * 0.7f, 1.2f, 0.1f), yaw, Pal.Window);
                    }
            }
            foreach (int p in gd.Pitches)
            {
                if (gd.Nodes[p].District != dd.Id) continue;
                var c = NodePos(p);
                switch (dd.Id)
                {
                    case "plaza":
                        k.Disc(c + new Vector3(0, 0, -12), 34f, 0.12f, open ? Pal.Paving : Pal.Locked, 16);
                        for (int i = 0; i < 4; i++)
                        {
                            float h = open ? 34 + i * 7 : 20;
                            var at = c + new Vector3(-36 + i * 24, 0, 34);
                            if (!KitInto(k, i % 2 == 0 ? "d_office_a" : "d_office_b", at, 180f, open, new Vector3(16, h, 14)))
                                k.Box(at, new Vector3(16, h, 14), 0, open ? Pal.WallTeal : Pal.Locked, open ? Pal.Steel : Pal.Locked);
                        }
                        for (int i = -1; i <= 1; i += 2)
                        {
                            var at = c + new Vector3(i * 20, 0, -30);
                            if (KitInto(k, "d_planter", at, 180f, open)) continue;
                            k.Box(at, new Vector3(10, 1.2f, 4), 0, open ? Pal.Wood : Pal.Locked); if (open) k.Tree(at + Vector3.up * 1.2f, 1.1f, 0);
                        }
                        if (open && !Low) for (int i = -2; i <= 2; i++) k.Umbrella(c + new Vector3(i * 8, 0.12f, -22), 0.9f, Pal.Brand);
                        if (open)
                        {
                            KitInto(k, "d_lights", c + new Vector3(0, 0.12f, -26), 0f, true);       // Medium / High extras
                            KitInto(k, "d_bits", c + new Vector3(28, 0.12f, -8), 200f, true);
                        }
                        break;
                    case "boardwalk":
                        k.Box(c + new Vector3(0, 0, -16), new Vector3(170, 0.6f, 16), 0, open ? Pal.Wood : Pal.Locked);
                        for (int i = -3; i <= 3; i++) k.Umbrella(c + new Vector3(i * 22, 0, -42), 1.3f, open ? (i % 2 == 0 ? Pal.Sunset : Pal.Retro) : Pal.Locked);
                        for (int i = -2; i <= 2; i++)
                        {
                            var at = c + new Vector3(i * 30 + 10, 0, 22);
                            if (!KitInto(k, "d_kiosk", at, 180f, open, new Vector3(12, 6, 9)))
                                k.Box(at, new Vector3(12, 6, 9), 0, open ? Pal.WallCream : Pal.Locked, open ? Pal.Sunset : Pal.Locked);
                        }
                        if (open)
                        {
                            for (int i = -1; i <= 1; i += 2)
                            {
                                KitInto(k, "d_lifeguard", c + new Vector3(i * 76, 0.05f, -36), 180f, true);
                                KitInto(k, "d_lights", c + new Vector3(i * 42, 0.6f, -22), 0f, true);
                            }
                            KitInto(k, "d_bits", c + new Vector3(-24, 0.6f, -12), 160f, true);
                        }
                        break;
                    case "stadium":
                    {
                        var bowl = c + new Vector3(40, 0, 40);
                        for (int i = 0; i < 14; i++)
                        {
                            float ang = i * Mathf.PI * 2f / 14f;
                            var off = new Vector3(Mathf.Cos(ang) * 46f, 0, Mathf.Sin(ang) * 32f);
                            if (KitInto(k, "d_stand", bowl + off, Mathf.Atan2(-off.x, -off.z) * Mathf.Rad2Deg, open, new Vector3(22, 16, 8))) continue;
                            k.Box(bowl + off, new Vector3(22, 16, 8), -ang * Mathf.Rad2Deg + 90, open ? Pal.Stadium : Pal.Locked, open ? Pal.Steel : Pal.Locked);
                        }
                        k.Disc(bowl + Vector3.up * 0.2f, 36f, 0.2f, open ? Pal.GrassDark : Pal.Locked, 16);
                        k.Disc(c + new Vector3(0, 0, -10), 26f, 0.12f, open ? Pal.Paving : Pal.Locked, 14);
                        if (open)
                            for (int i = 0; i < 4; i++)
                            {
                                var off = new Vector3(i % 2 == 0 ? -60f : 60f, 0, i < 2 ? -44f : 44f);
                                KitInto(k, "d_floodlight", bowl + off, Mathf.Atan2(-off.x, -off.z) * Mathf.Rad2Deg, true);
                            }
                        break;
                    }
                }
            }
            if (harbour)
            {
                // Medium / High: the working harbour around the commissary — sheds, a gantry crane on the shore, a pier, boats
                var cp = CommissaryPos;
                KitInto(k, "d_warehouse", new Vector3(5, 0, cp.z + 48), 180f, true);
                KitInto(k, "d_warehouse", new Vector3(-2, 0, cp.z - 28), 180f, true);
                if (KitInto(k, "d_crane", new Vector3(-8, 0.05f, -40), 180f, true))
                {
                    k.Box(new Vector3(-8, 0, -58), new Vector3(6, 0.8f, 24), 0, Pal.Wood);
                    KitInto(k, "d_boat", new Vector3(12, -0.5f, -62), 160f, true);
                    KitInto(k, "d_boat", new Vector3(-32, -0.5f, -66), 205f, true);
                }
                KitInto(k, "d_bits", cp + new Vector3(20, 0.1f, 4), 180f, true);
                if (k.Vertices == 0) return;
            }
            districtObjs[dd.Id] = Spawn("District_" + dd.Id, k.Build("district_" + dd.Id), Vector3.zero);
        }

        void SyncTrucks()
        {
            while (truckObjs.Count > Sim.S.Trucks.Count) { Destroy(truckObjs[truckObjs.Count - 1].gameObject); truckObjs.RemoveAt(truckObjs.Count - 1); truckTypes.RemoveAt(truckTypes.Count - 1); }
            for (int i = 0; i < Sim.S.Trucks.Count; i++)
            {
                string type = Sim.S.Trucks[i].Type;
                if (i < truckObjs.Count && truckTypes[i] == type) continue;
                var go = Prop("truck_" + type, () => MeshKit.Truck(type), ParkedPos(i), 90 + KitYaw);
                go.name = "Truck_" + i;
                if (i < truckObjs.Count) { Destroy(truckObjs[i].gameObject); truckObjs[i] = go.transform; truckTypes[i] = type; }
                else { truckObjs.Add(go.transform); truckTypes.Add(type); }
            }
            ApplyLivery();
        }

        public void SetLivery(string livery) { Livery = livery ?? ""; ApplyLivery(); }

        void ApplyLivery()
        {
            Color tint = Livery switch
            {
                "gold_trim" => new Color(1f, 0.86f, 0.4f),
                "livery_retro" => new Color(0.6f, 0.9f, 1f),
                "livery_neon" => new Color(1f, 0.6f, 0.95f),
                _ => Color.white,
            };
            foreach (var t in truckObjs)
                foreach (var r in t.GetComponentsInChildren<Renderer>())
                {
                    r.GetPropertyBlock(mpb);
                    mpb.SetColor("_BaseColor", tint);
                    r.SetPropertyBlock(mpb);
                }
        }

        /// <summary>Owned looks (store cosmetics): crew outfits, striped window awnings, neon menu boards.</summary>
        public void SetCosmetics(bool crewOutfits, bool windowAwnings, bool menuBoards)
        {
            uniforms = crewOutfits; awnings = windowAwnings; boards = menuBoards;
        }

        public List<Bounds> UnlockedBounds()
        {
            var o = new List<Bounds>();
            var b = new Bounds(CommissaryPos, Vector3.one * 60f);
            foreach (var n in Sim.Gd.Nodes) if (Sim.Unlocked(n.District)) b.Encapsulate(ToWorld(n.X, n.Y));
            b.Expand(new Vector3(60, 0, 60));
            o.Add(b);
            return o;
        }

        // ------------------------------------------------------------------ positions

        /// <summary>Where truck i parks: at its pitch (several trucks side by side) or in a row at the commissary.</summary>
        Vector3 ParkedPos(int i)
        {
            var t = Sim.S.Trucks[i];
            if (t.Open && t.AtNode != Sim.Gd.DepotNode)
            {
                int slot = 0;
                for (int k = 0; k < i; k++) if (Sim.S.Trucks[k].Open && Sim.S.Trucks[k].AtNode == t.AtNode) slot++;
                return NodePos(t.AtNode) + new Vector3(-8f + slot * 16f, 0.2f, 6f);
            }
            return CommissaryPos + new Vector3(-16 + i * 9f, 0.2f, -4f);
        }

        public Vector3 TruckWorld(int i) => i >= 0 && i < truckObjs.Count ? truckObjs[i].localPosition : CommissaryPos;
        /// <summary>The serving hatch side facing the camera (the line forms here).</summary>
        public Vector3 HatchWorld(int i) => TruckWorld(i) + new Vector3(0, 0, -3f);

        // ------------------------------------------------------------------ frame

        void Update()
        {
            if (Sim == null) return;
            if (dirty)
            {
                dirty = false;
                int dCount = Sim.S.Districts.Count;
                if (dCount != builtDistricts)
                {
                    foreach (var dd in Sim.Gd.Districts) BuildDistrict(dd);
                    if (builtDistricts >= 0 && rig != null) rig.Fit(UnlockedBounds(), false);
                    builtDistricts = dCount;
                }
                SyncTrucks();
            }
            if (truckObjs.Count != Sim.S.Trucks.Count) SyncTrucks();

            float dt = Time.deltaTime;
            slowAcc += dt;
            bool tick = !Low || slowAcc >= 1f / 15f;
            if (tick)
            {
                float step = Low ? slowAcc : dt;
                slowAcc = 0f;
                MoveTrucks(step);
                crowd.Tick(step, Sim);
                Effects();
                Light();
            }
            if (puffs.Count > 0) Graphics.RenderMeshInstanced(new RenderParams(matInst), puffMesh, 0, puffs);
            if (flames.Count > 0) Graphics.RenderMeshInstanced(new RenderParams(matInst), flameMesh, 0, flames);
            if (crewAt.Count > 0) Graphics.RenderMeshInstanced(new RenderParams(matInst), crewMesh[uniforms ? 1 : 0], 0, crewAt);
            if (awningAt.Count > 0) Graphics.RenderMeshInstanced(new RenderParams(matInst), awningMesh[awnings ? 1 : 0], 0, awningAt);
            if (boardAt.Count > 0) Graphics.RenderMeshInstanced(new RenderParams(matInst), boardMesh[boards ? 1 : 0], 0, boardAt);
            crowd.Draw();
        }

        void MoveTrucks(float dt)
        {
            for (int i = 0; i < truckObjs.Count && i < Sim.S.Trucks.Count; i++)
            {
                var t = Sim.S.Trucks[i];
                var tr = truckObjs[i];
                Vector3 target;
                Quaternion rot;
                if (t.Status == TruckStatus.Drive)
                {
                    var (x, y, heading) = Sim.TruckPos(t);
                    target = ToWorld(x, y) + new Vector3(0, 0.2f, 0);
                    rot = Quaternion.Euler(0, (float)(heading * Mathf.Rad2Deg) + KitYaw, 0);
                }
                else { target = ParkedPos(i); rot = Quaternion.Euler(0, 90 + KitYaw, 0); }
                float k = 1f - Mathf.Exp(-12f * dt);   // smooth the 15 Hz / step jumps
                tr.localPosition = Vector3.Lerp(tr.localPosition, target, k);
                tr.localRotation = Quaternion.Slerp(tr.localRotation, rot, k);
            }
        }

        /// <summary>Steam over a working grill (Medium / High only) and the flare-up pulse (every tier: it is gameplay).</summary>
        void Effects()
        {
            puffs.Clear();
            flames.Clear();
            crewAt.Clear();
            awningAt.Clear();
            boardAt.Clear();
            float time = Time.time;
            for (int i = 0; i < truckObjs.Count && i < Sim.S.Trucks.Count; i++)
            {
                var t = Sim.S.Trucks[i];
                if (!t.Open) continue;
                var top = truckObjs[i].localPosition + new Vector3(0, 4.2f, 0);
                var at = truckObjs[i].localPosition;
                for (int c = 0; c < Mathf.Min(2, t.Crew.Count); c++)
                    crewAt.Add(Matrix4x4.TRS(at + new Vector3(-1.2f + c * 2.4f, 0.4f, -0.3f), Quaternion.Euler(0, 180, 0), Vector3.one * 1.3f));
                awningAt.Add(Matrix4x4.TRS(at + new Vector3(0, 3.1f, -1.9f), Quaternion.identity, Vector3.one));
                boardAt.Add(Matrix4x4.TRS(at + new Vector3(4.4f, 0, -3.4f), Quaternion.Euler(0, 200, 0), Vector3.one));
                if (t.Flaring)
                {
                    float s = 1.4f + 0.35f * Mathf.Sin(time * 9f + i);
                    flames.Add(Matrix4x4.TRS(top, Quaternion.Euler(0, time * 90f, 0), Vector3.one * s));
                }
                else if (!Low && t.Queue.Count > 0 && t.Propane > 0)
                    for (int p = 0; p < 3; p++)
                    {
                        float ph = (time * 0.6f + p / 3f + i * 0.17f) % 1f;
                        puffs.Add(Matrix4x4.TRS(top + new Vector3(Mathf.Sin(ph * 6f + p) * 0.6f, ph * 5f, 0), Quaternion.Euler(0, ph * 120f, 0), Vector3.one * (0.6f + ph * 1.4f)));
                    }
            }
        }

        /// <summary>Morning to evening on the sun (colour + angle only; no extra light, no shader branch).</summary>
        void Light()
        {
            if (sun == null) return;
            float f = Mathf.Clamp01((float)((Sim.S.Minute - Sim.Gd.DayStart) / Math.Max(1, Sim.Gd.DayMinutes)));
            sun.color = Color.Lerp(new Color(1f, 0.97f, 0.9f), new Color(1f, 0.72f, 0.5f), Mathf.SmoothStep(0.55f, 1f, f));
            sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(55f, 22f, f), -30f + 60f * f, 0);
        }

        /// <summary>Sim events -> leavers in the crowd, structure rebuilds.</summary>
        public void OnSimEvent(SimEvent e)
        {
            if (e.Type == "served" && e.Text != "hand") crowd.Leave(e.A, e.B >= 3 ? 2 : e.B >= 2 ? 1 : 0, true);
            else if (e.Type == "walked" || e.Type == "sold_out") crowd.Leave(e.A, 0, false);
            else if (e.Type == "district" || e.Type == "truck_new" || e.Type == "day_start") MarkDirty();
        }

        // ------------------------------------------------------------------ picking

        void OnTap(Vector2 topLeft)
        {
            var hit = Pick(new Vector2(topLeft.x, Screen.height - topLeft.y));
            Picked?.Invoke(hit);
        }

        /// <summary>Screen px (bottom-left origin) -> what is there. Trucks win over pitches, pitches over the commissary.</summary>
        public WorldHit Pick(Vector2 screen)
        {
            var none = new WorldHit { Kind = "none", Index = -1 };
            if (cam == null || Sim == null) return none;
            var ray = cam.ScreenPointToRay(screen);
            if (Mathf.Abs(ray.direction.y) < 1e-4f) return none;
            float dist = -ray.origin.y / ray.direction.y;
            if (dist < 0) return none;
            var g = ray.origin + ray.direction * dist;
            float pickR = 22f * Mathf.Max(0.6f, rig != null ? rig.Zoom : 1f);
            float best = pickR * pickR * 0.8f;
            var res = none;
            for (int i = 0; i < truckObjs.Count; i++)
            {
                float d = (truckObjs[i].localPosition - g).sqrMagnitude;
                if (d < best) { best = d; res = new WorldHit { Kind = "truck", Index = i }; }
            }
            if (res.Kind == "truck") return res;
            best = 40f * 40f;
            foreach (int p in Sim.Gd.Pitches)
            {
                float d = (PitchPos(p) - g).sqrMagnitude;
                if (d < best) { best = d; res = new WorldHit { Kind = "pitch", Index = p }; }
            }
            if (res.Kind == "pitch") return res;
            if ((CommissaryPos - g).sqrMagnitude < 45f * 45f) return new WorldHit { Kind = "commissary", Index = 0 };
            return none;
        }

        /// <summary>Screen position (top-left px) of a world point, or null when behind the camera.</summary>
        public Vector2? ScreenOf(Vector3 world)
        {
            if (cam == null) return null;
            var s = cam.WorldToScreenPoint(world);
            if (s.z <= 0) return null;
            return new Vector2(s.x, Screen.height - s.y);
        }
    }
}
