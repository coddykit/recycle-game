// Pour & Roast — the 2.5D cutaway diorama of the active venue (design §8): the quayside cart under its awning, the corner
// cafe (back + left wall cut away, counter, back bar, pastry case, tables) or the roastery hall (the bar + a back room with
// the drum roaster, resting racks, sacks and the packing bench). Built ONCE per venue from the layout below; rebuilt only
// when something structural changes (venue switch, a group head / table / plant bought, a theme); baristas, customers and
// the cups on the pickup shelf update from the sim each frame (Low: 15 Hz, ≤ 14 customers).
//   * 1 unit = 1 m; one palette material for everything (MeshKit) + instanced draws for figures and cups;
//   * Low tier: no shadows (QualityTiers), customers ≤ 14 at 15 Hz, no weather light change, static window backdrop;
//   * picking: a tap ray-casts the floor plane (no physics module) and picks the nearest station / barista.
using System;
using System.Collections.Generic;
using CoddyKit;
using UnityEngine;
using PourAndRoast.Sim;

namespace PourAndRoast.World
{
    public struct WorldHit
    {
        public string Kind;   // station | staff | none
        public string Id;     // station id (espresso, grinder, fridge, shelf, pastry, ice, roaster) / staff id
        public override string ToString() => Kind + ":" + Id;
    }

    public sealed class CafeWorld : MonoBehaviour
    {
        public Camera cam;
        public CameraRig rig;
        public TapInput tap;
        public Light sun;

        public event Action<WorldHit> Picked;
        public Simulation Sim { get; private set; }
        public bool Low { get; private set; }
        public string Theme = "";
        public bool GoldTrim;
        public int ApronSwatch = Pal.Apron, CupSwatch = Pal.Brand;

        Material mat, matInst;
        readonly List<GameObject> built = new List<GameObject>();
        readonly Dictionary<string, Vector3> stations = new Dictionary<string, Vector3>();
        Crowd crowd;
        Mesh cupMesh;
        readonly List<Matrix4x4> cups = new List<Matrix4x4>();
        string builtKey = "";
        bool dirty = true;
        float slowAcc;
        MaterialPropertyBlock mpb;
        GameObject espressoGo;

        public Vector3 StationPos(string id) => stations.TryGetValue(id ?? "", out var p) ? p : Vector3.zero;
        public bool HasStation(string id) => stations.ContainsKey(id ?? "");
        public IEnumerable<string> StationIds => stations.Keys;

        public void Init(Simulation sim, bool low)
        {
            Sim = sim;
            Low = low;
            mpb = new MaterialPropertyBlock();
            mat = Resources.Load<Material>(low ? "materials/palette_low" : "materials/palette") ?? Resources.Load<Material>("materials/palette");
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")); mat.mainTexture = Resources.Load<Texture2D>("materials/palette"); }
            matInst = new Material(mat) { enableInstancing = true, name = "palette_instanced" };
            cupMesh = MeshKit.CupMesh(CupSwatch);
            crowd = new Crowd(this, matInst, low);
            if (tap != null) tap.Tapped += OnTap;
            MarkDirty();
        }

        /// <summary>A new career (save slot switch) in the same scene.</summary>
        public void Rebind(Simulation sim) { Sim = sim; crowd?.Clear(); builtKey = ""; MarkDirty(); }

        public void SetLow(bool low)
        {
            if (low == Low) return;
            Low = low;
            crowd?.SetLow(low);
            builtKey = "";
            MarkDirty();
        }

        public void SetLooks(string theme, bool goldTrim, bool aprons, bool cupsSkin)
        {
            Theme = theme ?? "";
            GoldTrim = goldTrim;
            ApronSwatch = aprons ? Pal.ApronWine : Pal.Apron;
            int cs = cupsSkin ? Pal.Teal : Pal.Brand;
            if (cs != CupSwatch && cupMesh != null) { CupSwatch = cs; cupMesh = MeshKit.CupMesh(cs); }
            crowd?.SetApron(ApronSwatch);
            builtKey = "";
            MarkDirty();
        }

        public void MarkDirty() => dirty = true;

        // ------------------------------------------------------------------ layout

        sealed class Layout
        {
            public float X0, X1, Z0, Z1;
            public bool Cart, Roastery;
            public float CounterX, CounterW, BarZ = 2.4f, FrontZ = 0.8f, TopY = 0.9f;
            public Vector3[] Tables = new Vector3[0];
            public Vector3 Exit, Door;
        }

        Layout LayoutFor(string venue)
        {
            switch (venue)
            {
                case "cart":
                    return new Layout
                    {
                        X0 = -5, X1 = 5, Z0 = -4.5f, Z1 = 4, Cart = true, CounterX = 0, CounterW = 2.6f, BarZ = 1.15f, FrontZ = 0.45f, TopY = 1.1f,
                        Tables = new[] { new Vector3(-3.0f, 0, -2.2f), new Vector3(3.0f, 0, -2.6f) }, Exit = new Vector3(5.5f, 0, -4.5f), Door = new Vector3(-5.5f, 0, -3.5f),
                    };
                case "roastery":
                    return new Layout
                    {
                        X0 = -7, X1 = 11, Z0 = -4.5f, Z1 = 5, Roastery = true, CounterX = 0, CounterW = 6,
                        Tables = new[] { new Vector3(-5f, 0, -1.6f), new Vector3(-2.5f, 0, -3.0f), new Vector3(0.5f, 0, -2.0f), new Vector3(3.2f, 0, -3.2f), new Vector3(-5.2f, 0, -3.6f) },
                        Exit = new Vector3(5f, 0, -5f), Door = new Vector3(5f, 0, -5f),
                    };
                default:
                    return new Layout
                    {
                        X0 = -6, X1 = 6, Z0 = -4.5f, Z1 = 5, CounterX = 0.5f, CounterW = 5,
                        Tables = new[] { new Vector3(-4.2f, 0, -1.5f), new Vector3(-1.4f, 0, -2.8f), new Vector3(1.6f, 0, -2.2f), new Vector3(4.3f, 0, -3.2f) },
                        Exit = new Vector3(5.5f, 0, -5f), Door = new Vector3(5.5f, 0, -5f),
                    };
            }
        }

        Layout lay;
        public Vector3 QueuePos(int i) => new Vector3(lay.CounterX - 0.6f + (i % 2) * 0.55f, 0, lay.FrontZ - 1.0f - (i / 2) * 0.75f);
        public Vector3 WaitPos(int i) => StationPos("shelf") + new Vector3(0.45f * (i % 3) - 0.45f, -StationPos("shelf").y, -0.85f - 0.6f * (i / 3));
        public Vector3 ExitPos => lay != null ? lay.Exit : new Vector3(6, 0, -5);
        public Vector3 DoorPos => lay != null ? lay.Door : new Vector3(6, 0, -5);
        public Vector3 BaristaPos(string station, int slot)
        {
            Vector3 p = station == Stations.Milk ? StationPos("milk") : station == Stations.Espresso ? StationPos("espresso") : StationPos("grinder");
            float z = lay.Cart ? lay.BarZ + 0.75f : (lay.BarZ + lay.FrontZ) * 0.5f + 0.1f;
            return new Vector3(p.x + slot * 0.5f, 0, z);
        }
        public Vector3 CashierPos => new Vector3(StationPos("shelf").x - 0.3f, 0, lay.Cart ? lay.BarZ + 0.75f : lay.FrontZ + 0.6f);
        public Vector3 RoasterStaffPos => StationPos("roaster") + new Vector3(-0.9f, -StationPos("roaster").y, -0.6f);

        int WallSwatch => Theme == "theme_nordic" ? Pal.Nordic : Theme == "theme_artdeco" ? Pal.Deco : Pal.WallCream;
        int FloorSwatch => Theme == "theme_nordic" ? Pal.Milk : Theme == "theme_artdeco" ? Pal.Slate : Pal.FloorWood;
        int TrimSwatch => Theme == "theme_artdeco" ? Pal.DecoGold : Pal.Trim;

        void Build()
        {
            foreach (var go in built) Destroy(go);
            built.Clear();
            stations.Clear();
            var v = Sim.Venue;
            lay = LayoutFor(Sim.S.Venue);
            var k = new MeshKit();
            // ground: quay + sea for the cart; a floor + two cutaway walls + windows for the cafes
            if (lay.Cart)
            {
                k.Floor(new Vector3(0, 0, 0), new Vector2(lay.X1 - lay.X0 + 4, lay.Z1 - lay.Z0 + 2), 0, Pal.Quay);
                for (int i = 0; i < 6; i++) k.Floor(new Vector3(lay.X0 + 1 + i * 1.7f, 0, lay.Z0 + 0.8f), new Vector2(1.5f, 0.6f), 0.005f, Pal.QuayDark);
                k.Floor(new Vector3(0, -0.3f, lay.Z1 + 8), new Vector2(60, 14), -0.3f, Pal.Sea);
                k.Box(new Vector3(0, 0, lay.Z1 + 0.6f), new Vector3(lay.X1 - lay.X0 + 4, 0.6f, 0.4f), 0, Pal.QuayDark);   // quay edge
                for (int i = -1; i <= 1; i += 2) k.Box(new Vector3(i * 4.2f, 0, lay.Z1 + 0.3f), new Vector3(0.3f, 0.9f, 0.3f), 0, Pal.Black);   // bollards
            }
            else
            {
                k.Floor(new Vector3((lay.X0 + lay.X1) * 0.5f, 0, (lay.Z0 + lay.Z1) * 0.5f), new Vector2(lay.X1 - lay.X0, lay.Z1 - lay.Z0), 0, FloorSwatch);
                k.Floor(new Vector3((lay.X0 + lay.X1) * 0.5f, -0.02f, (lay.Z0 + lay.Z1) * 0.5f), new Vector2(lay.X1 - lay.X0 + 30, lay.Z1 - lay.Z0 + 30), -0.02f, Pal.Quay);
                float w = lay.X1 - lay.X0;
                k.Box(new Vector3((lay.X0 + lay.X1) * 0.5f, 0, lay.Z1 + 0.1f), new Vector3(w, 3.2f, 0.2f), 0, WallSwatch, TrimSwatch);   // back wall
                k.Box(new Vector3(lay.X0 - 0.1f, 0, (lay.Z0 + lay.Z1) * 0.5f), new Vector3(0.2f, 3.2f, lay.Z1 - lay.Z0), 0, WallSwatch, TrimSwatch);   // left wall
                k.Box(new Vector3((lay.X0 + lay.X1) * 0.5f, 0, lay.Z1 - 0.01f), new Vector3(w, 0.9f, 0.04f), 0, TrimSwatch);   // wainscot
                for (int i = 0; i < 3; i++) k.Box(new Vector3(lay.X0 + 0.02f, 1.2f, lay.Z0 + 1.6f + i * 2.4f), new Vector3(0.04f, 1.3f, 1.6f), 0, Pal.Window);   // windows (harbour light)
                k.Box(new Vector3(lay.X0 + 2.2f, 1.5f, lay.Z1 - 0.02f), new Vector3(2.2f, 1.2f, 0.04f), 0, Pal.Chalk);   // chalk menu board (no text)
                if (lay.Roastery) k.Box(new Vector3(5.6f, 0, 2.2f), new Vector3(0.15f, 2.4f, 5.6f), 0, TrimSwatch, Pal.Walnut);   // half wall to the back room
            }
            Spawn("Shell", k.Build("shell_" + Sim.S.Venue), Vector3.zero);

            // the bar
            float cx = lay.CounterX;
            if (lay.Cart) Prop("cart", MeshKit.Cart, new Vector3(cx, 0, 1.0f), 0);
            else
            {
                Prop("counter", () => MeshKit.Counter(lay.CounterW), new Vector3(cx, 0, lay.FrontZ), 0, lay.CounterW / 5f);
                Prop("back_bar", () => MeshKit.BackBar(lay.CounterW), new Vector3(cx, 0, lay.BarZ), 0, lay.CounterW / 5f);
            }
            float top = lay.TopY;
            stations["grinder"] = new Vector3(cx - 1.0f, top, lay.BarZ);
            stations["espresso"] = new Vector3(cx + 0.05f, top, lay.BarZ);
            stations["milk"] = new Vector3(cx + 0.05f + 0.25f + 0.18f * Math.Max(1, v.Heads), top, lay.BarZ);
            stations["fridge"] = lay.Cart ? new Vector3(cx + 1.75f, 0, 1.5f) : new Vector3(cx + lay.CounterW * 0.5f - 0.45f, 0, lay.BarZ);
            stations["shelf"] = lay.Cart ? new Vector3(cx + 0.95f, top, 0.6f) : new Vector3(cx + lay.CounterW * 0.5f - 0.5f, 1.0f, lay.FrontZ);
            Prop("grinder", MeshKit.Grinder, stations["grinder"], 0);
            espressoGo = Prop("espresso_" + Mathf.Clamp(v.Heads, 1, 6), () => MeshKit.Espresso(v.Heads), stations["espresso"], 0);
            if (GoldTrim) Tint(espressoGo, new Color(1f, 0.86f, 0.45f));
            Prop("fridge", MeshKit.Fridge, stations["fridge"] + (lay.Cart ? Vector3.zero : new Vector3(0, 0, 0)), 0);
            Prop("pickup_shelf", MeshKit.Shelf, stations["shelf"], 0);
            if (Sim.Tier >= 2)
            {
                stations["pastry"] = new Vector3(cx - lay.CounterW * 0.5f - 0.6f, 0, lay.FrontZ);
                Prop("pastry_case", MeshKit.PastryCase, stations["pastry"], 0);
            }
            if (v.Ice > 0)
            {
                stations["ice"] = new Vector3(cx + lay.CounterW * 0.5f + 0.5f, 0, lay.BarZ);
                Prop("ice_maker", MeshKit.IceMaker, stations["ice"], 0);
            }
            if (lay.Roastery)
            {
                stations["roaster"] = new Vector3(8.2f, 0, 3.2f);
                Prop("roaster", MeshKit.Roaster, stations["roaster"], 0);
                Prop("rest_rack", MeshKit.Rack, new Vector3(10.2f, 0, 3.6f), 0);
                Prop("packing_bench", MeshKit.Bench, new Vector3(8.4f, 0, 0.6f), 0);
                MeshyProp("sack", MeshKit.Sack, new Vector3(6.4f, 0, 4.2f), 15, 0.9f);
                MeshyProp("sack", MeshKit.Sack, new Vector3(7.1f, 0, 4.5f), -20, 0.9f);
                if (!Low) MeshyProp("sack", MeshKit.Sack, new Vector3(6.6f, 0, 3.4f), 70, 0.9f);
            }
            // tables (seating upgrade) and plants (decor)
            for (int i = 0; i < v.Seats && i < lay.Tables.Length; i++) Prop("table", MeshKit.Table, lay.Tables[i], i * 37);
            var plants = new[] { new Vector3(lay.X0 + 0.5f, 0, lay.Z1 - 0.5f), new Vector3(lay.X1 - 0.6f, 0, lay.Z1 - 0.5f), new Vector3(lay.X0 + 0.5f, 0, lay.Z0 + 0.6f) };
            int nPlants = Math.Min(plants.Length, (lay.Cart ? 0 : 1) + v.Decor);
            for (int i = 0; i < nPlants; i++) MeshyProp("plant", MeshKit.Plant, plants[i], i * 50, 1.1f);
            if (!lay.Cart && !Low) MeshyProp("cat", MeshKit.Sack, new Vector3(lay.X0 + 1.2f, 0, -0.8f), 40, 1.4f, false);
            ApplyWeather();
            builtKey = Key();
            if (rig != null) rig.Fit(new List<Bounds> { new Bounds(new Vector3((lay.X0 + lay.X1) * 0.5f, 0, (lay.Z0 + lay.Z1) * 0.5f), new Vector3(lay.X1 - lay.X0, 1, lay.Z1 - lay.Z0)) }, true);
        }

        string Key()
        {
            var v = Sim.Venue;
            return Sim.S.Venue + "|" + v.Heads + "|" + v.Seats + "|" + v.Decor + "|" + v.Ice + "|" + Sim.Tier + "|" + Theme + "|" + GoldTrim + "|" + Low;
        }

        void ApplyWeather()
        {
            if (sun == null) return;
            string w = Sim.Weather;
            sun.intensity = Low ? 1.1f : w == Weathers.Rain ? 0.75f : w == Weathers.Fog ? 0.85f : w == Weathers.Heat ? 1.3f : 1.15f;
            sun.shadows = Low ? LightShadows.None : LightShadows.Soft;
        }

        GameObject Spawn(string name, Mesh mesh, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = Low ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows = !Low;
            built.Add(go);
            return go;
        }

        /// <summary>Blender-kit model when present (Resources/kit/&lt;name&gt;), else the procedural fallback.</summary>
        GameObject Prop(string kitName, Func<Mesh> fallback, Vector3 pos, float yaw, float scaleX = 1f)
        {
            var prefab = Resources.Load<GameObject>("kit/" + kitName);
            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, transform);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = mat; r.shadowCastingMode = Low ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On; }
                go.transform.localPosition = pos;
                go.transform.localScale = new Vector3(scaleX, 1, 1);
                built.Add(go);
            }
            else go = Spawn(kitName, fallback(), pos);
            go.name = kitName;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            return go;
        }

        /// <summary>A Meshy hero prop (own 1024 / Low 512 texture, LOD1 / Low LOD2) when present, else the palette fallback.</summary>
        GameObject MeshyProp(string name, Func<Mesh> fallback, Vector3 pos, float yaw, float scale, bool fallbackOk = true)
        {
            var prefab = Resources.Load<GameObject>("kit/" + name + (Low ? "_lod2" : "_lod1"));
            var tex = Resources.Load<Texture2D>("kit/" + name + (Low ? "_base_low" : "_base"));
            if (prefab == null) return fallbackOk ? Prop(name, fallback, pos, yaw) : null;
            var go = Instantiate(prefab, transform);
            var m = new Material(mat) { name = name };
            if (tex != null) { m.mainTexture = tex; if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex); }
            foreach (var r in go.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = m; r.shadowCastingMode = Low ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On; }
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            go.transform.localScale = Vector3.one * scale;
            go.name = name;
            built.Add(go);
            return go;
        }

        void Tint(GameObject go, Color c)
        {
            if (go == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", c);
                r.SetPropertyBlock(mpb);
            }
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            if (Sim == null) return;
            if (dirty || builtKey != Key())
            {
                dirty = false;
                if (builtKey != Key()) Build();
                else ApplyWeather();
            }
            float dt = Time.deltaTime;
            slowAcc += dt;
            bool tick = !Low || slowAcc >= 1f / 15f;
            if (tick)
            {
                float step = Low ? slowAcc : dt;
                slowAcc = 0f;
                crowd.Tick(step);
                BuildCups();
            }
            if (cups.Count > 0) Graphics.RenderMeshInstanced(new RenderParams(matInst), cupMesh, 0, cups);
            crowd.Draw();
        }

        void BuildCups()
        {
            cups.Clear();
            var shelf = StationPos("shelf");
            int n = 0;
            foreach (var t in Sim.S.Tickets)
            {
                if (!t.Ready) continue;
                foreach (var it in t.Items)
                {
                    if (it.Failed || n >= 12) continue;
                    var d = Sim.ItemDef(it);
                    if (d == null || d.IsPastry) continue;
                    int row = n / 6, col = n % 6;
                    cups.Add(Matrix4x4.TRS(shelf + new Vector3(-0.3f + col * 0.12f, 0.05f + row * 0.3f, 0), Quaternion.identity, Vector3.one));
                    n++;
                }
            }
        }

        /// <summary>Sim events -> customers (order / balk / served / walked).</summary>
        public void OnSimEvent(SimEvent e)
        {
            switch (e.Type)
            {
                case "order": crowd.Arrive(e.A, e.B, e.V > 0); break;
                case "balk": crowd.Balk(e.B); break;
                case "served": crowd.Leave(e.A, true); break;
                case "walked": crowd.Leave(e.A, false); break;
                case "cancelled": crowd.Leave(e.A, false); break;
                case "venue": case "venue_switch": case "bought": case "day_start": case "day_live": case "crisis": MarkDirty(); break;
            }
        }

        // ------------------------------------------------------------------ picking

        void OnTap(Vector2 topLeft)
        {
            var hit = Pick(new Vector2(topLeft.x, Screen.height - topLeft.y));
            Picked?.Invoke(hit);
        }

        /// <summary>Screen px (bottom-left origin) -> what is there. Stations win over baristas.</summary>
        public WorldHit Pick(Vector2 screen)
        {
            var none = new WorldHit { Kind = "none", Id = "" };
            if (cam == null || Sim == null) return none;
            var ray = cam.ScreenPointToRay(screen);
            float best = 0.9f * 0.9f;
            var res = none;
            foreach (var kv in stations)
            {
                // project the ray onto the station's own height plane (machines stand on the counter)
                if (Mathf.Abs(ray.direction.y) < 1e-4f) continue;
                float dist = (kv.Value.y + 0.25f - ray.origin.y) / ray.direction.y;
                if (dist < 0) continue;
                var g = ray.origin + ray.direction * dist;
                float d = (new Vector2(g.x, g.z) - new Vector2(kv.Value.x, kv.Value.z)).sqrMagnitude;
                if (d < best) { best = d; res = new WorldHit { Kind = "station", Id = kv.Key }; }
            }
            if (res.Kind == "station") return res;
            int staff = crowd.PickStaff(ray);
            if (staff >= 0) return new WorldHit { Kind = "staff", Id = staff.ToString() };
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
