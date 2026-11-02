// Bottle Route — the 2.5D diorama of the coastal region (design §8). Built ONCE from the map data (ground, sea, roads,
// district blocks, trees), rebuilt only when something structural changes (district unlocked, point bought, Green Rating
// day change); trucks / walkers / bale stacks update from the sim each frame (Low: 15 Hz).
//   * 1 km of map = 100 world units; props are drawn larger than life so they read at map zoom.
//   * one palette material for everything (MeshKit) + instanced draws for walkers, bales and litter.
//   * the town visibly gets cleaner as the Green Rating rises: fewer litter bits (the plan's cheap "wow").
//   * Low tier: no shadows (QualityTiers), walkers ≤ 50 % at 15 Hz, litter ≤ 50 %, no baler press animation.
//   * polish pass (2026-11-02): district buildings, the stadium shell and the depot compound are Blender-kit pieces
//     (Resources/kit/*.bytes, KitPiece) MERGED into the same district / depot mesh — 0 extra draws on every tier; Low
//     merges the *_low twins (no bevels / windows). The procedural boxes stay as the fallback. Medium/High only: the
//     baler press ram (+1 draw, animated without allocation).
//   * chapters 5-7 (2026-11-02): the Industrial Estate, Riviera Resort, Hill Villages and the Island are built from the
//     same map JSON with the same kit pieces. Low merges EVERY district into ONE mesh (8 districts = 1 draw, was 4 draws
//     for 4); Medium/High keep one mesh per district (culling). The island ground, the two piers and the dashed ferry lane
//     are merged into the ground mesh (0 extra draws); Medium/High add the ferry boat (+1 draw, no allocation), which
//     carries a truck while it sails. Large remote machines / hotels reuse the big machine / café props, scaled up.
//     Low draws the return points INSTANCED (one draw per prop kind: machine S, machine M/L, shop, café/hotel) instead of
//     one GameObject per point — the 38 sites of chapters 1-7 cost ≤ 4 draws on Low (Medium/High keep the GameObjects).
//   * picking: a tap ray-casts the ground plane (no physics module) and picks the nearest truck / point / depot.
using System;
using System.Collections.Generic;
using CoddyKit;
using UnityEngine;
using BottleRoute.Sim;

namespace BottleRoute.World
{
    public struct WorldHit
    {
        public string Kind;   // point | truck | depot | none
        public int Index;
        public override string ToString() => Kind + ":" + Index;
    }

    public sealed class RegionWorld : MonoBehaviour
    {
        public const float Scale = 100f;   // world units per map km

        public Camera cam;
        public CameraRig rig;
        public TapInput tap;
        public Light sun;

        public event Action<WorldHit> Picked;
        public Simulation Sim { get; private set; }
        public bool Low { get; private set; }
        public string Livery = "";

        Material mat, matInst, matBalePet, matBaleAlu;
        GameObject depotObj;
        Transform balerRam, boat;
        Vector3 ferryA, ferryB;
        float ramPhase, boatBob;
        bool lowBuilt;
        readonly Dictionary<string, GameObject> districtObjs = new Dictionary<string, GameObject>();
        readonly List<GameObject> pointObjs = new List<GameObject>();
        // Low: instanced point props (kit mesh + the prefab's root / child transform, or the procedural fallback)
        static readonly string[] PropKinds = { "machine_s", "machine_m", "shop", "horeca" };
        readonly Mesh[] propMesh = new Mesh[4];
        readonly Matrix4x4[] propLocal = new Matrix4x4[4];
        readonly bool[] propKit = new bool[4];
        readonly List<Matrix4x4>[] pointBatches = { new List<Matrix4x4>(), new List<Matrix4x4>(), new List<Matrix4x4>(), new List<Matrix4x4>() };
        readonly List<Transform> truckObjs = new List<Transform>();
        readonly List<string> truckTypes = new List<string>();
        Crowd crowd;
        Mesh baleMeshPet, baleMeshAlu, litterMesh, glassMesh;
        readonly List<Matrix4x4> litter = new List<Matrix4x4>();
        readonly List<Matrix4x4> balesPet = new List<Matrix4x4>(), balesAlu = new List<Matrix4x4>(), glassPiles = new List<Matrix4x4>();
        int builtPoints = -1, builtDistricts = -1, builtYard = -1, builtGlass = -1;
        double builtRating = -1;
        bool dirty = true;
        float slowAcc;
        MaterialPropertyBlock mpb;

        public static Vector3 ToWorld(double x, double y) => new Vector3((float)(x * Scale), 0f, (float)(y * Scale));
        public Vector3 NodePos(int node) { var n = Sim.Gd.Nodes[node]; return ToWorld(n.X, n.Y); }
        public Vector3 PointPos(int i) => NodePos(Sim.S.Points[i].Node);
        public Vector3 DepotPos => NodePos(Sim.Gd.DepotNode);

        public void Init(Simulation sim, bool low)
        {
            Sim = sim;
            Low = low;
            lowBuilt = low;
            mpb = new MaterialPropertyBlock();
            mat = Resources.Load<Material>(low ? "materials/palette_low" : "materials/palette") ?? Resources.Load<Material>("materials/palette");
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")); mat.mainTexture = Resources.Load<Texture2D>("materials/palette"); }
            matInst = new Material(mat) { enableInstancing = true, name = "palette_instanced" };
            baleMeshPet = KitMesh("kit/bale_pet_lod" + (low ? "2" : "1"));
            baleMeshAlu = KitMesh("kit/bale_alu_lod" + (low ? "2" : "1"));
            matBalePet = BaleMaterial("kit/bale_pet_base" + (low ? "_low" : ""), baleMeshPet != null);
            matBaleAlu = BaleMaterial("kit/bale_alu_base" + (low ? "_low" : ""), baleMeshAlu != null);
            baleMeshPet ??= BoxMesh(Pal.Pet);
            baleMeshAlu ??= BoxMesh(Pal.Alu);
            litterMesh = FlatMesh(Pal.Litter, 1.2f);
            glassMesh = BoxMesh(Pal.Glass);
            LoadPropMeshes();
            BuildStatic();
            crowd = new Crowd(this, matInst, low);
            if (tap != null) tap.Tapped += OnTap;
            if (rig != null) rig.Fit(UnlockedBounds(), true);
            MarkDirty();
        }

        public void SetLow(bool low)
        {
            if (low == Low) return;
            Low = low;
            crowd?.SetLow(low);
            MarkDirty();   // Update rebuilds districts + depot with the *_low / full kit pieces (structural, not per frame)
        }

        public void MarkDirty() => dirty = true;

        // ------------------------------------------------------------------ static scene

        void BuildStatic()
        {
            var gd = Sim.Gd;
            // ground + sea (two big quads)
            var k = new MeshKit();
            // land covers every mainland node (+ margin); the coast is the line z = -40 (map y = -0.4)
            float minX = -150, maxX = 850, maxZ = 650;
            foreach (var n in gd.Nodes)
            {
                if (n.Y < -0.4) continue;
                var w = ToWorld(n.X, n.Y);
                minX = Mathf.Min(minX, w.x - 150); maxX = Mathf.Max(maxX, w.x + 150); maxZ = Mathf.Max(maxZ, w.z + 120);
            }
            k.Quad(new Vector3(minX, 0, -40), new Vector3(minX, 0, maxZ), new Vector3(maxX, 0, maxZ), new Vector3(maxX, 0, -40), Pal.Grass);
            k.Quad(new Vector3(minX - 300, -0.5f, -800), new Vector3(minX - 300, -0.5f, -40), new Vector3(maxX + 300, -0.5f, -40), new Vector3(maxX + 300, -0.5f, -800), Pal.Water);
            k.Strip(new Vector3(minX, 0, -40), new Vector3(maxX, 0, -40), 16f, 0.05f, Pal.Sand);
            BuildIsland(k);
            // roads: one strip per edge, discs on the nodes; a ferry edge is a dashed lane on the water between two piers
            foreach (var e in gd.Edges)
            {
                Vector3 a = ToWorld(gd.Nodes[e.A].X, gd.Nodes[e.A].Y), b = ToWorld(gd.Nodes[e.B].X, gd.Nodes[e.B].Y);
                if (e.Ferry) { FerryLane(k, a, b); continue; }
                k.Strip(a, b, e.Alley ? 6f : 11f, 0.1f, e.Alley ? Pal.Concrete : Pal.Road);
            }
            foreach (var n in gd.Nodes) k.Disc(ToWorld(n.X, n.Y), n.Kind == "site" ? 9f : 7f, 0.11f, n.Kind == "site" ? Pal.Concrete : Pal.Road);
            Spawn("Ground", k.Build("ground"), Vector3.zero);
            BuildDepot();
            BuildFerry();
        }

        /// <summary>Sea nodes (map y below the coast) form the island: a sand disc + a grass disc around them.</summary>
        void BuildIsland(MeshKit k)
        {
            var gd = Sim.Gd;
            Vector3 c = Vector3.zero;
            int n = 0;
            foreach (var nd in gd.Nodes) if (nd.Y < -0.4) { c += ToWorld(nd.X, nd.Y); n++; }
            if (n == 0) return;
            c /= n;
            float r = 0;
            foreach (var nd in gd.Nodes) if (nd.Y < -0.4) r = Mathf.Max(r, (ToWorld(nd.X, nd.Y) - c).magnitude);
            k.Disc(c, r + 75f, -0.05f, Pal.Sand, 24);
            k.Disc(c, r + 55f, 0.02f, Pal.Grass, 24);
        }

        /// <summary>Two concrete piers reaching into the water and a dashed lane between them (merged into the ground).</summary>
        static void FerryLane(MeshKit k, Vector3 a, Vector3 b)
        {
            var dir = (b - a).normalized;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            k.Box(a + dir * 16f + Vector3.down * 0.4f, new Vector3(7f, 0.9f, 34f), yaw, Pal.Concrete);
            k.Box(b - dir * 16f + Vector3.down * 0.4f, new Vector3(7f, 0.9f, 34f), yaw, Pal.Concrete);
            float len = (b - a).magnitude;
            for (float s = 40f; s < len - 40f; s += 26f)
                k.Strip(a + dir * s, a + dir * Mathf.Min(s + 12f, len - 40f), 2f, -0.44f, Pal.RoadLine);   // just above the water (-0.5)
        }

        /// <summary>Medium/High: the ferry boat (+1 draw). Low: none — trucks simply cross the dashed lane.</summary>
        void BuildFerry()
        {
            if (boat != null) { Destroy(boat.gameObject); boat = null; }
            var gd = Sim.Gd;
            var fe = gd.Edges.Find(e => e.Ferry);
            if (fe == null) return;
            ferryA = NodePos(fe.A); ferryB = NodePos(fe.B);
            if (Low) return;
            var k = new MeshKit();
            k.Box(Vector3.zero, new Vector3(9f, 1f, 20f), 0, Pal.TruckWhite, Pal.Panel);              // hull + car deck
            k.Box(new Vector3(0, 1f, -7.5f), new Vector3(7f, 3.2f, 4f), 0, Pal.Brand, Pal.TruckWhite); // bridge
            k.Box(new Vector3(0, 2.6f, -9.55f), new Vector3(5f, 0.9f, 0.1f), 0, Pal.Window);
            var parked = ferryA + (ferryB - ferryA).normalized * 30f + new Vector3(0, -0.9f, 0);
            boat = Spawn("Ferry", k.Build("ferry"), parked).transform;
            var d = ferryB - ferryA;
            boat.localRotation = Quaternion.Euler(0, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 0);
        }

        /// <summary>The boat carries a truck that is sailing; otherwise it waits at the mainland pier, bobbing a little.</summary>
        void AnimateFerry(float dt)
        {
            if (boat == null) return;
            boatBob += dt;
            for (int i = 0; i < truckObjs.Count && i < Sim.S.Trucks.Count; i++)
            {
                if (!Sim.OnFerry(Sim.S.Trucks[i])) continue;
                boat.localPosition = truckObjs[i].localPosition + new Vector3(0, -1.1f, 0);
                boat.localRotation = truckObjs[i].localRotation;
                return;
            }
            var home = ferryA + (ferryB - ferryA).normalized * 30f;
            boat.localPosition = Vector3.Lerp(boat.localPosition, home + new Vector3(0, -0.9f + 0.15f * Mathf.Sin(boatBob * 1.7f), 0), 1f - Mathf.Exp(-3f * dt));
        }

        /// <summary>Depot compound: the kit piece (hall, count line unit, baler house, yard pad, glass bays, office) merged
        /// into one mesh, or the procedural boxes. Medium/High add the baler ram (its own small draw, animated in Update).</summary>
        void BuildDepot()
        {
            if (depotObj != null) Destroy(depotObj);
            if (balerRam != null) { Destroy(balerRam.gameObject); balerRam = null; }
            var d = new MeshKit();
            var dp = DepotPos;
            var km = KitPiece.Load(Low ? "depot_hall_low" : "depot_hall");
            if (km != null) d.Append(km, dp, 0, 1f);
            else
            {
                d.Box(dp + new Vector3(-14, 0, -6), new Vector3(26, 11, 18), 0, Pal.Depot, Pal.RoofGrey);
                d.Box(dp + new Vector3(10, 0, -10), new Vector3(10, 7, 9), 0, Pal.Brand, Pal.RoofGrey);       // baler house
                d.Box(dp + new Vector3(-14, 0, 6), new Vector3(20, 0.4f, 8), 0, Pal.Concrete);             // intake apron
                d.Box(dp + new Vector3(-2, 11, -6), new Vector3(6, 3, 4), 0, Pal.Panel);                  // count line roof unit
            }
            depotObj = Spawn("Depot", d.Build("depot"), Vector3.zero);
            if (!Low)
            {
                var r = new MeshKit();
                r.Box(Vector3.zero, new Vector3(1.6f, 2.6f, 1.6f), 0, Pal.Warning, Pal.Panel);
                balerRam = Spawn("BalerRam", r.Build("baler_ram"), dp + RamBase).transform;
            }
        }

        static readonly Vector3 RamBase = new Vector3(10f, 8.6f, -10f);   // on the press housing of the baler house

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
        GameObject Prop(string kitName, Func<Mesh> fallback, Vector3 pos, float yaw, float kitScale = 1f)
        {
            var prefab = Resources.Load<GameObject>("kit/" + kitName);
            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, transform);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = mat; r.shadowCastingMode = Low ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On; }
                go.transform.localPosition = pos;
                // keep the importer's root rotation (FBX axis conversion) under the yaw, and the prop's map-scale factor
                go.transform.localRotation = Quaternion.Euler(0, yaw, 0) * prefab.transform.localRotation;
                go.transform.localScale = prefab.transform.localScale * kitScale;
            }
            else
            {
                go = Spawn(kitName, fallback(), pos);
                go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            }
            return go;
        }

        /// <summary>The Meshy bales keep their own 1024 / 512 texture (not the palette): an instanced clone of the palette material.</summary>
        Material BaleMaterial(string texPath, bool meshy)
        {
            var m = new Material(mat) { enableInstancing = true, name = texPath };
            var tex = meshy ? Resources.Load<Texture2D>(texPath) : null;
            if (tex != null) { m.mainTexture = tex; if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex); }
            return m;
        }

        static Mesh KitMesh(string path)
        {
            var go = Resources.Load<GameObject>(path);
            var mf = go != null ? go.GetComponentInChildren<MeshFilter>() : null;
            return mf != null ? mf.sharedMesh : null;
        }

        static Mesh BoxMesh(int swatch) { var k = new MeshKit(); k.Box(Vector3.zero, Vector3.one, 0, swatch); return k.Build("box_" + swatch); }
        static Mesh FlatMesh(int swatch, float s) { var k = new MeshKit(); k.Quad(new Vector3(-s, 0.15f, -s), new Vector3(-s, 0.15f, s), new Vector3(s, 0.15f, s), new Vector3(s, 0.15f, -s), swatch); return k.Build("flat_" + swatch); }

        // Kit buildings per district: name, depth across the street (local X, street side = -X), frontage (local Z).
        struct Bld { public string Name; public float Depth; public Bld(string n, float d) { Name = n; Depth = d; } }
        static readonly Bld[] Harbour = { new Bld("bld_harbour_a", 11f), new Bld("bld_harbour_b", 13f) };
        static readonly Bld[] OldTown = { new Bld("bld_old_a", 10f), new Bld("bld_old_b", 12f) };
        static readonly Bld[] Campus = { new Bld("bld_uni_a", 14f), new Bld("bld_uni_b", 11f), new Bld("bld_old_b", 12f) };
        static readonly Bld[] Fair = { new Bld("bld_harbour_b", 13f), new Bld("bld_uni_a", 14f) };
        static readonly int[] Walls = { Pal.WallCream, Pal.WallTerracotta, Pal.WallBlue, Pal.WallCream };

        static readonly Bld[] Works = { new Bld("bld_harbour_b", 13f), new Bld("bld_uni_a", 14f), new Bld("bld_harbour_b", 13f) };
        static readonly Bld[] Resort = { new Bld("bld_harbour_a", 11f), new Bld("bld_uni_b", 11f), new Bld("bld_old_a", 10f) };
        static readonly Bld[] Village = { new Bld("bld_old_a", 10f), new Bld("bld_old_b", 12f) };

        static Bld[] SetFor(string district) => district switch
        {
            "oldtown" => OldTown, "university" => Campus, "stadium" => Fair,
            "industrial" => Works, "riviera" => Resort, "villages" => Village, "island" => Village,
            _ => Harbour,
        };

        /// <summary>Share of street slots that are trees: dense in the villages / island, few on the estate.</summary>
        static double TreeShare(string district) => district switch { "villages" => 0.55, "island" => 0.45, "riviera" => 0.3, "industrial" => 0.08, _ => 0.18 };

        /// <summary>Low: every district in ONE mesh (1 draw for all chapters). Medium/High: one mesh per district.</summary>
        void BuildDistricts()
        {
            foreach (var go in districtObjs.Values)
                if (go != null) { var mf = go.GetComponent<MeshFilter>(); if (mf != null) Destroy(mf.sharedMesh); Destroy(go); }
            districtObjs.Clear();
            if (Low)
            {
                var all = new MeshKit();
                foreach (var dd in Sim.Gd.Districts) AppendDistrict(dd, all);
                districtObjs["all"] = Spawn("Districts", all.Build("districts_low"), Vector3.zero);
                return;
            }
            foreach (var dd in Sim.Gd.Districts)
            {
                var k = new MeshKit();
                AppendDistrict(dd, k);
                districtObjs[dd.Id] = Spawn("District_" + dd.Id, k.Build("district_" + dd.Id), Vector3.zero);
            }
        }

        void AppendDistrict(DistrictDef dd, MeshKit k)
        {
            bool open = Sim.Unlocked(dd.Id);
            var gd = Sim.Gd;
            var rng = new System.Random(StableHash(dd.Id));
            int[] walls = Walls;
            var set = SetFor(dd.Id);
            double trees = TreeShare(dd.Id);
            bool lowKit = Low || !open;   // locked districts are grey silhouettes: the cheap twins are enough
            foreach (var e in gd.Edges)
            {
                var na = gd.Nodes[e.A]; var nb = gd.Nodes[e.B];
                if (na.District != dd.Id || e.Ferry) continue;   // each street belongs to its first node's district (no double rows)
                Vector3 a = ToWorld(na.X, na.Y), b = ToWorld(nb.X, nb.Y);
                var dir = b - a; float len = dir.magnitude; if (len < 30f) continue;
                dir /= len;
                var side = Vector3.Cross(Vector3.up, dir);
                float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                for (float s = 22f; s < len - 22f; s += 17f + (float)rng.NextDouble() * 6f)
                    for (int sd = -1; sd <= 1; sd += 2)
                    {
                        if (rng.NextDouble() < trees) { k.Tree(a + dir * s + side * sd * 16f, 1.4f, yaw); continue; }
                        var bld = set[rng.Next(set.Length)];
                        var km = KitPiece.Load(lowKit ? bld.Name + "_low" : bld.Name);
                        if (km != null)
                        {
                            float sc = 0.9f + (float)rng.NextDouble() * 0.25f;
                            int wallSw = walls[rng.Next(walls.Length)], roofSw = rng.NextDouble() < 0.5 ? Pal.RoofRed : Pal.RoofGrey;
                            // street side (local -X) towards the road: sd = +1 stands on +side (= local +X), so it keeps the yaw
                            k.Append(km, a + dir * s + side * sd * (bld.Depth * sc * 0.5f + 8f), sd > 0 ? yaw : yaw + 180f, sc,
                                open ? wallSw : -1, open ? roofSw : -1, open ? -1 : Pal.Locked);
                            continue;
                        }
                        float w = 10f + (float)rng.NextDouble() * 6f, h = (dd.Id == "oldtown" ? 9f : 7f) + (float)rng.NextDouble() * (dd.Id == "university" ? 22f : 14f);
                        int wall = open ? walls[rng.Next(walls.Length)] : Pal.Locked;
                        int roof = open ? (rng.NextDouble() < 0.5 ? Pal.RoofRed : Pal.RoofGrey) : Pal.Locked;
                        var c = a + dir * s + side * sd * (w * 0.5f + 9f);
                        k.Box(c, new Vector3(w, h, 10f), yaw, wall, roof);
                        if (open && !Low) k.Box(c + Vector3.up * h * 0.55f - side * sd * 5.05f, new Vector3(w * 0.7f, 1.2f, 0.1f), yaw, Pal.Window);
                    }
            }
            if (dd.Id == "stadium")
            {
                var st = ToWorld(6.3, 1.25);
                var km = KitPiece.Load(lowKit ? "stadium_low" : "stadium");
                if (km != null) k.Append(km, st, 0, 1f, -1, -1, open ? -1 : Pal.Locked);
                else k.Box(st, new Vector3(60, 14, 40), 0, open ? Pal.Panel : Pal.Locked, open ? Pal.Grass : Pal.Locked);
            }
        }

        static int PropKind(string kind) => kind == "machine_m" || kind == "machine_l" ? 1 : kind == "shop" ? 2 : kind == "horeca" || kind == "hotel" ? 3 : 0;

        /// <summary>The meshes the Low tier instances, with the same root rotation / scale Prop() gives the kit prefab.</summary>
        void LoadPropMeshes()
        {
            for (int k = 0; k < PropKinds.Length; k++)
            {
                var prefab = Resources.Load<GameObject>("kit/" + PropKinds[k]);
                var mf = prefab != null ? prefab.GetComponentInChildren<MeshFilter>() : null;
                propKit[k] = mf != null && mf.sharedMesh != null;
                if (propKit[k])
                {
                    propMesh[k] = mf.sharedMesh;
                    propLocal[k] = Matrix4x4.TRS(Vector3.zero, prefab.transform.localRotation, prefab.transform.localScale)
                        * (prefab.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix);
                }
                else
                {
                    propMesh[k] = k == 0 ? MeshKit.Machine(false) : k == 1 ? MeshKit.Machine(true) : k == 2 ? MeshKit.Shop() : MeshKit.Horeca();
                    propLocal[k] = Matrix4x4.identity;
                }
            }
        }

        void BuildPoints()
        {
            foreach (var go in pointObjs) Destroy(go);
            pointObjs.Clear();
            foreach (var b in pointBatches) b.Clear();
            for (int i = 0; i < Sim.S.Points.Count; i++)
            {
                var p = Sim.S.Points[i];
                var pos = PointPos(i) + new Vector3(0, 0.2f, 4f);
                float extra = p.Kind == "machine_l" || p.Kind == "hotel" ? 1.35f : 1f;
                if (Low)
                {
                    int pk = PropKind(p.Kind);
                    float ks = (propKit[pk] && pk <= 1 ? MachineKitScale : 1f) * extra;
                    pointBatches[pk].Add(Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one * ks) * propLocal[pk]);
                    continue;
                }
                // fronts face the camera (south, -Z). Blender kit: front = -Y, exported with axis_forward -Z (verify in the editor)
                bool big = p.Kind == "machine_m" || p.Kind == "machine_l";
                GameObject go = p.Kind == "shop" ? Prop("shop", MeshKit.Shop, pos, 0)
                    : p.Kind == "horeca" || p.Kind == "hotel" ? Prop("horeca", MeshKit.Horeca, pos, 0)
                    : Prop(big ? "machine_m" : "machine_s", () => MeshKit.Machine(big), pos, 0, MachineKitScale);
                // chapter 7 large remote machine / chapter 6 hotel: the same props, a size up (no new mesh, no new draw kind)
                if (extra != 1f) go.transform.localScale *= extra;
                go.name = "Point_" + i;
                pointObjs.Add(go);
            }
        }

        // The Blender machines are life-size (1.4 / 2.0 m wide); the map draws props larger than life (the procedural
        // fallback is 3.2 / 4.4 wide), so the kit machines get the same map scale or they vanish next to the buildings.
        const float MachineKitScale = 2.2f;

        /// <summary>String.GetHashCode is randomised per process on .NET Core / some players: a stable FNV-1a keeps the
        /// district layout identical between sessions.</summary>
        static int StableHash(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in s) { h ^= c; h *= 16777619; }
                return (int)(h & 0x7fffffff);
            }
        }

        void SyncTrucks()
        {
            while (truckObjs.Count > Sim.S.Trucks.Count) { Destroy(truckObjs[truckObjs.Count - 1].gameObject); truckObjs.RemoveAt(truckObjs.Count - 1); truckTypes.RemoveAt(truckTypes.Count - 1); }
            for (int i = 0; i < Sim.S.Trucks.Count; i++)
            {
                string type = Sim.S.Trucks[i].Type;
                if (i < truckObjs.Count && truckTypes[i] == type) continue;
                var go = Prop(type, () => MeshKit.Truck(type), DepotPos, 0);
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
                "gold_livery" => new Color(1f, 0.84f, 0.35f),
                "livery_sunset" => new Color(1f, 0.62f, 0.5f),
                "livery_forest" => new Color(0.55f, 0.85f, 0.6f),
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

        void BuildLitter()
        {
            litter.Clear();
            int max = Low ? 120 : 260;
            int n = (int)(max * Math.Max(0, 1.0 - Sim.S.Rating / 100.0) * 1.4);
            var rng = new System.Random(Sim.S.Day * 31 + 7);
            var gd = Sim.Gd;
            for (int i = 0; i < n && gd.Edges.Count > 0; i++)
            {
                var e = gd.Edges[rng.Next(gd.Edges.Count)];
                if (e.Ferry || !Sim.Unlocked(gd.Nodes[e.A].District)) continue;
                Vector3 a = NodePos(e.A), b = NodePos(e.B);
                var p = Vector3.Lerp(a, b, (float)rng.NextDouble()) + new Vector3((float)rng.NextDouble() * 14f - 7f, 0, (float)rng.NextDouble() * 14f - 7f);
                litter.Add(Matrix4x4.TRS(p, Quaternion.Euler(0, rng.Next(360), 0), Vector3.one * (0.6f + (float)rng.NextDouble())));
            }
            builtRating = Sim.S.Rating;
        }

        void BuildYard()
        {
            balesPet.Clear(); balesAlu.Clear(); glassPiles.Clear();
            var basePos = DepotPos + new Vector3(18, 0, 10);
            int k = 0;
            void Stack(List<Matrix4x4> list, int count, Vector3 size)
            {
                for (int i = 0; i < count; i++, k++)
                {
                    int col = k % 6, row = (k / 6) % 4, lay = k / 24;
                    list.Add(Matrix4x4.TRS(basePos + new Vector3(col * 4.4f, lay * 3.6f, row * 4.4f), Quaternion.identity, size));
                }
            }
            Stack(balesPet, Math.Min(48, Sim.S.Depot.Bales[Mats.Pet]), Vector3.one * 3.6f);
            Stack(balesAlu, Math.Min(48, Sim.S.Depot.Bales[Mats.Alu]), Vector3.one * 3.6f);
            for (int i = 0; i < Math.Min(8, Sim.S.Depot.Bales[Mats.Glass]); i++)
                glassPiles.Add(Matrix4x4.TRS(DepotPos + new Vector3(-30 - i * 5f, 0, 14), Quaternion.identity, new Vector3(4.5f, 2f, 4.5f)));
            builtYard = Sim.S.Depot.Yard;
            builtGlass = Sim.S.Depot.Bales[Mats.Glass];
        }

        public List<Bounds> UnlockedBounds()
        {
            var o = new List<Bounds>();
            var b = new Bounds(DepotPos, Vector3.one * 60f);
            foreach (var n in Sim.Gd.Nodes) if (Sim.Unlocked(n.District)) b.Encapsulate(ToWorld(n.X, n.Y));
            b.Expand(new Vector3(40, 0, 40));
            o.Add(b);
            return o;
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            if (Sim == null) return;
            if (dirty)
            {
                dirty = false;
                int dCount = Sim.S.Districts.Count;
                if (lowBuilt != Low)
                {
                    lowBuilt = Low;
                    BuildDistricts();
                    BuildDepot();
                    BuildFerry();
                    BuildPoints();   // GameObjects on Medium/High, instanced batches on Low
                }
                if (dCount != builtDistricts)
                {
                    BuildDistricts();
                    if (builtDistricts >= 0 && rig != null) rig.Fit(UnlockedBounds(), false);
                    builtDistricts = dCount;
                    BuildLitter();
                }
                if (Sim.S.Points.Count != builtPoints || PointKindsChanged()) { BuildPoints(); builtPoints = Sim.S.Points.Count; }
                if (Math.Abs(builtRating - Sim.S.Rating) > 2) BuildLitter();
                SyncTrucks();
            }
            if (Sim.S.Depot.Yard != builtYard || Sim.S.Depot.Bales[Mats.Glass] != builtGlass) BuildYard();

            float dt = Time.deltaTime;
            AnimateRam(dt);
            AnimateFerry(dt);
            slowAcc += dt;
            bool tick = !Low || slowAcc >= 1f / 15f;
            if (tick)
            {
                float step = Low ? slowAcc : dt;
                slowAcc = 0f;
                MoveTrucks(step);
                crowd.Tick(step);
            }
            // instanced draws (no GameObjects)
            if (litter.Count > 0) Graphics.RenderMeshInstanced(new RenderParams(matInst), litterMesh, 0, litter);
            if (balesPet.Count > 0) Graphics.RenderMeshInstanced(new RenderParams(matBalePet), baleMeshPet, 0, balesPet);
            if (balesAlu.Count > 0) Graphics.RenderMeshInstanced(new RenderParams(matBaleAlu), baleMeshAlu, 0, balesAlu);
            if (glassPiles.Count > 0) Graphics.RenderMeshInstanced(new RenderParams(matInst), glassMesh, 0, glassPiles);
            for (int k = 0; k < pointBatches.Length; k++)
                if (pointBatches[k].Count > 0) Graphics.RenderMeshInstanced(new RenderParams(matInst), propMesh[k], 0, pointBatches[k]);
            crowd.Draw();
        }

        /// <summary>Medium/High: the baler ram presses while a bale is being made (no allocation; Low has no ram).</summary>
        void AnimateRam(float dt)
        {
            if (balerRam == null) return;
            var dep = Sim.S.Depot;
            bool pressing = !dep.BalerDown && (dep.Press[Mats.Pet] > 0 || dep.Press[Mats.Alu] > 0);
            const float Tau = Mathf.PI * 2f;
            ramPhase = pressing ? Mathf.Repeat(ramPhase + dt * 3.2f, Tau) : Mathf.MoveTowards(ramPhase, ramPhase > Mathf.PI ? Tau : 0f, dt * 3.2f);
            float lift = 0.5f + 0.5f * Mathf.Cos(ramPhase);   // 1 = up (rest), 0 = down
            balerRam.localPosition = DepotPos + RamBase + new Vector3(0, 1.4f * lift - 1.4f, 0);
        }

        readonly List<string> lastKinds = new List<string>();
        bool PointKindsChanged()
        {
            bool changed = lastKinds.Count != Sim.S.Points.Count;
            for (int i = 0; i < Sim.S.Points.Count && !changed; i++) changed = lastKinds[i] != Sim.S.Points[i].Kind;
            if (!changed) return false;
            lastKinds.Clear();
            foreach (var p in Sim.S.Points) lastKinds.Add(p.Kind);
            return true;
        }

        void MoveTrucks(float dt)
        {
            for (int i = 0; i < truckObjs.Count && i < Sim.S.Trucks.Count; i++)
            {
                var t = Sim.S.Trucks[i];
                var (x, y, heading) = Sim.TruckPos(t);
                var target = ToWorld(x, y) + new Vector3(0, 0.2f, 0);
                if (!t.Busy) target = DepotPos + new Vector3(-20 + i * 6f, 0.2f, 14f);   // parked in a row at the depot
                var tr = truckObjs[i];
                float k = 1f - Mathf.Exp(-12f * dt);   // smooth the 15 Hz / step jumps
                tr.localPosition = Vector3.Lerp(tr.localPosition, target, k);
                if (t.Status == TruckStatus.Drive) tr.localRotation = Quaternion.Slerp(tr.localRotation, Quaternion.Euler(0, (float)(heading * Mathf.Rad2Deg), 0), k);
            }
        }

        /// <summary>Sim events -> walkers (deposit / turned away).</summary>
        public void OnSimEvent(SimEvent e)
        {
            if (e.Type == "deposit" || e.Type == "turned") crowd.Spawn(e.A, e.B, e.Type == "turned");
            else if (e.Type == "district" || e.Type == "point_new" || e.Type == "bought" || e.Type == "day_start") MarkDirty();
        }

        // ------------------------------------------------------------------ picking

        void OnTap(Vector2 topLeft)
        {
            var hit = Pick(new Vector2(topLeft.x, Screen.height - topLeft.y));
            Picked?.Invoke(hit);
        }

        /// <summary>Screen px (bottom-left origin) -> what is there. Trucks win over points, points over the depot.</summary>
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
            float best = pickR * pickR;
            var res = none;
            for (int i = 0; i < truckObjs.Count; i++)
            {
                float d = (truckObjs[i].localPosition - g).sqrMagnitude;
                if (d < best * 0.8f) { best = d; res = new WorldHit { Kind = "truck", Index = i }; }
            }
            if (res.Kind == "truck") return res;
            best = pickR * pickR;
            for (int i = 0; i < Sim.S.Points.Count; i++)
            {
                float d = (PointPos(i) - g).sqrMagnitude;
                if (d < best) { best = d; res = new WorldHit { Kind = "point", Index = i }; }
            }
            if (res.Kind == "point") return res;
            if ((DepotPos - g).sqrMagnitude < 45f * 45f) return new WorldHit { Kind = "depot", Index = 0 };
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

        public Vector3 TruckWorld(int i) => i >= 0 && i < truckObjs.Count ? truckObjs[i].localPosition : DepotPos;
    }
}
