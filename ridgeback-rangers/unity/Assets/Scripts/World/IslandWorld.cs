// Ridgeback Rangers — the 2.5D diorama of Ridgeback Isle (plan §8). Built ONCE from the map data (island, sea, service
// road, tram rail, volcano backdrop, trees and ferns, the buildings), rebuilt only when something structural changes (a zone
// opened, a paddock built, a fence upgraded or knocked loose); dinosaurs / rangers / visitors / the tram update from the sim
// each frame (Low: 15 Hz).
//   * 1 km of map = 100 world units; props and animals are drawn larger than life so they read at map zoom.
//   * one palette material for the island (MeshKit + the Blender kit) + one per species for the Meshy dinosaurs;
//     instanced draws for animals and people, merged meshes for every zone / paddock.
//   * Low tier: no shadows (QualityTiers), ≤ 24 animals / half the visitors at 15 Hz, no foliage sway, no lava glow.
//   * picking: a tap ray-casts the ground plane (no physics module) and picks an animal, the tram, the gate, a paddock or an
//     empty paddock slot.
using System;
using System.Collections.Generic;
using CoddyKit;
using UnityEngine;
using RidgebackRangers.Sim;

namespace RidgebackRangers.World
{
    public struct WorldHit
    {
        public string Kind;   // animal | tram | gate | paddock | slot | none
        public int Index;
        public override string ToString() => Kind + ":" + Index;
    }

    public sealed class IslandWorld : MonoBehaviour
    {
        public const float Scale = 100f;   // world units per map km
        public static readonly Vector2 PaddockSize = new Vector2(40f, 28f);

        public Camera cam;
        public CameraRig rig;
        public TapInput tap;
        public Light sun;

        public event Action<WorldHit> Picked;
        public Simulation Sim { get; private set; }
        public bool Low { get; private set; }
        public string TramSkin = "";

        Material mat, matInst;
        readonly Dictionary<string, GameObject> zoneObjs = new Dictionary<string, GameObject>();
        readonly List<GameObject> paddockObjs = new List<GameObject>();
        readonly List<Transform> tramCars = new List<Transform>();
        GameObject railObj;
        Herd herd;
        People people;
        string builtKey = "", builtZones = "";
        bool dirty = true;
        float slowAcc;
        MaterialPropertyBlock mpb;

        public static Vector3 ToWorld(double x, double y) => new Vector3((float)(x * Scale), 0f, (float)(y * Scale));
        public Vector3 NodePos(int node) { var n = Sim.Gd.Nodes[node]; return ToWorld(n.X, n.Y); }
        public Vector3 PaddockCenter(int p) => p >= 0 && p < Sim.S.Paddocks.Count ? NodePos(Sim.S.Paddocks[p].Node) : Vector3.zero;
        public Vector3 GatePos => NodePos(Sim.Gd.GateNode);
        public Vector3 StationPos => NodePos(Sim.Gd.StationNode);
        public Vector3 HatcheryPos => NodePos(Sim.Gd.HatcheryNode);

        public void Init(Simulation sim, bool low)
        {
            Sim = sim;
            Low = low;
            mpb = new MaterialPropertyBlock();
            mat = Resources.Load<Material>(low ? "materials/palette_low" : "materials/palette") ?? Resources.Load<Material>("materials/palette");
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")); mat.mainTexture = Resources.Load<Texture2D>("materials/palette"); }
            matInst = new Material(mat) { enableInstancing = true, name = "palette_instanced" };
            BuildStatic();
            herd = new Herd(this, mat, low);
            people = new People(this, matInst, low);
            if (tap != null) tap.Tapped += OnTap;
            if (rig != null) rig.Fit(UnlockedBounds(), true);
            MarkDirty();
        }

        public void SetLow(bool low)
        {
            if (low == Low) return;
            Low = low;
            herd?.SetLow(low);
            people?.SetLow(low);
            MarkDirty();
        }

        public void MarkDirty() => dirty = true;

        // ------------------------------------------------------------------ static scene

        void BuildStatic()
        {
            var gd = Sim.Gd;
            var k = new MeshKit();
            // sea, beach ring, island (ellipses)
            k.Quad(new Vector3(-400, -0.6f, -400), new Vector3(-400, -0.6f, 700), new Vector3(700, -0.6f, 700), new Vector3(700, -0.6f, -400), Pal.Water);
            var c = new Vector3(150, 0, 125);
            k.Disc(c, 205, -0.1f, Pal.Sand, 28, 170);
            k.Disc(c, 192, 0f, Pal.Grass, 28, 158);
            // the volcano behind the sanctuary (a cheap cone; the lava glow is Medium/High only, see Update)
            k.Cone(new Vector3(150, 0, 300), 95, 70, Pal.VolcanoRock, 14, 16, Pal.Lava);
            k.Cone(new Vector3(40, 0, 285), 55, 32, Pal.GrassDark, 10, 6);
            k.Cone(new Vector3(265, 0, 285), 60, 38, Pal.GrassDark, 10, 6);
            // the service road: one strip per edge, discs on the nodes
            foreach (var e in gd.Edges)
                k.Strip(ToWorld(gd.Nodes[e.A].X, gd.Nodes[e.A].Y), ToWorld(gd.Nodes[e.B].X, gd.Nodes[e.B].Y), 6f, 0.1f, Pal.Path);
            foreach (var n in gd.Nodes) if (n.Kind != "paddock") k.Disc(ToWorld(n.X, n.Y), n.Kind == "dig" ? 9f : 5f, 0.11f, n.Kind == "dig" ? Pal.Mud : Pal.Path);
            // the gate plaza and the beach in front of it
            k.Disc(GatePos + new Vector3(0, 0, 8), 16, 0.12f, Pal.Stone, 12);
            Spawn("Ground", k.Build("ground"), Vector3.zero);
            // buildings (Blender kit, procedural fallback)
            Prop("gate", "gate", GatePos, 0);
            Prop("hq", "hq", NodePos(gd.HqNode), 0);
            Prop("barn", "barn", NodePos(gd.BarnNode), 0);
            Prop("vet", "vet", NodePos(gd.VetNode), 0);
            Prop("hatchery", "hatchery", HatcheryPos, 0);
            Prop("station", "station", StationPos + new Vector3(0, 0, 4), 0);
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

        /// <summary>Blender-kit model when present (Resources/kit/&lt;name&gt;), else the procedural fallback.</summary>
        GameObject Prop(string kitName, string fallback, Vector3 pos, float yaw, Transform parent = null)
        {
            var prefab = Resources.Load<GameObject>("kit/" + kitName);
            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, parent != null ? parent : transform);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = mat; r.shadowCastingMode = Low ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On; }
                go.transform.localPosition = pos;
                go.transform.localScale = Vector3.one * 2.2f;   // kit props are 1 unit = 1 m; the diorama draws them larger than life
            }
            else go = Spawn(kitName, kitName == "tram_car" ? MeshKit.TramCar() : MeshKit.Building(fallback), pos, parent);
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            return go;
        }

        void BuildZone(ZoneDef z)
        {
            if (zoneObjs.TryGetValue(z.Id, out var old)) Destroy(old);
            bool open = Sim.Unlocked(z.Id);
            var gd = Sim.Gd;
            var root = new GameObject("Zone_" + z.Id);
            root.transform.SetParent(transform, false);
            var rng = new System.Random(z.Id.GetHashCode() & 0x7fffffff);
            var k = new MeshKit();
            // the zone's ground patch (dune = sand, forest = darker grass) + foliage between the paddocks
            Vector3 zc = Vector3.zero; int nn = 0;
            foreach (var n in gd.Nodes) if (n.Zone == z.Id && (n.Kind == "paddock" || n.Kind == "view")) { zc += ToWorld(n.X, n.Y); nn++; }
            if (nn > 0) zc /= nn;
            if (z.Id != "cove") k.Disc(zc, 70, 0.05f, open ? (z.Biome == "dune" ? Pal.Sand : Pal.GrassDark) : Pal.Locked, 16, 55);
            int trees = Low ? 14 : 28;
            for (int i = 0; i < trees; i++)
            {
                var p = zc + new Vector3((float)(rng.NextDouble() * 2 - 1) * 70f, 0, (float)(rng.NextDouble() * 2 - 1) * 55f);
                if (NearAny(p, 26f)) continue;
                if (z.Biome == "dune") { if (rng.NextDouble() < 0.5) k.Box(p, new Vector3(3f, 2f, 2.5f), rng.Next(90), Pal.Rock); else k.Fern(p, 1.6f, rng.Next(360)); }
                else if (rng.NextDouble() < 0.5) k.Tree(p, 1.8f, rng.Next(360), open ? Pal.Tree : Pal.Locked);
                else k.Fern(p, 1.7f, rng.Next(360));
            }
            Spawn("Ground_" + z.Id, k.Build("zone_" + z.Id), Vector3.zero, root.transform);
            int view = gd.ViewOf(z.Id);
            if (open && view >= 0) Prop("view", "view", NodePos(view) + new Vector3(0, 0, 4), 0, root.transform);
            zoneObjs[z.Id] = root;
        }

        bool NearAny(Vector3 p, float r)
        {
            foreach (var n in Sim.Gd.Nodes) if ((ToWorld(n.X, n.Y) - p).sqrMagnitude < r * r) return true;
            foreach (var e in Sim.Gd.Edges)
            {
                Vector3 a = NodePos(e.A), b = NodePos(e.B), ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-3f, ab.sqrMagnitude));
                if ((a + ab * t - p).sqrMagnitude < 8f * 8f) return true;
            }
            return false;
        }

        void BuildPaddocks()
        {
            foreach (var go in paddockObjs) Destroy(go);
            paddockObjs.Clear();
            var gd = Sim.Gd;
            for (int p = 0; p < Sim.S.Paddocks.Count; p++)
            {
                var pd = Sim.S.Paddocks[p];
                var c = NodePos(pd.Node);
                var k = new MeshKit();
                bool dune = Sim.BiomeOf(pd) == "dune";
                k.Disc(c, PaddockSize.x * 0.5f, 0.08f, dune ? Pal.Sand : Pal.Grass, 12, PaddockSize.y * 0.5f);
                k.Fence(c, PaddockSize.x, PaddockSize.y, pd.Fence, (float)pd.Integrity);
                if (pd.Water) k.Disc(c + new Vector3(12, 0, 7), 4.5f, 0.15f, Pal.Water, 10, 3f);
                if (pd.Shade) { k.Box(c + new Vector3(-12, 0, 7), new Vector3(0.4f, 5f, 0.4f), 0, Pal.Wood); k.Box(c + new Vector3(-12, 5, 7), new Vector3(8f, 0.2f, 6f), 15, Pal.Sunset); }
                var go = Spawn("Paddock_" + p, k.Build("paddock_" + p), Vector3.zero);
                paddockObjs.Add(go);
            }
            // empty slots in open zones: a dashed outline (the "+" build marker is UI)
            var slots = new MeshKit();
            foreach (var n in gd.Nodes)
            {
                if (n.Kind != "paddock" || !Sim.Unlocked(n.Zone) || Sim.PaddockAt(n.Index) >= 0) continue;
                var c = ToWorld(n.X, n.Y);
                for (int i = 0; i < 12; i++)
                {
                    float a = i / 12f * Mathf.PI * 2f;
                    slots.Box(c + new Vector3(Mathf.Cos(a) * PaddockSize.x * 0.5f, 0, Mathf.Sin(a) * PaddockSize.y * 0.5f), new Vector3(1.2f, 0.6f, 1.2f), 0, Pal.Wood);
                }
            }
            if (slots.Vertices > 0) paddockObjs.Add(Spawn("Slots", slots.Build("slots"), Vector3.zero));
        }

        void BuildRail()
        {
            if (railObj != null) Destroy(railObj);
            var k = new MeshKit();
            var stops = Sim.TramStops();
            for (int i = 0; i < stops.Count; i++)
            {
                var a = NodePos(stops[i]) + new Vector3(6, 0, 0);
                var b = NodePos(stops[(i + 1) % stops.Count]) + new Vector3(6, 0, 0);
                k.Strip(a, b, 3.2f, 0.14f, Pal.Wood);
                k.Strip(a, b, 0.5f, 0.2f, Pal.Steel);
            }
            railObj = Spawn("Rail", k.Build("rail"), Vector3.zero);
        }

        void SyncTram()
        {
            while (tramCars.Count > Sim.S.Tram.Cars) { Destroy(tramCars[tramCars.Count - 1].gameObject); tramCars.RemoveAt(tramCars.Count - 1); }
            while (tramCars.Count < Sim.S.Tram.Cars)
            {
                var go = Prop("tram_car", "tram_car", StationPos, 0);
                go.name = "TramCar_" + tramCars.Count;
                tramCars.Add(go.transform);
            }
            ApplySkin();
        }

        public void SetTramSkin(string skin) { TramSkin = skin ?? ""; ApplySkin(); }

        void ApplySkin()
        {
            Color tint = TramSkin switch
            {
                "golden_tram" => new Color(1f, 0.84f, 0.35f),
                "skin_tram_sunset" => new Color(1f, 0.62f, 0.5f),
                "skin_tram_jungle" => new Color(0.55f, 0.85f, 0.6f),
                "skin_tram_glacier" => new Color(0.6f, 0.85f, 1f),
                _ => Color.white,
            };
            foreach (var t in tramCars)
                foreach (var r in t.GetComponentsInChildren<Renderer>())
                {
                    r.GetPropertyBlock(mpb);
                    mpb.SetColor("_BaseColor", tint);
                    r.SetPropertyBlock(mpb);
                }
        }

        public List<Bounds> UnlockedBounds()
        {
            var o = new List<Bounds>();
            var b = new Bounds(GatePos, Vector3.one * 40f);
            foreach (var n in Sim.Gd.Nodes) if (Sim.Unlocked(n.Zone) && n.Kind != "dig") b.Encapsulate(ToWorld(n.X, n.Y));
            b.Expand(new Vector3(50, 0, 40));
            o.Add(b);
            return o;
        }

        // ------------------------------------------------------------------ frame

        string PaddockKey()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var p in Sim.S.Paddocks) sb.Append(p.Node).Append(p.Fence).Append(p.Water ? 'w' : '-').Append(p.Shade ? 's' : '-').Append(p.Integrity < 0.6 ? 2 : p.Integrity < 0.85 ? 1 : 0).Append(',');
            return sb.ToString();
        }

        void Update()
        {
            if (Sim == null) return;
            string zones = string.Join(",", Sim.S.Zones);
            if (dirty || zones != builtZones)
            {
                dirty = false;
                if (zones != builtZones)
                {
                    foreach (var z in Sim.Gd.Zones) BuildZone(z);
                    BuildRail();
                    if (builtZones != "" && rig != null) rig.Fit(UnlockedBounds(), false);
                    builtZones = zones;
                    builtKey = "";
                }
                SyncTram();
            }
            string key = PaddockKey();
            if (key != builtKey) { BuildPaddocks(); builtKey = key; }
            if (tramCars.Count != Sim.S.Tram.Cars) SyncTram();

            float dt = Time.deltaTime;
            slowAcc += dt;
            bool tick = !Low || slowAcc >= 1f / 15f;
            if (tick)
            {
                float step = Low ? slowAcc : dt;
                slowAcc = 0f;
                MoveTram(step);
                herd.Tick(step);
                people.Tick(step);
            }
            herd.Draw();
            people.Draw();
        }

        void MoveTram(float dt)
        {
            var t = Sim.S.Tram;
            for (int i = 0; i < tramCars.Count; i++)
            {
                double km = t.Running ? t.Pos - i * 0.07 : -i * 0.07;
                var (x, y, h) = Sim.TramPoint(km);
                var target = ToWorld(x, y) + new Vector3(6, 0.2f, 0);
                var tr = tramCars[i];
                float k = 1f - Mathf.Exp(-10f * dt);
                tr.localPosition = Vector3.Lerp(tr.localPosition, target, k);
                tr.localRotation = Quaternion.Slerp(tr.localRotation, Quaternion.Euler(0, (float)(h * Mathf.Rad2Deg), 0), k);
            }
        }

        /// <summary>Sim events -> walkers / rebuilds.</summary>
        public void OnSimEvent(SimEvent e)
        {
            if (e.Type == "arrive") people.Arrive(e.A);
            else if (e.Type == "zone" || e.Type == "paddock_new" || e.Type == "bought" || e.Type == "day_start") MarkDirty();
        }

        // ------------------------------------------------------------------ picking

        void OnTap(Vector2 topLeft)
        {
            var hit = Pick(new Vector2(topLeft.x, Screen.height - topLeft.y));
            Picked?.Invoke(hit);
        }

        /// <summary>Screen px (bottom-left origin) -> what is there. Animals win over the tram, the tram over buildings, those over paddocks.</summary>
        public WorldHit Pick(Vector2 screen)
        {
            var none = new WorldHit { Kind = "none", Index = -1 };
            if (cam == null || Sim == null) return none;
            var ray = cam.ScreenPointToRay(screen);
            if (Mathf.Abs(ray.direction.y) < 1e-4f) return none;
            float dist = -ray.origin.y / ray.direction.y;
            if (dist < 0) return none;
            var g = ray.origin + ray.direction * dist;
            float z = Mathf.Max(0.6f, rig != null ? rig.Zoom : 1f);
            int a = herd.Nearest(g, 7f * z);
            if (a >= 0) return new WorldHit { Kind = "animal", Index = a };
            foreach (var t in tramCars) if ((t.localPosition - g).sqrMagnitude < 81f * z * z) return new WorldHit { Kind = "tram", Index = 0 };
            if ((StationPos - g).sqrMagnitude < 144f * z * z) return new WorldHit { Kind = "tram", Index = 0 };
            if ((GatePos - g).sqrMagnitude < 144f * z * z) return new WorldHit { Kind = "gate", Index = 0 };
            for (int p = 0; p < Sim.S.Paddocks.Count; p++)
            {
                var c = PaddockCenter(p);
                if (Mathf.Abs(g.x - c.x) < PaddockSize.x * 0.5f && Mathf.Abs(g.z - c.z) < PaddockSize.y * 0.5f) return new WorldHit { Kind = "paddock", Index = p };
            }
            foreach (var n in Sim.Gd.Nodes)
            {
                if (n.Kind != "paddock" || !Sim.Unlocked(n.Zone) || Sim.PaddockAt(n.Index) >= 0) continue;
                var c = ToWorld(n.X, n.Y);
                if (Mathf.Abs(g.x - c.x) < PaddockSize.x * 0.5f && Mathf.Abs(g.z - c.z) < PaddockSize.y * 0.5f) return new WorldHit { Kind = "slot", Index = n.Index };
            }
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

        public Vector3 TramWorld => tramCars.Count > 0 ? tramCars[0].localPosition : StationPos;
        public Vector3 AnimalWorld(int i) => i >= 0 && i < Sim.S.Animals.Count ? herd.AnimalPos(Sim.S.Animals[i].Id) ?? PaddockCenter(Sim.S.Animals[i].Paddock) : Vector3.zero;
    }
}
