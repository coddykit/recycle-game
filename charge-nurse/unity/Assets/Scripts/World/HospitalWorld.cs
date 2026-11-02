// Charge Nurse — the 2.5D cut-away campus (plan §8). Built from map.json (1 unit = 1 m): every room is a floor with tall
// back walls and a knee-high front wall (the camera looks in from the south, roof off), joined by corridors. Locked wings
// are closed grey blocks. Rebuilt only when something structural changes (a wing, a bed, a room); people update from the
// sim each frame (Low: 15 Hz).
//   * one palette material for everything (MeshKit) + instanced people; a hospital theme swaps the palette texture.
//   * the corridors are ONE mesh whose UVs carry the flow heat map (calm / busy / jammed) and the lens's red line —
//     0 extra draws (plan §8 low-profile plan).
//   * Low tier: no shadows (QualityTiers), ≤ 28 people at 15 Hz, no decorative props (plants, trees thinned).
//   * picking: a tap ray-casts the ground plane (no physics module) and picks the nearest waiting patient / bed / room.
using System;
using System.Collections.Generic;
using CoddyKit;
using UnityEngine;
using ChargeNurse.Sim;

namespace ChargeNurse.World
{
    public struct WorldHit
    {
        public string Kind;   // patient | bed | zone | none
        public int Index;     // patient id / bed index / zone index
        public override string ToString() => Kind + ":" + Index;
    }

    public sealed class HospitalWorld : MonoBehaviour
    {
        public Camera cam;
        public CameraRig rig;
        public TapInput tap;
        public Light sun;

        public event Action<WorldHit> Picked;
        public Simulation Sim { get; private set; }
        public bool Low { get; private set; }
        public string Theme { get; private set; } = "";

        Material mat, matInst;
        Texture2D basePalette;
        readonly List<GameObject> built = new List<GameObject>();
        Crowd crowd;
        Mesh corridorMesh;
        List<Vector2> corridorUv;
        readonly List<int> corridorEdge = new List<int>();   // corridor index per strip (4 vertices each)
        int builtBeds = -1, builtRooms = -1, builtWings = -1, builtClosed = -1;
        bool dirty = true;
        float slowAcc, heatAcc, lensLeft, ambT = -1;
        int lensZone = -1;
        Transform ambulance;
        Vector3 ambPark;

        public static Vector3 W(double x, double y) => new Vector3((float)x, 0f, (float)y);
        public Vector3 ZoneCenter(int z) => z >= 0 && z < Sim.Gd.Zones.Count ? W(Sim.Gd.Zones[z].X, Sim.Gd.Zones[z].Y) : Vector3.zero;
        public Vector3 Entrance => ZoneCenter(Sim.Gd.Zone("reception")) + new Vector3(0, 0, -4f);
        public Vector3 AmbulanceBay => ZoneCenter(Sim.Gd.Zone("amb"));

        public void Init(Simulation sim, bool low)
        {
            Sim = sim;
            Low = low;
            mat = Resources.Load<Material>(low ? "materials/palette_low" : "materials/palette") ?? Resources.Load<Material>("materials/palette");
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")); mat.mainTexture = Resources.Load<Texture2D>("materials/palette"); }
            mat = new Material(mat) { name = "palette_world" };   // own instance: the theme swaps its texture
            basePalette = mat.mainTexture as Texture2D;
            matInst = new Material(mat) { enableInstancing = true, name = "palette_instanced" };
            crowd = new Crowd(this, matInst, low);
            if (tap != null) tap.Tapped += OnTap;
            Rebuild();
            if (rig != null) rig.Fit(OpenBounds(), true);
        }

        public void SetLow(bool low)
        {
            if (low == Low) return;
            Low = low;
            crowd?.SetLow(low);
            MarkDirty();
        }

        public void MarkDirty() => dirty = true;

        /// <summary>Hospital theme (cosmetic IAP): a palette swap — zero extra draws, no new shader.</summary>
        public void SetTheme(string theme)
        {
            Theme = theme ?? "";
            var tex = Theme == "" ? basePalette : Resources.Load<Texture2D>("materials/palette_" + Theme.Replace("theme_", "").Replace("gold_theme", "gold"));
            if (tex == null) tex = basePalette;
            foreach (var m in new[] { mat, matInst })
            {
                if (m == null) continue;
                m.mainTexture = tex;
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            }
        }

        /// <summary>The bottleneck lens: a red line on the corridors into this zone for a few seconds.</summary>
        public void ShowLens(int zone)
        {
            lensZone = zone;
            lensLeft = 8f;
            heatAcc = 1f;
        }

        // ------------------------------------------------------------------ layout

        public Vector3 SlotPos(int zone, int slot)
        {
            var z = Sim.Gd.Zones[zone];
            int slots = Math.Max(1, z.Slots);
            int cols = Math.Max(1, (slots + 1) / 2);
            int col = slot % cols, row = slot / cols;
            float sx = (float)z.W / cols;
            float x = (float)(z.X - z.W * 0.5) + sx * (col + 0.5f);
            float y = (float)z.Y + (row == 0 ? 1.6f : -1.9f) * (float)Math.Min(1.0, z.D / 9.0);
            return new Vector3(x, 0, y);
        }

        public Vector3 BedPos(int bed) => bed >= 0 && bed < Sim.S.Beds.Count ? SlotPos(Sim.S.Beds[bed].Zone, Sim.S.Beds[bed].Slot) : Vector3.zero;
        public Vector3 MachinePos(int zone) => ZoneCenter(zone) + new Vector3(0, 0, 1.2f);

        /// <summary>The waiting hall's seats (2 rows of 4), then standing room along the corridor (the visible overflow).</summary>
        public Vector3 WaitSpot(int k)
        {
            var c = ZoneCenter(Sim.Gd.Zone("waiting"));
            if (k < 8) return c + new Vector3(-2.7f + (k % 4) * 1.8f, 0, (k < 4 ? 1.8f : -0.6f));
            int j = k - 8;
            return ZoneCenter(Sim.Gd.Zone("hall_c")) + new Vector3(-6f + (j % 10) * 1.0f, 0, -0.4f - (j / 10) * 1.0f);
        }

        /// <summary>A queue spot at a room's door.</summary>
        public Vector3 QueueSpot(int zone, int k)
        {
            if (zone < 0) return WaitSpot(k);
            var z = Sim.Gd.Zones[zone];
            return W(z.X, z.Y - z.D * 0.5 + 0.8) + new Vector3(-1.5f + (k % 4) * 1.0f, 0, -(k / 4) * 1.0f);
        }

        /// <summary>Named spots in a zone: k = -1 the desk, 0.. a line / seats, 10.. idle staff places.</summary>
        public Vector3 Spot(string zoneId, int k)
        {
            int zi = Sim.Gd.Zone(zoneId);
            var c = ZoneCenter(zi);
            if (k < 0) return c + new Vector3(0, 0, 1.4f);
            if (k >= 10) return c + new Vector3(-2f + (k - 10) * 1.3f, 0, 2.4f);
            return c + new Vector3(-1.5f + (k % 4) * 1.0f, 0, -0.8f - (k / 4) * 1.0f);
        }

        public List<Bounds> OpenBounds()
        {
            var b = new Bounds(ZoneCenter(Sim.Gd.Zone("reception")), Vector3.one * 10f);
            foreach (var z in Sim.Gd.Zones) if (Sim.Unlocked(z.Wing)) b.Encapsulate(new Bounds(W(z.X, z.Y), new Vector3((float)z.W, 1, (float)z.D)));
            b.Expand(new Vector3(6, 0, 6));
            return new List<Bounds> { b };
        }

        // ------------------------------------------------------------------ build

        GameObject Spawn(string name, Mesh mesh, Vector3 pos, float yaw = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = Low ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows = !Low;
            built.Add(go);
            return go;
        }

        /// <summary>Blender-kit model when present (Resources/kit/&lt;name&gt;), else the procedural fallback.</summary>
        GameObject Prop(string kitName, Func<Mesh> fallback, Vector3 pos, float yaw)
        {
            var prefab = Resources.Load<GameObject>("kit/" + kitName);
            if (prefab == null) return Spawn(kitName, fallback(), pos, yaw);
            var go = Instantiate(prefab, transform);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = Low ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = !Low;
            }
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            built.Add(go);
            return go;
        }

        void Rebuild()
        {
            foreach (var go in built) if (go != null) Destroy(go);
            built.Clear();
            var gd = Sim.Gd;
            // grounds: lawn, paving under the campus, trees along the edge (thinned on Low)
            var g = new MeshKit();
            g.Floor(new Vector3(45, 0, 5), 160, 90, 0f, Pal.Grass);
            g.Floor(new Vector3(45, 0, 4), 104, 42, 0.02f, Pal.Paving);
            var rng = new System.Random(7);
            for (float x = -8; x < 100; x += Low ? 9f : 4.5f)
            {
                g.Tree(new Vector3(x + (float)rng.NextDouble(), 0, 28 + (float)rng.NextDouble() * 3), 1.2f, (float)rng.NextDouble() * 90);
                g.Tree(new Vector3(x + 2 + (float)rng.NextDouble(), 0, -19 - (float)rng.NextDouble() * 3), 1.0f, (float)rng.NextDouble() * 90);
            }
            // rooms (open wings furnished; locked wings are closed blocks)
            foreach (var z in gd.Zones)
            {
                if (z.Kind == "hall") continue;
                bool open = Sim.Unlocked(z.Wing);
                int floor = z.Kind == "or" ? Pal.OrFloor : z.Kind == "ambulance" ? Pal.Concrete : Pal.Floor;
                int wall = z.Kind == "ward" ? Pal.WallMint : z.Kind == "test" || z.Kind == "or" ? Pal.WallBlue : Pal.WallCream;
                if (z.Kind == "ambulance") { g.Floor(W(z.X, z.Y), (float)z.W, (float)z.D, 0.05f, open ? Pal.Concrete : Pal.Locked); continue; }
                g.Room(W(z.X, z.Y), (float)z.W, (float)z.D, floor, wall, open);
            }
            Spawn("Campus", g.Build("campus"), Vector3.zero);
            BuildCorridors();
            // furniture
            foreach (var z in gd.Zones)
            {
                if (!Sim.Unlocked(z.Wing)) continue;
                var c = W(z.X, z.Y);
                switch (z.Kind)
                {
                    case "reception": Prop("desk", MeshKit.Desk, c + new Vector3(0, 0, 1.2f), 0); break;
                    case "triage": Prop("desk", MeshKit.Desk, c + new Vector3(0, 0, 1.2f), 0); break;
                    case "waiting":
                        Prop("chairs", () => MeshKit.Chairs(4), c + new Vector3(0, 0, 1.8f), 0);
                        Prop("chairs", () => MeshKit.Chairs(4), c + new Vector3(0, 0, -0.6f), 0);
                        break;
                    case "lounge":
                        if (Sim.Has("discharge_lounge")) Prop("chairs", () => MeshKit.Chairs(4), c + new Vector3(0, 0, 0.6f), 0);
                        break;
                    case "lift": Spawn("Lift", BoxMesh(Pal.Machine, new Vector3(3, 3, 3)), c, 0); break;
                }
            }
            for (int i = 0; i < Sim.S.Beds.Count; i++)
            {
                var b = Sim.S.Beds[i];
                Prop(b.Bay ? "bay" : "bed", () => MeshKit.Bed(b.Bay), SlotPos(b.Zone, b.Slot), 0).name = "Bed_" + i;
                if (b.State == BedState.Closed) Spawn("Closed_" + i, BoxMesh(Pal.TagYellow, new Vector3(1.2f, 0.9f, 0.1f)), SlotPos(b.Zone, b.Slot) + new Vector3(0, 0, -1.2f));
            }
            foreach (var r in Sim.S.Rooms) Prop(r.Kind == "or" ? "or_table" : r.Kind, () => MeshKit.Machine(r.Kind), MachinePos(r.Zone), 0);
            var amb = Prop("ambulance", MeshKit.Ambulance, AmbulanceBay, 90);
            ambulance = amb.transform;
            ambPark = AmbulanceBay;
            builtBeds = Sim.S.Beds.Count; builtRooms = Sim.S.Rooms.Count; builtWings = Sim.S.Wings.Count; builtClosed = ClosedBeds();
            heatAcc = 1f;
        }

        static Mesh BoxMesh(int swatch, Vector3 size) { var k = new MeshKit(); k.Box(Vector3.zero, size, 0, swatch); return k.Build("box_" + swatch); }

        int ClosedBeds() { int n = 0; foreach (var b in Sim.S.Beds) if (b.State == BedState.Closed) n++; return n; }

        void BuildCorridors()
        {
            var gd = Sim.Gd;
            var k = new MeshKit();
            corridorEdge.Clear();
            for (int i = 0; i < gd.Corridors.Count; i++)
            {
                var c = gd.Corridors[i];
                bool open = Sim.Unlocked(gd.Zones[c[0]].Wing) && Sim.Unlocked(gd.Zones[c[1]].Wing);
                k.Strip(ZoneCenter(c[0]), ZoneCenter(c[1]), 2.2f, 0.08f, open ? Pal.Corridor : Pal.Locked);
                corridorEdge.Add(i);
            }
            corridorMesh = k.BuildDynamic("corridors", out corridorUv);
            Spawn("Corridors", corridorMesh, Vector3.zero);
        }

        /// <summary>Heat per corridor from the queues at both ends (plan §8: corridors light up as a flow map).</summary>
        void UpdateHeat()
        {
            if (corridorMesh == null || corridorUv == null) return;
            var gd = Sim.Gd;
            var load = new int[gd.Zones.Count];
            foreach (var p in Sim.S.Patients)
                if (Stage.PreDoctor(p.Stage) || p.Stage == Stage.Board || p.Stage == Stage.Ready || p.Stage == Stage.WardReady)
                {
                    int z = Sim.ZoneOfPatient(p);
                    if (z >= 0) load[z]++;
                }
            for (int i = 0; i < corridorEdge.Count; i++)
            {
                var c = gd.Corridors[corridorEdge[i]];
                bool open = Sim.Unlocked(gd.Zones[c[0]].Wing) && Sim.Unlocked(gd.Zones[c[1]].Wing);
                int h = Math.Max(load[c[0]], load[c[1]]);
                int sw = !open ? Pal.Locked : (lensLeft > 0 && (c[0] == lensZone || c[1] == lensZone)) || h >= 7 ? Pal.TagRed : h >= 4 ? Pal.TagOrange : h >= 2 ? Pal.TagYellow : Pal.Corridor;
                var uv = Pal.Uv(sw);
                for (int v = 0; v < 4; v++) corridorUv[i * 4 + v] = uv;
            }
            corridorMesh.SetUVs(0, corridorUv);
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            if (Sim == null) return;
            if (dirty || Sim.S.Beds.Count != builtBeds || Sim.S.Rooms.Count != builtRooms || Sim.S.Wings.Count != builtWings || ClosedBeds() != builtClosed)
            {
                bool refit = builtWings >= 0 && Sim.S.Wings.Count != builtWings;
                dirty = false;
                Rebuild();
                if (refit && rig != null) rig.Fit(OpenBounds(), false);
            }
            float dt = Time.deltaTime;
            if (lensLeft > 0) { lensLeft -= dt; if (lensLeft <= 0) heatAcc = 1f; }
            heatAcc += dt;
            if (heatAcc >= 1f) { heatAcc = 0; UpdateHeat(); }
            slowAcc += dt;
            bool tick = !Low || slowAcc >= 1f / 15f;
            if (tick)
            {
                float step = Low ? slowAcc : dt;
                slowAcc = 0f;
                crowd.Tick(Sim, step);
                MoveAmbulance(step);
            }
            crowd.Draw();
        }

        void MoveAmbulance(float dt)
        {
            if (ambulance == null || ambT < 0) return;
            ambT += dt / 3f;
            var from = ambPark + new Vector3(30f, 0, -14f);
            float k = Mathf.SmoothStep(0, 1, Mathf.Min(1, ambT));
            ambulance.localPosition = Vector3.Lerp(from, ambPark, k);
            if (ambT >= 1f) ambT = -1;
        }

        /// <summary>Sim events -> world (the ambulance drives in, structure changes rebuild).</summary>
        public void OnSimEvent(SimEvent e)
        {
            switch (e.Type)
            {
                case "ambulance": ambT = 0; break;
                case "wing": case "room": case "bought": case "day_start": case "research_done": MarkDirty(); break;
            }
        }

        // ------------------------------------------------------------------ picking

        void OnTap(Vector2 topLeft)
        {
            var hit = Pick(new Vector2(topLeft.x, Screen.height - topLeft.y));
            Picked?.Invoke(hit);
        }

        /// <summary>Screen px (bottom-left origin) -> what is there. Waiting patients win over beds, beds over rooms.</summary>
        public WorldHit Pick(Vector2 screen)
        {
            var none = new WorldHit { Kind = "none", Index = -1 };
            if (cam == null || Sim == null) return none;
            var ray = cam.ScreenPointToRay(screen);
            if (Mathf.Abs(ray.direction.y) < 1e-4f) return none;
            float dist = -ray.origin.y / ray.direction.y;
            if (dist < 0) return none;
            var g = ray.origin + ray.direction * dist;
            float pickR = 1.6f * Mathf.Max(0.7f, rig != null ? rig.Zoom : 1f);
            float best = pickR * pickR;
            var res = none;
            foreach (var p in Sim.Waiting())
            {
                if (!crowd.PatientPos(p.Id, out var pos)) continue;
                float d = (pos - g).sqrMagnitude;
                if (d < best) { best = d; res = new WorldHit { Kind = "patient", Index = p.Id }; }
            }
            if (res.Kind == "patient") return res;
            best = pickR * pickR * 1.4f;
            for (int i = 0; i < Sim.S.Beds.Count; i++)
            {
                float d = (BedPos(i) - g).sqrMagnitude;
                if (d < best) { best = d; res = new WorldHit { Kind = "bed", Index = i }; }
            }
            if (res.Kind == "bed") return res;
            foreach (var z in Sim.Gd.Zones)
                if (Mathf.Abs(g.x - (float)z.X) < z.W * 0.5f && Mathf.Abs(g.z - (float)z.Y) < z.D * 0.5f) return new WorldHit { Kind = "zone", Index = z.Index };
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

        /// <summary>Where a patient's figure is now (waiting chips follow it); falls back to the seat.</summary>
        public Vector3 PatientWorld(int id, int waitIndex) => crowd != null && crowd.PatientPos(id, out var p) ? p : WaitSpot(waitIndex);
    }
}
