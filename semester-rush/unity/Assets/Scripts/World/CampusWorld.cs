// Semester Rush — the 2.5D campus diorama (plan section 8). Built from the map data (lawns, paths, zone trees, the bus
// stop, gates, buildings / plots / scaffolding), rebuilt only when something structural changes (a zone opens, a build
// starts or ends, a gate opens); the bell-change crowd and heat ribbon come from the sim each frame (Low: 15 Hz).
//   * 1 hectometre of map = 30 world units; props are drawn larger than life (×1.6) so they read at campus zoom.
//   * one palette material for everything (MeshKit) + instanced draws for walkers, dots and ribbons.
//   * Low tier: no shadows (QualityTiers), half the walkers at 15 Hz, half the trees, no hero oak.
//   * campus dressing (polish pass): Blender trees (oak / autumn maple), lamps, benches, bike racks and the shortcut gates
//     from Resources/kit/merge are MERGED into the zone / path meshes — 0 extra draws on every tier (the gates even save
//     their own draws). Medium / High only; Low keeps the cheaper procedural trees and gates (Low must not get heavier).
//   * campus themes (cosmetic IAP) tint the buildings with a property block (no extra material, no extra pass).
//   * picking: a tap ray-casts the ground plane (no physics module) and picks the nearest building / plot / gate.
using System;
using System.Collections.Generic;
using CoddyKit;
using UnityEngine;
using SemesterRush.Sim;

namespace SemesterRush.World
{
    public struct WorldHit
    {
        public string Kind;   // hall | building | site | gate | none
        public int Index;     // node (hall / building / site) or edge (gate)
        public override string ToString() => Kind + ":" + Index;
    }

    public sealed class CampusWorld : MonoBehaviour
    {
        public const float Scale = 30f;       // world units per hectometre
        public const float PropScale = 1.6f;  // Blender metres -> world units

        public Camera cam;
        public CameraRig rig;
        public TapInput tap;
        public Light sun;

        public event Action<WorldHit> Picked;
        public Simulation Sim { get; private set; }
        public bool Low { get; private set; }
        public string Theme = "";

        Material mat, matInst;
        readonly Dictionary<string, GameObject> zoneObjs = new Dictionary<string, GameObject>();
        readonly Dictionary<int, GameObject> nodeObjs = new Dictionary<int, GameObject>();
        GameObject paths, oak;
        Crowd crowd;
        string builtKey = "";
        bool dirty = true;
        float slowAcc;
        MaterialPropertyBlock mpb;

        public static Vector3 ToWorld(double x, double y) => new Vector3((float)(x * Scale), 0f, (float)(y * Scale));
        public Vector3 NodePos(int node) { var n = Sim.Gd.Nodes[node]; return ToWorld(n.X, n.Y); }
        public Vector3 EdgeMid(int e) => (NodePos(Sim.Gd.Edges[e].A) + NodePos(Sim.Gd.Edges[e].B)) * 0.5f;

        public void Init(Simulation sim, bool low)
        {
            Sim = sim;
            Low = low;
            mpb = new MaterialPropertyBlock();
            mat = Resources.Load<Material>(low ? "materials/palette_low" : "materials/palette") ?? Resources.Load<Material>("materials/palette");
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")); mat.mainTexture = Resources.Load<Texture2D>("materials/palette"); }
            matInst = new Material(mat) { enableInstancing = true, name = "palette_instanced" };
            crowd = new Crowd(this, matInst, low);
            BuildGround();
            if (tap != null) tap.Tapped += OnTap;
            if (rig != null) rig.Fit(UnlockedBounds(), true);
            MarkDirty();
        }

        public void SetLow(bool low)
        {
            if (low == Low) return;
            Low = low;
            crowd?.SetLow(low);
            foreach (var z in zoneObjs.Values) Destroy(z);
            zoneObjs.Clear();
            builtKey = "";
            MarkDirty();
        }

        public void MarkDirty() => dirty = true;

        // ------------------------------------------------------------------ path geometry (crowd)

        /// <summary>A point a fraction f along a node path (by length).</summary>
        public Vector3 PathPoint(List<int> path, float f)
        {
            if (path.Count == 1) return NodePos(path[0]);
            float total = 0;
            for (int i = 1; i < path.Count; i++) total += Vector3.Distance(NodePos(path[i - 1]), NodePos(path[i]));
            float left = Mathf.Clamp01(f) * total;
            for (int i = 1; i < path.Count; i++)
            {
                Vector3 a = NodePos(path[i - 1]), b = NodePos(path[i]);
                float seg = Vector3.Distance(a, b);
                if (left <= seg || i == path.Count - 1) return Vector3.Lerp(a, b, seg <= 0 ? 1 : Mathf.Clamp01(left / seg));
                left -= seg;
            }
            return NodePos(path[path.Count - 1]);
        }

        public Vector3 PathDir(List<int> path, float f)
        {
            var a = PathPoint(path, Mathf.Max(0, f - 0.02f));
            var b = PathPoint(path, Mathf.Min(1, f + 0.02f));
            var d = b - a; d.y = 0;
            return d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward;
        }

        // ------------------------------------------------------------------ static scene

        void BuildGround()
        {
            var k = new MeshKit();
            k.Quad(new Vector3(-150, 0, -90), new Vector3(-150, 0, 480), new Vector3(560, 0, 480), new Vector3(560, 0, -90), Pal.GrassDark);
            k.Strip(new Vector3(-150, 0, -36), new Vector3(560, 0, -36), 22f, 0.04f, Pal.Slate);   // the town road
            var ent = NodePos(Sim.Gd.EntranceNode);
            k.Strip(ent, new Vector3(ent.x, 0, -36), 10f, 0.05f, Pal.Path);
            Spawn("Ground", k.Build("ground"), Vector3.zero);
            var bus = Prop("bus_stop", MeshKit.BusStop, ent + new Vector3(0, 0, -14f), 0);
            bus.name = "BusStop";
        }

        void BuildZone(ZoneDef z)
        {
            if (zoneObjs.TryGetValue(z.Id, out var old)) Destroy(old);
            bool open = Sim.Unlocked(z.Id);
            var gd = Sim.Gd;
            var nodes = gd.Nodes.FindAll(n => n.Zone == z.Id);
            if (nodes.Count == 0) return;
            var k = new MeshKit();
            // the zone lawn: a padded rectangle around its nodes
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (var n in nodes) { var p = NodePos(n.Index); minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z); }
            minX -= 30; minZ -= 26; maxX += 30; maxZ += 26;
            k.Quad(new Vector3(minX, 0.05f, minZ), new Vector3(minX, 0.05f, maxZ), new Vector3(maxX, 0.05f, maxZ), new Vector3(maxX, 0.05f, minZ), open ? Pal.Grass : Pal.Locked);
            // trees away from nodes and paths (deterministic per zone)
            var rng = new System.Random(z.Id.GetHashCode() & 0x7fffffff);
            int trees = open ? (Low ? 14 : 30) : (Low ? 6 : 12);
            KitGeo oakG = Low ? null : KitGeo.Load("tree_oak"), mapleG = Low ? null : KitGeo.Load("tree_maple");
            for (int i = 0, tries = 0; i < trees && tries < 400; tries++)
            {
                var p = new Vector3(minX + (float)rng.NextDouble() * (maxX - minX), 0.05f, minZ + (float)rng.NextDouble() * (maxZ - minZ));
                if (NearNodeOrPath(p, 14f)) continue;
                float size = 1.6f + (float)rng.NextDouble() * 0.8f;
                int yaw = rng.Next(90);
                var g = i % 3 == 2 && mapleG != null ? mapleG : oakG;
                if (g != null) k.Add(g, p, yaw * 4f, size * 1.1f);
                else k.Tree(p, size, yaw);
                i++;
            }
            zoneObjs[z.Id] = Spawn("Zone_" + z.Id, k.Build("zone_" + z.Id), Vector3.zero);
        }

        bool NearNodeOrPath(Vector3 p, float r)
        {
            var gd = Sim.Gd;
            foreach (var n in gd.Nodes) if ((NodePos(n.Index) - p).sqrMagnitude < (r + 10) * (r + 10)) return true;
            foreach (var e in gd.Edges)
            {
                Vector3 a = NodePos(e.A), b = NodePos(e.B);
                var ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-3f, ab.sqrMagnitude));
                if ((a + ab * t - p).sqrMagnitude < r * r) return true;
            }
            return false;
        }

        void BuildPaths()
        {
            if (paths != null) Destroy(paths);
            var gd = Sim.Gd;
            var k = new MeshKit();
            foreach (var e in gd.Edges)
            {
                bool zonesOpen = Sim.Unlocked(gd.Nodes[e.A].Zone) && Sim.Unlocked(gd.Nodes[e.B].Zone);
                if (!zonesOpen) continue;
                Vector3 a = NodePos(e.A), b = NodePos(e.B);
                if (e.Type == "shuttle") { if (Sim.Has("shuttle")) k.Strip(a, b, 6f, 0.09f, Pal.Slate); continue; }
                if (e.Type == "gate" && !Sim.S.OpenGates.Contains(e.Index)) { k.Strip(a, b, 4f, 0.08f, Pal.Sand); continue; }
                float w = Sim.S.Widened.Contains(e.Index) ? 9f : 6f;
                k.Strip(a, b, w, 0.08f, Pal.Path);
            }
            foreach (var n in gd.Nodes) if (Sim.Unlocked(n.Zone)) k.Disc(NodePos(n.Index), n.Kind == "junction" ? 6f : 9f, 0.09f, n.Kind == "junction" ? Pal.Path : Pal.Stone);
            AddGates(k);
            if (!Low) AddDressing(k);
            paths = Spawn("Paths", k.Build("paths"), Vector3.zero);
        }

        /// <summary>The shortcut gates, merged into the paths mesh (rebuilt when one opens): Blender model on Medium / High,
        /// the procedural one on Low or when the kit file is missing.</summary>
        void AddGates(MeshKit k)
        {
            var gd = Sim.Gd;
            KitGeo openG = Low ? null : KitGeo.Load("gate_open"), closedG = Low ? null : KitGeo.Load("gate_closed");
            MeshKit openP = null, closedP = null;
            foreach (var e in gd.Edges)
            {
                if (e.Type != "gate" || !Sim.Unlocked(gd.Nodes[e.A].Zone) || !Sim.Unlocked(gd.Nodes[e.B].Zone)) continue;
                bool open = Sim.S.OpenGates.Contains(e.Index);
                var d = NodePos(e.B) - NodePos(e.A);
                float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                var pos = EdgeMid(e.Index);
                var g = open ? openG : closedG;
                if (g != null) { k.Add(g, pos, yaw, PropScale); continue; }
                var proc = open ? (openP ??= MeshKit.GateKit(true)) : (closedP ??= MeshKit.GateKit(false));
                k.Add(proc, pos, yaw, PropScale);
            }
        }

        /// <summary>Medium / High: lamps and benches along the open paths, bike racks at the teaching buildings.</summary>
        void AddDressing(MeshKit k)
        {
            var gd = Sim.Gd;
            KitGeo lamp = KitGeo.Load("lamp"), bench = KitGeo.Load("bench"), rack = KitGeo.Load("bike_rack");
            const float spacing = 38f, clear = 16f;
            foreach (var e in gd.Edges)
            {
                if (e.Type != "path" || !Sim.Unlocked(gd.Nodes[e.A].Zone) || !Sim.Unlocked(gd.Nodes[e.B].Zone)) continue;
                Vector3 a = NodePos(e.A), b = NodePos(e.B);
                var dir = b - a; dir.y = 0;
                float len = dir.magnitude;
                if (len < clear * 2f) continue;
                dir /= len;
                var side = Vector3.Cross(Vector3.up, dir);
                float half = (Sim.S.Widened.Contains(e.Index) ? 9f : 6f) * 0.5f;
                int slot = 0;
                for (float f = clear; f <= len - clear; f += spacing, slot++)
                {
                    var at = a + dir * f;
                    if ((slot & 1) == 0) { if (lamp != null) k.Add(lamp, at + side * (half + 2.5f) + Vector3.up * 0.05f, 0, PropScale); }
                    else if (bench != null)
                    {
                        var s = -side;   // across the path, facing it
                        k.Add(bench, at + s * (half + 2.5f) + Vector3.up * 0.05f, Mathf.Atan2(s.x, s.z) * Mathf.Rad2Deg, PropScale);
                    }
                }
            }
            if (rack == null) return;
            foreach (var b in Sim.S.Buildings)
            {
                if (!b.Built || !Sim.Unlocked(gd.Nodes[b.Node].Zone)) continue;
                var def = Sim.DefOf(b);
                if (!def.Teaching && b.Def != "library") continue;
                k.Add(rack, NodePos(b.Node) + new Vector3(13f, 0.1f, -1f), 0, PropScale);
            }
        }

        void BuildNodes()
        {
            foreach (var go in nodeObjs.Values) Destroy(go);
            nodeObjs.Clear();
            foreach (var n in Sim.Gd.Nodes)
            {
                if (!Sim.Unlocked(n.Zone) || (n.Kind != "building" && n.Kind != "site")) continue;
                var b = Sim.BuildingAt(n.Index);
                var pos = NodePos(n.Index) + new Vector3(0, 0.1f, 9f);
                GameObject go;
                if (b == null) go = Prop("plot", MeshKit.Plot, pos, 0);
                else if (!b.Built) go = Prop("scaffold", MeshKit.Scaffold, pos, 0);
                else { string def = b.Def; go = Prop(def, () => MeshKit.Building(def), pos, 0); }
                go.name = "Node_" + n.Id;
                nodeObjs[n.Index] = go;
            }
            ApplyTheme();
        }

        void BuildOak()
        {
            if (oak != null) { Destroy(oak); oak = null; }
            if (Low) return;   // hero prop: Medium / High only
            var prefab = Resources.Load<GameObject>("kit/oak");
            int green = Sim.Gd.Node("j_green");
            if (prefab == null || green < 0) return;
            oak = Instantiate(prefab, transform);
            var tex = Resources.Load<Texture2D>("kit/oak_base");
            var m = new Material(mat) { name = "oak" };
            if (tex != null) { m.mainTexture = tex; if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex); }
            foreach (var r in oak.GetComponentsInChildren<Renderer>()) r.sharedMaterial = m;
            oak.transform.localPosition = NodePos(green) + new Vector3(-14f, 0, -10f);
            oak.transform.localScale = Vector3.one * 2.2f;
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
        GameObject Prop(string kitName, Func<Mesh> fallback, Vector3 pos, float yaw)
        {
            var prefab = Resources.Load<GameObject>("kit/" + kitName);
            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, transform);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = mat; r.shadowCastingMode = Low ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On; }
                go.transform.localPosition = pos;
                go.transform.localScale = Vector3.one * PropScale;
            }
            else go = Spawn(kitName, fallback(), pos);
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            return go;
        }

        public void SetTheme(string theme) { Theme = theme ?? ""; ApplyTheme(); }

        void ApplyTheme()
        {
            Color tint = Theme switch
            {
                "gold_trim" => new Color(1f, 0.92f, 0.7f),
                "theme_ivy" => new Color(0.78f, 0.95f, 0.75f),
                "theme_modern" => new Color(0.8f, 0.92f, 1f),
                "theme_desert" => new Color(1f, 0.88f, 0.7f),
                _ => Color.white,
            };
            foreach (var go in nodeObjs.Values)
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    r.GetPropertyBlock(mpb);
                    mpb.SetColor("_BaseColor", tint);
                    r.SetPropertyBlock(mpb);
                }
        }

        public List<Bounds> UnlockedBounds()
        {
            var o = new List<Bounds>();
            var b = new Bounds(NodePos(Sim.Gd.EntranceNode), Vector3.one * 40f);
            foreach (var n in Sim.Gd.Nodes) if (Sim.Unlocked(n.Zone)) b.Encapsulate(NodePos(n.Index));
            b.Expand(new Vector3(50, 0, 50));
            o.Add(b);
            return o;
        }

        // ------------------------------------------------------------------ frame

        string StructureKey()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(string.Join(",", Sim.S.Zones)).Append('|');
            foreach (var b in Sim.S.Buildings) sb.Append(b.Node).Append(b.Built ? 'b' : 'c');
            sb.Append('|').Append(string.Join(",", Sim.S.OpenGates)).Append('|').Append(string.Join(",", Sim.S.Widened)).Append(Sim.Has("shuttle") ? "s" : "");
            return sb.ToString();
        }

        void Update()
        {
            if (Sim == null) return;
            if (dirty)
            {
                dirty = false;
                string key = StructureKey();
                if (key != builtKey)
                {
                    bool zonesChanged = builtKey.Split('|')[0] != key.Split('|')[0] || zoneObjs.Count == 0;
                    if (zonesChanged)
                    {
                        foreach (var z in Sim.Gd.Zones) BuildZone(z);
                        if (builtKey != "" && rig != null) rig.Fit(UnlockedBounds(), false);
                        BuildOak();
                    }
                    BuildPaths();
                    BuildNodes();
                    builtKey = key;
                }
            }
            float dt = Time.deltaTime;
            slowAcc += dt;
            if (!Low || slowAcc >= 1f / 15f)
            {
                slowAcc = 0f;
                crowd.Tick();
            }
            crowd.Draw();
        }

        /// <summary>Sim events -> structural rebuilds.</summary>
        public void OnSimEvent(SimEvent e)
        {
            switch (e.Type)
            {
                case "zone": case "build_start": case "built": case "gate": case "bought": case "research_done": case "week_start": MarkDirty(); break;
            }
        }

        // ------------------------------------------------------------------ picking

        void OnTap(Vector2 topLeft)
        {
            var hit = Pick(new Vector2(topLeft.x, Screen.height - topLeft.y));
            Picked?.Invoke(hit);
        }

        /// <summary>Screen px (bottom-left origin) -> what is there. Buildings / plots win over gates.</summary>
        public WorldHit Pick(Vector2 screen)
        {
            var none = new WorldHit { Kind = "none", Index = -1 };
            if (cam == null || Sim == null) return none;
            var ray = cam.ScreenPointToRay(screen);
            if (Mathf.Abs(ray.direction.y) < 1e-4f) return none;
            float dist = -ray.origin.y / ray.direction.y;
            if (dist < 0) return none;
            var g = ray.origin + ray.direction * dist;
            float pickR = 26f * Mathf.Max(0.6f, rig != null ? rig.Zoom : 1f);
            float best = pickR * pickR;
            var res = none;
            foreach (var n in Sim.Gd.Nodes)
            {
                if (!Sim.Unlocked(n.Zone) || (n.Kind != "building" && n.Kind != "site")) continue;
                float d = (NodePos(n.Index) + new Vector3(0, 0, 9f) - g).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                var b = Sim.BuildingAt(n.Index);
                string kind = b == null ? "site" : b.Built && Sim.DefOf(b).Teaching ? "hall" : "building";
                res = new WorldHit { Kind = kind, Index = n.Index };
            }
            if (res.Kind != "none") return res;
            best = pickR * pickR * 0.6f;
            foreach (var e in Sim.Gd.Edges)
            {
                if (e.Type != "gate" || !Sim.EdgeOpen(e) && !(Sim.Unlocked(Sim.Gd.Nodes[e.A].Zone) && Sim.Unlocked(Sim.Gd.Nodes[e.B].Zone))) continue;
                float d = (EdgeMid(e.Index) - g).sqrMagnitude;
                if (d < best) { best = d; res = new WorldHit { Kind = "gate", Index = e.Index }; }
            }
            return res;
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
